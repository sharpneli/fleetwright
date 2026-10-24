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

## The battle sim

`shipgen scenario X --play` plays a scenario to its end with the AI on every side, headless (Jutland by default:
250 ships, 14 game hours, all of it in the fine tier). With one seed it ends on **where the time went**: every probe
(`Probes`, the systems' events and the parts they time inside themselves, nested), each one's share of the play, calls
a game second, the time a call and the longest; and the play's allocations and collections. No profiler, so it's the
truth for totals; trace only to find what inside a probe costs, and time a suspect with a temporary `Stopwatch` sum
printed at the end before trusting a trace's split. Start small (`squadrons` ~9 s, `tsushima` ~25 s), then Jutland
(~2.5 min).

```powershell
src\Fleetwright.Shipgen.Cli\bin\Release\net10.0\shipgen.exe scenario Content\Scenarios\jutland.json --play
# the first 70 s of it traced (the file stays manageable), the sim thread alone, who calls a frame
dotnet-trace collect --format Speedscope --duration 00:01:10 -o "$p\jut.nettrace" -- `
    src\Fleetwright.Shipgen.Cli\bin\Release\net10.0\shipgen.exe scenario Content\Scenarios\jutland.json --play
python -I tools/speedscope_top.py "$p\jut.speedscope.json" -n 40 --thread busiest
python -I tools/speedscope_top.py "$p\jut.speedscope.json" --thread busiest --focus "CaptainLayer.Think("
python -I tools/speedscope_top.py "$p\jut.speedscope.json" --thread busiest --focus "ZeroMemoryInternal" --callers
```

## Frame times in the game

`-frametimes` prints, at exit, each frame's length and its CPU work (from the frame's start to the submit, less the
wait for the swapchain): mean, p50, p95, p99 and max, after the first 60 frames. Frame length alone hides the CPU,
since vsync pads every frame to the display's interval. Pair it with `-screenshot=N` to end the run, and with
scripted input to load it: `F:pan:X0,Y0:X1,Y1:N` pushes a real SDL drag (button down, eight motion events a frame
for N frames, up), which reaches the scenes as a mouse does. `F:drag` only reaches ImGui.

```bash
dotnet run --project src/Fleetwright -c DevRelease -- -size=2560x1440 -frametimes -screenshot=1100 -map=50,10,4000     "-input=100:pan:900,500:400,350:80;200:pan:400,350:1400,650:80"
```

Measure the Debug build too (`dotnet run` without `-c`): it's what runs day to day, and it's 3-5x slower.

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
  **Unless nothing is collected:** the sampler itself stops every thread for each sample, and a thread caught on its
  way back from native code parks there. In the battle sim (no collections in play) a third of the sim thread showed as
  `PollGCWorker` under the probes' `Stopwatch.GetTimestamp` calls (`World.Advance`, `SmokeSystem.Tau`): the sampler's
  cost, not the sim's. `--callers` shows where; count it as unattributed.

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
- The battle sim: a `scenario --play` prints the same lines but for the timings (the timeline, the result, the main
  guns' tally): diff its output, less the "played in" and probe lines, before and after, on the scenarios that run the
  code changed (Jutland for anything the AI or a big fight touches). A deterministic sim moves on the slightest change,
  so the same output is a strong check.
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
| 2026-10-11 | `bench` on 11 designs | after the sizing rework (fewer, consistent layout passes but more weight solves): `Navarch.Solve` damped 50/50 over up to 60 passes, cloning every placed weight per call; `HullForm` 400 stations, root tolerance 2^-40; GC ~30% of CPU | secant on the displacement (tolerance 0.5 -> 0.05 t), placed weights cloned once at the end, `Stability` sums in loops, `HullForm` 240 stations / 2 rounds / 2^-24 (hydrostatics within 1.1%, displacements unchanged). 11 designs 0.84 -> 0.66 s, Bismarck 67 -> 20 MB per build. Left: ~4-5 full layouts per build (~29 ms), `Settle` ~15 ms, interior ~13 ms |
| 2026-10-24 | `scenario jutland --play` | 185 s for 14 game hours, no collections in play: ships' step 25%, sighting 21%, captains 20%, smoke 11%, admirals and commander 10%, gunnery 6%, damage 5.5%. The registry's lock on every class read, inflated by the builders' contention (7 s of a 70 s trace); the line of fire's grid visited every ship round a fall, enemies included, sorted, twice a gun; `Matchup.Judge` worked out the attackers 3-6 times and the chosen engagement twice, and a gun's pair tables were a hash asked 516 M times; a captain's fall-back rebuilt the enemy side for every force it weighed, all of them; a battery's pick asked every candidate's line of fire | registry reads lock-free (published arrays); a line-of-fire grid a side, unsorted, one query for a gun's fall; attackers once and the chosen engagement kept; pair tables in an array by the profile's number; fall-back's enemy built once, forces weighed nearest first; the line of fire asked only of a candidate that would be the best yet. 185 -> 139 s, output identical. Left: the ships' step 40 s (arithmetic: 160 ns a ship a tick), sighting 32 s (9.6 s of it the smoke's sight lines), smoke upkeep 18 s, captains 18 s, judgements ~9 s, gunnery 9 s, damage 9 s: per-ship and per-pair work, linear in ships (Tsushima to Jutland), for the job system (docs/battle/plan.md 2.7d) |
