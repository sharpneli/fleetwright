# Destruction in the world view: what rides with her, the gas in time, plating blown out, ships broken (plan, draft 2026-11-08)

The sim now tears ships apart properly (`docs/battle/explosions.md`: every explosion through one gas network, the
girder from the same boundaries; `docs/battle/workers.md` ET0-ET6: the gas runs in game time, a magazine burning for
20-170 s before she breaks). The world view shows almost none of it. The user's case: a 2000 mm shell (stabu.json, its
burst 8.6 t TNT-equivalent) bursting inside a destroyer. Captured 2026-11-08 at 0.4 m/px: a fireball over her, then she
lies **whole**, a few scorches and one small fire, and a minute later sinks straight down **in one piece**. The sim has
torn her open; the drawing shows a hit. This plan is the world view's side: show the carnage the sim already computes,
and add to the snapshot only what the sim has and the map lacks.

Mind the rules: looks never mislead about the sim's state (`sim-crunch-vfx-fluff`), nothing drawn changes shape with
the clock's speed (`same-at-any-compression`), effects short-lived (`vfx-ephemeral`), and a look never reshapes the
hull it draws: a piece is drawn where the sim says it is, clipped, never redrawn.

## Done first: what rides with her (D0, 2026-11-08)

The user: "When a shell hits the flame coming from the hole happens in world space ... it's detached from the ship.
They should follow the ship, same with all 'continuous' effects like that; smoke puffs etc naturally detach when they
are emitted." `HitBook` placed each hit once on her pose at the hit and drew it there for its whole life, while her
sprite steamed on (at 10x on the effects' clock a 1 s jet was left ~40 m astern of a destroyer at 30 kn).

**The rule** for every effect on a ship:
- **Attached** (a jet out of a hole, a flash on a plate, a glance's sparks, the light inside, the flames of a fire, a
  stow venting up its train, a breach's glow): posed on her pose **now** each frame (`ShipsView.PoseAt` at the draw
  time, as her sprite), so it stays where it was struck.
- **Released** (a burst's fireball in the open, smoke once laid, what flies off): it belongs to the air. A fireball
  drifts off her by **her own way over its age on the effects' clock**, so it parts from her as it would at 1x at any
  compression (posed on her pose now, less her velocity times its age). Puffs and debris, once handed to the smoke
  and splash books, are the air's and the sea's, as before.
- A ship no longer in the snapshot (gone, lost from sight) keeps the pose she was last seen in.

Built for the hits (`HitEffects.Build`: the frame's ships indexed by entity once, no scan a hit). Fires (I4) already
rode with her. Seen: an AP's jet out of a dreadnought's belt hole and an HE's fireball on her upper works, both on her
at 20 kn 15 s of world time after the hit (`-hit=at:0.25`). **Still to apply**: the magazine's port jets and anything
of D2 below, which are attached.

## What the sim gives today (surveyed 2026-11-08)

| What | In the sim | In the snapshot |
|---|---|---|
| A hit's burst | `HitsView`: W, RE, point, burst point, `VentM2` | Yes, but `VentM2` is only the near field (what tore at once, ET's `AtOnce`); plating the gas tears in later slices never reaches it |
| The live gas run | `ShipCondition.Gas` (`GasNetwork`): cells reached, `FeltMPa`, `Hot`, stows lit (`LitRoom`), ways vented (`VentedMount`), gunhouses lifted (`LiftedMount`), `Report()`'s counts and first-give/hull/vent times | **No.** Released at its end or the loss |
| Plating torn to the sea or air | `ShipCondition.FaceTornM2[cell*4+f]`, `FaceLost` (f: port, starboard, weather deck, bottom; the face's middle and area from `HullFaces.Of(c)`) | **No** |
| Walls and decks torn inside, doors blown | `JoinOpenM2`, `EdgeLost`, `JoinMarks & DoorBlown` | **No** (and unseen from outside: stays out) |
| An end blown off | `CutBowX`, `CutSternX` (every cell past holed, every part wrecked) | **No** |
| Broken in two | `LossKind.BrokeInTwo` (or `Magazine`), from the girder's reading; the break's station is **not kept** | Only `Loss`, `LossMs`, `GoneMs` |
| Her attitude going down | `Attitude.At`: one rigid body; a broken ship and a magazine fall to the default, straight down | Yes, one body |
| A magazine's record | `Blast(Ms, Ship, Class, Room, Cause, At, Forward, Point, Tier, FastKg)`, at the break since ET4 | `BlastsView` without `Tier` and `FastKg`; the map sizes it by the class's placeholder 0.3 fast share. **Bug:** the gas path records `Point` as (0,0,0), so a shell's or torpedo's magazine puts its hit flash at her origin |
| Parts wrecked | `PartState` Destroyed | Yes; a part thrown, wrecked or past a cut end all read the same |

## What we build

| Real | Here |
|---|---|
| Side plating blown out | A **ragged bite** out of her side over the torn face's length, as deep as the tear's area over its height allows, its rim curled out, sooted; dark inside (`shipmap.frag`: a new decal mode clipping the sprite by a noise edge, so the sea shows through where the deck edge is gone) |
| A weather deck torn up | A **ragged hole in the deck** over the cell, sized by the torn area: dark below, the decks under it showing deeper (darker) where they tore too, glowing while what's below burns (the fire's showing list already says so) |
| Plates flying | **Pieces** off each face as it tears, as the magazine's debris (`MagazineDebris`: arcs, a trail, a splash where they land), their count and speed from the tear's area and the gas's peak (the face's `FeltMPa`), plating 5-15 mm, at most a budget a ship |
| The gas venting | At each face as it tears and while the run goes, a **jet or fireball out of it** by the gas's push (a side's outward and up, a deck's up), on the magazine's body and jet models (one flame model), its size from the cell's pressure and the opening; attached (D0) |
| A stow burning up its train | A **roaring column** up the vented way out of the barbette or the lifted gunhouse's ring for as long as it burns (20-170 s), Heskestad's height by the burning rate, its light into the fires' pool, brown-yellow propellant smoke as a fire's trail; the tier 1 burn-out she survives (Seydlitz's turrets at Dogger Bank) |
| A gunhouse lifted, a funnel or mast blown off | **Thrown parts** on the X4 machinery (`IThrownTurrets`, `ThrownTurret`), extended from turrets to every component the run throws: up on its arc, turned and tilted, into the sea as a splash; gone from the sprite |
| An end blown off | **The ship drawn short**: her sprite clipped at the cut with a ragged edge, the cut piece sinking on its own for a minute (a bow torn off, a destroyer steaming on stern-first is the history's) |
| Broken in two | **Two pieces**: the sprite drawn twice, each clipped at the break station, each on its own pose and attitude (each going down by its broken end, the ends rising steeply; drifting a few metres apart and turning a little), turrets by the piece they stand on, ragged edges at the break, the I7 sinking layers along each piece, steam and smoke out of the break |
| A huge burst inside | No new model: the burst's gas run drives it all (the near field's fireball, the faces it tears in later slices venting fire, the plating out, the parts thrown, the girder broken). The fireball emerges by how much of her it opened (the run's `VentM2` against the cell's walls), up to its whole size |

## Steps

- **D0, what rides with her.** Done for the hits (above). The magazine's port jets with it in D3.
- **D1, the data** (display only, no hash moves; the scenario benches bit for bit):
  - `BlastsView` gains `Tier` and `FastKg`; the map sizes the event by them. The gas path's `Point` fixed to the
    source's (the burst's or the stow's).
  - `ShipsView` gains the **cut ends** (`CutBowX`, `CutSternX`) and the **break** (station and ms), the break kept on
    the condition when the girder gives (`GasTo`, `Hits.Land`) for display only.
  - `ShipsView` gains each ship's **torn faces** (cell, face, torn m², the ms it first tore: a new display-only stamp
    beside `FaceTornM2`), the face's point and area from `HullFaces`. Outside, so seen alike for every ship in sight.
  - `ShipsView` gains each ship's **live gas slice** while it runs: stows alight (rooms), ways vented and gunhouses
    lifted (mounts), each torn face's cell pressure now (one accessor on the run's `p[]`), the run's clock.
  - Tests: each record against a run (`-ignite=1` on bismarck; a 2000 mm burst in a destroyer; a torpedo breaking one).
- **D2, plating blown out.** The bites and deck holes on the sprite from the torn faces, the pieces flying as each
  tears, and the gas venting out of each face while the run goes. The hit marks' cap (24) doesn't apply: the faces
  are the sim's own list.
- **D3, the stows burning.** The roaring columns up the vented trains, their light and smoke; the gunhouse lifted on
  the thrown machinery at its moment; the magazine's port jets attached (D0); the magazine event at the break sized by
  the sim's `FastKg`. Tier 1 ends with the column dying and her ring open.
- **D4, thrown parts.** Every component a run throws (the sim, decision 2), on its arc, gone from the sprite.
- **D5, the end blown off.** Her sprite clipped at the cut, the ragged edge, the piece going down by itself.
- **D6, broken in two.** The two pieces (decision 1), drawn and sunk apart.
- **D7, cost.** The Jutland at 60x, the 2000 mm bench, a melee of destroyers under 15" fire: frame time, pieces,
  jets, lights, as I8 did.
- **D8, by eye.** Side-by-side captures, day and night: the 2000 mm shell on a destroyer at 0, 1, 5, 30 s and 2 min;
  bismarck's `-ignite` through its race; a torpedo breaking a destroyer's back; a cruiser losing her bow.

## Decisions for the user

1. **Two pieces in the sim or only on the map?** Recommended: **in the sim**, as `Attitude.At` is one model the hit
   queries and the screens both read (`single-implementation`). Each piece gets its own attitude after a break, so
   shells falling on a broken wreck hit what's drawn. Hashes move only for runs where a ship breaks; the benches'
   results don't (she's lost either way). The other way, display-only pieces, leaves a shell hitting the gap between
   them.
2. **Thrown parts in the sim?** Recommended: **yes, as a record**: a component over a torn face or a lifted ring is
   thrown when the gas's impulse on its base gives it more than a few m/s (v = I A / m, from the face's pressure and
   the run's slices), flagged `Thrown` with its speed, and already Destroyed as today. Nothing else reads it. The
   alternative, the map guessing from Destroyed parts near torn faces, could throw a funnel the sim only dented.
3. **A burst in the open drifting off her by her way on the effects' clock** (D0, built). Confirm, or have every
   short burst ride with her wholly.
4. **Budgets.** Plating pieces at most ~60 a ship and ~400 in the view, jets out of torn faces at most 16 a ship, the
   columns' lights in the fires' pool of 8. Tuned in D7.

## Later

- Burning oil on the water round a broken wreck; a capsized ship's explosion partly under water.
- Steam out of wrecked boiler and engine rooms (from hits-and-fires.md "Later").
- Inside views (the hitbox viewer's playback of a gas run cell by cell): `docs/battle/hit-view.md`.
