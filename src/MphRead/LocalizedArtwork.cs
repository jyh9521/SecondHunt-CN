using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace MphRead.Text;

// Authoring resources contain translation, placement and opacity, never source
// RGB. Restoration uses the user's fingerprinted texture or decoded frames.
public sealed class LocalizedArtwork
{
    public sealed record Lettering(int X, int Y, int Width, int Height, int Em, string Text, string Ink,
        string Style = "flat", bool Left = false, float Stretch = 1, int Tracking = 0,
        int Slant = 0, int Reveal = 0, int Delay = 0);
    public sealed record Ring(float Cx, float Cy, float Rx, float Ry);
    public sealed record TextureAlias(string Model, int Id, int Palette);
    public sealed record Texture(string Model, int Id, int Palette, int Width, int Height, string Sha256,
        string Mask, string Fill, Lettering[] Lettering, int SampleX = 0, int SampleY = 0, Ring? Ring = null,
        TextureAlias[]? Aliases = null);
    public sealed record TypeSpan(int X, int Y, int Width, int Height, int From, int Through);
    public sealed record LineGuide(int X, int Right, int Y, int Row, float Slope, int Hold, int Until);
    public sealed record Cue(bool Bottom, int From, int Until, int Reference, string Mask, Lettering[] Lettering,
        int FadeIn = 1, int FadeOut = 1, bool Scanline = false, string? FlashMask = null, int FlashLength = 0,
        int LateReference = -1, int UpperValueX = 256, int LowerValueX = 256, int SplitY = 11,
        TypeSpan[]? SourceTyping = null, LineGuide? Guide = null);
    public sealed record Film(string Key, string TopSha256, string BottomSha256, int Frames, Cue[] Cues);
    private sealed record Pack(int Version, Texture[] Textures, Film[] Movies);
    private sealed record Stencil(int X, int Y, int Width, int Height, byte[] Data)
    {
        public byte[] Expand(int width, int height)
        {
            if (Width < 1 || Height < 1 || X < 0 || Y < 0 || X > width - Width
                || Y > height - Height || Data.Length != Width * Height || !Data.Any(a => a > 0))
                throw new InvalidDataException("Invalid artwork stencil.");
            var result = new byte[width * height];
            for (int y = 0; y < Height; y++)
                Data.AsSpan(y * Width, Width).CopyTo(result.AsSpan((Y + y) * width + X, Width));
            return result;
        }
    }

    private const int FilmWidth = 256, FilmHeight = 192;
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static LocalizedArtwork? Current { get; set; }
    private readonly Dictionary<(string Model, int Id, int Palette), TexturePlan> _textures = new();
    private readonly Dictionary<string, Film> _movies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Cue, CuePlan> _cues = new();

    private sealed record TexturePlan(Texture Layout, byte[] Mask, ArtworkLettering[] Runs);
    private sealed record CuePlan(Cutout Steady, Cutout? Flash, ArtworkLettering[] Runs);
    private sealed record Cutout(byte[] Mask, RestorePoint[] Points);
    private sealed record RestorePoint(int Pixel, int[] Donors, float[] Weights, bool Late);

    public static LocalizedArtwork Load(Stream layout, Stream stencils, ReadOnlySpan<byte> glyphs)
    {
        var pack = JsonSerializer.Deserialize<Pack>(layout, _json);
        var masks = JsonSerializer.Deserialize<Dictionary<string, Stencil>>(stencils, _json);
        if (pack?.Version != 2 || pack.Textures == null || pack.Movies == null || masks == null)
            throw new InvalidDataException("Invalid localization artwork catalogue.");
        var atlas = new ArtworkLettering.Atlas(glyphs);
        var art = new LocalizedArtwork();
        ArtworkLettering[] Runs(Lettering[] lines, int w, int h) =>
            lines.Select(line => new ArtworkLettering(line, w, h, atlas)).ToArray();
        byte[] Mask(string id, int w, int h) => masks.TryGetValue(id, out Stencil? mask)
            ? mask.Expand(w, h) : throw new InvalidDataException($"Unknown artwork stencil: {id}");

        foreach (Texture tex in pack.Textures)
        {
            if (!HashString(tex.Sha256) || tex.Width is < 1 or > 512 || tex.Height is < 1 or > 512
                || tex.Fill is not ("alpha" or "sample" or "ring")
                || tex.SampleX < 0 || tex.SampleX >= tex.Width || tex.SampleY < 0 || tex.SampleY >= tex.Height
                || tex.Fill == "ring" && (tex.Ring == null || tex.Ring.Rx <= 0 || tex.Ring.Ry <= 0))
                throw new InvalidDataException("Invalid localized texture layout.");
            byte[] mask = Mask(tex.Mask, tex.Width, tex.Height);
            if (tex.Fill == "sample" && mask[tex.SampleY * tex.Width + tex.SampleX] != 0)
                throw new InvalidDataException("An artwork color sample intersects its own stencil.");
            var plan = new TexturePlan(tex, mask, Runs(tex.Lettering, tex.Width, tex.Height));
            art._textures.Add((Key(tex.Model), tex.Id, tex.Palette), plan);
            if (tex.Aliases != null)
                foreach (TextureAlias alias in tex.Aliases)
                    art._textures.Add((Key(alias.Model), alias.Id, alias.Palette), plan);
        }
        foreach (Film film in pack.Movies)
        {
            if (film.Frames <= 0 || !HashString(film.TopSha256) || !HashString(film.BottomSha256))
                throw new InvalidDataException("Invalid localized movie fingerprint.");
            foreach (Cue cue in film.Cues)
            {
                if (cue.From < 1 || cue.From >= cue.Until || cue.Until > film.Frames || cue.Reference < 0
                    || cue.Reference >= cue.From || cue.LateReference >= cue.Until || cue.LateReference < -1
                    || cue.FadeIn < 1 || cue.FadeOut < 1 || cue.FlashLength < 0
                    || (cue.FlashMask != null && cue.FlashLength == 0))
                    throw new InvalidDataException("Invalid localized movie frame range.");
                if (cue.SourceTyping != null)
                    foreach (TypeSpan t in cue.SourceTyping)
                        if (t.Width < 1 || t.Height < 1 || t.X < 0 || t.X > FilmWidth - t.Width || t.Y < 0
                            || t.Y > FilmHeight - t.Height || t.From < cue.From || t.Through <= t.From || t.Through >= cue.Until)
                            throw new InvalidDataException("Invalid source lettering timing.");
                if (cue.Guide is { } g && (g.X < 0 || g.Right <= g.X || g.Right > FilmWidth
                    || g.Y <= 0 || g.Row < g.Y || g.Row >= FilmHeight || Math.Abs(g.Slope) > 4
                    || g.Hold < cue.From || g.Until <= g.Hold || g.Until > cue.Until))
                    throw new InvalidDataException("Invalid line restoration guide.");
                Cutout Build(string id) => Prepare(Mask(id, FilmWidth, FilmHeight), cue);
                art._cues.Add(cue, new(Build(cue.Mask), cue.FlashMask is { } f ? Build(f) : null,
                    Runs(cue.Lettering, FilmWidth, FilmHeight)));
            }
            art._movies.Add(film.Key, film);
        }
        return art;
    }

    private static bool HashString(string s) => s.Length == 64 && s.All(Uri.IsHexDigit);
    private static string Key(string path) => path.Split('|')[0].Replace('\\', '/').ToLowerInvariant();
    private static bool SameHash(byte[] hash, string expected) =>
        Convert.ToHexString(hash).Equals(expected, StringComparison.OrdinalIgnoreCase);
    private static bool HasHash(string path, string expected)
    {
        using FileStream file = File.OpenRead(path);
        return SameHash(SHA256.HashData(file), expected);
    }

    public bool ApplyTexture(string model, int id, int palette, int width, int height, byte[] rgba)
    {
        if (!_textures.TryGetValue((Key(model), id, palette), out TexturePlan? tex)
            || width != tex.Layout.Width || height != tex.Layout.Height || rgba.Length != width * height * 4
            || !SameHash(SHA256.HashData(rgba), tex.Layout.Sha256)) return false;
        byte[] source = (byte[])rgba.Clone();
        Span<byte> fill = stackalloc byte[4];
        for (int p = 0; p < tex.Mask.Length; p++)
        {
            int strength = tex.Mask[p];
            if (strength == 0) continue;
            int d = 4 * p;
            switch (tex.Layout.Fill)
            {
                case "alpha": fill.Clear(); break;
                case "sample":
                    source.AsSpan(4 * (tex.Layout.SampleY * width + tex.Layout.SampleX), 4).CopyTo(fill);
                    break;
                case "ring":
                    RepairRing(tex.Layout.Ring!, source, tex.Mask, width, height, p, fill);
                    break;
            }
            for (int i = 0; i < 4; i++) rgba[d + i] = Mix(source[d + i], fill[i], strength);
        }
        foreach (ArtworkLettering run in tex.Runs) run.Draw(rgba, 4, 1, 0, int.MaxValue);
        return true;
    }

    // At an occluded point of the title medallion, borrow the same radial
    // surface from its intact arcs. The unoccluded rim is never repainted.
    private static void RepairRing(Ring ring, byte[] source, byte[] mask, int width, int height,
        int pixel, Span<byte> color)
    {
        color.Clear();
        float u = (pixel % width - ring.Cx) / ring.Rx, v = (pixel / width - ring.Cy) / ring.Ry;
        float radius = MathF.Sqrt(u * u + v * v);
        if (radius > 1.07f) return;
        float angle = MathF.Atan2(v, u);
        (int Index, int Steps) Arc(int direction)
        {
            for (int k = 1; k <= 100; k++)
            {
                float a = angle + direction * k * 0.028f;
                int x = (int)MathF.Round(ring.Cx + radius * ring.Rx * MathF.Cos(a));
                int y = (int)MathF.Round(ring.Cy + radius * ring.Ry * MathF.Sin(a));
                if (x < 0 || x >= width || y < 0 || y >= height) continue;
                int p = y * width + x;
                if (mask[p] == 0 && (radius > .95f || source[p * 4 + 3] > 0))
                    return (p * 4, k);
            }
            return (-1, 0);
        }
        var l = Arc(-1);
        var r = Arc(1);
        if (l.Index < 0 && r.Index < 0)
        {
            // The centre was fully covered by English. Reuse the closest
            // intact interior sample, not an opaque fill rectangle.
            for (int k = 1; k < 20; k++)
            {
                float d = radius + k * .04f;
                if (d >= .85f) break;
                int x = (int)MathF.Round(ring.Cx + d * ring.Rx * MathF.Cos(angle));
                int y = (int)MathF.Round(ring.Cy + d * ring.Ry * MathF.Sin(angle));
                int p = y * width + x;
                if (mask[p] != 0 || source[p * 4 + 3] == 0) continue;
                source.AsSpan(p * 4, 4).CopyTo(color);
                return;
            }
            return;
        }
        if (l.Index < 0 || r.Index < 0)
        {
            source.AsSpan(l.Index < 0 ? r.Index : l.Index, 4).CopyTo(color);
            return;
        }
        int blend = l.Steps * 255 / (l.Steps + r.Steps);
        for (int i = 0; i < 4; i++) color[i] = Mix(source[l.Index + i], source[r.Index + i], blend);
    }

    public Film? MatchFilm(string topPath, string? bottomPath)
    {
        string key = Path.GetFileNameWithoutExtension(topPath).Split('_')[0];
        if (!_movies.TryGetValue(key, out Film? film) || bottomPath == null) return null;
        return HasHash(topPath, film.TopSha256) && HasHash(bottomPath, film.BottomSha256) ? film : null;
    }

    private static Cutout Prepare(byte[] mask, Cue cue)
    {
        var result = new List<RestorePoint>();
        for (int p = 0; p < mask.Length; p++)
        {
            if (mask[p] == 0) continue;
            int x = p % FilmWidth, y = p / FilmWidth;
            var donors = new List<(int Pixel, float Weight)>();
            void Add(int dx, int dy, float wx, float wy)
            {
                int xx = x + dx, yy = y + dy;
                if (xx < 0 || xx >= FilmWidth || yy < 0 || yy >= FilmHeight) return;
                int q = yy * FilmWidth + xx;
                if (mask[q] != 0) return;
                float weight = 1 + dx * dx / (wx * wx) + dy * dy / (wy * wy);
                donors.Add((q * 3, 1 / (weight * weight)));
            }
            if (cue.Scanline)
            {
                // Fit the moving scan line from intact pixels on the *same*
                // source row. Copying an earlier whole rectangle would erase it.
                for (int dx = -74; dx <= 74; dx++) Add(dx, 0, 18, 1);
                if (donors.Count < 9)
                    for (int dx = -255; dx <= 255; dx++)
                        if (Math.Abs(dx) > 74) Add(dx, 0, 18, 1);
            }
            if (!cue.Scanline || donors.Count < 5)
            {
                for (int dy = -5; dy <= 5; dy++)
                    for (int dx = -14; dx <= 14; dx++) Add(dx, dy, 6, 2.7f);
            }
            if (donors.Count < 5)
                for (int dy = -30; dy <= 30; dy++)
                    if (Math.Abs(dy) > 5)
                        for (int dx = -13; dx <= 13; dx++) Add(dx, dy, 8, 4);
            if (donors.Count == 0)
                throw new InvalidDataException($"Artwork stencil {cue.Mask} has no donor near {x},{y}.");
            var near = donors.OrderByDescending(d => d.Weight).Take(cue.Scanline ? 48 : 36).ToArray();
            float sum = near.Sum(d => d.Weight);
            result.Add(new(p, near.Select(d => d.Pixel).ToArray(), near.Select(d => d.Weight / sum).ToArray(),
                cue.LateReference >= 0 && x >= (y < cue.SplitY ? cue.UpperValueX : cue.LowerValueX)));
        }
        return new(mask, result.ToArray());
    }

    // The VX buffers remain untouched. Only stencil and Chinese glyph pixels
    // in a presenter-owned copy can differ from the current decoded frame.
    public byte[] MovieFrame(Film film, int frame, int count, bool bottom, byte[] source, byte[] scratch,
        IReadOnlyDictionary<(bool Bottom, int Frame), byte[]>? referenceFrames = null)
    {
        if (film.Frames != count || source.Length != FilmWidth * FilmHeight * 3 || source.Length != scratch.Length
            || ReferenceEquals(source, scratch) || referenceFrames == null) return source;
        bool copied = false;
        foreach (Cue cue in film.Cues)
        {
            if (cue.Bottom != bottom || frame < cue.From || frame >= cue.Until
                || !referenceFrames.TryGetValue((bottom, cue.Reference), out byte[]? reference)
                || reference.Length != source.Length) continue;
            if (!copied) { source.CopyTo(scratch, 0); copied = true; }
            byte[]? late = null;
            if (cue.LateReference >= 0)
                referenceFrames.TryGetValue((bottom, cue.LateReference), out late);
            if (late?.Length != source.Length) late = null;
            CuePlan plan = _cues[cue];
            int elapsed = frame - cue.From, remaining = cue.Until - frame;
            float flash = cue.FlashLength == 0 ? 0 : Math.Clamp((cue.FlashLength - elapsed) / (float)cue.FlashLength, 0, 1);
            Cutout cut = flash > 0 && plan.Flash != null ? plan.Flash : plan.Steady;
            foreach (RestorePoint point in cut.Points)
            {
                int p = point.Pixel, d = p * 3;
                int weight = Math.Max(plan.Steady.Mask[p], (int)(cut.Mask[p] * Math.Clamp(flash * 1.8f, 0, 1)));
                int px = p % FilmWidth, py = p / FilmWidth;
                if (cue.SourceTyping != null)
                    foreach (TypeSpan t in cue.SourceTyping)
                    {
                        if (px < t.X || px >= t.X + t.Width || py < t.Y || py >= t.Y + t.Height) continue;
                        // Don't remove later letters before the baked-in typing reaches them.
                        float visible = frame + 1 < t.From ? 0
                            : Math.Clamp((frame + 2f - t.From) / (t.Through - t.From), 0, 1);
                        // The last visible letter may be wide and the codec's
                        // halo extends past its pen advance. Include one glyph
                        // of lead once the native line has actually started.
                        if (visible == 0 || px >= t.X + t.Width * visible + 8) weight = 0;
                    }
                if (weight == 0) continue;
                byte[] plate = point.Late && late != null ? late : reference;
                int guideSample = 0, guideAlpha = 0;
                if (cue.Guide is { } g && py < g.Y && px >= g.X && px < g.Right && frame < g.Until)
                {
                    // Continue an intact diagonal through tiny glyphs rather than
                    // smearing the edge from an early, low-exposure plate.
                    int xx = Math.Clamp(px + (int)MathF.Round((g.Row - py) * g.Slope), 0, FilmWidth - 1);
                    guideSample = (g.Row * FilmWidth + xx) * 3;
                    float ramp = Math.Min(1, Math.Min(px - g.X + 1, g.Right - px) / 5f);
                    float time = Math.Min(1, (g.Until - frame) / (float)(g.Until - g.Hold));
                    guideAlpha = (int)(242 * ramp * time);
                }
                for (int c = 0; c < 3; c++)
                {
                    float mx = 0, my = 0, xx = 0, xy = 0;
                    for (int i = 0; i < point.Donors.Length; i++)
                    {
                        int q = point.Donors[i] + c;
                        float x = plate[q], y = source[q], w = point.Weights[i];
                        mx += x * w;
                        my += y * w;
                        xx += x * x * w;
                        xy += x * y * w;
                    }
                    // The old plate supplies contours, not its exposure or
                    // color. A regularized local fit transfers today's light.
                    float ridge = cue.Scanline ? 42 : 12;
                    float gain = Math.Clamp((xy - mx * my + ridge * .7f)
                        / (Math.Max(0, xx - mx * mx) + ridge), 0, cue.Scanline ? 2.4f : 5f);
                    int recovered = (int)Math.Clamp(my + gain * (plate[d + c] - mx) + .5f, 0, 255);
                    if (guideAlpha > 0) recovered = Mix(recovered, source[guideSample + c], guideAlpha);
                    scratch[d + c] = Mix(source[d + c], recovered, weight);
                }
            }
            float opacity = Math.Min(1, Math.Min((elapsed + 1f) / cue.FadeIn, remaining / (float)cue.FadeOut));
            foreach (ArtworkLettering run in plan.Runs) run.Draw(scratch, 3, opacity, flash, elapsed);
        }
        return copied ? scratch : source;
    }

    internal static byte Mix(int behind, int front, int alpha) =>
        (byte)((behind * (255 - alpha) + front * alpha + 127) / 255);
}
