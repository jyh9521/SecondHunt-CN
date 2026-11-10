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
    public static int Run(string game, string report)
    {
        game = Path.GetFullPath(game);
        string assetRoot = Path.Combine(game, "files", Ver.AMHE0);
        Paths.SetPath(Ver.AMHE0, assetRoot);
        Paths.MphKey = Ver.AMHE0;
        DesktopLocale.Init("zh-CN");
        DesktopLocale.Activate(game, assetRoot);
        var session = FrontendSession.Load(assetRoot);
        session.Recomp.OnlinePreview(new PlayerCard { Name = "Hunter", Code = "123456789012" },
            new(), new(), hunters: 0, pingMs: 0);
        var misses = new SortedDictionary<int, string[]>();
        var failures = new List<string>();
        var seen = new HashSet<string>();
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
                }
                catch (Exception ex) { failures.Add($"page {page}: {ex.GetType().Name}: {ex.Message}"); }
            }
        }
        finally { LocalText.ObserveMissingUi = null; }
        File.WriteAllText(report, JsonSerializer.Serialize(new { pages = session.Menu.File.Pages.Count, misses, failures },
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"{session.Menu.File.Pages.Count} page entries, {misses.Values.SelectMany(x => x).Distinct().Count()} candidate strings, {failures.Count} traversal errors.");
        return failures.Count == 0 ? 0 : 1;
    }
}
