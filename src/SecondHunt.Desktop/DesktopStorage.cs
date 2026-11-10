using System.ComponentModel;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows.Forms;
using MphRead.Text;

namespace SecondHunt.Desktop;

// Keep the game asInvoker. Only creating a fresh data directory under a
// protected install needs elevation; the helper never opens the ROM or game.
internal static class DesktopStorage
{
    internal const string ProvisionArg = "--provision-portable-data";
    private const int Cancelled = 1223;
    private static readonly string[] ProgramFilesRoots =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
    ];

    public static string DefaultRoot(string install) => Path.Combine(install, "data");

    internal static bool IsUnder(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return false;
        string p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string r = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return p.Equals(r, StringComparison.OrdinalIgnoreCase)
            || p.StartsWith(Path.EndsInDirectorySeparator(r) ? r : r + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsProtectedInstall(string install)
        => ProgramFilesRoots.Any(root => IsUnder(install, root)
            && !Path.TrimEndingDirectorySeparator(Path.GetFullPath(install))
                .Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase));

    public static void RequireOrdinaryEntry(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Reparse points are not supported here: " + path);
    }

    public static void RequireOrdinaryDirectory(string path)
    {
        for (DirectoryInfo? dir = new(path); dir != null; dir = dir.Parent)
            if (dir.Exists) RequireOrdinaryEntry(dir.FullName);
    }

    // Invoked before any settings, DLL P/Invoke, updater, extractor or GL setup.
    public static int Provision(string[] args)
    {
        if (args.Length != 2) return 2;
        try
        {
            string install = AppContext.BaseDirectory;
            if (!IsProtectedInstall(install) || !IsLauncher(Environment.ProcessPath, install)) return 2;
            RequireOrdinaryDirectory(install);
            RequireOrdinaryEntry(Environment.ProcessPath!);
            using var identity = WindowsIdentity.GetCurrent();
            if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return 5;
            var user = new SecurityIdentifier(args[1]);
            if (!user.IsAccountSid()) return 2;
            string data = DefaultRoot(install);
            if (Directory.Exists(data) || File.Exists(data)) return 3; // never re-ACL existing data
            var acl = new DirectorySecurity();
            acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            Grant(acl, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl);
            Grant(acl, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl);
            Grant(acl, user, FileSystemRights.Modify);
            new DirectoryInfo(data).Create(acl);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or System.Security.SecurityException or InvalidOperationException)
        {
            return 5;
        }
    }

    private static bool IsLauncher(string? exe, string install)
        => exe != null && Path.GetFileName(exe).Equals("SecondHunt.exe", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetDirectoryName(exe), Path.TrimEndingDirectorySeparator(install), StringComparison.OrdinalIgnoreCase);

    private static void Grant(DirectorySecurity acl, SecurityIdentifier sid, FileSystemRights rights)
        => acl.AddAccessRule(new FileSystemAccessRule(sid, rights,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));

    internal enum AccessAction { Ready, RequestConsent, Stop }

    internal static AccessAction Decide(bool writable, bool defaultPath, bool protectedInstall, bool dataExists)
        => writable ? AccessAction.Ready : defaultPath && protectedInstall && !dataExists
            ? AccessAction.RequestConsent : AccessAction.Stop;

    public static bool Prepare(string root, bool isDefault)
    {
        bool writable = Probe(root, out string detail);
        switch (Decide(writable, isDefault, IsProtectedInstall(AppContext.BaseDirectory),
            Directory.Exists(root) || File.Exists(root)))
        {
        case AccessAction.Ready:
            return true;
        case AccessAction.RequestConsent:
            RequireOrdinaryDirectory(AppContext.BaseDirectory);
            string? exe = Environment.ProcessPath;
            if (!IsLauncher(exe, AppContext.BaseDirectory)) return false;
            RequireOrdinaryEntry(exe!);
            if (MessageBox.Show(LocalText.Format(
                    "Second Hunt is installed in Program Files and cannot create this data folder:\n{0}\n\nAllow Windows to ask for administrator approval once? Only the data folder will be created. The game itself will keep running normally.",
                    root), BuildFlags.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) return false;
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var start = new ProcessStartInfo(exe!)
                {
                    WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = true, Verb = "runas"
                };
                start.ArgumentList.Add(ProvisionArg);
                start.ArgumentList.Add(identity.User!.Value);
                using Process? helper = Process.Start(start);
                helper?.WaitForExit();
                if (helper?.ExitCode == 0 && Probe(root, out detail)) return true;
                detail = LocalText.Format("Windows setup did not create a writable directory (exit code {0}).", helper?.ExitCode ?? -1);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == Cancelled)
            {
                detail = LocalText.Ui("Administrator approval was cancelled.");
            }
            catch (Win32Exception ex) { detail = ex.Message; }
            break;
        }
        MessageBox.Show(LocalText.Format(
            "The data folder is not writable:\n{0}\n\n{1}\n\nMove Second Hunt to a folder you can write to, or check the data folder permissions.",
            root, detail), BuildFlags.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private static bool Probe(string root, out string detail)
    {
        try
        {
            RequireOrdinaryDirectory(root);
            Directory.CreateDirectory(root);
            string probe = Path.Combine(root, ".write-test-" + Guid.NewGuid().ToString("N"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
            detail = "";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            detail = ex.Message;
            return false;
        }
    }

    // Keep the previous install as a backup. Copy only when the corresponding
    // destination is absent; never merge into or replace an existing save or ROM tree.
    public static void Migrate(string from, string to)
    {
        if (!Directory.Exists(from) || IsUnder(to, from) || IsUnder(from, to)) return;
        RequireOrdinaryDirectory(from);
        RequireOrdinaryDirectory(to);
        foreach (string name in new[] { "recomp_settings.json", "window.json", "lobby.json", "movies_seen.json" })
        {
            string source = Path.Combine(from, name), target = Path.Combine(to, name);
            if (File.Exists(source) && !File.Exists(target))
            {
                RequireOrdinaryEntry(source);
                File.Copy(source, target, overwrite: false);
            }
        }
        foreach (string name in new[] { "game", "saves", "mods", "hd", "replays" })
        {
            string source = Path.Combine(from, name), target = Path.Combine(to, name);
            if (Directory.Exists(target) || !Directory.Exists(source)
                || (name == "game" && !DesktopGame.HasExtraction(source))) continue;
            string staging = target + ".migration-" + Guid.NewGuid().ToString("N");
            try
            {
                CopyTree(source, staging);
                Directory.Move(staging, target);
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
        }
    }

    private static void CopyTree(string from, string to)
    {
        RequireOrdinaryEntry(from);
        Directory.CreateDirectory(to);
        foreach (string file in Directory.EnumerateFiles(from))
        {
            RequireOrdinaryEntry(file);
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: false);
        }
        foreach (string child in Directory.EnumerateDirectories(from))
            CopyTree(child, Path.Combine(to, Path.GetFileName(child)));
    }
}
