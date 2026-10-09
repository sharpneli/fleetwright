# 03 — Gunnery / Fire-Control Radar, 1939–1990 (surface fire, with AA notes)
Status: final    Updated: 2026-10-09    Request: -

Tags: [INFERRED] means derived or calculated by me. [UNCERTAIN] means the sources are thin or conflict. Source keys are listed at the end. Each fact carries its key, e.g. [NW-US].

---

## 1. Key takeaways for the simulator

1. **Radar range error is roughly constant, plus a small proportional term.** USN microwave sets were rated at ±(15 yd + 0.1 % of range) [RAD1][IBB-FC]. Optical coincidence and stereo rangefinder error grows with R² [AT-OPT]. Radar range is better than optical beyond about 7–13 kyd against a good microwave set, and beyond about 16–27 kyd against an early 50 cm RN set (§5) [INFERRED].
2. **Metric and decimetric sets ranged well but gave poor bearing.** Mk 3 at 40 cm with lobing gave ±2–4 mil; the RN Type 284 at 50 cm needed beam switching (284M/P) before it was usable for bearing. Before that, ships fed radar range plus optical bearing into the computer, as Washington did at Guadalcanal [NW-T79][WIKI-SGFCS][MAR-FORUM].
3. **Splash spotting is a step change.** Mk 3 could range 16" splashes to about 20 kyd but could not spot deflection. Mk 8 spotted range well and small deflection errors poorly. Mk 13 (3 cm) was "accurate in range and deflection over full gun range", with individual 16" splashes visible beyond 42 kyd [NW-US][NAVSRC].
4. **Real night actions:** West Virginia fired 16 salvos under full radar control at Surigao, and all 13 full salvos straddled [LEY-BE]. Duke of York fired 52 radar-controlled salvos at North Cape and 31 straddled [WIKI-NC].
5. **Radars were fragile.** Bismarck's FuMO 23 was knocked out by the blast of her own guns [WIKI-BIS]. Duke of York's Type 284 aerial was knocked over by 11" hits and re-erected by hand [WIKI-NC]. KGV's Type 284 failed at 09:13 on 27 May 1941 [NW-T16]. Norfolk lost all radar except the 284 to one hit [GAZ-NC]. Wichita's Mk 8 was "unduly sensitive" to the shock of her own gunfire [LEY-BE].
6. **Post-war sets** (X-band, conical scan, later monopulse or track-while-scan) auto-track, give about 0.8–1 mrad angular accuracy and about 10 yd range accuracy, and need about 2 s from lock-on to a firing solution (Mk 56) [WIKI-MK56][RT-WM20][WIKI-347].

---

## 2. WWII set-by-set data

### 2.1 United States

| Set | In service | λ / band | Peak pwr | Beam (H) | Angle method | Range acc. | Bearing acc. | Max range (target) | Min range | Splash spotting | Auto-track | Src |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **Mk 3 (FC)** | late 1941, BB/CA main battery | 40 cm (≈700 MHz) | 15–20 kW | wide (not given; about 10° [INFERRED]) | **lobing** (first US set to use it) | ±40 yd | ±2 mil (3×12 ft antenna); ±4 mil (6×6 ft) | 28 kyd BB, 16 kyd DD [RAD2]; 40 kyd quoted [NW-US] | 1,000 yd | 16" splashes rangeable to about 20 kyd; range only, no deflection [NW-US][NW-T79] | No | NW-US, RAD2 |
| **Mk 4 (FD)** | Sept 1941, Mk 37/33 DP directors | 40 cm (L) | — | — | lobing (two stacked half-Mk 3 arrays) | ±40–50 yd | ±4 mil; elevation ±4–5 mil above 10° | 25–30 kyd BB, 16 kyd DD, 35–40 kyd bomber | 1,000 yd | night shoot: a new cruiser's first 11 salvos all straddled (FD) [CINC-42] | No; could not track low fliers | NW-US, RAD1, RAD2 |
| **Mk 8 (FH)** | 1942–43, BB/CA | 10 cm (S) | 15–20 kW, later 20–30 kW | narrow; electronically scanned 42-polyrod array (B-scope sector 29°) | electronic phase-shift scan ("rocking" beam), pulse-switching | ±(15 yd + 0.1 %R) | 0.1° to 2 mil | 40 kyd on a BB/CA-size target (antenna 120 ft) | 250–500 yd | Mod 0: 16" splashes to about 20 kyd; Mod 3: 14"/16" splashes to at least 35 kyd. Range spotting excellent; **deflection spotting impractical for small errors**; splashes within 1,000 yd of target read to the nearest 100 yd | No (manual B-scope) | NW-US, RAD1, RAD2, NAVSRC |
| **Mk 13** | 1945 (replaced Mk 8 on Mk 34/38 directors) | 3 cm (X) | 50 kW; PRF 1,800; 0.3 µs pulse | **0.9° × 3.6°** | mechanical "rocking horse" scan, 11.5° (200 mil) arc at 5 sweeps/s each way | ±(15 yd + 0.1 %R); repeaters read to 10 yd | ±2 mil | 40 kyd BB-size; main sweep to 80 kyd | 250 yd | **individual 16" splashes beyond 42 kyd**; accurate in range **and deflection** over full gun range; splash echo about 18 mil wide | could train the director automatically (bearing) | NW-US, NAVSRC, SLV-20G, WIKI-SGFCS |
| **Mk 12 (+Mk 22)** | 1944, Mk 37 DP | 33 cm (L), 100–110 kW; Mk 22 height finder 3 cm | — | resolution 300 yd and 7° | lobing; Mk 22 "orange peel" for elevation (0.8° above horizon) | ±20–25 yd | ±3 mil; elevation ±2–3 mil above 7° | 40 kyd large ships, 45 kyd bombers | 400 yd | — | automatic **range** tracking and range rate | NW-US, RAD1, RAD2 |
| **Mk 26** | DE, Mk 52 FCS | 15 cm (S), 50 kW | — | — | range-only (J-scope) | ±100–150 yd | optical | 25 kyd ship, 15 kyd bomber | 400 yd | — | No | NW-US, RAD1 |
| **Mk 27** | standby / turret | 10 cm, 50 kW | — | — | range-only | ±160 yd | 6.5° | — | — | — | No | NW-US |
| **Mk 28 / 29 / 34** | 1944–45, light AA and CL/DD directors | S / X | Mk 34: 25–30 kW | Mk 34: **2.4°** (40 in dish) | **conical scan** (nutation 30 Hz, 0.75° offset) | ±(15 yd + 0.1 %R) | ±2 mil (Mk 29/34), ±4 mil (Mk 28) | Mk 34: 25 kyd; Mk 28: fighter 15 kyd | 300–400 yd | — | angle tracking by conical scan; Mk 34 "locks on" | RAD1, JPROC, NW-US |

Display notes: Mk 4 and Mk 12 used an A-scope with a range "notch" and pip-matching for bearing. Mk 8 and Mk 13 had a switchable A or B scope plus a precision sweep. The Mk 13 precision sweep shows a 2–4 kyd window with bearing dots every 200 yd. Mk 28/29/34 used an A-scope plus an R-scope with a 2,500 yd expanded window [RAD1][SLV-20G]. The Mk 13 B-scope has bearing lines 50 mil apart and a total width of about 200 mil [SLV-20G].

US bench versus sea accuracy: the manual warns that a ±2 mil bench bearing can degrade to about ½° (≈9 mil) at sea against a 300-knot air target [RAD1].

### 2.2 Royal Navy

| Set | In service | λ | Peak pwr | Beam | Angle method | Range acc. | Bearing acc. | Max range | Splash | Src |
|---|---|---|---|---|---|---|---|---|---|---|
| **Type 279 (ranging mode)** | 1940 | 7.5 m | 60–70 kW | very wide (metric sets are about 20°) | — | coarse | ±3° (Hood) | 100 nm aircraft | KGV used it for ranging after the 284 failed (27 May 1941) | NW-BR, HOOD, KBIS-AS, NW-T16 |
| **Type 284** | June 1940 (Nelson); Hood early 1941 | 50 cm (600 MHz) | 25 kW | not stated; 21 ft cylinder-parabola [UNCERTAIN, ≈5–8° INFERRED] | none (original); bearing beam-switching added later | **±120 yd** (240 yd quoted without a correction template) | ±3–5 arc-min **with** beam switching | about 20 kyd on a BB (CB 3213); 26 kyd (Suffolk); 10 nm nominal | used for ranging and spotting; KGV ranged on Rodney's splashes by mistake | COMMS, NW-BR, WW2T, MAR-FORUM, NW-T16 |
| **284M / 284P** | 1941 / 1942 | 50 cm | 150 kW | — | **lobe switching** (allows blind fire) | resolution about 150 m; 284P had a Precision Ranging Panel | — | Duke of York: 29.7 kyd track on Scharnhorst | North Cape: 52 radar salvos, 31 straddles | LIST-RN, MAR-FORUM, WIKI-NC |
| **Type 285 / 285M/P** | 1941–42, DP/HA directors | 50 cm | 25 kW (150 kW M/P) | **18° H × 43° V**; 6-element Yagis | beam switching (M) | ±150 yd; **285P 25 yd RMS** | about ±15 arc-min vs aircraft | 17–18 kyd aircraft | blind fire only useful vs low torpedo bombers | WIKI-285, COMMS, LIST-RN |
| **Type 274** | from May 1944 (Belfast, Vanguard) | 9.1 cm (3.3 GHz) | 400–500 kW; 0.5 µs | "double cheese", separate Tx/Rx | azimuth switching on the Rx aerial | 100 m (RT); about 10 yd with the best displays [COMMS] | **better than ±3 arc-min** on small targets (not achievable on large ones) | 16 nm; designed to hold a DD at max gun range; resolution 80 m | special ±2,000 yd display estimates the MPI distance in **range only, not line** | RT-274, COMMS, LIST-RN |
| **Type 275** | 1945, DD main / BB secondary | 8.5 cm | 400 kW | twin dishes | **conical scan** | — | — | acquisition 36 kyd; reliable track below 25–30 kyd | — | COMMS, LIST-RN |
| **Type 262** | 1944–45 (twin Bofors STAAG); later Seacat | 3.2 cm (9.67 GHz) | 30 kW | 5.2° | conical scan (spun offset dish) | — | about ±5 arc-min | acquisition 7 kyd, 3-axis lock by 5 kyd | **auto search and lock-on** | COMMS, RT-262 |
| **Type 931** | not completed by VJ-day | 1.25 cm | — | — | plan display purpose-built for fall-of-shot spotting | — | — | — | — | COMMS |

### 2.3 Germany (Seetakt / FuMO family, about 80 cm, 368 MHz)

| Set | Fitted | Range | Range acc. | Bearing acc. | Notes | Src |
|---|---|---|---|---|---|---|
| Seetakt (Dete 1, 1938) | Graf Spee, Königsberg | 22 km good conditions, typically about half | **about 50 m** (optical RF at 20 km about 200 m) | about 1° with lobe switching | 8 kW, PRF 500 | WIKI-SEE |
| FuMO 21 | destroyers, 1941 | 14–18 km | 70 m | 3° | — | NW-GER |
| FuMO 22 / 23 | Scharnhorst, Gneisenau 1939; Bismarck and Tirpitz 1940 (FuMO 23 on 3 FC cupolas) | BB vs BB about 25 km | [UNCERTAIN] 25–70 m | **early sets: bearing resolution 5–6°**; lobing reportedly dropped from production in 1937 | AVKS report: bearing "unsatisfactory", hood play, echo jumps. Detected Suffolk at 12.5 km (23 May 1941); **forward set disabled by Bismarck's own gun blast** firing at Norfolk | NW-GER, MAR-FORUM, WIKI-BIS |
| FuMO 24/25 | capital ships, DDs (1943) | 15–20 km | 70 m | 0.3° | 2×6 m mattress | NW-GER |
| FuMO 26 | Tirpitz 1944 | 20–25 km | 70 m | 0.25° | 3×6 m mattress, about 3° beam [UNCERTAIN] | NW-GER, MAR-FORUM |
| FuMO 27 | Prinz Eugen 1940, Scharnhorst 1941 | ≈ FuMO 23 | — | — | 2×4 m | NW-GER |

German practice: radar gave range, and bearing came from optics. There was no true blind fire. Wikipedia's fire-control article says only the RN and USN reached radar-only blind fire [WIKI-SGFCS]. At North Cape an early hit wrecked Scharnhorst's forward Seetakt, and she then had to aim at British muzzle flashes [WIKI-NC].

### 2.4 Japan

| Set | λ | Peak | Range | Accuracy | Notes | Src |
|---|---|---|---|---|---|---|
| Type 21 (Mk2 Mod1) | 150 cm | 5 kW | 20 km large ship; 70–100 km aircraft | about 1–2 km | air search | CF, SP |
| **Type 22 (Mk2 Mod2)** | 10 cm | **2 kW** | 34.5 km large ship; 17 km single aircraft | **about 200 m range, about 3° bearing** | separate horns, no dish, crystal receiver; 300 built; Kongo/Haruna Oct 1942, Yamato/Musashi Oct 1943; "moderately accurate" for gunnery, not designed for it | CF, SP |
| Type 32 / 33 | 10 cm | 2 kW | 30 km large ship / 12–13 km small ship | 100–250 m, 0.5° [UNCERTAIN] | the dedicated FC sets; **never used operationally** | CF, SP |

IJN weaknesses: low power, no PPI, unreliable valves and magnetrons [WW-JAP]. No splash spotting is recorded. Yamato at Samar ranged optically with 10 m rangefinders, and the spray from her own splashes plus US smoke defeated tracking [YAM-SAM]. USN analysts stated the IJN relied "solely on optical range finders" for gunnery [NW-T86]. At Surigao the Japanese did not effectively use their guns [LEY-BE].

---

## 3. How radar was used against optics: battle record

| Action | Radar facts | Src |
|---|---|---|
| **Denmark Strait, 24 May 1941** | Prince of Wales obtained **no** radar ranges from Type 284 or 281; she fired on optics. Suffolk's Type 284 (about 26 kyd) shadowed Bismarck, which was tracked through a rainstorm. On 24 May Suffolk straddled at 20,700 yd using radar ranges. The German ships "probably" used radar ranges. | WIKI-DS, WW2T |
| **Bismarck, 27 May 1941** | KGV's Type 284 gave the opening range of 25,100 yd and passed it to Rodney. KGV then **ranged on the echoes of Rodney's splashes**, so she was off target until 09:10. The 284 failed at 09:13; she switched to the Type 279 (09:29–09:53). Rodney used optics throughout, hampered by funnel haze at long range. | NW-T16 |
| **Guadalcanal, 14–15 Nov 1942** | Washington used Mk 3 range plus **optical bearing**. Initial radar detection was at about 20 kyd. She opened fire at 8,400 yd and fired 75 × 16" rounds in 7 min. Her FC radar **saw no splashes**, either because receiver gain was cut by the huge target echo or because of Savo Island clutter. Hits observed: 8 or more; actual hits about 9–20. South Dakota lost all radar to an electrical fault. | NW-KIR, WIKI-NBG, NW-T79 |
| **North Cape, 26 Dec 1943** | Belfast detected Scharnhorst at 30.5 kyd; Duke of York's Type 273 at 45.5 kyd; her Type 284 tracked from 29.7 kyd. Duke of York **opened fire at 11,920 yd** by radar and starshell and hit with her first salvo. 52 radar-controlled salvos, 31 straddles. The 284 aerial was knocked down by 11" shells and fixed within minutes. | WIKI-NC, GAZ-NC |
| **Surigao Strait, 25 Oct 1944** | West Virginia (Mk 8) detected the target at 41–42 kyd, tracked to 22.4 kyd, and opened fire at about 21–22 kyd: 16 salvos, 93 rounds, all 13 full salvos straddled, every salvo under full radar control. Mk 8 ships fired 93, 69 and 63 rounds. Mk 3 ships: Maryland 48 (found the target only at about 22 kyd, partly by ranging on other ships' splashes), Mississippi 12 (one salvo), Pennsylvania 0. | LEY-BE, NW-T79, NG-SPOT |
| **Coral Sea era (1942)** | FC radar ranged cruisers to 25 kyd from high antennas; destroyers to 17 kyd. 14" projectiles could be ranged in flight to about 13 kyd. Five FC ranges gave a "perfect" range-rate setup. Recommended maximum for optical fire was 10 kyd; FD radar allowed 12 kyd or more. | CINC-42 |

Typical WWII fire-control procedure ("aided ranging"): the radar operator laid the range line on the target echo. The rangekeeper generated range, and the operator applied corrections until generated and observed range agreed. Mk 3 Mod 1 and Mk 13 sent range to the Mk 8 rangekeeper automatically [SLV-20G][NW-T86]. A radar-ranged first salvo made first-salvo straddles common [NG-SPOT].

---

## 4. Radar spotting of fall of shot

- **Display.** The B-scope (range against bearing) shows the target pip with the splash pips around it. The MPI's range **and** deflection are read straight off it [RAD8A][SLV-20G]. An A-scope gives range only. A Type 274-style expanded range display (±2,000 yd) shows range error but **not line** [COMMS].
- **Salvo footprint on the scope.** A full salvo forms a ladder about 50 yd wide and up to 500 yd long [RAD8A]. The Mk 13 splash echo is about 18 mil wide [NAVSRC], against a 0.9° (16 mil) beam.
- **Resolution.** Mk 8 reads splashes to the nearest 100 yd within 1,000 yd of the target [NAVSRC]. Mk 13 pulse length is about 100 yd, shown as 50 yd on the scope [SLV-20G]. Type 284M resolution was about 150 m [MAR-FORUM].
- **Splash pip duration.** No source found. [UNCERTAIN / INFERRED]: a heavy-shell column stands for about 5–10 s, so the pip is visible for a few PRF-integrated sweeps (Mk 13 sweeps 10 times a second). Model a "spotting window" of about 3–8 s after impact.
- **Problems.** (1) The target's echo saturates the receiver, so splashes are lost, as at Washington 1942 [NW-KIR]. (2) **Splash and target pips merge** when shots fall close; Mk 8 could not spot small deflection errors [NAVSRC]. (3) With several ships firing on one target, splashes are mis-assigned or ranged as the target (KGV with Rodney's splashes) [NW-T16]. (4) Land clutter near shore, as at Savo [NW-KIR]. (5) Line of sight is required, so radar is useless for indirect bombardment, where air spotters are preferred for deflection [RAD8A][NG-SPOT]. (6) Shells can be followed in flight on radar at short range [RAD1][CINC-42].
- US doctrine: use averaged radar spots whenever available. Range is spotted first, then deflection [RAD8A].

---

## 5. Error growth against range: radar compared with optical [INFERRED calcs]

Optical rangefinder error (AT formula): E = dq·R² / (B·M·206265). For dq = 12″ and M = 25, the coefficients are k ≈ 4.65e-7 yd⁻¹ for a 15 ft base and 1.66e-7 for a 42 ft base [AT-OPT]. Radar error is ±(15 + 0.001R) yd for USN microwave sets, a flat ±40 yd for Mk 3, and ±120 yd for Type 284 [RAD1][RAD2][COMMS].

| Range (yd) | 15 ft optical | 42 ft optical | Mk 8/13 radar | Mk 3 | Type 284 |
|---|---|---|---|---|---|
| 5,000 | 12 | 4 | 20 | 40 | 120 |
| 10,000 | 47 | 17 | 25 | 40 | 120 |
| 15,000 | 105 | 37 | 30 | 40 | 120 |
| 20,000 | 186 | 67 | 35 | 40 | 120 |
| 30,000 | 419 | 150 | 45 | 40 | 120 |

**Crossover ranges, where radar becomes better than optical** [INFERRED]:

| | 15 ft RF | 42 ft (≈10.5 m) RF |
|---|---|---|
| Mk 8/13 | ≈ 6,900 yd | ≈ 13,000 yd |
| Mk 3 | ≈ 9,300 yd | ≈ 15,500 yd |
| Type 284 | ≈ 16,000 yd | ≈ 27,000 yd |
| Seetakt (≈ 50–70 m) | ≈ 11–13 kyd | ≈ 18–21 kyd |

Caveats. These are laboratory optical numbers. Real optical spreads were worse (FQ2 about 1 % of range at 14.5 kyd [AT-OPT]) and degrade with haze, smoke, night, and the operator's stereo acuity. Radar does not degrade with visibility, and its error depends on the operator laying the range line, which a lag can bias (Wichita) [LEY-BE].

Bearing [INFERRED]: 2 mil ≈ 40 yd lateral at 20 kyd, comparable to a good optical director. Mk 3 lobing at ±4 mil (80 yd at 20 kyd) or the Type 284's original lack of switching forced the use of optical bearing.

---

## 6. Limitations

| Limitation | Data | Src |
|---|---|---|
| Minimum range | Mk 4 and Mk 3: 1,000 yd; Mk 8/13: 250 yd; Mk 34: 300 yd; Mk 12/26/28: 400 yd | RAD1, RAD2 |
| Radar horizon | Antenna at 120 ft gave Mk 8 40 kyd on a cruiser. Beyond 15–20 nm targets are hull-down and echo only from masts. Rule of thumb: horizon (nm) ≈ 1.23(√h_ant + √h_tgt) in ft, so 120 ft against 100 ft gives about 26 nm [INFERRED]. Destroyers with low antennas got about 17 kyd on cruisers. | RAD1, RAD2, CINC-42 |
| Low-angle lobing / multipath | Mk 4 could not track low fliers. Mk 22 was added to see aircraft 0.8° above the horizon. Mk 13 sees a target 2° above land. Not quantified for ships. | NW-US, NAVSRC |
| Bearing (metric sets) | Metric beams about 20°; 285 beam 18°; early Seetakt 5–6° resolution; needed lobing or optical bearing | KBIS-AS, WIKI-285, MAR-FORUM |
| Clutter | Land (Savo), own splashes, other ships' splashes | NW-KIR, NW-T16 |
| Jamming / ECM | US repeaters could switch frequency to the least-jammed band. Conical scan is easy to jam; monopulse resists it. | RAD1, NW-T8 |
| Propagation | Ducting extends range and bending cuts it; quoted maximum ranges are unreliable | RAD1 |
| Lag / manoeuvre | Radar FC lag gave large deflection errors against hard-manoeuvring targets (CruDiv 13) | LEY-BE |
| Fragility | Own-gun blast (Bismarck, Wichita); splinters (Sheffield); hits on the foremast (Duke of York, Norfolk); electrical faults (South Dakota) | WIKI-BIS, LEY-BE, GAZ-BIS, WIKI-NC, GAZ-NC, WIKI-NBG |

---

## 7. Post-war automated fire-control radars

| System / radar | Year | Band | Peak pwr | Beam | Tracking | Range acc. | Angle acc. | Range | Reaction / notes | Src |
|---|---|---|---|---|---|---|---|---|---|---|
| **Mk 56 / Mk 35 (SPG-35)** | first delivered Aug 1945; 1950s fleet | X | 50 kW | 2° | spiral-scan acquisition (6° swing), then **conical-scan auto-track** in range, bearing and elevation | **9 m (10 yd)** | "as accurate as optical" | 27 km | **solution about 2 s after lock-on**; RN Type 903/MRS-3 is a derivative | WIKI-MK56, WIKI-SGFCS |
| **Mk 37 + Mk 25** | late 1940s–50s | X (8.5–9 GHz) | 50 kW, 0.2 µs | 1.3°, 62 in dish | conical-scan auto-track | resolution 36 m | — | 24 nm instrumented | — | RT-MK25 |
| **Mk 63 + Mk 34 (SPG-34)** | 1945–1980s | X | 25–30 kW | 2.4° | conical scan (0.75° nutation, 30 Hz), lock-on | ±(15 yd + 0.1 %R) | ±2 mil | 25 kyd | AA 800–7,000 yd | JPROC, RAD1 |
| **Mk 68 / SPG-53** | 1950s–1990s (5"/54 DDs) | X [UNCERTAIN] | 250 kW (53A) | — | conical-scan acquisition and track; digital upgrade 1975–85 | — | — | 120 kyd | — | WIKI-53, WIKI-SGFCS, FAS-68 |
| **Mk 86 / SPQ-9 + SPG-60** | 1970s (DD-963, CG-47) | X (SPQ-9) | SPQ-9: 1.2 kW (B model) | — | SPQ-9: **4 TWS channels** for surface; SPG-60 for air. 2 targets at once | — | — | SPQ-9 0.15–20 nm | splash points shown on the WCC B-scan; **spots entered manually** | FAS-GUN, WIKI-SPQ9, GS-86 |
| **Mk 92 (HSA WM-28) CAS + STIR** | 1975/78 (FFG-7) | X | — | — | monopulse tracker for 1 air/surface target + 2 surface TWS; STIR +1 | — | — | — | — | FAS-92, WIKI-92 |
| **HSA WM-20/22/25** | late 1960s–70s | X, TWS | 180–200 kW | — | track-while-scan + tracker | — | **0.8 mrad** | 32 nm instrumented | — | RT-WM20 |
| **Selenia Orion RTN-10X / -20X (SPG-74)** | 1970s (Dardo, OTO 76) | I/X | — | — | monopulse/conical [UNCERTAIN] | — | — | — | Chinese Type 347G (said to be derived from SPG-74): 150 kW, **1.8°** beam, **≤1 mrad**, 30 km vs 2 m², 15 km vs 0.1 m² sea-skimmer | HELIS, WIKI-347 |
| **Signaal LIROD (Mk 2)** | 1980s–90s | **K (35 GHz)** | 100 W avg TWT | 1.5° az × 0.55° el | radar + EO/IR + laser | — | — | 36 km | slew 2 rad/s | LIROD |
| **RN Type 262 (post-war, Seacat/Bofors)** | 1958 version | X | 30 kW | 5.2° | conical, auto lock | — | — | 29 nm instr. | — | RT-262 |
| **Soviet MR-103 Bars** | 1960s (AK-725) | — | — | — | auto-track to 700 m/s targets | — | — | capture ≤40 km | — | GS-1134 |
| **"Owl Screech" (MR-105 Turel)** | IOC 1961, Kynda (76 mm) | — | — | — | — | — | — | — | — | MP-OWL |
| **MR-123 Vympel "Bass Tilt"** | 1970s (AK-630 / AK-176) | — | — | — | radar + SP-521 TV/laser | — | — | air 4 km, surface 5 km (gun limits) | EO sees torpedo boats to 70 km [UNCERTAIN] | WIKI-630 |
| **MR-184 Lev "Kite Screech"** | 1980s (AK-130, AK-100) | **dual-band: I (coarse) + K (fine)** | — | — | 2 targets; TV + DVU-2 laser RF | — | — | 75 km detect, 40 km track | — | WIKI-130, MP-KITE |
| **Phalanx (reference for closed-loop)** | 1980 | Ku | — | — | tracks target **and its own outgoing rounds** and drives the miss distance to zero | — | — | detect about 6 kyd, fire about 2 kyd | reaction about 3 s; closed loop estimated to raise lethality about 10× | NG-PHAL |

Generic post-war modelling values:
- **Lock-on time.** Spiral-scan acquisition to conical-scan track takes about 1–3 s [UNCERTAIN, Mk 56 basis].
- **Solution after lock.** About 2 s (Mk 56) [WIKI-MK56].
- **Angle error.** 0.8–1 mrad RMS (WM-20, Type 347) [RT-WM20][WIKI-347].
- **Range error.** About 9 m (Mk 56) [WIKI-MK56].
- **Laser rangefinder on 1980s directors** (DVU-2, SP-521, LIROD-class) [UNCERTAIN]: typically about ±5 m, with range limited by weather.
- **Closed-loop spotting.** Radar measures the miss distance of the rounds and corrects automatically. This is standard for CIWS (Phalanx). For medium-calibre surface fire it was proposed and studied by Monte Carlo methods in the late 1980s (KS-CLSA). In US Mk 86 practice, spots stayed **operator-entered from the B-scan** [FAS-GUN].
- **Hit probabilities.** No quantified Pk/Ph figures for post-war surface gunnery were found [UNCERTAIN].

---

## 8. Suggested sim parameters [INFERRED]

| Era / class | σ_range | σ_bearing | Max FC range (BB/CA/DD) | Splash spotting | Blind fire |
|---|---|---|---|---|---|
| Metric FC 1940–42 (284, FuMO 23, Type 22) | 50–120 m | 1–5° (needs optical bearing) | 20–25 / 18 / 12 km | range only, ≤15 km, unreliable | No (284M yes, crude) |
| Decimetric lobed (Mk 3, Mk 4, 284M/P) | 35–40 m | 2–4 mil | 26 / 23 / 15 km | range only, about 18 km (16") | Partial |
| Centimetric S (Mk 8, Type 274) | 15 m + 0.1 %R | 2 mil, or 3′ | 37 / 37 / 25 km | range good to 32 km; deflection coarse | Yes |
| X-band (Mk 13) | 15 m + 0.1 %R | 2 mil, 0.9° beam | 37+ km | range + deflection, 38 km+ | Yes |
| Post-war auto-track (Mk 56 → WM-25) | 9 m | 0.8–1 mrad | 25–60 km instrumented | B-scan manual spots; closed loop only for CIWS | Yes; 2 s solution |

Splash spotting failure modes worth simulating: pip merging within about 1 pulse-length or 1 beamwidth of the target, receiver saturation on very large targets at short range, and confusion over whose splashes are whose when several ships fire on one target.

---

## Source key

- NW-US — https://www.navweaps.com/Weapons/WNUS_Radar_WWII.php
- NW-BR — https://navweaps.com/Weapons/WNBR_Radar.php
- NW-GER — http://www.navweaps.com/Weapons/WNGER_Radar.php
- NW-T79 (Surigao) — https://www.navweaps.com/index_tech/tech-079.php
- NW-T16 (Bismarck's final battle) — https://navweaps.com/index_tech/tech-016.php
- NW-T86 (US vs IJN FC) — https://www.navweaps.com/index_tech/tech-086.php
- NW-T8 (tracking methods) — https://navweaps.com/index_tech/tech-008.php
- NW-KIR (Kirishima) — https://www.navweaps.com/index_lundgren/kirishimaDamageAnalysis.php
- RAD1 (COMINCH P-08-03 radar handbook) — https://ibiblio.org/hyperwar/USN/ref/RADONEA/COMINCH-P-08-03.html
- RAD2 (Ship fire-control sets) — https://ibiblio.org/hyperwar/USN/ref/Radar/Radar-2.html
- RAD8A (spotting) — https://ibiblio.org/hyperwar/USN/ref/RADEIGHTA/RADEIGHTA-21.html
- CINC-42 (CINCPAC gunnery bulletin 1942) — https://ibiblio.org/hyperwar/USN/rep/CINCPAC/GunBull/2-42/index.html
- LEY-BE (Leyte battle experience) — https://www.ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.3.html
- NAVSRC (Mk 8 / Mk 13) — https://www.navsource.org/archives/01/57r.htm
- SLV-20G (Mk 13 manual) — https://eugeneleeslover.com/USNAVY/CHAPTER-20-G.html
- COMMS (CB 3213 / BR 2435 "Navy Radar") — https://www.commsmuseum.co.uk/publications/chc/Navy%20Radar%20CB%203213%20-%20BR%202435.pdf
- LIST-RN — https://en.wikipedia.org/wiki/List_of_World_War_II_British_naval_radar
- WIKI-285 — https://en.wikipedia.org/wiki/Type_285_radar
- HOOD — https://www.hmshood.org.uk/ship/radar.htm
- WW2T (Suffolk radar) — https://www.ww2today.com/p/26-05-24-bismarck-and-royal-navy-radar
- MAR-FORUM — https://forum-marinearchiv.de/smf/index.php?msg=403346
- KBIS-AS — https://kbismarck.org/asradars.html
- WIKI-SEE — https://en.wikipedia.org/wiki/Seetakt_radar
- WIKI-BIS — https://en.wikipedia.org/wiki/German_battleship_Bismarck
- WIKI-DS — https://en.wikipedia.org/wiki/Battle_of_the_Denmark_Strait
- WIKI-NC — https://en.wikipedia.org/wiki/Battle_of_the_North_Cape
- GAZ-NC (Fraser despatch) — https://thegazette.co.uk/London/issue/38038/supplement/3706/data.pdf
- GAZ-BIS (Tovey despatch) — https://thegazette.co.uk/London/issue/38098/supplement/4865/data.pdf
- WIKI-NBG — https://en.wikipedia.org/wiki/Naval_Battle_of_Guadalcanal
- WIKI-SGFCS — https://en.wikipedia.org/wiki/Ship_gun_fire-control_system
- CF — http://www.combinedfleet.com/radar.htm
- SP — https://www.secretprojects.co.uk/threads/japanese-radar-type-designation-systems.37818/post-479930
- WW-JAP — https://weaponsandwarfare.com/?p=4504
- YAM-SAM — https://www.battleshipyamato.info/battle-off-samar
- NG-SPOT — https://navalgazing.net/Spotting
- NG-PHAL — https://www.navalgazing.net/Phalanx
- AT-OPT — https://admiraltytrilogy.com/pdf/FI2024_Development_of_Optical_Rangefinders.pdf
- RT-274 — https://www.radartutorial.eu/19.kartei/11.ancient3/karte029.en.html
- RT-262 — https://www.radartutorial.eu/19.kartei/11.ancient3/karte010.en.html
- RT-MK25 — https://www.radartutorial.eu/19.kartei/11.ancient5/karte043.en.html
- RT-WM20 — https://www.radartutorial.eu/19.kartei/11.ancient4/karte044.en.html
- WIKI-MK56 — https://en.wikipedia.org/wiki/Mark_56_Gun_Fire_Control_System
- WIKI-53 — https://en.wikipedia.org/wiki/AN/SPG-53
- FAS-68 — https://man.fas.org/dod-101/sys/ship/weaps/mk-68.htm
- FAS-GUN (Mk 86 GUNNO) — https://man.fas.org/dod-101/navy/docs/swos/gunno/INFO2.html
- GS-86 — https://www.globalsecurity.org/military/systems/ship/systems/mk-86.htm
- WIKI-SPQ9 — https://en.wikipedia.org/wiki/AN/SPQ-9
- FAS-92 — https://man.fas.org/dod-101/sys/ship/weaps/mk-92-fcs.htm
- WIKI-92 — https://en.wikipedia.org/wiki/Mark_92_Guided_Missile_Fire_Control_System
- JPROC (Mk 63 / Mk 34) — https://www.jproc.ca/haida/fcs.html
- HELIS (Orion RTN-10X) — https://www.helis.com/database/sys/159-fire-direction-radar-Selenia-Orion-RTN-10X/
- WIKI-347 — https://en.wikipedia.org/wiki/Type_347_radar
- LIROD — https://fcsorm.dyndns.org/fcsorm/images/showcase/optronics/lirod_mk2.pdf
- GS-1134 — https://www.globalsecurity.org/military/world/russia/1134-weapons.htm
- MP-OWL — https://www.militaryperiscope.com/weapons/electronics/naval-radars/owl-screech/overview/
- MP-KITE — https://www.militaryperiscope.com/weapons/electronics/naval-radars/mp-184-kite-screech/overview/
- WIKI-630 — https://en.wikipedia.org/wiki/AK-630
- WIKI-130 — https://en.wikipedia.org/wiki/AK-130
- KS-CLSA — https://koreascience.kr/article/CFKO198811919692800.pub

Gaps: Wikipedia pages for Type 284, Mk 3, Mk 8 and Mk 13, and the navweaps Japanese and post-war US radar pages, failed to fetch. No primary data were found for splash-pip duration, per-target detection ranges against periscopes, Type 903/904 accuracies, MR-104 Rys specifications, or post-war hit probabilities.
