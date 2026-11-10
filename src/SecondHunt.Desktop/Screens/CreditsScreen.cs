using System.Drawing;
using LocalText = MphRead.Text.LocalText;
using SecondHunt.Desktop.Ui;

namespace SecondHunt.Desktop.Screens;

// RECOMP SETTINGS > CREDITS (as the Android app's CreditsActivity): the app's version, what it's built on, and every
// third-party notice the app must carry (THIRD_PARTY_NOTICES.md, embedded at build time). Scrolls with the wheel, the
// arrow keys, Page Up / Down, the D-pad or the stick; Esc / B closes it.
internal sealed class CreditsScreen : PageScreen
{
    readonly string[] _lines;
    readonly Action _close;
    string[]? _visualLines;
    int _layoutWidth;
    float _layoutScale;
    float _top; // first line shown

    // doc: another embedded text instead (OnlineRules: "online.md", docs/ONLINE_RULES_IN_APP.md)
    public CreditsScreen(Action close, string? doc = null)
    {
        _close = close;
        _lines = Document(doc).Replace("\r\n", "\n").Split('\n');
    }

    internal static string Document(string? doc = null)
    {
        if (doc == "online.md" && DesktopLocale.Name == "zh-CN") return Notices("locales.zh-CN.online.md");
        if (doc != null) return Notices(doc);
        string heading = LocalText.FormatId("credits.heading", "{0} {1} for Windows", BuildFlags.AppName, BuildFlags.Version);
        string intro = LocalText.Id("credits.intro",
            "A fan-made recompilation project built on MphRead by NoneGiven (MIT licence).\n"
            + "Metroid Prime Hunters is (c) Nintendo. This app contains no game code or data: it runs from the ROM\n"
            + "you provide. It is not affiliated with or endorsed by Nintendo.");
        string noticeHeading = LocalText.Id("credits.notices", "");
        return heading + "\n\n" + intro + "\n\n"
            + "----------------------------------------------------------------------------------------------\n\n"
            + (noticeHeading.Length > 0 ? noticeHeading + "\n\n" : "") + Notices();
    }

    static string Notices(string name = "notices.md")
    {
        using Stream? s = typeof(CreditsScreen).Assembly.GetManifestResourceStream(name);
        if (s == null) return LocalText.Id("credits.missing", "(notices missing from this build)");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    int LinesShown => Math.Max(1, (int)((Height - 64 * S) / LineHeight));
    int LineCount => (_visualLines ?? _lines).Length;
    float LineHeight => 19 * S;

    void Scroll(float lines)
    {
        float max = Math.Max(0, LineCount - LinesShown);
        float top = Math.Clamp(_top + lines, 0, max);
        if (top != _top)
        {
            _top = top;
            Invalidate();
        }
    }

    public override void Move(int dx, int dy) => Scroll(dy * 3);
    public override void Wheel(float steps) => Scroll(-steps * 3);
    public override void Back() => _close();
    public override void Press() { }

    public override void Key(OpenTK.Windowing.GraphicsLibraryFramework.Keys key)
    {
        switch (key)
        {
        case OpenTK.Windowing.GraphicsLibraryFramework.Keys.PageDown: Scroll(LinesShown - 2); break;
        case OpenTK.Windowing.GraphicsLibraryFramework.Keys.PageUp: Scroll(-(LinesShown - 2)); break;
        case OpenTK.Windowing.GraphicsLibraryFramework.Keys.Home: Scroll(-LineCount); break;
        case OpenTK.Windowing.GraphicsLibraryFramework.Keys.End: Scroll(LineCount); break;
        }
    }

    // the right stick / held keys scroll smoothly
    public void ScrollBy(float lines) => Scroll(lines);

    protected override void Paint(Graphics g)
    {
        float s = S, x = 48 * s, y = 32 * s;
        using Font mono = Font(13, mono: true);
        using var brush = new SolidBrush(Color.FromArgb(0xD8, 0xE8, 0xF0));
        int width = Math.Max(1, (int)(Width - 2 * x - 8 * s));
        if (_visualLines == null || _layoutWidth != width || _layoutScale != s)
        {
            _visualLines = _lines.SelectMany(line => LocalText.WrapWide(line, width,
                part => g.MeasureString(part, mono).Width)).ToArray();
            _layoutWidth = width;
            _layoutScale = s;
            _top = Math.Clamp(_top, 0, Math.Max(0, LineCount - LinesShown));
        }
        int first = (int)_top;
        for (int i = first; i < Math.Min(first + LinesShown, _visualLines.Length); i++)
        {
            g.DrawString(_visualLines[i], mono, brush, x, y);
            y += LineHeight;
        }
        using Font hint = Font(12);
        DrawText(g, "Wheel / arrows / Page Up, Page Down: scroll    Esc: back", hint, Muted,
            new RectangleF(x, Height - 26 * s, Width - 96 * s, 22 * s), StringAlignment.Far);
    }
}
