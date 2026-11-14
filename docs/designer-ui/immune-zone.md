# Immune zone and shells fired in anger (plan, 2026-11-14)

**Status:** plan, the user's answers in (2026-11-14, "Decisions" at the end). Nothing built yet; IZ0 next.

The Protection tab has carried a greyed placeholder since its first review (protection.md, 2026-10-11): "Immune zone
and shells fired in anger · against her own 380 mm · NEEDS THE PENETRATION MODEL". The penetration model has been in
since 2026-10-17 (Thompson, its Cardano inverse, Lambert-Jonas, caps, shatter, break-up, fuzes: shells-armour-damage.md
P0-P7), and the damage behind it since E0-E8. This plan fills the panel.

Two readouts, one question each:

- **The immune zone:** at which ranges and target angles does a gun's shell fail to reach her vitals, through the side
  or through the decks? The designer's chart, the one the USN drew from 1929 on.
- **Shells fired in anger:** what do hits of that gun do to her, range by range across the whole fight? Not plates
  beaten, but ships: holes, fires, guns silenced, magazines, men, ships lost. The zone says where her vitals are safe;
  this says what that safety is worth, and how she fares inside it: even Yamato is felled by WW1 battleships if the
  range is short enough.

## What's already there

Everything below is built and tested; the plan adds a readout over it, not a model.

| Piece | Where | Used for |
|---|---|---|
| The shell's flight: fall and striking speed at a range, from a target angle | `GivenHits.Flight` (on `Ballistics`) | both |
| The tracer: a line through the ship's plates, parts and watertight cells | `ShipTracer.Trace`, `HitPath` (`CellIn` crossings carry the cell, `CellRow.Room` its room kind) | both |
| The terminal walk, plate by plate, with the plate lots, caps, shatter, break-up, turn, glance and fuze | `Terminal.Resolve` | the immune zone |
| A hit through the world's own damage system | `DamageSystem.StrikeOut`, `ShotRange` | shells fired in anger |
| N hits drawn uniformly over the ship's outline as the shell sees it, run on fresh ships, settled for flooding and fire | `GivenHits.Run`, its `Aim` and `Report` (`shipgen hits`) | shells fired in anger |
| A battery's shells from the design (its family and blocks at its calibre) | `Ammunition.ResolveBattery` (Shipgen), into rows by `Loadout`'s `Given` | the "against" picker |
| A design compiled into a class | `ShipClassCompiler.Compile` | both |

The matchup's profiles (`MatchupProfile.SideArmour`/`DeckArmour`) are a quick per-class estimate the AI asks every
think. They are not the immune zone and the immune zone doesn't use them (one model per thing: the zone walks real
shells through `Terminal.Resolve`). A test later checks the two don't drift far apart (IZ5).

## 1. The immune zone

### What it is

For a gun's shell (a battery of hers, at its calibre or another, its AP by default, any kind it carries on a pick), at each
range out to the gun's reach and each target angle (the line of fire off her bow, 0-180°, one side; she's symmetric
enough):

1. **Shells at the vitals.** Lines of flight drawn over her outline as the shell sees it, kept only if the line, unstopped,
   would enter a vital space. Vitals are cells whose room is a magazine or shell room (the magazines' zone) or a boiler or
   engine room (the machinery's). The USN gave both zones; so do we, two zones, each from its own shells (decision 2).
2. **Each shell walked** through `Terminal.Resolve` on an undamaged ship, upright on her design waterline: the same walk
   a hit in battle takes, with its rolls (the plate's lot, shatter, break-up, the fuze). No effects, no world: the
   walk only says where the shell went and whether it was whole.
3. **Reached** if the shell enters a vital cell whole, filler in it, before it stops (or bursts inside one). Shells
   broken up on the way don't count; their share shows on hover (decision 1).
4. **Through the side or the deck:** each shell that reached is put down to the heaviest plate it beat: a belt, strake,
   inclined plate, inner bulkhead or barbette is the side; an armour deck, its slopes seen from above, or a grating is the
   deck. A slope is the side when the shell met it from outboard, the deck when it came down on it: the plate's normal
   against the line decides, not its name.
5. **The edges.** At each angle, the inner edge is the range out to which more than half the shells at the vitals get
   in through the side; the outer edge, the range from which more than half get in through the deck. Between them she's
   immune; with the inner edge past the outer, she has no zone at that angle (Iowa against her own gun without the
   outer hull's credit, armor/00 §9 row 12). Half is the classic limit at the mean plate; the chart shades the band where
   the share is between 10% and 90%, so a soft edge looks soft (decision 3).

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
- **The line:** "Immune 20.4-26.7 km on the beam against her own 380 mm APC · magazines", or "No immune zone: the
  belt is beaten out to 27 km, the deck from 24 km".
- **Hover a cell:** the range and angle, the fall and striking speed, the share that reached and through what ("62%
  through the belt, 330 mm at 18°; 4% through the slope"), the share broken up.
- **The range axis is shared** with fired in anger's strip under it (below): a range band lines up with its ring.
- **The plate that sets each edge** is named under the line ("inner edge: the belt over the magazines; outer edge: the
  2nd deck's 80 mm over the forward magazines"). That's the knob the designer turns next.

## 2. Shells fired in anger

A sweep across the fight, not one shot: the zone says where her vitals are safe, this says what she suffers at every
range, inside the zone, short of it and past it (decision 6).

**The run.** `GivenHits.Run` as `shipgen hits` runs it, once a range band: bands every 2 km from 2 km to the gun's
reach. In a band each ship takes **10 hits** at one range in the band and one target angle, the angles taken in turn
over the chart's ten so every band sees each alike; the hits drawn uniformly over her outline as the shell sees it,
through the world's own damage system, then the ship **settled 30 minutes** for her flooding and fires. Ships are added
a band at a time, round-robin, so every band fills together and the strip sharpens as it runs; it stops at **25 ships
a band** (≈4,000 hits for a 15" gun), or when stopped. Hits a ship (5, 10, 20, 40) is a stepper. Nothing new in the
model; the readout is new.

Same seed for every build of the design, as the zone: a run before an edit and one after differ by the edit.

What it shows:

- **The strip,** under the chart, a column a range band, in the chart's units: the ships lost of those run (a bar split
  by how: magazine, foundered, capsized, broken), and beside it those with their main guns out and those under half
  speed. Inside the inner edge this is where Yamato dies to 12" shells; the bands in the zone show what the upperworks
  and the ends cost her there. Hovering a band highlights its ring on the chart.
- **A band picked** (a click on its column) shows its detail:
  - **the dots:** each hit a dot on the Protection tab's own elevation (and the half-sections), where it first met her,
    coloured by its class (16's: burst inside, passed through, dud, held by armour). The dots are the run, not a
    picture of it;
  - **by where:** belt, deck, upperworks, turrets and barbettes, ends; for each, the share held, burst inside, passed
    through;
  - **per 100 hits:** fires started, holes under the water (m²), men killed and wounded, turrets jammed or out,
    magazines set off (and how: flash, burst, fire);
  - **the ships:** the water and list of the worst still afloat, and the crew out of action against hits (the
    `CurveOut` the bench already keeps).

Cost: a hit through the damage system is ≈200 µs; a ship's 30-minute settle is more (flooding and fire on their steps)
and IZ2 measures it. If 25 ships a band takes more than a few seconds, the stop comes down, not the model. It runs on a
click (Fire), never by itself on an edit; an edit marks the result stale (greyed, "before the last edit") until fired
again.

## 3. The against picker

Against her own guns (decision 5): setting up a trial with your own gun is easier than finding a foreign one.

- **The battery:** hers, the main first, and its shell kind (AP by default; common, HE as it carries them).
- **The calibre:** a stepper on it, at the battery's own to start. Another calibre is her own shells at that calibre: a
  copy of the design with the battery's calibre set, through `Ammunition.ResolveBattery` (her shell family and blocks,
  her gun block), into a `ShellTypeRow` by the same conversion `Loadout` uses (made one shared function, not copied).
  The guns' elevation is her mounts'. Nothing is built but the shells; the design itself is untouched.

So "a WW1 12" against Yamato" is Yamato's own family at 305 mm: not a Dreadnought's shell, but the same question. The
pick (battery, kind, calibre) is kept in the session, not in the design.

## Where the code goes

- **`Fleetwright.Sim/ImmuneZone.cs`** beside `ShotRange`: a class and a shell in, the grid of cells out for each vital
  zone (shares by side, deck, broken; the plate that let most through), by passes, cancellable. Pure, no GPU, no UI;
  allocates per run, never per walk.
- **`Fleetwright.Sim/AngerRun.cs`:** the sweep over `GivenHits.Run` by band, with progress, a cancel and a summary
  record a band the UI reads (the bench's `Report` is large and bench-shaped), and the hits' first points and classes
  for the dots.
- **The shells at a calibre:** a small function by `Ammunition.ResolveBattery` and `Loadout`'s conversion, in
  `Fleetwright.Sim.Ships`.
- **`Fleetwright/Designer/ImmuneWorker.cs`:** compiles the design's class off the render thread (cached by the design
  hash), runs the zone on a couple of threads, newest request wins, as `DesignProbes` does; the sweep on a click.
- **`Fleetwright/Designer/DesignerScene.Immune.cs`:** the panel, the strip and the dots.
- **`shipgen immune DESIGN [--battery main|secondary] [--calibre MM] [--kind ap|common|he] [--vitals magazines|machinery]
  [--shells N] [--angles N] [--anger [--ships N] [--per-ship K]]`:** the chart as a table, the edges by angle and the
  plates setting them; with `--anger`, the strip as a table; with no design, the anchors (IZ1).

## Steps

| Step | What | Done when |
|---|---|---|
| IZ0 | `ImmuneZone`, the shells at a calibre, and `shipgen immune` | the bench prints both zones for bismarck, yamato, dreadnought against their own guns and other calibres; tests: deterministic, the same with 1 or N threads, a thicker belt never moves the inner edge out, a thicker deck never moves the outer edge in, the shells at the battery's own calibre equal the loadout's |
| IZ1 | The anchors, as history checks (never set): bench-only designs with SoDak's and Iowa's schemes (research 08's table, armor/00 §9 row 12: Iowa against 16"/50 Mk 8 at 23.6-27.4 kyd, North Carolina's three studies), each against its own family at the record's calibre | the numbers beside the record, the gaps named; no tuning in this plan |
| IZ2 | `AngerRun`: the sweep, progress, cancel, the summaries, the dots; its cost measured | `shipgen hits` unchanged, byte for byte; yamato against 305 mm shows her lost at short range and not in the zone |
| IZ3 | The mockup, `immune.html` (the Protection tab's pane, 1280 × 578): the facts pane's Plate and Immune zone tabs, the chart, the strip, the dots; set beside a designer screenshot | the user's review |
| IZ4 | The port: worker, panel, chart, strip, dots, picker; `-designer-ui=immune:...` for screenshots | on bismarck and dreadnought, a belt edit moves the edge within a second; captures checked |
| IZ5 | Beyond the tab: the line in the particulars and the ship card; the matchup profiles checked against the zone (a test in a loose band); the Overview levers' "immune zone" hints made real numbers if the probes can afford it | each as the user picks |

IZ0 and IZ2 can run together; the mockup waits for their numbers so it's drawn from real zones and runs.

## Decisions (user, 2026-11-14)

1. **Reaching the vitals:** the shell enters a vital space whole, filler in it, or bursts inside one. A shell broken up
   on the way doesn't count; the chart shows its share on hover.
2. **Two zones,** magazines and machinery, each from its own shells.
3. **The edge at half** the shells at the vitals getting in, the 10-90% band shaded.
4. **The facts pane gets two tabs,** Plate and Immune zone; the dots draw on the elevation whenever there's a run.
5. **Against her own guns,** a calibre change using her own shells: "it's easier to set up an impact test using your own
   gun than go search for a specific foreign weapon." No foreign designs, no free reference gun.
6. **Fired in anger is a spread** over ranges and angles, not one cell: "we want to know the immunity zone. Even
   Yamato is felled by WW1 battleships if the range is short enough."
