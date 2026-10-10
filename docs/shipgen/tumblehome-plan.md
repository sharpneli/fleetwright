# Plan: physical hull form variations, tumblehome first

2026-10-10. Status: proposed, not agreed. Background and options: `hull-form-variations.md`.

## Goal

Hull shape comes from the designer, not the look. Tumblehome is the first variation. It must be real in the hitbox
model, so the layout, weights, stability, shells and drawing all see the same hull. The look's `tumblehome` key goes
away. The architecture must also take the later variations (flare, bows, sterns, freeboard profiles, bulges, the
oddities) without another rewrite.

## Where we are

- `Hull` (`Hull.cs`) is the **planform at the main deck**: `HalfWidth(x)`. It is also the maximum beam, because nothing
  is wider than the deck.
- `HullForm.HalfWidth(x, z)` fits superellipse sections under the waterline (Cb, Cm, Cwp, keel, cut-up). Between the
  waterline and the deck it is a **linear ramp** from the waterline breadth to the deck breadth. Above the deck it
  returns the deck breadth. The waterline is clamped to be no wider than the deck. So a hull is either wall-sided or
  flared, never tumblehome.
- The export is the station table (`HullFormReport`: 49 stations, about 12 heights each, per-station z). Its
  `HalfWidth` is the one reader that the views and the game share: linear in z per station, then linear in x.
- About 40 layout and render call sites use `lay.Hull.HalfWidth(x)` as "the deck edge" (`grep` list in the step 1
  notes below).
- The look fakes tumblehome: `HullArt.DeckHull` draws the deck `B/(1+th)` narrow inside the physical hull
  (`looks.jsonc` toulon victorian 0.07, great_war 0.05).
- Stability is parametric (`Batteries.Evaluate`: GM from L, B, Cb and KG). There is no large-angle GZ. Hull and deck
  weights are parametric in L, B and D (`HullWeight.DeckArea` = `B*L*(0.66+0.33cb)`).
- `ShellTest` marches shells in 5 cm steps against `HullForm.HalfWidth`. `HitboxMesh` draws the hull as stacked slab
  prisms.

## Architecture: one hull surface, made of terms

The hull is one function, plus the deck line over it:

```
W(x, z)    half-breadth, from the keel up through the main deck and the raised hull decks
DeckTop(x) the top of the hull (main deck, raised stretches, later sheer)
```

Every design option is a term in how `W` is built. No consumer knows which options were chosen:

| Region (design input) | Term | Where it acts |
|---|---|---|
| `hull.section` (cross-section) | **Topside**: `W` above the waterline. Wall-sided (today), tumblehome, later flare | z > T |
| | Below-water character (later V, chine, box: today's fullness/character) | z < T |
| `hull.bow`, `hull.stern` | the planform ends (today's `HullEnd`) plus underwater volumes (ram, bulb) | the ends |
| `hull.profile` (later) | `DeckTop(x)`: sheer, forecastle, breastwork | the top |
| `hull.addons` (later) | volumes added to `W` (bulges) | below the waterline |

### Types

- **`IPlanform`**: `L`, `HalfWidth(x)` and `Points(inset, maxHw, xMin, xMax)`. `Hull` implements it (the spec-driven
  planform). A `TabulatedPlanform` (samples, linear) implements it for derived outlines. Painter, Clutter and the layout
  take `IPlanform`, not `Hull`.
- **`Hull` stays the maximum-beam planform**, with `B` as the maximum beam. That is the beam naval architecture uses
  (GM, resistance) and the beam the designer types in. For tumblehome ships, history quotes the maximum beam too.
- **`ITopside`**: `Breadth(x, z, wl, P, T, D)`, where `wl` is the fitted waterline half-breadth and `P` is
  `Hull.HalfWidth(x)`. It also has `DeckRatio(x, h)`: the half-breadth ratio at height `h` over the main deck,
  relative to `P`.
  - `WallSided`: today's ramp, bit for bit. It is the default, so the goldens hold.
  - `Tumblehome(strength, knuckle, extent)`: `W` ramps from `wl` up to `P` at the knuckle height, then curves in:
    `W = P(1 - a·env(x)·((z - zK)/(D - zK))^p)`, continuing above `D` for the raised decks. `a` is set by `strength`
    (1.0 gives a main deck of about 0.72 of the maximum beam, to be checked against research). `env(x)` is 1 over
    the midbody and fades toward the ends when `extent` is `"midships"` (the Borodino choice). `p` is about 1.6, for
    the French curve that steepens upward.
  - Later: `Flare`, `Knuckled`, and the rest.
- **`HullForm`** composes them. `HalfWidth(x, z)` is valid up to the top raised deck. **`Deck(h)`** returns the
  `IPlanform` of the hull edge at height `h` over the main deck: `Deck(0)` is the main deck edge, `Deck(n·pitch)` a
  raised deck's.

**Order problem.** The layout runs before the section fit, and the layout needs the deck edge. The topside therefore
depends only on `P`, `T` and `D`, never on the fitted waterline, above the knuckle. So `Deck(h)` is known at layout
time: the layout already has `Navarch.Result`, which gives T and D. Only the ramp from the waterline to the knuckle
uses `wl`, and nothing in the layout sits there.

### Design input

```jsonc
"hull": {
  "beam": 21.4,                                   // maximum beam, as now
  "section": { "topside": "tumblehome", "strength": 0.8, "knuckle": 0.2, "extent": "full" }
}
```

- `knuckle` is the height of the maximum beam above the waterline, as a fraction of freeboard (default 0.2).
- `extent` is `"full"` or `"midships"`.
- `strength` runs from 0 to 1. Validation rejects any combination whose main deck can't hold the main battery's
  barbettes. The layout's error says so ("tumblehome too strong for the wing turrets").

This follows the variations doc's region selectors (`section`, later `bow`, `stern`, `profile`, `addons`). I kept
`topside` plus `strength` rather than one signed slider: flare and tumblehome differ in shape as well as sign (flare is
mostly forward).

## What tumblehome changes, and where it comes from

| Effect | How it arises |
|---|---|
| Narrower deck: less room for wing turrets, deckhouses, boats, AA | The layout places on `Deck(h)` (step 1 audit) |
| Lighter topsides, lower KG | `HullWeight.DeckArea` and the armour deck widths use the deck breadth at their height. The side shell gets a slant-length factor. KG drops through the weights, and GM follows |
| Sloped upper belt and strakes | The belt lies on `W`. The ray tracer returns the surface normal, so obliquity emerges with no special rule |
| Less reserve buoyancy, GZ that peaks early and falls off fast | **Only with step 7 (GZ from the table).** Before that step, tumblehome is close to a free lunch |
| Casemates in the side | Placed at `W(x, z_casemate)`, not at the deck edge |
| Wider arcs for wing guns on sponsons | Later: wing mounts may stand on sponsons out over the tumblehome, within the maximum beam |
| Drawn hull | The top view draws the silhouette (`max_z W`) and the deck edge `Deck(0)`. The band between them is the sloping side. The look's key is deleted |

## The curved hull: one surface, three consumers

**The physical truth is the exported station table, as today.** The generator's analytic `HullForm` is the design-time
tool. The table is what the game, the views and the ray tracer read. The approximation is ours to choose, and a test
pins its error.

**It is already a piecewise-bilinear height field.** Take one strip between two stations, and split it by the union of
both stations' z breakpoints. In each cell, `HalfWidth` is exactly bilinear in (x, z). So each side of the hull is
`|y| = W(x, z)` over a rectilinear grid of cells. That holds for anything with one half-breadth interval per (x, z):
tumblehome, flare, chines (a row at the chine), bulges, rams and bulbs (stations past the stem), and popovkas. Only a
catamaran or trimaran breaks it, and those become several bodies, each its own field offset in y.

The table needs more rows above the waterline once the topside isn't wall-sided: the knuckle, 3 or 4 rows on the curve,
the deck and each raised top. Wall-sided hulls keep today's rows, so the goldens hold. A test samples the analytic form
against the table: worst deviation under 3 cm on every design and fuzz case.

### `HullField` (Shipgen, plain arrays, no garbage)

The table is compiled once per ship into flat arrays: station x, per-strip z rows, `y` per node, and per-cell `maxW`.

- `HalfWidth(x, z)`: binary search on x, then on z. This replaces the linear scans of
  `HullFormReport.HalfWidth`, and gives the same values.
- `Normal(x, z)`: from the cell's bilinear gradient, `(-∂W/∂x, ±1, -∂W/∂z)`.
- `Inside(p)`: `keel ≤ z ≤ DeckTop(x)` and `|y| < W`.
- **`Raycast(origin, dir, tMax)` returns `(t, point, normal, face)`.**
  1. Clip the ray to the hull's AABB.
  2. Run a DDA over strips in x. Skip a strip whose `maxW` box the ray misses.
  3. Within a strip, walk the z rows the ray crosses.
  4. Per cell, `y(t)`, `x(t)` and `z(t)` are linear, so `±y(t) = W(x(t), z(t))` is one quadratic per side. Take the
     first root inside the cell.
  5. Deck tops are planes (a 1D curve once sheer arrives).

  A shell crossing the hull visits a handful of cells, so this is exact against the game's `HalfWidth` and costs
  around a microsecond. The normal gives obliquity against the belt.
- `ShellTest` moves onto it. Its old 5 cm march stays in the tests as a reference: march and raycast must agree.

### Rendering

The same cells, two triangles each, with vertex normals from the bilinear gradient: smooth shading with no visible
facets. 50 stations × about 20 rows × 2 sides × 2 is about 4k triangles, trivial. In `HitboxMesh`, this hull skin
replaces the stacked slab prisms, which would look like a staircase on a tumblehome. The per-pixel clip works
unchanged. The whole-prism clip treats each strip as a pickable piece. The skin and the bilinear surface differ by at
most a few millimetres between diagonals. That is fine for drawing, and it never feeds back into physics.

The top-down sprites read the same table:

- the silhouette is `max_z W` per sample x;
- the deck edge is `W(x, DeckTop)`;
- the height map gets the sloping band as a ramp, so the sun shades it like a curved side.

### Stability from the same table (step 7)

Cross curves of stability: `KN(φ, Δ)` over heel 0–90° for a few displacements. Each comes from clipping the station
polygons with an inclined waterline at constant volume, so it is cheap: 50 polygons of about 40 points. The ship
stores the table, and the game gets `GZ = KN - KG·sin φ` in O(1), including for flooding (KG and Δ change, the curve
doesn't). The designer gets the GZ curve, the range of stability and the reserve buoyancy (table volume between the
waterline and `DeckTop`), plus large-angle warnings. This is where tumblehome pays its Borodino price.

## Steps

Each step is one verified commit.

1. **Deck vs hull audit (golden-neutral).** Add `IPlanform`, and give the layout `Deck` (for now the same object as
   `Hull`). Sort every `lay.Hull`/`hull.HalfWidth` call site into one of three groups:
   - on the deck → `Deck(h)`: mounts, deckhouses, boats, AA, raised decks, clutter;
   - inside the hull at z → `form.HalfWidth(x, z)`: barbette in-hull, casemates, plant widths, Subdivision's
     `Widest`;
   - true maximum beam → `Hull`.

   `golden-check` 371/371, `svg-check` and `sprite-check` unchanged.
2. **Topside term (golden-neutral).** Add `ITopside` and `WallSided` (today's ramp, exact), `HullForm.HalfWidth`
   valid above D, `HullForm.Deck(h)`, and `hull.section` input with validation. The fuzz mutator learns `section`.
   Still 371/371.
3. **Tumblehome.** Add the shape, extra table rows, deck-breadth weights (deck area, armour deck widths at their z),
   casemates in the side, and the barbette/deck-width layout error.
   - `bouvet.json` (and other French designs the user picks) get `hull.section` in place of the look's tumblehome.
   - A new 1945 design with strong tumblehome joins the fleet.
   - The affected goldens are updated on purpose, noted in `shipgen/golden/README.md`. Weights shift, which is
     fine (memory: weights follow physics). The shift is reported.
   - `shell-test` must stay tight on them.
4. **Drawing.** `TopView` gets `Silhouette` and `Deck` from the table. The height-map ramp is added. `HullArt.DeckHull`
   and the `tumblehome` shapes key are deleted (looks.jsonc, README "Looks"). `svg-check` and `sprite-check` are
   rewritten on purpose, `verify` ALL OK, and the French looks are checked in the viewer by `-screenshot`.
5. **`HullField` and the raycast.** Tests cover agreement with `HullFormReport.HalfWidth`, raycast vs march, normals,
   and no allocations per call (`alloctop`). `ShellTest` moves onto it and gets faster.
6. **Hull skin in the hitbox view.** The lofted mesh replaces the slabs. Checked with `-view=hitbox -camera=bow` on the
   tumblehome designs.
7. **Cross curves, GZ, reserve buoyancy.** These go in the report and hydrostatics, with large-angle warnings. Before
   players get tumblehome, ideally.
8. **Later, on the same frame:**
   - wing sponsons over the tumblehome (arcs);
   - flat-sided casemate stretches;
   - flare as a second topside;
   - bow and stern dropdowns (ram/bulb as stations past the stem);
   - `DeckTop` sheer and profiles;
   - bulges as an additive term.

## Questions for the user

- **Beam = maximum beam** (recommended), with the deck narrower? The other choice is beam = deck beam, with the hull
  bulging out below it.
- **Strength scale:** at 1.0, the main deck is about 0.72 of the maximum beam. Should the cap go further, for the
  Avant-garde extreme?
- **Raised hull decks:** should the tumblehome carry on up the forecastle sides (recommended, as on the French ships)?
  Deckhouses stay vertical.
- **GZ before or after shipping:** should step 7 land before tumblehome reaches players? Otherwise it is mostly upside
  for a while.
