namespace Fleetwright;

public static unsafe class Program
{
    public static int Main(string[] args)
    {
        // Parse command line arguments
        int screenshotFrame = -1;
        string screenshotPath = "screenshot.png";
        bool screenshotUi = false, newDesign = false, hitRay = false;
        int section = 0;
        string[]? steps = null;
        string[]? input = null;
        var units = Designer.UnitSystem.Metric;
        uint width = 0, height = 0;
        string? shipPath = null, navy = null, era = null, view = null, camera = null, show = null;
        bool designerFlags = false;   // flags that only mean something in the designer open it when no -view is given
        var mapCamera = Map.MapScene.DefaultCamera;
        bool mapGiven = false;
        var start = Sim.Core.WorldTime.FromUtc(1916, 5, 31, 14, 0);
        double? startSpeed = null;
        bool startPaused = false, noPause = false;
        string? hitBench = null;
        int follow = 0;
        var devShips = new List<string>();
        int selectShip = -1;
        var captains = new List<string>();
        var searches = new List<string>();
        var fires = new List<string>();
        var loadouts = new List<string>();
        var engages = new List<string>();
        string? weather = null;
        var torpedoes = new List<string>();
        var screens = new List<string>();
        var damages = new List<string>();
        var firefightings = new List<string>();
        var readinesses = new List<string>();
        var devGroups = new List<string>();
        var devForces = new List<string>();
        bool frameTimes = false;
        string? variants = null, scenarioArg = null, wakeBake = null;

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
            else if (arg.StartsWith("-clip="))   // the hitbox viewer's clip box at start: AFT,FORE[,port][,cut] in m
            {
                HitView.HitboxScene.DevClip = arg.Substring("-clip=".Length);
            }
            else if (arg == "-hitray")   // the hitbox viewer starts with a click firing a ray (step 4.1's tracer)
            {
                hitRay = true;
            }
            else if (arg.StartsWith("-hitshot="))   // the hitbox viewer fires a shell at start: X,Y,Z,RANGE_KM,ASPECT[,BATTERY[,KIND]][,at=S]
            {
                HitView.HitboxScene.DevShot = arg.Substring("-hitshot=".Length);
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
                mapGiven = true;
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
            else if (arg == "-gputime")   // each frame waits for its GPU work; -frametimes reports submit to done
            {
                Sdl3GpuEngine.GpuTime = true;
            }
            else if (arg.StartsWith("-viewer="))   // the ship viewer's view for captures: -viewer=sun:300:30,moon:120:40:0,shade:normal,at:20:0:10
            {
                ShipViewer.Dev = arg.Substring("-viewer=".Length);
            }
            else if (arg.StartsWith("-outline="))   // every look's plan outline weight, 0-1, for side-by-side captures
            {
                Shipgen.Render.Looks.OutlineOverride = double.Parse(arg.Substring("-outline=".Length), System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (arg.StartsWith("-lights="))   // light table knobs, for side-by-side captures: -lights=roughness:0.6,specular:0
            {
                foreach (var kv in arg.Substring("-lights=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (!LightTable.Current.Set(kv))
                        Console.Error.WriteLine($"Bad light knob: {kv} (want roughness, specular, knee, shadow, moon, moonsky)");
            }
            else if (arg.StartsWith("-sea="))   // the sea's look knobs, for side-by-side captures: -sea=relief:3,facet:0.7,wake:1
            {
                foreach (var kv in arg.Substring("-sea=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (!Map.SeaView.Set(kv))
                        Console.Error.WriteLine($"Bad sea knob: {kv} (want facet, relief, lowsunrelief, chop, debug, nightrelief, moonnight, starnight, wake, foam)");
            }
            else if (arg.StartsWith("-splash="))   // the world view's splashes' knobs (docs/visuals/splashes.md): -splash=pitch:45,floor:2,dye:auto
            {
                foreach (var kv in arg.Substring("-splash=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (!Map.Splashes.SplashLook.Set(kv))
                        Console.Error.WriteLine($"Bad splash knob: {kv} (want {Map.Splashes.SplashLook.Knobs})");
            }
            else if (arg.StartsWith("-flash="))   // the world view's muzzle flashes' knobs (docs/visuals/muzzle-flashes.md): -flash=gain:2
            {
                foreach (var kv in arg.Substring("-flash=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (!Map.Flashes.FlashLook.Set(kv))
                        Console.Error.WriteLine($"Bad flash knob: {kv} (want {Map.Flashes.FlashLook.Knobs})");
            }
            else if (arg.StartsWith("-magazine="))   // the world view's magazine explosions' knobs (docs/visuals/magazine-explosions.md): -magazine=at:20
            {
                foreach (var kv in arg.Substring("-magazine=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (!Map.Magazines.MagazineLook.Set(kv))
                        Console.Error.WriteLine($"Bad magazine knob: {kv} (want {Map.Magazines.MagazineLook.Knobs})");
            }
            else if (arg.StartsWith("-hit="))   // the world view's shell hits' knobs (docs/visuals/hits-and-fires.md): -hit=at:0.4,scale:1.2
            {
                foreach (var kv in arg.Substring("-hit=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (!Map.Hits.HitLook.Set(kv))
                        Console.Error.WriteLine($"Bad hit knob: {kv} (want {Map.Hits.HitLook.Knobs})");
            }
            else if (arg.StartsWith("-fires="))   // the world view's fires' knobs (docs/visuals/hits-and-fires.md I4-I5): -fires=height:1.2
            {
                foreach (var kv in arg.Substring("-fires=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (!Map.Fires.FireLook.Set(kv))
                        Console.Error.WriteLine($"Bad fires knob: {kv} (want {Map.Fires.FireLook.Knobs})");
            }
            else if (arg.StartsWith("-funnels="))   // the world view's funnel smoke (docs/visuals/hits-and-fires.md I6): -funnels=output:1500,on:0
            {
                foreach (var kv in arg.Substring("-funnels=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (!Map.Fires.FunnelSmoke.Set(kv))
                        Console.Error.WriteLine($"Bad funnels knob: {kv} (want {Map.Fires.FunnelSmoke.Knobs})");
            }
            else if (arg.StartsWith("-follow="))   // the camera kept on a ship (an entity): benches and captures
            {
                int.TryParse(arg.AsSpan("-follow=".Length), out follow);
            }
            else if (arg.StartsWith("-sinking="))   // the world view's sinking (docs/visuals/hits-and-fires.md I7): -sinking=on:0
            {
                Map.Sinking.SinkingEffects.On = !arg.EndsWith("on:0");
            }
            else if (arg.StartsWith("-hitbench="))   // the hits bench: -hitbench=TARGET:SHOOTER[:every_s], shells struck at a still ship in turn
            {
                hitBench = arg.Substring("-hitbench=".Length);
            }
            else if (arg.StartsWith("-tracks="))   // the world view's torpedoes' knobs (docs/visuals/torpedoes.md): -tracks=fast:0.8,life:90
            {
                foreach (var kv in arg.Substring("-tracks=".Length).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (!Map.Torpedoes.TorpedoLook.Set(kv))
                        Console.Error.WriteLine($"Bad torpedo knob: {kv} (want {Map.Torpedoes.TorpedoLook.Knobs})");
            }
            else if (arg == "-paused")   // the world starts paused
            {
                startPaused = true;
            }
            else if (arg == "-nopause")   // the player's encounters don't pause the clock (screenshots of a fight going on)
            {
                noPause = true;
            }
            else if (arg.StartsWith("-ships="))   // dev ships: -ships=dreadnought[:count[:side]][@lat,lon,heading[,kn]] (the North Sea by default); repeatable
            {
                devShips.Add(arg.Substring("-ships=".Length));
            }
            else if (arg.StartsWith("-group="))   // a dev group: -group=1,2,3,4[:column|abreast|echelon-s|echelon-p[:metres]]
            {
                devGroups.Add(arg.Substring("-group=".Length));
            }
            else if (arg.StartsWith("-force="))   // a dev force of -group= groups (by their order, from 1): -force=1,2,3[:abreast|column|echelon-s|echelon-p[:metres]]
            {
                devForces.Add(arg.Substring("-force=".Length));
            }
            else if (arg.StartsWith("-captain="))   // a dev ship's captain: -captain=2:follow:1[:800], keep, run:1, shadow:1[:m]
            {
                captains.Add(arg.Substring("-captain=".Length));
            }
            else if (arg.StartsWith("-fire="))   // a dev salvo: -fire=ship[:battery][:kind]:lat,lon (docs/battle/fighting.md 4.2a)
            {
                fires.Add(arg.Substring("-fire=".Length));
            }
            else if (arg.StartsWith("-loadout="))   // a dev ship's shells: -loadout=ship:battery:shell[@share][+shell...][|battery:...] (shells-armour-damage.md S5)
            {
                loadouts.Add(arg.Substring("-loadout=".Length));
            }
            else if (arg.StartsWith("-weather="))   // the dev world's weather: -weather=wind_from,kn[,visibility_km[,air_over_sea_k]]
            {
                weather = arg.Substring("-weather=".Length);
            }
            else if (arg.StartsWith("-engage="))   // a dev battery engages: -engage=ship[:battery]:target (docs/battle/fighting.md 4.2b)
            {
                engages.Add(arg.Substring("-engage=".Length));
            }
            else if (arg.StartsWith("-torpedo="))   // a dev spread: -torpedo=ship:target[:n] (docs/battle/fighting.md 4.6)
            {
                torpedoes.Add(arg.Substring("-torpedo=".Length));
            }
            else if (arg == "-smokefield")   // the smoke field on (docs/battle/smoke.md; settings.json's Smoke stays off)
            {
                Map.WorldSession.SmokeField = true;
            }
            else if (arg.StartsWith("-smokelook="))   // the smoke field's drawing's knobs (docs/battle/smoke.md Z5; Map/Smoke/SmokeFieldLook.cs)
            {
                foreach (var kv in arg.Substring("-smokelook=".Length).Split(','))
                    if (!Map.Smoke.SmokeFieldLook.Set(kv))
                        Console.Error.WriteLine($"Bad -smokelook knob: {kv} (have {Map.Smoke.SmokeFieldLook.Knobs})");
            }
            else if (arg.StartsWith("-smoke="))   // a dev ship makes smoke: -smoke=ship[:on|off] (docs/battle/smoke.md Z4; the field's on in settings.json)
            {
                screens.Add(arg.Substring("-smoke=".Length));
            }
            else if (arg.StartsWith("-damage-control="))   // a side's firefighting doctrine: -damage-control=side:name (docs/battle/fire.md B4; settings.json's DamageControl, 1890 the weakest)
            {
                firefightings.Add(arg.Substring("-damage-control=".Length));
            }
            else if (arg.StartsWith("-readiness="))   // a side's orders before action for fire: -readiness=side:name (docs/battle/fire.md B1; settings.json's Readiness)
            {
                readinesses.Add(arg.Substring("-readiness=".Length));
            }
            else if (arg.StartsWith("-hole=") || arg.StartsWith("-flood=") || arg.StartsWith("-jam=") || arg.StartsWith("-wreck=") ||
                     arg.StartsWith("-burn=") || arg.StartsWith("-sink="))   // dev damage (docs/battle/fighting.md 4.3b, 4.4): see DevDamage
            {
                damages.Add(arg);
            }
            else if (arg.StartsWith("-search="))   // a dev search: -search=1:ladder[:leg nm[:nm apart[:legs]]] (ladder, square, sector, patrol)
            {
                searches.Add(arg.Substring("-search=".Length));
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
            else if (arg.StartsWith("-worldview="))   // the map's table-to-world boundary, metres per pixel at 1080p (docs/visuals/sea.md W0)
            {
                if (double.TryParse(arg.Substring("-worldview=".Length), System.Globalization.CultureInfo.InvariantCulture, out double m) && m > 0)
                    Map.WorldView.BoundaryM = m;
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
            else if (arg.StartsWith("-variants="))   // writes a spec's design variants (knob steps on shipped designs) and exits
            {
                variants = arg.Substring("-variants=".Length);
            }
            else if (arg.StartsWith("-wakebake="))   // writes a design's wake bakes as PNGs and exits: -wakebake=destroyer:15,25,35 [-output=DIR]
            {
                wakeBake = arg.Substring("-wakebake=".Length);
            }
            else if (arg.StartsWith("-scenario="))   // the world starts with a scenario: a path, or a shipped one's name (-scenario=jutland)
            {
                scenarioArg = arg.Substring("-scenario=".Length);
            }
            else if (arg.StartsWith("-output="))
            {
                screenshotPath = arg.Substring("-output=".Length);
            }
        }

        if (variants != null)
            return Designer.DesignVariants.Write(variants) == 0 ? 0 : 1;
        if (wakeBake != null)
            return Map.Wakes.WakeLook.Write(wakeBake, args.Any(a => a.StartsWith("-output=")) ? screenshotPath : null, FindDesign);

        // the land the world sails on, built while the window opens (docs/world/terrain-plan.md T1)
        var shore = Map.WorldSession.LoadShore();
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
            Map.WorldSession? worldRef = null;   // the world, for a scenario dropped on the window
            engine.FileDropped = path =>
            {
                if (Sim.Missions.Scenario.IsScenario(path))
                {
                    worldRef?.Load(path);   // a scenario: the world tab's new world
                    return;
                }
                if (session.Open(path) is { } error)
                    SDL.SDL3.SDL_ShowSimpleMessageBox(SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_WARNING, "Not a design",
                        $"{Path.GetFileName(path)} couldn't be opened as a design:\n{error}", null);
            };
            var early = steps != null || newDesign ? MakeDesigner() : null;
            // the world: the app's, not a tab's, so it keeps running whatever tab is open (docs/world/architecture.md)
            // the classes in play and their art, the app's: the world and the map share them
            using var classes = new Sim.Ships.ShipClassRegistry();
            using var shipArt = new Map.ShipArtLibrary((SDL.SDL_GPUDevice*)device, classes);
            using var world = new Map.WorldSession(start, 1, classes, shore.Result, devShips.Count > 0 ? w =>
            {
                devShips.ForEach(d => SpawnDevShips(w, d));
                loadouts.ForEach(l => DevLoadout(w, l));
            } : null);
            foreach (var grp in devGroups)
                foreach (var c in DevGroup(grp))
                    world.Send(c);
            foreach (var force in devForces)
                foreach (var c in DevForce(force))
                    world.Send(c);
            foreach (var cap in captains)
                if (DevCaptain(world.Ai, cap) is { } order)
                    world.Send(order);
                else
                    Console.Error.WriteLine($"Bad captain: -captain={cap} (want entity:follow:target[:metres], entity:run:target or entity:keep)");
            foreach (var search in searches)
                if (!DevSearch(world, search, devGroups, devForces))
                    Console.Error.WriteLine($"Bad search: -search={search} (want entity:ladder|square|sector|patrol[:leg nm[:nm apart[:legs]]])");
            foreach (var fire in fires)
                if (DevFire(fire) is { } shot)
                    world.Send(shot);
                else
                    Console.Error.WriteLine($"Bad fire: -fire={fire} (want ship[:battery][:ap|common|he]:lat,lon)");
            if (weather != null)
            {
                if (DevWeather(weather) is { } w)
                    world.Send(w);
                else
                    Console.Error.WriteLine($"Bad weather: -weather={weather} (want wind_from,kn[,visibility_km[,air_over_sea_k]])");
            }
            foreach (var engage in engages)
                if (DevEngage(engage) is { } order)
                    world.Send(order);
                else
                    Console.Error.WriteLine($"Bad engage: -engage={engage} (want ship[:battery]:target)");
            foreach (var torpedo in torpedoes)
                if (DevTorpedo(torpedo) is { } order)
                    world.Send(order);
                else
                    Console.Error.WriteLine($"Bad torpedo: -torpedo={torpedo} (want ship:target[:n])");
            foreach (var screen in screens)
            {
                var p = screen.Split(':');
                if (p.Length is 1 or 2 && int.TryParse(p[0], out int ship) && (p.Length == 1 || p[1] is "on" or "off"))
                    world.Send(Sim.SmokeSystem.MakeSmoke(ship, p.Length == 1 || p[1] == "on"));
                else
                    Console.Error.WriteLine($"Bad smoke: -smoke={screen} (want ship[:on|off])");
            }
            foreach (var ff in firefightings)
            {
                var p = ff.Split(':', 2);
                var doctrines = world.Settings.DamageControl;
                int index = p.Length == 2 ? Array.FindIndex(doctrines, d => d.Name == p[1]) : -1;
                if (p.Length == 2 && int.TryParse(p[0], out int side) && index >= 0)
                    world.Send(Sim.DamageSystem.SetDamageControl(side, index));
                else
                    Console.Error.WriteLine($"Bad firefighting: -damage-control={ff} (want side:{string.Join("|", doctrines.Select(d => d.Name))})");
            }
            foreach (var rd in readinesses)
            {
                var p = rd.Split(':', 2);
                var orders = world.Settings.Readiness;
                int index = p.Length == 2 ? Array.FindIndex(orders, d => d.Name == p[1]) : -1;
                if (p.Length == 2 && int.TryParse(p[0], out int side) && index >= 0)
                    world.Send(Sim.DamageSystem.SetReadiness(side, index));
                else
                    Console.Error.WriteLine($"Bad readiness: -readiness={rd} (want side:{string.Join("|", orders.Select(d => d.Name))})");
            }
            foreach (var damage in damages)
                if (DevDamage(damage) is { } order)
                    world.Send(order);
                else
                    Console.Error.WriteLine($"Bad damage: {damage} (want -hole=ship:mid|bow|stern|x:z:m2[:p], -flood=ship:kind[:n], -jam=ship:mount[:min], " +
                                            "-wreck=ship:kind[:nth[:damaged|disabled|destroyed]], -burn=ship:x, -sink=ship:foundered|capsized|magazine[:mount])");
            worldRef = world;
            if (scenarioArg != null)
            {
                string found = FindScenario(scenarioArg, out string looked);
                if (File.Exists(found))
                    world.Load(found);
                else
                    Console.Error.WriteLine($"Scenario not found: {scenarioArg} (looked at {looked}; a new file in Content/Scenarios is copied next to the exe when the app builds)");
            }
            if (startSpeed is double speed0)
                world.Send(Sim.Command.SetSpeed(speed0));
            if (startPaused)
                world.Send(Sim.Command.Pause());
            if (noPause)
                world.Send(Sim.Command.SetInterest(Sim.InterestKind.Own, Sim.InterestAction.Nothing));
            engine.FrameStarted = world.Frame;

            // the world opens by default; -view=designer (or any designer flag), -view=hitbox, -view=ship or -view=profile
            // opens the others
            view ??= designerFlags ? "designer" : null;
            engine.Scene = new SceneSwitcher(view switch { "designer" => 1, "hitbox" => 2, "ship" => 3, "profile" => 4, _ => 0 },
                ("World", () => new Map.MapScene((SDL.SDL_GPUDevice*)device, world, mapCamera, shipArt, session, selectShip) { KeepCameraOnce = mapGiven && scenarioArg != null, SelectOnce = scenarioArg != null ? selectShip : -1, HitBench = hitBench, Follow = follow }),
                ("Designer", () => early ?? MakeDesigner()),
                ("Hitboxes", () => new HitView.HitboxScene((SDL.SDL_GPUDevice*)device, session, camera, show, hitRay)),
                ("Ship", () => new ShipViewer((SDL.SDL_GPUDevice*)device, session, navy, era)),
                ("Profile", () => new ProfileScene((SDL.SDL_GPUDevice*)device, session)));

            // Run the engine
            engine.Run();
            if (frameTimes)
                Console.Write(world.Perf.Report(world.Current, shipArt));

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Engine error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    /// <summary>-ships=design[:count[:side]]: a block of ships of one design at sea in the North Sea, 600 m apart, steaming
    /// 045° at 15 kn, every other one ordered round to 135° so the turns show (docs/battle/plan.md step 2.2: dev ships
    /// until missions place forces). With @lat,lon,heading[,kn] the block is there, all on that heading (the land
    /// scenarios of docs/world/terrain-plan.md). Entities are numbered across the flags in order.</summary>
    static void SpawnDevShips(Sim.World w, string arg)
    {
        double lat0 = 56.5, lon0 = 4.0, heading = 45, kn = 15;
        bool placed = false;
        int at = arg.IndexOf('@');
        if (at >= 0)
        {
            var q = arg.Substring(at + 1).Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            (lat0, lon0, heading, placed) = (q[0], q[1], q.Length > 2 ? q[2] : 0, true);
            if (q.Length > 3)
                kn = q[3];
            arg = arg.Substring(0, at);
        }
        int first = w.Ships.NewestEntity + 1;
        var parts = arg.Split(':');
        int count = parts.Length > 1 && int.TryParse(parts[1], out int n) ? Math.Max(n, 1) : 1;
        byte shipSide = parts.Length > 2 && byte.TryParse(parts[2], out byte sd) ? sd : (byte)0;
        var design = Shipgen.Design.Load(FindDesign(parts[0]));
        int c = w.Ships.Registry.Request(design);
        int side = (int)Math.Ceiling(Math.Sqrt(count));
        double dLat = 600 / 111_195.0, dLon = dLat / Math.Cos(lat0 * Math.PI / 180);
        for (int i = 0; i < count; i++)
            w.Apply(Sim.ShipSystem.Spawn(c, lat0 + dLat * (i / side), lon0 + dLon * (i % side), heading, kn, shipSide));
        if (!placed)
            for (int e = first + 1; e < first + count; e += 2)
                w.Apply(Sim.ShipSystem.Course(e, 135));
    }

    /// <summary>-loadout=ship:battery:shell[@share][+shell[@share]...][|battery:...]: a dev ship's shells other than her
    /// design's (docs/battle/shells-armour-damage.md S5), as she's spawned: a battery is main, secondary, main[i] or
    /// secondary[i], a shell a template's name (docs/shipgen/shell-templates.md), a block of the design's, or ap, common
    /// or he (the battery's own of that kind), shares left out splitting the rest.</summary>
    static void DevLoadout(Sim.World w, string arg)
    {
        int colon = arg.IndexOf(':');
        int ship = colon > 0 && int.TryParse(arg[..colon], out int e) ? e : 0, slot = ship > 0 ? w.Ships.Store.SlotOf(ship) : -1;
        if (slot < 0)
        {
            Console.Error.WriteLine($"Bad loadout: -loadout={arg} (want ship:battery:shell[@share][+shell...][|battery:...], a dev ship's entity)");
            return;
        }
        var errors = new List<string>();
        Sim.Ships.Loadout? lo = null;
        try
        {
            lo = w.Ships.Class(w.Ships.Store[slot].ClassIndex).Load(Sim.Ships.LoadoutSpec.Parse(arg[(colon + 1)..]), errors);
        }
        catch (FormatException x)
        {
            errors.Add(x.Message);
        }
        if (lo == null)
            Console.Error.WriteLine($"Bad loadout: -loadout={arg}: {string.Join("; ", errors)}");
        else
            w.Apply(Sim.ShipSystem.Load(ship, w.Ships.Registry.AddLoadout(lo)));
    }

    /// <summary>-fire=ship[:battery][:kind]:lat,lon: a dev ship's battery (0, the main, by default) fires a salvo at a
    /// point, of a kind (ap, common, he) or by the magazine order.</summary>
    static Sim.Command? DevFire(string arg)
    {
        var p = arg.Split(':');
        var ll = p[^1].Split(',');
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (p.Length is < 2 or > 4 || ll.Length != 2 || !int.TryParse(p[0], out int ship) || !double.TryParse(ll[0], inv, out double lat)
            || !double.TryParse(ll[1], inv, out double lon))
            return null;
        int battery = 0;
        Sim.Ships.ShellKind? kind = null;
        foreach (var x in p[1..^1])
            if (int.TryParse(x, out int b))
                battery = b;
            else if (x is "ap" or "common" or "he")
                kind = x switch { "ap" => Sim.Ships.ShellKind.Ap, "common" => Sim.Ships.ShellKind.Common, _ => Sim.Ships.ShellKind.He };
            else
                return null;
        return Sim.ShellSystem.FireAt(ship, battery, lat, lon, kind);
    }

    /// <summary>-weather=wind_from,kn[,visibility_km[,air_over_sea_k]]: the dev world's weather (degrees the wind is from,
    /// its knots, the visibility, 25 km without it, and the air's temperature over the sea's, K, 0 without it).</summary>
    static Sim.Command? DevWeather(string arg)
    {
        var p = arg.Split(',');
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        if (p.Length is < 2 or > 4 || !double.TryParse(p[0], ci, out double from) || !double.TryParse(p[1], ci, out double kn))
            return null;
        double km = p.Length >= 3 && double.TryParse(p[2], ci, out double v) ? v : 25;
        double airSea = p.Length == 4 && double.TryParse(p[3], ci, out double t) ? t : 0;
        return Sim.Command.SetWeather(km * 1000, from, kn * 0.514444, airSea);
    }

    /// <summary>-engage=ship[:battery]:target: a dev ship's battery (all of them without one) engages a ship.</summary>
    static Sim.Command? DevEngage(string arg)
    {
        var p = arg.Split(':');
        if (p.Length is < 2 or > 3 || !int.TryParse(p[0], out int ship) || !int.TryParse(p[^1], out int target))
            return null;
        int battery = p.Length == 3 && int.TryParse(p[1], out int b) ? b : -1;
        return Sim.GunnerySystem.Engage(ship, battery, target);
    }

    /// <summary>-torpedo=ship:target[:n]: a dev ship fires a spread of n (default 4) at a ship, aimed at it as it truly is
    /// (its navy needn't have it in sight yet).</summary>
    static Sim.Command? DevTorpedo(string arg)
    {
        var p = arg.Split(':');
        if (p.Length is < 2 or > 3 || !int.TryParse(p[0], out int ship) || !int.TryParse(p[1], out int target))
            return null;
        int n = p.Length == 3 && int.TryParse(p[2], out int k) ? k : 4;
        return Sim.TorpedoSystem.Launch(ship, target, n, truth: true);
    }

    /// <summary>Dev damage, as the damage system's dev commands: -hole=ship:mid|bow|stern|x:z:m2[:p] (a hole in the side, z m
    /// over the waterline, to port with :p), -flood=ship:kind[:n] (n spaces of a kind flooded: boiler_room, engine_room,
    /// magazine...), -jam=ship:mount[:min] (a mount's index; cleared after min minutes, else for the battle), -wreck=ship:kind[:nth[:state]] (the nth part of a component kind:
    /// superstructure, funnel, main...), -burn=ship:x (a fire in the highest space at x), -sink=ship:foundered|capsized|magazine[:mount] (it goes
    /// down now; a magazine's is the one serving that mount, else the fullest).</summary>
    static Sim.Command? DevDamage(string arg)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        int eq = arg.IndexOf('=');
        string flag = arg[1..eq];
        var p = arg[(eq + 1)..].Split(':');
        if (p.Length < 2 || !int.TryParse(p[0], out int ship))
            return null;
        static T? Kind<T>(string s) where T : struct, Enum => Enum.TryParse<T>(s.Replace("_", ""), true, out var k) ? k : null;
        switch (flag)
        {
            case "hole" when p.Length >= 4 && double.TryParse(p[2], inv, out double z) && double.TryParse(p[3], inv, out double area):
                var at = p[1] switch { "mid" => Sim.HoleAt.Mid, "bow" => Sim.HoleAt.Bow, "stern" => Sim.HoleAt.Stern, _ => Sim.HoleAt.X };
                double x = 0;
                if (at == Sim.HoleAt.X && !double.TryParse(p[1], inv, out x))
                    return null;
                return Sim.DamageSystem.SideHole(ship, at, x, z, area, p.Length < 5 || p[4] != "p");
            case "flood" when Kind<Shipgen.RoomKind>(p[1]) is { } room:
                return Sim.DamageSystem.FloodRooms(ship, room, p.Length > 2 && int.TryParse(p[2], out int n) ? n : int.MaxValue);
            case "jam" when int.TryParse(p[1], out int mount):
                return Sim.DamageSystem.Jam(ship, mount, p.Length > 2 && double.TryParse(p[2], inv, out double min) ? min : 0);
            case "wreck" when Kind<Shipgen.ComponentKind>(p[1]) is { } kind:
                int nth = p.Length > 2 && int.TryParse(p[2], out int i) ? i : 0;
                var state = p.Length > 3 && Kind<Sim.Combat.PartState>(p[3]) is { } s ? s : Sim.Combat.PartState.Destroyed;
                return Sim.DamageSystem.WreckKind(ship, kind, nth, state);
            case "burn" when double.TryParse(p[1], inv, out double bx):
                return Sim.DamageSystem.Burn(ship, bx);
            case "sink" when Kind<Sim.Combat.LossKind>(p[1]) is { } how and not Sim.Combat.LossKind.None:
                return Sim.DamageSystem.Sink(ship, how, p.Length > 2 && int.TryParse(p[2], out int served) ? served : -1);
        }
        return null;
    }

    /// <summary>-search=entity:pattern[:leg nm[:nm apart[:legs]]]: a search pattern for a dev ship, from where it is,
    /// along its heading, to starboard. A ship that guides a -group= steams it for its group, and one that guides a
    /// -force= for its force (the dev groups are made first, in order, so the n-th is group n; the forces after them).</summary>
    static bool DevSearch(Map.WorldSession world, string arg, List<string> devGroups, List<string> devForces)
    {
        var p = arg.Split(':');
        if (p.Length < 2 || !int.TryParse(p[0], out int e))
            return false;
        Sim.Ai.Admirals.SearchPattern? pattern = p[1] switch
        {
            "ladder" => Sim.Ai.Admirals.SearchPattern.Ladder,
            "square" => Sim.Ai.Admirals.SearchPattern.ExpandingSquare,
            "sector" => Sim.Ai.Admirals.SearchPattern.Sector,
            "patrol" => Sim.Ai.Admirals.SearchPattern.PatrolLine,
            _ => null,
        };
        var s = world.Current;
        int k = s.Ships.IndexOf(e);
        if (pattern is not { } pt || k < 0)
            return false;
        double Nm(int i, double d) => p.Length > i && double.TryParse(p[i], System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : d;
        var (at, f) = s.Ships.PoseAt(k, s.Time.Ms);
        var wps = new Sim.Core.Vec3d[world.Settings.RouteCapacity];
        int n = Sim.Ai.Admirals.SearchPatterns.Waypoints(pt, at, Sim.Core.Earth.Heading(at, f), Nm(2, 20) * 1852, Nm(3, 5) * 1852,
            (int)Nm(4, pt == Sim.Ai.Admirals.SearchPattern.Sector ? 3 : 6), true, wps);
        int guided = devGroups.FindIndex(g => g.Split(':')[0].Split(',')[0] == p[0]);
        int forced = guided >= 0 ? devForces.FindIndex(f => f.Split(':')[0].Split(',')[0] == (guided + 1).ToString()) : -1;
        var unit = forced >= 0 ? Sim.Ai.Unit.Group(devGroups.Count + forced + 1) : guided >= 0 ? Sim.Ai.Unit.Group(guided + 1) : Sim.Ai.Unit.Ship(e);
        Sim.Ai.Admirals.SearchPatterns.Orders(world.Ai.Orders, unit, wps.AsSpan(0, n), Sim.Ai.Admirals.SearchPatterns.Loops(pt), c => world.Send(c));
        return true;
    }

    /// <summary>-group=e1,e2,...[:formation[:metres]]: a dev group, guided by the first.</summary>
    static IEnumerable<Sim.Command> DevGroup(string arg)
    {
        var p = arg.Split(':');
        var ships = p[0].Split(',').Select(x => int.TryParse(x, out int e) ? e : 0).Where(e => e > 0).ToArray();
        if (ships.Length == 0)
        {
            Console.Error.WriteLine($"Bad group: -group={arg} (want -group=1,2,3[:column|abreast|echelon-s|echelon-p[:metres]])");
            yield break;
        }
        var f = (p.Length > 1 ? p[1] : "column") switch
        {
            "abreast" => Sim.Formation.LineAbreast,
            "echelon-s" => Sim.Formation.EchelonStarboard,
            "echelon-p" => Sim.Formation.EchelonPort,
            _ => Sim.Formation.Column,
        };
        double m = p.Length > 2 && double.TryParse(p[2], System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 600;
        yield return Sim.GroupSystem.Create(ships[0], f, m);
        foreach (int e in ships.Skip(1))
            yield return Sim.GroupSystem.Join(e, ships[0]);
    }

    /// <summary>-force=g1,g2,...[:formation[:metres]]: a dev force of groups (ids: the -group= flags' in order, from 1),
    /// guided by the first; line abreast 3 km apart by default.</summary>
    static IEnumerable<Sim.Command> DevForce(string arg)
    {
        var p = arg.Split(':');
        var ids = p[0].Split(',').Select(x => int.TryParse(x, out int g) ? g : 0).Where(g => g > 0).ToArray();
        if (ids.Length == 0)
        {
            Console.Error.WriteLine($"Bad force: -force={arg} (want -force=1,2[:abreast|column|echelon-s|echelon-p[:metres]])");
            yield break;
        }
        var f = (p.Length > 1 ? p[1] : "abreast") switch
        {
            "column" => Sim.Formation.Column,
            "echelon-s" => Sim.Formation.EchelonStarboard,
            "echelon-p" => Sim.Formation.EchelonPort,
            _ => Sim.Formation.LineAbreast,
        };
        double m = p.Length > 2 && double.TryParse(p[2], System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 3000;
        yield return Sim.GroupSystem.Form(ids[0], f, m);
        foreach (int g in ids.Skip(1))
            yield return Sim.GroupSystem.EnterAlongside(g, ids[0]);
    }

    /// <summary>-captain=entity:order[:target[:metres]]: an order for a dev ship's captain (a ship in a group's line is
    /// taken out of it, as any order to it alone).</summary>
    static Sim.Command? DevCaptain(Sim.Ai.AiSystem ai, string arg)
    {
        var p = arg.Split(':');
        if (p.Length < 2 || !int.TryParse(p[0], out int e))
            return null;
        int target = p.Length > 2 && int.TryParse(p[2], out int t) ? t : 0;
        double metres = p.Length > 3 && double.TryParse(p[3], System.Globalization.CultureInfo.InvariantCulture, out double m) ? m : 0;
        var o = ai.Orders;
        var ship = Sim.Ai.Unit.Ship(e);
        return p[1] switch
        {
            "follow" when target > 0 => o.Follow(ship, Sim.Ai.Unit.Ship(target), metres),
            "run" when target > 0 => o.Run(ship, Sim.Ai.Unit.Ship(target)),
            "shadow" when target > 0 => o.Shadow(ship, Sim.Ai.Unit.Ship(target), metres),
            "keep" => o.Hold(ship),
            _ => null,
        };
    }

    /// <summary>-scenario='s file: the path as given, else with ".json" added, else a shipped scenario of that name
    /// (Content/Scenarios next to the exe). What's not found comes back as given.</summary>
    static string FindScenario(string arg, out string looked)
    {
        var tried = new[] { arg, arg + ".json", Path.Combine(AppContext.BaseDirectory, "Content", "Scenarios", Path.GetFileNameWithoutExtension(arg) + ".json") };
        looked = string.Join(", ", tried.Select(Path.GetFullPath));
        foreach (var p in tried)
            if (File.Exists(p))
                return p;
        return arg;
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
