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
- 2026-10-10, tumblehome (`hull.section`), on purpose: `bouvet`, `brennus`, `babel` and `danton` got a physical
  tumblehome in place of the look's. Their hitboxes, reports, SVGs and sprite.json changed (narrower deck, sponsons
  under the side guns, lighter topsides, the hull form's extra rows); every other case is unchanged.
- 2026-10-10, the drawing reads the tumblehome (step 4), on purpose: the look's `tumblehome` key is gone; the deck is
  drawn from the hull form (narrower where the sides lean in), the height map steps up the leaning side, and the
  hitboxes carry `vertical.knuckle`. 4 design and 24 SVG cases rewritten: the four tumblehome designs, `courbet`
  (wall-sided, it lost the look's drawn tumblehome) and the 19 fuzz mutants of the French Victorian designs.
- 2026-10-10, tumblehome physics, on purpose: side armour and the side shell weigh as long as the leaning side,
  armoured bulkheads as wide as the hull up their height, and side guns' barbettes stay inside the deck edge.
  `bouvet` and `babel` go to strength 0.6, `brennus` to 0.4 (at 1 the barbette rule widened her to 34 m). 4 design,
  4 SVG and 4 sprite.json cases rewritten; every other case unchanged.
- 2026-10-10, the citadel bulkheads thinner under the belt, on purpose: half `bulkhead_mm` from the belt's lower edge
  down to the inner bottom. Every armoured design is lighter: standard median −1% (Yamato −7.8%, Bismarck −5.4%), and
  the hitboxes' bulkheads carry `lower_mm`/`lower_top`. 238 design, 477 SVG and 51 sprite.json cases rewritten.
- 2026-10-10, the tumblehome flush with the hull below, on purpose: the side above the waterline is the wall-sided
  ramp (waterline out to the planform at the main deck) leaned in by the topside, not a ramp out to the planform at
  the knuckle, which put a nearly flat shelf just above the water toward the ends. Only the four tumblehome designs'
  hull-form stations (between the waterline and the deck, off the midbody) and their height-map side bands changed;
  the deck edge, layout, weights and sprites are unchanged. 4 design and 4 SVG cases rewritten.
- 2026-10-11, tumblehome shading, on purpose: where the sides lean in, the plan lightens them in five bands from the
  knuckle to the deck (`TopView.ShadeBands`), as the profile shades its side. Only the hull drawing of the four
  tumblehome designs changed (5 white overlays each); the height map and sprite.json are unchanged. 4 SVG cases
  rewritten.
- 2026-10-11, submerged torpedo tubes in the designs that had them, on purpose: `dreadnought` (2 a side, stern,
  5 torpedoes a tube), `nassau` and `seydlitz` (beam, bow, stern), `kongo` (4 a side, 2 a tube), `invincible` (2 a side;
  her stern tube would have to clear the single rudder out of the water), `lion`, `nelson`, `colorado` (1 a side). All
  533 mm. Lengths within 0.1 m, standard displacement up 39-149 t, crew up 9-31. 8 design, SVG and sprite cases
  rewritten.
- 2026-10-11, stern tubes beside the rudder stock, on purpose: a stern tube now steps past a centreline rudder's
  stock instead of rising over the whole rudder, rises only while gear hangs aft of where it leaves the hull, and its
  room may stand a deck up over the steering gear; bow and stern rooms are placed before the beam rooms. `dreadnought`
  gets her twin rudders, `invincible` her stern tube back. 4 design, SVG and sprite cases rewritten.
- 2026-10-12, no berths over the bridge, on purpose: a command stack's levels over its bridge or aft control
  (`Bridge upper`, `Tower n`, `Aft control upper`, island towers) no longer count as crew space. Most cases change
  only `crew.space_usable_m3`/`space_free_m3`; `daring` and `destroyer` grow 1.3 m (+11 t), `fubuki`, `impetuoso`,
  `karlsruhe`, `victory_1944` and some fuzz cases move by centimetres.
- 2026-10-12, unique component ids (battle plan step 2.1), on purpose: the hangar bay is `Hangar bay`, not
  `Hangar`, which the seaplane carrier's hangar block is too. 20 carrier cases change only that id.
- 2026-10-12, the casemate flag (battle plan step 2.1), on purpose: a casemate gun carries `casemate: true` instead
  of `mount: "casemate"`, so a component's `mount` is only a barbette's link to its gun. 83 design cases change only
  that key, on 249 guns; `sprite.json` keeps its `mount: "casemate"`.
- 2026-10-14, the `kronstadt` navy (Russia), on purpose: `sverdlov` moved to `kronstadt`/`cold_war` (its design record's
  look echo and 3 SVGs: the colours), and `borodino` added, the first case since the port (`golden-update` writes a new
  case's goldens; it has no PNGs). Every other case unchanged.
- 2026-10-14, the plan's ink outlines at half weight (docs/visuals/ship-lighting.md L2: `Shapes.Outline`, default
  `Looks.DefaultOutline` 0.5, settled by side-by-side captures), on purpose: the hull and turret SVGs of 313 cases
  draw `Painter.Ln`'s strokes at 0.75 of their width and 0.5 opacity. The side view, design records and sprite.json are
  unchanged. `golden-update` no longer takes a fuzz case missing from the sprite capture for a new one (only the
  shipped designs have sprite goldens; it was drawing them and failing on the unbuildable).
- 2026-10-14, the painted light taken out of the turrets (docs/visuals/ship-lighting.md L3: the lighting carries it
  now), on purpose: the barrels' and tubes' highlight strokes, the roof insets, the face plates' dark shade (the body's
  colour now), the cupolas' highlight dots, the casemate shield's lighter disc and the champignon's lightened rings
  are gone from 317 cases' turret SVGs. Hulls, design records and sprite.json unchanged.
- 2026-10-14, brass (docs/visuals/ship-lighting.md L4: materials), on purpose: the Victorian looks have a `brass`
  palette colour, and a capstan's drum is drawn in it where the look has one (`Painter.Or`), so 44 cases' hull SVGs
  change only those circles' fill (98 of them). Other looks have no brass and draw as before; design records and
  sprite.json unchanged. The material keys colours now carry (`Rgb.Keyed`) aren't written to the SVG.
- 2026-10-15, lighter plants at full rating (docs/shipgen/DECISIONS.md, "Destroyer crunch" 1), on purpose: the
  designs carrying ST5 or ST6 plants have their `stress_floor` lowered (0.35 → 0.30, 0.42 → 0.33). Those 28 design
  cases change by their rating (tb_destroyer -19 %, fubuki -17 %, battleships under 2 %); every other case unchanged.
- 2026-10-15, deck secondaries clear what stands beside them (docs/shipgen/DECISIONS.md, "Destroyer crunch" 3), on
  purpose: whether a position has room is read where it stands instead of from the widest block anywhere. 87 cases
  change (19 designs and 68 fuzz cases): positions on narrow decks, beams (Dunkerque 30.95 -> 26.67 m, Impetuoso
  14.6 -> 10.7 m) and what follows from them; where the deck is wide the mounts stand where they did.
- 2026-10-15, engineering crew follows the plant's size (docs/shipgen/DECISIONS.md, "Destroyer crunch" 4), on
  purpose: `crew_k * (MW * stress_mult)^0.75`. 293 cases change, every design with a rating over 0: fewer men below,
  less crew weight and space, and where crew space set the length a shorter ship (destroyer 124.5 -> 112.1 m, Fubuki
  137 -> 122 m); capital ships about -0.2 %.
- 2026-10-16, the magazines weigh the real shells (docs/battle/shells-armour-damage.md S3), on purpose: a battery's
  rounds weigh its outfit's mean shell (the templates' k·D³, or the default 1912 standard family's) times AmmoMult,
  instead of the 16" Mk 8's W/D³ for every gun; ready-use rounds the same. 315 cases change (68 designs, 247 fuzz):
  every shipped design lighter, median -1.5 % standard (victory_1944 -5.3 %, doom -4.7 %, duilio -3.6 %, colorado
  -2.8 %), and the SVGs and sprite.json of the 311 and 69 whose hulls moved. One valid fuzz mutant grows by a sizing
  jump (fuzz_lim_014 +5.8 %, 256 -> 269 m) and one invalid one (fuzz_lim_139).
- 2026-10-16, magazines under the waterline (docs/shipgen/protection-plan.md H1), on purpose: crowns 0.4 m under the
  design waterline, longer instead of taller, platform decks over them and handling rooms above. 293 design cases
  change (median 0.0 %, -1.4 to +2.4 % standard: victory_1944 +2.4 %, Daring and Impetuoso -1.7 m), and the SVGs and
  sprite.json of the 126 and 29 whose trim or sizes moved.
- 2026-10-16, magazine wings (docs/shipgen/protection-plan.md H2), on purpose: a wing of stores 0.1 B wide between each
  magazine and the side, magazines narrower and a little longer. 286 design cases change (median 0.0 %, -2.1 to +1.4 %
  standard), and the SVGs and sprite.json of the 170 and 37 whose sizes or trim moved.
- 2026-10-16, bulkheads by kind and TDS layers (docs/shipgen/protection-plan.md H3), on purpose: collision bulkheads
  1.5 × the gauge, main and armoured ones with a 1.3 × lowest strake (`plate_low_mm`, `plate_low_top`). 326 design
  cases' hitboxes change; sizes, SVGs and sprite.json don't (one TDS layer by default: as before).
- 2026-10-16, armour widths exported (docs/shipgen/protection-plan.md H5b), on purpose: armour decks and bulkheads
  narrower than the hull carry `w` (the steering gear's box, the end belts' stern faces). 131 design cases' hitboxes
  change; nothing else.
- 2026-10-16, TDS cells are the TDS's own, a bug fix: a room whose box reached past the holding bulkhead took the TDS
  cells there (Richelieu's engine room 3, the carriers' aviation fuel; with layers, a boiler room took the innermost
  layer, so a hit that flooded it flooded the boiler room through an open join). 14 design cases' hitboxes change
  (rooms' cells, battle crew); nothing else.
- 2026-10-16, subdivision checks (docs/shipgen/protection-plan.md H7), on purpose: a room is one compartment however
  long (a station merged a few cm off a room's end had split engine rooms and long magazines in two), the report gains
  `compartment_standard` and, where she floats with fewer adjacent compartments flooded than her type should, a
  warning; the hitboxes gain `doors_below`. 326 design cases change; no size moves. ShipDesign.Version 16.
- 2026-10-16, the shipped designs on their protection schemes (docs/shipgen/protection-plan.md H8a), on purpose: 30
  designs' armour moves (slopes, inclined and internal belts, inner bulkheads, Kent's boxes, KGV's zones), and they grow
  (docs/shipgen/protection-templates.md has the table); the hull girder counts a sloped deck's flat over its share.
  30 design cases with their SVGs and sprite.json change. ShipDesign.Version 17.
- 2026-10-18, fire control as tech blocks (docs/battle/fire-control.md F3), on purpose: design format 6 (each battery's
  `fit`, the design's `fire_control_tech`), so the 326 design cases' design echo changes (version, the fits, the
  blocks); nothing built moves. ShipDesign.Version 30; then the real fits on 10 shipped designs (their echo only), 31.
- 2026-10-18, masts and tops from the design (docs/battle/fire-control.md F4a), on purpose: `masts` gives each mast's
  kind and top (the shipped warships' written from what their looks drew), tops are hitbox parts (`top`) at the
  heights the rule gives (2.25 x the highest gun over the water), mast weights go by kind, and the plan and profile
  draw the kind and the tops from the ship. 326 design cases change (median 0.0 %, at most +2.5 % standard: fubuki's
  two-tier tops), with 247 SVG and 46 sprite.json cases. ShipDesign.Version 32.

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
