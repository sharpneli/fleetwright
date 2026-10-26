# Map and command UI mockup (2026-10-11)

The visual reference for the game's main screen: one globe, continuous zoom from orbit to a single ship, and the
screens (designer, research, yards...) pushed on top of it. The reasoning behind the command model is in
`docs/battle/command-ui.md`; the world it sits on (one sphere, the scene stack, the snapshot) is
`docs/world/architecture.md`. This adds the multi-scale part: what forces look like at each zoom, how your forces
near the camera stay at hand, and where everything else lives.

**The battler first:** the game that ships first is a scenario battler, and its HUD is planned in
`battle-hud.md` with its own mockup, `battle.html` (U0-U7), and its launch screen (scenario, side, standing orders,
conditions, your own designs, the ships building behind it) in `launch.md` with `launch.html`. This mockup stays the
reference for the world game.

**It is a mockup, not code to port.** Take the layout, the bands, the symbology and the interactions, not the HTML.
The scenario (31 May 1916, an action off Horns Reef) is illustrative.

## Open it

Open `map.html` in a browser; it works offline (it uses `../designer-ui/assets` for the fonts and the Dreadnought
sprite). `map-v1.html` is the first version, with fixed panels on three sides, kept for comparison. It runs: the clock ticks, ships move, salvoes fall, the 5th BS takes in its signal. `map.html#1` … `#8`
opens a camera view; `map.html#fleet` (or `designs`, `yards`, `research`, `intel`) opens a screen.

Try: wheel from the globe down to Lion; click a squadron and right-drag; hold the right button for the radial menu;
click an edge marker; V for a selection's details; U for all forces; ? for the controls; F2–F6 for the screens and
Esc back.

## The idea

One map, always the bottom of the stack. The zoom decides **what a click selects** and **how forces are drawn**, so
the same continuous wheel moves you from "where is the war going" to "is Q turret's magazine flooded". Everything
is a ship at a real place: zoomed out, ships are gathered into markers by where they fall on the screen, never into
abstract areas, so zooming in always splits a marker into the ships it was made of.

Three things keep it usable at world-war scale:

1. **The view sorts your forces.** The forces panel is one list grouped by the camera: *In view*, *Near*, and
   *Elsewhere* by theatre. Zoom in on a battle and the panel becomes that battle's order of battle, with the
   reinforcements next on the list; zoom out and it becomes the fleet list.
2. **Edge markers for what's just off screen.** Your groups (and known enemies) within reach of the view sit on the
   screen edge at their true bearing, with distance, course and strength. You can order them without zooming out.
3. **Strength is one glyph, learned once.** The same pips (capital ships, cruisers, a bar for light forces; hollow
   for "maybe") appear on the map, in the panel, on edge markers and in cards, from the globe to the tactical band.

## The HUD (v2, 2026-10-11)

The first version had panels on three sides at all times, in the designer's style. The map is the game, so v2 gives
it the whole screen, in the way Supreme Commander moved on from Total Annihilation's side bar: small floating pieces
that appear when they have something to say and get out of the way when they don't. Nothing is lost; most of it is
one key or one hover away.

| Piece | Where | Shows itself when | Behind it |
|---|---|---|---|
| Top strip | top, 34 px | always: nation, clock and speeds, the screens, money and fuel | — |
| Actions | top centre | an action is open: one chip each, pulsing | hover: live mini-plot, sides, losses; click: go there |
| Decisions | top right | something needs you: the newest with its buttons | "4 more" expands the list; Q jumps to the next |
| Lens + layers | top left | always, two small controls | the layer toggles in a drop-down |
| Forces rail | left | your forces in view first (in action on top), then the nearest few off screen with an arrow and distance | U: the full list (in view, near, elsewhere by theatre, contacts, allies) as a drawer |
| Selection strip | bottom centre | only when something is selected: name, state, the 4–6 numbers that matter, the verbs | V: the full card (armament, fire control, doctrine...) grows above it |
| Event ticker | bottom left | the last few events, fading out after 14 s | J: the whole log as a drawer |
| Minimap | bottom left | below the globe band (the globe is its own minimap) | hover: the saved views; M hides it |
| Scale rail | right edge | a thin track with the band and width | hover: the band names, click to jump; a banner names the band when you cross into it |
| Controls | `?` button, bottom right | never on its own | ? opens the cheat sheet |

What went: the permanent "view summary" card, the controls list, the always-on log and decision bodies, the forces
section headers, the screen tabs' key chips. Edge markers keep clear of whatever HUD is showing.

## Zoom bands

The wheel is continuous; the bands are where the drawing changes (with cross-fades), shown on the scale rail at the
right of the map. Band edges are in metres per pixel; widths are for the 1,240 px map area.

| Band | View across | A force looks like | Click selects | What else is shown |
|---|---|---|---|---|
| **Globe** | 6,000 km+ | Clusters by screen position: frame + capital-ship count + one line of totals | Fleet (or the cluster) | Sea-area balance cards, actions, night side, ports as marks only |
| **Theatre** | 1,000–6,000 km | Clusters, named by fleet, with strength pips and a 6-hour leader | Fleet | Ports with counts (⚓ in port, ▣ docked, ⚒ building), enemy port estimates, DF wedges |
| **Operations** | 50–1,000 km | One symbol per group (stacked when they overlap), ships as ticks, 30-min leaders | Group | Encounter rings, sighting ellipses, DF fixes, edge markers |
| **Tactical** | 4–50 km | Every ship as a hull to scale (with a minimum size), groups labelled at their leader | Ship (double-click: its group) | Fire lines, splashes, range rings for the selection, order lines, sent-vs-executing, wrecks, edge markers |
| **Ship** | under 4 km | The ship's sprite, wake, fires; docks and slips with what's in them | Ship | The same ship card, at full detail |

Rules that hold across bands:

- **Frame = affiliation, line = certainty** (`command-ui.md`): circle friend, diamond hostile, square neutral;
  dashed for suspect, faded with age. Allies are friends with a thinner frame and aren't commandable.
- **Fixed modifier slots** on group symbols: damage bottom-left, doctrine override (±) top-right, order in transit
  (◷) bottom-right.
- **Labels never pile up.** Each label tries right of its symbol, then left, above, below, and is dropped (still on
  hover) when nothing fits. Own fleets with big strength and the selection always get theirs.
- **Ports are places.** Zoomed in, a dockyard shows its docks, free or occupied (Rosyth: Australia in No.1, No.2 and
  No.3 free); a yard shows its slips with the hull being built drawn to its progress (Clydebank: Hood at 1%, Repulse
  fitting out).

## Strength at the large scale

- **Sea-area cards** (globe band): own + allied capital ships to the left, the enemy estimate to the right, solid for
  what's known and hatched up to the high estimate; cruisers and light forces underneath. Counts are exact for your
  side (ships whose positions are inside the area) and intelligence for the enemy.
- **Cluster markers** carry the capital-ship count in the frame and the strength line beside it; an amber dot means
  some of it is in action.
- **The forces panel** at globe and theatre zoom lists fleets with status (in action, n at sea, in port, in the yards).
- **"Strength in view"** in the card (nothing selected) sums what's on screen, own against known enemy, and names the
  unidentified contacts separately.

## Commanding

- **Smart verb at the cursor.** With your group or ship selected, the cursor names what a right-click will do:
  MOVE with course, distance and ETA; ENGAGE (red) over a known enemy; INVESTIGATE over a contact; JOIN/SCREEN over
  your own group; RETURN TO port.
- **Right-drag** shows the order before it exists (a dashed line to the cursor with the label); release commits,
  Shift queues. The order then lives on the map with its ETA.
- **Hold right** for the radial menu: eight verbs in fixed directions, greyed with a reason when they don't apply
  (a convoy can't Engage), flick to pick.
- **Edge markers take orders.** Right-drag from the Grand Fleet's marker to a point in the battle view orders it
  there without leaving the view.
- **Sent vs executing.** The 5th BS shows the order you sent (amber dashed, "sent 135°, in transit 99 s") and the one
  it's executing (its leader, still 150°) until the signal is taken in, then the log says so. This is something to
  see, never something to do: repeating a signal by searchlight is the flagship's job, not the player's.
- **Control state is always on the group:** DIRECT (your order), TASK (its task), DOCTRINE (doctrine default),
  ◷ SIGNAL (in transit). The doctrine card shows inherited values grey and overrides in bold.

## The selection card

In v2 this is the details view of the selection strip (V); the strip itself carries a short version. Content by what's selected:

| Selected | The card shows |
|---|---|
| Nothing | Strength in view, conditions at the centre (sun and sunset, visibility, signals' delays), the controls |
| Fleet / cluster | Its groups with strength, status, course and task |
| Group | Ships (flotation, water aboard, target, ammunition), orders (task, executing, sent, waypoints with ETAs), doctrine, verbs |
| Ship | Condition (top view with the damage marked, flotation, list, fires, crew), armament by mount (bearing, target, rounds, state), fire control (range, rate, plot quality, salvos, straddles, hit rate), ship-only orders and links to its design and register entry |
| Enemy ship or contact | What we hold (identified or suspect), the report trail, the estimate, and "mark: assumed hostile" kept apart from the evidence |
| Port | Ships there and what's being done to them, docks in use, stocks |
| Action | Sides, losses, its auto-pause rules, replay |
| Building ship | Yard, stage, progress, completion; links to the yard schedule and the design |

## Time and attention

- **The clock** shows asked and achieved speed: "asked 1h · held 12× by 3 actions in progress" (the world
  architecture's tiers of time, made visible).
- **Actions in progress** (right, top): one card per open encounter, each with a live mini-plot, so three battles on
  three oceans can be watched at once and jumped to. A world war has several at a time.
- **Decisions** (≤5): things that need you, each jumps the camera and preselects. **Log** below, filterable, every
  line naming a ship jumps to it.
- **No needless busywork.** A decision is a real choice with a consequence the player cares about: hold the line
  with a damaged flagship or haul out, scatter a convoy or keep formation, treat an unidentified contact as the High
  Seas Fleet. Anything the simulation models at a lower level is handled by the AI at that level and shown, not asked:
  repeating a signal that wasn't taken in, closing to identify a contact, routine damage control, re-coaling. News
  that needs no answer (a ship laid down, a signal taken in) goes to the log and the ticker, never the decision list.
  The test for every prompt: would a real admiral's staff have bothered him with it?
- **Screens don't stop the clock.** A strip at the bottom of every screen shows the open actions and waiting
  decisions; Esc or F1 returns to the map exactly as it was.

## Where the other screens live

A row of tabs in the top bar, F1–F10, each a scene pushed over the map (`docs/world/architecture.md` "Screens: a
stack, not tabs"). Map-shaped information stays on the map as **lenses** (Operations, Intel, Supply, Yards);
table-shaped information gets a **screen**, and the two link both ways.

| Key | Screen | What it is | Links |
|---|---|---|---|
| F1 | Map | The globe | — |
| F2 | Fleet | The register: every ship, where it is, condition, fuel, ammunition, crew, refit due, captain; group by fleet, theatre, type or status | ⌖ flies the map to the ship; Open design |
| F3 | Designs | Your designs, in service and building; the designer opens over this screen with a DesignContext from the world | Lay down → F4 |
| F4 | Yards | Slips and docks on a timeline (on the slip, launch, fitting out, in dock); slip and dock limits | Show on map; the designer's limit presets |
| F5 | Research | Fields with budget shares, current projects, a timeline of what's done and next, lessons from action | Ships that benefit → F2; templates in the designer |
| F6 | Intelligence | The enemy order of battle as you hold it, with source and confidence; Room 40 | The Intel lens and the Contacts tab |
| F7–F10 | Supply, Admiralty, Officers, Reports | Sketched only: stocks and colliers; money and politics; flag officers and their traits; action reports and replays | — |

Opening something from context is the common path: a building ship's card opens the yard schedule, a ship's card
opens its design, a log line about Hood opens F4.

## Uncertainty on the map

Each contact keeps the shape its source gives it (`command-ui.md` "Sensor uncertainty"):

- **DF bearings** from Hunstanton and Flamborough as wedges, crossing at a soft fix ("DK · DF fix ±15 nm · 22 min").
- **A lookout's report** as an ellipse long along the line of sight, with a fan of possible courses ("HEAVY SHIPS?
  6–16 · visual · Southampton").
- **Ports** with an estimate and its age, fading as it gets older (Pola "~6–7 cap · 2 d", Kiel "14 d").
- **Rumours** as large soft blobs (a raider somewhere in the Indian Ocean, 9 days old).
- The forces panel's **Contacts** tab lists all of it by distance from the view, and the Intel lens strengthens it.

## What the engine needs to give the UI

The snapshot (`docs/world/architecture.md`) would need, beyond positions:

1. **The player's picture apart from the truth**: contacts with their source, geometry (wedge, ellipse, blob),
   estimate ranges and age. The map never reads enemy ships the player doesn't hold.
2. **Group and fleet membership and composition** per ship, so clusters, pips and panel rows are cheap; strength
   totals per sea area (or the UI sums them).
3. **Orders as data**: waypoints, the order sent and the order executing, signal transit time, control state.
4. **Encounters** with their members, start time and the speed they hold the world at.
5. **Ports' docks and slips** as places with sizes and occupants, and build progress per hull.
6. **The sun's position** for the night side and "sets 20:12".

## Open questions

1. **Band edges.** Operations up to ~1,000 km across fits the North Sea in one view; is Theatre then needed as its
   own band, or does Operations go straight to Globe?
2. **Clusters across sides.** Today own, allied, enemy and neutral never cluster together. Should an action collapse
   into one "action" marker at globe zoom instead of two clusters and a ring?
3. **Edge-marker reach.** 3× the view width (100 km to 1,500 km). Too many at the ship band? Per-band limits?
4. **Screens and the clock.** Running (as mocked, with the strip) or paused by default; the designer probably pauses.
5. **Number keys.** Camera views here; RTS players expect group hotkeys (Ctrl+n assign, n select, n n go).
6. **Ports before the Earth model.** The mockup draws a dockyard apron; the real coastline and berths come with it.

## Screenshots (`png/`)

v2 (`map.html`):

| File | State |
|---|---|
| `hud-tactical.png` | The battle with nothing selected: forces rail, actions, one decision, ticker, minimap |
| `hud-ship-selected.png` | Lion selected: the selection strip |
| `hud-ship-details.png` | V: Lion's full card above the strip |
| `hud-order-drag.png` | 1st BCS: right-drag order preview, the group's strip |
| `hud-action-popover.png` | Hovering the Horns Reef action chip |
| `hud-decisions-open.png` | Decisions expanded |
| `hud-globe.png` | Globe: fleets in the rail, no minimap, sea-area cards |
| `hud-operations.png` | Operations band |
| `hud-forces-drawer.png` | U: the full forces list |
| `hud-layers.png` | Intel lens and the layers drop-down |
| `hud-ship-band.png` | Ship band with the band banner |
| `hud-help.png` | ?: controls |

v1 (`map-v1.html`):

| File | State |
|---|---|
| `globe.png` | Globe band: sea-area balance cards, fleet clusters, three actions, the night side |
| `theatre.png` | Theatre: Home waters, ports with counts, Grand Fleet and BCF converging, DF wedges |
| `theatre-supply-lens.png` | Supply lens, 4th BS selected: fuel endurance and radius of action, base stocks |
| `operations.png` | Operations: the North Sea, groups stacked by fleet, the encounter ring, contacts, edge markers |
| `operations-intel-lens.png` | Intel lens and the Contacts tab sorted by distance |
| `tactical.png` | Tactical: the action off Horns Reef, fire lines, splashes, edge markers for the Grand Fleet and the contacts |
| `tactical-lion-selected.png` | Lion selected: range rings and the full ship card |
| `tactical-order-drag.png` | 1st BCS selected, right-drag: the order previewed with course, distance and ETA |
| `tactical-radial.png` | Holding right: the radial menu, Shadow greyed for capital ships |
| `tactical-smart-verb.png` | Hovering Derfflinger with 1st BCS selected: ENGAGE · 14 km, and the contact tooltip |
| `ship-lion.png` | Ship band: Lion's sprite, wake and fire, edge markers for the rest of the battle |
| `ship-rosyth-docks.png` | Rosyth: Australia in No.1 Dock, No.2 and No.3 free |
| `ship-clydebank-slips.png` | Clydebank: Hood laid down today, Repulse fitting out |
| `screen-fleet.png` | F2 Fleet register |
| `screen-designs.png` | F3 Designs, with the in-game designer as it opens over it |
| `screen-yards.png` | F4 Yards and docks timeline |
| `screen-research.png` | F5 Research: shares, projects, timeline, lessons from action |
| `screen-intel.png` | F6 Intelligence: the enemy order of battle as held |

## Files

- `map.html`: v2, the floating HUD. `assets/hud.js` builds it; `assets/hud_base.js` holds the cards, forces list
  and tooltips it shares with v1.
- `map-v1.html` + `assets/ui.js`: v1, fixed panels.
- `assets/map.js`: camera on the sphere, world state, drawing by band, input.
- `assets/ui.js`: top bar, forces panel, locator, actions, decisions, log, cards, tooltips.
- `assets/screens.js`: the pushed screens.
- `assets/data.js`: the scenario.
- `assets/land.js`: Natural Earth land (public domain) at 1:110M and 1:50M, and 1:10M around the North Sea, via
  `world-atlas`; `assets/d3-*.min.js`: d3-geo for the projection (ISC licence).

## Design tokens

The gunmetal-natural skin (`docs/ui-themes/ui-style-gunmetal-natural.md`, applied 2026-10-26): ground `#0F0F0E`, panels
`#1B1B19`, the accent `#E8772E`, amber `#E8B64A`; friend cream `#F1ECDD`, hostile `#E04A3C`; sea `#2E5269`, land
`#A3AD86`. Fonts in the game: Saira Extra Condensed (title strips), Barlow Condensed and Red Hat Mono; these older
mockups still show the slate-and-brass look and Archivo Narrow (`launch-bureau.html` is current). Fixed at 1920×1080, scaled to the window. The UI's chrome, its layout and its pieces: `docs/ui-language.md`.
