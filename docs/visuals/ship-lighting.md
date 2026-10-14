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
- **The lighting is painted in.** `Painter.Shade` fakes it: the lighter 1.12 inset on turret roofs, the highlight
  stroke along each barrel's port side, darker sloped face plates, the dark ink outlines (`Painter.Ln`). A turret
  turning carries its painted highlight round with it, which is wrong under any real sun.
- **Lit since L1** (2026-10-14): `shipmap.frag` and `shipview_sprite.frag` shade every sprite pixel through
  `Include/shiplight.glsl` (flat normal, one material) from the lights `ShipLight.cs` computes once a frame at the
  view's centre (`LightTable`: the one place the lights are set; `ShipLights.Compute`). Before L1 it was one number,
  0.3 + 0.7 x the terminator. The shadows (`shipmap_shadow.frag`, the turret silhouettes) take the stronger light.
- **The frame is display-referred.** The sprite textures are UNORM, not sRGB, blended as they are; the blit copies
  with no tonemap. So the sea and the sprites are sRGB-coded values, and anything over 1 clips.

## The model

Per pixel of a sprite layer (the hull and each turret type):

| | Texture | Notes |
|---|---|---|
| albedo | the RGBA we have (premultiplied) | painted lighting taken out (step L3) |
| normal + AO | RGBA8: xyz biased, AO in a | sprite space: x to the bow (+x), y to starboard (+y), z up. Mips by plain averaging, **not renormalised**: the shortened normal is Toksvig's input (below) |
| material | RG8: roughness, metallic | from the look's materials (step L4) |

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
  specular is bright, without another pass.

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

**L3. Profiles, and the painted light taken out.** Cylinders, domes and slopes tagged in `TurretArt`, `Clutter` and
the masts and boats; the painted highlights that the normals now carry removed (the barrel highlight stroke, the 1.12
roof insets, the face plates' dark shade becoming a slope). Goldens updated. The barrels' glint shows here.

**L4. Materials.** `Style.Mat`, the looks' material tables, the material layer, Toksvig, the analytic sky. Brass and
bare steel shine, decks go matte, eras differ.

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
