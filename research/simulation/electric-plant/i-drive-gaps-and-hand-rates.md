# I. Warship turret drives 1890-1945: remaining gaps, hand fallback, gyros on power loss
Status: draft    Updated: 2026-11-04    Request: [REQUESTS.md, 2026-11-04](../../REQUESTS.md); an appendix of [electric-plant.md](../electric-plant.md)

Scope note: a gap-filling pass. Items already in earlier notes (USN New York/Nevada/Pennsylvania/Colorado motor lists, 5-in/38 ~13 kW, German WWI 30.5 cm, Bismarck 5.4/6 deg/s, Nelson 750 hp pumping engine, Yamato 500 hp swashplate, Jaureguiberry, Waterbury gear from Virginia) are not repeated except where a new number was added. About 20 tool calls; several primary manuals (OP 769, Fullam & Hart) were found but could not be read (the proxy refused the host, and the landing pages carry only tables of contents). Hand-drive rates remain the largest gap.

## USN 16-in Mk 6 (North Carolina) and Mk 7 (Iowa) turrets: motors per turret; first USN electric turrets

### Takeaway
Both 16-in turrets used a 300 hp electric training motor driving hydraulic (A-end/B-end) gear, rated 4 deg/s. Each gun had a 60 hp elevation motor (12 deg/s) and a 60 hp rammer. Mk 7 hoists are 75 hp (shell) and 100 hp (powder). Mk 6 hoists are 60 hp and 75 hp. Two 40 hp shell-ring motors serve each turret. The connected load comes to about 850-950 kW a turret. The first USN electric turrets (Kearsarge era, c.1900) used two 35 hp training motors on the Ward Leonard system for a turret of about 600 tons, either motor able to train it alone.

### Cited Findings
- Mk 7 (Iowa): "Training was by a 300 hp electric motor driving hydraulic gear." Each gun had "a 60 hp motor for elevation", "a 60 hp electric motor for ramming", "a 75 hp electric motor for the shell hoist" and "a 100 hp electric motor for the propellant hoist". "Each shell ring was powered by a 40 hp electric motor." Rates: train 4 deg/s, elevation 12 deg/s, loading angle +5 deg, 2 rounds per minute per gun. Turret weight 1,701-1,708 tons (not stated as revolving). Each hoist supplied 3 projectiles a minute. Recoil 0.43 s and run-out 0.90 s at +15 deg. Training arcs were cut in the 1980s (I ±139, II ±126, III ±128) "to avoid overpressure problems". — [NavWeaps 16"/50 Mark 7](http://www.navweaps.com/Weapons/WNUS_16-50_mk7.php)
- The 1957 USNA text gives the 16"/50 training motor as "300 hp, with an overload rating of 540 hp" driving an A-end through reduction gear, one A-end to two B-ends. Elevation is "a 60-horsepower electric motor driving a variable-displacement hydraulic pump (A-end)", at "approximately 12 degrees of arc per second". The rammer is "60 hp with an overload rating of 108 hp", ramming in "1.7 seconds" at just under 14 ft/s. The projectile hoist is 75 hp and the powder hoist 100 hp, with "Three projectiles per minute per hoist". On power loss the training brakes set: "When power is off, no pressure is in the hydraulic cylinders, and the springs set the brakes". The projectile hoist has "A solenoid brake ... to prevent motion of the hoist when there is a power failure." Local control is "by handwheels" (these set the A-end tilt; they are not a hand drive). Projectiles are moved by parbuckling with a power capstan. — [Naval Ordnance and Gunnery Vol. 1, ch. 7 Turret Installations, NavPers 10797-A (1957)](https://eugeneleeslover.com/USNAVY/CHAPTER-7-D.html)
- Mk 6 (North Carolina/South Dakota): "Training was by a 300 hp electric motor driving hydraulic gear." "Each gun had a 60 hp motor for elevation, a 60 hp motor for ramming, a 60 hp motor for shell hoist and a 75 hp motor for power hoist." "Each of the two shell rings used a 40 hp motor." Train 4 deg/s, elevation 12 deg/s, 2 rpm. Turret 1,403-1,437 tons. — [NavWeaps 16"/45 Mark 6](http://www.navweaps.com/Weapons/WNUS_16-45_mk6.php)
- A hydraulic fault, not a power loss: on Washington, "a ball check valve being jarred loose by the firing shock, causing a loss of hydraulic pressure for that gun." The page has nothing on South Dakota's 1942 electrical failure. — [NavWeaps 16"/45 Mark 6](http://www.navweaps.com/Weapons/WNUS_16-45_mk6.php)
- First USN electric turrets: Lt Harry George (AIEE, May 1902), "Electricity in the Navy". "Turrets are turned by motors operated on the Ward Leonard system", with one generating set per turret. "For 12-inch turrets, two 35 h. p. motors are installed ... either one being capable of handling the turret alone"; "The weight of the mass moved was about 600 tons". A full traverse took "less than 45 seconds". A trial moved the turret "ten successive times" in a total of 30.9 s. "Automatic cut-outs are fitted at the limits of travel". The controller is "operated by one handle" under the sighting hood. Elevating motors are about 5 hp. Kearsarge's load table lists "Training, elevating and rammer motors" at 171,580 W. A motor-generator per turret was "under consideration". — [George, "Electricity in the Navy", AIEE Trans. 1902, pp. 582, 608-609 (zenodo scan)](https://zenodo.org/records/2210124/files/article.pdf)
- Kearsarge class: 13-in guns in "Mark III turrets that were electrically trained" (Friedman 2011). After an April 1906 propellant fire caused by an electrical short aboard Kentucky, "most electrical equipment was removed from the ships' turrets." — [Wikipedia, Kearsarge-class battleship, citing Friedman 2011 pp. 152, 165-167](https://en.wikipedia.org/wiki/Kearsarge-class_battleship)
- The same Wikipedia page gives the 13-in rate as "one shot every 320 seconds" [UNCERTAIN: probably a transcription error in Wikipedia; check Friedman].

### Inferences
- [EST] Connected load per turret, all motors summed (not a simultaneous demand):
  - Mk 7: 300 + 3 × (60 + 60 + 75 + 100) + 2 × 40 = about 1,265 hp ≈ 945 kW. Assumes two shell rings, as on the Mk 6; the Mk 7 page does not state the count.
  - Mk 6: 300 + 3 × (60 + 60 + 60 + 75) + 2 × 40 = 1,145 hp ≈ 855 kW.
  - A realistic simultaneous demand in action is perhaps 40-60%: training plus elevation plus one loading step at a time.
- [EST] 4 deg/s on about 1,700 t from 300 hp. Against the Kearsarge figures (2 × 35 hp, only one needed, about 600 t, full traverse in under 45 s), installed training hp per revolving tonne rose from about 0.06 (one motor) to about 0.18 by 1940.
- [EST, UNCERTAIN OCR] The George trial suggests an average of about 6 deg/s over a 272 deg traverse. That is high for 1900 and is a peak motor-driven figure.
- Design-block rule: in USN electro-hydraulic turrets, power loss leaves the turret held by spring brakes and the hoists held by solenoid brakes, so it fails safe and immobile. Moving it needs hand gear (not documented here) or restored power.

### Gaps
- The Mk 7 and Mk 6 hand-training gear (handwheel turns per degree, men, achievable deg/s) is in OP 769 ch. 6 and ch. 16 (Auxiliary and Emergency), which I could not open: [OP 769 parts](https://www.eugeneleeslover.com/ENGINEERING/OP769/OP-769_Part4.pdf).
- Brooklyn CL 6-in and Baltimore 8-in turret motors were not found. The 8-in/55 three-gun turret is in [OP 1180](https://www.eugeneleeslover.com/ENGINEERING/OP1180/OP-1180_8-Inch_3-Gun_Turrets_Vol1.pdf), not read.
- Brooklyn (1896) electric turret specifics and Indiana-class steam turret rates were not found. Fullam & Hart's text-book (chapters on the 10-in Mk II, 12-in Mk IV and 8-in Mk XII turret mounts, pp. 182-203) was located but not read: [Fullam & Hart](https://www.eugeneleeslover.com/ENGINEERING/Fullam/Fullam_Hart_COMPLETE_Pg_188c-214.pdf).

## Russian, British, Italian, Japanese turret drives

### Takeaway
- Russian Borodino-class and Pattern 1895 12-in turrets were all-electric, on the French pattern: train 2.4-3.0 deg/s, elevation 2.2-2.7 deg/s, about 180 t a turret.
- The Gangut/Sevastopol 12-in triple was electric with hydraulic drive gear: train 3.2 deg/s, about 780 t.
- The British KGV 14-in used hydraulic swashplate training engines of 160 bhp (quad) and 70 bhp (twin), at 2 deg/s.
- The British 5.25-in mount had its own 80 bhp (160 peak) electric motor and oil pump on the mount, 10 deg/s, with hand gear that was hard to use.
- Littorio's 381 mm turrets were plain electric through reduction gears, 6 deg/s.

### Cited Findings
- Russian 12"/40 Pattern 1895: "Turrets were electrically powered and followed contemporary French designs." Turret weights: Tri Svyatitelya 173 t, Borodino 179 t (182 mt), Andrey Pervozvanny 218 t. Elevation 2.2-2.7 deg/s and training 2.4-3.0 deg/s. Rate of fire 0.5-0.7 rpm in the Russo-Japanese War, Borodino 1.5 rpm. Hoists ran in a single stage from magazine to breech. Motor hp and behaviour at Tsushima are not covered. — [NavWeaps Russian 12"/40 Pattern 1895](http://www.navweaps.com/Weapons/WNRussian_12-40_m1895.php)
- Russian 12"/52 (Gangut, MK-3-12): "Training and elevation were electrically powered with hydraulic drive gear." Train 3.2 deg/s. Elevation 3-4 deg/s, or 6 deg/s on Sevastopol after the 1940 modification. Turret 767.8 t (780 mt), 784 mt modified. Rate of fire 1.8 rpm (2.2 after 1940). — [NavWeaps Russian 12"/52 Pattern 1907](http://www.navweaps.com/Weapons/WNRussian_12-52_m1907.php)
- British 14"/45 Mk VII (KGV): "Training was by one of two swashplate engines, 160 bhp in the quads and 70 bhp in the twin." "Elevation was by hydraulic cylinder and piston." Run-out by compressed air. "The shell ring was driven by a 24 bhp hydraulic engine." Train 2 deg/s, elevation 8 deg/s, 2 rpm. Revolving weight 1,582 t (quad) and 915 t (twin). Reliability: Prince of Wales "fired only 55 out of a possible 74 shells" against Bismarck. On King George V "'A' turret was completely out of action for 30 minutes" from a jam between fixed and revolving structure in the shell room, and Y turret was out 7 min from drill errors. At North Cape, Duke of York had three Y-turret guns out about 15 min. The page does not say what drove the main hydraulic pumps. — [NavWeaps 14"/45 Mark VII](http://www.navweaps.com/Weapons/WNBR_14-45_mk7.php)
- British 5.25"/50 Mk I/II mount: "driven by a 80 bhp (peak 160 bhp) electric motor and oil hydraulic pump mounted on the revolving structure". Training by hydraulic motor with two worm and pinion drives, and a hydraulic motor per gun for elevation. 10 deg/s train and elevation on Mk I/II, 20 deg/s on RP10. 7-8 rpm in service against a design 10-12. Mount weight 77.5 t (Mk I) and 84-96 t (Mk II), including crew and ammunition. "Hand gears were provided for both elevation and training", but the mount was "difficult to train in the unpowered mode using the hand mechanisms." On Prince of Wales after she listed, "some of the mounts could not be trained to engage the succeeding attacks." "Following the loss of Prince of Wales, mountings on the surviving class members had duplicated power leads installed." — [NavWeaps 5.25"/50 QF Mark I](http://www.navweaps.com/Weapons/WNBR_525-50_mk1.php)
- Prince of Wales, 10 Dec 1941, report by Lt Cdr Skipwith: after the torpedo hit abreast P3/P4 at 1144, "Power failed at both after groups of 5.25 Inch guns and P 1 turret jammed in training: power failed at P.2." "Steering gear was affected and the ship was never again under complete control." "The warning telephone system failed." Both shafts stopped. — [Skipwith report, London Gazette supplement (fepow.family transcription)](https://fepow.family/Supplement/London_Gazette/Loss_of_Force-Z/html/page_7.htm)
- Italian 381 mm/50 (Littorio): "Elevation, training and hoisting was performed by electric motors working through reduction gears." The breech was hydro-pneumatic and the hoists were "electric winches". Train and elevation 6 deg/s, 1.3 rpm. Turret 1,570 t (1,595 mt). An auxiliary rammer was fitted "in case of damage to the primary rammer". — [NavWeaps 381 mm/50 Model 1934](http://www.navweaps.com/Weapons/WNIT_15-50_m1934.php)
- Yamato (additions to the earlier note): there were "two independent sets of training gear, only one of which was in use at a time", training at 2 deg/s. Elevation was 8 deg/s in practice (6 planned). Loading took about 30 s at the loading angle (+3 deg), about 35 s on average. "The total revolving weight of one turret being 2,510 metric tons" (O-45). Shells moved in the gunhouse "manually by overhead travelers and chain purchase." The page does not say what drove the hydraulic pumps. — [NavWeaps 46 cm/45 Type 94](http://www.navweaps.com/Weapons/WNJAP_18-45_t94.php)

### Inferences
- [EST] KGV 14-in quad: 160 bhp training on 1,582 t at 2 deg/s, a ratio of about 0.10 bhp/t. Yamato: 500 hp on 2,510 t at 2 deg/s, about 0.20 hp/t. Iowa: 300 hp on about 1,700 t at 4 deg/s, about 0.18 hp/t.
- [EST] A per-mount electric motor (5.25-in, 80 bhp ≈ 60 kW continuous, 120 kW peak, on about 78 t) dies when its power lead is cut. A central hydraulic ring main (RN heavy turrets) dies when the pumps or main are lost. Both fail to hand gear, and the 5.25-in experience suggests hand gear is close to useless with a list.
- [EST] The Russian Pattern 1895 turrets weighed about 180 t and trained at 2.4-3.0 deg/s. As with the USN 35 hp motors, these early electric turrets probably carried motors of a few tens of hp.

### Gaps
- Not found:
  - motor hp for the Tsesarevich/Borodino and Gangut turrets;
  - the drive of the KGV main hydraulic pumps (electric motor or steam), and their hp;
  - the British 6-in Mk XXIII cruiser turret drive;
  - Conte di Cavour (as rebuilt) and Tegetthoff drives;
  - Kongo/Nagato hydraulic pump drive (steam or electric);
  - Yamato's pump drive.
- The pages that are a natural next step: NavWeaps WNAH_12-45_skodaK10, WNIT_32-44_m1934, WNJAP_14-45_t41, WNJAP_16-45_t3, WNBR_6-50_mk23.

## Hand fallback, and drive machinery weight

### Takeaway
I found no number for men per crank, gear ratio or achieved hand deg/s, and no hand ramming or hand hoisting rate, for any heavy turret. The sources confirm only these points:
- USN 16-in turrets brake on power loss.
- The 16-in hoists hold on solenoid brakes.
- The RN 5.25-in had hand gear that was "difficult" to use and failed with a list.
- Yamato shells were moved in the gunhouse by chain purchase.

No drive machinery weights were found.

### Cited Findings
- 5.25-in hand gear was present but "difficult to train in the unpowered mode", and some mounts could not train after the list. — [NavWeaps 5.25"/50](http://www.navweaps.com/Weapons/WNBR_525-50_mk1.php)
- USN 16-in: spring brakes set when hydraulic pressure is lost, and a solenoid brake holds the projectile hoist on power failure. The local handwheels set the hydraulic A-end tilt, so they need power. — [Naval Ordnance and Gunnery 1957 ch. 7](https://eugeneleeslover.com/USNAVY/CHAPTER-7-D.html)
- Kearsarge: two training motors were fitted so that one could fail. — [George 1902](https://zenodo.org/records/2210124/files/article.pdf)
- Yamato: two independent training gears. — [NavWeaps 46 cm](http://www.navweaps.com/Weapons/WNJAP_18-45_t94.php)

### Inferences
- [EST] Redundancy was usually designed in as a second power drive rather than hand gear: twin training motors on Kearsarge, twin training gears on Yamato, two swashplate engines on KGV, and duplicated power leads on the 5.25-in after 1941.
- [EST] A game's hand fallback for heavy turrets (over about 500 t) should be near zero. For medium mounts (about 80 t) it should be very slow and fail under list.

### Gaps
- Men, gear ratios and deg/s by hand; hand elevation; hand ramming and hoisting rates; Jutland or South Dakota hand-working reports.
- Waterbury gear weight, RN pumping engine weight, and training motor weights as a share of revolving weight.
- Where to look: OP 769 ch. 6 and ch. 16; OP 1180; Fullam & Hart ch. XIV-XVI; RN gunnery handbooks. None was readable in this pass.

## Gyrocompass and stable-vertical run-down after power loss

### Takeaway
- The early Sperry compass ran its rotor at 8,000 rpm on "a few amperes" from a motor-generator set. It settled in about 2 hours from 90 deg off north, or a few minutes near north.
- The Admiralty Mk V gyro of the 1940s ran on 85 V, 150 Hz, three-phase, and took 4.5 hours to settle from power-up (about 1.5 h by a quick method, accurate to 1-2 deg).
- I found no source on rotor coast-down time or on how long the heading stays usable after power loss.

### Cited Findings
- Sperry, 1911: "The gyroscope is operated at a speed of 8000 revolutions per minute." "The electric power required is very small, only a few amperes." It runs from "a motor generator set, mounted on a common base". Near true north "it will adjust itself so as to point true north in a few minutes". From 90 deg off, "it will require about two hours to get settled down on true north." "Operating in vacuum materially reduces the power required." — [USNI Proceedings, Sept 1911, "Sperry Gyroscopic Compass for Naval Vessels"](https://www.usni.org/magazines/proceedings/1911/september/sperry-gyroscopic-compass-naval-vessels)
- HMCS Haida, Admiralty Mk V (1943-63): the gyro "runs on" 85 VAC, 150 Hz, three-phase (AP 1005 type). "After power up, it took four and a half hours to settle on true north." A quick method north-aligns "in about one and a half hours" to 1-2 deg. Follow-up accuracy is 2 minutes of arc, and later ships used 120 V 400 Hz. — [jproc.ca, HMCS Haida gyro compass](https://jproc.ca/haida/gyrocompass.html)

### Inferences
- [EST] A rotor at 6,000-20,000 rpm in a vacuum or low-friction case coasts for tens of minutes. Coasting is not the same as staying accurate: the follow-up and its repeaters stop when power goes, so the bridge loses a usable gyro heading at once even while the rotor spins. After restart, a sensitive element that has wandered needs hours to settle (2 h for the 1911 Sperry from 90 deg off, 4.5 h for the Mk V from cold). A short interruption, with the element still near the meridian, recovers in minutes.

### Gaps
- Not found:
  - coast-down minutes for any naval gyro;
  - Anschütz rotor speed and supply wattage;
  - Sperry rotor weight and wattage;
  - stable-vertical (Mk 6 stable element, RN gyro level) erection time after restart.
- Prince of Wales's report does not mention the gyro.
