using System;
using System.Collections.Generic;
using System.Linq;
using MphRead.Formats;
using MphRead.Hud;
using MphRead.Text;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        private HudObjectInstance? _extraText;
        private HudGlyphRaster? _extraRaster;

        private static int LocaleGlyph(ReadOnlySpan<char> line, ref int i, Font font)
        {
            int ch = line[i];
            if ((ch & 0x80) != 0 && i + 1 < line.Length)
                ch = line[++i] & 0x3F | ((ch & 0x1F) << 6);
            int glyph = ch - font.MinCharacter;
            return glyph >= 0 && glyph < font.Widths.Count ? glyph : '?' - font.MinCharacter;
        }

        private static float LocaleWidth(ReadOnlySpan<char> line, Font font)
        {
            float width = 0;
            for (int i = 0; i < line.Length; i++)
                width += line[i] > 255 ? ExtraGlyphs.Width(line[i]) : font.Widths[LocaleGlyph(line, ref i, font)];
            return width;
        }

        private static ColorRgba ReadableHudInk(IReadOnlyList<ColorRgba> colors, int palette, ColorRgba? color)
        {
            // Index 1 of many DS palettes is a dark contour colour, not the text's ink.
            // Keep the accent of the active palette (or explicit tint) but lift its value
            // for readability on the visor, over the world and over solid dialog panels.
            ColorRgba tint = color ?? new ColorRgba(255, 255, 255, 255);
            if (color == null)
            {
                int start = palette * 16;
                int brightest = -1;
                for (int i = start + 1; i < Math.Min(start + 16, colors.Count); i++)
                {
                    ColorRgba candidate = colors[i];
                    int light = 21 * candidate.Red + 72 * candidate.Green + 7 * candidate.Blue;
                    if (candidate.Alpha != 0 && light > brightest)
                    {
                        brightest = light;
                        tint = candidate;
                    }
                }
            }
            static byte Lift(byte c) => (byte)(196 + 59 * c / 255);
            return new ColorRgba(Lift(tint.Red), Lift(tint.Green), Lift(tint.Blue), 255);
        }

        private Vector2 DrawLocalizedText(float x, float y, Align align, int palette, ReadOnlySpan<char> text,
            ColorRgba? color, float alpha, float spacingY, int visibleLength)
        {
            ExtraGlyphs glyphs = LocalText.Glyphs!;
            if (_extraRaster?.Glyphs != glyphs)
            {
                _extraRaster = new HudGlyphRaster(glyphs);
                _extraText = new HudObjectInstance(glyphs.Size, glyphs.Size)
                {
                    Enabled = true,
                    Scale = (float)ExtraGlyphs.Advance / glyphs.Size
                };
            }
            HudObjectInstance extra = _extraText!;
            // Restore the original 8-pixel font if we were drawing a Japanese ROM's kanji page.
            Font font = SetUpFont(' ', set: true);
            ColorRgba ink = ReadableHudInk(_textInst.PaletteData!, palette, color);
            _textInst.Alpha = extra.Alpha = alpha * ((color?.Alpha ?? 255) / 255f);
            float anchor = x;
            for (int start = 0; start < text.Length;)
            {
                int n = text[start..].IndexOf('\n');
                if (n < 0) n = text.Length - start;
                ReadOnlySpan<char> line = text.Slice(start, n);
                float width = LocaleWidth(line, font);
                x = align == Align.Left ? anchor : align == Align.Right ? anchor - width : anchor - width / 2;
                for (int i = 0; i < line.Length; i++)
                {
                    int characterOffset = start + i;
                    char ch = line[i];
                    bool wide = ch > 255;
                    int glyph = wide ? glyphs.Index(ch) : LocaleGlyph(line, ref i, font);
                    int advance = wide ? ExtraGlyphs.Width(ch) : font.Widths[glyph];
                    if (ch != ' ' && (align != Align.PadCenter || characterOffset < visibleLength))
                    {
                        HudObjectInstance instance = wide ? extra : _textInst;
                        instance.PositionX = (x + (wide ? (advance - ExtraGlyphs.Advance) / 2f : 0)) / 256f;
                        instance.PositionY = (y + (wide ? 0 : font.Offsets[glyph])) / 192f;
                        if (wide) instance.SetRaster(_extraRaster.Get(glyph, ink), _scene);
                        else instance.SetData(glyph, ink, _scene);
                        _scene.DrawHudObject(instance, mode: align == Align.Left ? 2 : 0);
                    }
                    x += advance;
                }
                start += n + 1;
                if (start < text.Length) y += spacingY;
            }
            return new Vector2(x, y);
        }

        private static int WrapLocalizedText(string text, int width, char[] dest)
        {
            string result = string.Join("\n", text.Split('\n').SelectMany(line =>
                LocalText.WrapWide(line, width, s => LocaleWidth(s, Font.Normal))));
            int count = Math.Min(result.Length, dest.Length - 1);
            result.AsSpan(0, count).CopyTo(dest);
            dest[count] = '\0';
            int lines = 1;
            for (int i = 0; i < count; i++) if (dest[i] == '\n') lines++;
            return lines;
        }
    }
}
