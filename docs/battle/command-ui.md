# Fleet Command UI Research

Oct 9, 2026 · Teemu

## Summary

The genre's UI problem is not visual polish; it is that most games make the player do every job on the ship at once, through menus built for a database rather than a commander. The fix is to let the player state **intent** (what and why), let subordinates handle **execution** (how), and make every order **previewable, visible on the map, and reversible**.

Top recommendations, in priority order:

1. **Command by intent, not by keystroke.** Orders are tasks with a goal (screen, escort, intercept, hold) that ships execute with their own judgement, bounded by player-set doctrine. Direct control stays available but is the exception.
2. **One smart verb, many explicit ones.** A single context-sensitive action (right-click / drag) covers the 80% case; a radial marking menu exposes every other verb at the cursor, and the same gestures become muscle memory for experts.
3. **Preview before commit.** Every order shows its consequence in the world before release: path, arrival time, weapon arcs, fuel/ammo cost, and which enemies can reach you along the way.
4. **The map is the interface.** Orders, intent, and state live on the map as symbols and lines, not in side panels. Panels exist for detail, never as the primary place to act.
5. **Symbology with redundancy.** Adopt the logic of NATO APP-6 / MIL-STD-2525: affiliation coded by frame shape *and* colour, certainty coded by line style, level of detail driven by zoom.
6. **No hidden modes.** The game never silently changes who is in control of a ship, which weapons are free, or what an input will do. The USS John S. McCain collision is the cautionary tale.
7. **Time is a UI tool.** Pause-and-order, slow-motion, and an alert system that tells the player *what needs a decision* rather than everything that happened.

## Why genre UIs fail

The same six failures show up in player feedback across naval and space command games, regardless of era or budget.

| Failure | What it looks like | Evidence |
| --- | --- | --- |
| Player does every crew job | Fleet admiral, captain, weapons officer and damage control all at once | Nebulous players [describe doing everything from fleet command down to damage control](https://steamcommunity.com/app/887570/discussions/0/3779119905187883230) and wanting to lead a fleet instead |
| Right-click menu hell | Every verb buried in a long flat or nested context menu | EVE players proposed [moving fleet functions out of the right-click menu](https://forums-archive.eveonline.com/message/5733941/); CMO's devs [restructured their unit context menu](https://forum.quartertothree.com/t/command-modern-air-naval-operations-the-harpoon-that-never-was/75669?page=17) to be deeper rather than flatter |
| Optimal play demands joyless micro | The best result needs constant map-checking and re-ordering | Starsector's first RTS-style scheme [rewarded constantly interrupting combat to tweak orders](https://fractalsoftworks.com/tag/rts) |
| RTS expectations, non-RTS units | Click implies instant obedience; realistic ships hesitate and look like they're ignoring you | Starsector's dev names this [mismatch between implied and actual control](https://fractalsoftworks.com/tag/rts) as a core reason the RTS model failed |
| No feedback on why things happened | Battles resolve but the player can't see who shot whom with what | HOI4 players [complain the naval combat view hides what is actually going on](https://steamcommunity.com/workshop/filedetails/discussion/1687056790/1850323802578952897) |
| Clunky order entry | Simulation depth is praised; issuing orders is not | Sea Power's review aggregate lists [clunky, non-intuitive order UI](https://vaporlens.app/app/1286220/sea_power_naval_combat_in_the_missile_age) as a top complaint |

The common root: the UI exposes the **simulation's data model** (every subsystem, every parameter) instead of the **commander's decisions**. Realism of the simulation and realism of the *role* are different things; a real admiral has a staff.

## Lessons from reference games

Each reference below solved one piece of the problem well; none solved all of it. Steal the piece, not the whole.

| Game | Steal this | Avoid this |
| --- | --- | --- |
| [Starsector](https://fractalsoftworks.com/tag/rts) | Assignments (capture, defend, escort) that ships carry out themselves; limited command points so you aren't rewarded for spamming orders | Making the intent model the *only* entry point: clicking your own ship gave assignments that targeted it, not orders for it, which confused players |
| [Supreme Commander](<https://en.wikipedia.org/wiki/Supreme_Commander_(video_game)>) | Strategic zoom: one continuous scroll from a single hull to the whole map, units collapsing to icons as you pull back | Letting the icon view become the whole game; [some devs deliberately avoided](https://forums.wincustomize.com/469761) a sea-of-icons endgame |
| [Total War](https://academy.totalwar.com/total-war-rome2-battle-controls/) | Right-drag to set position, frontage and facing in one gesture; hold a key to see every unit's destination; shift-queue | Modifier soup (ctrl/alt/double-click variants) that players [have to document for each other](https://steamcommunity.com/app/34330/discussions/0/3039355280230136766) |
| [Frozen Synapse](https://en.wikipedia.org/wiki/Frozen_Synapse) | Simulate your plan before committing, including guessed enemy moves; drag handles to edit an order in place | Nothing major for our purpose; the plan-preview loop is directly transferable to pause-and-order |
| [Into the Breach](https://gdcvault.com/play/1025772/-Into-the-Breach-Design) | Enemy intent telegraphed on the board; nothing on screen without a purpose | Perfect information doesn't fit a sensor-driven game, but the principle of showing *known* threats in the world does |
| [Highfleet](https://www.pcgamer.com/au/unique-action-strategy-game-highfleet-is-coming-to-early-access-this-month) / [Carrier Command 2](https://www.speedrun.com/Carrier_Command_2/news/r65gklxv) | Diegetic, tactile instruments that make the fantasy legible; CC2 groups controls into stations by mode of play | Diegesis at the cost of speed; even admirers found Highfleet's layered systems daunting |
| Nebulous / Sea Power / CMO | Simulation depth that players genuinely love; damage-control priorities, EMCON, sensor play | Exposing all of it at one level of the UI; reviews consistently separate "great sim" from "painful to command" |

The through-line: the best-regarded command UIs **reduce the number of decisions per minute** while **raising the weight of each one**, and they show the result of a decision before and after it is made.

## Deep dive: Rule the Waves and CMO

Both games already have the command model we want; what they lack is a way to express it on the map. The goal is to keep their viewpoint (you are the admiral, the simulation is honest, control is limited) and replace their dialogs, tables and menus with direct, previewable gestures.

### Rule the Waves combat: the model to keep

- **You command divisions, not ships.** In battle you usually [control only your flagship and a few nearby divisions](https://www.navalgazing.net/Review-Rule-the-Waves-3); the rest act on their own. This is exactly the top two rows of our command-layer table.
- **Structure is set before battle.** RTW3 lets you [set each division's subordination and the role it plays when battle comes](https://www.slitherine.com/news/rule-the-waves-3-comparison-document), and divisions develop personalities from their commanders. Players use this as doctrine: a battleship flag division with a [subordinate cruiser division set to scouting](https://steamcommunity.com/app/2008100/discussions/0/3826416272370372562).
- **Orders are coarse; doctrine does the rest.** Per division you mainly set heading and speed, and rules like [HE vs AP per gun calibre per target class](https://roundtablecoop.com/?p=5073) come from doctrine.
- **Friction is real.** Signals can be [misread, sending AI ships off on the wrong orders](https://roundtablecoop.com/?p=5073). This is a feature, and the UI's job is to make it legible rather than mysterious.

### Rule the Waves: where the UI fails it

| Problem | Evidence | Root cause |
| --- | --- | --- |
| Course is set either by typing exact numbers into a text dialog or by Ctrl+click on the map | Reviewer finds control [hampered by the interface](https://roundtablecoop.com/?p=5073) and hopes it improves; in our own play, Ctrl+click on the map is the only practical method | The usable path is hidden behind a modifier because right-click is taken by a standard Windows context menu, complete with an error ding. The map gesture is second-class to the dialog |
| Carrier air ops are opaque | Players ask why [planes take off without an execute order](https://steamcommunity.com/app/2008100/discussions/0/4362373511166232156) and track it through a separate air formations screen | Automatic behaviour runs without visible state or reason |
| Your structure gets silently rearranged | Players form divisions, then find the battle setup [splits them into its own divisions](https://steamcommunity.com/app/2008100/discussions/0/508455105153036788) | A hidden mode: the system overrides player intent without saying so |
| Organisation chores | Replacing ships means [manually clearing out division after division](https://steamcommunity.com/app/2008100/discussions/0/4293692116962694166) | No bulk operations on the hierarchy |
| Dated presentation | Review aggregates call the UI [spreadsheet-like and dated](https://vaporlens.app/app/2008100/rule_the_waves_3.md), even when it is functional once learned | Tables are the primary surface; the map is secondary |

### Translating RTW's model into map gestures

Naval signalling already had a compact grammar for this. At the River Plate, Harwood's [signal ZMM meant single ships turn, MM meant divisions turn](https://thegazette.co.uk/London/issue/37989/supplement/2760/data.pdf). Tabletop rules draw the same distinction as a [single-line arrow for turn-in-succession and a double-line arrow for a simultaneous turn](https://www.wtj.com/games/beta/battlefleet_1915/docu_rules_a.htm). That is a ready-made visual vocabulary.

| RTW concept | Our treatment |
| --- | --- |
| Division course and speed | Plain right-drag from the division leader, no modifier and no dialog: angle sets course, length sets speed, ETA and turning arc shown live |
| Turn in succession vs turn together | Two radial verbs drawn as two arrow styles on the map: single line (follow the leader's wake), double line (all ships turn at once) |
| Division role and subordination | Shown on the map as thin hierarchy lines from subordinate to parent division, plus a role badge on the symbol; editable by drag in the fleet tree |
| Doctrine (ammo, engagement range, retreat) | The per-fleet doctrine card; overrides badge the ship |
| Signal delay and misreading | Every ordered division shows two things: the order you sent (ghost line) and the order they are executing (solid line). A mismatch is visible at a glance and earns a decision-queue item |
| Flotilla torpedo attack | A dedicated radial verb with a preview of the attack run and expected spread before commit |
| Carrier strike prep | Each air group's state (hangar, readying, on deck, airborne) as a small status strip on the carrier's selection card, with the reason for any automatic launch |
| Battle setup reorganising divisions | Allowed, but announced: a pre-battle summary lists any change to your divisions and why |

### CMO: the model to keep

- **Missions are intent.** You draw reference points and assign units to [patrol or strike missions](https://steamcommunity.com/app/321410/discussions/0/1743355067119260455) rather than steering them; this is the most complete intent system in the genre.
- **Doctrine, ROE, EMCON and weapon release rules** are explicit and layered. Players note units follow doctrine to the letter, so a careless setting can have pilots [fire every long-range missile at max range](https://steamcommunity.com/app/1076160/discussions/0/6678353721812081358). The same thread shows the split audience: some want units to obey slavishly, others want judgement.
- **CMO's own redesign moved toward the map:** a [hover info databox, coloured datablocks and minimaps](https://store.steampowered.com/app/1076160/Command:_Modern_Operations/). Even the most table-driven game in the genre found information belongs on the plot.

### CMO: where the UI fails it

- **The UI is a cliff.** Long-time players call it [overloaded and cluttered, with direct control almost impossible](https://forum.quartertothree.com/t/command-modern-air-naval-operations-the-harpoon-that-never-was/75669?page=17).
- **Intent lives in forms, not on the map.** Missions are built from reference points plus a separate editor window, so the plan and the battlespace are in two places.
- **Doctrine depth without visibility.** Layered settings are powerful, but the player can't see at a glance which setting a unit is actually using or where it came from.

### Translating CMO's model

| CMO concept | Our treatment |
| --- | --- |
| Reference points + mission editor | Draw the task area directly: drag a box or polygon, choose the task from the radial menu, assign by dragging groups onto it |
| Layered doctrine | The doctrine card shows inherited values greyed and overrides bold, with "reset to fleet"; the map can tint ships by any doctrine setting on demand |
| Weapon release rules | Three presets (Conserve, Standard, Saturate) with the full table behind an Advanced toggle |
| Unit does something unexpected | Reason tag on the symbol and a one-line "why" in the event log, tied to the doctrine rule that caused it |

### The test for our design

Play an RTW battle and note every time you open a dialog, read a table, or wonder what a division is doing. Each one should map to a gesture, an on-map mark, or a reason tag in our UI. If we keep RTW's admiral's-eye limits and CMO's intent model, and lose their forms, we have the game.

## Context-sensitive commands

Context sensitivity works when the default is predictable from what's under the cursor and the cursor tells you the default *before* you click. It fails when the game guesses invisibly.

### The smart default (one verb)

Right-click (or release of a right-drag) resolves to one action from a small, fixed table. The cursor icon and a short label change on hover so the player always knows which action will fire.

| Selection | Cursor over | Default action | Cursor label |
| --- | --- | --- | --- |
| Ship or group | Empty space | Move (keep formation) | MOVE · ETA 4:20 |
| Ship or group | Known hostile | Engage per current ROE | ENGAGE · in range 1:10 |
| Ship or group | Uncertain contact | Close and identify | INVESTIGATE |
| Ship or group | Friendly ship | Escort / screen | ESCORT |
| Ship or group | Friendly fleet | Join fleet | JOIN |
| Ship or group | Objective / station | Task (defend, capture, resupply) | DEFEND |
| Fleet | Own flagship | Rally on flagship | RALLY |

Rules that keep this honest:

- **Never more than one smart verb per target type.** If two actions are equally plausible, it isn't a default; push it to the radial menu.
- **Hold-to-reveal beats guess-and-hope.** Holding a key (Total War uses space) draws every ship's current destination and task line; show it while the player is aiming an order too.
- **Drag adds parameters, never changes the verb.** Right-drag on space sets final heading and frontage (the Total War gesture); right-drag on a hostile sets standoff range; right-drag on an escortee sets screen radius.

### The radial marking menu (every other verb)

Holding the command button opens a radial menu at the cursor; flicking in a direction without waiting selects that item without the menu ever drawing. Kurtenbach and Buxton found that once learned, [flick marks averaged 3.5× faster than menu selection](https://www.research.autodesk.com/publications/user-learning-and-performance-with-marking-menus/), and that experts still drop back to the visible menu to refresh their memory, so both paths must stay. The design principles from [Kurtenbach's thesis](https://www-pt.autodesk.com/research/publications/the-design-and-evaluation): the menu reveals what's available, guides the hand, and every novice use rehearses the expert gesture.

- 8 slots max, with the same verb always in the same direction (Engage always north, Move/Hold always south, etc.) so direction becomes muscle memory.
- Slots that don't apply are **greyed, not removed**, with a reason on hover ("No torpedoes remaining"); removing them breaks muscle memory.
- One level of nesting at most (e.g. Engage → weapons-free / weapons-tight / specific battery).
- Mirror every slot to a hotkey for keyboard players.

### Modifiers: keep to three

- **Shift = queue** (append to the order list). Universal across RTS; don't fight it.
- **Alt = this ship only**, overriding fleet/formation behaviour for one order.
- **Ctrl = preview without committing** (hold to see the consequence; release to cancel).

Anything beyond three modifiers belongs in the radial menu or a panel.

### Orders as editable objects

An order is a thing on the map, not a fire-and-forget event. Waypoints, standoff rings and escort radii have drag handles (as in [Frozen Synapse's plan editing](https://blog.playstation.com/?p=124418)); right-click on an order line opens its own small menu (delete, insert waypoint, change speed). This removes most of the need for an order-queue panel.

## Command hierarchy and delegation

Build the UI around how real fleets are commanded: the commander states intent, subordinates decide execution. US Army doctrine frames this as [mission command](https://www.army.mil/article/225414/): empowering subordinate decisions and decentralising execution as the situation demands, with [commander's intent](https://www.army.mil/article/215297/mission_command_requires_sharp_commanders_intent) defined by task, purpose and end state. For a game, that maps onto four layers.

**Command layers: the player sets intent at each level; subordinates own the how.**

| Level | The player decides | Subordinates decide |
| --- | --- | --- |
| **Fleet** *(default play space)* | Objective, doctrine, ROE (e.g. hold the strait, weapons tight) | Fleet AI: formation, screens, who covers whom, spacing |
| **Task group** *(default play space)* | Task and area: screen, strike, escort, patrol | Group lead: assigns ships, routes, sectors, timing |
| Ship *(opt-in drill-down)* | Optional direct order: move, engage, stance, facing | Captain AI: maneuver, targeting, evasion, range keeping |
| Departments *(opt-in drill-down)* | Priorities only (e.g. repair order, EMCON level) | Crew: repairs, EW, sensor tasking |

The player can reach every layer, but the default zoom, selection and order set all live at the top two. Dropping to ship or department level is a deliberate drill-down, not the baseline.

### Design rules

- **Orders carry a purpose, not just a destination.** "Screen the carrier against air threats from the north" lets the group lead re-position sensibly when the threat moves; "go to X" does not. Starsector's dev reached the same conclusion: [tell ships *what*, let them work out *how*](https://fractalsoftworks.com/tag/rts).
- **Doctrine is set once, overridden rarely.** A per-fleet doctrine card (ROE, engagement range, retreat threshold, EMCON, ammo conservation) defaults every order beneath it. Ship-level overrides show a small badge on the ship's symbol so the exception is visible.
- **Intent and direct control must coexist.** Starsector's assignment-only version frustrated players who clicked a ship expecting to order it. Selecting your own ship must always offer direct orders; intent tasks sit alongside them.
- **Subordinates explain themselves.** When a ship deviates from the literal order (breaking off to evade, holding fire under ROE), its symbol shows a short reason tag ("EVADING", "HOLD: ROE"). This answers the Starsector problem of ships appearing to ignore orders.
- **Command friction is optional design, not UI.** There are no command limits; a comms delay is the planned friction. The UI's job is to make it visible (order in transit, sent vs executing) rather than to hide functions.

## Visual language

Give each visual channel exactly one job, so the player can read a contact's state without reading text. Real-world military symbology already solved most of this; borrow its logic, not its exact glyphs.

### Symbology: borrow the grammar of APP-6 / MIL-STD-2525

In NATO symbology the [frame shape and colour both encode affiliation](https://en.wikipedia.org/wiki/NATO_Joint_Military_Symbology): blue friend, red hostile, green neutral, yellow unknown. The redundancy is deliberate; it lets the symbol survive [less-than-ideal displays such as monochrome red night lighting](https://military-history.fandom.com/wiki/NATO_Military_Symbols_for_Land_Based_Systems). Planned or anticipated items use a [dashed frame](https://worldwind.arc.nasa.gov/java/tutorials/adding-new-tactical-symbols). That gives a ready-made grammar for a sensor-driven game.

**Proposed contact symbology: shape, colour and line style each carry one fact about a contact.**

| Affiliation | Frame | Colour |
| --- | --- | --- |
| Friend | Circle | Blue |
| Neutral | Square | Green |
| Unknown | Quatrefoil | Yellow |
| Hostile | Diamond | Red |

| Certainty stage | Drawn as | Meaning |
| --- | --- | --- |
| Sensor return | Dot inside a dotted ring | Position only |
| Suspect | Dashed frame | Type inferred |
| Identified | Solid frame | Class confirmed |
| Stale | Solid frame, faded | Fades with time unseen |

- **Shape + colour = affiliation.** Redundant on purpose: red-green colour blindness affects [roughly 8% of men](https://www.pcc.edu/instructional-support/accessibility/color/), so hue must never be the only cue.
- **Line style = certainty.** Dotted for a raw return, dashed for suspect or inferred, solid for confirmed. Players learn to distrust dashed lines without a tutorial.
- **Opacity = age.** A contact not refreshed fades toward a "last known" ghost with a timer, the way Frozen Synapse shows [ghosts at last-known enemy positions](https://giantbomb.com/wiki/Games/Frozen_Synapse).
- **Inner icon = class or role** (carrier, escort, picket, auxiliary). Keep it to one silhouette per role, readable at 24 px.
- **Small modifiers at fixed positions** around the frame: damage state bottom-left, ROE override top-right, order-in-transit bottom-right. Same slot, same meaning, every time.

### Lines and fields on the map

- **Velocity leader:** a line from each symbol showing heading and projected position in N minutes. This is the single most information-dense mark on a naval plot.
- **Order lines in the owner's colour,** solid for current leg, dotted for queued. Hostile intent lines (when known) in red, like Into the Breach's telegraphing.
- **Range rings on demand, not always.** Show weapon and sensor envelopes for the selection and for whatever is hovered; show all of them only while a key is held. Always-on rings for 30 ships are noise.
- **Threat shading, not threat rings,** at fleet zoom: a soft heat layer showing where enemy weapons can reach collapses dozens of rings into one read.

### Level of detail by zoom

Use a continuous strategic zoom, as Supreme Commander did, where [models give way to icons as you pull back](https://www.matchstickeyes.com/tag/strategy/). Define three explicit bands:

| Zoom band | What a ship looks like | What's shown | Primary actions |
| --- | --- | --- | --- |
| Ship | Hull art, turrets, damage decals | Arcs, subsystems, crew status | Facing, weapon targets, repair priorities |
| Group | Symbol + small hull silhouette | Velocity leaders, order lines, weapon ranges for selection | Move, engage, formation |
| Fleet | Aggregated group symbols with counts | Task areas, threat shading, objectives | Tasks, doctrine, reassign groups |

At fleet zoom, groups collapse into one symbol with a count, as Planetary Annihilation players [asked for](https://forums.planetaryannihilation.com/posts/719229); the group symbol's frame shows the worst state in the group so a damaged ship isn't hidden.

### Restraint

The US Navy's Aegis displays are often cited for [choosing a plain 2D plan view over 3D](https://www.besthub.dev/tags/aegis); a top-down 2D game already has that advantage. Keep chrome flat and quiet, reserve saturated colour for affiliation and alerts, and let the map dominate. Diegetic styling (Highfleet's CRT instruments) can carry the fantasy, but only where it doesn't slow a decision.

## Sensor uncertainty

The map should show **the plot, not the truth**: what your fleet believes, built from reports, with the doubt drawn in the shape the sensors actually produce. This was the job of the wartime Combat Information Center, where radarmen [plotted a series of ranges and bearings over time to work out a contact's course and speed](https://ussslater.org/cic), and kept a constant picture as [the best way to separate friend from enemy](https://usskidd.com/ship-tour/combat-information-center). The player is, in effect, reading that plot.

### Principle 1: doubt has a shape

A generic circle of uncertainty throws away the most useful information: *which* dimension is uncertain. Each sensor type should leave its own geometry, so a glance tells the player what kind of report they're looking at and what would resolve it.

**Position uncertainty by sensor: each sensor leaves a different shape of doubt.**

| Sensor | Shape on the plot | Knows well | Knows poorly |
| --- | --- | --- | --- |
| Bearing only (hydrophone, radio DF) | Narrow wedge from own ship, no point | Bearing | Range |
| Early radar | Short arc band around a best-estimate dot | Range | Bearing |
| Visual sighting | Long thin ellipse along the line of sight | Bearing | Range |
| Cross-fix | Two bearing wedges intersecting; small area at the crossing | Position | — |

This creates gameplay as well as clarity. A hydrophone or DF bearing tells you a line, not a point; a second ship's bearing turns two lines into a fix. Early radar gives range you can trust and bearing you can't. A lookout's report is the reverse. The player learns to position pickets to *shape* the plot.

### Principle 2: avoid the hurricane-cone trap

The National Hurricane Center's cone is the best-studied uncertainty graphic in public use, and it is widely misread. People read the cone's area as [the region the storm will hit](https://www.behance.net/gallery/52300291/Uncertainty-Visualizations-of-Hurricane-Forecasts), and read its widening as [the storm growing larger and stronger](https://www.preventionweb.net/quick/25486). NOAA's literature review traces part of the problem to [the cone's hard border](https://www.aoml.noaa.gov/general/lib/lib1/nhclib/Bibliographies/Cone%20of%20Uncertainty%20Literature%20Review_4_10_19%20%281%29%20%281%29.pdf), which reads as safe outside, certain inside. For us:

- **Soft edges, not hard outlines,** for probability areas. A hard-edged ellipse looks like a ship-sized object or a safe boundary.
- **A best-estimate point inside every area,** so the player has something to aim at and the area reads as doubt about that point.
- **Projected tracks as a fan of a few faint lines** (an ensemble) rather than a single widening cone. A fan reads as "one of these"; a cone reads as "all of this".
- **Never let uncertainty size look like ship size or threat size.** Threat is carried by the frame and class icon, never by the blob.

### Principle 3: separate the kinds of doubt

A contact can be uncertain in five independent ways. Give each one its own channel so they never blur together:

| Doubt | Channel | Example |
| --- | --- | --- |
| Where it is | Shape and size of the soft area | Radar arc band, bearing wedge |
| Where it's going | Velocity leader: single line once several fixes agree, faint fan before | Two fixes: wide fan. Six fixes: one firm leader |
| What it is | Frame line style (dotted, dashed, solid) and class icon with a ? until confirmed | "Large ship?" vs "Battleship, Kongo class" |
| How many | Count badge with qualifier | "2+" when radar returns may be merging close-spaced ships |
| How old | Opacity fade plus last-seen time | Ghost at 40% with "6 min" |

### Principle 4: show where the knowledge came from

- **Source glyphs** on the symbol: eye (visual), radar, ear (hydrophone/sonar), antenna (DF), plane (air report), message (relayed by another ship). Several glyphs mean several sensors agree.
- **Hover opens the report trail:** the individual reports behind the track, each with source, time and reporter ("Radar, *Danae*, 21:14, 18,200 yd, bearing 045").
- **Track history as plotted fixes:** a trail of small dots, like a grease-pencil plot. Regular, tight dots mean a solid track; scattered dots mean the course estimate is shaky.

### Principle 5: every ship has its own picture

With a comms delay, the flagship's plot is not the same as the destroyer's. This matters to a hardcore player because it explains behaviour: a ship didn't fire because *it* never held the contact.

- **The default view is the flagship's plot,** with relayed contacts appearing after their delay and marked as relayed.
- **A "ship's eye" toggle** shows the plot as any selected ship holds it, including contacts it has that the flagship doesn't yet.
- **Correlation is a player tool.** Two ships reporting the same enemy may create two tracks. The plot can auto-merge with a visible "merged" mark, and the player can merge or split tracks by hand, just as a CIC officer would.
- **False and ghost contacts exist** (radar clutter, misidentified aircraft reports) and look identical to real ones at first. Only more reports resolve them; the UI must never cheat by fading false contacts faster.

### Principle 6: radar changes the map, so the UI should change with it

Radar's arrival should be felt as a shift in the plot, not just a longer detection range.

- **Before radar:** the plot is mostly visual. Coverage overlays show visibility and weather limits per ship; at night or in fog the map is genuinely dark, with contacts appearing only at short range.
- **With radar:** the coverage overlay adds radar horizon and detection range for given target sizes. Contacts now appear far beyond visual range, but as range-good, bearing-coarse arcs with no identity, which is precisely the new tactical problem radar created.
- **Emissions cut both ways.** When your radar or radio transmits, show your own emission footprint, so the player sees the DF and radar-detector risk they're accepting. EMCON settings sit on the doctrine card.
- **Identification is its own step.** Radar finds; something else identifies. Keep "detected" and "identified" visibly separate on every contact for the whole game, even once identification aids arrive.

### Principle 7: decisions should price in uncertainty

- **Fire previews show track quality:** engaging a radar-only, two-fix contact shows a lower expected hit rate and the reason ("Bearing ±3°, course unknown").
- **The player can declare their own judgement:** mark a contact *assumed hostile* or *assumed friendly* with a distinct player-marking style, separate from what sensors confirm. APP-6 already distinguishes suspect from confirmed hostile; our UI should distinguish the commander's call from the evidence.

All sensor characteristics above are design placeholders. The visuals should mirror whatever the simulation actually models, and nothing more precise than it.

## Time, information and attention

The scarce resource is the player's attention, not screen space. The UI should spend it only on decisions.

### Time control

- **Pause-and-order is the core loop,** not an accessibility option. Every order type, including radial menus and drag handles, must work while paused.
- **Preview while paused.** Borrow Frozen Synapse's loop: [simulate the plan before committing, including guessed enemy moves](https://en.wikipedia.org/wiki/Frozen_Synapse). For us that means a scrub-ahead ghost showing where every ship will be in 30 s / 2 min / 5 min if nothing changes, with predicted weapon exchanges marked.
- **Auto-pause rules** the player configures (new contact identified, ship below 50% integrity, weapons release, order complete). Default to a small, conservative set.
- **Time compression shows its own state** clearly; a player should never wonder whether the game is paused.

### Alerts: decisions, not events

Split the information stream in two:

| Stream | Content | Presentation |
| --- | --- | --- |
| Decision queue | Things that need the player: contact identified, ship asking to retreat, ammo low, objective threatened | A short stack (≤ 5) at screen edge; each item jumps the camera and pre-selects the relevant ship; dismiss or act |
| Event log | Everything that happened: hits, kills, launches, sensor changes | Scrollable, filterable, timestamped; each line clickable to jump camera and time-scrub |

The event log answers the HOI4 complaint that players [can't see who shot whom](https://steamcommunity.com/workshop/filedetails/discussion/1687056790/1850323802578952897). Add a post-battle replay with the same filters.

### No hidden modes

The USS John S. McCain collision is the clearest real-world lesson in command UI. Steering control was [unintentionally transferred between stations](https://www.rivieramm.com/news-content-hub/touchscreens-pinpointed-as-the-cause-of-the-collision-between-warship-uss-john-s-mccain-and-chemical-tanker-alnic-mc-55904), and the throttles were [unganged without the helmsman realising one shaft was turning much faster](https://taskandpurpose.com/news/navy-mechanical-throttles-destroyers/). The NTSB found the touchscreen system's design [raised the likelihood of the operator errors](https://features.propublica.org/navy-uss-mccain-crash/navy-installed-touch-screen-steering-ten-sailors-paid-with-their-lives) behind the collision, and the Navy is retrofitting physical throttles. Game translations:

- **Control state is always visible on the ship symbol:** player-direct vs AI task vs doctrine default, weapons free / tight / hold.
- **Nothing transfers control silently.** If a group order overrides a ship's direct order, say so on the ship.
- **Linked vs independent must be obvious.** If ships in a formation can be unlinked, show the link.

### Learnability

- **Progressive disclosure:** early missions expose only fleet and group layers; ship and department controls unlock as scenarios need them.
- **Every hover teaches.** Tooltips on any symbol modifier state its meaning in plain words ("ROE override: weapons hold").
- **One way to learn, many ways to do.** Every action reachable by visible UI (button or radial menu) has a hotkey, and the tooltip shows it, so novices rehearse the expert path.

## Proposal for our game

A concrete starting spec, meant to be prototyped and broken in playtests rather than adopted whole.

### Order lifecycle

Every order is aimed, shaped and previewed before it exists:

1. **Select** — ship, group or fleet; zoom sets the level.
2. **Aim** — the cursor names the verb; hold for the radial menu.
3. **Shape and preview** — drag sets heading and range; Ctrl previews the outcome. *(This is the key step.)*
4. **Commit** — release to issue; Shift queues, Esc cancels.
5. **Order on map** — drag handles on the order line; click it for options. Editing loops back to step 3.
6. **Execute, report** — the AI captain executes and tags any deviation.

Step 3 is where this design differs most from genre norms: nothing is committed until the player has seen its shape and, on request, its outcome. Once issued, an order stays editable on the map instead of disappearing into a queue panel.

### Default input map (mouse + keyboard)

| Input | Action |
| --- | --- |
| Left-click / box-drag | Select (ship, group, or fleet depending on zoom band) |
| Double-click | Select the whole group or fleet the ship belongs to |
| Right-click | Smart default verb (see the context table above) |
| Right-drag | Same verb + shape it (heading, frontage, standoff, screen radius) |
| Hold right button | Radial marking menu; flick to select without opening |
| Shift + order | Queue |
| Alt + order | This ship only, overriding group behaviour |
| Ctrl (hold) | Preview outcome; release to cancel |
| Space | Pause / resume |
| Tab (hold) | Show all order lines, ranges and destinations |
| Mouse wheel | Continuous zoom through ship → group → fleet |

### Screen layout

- **Map: 85%+ of the screen,** always the primary surface.
- **Top bar:** time state and controls, mission clock, decision queue count.
- **Left edge:** fleet tree (fleet → groups → ships) that mirrors the map selection; drag ships between groups here.
- **Bottom-right, contextual:** a selection card with doctrine, stance and key stats for whatever is selected. It shrinks to a strip at fleet zoom and expands at ship zoom.
- **Right edge:** decision queue (≤ 5 items) above a collapsible event log.
- **Nothing modal during combat** except the radial menu, which closes on release.

### Prototype and test plan

1. **Paper / grey-box the symbology** (frames, certainty, modifiers) and run a 5-second read test: show a cluttered plot, ask players which contact is most dangerous and how sure they are.
2. **Build the smart verb + radial menu** on a single scenario with 6 ships; measure orders per minute and mis-issued orders.
3. **Add intent tasks and doctrine;** compare battle outcomes and self-reported workload with and without them.
4. **Test with colour-blind simulation** on every build; no information may depend on hue alone.
5. **Watch for "why is it doing that?"** moments in playtests; each one is a missing reason tag or a hidden mode.

### Open questions

- [x] **Command limits:** none. A comms delay is likely, so the sent-vs-executing order lines and the order-in-transit modifier are core features, not polish. Since nothing rations orders, the UI itself must make delegation the efficient path (intent tasks, doctrine, group orders).
- [ ] **Multiplayer:** open. Pause-and-order works for single player; multiplayer would need simultaneous planning turns, a shared slow clock, or vote-pause. Decide before the time-control UI is locked.
- [x] **Gamepad:** not supported. Mouse and keyboard only, so dense hotkeys, modifier gestures and hover-heavy tooltips are fine.
- [x] **Audience:** hardcore naval players who find HOI4's many shallow systems too simple. Progressive disclosure is about *ordering* depth, not hiding it: every system stays reachable from the first battle, and tutorials teach the reading of the plot rather than simplifying it.
- [x] **Sensor uncertainty:** a central system, especially once radar arrives. See the Sensor uncertainty section.

## Sources

Game design

- [Fractal Softworks: Revisiting the Command UI and Fleet Control (Starsector dev blog)](https://fractalsoftworks.com/tag/rts)
- [Nebulous: Fleet Command Steam discussion on fleet command vs micro](https://steamcommunity.com/app/887570/discussions/0/3779119905187883230)
- [Sea Power review summary (Vaporlens)](https://vaporlens.app/app/1286220/sea_power_naval_combat_in_the_missile_age)
- [Command: Modern Air/Naval Operations thread, Quarter to Three](https://forum.quartertothree.com/t/command-modern-air-naval-operations-the-harpoon-that-never-was/75669?page=17)
- [EVE Online: moving fleet interface out of right-click menu hell](https://forums-archive.eveonline.com/message/5733941/)
- [HOI4 naval feedback discussion](https://steamcommunity.com/workshop/filedetails/discussion/1687056790/1850323802578952897)
- [Supreme Commander (Wikipedia)](<https://en.wikipedia.org/wiki/Supreme_Commander_(video_game)>) · [Strategic zoom in RTSes](https://www.matchstickeyes.com/tag/strategy/) · [Planetary Annihilation strategic icons thread](https://forums.planetaryannihilation.com/posts/719229) · [Revisiting Supreme Commander (Stardock)](https://forums.wincustomize.com/469761)
- [Total War: Rome II battle controls](https://academy.totalwar.com/total-war-rome2-battle-controls/) · [Shogun 2 control thread](https://steamcommunity.com/app/34330/discussions/0/3039355280230136766)
- [Frozen Synapse (Wikipedia)](https://en.wikipedia.org/wiki/Frozen_Synapse) · [Giant Bomb](https://giantbomb.com/wiki/Games/Frozen_Synapse) · [PlayStation Blog on Frozen Synapse Tactics](https://blog.playstation.com/?p=124418)
- ['Into the Breach' Design Postmortem, GDC 2019](https://gdcvault.com/play/1025772/-Into-the-Breach-Design)
- [HighFleet preview, PC Gamer](https://www.pcgamer.com/au/unique-action-strategy-game-highfleet-is-coming-to-early-access-this-month) · [Carrier Command 2: The Command Interface](https://www.speedrun.com/Carrier_Command_2/news/r65gklxv)

Rule the Waves and CMO

- [Rule the Waves 3 review, Round Table Co-op](https://roundtablecoop.com/?p=5073) · [Naval Gazing review](https://www.navalgazing.net/Review-Rule-the-Waves-3) · [RTW3 comparison document (Slitherine)](https://www.slitherine.com/news/rule-the-waves-3-comparison-document) · [RTW3 review summary (Vaporlens)](https://vaporlens.app/app/2008100/rule_the_waves_3.md)
- RTW3 Steam threads: [division roles](https://steamcommunity.com/app/2008100/discussions/0/3826416272370372562) · [carrier recon and strikes](https://steamcommunity.com/app/2008100/discussions/0/4362373511166232156) · [battle generator and divisions](https://steamcommunity.com/app/2008100/discussions/0/508455105153036788) · [update 1.00.40 discussion](https://steamcommunity.com/app/2008100/discussions/0/4293692116962694166)
- [Command: Modern Operations Steam page](https://store.steampowered.com/app/1076160/Command:_Modern_Operations/) · [CMO doctrine discussion](https://steamcommunity.com/app/1076160/discussions/0/6678353721812081358) · [CMANO missions and reference points](https://steamcommunity.com/app/321410/discussions/0/1743355067119260455)
- [Harwood's River Plate despatch, London Gazette 1947](https://thegazette.co.uk/London/issue/37989/supplement/2760/data.pdf) · [Battlefleet 1915 rules: divisional turn orders](https://www.wtj.com/games/beta/battlefleet_1915/docu_rules_a.htm)

Human factors and doctrine

- [Kurtenbach & Buxton, User learning and performance with marking menus (CHI 1994)](https://www.research.autodesk.com/publications/user-learning-and-performance-with-marking-menus/) · [Kurtenbach, The Design and Evaluation of Marking Menus (1993)](https://www-pt.autodesk.com/research/publications/the-design-and-evaluation)
- [NATO Joint Military Symbology (Wikipedia)](https://en.wikipedia.org/wiki/NATO_Joint_Military_Symbology) · [NATO symbols for land-based systems](https://military-history.fandom.com/wiki/NATO_Military_Symbols_for_Land_Based_Systems) · [NASA WorldWind tactical symbols](https://worldwind.arc.nasa.gov/java/tutorials/adding-new-tactical-symbols)
- [Aegis UI: 2D over 3D](https://www.besthub.dev/tags/aegis)
- [ProPublica: USS McCain touchscreen steering](https://features.propublica.org/navy-uss-mccain-crash/navy-installed-touch-screen-steering-ten-sailors-paid-with-their-lives) · [Riviera: NTSB findings](https://www.rivieramm.com/news-content-hub/touchscreens-pinpointed-as-the-cause-of-the-collision-between-warship-uss-john-s-mccain-and-chemical-tanker-alnic-mc-55904) · [Task & Purpose: physical throttles](https://taskandpurpose.com/news/navy-mechanical-throttles-destroyers/)
- [US Army: new mission command doctrine (ADP 6-0)](https://www.army.mil/article/225414/) · [Mission command requires sharp commander's intent](https://www.army.mil/article/215297/mission_command_requires_sharp_commanders_intent)
- [PCC accessibility: colour](https://www.pcc.edu/instructional-support/accessibility/color/)

Sensors and uncertainty

- [USS Slater: Combat Information Center](https://ussslater.org/cic) · [USS Kidd: Combat Information Center](https://usskidd.com/ship-tour/combat-information-center) · [Naval Gazing: Information, Communication and Naval Warfare Part 2](https://www.navalgazing.net/Information-Communication-and-Naval-Warfare-Part-2)
- [University of Utah study on hurricane forecast displays](https://www.preventionweb.net/quick/25486) · [NOAA cone of uncertainty literature review](https://www.aoml.noaa.gov/general/lib/lib1/nhclib/Bibliographies/Cone%20of%20Uncertainty%20Literature%20Review_4_10_19%20%281%29%20%281%29.pdf) · [Uncertainty visualizations of hurricane forecasts](https://www.behance.net/gallery/52300291/Uncertainty-Visualizations-of-Hurricane-Forecasts)