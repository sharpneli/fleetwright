# 09 — Emergency and "MK1 Eyeball" Gunnery, 1914–1990
Status: final    Updated: 2026-10-09    Request: -

Scope: how guns were fired once the director or computer was lost, or was never fitted. Covers local control, night melee by eye, open and emergency sights, light weapons by tracer, human factors, and point-blank geometry.
Tags: [INFERRED] marks my own derivation or a design suggestion. [UNCERTAIN] marks thin or conflicting sources. Everything else is paraphrased from the URL beside it.

---

## 0. Key numbers for the sim

| Parameter | Value | Basis |
|---|---|---|
| Director vs individual (local) aim, 1912 RN trial | 39 rds, about 26 hits (≈67%) vs 27 rds, about 4 hits (≈15%) in 3.5 min. Roughly 4.5× hit rate, 1.4× rate of fire, about 6–7× hits per minute | [navalgazing Directors](https://www.navalgazing.net/Directors) |
| Same trial repeated in good visibility (4 Dec 1912) | Orion (individual aim) did much better and apparently beat Thunderer | [Wikipedia HMS Thunderer](https://en.wikipedia.org/wiki/HMS_Thunderer_(1911)) |
| Turret local-control computer (USN BB) | Aux Computer Mk 6 in turret booth. No ship-motion or own-ship inputs. Less accurate than Rangekeeper Mk 8. Deflection phoned to sight setter | [Slover Ch.20-F](https://eugeneleeslover.com/USNAVY/CHAPTER-20-F.html) |
| Turret RF (USN 16" BB) | 26.5 ft base, 25×. Stereo in T2/T3, coincidence in T1 | [Slover Ch.20](https://www.eugeneleeslover.com/USNAVY/CHAPTER-20.php) |
| Turret RF (RN 15") | 15 ft MG8 in gunhouse originally, later 30 ft MG14 on B/X. Vanguard 24.5 ft | [navweaps 15"/42](http://www.navweaps.com/Weapons/WNBR_15-42_mk1.php) |
| Turret RF (Bismarck 38 cm) | 10.5 m in all four turrets | [navweaps 38cm](http://www.navweaps.com/Weapons/WNGER_15-52_skc34.php) |
| Hand drive, 5"/38 Mk 21 | 5° elevation and 10° train per handwheel turn | [navweaps 5"/38](http://www.navweaps.com/Weapons/WNUS_5-38_mk12.php) |
| Power drive for comparison | 5"/38 base-ring mounts 15°/s elev, 25–34°/s train. 16"/50 turret 12°/s elev, 4°/s train. RN 15" turret 2°/s train. Bismarck 5°/s train | navweaps pages cited |
| 20 mm human range judgement | Gunners could judge distance only to about 400 yd | [USN History blog, Oerlikon](https://usnhistory.navylive.dodlive.mil/Recent/Article-View/Article/2686834/the-oerlikon-20-mm-the-right-tool-for-the-job) |
| .50 cal / .30 cal boat MG vs surface | Max effective about 1,100 / 700 yd. Hit chance poor beyond 600 yd | [Transport Doctrine Ch.XXV](https://ibiblio.org/hyperwar/USN/ref/Transport/transport-25.html) |
| Manual vs stabilized 25 mm (Mk 38 Mod 1 vs Mod 2) | Mod 2 has 2–3× Ph and kills a maneuvering boat at more than 2× range | [navweaps Mk 38](https://www.navweaps.com/Weapons/WNUS_25mm_mk38.php) |
| Searchlight useful range (WWII USN) | About 2,500 yd | [Transport Doctrine Ch.XXV](https://ibiblio.org/hyperwar/USN/ref/Transport/transport-25.html) |

---

## 1. Local control when the director is lost

### 1.1 Mechanics (USN WWII, generalizable)
- **Turret local control** ([Slover 20-F](https://eugeneleeslover.com/USNAVY/CHAPTER-20-F.html)):
  1. The turret officer finds the target with the periscope and trains the turret until the target is in the sights.
  2. Range comes from the turret rangefinder. Train comes from the turret train indicator. Both are entered by hand into the Aux Computer Mk 6.
  3. Sight deflection is phoned to the sight setter.
  4. Pointers and trainer lay continuously on the target with their own telescopes, which can be deflected 110 mils right or 90 mils left.
- **Backup chain**, from the same source:
  - Hand cranks for train and elevation if the motor is lost.
  - Hand firing key if the automatic firing contacts fail.
  - Percussion firing if electric firing fails.
  - An auxiliary switchboard in the after gyro room if the main IC switchboard is lost.
  - "Selected level" firing (fire at a fixed point in the roll) using hand follow-ups and galvanometers.
- **Loss of the stable element:** the turret or mount is aimed with the ship moving. Pointers fire on the roll, or at a chosen roll angle (selected level). In LOCAL or MANUAL modes, automatic level stabilization is impossible ([Slover Ch.25](https://www.eugeneleeslover.com/USNAVY/CHAPTER-25.php)).
- **Manual (no-power) drive** is described as "slow and arduous", used only when the power drive fails ([Slover Ch.25](https://www.eugeneleeslover.com/USNAVY/CHAPTER-25.php)). Turret manual drive is "much slower" ([Slover 20-F](https://eugeneleeslover.com/USNAVY/CHAPTER-20-F.html)).
  - No published hand-training rate for 14–16" turrets was found. [UNCERTAIN]
  - [INFERRED] Assume about 0.1–0.3°/s by hand crank for a heavy turret, an order of magnitude below power. That is adequate only for a nearly steady bearing.
- **List kills training:** Renown's 15" training engines failed at lists well below design. Resolution could not train her turrets at a 12° list in 1940 ([navweaps 15"/42](http://www.navweaps.com/Weapons/WNBR_15-42_mk1.php)).
- **5"/38 mount:**
  - In local control the mount captain directs the sight setter. Pointer and trainer lay through hooded telescopes ([navweaps crews](https://navweaps.com/Weapons/WNUS_5-38_mk12_Crews.php), [okieboat](https://okieboat.com/Gun%20mount.html)).
  - Hydraulic failure gives "Manual" mode, which is very slow. The Mk 21 mount had prominent open sights. The Mk 37 single mount had a simplified sight set for local control only ([navweaps 5"/38](http://www.navweaps.com/Weapons/WNUS_5-38_mk12.php)).
  - Doctrine allowed open ring sights on 5" mounts against aircraft if the telescopes could not be used ([Transport Ch.XXV](https://ibiblio.org/hyperwar/USN/ref/Transport/transport-25.html)).
- **Local surface-fire procedure for 3"/5"** ([Transport Ch.XXV](https://ibiblio.org/hyperwar/USN/ref/Transport/transport-25.html)):
  - Range from radar, rangefinder, or dip curves.
  - Fire salvos and spot each one by halving: ladder in steps of 500 yd or more, halve the step after crossing, then go to rapid fire inside a 100 yd bracket.
  - [INFERRED] This means about 3–5 spotting salvos before the first likely hit, against 1–2 under director/radar control at the same range.

### 1.2 Combat cases

| Case | What was lost | Result | Source |
|---|---|---|---|
| Bismarck, 27 May 1941 | 0902: foretop RF disabled. About 0908: forward RF and A/B turrets out, control shifts aft. Aft station fires about 4 salvos at KGV, then its cupola is destroyed (about 0913). C/D turrets go to local control against Rodney | Before 0902, her 2nd salvo straddled Rodney. After the shift, fire is described as erratic and sporadic, with no British hits recorded. D turret wrecked by its own shell (barrel burst) at 0921. Last salvo from C at 0931 | [kbismarck](https://kbismarck.com/bismarck-last-battle.html), [Wikipedia](https://en.wikipedia.org/wiki/Last_battle_of_the_battleship_Bismarck), [navweaps tech-016](https://navweaps.com/index_tech/tech-016.php) |
| Bismarck context | No steering. Broaching and pitching in a heavy sea | [INFERRED] Local control on an unstabilized, rolling, uncontrolled platform had almost zero effect | [USNI 1991](https://www.usni.org/magazines/proceedings/1991/june/who-sank-bismarck) |
| British fire on Bismarck | Rodney fired under **optical control throughout**: 375×16", 716×6". KGV fired 339×14", 660×5.25", and her radar failed at 0913. Norfolk 527×8", Dorsetshire 254×8" | About 40 heavy hits each from Rodney and KGV, about 300–400 hits in all from 2,876 shells (about 10–14%). Range closed to 2,500–4,000 m, "virtually impossible to miss" | [navweaps tech-016](https://navweaps.com/index_tech/tech-016.php), [kbismarck](https://kbismarck.com/bismarck-last-battle.html), [USNI](https://www.usni.org/magazines/proceedings/1991/june/who-sank-bismarck) |
| Scharnhorst, 26 Dec 1943 | Early hit destroyed forward Seetakt radar controls. She was "virtually blind" in snow. A/B turrets were knocked out at 16:48, and B was later restored | Gunners aimed at British muzzle flashes, which was harder against cruisers using flashless propellant. Duke of York: 52 radar salvos, 31 straddles | [Wikipedia North Cape](https://en.wikipedia.org/wiki/Battle_of_the_North_Cape) |
| USS Hoel, Samar, 25 Oct 1944 | Director and FC radar shot away | Guns 1–2 went to local telescopic control and were "very accurate". The report says local control practice "cannot be stressed too strongly". Director salvos 4–5 hit, 1–3 short | [Leyte Battle Experience](https://www.ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.3.html) |
| USS Johnston, Samar | A Yamato hit aft cut power to all 5" guns. About 10 min of damage control restored power to mounts 1–2 | [UNCERTAIN] Johnston is credited with about 45 hits on Kumano from 200+ rounds at about 18,000 yd in 5 min, before the power loss. The figure is probably inflated | [Wikipedia Johnston](https://en.wikipedia.org/wiki/USS_Johnston_(DD-557)) |
| USS Samuel B. Roberts, Samar | Gun 2 lost its gas ejection | One 5" gun fired 300+ rounds and scored at least 40 hits in about 50 min. Then 6 rounds were hand-rammed without gas ejection. The 7th cooked off and killed all but 3 of the crew | [de413.org](https://de413.org/category/ships-history/) |
| South Dakota, 14–15 Nov 1942 | 0033: firing turret III astern welded a bus-transfer contactor, and the after half of the ship lost power for about 1 min. Later, enemy hits overloaded the IC switchboard and cut **all FC and IC circuits for about 3 min**. All radars except Director II's were destroyed. SG radar was down 0041–0046 | Illuminated by 4 searchlights at about 5,800 yd. Took 26–27 hits (18×8"). Withdrew because night-fighting ability was "seriously impaired" | [WDR-57](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-south-dakota-bb57-war-damage-report-no57.html), [Wikipedia](https://en.wikipedia.org/wiki/Naval_Battle_of_Guadalcanal) |
| Kirishima (same night) | Under Washington's fire at about 8,400 yd | Washington fired 75×16" in about 7 min, with about 8–20 hits (≈11–27%). Also 107×5" (17–40 claimed hits) and 62 starshell. Hits were observed **optically**, with no radar splashes. Kirishima fired from 3 turrets during the check-fire, then only turret 4 | [Lundgren/navweaps](https://navweaps.com/index_lundgren/kirishimaDamageAnalysis.php) |
| Aoba, Cape Esperance | Up to 40 hits destroyed 2 turrets and **main director**. Gotō killed | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Cape_Esperance) |
| Jutland, HMS St Vincent | A hit cut wiring to the after turret, which then trained on its auxiliary motor | Fired 96–98 rds at about 9,500–10,000 yd, with 2 hits on Seydlitz. Captain called the director "invaluable" | [jutland1916 St Vincent](https://www.jutland1916.com/?p=15464) |
| Jutland, HMS Malaya | Shock jammed B turret's main cage | Right gun hand-loaded by the secondary method, described as "very slow" | [Malaya reports](https://www.jutlandcrewlists.org/reports-from-malaya) |
| Jutland, Erin and Agincourt | No director fitted | Fought in individual/turret control | [navalgazing](https://www.navalgazing.net/Directors) |

- **No source found gives a WWII "local control = X% of director" figure.** [UNCERTAIN]
- [INFERRED] The best quantified anchor is the 1912 trial: about 0.2× hit rate and about 0.15× hits per minute in poor visibility and smoke, and roughly parity in clear calm weather.
- [INFERRED] A sim multiplier on director hit probability:
  - Clear day, at or under about 8 kyd: about 0.5.
  - Haze/smoke, or long range: about 0.15–0.25.
  - Ship rolling heavily with stable element lost: a further ×0.5.

---

## 2. Night melee by eye at short range

| Action | Ranges | Illumination/aiming | Outcome numbers | Source |
|---|---|---|---|---|
| Matapan, 28 Mar 1941 | Opened at 2,900–4,000 yd (one account says the director layer "saw the target" at about 3,800 yd) | Destroyer searchlights. The Italians were caught with turrets trained fore and aft | Warspite's first salvo hit. Fiume took 3 × 15" broadsides in under 5 min. Cruisers blazing in about 5 min | [USNI 1995](https://www.usni.org/magazines/naval-history-magazine/1995/june/cape-matapan), [ahoy](https://ahoy.tk-jk.net/macslog/BattleofMatapan.html), [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Cape_Matapan) |
| Savo Island, 9 Aug 1942 | IJN lookouts sighted at 12.5–16 km. Gunfire at a few thousand yd | Floatplane flares plus searchlights at 01:50. Allied targets silhouetted by a burning transport | Canberra took up to 24 heavy hits and never fired. Vincennes took up to 74. Allied return fire: about 2 hits on Chōkai, 1 on Kinugasa | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Savo_Island) |
| Cape Esperance, 11–12 Oct 1942 | About 5,000 yd at sighting | US cruisers shot by radar/optics. Boise and Salt Lake City lit searchlights and were hit at once. Boise's magazine was hit at 00:10 | Aoba up to 40 hits. Firing stopped by mistaken IFF concerns | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Cape_Esperance) |
| Guadalcanal I, 13 Nov 1942 | 20 ft (Laffey under Hiei's bow) to 7,000 yd. Typical 1,000–3,000 yd. SF vs Hiei at 2,500 yd | Akatsuki and Hiei searchlights at 01:48. Akatsuki drew fire from at least 6 ships and sank by 01:55 | Hiei took 85+ shell hits, including about 30 × 8" from SF and Portland. Hiei could not depress enough for destroyers alongside. SF fired 2 full salvos into Atlanta (19 × 8") | [H-gram 012](https://www.history.navy.mil/about-us/leadership/director/directors-corner/h-grams/h-gram-012/h-012-1.html), [Wikipedia Hiei](https://en.wikipedia.org/wiki/Japanese_battleship_Hiei), [Wikipedia](https://en.wikipedia.org/wiki/Naval_Battle_of_Guadalcanal) |
| Guadalcanal II, 14–15 Nov 1942 | About 5,800–8,400 yd | Atago's searchlights at about 01:00 cued Washington. Searchlight mantelets burned | See §1.2 | [Lundgren](https://navweaps.com/index_lundgren/kirishimaDamageAnalysis.php) |
| Tassafaronga, 30 Nov 1942 | US opened at 8,700–11,500 yd by radar. Visibility about 2 nm | Starshell often hidden by smoke and oil fires. Gun flash and smoke blinded spotters. Flares arrived late | US gunfire sank 1–2 DDs (Takanami certain). No Japanese shell hit a US ship. Torpedoes decided it | [hyperwar Combat Narrative](https://ibiblio.org/hyperwar/USN/USN-CN-Tassafaronga/index.html), [H-gram 013](https://www.history.navy.mil/about-us/leadership/director/directors-corner/h-grams/h-gram-013/h-013-1.html) |
| 1st Narvik, 10 Apr 1940 | Snowstorm visibility 100–1,000 m | Close-range eyeball destroyer fight | Roeder took 5 hits, Lüdemann 2. The German DDs were left with about half their ammunition | [Wikipedia Narvik](https://en.wikipedia.org/wiki/Naval_battles_of_Narvik) |
| North Cape, 1943 | About 10–12 kyd | Belfast starshell "like a chandelier". Scharnhorst aimed at gun flashes | See §1.2 | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_North_Cape) |
| Jutland night, 1916 | Torpedo shots at 700–1,500 yd. Gunfire at similar ranges | Searchlights and gun flash made observation "nearly impossible". Ships showing searchlights took heavy casualties | — | [Brooks BJMH 2017](https://bjmh.gold.ac.uk/index.php/bjmh/article/download/757/879/980) |
| Falklands, 10–11 May 1982 | Not published | Alacrity illuminated Isla de los Estados with 4.5" starshell, then shot her | "A number of hits", target exploded. The only ship-vs-ship surface gun action of the war | [naval-history.net](https://naval-history.net/F38opsweek7.htm) |

**Patterns for the sim:**
1. **Illuminating means being hit.** Every searchlight user in these actions drew concentrated fire within seconds to minutes: Akatsuki, Hiei, Boise, Atago, and the Jutland cruisers. [INFERRED] Treat a lit searchlight as making its owner the highest-priority, easiest target, with near-day hit probability against it.
2. **Night eyeball ranges are 1,000–4,000 yd.** Japanese 1942 lookouts with large binoculars made first sightings at 9–16 km. [UNCERTAIN on the exact optics]
3. **Hits pile up on whatever is visible.** Hiei 85+, Canberra 24, Vincennes 74, Aoba 40. Unlit ships in the dark are largely spared.
4. **Identification failures are as important as accuracy.** Examples: SF shooting Atlanta, Scott's cease-fire at Esperance, Monssen hit after switching on her recognition lights.
5. **Friendly gun flash and smoke blind the firer** (Tassafaronga). Starshell is unreliable: Chicago's starshell failed at Savo, and smoke defeated it at Tassafaronga.

---

## 3. Open, ring, and emergency sights on medium and big guns

- **5"/38:**
  - Pointer and trainer telescopes are the normal local sights.
  - Mk 21 mounts had prominent open sights.
  - Open ring sights were authorized as the fallback when telescopes could not be used ([navweaps](http://www.navweaps.com/Weapons/WNUS_5-38_mk12.php), [Transport Ch.XXV](https://ibiblio.org/hyperwar/USN/ref/Transport/transport-25.html)).
  - Telescope magnification was not found. [UNCERTAIN] Typical WWII values were about 4–6× for mount sights.
- **Big-gun turrets:** pointer, trainer, and checker telescopes plus the turret officer's periscope ([Slover 20-F](https://eugeneleeslover.com/USNAVY/CHAPTER-20-F.html)). No evidence of true open sights on post-1918 heavy turrets. [UNCERTAIN]
- **5"/54 Mk 42 (1953+):**
  - Two "frog-eye" local control domes: right for AA, left for surface.
  - The AA dome was removed from many mounts in the 1960s–70s because local control against jets was judged "nearly impossible". The surface local-control dome was kept ([navweaps Mk 42](https://navweaps.com/Weapons/WNUS_5-54_mk42.php)).
- **Rate of fire in degraded modes:**
  - 5"/38 normal: 15–22 rpm. HMS Delhi trials: 25 rpm from ready-use ammunition vs 15 from magazine supply ([navweaps](http://www.navweaps.com/Weapons/WNUS_5-38_mk12.php)).
  - Hand breech operation and hand ramming are possible but slow and risky. Samuel B. Roberts lost a crew on the 7th hand-rammed round without gas ejection ([de413](https://de413.org/category/ships-history/)).
  - Turret secondary loading on Malaya was "very slow" ([Malaya](https://www.jutlandcrewlists.org/reports-from-malaya)).
  - [INFERRED] Hand-loaded or hand-rammed heavy guns run at about 1/3–1/2 of normal rate. Medium guns on manual drive run at about 1/2 rate, and train slowly.

---

## 4. Light weapons by eye

| Weapon/sight | Data | Source |
|---|---|---|
| 20 mm Oerlikon, open ring sight (to 1942) | 450–480 rpm, 60-rd drum. Barrel change after about 240 rds sustained. Effective range 1,500 m to 2,000 yd. Aimed by tracer "like a stream of water". Range judgement only to about 400 yd | [USS Slater](https://ussslater.org/20-mm), [USN History](https://usnhistory.navylive.dodlive.mil/Recent/Article-View/Article/2686834/the-oerlikon-20-mm-the-right-tool-for-the-job) |
| Mk 14 gyro sight | Max sight range: 20 mm 2,000 yd, 1.1" 2,800, 40 mm 3,200. Reticle circle 25 mils (20 mm) or 15 mils (Mk 51). Warm-up 30 min, or 10 min in emergency with "considerably" reduced accuracy. Track 1–2 s before firing. **Fallback in smoke/blast is tracer control**. Surface use not addressed | [hyperwar Mk 14 OP](https://ibiblio.org/hyperwar/USN/ref/Ordnance/GS-Mk14/index.html) |
| Doctrine | 20 mm local or tracer control only if the Mk 14 is knocked out. 40 mm uses its ring sight in local control when the director is down | [Transport Ch.XXV](https://ibiblio.org/hyperwar/USN/ref/Transport/transport-25.html) |
| Mk 14 effect | Kamikaze phase: 20/40 mm with the Mk 14 accounted for 78.6% of suicide planes downed by ship AA. About 85,000 sights built. About 14,000 Mk 51 directors | [USNI 2013](https://www.usni.org/magazines/naval-history-magazine/2013/november/shoebox-transformed-antiaircraft-fire-control) |
| 40 mm Mk 51 | 120 rpm. Range input fixed at about 5,000 yd. Tracers seen in the sight for lead/lag correction. The 3"/50 had no tracer, so it was harder to correct | [navweaps tech-049](https://navweaps.com/index_tech/tech-049.php) |
| Early-war AA by eye | 1.1"/20 mm fire "erratic, behind and low". Some ships opened at 4–5 kyd. Coral Sea expenditure per gun: 1.1" 180, 20 mm 330, .50 880 | [CINCPAC Gunnery Bulletin 2-42](https://ibiblio.org/hyperwar/USN/rep/CINCPAC/GunBull/2-42/index.html) |
| Night 20 mm | "Of doubtful value" unless the target is already burning. Fire reveals own ship | [Leyte BE](https://www.ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.3.html) |
| PT/gunboat vs barges | Daihatsu drew about 5 ft, under the torpedo minimum depth of about 10 ft, so guns were used: 37 mm (125 rpm), 40 mm (120 rpm, 5,420 yd), twin .50s. Even 40 mm struggled against compartmented barges | [Wikipedia PT boat](https://en.wikipedia.org/wiki/PT_boat) |
| Vietnam PBR | Twin .50 forward, .50 aft, M60s, Mk 18/19 grenade launcher, all hand-aimed and unstabilized. One PBR patrol destroyed 65 boats (31 Oct 1966). At least 19 PBRs lost | [Wikipedia PBR](https://en.wikipedia.org/wiki/Patrol_Boat,_River) |
| Mk 38 Mod 1 25 mm (1986) | Manually trained and elevated, unstabilized. HEI effective about 2,700 yd. 175–200 rpm. "Target tracking is difficult" against small combatants. Mod 2 (post-Cole, 2005): 2–3× Ph, kill at more than 2× range | [navweaps Mk 38](https://www.navweaps.com/Weapons/WNUS_25mm_mk38.php), [seaforces](https://www.seaforces.org/wpnsys/SURFACE/Mk-38-machine-gun-system.htm) |
| Small-boat threat ranges (2005 NDIA) | .50 cal 1 nm, Mk 38 1,500 yd, CIWS 1B 2 nm. Boat RPG/small arms out to 1,000+ yd | [NDIA Steelman](https://ndia.dtic.mil/wp-content/uploads/2005/garm/thursday/steelman.pdf) |
| Phalanx surface mode | Pre-1B surface mode was "primitive and hard to use". 1B added FLIR and auto-track. Surface mode is manual, firing 50-round bursts | [navalgazing Phalanx](https://www.navalgazing.net/Phalanx) |
| Praying Mantis, 1988 | Boghammars were killed by A-6 Rockeye, not ship guns. Joshan was finished by gunfire after SM-1 hits. Sassan platform ZU-23s silenced by 5" gunfire | [Wikipedia](https://en.wikipedia.org/wiki/Operation_Praying_Mantis) |

[INFERRED] Sim model for hand-aimed automatic weapons against boats:
- Ph per burst falls roughly with the square of range beyond about 400–600 yd, the limit of human range estimation.
- Tracer-walking needs about 1–3 s of observed fire before hits.
- Unstabilized mounts lose most effectiveness in sea state 3+ or when firer and target are both maneuvering. The Mk 38 Mod 1→2 jump of 2–3× bounds this.

---

## 5. Human factors in emergencies

- **Smoke, flash, and blindness:**
  - Tassafaronga: own flashes, smoke, and splashes blocked observation ([Combat Narrative](https://ibiblio.org/hyperwar/USN/USN-CN-Tassafaronga/index.html)).
  - 1912 trial: smoke over the guns is the main reason individual aim collapsed ([Wikipedia Thunderer](https://en.wikipedia.org/wiki/HMS_Thunderer_(1911))).
  - Mk 14 doctrine prescribes tracer control in smoke or blast ([Mk 14 OP](https://ibiblio.org/hyperwar/USN/ref/Ordnance/GS-Mk14/index.html)).
- **Power and IC loss:**
  - South Dakota: 1 min after-ship blackout, then 3 min of total FC/IC loss. Repair parties restored mounts 6 and 8 in about 1 more minute ([WDR-57](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-south-dakota-bb57-war-damage-report-no57.html)).
  - Johnston: about 10 min in a rain squall to restore 5" power ([Wikipedia](https://en.wikipedia.org/wiki/USS_Johnston_(DD-557))).
  - [INFERRED] Electrical casualties last minutes, not seconds: about 1–3 min for switch or breaker faults and about 10 min for damage repair. Guns can be silent, or in local or manual control, for that time.
- **Casualties at the guns:** Samuel B. Roberts lost almost the whole gun crew to a cook-off. Hoel reassigned automatic-weapons crews as replacements for 5" crews and damage control ([Leyte BE](https://www.ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.3.html)).
- **Mount environment:** 5"/38 crews handled 55 lb shells and 35 lb powder in hot, smoky, fume-filled mounts. Upper handling room "stifling" ([navweaps crews](https://navweaps.com/Weapons/WNUS_5-38_mk12_Crews.php)). [INFERRED] Sustained rate decays after about 15–30 min. Delhi's 25→15 rpm drop is partly ammunition supply.
- **Maintenance and training:** some ships had excellent 1.1" results while others suffered "over a hundred casualties" (stoppages) from poor upkeep and untrained loaders ([GunBull 2-42](https://ibiblio.org/hyperwar/USN/rep/CINCPAC/GunBull/2-42/index.html)).
- **Ammunition exhaustion:** West Virginia used half her heavy-target ammunition in about 9 minutes at Surigao ([Leyte BE](https://www.ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.3.html)). The German Narvik DDs were down to half their ammunition after one action ([Wikipedia](https://en.wikipedia.org/wiki/Naval_battles_of_Narvik)).
- **Fear and confusion:** recognition failures and TBS overload at Guadalcanal I ([H-gram 012](https://www.history.navy.mil/about-us/leadership/director/directors-corner/h-grams/h-gram-012/h-012-1.html)). No quantified fear and accuracy data was found. [UNCERTAIN]

---

## 6. Point-blank: where fire control stops mattering

**Danger space** is the range band within which a shot still hits a target of height h ([Slover Ch.18](https://eugeneleeslover.com/USNAVY/CHAPTER-18-A.html), [mathscinotes](https://mathscinotes.com/2013/04/battleship-guns-and-danger-space/)). The simple approximation is δ ≈ h·cot(angle of fall). USN used a 20 ft citadel height, RN 30 ft. Angles of fall are from navweaps range tables.

| Gun | Range | Angle of fall | Danger space, h=20 ft | Danger space, h=30 ft |
|---|---|---|---|---|
| 16"/50 AP | 5,000 yd | 2.50° | about 150 yd | about 230 yd |
| 16"/50 AP | 10,000 yd | 5.01° | about 75 yd | about 115 yd |
| 16"/50 AP | 15,000 yd | 9.78° | about 40 yd | about 60 yd |
| 16"/50 AP | 20,000 yd | 14.92° | about 25 yd | about 40 yd |
| 5"/38 | 1,000 yd | 0.47° | about 800 yd | about 1,200 yd (point-blank) |
| 5"/38 | 2,000 yd | 0.93° | about 400 yd | about 600 yd |
| 5"/38 | 4,000 yd | 2.85° | about 135 yd | about 200 yd |
| 5"/38 | 6,000 yd | 5.97° | about 65 yd | about 95 yd |
| 5"/38 | 8,000 yd | 11.1° | about 35 yd | about 50 yd |

Calculated [INFERRED] from [navweaps 16"/50](http://www.navweaps.com/Weapons/WNUS_16-50_mk7.php) and [navweaps 5"/38](http://www.navweaps.com/Weapons/WNUS_5-38_mk12.php).

- **Slover's worked example:** 5"/38 at 8,500 yd against a 40 ft-high, 90 ft-deep target gives an 80 yd hitting space and a 65 yd range dispersion, so P(hit) is about 0.37 per round with the mean point of impact centred. With the MPI half a pattern off, P falls to about 0.12 ([Slover Ch.18](https://eugeneleeslover.com/USNAVY/CHAPTER-18-A.html)).
- **Point-blank condition:** when the maximum ordinate of the trajectory is below the target height, the danger space equals the range and a hit is essentially guaranteed if the line is right ([mathscinotes](https://mathscinotes.com/2013/04/battleship-guns-and-danger-space/)). This matches Rodney at 2,500–4,000 m ("virtually impossible to miss", [kbismarck](https://kbismarck.com/bismarck-last-battle.html)) and Matapan at 2,900–3,800 yd.
- [INFERRED] Thresholds where range-keeping stops mattering:
  - Danger space exceeds a typical eyeball or stadimeter range error (about ±10–15% of range):
    - 5"/38: at or under about 2,500–3,000 yd.
    - Heavy guns: at or under about 4,000–5,000 yd.
  - Inside these, only line (deflection) and target identification matter.
  - Between about 5 and 10 kyd, local control with turret rangefinders is workable.
  - Beyond about 12 kyd without a director, expect near-zero hits.
- **Battle-sight doctrine:** no formal USN or RN "battle sight, aim at waterline" doctrine for ship guns was found. [UNCERTAIN] The closest equivalents are fixed preset settings for AA:
  - 20 mm Mk 14 set to 800/1,200 yd and left.
  - 40 mm stepped 3,200→2,400→1,600.
  - 5"/38 preset sight angles ([Transport Ch.XXV](https://ibiblio.org/hyperwar/USN/ref/Transport/transport-25.html), [Mk 14 OP](https://ibiblio.org/hyperwar/USN/ref/Ordnance/GS-Mk14/index.html)).
- **Spotting rule against small/fast surface targets:** keep splashes well abaft the conning station, because splashes take about 2 s to rise ([Transport Ch.XXV](https://ibiblio.org/hyperwar/USN/ref/Transport/transport-25.html)).
- **Depression limit:** Hiei's main battery could not depress to hit destroyers close alongside ([Wikipedia Hiei](https://en.wikipedia.org/wiki/Japanese_battleship_Hiei)). [INFERRED] Model a minimum engagement range set by depression limit plus freeboard. Many turrets bottom out at about −2° to −5°.

---

## 7. Gaps and conflicts

- No sourced hand-crank training rate for heavy turrets.
- No WWII statistical "local control = X% of director" figure.
- No turret or mount telescope magnifications.
- No Indo-Pakistani 1971 short-range gunfire data. Searches returned only missile and Operation Trident material. [UNCERTAIN]
- Johnston's 45 hits on Kumano and Samuel B. Roberts's 40 hits are post-war claims. [UNCERTAIN]
- Hit counts on Bismarck range from 200 to 400 across sources.
- Washington's hits on Kirishima range from 8 to 20 (16").
