# Ship sprites lit: relief, materials, sun and moon

Agreed 2026-10-14. The ship sprites get a PBR-inspired model (albedo, normal, roughness, metallic, a little ambient
occlusion), the sun and the moon, so plates catch the light, barrels show a line of specular on close zoom and
brass shines. The sprites stay 2D: what's new is per-pixel surface data baked beside the colour and a richer sprite
shader.

**Why:** the ships should look more real. Cartoonish ships will clash with the realistic effects to come, so the
target is a realistic look, not an illustrated one.

**PBR-inspired, not strict PBR.** The BRDF is the standard one so materials behave plausibly, but the lights are set
by eye: the sun's strength and colour, the moon, the ambient. Nothing is held to energy conservation or physical
units where looks say otherwise.

## Restrictions

- **The pipeline doesn't change.** Single-pass forward, in the globe's pass as today: no new passes, no G-buffer, no
  change to the main target (HDR R16G16B16A16, 8x MSAA), the blit or the engine. What changes is `shipmap.frag`/`.vert`
  (and `shipview_sprite.frag` for the ship viewer), the textures they bind, and the bake that makes them.
- **Ships only.** The water is its own work: a separate FFT water surface pass (prototyped in the old code and in
  WebGL), which brings the sea's light, the moon's path on the water and the rest. Nothing here touches the globe's
  sea. What the two share is the light: the sun, the moon, their colours and the sky and ambient come from one
  place, so the ships and the water are lit alike, and the ships' ambient "sea below" and analytic sky read the
  water's colours once it has them (today's `globe.frag` constants until then).
- **The sprites stay 2D quads.** No meshes; the relief is a normal map, never geometry.
- **One physical model** (CLAUDE.md, "The sprites are drawn from the physical model"). Relief comes from the heights
  the hitbox model already gives (the height map's columns) and from the drawn parts' own sizes (a barrel's width is
  its bore's). The lighting reshapes nothing.
- **Visuals stay light.** Each step below is visible on its own and could be the last; stop when it looks right.
- **No garbage per frame.** The quad stays 160 bytes; the light goes in the uniforms.

## Where we are

- The bake (`Fleetwright.Shipgen.Render`) draws display lists (`Scene`, from `HullArt`, `TurretArt`, `Clutter`,
  `Painter`), lowered to triangles (`Lower`) and stencil-then-covered in **flat colours** (`GpuBaker`,
  `shipbake.frag`). Per class: the hull RGBA, each turret type's RGBA, and the hull's height map (R8, 0.25 m a step),
  mipmapped (`SpriteImages.Chain`), uploaded by `ShipArtLibrary`.
- **The lighting was painted in** (until L3). `Painter.Shade` faked it: the lighter 1.12 inset on turret roofs, the highlight
  stroke along each barrel's port side, darker sloped face plates, the dark ink outlines (`Painter.Ln`). A turret
  turning carries its painted highlight round with it, which is wrong under any real sun.
- **Lit since L1** (2026-10-14): `shipmap.frag` and `shipview_sprite.frag` shade every sprite pixel through
  `Include/shiplight.glsl` (flat normal, one material) from the lights `ShipLight.cs` computes once a frame at the
  view's centre (`LightTable`: the one place the lights are set; `ShipLights.Compute`). Before L1 it was one number,
  0.3 + 0.7 x the terminator. The shadows (`shipmap_shadow.frag`, the turret silhouettes) take the stronger light.
- **Relief since L2** (2026-10-14): the bake writes a relief layer (`GpuBaker.RenderRelief`, R32F metres) from draws
  with a `Style.Relief` (plateaus, and since L3 cylinders, domes and slopes, evaluated per pixel in `shipbake.frag`:
  the hull's height columns at their exact tops, then the hull drawing's profiled parts; the turret drawing's), and
  `ReliefMaps` makes each layer's normal + AO (RGBA8) on the building thread, mipped by coverage; the sprite shaders
  sample it beside the colour.
- **Materials since L4** (2026-10-14): each fill's colour carries its palette key (`Painter.C`, `Rgb.Keyed`), the
  drawing may set a key over it (`.Mat("glass")`), and the look's tables (`default_materials`, `era_materials`, a
  look's own `materials` in `Data/looks.jsonc`) give each key a roughness and metallic; the bake draws the display list
  a second time in those (`DrawList.WithMaterials`) into an RG8 layer premultiplied by the colour's coverage. The
  shader widens roughness by Toksvig and reflects an analytic sky.
- **The frame is display-referred.** The sprite textures are UNORM, not sRGB, blended as they are; the blit copies
  with no tonemap. So the sea and the sprites are sRGB-coded values, and anything over 1 clips.

## The model

Per pixel of a sprite layer (the hull and each turret type):

| | Texture | Notes |
|---|---|---|
| albedo | the RGBA we have (premultiplied) | painted lighting taken out (step L3) |
| normal + AO | RGBA8: xyz biased, AO in a | sprite space: x to the bow (+x), y to starboard (+y), z up. Mips by plain averaging, **not renormalised**: the shortened normal is Toksvig's input (below) |
| material | RG8: roughness, metallic | from the look's materials (step L4); premultiplied by the colour's coverage, which it shares, so the shader divides by the colour's alpha |

Memory: a hull at 10 px/m (Bismarck, 2432 x 384) is 3.7 MB of RGBA; this adds 3.7 + 1.9 MB, so the art goes from
~100-150 MB to ~250-350 MB for a Jutland, uncompressed. Fine for now; BC5 (normals), BC4 (AO) and BC7 bring it back
under today's if we ever need to (a CPU encoder on the building thread).

**Lighting**, in linear light, in the sprite shader:

- **Sun:** one directional light, its colour warming and dimming as the sun gets low, zero below the terminator
  (the same smoothstep the globe and `Light` use, so ships and sea darken together; time-lapse keeps today's
  day-average path).
- **Moon:** the real one (`Moon` in Sim.Core): a second directional light, mild and cool, from
  `Moon.Direction(view centre, t)` once a frame (its parallax across one view is far under a degree). Its strength is
  the moon's light at that place, one function of time and place in Sim.Core beside `Moon` (the phase law: a half
  moon about a tenth of a full one; nothing below the horizon; dimmer and warmer low down), so the sim's night sight
  can read the same number when it learns the moon and the two can't disagree. The display maps that number to
  brightness by eye: a full moon high gives the hull's relief and a faint glint on gloss paint, never daylight; a new
  moon leaves only the ambient. By day it is there but lost under the sun.
- **Shadows by the stronger light:** the height-map shadow and the turret silhouettes take the direction of whichever
  light is stronger, crossfading through twilight, and their opacity follows that light. A full moon casts soft
  shadows; a new-moon night has none. Same passes, a different vector.
- **Diffuse:** Lambert, albedo x (1 - metallic).
- **Specular:** GGX with Schlick Fresnel and the Smith term, F0 = 0.04 for paint, wood and canvas, the albedo for
  metal. Roughness widened by **Toksvig** from the mip-averaged normal's length, so a barrel's glint is sharp at
  10 px/m and fades out by itself as the mips blur the cylinder away. That is the "specular on closer zoom", with no
  zoom rule.
- **Ambient:** a hemisphere: sky above, the sea's colour below (`globe.frag`'s `DaySea` today, the water pass's
  later), times AO. Its
  colours and strength by day, at dusk and at night are tuned by eye, in one place (a small table of uniforms the
  ship viewer's panel can edit live). Night has a readability floor: on a moonless night your own ships still read
  as ships, not black cut-outs (today's 0.3 floor is the reference).
- **Time-lapse** (`SunLapse.Motion` < 1): the moon goes the sun's way. Its light fades to the night's mean for the
  phase, and its glints and shadows fade out, since a moon spinning round the sky would flicker.
- **Environment:** metal with nothing to reflect looks black, and there is no cube map in a single pass. An analytic
  sky instead: the reflected ray's z picks zenith, horizon haze or sea, blurred toward the hemisphere's average by
  roughness, weighted by the Karis analytic env-BRDF fit. A few ALU, no textures.
- **Out:** encoded back to the frame's sRGB-coded values, highlights through a soft shoulder, so a glint rolls off
  instead of clipping (the blit has no tonemap and we don't add one).
- **Calibration rule:** a flat, matte, up-facing pixel at the reference sun (high noon) comes out as today's colour.
  The look doesn't shift; it gains relief and glints. Step L1 checks this with a screenshot diff. This anchors the
  start only: the lights may move off it later by eye.
- **Glints in shadow (an approximation):** the shadows are drawn over the lit sprite, darkening it by their opacity,
  so a sun glint inside a shadow is dimmed, not removed. Glints are mostly on the turrets, which stand above most
  shadows. If one shows where it shouldn't, the sprite shader can march the height map a few steps only where the
  specular is bright, without another pass. (Step S puts the hull's shadow under the turrets and has the turrets
  receive it in their own shader.)

**Frames for free.** The quad's own edges (u along the sprite's x, v along its y, metres from the eye) are its frame.
The vertex shader takes the world's sun and moon from the View uniform and projects them and the eye (-corner) onto
(u, v, up = v x u), normalised. A turret's quad is already turned to its bearing, so its light and view come out in
its own frame at any bearing with nothing new per quad on the CPU. The eye per vertex also makes the glints slide a
little as the map pans, as they would under a real camera.

## The bake

**Relief.** A new mode of `GpuBaker`: a relief layer in metres (R16, 1 cm or finer), one sample, each draw writing a
height by a profile instead of a colour (later draws win, as the painter's order already makes them):

- *plateau:* a constant (the height map's columns are exactly this, with their real tops);
- *cylinder:* an axis and a radius, height = base + sqrt(r² - d²): barrels, torpedo tubes, masts, yards, booms, the
  boats' hulls along their length;
- *dome:* centre and radii: cupolas, searchlights, ventilator heads, capstans, a drum turret's roof;
- *slope:* a plane: sloped face plates, the hood fronts.

Only the cover shader learns these; stencil, winding and clip stay as they are. Draws carry the profile from a new
`Style.Relief` (inherited like the fill), set where the art already knows the part: `TurretArt.Barrels`, the clutter's
kits, the hull's columns. A draw with no profile leaves the relief alone: paint on a surface (dazzle, deck stripes,
the hull number, recognition bands, plank lines) must not flatten what's under it.

**Normals and AO** from the relief on the CPU, on the building thread (one implementation, headless, testable):
blur ~1 px, Sobel, slopes clamped so a 3 m wall reads as a bright or dark rim a pixel or two wide rather than a black
line. The rim on every block's edge is most of the realism; the cylinders and domes are the rest. AO from the
relief: how far the surroundings stand above a pixel within a metre or two (the deck darkens along the deckhouse's
foot, inside gun tubs, round the barbettes). Where the sprite is empty the normal is straight up, and the normal
mips are weighted by coverage, so a silhouette's edge doesn't pick up the empty canvas's normal as it blurs.

Turrets get their own relief (they have no height map now): the body a plateau at its roof over its deck, the barrels
cylinders, the hoods domes or plateaus. Only the normals and AO use it; the turret shadows stay as they are.

**Materials.** `Style.Mat` (inherited), defaulted from the palette key the colour came from (`Painter.C("deck")` is
deck wood, `"turret"` is paint, `"barrel"` gloss paint), set explicitly where the drawing knows better (brass,
glass, a bare steel muzzle, canvas). Each look in `Data/looks.jsonc` gets a `materials` table (key -> roughness,
metallic), so a Victorian ship is glossy white and buff with varnished wood and polished brass, and a 1940s one matte
grey. The material layer is the same display list drawn a second time with each op's (roughness, metallic) for its
colour.

**What the CLI writes:** `shipgen bake` adds `relief.png`, `normal.png` and `material.png` (and their `_mips`) beside
`hull.png`, for looking at. `sprite-check` and `svg-check` change only where the art changes (step L3), each change
through `golden-update` and noted in `shipgen/golden/README.md`.

## Metallic: realistic first

Decided: realistic first, and glossy paint is fine. Almost everything on a warship is painted, and paint is a
dielectric, so painted steel is metallic 0 with a fairly low roughness (gloss paint). The metallic channel is for
what wasn't painted: brass (ventilator rims, binnacles, Victorian fittings), bronze, bare steel (anchor chain, worn
muzzles), the searchlights' glass and mirrors. The barrels' line of light is gloss paint on a cylinder, not metal.
A stylised sheen on painted steel stays possible per look (its materials table) but isn't in the default looks.

## Steps

**L1. The lighting, flat.** The shared lighting in one GLSL file both sprite shaders include (`Shaders/Source/Include/`,
so `compile_shaders.bat`'s `*.glsl` loop doesn't compile it alone; glslangValidator's `#include` via
`GL_GOOGLE_include_directive`). The sun in the View uniform, the quad frame in the vertex shader, linear-light
lighting with a flat normal and one default material (gloss paint, roughness ~0.4), the sRGB round trip, the
shoulder, the ambient table, the moon (its light function in Sim.Core, with tests: none below the horizon, the phase
law's half moon at about a tenth) and the shadows by the stronger light. The ship viewer and the poster go through
the same shader: the viewer's panel gets moon controls beside the sun's (direction and phase, or a date), the
poster's "lit" (a fixed sun) or "flat" (albedo as drawn: flat normal, no specular, ambient 1) is a switch in its
uniforms, not another pipeline. Done when the reference-sun screenshot matches today's within a few levels, a low
sun warms and dims the ships with the sea, a full-moon night shows the ships and their shadows faintly, a moonless
one keeps them readable, and DevRelease frame time is unchanged within noise.

*Done 2026-10-14.* As planned, with these findings:

- **The default roughness is 0.6, not 0.4.** With the flat normal, the sun high and the eye straight above, a gloss
  plane at 0.4 sits in the GGX peak over the whole ship and washes it white, inks and all (side by side at 0.4, 0.6
  and 0.7 under the zenith sun). At 0.6 it is a mild sheen under the sea's own glint. Worth revisiting at L3/L4, when
  decks go matte and the normals break the plane up.
- **Calibration holds:** with `-lights=specular:0`, ships under the zenith sun (21.9°N 60.6°W, 1916-05-31 16:00)
  match the pre-L1 captures to 3 levels at 1, 3 and 10 px/m (10 px/m bit-identical). The sun high but not overhead
  is darker than before by Lambert (56° at noon in the North Sea: about 10 levels), as intended.
- **A low sun** (7°) dims the ships more than the globe's sea, whose day colour barely follows the sun; physically the
  ships are right (a low sun puts little on a level deck, and its shadows there are faint), the sea is for the water
  pass. The warm light shows little on level decks: it waits for the relief (L2).
- **Moon:** a high full moon (1916-12-09 23:30, 56° up) lights the ships cool with faint shadows; a moonless night
  (1916-12-24) leaves them dark but readable. Jutland's own night is a day off new. A June full moon at 56°N stands
  only ~10° up and shows nothing, which is right. The display shows the moon only as the night comes (the eye's
  adaptation): its light by eye is 0.1 of the sun's, which by day would otherwise brighten everything.
- **Frame times:** DevRelease before and after (one ship at 10 px/m, 60 at 3 m/px, Jutland running) are vsync-bound
  at 120 Hz, p50 8.33 ms, CPU work unchanged: the extra ALU fits.
- **Tools:** `-lights=name:value,...` sets the table's scalar knobs for captures (roughness, specular, knee, shadow,
  moon, moonsky); the ship viewer's panel edits the whole table live, with moon bearing, elevation and phase beside
  the sun's (elevation now -20 to 90). The poster is flat by default (`ProfileScene.PosterLit`).

**L2. Relief from the height map.** The relief bake mode, plateaus only, from the existing columns for the hull and
from the turret drawing's own parts; the normal + AO texture, its mips, the upload, the binding. Block edges, funnels,
barbettes and turret roofs catch the sun; decks darken at the walls' feet. The ship viewer gets the debug views
(albedo, normal, AO, roughness, metallic, lit) next to its sun sliders: this is the bench for the rest. The ink
outlines get a look knob (`Shapes.Outline`, 0-1 on `Painter.Ln`'s opacity and width) and side-by-side captures at
0, 0.5 and 1 settle its default. Measure bake time per class and VRAM (`ShipArtLibrary.Bytes`).

*Done 2026-10-14.* As planned, with these findings:

- **Heights.** The hull's relief is the height map's columns at their exact tops (`HeightMap.Build` tags each column a
  plateau). The turrets' come from the physical model: the gun house at `Geometry.TurretHeight`, the barrels at
  `BarrelBand`'s axis (0.55 of it) plus half their width; hoods, cupolas, ears and hatches get a nominal 0.3 m over
  the roof (no hitbox gives them a height: L3's domes and slopes replace it), an open mount's platform 0.15 m.
- **Normals and AO** (`ReliefMaps`, `Bake/Relief.cs`): a [1 2 1] blur, Sobel, slopes clamped at 2.5 (68°), so a wall
  is a 2-3 px rim; AO is the horizon in 16 directions at 0.25-2 m, strength 0.85, then smoothed by a pixel (8
  directions left a star around every post). The mips average by the colour's coverage, not renormalised; the
  shader renormalises until L4's Toksvig reads the length.
- **The look:** up close, walls show a dark lee rim and a lit sun-side edge, the feet of blocks darken, vents read as
  domes. At 3 m/px it is subtle; at a 7° sun it all goes dark (as L1 found, a low sun puts little light on a level
  deck).
- **Outlines: 0.5** (`Looks.DefaultOutline`; `Shapes.Outline` per look, `-outline=` for captures). Side by side at
  0, 0.5 and 1 in the viewer (8 px/m) and on the map (10 and 3 px/m): at 1 the inks and the new dark rims double up;
  at 0 the relief carries every edge and close-ups look the most physical, but small fittings blur into the deck at
  3 m/px; 0.5 keeps silhouettes and fittings crisp at range and lets the relief speak up close. Revisit after L3,
  when the cylinders and domes add relief. The side view (the poster's profile) keeps full inks; the poster's plan
  takes the half weight and reads well. 313 cases' SVG goldens rewritten (`shipgen/golden/README.md`).
- **Memory: 326 -> 599 MB at Jutland** (53 classes, `ShipArtLibrary.Bytes`): the RGBA8 normal + AO nearly doubles
  the art, above the estimate (which assumed a smaller base). L4's material layer would add ~half again. Fine on an
  8 GB card; if it bites, BC5 for the normal and AO into the material texture (or BC4) bring it back, as planned.
- **Bake time** per class (the CLI's bake, all layers): 0.01-0.03 s before, 0.03-0.18 s now (fleet carrier the
  most), on the building threads. png-check's 70 designs: 3.0 -> 9.1 s.
- **Frame times** (DevRelease, Jutland running 3000 frames, alternating builds): equal in both of the machine's
  refresh states (8.38 ms mean at 120 Hz, CPU 0.27 ms either way); CPU p99 2.0 vs 1.4 ms from the doubled uploads
  while classes arrive (longest upload frame 6.6 ms).
- **Tools:** the viewer's "show" picks lit, albedo, normal, AO, roughness or metallic (shadows off but for lit);
  `-viewer=sun:AZ:EL,moon:AZ:EL:PHASE,shade:NAME,at:X:Y:ZOOM,mip:N` sets the viewer for captures. `shipgen bake`
  writes relief.png, normal.png (+ mips) and each turret's.
- **Fixed on the way:** `golden-update` took every fuzz case (none has a sprite golden) for a new case, drew it and
  crashed on the unbuildable; `png-check` crashed on `borodino` (no Python PNGs) and now also draws at Python's full
  ink (`outline: 1`), as does `BakeTests`.

**L3. Profiles, and the painted light taken out.** Cylinders, domes and slopes tagged in `TurretArt`, `Clutter` and
the masts and boats; the painted highlights that the normals now carry removed (the barrel highlight stroke, the 1.12
roof insets, the face plates' dark shade becoming a slope). Goldens updated. The barrels' glint shows here.

*Done 2026-10-14.* As planned, with these findings:

- **Profiles** (`Relief` in Scene.cs: `Cylinder`, `Dome`, `Slope`; `.Profile(...)`): set in the node's own
  coordinates, placed in canvas pixels by Lower at the node where they're set (`ReliefPx`), evaluated by the bake
  shader at each pixel's canvas position. Tested on the GPU through a rotation.
- **Where:** turrets: barrels, muzzles, blast bags and torpedo tubes cylinders on the axis; cupolas and round hatches
  domes (rising 0.8 of their radius, at most 0.6 m); face plates slopes falling 0.3 of the gun house's height to the
  front; a drum turret's roof, the hooded mount's hood, the champignon's mushroom roof and a casemate's shield domes.
  The hull: vent cowls, mushroom vents and searchlights domes, boats ellipsoids (both as column profiles:
  `HeightColumn.Profile`, the height map keeps the column), mast heads domes, yards cylinders a metre under the head
  (drawn in the hull's colour scene, appended to the relief pass: `DrawList.Concat`). Booms, tripod legs and cranes
  stay flat for now (lines with no height the model gives).
- **Painted light out:** the barrels' and tubes' highlight strokes, the roof insets (1.12 on armoured and DP turrets,
  1.15 on the hooded mount's hood), the face plates' 0.82 shade (now the body's colour, sloped), the faceted cupolas'
  highlight dots, the casemate shield's lighter disc, the champignon's lightened rings. 317 cases' turret SVGs
  rewritten; hulls, design records and sprite.json unchanged; png-check's turret coverage unchanged (0.9496 worst).
- **The look:** turrets read as solid at any bearing (a painted highlight turned with them); against L2's painted
  contrast they look flatter under a mid sun, the honest result. Barrels shade round; at the default 0.6 roughness
  their glint is a soft sheen, at 0.35 a clear line along each barrel and a spark on each cupola: L4's materials give
  barrels their own gloss.
- **Costs:** bake time as L2 (bismarck 0.14 s); no runtime change (only the bake's shaders and the textures' content).

**S. Shadows reworked** (2026-10-14, before L4). Light: the same passes, roughly right, nothing new per
frame on the CPU but a few numbers per turret quad.

What's wrong today (on the map and in the viewer, which draw alike):

- **The hull's shadow is drawn over the turrets.** The height-map shadow is one overlay over the canvas, drawn last.
  At a turret or barrel pixel it marches from the height *under* it (the deck, or 0 over the sea), so the hull's side
  shadows a barrel overhanging the water, and a deckhouse lower than a turret's roof shadows the turret.
- **A turret's shadow is shifted for the deck only:** by (roof - deck) / tan. Over the side (barrels trained abeam) it
  should fall a further deck / tan out; on a deckhouse, shorter or not at all. Today it's clipped where the hull
  under it stands above the roof, but never moved.
- **Shadows stack:** a turret's shadow inside the bridge's comes out darker than either.

The fix, each part a small change to what's there:

1. **Order:** hull, scorches, *the height-map shadow*, the turrets' shadows, the turrets. The overlay then darkens
   only the hull and the sea; nothing drawn after it can be wrongly shadowed by it.
2. **Turrets receive the hull's shadow in their own shader:** the same march (the height map is bound to the turret
   draws already), from the turret's ship position at its *roof* height, darkening the lit colour by the overlay's
   rule (x (1 - shade x opacity)). The bridge shadows B turret when the sun's behind it; the hull's side never shadows
   a barrel. The march is short (from the roof up to the tallest column: (max - roof) / tan) and only on the turrets'
   pixels. Barrels take the roof's height too: close enough.
3. **A turret's shadow projected per pixel, by the receiver's height:** its quad becomes a ship-aligned box over the
   turret and as far down-sun as its roof's shadow can fall (roof / tan, the sea). Per pixel: the receiver's height h
   from the height map; the ray toward the light meets the roof's height t = (roof - h) / tan along; the turret's
   alpha there is the shadow (t <= 0: the receiver stands above the roof, none). So it lands right on the deck, the
   sea and a deckhouse alike, in one height sample and one texture sample. The quad carries the turret's frame
   (corner and u edge, ship metres) in its tint, which silhouettes don't use: still 160 bytes. The texture's mip is
   picked from the turret's own uv derivatives, not the projected ones (they jump at every deck edge).
4. **No stacking with the hull's shadow:** where a turret's shadow has alpha, its shader also marches the hull's
   shadow at that pixel (hs) and adds only the difference, (max(hs, a) - hs) / (1 - hs) (by opacity), so the two come
   out as the darker of them, not their product. Turret shadows over each other still stack (rare, small).
5. **One implementation:** the march (now copied in `shipmap_shadow.frag` and `shipview_shadow.frag`) and the
   projection go into `Include/shipshadow.glsl`, which the four shaders include.

Not in it: turrets shadowing each other (a superfiring B's shadow on A's roof: the turret shadows are drawn under all
the turrets), barrels' shadows at the barrels' own height, self-shadowing within a turret (its normals and AO carry
that), shadows tilted with the hull's heel and trim. Each is a later knob if the captures ask for it.

Done when: captures at 10 and 3 px/m, sun high, low and abeam, turrets trained abeam over the side: no barrel darkened
by the hull's side, the barrels' shadows on the sea further out than on the deck and continuous across the side, the
bridge's shadow on B turret at a low sun from astern, no darker patches where shadows meet; DevRelease frame times at
a Jutland unchanged within noise.

*Done 2026-10-14.* As planned, with these findings:

- **The hull's shadow was cut short.** The march stopped at 96 steps of the zoom's finest step, so at 10 px/m no
  shadow reached past 9.6 m (the hull's side at a 24° sun should throw ~20 m, the masts ~75 m). The step now spreads
  over the whole reach (`ShipDrawList.HullMarch`, at most 256 steps), and each ray marches only where it crosses the
  height map (a slab test in `hullShadow`), stopping once fully shaded; the shadow quad covers the canvas and only its
  down-light side. Long low-sun shadows of the funnels and masts now show on the sea.
- **Turret shadows on the deck are shorter, over the side longer.** The old offset measured from the main deck
  (`Shadow.DeckM`, 4.9 m on the super-dreadnought); A turret's roof is 11.1 m over a forecastle at 8.25 m, so at 20°
  its shadow falls 7.8 m off on the deck, not 17 m, and 30 m out on the sea. That's the hitbox model's heights.
- **Seen:** at 10 px/m, sun low from abeam with the turrets trained over that side: the barrels clean, their shadows
  out on the sea past the hull's and merged with it; from astern, a turret's shadow on the deck ahead of it; the
  casemates' shadows on the sea; the viewer alike. A faint comb remains where a pointed end's shadow ends at a low sun
  (the coarser step); acceptable.
- **Frame times** (DevRelease, alternating builds, two runs each): one ship at 10 px/m with a low sun, and Jutland
  (3000 frames), equal (vsync-bound, p50 8.33 ms; CPU unchanged).
- **Left as it was:** the bake CLI's CPU preview (`Bake/Preview.cs`, preview_rest.png) still offsets the turret
  shadows from the deck; it only writes pictures to look at.

**L4. Materials.** `Style.Mat`, the looks' material tables, the material layer, Toksvig, the analytic sky. Brass and
bare steel shine, decks go matte, eras differ.

*Done 2026-10-14.* As planned, with these findings:

- **How a draw knows its material:** the colour string itself carries its palette key, `"#rrggbb/deck"`
  (`Rgb.Keyed`, made by `Painter.C` and `Painter.Or`, kept by `Painter.Shade`; `Rgb.Parse` reads the hex only). So
  the ~120 places that paint by palette key, and their shaded variants, needed no change; fills and strokes keep
  their own keys (`Style.FillMat`, `StrokeMat`: the ink outline is a stroke on a deck). A key the drawing sets
  (`Style.Mat`, inherited) goes over both: the skylights' and searchlights' glass. Unkeyed colours (dazzle, literal
  hexes) are `"paint"`. The SVGs don't carry the keys.
- **The tables** (`Data/looks.jsonc`): `default_materials` is the WWII baseline (paint 0.6; turrets 0.55, barrels 0.4;
  painted steel decks 0.8, teak 0.85, flight decks 0.9; funnel caps 0.95, soot; canvas 0.95; chain, catapult tracks
  and `steel` bare metal; propellers bronze; `brass` 0.25 metal; `glass` 0.08). `era_materials` goes over it for every
  navy whatever its `from` chain (several navies' great_war and treaty come from their own wwii, not generic's):
  Victorian gloss enamels (0.3-0.4) and varnish, Great War 0.5, treaty 0.45, Cold War non-skid decks 0.9. A look's
  own `materials` goes last (none yet). Brass is drawn only where a look has a `brass` colour (the Victorian ones: a
  capstan's drum); 44 cases' SVGs changed in just those circles.
- **The layer:** the same display list drawn again with each fill's colour (roughness, metallic, 0) at its alpha,
  through the colour pipeline, then the colour's mip chain, kept as two channels. Bake time unchanged (bismarck
  0.14 s, fleet carrier 0.18 s).
- **The shader:** roughness x the table's `roughness` knob (now a scale, 1 by default; `-lights=roughness:0.5` halves
  them all), widened by Toksvig through the von Mises-Fisher fit (alpha squared + 2 / kappa, kappa from the mip normal's
  length, half a percent let through for the 8-bit encoding's error), then GGX as before. The analytic sky: the
  reflected ray picks zenith (the sky ambient), a hazier, brighter horizon (`horizon` knob, 1.4) or the sea, blurred
  toward the hemisphere's average by roughness, times Karis's analytic environment BRDF and the AO, scaled with the
  specular (so `specular:0` still checks the calibration). The roughness debug view shows the roughness as shaded.
- **The look:** realistic first, so subtle, as intended. Decks lose their sheen; gloss parts (boats, turret roofs,
  barrels) pick up a little sky, and block rims a little more at grazing angles; at a low sun the cupolas spark and the
  barrels show a line by the breech; bare chain reads dark, brass capstans warm. At 3 and 1 px/m the frame differs
  from L3's by at most 24 levels: Toksvig and the mips keep it quiet. The Victorian barrels are near black, so their
  glint stays faint at the look's own 0.3.
- **Memory: 599 -> 736 MB at Jutland** (`ShipArtLibrary.Bytes`), the RG8 layer's half of the colour's, as estimated.
- **Frame times** (DevRelease, alternating builds): one ship at 10 px/m and Jutland, equal (vsync-bound, p50 8.33 ms);
  Jutland's CPU p99 2.1 -> 3.0 ms from the extra uploads while classes arrive.

**L5. The rest of the ship's states.** Scorches matte and sooty (roughness 1, AO down), what the sea drowns loses its
specular, the light tilted with the heel and trim (the sea plane's slopes are already in the quad), turret damage
tints in albedo before the light.

Each step: Release build, screenshots at 10, 3 and 1 px/m (`-screenshot`, sun high and low, and night), before and
after side by side, DevRelease frame times at a Jutland, the bake bench. Looks are judged on the captures, not
decided ahead.

## Decisions

1. **Realistic first** (2026-10-14): plausible materials, glossy paint, metal only where it's bare (above).
2. **The ink outlines** are settled by visual comparison at L2, through the outline knob.
3. **The poster** is configurable, lit or flat, on the same shader as the sprites.
4. **Night:** the real moon (2026-10-14), its light one function in Sim.Core the display maps by eye; an ambient set
   by eye with a readability floor. The display reads the sim's moon, never the reverse.
5. **PBR-inspired:** a plausible BRDF, lights by eye.

## Open

- **The sim's night sight and the moon:** sight is starlight only today (`SightNightM`), so a full-moon night shows
  ships on screen that the sim says can't be seen past 5 km. That's for the night-sight system to settle (reading
  the same light function); until it does, the display may look brighter than the sim's night.
- **Spare channels:** the material texture has two free if it goes RGBA8: wetness (spray, rain later) and soot
  would fit there instead of in the scorch quads. Not needed until weather.
