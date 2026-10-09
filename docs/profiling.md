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

The game: `src\Fleetwright\bin\Release\net10.0\Fleetwright.exe -ship=shipgen/designs/bismarck.json -screenshot=300`
runs 300 frames and exits, so it traces the same way. To trace an already running game:
`dotnet-trace collect -p <pid> --duration 00:00:10 --format Speedscope -o game.nettrace`.

The `.speedscope.json` also opens in https://www.speedscope.app for a flame graph, and the `.nettrace` in PerfView or
Visual Studio.

## Reading the numbers

- dotnet-trace ends every stack in a pseudo-frame: `CPU_TIME` (running) or `UNMANAGED_CODE_TIME` (native code,
  waiting, idle). The script charges `CPU_TIME` to the method above it and drops idle time from self time.
  Inclusive time still includes waiting.
- Times are summed over threads: a `Parallel.For` body shows its CPU time, about (wall time x cores).
- The first run of a method includes its JIT; tiny methods can look odd. Compare runs, not single samples.
- `PollGCWorker` time is threads parked for a GC: allocation pressure, look for the allocating loop.

## After an optimization

Speed changes must not change output. Check, all `-c Release`:

- `Fleetwright.Shipgen` (design side): `golden-check` stays 371/371.
- `Fleetwright.Shipgen.Render`: `svg-check`, `sprite-check`, `png-check`; `dotnet test` covers the rest.
- Previews (`Bake/Preview.cs`): no golden; compare the preview PNGs byte for byte before and after
  (`design ... --previews --out before` / `after`).

## Log

| Date | Target | Finding | Result |
|---|---|---|---|
| 2026-10-09 | `design bismarck --previews` | `Preview.ShadowMask` ~1.9 s CPU (8 threads), `ShipDesign.Build` ~0.9 s, `GpuBaker` device ~0.2 s, `Preview.Rotate` 0.16 s | baseline, ~3 s wall |
