# 06: Evading gunfire: history, doctrine and operations research (1900–1990)
Status: final    Updated: 2026-10-09    Request: -

Scope: how ships under gunfire tried to make themselves harder to hit, and how fire control answered. Torpedoes and bombs appear only for contrast.

Tags:
- **[INFERRED]** means my own derivation or synthesis.
- **[UNCERTAIN]** means the evidence is thin, conflicting or from memory.
- Every other fact has its source URL next to it.

---

## 0. Key findings for the simulator

1. **Evasion works through time of flight (ToF).** A ship can only dodge if it moves a pattern-width or more between the moment the gun fires (aim frozen) and impact. That is a heavy-gun effect at 15,000 yd or more (ToF 25–95 s). Against 4–5 in fire inside about 6,000 yd (ToF under 10 s) it has very little effect. Iowa 16 in ToF at maximum range is about 95 s, and a 26,500 yd shot takes about 40 s ([Wikipedia, rangekeeping](https://en.wikipedia.org/wiki/Mathematical_discussion_of_rangekeeping)). [INFERRED]
2. **Analog rangekeepers extrapolated a straight course.** WWII computers predicted the target's course and speed linearly, which is "reasonable" only because big ships turn slowly. Target course was the hardest input to get ([same source](https://en.wikipedia.org/wiki/Mathematical_discussion_of_rangekeeping)). Any deliberate course change therefore throws out the solution until it is re-tracked.
3. **Measured long-range hit rates against manoeuvring or obscured targets were 0.4–2%.** Against a crippled, steady target they rose to about 11%. See the tables in §1 and §5.
4. **The fire-control answers were:**
   - enlarging the pattern (the rocking ladder: +100 / 0 / −100 yd);
   - barrage fire against fast light craft;
   - closing the range to cut ToF;
   - tracking faster (Mk 8 against Mk 1, radar);
   - after 1970, adaptive tracking filters and closed-loop spotting (Phalanx).

---

## 1. Salvo chasing ("steer for the last splash")

### 1.1 Who did it

| Case | Who and how | Outcome | Source |
|---|---|---|---|
| Samar, 25 Oct 1944 | At 06:50 Sprague ordered Taffy 3's CVEs to take evasive action by "chasing salvos". | Under fire about 2.5 h. 2 of 6 CVEs lost (Gambier Bay to gunfire, St. Lo to a kamikaze). Every ship damaged. | [Wikipedia Samar](https://en.wikipedia.org/wiki/Battle_off_Samar); [Battle Experience Leyte 78.1](https://ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.1.html) |
| Hoel (DD-533) | Was "fish-tailing and chasing salvos" while drawing fire from the CVEs. | Took 40+ hits and sank at 08:55. Fired about 600 rounds. | [destroyerhistory.org](https://destroyerhistory.org/actions/index.asp?pid=4583); [Wikipedia](https://en.wikipedia.org/wiki/Battle_off_Samar); [BatExp 78.3](https://ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.3.html) |
| Johnston (DD-557) | "Zigzagged between the splashes" while laying smoke. Hid in a rain squall to repair. | Hit by 3×14 in and 3×6 in early, sank about 10:10. | [destroyerhistory.org](https://destroyerhistory.org/actions/index.asp?pid=4583) |
| Heermann (DD-532) | Dodged salvos landing in her wake as she retired. | Several 8 in hits forward, survived. | [destroyerhistory.org](https://destroyerhistory.org/actions/index.asp?pid=4583); [Wikipedia](https://en.wikipedia.org/wiki/USS_Heermann_(DD-532)) |
| Samuel B. Roberts (DE-413) | Contemporary accounts say she "dodged shells". | About 50 min of action, 300+ rounds from one 5 in gun, sunk by 14 in hits. | [de413.org](https://de413.org/category/ships-history/) |
| Gambier Bay (CVE-73) | Turned with the formation into a squall, under smoke. Captain Vieweg's own salvo chasing is described in secondary literature only. [UNCERTAIN] | First hit about 08:06–08:10. At least 15 hits from 08:10 to 08:50. Dead in the water at 08:45. | [USNI NH 2019](https://www.usni.org/magazines/naval-history-magazine/2019/october/gambier-bays-final-hours); [Wikipedia](https://en.wikipedia.org/wiki/USS_Gambier_Bay) |
| Komandorski, 26 Mar 1943: Salt Lake City | Capt. Rodgers made "the most abrupt zigs and zags" and "chased the salvos", turning after each near miss so the next salvo fell where the ship would have been. | Over 200 shells landed within 50 yd. Only 4–5 hits in about 3.5 h. After 10:02 her rudder was limited to 10°. | [ussslcca25 komador3](https://ussslcca25.com/komador3.htm); [ussslcca25](https://ussslcca25.com/komadors.htm); [H-gram 016](https://history.navy.mil/about-us/leadership/director/directors-corner/h-grams/h-gram-016/h-016-1.html); [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_Komandorski_Islands) |
| River Plate, 13 Dec 1939: Graf Spee as evader | Frequent course changes under smoke "undoubtedly threw out our gunfire" (Harwood ¶89). Turns of 130–150° under smoke at 06:37, 07:16 and 07:24. | Harwood asked for more practice against "highly mobile targets at fine inclinations". | [Harwood despatch, ¶21–35](https://www.fepow.family/Supplement/London_Gazette/River_Plate_Battle/html/part_ii.htm); [kbismarck](https://kbismarck.com/river-plate-battle.html) |
| River Plate: British shadowers | Harwood recommended that shadowers zigzag to spoil enemy range plotting, and found a drastic course change at the enemy's first salvo helpful (¶92). Graf Spee's second salvo at Ajax landed in her wake as she turned (¶46). | Achilles and Ajax were not hit while shadowing at 23,000–26,000 yd. | [kbismarck](https://kbismarck.com/river-plate-battle.html); [despatch](https://www.fepow.family/Supplement/London_Gazette/River_Plate_Battle/html/part_ii.htm) |

**WWI.** I found no explicit "chase the salvo" doctrine statement. British and German capital ships made turn-aways against torpedoes (see §2), and Beatty's 90° turn at Dogger Bank was against a supposed submarine ([Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Dogger_Bank_(1915))). [UNCERTAIN]

### 1.2 Results: the Japanese side of the Samar and Komandorski engagements

| Engagement | Shooter rounds | Hits | Rate | Source |
|---|---|---|---|---|
| Komandorski, Japanese 8 in on Salt Lake City | Nachi + Maya about 1,600 [UNCERTAIN, memory] | 5 hits on SLC, 2 on Bailey, 1 on Coghlan | about 0.5% | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_Komandorski_Islands) |
| Komandorski, SLC 8 in on Nachi | 806 AP + 26 HC = 832 | about 4–5 on Nachi, shared with Richmond and DDs | about 0.5% | same |
| Java Sea, 27 Feb 1942, Japanese 8 in | 1,271 | 5 (4 duds) | 0.4% | [Pacific Wrecks](https://pacificwrecks.com/battle/battle-of-the-java-sea.html) |
| Samar, Gambier Bay as target | many hundreds of rounds [UNCERTAIN] | at least 15 in 40 min, after closing to about 10,000 yd | not known | [Wikipedia](https://en.wikipedia.org/wiki/USS_Gambier_Bay) |

### 1.3 Why salvo chasing works

- The spotter corrects the next salvo's mean point of impact (MPI) toward where the target was predicted to be. A ship that turns toward the last splash goes where the gunner has just decided it is not.
- The salvo interval (about 30–35 s on Yamato, [navweaps](https://navweaps.com/Weapons/WNJAP_18-45_t94.php)) is comparable to ToF. Corrections therefore always lag by at least one cycle, and "pyramiding" (applying a spot before the last one has shown its effect) was already a common error ([Naval Ordnance & Gunnery ch. 18C](https://eugeneleeslover.com/USNAVY/CHAPTER-18-C.html)).
- Naval Gazing doubts the folk logic. It argues the real value is extra motion variability that defeats a spotting method built on a steady course. A commenter adds that humans are poor at random turning, so a mechanical rule like chasing may beat "random" turning ([navalgazing Spotting](https://navalgazing.net/Spotting)).

### 1.4 Did shooters adapt?

- The USN answers on record are the rocking ladder for "targets capable of rapid maneuvering" ([ch. 18C5](https://eugeneleeslover.com/USNAVY/CHAPTER-18-C.html)) and barrage fire with secondary guns ([navalgazing](https://navalgazing.net/Spotting)).
- At Samar the Japanese adapted mainly by closing the range. The Gambier Bay hits came after the cruisers closed to about 10,200 yd ([Wikipedia](https://en.wikipedia.org/wiki/USS_Gambier_Bay)).
- They also eventually switched from AP to HE ([bukowo](https://bukowo.com/2019/11/23/the-battle-of-leyte-gulf-act-iii-3-the-battle-off-samar/)).
- US reports noted tight Japanese patterns: 400–500 m at maximum range for 46 cm ([navweaps](https://navweaps.com/Weapons/WNJAP_18-45_t94.php)). Tight patterns make salvo chasing more effective. [INFERRED]

---

## 2. Evasive steering doctrine and practice by navy

| Navy and case | What was done | Numbers | Source |
|---|---|---|---|
| **KM / HSF**: Scheer's battle turns, Jutland | Turn-aways covered by torpedo-boat attacks. During the "death ride" before about 20:30 the British landed 25 hits on the battlecruisers and 12 on the battleships. The Germans hit only Colossus (2). | 13 boats fired 31 torpedoes at 20:22–20:30, scoring no hits. 1 boat lost, 4 badly damaged. | [navalgazing Jutland 4](https://www.navalgazing.net/Jutland-Part-4) |
| **RN**: Jellicoe's turn-away (20:21) | Presented a smaller target, lengthened the torpedo run and let ships comb the tracks. At least 11 ships dodged torpedoes. | Turn-away of about 3.5 points was enough at 7,000 yd, about 2.5 points at 8,000 yd. | [navalgazing](https://www.navalgazing.net/Jutland-Part-4); [BJMH Jutland DDs](https://bjmh.gold.ac.uk/index.php/bjmh/article/download/757/879/980) |
| **HSF battlecruisers** against the 13th Flotilla | Turned away a total of 8 points from 16:27 to 16:36. Seydlitz made sharp turns. One attacker reported a 16-point turn. | 11 torpedoes, 1 hit | [BJMH](https://bjmh.gold.ac.uk/index.php/bjmh/article/download/757/879/980) |
| **KM**: Graf Spee | Smoke plus large alterations (130–150°) to break British fire, and zigzagging to "throw out" the First Division's fire at 07:32. | Harwood estimated 60–70 hits, all calibres. Later German counts were lower. [UNCERTAIN] | [despatch](https://www.fepow.family/Supplement/London_Gazette/River_Plate_Battle/html/part_ii.htm) |
| **KM**: Scharnhorst, North Cape | Twisted to comb torpedoes after starshell illumination (18:49–18:51). Stord's 8 torpedoes all missed, probably combed. | Duke of York fired 52 radar-controlled salvos, 31 of them straddles. Speed fell 31 → 10 → 22 → 10 kt. | [Gazette 38038](https://thegazette.co.uk/London/issue/38038/supplement/3708/data.pdf); [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_North_Cape) |
| **KM**: Bismarck and Prinz Eugen, Denmark Strait | No deliberate evasion recorded. PoW's emergency turn to avoid Hood's wreck spoiled her own aim. PoW then turned away under smoke. | Bismarck fired 93 rounds, PE 157. Bismarck 4 hits and PE 3 on PoW; PoW 3 hits on Bismarck. | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_Denmark_Strait) |
| **IJN**: cruisers at Komandorski | Zigzagged so all turrets could bear, which cost them their speed advantage. Tama made a 360° evasive turn after 8 SLC salvos. | (see §1) | [ussslcca25](https://ussslcca25.com/komador3.htm) |
| **IJN**: Yamato at Samar | Reversed course to escape two torpedo spreads (Hoel and Heermann) and was out of the action about 10–20 min. Nagato followed her. | | [destroyerhistory](https://destroyerhistory.org/actions/index.asp?pid=4583); [Wikipedia](https://en.wikipedia.org/wiki/Battle_off_Samar) |
| **IJN**: cruisers off Leyte, US view | ComCruDiv 6: "evasive maneuvers of enemy cruisers were very effective" and tested the fire-control parties. | | [BatExp 78.3](https://ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.3.html) |
| **RM**: Italians at Calabria | Destroyers laid smoke at 16:01 and the battleships withdrew under it. Long-range DD torpedo runs on both sides: no hits. | Warspite hit at about 24,000 yd | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Calabria) |
| **RN**: Second Sirte | Cruisers and destroyers darted out of smoke to fire, then ducked back when Italian salvos came close. | Italian ships fired 1,511 rounds. British cruisers fired 1,553 and DDs about 1,300 plus 38 torpedoes. Very few hits either way. | [Wikipedia](https://en.wikipedia.org/wiki/Second_Battle_of_Sirte) |

**Explicit doctrine statements**

- **USN WWII war instructions.** These require zigzagging against submarines and radical manoeuvres against air attack. Their manoeuvring sections do not address surface gunfire ([WarInst ch. 7](https://ibiblio.org/hyperwar/USN/ref/WarInst/WarInst-7.html)).
- **Gunfire evasion as practice.** It appears to have been captain's practice codified only afterwards (Sprague's 06:50 order, Harwood ¶92). [INFERRED]
- **"Alter course after each enemy salvo".** I found no primary doctrine text with this wording. [UNCERTAIN]

---

## 3. Destroyer torpedo attacks under gunfire

| Action | Attackers and launch range | Torpedoes and hits | Losses to gunfire | Source |
|---|---|---|---|---|
| Jutland, 13th Flotilla (day) | 2,000–8,500 yd, mostly 5,000–7,000 | 11, 1 hit (Seydlitz) | Nestor and Nomad stopped by gunfire. The other side also lost 2 DDs. | [BJMH](https://bjmh.gold.ac.uk/index.php/bjmh/article/download/757/879/980) |
| Jutland, German flotillas (day, 20:15) | 13 boats, about 7,000+ yd | 31, 0 hits | 1 sunk, 4 badly damaged | [navalgazing](https://www.navalgazing.net/Jutland-Part-4) |
| Jutland, 4th Flotilla (night) | 800–1,500 yd. Tipperary hit by Westfalen at 1,500–2,000 yd. | probably 1 (Rostock) | 4 lost: Tipperary, Ardent, Fortune to gunfire; Sparrowhawk after collision. German searchlights, starshell and secondaries "highly effective". | same |
| Jutland, 12th Flotilla (dawn) | 1,700–5,000 yd | 12, 1–2 hits (Pommern blew up) | light | same |
| River Plate | Ajax fired at about 9,000 yd | 0 | Graf Spee turned 130° away | [despatch ¶32](https://www.fepow.family/Supplement/London_Gazette/River_Plate_Battle/html/part_ii.htm) |
| Cape Bon, Dec 1941 (night, radar) | under 1,000 m, by surprise, from inshore | 3+ hits, 2 cruisers sunk | none | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Cape_Bon_(1941)) |
| Matapan (night) | battleships at 3,800 yd | Italian DD counter-charge failed | 2 Italian DDs sunk in 5 min | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Cape_Matapan) |
| Second Sirte (day) | about 5,000 yd, "the closest the Italians would allow" | 38, 0 hits | Havock, Kingston, Lively damaged by Littorio and/or Gorizia | [Wikipedia](https://en.wikipedia.org/wiki/Second_Battle_of_Sirte) |
| Komandorski (day) | Bailey at 9,500–10,000 yd | 5, 0 hits | Bailey 2×8 in hits, stopped. Coghlan 1 hit. | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_Komandorski_Islands) |
| North Cape (night, starshell) | Scorpion 2,100, Stord 1,800, Savage 3,500, Saumarez about 1,800 yd. Scharnhorst opened fire at about 10,000 yd and the DDs returned it at 7,000. | 28 torpedoes, 3–4 hits. Second wave 19 more. | Only Saumarez hit (11 killed). Her speed fell to 10 kt. | [Gazette](https://thegazette.co.uk/London/issue/38038/supplement/3708/data.pdf) |
| Barents Sea (Arctic twilight) | Feint attacks only; the threat kept Hipper away | 0 launched | Onslow badly hit (17 killed). Achates and Bramble sunk. | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_Barents_Sea) |
| Samar (day) | Johnston about 10,300 yd. Hoel 9,000 and 6,000. Heermann 4,400. Roberts about 4,000. | Johnston 1 hit on Kumano. Others missed but forced Yamato and Nagato to turn away. | 3 of 4 attacking ships sunk (Hoel, Johnston, Roberts). Heermann heavily hit. | [Wikipedia](https://en.wikipedia.org/wiki/Battle_off_Samar); [destroyerhistory](https://destroyerhistory.org/actions/index.asp?pid=4583); [de413](https://de413.org/category/ships-history/) |
| Surigao (night, radar) | DesRon 54 about 7,000 yd. Arunta section 6,500–7,000. Daly 10,700. | Several hits; Fuso and Yamashiro torpedoed | Only Albert W. Grant hit: up to 22 hits, mostly US friendly fire. | [BatExp 78.1](https://ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.1.html); [destroyerhistory DesRon56](https://destroyerhistory.org/fletcherclass/desron56/) |

**Doctrine on the "suicide" range**

- RN 1916 torpedo handbook: destroyers "should not approach within 7,000 yards gun range" when facing rapid, effective fire ([BJMH](https://bjmh.gold.ac.uk/index.php/bjmh/article/download/757/879/980)).
- Jellicoe, May 1916: keep 3,000–4,000 yd of torpedo running range, and the perpendicular to the enemy line no less than 7,000 yd. Attacking from ahead gave the best chance of avoiding secondary fire (same source).

**[INFERRED] summary**

- **Daylight against intact capital-ship secondaries:** attacks pressed inside about 7,000 yd usually cost 25–75% of attackers sunk or disabled (Jutland 13th Flotilla, Samar, the German 20:15 wave).
- **Night with radar and/or surprise:** attackers fired at 1,000–3,500 yd with losses under about 10% (Cape Bon, North Cape, Surigao). Illumination (searchlight or starshell) is the switch: the 4th Flotilla at Jutland was lit up and destroyed.
- **What attacks achieved:** even failed daylight attacks forced turn-aways of 10–20 min or broke off the enemy (Jutland, Samar, Barents Sea, Sirte). The "fleet in being" effect of the threat often mattered more than hits.

---

## 4. MTB / PT / E-boat / FAC against gunfire

| Case | Numbers | Source |
|---|---|---|
| PT boats, Surigao, 24–25 Oct 1944 (night) | 39 PTs; 30 came under fire; 10 hit; 1 lost (PT-493, 3×4.7 in hits); 3 killed and 20 wounded. 15 boats fired 35 torpedoes at 400–4,000 yd (most 700–3,000). Radar contact at 8–10 nmi. Japanese used searchlights and starshell; one DD chased PT-152 for 23 min. Results: no hits on Nishimura; 1 hit on Abukuma (Shima). | [At Close Quarters pt VIII](https://ibiblio.org/hyperwar/USN/CloseQuarters/PT-8.html); [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Surigao_Strait) |
| PTs against the Tokyo Express, 1942–43 | Launch ranges 200–2,000 yd. 10/11 Jan 1943: 17 torpedoes, no confirmed damage; PT-43 and PT-112 lost to DD salvos. 1 Feb 1943: 19 torpedoes; 3 PTs lost. Searchlights, then gunfire. PTs survived on speed, smoke and zigzag. | [At Close Quarters pt III](https://ibiblio.org/hyperwar/USN/CloseQuarters/PT-3.html) |
| All USN PT losses, WWII | 69 total. Enemy surface ships 8, shore batteries 5, aircraft 7, mines 4, grounding 20, friendly fire 7. Gunfire from enemy ships was the cause in only about 12%. | [At Close Quarters App. B](https://ibiblio.org/hyperwar/USN/CloseQuarters/PT-B.html) |
| RN MGB against E-boat (Hichens), 1941–42 | Closed to 20–100 yd. About 8 min of action set 2 E-boats on fire. Detection by listening at several miles was preferred to sighting at a few hundred yards. | [Coastal Forces Heritage](https://coastal-forces.org.uk/monument-fundraising/coastal-forces-heroes/robert-hichens-dso-dsc/) |
| E-boats overall | About 191 built. Losses by gunfire from MGBs or escorts listed (S29, S77, S88, S183, S185, S190, S193), plus many to mines and aircraft. Claims: 101 merchant ships and 12 destroyers. 43.5 kt sustained. | [Wikipedia](https://en.wikipedia.org/wiki/E-boat) |
| Gulf of Tonkin, 2 Aug 1964 (day) | Maddox ordered to fire inside 10,000 yd and fired 283 5 in rounds in about 20 min. P-4s at 30+ kt, closing at 50+ kt. T-339 launched 2 torpedoes at about 3,000 yd; all torpedoes missed (Maddox manoeuvred). Wikipedia gives about 1 direct 5 in hit, which is about 0.35%. T-339 set afire and dead in the water; T-336's engines knocked out. Some damage came from F-8 strafing. Maddox took one 14.5 mm hit. Sources conflict on how many rounds were 5 in versus 3 in and on launch range. [UNCERTAIN] | [American Heritage](https://www.americanheritage.com/what-happened-gulf-tonkin); [Wikipedia](https://en.wikipedia.org/wiki/Gulf_of_Tonkin_incident); [Military Times](https://www.militarytimes.com/veterans/2017/12/13/how-north-vietnams-p-4s-missed-the-mark/) |
| Tonkin, 4 Aug 1964 (night, probably phantom) | 249 5 in + 123 3 in rounds fired; more than 20 torpedo attacks reported; no boats present. Shows how night radar gunfire against "FACs" can be generated by false contacts. | [USNI NH 2008](https://m.usni.org/magazines/navalhistory/2008-02/truth-about-tonkin) |
| Latakia, 1973 | Gabriel missiles crippled the targets and 76 mm guns finished the cripples (K-123, a grounded Komar). Chaff defeated 8–12 Styx. | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Latakia) |
| Operation Trident, 1971 | Night Styx strikes from Osa boats. No gunfire role recorded. | [Wikipedia](https://en.wikipedia.org/wiki/Operation_Trident_(1971)) |
| Vincennes against IRGCN boats, 3 Jul 1988 | About 100 5 in rounds: 2 boats sunk, 1 damaged by near miss. Forward mount jammed. Hard rudder at high speed to bring the aft mount to bear. Boats on "erratic courses". | [H-gram 020](https://history.navy.mil/about-us/leadership/director/directors-corner/h-grams/h-gram-020/h-020-1-uss-vincennes-tragedy--.html) |
| Praying Mantis, 18 Apr 1988 | Boghammers were hit by A-6 Rockeye, not guns. Joshan was wrecked by SM-1s and then sunk by gunfire. No round counts published. | [Wikipedia](https://en.wikipedia.org/wiki/Operation_Praying_Mantis) |
| Bubiyan, 1991 | Iraqi FACs killed by Lynx Sea Skua and aircraft. No naval gunfire role. | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Bubiyan) |

**[INFERRED] takeaways**

1. A 5 in gun with 1940s–60s directors scored about 0.3–2% per round against a 30–50 kt boat jinking at 3,000–10,000 yd. This is consistent with Tonkin (about 1 hit in 283 rounds) and Vincennes (about 2 kills in about 100 rounds).
2. Against FACs, the gun's real job after 1970 was to finish cripples.
3. Night PT attacks on alert destroyers with searchlights or starshell were costly in boats hit (about 33% at Surigao) but seldom fatal (about 3% lost). Speed, smoke and the boat's small size kept them alive. Their torpedo hit rate was very low (about 3%).

---

## 5. Fire-control countermeasures against evaders

| Countermeasure | Description and numbers | Source |
|---|---|---|
| **Optimum dispersion** | Hits peak at a pattern spread of about 4/5 of the expected MPI error (Schuler, 1911). Tight patterns are not always best. | [USNI 1911](https://www.usni.org/magazines/proceedings/1911/september/note-salvo-dispersion) |
| **Ladder / pattern fire** | Salvos at the estimate, +a few hundred yd, and −a few hundred yd. Became more important "as ships were expected to maneuver". USN manual: ladders are "not particularly adaptable to fast-moving targets". | [navalgazing](https://navalgazing.net/Spotting); [NO&G 18C](https://eugeneleeslover.com/USNAVY/CHAPTER-18-C.html) |
| **Rocking ladder** | After a straddle, rock the MPI by +100 / 0 / −100 yd with arbitrary spots at the rangekeeper, spotting only on the zero salvos. Explicitly enlarges the effective pattern against "targets capable of rapid maneuvering". | [NO&G 18C5](https://eugeneleeslover.com/USNAVY/CHAPTER-18-C.html) |
| **Bracket and halve** | RN: 400 → 200 → 100 yd. USN: never spot below the pattern size, then centre on deflection. | [navalgazing](https://navalgazing.net/Spotting); [NO&G](https://eugeneleeslover.com/USNAVY/CHAPTER-18-C.html) |
| **Barrage** | Hold the gun range constant in a zone short of or over the target and let a fast light target run through it, then shift the zone. Used with fast-firing small calibres. Also proposed with secondaries against salvo-chasers. | [NO&G 18C](https://eugeneleeslover.com/USNAVY/CHAPTER-18-C.html); [navalgazing](https://navalgazing.net/Spotting) |
| **Lead for high-speed targets** | Hold the deflection MPI abaft the point of aim to allow for travel during splash formation. | [NO&G](https://eugeneleeslover.com/USNAVY/CHAPTER-18-C.html) |
| **Hit-probability sensitivity** | 5"/38 example: MPI centred gives P(hit) 0.372. MPI half a pattern (130 yd) off in range drops it to 0.116, a 3.2× drop. One unanticipated turn costs about this much. [INFERRED] | [NO&G 18A](https://eugeneleeslover.com/USNAVY/CHAPTER-18-C.html) |
| **Radar FC lag** | ComCruDiv 13 (1944): against "rapidly and radically maneuvering targets" radar lag causes large errors, especially in deflection, and fire may consistently "just miss". | [BatExp 78.3](https://ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.3.html) |
| **Faster solution** | The Mk 1 rangekeeper was "sluggish" and Mk 8 reached a solution much faster, so the Mk 1 was modified on Mk 8 principles. The Mk 1A redesigned target-parameter updating, and slew buttons reset the vector solver. | [Wikipedia Mk 1](https://en.wikipedia.org/wiki/Mark_I_Fire_Control_Computer) |
| **Radar following with steady straddles** | Surigao: West Virginia's 13 full salvos were all straddles at 22,800 yd against a non-evading column. North Cape: Duke of York 31 of 52 salvos straddled. Straddle rates fall sharply when the target turns. [INFERRED] | [BatExp 78.3](https://ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.3.html); [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_North_Cape) |
| **Close the range** | Cuts ToF. Samar cruisers closed to about 10,000 yd before Gambier Bay was crippled. Final Bismarck action: about 80 heavy hits from 714 rounds (about 11%) against a target that could not manoeuvre. | [Wikipedia](https://en.wikipedia.org/wiki/USS_Gambier_Bay); [navweaps tech-016](https://navweaps.com/index_tech/tech-016.php) |
| **Crossfire / concentration** | Two bearings mean that one target turn cannot open the range for both shooters. Used at Bismarck (KGV/Rodney) and North Cape (DoY plus cruisers). The cost: shooters confuse each other's splashes. KGV ranged on Rodney's splashes until 09:10. | [navweaps tech-016](https://navweaps.com/index_tech/tech-016.php) |
| **Adaptive tracking filters (1970s)** | TASC/NAVORD 1974: for highly manoeuvrable targets, prediction error is "the most significant error source" in gun fire control. Adaptive-bandwidth filters were best overall, but none beat a fixed filter at high manoeuvre rates. Doubling the data rate from 10 to 20 Hz helps. | [DTIC ADA023015](https://apps.dtic.mil/sti/pdfs/ADA023015.pdf) |
| **Constant-speed turn constraint (1991)** | NSWC: a velocity ⟂ acceleration pseudomeasurement improves Kalman tracking of turning targets. | [DTIC ADA255988](https://apps.dtic.mil/sti/pdfs/ADA255988.pdf) |
| **Closed-loop spotting (CIWS)** | Phalanx tracks its own outbound rounds and the target, and drives the miss distance to zero, which raises lethality by an estimated order of magnitude. Block 1B surface mode: FLIR, 50-round bursts against boats. | [navalgazing Phalanx](https://www.navalgazing.net/Phalanx) |

**How much did manoeuvring reduce hits? [INFERRED]**

| Situation | Measured heavy-gun hit rate | Source |
|---|---|---|
| Steady or crippled target | Bismarck final action about 11% | [navweaps](https://navweaps.com/index_tech/tech-016.php) |
| Long range, target turning or smoke-obscured | Falklands 1,174×12 in for about 80 hits ≈ 7% | [navweaps OOB](https://www.navweaps.com/index_oob/OOB_WWI/OOB_WWI_Falklands.php) |
| Long range, target turning or smoke-obscured | Dogger Bank, British 1,150 heavy rounds ≈ 7 hits on the BCs (≈ 0.6%) | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Dogger_Bank_(1915)) |
| Long range, target turning or smoke-obscured | Matapan day, Italian cruisers 535 rounds with 0 hits; Vittorio Veneto 94 with 0 | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Cape_Matapan) |
| Chasing or zigzagging targets | Komandorski and Java Sea about 0.4–0.5% | §1.2 |

- The overall reduction is plausibly 3–10× against an energetic evader at long range.
- The data are confounded by range, smoke, visibility and crew quality, so this is not a controlled measurement.

---

## 6. Operations research

- **Isaacs, "The Problem of Aiming and Evasion" (RAND P-642, 1951)** ([DTIC AD0604643](https://apps.dtic.mil/sti/pdfs/AD0604643.pdf); [RAND](https://www.rand.org/pubs/papers/P642.html)).
  - Setup: a marksman fires at a target that moves left or right one step per time unit. The time lag n is the projectile's flight time plus aiming delay.
  - Results: hit probability is 0.5 at n = 1 and about 0.302 at n = 2. The evader's optimal play is a unique mixed strategy. The marksman has only ε-optimal strategies.
  - Principle: the evader should keep himself equally likely to be anywhere in his reachable set while avoiding patterns that can be extrapolated.
  - Implication: hit probability falls roughly with the size of the reachable set, which grows with (ToF)². [INFERRED]
- **Optimum evasion period.** For a ship, the reachable set during ToF is set by rudder lag and turning radius. [INFERRED: at 30 kt with about a 450 m turning radius, lateral acceleration ≈ v²/R ≈ 0.5 m/s². After about 5 s of rudder lag, the ship departs from the predicted straight track by ½·a·t²: about 40 m at a 20 s ToF, 160 m at 30 s and 300 m at 40 s. Compare a beam of 20–30 m and 8–16 in patterns of 200–500 yd.] Evasion therefore buys little at ToF under about 15 s and dominates at ToF over about 30 s. The ideal turn period is of order ToF: change the rudder about once per enemy salvo cycle. That is exactly what salvo chasing does.
- **Zigzag studies.** The WWII zigzag plans were anti-submarine. Large slow formations (under 12 kt) got little from zigzagging and used radical course changes at intervals of an hour or more ([WarInst 7](https://ibiblio.org/hyperwar/USN/ref/WarInst/WarInst-7.html)). I found no OR study quantifying zigzag against surface gunfire. [UNCERTAIN]
- **Tracking research (1960s–90s).** Work moved to filters that detect and adapt to manoeuvres: submarine zig detection by statistical bearing processing ([DTIC AD0329725](https://apps.dtic.mil/sti/pdfs/AD0329725.pdf)), multiple-model adaptive estimators ([DTIC AD0754387](https://apps.dtic.mil/sti/pdfs/AD0754387.pdf)), and adaptive-bandwidth gun filters ([DTIC ADA023015](https://apps.dtic.mil/sti/pdfs/ADA023015.pdf)). The shared conclusion is that a manoeuvre produces a transient bias. You can shorten the transient but not remove it.

---

## 7. Suggested simulation rules [INFERRED]

- **Prediction.** Gun aim uses a linearly extrapolated target track with tracking lag τ: about 60–120 s for a WWI clock or Dreyer table, about 20–40 s for a WWII Mk 1/Mk 8 with optical tracking, about 10–20 s with radar, and 2–5 s for a 1970s+ digital filter. The miss is the target's actual position at impact minus its predicted position, added to the dispersion.
- **Evader AI options:** none, zigzag on a fixed period, chase salvos (on each splash, turn toward it with hard rudder), or random (Isaacs mixed strategy).
- **Shooter counters:**
  - rocking ladder: multiplies the effective pattern by 1.3–1.5 and lowers peak hit probability;
  - barrage, for small calibres against FACs;
  - close the range;
  - concentrate from a second bearing;
  - better tracking: lower τ.
- **Targets to calibrate against:**
  - heavy guns at 15–25 kyd against a chasing evader: 0.3–1%;
  - against a steady target: 5–11%;
  - 5 in against a 30–40 kt boat: about 0.5–2% per round;
  - daylight DD attack inside about 7 kyd against an intact BB or CA: about 30–75% chance each attacker is sunk or disabled;
  - night radar attack: under about 10%.
