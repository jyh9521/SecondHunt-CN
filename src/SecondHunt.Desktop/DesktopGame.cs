using System.Text;
using MphRead.Text;
using MphRecomp.App;

namespace SecondHunt.Desktop;

internal static class DesktopGame
{
    private static readonly string[] Revisions = ["AMHE0", "AMHE1"];

    private static string RomDirectory(string game, string revision) => Path.Combine(game, "files", revision);
    private static bool IsUsExtraction(string game, string revision)
    {
        string rom = RomDirectory(game, revision);
        return File.Exists(Path.Combine(rom, "frontend", "metroidhunters.bin"))
            && File.Exists(Path.Combine(rom, "frontend", "metroidhunters_text_en.bin"))
            && File.Exists(Path.Combine(rom, "ftc", "arm9.bin"))
            && File.Exists(Path.Combine(rom, "stringTables", "ScanLog.bin"));
    }

    private static string? Revision(string game)
    {
        string marker = RomSetup.MarkerPath(game);
        if (!File.Exists(marker)) return null;
        DesktopStorage.RequireOrdinaryDirectory(game);
        DesktopStorage.RequireOrdinaryEntry(marker);
        string? rev = File.ReadLines(marker).FirstOrDefault(s => s.StartsWith("rom=", StringComparison.Ordinal))?[4..];
        return rev != null && Revisions.Contains(rev, StringComparer.OrdinalIgnoreCase) && IsUsExtraction(game, rev)
            ? rev.ToUpperInvariant() : null;
    }

    public static bool HasExtraction(string game) => Revision(game) != null;

    public static bool Prepare(string game)
    {
        if (Revision(game) is not string rev) return false;
        PrepareExtracted(game, rev);
        DesktopLocale.Activate(game, RomDirectory(game, rev));
        return true;
    }

    private static void PrepareExtracted(string game, string rev)
    {
        DesktopStorage.RequireOrdinaryDirectory(RomDirectory(game, rev));
        DesktopLocale.Install(game);
        // Upstream writes an absolute paths.txt, which would still point into
        // LocalAppData (or the staging directory) after the whole folder moves.
        string path = Path.Combine(game, "paths.txt");
        string newPaths = $"{MphRead.AppInfo.Version}\n{rev}=files/{rev}\n";
        if (File.Exists(path))
        {
            DesktopStorage.RequireOrdinaryEntry(path);
            // Preserve optional imported First Hunt references; leave retired US/region
            // paths out so the chosen .extracted marker is always authoritative.
            foreach (string line in File.ReadLines(path))
                if (line.StartsWith("AMFE0=", StringComparison.Ordinal) || line.StartsWith("AMFP0=", StringComparison.Ordinal))
                    newPaths += line + "\n";
            if (File.ReadAllText(path) == newPaths) return;
        }
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, newPaths, new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static bool Extract(string rom, string game, Action<string> log)
    {
        string originalCwd = Environment.CurrentDirectory;
        // The upstream extractor needs a clean working directory (it otherwise asks
        // a console question). A failing patch or ROM never publishes a partial tree.
        string staging = game + ".unpack-" + Guid.NewGuid().ToString("N");
        try
        {
            if (RomSetup.Unsupported(rom) is string why) { log(why); return false; }
            if (!RomSetup.Extract(rom, staging, log)) return false;
            Directory.SetCurrentDirectory(originalCwd);
            string rev = Revision(staging) ?? throw new InvalidDataException("No supported USA extraction was produced.");
            if (DesktopLocale.Name != "en") log(LocalText.Ui("Applying the translation pack…"));
            PrepareExtracted(staging, rev);
            string? previous = null;
            if (Directory.Exists(game))
            {
                DesktopStorage.RequireOrdinaryEntry(game);
                if (Directory.EnumerateFileSystemEntries(game).Any())
                {
                    if (HasExtraction(game)) return Prepare(game);
                    previous = game + ".previous-" + Guid.NewGuid().ToString("N");
                    Directory.Move(game, previous); // a legacy partial extraction is retained, not deleted
                }
                else Directory.Delete(game);
            }
            try { Directory.Move(staging, game); }
            catch
            {
                if (previous != null && !Directory.Exists(game)) Directory.Move(previous, game);
                throw;
            }
            DesktopLocale.Activate(game, RomDirectory(game, rev));
            log(LocalText.Ui("Setup complete."));
            return true;
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }
}
