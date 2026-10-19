# Fleetwright

Fleetwright is a naval game written in C# on .NET 10, rendered with the SDL3 GPU API (via ppy.SDL3-CS). The exe opens the world (the globe of water on its sim thread, `Map/`), with the ship designer and the hitbox, ship and profile viewers (test tools for the ship generator) as further tabs, in a Dear ImGui UI; game code builds on top of it.

## Build and test

```bash
dotnet build          # whole solution (Fleetwright.slnx)
dotnet test
```

## Run: the designer and the viewers

```bash
dotnet run --project src/Fleetwright                                       # the world: the globe, its clock running at 1x
dotnet run --project src/Fleetwright -- -map=56.7,5.8,0.1 -time=1916-05-31T15:48 [-speed=30000] [-paused]   # map camera: lat,lon,m/px; world start UTC
dotnet run --project src/Fleetwright -- -ships=dreadnought:9 -map=56.51,4.02,3 [-select=1]   # dev ships in the North Sea (sprites close, icons far), every other turning to 135°
dotnet run --project src/Fleetwright -- -ships=dreadnought:1:0@59.03,5.50,90,15 -ships=destroyer@59.035,5.5,80 -select=1 -map=59.02,5.6,8 -speed=30   # dev ships placed (lat,lon,heading[,kn]; repeatable): onto Norway, sliding round a headland
dotnet run --project src/Fleetwright -- -ships=dreadnought:1:0@56.5,4.0,0,0 -ships=dreadnought:1:0@56.5,4.13,0,0 -fire=1:56.5,4.13 -map=56.5,4.12,2 -select=1   # a dev salvo (ship[:battery][:ap|common|he]:lat,lon; by the magazine order without a kind); the strip's Fire row arms right-click and picks the kind
dotnet run --project src/Fleetwright -- -ships=lion:1:0@56.5,4.0,0,0 -ships=destroyer:1:1@56.5,4.04,0,0 "-loadout=1:main:he|secondary:RN 1906 light HE (lyddite)" -engage=1:2 -nopause -speed=10   # a dev ship's loadout (docs/battle/shells-armour-damage.md S5): ship:battery:shell[@share][+shell...], | between batteries; a shell a template's name, a block of the design's, or ap|common|he (the battery's own); a scenario's ship or unit takes "loadout"
dotnet run --project src/Fleetwright -- -ships=super_dreadnought:1:0@56.5,4.0,0,0 -ships=super_dreadnought:1:1@56.5,4.23,0,15 -engage=1:2 -select=1 -speed=30 -map=56.5,4.0,0.45   # fire control: ship[:battery]:target engages (the strip's Fire row: engage, then right-click a ship or contact; cease)
dotnet run --project src/Fleetwright -- -ships=dreadnought:2 -captain=2:follow:1:800 -select=2   # a captain: follow:target[:m], run:target, keep, none
dotnet run --project src/Fleetwright -- -ships=super_dreadnought:1:0@56.5,4,0,10 -hole=1:stern:-1:3 -flood=1:engine_room:1 -jam=1:0 -wreck=1:funnel -burn=1:20 [-sink=1:foundered] -select=1 -map=56.5,4,0.3   # dev damage: holes, flooded spaces, a jammed mount (-jam=ship:mount:min clears after min minutes), a wrecked part, a fire, going down
dotnet run --project src/Fleetwright -- -ships=dreadnought:6 -group=1,2,3,4,5,6:column:500 -select=1   # a group: column, abreast, echelon-s, echelon-p
dotnet run --project src/Fleetwright -- -ships=dreadnought:4:1 -group=1,2,3,4:column:500 -search=1:ladder:10:3:6 -speed=1000   # a search on rails (ladder, square, sector, patrol); :1 the side
dotnet run --project src/Fleetwright -- -scenario=squadrons [-nopause] [-speed=10]   # two squadrons fight (fire at will, Fight); -nopause: the player's encounters don't pause the clock
dotnet run --project src/Fleetwright -- -scenario=flotilla -nopause -speed=30   # torpedo boats attack a battle line (docs/battle/fighting.md 4.6)
dotnet run --project src/Fleetwright -- -ships=super_dreadnought:1:0@56.5,4.0,0,0 -ships=destroyer:1:1@56.5,4.04,0,0 -torpedo=2:1:4 -nopause -speed=10 -map=56.5,4.02,2.2   # a dev spread: ship:target[:n], aimed at the truth (the strip's "torpedoes at": right-click a ship)
dotnet run --project src/Fleetwright -- -scenario=jutland [-map=56.82,4.63,4]   # a scenario (Content/Scenarios; also the world tab's picker, or drop the file on the window): the Jutland, 250 ships
dotnet run --project src/Fleetwright -- -view=designer                    # the designer on bismarck (any designer flag below opens it too)
dotnet run --project src/Fleetwright -- -ship=shipgen/designs/yamato.json [-new] [-section=0-7] [-units=imperial] [-steps=speed:-2,belt:+3]
dotnet run --project src/Fleetwright -- -screenshot=30 [-output=ship.png]   # save frame 30 (default screenshot.png), exit
dotnet run --project src/Fleetwright -- -screenshot=30 -ui [-size=1920x1080] # the window as seen, ImGui included
dotnet run --project src/Fleetwright -- -view=hitbox [-camera=bow|quarter|side|internal|plan|ahead|astern] [-show=all|outside|internal|rooms|armour] [-clip=AFT,FORE[,port][,cut]] [-hitray]   # -clip: the clip box at start (the view framed to it); -hitray: a click fires a ray through the tracer
dotnet run --project src/Fleetwright -- -view=hitbox -camera=internal -show=internal -clip=-40,40,port "-hitshot=10,0,0,25,90,0,ap,at=6"   # a shell fired at start through ship point X,Y,Z from RANGE_KM and ASPECT (° off the bow)[,battery index[,ap|common|he]][,at=S: playback held at S s] (HitboxScene.Shot.cs)
dotnet run --project src/Fleetwright -- -view=ship [-navy=kure] [-era=wwii]
dotnet run --project src/Fleetwright -- -view=profile                      # the ship card's side view
dotnet run --project src/Fleetwright -- -lights=roughness:0.5,specular:0   # the ships' light table knobs (ShipLight.cs: roughness scales the looks' materials), for side-by-side captures
dotnet run --project src/Fleetwright -- -view=ship -viewer=sun:300:30,shade:normal,at:10:0:8 [-outline=0.5]   # the viewer for captures: sun, moon, debug view (lit, albedo, normal, ao, ...), centre and zoom; the plan's ink weight
dotnet run --project src/Fleetwright -- -uiscale=1.25                     # UI size over the window-height scale (1080 px = 1.0)
dotnet run --project src/Fleetwright -- -worldview=4                       # the map's table-to-world boundary, m/px at 1080p (Map/WorldView.cs; live in the "where" panel with F2)
dotnet run --project src/Fleetwright -- -map=-40,100,2 -time=1916-05-31T05:20 -sea=lowsunrelief:4,nightrelief:5   # the sea's look knobs (Map/SeaView.cs: relief, lowsunrelief, facet, chop, nightrelief, moonnight, starnight, debug (3: the wakes, 4: their foam), wake (the wakes' gain, 0 none), foam (the trails' foam lanes' gain, 0 none)), for side-by-side captures
dotnet run --project src/Fleetwright -- -ships=dreadnought:1:0@56.5,4.0,0,0 -ships=dreadnought:1:0@56.5,4.13,0,0 -fire=1:56.5,4.126 -map=56.5,4.126,0.5 -splash=dye:auto,pitch:45   # the world view's splashes (docs/visuals/splashes.md; Map/Splashes/SplashLook.cs: pitch, floor, knee, power, keep, faint, steps, vivid, vary, on, parts, dye:auto)
dotnet run --project src/Fleetwright -- -ships=super_dreadnought:1:0@56.5,4.0,0,0 -ships=super_dreadnought:1:1@56.5,4.05,0,15 -engage=1:2 -nopause -speed=30 -map=56.5,4.0,0.5 -weather=180,12 -flash=at:0.12 -screenshot=900   # the world view's muzzle flashes, blasts and gun smoke (docs/visuals/muzzle-flashes.md; Map/Flashes/FlashLook.cs: gain, halo, haloscale, knee, peak, offset, scatter, rh, air, frames, pvis, psat, frost, scour, scourk, parts (1 flashes, 2 lights, 4 blast, 8 smoke), on; at:S and blastat:S stills of the newest salvo at that age); -weather=wind_from,kn[,visibility_km] the dev world's weather)
dotnet run --project src/Fleetwright -- -ships=bismarck:1:0@56.5,4.0,90,0 -sink=1:magazine[:mount] -map=56.5025,4.0,1.5 -weather=270,8 -magazine=at:15 -screenshot=40   # the world view's magazine explosions (docs/visuals/magazine-explosions.md; Map/Magazines/MagazineLook.cs: scatterd, scatters, fadefrom, fadeto, skin, cold, hot, soot, opacity, spin, churn, firefade, fireevery, firesmoke, steam, gain, whiten, light, steps, parts (1 body, 2 shadow, 4 jets, 8 debris, 16 light, 32 after-smoke); at:S a still of the newest at that age after the hit)
dotnet run --project src/Fleetwright -- -ships=super_dreadnought:1:0@56.5,4.0,0,0 -ships=destroyer:1:1@56.5,4.04,0,0 -torpedo=2:1:4 -nopause -speed=10 -map=56.5,4.03,1 -tracks=at:40 -screenshot=700   # the world view's torpedoes (docs/visuals/torpedoes.md; Map/Torpedoes/TorpedoLook.cs: fast, slow, life, width, spread, dive, gain, floor, slick, body, bodyfloor, on; at:S every torpedo drawn S s after the newest launch)
dotnet run --project src/Fleetwright -- -ships=dreadnought:1:0@56.5,4.0,90,0 -ships=super_dreadnought:1:0@56.56,4.0,0,0 -hitbench=1:2:3[:SHOT] -nopause -map=56.4995,4.0,0.1 -weather=200,10 -hit=at:0.15 -screenshot=300   # the world view's shell hits (docs/visuals/hits-and-fires.md; Map/Hits/HitLook.cs: scale, open, flash, flashs, through, inside, vent, reach, smoke, ventsmoke, dust, light, pieces, marks, piecemm, fallen, hole, scorch, boil, parts (1 fireballs, 2 plate flashes, 4 vents, 8 lights, 16 smoke, 32 what flies off, 64 marks), on; at:S the newest hits at that age); -hitbench=TARGET:SHOOTER[:every_s[:shot]] strikes the shooter's shells at a still ship in turn (DamageSystem.Shoot: the belt, the upper works, the thin ends, the deck, a turret, a secondary's), each hit's outcome printed
dotnet run --project src/Fleetwright -- -ships=dreadnought:1:0@56.5,4.0,90,0 -ships=super_dreadnought:1:0@56.56,4.0,0,0 "-readiness=0:prepared, cluttered" -firefighting=0:unorganised-1890 -hitbench=1:2:15:1 -nopause -speed=30 -map=56.5,4.0,0.15 -time=1916-05-31T23:30   # the world view's fires (docs/visuals/hits-and-fires.md I4-I5; Map/Fires/FireLook.cs via -fires=: height, puff, flare, lean, kelvin, light, parts (1 flames, 2 lights, 4 smoke), on); -readiness=SIDE:ORDERS and -firefighting=SIDE:DOCTRINE a side's orders before action for fire and its firefighting (settings.json's FireReadiness and Firefighting), for fire benches
dotnet run --project src/Fleetwright -c Release -- -wakebake=destroyer:16,26,39 [-output=wakes]   # a design's wake bakes (docs/visuals/wakes.md K1) as PNGs: height and lit slopes, bow right, starboard down; exits
dotnet run --project src/Fleetwright -- -section=2 -designer-ui=secsel:1:2,secmenu  # UI state for screenshots (DesignerScene.ApplyDevUi)
dotnet run --project src/Fleetwright -- -section=1 -input=60:click:597,539;80:wheel:1160,559:-1;100:type:150  # scripted clicks, wheel, typing, F:drop:PATH, F:drag:X0,Y0:X1,Y1, F:mclick or F:mright:X,Y (SDL clicks the map gets) (window px; Engine.ScriptedInput)
dotnet run --project src/Fleetwright -c DevRelease -- -frametimes -screenshot=1100 -input=100:pan:900,500:400,350:80   # frame times at exit; F:pan is a real SDL drag (the map's input)
dotnet run --project src/Fleetwright -c DevRelease -- -map=56.5,4,2 -gputime -frametimes -screenshot=600   # the scene's GPU time: drawn in a command buffer of its own and waited for (the display's pacing left out)
```

The world tab (`Map/`, `docs/battle/plan.md` step 1.2) is first and the default: `WorldSession` owns the world and
its sim thread for the app (the engine's `FrameStarted` reports real time every frame, whatever tab is open), and
`MapScene` draws the globe from orbit to 10 px/m (`MapCamera` and `MapView`: straight down, north up, zoom in metres
per pixel; projection, picking, the grid and the land's clip in double, so nothing jitters), the land from
`Content/World/` (Natural Earth, a rough first Earth: `LandView`), with the clock's controls (space pauses,
+ and - change speed). Wheel zooms at the cursor, left drag pans. Ships (`MapScene.Ships.cs`, step 2.3a): a click
selects, the strip at the bottom orders course and speed, right-click steers for a point, shift+right-click adds a
waypoint, the search row orders a search pattern; the top-left panel spawns where you click, one ship or a group already on its
stations (count, formation, spacing, heading, speed, side), the designer's design as it is now ("From designer", the
default: an edit spawned is a new class, ships already out keep theirs) or a shipped one; pressed and dragged, they head
the way of the drag (a ghost shows them until the release), and a spawn looks round at once (`ShipSystem.LookNow`). `ShipBatchRenderer` draws them inside the
globe's pass (sprites from `ShipArtLibrary`, a stand-in icon when small). Without `-view`, a designer-only flag (`-section`,
`-steps`, `-new`, `-designer-ui`, `-units`) opens the designer instead.

The other four tabs are on one design (menu bar, or F3 to step): the designer, the hitbox viewer, the
ship viewer and the profile. `-ship=` picks the design; without it, it is `Content/Designs/bismarck.json` next to the exe (a game
asset, below). `-ship=` also takes the path without `.json`, or just a shipped design's name (`-ship=yamato`). All of them show `DesignSession`'s design: an edit in the designer shows in the viewers, and a design
picked in the ship viewer's list opens in the designer.

The designer (`Designer/`, `docs/designer-ui/designer-plan.md`) edits the design, or the empty 8 kn hull with `-new`,
with knobs, templates (plant, hull construction, crew standard, armour schemes, shell families, material names) and a style (View
menu: navy, era, pennant number), tried on every view or kept in the design. The layout is the dense one in
`docs/designer-ui/bureau.html`: outliner and templates, plan and profile with overlays (A R M T), section tabs (0-7),
ledger, particulars and remarks. The name and type label edit in place in the resource bar. The Main battery tab is its own pane (`docs/designer-ui/mainbattery.md`): a battery table, a
clickable turret arrangement (`+` slots add, the step between turrets toggles superfiring, right-click for battery,
move and split), guns bearing by bearing, and presets. The Secondary · AA · TT tab is too
(`docs/designer-ui/secondary.md`): a battery table whose position (deckhouse, deck, upper or lower casemate) is one
click, the broadside strip (a half-plan over one schematic track per position, a + at each track's head), the
secondaries' guns bearing, and AA, torpedo and selected-battery panels. Both battery tables have a SHELLS column (`docs/designer-ui/shells.md`): the
loadout's kinds and shares, and a picker for the family (a tree by navy, the design's first) and each load's block and
share; the Templates panel's Shells row sets every battery's family. Its Guns, Torpedoes, Shock and Outfit rows pick
the design's tech blocks from their catalogues, a tooltip giving each entry's numbers for this design
(`docs/designer-ui/tech-blocks.md`, `TechEdits`: one gun block for every battery, copied in, the unnamed dropped), and the
Fire control tab each battery's fit (preset, optics, computer, mount) and sights, plain rows until its own tab. The Hull & upperworks
tab sets the masts (kind, top, its director, height, tiers), the joints and doors below. The Protection tab (`docs/designer-ui/protection.md`,
the layers in `protection-layers.md`) is the elevation (the side deck by deck, heights stretched, every built plate, `+`
ghosts where a missing part goes, Rooms and Compartments toggles, two flags for the sections' cuts), two half-sections at
their own stations to scale (`ProtectionSection`, from the build's cells, walls and armour; a Whole section toggle locks
them), a table of the parts the ship has with sub-rows for zones and slopes (a `+` in each group's header adds one with
values from the belt, `x` removes), the selected plate's facts and Shape, and the sub-bar's coverage presets, Layout
templates and Materials; brass handles drag belt edges and lean, reach, decks, a slope's knee and the torpedo, wing and
inner walls, and a drag edits once, on release. Its dev hooks: `-designer-ui=protsel:ID,protcut:K:STOP,protlink,protlayout,
prottip:layout:N,protcomp,protrooms:0` (`DesignerScene.ApplyProtDevUi`). The Overview tab (`docs/designer-ui/overview.md`) is a trim sheet of
18 broad-stroke levers, each with what one step down and up does to the standard displacement, measured by real builds
in the background (`DesignProbes`: validate and build only, on several threads, a newer batch supersedes); limits
(mocked in `DesignLimits` until the gameplay layer hands them in) only report, never refuse; Fit is greyed out until the
autodesigner. `-steps=` applies knob steps at startup (knob ids from `Knobs`: `speed:-2`, `topside:tumblehome`,
`distiller:off`), for screenshots of edited states in any view. Saves go to Documents/My Games/Fleetwright/Designs.
The game uses the designer's other constructor: a design in, the edited one out through a callback, with a worker of
its own.

The hitbox viewer (`-view=hitbox`) shows the design's hitbox model in 3D (`HitView/`, the live `hitview.py`). Left
drag orbits, right drag pans, wheel zooms; hovering names a prism and a click selects it and lists its fields. Its
panel picks the view, the projection, which kinds are drawn, and a clip box: whole prisms that overlap it (a tier
button gives that tier's rooms, `hitbox_cells.png` live) or a per-pixel cut. A click selects, fires a ray through the tracer, or fires a
shell (`HitboxScene.Shot.cs`): through the point clicked from the range and the view's bearing (or a fixed aspect), by
this ship's or another design's battery, through the world's own damage system on a ship of the design (`ShotRange`,
with a `TerminalTrace` listening to `Terminal.Resolve`). The Shot window lists the walk plate by plate (obliquity,
speed in and out, V_L, what it made of the plate), the fuze, the burst and what the hit did (parts made worse, holes,
fires), and plays it back over the view; a seed, again and re-roll, and a ship that keeps her damage across shots.
`docs/battle/hit-view.md` has what's next (battle hits replayed, the effects' own trace, a hit-cam for players).

The ship viewer (`-view=ship`) shows the design from the baked textures (mips, turrets through their arcs,
height-map shadows), lit as on the map; its panel switches design, look, mip level, turrets, sun and moon, the
lighting's debug views (albedo, normal, AO, ...) and the light table. Wheel zooms, left drag pans.

The profile (`-view=profile`, `ProfileScene`) is the ship card, a poster of the session's ship in its look: the name,
the plan (the sprite, turrets at rest, no shadow) over the profile (`ShipProfile`) at one scale with a scale bar, the
recognition silhouette small, and the headline figures in big type over the particulars (`ShipCard`), in the designer's
units (`DesignSession.Units`). No controls: the design and look are picked in the designer or the ship viewer. Right-click
saves the poster as a PNG in Documents/My Games/Fleetwright/Cards (`FrameCapture`: a scene asks, the engine captures
the window as seen a couple of frames later, cropped to the poster). The profile is made only while the tab is open,
off the render thread with a baker of its own, at the scale nearest above the poster's.

F2 shows the stats window, Escape quits. `-screenshot` captures the scene alone; with `-ui` it is the window as seen,
ImGui on top. Both go through the window's blit shader, and a capture run reads no `imgui.ini` and writes none, so
the UI shows its defaults. `-size=WxH` sets the window size.

## Release and game assets

Three configurations:

- **Debug** - the everyday build: console, Tracy.
- **Release** - what players get: optimized, no console window (`WinExe`), no Tracy (`TracyWrapper` isn't referenced
  and `ProfilerStubs.cs` stands in for its calls as no-ops). Console output goes nowhere; there's no log file yet.
- **DevRelease** - for us: optimized like Release, with the console and Tracy (the `TRACY` define).

```bash
release.bat              # release/Fleetwright/            (gitignored)
release.bat DevRelease   # release/Fleetwright-DevRelease/
```

Each holds Fleetwright.exe, the .NET runtime, the native DLLs and the game assets: a self-contained win-x64 publish
that runs on a stock Windows 11 with nothing installed, except the VC++ runtime (`vcruntime140`, `msvcp140`;
SDL3_image and TracyClient import them), which is assumed present.

Game assets are the data the game ships with, listed as `GameAsset` items in `src/Fleetwright/Fleetwright.csproj`:
each lands in the build and publish output under its `Link` path and is read through `AppContext.BaseDirectory`, so a
dev build and a release find it the same way. Today the assets are the designs, `shipgen/designs/*.json` ->
`Content/Designs/` (not `fuzz/`, which is test data). Code must not reach into the repo for data a release needs.

## Shipgen CLI

The ship generator's command line, the port's test harness (`PORTING.md`):

```bash
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- golden-check          # every golden case, ~25 s
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- golden-check bismarck fuzz_lim_*
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- golden-update         # after a deliberate output change
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- snapshot --out DIR    # every case's output as plain files: diff -r two of them to prove a refactor byte-identical (the goldens allow 1e-9)
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- design shipgen/designs/bismarck.json --out out --previews
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- validate shipgen/designs/*.json
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- bench bismarck yamato
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- draw shipgen/designs/bismarck.json --out out   # sprite.json + SVGs
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- bake shipgen/designs/bismarck.json --out out   # PNGs on the GPU
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- profile shipgen/designs/bismarck.json --out out [--scale 10]   # the side: profile.png, silhouette.png + SVGs
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- png-check             # bake all, IoU vs Python's PNGs (turrets; hulls retired)
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- svg-check             # every case's SVGs, ~20 s
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- fuzz shipgen/designs/*.json --cases 1600   # robustness
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- verify out_designs/*  # sprites vs hitboxes
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- sprite-check          # every design's sprite.json
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- shell-test shipgen/designs/bismarck.json [--gratings] [--underwater 4] [--path 5] [--dump DIR]   # shells at the citadel through the hit query's tracer: leaks through neither armour nor water
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- mono-check [bismarck ...] [--steps 4]   # trim levers swept: more weight in, never a lighter ship
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- sim [--from 1916-05-31T14:00] [--speed 30000] [--seconds 3]   # the world on its thread: clock, speed asked/achieved, sun, sim allocations
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- shore [--points 100000]   # the land index: build time, memory, IsLand vs Land, query costs
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- trials shipgen/designs/yamato.json [--out out]   # sea trials on the ship model: turning circles, 0 -> full, coasting, crash stop, beside the research's bands
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- scenario Content/Scenarios/squadrons.json [--minutes 30] [--every 2] [--no-land] [--watch 1,5] [--replay]   # a scenario headless: speed, probes, hash, and the fight (who fired at whom, hits)
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- scenario Content/Scenarios/tsushima.json --play [--seeds 5]   # played to its end with the AI on every side (docs/battle/command.md 5.0, the commanders 5.1d): the timeline (the commanders' phases and orders too), the result and the AI layers' cost; or a line a seed and the spread
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- duel super_dreadnought [lion] [--range 14630] [--fit 3|argo|design] [--discipline german] [--runs 20] [--minutes 15] [--trace] [--damage]   # fire control's bench: one battery on a ship, run after run; hit and straddle %, time to first hit, the fire discipline (ranging, fire for effect, the spotter's calls), cost a battery-second (hits do nothing unless --damage); --trace each salvo's phase, spot and bracket
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- ballistics dreadnought [--research]   # each battery's range table (range, elevation, time of flight, fall, striking velocity); the research's 16"/50 beside its sourced figures
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- templates   # the shell, explosive, material, fittings, outfit and fire-control catalogues (ShellTemplates.cs, Explosives.cs, MaterialTemplates.cs, Fittings.cs, Outfits.cs, FireControlTemplates.cs) out to Content/Templates/shells.json, explosives.json, materials.json, fittings.json, outfits.json, fire-control.json and docs/shipgen/*-templates.md
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- upgrade shipgen/designs/*.json Content/Scenarios/Designs/*.json [--materials] [--dry]   # design files rewritten to the current format (DesignFormat) in their own layout; --materials also refreshes their materials tables from the catalogue
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- fit shipgen/designs/kgv.json --main table [--secondary "Stereo, long base (O3)|Admiralty Fire Control Clock|Director, power drive"] [--main-sights "Telescopic sights, fired on the roll"] [--dry]   # batteries' fire-control fits set (docs/battle/fire-control.md F3): a preset's name or tech blocks joined by |; --BATTERY-sights the sights its guns lay by in local control; fire_control_tech follows
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- sight lion derfflinger [--from lion]   # sighting facts as the sim reads them (docs/battle/fire-control.md F4d): each class's eyes (kind, height, horizon, rangefinder, blind arc), silhouette by height, how far a lookout (--from's highest eye) makes it out and first sights it closing, the world's derived reach
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- weapons dreadnought [--research]   # weapon facts: batteries, fire-control level, guns' working numbers, shells, plating (placeholders beside the research's guns)
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- hits v25 --shooter lion --battery 1 --range 6000 [--kind he] [--n 2000] [--per-ship 10] [--show 3] [--settle 30] [--readiness "closed up"] [--firefighting rn-1916]   # the damage model alone: hits spread over a design as falling shells see it, through the damage system; outcomes, parts lost, killed and wounded (16's hit classes, flashes aside, the shape), ships' state after K hits and MIN minutes of flooding and fire (the fires still burning, the crew out), the target's orders before action for fire by --readiness (docs/battle/fire.md B1) and her crews' firefighting by --firefighting (B4)
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- scheme belt_and_slope dreadnought [--write]   # a protection scheme (docs/shipgen/protection-templates.md) on designs: displacement before and after; --write rewrites the design's armour
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- subdivision destroyer yamato [shipgen/designs/*.json] [--rooms]   # the main compartments (docs/shipgen/protection-plan.md H7): sections, the longest, how many adjacent ones she floats with flooded, the worst run that sinks her
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- protection v25 destroyer [shipgen/designs/*.json --summary] [--shells 400]   # what stands between the sea and each magazine (docs/shipgen/protection-plan.md H0): crown on the waterline, the layers in from the side and down from above, shells at 5-35° falls (sea first, steel before it, open)
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- pen-check   # the plate model alone (Thompson's limit and its inverse, the materials' q; the early trials with the shells' caps and shatter) against research/simulation/armor/07-*.csv: range tables, NPG 1946-47, the Italian obliquity tables, the early trials, Okun's M79 decks; no rolls
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- frag ["US 1941 HC (Explosive D)" --calibre 406]   # a burst's pieces (docs/battle/shells-armour-damage.md E1, Fragments.cs): Gurney, Mott, drag and THOR against damage/13's anchors (Okun's 8" HE table, WDR 51's speed and splinter knee, Boise), or one template at a calibre
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- burst [destroyer --room accommodation[:n] --filler 9.7 --explosive "Explosive D" --calibre 203]   # a burst's gas in a closed space (docs/battle/shells-armour-damage.md E5, Blast.cs): the cascade over the cell graph, what ruptured, the doors blown, the spaces reached and what each felt; with no charge, damage/11's cases on our ships
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- nearmiss destroyer --shooter yamato --kind he [--at "short:1,7,15;over:7;beside:10;under:5" --n N --settle MIN] [--salvos N --fc-range M --fc-defl M] [--water-ms MS]   # near misses (docs/battle/shells-armour-damage.md E7, NearMiss.cs): a burst placed in the water off the target (splinters through her side and upper works, waterline holes, men, the shock factor, gear out, sprung seams) or salvos with the fire control's error, through the world's damage system
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- matchup arethusa:4 iron_duke [--range M] [--share 0.6] [--sight M]   # who can fight whom (command.md 5.1g): profiles, hit and out chances, the engagements, A's confidence; NAME:N:t0 strips torpedoes
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- torpedo super_dreadnought [--shooter v25] [--at mid|bow|stern|SHARE] [--n 200] [--per-ship K] [--settle 60] [--range M --aspect DEG --spread N --runs N]   # torpedoes: the hit alone (outcomes, water, list, lost and how), or with --range the attack (hit %)
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- flood destroyer [--hole mid:-1:0.5,bow:-1:2:p] [--rooms boiler_room:2] [--minutes 120] [--every 600] [--step 1000] [--cells]   # the flooding model alone: reserve buoyancy, TPC, GM; holes or flooded spaces, then water, sinkage, trim, heel, GM until it goes down
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- burn   # one space burning unfought (docs/battle/fire.md B0, Fires.Advance): damage/17's T2 spaces beside fire_ref.py's flashover, peak and hours, at a 1 s and a 15 s step; a big space's rooms joining, a weather deck
dotnet run --project src/Fleetwright.Shipgen.Cli -c Release -- fire-history [--runs 20] [--case Suvorov] [--hours 4] [--trace]   # fire's yardstick (docs/battle/fire.md B5; damage/17 §9 T6): the record's cases (Santiago, the Yalu, Tsushima, Jutland; Savo for later cuts), a ship hit by the case's weight of fire over its minutes through the world's damage system and run on for hours: ablaze, all out, still burning, water, how she ended; --trace one run minute by minute
```

`mono-check` (about 15 s) lists where adding weight gave a lighter ship (reversals) or one step jumped far more than its
neighbours (lumps); a sizing change shouldn't add to them. `golden-check` must stay at 372 of 372 for any change to `Fleetwright.Shipgen` that isn't a deliberate fix (fixes
update the goldens on purpose with `golden-update`, one per commit, and bump `ShipDesign.Version`, so compiled ships
know they're stale). `svg-check` and `sprite-check` do the same for
`Fleetwright.Shipgen.Render`: a deliberate drawing change rewrites them with `golden-update` too, noted in
`shipgen/golden/README.md`.

The sprites are drawn from the physical model: `TopView` reads every part with a hitbox from `Ship.Hitboxes`, and only
dressing with no physical effect (boats, fittings, hatches, cranes, deck markings) from elsewhere. A look may draw a
part a little over or under its hitbox, never move or reshape it (the hull least of all). Don't add geometry the
drawing needs anywhere but the hitbox model.

## Profiling

`dotnet-trace` is installed; `docs/profiling.md` has the recipe (trace a Release exe, summarize with
`python -I tools/speedscope_top.py X.speedscope.json [--focus Method]`), allocation traces (`tools/alloctop`: the game
is soft real time, so garbage counts), how to read the numbers, the checks after an optimization, and a log of past
findings.

## Project Structure

```
Fleetwright.slnx
release.bat                   # builds release/Fleetwright/ (see "Release and game assets")
Directory.Build.props         # net10.0, nullable, InvariantGlobalization; warnings as errors in Fleetwright.Shipgen*
Directory.Packages.props      # central package versions (PackageReference has no Version)
src/
  Fleetwright/                # the game exe
    Program.cs                #   entry point, command-line parsing
    Map/                      #   the world tab: WorldSession (the app's world and sim thread), MapCamera/MapView
                              #   (double projection), WorldView (the one boundary between the planning table
                              #   and the world: ships' icons or sprites, the grid or its edge ticks, the sea;
                              #   docs/visuals/sea.md), SeaSim (the FFT waves: two compute dispatches and mips) and
                              #   SeaView (the sea's camera-relative frame and clock), GlobeRenderer (the sphere per
                              #   pixel, the sea in the world view, then MapLines), Graticule,
                              #   LandView (the finest land level, brought inside a cap around the view per frame),
                              #   LandMesh (the coarse levels baked once: tiled fans and a coast mesh), CapRetraction,
                              #   MapScene (input, clock controls, HUD; MapScene.Ships.cs: selection, orders, dev
                              #   spawn; MapScene.Groups.cs: ctrl+click picks, forming groups, labels, collapsed
                              #   markers; MapScene.Damage.cs: fires, wrecks, losses, what contacts show), ShipArt (ShipArtLibrary: each class's sprites drawn on the class
                              #   registry's threads, uploaded here), ShipBatchRenderer (ShipDrawList on the CPU,
                              #   instanced quads in the globe's pass: sprites, shadows, icons; damage drawn through the height map;
                              #   the wakes' stamps into the wake target before it), Wakes/ (docs/visuals/wakes.md: WakeBake, the
                              #   Kelvin field by FFT from the hitbox model's waterline; WakeLibrary, bakes shared by hull size
                              #   and speed bucket, baked on threads of its own and pooled on the GPU; WakeTrail, each ship's stern path:
                              #   the table's line and the world's foam lane, FoamTrails), Splashes/ (docs/visuals/splashes.md:
                              #   SplashBook, the splashes seen and their display clocks; SplashRenderer, columns through the
                              #   3/4 effects camera, shadows, foam into the wake target, dye stains; SplashLook, the knobs),
                              #   Flashes/ (docs/visuals/muzzle-flashes.md: flashes, their lights, blasts on the water, gun
                              #   smoke), Magazines/ (docs/visuals/magazine-explosions.md: MagazineBook, MagazineRenderer: the
                              #   fireball and its cloud marched through the effects camera, their shadow; MagazineLook),
                              #   Hits/ (docs/visuals/hits-and-fires.md: HitBook, the shell hits seen; HitMarks, holes on the sprites; HitEffects, bursts,
                              #   plate flashes, vents and smoke through the magazine's body and jets; HitLook)
    Engine.cs                 #   Sdl3GpuEngine: init, resources, render loop, PbrMaterial
    GltfLoader.cs             #   glTF/GLB loader (SharpGLTF), not wired to the command line
    ImGuiRenderer.cs          #   Dear ImGui backend on SDL3 GPU
    Camera.cs                 #   FPS camera (unused; to become the top-down Earth camera)
    DesignSession.cs          #   the picked design and its newest DesignResult, shared by the viewers (they watch Version)
    DesignWorker.cs           #   builds designs off the render thread: ship, hitbox mesh, sprites baked and mipped
                              #   (on the game's device, its own command buffers); the newest request wins
    ShipSpriteRenderer.cs     #   a baked ship sprite into any RenderTarget (hull, turrets at given bearings, shadows)
    ShipLight.cs              #   the ships' lights: LightTable (set by eye, one place), ShipLights (a frame's light, shadows
                              #   by the stronger of sun and moon); shaded by Content/Shaders/Source/Include/shiplight.glsl
    IScene.cs                 #   a full-window view with its own UI; the engine draws the current one (see Views)
    ShipViewer.cs             #   the ship viewer, a test tool for Shipgen
    ProfileScene.cs, ShipCard.cs   # the profile tab: the ship card poster (drawings, silhouette, figures)
    SceneSwitcher.cs          #   several scenes behind one IScene: the menu bar and F3 switch
    UiFonts.cs                #   the game UI's fonts (Content/Fonts) in ImGui's atlas
    Designer/                 #   the ship designer: DesignerScene (design in, design out), DesignDoc (history, save),
                              #   Knobs (every control over the design), Templates, Units, DesignerTheme (the tokens)
    HitView/                  #   the hitbox viewer: HitboxMesh (hitboxes as triangles, CPU), HitboxRenderer (into any
                              #   RenderTarget), HitboxCamera and HitboxViewState (what it shows), HitboxScene (UI)
    ProfilerStubs.cs          #   no-op Tracy stand-ins for Release
  Fleetwright.Gpu/            # GPU helpers for the game exe
    GpuTypes.cs               #   buffers, textures, samplers, DrawContext, SceneNode, MeshNode, materials
    GpuPipelineBuilder.cs     #   fluent pipeline builder
    GpuMath.cs                #   math helpers (perspective, lookAt)
    ShaderTypes.cs            #   vertex layout, uniforms, scene data
    RenderTarget.cs           #   colour (+MSAA resolve) and depth a view draws into: the main target or offscreen
    GpuShader.cs              #   SPIR-V loading from a device alone
    GpuUpload.cs              #   static buffer uploads, and DynamicGpuBuffer (refilled within a frame)
  Fleetwright.Shipgen/        # ship design library (ported, see below). No package references.
    Model/                    #   the typed data: Design (the input), Ship (the output), the JSON context and JsonFile,
                              #   Kinds (the closed sets as enums: rooms, components, extents; the sim shares them)
    Layout/                   #   layout.py: the Layout object, parts, superstructure levels, the warship layout
                              #   (BuildLayout: its stages, each in Warship/ with its inputs and outputs)
    Styles/                   #   the style hooks and the warship, carrier, merchant and planing styles
    Golden/                   #   the golden comparer and case runner
    Tools/                    #   the fuzz mutator
  Fleetwright.Shipgen.Render/ # the drawing side: reads only the built Ship (its hitboxes and dressing)
    TopView.cs                #   the ship from above: the hitboxes' parts in drawing shapes, plus the dressing
    SideView.cs, SideArt.cs   #   the ship from the side (the card's profile), drawn by the plan's rules
    ShipProfile.cs            #   the profile and recognition silhouette as scenes (docs/shipgen/profile-plan.md)
    Dazzle.cs                 #   the dazzle scheme both views paint from, so panels meet across the deck edge
    Data/looks.jsonc          #   every navy and era's colours and shapes (the documented table)
    Looks.cs                  #   resolving a design's look: from-chains, adjust, era muting
    Scene.cs                  #   the display list (paths, circles, rects, lines, text, clipped groups) and SvgWriter
    Painter.cs, HullArt.cs, TurretArt.cs, Clutter.cs   # shipgen.py's drawing and clutter.py
    Sprite.cs                 #   the height map and sprite.json (ShipSprites.Build)
    ShipRng.cs                #   the drawing's seeded RNG (per feature)
    RenderTypes.cs            #   Palette, Shapes and the sprite.json records
    Bake/                     #   the GPU bake: Lower (scene -> triangles), Stroker, Glyphs, GpuBaker (SDL_GPU: colour, height
                              #   map, relief), Relief (ReliefMaps: normals and AO from the relief), PNG, mips
    Golden/                   #   the SVG comparer and the drawing's golden checks
  Fleetwright.Shipgen.Cli/    # `shipgen` command, the port's test harness (and `sim`, an empty world on its thread)
  Fleetwright.Sim.Core/       # the sim's lowest layer, pure values (docs/battle/plan.md "Inside Fleetwright.Sim"):
                              #   Vec3d, Earth (the sphere), ShipFrame (left-handed: +x bow, +y starboard, up),
                              #   WorldTime (ms since 1850), Sun, RandomStream, StateHash, SimSettings, Track (a
                              #   constant-velocity Kalman track on the sphere and a range-and-bearing fix: pictures and
                              #   fire control), CubeIndex (the one "what is near here" grid), Land (rings,
                              #   IsLand), CubeSphere (the cube's faces, gnomonic), Shore (the land index the sim asks:
                              #   IsLand, Clearance, FirstContact along great circles and rhumb lines; World.Shore)
  Fleetwright.Sim.Ships/      # the ships' data: ShipClass (a built Ship's hitboxes and report as read-only, int-indexed
                              #   tables: Rows.cs), ShipClassCompiler (strict: every kind an enum, every link an index),
                              #   ShipSolids and ShipTracer (the one tracer: a segment through a ship into a HitPath),
                              #   ContentHash (design and layout hashes), ShellTest (shell-test on a class, a tracer caller),
                              #   the weapon facts (batteries, shell types, the guns' working numbers, plating), Fragments (a shell's burst:
                              #   Gurney, Mott, drag, THOR; compiled per shell type, queried at the burst),
                              #   DesignerPlaceholders (facts the designer doesn't give yet: the one place they live),
                              #   ShipClassRegistry (classes by design hash, built on threads of its own, ids for commands)
  Fleetwright.Sim.Combat/     # ships at sea: ShipStore (slots, state in columns), ShipState (a handle), Handling (a
                              #   class's numbers for moving), ShipMotion (the step: Nomoto turning, forces on the
                              #   power curve, arcs on the sphere; the helmsman: courses and routes), Slide (ships and
                              #   the land: the feeler slides a ship along a coast at its standoff, the backstop
                              #   refuses land, spawns and waypoints go to sea; docs/world/terrain-plan.md), HitQuery (a world
                              #   segment into a ship's frame and attitude, through the tracer; a miss's fall), Ballistics
                              #   (the shells' flight on the sphere: tables by elevation x speed x density x time, and
                              #   the range table: falls by elevation x speed x muzzle height), CombatStore (each ship's
                              #   mounts as analytic train/elevation anchors, loading clocks, rounds), FireControl (the
                              #   chain's pure models: reading errors, dispersion, laying a gun with its ship's motion, the
                              #   spotting ladder), Condition (Hit, each ship's ShipCondition), Terminal (penetration:
                              #   Thompson's limit and its inverse; the fuze, the outcome), Effects (what a hit wrecks, holes, fire, crew), NearMiss (a missed
                              #   shell's burst in the water: splinters and shock on a ship close by), Underwater (a torpedo's
                              #   explosion at the hull: holes, the TDS, shock, a broken back), TorpedoAim (the intercept,
                              #   the setting), Functions (what the
                              #   ship can still do, from the class's function table), Flooding (FloodModel: the cells as
                              #   water sees them; the sea in, across the cell graph, pumps, settling, founder and
                              #   capsize), Fires (burning, spreading, put out, cooking off magazines), Fitness (what a
                              #   ship can still do; FallOutRule, doctrine's fall-out), Matchup (who can fight whom: a
                              #   class's profile, two sides' engagement and A's confidence; docs/battle/command.md 5.1g)
  Fleetwright.Sim.Ai/         # the AI above the world: AiSystem (layers of command, top-down, each at its rate:
                              #   Commanders/ (CommanderLayer: each side's C-in-C, his aims and phases, tasks to his top
                              #   units by their roles; docs/battle/command.md 5.1d), Tasks/ (TaskLayer: a unit's task
                              #   and its plan, tasks queued one after another), admirals, captains),
                              #   Knowledge/ (IPicture: the navy's picture; SeenContacts: its contacts in sight, gridded;
                              #   TacticalPicture and HeldGroups: the enemy in groups, in sight and held; Matchups and
                              #   MatchupJudge: the matchup's sides from the world, the one judgement every layer asks),
                              #   Captains/ (CaptainLayer, Behaviours: keep course, follow, run, station, fight;
                              #   CaptainLayer.Fire: fire at will, engage, hold fire, torpedoes, a torpedo craft's attack), Admirals/ (AdmiralLayer: formations
                              #   as captains' stations, fire distributed over the enemy's line); admiralties go beside them
  Fleetwright.Sim/            # the world: World (clock, speed, events, commands in, snapshot out), Scheduler,
                              #   IWorldSystem, ShipSystem (every ship, the ship tick, ship commands), GroupSystem
                              #   (groups: members, guide first, formation, spacing), ShellSystem (shells in flight: two events
                              #   each, the danger window through the hit query; splashes), GunnerySystem (engaged batteries:
                              #   measure, track, solve, lay and fire, spot; docs/battle/fighting.md 4.2b), CombatLog (who
                              #   fired, what hit), DamageSystem (hits in: terminal, effects, functions; each wet or burning
                              #   ship's flooding and fire steps; going down; docs/battle/fighting.md 4.3a, 4.3b), TorpedoSystem
                              #   (launches, torpedoes as tracks checked in pieces, hits; 4.6), GivenHits (the hits and torpedo
                              #   benches), ShipIndex (every ship on a CubeIndex, built when asked), SimHost (the sim
                              #   thread), TripleBuffer; no GPU or UI, runs headless
tests/
  Fleetwright.Tests/          # xUnit for the game exe: the hitbox mesh over every design, the map camera's maths
  Fleetwright.Sim.Tests/      # xUnit, headless: the sphere, the sun, the streams, replays and slicing, the sim thread,
                              #   every golden build compiled into a ShipClass, shell-test
  Fleetwright.Shipgen.Tests/  # xUnit: design input, every golden case, concurrent builds, the drawing, the
                              #   bake (trait Gpu: needs a GPU)
shipgen/                      # the port's test data
  designs/                    #   the 71 designs, and fuzz/ (300 mutants)
  golden/                     #   Python's output (README.md there)
Content/                      # copied into the game's output folder; the bake shaders are embedded in Shipgen.Render
                              #   (the output's Content/Designs/ comes from shipgen/designs, see GameAsset)
  Fonts/                      #   UI fonts (OFL), game assets
  World/                      #   the land at 1:110M, 1:50M, 1:10M (game assets), from tools/land_bake.py (README.md)
  Sim/settings.json           #   SimSettings: every tuning number of the sim (game asset; strict, every key)
  Templates/                  #   the designer's template catalogues (game assets); shells, explosives, materials, fittings and fire control from `shipgen templates`, the rest from tools/designer_templates.py:
                              #   plants, hulls, crew from docs/shipgen/*-templates.md; armour from the designs' maps
  Shaders/Source/             #   GLSL sources
  Shaders/Compiled/           #   SPIR-V binaries (checked in)
docs/shipgen/                 # the ship designer: inputs, outputs, conventions, decisions, TODO, its research notes
docs/battle/plan.md           # the battle module (a deliverable: the whole ship sim on the world map + missions):
                              #   what done means, ship counts, the build order from a world of water up, the ships' data
docs/world/architecture.md    # the world: tiers of time on one clock, the sim thread, commands/snapshot, the scene stack
docs/missions/plan.md         # missions, the battle module's stand-in for politics and manufacturing (format, objectives, UI, generator)
docs/visuals/sea.md           # the sea: the table/world boundary, the FFT ocean in the globe's pass (W0-W4 done; foam later)
docs/visuals/wakes.md         # wakes: the Kelvin field baked per class and speed into the sea's slopes, the trail as foam (agreed 2026-10-15; K1-K4 done: the bake, the library, the stamps, the foam lanes)
docs/visuals/magazine-explosions.md   # magazine explosions in the world view: the sim's record, the event per class, the analytic body, jets, debris, the blast and light, the after-smoke (X0-X7 done and approved 2026-10-15)
docs/visuals/torpedoes.md     # torpedoes in the world view: the bubble track as foam, the torpedo, launches, the hit's column on the splash model (agreed 2026-10-15; R0-R5 built, R6 cost measured, tuning by eye next)
docs/visuals/splashes.md      # shell splashes in the world view: columns through the 3/4 effects camera, foam, shadow, dye, time compression (P0-P5 done 2026-10-15)
docs/visuals/ship-lighting.md # the ship sprites lit: relief and normals, materials, the sun and moon, in the one forward pass (L1 done)
research/                     # research notes, see below
```

Namespaces follow the project names: `Fleetwright`, `Fleetwright.Gpu`, `Fleetwright.Shipgen`, ...

The sim's layers are projects, so the compiler keeps their direction: `Fleetwright.Sim.Core` references nothing,
`Fleetwright.Sim.Ships` references Core and `Fleetwright.Shipgen` (the only sim layer that does), the world
(`Fleetwright.Sim`) references Core and `Fleetwright.Sim.Combat` (ships at sea, step 2.2), which references Core
and Ships. Ships steaming steady go on the rails clock tier (step 2.5); off rails the asked speed picks the tier,
coarse (kinematic segments, `CoarseMotion`, decided once a coarse step) at 240x and up, fine below (step 2.8): every
rule for which tier is in `ShipSystem.Tiers.cs`. Read a ship's pose through `ShipState` (it evaluates rails and coarse
segments at the world's time), never the store's columns, and take a ship off rails before changing it
(`ShipSystem.Derail`; every command does, unless the clock is fast enough to snap it on rails: `ShipSystem.Snap`). Opposing
sides (a side per ship) within the encounter range are off rails, solved from the rails (`ShipSystem.Contacts.cs`). Zoom bands (the map's Globe ... Tactical, Ship: drawing only) and clock tiers (the sim's Routine, Rails,
Coarse, Fine: how time advances) are different things (`docs/world/architecture.md` "Terms"); nothing in the sim reads the map. The world
rests its clock only on a moment's boundary, so commands replay exactly; a world system is an `IWorldSystem` with its
events as plain structs, and nothing in a step allocates (the tests check it).

Content paths in code (`"Content/Shaders/Compiled/..."`) resolve against `AppContext.BaseDirectory`, not the working directory.

## Renderer

- HDR R16G16B16A16 color target, 8x MSAA, resolved and blitted to the swapchain
- The engine's main `RenderTarget` holds it; the current `IScene` draws into it. Without a scene the
  engine draws its scene graph instead (meshes with a base color
  texture and spherical-harmonics ambient light, `PbrMaterial` in Engine.cs), but nothing fills the scene today
- Dear ImGui overlay; Tracy zones in the frame loop (not in Release, see above)

## Views

Every view is built in layers joined by interfaces, so each runs on its own and none needs engine changes:

- a CPU step from the ship's data (the hitbox model) to what is drawn (a mesh), with no GPU;
- a renderer that draws into any `RenderTarget` (the main one, or a small offscreen one shown with `ImGui.Image` or
  on a game UI quad), driven by plain camera/state structs. It takes a device, never the engine; it reads the
  target's format and sample count to build its pipelines, and knows nothing of input, ImGui or the window;
- an `IScene`: input and its own ImGui UI around a renderer. The engine owns the window, the frame loop and the main
  target and calls the current scene each frame.

## Shaders

GLSL sources live in `Content/Shaders/Source/` and compile to SPIR-V in `Content/Shaders/Compiled/`. Shared code lives in `Source/Include/` (pulled in with `#include` under `GL_GOOGLE_include_directive`; the batch file compiles only the top folder). The compiled `.spv` files are checked in, so recompile after editing a shader. Requires `glslangValidator` from the Vulkan SDK:

```bash
compile_shaders.bat
```

## Dependencies

- ppy.SDL3-CS - SDL3 C# bindings
- ppy.SDL3_image-CS - texture decoding (glTF images) and PNG saving
- SharpGLTF.Toolkit - glTF/GLB loading
- ImGui.NET - Dear ImGui bindings
- TracyWrapper - Tracy profiler integration (Debug and DevRelease only)

## Research Notes

Research lives in `research/` and is produced by a separate research companion (Claude in the Claude app), which writes files directly into this folder.

- Before designing a non-trivial feature, check `research/` for an existing note on the topic.
- To ask for research, add an entry to `research/REQUESTS.md` (template inside) and tell the user. Don't block on it; continue with other work.
- Research notes are advisory: verify against the actual code before applying. Builds, tests, and git stay with Claude Code.
- Imported research from other projects lives in subfolders (e.g. `research/naval/`).

## Porting shipgen

The Python ship generator in `../shipgen` (frozen) has been ported here as `Fleetwright.Shipgen` (done 2026-10-09). `PORTING.md` holds the plan, the decisions and how each step went. `HANDOFF.md` has the latest session's notes and next steps: read it first. The shipgen docs live in `docs/shipgen/`.
