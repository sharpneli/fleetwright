# Shipgen goldens

Python shipgen's output, captured by `../shipgen/tools/golden.py` at the shipgen tag `golden-capture`
(commit `b867f6bf`). The C# port must reproduce it (PORTING.md, Step 1). Don't edit these by hand. A capture
is replaced only on purpose: after the port, when a bug fix changes the output, one fix per commit.

## Layout

```
../designs/*.json             the 71 designs (copied from ../shipgen/designs)
../designs/fuzz/*.json        300 fuzz mutants (fuzz.mutate, mode all): fuzz_lim_* drawn inside the input
                              limits (seed 1), fuzz_free_* without them (seed 2)
cases.json                    every case: name, design file, base design, limits flag, the mutations
design/<case>.json.gz         design side, Windows Python 3.14.8 (the target libm: UCRT, same as .NET)
design/capture.json           platform, commit, job count, build seconds per case
sprite/<design>/              WSL (cairosvg), scale 10 px/m, mips 5, no previews:
  sprite.json.gz                compared: integers exact, floats 1e-9 relative, shadow.max_height_m within the
                                clutter bound (clutter RNG isn't ported)
  hull.png, height.png, turrets/*.png, hull.svg.gz    visual references only, never compared pixel by pixel
sprite/capture.json           which designs rendered (murica fails validate with limits, so design.py skips it)
```

A design case file holds `validate_limits`, `validate_no_limits` and `looks_validate` (exact strings), then,
when `validate(limits=False)` passes, `build` (the whole ship dict: design, report, hitboxes, render, with the
private `_` keys), `hint` (`length_m` and whether `build(design, hint=length_m)` equals the unhinted build) and
`build_s`. A raise is recorded as `{"raised": {"type", "message"}}`; none occurred.

Totals: 325 cases build, 46 mutants fail validation, no raises. The hinted build differs in 13 cases (fleet_carrier
and 12 mutants). That is allowed: `build()` promises the same result only up to the search's half-percent
tolerance. The C# port should reproduce the same 13.

## Capture checks (2026-10-09)

- **Hash seed:** WSL with `PYTHONHASHSEED=0` and `=1` gave identical output in all 371 cases.
- **Windows vs WSL (glibc):** floats differ by at most 1.0e-13 relative. One discrete difference:
  `fuzz_free_057` (seaplane_carrier with 8 tower levels). `crew.spread()` hands the command party's last man to the
  largest remainder, and the two bridge towers (Tower 5 and Tower 7, the same 3 x 3 m rounded rectangle and 2.6 m
  tall) have volumes equal up to an ulp of the rounded-corner trig in `block_outline`. Windows gives `battle_crew`
  to Tower 5, glibc to Tower 7. If C# disagrees here, check the volume computation's operation order before
  anything else. It's a candidate for an explicit tie-break after the port.

## Performance baseline

Windows, 16 workers in parallel on 32 threads, unhinted build: the 71 designs take median 1.14 s and at most
3.0 s (87.8 s in total); the 254 buildable mutants take median 1.27 s and at most 18.3 s (fuzz_lim_004).

## Regenerating

From `../shipgen` at the tag, with `PYTHONHASHSEED=0`:

```
python tools/golden.py mutants                              # Windows; libm draws the mutant numbers
python tools/golden.py design --platform windows            # Windows
~/.venv/bin/python tools/golden.py sprites                  # WSL
~/.venv/bin/python tools/golden.py design --platform wsl --golden /tmp/wsl
python tools/golden.py diff --tolerant design /tmp/wsl      # the platform check above
```
