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

### 2026-10-18 - Which fire-control machines each class carried, 1898-1925
- Question: for the classes in the game's first cut, which rangefinders (make, base, magnification, coincidence or
  stereo, how many and where), which computer or plot, and which laying gear (director firing or not, from when) did
  each carry, in the year of its battle? Above all the Royal Navy's Dreyer marks by class (Mk I-V, and which were
  helm-free) and which ships had Pollen's Argo clock at Jutland; the German Jutland ships' clocks, plots and
  Richtungsweiser (and whether turrets laid by director or by their own sights); the USN's Ford Mk 1 rangekeeper (from
  which ships and year); the French, Italian and Austrian tables of 1910-18; the Russians' Liuzhol micrometer sets in
  1904-05 (its error and range, as a stadimeter's). Numbers for any machine's limits (rate clock ranges, bearing rates)
  are welcome.
- Context / relevant files: `src/Fleetwright.Shipgen/FireControlTemplates.cs` (the catalogue; blocks marked [EST]
  where no source gave numbers), `docs/shipgen/fire-control-templates.md`, `docs/battle/fire-control.md` F3 ("Real
  fits"), the designs under `Content/Scenarios/Designs/` (the Jutland and Tsushima ships) and `shipgen/designs/`.
  `research/simulation/fire-control-research/computing.md` §3 has what's known so far. Not blocking.
- Wanted output: a table by class (navy, class, year, rangefinders, computer, laying), with sources, and numbers for
  machines the catalogue lacks.

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

### 2026-10-13 - Jutland at 14:00 GMT: positions, courses, dispositions
- Done 2026-10-13: [jutland-1400-dispositions.md](jutland-1400-dispositions.md). The British 2 p.m. dispositions are
  from Jellicoe's despatch and the Admiralty Narrative; the German ones are reckoned from the first contact (Groos not
  available online). Put into `Content/Scenarios/jutland.json` (commit `8f1d289`). The classes-as-built part was
  split off into its own open request above.
