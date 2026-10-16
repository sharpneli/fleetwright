# 17 — Service networks: breaker trips from shock, and cable cuts

*Research for the damage model, after `15_near_misses.md` and `16_casualties.md`. It firms up two pieces of the draft
`06_service_networks.md` (project doc `claude/damage-research/06_service_networks.md`): its Model B hit resolution and
"protection event" (06 §4), and the repair times for trips and cuts (06 §5.1). It feeds 4.5's systems iteration in
`fighting.md`.*

*The question as asked: "Service networks: breaker trips from shock, cable cuts. 06 is a draft."*

*What 06 had: a plan (routes through compartments, fault states, selectivity as a knob) and the South Dakota cascade.
What it lacked: when shock trips a breaker, how often a burst cuts a cable, what a cut cable does electrically, and how
long each takes to put right. This note supplies those numbers and checks them against the record on two test
networks.*

*Tags as in the other damage docs: **[SRC]** sourced, **[FIT]** fitted here to the record, **[EST]** judgement,
**[UNCERTAIN]** thin or conflicting sources.*

**Companion code:** `service_fault_ref.py` (pure Python, about 10 s). It **imports** `fragmentation_ref.py` for the
fragment field (13). Every table below comes out of it ("ref T*n*").

---

## 0. Short answer

1. **Two different failures, and the record keeps them apart.**
   - **Shock trips** open (or close) good equipment with no fault. They are mostly caused by underwater bursts: torpedoes,
     mines, bomb and shell near misses.
     - The fix is a reset, and power comes back in **about 1–3 minutes**: South Dakota 1 min, Electra 2, White Plains
       and South Dakota's FC/IC bus 3 [SRC].
   - **Cable cuts** are faults. They are mostly caused by fragments in the superstructure.
     - The cut circuit is gone for the action unless someone splices or jumpers it.
     - The cut may also *short*. The short trips a protective device. If protection is poor, the device that trips is
       one level higher than it should be: a whole bus goes dark until the fault is found (South Dakota's 3 minutes).
2. **Shock trips are a fragility curve in the shock factor at the device, by the fit's "grade".** This is the
   lognormal `P = Φ(ln(S/S50)/0.49)` with `S = hull NSF × mount level × size` (ref T1).

   | Grade | Who | Breaker S50 | Anchors |
   |---|---|---|---|
   | **G0 unsecured** | Everyone to 1940; RN until the Southampton fixes; merchant-built hulls | **0.07** | British destroyers 1940 [SRC]; White Plains "most breakers" |
   | **G1 secured** | RN late 1940 on; USN 1942 ("fuse and switch securing alterations") | **0.22** | North Carolina, no trips; O'Brien, one main breaker |
   | **G2 shock-tested** | USN 1943+ new construction and refits (high-impact shock tested) | **0.55** [EST] | — |
   | **G3** | Post-war grade-A gear | 1.1 [EST] | — |

   Lamps go first, then contactors and breakers, then generators. Busbars shaken together are rare (ref T1).
3. **The model reproduces the record on a 2,100 t destroyer network** (ref T2):

   | Case | Record | Model |
   |---|---|---|
   | 1940 bomb near misses (G0) at NSF 0.1–0.25 | Power lost | Every vital load dark for **1.5 min median** |
   | O'Brien (G1) | One main breaker, back at once | P(any trip) 0.66, share tripped 0.07, back in 0.6 min |
   | North Carolina's torpedo (G1) | No trips | P(any) 0.06–0.19 (ref T2/T2b) |
   | White Plains (G0) | "Most breakers", 3 min | 59% of devices, back in 1.6 min (p90 2.4) |

4. **A fragment cuts a cable it touches; it does not stop in the bundle.** 13's fragments carry 14–160 kJ; a cable
   takes about 0.5 kJ. So a wireway is a bigger target than one cable and gives no shelter: each fragment that reaches
   it severs about four cables (ref T3).

   P(a given cable in a 24-cable wireway is cut), one burst:

   | Shell | 2 m | 4 m | 8 m | 15 m |
   |---|---|---|---|---|
   | 5″/38 AAC | 0.64 | 0.40 | 0.22 | 0.13 |
   | IJN 20 cm common | 0.81 | 0.56 | 0.34 | 0.20 |
   | 14″ HC | 0.90 (wrecked inside 3.6 m) | 0.68 | 0.44 | 0.26 |

   A 6 mm bulkhead in between barely matters (×0.8–0.95).
5. **Checked against the record** (ref T5, T6):
   - **San Francisco:** 19 of about 45 hits cut lighting circuits (0.42). The model gives 0.45.
   - **Ralph Talbot:** one burst in her chart house or director trunk loses the directors' circuits 0.77–0.98 of the
     time, and the IC to the bridge, radar and torpedo director about 0.8 each. Her report says all were cut.
6. **What a cut does electrically:** short 30%, ground 40%, open 30% for fragments [EST].
   - USN AC and DC systems were both **insulated**. One ground does nothing but light the ground detector. A **second
     ground** on the same system is a short (25% per new ground [EST]).
   - A short is cleared by the nearest device with probability **0.75 for a branch breaker and 0.9 for a fuse** in 1942
     (basic), 0.92 and 0.95 with selective relays (1943+) [FIT/EST].
   - **Locked-in breakers** never clear a fault, so the fault passes up a level. That is the trade-off the record
     names (South Dakota, the ITE patent).
7. **South Dakota's night, replayed** (26 HE hits, mostly in the superstructure; ref T4):

   | Variant | Circuits cut | Faults passed up | Board dark | IC/FC bus outage | Electrical fires |
   |---|---|---|---|---|---|
   | 1942 basic protection, panels locked in | ~32 of 42 | 8.8 | 0.95 | P 0.56, median 3.4 min | 1.8 |
   | Selective relays, panels not locked | — | 0.9 | 0.03 | P 0.20 | 0.8 |

   The record: about 35 kinds of circuit damaged, and one ship-wide FC/IC loss of about 3 min.
8. **The three design knobs that matter most** (ref T7):
   - **Fitting grade.** At NSF 0.2, a G0 destroyer loses vital loads every time for 1.6 min. G2 loses them 8% of the
     time.
   - **Lock-in.** It cuts the long shock outages (p90 1.6 against 5.4 min) but more than doubles electrical
     fires in a gunfire battle (1.8 against 0.8).
   - **LVR vs LVP pumps.** LVP pumps need a man to restart them: 30% of pumps stay off longer than 3 min, against 3%.

   Emergency diesels and ABTs matter less for shock than for flooding (06).
9. **Fire and water also kill cables** [SRC qualitative, numbers EST]:
   - **Fire.** Rubber cable in a burning space fails in about 6 min, the asbestos "HF" cable in about 15. Burning
     cable runs carry fire through bulkheads.
   - **Water.** Armoured cable works under water, but junction boxes fail (San Francisco's turret I). Water also wicks
     along cut cables to dry boards: Savannah, more than 160 ft and a fire three days later.
   - **Stuffing tubes.** Unpacked cable penetrations are leak paths (Boise, Houston, Northampton).

---

## 1. The record: shock

### 1.1 What trips, and why

**Mechanism [SRC].** A breaker holds closed on a latch. It trips when an armature, moved by a magnet on overcurrent,
knocks the latch free. Under shock the armature and the trip bar move by their own inertia.

- **The ITE patent of 1943** ("Shockproof tripping armature", US2491657): "tremendous shocks, far exceeding a
  broadside shock, are imposed by near misses of bombs". Navy shock tests had been "increased many fold over the old
  requirements".
- **The earlier fixes and their drawbacks** (same patent):
  - balancing the armature added weight and inertia;
  - stops could pass the shock into the armature;
  - **hold-in and lock-in devices** stopped false trips "but prevent automatic opening" on real faults.
- **The GE patent of 1948** (US2451239) was still adding captive pivots and anti-bounce stops "for war craft…
  subjected to violent shock".
- **It never went away.**
  - In the 1970s, 15 of 18 nominally shock-certified switchgear breakers failed the 5-ft back and top blows; most
    "tripped from the closed position" (NRL MR 4003, 1979).
  - A modern Navy SBIR cites spurious opening at about 600 g pulses.

**Shock can close things too [SRC].** In South Dakota (WDR 57 ¶46), shock from her own turret III firing astern
**closed** the alternate contactor of an automatic bus transfer (ABT):
- it paralleled two out-of-phase switchboards and welded the contacts;
- a feeder ruptured;
- with the panel breaker locked in, generator No. 7's breaker cleared it;
- the after part of the ship was dark about 1 min.

All her ABTs were then replaced with manual switches.

**The item ranking [SRC, WDR 58 §18, submarines].**

| Rating | Items |
|---|---|
| "Poor" | Instruments, gauges, meters, light bulbs |
| "Fair" | Relays, switches, controllers |
| "Excellent" | Resiliently mounted equipment, "no known cases of failure through shock" |

Temporary power losses were "principally through tripping of relays and contactors". Two boats had battery busbars
jarred together: Tunny (a fire) and Cobia (arcing).

### 1.2 The cases

| Case | Shock | Fitting | What tripped | Back in | Source |
|---|---|---|---|---|---|
| British destroyers, 1940 bombs (Montrose, Walpole, Vanessa) | NSF 0.1–0.3 (15 §3.2) | unsecured | "electric power lost", "temporary loss", "secondary lighting all blown down" | minutes | BuShips summary [SRC] |
| Stuart | near miss | unsecured | one dynamo tripped | — | same |
| Sheffield | 0.05 | unsecured | "some… electrical equipment" | — | same |
| Southampton | bomb | unsecured | power failed temporarily, giving the British fixes (§1.3) | — | same |
| O'Brien, torpedo forward, 1942 | contact, ~40 m to the boards | secured | "one main circuit breaker tripped out… immediately thrown back in"; bulbs shattered except pendant ones; grounds "rapidly cleared" | at once | WDR 28 [SRC] |
| North Carolina, torpedo, 1942 | contact, forward of turret I | secured ("all known alterations… installed") | **none**: "No circuit breakers tripped… no motors started or stopped" | — | WDR 61 [SRC] |
| Electra (AKA), torpedo | contact | merchant | "all circuit breakers for pumps in operation were blown open" | 2 min | WDR 32 [SRC] |
| Helena, Pearl Harbor torpedo | contact | 1941 | lights out, emergency lights on in seconds; "the generator kept tripping out" | 1–1.5 min (diesel) | WDR 4 statements [SRC] |
| White Plains, Samar | 46 cm under the bilge, NSF 0.3–0.6 (15 §3.3) | CVE, Kaiser-built | "tripped most of the circuit breakers" | **3 min** | DANFS via Wikipedia [SRC, secondary] |
| South Dakota, own turret III | own gun | 1942, ABTs | ABT closed, generator breaker tripped | ~1 min | WDR 57 [SRC] |
| Crossroads (mostly the air burst) | weak | 1946 | lamps broken, fuses "jarred out of their clips" (Pennsylvania); ~100 German lamps with broken filaments (Prinz Eugen) | — | DTIC AD0366708, -714 [SRC] |

**Restore times in the record** cluster at **1–3 min** for shock trips at manned boards. They are longer when the
board itself faulted (Savannah's bomb: 8 min aft, 16 min ship-wide) or when a diesel had to start.

### 1.3 What fixed it

**Fixes by navy and year [SRC]:**
- **RN after Southampton, 1940:** "Circuit breakers should be locked in"; "Starter handles… tied in the 'on'
  position"; "Important fuses should be wired in place to prevent jumping out under shock" (BuShips summary).
- **USN by mid-1942:** AQB breakers in unattended panels were "locked in" by outstanding instructions (WDR 57). There
  were also "alterations for securing fuses, switches, etc., against shock", complete in North Carolina by September
  1942 (WDR 61).
- **USN 1943+:** high-impact shock tests to Specification 17E13(INT) by May 1943 (NRL B-2054); shockproof armatures;
  shockproof overload relays (submarines); resilient mounts; pendant lamp holders.
- **The machines.** The British lightweight shock machine was first built in 1939, and Belfast's mine set its
  parameters. The first USN medium-weight machine came in 1943 (220–580 g, 1–3 m/s). The 1941 Portsmouth tests on
  Tambor, Trout and Gar "formed the basis of practically the entire shock protection program" (WDR 58).

**The trade-off is explicit in the record.** A locked-in breaker cannot trip on shock, but it cannot clear a fault
either, so the next breaker up does. In South Dakota that was a generator.

### 1.4 How hard the hull shakes the gear

These figures relate the shock factor to what a device feels:
- Mounted-equipment velocity goes roughly with the keel shock factor, and its scatter around the ship is far smaller
  than the acceleration's (DTIC ADA326738) [SRC].
- The acceleration a mounted item feels is about its velocity × 2πf. 10 ft/s at 20 Hz is about 40 g, and resonance adds
  "seldom more than double" (RN, *Effect of Shock on Ships' Machinery*) [SRC].
- Light items see far more: a 25 lb item 75–250 g, a 5,000 lb item 50–120 g, the keel 1,000–10,000 g [SRC,
  secondary].
- The USN later assigned design shock factors of 0.2 for military equipment and 0.1 or less for commercial gear
  [SRC, secondary]. That is G2/G3 against G0 in this note's terms.

No WWII threshold in g for a breaker was found. The fragility is therefore fitted directly in shock-factor units, at the
device's space, from the cases above.

---

## 2. The shock model

### 2.1 Severity at a device

`S = NSF_hull(space) × MOUNT[level] × size(Δ) × (0.5 if resilient)`

- **NSF_hull** is 15's near-miss factor (`√W/R` with the angle and surface cut-off) evaluated from the burst to *that
  space*, not one number per ship. A torpedo forward shakes the forward spaces most.
- **MOUNT** reflects that shock falls off up the structure [EST]:

  | Level | Factor |
  |---|---|
  | Hold (on the bottom framing) | 1.0 |
  | Platform | 0.85 |
  | Deck | 0.65 |
  | Superstructure | 0.45 |

- **size(Δ) = (Δ / 2,000 t)^−1/6**, capped at 1 [EST]. A battleship's hull girder moves less for the same factor: 0.6
  at 44,000 t, 0.75 for a CVE.

  This is the weakest number here. It is what lets North Carolina's G1 gear see no trips with a 400 kg warhead
  50–80 m from her boards.

### 2.2 Fragility

`P(fail) = Φ( ln(S / S50) / β )`, β = 0.49. β splits into 0.35 shared by everything in one space for one event, and
0.35 per device, so the devices of one switchboard trip together, as White Plains' did.

S50 by item and grade (`S50` in the ref) [FIT where marked, else EST]:

| Item | G0 | G1 | G2 | G3 | What it means |
|---|---|---|---|---|---|
| Lamp (incandescent) | 0.05 | 0.10 | 0.18 | 0.40 | Zone light level falls; battle lanterns cover in seconds |
| Contactor (motor controller) | 0.06 | 0.18 | 0.45 | 0.90 | The motor stops; restart depends on LVR or LVP |
| **Breaker** | **0.07** [FIT] | **0.22** [FIT] | 0.55 | 1.10 | Circuit opens; reset |
| ABT spurious operation | 0.10 | 0.30 | 0.80 | — | AC only: out-of-phase paralleling, a feeder fault, the generator clears |
| Fuse jumps its clips | 0.10 | 0.60 | 1.20 | 2.00 | Circuit opens; refit the fuse |
| Generator trips | 0.12 | 0.30 | 0.70 | 1.40 | Board dark until restarted and synchronised |
| Busbars jarred together | 0.90 | 1.60 | 3.00 | 5.00 | A short on the board itself: 8–16 min (Savannah); fire chance |

Read across (ref T1):
- At **S = 0.08**, a G0 breaker trips 61% of the time, a G1 breaker 2%.
- At **0.3**, G0 is certain, G1 73%, G2 11%.

**Grades by navy and year** [EST from §1.3]:

| Grade | Who |
|---|---|
| G0 | Everyone before 1940. RN to late 1940. USN to early 1942. Merchant-built and converted hulls throughout. IJN and Kriegsmarine throughout **[UNCERTAIN]**: no record of their securing work was found. Prinz Eugen's lamp sockets failed at Crossroads where US ones held |
| G1 | RN from late 1940. USN from 1942 |
| G2 | USN 1943+ new construction and refits. RN 1943+ new construction **[UNCERTAIN]** |
| G3 | Post-war |

**Before about 1910** ships had few breakers (fuses, knife switches, dynamos with their own governors). Keep G0, but
there are few devices to trip and little that depends on them. 13 §3.3 already says so.

### 2.3 Other shock sources (kept from 13 and 15)

- **Heavy hits on armour** (13 §3.4): keep 13's per-zone rates. They are structure-borne shock, local and high in g,
  and do not fit the hull-NSF curve. A "local breakers trip" in 13 now maps onto *the devices in zone A*, with one
  shared draw per switchboard or panel as above.
- **Own guns** (13 §3.4): per 20 min of sustained main-battery fire, P(breaker or ABT event) is 0.05 before the fixes
  and 0.02 after. South Dakota's ABT event is the anchor.
- **Near misses** (15 §6.4): band 0.03–0.08 is "lighting and breakers trip". It now becomes the fragility roll,
  space by space.

---

## 3. The record: cable cuts

### 3.1 What cables were, and where

**Construction [SRC]:**
- **USN WWII:** armoured cables with letter codes, e.g. DHFA = "double conductor, heat and flame resistant, armored";
  also MHFA, SHFA and TTHFA (telephone). DCOP was the portable cable for casualty power (NAVPERS 10622, 1945). The
  "HF" construction was asbestos and varnished cambric, self-extinguishing.
- **RN:** rubber-insulated, lead-sheathed cable from 1888 "until the end of the Second World War" (MacLaren, 1960).
  The same source puts electrics at about 30% of a warship's cost.
- **IJN:** multicore, hard to splice (06).

**Quantities [SRC]:**
- a US battleship (1944): 1,220,000 ft (about 231 mi) of cable, 5,300 light fixtures, 1,091 telephones;
- Coral Sea: 242 mi of cable, 2,000 motors.

**Routes [SRC].** The WDRs name the same kinds of place again and again:
- "wireways under the charthouse overhead" (Ralph Talbot);
- "wiring passage A-509-T" and the trunks under the turrets (Boise);
- "the entire group of cables on the starboard overhead, including the starboard steering motor cable" (Sterett);
- "all the wireways on the port side leading aft" (Johnston);
- director trunks (San Francisco).

The Mk 37 director tube was ¾″ steel; a battleship's conning-tower tube was heavy armour. Cables inside armour were
not reached by these hits.

### 3.2 The cases

| Case | Hits | What was cut | Consequence | Source |
|---|---|---|---|---|
| **San Francisco**, Guadalcanal | ~45 (8″, 6″, 5.5″, 5″, 14″) | "Approximately forty different kinds of circuits"; **19 hits cut lighting circuits**; 9 cut degaussing. Hit 41: "practically all cable in the director trunk was severed by fragments". Hit 7: the steering selsyn, rudder indicators and phones. **Feeders generally intact**; branches cut everywhere | Steering shifted aft; power to both superstructures deliberately cut off; a fragment short in the flood-control panel flooded turret II's handling room | WDR 26 [SRC] |
| **South Dakota**, Guadalcanal | ~26 | 35 kinds of circuit repaired, including all flexible wiring to main battery director 1 and secondary director 1 | FC and IC cables shorted, the feeder breaker tripped, the ABT moved to the emergency diesel, the **emergency fuses blew**; FC/IC dark ship-wide **about 3 min** | WDR 57 [SRC] |
| **Ralph Talbot** | one 8″ pattern under the charthouse | IC to bridge and directors, director-trunk power, torpedo directors, SC/FD radars | "fighting efficiency was seriously reduced by the cutting and shorting of important power and communications circuits" | WDR 51 [SRC] |
| **Johnston**, Samar | many | "all the wireways on the port side leading aft" | Power to the after guns and steering lost; casualty power failed; "repeated electrical fires" (CO₂, ~30 min); "all circuit breakers leading aft were left open"; hand steering | WDR 51 [SRC] |
| **Sterett** | gunfire | starboard steering cable, then the emergency steering cable | Rudder jammed while shifting to the port cable; steered by engines; cables "severed or so badly torn and burned"; "probably… several electrical fires" until de-energised | WDR 33 [SRC] |
| **Quincy, Vincennes**, Savo | gunfire | steering leads | "loss of steering control"; a hit on Vincennes' FC tube cut the cables "and set them on fire" | WDR 29 [SRC] |
| **Boise**, Cape Esperance | several | "All power and communication to 5″ gun No. 1"; degaussing; wiring passages flooded | Breakers to the wrecked turrets pulled "to prevent possible electrical fires" ("an excellent move", BuShips); sound-powered phones failed only "where lines were actually severed"; casualty power "not extensive enough" | WDR 24 [SRC] |
| **Exeter**, River Plate | 8″ | gunnery communications; wheelhouse communications | After control lost all communications; steered "by means of a chain of messengers" | Gazette, navyhistory.au [SRC] |
| **Kirishima** | 16″ | power to the rudders | Rudders stopped at 10° starboard | Lundgren, navweaps [SRC] |
| **Savannah**, bomb | 1 | the forward main switchboard shorted; an unprotected diesel-start relay circuit | Both turbo-generators tripped; a re-closed bus tie tripped again; 8 min aft, 16 min ship-wide; emergency phones 2 h 15 min; emergency steering cables next day | WDR 44 [SRC] |

**The destroyer report's summary (WDR 51)** [SRC]:
- "it is rare for even a minor hit to be sustained without some damage to wiring by blast and fragments… some
  dislocation of the power distribution system is almost certain to occur";
- "The majority of gyro failures in battle in destroyers have been due to electrical damage";
- "place little reliance on installed interior communications circuits".

### 3.3 Fire and water [SRC qualitative]

- **Fire:** cut energised cables start fires (Sterett, Johnston, Vincennes, Savannah), and much of San Francisco's
  wiring damage was "primarily due to fire". A 1974 Navy study traced fire spread "in many instances" to burning
  cable runs overhead, through bulkheads.
- **Water:** in North Carolina water entered FC connection boxes; the flexible core "acted as a wick", and a seeping
  feeder shorted an emergency board. In Savannah water travelled "more than 160 feet" inside cables and started a
  switchboard fire three days later. The armoured cable itself survived immersion. Helena's flooded cables grounded
  the steering, the telephone exchange and the IC board, with "flash-overs" at the bus ties.
- **Stuffing tubes:**
  - Northampton: "cable stuffing tubes are a recognized source of danger and will permit the spread of water".
  - Houston (CL-81): two magazines flooded "through cable stuffing tubes in bulkhead 57".
  - Boise: a trunk room flooded to 3 ft "through unpacked stuffing tubes".

**Not found:** fragment-versus-cable test data, burn-through times, IJN or German cable data, and cable weight per
metre.

---

## 4. The cut model

### 4.1 Wireways, not cables

A route segment (06 §3.3) lives in a **wireway** in its compartment. The wireway holds the modelled routes plus
`filler` cables (the hundreds of circuits the network does not model) [EST: 10–80 by space].
- **Geometry:** 8 cables across at 25 mm each, stacked in layers (`tray`).
- **Hits:** a burst at distance R puts `hits = n·w/(2πR)` fragments on it. These are 13 §4.4's line-target hits,
  counting fragments of 5 g or more still faster than 150 m/s, and through any plate in between by THOR.
- **Cables cut per hit:** `1.3 × min(layers, KE/500 J)`. A fragment goes on through the bundle, cutting about one and
  a third cables per layer.
- **A given cable is cut** with `1 − exp(−hits · c / n)`.

Because 13's fragments carry 14–160 kJ (ref T3), they cut through every layer: about 4 cables of a 24-cable tray per
hit. **Bundling therefore correlates cuts but does not shelter anything.** A route in a busy wireway is *more* likely
cut than the same cable run alone, because the wireway is a wider target. That is San Francisco's director trunk.

### 4.2 The numbers (ref T3)

P(a given cable cut), one burst, side spray crossing the run:

| Shell | wreck r | 2 m | 4 m | 8 m | 15 m |
|---|---|---|---|---|---|
| 5″/38 AAC | 1.5 m | 0.54 / 0.64 / 0.55 | 0.32 / 0.40 / 0.30 | 0.18 / 0.22 / 0.15 | 0.10 / 0.13 / 0.07 |
| 6″ HC | 1.8 m | 0.69 / 0.78 / 0.66 | 0.44 / 0.53 / 0.42 | 0.25 / 0.32 / 0.21 | 0.14 / 0.18 / 0.11 |
| IJN 20 cm common | 2.0 m | 0.72 / 0.81 / 0.69 | 0.47 / 0.56 / 0.40 | 0.27 / 0.34 / 0.23 | 0.15 / 0.20 / 0.12 |
| IJN 20 cm T91 AP | 1.5 m | 0.37 / 0.45 / 0.31 | 0.20 / 0.26 / 0.17 | 0.11 / 0.14 / 0.08 | 0.06 / 0.08 / 0.04 |
| 14″ HC | 3.6 m | 0.83 / 0.90 / 0.87 | 0.59 / 0.68 / 0.64 | 0.36 / 0.44 / 0.40 | 0.21 / 0.26 / 0.22 |
| 16″ AP | 2.6 m | 0.43 / 0.51 / 0.44 | 0.24 / 0.30 / 0.25 | 0.13 / 0.16 / 0.13 | 0.07 / 0.09 / 0.07 |

Columns: a single cable / one of a 24-cable wireway / the same behind 6 mm.

- **Inside the wreck radius** (`1.0·W^⅓` m, 11 §4.3) every run is cut by blast.
- **A 6 mm superstructure bulkhead** barely matters against these fragments.
- **Splinter boxing** matters only at 13 §5.4's thicknesses. It is a THOR check, already in the field.
- **A through-shot** (no burst) cuts each run in the spaces it crosses with 0.10 [EST].

### 4.3 Checks against the record

**Ralph Talbot (ref T6):** one burst in a destroyer's chart house (CHART) or director trunk (TRUNK):

| Shell | Burst in | Directors' circuits | IC to bridge | Search radar | Torpedo director | Circuits cut |
|---|---|---|---|---|---|---|
| 5″/38 AAC | CHART | 0.77 | 0.80 | 0.72 | 0.74 | 5.2 |
| 5″/38 AAC | TRUNK | 0.93 | 0.81 | 0.73 | 0.74 | 5.5 |
| IJN 20 cm common | CHART | 0.87 | 0.92 | 0.85 | 0.86 | 6.7 |
| IJN 20 cm common | TRUNK | 0.98 | 0.93 | 0.87 | 0.88 | 7.1 |
| IJN 20 cm T91 AP | CHART | 0.53 | 0.62 | 0.57 | 0.57 | 3.4 |
| IJN 20 cm T91 AP | TRUNK | 0.80 | 0.63 | 0.58 | 0.56 | 3.8 |

The record has all of them cut by one 8″ pattern. "One hit in the director trunk blinds a destroyer" is the normal
case, not bad luck. That is 06's argument for route separation, now with a number on it.

**San Francisco (ref T5):** with 23% of hits on turrets, belt, barbettes and hull (no cable effect), the model gives
**0.45** of hits cutting a lighting circuit, against the record's 19 of about 45 (0.42).

**Feeders against branches.** Feeders run low and inside armour, branches high and in the open. San Francisco's
"numerous branch circuits… severed" with "feeders generally intact" falls out of the routing; it needs no rule.

### 4.4 Fire, water and construction (rules) [EST, from §3.3]

**Fire:**
- **What fails:** a route segment in a burning space fails after a lognormal time (s 0.5).
- **When:** the median is **6 min** for rubber, lead-sheathed cable (RN; USN before 1942) and **15 min** for USN HF
  asbestos cable.
- **The short it makes:** fire failures short 60% of the time (`FAULT_MIX["fire"]`), so a fire on an energised run
  starts the protection chain like a fragment cut.
- **Spread:** burning cable runs add fire load and a spread chance through the bulkheads they pierce. That belongs to
  the fire model (4.5); this note only flags it.

**Water:**
- **The cable itself:** armoured cable under water keeps working.
- **Boxes:** junction and connection boxes in a flooded space fail after a median of 20 min.
- **Wicking:** a dry board or box fed by a cable *cut* in a flooded space faults within 24 h with 0.15, up to 50 m away.
  This is mainly a campaign effect (Savannah's three days).

**Construction and penetrations:**
- **Multicore (IJN):** about 4 circuits per cable, cut together; splicing takes ×2.5 as long.
- **Stuffing tubes:** each unpacked cable penetration of a holed or sprung bulkhead adds about 2 cm² of leak area
  (09's leak paths).

---

## 5. What a cut does electrically

### 5.1 Fault type

| Cause | Short | Ground | Open |
|---|---|---|---|
| Fragment | 0.30 | 0.40 | 0.30 |
| Blast tear | 0.25 | 0.30 | 0.45 |
| Fire | 0.60 | 0.35 | 0.05 |

These are [EST]. The record's order: energised cut cables short and burn (Sterett, Johnston), grounds were common and
cleared later (O'Brien, Helena), and some cuts were silent.

- **A de-energised cable** makes no fault.
- **"Open all non-vital circuits before an action"** (WDR 51) is therefore a doctrine knob. It cuts shorts and fires on
  those circuits, at the cost of their loads.
- **Insulated systems:** USN 450 V AC was an ungrounded delta, and the DC systems were two-wire insulated. A single
  ground only lights the ground detector. A *second* ground on the same system, on another conductor, is a short
  (`P_SECOND_GROUND` 0.25 per new ground) [SRC principle, EST rate].

### 5.2 Protection: who clears the fault

The fault walks up its path from where it is: branch device, then feeder breaker at the switchboard, then the
generator breaker. **The first unlocked device clears with probability SEL; the generator always does** [FIT/EST]:

| Device | Basic (1939–42: dashpot ACBs) | Selective (1943+: PQ relays) |
|---|---|---|
| Fuse | 0.90 | 0.95 |
| Branch breaker | 0.75 | 0.92 |
| Feeder breaker | 0.90 | 0.98 |
| Generator breaker | 1 | 1 |

- **Locked-in devices are skipped.** A short on a locked-in branch goes straight to the feeder, and in 10% of cases on
  to the generator.
- **An ABT between the fault and the clearing device** transfers the load, and the alternate source sees the same
  fault. Its fuses blow too (South Dakota). An ABT does not help a fault below it.
- **Manual transfer is no better for that fault,** since the operator has to isolate it first. It is safer against
  shock (no spurious closing).

**Electrical fire per short** [EST, set so South Dakota's night gives 1–2]:

| Situation | P(fire) |
|---|---|
| Cleared at the nearest device | 0.03 |
| Cleared further up | 0.10 |
| A crew resets into the fault before it is found (50% of trips) | +0.05 |
| Any locked-in device in the chain | at least 0.20 |

### 5.3 South Dakota's night, replayed (ref T4)

26 HE hits (1×14″, 6×8″, 8×6″, 11×5″) are spread over a 1942 fast battleship's superstructure. 23% of hits land on
armour and turrets, and 40% of superstructure hits pass through without bursting. The network has four main boards,
two emergency diesel boards, an IC/FC bus on an ABT, eight 5″ mounts, lighting in every space, steering and pumps.

| Variant | Circuits cut (of 42) | Shorts | Passed up | To a generator | Electrical fires | P(IC/FC bus dark) | Bus dark, median |
|---|---|---|---|---|---|---|---|
| 1942: basic, panels locked in | 31.8 | 11.6 | 8.8 | 0.95 | 1.8 | 0.56 | 3.4 min |
| 1942, panels not locked | 32.0 | 11.5 | 2.8 | 0.34 | 0.8 | 0.51 | 4.1 min |
| 1943+: selective, locked in | 31.9 | 11.6 | 8.3 | 0.21 | 1.9 | 0.21 | 3.9 min |
| Selective, not locked | 32.0 | 11.4 | 0.9 | 0.03 | 0.8 | 0.20 | 4.2 min |

What the record says: "about 35 kinds of circuit repaired", one FC/IC-wide loss of about 3 min, and the electrical
damage mostly "cannot be associated with specific hits". The model gets the bus loss at the right length and
roughly the right frequency (one in one battle against 0.56). It also shows the fix BuShips chose, selective generator
relays: the bus loss falls from 0.56 to 0.21, and whole-board trips from 0.95 to 0.21 per battle.

Locking panels in *raises* faults passed up and electrical fires. This is the cost of the 1940–42 fix against shock,
and it is why Johnston and Boise pulled breakers by hand.

The main director's circuits are cut in every replay. South Dakota's director wiring did need repair, so a
battleship's superstructure FC fails under this much fire unless its cables run in armoured tubes.

---

## 6. Restoring

| Action | Fixes | Time [FIT where marked] | Anchor |
|---|---|---|---|
| Reset a breaker at a manned board | Shock trip; fault after isolation | median 0.5 min (s 0.6), +0.15 min per further device | O'Brien "immediately"; White Plains 3 min for "most" |
| Reset at an unmanned panel | same | median 4 min (s 0.6): someone must go | [EST] |
| Restart and synchronise a turbo-generator | Generator trip | median 1.5 min (s 0.5) | South Dakota 1 min; Helena 1–1.5 |
| Emergency diesel auto-start | Loss of normal supply | 0.4 min | DD report |
| ABT spurious event (welded, generator cleared) | — | median 1 min | South Dakota |
| Find and isolate a fault in a damaged space | Before any reset holds | median 3 min (s 0.7) [FIT] | South Dakota's 3 min |
| Find a fault on a switchboard itself | Bus short | median 10 min (s 0.5) [FIT] | Savannah 8 / 16 min |
| Restart an LVP motor controller | After any interruption | 0.3 min if manned, else median 3 min | [EST] |
| Splice or jumper one cut cable | A cut vital circuit | median 30 min (s 0.6); multicore ×2.5 | Savannah phones 2 h 15 min for many; Sterett's steering cable next day |
| Rig casualty power | A cut feeder or board | 06 §5.1: 5–20 min a run | 06 |

**Blind resets.** Half the time a crew resets before the fault is found [EST]. The device trips again ("the generator
kept tripping out"; Savannah's bus tie), the attempt adds fire risk, and nothing is gained.

**LVR against LVP** [EST, from USN practice as generally described; NSTM ch. 320 not checked here]:
- **LVR (low-voltage release)** controllers restart their motor when power returns. Vital auxiliaries had them: fire
  and drainage pumps, steering, ventilation of manned spaces.
- **LVP (low-voltage protection)** controllers need a man to restart.

In ref T7, LVP pumps leave 30% of pumps off for more than 3 min after a shock event, against 3% with LVR.

---

## 7. Design knobs (ref T2, T2b, T7)

**A destroyer under a near miss at hull NSF 0.20:**

| Variant | P(a vital load dark) | Median / p90 min | Pumps off > 3 min |
|---|---|---|---|
| G0, emergency diesels, LVR pumps | 1.00 | 1.6 / 2.3 | 0.03 |
| G0, no emergency diesels | 1.00 | 2.0 / 3.2 | 0.06 |
| G0, LVP pumps | 1.00 | 1.6 / 2.4 | 0.30 |
| G1 (baseline) | 0.94 | 0.8 / 1.6 | 0.01 |
| G1, panels not locked in | 0.96 | 1.1 / 5.4 | 0.09 |
| G1, manual transfer instead of ABTs | 0.92 | 1.2 / 2.0 | 0.01 |
| G2 shock-tested | 0.08 | 0.4 / 1.2 | 0.00 |

**A battleship torpedoed** (400 kg TNT, 4 m down at the side; ref T2b):

| Hit | Grade | S at SWBD 1 | P(any trip) | Share tripped | P(vital dark) | Median / p90 min |
|---|---|---|---|---|---|---|
| Forward (45 m from the bow) | G0 | 0.071 | 1.00 | 0.20 | 0.99 | 1.8 / 2.7 |
| Forward | G1 | 0.071 | 0.19 | 0.01 | 0.16 | 0.7 / 1.6 |
| Forward | G2 | 0.071 | 0 | 0 | 0 | — |
| Amidships (100 m) | G0 | 0.354 | 1.00 | 0.41 | 1.00 | 2.6 / 4.1 |
| Amidships | G1 | 0.354 | 1.00 | 0.15 | 0.99 | 1.7 / 3.0 |
| Amidships | G2 | 0.354 | 0.44 | 0.01 | 0.30 | 0.8 / 2.2 |

North Carolina's G1 hit forward sits at 0.19: no trips is the likely outcome, as the record has.

**What they mean for the player:**
- **Fitting grade is a tech step, not a slider.** G1 is cheap: securing fixes and procedures. G2 costs money and a
  little weight: tested gear and resilient mounts [EST: +1–2% of electrical weight]. Each step roughly triples the
  shock a ship can take before minutes of blackout.
- **Lock-in is a real choice with a cost** (§5.3): fewer shock outages, more cascades and electrical fires.
- **Protection selectivity** (basic or selective) is a tech step from 1943 (USN). It turns whole-board trips into
  local ones.
- **ABT or manual transfer:** an ABT is faster after shock (0.8 against 1.2 min), but under G0 fittings it is a
  spurious-closing risk (South Dakota).
- **LVR on vital auxiliaries** is cheap and should be the default after about 1930 [EST].
- **Route separation** (06 §3.5) is the answer to §4.3. A vital load fed only through one superstructure wireway
  loses it 0.8–0.98 of the time to one burst there.

---

## 8. Proposed game model

### 8.1 Where it plugs in

This fills two hooks of 06 Model B and runs on 4.3a's `FunctionTable` (the service networks become more needs; nothing
that reads a function changes).

1. **`ShockEvent(ship, burst)`.** It comes from 15's near-miss roll, 4.6's torpedoes, mines and bombs, and from 13
   for armour hits (zone rates) and own guns (per-20-min rates).
   - **Per space:** compute S (§2.1) and draw the space's shared lognormal term.
   - **Per device in that space:** roll its item by the ship's grade (§2.2).
   - **Outcomes:** TRIPPED for breakers, fuses and generators; a contactor dropping out stops its motor; a busbar short
     is a fault on the board; an ABT spuriously closing gives a generator trip.
   - **Lamps:** reduce the zone's light level (and DC speed, 06 §4) unless the zone has battle lanterns.
2. **`CableHits(ship, burst)`.** It comes from the burst model (13's field, 11's wreck radius) for the burst space and
   its neighbours. Through-shots use the line of flight.
   - **Per wireway:** compute R, the plate between (THOR), hits and c (§4.1).
   - **Per route segment:** roll the cut.
   - **A cut energised circuit** rolls its fault type (§5.1).
   - **A short** is a `ProtectionEvent` (§5.2): walk up, trip one device, mark everything between the fault and that
     device blocked until the fault is isolated, and roll the fire.
   - **A ground** is counted per system and can pair into a short later.
3. **Fire and flood ticks:** segments in burning spaces fail on their timers; boxes in flooded spaces fail; wicking
   faults are rolled on the campaign clock.
4. **Repair** is 06 §5: DC parties take tasks in priority order, with the times of §6. "Pull the breakers to a wrecked
   zone" is a task that removes the zone's shorts and fire rolls.

### 8.2 Data the designer emits (additions to 06 §3.3)

- `grade` (from navy, year and refit) and `selectivity` (basic or selective) per ship.
- `locked: bool` per panel and branch device (default: unmanned panels locked in, as USN 1942).
- `attended: bool` per board and panel, from the watch bill (manned at general quarters).
- `lvr: bool` per motor consumer (default: vital ones on).
- `resilient: bool` per consumer or space (radar, FC gear; later all of them).
- `wireways`: per compartment, the routes in it, the `filler` count, and the plate between it and each neighbour (from
  the splinter plating).
- `cable_type`: rubber/lead, HF or multicore. It sets the fire timer, the splice multiplier and the circuits per
  cable.
- `insulated: true` (all WWII navies modelled) for the ground-pairing rule.

### 8.3 What the player sees

**In the log** (symptom ← cause, as 06 §6):
- "Lights out forward; SWBD 1 tripped by shock (near miss, NSF 0.21). Reset in 40 s."
- "FC/IC bus dark ← emergency fuses blown ← ABT transferred into a short ← IC branch to sky control cut (hit 12,
  SUP02). Fault isolated 3 min."
- "Electrical fire in SUP01 ← locked-in lighting panel fed a short."

**In the designer:** fitting grade and selectivity on the electrical page; lock-in as a doctrine toggle (shock trips
against fires); wireway exposure on 06's vulnerability overlay.

### 8.4 Cost

- **Shock:** one S per space per event, then one roll per device: a few hundred rolls for a battleship. Shock events
  are rare.
- **Cuts:** per burst, only the wireways in the burst space and its neighbours (usually under ten), each with a cached
  `(shell, R bucket, width, plate) → hits, KE` lookup from 13's field (`wireway_hits` caches already).
- **Protection:** a walk of three or four devices. No allocation per hit.

### 8.5 Tests (wide bands)

- **G0 destroyer, near miss at NSF 0.1–0.25:** a vital load is dark (P > 0.9), back within 1–4 min.
- **North Carolina (G1, 44,000 t), 400 kg torpedo forward:** P(any trip) < 0.3.
- **White Plains (G0, CVE), NSF 0.4 under her:** more than 40% of devices trip; back within 1–4 min.
- **One HE burst in a destroyer's director trunk:** P(directors' circuits cut) > 0.75.
- **San Francisco-like 45-hit battle:** 0.3–0.6 of hits cut a lighting circuit.
- **South Dakota-like 26-hit battle:**
  - with 1942 protection, P(IC/FC bus outage) 0.3–0.8, of 2–6 min;
  - with selective protection, under half the 1942 rate;
  - locked-in panels give more electrical fires than unlocked ones.
- **Replays hold** with the same seed.

---

## 9. Uncertainties

- **The size factor** (Δ^−1/6) is a judgement. It decides how much big ships shrug off UNDEX for their equipment, and
  it is what reconciles North Carolina with White Plains. Fragility is in hull-NSF units at the space, with no g
  thresholds.
- **G0 for the IJN and Kriegsmarine** is a default for lack of evidence, not a finding.
- **The fault mix** (short, ground, open) and the **fire chances** have no direct data. They are set from the record's
  narratives and to give South Dakota 1–2 fires.
- **Selectivity values** are fitted to one cascade in one battle (South Dakota). The direction is sourced (PQ relays,
  dashpots), the size is not.
- **Wireway filler counts and layout** in the test ships are invented. The checks pass with them, but the designer's
  routing will set the real exposure.
- **The cut criterion** (5 g, 150 m/s, 500 J per cable) is 13's and 14's estimate. Raising the energy per cable to 2 kJ
  changes little, because fragments carry ten times more. Raising the mass threshold would.
- **Cable fire timers** (6 and 15 min) and **wicking** (0.15 per 24 h) are placeholders.
- **The two test networks** are coarse (42 and 18 circuit groups). "32 of 42 cut" means groups of circuits, which
  matches "35 kinds" in spirit only.

---

## Sources

**War Damage Reports (NHHC / HyperWar):**
- [South Dakota WDR 57](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-south-dakota-bb57-war-damage-report-no57.html) ([PDF, HyperWar](https://www.ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20SOUTH%20DAKOTA%20(BB-57),%20GUNFIRE%20DAMAGE%20-%20Battle%20of%20Guadalcanal,%20November%2014-15,%201942.pdf))
- [North Carolina WDR 61](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-north-carolina-bb55-war-damage-report-no-61.html)
- [O'Brien WDR 28](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-obrien-dd415-war-damage-report-no-28.html)
- [Joseph Hewes / Electra etc., WDR 32](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-joseph-hewes-apa22-war-damage-report-no-32.html)
- [San Francisco WDR 26](https://ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20SAN%20FRANCISCO%20(CA-38),%20GUNFIRE%20DAMAGE%20-%20Battle%20of%20Guadalcanal,%20November%2013,%201942.pdf)
- [Boise WDR 24](https://ibiblio.org/hyperwar/USN/WarDamageReports/WarDamageReportCL47/WarDamageReportCL47.html)
- [Sterett WDR 33](https://ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20STERETT%20(DD-407),%20GUNFIRE%20DAMAGE%20-%20Battle%20of%20Guadalcanal,%20November%2013,%201942.pdf)
- [Quincy, Astoria, Vincennes WDR 29](https://ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20QUINCY%20(CA-39),%20U.S.S.%20ASTORIA%20(CA-34)%20and%20U.S.S.%20VINCENNES%20(CA-44),%20GUNFIRE%20AND%20TORPEDO%20D.pdf)
- [Savannah WDR 44](https://ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20SAVANNAH%20(CL-42),%20BOMB%20DAMAGE%20-%20Gulf%20of%20Salerno,%20Italy,%20September%2011,%201943.pdf)
- [Destroyers: gunfire, bomb, kamikaze, WDR 51](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/destroyer-report-gunfire-bomb-kamikaze-damage.html)
- [Submarine war damage, WDR 58 §18](https://ibiblio.org/hyperwar/USN/rep/WDR/WDR58/WDR58-18.html) and [§19](https://ibiblio.org/hyperwar/USN/rep/WDR/WDR58/WDR58-19.html)
- [Northampton WDR 41](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-northampton-ca26-war-damage-report-no-41.html)
- [Houston (CL-81) WDR 53](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-houston-cl81-war-damage-report-no-53.html)
- [Helena, Pearl Harbor damage report and statements](http://www.researcheratlarge.com/Ships/CL50/1942JanuaryPearlHarborDamageReport.html)
- [War Damage to British Naval Vessels: bombs to September 1941 (BuShips summary)](https://ibiblio.org/hyperwar/USN/rep/WDR/WAR%20DAMAGE%20TO%20BRITISH%20NAVAL%20VESSELS%20-%20Summary%20of%20Damage%20by%20Bombs%20to%20September%202,%201941.pdf)
- [WDR index](https://ibiblio.org/hyperwar/USN/rep/WDR/index.html) (Marblehead, Saratoga, Salt Lake City, damage summaries)

**Ships' logs and accounts:**
- [Helm, Pearl Harbor log](https://ibiblio.org/hyperwar/USN/ships/logs/DD/dd388-Pearl.html)
- [USS White Plains (Wikipedia, from DANFS)](https://en.wikipedia.org/wiki/USS_White_Plains_(CVE-66))
- Exeter at the River Plate: [London Gazette despatch](https://fepow.family/Supplement/London_Gazette/River_Plate_Battle/html/part_ii.htm), [navyhistory.au](https://navyhistory.au/hms-exeter-at-the-battle-of-river-plate/2/)
- [Kirishima damage analysis (Lundgren, navweaps)](https://navweaps.com/index_lundgren/kirishimaDamageAnalysis.php)

**Shock and equipment:**
- Patents: [US2491657 Shockproof tripping armature (ITE, 1943)](https://patents.google.com/patent/US2491657A/en) · [US2451239 (GE, 1948)](https://patents.google.com/patent/US2451239A/en) · [US4710739](https://patents.google.com/patent/US4710739)
- DTIC and NRL reports:
  - [NRL MR 4003, switchgear breaker shock (Clements 1979)](https://apps.dtic.mil/sti/pdfs/ADA070785.pdf)
  - [Shock machines' history (ADA326738)](https://apps.dtic.mil/sti/pdfs/ADA326738.pdf)
  - [NRL 8631](https://apps.dtic.mil/sti/tr/pdf/ADA121051.pdf)
  - [NRL 1963 machine velocities](https://apps.dtic.mil/sti/pdfs/AD0408280.pdf)
  - [NRL B-2054 / AD1175599](https://apps.dtic.mil/sti/trecms/pdf/AD1175599.pdf)
  - [Fluorescent lamp shock (Farnsworth 1952)](https://apps.dtic.mil/sti/pdfs/ADA955958.pdf)
- Crossroads inspection reports:
  - [Pennsylvania](https://apps.dtic.mil/sti/pdfs/AD0366708.pdf)
  - [Nagato](https://apps.dtic.mil/sti/pdfs/AD0366709.pdf)
  - [Prinz Eugen](https://apps.dtic.mil/sti/pdfs/AD0366714.pdf)
  - [Fallon](https://apps.dtic.mil/sti/pdfs/AD0366753.pdf)
  - [Carteret](https://apps.dtic.mil/sti/pdfs/AD0366747.pdf)
  - [ARDC-13](https://apps.dtic.mil/sti/pdfs/AD0366763.pdf)
  - [Skipjack](https://apps.dtic.mil/sti/pdfs/AD0367467.pdf)
- [RN, "Effect of Shock on Ships' Machinery" (Papers on Engineering Subjects 17)](https://library.imarest.org/record/10519/files/v6b17p03.pdf)
- [MacLaren, Admiralty electrical engineering (JNE 1960)](https://library.imarest.org/record/9717/files/v13b1p07a.pdf)
- [Navy SBIR N103-222](https://www.navysbir.com/n10_3/N103-222.htm)
- [DDAM interim values (vibrationdata)](https://blog.vibrationdata.com/?p=150278)
- [Taiwan MND shock summary (secondary)](https://www.mnd.gov.tw/File/7077)

**Cables and systems:**
- [NAVPERS 10622 cable designations (rfcafe)](https://www.rfcafe.com/references/electricity-basic-navy-training-courses/electricity-basic-navy-training-courses-Appendix-III.htm)
- [GE heat-and-flame-resistant cable patent US2155243](https://patents.google.com/patent/US2155243A/en)
- [Your Battleship, Army Newsmap 1944](https://www.geographicus.com/P/AntiqueMap/yourbattleship-newsmap-1944)
- [USS Coral Sea specifications](https://usscoralsea.net/specs.php)
- [Copper in shipbuilding](https://copper.org/education/history/60centuries/modern/shipbuilding.php)
- [Mk 37 director (okieboat)](https://okieboat.com/Gun%20Director.html)
- [Cable fires study, 1974 (TRID)](https://trid.trb.org/View/17205)
- [Ungrounded ship systems (CR4, citing NSTM 320)](https://cr4.globalspec.com/thread/71394/Electric-grounding-in-ship)
- [Naval Gazing: survivability and mission kills](https://www.navalgazing.net/Survivability-Mission-Kills)

**Project docs:** `06_service_networks.md` (draft, project), `11_blast_confinement.md`, `13_fragmentation.md`,
`13_nonpenetrating_hits.md`, `14_spall_behind_armour.md`, `15_near_misses.md`, `09_damage_control.md`, `fighting.md` (4.3a,
4.5).
