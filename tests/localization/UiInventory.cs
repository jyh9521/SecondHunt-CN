using System.Text.Json;
using System.Text.RegularExpressions;
using MphRead;
using MphRead.Text;
using MphRecomp.Frontend;
using MphRecomp.Online;
using SecondHunt.Desktop;

internal static class UiInventory
{
    // Traverse the renderer, not just the source dictionary. The offline preview data
    // suppresses live online refresh; no online identity, saves or settings are persisted.
    public static int Run(string game, string report, string? replayData = null)
    {
        game = Path.GetFullPath(game);
        string assetRoot = Path.Combine(game, "files", Ver.AMHE0);
        Paths.SetPath(Ver.AMHE0, assetRoot);
        Paths.MphKey = Ver.AMHE0;
        DesktopLocale.Init("zh-CN");
        DesktopLocale.Activate(game, assetRoot);
        var session = FrontendSession.Load(assetRoot,
            settingsPath: replayData == null ? null : Path.Combine(Path.GetFullPath(replayData), "recomp_settings.json"));
        session.Recomp.OnlinePreview(new PlayerCard { Name = "Hunter", Code = "123456789012" },
            new(), new(), hunters: 0, pingMs: 0);
        var misses = new SortedDictionary<int, string[]>();
        var failures = new List<string>();
        var seen = new HashSet<string>();
        int? populatedReplayPage = null;
        var words = new Regex("[A-Za-z]{2,}", RegexOptions.CultureInvariant);
        LocalText.ObserveMissingUi = s => { if (s.Length > 1 && words.IsMatch(s)) seen.Add(s); };
        try
        {
            for (int page = 0; page < session.Menu.File.Pages.Count; page++)
            {
                try
                {
                    seen.Clear();
                    session.Menu.Enter(page);
                    for (int i = 0; i < 56 && session.Menu.Page?.Index == page; i++)
                    {
                        session.Tick();
                        if (i is 0 or 8 or 24 or 55) session.Build(1280, 720);
                    }
                    if (seen.Count != 0) misses[page] = seen.Order().ToArray();
                    if (seen.Any(s => s.StartsWith("your nickname is\n", StringComparison.OrdinalIgnoreCase)
                        || Regex.IsMatch(s, @"^IMPORT \d+/\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
                        failures.Add($"page {page}: untranslated nickname notice or paginated import heading");
                }
                catch (Exception ex) { failures.Add($"page {page}: {ex.GetType().Name}: {ex.Message}"); }
            }
            if (replayData != null)
            {
                try
                {
                    // A replay file exercises the labels that the empty-state traversal never draws.
                    int list = session.Recomp.ReplaysPage;
                    session.Menu.Enter(list);
                    for (int i = 0; i < 40; i++) session.Tick();
                    session.Build(1280, 720);
                    seen.Clear();
                    session.TouchCanvas(600, 193); // first populated row at the known desktop layout
                    for (int i = 0; i < 40; i++) session.Tick();
                    session.Build(1280, 720);
                    populatedReplayPage = session.Menu.Page?.Index;
                    if (populatedReplayPage != list + 1) failures.Add("first replay row did not open its details");
                    string[] labels = { "date", "length", "result", "favorite", "you", "bot" };
                    foreach (string label in labels.Where(w => seen.Contains(w))) failures.Add($"replay details: untranslated {label}");
                }
                catch (Exception ex) { failures.Add($"populated replay: {ex.GetType().Name}: {ex.Message}"); }
            }
        }
        finally { LocalText.ObserveMissingUi = null; }
        File.WriteAllText(report, JsonSerializer.Serialize(new { pages = session.Menu.File.Pages.Count, misses, populatedReplayPage, failures },
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"{session.Menu.File.Pages.Count} page entries, {misses.Values.SelectMany(x => x).Distinct().Count()} candidate strings, {failures.Count} traversal errors.");
        if (populatedReplayPage != null) Console.WriteLine($"Populated replay opened page {populatedReplayPage}.");
        return failures.Count == 0 ? 0 : 1;
    }
}
