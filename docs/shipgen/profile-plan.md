# The profile: the ship from the side (plan)

For the game's identification card: the ship seen from the starboard side, in the style of SpringSharp's profile
drawings. A still picture; nothing turns, no mips. Called once per design and saved with the ship (savegame, later).

## The user's decisions (2026-10-11)

- Two pictures: the **full hull** (above and below the waterline, anti-fouling on the bottom) and a **recognition
  silhouette** (the part above the water in one dark colour). No photo treatment.
- The scale is an input (px/m); the drawing is vector, so it renders sharp at any scale. 10 px/m is the sprites' usual.
- **Stylistic uniformity with the top view comes first.** The profile and the sprite must look like one drawing seen
  from two sides.
- No stem rake, sheer or flare until the generator has them (the hull is the physical one: straight stem, flat deck).
  When the generator adds them to the hitbox model, the profile draws them; no faking them in the drawing.
- The generator doesn't change. The profile reads the built `Ship` only, and stays standalone: any code may call it.

## Shape: the sprite pipeline, seen from the side

The designer's profile pane is the hitbox mesh in 3D; it can't look like the sprites. The profile goes the sprites'
way instead, so the two share the drawing code (`Painter` rules, palette, outline, rasteriser):

| piece | mirrors | what it does |
|---|---|---|
| `SideView.Of(Ship)` | `TopView.Of` | the elevation from `Ship.Hitboxes`: every part as an x extent over Base..Top, with its y (depth: the far side is drawn first); the hull's outline from the hull-form stations (keel line, forefoot, counter), the waterline from `Vertical`; yards, booms, boats and cranes from `Dressing` |
| `SideArt.Build(SideView, Palette, Shapes, S, ...)` | `HullArt.Build` | the `Scene`: parts far to near, then the hull, then what stands outside the hull's side |
| `ShipProfile.Build(Ship, S, look)` | `ShipSprites.Build` | both scenes (full, silhouette), canvas and origin; `ShipProfile.Bake(GpuBaker)` gives the two `Image8` |
| CLI `profile DESIGN... [--out] [--scale]` | `draw` + `bake` | SVG and PNG of both, for looking at and checks |

Heights stay above the main deck as in the hitboxes; the scene's y is -z. The canvas is as wide as the sprite's at the
same scale (the hull's length plus `HullArt.PadM`, snapped the same way), so the plan and the profile stack into a
general arrangement with the hull ends lined up.

## Uniformity rules

1. **One scale, one line.** `Painter.Sw` follows the scale, so a profile and a sprite at one px/m have the same
   outline width. The outline is palette `line`, as in the plan.
2. **Same palette keys.** Superstructure level n is `levels[n]` in both views; funnel, funnel cap and band, mast,
   turret, barbette, barrel, tub, boat and crane are the same keys. The hull side is `hull` (the colour the plan shows
   round the deck). New keys for what only the side shows, in `default_palette` so every look has them and any look
   may override them: `bottom` (anti-fouling), `boot_top` (the waterline band), `propeller` (bronze).
3. **Same light.** The plan puts a lighter rim on the edges toward -x, -y (aft, port). The profile keeps the aft
   component and lights from above: a lighter rim on top and aft edges.
4. **Same seeds.** Anything random in both views comes from the same `ShipRng` stream (dazzle cuts are positions along
   x, so the panels meet across the deck edge; the pennant number is the plan's).
5. **Same shape vocabulary.** Every look shape gets a side form: funnels (box, oval, capped, pan, hat, rake), masts
   (pole, tripod, fighting top, tiers, cage, lattice), block corners, and the armoured turret looks (standard, slab,
   round, classic, faceted, drum, hooded, lancia, champignon, quadruple).

A look may draw a part a little over or under its hitbox, never move or reshape it (CLAUDE.md). Inside a part's box
the drawing chooses freely: the gun's trunnion height, a sloped turret face, window rows, the vision slit band.

## Steps

1. **SideView**, the data step, with tests over every design: no throw, every part's extent inside the hull's length
   and the hitboxes' heights.
2. **The plain profile**: hull with bottom and boot top, raised decks, superstructure tiers, funnels, masts, turrets
   and barrels, AA, the conning tower, boats, propellers, shafts and rudders; the silhouette; the CLI command. Looked
   at across navies, eras and styles (battleship, cruiser, destroyer, carrier, merchant, planing).
3. **The look variations**: turret looks, mast and funnel shapes, dazzle shared with the plan, hull number, portholes
   and rails.
4. **Checks**: `profile-verify` (the silhouette against the hitboxes seen from the side, the side twin of `verify`)
   and SVG goldens beside `svg-check`.

## How it went

- Steps 1 and 2 (2026-10-11): `SideView`, `SideArt`, `ShipProfile`, `ProfileBake`, `shipgen profile`, and a Profile
  tab in the exe (`ProfileScene`, made only while open). Tested over every golden case that builds.
- Step 3 (2026-10-11): side forms for the ten armoured turret looks (`SideArt.Profiles`, `Roofs`, hooded and
  champignon drawn apart), the plan's hoods and cupolas and rangefinder ears at the same places; dazzle from one
  `DazzleScheme` for both views (the plan's output unchanged; the side unrolls each cut over the hull's real edge, so
  panels meet along the deck and roof edges); the hull number on the bow; scuttles a deck below each open deck (none
  through the side armour, casemates or the fine ends); bridge window bands on each control position's top level,
  scuttles on other deckhouses; rails on stanchions (not in the silhouette, not over a hangar); the hawse and anchor.
  Tumblehome shading in both views: five bands from the knuckle to the deck (`TopView.ShadeBands`), each 3% whiter,
  the side turning toward the light as it leans in; the four tumblehome designs' plan SVGs rewritten on purpose.

## Known gaps (the generator's, not the drawing's)

- Straight stem and flat deck (no rake, sheer, flare): picked up once the hitbox model has them.
- Turret parts (hoods, rangefinder ears) have plan shapes only; the side gives them a height inside the gunhouse box.
- Clutter has no heights of its own beyond its columns; only what breaks the outline is drawn from the side.
- Liberty's propeller is 1.45 m across in the hitboxes, small for a 142 m freighter; the profile shows it as built.
- The plan's canvas is sized from the hull's length, so a flight deck overhanging the stern is cut off there; the
  profile's canvas fits it.
