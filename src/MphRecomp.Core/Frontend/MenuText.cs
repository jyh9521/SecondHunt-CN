using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MphRead;
using MphRead.Formats;
using MphRead.Text;

// Menu strings and the menu font, both from the user's ROM.
//   strings: frontend/metroidhunters_text_<lang>.bin (null-terminated list of entry offsets; each entry = offset,
//            offset, length, length -- the pairs always match), indexed by the menu file's string ids
//   font:    the glyph atlas in models/hudfont_Model.bin (128x256, 16 columns of 8x8 cells, glyph = char - 32) plus
//            the per-glyph advance widths and vertical offsets from arm9.bin that MphRead's HUD text also uses
namespace MphRecomp.Frontend
{
    public sealed class MenuStrings
    {
        private readonly List<string> _strings;
        public IReadOnlyList<string> Strings => _strings;
        public int RomCount { get; }

        private readonly (string Source, string? Text)[]? _locale;

        private MenuStrings(List<string> strings, RomTextPack? pack)
        {
            _strings = strings;
            RomCount = strings.Count;
            if (pack != null)
            {
                _locale = new (string, string?)[RomCount];
                for (int i = 0; i < RomCount; i++) _locale[i] = (strings[i], pack.Menu(strings[i]));
            }
        }

        // Keep the raw indexer for game-code lookups (BEGIN GAME, placeholders, etc.).
        // Only drawing sees the overlay, and dynamically filled values take precedence.
        public string Display(int id)
        {
            string text = this[id];
            if (_locale != null && id >= 0 && id < _locale.Length
                && _locale[id] is var row && row.Source == text && row.Text != null)
                return row.Text;
            return text;
        }

        public string this[int id] => id >= 0 && id < _strings.Count ? _strings[id] : "";

        // the recomp's own text (labels, values that change): ids after the ROM's
        public int Add(string text)
        {
            _strings.Add(text);
            return _strings.Count - 1;
        }

        public void Set(int id, string text)
        {
            if (id >= RomCount && id < _strings.Count) _strings[id] = text;
        }

        // Fill one of the ROM's placeholder strings ("rumb", "priv", "setting a"...) the way the game's code does.
        public void Fill(int id, string text)
        {
            if (id >= 0 && id < RomCount) _strings[id] = text;
        }

        // lang: en, fr, de, it, es, jp
        public static MenuStrings Load(string fileSystemRoot, string lang = "en")
        {
            byte[] data = File.ReadAllBytes(Path.Combine(fileSystemRoot, "frontend", $"metroidhunters_text_{lang}.bin"));
            var list = new List<string>();
            for (int o = 0; ; o += 4)
            {
                uint entry = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(o));
                if (entry == 0) break;
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)entry));
                ushort length = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan((int)entry + 8));
                int end = Array.IndexOf(data, (byte)0, (int)offset, length);
                // Latin-1 keeps each byte as one char, so the game's two-byte (0x80) sequences survive for the font
                list.Add(Encoding.Latin1.GetString(data, (int)offset, (end == -1 ? length : end - (int)offset)));
            }
            return new MenuStrings(list, lang == "en" ? LocalText.ForRom(fileSystemRoot) : null);
        }
    }

    public sealed class MenuFont
    {
        public const int MinCharacter = 32;
        public const int Cell = 8;
        private const int Columns = 16;
        private const int ExtraColumns = 64;
        private readonly ExtraGlyphs? _extra = LocalText.Glyphs;
        private readonly UiTexture? _extraTexture;

        private readonly IReadOnlyList<int> _widths;
        private readonly IReadOnlyList<int> _offsets;
        public int TextureId { get; }
        public int AtlasWidth { get; }
        public int AtlasHeight { get; }
        // The UI inventory records text after localization and before glyph
        // emission. Leave unset outside the test harness.
        internal Action<string>? ObserveDrawing { get; set; }

        public MenuFont(UiTextureCache textures)
        {
            if ((object?)MphRead.Text.Font.Normal.Widths == null)
            {
                Extract.LoadRuntimeData(); // arm9.bin widths / offsets (MphRead's own loader)
            }
            _widths = MphRead.Text.Font.Normal.Widths;
            _offsets = MphRead.Text.Font.Normal.Offsets;
            Model model = Read.ReadModelFile("hudfont", "models/hudfont_Model.bin", null);
            Material material = model.Materials[0];
            Texture texture = model.Recolors[0].Textures[material.TextureId];
            AtlasWidth = texture.Width;
            AtlasHeight = texture.Height;
            TextureId = textures.GetOrAdd("hudfont", () =>
            {
                IReadOnlyList<ColorRgba> pixels = model.GetPixels(material.TextureId, material.PaletteId, recolorId: 0);
                byte[] rgba = new byte[texture.Width * texture.Height * 4];
                for (int i = 0; i < pixels.Count && i * 4 + 3 < rgba.Length; i++)
                {
                    rgba[i * 4] = pixels[i].Red;
                    rgba[i * 4 + 1] = pixels[i].Green;
                    rgba[i * 4 + 2] = pixels[i].Blue;
                    rgba[i * 4 + 3] = pixels[i].Alpha;
                }
                return (texture.Width, texture.Height, rgba);
            }).Id;
            if (_extra is ExtraGlyphs extra)
            {
                _extraTexture = textures.GetOrAdd("locale-font", () =>
                {
                    int size = extra.Size, width = ExtraColumns * size;
                    int height = ((extra.Count + ExtraColumns - 1) / ExtraColumns) * size;
                    byte[] rgba = new byte[width * height * 4];
                    for (int i = 0; i < extra.Count; i++)
                    {
                        int tx = (i % ExtraColumns) * size, ty = (i / ExtraColumns) * size;
                        for (int y = 0; y < size; y++)
                            for (int x = 0; x < size; x++)
                            {
                                int dest = ((ty + y) * width + tx + x) * 4;
                                rgba[dest] = rgba[dest + 1] = rgba[dest + 2] = 255;
                                rgba[dest + 3] = extra.Alpha[i * size * size + y * size + x];
                            }
                    }
                    return (width, height, rgba);
                });
            }
        }

        // the game's two-byte characters: a byte with the top bit set combines with the next one
        private static int Glyph(string text, ref int i)
        {
            int ch = text[i];
            if (ch <= 255 && (ch & 0x80) != 0 && i + 1 < text.Length)
            {
                ch = text[++i] & 0x3F | ((ch & 0x1F) << 6);
            }
            return ch - MinCharacter;
        }

        private int Advance(int glyph) => glyph >= 0 && glyph < _widths.Count ? _widths[glyph] : Cell;

        private (int Texture, float Offset, float U, float V, float U1, float V1) Sprite(int glyph, bool extra)
        {
            UiTexture? tex = extra ? _extraTexture : null;
            if (extra && tex == null || !extra && (glyph < 0 || glyph >= _widths.Count))
                return (-1, 0, 0, 0, 0, 0);
            int index = tex != null ? _extra!.Index((char)(glyph + MinCharacter)) : glyph;
            int cell = tex != null ? _extra!.Size : Cell;
            int columns = tex != null ? ExtraColumns : Columns;
            int width = tex?.Width ?? AtlasWidth, height = tex?.Height ?? AtlasHeight;
            float x = index % columns * cell, y = index / columns * cell;
            const float inset = 0.02f;
            return (tex?.Id ?? TextureId, tex == null && glyph < _offsets.Count ? _offsets[glyph] : 0,
                (x + inset) / width, (y + inset) / height, (x + cell - inset) / width, (y + cell - inset) / height);
        }

        public float Measure(string line) => MeasureRaw(LocalText.Ui(line));

        private float MeasureRaw(string line)
        {
            float width = 0;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                int glyph = Glyph(line, ref i);
                width += c > 255 ? ExtraGlyphs.Width(c) : Advance(glyph);
            }
            return width;
        }

        // Break into lines at '\n', and at spaces when a line would pass wrapWidth (0 = never).
        public List<string> Lines(string text, int wrapWidth) => Lines(text, wrapWidth, drawing: false);

        private List<string> Lines(string text, int wrapWidth, bool drawing)
        {
            var lines = new List<string>();
            string display = LocalText.Ui(text);
            if (drawing) ObserveDrawing?.Invoke(display);
            foreach (string raw in display.Split('\n'))
            {
                if (wrapWidth <= 0 || MeasureRaw(raw) <= wrapWidth)
                {
                    lines.Add(raw);
                    continue;
                }
                if (LocalText.UsesExtraGlyphs(raw))
                {
                    lines.AddRange(LocalText.WrapWide(raw, wrapWidth, MeasureRaw));
                    continue;
                }
                var sb = new StringBuilder();
                foreach (string word in raw.Split(' '))
                {
                    string candidate = sb.Length == 0 ? word : sb + " " + word;
                    if (sb.Length > 0 && MeasureRaw(candidate) > wrapWidth)
                    {
                        lines.Add(sb.ToString());
                        sb.Clear().Append(word);
                    }
                    else
                    {
                        sb.Clear().Append(candidate);
                    }
                }
                lines.Add(sb.ToString());
            }
            return lines;
        }

        // Emit text as glyph quads in MENU SPACE (Y up). (x, y) is the anchor in the menu file's text coordinates
        // (Y up from the bottom of the touch screen, so menu-space y = textY - 192): align 0 = left edge at x,
        // 1 = right edge at x, 2 = centred on x. Lines run downward `lineHeight` apart; y is the top of the first line.
        public void Emit(string text, float x, float y, int align, int wrapWidth, float lineHeight,
            float r, float g, float b, float a, float z, List<WidgetTri> output)
        {
            float top = y - 192 + lineHeight; // the anchor is the BOTTOM of the first line (checked against the game)
            foreach (string line in Lines(text, wrapWidth, drawing: true))
            {
                float width = MeasureRaw(line);
                float penX = align == 1 ? x - width : align == 2 ? x - MathF.Floor(width / 2) : x;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    bool extra = c > 255;
                    int glyph = Glyph(line, ref i), advance = extra ? ExtraGlyphs.Width(c) : Advance(glyph);
                    var (texture, offset, u0, v0, u1, v1) = Sprite(glyph, extra);
                    if (c != ' ' && texture >= 0)
                    {
                        float gy = top - offset, gx = penX + (extra ? (advance - Cell) / 2f : 0);
                        var p0 = new UiVertex(gx, gy, u0, v0, r, g, b, a);
                        var p1 = new UiVertex(gx + Cell, gy, u1, v0, r, g, b, a);
                        var p2 = new UiVertex(gx + Cell, gy - Cell, u1, v1, r, g, b, a);
                        var p3 = new UiVertex(gx, gy - Cell, u0, v1, r, g, b, a);
                        output.Add(new WidgetTri { A = p0, B = p1, C = p2, Z = z, TextureId = texture });
                        output.Add(new WidgetTri { A = p0, B = p2, C = p3, Z = z, TextureId = texture });
                    }
                    penX += advance;
                }
                top -= lineHeight;
            }
        }

        // Emit text straight into a canvas-space UiDrawList (Y down, origin top-left; a HUD bar, not a menu file):
        // (x, y) is the top-left of the first line, scale multiplies the 8px cell. align as Emit's.
        public void EmitCanvas(string text, float x, float y, int align, float scale,
            float r, float g, float b, float a, UiDrawList list)
        {
            float lineHeight = Cell * scale;
            float lineTop = y;
            foreach (string line in Lines(text, wrapWidth: 0, drawing: true))
            {
                float width = MeasureRaw(line) * scale;
                float penX = align == 1 ? x - width : align == 2 ? x - width / 2 : x;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    bool extra = c > 255;
                    int glyph = Glyph(line, ref i), advance = extra ? ExtraGlyphs.Width(c) : Advance(glyph);
                    var (texture, offset, u0, v0, u1, v1) = Sprite(glyph, extra);
                    if (c != ' ' && texture >= 0)
                    {
                        float glyphTop = lineTop + offset * scale, gx = penX + (extra ? (advance - Cell) * scale / 2 : 0);
                        list.Quad(texture, gx, glyphTop, gx + Cell * scale, glyphTop + Cell * scale, u0, v0, u1, v1, r, g, b, a);
                    }
                    penX += advance * scale;
                }
                lineTop += lineHeight;
            }
        }

        // MeasureCanvas: a line's width at EmitCanvas's scale (layout before drawing it)
        public float MeasureCanvas(string line, float scale) => Measure(line) * scale;
    }
}
