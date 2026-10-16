# 10 — Damage control as one system: every threat, how it was handled, how to model it

*Research + model proposal, 2026-10-14. Widens `09_damage_control.md` (flooding) to all the major threats and, above all, to how they feed each other. Builds on `damage-model-research.md` (§8.5 fire, §9 dependency graph), `02_components.md` (components, casualties), `04_carriers.md` (avgas, hangars), `06_service_networks.md` (power, hydraulics, repair of services) and `07_magazine_explosions.md` (what happens after a magazine ignites). Not a decision document.*

*Reference implementation: `claude/dc_holistic_ref.py` (fire, smoke, cook-off, fire main, firefighting water, machinery abandonment, shared crews; imports `damage_control_ref.py` for flooding and hydrostatics). Results in §8.*

Markers: **[EST]** = my estimate or designer choice. **(?)** = weakly sourced.

---

## 0. Short answer

1. **Ships were rarely lost to one threat. They were lost to a chain.** The classic chain is in the Savo report. Hits started fires and cut the fire main, so there was no water for the hoses. The fires grew, and blowers drew their smoke into the fire rooms, which were abandoned. That cost steam and power, so the pumps stopped. Fires then reached the 5-inch hoists and magazines. Internal explosions holed the hull, and the ship listed and capsized hours later. A damage-control model is mainly a model of these links.
2. **The major threats** (§1):
   - flooding
   - fire
   - ammunition (cook-off, flash, magazines)
   - fuel vapour
   - smoke, toxic gas and heat
   - steam
   - loss of power and services
   - loss of steering and propulsion
   - firefighting water itself
   - hull-girder failure
   - crew casualties, including the damage-control organisation itself
   
   Each has its own timescale, from seconds (flash) to hours (fire reaching a magazine, slow flooding).
3. **Fire is fought in minutes or not at all.**
   - **Compartment fires:** a fog hose in a 2,600 ft³ (~74 m³) test space controlled the fire in 2 minutes and had it out in about 9 minutes (NRL, ex-USS Shadwell).
   - **Destroyer gasoline and ammunition fires:** controlled in 15–25 minutes.
   - **The first minute:** the destroyer war-damage report says one hose stream reaching the fire within a minute was often worth more than several arriving a few minutes later.
   
   In the model, the first hose at 1 minute instead of 6 cut a kamikaze fire on a destroyer from ~58 to ~26 minutes and removed the cook-offs.
4. **The fire main is the single most important damage-control system, and the most often hit.**
   - **Frequent rupture:** destroyers lost it again and again.
   - **Water poured into the ship:** a ruptured main keeps pumping water inside. Ralph Talbot took over 200 tons this way, and the list submerged openings that would otherwise have stayed above water.
   - **The first job is isolation.** In the model, isolating fire-main breaks comes first under any doctrine.
   - **The fixes:** looped mains, independent diesel fire pumps, handy-billies and jumper hoses.
5. **Firefighting water is flooding, high in the ship.** Free surface from firefighting water hurts stability, and shallow water has the same effect as deep. Examples: Ralph Talbot reached about 20° of list with GM about 0.75 ft negative. Illustrious listed 5° with blocked scuppers. Franklin's hangar water ran down the air intakes and flooded four boilers.
6. **Smoke, not flame, takes the machinery.** Ships lost their machinery spaces to drawn-in smoke and heat: Vincennes, Akagi, Franklin, Illustrious, Bunker Hill. Securing supply ventilation is the cheap fix; Franklin secured supply ventilation and left exhaust running. Ordering engineers to stay costs lives; Bunker Hill lost 99 of 500 engineering-space men killed or wounded.
7. **Ammunition near a fire is a clock** (§2.3):
   - **Exposed ordnance:** depth-charge fires led to blasts after 23 and 26 minutes. A bomb in a fuel fire went in about 90 seconds (Forrestal, 1967).
   - **Magazines below the protective deck:** hours, unless a flash path is open. Astoria's 5-inch magazine was reached through the hoists.
   - **Cures:** sprinkling (32 l/m²/min), flooding the magazine, or jettisoning the ammunition.
8. **Preparation beats reaction.**
   - Strip flammables (Savo).
   - Defuel and de-arm aircraft: Enterprise's kamikaze fire was out in under 30 minutes, while Bunker Hill's took about five hours.
   - Close up (Zebra or RN Red/Blue).
   - Disperse the repair parties and the conflagration station.
   - Test the sprinklers and the emergency diesels.
   
   In the model the post-Savo cruiser fit lost 4/12 against 11/12 for the 1942 fit, under the same hits.
9. **Priority doctrine matters when crews are short.** In the battleship test (torpedo plus three bomb fires, four parties), "fire-main breaks and fires near magazines first" put out all fires in 7/8 runs. "Flooding first" put out none, and the magazines reached the cook-off zone; only sprinklers saved them. List was identical, because counterflooding is done by DC Central, not by the parties. This matches the USN finding that fires normally had to be controlled before flood control could begin.
10. **Proposed model** (§7). It adds a fire/smoke/heat layer and a service layer to the flooding model of `09`. Every threat writes into the same compartment graph and draws on the same crews. The player sees the chain as a symptom → cause story.

---

## 1. Threat catalogue

| Threat | How it kills or disables | Timescale | How it was handled | What decided the outcome | Anchors | Detail |
|---|---|---|---|---|---|---|
| **Flooding** | Loss of buoyancy or stability | seconds (holed space) to hours (progressive) | Close up, counterflood, boundary, shore, plug, pump | Subdivision, closures, counterflood capacity | NC, Shinano, Seydlitz | `09` |
| **Fire** | Kills crew, destroys systems, lights the ship, spreads to ammunition, makes spaces uninhabitable | minutes (flashover) to hours (spread, burn-out) | Hoses (fog), foam, CO₂, sprinklers, boundary cooling, stripping | First-hose time, fire-main pressure, fuel load | Savo, Franklin, Bunker Hill, DD report | §2 |
| **Ammunition cook-off** | Explosions kill firefighters, hole the hull, spread fire; magazine → loss | ~1.5 min (bomb in fuel fire) to hours (protected magazine) | Sprinkle, flood magazine, jettison, cool | Distance from the fire, flash paths, sprinkler pressure | Forrestal, Abner Read, Isherwood, Astoria | §2.3, `07` |
| **Fuel / avgas vapour** | Delayed explosion far from the hit | 1–6 h | Drain and purge lines (CO₂), inert, ventilate, secure electrics | Line state, vapour spread through ducts | Lexington +1 h, Taihō +6.4 h | `04` |
| **Smoke, toxic gas, heat** | Spaces abandoned, men suffocated (ready rooms, engine rooms) | minutes | Secure supply ventilation, exhaust fans, breathing apparatus, rotate men | Ventilation doctrine, breathing sets | Vincennes, Akagi, Franklin, Bunker Hill, Illustrious | §3 |
| **Steam** | Scalding; blocks access to engineering spaces | instant | Isolate steam lines (engineers), asbestos suits | Valve access | Johnston: live steam blocked the after engineering spaces | §3 |
| **Loss of power / services** | No pumps, no fire main, no steering, no turrets | instant (breaker) to permanent | Reset, isolate, cross-connect, emergency diesels, casualty power | Redundancy, emergency diesels | South Dakota, PoW, Astoria | `06` |
| **Loss of steering** | Circling, collision, a sitting target | instant | Shift pumps, after steering, steam/hand steering, steer by engines | Location of the steering gear, power | Illustrious: electric steering failed, steam steering 8 min later, then lost; steered by engines | §3 |
| **Loss of propulsion** | Dead in the water; nothing works | minutes | Cross-connect, relight boilers, re-man | Unit machinery, smoke, flooding | Yorktown 23 kn ~1 h after boilers went out; Franklin dead in the water, towed | `02` |
| **Firefighting water** | GM loss + free surface → list, loll, capsize; flooded boilers | 30 min – hours | Isolate breaks, drain, eductors, cut drain holes, a hard turn | Drainage, scuppers, coamings | Ralph Talbot, Illustrious, Franklin, Normandie | §2.5 |
| **Hull-girder failure** | Ship breaks in two | instant to hours | Slow down, put the sea right, shift weight, shore | Location (amidships), sea state | 6 of 30 destroyer losses were structural (jackknifing) | `07` §3 |
| **Crew casualties & the DC organisation** | Fewer hands, slower everything; loss of DC Central | instant | Disperse parties and stations, cross-train, turn-to after action, dressing stations | Dispersal, training, stations under armour | Lexington DC central, Franklin conflagration station killed in both fires, Yorktown Repair V | §5 |
| **Shock** | Breakers trip, pipes crack, instruments fail | instant | Shock mounting, resets | Design | NC (slight), Johnston | `06` |

---

## 2. Fire

### 2.1 What burns

- **Fittings and stores.** Cork insulation, upholstered furniture, linoleum and unlocked clothing fed WWII fires; so did lubricating oil, paper, crates, cardboard, liquid paint and cable insulation ([Naval Gazing, Fire](https://www.navalgazing.net/Survivability-Fire)).
- **Savo.** Boats, aircraft, ready 5-inch ammunition, kerosene, bedding, papers, clothing, paint, life jackets, flags and lubricating oil all burned ([WDR 29](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-quincy-ca39-astoria-ca34-vincennes-ca44-war-damage-report-no29.html)). A cane fender fire on Vincennes was too hot to put out.
- **Pre-dreadnought era.** At Tsushima, Orel had 34 separate fires ([Scientific American](https://www.scientificamerican.com/article/with-the-baltic-fleet-at-tsushima)). Suvorov "left the line, ablaze fore and aft" about 30 minutes after firing began. Japanese HE shells against paint, wood and coal dust are the pre-WWI version of Savo.
- **Aircraft and gasoline** (`04`): fuelled and armed aircraft turn one bomb into a conflagration (Akagi, Franklin, Bunker Hill).
- **Ready-service ammunition, deck torpedoes, depth charges**: fuel that explodes (§2.3).
- **Bunker oil** is hard to ignite (`damage-model-research.md` §8.5).

**Fuel load for the model** (kg of wood-equivalent per m² of deck) [EST]:

| Space | Load |
|---|---|
| Accommodation | 45 |
| Wardroom | 55 |
| Stores | 70 |
| Bridge | 30 |
| Machinery | 12 |
| Magazines | 0 (handled separately) |

"Stripped for war" leaves ~45 % of the fittings load.

### 2.2 How a shipboard fire behaves

| Stage | Time | What happens | Anchor |
|---|---|---|---|
| Ignition → growth | 1–5 min | Fire grows; one hose or extinguisher puts it out | DD report "first minute" |
| Flashover | a few minutes | The whole compartment ignites; upper layer 500–600 °C | Shadwell 1996: flashover usually within ~2 min of entry with a straight stream |
| Fully developed | 20 min – hours | Burns until the fuel is gone; ~9 MW in a berthing compartment | Shadwell Phase VI: ~9.2 MW, ~1,000 °C overhead |
| Spread through open paths | ~1–2 min | Doors, hatches, ducts, blown bulkheads, hoists | Savo; NC flash through open WT doors |
| Spread through steel | ~10–40 min [EST] | Steel conducts heat; paint and contents on the far side ignite. Upward fastest, downward slowest | Shadwell Phase VI: after 20 min of a severe fire the next compartment's deck was hot enough to ignite things (270–335 °C) |
| Backdraft | on opening | Opening a sealed, cooled space can re-ignite it | Naval Gazing |
| Burn-out | hours | Fuel exhausted; spaces stay hot and smoky | Astoria's wardroom fires never controlled |

Boundary cooling stops spread through steel. In NRL tests about 2 l/min/m² cooled boundaries to 100 °C. The steam it raises shortens firefighters' stay time to about 7–23 minutes, so cool only when spread is imminent ([NRL Phase VI](https://apps.dtic.mil/sti/tr/pdf/ADA278790.pdf)).

### 2.3 Ammunition in or near a fire

| Ordnance | Time to react in a fire | Anchor |
|---|---|---|
| Bombs in a jet-fuel fire | ~90 s (first detonation) | Forrestal 1967; insulated bombs were later brought to 5–10 min, but the explosion was no less violent ([insensitivemunitions.org](https://www.insensitivemunitions.org/history/the-uss-forrestal-cva-59-fire-and-munition-explosions/)) |
| Depth charges in a deck fire | 23–26 min | Isherwood, Mullany ([DD report](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/destroyer-report-gunfire-bomb-kamikaze-damage.html)) |
| Smokeless powder | ignites at about 160–170 °C | [Wikipedia, Cooking off](https://en.wikipedia.org/wiki/Cooking_off) |
| Handling room next to a gasoline fire | minutes | Abner Read lost |
| Protected magazine, fire above it | hours | Astoria: fire went down through wardroom country into the 5-inch hoists; forward 5-inch magazines could not be flooded because the controls were inaccessible |
| Magazine opened by the hit itself | immediate; inrushing seawater may quench it | Killen, Stormes, Boise |

**Countermeasures:**
- **Sprinkle.** USN design density 0.8 gpm/ft² ≈ 32 l/m²/min. Recent tests suggest even less water would do ([firesafetysearch](https://www.firesafetysearch.com/sprinklers-in-ammunition-magazines-on-naval-vessels/)). Sprinklers need fire-main pressure; Savo showed they can fail under severe damage.
- **Flood the magazine.** This needs the valves to be reachable and costs the turret.
- **Jettison** ready ammunition, torpedoes and depth charges. Forrestal's crew pushed bombs over the side.
- **Cool** the ordnance with hoses.

The destroyer report concludes that a magazine explosion is unlikely if the sprinklers operate promptly or the magazine floods quickly. Four of its destroyer losses followed fire (Cushing, Monssen, Abner Read, Mahan).

### 2.4 Firefighting means

| Means | Capacity / performance | Needs | Notes |
|---|---|---|---|
| **Fire main** | Destroyer pumps fed ~30 (2,100 t) to ~50 (2,200 t) 1.5-in fog nozzles at once | Power or steam for the fire pumps; intact risers | Sectioned at GQ: Franklin used 8 sections and ~31 valve closures; the report recommended ~4 |
| **1.5-in fog nozzle / applicator** | ~60–95 gpm ≈ 14–21 t/h [EST range] | ≥ ~60 psi (fog heads unreliable below that, Franklin WDR) | Fog: better heat absorption, oil fires, protects the hose team |
| **Straight stream** | Long reach, poor cooling | — | Floods the ship; steam and re-flash; burns to the hose team (Shadwell) |
| **Foam** | Best agent for gasoline | Proportioners, tanks | Not available for big hangar fires until after Franklin; then 33 t of foam equipment per CV |
| **CO₂ portable** | Small and electrical fires | — | Soon exhausted (Savo); Johnston put out electrical fires in ~30 min with CO₂ |
| **Sprinklers / water curtains** | Magazines 32 l/m²/min; hangars | Pressure, intact control leads | Illustrious: blast shredded the curtains, and a short restarted a pump that flooded the ship |
| **Handy-billy (petrol)** | 60 gpm, one hose | 2 men to carry | Works with no power |
| **P-500 (petrol)** | ~500 gpm; DD report rates it "up to 1,000 GPM at low head" with its two 4-in eductors | 4+ men | Often failed through poor maintenance (DD report) |
| **Diesel emergency fire pump** | 1,000 gpm (heavy cruisers, post-Savo) | Its own space intact | Kept Franklin's forward section pressurised 18 h, the last 14 unattended |
| **Breathing apparatus** | Rescue breathers (WWII); OBA canisters later rated ~45 min ([Chemox](https://archief.therebreathersite.nl/Zuurstofrebreathers/USA/msa_chemox.htm)) | Few sets | Franklin: gas masks partly effective, rescue breathers too few and had to be started in fresh air |
| **Asbestos suits** | Reach fires behind heat | — | DD report; Johnston (steam) |
| **Boundary cooling** | ~2 l/min/m² | 2 men | Stops spread through steel |
| **Ship manoeuvre** | Hard turn spills burning fuel overboard | Speed, steering | Bunker Hill: ~70° turn |
| **Ships alongside** | Hoses, pumps, breathing gear, take off wounded | Calm-ish sea, escort risk | Santa Fe alongside Franklin 1050–1225; Wilkes-Barre alongside Bunker Hill |

### 2.5 How long fires took

| Case | Fire | Time | Why |
|---|---|---|---|
| Shadwell compartment tests | ~74 m³ berthing fire | 9.1 min fog, 10.9 min straight stream | Trained team, full pressure |
| Destroyers (Zellars, Walke, Stormes, Hadley) | Gasoline + ammunition | 15–25 min | Drilled crews, independent risers (Hadley) |
| Johnston | Forward fires after hits 10–11 | 10 min | 2 hoses + sprinklers |
| New Mexico (kamikaze, 1945) | Funnel area, boiler room | 21 min | ([H-Gram 048](https://www.history.navy.mil/about-us/leadership/director/directors-corner/h-grams/h-gram-048/h-048-1.html)) |
| Enterprise (kamikaze, 1945) | Hangar | < 30 min | Aircraft defuelled and de-armed beforehand |
| Bunker Hill | Deck park + hangar, 2 kamikazes | ~5 h (1004 → ~1500) | Fuelled aircraft; ships alongside |
| Ralph Talbot | Forward superstructure | ~7 h | Broken fire main poured water into the ship; CO₂ and buckets |
| Franklin | Hangar, ordnance | Out of control for 5 h; major fires ~10 h; some until 22 Mar | Armed and fuelled deck; firemain lost except diesel section |
| Illustrious (Jan 1941) | Hangar | ~13 h | Curtains blown, sprinklers damaged, smoke |
| Astoria | Hangar, wardroom, ready ammo | Never; capsized ~10 h later | Fire main lost early, power lost, magazines unreachable |
| Kaga, Akagi, Sōryū | Hangar | Never | Fuelled and armed aircraft; Sōryū lost fire mains within 10 min |

### 2.6 The fire-main lessons

- Ruptures are frequent (Ralph Talbot, Albert W. Grant, Abner Read, Killen, Hugh W. Hadley).
- **Isolating the break decides it.** Killen and Grant isolated theirs. Abner Read could not, lost control of the fire, and was lost. Hadley fought on with emergency pumps feeding independent risers (DD report §2-43).
- A non-looped main loses everything beyond the break. Savo's fix for cruisers was:
  - loops with cross-connections;
  - a 1,000 gpm diesel fire pump forward of the machinery;
  - 100 kW diesel generators fore and aft;
  - six handy-billies and twelve submersibles.
  
  Destroyers got two emergency electric fire pumps with independent risers instead of a loop.
- At GQ the main is split into sections. Too many sections means too many valves to set and find (Franklin).

### 2.7 Firefighting water as a stability threat

- **Ralph Talbot.** Her fire pumps poured over 200 t through the breaks. Water trapped above the main deck gave a list of about 20°, with GM about 0.75 ft negative. She was immobilised about 9 h, largely because the flooding was misdiagnosed.
- **Illustrious.** Sprinkler and hose water could not drain because the scuppers were blocked. It gave about 5° of list, relieved only by cutting a drain hole in trunking ([armouredcarriers.com](https://www.armouredcarriers.com/adm26783/2014/10/16/illustrious-january-10-damage-report-bomb-shell)).
- **Franklin.** Hangar water drained through torn air intakes into the plenum chambers and flooded boilers 1–4. The fix was 12-in coamings on ducts, 36-in plenum coamings and 6-in drains ([WDR 56](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-franklin-cv-13-war-damage-report-no-56.html)).
- **Normandie** capsized at her pier from firefighting water ([Naval Gazing, Flooding](https://www.navalgazing.net/Survivability-Flooding)).

The model rule is simple:
- Every hose adds ~14–21 t/h, less ~25 % evaporated on a working fire [EST], into the compartment it plays on.
- That water is weight at height (it costs GM) and slack (free surface).
- It drains only through deck leaks, scuppers (if not blocked), pumps and eductors.

---

## 3. Smoke, heat, steam, steering, propulsion

**Smoke and heat**
- **Vincennes** lost steam at about 0205 when damaged blowers drew smoke and flame into the fire rooms (WDR 29).
- **Akagi's** engine-room crews suffocated from heat drawn in through the intakes before they could be recalled ([USNI 1955](https://www.usni.org/magazines/proceedings/1955/june/five-fateful-minutes-midway)).
- **Franklin** secured supply ventilation and left exhaust running; men still collapsed at their stations, and all machinery spaces were evacuated by about 0930.
- **Bunker Hill.** The chief engineer kept the engineers at their posts in suffocating smoke, so the boilers and fire-main pressure kept going; 99 of 500 engineering-space men were killed or wounded ([History Hit](https://www.historyhit.com/the-crippling-kamikaze-attack-on-uss-bunker-hill/)). Many in the ready rooms died of smoke inhalation.
- **Illustrious.** Thick black smoke mixed with extinguisher fumes filled the boiler rooms within about 10 minutes; the crews worked on with wet rags.

**Model rule:**
- Smoke is a 0–1 level per compartment. Fire produces it.
- It moves through openings, and through the supply ventilation into machinery intakes while the fans run.
- It vents quickly topside and slowly below.
- Above ~0.5 a space needs breathing sets to work in. Above ~0.6 for a few minutes a machinery space is abandoned, unless the captain orders "stay" (fewer abandonments, more dead).

**Steam.** On Johnston a hit cut the main steam line, and live steam blocked access to the after engineering spaces. Repair parties used asbestos suits and breathing apparatus. Model: a machinery hit may cause a steam leak, which makes the space untenable for 10–30 minutes until engineers isolate it [EST].

**Steering.** Illustrious, Jan 1941 (same source):

| Time | Event |
|---|---|
| ~1255 | Electric steering fails |
| 1303 | Steam steering takes over |
| 1330–1350 | Steering lost again; the ship turns in circles |
| 1434 | Steered by main engines until Malta |

The later cause was a solenoid valve jammed open, so oil escaped and the steam steering failed. Modern procedure is the same in shape: shift steering pumps, sound the alarm, take control in after steering ([Naval Safety Command](https://navalsafetycommand.navy.mil/Portals/100/Documents/LL%2021-17%20Shipboard%20Steering%20Casualties.pdf)).

Model ladder [EST]:

| Mode | Time to take over | Rudder speed | Needs |
|---|---|---|---|
| Bridge | — | full | — |
| After steering, local | 2–5 min | full | Men there, phones |
| Hand or steam | 10–20 min | slow | — |
| By engines | — | turn ≈ 20–40 % | ≥ 2 shafts |

**Propulsion.** See `02` and `06`. A smoke-filled or flooded engineering plant is the commonest way to end up dead in the water, which then stops the fire pumps and drainage.

---

## 4. The couplings, and the death spiral

```
 hit ─┬─► fire ──┬─► heat through steel / open doors ──► more fire ──► ready ammo cook-off ─┐
      │          ├─► smoke ─► (supply fans) ─► machinery abandoned ─► no steam/power ─┐    │
      │          └─► magazine heats ─► needs sprinkler pressure or flooding valves     │    │
      ├─► fire-main rupture ─► no water at the hoses   AND  water pouring into ship   │    │
      ├─► steam leak ─► machinery abandoned                                            │    │
      └─► hole / sprung bulkhead ─► flooding (09)                                      │    │
                                                                                        ▼    │
      no power ─► no fire pumps, no drainage pumps, no sprinklers, darkness slows DC ◄──┘    │
      hose water + rupture water ─► GM loss + free surface ─► list ─► holes submerge          │
      explosions ─► repair-party casualties, holes in the hull, more ruptures ◄───────────────┘
      ONE crew pool for all of it; DC Central itself can be hit
```

Each arrow is a historical case: Savo, Franklin and Akagi for the smoke path; Ralph Talbot and Abner Read for the fire main; Abner Read and Astoria for ammunition; Lexington and Franklin for DC Central. The **breakers in the chain** are what doctrine and design aim at:

- emergency diesels and diesel fire pumps;
- loops and independent risers;
- isolation drills;
- securing supply ventilation;
- coamings and drains;
- sprinklers;
- stripping flammables, defuelling, jettisoning;
- dispersed parties;
- turn-to after the action.

---

## 5. Organisation, people and doctrine

### 5.1 Priorities

The USN late-war order, as the war damage reports imply:
1. **Magazines threatened**: sprinkle or flood (DC Central).
2. **Fire-main breaks**: isolate, because without water nothing else works and the break is also flooding the ship.
3. **Fires** next to ammunition, gasoline or magazines.
4. **Counterflood** to remove list (DC Central, in parallel; NC removed it within 6 minutes).
5. **Flooding boundaries**: close, shore.
6. **Other fires**, then boundary cooling.
7. **Restore power and steering** (`06`).
8. **Dewater**, plug and patch, restore buoyancy and trim (`09`).

The destroyer report states it outright: fires normally must be brought under control before flooding control can begin.

### 5.2 Preparation (what the captain decides before the first hit)

| Measure | Effect | Cost | Anchor |
|---|---|---|---|
| Strip flammables | Less fire load, slower growth | Habitability, morale | WDR 29; Naval Gazing |
| Defuel / de-arm aircraft, drain avgas lines | Turns a hangar holocaust into a 30-min fire | Tempo | Enterprise vs Bunker Hill 1945 |
| Fly off or jettison cruiser floatplanes before night action | Removes a beacon and a fire source | Scouting | WDR 29 (allowed at the CO's discretion) |
| Material condition Zebra / Red-Blue | Less progressive flooding, smoke and fire spread | Slower movement, hot spaces | `09`; RN "close all red and blue openings" (CAFO 813/43) |
| Fire main sectioned, emergency pumps lit off | Water survives the first hits | Valve setting time | Franklin |
| Supply ventilation secured at GQ or on fire | Smoke stays out of machinery | Heat in machinery and magazines | Franklin |
| Repair parties dispersed, conflagration station protected | Survives single hits | Men | Yorktown, Franklin |
| Breathing sets and suits issued | Can attack smoke-filled spaces | Stores | Franklin, DD report |
| Ready-service ammunition limited | Fewer cook-offs | Rate of fire | WDR 29 recommendation (rejected) |

### 5.3 People

- **Repair parties.** Battleships and large carriers had 5–6 area parties; destroyers had about 3 of ~10 men [EST] (`09` §5).
- **The rest of the crew.** After the action, gun crews and others turn to. In the model, extra hose parties of lower skill join 15 minutes after the last hit [EST].
- **Losses among firefighters.** Each explosion hits the firefighting parties (Akagi). Franklin's conflagration-station crew was killed in both fires. One of Illustrious's electrical repair parties lost 3 of 4 men. Lexington's vapour explosion destroyed her damage-control facilities.
- **Stay time.** In heat and smoke men last ~7–23 minutes (NRL Phase VI) and must be rotated. The model applies a ×0.8 duty factor.
- **Medical.** The medical battle bill attaches corpsmen to repair parties and disperses dressing stations, mostly behind armour. Repair-party men get first-aid training. The medical officer posts at central station with the first lieutenant ([USNI 1945](https://www.usni.org/magazines/proceedings/1945/january/ships-medical-officer-prepares-battle)). Franklin had about 300 men and 100 officers fit for duty the next day, out of 603 men and 103 officers then aboard. In the game, wounded who are not treated become dead over hours, and casualties drain the damage-control pool.

### 5.4 Era and nation

| Era | Damage control | Typical failure |
|---|---|---|
| 1890–1905 | Steam fire pumps, hand pumps, collision mats; wooden fittings and paint; little organisation | Topside fires from HE (Tsushima: Orel 34 fires), capsizes through open ports |
| WWI | RN: no unified DC; KM good closure discipline and magazine flooding (Seydlitz) | Flash (Jutland), open doors (Audacious) |
| 1939–42 | USN DC Central, repair parties, sound-powered phones; RN Damage Control Boards on large ships; IJN DC as a side duty | Savo, Midway carriers, Ark Royal, Lexington |
| 1943–45 USN | Fog nozzles and fire schools, emergency diesels, casualty power, stripping, CO₂ purging, foam, salvage parties | Kamikaze gasoline fires, but most ships saved |
| 1943–45 RN | Unified system (CAFO 813/43), Red/Blue openings | — |

The RN source says unified damage control did not arrive until 1943 ([commsmuseum](https://www.commsmuseum.co.uk/dykes/damagecontrol/damagecontrol.htm)).

---

## 6. Repair and response catalogue (fire, ammunition, services)

Flooding tasks are in `09` §4 and electrical/hydraulic tasks in `06` §5. New tasks here. All times are for a trained party, before the multipliers (darkness ×1.6, list, skill); most are [EST].

| Task | Effect | Men | Time | Needs | Anchor |
|---|---|---|---|---|---|
| First hose (fire watch / locker on scene) | Stops a growing fire | 2–4 | ~1 min | Pressure at the plug | DD report |
| Hose attack (fog) | Puts out a ~150 m² fully developed fire in ~9 min per hose | 4 per hose | Ongoing; muster ~2 min plus travel | Pressure; breathing sets in smoke | Shadwell |
| Boundary cooling | Stops spread through steel | 2 | Ongoing | Water | NRL Phase VI |
| Isolate fire-main break | Restores pressure elsewhere, stops the water | 2 | ~3 min once found; finding takes 1.5–8 min, longer in smoke | Valve access | DD report §2-43 |
| Jumper hose | Half pressure to a section with a cut riser | 3 | ~6 min | A pressurised plug nearby | — |
| Rig handy-billy / P-500 for firefighting | Water with no power | 2–4 | 5–10 min | Fuel, suction | WDR 29 |
| Sprinkle magazine | Prevents cook-off | DC Central | ~1 min (remote) | Pressure ≥ ~60 psi, control leads | WDR 29, Franklin |
| Flood magazine | Removes risk; turret lost; weight | DC Central or 2 men at the valves | 5–15 min to fill [EST] | Valves reachable | Seydlitz, Astoria |
| Jettison ammunition, torpedoes, depth charges | Removes cook-off fuel | 6 | ~8 min per mount | Access | Mogami (`02`), Forrestal |
| Secure supply ventilation | Smoke stays out of machinery | DC Central | ~1 min | — | Franklin |
| Hard turn | Spills burning fuel off the deck | Bridge | ~1 min | Steering, speed | Bunker Hill |
| Re-man an abandoned space | Power/steam back | Engineers | When smoke and fire are gone, then a restart | — | Franklin re-manned and relit boilers ~2230 to 0815 |

---

## 7. Proposed game model

### 7.1 State added to each compartment (on top of `09`)

| Field | Meaning |
|---|---|
| `use` | Accommodation, stores, machinery, magazine, hangar, … (sets fuel load) |
| `fuel` | MJ remaining |
| `I` | Fire intensity, 0–1 |
| `soak` | Heat soak from neighbours, 0–1 |
| `smoke` | 0–1 |
| `gas` | Gasoline/avgas, tonnes |
| `ready` | Ready ammunition / warheads, tonnes |
| `rheat` | Heat in the ready ammunition |
| magazine block | heat, sprinkling, flooding, flooded, control-valve location |
| `abandoned`, `steam_until` | Machinery state |
| `cooled_until`, `jumper` | Firefighting state |

### 7.2 Per-step rules (one step per 1–2 s of game time; parties re-plan every 30 s)

1. **Growth.**
   - `dI = r·I·(1 − I/Imax)·max(0, 1 − hoses)`, with r = 1/1.5 min (unstripped) or 1/3 min (stripped).
   - `Imax` is 0.65 in a closed-up space (the fire is oxygen-limited) and 1 if the space is open.
   - Gasoline sets I = 1 and keeps re-flashing at half the hose effect. [EST]
2. **Suppression.** `dI −= hoses · (1/5 min) · √(150/area) · (0.3 + 0.7 I)`, with hoses already scaled by pressure (fog, full pressure). With no pressure, CO₂ and buckets count as 0.15 hose per party.
3. **Burn.** HRR = I · min(0.06 MW/m² · area, 20 MW). Fuel falls by HRR, and the fire dies when the fuel is gone.
4. **Hose water.** 17 t/h per hose, less 25 % evaporated if I > 0.3, into the compartment.
5. **Spread.** A neighbour of a fire with I > 0.8 gains `soak += dt/T`:

   | Path | T |
   |---|---|
   | Open, sprung or failed boundary | 1.5 min |
   | Upward through steel | 12 min |
   | Sideways through steel | 25 min |
   | Downward through steel | 40 min |

   Boundary cooling divides the gain by ~7. At soak 1 the neighbour ignites with p = 0.6 ("something burnable against the hot steel").
6. **Smoke.** Produced at I per minute. Exchanged through links in proportion to their opening. Supply fans pull superstructure smoke into the machinery below unless secured. Vented at 0.6/min topside and 0.05–0.15/min below.
7. **Machinery.** Abandoned on fire (I > 0.3), steam, water deeper than 2 m, or smoke above 0.6 for 5 minutes (0.9 if ordered to stay). Re-manned when clear.
8. **Power.** Power is on while any machinery space or the emergency diesel is working.
9. **Fire-main pressure at a plug.**
   - Sources: main fire pumps (need power), or a diesel fire pump at 0.6.
   - An unisolated break in the same section gives 0.
   - A single (unlooped) main: 0 beyond the break, 0.4 elsewhere.
   - A looped main: 0.75 elsewhere.
   - A jumper hose gives 0.5.
10. **Fire-main break.** Pours 80–200 t/h into its compartment while the pumps run, until isolated.
11. **Ready ammunition.** `rheat += I·dt/15 min` (×0.3 under hoses). Above 1, cook-off events at 0.3/min: casualties to parties present, neighbours' soak +0.4, 30 % chance of a fire-main rupture, 35 % chance (inside the hull) of a 0.1–0.6 m² hole in the side.
12. **Magazines.** Heat from a fire above: 20 minutes through an open flash path, 3 hours through steel; minutes if the fire is inside. Sprinkling cools them, and stops when pressure falls below 0.3. Flooding fills the magazine fraction (~35 % of the compartment) through sea valves. Heat above 1 without sprinkling gives a detonation chance, which hands over to the `07` model.
13. **Hydrostatics.** As in `09`, plus:
    - the height of added water: `GM₀ = (Δ·GM + Σw·(KG − z_w)) / (Δ + Σw)`;
    - a wall-sided righting arm, so negative GM gives a loll angle (Ralph Talbot), not an instant capsize;
    - a solver that settles on the stable root.
14. **Parties.**
    - Task priority by doctrine.
    - Travel 0.6 min per section, ×1.5 in smoke.
    - Muster time before the first water.
    - Hoses limited to `men/4` (at most 3) and to available breathing sets when the space is smoky (topside fires excepted).
    - Turn-to parties after the action.

### 7.3 Player decisions (all historical)

- Secure supply ventilation (cooler, smoke-free machinery) or keep it (cooler magazines and men).
- Order engineers to stay at their posts in smoke (keeps steam and fire pumps; costs lives).
- Sprinkle or flood a threatened magazine (flooding loses the turret and adds weight).
- Jettison ready ammunition, torpedoes or depth charges.
- Fight the fire, or isolate the main first.
- Hard turn to spill burning fuel (carriers).
- Ask an escort to come alongside (hoses, pumps, wounded) at the risk of the escort.
- Speed: fanning the fire and the bow wave vs reaching safety.
- Before battle: strip, defuel, Zebra, fly off the floatplanes.
- Doctrine: fire-first vs flood-first; how many men leave the guns to fight fires during the action.

### 7.4 What the player sees

- **Compartment schematic.** Water depth (blue), fire intensity (orange), smoke (grey hatch), and icons for magazine heat, fire-main breaks, abandoned spaces and parties.
- **Service strip.** Power, fire-main pressure by section, pumps, steering mode.
- **Doom clocks.** Magazine heat, counterflood capacity left, GM trend.
- **Symptom → cause log.** For example: "No water at hoses aft ← fire main cut at frame 112, not isolated ← Repair 3 fighting fire near aft magazine".

### 7.5 Performance and LOD

The reference code runs a 12-hour cruiser night in ~10 s of plain Python at 2 s steps (~50 compartments, ~120 links). In a real engine:
- per-compartment arrays with a fixed-step update are trivially cheap;
- off-screen ships use the function-state LOD from `06` §4, plus a per-ship fire count, total water, and an "uncontrolled" flag that grows or decays with DC quality and fire-main state.

---

## 8. Reference model results (`dc_holistic_ref.py`)

| Scenario | Model | History / anchor |
|---|---|---|
| **Heavy cruiser, night surprise, ~25 shell hits in 15 min**; 1942 fit (unstripped, single main, fuelled floatplanes, supply fans on, 1 breathing set per party, magazine controls in the wardroom country) | Lost 11/12 runs, capsized, median ~2.5 h; fires never out; power lost in 6/12; ~17 cook-offs per run | Astoria: power lost ~20 min after the first hits, fires never controlled, capsized ~10 h after. Quincy and Vincennes sank within an hour (also torpedoed) |
| Same hits, **post-Savo fit** (stripped, looped main, 1,000 gpm diesel pump, emergency generator, secured supply fans, 6 breathing sets) | Lost 4/12; all fires out in a median ~1.8 h; power never lost; ~4 cook-offs per run | Later USN cruisers survived heavy gunfire with fires controlled |
| **Destroyer, HE-shell fire** in a deckhouse; first hose at 1 vs 6 min | Out in 3 vs 9 min; no spread | Shadwell 9–11 min for a developed compartment fire |
| **Destroyer, kamikaze + bomb** amidships; first hose at 1 vs 6 min (20 runs each) | Out in median 26 vs 58 min; 2.1 vs 3.5 burning spaces at peak; cook-offs 0 vs 1.05 per run | DD report 15–25 min with drilled crews; "first minute" rule; Abner Read lost when control was lost |
| **Ralph Talbot-type** fire-main break forward between decks, one submersible pump; isolated at 3 min vs found after 2.5 h | 0 t and no list vs up to ~150 t and ~12° list (effective GM about −1 m while the water is slack) | >200 t, ~20° list, GM ≈ −0.75 ft |
| **Battleship in action, torpedo + 3 bombs**, 4 parties of 30, no turn-to (8 runs) | "Breaks and fires near magazines first": all fires out within 4 h in 7/8 runs, ~18 cook-offs per run, hottest magazine 0.27. "Flooding first": 0/8 out, ~35 cook-offs, magazines in the cook-off zone (saved only by sprinklers); list identical (≤1.5°) | DD report: fires before flooding; NC: counterflooding by DC Central in 6 min |

**What the runs say:**
- **The design and doctrine changes after Savo are worth more than crew skill alone.** The same hits killed 11/12 ships in the 1942 fit and 4/12 in the post-Savo fit.
- **Time-to-first-hose is the strongest single lever on gasoline fires.** It matters less for slow-growing fires.
- **Isolating the fire main is a flooding task as much as a fire task.**
- **Counterflooding stays with DC Central**, which is why sending parties to fight fires first costs almost nothing in list.
- **The model is harsher on the 1942 cruiser than history (hours vs ~10 h).** In the model, side holes from internal explosions and two waterline shell holes are never patched while the parties fight fire. Tune the hole-from-cook-off chance (0.35) and the waterline holes per class.
- **Battleship fires burn too long.** With four parties and nobody released from the guns, they last hours. That is plausible only for unstripped 1942 ships; real battleships also used secondary-battery crews.

---

## 9. Uncertainties

- **Fire-spread times through steel** (12/25/40 min) and **growth rates** are estimates. The single anchor is NRL's severe-fire boundary test. The Darwin, Leonard & Scheffey paper (IMAS 94) and NRL's "fire dynamics" companion report have the curves.
- **Hose capacity.** 60–95 gpm is the range for 1.5-in nozzles; WWII all-purpose nozzle flows were not confirmed.
- **Cook-off times** for WWII naval rounds in a deck fire: only the destroyer-report depth-charge times and the 1967 Forrestal numbers are sourced.
- **Magazine heating through decks (3 h)** is fitted loosely to Astoria's narrative, not measured.
- **Breathing apparatus.** Endurance of WWII rescue breathers is not found. The 45-minute figure is the postwar Chemox OBA.
- **Repair-party sizes and turn-to numbers** for large ships are not sourced.
- **The Savo scenario hits are invented**, chosen to resemble Astoria's damage. Losses come out faster than history (§8).
- **The Franklin WDR flooding/stability section** gave drainage details but no tonnage of firefighting water.

---

## 10. Sources

- USN War Damage Reports (NHHC):
  - [Savo cruisers WDR 29](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-quincy-ca39-astoria-ca34-vincennes-ca44-war-damage-report-no29.html)
  - [Destroyers: gunfire, bomb, kamikaze](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/destroyer-report-gunfire-bomb-kamikaze-damage.html)
  - [Franklin WDR 56](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-franklin-cv-13-war-damage-report-no-56.html)
  - [North Carolina WDR 61](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-north-carolina-bb55-war-damage-report-no-61.html)
- NRL ex-USS Shadwell:
  - [Phase VI boundary cooling (1994)](https://apps.dtic.mil/sti/tr/pdf/ADA278790.pdf)
  - [Offensive fog attack (NHRC 96-22)](https://apps.dtic.mil/sti/pdfs/ADA319156.pdf)
  - [Fire spread through steel bulkheads (IMAS 94 abstract)](https://trid.trb.org/View/449578)
- [NHHC H-Gram 048: Bunker Hill, New Mexico, Enterprise](https://www.history.navy.mil/about-us/leadership/director/directors-corner/h-grams/h-gram-048/h-048-1.html) · [History Hit: Bunker Hill](https://www.historyhit.com/the-crippling-kamikaze-attack-on-uss-bunker-hill/)
- [Illustrious damage report, 10 Jan 1941 (armouredcarriers.com)](https://www.armouredcarriers.com/adm26783/2014/10/16/illustrious-january-10-damage-report-bomb-shell)
- [USNI 1955: Five Fateful Minutes at Midway](https://www.usni.org/magazines/proceedings/1955/june/five-fateful-minutes-midway) · [USNI 1945: Ship's medical officer prepares for battle](https://www.usni.org/magazines/proceedings/1945/january/ships-medical-officer-prepares-battle) · [USNI 2019: Fighting for Survival](https://www.usni.org/magazines/naval-history-magazine/2019/december/fighting-survival)
- [Naval Gazing: Fire](https://www.navalgazing.net/Survivability-Fire) · [Flooding](https://www.navalgazing.net/Survivability-Flooding)
- [Forrestal fire and munition explosions](https://www.insensitivemunitions.org/history/the-uss-forrestal-cva-59-fire-and-munition-explosions/) · [Cooking off (Wikipedia)](https://en.wikipedia.org/wiki/Cooking_off)
- [Sprinklers in ammunition magazines on naval vessels](https://www.firesafetysearch.com/sprinklers-in-ammunition-magazines-on-naval-vessels/)
- [MSA Chemox OBA](https://archief.therebreathersite.nl/Zuurstofrebreathers/USA/msa_chemox.htm)
- [Naval Safety Command: shipboard steering casualties](https://navalsafetycommand.navy.mil/Portals/100/Documents/LL%2021-17%20Shipboard%20Steering%20Casualties.pdf)
- [RN damage control history (commsmuseum)](https://www.commsmuseum.co.uk/dykes/damagecontrol/damagecontrol.htm)
- [Scientific American: With the Baltic Fleet at Tsushima](https://www.scientificamerican.com/article/with-the-baltic-fleet-at-tsushima)
