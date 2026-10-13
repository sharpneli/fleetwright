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

### 2026-10-13 - Jutland at 14:00 GMT: positions, courses, dispositions
- Question: where was each formation of both fleets at 14:00 GMT on 31 May 1916, and how was it disposed? For each
  (Beatty's battlecruisers and the 5th Battle Squadron, the light cruiser screens and flotillas with them, Engadine;
  Jellicoe's battle fleet in its cruising order, its cruiser screen, 4th LCS, flotillas and attached ships; Hood's
  3rd BCS with Chester and Canterbury; Hipper's scouting groups and torpedo boats; Scheer's battle line, 4th Scouting
  Group and torpedo boats): the lead ship's position, the course and speed, the formation and the distances between
  ships, columns and screens. Also the order of each line (which ship led, which followed), and the classes as built
  (displacement, speed, main battery and layout, belt) for a ship design session.
- Context / relevant files: `Content/Scenarios/jutland.json` (the scenario, positions from memory, marked
  approximate), `tools/scenarios/jutland-classes.json` (the stand-in classes), `docs/battle/plan.md` step 2.7.
- Wanted output: a table per formation (position, course, speed, formation, spacing), with sources, and a table of
  the classes present.

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
