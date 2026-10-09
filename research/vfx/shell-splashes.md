# Shell Splashes — Research & Rendering Plan
Status: final    Updated: 2026-10-08    Request: -

## TL;DR

Keep the 3/4 camera: a splash is a vertical event, and from straight above a 75 m column collapses into a white dot. Render every splash as a GPU-instanced, shader-animated record with four screen-size LOD tiers, so 5,000 live splashes cost roughly what a few hundred particles do.

- **Size:** column height scales roughly with caliber. Use about 180 × caliber (m) as the peak, so a 16-inch splash peaks near 70–80 m and a 5-inch near 20–25 m. These are design estimates, not measured data.
- **Timing:** rise ≈ fall ≈ √(2H/g). A 16-inch column peaks at about 4 s, is down by about 8 s, and leaves foam for 30–60 s.
- **Shape is driven by angle of fall:** flat trajectories at short range give leaning, ricocheting splashes; plunging fire at long range gives tall vertical columns.
- **Dye is real and is your best readability tool.** USN, IJN, French and German heavy AP shells carried colored dye so each ship could spot its own fall of shot. Tint per firing ship; it doubles as a gameplay cue.
- **Zoom continuity:** never let a splash pop. Clamp to a minimum screen size, crossfade tiers by projected pixel height, and below 2 px write foam and dye into a sea-surface stain texture instead of drawing geometry.
- **The real cost is overdraw, not count.** Render translucent columns into a half- or quarter-resolution buffer and keep mist to a few large quads.

## Anatomy of a heavy-shell splash

A heavy splash is two water features, not one: a hollow conical sheet thrown up as the shell opens a cavity, then a taller central jet as that cavity slams shut. The jet is what reaches masthead height and what spotters read. Timings below are for a 16-inch miss; scale them with √H for other calibers.

1. **Impact, 0–0.3 s.** A low white crown flares outward as a thin cone, about 15–25 m across. No flash for AP; the fuze usually does not fire on water.
2. **Sheet and jet rise, 0.3–4 s.** The cone thickens into a dense white column and the central jet punches through it. The column reads as a solid, slightly ragged pillar about 10–15 m wide.
3. **Apex and blossom, 4–5 s.** The top stalls, mushrooms into a ragged "tree" crown and turns from opaque white to grey-white spray. Dye shows strongest here.
4. **Collapse, 5–9 s.** Water falls back as a rain curtain. A low base surge of spray rolls out to roughly 2–3 column-widths.
5. **Residue, 10–60 s.** Mist drifts downwind and fades. A white foam disc, plus a dye stain if dyed, marks the impact point on the water.

Optional extra for AP with delay fuzes: a short beat after impact, an underwater burst can lift a low white dome and a second, dirtier spout. It is a nice detail for the hero tier only.

## Splash size by caliber

Use peak height H ≈ 180 × caliber in metres; a 16-inch splash then peaks near 73 m, comfortably over a destroyer's or cruiser's masthead, which matches how period photos read. I found no tabulated measurements, so treat every number here as a calibrated design estimate, and tune k by eye against reference photos.

```latex
H = H_{16}\left(\frac{d}{0.406\,\mathrm{m}}\right)^{k},\quad H_{16}\approx 73\,\mathrm{m},\quad k\in[0.75,\,1.0]
```

k = 1 is plain geometric scaling. k = 0.75 is energy scaling (shell mass grows with caliber cubed, and splash height grows with roughly the fourth root of impact energy); it makes small calibers relatively taller. Apex time is t = √(2H/g); total column life is about 2t + 1 s.

| Caliber | Typical user | Peak height (m) | Column width (m) | Time to apex (s) | Column life (s) |
| --- | --- | --- | --- | --- | --- |
| 18.1 in / 460 mm | Yamato | 83 | 12 | 4.1 | 9 |
| 16 in / 406 mm | Iowa, Nagato | 73 | 10 | 3.9 | 9 |
| 15 in / 381 mm | Bismarck, Queen Elizabeth | 69 | 10 | 3.7 | 8 |
| 14 in / 356 mm | KGV, Kongō, New York | 64 | 9 | 3.6 | 8 |
| 12 in / 305 mm | Dreadnought era | 55 | 8 | 3.3 | 8 |
| 11 in / 283 mm | Scharnhorst | 51 | 7 | 3.2 | 7 |
| 8 in / 203 mm | Heavy cruisers | 37 | 5 | 2.7 | 6 |
| 6 in / 152 mm | Light cruisers | 27 | 4 | 2.4 | 6 |
| 5 in / 127 mm | Destroyers, secondaries | 23 | 3 | 2.2 | 5 |

Width is about 25 × caliber. Foam disc radius is about 1.5 × column width and lingers 30–60 s regardless of caliber; that lingering disc is what makes a salvo readable from altitude.

## What changes the look

Angle of fall matters more than anything after caliber, and range sets it. US 16-inch AP falls at about 3° at 5,000 yd but about 45° at 35,000 yd ([NavWeaps](https://navweaps.com/Weapons/WNUS_16-45_mk6.php)), so a close-range brawl and a long-range duel should look different.

| Range (16-inch AP) | Angle of fall | Time of flight | Splash look |
| --- | --- | --- | --- |
| 5,000 yd / 4.6 km | 2.9° | 6.8 s | Low, leaning spray fan; frequent ricochet skips |
| 10,000 yd / 9.1 km | 6.8° | 14.5 s | Leaning column, about 70% height, elongated foam |
| 20,000 yd / 18.3 km | 17.9° | 32.6 s | Near-vertical column, full height |
| 30,000 yd / 27.4 km | 34.1° | 56.6 s | Vertical column, round foam disc |
| 35,000 yd / 32 km | 44.9° | 74.4 s | Tallest, cleanest pillar |

Drive three shader parameters from angle of fall: column lean (tilted toward the direction of travel), height multiplier (about 0.5 at 3° rising to 1.0 above 15°), and foam ellipse stretch along the shell's track.

- **Ricochets.** At very flat angles a shell can skip and climb again; a 1911 observer of US 12-inch practice described ricochets arcing high into the sky ([Scientific American](https://www.scientificamerican.com/article/a-landsmans-log-aboard-the-battlesh-1911-11-11)). Spawn a second, smaller splash 300–800 m downrange for a fraction of sub-8° impacts.
- **AP vs HE.** AP misses throw clean white water. HE (US "HC") bursts on contact, so it gets a brief orange flash, a shorter broader column and grey-black smoke mixed into the spray.
- **Hits.** A hit makes no water column; it is a flash, smoke and debris on the ship. Near-misses alongside should drench the deck and briefly hide the hull, which sells the drama.
- **Salvo pattern.** USS Massachusetts reported salvo dispersion of about 2 mils across and 200–300 yd along the range at Casablanca ([NavWeaps](https://navweaps.com/Weapons/WNUS_16-45_mk6.php)). Make the fall-of-shot pattern a long ellipse, roughly 5–8 times longer than wide.
- **Salvo timing.** US triple turrets used delay coils so guns fired about 60 ms apart, left, right, then centre ([NavWeaps](https://navweaps.com/Weapons/WNUS_16-45_mk6.php)). Add small time-of-flight jitter as well, so a 9-gun salvo lands as a ripple over 0.2–0.5 s rather than one frame. That ripple is most of what makes a broadside look glorious.

## Dye loads

Colored splashes are historically grounded and solve the game's biggest readability problem: which ship's shells are which. The USN introduced "splash colors" at Force Battle Practice in 1930 and most navies used them in WWII, so ships firing on one target could each spot their own fall of shot ([NavSource](https://www.navsource.org/archives/01/pdf/016292s.pdf)).

**How it worked.** Dye sat in the hollow between the AP cap and the thin windscreen. US shells used dry powder in paper bags; the 16-inch Mark 8 carried a nominal 1.5 lb bag, allowed up to 3 lb to trim underweight shells to 2,700 lb ([NavWeaps](https://navweaps.com/Weapons/WNUS_16-45_mk6.php)). British windscreens were vented so water rammed through and carried the dye out ([Naval Gazing](https://www.navalgazing.net/Shells-Part-3)). French shells reportedly used a small fuze and burster to scatter it.

| Navy | Ship | Dye color | Source |
| --- | --- | --- | --- |
| USN | Iowa | Orange | [NavSource](https://www.navsource.org/archives/01/pdf/016292s.pdf) |
| USN | New Jersey | Blue | [NavSource](https://www.navsource.org/archives/01/pdf/016292s.pdf) |
| USN | Missouri | Red | forum quote of NavSource, unverified |
| USN | Wisconsin | Green | forum quote of NavSource, unverified |
| USN | North Carolina | Green | [NavWeaps](https://navweaps.com/Weapons/WNUS_16-45_mk6.php), 1945 |
| USN | Washington | Orange | [NavWeaps](https://navweaps.com/Weapons/WNUS_16-45_mk6.php), 1945 |
| USN | South Dakota | Blue | [NavWeaps](https://navweaps.com/Weapons/WNUS_16-45_mk6.php), 1945 |
| USN | Indiana | Red | [NavWeaps](https://navweaps.com/Weapons/WNUS_16-45_mk6.php), 1945 |
| USN | Massachusetts | Green | [NavWeaps](https://navweaps.com/Weapons/WNUS_16-45_mk6.php), 1945 |
| USN | Alabama | None | [NavWeaps](https://navweaps.com/Weapons/WNUS_16-45_mk6.php), 1945 |
| IJN | Nagato | Pink | [Tides of History](https://thetidesofhistory.com/2020/09/27/japanese-16-1-45-3rd-year-type-gun/) |
| IJN | Mutsu | Black | [Tides of History](https://thetidesofhistory.com/2020/09/27/japanese-16-1-45-3rd-year-type-gun/) |
| French | Jean Bart | Orange | forum, unverified |
| French | Richelieu | Yellow | forum, unverified |

**Japan.** The IJN term was chakushokudan, "pillar coloring shell" ([NavWeaps](https://navweaps.com/Weapons/WNJAP_projectiles.php)), and the Type 1 AP shell added a dye bag ([NavWeaps](https://navweaps.com/Weapons/WNJAP_18-45_t94.php)). At Samar, Taffy 3 crews remembered brightly colored geysers from Japanese near-misses ([NHHC](https://history.navy.mil/browse-by-topic/wars-conflicts-and-operations/world-war-ii/1944/samar.html)). Secondary accounts disagree on whether Yamato's were red or undyed.

**Germany.** Evidence is thin. One British interrogation report has S-boat crews saying their pink paint blended with the pink marker dye in German shells ([Wikipedia: Mountbatten pink](https://en.wikipedia.org/wiki/Mountbatten_pink)).

**Design notes.**

- Colors repeat across a navy (North Carolina and Massachusetts were both green); they only had to differ within a group shooting at the same target. Assign dye per ship, unique within each division, from a palette of about 7 plus "none".
- Real dye did not make neon pillars. Keep the base and falling curtain mostly white, tint the upper column and crown at 30–50% toward the dye, and let the tint grow as the column thins. Mist and the foam stain carry the color longest.
- Leave a faint colored stain in the foam disc for 30–60 s. From high zoom, that stain is the dye, and it lets players read whose salvo just landed.
- Ship a colorblind-safe palette and an optional "vivid" toggle; pastel realism and gameplay legibility pull in opposite directions.

## Camera: 3/4 view vs top-down

Use both, coupled to zoom: a 3/4 view up close where height is the spectacle, easing toward top-down as the player pulls out, where the pattern on the water is what matters. A splash's on-screen height is about H × cos(pitch) ÷ (metres per pixel), so pitch decides whether a column exists at all.

| Zoom | View width on a 2560 px screen | m per px | Pitch | 16-inch column on screen | What the player reads |
| --- | --- | --- | --- | --- | --- |
| Hero / cinematic | 600 m | 0.25 | 20–35° | 250+ px | Full anatomy, dye, mist |
| Tactical | 3 km | 1.2 | 40–50° | about 40 px | Columns, ripple, colors |
| Fleet | 10 km | 4 | 55–65° | about 8 px | Thin pillars and foam |
| Strategic | 30 km+ | 12+ | 70–85° | about 1 px | Foam and dye stains only |

- **Why not top-down only.** From straight above, a 73 m column becomes a 10–15 m white blob, which is 1 pixel at strategic zoom. You lose the single most impressive thing about the effect, and the ripple of a broadside reads as flicker.
- **Why not 3/4 only.** At low pitch, tall columns hide the ships behind them, which hurts targeting. At strategic scale the line of battle is long and thin, and a steep view frames it better.
- **Occlusion rule.** Dither columns to about 40% opacity where they cover the selected ship or the cursor. Players keep the spectacle without losing their target.
- **Billboard orientation.** With pitch changing, use axis-aligned (cylindrical) billboards that rotate only around the vertical axis. Screen-facing quads look like paper cutouts when seen from above.

## Rendering architecture for thousands of splashes

Treat a splash as a 32-byte record plus a clock, never as a CPU-side particle system. The simulation writes one record at impact; every frame after that is a pure function of (now − t0) evaluated on the GPU, so 10,000 live splashes need no per-frame CPU work and replay deterministically from their seed.

**Per-splash record (32 B):** position (12), impact time t0 (4), caliber and angle of fall (2 × half), heading of travel (half), dye RGBA8 (4), seed and flags such as AP/HE or ricochet (4). A ring buffer of 16k records is 512 KB.

**Frame pipeline**

1. **Stamp.** On spawn, splat the foam disc and dye tint once into a world-space sea-stain texture (a clipmap around the camera). A global per-frame decay fades it, so stain cost is constant whatever the count.
2. **Cull and classify.** A compute pass culls records by frustum and life, computes projected column height in pixels, and appends each to one or two tier buckets with a blend weight. It writes indirect draw arguments.
3. **Draw tiers.** One instanced indirect draw per tier. Vertex shaders evaluate height, lean, width and crown spread from analytic curves; pixel shaders use a shared column texture plus world-space noise seeded per splash.
4. **Composite.** Translucent splash layers render to a half-resolution buffer and upsample with depth awareness before the ocean's foam and the HUD.

**LOD tiers by projected column height p**

| Tier | When | What draws | Cost per splash |
| --- | --- | --- | --- |
| T0 Stain | p < 2 px | Sea-stain stamp only, plus an optional 2 px glint sprite during the first 4 s so the salvo ripple still flickers across the map | Stamp once, then 0–1 point |
| T1 Pillar | 2–16 px | One cylindrical billboard with a procedural gradient column, dye tint at the top | 2 triangles |
| T2 Column | 16–96 px | Column, crown and mist billboards, a flat expanding base-surge ring, 8–16 GPU spray sprites | About 40 triangles |
| T3 Hero | p > 96 px, capped at the nearest 32–64 | Vertex-animated column mesh, soft particles, falling curtain, optional delayed underwater burst, deck wetting on nearby ships | A few hundred triangles |

**Smooth zoom, no popping**

- Blend adjacent tiers across a ±25% band of p with dithered alpha. Because p changes continuously with zoom, the blend is continuous too and needs no hysteresis.
- Apply a size floor: when the true column would be thinner than 2 px, widen it to 2 px and cut alpha by the same factor. Perceived brightness stays constant, so splashes fade out with altitude instead of vanishing.
- Keep everything in true world scale above the floor. Realism and readability then only meet at the floor, which is a single tunable number.

**Overdraw is the real budget**

- A tactical-zoom broadside can stack dozens of large translucent quads. Half-resolution compositing cuts that cost by 4×; quarter resolution suits mist.
- Skip sorting. Splashes are nearly white, so weighted blended order-independent transparency, or plain unsorted premultiplied alpha, is visually fine.
- Spend detail on silhouette, not interior: a hard-ish noisy edge with a soft core reads as water at any distance.

**Cheap lighting that still looks right**

- Cylinder normal for wrap diffuse, plus a forward-scatter term so backlit columns glow at the rim. That glow is what makes splashes look like water rather than smoke.
- Shadows: stamp an elongated blob into the stain texture along the sun direction for T1 and T2. Columns casting long shadows across the sea read strongly from a 3/4 camera.

Rough budget: 10,000 live splashes, about 300 at T2 and up to 64 at T3, should cost well under a millisecond of GPU time outside overdraw. Profile the composite first.

## Parameter sheet and next steps

Five curves driven by one normalized clock cover T1 and T2; T3 adds detail on top. With t\_a = √(2H/g) and τ = t / t\_a:

```latex
h(\tau)=H\,\alpha(\theta)\left[1-(1-\min(\tau,1))^{2}\right],\qquad \rho(\tau)=\begin{cases}1 & \tau<1\\ e^{-1.5(\tau-1)} & \tau\ge 1\end{cases}
```

- h: column top height, with α(θ) the angle-of-fall multiplier (0.5 at 3°, 1.0 above 15°).
- ρ: column opacity. It holds until apex, then thins while the crown spreads.
- Crown radius grows from 0.5 to 2 column-widths over τ = 0.8–1.6.
- Dye mix toward the ship's color rises from 0.1 at the base to 0.5 at the crown, and from 0.2 at impact to 0.6 by τ = 1.5.
- Base-surge ring radius grows linearly to 2.5 column-widths by τ = 2.5 and fades by τ = 4.

**Next steps**

- [ ] Collect 10–15 reference photos (Iowa class firing trials, Samar, Casablanca) and tune k and H₁₆ against them
- [ ] Prototype T1 and T0 first: they carry the strategic view and the 10,000-splash load
- [ ] Add the salvo ripple (60 ms firing order plus time-of-flight jitter) before polishing any single splash
- [ ] Define the dye palette: about 7 colors plus none, colorblind-checked, with a vivid toggle
- [ ] Profile overdraw at tactical zoom with two full battle lines firing
- [ ] Decide whether ricochets and delayed underwater bursts are worth T3-only implementation

## Sources

- [NavWeaps: USA 16"/45 Mark 6](https://navweaps.com/Weapons/WNUS_16-45_mk6.php) (range table, salvo dispersion, delay coils, 1945 dye colors)
- [NavSource: Splash Colors](https://www.navsource.org/archives/01/pdf/016292s.pdf) (origin of dye loads, Iowa-class colors)
- [NavWeaps: Japanese projectile terms](https://navweaps.com/Weapons/WNJAP_projectiles.php) (chakushokudan)
- [NavWeaps: Japanese 46 cm/45 Type 94](https://navweaps.com/Weapons/WNJAP_18-45_t94.php) (Type 1 shell dye bag)
- [Naval Gazing: Shells Part 3](https://www.navalgazing.net/Shells-Part-3) (British vented windscreen)
- [The Tides of History: Japanese 16.1"/45](https://thetidesofhistory.com/2020/09/27/japanese-16-1-45-3rd-year-type-gun/) (Nagato and Mutsu dyes)
- [NHHC: The Battle off Samar](https://history.navy.mil/browse-by-topic/wars-conflicts-and-operations/world-war-ii/1944/samar.html) (colored geysers)
- [Wikipedia: Mountbatten pink](https://en.wikipedia.org/wiki/Mountbatten_pink) (German pink marker dye anecdote)
- [Scientific American, 1911: A Landsman's Log](https://www.scientificamerican.com/article/a-landsmans-log-aboard-the-battlesh-1911-11-11) (ricochets)

Splash heights, widths and timings are design estimates from scaling and period photographs; no measured tables were found.
