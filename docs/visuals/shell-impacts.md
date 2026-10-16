# VFX Research — Naval Shell Impacts & Ship Fires

Oct 15, 2026

## Scope and outcome taxonomy

Every shell impact should be driven by two inputs from gameplay: **caliber class** (how big) and **outcome** (what the shell did). The VFX system picks a recipe by outcome, then scales it by caliber. That way players read both "how hard was I hit" and "what happened" at a glance, which is the main job of impact VFX in a naval game.

Camera assumption: top-down or steep isometric, realtime. Vertical effects (splash columns, smoke pillars) are seen nearly end-on, so their *footprint*, *brightness*, *shadow* and *spread on the water* do more work than their height. Any height cues should come from drop shadows, parallax-faked billboards, and slight camera tilt.

| Outcome | What happened physically | Primary read from above |
| --- | --- | --- |
| Miss (far) | Shell enters water away from ship | White ring + crown + spray ring on water, optional dye tint |
| Near miss / underwater hit | Shell enters water close to hull; may travel underwater and hit below waterline | Splash that *engulfs* the hull edge + spray falling on deck, possible delayed hull puff |
| Ricochet | Shell glances off armor at a shallow angle | Bright spark streak, grey-white puff, shell tracer leaving at a deflected angle |
| Shatter / non-pen | Shell breaks or bursts on the armor face | Short hard flash on the hull edge, dark scorch, little or no fire |
| Overpenetration | AP passes through thin structure without fuzing | Two small puffs (entry + exit), exit splash on far side of ship |
| Penetration (fuzed inside) | Shell bursts inside the hull | Delayed (tens of ms) muffled flash *through* holes and vents, smoke from hatches, debris |
| HE surface hit | HE bursts on deck or superstructure | Big immediate orange fireball, dense fragments, high fire chance |
| Critical / module hit | Turret, funnel, bridge, engine, or citadel hit | Penetration recipe + module-specific secondary (steam, oil, sparks) |
| Fire | Fuel, paint, cork, cable, ready ammunition burning | Persistent flame sprite + rising, wind-blown smoke plume |
| Magazine flash / detonation | Propellant or magazine ignites | Tall jet of flame from turret, or full detonation and mushroom cloud |

## Caliber scaling model

Scale effects by a single normalized value `k = caliber / 406 mm`, but do not scale linearly: shell mass grows roughly with the cube of caliber ([USNI](https://www.usni.org/magazines/naval-history-magazine/2015/february/armaments-innovations-navys-supershells)), so a 16-inch shell is about 15 times heavier than a 6-inch one. Visually that should feel like a different *class* of event, not just a bigger sprite.

Recommended curves (tune by eye, these are starting points, not physics):

- **Footprint radius** ∝ k^0.9 — splash ring, scorch decal, fireball radius.
- **Duration / hang time** ∝ k^0.5 — big columns stand and collapse slowly; small ones pop and vanish.
- **Particle count** ∝ k^1.5, clamped — debris, spray droplets, sparks.
- **Brightness / bloom** ∝ k^0.7 — keep small calibers readable, but stop super-heavy flashes from blowing out the screen.
- **Camera shake / screen flash** only for heavy and super-heavy classes, and only when the player's own ship is hit or very near.
- **Fire chance** is driven by shell type more than caliber: HE (thin wall, large burster) starts fires far more often than AP, which has a small burster relative to its weight.

| Class | Typical calibers | Ships | Splash (miss) feel | Hit feel | Notes |
| --- | --- | --- | --- | --- | --- |
| Light | 76–130 mm | Destroyers, secondaries | Small white spurt, gone in ~1–2 s | Quick spark/pop, small smoke puff | Fire spam source; many hits per second, so keep each cheap |
| Medium | 150–203 mm | Cruisers, battleship secondaries | Clear column, ~2–4 s | Solid flash, visible debris | Overpen and HE fire are the common outcomes |
| Heavy | 280–356 mm | Older battleships, battlecruisers | Tall column, ~4–6 s, wide foam ring | Big flash, plate debris, shake | Penetrations start to look catastrophic |
| Super-heavy | 380–460 mm | Modern battleships | Towering column, ~6–10 s, mist drifts | Huge flash, ship visibly jolts | Reserve the biggest bloom/shake budget for these |

Durations are approximate, from period film and photography rather than a measured source. Treat them as art targets.

## Misses: splashes, dye and near misses

A miss is the most frequent impact in naval combat, so it carries most of the visual budget and most of the gameplay information (where the salvo fell, whose salvo it was).

**Splash anatomy, seen from above (in order):**

1. **Entry flash** (0–50 ms): no fire for AP; a tiny white disc where the shell broke the surface. HE with an impact fuze may burst on the surface — add a small orange-grey pop.
2. **Crown and column** (0–0.5 s): a fast-rising white column. From above it reads as a bright white disc that grows quickly. Fake height with a stretched billboard leaning away from the camera, plus a soft shadow cast on the water offset by sun direction.
3. **Base surge ring** (0.2–2 s): a ring of foam and spray expands outward along the water. This is the strongest top-down read — make it the main element.
4. **Collapse** (column falls back): secondary splash ring and a white mist that drifts downwind.
5. **Foam decal** (lingers 5–20 s by caliber): a pale, broken-edged patch that fades. Salvo footprints should stay visible long enough to read the spread pattern.

**Dye splashes.** Battleships in WWII colored their splashes so spotters could tell their own fall of shot from a neighbor's. Navies did this from the 1930s, with a dye charge packed into the space between the AP cap and the windshield ([NavSource](https://www.navsource.org/archives/01/pdf/016292s.pdf)). Examples of assigned colors: Iowa orange and New Jersey blue; Nagato pink and Mutsu black ([Tides of History](https://thetidesofhistory.com/2020/09/27/japanese-16-1-45-3rd-year-type-gun/)); Jean Bart orange and Richelieu yellow; KGV-class ships yellow, green, red or no dye ([DevStrike, quoting NavWeaps](https://www.devstrike.net/topic/1818-dye-cappedsplash-shells-are-shells-that-also-include-dyes-creating-areas-of-colored-water-if-they-miss/)). Yamato is often said to have fired undyed shells.

- Use dye as a per-ship or per-player tint on the column and the foam decal. It is historically grounded and solves a real readability problem in multiplayer.
- Keep the tint subtle (30–50% saturation over white) so it reads as tinted water, not paint.

**Near misses.** A shell landing within roughly a beam width of the hull should throw its column *over* the ship: spray falls on the deck, the hull edge is briefly hidden, and a wet sheen decal appears on the near side. Near misses also shook and dished hull plating in real ships, so a small damage tick with a hull-shudder animation fits.

**Underwater ("diving") hits.** Japanese Type 91 AP shells were designed to keep travelling underwater after falling short and hit below the waterline. The only confirmed case is the hit on USS Boise at Cape Esperance ([Warfare History Network](https://warfarehistorynetwork.com/japans-diving-shells-in-the-battle-of-cape-esperance/)). VFX: a normal short splash, then after ~0.1–0.3 s a dull white boil of water against the hull (no flame visible), followed by flooding effects. This makes a clear visual distinction from a regular miss.

## Hits by outcome

The key distinction for players is **outside vs inside**: a shell that burst on the outside (ricochet, shatter, HE on armor) should look loud but shallow; a shell that burst inside should look muffled at first, then deep and lasting. Armor penetration was rarely all-or-nothing in reality ([Naval Gazing](https://navalgazing.net/Armor-Part-2)), but games collapse it into clear outcomes, and the VFX should make each one unmistakable.

### Ricochet

The shell strikes armor at a shallow angle and glances away, typically against the belt when the target is bow-on ([War on the Sea discussion](https://steamcommunity.com/app/1280780/discussions/0/5671690972172689994)).

- A bright, short spark fan sprayed *along* the hull in the direction of travel (not outward).
- Small grey-white puff, no fire, no persistent smoke.
- The shell tracer continues out at the deflected angle and lands as a miss splash beyond the ship. This second splash sells the bounce from above.
- A bright scratch decal on the hull that fades within a few seconds.

### Shatter / non-penetration

- A hard white-yellow flash right on the hull line, very short (≤100 ms).
- Dark, roughly circular scorch decal, scaled by caliber.
- A few large fragments thrown back toward the shooter's side and falling into the water as small splashes.
- No internal glow. Players should feel "that one bounced off my armor".

### Overpenetration

An AP shell punches through thin structure without fuzing and keeps going.

- Small dust/smoke puff at the entry point and a second, smaller one on the far side of the ship.
- An exit splash in the water beyond the far side, lined up with the shot direction. This is the clearest top-down tell.
- Two small dark hole decals. Little or no fire.

### Penetration (fuzed inside)

The shell bursts inside the hull after a short fuze delay.

- Entry: a small hole flash, then 30–60 ms of nothing (the fuze delay — keep it, it sells "it went in").
- Burst: a muffled orange flash that shows *through* openings — hatches, vents, the entry hole, and seams — rather than a clean fireball on top. A flicker on nearby deck surfaces via a short-lived point light.
- Debris: deck plating and fittings thrown upward; from above they read as dark flecks scattering outward and dropping into the water.
- Dark grey-brown smoke venting from the hole and deck openings for several seconds.
- Persistent result: a jagged blackened hole decal with a curled-plate edge, plus a chance of fire.

### HE surface hit

- Large, immediate orange fireball on deck or superstructure, the brightest non-catastrophic event in the game.
- Dense fan of fragments and splinters, with many small splashes around the ship.
- High chance to spawn a fire at the impact point. Splinter damage to light structure (rails, boats, AA mounts) can show as bits flying off.

### Critical and module hits

Layer a module-specific secondary on top of the penetration recipe:

| Module | Added effect (top-down) |
| --- | --- |
| Turret / barbette | Sparks, turret stops rotating, chance of propellant flash fire (see Catastrophic) |
| Funnel / uptakes | Funnel smoke turns ragged and lower; soot spills sideways from the hole |
| Boiler / engine | White steam jets from vents and funnel base; wake shrinks as speed drops |
| Fuel tanks | Black oil sheen spreading on the water downstream; high fire chance |
| Bridge / fire control | Sparks and glass glint, small fire; range-finder visibly knocked askew |
| Steering | Rudder area splash/smoke; ship visibly circles |
| Citadel | Full penetration recipe at 1.3–1.5× scale, deep red glow, heavy smoke |

## Fires

From above, a shipboard fire is mostly **smoke**, not flame. Flame is a small bright core at the source; the smoke plume is what covers the screen, drifts with the wind and tells everyone at a distance that a ship is burning. Warships burn more than their steel suggests: fittings, cable insulation and other materials all fuel fires, and a burning ship fills with toxic smoke ([Naval Gazing](https://www.navalgazing.net/Survivability-Fire)).

### Fire types and their look

| Fire type | Source | Flame | Smoke | Behavior |
| --- | --- | --- | --- | --- |
| General / superstructure | Paint, cork, wood, cables, stores after HE hit | Orange-yellow, flickering, small | Grey to dark grey, medium volume | The common case. Grows if not "repaired", spreads to nearby compartments |
| Fuel oil | Ruptured bunkers or fuel lines | Deep orange-red, lazy, rolling | Dense black, heavy, rises in a thick column | Long-lasting; can leak onto water and burn on the surface beside the hull |
| Propellant (cordite) | Charges in a turret or hoist | White-yellow, extremely bright jet, upward | Yellow-brown then white-grey, sudden burst | Seconds-long violent flare; may flash down to the magazine |
| Ready ammunition (AA, secondaries) | Hit near a gun mount | Intermittent pops and tracer-like sparks | Grey puffs | Crackling "cook-off" bursts during the fire |

Notes on period accounts:

- A wartime eyewitness of Hood's last battle described **yellow cordite smoke** from her guns and a large fire with **thick black smoke** before she blew up ([Daily Express via KBismarck](https://kbismarck.com/archives/bpressart9.html)). That pairing — yellowish propellant smoke vs black burning-oil smoke — is a good palette anchor.
- Royal Navy fire-damage reviews describe oil blown over the superstructure feeding fires, and fires fanned through rents in the hull ([IMarEST](https://library.imarest.org/record/9949/files/v10b3p02a.pdf)). Use hull holes and deck openings as spawn points for flame tongues.
- Oil burning on water is hard to put out with plain hose streams because it floats ([Naval Gazing](https://www.navalgazing.net/Survivability-Fire)). A burning slick beside a crippled ship is a strong, accurate visual.
- In Bismarck's final action, observers could see fires burning inside her through holes in the upper deck ([Padre Steve](https://padresteve.com/tag/hms-rodney/)). A late-stage burning ship should glow from *inside* openings, not just have flames on top.

### Fire lifecycle (per fire instance)

1. **Ignition** (0–1 s): a small flame core appears at the hit point; smoke starts thin and light grey.
2. **Growth** (1–5 s): flame core grows; smoke thickens and darkens toward the type's color.
3. **Steady burn**: looping flame flipbook + continuous plume. Plume leans downwind and *also* trails behind the ship according to its speed (apparent wind). From top-down this streak behind the ship is the main read.
4. **Extinguish**: flame core shrinks over 1–2 s; smoke turns light grey and white (steam from hoses) before fading. Leave a black scorch decal.

### Top-down rendering tips

- Build the plume from soft particles that grow, darken in the middle and fade. Offset each particle's position by height × wind vector to fake a rising, leaning column from above.
- Cast plume shadows on the ship and water; this gives height and scale without a tilted camera.
- Smoke must not hide gameplay-critical information. Cap opacity over the player's own ship, or fade plumes near the cursor or selected target.
- Night: flames become the main read. Add an emissive glow on the water and hull, and let large fires light nearby ships.

## Catastrophic: flash fires and magazine detonations

There are two distinct catastrophic events, and they should look different: a **propellant flash fire** (survivable, terrifying) and a **magazine detonation** (ship lost).

### Propellant flash fire (turret burn-out)

At Dogger Bank in 1915, a 13.5-inch shell from Lion pierced Seydlitz's aft barbette. The flash lit cordite in the reloading chamber, and the fire climbed to the gun house and threatened the magazine ([History of War](https://www.historyofwar.org/articles/weapons_SMS_Seydlitz.html)). It spread through compartments and knocked out both aft turrets with 165 men lost ([Model Shipwrights](https://modelshipwrights.kitmaker.net/forums/152347/index.htm)); the magazine was flooded to save the ship ([Chemistry World](https://www.chemistryworld.com/podcasts/cordite/1010201.article)).

VFX recipe:

- Delay after the penetrating hit: 0.5–2 s of building smoke from the turret (give the player a beat to dread it).
- A tall, roaring column of white-yellow flame jets straight up out of the turret roof, hatches and gun ports. From above: an intensely bright disc with radial flare and a long shadow.
- Lasts several seconds, then collapses into a heavy brown-yellow cloud and a burnt-out turret decal with drooping barrels.
- Option: chain to an adjacent turret if the game models it — historically the fire spread through connecting doors.
- If the magazine is flooded (game mechanic), finish with a white steam burst and the fire dies.

### Magazine detonation

Hood, Queen Mary and Indefatigable blew up when hits reached cordite stored in or near turrets ([Chemistry World](https://www.chemistryworld.com/podcasts/cordite/1010201.article)). Yamato's final explosion produced a mushroom cloud reported as more than four miles high and seen from over 120 miles away ([Warfare History Network, quoted](https://thefewgoodmen.com/thefgmforum/threads/japanese.391/page-10)).

VFX recipe, by phase:

1. **Flash** (0–0.2 s): full-screen-adjacent white flash centered on the magazine, with a strong point light hitting nearby ships and water.
2. **Fireball and breakup** (0.2–2 s): an orange fireball erupts; the hull splits at the magazine (swap to broken-hull meshes). Turrets and large pieces are thrown, casting moving shadows.
3. **Shock ring** (0.2–1.5 s): a fast white ring races out across the water, then a slower spray ring. This is the best top-down read for scale.
4. **Column and cap** (1–15 s): a dark smoke column rises; from above it reads as a growing, rolling disc that stays put while the wind shears its top. Debris splashes rain down over a wide radius.
5. **Aftermath**: bow and stern sections sink at different rates, burning oil spreads on the water, and a long-lasting smoke pall drifts downwind.

Scale this event mostly by the **target's** size, not the shooter's caliber — a destroyer's magazine going up is far smaller than a battleship's, whatever hit it.

## Persistent damage state and performance

Impact VFX are short; the ship's *accumulated* look tells the story of the fight. Split every hit into a transient effect (seconds) and a persistent mark (rest of the match).

**Persistent layers on the ship:**

- **Decals:** scorch rings (shatter, HE), jagged holes with curled plate (penetration), small paired holes (overpen). Cap per ship (e.g. 24–40) and merge older ones into a baked damage mask.
- **Damage mask on the hull material:** blend from clean paint → soot-streaked → burnt bare metal as total damage rises. Cheap and very readable from above.
- **Smoke emitters:** penetrations and fires leave a thin, lingering smoke trail. Rule of thumb: more damage = darker, denser, longer trail behind the moving ship.
- **Physical state:** list and trim from flooding (tilt the mesh, waterline creeps up on the low side), lower freeboard, and a reduced bow wave and wake as speed drops.
- **Lost parts:** swap or hide masts, boats, AA mounts, funnel tops when destroyed.

**Budget and LOD (realtime, many ships on screen):**

| Priority | Effect | Rule |
| --- | --- | --- |
| Always full | Miss splashes from player's salvos, hits on/by player ship, catastrophic events | Never culled |
| Reduced | Other ships' hits on screen | Half particle counts, no point lights for light/medium calibers |
| Minimal | Off-screen or far zoom | Decal + smoke trail only; skip debris and sparks |
| Pooled | Light-caliber spam (secondaries, destroyers) | Shared emitter per ship; merge hits within ~100 ms into one effect |

- Use flipbook sprites for flames and splash columns; GPU particles for spray, debris and smoke.
- Limit dynamic lights: one short-lived light per heavy/super-heavy hit, one per active fire, priority-sorted.
- Use soft particles with depth fade where smoke meets the water and the deck to avoid hard intersections.

## Implementation summary

One recipe per outcome, scaled by caliber class. Fire chance is relative within the game, not a historical figure.

| Outcome | Light (76–130) | Medium (150–203) | Heavy (280–356) | Super-heavy (380–460) | Fire chance | Persistent mark |
| --- | --- | --- | --- | --- | --- | --- |
| Miss | Small spurt | Column + ring | Tall column, wide ring | Towering column, drifting mist | — | Foam decal, dye tint |
| Near miss | Spray on deck | Hull edge hidden | Deck awash, hull shudder | Ship engulfed, jolt | — | Wet sheen |
| Ricochet | Spark tick | Spark fan + puff | Long spark streak + far splash | Same, bigger, light shake | None | Fading scratch |
| Shatter | Pop | Hard flash, scorch | Big flash, fragments | Huge flash, shake | Very low | Scorch ring |
| Overpen | Tiny puffs | Entry/exit puffs + exit splash | Same, larger exit splash | Same, debris on exit | Low | Paired holes |
| Penetration | Small muffled pop | Muffled flash, smoke vents | Deep flash, plate debris | Huge internal burst, ship jolts | Medium | Jagged hole, smoke trail |
| HE surface | Bright pop | Fireball, splinters | Large fireball | Very large fireball | High | Scorch + fire |
| Flash fire | — | Rare | Turret flame jet | Turret flame jet | Certain | Burnt-out turret |
| Detonation | Scaled by target size | | | | — | Broken hull, oil fire, pall |

### Sources

- [NavSource — Splash Colors (PDF)](https://www.navsource.org/archives/01/pdf/016292s.pdf)
- [The Tides of History — Japanese 16"/45 guns](https://thetidesofhistory.com/2020/09/27/japanese-16-1-45-3rd-year-type-gun/)
- [DevStrike — Dye-capped shells (quoting NavWeaps)](https://www.devstrike.net/topic/1818-dye-cappedsplash-shells-are-shells-that-also-include-dyes-creating-areas-of-colored-water-if-they-miss/)
- [USNI Naval History — The Navy's supershells](https://www.usni.org/magazines/naval-history-magazine/2015/february/armaments-innovations-navys-supershells)
- [Naval Gazing — Armor Part 2](https://navalgazing.net/Armor-Part-2)
- [Naval Gazing — Survivability: Fire](https://www.navalgazing.net/Survivability-Fire)
- [IMarEST — Ship fire fighting (PDF)](https://library.imarest.org/record/9949/files/v10b3p02a.pdf)
- [KBismarck — Daily Express, "I saw the Hood blow up"](https://kbismarck.com/archives/bpressart9.html)
- [History of War — SMS Seydlitz](https://www.historyofwar.org/articles/weapons_SMS_Seydlitz.html)
- [Model Shipwrights — Dogger Bank 1915](https://modelshipwrights.kitmaker.net/forums/152347/index.htm)
- [Chemistry World — Cordite](https://www.chemistryworld.com/podcasts/cordite/1010201.article)
- [Warfare History Network — Japan's diving shells](https://warfarehistorynetwork.com/japans-diving-shells-in-the-battle-of-cape-esperance/)
- [The Few Good Men forum — quoting Warfare History Network on Yamato](https://thefewgoodmen.com/thefgmforum/threads/japanese.391/page-10)
- [Padre Steve — Bismarck's last battle](https://padresteve.com/tag/hms-rodney/)
- [Steam — War on the Sea penetration discussion](https://steamcommunity.com/app/1280780/discussions/0/5671690972172689994)

Open question: real splash heights and hang times per caliber need a measured source; the durations above are art targets from period imagery.