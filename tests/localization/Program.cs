using System.Security.Cryptography;
using System.Text;
using MphRead;
using MphRead.Text;
using MphRecomp.Frontend;
using SecondHunt.Desktop;
using AccessAction = SecondHunt.Desktop.DesktopStorage.AccessAction;

Console.OutputEncoding = Encoding.UTF8;
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
    Check(log.Value1 == "扫描目镜" && log.String1 == "扫描目镜"
        && log.Speed == originalLog.Speed && log.Category == originalLog.Category,
        "scan log renders Unicode and preserves metadata");
    var msg = Strings.GetEntry('M', 5, StringTables.GameMessages)!;
    Check(msg.Value1.Contains("&tab0") && msg.Value2.Contains("&tab0") && msg.Value2.Contains("&tab1"),
        "campaign substitution tokens preserved");
    Check(Strings.GetEntry('B', 1, StringTables.MBBanner)!.Value1 == "密特罗德究极：猎人"
        && Strings.GetEntry('S', 21, StringTables.HudMessagesMP)!.Value1 == "究极猎人规则"
        && LocalText.Ui("METROID") == "密特罗德" && LocalText.Ui("PRIME") == "究极",
        "Metroid and Prime use the requested Chinese terminology");
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
    DesktopLocale.Init("en");
    DesktopGame.Prepare(game);
    Check(MenuStrings.Load(Paths.FileSystem).Display(begin) == "BEGIN GAME" && Strings.GetScanEntry(1)!.Value1 == originalLog.Value1,
        "English mode bypasses the installed overlay and invalidates the string cache");
    Console.WriteLine($"PASS: {passed} checks");
}
finally { Directory.SetCurrentDirectory(cwd); }
return 0;
