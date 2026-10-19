# Hits, fires, smoke and sinkings in the world view (plan agreed 2026-10-19)

The world view has its own splashes, muzzle flashes, gun smoke, wakes, torpedoes and magazine explosions. Three things
a battle shows most are still the table's marks drawn at pixel sizes over the world: **a shell hitting a ship**, **a
fire** and **the smoke from it**. A ship going down has the same problem: the sprite drowns properly, but the water
around it does nothing. **Funnel smoke**, missing altogether, is folded in, kept light (decision 6). This plan covers
the **world view** only, as `splashes.md`, `muzzle-flashes.md`, `magazine-explosions.md` and `torpedoes.md` did: what it
draws × the world's share (`WorldView.Factor`), the table's marks × (1 − it).

**Placeholders first** (the user, 2026-10-19): the plan builds shapes, sizes, timing and cost. Colours (the fire
smoke's, the burst smoke's by filler, the funnel smoke's by fuel), soot yields and the funnels' output are reasonable
placeholders, flagged in code where the estimates live (`[EST]`), and set right in a later pass when the research
requests below come back.

## The tally: what the world view still draws as a placeholder

From the code (2026-10-19). "Faded" means the table mark already gives way to a world look over the cross-fade.

| What | Drawn now (where) | Faded? | Status |
|---|---|---|---|
| **Shell hit on a ship** | The table's pop: a disc going white-hot to orange, five spark lines, a grey disc for 6 s, pixel sized, the same for every outcome (`MapScene.Shells.cs` `Hit`, called from `DrawSplashes` when `Ship >= 0`) | No | **Placeholder.** The sim has the outcome (`Terminal.Outcome`, `BurstKind`, the blast, the fire started) but the snapshot only gets a splash record with the ship's id |
| **Fire** | Up to 4 flickering orange discs with a hot core per ship, at every burning cell and part (`MapScene.Damage.cs` `Flame`) | No | **Placeholder.** It also draws fires B2 says don't show from outside (growing or starved fires inside the hull), for enemies too: a leak of what the observer can't see |
| **Fire smoke** | One grey 8-point streak per ship, 1.5 km or 40 px, downwind, meandering (`MapScene.Damage.cs` `Smoke`) | No | **Placeholder.** Mid-grey on purpose (black vanished on the chart's sea, `battle-marks.md`) |
| **Ship going down** | A pale ring widening round her until she's gone (`MapScene.Damage.cs` `GoingDown`) | No | **Placeholder** in the world. The sprite already drowns by the attitude (`ShipBatchRenderer`). `sinking-vfx-research.md` (foam, boils, slam, oil, debris) isn't built |
| **Wreck** | A circled cross where she went, with her name | No | A table mark and a label. In the world, the oil and debris would mark the place |
| Torpedo hit | The table's pop under the world's column | Yes (R5) | Done |
| Magazine | The table's flash, ring and pall | Yes (X0-X7) | Done |
| Miss splash, gun flash, torpedo track | Table marks | Yes | Done |
| Shells in flight | The table's streak and head, in both views | No | Kept as a mark: a shell in flight shows almost nothing from above. Stays (decision 7) |
| Scorched parts, damaged turrets | On the sprite (`ShipBatchRenderer` `Scorches`, `TurretTint`) | — | World look already. Whole parts only, nothing at the hit point |

**Not there at all:** **funnel smoke**, which at Jutland was the biggest thing on the sea (folded in: I6); **steam**
from boiler and engine room hits; searchlights and star shell (no sim yet); smoke screens (the smoke field). The last
three are "Later".

## Research

- `docs/visuals/shell-impacts.md` (2026-10-15): a survey of impact and fire VFX, organised by outcome (ricochet,
  shatter, overpenetration, burst inside, HE on deck, fire, flash fire). Its outcome list fits the sim's `Outcome`
  closely and is the plan's backbone. Its numbers are game-art targets (caliber curves, camera shake), not physics; we
  take the anatomy, not the numbers.
- `research/vfx/sinking-vfx-research.md` (2026-10-06): the sinking layers, with a numpy reference. Ready to build.
- `research/vfx/unified-smoke-system.md` (2026-10-08): puffs as the one smoke state, trails from moving sources,
  merging by age. Fire smoke is its first continuous source.
- `research/vfx/magazine-explosion-vfx-research.md`: the fireball's size and life by mass, already in code
  (`MagazineBlast.Fireball`, `Heskestad`). A shell's burst is the same law at a few kilograms.
- `docs/battle/fire.md` and damage/17: what a fire is in the sim (Q, phase, openings, charges, water on it).

**Gaps**, filed in `research/REQUESTS.md` (2026-10-19, "Burning warships and shell hits as seen" and "Funnel smoke as
seen"):
1. Period photos and film of burning warships, as anchors for flame size and smoke colour and density: Blücher at
   Dogger Bank, Lion and Seydlitz, Invincible, Wiesbaden, the Russian ships at Tsushima.
2. The **filler's smoke colour**: black powder (white-grey), picric acid as lyddite and shimose (yellow to black,
   noted at Tsushima), TNT (black). The sim has the filler's mass and RE (`FillerKg`, `FillerRe`); its kind waits on
   the 2026-10-16 fillers request.
3. Soot yield of a 1910s warship's fire load (cork, linoleum, wood, rubber-insulated cable, paint).
4. Funnel smoke by fuel, plant and power: colour, density, rise, how far it trailed.

The plan doesn't wait for these: placeholders, flagged in code.

## What the physics gives

| What | Estimate | From |
|---|---|---|
| A burst's fireball | D = 5.8 W^⅓ m, life 0.45 W^⅓ s, W the filler's TNT-equivalent kg. A 12" AP (~15 kg): 14 m, 1.1 s. A 12" HE (~50 kg): 21 m, 1.7 s. A 4" (~1.5 kg): 7 m, 0.5 s | `MagazineBlast.Fireball`, one implementation |
| Burst outside or on deck | The whole fireball shows, then the burst's smoke as one puff: its extinction area from the filler (as the gun smoke's A from the charge) | Muzzle-flash research §5 for the puff |
| Burst inside | Little shows at once: the flash lights the openings the blast had (the sim's E5 `BlastResult.OpenM2`, the entry hole), smoke and flame jet out of them for about the venting time. Burst after the fuze's delay (the sim's `BurstMs`) | damage/11 venting; `shell-impacts.md` "Penetration" |
| Defeated or shattered on the plate | A hard flash at the plate, about 0.1 s; a few large pieces fall into the sea close by | `shell-impacts.md` "Shatter" |
| Passed through | Two holes; a small puff at each; the shell goes on and lands beyond as a splash | The sim's exit crossing |
| Ricochet | A spark fan along the plate; the shell leaves at the glance's angle and lands as a splash | The sim's glance (P5) |
| Fragments | A burst's fragments landing in the sea round the ship as small foam marks; a share of E1's count, display only | E1 |
| Flame height | Heskestad, L = 0.235 Q^0.4 − 1.02 D (Q in kW, D the fire's width). 5 MW over 4 m: 3 m. 20 MW over 6 m: 7 m. A developed fire inside burns out of its openings: flames out of holes, hatches and ports, as Bismarck's were seen through her deck | `MagazineBlast.Heskestad`; damage/17 openings |
| Fire smoke's output | Ȧ = (Q / ΔH_c) · y_s · σ_s m²/s: ΔH_c ≈ 18 MJ/kg, soot yield y_s ≈ 0.03-0.08, σ_s ≈ 8,700 m²/kg (Mulholland). 10 MW: ~250 m²/s; 40 MW: ~1,000. The smoke research's funnel tuning is 1,500 m²/s, so a big fire is about a funnel's | Estimate; gap 3 |
| Plume rise | Bent over in the wind: Δh = 1.6 F^⅓ x^⅔ / U, F ≈ 9 Q_c m⁴/s³ (Q_c the convective MW, ~0.7 Q). 10 MW in a 7 m/s apparent wind: 40 m up 200 m downwind. Then it levels off and spreads as the puffs do | Briggs |
| Smoke colour | Cellulosic and cable fires grey-brown to near black when developed; light grey while growing; **white steam** while water is on it (`Control`, the boil share of B3's water); burning charges a sudden yellow-brown burst then white-grey | `shell-impacts.md` "Fires"; photos (gap 1) |
| Fire light at night | Radiant share ~0.3 Q, warm (~1,200 K) | Estimate |
| Funnel smoke | A line source behind each funnel, Ȧ ≈ 1,500 m²/s at full power for coal, scaled by the power share ((v / v_max)³) and the plant's k_smoke (oil ~0.3 of coal), rising some tens of metres on its heat and trailing kilometres. Grey-brown for coal, light grey for oil (placeholders) | `unified-smoke-system.md` "Calibrating funnel smoke"; gap 4 |
| Sinking | As `sinking-vfx-research.md`: crossing foam and collar, air boils (and late ones after she's gone), the closing slam, oil (Fay spreading, damping the ripples), debris | The research |

## What the sim gives today, and what it needs

- **Hits:** `Snapshot.Splashes` with `Ship >= 0`: where, when, the calibre, the shooter. The look needs, per hit:
  the outcome and burst kind; the hit point, the burst point and the exit (the point and the way) **in the ship's
  frame**; the burst's delay; the filler's W; the blast's open area (inside); whether it was under water; whether it
  started a fire. All of it is in `DamageSystem.Strike` at the moment of the hit (`TerminalResult`,
  `EffectsResult`). A **hits record** for the screens, as `ShotsView` is for the shots (I0).
- **Fires:** `ShipsView.FireCells` lists every burning cell. The look needs, per fire **that shows** (B2's
  `Fires.Shows`): where (its space, and its opening to the outside when inside), Q, the phase, charges burning, and
  the water on it. The same rule for own ships and enemies; the table keeps marking own hidden fires (decision 3).
- **Funnel smoke:** the funnels are the class's components (their plan and top), the fuel is the plant's (`Fuel`),
  the speed and the most speed are known. Nothing new from the sim; a smoke output per funnel from the designer can
  replace the power-share estimate later (`plan.md`, "needed from the designer for sighting").
- **Sinking:** the attitude over time and how she's lost (`Attitude.At`, `Loss`) are there. Oil needs the fuel
  aboard (the class has its tanks and bunkers). Nothing else.

I0 adds these. They're facts the sim has, so no result changes: the scenario benches must match bit for bit.

## What we build

| Real | Here | Kept |
|---|---|---|
| A burst's fireball | **The magazine's fireball volume at the shell's W** (`MagazineRenderer`'s analytic body, small and short), through the effects camera, its light in `FlashLights` | One fireball model |
| Burst inside: light through the openings | **A short warm light** at the burst point, and a **jet** out of the entry hole along its way in, sized by the open area (the magazine's port jets, `JetLength`, at small scale) | |
| Hard flash on a plate | **A flash quad** on `FlashRenderer`'s model (white-yellow, 0.1 s) | One flash model |
| Burst smoke, vent smoke | **`SmokeBook` puffs**, A from the filler, grey-brown (black powder white-grey; see gap 2) | The gun smoke's system |
| Exit and ricochet splashes, fragments, falling pieces | **`SplashBook.Add`** splashes where they land: a full one for a shell going on, foam-only ones for fragments and pieces | One splash model |
| Under-water hit | **The splash model's torpedo kind** against the hull, sized by W | As R5 |
| Holes at the hit point | **Decals on the sprite** in the ship's frame: a dark hole with a scorch, sized by calibre and outcome, capped per ship (old ones fold into the part scorch) | Lit like the scorches (L5) |
| A fire's flames | **A flame sprite** per showing fire at its opening or its deck, Heskestad's height up the screen through the effects camera, flickering, warm; charges burning a brighter, taller flare | |
| A fire's glow | **A fire light**, steady with flicker, in the Lights block (a pool of its own, decision 5), lighting the ship, the sea and the smoke | |
| A fire's smoke | **A trail of puffs** per fire (the smoke research's continuous source): Ȧ from Q, the Briggs rise, drift with the wind relative to a moving ship, merged by age; coloured by the phase and the water | The puffs' renderer, `SmokeRenderer` |
| Funnel smoke | **The same trails**, one per funnel, light: coarse puff spacing, early merging, a shared budget with the fires' trails (fires first when it's tight) | One trail model for every continuous source |
| Sinking | **The research's layers**: crossing foam and collar into the wake target from the hull's waterline cut, boils and the slam as foam-only splashes and a ring on the water (the blast quads' ring), an oil slick (dark, damping the glitter), debris points drifting | The sea's shading turns them into foam and calm |

## Decisions (agreed 2026-10-19)

1. **Order:** hits (I1-I3), fires and their smoke (I4-I5), funnel smoke (I6), sinking (I7). Steam is "Later".
2. **No smoke blocks sight yet**, fire or funnel, until the smoke field: the gun smoke's exception (`muzzle-flashes.md`,
   approved 2026-10-15) **extended to fire and funnel smoke**, approved by the user 2026-10-19 and logged here. The
   enemy still sees a fire by its heat (B2's `FireShowMW`).
3. **The world draws only fires that show from outside**, for every ship: nobody sees inside a hull, so a growing or
   starved fire inside shows nothing. The table keeps marking the player's own hidden fires (he knows of them); an
   enemy's hidden fires go from the table too (today's leak).
4. **Exit and ricochet splashes are display only:** the sim gives the exit point and way; the map flies the shell on
   to the water on its ballistic table and lands a splash. The spotter doesn't see it. If fire control should ever
   count it (an "over"), the sim must carry the shell on: a fire-control question (F7), not this plan's.
5. **16 lights in all:** the Lights block grows from 8 to 16, the flashes keeping their 8 and the fires a pool of 8
   (the brightest by Q over distance), so the guns and the fires don't compete. Cost measured in I8; if 16 breaks the
   budget, I8 says so and we choose.
6. **Funnel smoke folded in, light:** one trail per funnel on I5's machinery, coarse and merged early, placeholder
   output and colour. No per-funnel designer output, no smoke screens.
7. **Shells in flight keep the table's streak** in the world view: there's almost nothing to see of a real shell
   from above, and the streak is how the player reads who is firing at whom.
8. **Clocks:** hits on the effects' clock (knee 2x), never thinned (they're few and decisive); fires and smoke on the
   sea's clock, as the gun smoke.
9. **Placeholders for colours and yields**, set right in a later pass.

## Steps

- **I0, the data.** The hits record (`HitsView`) and the showing fires (Q, phase, charges, water, opening) in the
  snapshot. The table's fires read "shows" (decision 3). Benches match bit for bit.
- **I1, the burst.** The fireball outside, the light and the jet inside after the fuze's delay, the plate flash, the
  burst's smoke; the table pop fades over the cross-fade. `-hit=` knobs; a hits bench scene firing each outcome at a
  still target (as the duel bench), captured with `-screenshot`.
- **I2, what flies off.** Exit and ricochet splashes, fragment foam, falling pieces.
- **I3, the marks.** Hole decals at the hit points; the under-water hit's boil against the hull.
- **I4, the flames.** Flames at the openings and on deck, the charges' flare, the fire lights (the Lights block to
  16, decision 5); the table's glow fades over the cross-fade.
- **I5, the fire smoke.** The trail of puffs per fire, its rise, drift, merging and placeholder colours, steam while
  water is on it; the table's streak fades over the cross-fade (it stays on the table, in its grey).
- **I6, funnel smoke.** A trail per funnel on I5's machinery, by the power share and the fuel; the shared budget.
- **I7, sinking.** The research's layers; the going-down ring fades over the cross-fade; the wreck in the world is
  its oil and debris (the label stays).
- **I8, cost.** The Jutland and a 40-ship melee at 1x, 10x and 60x: frame time, puffs, lights (`WorldPerf`, F2's
  stats). Budgets as F7 and R6 did.
- **I9, by eye.** Side-by-side captures for shape, size and timing, day, dusk and night, calm and rough. Colours wait
  for the colour pass.

### Done

**I0 (2026-10-19).** `Snapshot.Hits` (`HitsView`), the damage system's ring of shell hits (shells and coarse fire's;
torpedoes have their ends, near misses their splashes) kept for `SplashLifeMs`: when, the ship and class, the shooter,
her pose at the hit, and in her frame the point and way in, the outcome and burst kind, the burst's point and its delay
after the strike (the path's run to it over the mean of the striking and bursting speeds), the exit's point and way
for a shell that passed through or glanced off and leaves above the water (the last time its path left her hull or a
part, on the leg it was on; a glance that never got in leaves at the plate) and its speed out (an estimate: 0.85 of
the striking speed through, half the plate's speed off a glance; the plates' toll isn't kept), the calibre and kind, the
filler's TNT-equivalent W (low-order's force in) and the area its burst tore to the sea or air; flags for under water,
a fire started, an open space's burst and spall. `ShipsView` gains the fires that show (`Fires.Shows`), every ship's
alike: the space, Q, the charges' MW, the phase, the water and its boil share, where the flames come out (a hole in her
side above the water, else up through the deck over the space, 1.5 m of hatches [EST]; a part's top; a weather deck's
middle) and how wide. A ship seen (the `Sighted` rows) lists only fires that show, and her parts are flagged burning only
when theirs does: the table's leak is closed (decision 3), her own side's marks unchanged. No hash moved (squadrons, 20
min: `EBA470E7A89D8BBA` before and after). Tests: `DamageTests.Each_shell_hit_records_its_outcome_burst_and_exit_for_the_screens`,
`Only_fires_that_show_are_listed_for_the_world_and_for_an_enemy_seen`.

**I1 (2026-10-19).** `Map/Hits/`: `HitBook` keeps each hit seen (placed once where she was at the hit: a burst's gas and
smoke stay in the air, she steams on from under them; back-dated on the effects' clock, never thinned), `HitEffects`
draws it each frame through the models already there, `HitLook` holds the knobs (`-hit=`). One body, one jet, one smoke:
- **a burst in the open** (outside a plate, in it, beyond her, or inside a space open enough to vent as fast as it
  formed, at `open` 0.7 of its size [EST]): the magazine's fireball body at the filler's W (`MagazineRenderer.AddBody`,
  refactored out of the magazine's loop; `MagazineBlast.Fireball`: a 15" HE's 102 kg, 27 m and 2.1 s), after the fuze's
  delay, its centre at the burst; its light into the flashes' lights by its heat (`MagazineRenderer.Light`, the
  magazine's rule, share 0.3);
- **a burst inside**: a short warm light (0.4 of its fireball's life) and its gas jetting back out of the hole it came in
  by (`MagazineRenderer.AddJet`, the ports' jets: 45 d âˆš2, d the hole's (1.2 calibres) with what the burst tore open, at
  most `reach` 0.6 of its fireball, for 0.6 of its life) [EST];
- **a plate that held** (stopped, broke up, holed in pieces, a dud, a glance): a hard flash 3 m for a 12" by the calibre,
  0.1 s, as the jet shader's round flash (the magazine's hit's flash: one flash model, rather than `FlashRenderer`'s
  muzzle parts the plan named); a shell through thin plating flashes half that at each hole;
- **smoke** on the gun smoke's book, once, dated to the burst: the burst's (A 1 mÂ²/g of W [EST]; its tint from the
  filler's RE, black powder's white-grey to the high explosives' grey-brown, placeholders), the vent's (half) at the
  hole, a hole's dust and paint (300 mÂ² for a 12").

A hit under the water shows nothing yet (I3's boil). The table's pop fades out over the cross-fade. `HitsView` gained the
filler's RE; a shell that bursts beyond her far side is recorded 5 m past it [EST] (the walk keeps no point there). The
bench: `DamageSystem.Shoot` (the hitbox viewer's shot in the world, a dev command, so it replays) and `-hitbench=
TARGET:SHOOTER[:every_s[:shot]]`, the shooter's shells at a still ship in turn, each hit's outcome printed. Seen on a
dreadnought struck by a super-dreadnought's 15" at 0.1 m/px, noon: an AP burst inside her belt a thin orange jet out of
the hole; a 15" HE on her upper works a fireball over the funnels, its soot skin and light on the water at 0.8 s; an AP
bursting beyond her bow a fireball off her far side; the smoke drifting off downwind. No hash moved. Test: `HitBookTests`.

## Later

- **The colour pass:** smoke colours by fire, filler and fuel, soot yields and the funnels' output from the research
  requests (decision 9).
- **Per-funnel smoke output** from the designer, replacing the power-share estimate; smoke screens with the smoke
  field.
- **Steam** from wrecked boiler and engine rooms and a holed funnel: white jets, from the parts' states.
- **Night lights** (searchlights, star shell) with their sim.
- **A ship broken in two** parting in halves, with I6's layers at the break (`magazine-explosions.md` "Later").
