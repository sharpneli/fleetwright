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
