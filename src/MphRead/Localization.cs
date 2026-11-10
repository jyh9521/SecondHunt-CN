using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MphRead.Text
{
    // One host-selected locale per process. ROM lookups are bound to the extracted root and
    // to the exact original text, not to offsets that can drift between ROM revisions.
    public static class LocalText
    {
        private static IReadOnlyDictionary<string, string>? _ui;
        private static IReadOnlyDictionary<string, string>? _caseFolded;
        private static IReadOnlyDictionary<string, string>? _identified;
        private static RomTextPack? _rom;
        private static string? _romRoot;
        public static ExtraGlyphs? Glyphs { get; private set; }
        public static bool IsLocalized => _ui != null;
        // Development census: observes untranslated display lookups without changing rendering.
        public static Action<string>? ObserveMissingUi { get; set; }

        public static void Configure(IReadOnlyDictionary<string, string>? ui, ExtraGlyphs? glyphs,
            IReadOnlyDictionary<string, string>? identified = null)
        {
            _ui = ui;
            if (ui == null) _caseFolded = null;
            else
            {
                var folded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (source, value) in ui)
                {
                    if (!folded.TryAdd(source, value) && folded[source] != value) ambiguous.Add(source);
                }
                foreach (string source in ambiguous) folded.Remove(source);
                _caseFolded = folded;
            }
            _identified = identified;
            Glyphs = glyphs;
        }

        public static string Ui(string source)
        {
            if (_ui == null) return source;
            if (_ui.TryGetValue(source, out string? text) || _caseFolded!.TryGetValue(source, out text)) return text;
            ObserveMissingUi?.Invoke(source);
            return source;
        }

        // For generated UI, a stable ID is independent of edits to the English wording.
        public static string Id(string id, string fallback)
            => _identified != null && _identified.TryGetValue(id, out string? translated) ? translated : Ui(fallback);

        public static string FormatId(string id, string fallback, params object?[] values)
            => string.Format(CultureInfo.InvariantCulture, Id(id, fallback), values);

        public static string Format(string source, params object?[] values)
            => string.Format(CultureInfo.InvariantCulture, Ui(source), values);

        // Only built-in bot aliases are display text. Human names (including names that
        // match an alias) and custom character names stay as the player supplied them.
        public static string MatchNickname(string nickname, Hunter hunter, bool bot, int generatedPlayer = 0)
        {
            if (generatedPlayer > 0) return FormatId("match.player.number", "Player{0}", generatedPlayer);
            if (!bot || !IsLocalized) return nickname;
            return string.Equals(nickname, hunter.ToString(), StringComparison.OrdinalIgnoreCase)
                ? Id("hunter." + hunter, nickname)
                : Id("match.bot." + hunter + "." + nickname.ToLowerInvariant(), nickname);
        }

        public static void SetRom(string? root, RomTextPack? rom)
        {
            _romRoot = root == null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            _rom = rom;
            Strings.ClearCache();
        }

        private static bool Matches(string root)
            => _romRoot != null && string.Equals(_romRoot, Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public static RomTextPack? ForRom(string root) => _rom != null && Matches(root) ? _rom : null;

        // A translated string contains native Unicode. ROM strings are Latin-1 byte containers,
        // so leave their two-byte glyph codes alone; pack text uses ASCII and code points > 255.
        // Break a wide-script line between characters, but keep short Latin words intact.
        public static IEnumerable<string> WrapWide(string line, int width, Func<string, float> measure)
        {
            int start = 0;
            while (start < line.Length)
            {
                int end = start + 1, boundary = -1;
                while (end < line.Length && measure(line[start..(end + 1)]) <= width)
                {
                    if (char.IsWhiteSpace(line[end - 1]) || line[end - 1] > 255 || line[end] > 255)
                        boundary = end;
                    end++;
                }
                if (end < line.Length)
                {
                    if (char.GetUnicodeCategory(line[end]) is UnicodeCategory.ClosePunctuation
                        or UnicodeCategory.FinalQuotePunctuation or UnicodeCategory.OtherPunctuation && line[end] > 255)
                        end++;
                    else if (boundary > start && !char.IsWhiteSpace(line[end])
                        && line[end - 1] <= 255 && line[end] <= 255)
                        end = boundary;
                }
                yield return line[start..end].TrimEnd();
                start = end;
                while (start < line.Length && line[start] == ' ') start++;
            }
            if (line.Length == 0) yield return "";
        }

        public static bool UsesExtraGlyphs(ReadOnlySpan<char> text)
        {
            if (Glyphs == null) return false;
            foreach (char c in text)
                if (c > 255) return true;
            return false;
        }
    }

    public sealed class RomTextPack
    {
        private readonly IReadOnlyDictionary<string, string> _menu;
        private readonly Dictionary<string, Dictionary<string, Entry>> _tables;
        private sealed record Entry(string Source, string? First, string? Second);

        private RomTextPack(Dictionary<string, string> menu, Dictionary<string, Dictionary<string, Entry>> tables)
        {
            _menu = menu;
            _tables = tables;
        }

        public static RomTextPack Load(Stream stream, string locale)
        {
            using JsonDocument json = JsonDocument.Parse(stream);
            JsonElement doc = json.RootElement;
            if (doc.GetProperty("version").GetInt32() != 1 || doc.GetProperty("locale").GetString() != locale)
                throw new InvalidDataException("Unsupported ROM translation pack.");
            var menu = new Dictionary<string, string>(StringComparer.Ordinal);
            var tables = new Dictionary<string, Dictionary<string, Entry>>(StringComparer.Ordinal);
            foreach (JsonProperty entry in doc.GetProperty("menu").EnumerateObject())
                menu.Add(entry.Name, entry.Value.GetString() ?? throw new InvalidDataException("Empty menu translation."));
            foreach (JsonProperty table in doc.GetProperty("tables").EnumerateObject())
            {
                var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
                foreach (JsonProperty entry in table.Value.EnumerateObject())
                {
                    JsonElement value = entry.Value;
                    string source = value.GetProperty("source").GetString() ?? "";
                    string? first = value.TryGetProperty("first", out JsonElement f) ? f.GetString() : null;
                    string? second = value.TryGetProperty("second", out JsonElement s) ? s.GetString() : null;
                    if (source.Length != 64 || (first == null && second == null))
                        throw new InvalidDataException($"Invalid translation entry {table.Name}:{entry.Name}.");
                    entries.Add(entry.Name, new Entry(source, first, second));
                }
                tables.Add(table.Name, entries);
            }
            return new RomTextPack(menu, tables);
        }

        private static string Key(string value)
            => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        public string? Menu(string source)
            => source.Length != 0 && _menu.TryGetValue(Key(source), out string? text) ? text : null;

        public StringTableEntry Apply(string table, StringTableEntry original)
        {
            if (!_tables.TryGetValue(table, out Dictionary<string, Entry>? rows)
                || !rows.TryGetValue(original.Id, out Entry? entry)
                || entry.Source != Key(original.Value1 + "\0" + original.Value2))
                return original;
            return new StringTableEntry(original.Id, original.Prefix, entry.First ?? original.Value1,
                entry.Second ?? original.Value2, original.Speed, original.Category);
        }
    }

    // 16x16 antialiased glyphs in a compact, locale-independent bitmap format.
    public sealed class ExtraGlyphs
    {
        public const int Advance = 8;
        private readonly Dictionary<char, int> _indices = new();
        public int Size { get; }
        public byte[] Alpha { get; }
        public int Count => _indices.Count;

        public ExtraGlyphs(ReadOnlySpan<byte> data)
        {
            Size = data.StartsWith("SHG2"u8) ? 32 : data.StartsWith("SHG1"u8) ? 16 : 0;
            int pixels = Size * Size, stride = 2 + pixels;
            if (Size == 0 || data.Length < 4 + stride || (data.Length - 4) % stride != 0)
                throw new InvalidDataException("Invalid locale glyph atlas.");
            int n = (data.Length - 4) / stride;
            Alpha = new byte[n * pixels];
            for (int i = 0; i < n; i++)
            {
                int start = 4 + i * stride;
                _indices.Add((char)BinaryPrimitives.ReadUInt16LittleEndian(data[start..]), i);
                data.Slice(start + 2, pixels).CopyTo(Alpha.AsSpan(i * pixels));
            }
            if (!_indices.ContainsKey('□')) throw new InvalidDataException("Locale glyph atlas lacks a fallback.");
        }

        // Unicode halfwidth forms use half the pen advance; the centred bitmap is
        // positioned within that space (no downsampling of a small mark).
        public static int Width(char c) => c is >= '\uFF61' and <= '\uFF9F' ? Advance / 2 : Advance;

        public int Index(char c) => _indices.TryGetValue(c, out int i) ? i : _indices['□'];
    }
}
