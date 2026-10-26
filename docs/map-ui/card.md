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
