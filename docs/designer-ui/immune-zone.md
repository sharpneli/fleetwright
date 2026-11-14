# Immune zone and shells fired in anger (plan, 2026-11-14)

**Status:** plan, awaiting the user's answers to the questions at the end. Nothing built yet.

The Protection tab has carried a greyed placeholder since its first review (protection.md, 2026-10-11): "Immune zone
and shells fired in anger · against her own 380 mm · NEEDS THE PENETRATION MODEL". The penetration model has been in
since 2026-10-17 (Thompson, its Cardano inverse, Lambert-Jonas, caps, shatter, break-up, fuzes: shells-armour-damage.md
P0-P7), and the damage behind it since E0-E8. This plan fills the panel.

Two readouts, one question each:

- **The immune zone:** at which ranges and target angles does a gun's shell fail to reach her vitals, through the side
  or through the decks? The designer's chart, the one the USN drew from 1929 on.
- **Shells fired in anger:** what do a thousand hits of that gun, at a range and angle, do to her? Not plates beaten,
  but ships: holes, fires, guns silenced, magazines, men, ships lost.

## What's already there

Everything below is built and tested; the plan adds a readout over it, not a model.

| Piece | Where | Used for |
|---|---|---|
| The shell's flight: fall and striking speed at a range, from a target angle | `GivenHits.Flight` (on `Ballistics`) | both |
| The tracer: a line through the ship's plates, parts and watertight cells | `ShipTracer.Trace`, `HitPath` (`CellIn` crossings carry the cell, `CellRow.Room` its room kind) | both |
| The terminal walk, plate by plate, with the plate lots, caps, shatter, break-up, turn, glance and fuze | `Terminal.Resolve` | the immune zone |
| A hit through the world's own damage system | `DamageSystem.StrikeOut`, `ShotRange` | shells fired in anger |
| N hits drawn uniformly over the ship's outline as the shell sees it, run on fresh ships, settled for flooding and fire | `GivenHits.Run`, its `Aim` and `Report` (`shipgen hits`) | shells fired in anger |
| The shooter's class and battery picked from her own or another design | `HitboxScene.Shot.cs` (`ShooterClass`, `Batteries`) | the "against" picker |
| A design compiled into a class | `ShipClassCompiler.Compile` | both |

The matchup's profiles (`MatchupProfile.SideArmour`/`DeckArmour`) are a quick per-class estimate the AI asks every
think. They are not the immune zone and the immune zone doesn't use them (one model per thing: the zone walks real
shells through `Terminal.Resolve`). A test later checks the two don't drift far apart (IZ5).

## 1. The immune zone

### What it is

For a gun's shell (a battery of hers or another design's, its AP by default, any kind it carries on a pick), at each
range out to the gun's reach and each target angle (the line of fire off her bow, 0-180°, one side; she's symmetric
enough):

1. **Shells at the vitals.** Lines of flight drawn over her outline as the shell sees it, kept only if the line, unstopped,
   would enter a vital space. Vitals are cells whose room is a magazine or shell room (the magazines' zone) or a boiler or
   engine room (the machinery's). The USN gave both zones; so do we (question 2).
2. **Each shell walked** through `Terminal.Resolve` on an undamaged ship, upright on her design waterline: the same walk
   a hit in battle takes, with its rolls (the plate's lot, shatter, break-up, the fuze). No effects, no world: the
   walk only says where the shell went and whether it was whole.
3. **Reached** if the shell enters a vital cell whole, filler in it, before it stops (or bursts inside one). Shells
   broken up on the way are counted apart (question 1).
4. **Through the side or the deck:** each shell that reached is put down to the heaviest plate it beat: a belt, strake,
   inclined plate, inner bulkhead or barbette is the side; an armour deck, its slopes seen from above, or a grating is the
   deck. A slope is the side when the shell met it from outboard, the deck when it came down on it: the plate's normal
   against the line decides, not its name.
5. **The edges.** At each angle, the inner edge is the range out to which more than half the shells at the vitals get
   in through the side; the outer edge, the range from which more than half get in through the deck. Between them she's
   immune; with the inner edge past the outer, she has no zone at that angle (Iowa against her own gun without the
   outer hull's credit, armor/00 §9 row 12). Half is the classic limit at the mean plate; the chart shades the band where
   the share is between 10% and 90%, so a soft edge looks soft (question 3).

What it doesn't count, on purpose: hits on the ends, upperworks and turrets (the zone is about the vitals), the ship's
own motion (roll and heel would move the deck edge a little), and damage from earlier hits. Shells fired in anger
covers all of those.

Cost: a range step of 1 km to the gun's reach (≈35 steps for a 15" gun), 10 angles, 128 shells a cell: ≈45,000 walks.
A walk is the tracer plus the plate steps, likely tens of microseconds (IZ0 measures it); then it's about a second on
the designer's own worker threads. It refines in passes (16, then 64, then 128 a cell) so the chart appears at once and sharpens.

**Common random numbers.** Every build of the design uses the same seed, so one cell's lines and rolls are the same
before and after an edit: a 10 mm thicker belt moves the edge because the belt did, not because the dice did.

### How it looks (the mockup decides)

- **The chart:** a half-polar plot, USN style. Her plan small at the centre, bow up; the target angle around it, the
  range out from it to the gun's reach in rings every 5 km (or kyd, by the designer's units). Each cell coloured by what
  happens there: through the side (inside), immune, through the deck (outside), beyond reach. All colours are `Theme`
  tokens. The magazines' and the machinery's zones as two outlines, the selected one filled.
- **The line:** "Immune 20.4-26.7 km on the beam against bismarck's 380 mm APC · magazines", or "No immune zone: the
  belt is beaten out to 27 km, the deck from 24 km".
- **Hover a cell:** the range and angle, the fall and striking speed, the share that reached and through what ("62%
  through the belt, 330 mm at 18°; 4% through the slope"), the share broken up.
- **Click a cell:** it becomes the place shells are fired in anger (below).
- **The plate that sets each edge** is named under the line ("inner edge: the belt over the magazines; outer edge: the
  2nd deck's 80 mm over the forward magazines"). That's the knob the designer turns next.

## 2. Shells fired in anger

`GivenHits.Run` as `shipgen hits` runs it: n hits of the shell at the range and angle picked (the chart's click, or
the beam at the inner edge to start), drawn uniformly over her outline, k hits a ship, each ship settled for its
flooding and fires, through the world's own damage system. Nothing new in the model; the readout is new.

Defaults (question 6): **1,000 hits, 10 a ship (100 ships), settled 30 minutes.** A run's size and the hits a ship
are two steppers; Fire again re-rolls the seed.

What it shows:

- **Where they struck and what they did:** each hit a dot on the Protection tab's own elevation (and the half-sections),
  where it first met her, coloured by its class (16's: burst inside, passed through, dud, held by armour). The dots are
  the run, not a picture of it.
- **By where:** belt, deck, upperworks, turrets and barbettes, ends; for each, the share held, burst inside, passed
  through.
- **Per 100 hits:** fires started, holes under the water (m²), men killed and wounded, turrets jammed or out, magazines
  set off (and how: flash, burst, fire).
- **The ships:** of the 100 ships taking 10 each, after the settle: lost (foundered, capsized, magazine, broken), how
  many with their main guns out, how many below half speed, the water and list of the worst still afloat. A small curve
  of crew out of action against hits (the `CurveOut` the bench already keeps).

Cost: a hit through the damage system is ≈200 µs, a ship's 30-minute settle more (flooding and fire on their steps);
a 1,000-hit run is seconds, on a worker, the readout filling in as ships finish. It runs on a click, never by itself
on an edit; an edit marks the result stale (greyed, "before the last edit") until fired again.

## 3. The against picker

One drop-down, the same tree as the hitbox viewer's shooter:

1. **Her own batteries** (the main first), each with its shell kinds.
2. **Shipped designs** by navy and era (`Content/Designs`), each compiled the first time it's picked and kept for the
   session.
3. Later, maybe: **a free reference gun** (a calibre, a gun block, a shell family and block), the "13.5 in Mk V" of
   designer-ui-research.md. It needs a shell's report built outside a ship (shipgen's `Ammunition` on its own); worth
   it only if picking a design's battery proves clumsy (question 5).

The pick is kept in the session, not in the design.

## Where the code goes

- **`Fleetwright.Sim/ImmuneZone.cs`** beside `ShotRange`: a class and a shell in, the grid of cells out (shares by
  side, deck, broken; the plate that let most through), by passes, cancellable. Pure, no GPU, no UI; allocates per
  run, never per walk.
- **`GivenHits.Run`** as it is, with a progress callback, a cancel and a summary record the UI reads (the bench's
  `Report` is large and bench-shaped), and the hits' first points and classes for the dots.
- **`Fleetwright/Designer/ImmuneWorker.cs`:** compiles the design's class off the render thread (cached by the design
  hash), runs the zone on a couple of threads, newest request wins, as `DesignProbes` does; fires in anger on a click.
- **`Fleetwright/Designer/DesignerScene.Immune.cs`:** the panel and the dots.
- **`shipgen immune DESIGN [--gun DESIGN:BATTERY[:ap|common|he]] [--vitals magazines|machinery] [--shells N]
  [--angles N]`:** the chart as a table, the edges by angle and the plates setting them, and with no design the anchors
  (IZ1).

## Steps

| Step | What | Done when |
|---|---|---|
| IZ0 | `ImmuneZone` and `shipgen immune` | the bench prints a zone for bismarck, yamato, dreadnought against their own guns and each other's; tests: deterministic, the same with 1 or N threads, a thicker belt never moves the inner edge out, a thicker deck never moves the outer edge in |
| IZ1 | The anchors, as history checks (never set): bench-only designs with SoDak's and Iowa's schemes (research 08's table, armor/00 §9 row 12: Iowa against 16"/50 Mk 8 at 23.6-27.4 kyd, North Carolina's three studies) | the numbers beside the record, the gaps named; no tuning in this plan |
| IZ2 | `GivenHits.Run` for the UI: progress, cancel, the summary, the dots | `shipgen hits` unchanged, byte for byte |
| IZ3 | The mockup, `immune.html` (the Protection tab's pane, 1280 × 578), set beside a designer screenshot | the user's review |
| IZ4 | The port: worker, panel, chart, dots, picker; `-designer-ui=immune:...` for screenshots | on bismarck and dreadnought, a belt edit moves the edge within a second; captures checked |
| IZ5 | Beyond the tab: the line in the particulars and the ship card; the matchup profiles checked against the zone (a test in a loose band); the Overview levers' "immune zone" hints made real numbers if the probes can afford it | each as the user picks |

IZ0 and IZ2 can run together; the mockup waits for IZ0's numbers so it's drawn from real zones.

## Questions

1. **What counts as reaching the vitals?** Proposed: the shell enters a vital space whole, filler in it, or bursts
   inside one. A shell broken up on the way doesn't count (its pieces are E1's splinters, not a magazine hit), but the
   chart shows its share on hover. The alternative counts any shell that gets in, which narrows the zone a lot against
   pre-1914 shells that shatter.
2. **Which vitals?** Proposed: magazines and machinery as two zones, as the USN gave them, the magazines' shown first.
   The alternative is one zone, the narrower of the two.
3. **The edge:** half the shells at the vitals getting in, with 10-90% shaded. Or a stricter edge (10%), closer to what
   a designer would promise an admiral.
4. **Where it lives:** the placeholder is one line at the foot of the selected-plate pane, and the chart needs room.
   Proposed: the facts pane gets two tabs, Plate and Immune zone; the dots draw on the elevation whenever there's a run.
   The mockup tries this first.
5. **The free reference gun:** wanted now, or only her own and the shipped designs' batteries?
6. **Fired in anger:** 1,000 hits, 10 a ship, 30 minutes' settle, at the chart's clicked cell. Or a spread over a
   battle's ranges and angles in one run instead of one cell.
