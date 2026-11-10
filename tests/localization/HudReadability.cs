using System.Reflection;
using MphRead;
using MphRead.Entities;
using MphRead.Formats;
using MphRead.Text;
using MphRecomp.App;
using MphRecomp.App.Gl;
using MphRecomp.Campaign;
using OpenTK.Mathematics;
using SecondHunt.Desktop;

internal static class HudReadability
{
    const string Room = "UNIT2_C0";
    const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    // The reported scan is L251. Drive the actual scanner, then inspect the same draw packets that the Windows GL host consumes.
    public static int Run(string game, string? shot, string locale)
    {
        game = Path.GetFullPath(game);
        if (shot != null) shot = Path.GetFullPath(shot);
        DesktopLocale.Init(locale);
        if (!DesktopGame.Prepare(game)) throw new InvalidOperationException("Expected an extracted USA game fixture.");
        Directory.SetCurrentDirectory(game);
        Paths.UpdatePaths();
        Paths.ChooseMphPath();
        using CampaignHost host = CampaignHost.Start(Room, collectDrawItems: true, viewWidth: 1280, viewHeight: 720, setupSave: s =>
        {
            s.Weapons = 0xFF;
            s.Health = s.HealthMax = 799;
        });
        void CloseDialogs()
        {
            for (int i = 0; i < 240 && host.DialogPaused; i++)
                host.Step(new CampaignInput { DialogButton = i % 8 == 0 ? HostDialogButton.Advance : HostDialogButton.None });
        }
        for (int i = 0; i < 100; i++) host.Step(default);
        CloseDialogs();
        var doors = new List<EntityBase>();
        foreach (EntityBase e in host.Scene.Entities)
            if (e is DoorEntity && e.GetScanId() == 251) doors.Add(e);
        if (doors.Count == 0) throw new InvalidOperationException("No L251 door in the test room.");
        var scanningEntity = typeof(PlayerEntity).GetField("_scanningEntity", PrivateInstance)!;
        foreach (EntityBase door in doors)
        {
            door.GetPosition(out Vector3 at);
            if (!PlaceNear(host, at)) continue;
            CloseDialogs();
            if (!host.Player.ScanVisor)
            {
                for (int i = 0; i < 20; i++) host.Step(new CampaignInput { Buttons = i < 2 ? CampaignButtons.ScanVisor : CampaignButtons.None });
            }
            for (int i = 0; i < 200 && !host.DialogPaused; i++)
                host.Step(new CampaignInput { Buttons = CampaignButtons.Scan, AimDelta = AimAt(host.Player, at) });
            if (host.Dialog != DialogType.Scan || scanningEntity.GetValue(host.Player) != door) { CloseDialogs(); continue; }
            for (int i = 0; i < 12; i++) host.Step(default);
            var entry = Strings.GetScanEntry(251)!;
            Console.WriteLine($"SCAN: {Paths.MphKey} room={host.RoomId} door={door.Id} entry=L251 dialog={host.Dialog} locale={DesktopLocale.Name}");
            Console.WriteLine("TEXT: " + entry.Value2);
            if (shot != null) Capture(host, game, shot);
            if (DesktopLocale.Name == "en")
            {
                if (LocalText.Glyphs != null || !string.Equals(entry.Value1, "Door", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("English should use the original scan and font.");
                Console.WriteLine("PASS: English uses the original scan and font.");
                return 0;
            }
            return Assess(host.Scene, entry.Value2.Count(c => c > 255));
        }
        throw new InvalidOperationException("Could not open the real door scan dialog.");
    }

    // Use the real Windows WGL context and campaign render pass, without a debug hook in the retail app.
    static void Capture(CampaignHost host, string game, string path)
    {
        using var window = new GlWindow("HUD readability test", null);
        IntPtr context = window.CreateContext();
        try
        {
            int width = window.Window.FramebufferSize.X, height = window.Window.FramebufferSize.Y;
            var app = typeof(GLES30).Assembly;
            var rendererType = app.GetType("MphRecomp.App.CampaignRenderer", throwOnError: true)!;
            var layoutType = app.GetType("MphRecomp.App.ControlLayout", throwOnError: true)!;
            var renderer = (IGlScreen)Activator.CreateInstance(rendererType,
                game, Room, Enum.Parse(layoutType, "Prime"), Hunter.Samus, null, null, null)!;
            rendererType.GetField("_host", PrivateInstance)!.SetValue(renderer, host);
            renderer.OnSurfaceCreated();
            renderer.OnSurfaceChanged(width, height);
            host.Step(default);
            rendererType.GetMethod("Render", PrivateInstance)!.Invoke(renderer, null);
            var rgba = new byte[width * height * 4];
            GLES30.GlReadPixels(0, 0, width, height, GLES30.GlRgba, GLES30.GlUnsignedByte, rgba);
            int glError = GLES30.GlGetError();
            if (glError != 0) throw new InvalidOperationException($"HUD render returned GL error 0x{glError:X}.");
            var flipped = new byte[rgba.Length];
            for (int y = 0; y < height; y++) Buffer.BlockCopy(rgba, (height - y - 1) * width * 4, flipped, y * width * 4, width * 4);
            if (!DesktopPlatform.EncodePng(path, flipped, width, height)) throw new IOException("HUD screenshot could not be saved.");
            Console.WriteLine($"WGL: {width}x{height}, {window.Renderer}");
            Console.WriteLine($"PNG: {path}");
        }
        finally { window.DeleteContext(context); }
    }

    static bool PlaceNear(CampaignHost host, Vector3 at)
    {
        foreach (float rise in new[] { 0f, -2f, -4f, 2f })
            foreach (float distance in new[] { 3f, 5f, 8f })
                for (int a = 0; a < 12; a++)
                {
                    float angle = a * MathF.PI / 6;
                    var dir = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
                    host.PlacePlayer(at + dir * distance + new Vector3(0, rise, 0), -dir);
                    for (int i = 0; i < 24; i++) host.Step(new CampaignInput { AimDelta = AimAt(host.Player, at) });
                    CollisionResult hit = default;
                    if (host.Player.Health > 0 && (host.Player.CameraInfo.Position - at).Length < 20
                        && !CollisionDetection.CheckBetweenPoints(host.Player.CameraInfo.Position, at, TestFlags.Scan, host.Scene, ref hit))
                        return true;
                }
        return false;
    }

    static System.Numerics.Vector2 AimAt(PlayerEntity player, Vector3 at)
    {
        Vector3 delta = (at - player.CameraInfo.Position).Normalized();
        Vector3 facing = player.CameraInfo.Facing.Normalized();
        Vector3 right = Vector3.Cross(facing, Vector3.UnitY).Normalized();
        Vector3 flat = new Vector3(facing.X, 0, facing.Z).Normalized();
        float yaw = MathF.Atan2(Vector3.Dot(delta, right), Vector3.Dot(new Vector3(delta.X, 0, delta.Z), flat)) * 180 / MathF.PI;
        float pitch = (MathF.Asin(Math.Clamp(delta.Y, -1, 1)) - MathF.Asin(Math.Clamp(facing.Y, -1, 1))) * 180 / MathF.PI;
        return new System.Numerics.Vector2(Math.Clamp(yaw * 2, -40, 40), Math.Clamp(-pitch * 2, -40, 40));
    }

    static int Assess(Scene scene, int bodyGlyphs)
    {
        var rows = new (string Name, float Top, float Bottom, int Count, ColorRgba Panel, List<Scene.HostHudSprite> Glyphs)[]
        {
            ("energy", 16, 26, 2, new ColorRgba(71, 80, 65, 255), new()),
            ("status", 56, 65, 4, new ColorRgba(71, 80, 65, 255), new()),
            ("title", 115, 125, 1, new ColorRgba(48, 73, 81, 255), new()),
            ("body", 133, 163, bodyGlyphs, new ColorRgba(71, 80, 65, 255), new()),
            ("confirm", 174, 185, 2, new ColorRgba(57, 57, 57, 255), new())
        };
        for (int i = 0; i < scene.HudItemCount; i++)
        {
            var item = scene.GetHudItem(i);
            float y = (1 - item.Top) * 96;
            if (item.SpriteIndex < 0 || item.Right - item.Left > 0.12f) continue;
            var sprite = scene.HudSprites[item.SpriteIndex];
            if (sprite.Width != sprite.Height || sprite.Width is not (16 or 32)) continue;
            foreach (var row in rows)
                if (y >= row.Top && y <= row.Bottom) row.Glyphs.Add(sprite);
        }
        static double Linear(byte c)
        {
            double x = c / 255d;
            return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }
        static double Luma(ColorRgba c) => 0.2126 * Linear(c.Red) + 0.7152 * Linear(c.Green) + 0.0722 * Linear(c.Blue);
        bool passed = true;
        foreach (var row in rows)
        {
            if (row.Glyphs.Count != row.Count)
                throw new InvalidOperationException($"Expected {row.Count} {row.Name} glyphs; recorded {row.Glyphs.Count}.");
            // Compare the actual draw ink with a reference swatch of the panel/surround.
            double panel = Luma(row.Panel);
            var ink = row.Glyphs.Select(t => t.Pixels.Where(c => c.Alpha >= 200).OrderByDescending(Luma).FirstOrDefault()).ToArray();
            double ratio = ink.Select(Luma).Min(c => (Math.Max(c, panel) + 0.05) / (Math.Min(c, panel) + 0.05));
            bool coverage = row.Glyphs.All(t => t.Pixels.Any(c => c.Alpha is > 0 and < 255)
                && t.Pixels.Any(c => c.Alpha == 0));
            int size = row.Glyphs[0].Width;
            Console.WriteLine($"HUD {row.Name}: {row.Count} glyphs, {size}px raster, ink #{ink[0].Red:X2}{ink[0].Green:X2}{ink[0].Blue:X2}, "
                + $"minimum contrast={ratio:0.00}:1, antialiased={coverage}");
            passed &= ratio >= 4.5 && coverage && size >= 32;
        }
        if (!passed)
        {
            Console.Error.WriteLine("FAIL: localized HUD glyphs lack contrast, coverage or raster detail.");
            return 1;
        }
        Console.WriteLine("PASS: energy, scan status, title, body and confirm glyphs have bright ink and continuous edge coverage.");
        return 0;
    }
}
