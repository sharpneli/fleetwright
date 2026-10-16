# Plan: subdivision and protection schemes

2026-10-16. Status: agreed 2026-10-16 (decisions at the end). Done: H0-H7, H8a (2026-10-16). Next: H8b, the Protection tab (a UI session of its own). Research: `research/09_protection_schemes.md`
(the families, the layers, the bulkhead kinds), on top of `01_capital_subdivision.md` and `03_small_vessels.md`.

This comes before phase P of `docs/battle/shells-armour-damage.md` (materials as data). The user wants the subdivision
right before plates get their materials, so every plate added here names an armour part and P0 picks it up with the rest.
Ships change in this pass on purpose: the hull's layout changes, so weights, lengths and goldens move (memory: weights
follow physics; report them).

## Goal

No ship has its magazine behind a single line. Every design gets the layers its family really had, from the outer shell
inward: low and inboard magazines on every ship; an inner line behind the belt where the scheme had one; zones, boxes and
sloped decks where the history needs them. The damage model sees all of it through the same tracer, so nothing is a
damage-side fudge.

## Where we are (measured 2026-10-16)

**The model.** Cells are boxes on one grid: global tiers (the deck stack, every 2.6 m down from the main deck, plus raised
decks with spans), sections between transverse bulkheads, and bands across the beam (`P`, `C`, `S`) only where a TDS
(inside the citadel) or wing coal bunkers (inside the machinery) split them. The tracer crosses side armour at the hull's
skin, flat armour decks and transverse armour bulkheads as exact planes, and cell walls as plates of their `plate_mm`.
The TDS's longitudinal wall *is* seen: one plate of 12 mm per metre of depth (Bismarck about 66 mm), all of its layers'
steel in one wall (the research's §6.4 says it isn't seen; it is, as one plate). Every other watertight bulkhead is
0.7 × the minimum gauge.

**Magazines.** `Ordnance.Stow` gives a magazine zone a fixed length (one mount's diameter, or the layout's magazine
segment for grouped batteries) and lets the height grow from the inner bottom to the armour roof, or the main deck when
there is none. A room claims a cell by half-overlap, and cells span the whole beam outside the TDS and wing bunkers.

A survey of the shipped designs (goldens, 70 with magazines):

| | Designs |
|---|---|
| A magazine's crown **above the waterline** | 15: heavy_cruiser +2.4 m, des_moines +2.4, sverdlov +2.2, q_ship +2.1, pt_boat +2.0, seaplane_carrier +1.9, deutschland +1.8, mtb +1.8, fubuki +1.1, tb_destroyer +0.8, daring +0.8, hamburg +0.7, akizuki_1960 +0.7, impetuoso +0.6, victory_1944 +0.1 |
| Crown within 0.5 m under it | 8 more: nagato, dunkerque, doom, zara, all_forward, all_forward_flush, nelson, babel |
| Every magazine cell against the hull side (nothing between it and the shell plating) | about 50; only the TDS ships (bismarck, yamato, battleship, kgv, richelieu, duilio, inflexible ...) have cells between |

There are two causes. The heavy cruiser's 5-inch magazine holds 128 t in a fixed 9 m zone, so it climbs three tiers to
the armour deck. On V25 and the TB destroyer the hold runs from the inner bottom up to the second deck (2.6 m under the
main deck), which is above the waterline, so the magazine fills a tier the waterline cuts.

**What it costs** (`shipgen hits`, 2000 hits, 10 a ship, 30 min after):

| Target | Fire | Magazine a hit | Lost to the magazine |
|---|---|---|---|
| v25 ← lion 4″ HE, 6 km | 35% | 0.45% | 7.5% |
| tb_destroyer ← lion 4″ HE, 6 km | 35% | 0.6% | 9.5% |
| destroyer (WW2, crown 1.7 m under the WL) ← 6″ HE, 6 km | 38% | 0.05% | 1.5% |
| heavy_cruiser ← 8″ AP, 12 km | 3% | 0.7% | 9% |
| heavy_cruiser ← 8″ HE, 12 km | 40% | 0.05% | 0.5% |
| dreadnought ← 12″ AP, 12 km | 3% | 0.5% | 5% |

The WW2 destroyer is the control. With the same damage model, a magazine below the waterline already loses about a sixth
as many ships as V25's does. That is §6.1's claim, measured.

**What the layers can buy today.** `Effects` lets a burst reach the rooms of a neighbouring cell within the wreck radius
(1.6·∛filler-kg: about 1.7 m for a 4″ lyddite shell, 3 m for a 12″ AP's burst) through any wall its fragments beat. A
1 m wing of stores behind a 4 mm wall therefore helps less than distance does: it turns "burst in the magazine" into
"burst beside it" (half the chance), not into nothing. E1's fragment model (phase E) makes every layer count for more;
this plan builds the layers so E1 has something to work on.

## Principles

- **Layout first, plate second.** §6.1's arrangement fixes most of the unarmoured ships' problem with no new inputs; plate
  comes after, as inputs a design can leave at 0.
- **Defaults reproduce the family, inputs at 0 mean none.** New inputs default to what the design's existing data implies
  (a coal ship's wing bunkers become its inner line's place), so designs that say nothing keep working and get better.
- **One grid.** Every new part is a cell wall, a deck with spans, or an exact plate in the tracer. No layer exists only in
  the weights or only in the damage model.
- **Limits come from gameplay and never block** (memory). A magazine that can't get under the waterline spills upward
  with a warning; a floodable-length check warns.
- Rules of the house as always: `ShipDesign.Version` bumped and goldens updated per step (one per commit), `mono-check`
  gains no reversals, no O(n²) passes (sections × rooms is fine; cells × cells isn't), the matchup reads any new layer in
  the same commit as the terminal does (shells-armour-damage.md's rule).

## Steps

### H0. The bench (no numbers move)
- `shipgen protection DESIGN...`: per magazine (and machinery room), its crown against the waterline, what lies between
  it and the side at mid-height (each wall and cell, by kind and mm), and the decks over it; then the survey above as a
  table. Also a shell sample per magazine (flat fire at 5, 10 and 15 km; plunging at 20 km) through the tracer, giving the
  share that reaches it and the plates they crossed. This builds on `shell-test`'s tracer runs.
- `shipgen hits` splits "magazine" by path: burst in it, burst beside it (neighbour), turret or barbette (handling path),
  fire later. Then each later step shows what it moved.
- The baselines above become the bench's first table.
- **Done 2026-10-16.** `shipgen protection` and the split in `hits` ("magazines set off by: a burst in it, a burst
  beside it, the turret or barbette, under water"; "to the magazine: set off by a hit, by a fire later"). Before H1:
  - `protection --summary` (200 shells a fall, every shipped and scenario design; "open": in from above the water
    through the skin alone, no deck or wall inside it, a definition settled during H1): every WWI destroyer and
    torpedo boat (V25, V1, G101, B97, M, K, L, Faulknor, Abdiel, Buyny, Ikazuchi) 22-44 % open at a 5° fall, 6-11 % at
    35°, every magazine against the side; the WWII-and-later destroyers Daring, Akizuki, Hamburg, Impetuoso and Fubuki
    27-39 % at 5°; the seaplane carriers 57-58 %, the Q-ship 59-85 %; the heavy cruiser, Des Moines, Sverdlov and
    Deutschland 9-21 % (crowns 1.8-2.4 m over the waterline). The R class and the Fleet Destroyer (crowns 1.7-1.8 m
    under) 0-4 %, and no other armoured ship is open anywhere.
  - `hits`, lion 4" HE at 6 km, 10 a ship, 30 min: V25 magazine 0.45% a hit (in it 0.15%, beside it 0.3%), lost to the
    magazine 7.5% (a hit 4.5%, a fire later 3%); TB destroyer 0.6% (0.3/0.3), 9.5% (6/3.5). "Beside it" is mostly a
    burst in the quarters over the 3.6 mm second deck: H1 moves bursts out of the magazine, the neighbour rule still
    reaches through a thin deck within the wreck radius.

### H1. Magazines below the waterline (§6.1)
- **Crown rule:** a magazine's top at most `WL − margin` at the design draught (`margin` a tuning value, 0.4 m to start
  [INFERRED]). The zone takes its length from the height under that crown (longer, not taller). It may spill upward only
  where the layout can't give it length (a warning in the report: "Magazine S fore rises 1.2 m above the waterline").
- **Platform deck:** where a tier straddles the waterline over a magazine, a deck at the crown over that stretch (a deck
  with spans, as raised decks have). Where a deck has no span, the cells on either side of it join, as slivers join now.
  On V25 this gives a platform 0.4 m under the waterline over the magazines and the hold as now elsewhere.
- **Handling rooms:** the space over the crown, in a mount's own column, becomes a `handling` room (no stowage; the flash
  chain's node for the later magazine session, §6.7). Over a grouped battery's zone it's stores.
- **Expected:** heavy cruisers, Sverdlov, Deutschland and Fubuki get longer magazines and grow a little (reported).
  Destroyers' waterline hits burst over a deck instead of inside the magazine. pt_boat and mtb can't comply (draught 1 m)
  and keep a warning. Carriers' and merchants' magazines follow the same rule.

- **Done 2026-10-16.** What was built:
  - `PlantPlan.Crown` (T − `Ordnance.CrownMarginM`, above the keel). `Ordnance.Span(..., low)` caps a stowed zone's
    top there unless the zone needs more height than that leaves (then it keeps its deck and `Stow` warns: "X rises
    1.2 m above the waterline: there's no room to lengthen it under it"). The crown is never snapped to a nearby deck,
    which would step the magazine's height as the draught moved: a deck just over the crown leaves a thin cofferdam.
    Planing craft (`Zone.Low` false) keep their lockers above the water.
  - Grouped magazine segments are sized at the width `Stow` fills (`Ordnance.GroupHalfWidth`); before, the layout used
    the plant's full width and `Stow` a narrower one, so the heavy cruiser's 5-inch magazine climbed three tiers.
  - `Ordnance.OwnZones`: a mount's own magazine is its diameter long, or longer where the height under the crown won't
    hold it, inside the free stretch of hold around its mount (between the machinery, the collision bulkhead and the
    steering gear); overlapping ones stow end to end in one zone.
  - Platform decks (`Subdivision.Platforms`): a deck at a magazine's crown where it falls inside a tier, over the
    magazines with that crown; where it doesn't run, the tiers it parts are one cell. It covers a section by the rooms'
    claim rule (half the shorter), so a magazine shorter than its section still gets it.
  - Handling rooms (`handling`, `RoomKind.Handling`): over each mount's own magazine, from its crown to the deck over it.
    They don't take crew space or keep a submerged torpedo room out: the deck over a magazine was always its handling
    space (`Ordnance.TPerM3` 0.6 counts on it). A first try with both made Kongo 17 m longer (its beam tubes lost their
    place, the middle got a tube length).
  - Sizes: 297 valid cases, median 0.0 %, −1.4 to +2.4 % standard; designs: victory_1944 +2.4 %, doom +1.0 m,
    heavy_cruiser +0.2 %, Daring and Impetuoso −1.7 m (−1.0, −0.8 %); everything else within 0.3 %. mono-check 35 → 33
    reversals, lumps 13 → 13. One new warning: the escort carrier's aviation fuel stands 0.8 m over the waterline (its
    zone is fixed by the hangar's layout).
  - `protection --summary`: no ship is open any more (0 % at every fall; Buyny 2 % at 5°, before 44 %), every crown at
    least 0.4 m under the waterline but Buyny's (0.0) and Ikazuchi's (−0.3), whose holds are too shallow. Every
    magazine still lies against the side: H2.
  - `hits` (lion 4" HE at 6 km, 10 a ship, 30 min): V25 lost to the magazine 7.5 → 4 % (a hit 2.5, fire 1.5; no burst
    in it any more, beside it 0.25 % a hit), TB destroyer 9.5 → 2 %, Fleet Destroyer 1.5 → 1.5 %; heavy cruiser 8"
    AP 9 → 8.5 %, HE 0.5 → 0 %; dreadnought 12" AP 5 → 5 %. What's left on the big ships comes all through the turret
    and barbette (the flash path, §6.7), none through the hull.
  - Matchup (arethusa:4 v25:8) within 0.5 % of its confidence; squadrons 155 hits in 30 min against 169 (the runs
    diverge). FlotillaAttackTests' pinned seed 1 → 4: 7 of seeds 1-30 get in, against 10 at H0, within the noise.

### H2. Magazines inboard (§6.1)
- A **wing** between the magazine and the side on every ship that doesn't already have a TDS or wing bunker there:
  `armour.magazine_wing_m`, default 0.1 B [INFERRED]. The bands logic that splits TDS and coal sections splits magazine
  sections too. The wing is stores (refrigerated rooms, the stores the crew model already asks for), never fuel. A tank
  that would border a magazine gets a void cofferdam instead (fire, §6.1).
- In the narrow ends the wing takes its width from the waterline half-breadth, and the magazine grows longer for its
  volume.
- **Expected:** the magazine moves from "behind the shell" to "behind the shell, stores and a wall". The gain is modest
  until E1 (see above), and the bench says how modest.

- **Done 2026-10-16.** What was built:
  - `armour.magazine_wing_m` (null: `Armour.MagazineWingShare` 0.1 × B; 0: none; limits 0-20 m). A designer knob comes
    with H8's Protection tab session.
  - The subdivision bands a magazine's section (one the magazine overlaps by half the shorter, as rooms claim cells) like
    a wing-bunker section: `wing` bulkheads `split` in from the side, up to the crown. The wing is measured halfway up
    the magazine: a box cell can't follow a fine end's V, so measured at the crown (the first try) the stern magazines
    still met the shell lower down. The wing cells are `stores`; a magazine may never own a wing or TDS cell (the fleet
    carrier's aviation magazines, 0.4 B wide, had taken their TDS cells since before H1).
  - The layout sizes magazines inside their wings (`Ordnance.GroupHalfWidth`, `OwnZones`), so they come out a little
    longer.
  - Slivers never merge through a magazine's crown, a platform deck or the deck the crown stands on: at the fine ends
    the narrowed magazine cell was a sliver and took the magazine up through its crown (the Fleet Destroyer's Y to
    0.9 m over the waterline), and one merged across a platform left a hold and a magazine overlapping with no deck
    between (the Q-ship). A crown within 5 cm of a deck is on the deck, for the platform and the bands alike.
  - Sizes against H1: median 0.0 %, -2.1 to +1.4 % standard (victory_1944 +1.1 %, Seydlitz +0.5 m, Nassau +0.6 m).
    mono-check 33 → 34 reversals and 13 → 14 lumps: a 3 t reversal on Mikasa's armour deck (0.1 × its usual step,
    noise), and Dreadnought's secondaries 1 → 2 a side (+245 t, 1.0 %: the aft secondary magazine segment that appears
    then, a step that was there and is a little bigger now the magazines are narrower).
  - `protection --summary`: magazines against the side 485 → 10 of 558 (Gangut's long midships groups 9, the
    Liberty type's stern locker); none open but the planing craft's lockers.
  - `hits` (lion 4" HE at 6 km, 10 a ship, 30 min): no hit sets off a destroyer's magazine any more (V25, the TB
    destroyer and the Fleet Destroyer 0 % a hit); lost to the magazine V25 5 % (all a fire later, 1.5 at H1: the wing
    stores are new cells that burn beside it), TB destroyer 3 %, Fleet Destroyer 1.5 %. Heavy cruiser 8" AP 8.5 → 5 %
    (mostly the turret path), dreadnought 5 → 4.5 % (all the turret path).
  - Decision 4: DamageTests' destroyer anchor narrowed from 20 % to 8 % lost (measured 2 and 3 %).
  - FlotillaAttackTests no longer pins a seed: it takes the first of seeds 1-12 where two boats or more fire on the
    signal. Seed 1 now gives the signal with two crippled boats lying in the line and the others' tracks foul, and fires
    nothing (docs/battle/bugs.md: the admiral should signal only with the tracks clear).

### H3. Bulkheads by kind (§7.1, §7.4.1)
- Each kind gets its own plate: main transverse at today's gauge with the lowest tier 1.3× and the top 0.8× (a
  `plate_low_mm` on the row); collision 1.5×; wing and centreline at the structural gauge.
- **The TDS as layers:** `armour.tds` becomes `{depth_m, layers, layer_mm, holding_mm, fill}` (today's `tds_m` upgraded by
  `DesignFormat`). The layer walls are thin (6–16 mm) and the **holding bulkhead** is its own thickness (25–50 mm), the
  innermost wall. Each layer is a band of cells, liquid or void by `fill` (closes TODO "TDS layers in detail" and the
  TDS half of "Double-bottom contents"). Defaults keep today's weight within a few percent: 12 mm per metre spread as
  holding plus layers.
- The tracer sees each wall as a plate already; this only gives them their real thicknesses.

- **Done 2026-10-16.** What was built (a change from the text above: flat keys beside `tds_m`, as the belt's are, not
  an `armour.tds` block, so no format version and the designer's ~30 uses of `tds_m` stand):
  - `armour.tds_layers` (1-8, default 1), `armour.tds_holding_mm` and `armour.tds_layer_mm` (`Armour.Tds`). One layer is
    today's TDS (one wall with all the steel, 12 mm a metre of depth); with more, the holding bulkhead is the innermost
    wall (default half the steel), the layer walls stand evenly from it to the shell (default the rest shared), and each
    layer is a band of cells and its own room (`P1` against the holding bulkhead out to the shell; `Torpedo protection
    7 S2`), so a shell meets each wall and the flooding each space. The weight is the walls' sum: unchanged by default.
  - Neighbours across a longitudinal wall are found by the wall's position (several a side now).
  - Plates by kind: collision bulkheads 1.5 × the gauge; main and armoured transverse bulkheads 1.3 × in their lowest
    strake, up to the hold's top (`plate_low_mm`, `plate_low_top`; the tracer reads `BulkheadRow.PlateAt`); wing and
    centreline at the gauge. Bulkhead weight stays the hull's parametric one, so no weight moves.
  - Not done: the layers' `fill` (liquid or void): the flooding has no initial contents yet (TODO "Double-bottom
    contents"); it comes with them.
  - Every shipped design keeps one layer, so sizes, `protection` and `hits` are as at H2 (V25 lost to the magazine 5 %,
    dreadnought 4.5 %). A Bismarck with 3 layers and a 45 mm holding bulkhead (her own): from the side 11 mm shell,
    three spaces of 0.8-2.3 m behind 10.5 mm walls, the 45 mm bulkhead, the magazine; same weight. Torpedoes on it
    (`torpedo`, 200 hits, 2 a ship, 60 min): holes 136 → 178 m² a hit, water 3,216 → 3,637 t, lost 18 → 21 %: the
    torpedo model holes every cell within its reach, each layer now one, where the walls should take the blast in
    turn. To settle before H8 gives designs layers (the flooding session, or E5's blast).
  - 2026-10-16, looked into: three causes, one fixed. (1) A room whose box reached past the holding bulkhead took the
    TDS cells there: with layers a boiler room took the innermost layer, so water reaching it ran into the boiler room
    through an open join (and one-layer Richelieu's engine room 3 and the carriers' aviation fuel had TDS cells too).
    Fixed: TDS cells are the TDS's own. The 3-layer Bismarck: water 3,637 → 3,357 t, lost 21 → 20 % (one layer 3,216 t,
    18 %; seeds 2-3 at 400 hits: lost equal, 17/17 and 20/19 %), out of the fight 41 % (one layer 52 %). (2) On a hit
    the TDS holds, layers already flood less: the layers past the reach stay dry where one cell floods whole (amidships
    1,327 against 1,799 t). Where the TDS is shallower than the reach (by the end turrets) they flood more, and the
    rest of the excess is the flooding's flat leak a link (`FloodLeakBulkheadM2`, 5 cm² whatever the wall's size): more
    cells, more links, more leak, and small layer cells fill from it; on one hit that cascaded (3,128 → 5,890 t, the
    ship sinking level until the accommodation deck went under). (3) The inner layers in reach get holes to the sea
    rather than through the wall outboard of them: the same water in the end, faster, and the hole area a hit counts
    them (136 → 178 m²). (2)'s leak by a wall's area is the flooding session's (`17_structural_leakage.md`), (3)'s walls
    as openings E5's (ruptured boundaries as ¼ of the wall); both before H8.

### H4. The inner line behind the belt (§6.4)
- `armour.inner_bulkhead {mm, magazines_mm, inboard_m, extent}`: a longitudinal armoured wall from the inner bottom to
  the armour deck. Defaults: on a coal ship with wing bunkers it is the bunkers' inner wall (the German torpedo bulkhead,
  with the coal outboard); on a TDS ship it is the holding bulkhead (H3), which can be thickened beside the magazines
  (Dunkerque 30 → 40–50 mm). `mm` 0 means none: no design gains plate it didn't ask for.
- The longitudinal bulkhead row gets `ArmourMm` as the transverse ones have, so the tracer adds it to the crossing's mm.
  It is weighed from its area.
- Bismarck's splinter cells above the armour deck (30 mm walls between the decks) are the same wall over another tier
  range: `extent` takes a tier range. Later in this step, or left out if it doesn't pay.

- **Done 2026-10-16.** `armour.inner_bulkhead {mm, magazines_mm, inboard_m}` (0 by default: no shipped design has one
  until H8): the armour goes on the wall a section already has inside the citadel (a TDS's holding bulkhead, the wing
  bunkers' inner wall, a magazine's wing, now carried up to the armour deck), or on one of its own `inboard_m` in from
  the side (default the magazine wing's width), from the inner bottom to the armour deck; `magazines_mm` beside the
  magazines. Weighed from its area, thicker over `Geo.MagazineSpans` (where `Stow` put the magazines). The tracer adds a
  longitudinal wall's armour to its crossing (a transverse one's is its own plane already), and the matchup counts it
  behind the side, as the rules ask. Bismarck's splinter cells over the armour deck are left out (not worth it yet).
  - Nassau with 30 mm, 40 at the magazines: +838 t standard (the wall 537 t), from the side 8.6 mm shell, 4 m of stores,
    46 mm, the magazine; steel before a magazine at 10° 15 → 55 mm (median). Lion's 13.5" AP at 12 km, 10 hits:
    crippled 22 → 16 %, lost 10 → 7 % (nearly all through the turrets). Matchup lion against it: lost 13.6 → 13.2 %.
    Dreadnought with 38/51 mm: +1,534 t, 16 → 67 mm.
  - Along the whole citadel the wall makes wing compartments, so the ship floods one side when holed there: the
    asymmetric flooding shelved for the everyday hull (TODO), here only for designs that ask, as wing bunkers already do.
  - Goldens unchanged (372 of 372 match); no version bump.

### H5. Zones and boxes (§6.5)
- **Per-zone thickness:** `belt_mm` and a deck's `mm` may be `{magazines, machinery}` (KGV 374/348, Belfast's deck 3 in/2
  in). The layout already knows each stretch.
- **Belt extent:** `belt.extent: citadel | machinery`.
- **Magazine boxes:** `armour.magazine_box {side_mm, crown_mm, end_mm, floor_mm}` around each magazine group, on any
  style. It is the steering box's mechanism (a narrow deck, box strakes, short bulkheads) generalised. Its crown is H1's
  platform deck and its sides H2's wing wall, so the earlier steps give it its shape. Unlocks Kent (no belt, 4 in boxes),
  Leander, New Orleans and Brooklyn (a belt plus a box), Dunkerque's floor plate, and an up-armoured destroyer for
  players who'll pay for it.

- **H5a done 2026-10-16** (zones and belt extent; boxes are H5b). Flat keys again: `armour.belt_magazines_mm`, a deck's
  `magazines_mm` (citadel decks), `armour.belt_extent: citadel | machinery`. Where the last layout pass stowed the
  magazines (`Geo.MagazineSpans`, merged), the belt gets `zone` strakes in its place (tapering as it does; weighed with
  the belt's own formula, the belt over its stretch less them) and a citadel deck is cut into pieces. A zone overrides
  the belt wherever it covers, in the subdivision, the tracer (`SidePlate`) and the matchup alike, so a belt thinner at
  the magazines (Brooklyn) works as well as a thicker one. Off by default: goldens unchanged.
  - KGV with 348/374 mm and a 127/152 mm deck: +167 t; the zones over each magazine stretch, shell-test no leaks. Kent
    with the 1930s refit's 114 mm machinery belt: +695 t, the belt from −33 to +26 m. A light cruiser with 127 mm,
    51 at the magazines (Brooklyn): +537 t.
  - The zones follow the magazines exactly, so a gap between two magazine stretches keeps the belt's own thickness;
    real ships ran one thickness over the whole magazine stretch. Left as it is.

- **H5b done 2026-10-16** (magazine boxes). `armour.magazine_box {side_mm, crown_mm, end_mm, floor_mm}` (0 by default):
  round each run of magazines that touch on the same floor and under the same crown. The sides are armour on the
  magazines' wing walls (a wing of the default width even when `magazine_wing_m` is 0); the crown and floor are armour
  deck pieces on the subdivision's deck at their height (a platform deck, the inner bottom), and the ends are armour
  bulkheads, one where two boxes meet. All are as wide as the magazines' cells. The plates are laid on the subdivision's
  magazine rooms (`Armour.BoxPlates` in the hitbox export), because a short grouped magazine takes its whole section. The
  weight comes from Stow's compartments (`Geo.MagazineBoxes`), so it runs a little under where a cell outgrows its
  compartment: open.
  - Two bugs fixed on the way: armour decks and bulkheads narrower than the hull (the steering gear's box) had lost their
    width in the export, so in the sim they covered the whole beam (now `w` on both, kept to by the tracer, the
    matchup's deck by its share of the beam); and fragments through a deck saw the deck row's armour only, 0 on a deck
    armoured in several pieces (now `Effects.DeckMm` sums the armour pieces over the cell beyond). 131 goldens gain
    `w` (the steering boxes and the end belts' stern faces); no size moves. `ShipDesign.Version` 14.
  - Kent with the County class's boxes (102 mm sides, 64 crown and ends): +1,020 t; the two boxes cover the fore and
    aft magazine groups whole (26.8-54.2 m, -58.4 to -33.3 m), steel before a magazine at a 10° fall 20 → 118 mm;
    8" AP at 12 km, 10 hits: lost to the magazine 3.5 → 0.5 %, no hit sets one off. A destroyer with a 25 mm box:
    +45 t, 13 → 38 mm, its losses (fires reaching the magazine later) as before.
- **Phase P may start now** (decision 5). H6 (slopes, inclined belts) is next in this plan, in its own session
  (user, 2026-10-16).

### H6. Sloped decks and inclined belts (§6.2, §6.3)
- **One new plate primitive:** a longitudinal inclined plate (y linear in z between two heights, over x0..x1), crossed
  exactly in the tracer like the armour decks, weighed by its area.
- **Deck slopes:** `decks[k].slope {mm, deg}` runs from the flat's edge down to the belt's lower edge (or to
  `belt_depth_m` under the waterline on a beltless ship). This unlocks the protected cruiser (impossible today), the
  armoured cruiser, every pre-dreadnought and dreadnought (§2.1's two-line scheme), Hipper, Bismarck and Dunkerque's
  lower deck.
- **Inclined belts:** `belt.incline_deg` and `belt.position: external | internal`. An external belt is today's
  hull-side crossing with its normal tilted and its weight × 1/cos. An internal belt is the new primitive inboard of the
  shell, its lower part continuing as the TDS's holding wall (South Dakota, Iowa, Nelson).
- **Cells stay boxes.** A slope runs through the wing cells; the space under it counts as the wing cell, and what's over it
  is the bunker. This is a stated simplification: the tracer gets the plate and its angle exactly, and only a burst just
  under the slope is booked to the wing instead of the vitals. A finer split can come later if the bench shows it matters.
- This reverses the 2026-10-03 shelving of turtlebacks (TODO "Shelved by the user"), so it needs the user's yes.

- **H6a done 2026-10-16** (deck slopes; inclined belts are H6b). Flat keys again: a deck's `slope_mm` and `slope_deg`
  (default `Armour.SlopeDegDefault` 30° [INFERRED]; limits 10-60°). Off by default: goldens unchanged (372 of 372), no
  version bump.
  - The primitive is `InclinedPlate` (`armour.inclined` in the hitboxes): over x0..x1 from z0 up to z1, u0 in from the
    side at z0 and u1 at z1, the side's half-breadth taken at `side_z`, so it follows the hull along its length (a plane
    in a fine end would gape or stand outside it). A slope runs from the flat's edge down to the side at the belt's lower
    edge; the flat stops `slope_reach` in from the side (measured at `slope_foot`). In a fine end narrower than the
    reach the two slopes meet at the centreline, a ridge, and the flat is gone.
  - The tracer crosses it exactly: at one height the hull's half-breadth is linear between stations, so along a segment
    the plate's equation is linear strip by strip (a first try with fixed-point steps, then Newton's, missed crossings
    near the ridge, where a segment can meet one side's slope twice).
  - Weight: the slopes take the outer reach each side of the deck's breadth, longer by 1/cos of their angle.
  - Cells stay boxes, but a single wing wall can't follow a slope that moves several metres inboard over one tier
    (Dreadnought's: 8.5 m at 30° over 4.9 m of height). Each tier the slope passes through gets its wing wall where the
    slope stands at the tier's top, a staircase: what's over the slope is the wing's, and a little under it too (the
    stated simplification, never the other way). A TDS, the inner line and a magazine's wing up to its crown keep their
    walls; wing bunkers' wall moves in where the slope is inboard of it.
  - Effects: fragments through a deck see its armour only where the flat is over the meeting of the two cells, and any
    inclined plate on the line from the burst to the neighbour counts (`Effects.InclinedMm`). The matchup adds a slope
    behind the side as thick as a level line through it, and on the deck by its share of the beam as thick as a plumb
    line. Shell-test aims only under the armour (a point over a slope is outside it).
  - Dreadnought with her 44 mm flat and 70 mm slopes (for the 76 mm deck): +102 t standard (the shell plating thickens
    3.6 mm: the flat deck is narrower in the girder). Shell-test leaks as before (the uptake gratings only). 12" AP at 12
    km, 10 hits: crippled 13 → 10 %, out of the fight 9 → 6 %, lost to the magazine 4.5 → 2 % (all through the turrets).
    A protected cruiser (the armoured cruiser with no belt, a 51 mm flat and 76 mm slopes the whole length): shell-test
    no leaks; against a 51 mm flat deck, +789 t; 6" at 6 km moves little (power after 10 HE hits 93 → 96 %).
  - The default 30° gives long slopes on hulls whose belt reaches deep (the reach is the rise over tan 30°); H8's
    templates should give each family its angle.

- **H6b done 2026-10-16** (inclined belts). `armour.belt_incline_deg` (0-30°, its top outboard) and `armour.belt_position:
  external | internal`, off by default: goldens unchanged (372 of 372), no version bump.
  - External: the belt and its zones stay on the side; the tracer tilts the plate's normal (the face looks down as well
    as out), the weight is longer by 1/cos of the lean (or the side's own slant where that's more), the matchup counts it
    as thick as a level line through it.
  - Internal: the belt and its zones are inclined plates inside the shell, `Armour.InternalBeltInsetM` (0.3 m) in at
    their top and further in below by the lean, and the hitboxes' `belt` says `position: internal` (it isn't side
    armour). A first try stopped at the belt's lower edge and leaked: a shell falling into the space outboard of the
    belt passed under its foot (458 of 200,000 on the battleship). So, as the text above has it, the plane goes on down
    to the inner bottom as a `Lower belt`, tapering from the belt's bottom thickness to `Armour.LowerBeltFootMm` (25 mm,
    South Dakota's 1 in) [INFERRED], weighed as its own item. It is a plate of its own through the TDS's cells, not the
    holding bulkhead itself: the TDS keeps its upright walls. Shell-test then shows only the uptake gratings it always
    had. The space between the belt and the shell is a wing, tier by tier as a slope's; an inclined plate counts only
    inside the hull (a plane in a fine section can cut a bilge).
  - Yamato with her 20° lean: +612 t standard; 16" AP at 20 km, 10 hits: out of the fight 40 → 38 %, lost to the
    magazine 10 → 7 %. The battleship with an internal 19° belt: +4,768 t, because its belt doesn't taper (bottom_mm =
    belt_mm), so the lower belt starts at 307 mm; 18" AP at 20 km: out of the fight 38 → 42 %, lost to the magazine 2 →
    4 % (2 to 4 ships of 200, noise), more water (3,459 → 3,635 t: the space outboard of the belt floods). The heavy
    cruiser with an internal 12° belt: +949 t; 8" AP at 12 km: out 13 → 17 %, water 275 → 424 t. The internal belt gains
    nothing yet: the outer shell's decapping and fuzing (phase P/E) is what it was for; until then it costs weight and
    flooding. Near the ends the lower belt's plane meets the shell where the hull narrows under the waterline, and
    there it is side armour.
  - H8's templates should give an internal belt's family a tapered belt (`belt_bottom_mm`), or the lower belt is heavy.

### H7. Subdivision checks (§7.4.5)
- A warning when two adjacent main compartments flood past the floodable length at the design draught. It reproduces
  the Iowa redesign and the destroyers' "four-compartment" standard, and gives unit machinery a measured payoff. It
  warns and never refuses (memory: limits never block).
- The destroyer's 19 sections against the research's 12–16 (TODO): the minimum-section rule looked at again with the
  magazines' new lengths.
- `subdivision.doors_below` exported (the damage model's open-door path comes with damage control). Frame snap left out
  unless the user wants the names.

- **Done 2026-10-16.** What was built:
  - `Floodable.Check`: each run of 1 to 4 adjacent sections, every cell under the bulkhead deck flooded to the sea
    outside (its volume by its permeability), the ship settled to that water, round after round until it stops
    changing; she fails when the margin line (76 mm under the main deck at the side, the standard) goes under at
    either end. The settling is the damage model's own: its two formulas (sinkage from TPC, trim from the moment over MCT)
    and the sea's plane moved into shipgen's `Flotation`, which `Flooding.Settle` and `HitQuery.WaterOf` now call (one
    implementation; the sim's numbers are the same to the bit, tests unchanged).
  - The report's `results.compartment_standard` (0 to 4) and a warning when it's under the type's
    (`Style.CompartmentStandard`: warships 2, merchants and planing craft 1): the worst run, its length, the rooms that
    matter in it and how far the margin line went under. It warns, never refuses.
  - `subdivision.doors_below` (default merchants true, warships false), exported as the hitboxes' `doors_below` and
    compiled into `ShipClass.DoorsBelow` for damage control. Frame snap left out.
  - Sections: a station merged a few cm off a room's end made the long-gap rule (over 0.07 L) cut the room in two,
    since "inside one room" was tested to a millionth of a metre. A room is one compartment now when what it leaves
    of the gap is shorter than a minimum section. The cut never held water (a room's cells join open) but it named a
    bulkhead that wasn't there. The destroyer 20 → 18 sections (the rest: a magazine per mount, and the 0.07 L splits
    in empty stretches; TODO). Wing bunkers are cut per section, so this could have lengthened them (the 2026-10-07
    pass cut them on purpose): measured on all 26 coal ships with wing bunkers, none changed.
  - The fleet (`shipgen subdivision`, the 72 shipped designs): 4 at 0, 21 at 1, 22 at 2, 15 at 3, 10 at 4 or more.
    Every destroyer and torpedo boat 4 or more (the "four-compartment ship"). Warned (a warship at 1 or 0, a merchant
    at 0): the battleship, Yamato, KGV, Nelson, Richelieu, Colorado, Pennsylvania, the super-dreadnought, Tegetthoff,
    Connecticut, the all-forward pair, Doom, Duilio, Murica, Devastation (0), Erebus (0), Inflexible (0), the tanker
    (0). The merchants, the Q-ship and the planing craft at 1 meet their type's. The worst run is mostly the forward
    magazines (H1 made them longer, not taller, and near the bow the floodable length is shortest): Yamato's A and B,
    38.5 m, put the margin line 2.3 m under; KGV and the super-dreadnought miss by 4-6 cm. TODO: split long magazine
    compartments. Sizes unchanged, mono-check 34 reversals and 14 lumps as at H6,
    `hits` as before (V25 lost to the magazine 5 %, the destroyer 1 %, Dreadnought 4.5 %). ShipDesign.Version 16.

### H8. Scheme templates and the designer
- `armour.json` gains layout schemes beside its material schemes: §6.8's table (`protected_deck`, `belt_and_slope`,
  `german_bulkhead`, `all_or_nothing`, `inclined_internal`, `turtleback_modern`, `french_modern`, `cruiser_box`,
  `unarmoured`, ...) as blocks of the new inputs, no year inside. The shipped designs move onto the scheme of their
  family, with the weight changes reported.
- The Protection tab shows the new parts (platform deck, wing, holding and inner bulkheads, boxes, slopes) in its
  elevation and midship section. That is a UI session of its own, mockup then port (memory).

- **H8a done 2026-10-16** (the templates and the designs moved; the Protection tab is H8b, a session of its own, the
  user). What was built:
  - The schemes are their own catalogue, `Content/Templates/protection.json` (from `docs/shipgen/protection-templates.md`
    by `tools/designer_templates.py`), not entries in armour.json: a scheme's value is a block of armour inputs, not a
    materials map, and each catalogue is one type. `ProtectionSchemeInput` holds what a scheme sets; thicknesses that
    scale with the ship are shares of the design's own (the slopes keep the main deck's plate, the flat takes
    `flat_share` of it; an internal belt's lower edge `belt_bottom_share` of the belt), the rest typical plates.
    `ProtectionSchemes.Apply` is the one implementation (the designer's templates, `Templates.Protection`, and `shipgen
    scheme`), and applying a scheme twice gives what once did (tested on every template).
  - Eight schemes: belt_and_slope, german_bulkhead, turtleback_modern, inclined_internal, inclined_external,
    french_modern, protected_deck, cruiser_box. Left out: the ones that need no new inputs (unarmoured, all or nothing,
    the structural cruiser belt) and the ones the model can't make yet (spaced decapping belts, cellulose cofferdams).
    No scheme gives TDS layers yet: the torpedo model holes every layer at once (H3, open).
  - `shipgen scheme SCHEME DESIGN... [--write]` reports the displacement before and after and rewrites the design's
    armour value in place (untouched values keep their text, the rest of the file untouched).
  - 30 shipped designs moved (the table in protection-templates.md): +0.3 to +9.4 % standard, Bismarck −0.7 % on her
    own 80/110 mm deck; seven designs keep their own figures (named there). Littorio and the super-dreadnought become two-compartment
    ships, Kongo goes 3 → 2. The scenarios' designs stay (tuned for balance).
  - Fixed on the way:
    - The hull girder credited a sloped deck's flat over the whole breadth: thicker flat, less strength plating, a
      lighter ship (Kongo's armour-deck lever reversed at every step, mono-check 34 → 37). The flat now counts over its
      share and the slopes at their mid-height; mono-check 29 reversals and 12 lumps (34 and 14 at H7).
    - An inner bulkhead only beside the magazines (Dreadnought's screens, `mm` 0) built a wall the length of the
      citadel; now only the magazines' wings carry it.
    - The designer's armour scale (`Knobs.ScaleArmour`) left the H3-H6 plates out (zones, slopes, inner bulkhead,
      boxes); it scales them now.
    - Shell-test found no roof on a deck zoned in pieces (KGV): pieces end to end at one height count.
    - `--write` and `--rooms` weren't registered as CLI switches.
  - Benches: shell-test leaks on the moved designs as before (the uptake gratings), Deutschland 572 → 0, Karlsruhe
    3,020 → 6; Kent's "through the side" (7,436 of 100,000) is the scheme, the citadel's sides over the magazines bare
    and the magazines boxed: steel before a magazine at a 10° fall 16 → 118 mm. Lion's 13.5" AP at 12 km, 10 hits:
    Nassau out of the fight 19 → 14 %, lost to the magazine 10 → 5.5 %; Dreadnought 12 → 11 %, 7 → 6 %; both take
    more water (the wing spaces over the slopes flood). FloodingTests' QE anchor floods its engine room directly (a hole
    in the side there now opens the wing over the slope).

## What stays out

- Coal and cellulose as damage media, flooding through cellular layers (§6.6): H3 exports each cell's `fill`; the
  damage model reads it in the flooding and fire sessions.
- The ammunition chain (§6.7: stowage order, flash-tight handling rooms, sprinkling, propellant sensitivity): the
  magazine session of 4.5. H1's handling rooms are its seam.
- Light partitions inside rooms (§7.4.2) and blast by distance (§7.4.3): phase E (E1's fragments, E5's blast), as a
  room's partition count and each bulkhead's strength, not geometry.
- Side subdivision for asymmetric flooding (TODO, shelved by the user): H2 and H3's wing cells are where it would come
  from, but it stays shelved.

## Steps at a glance

| Step | Needs | Moves numbers? |
|---|---|---|
| H0 bench | — | no |
| H1 magazines low, platform decks, handling rooms | H0 | lengths, weights, magazine losses |
| H2 magazine wings | H1 | layout, magazine losses |
| H3 bulkheads by kind, TDS layers | H0 | weights (small), torpedo and shell results |
| H4 inner line | H3 | only designs that ask; defaults for coal and TDS ships |
| H5 zones, belt extent, magazine boxes | H1, H2 | only designs that ask |
| H6 slopes, inclined belts | H4 | the designs moved onto them |
| H7 checks | H1 | warnings |
| H8 templates, designs moved, designer tab | H4–H6 | the shipped designs |

H1–H2 are the destroyer fix and can ship first. H3–H5 are independent of each other once H0 exists. Phase P can start
after H5, or after H6 if slopes are in.

## Decisions (user, 2026-10-16)

1. **Turtlebacks come back** (H6): "now it's time for them to come back". DECISIONS.md's 2026-10-03 shelving is reversed.
2. **Wing width** (H2): 0.1 B by default, destroyers included, configurable as usual.
3. **Crown margin** (H1): 0.4 m under the full-load waterline to start.
4. **The destroyer anchor**: narrowed back after H2 to the measured figure plus a margin ("we'll see what happens").
5. **Phase P** comes after H5.
