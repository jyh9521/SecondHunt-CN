using System.Security.Cryptography;
using System.Text;
using MphRead;
using MphRead.Text;
using MphRecomp.Config;
using MphRecomp.Frontend;
using SecondHunt.Desktop;
using AccessAction = SecondHunt.Desktop.DesktopStorage.AccessAction;

Console.OutputEncoding = Encoding.UTF8;
// The shipped launcher sets these at startup; the test runs its libraries directly.
RecompSettings.PublicBuild = true;
RecompSettings.DesktopHost = true;
if (args.Length is >= 2 and <= 4 && args[0] == "--hud-readability")
    return HudReadability.Run(args[1], args.Length >= 3 ? args[2] : null, args.Length == 4 ? args[3] : "zh-CN");
if (args is ["--media-inventory", string assets, string output, string stride])
    return MediaInventory.Run(assets, output, int.Parse(stride));
if (args is ["--movie-inventory", string movie, string movieOutput, string movieStride])
    return MediaInventory.SingleMovie(movie, movieOutput, int.Parse(movieStride));
if (args is ["--ui-inventory", string extracted, string uiReport])
    return UiInventory.Run(extracted, uiReport);
if (args is ["--ui-inventory", string populatedGame, string populatedReport, string replayData])
    return UiInventory.Run(populatedGame, populatedReport, replayData);
if (args.Length is 2 or 3 && args[0] == "--artwork-check")
    return ArtworkCheck.Run(args[1], args.Length == 3 ? args[2] : null);
if (args is ["--settings-only"])
{
    DesktopLocale.Init("zh-CN");
    string hint = LocalText.Ui(ControlHints.For("maplook.map"));
    Console.WriteLine("MAP_HINT=" + hint);
    const string expected = "暂停地图使用原版房间模型，在地图中按 Y 切换。";
    if (hint != expected)
    {
        Console.Error.WriteLine("FAIL: the selected Map Look value did not produce its Chinese hint.");
        return 1;
    }
    Console.WriteLine("PASS: selected Map Look hint is translated.");
    return CheckSettingsCatalogue() ? 0 : 1;
}
if (args.Length != 2 || !Directory.Exists(args[0]) || !Directory.Exists(args[1]))
{
    Console.Error.WriteLine("usage: SecondHunt.LocalizationTests <extracted-game-dir> <fixture-parent>");
    return 2;
}
string game = Path.GetFullPath(args[0]), parent = Path.GetFullPath(args[1]);
string root = Path.Combine(parent, "checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int passed = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new InvalidOperationException(name);
    Console.WriteLine($"PASS {++passed}: {name}");
}

string install = Path.Combine(root, "Program Files", "Second Hunt");
string old = Path.Combine(root, "legacy"), oldGame = Path.Combine(old, "game");
string data = DesktopStorage.DefaultRoot(install);
string key = "AMHE0";
string stub = Path.Combine(oldGame, "files", key);
foreach (string dir in new[] { "frontend", "ftc", "stringTables" }) Directory.CreateDirectory(Path.Combine(stub, dir));
File.WriteAllText(Path.Combine(stub, "frontend", "metroidhunters.bin"), "placeholder");
File.WriteAllText(Path.Combine(stub, "frontend", "metroidhunters_text_en.bin"), "placeholder");
File.WriteAllText(Path.Combine(stub, "ftc", "arm9.bin"), "placeholder");
File.WriteAllText(Path.Combine(stub, "stringTables", "ScanLog.bin"), "placeholder");
File.WriteAllText(Path.Combine(oldGame, ".extracted"), "version=0.35.0.0\nrom=AMHE0\nfiles=4\n");
File.WriteAllText(Path.Combine(oldGame, "paths.txt"), $"AMHE0={stub}\n");
Directory.CreateDirectory(Path.Combine(old, "saves"));
File.WriteAllText(Path.Combine(old, "saves", "campaign1.json"), "old save");
File.WriteAllText(Path.Combine(old, "recomp_settings.json"), "old settings");
Directory.CreateDirectory(data);

Check(data == Path.Combine(install, "data"), "default root is executable-dir/data");
Check(DesktopStorage.IsUnder(install, Path.Combine(root, "PROGRAM FILES")), "case-insensitive Program Files descendant");
Check(!DesktopStorage.IsUnder(install + " Backup", install), "prefix-matching sibling is not a descendant");
Check(DesktopStorage.Decide(true, true, true, false) == AccessAction.Ready, "writable path does not ask for UAC");
Check(DesktopStorage.Decide(false, true, true, false) == AccessAction.RequestConsent, "protected fresh default requests consent");
Check(DesktopStorage.Decide(false, true, true, true) == AccessAction.Stop, "existing data is never re-permissioned");
Check(DesktopStorage.Decide(false, false, true, false) == AccessAction.Stop, "explicit --data never elevates");
Check(DesktopStorage.Decide(false, true, false, false) == AccessAction.Stop, "non-Program Files never elevates");
Check(DesktopStorage.Provision([DesktopStorage.ProvisionArg, "S-1-5-21-1-2-3-1000"]) == 2,
    "elevated helper rejects an invocation from a non-Program Files build");
DesktopStorage.Migrate(old, data);
string newGame = Path.Combine(data, "game");
string save = Path.Combine(data, "saves", "campaign1.json");
Check(DesktopGame.HasExtraction(newGame) && File.Exists(Path.Combine(oldGame, ".extracted")),
    "legacy extraction migrated without removing original");
Check(File.ReadAllText(save) == "old save" && File.ReadAllText(Path.Combine(data, "recomp_settings.json")) == "old settings",
    "legacy saves and settings copied");
File.WriteAllText(save, "new save");
DesktopStorage.Migrate(old, data);
Check(File.ReadAllText(save) == "new save" && File.ReadAllText(Path.Combine(old, "saves", "campaign1.json")) == "old save",
    "subsequent migration preserves both saves");

DesktopLocale.Init("zh-CN");
Check(DesktopGame.Prepare(newGame), "old absolute paths upgraded");
string moved = Path.Combine(root, "moved-game");
Directory.Move(newGame, moved);
string cwd = Environment.CurrentDirectory;
try
{
    Directory.SetCurrentDirectory(moved);
    Paths.UpdatePaths();
    Paths.ChooseMphPath();
    string absolute = Path.Combine(moved, "files", key);
    Check(Paths.FileSystem == absolute, "moved extraction resolves at its new location");
    Directory.SetCurrentDirectory(absolute);
    Check(Paths.FileSystem == absolute, "renderer CWD changes do not break resolved paths");
}
finally { Directory.SetCurrentDirectory(cwd); }

string bad = Path.Combine(root, "invalid.nds");
File.WriteAllText(bad, "not a DS image");
string badGame = Path.Combine(root, "invalid-game");
var progress = new List<string>();
Check(!DesktopGame.Extract(bad, badGame, progress.Add) && !DesktopGame.HasExtraction(badGame)
    && !Directory.EnumerateDirectories(root, "invalid-game.unpack-*").Any(),
    "invalid ROM leaves no extraction, no translation or staging tree");
Check(progress.Single().Contains("文件过小"), "ROM error is localized");

string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
try
{
    DesktopLocale.Init("en");
    Check(DesktopGame.Prepare(game), "known USA extraction is valid");
    Directory.SetCurrentDirectory(game);
    Paths.UpdatePaths();
    Paths.ChooseMphPath();
    key = Paths.MphKey;
    var englishMenu = MenuStrings.Load(Paths.FileSystem);
    var originals = StringTables.All.ToDictionary(t => t, t => Strings.ReadStringTable(t));
    string fontModel = Path.Combine(Paths.FileSystem, "models", "hudfont_Model.bin");
    string nativeTable = Path.Combine(Paths.FileSystem, "stringTables", "ScanLog.bin");
    string beforeModel = HashFile(fontModel), beforeTable = HashFile(nativeTable);

    DesktopLocale.Init("zh-CN");
    Check(DesktopGame.Prepare(game), "post-extract translation pack installed");
    string packPath = DesktopLocale.PackPath(game);
    string installed = HashFile(packPath);
    DateTime written = File.GetLastWriteTimeUtc(packPath);
    Check(DesktopGame.Prepare(game) && HashFile(packPath) == installed && File.GetLastWriteTimeUtc(packPath) == written,
        "restart does not rewrite an unchanged locale pack");
    Check(HashFile(fontModel) == beforeModel && HashFile(nativeTable) == beforeTable, "original extracted ROM resources untouched");
    var chineseMenu = MenuStrings.Load(Paths.FileSystem);
    int begin = englishMenu.Strings.ToList().IndexOf("BEGIN GAME");
    Check(begin >= 0 && chineseMenu[begin] == "BEGIN GAME" && chineseMenu.Display(begin) == "开始游戏",
        "display changes without altering the menu callback's original string");
    chineseMenu.Fill(begin, "runtime value");
    Check(chineseMenu.Display(begin) == "runtime value", "runtime values override original ROM menu translations");
    var originalLog = originals[StringTables.ScanLog].First(x => x.Id == "L001");
    var log = Strings.GetScanEntry(1)!;
    Check(log.Value1 == "扫描面罩" && log.String1 == "扫描面罩"
        && log.Speed == originalLog.Speed && log.Category == originalLog.Category,
        "scan log renders Unicode and preserves metadata");
    var msg = Strings.GetEntry('M', 5, StringTables.GameMessages)!;
    Check(msg.Value1.Contains("&tab0") && msg.Value2.Contains("&tab0") && msg.Value2.Contains("&tab1"),
        "campaign substitution tokens preserved");
    Check(LocalText.Ui("no replays yet.") == "暂无回放"
        && LocalText.Format("Windows setup did not create a writable directory (exit code {0}).", 7)
            == "未能创建可写目录（退出码 7）"
        && LocalText.FormatId("ship.slot.saved", "saved to slot {0}.", 2) == "已将进度保存到栏位 2",
        "standalone system notices omit the final stop and preserve parentheses and format arguments");
    Check(Strings.GetEntry('M', 3, StringTables.GameMessages)!.Value2 == "导弹容量增加 10 枚"
        && Strings.GetScanEntry(13)!.Value2 == "导弹容量增加 10 枚。"
        && Strings.GetScanEntry(346)!.Value2 == "舰艇甲板的传送门已启动"
        && Strings.GetScanEntry(251)!.Value2 == "门外的微弱能量盾用于隔绝游荡的生物，任何武器都能打开这扇门。",
        "ROM popup and console styles differ from catalogue prose, and related scan clauses stay together");
    Check(Strings.GetScanEntry(54)!.Value2.Contains("这是我们留下的最后记录。我们即将离开这片死亡之地。")
        && Strings.GetEntry('W', 3, StringTables.HudMessagesMP)!.Value1 == "已与主机断开。\n\n按 A 返回菜单。"
        && LocalText.FormatId("replay.detail.refused", "refused.\n\n{0}", "版本不符") == "无法播放这份回放\n\n版本不符",
        "deliberate narrative breaks and dialog paragraphs remain intact");
    Check(Strings.GetEntry('B', 1, StringTables.MBBanner)!.Value1 == "密特罗德究极：猎人"
        && Strings.GetEntry('S', 21, StringTables.HudMessagesMP)!.Value1 == "究极猎人规则"
        && LocalText.Ui("METROID") == "密特罗德" && LocalText.Ui("PRIME") == "究极",
        "Metroid and Prime use the requested Chinese terminology");
    Check(Strings.GetEntry('M', 1, StringTables.GameMessages)!.Value1 == "猎人舰艇"
        && Strings.GetEntry('H', 4, StringTables.WeaponNames)!.Value1 == "赛拉克斯"
        && Strings.GetScanEntry(8)!.Value2.Contains("萨姆斯･亚兰"),
        "canonical terms occur in the real dialogue, weapons and scan-log tables");
    var fonts = new MenuFont(new UiTextureCache());
    Check(LocalText.Glyphs!.Index('･') != LocalText.Glyphs.Index('□')
        && fonts.Measure("萨姆斯･亚兰") == 5 * ExtraGlyphs.Advance + ExtraGlyphs.Advance / 2,
        "halfwidth middle dot has its own glyph and half-cell advance");
    Check(CheckSettingsCatalogue(), "settings and import display catalogues resolve");
    var imports = new ImportGames(Path.Combine(root, "imports"));
    var items = ImportGames.GameRows.Where(g => g.Id != ImportGames.TrilogyId)
        .SelectMany(g => imports.Game(g.Id)?.Items ?? throw new InvalidOperationException(g.Id)).ToArray();
    string[] unknown = items.Where(i => !ImportGames.ItemLabel(i).Any(c => c is >= '\u3400' and <= '\u9fff'))
        .Select(i => i.Id).ToArray();
    Check(items.Length > 0 && unknown.Length == 0, $"all {items.Length} public import cards localized: {string.Join(", ", unknown)}");
    Check(ImportGames.DisplayAsset("MP2VariaSuit", "P2 VAR") == "防护服"
        && ImportGames.DisplayAsset("custom-export", "CUSTOM") == "CUSTOM",
        "built-in costume choices use the import glossary, unknown exports keep their own label");
    Check(RecompSettings.MusicSources.All(x => AudioOptions.QualityDescription(x).Contains("即时生效")),
        "legacy audio quality uses the current choice's localized hint");
    Check(Strings.ReadStringTable(StringTables.ScanLog).All(e => HasHan(e.Value1) && HasHan(e.Value2)),
        "all real scan-log headings and bodies have Chinese text");
    Check(Strings.GetScanEntry(34)!.Value2.Contains("终极力量的秘密就藏在阿林比克星团")
        && Strings.GetScanEntry(453)!.Value2.Contains("神圣光谱")
        && Strings.GetScanEntry(460)!.Value2.Contains("红色印记"),
        "mission message and six-colour prophecy preserve the clues");
    Check(LocalText.FormatId("online.hub.hunters", "{0} hunter{1} online", 9, "s") == "9 名猎人在线"
        && LocalText.FormatId("import.run.progress", "importing {0}: {2}/{3}, {4}%", "fixture.iso", "防护服", 2, 3, 79)
            == "正在从「fixture.iso」导入。\n防护服\n第 2/3 项，79%",
        "localized runtime templates preserve counters and source names");
    var room = Strings.GetEntry('R', 103, StringTables.LocationNames)!;
    Check(room.Value1 == "Unit2_land" && room.Value2 == "天体档案馆入口", "display name changes, room key does not");
    int menuHits = Enumerable.Range(0, englishMenu.RomCount).Count(i => englishMenu[i] != MenuStringsForDisplay(i));
    string MenuStringsForDisplay(int i) => i == begin ? "开始游戏" : chineseMenu.Display(i);
    int tableHits = 0;
    foreach (var (table, before) in originals)
    {
        var after = Strings.ReadStringTable(table);
        if (before.Count != after.Count) throw new InvalidOperationException(table + ": wrong row count");
        for (int i = 0; i < before.Count; i++)
        {
            var a = before[i]; var b = after[i];
            if (a.Id != b.Id || a.Prefix != b.Prefix || a.Speed != b.Speed || a.Category != b.Category)
                throw new InvalidOperationException(table + ": metadata changed");
            if (a.Value1 != b.Value1 || a.Value2 != b.Value2) tableHits++;
        }
    }
    Console.WriteLine($"COVERAGE {key}: {menuHits}/{englishMenu.RomCount} menu indices, {tableHits}/{originals.Sum(x => x.Value.Count)} table entries");
    Check(tableHits >= 650 && menuHits >= 500, "catalogue binds to real AMHE base text and metadata");
    Check(LocalText.Glyphs!.Count > 1000 && LocalText.Glyphs.Index('萨') != LocalText.Glyphs.Index('□'), "glyph atlas supplies Chinese characters");
    Check(LocalText.WrapWide("天体档案馆", 16, s => s.Length * 8).SequenceEqual(new[] { "天体", "档案", "馆" }),
        "CJK wraps without spaces");
    RuntimeLocalization.Check(Paths.FileSystem, Check);
    DesktopLocale.Init("en");
    DesktopGame.Prepare(game);
    Check(ControlHints.ForSetting("maplook", "MAP", "", true) == ControlHints.All["maplook.map"]
        && ImportGames.DisplayTitle(ImportGames.Row("mp4")) == "PRIME 4: BEYOND"
        && ImportGames.DisplayAsset("MP2VariaSuit", "P2 VAR") == "P2 VAR"
        && AudioOptions.QualityDescription("hq") == AudioOptions.OriginalQualityDescription,
        "English mode restores the original generated settings and import labels");
    Check(MenuStrings.Load(Paths.FileSystem).Display(begin) == "BEGIN GAME" && Strings.GetScanEntry(1)!.Value1 == originalLog.Value1,
        "English mode bypasses the installed overlay and invalidates the string cache");
    RuntimeLocalization.CheckEnglish(Check);
    Console.WriteLine($"PASS: {passed} checks");
}
finally { Directory.SetCurrentDirectory(cwd); }
return 0;

static bool CheckSettingsCatalogue()
{
    string[] missing = ControlHints.All.Keys.Where(k => !HasHan(ControlHints.For(k))).ToArray();
    if (missing.Length > 0)
    {
        Console.Error.WriteLine("Missing localized hint IDs: " + string.Join(", ", missing));
        return false;
    }
    int binds = 0;
    foreach (bool keys in new[] { false, true })
        foreach (ControlBinds.Function f in ControlBinds.Functions(keys))
        {
            if (!HasHan(ControlBinds.DisplayHint(f, keys)) || !HasHan(ControlBinds.DisplayLabel(f)))
            {
                Console.Error.WriteLine($"Untranslated {f.Id} (keys={keys})");
                return false;
            }
            binds++;
        }
    string alternate = ControlHints.ForSetting("maplook", "DETAILED", "unused", true);
    string mapped = ControlHints.ForSetting("maplook", "MAP", "unused", true);
    bool ok = mapped == "暂停地图使用原版房间模型，在地图中按 Y 切换。"
        && alternate.Contains("实际形状")
        && ControlHints.ForSetting("importgames", "OPEN", "unused", true).Contains("高清装甲")
        && ControlHints.DisplayValue("map\nlook", "MAP") == "原版模型"
        && ControlBinds.DisplayButton("Mouse.Right+LeftShift") == "鼠标右键+左 Shift"
        && ImportGames.DisplayTitle(ImportGames.Row("mp2")) == "密特罗德 究极2 黑暗回声"
        && ImportGames.DisplayTitle(ImportGames.Row("mp3")) == "密特罗德 究极3 腐蚀"
        && ImportGames.DisplayTitle(ImportGames.Row("mp4")) == "密特罗德 究极4 穿越未知"
        && ImportGames.GameRows.All(g => HasHan(ImportGames.DisplayTitle(g)));
    Console.WriteLine(ok ? $"SETTINGS: {ControlHints.All.Count} hints, {binds} device/action pairs, 5 import titles localized."
        : "FAIL: a runtime setting value or canonical import title did not resolve.");
    return ok;
}

static bool HasHan(string text) => text.Any(c => c is >= '\u3400' and <= '\u9fff');
