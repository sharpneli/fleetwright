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
        string? devShips = null;
        int selectShip = -1;
        var captains = new List<string>();
        bool frameTimes = false;

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
            else if (arg == "-frametimes")   // report the frame times at exit
            {
                frameTimes = true;
            }
            else if (arg == "-paused")   // the world starts paused
            {
                startPaused = true;
            }
            else if (arg.StartsWith("-ships="))   // dev ships in the North Sea: -ships=dreadnought[:count]
            {
                devShips = arg.Substring("-ships=".Length);
            }
            else if (arg.StartsWith("-captain="))   // a dev ship's captain: -captain=2:follow:1[:800], keep, run:1, none
            {
                captains.Add(arg.Substring("-captain=".Length));
            }
            else if (arg.StartsWith("-select="))   // the ship selected on the map at start, by entity id (screenshots)
            {
                int.TryParse(arg.Substring("-select=".Length), out selectShip);
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
            engine.RecordFrameTimes = frameTimes;

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
            // the classes in play and their art, the app's: the world and the map share them
            using var classes = new Sim.Ships.ShipClassRegistry();
            using var shipArt = new Map.ShipArtLibrary((SDL.SDL_GPUDevice*)device, classes);
            using var world = new Map.WorldSession(start, 1, classes, devShips != null ? w => SpawnDevShips(w, devShips) : null);
            foreach (var cap in captains)
                if (DevCaptain(world.Ai, cap) is { } order)
                    world.Send(order);
                else
                    Console.Error.WriteLine($"Bad captain: -captain={cap} (want entity:follow:target[:metres], entity:run:target, entity:keep or entity:none)");
            if (startSpeed is double speed0)
                world.Send(Sim.Command.SetSpeed(speed0));
            if (startPaused)
                world.Send(Sim.Command.Pause());
            engine.FrameStarted = world.Frame;

            // the world opens by default; -view=designer (or any designer flag), -view=hitbox, -view=ship or -view=profile
            // opens the others
            view ??= designerFlags ? "designer" : null;
            engine.Scene = new SceneSwitcher(view switch { "designer" => 1, "hitbox" => 2, "ship" => 3, "profile" => 4, _ => 0 },
                ("World", () => new Map.MapScene((SDL.SDL_GPUDevice*)device, world, mapCamera, shipArt, selectShip)),
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

    /// <summary>-ships=design[:count]: a block of ships of one design at sea in the North Sea, 600 m apart, steaming
    /// 045° at 15 kn, every other one ordered round to 135° so the turns show (docs/battle/plan.md step 2.2: dev ships
    /// until missions place forces).</summary>
    static void SpawnDevShips(Sim.World w, string arg)
    {
        var parts = arg.Split(':');
        int count = parts.Length > 1 && int.TryParse(parts[1], out int n) ? Math.Max(n, 1) : 1;
        var design = Shipgen.Design.Load(FindDesign(parts[0]));
        int c = w.Ships.Registry.Request(design);
        int side = (int)Math.Ceiling(Math.Sqrt(count));
        double dLat = 600 / 111_195.0, dLon = dLat / Math.Cos(56.5 * Math.PI / 180);
        for (int i = 0; i < count; i++)
            w.Apply(Sim.ShipSystem.Spawn(c, 56.5 + dLat * (i / side), 4.0 + dLon * (i % side), 45, 15));
        for (int e = 2; e <= count; e += 2)
            w.Apply(Sim.ShipSystem.Course(e, 135));
    }

    /// <summary>-captain=entity:order[:target[:metres]]: a captain's order for a dev ship.</summary>
    static Sim.Command? DevCaptain(Sim.Ai.AiSystem ai, string arg)
    {
        var p = arg.Split(':');
        if (p.Length < 2 || !int.TryParse(p[0], out int e))
            return null;
        int target = p.Length > 2 && int.TryParse(p[2], out int t) ? t : 0;
        double metres = p.Length > 3 && double.TryParse(p[3], System.Globalization.CultureInfo.InvariantCulture, out double m) ? m : 0;
        Sim.Ai.Captains.CaptainOrder? order = p[1] switch
        {
            "follow" => Sim.Ai.Captains.CaptainOrder.Follow,
            "run" => Sim.Ai.Captains.CaptainOrder.Run,
            "keep" => Sim.Ai.Captains.CaptainOrder.KeepCourse,
            "none" => Sim.Ai.Captains.CaptainOrder.None,
            _ => null,
        };
        return order is { } o ? ai.Captain(e, o, target, metres) : null;
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
