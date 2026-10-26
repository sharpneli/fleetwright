# Haze: the air between the eye and the sea (built 2026-10-26)

The scenario's visibility (`weather.visibility_km`) drawn in the world view: the camera looks straight down through
the haze, so the higher it is, the more the view washes out, up to what the whole column of air holds and never
past it. The table is a chart and has none.

## The model

- **The eye.** The map's camera is a pinhole straight down (`MapCamera`, 40° high): 150 m up at 0.1 m/px, 1.5 km at
  1 m/px, 5.9 km at the world boundary (4 m per reference pixel). Everything drawn in the world view (the sea, ships,
  splashes, smoke, a magazine's cloud) is at the sea's distance: the tallest effect is under 2% of the eye's height.
- **The extinction is the sim's.** `SightModel.Extinction(visibility)`, the haze the lookouts see through (a 3,000 m²
  ship made out at the visibility; fire-control.md F4d), so what the player sees and what the ships see come from
  one haze. It is about half of Koschmieder's 3.912 / V: 0.21 /km at 8 km.
- **The column** (`HazeLook.OpticalDepth`). Deep haze, the sea's boundary-layer aerosol, thins with height over 1 km
  (OPAC's maritime scale height, Hess, Koepke and Schult 1998). Past its densest (4 km visibility, softly:
  cap·(1 − e^(−σ/cap))), what more a thicker day holds is mist and fog lying low over the water, thinning over 250 m
  (a sea fog's top is commonly a few hundred metres up). Straight down from altitude h, τ = Σ σᵢ·Hᵢ·(1 − e^(−h/Hᵢ)); a
  pixel's ray slants through the level slab, τ/cos of its angle off straight down (a corner ~25% more).
- **What it does** (`shiplight.glsl`, `hazeLinear`, in linear light): the scene × e^−τ (the contrast's loss, as the
  lookouts' haze takes it), plus the slab's backscatter of the light on a level white deck (sun, moon, sky: the frame's
  own), R = γτ / (1 + γτ) (two-stream, conservative; γ 0.15, a marine aerosol's ~20 sr lidar ratio for a thin haze,
  climbing toward a fog top's albedo for a thick one), plus the sea's own light scattered on through it, 1 − R − e^−τ
  of the ambient from below. **At night both are the sea's own** (decision 5): the haze's light and the sea's mean
  fade over to the night sea as `seashade.glsl` draws it (its body by `SeaView`'s tuned night light, the zenith's 2%
  over it) as the sun goes from 12° to −2° (`SeaView.NightFromDeg`), so a haze at night only takes contrast away and the
  ships sink into the night sea's navy.

What shows of the scene, e^−τ straight down (`HazeLookTests.Table`):

| Visibility | σ /km | 0.1 m/px (150 m) | 0.5 (740 m) | 1 (1.5 km) | 2 (3 km) | 4 (5.9 km) |
|---|---|---|---|---|---|---|
| 0.5 km | 3.86 | 0.64 | 0.35 | 0.30 | 0.28 | 0.27 |
| 1 km | 1.91 | 0.80 | 0.56 | 0.49 | 0.45 | 0.44 |
| 2 km | 0.94 | 0.89 | 0.71 | 0.64 | 0.60 | 0.59 |
| 5 km (Heligoland) | 0.36 | 0.95 | 0.86 | 0.80 | 0.77 | 0.76 |
| 8 km (Åland) | 0.21 | 0.97 | 0.91 | 0.87 | 0.84 | 0.84 |
| 13 km (Tsushima) | 0.12 | 0.98 | 0.94 | 0.92 | 0.90 | 0.90 |
| 25 km (Jutland) | 0.05 | 0.99 | 0.97 | 0.96 | 0.95 | 0.95 |

So the scenarios' days are a light wash, a mist is a veil, and a silly fog goes dim grey with the ships still there:
looking down through a shallow fog sees far more than looking along it.

## How it is drawn

No pass, target or blit of its own. The frame's light block (`LightUniforms`, every world-view shader reads it) carries
three more vec4s, set once a frame by `HazeLook.Uniforms` right after the ships' build (`ShipDrawList.SetHaze`), before
any effect takes the light. Each world shader applies it at its output: `hazeCoded` on an opaque colour (the sea and
land in `globe.frag`, the ships' sprites and scorches), `hazePremul` on a premultiplied one (splash columns, stains,
torpedoes, gun smoke, the smoke field, magazine bodies and trails, and every shadow: a shadow's black becomes the
haze's own light over its share, so no shadow darkens the haze over it), `hazeThrough` on an added light (muzzle
flashes, magazine jets). The water's marks (wakes, foam, blasts) go into the wake target and are hazed with the sea.
The table's marks (icons, lines, symbols, labels) are not. The haze is dithered by half a level of 8 bits: its slant is
a few levels across the screen, and a calm sea showed it as rings.

Cost (`-gputime`, RTX 4090, 1080p, a duel at 0.8 m/px in a 2 km haze, two runs each): scene p50 2.55 / 2.57 ms off,
2.61 / 2.57 on: within the noise.

Knobs for captures: `-haze=on:0,gain:2,hazescale:1000,fogscale:250,deepest:4000,back:0.15,slant:1`
(`Map/HazeLook.cs`).

## Decisions (2026-10-26; 1-4 mine, open to the user's eye)

1. The extinction is the sim's, not Koschmieder's: the drawn haze never says more or less than the lookouts' haze.
2. Two layers by the one number: deep haze up to its densest, the rest low over the water. No switch: the split
   moves smoothly with σ.
3. The slant is the camera's own (the corners hazier). `slant:0` makes every pixel straight down if it reads as a
   lens's vignette.
4. The haze follows the world factor through the cross-fade; the table has none.
5. At night the haze is lit by the sea's night light (the user, 2026-10-26). First built with the ships' sky light, a
   thick haze greyed the night: a 2 km haze at 2 m/px lifted a moonless sea from 7 21 39 to 26 34 48 (median 8-bit
   RGB). Now it stays 7 21 39 at any thickness; dusk is between (27 36 49, was 46 51 62); day is unchanged.

## Open

- **The light inside the haze.** The sun reaching the sea is dimmed and made diffuse by the column (e^(−τ/μ₀) of it
  direct), so shadows soften and a fog's light goes flat. Not done: it changes `ShipLights`, which every tuned look
  reads.
- **Lights glowing in the haze**: a flash, a fire or a magazine lighting the air round it (a halo), not only dimmed.
- **The sea's mean by day** in the forward-scattered term is the ambient from below (`SeaDay`), somewhat brighter and
  greyer than the drawn sea (the night's is the drawn one); left so, as the day was approved.
