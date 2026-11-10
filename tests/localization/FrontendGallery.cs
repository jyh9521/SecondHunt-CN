using System.Reflection;
using MphRecomp.App;
using MphRecomp.App.Gl;
using MphRecomp.Frontend;
using MphRecomp.Media;
using MphRead;
using SecondHunt.Desktop;

internal static class FrontendGallery
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    // This drives the real WGL front end. A real Windows click posts down and up
    // on different frames; a single scripted tap does not reproduce the bug.
    public static int Run(string game, string output, string scenario = "gallery")
    {
        game = Path.GetFullPath(game);
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        string profile = Path.Combine(output, "profile");
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(profile, MovieGallery.FileName), """{"Seen":[0]}""");
        DesktopLocale.Init("zh-CN");
        if (!DesktopGame.Prepare(game)) throw new InvalidOperationException("Expected an AMHE0 fixture.");
        using var window = new GlWindow("Movie gallery test", null);
        IntPtr context = window.CreateContext();
        var type = typeof(IGlScreen).Assembly.GetType("MphRecomp.App.FrontendRenderer", throwOnError: true)!;
        var screen = (IGlScreen)Activator.CreateInstance(type, game, Path.Combine(profile, "recomp_settings.json"),
            Path.Combine(profile, "mods"), Path.Combine(profile, "saves"))!;
        MoviePlayer? Movie() => (MoviePlayer?)type.GetField("_movie", Fields)!.GetValue(screen);
        try
        {
            int w = window.Window.FramebufferSize.X, h = window.Window.FramebufferSize.Y;
            type.GetProperty("StartPage")!.SetValue(screen, MovieGallery.FirstPage);
            screen.OnSurfaceCreated();
            screen.OnSurfaceChanged(w, h);
            var session = (FrontendSession?)type.GetField("_session", Fields)!.GetValue(screen)
                ?? throw new InvalidOperationException("Front end did not load.");
            void Draw()
            {
                Thread.Sleep(17);
                screen.OnDrawFrame();
            }
            if (scenario is "rules" or "protocol")
            {
                if (scenario == "rules") UiInventory.OpenRules(session);
                else UiInventory.OpenProtocolError(session);
                for (int i = 0; i < 48; i++) Draw();
                string shot = Path.Combine(output, scenario + ".png");
                Capture(shot, w, h);
                Console.WriteLine($"FRONTEND: {scenario}, page {session.Menu.Page?.Index}, WGL {w}x{h}; {shot}");
                return 0;
            }
            void Touch(int phase)
            {
                float x = w * 413f / 1280, y = h * 307f / 720;
                if (type.GetMethod("Touch") is MethodInfo touch)
                    touch.Invoke(screen, new object[] { x, y, phase });
                else
                    type.GetMethod("Post")!.Invoke(screen, new object[] { (Action<FrontendSession>)(s => s.TouchCanvas(x, y, phase)) });
            }
            for (int i = 0; i < 30; i++) Draw();
            if (session.Menu.Page?.Index != MovieGallery.FirstPage || !session.Movies.Open(0))
                throw new InvalidOperationException("The first gallery tile must be open.");
            Capture(Path.Combine(output, "album.png"), w, h);
            Touch(0);
            Draw();
            if (scenario == "missing")
            {
                if (Movie() != null || session.Menu.Page?.Index != MovieGallery.FirstPage || session.Popup?.IsOpen != true)
                    throw new InvalidOperationException("Missing media should keep the gallery open and show a localized notice.");
                var text = new HashSet<string>();
                session.Menu.Font.ObserveDrawing = s => text.Add(s);
                for (int i = 0; i < 30; i++) Draw();
                if (!text.Contains("影片播放失败，请检查游戏数据或查看日志。"))
                    throw new InvalidOperationException("Missing media notice did not draw in Chinese.");
                Capture(Path.Combine(output, "missing.png"), w, h);
                Console.WriteLine("PASS: missing movie shows a Chinese message and leaves the gallery open.");
                return 0;
            }
            var movie = Movie() ?? throw new InvalidOperationException("Click did not open the first film.");
            if (movie.Name != "01") throw new InvalidOperationException($"Unexpected film {movie.Name}.");
            Console.WriteLine("GALLERY: page 19, thumbnail 01, press started playback.");
            Touch(2);
            Draw();
            if (Movie() != movie)
            {
                Console.WriteLine($"FAIL: mouse release ended playback at {movie.Position:0.000}s (skipped={movie.Skipped}).");
                return 1;
            }
            Touch(1);
            Draw();
            for (int i = 0; i < 260 && Movie() == movie && movie.FrameIndex < 32; i++) Draw();
            if (Movie() != movie || movie.FrameIndex < 32 || movie.Error != null)
                throw new InvalidOperationException($"Film did not advance: frame={movie.FrameIndex}, error={movie.Error}.");
            Console.WriteLine($"GALLERY: release and drag ignored; decoded frame={movie.FrameIndex}, {movie.FrameCount} total.");
            Capture(Path.Combine(output, "playing.png"), w, h);
            Touch(0);
            Draw();
            if (Movie() != null || !movie.Skipped || session.Menu.Page?.Index != MovieGallery.FirstPage)
                throw new InvalidOperationException("A deliberate second press should skip and return to page 19.");
            Touch(2);
            Draw();
            Capture(Path.Combine(output, "returned.png"), w, h);
            Console.WriteLine("PASS: click release leaves playback running; next press skips and returns to the gallery.");
            return 0;
        }
        finally
        {
            Movie()?.Dispose();
            window.DeleteContext(context);
        }
    }

    internal static int DecodeAll(string game)
    {
        game = Path.GetFullPath(game);
        DesktopLocale.Init("zh-CN");
        if (!DesktopGame.Prepare(game)) throw new InvalidOperationException("Expected an AMHE0 fixture.");
        Directory.SetCurrentDirectory(game);
        Paths.UpdatePaths();
        Paths.ChooseMphPath();
        int count = 0;
        for (int call = MovieGallery.FirstCall; call <= MovieGallery.LastCall; call++)
        {
            string file = MovieGallery.FileOf(MovieGallery.MovieOfCall(call));
            using var movie = MoviePlayer.Open(file);
            for (int i = 0; i < 600 && !movie.Finished && movie.FrameIndex < 3; i++)
            {
                Thread.Sleep(5);
                movie.Update(1 / 60d);
            }
            if (movie.FrameIndex < 3 || movie.Error != null)
                throw new InvalidOperationException($"Movie tile {call}/{file} did not play: {movie.Error}");
            Console.WriteLine($"TILE {call}: file {file}, frames {movie.FrameIndex + 1}/{movie.FrameCount}, audio {movie.AudioChannels}ch.");
            count++;
        }
        Console.WriteLine($"PASS: all {count} gallery tiles decoded successive frames.");
        return 0;
    }

    private static void Capture(string path, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        GLES30.GlReadPixels(0, 0, width, height, GLES30.GlRgba, GLES30.GlUnsignedByte, rgba);
        var pixels = new byte[rgba.Length];
        for (int y = 0; y < height; y++)
            Buffer.BlockCopy(rgba, (height - 1 - y) * width * 4, pixels, y * width * 4, width * 4);
        DesktopPlatform.EncodePng(path, pixels, width, height);
    }
}
