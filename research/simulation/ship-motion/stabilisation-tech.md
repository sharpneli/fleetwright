# Fire-control stabilisation against ship motion (roll, pitch, yaw, deck tilt), 1900–1990
Status: draft    Updated: 2026-10-12    Request: -    Appendix to: [ship-motion-gunnery-research.md](ship-motion-gunnery-research.md)

Scope note: this covers how navies dealt with own-ship motion when laying guns, and the errors left over. Most primary US material came from Gene Slover's transcriptions of USN NAVPERS/OP texts on eugeneleeslover.com. Several key RN sources could not be read: hmshood.org.uk returned fetch errors, and the commsmuseum "Directors" RN handbook PDF was blocked by the proxy. Coverage of Germany and Japan is thin. Treat any number in the "Inferences" sections as derived, not sourced.

## Q1. Geometry: level, cross-level, trunnion tilt, deck tilt, parallax, yaw

### Takeaway
Deck tilt along the line of sight (level, L′) adds directly to elevation. Tilt across it (cross-level, Zh/Zd) tilts the trunnions and mostly produces a **deflection (train) error that grows roughly as sin(cross-level)·tan(elevation)**. USN texts call it "normally large and in the direction of the tilt", while the range error from cross-level is small. Trunnion tilt matters far more at high elevation: long-range surface fire, and above all AA fire.

### Cited Findings
- Definitions. Level angle L′ is "the angle between the horizontal and the deck plane, measured in the plane perpendicular to the deck through the line of sight". Cross-level Zh is measured about the axis where the horizontal plane meets the vertical plane through the LOS. — [NAVPERS Naval Ordnance & Gunnery ch.19 (Slover)](https://eugeneleeslover.com/USNAVY/CHAPTER-19-A.html)
- Level is "the deck tilt component along the line of sight; modifies elevation". Cross-level is the component across the LOS and "affects both train and elevation because it tilts the gun trunnions". Early systems used a cross-level telescope kept on the horizon. In modern systems this is only a standby method: it is "prone to human error" and unusable without a visible horizon. — [ch.19G (Slover)](https://eugeneleeslover.com/USNAVY/CHAPTER-19-G.html)
- "The deflection error resulting from trunnion tilt is normally large and in the direction of the tilt; the range error is small and usually reduces gun range." Equal and opposite train/elevation corrections do not work: the gun must be trained in the inclined deck plane and elevated perpendicular to it. "The only practicable method of correction for trunnion tilt is by mechanical means." — [ch.19 (Slover)](https://eugeneleeslover.com/USNAVY/CHAPTER-19-A.html); [ch.19G](https://eugeneleeslover.com/USNAVY/CHAPTER-19-G.html)
- USN gun-order equations with tilt correction: E′g = E′b + Vs + Vz and B′gr = B′r′ + Dd′, where Dd′ = Ds + Dz. Vz and Dz are the trunnion-tilt corrections in elevation and train, computed from L′, Zh, Vs and Ds supplied by Stable Vertical Mk 41. Deck-tilt correction jB′r′ converts director train in the deck plane to horizontal bearing. — [ch.19G](https://eugeneleeslover.com/USNAVY/CHAPTER-19-G.html)
- DP form: E′g = E + L + Vs − Vz, B′gr = B′r + Dd. Tilt corrections go into gun orders, not into Vs/Ds, "because of the rapidity with which they change". "Trunnion tilt corrections assume greater importance in AA fire than in surface fire, because of higher gun elevation." — [ch.24C (Slover)](https://eugeneleeslover.com/USNAVY/CHAPTER-24-C.html)
- Gimbal geometry differs between systems. The main-battery stable vertical measures L′ perpendicular to the deck and Zh about a horizontal axis, because main-battery director sights were *not* stabilised. The DP Stable Element reverses the gimbals, with cross-level Zd measured in the deck plane, because DP director sights *were* stabilised to keep the sighting plane vertical. "An unstabilized elevated LOS would introduce large errors." — [ch.24C](https://eugeneleeslover.com/USNAVY/CHAPTER-24-C.html)
- Exact geometry from a tilt-corrector patent (US2892384A). T = tilt of the mounting plane, GT = training in the tilted plane, GE = elevation relative to the tilted plane.
  - Level of gun: LG = arctan(tan T · sin GT).
  - Cross-level of gun: CLG = arcsin(sin T · cos GT).
  - Training error term: COB = arctan[sin CLG · tan(GE + LG)].
  - The corrector uses small-angle forms. The OCR is garbled in places; signs and the elevation equation (Eq. 11) are incomplete in the text.
  — [US2892384A](https://patents.google.com/patent/US2892384A/en)
- Wild-shot definition gives a scale for deflection errors: more than 10 mils off in deflection, or more than 3.5–4% of range off in range. One mil = 3.44 arcmin. — [Fire control definitions (navsource)](https://www.navsource.org/archives/01/57%20Excess%20pages/57t2.htm)
- Parallax: Scott's 1907 director compensated "for parallax, dip, turret roller paths not being level, and ship flexing". — [navalgazing Directors](https://www.navalgazing.net/Directors). Mk 37/Mk 1 used a 100-yd horizontal and 10-yd vertical parallax base. — [navsource 57o](https://www.navsource.org/archives/01/57o.htm). Turret roller-path tilt was corrected by a "roller path tilt compensator, driven from the turret train rack". — [USN Fire Control Installations ch.9 (ibiblio)](https://www.ibiblio.org/hyperwar/USN/ref/FireControlInst/FireControlInstsCh9.html)
- Yaw: Pollen (1905–06) added a gyroscope "to allow for the yaw of the firing ship". — [Wikipedia Fire-control system](https://en.wikipedia.org/wiki/Fire-control_system). In RN director firing the trainer corrected for yaw with the vertical crosshair, and guns fired when both wires were on. — [navalgazing Directors](https://www.navalgazing.net/Directors)
- A survey of the RN "Directors" handbook shows separate sections titled "Error in Laying & Training due to movement of the ship", "Cross-Levelling Gear", "Canted-Trunnion Error", "Methods of reducing the error – No corrector fitted", "Forecasting in Gyro Firing" and "Use of Gyro and Director Firing – Policy". Only the contents list was readable. — [commsmuseum RN gunnery directors PDF](https://www.commsmuseum.co.uk/dykes/gunnery/directors2.pdf)

### Inferences
- **Formula for modelling (INFERENCE, from patent Eq.3 geometry).** Uncorrected train error ≈ arctan(sin Z · tan E), where Z is cross-level and E is gun elevation. Elevation error ≈ E − arcsin(cos Z · sin E), which is small. Computed values:

| Cross-level Z | E=5° | E=10° | E=20° | E=30° | E=45° (AA) |
|---|---|---|---|---|---|
| 2° (train err, mil) | 3.1 | 6.2 | 12.7 | 20 | 35 |
| 5° | 7.6 | 15.4 | 32 | 50 | 87 |
| 10° | 15 | 31 | 63 | 100 | 172 |

Elevation (range) error stays under about 1.5 mil for Z ≤ 5° and E ≤ 20°.
- Implication: at long surface ranges (E of 15–30°), even 2–3° of uncorrected cross-level gives deflection errors of 10–30 mils. That is larger than a battleship's beam at 20 km (30 m at 20 km ≈ 1.5 mil). This is why every main-battery system either fired at a chosen point in the roll (where cross-level is near-constant and known) or corrected mechanically.
- Cross-level depends on target bearing: roll appears as cross-level for targets fore and aft, and pitch appears as cross-level for targets abeam. Broadside fire in a beam sea therefore turns roll mostly into *level* (range) error. Fire towards the bow or stern turns roll into *cross-level* (line) error.

### Gaps
- Could not access the NAVPERS 91900/OP 1140 images that hold the exact Vz/Dz equations (the Slover text renders them as images, "will not be further developed"). The geometry above is reconstructed from the patent.
- No RN text was readable on the canted-trunnion corrector, so its date of introduction is unknown.

## Q2. Royal Navy solutions

### Takeaway
The RN went from Scott's continuous aim (small guns, elevation only) to director firing on the roll. The director layer held the wire at a set point in the roll, ideally mid-roll, and fired the salvo by key. "Gyro firing" (gyro-timed firing at a chosen level) and cross-levelling gear came later, and full gyro stabilisation came first in AA (HACS roll corrector, Gyro Rate Unit 1940, Pom-Pom director). Interwar detail is poorly documented online.

### Cited Findings
- Scott's continuous aim compensated for roll, "but the main guns of a battleship could not elevate fast enough to use it". In director firing the layer set the crosshairs so the target passed through them at a set point in the roll and pressed the key. The trainer handled yaw. This "reduced the rate of fire". The roll point was first meant to be one end of the roll, "but the middle worked better because ship motion was more consistent." — [navalgazing Directors](https://www.navalgazing.net/Directors)
- 1912 trial: Thunderer (director) fired 39 rounds in 3.5 min, 26 of which would have hit. Orion (individual aim) fired 27 rounds for 4 hits. Eight battleships had directors by August 1914, and by Jutland only Erin and Agincourt lacked them. — [navalgazing Directors](https://www.navalgazing.net/Directors)
- Turret machinery limits: before B.VIII mountings, the layer "had to wait for the roll to bring the guns to the angle set", which reduced rate of fire. Elevation reached up to about 3°/s on paper and training about 2°/s. Dreadnought trials showed the hydraulics still could not achieve continuous aim. — [Navy General Board](https://www.navygeneralboard.com/?p=3893)
- HACS Mk I included a "stabilizing unit: roll corrector". Most HACS directors were not stabilised (hand-driven, undisturbed LOS). Henderson's gyro-stabilised HA director, with LOS and vertical gyros and cross-level follow-up using bang-bang air-puffer servos, was dropped by about April 1925. Post-1937 priorities included a "master stabilizing unit" and a "stabilized sight". — [navweaps tech-111](https://www.navweaps.com/index_tech/tech-111.php)
- The Gyro Rate Unit and Pom-Pom Director (both 1940) pioneered gyroscopic tachymetric control for medium- and short-range weapons. — [Wikipedia Ship gun FCS](https://en.wikipedia.org/wiki/Ship_gun_fire-control_system)
- A 1943 Admiralty order on Vickers gyros lists the Mk II gyro as "also fitted to roll correctors". A gyro losing speed shows "sluggish stabilisation". — [RAN Seapower 6194](https://seapower.navy.gov.au/sites/default/files/documents/6194.pdf)
- RCN Haida (Tribal, 1943–49): the director layer elevates the director and "fires the guns in director firing". The FKC system includes a "Gyro Level Corrector". The rangetaker can add ±0.5° of elevation. — [jproc Haida FCS](https://www.jproc.ca/haida/fcs_1943_1949.html)
- AFCT Mk I fitted to Nelson/Rodney in the early 1920s, Mk VII to the reconstructed QE class and Renown in the late 1930s, Mk IX to KGV, Mk X to Vanguard. The source does not say how motion was handled. — [Wikipedia AFCT](https://en.wikipedia.org/wiki/Admiralty_Fire_Control_Table)
- The RN lacked remote power control for main guns until late in the war. — [navweaps tech-052](https://navweaps.com/index_tech/tech-052.php)

### Inferences
- In a game model, RN main-battery fire 1912–c.1940 should be treated as **"director firing on the roll"**: a human layer times the shot. Residual error is then set by the layer's timing scatter times the roll rate at mid-roll, where the rate is highest. The 1900 USNI figures (Q6) suggest about 0.1 s of effective timing error. "Gyro firing" (automatic contacts at a chosen level) removes the reaction-time part. The "Forecasting in Gyro Firing" heading suggests the RN also had to anticipate the firing delay.

### Gaps
- No readable source gave the introduction date of RN main-battery gyro firing gear, cross-levelling gear or stabilised DCT sights, nor accuracy figures for them. The likely sources are Brooks, *Dreadnought Gunnery and the Battle of Jutland*; Friedman, *Naval Firepower*; and the commsmuseum RN handbook. None were accessible.
- Pollen/Argo gyro-stabilised rangefinder mount: only the yaw-gyro statement was found; no stabilisation figures.

## Q3. US Navy solutions

### Takeaway
USN main batteries used Stable Vertical Mk 41 (Mk 43 on South Dakota) in Plot. It fed continuous level and cross-level to the Mk 8 rangekeeper and offered two modes: **continuous fire** (fire at any point in the roll, orders continuously tilt-corrected) and **selected level fire** (guns laid for a preset level, typically mid-roll, and fired automatically by contacts with a firing-delay compensator). DP Mk 37 systems used Stable Element Mk 6, which stabilised director sights, rangefinder and radar in level and cross-level and erected faster (about 1 min).

### Cited Findings
- Continuous fire (normal seas): level and cross-level go continuously to the rangekeeper, and guns can be fired "at any point in the motion of the ship in roll and pitch". Selected level fire is used "when sea conditions make it difficult for the guns to stay on target". L′j is set by hand (or electrically from the director) and "usually chosen so the guns fire at about the middle of the ship's roll". Firing contacts close when L′ = L′j. A **firing-delay compensator** advances firing to offset ejection time so the projectile leaves at the selected level. Selected cross-level fire also existed. Continuous level was sent aloft to stabilise rangefinders and radar antennas. Mercury cut-out prevents gyro errors during high-rate manoeuvres. — [ch.20E Main-Battery Plotting Room (Slover)](https://eugeneleeslover.com/USNAVY/CHAPTER-20-E.html)
- Mk 41 rim weight about 30 lb, about 12,000 rpm reached in 30–60 min, reaches the vertical in 5 min or less, and an "At the Vertical" state is required before accurate firing. Stable Element (Mk 1A ships) rim weight about 10 lb, reaches vertical in about 1 min, and is "just as accurate". The author's era had "no rate-of-roll computation" in the computers. Early stable verticals had a trigger that fired at a set roll angle. — [navweaps tech-074 (veteran FC tech)](https://www.navweaps.com/index_tech/tech-074.php)
- Mercury-tank erection with the gyro housing rotating at 18 rpm; settles "after several minutes" from start; latitude weight compensates for Earth rate. — [ch.19G](https://eugeneleeslover.com/USNAVY/CHAPTER-19-G.html); [Wikipedia Ship gun FCS](https://en.wikipedia.org/wiki/Ship_gun_fire-control_system)
- Mk 37 / Stable Element Mk 6: Zd goes electrically to the director to stabilise telescopes, rangefinder and radar in cross-level. Cross-level limit switches stop the director's cross-level motor at **Zd = 17½°**. Level stabilisation of sights works only in AUTOMATIC elevation, not LOCAL/MANUAL. The computer takes L + Zd/30 (gearing compensation) and also selected level Lj and selected cross-level Zdj. — [ch.25A (Slover)](https://eugeneleeslover.com/USNAVY/CHAPTER-25-A.html)
- Gun-stabilising angles in the Mk 37 were "computed approximately because exact computation needed too many terms". — [Wikipedia Ship gun FCS](https://en.wikipedia.org/wiki/Ship_gun_fire-control_system)
- Mk 37 offered director salvo fire "at selected points of level", and fire "throughout the roll". At night or in poor visibility, level was controlled at the stable element. Against surface targets, a firing pointer could fire at set stopwatch intervals. An experienced crew held bearing and elevation within ±1–2° of optical (radar tracking). — [navsource 57o](https://www.navsource.org/archives/01/57o.htm)
- Pre-stable-vertical USN practice (c.1912–18): directors only levelled the guns. The operator centred on the target at mid-roll, a buzzer sounded at the top of the roll, and he fired on the downroll when the wires came on. — [navalgazing Directors](https://www.navalgazing.net/Directors)
- Backup: Gun Director Mk XIII in Plot had an element stabilised by two opposed gyros with an artificial horizon. The observer fired when the crosslines passed the horizon at the set director correction. — [USN FC Installations ch.9](https://www.ibiblio.org/hyperwar/USN/ref/FireControlInst/FireControlInstsCh9.html)
- Gear backlash shifts side to side as the ship rolls, and firing (especially salvos) makes this worse. — [navweaps tech-074](https://www.navweaps.com/index_tech/tech-074.php)
- The USN had RPC in both train and elevation; the Germans had it only in elevation; the Japanese effectively none (follow-the-pointer). — [navweaps tech-052](https://navweaps.com/index_tech/tech-052.php)

### Inferences
- Continuous-aim fire depends on RPC tracking (servo lag) plus tilt correction. Selected-level fire depends on contact timing plus how well the delay compensator matches the actual ignition-to-exit time. Selected-level fire is also slower: at most one salvo per roll cycle (about 10–16 s for a battleship), not a problem when the reload is about 30 s, but a constraint for fast-firing secondaries.

### Gaps
- No sourced accuracy specification (arcmin) for Mk 41 or Mk 6 verticals. No USN trial comparing continuous vs selected-level fire in heavy seas was found.

## Q4. Germany, Japan, Italy, France

### Takeaway
Germany fully stabilised its AA directors (the triaxial SL series, heavy and fragile). Main-battery fire relied on gyro-stabilised rangefinders/directors, with gun RPC in elevation only. Trials show alignment breaking down at about 4–6° of roll. Japan corrected roll and cross-roll by hand-follow computation at the Type 94 Hoiban but lacked a reliable gyro vertical. Nothing was found on Italy or France.

### Cited Findings
- Germany, SL (Stabilisierter Leitstand) AA directors: stabilised in pitch and roll so measurements are made in the horizontal plane. Yaw stabilisation from SL-1 on, but SL-4 tests (1936) found it insufficient for sharp turns. SL-1 also stabilised cant/tilt. Weights: SL-1 21 t, SL-6 46 t, SL-8 about 40 t including a 5 t ballast; the M42 replacement was 6 t. SL-1 was easily upset by wind or crew movement, and gyro drift required a horizon-check device. The SL family was judged a failure: too heavy, harmful to stability, and fragile (gimbals knocked out by weather or a small hit). Fitted: Leipzig class SL-1; Deutschland 2×SL-2; Scheer/Spee 3×SL-4; Hipper/Blücher 4×SL-6; Bismarck/Tirpitz/Prinz Eugen 4×SL-8. — [Wikipedia Stabilisierter Leitstand](https://en.wikipedia.org/wiki/Stabilisierter_Leitstand)
- Cardan-ring flak directors weighed about 40 t, were "very sensitive to underwater hits", and the 6 t triaxial replacement was never fitted at sea. — [Weapons and Warfare](https://weaponsandwarfare.com/?p=16211)
- Bismarck artillery trials report (1941): 38 cm turret alignment upper limit about **±3–4° of roll**. At about 4° roll, average alignment errors were about ±6/16° (with outliers ±10/16°), and realignment was "impossible at about 6°". The 3 m rangefinder's elevation stabilisation drifted at low speed during turns, up to 0.5/16°/s at 42°/min turn and 10 kn. Flak remote-control error band was about 25/16° on an artificial rolling rig. Lateral-lead oscillations moved the rangefinder about ±1°, enough to lose the target in a 1.5° field at 40×. Bearing telescopes leaked in moderate seas. — [kbismarck artillery testing report](https://kbismarck.com/artillery-testing-report.html)
- German servos drove guns in elevation only. — [Wikipedia Ship gun FCS](https://en.wikipedia.org/wiki/Ship_gun_fire-control_system); [navweaps tech-052](https://navweaps.com/index_tech/tech-052.php)
- Japan: the Type 94 Hoiban (director/main control station) computes corrections for "roll in the line of sight, cross roll for elevation and train, and parallax", with a dedicated operator's instrument. Orders went to the guns by follow-the-pointer. The IJN "lacked a gyro for an artificial horizon". Some ships had gyros manually followed at the director for night or obscured horizon. Japanese gyros "were neither accurate nor reliable", and errors grew as crews tired. — [navweaps tech-086](https://navweaps.com/index_tech/tech-086.php)
- Japan "lacked gyros to sense the horizon", relied on averaging optical rangefinders, and needed manual follow-up on Sokutekiban, Shagekiban and Hoiban. Kato's 1904 system on Asahi fired at a consistent point in the roll/pitch cycle. Pre-gyro turret artificial horizons were "glass of water" inclinometers, and a sensitive bridge inclinometer was preferred. — [Wikipedia Ship gun FCS](https://en.wikipedia.org/wiki/Ship_gun_fire-control_system)
- US fire control had "by far the most advanced stable vertical elements in the world"; Yamato had no RPC in train or elevation. — [combinedfleet b_fire](http://www.combinedfleet.com/b_fire.htm)

### Inferences
- In a model, Japanese main-battery fire is best treated as firing at a fixed roll point using a director/horizon reference, with the laying error multiplier rising faster with sea state than for the USN. Germany sits in between: well-stabilised optics and rangefinders, but turret alignment degrading sharply above about 4° of roll.

### Gaps
- Could not confirm a German main-battery "Schwingungs…" firing gear, or that German main batteries fired at a level point (likely, but unsourced). Could not access the USNTMJ O-series fire-control reports. No information found on Italy or France.

## Q5. Post-war (1945–1990)

### Takeaway
Post-war USN directors stabilised the line of sight with director-mounted gyro packages (Mk 56: vertical + rate gyros, measuring true elevation and cross-traverse). Mk 68 was stabilised in cross-level in a yoke. Radar autotrack gave about 0.5 mil pointing. Residual errors moved from laying to tracking and servo, but no sea-state-dependent figures were found.

### Cited Findings
- Mk 56: LOS stabilised by a vertical gyro plus rate gyro at the director rear, measuring true elevation E and cross-traverse Zs. Automatic radar tracking "within one-half mil". Solution time about 2 s. Dead time settable 2–6 s. Coast mode up to about 2 min. — [ch.26E (Slover)](https://eugeneleeslover.com/USNAVY/CHAPTER-26-E.html)
- Mk 63: a cross-roll gyro measures cross-roll. Torque motors correct the lag of gyro damping when deck inclination swaps elevation and train functions. Mk 34 radar pointing within 1 mil, range ±15 yd + 0.1%. — [ch.26E](https://eugeneleeslover.com/USNAVY/CHAPTER-26-E.html)
- Mk 68: manned director in a large yoke, stabilised in cross-level about the yoke pivot. Gyro stabilisation unit; Mk 47 computer; digital upgrade 1975–85. — [Wikipedia Ship gun FCS](https://en.wikipedia.org/wiki/Ship_gun_fire-control_system)
- OTO 76 mm Compact drives: elevation 35°/s and training 60°/s, both at 72°/s² acceleration. — [Wikipedia OTO 76](https://en.wikipedia.org/wiki/OTO_Melara_76_mm)

### Inferences
- Drive rates of 35–60°/s with 72°/s² acceleration far exceed peak roll rates (about 2–7°/s, see Q6). Post-1960 mounts can therefore follow ship motion continuously, so unstabilised laying error effectively vanishes. The residual is roughly the 0.5–1 mil tracking error plus the computed-stabilisation approximation.

### Gaps
- No WM-20, NA-10, Mk 86 or Mk 68 stabilisation accuracy figures found. No trial data comparing sea states.

## Q6. Quantitative residual errors vs sea state; rate of fire in heavy seas; rangefinders in a seaway

### Takeaway
The dominant unstabilised error is firing-time scatter times roll rate. An 1900 USN estimate is 0.1 s at 2°/s, about 3.5 mil (about 21 ft at 2000 yd), and up to 0.3 s untrained (about 10 mil). Fixed-angle loading (+3° Yamato, +5° Iowa) and waiting for the roll both lengthen the firing interval. Rangefinders and directors lose the target as the field of view becomes small relative to unstabilised motion.

### Cited Findings
- USNI 1900, "Errors of gun fire at sea" (2000 yd, 12-in): about 1/50 s electric firing lag, allowed as 3 ft vertical error. Reaction interval about 3/10 s, giving an uncorrected roll error of "some 63 feet". Assuming an uncorrected 1/10 s at an average roll rate of 2°/s, a corrected mean vertical error of about 21 ft is expected from average gunners. Rolling and pitching together give a mean lateral and vertical error of about 3 ft each. Fire on the downroll when the lower edge of the wire reaches the enemy's freeboard. — [USNI Proceedings Oct 1900](https://www.usni.org/magazines/proceedings/1900/october/errors-gun-fire-sea)
- Loading at a fixed angle: Yamato's Type 94 loads at +3°. The cycle is about 30 s at the loading angle; elevating to 41° and back adds about 11 s, and about 35 s is typical at battle elevations. — [navweaps 46 cm Type 94](https://navweaps.com/Weapons/WNJAP_18-45_t94.php). Iowa 16"/50 loads at +5° and elevates at 12°/s; the gun automatically returns to the loading position on runout. — [navweaps 16"/50 Mk 7](https://navweaps.com/Weapons/WNUS_16-50_mk7.php). KGV 14in "probably +5°" (the cited page is the 15"/45 Mk II, data "sketchy"). — [navweaps 15"/45 Mk II](https://www.navweaps.com/Weapons/WNBR_15-45_mk2.php)
- Early RN barbettes loaded only at a fixed elevation *and training*. B.VIII introduced all-angle loading. Waiting for the roll reduced the rate of fire. — [Navy General Board](https://www.navygeneralboard.com/?p=3893)
- Bismarck trials: ammunition transport and casing ejection could not be tested in a seaway. Main-elevator supply was about 23–25 rounds/min for all four turrets. — [kbismarck trials](https://kbismarck.com/artillery-testing-report.html)
- Rangefinders: Mk 41 continuous level stabilised aloft rangefinders and radar ([ch.20E](https://eugeneleeslover.com/USNAVY/CHAPTER-20-E.html)). The Mk 37 rangefinder axis was kept horizontal by cross-level, and the rangefinder's own inertia kept it near horizontal ([Wikipedia](https://en.wikipedia.org/wiki/Ship_gun_fire-control_system)). German rangefinder lateral oscillation of ±1° in a 1.5° field at 40× could lose the target ([kbismarck](https://kbismarck.com/artillery-testing-report.html)). At Denmark Strait spray coated the forward turrets' optics, forcing use of main directors ([navweaps tech-052](https://navweaps.com/index_tech/tech-052.php)).

### Inferences
- Peak roll rate for sinusoidal roll = 2π·A/T. Computed values:
  - 3° amplitude, 10 s period (about SS3): 1.9°/s, so a 0.1 s timing error ≈ 3.3 mil.
  - 5°/12 s: 2.6°/s → 4.6 mil.
  - 10°/14 s (about SS6): 4.5°/s → 7.8 mil.
  - 15°/14 s: 6.7°/s → 11.7 mil.

  These are vertical errors for broadside fire. For bow/stern bearings the same angle becomes cross-level, amplified by tan(E) into deflection.
- Suggested model shape (INFERENCE):
  - Unstabilised human-timed laying: σ ≈ √(base² + (k·τ·ω_roll)²), with τ ≈ 0.1 s for a trained director layer and 0.3 s for untrained.
  - Selected-level automatic firing: τ is replaced by contact and delay-compensator mismatch. This is unsourced; perhaps 0.02–0.05 s.
  - Continuous-aim RPC: residual set by servo lag and stable-vertical error.

  Because error grows with roll *rate* (amplitude/period), it rises roughly linearly with sea state. The existing multiplier ×(1 + 0.25·(SS − 3)) is consistent in shape. Its slope should be steeper for unstabilised human-timed fire and near zero for fully stabilised post-war systems.
- Fixed-angle loading costs roughly (elevation difference)/(elevation rate) twice per cycle. Waiting for the roll adds on average half a roll period (about 5–7 s) when the loading point and firing point are out of phase.

### Gaps
- No sourced arcmin accuracy for any stable vertical. No sourced firing-contact timing error. No WWII trial directly comparing stabilised and unstabilised hit rates by sea state. No sourced figure for the sea state or roll angle at which navies ceased main-battery fire.
