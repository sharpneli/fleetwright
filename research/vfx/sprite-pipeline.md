# Ship sprite & design pipeline
Status: final    Updated: 2026-10-02    Request: -

## Decisions (2026-10-01)
- Style: clean vector, WWII steel navy, top-down 2D on a 3D game.
- The player designs ships with parameters: hull L/B/Cb, speed, range, armour, and main battery calibre/barrels with fore/aft counts. Secondaries per side, torpedo mounts, AA counts and an optional funnel count are also inputs.
- The player does NOT place parts. The auto-layout places everything by naval rules and balances the centre of gravity over the centre of buoyancy (this avoids "bazillion tons in the nose").
- Tonnage is derived, never entered. The model is a "naval architecture lite" (Springsharp-style): structure from L·B·D, machinery from power (admiralty coefficient vs Froude number), fuel from range, armour from citadel geometry, plus guns, mounts and ammunition. It iterates to a fixed point.
- Hitboxes are exported from the same geometry as the sprites (geometry.py is the single source of truth). Verified by pixel IoU of about 0.9–0.95 (the difference is the outline stroke and anti-aliasing). Thin casemate barrels score lower (~0.75) because the outline stroke widens them; the shapes themselves line up.
- Firing arcs are computed from obstructions, height-aware: superfiring clears lower turrets, and funnels and bridges block.
- The 3rd turret in a group superfires over the 2nd (gameplay choice; historically Nelson's X could not).

## Nation styles (2026-10-02), styles.py, set with "style" in the design
- us: Deck Blue turret tops, rounded bridge, oval funnels, quad 40 mm, broad deckhouse.
- uk: pale grays, boxy turrets and bridge, secondaries on deck in fore/aft groups, hangars and a catapult, pom-poms, cruiser stern.
- de (imperial): casemate secondaries (±65° arcs, below deck), twin turrets with rounded faces, round black-capped funnels, conning tower, pole masts, net shelves. "era": 1914 means heavier machinery and coal.
- jp: Kure gray, linoleum decks with brass strips, pagoda bridge, single raked funnel, long-rear turrets, triple 25 mm, stern catapults.
- Example designs are in designs/nations/*.json.

## Code (shipgen package)
- design.py (CLI), navarch.py (weights/stability), layout.py (placement/balance), hitbox.py (arcs + export), geometry.py (shared shapes), styles.py, shipgen.py (renderer), verify.py (pixel check), designs/*.json.
- Conventions: the bow points +x, angles run clockwise with 0 = ahead, and the origin is the sprite centre. Layers: hull_base, then turrets (z), then hull_upper.
- Calibration (std displacement): Iowa-like 47.7k (real 45k), Baltimore-like 13.9k (14.5k), Fletcher-like 1.9k (2.05k).

## Open items / next
- Port to standalone (the game's language), keeping geometry.py semantics identical.
- Carriers and submarines are not in the parametric designer yet (they exist only as hand specs in fleet.py).
- Amidships (Q) turrets, wing turrets, cost model, damage sprites, per-nation destroyers and cruisers.
- GM runs high on very beamy designs (Yamato-like 6.2 m vs ~2.9 m real); tune the BM/KG model. The stiffness warning threshold is 0.17·B.
- The WWI weight model undershoots (Derfflinger-like 20.8k std vs ~26k real).
