# 02 — Fire Control: the COMPUTING stage (ranges/bearings to gun orders), 1900–1990
Status: final    Updated: 2026-10-09    Request: -

Scope: how measured range and bearing become gun orders (elevation, train, fuze), what a human can do without help, and what each computer generation adds. Tags: **[INFERRED]** means my own derivation; **[UNCERTAIN]** means sources are thin or conflict. Sources are given inline.

**Source caveat.** Several primary sources would not load: the ibiblio NavPers 16116 chapters, the dreadnoughtproject Argo/AFCT pages, combinedfleet b_fire, the warhistory French page, and the range-table scans. Japanese, German, Italian, French and Soviet *computer* specifications (target-speed limits, solution times) were not found in anything accessible. Where a number below is a model parameter rather than a sourced figure, it is tagged.

---

## 0. Model in one paragraph (for implementation)

A gun order is: **present range + bearing**, advanced by **relative motion × time of flight (TOF)**, plus **ballistic corrections**, plus **spots**. Two kinds of error matter:

- A **rate error** (wrong target course or speed) causes two problems. It gives an immediate miss of about `rate error × TOF`. It also makes the generated range drift away from the true range at `rate error × elapsed time` until a spot or a fresh range corrects it.
- A **ballistic/bias error** (wind, density, muzzle velocity) is a constant offset. Spotting removes it after 2–4 salvos.

Each computer generation mainly changes four things:
1. how fast and how accurately the rate is found;
2. whether the rate survives own-ship maneuvers;
3. whether the rate tracks target maneuvers or the geometry change of a crossing target (a constant range rate is wrong over time);
4. how many corrections are applied automatically.

---

## 1. The problem and correction magnitudes

### 1.1 Kinematic constants (NavPers / Naval Ordnance & Gunnery)
- 1 kn = 0.563 yd/s = 33.8 yd/min. A 30 kn ship moves 16.89 yd/s. Bearing rate (arcmin/s) = 1936 × speed-across (kn) / range (yd). 1 mil = 3.438 arcmin. (https://eugeneleeslover.com/USNAVY/CHAPTER-19-D.html)
- Deflection lead = 3438 × speed-across (yd/s) × TOF / range, in arcmin. (same)
- Range-table columns 15 and 18 give the range or deflection change for 10 kn of target motion, which equals 10 kn × TOF. Example: 124 yd per 10 kn at 10,000 yd for a 5"/38. (https://eugeneleeslover.com/USNAVY/CHAPTER-17-D.html)

### 1.2 Time of flight (anchors the whole rate-error budget)

16"/50 Mk 7, 2,700 lb AP, 2,500 ft/s (https://www.navweaps.com/Weapons/WNUS_16-50_mk7.php):

| Range (yd) | Elevation | TOF (s) | Angle of fall |
|---|---|---|---|
| 10,000 | 5.05° | 13.2 | 5.0° |
| 15,000 | 8.16° | 21.0 | 9.8° |
| 20,000 | 11.77° | 29.6 | 14.9° |
| 25,000 | 16.03° | 39.3 | 21.1° |
| 30,000 | 21.11° | 50.3 | 28.3° |

Other TOF anchors:
- German 38 cm SK C/34: 32.0 s to 20 km. (https://navweaps.com/Weapons/WNGER_15-52_skc34.htm)
- Japanese 46 cm: 26 s to 16.8 km and 98.6 s to 42 km. (https://en.wikipedia.org/wiki/46_cm/45_Type_94_naval_gun)
- Iowa at maximum range: about 95 s. (https://en.wikipedia.org/wiki/Mathematical_discussion_of_rangekeeping)

### 1.3 Corrections at ~20 kyd for a 15"/16" gun

| Correction | Magnitude (sourced; scaled to 20 kyd where possible) | Source / note |
|---|---|---|
| Target motion, 1 kn along LOS | **≈17 yd** (0.563 × 29.6 s) | [INFERRED] from TOF |
| Target motion, 1 kn across LOS | ≈17 yd ≈ **0.8 mil** | [INFERRED] |
| Range wind | 10 kn → ~200 yd (16"; range unstated, probably long) | https://www.navalgazing.net/Ballistics |
| Cross wind | 10 kn → ~126 yd off line | same |
| Ballistic wind error (5"/54 at 25 kyd) | 1 kn error ≈ 125 yd | https://man.fas.org/dod-101/navy/docs/swos/gunno/INFO9.html |
| Air density | 1% → ~135 yd (5"/54 at 25 kyd); ~61 yd per 1% for a 16" coast gun at 15 kyd | INFO9; https://www.armygroundforces.org/PDF/Coast%20Artillery%20Firing%20Tables/16inch%20MarkII%202100%20June%201942.pdf |
| Air density, climate extremes | Hot tropical day vs Atlantic winter (5"/38, 15 kyd): +300 yd vs −360 yd | navalgazing Ballistics |
| Powder temperature | 1 °F ≈ 2 ft/s, which moves a 16" impact ~55 yd at 40 kyd; [INFERRED] ~25–35 yd/°F at 20 kyd | navalgazing Ballistics |
| Muzzle velocity, gun wear | New 2,500 → average worn 2,425 ft/s on the 16"/50, costing 2,160 yd of max range; ~11 yd per ft/s for a 16" coast gun at 15 kyd. [INFERRED] a 75 ft/s loss is ~600–800 yd at 20 kyd | navalgazing; coast table |
| Bore erosion example | 0.08" enlargement → 34 ft/s loss (5"/38) | CHAPTER-17-D |
| Drift (spin) | 16": 949 yd at 36 kyd, up to 1,850 yd at max range. Coast 16": 12 mils at 15 kyd. [INFERRED] ~10–15 mils (~200–300 yd) at 20 kyd | navalgazing; coast table |
| Earth rotation (Coriolis) | Coast 16", ~15 kyd: 1.1 mil deflection and +47 yd range (42°N). Corrected in USN practice only from the mid-1930s, mainly for 8" and larger | coast table; https://www.eugeneleeslover.com/USN-GUNS-AND-RANGE-TABLES/CORIOLIS-FORCE.php |
| Earth curvature | 84 ft drop at 19,800 yd. [INFERRED] ≈100 yd range correction at a 15° angle of fall | https://www.mathscinotes.com/2017/12/earths-curvature-and-battleship-gunnery/ |
| Parallax (director to turret) | Iowa: uncorrected parallax gives a ~400 ft pattern. Cruiser with director ~140 ft from turret and 56 ft higher: >100 ft miss. Computed for a 100 yd base | https://www.navalgazing.net/Fire-Control-Part-2 ; https://okieboat.com/Gun%20plot.html |
| Trunnion tilt / roll | Large deflection error toward the tilt, plus a small range error that almost always shortens range. Mk 1A crosslevel cutout at 17.5° | CHAPTER-19-C; CHAPTER-25-C |
| Gun alignment | Guns aligned within 0.5°; 0.25° divergence at 30° elevation | https://navweaps.com/index_inro/INRO_BB-Gunnery.php |
| 5"/38 worked example, 10 kyd | Net ballistic error ADD 368 yd (gross +452/−84) and 2.6 mils deflection | CHAPTER-17-D |

**Takeaway for the model [INFERRED].** At 20 kyd the uncorrected ballistic bias (density + muzzle velocity + wind + drift) is several hundred yards to 1,000+ yd. A pre-dreadnought crew without range-table corrections needs spotting to remove it. From ~1912 tables (Dreyer) and from the 1930s (US Mk 8) the bias is mostly computed out, and the **residual bias is ~100–400 yd**. Compare with US-measured opening errors: first-salvo MPI 1,252 yd over (New York, 1930–31), and LRBP 1932–33 average MPI error 313 yd at ~30 kyd. (INRO_BB-Gunnery)

---

## 2. Human and manual methods

### 2.1 Estimating target course and speed by eye
- Speed was judged from the bow wave and course from the bearing or inclination (RN cruiser practice). (https://www.iwm.org.uk/history/hms-belfasts-armament-how-to-fire-the-6-inch-guns)
- Inclinometer relation: apparent angle = L·cos(target angle)/R. (Mathematical discussion of rangekeeping, Wikipedia)
- Quantified estimation errors: US AA drone runs (aircraft, not ships) averaged **1–2 kn speed errors after cancellation**; absolute errors were nearly twice that. One run estimated 86 kn against an actual 131 kn and 163° against 145° course. (INRO_BB-Gunnery fn 54/58)
- **[UNCERTAIN / INFERRED] game defaults for surface targets by eye at 10–20 kyd:**
  - speed ±2–4 kn (1σ);
  - course ±10–20°, worse near bows-on or stern-on, where the inclination change is subtle.
- A 10° course error on a 20 kn target is a 3.5 kn vector error, i.e. ~60 yd at 30 s TOF plus ~120 yd/min of drift in generated range. [INFERRED]

### 2.2 Rangefinder input quality
- About 4 ranges per minute per rangefinder, scatter "several hundred yards" at battle range. (https://www.navalgazing.net/Rangekeeping-Part-1)
- Ranges were transmitted to the Dreyer table in 25 yd steps. Range cuts at Jutland were few and unreliable. (https://dreadnoughtproject.org/tfs/index.php/Dreyer_Fire_Control_Table)

### 2.3 Synthetic vs analytic methods
- **Synthetic (Dumaresq):** estimate course and speed, then check the estimate against observed ranges and spots. This beat the **analytic** method (rate taken from the trend of plotted ranges) in head-to-head tests. (Rangekeeping-Part-1)
- By 1919 the RN concluded that range rate could not reliably be taken from the range-plot trend alone. (dreadnoughtproject Dreyer)
- A manual plot at one point per minute needs about **2.3–4× the tracking time** of an automatic 2-second-sampled computer to reach equal accuracy. Same-time errors are ~8× larger. This figure comes from a 1961 submarine bearings-only study, but it is a useful scaling for manual vs automatic plotting. (https://apps.dtic.mil/sti/pdfs/AD0329725.pdf)
- **[INFERRED] rate convergence times:** a hand plot needs 2–5 min of ranges for a usable rate (±50–100 yd/min). A Dreyer-type plot with Dumaresq cross-check needs 1–3 min. A US Mk 8 with continuous range and bearing needs 30–90 s. A Mk 1A on a surface target has a 2–5 s time constant (see §3).

### 2.4 Rate control (USN procedure)
- Only one combination of course and speed makes both generated range rate and generated bearing rate match observation. (https://eugeneleeslover.com/USNAVY/CHAPTER-20-E.html)
- Changing speed mostly affects range rate when the target is nearly bows-on or stern-on, and mostly affects bearing rate when it is crossing.
- Worked example: generated 15 kn against observed 10 kn range rate; the operator moved target angle from 160° to 140°.
- The manual says there is no fixed rule for the size of a correction; skill comes from practice.
- Applying spots during rate corrections has the same effect as pyramiding. (https://eugeneleeslover.com/USNAVY/CHAPTER-18-C.html)

### 2.5 Spotting rules

| Rule | Numbers | Source |
|---|---|---|
| RN bracket (WWI) | 400 yd first correction; 200 yd back when crossed; then 100 yd, expecting a straddle. Rate correction ≈ half the range correction (e.g. "down 200, rate down 100 yd/min") | https://navalgazing.net/Spotting |
| RN cruiser WWII | First round 200 short, second 400 over, third 200 beyond the first; fire for effect on straddle | IWM Belfast |
| German ladder (WWI) | Successive turrets ~400 yd apart; 2 salvos in flight allowed after Jutland (RN) | https://www.jutland1916.com/tactics-and-technologies-4/range-finding-and-course-plotting-2 |
| German WWII (Bismarck) | 3 partial salvos ~2 s apart at different ranges. Typical sequence: inclination correct by salvo 2, over by 3, straddle by 4, then rapid fire | https://kbismarck.com/controltiri.html |
| USN ladder | Steps ≥ pattern size; reverse and halve after crossing; rocking ladder +100/0/−100 yd; after a straddle, deflection-only spots, plus one add-spot of a pattern size every 3–4 salvos | CHAPTER-18-C |
| USN spot units | Range in yd (50 yd detents on the Mk 37); deflection in mils (1 mil detents). A 600 ft target is 20 mils wide at 10 kyd. Visual range spotting at 15 kyd needs a spotter ≥120 ft high | CHAPTER-18-C; CHAPTER-25-A |
| Pattern size | 16"/50 nine-gun ≈1.5–1.9% of range; 15" RN ~200 yd and 12" ~400 yd at 12 kyd | INRO; navalgazing Spotting |
| Pyramiding | Occurs when salvo interval < TOF + spotting time. A TOF buzzer cues the spotter | CHAPTER-18-C |
| Radar spotting | Mk 8 mod 0 radar saw 16" splashes to ~20 kyd; mod 3 to ≥35 kyd; Mk 13 (1945) spots within 100 yd (50 yd with experience). Optical spotting deteriorates beyond ~18 kyd | INRO_BB-Gunnery |
| Air spot | 1935 NWC estimate: 6× the hits of spotters aloft at 29 kyd | INRO_BB-Gunnery |

**Typical time to first hit (USN practice).**
- Tennessee 1939–40, ~19 kyd: first hit on salvo 4, at 3 min 42 s.
- Nevada 1924–25, ~18 kyd: hit on salvo 5.

(INRO_BB-Gunnery)

**Salvo intervals.**

| Ship / class | Interval | Context |
|---|---|---|
| New York, 1930–31 | 66 s average (31 s loading, 10 s director) | Main battery |
| Idaho, 1942 sustained test | 84 s average | Main battery |
| 1929 main battery | 30–50 s | |
| 1929 secondary battery | 7–10 s | |
| Night practice | ~10 s | |
| 1938 minimum loading interval | 24 s | |

(INRO_BB-Gunnery)

### 2.6 What one gunnery officer can handle [INFERRED]
- One target at a time. Each additional plot or rate change costs attention.
- Dreyer-era transmitting stations needed 7+ table operators and up to ~30 people (Hood, Mk V; 27 per the Wikipedia fire-control article). (dreadnoughtproject Dreyer)
- Japanese Type 92 Shagekiban: 7-man team; Sokutekiban: 8 men. (https://navweaps.com/index_tech/tech-086.php)
- US Mk 8: operated by **one person**. (tech-086)

---

## 3. Computer generations

| System (date) | Automates | Limits / numbers | Own-ship turn? | Notes and sources |
|---|---|---|---|---|
| **Dumaresq** (1902; Mk I patent 1904) | Converts own and target course/speed into range rate (yd/min) and speed-across (kn → deflection) | Inputs are estimates. ~1,000 bought by 1913 | Mk IV kept relative enemy direction "within a few degrees" during own turns; helm-free Mk VI* (Dreyer, 1908) | No integration over time. https://en.wikipedia.org/wiki/Dumaresq |
| **Vickers range clock** (1905/06; 246 ordered 1906) | Integrates a constant range rate into range | Scales 2–6, 6–10, 10–14 kyd in 25 yd steps. **Constant rate only**; rubber roller slips when the rate is changed, so rate changes are quick discrete steps | No | https://en.wikipedia.org/wiki/Vickers_range_clock |
| **Dreyer FC Table** Mk I–V (1911–1922) | Range plot plus bearing plot (manual or typewriter), Dumaresq, range clock, deflection; electric follow-up from Mk IV | Range clock ±1,200 yd/min. Bearing gear ±15°/min (Mk II/III). Ranges 2–28/29 kyd. Granularity 25 yd and 25 yd/min; deflection in 1 kn steps. Bearing quantization 1/4° (later 4′). Crew 7–8 (5–30 total) | Mk III "helm-free" via gyro. Wartime improvements for own turns are unquantified | Weak bearing plot, wandering gyros, manual plotting. https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php ; dreadnoughtproject Dreyer |
| **Pollen Argo clock / Aim Corrector** (trials 1905–06; Mk IV ~1912) | True-course plotter (plan view of both ships from range + bearing); gyro yaw correction; variable-speed integrator that tracks *changing* range rate | Only ~6 in RN service at Jutland | Gyro-corrected, so effectively helm-free | Better on rapidly changing range rate than the Vickers clock. https://navalgazing.net/Rangekeeping-Part-2 ; jutland1916 ; https://en.wikipedia.org/wiki/Arthur_Pollen |
| **Ford Rangekeeper Mk 1** (eval 1916, Texas 1916/17) | Continuous range from target course/speed; bearing output added 1926 | Inputs: gyro course, manual own speed, manual range/bearing, estimated target course/speed. TOF approximated as linear in range | Own course from gyro | https://www.usni.org/magazines/naval-history-magazine/2015/october/revolutionary-rangekeeper ; Rangekeeper (Wikipedia) |
| **Ford Mk 8 Rangekeeper** (Portland class ~1932/33; all WWII USN BB/CA/CL) | Rangekeeper + bearing keeper + predictor + deck-tilt and trunnion-tilt correctors + graphic plotter. Wind, drift, IV loss; TOF buzzer; generated range fed back to the radar (auto present-range) | One operator. Ballistics on a single flat cam. 1980s Iowas added 2 HP digital computers (earth curvature/rotation, MV via DR-810 velocimeters) | **Yes**: continuous own course/speed. North Carolina 1945 held a solution through 450° and 100° turns with MPI shifts of "several hundred yards". Ponape 1944: 15 min regenerated blind, off by **100 yd and 1 mil** | https://www.navalgazing.net/Fire-Control-Part-2 ; INRO_BB-Gunnery ; Rangekeeper (Wikipedia) |
| **Mk 1 / Mk 1A computer** (Mk 37 GFCS; Mk 1 1935, Mk 1A WWII) | Dual-purpose (air + surface); automatic rate control from director tracking; fuze, parallax, roll/pitch via Stable Element | Target speed counter 0–800 kn (stops 300 → 600 → 1,200 kn). **Auto rate control for surface targets ≥15 kn only; manual below.** Surface rate-control range fixed at 3,000 yd, so time constant Tc = 2 s × R/3,000 (≈5.3 s at 8 kyd; rate error falls to 37% per Tc). IV correction 2,350–2,600 ft/s. ~3,000 lb | Yes | Mk 1A computed faster than the Mk 8. Mk 37 probable error <100 yd at >30 kyd claimed. Mk 8 Mod 2 *computer* (1944, not the rangekeeper) beat the Mk 1 on speed. https://eugeneleeslover.com/USNAVY/CHAPTER-25-C.html ; CHAPTER-25-A ; https://en.wikipedia.org/wiki/Mark_I_Fire_Control_Computer ; https://historyrise.com/article/the-evolution-of-naval-gunfire-control-systems-in-wwii-battleships/ [claim UNCERTAIN] |
| **AFCT** Mk I (Nelson/Rodney) → VII (QE refits, Renown) → IX (KGV) → X (Vanguard) | Integrated clock + plot (MPI plotting); AFCC is the version without the plot (destroyers, secondaries) | AFCC Mk III on HMCS Haida: range limit **16,000 yd** (4" guns). Manual inputs: wind, enemy course/speed, air temperature | Helm-free inputs. Friedman (via forum): the only big-gun computer that could handle a target in a *constant* turn [UNCERTAIN] | https://en.wikipedia.org/wiki/Admiralty_Fire_Control_Table ; https://www.jproc.ca/haida/transmitting_station.html ; https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=884024 |
| **Japan: Type 92 Shagekiban** (1932) + **Sokutekiban** (course/speed meter) + **Type 94/98 Hoiban** (director) | Shagekiban computes gun orders. Range rate from a range-rate plot; target speed the only target-motion input; deflection set by hand (own speed, target speed, wind, drift). Sokutekiban derives speed from (R+ΔR)·sinΔB | Shagekiban 7 men; Sokutekiban 8 men (train ~300° on Kongo/Haruna). Yamato still needed 7 operators. Gyros "neither accurate nor reliable"; **no gyro horizon reference**; no RPC on Yamato | Poor [UNCERTAIN]: plot-based rates must be rebuilt after maneuvers | Directors and rangefinders separate; slow switching between directors. Exercise patterns >1,000 m. https://navweaps.com/index_tech/tech-086.php ; https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=847968 ; https://weaponsandwarfare.com/?p=22992 ; Ship gun FC (Wikipedia) |
| **Germany** (Rechenstellen fore and aft under armour; C/38K-type computers [name UNCERTAIN]) | Stereoscopic 10.5 m rangefinders feeding the computing room; WWI "EU-Anzeiger" with C12 range-rate clock | Main turrets had RPC in elevation only, not train (Bismarck elevation RPC "unsatisfactory"). Turrets 5°/s train, 6°/s elevation | Unknown. SL-4 AA directors (1936) could not stabilize yaw in sharp turns | No surface computer specs found. kbismarck controltiri; navweaps 38 cm; https://en.wikipedia.org/wiki/Stabilisierter_Leitstand ; jutland1916 |
| **Italy** (San Giorgio automatic central, Littorio) | Director + computing + inclinometers + follow-the-pointer; "scartometry" (measured splash offsets fed back as corrections) | Two 7 m rangefinders; 45 s loading cycle; 381 mm dispersion ~1.5–2× British | Unknown | https://trentoincina.it/mostrapost.php?id=257 ; https://www.sociostudies.org/almanac/articles/the_british-italian_performance_in_the_mediterranean_from_the_artillery_perspective/ |
| **France** | not retrieved | — | — | [UNCERTAIN; no source accessed] |

**Systematic weaknesses to model:**
- **Constant-rate integrators** (Vickers clock, early Dreyer) drift on any crossing target, because the true range rate changes continuously as bearing changes. Pollen's variable integrator and the US Mk 1/8 compute range rate from the resolved geometry, so they do not drift this way. [INFERRED from Rangekeeping-Part-2]
- **Dumaresq with fixed speed settings** could not follow rapid speed changes. (jutland1916)
- **Japanese system:** human-plotted rates and hand-set deflection mean **re-convergence after any maneuver takes minutes**. [INFERRED]

---

## 4. Maneuvering: own ship and target

**Measured examples (USN practice).**

| Case | Result | Source |
|---|---|---|
| 1927–28, 20–30° course changes at ~25 kyd | MPI error ~400 yd; **no salvo hit** | INRO_BB-Gunnery |
| California 1941, 150° countermarch at 23 kyd | 212 yd mean deflection error, plus one wild salvo 4,100 yd off | INRO_BB-Gunnery |
| Firing during turns generally | Salvos off in deflection while correct in range, or vice versa | INRO_BB-Gunnery |
| North Carolina 1945 (Mk 8 + stable vertical) | Through 450° and 100° turns the MPI moved "several hundred yards", judged acceptable | INRO_BB-Gunnery ; https://en.wikipedia.org/wiki/Rangekeeper |

**Target maneuver.** A target that changes course during a shell's flight is not handled by any analog rangekeeper. Regenerated orders go stale as soon as the target turns. (CHAPTER-19-C)

**Chasing salvos (Komandorski, 1943).** Salt Lake City zigzagged and steered for the last splashes. She survived ~200 salvos falling within 50 yd and took about 4–5 hits in 3.5 h. (https://ibiblio.org/hyperwar/USN/Aleutians/USN-CN-Aleutians-9.html)

**Course-change speed loss.** Thorsten Wahl notes that course changes cost speed and that evasion works best for ships quick on the helm. (tapatalk p=880860)

**[INFERRED] re-convergence model after a target turn of Δψ at speed V.** The new velocity error is 2V·sin(Δψ/2): about 10 kn for a 30° turn at 20 kn. Time to restore the solution:

| Generation | Re-convergence | Basis |
|---|---|---|
| Dreyer / Japanese plot | 2–4 min | New rate from plot + spots; ~1 salvo cycle of 40–60 s to detect, then 2–3 spots |
| AFCT / Mk 8 with continuous optical tracking | 1–2 min | Bearing-rate mismatch visible in ~15–30 s; course correction; one confirming spot |
| Mk 8 / Mk 1A with radar range auto-feed | 30–60 s | |
| Mk 1A auto rate control | ~10–20 s | Several time constants |
| Digital Mk 86 / WM-25 | a few seconds after the TWS track updates | [UNCERTAIN] |

**Implication.** A target that turns at intervals shorter than TOF + re-convergence time is effectively unhittable except by pattern luck. For WWII heavy guns at 20 kyd that is ~1.5–2 min with a Mk 8, and longer with plot-based systems. [INFERRED]

---

## 5. Gun laying

### 5.1 Director vs local control
- Director firing gives a tight salvo from one key, with crews away from smoke. Most RN capital ships had directors by mid-1916. (https://www.navalgazing.net/Fire-Control-Part-1 ; https://en.wikipedia.org/wiki/Fire-control_system)
- In local control the turret rangefinders and a turret computer take over:
  - Iowa turret: 46 ft rangefinder plus a Mk 3 computer without trunnion-tilt correction.
  - Bismarck turrets: 10.5 m rangefinders.

  (Fire-Control-Part-2 ; kbismarck)
- **[INFERRED] game penalty for local control:** about 2–4× the director-control MPI error and slower rates.

### 5.2 Stabilization and gyro firing
- **Continuous aim** (Percy Scott, 1898–99): practical only up to ~9.2"; stabilized level only. Cross-level was not stabilized until the 1930s. (Fire-Control-Part-1)
- **USN Stable Vertical Mk 41 / Stable Element Mk 6:**
  - Continuous level and cross-level in normal seas.
  - In heavy seas, a "selected level" mode fires automatically when the ship passes a set roll angle (e.g. 0° or 5°), with a firing-delay compensator.
  - Spin-up 30–60 min; settles to the vertical in ≤5 min (Stable Element ≤1 min).

  (https://eugeneleeslover.com/USNAVY/CHAPTER-20-E.html ; https://www.navweaps.com/index_tech/tech-074.php)
- **Japan:** no gyro horizon reference in its systems. (Ship gun FC, Wikipedia)

### 5.3 RPC and follow-the-pointer
- **USN RPC:** synchro-driven servos. Coarse synchros bring the mount within a couple of degrees, then fine 36:1 synchros take over (10× gearing, 36° per turn). (https://navweaps.com/index_tech/tech-013.php)
- A dial-turn error gave a 1,664 yd miss when a turret was 5° out of train (Maryland). (INRO)
- When receiver regulators were missing, turrets fell back to follow-the-pointer (Massachusetts at North Africa). (tech-086)
- **Other navies:**
  - RN RPC: experimental on Champion 1928; on 40 mm mounts from 1941.
  - Bismarck: elevation RPC only.
  - Yamato: no RPC.

  (Ship gun FC Wikipedia ; navweaps 38 cm ; tech-086)
- **[INFERRED] laying error for the model:**
  - Follow-the-pointer adds ~1–3 mils random laying error per gun plus a 1–3 s lag.
  - RPC adds well under 1 mil.

### 5.4 Mount rates (cap the bearing rate that can be tracked)

| Mount | Train | Elevation | Fire rate | Source |
|---|---|---|---|---|
| US 16"/50 Mk 7 | 4°/s | 12°/s | 2 rpm | navweaps 16"/50 |
| German 38 cm C/34 | 5°/s | 6°/s | ~2.3–3 rpm nominal; Denmark Strait <1 rpg/min | navweaps 38 cm |
| Japanese 46 cm | 2°/s | 10°/s | 1.5–2 rpm (~35 s cycle at battle elevations) | Wikipedia 46 cm; navweaps 18" |
| OTO 76 mm (post-war) | 60°/s | 35°/s | 120 rpm | https://navalhistory.dk/English/Weapons/Guns_after1945/76mmM85.htm |

**[INFERRED] bearing rate vs mount limits.** Relative bearing rate (°/s) ≈ 0.0323 × speed-across (kn) / range (kyd). For example, 40 kn of combined crossing speed gives ~0.65°/s at 2 kyd and ~1.3°/s at 1 kyd. Heavy turrets can therefore keep up with ship targets except at a few hundred yards or during the firer's own fast turns (~1–3°/s for a battleship). Own-ship turn rate plus target bearing rate must stay below the 2–5°/s train rate.

---

## 6. Post-war automation

| System | Type / numbers | Source |
|---|---|---|
| Mk 56 GFCS | Solution in **<2 s** after Mk 35 radar lock (AA-focused) | Ship gun FC (Wikipedia) |
| Mk 63 (1953) | SPG-34 radar on the gun mount; Mk 29 sight | Wikipedia; https://hazegray.org/navhist/canada/systems/firecontrol/ |
| Mk 68 / Mk 47 computer | Electro-mechanical with potentiometer multipliers; SPG-53 (120 kyd); in production >25 yr; digital upgrade 1975–85; controls the 5"/54 | https://man.fas.org/dod-101/sys/ship/weaps/mk-68.htm ; https://en.wikipedia.org/wiki/AN/SPG-53 |
| Mk 86 (service 1974) | Digital; SPQ-9 2-D TWS with 4 channels + SPG-60; 2 targets at once; operator ballistic inputs (GFMPL wind/density, powder temperature from the average of the last three magazine readings, seating distance for IV loss); Mk 34 velocimeter on later systems | https://man.fas.org/dod-101/sys/ship/weaps/mk-86.htm ; https://man.fas.org/dod-101/navy/docs/swos/gunno/INFO2.html ; INFO9 |
| Mk 92 (= WM-25 licence, approved 1975) | 2 surface + 1 air gun channel (Mod 1); STIR in Mod 2/6 | https://en.wikipedia.org/wiki/Mark_92_Guided_Missile_Fire_Control_System |
| HSA WM-20 series | Single digital SMR computer; TWS + tracker in one radome | https://www.militaryperiscope.com/weapons/electronics/naval-radars/wm20/overview/ |
| Iowa 1980s | Mk 8 analog rangekeepers + HP digital; DR-810 per-gun muzzle-velocity radar; 1987 test: 220 yd pattern at 34 kyd (0.64% of range) | navweaps 16"/50 ; Fire-Control-Part-2 |
| Soviet | Sverdlov: Top Bow / Egg Cup / Sun Visor radars; no computer specs found | https://en.wikipedia.org/wiki/Soviet_cruiser_Sverdlov [UNCERTAIN] |

**Error sensitivity remains even with digital systems.** At 25 kyd a 1 kn wind error is still ~125 yd and a 1% density error ~135 yd. Digital systems shrink *kinematic* error but not *meteorological* error. (INFO9)

**Not found:** single-shot hit probabilities for the 5"/54 or OTO 76 mm against surface targets; Mk 86 accuracy in mils. A Sri Lankan SSHP model of the OTO 76/62 exists but its figures are not public. (https://ir.kdu.ac.lk/handle/345/2906)

**[INFERRED] game defaults for 1970s–80s digital GFCS vs a surface target at 5–10 nm:**
- solution in 2–5 s after track;
- kinematic error 0.5–1 mil;
- ballistic error dominated by MET, 0.3–0.5% of range before spotting;
- first-round SSHP vs a frigate-sized target ~10–30% at 5 nm and a few % at 10 nm.

---

## 7. Outcome anchors (calibrating the overall chain)

| Case | Result | Source |
|---|---|---|
| Jutland (RN) | ~3% hits | Rangekeeper (Wikipedia) |
| USN 1930–31 practice | 4–6% | Rangekeeper (Wikipedia) |
| LRBP 1932–33, ~30 kyd | 4.4% | INRO |
| Battle-line raft practice, 27.5 kyd | 5.1% | INRO |
| 16"/50 WWII expected | 32.7% / 10.5% / 2.7% at 10 / 20 / 30 kyd (optimistic) | navweaps 16"/50 |
| Washington vs Kirishima, night radar | 9 of 75 (12%) at 8.4 kyd | Rangekeeper (Wikipedia) |
| Duke of York vs Scharnhorst | 31 of 52 radar-controlled salvos straddled | https://en.wikipedia.org/wiki/Battle_of_the_North_Cape |
| 1917 USNI model, 14" at 18 kyd | Danger space 49 yd vs fire-control error "several hundred yd"; ~5.7% hits | https://www.usni.org/magazines/proceedings/1917/june/estimate-value-accuracy-pointing-long-range-naval-gunnery |
| BuOrd hit trend formula | H = 100/[1 + 0.0007(R − 2000)] | INRO |
| 1920–45 trend | Mean dispersion fell 66%, MPI error only 23%, i.e. *computing/bias error became the limiting factor* | INRO |

---

## 8. Suggested parameter table for the simulator [INFERRED; calibrated loosely to the above]

| Era / system | Rate σ after settle | Settle time | Residual bias (pre-spot) | Own-turn penalty | Target-turn re-converge |
|---|---|---|---|---|---|
| Eye + hand (pre-1905) | ±4 kn, ±20° | 3–5 min | 1–2% of R | lose solution | 3–5 min |
| Dumaresq + Vickers clock | ±3 kn, ±15° | 2–4 min | 1–1.5% R | lose rate (constant-rate clock) | 3–4 min |
| Dreyer Mk III/IV (1912–18) | ±2 kn, ±10° | 1.5–3 min | 0.8–1.2% R | partial (helm-free) | 2–3 min |
| Argo clock | ±1.5 kn, ±8° | 1–2 min | ~1% R | mostly OK | 1.5–2.5 min |
| AFCT/AFCC; Japanese Type 92/98; German 1930s | ±1.5 kn, ±8° | 1–2 min (JP 2–3) | 0.5–0.8% R | AFCT OK, JP poor | 1–2 min (JP 2–4) |
| US Mk 8 + SV Mk 41 (+radar) | ±1 kn, ±5° | 45–90 s (30–60 s radar) | 0.3–0.5% R | MPI shifts a few hundred yd in hard turns | 45–90 s |
| Mk 1A auto rate (≥15 kn targets) | ±1 kn | 10–20 s | 0.4% R | OK | 15–30 s |
| Digital (Mk 86, WM-25), 1970s+ | ±0.5 kn | 2–5 s | 0.3% R (MET-limited) | OK | 5–15 s |

The miss from a rate error is `rate error (yd/s) × TOF` immediately, plus `rate error × time since last spot or range` accumulating, until the next spot is applied.
