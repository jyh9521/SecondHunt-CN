namespace SecondHunt.Desktop;

// Where the Windows app keeps things. Nothing of the game ships with it: the player's ROM is unpacked once into Game.
//   data\ beside SecondHunt.exe by default; --data <dir> is an explicit override.
//     recomp_settings.json, saves\, mods\
//     game\        the unpacked ROM (MphRead's working directory: files\, .extracted)
//     cache\       GL program binaries
//     crash\, logs\
internal static class DesktopPaths
{
    public static string Root { get; private set; } = "";
    public static string Game => Path.Combine(Root, "game");
    public static string Cache => Path.Combine(Root, "cache");
    public static string Saves => Path.Combine(Root, "saves");
    public static string Mods => Path.Combine(Root, "mods");
    public static string Crash => Path.Combine(Root, "crash");
    public static string Logs => Path.Combine(Root, "logs");
    public static string Settings => Path.Combine(Root, "recomp_settings.json");
    public static string Extracted => Path.Combine(Game, ".extracted");

    public static bool Init(string? dataArg, bool migrate = true)
    {
        Root = dataArg != null ? Path.GetFullPath(dataArg) : DesktopStorage.DefaultRoot(AppContext.BaseDirectory);
        if (!DesktopStorage.Prepare(Root, isDefault: dataArg == null)) return false;
        if (migrate && dataArg == null)
        {
            string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Second Hunt");
            DesktopStorage.Migrate(legacy, Root);
        }
        foreach (string dir in new[] { Game, Cache, Saves, Mods, Crash, Logs })
        {
            DesktopStorage.RequireOrdinaryDirectory(dir);
            Directory.CreateDirectory(dir);
        }
        return true;
    }
}

internal static class BuildFlags
{
#if MPH_PUBLIC
    public const bool Public = true;
#else
    public const bool Public = false;
#endif

    public static string AppName => Public ? "Second Hunt" : "MPH Recomp";

    public static string Version =>
        typeof(BuildFlags).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            is [System.Reflection.AssemblyInformationalVersionAttribute v, ..] ? v.InformationalVersion : "dev";
}
