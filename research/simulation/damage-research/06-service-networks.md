# 06 — Service networks: hydraulics, electrics, electronics. Designer outputs and damage/repair models
Status: draft    Updated: 2026-10-04    Request: -

*Preliminary research and design options, 2026-10-04. Builds on [`02-components.md`](02-components.md) §8–9 (electrical, hydraulics) and [`damage-model-research.md`](../damage-model-research.md) §9 (dependency graph) and §13 (generator outputs). Not a decision document.*

**Question:** warships live or die by power, hydraulic pressure and signal circuits. Simulating them wire by wire is too expensive. What must the ship designer emit so a cheaper model can still break them believably, and let crews fix them? And what should that model look like?

Markers: **[EST]** = my estimate or designer choice, not sourced. **(?)** = weakly sourced.

---

## 0. Short answer

1. **Model routes, not wires.** Every service (power, hydraulics, fire-control data, phones, fire main) travels between a few *nodes* (generators, switchboards, pumps, consumers) along *routes*. A route is just the ordered list of compartments it passes through. The designer computes routes once. At runtime a hit on a compartment damages every route segment that passes through it. That is the whole trick: geometry is precomputed, runtime is a small graph.
2. **One engine, several layers.** Electric power, hydraulics, steam (already in [`powerplant-model.md`](../powerplant-model.md)), fire main, data links and command links all use the same node/route machinery. They differ only in what "working" means: connected (data), connected + enough capacity (power), connected + enough pressure (hydraulics, fire main), and whether a break causes side effects (a broken fire main floods; a hydraulic oil leak burns).
3. **Most electrical casualties were transient, not destruction.** Breakers tripped, transfer switches misbehaved, fuses blew, and power came back in 1–3 minutes (South Dakota). Model **fault states** (tripped / faulted / severed / destroyed / flooded) with very different repair times, not HP.
4. **Redundancy is the design lever, and the designer should show its value.** At design time the generator can find single points of failure ("lose compartment 14 → no steering power") and show them to the player. The same analysis is the AI's design evaluator and the post-battle "why did this happen" explanation.
5. **Repair is a crew-agent problem.** Damage-control parties travel through the compartment graph, are blocked by fire/flooding/darkness, need communications to be dispatched, consume spares and cable, and choose between isolating, jumpering (casualty power), resetting and replacing.

---

## 1. What actually happened (system-specific evidence)

General component failures are in `02` §8–9. New or sharpened findings for this topic:

### 1.1 Electrical

| Mechanism | Evidence | Model meaning |
|---|---|---|
| **Protection cascade** — a fault downstream trips a breaker too far upstream | South Dakota: superstructure hits shorted FC and IC cables, the short overloaded the IC switchboard feeder and tripped **main generator breaker No. 1**. The bus transfer moved the load to the emergency diesel board, whose **fuses then blew** because the short was still there. All FC and IC circuits ship-wide were out for **~3 min**. Fix: selective relays (type PQ) or time-delay dashpots on generator breakers ([WDR 57](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-south-dakota-bb57-war-damage-report-no57.html)). | A fault's blast radius depends on **protection selectivity**, a tech/design attribute. Poor selectivity turns a local cable cut into a ship-wide blackout. |
| **Own-ship shock** | South Dakota, before combat: shock from turret III firing astern closed an automatic bus transfer (ABT) contactor and paralleled two switchboards out of phase; contacts welded, a feeder to the after 5″ director ruptured, a generator breaker tripped, ~1 min power loss aft. Fix: ABTs replaced with manual ones (same WDR). | ABTs give redundancy and a new failure mode. Small chance per heavy salvo; mostly flavour. |
| **Steam dependency of generators** | Prince of Wales: 6 × 330 kW turbo-dynamos + 2 × 330 kW diesel dynamos. After the first torpedo, steam from Y boiler room failed and with it Y machinery room's turbo-generator; only **3 of 8** were running at 12:19. Steering, pumps, ventilation, IC and the after 5.25″ turrets lost power. Steam was later cross-connected from A boiler room ([Death of a Battleship](https://pacificwrecks.com/ship/hms/prince-of-wales/death-of-a-battleship-2012-update.pdf)). | Generators are consumers on the steam layer. Cross-connecting steam is a repair action. |
| **Emergency diesels outside the machinery spaces** | USN 2,200-ton destroyers had **two emergency diesels, one forward and one aft, below decks outside the main engineering spaces**; this "largely overcame the formerly frequent casualty of losing all electric power". The 1,630-ton classes had theirs **removed for weight compensation** "and were seriously handicapped". Aaron Ward kept fire-main pressure and power forward on the forward diesel ([DD gunfire/bomb/kamikaze report](https://ibiblio.org/hyperwar/NHC/WarDamageReports/WarDamageReportDDGunBombKamikaze/WarDamageReportDDGunBombKamikaze.html)). | Placement and count of independent sources is the single most valuable designer choice. It also competes with weight (topweight compensation for AA). |
| **Casualty power** | Portable cables, watertight bulkhead terminals and deck risers, rigged "from the load to the source" to feed steering, IC switchboards, fire pumps and vital machinery auxiliaries ([DC training manual](https://www.tpub.com/dc32/97.htm)). Destroyers made "voyages of several thousand miles to a home yard" on it; DD-692 class allowance grew from **1,300 to >1,900 ft** of cable (DD report). In one action leads "were available almost immediately" ([1945 DC Handbook](https://maritime.org/doc/dc/index.php)). | A repair action that bypasses severed route segments, limited by a cable-length budget and by pre-installed terminals. |
| **Cables cut in the superstructure** | Ralph Talbot: one hit severed power and lighting in the director trunk and power to the torpedo directors → no radar, no fire control (DD report). South Dakota: one hit cut Sky Control and main-director cables (`05` §A5). | Trunks that bundle many routes are high-value targets. Routing everything up one director trunk is a real weakness. |
| **Japanese multicore cables** | Hard to repair after splinter damage ([pwencycl](http://pwencycl.kgbudge.com/D/a/Damage_Control.htm)). | A cable-construction knob: multicore = lighter and cheaper, slow to splice. |
| **Electrics as ignition source** | Lexington's 12:47 explosion: avgas vapour in the **IC motor-generator room** "in the presence of operating electrical machinery capable of producing sparks". Recommendation: shut down sparking equipment in the affected area ([WDR 16](http://ibiblio.org/hyperwar/USN/WarDamageReports/WarDamageReportCV2/WarDamageReportCV2.html)). | Running electrics in a vapour zone raise ignition chance; "secure electrics in zone" is a DC action with a cost. |
| **Turn it off to save it** | Crews "pulling all power circuits to the forward turrets when these turrets were out of action… undoubtedly prevented further trouble such as fires from shorted circuits" (1945 Handbook). | De-energising a damaged branch is a valid player/AI action: removes short-circuit fire risk and nuisance trips. |
| **Darkness** | "Salvage and rescue work was seriously handicapped by absence of light" (1945 Handbook); PoW's stern was plunged into darkness. | Lighting is a consumer whose loss slows DC in that zone, unless battle lanterns. |

### 1.2 Hydraulics

| Fact | Source | Model meaning |
|---|---|---|
| USN: oil-filled **Waterbury variable-speed gear** (electric motor → swashplate pump → hydraulic motor), first on USS Virginia 1906, standard on all new capital ships from 1909. Used for turret training and elevation, hoists, rammers, **rudder control** and boat cranes. | [USNI Naval History, Oct 2023](https://www.usni.org/magazines/naval-history-magazine/2023/october/waterbury-pump) | **Self-contained, distributed hydraulics:** each mount has its own pump. Hydraulic failure stays local; the real dependency is *electric power*. |
| RN: early **water hydraulics at very high pressure** from central pumping engines, with sealing problems and "walking pipes". | same; `02` §2.2, §9 | **Central hydraulics:** one plant, a pressure main to several consumers. Pump-room flooding (Marlborough's hydraulic engine room) starves *all* turrets on that main. |
| Lexington's torpedo hit: "both main elevators were immediately put out of commission due to **loss of hydraulic pressure**". | [WDR 16](http://ibiblio.org/hyperwar/USN/WarDamageReports/WarDamageReportCV2/WarDamageReportCV2.html) | Pressure loss is global to a hydraulic network until the break is isolated. Elevators drop to safety latches (fail-safe state matters: up or down). |
| Steering gear: electro-hydraulic (variable-delivery pumps driving rams), controlled from the bridge by a telemotor or electric link, with a local helm in the steering room. Lexington kept steering by phoning orders (JV circuit) to the steering room using the after gyro. | WDR 16; general marine practice | Steering = three layers: hydraulic rams (local), electric power to pumps, and the control link from the bridge. Each has a fallback. |

### 1.3 Electronics, data and communications

- **Fire control** is a data chain: director (topside) → plot / transmitting station (deep) → mounts, over synchro cables. Synchros replaced step-by-step transmitters around 1925 (?) ([NavWeaps forum](https://www.tapatalk.com/groups/warships1discussionboards/introduction-of-electricity-t12077.html)). Data links need power at both ends *and* an intact route.
- **Battle telephones** were **sound-powered** and need no ship's power. USN circuits: 2JZ (damage & stability control hub), 3–7JZ (one per repair party), 8JZ (flight deck), JA (captain). Each had *primary* and *auxiliary* wiring "installed remotely from primary wiring". A short anywhere on a parallel circuit killed every phone on it; **action cut-out switches** isolated the damaged part ([WWII DC communications manual](https://www.navy-radio.com/manuals/ic-damage-control.pdf)). The fallback was messengers.
- **Vacuum-tube electronics** (radar, later FC computers) are the shock-fragile, high-power, heat-producing consumers. Shock hardening ("high-impact shock" requirements) was a WWII lesson made standard after the war ([Doerry & Amy](http://doerry.org/norbert/papers/20190524%20ests%20tutorial%20-%20doerry-amy%20-%20historical%20perspective%20-%20distro%20A.pdf); `05` §A6 shock thresholds: 0.1–0.15 shock factor → electrics and lighting fail).

### 1.4 Era and nation knobs specific to this layer

| Knob | Values |
|---|---|
| Supply | USN: 80 V DC → 125 V DC (USS Missouri BB-11) → 120/240 V DC three-wire (Arizona) → **AC ship service from 1932**, ~220 V on Farragut, then **450 V 3-phase** standard. RN: DC (220 V (?)) through WWII, AC only post-war. ([Doerry & Amy](http://doerry.org/norbert/papers/20190524%20ests%20tutorial%20-%20doerry-amy%20-%20historical%20perspective%20-%20distro%20A.pdf); [NavWeaps forum](https://www.tapatalk.com/groups/warships1discussionboards/introduction-of-electricity-t12077.html)) |
| Distribution | Radial from main switchboards (USN), ring main (RN), split forward/after plants (Iowa), emergency switchboards (USN WWII). Zonal distribution is post-war. |
| Load dependence | 1890s: lighting, searchlights, some fans and hoists; hand/steam backups everywhere. 1941+: nearly everything (`02` §8). |
| Emergency sources | None (Ark Royal), diesels inside machinery spaces (PoW), diesels outside machinery spaces fwd/aft (USN 2,200-ton DDs, post-Savo cruisers 50–75 kW). |
| Transfer | Manual bus transfer → ABT (unreliable early; South Dakota). |
| Protection selectivity | Poor (generator breakers trip on feeder faults) → selective relays (late-war USN). |
| Casualty power | USN WWII yes, grows during the war; others limited (?). |
| Hydraulics | Central water (RN, early) vs self-contained oil (USN from 1906–09). |
| Shock hardening | Post-WWII standard. |
| Cable type | Single-core armoured vs multicore (IJN, slow repair). |
| DC communications | Sound-powered primary + auxiliary circuits (USN), messengers. |

---

## 2. The core abstraction: a multi-layer service network over the compartment graph

```
COMPARTMENT GRAPH (already planned: ~10–30 long. × 2–3 trans. × 2–3 vertical bands)
        ▲  every route segment lives in exactly one compartment
        │
SERVICE NETWORK (per layer)
   nodes:  sources ── hubs ── consumers
   edges:  route segments (layer, capacity, list of compartments, protection, separation tag)
```

**Layers and their "working" rule:**

| Layer | Sources | Hubs | Consumers | Works if | Break side effect |
|---|---|---|---|---|---|
| `STEAM` | boiler rooms | cross-connect valves | engines, turbo-generators, steam pumps, steam steering | connected + steam ≥ demand | scalding in compartment (`02` §6) |
| `POWER` | generators (steam/diesel), batteries | switchboards, load centres, ABTs | everything electric | connected + kW ≥ demand (else shed) | short → breaker trip; arc/fire chance; spark in vapour |
| `HYD` | pumps (central) or none (self-contained) | valves / accumulators | turrets, steering rams, lifts, catapults, hoists | connected + fluid inventory > min + pump capacity ≥ demand + leaks | leak drains inventory; oil hydraulics: fire chance |
| `FIREMAIN` | fire pumps (electric, steam, independent petrol) | cross-connect/isolation valves, loop | hydrants, sprinklers, magazine flooding, eductors | connected + pressure | **rupture floods** compartment until isolated (Ralph Talbot) |
| `DATA` | directors, radars, gyro | plot / TS, IC switchboard | gun mounts, torpedo mounts, displays | connected + powered at both ends | none |
| `VOICE` | sound-powered (no source) | cut-out switches | stations | connected (no power needed) | short kills the whole circuit until cut out |
| `CMD` | bridge / CT / aft conn | — | steering, engine telegraphs | via VOICE or DATA routes | — |

Optional later: `VENT` (fans as POWER consumers with ducts as routes; spreads smoke/vapour) and `COOLING` (chilled water for electronics, post-war).

Why this is cheap:

- A battleship needs perhaps **100–300 nodes and 200–600 route segments across all layers [EST]**; a destroyer 30–80 nodes. Connectivity checks run **only when something changes** (dirty flag per layer), not every tick.
- Hits only need `compartment → list of segments/nodes inside it`, an inverted index built once by the designer.
- Geometry never appears at runtime.

---

## 3. What the designer must emit

### 3.1 Inputs the designer already has (or plans to)

From [`damage-model-research.md`](../damage-model-research.md) §13: compartments with bounds and adjacency, armour tiers, component placements, machinery rooms, magazines, superstructure stations. From [`powerplant-model.md`](../powerplant-model.md): boiler/engine room positions, transmission (turbo-electric is a POWER source feeding propulsion motors), steam technology. From [`crew-space-model.md`](../crew-space-model.md): complement and hotel loads.

### 3.2 New design choices (player-facing knobs)

Each knob must cost something (weight, volume, crew, money, topweight) or the player will max it.

| Knob | Options | Cost | Benefit |
|---|---|---|---|
| Generator count, type and placement | n × {turbo, diesel}, per compartment | weight, volume; turbo needs steam route | capacity, independence |
| Emergency source | none / diesel in machinery space / diesel outside machinery fwd+aft / battery | weight, volume | survives machinery flooding |
| Switchboard arrangement | single / split fwd-aft / split + emergency boards | weight, volume | fewer total blackouts |
| Distribution topology | radial / ring main / split ring [/ zonal post-war] | cable weight | alternative paths |
| Vital-load dual feed | none / manual transfer / ABT | cable weight, ABT failure mode | alternate path to vital loads |
| Route separation | single trunk / port+starboard / port+starboard+high-low | cable weight +20–60 % [EST] | one hit cannot cut both feeds |
| Route depth | through superstructure / below armour deck where possible | longer routes; volume | protection tier of segments |
| Protection selectivity | era tech: basic / selective | small | local faults stay local |
| Casualty power | none / terminals + risers + cable allowance (ft) | weight, small | bypass severed segments |
| Hydraulics | central plant(s) / self-contained per consumer | central: lighter overall, pump rooms; local: more motors, more power demand | central: shared failure; local: power dependent |
| Hydraulic fluid | water-based / oil | — | oil: better performance, fire risk [EST] |
| Fire main | single line / split / loop / + independent pumps | weight | survives one rupture |
| Phones | sound-powered primary / + auxiliary circuits | small | command and DC dispatch survive |
| Shock mounting | none / partial / full (era-gated) | weight, volume | fewer shock trips and failures |
| Spares allowance | low / normal / high | volume, weight | replace destroyed electronics at sea |
| Cable construction | single-core armoured / multicore | multicore lighter | repair time |

### 3.3 Emitted data (the contract)

```yaml
networks:
  POWER:
    nominal_voltage: 450            # flavour + era checks
    ac: true
    protection_selectivity: 0.6     # 0..1, tech-driven
    nodes:
      - {id: G1, kind: source, sub: turbo_gen, kW: 1250, comp: ER1_S, needs: {STEAM: BR1}, start_s: 60}
      - {id: G3, kind: source, sub: diesel_gen, kW: 250, comp: EDG_FWD, needs: {}, start_s: 20}
      - {id: SWB_F, kind: hub, sub: switchboard, comp: ER1_C, prot_tier: A}
      - {id: ABT_STEER, kind: hub, sub: abt, comp: STEER_RM, normal: SWB_A, alternate: SWB_F}
      - {id: C_STEER, kind: consumer, kW: 120, vital: 1, comp: STEER_RM,
         fallback: {mode: hand, perf: 0.25, crew: 6}}
      - {id: C_RADAR_SG, kind: consumer, kW: 15, vital: 2, comp: MAST_TOP,
         fragility: {shock: 0.8, splinter: 0.9}, spares: 1, warmup_s: 120}
    segments:
      - {id: s17, from: SWB_F, to: ABT_STEER, path: [ER1_C, BR2_C, ER2_S, AFT_PLAT_S, STEER_RM],
         tag: starboard, cable: single_core, kW_max: 400}
    casualty_power:
      cable_ft: 1900
      terminals: [BHD_44, BHD_61, BHD_88, RISER_F, RISER_A]   # bulkheads/decks with fittings
  HYD:
    plants:
      - {id: HP1, comp: HYD_RM_F, pumps: 2, needs: {POWER: SWB_F}, inventory_l: 4000, fluid: oil}
    consumers: [...]
  DATA: {...}
  VOICE: {circuits: [{id: 2JZ, primary_path: [...], aux_path: [...], cutouts: [...]}]}
  FIREMAIN: {...}

loads:            # electrical load analysis per condition, kW
  cruise: 900
  battle: 2100
  emergency_vital_only: 520

shedding_order: [hotel, vent_nonvital, lighting_nonvital, radar_search, hoists, ...]  # last = steering, IC

index:
  by_compartment: {ER1_C: [SWB_F, s17, s18, ...], ...}   # inverted index for hit resolution

analysis:         # design-time, see §3.4
  single_points: {steering_power: [STEER_RM], main_FC: [DIR_TRUNK, PLOT], ...}
  cut_pairs: {...}
  function_survival_vs_random_hits: {...}
```

**Functions** sit above consumers and are what the game and AI care about: `steering`, `propulsion_shaft_k`, `main_battery_director_fire`, `secondary_fire`, `search_radar`, `pumping_zone_z`, `firefighting_zone_z`, `magazine_flooding_m`, `lifts`, `catapults`, `internal_comms`, `external_comms`, `lighting_zone_z`. Each function is a boolean or graded expression over consumers and their fallbacks, e.g.

```
steering = rudder_ok AND (
             (C_STEER powered AND hyd_rams_ok AND cmd_link(bridge|aft_conn → STEER_RM))  -> 1.0
          OR (hand_steering_crew_present AND VOICE link to STEER_RM)                    -> 0.25
          OR (messenger chain)                                                          -> 0.25, +delay)
```

### 3.4 Design-time analyses (cheap, and the real payoff)

Because the network is small, the designer can run, in milliseconds:

1. **Electrical load analysis.** Generation (minus the largest single source, the "N-1" rule [EST]) vs battle load. Flags under-generation; drives generator sizing and weight.
2. **Single points of failure.** For each compartment c: remove everything indexed in c, recompute every function. Any function lost by one compartment is a red flag. O(C × graph).
3. **Cut pairs.** Same for compartment pairs (C ≈ 100 → ~5,000 checks [EST], fine offline). Shows "a torpedo here + a shell there blacks out the stern".
4. **Monte Carlo survival.** Throw N random hits weighted by hit probability per compartment (from size, height, armour). Report P(function survives k hits). Gives a single "vitality" score per function for UI and AI design selection.

Output these as a **vulnerability overlay** in the designer: compartments coloured by how many vital functions they can kill. This is the readable feedback RTW lacked (`05` §B1, pattern 18), and it makes redundancy feel worth its weight.

### 3.5 How the designer builds routes automatically

The player should not draw cables. Proposed auto-router [EST]:

1. Each layer has **trunk preferences**: e.g. power mains run along port and starboard passages on the deck just below the armour deck; data runs from director down the director trunk to the plot, then aft/forward along the trunk; fire main runs along the damage-control deck as a loop.
2. Route = shortest path on the compartment graph with **edge costs** = length × (protection penalty for low-tier compartments) × (penalty for crossing main bulkheads, since penetrations add leak paths, `damage-model-research` §3.5).
3. **Separation constraint:** the normal and alternate feeds of a vital consumer must not share compartments except at the endpoints. If impossible, flag it.
4. Cable weight = Σ segment length × kW-dependent weight per metre [EST]. This is how redundancy costs weight.
5. Every main-bulkhead crossing is a **leak path** added to that bulkhead's leak rate. Redundancy therefore has a flooding cost too: a nice, physical trade-off.

---

## 4. Candidate damage models

Four options from cheapest to richest. They share the emitted data, so the game can mix them by LOD.

### Model A — Function cut-set flags (cheapest)

- Runtime state: one damage flag per compartment (or a few severity levels).
- Each function stores its precomputed **minimal cut sets** (sets of compartments whose combined loss kills it, up to size 2–3).
- Function lost when any cut set is fully damaged. Repair = repair the compartment.
- **Pros:** trivial runtime, no graph. Good for MTBs, distant AI ships, auto-resolve.
- **Cons:** no transients, no capacity/shedding, no partial jumpers, repairs are coarse. Cut-set count can explode on very redundant designs (cap at size 3).

### Model B — Service graph with fault states (recommended core)

Runtime state per element:

```
node/segment.state ∈ {OK, TRIPPED, FAULTED, SEVERED, DESTROYED, FLOODED, ISOLATED, JUMPERED, DEENERGISED}
hyd_network.inventory, hyd_network.leak_rate
firemain_segment.ruptured
```

**Hit resolution** (per hit or splinter burst):

```
for each element e indexed in hit compartment c (and neighbours within blast radius):
    if e.prot_tier stops this hit: continue
    p = base_p[e.kind] * exposure(e, hit) * (1 - shock_mount * k)          # [EST]
    if rand < p:
        severity = roll(hit.energy, e.kind)
        e.state = SEVERED | DESTROYED | FAULTED     # cables mostly FAULTED→SEVERED
        if layer == POWER and e.state in {FAULTED}: queue protection_event(e)
        if layer == HYD: network.leak_rate += leak(e)
        if layer == FIREMAIN: c.flood_inflow += rupture_flow(e)
    mark layer dirty
```

**Protection event** (makes transient blackouts emerge):

```
fault at element e:
    upstream = path from e toward its feeding source(s)
    if rand < selectivity:   trip the nearest breaker upstream of e          # local loss only
    else:                    trip a breaker further up (switchboard / generator)  # cascade (South Dakota)
    if the tripped branch has an ABT: transfer to alternate
        if fault still connected: alternate protection trips too (fuses blew)
    tripped breakers: auto-reclose? no. Manual reset task (seconds–minutes), fails again if fault not isolated.
```

**Evaluation** when dirty:

```
POWER: BFS from running sources through non-failed elements → energised set.
       per island: supply = Σ running source kW; demand = Σ consumer kW (priority-ordered)
       if demand > supply: shed in shedding_order until it fits; if still > 1.1×: source trips (overload)
HYD:   per network: pressurised if connected to a running pump AND inventory > min
       AND pump_capacity ≥ demand + leak_rate. Inventory -= leak_rate·dt.
DATA/VOICE: reachability. VOICE needs no power; a FAULTED element kills the whole circuit until cut out.
FUNCTIONS: evaluate expressions with fallbacks; publish graded values (1.0 / degraded / 0).
```

**Couplings** (where the realism comes from):

| Event | Effect |
|---|---|
| Compartment floods above level h | elements there → FLOODED (generators, switchboards at deck level; cables only if junctions submerged [EST]). Recovery: pump out + dry, hours. |
| Fire in compartment | cable insulation burns: segments there degrade to SEVERED over minutes; cables add fire load; smoke. |
| Shock event (UNDEX, near miss, heavy hit) | per-zone shock factor (`05` §A6). Each element rolls by fragility: ≥0.1 → breakers/lighting trip, tube electronics need reset; ≥0.15 → equipment failures, pipe leaks (HYD, FIREMAIN). Shock mounting divides the roll. |
| Own heavy salvo | tiny chance of a shock trip near the mount [EST], scaled by protection era. Flavour only. |
| Steam lost to a turbo-generator | source stops, island re-evaluated, possible overload trip of the rest. |
| Ventilation lost in an electronics or machinery space | heat timer; after T, electronics fail (soft) and crew efficiency drops. |
| Avgas/petrol vapour in zone with energised sparking equipment | ignition chance per tick ([`04-carriers.md`](04-carriers.md)). Player can de-energise the zone. |
| Lighting lost in zone | DC task times × 1.5–2 unless battle lanterns [EST]. |
| List > threshold | some pumps lose suction, hoists and hydraulics degrade (`02` §2.3). |

**Pros:** captures South Dakota (cascade + quick restore), PoW (steam → generators → pumps/steering → flooding), Lexington (hydraulic pressure loss, spark ignition), Ralph Talbot (one trunk hit blinds the ship). Readable: "steering lost — power: switchboard A tripped (fault in compartment 52)".
**Cons:** more state and content; needs a reasonable auto-router.

### Model C — Function reliability states (Markov / probabilistic)

- No topology at runtime. Each function is a small state machine: `UP → DEGRADED → DOWN`, with transient vs persistent down states.
- Hits in a compartment raise each function's transition probability by a precomputed sensitivity `S[f][c]` (from §3.4 Monte Carlo).
- Repairs are timed transitions sampled from distributions, shaped by DC quality.
- **Pros:** very cheap; ideal for fleet battles out of view and campaign auto-resolve. Calibrates directly against historical outcomes.
- **Cons:** no causal story; player cannot see *why*. Not for the ship under the camera.

### Model D — Hybrid LOD (recommended overall)

| Situation | Model |
|---|---|
| Player's ships and ships under camera, ≥ ~1,500 t | B |
| Coastal craft (< 150 t) | A (few functions: engines, steering, guns, radio) |
| Off-screen AI ships, auto-resolve | C, with sensitivities from the same design-time analysis |
| Switching LOD | B → C: collapse to function states (keep persistent damage). C → B: sample element states consistent with function states [EST; may be skipped by keeping B for any ship once damaged]. |

---

## 5. Repair model

### 5.1 Task types

Times are order-of-magnitude anchors for a trained crew in light conditions; most are **[EST]**, anchored where noted.

| Task | Fixes | Base time | Needs | Anchor |
|---|---|---|---|---|
| Reset breaker / replace fuse | TRIPPED | 0.5–3 min | access to switchboard; fault isolated first or it trips again | South Dakota ~1 and ~3 min outages |
| Transfer bus manually | lost normal feed | 1–2 min | alternate path OK | WDR 57 (manual transfer after the war) |
| Isolate (open breaker, shut valve, cut out phone section) | stops cascading, leak, rupture flooding | 1–5 min | access to valve/breaker; knowing *where* the break is | Ralph Talbot fire-main break "could have been isolated readily" |
| Start emergency diesel | adds source | 0.3–1 min | crew present; diesel not flooded | DD report |
| Cross-connect steam | restores turbo-gens / engines from another boiler room | 5–15 min | valves accessible | PoW (A → X engine room) |
| Rig casualty power | JUMPERED path between terminals | 5–20 min per run; "almost immediately" when pre-laid | cable budget (ft); terminals on both sides; no flooding at riser | 1945 Handbook; DD-692 cable allowance |
| Splice / temporary cable repair | SEVERED → OK (jury) | 15–60 min; multicore × 2–3 | spares, access | IJN multicore |
| Make up hydraulic fluid, restart pump | restores pressure after isolation | 5–15 min | fluid reserve | — |
| Replace component from spares | DESTROYED electronics/motors | 30 min–hours | spares count | `02` §4 radar note |
| Pump out and dry flooded switchboard/generator | FLOODED | hours | flooding stopped, pumps | — |
| Not repairable at sea | wrecked switchboard rooms, wrecked masts | — | dockyard | — |

**Jury repairs** restore function at reduced capacity (e.g. casualty cable carries 50 % of normal kW [EST]) and can fail again on the next shock.

### 5.2 DC parties as agents

- Repair parties (USN: ~3 per destroyer, ~10 men each; more on large ships) are stationed in zones and move along the compartment graph. Travel time scales with distance; flooded or burning compartments are impassable or slow; list and darkness slow movement.
- **Detection delay:** a fault must be *found* before it can be isolated. Delay depends on internal comms (2JZ up?), sensors (late-war indicators), and whether the space is manned. This is what made Ralph Talbot's and Taihō's mistakes possible.
- **Dispatch needs communications.** If VOICE to a party is down, orders go by messenger: + travel time of the messenger. Parties without orders work on whatever is nearest (local initiative) [EST].
- **Priority doctrine** (DC Central; player can override): (1) flooding boundaries and stability, (2) fires near magazines/avgas, (3) power to pumps, fire pumps and steering, (4) internal comms, (5) weapons and sensors. This order matches USN handbook emphasis (flooding and fire first) [EST for the exact order].
- **Skill multipliers:** nation/era DC doctrine (`damage-model-research` §10): USN 1943+ high, IJN low-mid, and crew training. Multiply task times and success chance.
- **Spares and cable** are consumable ship stores and carry into the campaign.

### 5.3 Player/AI decisions this creates (all historical)

- De-energise a damaged zone (fewer shorts/fires, lose consumers there).
- Shed loads to keep steering and pumps running (radar off to save pumps).
- Isolate a hydraulic or fire-main section (lose consumers beyond it, stop the drain or the flooding).
- Rig casualty power to steering or to radar? (cable budget).
- Start emergency diesels early (noise/fuel cost negligible; crew tied up) [EST].
- Secure electrics in a vapour zone (carrier).
- Shift to local control / hand power vs wait for repair.

---

## 6. Feedback and UI implications

- **Ship status panel = the network.** A simplified schematic: sources, switchboards, zones lit or dark, hydraulic pressure gauges, phone circuits. The player sees *which* zone is dark and *why*.
- **Symptom → cause chain** in reports: "Turret 3 slow (hand training) ← no hydraulic pressure ← pump room 2 lost power ← switchboard B tripped ← short in compartment 41 (hit 12)". The graph gives this for free by walking the failed dependency.
- **Designer overlay** of single points of failure (§3.4).
- **Post-battle report** with "what would have saved it" hints (e.g. "an alternate feed on the starboard side would have kept steering power").

---

## 7. Calibration anchors for this layer

| Situation | Target |
|---|---|
| Superstructure hits on a 1942 battleship, poor protection selectivity | ship-wide FC/IC blackout of ~1–3 min, then restored; repeated (South Dakota) |
| Same, with selective protection | local loss only (director or radar), rest unaffected |
| Torpedo floods one machinery room on a ship with steam-only generators and diesels inside machinery spaces | progressive power loss over minutes; pumps and steering fail; flooding outruns pumps (PoW: 3 of 8 generators left) |
| Same with emergency diesels outside machinery spaces | vital power (pumps, fire main, steering) retained forward/aft (Aaron Ward, 2,200-ton DDs) |
| No independent generation, machinery flooded | total blackout, slow sinking over hours with almost everyone saved (Ark Royal, `02` §8) |
| One hit in a director trunk | radar + director lost, guns on local control (Ralph Talbot) |
| Hydraulic main breach on a carrier | all lifts on that network down immediately (Lexington) |
| Casualty power in a damaged destroyer | vital loads restored within minutes; ship can steam thousands of miles home (DD report) |
| 1890s ship blackout | minor: lighting and searchlights; hand and steam backups everywhere |

---

## 8. Recommended next steps (proposal)

1. Add the **service network** and **functions** to the generator output schema (§3.3). Start with POWER, STEAM, HYD, DATA, VOICE; add FIREMAIN when flooding exists.
2. Write the **auto-router** with trunk preferences and the separation rule (§3.5). Cable weight feeds the weight model ([`hull-weight-model.md`](../hull-weight-model.md) / [`hull_weight_ref.py`](../hull_weight_ref.py)).
3. Build the **design-time analysis** (single points, pairs, Monte Carlo). Ship it in the designer before the combat model exists: it is useful on its own, and it tests the data.
4. Implement **Model B** for one test ship (a Sumner-like 2,200-ton destroyer: 2 turbo-gens + 2 emergency diesels fwd/aft outside machinery, split plant, self-contained 5″ mount hydraulics, Mk 37 director; exact generator ratings not yet sourced) and replay the Ralph Talbot / Aaron Ward / South Dakota anchors.
5. Derive **Model C** sensitivities from the same Monte Carlo and use them for AI/off-screen ships.

### Open questions

- Granularity of compartments for routing: is the planned 10–30 × 2–3 × 2–3 grid fine enough to separate port/starboard runs in a destroyer? (Probably yes with 3 transverse cells.)
- Do we want real-time DC parties as visible agents, or abstract "DC capacity" with queued tasks (Admiralty Trilogy-style)? Agents are richer but need UI.
- Generator and cable weights per kW by era: not researched yet; needed for the cost side.
- Hydraulic fluid fire risk (oil vs water) in WWII: plausibly real but unsourced here.
- Late-war automation (fault indicators, remote-operated breakers) as a tech that cuts detection delay: worth a tech step?

---

## 9. Sources

- NHHC War Damage Reports: [South Dakota WDR 57](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-south-dakota-bb57-war-damage-report-no57.html) · [Lexington WDR 16 (HyperWar)](http://ibiblio.org/hyperwar/USN/WarDamageReports/WarDamageReportCV2/WarDamageReportCV2.html) · [Destroyers: gunfire, bomb, kamikaze](https://ibiblio.org/hyperwar/NHC/WarDamageReports/WarDamageReportDDGunBombKamikaze/WarDamageReportDDGunBombKamikaze.html)
- Garzke, Dulin, Denlay et al.: [Death of a Battleship: HMS Prince of Wales (2012)](https://pacificwrecks.com/ship/hms/prince-of-wales/death-of-a-battleship-2012-update.pdf)
- [Handbook of Damage Control, NAVPERS 16191 (1945)](https://maritime.org/doc/dc/index.php)
- [Shipboard Damage Control Communications (WWII manual)](https://www.navy-radio.com/manuals/ic-damage-control.pdf)
- [Damage Controlman training manual: Casualty Power Systems](https://www.tpub.com/dc32/97.htm) · [NSTM ch. 320, Electric Power Distribution](https://maritime.org/doc/nstm/ch320.pdf)
- Doerry & Amy: [Electric Ship: Historical Perspective](http://doerry.org/norbert/papers/20190524%20ests%20tutorial%20-%20doerry-amy%20-%20historical%20perspective%20-%20distro%20A.pdf)
- [USNI Naval History: The Waterbury Pump (Oct 2023)](https://www.usni.org/magazines/naval-history-magazine/2023/october/waterbury-pump)
- [NavWeaps forum: Introduction of electricity](https://www.tapatalk.com/groups/warships1discussionboards/introduction-of-electricity-t12077.html)
- [Pacific War Online Encyclopedia: Damage Control](http://pwencycl.kgbudge.com/D/a/Damage_Control.htm)
- Project docs: [`02-components.md`](02-components.md), [`05-mechanics-and-games.md`](05-mechanics-and-games.md), [`04-carriers.md`](04-carriers.md), [`damage-model-research.md`](../damage-model-research.md), [`powerplant-model.md`](../powerplant-model.md), [`crew-space-model.md`](../crew-space-model.md).
