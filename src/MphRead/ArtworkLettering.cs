using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MphRead.Text;

// A layout rectangle defines alignment only. Every render path below is an
// alpha blend of glyphs, their bevel/outline and their short-range glow.
internal sealed class ArtworkLettering
{
    internal sealed class Atlas
    {
        internal const int Side = 48;
        private readonly Dictionary<char, byte[]> _glyphs = new();
        public Atlas(ReadOnlySpan<byte> data)
        {
            const int stride = 2 + Side * Side;
            if (data.Length < 4 + stride || !data[..4].SequenceEqual("SHA1"u8)
                || (data.Length - 4) % stride != 0)
                throw new InvalidDataException("Invalid artwork glyph atlas.");
            for (int p = 4; p < data.Length; p += stride)
                _glyphs.Add((char)BinaryPrimitives.ReadUInt16LittleEndian(data[p..]),
                    data.Slice(p + 2, Side * Side).ToArray());
        }

        public byte[] Get(char c) => _glyphs.TryGetValue(c, out byte[]? a)
            ? a : throw new InvalidDataException($"The artwork atlas lacks U+{(int)c:X4}.");
    }

    private sealed record Stamp(int X, int Y, int Width, int Height, byte[] Alpha, byte[] Edge,
        byte[] Glow, int Index, int Count)
    {
        public int At(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height ? Alpha[y * Width + x] : 0;
    }

    private enum Style { Flat, Hud, Bloom, Metal, Outline }
    private readonly Style _style;
    private readonly int _width, _height, _delay, _reveal;
    private readonly uint _ink;
    private readonly List<Stamp> _stamps = new();

    public ArtworkLettering(LocalizedArtwork.Lettering line, int width, int height, Atlas atlas)
    {
        if (!Enum.TryParse(line.Style, true, out _style) || !Enum.IsDefined(_style)
            || line.Ink.Length != 6 || !uint.TryParse(line.Ink, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _ink)
            || line.X < 0 || line.Y < 0 || line.Width < 1 || line.Height < 1 || line.Em is < 1 or > 96
            || line.X > width - line.Width || line.Y > height - line.Height || line.Stretch is < .5f or > 2f
            || line.Tracking is < -3 or > 20 || line.Slant is < -9 or > 9 || line.Reveal < 0 || line.Delay < 0
            || String.IsNullOrWhiteSpace(line.Text))
            throw new InvalidDataException("Invalid translated artwork lettering.");
        _width = width;
        _height = height;
        _delay = line.Delay;
        _reveal = line.Reveal;
        string[] lines = line.Text.Split('\n');
        int em = Math.Min(line.Em, line.Height / lines.Length);
        int Advance(char c, int e) => Math.Max(2, (int)MathF.Round(e * line.Stretch
            * (c == ' ' ? .34f : c < 127 ? .64f : 1)) + line.Tracking);
        int Measure(string s, int e) => s.Sum(c => Advance(c, e)) - line.Tracking;
        while (em > 1 && lines.Any(s => Measure(s, em) > line.Width - Math.Abs(line.Slant)))
            em--;
        int y = line.Y + (line.Height - em * lines.Length) / 2;
        foreach (string s in lines)
        {
            int x = line.X + (line.Left ? 0 : (line.Width - Measure(s, em)) / 2);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                int advance = Advance(c, em);
                if (c != ' ')
                {
                    int wide = Math.Max(2, advance - line.Tracking);
                    _stamps.Add(Prepare(atlas.Get(c), x, y, wide, em, line.Slant, c < 127, i, s.Length));
                }
                x += advance;
            }
            y += em;
        }
    }

    private Stamp Prepare(byte[] glyph, int x, int y, int width, int em, int slant, bool ascii, int index, int count)
    {
        const int border = 5;
        int w = width + 2 * border + Math.Abs(slant), h = em + 2 * border;
        byte[] alpha = new byte[w * h], edge = new byte[w * h], glow = new byte[w * h];
        int Pixel(float sx, float sy)
        {
            int col = (int)MathF.Floor(sx), row = (int)MathF.Floor(sy);
            int A(int a, int b) => a >= 0 && a < Atlas.Side && b >= 0 && b < Atlas.Side ? glyph[b * Atlas.Side + a] : 0;
            float u = sx - col, v = sy - row;
            return (int)(A(col, row) * (1 - u) * (1 - v) + A(col + 1, row) * u * (1 - v)
                + A(col, row + 1) * (1 - u) * v + A(col + 1, row + 1) * u * v + .5f);
        }
        for (int yy = 0; yy < em; yy++)
        {
            float sxShift = slant * (1 - (yy + .5f) / em) - Math.Min(0, slant);
            for (int xx = 0; xx < w - 2 * border; xx++)
            {
                float tx = (xx + .5f - sxShift) / width;
                // The ASCII glyph is centred in the atlas's em square.
                if (ascii) tx = .5f + (tx - .5f) * .64f;
                float sx = tx * Atlas.Side - .5f, sy = (yy + .5f) * Atlas.Side / em - .5f;
                int a = tx is >= 0 and <= 1 ? Pixel(sx, sy) : 0;
                if (_style == Style.Metal && em >= 22 && tx is >= 0 and <= 1)
                    a = Math.Max(a, (int)(Pixel(sx - 1.2f, sy) * .91f));
                alpha[(yy + border) * w + xx + border] = (byte)a;
            }
        }
        for (int yy = 1; yy < h - 1; yy++)
            for (int xx = 1; xx < w - 1; xx++)
            {
                int p = yy * w + xx;
                int max = alpha[p], sum = 0, n = 0;
                for (int dy = -3; dy <= 3; dy++)
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        if (xx + dx < 0 || xx + dx >= w || yy + dy < 0 || yy + dy >= h) continue;
                        int a = alpha[p + dy * w + dx];
                        if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1) max = Math.Max(max, a);
                        int wt = Math.Max(0, 10 - dx * dx - dy * dy);
                        sum += a * wt;
                        n += wt;
                    }
                edge[p] = (byte)max;
                glow[p] = (byte)(sum / n);
            }
        return new(x - border, y - border, w, h, alpha, edge, glow, index, count);
    }

    public void Draw(byte[] pixels, int channels, float opacity, float flash, int elapsed)
    {
        if (elapsed < _delay) return;
        foreach (Stamp stamp in _stamps)
        {
            float alpha = opacity;
            if (_reveal != 0)
                alpha *= Math.Clamp((elapsed - _delay + 1f) * stamp.Count / _reveal - stamp.Index, 0, 1);
            if (alpha <= 0) continue;
            for (int sy = 0; sy < stamp.Height; sy++)
            {
                int y = stamp.Y + sy;
                if (y < 0 || y >= _height) continue;
                for (int sx = 0; sx < stamp.Width; sx++)
                {
                    int x = stamp.X + sx;
                    if (x < 0 || x >= _width) continue;
                    int i = sy * stamp.Width + sx, p = (y * _width + x) * channels;
                    int a = stamp.Alpha[i], edge = stamp.Edge[i], halo = stamp.Glow[i];
                    switch (_style)
                    {
                        case Style.Hud:
                            Blend(p, 0x102327, (int)(edge * .62f * alpha));
                            break;
                        case Style.Bloom:
                            Blend(p, 0xFF5722, (int)(halo * .92f * alpha));
                            break;
                        case Style.Metal:
                            Blend(p, 0x0B1019, (int)(stamp.At(sx - 1, sy - 2) * .82f * alpha));
                            Blend(p, 0xD5E4FF, (int)(halo * (.15f + .85f * flash) * alpha));
                            Blend(p, 0x697F96, (int)((edge - a) * .44f * alpha));
                            break;
                        case Style.Outline:
                            Blend(p, 0xD34A00, (int)(halo * .88f * alpha));
                            Blend(p, _ink, (int)(edge * .96f * alpha));
                            break;
                    }
                    if (a == 0) continue;
                    uint color = _style switch
                    {
                        Style.Metal => Metal(sy, stamp, sx, a, flash),
                        Style.Outline => sy < stamp.Height / 2 ? 0x47200Cu : 0x28130Bu,
                        Style.Bloom => 0xFFA841u,
                        _ => _ink
                    };
                    Blend(p, color, (int)(a * alpha));
                }
            }
        }
        void Blend(int p, uint color, int a)
        {
            if (a == 0) return;
            int behind = channels == 4 ? pixels[p + 3] * (255 - a) / 255 : 255 - a;
            int coverage = a + behind;
            pixels[p] = (byte)(((color >> 16) * a + pixels[p] * behind) / coverage);
            pixels[p + 1] = (byte)((((color >> 8) & 255) * a + pixels[p + 1] * behind) / coverage);
            pixels[p + 2] = (byte)(((color & 255) * a + pixels[p + 2] * behind) / coverage);
            if (channels == 4) pixels[p + 3] = (byte)coverage;
        }
    }

    private uint Metal(int y, Stamp stamp, int x, int a, float flash)
    {
        float t = Math.Clamp((y - 5f) / Math.Max(1, stamp.Height - 10), 0, 1);
        int shade = t switch
        {
            < .44f => (int)(236 - t * 172),
            < .53f => (int)(164 + (t - .44f) * 690),
            < .71f => 238,
            _ => (int)(233 - (t - .71f) * 190)
        };
        int bevel = (a - stamp.At(x - 1, y - 1)) / 8 - (a - stamp.At(x + 1, y + 1)) / 10;
        uint Channel(int c) => (uint)Math.Clamp((int)(c * (shade + bevel) / 235f * (1 - flash * .56f)
            + 255 * flash * .56f), 0, 255);
        return Channel((int)(_ink >> 16)) << 16 | Channel((int)(_ink >> 8 & 255)) << 8 | Channel((int)(_ink & 255));
    }
}
