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
| Scenarios | left column | the shipped scenarios (date, place, each side's ships, its classes and load time, a bar while it builds), filters by era and size, the scale test's rungs under Tests; **your setups** below (a saved setup is a scenario file, see "A setup is a scenario file"); open a file | all |
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
- **The matchup** in the add picker is the new unit's against the enemy line nearest where it's placed, so it fills in
  once it's on the map.

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
  "remove": [ "Grozny" ],
  "crews": { "Japan": "average" }, "seed": 1905 }
```

A swap names a unit and a design, and optionally one ship (`"ship": "Knyaz Suvorov"`); without a unit it swaps every
ship of that design. `"add"` takes units as the scenario's own `"units"` do (one joining another names it in
`"joins"` instead of `"at"`); `"remove"` takes ship or unit names; a temper names a unit or a ship. Applying a setup is a pure function on a parsed `Scenario` (tested), and the launch screen edits
that, so the screen and the file can't disagree. The result says when the fight was a changed one ("Tsushima, as you
set it"), with each change listed.

**What's allowed** (decision 6): nothing is refused here for now. A later pass decides what a scenario allows (its
year, its tonnage, placing in sight of the enemy); a player may still go past it, and the result says so plainly, by
the changes against the scenario as written: someone who took Bismarck to Jutland is shown to have, and someone who
won Tsushima as written is shown to have done the hard thing.

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
| **L3** | Ships: each side's order of battle, the design rows and their state, the picker (sources, filters, figures, matchup, apply to all, one or every), the designer round trip (edit a copy, saved back into the picker as yours) | the matchup on candidates |
| **L3b** | Adding and removing: − n + per row, ✕ per unit, the add picker, the placing view (on the sea, the course dragged), joining a unit | `"add"`, `"remove"` |
| **L4** | Standing orders: aggression and what it moves, the admirals' and captains' tempers, the rules | `Temper` on units and ships |
| **L5** | Conditions and the day: both sides' crews, readiness, damage control, fire discipline, fuel; presets; visibility, wind, the end, the seed | — |

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

## Files

- `launch.html`: the mockup, self-contained but for `assets/land.js` (the opening's coast) and the fonts.
- `assets/launch/*.png`: the classes' thumbnails, baked from the designs at 4 px/m and composited with their turrets.
- `png/launch-*.png`: the states, at 1920×1080.
