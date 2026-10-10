# Profiling

CPU profiling with `dotnet-trace` (installed as a global tool: `dotnet tool list -g`), summarized by
`tools/speedscope_top.py`. Works on both exes; the shipgen CLI is the easy target because it exits on its own.

## Recipe

```powershell
# 1. Always Release
dotnet build src/Fleetwright.Shipgen.Cli -c Release

# 2. Trace (samples every thread's stack, ~1 ms). Writes X.nettrace and X.speedscope.json
$p = "$env:TEMP\prof"   # anywhere outside the repo
dotnet-trace collect --format Speedscope -o "$p\shipgen.nettrace" -- `
    src\Fleetwright.Shipgen.Cli\bin\Release\net10.0\shipgen.exe design shipgen/designs/bismarck.json --out "$p\out" --previews

# 3. Summarize: threads, top self CPU, top inclusive Fleetwright frames
python -I tools/speedscope_top.py "$p\shipgen.speedscope.json" -n 25
# drill into one method: CPU time per direct callee
python -I tools/speedscope_top.py "$p\shipgen.speedscope.json" --focus "ShipDesign.Fit("
```

The exe is `shipgen.exe` (not `Fleetwright.Shipgen.Cli.exe`). Other useful targets: `bench bismarck yamato` (build
only, single-threaded), `bake ...` (the GPU bake), `golden-check` (everything, ~25 s).

The game, built `-c DevRelease` (Release has no console): `src\Fleetwright\bin\DevRelease\net10.0\Fleetwright.exe -ship=shipgen/designs/bismarck.json -screenshot=300`
runs 300 frames and exits, so it traces the same way. To trace an already running game:
`dotnet-trace collect -p <pid> --duration 00:00:10 --format Speedscope -o game.nettrace`.

The `.speedscope.json` also opens in https://www.speedscope.app for a flame graph, and the `.nettrace` in PerfView or
Visual Studio.

## Reading the numbers

- dotnet-trace ends every stack in a pseudo-frame: `CPU_TIME` (in managed code) or `UNMANAGED_CODE_TIME` (native
  code, waiting, idle). The script charges `CPU_TIME` to the method above it and drops idle time from self time.
  Inclusive time still includes waiting.
- **Parallel code is badly overstated.** Times are summed over threads, and `CPU_TIME` means "the sampled thread was
  in managed code", not "on a core": `Preview.ShadowMask`'s `Parallel.For` showed 1.9 s while its wall time was
  ~60 ms per call. Trust the main thread's inclusive times; for anything parallel, time it with a temporary
  `Stopwatch` + `Console.Error.WriteLine` and remove it before committing.
- One `design` run is cold: most of `ShipDesign.Build`'s ~0.9 s there is JIT. `bench --repeat 5` gives warm build
  times (~0.17 s for Bismarck) and allocations; profile `bench` for the design side.
- The first run of a method includes its JIT; tiny methods can look odd. Compare runs, not single samples.
- `PollGCWorker` time is threads parked for a GC: allocation pressure, look for the allocating loop. The CPU trace
  charges it to whoever triggered the GC, not to who allocated: use an allocation trace (below) to find the garbage.

## Allocations

The game is soft real time, so garbage matters as much as CPU time. `bench` prints MB allocated per build; to see
who allocates, take a `gc-verbose` trace (an AllocationTick event with its stack per ~100 KB) and sum it with
`tools/alloctop`:

```powershell
dotnet build tools/alloctop -c Release
dotnet-trace collect --profile gc-verbose -o "$p\alloc.nettrace" -- `
    src\Fleetwright.Shipgen.Cli\bin\Release\net10.0\shipgen.exe bench bismarck yamato --repeat 3
tools\alloctop\bin\Release\net10.0\alloctop.exe "$p\alloc.nettrace" -n 25      # by method, by type, by both
tools\alloctop\bin\Release\net10.0\alloctop.exe "$p\alloc.nettrace" --focus "Navarch.Solve("            # by callee
tools\alloctop\bin\Release\net10.0\alloctop.exe "$p\alloc.nettrace" --focus "Pt].AddWithResize" --callers # who grows lists
```

Methods show as the innermost `Fleetwright` frame; lambdas and local functions show by their compiler names
(`<BuildLayout>b__135`): `--focus` on that name shows what it calls, `--callers` who calls it.

How the hot loops avoid garbage (`Fleetwright.Shipgen`):

- `Scratch<T>.Rent(out var list)` lends a per-thread list for the length of a `using`; it comes back cleared. Hot
  helpers have an overload that appends to a caller's list (`RoofSpots`, `CirclePolygon`, `Footprint.Points`,
  `FpIntervals`).
- `KeyedSort` sorts `(key, index)` pairs in a scratch list: `OrderBy`'s order (ties by index) without its arrays.
  `List.Sort` alone is not stable and can change a layout.
- LINQ's `Min`/`Max` on doubles have NaN rules (`Min`: a NaN wins; `Max`: leading NaNs are skipped); a loop that
  replaces one keeps them (`SpanMath.Min`), or golden-check may catch it one day.

## After an optimization

Speed changes must not change output. Check, all `-c Release`:

- `Fleetwright.Shipgen` (design side): `golden-check` stays 371/371.
- `Fleetwright.Shipgen.Render`: `svg-check`, `sprite-check`, `png-check`; `dotnet test` covers the rest.
- Previews (`Bake/Preview.cs`): no golden; compare the preview PNGs byte for byte before and after
  (`design ... --previews --out before` / `after`).

## Log

| Date | Target | Finding | Result |
|---|---|---|---|
| 2026-10-09 | `design bismarck --previews` | `Preview.ShadowMask` "1.9 s" (really ~60 ms wall per call, see above), `ShipDesign.Build` ~0.9 s cold, `GpuBaker` device ~0.2 s, `Preview.Rotate` 0.16 s | baseline, ~2.3 s wall untraced |
| 2026-10-09 | `Preview` | ShadowMask marched every ray to full length, also over the sea | rays only visit steps inside the height map's bounding box and stop once hmax can't beat the best; rotated turrets cached per (type, angle). ShadowMask 56-75 -> 30-49 ms, Rotate 161 -> 57 ms; previews byte-identical |
| 2026-10-09 | `bench bismarck yamato` | warm 0.17 s per build, 229-278 MB allocated each; top self: `Layout.Clear` (linear scan of all sweep polys), int sort with a `Comparison`, `PolygonsIntersect`, `AddDeckhouseLevels.Runs`, LINQ in `DeckLevel` | not done: next candidates are a spatial index for `Layout.Sweeps` and cutting allocations; gate is `golden-check` |
| 2026-10-10 | `bench bismarck yamato` | the row above misread `Layout.Clear`: `Sweeps` holds one poly per main turret (2-6), an index can't help, and `Clear` was <1% of CPU. GC was 41% of CPU; an allocation trace put the garbage in `AaSlots`/`RoofSpots`, `LevelOutline`, `FireControl.Place`, `Slabs`, `Navarch.Solve`'s weight list, LINQ in `DeckLevel`, `BarrelFootprint`, the deckhouse width search | scratch lists, keyed sorts and loops in place of LINQ, outputs unchanged (golden/svg/sprite-check 371/371). Allocated per build 230 -> 65 MB (Bismarck), 278 -> 76 MB (Yamato); warm build ~25-35% faster, same machine back to back |
| 2026-10-10 | (left) | the weight iteration (`Navarch.Solve`: `ArmourGeometry`, `ArmourWeights`, `HullStructure`, up to 60 passes) makes new layouts and `Weight`s per pass, ~35% of what remains; a fresh `Footprint` per probe (`AddDeckhouseLevels.Ok`, `Footprint.Rect`/`Circle`) ~12% | needs a design change (sums-only passes, or footprint checks that take a box without an object), not pooling |
