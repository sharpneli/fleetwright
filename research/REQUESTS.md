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

### 2026-10-20 - Magazines: the flash chain and the venting race, 1890-1925 first
- Question: the numbers a magazine model needs, as ranges by type and period (a general system, not class replicas).
  (1) Propellant burn rates under pressure (the law r = beta·P^alpha) for cordite MD, RP C/12, single-base (USN SPD,
  poudre B, pyroxylin) and black powder, or ranges to fit within; and bags against brass cases (how many charges light
  together). (2) Turret revolving weights and barbette diameters by calibre and period (07 §7: the lift pressure).
  (3) Flash doors, scuttles, interlocks and handling practice by navy and period: how many charges stood in the
  gunhouse, working chamber, hoists and handling room in drill and at Jutland, and how often doors stood open.
  (4) Sprinklers and magazine flooding: who had them when, and how long flooding took. (5) Open and shielded guns: did
  a flash ever go down an open mount's hoist to its magazine, or was the path always ammunition passages and casemate
  batteries (Defence, Malaya, Kent)?
- Context / relevant files: `docs/battle/magazines.md` (the plan; decision 4 on shield mounts stands on our notes
  until (5) is checked), `research/simulation/damage-research/07-magazine-explosions.md` (the venting race, its open
  questions), `docs/shipgen/research/02_components.md` §1. Not blocking: V0-V2 build first, and the race's constants
  are fitted to the anchors until then.
- Wanted output: tables of burn-rate constants, turret weights and barbette sizes, charges by station and door practice
  by navy and period, flooding and sprinkler times, and the open-gun flash cases, with sources.

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
