# Evasion: the motion contest between a ship and the gun that wants to hit it
Status: final    Updated: 2026-10-09    Request: -

*Companion to [`fire-control-research.md`](fire-control-research.md). That note covers how a shooter gets a solution; this one covers what the target can do to spoil it, and what the shooter can do back. The scope runs from battleships to MTBs, 1900–1990: how each class should evade, how much evasion is enough at a given range, when an attack run becomes suicidal, and which predictive systems a game can give shooters to make evaders' lives harder. Everything stays within what was physically achievable.*

**Companion code:** [`evasion_ref.py`](evasion_ref.py) (numpy, about 1,200 lines; imports [`fire_control_ref.py`](fire_control_ref.py)). `python3 evasion_ref.py` prints every table below (tables 1–16) in about 40 minutes; `quick` prints the dynamics and envelope tables in seconds. `python3 evasion_ref.py <table_function>` runs one table.

**Raw research appendices** (full sourcing):
- [`manoeuvring.md`](fire-control-research/manoeuvring.md): turning circles, rudder and speed data by class.
- [`evasion-history.md`](fire-control-research/evasion-history.md): salvo chasing, evasion doctrine, destroyer and small-craft attacks, fire-control countermeasures, operations research.

**Tags** as in the other docs: **[S]** sourced, **[INFERRED]** my derivation or tuning, **[UNCERTAIN]** thin sources. Every number in a "model" table is output of the companion code. Unless stated otherwise, model runs use combat conditions (`combat = 1.5`, as calibrated in the fire-control note) and clear visibility.

---

## 0. TL;DR for the game

1. **A manoeuvring target defeats the gun by two different mechanisms. Model them separately.**

   | | Dodge | Corruption |
   |---|---|---|
   | **What happens** | The target moves off the predicted point after the gun fires: during latency plus time of flight | Every course change leaves the computer's idea of the target's course wrong until it re-converges, so every salvo in that window is aimed with a wrong rate |
   | **Limit** | Hard kinematic limit; no computer predicts a decision not yet made | None; a better predictor can largely remove it |
   | **What it depends on** | The shell's flight time, almost nothing else | The predictor's motion model |

   The model's main result is that **corruption, not dodging, is what made WWII evasion work.** A battleship can only get about 20 m off its predicted point in 20 s, about 70 m in 30 s and about 220 m in 45 s (table 2). Yet zigzagging or salvo chasing halved WWII hit rates at 9–20 kyd (tables 4 and 16).

2. **Turning moves a ship across its own heading, which is always along its narrow dimension.** Seen from any bearing, the displacement falls on the axis where the hull is thinnest: the range window if she is broadside, the beam if she is bow-on. So evasion works at every aspect. A second shooter on another bearing does not cancel it (table 11).

3. **Changing speed is almost useless for a big steam ship.** Stopping engines puts an Iowa only 20 m short of her predicted point after 30 s and 78 m after 60 s. It is real for gas-turbine ships (backing gives about 200 m in 30 s). It is decisive for small craft, though slowing down is usually a mistake for them (§7).

4. **What is enough, by class** (tables 4–6, 15):

   | Target | Range | Evasion needed |
   |---|---|---|
   | Battleship / cruiser vs WWII optical fire control | 6–20 kyd | ±30° course changes every 1–2 min, or turning after each enemy salvo lands, cut the enemy's hits by 35–55%, even at 9 kyd. ±15° is too little |
   | Battleship / cruiser vs WWII radar fire control | below ~6 kyd | Nothing helps. Fight steady |
   | | 9–20 kyd | Same styles cut hits 15–55%; salvo chasing does best |
   | Battleship / cruiser, any WWII fire control | beyond ~25 kyd | Every style works; salvo chasing and ±45° every minute do best |
   | Battleship / cruiser vs model-based digital fire control | inside ~20 kyd | Evasion buys 0–15%. Fight |
   | Destroyer | below 4 kyd | Evasion is useless and can even raise enemy hits (shells fly 5–7 s) |
   | | 8–12 kyd | ±30–45° zigzag cuts radar-directed 5″ hits by 50–75% |
   | MTB | any range | A continuous full-speed weave (±40°, 30 s period) is by far the best defence: it cuts 5″/38 hits 3–5× at 1–3 km. Slowing down is fatal |

5. **Suicidal thresholds** (tables 8–9; P = chance of being stopped before launching):

   | Attack | Defence | Result |
   |---|---|---|
   | Destroyer, steady approach | Radar-directed 1944-standard battleship, day or night | Stopped 71% of the time even launching at 10 kyd |
   | Destroyer, weaving or salvo chasing | Same | 0.21 at 10 kyd, about 0.3–0.5 at 8 kyd, 0.5–0.7 at 6 kyd, 0.75+ at 4 kyd |
   | Destroyer | No radar, at night | Untouched outside the 4 km illumination range (Surigao in reverse) |
   | MTB | Alert radar destroyer, by day | Suicidal at any torpedo range: P ≥ 0.2 even weaving at 4 km, 0.9 at 1 km |
   | MTB | Non-radar ship, at night | Safe outside the illumination range. Fatal inside about 1 km unless the approach is unseen |

6. **What actually beats evaders is a better motion model, not a wider net** (tables 10, 13, 14). In the model:
   - **Reading the target's heading from her silhouette ("aspect"),** then aiming as if she will hold it, recovers most of the hits lost to zigzagging and chasing.
   - **Holding each salvo until the last one lands ("fire on the splash")** and leading the target's known habit adds a little more.
   - **Deliberately widening the pattern, the rocking ladder, splitting the salvo, or aiming at the mean of sampled futures** gain almost nothing. Against the same evasion styles they changed hits by −26% to +17% compared with plain tracking, mostly within ±10%.
   - **A turn-following digital filter** kills circling targets, but **weaving fools it:** it over-extrapolates every turn.

7. **Evading costs WWII ships their own gunnery** (table 12). A ±30°/2 min zigzag costs a 1941 optical battleship 27% of her hits, and ±45°/45 s costs 44%. A digital fire-control system loses nothing. **WWII evasion is a trade-off; post-war evasion is free.**

---

## 1. The physics of the gap

### 1.1 What the gun knows and when

- The gun fires at **t₀** using the computer's estimate of the target's motion, which is already **latency** seconds old: about 15 s for a Dreyer table, 2–3 s for a Mk 8, 0.5 s digital (fire-control note §5).
- The shell lands at **t₀ + TOF**. TOF is about 13 s at 10 kyd, 30 s at 20 kyd and 51 s at 30 kyd for the 16″/50 [S]. A 5″/38 takes about 6 s at 4 kyd. A 40 mm takes about 3 s at 2 km.
- **The target sees the flash** (heavy-gun flash and smoke are visible to the horizon by day [S, muzzle-flash note]). After a reaction time of about 4 s for a big ship and 2–3 s for small craft [INFERRED], she can begin a manoeuvre the gun could not have known about.
- **The target sees the splashes** of each salvo and knows which side they fell. That is the basis of salvo chasing (§8).

### 1.2 Hull dynamics (model table 1)

Each hull uses first-order (Nomoto) yaw with a rudder-rate limit. Partial rudder follows TD ∝ (35/δ)^0.55 [S fit to trawler trials]. Speed loss in turns and first-order speed lags are included. Steady turning radius is fitted so the tactical diameter (TD) matches the sources (manoeuvring appendix).

| Hull | TD m (source) | 90° | 180° | 360° | Speed after 180° |
|---|---|---|---|---|---|
| BB fast (Iowa, 31 kn) | 744 (744 [S]) | 61 s | 101 s | 190 s | 22.8 kn |
| BB WWI (QE, 24 kn) | 640 [UNCERTAIN] | 66 | 112 | 214 | 17.9 |
| CA (Baltimore, 33 kn) | 800 [INFERRED] | 60 | 112 | 230 | 21.1 |
| DD single rudder (Fletcher, 36 kn) | 869 (869 [S]) | 60 | 121 | 247 | 21.6 |
| DD twin rudder (Sumner, 34 kn) | 650 (650 [S]) | 48 | 95 | 194 | 20.5 |
| FF gas turbine (Perry, 29 kn) | 650 [INFERRED] | 56 | 110 | 226 | 17.6 |
| FAC (Osa, 40 kn) | 230 [INFERRED] | 15 | 31 | 64 | 22.1 |
| MTB / PT (Elco 80, 40 kn) | 150 [INFERRED] | 10 | 22 | 45 | 20.0 |

Checks against sources:
- Iowa and Ticonderoga turn 180° in about 2 min [S]. PT boats reverse course in 9–22 s [S].
- In service, big ships used hard rudder only for emergencies; rudder angle plus knots was kept at or below 30 [S, New Jersey and Ticonderoga ship-handling articles].
- **Model note:** AI ships should use hard rudder for evasion only, and pay for it in speed loss, heel and gunnery.

### 1.3 How far off the prediction a ship can get (model table 2)

**Reach** is the farthest lateral offset from the straight-line predicted point after T seconds: hard over, then rudder amidships at the best moment. In metres:

| Hull | 10 s | 20 s | 30 s | 45 s | 60 s | 90 s |
|---|---|---|---|---|---|---|
| BB fast (Iowa) | 2 | 20 | 71 | 218 | 417 | 851 |
| BB WWI (QE) | 1 | 13 | 48 | 150 | 296 | 625 |
| CA (Baltimore) | 3 | 28 | 91 | 245 | 437 | 858 |
| DD (Fletcher) | 6 | 44 | 118 | 274 | 457 | 879 |
| DD (Sumner) | 7 | 51 | 138 | 312 | 509 | 939 |
| FF (Perry) | 4 | 33 | 97 | 236 | 405 | 792 |
| FAC (Osa) | 64 | 212 | 390 | 681 | 981 | 1,597 |
| MTB (Elco) | 87 | 241 | 424 | 712 | 1,030 | 1,593 |

Speed changes, metres short of the predicted point (model table 2, second block):

| Hull | Stop engines, 30 s | Stop, 60 s | Full astern, 30 s | Full astern, 60 s |
|---|---|---|---|---|
| Iowa | 20 | 78 | 66 | 240 |
| Fletcher | 47 | 177 | 168 | 541 |
| Perry (gas turbine) | 43 | 159 | 208 | 595 |
| MTB | 399 | 999 | 525 | 1,142 |

Small craft can run a whole circle within one shell's flight, so the full envelope matters for them: speed and heading together.

### 1.4 When pure dodging pays (model table 3, analytic)

Analytic test:
- **Target:** after each flash she gambles hard left / hold / hard right with equal odds.
- **Gun:** aims at the straight-line prediction.
- **Errors:** WWII-quality fire-control error (1.8% of range) or digital (0.8%), plus shell dispersion.

Hits as a fraction of hits on a steady target (WWII / digital):

| Target vs gun | Short | | | Long |
|---|---|---|---|---|
| Iowa vs 16″ | 15 kyd: 1.00 / 0.99 | 20: 0.99 / 0.96 | 25: 0.97 / 0.88 | 30: 0.91 / 0.72; 35: 0.84 / 0.56 |
| Baltimore vs 8″ | 12: 1.00 / 1.00 | 16: 0.99 / 0.96 | 20: 0.95 / 0.84 | 24: 0.87 / 0.63; 28: 0.76 / 0.45 |
| Fletcher vs 5″/38 | 5: 1.00 / 1.00 | 9: 0.99 / 0.96 | 11: 0.95 / 0.83 | 13: 0.89 / 0.64 |
| Fletcher vs 16″ | 10: 1.00 / 1.00 | 15: 0.99 / 0.97 | 20: 0.97 / 0.90 | 25: 0.93 / 0.78; 30: 0.88 / 0.64 |
| MTB vs 40 mm | 2 km: 1.00 / 1.00 | 3 km: 0.98 / 0.96 | 4 km: 0.89 / 0.73 | 5 km: 0.75 / 0.50 |
| MTB vs 5″/38 | 2 km: 1.00 / 1.00 | 4 km: 0.97 / 0.89 | 6 km: 0.84 / 0.55 | 8 km: 0.65 / 0.37 |

What this says:
- **Pure dodging only pays at long range, and pays more against good shooters.** Against a sloppy shooter the dodge is lost in his own error. Against a precise one it decides the shot.
- **The model engagements below show far bigger effects at 9–20 kyd.** The difference is the corruption mechanism: the next sections are mostly about it.

---

## 2. Evasion styles (what the code's captains do)

| Style | Rule | Information it needs | Historical form |
|---|---|---|---|
| `steady` | Hold course | — | Fighting ship, gun duel |
| `zigzag A/P` | ±A° legs every P s | Nothing | Convoy and screen habit; Komandorski "abrupt zigs and zags" [S] |
| `weave A/P` | Continuous sinusoidal heading, ±A°, period P | Nothing | Fishtailing (Hoel at Samar [S]) |
| `random A/D` | Random headings from {±A, ±A/2, 0} at random times (mean dwell D); optional speed jinks | Nothing | Destroyers and small craft under fire |
| `flash A` | On each enemy flash, gamble hard left / hold / hard right; otherwise steady | Enemy flashes | Harwood found a drastic course change at the enemy's first salvo helpful [S] |
| `chase A` | On each splash: steer toward it (probability p), otherwise gamble | Enemy splashes | Samar, Komandorski, River Plate [S] |

All styles act relative to a **base course** (the mission). They are limited to ±A° from it, and they hold steady during the last 30 s (destroyers) or 10 s (MTBs) before a torpedo launch [INFERRED].

---

## 3. Battleships

Model table 4: 9 × 16″/50 every 30 s against an Iowa-type target at 25 kn starting broadside, 15 min, combat conditions. Hit % per shell:

| Shooter | kyd | Steady | Zigzag 30/120 | Weave 30/90 | Random | Flash | Chase |
|---|---|---|---|---|---|---|---|
| WWII optical (Mk 8), track | 12 | 19.5 | 11.7 | 19.1 | 19.9 | 15.4 | 10.5 |
| | 20 | 7.1 | 3.6 | 6.7 | 5.1 | 5.4 | 4.1 |
| | 28 | 3.4 | 1.8 | 2.7 | 2.1 | 2.2 | 1.7 |
| WWII optical, aspect | 12 | 18.8 | 14.2 | 16.8 | 19.6 | 17.0 | 16.1 |
| | 20 | 6.8 | 5.1 | 7.6 | 6.4 | 7.2 | 6.5 |
| | 28 | 2.9 | 2.5 | 3.3 | 2.7 | 3.2 | 2.0 |
| WWII radar (Mk 8 + Mk 13), track | 12 | 18.2 | 15.0 | 15.4 | 18.1 | 16.4 | 14.1 |
| | 20 | 7.7 | 5.1 | 6.6 | 6.1 | 6.1 | 3.4 |
| | 28 | 3.6 | 1.9 | 2.7 | 3.0 | 2.3 | 1.6 |
| WWII radar, anticipate | 12 | 18.4 | 17.4 | 20.5 | 18.9 | 17.8 | 18.5 |
| | 20 | 6.7 | 6.7 | 6.9 | 6.8 | 6.4 | 6.7 |
| | 28 | 3.3 | 2.8 | 3.0 | 3.2 | 3.0 | 2.7 |
| Digital (turn filter), track | 12 | 36.2 | 29.9 | 22.3 | 32.2 | 24.8 | 23.0 |
| | 20 | 15.1 | 10.6 | 6.9 | 11.1 | 8.3 | 5.9 |
| | 28 | 8.4 | 2.9 | 1.5 | 4.3 | 2.9 | 2.1 |
| Digital, anticipate | 12 | 30.0 | 32.8 | 28.3 | 33.7 | 31.7 | 34.2 |
| | 20 | 13.0 | 11.5 | 11.7 | 12.2 | 11.0 | 11.5 |
| | 28 | 9.0 | 5.9 | 5.6 | 5.6 | 4.8 | 5.1 |

Doctrines are defined in §9.

**How much is enough** (model table 15; hit % per shell):

| Shooter | kyd | Steady | ZZ 15/180 | ZZ 30/120 | ZZ 45/60 | Flash 15 | Flash 35 | Weave 15/90 | Chase 45 |
|---|---|---|---|---|---|---|---|---|---|
| WWII radar, track | 12 | 18.2 | 18.1 | 15.0 | 13.1 | 17.8 | 16.4 | 21.1 | 14.1 |
| | 20 | 7.7 | 7.0 | 5.1 | 4.1 | 7.6 | 6.1 | 7.3 | 3.4 |
| | 28 | 3.6 | 3.3 | 1.9 | 1.8 | 3.6 | 2.3 | 3.3 | 1.6 |
| Digital, anticipate | 12 | 30.0 | 29.8 | 32.8 | 28.3 | 32.7 | 31.7 | 32.2 | 34.2 |
| | 20 | 13.0 | 12.9 | 11.5 | 9.0 | 13.9 | 11.0 | 12.4 | 11.5 |
| | 28 | 9.0 | 7.5 | 5.9 | 4.5 | 6.5 | 4.8 | 5.8 | 5.1 |

**What evasion costs her own guns** (model table 12; she fires at a steady battleship at 20 kyd while zigzagging; hit %):

| Own fire control | Steady | ±30°/120 s | ±30°/60 s | ±45°/45 s |
|---|---|---|---|---|
| 1916 Dreyer | 4.0 | 3.2 | 2.5 | 2.0 |
| 1941 Mk 8 optical | 8.5 | 6.2 | 5.7 | 4.8 |
| 1945 Mk 8 + radar | 8.1 | 7.2 | 5.3 | 4.5 |
| 1975 digital | 16.2 | 15.6 | 16.5 | 16.9 |

**Short range** (model table 16; 16″, 10 min, hit % per shell):

| Shooter | kyd | Steady | Zigzag 30/120 | Chase | Flash |
|---|---|---|---|---|---|
| WWII optical, track | 6 | 51.2 | 42.7 | 36.5 | 46.2 |
| | 9 | 33.1 | 19.4 | 15.3 | 23.3 |
| WWII radar, track | 6 | 42.7 | 38.4 | 44.2 | 41.5 |
| | 9 | 30.6 | 26.3 | 22.0 | 23.4 |
| Digital, anticipate | 6 | 52.0 | 54.9 | 56.7 | 55.6 |
| | 9 | 45.3 | 43.0 | 45.6 | 43.1 |

**Battleship verdict:**
- **Even at short range, course changes corrupt a WWII optical solution.** At 9 kyd the shell flies about 12 s and the ship can dodge almost nothing, yet zigzagging or chasing cuts optical hits by 40–55%. Radar ranging halves that benefit. A model-based digital predictor removes it entirely inside 9 kyd. At close range your own hits matter most, so the price in your own gunnery (table 12) usually decides: fight steady at short range unless you are outgunned.
- **At 12–20 kyd against WWII fire control:**
  - **Zigzag ±30° every 2 min, or chase salvos.** Either cuts enemy hits by 35–55% at 20 kyd. ±15° buys almost nothing.
  - **Turning after every salvo is decisive (chasing, about −55%),** because it lands each course change just after the enemy took his data.
  - **The cost** is about 10–27% of your own hits. Against an equal opponent at 20 kyd that is a good trade if you want to survive, and a bad one if you need to sink him first.
- **Beyond about 25 kyd,** every style works. Salvo chasing and ±45° every minute are best, and real dodging starts to add to corruption.
- **Weaving is useless against WWII rangekeepers** (they average it out) but **devastating against a turn-following digital filter** (15.1 → 6.9% at 20 kyd).
- **Against a model-based digital predictor** (anticipate), only long-range dodging still works: 9.0 → 4.5–5.9% at 28 kyd.

**History check.** River Plate, Komandorski and Samar all show the 2–5× reductions the model gives for chasing and zigzagging at 10–20 kyd:
- Harwood: Graf Spee's turns "threw out" British fire [S].
- Salt Lake City: over 200 shells within 50 yd but only 4–5 hits in 3.5 h [S].

---

## 4. Cruisers

Model table 5: 9 × 8″/55 every 20 s against a Baltimore-type target at 32 kn. Hit % per shell:

| Shooter | kyd | Steady | Zigzag | Weave | Random | Flash | Chase |
|---|---|---|---|---|---|---|---|
| WWII optical, track | 10 | 15.7 | 10.4 | 13.1 | 14.1 | 13.3 | 9.3 |
| | 16 | 6.3 | 2.3 | 4.9 | 4.1 | 3.4 | 2.5 |
| | 22 | 2.8 | 1.0 | 2.2 | 1.3 | 1.7 | 0.9 |
| WWII optical, aspect | 16 | 6.1 | 3.7 | 5.8 | 4.6 | 5.1 | 3.7 |
| WWII radar, track | 16 | 6.0 | 3.2 | 3.8 | 3.8 | 3.5 | 2.9 |
| WWII radar, anticipate | 16 | 5.7 | 4.7 | 5.5 | 4.6 | 5.7 | 4.7 |
| Digital, track | 10 | 30.9 | 22.7 | 20.3 | 28.2 | 25.3 | 22.4 |
| | 16 | 12.1 | 6.4 | 3.2 | 7.3 | 5.5 | 3.4 |
| | 22 | 6.5 | 1.8 | 0.7 | 2.2 | 1.3 | 1.2 |
| Digital, anticipate | 16 | 11.1 | 7.4 | 9.3 | 10.5 | 8.7 | 7.6 |
| | 22 | 4.8 | 2.9 | 3.4 | 3.8 | 3.7 | 2.7 |

**Cruiser verdict.** Same pattern as battleships, but stronger: the cruiser is faster and its hitting window is smaller.
- **At 16 kyd,** a zigzag or salvo-chasing cruiser takes about 40% of the hits a steady one would.
- **At 22 kyd,** about a third.
- **This is Komandorski:** the model gives about 0.9% for the 3.5-hour stern chase against about 0.5% recorded (fire-control note §11). Cruiser gunnery against evading cruisers was historically 0.4–0.5% (Java Sea 1,271 rounds for 5 hits [S]).

---

## 5. Destroyers as targets

Model table 6: 8 × 5″/38 every 4 s (WWII, Mk 37 + radar) or one 5″/54 every 3 s (digital), against a 35 kn Fletcher. Hit % per shell:

| Shooter | kyd | Steady | Zigzag | Weave | Random | Flash | Chase |
|---|---|---|---|---|---|---|---|
| WWII 5″/38, track | 4 | 24.4 | 20.1 | 25.3 | 23.9 | 18.8 | 25.7 |
| | 8 | 6.9 | 3.3 | 3.3 | 5.1 | 3.9 | 3.7 |
| | 12 | 2.4 | 0.9 | 1.1 | 1.5 | 1.0 | 1.0 |
| WWII 5″/38, anticipate | 8 | 6.3 | 3.0 | 5.5 | 4.5 | 4.7 | 6.0 |
| Digital 5″/54, track | 4 | 36.8 | 37.1 | 42.7 | 40.3 | 37.8 | 43.7 |
| | 8 | 12.0 | 9.6 | 9.8 | 10.8 | 10.9 | 11.2 |
| | 12 | 4.2 | 2.7 | 1.7 | 3.4 | 2.6 | 2.5 |
| Digital 5″/54, anticipate | 8 | 14.2 | 9.2 | 13.1 | 15.7 | 12.9 | 13.2 |

Sufficiency (model table 15), WWII 5″/38 radar, hit %:

| kyd | Steady | ZZ 15/180 | ZZ 30/120 | ZZ 45/60 | Flash 15 | Flash 35 | Weave 15/90 | Chase |
|---|---|---|---|---|---|---|---|---|
| 4 | 24.4 | 20.6 | 20.1 | 19.4 | 20.7 | 18.8 | 20.6 | 25.7 |
| 8 | 6.9 | 5.0 | 3.3 | 1.9 | 4.7 | 3.9 | 4.1 | 3.7 |
| 12 | 2.4 | 1.8 | 0.9 | 0.6 | 2.0 | 1.0 | 1.5 | 1.0 |

**Destroyer verdict:**
- **Inside about 4 kyd evasion is nearly worthless:** a 5-inch shell flies about 6 s. Salvo chasing even raises the enemy's hits, because the turn bleeds speed and the ship turns toward where the gun is already pointing.
- **At 8–12 kyd** a hard zigzag (±45° every minute) cuts radar-directed WWII 5-inch hits by 70–75%.
- **Against digital guns** the same zigzag cuts hits only by about a third at 8 kyd, and by about two-thirds at 12 kyd.

### 5.1 Torpedo attacks on a battleship

Model table 8. Setup:
- The attacker starts 18 kyd away on the target's bow and runs in at 35 kn.
- She must hold 30 s steady to fire.
- Per-hit stopping chances [INFERRED from damage notes 03/08]: 16″ AP 0.10 (it mostly passes through), 5″ 0.06, 40 mm 0.01, 6″ 0.15.

Cell = P(stopped before launch) / mean hits before launch:

| Defence | Launch at | Steady | Weave | Flash | Chase |
|---|---|---|---|---|---|
| **USN 1944 battleship, day** (16″ + 10 × 5″/38 Mk 37 radar + 16 × 40 mm) | 10 kyd | 0.71 / 9.1 | 0.21 / 3.7 | 0.25 / 3.9 | 0.21 / 2.0 |
| | 8 kyd | 0.92 / 11.3 | 0.29 / 4.9 | 0.42 / 5.5 | 0.50 / 4.2 |
| | 6 kyd | 0.92 / 13.3 | 0.54 / 9.6 | 0.71 / 12.0 | 0.71 / 7.1 |
| | 4 kyd | 1.00 / 14.9 | 0.75 / 13.9 | 0.92 / 15.2 | 0.83 / 10.8 |
| **Same, night** (radar 5″; optics 4 km) | 10 kyd | 0.71 / 10.4 | 0.25 / 4.3 | 0.21 / 2.7 | 0.29 / 2.7 |
| | 8 kyd | 0.92 / 12.2 | 0.38 / 5.8 | 0.33 / 5.0 | 0.42 / 3.6 |
| | 6 kyd | 1.00 / 13.0 | 0.50 / 9.4 | 0.58 / 9.9 | 0.62 / 7.1 |
| | 4 kyd | 1.00 / 13.0 | 0.71 / 15.3 | 0.88 / 14.8 | 0.79 / 11.0 |
| **Night, no radar** (searchlights 4 km) | 10 / 8 / 6 kyd | 0.00 | 0.00 | 0.00 | 0.00 |
| | 4 kyd | 0.04 / 2.3 | 0.04 / 1.4 | 0.04 / 2.4 | 0.04 / 2.3 |
| **WWI dreadnought, day** (12 × 6″ casemates, Dreyer) | 10 kyd | 0.12 / 1.7 | 0.08 / 0.8 | 0.00 / 0.6 | 0.00 / 0.5 |
| | 8 kyd | 0.46 / 3.3 | 0.25 / 1.4 | 0.12 / 1.1 | 0.00 / 0.8 |
| | 6 kyd | 0.62 / 5.3 | 0.38 / 2.5 | 0.17 / 2.7 | 0.08 / 1.2 |
| | 4 kyd | 0.92 / 7.1 | 0.38 / 3.0 | 0.29 / 4.0 | 0.42 / 3.1 |

**What the table says:**
- **A steady approach is suicide against anything competent.** Every destroyer captain in the record weaved, zigzagged or chased salvos on the way in.
- **Against a 1944 radar battleship, an evasive attack becomes a coin toss at about 6–8 kyd and suicidal inside 5 kyd,** day or night. Radar removes the night's protection.
- **Against WWI fire control, an evasive attack to 6 kyd costs about 10–40%.** That matches Jutland: German boats launched at about 7–8 kyd during Scheer's turn-away, losing 1 of 13 sunk and 4 badly damaged [S].
- **Without radar, night protects completely until the attacker is illuminated.** Surigao: US destroyers launched at 6.5–10.7 kyd with almost no losses [S]. Cape Bon: surprise from inside 1,000 m [S].
- The 1916 RN torpedo handbook's rule, not to approach inside 7,000 yd against effective fire [S], is exactly the model's coin-toss range.

---

## 6. Battleship and cruiser verdicts, condensed

| Range (heavy guns) | Against WWII fire control | Against post-war (model-based) fire control |
|---|---|---|
| < 6 kyd | Fight steady (radar); optical shooters still lose ~10–30% to chasing | Fight steady |
| 9–20 kyd | ±30° every 1–2 min or chase salvos: −35–55% enemy hits (optical), −15–55% (radar); costs −10–27% of own hits | Barely helps (0–15%). Fight |
| 25–30 kyd | Any style −40–55%; chasing or ±45°/60 s best | ±45°/60 s about −50%; free for a digital ship |
| > 30 kyd | Dodging alone works; hit rates become tiny anyway | Same |

---

## 7. MTBs and fast attack craft

Model table 7: MTB (Elco 80 ft, 40 kn), daylight, combat. Hit % per round:

| Shooter | km | Steady | Weave ±40°/30 s | Random + speed | Flash + speed | Chase |
|---|---|---|---|---|---|---|
| 40 mm quad, Mk 51 + tracers | 1 | 7.4 | 6.1 | 12.1 | 16.1 | 12.2 |
| | 2 | 3.9 | 3.0 | 3.3 | 4.9 | 4.1 |
| | 3 | 1.6 | 1.2 | 1.6 | 1.6 | 1.6 |
| 4 × 5″/38, Mk 37 + radar | 1 | 34.3 | 8.4 | 25.1 | 21.6 | 20.2 |
| | 2 | 23.5 | 4.7 | 10.9 | 10.5 | 8.8 |
| | 3 | 12.3 | 3.5 | 5.4 | 4.3 | 3.8 |
| | 5 | 3.4 | 1.7 | 0.7 | 1.3 | 1.0 |
| 76 mm OTO, digital | 1 | 36.6 | 30.4 | 38.0 | 45.4 | 37.5 |
| | 2 | 27.9 | 18.7 | 21.8 | 22.9 | 19.0 |
| | 3 | 16.3 | 8.6 | 10.2 | 11.6 | 8.8 |
| | 5 | 5.2 | 0.9 | 2.3 | 2.1 | 1.4 |

(The 40 mm battery's effective range is set to 4.5 km.)

**Small-craft verdict:**
- **Weave continuously at full speed.** A ±40° weave on a 30 s period cuts radar-directed 5-inch hits 3–5× at 1–3 km. That is the best single defence in the model.
- **Never slow down.** Speed jinks that drop to 40% speed *raise* 40 mm hits at 1 km (16% against 7%). The boat's speed is its armour; deceleration hands the gunner a slow, predictable target.
- **Light automatic guns are hard to evade but miss a lot anyway.** Tracers close the loop every second and flight times are 2–4 s, so evasion barely matters to them. Their hit rate is about 4–7% per round at 1–2 km.
- **The digital 76 mm is the hardest to fool.** Weaving still halves its hits at 3 km and cuts them 5× at 5 km.

### 7.1 MTB torpedo attack on a destroyer

Model table 9. Setup:
- Attacker at 40 kn; must hold 10 s steady to fire.
- Defence: 5 × 5″/38 (Mk 37 + radar), 4 × 40 mm (Mk 51 with tracers, 3.5 km), 6 × 20 mm (1.8 km).
- Per-hit stopping chances [INFERRED; wooden hulls pass shells unfuzed, petrol fires]: 5″ 0.5, 40 mm 0.12, 20 mm 0.04.
- At night, eyes and optics have ×2.5 range error and ×2 bearing error, and tracer spotting is ×3 noisier [INFERRED].

P(stopped before launch):

| Condition | Launch at | Steady | Weave | Random + speed | Flash + speed |
|---|---|---|---|---|---|
| **Day, seen at 8 km** | 4 km | 0.78 | 0.20 | 0.40 | 0.65 |
| | 3 km | 0.90 | 0.30 | 0.65 | 0.88 |
| | 2 km | 1.00 | 0.35 | 1.00 | 0.97 |
| | 1 km | 1.00 | 0.90 | 1.00 | 1.00 |
| **Night, radar-directed 5″; seen by eye at 2 km** | 4 km | 0.70 | 0.33 | 0.42 | 0.53 |
| | 3 km | 0.93 | 0.45 | 0.68 | 0.80 |
| | 2 km | 1.00 | 0.62 | 0.93 | 0.97 |
| | 1 km | 1.00 | 0.93 | 1.00 | 1.00 |
| **Night, no radar; seen at 2 km** (starshell, wake) | 4 / 3 / 2 km | 0.00 | 0.00 | 0.00 | 0.00 |
| | 1 km | 0.97 | 0.45 | 0.95 | 0.90 |
| | 500 m | 1.00 | 0.65 | 1.00 | 1.00 |
| **Night, no radar; quiet approach, seen at 1.2 km** | 1 km | 0.17 | 0.23 | 0.72 | 0.20 |
| | 500 m | 0.95 | 0.72 | 1.00 | 0.93 |

**Where it becomes fully suicidal:**
- **By day against an alert radar destroyer, every MTB attack is suicidal.** Even weaving launchers are stopped 20% of the time at 4 km and 90% at 1 km, and a 4 km torpedo shot at a manoeuvring destroyer rarely hits. Historically MTBs and PT boats did not attack warships by day.
- **At night against radar-directed guns:**
  - **Weaving launchers:** survival is about two-thirds at 4 km, half at 3 km, a third at 2 km. Hits from inside 2 km cost the boat almost certainly.
- **At night against a non-radar ship:**
  - **What protects the boat** is not being seen. Launching from outside the illumination range is safe.
  - **Inside 1 km it becomes suicidal** unless the boat weaves (about 0.45) or got there unseen (a quiet approach: about 0.2).
  - **Surigao** fits this pattern: PTs launched at 400–4,000 yd against searchlight- and starshell-equipped Japanese ships; 10 of 30 boats under fire were hit and 1 was lost [S].
  - **The model is harsher than Surigao at the shortest ranges.** Japanese light AA lacked lead-computing sights; give such defenders a weaker light-gun level [UNCERTAIN].
- **Missile-era note.** Post-1960 FACs fight with missiles from 20+ km, outside gun range. The gun duel matters for interceptors (Boghammar-type) and for finishing cripples. At Latakia, 76 mm guns only finished off cripples; at Praying Mantis, missiles and aircraft did the killing [S]. The 76 mm row in table 7 is the reference for close-in boat swarms.

---

## 8. Salvo chasing, examined

**History** [S, evasion appendix §1]:
- Samar: Sprague ordered his escort carriers to chase salvos. Hoel was "fish-tailing and chasing salvos". Johnston "zigzagged between the splashes".
- Komandorski: Salt Lake City "chased the salvos" for 3.5 h.
- River Plate: Graf Spee's 130–150° turns under smoke threw out British fire.
- The folk logic: the spotter has just corrected away from the last splash, so steer there.

Model table 10: cruiser against cruiser, 16 kyd, 8″, 25 min. Rows set how often the target steers for the splash; otherwise it gambles left / hold / right on each splash. Hit % per shell (steady target, optical track: 5.5):

| p(chase) | Optical track | Optical + aspect, fire on splash | Optical anticipate | Radar track | Radar anticipate |
|---|---|---|---|---|---|
| 0.00 | 2.9 | 3.6 | 3.6 | 2.9 | 4.9 |
| 0.50 | 2.5 | 4.6 | 4.1 | 3.0 | 4.8 |
| 0.75 | 2.2 | 4.4 | 4.0 | 3.2 | 4.8 |
| 1.00 | 2.4 | 3.5 | 4.0 | 3.2 | 4.2 |

**What this shows:**
- **Any big course change triggered by each splash halves hits against a tracking WWII shooter** (5.5 → 2.2–2.9).
- **Steering *toward* the splash is not what does it.** A random left/hold/right gamble on the same trigger is about as good. This supports Naval Gazing's argument that chasing works because it breaks the steady-course assumption at the worst moment for the computer: just after the shooter's newest data point [S]. A commenter's point applies too: humans are bad at random, so a mechanical rule is a practical way to be unpredictable.
- **The shooter's counter is to see the turn, not to guess its direction.**
  - Reading the target's heading from her silhouette and holding fire until the last splash lands recovers roughly a third to three-quarters of the lost hits per shell.
  - Learning the habit and leading it adds little beyond that.
  - **Fire-on-the-splash costs rate of fire** (table 13): it pays at 12–20 kyd against evaders but costs hits at 28 kyd, where the time of flight exceeds the salvo interval.

Model table 13: hits per 10 min (and % per shell), battleship duel:

| Shooter | kyd | Steady | Flash | Chase | Weave |
|---|---|---|---|---|---|
| WWII radar, track | 12 | 31.0 (18.5) | 27.0 (16.1) | 22.7 (13.5) | 25.8 (15.4) |
| | 20 | 13.0 (8.0) | 9.5 (5.9) | 5.9 (3.6) | 10.3 (6.3) |
| | 28 | 5.9 (3.7) | 3.8 (2.3) | 2.7 (1.7) | 4.3 (2.6) |
| WWII radar, anticipate | 12 | 31.3 (18.6) | 31.1 (18.5) | 30.4 (18.1) | 32.9 (19.6) |
| | 20 | 10.0 (6.9) | 9.6 (6.5) | 9.7 (6.5) | 9.8 (6.7) |
| | 28 | 3.1 (3.2) | 3.2 (3.3) | 2.6 (2.6) | 2.7 (2.8) |
| Digital, track | 12 | 60.1 (35.8) | 42.6 (25.3) | 38.0 (22.6) | 39.1 (23.3) |
| | 20 | 24.2 (14.9) | 13.0 (8.0) | 9.9 (6.1) | 11.4 (7.0) |
| | 28 | 14.3 (8.8) | 4.4 (2.7) | 3.3 (2.0) | 2.5 (1.5) |
| Digital, anticipate | 12 | 51.4 (30.6) | 52.0 (31.0) | 55.8 (33.2) | 49.0 (29.2) |
| | 20 | 18.7 (13.0) | 16.5 (11.3) | 16.4 (11.2) | 17.1 (11.8) |
| | 28 | 8.6 (8.9) | 5.2 (5.4) | 4.8 (5.0) | 5.4 (5.6) |

**Game reading.** Against a WWII shooter at 20 kyd, chasing turns 13 hits per 10 min into 6. A gunnery officer who switches to reading aspect and firing on the splash gets back to about 10, but loses 3 against a steady target. That is a real decision with no dominant answer: exactly the kind the game wants.

---

## 9. Predictive systems: what a shooter can do (and what it can't)

### 9.1 Doctrines in the code

| Doctrine | What it does | Era / who | Needs |
|---|---|---|---|
| `track` | Aims where the computer's tracker says (straight-line for WWII machines, turn-following for digital) | All | — |
| `aspect` | Re-aims along the heading the lookouts can see (target angle), assuming she holds it | WWII on; human skill | Target visible. Aspect error is 10° 1σ [INFERRED] |
| `anticipate` | `aspect` + holds each salvo until the last splash lands + learns the target's reaction to splashes (chase / away / hold) and leads the learned habit | Skilled WWII officer; digital | Splashes seen; costs rate when TOF > interval |
| `rocking` | Rocking ladder: alternate range offsets +/0/− [S doctrine] | USN WWII | — |
| `spread` | Deliberately widen the pattern to about 0.4 × reach | Any | — |
| `decay` | Turn-following, but expects the turn to decay (τ = 30 s) | Digital | Turn-rate estimate |
| `centroid` / `cover` / `split` | Monte-Carlo the target's possible futures (its dynamics + learned habits); aim at the mean / widen to cover / split guns into 3 aim points | Post-war digital (plausible 1980s+) | Target-class dynamics model |

### 9.2 What they buy

Model table 14: battleship duel at 20 kyd, hit % per shell:

| Shooter | Steady | Zigzag | Weave | Flash | Chase |
|---|---|---|---|---|---|
| WWII radar, track | 8.0 | 4.9 | 6.3 | 5.9 | 3.6 |
| WWII radar, rocking ladder | 7.4 | 4.8 | 5.9 | 5.5 | 4.1 |
| WWII radar, deliberate spread | 7.0 | 4.5 | 6.6 | 5.4 | 4.2 |
| Digital, track | 14.9 | 10.5 | 7.0 | 8.0 | 6.1 |
| Digital, decaying turn | 16.1 | 10.8 | 7.0 | 8.0 | 6.6 |
| Digital, centroid of futures | 16.5 | 10.5 | 6.9 | 8.5 | 5.6 |
| Digital, split salvo (3 aims) | 13.2 | 7.8 | 5.9 | 6.2 | 5.9 |
| Digital, cover envelope | 16.5 | 10.5 | 6.9 | 8.5 | 5.6 |

In the code, `cover` only widens the pattern when the sampled futures spread wider than the gun's own dispersion. At 20 kyd they do not, so `cover` aims exactly like `centroid` and the two rows coincide.

Compare with `aspect` and `anticipate` in tables 4, 10 and 13.

**Why the clever-looking options fail, and what works:**
1. **The real unpredictability at these ranges is small.** At 20 kyd a battleship can only get about 50 m off a sensible prediction before the shell lands, which is far below the shooter's own salvo error of about 200–300 m. What hurts the shooter is a **wrong motion model**: straight-line trackers lag every turn, and turn-following filters over-extrapolate every jink. Widening the net (spread, rocking, split, cover) pays for a problem that isn't there and thins the pattern.
2. **The fix is a better model of the target's intent: "she turned, and now she will hold her new heading".** In daylight a WWII lookout delivers that by reading target angle. Digitally, a model-based predictor does it (`anticipate` gives 16.4–17.1 hits per 10 min against evaders at 20 kyd, where `track` gives 9.9–13.0).
3. **Splitting and covering only pay when the shooter's own error is smaller than the target's reach.** That means very long flight times with precise post-war guns, or small craft at medium range: compare the 76 mm "cover" row in table 7 with plain digital tracking, roughly a wash.
4. **Crossfire does not beat evasion** (table 11). Two ships 90° apart score about the same per shell as one. Turning displaces the target along her narrow axis whoever is watching.
   - Crossfire's real value is elsewhere: more guns, and each spotter sees the other's deflection as range.
   - It also forces a flash-dodger to react to two sets of flashes.

   Model table 11 (digital fire control, 16″, 25 kyd; hit % per shell):

   | | Steady | Zigzag | Weave | Random | Flash | Chase |
   |---|---|---|---|---|---|---|
   | One ship, 9 guns | 11.0 | 4.0 | 3.7 | 6.2 | 4.2 | 3.3 |
   | Two ships, 2 × 9 guns, 90° apart | 10.3 | 3.2 | 1.9 | 5.1 | 3.0 | 2.3 |

5. **The only universal counters are shorter flight time and less latency.** Close the range (at Samar the Japanese hit Gambier Bay only after closing to about 10 kyd [S]), use faster-firing and flatter guns, and cut the tracker's lag. These shrink both the dodge and the corruption.

### 9.3 Predictor ladder for the game (realistic per era)

| Tier | System | Counters | Exploitable by |
|---|---|---|---|
| P0 | Eye / Dumaresq + clock (1890–1910) | Nothing | Any course change |
| P1 | Straight-line rangekeeper (Dreyer → Mk 8) | Steady targets, circles slowly | Zigzag, chasing, any turn (corruption 1–3 min) |
| P2 | + aspect reading (crew skill, daylight, < ~20 kyd) | Zigzag, chasing | Night, smoke, haze, long range (aspect unreadable) |
| P3 | + fire on the splash + habit learning (skilled officer) | Chasing habits, flash-dodge timing | Randomised habits; costs rate at long range |
| P4 | Turn-following filter (1950s–70s analog/digital) | Circles, steady turns | **Weaving and short jinks** (over-extrapolation) |
| P5 | Model-based intent predictor (digital + visual/IR aspect, 1980s+) | Most styles | Pure long-range dodging only (reach > window); unpredictable random gambles |
| P6 | Closed-loop spotting (radar sees own rounds; CIWS-style) | Bias, corruption | Only TOF dodging, i.e. long range or small agile craft |

---

## 10. Game design notes

**Expose the contest, not just the numbers:**
- **Incoming-salvo indicator** with a time-to-impact clock. The player (or AI captain) sees the flash and has TOF minus reaction time to act. Show the ship's **reach** for that time as a ring around the predicted point. This teaches range dependence instantly: tiny at 12 kyd, large at 28.
- **Splash markers** with fade, so chasing is a visible player tactic.
- **Own fire-control quality meter** that drops while the player's ship turns (table 12). This is what makes WWII evasion a decision rather than a free button.
- **Enemy habit readout** for a skilled gunnery officer, e.g. "target chases splashes, 70%". Let AI captains have a habit trait with randomness, and let opposing crews learn it over a few salvos (`Learner` in the code needs about 3–6 observed salvos).

**AI captain behaviour, by class:**

| Class | Default under fire | Switch to |
|---|---|---|
| Battleship / cruiser (WWII) | Steady inside 10 kyd. ±30°/2 min zigzag or chase salvos beyond 15 kyd if outgunned | Steady again when its own hits matter more (finishing a cripple) |
| Battleship / cruiser (post-war) | Always evade (free); ±45°/60 s beyond 20 kyd; random gambles | — |
| Destroyer in a torpedo attack | Weave or chase salvos on the run-in. Steady only for the last 30 s. Turn away hard after launch | Abort if the defence is radar-directed and launch range < 6 kyd by day |
| MTB / FAC | Continuous full-speed weave (±40°, 30 s). Never slow. Approach unseen at night | Abort if illuminated inside 2 km against a radar ship |

**Crew and doctrine traits for shooters** (the predictive systems):
- `aspect_skill` (lookout quality): its error grows with range, haze and night; unavailable beyond visual range.
- `fire_on_splash` toggle: a player decision; costs rate when TOF > interval.
- `habit_learning` (officer experience).
- Computer type: straight / turn-following / model-based, per era.
- `closing range` is a captain decision.

**Fairness and determinism.** The shooter never sees the target's future orders. Every predictor in the code uses only what was observable then: sensor readings, visible aspect, splashes and past behaviour. That keeps the contest honest and the counters learnable.

**CPU.** A full duel (tracker, aim, Monte-Carlo samples for P5) costs about 0.1–0.3 s of Python per 15 simulated minutes per gun battery, and much less in a compiled engine. The quick tier can use table 2 (reach) and the multipliers in tables 4–6 directly.

---

## 11. Gaps and uncertainties

- **No sourced numbers** on how accurately WWII lookouts read target angle. The model's 10° is [INFERRED]; it drives the `aspect` and `anticipate` gains.
- **No operations-research study of evasion against surface gunfire was found** (zigzag plans were anti-submarine). The best formal result is Isaacs' RAND game on aiming and evasion with a projectile time lag [S]: the evader's optimal play is random; the marksman has only near-optimal strategies. The model's results agree qualitatively.
- **Turning data** for Bismarck, KGV, Hood, Myoko, Fubuki, S-boats, Osa and Boghammar was not found. Those hulls use length-ratio analogues.
- **Per-hit stopping probabilities** (5″ vs DD 0.06, 40 mm vs MTB 0.12, and so on) are inferred from the damage notes. Attack-run thresholds scale with them.
- **The MTB night case is harsher than Surigao at 1 km.** Defender light-gun quality is the likely difference [UNCERTAIN].
- **Not modelled:** formations (ships could not all zigzag freely); smoke as a tool (only as visibility); torpedo combing; the target's own gunfire suppressing the shooter; heel affecting the evader's gunnery.

---

## Sources (main)

**Manoeuvring** (full list in the manoeuvring appendix):
- Iowa tactical diameter: https://en.wikipedia.org/wiki/Iowa-class_battleship
- USNI 1988, Handling a battleship: https://www.usni.org/magazines/proceedings/1988/april/handling-battleship
- USNI 1953, Yamato and Musashi: https://www.usni.org/magazines/proceedings/1953/october/design-and-construction-yamato-and-musashi
- USNI 1925, Speed loss in turning: https://www.usni.org/magazines/proceedings/1925/september/retardation-ships-speed-due-turning
- USNI 1987, Handling Ticonderoga: https://www.usni.org/magazines/proceedings/1987/january/handling-ticonderoga
- Fletcher class: https://destroyerhistory.org/fletcherclass/index.asp?pid=200
- Sumner and Gearing classes: https://destroyerhistory.org/sumner-gearingclass/index.asp?r=0&pid=10
- PT-487: https://www.navsource.net/archives/12/05487.htm
- Partial-rudder trawler trials: https://www.transnav.eu/html,205.html

**Evasion history and doctrine** (full list in the evasion appendix):
- Battle off Samar: https://en.wikipedia.org/wiki/Battle_off_Samar
- Samar destroyer actions: https://destroyerhistory.org/actions/index.asp?pid=4583
- Battle Experience, Leyte 78.3: https://ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.3.html
- Salt Lake City at Komandorski: https://ussslcca25.com/komador3.htm
- H-gram 016: https://history.navy.mil/about-us/leadership/director/directors-corner/h-grams/h-gram-016/h-016-1.html
- Harwood's River Plate despatch: https://www.fepow.family/Supplement/London_Gazette/River_Plate_Battle/html/part_ii.htm
- Naval Gazing, Spotting: https://navalgazing.net/Spotting
- Destroyers at Jutland (BJMH): https://bjmh.gold.ac.uk/index.php/bjmh/article/download/757/879/980
- Naval Gazing, Jutland Part 4: https://www.navalgazing.net/Jutland-Part-4
- PT boats at Surigao: https://ibiblio.org/hyperwar/USN/CloseQuarters/PT-8.html
- Gulf of Tonkin: https://www.americanheritage.com/what-happened-gulf-tonkin
- NavPers Ch. 18-C (rocking ladder, barrage zones): https://eugeneleeslover.com/USNAVY/CHAPTER-18-C.html
- Isaacs, RAND P-642, The Problem of Aiming and Evasion: https://apps.dtic.mil/sti/pdfs/AD0604643.pdf
- DTIC ADA023015, tracking manoeuvring targets: https://apps.dtic.mil/sti/pdfs/ADA023015.pdf
- Java Sea: https://pacificwrecks.com/battle/battle-of-the-java-sea.html
