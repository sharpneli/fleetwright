namespace Fleetwright;

public static unsafe class Program
{
    public static int Main(string[] args)
    {
        // Parse command line arguments
        int screenshotFrame = -1;
        string screenshotPath = "screenshot.png";
        bool screenshotUi = false, newDesign = false;
        int section = 0;
        string[]? steps = null;
        string[]? input = null;
        var units = Designer.UnitSystem.Metric;
        uint width = 0, height = 0;
        string? shipPath = null, navy = null, era = null, view = null, camera = null, show = null;
        bool designerFlags = false;   // flags that only mean something in the designer open it when no -view is given
        var mapCamera = Map.MapScene.DefaultCamera;
        var start = Sim.Core.WorldTime.FromUtc(1916, 5, 31, 14, 0);
        double? startSpeed = null;
        bool startPaused = false;

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
            else if (arg == "-new")   // the designer starts from the empty hull
            {
                newDesign = designerFlags = true;
            }
            else if (arg.StartsWith("-map="))   // the world map's camera: lat,lon,metres per pixel
            {
                var v = arg.Substring("-map=".Length).Split(',');
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                if (v.Length != 3 || !double.TryParse(v[0], ci, out double lat) || !double.TryParse(v[1], ci, out double lon)
                    || !double.TryParse(v[2], ci, out double mpp))
                {
                    Console.Error.WriteLine($"Bad map camera: {arg} (want -map=56.7,5.8,12000: lat, lon, metres per pixel)");
                    return 1;
                }
                mapCamera = Map.MapCamera.At(lat, lon, mpp);
            }
            else if (arg.StartsWith("-time="))   // the world's start, UTC: 1916-05-31T15:48
            {
                if (!DateTime.TryParseExact(arg.Substring("-time=".Length), ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd"],
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var t))
                {
                    Console.Error.WriteLine($"Bad time: {arg} (want -time=1916-05-31T15:48, UTC)");
                    return 1;
                }
                start = Sim.Core.WorldTime.FromUtc(t);
            }
            else if (arg.StartsWith("-speed="))   // the world's clock speed at start
            {
                if (double.TryParse(arg.Substring("-speed=".Length), System.Globalization.CultureInfo.InvariantCulture, out double sp))
                    startSpeed = sp;
            }
            else if (arg == "-paused")   // the world starts paused
            {
                startPaused = true;
            }
            else if (arg.StartsWith("-steps="))   // designer knob steps at startup: speed:-2,belt:+3
            {
                steps = arg.Substring("-steps=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries);
                designerFlags = true;
            }
            else if (arg.StartsWith("-input="))   // scripted UI input for testing: 60:click:957,559;80:type:12
            {
                input = arg.Substring("-input=".Length).Split(';', StringSplitOptions.RemoveEmptyEntries);
            }
            else if (arg.StartsWith("-designer-ui="))   // designer UI state for screenshots: secsel:1:2,secmenu
            {
                Designer.DesignerScene.DevUi = arg.Substring("-designer-ui=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries);
                designerFlags = true;
            }
            else if (arg == "-units=imperial")   // the designer's units
            {
                units = Designer.UnitSystem.Imperial;
                designerFlags = true;
            }
            else if (arg.StartsWith("-uiscale="))   // a factor over the UI scale (the window's height over 1080 px)
            {
                if (float.TryParse(arg.Substring("-uiscale=".Length), System.Globalization.CultureInfo.InvariantCulture, out float f) && f > 0)
                    UiFonts.UserScale = f;
            }
            else if (arg.StartsWith("-section="))   // the designer's open section, 0-7
            {
                int.TryParse(arg.Substring("-section=".Length), out section);
                designerFlags = true;
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
            if (input != null)
                engine.ScriptedInput.AddRange(input);
            if (screenshotFrame >= 0)
            {
                engine.SetScreenshotCapture(screenshotFrame, screenshotPath, screenshotUi);
            }

            // The design's scenes: the ship viewer (sprites) and the hitbox viewer, on one shared design
            shipPath = shipPath == null ? Path.Combine(AppContext.BaseDirectory, DefaultDesign) : FindDesign(shipPath);
            if (!File.Exists(shipPath))
            {
                Console.Error.WriteLine($"Design not found: {shipPath} (give a path to a .json, or a shipped design's name: -ship=yamato)");
                return 1;
            }
            nint device = (nint)engine.Device;   // lambdas may not capture a pointer
            using var session = new DesignSession(shipPath, ShipViewer.Scale, ShipViewer.MipLevels, device) { Units = units };
            // the designer edits the session's design, so every scene shows the same ship; with -steps it is made at
            // once, so its edits are in place for the first frame of any scene
            Designer.DesignerScene MakeDesigner()
            {
                if (newDesign)
                    session.Load(Designer.DesignDoc.Empty(), null);
                return new Designer.DesignerScene((SDL.SDL_GPUDevice*)device, session, units, section, steps);
            }
            // a design json dropped on the window is an edit to it, in every scene (unsaved until saved) (sharing ships is passing files around)
            engine.FileDropped = path =>
            {
                if (session.Open(path) is { } error)
                    SDL.SDL3.SDL_ShowSimpleMessageBox(SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_WARNING, "Not a design",
                        $"{Path.GetFileName(path)} couldn't be opened as a design:\n{error}", null);
            };
            var early = steps != null || newDesign ? MakeDesigner() : null;
            // the world: the app's, not a tab's, so it keeps running whatever tab is open (docs/world/architecture.md)
            using var world = new Map.WorldSession(start, 1);
            if (startSpeed is double speed0)
                world.Send(Sim.Command.SetSpeed(speed0));
            if (startPaused)
                world.Send(Sim.Command.Pause());
            engine.FrameStarted = world.Frame;

            // the world opens by default; -view=designer (or any designer flag), -view=hitbox, -view=ship or -view=profile
            // opens the others
            view ??= designerFlags ? "designer" : null;
            engine.Scene = new SceneSwitcher(view switch { "designer" => 1, "hitbox" => 2, "ship" => 3, "profile" => 4, _ => 0 },
                ("World", () => new Map.MapScene((SDL.SDL_GPUDevice*)device, world, mapCamera)),
                ("Designer", () => early ?? MakeDesigner()),
                ("Hitboxes", () => new HitView.HitboxScene((SDL.SDL_GPUDevice*)device, session, camera, show)),
                ("Ship", () => new ShipViewer((SDL.SDL_GPUDevice*)device, session, navy, era)),
                ("Profile", () => new ProfileScene((SDL.SDL_GPUDevice*)device, session)));

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

    /// <summary>-ship='s design: the path as given, else with ".json" added, else a shipped design of that name
    /// (Content/Designs next to the exe). What's not found comes back as given.</summary>
    static string FindDesign(string arg)
    {
        string name = Path.GetFileNameWithoutExtension(arg);
        foreach (var p in new[] { arg, arg + ".json", Path.Combine(AppContext.BaseDirectory, "Content", "Designs", name + ".json") })
            if (File.Exists(p))
                return p;
        return arg;
    }
}
