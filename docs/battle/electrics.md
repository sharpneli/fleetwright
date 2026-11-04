# Electrics: how electrified a ship is, and what losing it costs

Proposed and agreed 2026-11-04 (the user). Grew out of `bugs.md`'s "every mount of a ship out at once from one hit" and
the open item in `shells-armour-damage.md` E7 (the G0 grade gives 1916 ships 1939-42's rates). The research is
`research/simulation/electric-plant.md` (2026-11-04) and its appendices; `damage/17_service_network_faults.md` is the older
note on trips and cables, never built.

**Status:** EL0-EL4 done 2026-11-04. EL5 next.

## The rules (the user)

- **No era multiplier.** How much danger a ship is in from losing power follows from how electrified she is, and how
  electrified she is follows from what she gains by it. A modern digital computer on a 1916 hull isn't immune; a 1944
  ship stripped to hand gear isn't punished for her year.
- **Total, lasting power loss only when the generators are gone.** A trip is put right by isolating the fault and
  resetting, in minutes.
- **Electrification has a price:** the plant's weight (EL7), so a design doesn't simply take everything.
- **Total kW is the measure of how electrified she is, and it's a stand-in. Keep that in view** (below).

The research's headline agrees: what a ship loses with her power is what she runs on it. British and German capital ships
at Jutland both had plants of 750-2,040 kW, but only the German turrets stood on theirs.

## kW is an approximation

The trip rate follows the ship's battle load in kW (EL6) because kW can be added up from what she carries and checked
against recorded plants. It isn't what trips a breaker. What it can't see:

- **Protection:** fuses and knife switches against breakers; locked-in breakers, which pass a fault up to the generator
  (South Dakota); selective relays. 17 §5.2 has this properly.
- **Topology:** radial or ring main, split plants, whether dynamos run in parallel. A ring main severed cost Prince of
  Wales 6 of 8 secondary turrets with generators still running (02 §8).
- **Wiring against load:** a big drive is many kW on one feeder, a fire-control network a few kW on hundreds of circuits.
  kW overweights the first and underweights the second. A count of circuits might read closer, but there's no data for it.
- **The calibration points are estimates:** the rates at each load (EL6) come from 13 §3.4's columns, [EST] on very few
  cases, and the research's count of about 100 heavy hits at Jutland and the Dogger Bank, which only bounds the ship-wide
  rate (none in 100: at most 0.03). The era enters the curve only there, as loads with a rate each; no ship reads her year.
  The recorded plants check the kW, not the rates.
- **The kW itself is rough:** the research found only two period load tables, both from 1900-02, and no WWII battle-load
  analysis. Our consumer fits are [EST] from a few ratings (`ElectricLoad`), and the 1940s ships read low (EL3).

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

**The research's count bears it out** (electric-plant.md §7, appendix h). Six heavily hit capital ships of 1915-16, about 100
heavy hits: no ship-wide loss from a hit above water with no water in the plant; about 4 section losses (0.04 a hit,
0.01-0.10); local losses from most hits (0.3-0.5), mostly severed leads lasting the action. The ship-wide losses are water in
the dynamo or switchboard spaces (Lion twice, Szent István, Audacious), an underwater blast cutting the ring main
(Inflexible), or a ship's last minutes (Blücher). So three severities: concussion trips, re-made in minutes (Seydlitz,
Marlborough); severed leads, for the action; a generating group or the main distribution, until pumped or in port. A
partial loss is common: Seydlitz lost two forward generators, a third of her supply, and ran on the rest.

## Steps

### EL0 The record and this plan. Done 2026-11-04.

### EL1 A trip is short. Done 2026-11-04.

`CascadeMin` from 90% 1-10 min, 10% 30-90, to 90% 1-5 min, 10% 5-45: the record's cluster at 1-3, the board-fault cases at
8 and 16 (17 §6) and Lützow's lighting at 45, the longest found (electric-plant.md EL6.5). `NonPenTests`' 13 §5.3 test 6
restated (under 5 min about 90% of the time, none over 45; it asked under 10). The rate stays until EL6.

### EL2 A lasting loss is the plant's. Done 2026-11-04.

`ShipCondition.PlantOut`, worked out in `Functions.Evaluate`: no steam from any boiler room (`NoSteam`), or no engine room
working (the dynamos are driven by that steam and stand there; Ark Royal, Elbing). What stands on the plant by hand,
directors and searchlights out, for as long as it lasts; it ends when damage control or an outage's end brings a room back.
Rough stand-in until EL7 places generators: no partial loss (a ship with one engine room left has her full supply), no
diesels (so Szent István, her boiler rooms flooded and her lights on, comes out wrong; Bayern's all-diesel plant too), no
switchboard of its own (Lion's). Pumps and the dark already read `PowerCap`, the propulsion's share (EL5 separates them).

**The balance panel**, EL1-EL2 against the commit before (`balance --seeds 8 --fc-runs 0`, the Jutland 4 seeds; EL1's tail then
to 20 min): 6 → 5 of 14 sides within twice the record, the record's loser losing more in 25 of 44 seeds both ways. EL2 reaches
every ship, so the scenarios without director fire move now too, by a little: Coronel Britain 1.0 → 0.8 of 3 (record 2, now
LOW), Germany 1.4 → 1.0 of 5 (record 0, now in); Tsushima Russia 4.9 → 4.6, Japan 5.1 → 4.4 of 14. The Dogger Bank Britain 7.9 →
7.4, Germany 8.9 → 9.9; Cape Sarych Russia 4.6 → 3.9. Jutland Britain 23.5 → 30.3 of 151 (record 14, now HIGH), Germany 38.5 of 99
both, British torpedo hits 14 → 24 and shell hits 2,986 → 4,065, German shell hits 2,945 → 2,435: four seeds, and Britain's
range was already 13-40, so not read as an effect of this step or tuned on.

### EL3 What a ship's plant feeds: the consumer table. Done 2026-11-04.

`ElectricLoad` (`ShipClass.Electric`), from what she carries and how it's driven: each consumer's kW at full load and its
battle share, by the research's §3.10. Only what runs on electricity is in it. `shipgen electrics DESIGN...` lists it beside
the plant her namesake had. **It's rough on purpose:** training is the research's line (0.035 × t × °/s), elevation its fit; rammers and
hoists are fitted here to its ratings (kW for shell kg over reload seconds); fans 5 W/t and lamps 2.5 W/t, flat; the
searchlights keep `SearchlightTemplates.PowerKw` (about I × 65 V, the research's advice on 220 V ships, its supply [UNCERTAIN]).
Left out: what draws nothing in battle (cranes, winches, galley), so the connected figure runs under the period's.

| Design | Drive | Connected kW | Battle kW, day | Plant recorded | Battle/plant | Research's band |
|---|---|---:|---:|---:|---:|---|
| dreadnought | steam hydraulic | 257 | 190 | 350 | 0.54 | |
| tiger_1914 | steam hydraulic | 381 | 275 | 750 | 0.37 | 0.3-0.8 |
| seydlitz | electro-hydraulic | 1,156 | 628 | 1,200 | 0.52 | 0.3-0.7 |
| nassau | electro-hydraulic | 1,601 | 843 | 1,280 | 0.66 | 0.3-0.7 |
| borodino | electric | 669 | 357 | 738 | 0.48 | |
| kgv | steam hydraulic | 1,206 | 648 | 2,640 | 0.25 | |
| bismarck | electro-hydraulic | 3,183 | 1,646 | 7,910 | 0.21 | 0.4-0.8 |
| yamato | steam hydraulic | 1,626 | 905 | 4,800 | 0.19 | |

**The 1940s ships read about half of what they should**, and the bench shows why it's not just the fits: their mounts'
training and elevation are `DesignerPlaceholders.Gun`'s (a 1.9°/s 16" against Iowa's 4), and radar, air conditioning and
what else the plants of that time fed aren't in. The Great War is the first cut and reads in band; the rest waits for the
1930s-40s cut. EL6's top anchor has to be read knowing this.

### EL4 The drives as design blocks. Done 2026-11-04.

`fittings.turret_drive` (hand, steam hydraulic, electro-hydraulic, electric) and `fittings.auxiliaries` (steam, electric, her
steering and pumps), format 18: the step reads her navy and plant year once (`Fittings.TurretDriveBy`, the research's §4;
the plant's label is a rounded generation, so the German dreadnoughts' reads 1905); after that a ship reads her block, and a
design can carry whatever it likes. `ShipClass.Drive`: open and casemate guns by hand, a dual-purpose mount electro-hydraulic on
the mount (5"/38, 5.25"), any other gunhouse by her block. A trip reaches only what's `OnElectric`: **a British 1916 turret on
steam pumps doesn't notice one**, and falls to hand only with no steam (`NoSteam`).

Hand rate corrected: `HandGearShare` 0.5 (gear losses; the power drives were 4-5× the friction power), so a 606 t turret 0.08°/s
where the first cut had 0.16; the research finds no number for any heavy turret, and designers duplicated power drives
rather than trust hand gear. The reload's 3× stays, unbounded by any source.

**Not yet:** a USN-pattern turret without power stands on its brakes, its handwheels needing power, until hand gear is engaged
(a changeover delay [EST]); hand rates halved past 5° of list (5.25" mounts "could not be trained", Nelson's rate halved with
roll). The drives' weight and rates: no source found weights; the block changes no weight or rate yet. So a player has no
reason yet to pick one but the risk: **electrification still needs its gain**. Invincible's 1908-14 electric turrets, Bouvet's
1898 ones: designs carry the label's answer, editable. Japan's pump drive, KGV's: [UNCERTAIN], steam for now.

### EL5 Each consumer reacts for itself

Today a trip takes out directors whole, whatever they are, and a loss of power slows nothing else. From the research's §5:

- A DC table (Dreyer) keeps its solution with one man more, slower and less accurate, not dead; a clockwork clock doesn't
  notice. An AC synchro computer's outputs die; mounts go to local laying, or hold on brakes then manual.
- A director's receivers freeze; turrets with power or steam hydraulics lay by their own sights.
- A gyro's repeaters stop at once: 5-20 min after a short outage, 2-4.5 h from far off; own-course inputs go to hand. A stable
  vertical: hand level and firing key; re-erection 5-30 min [EST].
- Electric pumps stop and steam ones don't (`auxiliaries` against `PowerCap`, today's stand-in for the supply), steering goes
  by bus transfer, standby or steam, then hand. Lamps out slow repairs (`RepairDarkPower`), secondary lighting about 5%.
- Fans stop: engine rooms heat; a dynamo room can be driven out (Seydlitz's 72 °C) [EST].

### EL6 The trip rate from the load

`ShockCascade`, `ShockSectionPower`, `ShockLocalTrip` and `OwnGunBreaker` by a curve on the log of the battle load in kW,
interpolated and clamped; the shock grade stays its own axis, how well the gear is secured. `NonPen.Electrics` (yes or no on
director firing) goes: no hard switch. **Corrected by the research:** the plan had WWI's rates at about 300 kW, which is a
pre-dreadnought's battle load. The ships behind them had 750-2,040 kW plants and 300-1,000 kW of load:

| Battle load | Ship-wide | Section | Local |
|---:|---:|---:|---:|
| 100 kW | 0 | 0.02 | 0.25 |
| 300 kW | 0.002 | 0.04 | 0.30 |
| 1,000 kW | 0.005 | 0.06 | 0.40 |
| 5,000 kW | 0.02 | 0.15 | 0.50 |

A Borodino lands near the WWI rates, which her turrets' electrical faults at Tsushima bear out. The top anchor is untested and
the 1940s ships read low (EL3); the first cut doesn't reach it.

**Also from the research:**

- **Underwater hits trip power, and today they don't at all:** `Underwater` never calls `PowerOut`, though Inflexible, Marlborough,
  Seydlitz and Szent István are 4 of 4 with a section or ship-wide effect. A cascade on torpedo and mine shock at a far higher
  rate than shells [EST]; the shell roll stays low.
- **A local loss is usually a cut lead, not a trip** (Warspite, Princess Royal): half "tripped", minutes; half "cut", until a
  party mends it [EST].
- A "darken ship" order before action (Princess Royal): less lighting load, less fire from short circuits.

Checks: a 1916 hull given a table and electric drives trips at about the 1939 rate; a 1944 hull stripped to hand gear about at
WWI's. `damage-history` on the Jutland and Dogger Bank cases: at most 2 ship-wide losses from above-water shellfire in about 100
hits, 1-10 section losses, most outages under 5 min.

### EL7 The plant itself

Generators as design facts: how many, kW, prime mover (reciprocating, turbo, diesel), which space; recorded mixes in the research's
§1, EL7. Capacity about 1.0-1.5× the battle load (0.5× connected in the USN), and its weight in the design, **the price of
electrifying** (goldens change on purpose; displacements shift, reported as `weights-follow-physics` says; no per-kW weights were
found, diesels heavier than turbo sets). Then `PlantOut` becomes supply against load:

- a flooded dynamo or switchboard space blacks her out (Lion, check 11); a diesel set keeps the lights with the boiler rooms gone
  (Szent István, Lützow);
- a partial loss sheds load, in order: searchlights by day, pumps not fighting damage, hoists, last steering, fire control and lights
  [EST]; Seydlitz ran on two thirds;
- an emergency diesel, 4-5% of the plant (USN WWII), feeds steering first;
- generator steam is a boiler load (12.5 kg/kWh, 1906). A UI row with the UI pass.

## Not in this plan

17's wireways and cable cuts, faults walking up through selective protection, lock-in as a doctrine, LVR and LVP controllers,
casualty power. They're the full service network; fire and damage control were kept rough on purpose (`rough-for-non-core`),
and this should be too unless a playtest asks for more. A hit cutting one consumer's cable stays what bursts already do to
the part it reaches.
