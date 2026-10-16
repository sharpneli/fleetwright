# Plan: subdivision and protection schemes

2026-10-16. Status: agreed 2026-10-16 (decisions at the end). Research: `research/09_protection_schemes.md`
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
  - `protection --summary` (200 shells a fall, every shipped and scenario design): every WWI destroyer and torpedo boat
    (V25, V1, G101, B97, M, R... K, L, Faulknor, Abdiel, Buyny, Ikazuchi) has 40-98% of shells reaching a magazine from
    above the water through 6-8 mm ("open"), and every magazine against the side. The WWII-and-later destroyers Daring,
    Akizuki, Hamburg, Impetuoso and Fubuki 9-39% open; the seaplane carriers and Q-ship up to 85%. The R class (crown
    1.8 m under) 0-12%, the WW2 Fleet Destroyer 0%. No armoured ship is open anywhere; the heavy cruiser, Des Moines,
    Sverdlov and Deutschland's crowns stand 1.8-2.4 m over the waterline behind their belts and decks.
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

### H2. Magazines inboard (§6.1)
- A **wing** between the magazine and the side on every ship that doesn't already have a TDS or wing bunker there:
  `armour.magazine_wing_m`, default 0.1 B [INFERRED]. The bands logic that splits TDS and coal sections splits magazine
  sections too. The wing is stores (refrigerated rooms, the stores the crew model already asks for), never fuel. A tank
  that would border a magazine gets a void cofferdam instead (fire, §6.1).
- In the narrow ends the wing takes its width from the waterline half-breadth, and the magazine grows longer for its
  volume.
- **Expected:** the magazine moves from "behind the shell" to "behind the shell, stores and a wall". The gain is modest
  until E1 (see above), and the bench says how modest.

### H3. Bulkheads by kind (§7.1, §7.4.1)
- Each kind gets its own plate: main transverse at today's gauge with the lowest tier 1.3× and the top 0.8× (a
  `plate_low_mm` on the row); collision 1.5×; wing and centreline at the structural gauge.
- **The TDS as layers:** `armour.tds` becomes `{depth_m, layers, layer_mm, holding_mm, fill}` (today's `tds_m` upgraded by
  `DesignFormat`). The layer walls are thin (6–16 mm) and the **holding bulkhead** is its own thickness (25–50 mm), the
  innermost wall. Each layer is a band of cells, liquid or void by `fill` (closes TODO "TDS layers in detail" and the
  TDS half of "Double-bottom contents"). Defaults keep today's weight within a few percent: 12 mm per metre spread as
  holding plus layers.
- The tracer sees each wall as a plate already; this only gives them their real thicknesses.

### H4. The inner line behind the belt (§6.4)
- `armour.inner_bulkhead {mm, magazines_mm, inboard_m, extent}`: a longitudinal armoured wall from the inner bottom to
  the armour deck. Defaults: on a coal ship with wing bunkers it is the bunkers' inner wall (the German torpedo bulkhead,
  with the coal outboard); on a TDS ship it is the holding bulkhead (H3), which can be thickened beside the magazines
  (Dunkerque 30 → 40–50 mm). `mm` 0 means none: no design gains plate it didn't ask for.
- The longitudinal bulkhead row gets `ArmourMm` as the transverse ones have, so the tracer adds it to the crossing's mm.
  It is weighed from its area.
- Bismarck's splinter cells above the armour deck (30 mm walls between the decks) are the same wall over another tier
  range: `extent` takes a tier range. Later in this step, or left out if it doesn't pay.

### H5. Zones and boxes (§6.5)
- **Per-zone thickness:** `belt_mm` and a deck's `mm` may be `{magazines, machinery}` (KGV 374/348, Belfast's deck 3 in/2
  in). The layout already knows each stretch.
- **Belt extent:** `belt.extent: citadel | machinery`.
- **Magazine boxes:** `armour.magazine_box {side_mm, crown_mm, end_mm, floor_mm}` around each magazine group, on any
  style. It is the steering box's mechanism (a narrow deck, box strakes, short bulkheads) generalised. Its crown is H1's
  platform deck and its sides H2's wing wall, so the earlier steps give it its shape. Unlocks Kent (no belt, 4 in boxes),
  Leander, New Orleans and Brooklyn (a belt plus a box), Dunkerque's floor plate, and an up-armoured destroyer for
  players who'll pay for it.

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

### H7. Subdivision checks (§7.4.5)
- A warning when two adjacent main compartments flood past the floodable length at the design draught. It reproduces
  the Iowa redesign and the destroyers' "four-compartment" standard, and gives unit machinery a measured payoff. It
  warns and never refuses (memory: limits never block).
- The destroyer's 19 sections against the research's 12–16 (TODO): the minimum-section rule looked at again with the
  magazines' new lengths.
- `subdivision.doors_below` exported (the damage model's open-door path comes with damage control). Frame snap left out
  unless the user wants the names.

### H8. Scheme templates and the designer
- `armour.json` gains layout schemes beside its material schemes: §6.8's table (`protected_deck`, `belt_and_slope`,
  `german_bulkhead`, `all_or_nothing`, `inclined_internal`, `turtleback_modern`, `french_modern`, `cruiser_box`,
  `unarmoured`, ...) as blocks of the new inputs, no year inside. The shipped designs move onto the scheme of their
  family, with the weight changes reported.
- The Protection tab shows the new parts (platform deck, wing, holding and inner bulkheads, boxes, slopes) in its
  elevation and midship section. That is a UI session of its own, mockup then port (memory).

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
