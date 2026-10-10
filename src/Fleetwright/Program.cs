namespace Fleetwright;

public static unsafe class Program
{
    public static int Main(string[] args)
    {
        // Parse command line arguments
        int screenshotFrame = -1;
        string screenshotPath = "screenshot.png";
        bool screenshotUi = false;
        uint width = 0, height = 0;
        string? shipPath = null, navy = null, era = null, view = null, camera = null, show = null;

        foreach (string arg in args)
        {
            if (arg.StartsWith("-screenshot="))
            {
                string value = arg.Substring("-screenshot=".Length);
                if (int.TryParse(value, out int frame))
                {
                    screenshotFrame = frame;
                }
            }
            else if (arg.StartsWith("-ship="))
            {
                shipPath = arg.Substring("-ship=".Length);
            }
            else if (arg.StartsWith("-navy="))
            {
                navy = arg.Substring("-navy=".Length);
            }
            else if (arg.StartsWith("-era="))
            {
                era = arg.Substring("-era=".Length);
            }
            else if (arg.StartsWith("-view="))
            {
                view = arg.Substring("-view=".Length);
            }
            else if (arg.StartsWith("-camera="))
            {
                camera = arg.Substring("-camera=".Length);
            }
            else if (arg.StartsWith("-show="))
            {
                show = arg.Substring("-show=".Length);
            }
            else if (arg == "-ui")   // the screenshot shows the UI too
            {
                screenshotUi = true;
            }
            else if (arg.StartsWith("-size="))   // the window's size, WxH
            {
                var wh = arg.Substring("-size=".Length).Split('x');
                if (wh.Length != 2 || !uint.TryParse(wh[0], out width) || !uint.TryParse(wh[1], out height) || width == 0 || height == 0)
                {
                    Console.Error.WriteLine($"Bad size: {arg} (want -size=1920x1080)");
                    return 1;
                }
            }
            else if (arg.StartsWith("-output="))
            {
                screenshotPath = arg.Substring("-output=".Length);
            }
        }

        try
        {
            using var engine = new Sdl3GpuEngine();
            if (width > 0)
                engine.SetWindowSize(width, height);
            engine.Init();

            // Configure screenshot if requested
            if (screenshotFrame >= 0)
            {
                engine.SetScreenshotCapture(screenshotFrame, screenshotPath, screenshotUi);
            }

            // The design's scenes: the ship viewer (sprites) and the hitbox viewer, on one shared design
            shipPath ??= Path.Combine(AppContext.BaseDirectory, DefaultDesign);
            if (!File.Exists(shipPath))
            {
                Console.Error.WriteLine($"Design not found: {shipPath}");
                return 1;
            }
            nint device = (nint)engine.Device;   // lambdas may not capture a pointer
            using var session = new DesignSession(shipPath, ShipViewer.Scale, ShipViewer.MipLevels, device);
            engine.Scene = new SceneSwitcher(view == "hitbox" ? 1 : 0,
                ("Ship", () => new ShipViewer((SDL.SDL_GPUDevice*)device, session, navy, era)),
                ("Hitboxes", () => new HitView.HitboxScene((SDL.SDL_GPUDevice*)device, session, camera, show)));

            // Run the engine
            engine.Run();

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Engine error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    /// <summary>The design shown without -ship=: a game asset (GameAsset in the csproj), next to the exe.</summary>
    const string DefaultDesign = "Content/Designs/bismarck.json";
}
