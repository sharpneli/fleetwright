# 08 — Historical Hit Rates, 1860–1910 (Ironclad and Pre-Dreadnought Era)
Status: final    Updated: 2026-10-09    Request: -

Purpose: calibration data for a model of naval gunnery without computers: optical sights, then short-base rangefinders, voice or dial range transmission, and local or turret spotting.
Tags: [INFERRED] = derived by me from the sources; [UNCERTAIN] = thin, conflicting or unverified; [RECALL] = from memory of the secondary literature and not confirmed online during this pass.

---

## 0. Headline calibration numbers (TL;DR)

| Regime | Typical hit % | Notes |
|---|---|---|
| Ironclad melee, point-blank (≤500 yd), 1862–1879 | 20–50% of heavy shot | Small numbers of shots. Smoke-limited. Ranges ~50–1,000 yd. |
| Ironclad stand-off (1–3 km), 1879 | ~2–5% | Huascar at Iquique: 1/40. |
| 1890s fleet action, 1,000–3,000 m, untrained, no rangefinder (Yalu, Manila, Santiago) | **1–3% all calibres**; 3–5% for heavy guns | Santiago: ~1.3% of all rounds. Manila: ~2.5% counted, maybe ~4% real. |
| 1904 long-range action, 5–8 km, Barr & Stroud 1.5 m rangefinder (Yellow Sea) | 12": ~5%; all calibres ~1.7% | |
| 1905 Tsushima, mostly ≤6 km, trained and blooded fleet | 12": ~9% (Campbell-era figure); 6": ~2–4% [INFERRED] | |
| RN Prize Firing 1897 (~1,400–1,600 yd, stationary target, ship moving) | Fleet average ~24–35% (28% "Navy average") | |
| Best RN ships 1899–1902 (Scott method, continuous aim) | 70–88%, 4.6–5.7 hits/gun/min (light QF) | |
| RN battle practice c.1907–08, 12" | ~0.4 hits/gun/min (fleet average) | 9.2": ~2.0 hits/gun/min |
| USN 1912–18 battle practice formula | H% = 100 / [1 + 0.0007·(R−2000)] | ≈ 26% at 6,000 yd, ≈ 19% at 8,000 yd; ≈ 0.165 hits/gun/min |

**Rule of thumb [INFERRED]:** battle hit rates ran about one-fifth to one-tenth of the same navy's practice hit rates at the same range. This matches the WWI Jutland comparison: about 3% in battle against roughly 20% expected from practice (https://www.navalgazing.net/Open-Thread-106).

---

## 1. Master table — battles

| Battle (date) | Shooter → target | Range | Rounds | Hits (how counted) | Hit % | Source |
|---|---|---|---|---|---|---|
| Hampton Roads (9 Mar 1862) | Monitor (2×11" Dahlgren) → Virginia | Close range, tens to hundreds of yd; ~3 h | 43 | ~20 (Virginia's casemate marks) | ~47% | https://historynet.com/what-if-monitor-and-virginia-alternate-outcome/ |
| Same | Virginia → Monitor | Same | ? | 22 (9 on turret) | — | Same |
| Mobile Bay (5 Aug 1864) | Chickasaw (11") → Tennessee | 10–50 yd | 52 shells at Tennessee (+75 at Fort Morgan) | Uncounted; none penetrated; jammed port shutters | — | https://en.wikipedia.org/wiki/USS_Chickasaw_(1864) |
| Lissa (20 Jul 1866) | Austrian fleet → Italians | Melee, ~100–1,000 yd; Maria Pia opened at 874 yd | ~4,250 [INFERRED: Kaiser fired 850 ≈ 1/5 of the total] | ? | ? | https://dawlishchronicles.com/?p=5369 |
| Same | Italian fleet → Austrians | Same | ~1,450 [UNCERTAIN] | ~400 [INFERRED: Kaiser took 80 hits ≈ 1/5 of the total] | ~25–30% [INFERRED, UNCERTAIN] | https://shipsofscale.com/sosforums/threads/10th-of-may-today-in-naval-history-naval-maritime-events-in-history.2104/page-9 ; dawlishchronicles |
| Iquique (21 May 1879) | Huascar (2×10" Armstrong) → Esmeralda | Shallow water kept Huascar from closing; 4 h | 40 (10") | 1 | 2.5% | https://www.navalgazing.net/Huascar-Part-1 |
| Angamos (8 Oct 1879) | Cochrane + Blanco Encalada (6×9" each) → Huascar | 2,200–3,000 m at first, then 200 yd to point-blank; ~1 h 40 min | ~76 [RECALL, UNCERTAIN: Cochrane ~45, Blanco ~31] | 27 heavy hits (later survey); 10 timed in the official log | ~35% [INFERRED, UNCERTAIN] | https://www.navalgazing.net/Huascar-Part-2 ; https://www.usni.org/magazines/proceedings/1879/december/official-report-naval-engagement-between-chilian-fleet-and |
| Same | Huascar → Cochrane | Same | ? (two 300-pdr shots overshot at 9:20) | 2 | — | USNI 1879 |
| Fuzhou (23 Aug 1884) | French squadron → Chinese fleet at anchor | Point-blank in a river anchorage | Not given | 9 of 11 Chinese ships lost; Zhenwei sunk by one shell | — | https://en.wikipedia.org/wiki/Battle_of_Fuzhou |
| Yalu (17 Sep 1894) | Chinese → Japanese | Opened at 5,200–6,000 m; main fight 1,000–3,000 m | 12": 197 (both ironclads); Chen Yuen 6": 148 | 12" hits known: Matsushima 2, Saikyo Maru 4, others few | 12": ~4–6% [INFERRED]; McGiffin estimated ~20% (heavy guns) | https://www.usni.org/magazines/proceedings/1895/july/battle-yalu ; https://navyandmarine.org/ondeck/1894YaluBattle.htm |
| Same | Japanese → Chinese | Same | Not found (Matsushima's 32 cm Canet fired only 4 rounds) | Ting Yuen ≥200, Chen Yuen ~120 | McGiffin estimated ~12% excluding 6-pdr and smaller [UNCERTAIN] | Same; https://en.wikipedia.org/wiki/Japanese_cruiser_Matsushima |
| Manila Bay (1 May 1898) | US Asiatic Sqn → Spanish ships | 5,000 → 2,000 yd, five passes; targets mostly stationary | 5,859 (all, including shots at shore targets) | 141 (Ellicott's post-battle survey) / 145 (Wikipedia) | 2.4–2.5% (Ellicott thought half the hits on the 3 main ships went uncounted, so ~3.5–4% [INFERRED]) | https://en.wikipedia.org/wiki/Battle_of_Manila_Bay ; https://www.usni.org/magazines/proceedings/1899/april/effect-gun-fire-battle-manila-bay-may-1-1898 |
| Santiago (3 Jul 1898) | US fleet → Spanish cruisers | 6,000 yd at most; mostly 1,000–3,000 yd; Brooklyn–Vizcaya at 1,200 → 950 yd | ~9,000 (Sims); 9,500 (Morison/Wikipedia) | 120–121 | **~1.3%** | https://history.navy.mil/research/publications/documentary-histories/united-states-navy-s/squadron-bulletins/north-atlantic-fleet-17.html ; https://en.wikipedia.org/wiki/William_Sims |
| Same, major calibre only | US 8"/12"/13" | Same | 1,200 | 42 | 3.5% | https://www.history.navy.mil/about-us/leadership/director/directors-corner/h-grams/h-gram-020/h-020-6-victory-at-santiago-.html |
| Same | Spanish → US | Same | ~3,200 [UNCERTAIN, forum] | Brooklyn ~20+ (4 medium, 16 light) | ~1–2% [UNCERTAIN] | https://navalgazing.net/Spanish-American-War-Part-8 ; H-gram 020 |
| Chemulpo (9 Feb 1904) | Uryu's cruisers → Varyag | ~5.6–6 km and closing; ~1 h | ? [RECALL: Japanese ~400 rounds] | "5 serious" waterline hits; upper works riddled [RECALL: ~11 hits in total] | ~3% [INFERRED, UNCERTAIN] | https://en.wikipedia.org/wiki/Battle_of_Chemulpo_Bay |
| Same | Varyag → Japanese | Same | [RECALL: Rudnev claimed ~1,100] | 0 per Japanese records | ~0% | Same |
| Yellow Sea (10 Aug 1904) | Japanese 12" | Opened at >8 nmi; main fire ~3.5 mi (5.6 km); 6–7 h in all | 603 (12"); 279 of them AP | ~30 | 4.7–5% | https://www.navweaps.com/Weapons/WNJAP_12-40_EOC.php |
| Same | Both sides, all calibres | Same | ~7,382 (12": JP 603 / RU 259; 10": RU 224 / JP 33; 8": JP 307; 6": JP 3,592 / RU 2,364) | ~125 [INFERRED: 1.7% × 7,382] | ~1.7% | https://en.wikipedia.org/wiki/Battle_of_the_Yellow_Sea |
| Same | Russians → Mikasa | Same | — | 20 hits on Mikasa | — | Same |
| Ulsan (14 Aug 1904) | Kamimura's 4 armoured cruisers → Vladivostok cruisers | Opened at 8,500 m; ~3 h chase | Not found | Rossia 28; Gromoboi ≥22 hull hits plus superstructure; Rurik sunk | — | https://naval-encyclopedia.com/ww1/russia/rossiya-class-cruisers-1896.php ; https://en.wikipedia.org/wiki/Battle_off_Ulsan |
| Tsushima (27–28 May 1905) | Japanese 12" | Opened at 6,400–7,000 m; mostly ≤6,000 yd; mist; Japanese 15 kn, Russians 11 kn | 446 | ~40 | **~9%** | https://www.navweaps.com/Weapons/WNJAP_12-40_EOC.php |
| Same, contemporary claim | Japanese 12" | Same | 1,275 | 250 | 19.6% — inflated, superseded | https://www.usni.org/magazines/proceedings/1906/october/inherent-tactical-qualities-all-big-gun-one-caliber-battleships |
| Same | Japanese 6"/8" | Same | ~16,875 (Sims's estimate) | ~350 (Sims's estimate) | ~2.1% [UNCERTAIN] | Same |
| Same | Russians → Mikasa | Same | — | >40 (10×12", 22×6", rest unknown); 6×12" + 19×6" in the first phase | — | https://en.wikipedia.org/wiki/Japanese_battleship_Mikasa |

---

## 2. Engagement notes

### Hampton Roads, 9 March 1862
- The fight was mostly at close range for about 3 hours. Virginia carried only shell, no armour-piercing shot. Monitor used reduced 15 lb charges; later tests showed 30 lb was safe. (https://en.wikipedia.org/wiki/Battle_of_Hampton_Roads)
- Monitor: 43 rounds, about 20 hits, none at or below the waterline. Virginia: 22 hits on Monitor, 9 of them on the turret. (https://historynet.com/what-if-monitor-and-virginia-alternate-outcome/)
- Rate of fire: two guns fired 43 rounds in about 3 hours, roughly 7 rounds per gun per hour. The turret was slow to start and stop and gun ports were shut while reloading. [INFERRED]
- Model lesson: at ≤200 yd against a large casemate, about 50% of heavy shot hits. Rate, not accuracy, is the limit.

### Mobile Bay, 5 August 1864
- Chickasaw fired 52 shells at Tennessee from 10–50 yd. None penetrated, but they jammed the stern port shutters. (https://en.wikipedia.org/wiki/USS_Chickasaw_(1864))
- Repeated 11" solid shot hits left the after shield "shaky". A shot on the edge of a port cover broke Buchanan's leg. (https://hathi.library.illinois.edu/feed/packages/BrittleBooks/prep/uiuc-7_20221004/99116645612205899/00000350.txt)
- No hit count was found. [UNCERTAIN]

### Lissa, 20 July 1866
- Visibility was poor. The melee was fought in dense smoke with only brief glimpses of the enemy. A heavy swell made the opening Italian salvoes miss. Maria Pia opened on Kaiser at 874 yd. (https://en.wikipedia.org/wiki/Battle_of_Lissa_(1866))
- Kaiser fired 850 rounds, about one-fifth of the Austrian total, and received 80 hits, about one-fifth of all hits taken by Austrian ships. (https://dawlishchronicles.com/?p=5369)
- That puts total Austrian fire at about 4,250 rounds and hits on Austrian ships at about 400. [INFERRED]
- Italian fire is given as about 1,450 shots. (shipsofscale, above) [UNCERTAIN]
- If 1,450 shots and about 400 hits are both right, the Italian hit rate was 25–30% in a point-blank melee. That figure is very uncertain. The Austrians also had to fire under about 4,000 rounds through smoke, and their result was decided by ramming, not gunfire.
- Battle hits at this point sank nothing armoured. Re d'Italia was rammed, and Palestro was lost to fire.

### Iquique and Angamos, 1879
- **Iquique.** Huascar's 10" guns fired 40 rounds in 4 hours for 1 hit, 2.5%. Her turret arc was limited and she could not close because of shallow water. (https://www.navalgazing.net/Huascar-Part-1)
- **Angamos ranges.** Cochrane opened at about 2,200 m (Wikipedia) or "a mile and a quarter" (militaryheritage). Blanco Encalada fired from about 200 yd. The closest pass was about 25 m. (https://www.usni.org/magazines/proceedings/1879/december/official-report-naval-engagement-between-chilian-fleet-and)
- **Hits on Huascar.** A later survey counted 27 heavy hits (https://www.navalgazing.net/Huascar-Part-2). The official log times only 10 of them: 7 from Cochrane and 3 from Blanco Encalada.
- **Huascar's results.** Two hits on Cochrane. Her steering was disabled 3–4 times, and Admiral Grau was killed by a hit on the conning position.
- **Rounds fired.** Chilean round counts are [RECALL]; with them, the Chilean hit rate was roughly a third, at mostly ≤1,000 m. [UNCERTAIN]

### Fuzhou (Mawei), 23 August 1884
- The French fired at point-blank range on a Chinese squadron moored in the Min River. Fighting ran from about 14:00. Nine of eleven Chinese ships were lost. French losses were 10 killed and 48 wounded; Chinese losses about 800 killed. (https://en.wikipedia.org/wiki/Battle_of_Fuzhou)
- Hotchkiss revolving cannon were decisive against torpedo launches.
- No round counts were found. Use this as a "massacre" case: anchored targets at a few hundred metres, where hits are effectively certain. [INFERRED]

### Yalu, 17 September 1894
- **Ranges.** The Chinese opened at 5,200–6,000 m. The Japanese held fire until about 3,000 m. The main action was at 2,000–3,000 m, with circling passes down to 1,000 m and individual duels at 300–800 m. The Japanese flying squadron ran at 14 kn. (https://en.wikipedia.org/wiki/Battle_of_the_Yalu_River_(1894) ; https://www.usni.org/magazines/proceedings/1895/july/battle-yalu)
- **Rounds.** The Chinese ironclads fired 197 rounds of 12", about one round every 5–6 minutes per gun. Chen Yuen exhausted her 6" ammunition after 148 rounds. Matsushima's 32 cm Canet fired only 4 rounds. (https://en.wikipedia.org/wiki/Japanese_cruiser_Matsushima)
- **Hits counted.** Ting Yuen was struck by 200 or more projectiles and Chen Yuen about 120 times. Neither belt was penetrated more than 4". (USNI 1895)
- **McGiffin's estimates.** He served on Chen Yuen and put Japanese accuracy at about 12% (excluding 6-pdr and smaller) and Chinese at perhaps 20%. These are participant estimates and probably too high, especially the Chinese figure. [UNCERTAIN] (https://navyandmarine.org/ondeck/1894YaluBattle.htm)
- **Chinese handicaps.** Old powder, some cement-filled or wrong-calibre shells, solid shot instead of shell, and rare live-fire practice.

### Manila Bay, 1 May 1898
- **Ranges.** 5,000 to 2,000 yd. Spanish ships were anchored or barely moving, and the US squadron made five passes. (https://www.usni.org/magazines/proceedings/1899/july/official-report-battle-manila-bay)
- **Rounds.** 5,859 in total, including shots at shore batteries and unengaged ships. (https://en.wikipedia.org/wiki/Battle_of_Manila_Bay)
- **Hits counted.** Lt. Ellicott inspected the wrecks, interviewed survivors and used Montojo's report. His count:

| Ship | Hits | Ellicott's view of the count |
|---|---|---|
| Reina Cristina | 39 | Probably no more than half; Montojo estimated about 70 |
| Castilla | 40 | Probably no more than half |
| Ulloa | 33 | Probably no more than half |
| Don Juan de Austria | 13 | Complete |
| Others | 16 | Mostly complete |
| **Total** | **141** | |

  Source: https://www.usni.org/magazines/proceedings/1899/april/effect-gun-fire-battle-manila-bay-may-1-1898
- **By calibre (partial).** 13×8", 6×6", 22 other hits of 5" or larger, 31×6-pdr and 29 other small, which sums to 101. The article is internally inconsistent here.
- **Hit rate.** 141 / 5,859 = 2.4% as counted. If the three main ships really took about twice their counted hits, the rate is about 3.5–4%. [INFERRED]
- **Fire control.** Fiske's stadimeter only, with ranges called by voice.

### Santiago, 3 July 1898
- **Totals.** About 9,000 shots for 120 hits (1.3%). Sims, cited in NHHC Squadron Bulletin No. 23 (https://history.navy.mil/research/publications/documentary-histories/united-states-navy-s/squadron-bulletins/north-atlantic-fleet-17.html). Morison and Wikipedia give 9,500 shells and 121 hits (https://en.wikipedia.org/wiki/William_Sims). Morison's 9,500 / 121 may combine Manila Bay and Santiago. [UNCERTAIN]
- **Major calibre.** 1,200 rounds for 42 hits, 3.5% (NHHC H-Gram 020, above).
- **Other estimates.** Trask puts US accuracy at 1–3%. Alger said no gun made as much as 5% at a mean range of about 2,800 yd against targets about 200 × 24 ft. Yates Stirling said "under one hit in fifteen shots" (https://penelope.uchicago.edu/Thayer/E/Gazetteer/People/Yates_Stirling/Sea_Duty/8*.html).
- **US Board counts** on the three wrecks Teresa, Oquendo and Vizcaya. Calibres are as the Spanish source names them; 12.7 cm is presumably the US 5". (https://www.usni.org/magazines/proceedings/1899/april/sketches-spanish-american-war)

| Calibre | Hits |
|---|---|
| 12" | 2 |
| 8" | 10 |
| 5" | 16 |
| 4" | 8 |
| Secondary battery | 73 |
| **Total** | **109** |

  Colón took a few more (4×5"?) [UNCERTAIN]. About 67% of the hits came from 6-pdr and smaller guns.
- **Undercounting.** Shells that hit wood later burned away, and areas that sank were never inspected (https://navalgazing.net/Spanish-American-War-Part-8). The low-end figure (navalgazing, from the board): Teresa 10, Vizcaya 14, Oquendo 14 hits from 4" and larger. The H-gram's "Vizcaya hit over 200 times" and "Oquendo 57" are inflated or include small arms. [UNCERTAIN]
- **Conditions.** A stern chase at 8–14 kn; 13" turrets took about 5 minutes per round (Stirling); heavy smoke from brown powder; no rangefinders beyond stadimeters.
- **Spanish fire.** Some Spanish sights were found set for 10,000+ yd although actual ranges were at most 6,000 yd. Brooklyn was hit about 20 times; Iowa twice by Colón's secondary battery.
- **Conflicting claim.** A forum post gives about 5,400 US rounds and 163 hits, plus about 3,200 Spanish rounds and 64 hits. It has no provenance. [UNCERTAIN] (https://alternate-timelines.com/thread/4374/spanish-american-lessons-learned-forgotten?page=3)

### Chemulpo, 9 February 1904
- Varyag fired from 11:45 to 12:45, with range closing to 28–30 cables (5.6–6 km). She took 5 serious waterline hits, had at least 5 fires, and lost all twelve 6" guns. Japanese records show no damage to their ships. (https://en.wikipedia.org/wiki/Battle_of_Chemulpo_Bay)
- [RECALL, UNCERTAIN] Russian claims of about 1,100 rounds fired give a Russian hit rate of effectively 0%. Japanese fire was a few hundred rounds for about 11 hits, roughly 3%.

### Yellow Sea, 10 August 1904
- **Fire control.** Japanese pre-dreadnoughts had Barr & Stroud 1.5 m coincidence rangefinders rated to about 6 km. Russian ships had Liuzhol stadiametric sets rated to about 4 km. (https://en.wikipedia.org/wiki/Battle_of_the_Yellow_Sea)
- **Ranges.** Togo's first salvo came at more than 8 nmi, which is speculative long-range fire. The main fire was at about 3.5 mi. Both fleets ran at about 14 kn.
- **Rounds by calibre.** 12": Japan 603, Russia 259. 10": Russia 224, Japan 33. 8": Japan 307. 6": Japan 3,592, Russia 2,364. Total about 7,382, with an overall hit rate of about 1.7% (Wikipedia).
- **Japanese 12".** About 30 hits from 603 rounds (4.7%). 279 rounds were AP; at least 10 AP hits struck armour, none penetrated. (https://www.navweaps.com/Weapons/WNJAP_12-40_EOC.php)
- **Hits by ship.** Tsesarevich 13×12" + 2×8"; Peresvet 39; Retvizan 12–18; Pobeda 11; Poltava 12–14. Mikasa took 20.
- **Russian hit rate.** Russian rounds above 6" totalled 483, plus 2,364 of 6". Russian hits on the Japanese were about 25–35. That gives about 1% overall; Russian heavy guns did perhaps ~5%. [INFERRED, UNCERTAIN]

### Ulsan, 14 August 1904
- Japanese fire opened at 8,500 m (05:20–05:23). Kamimura broke off at 11:15. The Russian squadron ran at about 14 kn. (https://en.wikipedia.org/wiki/Battle_off_Ulsan)
- Rossia took 28 hits (19 starboard, 9 port); Gromoboi at least 22 hull hits plus superstructure hits; Rurik was sunk. (https://naval-encyclopedia.com/ww1/russia/rossiya-class-cruisers-1896.php)
- No round counts were found. Sixty or more hits on three cruisers over about 5 hours from four Japanese armoured cruisers suggests a hit rate of a few percent. [INFERRED]

### Tsushima, 27–28 May 1905
- **Fire control and conditions.** Japanese ships had Barr & Stroud FA3 (1.5 m) rangefinders; most Russian ships had Liuzhol sets, and Oslyabya and Navarin had FA2. Visibility was poor, with mist limiting sight to about 5 mi, and high waves. Japanese 15 kn, Russians 11 kn. Opening ranges were 7,000 m (Russian) and 6,400 m (Japanese). (https://en.wikipedia.org/wiki/Battle_of_Tsushima)
- **Japanese 12".** 446 rounds, about 40 hits, about 9%. Navweaps attributes the improvement over the Yellow Sea mainly to shorter range, with most fighting at ≤6,500 yd. (https://www.navweaps.com/Weapons/WNJAP_12-40_EOC.php) Navweaps lists Campbell's Warship articles among its sources, but the exact figure attribution is [UNCERTAIN].
- **Per-ship expenditure.**
  - Mikasa: 124 × 12" (https://en.wikipedia.org/wiki/Japanese_battleship_Mikasa)
  - Shikishima: 74 × 12", 1,395 × 6", 1,272 × 12-pdr; she was hit 9 times (https://naval-encyclopedia.com/ww1/japan/shikishima-class-battleships-1898.php)
- **Secondary fire [INFERRED].** Shikishima's figures show the 6" fired about 19 rounds for every 12" round. Scaled to the fleet, Japanese 6" fire likely ran to 7,000–10,000 rounds.
- **Hits counted on Oryol** (surrendered, so surveyed): 5×12", 9×8", 39×6", 21 smaller or unknown, 74 in all (Wikipedia, above). Sims's 1906 claim of "42 12-inch hits on Orel" was a contemporary overestimate.
- **Russian fire.** Mikasa took more than 40 hits (10×12", 22×6"), including 6×12" and 19×6" in the opening phase. Russian opening fire was effective in that phase.
- **Sims's 1906 figures** (from R.D. White, contemporary): 12" 1,275 rounds and 250 hits (19.6%); 6"/8" about 16,875 rounds and 350 hits (2.1%). The 12" figures are superseded by the 446/40 count. Sims's 2.1% secondary rate is an estimate. [UNCERTAIN] (https://www.usni.org/magazines/proceedings/1906/october/inherent-tactical-qualities-all-big-gun-one-caliber-battleships)
- **Rates of fire** (Sims 1906): 12" about 2 rounds per minute maximum and controlled 6" about 4 per minute. The Japanese 6" in practice fired about twice as fast as the 12".
- **Russian rates** (Russo-Japanese War): 12" about 0.5–0.7 rounds per minute. Pre-war Russian estimates gave a 4-gun broadside a 40% chance of a hit at 4,000 yd and 10% at 10,000 yd. (http://www.navweaps.com/Weapons/WNRussian_12-40_m1895.php)

---

## 3. Peacetime practice

### Royal Navy Prize Firing (annual, to 1904) and Gunlayers' Test
- **Format.** A stationary target of about 20 ft × 16 ft 9 in, passed by the firing ship at about 1,400 yd closest approach (1892–1903). The ship was moving, so gunlayers had to correct continuously. About 400 rounds of 1-inch aiming-rifle ammunition per heavy gun per year c.1900. (https://www.navalgazing.net/Open-Thread-106)
- **1904.** Prize-firing range moved from 1,600 to 2,700 yd. (https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing)
- **Fleet results.**
  - Mediterranean Fleet 1897: 24% hits with 6" and 35% with 4.7". "Navy average" for comparable guns: 28%. (https://en.wikipedia.org/wiki/Percy_Scott)
  - 1901: 25% of RN ships excused themselves from firing. (https://www.usni.org/magazines/proceedings/1949/april/admiral-sir-percy-scott-and-british-naval-gunnery)
- **Scylla** (4.7" and 6" QF; Wikipedia Percy Scott; USNI 1949):

| Year | 6" | 4.7" | Notes |
|---|---|---|---|
| 1897 | 8% | 13% | |
| 1898 | — | — | 2nd in Mediterranean Fleet |
| 1899 | 45% | 80% | 4.6 hits/gun/min record. Empress of India 46.9% with 6" the same year. |

- **Terrible** (9.2" and 6"):
  - 1900: 76.8% with 6".
  - 1901: 80%, best in the navy; three gun crews scored 41% against a 28% navy average.
  - 1902: 88.2% after sight correction.
  - Petty Officer Grounds: 8 hits from 8 rounds in one minute.
- **Barfleur, 1901.** 71.7% and 5.7 hits/gun/min after adopting Terrible's methods; she roughly doubled her score within a month.
- **Later records.** Good Hope (2nd Cruiser Squadron, 1907): 6.6 hits/gun/min. Fleet average when Scott was Inspector of Target Practice (1905–07): 79.1% in gunlayers' tests (USNI 1949). Hits per gun per minute more than doubled for all calibres, and rose about 800% for the 9.2".
- **Overall trend.** Gunlayers' Test accuracy roughly quadrupled from 1900 to 1908, and rate of fire rose about 50%, then levelled off (navalgazing Open Thread 106).
- **[UNCERTAIN] anecdote.** RN battleships in 1898 fired 200 rounds at a stationary target for 2 hits; the stated range of "200 yards" is surely garbled. (https://warhistory.org/es/article/late-nineteenth-century-naval-gunnery)

### Royal Navy Battle Practice (from 1901/1904)
- **Ranges.** About 3,000–4,000 yd in 1904, about 5,000–7,000+ yd by 1907–08, and 9,000 yd in the 1912 Thunderer–Orion trial, where director firing gave about 6× the hits of gunlayer firing. (warhistory.org; https://en.wikipedia.org/wiki/Percy_Scott)
- **Results.** Whole-service average c.1907–08: 12" about 0.4 hits per gun per minute; 9.2" about 2.01 hits per gun per minute (https://www.usni.org/magazines/proceedings/1908/december/professional-notes). A 12" gun firing about 1 round per minute in practice [INFERRED] implies roughly 30–40% hits at battle-practice range. [INFERRED, UNCERTAIN]
- **Official returns.** "Result of battle practice in H.M. Fleet, 1906" (HMSO, 16 pp.) exists but is not online (https://rmg.co.uk/collections/library/rmgl-36391). [GAP]
- **Sims 1906.** In an unnamed comparison at about 6,000 yd, firing underway at a 90 × 30 ft target, ship A scored about 40% hits and ship B scored 0%. Both did equally well at short range. This illustrates how widely ships varied at long range.

### US Navy target practice
- **1899, North Atlantic Squadron.** Five ships fired for 5 minutes each (25 minutes in all) at a lightship hulk at about 1,600 yd and scored 2 hits. (Morison, quoted at https://alternate-timelines.com/thread/4374/spanish-american-lessons-learned-forgotten?page=3) Other tellings give 2,800 yd. [UNCERTAIN]
- **About six years later.** A single gunner made 15 hits in 1 minute at 1,600 yd, half of them in a 50-inch bull's-eye. (Morison, same)
- **Rate of fire.** 13" turrets went from about 300 s per round (1898) to about 30 s under the Sims reforms. 15/15 hits on canvas targets were achieved. Connecticut made an excellent battle-practice score at about 8,000 yd, c.1907–08. (https://penelope.uchicago.edu/Thayer/E/Gazetteer/People/Yates_Stirling/Sea_Duty/8*.html)
- **Sims's claims.** A 100% increase in rate of hitting and a 500% increase in battery effectiveness (https://www.usni.org/magazines/navalhistory/2015-04/armaments-innovations-continuous-aim-fire-learning-how-shoot). Terrible's methods gave about 10× accuracy and nearly 4× rate.
- **Sims 1904 record practice** (https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing):
  - Target 17 × 21 ft at 1,400–1,600 yd, with a 51-inch bull's-eye.
  - Theoretical estimates: 100% achievable at 1,600 yd; about 40% (realistically about 1 in 4) at 3,200 yd; about 16% at 6,000 yd.
  - A 12" against a ship target at 6 mi: about 1 in 48.
  - Best rangefinder error at 6,000 yd: over 100 yd, against about 25 yd needed.
  - Pointer vertical dispersion: 4, 8 and 15 ft at 1,600, 3,200 and 6,000 yd.
- **USN 1912–18 battle practice fit** (BuOrd, 1937 study): H% = 100 / [1 + 0.00070·(R − 2000)] against a 600 × 90 × 30 ft constructive target; about 0.165 hits per gun per minute (https://www.navweaps.com/index_inro/INRO_BB-Gunnery.php). This gives about 59% at 3,000 yd, 32% at 5,000 yd, 26% at 6,000 yd, 19% at 8,000 yd and 15% at 10,000 yd [INFERRED: computed from the formula].

### Japanese practice before Tsushima
- No quantitative figures were found. [GAP]
- [RECALL] Togo's fleet fired heavily with sub-calibre and aiming-rifle ammunition at Masan Bay in the months before Tsushima, and Japan adopted centralised fire control (a range officer in the foretop) after the Yellow Sea.

---

## 4. Modelling guidance [INFERRED]
1. **Base hit probability versus range.** Use the USN fit H(R) = 1 / [1 + k(R − 2000)].
   - Trained 1905–10 practice: k ≈ 0.0007/yd.
   - 1890s practice before continuous aim: hits are about 3–5× lower at the same range (RN 1897 about 28% at 1,400 yd, against about 90%+ for Scott-trained ships).
2. **Battle degradation.** Multiply practice H by about 0.1–0.3. Smoke, own and target manoeuvre, being fired at, and unknown ranges all contribute.
   - Santiago: about 1.3% achieved where 1898 practice would have given about 10%.
   - Tsushima 12": about 9% achieved where practice at about 6,000 yd would have given about 25–30%.
3. **Calibre effect.** Heavy guns hit at 2–4× the rate of QF secondaries in the same action (Santiago 3.5% against 1.3%; Tsushima 9% against about 2%). But QF guns fire 5–20× more rounds, so they dominate hit counts (67% of Santiago hits came from secondaries).
4. **Fire-control step.** Short-base rangefinders were Barr & Stroud 1.5 m from about 1903, against stadimeters or Liuzhol sets. They roughly doubled heavy-gun accuracy at 5–6 km: about 5% at Yellow Sea and about 9% at Tsushima, against Russian figures well below that. Training and blooding mattered as much.
5. **Ironclad era (1860s–1880s).** At ≤500 yd, 20–50% hits but very slow fire, about 0.1–0.2 rounds per gun per minute. Beyond 1,500 yd, under about 5%. Rams and smoke dominate.
6. **Counting bias.** Post-battle hit counts undercount by up to 2×: burnt wood, sunk areas, and survey only above water (Manila, Santiago). Contemporary claims overcount: Sims's Tsushima 19.6%, McGiffin's Yalu 12–20%.

## 5. Gaps
- Official round counts for Lissa (Austrian and Italian) and Ulsan, and Campbell's full Tsushima table (8"/6" rounds and hits) — not accessible online.
- RN Battle Practice annual percentages 1904–1910, held in HMSO returns at the National Maritime Museum.
- USN target practice tables 1900–08, in Bureau of Navigation annual reports.
- Exact Chilean round counts at Angamos.
