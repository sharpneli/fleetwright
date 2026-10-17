# Shipgen refactor plan: comfortable to work on

From the review's "Is it good enough to work on?" (`shipgen_review.md`, 2026-10-16): the four items that cost
time or risk mistakes, in its order. Agreed 2026-10-17: start with 1, do all four, plan first, then step by step.

**The rule for every step: the output stays byte-identical.** The goldens alone can't prove it (their comparer allows
1e-9), so each step is checked with `shipgen snapshot` (step 0): every golden case's capture, SVGs and sprite.json
as plain files, `diff -r` against the snapshot taken before the work began. `mono-check`'s text output must not change
either. Then `dotnet test`, a commit and a push per step. A step that can't stay identical stops and asks. The exceptions
are item 2's bug fixes (below): they change behaviour on purpose, each in its own commit.

## Step 0: the safety net

- `shipgen snapshot --out DIR [CASE...]`: every case as plain files (`design/<case>.json`, `svg/<case>/`,
  `sprite/<case>.json`). Two runs of the same build are byte-identical (checked), about 10 s.
- The baseline is taken from the commit before step 1.

## 1. Split `Layout.BuildLayout` into stages

Today one 1,434-line method with dozens of local functions sharing mutable lists. Afterwards, `BuildLayout` reads as a
table of contents: one call per stage, each stage's inputs its parameters and its output a small record, so a stage
can be changed while trusting the rest stayed put. The shared `Layout` (footprints, weights, warnings, failures) is
still written by every stage; that is what the layout builds. The order of its side effects (warnings, failures,
`Occupy`) stays exactly as now.

Stages (each a commit, checked with the snapshot), in `partial class Layout` files beside `BuildLayout.cs`:

| Step | Stage | Out |
|---|---|---|
| 1a | the main battery's groups (fore, aft, midships, wing abreast, wing echelon); the bridge's and aft control's levels; the torpedo mounts' type | `MainGroups`, `ControlPlan`, `TorpedoPlan` |
| 1b | the middle's plan: plant segments, the gaps for midships and echelon turrets, magazine segments, the funnel plan, the item row (`seq` with its widths), and the row's rules (wing gaps and staggers, cross-deck ends, widths, what the middle needs, where its front goes) | `MiddleRow`, a class: the row and its methods |
| 1c | the end groups along the hull: bow and stern clearances, the deficit, the shift range (the `plan` return follows it) | `Ends` |
| 1d | the middle placed: the wing turrets' offset, the funnels', midships and wing turrets' x, the plant segments placed; the raised stretches | `MiddlePlaced` |
| 1e | main turrets: end groups, midships and wings; the riders level 1 is built under | mounts, riders |
| 1f | superstructure: the bridge tower, conning tower, aft control; the funnels trunked and placed | blocks, funnels |
| 1g | deck secondaries, one battery at a time | |
| 1h | casemates and level 1 (the deckhouse cut round what stands on the deck); torpedo mounts | level 1 blocks |
| 1i | deckhouse levels, fire control, AA, masts and boats, citadel and compartments, dressing | |

Done when `BuildLayout` is a page, and no stage method is over about 200 lines.

**Done 2026-10-17** (1a-1i, each byte-identical): `BuildLayout` is 98 lines, the stages in `Layout/Warship/`.
The longest are the middle row's constructor and `PlaceDeckBattery`, about 200 lines each, both one rule
throughout. mono-check and `dotnet test` unchanged.

## 2. Each rule in one home

The single-implementation rule, applied to what the review found:

- **The main armour deck** (bugs 1 and 2): one rule, `Armour.MainDeck`, used by the build, `ProtectionSchemes`, the
  designer's knob and mono-check: of the decks over the citadel or the whole length on the hull's stack, the thickest
  plate (flat or slopes), the higher of equals. The build stays byte-identical (checked). **Done 2026-10-17.**
  Decided (user, 2026-10-17): no marker on a scheme's deck and no new keys. "Main deck" is a choice, not physics,
  and should stay out of the model where it can. The accepted quirk: a scheme's lower deck thicker than the ship's own
  (French modern's 40/40 under Kent's 35 mm) becomes the main deck, so the layout doesn't read back as itself and
  putting it on again adds a deck. The old tie-to-the-lower rule hid this from the test while the decks stacked. The
  build still uses the rule physically (the belt runs up to it, the barbettes' armour stops on it, two hitbox
  fields); taking that out is a physics change for later, not this refactor.
- **The Overview levers**: `Shipgen.Levers` (a `NumberLever`: get, set, Auto, bounds, apply, the step as the
  Overview's arrows take it) is the one definition; the designer's `NumberKnob` wraps a lever (label, units), and the
  knobs that aren't Overview levers build theirs through the same properties. `Levers.Trim(d)` is the Overview's row
  list: the Overview builds its rows from it and mono-check sweeps it, stepped as the arrows step. **Done 2026-10-17.**
  mono-check's output changed once: it now sweeps each armour deck (no "thickest deck" lever), Cb and the armour
  scale; aa_heavy is gone (not an Overview row); steps start from Auto values, snap to the grid and stop at the style's
  limits, as the arrows do. Before: 29 reversals and 12 lumps over 228 sweeps; after: 27 and 16 over 237. The
  aa_heavy pair is gone; the new lumps are Cb (3, sense 0), endurance stepped from its Auto 1 day to the 5-day grid
  (2), and an armour deck added where a ship has none (pt_boat, tanker).
- **Navy and era names** (bug 3): `Shipgen.Render.Looks.Validate` reads looks.jsonc's navies (and the renderer's
  era list), the one list the designer's pickers and the drawing read too; Shipgen's copy is gone, and the golden
  capture is handed the check by its caller. Byte-identical. **Done 2026-10-17.**
- **Small duplicates**: sea-water density once (`Weight.Seawater`); `Vdc` and the double bottom in `Geometry`,
  `GunsAreSecondaries` in `Style`. Byte-identical. **Done 2026-10-17.** `ShipDesign.Interior`/`FormOf` stay: moving
  them is churn the review rates as not mattering much.

## 3. Who sets and reads `Geo`

A short note on each `Geo` field (and the `Layout` fields later stages read): which stage sets it, which read it, and
when it can be null. Uses item 1's stage names. Comments only, so byte-identical by construction (the snapshot is run
anyway).

**Done 2026-10-17**: every `Geo` field says what sets it and what reads it, and the class says when it is read (the
next solve reads the previous pass's Geo; PlanMachinery reads a half-made one; Build patches MagazineRooms in last).
`Layout.EndMounts` too. The other `Layout` fields read across stages (Deficit, NeedMid, NeedL, NeedB, Short) already
say so. Byte-identical.

## 4. Enums for the small closed sets

Deck extents, compartment kinds and mount kinds: today strings, where a typo compiles and falls through to a default
(`RoomPriority` 4, permeability 0.9). First a survey of every use (Shipgen, the renderer, the sim's compiler, which
already has enums of its own, and the designer), then enums for the internal model, with JSON that writes the same
strings, so the output stays byte-identical. The design input (player data) keeps strings checked by validation.
Scope and where the enums live (Shipgen, so the sim's compiler can share them) are settled after the survey.

**Done 2026-10-17** (user: through the output). `Model/Kinds.cs` holds `RoomKind`, `ComponentKind` and `Extent`, moved
from the sim (same byte values; `Hangar` added to the rooms for the carrier's hangar bay compartment) and used by the
layout, the built Ship, the renderer, the designer, the hit viewer and the sim's compiler and damage model. JSON writes
each member's snake_case name (`SnakeEnumConverter`, no list of strings anywhere), so every output is byte-identical;
an unknown kind now fails at load. The subdivision's priority and permeability tables are exhaustive switches (a kind
left out is a compile error: CS8509, with CS8524 off in Shipgen and the sim). What stays strings, on purpose: the
design input (validated), Crew's per-station tallies (they mix room and component kinds), and the UI's display
taxonomies (the hit viewer's kinds, the designer's labels) that take `Kind.Name()`.

## Afterwards

Every step byte-identical except item 2's deliberate fixes (the schemes' and the knob's main deck, mono-check's
levers). Left for later: the "main deck" in the physics (the belt's top, the barbettes' foot, two hitbox fields), the
thin-deck quirk under French modern, Crew's string keys, and the review's other items as they come up.

