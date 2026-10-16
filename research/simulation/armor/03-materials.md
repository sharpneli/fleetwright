# 03 — Naval Armour Materials 1859–1945: Catalogue and Parameters
Status: draft    Updated: 2026-10-16    Request: —

Foundational research for the armour-penetration model. Right now the game treats armour as "thickness × density of steel". This file catalogues the actual materials, gives their physical parameters, and puts each on a common **relative-resistance** scale so that armour can later be parametrised by era and nation.

**Conventions**
- Every number carries a citation link. Citation keys are listed in §0 and are reference-style Markdown links, so each one resolves to a URL.
- **[INFERRED]** marks my own derivation or estimate. **[UNCERTAIN]** marks a point where sources conflict or the evidence is thin.
- Hardness is Brinell (BHN). Plate thickness is in inches, with mm where the source gave metric.
- **Q** is relative resistance in Okun's convention: late-period (WWII) US armour is 1.00, rated on roughly 8-inch plates at normal impact ([forum summary of Okun's definition][tt-q]).
- **FM** is the historical "figure of merit": the thickness of wrought iron a plate equals, divided by the plate's own thickness ([forum summary][tt-q]; [EB1911][eb]).

**Access note.** `navweaps.com/index_nathan/metalprpsept2001.php` returned a client error and could not be read. I used the September 2009 revision of the same Okun table on navweaps ([okun-09]), the combinedfleet mirror ([okun-cf]), and an older full-table HTML copy ([okun-tbl]). The automated fetcher would not reproduce Okun's table verbatim (copyright) and returned a close paraphrase instead. Brinell, tensile and elongation figures from that copy should be spot-checked against the original before they are hard-coded.

---

## 0. Sources (citation keys)

| Key | Source |
|---|---|
| [okun-tbl] | Okun, *Table of Metallurgical Properties of Naval Armor and Construction Materials* (full-table HTML copy): https://makettinfo.hu/forum/upload_20060921/299.1116.1_ARMOR.HTM |
| [okun-09] | Same, navweaps Sept 2009 rev.: https://www.navweaps.com/index_nathan/metalprpsept2009.php |
| [okun-cf] | Same, combinedfleet mirror: http://www.combinedfleet.com/metalprp.htm |
| [okun-biz] | Okun, *Armor Protection of KM Bismarck* (1991): https://www.combinedfleet.com/okun_biz.htm |
| [okun-tests] | Okun, Corrections to ADM 281/127 (US vs German plate tests): https://www.navweaps.com/index_nathan/britUSGermanArmorTests.php |
| [nw-017] | navweaps tech-017, *US Armor Plate Types*: https://www.navweaps.com/index_tech/tech-017.php |
| [nw-040] | navweaps tech-040, *Ballistic Tests on IJN Shinano's Turret Face Armor*: http://www.navweaps.com/index_tech/tech-040.php |
| [tt-q] | NavWeaps forum, *Different definitions of armor quality* (Okun Q values relayed by N. Robertson): https://www.tapatalk.com/groups/warships1discussionboards/different-definitions-of-armor-quality-t34968.html |
| [tt-ca] | NavWeaps forum thread (Robertson/Wahl on KC n/A vs CA), Apr 2021: https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=886271 |
| [ng1] | Naval Gazing, *Armor Part 1*: https://www.navalgazing.net/Armor-Part-1 |
| [ng2] | Naval Gazing, *Armor Part 2*: https://www.navalgazing.net/Armor-Part-2 |
| [eb] | Encyclopaedia Britannica 11th ed. (1911), "Armour Plates" (Gutenberg): https://gutenberg.org/cache/epub/34162/pg34162-images.html |
| [eb623] | EB1911 vol. 2 p. 623 (Wikisource): https://en.wikisource.org/wiki/Page:EB1911_-_Volume_02.djvu/623 |
| [no37] | USN *Naval Ordnance* (1937), Ch. XII "Armor" (Slover): https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php |
| [nh37] | Same text, naval-history.net: https://Naval-History.net/WW0Book-USN-Armour1937.htm |
| [nog57] | USN *Naval Ordnance and Gunnery* Vol. 1 (NavPers 10797-A, 1957), Ch. 4: https://www.eugeneleeslover.com/USNAVY/CHAPTER-4.php |
| [gs] | GlobalSecurity, ship armor (reproduces old USN texts): https://www.globalsecurity.org/military/systems/ship/armor.htm |
| [hap] | *History of Armor Plate* (US Senate/Navy cost report c.1899; Slover PDF, poor OCR): https://www.eugeneleeslover.com/AMMUNITION/History_of_Armor_Plate.pdf |
| [usni90] | USNI Proc. Dec 1890, *Report of Board on Competitive Trial of Armor Plates* (Annapolis): https://www.usni.org/magazines/proceedings/1890/december/report-board-competitive-trial-armor-plates |
| [usni94] | USNI Proc. Oct 1894, *Professional Notes* (Harvey trials): https://www.usni.org/magazines/proceedings/1894/october/professional-notes |
| [w-krupp] | Wikipedia, Krupp armour: https://en.wikipedia.org/wiki/Krupp_armour |
| [w-harvey] | Wikipedia, Harvey armor: https://en.wikipedia.org/wiki/Harvey_armor |
| [w-comp] | Wikipedia, Compound armour: https://en.wikipedia.org/wiki/Compound_armour |
| [w-iron] | Wikipedia, Iron armour: https://en.wikipedia.org/wiki/Iron_armour |
| [w-wi] | Wikipedia, Wrought iron: https://en.wikipedia.org/wiki/Wrought_iron |
| [w-sts] | Wikipedia, Special treatment steel: https://en.wikipedia.org/wiki/Special_treatment_steel |
| [w-ducol] | Wikipedia, Ducol: https://en.wikipedia.org/wiki/Ducol |
| [w-infl] | Wikipedia, HMS Inflexible (1876): https://en.wikipedia.org/wiki/HMS_Inflexible_(1876) |
| [w-warr] | Wikipedia, HMS Warrior (1860): https://en.wikipedia.org/wiki/HMS_Warrior_(1860) |
| [w-litt] | Wikipedia, Littorio-class battleship: https://en.wikipedia.org/wiki/Littorio-class_battleship |
| [w-rich] | Wikipedia, Richelieu-class battleship: https://en.wikipedia.org/wiki/Richelieu-class_battleship |
| [w-gangut] | Wikipedia, Gangut-class battleship: https://en.wikipedia.org/wiki/Gangut-class_battleship |
| [dp-col] | Dreadnought Project, *Colossus* protection: https://dreadnoughtproject.org/tech/colossus/protection1.php and https://dreadnoughtproject.org/tech/colossus/protection3.php |
| [izh] | Izhora plant history: https://www.aemtech.ru/en/factories/izhora/izhora_history ; https://encspb.ru/object/2855712615?lc=en |
| [greg] | Gregoretti, "Fabrication des plaques de blindage par le procédé Krupp", *Revue de Métallurgie* 7(4), 1910: https://www.metallurgical-research.org/articles/metal/abs/1910/04/metal19100704p260/metal19100704p260.html |
| [mmfg] | MachineMFG metal density table: https://machinemfg.com/density-table-of-metals-iron-steel-brass-aluminum-alloys |
| [nws] | NWS forum, *Armour quality* thread: https://nws-online.proboards.com/thread/6993/armour-quality?page=2 |

[okun-tbl]: https://makettinfo.hu/forum/upload_20060921/299.1116.1_ARMOR.HTM
[okun-09]: https://www.navweaps.com/index_nathan/metalprpsept2009.php
[okun-cf]: http://www.combinedfleet.com/metalprp.htm
[okun-biz]: https://www.combinedfleet.com/okun_biz.htm
[okun-tests]: https://www.navweaps.com/index_nathan/britUSGermanArmorTests.php
[nw-017]: https://www.navweaps.com/index_tech/tech-017.php
[nw-040]: http://www.navweaps.com/index_tech/tech-040.php
[tt-q]: https://www.tapatalk.com/groups/warships1discussionboards/different-definitions-of-armor-quality-t34968.html
[tt-ca]: https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=886271
[ng1]: https://www.navalgazing.net/Armor-Part-1
[ng2]: https://www.navalgazing.net/Armor-Part-2
[eb]: https://gutenberg.org/cache/epub/34162/pg34162-images.html
[eb623]: https://en.wikisource.org/wiki/Page:EB1911_-_Volume_02.djvu/623
[no37]: https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php
[nh37]: https://Naval-History.net/WW0Book-USN-Armour1937.htm
[nog57]: https://www.eugeneleeslover.com/USNAVY/CHAPTER-4.php
[gs]: https://www.globalsecurity.org/military/systems/ship/armor.htm
[hap]: https://www.eugeneleeslover.com/AMMUNITION/History_of_Armor_Plate.pdf
[usni90]: https://www.usni.org/magazines/proceedings/1890/december/report-board-competitive-trial-armor-plates
[usni94]: https://www.usni.org/magazines/proceedings/1894/october/professional-notes
[w-krupp]: https://en.wikipedia.org/wiki/Krupp_armour
[w-harvey]: https://en.wikipedia.org/wiki/Harvey_armor
[w-comp]: https://en.wikipedia.org/wiki/Compound_armour
[w-iron]: https://en.wikipedia.org/wiki/Iron_armour
[w-wi]: https://en.wikipedia.org/wiki/Wrought_iron
[w-sts]: https://en.wikipedia.org/wiki/Special_treatment_steel
[w-ducol]: https://en.wikipedia.org/wiki/Ducol
[w-infl]: https://en.wikipedia.org/wiki/HMS_Inflexible_(1876)
[w-warr]: https://en.wikipedia.org/wiki/HMS_Warrior_(1860)
[w-litt]: https://en.wikipedia.org/wiki/Littorio-class_battleship
[w-rich]: https://en.wikipedia.org/wiki/Richelieu-class_battleship
[w-gangut]: https://en.wikipedia.org/wiki/Gangut-class_battleship
[dp-col]: https://dreadnoughtproject.org/tech/colossus/protection3.php
[izh]: https://www.aemtech.ru/en/factories/izhora/izhora_history
[izh2]: https://encspb.ru/object/2855712615?lc=en
[greg]: https://www.metallurgical-research.org/articles/metal/abs/1910/04/metal19100704p260/metal19100704p260.html
[mmfg]: https://machinemfg.com/density-table-of-metals-iron-steel-brass-aluminum-alloys
[nws]: https://nws-online.proboards.com/thread/6993/armour-quality?page=2

---

## 1. The big picture: the relative-resistance ladder

The most-repeated chain of equivalences comes from USN ordnance texts of 1937 and 1957, and Naval Gazing repeats it:

| Step | Claim | Source |
|---|---|---|
| Wrought iron → compound or all-steel (c.1876–86) | "10-inch compound or all-steel plate resisted the energy of a 12.6-inch iron plate", i.e. ~25% better. The 1957 text says "10 in ≈ 12½ in iron". | [no37], [nog57], [gs] |
| → nickel steel (1889) | "about 5%" more; "a 10-inch treated plate equaled about 13 inches of iron" | [no37], [ng1] |
| → Harvey (1890–91) | "13 inches of Harvey equaled about 15.5 inches of nickel-steel", a 15–20% gain | [no37], [w-harvey], [ng1] |
| → Krupp KC (1893–94) | "11.9 inches of Krupp equaled about 13 inches of Harveyized", ~15% better for plates over ~5 in | [no37], [ng1] |
| → "modern" (1937) | "Modern armor is about 10% better ballistically than early Krupp" | [nh37] |
| → best WWII | "up to 40% better than early Krupp" | [ng1] |

Multiplying these out [INFERRED]: WI 1.00 → compound/steel 1.25 → Ni-steel 1.30 → Harvey ≈ 1.50–1.56 → KC (1900) ≈ 1.73–1.80 → 1937 ≈ 1.9–2.0 → best WWII ≈ 2.4–2.5 "inches of WI per inch" (normal impact, period projectiles).

**Competing contemporary figures of merit** (inches of WI per inch of plate):
- Steel and compound, 1880–90: "about 1.3 to 1" ([eb]).
- Harvey-type steel: "about twice the resisting power of wrought iron"; "rather more than 2 with plates from 6 to 8 in. thick", lower for thicker plates ([eb]).
- Harvey test plates in 1894: Carnegie 6-in plate **1.93**, Bethlehem 8-in plate **2.14**, an English 6-in plate **1.83**, a French estimate **1.80** ([usni94]).
- KC: "about 2.7 in the case of good 6-in. plates", falling "to about 2.3 in the case of 12-in."; "not … more than 2 against capped shot" vs "about 2.5 against uncapped" ([eb]).
- KNC: "2.25 times that of wrought iron" on average ([eb]).
- A 3-in Vickers KNC plate (1905): "about 2.6", called exceptional ([eb]).
- Krupp cast nickel steel (1902): ">2.2" ([eb], [eb623]).
- A forum compilation puts original Krupp FH at **2.55** and 1930s FH at **3.47** (+36%) ([nws]) [UNCERTAIN: the underlying chart was not visible].

**De Marre coefficients** from USN 1937 ([no37]):
- "Harvey plate is to Krupp plate as 1 is to **1.216**."
- A 12-in KC limit gave a coefficient of 1.1165 (relative to nickel steel at ~1.0 [INFERRED from context]).
- A 14-in Class A plate gave ~1.108.
- One Class A plate gave K = 1.23.

**Pre-1900 Krupp-vs-Harvey claims (US Senate report c.1899, [hap]):**
- "20–30% more resistance than Harveyized", or 25% per O'Neil.
- "A 12-inch Krupp plate … equal a 15-inch Harveyized plate".
- The belt could drop "from 16.5 inches to 12 inches", ~23% less weight.
- Wikipedia: "10.2 in Krupp ≈ 12 in Harvey" ([w-krupp]).

> **[UNCERTAIN] KC-over-Harvey margin:** 15% ([no37], [ng1]), 17.6% ([w-krupp] 12/10.2), 21.6% ([no37] De Marre), 20–30% ([hap]), 25% ([hap], 15/12). The higher numbers are from early tests, which favoured thick Krupp plates and were probably affected by Harvey plates cracking. **Recommended model value: KC/Harvey ≈ 1.18–1.20.**

> **[UNCERTAIN] WWII-over-early-KC margin:** Okun's full table implies original KC a/A ≈ **0.83** of WWII face-hardened quality ([okun-tbl], Witkowitz section). That is a 20% improvement. Naval Gazing says "up to 40%" ([ng1]), and the forum FM pair gives 36% ([nws]). The 1937 USN text says only ~10% for "modern" (c.1920s–30s) plate ([nh37]). The spread is probably explained by different projectiles (uncapped vs capped, hard caps) and thicknesses. **Recommended: KC a/A ≈ 0.80–0.85 Q.**

---

## 2. Material catalogue

Each entry gives: names and makers · years · composition · manufacture · density · hardness · face depth · tensile and elongation · thickness range · uses · weldability · cost · relative resistance.

### 2.1 Wrought iron (with timber backing)

- **Makers and years:**
  - 1859–c.1880 for armour. Okun dates the "avg. wrought iron" type 1855–1870 for prime use ([okun-tbl]).
  - John Brown "pioneered armour plate production in the 1860s", and by the 1870s 75% of British ironclads carried its plate ([Sheffield archives guide](https://www.sheffield.gov.uk/sites/default/files/docs/libraries-and-archives/archives-and-local-studies/research/Armaments%20study%20guide%20v1-4.pdf)).
  - Izhora (Russia) rolled its first armour plates in 1866 ([izh2]).
- **Composition:**
  - C under 0.08% ([okun-cf]). Si (as slag) "up to 7%, mostly 3–4%" ([okun-09]) [UNCERTAIN: this is very high, and probably refers to slag silicate content rather than dissolved Si].
  - Wikipedia gives C <0.05–0.25% (it is internally inconsistent) and slag up to 2% by weight ([w-wi]).
- **Manufacture:** hammered, then rolled. Britain's 1861–65 Special Committee on Iron tested both, and rolled plate became standard by 1865 ([w-iron]). Single solid plates beat laminated plates of the same weight ([nh37]).
- **Density:** 7,600–7,900 kg/m³ (s.g. 7.6–7.9) ([w-wi]). Use **7,700 kg/m³** [INFERRED midpoint]. At the 40 lb/ft²/in convention used for steel ([ng1]), iron is ~39 lb/ft²/in [INFERRED].
- **Hardness:** ~105 BHN (average) ([okun-09]).
- **Tensile and elongation:** ~47 ksi and 22% ([okun-tbl]). Wikipedia gives UTS 34–54 ksi and yield 23–32 ksi ([w-wi]).
- **Thickness range:**
  - Early plates were "4 to 5 inches" on "36 inches of solid wood" ([nh37], [gs]). Wikipedia gives 18–36 in of wood ([w-iron]).
  - *Warrior* (1860): 4.5 in WI on 18 in teak, in two 9-in layers laid crosswise ([w-warr]).
  - The maximum was ~24 in, both single plates and sandwich systems ([ng1]). In 1876 "about 22 inches of iron" was needed against the heaviest guns ([nh37], [w-comp]).
- **Uses:** belts, batteries, turrets, and hull construction (*Warrior* hull, 1860) ([okun-09]).
- **Relative resistance:** this is the **reference, FM = 1.0** ([ng1], [eb]). Tresidder's WI perforation formula, *t² = wv³/dA*, is the reference formula ([eb]).
- **Failure mode:** soft but tough. It is pushed through or holed rather than shattered ([okun-tbl]).

**Sandwich armour (HMS *Inflexible*, 1876)** ([w-infl])
- At the waterline: 12 in plate + 11 in teak + 12 in plate + 6 in teak + 2 × 5/8 in skin. That is ~41 in in total and **1,100 lb/ft²**.
- Above the waterline: 12 + 8 in iron with 21 in teak. Below the waterline: 12 + 4 in iron with 25 in teak.
- Turrets: 9 in compound + 7 in WI + 18 in teak.
- "24 inches of armour was considered almost completely proof."
- Weight check [INFERRED]: 24 in × ~39 lb = 936 lb, 17 in teak ≈ 60 lb, 1.25 in skin ≈ 51 lb, total ≈ 1,050 lb/ft². This agrees with the 1,100 figure, so **iron dominates and teak adds ~6% to the weight**.
- Wikipedia notes that laminated iron-and-wood let armour "spread sideways into softer backing" ([w-comp]). Laminated plate is worse than solid plate of the same weight ([nh37]).

### 2.2 Chilled cast iron (Gruson)

- **Maker:** Gruson, Magdeburg-Buckau. The turret was first tested by Prussia in 1868 ([w-iron], [eb]). Okun dates it 1860–1890 ([okun-tbl]).
- **Composition:** ~4% C ledeburite face ([okun-09]).
- **Manufacture:** cast into chilled moulds, which gives a ~33% hardened face ([okun-tbl]).
- **Density:** white cast iron is **7,400–7,700 kg/m³** ([mmfg]). Use ~7,500 kg/m³ [INFERRED].
- **Hardness:** face ~475 (range 425–500), back ~130 ([okun-09], [okun-tbl]).
- **Tensile and elongation:** the table copy shows 57 ksi and 22% ([okun-tbl]). [UNCERTAIN: 22% elongation is implausible for cast iron, and may be a transcription problem in the paraphrase.]
- **Thickness range:** 12–24 in for **land fortress turrets** only ([okun-tbl]). Coastal forts used it, ships did not.
- **Relative resistance:** "extremely hard, and cannot be perforated, but must be destroyed by fracture" ([eb]). It "shattered even the best uncapped projectiles of its period" ([okun-tbl]). No published FM was found [UNCERTAIN].
  - Model it as very high resistance to first hits (shatter) but **cumulative cracking**, with each hit degrading the whole casting.
  - Very heavy, and not usable at sea [INFERRED].

### 2.3 All-steel armour (Schneider / Le Creusot, 1876)

- **Maker and dates:**
  - Schneider et Cie, Le Creusot. At the 1876 Spezia trial a 22-in plate of "about .45 per cent carbon" "outclassed iron" ([no37]). It was reputedly hammered down from 7 ft ([nog57]).
  - The plates were made for Italian battleships ([okun-tbl]).
  - France and Italy used all-steel; Britain and Germany favoured compound in 1877–90 [INFERRED from [w-comp]; Lepanto bought Creusot steel].
- **Composition:** C ~0.30–0.40% for later all-steel armour ([nh37]); 0.45% for the 1876 plate ([no37]).
- **Hardness:** mild/medium steel 120–140 ([okun-tbl]).
- **Tensile and elongation:** mild/medium steel 53–68 ksi, 16–25% ([okun-tbl]). The "older grades very brittle" ([okun-tbl]).
- **Density:** 7,850 kg/m³ ([mmfg]).
- **Relative resistance:** ≈ compound, **~1.25 × WI** ([no37]). The 1880–90 range is 1.3 ([eb]).
- **Trials:**
  - Gâvres 1880: compound beat all-steel. Copenhagen 1884: little difference. Lepanto (c.1884): 20 in Creusot steel shattered the projectiles that demolished a 20 in compound plate ([w-comp]).
  - Annapolis 1890: the Schneider all-steel plate kept out every shot and showed "slightly greater resistance than the nickel-steel plate", but was "badly cracked by the 8-inch shell" ([usni90]).
  - US first armour contract (Bethlehem, 1887): ~6,700 tons of plain steel armour at about **$536/ton** ([hap]).

### 2.4 Compound armour (steel-faced wrought iron)

- **Makers and process:**
  - Alexander Wilson, Cammell & Co. (1877): molten steel poured onto a hot WI plate.
  - J. D. Ellis, John Brown: steel poured between two plates.
  - Both rolled the result down to ~½ thickness ([w-comp]). Friedrich Thiele (Austria) designed the first compound ([w-comp]).
- **Years:** 1877/78–c.1890 ([okun-tbl]). It was obsolete "by the end of the 1880s" because of chrome-steel shot (1886) and nickel steel (1889) ([w-comp]).
- **Composition:**
  - Steel face 0.50–0.60% C ([nh37]); Okun gives ~1% C face ([okun-tbl]) [UNCERTAIN].
  - Face ~⅓ of thickness ([w-comp]). Okun gives 25–33% face and 67–75% WI back ([okun-tbl]).
- **Hardness:** face ~400 (an estimate) ([okun-tbl], [okun-cf]).
- **Density:** composite of ~⅓ steel at 7,850 and ~⅔ WI at 7,700, giving **~7,750 kg/m³** [INFERRED].
- **Thickness:** up to ~18–20 in.
  - HMS *Colossus*: 18 in compound on 20 in teak in two layers ([dp-col]).
  - Lepanto trial: 20 in plate ([w-comp]).
  - Cammell 1883: a 12-in plate of 10½ tons, backed by granite, stopped a 16-in Palliser shot of ~30,000 ft-tons ([eb]).
- **Relative resistance:** **~1.25 × WI** ("10 in ≈ 12½ in iron") ([nog57], [no37]). It was "initially about 25% better than either iron or steel" ([w-comp]).
  - **Failure mode:** the face delaminated: "Steel facing did not adhere well to iron" ([w-comp]).
  - At Annapolis 1890 the Cammell compound plate was holed by every shot and its face was destroyed. It ranked last behind nickel steel and all-steel ([usni90]).
- **Cost:** Denmark ordered compound in 1884, "probably because it was cheaper" ([w-comp]).

### 2.5 Nickel steel (Schneider 1889; Annapolis 1890)

- **Makers:**
  - Schneider introduced it in 1889 ([eb], [no37]).
  - In the US, Bethlehem (John Fritz) and Carnegie made it under Schneider patents ([nh37]). Carnegie's 1890 contract was 6,000 tons at Bethlehem's prices, all nickel steel ([hap]).
- **Composition:**
  - Ni 2–5%, "settling near 4%" ([no37]).
  - Early US deliveries (*Texas*, *Maine*, *Oregon*): C ~0.20%, Mn 0.75%, P and S 0.025%, Ni 3.25% ([nh37]).
- **Manufacture:** forged, then oil- or water-quenched and tempered. "Oil-tempered nickel steel" and "water-hardened nickel steel (Loire)" are both attested in 1894 ([usni94]).
- **Hardness:** average Ni-steel armour ~180 ([okun-tbl]).
- **Tensile and elongation:** ~90 ksi, 19% (Ni up to 7% in the table copy's average entry) ([okun-tbl]).
- **Density:** ~7,850–7,880 kg/m³ [INFERRED]. The Ni-Cr steel group is 7,850 ([mmfg]); Ni (8.9) at 3–4% adds ≈ +0.3–0.4%, which can be ignored.
- **Relative resistance:** **~1.30 × WI** ("10 in ≈ 13 in iron", ~5% over compound or steel) ([no37], [ng1]).
  - Annapolis 1890: the Schneider Ni-steel plate (10.42 in, 20,670 lb) kept out all 6-in and 8-in AP shots and stayed **uncracked**. The board ranked it **1st**, ahead of all-steel and compound ([usni90]).
  - [UNCERTAIN] The 1894 notes recall that the 1890 board found Ni steel "softer than steel and more penetrable" for those two plates ([usni94]). Its advantage was toughness (no cracking), not penetration resistance.
- **Later role:** support or backing layers, protective decks, and turret roofs ([okun-tbl]).

### 2.6 Harvey armour (Harveyized nickel steel / Harveyized mild steel)

- **Inventor and makers:**
  - H. A. Harvey of Newark, 1888–90. The first plate was a Creusot 10.5-in, treated at Washington Navy Yard ([no37]); the first Harvey-process trial was 14 Feb 1891 ([gs]).
  - The Harvey Syndicate of 1894 included Vickers, Armstrong, Krupp, Schneider, Carnegie and Bethlehem ([w-harvey]).
  - By Oct 1894: Cammell, Brown and Vickers had Harveying plants. Saint-Chamond, Châtillon-Commentry and Marrel Frères took contracts for plates "up to a thickness of 15.75 inches". St-Étienne made plates under 8 in. Schneider, Krupp, Dillingen and Witkowitz adopted the process ([usni94]).
- **Years:** 1891–1899 for new production ([okun-tbl]). It stayed on ships far longer.
- **Composition:**
  - US base: C ~0.20%, Mn 0.60%, Ni 3.25–3.50% ([no37], [w-harvey]).
  - British plates often used ordinary steel instead of Ni steel. "Harveyized mild steel" "resisted penetration as well as nickel steel, though … less tough" ([w-harvey], [okun-tbl]).
- **Manufacture:**
  - Cementation (carburising) in charcoal at ~1,200 °C for "two to three weeks". Surface C rises to ~1–1.10%, falling back to base C at ~1 in depth ([w-harvey], [no37]).
  - Then a water quench; Tresidder's 1887 water-spray patent was used ([no37]).
  - Corey (Carnegie) "double forging" after carburising cut thickness 10–15% for equal resistance ([no37], [nog57]).
- **Hardness:** face 680, back 190 (Ni-steel) or 140 (mild steel). Okun gives 575–700 at the surface ([okun-tbl], [okun-09]).
- **Face depth:** only the ~1 in cemented skin, so it is **thin-faced** ([okun-09]).
- **Tensile and elongation:** Ni-steel base 95 ksi, 18%; mild-steel base 60–68 ksi, 20–25% ([okun-tbl]).
- **Thickness range:**
  - "Vertical armor 3.2" and thicker" ([okun-tbl]).
  - EB1911 says Harvey plates were used "in the 3 to 5 in. range" after Krupp arrived ([eb]).
  - USN c.1900: Harvey "for plates having a thickness of five inches or less", Krupp above 5 in ([gs]).
  - Contracts ran up to 15.75 in in France ([usni94]); US belts were 16–18 in ([gs]).
- **Relative resistance:**
  - **~1.15–1.20 × nickel steel** ([no37], [ng1]), i.e. **~1.5–1.56 × WI** [INFERRED].
  - Measured FM 1.83–2.14 ([usni94]). EB1911 gives "about twice", "rather more than 2" at 6–8 in, and less for thick plates ([eb]). A French estimate gives 1.35 × simple steel and 1.80 × WI ([usni94]).
  - Okun: thin-faced Harvey gets a **~30% "shatter bonus" against uncapped shot at normal impact** ([okun-tbl]). That bonus vanishes against capped shot, so Harvey's value depends strongly on the projectile.
- **Cost:**
  - 1893 contracts paid the plain-steel price plus extras for Ni and Harveying, roughly $536/ton base ([hap]).
  - Russia, Dec 1895: Bethlehem **$627/ton**, Carnegie **$680/ton** for hard-faced Ni steel ([hap]).
  - The US Congress capped the price at $300/ton in 1897 ([hap]).

### 2.7 Krupp Cemented, old type (KC a/A, "alter Art", 1894)

- **Maker:**
  - Krupp, Essen. Large Ni-Cr ingots were solved c.1893, and Cr was introduced as an armour hardener in 1894 ([no37], [okun-cf]).
  - Licensed worldwide: Carnegie-Krupp ("CKC"), Bethlehem, Midvale, Vickers, Armstrong, Cammell, John Brown, Beardmore, Schneider, Terni, Witkowitz, Dillingen, Izhora, and others ([nw-017], [eb], [greg], [izh]).
  - Izhora set up "production of Krupp armour" in **1899** ([izh]). Terni used the Krupp process by 1908–10 ([greg]). The US "applied [it] to US armor in 1900" ([nh37]).
- **Years:** 1894–1918 ([okun-tbl]); in the US to 1923.
- **Composition:**
  - Okun: Ni 2–4%, Cr 1.5–3%, C 0.25–0.40% (some 0.55–0.6) ([okun-tbl]).
  - EB1911: Ni 3–4%, Cr 1.0–2.0%, C 0.25–0.35%, Mn 0.3–0.7% ([eb]).
  - Early US KC: C 0.27%, Ni 3.75%, Cr 1.75% ([no37]).
  - "Modern" KC (as given by Wikipedia and USN 1937): C 0.35, Ni 3.90, Cr 2.00, Mn 0.35, Si 0.07, P 0.025, S 0.020 ([w-krupp], [no37]).
- **Manufacture:**
  1. Cementation of the face. Krupp used gas (coal gas) cementation; Bethlehem adopted gaseous cementation in 1898 ([no37]).
  2. **Decremental hardening:** the face is heated to above the critical temperature over 30–40% of the thickness while the back stays cooler, then the plate is water-sprayed ([w-krupp], [no37]).
  - Forging reduced the ingot to ~⅓ of its thickness ([no37]). Super-carburising took 10–14 days for large forgings ([no37]).
- **Hardness:** cemented skin 650–700; deep face ~500, falling to ~350 at ~20% depth; transition ends at 33–35%; back ~225. Not tempered after hardening ([okun-cf]). Table average: 680/225 ([okun-tbl]).
- **Face depth:** ~20% face + 13–15% transition = **33–35%** ([okun-tbl]).
- **Tensile and elongation:** 92–105 ksi, 18–22% ([okun-tbl]).
- **Thickness range:**
  - Krupp and Witkowitz made it down to **3.2 in (8 cm)**; most others had a minimum of ~4 in ([okun-tbl]).
  - EB1911 says KC was made "down to about 2 in." and that Krupp cemented plates from 3 in up in 1895 ([eb]) [UNCERTAIN, minor].
  - Okun gives Witkowitz's heaviest plate as 11 in (28 cm) ([okun-tbl]).
  - Typical ingots ([no37]):
    - belt plate ~26″×132″×200″, ~200,000 lb
    - turret port plate ~425,000 lb
- **Uses:** belts, barbettes, turrets, conning towers, i.e. vertical armour.
- **Relative resistance:**
  - ~1.15–1.25 × Harvey (see §1). FM 2.3–2.7 vs uncapped and ~2.0 vs capped ([eb]).
  - Okun: ≈ **0.83 of WWII face-hardened quality** ([okun-tbl]).
  - The deep face adds 10–20% effective thickness against uncapped shot and 13–26% against capped shot, compared with thin-faced Harvey ([okun-tbl]).
- **Cost:**
  - Krupp-process price **$545/ton incl. royalty**; British price ~**£117/ton (≈$569)**; Russian battleship armour $575 ([hap], c.1898–99).
  - The US $300/ton cap of 1899 included royalties ([hap]). Royalty was $4.50–6.00/ton [UNCERTAIN, poor OCR].

### 2.8 Krupp Non-Cemented (KNC) and "Krupp Soft" / high-% nickel steel

- **Two meanings:**
  - (a) **Face-hardened KNC**: decrementally hardened without cementation. USN 1937 gives C up to 0.50%, Ni 3.5%, Cr 2.30–2.50%, "fully equal ballistically to K.C." but "prone to spalling … abandoned after a few years" in the US ([no37]).
  - (b) **British "KNC"** and German "Krupp Soft" (Krupp high-% Ni steel): **homogeneous** Ni-Cr armour. Okun gives 220 BHN, 96–113 ksi, 22%, 1900–1925, used for turret and CT roofs and vertical armour up to 4 in ([okun-tbl]).
    - Krupp high-% Ni steel: Ni 2–4%, Cr 1.5–3%, C 0.25–0.4%, 220 BHN, 113 ksi, 20%, 1894–1918, used for roofs and vertical armour ≤3.2 in. This is the base steel for KC ([okun-tbl]).
- **Relative resistance:** homogeneous Krupp-type armour through WWI ≈ **Q 0.95** (Okun, homogeneous scale) ([okun-biz]). EB1911 gives FM 2.25 ([eb]); a 3-in Vickers KNC plate reached 2.6 ([eb]).
- **Uses:**
  - Russian *Gangut*: KNC splinter bulkheads of 37.5–50 mm, a 50 mm KNC deck, and a KCA belt of 225 mm ([w-gangut]).
  - British pre-1925 turret roofs and decks.
- **[UNCERTAIN]** Different sources use "KNC" for both the face-hardened and the homogeneous material. The model should keep two separate entries.

### 2.9 US Class A by maker (1900–1923)

From [nw-017] and [okun-tbl]:

| Type | Years | Composition | Face | Hardness face/back | Notes |
|---|---|---|---|---|---|
| Carnegie-Krupp Cemented (CKC) | 1900–1923 | KC-like | 33–35% | ~650/195 (avg. WWI Class A) | "Slightly better than captured Krupp KC against new soft-capped 12-in" ([nw-017]) |
| Bethlehem Non-Cemented (BNC) | 1906–1910 | Cr ~4%, C ~0.6%, Ni ~2% ([okun-tbl]) | deep | — | "Very brittle", barely acceptable, dropped ([nw-017]) |
| Midvale Non-Cemented (MNC, US) | 1906–1910 | C 0.55%, otherwise KC-like | >80% chill, face hardened to ~25% | 490/200 | ~30% better than CKC vs 12-in soft-capped (shattered them); fell off sharply vs 14–16 in; vs 16 in "no better than Harveyized Ni-steel"; 343 °C final temper ([nw-017], [okun-tbl], [okun-cf]) |
| Bethlehem Thin Chill (BTC) | 1922–23 | KC-like | ~15% | 650/200 | could not damage hard-capped projectiles until ~40° obliquity ([okun-tbl]) |
| Average WWI Class A | 1900–1923 | — | KC-like | 650/195; 93–100 ksi, 26% | ([okun-tbl]) |

*Arkansas* carried plates from all three makers ([nw-017]). USN acceptance was by lots of 600–1,200 tons, each tested with two major-calibre AP shots ([no37]).

### 2.10 US Class A, WWII (1933–1955)

- **Composition (typical):** C 0.33, Ni 3.33, Cr 2.00, Mn 0.30, Si 0.07, P 0.016, S 0.02 ([nog57]).
- **Face:** 35–40% face + 15–20% transition, **~55%** "Thick Chill" ([okun-tbl], [okun-biz]).
- **Hardness:** 650/220 ([okun-tbl]).
- **Tensile and elongation:** 100–114 ksi, 23–29% ([okun-tbl]).
- **Thickness range:** vertical ≥5 in, except 16 in+ turret faces, which were Class B ([okun-tbl], [okun-biz]). In 1957 the boundary between Class A and STS was put at ~3 in ([nog57]).
- **Quality:** the **reference, Q = 1.00** (Okun FH, BLT 45%) ([tt-q]).
  - "Best face hardened armor used by anyone" at ≤7 in, but thick plates were "inferior" to KC n/A and CA ([okun-tbl], [nw-017]).
  - Okun's comparisons: "relative resistance declined against larger projectiles" ([nw-017]). The USN also specified that FH armour must **damage projectiles**, which cost some absolute resistance ([tt-ca]).
- **Mounting:** ~2 in stand-off from the hull, the gap filled with concrete ([no37], [gs]).

### 2.11 US Class B, STS, and cast armour

**Class B, WWI:**
- C ~0.30, Ni ~3.85, Cr ~1.85; vanadium used c.1909–1914 ([no37]).
- 115 ksi UTS, 23% elongation, 65% reduction of area ([no37]).
- Rolled below 4 in, forged above ([no37]).
- 240 BHN, 1910–1932 ([okun-tbl]).

**Class B, WWII (1933–55):**
- 200–240 BHN, 92–120 ksi, 25% ([okun-tbl]).
- Bethlehem <7 in: Mo 0.3–0.4%, V 0.1–0.14%, C 0.18–0.20%. Midvale <15 in: Mo 0.2–0.3% ([okun-cf]).
- Used for turret roofs and for faces of 16 in or more: 16–19.5 in faces were Class B ([okun-biz]).
- **Q 1.00** on the homogeneous scale ([okun-biz]).

**STS:**
- Carnegie developed it c.1910 ([w-sts]).
- C 0.35–0.40, Ni 3.00–3.50, Cr 1.75–2.00, no Mo; V until 1914 ([w-sts]).
- YS 75–85 ksi, UTS 110–125 ksi, **25% elongation**, 68% reduction of area, 200–240 BHN ([w-sts]).
- Under 4 in before 1930 and under 5 in after ([okun-tbl]); "3 in and less" in 1957 ([nog57]).
- **Structural:** STS was used as **hull structure** (bulkheads, decks, lower belts) on almost every USN class from 1930 ([w-sts]).
- **Q 1.00** ([okun-biz]).
- **Scale effect:** the Navy ballistic limit drops only 1.4% for STS, against 13.2% for Wotan Hart, as projectile diameter rises for the same thickness-to-diameter ratio ([okun-cf]).

**Cast armour:**
- Used for "small fittings, such as turret range-finder end windows and sight hoods" ([nog57]).
- Homogeneous Krupp-type cast armour has **Q 0.9** ([okun-biz]).
- Krupp's 1902 cast face-hardened Ni steel reached FM >2.2 ([eb]). Hadfield cast gun shields ran to 6 in ([eb]).
- [UNCERTAIN] I found no evidence of large USN cast Class B turret faces on battleships in this era. Treat cast armour as a fittings and shields material with a quality penalty.

### 2.12 German post-WWI Krupp: KC n/A, Wotan Hart, Wotan Weich, Wotan Starrheit

**KC n/A (neuer Art):**
- Developed late 1920s to early 1930s; Okun's table gives 1928–1945 ([okun-cf], [okun-tbl]).
- Face 650–700 BHN; ~500 just behind the cemented layer; **back 240**, "the maximum used by anyone"; **face 41%**; tempered ([okun-cf], [okun-biz]).
- Mo added ([okun-tbl]).
- 112–117 ksi, 22% ([okun-tbl]).
- Made from just under 4 in (100 mm) to 14.96 in (380 mm) ([okun-biz]).
- **Q 0.96** (FaceHard, BLT 59%) ([tt-q], [tt-ca]).
- Tests ([okun-tests]):
  - 8-in AP at 30°: a 21.5 cm KC n/A plate reached 113% of the US reference "best plate".
  - 14-in at 30°: 35–38 cm plates reached 95–97.5% of the "average" US plate limit.
  - Overall, Okun scored 4 KC n/A plates "significantly superior", 1 "approximately the same" and 1 "significantly inferior" to the closest US armour.

**Wotan Hart (Wh):**
- 1925–45 ([okun-tbl]); 225–250 BHN, 113–127 ksi, **~18%** elongation ([okun-tbl], [okun-cf]).
- Mo-bearing Ni-Cr steel ([okun-tbl]).
- Used for decks, slopes, and vertical armour of 1–4.33 in ([okun-biz], [okun-tbl]).
- Equal to the best non-German homogeneous armour in thin plates ([okun-tbl]), but **scales badly**:
  - 8-in AP at 35°: a 7-in Wh plate reached 105% of the US Class B average.
  - 12- and 14-in shells: 29–45 cm Wh plates reached **<89–94%** of the US Class B average.
  - Okun scored 3 of 4 Wh plates "significantly inferior" ([okun-tests]).
  - This fits the 13.2% scaling drop above ([okun-cf]).

**Wotan Weich (Ww):**
- ~180–200 BHN, 92–117 ksi, 22% ([okun-tbl], [okun-biz]).
- Maximum 50 mm by Krupp specification; used for torpedo bulkheads ([okun-biz]).
- 1 in Ww ≈ 0.95 in Wh ([okun-biz]). No significant difference from Wh under underwater blast ([okun-tbl]).

**Wotan Starrheit (Wsh):**
- 250–280 BHN, 128–142 ksi, 16%, up to 50 mm.
- Used for MG-proof and splinter shields and director hoods ([okun-tbl], [okun-biz]).

**Krupp low-% Ni steel:**
- 150–160 BHN, 73–78 ksi, 21%, 1900–1918.
- Used for decks and torpedo bulkheads, the German counterpart to British HT ([okun-tbl]).

### 2.13 British: Vickers/Armstrong/Cammell/Brown/Beardmore KC; post-1930 CA and NCA; HT; Ducol (D-steel)

**British KC (1905–1925), also Japanese VC and Italian WWI KC:**
- 650/210 BHN, "modified KC" with a softer face than KC a/A ([okun-tbl]).
- 96–113 ksi, 20% ([okun-tbl]).
- **"Inferior to German KC a/A"**, with curved plates brittle under high-obliquity ricochet ([okun-tbl]).
- Pre-1905 British cemented plate trials, all from [eb]:
  - Beardmore 6 in (Eskmeals, 1901): FM 2.6–2.8 vs uncapped, below 2.2 vs capped.
  - Armstrong 9 in KC (for Japan): >2.45.
- Shell proof standards against British KC (c.1918–20), relayed on the NavWeaps forum ([tt-ca]):
  - 15 in Cardonald proof required defeating 12 in "C" at 40°.
  - Hadfield 14 in could defeat 10 in "C" at 45°.
- [UNCERTAIN] I found no hard numbers for a "post-Jutland" British armour improvement, as distinct from the much larger shell improvement.

**CA (Cemented Armour), 1933–46:**
- Mo 0.4% ([okun-tbl]).
- Face 15% + transition 10%; often quoted as ~30% face ([okun-biz]).
- Cemented skin ~600 BHN (RC 58), the lowest of the KC types; back 225 ([okun-cf], [okun-tbl]).
- **120 ksi, 25% elongation**, the highest backing tensile of any WWII face-hardened armour ([okun-tbl]).
- Over 4 in.
- **Q 0.928** in FaceHard (BLT 70%) ([tt-q]). Even so, Okun calls it "the most resistant type at battleship thicknesses" ([okun-biz]) and "best for large thicknesses" ([okun-tbl]).
  - Robertson's FaceHard reading: CA has a "marked advantage" over KC n/A up to ~35° obliquity, and they are equal at 40° ([tt-ca]).
  - [UNCERTAIN] Q is fitted at 8 in thickness. CA's thin face and thick tough back give a better scaling exponent, so a single Q understates CA at 12–15 in.

**NCA (Non-Cemented Armour), 1926–46:**
- Homogeneous, 225 BHN, Mo 0.4%, **120 ksi, 25%** ([okun-tbl]).
- Used for roofs, decks and vertical armour under 4 in. D-steel substituted up to 2 in to save cost ([okun-tbl]).
- "Normally equal to any foreign armor of its type", though WWII thin-plate quality control was lax ([okun-tbl]).

**HT (High-Tensile) steel, Admiralty c.1900–WWI:**
- C 0.35–0.40, Mn 0.8–1.2, Si 0.15 ([w-ducol]).
- ~160 BHN, ~78 ksi, 22% ([okun-tbl]).
- Used for hull construction, protective decks, torpedo bulkheads and splinter protection, often laminated in 1–2 in layers ([okun-tbl]).

**Ducol / "D"-steel (Colville, early 1920s):**
- C 0.24–0.30, Mn ~1.5 (up to 1.6), Si 0.06–0.10 ([w-ducol]).
- Japanese bridge tests: 63–71 kg/mm² (≈ 90–101 ksi), 20–23% elongation ([w-ducol]); 170 BHN, 89 ksi, 22% ([okun-tbl]).
- Used in British, Japanese and Italian ships, not in German or US ones ([okun-tbl]):
  - *Nelson* and *Rodney* (1927)
  - KGV weather deck and bulkheads
  - *Ark Royal*'s armoured hangar
  - many IJN ships, e.g. *Shōkaku*'s 25 mm magazine protection ([w-ducol])
  - the Italian "ER" Si-Mn steel of Pugliese bulges, 28–40 mm, is "probably similar" ([w-ducol])
- **Weldability:** the 1920s Ducol was weldable "with dubious results". The all-welded Ducol bulkheads of the *Mogami* class distorted the hull and were rebuilt riveted ([w-ducol]).

### 2.14 Japan: VC, VH, NVNC, MNC, CNC

**VC (Vickers Cemented):**
- Japan's KC, from British Vickers practice, 1905–1925 class ([okun-tbl]).
- Same entry as British KC: 650/210 ([okun-tbl]).
- Imported British plates came first, e.g. Armstrong's 9 in KC for Japan at FM >2.45 ([eb]).

**VH (Vickers Hardened, non-cemented FH), 1937–45:**
- Used only for vertical armour over ~11 in, up to 26 in on *Yamato* turret faces ([okun-tbl], [nw-040]).
- C 0.55%, ~0.5% Cu replacing part of the Ni ([okun-tbl]).
- Face ~35% ([okun-tbl]). The *Shinano* 26 in plate had HRC 50 at 0.75 in, a face to ~5 in (~19%), and HRC 11–15 at the back ([nw-040]).
- 98–106 ksi, 23% ([okun-tbl]).
- *Shinano* coupons ([nw-040]):
  - longitudinal: YS 68.5 ksi, UTS 99.4 ksi, 22.3% elongation
  - normal: UTS 93.2 ksi, only 7% elongation
- **Q ≈ 0.839** vs US Class A for the 26 in plate. It was "excessively brittle internally" from upper bainite caused by slow cooling, and "about equal to the best WWI-era British KC" ([nw-040]).
- By contrast, an experimental 7.21 in VH plate on the KC n/A pattern was "the best plate of its thickness range … ever tested by the U.S. Navy" ([nw-040]).
- Okun declined to give Japanese FH a single Q because it was made "to a WWI-era specification" ([tt-q]).

**NVNC (New Vickers Non-Cemented), 1926–45:**
- 220 BHN, C 0.55%, Cu ~0.5% replacing Ni, 100–110 ksi, 20% ([okun-tbl]).
- Used for roofs and vertical armour under 12 in, including belts ([okun-tbl]).
- "Somewhat inferior to most WWII homogeneous armors" ([okun-tbl]). I estimate **Q ≈ 0.90** [INFERRED].

**MNC (Molybdenum Non-Cemented, Japan), 1941–45:**
- 210–235 BHN, 100–120 ksi, 23% ([okun-tbl]).
- *Yamato* main deck, 200–230 mm (7.87–9.06 in) ([okun-tbl]).
- "Only slightly better than NVNC" ([okun-tbl]). I estimate **Q ≈ 0.92** [INFERRED].

**CNC / CNC1 / CNC2 (Copper Non-Cemented), 1931–45:**
- Up to 0.85% Cu replacing Ni in homogeneous plate under 3 in. In CNC1/CNC2, ~0.25% of the Cr was replaced by Mo and the maximum thickness rose to 3.21 in (81.5 mm) ([okun-cf]).
- 225 BHN, 110–122 ksi, 22% ([okun-tbl]).
- A "moderately successful Ni-saving effort" ([okun-tbl]). I estimate **Q ≈ 0.90–0.95** [INFERRED].

### 2.15 Italy: Terni cemented and non-cemented

**Terni:**
- Terni was founded in 1884 [INFERRED, general knowledge].
- It used the Krupp process for Regia Marina armour by 1908–10 ([greg]).
- WWI Italian KC is grouped with British KC at 650/210 and "inferior to German KC a/A" ([okun-tbl]).

**WWII Italian (Terni) KC, 1929–43:**
- 650/215 BHN, an assumed 35% face, 106–114 ksi, **18%** ([okun-tbl]).
- "Lower yield than other WWII KC"; quality "assumed comparable … (little data)" ([okun-tbl]).
- Okun's FaceHard: **Q 0.98, BLT 55% (?)** ([tt-q]).
- Use: the *Littorio* belt was a 70 mm homogeneous outer plate, a 250 mm gap filled with "Cellulite", then a 280 mm cemented belt on 150 mm oak and a 15 mm steel backing plate ([w-litt]).

**PO (Piastre Omogenee, 1929–43):**
- 225 BHN, 121 ksi, **16%** elongation ([okun-tbl]).
- Used for roofs, decks and vertical armour under 4 in, with D-steel substituted up to 50 mm ([okun-tbl]).
- "Equal to all foreign armor of its type (little data)" ([okun-tbl]).
- [UNCERTAIN] The low elongation suggests it may scale like Wh rather than like STS.

### 2.16 France: Schneider/Creusot, Saint-Chamond, Châtillon-Commentry, Marrel

- **1876:** Schneider all-steel (§2.3).
- **1889:** Schneider nickel steel (§2.5).
- **1890s:** Harvey. Saint-Chamond, Châtillon-Commentry and Marrel took plates up to 15.75 in ([usni94]). Saint-Chamond's untreated Ni-Cr armour was claimed to "equal, and in some cases surpass, Harveyed plates" ([usni94]).
- **1912:** France introduced molybdenum into armour ([okun-cf], [okun-tbl]).
- **1930s:** FH armour was used even on turret and CT **roofs** over 4 in ([okun-tbl]). The *Richelieu* turret top is the only horizontal FH armour Naval Gazing knows of, and it was holed when hit ([ng2]).
  - *Richelieu*: 327 mm belt inclined 15°24′ on 60 mm teak; 150–170 mm deck on 15 mm plate ([w-rich]).
- **Quality:** Okun **assumed Q 1.00, BLT 65%** for French FH, but German tests of French plates "found them inferior" ([tt-q]) **[UNCERTAIN]**. A model value of **0.90–0.95** is safer.
- [UNCERTAIN] I found no composition data for French homogeneous armour of the 1930s.

### 2.17 Austria-Hungary / Czech: Witkowitz (Vítkovice), Škoda

**Witkowitz (Witkowitzer Bergbau- und Eisenhütten-Gewerkschaft, Moravia):**
- Adopted Harvey by 1894 ([usni94]).
- Its KC used normal KC a/A chemistry plus a **toughening process and post-hardening temper** that kept the face from cracking early ([okun-tbl]).
- Thickness 3.2 in to a maximum of 11 in (28 cm) ([okun-tbl]).
- Tested with Škoda 30.5 cm AP. Rated "significantly superior to all other face-hardened plates made through the end of WWI", **~95% as good as WWII FH, against ~83% for KC a/A** ([okun-tbl]).
- Okun calls this a probable WWII-quality outlier, i.e. **Q ≈ 0.95 from c.1905–1918** [dates INFERRED].

**Škoda:** in this period it was the gun and shell maker. I found no Škoda armour-plate data for 1859–1918 [UNCERTAIN].

**Interwar Czech Vítkovice:** no data found [UNCERTAIN].

### 2.18 Russia: Izhora (Kolpino), Obukhov

- **Izhora:**
  - First armour plates in 1866 ([izh2]); a steel shop in 1892; "Krupp armour" from 1899 ([izh]).
  - A new armour plant in 1910 and a 10,000-ton press in 1912 ([izh2]).
  - The Kolpino plant cost $8–10 million ([hap]).
- **Imports:** Russia also bought abroad.
  - Bethlehem: 1,264 tons at **$249/ton** in Dec 1894, a below-cost price.
  - Dec 1895: Bethlehem $627 and Carnegie $680/ton for hard-faced Ni steel ([hap]).
  - The *Poltava* class shows the transition within one class: *Petropavlovsk* had nickel steel, *Sevastopol* had Harvey, and *Poltava* had Krupp ([ng1]).
- **Dreadnought era:** the *Gangut* class had a 225 mm KC belt, KNC decks and bulkheads, and a 203 mm KC turret face ([w-gangut]).
- **Quality:** no published Q found [UNCERTAIN]. Treat it as KC a/A at Q 0.80–0.83 [INFERRED]. I found no Obukhov armour-plate specifics.

---

## 3. Non-steel protection

| Material | Role | Equivalence / data | Source |
|---|---|---|---|
| Teak backing | Spread load, absorb shock, stop splinters, bolt bed | *Warrior*: 18 in under 4.5 in WI. *Inflexible*: 17–25 in in sandwich. *Colossus*: 20 in under 18 in compound. *Richelieu*: still 60 mm teak in 1939. | [w-warr], [w-infl], [dp-col], [w-rich] |
| Oak backing | Same as teak | *Littorio*: 150 mm oak behind the 280 mm belt. 1890s test plates were backed by 36 in oak. | [w-litt], [usni94] |
| Wood backing in general | Same | 18–36 in under early iron | [w-iron] |
| Coal bunkers | Splinter/shell absorption, also fuel | "2 feet of coal was equivalent to an inch of armour plate" | [dp-col] |
| Cork (cofferdam fill) | Buoyancy, flood limitation | *Inflexible*: 4-ft cork-filled wing compartments. No ballistic value. | [w-infl] |
| Cellulose / "Cellulite" | Flood limitation, holes self-seal | *Littorio*: Cellulite (cement foam) filled the 250 mm gap between the 70 mm and 280 mm belts. No ballistic value found. | [w-litt] |
| Concrete | Filling between belt and hull | Class A stood ~2 in off the hull, filled with concrete | [no37], [gs] |
| Granite | Test backing only | Cammell's 1883 test plate was granite-backed | [eb] |

Weights for the game [INFERRED]:
- Teak ≈ 650 kg/m³ (~40 lb/ft³), so 1 in of teak is ~3.4 lb/ft², about 8% of 1 in of iron by weight.
- Coal stowed at roughly 45 ft³/ton is ~800 kg/m³. 2 ft of coal is then ~100 lb/ft², about **2.5×** the weight of the 1 in of steel it replaces. It is worth something only because it is carried anyway as fuel, and it is lost as bunkers are emptied.
- Splinter mats: no equivalence numbers found [UNCERTAIN]. Treat them as negligible against shells and partial against fragments.

---

## 4. (a) Timeline: best available belt armour by navy

**References used:**
- **Q** is Okun's scale: WWII US Class A ≤8 in = 1.00.
- **FM** is inches of WI per inch, at normal impact against period projectiles.

Q for pre-Krupp materials has no direct Okun source. I derived it by chaining the §1 ratios from KC a/A = 0.83 [INFERRED]:

| Material | Q |
|---|---|
| KC a/A | 0.83 |
| Harvey | 0.83 / 1.19 ≈ 0.70 |
| Nickel steel | 0.70 / 1.18 ≈ 0.59 |
| Compound / all-steel | 0.59 / 1.04 ≈ 0.57 |
| Wrought iron | 0.57 / 1.25 ≈ 0.45 |

FM for the post-Krupp materials uses FM(WWII) ≈ 2.45 [INFERRED], i.e. FM ≈ 2.45 × Q.

| Material (generic) | Q (Class A WWII = 1) | FM (WI = 1) | Basis |
|---|---|---|---|
| Wrought iron | 0.45 [INF] | 1.00 | [ng1], [eb] |
| Chilled cast iron | n/a (shatters shot, cracks) | — | [okun-tbl] |
| Compound / all-steel | 0.56–0.58 [INF] | 1.25–1.3 | [no37], [eb] |
| Nickel steel | 0.59 [INF] | 1.30 | [no37] |
| Harvey Ni-steel | 0.68–0.72 [INF] | 1.5–2.1 (thin plates read high) | [no37], [usni94], [eb] |
| KC a/A (1894–1918) | 0.80–0.85 | 2.0 (capped) – 2.7 (uncapped, 6 in) | [okun-tbl], [eb] |
| British/Japanese/Italian WWI KC (VC) | 0.78–0.84 [INF: "inferior to KC a/A"] | ~2.0–2.5 | [okun-tbl], [nw-040] |
| Witkowitz KC | ~0.95 | ~2.3+ [INF] | [okun-tbl] |
| WWII US Class A | 1.00 | ~2.45 [INF] | [tt-q] |
| WWII Italian KC | 0.98 | — | [tt-q] |
| KC n/A | 0.96 | — | [tt-q] |
| British CA | 0.928 (but best at >12 in) | — | [tt-q], [okun-biz] |
| French WWII FH | 1.00 assumed; probably 0.90–0.95 | — | [tt-q] [UNCERTAIN] |
| Japanese VH (26 in) | 0.84 | — | [nw-040] |

**Timeline by navy.** Ship examples are cited where sourced; other placements are [INFERRED] from licensing and adoption dates given in §2.

| Year | UK | US | Germany | France | Italy | Russia | Japan | A-H |
|---|---|---|---|---|---|---|---|---|
| 1860 | WI 4.5″ + teak (*Warrior*) FM 1.0 | — (WI on monitors) | WI | WI (*Gloire*) | WI | WI | — | WI |
| 1868 | WI; Gruson for forts in Germany | WI | WI; Gruson land turrets | WI | WI | WI (Izhora 1866) | — | WI |
| 1876 | WI sandwich 24″ (*Inflexible*) FM 1.0 | WI | WI | Schneider steel available, FM 1.25 | **Schneider steel 22″**, FM 1.25 (Q≈0.57) | WI | — | WI |
| 1880 | Compound (Cammell/Brown), FM 1.25 | WI | Compound [INF] | Steel/compound, FM 1.25–1.3 | Steel | Compound/steel [INF] | — | Compound [INF] |
| 1887 | Compound | **Steel** (Bethlehem contract, $536/t) | Compound | Steel | Steel | Compound | Imported | Compound |
| 1890 | Compound → Ni steel | **Ni steel** (Annapolis), FM 1.3, Q≈0.59 | Compound/Ni steel | Ni steel | Ni steel | Ni steel (*Petropavlovsk*) | Imported | Ni steel [INF] |
| 1893 | **Harvey** (steel or Ni), FM ~1.8–2.0, Q≈0.70 | **Harvey Ni** | Harvey/Krupp trials | Harvey (St-Chamond/Marrel/Châtillon) | Harvey [INF] | Harvey (*Sevastopol*) | Harvey (British-built) | Harvey (Witkowitz) |
| 1895 | Harvey | Harvey | **KC a/A**, Q≈0.80 | Harvey | Harvey | Harvey/KC (*Poltava*) | Harvey | Harvey/KC |
| 1900 | KC (licensed) Q≈0.80 | **CKC Class A** Q≈0.80 | KC a/A 0.83 | KC (licensed) [INF] | KC [INF] | KC (Izhora 1899) | KC (British-built) | KC (Witkowitz) |
| 1905 | British KC Q≈0.80 | Class A, 3 makers (0.75–0.85, maker-dependent) | KC a/A 0.83 | KC | KC (Terni) | KC | VC (imported/licensed) | **Witkowitz KC ~0.95** |
| 1910 | British KC | CKC/MNC/BNC [maker spread] | KC a/A | KC + Mo (1912) | Terni KC 0.80 | KC (*Gangut* design) | VC | Witkowitz ~0.95 |
| 1915 | British KC | Class A | KC a/A | KC | Terni KC | KC | VC | Witkowitz |
| 1920 | British KC (post-war) | Class A (BTC 1922–23, thin chill) | — (no capital ships) | KC | KC | — | VC/NVNC | — |
| 1925 | British KC → *Nelson* (KC/NC + Ducol) | holiday | Wotan (Wh/Ww) emerging | — | — | — | NVNC (1926) | — |
| 1930 | KC → **CA** (1933) | Class A resumes (1933) | **KC n/A** (1928+) Q 0.96 | FH (*Dunkerque*) | **Terni WWII KC** (1929) Q 0.98 | Soviet — no data | VC/NVNC; CNC (1931) | — |
| 1935 | **CA** Q 0.928 (best >12″) | **WWII Class A** Q 1.00 | KC n/A 0.96 | FH (*Richelieu*), ~0.9–1.0 [UNC] | Terni KC 0.98 | — [UNC] | **VH** (1937) 0.84 at 26″ | (Czech) — |
| 1945 | CA | Class A | KC n/A | FH | Terni KC | — | VH / MNC | — |

Notes:
- The **US 1906–23 Class A** spread matters. Midvale MNC was ~1.3 × CKC against soft-capped 12 in but no better than Harvey against 16 in ([nw-017]). A model could add a per-plate "maker variance" for the US in 1906–23.
- For the **ironclad era** (≤1890), FM against WI is the natural scale: WI 1.0 → compound/steel 1.25 → Ni steel 1.30.

---

## 5. (b) Homogeneous vs face-hardened: where each was used, and why

Thresholds by source:

| Navy / era | FH used at | Homogeneous used at | Source |
|---|---|---|---|
| Harvey era (US, c.1900) | Krupp >5 in | Harvey ≤5 in | [gs] |
| EB1911 | KC down to ~2 in; Harvey 3–5 in | — | [eb] |
| Krupp/Witkowitz KC a/A | ≥3.2 in (8 cm) | — | [okun-tbl] |
| Most other makers | ≥~4 in | — | [okun-tbl] |
| WWII British CA, KC n/A, VH, Italian KC | >4 in (KC n/A from just under 100 mm) | <4 in: NCA, Wh, PO | [okun-tbl], [okun-biz] |
| WWII US Class A | ≥5 in | STS <4–5 in | [okun-tbl] |
| US 1957 | Class A > ~3 in | STS ≤3 in; Class B >3 in, horizontal | [nog57] |
| VH | >11 in only | — | [okun-tbl] |
| Very thick faces (US) | — | 16–19.5 in turret faces were Class B | [okun-biz] |

**Why face hardening was not used for thin plates:**
- Against a thin plate, **"the thickness of the tough back is the key factor"**. Face-hardening armour under about 3 in "makes it easier to penetrate" ([ng2]).
- The hard face is brittle, so it adds little stopping power on its own. In a thin plate it takes away tough back thickness and promotes cracking and plugging [INFERRED, consistent with [ng2]].
- Manufacturing also set a floor: decremental hardening needs enough thermal mass to keep the back cool while the face goes over the critical temperature [INFERRED].

**Why homogeneous armour was used for decks and steep slopes:** decks are hit at high obliquity, where **"face-hardening is worse"** ([ng2]). A hard face also does not stop a ricochet from gouging the plate.

**Why homogeneous armour was used for very thick faces (US 16 in+):**
- Okun gives US Class A's thick 55% face as best at ≤7–8 in but **declining against larger projectiles** ([nw-017]).
- Making a 16–19 in FH plate was hard. VH at 26 in shows the cost: a slow-cooled centre turned to upper bainite and the plate was brittle ([nw-040]).

**Why face hardening was worth having:** the face breaks or deforms the projectile. Against uncapped shot this gave a ~30% "shatter bonus" even for thin-faced Harvey. A deep KC face added a further 10–20% against uncapped shot and 13–26% against capped shot ([okun-tbl]). AP caps (1894 Russian experiments, [eb]) largely cancelled the shatter bonus, and KC FM fell from ~2.5 against uncapped shot to ~2.0 against capped shot ([eb]).

**Face-depth "styles" in WWII:**

| Armour | Face / back | Source |
|---|---|---|
| US Class A | 55% / 45% | [tt-q] |
| Italian | ~45% / 55% | [tt-q] |
| KC n/A | 41% / 59% | [tt-q], [okun-biz] |
| French | ~35% / 65% | [tt-q] |
| British CA | 25–30% / 70% | [tt-q], [okun-tbl] |

Deep faces suit small and medium shells, which they break. Thin faces with thick tough backs suit large, well-capped shells.

---

## 6. (c) Why later armour was better: metallurgy

1. **Alloying for toughness (Ni, 1889).** About 4% Ni gave a tough, crack-resistant steel. At Annapolis the all-steel plate cracked and the Ni-steel plate did not ([usni90]). Nickel also made deep hardening possible.
2. **A surface hardness gradient (Harvey cementation, 1890–91).** Carburising to ~1% C at the face, then quenching, produced a ~600–700 BHN martensitic skin over a soft back ([w-harvey], [okun-09]). This breaks uncapped shot.
3. **Chromium and decremental hardening (Krupp, 1894).**
   - Cr at 1.5–3% gives hardenability, so a deep hard face can form.
   - Decremental heating grades the face through 30–40% of the thickness before the back begins ([okun-cf], [w-krupp]).
   - Cr appeared in projectiles c.1890 and in armour in 1894 ([okun-cf]).
4. **Heat-treatment control: tempering and toughening.**
   - The Midvale and Bethlehem experience shows that the final temper governs performance. Midvale MNC at a 343 °C temper behaved well; at 496 °C it acted like brittle BNC ([okun-cf]).
   - Witkowitz's post-hardening temper gave near-WWII performance before 1918 ([okun-tbl]).
   - KC n/A was tempered; KC a/A was not ([okun-cf]).
5. **Molybdenum (France 1912; widespread in the 1930s).**
   - ≤0.4% Mo, falling as plate thickness rises in WWII Krupp steels ([okun-09]).
   - It reduces temper embrittlement and improves hardenability in thick sections [INFERRED, standard metallurgy].
   - British CA and NCA carried ~0.4% Mo ([okun-tbl]).
6. **Cleaner steel and lower Si.**
   - Si fell from ~0.25 ±0.1% (WWI) to 0.05–0.1% (WWII) ([okun-cf]).
   - Okun: "most of its quality variation is caused by impurities or poor smelting practice", and later gains came "due to the reduction in the amount of impurities in the metal and tighter quality control" ([okun-cf]).
7. **Back strength.** WWII backs reached 220–240 BHN and 112–120 ksi at 22–25% elongation, against ~195–225 BHN and 92–105 ksi in WWI ([okun-tbl]). A stronger, tough back resists plugging and bending at large thickness.
8. **Scale effects and ductility.** Low-elongation armours lose more resistance as plate and shell grow: Wh at 18% elongation suffers a 13.2% ballistic-limit drop, STS at 25% only 1.4% ([okun-cf]). Thick-plate quality therefore depends on **elongation and thick-section uniformity** as well as hardness. This is why VH (upper bainite in the centre) and Wh fell behind at large thickness.
9. **Tuning to projectiles.** Each step was answered by shells: caps from 1894, then hard caps. KC FM fell from ~2.5 to ~2.0 against capped shot ([eb]). Later armour became better partly by being tuned to defeat capped shot: deeper faces, tougher backs, and the right face-to-back ratio.

---

## 7. (d) Density and weight implications

| Material | Density kg/m³ | lb/ft² per inch | Source |
|---|---|---|---|
| Wrought iron | 7,600–7,900 (use 7,700) | ~39.1 [INF] | [w-wi] |
| White/chilled cast iron | 7,400–7,700 (use 7,550) | ~38.4 [INF] | [mmfg] |
| Mild/carbon steel | 7,850 | 40.0 (USN convention) | [mmfg], [ng1] |
| Ni-Cr armour steels (Ni-steel, Harvey, KC, Class A/B, STS, Wotan, CA/NCA, VH, NVNC) | 7,850 (Ni adds <0.5%) | 40.0 | [mmfg] [INF re Ni] |
| Cast steel (cast armour) | 7,800 | 39.7 | [mmfg] |
| Mn steels (HT, Ducol) | ~7,810–7,850 | 40.0 | [mmfg] |
| Compound (⅓ steel face + ⅔ WI back) | ~7,750 [INF] | ~39.4 | — |
| Teak | ~650 [INF] | ~3.4 | — |
| Oak | ~700–750 [INF] | ~3.7 | — |
| Coal (bunkered) | ~800 [INF] | ~4.2 | — |

Implications for the weight model:
1. **Density differences between metal armours are at most ~2–4%.** WI and cast iron are ~2–4% lighter than steel. Steel alloys differ from each other by <0.5% and are not worth modelling. Weight per inch can stay at "steel" for everything except WI, cast iron and compound.
2. **Backing weight matters in the ironclad era.** Using the weights above [INFERRED]:
   - *Warrior*: 4.5 in WI ≈ 176 lb/ft² plus 18 in teak ≈ 61 lb/ft², so **backing ≈ +35%** on the armour weight.
   - *Inflexible*: 24 in iron plus 17 in teak plus skin ≈ 1,100 lb/ft² ([w-infl]); teak is only ~6% of that.
   - The thinner the plate, the larger the backing's share of the weight.
   - Model ironclad backing as a separate layer with near-zero resistance (it works only as a splinter catcher) and real weight.
3. **Steel-era backing is smaller but present.** *Richelieu* had 60 mm teak (~8 lb/ft²); *Littorio* had 150 mm oak plus 15 mm steel (~20 + 24 lb/ft²); US belts had a concrete fill of ~2 in ([w-rich], [w-litt], [no37]). Use ~3–6% of belt weight.
4. **Weight efficiency is the point.** Resistance per ton scales with Q at constant density. Changing a belt from WI to WWII Class A raises protection per ton by ~2.2–2.5× [INFERRED from FM 2.45 and the density ratio 7,850/7,700]. A Krupp belt of 12 in replaced a Harvey belt of 16.5 in, a weight saving of ~23% ([hap]).

---

## 8. What a model needs from this

Proposed per-material parameter table. The **Q** column is resistance per unit thickness relative to WWII US Class A at ~8 in (Q=1.00). Effective thickness = Q × actual thickness, which plugs straight into a penetration formula calibrated for Class A.
- **FH** = face-hardened (projectile-breaking behaviour applies).
- **Face %** = hardened layer as a percentage of thickness.
- **Scale k** = an optional penalty per doubling of plate thickness above 8 in, for brittle or low-elongation steels [INFERRED, from §2.12, §2.14, §5].
- Years are availability for **new** construction. Ships keep their original armour.

| id | Material | ρ kg/m³ | FH | Q (vs WWII Class A) | FM vs WI | Face % | Usable thickness (in) | Scale k / note | Availability by nation |
|---|---|---|---|---|---|---|---|---|---|
| WI | Wrought iron (+backing layer) | 7,700 | N | 0.45 [INF] | 1.00 | 0 | 2–24 (single ≤~22) | needs timber backing layer | All 1859–c.1880 |
| CCI | Chilled cast iron (Gruson) | 7,550 | FH-like (brittle) | ~0.6 first hit, then degrades [INF] | — | 33 | 12–24, forts only | crack accumulation | DE forts 1868–90; not shipborne |
| STL | Mild/all-steel (Schneider) | 7,850 | N | 0.56 [INF] | 1.25–1.3 | 0 | ≤22 | brittle cracking | FR/IT 1876–90; US 1887–90 |
| CMP | Compound | 7,750 | quasi-FH | 0.56 [INF] | 1.25 | 25–33 | 4–20 | face delamination | UK 1877–90, DE/RU/DK/A-H c.1880–90 [INF] |
| NIS | Nickel steel | 7,850 | N | 0.59 [INF] | 1.30 | 0 | 2–18 | tough, good multi-hit | FR 1889; US 1890; all by 1891 |
| HRV | Harvey (Ni or mild base) | 7,850 | FH (thin) | 0.70 [INF] (×1.3 vs uncapped normal) | 1.5–2.0 | ~1 in skin (≈5–10%) | 3.2–18 | big uncapped/capped split | US/UK/FR/RU/JP/A-H 1891–99 |
| KCA | KC a/A (and licensees, incl. CKC, Russian, Terni pre-WWI) | 7,850 | FH | 0.83 (0.80–0.85) | 2.0–2.7 | 33–35 | 3.2–4 up to ~14 | — | DE 1894–1918; US 1900–23; RU 1899+; IT c.1905+; FR c.1900+ [INF] |
| BKC | British/Japanese VC/Italian WWI KC | 7,850 | FH | 0.80 [INF] | ~2.0–2.5 | ~30 | 4–13.5 | brittle at high obliquity | UK 1900–1932; JP 1905–1937; IT 1905–1925 |
| WKC | Witkowitz KC | 7,850 | FH | 0.95 | — | 33–35 | 3.2–11 | — | A-H c.1905–1918 [INF dates] |
| MNCUS | Midvale non-cemented (US) | 7,850 | FH (very deep) | 1.05 vs ≤12 in shells; 0.70 vs 16 in [INF] | — | >80 chill | 4–14 | strong scale penalty | US 1906–10 |
| BNC | Bethlehem non-cemented | 7,850 | FH | 0.75 [INF] (minimum acceptable) | — | deep | 4–14 | brittle | US 1906–10 |
| BTC | Bethlehem thin chill | 7,850 | FH (thin) | 0.80 [INF] | — | 15 | 4–14 | weak vs hard caps | US 1922–23 |
| KNCh | Krupp/British homogeneous Ni-Cr ("KNC", Krupp Soft) | 7,850 | N | 0.95 | 2.25 | 0 | 0.5–4 (roofs to ~7) | — | DE 1894–1918; UK 1900–1925; RU 1910s |
| HT | High-tensile steel (UK HT, Krupp low-Ni) | 7,850 | N | 0.75 [INF] | — | 0 | 0.25–2 per layer (laminated) | structural | UK/DE 1900–1925 |
| DS | Ducol / D-steel / Italian ER | 7,850 | N | 0.80 [INF] | — | 0 | ≤2 | structural; poor weld (1920s) | UK 1925–45; JP 1920–45; IT 1930–43 |
| STS | STS / WWII Class B | 7,850 | N | 1.00 | — | 0 | STS 0.25–5; Class B to 19.5 | scale k≈0.01 | US 1910–45 (Class B 1910+) |
| CB1 | WWI Class B | 7,850 | N | 0.95 | — | 0 | 1–9 | — | US 1910–32 |
| CAST | Cast homogeneous armour | 7,800 | N | 0.90 | — | 0 | fittings, shields ≤6 | — | all; small fittings |
| KCnA | Krupp KC n/A | 7,850 | FH | 0.96 | — | 41 | ~3.9–15 | — | DE 1928–45 |
| WH | Wotan Hart | 7,850 | N | 1.00 at ≤3 in; ~0.90 at 12–18 in | — | 0 | 0.8–4.3 (decks/roofs to 7) | k≈0.13 [okun-cf] | DE 1925–45 |
| WW | Wotan Weich | 7,850 | N | 0.95 (×Wh) | — | 0 | ≤2 (50 mm) | blast-tough | DE 1925–45 |
| WSH | Wotan Starrheit | 7,850 | N | ~1.0 vs small arms and fragments [INF] | — | 0 | ≤2 | — | DE 1925–45 |
| CA | British Cemented Armour | 7,850 | FH (thin) | 0.93 (rises to ~1.0+ at 12–15 in [INF]) | — | 25–30 | 4–15 | favourable scaling | UK 1933–45 |
| NCA | British Non-Cemented | 7,850 | N | 1.00 (QC lax in thin WWII plate) | — | 0 | 0.5–4 (roofs/decks thicker) | — | UK 1926–45 |
| USA2 | US Class A WWII | 7,850 | FH (deep) | 1.00 (≤8 in); ~0.93–0.95 at 13–17 in [INF from okun-tests] | — | 55 | 5–17 | weaker vs big shells | US 1933–45 |
| ITKC | Italian Terni WWII KC | 7,850 | FH | 0.98 | — | ~35–45 | 4–15 | low elongation (18%) | IT 1929–43 |
| PO | Piastre Omogenee | 7,850 | N | 0.95 [INF] | — | 0 | ≤4 (decks to ~6) | 16% elongation → scale penalty | IT 1929–43 |
| FRFH | French 1930s FH | 7,850 | FH | 0.92 [INF; Okun assumed 1.00] [UNCERTAIN] | — | ~35 | 4–17 | — | FR 1932–45 |
| VH | Japanese Vickers Hardened | 7,850 | FH | 0.84 at 26 in; ~1.0 in thin experimental plates | — | ~20–35 | >11–26 | brittle centre | JP 1937–45 |
| NVNC | New Vickers Non-Cemented | 7,850 | N | 0.90 [INF] | — | 0 | 0.5–12 | — | JP 1926–45 |
| MNCJ | Molybdenum Non-Cemented | 7,850 | N | 0.92 [INF] | — | 0 | 7–9 decks | — | JP 1941–45 |
| CNC | Copper Non-Cemented (CNC1/2) | 7,850 | N | 0.92 [INF] | — | 0 | ≤3.2 | — | JP 1931–45 |
| TEAK | Teak/oak backing | 650–750 | — | ~0 vs AP; splinter catcher | — | — | 2–36 | weight-only layer | all; heavy 1859–90, thin later |
| COAL | Coal bunker | ~800 | — | 2 ft ≈ 1 in armour → Q-equivalent ≈ 0.04 per inch of coal × 0.6 (period armour) [INF] | — | — | 12–120 | depletes with fuel use | all coal-fired ships |
| CONC | Concrete filling | ~2,300 [INF] | — | ~0 | — | — | ~2 | weight-only | US Class A mounting |

**Recommended minimal implementation [INFERRED]:**
1. Store each plate as `{material_id, thickness}` and look up `Q`, `ρ`, `FH`, `face%` and `scale_k`.
2. Effective thickness: `t_eff = Q × t × (1 − scale_k·max(0, log2(t/8)))`.
3. For FH plates against uncapped shells (pre-1895 shells, or after a cap failure), add an extra multiplier: ~1.3 at normal impact for thin-faced FH, and ×(1.1–1.2) more for deep faces ([okun-tbl]). That multiplier fades with obliquity.
4. Apply the minimum face-hardening thickness: FH materials not below ~3.2–5 in, by maker (§5). Below that, the designer silently substitutes the nation's homogeneous material of the same era.
5. Weight: `t × ρ`, plus a backing layer (teak/oak/concrete) as weight-only. That layer is large before ~1890 (≈20–35% of the plate) and small after (≈3–6%).
6. Nation × year availability comes from the last column. Licensed processes reached the minor navies roughly 2–6 years after the originator [INFERRED from §2.6–2.7 dates].

**Main open items [UNCERTAIN]:**
- The size of the WWII-over-1900 KC gain: 20% (Okun) vs 36–40% (Naval Gazing and the forum).
- French and Russian quality factors.
- A British post-Jutland armour change.
- Škoda and Vítkovice interwar data.
- Exact Q for the Japanese homogeneous types.
- Verbatim confirmation of Okun's tensile figures from the original table. The 2001 navweaps page was unreachable, and the copy I used was a fetcher paraphrase.
