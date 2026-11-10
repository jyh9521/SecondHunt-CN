using System;
using System.Collections.Generic;
using MphRead.Formats;
using MphRead.Text;

namespace MphRead.Hud
{
    // Cache immutable sprites per player. Glyph antialiasing stays in the atlas;
    // only the small outline and the caller's HUD tint are composed at runtime.
    internal sealed class HudGlyphRaster(ExtraGlyphs glyphs)
    {
        public ExtraGlyphs Glyphs { get; } = glyphs;
        private readonly Dictionary<(int Glyph, uint Ink), Scene.HostHudSprite> _sprites = new();

        public Scene.HostHudSprite Get(int glyph, ColorRgba ink)
        {
            var key = (glyph, ink.ToUint());
            if (_sprites.TryGetValue(key, out Scene.HostHudSprite? sprite)) return sprite;

            int size = Glyphs.Size, count = size * size;
            ReadOnlySpan<byte> coverage = Glyphs.Alpha.AsSpan(glyph * count, count);
            var pixels = new ColorRgba[count];
            int radius = Math.Max(1, size / 16);
            const int outlineOpacity = 192, maxCoverageSquared = 255 * 255;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int at = y * size + x;
                    int fill = coverage[at];
                    if (fill == 255)
                    {
                        pixels[at] = ink;
                        continue;
                    }
                    int near = fill;
                    for (int sy = Math.Max(0, y - radius); sy <= Math.Min(size - 1, y + radius); sy++)
                    {
                        for (int sx = Math.Max(0, x - radius); sx <= Math.Min(size - 1, x + radius); sx++)
                        {
                            int dx = sx - x, dy = sy - y;
                            if (dx * dx + dy * dy <= radius * radius)
                                near = Math.Max(near, coverage[sy * size + sx]);
                        }
                    }
                    // Put a thin, partly transparent outline behind the fill. Store straight RGBA
                    // for the HUD's source-alpha blend; leave pixels outside the contour transparent.
                    int edge = (near * (255 - fill) * outlineOpacity + maxCoverageSquared / 2) / maxCoverageSquared;
                    int alpha = fill + edge;
                    if (alpha != 0)
                    {
                        pixels[at] = new ColorRgba(
                            Blend(ink.Red, 10, fill, edge, alpha),
                            Blend(ink.Green, 15, fill, edge, alpha),
                            Blend(ink.Blue, 20, fill, edge, alpha), (byte)alpha);
                    }
                }
            }
            sprite = new Scene.HostHudSprite { Width = size, Height = size, Pixels = pixels, Smooth = true };
            _sprites.Add(key, sprite);
            return sprite;
        }

        private static byte Blend(int ink, int outline, int fill, int edge, int alpha)
            => (byte)((ink * fill + outline * edge + alpha / 2) / alpha);
    }
}
