# Shipgen goldens

Python shipgen's output, captured by `../shipgen/tools/golden.py` at the shipgen tag `golden-capture`
(commit `b867f6bf`). The C# port must reproduce it (PORTING.md, Step 1). Don't edit these by hand. A capture
is replaced only on purpose: after the port, when a bug fix changes the output, one fix per commit.

Since the port the C# side owns them: a deliberate change rewrites the cases it changes with
`shipgen golden-update` (design records, SVGs and sprite.json; the PNGs stay as visual references). Rewritten so far:

- 2026-10-09, idiomatic C# cleanup: Python's numerics (compensated sum, CPython's hypot, correctly rounded
  `round(x, n)`) gave way to .NET's (`Enumerable.Sum`, `double.Hypot`, `Math.Round`). Last-digit drift in most
  cases, rounding ties going .NET's way, and a few ties among equal values (crew remainders, the order of equal
  height columns) settling differently.
- 2026-10-09, typed design input (System.Text.Json): a lone secondary battery is echoed back as a list of one, and a
  few validation messages read differently (numbers as C# prints them, `null` for Python's `None`, the battery index
  on a lone secondary's missing keys).

- 2026-10-10, one physical model (step 1): the hitboxes gained `bow`, `stern`, `turret_types`, a `mast` component
  per mast, a block's `level`, a funnel's `pipes`, an AA mount's `rest_deg` and the deck-edge elevators' `role`; the
  render spec's masts carry their resolved `top`. Checked: with those removed, every case equals the old capture
  (within the comparer's 1e-9).
- 2026-10-10, the sprites drawn from the hitboxes (step 2): the same drawing, read from the physical model's rounded
  values. 78 SVG cases and 67 sprite.json files rewritten. Checked: every difference was hitbox rounding (at most
  0.003 m in the drawing, 0.01 in `top_m`), height columns of the same grey swapping order, or one grey step (0.25 m)
  flipping on a rounded top in three cases.
- 2026-10-10, the drawing follows the physical model (step 3), on purpose: a look no longer reshapes the hull
  (`bow_power`, `bow_flare`, `transom` are gone; tumblehome draws the deck narrower inside the hull instead of the hull
  wider), the conning tower and casings above the deck are drawn and cast height, AA mounts on a high-pass roof draw
  over it (q_ship, seaplane_carrier), the canvas fits deck-edge elevators, and the height map's deck datum is the
  hitboxes' freeboard. 286 SVG cases and 2 sprite.json files rewritten. `png-check`'s hull and height columns no
  longer compare like with like (Python drew the look's hull): about 0.97, 0.93 with tumblehome, 0 for the two
  carriers whose canvas grew; turrets unchanged.
- 2026-10-10, the render data gone (step 4): `build.render` became `build.dressing` (only what has no physical
  effect; mast dressing by the mast's id), and the style's summary lines moved to `report.summary`. Checked: the
  hitboxes and the rest of the report are unchanged, and the dressing and summary hold exactly the old values.

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
svg/<case>/                   WSL, every case that builds (325), 10 px/m, mips 5, drawn with PortRandom, the
                              C# port's RNG (ShipRng), so clutter, dazzle and vents come out the same on both sides:
  hull.svg.gz, height.svg.gz, turrets/<type>.svg.gz   compared as drawings (SvgDiff): element trees in order,
                                clip paths resolved, colours canonical, numbers within 1.5e-3 (f()'s 3 decimals)
svg/capture.json              which cases were drawn, with max_height_m and the clutter count (shipgen tag
                              golden-svg-capture, commit 2b168877)
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
~/.venv/bin/python tools/golden.py svgs                     # WSL, at golden-svg-capture
~/.venv/bin/python tools/golden.py design --platform wsl --golden /tmp/wsl
python tools/golden.py diff --tolerant design /tmp/wsl      # the platform check above
```
