using System.Net;
using MphRead;
using MphRead.Text;
using MphRecomp.Campaign;
using MphRecomp.Frontend;
using MphRecomp.Multiplayer;
using MphRecomp.Multiplayer.Net;
using MphRecomp.Online;
using MphRecomp.Replay;
using SecondHunt.Desktop.Screens;

internal static class RuntimeLocalization
{
    private static bool Han(string s) => s.Any(c => c is >= '\u3400' and <= '\u9fff');

    public static void Check(string rom, Action<bool, string> check)
    {
        var rooms = Metadata.RoomList.Where(r => !r.FirstHunt && (!string.IsNullOrEmpty(r.InGameName)
            || r.Id is >= 89 and <= 92)).ToArray();
        string[] misses = rooms.Where(r => !Han(DisplayNames.Room(r))).Select(r => r.Name).ToArray();
        check(rooms.Length >= 100 && misses.Length == 0, $"all {rooms.Length} named retail room labels: {string.Join(", ", misses)}");
        var arenas = MatchArenas.All.Where(r => r.Id is >= MatchArenas.FirstId and <= MatchArenas.LastId).ToArray();
        check(arenas.Length == 26 && arenas.All(r => Han(DisplayNames.Arena(r.Name)) && MatchArenas.Find(r.Name)?.Id == r.Id)
            && DisplayNames.Arena("MP6 HEADSHOT") == "爆头场" && DisplayNames.Arena("Outer Reach") == "外缘",
            "all 26 retail arena labels use the ROM glossary without altering lookup keys");
        check(MatchSettings.Modes.All(m => Han(DisplayNames.Mode(m))) && DisplayNames.Hunter(Hunter.Sylux) == "赛拉克斯"
            && LocalText.MatchNickname("KANBOT", Hunter.Kanden, true) == "坎登（电脑）"
            && LocalText.MatchNickname("KANBOT", Hunter.Kanden, false) == "KANBOT"
            && LocalText.MatchNickname("Player1", Hunter.Samus, false, 1) == "玩家 1"
            && LocalText.MatchNickname("Player1", Hunter.Samus, false) == "Player1"
            && LocalText.MatchNickname("CustomName", Hunter.Kanden, true) == "CustomName", "mode, hunter and bot names localize without altering player names");

        var map = new PauseMap(rom, new UiTextureCache());
        RoomMetadata room = Metadata.GetRoomByName("UNIT2_RM1").Item1!;
        map.Open(new PauseMap.Info { Room = room.Name, Title = DisplayNames.Room(room), Visited = id => id == room.Id });
        for (int i = 0; i < 4; i++) map.Tick(new PauseMap.Input());
        check(map.VisibleTitle == "数", "pause-map typewriter starts with Chinese, not an English prefix");
        for (int i = 0; i < 36; i++) map.Tick(new PauseMap.Input());
        map.Build(1280, 720);
        check(map.VisibleTitle == "数据圣殿 01" && DisplayNames.MapPart(new AreaMapModel.Part
            { IsConnector = true, Name = "Con07", ConnectorNumber = 7 }) == "通道 07", "full map title and connector labels localize");
        map.Close();

        var host = new LanHostInfo { Arena = "MP3 PROVING GROUND", Name = "MyHost", Mode = GameMode.Battle,
            RomKey = "MPH USA", Address = new IPEndPoint(IPAddress.Loopback, 14500) };
        check(host.DisplayLabel == "MyHost：对战 · 战斗大厅（127.0.0.1，ROM MPH USA）"
            && host.Label == "MyHost: Battle in Combat Hall (127.0.0.1, ROM MPH USA)", "LAN labels separate translated fields and raw diagnostics");
        var quick = new ChatLog.Line(1, "fixture", "user-code", "MyHost", 0, "", DateTime.UnixEpoch);
        check(quick.Shown == "打得漂亮！" && quick.Preset == 0 && ChatPresets.Text(0) == ChatPresets.All[0]
            && Enumerable.Range(0, ChatPresets.All.Length).All(i => Han(ChatPresets.Display(i)))
            && (quick with { Preset = -1, Text = "TypedText" }).Shown == "TypedText", "quick-chat presets retain indices; user text remains verbatim");
        var oldReplay = new ReplayFile { SimVersion = 0 };
        check(ReplayPlayer.CheckBeforeStart(oldReplay, display: true)?.Contains("游戏逻辑版本") == true
            && ReplayPlayer.CheckBeforeStart(oldReplay)?.StartsWith("recorded with game logic version 0") == true,
            "replay refusals retain raw diagnostic reason and show Chinese to the player");
        check(CreditsScreen.Document().Contains("第三方许可原文") && CreditsScreen.Document("online.md").Contains("行为准则")
            && CreditsScreen.Document("online.md").Contains("陌生人的语音默认关闭"), "Windows credits and online rules embed the Chinese display copy");

        MenuFile menu = MenuFile.Load(Path.Combine(rom, "frontend", "metroidhunters.bin"));
        var ending = new GameEnding();
        check(ending.Bind(menu), "both ending result pages bind to the real ROM menu");
        var s = MenuStrings.Load(rom);
        foreach ((bool good, int page) in new[] { (true, GameEnding.GoodPage), (false, GameEnding.BadPage) })
        {
            ending.Current = new EndingResults(good, 87, 3712, 98, 2, 7);
            ending.Fill(s);
            var text = menu.Pages[page].Items.Where(i => i.IsText)
                .Select(i => s.Display(i.States[0].Text!.StringId)).ToArray();
            check(text.Contains("1 小时 01 分") && text.Contains("87%") && text.Contains("2") && text.Contains("7")
                && (good ? text.Contains("1 分 38 秒") : text.Any(t => t.Contains("未完成"))),
                $"{(good ? "good" : "bad")} ending fills Chinese results with unchanged numeric values");
        }
    }

    public static void CheckEnglish(Action<bool, string> check)
    {
        check(DisplayNames.Arena("MP3 PROVING GROUND") == "Combat Hall"
            && ChatPresets.Display(0) == ChatPresets.All[0] && LocalText.MatchNickname("KANBOT", Hunter.Kanden, true) == "KANBOT"
            && CreditsScreen.Document("online.md").StartsWith("ONLINE PLAY: AGES 13 AND UP"),
            "English fallback restores map, bot, chat and policy labels without changing game state");
    }
}
