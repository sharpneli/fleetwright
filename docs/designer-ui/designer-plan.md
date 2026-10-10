# Ship designer, first iteration: plan (2026-10-10)

The goal is the first designer that **changes ships**: start from an empty hull or open an existing design, turn
knobs, see the ship, its profile and its particulars follow, and save the result. It builds on
`designer-ui-research.md` (§15.6–15.7 for the layout, §11 for responsiveness) and `follow.html` as a starting
point, not a spec.

Out of scope for now: roles, matchups and the "catches · escapes · beats" chart, admiralty requests, sketches A/B
and compare, goal-seek, widget-grow animations, the drag-and-drop arrangement strip. The plan leaves room for each.

## User's decisions so far

- **An empty ship is an 8 kn hull and nothing else.** `{"id": "new", "speed_kn": 8}`. (First proposed at 6 kn;
  8 kn, the engine's existing common floor, is fine.)
- **Nothing under 8 kn.** Slower ships aren't seaworthy in rougher weather, and we don't want to simulate that for
  an undamaged ship cruising. The speed knob's floor is 8 kn, for every style. (This replaces research §14.4 item 8,
  "speed 0 means no plant": there is always a plant.)
- **The designer is a scene of its own, callable from anywhere.** It takes a design in and hands a design back
  (Accept or Cancel); the game launches it from its own UI and uses the result. The ship viewer's
  `DesignSession` is one caller, not a dependency.
- **ImGui** for the designer's UI, themed to the mockup.
- **Units: a toggle** (metric / imperial). In the game it becomes a per-nation option; historical nations use their
  own units for flavour.
- **Saved designs** go to `Documents/My Games/Fleetwright/Designs/` for now; campaign saves come later and will
  own them.
- **The empty ship's warnings are accepted** for v1.
- **Threading can grow step by step**: the build goes to the worker first; moving the sprite drawing and bake there
  too can wait until it's convenient.
- **Existing ships can be opened and tuned**: load, play with the knobs, see the effect.
- **The designer works on a background thread.** No edit freezes the game; results land when ready.
- **Compose from the offscreen renderers** (CLAUDE.md "Views"): the designer is an `IScene` whose pictures come
  from renderers drawing into their own `RenderTarget`s.

## What was measured

C# build, Release, this machine (`shipgen bench`):

| design | cold build | warm rebuild | allocated per build |
|---|---|---|---|
| destroyer | 62 ms | 50 ms | 30 MB |
| dreadnought | 106 ms | 79 ms | 78 MB |
| bismarck | 359 ms | 118 ms | 66 MB |
| yamato | 90 ms | 62 ms | 76 MB |

In the viewer (first build, JIT included): build 0.5–0.9 s, sprite drawing 0.09–0.18 s, GPU bake 0.02–0.03 s. All
of it runs on the render thread today (`DesignSession.Rebuild`, `ShipViewer.Rebuild`), so switching design in the
viewer already stalls the frame.

The empty ship, built with the limits off (they reject it today, below):

| | |
|---|---|
| hull | 30.0 × 7.9 m, T 1.19 m, GM 0.45 |
| displacement | 157 t standard, 160 t full |
| plant | geared turbines, oil, 20 shp, 1 shaft, 1 funnel |
| crew | 7 |
| weights | hull 81 t, superstructure 58, misc 15, machinery 4, fuel 2 |
| warnings | "Very beamy hull (L/B 3.8)"; "Heels 38° in a beam gale … deck edge under at 28°" |

Findings that shape the plan:

1. **A warm build is 50–120 ms**, fast enough that the latest-wins queue below feels live without the research's
   tier-0/tier-1 estimates. Those stay out of v1.
2. **A build allocates 30–80 MB.** Moving the build off the render thread does not move its garbage collections:
   gen0/gen1 collections stop every managed thread, the render thread too. A slider dragged across ten values is
   ~0.7 GB of garbage. This needs measuring (Tracy frame times during a drag) before deciding on a fix, see Step 1.
3. **The input limits already agree**: `Style.CommonLimits` has `speed_kn` 8..42, which warships use (the 15..60
   is the planing style's). The empty ship at 8 kn validates and builds: 30.0 × 7.9 m, 159 t, 100 shp, the same
   two warnings. No engine change.
4. **The empty ship is not clean**: two warnings, both true physics of a 30 m hull carrying the default bridge. See
   open question 1.
5. **Shipgen builds are already thread-safe** (`ConcurrentBuildsAreIdentical`), and the drawing tests run in
   parallel too. The bake creates its own SDL GPU device when it isn't handed one (the CLI does this), so it may run
   on the worker as well (to be confirmed in Step 1).

## Architecture

Three layers, matching the views' rule (data → renderer → scene):

```
 UI thread (render loop)                         worker thread (below-normal priority)
 ───────────────────────                         ─────────────────────────────────────
 DesignerScene ── edits ──► DesignDoc            DesignWorker
   knobs, sections,          (Design records,     loop: take the newest request
   Legend, history           undo/redo,             Validate (+ limits)
                             baseline)              ShipDesign.Build
        │                        │ Submit(design)   HitboxMesh.Build (profile, inset)
        │                        └───────────────►  ShipSprites.Build + ShipBake (own GpuBaker device)
        │                                           mips on the CPU
        │      ◄────── Latest: DesignResult ─────── publish (one reference swap)
        ▼
 each frame: if Latest.Id > shown.Id → upload textures + meshes, swap (all in one frame)
 renderers draw into offscreen targets → ImGui.Image in the layout
```

### DesignDoc: the edit model (no GPU, no threads)

- `Design` is already an immutable record tree; an edit is a `Design → Design` function with `with`. Undo/redo is a
  list of `Design` values plus a label ("Speed 21 → 20.5 kn"). Structural sharing keeps it cheap.
- **Baseline**: the design as opened. "Reset" returns to it; the Legend shows "since opened: −1,117 t · −4.4 m".
- **Dirty / Save as**: designs from `Content/Designs/` are game assets and are never overwritten. Saving writes
  `Design.ToJson(indented)` to the user's designs folder (open question 2).
- **Knobs**: one table describes every control the UI offers.

  ```csharp
  sealed record Knob(string Id, string Label, string Unit, double Step,
                     Func<Design, double?> Get,            // null = Auto
                     Func<Design, double?, Design> Set,    // null returns it to Auto
                     Func<Ship, double>? Effective,         // what Auto chose, read from the built ship
                     Func<Design, bool>? Applies);          // e.g. turret faces only with a main battery
  ```

  The range comes from the style's `Limits()` by the knob's JSON path, so the engine stays the one source of truth;
  the 8 kn floor is the limits' own. A knob on Auto shows `Auto · 100`, typing
  or stepping overrides it, ⟲ returns it to Auto. Trim sheet and section panels read the same knobs, so an edit in
  one shows in the other (research §15.7 rule 2).

Unit-testable on its own: every knob's `Set(Get(d))` round-trips over all 71 designs; every knob stepped once still
validates; undo/redo restores equal records.

### DesignWorker: the background builder

- One dedicated `Thread` (`BelowNormal`, so the render thread keeps its core), one mailbox slot. `Submit(design)`
  overwrites whatever is pending and returns a request id. **Latest wins**: a build that is running finishes (Shipgen
  has no cancellation, and 120 ms isn't worth adding it), then the worker takes the newest request and skips the rest.
  A slider dragged across 20 values builds about 3–5 of them, not 20, with no debounce timer.
- **Result** is one immutable `DesignResult` published by a single reference swap:
  `{ Id, Design, Ship?, Errors, Warnings, HitboxMesh, SpriteSet, baked Image8 layers with mips, timings }`.
  Everything heavy is made on the worker; the UI thread only uploads textures and vertex buffers. Errors keep the
  last good ship on screen with the remarks saying why the new one didn't build.
- **Cache** by the design's JSON (an LRU of ~16 results), so undo, redo and Reset are instant.
- **Look-only edits** (navy, era) reuse the built `Ship` and only redraw.
- **The bake runs on the worker** with its own `GpuBaker` (its own headless SDL GPU device, as the CLI does). Two
  devices in one process (the engine's and the baker's) is untested; Step 1 checks it. If it is fragile, the fallback is to
  hand the lowered draw list to the render thread and bake there (20–30 ms, one hitch per landed result).
- **Probes later** (marginal-cost chips, Step 7) use the same worker at a lower priority: they run only when the
  mailbox is empty and are dropped when a user edit arrives.
- **`DesignSession` moves onto the worker too**, so the ship viewer and hitbox viewer stop freezing on design
  switches and all three scenes share one result.

GC (finding 2) is handled in Step 1: measure first. Candidates, cheapest first: `GCSettings.LatencyMode =
SustainedLowLatency` while the designer is open; a larger gen0 budget (`GCgen0size`) so collections are rarer;
then cutting allocations in the hot spots of `ShipDesign.Build` with `tools/alloctop`, which also speeds the build.

### Renderers the designer composes

| picture | renderer | status |
|---|---|---|
| top view (the sprite) | `ShipSpriteRenderer`: the sprite quads, turrets and shadows, into any target | **extract** from `ShipViewer.DrawShip`, which draws straight into the main target today |
| side profile with armour | `HitboxRenderer`, orthographic, `Bearing 90, Elevation 0`, armour kinds only | exists; needs a **colour-by-thickness** mode (the armour ramp from the mockup) and the mm per prism carried in `HitboxMesh` |
| 3D inset (optional) | `HitboxRenderer` | exists (the viewer's inset) |
| weight bar, curves, strip | ImGui draw lists | new, small |

Each picture is fitted to the hull's length and gets a scale bar, so a 30 m hull and a 263 m one both fill the
stage (research §15.4). When the length changes, a faint outline of the previous hull stays for a second ("since
last edit: +8.5 m").

### DesignerScene: the screen

A third scene in `SceneSwitcher` ("Designer"), and `-view=designer` / `-view=designer -ship=…` on the command line
so screenshots can drive it. Layout after `follow.html`, in ImGui themed with the mockup's tokens and the bundled
IBM Plex fonts (shipped as game assets):

```
┌ name · file ▾  [Open] [New] [Save as]   Undo Redo        settling ◔ 80 ms ┐
├ rail ──────────┬ centre ───────────────────────────────────┬ Legend ──────┤
│ 0 Overview     │ top view (pinned, fitted, scale bar)      │ dimensions   │
│ 1 Main battery │ side profile, armour mm on the plates     │ displacement │
│ 2 Secondary…   │ ───────────────────────────────────────── │ machinery    │
│ 3 Protection   │ the open section's panel                  │ armament     │
│ 4 Speed & mach.│  (0: trim sheet + standard vs baseline)   │ protection   │
│ 5 Hull & upper.│                                           │ complement   │
│ 6 Fire control │                                           │ weight bar   │
│ 7 Crew         │                                           │ remarks      │
├────────────────┴───────────────────────────────────────────┴──────────────┤
│ history: ○ Opened  ○ Speed 21 → 20.5  ○ Belt 279 → 267  ● +Q turret         │
└──────────────────────────────────────────────────────────────────────────────┘
```

- **Role** (section 1 in the mockup) is left out until roles exist; the numbers shift down by one.
- **Settling**: while `requested > shown`, a spinner and the Legend numbers that are about to change dim slightly.
  Nothing blanks. When the result lands, changed numbers show their delta for a moment.
- **Remarks** are the build's warnings and errors, as today's strings. Structured warnings with fixes (research
  §12.2) are a later engine change.
- **Every widget is laid out in a rect** computed per frame, so the later grow/shrink animations (§15.6) are a
  tween between two rects and need no restructuring.

**v1 sections and their knobs** (essentials only; "Details" can come later):

| section | knobs |
|---|---|
| 0 Overview (trim sheet) | speed, range, belt, armour deck, turret faces, rounds per gun, secondaries per side; standard displacement vs opened, full load, L, B, GM |
| 1 Main battery | per battery: calibre, calibre length, barrels, fore / aft / mid / wing counts, superfire per end, echelon; + battery, − battery |
| 2 Secondary · AA · TT | per battery: calibre, barrels, per side, mount (deck / casemate); AA heavy and light; torpedo mounts and tubes |
| 3 Protection | belt, belt bottom, upper belt, armour deck(s) mm, bulkhead, end belts fore / aft (mm, reach), TDS depth, turret faces per battery |
| 4 Speed & machinery | speed (≥ 8), range, stress (Conservative ↔ Forced), shafts (Auto), arrangement (named presets) |
| 5 Hull & upperworks | block coefficient (Full ↔ Fine), freeboard, raised stretches (presets), tower levels, deckhouse levels, aft control |
| 6 Fire control | directors per battery (count), rangefinder base |
| 7 Crew | crew standard (presets), endurance days (Auto) |

Adding the first main battery or the first armour to an empty ship sets a modest default (a 152 mm twin forward;
belt 50 mm) that the knobs then move; the history labels it.

**Input**: `−`/`+` buttons, mouse wheel over a value (shift for ×4), typing, Ctrl+Z / Ctrl+Y, 0–7 for sections.
Values show in the toggled units (metric `279 mm`, imperial `11.0 in`; the design always stores metric).

## Progress (2026-10-10)

- Done: the worker (build, mesh, sprites and bake off the render thread, on the game's device with its own command
  buffers; the viewers use it too), `ShipSpriteRenderer`, `DesignDoc` and the knob table with tests, the fonts, and
  the scene: top bar (New / Open / Save as / Undo / Redo / Reset / units), the rail with summaries, the top view and
  side profile offscreen, the trim sheet with "since opened", sections 1-7, the Legend (particulars, weight bar,
  remarks) and the history strip. Idle frames allocate about 200 bytes.
- Learned: the engine builds the UI before `Draw`, so offscreen targets shown with `ImGui.Image` are sized in
  `BuildUi` (resizing in `Draw` freed a texture the UI held: a crash).
- Then (user): one design across the scenes (`DesignSession` is the hub; the designer edits through it on its
  shared worker), template pickers (plant, hull construction, crew standard, armour materials; catalogues in
  `Content/Templates` from `tools/designer_templates.py`), the style button (navy and era, tried on or kept).
- Next: the profile's armour coloured by thickness with mm on the plates (Step 7), then the marginal-cost chips
  (Step 9), then the research's later items.

## Steps

Each step is a commit, verified with `dotnet test` and, for UI, `-screenshot -ui` readback.

1. **Measure the GC cost.** A Tracy run of the viewer with a background loop rebuilding Dreadnought; record frame-time
   spikes with the default GC, `SustainedLowLatency` and a larger gen0. Note the result in `docs/profiling.md` and pick
   the setting. Also confirm `ShipSprites.Build` + `ShipBake` with its own device on a non-main thread.
2. ~~Engine: the 8 kn floor.~~ Not needed (finding 3). The empty design gets a test with
   DesignDoc (Step 5): it validates and builds.
3. **DesignWorker + DesignResult**, with tests (latest wins, ids increase, errors keep the last good ship, cache hit
   on undo). Move `DesignSession` onto it; the ship and hitbox viewers consume results and stop stalling on design
   switches.
4. **ShipSpriteRenderer** extracted from `ShipViewer`; the viewer uses it into the main target, unchanged on screen
   (`-screenshot` before and after match).
5. **DesignDoc and the knob table**, with the round-trip and validate-after-step tests over all designs. Save as /
   open from the user folder.
6. **DesignerScene skeleton**: theme and fonts, the zones, the top view offscreen, the Legend from the report, the
   trim sheet, history, New / Open / Save as. Usable end to end from here.
7. **Side profile**: armour colour-by-thickness in `HitboxRenderer` / `HitboxMesh`, mm labels on the plates.
8. **Section panels 1–7** with the knobs above, one or two per commit.
9. **Marginal-cost chips** on the trim sheet: probe builds one step up and down for the hovered row, at low priority,
   cached by design. The worker's priority lane comes in here.

After that, in the order the research suggests: the arrangement strip, the speed curve with goal-seek, widget
animations, sketches and compare, structured warnings with fixes, size drivers in the report.

## Open questions

None open. Answered 2026-10-10: the empty ship's warnings stay (v1), saves go to My Games for now, ImGui, a units
toggle (see the decisions at the top).
