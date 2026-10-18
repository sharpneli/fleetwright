# Plan: fire (a 4.5 deepening)
Status: **agreed with the user 2026-10-18** ("Decided" below); B0 done 2026-10-18    Updated: 2026-10-18

The fine-tune pass for fire: what happens after a fire starts. Its research is `damage/17_fire.md` (with `fire_ref.py`),
on top of `damage/10_damage_control_holistic.md` §2 (fire, ammunition in a fire, the fire main) and `09_damage_control.md`
§5 (parties). The starts stay as they are: 08 §3.9's chance per burst by shell, times 12's filler. As in the other plans,
the reference code is something to check against, never a second engine.

## The aim

The record says a fire was out in minutes or never (17 §0.1), and that **water decided which**: every "never" whose cause
is known had its fire main cut, its hoses shredded or its pumps lost to smoke. The physics behind it is flashover (17
§0.2): before it one hose wins, after it the space needs ten times the water. Fire v0 can't show either. This pass makes:

1. **A fire a thing with a size:** it grows, flashes over or stays local, burns as fast as its air lets it, and burns out.
2. **Spread by the way it goes:** through openings in minutes, up through steel in a quarter of an hour, sideways in
   most of an hour, down in hours.
3. **Firefighting as parties with water or without:** a rough model, the parties and their water by section, whose end
   results match the record.

## What there is today (checked 2026-10-18)

- **`Fires.Step`** (`Sim.Combat/Fires.cs`, Fire v0, `fighting.md` 4.3b): `CellFire[i]` and `PartFlags.Burning` are
  booleans. Per minute a burning space harms each room in it (0.2), spreads to each neighbour (0.02), is put out (0.15 ×
  the share of the whole crew left), costs 0.3 crew, and may set off a magazine in it (0.05) or next to it (0.01).
  Drowned at half full. Called from `DamageSystem.Step` every `FloodMs` (1 s) while anything burns.
- **The starts** (`Effects.cs` ~570): `FireChance` by shell kind × `FireLoadByEra[c.Era]` × superstructure / machinery /
  hull load × the burst's size; a fragment-struck mount's ready ammunition lights with `ReadyAmmoFireChance`.
- **Readers:** the map's damage marks, `SeenFlags.Burning`, the hits bench's fire %, the scenario log's fire count, the
  dev `Burn` command.
- **Flooding** (`Flooding.Step`) has the sea in through holes, water across the graph, and pumps on steam
  (`PowerCap`). Nothing leaves a cell through a hole above the sea.

## What's wrong, against the research

| Gap | Today | The research | Why it matters |
|---|---|---|---|
| **A fire has no size** | on or off | grows as t², flashes over at Thomas's threshold, burns at ~1.5·A√H MW through its openings, out when the fuel's gone (17 §3-4) | a smoulder and an inferno look the same; nothing burns out; the enemy sees the same "burning" |
| **Putting out ignores water** | 0.15/min × the ship's crew share: every fire's half-life ~4.6 min | one hose before flashover, 4 L/min/m² after; held below it (17 §3, §6.1) | the split (minutes or never) can't appear |
| **Load by the look** | `FireLoadByEra[c.Era]`, the look's era | load by space and outfit, MJ/m², with stripping (17 §2) | a look shapes physics (one physical model); no stripping for war |
| **Spread the same every way** | 0.02/min to every neighbour | open path 1-3 min; up ~18 min, sideways ~40, down ~2.5 h from flashover (17 §5) | fires climb, they don't creep down; an open room goes at once |
| **Harm by the minute** | flat chances for rooms, crew, magazines | heat: a developed fire harms; a protected magazine holds for hours, a handling room by a gasoline fire minutes (10 §2.3) | magazine losses to fire are a flat lottery |
| **Part fires** | burn till put out, never spread | propellant at the guns is seconds to minutes (Malaya ~5 min); superstructure is a well-ventilated space | ready ammunition burns like a cabin |
| **No water left aboard** | — | 80-90% of hose water stays: 12-21 t per hose-hour (17 §6.5) | Orel's heel, Exeter's list |
| **No outflow above the sea** | water above the sea stays in a holed cell | it runs out over the hole's lower edge | hose water high in the ship would stay forever |

## The principles

- **Rough where it doesn't pay, physical where it decides** (the user, 2026-10-18: "not an ultra core system like fire
  control ... a rougher sim that gives good end results"). The per-space fire is a few multiplies and is what makes the
  split, so it keeps 17 §8's physics. The supply side is coarse: parties as units with a flow, water as a share per
  section. **No hoses, no pipe network, no pumps as parts.**
- **Physics as numbers in the design, never by period.** Fire loads are MJ/m² per space kind in the design's `outfit`
  block; firefighting is numbers in the side's doctrine. Templates by navy and year are labels on the numbers, as the
  fittings' shock grades are.
- **The outfit is the hull's, the doctrine is the side's, the readiness is the ship's orders.** What's built in (load,
  paint, wooden decks) is design. How the crew fights fire (first water, parties, nozzles) is doctrine, like the spotting
  rules (fire-control decision 3). Stripped, doors shut, fans secured, hoses laid out and decks wetted are orders before
  action: ship state, set by the scenario and later the player.
- **History is the yardstick.** Fire now has its own damage control, so its outcomes are judged against the record (the
  yardstick below). Flooding's still aren't (no historical tuning without damage control).
- **No size cutoffs.** Anything that scales with the ship scales smoothly with it (the main's break chance by
  displacement, not by type).
- **Allocation-free and dt-proof.** Per-space arrays at spawn; only burning or hot spaces cost anything; a run at a 1 s
  step and at 15 s agree within the noise.

## The yardstick: the historical record

The first game is 1890-1925, so its anchors lead; the WWII ones check the mechanisms for later cuts (17 §7, §9 T6).

| Case | Record | Wanted |
|---|---|---|
| Santiago: unstripped, unorganised cruiser, ~30 hits in 45 min | beached burning at 40, 55, 100 min | burning beyond help in most runs, in about 30-100 min |
| Yalu: the same with 12 hits, stripped and prepared | Matsushima, Zhenyuan: fires kept in hand | most runs all fires out |
| Yalu: 12 hits, unstripped, no organisation | Chaoyong, Yangwei lost to fire | most runs burning beyond help |
| Tsushima: battleship, ~60 hits in 40 min | Suvorov ablaze in ~30 min, burning till sunk | ablaze in about 20-40 min |
| Tsushima: Orel | 34 fires, each fought, survived; hose water heeled her | many fires, mostly out, tens to hundreds of tonnes of water |
| Jutland: RN battleship, ~15 heavy hits | fires rare and short; propellant the danger | fires out in minutes; the danger the magazine |
| Savo: 1942 cruiser, 50 hits in 20 min (later cuts) | never out | never out in most runs; out within ~30 min on the post-Savo fit |
| South Dakota / San Francisco (later cuts) | 13-22 fires, quickly out | out within ~30 min in most runs |
| A single HE fire, prompt water | Boise's cabin 12 min, Matsushima "immediately" | out in minutes without flashover |
| Unfought, one door | hulks burn for days; a space 2-4 h | a 40 m² space burns 2-4 h |

## Decided (the user, 2026-10-18)

1. **Firefighting comes in now**, as damage control's first slice with fire the only job ("otherwise we'd have the
   ships burn way too fast"). An approved exception to the shells plan's "damage control comes after this plan". Flooding
   jobs join the same parties later (fire-first or flood-first, 10 §5).
2. **Before E8** (the battle anchors): fire stands alone, so E8 anchors once, with fire in.
3. **The supply side is rough:** parties as units, no individual hoses anywhere, no detailed fire main or pumps.
4. **Until B4 the stand-in stays:** B0-B3 keep v0's put-out chance on top of the new fire, so the ships don't burn
   unfought in between (end-goal rules: a step may relax a rule the plan restores). B4 removes it.

## The model

### Fire spaces

One state for every place that burns, in one code path:
- **hull cells**, with the room kinds' loads;
- **superstructure parts**, as spaces with generous openings;
- **the weather deck**, one strip per section: what's on it burns (planking, boats, hammocks), sustained only above
  ~300 MJ/m² (×0.6 wetted) (17 §5.3).

A **propellant seat** (ready ammunition, charges in a gunhouse or handling room) is its own kind: a big release for
seconds to a few minutes that lights its space, as the flash already does for the crew (16 §4.6).

**Sub-rooms.** A cell is a whole watertight section × tier × band, often bigger than one room (17 §10). Each room kind
has a typical room size [EST: cabin ~12 m², office ~20, messdeck and stores up to ~90]. A fire flashes over one
sub-room; the involved share of the cell grows a sub-room at a time at the open-path rate (1-3 min). The water a cell
needs follows what's involved, and a big messdeck section doesn't go up as one room.

State per space [17 §8.1, cut down]: heat release Q (MW), fuel left E (MJ), phase (`grow`, `local`, `starved`, `dev`,
`decay`, `out`), the involved share, the ignition and flashover clocks, and hot-until (for reflash).

### The step (17 §8.2)

1. **Limits:** air `Qvent = 1.5·(leak + doors·1.44 + holes^1.25)` MW (open deck: none); fuel `Qfuel = area·0.25·
   clamp((q−230)/420)`; flashover `Qfo = 0.65 × Thomas`.
2. **Grow** as α·t² (medium × the outfit's growth factor; stores ×2, paint and gasoline ×4) to: flashover (`dev`); `local`
   when the fuel can't reach Qfo (dies with the seat's items, ~20 min); `starved` when the air can't (smoulders ~30 min,
   flashes on a new hole or an opened door).
3. **Developed:** Q = min(Qvent, Qfuel) × (1 − 0.7·held); decays on the last 20% of fuel; out when it's gone.
4. **Water** (below): held, knocked down, mopped up.
5. **Spread** (below), **effects** (below), **reflash:** a space that burned developed > 10 min and went out has 1/20
   min for 30 min (×0.1 with a reflash watch).

**Openings:** doors per cell from the readiness (doors open: one per cell; shut: leakage only); holes from `BreachM2`,
`JoinOpenM2` and joins a burst opened (`CellOpened`).

### Spread

| Way | Hazard | Source |
|---|---|---|
| open path (same room, an opened join, a hole) | 1/2 min × Q/2 MW, ×2 when flames come out (Qfuel > 1.5·Qvent) | 17 §5.1 |
| up through a deck | after 8 min, 1/15 min | 17 §5.2 |
| sideways through a bulkhead | after 12 min, 1/40 min | 17 §5.2 |
| down through a deck | after 40 min, 1/180 min | 17 §5.2 |
| along the weather deck | 1/6 min × load/300 | 17 §5.3 |

Delays from flashover; × the neighbour's load (load/400, 0.3-1.5), ×1.5 for heavy paint; ×0.3 when the fire's party has
the flow it needs and the doctrine cools boundaries. Not into a space half flooded (as v0).

### Effects

- **Rooms and parts:** harmed by a developed fire (a dose of time at heat: cables and paper at 232 °C, 17 §5.2), lightly
  by a local one. Replaces the 0.2/min.
- **Crew:** the men in a space at flashover, and smoke, by 16. Replaces the flat 0.3/min.
- **Smoke into the machinery:** a machinery space over or next to a developed fire is abandoned at 1/8 min with the
  supply fans running, 1/60 min secured (17 §5.4 [EST, set to Savo]); out of action on E4's outage clock until the fire
  is out and a while after. Steam lost stops the pumps.
- **Magazines:** heat through the boundary instead of a chance a minute. A magazine sharing a bulkhead with a developed
  fire cooks off after a time drawn around tens of minutes; under a fire (the fire's floor is its cool layer) hours; a
  fire in it or a handling room, minutes (10 §2.3; propellant at ~160-170 °C). **Flooding or sprinkling it** stops the
  clock: the doctrine floods a threatened magazine if its controls aren't in a burning or smoked space (Teresa, Astoria)
  and there's water; the flooding model takes the water, the mount loses the ammunition (07 §flood_state).
- **Seen:** `Burning` while a developed or open-deck fire shows, its brightness by Q (night sighting). A starved fire
  inside shows nothing (17 §8.5).

### Water on a fire (17 §6.1)

- **Needed:** `Freq = max(40, 4·A_involved)` L/min (fog-equivalent).
- **Given:** F, the fog-equivalent flow on the space (the fire watch and the parties, below).
- `held = min(1, F/Freq)`; at F ≥ Freq knocked down with τ = 90 s·Freq/F, then out after 60 L per m² involved; water that
  stops before then leaves a 30% chance it rekindles.
- **Left aboard:** applied − min(applied, Q·dt/2.6 MJ/kg) goes into the cell's water. **Flooding gains outflow**: water
  above a hole's lower edge, with the sea below it, runs out through it (a weir), and from there down through opened
  decks as water already does.

### Firefighting (rough, by design)

The doctrine (the side's, templates by navy and year) is a handful of numbers:

| Number | Meaning |
|---|---|
| fire watch | the delay to first water on a new fire, and its flow (one hose's worth); none when unorganised |
| muster | the parties' delay to their first job |
| per-man flow | L/min fog-equivalent a party man gives: nozzle, men per hose and rotation folded in |
| parties | the share of the complement in them, and the party size |
| smoke | the flow's share below decks on a developed fire (breathing sets or not) |
| main | the break chance per hit, looped or not, the restore time (never when unorganised), the pumps' floor (a diesel) |
| habits | wets decks, cools boundaries, reflash watch, floods threatened magazines |

Per-man flows [DERIVED from 17 §6.1's hose table]: a 2.5-in canvas jet (1890s-1918) 600/3/6 ≈ 33; RN 1916 large jet
420/3/5 ≈ 28; USN 1942 all-purpose 125/4 ≈ 31; USN 1943 fog 360/4 = 90; each ×0.8 for rotation.

**Water by section.** Each section has its main or not. The ship's share is the pumps' (`PowerCap`, steam), at least the
doctrine's floor. A hit in a section breaks the main there with the doctrine's chance × (2000 t / displacement)^0.4
[FIT to 17 §6.4's ×1 destroyer, ×0.5 cruiser, ×0.35 battleship]. Unlooped, the sections beyond it (away from the
machinery) go dry too; looped, only that one. After the restore time the rest come back and the broken section has half.
A dry section fights with buckets and extinguishers at ~0.15 of the flow.

**The fire watch.** A new fire gets the watch's flow after its delay, if its section has water and its space has men
left (on deck and in the superstructure, always). This is the "one hose within a minute" (17 §0.2).

**Parties.** A ship has `round(complement × share / size)` parties (at least one), each a size of men at a muster point
amidships, fore and aft. Every 30 s they're given to the fires by threat (Q, ×3 next to a magazine, spreading) with some
stickiness; a party arrives after its muster (first job) plus 30 s a section and gives men × per-man flow × its section's
water × the smoke share. A big fire may take more than one. A hit in the section where a party works stops it for 2 min
with the doctrine's chance (hoses cut, men down) and costs it men in the share the cell's crew lost. Fifteen minutes
after the last hit the gun crews turn to as extra parties at 0.7 (10 §5). No hoses, no positions in the room.

**Templates** (the first game's first): unorganised 1890s, prepared 1890s, Russian 1905, RN 1914-18, German 1914-18;
for later cuts USN 1942 and USN 1943+ (17 §8.3's table).

### Inputs

- **`outfit`** (design, shipgen): load MJ/m² by space kind as fitted and as stripped (cabins and wardroom, messdecks,
  offices, stores, open deck), paint (MJ/m² of deck), wooden weather deck, aircraft fuel. Templates by navy and year from
  17 §2.2's table; the design format gains the block with a version step, old designs taking their navy and year's.
- **Readiness** (ship state, scenario): stripped, doors shut, fans secured, fire watch posted, decks wetted. Defaults
  from the doctrine.
- **The starts** lose `FireLoadByEra` and the load multipliers: 08's chance by shell, times the space's load, starts a
  seat; the load then decides whether it grows (a thin load gives a local fire that dies, 17 §3). The hits bench's fire %
  will move.

## The steps

Each step reports its numbers on the hits bench, the squadrons scenario and the matchup, and the record's anchors
from B0's bench on.

### B0 One space, unfought, and the bench (done 2026-10-18)
- The fire spaces' state (cells, superstructure parts, deck strips), the outfit block and its templates, sub-rooms;
  growth, the limits, flashover, local and starved, burn-out. The starts on the new seats.
- `shipgen burn`: one space by load, size and openings (17 §9 T2), against `fire_ref.py`'s tables.
- v0's spread, harm and put-out stay as the stand-in (decision 4).
- Tests: 17 §8.7-1 unfought half and 3; dt 1 s vs 15 s.
- **Built:**
  - **The outfit** (shipgen): `outfit` in the design (`OutfitInput`: `fitted` and `stripped` loads by kind of space,
    `paint_mj_m2`, `growth`), five templates (`Outfits.cs`: Victorian 1890, pre-dreadnought 1905, Great War 1914,
    interwar 1939, post-Savo 1943) by navy and the plant's year; format 7 → 8 wrote each design its template's block
    (125 files); `shipgen templates` writes `Content/Templates/outfits.json` and `docs/shipgen/outfit-templates.md`.
    The goldens gained the block (ShipDesign 39); nothing else in shipgen's output moved.
  - **The fire spaces** (`ShipClass.FireSpaces`, `FireSpaceRow`): every cell at its own index by its room's use
    (quarters; stores and holds; boiler, engine, steering and torpedo rooms as machinery; the transmitting station as
    offices; magazines, handling rooms, bunkers, tanks and voids burn nothing from a shell); superstructure blocks as
    cabins-and-offices, the conning tower and tops as offices, gunhouses, casemates and tubes as machinery (local fires
    until B1's propellant seats), hangar and flight deck; a weather-deck strip a section over its top cells, wooden at the
    outfit's deck load, steel at half [EST]. Dreadnought: 308 spaces (253 cells, 35 parts, 20 decks), 101 cells that
    don't burn.
  - **The fire** (`Fires.Advance`, `BurnState`, `BurnPhase`; one function for the ship and the bench): 17 §8.2 with
    the sub-rooms (`FireRoomM2` by use: a messdeck 90 m², a cabin 12, an office 20, stores 60; developed, the next room
    joins every 2 min). Openings: one door a cell, two a part, a cell's holes to the sea. `Fires.Ignite` lights a space
    (refuses one with nothing to burn or burnt out; a space alight takes the seat's heat); `CellFire` and a part's
    `Burning` are shown from it, so every reader stayed as it was. The dev `-burn` lights the top cell (an error where
    nothing burns).
  - **The starts:** the space the burst is in (its part, its cell, outside the part its fragments reached first, else
    the weather deck, only when it burst at deck level); the chance is v0's × the space's load over 560 MJ/m² (at most
    ×2; a Great War messdeck reads as v0's hull did) in place of the look's era and the three multipliers; the seat by
    kind (AP 0.1, common 0.4, HE 0.3 MW; ready ammunition 1). The load still scales the start (fire_ref.py's p_fire does
    too), and a thin load's fire then dies on its own.
- **The bench** (`shipgen burn`): every T2 row within 0.1 min of `fire_ref.py`'s flashover, its peak and its hours
  (the 40 m² 1939 messdeck: 4.3 min, 2.24 MW, 3.1 h; closed up: starved 0.08 MW, out at 30 min; the 1943 load: local, 24
  min); at a 15 s step flashover 0.2-0.3 min later, hours the same. A 300 m² messdeck section is all involved ~4 min after
  its first room flashes; a wooden 1890 deck burns at ~20 MW for 1.6 h, a 1914 one dies.
- **What moved** (the hits bench, before → after; v0's put-out still ends every fire inside the settle):
  - fire per hit: Borodino under Mikasa's 6" common 11% → 4%, Dreadnought under 12" HE 52% → 33%, Iron Duke under
    Seydlitz 7% → 5%, a destroyer under a light cruiser 2% → 2%. Bunkers, tanks, magazines, voids, funnels, masts and
    barbettes no longer light (v0 lit them at the hull's or superstructure's multiplier). Pre-dreadnoughts lose most: their
    sides are coal bunkers, and v0's ×1.6 for the Victorian look is gone. B5 judges the starts against 08 §3.9 and
    17 §9 T6 (Tsushima's ~19 fires in 60 hits), with the outside bursts' share the first thing to look at.
  - crew out of action after the settle: Dreadnought 76 → 69, the rest within 1. Magazines lost, flooding, speed:
    unchanged within the noise. The squadrons scenario diverges at the first fire draw (a single seed); the damage
    system's share of the run 12.7% → 10.4%.

### B1 Openings, spread, reflash
- The readiness (ship state): stripped or as fitted (B0 burns every ship as fitted), doors shut or open, decks wetted;
  doors from it, holes from the breaches; the spread table; the open deck; propellant seats; reflash.
- The bench: spread times from flashover, the starved fire that flashes on a hole.

### B2 Effects
- Heat harm, crew by 16, smoke into the machinery, magazine heat and flooding, seen by Q. The v0 chances go, except
  put-out.

### B3 Water
- The attack physics with a bench flow (17 §9 T3, T4); water left aboard; flooding's outflow above the sea.
- Tests: Shadwell's fog and jet (17 §8.7-2); 12-21 t a hose-hour.

### B4 Firefighting
- The doctrine block and templates, the fire watch, parties, water by section, hits on parties, turn-to. v0's put-out
  goes.
- The bench gains ships: 17 §9 T6-T8's scenarios, given hits (`GivenHits`) over time.

### B5 Anchors and cost
- The yardstick above; the first game's anchors first. Tune only within the [EST] numbers, and log what moved.
- Cost per burning space and per ship; the squadrons scenario's frame time.
- What the AI and the matchup read about fire, if anything (a ship burning beyond help).

## What stays out

- Hoses, pipes, pump parts, valves and the fire main as a network (decision 3).
- Bunker spontaneous combustion (17 §2.1), oil fires after torpedoes beyond a fuel spill seat, avgas systems beyond the
  outfit's fuel.
- Wind and heading on deck fires, jettisoning ammunition, the player's orders and the designer's rows: the UI pass.
- Flooding jobs for the parties: damage control proper.
- The coarse tier's fire: with 4.8/4.9.
