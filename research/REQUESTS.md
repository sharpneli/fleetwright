# Research requests

Add new requests at the top of **Open**. Keep each one short: the question, why it matters, and any files to look at.
When done, the research companion moves the entry to **Done** with a link to the resulting note.

## Open

<!-- Template:
### YYYY-MM-DD - Short title
- Question:
- Context / relevant files:
- Wanted output: (comparison, recommendation, API notes, plan...)
-->

### 2026-11-07 - What sets an explosive store off: sympathetic detonation, cook-off and flame, by store
- Question: For what a warship carried, 1890-1945: propellant charges (bags, cased, brass), shells' bursters (black powder, lyddite/picric, TNT, Amatol, Explosive D, Shellite) in shell rooms and racks, torpedo warheads (wet guncotton, TNT, Type 93's), depth charges, mines, aircraft bombs. (1) **Blast and shock:** the overpressure or impulse that detonates or breaks up each when an explosion goes off beside it (sympathetic detonation, the gap and card tests' figures turned into a distance from a given charge, kg at m), and what a store does that's struck but not set off (burns, breaks, nothing). (2) **Fragments:** the speed and mass of fragment that initiates a cased charge or a shell's filler (impact sensitivity, bullet and fragment tests), and how a shell's own case shields its filler. (3) **Heat:** cook-off: how long each takes to burn or detonate in a fire or beside a burning magazine (fast and slow cook-off times; Type 93s and depth charges aboard), and the gas temperature that lights propellant in its bag or case. (4) **Does a shell room go up with its magazine?** The cases: Hood's 4 in to her 15 in, Defence, Queen Mary, Invincible, Mutsu, Arizona, Fuso, the Japanese cruisers' torpedoes (Mikuma, Suzuya, Chōkai), and shell rooms that survived a magazine's fire.
- Context / relevant files: `docs/battle/explosions.md` EX6 ("the chain": what an explosion reaches goes off, each as its own explosion through the one path; flat rolls to go where physics replaces them); `docs/battle/magazines.md`; today a shell's burst lights a magazine by `MagazineFlashChance` [EST] and hot gas lights one above 470 K [EST]; `research/simulation/damage-research/07-magazine-explosions.md`, `10-magazine-flash-chain.md`, `03-small-vessels.md` §2.5 (the cruisers' torpedoes).
- Wanted output: by store, the thresholds (overpressure or impulse at a distance, fragment speed and mass, temperature and time) with ranges and tags, and whether each goes off whole (detonation) or burns; the cases with what went off and what didn't.

### 2026-11-05 - Did rate first raise the rate of fire, and by how much?
- Question: (1) Rounds a gun a minute actually fired in battle, by ship and period, above all the BCF beside the Grand Fleet and the High Seas Fleet at Jutland and the Dogger Bank (rounds expended over minutes in action), and any practice shoot's rate with doors propped against doors worked. (2) What's in a heavy turret's battle cycle: the drill's reload against the seconds lost to flash doors, interlocks, cages and handling, and what propping doors or stacking cordite took off it. (3) What held rates down in battle, loading or fire control (waiting for the fall of shot, the layers), and how much each. (4) What the 1913 outfit and "rapid fire" drills were meant to achieve, and how historians weigh it (Lambert and his critics).
- Context / relevant files: `docs/battle/magazines.md` "The stack and Stupidity". In `duel lion derfflinger` every flash discipline fires 1.05 rounds a gun a minute: the crew's battle factor (`GunneryCrew.Load` 1.2 [EST]) and the layers' 7 s leave the reload at about 54 s against the drill hoists' 59.4 s, so hoists and stack hardly ever bind. The user's view: the ships did fire faster. Until this is answered a placeholder stands in: `FlashDisciplines`' `LoadShare`, rate first 1/1.2 [GUESS]; safe left at 1, nearly free. Does safe want a cost too?
- Wanted output: ranges by type and period with tags: what share of the battle reload a discipline can buy, or why none. If the gain was in fire control, not the loading, say so.

### 2026-11-05 - Magazines alight: what actually happened, 1890-1925 first
- Question: (1) Every case found of a ship's magazine alight, in battle or by accident (from memory, to check: Natal, Vanguard, Bulwark, Mikasa 1905, Tsukuba, Kawachi, Iéna, Liberté, Benedetto Brin, Leonardo da Vinci, Imperatritsa Mariya; Indefatigable, Queen Mary, Invincible, Defence, Black Prince, Good Hope, Borodino; Pommern, Pathfinder, Pallada, Goliath, Frauenlob; Petropavlovsk, Hatsuse): the propellant, the packing and whether lids were on, what lit it, and whether she blew up, burned out or was saved. Above all: is there any case of a dry cordite or early single-base magazine alight, before about 1920, that burned out with the ship afloat? (2) Per heavy hit: how many hits each navy's ships of each type took for each magazine lost, by type (dreadnought, armoured cruiser, pre-dreadnought), so "drill" has a rate to meet beside the BCF's ~5%. (3) How the flame reached the armoured cruisers' magazines: Defence's and Black Prince's passages, how much lay in them, and how common such passages were by type and period. (4) Did a shut flash door ever stop a flame before 1916, and how often did a flame run the whole train from a lit gunhouse?
- Context / relevant files: `docs/battle/magazines.md` "Too safe" (on drill a Great War ship can't be lost to a magazine; `MagazinePackageR` Clarkson 4 is what decides it, and `10-magazine-flash-chain.md` §6.2 and §8 call it the most uncertain constant), `MagazineRace.cs`.
- Wanted output: a table of cases with tags, R's range for each packing and period, per-hit rates by type. Ranges by type and period, not replicas.

### 2026-11-04 - What crews did about holes too big to plug, 1890-1925 first
- Question: (1) Collision mats by ship size: men, time and how far they reduced the leak, on destroyers and cruisers as well as capital ships; were they carried and used on destroyers at all? (2) What a crew did about a hole of 1-5 m2 and over, out of reach of a plug or patch: shoring and stuffing from inside, giving up the compartment and holding its boundary, hours afterwards with no enemy near. (3) How long damaged destroyers and cruisers stayed afloat and whether they got home, and how often a consort towed one or took off her crew (Jutland's Onslow, Acasta, Broke, Warrior, Sparrowhawk). (4) Lost and survived, the speeds they crept home at.
- Context / relevant files: `docs/battle/loss-stock-take.md` "The slow deaths" (`MatGirthM` 46 m and `EaseDownKn` 12 kn are [EST]), `docs/battle/09_damage_control.md` line 129, `src/Fleetwright.Sim.Combat/Repairs.cs`. Not blocking.
- Wanted output: ranges by ship size and period, the cases with hours and water where known.

### 2026-11-01 - Fire distribution and cripples, 1890-1925 first
- Question: what the fleets' doctrine said about whom the guns fire on, and what they did. (1) Fire distribution: the
  rules each navy gave its lines (the Grand Fleet Battle Orders' ship for ship from the van or the rear, concentrating,
  leaving a ship unfired on; the High Seas Fleet's; the Japanese at Tsushima), and how a ship chose when its
  distributed target wasn't seen. (2) Disabled enemy ships: whether battle orders left them to the light forces and
  their torpedoes, and the cases that went otherwise (Blücher at the Dogger Bank, Wiesbaden and Defence at Jutland,
  Suvorov at Tsushima, Monmouth at Coronel, Scharnhorst's and Gneisenau's ends at the Falklands): who fired on the
  cripple, how much, and what it cost. (3) Light forces' and cruisers' own rules for finishing cripples.
- Context / relevant files: `docs/battle/balance.md` "BL5 again: the blood inside 4 km is the cripples" (the
  proposed doctrine field `Targets`: fight or tonnage), `src/Fleetwright.Sim.Ai/Captains/CaptainLayer.Fire.cs`
  (`Matters`, `Threat`, the target score). Not blocking: the doctrine field is the user's choice either way.
- Wanted output: the rules by navy with sources, the cases with the fire on the cripple, ranges by type.

### 2026-10-25 - Which shell at which target, 1890-1925
- Question: how navies chose the shell by target, as ranges by navy and period. (1) The rules each navy gave its
  gunnery officers (AP, common/CPC, HE; by target type, range, armour), and how far the practice followed them
  (Yalu, Santiago, the Yellow Sea, Tsushima, the Falklands, the Dogger Bank, Jutland; Samar as a later check). (2) The
  outfits actually carried by kind against what was fired by kind, where counts survive. (3) How long a change of kind
  took at the gun: the rounds already in the gun, the cage and the working chamber, and whether guns were ever
  unloaded. (4) What heavy HE and common did to capital ships' fighting power against AP (Tsushima's Shimose,
  Jutland's British CPC), beyond sinking: fire control, guns and crews silenced.
- Context / relevant files: `docs/battle/shell-choice.md` (the plan: A0's bench, open questions 1, 2 and 4),
  `research/simulation/damage-research/08-gunfire-effects.md` §1-2, `src/Fleetwright.Sim.Ships/Rows.cs`
  (`MagazineOrder`). Not blocking.
- Wanted output: tables by navy and period, the switching time with sources, cases of HE against armour.

### 2026-10-21 - Plume meander over the sea: the large eddies
- Question: how far and how fast a smoke plume meanders over the open sea from eddies larger than the plume itself
  (boundary-layer and mesoscale, ~1-15 km), against its own (relative) spread. (1) Instantaneous against time-averaged
  plume width at 1-30 km downwind (Gifford's meandering-plume model, over-water dispersion trials, offshore stack or
  ship-plume studies, satellite ship tracks); (2) the meander's displacement and speed by wind and stability; (3) the
  eddies' wavelengths and lifetimes over the sea; (4) photographs or accounts of trails at sea, 1890-1925 (straight,
  snaking, broken into clumps?).
- Context / relevant files: `docs/battle/smoke.md` Z5c (the large eddies, decision 10: the sim's, so sight reads the
  gaps), `SmokeEddies` in `src/Fleetwright.Sim.Combat/SmokeModel.cs` [EST: eight waves 1.5-12 km, strain 0.1 a mode,
  turning over at 2 m/s: ~20 m off in a minute, ~430 m in an hour in 8 m/s neutral air]. Not blocking.
- Wanted output: ranges by wind and stability for the meander's size, speed and scales, with sources.

### 2026-10-20 - Smoke in the record, 1890-1925
- Question: smoke as numbers the sim's smoke field can be checked against. (1) Funnel smoke by plant and fuel (coal
  under natural and forced draught, oil, mixed firing): how dark, how high it rose, how long and wide trails lay in a
  given wind, and how much a ship made at cruising against full power. (2) How far smoke was sighted before the ships
  under it, by period, fuel and weather (Jutland, Dogger Bank, the Falklands, Tsushima, anything with ranges and
  bearings). (3) Smoke screens: how fast destroyers laid funnel smoke and (later) chemical smoke, the screen's length,
  height and how long it held by wind; the cases at Jutland. (4) Black and brown powder gun smoke at the Yalu and
  Santiago: how long it hung over a firing ship and what it did to her gunnery.
- Context / relevant files: `docs/battle/smoke.md` (the plan, a feasibility study; its yardstick), 
  `research/vfx/unified-smoke-system.md` (the puff model and its INFERRED funnel calibration, Ȧ = 1,500 m²/s),
  `research/vfx/muzzle-flash-smoke-research.md` §5, `research/simulation/powerplant-model.md` (k_smoke). Not blocking.
- Wanted output: tables of sightings and conditions, plume and screen sizes and lifetimes, gun smoke cases, with sources.

### 2026-10-20 - Flooding and damage control in the record, 1890-1925
- Question: how ships of the first game's period took on water after hits, and what their crews did about it, as
  numbers. (1) Cases with times, tonnages, list and speed: Seydlitz and Lützow at Jutland (Lützow's hour by hour if
  known), Marlborough's torpedo, Audacious (1914), Oslyabya, Borodino and Alexander III at Tsushima, the 1915
  pre-dreadnoughts torpedoed or mined (Majestic, Triumph, Formidable, Irresistible, Ocean, Bouvet, Goliath), Retvizan
  and Tsesarevich at Port Arthur, and any destroyer cases. (2) Who counterflooded and how: Makarov's and Krylov's
  tables, the German navy's system, the RN at Jutland; the rate and the tanks used. (3) Portable and fixed pumps by navy
  and year (hand, steam, petrol), collision mats and shot plugs in use, repair-party organisation and size before the
  USN's 1940s system. (4) Watertight doors and closure discipline in action, and how often doors left open mattered.
- Context / relevant files: `docs/battle/damage-control.md` (the plan; its yardstick's rows marked (?) wait on this),
  `docs/battle/09_damage_control.md` and `10_damage_control_holistic.md` (mostly WWII anchors),
  `docs/battle/damage/17_structural_leakage.md`. Not blocking: the plan builds on 09/10/17's mechanism checks first.
- Wanted output: a table of cases (hits, water by time, list, speed, outcome, what the crew did), counterflood practice
  and rates by navy, pumps and party sizes by navy and year, with sources.

### 2026-10-19 - Burning warships and shell hits as seen, 1904-1925
- Question: what a shell hit and a shipboard fire looked like from outside, as numbers and colours the world view can
  take. (1) Photos and film of burning warships as anchors for flame height, smoke colour and density, and how far a
  fire's smoke trailed: Blücher at Dogger Bank, Lion and Seydlitz, Invincible and Wiesbaden at Jutland, the Russian
  ships at Tsushima, anything else of the period. (2) The burst's smoke colour by filler: black powder, wet guncotton,
  the picric fillers (lyddite, melinite, Shimose: the yellow-to-black smoke noted at Tsushima), TNT; and how a hit
  looked by what it did (burst on the side or deck, burst inside, holed and through, glanced off). (3) The soot yield
  of a 1910s warship's fire load (cork, linoleum, wood, rubber-insulated cable, paint, stores) and how a fire's smoke
  changed colour as it grew, burned out, or had water put on it (steam).
- Context / relevant files: `docs/visuals/hits-and-fires.md` (the plan; its "What the physics gives" table has the
  estimates these replace), `docs/visuals/shell-impacts.md` (the game-art survey), `docs/battle/fire.md`, the 2026-10-16
  request "Explosive fillers as parameters" (the filler's smoke would sit beside its other numbers). Not blocking: the
  plan builds with flagged placeholder colours and a later pass sets them.
- Wanted output: a table of flame and smoke appearance by fire kind and phase, burst smoke by filler, soot yields with
  sources, and a short list of the best photos with what each shows.

### 2026-10-19 - Funnel smoke as seen, coal and oil, 1890-1925
- Question: how much smoke a funnel made and what it looked like from above: its colour and density for coal (by
  grade, and when stoking or forcing the boilers) and for oil (clean burning against making smoke on purpose), how it
  changed with speed and power, how high the plume rose and how far it trailed before thinning, and how it bent and
  spread in the wind. Photos of squadrons under way with their smoke, Jutland's above all.
- Context / relevant files: `docs/visuals/hits-and-fires.md` (funnel smoke folded in, kept light),
  `research/vfx/unified-smoke-system.md` ("Calibrating funnel smoke": Ȧ = 1500 m²/s tuning, the k_smoke factors by
  plant), `src/Fleetwright.Shipgen` `Powerplant.SmokeReach`. Not blocking; the smoke field's sight lines will want the
  same numbers later.
- Wanted output: smoke output and appearance by fuel, plant and power share, plume rise and trail length, with sources
  and photos.

### 2026-10-18 - Fire discipline by navy, and battle rates of fire from their causes, 1898-1925
- Question: how each navy ranged and fired for effect, as numbers a general model can take (the game keeps it as a
  side's doctrine: salvos in flight while ranging, a ladder's step and size, the guns in a ranging salvo, alternating
  halves or full salvos in fire for effect, when they went back to ranging). The RN's 1914 and 1917 rules and the German
  ladder are in `computing.md` §2.5; wanted are the IJN's (1904-05 and the Great War), the Russians', the French, the
  Italians' and the Austrians', and the USN's before 1920 (the 1912-18 battle practice: ladder or single salvos?).
  Second, the battle rate of fire as causes rather than one factor: how many ready-use rounds a mount held and how fast
  the hoists fed after them, by mount type and period (the Majestics' 70 then 100 s is one case), the director's or
  layer's cycle between salvos, and the crew's fatigue and heat over an hour; the research has battle rates at 30-60% of
  drill but not what made them.
- Context / relevant files: `docs/battle/fire-control.md` F6 (the cut: F6b the disciplines, F6d the rate from the
  parts), `src/Fleetwright.Sim.Core/SimSettings.cs` (`FireDisciplines`: rn-1914, rn-1917, german, usn; the Japanese and
  Russians take the RN's for want of their own), `research/simulation/fire-control-research/computing.md` §2.5,
  `predreadnought-tech.md` §3, `manual-gunnery-research.md` §4. Not blocking.
- Wanted output: a table by navy and period of the ranging and fire-for-effect rules with their numbers, and the rate of
  fire's parts (ready-use rounds, hoist rates, cycle times, fatigue) by mount type and period, with sources.

### 2026-10-18 - Fire-control machines as mechanisms and typical fits, 1898-1925
- Question: what kinds of fire-control machine existed and what could each do, as numbers a general model can take
  (the game builds ships from mechanisms with historical inspiration, not 1:1 replicas of classes): the rate clocks and
  plots (range and bearing rate limits, how own ship's turns were fed, the Dreyer marks' steps in capability, Pollen's
  Argo against them), the German clocks, plots and Richtungsweiser (did turrets lay by director or their own sights),
  the USN's Ford Mk 1 rangekeeper, the French, Italian and Austrian tables of 1910-18, and the stadiametric sets (the
  Russians' Liuzhol: error and range). Then the typical fit by ship type and period (how many rangefinders, what base,
  where they stood, whether director firing), as ranges for the designer's defaults rather than a per-class inventory.
- Context / relevant files: `src/Fleetwright.Shipgen/FireControlTemplates.cs` (the catalogue; blocks marked [EST]
  where no source gave numbers), `docs/shipgen/fire-control-templates.md`, `docs/battle/fire-control.md` F3 ("Real
  fits"), the designs under `Content/Scenarios/Designs/` (the Jutland and Tsushima ships) and `shipgen/designs/`.
  `research/simulation/fire-control-research/computing.md` §3 has what's known so far. Not blocking.
- Wanted output: a table of machines with their numbers, and typical fits by type and period as ranges, with
  sources.

### 2026-10-16 - Shell bursts in confined spaces aboard ship
- Question: how much harder does a shell's burst hit inside a closed compartment than on an open deck, and how should a
  model scale it? The quasi-static pressure from a confined burst against the room's volume and its vents (doors,
  hatches, ducts, the hole the shell made), what it does to bulkheads, decks and doors (blown in, bulged, torn from
  their frames), how far it reaches into neighbouring spaces, and its effect on the people inside. War-damage cases with
  the charge, the room's size and what failed are the best anchors.
- Context / relevant files: `src/Fleetwright.Sim.Combat/Effects.cs` (bursts sized only by the charge: wreck radius
  1.6·W^⅓ m, blast 3.5·W^⅓ m, a bulkhead breached when the wreck radius passes 0.4 of the room's length, the same in a
  small room as on a deck), `research/simulation/damage-research/08-gunfire-effects.md` §3.6 (the radii, fitted to 2-3
  holes), `07-magazine-explosions.md` §2 (the venting balance, for propellant), `docs/battle/shells-armour-damage.md`
  "Open questions". Not blocking: the plan's steps go ahead on today's radii.
- Wanted output: a rule for a burst's reach and damage by charge, room volume and venting, with the cases it's fitted to.

### 2026-10-16 - Explosive fillers as parameters, 1860-1970
- Question: which bursting charges did shells (and torpedoes and bombs, briefly) carry, and what numbers describe each
  for a game: TNT equivalence for blast, detonation velocity or brisance (how it breaks the shell body into fragments),
  incendiary effect, sensitivity to the shock of striking armour (bursting in the plate), how often it went low-order
  or failed and what booster it needed, stability in storage, and when and by whom it was used. Black powder, wet
  guncotton, picric fillers (lyddite, melinite, Shimose), TNT, Explosive D, Shellite, TNA, trotyl/hexanite and the
  later RDX mixes and Torpex, and any others in naval service.
- Context / relevant files: `research/simulation/armor/04-shells-fuzes.md` §2 (a first table: RE, detonation velocity,
  sensitivity, AP suitability; several REs not found), `Content/Sim/settings.json` `FillerTnt` (1 for every explosive
  today), `docs/battle/shells-armour-damage.md` (the shell block's `filler`, and "Open questions"),
  `src/Fleetwright.Sim.Combat/Effects.cs`.
- Wanted output: a parameter table per explosive with sources, and what each parameter should drive in the model
  (blast, fragments, fire, in-plate bursts, low-order rates).

### 2026-10-15 - Torpedo tracks, launches and hits as seen, 1890-1945
- Question: what a torpedo looked like from a ship's bridge or aloft, and from above. The bubble track: how far behind
  the torpedo it surfaced (by depth and speed), how wide it was and how it spread, how long it stayed visible, how it
  read in sun, overcast, a seaway and at night (phosphorescence?), cold-air against wet-heater against steam, and how
  little oxygen and electric ones left. Whether the track was also a slick (smoother water darkening the glint). The
  launch: a deck mount's impulse charge (smoke, flash), the drop and splash, the dive and recovery; a submerged tube's
  air. The hit: the column's height and width by charge (100-300 kg), its timing, its colour (water, smoke), what
  shows at night. Period photos with scale (Pearl Harbor, Jutland, the Dardanelles, the Adriatic) are the best anchors.
- Context / relevant files: `docs/visuals/torpedoes.md` (the plan, whose numbers are physics estimates: bubbles rising
  at 0.25-0.8 m/s from 3-5 m, a track 2 m wide spreading to ~7 m over 90 s, a column of 12 W^⅓ m),
  `docs/visuals/battle-marks.md` (the table's torpedo marks), `src/Fleetwright.Sim/TorpedoSystem.cs`. Not blocking:
  the plan builds on its estimates and tunes by eye; this replaces them.
- Wanted output: the numbers with sources and their spread, against the plan's estimates; what the look should show
  that the plan leaves out.

### 2026-10-14 - When ships fell out and fleets broke off, 1890-1925
- Question: what damage made a captain leave the line (speed lost, guns out, list, flooding, fire), and what losses
  or circumstances made an admiral break off an action, across the era's actions (Yalu, Santiago, Yellow Sea,
  Tsushima, Coronel, Falklands, Dogger Bank, Jutland, and smaller ones)? Were there written rules, or only practice?
- Context / relevant files: `docs/battle/command.md` "Doctrine" (the fall out and break off thresholds, now plain
  guesses: 60% speed, a third of the guns, 10° list, half the reserve; a third of the force lost), step 5.1a and 5.1d.
  Not needed for the first version of step 5, which uses basic numbers; this is for the fine-tuning pass.
- Wanted output: cases with what each ship or fleet had suffered when it fell out or broke off, and suggested
  thresholds by navy and period, with sources.

### 2026-10-14 - Fleet tactics and doctrine by navy, 1890-1925
- Question: how did the era's fleets cruise, scout, deploy and fight, navy by navy? The Grand Fleet Battle Orders
  (cruising order, deployment from columns into line, the turn away from torpedo attack), the German battle
  instructions (the battle turn-away, *Gefechtskehrtwendung*), the Japanese at the Yellow Sea and Tsushima, the
  Russian and French practice; flotilla attack doctrine (launch ranges, approach, turning away); cruiser scouting lines
  (spacing, signalling, falling back on the main body). And timings: how long a deployment, a turn together and a
  battle turn-away took (Jellicoe's deployment of 31 May 1916, Scheer's turn-aways).
- Context / relevant files: `docs/battle/command.md` steps 5.1b (squadron tactics, the flotilla's attack), 5.1c (the
  force: approach, deployment), 5.1d (the commander) and "Doctrine" (the fields a navy's character would set);
  `research/simulation/evasion-research.md` covers evasion under fire and torpedo attack ranges already. Not needed for
  the first version of step 5; this is for the fine-tuning pass (navy doctrine presets by year).
- Wanted output: per navy and period, the doctrine as rules an AI can follow (when to engage, at what range, how to
  deploy, how to meet torpedo attack), with timings, and what the historical actions show of it in practice.

### 2026-10-14 - Signals in the fleet, 1890-1925
- Question: how did orders travel in a fleet of the era, and how long did they take? Flag hoists (hoist, read,
  repeat down the line, execute), searchlight and lamp signalling by night, early wireless (coding, sending, delays,
  failures, jamming). How often were signals missed or misread, and with what effect: Jutland's 5th Battle Squadron
  turn, Beatty's signals at Dogger Bank and Jutland, others. What happened when a flagship was lost or her signalling
  wrecked (Tsushima's *Knyaz Suvorov*, Yalu), and how long before command passed?
- Context / relevant files: `docs/battle/command.md` "Signals: orders take time" and step 5.1e (delays, chances of a
  missed signal, command passing); `docs/battle/plan.md` "The first cut's era" ("Signals, not radio orders"). Not
  needed for the first version of step 5, which uses basic delays; this is for the fine-tuning pass.
- Wanted output: typical delays and failure rates per means of signalling and conditions (day, night, smoke, range),
  the passing of command, and the historical cases, with sources.

### 2026-10-13 - The Jutland's classes as built
- Question: for each class present at Jutland (both fleets), its displacement, speed, main battery and its layout,
  and belt, as built or as refitted by May 1916, for a ship design session. Split from the Jutland 14:00 request,
  whose positions part is done.
- Context / relevant files: `tools/scenarios/jutland-classes.json` (the stand-in classes), `Content/Scenarios/jutland.json`
  (the order of battle), [jutland-1400-dispositions.md](jutland-1400-dispositions.md).
- Wanted output: a table of the classes present (~55), with sources.

### 2026-10-12 - How many ships were at sea: navies and merchant fleets
- Question: how many seagoing ships did the world have, by nation, in 1890, 1905, 1914, 1918 and 1925 (the first
  campaign's era)? Warships by type
  (capital ships, cruisers, destroyers and torpedo boats, escorts, submarines) and merchants (steamers and sailing
  ships over ~500 GRT, and how many were at sea on a typical day rather than in port). And the largest single actions
  by ships engaged and by classes present (Jutland's 250 ships: how many classes?).
- Context / relevant files: `docs/battle/plan.md` "How many ships" (load and tick budgets), `docs/world/architecture.md`
  "Many small fights" (merchants as light tracks; it assumes ~9,000, Britain's 1914 figure).
- Wanted output: a table per year and nation, with sources, and the few largest actions with ships and classes.

### 2026-10-11 - Historical search and interception operations as mission seeds
- Question: which naval operations 1890-1950 make good "find it and fight it" missions, with fixed forces in a
  limited area? For each: the forces on each side (ships, groups, where they started), the area and duration, what
  each side knew at the start and what reports came in when (sightings, shadowing, aircraft, D/F, codebreaking), the
  weather and light, the hunted side's plan, and how it ended. Examples: the Bismarck chase, Graf Spee and the River
  Plate, Emden, Goeben and Breslau, Scharnhorst at North Cape, Coronel and the Falklands, Dogger Bank, Operation
  Berlin, Kormoran and Sydney.
- Context / relevant files: `docs/missions/plan.md` (the mission format: forces, area, starting pictures, scripted
  reports, objectives), `docs/world/architecture.md` (pictures, encounters, time compression).
- Wanted output: a catalogue (one section per operation, the fields above), and which mission templates they suggest
  (hunt, intercept, breakout, convoy, fleet action) with the parameters a generator would vary.

### 2026-10-10 - Tumblehome proportions
- Question: for real tumblehome ships (Jauréguiberry, Charles Martel, Bouvet, Charlemagne, Tsesarevich, Borodino),
  what were the maximum beam, the beam at the upper/main deck and at the forecastle, the height of the maximum beam
  over the waterline, and the shape of the curve (straight slope or curved)? Did it run the full length or fade at
  the ends? Anything on their GZ curves or range of stability?
- Context / relevant files: `docs/shipgen/tumblehome-plan.md` (the `Tumblehome(strength, knuckle, extent)` term and
  its 0.72 deck/beam guess), `docs/shipgen/hull-form-variations.md`.
- Wanted output: a table of the ratios per ship, and a recommended range for `strength` and `knuckle`.

## Done

### 2026-11-07 - Torpedo protection walls: how much they held, and how far they bent
- Done 2026-11-07 (draft): [simulation/damage-research/14-torpedo-protection-walls.md](simulation/damage-research/14-torpedo-protection-walls.md) with
  [tds_walls_ref.py](simulation/damage-research/tds_walls_ref.py). **The walls aren't weaker in the code than in the record; the load looks
  too high.** With real inputs (flow 345-680 MPa by grade, span = the wall's height between supports 5-9 m, riveted seams opening at
  w/a ~0.10) a holding bulkhead holds 1.1-3.6 MPa, K 0.7-1.4 (continuous welded 1.1-2.1); `TdsMembraneK` 2.2 is at the top. The static
  load that leaves the measured sets at design-level hits is 0.7-1.5 MPa (Job 74's 1,000 lb 18 in, Caisson H 22 in, Nevada, Tirpitz), so
  walls at 2.2 giving in half the hits point at the gas held as pressure. Trials: Job 74 (ADM 324/4: #4 2 x 35 lb D.1 held at 18 in with
  oil behind broken by its pulse; #5 60 lb D.1 torn where welded, a transverse bulkhead 4 ft off tight), Monarch 1923 (2,084 lb at 8 ft,
  bulkhead "completely intact"), Nelson's 1,000 lb, Tosa 1924 (5 shots, 726-1,203 lt), Caissons F and H, Pugliese. Bend: weeps at w/a 0.03,
  tight to 0.06, seams open or edges loose at 0.10, plate torn at 0.25 (Saratoga). Compartments 5-7 m (Ramillies 18-20 ft, PoW 24 ft, BB55
  ~4.7 m inferred), damage length 1.43 W^1/3 destroyed and 2.6 W^1/3 flooded (Job 74). Tonnes a hit: held 250-1,066 (median ~540), leaked
  ~970, breached 740-3,000 (median ~1,200). Liquid against the holding bulkhead passed it through: Saratoga 1942, Job 74 #4, the US 1/16
  tests, Leipzig; PoW f.109 the one counter-case. Not reached: Brown's pages, the caisson reports, NC's inner void, US tank plans, Sprengziel.

### 2026-11-05 - Big explosions in and beside ships: gas, the girder and bombs
- Done 2026-11-07 (draft): [simulation/damage-research/13-big-explosions.md](simulation/damage-research/13-big-explosions.md) with
  [big_explosions_ref.py](simulation/damage-research/big_explosions_ref.py). **Pressure stayed local, gas didn't:** in every wreck and
  trial found, what failed lay within one or two spaces (20-60 m) of the seat (Arizona, Defence, Invincible, Indefatigable, Mutsu, Hood),
  while flame and low pressure went 30-100 m by passages and vents (Baden 1921: a 15 in CPC smashed a bulkhead 100 ft off). The wave
  crosses 200 m in 0.25-0.6 s but the gas fills beyond only in V/(Cd A c*), 0.1-80 s; a fast release stays local (the far side 0-7%),
  a burn of seconds through a wide tear equalises: the network, not a merge. **A belt is a heavy door:** held by its riveted edges at
  about twice its backing's membrane value (2.7-4.4 MPa, EST), its 2-3 t/m2 a wall to a shell's pulse for 20-70 ms, opening a whole
  strake when it goes (Hood's side gone to the fuel tanks' inner wall, curled out; Arizona's belt sent it up; Baden's plate displaced).
  **Destroyers:** the USN's 6 structural losses of 30 were all bombs or kamikazes on the upper flange or her own warheads (Brownson,
  Bush, Abele, Pringle, Little, Colhoun), 1 min to hours; "no instance" of gunfire weakening the girder. S35 broke to two 13.5 in;
  Eckoldt's cause unrecorded; V44's 6 in HE burst inside tore 6 m2 of side (open item 6). **Whipping:** Cole's period checks to Cheonan
  (0.985 vs 0.990 s); two-node f from a free-free beam with Kumai's virtual mass (destroyer 1.2 Hz, Lion 1.0, Yamato 0.46); a one-cycle
  law M = EI k rho w Vmax T g(wT) Infl/(4 pi M*), valid from gamma ~1 (closer the bubble vents into her: Shubrick, Uganda, Roma);
  Cheonan 18 and a Mk 48 frigate 13 design moments (broke), Ostfriesland 1.3 and Lützow 0.9 (held); uncertain x2-3. **Tallboys:** Tirpitz's
  wing-tank hit tore frames 98-132 of skin, no break, capsized by 17,000 t; Lützow's 18 m near miss a 1 x 10 m tear (the shock's, past
  NearField's reach). Fits above 1 kg/m3 hold to ~10 (afterburn oxygen-limited past 0.37 kg/m3); a bomb table (W/D3, filler, fuzes) and
  fall speeds checked to Tallboy's 336 m/s. Not reached: Job 74's details (Brown pp. 20-24, ADM 324/4), Washington's charge place, the
  Jutland wrecks' belt plates, fuze delays in seconds, British SAP/AP and WWI bombs.

### 2026-11-07 - A magazine's charges as a packed bed: how they were stacked, how confined they burn
- Done 2026-11-07 (draft): [simulation/damage-research/12-charge-bed.md](simulation/damage-research/12-charge-bed.md) with
  [charge_bed_ref.py](simulation/damage-research/charge_bed_ref.py). **A stack with a free face can't stay ahead of its
  room:** the bed throws itself into the aisle in 10-20 ms (racks hold nothing against MPa). Lit at once, a 1.5 m pile of bags
  peaks 5.5 MPa over its room (its face choked), burns 2.5% extra and throws the stack at ~150 m/s; lit over 0.2 s, 0.2-0.4 MPa
  and 0.5-1%. Inertia alone holds 0.4-120 Pa over a burn. So `BedPressure`'s steady bed (3-17 MPa for the whole burn) doesn't
  exist outside strong walls; proposed: a one-off kick (~0.016·L^1.3 of a pile lit together) or the ref's lumped bed, loose
  charges only. Stowage: RN two cartridges to a metal case, lids both ends, racked (Clarkson's was the small calibres' transit
  case: rename `Packaging.Clarkson`); German brass 305 x 1,271 mm with 91 kg; USN tanks in tiers, no racks. Bulk 500-630 kg/m3
  cased, 340-500 USN tanks, 580-770 loose bags (600 kept). Ergun's inertial term holds (Re 1e7), obstacle a cartridge's 6V/S
  (0.34 m), voids 0.35-0.45 from the bag's density; the face chokes first. A lit package's own mouth: x1-2 on the rate (RN case,
  USN tank), x5 for a German brass case lit inside, for ~60 ms. No DDT of the propellant at magazine bulks and walls. Not found:
  magazine plans, case sizes, any 1916-30 stack trial, "Hemus".

### 2026-11-05 - Flooding a magazine: how long it took, and who gave the order
- Done 2026-11-05 (draft): [simulation/damage-research/11-magazine-flooding.md](simulation/damage-research/11-magazine-flooding.md)
  with [magazine_flood_ref.py](simulation/damage-research/magazine_flood_ref.py). Period times found: the USN's 1917 rule
  (flood pipes and magazine sprinklers sized to fill in 20 min), the French Saint Louis class 30 min, Leonardo da Vinci not
  done in 20; no valve bores (physics: 100-250 mm for 50-300 m3). Valves locked, on the main or berth deck, keys in racks or
  the captain's cabin; the RN had a second station below and drenching from 1918 (corrects 10's sprinklers). Deciders were
  plural and mostly outside the train (Lion's Q: Harvey, the Chief Gunner, Chatfield; Seydlitz: the I.O.); engine-room men
  often worked the valves. Battle flashes stopping short, 13 recorded: 7 flooded, 3 saved otherwise, 3 unrecorded, none an
  order that failed; but Good Hope, Black Prince and Borodino burned 10-40 min and blew. Accidents with time: 3 saved, 5
  failed, all at the valves (smoke, keys, one station, slow bottom fill). Proposes FireMagazineFloodS 1,200 s filling by
  head, MagazineOrderS 120 s kept with the valve station as a place the fire can take, the order from any survivor or a
  conned ship, sprinklers USN 1917 and RN 1918, and a yardstick. Not reached: bores, written order rules, Campbell's Jutland.

### 2026-11-04 - What a warship's electric plant fed, consumer by consumer, 1890-1945
- Done 2026-11-04 (draft): [simulation/electric-plant.md](simulation/electric-plant.md), its nine strands as appendices in
  [simulation/electric-plant/](simulation/electric-plant/). ~35 plants 1893-1944 (Iowa 8 x 1,250 kW, PoW 8 x 330 confirmed)
  split by what ran on electricity, not by year: electric-turret navies 45-90 W/t by 1900-16, the RN 17-40. The only
  consumer-by-consumer tables are 1900-02 (George, AIEE): Kearsarge 655 kW connected on 350 (turrets and hoists 44%, cranes
  and winches 33%, fans 15%, lamps 7%), Kaiser Friedrich III 221 on 324. Fits [EST]: training 0.035·W_rev·ω kW (4-5x
  NonPen's friction power), elevation 0.0013·d_in³·ω a gun, whole turret 0.25-0.4 kW/t to 1921 and 0.7 in the 1940s,
  searchlights arc A x mains V. RN heavy turrets on steam hydraulic pumping engines to Nelson (WWI drive circumstantial,
  high confidence) bar Invincible's electric turrets 1908-14; German WWI turrets all on electricity. What each consumer
  falls to (Dreyer to hand +1 man, receivers freeze, gyros hours to settle, USN turrets brake and stand). No ship-wide
  loss from above-water shellfire before 1925 in ~100 heavy hits; every one was water in the dynamo or switchboard spaces or
  an underwater blast (Lion, Audacious, Inflexible, Szent István). Proposes EL3-EL7 numbers, anchors at 100/300/1,000/5,000
  kW for EL6, and 12 checks. Not reached: hand rates, drive weights, KGV/Yamato pump drives, any WWII load analysis.

### 2026-11-02 - Own course and speed into the fire-control table, and the gyrocompass in a turn
- Done: [ship-motion/own-motion-inputs.md](simulation/ship-motion/own-motion-inputs.md). FL1 stands; the Type 92's
  hand-followed course built (0.5 s); the stable vertical in a turn to FL7 (docs/battle/fc-ladder-check.md).

### 2026-10-31 - Striking colours, abandoning and scuttling, 1890-1925 first
- Done 2026-10-31 (draft): [simulation/striking-colours.md](simulation/striking-colours.md), its seven strands as
  appendices in [simulation/striking-colours/](simulation/striking-colours/). 60 decisions and 77 sinkings, Yalu to the 1918 Baltic. A
  ship was given up when she couldn't hurt the enemy, couldn't get away, and an enemy was in reach; casualties weren't
  the trigger (7-50% of the crew). Two clocks: 5-50 min (median ~20) after the last gun in action, or 2-14 h nursing a
  cripple, ended within 0-10 min of an enemy sighted. Land near: beach; sinking at sea: scuttle or abandon; strike rarely,
  and only an admiral (or a ship with one aboard) gave up a sound ship (Nebogatov, Bedovy, Weihaiwei); Germans and
  British 1914-18 almost never struck. Believed: every ensign down on a ship stopped and silent, fire ceasing in 0-5 min
  (doubtful signs 10-30; a gun firing cancels). Saved: blew up 0-2%, fast capsize alone 2-20% or 50-93% with craft near,
  slow with friends 80-100%, slow and left 0-5%; ~half dead in an hour at 10 C. Scuttling: charges 10-30 min, a consort's
  torpedoes 2-30 (many failed), prepared valves at Scapa 56-340 min (median ~2 h 40). The victor took sound strikers as
  prizes the same day, finished wrecks with colours up, left what he couldn't safely reach; rescue held him 10-30 min
  under threat, 1-3 h without. Proposes triggers, doctrine fields (Strike, Deny, StandBy, Rescue, Trust), a "colours down"
  seen fact, survivors in three stages, scuttling as openings in FloodModel, an end rule for BL5, and an 18-case yardstick
  (all [EST]). Not reached: German written orders, prize-crew sizes, tow speeds, Kingston valve sizes, Russian sources.

### 2026-10-29 - Night fighting, 1890-1925 first
- Done 2026-10-29 (draft): [simulation/night-fighting.md](simulation/night-fighting.md), its eight strands as appendices in
  [simulation/night-fighting/](simulation/night-fighting/), and [night_ref.py](simulation/night_ref.py) (the sight law
  against the record). Searchlights by navy and year (plain arc 1.6-1.8e4 cd/cm2, a 36-in ~8-9e7 cd at 2.5-3 deg; the
  RN's pre-war twin 24-in, 36-in and control gear from mid-1915; Russia 6 x 75 cm with remote control; USN high-intensity
  1914-18; useful 2.7-3.7 km plain, 5.5-7.3 km HI; sweep 1.5 deg/s, 1894). Star shell: the Germans' at Jutland (to find,
  then lights already trained), 15 cm Lg 37 kg at 500 m/s; RN from 1917; USN by 1922; burst 300-370 m. Glasses by navy
  (KM 7x50 from 1914, RN 10x45, USN 8x with a 2.5 mm exit pupil). Identification: the challenge gave the challenger
  away; the Germans had part of the British challenge; the British line held fire in 6 of 7 meetings. Cases 1891-1918
  with computed sun and moon: ranges point blank to 650 yd in the dark, Jutland 600-1,500 yd at -11.5 deg (a new moon,
  nautical twilight all night, mist), the British looking toward the glow. Night torpedoes: 1 in 2.5-7 at anchor, 1 in
  9-17 under way. The sky by bearing (Koomen 1952: away/toward 1/11 at -6..-9 deg, not 1/20) and Blackwell's thresholds:
  the sim's night is ×2-5 too long in twilight and moonless dark; a law with Blackwell's shape fits the anchors and keeps
  the day. Not reached: German searchlight sizes, star shell candle and burn, Makarov, Japanese written doctrine.

### 2026-10-27 - Crews under fire: who held their stations, 1890-1925 first
- Done 2026-10-28 (draft): [simulation/crews-under-fire.md](simulation/crews-under-fire.md). Over 40 cases in six
  navies, Yalu to Samar: regular crews of every training level almost never left a working gun unordered; stations
  emptied by casualties, fire and gas, wrecked guns or orders. Unordered leaving shows only in green, worn crews under
  overwhelming HE (Suvorov: freezing, short routs, hiding until rallied), civilian crews (Dardanelles trawlers) and a
  committee crew (Slava). Ordered cover is the big lever (Rossia against Gromoboi at Ulsan: half the dead). Re-manning
  on three clocks (seconds within a crew, minutes from idle mounts, a lull for stokers); no measured under-fire
  gunnery effect beyond the mechanical causes. Proposes thresholds, pressure terms and half-lives, relief-order
  variants and 15 yardsticks for crew.md N3 (all [EST]). Written orders (GFBO, Makarov, drill books) not reached.

### 2026-10-20 - Magazines: the flash chain and the venting race, 1890-1925 first
- Done 2026-10-20 (draft): [simulation/damage-research/10-magazine-flash-chain.md](simulation/damage-research/10-magazine-flash-chain.md)
  with [magazine_race_ref.py](simulation/damage-research/magazine_race_ref.py). Burn law in two branches (alpha ~0.2
  below 0.15 MPa, 0.75-0.9 above; beta MD 1.8, RP ~1.6, single-base ~1.0 mm/s at 1 MPa); turret W and barbette D fits,
  lift 40-140 kPa; charges by station and door practice by navy; flooding 10-30 min, no magazine sprinklers before the
  late-1920s USN; no case of a flash down an open mount's hoist (Kent's casemate stopped at the passage). The race
  says the outcome turns on how much lights at once: open bags tier 3, packaged charges (R < ~2) tier 0-1.

### 2026-10-18 - What inside a ship stops or slows a shell, 1890-1925 first
- Done 2026-10-19 (draft): [simulation/damage-research/09-contents-in-the-path.md](simulation/damage-research/09-contents-in-the-path.md)
  with [contents_ref.py](simulation/damage-research/contents_ref.py). Two rules: media (liquids at the sea's 25 cal
  scaled by density, coal and tube nests at 96,000/rho cal) and solids met at lambda = chord/f in a height band, struck
  as one plate (round ones by impact offset). Tables for R1-R6 by type and period; most dimensions are [EST].

### 2026-10-13 - Jutland at 14:00 GMT: positions, courses, dispositions
- Done 2026-10-13: [jutland-1400-dispositions.md](jutland-1400-dispositions.md). The British 2 p.m. dispositions are
  from Jellicoe's despatch and the Admiralty Narrative; the German ones are reckoned from the first contact (Groos not
  available online). Put into `Content/Scenarios/jutland.json` (commit `8f1d289`). The classes-as-built part was
  split off into its own open request above.
