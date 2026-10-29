# A. Warship searchlights, 1890-1925
Status: draft    Updated: 2026-10-29    Request: [REQUESTS.md "Night fighting, 1890-1925 first"](../../REQUESTS.md); an appendix of [night-fighting.md](../night-fighting.md)

Research strand A for the night-fighting model. Web only: about 60 fetches and searches. The shared web-search budget ran out before the end, so some leads stayed open (see "Not reached / open").

Tags: **[SRC]** a cited text says it; **[INFERRED]** reasoning or a fit from sourced facts; **[EST]** my estimate; **[UNCERTAIN]** thin or conflicting.

Units: period "candlepower" (cp) is the international candle (US/UK after 1909). It is within about 2% of the candela, so cp is taken as cd here. A German Hefner candle (HK) is about 0.9 cd. 1 cp/mm² = 10,000 cd/cm² (written 10⁴ cd/cm²).

---

## 1. Hardware by navy and year

### 1.1 The general technology line

| Date | What | Numbers | Tag / source |
|---|---|---|---|
| 1876 | Mangin's silvered-glass reflector (a meniscus lens-mirror) | "In 1876, Col. Mangin brought out his silvered glass reflector which gave excellent results"; its limitation was "its small effective angle" | [SRC] USN 1918 [1] |
| 1886 | Schuckert (Nuremberg): the first plate-glass parabolic mirror | collected light "across an angle of 145 degrees"; beam "less than 3 degrees of dispersion" | [SRC] [1], [3] |
| 1894 | Mangin vs Schuckert in service | Schuckert's thin parabolic glass mirror gives "less chromatic dispersion than the Mangin lens" and "may soon supplant" it, but Mangin projectors still outnumbered Schuckert on ships about ten to one | [SRC] Heinz 1894 [2] |
| 1894 | Schuckert beam divergence | "The divergence of the reflected beam amounts to 3°" | [SRC] [2] |
| to ~1914 | Plain ("low-power") carbon arc | crater about 180 cp/mm² (1.8·10⁴ cd/cm²): "at this temperature it radiates 180 candle power per sq. mm." | [SRC] US patent 1,632,161 [7] (1920s, describing the ordinary arc) |
| July 1914 | Beck high-intensity arc: a prototype from Germany fitted in USS *Texas* for trials, eight more ordered | Beck searchlights "had twice the range of the old type" | [SRC] Blackley 2026 [3] |
| Jan 1915 | USN bid specification for high-intensity lights in 24-, 30- and 36-in sizes | — | [SRC] [3] |
| 1917 | USN specifications: 36-in dated 26 Feb 1917, 24-in dated 5 June 1917 | see 1.3 | [SRC] [1] |
| 1918 | Sperry high-intensity searchlight "brought out" | about 900 cp/mm² (9·10⁴ cd/cm²), "about five times" the best earlier lamps | [SRC] Grace's Guide [8] |
| 1917 | France adopts the Sperry system; the Bréguet regulator had been the basis of French arc projectors until then | — | [SRC] Dessornes [9] |
| WWI | The German navy rejected the Beck lamp: its blue-white light did worse than yellow light in Baltic mist and fog | — | [SRC] Blackley [3] (secondary; no German primary source seen) |

### 1.2 1894: projector table (Austro-Hungarian navy journal, translated in Proceedings USNI)

Source [2]: J. Heinz, "Electric Search Lights at Sea", *Proceedings* 20/4 (Oct 1894), "Translated from Mittheilungen aus dem Gebiete des Seewesens" (the k.u.k. navy's journal, Pola). Blackley [3] calls it an account of German lights. The table is general, not one navy's. **[SRC]**

| Mirror (cm) | Volts | Amperes | "Approx. candlepower" | Electric HP |
|---|---|---|---|---|
| 90 | 45-55 | 80-120 | 40,000 | 4.9-9.0 |
| 60 | 45-55 | 50-65 | 35,000 | 3.0-4.9 |
| 40 | 45-55 | 30-50 | 20,000 | 1.9-3.7 |
| 30 | 45-55 | 11-20 | 3,000 | 0.7-1.5 |

- One exception: a 100 cm Schuckert mirror drawing 150 A. [SRC]
- The "candlepower" column is the bare arc's intensity, not the beam's. Evidence: Blackley [3] gives the 90 cm as having "a 45,000-candlepower rating", and the USN's 1918 36-in arc is specified at "at least 100,000 candlepower" maximum intensity. Beam intensities are 10⁷-10⁸ (see 1.5). [INFERRED]
- Power at the arc: 90 cm ≈ 3.6-6.6 kW (4.9-9.0 hp is 3.7-6.7 kW). [SRC]/[INFERRED]
- The British Army's 90 cm Defence Electric Light (Siemens), mid-1890s on: "60 volts dc at 120 amps = 7.2 kW", range "3,650 Metres". [SRC] [10]. The same page gives the beam as "Two Billion candles". [UNCERTAIN, almost certainly wrong for a plain arc: the physics gives about 9·10⁷ cd.]

### 1.3 USN, 1914-1918 (Navy Dept., Bureau of Steam Engineering, *Searchlights and Signal Lights*, Nov 1918, confidential) [1]

| Size | Arc A | Arc V | Carbons (+/−) | Spec | Tag |
|---|---|---|---|---|---|
| 36-in high-power | 150 | 70-80 (75 nominal), arc ≈ 2.2 cm | 16 mm × 1,100 mm / 11 mm × 340 mm | "maximum light intensity will be at least 100,000 candlepower"; intrinsic brilliancy "at least 500 candlepower per square millimeter" (5·10⁴ cd/cm²) | [SRC] |
| 30-in (Beck) | 120 | 72 | 13 × 1,000 / 11 × 270 | — | [SRC] |
| 24-in | 75 | 55-60 | 11 mm both | "minimum useful mean spherical candlepower on mirror 24,000"; ≥300 cp/mm² (OCR text) | [SRC] |

- Line supply: "designed to be operated on a line voltage of 120" (1918) [SRC]; "125 volt circuit" (1914) [SRC, 4]. A 36-in high-power light therefore draws 150 A × 120-125 V ≈ **18-19 kW from the bus**, of which ≈ 11 kW is at the arc. The rest goes in the ballast. [INFERRED]
- Carbons to burn 2.5 h continuously (1917 spec) [SRC]. WWII manual: a 36-in pair lasts about 90 min at 150 A and 45 min at 190 A [SRC, 11].
- The USN 1918 text says "The size of the mirror is limited by practical considerations to about 36 inches for use on ships", and "Searchlight power was therefore doubled by one improvement" (the high-intensity crater). [SRC]
- Beam colour: "The color of the beam of the high-power searchlight inclines to blue." [SRC]
- Fittings: front door of "clear, white, plate glass" in 12 parallel strips about ¼ in thick, with metal strengthening strips. Iris shutters: "All searchlights will be supplied with iris shutters". A Venetian-blind shutter (for signalling) is worked by a side handle. The arc must stand 20 min burning behind the closed shutter. Observing screen of red and green glass; blue goggles. [SRC]
- No dispersing or spreading lens is mentioned in the 1918 text. [SRC, by absence]

### 1.4 Hardware by navy (what was found)

| Navy | Period | Projector | Count, capital ship | Notes | Tag |
|---|---|---|---|---|---|
| RN | 1906-07 | 24-in | London class: lights landed from the tops, "two additional 24-inch" on the shelter or boat deck, "to be augmented by (or further upgraded to?) a pair of 36-in searchlights when they became available" (order C.N.2 11884/13066, 13 Dec 1906) | 36-in not yet available in late 1906 | [SRC] TDP [12] |
| RN | 1908-1914 | **twin 24-in** | St Vincent and Colossus classes: "Siemens' No. 3 Twin Mountings for 24-in projectors"; 1914: modified "to permit 90 degree elevation for use in anti-aircraft work". Iron Duke: "Crompton's 24-in twin projectors" (citing TDP). Model builders find twin mounts on Lion and Orion plans, singles on Dreadnought and Warspite plans | the pre-war RN dreadnought fit is twin 24-in, **not 36-in** | [SRC] TDP [13][14]; [UNCERTAIN] forum [15] |
| RN | 1915-1918 | **36-in** | HMS *Dreadnought*: 7 June 1915 "36-in searchlight platforms added to foremast struts" and "searchlight control gear installation begins"; 19 Aug 1917 "Four 36-in searchlights replace main top with control position underneath"; 31 Dec 1918 "new 36-in searchlights installed" | 36-in and control gear arrive from mid-1915, before Jutland, and are widespread by 1917-18 | [SRC] TDP [16] |
| RN | WWI | — | Iron Duke class: "'coffee box' searchlight protectors… In action the search lights would be lowered into these protectors" | protected stowage | [SRC] [17] |
| USN | before 1907 | (36-in favoured) | **6** a battleship | | [SRC] Fawcett 1914 [4] |
| USN | 1907-1910 | 36-in | **8** | | [SRC] [4] |
| USN | 1910-c.1913 | 36-in | **16** (a three-year trial), then back to **8** | 1914 allowance: "eight searchlights … each battleship or armored cruiser"; 2-4 a protected cruiser; 1-2 a gunboat; "the 36 inch pattern" favoured; about $2,000 each | [SRC] [4] |
| USN | 1914-1918 | 36-in high-intensity (Beck/GE, Sperry) | 8 | Beck in *Texas* July 1914; 1915 bids; 1917 specs; by 1918 "integrated into the latest fire-control systems" (self-synchronous AC motors, follow-the-pointer) | [SRC] [1][3] |
| German | 1886-1894 | Schuckert parabolic 30-90 cm | — | 90 cm the standard battleship light, "45,000-candlepower rating", "effective range of more than 3,500 meters"; larger units motor-trained | [SRC] [3] (citing [2]) |
| German | 1914-1918 | (110 cm not confirmed) | — | Better searchlight control at Jutland, "using iris shutters which could rapidly switch on and off the light" | [SRC] Wikipedia [18]. No WWI German count or size was confirmed in this pass. |
| Russian | 1898-1904 | **75 cm Mangin** | **6** | *Pobeda*: "six Mangin searchlights of 75 cm diameter with electric remote control"; two on the fore bridge, two on a platform before the middle funnel, two on the mainmast one above the other; 40 cm on the boats. *Oslyabya*: six 75 cm Mangin (fore bridge 2, beside the third funnel 2, after bridge 2). Poltava class: "six fighting searchlights 75 cm", arc of action "180°-220°", in pairs on the foremast and on the main and middle mast platforms | 6 × 75 cm is the Russian pre-dreadnought standard | [SRC] ru-wiki mirrors [19][20][21] |
| Russian | 1902 | — | — | *Pobeda* electrical plant: 4 × 1,000 A + 2 × 640 A dynamos at 105 V, total 555 kW | [SRC] [19] |
| Italian | 1894 | — | "not less than six" on large ships | | [SRC] [2] |
| French | to 1917 | Mangin (Sautter-Harlé) 60-90 cm; Bréguet regulator | — | a Sautter-Harlé 90 cm Mangin projector, 8,000 W (fortress); *Richelieu* (1879) among the first big ships with an electric light; Sperry adopted from 1917 | [SRC] Dessornes [9] |
| Japanese | — | — | — | nothing primary found; IJN treated high-intensity lights as "a vital part of the night-fighting tactics" (interwar) | [SRC] [3] general only |
| A-H | 1894 | Mangin dominant (10:1 over Schuckert at sea, general) | — | source [2] is the k.u.k. navy's own journal | [SRC]/[INFERRED] |

Small craft:
- Russian boats (launches) carried 40 cm lights [SRC, 19]. Gunboats carried 1-2 lights, protected cruisers 2-4 (USN 1914) [SRC, 4].
- RN destroyer light size (20-in or 24-in) and German torpedo-boat light size (60 cm or other): **not confirmed** (german-navy.de tech pages list no searchlights).

### 1.5 Beam peak intensity

No measured beam candlepower for a 1890-1918 naval projector was found. The 1919 GE Review photometry is unattributed. Cross-checks against the game's formula (peak = L × mirror face area × 0.9 × 0.85):

| Case | L (cd/cm²) | Mirror | Formula peak (cd) | Check | Tag |
|---|---|---|---|---|---|
| 90 cm plain arc | 1.8·10⁴ (180 cp/mm², [7]) | 6,362 cm² | 8.8·10⁷ | the "Two billion" DEL figure [10] is ~20× too high | [INFERRED] |
| 36-in plain arc | 1.6-1.8·10⁴ | 6,567 cm² | 8.0-9.0·10⁷ | game's 8·10⁷ consistent | [INFERRED] |
| 36-in HI, USN minimum | 5.0·10⁴ (≥500 cp/mm², [1]) | 6,567 | 2.5·10⁸ | | [INFERRED] |
| 36-in HI, Sperry claim | 9.0·10⁴ (900 cp/mm², [8]) | 6,567 | 4.5·10⁸ | | [INFERRED] |
| WWII German 60 cm Flak, HI | — | 2,827 | **1.35·10⁸ rated** [SRC, 22] | implies L ≈ 6.2·10⁴ | [INFERRED] |
| WWII German 150 cm Flak 34/37 | — | 17,671 | **9.9·10⁸ rated**; 200 A at 110 V, 24 kW generator [SRC, 22] | implies L ≈ 7.3·10⁴; game 75,000 → 1.0·10⁹ ✓ | [INFERRED] |
| WWII US 60-in (152 cm) | — | 18,241 | **8·10⁸** [SRC, 23]; 15 kW generator | implies L ≈ 5.7·10⁴ | [INFERRED] |

Arc intensity ratio: the 1894 90 cm arc (~40,000 cp) against the 1918 36-in HI arc (≥100,000 cp) is ×2.5, matching "power … doubled" [1]. [INFERRED]

Divergence:
- Plain arc, Schuckert 90 cm: 3° [SRC, 2]. 1886: "less than 3 degrees" [SRC, 3].
- High-intensity: "very small dispersion" [SRC, 1], "small divergence" [SRC, GE 1919, 6]. No figure in degrees was found.
- Divergence ≈ crater diameter / focal length. With the HI crater much smaller (16 mm positive) than a plain 100-150 A crater, the HI beam should be about ½ to ⅔ of the plain arc's. So ~1.5-2° against ~2.5-3°. [INFERRED/EST]

Search spreading:
- In 1894, sweeping fleet lights were "set at 7°" divergence, and the rear ships' and harbour fans at "20° divergence in each" [SRC, 2]. That is, lights were deliberately defocused or spread for search.

---

## 2. How many per ship, where

- **RN**:
  - 1906: lights in the tops, being moved to the shelter or boat deck [SRC, 12].
  - Pre-war dreadnoughts: twin 24-in mounts. The count is not confirmed, but plans show several twin mounts on Lion [UNCERTAIN, 15].
  - From 1915: 36-in on foremast-strut platforms. In 1917, Dreadnought got four 36-in in place of the main top, with a control position under them [SRC, 16].
  - Iron Duke class: lights lowered into "coffee box" protectors in action [SRC, 17].
- **USN** (1914): "mounted upon platforms jutting out from the fire control towers [cage masts] which have supplanted the old time military masts"; "also mounted upon towers" [SRC, 4]. Eight a battleship [SRC, 4].
- **Russian** (1900-04): six × 75 cm, in pairs (fore bridge or foremast, amidships between funnels, after bridge or mainmast) [SRC, 19-21].
- **Positions in general** (1894): "military tops", the upper-deck level, or "in ports on the gun-deck" [SRC, 2].
- **German WWI class counts** (Nassau, Kaiser, König, Moltke, Derfflinger), **Mikasa**, **Majestic/Formidable**, **QE**, **Lion** exact counts: **not found** in this pass.

## 3. Power

| Light | Arc | From the bus | Tag |
|---|---|---|---|
| 90 cm plain, 1894 | 80-120 A × 45-55 V = 3.6-6.6 kW | (4.9-9 hp electric) | [SRC] [2] |
| 90 cm plain, British DEL | 120 A × 60 V = 7.2 kW | one dedicated generator a light | [SRC] [10] |
| 36-in HI, USN 1917-18 | 150 A × 75 V ≈ 11 kW | 150 A × 120-125 V ≈ 18-19 kW | [SRC] [1]; [INFERRED] product |
| 24-in HI, USN 1917 | 75 A × 55-60 V ≈ 4.3 kW | ≈ 9 kW at 120 V | [INFERRED] |
| 30-in HI, GE 1919 | 150 A × 75 V | — | [SRC] [6] |
| General, 1915 | "from 5 to 15 kilowatts", 80-110 V DC | — | [SRC] Electrical Experimenter Dec 1915 [24] |
| WWII USN 24-in (24-G-20) | 75-80 A at 65-70 V arc, from 105-125 V DC | | [SRC] [25] |

## 4. Control

- **1894, motor control already in use** [SRC, 2]. Large lights had "two electric motors fitted to the pedestal, one controlling the train, the other the elevation", run from a switchboard and workable at a distance, replacing speaking tubes.
- **1894 sweep doctrine** [SRC, 2]. A fleet's cruisers each swept a 90° sector once a minute "with a turning speed 1 ½° per second, every point in the sector will be lighted up for 4 ½ seconds during each minute". Operators needed "several years of drill and practice".
- **1894 observer rule** [SRC, 2]. "the observer's line of sight must make an angle of from 25° to 60° with the axis of the projected beam". This is the operator-sees-worse effect. The text gives no reason; backscatter glare is the evident one [INFERRED].
- **USN 1914 spec** [SRC, 4]. "Three methods of control; namely, distant electric, local mechanical and distant mechanical". The controller can make the light oscillate "two to 40 degrees in train" and "two to 30 degrees in elevation every ten seconds". That is up to ~8°/s in search [INFERRED].
- **USN 1917 spec** [SRC, 1]. Distant electrical controller "capable of training the searchlight at a minimum speed of 3° per minute and a maximum speed of 360° per minute" (0.05-6°/s), with a hand override. Distant-mechanical types A-D are also listed.
- **USN 1918**: lights tied into fire control with self-synchronous motors and follow-the-pointer handwheels [SRC, 3].
- **WWII USN**: 36-in had remote train and elevation, 24-in did not [SRC, 11].
- **RN**: "searchlight control gear installation begins" June 1915 in *Dreadnought*; 1917: four 36-in with a "control position underneath" [SRC, 16]. That is grouped or remote control from a position before Jutland, completed after. The RN's "Searchlight Control Sight" or director-type control by name and date: **not found**.
- **German**: better searchlight control at Jutland with fast iris shutters [SRC, 18]. A Scheinwerferleitstand by name, and German drill: **not found**.
- **How well the beam was held on a moving target**: no quantitative period figure found.

## 5. Useful ranges

| Light | Conditions | Range | Tag |
|---|---|---|---|
| 90 cm plain (general, 1894) | "most favorable circumstances" | detects "up to 3500 meters" (3.8 kyd); "will betray the presence of ship or fleet … at distances far beyond this range"; signalling ≤ 25 nm | [SRC] [2] |
| 90 cm plain (British DEL, 1890s) | — | 3,650 m | [SRC] [10] |
| 36-in **low-power** (USN 1918) | not stated (normal night) | good illumination ≤ **3,000 yd (2.7 km)**; extreme with binoculars ~**4,000 yd (3.7 km)**; rangefinder readings "not much over 2,000 yards" (1.8 km) | [SRC] [1] |
| 36-in **high-power** (USN 1918) | not stated | "objects may be plainly seen at 6,000 yards [5.5 km], and at 8,000 yards [7.3 km] the target is plain when binoculars are used" | [SRC] [1] |
| Beck vs old | — | "twice the range of the old type" | [SRC] [3] |
| Scaling law (1948 IES review) | — | raising beam intensity from 100 to 500 megacandles increases range "by some 50 per cent in clear weather" and "by only some 15 per cent in medium weather" | [SRC] Nature 1948 [26] |

Combat ranges seen (Jutland night, 31 May-1 June 1916):
- German IV SG against *Castor* and the 11th Flotilla: "At about 1 mile range, the German ships switched on searchlights and opened fire" [SRC, 18].
- *Westfalen* against *Broke* and *Sparrowhawk*: searchlights at "in the region of 150 yards" [SRC, 18].
- *Thüringen* "fixed Black Prince in her searchlights"; the Germans were at 750-1,500 yd (700-1,350 m), and she sank within 15 min [SRC, 27].
- *Southampton* against the IV SG: "the Germans switched on their searchlights, and we switched on ours"; "The action lasted three and a half minutes"; "The four leading German ships concentrated their lights and guns on the Southampton" [SRC, 28].

Exposure in practice:
- German "iris shutters which could rapidly switch on and off the light" [SRC, 18].
- USN arcs are kept burning behind a closed shutter (a 20-min ventilation requirement) [SRC, 1], so exposure is a shutter action, not an arc strike [INFERRED].
- Heinz (1894): "ready for immediate use, not to be operated except in case of urgency" [SRC, 2].

## 6. Damage

- **Tsushima (1905)** [SRC, USNI 1907, 29]. Later Japanese torpedo success was "because the Japanese rapid fire guns had destroyed the latter's [the Russian ships'] searchlights and light artillery". Ships out of action had "neither searchlights nor light artillery" for defence. The author suggests armour protection at most "might be applied to searchlights".
- **Tsushima, *Admiral Nakhimov***: "was visible, turning on searchlights" while others were attacked [SRC, 30].
- **Jutland, *Southampton*** [SRC, 28]. "the search light crew died instantaneously in a shell burst that carried the lights away". There were 89 casualties in 3½ minutes. Her lights also drew the German fire: "four leading German ships concentrated their lights and guns".
- **Jutland, *Castor***: her burning motor boat lit the ship, "illuminating the whole ship" [SRC, 18]. This is a fire as an illuminant, relevant to the game.
- **Own guns' blast** [SRC, 1]. The USN spec makes the front door "supported by springs and rollers to eliminate breakage due to gun fire" and clamps the dome "so as to eliminate breakage due to gunfire". Blast breakage was a design concern.
- **RN protection**: Iron Duke's "coffee box" protectors into which lights were lowered in action [SRC, 17].
- **Rate of loss**: no statistic found on how often lights were knocked out.

## 7. Weight and crew

- **Weight**:
  - No naval 36-in figure found.
  - GE 1919: a new 30-in high-intensity open-type unit weighing 200 lb was "more powerful than the 4000-lb., 36-in. seacoast searchlights" [SRC, 6]. The seacoast light is 1.8 t, but with its heavy stand.
  - A 24-in iris mechanism of "about 50 lbs" (a reader anecdote) [UNCERTAIN, 11].
- **Crew**:
  - No naval figure found.
  - The WWII German 60 cm Flak needed "a crew of three" and the 150 cm "seven" [SRC, 22]. Army practice, with a generator.
  - The WWII US Army control station had three men, placed "several hundred feet away from a searchlight" [SRC, 31].

---

## Against the game's estimates

| Estimate | Verdict |
|---|---|
| Victorian 1890: 24-in capital ×5, small 40 cm, 12,000 cd/cm², 3°, local | **Partly confirmed, revise.** Spread 3° confirmed [2]. 40 cm small lights confirmed (Russian boats [19]; the 1894 table has 40 cm). Count: Italy "not less than six" [2], USN 6 before 1907 [4] → use **×6**. Size: the RN used 24-in through 1906 [12], but the continental standard by 1894 was **90 cm** on battleships [2][3] → RN 24-in OK, others 60-90 cm. Crater 12,000: the ordinary arc is ~180 cp/mm² (1.8·10⁴) [7] (a 1920s statement); a lower 1890 value is plausible but unsourced [EST]. Control: **not just local**: motor-trained remote control existed in 1894 for large lights [2] → "local or motor". |
| Pre-dreadnought 1904: 75 cm ×6, small 45 cm, 14,000, 2.5° | **Confirmed for Russia** (6 × 75 cm Mangin, electric remote control: Pobeda, Oslyabya, Poltava [19-21]). Small 45 cm: sources show 40 cm → revise to 40 cm. 14,000 and 2.5°: no direct evidence; 2.5-3° fits [2]. |
| RN pre-dreadnought: 24-in ×6, destroyers 20-in | 24-in **confirmed** for c.1906 [12]; ×6 no evidence. Destroyers 20-in: **no evidence found**. |
| Great War 1914: 36-in ×8, destroyers 24-in, 16,000, 2°, local | **USN: confirmed** (8 × 36-in, 1914 [4]); USN control was distant electric by spec, not local [4]. **RN: revise.** In 1914 RN dreadnoughts carried **twin 24-in** projectors (St Vincent, Colossus, Iron Duke [13][14][15]). 36-in arrived from **June 1915** and is general by 1917-18 [16]. 16,000 cd/cm²: consistent (≤18,000 [7]). 2°: plain arc sources say ~3° [2] → revise to **2.5-3°** [EST]. Destroyers 24-in: no evidence. |
| RN post-Jutland 1918: same but remote | **Revise date.** "Searchlight control gear" from **June 1915** (Dreadnought), a control position under four 36-in lights in Aug 1917 [16]. So remote/grouped control 1915-17, general by 1918. |
| German WWI: 110 cm ×8, boats 60 cm, 16,000, 1.8°, remote | **No evidence** for 110 cm, ×8 or 60 cm in this pass. 90 cm was the German battleship standard in the 1890s [3]. Remote motor training existed already in 1894 [2], and iris shutters at Jutland [18] → "remote" plausible. 1.8° on a plain arc is tighter than the sourced 3° [UNCERTAIN]. The German navy kept the yellow plain arc (rejected Beck) [3] → do **not** give German WWI lights a high-intensity crater. |
| USN high-intensity 1918: 36-in, 60,000 cd/cm², 1.4° | **Confirmed in kind and date.** Beck in *Texas* 7/1914; bids 1/1915; specs 2/1917; Sperry 1918 [1][3][8]. 60,000 lies between the USN minimum of 50,000 [1] and Sperry's 90,000 [8]. WWII rated beams imply 57,000-73,000 effective [22][23] → 60,000 good. 1.4°: no figure found; "very small dispersion" [1] → keep as [EST]. |
| Interwar 110 cm HI 60,000 | no evidence (not searched far). France took Sperry from 1917 [9]. |
| WWII 150 cm 75,000 | **Confirmed**: German 150 cm rated 9.9·10⁸ cd [22]; formula gives 1.0·10⁹. US 60-in 8·10⁸ [23]. |
| Beam peak formula: 36-in plain 8·10⁷, HI 3·10⁸ | **Consistent** with the sourced crater brightnesses (plain 8-9·10⁷; HI 2.5-4.5·10⁸) and with WWII rated figures. No measured WWI beam value found. |
| Weight 1.2 t (36-in) | **No evidence** for a naval 36-in. A coast 36-in with stand was 4,000 lb (1.8 t) [6]. 1.2 t is plausible for a remote-controlled pedestal light [EST]. |
| Draw 6.5 kW (36-in plain) | **Confirmed for the arc** of a plain 90 cm/36-in (3.6-7.2 kW [2][10]). **Revise for HI**: 150 A × 75 V ≈ **11 kW at the arc, ≈ 18-19 kW from the 120-125 V bus** [1][4]. |
| Crew 3 | **No naval evidence**; army 60 cm: 3 [22]. Keep [EST]. |
| Pointing error: local 0.5°, remote 0.35°, director 0.15° | **No evidence.** |
| Strike arc 10 s; train and expose 4 s | Arcs were kept burning behind shutters (20-min spec) [1], and iris shutters exposed "rapidly" [18]. So exposure on order is near-instant if the arc is already struck; the 10 s strike applies only from cold [INFERRED]. 4 s train-and-expose: plausible against 6°/s max distant training [1] [EST]. |
| Sweep 1.5°/s searching | **Confirmed**: "turning speed 1 ½° per second" (1894) [2]. The 1914 USN spec allowed faster oscillation, up to ~8°/s [4]. |
| Front-glass stray 1e-5; backscatter phase 0.3 | **No numeric evidence.** Qualitative support: the observer was to stand 25°-60° off the beam axis [2]; WWII control stations stood several hundred feet from the light [31]. |
| Useful reach of a 36-in, moonless: 5.2 km at 25 km visibility, 2.8 at 10, 1.7 at 5, 0.8 at 2 | **Revise for plain arc.** Period figures for a plain 36-in/90 cm: **2.7 km good illumination, 3.5-3.7 km extreme** [1][2][10]; 5.2 km fits the **high-intensity** 36-in (5.5 km plainly, 7.3 km with binoculars [1]). Suggest plain arc ≈ **3.3 km @ 25 km, 2.0 @ 10, 1.3 @ 5, 0.7 @ 2** [EST, scaled]. Keep the game's curve for HI. Rangefinder use only to ~1.8 km plain [1]. Jutland lightings happened at 150 yd to ~1 nm [18][27]. |

## Not reached / open

- The web-search budget (shared across agents) ran out mid-pass. Several fetches of dreadnoughtproject.org class pages (Orion, Lion, KGV, Iron Duke, Neptune, Centurion, Devonshire, the "Searchlight" page) failed with client errors.
- German WWI counts and sizes by class (Nassau-Derfflinger); the Scheinwerferleitstand and German drill; Marine-Rundschau.
- RN counts by class (Majestic, Formidable, Lion, QE); the RN Handbook of Searchlights; the Searchlight Control Sight by name; Jellicoe's post-Jutland night-fighting changes (the Gutenberg text was truncated).
- Japanese fits (Mikasa, Kongō): ja-wiki and navypedia list none.
- Italian and A-H class fits; French class fits (Sautter-Harlé, Bréguet sizes).
- Measured beam candlepower and beam divergence in degrees for any 1890-1918 naval projector; Jean Rey's *The Range of Electric Searchlight Projectors* (1917 translation) and Blondel's work were not reached online.
- Naval projector weight and crew; RN destroyer and German torpedo-boat light sizes; the rate of damage to lights.
- WWII USN doctrine of ~2,500 yd useful: not checked.

## Sources

1. U.S. Navy Dept., Bureau of Steam Engineering, *Searchlights and Signal Lights* (N.S.E. 312, confidential), GPO, Nov 1918. Full text: https://distantreader.org/stacks/trust/uc2/uc2_ark+=13960=t8cg00d51.txt
2. J. Heinz, "Electric Search Lights at Sea", *Proceedings USNI* 20/4/72, Oct 1894, transl. Lt H. G. Dresel from *Mittheilungen aus dem Gebiete des Seewesens*. https://www.usni.org/magazines/proceedings/1894/october/electric-search-lights-sea
3. A. K. Blackley, "Lighting the Night", *Naval History*, June 2026. https://www.usni.org/magazines/naval-history/2026/june/lighting-night
4. W. Fawcett, navy searchlights article, *Popular Electricity*, Feb 1914. https://worldradiohistory.com/Archive-Popular-Electricity/PopElec-1914-02.pdf
5. (unused number)
6. *General Electric Review*, Sept 1919 (Beechlyn, Murphy, Benford articles; editorial). https://worldradiohistory.com/Archive-Company-Publications/GE-Review/1919/General-Electric-Review-1919-09.pdf
7. US Patent 1,632,161, "Arc lamp" (ordinary arc 180 cp/mm²). https://patents.google.com/patent/US1632161A/en
8. Grace's Guide, Elmer Ambrose Sperry (Sperry HI 1918, 900 cp/mm²). https://www.gracesguide.co.uk/Elmer_Ambrose_Sperry
9. G. Dessornes, *Les Projecteurs Militaires 1862-1962* (2017). https://artillerie.asso.fr/docs/DIVERS/Les%20Projecteurs%20Miltaires%201862-1962%20LK%202.pdf
10. Defence Electric Lights (Siemens 90 cm, 60 V 120 A, 3,650 m). https://pinkroutes.org/research/defended-ports/port-defence/defence-electric-lights/ and https://nqht2020.duckdns.org/?p=11837
11. *Electrician's Mate 3*, NAVPERS 10548, ch. 15 (WWII). https://www.rfcafe.com/references/electrical/electricians-mate-3-navpers-10548/navy-training-electricians-mate-3-NAVPERS-10548-chapter-15.htm
12. The Dreadnought Project, London Class Battleship (1899). https://dreadnoughtproject.org/tfs/index.php/London_Class_Battleship_(1899)
13. The Dreadnought Project, St. Vincent Class Battleship (1908). https://dreadnoughtproject.org/tfs/index.php/St._Vincent_Class_Battleship_(1908)
14. The Dreadnought Project, Colossus Class Battleship (1910). https://dreadnoughtproject.org/tfs/index.php/Colossus_Class_Battleship_(1910)
15. Model Boat Mayhem forum, RN twin 24-in searchlights (cites TDP on Iron Duke) [UNCERTAIN]. https://www.modelboatmayhem.co.uk/forum/index.php/topic,50014.0.html
16. The Dreadnought Project, H.M.S. Dreadnought (1906). https://www.dreadnoughtproject.org/tfs/index.php/H.M.S._Dreadnought_(1906)
17. History of War, "'Coffee Boxes', HMS Marlborough". https://www.historyofwar.org/Pictures/pictures_hms_marlborough_coffee_boxes.html
18. Wikipedia, Night action at the Battle of Jutland. https://en.wikipedia.org/wiki/Night_action_at_the_Battle_of_Jutland
19. Победа (броненосец), ru-wiki via academic.ru. https://dic.academic.ru/dic.nsf/ruwiki/254440
20. Ослябя (броненосец), ru-wiki via academic.ru. https://dic.academic.ru/dic.nsf/ruwiki/251326
21. Броненосцы типа «Полтава», ru-wiki via academic.ru. https://dic.academic.ru/dic.nsf/ruwiki/1757136
22. Wikipedia, German searchlights of World War II. https://en.wikipedia.org/wiki/German_searchlights_of_World_War_II
23. Wikipedia, Searchlight (US 60-in, 8·10⁸ cd, 15 kW). https://en.wikipedia.org/wiki/Searchlight
24. *The Electrical Experimenter*, Dec 1915, p. 397. https://mobicore.dissgea.unipd.it/files/original/54cf17b09043e6ffe7faafc902a8da895ad69b03.pdf
25. NavSource, 24-in Searchlight Model 24-G-20. https://www.navsource.org/archives/01/57w.htm
26. W. M. Hampton, review of *Searchlights* (IES), *Nature* 162:424 (1948). https://www.nature.com/articles/162424a0.pdf
27. Wikipedia, HMS Black Prince (1904). https://en.wikipedia.org/wiki/HMS_Black_Prince_(1904)
28. Mitcham War Memorial, "HMS Southampton: day and night at Jutland" (quotes King-Hall). https://mitchamwarmemorial.wordpress.com/2016/05/27/hms-southampton-day-and-night-at-jutland/
29. "Torpedo and Mine Effects in the Russo-Japanese War", *Proceedings USNI*, Oct 1907. https://www.usni.org/magazines/proceedings/1907/october/torpedo-and-mine-effects-russo-japanese-war
30. Wikipedia, Russian cruiser Admiral Nakhimov (1885). https://en.wikipedia.org/wiki/Russian_cruiser_Admiral_Nakhimov_(1885)
31. Skylighters, The Antiaircraft Artillery Searchlight Story. https://skylighters.org/the-antiaircraft-artillery-searchlight-story
32. "A Description of the Battle of Jutland (Concluded)", *Proceedings USNI*, Jan 1920 (German star shells and frequent searchlights; *Southampton*'s torpedo "at a group of searchlights" sank *Frauenlob*). https://www.usni.org/magazines/proceedings/1920/january/description-battle-jutland-concluded
