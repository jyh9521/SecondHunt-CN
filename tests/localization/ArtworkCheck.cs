using System.Security.Cryptography;
using MphRead;
using MphRead.Text;
using SecondHunt.Desktop;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

internal static class ArtworkCheck
{
    public static int Run(string assets, string? report)
    {
        if (report != null) Directory.CreateDirectory(report);
        DesktopLocale.Init("zh-CN");
        LocalizedArtwork art = LocalizedArtwork.Current ?? throw new InvalidOperationException("No localized artwork.");
        string top = Path.Combine(assets, "movies", "01_top.vx");
        string bottom = Path.Combine(assets, "movies", "01_bot.vx");
        var film = art.MatchFilm(top, bottom) ?? throw new InvalidOperationException("Opening fingerprint mismatch.");
        string[] names = ["赛拉克斯", "韦弗尔", "诺克萨斯", "特雷斯", "斯派尔", "坎登"];
        if (film.Cues.Length != 12) throw new InvalidOperationException("Expected name and details for all six cards.");
        var frames = new Dictionary<(bool Bottom, int Index), byte[]>();
        const int traceTypeFrame = 558;
        foreach (bool side in new[] { false, true })
        {
            var wanted = film.Cues.Where(c => c.Bottom == side).SelectMany(c =>
                new[] { c.Reference, c.LateReference, c.From, c.From + 1, c.From + 64, c.Until - 3, 1100 })
                .Where(i => i >= 0).Concat(side ? [] : new[] { traceTypeFrame });
            foreach (var entry in MediaInventory.ReadFrames(side ? bottom : top, wanted))
                frames.Add((side, entry.Key), entry.Value);
        }
        byte[] scratch = new byte[256 * 192 * 3];
        foreach (string name in names)
        {
            var cue = film.Cues.Single(c => c.Lettering.Any(p => p.Text == name));
            foreach (var (kind, frame) in new[] { ("entry", cue.From), ("flash", cue.From + 1),
                ("settled", cue.From + 64), ("fade", cue.Until - 3) })
            {
                byte[] source = frames[(cue.Bottom, frame)], old = (byte[])source.Clone();
                if (art.MovieFrame(film, frame, film.Frames, cue.Bottom, source, scratch, frames) != scratch
                    || !source.AsSpan().SequenceEqual(old)
                    || !scratch.AsSpan(40 * 256 * 3, 62 * 256 * 3).SequenceEqual(source.AsSpan(40 * 256 * 3, 62 * 256 * 3))
                    || art.MovieFrame(film, frame, film.Frames, !cue.Bottom, source, scratch, frames) != source
                    || art.MovieFrame(film, frame, film.Frames, cue.Bottom, source, scratch) != source)
                    throw new InvalidOperationException("Film preservation or fingerprint gate failed: " + name);
                int changed = ChangedPixels(source, scratch, 3);
                int largestUniformRun = LargestUniformReplacementRun(source, scratch, 256, 3);
                if (changed < 1500 || changed > 18800 || largestUniformRun > 75)
                    throw new InvalidOperationException($"{name} {kind}: {changed} pixels, largest solid replaced run {largestUniformRun}.");
                if (name == "韦弗尔" && kind == "entry")
                {
                    int blade = (4 * 256 + 79) * 3;
                    if (scratch[blade] < 193 || scratch[blade + 1] < 185)
                        throw new InvalidOperationException("The early caption cut a dark notch into the yellow blade.");
                }
                Console.WriteLine($"MOVIE {name} {kind} {(cue.Bottom ? "bottom" : "top")} frame {frame}: "
                    + $"{changed} changed, {256 * 192 - changed} unchanged; maximum solid replaced run {largestUniformRun}.");
                SavePair<Rgb24>(report, $"film-{cue.From:0000}-{kind}", source, scratch, 256, 192);
            }
        }
        byte[] off = frames[(false, 1100)];
        byte[] traceTyping = frames[(false, traceTypeFrame)];
        art.MovieFrame(film, traceTypeFrame, film.Frames, false, traceTyping, scratch, frames);
        // This stroke belonged to the transient English word before it had
        // finished typing. It is beyond the translated string's final glyph.
        int originalP = (15 * 256 + 112) * 3;
        if (traceTyping[originalP] - scratch[originalP] < 12)
            throw new InvalidOperationException("A Trace typing stroke survived past the Chinese caption.");
        SavePair<Rgb24>(report, "film-trace-type-chase", traceTyping, scratch, 256, 192);
        Console.WriteLine($"MOVIE 特雷斯 type-chase top frame {traceTypeFrame}: transient English stroke removed.");
        if (art.MovieFrame(film, 1100, film.Frames, false, off, scratch, frames) != off || art.MatchFilm(top, top) != null)
            throw new InvalidOperationException("Unexpected edit outside matched frames and hashes.");

        Paths.SetPath(Ver.AMHE0, assets);
        Paths.MphKey = Ver.AMHE0;
        var textures = new[]
        {
            ("logo_screen/MAYA/nst_Model.bin", 0, (0, 0, 256, 41)),
            ("logo_screen/MAYA/nst_Model.bin", 1, (0, 0, 128, 38)),
            ("logo_screen/MAYA/whitelogo_Model.bin", 0, (0, 21, 256, 61)),
            ("main menu/esrb_Model.bin", 0, (0, 0, 16, 64)),
            ("tostart/logofinal_Model.bin", 3, (0, 0, 256, 24)),
            ("main menu/toplogo_Model.bin", 0, (0, 0, 256, 24)),
        };
        for (int i = 0; i < textures.Length; i++)
        {
            var (path, id, protectedArea) = textures[i];
            var model = Read.ReadModelFile(path.Replace('/', '\\'), path, animationPath: null);
            var meta = model.Recolors[0].Textures[id];
            byte[] source = model.GetPixels(id, id, recolorId: 0).SelectMany(c => new[] { c.Red, c.Green, c.Blue, c.Alpha }).ToArray();
            byte[] modified = (byte[])source.Clone();
            if (!art.ApplyTexture(path, id, id, meta.Width, meta.Height, modified)
                || art.ApplyTexture(path, id, id, meta.Width, meta.Height, modified))
                throw new InvalidOperationException("Texture source fingerprint or match failed: " + path);
            var (x, y, w, h) = protectedArea;
            for (int yy = y; yy < y + h; yy++)
            {
                int start = 4 * (yy * meta.Width + x);
                if (!source.AsSpan(start, 4 * w).SequenceEqual(modified.AsSpan(start, 4 * w)))
                    throw new InvalidOperationException($"Protected texture artwork changed: {path}#{id}, row {yy}.");
            }
            int changed = ChangedPixels(source, modified, 4);
            SavePair<Rgba32>(report, $"texture-{i}", source, modified, meta.Width, meta.Height);
            if (changed < 80 || changed > (i >= 4 ? 13500 : 5000))
                throw new InvalidOperationException($"Texture edit is not localized: {path}, {changed} pixels.");
            Console.WriteLine($"TEXTURE {path}#{id}: {changed} changed, {meta.Width * meta.Height - changed} unchanged; protected art identical.");
        }
        DesktopLocale.Init("en");
        if (LocalizedArtwork.Current != null) throw new InvalidOperationException("English should use the original artwork.");
        Console.WriteLine("PASS: 25 real movie samples, six real texture pairs, artwork and English preserved.");
        return 0;
    }

    private static void SavePair<T>(string? root, string name, byte[] before, byte[] after, int width, int height)
        where T : unmanaged, IPixel<T>
    {
        if (root == null) return;
        using var a = Image.LoadPixelData<T>(before, width, height);
        using var b = Image.LoadPixelData<T>(after, width, height);
        a.SaveAsPng(Path.Combine(root, name + "-before.png"));
        b.SaveAsPng(Path.Combine(root, name + "-after.png"));
    }

    private static int ChangedPixels(byte[] a, byte[] b, int channels)
    {
        int n = 0;
        for (int p = 0; p < a.Length; p += channels)
            if (!a.AsSpan(p, channels).SequenceEqual(b.AsSpan(p, channels))) n++;
        return n;
    }

    private static int LargestUniformReplacementRun(byte[] a, byte[] b, int stride, int channels)
    {
        int best = 0;
        for (int p = 0, run = 0; p < a.Length; p += channels)
        {
            if (a.AsSpan(p, channels).SequenceEqual(b.AsSpan(p, channels))) run = 0;
            else if (p % (stride * channels) == 0 || !b.AsSpan(p, channels).SequenceEqual(b.AsSpan(p - channels, channels))) run = 1;
            else best = Math.Max(best, ++run);
        }
        return best;
    }
}
