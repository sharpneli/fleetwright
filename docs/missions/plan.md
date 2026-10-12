# Plan: missions, the first shippable game

2026-10-11. Status: the direction is agreed with the user ("Decisions" at the end); the work goes in small steps,
each planned before it's built ("Stages"). Since 2026-10-12, missions are part of the battle module
(`docs/battle/plan.md`), and that plan's step order is the one to follow.

## Goal

The first thing that's complete and shippable: **missions**. Fixed forces, set at the mission's launch, in a limited
area, with objectives. "*Bismarck* has been reported in the Denmark Strait: find her and sink her before she reaches
Brest." Once one plays well, a generator makes many, and that's a game.

A mission is **a small world, not a separate mode**: the same `Fleetwright.Sim`, the same clock and tiers, the same
map, pictures and encounters as the whole planet later (`docs/world/architecture.md`). Nothing built for missions is
thrown away when the campaign comes; the campaign is a bigger world with more systems switched on.

**Missions are the battle module's stand-in for the campaign** (the user, 2026-10-12). The battle module is the final
game's whole ship simulation on the world map. What it doesn't have is the political system that decides who fights,
and the manufacturing that decides what each side has and where. A mission does both jobs: its file and its UI place
the forces, set their orders and objectives, and judge the result. So the mission layer is the only part of the
battle module the final game replaces, and missions are also the first shippable game.

## What "find Bismarck" asks of the sim

The Bismarck chase ran 24-27 May 1941 over ~1,500 nm. A mission like it needs:

- **Days of game time:** the search is hours or days on rails at high compression; the fight at the end is Full.
  So the time tiers, the contact scan and the speed drops are in the first version, not later.
- **Not knowing where the enemy is.** The mission is the search: the player's navy has a picture (a report, its age,
  its uncertainty growing), and plans from that. Pictures are in the first version.
- **An enemy that wants something:** Bismarck runs for Brest, evades, doubles back. The enemy side is a captain AI
  with orders (a destination, evade contact, fight if cornered), not a target that waits.
- **Reports that arrive:** shadowing cruisers' signals, an aircraft's sighting, a D/F bearing. Until aircraft are in
  the sim, a mission's **scripted reports** stand in for them (at a time, or on a condition, with an error).
- **A fight worth having at the end:** the fine clock tier at Full, the damage model, flooding, a jammed rudder.
- **An ending:** objectives met or failed, a time limit (Luftwaffe cover, the fuel of the hunters).

## A mission, as data

A mission is a file (JSON, `Content/Missions/*.json`, a game asset), everything a world needs to start:

- **When and where:** the start time (UTC; sets the sun, the moon, night), the weather and sea state (fixed or a
  scripted sequence), the **area** (a polygon on the sphere; the map's camera and every order keep inside it), and
  the land in it (none until the Earth model).
- **Sides:** each nation taking part, which is the player's, relations (at war), each side's victory conditions, and
  its technology (a year preset or a list).
- **Forces:** groups of ships: the design (a shipped design by name, or a design embedded in the file, with its
  content hash), the ship's name, position, course, speed, fuel and ammunition, state of readiness; the group's
  standing orders; the player's groups marked important or not.
- **What each side knows at the start:** its picture (e.g. "a battleship and a cruiser, 63°N 30°W, course 220,
  28 knots, as of 05:00, ±10 nm").
- **The enemy's plan:** the AI's orders for its groups (go to Brest by this route, evade, rendezvous with a tanker).
- **Events:** scripted reports, reinforcements entering the area, weather changes; at a time or on a condition.
- **Objectives**, from a small typed set (below), each with its side, its weight and whether it ends the mission.
- **Seeds:** one per mission for its random streams, so a mission plays the same for the same orders (and a
  replay is the file plus the command log).

### Objectives: a small typed set

Each is a condition on world state, checked on the events that can change it (a sinking, an entry into an area, a
time), never polled per tick:

- **Destroy** an entity, a group, a class, or x tonnes of a side ("sink *Bismarck*").
- **Protect:** keep an entity or group afloat (or above a damage level).
- **Reach / deny an area:** get there (the enemy reaching Brest's air cover fails the player), or keep out of one.
- **Find / shadow:** have the entity in the side's picture with an accuracy for a time ("maintain contact for 6
  hours"): an objective on knowledge, which the picture makes possible.
- **Hold until:** survive or keep a state until a time.
- **End conditions:** a time limit, all objectives of a side decided, a side gone, or a side out of the area.

The result is graded by the weighted objectives (decisive, marginal, defeat) and goes on a debrief: what happened,
from the event log and the pictures (what each side knew, when).

## Screens

On the scene stack (`docs/world/architecture.md` "Screens"):

- **Mission select:** the shipped and generated missions, with their date, area and briefing line.
- **Mission setup:** place the sides' groups on the globe, set their orders, the objectives and the events. It's the
  editor for hand-made missions, and it shows a generated one for changes before it starts.
- **Briefing:** the situation as the player's side knows it (its picture at the start), the forces, the objectives;
  maybe the player's choices (which groups to commit, their starting orders).
- **The map:** the world's screen, limited to the area: the globe there, own forces and contacts, orders, the clock
  with its compression; encounters opening; the whole zoom down to a single ship.
- **No separate tactical view:** the battle is the same map, zoomed in to the tactical and ship bands
  (`docs/map-ui/README.md`). "Take command" takes the camera there and hands the encounter to the player.
- **Debrief:** the result, the objectives, the event log, a replay later.

**Mission start (agreed):** a mission gives fixed forces, or a limit (tonnage, cost, a year's technology) within
which the player designs their own ships, or both. The designer is pushed from the mission start screen; it already
takes a design in and hands one back, and the limits are its `DesignLimits` filled from the mission. **The mission's
technology** is a year preset per side (or an explicit list): the designer offers only the templates it unlocks, so
no STS steel in 1910 (`docs/world/architecture.md` "Technology"; the campaign's research fills the same set later).

## Generating missions

Once one mission plays well, a generator makes more from templates:

- **Templates** are the mission types: hunt a raider (Bismarck, Graf Spee), intercept a sortie, protect a convoy,
  a fleet action from contact, a night destroyer attack, a breakout through a strait.
- **Parameters** are drawn per mission: the area (from real waters, once the Earth model is in), the date and
  weather, the forces (from the shipped designs by era and nation, or from the player's), the starting intelligence
  (how old, how precise), the enemy's plan among a few.
- **Checked headless before shipping:** each generated mission is played AI against AI many times (the sim runs
  headless and deterministic). Kept only if it's winnable and not trivial (the enemy is found in some runs and not
  in others, the fight isn't hopeless), with the spread of outcomes kept as its difficulty. This is also a test of
  the sim: a mission that always ends the same way, or never ends, says something's wrong.

## What the first version needs from the world, and what it doesn't

Needed: the whole ship simulation (`docs/battle/plan.md` "Done means"). In particular, the search a mission is about
needs:

- time compression with the rails tier, and speed drops by the user's interest table (steps 2.5, 3.1);
- **pictures** (step 3.2): the search is the mission;
- a captain AI per group following standing orders (evade, shadow, engage, go to), both sides (step 5.1);
- a sighting model good enough for search: horizon, weather, night, smoke (step 3.2). No radar: the era is about
  1890-1925 (`docs/battle/plan.md` "The era it's built for").

Not needed yet: Resolved (every encounter in a mission is the player's, at Full; Resolved comes when the player
wants to hand fights to captains or the campaign brings NPC wars), the world's merchant traffic (a mission's
merchants, harassed by its warships, are just ships in it), NPC admiralties, diplomacy, the
coarse tier beyond what a damaged ship needs after a fight. The job system comes when profiling asks for it (the
architecture's threading rules hold from the start). The Earth model when the user brings it: first missions are
open ocean.

Torpedoes, destroyer attacks and mines are in the battle module (steps 4.6, 4.7). Later inside missions: D/F and signals
intelligence; submarines before the campaign; aircraft (search planes and torpedo bombers, replacing scripted
reports) in version 2.0. The game's era is about 1890-1925 (`docs/battle/plan.md` "The era it's built for"), so the
first missions are drawn from it; the Bismarck chase above stays the model of what a mission asks for.

## Stages: baby steps (agreed 2026-10-11)

Build one thing at a time, each playable before the next starts. The missions above are where the stages end up
(days long, so time compression is in by then), not where they start.

**This needs planning before it's built:** each stage gets its own plan (what's in it, what it proves, how it's
tested), agreed with the user, at the start of the session that builds it; a stage may split into several sessions
as we ramp up.

Since 2026-10-12 the stages are steps in the battle module's order (`docs/battle/plan.md` "Building it from the ground
up"), which starts one level lower, with a world of water and nothing on it (1.1, 1.2):

1. **One ship in the world, controllable:** step 2.3, first half.
2. **Two ships,** the second steered by a trivial captain: step 2.3, second half.
3. **They shoot at each other:** steps 4.1-4.3.
4. **And on:** groups, time compression, contacts and pictures, the captain AI, scripted reports, objectives, the
   mission UI, the generator: the rest of that order.

## Decisions (the user's, 2026-10-11)

1. **Start with one ship, then two, then shooting**, building one thing at a time; each stage planned first (above).
2. **Scripted reports, not aircraft,** in the first iteration: warships only, and sometimes merchant ships harassed by
   warships. Aircraft come later; submarines after (they'll be fun).
3. **A mission start UI.** Missions can give fixed forces, or a tonnage limit (and the like) within which the player
   designs their own ships, with the designer pushed from it. (An endless swarm of torpedo boats against a battleship
   is a legitimate answer.)
4. **No first mission picked;** the Bismarck chase is the kind of mission wanted: a few days long, so time compression
   is needed by the time we're done.

## Open questions (the user's)

1. **Research:** a catalogue of historical search and interception operations as mission seeds (requested in
   `research/REQUESTS.md`).
