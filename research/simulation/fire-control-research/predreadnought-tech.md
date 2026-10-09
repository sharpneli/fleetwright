# 07 — Pre-director naval gunnery, 1860–1910: human limits of the gunlayer and gun crew
Status: final    Updated: 2026-10-09    Request: -

Scope: ironclads, pre-dreadnoughts and early dreadnoughts before directors and mechanical fire-control tables. The focus is the gunlayer (pointer), trainer, sight-setter and loaders, and the numbers a simulator needs.
Tags: [INFERRED] means my derivation or model suggestion. [UNCERTAIN] means the source is thin or sources conflict. A URL follows each sourced fact.

---

## 0. Executive summary for the model

| Era | Typical laying | Sight | Rangefinding | Practical battle range | Battle hit rate (all calibres) |
|---|---|---|---|---|---|
| 1860s–1870s ironclads (Lissa 1866) | Firing on the roll, open sights, gun captain lays and fires | Notch/bead, tangent leaf | Eye estimate | "Point blank", a few hundred yards, ramming | Italians fired ~1,450 shots at Lissa without seriously damaging any Austrian ship ([wiki](https://military-history.fandom.com/wiki/Battle_of_Lissa_(1866))) |
| 1880s (Alexandria 1882) | Same; fixed directors (broadside converging on a point at 800 or 1,100 yd) | Open sights | Eye estimate, Watkin instruments ashore | 1,000–2,000 yd | ~3,000 rounds for ~10 hits on enemy guns ([Scott memoir](https://www.naval-history.net/WW0Book-Adm_Scott-50YearsinRN.htm); [navalgazing Directors](https://www.navalgazing.net/Directors)) |
| 1894 (Yalu) | Roll firing; QF medium guns | Open sights, a few telescopes | Early B&S FA on some ships [UNCERTAIN] | ~2,000–3,000 m | Not counted. "Wild" firing ([USNI 1895](https://www.usni.org/magazines/proceedings/1895/october/professional-notes)) |
| 1898 (Santiago, Manila) | Roll firing, separate pointer, telescopes poorly mounted | Mixed | Fiske stadimeter aloft (Manila), Fiske RF unreliable | 1,100–6,000 yd | ~121 hits from ~9,500 shots = 1.3% ([Morison via IITK](https://cse.iitk.ac.in/users/amit/books/morison-1966-men-machines-modern.html); [Wikipedia Sims](https://en.wikipedia.org/wiki/William_Sims)); "<3%" ([navalgazing](https://navalgazing.net/Spanish-American-War-Part-8)) |
| 1904 (Yellow Sea) | Continuous aim spreading (medium guns); heavy guns still roll-fired | Telescopes | B&S FA2/FA3 | 15 km opening, main action ~5.6 km | ~1.7% overall; Japanese 12": 30/603 = 4.7% ([Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_Yellow_Sea); [navweaps](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php)) |
| 1905 (Tsushima) | As above, better ranging and spotting | Telescopes | B&S FA3, "accurate to ~8,000 yd" | Mostly ≤6,500 yd | Japanese 12": ~40/446 = 9% ([navweaps](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php)) |
| 1912 (Orion trial, end state of independent laying) | Independent layers, continuous aim, salvos | Telescopes | B&S 9 ft | 9,000 yd, heavy sea | Orion (independent): 4/27 ≈ 15%. Thunderer (director): 26/39 ≈ 67% ([navalgazing Directors](https://www.navalgazing.net/Directors)) |

Model takeaways [INFERRED]:
1. Before about 1898, the main error is the human laying error in elevation from firing on the roll, roughly 10–30 arcmin, plus range-estimate error. Sight optics matter less.
2. Between 1898 and 1905, continuous aim plus telescopes cut laying error to about 1–3 arcmin on medium guns. Range knowledge and spotting then become the bottleneck.
3. From 1904 to 1910, ranges reach 5–10 km. Independent fire stops working because splashes cannot be attributed, which forces salvo fire and fire control from aloft.

---

## 1. Laying methods

### 1.1 Firing on the roll (to c. 1900 for all guns; to about 1910 or later for heavy guns)
- **How it worked:** The layer (gun captain) set elevation, then waited for the roll to carry the sight line across the target and fired at that instant. Scott, describing gunboat practice around 1880, says the layer fired on the upward roll and had to aim behind the target to allow for own ship's motion ([Scott memoir](https://www.naval-history.net/WW0Book-Adm_Scott-50YearsinRN.htm)).
- **Roll numbers from the USS *Amphitrite* 10-inch study** ([USNI 1901, "Notes on firing interval"](https://www.usni.org/magazines/proceedings/1901/january/notes-firing-interval-examples)):
  - Extreme roll ±8° with a stated 6-second period. The muzzle swings about 3.8 ft above and below horizontal in 3 s, with the muzzle 27 ft from the roll axis.
  - Mean roll rate 1⅓°/s; maximum (mid-roll) assumed 2⅔°/s.
  - **Firing interval** (decision to shot leaving the muzzle): assumed 0.2 s. Lt Cdr Fletcher measured 0.3 s on average. The interval is longer with electric firing than with a lanyard.
  - Error at mid-roll: 2⅔°/s × 0.2 s ≈ 32 arcmin of elevation. At 2,100 yd (time of flight about 4 s), this put a shot about 450 yd over when rolling away, or about 557 yd short when rolling toward the target.
  - Recommendation: fire at the end of the roll, when angular velocity is near zero.
- **Later doctrine:** Firing timing was first set at one end of the roll, but mid-roll was later found better because the motion there is more consistent ([navalgazing Directors](https://www.navalgazing.net/Directors)). [UNCERTAIN] The two sources conflict. End-of-roll minimises rate error, but the end point is hard to predict. Mid-roll is predictable but fast.
- **Reaction measurement:** Scott's "Foolometer" (personal error machine) measured each man's delay between signal and pulling the lanyard. No timings survive in the excerpt ([Scott memoir](https://www.naval-history.net/WW0Book-Adm_Scott-50YearsinRN.htm)).
- **Skill spread:** Scott says that of many hundreds of seamen trained, only 1–2% could hit reliably when firing on the roll. One exceptional layer hit within a foot of a small flagstaff at 1,000 yd ([Scott memoir](https://www.naval-history.net/WW0Book-Adm_Scott-50YearsinRN.htm)).
- **Platform:** The Royal Sovereign class ("Rolling Ressies") rolled badly until bilge keels were fitted in 1894–95. *Hood*'s GM of 4.1 ft (vs 3.6 ft) gave a roll period about 7% shorter, and her gunnery was rated worse. Main-deck 6" guns could only be used in calm weather ([Wikipedia](https://en.wikipedia.org/wiki/Royal_Sovereign-class_battleship)).

**Model for roll firing [INFERRED]**
- Elevation error ≈ ω(t)·Δt, plus anticipation error.
- Δt: mean 0.2–0.3 s; skilled σ ≈ 0.03–0.05 s, average σ ≈ 0.1 s.
- Pre-dreadnought roll: amplitude 3–8°, full period 8–14 s. Hence ω_max = 2πA/T ≈ 1.5–6°/s.
- Results: a skilled layer firing near the end of the roll has σ_elev ≈ 5–10 arcmin; an average layer firing mid-roll has σ_elev ≈ 15–35 arcmin.
- Training error is smaller because roll mainly disturbs elevation. Yaw adds about 2–5 arcmin.

### 1.2 Continuous aim (Scott 1898 on *Scylla*; Sims in the USN from 1901–02)
- **How it worked:** The layer kept the cross-wire on the target throughout the roll by working the elevating handwheel, firing when on. Scott achieved this on *Scylla* by regearing the guns (higher handwheel ratio, meaning fewer turns per degree) and fitting telescopic sights ([USNI Naval History 2015](https://www.usni.org/magazines/naval-history-magazine/2015/april/continuous-aim-fire-learning-how-shoot)).
- **Practical limit:** Up to about 9.2-inch guns. Main battleship guns could not elevate fast enough ([navalgazing Fire Control 1](https://www.navalgazing.net/Fire-Control-Part-1); [Directors](https://www.navalgazing.net/Directors)).
- **US BuOrd objection:** Five men on a 6" elevating gear could not generate enough power to follow a 5° roll in 10 s, so the Bureau argued continuous aim was impossible ([Morison via IITK](https://cse.iitk.ac.in/users/amit/books/morison-1966-men-machines-modern.html)). Sims showed the gear ratios, not the men, were the limit.
- **Gear ratios:** No published gear-ratio numbers were found. [UNCERTAIN]
  - Required tracking rate [INFERRED]: ω_max ≈ 2–4°/s (±5–8° roll, 8–12 s period).
  - A man can comfortably spin a handwheel at about 1–2 rev/s [INFERRED]. So a gear of about 1–3° per handwheel turn is needed, against perhaps 0.2–0.5° per turn on 1890s mounts designed for precision rather than speed [INFERRED].
- **Results:**
  - *Terrible*'s accuracy rose about tenfold and rate of fire nearly fourfold. The US system was claimed to double the speed of hitting and to raise battery effectiveness by 500% ([USNI 2015](https://www.usni.org/magazines/naval-history-magazine/2015/april/continuous-aim-fire-learning-how-shoot)).
  - In 1899, five North Atlantic Squadron ships each fired 5 minutes at a hulk at 1,600 yd: 2 hits in 25 minutes. About six years later, one pointer made 15 hits in 1 minute on a 75×25 ft target at 1,600 yd, half of them in a 50-inch bull ([Morison via IITK](https://cse.iitk.ac.in/users/amit/books/morison-1966-men-machines-modern.html)).
- **Residual error under continuous aim:** Sims gave a pointer's aiming error of about 2 ft at 1,600 yd (about 1.4 arcmin) and the gun's unavoidable errors as about ±4.5 ft at 1,600 yd (about 3 arcmin). Together these give about 4 ft of vertical dispersion at 1,600 yd, 8 ft at 3,200 yd and 15 ft at 6,000 yd ([USNI 1904, Sims](https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing)).

### 1.3 Separate layer and trainer; sight-setter
- Scott added a dedicated **sight-setter** in 1898, so the layer no longer took his eye off the target to set range and deflection. The Admiralty took about four years to adopt it ([USNI 1949](https://www.usni.org/magazines/proceedings/1949/april/admiral-sir-percy-scott-and-british-naval-gunnery); [Wikipedia Scott](https://en.wikipedia.org/wiki/Percy_Scott)).
- Medium and heavy mounts split the work: the layer worked elevation and fired, and the trainer worked bearing. Each needed his own telescope for continuous aim. Coordination is a source of lateral error [INFERRED].
- Turrets carried one aimer each. Because the guns were laid individually, firing one gun could disturb the other, so the two guns in a turret were not fired together ([navalgazing Directors](https://www.navalgazing.net/Directors)).
- **Aim point:** The waterline or centre of the target. Danger space made waterline aim attractive at short range [INFERRED].

### 1.4 Fixed directors (1870s–1880s)
- All broadside guns were laid to converge on a point abeam at 800 or 1,100 yd and fired together when the ship's director sighted the target. The method became obsolete around 1890 as speeds and ranges grew ([navalgazing Directors](https://www.navalgazing.net/Directors)).

---

## 2. Sights

### 2.1 Open sights
- **Bar or open sights** required the eye to hold three things in focus at once: the rear sight about 15 in away, the foresight about 50 in away, and the target at range ([USNI 1896](https://www.usni.org/magazines/proceedings/1896/july/telescopic-sights-guns); also Scott's account, [memoir](https://www.naval-history.net/WW0Book-Adm_Scott-50YearsinRN.htm)).
- An open sight showed about 9.5° horizontally by 3° vertically ([USNI 1909, Fiske](https://www.usni.org/magazines/proceedings/1909/june/invention-and-development-naval-telescope-sight)).
- **1892 *Yorktown* comparison** ([USNI 1912](https://www.usni.org/magazines/proceedings/1912/june/relative-importance-turret-and-telescope-sight)):

| | Mean vertical deviation | Mean lateral deviation |
|---|---|---|
| Open sight | 6.54 ft | 8.90 ft |
| Telescope | 2.25 ft | 2.75 ft |

- The ratios are about 2.9:1 vertical and 3.2:1 lateral. The same source extrapolates to about 2.7× the hit probability at 10,000 yd, and about 8.4× against small targets.
- The trial range is not given in the excerpt. [UNCERTAIN] Assuming about 1,200–1,600 yd [INFERRED], the open sight gives about 4.5–6 arcmin mean vertical error and the telescope about 1.6–2 arcmin.

### 2.2 Telescopic sights
- **Early history:** Telescopes were tried in 1857 (Younghusband), 1875 (Scott), about 1888 (Grenfell) and 1890 (Fiske patent application) ([USNI 1896](https://www.usni.org/magazines/proceedings/1896/july/telescopic-sights-guns)).
- **Fiske sight** ([USNI 1909](https://www.usni.org/magazines/proceedings/1909/june/invention-and-development-naval-telescope-sight)):
  - Telescope sight applied for March 1891, granted 1893.
  - 4× magnification, 8° field, 2-inch objective, about 24 in long.
  - First trial on *Yorktown* in spring 1892. The executive officer was cut over the eye by recoil, and the captain called the sight "of no value" in its then-current form.
  - Recoil shock also disturbed the alignment: on *San Francisco* (1893) the worm shaft bent, and the range disc had to be reset after every shot.
- **USN mid-1890s design** ([USNI 1896](https://www.usni.org/magazines/proceedings/1896/july/telescopic-sights-guns)):
  - Only about 1.5×, with a ~17° field and a 2-inch objective.
  - Thick cross-wires so they stayed visible at night; about 0.4 in of tolerance for head movement.
  - "Nearly useless" in damp or misty weather, so open finder sights were retained.
  - Mounting on guns that recoil in a sleeve kept shock off the telescope.
  - A 10-inch gun with the telescope took 2 min 25 s between shots, about the same as with bar sights, but scored the best result recorded for that calibre.
- **USN 2-inch Model 1906 sight** ([manual](https://allworldwars.com/Description%20of%202-inch%20Telescopic%20Sights%20Model%20of%201906.html)):
  - 8×, 11° field, Porro prisms.
  - Rubber eyepiece hood against recoil.
  - Lamp-lit cross-wires and scales; amber glare filter.
  - Deflection and elevation scales read to 0.1° (6 arcmin), elevation scale 0–16°.
  - Fogging was cleared by gently warming the telescope.
- **Sims-era problems:** Telescope mountings and scales were poorly built ([Stirling memoir](https://penelope.uchicago.edu/Thayer/E/Gazetteer/People/Yates_Stirling/Sea_Duty/8*.html)). Sims modified sights and bought telescopes on the Asiatic Station ([USNI 1921](https://www.usni.org/magazines/proceedings/1921/september/gunnery-and-turret-design)).
- **RN adoption:** Scott fitted telescopes to *Scylla* (1898) and *Terrible*. On 11 January 1904 he wrote urging that all Navy guns be resighted ([USNI 1949](https://www.usni.org/magazines/proceedings/1949/april/admiral-sir-percy-scott-and-british-naval-gunnery)). Fleet-wide fitting probably fell around 1902–06 [INFERRED/UNCERTAIN].
- **Model parameters [INFERRED]:**
  - Open sight: σ_aim ≈ 3–6 arcmin in calm conditions, plus the roll-timing error from §1.1.
  - Telescope with continuous aim: σ_aim ≈ 1–2 arcmin.
  - Telescope penalties: a narrow field means slower target acquisition (about 2–5 s to find a target, [INFERRED]); performance degrades in mist and spray; after-shot misalignment on early mounts.

### 2.3 How ranges and deflections reached the gun
- Ranges went by voice pipe, messenger or shouting, and later by dial transmitters.
- Scott submitted an electrical range transmitter with dials in hundreds of yards to the Admiralty in 1881. Nothing comparable was supplied for about 25 years ([Scott memoir](https://www.naval-history.net/WW0Book-Adm_Scott-50YearsinRN.htm); [USNI 1949](https://www.usni.org/magazines/proceedings/1949/april/admiral-sir-percy-scott-and-british-naval-gunnery)).
- Electrical range transmission to the guns began around 1900 ([navalgazing Rangekeeping 1](https://www.navalgazing.net/Rangekeeping-Part-1)).
- In the USN, Fiske's electrical range indicator was tested on *San Francisco* in 1893–94. By June 1896, six battleships and cruisers had been fitted ([USNI 1909](https://www.usni.org/magazines/proceedings/1909/june/invention-and-development-naval-telescope-sight)).
- **Delay and quantisation [INFERRED]:**
  - Voice: 5–15 s delay, with occasional mishearing (about 1–5% of orders garbled).
  - Dial transmitters: 1–3 s, in 25–100 yd steps. The Vickers clock dial reads in 25-yd subdivisions ([Wikipedia](https://en.wikipedia.org/wiki/Vickers_range_clock)).

---

## 3. Mounts, loading and rates of fire

| Gun (period) | Rate of fire | MV | Shell | Max range / elevation | Source |
|---|---|---|---|---|---|
| RML 12" 35-ton (1870s, *Devastation*) | [UNCERTAIN] ~1 round per 2–4 min [INFERRED] | 1,390 fps | 706 lb Palliser | — | [Wikipedia](https://en.wikipedia.org/wiki/RML_12-inch_35-ton_gun) |
| BL 13.5"/30 Mk I (1880s–90s, Admirals, Royal Sovereign) | 0.3–0.5 rpm; 4 rounds in 9 min in trial; ~1 per 2 min for a trained crew; loads only at 0° train | 2,016 fps (SBC, brown "cocoa" powder) / 2,099 fps (cordite) | 1,250 lb | 11,950–12,620 yd at 13.5° | [navweaps](https://www.navweaps.com/Weapons/WNBR_135-30_mk1.php) |
| USN 13"/35 (Indiana, Kearsarge; 1890s) | ~0.2 rpm with black powder (5 min per round: [Stirling](https://penelope.uchicago.edu/Thayer/E/Gazetteer/People/Yates_Stirling/Sea_Duty/8*.html)); ~1 rpm by c.1915 | 2,000 fps | 1,130 lb | ~12,000 yd at 15° | [navweaps](https://www.navweaps.com/Weapons/WNUS_13-35_mk1.php) |
| Canet 32 cm (*Matsushima*, 1894) | Max ~2 rounds per hour; 4 shots in the whole Yalu battle | — | 450 kg AP | eff. 8,000 m | [Wikipedia](https://en.wikipedia.org/wiki/Japanese_cruiser_Matsushima); [historyofwar](https://www.historyofwar.org/articles/battles_naval_yalu_river.html) |
| BL 12"/35 Mk VIII (Majestic, 1895) | 1 round per 70 s from ready rounds, then 1 per 100 s; later mounts 1.33 rpm; *Vengeance* 1.9 rpm. Majestic loads at 13.5° and 0° train | 2,350 fps (cordite) | 850 lb | 13,900 yd at 13.5° | [navweaps](https://www.navweaps.com/Weapons/WNBR_12-35_mk8.php) |
| BL 12"/40 Mk IX (Formidable, KE VII; 1901) | ~1.5 rpm. Jellicoe 1906: 2 rpm in the gunlayer's test, 1 rpm in battle practice | 2,525–2,612 fps | 850 lb | 15,150–15,600 yd at 13.5° | [navweaps](https://www.navweaps.com/Weapons/WNBR_12-40_mk9.php) |
| Japanese 12"/40 EOC (Fuji, Shikishima, Mikasa) | Optimum 0.5–1.5 rpm; **in combat 0.2–0.75 rpm** | 2,400 fps | 850 lb | ~15,000 yd at 15° | [navweaps](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php) |
| USN 12"/40 Mk 3/4 (Maine, Virginia) | 0.66 rpm as commissioned; ~2 rpm after c.1906 | 2,400–2,800 fps | 870 lb | 19,000 yd at 15.5°; fire control limited to ~10,000 yd | [navweaps](https://www.navweaps.com/Weapons/WNUS_12-40_mk3.php) |
| QF 6"/40 (1892 on) | 5–7 rpm; hand elevation and training only | 1,882 fps (powder) / 2,154–2,230 fps (cordite) | 100 lb | ~10,000 yd at 15°; casemate limit +15° | [navweaps](https://navweaps.com/Weapons/WNBR_6-40_mk1.php); [Wikipedia](https://en.wikipedia.org/wiki/QF_6-inch_naval_gun) |
| QF 12-pdr 12 cwt | 15 rpm; manual pedestal mount | 2,600 fps | 12.5 lb | 9,300 yd at 20° | [navweaps](https://www.navweaps.com/Weapons/WNBR_3-50_mk1.php) |
| USN heavy turrets, before vs after Sims | 300 s per round → 30 s per round | — | — | — | [Stirling](https://penelope.uchicago.edu/Thayer/E/Gazetteer/People/Yates_Stirling/Sea_Duty/8*.html) (memoir, probably exaggerated [UNCERTAIN]) |

**Aimed hits per gun per minute (RN prize firing, medium guns):** *Scylla* 4.6 (1899) → *Barfleur* 5.7 (1901) → *Good Hope* 6.6 (1907). In 1901 PO Grounds made 8 hits from 8 rounds in one minute ([USNI 1949](https://www.usni.org/magazines/proceedings/1949/april/admiral-sir-percy-scott-and-british-naval-gunnery)). In other words, the best 6" layers were hitting at close to the gun's mechanical rate.

**Loading-cycle notes:**
- Early heavy mounts had to return to a fixed loading position before each round: 0° train for the 13.5" and Majestic 12"; 13.5° elevation for the Majestic and 4.5° for the BVI; +10° or +2° for the USN 13".
- Any-angle loading arrived with *Vengeance*'s BV and the BVII mounts ([navweaps 12"/35](https://www.navweaps.com/Weapons/WNBR_12-35_mk8.php); [12"/40](https://www.navweaps.com/Weapons/WNBR_12-40_mk9.php)).
- Returning to the loading position cost each round the time to slew back and re-acquire [INFERRED: +15–40 s on a hydraulic mount].
- Ready-use ammunition sets the burst rate: the Majestic class fired faster until its ready rounds ran out (70 s per round, then 100 s). The BVI mount held 2 ready rounds per gun, later 5 ([navweaps](https://www.navweaps.com/Weapons/WNBR_12-40_mk9.php)).
- Combat rate fell to roughly 30–60% of drill rate: Japanese 12" optimum vs combat figures; Jellicoe's gunlayer's test vs battle practice figures.

**Training and elevation speeds:** navweaps lists "N/A" for these on pre-dreadnought heavy mounts. Hydraulic heavy mounts probably trained at about 1–3°/s and elevated at about 1–3°/s; hand-worked 6" mounts managed about 2–5°/s at a high gear ratio [INFERRED/UNCERTAIN].

---

## 4. Propellant smoke and funnel smoke

- **Black and brown powder:** Black powder is 75% saltpetre, 15% charcoal, 10% sulphur; brown "cocoa" powder is 79/18/3 ([dreadnoughtproject](https://www.dreadnoughtproject.org/tech/colossus/gunpowder.php)).
  - Only about 35% of brown powder's mass becomes useful gas. A USNI author estimates about 23 lb of smoke per heavy charge ([USNI 1901](https://www.usni.org/magazines/proceedings/1901/october/smokeless-powder)).
- **Obscuration after a 13-inch shot in calm weather:** a point 4 miles away became visible again 25 s after a brown-powder round, versus 5 s after a smokeless round ([USNI 1901](https://www.usni.org/magazines/proceedings/1901/october/smokeless-powder)).
  - Smokeless 6" rounds did not obscure the target at all. Smoke from smokeless 13" rounds came mainly from the black-powder igniter (about 14 lb) and cleared quickly.
- **Effect on rate of fire with black or brown powder [INFERRED]:**
  - The layer of a heavy gun cannot re-aim for about 15–30 s in calm air, so the effective rate is the lower of the mechanical rate and 1 shot per ~30 s.
  - In wind of 10+ kt, clearing time falls to about 5–10 s, but smoke drifts down onto the leeward battery and the target bearing.
  - In a QF battery firing black powder (1880s–early 1890s), continuous fire builds a smoke bank. Independent fire then degrades to "fire at the flashes".
- **Battle accounts:** Smoke and battle stress are cited as partial causes of the Santiago misses ([navalgazing](https://navalgazing.net/Spanish-American-War-Part-8)). At Tsushima, Togo lost sight of the Russians in "smoke and fog" twice (about 15:40 and 16:40) ([USNI 1905](https://www.usni.org/magazines/proceedings/1905/october/battle-sea-japan)).
  - That was largely funnel and fire smoke, since both sides used smokeless propellant by then [INFERRED].
- **Smokeless dates:**
  - Poudre B: 1884 (French) ([Wikipedia](https://en.wikipedia.org/wiki/Cordite)).
  - Cordite: patented 1889 ([Wikipedia](https://en.wikipedia.org/wiki/Cordite)). The QF 6" was in service with cordite by 1892 ([Wikipedia](https://en.wikipedia.org/wiki/QF_6-inch_naval_gun)).
  - USN: the 13"/35 was commissioned on black powder, and Santiago (1898) was fought largely with brown or black powder ([navweaps](https://www.navweaps.com/Weapons/WNUS_13-35_mk1.php)). The USN switched to its nitrocellulose "SP" around 1899–1901 [INFERRED from the 1901 USNI article describing it].
- **Inside turrets:** Fouling gas and smoke in turrets led to a USN gas-ejector requirement of 100–250 psi air blown through the bore when the breech opened ([Stirling](https://penelope.uchicago.edu/Thayer/E/Gazetteer/People/Yates_Stirling/Sea_Duty/8*.html)).
  - Turret flash fires: *Massachusetts* 1903, *Missouri* 1904, *Kearsarge* 1905 (powder burn killed 2 officers and 8 men), *Georgia* 1907 ([USNI 1921](https://www.usni.org/magazines/proceedings/1921/september/gunnery-and-turret-design); [navweaps](https://www.navweaps.com/Weapons/WNUS_13-35_mk1.php)).
- **Spotting:** Pointers could not see their own fall of shot because of gun smoke and gases, so spotting had to move aloft ([USNI 1904](https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing)).

---

## 5. Range finding

### 5.1 Estimation by eye (the baseline)
- In Sims's 1904 test of about 30 observers ([USNI 1904](https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing)):
  - At 1,193 yd with the target broadside, 19 of 20 estimates fell within half the danger space of a 20-ft target. End-on, only 8 did.
  - Beyond 2,100 yd, no more than one observer per ship was within that margin.
  - Errors reached 1,200 yd at short range and 2,400 yd end-on.
- **Model [INFERRED]:** eye estimate σ ≈ 10–15% of range broadside and 20–30% end-on. Experienced officers were better at short range.

### 5.2 Instruments

| Instrument | Date | Performance | Source |
|---|---|---|---|
| Watkin depression range/position finder (coast artillery) | 1880s; position finder 1886 | Accurate from a fixed known height; the depression finder was less accurate than the position finder; poor beyond ~3–4 kyd | [victorianforts](https://www.victorianforts.co.uk/CoastDefence2.htm); [Admiralty Trilogy](https://admiraltytrilogy.com/pdf/FI2024_Development_of_Optical_Rangefinders.pdf) |
| Watkin Mekometer (two-observer, short base) | late 1870s | Poor beyond ~3,000–4,000 yd | [Admiralty Trilogy](https://admiraltytrilogy.com/pdf/FI2024_Development_of_Optical_Rangefinders.pdf) |
| Barr & Stroud requirement | 1888 | ≥4 observations per minute; mean error ≤4% (as summarised); fixed targets to 2,500 yd within 100 yd; operator trained in 1 month | [PPI](https://www.ppi-int.com/articles-systems-engineering/a-set-of-requirements-132-years-old/) |
| Fiske two-station electrical RF | 1889; *Kentucky* baseline 130–135 ft | Unreliable in 1898; not adopted by the US or France | [Admiralty Trilogy](https://admiraltytrilogy.com/pdf/FI2024_Development_of_Optical_Rangefinders.pdf); [USNI 2024](https://www.usni.org/magazines/naval-history-magazine/2024/february/barr-and-stroud-rangefinder) |
| B&S FA1/FA2 coincidence (4.5 ft base) | Trial on *Arethusa* April 1892; accepted 1893 | 1% error at 3,000 yd; FA2 effective to ~6,500 yd | [USNI 2024](https://www.usni.org/magazines/naval-history-magazine/2024/february/barr-and-stroud-rangefinder); [Admiralty Trilogy](https://admiraltytrilogy.com/pdf/FI2024_Development_of_Optical_Rangefinders.pdf) |
| B&S FA3 (Japan, 1904–05) | — | "Accurate" to ~8,000 yd | [navweaps](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php) |
| B&S 9 ft (for *Dreadnought*, 1906) | 1906 | 1% to 7,000 yd | [USNI 2024](https://www.usni.org/magazines/naval-history-magazine/2024/february/barr-and-stroud-rangefinder) |
| USN, Japan, Austria adopt B&S | 1903 | — | [USNI 2024](https://www.usni.org/magazines/naval-history-magazine/2024/february/barr-and-stroud-rangefinder) |
| Fiske stadimeter (masthead angle) | 1890s; used from *Petrel*'s foretop at Manila (1 May 1898) | 20 readings on a 15.5 ft target at 6,580 yd: mean error 35.5 yd, max 100 yd (ideal conditions, seated observer) | [USNI 1905](https://www.usni.org/magazines/proceedings/1905/october/stadimeter-fire-control) |
| Single-observer depression finders at sea | 1882–1894 | Effective ~4,000–5,000 yd; need a steady ship | [Admiralty Trilogy](https://admiraltytrilogy.com/pdf/FI2024_Development_of_Optical_Rangefinders.pdf) |
| Battenberg course indicator | 1892 | Relative-velocity and station-keeping calculator, not a rangefinder | [Wikipedia](https://en.wikipedia.org/wiki/Battenberg_course_indicator) |

- **Error formula:** δR = dq·R² / (B·M·206,265), with dq ≈ 12 arcsec as the standard operator resolution (Germans required 10 arcsec) ([Admiralty Trilogy](https://admiraltytrilogy.com/pdf/FI2024_Development_of_Optical_Rangefinders.pdf)).
  - Applied to the 4.5 ft (1.5 yd) base at about 24× [INFERRED]: δR ≈ 12·R²/(1.5·24·206,265). That gives about 15 yd at 3,000 yd, 60 yd at 6,000 yd and about 170 yd at 10,000 yd.
  - These are ideal values. At sea the practical error is about 2–3× larger.
- **Sea practice:** At 6,000 yd Sims put rangefinder error at well over 100 yd ([USNI 1904](https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing)). Successive ranges scattered by several hundred yards at battle range, at about 4 readings per minute ([navalgazing Rangekeeping 1](https://www.navalgazing.net/Rangekeeping-Part-1)).
- **Range rate:**
  - Before 1902 it was ignored or estimated from the two ships' speeds by rule of thumb. A British pamphlet ran to five pages of such rules ([USNI 1904 discussion](https://www.usni.org/magazines/proceedings/1904/october/discussion)).
  - Dumaresq conceived his relative-motion slide rule in 1902 and took about two years to make it work ([navalgazing](https://www.navalgazing.net/Rangekeeping-Part-1)).
  - Vickers range clock: described by Scott in 1903, patented April 1904, trials in 1905, 246 ordered in 1906 ([Wikipedia](https://en.wikipedia.org/wiki/Vickers_range_clock)).
- **Ranging fire:** Fiske's virtual-mast-height method was to start the stadimeter low and raise the setting until shell flashes appeared on the target. Corrections for muzzle-velocity loss were folded into a fictitious mast height ([USNI 1905](https://www.usni.org/magazines/proceedings/1905/october/stadimeter-fire-control)).
- **Mean-point-of-impact drift [INFERRED]:** Smokeless charges varied by about 15–25 fps from target muzzle velocity, with extremes of 30–50 fps ([USNI 1904](https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing)). At 6,000 yd this shifts the mean point of impact by about 0.5–1.5% of range.

### 5.3 Danger space: why range error was tolerable at short range
Half danger space for a 20-ft-high target ([USNI 1904](https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing)):

| Range (yd) | 6" gun | 12" gun |
|---|---|---|
| 1,200 | 260 | 395 |
| 2,000 | 140 | 230 |
| 3,000 | 75 | 140 |
| 4,000 | 45 | 100 |
| 6,000 | 20 | 55 |

- Sims estimated the best achievable hit rate on a 17×21 ft screen at about 100% at 1,600 yd, 40% at 3,200 yd and 16% at 6,000 yd.
- A 12" gun at 6 miles would need about 48 shots per hit, against about 60 shells carried per gun. He set roughly 20% hits as the realistic ceiling at long range ([same](https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing)).

### 5.4 Typical battle and practice ranges by year

| Year | Range | Source |
|---|---|---|
| 1866 Lissa | "Point blank", ramming | [wiki](https://military-history.fandom.com/wiki/Battle_of_Lissa_(1866)) |
| 1870s–80s fixed directors | 800–1,100 yd | [navalgazing](https://www.navalgazing.net/Directors) |
| Early 1890s | Effective fire ≤~1,000 yd ([navalgazing FC1](https://www.navalgazing.net/Fire-Control-Part-1)); practice ranges <2,000 yd ([Adm. Trilogy](https://admiraltytrilogy.com/pdf/FI2024_Development_of_Optical_Rangefinders.pdf)) | — |
| 1894 Yalu | Japanese opened at ~3,000 yd and fought at ~2,000 m | [historyofwar](https://www.historyofwar.org/articles/battles_naval_yalu_river.html); [USNI 1895](https://www.usni.org/magazines/proceedings/1895/october/professional-notes) |
| 1898 Santiago | Opened ~1,100 yd, widened to ~3,000 yd; longest ~6,000 yd; Oregon's last 13" shots at ~9,500 yd | [Schley report](https://spanamwar.com/schleyreport.htm); [navalgazing](https://navalgazing.net/Spanish-American-War-Part-8); [navweaps](https://www.navweaps.com/Weapons/WNUS_13-35_mk1.php) |
| USN record practice c.1903 | 1,400–1,600 yd, 17×21 ft screen | [USNI 1904](https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing) |
| Late 1890s practice | 3,000–4,000 yd with telescopes; after 1900 ~6,000 yd | [Adm. Trilogy](https://admiraltytrilogy.com/pdf/FI2024_Development_of_Optical_Rangefinders.pdf) |
| 1904 Yellow Sea | Opened at >15 km (8 nmi, with some hits at that range); main action ~5.6 km | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_Yellow_Sea) |
| 1905 Tsushima | Japanese held fire until <7,000 yd; most fighting ≤6,500 yd | [USNI 1905](https://www.usni.org/magazines/proceedings/1905/october/battle-sea-japan); [navweaps](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php) |
| RN 1904 → WWI | Firing ranges 3,000–4,000 yd (1904) → 16,000+ yd | [warhistory](https://warhistory.org/@msw/article/late-nineteenth-century-naval-gunnery) |
| 1908 USN battle practice | *Connecticut* at ~8,000 yd | [Stirling](https://penelope.uchicago.edu/Thayer/E/Gazetteer/People/Yates_Stirling/Sea_Duty/8*.html) |

---

## 6. Spotting and the move to salvos

- **Problem:** When many guns fire independently, the layer cannot tell his splash from others because of smoke, gas and overlapping splashes ([USNI 1904](https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing)).
  - Times of flight at 6 miles were about 19 s for a 12" shell and 27 s for a 6" shell (same source).
  - With a 6" gun firing every 10 s, 2–3 of its own shells are in the air at once. A ship's battery puts dozens of mixed-calibre shells in the air [INFERRED].
- **Remedy:** Salvo firing replaced independent fire once ranges grew. With 3 or more shells, ideally 4+, a wild shot can be discarded. Bracket corrections were 400 yd, then 200, then 100 yd to a straddle, with range-rate corrections about half the range correction ([navalgazing Spotting](https://navalgazing.net/Spotting)).
  - The RN shift came about 1905–08 with the Vickers clock and Dumaresq; the USN followed about 1906–08 [INFERRED/UNCERTAIN]. No dated adoption orders were found.
- **Spotting aloft:** Fiske spotted and ranged from *Petrel*'s foretop at Manila in 1898 ([USNI 1909](https://www.usni.org/magazines/proceedings/1909/june/invention-and-development-naval-telescope-sight)). Sims called for an elevated position above the smoke ([USNI 1904](https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing)). USN cage-mast tops sat at about 122 ft by around 1909 ([Stirling](https://penelope.uchicago.edu/Thayer/E/Gazetteer/People/Yates_Stirling/Sea_Duty/8*.html)).
- **Tsushima:** Japanese fire was rapid only once they saw they were hitting, and hits were numerous within spotting distance ([USNI 1905](https://www.usni.org/magazines/proceedings/1905/october/battle-sea-japan)).
- **Model [INFERRED]:**
  - P(own splash correctly identified) = 1 / (1 + k·N), where N is the number of shells from other guns landing within ±5 s of own splash in the spotter's view and k ≈ 0.5–1.
  - Mixed calibres in the same splash window (12", 9.2" and 6" on pre-dreadnoughts) cut it further. That was the argument for the all-big-gun ship.

---

## 7. Training, skill and test scores

### 7.1 Royal Navy

| Year / ship | Score | Source |
|---|---|---|
| 1897 *Scylla* | 6": 8% hits; 4.7": 13% (Med Fleet average 24% and 35%) | [Wikipedia Scott](https://en.wikipedia.org/wiki/Percy_Scott) |
| 1898 RN battleships | 200 rounds at a stationary target at 200 yd(?) → 2 hits [UNCERTAIN: the range figure looks garbled] | [warhistory](https://warhistory.org/@msw/article/late-nineteenth-century-naval-gunnery) |
| 26 May 1899 *Scylla* | 4.7": 80%; 6": 45%; 4.6 hits per gun per minute; overall score up sixfold | [USNI 1949](https://www.usni.org/magazines/proceedings/1949/april/admiral-sir-percy-scott-and-british-naval-gunnery); [Wikipedia](https://en.wikipedia.org/wiki/Percy_Scott); [navalgazing](https://www.navalgazing.net/Fire-Control-Part-1) |
| 1900 *Terrible* | 6": 76.8% (record) | [USNI 1949](https://www.usni.org/magazines/proceedings/1949/april/admiral-sir-percy-scott-and-british-naval-gunnery) |
| 1901 *Terrible* | 80%, best in the Navy; fleet average with the same guns 28%; Scott's criticised crews still made 41% | [Wikipedia](https://en.wikipedia.org/wiki/Percy_Scott) |
| 1901 *Barfleur* | 71.7%, 5.7 hits/gun/min; score doubled within a month of copying *Terrible* | [USNI 1949](https://www.usni.org/magazines/proceedings/1949/april/admiral-sir-percy-scott-and-british-naval-gunnery); [Wikipedia](https://en.wikipedia.org/wiki/Percy_Scott) |
| 1901 fleet | 25% of ships excused themselves from firing | [USNI 1949](https://www.usni.org/magazines/proceedings/1949/april/admiral-sir-percy-scott-and-british-naval-gunnery) |
| May 1902 *Terrible* | 88.2% average | same |
| 1905 | Scott, newly Inspector of Target Practice, saw 68 ships using 20 different fire methods | same |
| 1907 fleet | 79.1% average; hits/gun/min more than doubled for all calibres, almost ×8 for the 9.2"; *Good Hope* 6.6 hits/gun/min | same |

Note: prize firing and the later gunlayer's test were short-range (about 1,400–2,000 yd [INFERRED]) tests of individual laying and loading. Battle Practice (from about 1904–05) tested whole-ship fire at 5,000–7,000 yd [INFERRED/UNCERTAIN]. Exact dates and rules of the RN tests could not be fetched from dreadnoughtproject (the site was unreachable from here).

### 7.2 US Navy
- **Santiago and Manila 1898:** about 9,500 shots for 121 hits (1.3%) ([Wikipedia Sims](https://en.wikipedia.org/wiki/William_Sims); [Morison](https://cse.iitk.ac.in/users/amit/books/morison-1966-men-machines-modern.html)). Stirling puts Santiago at under 1 hit in 15 shots ([Stirling](https://penelope.uchicago.edu/Thayer/E/Gazetteer/People/Yates_Stirling/Sea_Duty/8*.html)).
- **1899 squadron test:** 2 hits in 25 ship-minutes at 1,600 yd ([Morison](https://cse.iitk.ac.in/users/amit/books/morison-1966-men-machines-modern.html)).
- **Sims:** wrote 13 reports in about 2 years from 1900–01, starting from the *Kentucky*. The 17 December 1901 summary compares *Kentucky* with RN practice ([USNI 2015](https://www.usni.org/magazines/naval-history-magazine/2015/april/continuous-aim-fire-learning-how-shoot); [TR Center](https://www.theodorerooseveltcenter.org/digital-library/o36246/)). He became Inspector of Target Practice on 5 November 1902 and held the post about 6 years ([Wikipedia](https://en.wikipedia.org/wiki/William_Sims)).
- **New target-practice system:** first run at Pensacola in March–April 1903 ([USNI 1921](https://www.usni.org/magazines/proceedings/1921/september/gunnery-and-turret-design)).
- **By about 1905:** a single pointer made 15 hits in 1 minute at 1,600 yd ([Morison](https://cse.iitk.ac.in/users/amit/books/morison-1966-men-machines-modern.html)), and continuous aim was standard ([engines](https://engines.egr.uh.edu/episode/148)).
- **Selection:** fire small arms, take twice as many candidates as needed, choose for consistency as well as mean score. A pointer had to reach about 80% hits on a fixed target before moving-target work ([USNI 1902](https://www.usni.org/magazines/proceedings/1902/october/training-gun-captains)).

### 7.3 Training devices
- **Dotter (Scott, 1899):** a target moved up and down in front of the gun to simulate roll. An electromagnetic pencil marked a dot where the gun was pointed when the layer pressed the trigger, scoring continuous aim without ammunition ([USNI 1949](https://www.usni.org/magazines/proceedings/1949/april/admiral-sir-percy-scott-and-british-naval-gunnery)). The USN bought 10 dotters in 1901 (same).
- **Deflection teacher (12-pdr):** a moving-target device for deflection and lead. Scott's memoir illustrates it ([memoir](https://www.naval-history.net/WW0Book-Adm_Scott-50YearsinRN.htm)); no numbers found.
- **Sub-calibre practice:** a 1-inch aiming rifle until Scott's Lee-Metford adaptation, adopted Navy-wide about 7 years later ([USNI 1949](https://www.usni.org/magazines/proceedings/1949/april/admiral-sir-percy-scott-and-british-naval-gunnery)).

### 7.4 Skill spread model [INFERRED]
- Pre-1898: only 1–2% of men were reliable roll-firers ([Scott](https://www.naval-history.net/WW0Book-Adm_Scott-50YearsinRN.htm)). Fleet prize-firing averages of 24–35% at short range against 80%+ for the best ships imply a 3–10× hit-rate ratio between good and average crews.
- Suggested layer skill distribution for a sim: σ_aim (arcmin, continuous aim) lognormal with median 2.5, 10th percentile 1.2, 90th percentile 6.
  - Roll firing multiplies it by 3–8.
  - A green crew adds 20–40% to cycle time.
  - In battle, drill rates fall to 30–60% (from the navweaps Japanese and Jellicoe data).

---

## 8. Human factors (for the gunlayer agent)

| Factor | Value | Source / tag |
|---|---|---|
| Visual acuity | ~1 arcmin (normal eye); a 4–8× telescope gives an effective 0.15–0.25 arcmin, but platform jitter dominates | [INFERRED] |
| Firing interval (decision → shot leaves) | 0.2 s assumed; 0.3 s measured (Fletcher); longer with electric firing | [USNI 1901](https://www.usni.org/magazines/proceedings/1901/january/notes-firing-interval-examples) |
| Simple reaction time | ~0.2 s, σ ~0.03–0.05 s, rising 20–50% when fatigued or under stress | [INFERRED] (standard psychophysics) |
| Handwheel tracking | A human tracking loop has ~0.3–0.5 s lag and ~1–3 Hz bandwidth; adequate for roll periods of 8–14 s if the gear ratio permits | [INFERRED] |
| Physical limit | Five men could not follow a 5° roll in 10 s on 1890s 6" elevating gear | [Morison](https://cse.iitk.ac.in/users/amit/books/morison-1966-men-machines-modern.html) |
| Telescope recoil injuries | Eye cut by eyepiece (1892); rubber hood standard by 1906 | [USNI 1909](https://www.usni.org/magazines/proceedings/1909/june/invention-and-development-naval-telescope-sight); [1906 manual](https://allworldwars.com/Description%20of%202-inch%20Telescopic%20Sights%20Model%20of%201906.html) |
| Mist and fog | Telescope "nearly useless" in damp weather | [USNI 1896](https://www.usni.org/magazines/proceedings/1896/july/telescopic-sights-guns) |
| Glare | Amber filter fitted by 1906 | [1906 manual](https://allworldwars.com/Description%20of%202-inch%20Telescopic%20Sights%20Model%20of%201906.html) |
| Smoke blinding | 25 s (brown powder) vs 5 s (smokeless) to see 4 miles after a 13" round | [USNI 1901](https://www.usni.org/magazines/proceedings/1901/october/smokeless-powder) |
| Stress, battle vs practice | Santiago results were "significantly lower" than practice predicted | [navalgazing](https://navalgazing.net/Spanish-American-War-Part-8) |
| Battle errors | Spanish sights found set for >10,000 yd at ~6,000 yd actual range (gross mis-setting); Ancona fired a broadside with no shot loaded at Lissa | [navalgazing](https://navalgazing.net/Spanish-American-War-Part-8); [wiki](https://military-history.fandom.com/wiki/Battle_of_Lissa_(1866)) |
| Turret hazards | Flash fires 1903–1907; air-blast gas ejection at 100–250 psi | [USNI 1921](https://www.usni.org/magazines/proceedings/1921/september/gunnery-and-turret-design); [Stirling](https://penelope.uchicago.edu/Thayer/E/Gazetteer/People/Yates_Stirling/Sea_Duty/8*.html) |
| Splinters on gear | Yalu: fragments cut hydraulic pipes and training gear of heavy guns | [USNI 1895](https://www.usni.org/magazines/proceedings/1895/october/professional-notes) |
| Open mounts | Spanish gunners driven from open guns by rapid light-gun fire (*Gloucester*) | [navalgazing](https://navalgazing.net/Spanish-American-War-Part-8) |
| Heat and fatigue | Casemate and turret temperatures of 40–50 °C plausible with coal-fired ships; hand loading of 100-lb 6" shells limits sustained rate after ~10–20 min [INFERRED/UNCERTAIN] | no source found |

---

## 9. Suggested simulation parameters (all [INFERRED] from the above)

**Per-shot angular error (1σ, arcmin), elevation / train:**

| Laying regime | Elevation | Train |
|---|---|---|
| Open sight, roll firing, avg layer (1860–1898) | 20 / skilled 8 | 6 |
| Telescope, roll firing (heavy guns 1898–1910) | 10 / skilled 5 | 3 |
| Telescope, continuous aim (≤9.2", 1899+) | 2.5 / skilled 1.2 | 2 |
| Gun/ammunition dispersion | ~3 | — |
| Muzzle-velocity variation | 0.5–1.5% of range in range | — |

Elevation error is converted to range error via the angle of fall.

**Range-knowledge error (1σ):**

| Method | Error |
|---|---|
| Eye estimate | 12% of range (broadside), 25% (end-on) |
| Stadimeter, known masthead height | 1% (ideal) – 3% (sea) |
| B&S 4.5 ft | δR ≈ 15·(R/3000)² yd ideal, ×2–3 at sea |
| B&S 9 ft (1906) | ~1% to 7,000 yd |

Ranges are refreshed every 15–60 s and passed to the guns with a 5–15 s (voice) or 1–3 s (dial) delay. Range rate is ignored before 1902 (constant range assumed) or estimated to ±50% from relative speeds.

**Smoke:** after each shot, black or brown powder blocks the layer's line of sight for 15–30 s (heavy gun, calm) or 3–8 s (6"). Smokeless: 0–5 s. Funnel and fire smoke are a separate random visibility term.

**Spotting:** independent fire uses the splash-identification model in §6. Salvo (≥3 guns) identification probability is about 0.8–0.95 within a spotting range of about 6–8 km for a top-mounted observer.

**Rates:**
- Use the drill rates in the table in §3, multiplied by 0.5 (±0.15) in battle.
- Heavy guns with fixed loading angles add the slew-and-re-acquire time.
- Under black or brown powder, cap the effective aimed rate at about 1 shot per smoke-clear interval.
