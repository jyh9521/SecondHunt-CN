using System.Text.Json;
using System.Text.RegularExpressions;
using MphRead;
using MphRead.Text;
using MphRecomp.Frontend;
using MphRecomp.Multiplayer.Net;
using MphRecomp.Online;
using SecondHunt.Desktop;

internal static class UiInventory
{
    private static readonly (int Call, int Page)[] RulesCases = { (103, 28), (105, 29), (101, 30),
        (102, 31), (99, 32), (106, 33), (104, 34) };

    internal static void OpenRules(FrontendSession session, int call = 103, int page = 28)
    {
        session.Menu.Enter(MultiplayerMenu.ModePage);
        if (!session.Multiplayer.OnCall(session.Menu, new MenuAction(), call, -1))
            throw new InvalidOperationException($"Mode callback {call} failed.");
        session.Menu.Enter(page);
    }

    internal static OnlineException ProtocolMismatch() => new(2, OnlineProtocol.Version);

    internal static void OpenProtocolError(FrontendSession session)
    {
        session.Recomp.OnlinePreview(new PlayerCard { Name = "测试猎人" }, new(), new(), 0, 0, ProtocolMismatch());
        session.Menu.Enter(RecompMenus.OnlinePages.Browse);
    }

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
        var displayed = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        var failures = new List<string>();
        var seen = new HashSet<string>();
        var drawn = new HashSet<string>();
        int? populatedReplayPage = null;
        var words = new Regex("[A-Za-z]{2,}", RegexOptions.CultureInvariant);
        LocalText.ObserveMissingUi = s => { if (s.Length > 1 && words.IsMatch(s)) seen.Add(s); };
        session.Menu.Font.ObserveDrawing = s => { if (!string.IsNullOrWhiteSpace(s)) drawn.Add(s); };
        void Capture(string name)
        {
            displayed[name] = drawn.Order().ToArray();
            drawn.Clear();
        }
        void Render()
        {
            for (int i = 0; i < 56; i++) session.Tick();
            session.Build(1280, 720);
        }
        try
        {
            for (int page = 0; page < session.Menu.File.Pages.Count; page++)
            {
                try
                {
                    seen.Clear();
                    drawn.Clear();
                    session.Menu.Enter(page);
                    for (int i = 0; i < 56 && session.Menu.Page?.Index == page; i++)
                    {
                        session.Tick();
                        if (i is 0 or 8 or 24 or 55) session.Build(1280, 720);
                    }
                    if (seen.Count != 0) misses[page] = seen.Order().ToArray();
                    Capture("page " + page);
                    if (seen.Any(s => s.StartsWith("your nickname is\n", StringComparison.OrdinalIgnoreCase)
                        || Regex.IsMatch(s, @"^IMPORT \d+/\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
                        failures.Add($"page {page}: untranslated nickname notice or paginated import heading");
                }
                catch (Exception ex) { failures.Add($"page {page}: {ex.GetType().Name}: {ex.Message}"); }
            }
            foreach ((int call, int page) in RulesCases)
            {
                OpenRules(session, call, page);
                drawn.Clear();
                Render();
                if (!drawn.Any(s => s.StartsWith("地图大小：", StringComparison.Ordinal))
                    || !drawn.Any(s => s.StartsWith("额外内容：", StringComparison.Ordinal)))
                    failures.Add($"rules {page}: localized size or advanced summary did not draw");
                if (page == 28 && !drawn.Contains("团队对战"))
                    failures.Add("rules 28: team play did not draw in Chinese");
                Capture("populated rules " + page);
                for (int change = 0; change < 3; change++)
                {
                    foreach (int rule in new[] { 118, 119, 120, 121, 113 })
                        session.Multiplayer.OnCall(session.Menu, new MenuAction(), rule, -1);
                    Render();
                }
                if (drawn.Any(s => Regex.IsMatch(s, @"\b(?:RADAR (?:OFF|ON)|DAMAGE (?:LOW|MED|HIGH)|F\.F\. (?:OFF|ON)|"
                    + @"DEFAULT WEAPONS|AFFINITY WEAPONS|EXTRAS:|MAP SIZE:|TEAM PLAY)\b", RegexOptions.IgnoreCase)))
                    failures.Add($"rules {page}: an English advanced variant drew");
                Capture("alternate rules " + page);
            }
            drawn.Clear();
            OpenProtocolError(session);
            Render();
            if (!drawn.Contains($"服务器协议为 2，本版本使用 {OnlineProtocol.Version}，请更新游戏。"))
                failures.Add("online browse: localized protocol reason did not draw in full");
            Capture("browse incompatible protocol");
            session.Recomp.OnlinePreview(new PlayerCard { Name = "测试猎人" }, new(), new(), 0, 0,
                new OnlineException("fixture_unknown", "Unrecognized server diagnostic (fixture)."));
            session.Menu.Enter(RecompMenus.OnlinePages.Browse);
            Render();
            if (!drawn.Contains("线上操作失败，详细原因见日志。")
                || drawn.Any(s => s.Contains("Unrecognized server diagnostic", StringComparison.OrdinalIgnoreCase)))
                failures.Add("online browse: unknown diagnostic leaked into Chinese UI");
            Capture("browse other error");
            var owner = new PlayerCard { Name = "测试房主", Code = "222222222222", Tag = 42 };
            var card = new GameCard
            {
                Id = "local-preview-only", Host = owner, Members = new() { owner }, Players = 1,
                Info = new GameInfo
                {
                    Mode = "Battle", Arena = "MP3 PROVING GROUND", Version = OnlineMatch.VersionKey(LanSession.RomKeyFor(Paths.MphKey)),
                    Friends = true, Rivals = true, ExtraPickups = false, ExtraCharacters = false
                }
            };
            session.Recomp.OnlinePreview(new PlayerCard { Name = "测试猎人" }, new() { card }, new(), 1, 25);
            session.Menu.Enter(RecompMenus.OnlinePages.Browse);
            Render();
            Capture("populated browse");
            session.Recomp.OnCall(session.Menu, 2520, inPause: false); // the first card's view action, not JOIN
            Render();
            if (session.Menu.Page?.Index != RecompMenus.OnlinePages.Detail
                || !drawn.Contains("好友和对手") || !drawn.Contains("关闭额外拾取物和角色"))
                failures.Add("populated online card: detail page or translated facts were missing");
            Capture("populated online detail");
            session.Recomp.OnlinePreview(new PlayerCard { Name = "测试猎人" }, new(), new(), 0, 0);
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
                    drawn.Clear();
                    session.TouchCanvas(600, 193); // first populated row at the known desktop layout
                    for (int i = 0; i < 40; i++) session.Tick();
                    session.Build(1280, 720);
                    populatedReplayPage = session.Menu.Page?.Index;
                    if (populatedReplayPage != list + 1) failures.Add("first replay row did not open its details");
                    string[] labels = { "date", "length", "result", "favorite", "you", "bot" };
                    foreach (string label in labels.Where(w => seen.Contains(w))) failures.Add($"replay details: untranslated {label}");
                    Capture("populated replay");
                }
                catch (Exception ex) { failures.Add($"populated replay: {ex.GetType().Name}: {ex.Message}"); }
            }
            // Credits are original attributions; trademarks, hardware and actual
            // file names are not UI instructions. Replay nicknames are player data.
            var retained = new Regex(@"\b(?:Second Hunt|GameCube|Switch|Wii|Wi-Fi|DS|mod\.json|mods|IP)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            foreach ((string at, string[] strings) in displayed)
            {
                if (at is "page 7" or "page 8" or "page 9" or "page 10" or "page 11" or "populated replay") continue;
                foreach (string value in strings.Where(s => words.IsMatch(retained.Replace(s, ""))))
                    failures.Add($"{at}: unexplained Latin text {value.Replace('\n', ' ')}");
            }
        }
        finally { LocalText.ObserveMissingUi = null; session.Menu.Font.ObserveDrawing = null; }
        File.WriteAllText(report, JsonSerializer.Serialize(new { pages = session.Menu.File.Pages.Count, misses, displayed, populatedReplayPage, failures },
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"{session.Menu.File.Pages.Count} page entries, {misses.Values.SelectMany(x => x).Distinct().Count()} candidate strings, {failures.Count} traversal errors.");
        Console.WriteLine($"{RulesCases.Length} populated rules pages, 2 offline diagnostics, populated browse/detail, "
            + $"{displayed.Values.SelectMany(x => x).Distinct().Count()} distinct drawn strings.");
        foreach (string failure in failures) Console.Error.WriteLine(failure);
        if (populatedReplayPage != null) Console.WriteLine($"Populated replay opened page {populatedReplayPage}.");
        return failures.Count == 0 ? 0 : 1;
    }
}
