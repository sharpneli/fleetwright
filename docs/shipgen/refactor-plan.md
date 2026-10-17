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

## 2. Each rule in one home

The single-implementation rule, applied to what the review found:

- **The main armour deck** (bugs 1 and 2): one definition in Shipgen, used by `Armour`, `ProtectionSchemes`, the
  designer's knobs and mono-check. The build's own rule (`Armour.ArmourGeometry`: thickest, the higher of equals, on
  the merged decks) is the one kept, so the built ships don't change. The input-side callers then follow it, which
  changes what the scheme and the knob pick where they disagreed: that is the fix, its own commit, with tests for
  the stray 40 mm deck (bug 1) and the thin-over-thick case (bug 2).
- **The Overview levers**: moved from the exe into Shipgen as the one definition; the designer's Overview and
  mono-check both read them (the review's suggested order 2). mono-check's output changes only where its old copy
  differed from the real lever, and that is reported.
- **Navy and era names** (bug 3): one list. Shipgen can't read the renderer's `looks.jsonc`, so the list moves to
  where both can see it, or validation is handed the names by its caller; worked out when we get there.
- **Small duplicates**: sea-water density (`Weight.Seawater`, `Flotation.SeaT`) and the sideways helpers the review
  lists (`CarrierStyle.Vdc`, `GunsAreSecondaries`, `ShipDesign.Interior`/`FormOf`, `Powerplant.DoubleBottom`), moved
  to where their callers expect them. Byte-identical.

## 3. Who sets and reads `Geo`

A short note on each `Geo` field (and the `Layout` fields later stages read): which stage sets it, which read it, and
when it can be null. Uses item 1's stage names. Comments only, so byte-identical by construction (the snapshot is run
anyway).

## 4. Enums for the small closed sets

Deck extents, compartment kinds and mount kinds: today strings, where a typo compiles and falls through to a default
(`RoomPriority` 4, permeability 0.9). First a survey of every use (Shipgen, the renderer, the sim's compiler, which
already has enums of its own, and the designer), then enums for the internal model, with JSON that writes the same
strings, so the output stays byte-identical. The design input (player data) keeps strings checked by validation.
Scope and where the enums live (Shipgen, so the sim's compiler can share them) are settled after the survey.
