using System.Buffers.Binary;
using System.Text.Json;
using MphRead;
using MphRead.Formats;
using MphRecomp.Frontend;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

// Inspection of the user's own extracted assets. Frame and texture previews are never release inputs.
internal static class MediaInventory
{
    public static int Run(string assetRoot, string output, int stride)
    {
        if (stride <= 0) throw new ArgumentOutOfRangeException(nameof(stride));
        assetRoot = Path.GetFullPath(assetRoot);
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        var movies = Directory.GetFiles(Path.Combine(assetRoot, "movies"), "*.vx").Order().ToArray();
        var clips = new Clip[movies.Length];
        Task film = Task.Run(() => Parallel.For(0, movies.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 },
            i => clips[i] = Film(movies[i], Path.Combine(output, "movies"), stride)));
        try
        {
            Textures(assetRoot, Path.Combine(output, "textures"));
        }
        finally
        {
            film.GetAwaiter().GetResult();
        }
        File.WriteAllText(Path.Combine(output, "movies.json"), JsonSerializer.Serialize(clips));
        Console.WriteLine($"Decoded {movies.Length} VX streams; sampled every {stride} frames and at the end of each stream.");
        return 0;
    }

    private sealed record Clip(string Name, int Frames, double Fps, int Samples);

    public static int SingleMovie(string path, string output, int stride)
    {
        if (stride <= 0) throw new ArgumentOutOfRangeException(nameof(stride));
        Console.WriteLine(JsonSerializer.Serialize(Film(path, output, stride)));
        return 0;
    }

    private static Clip Film(string path, string root, int stride)
    {
        string name = Path.GetFileNameWithoutExtension(path), target = Path.Combine(root, name);
        Directory.CreateDirectory(target);
        return Decode(path, (i, frames) => i % stride == 0 || i == frames - 1, (i, pixels) =>
        {
            using var image = Image.LoadPixelData<Rgb24>(pixels, 256, 192);
            image.SaveAsPng(Path.Combine(target, $"{i:0000}.png"));
        });
    }

    internal static Dictionary<int, byte[]> ReadFrames(string path, IEnumerable<int> frames)
    {
        var wanted = frames.ToHashSet();
        var output = new Dictionary<int, byte[]>();
        Decode(path, (i, _) => wanted.Contains(i), (i, pixels) => output.Add(i, (byte[])pixels.Clone()));
        if (output.Count != wanted.Count) throw new InvalidDataException("Frame request outside stream.");
        return output;
    }

    private static Clip Decode(string path, Func<int, int, bool> select, Action<int, byte[]> visit)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        using var file = File.OpenRead(path);
        Span<byte> header = stackalloc byte[20];
        file.ReadExactly(header);
        int frames = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
        int width = BinaryPrimitives.ReadInt32LittleEndian(header[8..]);
        int height = BinaryPrimitives.ReadInt32LittleEndian(header[12..]);
        double fps = BinaryPrimitives.ReadInt32LittleEndian(header[16..]) / 65536d;
        if (frames <= 0 || width != 256 || height != 192 || fps <= 0)
            throw new InvalidDataException(path + ": unexpected VX stream");
        using var cts = new CancellationTokenSource();
        var decoder = new VxDecoder();
        // Long-running producer, since it is backpressured by the frame reader.
        var task = Task.Factory.StartNew(() => decoder.Decode(path, token: cts.Token),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        var pixels = new byte[width * height * 3];
        int samples = 0;
        try
        {
            for (int i = 0; i < frames; i++)
            {
                while (decoder.FramesQueued == 0 || !decoder.GetImage(i, pixels))
                {
                    if (task.IsCompleted)
                    {
                        task.GetAwaiter().GetResult();
                        if (decoder.FramesQueued == 0) throw new InvalidDataException($"{name}: expected frame {i}");
                    }
                    Thread.Sleep(1);
                }
                if (!select(i, frames)) continue;
                visit(i, pixels);
                samples++;
            }
            task.GetAwaiter().GetResult();
        }
        finally { cts.Cancel(); }
        return new Clip(name, frames, fps, samples);
    }

    private sealed record TextureRef(string File, string Model, int Texture, int Palette, int Width, int Height);

    private static void Textures(string root, string output)
    {
        Directory.CreateDirectory(output);
        Paths.SetPath(Ver.AMHE0, root);
        Paths.MphKey = Ver.AMHE0;
        var menu = MenuFile.Load(Path.Combine(root, "frontend", "metroidhunters.bin"));
        var found = new List<TextureRef>();
        var errors = new List<string>();
        foreach (string modelPath in menu.Widgets.Select(w => w.ModelPath).Distinct().Order())
        {
            try
            {
                if (modelPath.StartsWith(@"sourceimages\bg\", StringComparison.OrdinalIgnoreCase))
                {
                    var bitmap = FrontendSession.LoadBitmap(Path.Combine(root, modelPath.Replace('\\', Path.DirectorySeparatorChar)));
                    string file = $"{found.Count:0000}.png";
                    using var image = Image.LoadPixelData<Rgba32>(bitmap.Rgba, bitmap.Width, bitmap.Height);
                    image.SaveAsPng(Path.Combine(output, file));
                    found.Add(new TextureRef(file, modelPath, -1, -1, bitmap.Width, bitmap.Height));
                    continue;
                }
                var model = Read.ReadModelFile(modelPath, modelPath.Replace('\\', '/'), animationPath: null);
                foreach (var (texture, palette) in model.Materials.Select(m => (m.TextureId, m.PaletteId)).Distinct())
                {
                    if (texture < 0) continue;
                    Texture meta = model.Recolors[0].Textures[texture];
                    var pixels = model.GetPixels(texture, palette, recolorId: 0)
                        .Select(c => new Rgba32(c.Red, c.Green, c.Blue, c.Alpha)).ToArray();
                    if (pixels.Length != meta.Width * meta.Height) continue;
                    string file = $"{found.Count:0000}.png";
                    using var image = Image.LoadPixelData<Rgba32>(pixels, meta.Width, meta.Height);
                    image.SaveAsPng(Path.Combine(output, file));
                    found.Add(new TextureRef(file, modelPath, texture, palette, meta.Width, meta.Height));
                }
            }
            catch (Exception ex) { errors.Add(modelPath + ": " + ex.Message); }
        }
        File.WriteAllText(Path.Combine(output, "textures.json"), JsonSerializer.Serialize(found));
        File.WriteAllLines(Path.Combine(output, "read-errors.txt"), errors);
        Console.WriteLine($"Exported {found.Count} menu widget texture/palette pairs ({errors.Count} model read errors).");
    }
}
