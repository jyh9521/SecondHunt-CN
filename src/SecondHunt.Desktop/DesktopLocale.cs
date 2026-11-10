using MphRead.Text;
using System.Reflection;
using System.Text.Json;

namespace SecondHunt.Desktop;

internal static class DesktopLocale
{
    private const string Default = "zh-CN";
    private const string English = "en";
    public static string Name { get; private set; } = Default;
    public static string PackPath(string game) => Path.Combine(game, "localization", Default + ".json");

    public static void Init(string? requested)
    {
        string preference = Path.Combine(AppContext.BaseDirectory, "locale.txt");
        if (requested == null && File.Exists(preference))
        {
            DesktopStorage.RequireOrdinaryEntry(preference);
            using var reader = File.OpenText(preference);
            requested = reader.ReadLine()?.Trim();
        }
        Name = requested?.ToLowerInvariant() switch
        {
            null or "" or "zh-cn" => Default,
            "en" => English,
            _ => throw new ArgumentException($"Unknown locale: {requested}. Use zh-CN or en.")
        };
        LocalizedArtwork.Current = null;
        if (Name == English) { LocalText.Configure(null, null); return; }
        var identified = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string resource in new[] { "settings.json", "terms.json", "extras.json" })
            foreach (var pair in LoadCatalogue(resource))
                identified.Add(pair.Key, pair.Value);
        using Stream glyph = Resource("glyphs.bin");
        using var glyphBytes = new MemoryStream();
        glyph.CopyTo(glyphBytes);
        var atlas = new ExtraGlyphs(glyphBytes.ToArray());
        using Stream art = Resource("artwork.json");
        using Stream masks = Resource("artwork-masks.json");
        using Stream artFont = Resource("artglyphs.bin");
        using var artFontBytes = new MemoryStream();
        artFont.CopyTo(artFontBytes);
        LocalizedArtwork.Current = LocalizedArtwork.Load(art, masks, artFontBytes.ToArray());
        LocalText.Configure(LoadCatalogue("ui.json"), atlas, identified);
    }

    private static Dictionary<string, string> LoadCatalogue(string name)
    {
        using Stream stream = Resource(name);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException($"Missing locale catalogue: {name}");
    }

    // Parse the built-in pack before changing the install. The installed copy has no ROM
    // bytes; it overrides the original strings at read/render time, and is easy to remove.
    public static void Install(string game)
    {
        if (Name == English) return;
        using Stream source = Resource("rom.json");
        using var copy = new MemoryStream();
        source.CopyTo(copy);
        copy.Position = 0;
        _ = RomTextPack.Load(copy, Name);
        byte[] bytes = copy.ToArray();
        string path = PackPath(game);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        DesktopStorage.RequireOrdinaryDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            DesktopStorage.RequireOrdinaryEntry(path);
            if (new FileInfo(path).Length == bytes.Length && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                return;
        }
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void Activate(string game, string rom)
    {
        if (Name == English) { LocalText.SetRom(null, null); return; }
        using Stream installed = File.OpenRead(PackPath(game));
        LocalText.SetRom(rom, RomTextPack.Load(installed, Name));
    }

    private static Stream Resource(string name)
        => Assembly.GetExecutingAssembly().GetManifestResourceStream("locales." + Default + "." + name)
            ?? throw new InvalidDataException($"Missing locale resource: {name}");
}
