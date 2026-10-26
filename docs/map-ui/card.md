# The ship card: the damage plate (mockup 2026-10-26)

The card at the right of the battle map (`MapScene.Card.cs`, battle-hud.md U3b) redrawn in the UI language, the condition
diagram first. The mockup is `card.html` (`png/card-mock.png`), drawn over the game's capture of the card as built
(`png/card-before.png`). Lion-like's geometry is her class's, dumped from `ShipClass` into `assets/card-lion.js`; the damage
is made up.

## The plate

The old diagram drew every cell at one weight, the upper works as grey slabs and every healthy mount in green, so damage
had to compete with the drawing. The plate is a damage-control drawing instead:

- **Profile over plan, one length scale**, bow right, the profile's heights ×2 (said in the title strip). A 50 m bar top
  left, the length in twentieths AP to FP under the plan, amidships marked.
- **Hairlines**: the hull's real side (the hull field's deck tops and bottom, each strip's step drawn through its
  middle), the watertight bulkheads and the decks inside it, the armour deck a touch heavier. Cells are not drawn; only
  water fills them.
- **The upper works as one silhouette** a run (the blocks' skyline), their levels faint inside; funnels dark with a cap
  line; main mounts as gunhouses with their guns. No masts (they would double the plate's height).
- **The sea**: her waterline now, solid, heeled and trimmed, past her ends; the design's dashed; the sea faintly under
  it, so lost freeboard shows. One annotation row under the keel: draughts at her ends, the water under each flooded run
  (tonnes; `cf` for counterflooding).
- **The plan shows the side**: each section's water by band (port, centre, starboard), so a list reads from the picture.
  Centreline dash-dot.
- **Colour only where something is wrong.** Healthy is the drawing's line colour; amber jammed, down (dashed) or wrecked
  (hatched); red out of action, destroyed (hatched) or a hole; water blue; counterflooding blue hatched (not green: green
  is "within limits"); fires as small flame marks. A loading main mount carries its loading clock as an arc.
- **Readings** under the plate, a table: afloat (bar, water aboard), trim, draughts, power, steering, fires, safe speed,
  crew; beside it a **clinometer**, her midship section heeled to the true angle with the sea level and a needle on a
  ±20° arc.

## The rest of the card

Restyled to the workstation pieces, content as before: tabs as the designer's, her name as a resource bar with two status
chips, the four figures as resource-bar cells, every section a title strip. Guns: one row a main mount; secondaries one
row a side with a pip a mount (state as on the plate) and only what's wrong in words. Fire control a row a battery with
its phases as a segmented strip; damage control a row a party at work.

## For the port

Everything drawn is in the class or the snapshot already: the profile and section from `ShipClass.Hull` (`DeckTop`,
`HalfWidth`, made once a class), bulkheads and decks, components; water by `FloodCell`/`FloodShare` (tonnes from the
cell's volume and permeability), holes (`HoleZ`, `HoleY`, `HoleM2`), fires (`FireCells`), parts' states, mounts' states
and `MountUntilMs` against the battery's reload, the attitude for the waterline, draughts and clinometer.

## As built (2026-10-26)

The user took the mockup ("I like that a lot") with three answers: healthy mounts neutral; the clinometer stays ("we
want to see the capsizing"); no masts ("turrets and flooding at a glance"). The whole card was ported, and with it the
enemy's and a unit's cards, which share its pieces.

- `MapScene.Plate.cs`: the plate and the clinometer. What it draws of a class (`PlateShape`: the side sampled from the hull
  field, each strip's step drawn through its middle; the decks and watertight bulkheads inside her; the upper works' runs
  as outlines; the deck's half-breadth by station; the midship section) is made once a class, with the frame's scratch
  (the fullest space by section and tier, and by section and side; the water by section; her parts' states). The
  waterline and the clinometer read the sim's own attitude (`Attitude.At` going down, `HitQuery.WaterOf`), so a capsize
  rolls the section over as it rolls the sprite. The annotation row places the water's tags left to right between the
  draughts and drops one with no room (the table has her water).
- `MapScene.Card.cs`: the card's layout on `Pieces` (draw list, explicit rows, a scrolling body): the dock's tabs, the head
  and its chips, the figures' cells, title strips that fold on a click (one set for every card), table rows. A battery
  with more than four mounts shows one row (pips, what's wrong in words) until its row is clicked.
- `Pieces` gained UTF-8 text (`Width`, `Text`, `TextRight`, `TextClipped`, chips and tags of `U8` text), a hostile chip and
  the dock tab; `Theme` the flame's colours and the hostile chip's.
- The orders in the enemy's and a unit's cards are still the strip's buttons, until the bottom panel's mockup.

Departures from the mockup: a row a secondary battery, not one for all of them (they have their own targets); the profile's
water is each section and tier at its fullest band, so a run of partly flooded tiers reads as bands, as it is; the
clinometer's arc is ±45°, labelled every 15° by its ticks only.

Checks: `PlateTests` builds the plate for every design and checks it holds together; the card allocates nothing on a frame
without a new hit (measured round `Card()`: only a hit's frame remakes her hits' lines, as before). Captures:
`png/card-game-damaged.png` (Lion with dev holes, flooding, a jam, a wreck, fires and the hit bench), `card-game-enemy.png`,
`card-game-squadron.png`, `card-game-capsizing.png`.
