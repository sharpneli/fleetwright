# The scenario launch: picking, setting up and loading a fight (2026-10-25)

Today a scenario starts from `-scenario=` or the menu's list, straight into the map with the scenario as written; its
ships build in the background while an empty world runs, and the briefing opens over the map. For playtesting that's
thin: no choice of side, no way to try the same fight bolder or with other ships, and Jutland's five seconds of
building spent looking at an empty sea. This plan gives the battler **a launch screen**: the first thing the app shows,
open before any ship is built, where the player picks the scenario and the side, sets the standing orders and the
conditions, swaps in designs of their own ("what if Tsushima, but the Borodinos had 14-inch guns"), and adds or
removes ships and whole units (placed on the opening's map), while the ships build behind it.

The mockup is `launch.html` (open it in a browser; `#japan`, `#watch`, `#design`, `#swapped`, `#orders`, `#add`,
`#place`, `#loading`, `#jutland` open a state; screenshots `png/launch-*.png`). It takes the battle HUD's look (`battle.html`) and is drawn
in full for Tsushima; the other scenarios show their header and their load. The figures are real: each class's from
`shipgen matchup`, the 14-inch Borodino a copy of `borodino.json` with 356 mm guns, sized by the designer (15,350 t),
and the thumbnails baked from the designs at 4 px/m, so the lengths compare true.

## What the player sees

| Piece | Where | Shows | Mocked |
|---|---|---|---|
| Scenarios | left column | the shipped scenarios (date, place, each side's ships, its classes and load time, a bar while it builds), filters by era and size, the scale test's rungs and the benches (squadrons, flotilla: a file's `"test": true`) under Tests; **your setups** below (a saved setup is a scenario file, see "A setup is a scenario file"); open a file | all |
| Header | top | kicker, name, date and place, the weather, sunset, the end; the brief | all |
| Play as | under the brief | one card a side (ensign, strength pips, ships, its character in a line, its commander) and **Watch** (both sides the AI's, everything shown: for checking a setup) | `#japan`, `#watch` |
| The opening | top right | the scenario's start on the map: each top unit at its place with its ships astern, its course for 30 minutes, the range between the sides; yours blue, theirs red. While a new unit is placed it widens over the header into **the placing view** | all, `#place` |
| Ships | first column | each side's order of battle as a tab (yours, the enemy's): units with their admirals and tempers, a row a design (thumbnail to scale, **− n +** for its ships, their names with removed ones struck and captains' tempers marked ▲ ▼, displacement, speed, guns, belt, reach), its build state, **Change…**; ✕ removes a unit (undo in its place); **Add a unit…** at the end | all |
| Design picker | over the orders and conditions | the row's ships; candidates from the scenario's designs, yours and the shipped ones, every filter off by default (type, source, up to the scenario's year); figures and the **matchup** against the enemy's line; the old and new side by side with the changes; use it for all of the unit, one ship, or every ship of the class; edit a copy in the designer; new design from this one | `#design` |
| Add a unit | the picker, in its add mode | name, admiral, ships (− n +, named), formation and spacing, and whether it **joins** a unit (on its station: no placing) or stands **on its own** (placed on the map); the design list as the picker's; then Place it on the map | `#add` |
| Placing view | the opening, widened | the unit at the cursor with its ships astern and its course for 30 minutes; click places her head, drag sets the course; course and speed typed in the bar; where she is, how far from her nearest own unit and from the enemy; Esc cancels | `#place` |
| Standing orders | second column | the side's **aggression** (the C-in-C's, for every unit) with what it moves, written against now; each **admiral's temper**, added to it, with why the scenario gave it (captains' sit on their ships); the rules (engage, deploy, torpedoes, evasion, tracks, night action, night attacks, roles) | `#orders` |
| Conditions | third column | both sides side by side: gun crews, readiness, damage control, fire discipline, fuel; presets (as history, even crews, easier, harder); the day (visibility, wind, the end, the seed) | `#orders` |
| Changes | under the conditions | every change from the scenario as written, one line each, ✕ to undo, reset all | `#orders` |
| The load | footer | one segment a class (dark: building, brass: its art), classes built and art in, the threads; **Begin** (Watch on Watch), Begin paused, Save setup… | `#loading`, `#jutland` |

Nothing on it is a modal: the player can read the brief, pick a side and change ships in any order, and Begin
whenever the ships are ready. Esc closes the picker; with nothing open on the launch screen it opens the app's menu
(battle HUD decision 4).

## Loading: start early, sim first, art after

**Today** (`WorldSession.Load`, `ShipClassRegistry`): a scenario's classes are asked for when it's loaded; each is
built (shipgen), compiled (the sim's class) and drawn (the art: sprites drawn and baked on the same builder thread,
`ShipArtLibrary` on the registry's `Built`), on `ProcessorCount / 2` threads capped at 4, below normal priority. The
world is placed once every class is in, art included. Ships whose art isn't up yet draw as icons.

**Measured** (DevRelease, 2026-10-25, this 32-thread machine):

| Scenario | Classes | Ready after | Thread time: build / compile / art | Slowest |
|---|---|---|---|---|
| Tsushima | 7 | 0.77 s (world running 1.02 s after asking) | 0.92 / 0.31 / 1.39 s | borodino 0.61 s |
| Jutland | 53 | 5.52 s (world running 6.85 s) | 5.11 / 0.36 / 16.20 s | agincourt 1.03 s |

Three quarters of it is art, which the sim never needs. So:

1. **Ask on selection, not on Begin.** A scenario's classes are requested the moment it's picked in the list (and the
   last one played at start), so reading the brief and choosing the side covers the build. A swapped design is
   requested the moment it's picked in the picker. Switching scenarios keeps what's built (the registry is by design
   hash; `Unused` frees it later).
2. **Two queues: every class's build and compile first, then the art.** The world needs only the compiled classes, so
   it can be placed and Begin enabled once they're in: Jutland's 5.5 s of build and compile over 4 threads is about
   1.4 s, the art following while the battle runs (icons until it's in, as now: display only, so the sim is the same
   whenever the art lands). Art order: the player's side first, then the enemy's. The cost: each built `Ship` is kept
   until its art is drawn (the art is drawn from that very ship), so measure the memory at Jutland.
3. **More threads while nothing runs.** The cap of 4 is for building beside a running world. On the launch screen
   nothing runs, so the registry takes more (say `ProcessorCount - 2`, capped at 12) and drops back to the cap when a
   world starts. The bakers are one GPU device per thread (`GpuBaker`, thread-local), so measure where more threads
   stop paying. Estimated at 8 threads: Jutland's classes compiled in ~0.7 s, its art in ~2.7 s.
4. **Later, if it's still slow: a disk cache of the art** by design hash (`ShipClassRegistry.Get` is already the seam
   its comment names), so a scenario played before loads at once.

Begin pressed before the classes are in reads "Begins when ready…" and starts the world the moment they are.

**L0 done (2026-10-25).** `ShipClassRegistry` has two queues: every build and compile before any drawing, so a class
is ready (`Get`) before its art and its `Ship` is held until it's drawn (`IsDrawn`; `WaitAll` waits for both);
`Prefer` puts a class's drawing ahead (the player's side's). Its threads are 4 beside a running world and 8 while
`Idle` (`ProcessorCount - 2`, capped at **8**, not 12: see below); `WorldSession` sets `Idle` each frame while the
world is empty or paused. `WorldSession.Select` asks for a scenario's classes and starts placing its world as soon as
they're compiled; `Begin` swaps that world in when it's placed (`Load` is both, for `-scenario=`, a dropped file and
the menu). The dev panel's picker (F1) selects on change and enables Begin once the classes are compiled. The app's
start selecting the last scenario played is L1's (the launch screen opens on it). `-builders=N[,IDLE]` sets the
threads. Measured (DevRelease, this 32-thread machine; seconds after asking, two runs each):

| Scenario | Classes compiled | World running | Art in (paused / running world) | Peak working set |
|---|---|---|---|---|
| Tsushima, before | 0.50 (with art) | 1.02-1.54 | with the classes | 0.90-0.98 GB |
| Tsushima, L0 | 0.25-0.26 | 1.11-1.29 | 1.16-1.30 / 1.12-1.22 | 0.99-1.00 GB |
| Jutland, before | 5.37-6.33 (with art) | 6.68-7.61 | with the classes | 1.45-1.47 GB |
| Jutland, L0 | 1.03-1.09 | 2.64-3.50 | 3.83-4.00 / 4.21-4.30 | 1.93-2.28 GB |

- **The held ships cost nothing to speak of:** Jutland's 53 built `Ship`s retain 14 MB together (0.3 MB each,
  measured alone). The higher peak is the drawings in flight, 8 at a time instead of 4, and the GC collecting later.
- **Placing is now most of Begin:** `ScenarioLoader.Apply` takes ~1.5 s at Jutland after the classes (~0.9 s at
  Tsushima, where it is the whole of the load): every gun's ballistic tables (`ShellSystem.Prepare`, ~0.7 s) and the
  matchups warmed (`Matchups.Warm`, ~0.7 s), both pure functions of the classes. On the launch screen it runs while
  the player reads, so Begin is instant; a later pass can warm both on the building threads. Holding the art while a
  world is placed was tried and didn't help (placed no sooner, art later).
- **Threads** (Jutland paused, idle count 4 / 6 / 8 / 12 / 16): compiled 1.3 / 1.1 / 1.0 / 1.2 / 1.3 s, art in 5.0-5.9 /
  4.2-5.1 / 3.9-4.4 / 4.0-4.3 / 3.8-4.0 s, running 2.8-3.1 / 2.9-3.0 / 2.7-3.9 / 3.8-4.0 / 3.7-4.0 s. Builds stop
  scaling past ~6 (their thread time grows from 5 to 8-10 s: the GC and memory), the art past 8, and past 8 the
  bakers crowd the world's placing. The art's thread time doubles from 4 to 12 threads (16 to 33 s).

## Adding and removing ships

The opening's map made adding possible (decision 4): a unit added on its own is placed there, at its head, with the
course dragged out from it, as the spawner places dev ships today but before the world exists. A unit that **joins**
another (a fourth division astern of the Second Pacific Squadron) needs no placing: it takes its station in that
unit's formation as the scenario's own units do. Removing is the row's − (the last ship of the row first; a struck
name's + brings her back) and the unit's ✕. Every add and removal is on the changes list.

- **Where a unit may go:** on the sea, off the land (`ShipSystem`'s slide keeps ships off it anyway; the placing view
  refuses a head on land). Scenarios have no area yet (`docs/missions/plan.md` "A mission, as data" gives missions
  one); until they do, anywhere the placing view shows. The pass on what's allowed decides whether a unit may start in
  sight of the enemy.
- **Names:** an added ship gets "new" and her class until named; the player may name her in the row. An added unit's
  name and admiral are the player's.
- **No matchup** in the add picker (decision 7): the scenario's maker places the units, and an added one meets whom it
  meets.

## Tempers: the side, the admiral, the captain

Aggression is set at three levels and they add (decision 1): **the side's** (the C-in-C's standing order, the big
slider), **an admiral's** temper (his unit and those under it that have no admiral of their own), and **a captain's**
(her ship's). A captain's decisions (falling out, evading, torpedoes at will) read side + her admiral + herself; an
admiral's (fight or turn away, break off, how close) read side + himself. Clamped to ±1. So a timid captain under a
bold admiral fights harder than she would alone, and never as hard as a bold captain under the same flag. The
scenario gives tempers where history suggests one (Enkvist cautious, Kamimura bold; in the mockup Aleksandr III's
captain ▲, Bedovy's ▼, as examples); the player may change any. The admirals' sliders are on the standing orders
column; a captain's is on her row (the mark by her name opens it).

## A setup is a scenario file

Everything the launch screen changes is something a scenario file already says or nearly says, so a setup is kept as
**the scenario it came from plus its changes**: a small file in the user's folder (beside their designs), listed under
"Your setups", shareable like a design (sharing ships is passing files around), and the same fight again for the same
seed. The file's `"from"` already takes another scenario's units; it grows to take everything else too unless
overridden, and gains the swaps:

```json
{ "scenario": 1, "name": "Tsushima · 14 in Borodinos", "from": "tsushima", "player": "Russia",
  "designs": [ { "unit": "1st Armoured Division", "from": "borodino", "to": "borodino_14in" } ],
  "doctrine": { "Russia": { "preset": "default", "Aggression": 0.5, "NightAction": "accept" } },
  "tempers": { "Cruiser Division": 0.2, "Imperator Aleksandr III": 0.3 },
  "add": [ { "name": "3rd Armoured Division", "side": 0, "at": [34.035, 129.80], "course": 23, "kn": 11,
             "ships": [ { "design": "oslyabya", "names": ["Imperator Nikolai I", "General-Admiral Apraksin"] } ] } ],
  "counts": [ { "unit": "1st Destroyer Detachment", "design": "buyny", "by": -1 } ], "remove": [ "Cruiser Division" ],
  "crews": { "Japan": "average" }, "seed": 1905 }
```

A swap names a unit and a design, and optionally one ship (`"ship": "Knyaz Suvorov"`); without a unit it swaps every
ship of that design. `"add"` takes units as the scenario's own `"units"` do (one joining another names it in
`"joins"` instead of `"at"`); `"counts"` takes ships off the end of a squadron's row (struck, the last first) or adds
unnamed ones; `"remove"` takes units' names; a temper names a unit or a ship. Applying a setup is a pure function on a parsed `Scenario` (tested), and the launch screen edits
that, so the screen and the file can't disagree. The result says when the fight was a changed one ("Tsushima, as you
set it"), with each change listed.

**What's allowed** (decision 6): nothing is refused here for now. A later pass decides what a scenario allows (its
year, its tonnage, placing in sight of the enemy); a player may still go past it, and the result says so plainly, by
the changes against the scenario as written: someone who took Bismarck to Jutland is shown to have, and someone who
won Tsushima as written is shown to have done the hard thing. The likely shape (the user, 2026-10-25): a design stays fair while it keeps within the
designer's approved template years (its tech blocks); a fight with one past them gets an "Imaginary" tag on its result.

## What the sim needs

- **Tempers.** Doctrine's `Aggression` is inherited by override today: a unit's value replaces its side's. It
  becomes the side's only, with a `Temper` on units (the admiral's) and on ships (the captain's), summed as in
  "Tempers" above wherever aggression is read (decision 1).
- **Added units** are scenario units placed by the same `ScenarioLoader.Apply`; a joining unit takes its station
  in the unit it joins (as a scenario's nested units do).
- **The player's side may be none** (Watch): `Scenario.Player` is -1 already; the map shows everything (the truth)
  and the bar's tally counts both sides.
- **Matchups for the picker:** the commander's own judgement (5.1g), four of the candidate against the enemy unit
  that line would meet, needs the candidate's compiled class: the picker asks for its filtered candidates' classes
  (sim only, no art) when it opens, and fills the column as they come in.
- Nothing else: crews, readiness, damage control, fire discipline, fuel, weather, loadouts and the seed are all a
  scenario's already.

## Steps

Each a session, as for the HUD: the piece ported to ImGui in the designer's theme, judged by `-ui` captures beside the
mockup's, committed.

| Step | What | Needs |
|---|---|---|
| **L0** (done 2026-10-25) | The load: classes asked on selection, the two queues (compile first, art after), more threads while nothing runs, Begin when compiled; measured at Tsushima and Jutland, memory included | `ShipClassRegistry`, `ShipArtLibrary`, `WorldSession.Load` |
| **L1** (done 2026-10-25) | The launch scene: the list, the header and brief, the side cards (Watch included), the opening drawn from the file's top units, the footer's load and Begin. The app opens on it (the last scenario played selected); the menu's and the result's "Scenarios" open it; `-scenario=` still goes straight into the world (captures and tests), `-launch[=name]` opens the screen. The briefing modal at start goes (the brief is on this screen; the menu's Briefing stays) | — |
| **L2** (done 2026-10-25) | A setup as data: the overrides on a parsed `Scenario`, saved and listed as a scenario file with `"from"` and `"designs"`; the changes list and reset | `Scenario`, `ScenarioLoader` |
| **L3** (done 2026-10-25) | Ships: each side's order of battle, the design rows and their state, the picker (sources, filters, figures, matchup, apply to all, one or every), the designer round trip (edit a copy, saved back into the picker as yours) | the matchup on candidates |
| **L3b** (done 2026-10-25) | Adding and removing: − n + per row, ✕ per unit, the add picker, the placing view (on the sea, the course dragged), joining a unit | `"add"`, `"remove"` |
| **L4** (done 2026-10-25) | Standing orders: aggression and what it moves, the admirals' and captains' tempers, the rules | `Temper` on units and ships |
| **L5** (done 2026-10-25) | Conditions and the day: both sides' crews, readiness, damage control, fire discipline, fuel; presets; visibility, wind, the end, the seed | — |

L0 stands alone and pays at once (every load gets faster); it can go before the mockup's review is done.

**L1 done (2026-10-25).** `Map/LaunchScene.cs` (and `.Opening.cs`), a scene of its own that `SceneSwitcher` shows in
place of the map (index -1) until a scenario begins (`Home`: where the designer goes back to; "Back to the battle" in
the menu once one has). The app opens on it unless the command line asks for a world (`-scenario=`, `-ships=`,
`-map=`, `-view=`, a designer flag); `-launch[=name]` opens it anyway. The last scenario begun is kept in
`My Games/Fleetwright/launch.json` and picked at start. Picking a scenario or a side calls `WorldSession.Select` with a
copy of the scenario (`Scenario` is a record now: `s with { PlayerName = ... }`), which asks for its classes and places
its world; Begin (or Begin paused, or Watch) swaps it in and shows the map; Watch begins with no player side and the
truth shown. The menu's Scenarios… and the result's Scenarios open it (a running battle pauses under it, and runs on
if the player goes back without beginning); the briefing no longer opens at a load (the menu's Briefing and `-brief`
stay). A scenario file dropped on the screen joins the list under Opened and is picked.

- **The files say what the screen shows:** `"place"` and each side's `"about"` (character, commander, ensign),
  taken through `"from"`; the four shipped scenarios have them. The meta line is worked out: the date in local mean
  time by the longitude (the sun's, not a zone's: Tsushima reads 13:20 where the mockup has Japan's 13:40), the
  position, the weather in words, the next sunset (or sunrise) by the sun model, the end.
- **The side cards' pips** (capital, cruiser, light, by `Sightings.TypeOf`) come once the side's classes are compiled,
  and only up to 24 ships (Jutland's sides show their counts alone, as the mockup's do).
- **The opening** frames every ship of the top units on its station (`ScenarioLoader.Stations`, the placing's own)
  with half an hour's course; the coast is the 1:50M land brought into the view's cap (`CapRetraction`) and filled by
  pixel rows by its winding number (ImGui's concave fill can't take the retraction's rings), made once per pick.
  Labels that would overlap are left out. Tsushima's frame holds no land, as the mockup's doesn't.
- **The body until L3-L5** shows what the briefing did: your units and their ships, the enemy's units as reported
  (both sides in full when watching), the aim and the end, the orders before action. Both unit lists scroll.
- **Left for later:** Settings (there are none), the list's load estimates ("under 1 s"), opening a file by a dialog
  (dropping one works), the side cards' fixed height with a long character line.

**L2 done (2026-10-25).** `Sim.Missions/ScenarioSetup.cs`: a setup is the scenario as written plus an ordered list of
changes (`SetupChange`: `PlayAs` a side or watch, `SwapDesign` by unit, by one ship or everywhere, `SideSetting` for a
side's gun crews, readiness, damage control, fire discipline or fuel, `SetSeed`), one per key, a later change of a key
replacing the earlier. `Apply` is pure: units are copied where a swap reaches (a one-ship swap splits her entry round
her, the order and the names kept, so the stations don't move), the written scenario untouched, and the result
remembers both (`Scenario.Setup`, `Scenario.Written`). The file is a scenario file with `"from"` and none of
`"units"`, `"only"` or `"sides"` (those still compose, as the ladder does); its keys are exactly the changes' (`player`
(null to watch), `designs`, `crews`, `readiness`, `damage_control`, `fire_discipline`, `fuel`, `seed`), anything else
refused, and it reads back to the same scenario (tested). `Scenario.Load(path, fromFolders)` finds a setup's
`"from"` in the shipped folder; its designs are the written one's, then the shipped, then the player's
(`My Games/Fleetwright/Designs`, for L3's swaps).

- **On the screen:** the pick is the written scenario and the setup being edited; the side cards edit it (the side as
  written takes the change away again). The third column ends with **Changes from the scenario**: a line each, as
  `SetupChange.Describe` says it against the written one, a ✕ to undo it, Reset all. **Save setup…** asks a name and
  writes `My Games/Fleetwright/Setups/<name>.json` (a setup saved again keeps its file; a new name taken by another
  gets a number), then reads it back as its entry and picks it: what's listed is what's on disk. **Your setups** at the
  list's foot lists them newest first; a shipped card with setups of yours says so; a setup picked heads the screen as
  "Your setup of Tsushima". The last begun is remembered as `setups/<name>`.
- **The result** says "Tsushima, as you set it:" with each change, when the fight was a setup's.
- **Still to come in this format:** doctrine and tempers (L4), the day (L5), `"add"` and `"remove"` (L3b): each joins
  `ScenarioSetup` as a change of its own, with its file key.

**L3 done (2026-10-25).** `LaunchScene.Ships.cs`:
the first column is the sides' orders of battle as tabs (yours first; watching, side by side), each unit with its count
and under it a row a design: the ship drawn to scale (all rows at one scale, the longest ship of the pick fitting), how
many, the names (cut to the column, the whole list on hover), the class's figures in a line, its build (building, art…,
ready, failed) and Change…. A row the setup changed is brass, with what it was ("was Borodino-like · all 4").
`LaunchScene.Picker.cs`: the picker opens over the columns right of the ships, a window of its own; every design the
pick can name, found as a scenario finds them (its own Designs/, the shipped, the player's; the first of a name wins),
the row's own first, the player's newest, the scenario's, the shipped by year; a search and the filters (the row's
type or any, the three sources, up to the scenario's year), all off but the sources at first (decision 6). The old and
the new side by side with each figure's change; used for the row's unit, one ship of it (a name picked), or every ship
of that design in the scenario. Esc, ✕ or Cancel closes it; Use (or a double click) makes the change.
`LaunchScene.Designs.cs`: the design files read on a thread at the start (the player's again whenever the screen is
entered), and the figures.

- **Candidates are built without their art** (`ShipClassRegistry.Request(design, art: false)`): opening the picker asks
  for every candidate's class, ~130 at Tsushima, so their figures fill in within a few seconds; drawing them all would
  be ~40 s of the builders' time and ~1 GB of textures. The one highlighted is asked for again with its art (a class
  built without it is built again for the drawing, its class kept), so the comparison and a swap show the real ship;
  the rest draw as an outline of their length and beam. The ImGui sampler now reads mip levels, so sprites drawn small
  don't shimmer.
- **The figures** are the matchup's own view of a class (`MatchupProfile`: displacement, length, speed, the main guns'
  reach), worked out on a thread once the class is compiled, plus the guns by calibre (the chief battery's, then the
  heaviest other, then the tubes) and the belt (the thickest side plate at the waterline).
- **The matchup** (`LaunchScene.Matchup.cs`, done the same day): each candidate judged by the commander's own
  `Matchup.Judge` (5.1g), as many of her as the row's unit has against one enemy unit for the whole column: of the
  enemy's units with ships of their own, the one whose ships are nearest the row's in displacement (as a ratio). Each
  side at its doctrine's range share (`ScenarioLoader.SideDoctrine`, tested), in the start's light. A column (green or
  red against the design in use), a row in the comparison, and the line it's of over the list ("Matchup: 4 of her v 1st
  Division (4 Mikasa-like, 2 Izumo-like); 1 = even"). At Tsushima: Borodino-like 0.53, Mikasa-like 0.50.
  - "Nearest in size" isn't always the line she'd meet (Jutland's 1st Scouting Group is judged against the Queen
    Elizabeths, not Beatty's battlecruisers); it stays so: no pairing by role, no scenario naming whom a unit meets
    (decision 7).
- **A design's year** is the latest year its file names (its machinery's, its guns', its shells'), shown as "tech 1905"
  and used by the "up to" filter: designs have no year of their own. A design file could say one later if the filter
  needs to be exact.
- **A swap made again** changes the change it made: `ScenarioSetup.Swap` (tested) changes the change that brought the
  row's design with the same reach, or takes it away when it puts back what was written, so trying designs back and
  forth leaves one line in the changes, or none. The changes list names designs by their titles ("1st Armoured
  Division: Borodino-like, now Mikasa-like"); the font has no arrows, so the line says "now".
- **The designer round trip:** "Edit a copy in the designer…" (or "+ New design from …" at the list's end) opens the
  designer on a copy, unsaved, with its own history (`DesignSession.Copy`); back on the launch screen (`SceneSwitcher`'s
  `LaunchEntered`) the player's designs are read again and one saved since is picked in the picker, still open.
- **Left for later:** the admirals' names and tempers on the
  units, the captains' marks (L4); − n + and ✕ (L3b). Jutland's rows draw small because one design is far too long
  (docs/battle/bugs.md, Agincourt-like at 313 m).

**L3b done (2026-10-25).** `ScenarioSetup` takes three changes more, each tested and read back from its file:
`AddUnit` (a unit on its own, a top unit with its place, course and speed, or `"joins"` a force as its last unit, on its
station; of a unit's name, side, formation, spacing and units or ships only, and a name new to the scenario),
`ChangeCount` (a squadron's ships of a design, more unnamed at the end, or fewer: struck, the last first, kept by name on
`ScenarioShips.Struck` for the screen and never placed) and `RemoveUnit` (every ship under it struck, the unit kept as
`Removed`). The changes now apply by kind (the side, the adds, the swaps, the counts, the removals, the settings, the
seed), each kind in the order made, so a file written by kind reads back the same; a count names its ships' design after
the swaps, which `Swap` keeps (a row swapped carries its count along). A unit the setup added takes its own − + and
design changes into its `AddUnit`, so the changes list says it in one line ("Russia: added New squadron (4 ×
Aurora-like)"); its ✕ takes it and anything made of it away.

- **The column** (`LaunchScene.Ships.cs`): each row's − n + (− stops at none, or at an added unit's last ship), the
  struck names after the live ones, greyed and struck through; each unit's ✕ (any level) and, taken off, its name struck
  with "removed · undo" in its place; an added unit brass with ADDED, its count, and "move" when it's on its own; **+ Add
  a unit…** at the foot of each side's tab.
- **The add picker** (`LaunchScene.Add.cs`, the picker's add mode): the name (new to the scenario), the ships (− n +, of
  the design picked, unnamed), the formation and spacing, and Joins (on its own, or each of the side's forces "on its
  station"); the design list as the picker's, without the type toggles or a matchup (decision 7); the foot says what it
  adds ("4 × Aurora-like protected cruiser: 14,548 t at 20 kn"), then **Place it on the map…** or **Add it**.
- **The placing view**: the opening widened over the header (more margin round the units); the unit at the cursor with
  its ships on their stations and half an hour's course, amber (red over land); a press places her head, a drag sets the
  course; course and speed typed in the bar (starting from the side's lead unit's, no faster than the design), beside
  her place, the nearest of her own and the nearest enemy; Esc or Cancel stops. A head on land isn't placed.
- **Checked** by scripted clicks at Tsushima (-input=): − on the destroyers strikes Grozny, + on the cruisers adds one,
  ✕ strikes the 2nd Armoured Division; a unit placed by a drag lands on its dragged course; one joined to the Second
  Pacific Squadron begins in the world (31 ships in 10 groups), where its commander screens with it.
- **Added after the user's first try (the same day):** a unit joins any unit of its side, a squadron too: joined, a
  squadron becomes a force of its name, its own ships the first unit ("Cruiser Division I"), the new one astern at its
  spacing; every unit heading has **+ division** (the add picker, joining it), and Joins is a list of the side's units.
  **Renaming**: a click on a unit's name types a new one (`RenameUnit`, `"rename": { "Cruiser Division": "Enkvist's
  cruisers" }` in the file; an added unit renamed in its own change). Renames apply last, the scenario's tasks that name
  the unit with them, so every other change names units as written (`ScenarioSetup.NameAsMade`): a division made by a
  join can be renamed too.
- **Left for later:** naming an added ship in her row (she's unnamed: "and 1 more"); zooming the placing view (it frames
  the units, so a far placing means placing at the frame's edge); the admiral of an added unit (L4); a capture flag for
  the screen's states (the scripted clicks did).

**L4 done (2026-10-25).** The sim first: doctrine's `Aggression` is a side's only (a unit's is refused) and a new
field, `Temper`, a unit's only: a group's is its admiral's (the nearest up the tree that has one), a ship's her
captain's. `Doctrine.Of` (what a captain reads) is side + admiral + captain, `OfGroup` (an admiral's) side + admiral,
clamped to ±1, and that sum scales the AggressionShift fields as the side's alone did (tested in `DoctrineTests`).
The scenario file names a unit's admiral, `"admiral": "R-Adm Enkvist"`, or gives his temper too, `"admiral": { "name":
..., "temper": -0.4, "why": "kept his cruisers clear of the line" }`; a ship entry its captains', `"captains": {
"Bedovy": -0.5 }`. A unit with an admiral of its own (a top unit, one named, or one with a temper given) has his
temper, 0 if none is given; one without takes its force's (decision 1's "those under it that have no admiral of their
own"). Tsushima has the mockup's: Enkvist −0.4, Togo +0.2, Kamimura +0.4, the Japanese flotilla +0.3, Aleksandr III's
captain +0.3, Bedovy's −0.5, Akebono's +0.4; Squadrons, Flotilla and Jutland name their admirals with no tempers, so
they fight as before.

- **The setup** takes two changes more (tested, read back from the file): `SetDoctrine` (a side's field: its
  aggression or a rule; `"doctrine": { "Russia": { "Aggression": 0.5, "NightAction": "accept" } }`, enum fields by
  name) and `SetTemper` (a unit's or a ship's, by name; `"tempers": { "Cruiser Division": 0.2 }`). Each is gone when set
  back to as written. An added unit's admiral and temper go in its own change (`"admiral"` in `"add"`).
- **The column** (`LaunchScene.Orders.cs`) is of the side the ships' tab shows (the enemy's too, marked AI): the
  doctrine's preset (all its fields on hover of "all fields…"), the aggression's slider with the scenario's tick and
  what it moves written against now (the ten fields the settings' AggressionShift lists), the tempers (every top unit
  and every unit with an admiral of its own: his name, the unit, why the scenario gave it while it's as written, his
  slider, and what he reads, side + his), the captains' note, and the eight rules as two-way switches. Sliders go by
  0.05 and make their change on release (each change places the world again). The aims and the orders before action
  moved to the third column, over the changes, until L5.
- **The ships column**: each unit heading names its admiral and, when his temper isn't 0, a chip (BOLD +0.40,
  CAUTIOUS −0.40); a captain with a temper has a mark after her name (bold up, amber; timid down, blue), and a click on
  the names opens the row's captains, a slider each.
- **Checked** by scripted input at Tsushima: a drag of the aggression to +0.50 moves the fields (outgunned 2.0 to 2.5 : 1,
  range 60% to 51%), Enkvist's slider to +0.35 reads +0.85 and turns his chip bold, heavy ships at night to accept;
  three lines in the changes. The enemy's tab shows Togo's, Kamimura's and the flotilla's.
- **Left for later:** choosing another doctrine preset (one "default" is all there is); a temper's why for captains;
  the admiral's name of an added unit typed in the add picker (the file takes one); the picker's matchup reads each
  side's range share without its aggression's shift.

**L5 done (2026-10-25).** The setup takes the day: `SetWeather` (all of it, `"weather"` as a scenario writes it),
`SetEnd` (`"until"`) and a seed that may be `"new"`: a new one each time it's begun, which `Apply` leaves as written and
the screen draws (once a pick, again after each Begin; shown as "this time 71154"). The sides' settings were L2's
`SideSetting` already. Tested and read back from the file.

- **The column** (`LaunchScene.Conditions.cs`, the third): both sides side by side (yours first, or the first tab's when
  watching) in gun crews, readiness, damage control, fire discipline and fuel, each a drop-down of the settings' names
  (fuel full, 75%, 50%, 25%), brass when it isn't as written; the presets over them; then the day: visibility (1 to 30
  km), the wind (from, by the sixteen points, and its strength, by 2 kn), the end (by the hour, local, up to three days
  after the start), the seed (the file's, or new each time), the aim and the end in words; then the changes, as before.
  The orders before action that stood there since L1 are these drop-downs now.
- **The presets** are first guesses for playtesting (decision 5), each a set of the sides' settings over the written
  ones (the day untouched): as history (none), even crews (both trained), easier (yours trained; the enemy's average,
  his decks cluttered), harder (yours green, your decks cluttered).
- **Checked** by scripted input at Tsushima: Harder, the visibility dragged to 8 km, the wind up to 20 kn, the seed new
  each time, the end two hours later; each a line in the changes, the header's day line following.
- **Found on the way:** two − n + steppers in one column shared their buttons' ids, so the second's clicks were lost; each
  is scoped now.
- **Left for later:** the sea state beside the wind (the mockup's "sea 4": no sea state in the sim yet); the end said in
  the changes in GMT, the screen in local time; typing a seed.

## Decisions (the user, 2026-10-25)

1. **Tempers add**, at three levels: the side's, the admiral's, the captain's (a captain's personality per ship). A
   timid captain under a bold admiral is bolder than alone, never as bold as a bold captain under him ("Tempers").
2. **Begin before the art is in.**
3. **The enemy's ships can be changed**, as yours.
4. **The preview stays a framed map**, and becomes where added units are placed: adding and removing ships and units
   is in ("Adding and removing ships").
5. **The presets** (easier, harder): decided after playtesting with the screen in place.
6. **Nothing is filtered or refused for now**: the picker shows every design. What a scenario allows is a later pass;
   the result shows plainly what was changed, so the hard thing done is seen as done.
7. **No automatic matchup of lines**: the placement is the scenario maker's, and a fun scenario is what it is (torpedo
   boats swapped for battleships make a livelier start). The picker's column stays as built (nearest in size); added
   units get none, and no pairing by role.

## Files

- `launch.html`: the mockup, self-contained but for `assets/land.js` (the opening's coast) and the fonts.
- `assets/launch/*.png`: the classes' thumbnails, baked from the designs at 4 px/m and composited with their turrets.
- `png/launch-*.png`: the states, at 1920×1080.
