# Electrics: how electrified a ship is, and what losing it costs

Proposed and agreed 2026-11-04 (the user). Grew out of `bugs.md`'s "every mount of a ship out at once from one hit" and
the open item in `shells-armour-damage.md` E7 (the G0 grade gives 1916 ships 1939-42's rates). Research asked for in
`research/REQUESTS.md` (2026-11-04); `damage/17_service_network_faults.md` is the fullest note we have, written before
any of this and never built.

**Status:** EL0-EL2 done 2026-11-04. EL3 next, waiting on the research.

## The rules (the user)

- **No era multiplier.** How much danger a ship is in from losing power follows from how electrified she is, and how
  electrified she is follows from what she gains by it. A modern digital computer on a 1916 hull isn't immune; a 1944
  ship stripped to hand gear isn't punished for her year.
- **Total, lasting power loss only when the generators are gone.** A trip is put right by isolating the fault and
  resetting, in minutes.
- **Electrification has a price:** the plant's weight (EL7), so a design doesn't simply take everything.
- **Total kW is the measure of how electrified she is, and it's a stand-in. Keep that in view** (below).

## kW is an approximation

The trip rate follows the ship's battle load in kW (EL6) because kW can be added up from what she carries and checked
against recorded plants. It isn't what trips a breaker. What it can't see:

- **Protection:** fuses and knife switches against breakers; locked-in breakers, which pass a fault up to the generator
  (South Dakota); selective relays. 17 §5.2 has this properly.
- **Topology:** radial or ring main, split plants, whether dynamos run in parallel. A ring main severed cost Prince of
  Wales 6 of 8 secondary turrets with generators still running (02 §8).
- **Wiring against load:** a big drive is many kW on one feeder, a fire-control network a few kW on hundreds of circuits.
  kW overweights the first and underweights the second. A count of circuits might read closer, but there's no data for it.
- **The two calibration points are estimates:** 13 §3.4's WWI and 1939-42 columns are [EST], fitted to very few cases. The
  era enters the curve only there, as two loads with a rate each; no ship reads her year. The recorded plants check the
  kW, not the rates.

If a later playtest or source shows the rate wrong in a way kW can't express, the fix is to add what's missing from the
list above, not to bend the curve per ship.

## What the record says about total power loss

The user's doubt, 2026-11-04: did the record really have that much total loss not solved by isolating the damage? It
doesn't. Two kinds of event, as 17 §0 says, and the sim had them as one.

**Short, the plant whole: a trip or a fault, reset (17 §1.2, 13 §3.2).**

| Ship | Cause | What went dark | Back in |
|---|---|---|---|
| South Dakota 1942 | own turret firing, an ABT closing wrongly | all power aft | ~1 min |
| South Dakota 1942 | enemy hits shorting circuits | FC and IC circuits, not the plant | ~3 min |
| White Plains 1944 | 46 cm burst under the bilge | "most of the circuit breakers" | 3 min |
| Electra | torpedo | the pumps' breakers | 2 min |
| Helena 1941 | torpedo | lights, a generator tripping | 1-1.5 min |
| British destroyers 1940 | bomb near misses, NSF 0.1-0.3 | electric power | minutes |
| O'Brien, North Carolina 1942 | torpedoes, gear secured | one breaker; none | at once; - |
| Savannah 1943 | a bomb, the forward switchboard itself shorted | ship-wide | 8 min aft, 16 ship-wide |

So a ship does go dark for a few minutes, and that's on the record, most of it from underwater bursts. Savannah is the
longest, and there the board itself had faulted.

**Long: the plant itself gone.**

| Ship | What was lost |
|---|---|
| Ark Royal 1941 | boiler room and main switchboard flooded, all generation steam, no diesels |
| Courageous 1939 | two torpedoes, all power |
| Prince of Wales 1941 | steam failed, 3 of 8 generators running |
| Elbing 1916 | both engine rooms flooded |
| Shokaku 1944 | generator and switchboard knocked out |

**What that means for what we had.** `CascadeMin`'s tail (10% at 30-90 min) has no case behind it, and neither does a long
blackout from a shell on armour. 13's rate (0.02 a heavy hit on armour at 1939-42) is an [EST], cut from an agent's
0.05-0.08 because "nothing in the record looks like that", on one hit-caused event, which took out the FC and IC bus, not
the plant. Before WWII: 13 §3.3 has dynamos and switchboards failing "mostly through flooding and splinters"; Von der
Tann's lights at Jutland were splinters in a condenser, and there's no WWI case of a shock blacking out a ship.

## Steps

### EL0 The record and this plan. Done 2026-11-04.

### EL1 A trip is short. Done 2026-11-04.

`CascadeMin` from 90% 1-10 min, 10% 30-90, to 90% 1-5 min, 10% 5-20: the record's cluster at 1-3 and the board-fault
cases at 8 and 16 (17 §6). `NonPenTests`' 13 §5.3 test 6 restated (under 5 min about 90% of the time, none over 20; it
asked under 10). The rate stays until EL6.

### EL2 A lasting loss is the plant's. Done 2026-11-04.

`ShipCondition.PlantOut`, worked out in `Functions.Evaluate`: no steam from any boiler room, or no engine room working
(the dynamos are driven by that steam and stand there; Ark Royal, Elbing). Gunhouses by hand, directors and searchlights
out, for as long as it lasts; it ends when damage control or an outage's end brings a room back. Rough stand-in until
EL7 places generators: no partial loss (a ship with one engine room left has her full supply), no diesels, no switchboard
of its own. Pumps and the dark already read `PowerCap`, the propulsion's share (EL5 separates them).

**The balance panel**, EL1-EL2 against the commit before (`balance --seeds 8 --fc-runs 0`, the Jutland 4 seeds): 6 → 5 of 14
sides within twice the record, the record's loser losing more in 25 of 44 seeds both ways. EL2 reaches every ship, so
the scenarios without director fire move now too, by a little: Coronel Britain 1.0 → 0.8 of 3 (record 2, now LOW), Germany
1.4 → 1.0 of 5 (record 0, now in); Tsushima Russia 4.9 → 4.6, Japan 5.1 → 4.4 of 14. The Dogger Bank Britain 7.9 → 7.4,
Germany 8.9 → 9.9; Cape Sarych Russia 4.6 → 3.9. Jutland Britain 23.5 → 30.3 of 151 (record 14, now HIGH), Germany 38.5 of 99
both, British torpedo hits 14 → 24 and shell hits 2,986 → 4,065, German shell hits 2,945 → 2,435: four seeds, and Britain's
range was already 13-40, so not read as an effect of this step or tuned on.

### EL3 What a ship's plant feeds: the consumer table

From the research (`REQUESTS.md`, 2026-11-04): each consumer's battle load in kW and what it fails to. In code, a
`Consumers` table on the class, and a bench `shipgen electrics DESIGN...`: each consumer and the total beside the
recorded plants (Dreadnought a few hundred kW, Prince of Wales 2,640, Bismarck 7,910; a check, not a target). What the
designs know today comes to 20-165 kW a ship (searchlights, 1-10 kW a computer, `PowerKw`, which the compiler drops), so
drives, fans and pumps are most of it.

### EL4 The turret drive as a design block

Hand, steam hydraulic, electro-hydraulic, electric, by navy and period (design block, not a look: `looks-decorative-only`).
Its gains: the training rate and reload `DesignerPlaceholders.Gun` now gives every mount, its weight, its kW. An electric
trip then reaches only what runs on electricity: **a British 1916 turret on steam pumps doesn't notice one**, which today
it does (`ShipClass.Enclosed` is the first cut). A design format upgrade sets existing designs by year and navy, once.

### EL5 Each consumer reacts for itself

A computer without power falls to a hand plot, a stable vertical's gyro stops, remote power control to following pointers
or local, electric pumps stop and steam ones don't (`Drainage`, `PowerCap` as the supply today), lamps out slow repairs
(`RepairDarkPower`). Today a trip takes out directors whole, whatever they are.

### EL6 The trip rate from the load

`ShockCascade`, `ShockSectionPower`, `ShockLocalTrip`, their near-miss bands and `OwnGunBreaker` by a smooth curve on the
log of the battle load in kW, through 13's columns at two loads (WWI's about 300 kW; 1939-42's several MW). The shock
grade stays as its own axis, how well the gear is secured. `NonPen.Electrics` (yes or no on director firing) goes: no
hard switch. The checks: a 1916 hull given a table and electric drives trips at about the 1939 rate; a 1944 hull stripped
to hand gear about at WWI's.

### EL7 The plant itself

Generators as design facts: how many, steam or diesel, where; capacity sized from the load, and its weight in the design
(goldens change on purpose: `golden-update`, `ShipDesign.Version`; displacements shift, reported as `weights-follow-physics`
says). Then `PlantOut` becomes supply against load: partial loss sheds load, an emergency diesel outside the machinery
spaces keeps vital consumers (06 §1.1, the USN 2,200-ton destroyers). A UI row with the UI pass.

## Not in this plan

17's wireways and cable cuts, faults walking up through selective protection, lock-in as a doctrine, LVR and LVP controllers,
casualty power. They're the full service network; fire and damage control were kept rough on purpose (`rough-for-non-core`),
and this should be too unless a playtest asks for more. A hit cutting one consumer's cable stays what bursts already do to
the part it reaches.
