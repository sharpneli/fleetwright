# Porting shipgen into Fleetwright

The working plan for moving shipgen (`../shipgen`, Python) into this repo as C#. It lives here until the port is
done, then gets folded into README/CLAUDE.md and deleted.

Order (user, 2026-10-09): **ship generation first, sprites second.**

This plan replaces `../shipgen/PORTING.md` (2026-10-08), which was written before this repo existed. Its
decisions carry over unchanged. What's new here comes from looking at the real skeleton and the real Python, and
is marked **(new)**.

## Decisions (carried over from shipgen, user 2026-10-08)

- **The ship model must not change.** Everything deterministic must match Python: validation strings, sizing,
  layout, report, hitboxes, subdivision and `sprite.json`. The Python output is the test base.
- **Port as is, bugs included** (shipgen TODO "Generator bugs", "Hidden thresholds"). Fix them after the goldens
  pass, one fix per commit, updating the goldens on purpose.
- **Python is frozen now** (user, 2026-10-09). No more features go into it. Work on the model continues here,
  in C#, after the port. The Python only exists to produce the goldens.
- **Drawing only has to look right.** Clutter placement, dazzle, antialiasing and exact pixels may differ. Any
  seeded RNG works, deterministic per ship and **seeded per feature** (`"<id>/clutter/<key>"`, `"<id>/dazzle"`), so
  a refit doesn't reshuffle untouched parts.
- **Bake once.** Ships are drawn into textures when designed, refitted or repainted, not live.
- **The height map is never averaged.** No antialiasing, max-reduce for mips, and never the hardware MSAA resolve.
- **The command line is for testing.** The game calls the libraries directly.
- **Shipgen is a subproject, `Fleetwright.Shipgen`** (user, 2026-10-09). It works both as a library and as a
  command-line tool. The CLI comes first, as the test harness. The game's graphics side takes the library in
  later, and its API gets designed then, not now.
- **The RNG doesn't need to match Python** (user, 2026-10-09). That covers `sprite.json`'s `max_height_m`, which
  is compared with a tolerance.
- **Goldens are committed gzipped** (user, 2026-10-09), around 20–30 MB.
- **Not ported:** vidgen, `hitview.py`, `sinking.py`, `lookgrid.py`, `fleet.py`, `calibrate.py`, the legacy fleet
  CLI in `shipgen.py`. `fuzz.py` and `verify.py` come later (Step 8).
- **The contract stays two halves.** The design side never draws. The renderer reads only the built `Ship`.
  Colours live only in Looks.
- **Refits come later, but the structure must allow them:** keep `size`, the grow loop and `balance` as separate
  public stages (shipgen PORTING "Refits").

## What the earlier plan missed (new)

Found by reading the skeleton and the Python. Each one is handled in a step below.

1. **The repo can't hold more projects yet.** `Fleetwright.csproj` sits at the root and globs `**/*.cs`, so any
   project added under it gets compiled into the game. The GPU helpers the renderer needs (`Shared/`) live inside
   the game exe. → Step 0.
2. **Shaders are GLSL → SPIR-V only** (`glslangValidator`, Vulkan backend). There is no HLSL or shadercross.
   The renderer's shaders follow the same route.
3. **There's no stencil buffer.** The engine uses `D32_FLOAT`. Stencil-then-cover fills need `D24_UNORM_S8_UINT`
   or `D32_FLOAT_S8_UINT`, checked with `SDL_GPUTextureSupportsFormat`. AMD has no D24S8 on Vulkan, so prefer
   D32S8.
4. **`sprite.json` isn't fully RNG-free.** `shadow.max_height_m` includes `clutter.height_columns`, and clutter is
   placed by `random.Random`. Since we don't port Python's RNG, that one field can't match exactly. → Step 1,
   golden rules.
5. **Canvas sizes come from the rasteriser.** `size_px` for the hull and each turret is read back from the PNG
   cairosvg wrote, so it follows from the SVG's viewBox, the scale and `align = 2^(mips+1)`. The C# side must
   compute it on the CPU from the same rule, before anything is drawn.
6. **Number formatting in exact strings.** Validation and warning messages use `:.0f`/`.1f`/`.2f`/`.3f`
   (~70 uses), `:g` (15), `:,` and `:,.0f` (19), and `:+.1f`. .NET has no `:g` equivalent, so write a
   `PyFormat` helper. Use the invariant culture everywhere: set `<InvariantGlobalization>true</InvariantGlobalization>`,
   because a player's machine may be in a culture with a decimal comma.
7. **String sorting.** C#'s default string comparison is culture-aware, while Python compares code points. Use
   `StringComparer.Ordinal` in every sort or comparison of ids and names.
8. **Thread safety.** `geometry.py` uses `functools` caches. The designer UI will build on a worker thread, so C#
   caches must be per build or immutable after setup. No mutable statics.
9. **NativeAOT** (tech-stack plan). `System.Text.Json` must use source generation, with no reflection-based
   serialisation, in Core and the CLI.
10. **libm.** The earlier plan had the goldens captured on WSL (glibc). .NET on Windows calls the UCRT's
    `sin`/`pow`/`exp`, and so does Windows CPython. Capture the design-side goldens **with Windows Python** so
    both sides share a libm, and capture again on WSL as a check. Where WSL and Windows differ, we've found a
    threshold that flips on one ulp, which is worth knowing before we port it.
11. **Golden size.** Hitboxes are ~370 kB per battleship; 71 designs already make 16 MB. With a few hundred fuzz
    cases on top, store goldens gzipped (~12× smaller) and keep the fuzz set near 300 cases.
12. **A viewer.** "Looks right" needs something to look at. Use a ship viewer mode in the game exe that bakes a
    design and shows it with turrets and height-map shadow, plus the existing `-screenshot` capture so Claude can
    review output as PNGs too. → Step 6.

## Target layout (Step 0)

```
Fleetwright.slnx
Directory.Build.props         net10.0, nullable, InvariantGlobalization, warnings as errors in Fleetwright.Shipgen*
Directory.Packages.props      central package versions
src/Fleetwright/              the game exe (today's root files moved here)
src/Fleetwright.Gpu/          Shared/ extracted: GpuTypes, GpuPipelineBuilder, GpuMath, ShaderTypes
src/Fleetwright.Shipgen/          design side library. No package references. Pure, deterministic, thread-safe.
src/Fleetwright.Shipgen.Render/   display list (pure C#) + SDL_GPU backend. Refs Shipgen, Fleetwright.Gpu.
src/Fleetwright.Shipgen.Cli/      `shipgen` command: the design.py equivalent, plus golden-diff
tests/Fleetwright.Shipgen.Tests/  xUnit: golden tests, helper tests
shipgen/designs/              the 71 design JSONs, copied from ../shipgen/designs
shipgen/golden/               captured Python output (gzipped), see Step 1
Content/                      stays at the root, shared; each exe copies it to its output (decided in Step 0)
```

Rules, enforced by project references:
- `Fleetwright.Shipgen` (the design side, "Core" below) references nothing.
- Render references Core and Fleetwright.Gpu.
- The CLI references Core, and later Render.
- The game picks them up when its graphics side is ready (after the port).
- `geometry` lives in Core as the shared layer.

**The API is deferred** (user). The CLI is the first and, during the port, the only caller. Keep the same seams
as Python so a good API is easy to cut later:
- validate
- build, with `size`, the grow loop and `balance` as separate stages
- a CPU-only sprite layout, i.e. everything in `sprite.json`
- the GPU bake

The layout is kept apart from the bake so `sprite.json` can be tested without a GPU.

## Steps

### Step 0: prepare the repo (C#, no shipgen code yet)

- Move the game into `src/Fleetwright/`, extract `Shared/` into `src/Fleetwright.Gpu/`, add the `.slnx`,
  `Directory.Build.props` and `Directory.Packages.props`.
- Add empty `Fleetwright.Shipgen`, `.Cli` and `.Tests` projects. `.Render` waits until Step 4.
- `dotnet build` and `dotnet run --project src/Fleetwright` still work. The `-screenshot` capture still works.
- Update CLAUDE.md and README for the new paths and `dotnet test`.
- Add `*.json.gz binary` to `.gitattributes`.

Done 2026-10-09. Notes:
- `Content/` stays at the repo root, since the game and Shipgen.Render share the shaders and `compile_shaders.bat`.
  Each exe links it into its output folder, and shader loads now resolve against `AppContext.BaseDirectory`
  instead of the working directory.
- `Camera.cs` stayed in the game (it's input + Tracy, not GPU). The extracted helpers' namespace is now
  `Fleetwright.Gpu`.
- `Fleetwright.Shipgen` is `IsAotCompatible` and the CLI (assembly `shipgen`) is `PublishAot`, so the trimming/AOT
  analysers catch reflection early (trap 9).
- Tests are xUnit 2.9.3 (the SDK template's versions). `SetupTests` checks the invariant culture is in effect.

### Step 1: capture the goldens (Python, the last Python commit in ../shipgen)

Add `tools/golden.py` to the shipgen repo. It writes into `fleetwright/shipgen/golden/`:

- For every `designs/*.json` and ~300 fuzz mutants (fuzz.py's mutator; save the mutant designs, half with
  limits, half without):
  1. `validate(design, limits=True)`, `validate(..., limits=False)` and `looks.validate(design)`: exact strings.
  2. The whole `ship` dict from `build(design)`: design, report, hitboxes, render. Leave in the private `_` keys
     for now; we decide per key while porting whether it's part of the contract.
  3. Whether `build(design, hint=length_m)` equals the unhinted build.
  4. If Python raised: the exception type and message.
  5. Build time per design (the performance baseline).
- `sprite.json` from `render.render_ship(..., mips=5, previews=False)` for each design (WSL only, since it needs
  cairosvg).
- Copy each design's `hull.svg`, `hull.png`, `turrets/*.png` and `height.png` as visual references. They aren't
  compared pixel by pixel.
- `manifest.json`: the shipgen commit hash, Python version and platform for each capture, and the case list.

Run the design-side capture twice, on Windows Python (the target) and on WSL. Diff the two and write down every
difference. Run with `PYTHONHASHSEED=0`. A grep shows the design side uses no `hash()`, no RNG and no order-sensitive
`set` iteration (`set`s are only compared or sorted), but the capture checks this anyway.

Tag the shipgen commit (`golden-capture`).

Done 2026-10-09 at shipgen `b867f6bf` (tag `golden-capture`). `shipgen/golden/README.md` has the layout and
findings. In short:
- 371 cases: 71 designs plus 300 mutants. 325 build, 46 fail validation, none raise. The mutants are generated
  once on Windows and saved in `shipgen/designs/fuzz/`, since mutation draws through libm.
- 13 cases differ when built with the length hint (allowed by `build()`'s contract); C# should match those too.
- Hash seed 0 vs 1: identical. Windows vs WSL: floats within 1e-13, plus **one discrete flip**
  (`fuzz_free_057`, a `crew.spread()` remainder tie between two identical bridge towers, broken by an ulp of
  `block_outline` trig). That's the threshold item 10 predicted. The Windows capture is the golden.
- `murica` fails validate with limits, so it has no sprite golden (design.py skips it too).
- Baseline: median 1.1 s per design build in Python, max 18 s for a mutant.
- 25 MB in all, gzipped JSON plus reference PNGs.

**Golden comparison rules** (implemented in the tests and in `shipgen golden-diff`):
- Compare JSON trees, not bytes. The key sets must be equal, list lengths equal, strings and booleans exact, and
  integers exact.
- Floats are compared with a relative tolerance of about 1e-9. A difference in any **discrete** field (count, id,
  warning, a position that jumps) is a failure to investigate, never a reason to loosen the tolerance.
- `sprite.json` integers are exact. The exception is `shadow.max_height_m`, which must be at least the maximum
  of the non-clutter columns and at most that plus the tallest clutter item, since the clutter RNG differs (see
  "What the earlier plan missed" 4).

### Step 2: the CLI as the test harness

Build the command line first, so every module ported in Step 3 can be checked against the goldens with it.

- `shipgen design shipgen/designs/*.json [--no-limits] [--out DIR]` writes report.json and hitboxes.json (and
  the whole ship dict for golden comparison), then prints design.py's one-line summary. It reports "not ported
  yet" until Step 3 fills in the pipeline.
- `shipgen golden-diff GOLDEN OUT` applies the comparison rules above, naming the first differing path in each
  file. The xUnit golden tests call the same comparer.
- `shipgen validate` prints the validation strings, which are the first thing to match.
- Arguments are parsed by hand like `Program.cs`: there are only a few flags, and a hand parser stays AOT-safe.

Done 2026-10-09. Beyond the plan: `shipgen capture` (golden.py's design capture from the C# side, for
`golden-diff`), `shipgen golden-check` (capture and compare in one go, the everyday command) and `shipgen bench`
(single-threaded build and per-knob rebuild times next to Python's). The comparer (`GoldenDiff`) and the case runner
(`GoldenCases`) live in the library, so the tests use the same code. The CLI uses server GC: it builds in parallel.

### Step 3: Fleetwright.Shipgen (the design side)

About 10k lines of Python. Port bottom-up along the imports, with each module tested against the goldens as soon as
it exists. Start with the helpers and their own unit tests:
- `PyMath`: `PyMod`, `FloorDiv`, `PyRound(x, n)`.
- `PyFormat`: `.Nf`, `g`, `,`, `+`.
- Truthiness helpers for `d.get("k") or default`.
- CRC-32 (`System.IO.Hashing.Crc32` matches zlib).

```
weights (20) · geometry (961) · powerplant (411) · propulsion (182)
→ decks (51) · geo (61) · arcs (110) · batteries (145) · stability (118)
→ hullweight (272) · ordnance (137) · armour (364)
→ navarch (254)                    size solver; imports styles: break the cycle with an interface
→ layout (2858) + armament (346) + firecontrol (266)      import each other: one unit
→ crew (419) · subdivision (551) · hitbox (248)
→ styles: base (306) · warship (30) · carrier (622) · merchant (368) · planing (183)
→ shipdesign (450)                 validate, build, height_columns
```

`hull/crew/plant_templates.py` generate the `*-templates.md` tables and aren't on the build path. Port them only
when the designer UI needs the presets.

Data model:
- Typed records for the design input (README "Design input") and for the `Ship` the game reads (report, hitboxes,
  render spec), serialised with source-generated `System.Text.Json` under snake_case names. A golden test then
  compares C#'s `Ship` JSON with Python's.
- Internal modules may stay close to the Python at first and get tidied once the goldens pass.
- **Structural cleanup is allowed during the port if the output stays identical.** The typed model naturally
  settles some shipgen TODO "Code structure" items (`Geo.plant` as a record, `_plate_mm`/`_clutter` side
  channels). Behaviour changes wait until after the port.

Python → C# traps (from the shipgen plan; still all valid):
- `%` and `//` floor in Python and truncate in C#.
- `round(x, n)` is correctly rounded decimal in Python and scaled in .NET.
- `int()` never overflows in Python.
- `min`/`max`/`sorted` return the first of equal elements.
- Truthiness: `or` replaces 0, so it isn't `??`.
- Dict order is insertion order.
- Float printing differs (`1.0` vs `1`).
- libm last-bit differences.

New traps: 6–8 in "What the earlier plan missed" (formatting, culture, ordinal sorting, thread safety).

**Done when:**
- Every design and fuzz golden matches, validation strings exactly.
- Builds are no slower than Python (they should be far faster). Add a BenchmarkDotNet or simple timing test for
  the designer's per-knob rebuild.
- `Build` runs concurrently on several threads with identical results (a test).

With Step 3 done, **ship generation is ported**. `shipgen design` reproduces design.py's data output for every
design.

Done 2026-10-09: all 371 cases match, and every float in them is **bit-identical** to the Windows capture (the
`fuzz_free_057` crew tie included). The only differences are JSON int-vs-float types where Python's `max()`/`round()`
keep a design's int (`control_mm: 25` vs `25.0`), which the golden rules accept. What it took:

- **Data model (a deviation):** the port keeps Python's data model rather than typed records: a small Python runtime
  (`Py/`: an insertion-ordered `PyDict`, Python ints as `long` and floats as `double` kept apart, lists, `object?[]`
  tuples) carries the design, the specs and the ship dict, and real classes stand where Python has them (`Hull`,
  `HullForm`, `Layout`, `Navarch.Result`, `Geo`, `Weight`, footprints). Typed records for every dict would have been
  a rewrite with key-set mismatches at every step, and validation prints the design's raw values (`76` vs `76.0`).
  The typed `Ship` contract comes with the API design (after the port), as a mapping over this.
- **Exact numerics:** .NET's `Math` (sin, pow, exp, ...) is the UCRT's, bit for bit with Windows CPython (checked on
  4,000 inputs each: `PyTests`, `tests/.../Data/pyref.py`). CPython's own algorithms are ported: `sum()` (Neumaier,
  3.12+), `hypot`/`dist` (`vector_norm`), `gamma` (Lanczos), `round(x, n)` (correctly rounded via BigInteger), float
  `//` and `%`, repr and the `.Nf`/`g`/`,`/`+` formats (half-even on the exact value), and `min`/`max`/`sorted` with
  Python's first-of-equals and stability.
- **Thread safety:** no mutable statics. Python's caches became per-object (`PreparedPolygon` on the footprint,
  `level_outline`'s scanlines and `section_exponents` per layout or hull form) or a `[ThreadStatic]` memo of a pure
  function (turret shapes). `GoldenTests.ConcurrentBuildsAreIdentical` builds the same designs on many threads.
- **Speed:** a single-threaded build is 6-14x Python's (`shipgen bench`: bismarck 0.32 s vs 2.5 s, the slowest
  mutant 1.0 s vs 18 s); the designer's per-knob rebuild (the length hint) takes 0.02-0.17 s. Most of what is left
  is allocation (about 300 MB per battleship build: dicts, LINQ, point lists), for the post-port refactor.
- **Tests:** `dotnet test` runs the Py helpers against CPython, every golden case (about 40 s) and the concurrency
  check.

### Step 4: display list and sprite layout (Fleetwright.Shipgen.Render, CPU only)

Port the drawing in `shipgen.py` (`build_hull`, `build_turret`, `dazzle`, the SVG primitives), `looks.py` (805),
`clutter.py` (717) and `render.py`'s layout half, so that it emits draw commands instead of SVG text. That's about
3k lines of Python.

- Commands: `FillPath(polys, rule, rgba)`, `StrokePath(polyline, width, cap, join, dash, rgba)`, circle and
  ellipse fills, `PushClip`/`PopClip`, `PushOpacity`/`PopOpacity`, `Transform`, `Text`.
- Arcs and curves are flattened on the CPU, with a tolerance in pixels.
- A tiny SVG writer for the display list replaces `hull.svg` for debugging. It can be compared side by side with the
  golden `hull.svg` in a browser, with no GPU involved.
- The sprite layout: canvas sizes (see "What the earlier plan missed" 5), origin, mount pixels, z, arcs and mip
  rects, i.e. all of `sprite.json`.
- Height columns: the layout's columns plus the clutter columns, drawn lowest first.
- The RNG is seeded per feature (Decisions).

**Done when** `sprite.json` matches the goldens for every design (exact apart from `max_height_m`), and every
design produces a display list and an SVG without errors.

### Step 5: SDL_GPU backend

In this order, with a test rendering after each item (Step 6's viewer, or CLI PNGs):

1. **Fills:** stencil-then-cover into a D32S8 target. A fan per polygon, increment/decrement for nonzero,
   invert for even-odd, then cover where the stencil ≠ 0. Circles and ellipses are tessellated into polygons.
2. **Strokes:** expanded to polygons on the CPU (butt/round/square caps, miter/round joins, dashes), then filled.
3. **Clips:** a second stencil bit.
4. **Group opacity:** an offscreen texture composited with alpha.
5. **Text:** the hull number. Render it with SDL3_ttf (the `ppy.SDL3_ttf-CS` binding, alongside the existing ppy
   packages) into a surface, upload it, and draw it as a rotated quad. DejaVu Sans Bold goes in
   `Content/Fonts/`; its licence allows bundling it.
6. **Antialiasing:** 8× MSAA on the colour layers (checked with `SDL_GPUTextureSupportsSampleCount`, falling back to
   4×), with the hardware resolve.
7. **Height map:** R8 at 1 sample, grey = `round(top_m / 0.25)` clamped to 0–255. If thin parts break up, use
   MSAA with a **max** compute resolve. This deliberately differs from Python's antialiased `height.png`; compare
   coverage only.
8. **Mips:** colour layers with a premultiplied 2×2 box (`GenerateMipmaps` is fine), the height map with a 2×2
   **max** compute pass.
9. **Large canvases:** tile when a canvas exceeds the device's maximum texture size.
10. **Readback** for the CLI (the engine's screenshot code already does `DownloadFromGPUTexture` and
    `IMG_SavePNG`).

Shaders are GLSL in `Content/Shaders/Source/`, compiled by `compile_shaders.bat`, with the SPIR-V checked in, like
the game's own.

The CLI creates the device with no window. This is verified (2026-10-09, SDL 3.5.0 via ppy.SDL3-CS 2026.1002.1,
Vulkan):
- `SDL_Init(SDL_INIT_VIDEO)` is required and is enough. `SDL_GPUSelectBackend` fails with "Video subsystem not
  initialized" when no video device exists, whatever the backend.
- A clear and readback works with no window.
- The `offscreen` video driver (`SDL_HINT_VIDEO_DRIVER`) also works, for sessions with no desktop.
- The `dummy` driver does not work.
- On the dev GPU: D32S8, D24S8 and 8× MSAA on RGBA8 are all supported.

**Done when:**
- Every design bakes without errors.
- The per-layer alpha-coverage IoU against the golden PNGs is above about 0.98.
- The user signs off the look in the viewer, next to the old PNGs.

### Step 6: viewer and full CLI

The viewer is a test tool. How the game itself uses Shipgen (the API, the asset path) is designed after the port.

- **Viewer:** `dotnet run --project src/Fleetwright -- -ship=shipgen/designs/bismarck.json [-era=wwii]`. It
  builds and bakes the ship and draws hull and turrets from the baked textures, using the mip levels as the game
  will. Turrets turn through their `traverse_deg`, and the shadow comes from the height map (a first version of the
  game's own shadow shader). An ImGui panel picks the design, navy/era, mip level and sun, and rebuilds on change,
  which is the designer UI's loop in miniature. `-screenshot=N` works here too, so Claude can check renders
  without a human.
- **CLI:** `shipgen design ... [--scale 10] [--mips 5] [--previews]` writes design.py's files (sprite.json,
  hull.png, turrets/*.png, height.png, `*_mips.png`, preview_rest). The other debug images only if they're
  missed.

### Step 7: retire Python

- Fold what the game needs from shipgen's README (design input, outputs, conventions) and HANDOFF (decisions)
  into docs here (`docs/shipgen/`).
- Move the open shipgen TODO items into this repo's tracking. Drop the vidgen and Python-only ones. Work on the
  model (sizing, physics, damage, looks) continues here after the port.
- The shipgen repo stays as the frozen reference at the `golden-capture` tag.
- Regression workflow from here on: `shipgen design --out new/` then `shipgen golden-diff`.

### Step 8: fuzz and verify in C#

- `fuzz`: a mutator plus time and memory guards, as a test or a CLI command.
- `verify`: the sprite-vs-hitbox pixel check, run on CLI output.

Then start the bug fixes held back by "port as is".

## Out of scope for the port

- Texture compression (KTX2, BC7/BC4) and the game's sprite batcher and asset pipeline: tech-stack plan, Phase 3.
- Refits as a feature (the structure is kept ready for them).
- Any change to physics, layout or looks.

## Open questions

- The game-facing API and asset path (after the port).

## Progress

- [x] Step 0: repo restructure (solution, src/, Fleetwright.Gpu, props files)
- [x] Step 1: goldens captured, shipgen tagged
- [x] Step 2: CLI harness: design, validate, golden-diff
- [x] Step 3: Fleetwright.Shipgen matches the goldens
- [ ] Step 4: display list, SVG writer, sprite.json matches
- [ ] Step 5: SDL_GPU backend
- [ ] Step 6: viewer + full CLI
- [ ] Step 7: Python retired, docs moved
- [ ] Step 8: fuzz and verify ported
