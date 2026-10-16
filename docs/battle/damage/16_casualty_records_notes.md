# 16 — Casualty records: the collectors' notes

*Companion to `16_casualties.md` and `16_casualty_records.csv`. Three searches, one per body of reports, made for this note in October 2026 from web sources. Kept as written: sources, conflicts, and what each period's reports say. The CSV's `class` (A–G) and `group` (G1–G7) columns were added afterwards for the fit; `killed_n`, `wounded_n` and `hits_n` are the parsed numbers the fit uses (ranges at their midpoint, the group rows' chosen counts in their notes).*

---

## USN gunfire hits and casualties: notes

Companion to `usn_hits.csv`, which has 88 rows: 62 at `level=hit` (56 shell, 6 bomb) and 26 at `level=ship`. The research was done in October 2026 using web sources only: WDRs on ibiblio and history.navy.mil, DANFS, ONI Combat Narratives, Wikipedia and ship association sites.

### 1. What the sources actually contain
- **The BuShips War Damage Reports almost never attribute casualties to individual hits.** Reports were fetched and checked for South Dakota (WDR 57), San Francisco, Boise, Sterett, Salt Lake City (Komandorski), the Destroyer report (WDR 51, scanned, OCR-garbled) and the Summary of War Damage (1941-42). They give good per-hit data on:
  - location
  - estimated calibre
  - burst mode
  - fires and flooding

  Personnel appear only qualitatively, or not at all. The Summary of War Damage has no casualty column.
- Boise is the exception. Its WDR gives small per-hit numbers: 3 injured, 1 injured, and "all of the damage-control party killed".
- **The per-hit casualty numbers therefore come mostly from single-hit incidents.** In those, the ship total *is* the per-hit value: Wichita, Murphy, O'Brien, Patterson, Fletcher, Gwin, California, Leutze, Colorado (friendly 5 in), North Carolina, Texas, Brooklyn and others. Two further per-hit numbers come from narrative statements: Heermann's pilothouse hit and Duncan's bridge-wing hit.
- Treat every DANFS or Wikipedia figure as "secondary". Totals disagree between sources:
  - South Dakota: 40 killed / 180 wounded (Wikipedia), against about 38-39 / 60 elsewhere.
  - San Francisco: 86 / 85 or 77 / 105.
  - Kalinin Bay: 5 or 9 dead.
  - Pensacola: 17 or 19 killed.
  - Murphy: 25 or 26 wounded.
- The original ship action reports (enclosure casualty lists by battle station) are the way to get true per-hit splits for the big multi-hit ships: South Dakota, San Francisco, Colorado at Tinian, Pensacola. They were not reachable online in fetchable form. NARA / Fold3 scans would be the next step.

### 2. Zero-casualty hits are common
- 38 gunfire per-hit rows have a known killed count:
  - 22 of the 38 (58%) killed nobody.
  - 15 of the 38 (39%) caused no casualties at all.
- The zero-casualty hits fall into these groups:
  - Hits on armour that were defeated or burst on the plate: Boise 1, 4, 5 and 8.
  - AP shells passing through without bursting, or bursting underwater or in fuel tanks: Salt Lake City 1 and 2, Ludlow, and Spence.
  - Shorts and near-hull bursts: Salt Lake City 4 and 5, Texas 1.
  - Duds in unmanned spaces: Texas 3, Laffey, Bailey B.
- This share is probably biased low. Secondary sources mention hits mostly when people died, while WDR rows say "none stated" rather than "0". Within the two fully itemised WDR ships that have casualty notes (Boise and Salt Lake City, 13 hit entries), 9 entries (69%) had no casualties and only 2 killed anyone.
- Burst-mode mix in the itemised WDRs:
  - **South Dakota** (26 hits): about 13 hits did not detonate. These were mostly Japanese 8 in AP, which passed through light superstructure. About 10 detonated.
  - **Kalinin Bay** (14 described): about 7 passed through the thin CVE hull without bursting and about 5 burst inside. In total 15 hits produced only about 5 killed and 55 wounded, and that total includes later kamikaze damage.
  - **San Francisco** (45 hits): the reverse pattern. Almost all burst on impact, because they were 14 in bombardment HE and 5-6 in HE. This gave about 1.9 killed and 1.9 wounded per hit, and the WDR singles out fragment casualties at secondary-battery and AA stations.
  - **Implication for the model:** whether the fuze functions against thin plating is the main switch. An AP shell that passes through costs about 0 casualties, unless it crosses a manned space directly or causes secondary fragments (Boise hit 3: one man hurt by "linoleum fragments"). A dud can still wound: Brooklyn's dud wounded 5.

### 3. Casualties per hit by situation (gunfire)

| Situation | Examples (killed / wounded per hit) |
|---|---|
| Medium-calibre burst in an open, crowded AA or control station | Colorado Air Defense Station, friendly 5 in Mk35 HC: **20 / 48**. North Carolina, base of Sky II director, friendly 5 in: **3 / 44**. California, 4.7 in by the fire-control platform: 1 / 10. Texas, 24-28 cm on the bridge support: 1 / 10. |
| Burst in or at a bridge or pilothouse | O'Brien 13 / 19. Fletcher 8 / 3. Heermann pilothouse 4 / ?. Duncan bridge wings 4 / ?. Dickerson charthouse 4 / ?. San Francisco: "all officers on navigation bridge except one killed or badly wounded". |
| Gun mount with ready powder | Patterson No. 4 gun 10 / 8 (powder fire). |
| Burst inside a machinery space | Murphy after engine room 3 / 25-26. Duncan fireroom: "all men in it were killed". Gwin main deck aft (engine stopped) 3 / 7. |
| Burst below decks in berthing or other spaces | Wichita 194 mm 0 / 14. Boise Captain's cabin 0 / 3. |
| Magazine or powder-train penetration | Boise hit 6: about 100 of the 107 dead, the worst single shell hit in the set (fire and flash, not fragments). |
| HE burst on the open deck / catapult area | Salt Lake City Komandorski hit 3: 2 killed (WDR: "severe personnel casualties", fragments travelled 120 ft). |

Ship-level averages for multi-hit gunfire actions (killed / wounded per hit):
- **Superstructure / shore-battery HE:**
  - San Francisco 1.9 / 1.9
  - Colorado (Tinian, 22 hits of 150 mm) 2.0 / 9.0
  - Pensacola (6 hits) 2.8 / 19.8
  - Norman Scott (6) 3.8 / 9.5
  - Tennessee (3) 2.7 / 8.3
  - Aaron Ward (9) 1.7 / 6.3
  - Sterett (11) 2.5 / at least 1.2 (seriously wounded only)
  - Albert W. Grant (22) 1.7 / 4.7
- **AP-dominated or mostly non-bursting:**
  - Kalinin Bay (15) 0.3 / 3.7 (includes kamikaze damage)
  - Helena (5) 0.2 / ?
  - Bailey (3) 1.7 / 2
  - Denver (3 duds): casualties not reported
- Overall, a typical medium-calibre HE hit on a destroyer or cruiser superstructure costs about 2 killed and 5-10 wounded. AP pass-throughs cost about 0. A burst in a crowded open station costs 10-20 killed and 40-50 wounded.

### 4. Killed:wounded ratios and mechanisms
- **NHRC Report 91-1** (Blood 1991; generalstaff.org ADA252892) is incident-level, not per-hit, and covers 513 incidents:
  - Gunfire, 118 incidents: mean 19.0 KIA and 22.5 WIA, giving **KIA:WIA = 0.84**.
  - Bombs: 1.10. Kamikaze: 0.60. Torpedo: 2.09.
  - Gunfire by ship type (mean KIA / WIA per incident): BB 8.4 / 30.9; CA 33.2 / 48.5; CL 14.6 / 9.3; DD 18.6 / 15.0; DE 25.0 / 37.8.
- **NHRC wound types for gunfire** (1,994 wounds):
  - Penetrating wounds 59% (fragments)
  - Multiple wounds 15%
  - Burns 7%
  - Fractures 5%
  - Concussion 2%
  - Amputations 0.8%

  Fragments dominate. Burns are much rarer than for kamikaze hits.
- **Per-hit pattern:**
  - Fragment bursts in the open give **wounded far greater than killed**: North Carolina 15:1, Santa Cruz bomb 25:1, California and Texas 10:1, Wichita 14:0, Pensacola 7:1.
  - Bursts inside small enclosed spaces (bridge, pilothouse, fireroom, gun mount) and powder or magazine fires give **killed comparable to or greater than wounded**: Patterson 10:8, Fletcher 8:3, Duncan fireroom "all killed", Boise about 100 killed with few wounded, and the Houston turret bomb 48:20.
  - Destroyer per-incident means (killed slightly above wounded) reflect this: thin structure plus compact, crowded spaces.
- **Mechanisms the reports name:**
  - Fragments (overwhelmingly)
  - Powder fires from ready-service charges and 1.1 in clips: Sterett, Patterson, South Dakota hit 7, San Francisco hit 44
  - Magazine flash and fire through ventilation: Boise
  - Steam / fireroom: Duncan
  - Secondary debris such as linoleum: Boise
  - Blast alone is rarely cited as the killer.

### 5. Suggestions for fitting the game model
- Use a burst-mode branch first:
  - No burst / dud / defeated by armour: expected casualties about 0, with a small chance of 1-5 wounded from spall, debris or the dud's own impact.
  - Burst: then apply crew density times area.
- For HE bursts in open or exposed stations, target about 1:5-1:15 killed:wounded.
- For bursts in enclosed manned spaces, target about 1:1 or higher.
- Treat the powder or magazine branch as a separate fire and flash mechanism with near-total loss of the compartment group: Boise (about 100), Savannah's Fritz-X bomb (175-197) and Houston's turret 3 bomb (48).
- Calibration anchors for "one 5 in HE in a crowded AA control station": Colorado 20 killed / 48 wounded, North Carolina 3 / 44.

---

## Crew casualties per gunfire hit, 1894–1918: notes

Data file: `ww1_hits.csv` holds 90 hit-level rows (some rows cover a pair or trio of shells, see `n_hits`) and 62 ship-level rows.

### How to read the data

- **Propellant catastrophes** are tagged `[PROPELLANT CATASTROPHE]` in `notes` and `PROPELLANT FIRE` in `mechanism`. Smaller cordite or ready-use ammunition fires are tagged `[PROPELLANT-small]`. Keep both out of the fitted "ordinary hit" casualty model.
  - Catastrophes: Seydlitz aft barbette (Dogger Bank), Lion Q, Derfflinger C and D, Malaya's starboard 6-in battery, Seydlitz's aft superfiring turret (Jutland), Fuji's rear barbette (Tsushima).
  - Small fires: Kent's casemate, Tiger's 6-in ammunition passage, Barham's S2 casemate, Moltke's casemate, König's casemates, Lützow's B turret, Laurel's ready racks.
- **Blank cells are blank on purpose.** About 40% of the hit rows have a known location and effect but no casualty count, because the source gives none. I have not filled them in.
  - Many of them are still useful as "hit happened, nothing dramatic reported" rows.
  - Rows where the source says or clearly implies no casualties are recorded as `0,0` with confidence `low`/`medium` (e.g. "no damage" or "burst harmlessly").
- **"Wounded" is not consistent between sources.** Despatch and official lists often count only the seriously wounded. For the same ship:
  - Malaya: 33 vs 68
  - Tiger: 37 vs 46
  - Barham: 37 vs 46
  - Warspite: 16 vs 32
  - Asahi at Tsushima: 5 serious + 19 light

  "Killed" sometimes includes men who died of wounds and sometimes doesn't. If you calibrate wounded numbers, use the larger figure, or model light and serious wounds separately.
- **Campbell hit counts.** The Jutland per-ship heavy-hit counts come from Campbell's table as reproduced on Wikipedia, "Damage to major ships at the Battle of Jutland". I could not get the full text of Campbell's *Jutland: An Analysis of the Fighting*: archive.org only has a lending copy with no OCR text. Most per-hit casualty figures here therefore come from:
  - Japanese official history extracts (USNI 1914)
  - Kostenko's memoir (militera.lib.ru)
  - Official reports (Dreadnought Project; Jutland Crew Lists Project)
  - USNI 1925 "Lessons of Jutland"
  - Forum and auction-catalogue quotations of Campbell
  - Wikipedia ship articles that cite Campbell

  Campbell's book is the obvious next source for filling the blanks.

### Ship-level averages (heavy hits, both killed and wounded counted)

| Ship / battle | Heavy hits | Killed | Wounded | Killed/hit | Wounded/hit | Notes |
|---|---|---|---|---|---|---|
| Princess Royal, Jutland | 8–9 | 22 | 78 | ~2.6 | ~9 | German 30.5 cm APC; 11k/31w from one 2-shell salvo |
| Tiger, Jutland | 15 (+3 × 15 cm) | 24 | 37–46 | ~1.6 | ~2.5–3 | 28 cm APC; ~15 killed from 2 identified hits |
| Warspite, Jutland | 15 | 14 | 16–32 | ~0.9 | 1–2 | many hits on belt or aft cabins |
| Barham, Jutland | 6 | 26 | 37–46 | ~4.3 | ~6–8 | one hit (medical store, S2 casemate) likely produced most |
| Malaya, Jutland | 7–8 | 63 | 33–68 | ~8.5 | — | ~104 killed or wounded in one battery cordite fire. **Ex-fire: perhaps ~0.5–1 per hit** |
| Lion, Jutland | 13 | 99 | 44 | 7.6 | 3.4 | **Ex-Q turret (~60+): ~3 killed/hit** |
| New Zealand, Jutland | 1 | 0 | 0 | 0 | 0 | |
| Colossus, Jutland | 2 | 0 | 7 | 0 | 3.5 | |
| Lützow, Jutland | 24 | 115 | 50 | 4.8 | 2.1 | sank; includes men trapped forward |
| Seydlitz, Jutland | 21+2 | 98 | 55 | ~4.3 | 2.4 | includes aft-turret propellant fire |
| Derfflinger, Jutland | 17 (+9 sec) | 157 | 26 | 9.2 | 1.5 | **C+D turrets ≈ most of the dead.** Ex-C/D: roughly ≤1 killed per hit |
| König, Jutland | 10 | 45 | 27 | 4.5 | 2.7 | |
| Grosser Kurfürst, Jutland | 8 | 15 | 10 | 1.9 | 1.3 | |
| Markgraf, Jutland | 5 | 11 | 13 | 2.2 | 2.6 | one casemate hit killed a gun crew |
| Moltke, Jutland | 4 | 16 | 20 | 4 | 5 | mostly one casemate hit with ammunition fire |
| Von der Tann, Jutland | 4 | 11 | 35 | 2.8 | 8.8 | |
| Kaiser, Jutland | 2 | 0 | 1 | 0 | 0.5 | |
| Schleswig-Holstein, Jutland | 1 | 3 | 8 | 3 | 8 | effectively a single-hit row |
| Tiger, Dogger Bank | 6 | 10 | 11 | 1.7 | 1.8 | 7k/9w from one hit |
| Lion, Dogger Bank | 14–16 (+1 × 21 cm) | 0–1 | 17–20 | ~0 | ~1.2 | many belt and waterline hits |
| Derfflinger, Dogger Bank | 1–3 | 0 | 0 | 0 | 0 | |
| Mikasa, Yellow Sea | 20 | 34* | 94* | 1.7 | 4.7 | *sum of the official per-hit list (ship total 125 killed and wounded) |
| Oryol, Tsushima | ~55 shell hits + 21 small | 43 | ~80 | ~0.8 | ~1.5 | Campbell's count; Kostenko claims far more |
| Tsesarevich, Yellow Sea | 15 | 12 | 47 | 0.8 | 3.1 | |
| Retvizan, Yellow Sea | 18 | 6 | 42 | 0.3 | 2.3 | |
| Kent, Falklands | 38 (10.5 cm) | 6 | 8 | 0.16 | 0.2 | 10 of 14 from one casemate cordite fire |
| Cornwall, Falklands | 18 (10.5 cm) | 0 | 0 | 0 | 0 | |
| Invincible + Inflexible, Falklands | 25 | 1 | 4–5 | 0.04 | 0.2 | 21 cm and 15 cm against large, well-subdivided ships |
| Arethusa, Heligoland | ~35 (10.5 cm) | 10 | 16 | 0.3 | 0.5 | |
| Chester, Jutland | 17 (15 cm) | 35 | 42 | 2.1 | 2.5 | open-backed shields |
| Southampton, Jutland | 18 | 35 | 41 | 1.9 | 2.3 | point-blank night action |

**Rough central tendencies to use in the game:**

- **Capital ships, heavy AP hits, ordinary hits (propellant fires removed):** about 1–3 killed and 2–5 wounded per hit. The distribution is very skewed:
  - Most hits on belts, barbettes, turret faces, funnels and underwater kill no one or one or two men.
  - A minority that burst inside a manned space kill 7–18 (Mikasa YS-MIK-4: 18k/55w; Tiger Dogger Bank: 7k/9w; Tiger's Jutland passage hit: 12k; Princess Royal's salvo: 11k/31w from 2 shells).
- **Propellant catastrophe (ship survives):** 60–165 killed per event, with few survivors among those present. Treat it as a separate mechanism.
  - Seydlitz at Dogger Bank: 159–165 killed
  - Lion Q: ~60+
  - Derfflinger C: 73 of 78
  - Malaya's battery: 104 killed or wounded out of 121 present
- **Light and medium HE (10.5–15 cm) against cruisers and destroyers:**
  - Against armoured or protected cruisers with casemates: about 0–0.3 killed per hit (Cornwall 0/18; Kent 6/38; Arethusa 10/35).
  - Against light cruisers with open shielded guns: about 2 killed per hit (Chester; Southampton at point-blank).
  - Destroyer hits in the engine room, boiler room or on the bridge: 2–9 killed per hit (Laurel engine room 4; Asagiri boiler room 9; Meteor 2–3; Liberty's bridge 2).

### Killed : wounded ratios by mechanism

| Mechanism | Example rows | Killed:wounded |
|---|---|---|
| Propellant fire or flash in a turret, battery or working chamber | Derfflinger C 73:5; Seydlitz DB 159:33 (ship total); Lion ship 99:44 | **≈ 2:1 to 15:1**; nearly everyone present dies, survivors badly burned, delayed burn deaths common (Malaya) |
| Large shell bursting inside an enclosed manned space (fragments + blast) | Mikasa YS-MIK-4 18:55; Tiger DB 7:9; PR salvo 11:31; Matsushima 28:68; Nisshin YS-NIS-2 11:2 | **≈ 1:3 to 1:1**; close blast ("bodies blown to pieces") pushes toward killed |
| Burst on deck or in the open near exposed men | Mikasa forecastle 7:16; Mikasa mainmast 8:5; Liberty bridge 2:? | ≈ 1:2 to 1.5:1 |
| Burst on armour outside, with spall or splinters through slits or ports | Tiger Q roof 3:0 (2 + 1 DOW); Derfflinger CT 0:several; König CT 0:1; Oryol CT and turrets 1-2 per event; Markgraf casemate: gun crew killed, 2 wounded | ≈ 1:1 to 1:3; small numbers per hit |
| Small-fragment HE ("Shimose"), many hits, ship-wide | Oryol 43:80; Mikasa YS whole ship 34:94 | ≈ 1:2 to 1:3 |
| Ship-wide, ordinary splinter damage (no propellant fire) | Princess Royal 22:78; Tiger 24:46; Colossus 0:7; Southampton 35:41; Chester 35:42 | **≈ 1:1 to 1:3.5** |
| Concussion / pressure only | Oryol 6-in turret: 1 killed with no external wounds; Lion Q dead mostly unburned; Mississippi turret (1924): ~45 killed by pressure and gas | killed only |

Rule of thumb: ordinary splinter hits give about 1 killed per 2–3 wounded. Propellant fires flip that to many killed per wounded. A large shell bursting inside a crowded space sits in between, at about 1:3 to 1:1.

### What period reports and memoirs say about splinters vs blast

- **USNI Proceedings 1925, "Lessons of Jutland affecting design – turret armor":**
  - On Lion's Q turret, most of those killed by the cordite fire were not burned, and body parts covered by non-inflammable material were not discoloured. It attributes the near-instant deaths chiefly to *pressure*.
  - It reports the same for Mississippi's 1924 turret fire: about 45 killed, some by pressure and gas alone.
  - On Tiger, a burst on the turret roof that did *not* penetrate still killed men with roof-armour fragments, so turret roofs spall.
- **Kostenko (Oryol, Tsushima), chapters 38 and 44:**
  - The main losses were *behind armour*, in turrets, conning tower, casemates and battery. Fragments came in through ports, embrasures and sighting slits from shells bursting on the hull or on the water.
  - Japanese HE fragments were tiny ("coarse sand") and of low penetration: wounds were shallow and fragments often stuck in the skin.
  - Fires from hammocks, paint and cartridges, and fire-fighting itself, added casualties: two fire-party men killed and an officer burned.
  - Men were knocked unconscious or killed by concussion alone.
  - Shimose fumes nearly suffocated the wounded in the operating room until the ventilation was shut.
  - The conning tower went from 14 men to 3 by nightfall.
- **Princess Royal's damage report (Jutland):**
  - Burns to the face and arms were common and serious among 4-in gun crews and salvage parties.
  - Men in the transmitting station and lower conning tower were "partially gassed" by shell fumes drawn down ventilation.
  - Report quoted: "a constant stream of shell fragments."
- **Chester:** Many of the wounded lost legs because the open-backed gun shields did not reach the deck. Exposed light-cruiser gun crews were hit by splinters at foot and leg level.
- **Malaya battery fire:** The worst burns were to faces and hands. Some men who looked lightly burned died quickly, and one died days later. Survivors of No. 1 gun were rescued through a forward door; other doors jammed and crews broke through them with hoses.
- **Japanese official history (Yellow Sea):** The worst single hits killed a mix of officers and men in concentrated groups: 18 killed and 55 wounded from one shell on Mikasa, 27 killed or wounded from one shell on Yakumo's main deck. In other words, a large-calibre burst in a manned compartment is the main killer outside propellant fires.

### How casualties per hit depended on shell type (HE/common vs AP)

- **AP or APC against heavily armoured ships (Jutland, Dogger Bank):**
  - A large share of hits fall on belt, barbette, turret faces or underwater, or are duds or low-order breaks. These produce zero or near-zero casualties.
  - Examples: Markgraf's two 15-in duds and Agincourt's shattered 12-in; Barham's waterline 11-in; Princess Royal's B-turret armour hit; Derfflinger's Dogger Bank belt hits; Lion's 16 Dogger Bank hits for 0–1 killed.
  - When AP does get inside a manned space or a propellant train, it is the most lethal hit type of the era, because of the propellant fires.
  - German APC got inside British turrets and passages more often than British APC got inside German ones. British APC tended to break up on armour, as Grosser Kurfürst's and Markgraf's belt hits show.
- **Japanese HE / common with Shimose (Tsushima):**
  - Produced many hits, many fires and very many small fragments.
  - Per-hit lethality was moderate (Oryol about 0.8 killed per hit), but whole crews in exposed positions and behind openings were eroded.
  - Wounds were mostly light or shallow, which pushes the killed:wounded ratio toward the wounded side (about 1:2).
  - HE rarely produced the catastrophic in-turret fires that AP did, apart from 6-in turret ammunition fires on Oryol.
- **Russian AP / common with light filler (Yellow Sea and Tsushima):**
  - Fewer fragments and poorer bursting, according to Kostenko.
  - Yet single hits on Mikasa's unarmoured upper works still killed 7–18 men each, because the shells burst among crowded exposed personnel.
  - Ship-wide, the 1904 Yellow Sea gave about 1.7 killed per hit on Mikasa, *higher* than Japanese HE achieved against Oryol a year later. The difference reflects where the hits landed and where the men were, more than the filler.
- **Black-powder common shell (Yalu 1894):** One 12-in burst on Matsushima's crowded open battery deck killed 28 and wounded 68, the deadliest non-catastrophic single hit in the dataset. The causes were an unarmoured battery, ready ammunition and dense crews.
- **Small-calibre HE (10.5 cm) against protected or armoured cruisers:** It produced very few casualties: 0 killed from Cornwall's 18 hits and 4 lightly wounded from Glasgow's 5 at Coronel. The exceptions were when it ignited cordite (Kent's casemate) or hit a crowded open gun position.

### Open issues and conflicts in the sources

- **Seydlitz at Dogger Bank:** 159 (en.wiki) vs 165 (de.wiki) killed.
- **Derfflinger C turret:** USNI 1925 says 5 of 78 escaped. de.wiki says only one man of both C and D crews survived.
- **Lion:** 14 vs 13 heavy hits at Jutland. At Dogger Bank, 14 vs 16 hits, and 0 vs 1 killed.
- **Princess Royal:** 8 hits (Campbell's table on Wikipedia) vs 9 (ship article).
- **Malaya:** 7 vs 8 hits; 33 (despatch) vs 68 wounded.
- **Oryol:** Campbell's ~55 shell hits vs Kostenko's 42 × 12-in + ~100 × 8/6-in.
- **Kasagi vs Kasuga (Yellow Sea):** the same "3 hits, 11 wounded" figure is attributed to both.
- **Shikishima (Yellow Sea):** hit once with no casualties (official) vs not hit (Wikipedia).
- **Chester:** 29k/49w (Wikipedia) vs 35k/42w (Jellicoe's despatch table).
- **Hits I could not attribute casualties to:** Barham's medical-store hit, the Malaya battery killed/wounded split, Derfflinger D, Seydlitz's Jutland turret and bridge hits, Moltke's casemate. Campbell's book is the likely source for most of these.
- **Not covered:** Otranto Strait 1917. Sources found give no per-ship casualty numbers for Novara, Saida, Helgoland or Dartmouth, so there are no rows for it.

---

## WWII gunfire hits and crew casualties: notes (non-US War Damage Reports)

Companion to `ww2_hits.csv`. That file has 88 hit-level rows (82 direct hits and 6 near misses, marked `n_hits=0`) and 56 ship-level rows. Every number comes from a cited web source. Blank or "n not stated" means the source gave no figure; I never filled one in by estimate. The confidence column means: **high** = primary or despatch-derived, or several sources agree. **med** = one secondary source, mostly Wikipedia or combinedfleet. **low** = sources conflict, or the attribution is unclear.

### Source situation (read first)
- **There are few per-hit casualty figures.** Per-hit splits exist only where a despatch, damage report or eyewitness tied deaths to one shell: Harwood's River Plate despatch, the Exeter eyewitness, Prince of Wales, Gneisenau, Berwick, Cleopatra, Saumarez, Norfolk, Nachi, Glasgow, De Ruyter and Chokai. Most other battles, including the Japanese TROMs, Narvik, Barents Sea and Mers-el-Kébir, give only ship totals.
- **I could not reach the ADM 267 / DNC damage reports directly.** The kbismarck.org forum is blocked to fetch, and the hmshood.org.uk Prince of Wales report extract has no casualty data. The best next step is the ADM 267 file for Prince of Wales or the Exeter River Plate report (via Grove or the National Archives). Those should give the compass-platform casualty count, which I could not find stated anywhere.
- **Sources conflict, and the conflicts are marked in `notes`:**
  - Exeter River Plate totals: 61 killed/23 wounded, 63/23, or 55+2 missing/82.
  - Ajax River Plate: the despatch gives 4 killed/6 wounded for the X-turret hit, but ship totals are 7/5.
  - Exeter Java Sea boiler-room hit: 14 or 40 killed.
  - Nachi: 6in versus 5in shells; 13/26 versus 14/27.
  - Furutaka: 258 killed, versus 33 killed plus 110 missing.
  - Graf Spee hit count: 20 versus 60–70.

### Killed : wounded ratios
These use ship or hit rows where both figures are stated.

| Case | K | W | K:W |
|---|---|---|---|
| Exeter, River Plate (7 x 28 cm) | 61 | 23 | 2.7 |
| Prince of Wales (7 hits) | 13 | 9 | 1.4 |
| Norfolk, North Cape (2 x 28 cm) | 7 | 5 | 1.4 |
| Ajax, River Plate (1 hit) | 7 | 5 | 1.4 |
| Achilles DCT near miss | 4 | 2 | 2.0 |
| Cleopatra ADP/bridge (1 x 15 cm) | 15 | 5 | 3.0 |
| Saumarez (DCT dud + others) | 11 | 11 | 1.0 |
| Berwick Y turret (1 x 20.3 cm) | 7 | 9 | 0.8 |
| Onslow (3 x 20.3 cm) | 17 | 23 | 0.74 |
| Ajax, Cape Passero (7 small hits) | 13 | 22 | 0.6 |
| Graf Spee (~20–70 hits, mostly 6 in) | 36 | 60 | 0.6 |
| Nachi (5 hits) | 14 | 27 | 0.5 |
| Nachi signal-bridge hit | 11 | 21 | 0.5 |
| Cossack, Narvik (7 x 12.7 cm) | 9 | 21 | 0.43 |
| Glasgow pom-pom hit | 2 | 6 | 0.33 |
| Asagumo (4.7 in) | 5 | 16 | 0.31 |
| De Ruyter motor room (1 x 20.3 cm) | 1 | 6 | 0.17 |
| Michishio | 13 | 83 | 0.16 |
| Bismarck, Denmark Strait (steam) | 0 | 5 | 0 |

- **Median is about 0.7 killed per wounded.** The range runs from 0.16 to 3.
- **Ratio rises above 1 in two situations.** The first is when heavy shells (28–38 cm) hit occupied spaces such as turrets, control positions and flats. The second is when a fire or flash follows. Exeter's 2.7 reflects 28 cm hits on turrets and on the crowded bridge, plus a burst inside the CPO flat that killed "nearly all" there, mostly by blast. Some of Exeter's wounded also died later, which pushes the ratio up.
- **Ratio falls well below 1 for medium and light calibres on open, dispersed crews.** Examples are Graf Spee's 6-in hits, destroyer 4.7–5 in hits, and splinter-dominated events.
- **Rule of thumb for the model (my inference, not from a source):** K:W of about 0.4–0.7 for ≤6 in hits on exposed crews, and about 1–3 for ≥8 in hits that burst inside, or pass through, crowded small spaces.

### Fragment vs blast vs other mechanisms
- **Fragments dominate among the wounded.** Cussen (Exeter's senior surgeon) found that 63% of the wounded had metal-fragment lacerations (9 with open fractures) and 18 had burns. He records "absent were any blast injuries". Blast still killed: the eyewitness says men in the CPO flat were killed "mostly by blast". So blast tends to produce dead, and fragments produce both dead and wounded. This favours modelling blast as a lethal radius close to the burst, with a much wider fragment zone that mostly wounds.
- **Shells that did not burst still killed men in small, crowded control spaces.** Four such cases:
  - Gneisenau director tower, 15 in: 1 officer and 5 men killed.
  - Saumarez DCT, 28 cm: 11 killed.
  - Prince of Wales compass platform, 38 cm: all but the Captain and Chief Yeoman killed. Most of the ship's 13 dead were probably here (my inference). Wounds came from cap and structure fragments.
  - Aoba flag bridge: shells "killed many men" by "the force of their passage".

  In larger or emptier spaces, a shell that did not burst caused about zero casualties. Examples: Exeter sick bay, Prince of Wales underwater dud, Bismarck bow and boat hits, Duke of York masts, De Ruyter's second hit, Dunkerque stern. **Model implication:** a pass-through or dud in a space with crew density above roughly 1 man per m² (bridges, directors, DCTs) should still cause casualties from impact and spall.
- **Fire, flash and asphyxiation:**
  - Dunkerque turret II: a dud ignited propellant and "asphyxiated all the men in that half".
  - Dunkerque hit 3: smoke flooded machinery spaces and "only a dozen" escaped the room.
  - Giulio Cesare: the funnel burst drew fumes into the boiler rooms. Four boilers went off line, with only about 2 killed.
  - Berwick Y turret: the fire took an hour to subdue.
- **Steam:** Bismarck's 5 wounded were all scalded by a severed steam line. Boiler-room bursts (Exeter at Java Sea, Glasgow, Kingston, Scharnhorst No.1 boiler room) kill the watch.
- **Near misses can be as lethal as hits.** Achilles had 4 killed in the DCT from an 11 in water burst. Exeter's torpedo-tube crew were killed by splinters. Havock had 8 killed. A model should keep near-miss splinter casualties against unarmoured topsides.

### Crowding in exposed positions
The highest per-hit death counts in the dataset are almost all hits on crowded, lightly protected command or control positions:
- Achates: 40 killed by one hit, including the CO.
- Cleopatra ADP/bridge: 15 killed.
- Kingston amidships: 15 killed.
- Saumarez DCT: 11 killed.
- Nachi signal bridge: 11 killed, 21 wounded.
- Exeter bridge and B turret: more than 7 killed.
- Chokai chart room: 36 killed or wounded.
- Tenryu: 23 killed from one probable hit.
- Prince of Wales compass platform.
- Hardy's bridge (Warburton-Lee).
- Jintsu's bridge (admiral, captain and XO).
- Baleno and Luca Tarigo bridges, where the COs were killed.

Casualties scale with the number of men exposed, not with shell size alone. A 15 cm hit on Cleopatra's ADP killed more than a 38 cm hit on Bismarck's boiler-room bulkhead. Losing command (CO, admiral or gunnery officer) is a recurring second-order effect: Exeter, Hardy, Onslow (Sherbrooke), Aoba (Goto), Hiei (Suzuki), Jintsu and Baleno.

### Hits with no casualties
- **Of the 82 direct-hit rows, 46 have a stated or derivable killed figure.** Of those, 24 are zero or "none stated" and about 19 have deaths. Hits with no casualties come mainly from:
  - duds and shells that passed through without bursting outside crowded spaces;
  - hits on masts and funnels;
  - underwater hits that stopped in the torpedo-defence system;
  - armour that defeated the shell (Jean Bart barbette, Scharnhorst turret vs 4.7 in).
- **"None stated" is not a confirmed zero.** Treat roughly 30–50% of large-calibre hits on major warships as producing no casualties. This range is my judgement from the dataset, not a measured figure.
- **Ship-level examples of many hits with few deaths:**
  - Graf Spee: about 20–70 hits for 36 killed, roughly 0.5–1.8 killed per hit. "Most of the hits... caused only minor structural and superficial damage."
  - Provence: "several" 15 in hits, 3 killed.
  - Bismarck, Denmark Strait: 3 hits, 0 killed.
  - Richelieu, Dakar: a hit "causing no casualties".
  - Jean Bart: about 7 x 16 in hits plus bombs for 22 killed and 22 wounded.
  - Berwick, December 1940: several 8 in shells "mostly passed right through", 4 killed.

### Killed per hit (ship level, rough)
- **Heavy ships hit by heavy shells, not lost:** about 0–6 killed per hit. Examples are Prince of Wales (13 over 7 hits), Norfolk (3.5), Exeter (8.7), Gneisenau (2–3) and Bismarck (0).
- **Destroyers:** about 2–6 killed per hit (Cossack 1.3, Hotspur 2.6, Onslow 5.7). A single unlucky hit can kill 40 (Achates).
- **Ships that sank:** losses are driven by the sinking, not the hits (Glorious, Ardent, Acasta, Bretagne, Scharnhorst, Furutaka, Akatsuki). Keep sinking deaths separate from hit deaths in the model.

### Gaps and next steps
- **Not obtained:**
  - Graf Spee per-hit list (the German damage report exists; Grove's River Plate study).
  - Hipper's casualties at Barents Sea.
  - Dunkerque's 3 July deaths, separate from 6 July.
  - Kirishima's casualties.
  - Prince of Wales compass-platform count (ADM 267/111 or the Leach report).
  - Per-hit split for Onslow.
- **Calabria, Giulio Cesare:** casualty figures vary; I used Wikipedia's "two killed, several wounded".
