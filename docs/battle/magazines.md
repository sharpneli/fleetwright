# Plan: magazines (a 4.5 deepening)
Status: **draft**; the user decided questions 1-3 and 5 on 2026-10-20 ("Decided" below), question 4 (shield mounts) is
open; no step started    Updated: 2026-10-20

The fine-tune pass for magazines: how a flash gets to the charges, what happens once they burn, and what the ship
keeps. **This is the one place for magazine work.** Every open item about magazines, hoists, flash doors, the
venting race and the hull at the magazine lives here. The other docs point here and keep only what's already built:
fighting.md 4.5's Magazines bullet, the shells plan's hand-off and its E8a items 1 and 4, bugs.md's mount kind, fire's
B2 heat, damage control's valves, plan.md's propellant need, and the magazine look's tiers.

The research:
- `research/simulation/damage-research/07-magazine-explosions.md` is the core: the venting race (§2.3), the boundaries
  in the order they fail (§2.4), the hull girder (§3), the outcome tiers (§6.3) and the anchors (§6.4).
- `docs/shipgen/research/02_components.md` §1 covers the ammunition train, the propellants, the failure modes (Seydlitz,
  Lion, Derfflinger) and the mitigations (flash doors, interlocks, flooding).
- `docs/battle/10_damage_control_holistic.md` §2.3 is magazines under fire, and `docs/battle/damage/11_blast_confinement.md`
  covers trunks as joins between spaces.

As in the other plans, the reference code is something to check against, never a second engine.

## The aim

Today a flash is a lottery that ends the ship. The record says it's a chain and a race:

1. **A chain.** Flame goes from where charges lie open (gunhouse, working chamber, hoists, handling room) down to the
   magazine through doors and scuttles, and only as far as they're open.
2. **A race.** Burning propellant makes gas faster as the pressure rises, and the openings let it out. If the vents win,
   the result is a flame column and a burned-out turret, and the ship fights on (Seydlitz, Derfflinger, Lion). If the
   gas wins, the decks, sides and bottom go and the ship breaks at the magazine (Indefatigable, Queen Mary, Invincible).
3. **Choices that move both.** Flash discipline trades rate of fire for risk. Flooding the magazine in time saves her
   (Seydlitz's Heidkamp, Lion's Harvey).

## What there is today (checked 2026-10-20)

- **The sources** are in `Effects.cs`, each one flat roll ending in `MagazineVia`:
  - a burst in a magazine (`In`) or beside one (`Beside`) rolls `MagazineFlashChance` 0.25;
  - a burst in an enclosed mount's gunhouse or barbette (`Turret`) rolls `TurretFlashChance` 0.25;
  - E3's spall among charges (`Spall`) rolls the same chances, times how many pieces got there;
  - E5's hot gas reaching a magazine or handling room (`Gas`), or a burst in a handling room (`Handling`), rolls them too;
  - a torpedo (`Underwater.cs`) rolls `TorpedoMagazineChance` 0.05 when its reach takes in a magazine;
  - a fire's heat (`Fires.Magazines`, fire B2) goes up past a drawn limit.
- **Any of them loses the ship at once:** `DamageSystem.Lose(LossKind.Magazine)`, gone after `MagazineLossMs` (60 s).
  `Casualties.Train` flashes the men in that magazine's mounts' gunhouses, barbettes and handling rooms and in the
  magazine. Nothing between a flash and a lost ship exists: no burned-out turret, no partial hull damage.
- **Enclosed** means `TurretShape.Bb` (`Effects.Enclosed`). Shipgen makes every main-battery mount of 150 mm or more a
  `bb` (`Geometry.MakeTurretType`: `calMm >= 150 ? "bb"`, a switch by calibre). So Chester's open-backed 6″ get a
  turret's flash path.
- **Shield mounts** (checked 2026-10-20): the same switch makes every mount of 76-150 mm a `dp` (the designer calls it
  shielded) and anything smaller `open`; casemates come from the secondaries' position. Then:
  - **No flash goes down from the mount:** a burst or spall in a `dp` or `open` mount isn't a turret source.
  - **Crews sit in steel:** `Casualties.Enclosed` counts `dp` as a gunhouse, for the men's cover and their flash. A
    destroyer's 4″ behind a shield and a 5″/38 gunhouse are the same shape.
  - **A handling room** sits over every main mount's own magazine, a destroyer's shielded guns included
    (`Ordnance.AddHandlingRooms`). A burst or hot gas in it rolls `TurretFlashChance` (V25 under Arethusa's 6″ HE:
    0.17% of hits). Secondaries share a battery magazine, fore and aft, with no handling room.
  - **The ready rounds** burn when the mount is disabled (`ReadyAmmoFireChance` 0.3 × the burst's force): a propellant
    seat and the crew's flash.
  - **That fire doses the mount's magazine** as if down a gunhouse's trunk (`Fires.Magazines`, 300 s with charges
    burning, 1,200 without), for any mount, open or shielded. That's a fake path, like the calibre switch.
- **The ammunition train in the hitbox model:**
  - each mount has its magazine (`MountRow.Magazine`) and its barbette;
  - a handling room sits under each enclosed mount (`RoomKind.Handling`, protection-plan H1);
  - there's no working chamber apart from the barbette, no hoist trunk as a join in the cell graph, and no flash doors.
- **Magazine flooding** is fire's. `Fires.Magazines` floods a threatened magazine on the doctrine's
  `FloodMagazineS` unless smoke cuts the controls off; the sea or the main fills it; a drowned magazine can't go up, and
  its mounts lose their rounds. Damage control's own valves (counterflooding) are separate: one set of valves in the
  story, two in the code (damage-control.md DC4).
- **The supply:** ready-use rounds, then the hoists at `HoistShare` × the reload (`DesignerPlaceholders`, fire-control
  F6d); `CombatStore` keeps each mount's rounds. A magazine has no fill of its own.
- **The charge:** `DesignerPlaceholders.Charge` gives the mass (4000·d³·L/50) and the propellant from the gun block
  (black powder, cordite and kin, single-base, RP C/12). There's no packaging (bag or case). The magazine look burns
  `MagazineFastShare` 0.3 of it.
- **No navy values** since E8b: the flash chances, the torpedo's and fire's limit are one set of numbers for every side.
- **The look** (`docs/visuals/magazine-explosions.md`) draws tier 3 only. Its barbette column is built already, so
  tier 1 is the same event without the fireball. Its `MagazineBlast` has its own stand-in for the race (`PeakBar` 3.0,
  `PressureBar(t)`, the openings' failure times), which is display-only.
- **The benches:**
  - `damage-history` reports how a ship ended and what set the magazine off (by `MagazineVia`), and the turret-minutes lost;
  - `hits` prints magazine losses by cause;
  - `burst` is E5's gas cascade alone.

## What's wrong, against the research

| Gap | Today | The research | Why it matters |
|---|---|---|---|
| **Fake turrets** | every main mount ≥ 150 mm a gunhouse with a barbette | open-backed shields on cruisers' 6″ (Chester, the Towns) | 13% of Chesters lost to a magazine; record none (E8a item 1) |
| **No chain** | one roll per source | gunhouse → working chamber → hoist → handling room → magazine, a door at each step (02 §1.3) | doors and discipline can't matter |
| **No race** | a flash = ship lost | vents vs gas, the knife-edge `P* ∝ (S/A)^(1/(1−α))` (07 §2.3) | no Seydlitz, no Derfflinger C and D (E8a item 4) |
| **No partial outcome** | tiers 0-2 missing | contained, burned out, local breach (07 §6.3) | every flash costs the ship |
| **The hull** | lost whole | breaks at the magazine; an end magazine takes only the end (New Orleans) | where it went up doesn't matter |
| **Neighbours** | one magazine | the secondary magazine beside the main is often the trigger (Hood, Queen Mary, Barham) | — |
| **Two valve sets** | fire's flooding apart from DC's | one set of valves, one order | confusion, two places to tune |
| **No discipline** | — | doors propped, charges stacked: faster and far riskier (02 §1.3, Jutland) | the player's choice the record turns on |

## The principles

- **One gas model.** The race runs on E5's gas: the same boundaries, rupture pressures, vents and cascade
  (`Blast.cs`), fed by a burning source over seconds instead of one burst. There's no second pressure engine (single
  implementation).
- **One magazine state.** Heat, flooding, fill and burning live in one place (a `Magazines` module in Sim.Combat).
  Fire, the hits, torpedoes and damage control all feed it; none keeps its own copy.
- **Physics as design data.** The mount's housing, the charge's packaging, the turret's weight, sprinklers and
  interlocks are the design's. How the crew handles charges is the ship's orders. No navy values (E8b), no switches
  by type or size (no hard switches).
- **History is the yardstick.** The anchors are 07 §6.4 and damage-history's cases. Today's numbers prove only
  refactors.
- **Rough where it doesn't decide.** The race and the chain decide outcomes, so they're physical. The pieces after a
  break, their trim and their floating are the sinking's and the look's; here only "broken, which end" (rough for
  non-core).
- **Allocation-free and dt-proof.** Per-magazine arrays at spawn. A race costs only while it runs (seconds at the fine
  step). A 1 s and a 15 s step agree within the noise for the slow paths (heat, flooding).

## The yardstick

The first game's cases lead (1890-1925); the later ones check the mechanisms.

| Case | Record | Wanted |
|---|---|---|
| Seydlitz, Dogger Bank 1915: aft barbette holed, charges in transit, a door open between the aft turrets | both aft turrets burned out, 159 dead, magazines flooded by hand, survived | tier 1 in most runs, both turrets when the door's open |
| Derfflinger C and D, Jutland | burned, didn't explode; 73 of 78 and 80 of 80 dead | tier 1 in most runs |
| Lion Q, Jutland: face-roof joint holed, magazine ordered flooded | charges in the chain lit ~28 min later, masthead flame, the flooded magazine held | tier 0-1; flooded in time in most runs |
| British battlecruisers, Jutland (Indefatigable, Queen Mary, Invincible): ~3 lost in ~58 heavy hits | tier 3, broke at the magazine, ~1,000 dead each | ~5% a heavy hit (1-14% within the count) with Jutland's BCF discipline |
| The same ships on safe discipline | (the Grand Fleet's battleships, stricter) | well under the BCF's rate |
| Thirty 6″ HE on a battleship | Malaya's battery fire came close | about 1.5% of ships (the user's, 2026-10-15) |
| Chester, 17 × 15 cm on open shields | 3 of 10 guns out, no magazine | magazine losses near none |
| Gneisenau 1942 (later cuts): 23 t RP C/32 | no explosion, turret displaced | tier 1 |
| Hood, Mutsu, Vanguard, New Orleans (later cuts) | stern off; cut in two at anchor; midships gone; bow lost and survived | tier 3 aft; tier 3 in calm water; tier 4; end lost, ship afloat |

## The model

### The mount's housing (from the design)

A battery says how its guns are housed: a **gunhouse** (enclosed, with a barbette, a working chamber and hoists in a
trunk), a **shield** (open behind, ammunition up by hoist or whip to the deck, ready rounds by the gun), or **open**.
It replaces the calibre switch, and splits today's `dp`: a 5″/38's gunhouse from a destroyer's shield. Every reader of
"enclosed" takes it from the housing: the flash path, the crews' cover and flash, and fire's trunk to the magazine. The
design key isn't `mount` (the secondaries' position, and fire control's mount already use that word: one word, one
meaning). Working name `housing`.

**The housing is selected, at any calibre** (the user, 2026-10-20). It's the designer's choice for each battery, not
something the calibre decides:
- **Small guns had enclosed gunhouses too.** Examples: the Monmouths' twin 6″ turrets (1901), Fubuki's fully enclosed
  12.7 cm destroyer gunhouses (1928), and the USN's enclosed 5″/38s. Big guns could stand behind shields: the Towns' and
  Chester's 6″.
- **No rule ties the two.** A new battery may start from a suggestion, but the design keeps what was chosen, and nothing
  refuses a combination (limits come from gameplay, never block).
- **The choice is felt three ways:**
  - in the weight, which shipgen weighs from the housing, so a gunhouse is heavier than a shield, and in the cost in
    the campaign;
  - in the crews' cover from splinters and weather;
  - in the flash path to the magazine, which only a gunhouse's trunk has.

### The ammunition train (an enclosed mount)

The stations are the gunhouse, the working chamber (in the barbette), the hoist trunk, the handling room and the
magazine. Each has:
- **charges lying open**: in transit by the drill, more by the discipline (stacked in the handling room, cases opened);
- **a door or scuttle below it**: shut, or open a share of the time (the hoist's cycle, the discipline), and failed
  when struck (an interlock that failed, 02 §1.3).

Charges lit at a station burn there: the station's men flash (`Casualties.Flash`) and a propellant seat starts its
fire (fire B's seat). The flame goes down one station past each open door, a roll per door, and stops at a shut one.
Every source above lights the station it reaches: a burst in the gunhouse lights the gunhouse, spall in the working
chamber lights that, gas in a handling room lights that. A burst among the magazine's charges lights the magazine.

The hoist trunk is also a join in the cell graph. E5's gas and fire's spread go through it as a low-pressure edge
(11 §4). It's one link for flash, gas and fire.

A shield mount has no train down: its ready rounds burn on deck (fire's seat, as today). Whether a flash can go down
its hoist is question 4.

### The venting race (a magazine alight)

Once a magazine burns:
- **gas made** `∝ S·β·P^α`. S is the burning surface: the fill (rounds left, from `CombatStore`) × the packaging
  (bags all at once, cases one by one) × the propellant's sensitivity, × (1 − flooded share).
- **gas out** `∝ A_vent·P`. A_vent is the open chain (the trunk, the barbette), then each boundary E5's cascade breaks.
- **The gunhouse lifts** at its revolving weight × g over the barbette's area (07 §2.2: about 0.13 MPa for a 15″). It's
  a big vent: the flame column.
- **Integrated over seconds** (07 §6.2's ODE, a few ms steps while it runs, then off). Where the pressure tops out
  gives the tier:

| Tier | When | What's left |
|---|---|---|
| 0 contained | flooded or sprinkled before the gunhouse lifts | the turret out, its men by the flash, a fire |
| 1 burned out | the gunhouse lifts and the pressure stays under the decks' and sides' | the turret destroyed, the train's men dead, a heavy fire; she fights on |
| 2 local | some decks or sides go; the girder holds | E5's holes into the flooding; she may founder later |
| 3 severed | the girder at the station breaks (below) | lost, broken in two at the magazine |
| 4 two groups | the race reaches another magazine (hot gas into it, as E5's) | lost, the middle gone |

A neighbour magazine reached by the cascade's hot gas is lit, and its race joins: Hood's 4″ to her 15″.

The propellant's β and α aren't in the sources (07 §7). They're fitted to the anchors [EST], one row per propellant
kind in the explosives' catalogue beside the fillers. `MagazineFastShare` goes; the look reads the burned share from
the race.

### The hull at the magazine

The cascade's broken boundaries at the magazine's station give `d`, the share of the girder's section lost (the deck,
sides and bottom plate there, from the cells' boxes and plates). The check is 07 §3.1: d under ~0.5 holds in calm
water, 0.5-0.8 breaks in a seaway (the weather's sea), over 0.8 breaks at anchor. An end magazine (under ~0.12 L from
an end) opens that end to the sea and the ship may live (New Orleans). A broken ship is `LossKind.BrokeInTwo`: she goes
down fast, as the record has it (decision 3). The pieces' own floating and the look of the break come later, with the
sinking and the VFX (07 §4, `sinking-vfx-research.md`).

### Flash discipline (a doctrine row)

A row in the side's doctrine, by period, as damage control's (decision 2): **safe**, **drill** and **rate first**.
- **safe:** doors shut, interlocks kept, charges only in transit.
- **drill:** the period's practice.
- **rate first:** doors propped, charges stacked in the handling room and working chamber, cases opened (the BCF at
  Jutland).

Rate first gives the handling room's stack as more ready rounds and a shorter hoist cycle (`HoistShare` by discipline,
fire-control F6d). It also gives more open charges and doors open more of the time. A scenario picks a side's doctrine
as it does damage control's; per-ship orders can come later. Felt both ways: the rate in `duel`, the risk in
`damage-history`.

### Flooding and sprinkling (one set of valves)

The magazine's flooding moves out of `Fires.Magazines` into the magazines module, on damage control's valves. It's
ordered by:
- fire's heat threat (B2's rule, unchanged);
- a flash in the train above it (Harvey: the order, then the valves' time);
- the doctrine.

The controls are cut by smoke or fire over them (as today) or by lost power (Yamato). Sprinklers, where the design has
them, wet the charges in a fraction of the time. The water lowers S (Lion's held), and a drowned magazine can't go up.
The mounts lose their rounds, as today.

## The steps

Each step shows `damage-history`'s magazine lines before and after (the cases above, by tier and cause). The order
follows the dependencies: the fake turrets first, because the chain means nothing while light cruisers have barbettes.

- **V0. The bench.**
  - `damage-history` reports tiers (only 3 today), turrets burned out, and stations reached in the train.
  - `shipgen magazine DESIGN [--mount N] [--discipline D] [--flooded S]` lights one magazine or station, and prints the
    chain, the race's pressure by the ms, what broke, d, and the tier. It's the race alone, like `burst`.
  - Records today's numbers here.
- **V1. The housing.**
  - `housing` on batteries (gunhouse, shield, open); shipgen's turret types take it, and the calibre switch goes.
  - The designs say theirs: Chester and the Towns shield, the destroyers' guns shield, the rest as they are, checked
    one by one.
  - Every reader of "enclosed" takes the housing (`Effects.Enclosed`, `Casualties.Enclosed`, fire's trunk dose in
    `Fires.Magazines`). How a shield mount reaches its magazine follows question 4.
  - The designer's battery tables (main and secondary) let it be picked for any battery at any calibre (one cell, no
    new pane); its weight follows in the ledger.
  - Goldens move, so `ShipDesign.Version` bumps. Closes bugs.md's item and E8a item 1.
- **V2. The train.**
  - The stations and their doors as class rows (from the mount, barbette, handling-room and magazine links).
  - The hoist trunk as a join for gas and fire.
  - Every source lights its station; the flame goes down by the doors. `TurretFlashChance` goes;
    `MagazineFlashChance` stays as a burst among the magazine's charges.
  - Until V3, a magazine reached is lost as today.
- **V3. The race.**
  - The ODE on E5's cascade, the gunhouse lift, tiers 0-4, the neighbour link.
  - The packaging (decision 1), the turret's revolving weight from shipgen's weights, and the fill from the rounds.
  - The ship is lost only at tiers 3-4. Tier 1 destroys the mount and lights the fire's seat; tier 2 leaves its holes.
  - The charge as design data: the shells plan's deferred `charge` (`{kg, propellant, packaging}`), read by the
    muzzle flashes too, replacing `DesignerPlaceholders.Charge`.
  - The record gains the tier, the burned share and the boundaries' failure times for the look. `MagazineBlast`'s
    stand-ins (`PeakBar`, `PressureBar`, `MagazineFastShare`) read those instead (one model).
  - Closes E8a item 4.
- **V4. The hull at the magazine.** `d`, the girder check with the sea, the end case, `BrokeInTwo`.
- **V5. Flash discipline.**
  - A doctrine row by period (decision 2), picked by the scenario with the side's doctrine, and the supply.
  - Jutland's British side on drill. The BCF's rate first would need a doctrine of its own for that squadron, which is
    for the scenario to choose.
- **V6. One set of valves.**
  - The flooding moved and ordered as above.
  - Sprinklers as design data (a fit by period, in the outfit block).
  - Power and controls cut.
- **V7. Every other source into the race.**
  - Fire's heat limit lights the magazine instead of losing the ship.
  - A torpedo's gas and shock reaching the magazine light it (`TorpedoMagazineChance` goes): Pommern.
  - Magazines going up as she sinks or capsizes (Barham, Yamato, Kongō) wait for the sinking pass (decision 5).
- **V8. The yardstick.**
  - The propellants' β and α fitted.
  - The cases above, scenario runs (squadrons, jutland, tsushima), and the user's 1.5%.
  - What's left goes to open items here.
- **Then the look:** tier 1 (the column without the fireball) and tier 4 (two fireballs) in
  `docs/visuals/magazine-explosions.md`, by its own process.

## What stays out

- Bombs (Arizona): their shapes and fuzes aren't researched (the shells plan's "What stays out").
- Torpedo warheads and ready ammunition on deck as explosives (Mikuma, Furutaka, Quincy). They're fire's propellant
  seats today; a warhead's detonation is a later cut's (deck torpedoes and Long Lances are WWII).
- Unstable propellant and accidents (Iéna, Liberté, Bulwark, Vanguard): peacetime, not a battle's.
- The pieces as floating bodies (07 §4): with the sinking and the look.
- Magazines going up after the ship is lost (Barham, Yamato, Kongō): the sinking pass's, eye candy once she's lost
  (decision 5).
- Uptakes and vent trunks as joins: Hood's vent through the engine-room vents wants them. They're E5's open item,
  added here only if the yardstick needs them.

## Research wanted (to file in research/REQUESTS.md once agreed)

- Burn-rate law (β, α) for the period's propellants (cordite MD, RP C/12, single-base, black powder), or ranges to fit
  within.
- Turret revolving weights and barbette diameters by calibre and period (07 §7). Shipgen's turret weights may do.
- Flash doors, scuttles, interlocks and handling practice: ranges by type and period (general system, not replicas),
  and how often doors stood open in drill.
- Sprinklers and magazine flooding times by period (10 §2.3 has some).

## Decided (the user, 2026-10-20)

1. **The charge's propellant and packaging are gun-block data.** Bags or brass cases are a design choice, not a navy
   value. It changes little now; in the campaign it has an economic cost, as design choices do.
2. **Flash discipline is a doctrine row** by period, for now. Per-ship orders can come later.
3. **The hull girder is in now (V4).** Tier 3 means the ship breaks and sinks fast; the look of the break comes later.
4. *(open, below)*
5. **Magazines going up as a ship sinks** wait for the sinking pass: by then she's lost anyway, so it's eye candy.

## Questions for the user

4. **Shield mounts: how do they reach their magazine?** The record as far as our research goes:
   - The danger at an open or shielded gun was its **ready charges**: fires and burns on deck (Chester and Southampton
     at Jutland, Kent's casemate at the Falklands: 10 of her 14 casualties from one cordite fire).
   - In big ships the path below ran along the **ammunition passages** and through the **casemate battery**, not down
     one gun's hoist. Defence's fire ran along her ammunition passages from magazine to magazine. Malaya's 6″ battery
     burned from charges staged on trolleys (~5 min, 65 dead) and came close.
   - We have **no case of a flash going straight down an open mount's hoist** to its magazine. The hoists ended in a
     scuttle at the deck, and an open gun vents its flash to the air. [UNCERTAIN: not checked against the sources;
     worth a research request.]

   Recommendation: no flash roll down a shield mount's hoist. Its ready charges burn as now, and fire reaches the
   magazine only by the ways fire goes (through the decks, by heat). Where charges travel along a passage or a casemate
   battery, that passage is a station of the train, with its doors, like a handling room. Today's 300 s dose "down its
   turret's trunk" goes for shield and open mounts.

## Open items (from the other docs, folded in)

- E8a item 1 (Chester's fake turrets): V1. E8a item 4 (German turrets burn out): V3.
- Fighting.md 4.5's Magazines list (the venting race, the flash chain, propellant by navy, flooding as an order, the
  hull broken, flash discipline, medium HE stays rare): V2-V8. Propellant by navy is now the packaging (decision 1).
- The shells plan's seams: E2's and E3's flash sources and E5's gas are the train's first links (V2). E5's trunks are
  the hoist trunk (V2). E6 kills by flash per station (V2).
- Fire B2's magazine heat and flooding: V6 and V7. Damage control's two valve sets: V6.
- The magazine look's tiers 1 and 4: after V3 (`docs/visuals/magazine-explosions.md` decision 1).
- bugs.md's mount kind: V1.
