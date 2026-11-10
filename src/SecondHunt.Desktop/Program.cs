using MphRecomp.App.Platform;
using MphRecomp.Config;
using MphRecomp.Update;
using MphRead.Text;

namespace SecondHunt.Desktop;

internal static class Program
{
    [STAThread] // the ROM file picker
    static int Main(string[] args)
    {
        // every text format is invariant, like the PC tools and the Android app (10-16: Finnish -1 = "−1" crash)
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == DesktopStorage.ProvisionArg)
            return DesktopStorage.Provision(args);
        AppOptions options = AppOptions.Parse(args);
#if MPH_PUBLIC
        options.Launch = null; // a public build starts at the menus only (the launch shortcuts are for dev builds)
#endif
        try
        {
            DesktopLocale.Init(options.Locale);
            if (!DesktopPaths.Init(options.Data, options.Migrate)) return 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or System.Text.Json.JsonException)
        {
            System.Windows.Forms.MessageBox.Show(LocalText.Format("Could not prepare Second Hunt:\n\n{0}", ex.Message),
                BuildFlags.AppName, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            return 2;
        }
        DesktopLog.Init(DesktopPaths.Logs);
        DesktopCrashLog.Install();
        RecompSettings.PublicBuild = BuildFlags.Public;
        RecompSettings.DesktopHost = true;
        MphRecomp.Frontend.RecompMenus.PadConnected = () => DesktopInput.PadSeen;
        RecompSettings.AppId = BuildFlags.AppName;
        // RECOMP SETTINGS > UPDATES (Core Update/): this build's version and its installer; a dev build reads a test
        // server's base URL from update_feed.txt in its data folder
        UpdateHost.AppVersion = BuildFlags.Version;
        UpdateHost.Platform = UpdatePlatform.Windows;
        // The upstream release feed ships a different build. Never replace this fork with it.
        UpdateHost.Enabled = false;
        UpdateHost.OpenUrl = url =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex) { Log.Warn("MPHUpdate", $"couldn't open {url}: {ex.Message}"); }
        };
        // this app draws and plays everything itself: MphRead's own window, sound device and OpenAL stay closed
        MphRead.Scene.Headless = true;
        Log.Info("MPHDesktop", $"data folder: {DesktopPaths.Root}");
        try
        {
            DesktopPlatform.Install();
            using (var app = new DesktopApp(options))
            {
                DesktopImport.Install(app.Defer); // IMPORT GAMES' file dialog (between frames, on this STA thread)
                app.Run();
            }
            DesktopPlatform.Shutdown();
            return 0;
        }
        catch (Exception ex)
        {
            string? report = DesktopCrashLog.Write(ex, "unhandled (main loop)");
            Log.Error("MPHDesktop", ex.ToString());
            System.Windows.Forms.MessageBox.Show(
                LocalText.Format("{0} closed because of an error:\n\n{1}\n\n", BuildFlags.AppName, ex.Message)
                + (report != null ? LocalText.Format("A crash report was saved to\n{0}", report)
                    : LocalText.Ui("No crash report could be saved.")),
                BuildFlags.AppName, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            return 1;
        }
    }
}
