# G. Warship battle load and auxiliaries by consumer group, 1895-1945: a gap-filling pass
Status: draft    Updated: 2026-11-04    Request: [REQUESTS.md, 2026-11-04](../../REQUESTS.md); an appendix of [electric-plant.md](../electric-plant.md)

This pass adds to the earlier notes (a-recorded-plants.md and d-hull-auxiliaries.md) and does not repeat them. Already covered there: the c.1911 USN motor list, Kearsarge's 350 kW against 661 kW total, the 24/30-inch searchlights, carbon lamps, the 35 hp circulating-pump motor, and Massachusetts steering. The new material falls into three groups:
- Two period demand tables by consumer group, for Kearsarge 1900 and Kaiser Friedrich III. These are the only warship breakdowns in kW found so far.
- Unit motor ratings from 1902 and 1907 USN papers.
- The 60-inch and 36-inch searchlight draw.
- WWII destroyer and destroyer-escort auxiliary ratings.
- The modern USN load-analysis method.

## Q1. Per-consumer kW: motor ratings by group (per unit), 1895-1945

### Takeaway
USN papers of 1902 and 1907 give ratings for most consumer groups:
- 12-inch turret ammunition hoist: 30 hp, rising to 55 hp by 1907.
- 12-inch turret training: 2 × 35 hp per turret.
- Rammer: 10 hp. Elevating motor: 8 hp.
- Boat crane: 50 hp hoisting and 25-30 hp slewing.
- Deck winch: 25 hp.
- Blowers and exhausters: 4-15 hp.
- Torpedo air compressor: 25-80 hp.
- Wireless motor-generator: 2-3 kW.

A 60-inch searchlight drew 25 kW from 125 V mains for a 12 kW arc. A 1949 USN manual gives the WWII 36-inch high-intensity arc at 150-190 A.

WWII destroyer examples:
- Sumner-class DD: electro-hydraulic steering motor 35/8.75 hp; windlass 30/7.5 hp.
- Cannon-class DE: fire/bilge pumps 20 hp each.

### Cited Findings
**USN, 1902** (Lt Harry George, "Electricity in the Navy", AIEE meeting 28 May 1902). [Zenodo copy](https://zenodo.org/records/2210124/files/article.pdf)
- Motors range from 1/12 hp to 50 hp.
- Fans: 1/12 hp (desk), 1/6 hp (bracket).
- Blowers and exhausters: 4-15 hp.
- Turret ammunition hoists: 12-inch 30 hp, 10-inch about 16 hp, 8-inch 8 hp.
- Chain ammunition hoists about 3 hp; whip hoists up to 6 hp.
- Gun elevating about 5 hp. Rammers 7 hp (12-inch) and 5 hp (10-inch).
- Turret turning: "two 35 h.p. motors per 12-inch turret".
- Boat cranes about 50 hp hoisting and 25 hp revolving.
- Torpedo air compressors about 25 hp.

**USN, 1907** ("Electrical Installations of the United States Navy (continued)", USNI Proceedings April 1907). [USNI 1907](https://www.usni.org/magazines/proceedings/1907/april/electrical-installations-united-states-navy-continued)
- 12/13-inch turret hoist: 20 hp at 160 V in the Kearsarge class. The "latest design has an output of 55 horsepower at 125 volts", the size driven by the turret's automatic doors.
- 8-inch hoist 15 hp. Whip hoist 3 hp. Chain hoists 3 hp. Ammunition conveyors (Connecticut) 4 hp.
- Gun elevating 8 hp (12-inch) and 5 hp (8-inch). Rammers 10 hp (12-inch) and 3.5 hp (8-inch).
- Boat crane hoisting 50 hp and rotating 30 hp, for a 33,000 lb working load. Upper-deck winch 25 hp (2,200 lb pull at 300 ft/min).
- Fresh-water pumps 2 × 2 hp. Flushing pumps 2 × 3 hp.
- Torpedo air compressor 80 hp at 125 V.
- Laundry machinery 8 hp. Galley: dough mixer 3 hp, dishwasher ¾-1 hp, potato peeler 1 hp.
- Workshop: 28-inch gap lathe 7.5 hp, 16-inch shaper 5 hp, milling machine 2 hp, emery grinder 2 hp, drill press 1 hp, 14-inch lathe 1 hp.
- Power doors and hatches (Connecticut): 1 hp each.
- Portable drying fan ¼ hp (80 V, 3.5 A). The article's blower table is garbled in the OCR.
- Wireless motor-generator: "2 to 3 kW".
- Call-bell dynamotor: ¼ hp, 2.5-4 A at 80 V in.

**Big searchlight draw, USN 1907.** For a 60-inch lamp, "125 volts X 200 amperes = 25 k.w., of which the lamp only requires 60 X 200 = 12 k.w.". The other 13 kW was burned in the rheostat (the reason for a balancer). — [USNI 1907](https://www.usni.org/magazines/proceedings/1907/april/electrical-installations-united-states-navy-continued)

**Searchlight arc currents, USN 1902.** Visibility ranges are on a 10 × 20 ft target. — [George 1902](https://zenodo.org/records/2210124/files/article.pdf)

| Light | Arc current | Arc voltage | Visibility |
|---|---|---|---|
| 13-inch | 18-20 A | 45-48 V | — |
| 18-inch | 30-35 A | 47-50 V | 4,000 yd |
| 24-inch | 40-50 A | 48-52 V | 5,000 yd |
| 30-inch | 70-80 A | 49-53 V | 6,000 yd |

**Searchlights, WWII USN** (Electrician's Mate 3, NAVPERS 10548, ch. 15, GPO 1949). — [RF Cafe transcription](https://www.rfcafe.com/references/electrical/electricians-mate-3-navpers-10548/navy-training-electricians-mate-3-NAVPERS-10548-chapter-15.htm)
- 36-inch and 24-inch lights are high-intensity carbon arcs. The 12-inch is "a 1,000-watt lamp" (incandescent).
- 36-inch carbons last about 90 min at 150 A and about 45 min at 190 A. A 24-inch pair lasts about 105 min.
- The lamp's ventilation motor is about 1/12 hp, across the arc circuit.
- No arc or supply voltage is given.

**WWII destroyer, Allen M. Sumner (DD-692).** — [dd-692.com machinery](https://dd-692.com/machinery.htm)
- Electro-hydraulic steering gear: GE motor 35/8.75 hp, pump 1,600 psi.
- Anchor windlass: electric, 30/7.5 hp.
- Air-conditioning compressor 7.5 hp. Refrigeration compressor 4/2 hp.
- Ship-service turbo-generator 400 kW (500 kVA) at 450 V AC, 3-phase 60 Hz.
- Emergency diesel generator 100 kW (GM 3-268A, 145 bhp).

**WWII destroyer escort, Slater (Cannon class, diesel-electric).** — [USS Slater, Motor Room](https://ussslater.org/motor-room)
- Fire and bilge pumps: 2 × 20 hp centrifugals, 200 gpm at 100 psi, 440 V AC.
- HP air compressor 22.5 hp. LP air compressor 20 hp.
- Motor-generator sets: 30 hp motors driving 2 × 8.5 kW and 1 × 2 kW 120 V DC generators.
- Main switchboard generators 300 kW at 450 V AC. Emergency diesel generator 100 kW.
- The Doerry-Amy slides give Slater's sets as "200/300 kW 450 V AC generators, 40 kW 120 V DC generators, and 8.5 kW motor-generator sets". — [Amy & Doerry, "Electric Ship: Historical Perspective", ESTS 2019 tutorial](https://www.doerry.org/norbert/papers/20190524%20ests%20tutorial%20-%20doerry-amy%20-%20historical%20perspective%20-%20distro%20A.pdf)

**Iowa:** emergency diesel generator rated 250 kW AC plus 5.5 kW DC. — [Naval Gazing, Iowa Auxiliary Machinery](https://navalgazing.net/Pictures-Iowa-Auxiliary-Machinery)

### Inferences
- [EST] kW conversion: motor input ≈ hp × 0.746 / efficiency of about 0.8-0.85, so about 0.9 kW per hp at full load for these DC motors. Per unit, drawn:

  | Consumer | kW drawn |
  |---|---|
  | 12-inch hoist, 1907 | ≈ 50 |
  | 12-inch turret training | 2 × 31 |
  | Boat crane hoist | ≈ 45 |
  | Deck winch | ≈ 22 |
  | Blower | 4-14 |
  | Sumner steering, fast speed | ≈ 31 (≈ 8 slow) |
  | Sumner windlass | ≈ 27 |
  | DE fire/bilge pump | ≈ 18 |

- [EST] Searchlight plant draw = arc current × mains voltage, because a series rheostat burns the difference:

  | Light | Current | Mains | Plant draw |
  |---|---|---|---|
  | 30-inch | 70-80 A | 80 V | 5.6-6.4 kW |
  | 30-inch | 70-80 A | 125 V | 8.8-10 kW |
  | 60-inch | 200 A | 125 V | 25 kW (cited) |
  | 36-inch HI (WWII) | 150-190 A | 120 V DC | 18-23 kW |

  The 36-inch figure assumes the light is fed from the ship's 120 V DC system. That is higher than the 10-15 kW the earlier note (d) extrapolated. [UNCERTAIN] Whether WWII USN 36-inch lights ran on 120 V DC mains, a motor-generator, or a rectifier is not established. If a dedicated supply matched the arc voltage, draw falls toward the arc's own power (of the order of 150 A × ~75 V ≈ 11 kW; the arc voltage is my assumption, not sourced).
- [EST] By 1907 a 12-inch turret's electrical machinery was about 2 × 35 (training) + 2 × 55 (hoists) + 2 × 8 (elevating) + 2 × 10 (rammers) ≈ 215 hp, about 190 kW connected per twin turret. That is consistent with the earlier note's c.1911 list, in which the turrets are about 40% of connected load.

### Gaps
- No ratings were found for:
  - forced-draught blower motors after 1911 (the earlier note's 45 hp × 12 stands);
  - capstans of capital ships;
  - USN WWII ventilation fan ratings and counts;
  - galley ranges in kW;
  - gyro-compass motor-generators (the Arma/Sperry manuals on maritime.org were not reached).

## Q2. Consumer-group breakdowns and the battle load against installed plant

### Takeaway
Two period demand tables by consumer group exist in George (1902).

**Kearsarge (1900): 655 kW of demand on 7 × 50 kW = 350 kW of plant.**
- Boat cranes and winches 33%.
- Turret training, elevating and rammers 26%.
- Ammunition hoists 18%.
- Ventilation 15%.
- Lighting 7%.
- Searchlights 1% (a figure that looks wrong).

**German Kaiser Friedrich III: 221 kW of loads on a 324 kW plant.**
- Ammunition hoists 35%.
- Searchlights 25%.
- Lighting 17%.
- Ventilation 13%.
- Gun training 4%.
- Coal winches 3%.
- Refrigeration 2%.

The German ship's plant exceeded her listed loads. The American's plant was about half of them.

### Cited Findings
**Indiana (1895), 10,200 t.** Three 24 kW, 80 V generators supplied:
- 500 lamps;
- four 60 cm searchlights;
- 2 × 2 hp dynamo-room ventilating motors and four portable blowers;
- winches and ammunition hoists.

Motors came to about 36 hp in all. Total output was 67 kW, "about 7 W per ton". — [George 1902](https://zenodo.org/records/2210124/files/article.pdf)

**Kearsarge (1900), 11,525 t.** Seven 50 kW, 80 V generators. The stated total is 655,240 W, "about 57 W/ton"; the items sum to 655,068 W. — [George 1902](https://zenodo.org/records/2210124/files/article.pdf)

| Load | W |
|---|---|
| 700 incandescent lamps | 48,000 |
| 4 × 30-inch searchlights | 6,000 |
| Ventilating motors and portable fans | 98,000 |
| Ammunition hoists and whips | 115,138 |
| Training, elevating and rammer motors | 171,580 |
| Boat cranes and winches | 216,350 |

**Foreign plants, from George 1902.** — [George 1902](https://zenodo.org/records/2210124/files/article.pdf)
- **Kaiser Friedrich III**, 11,130 t: five generators, 324 kW. Loads: lighting 38,260 W; searchlights 55,500 W; ventilation 28,340 W; ammunition hoists 78,440 W; gun training 8,880 W; coal winches 7,400 W; refrigeration 4,500 W. Sum ≈ 221,320 W.
- **Fürst Bismarck**, 10,650 t: 325 kW plant, 900 lamps, 44 motors.
- **Retvizan**: 3 × 132 + 4 × 66 + 1 × 60 kW, a "maximum output" of 588 kW. The units sum to 720 kW [UNCERTAIN]. She had 1,167 lamps and six 30-inch projectors.
- **Jena** (France), 12,052 t: 2 × 98.4 + 2 × 49.2 kW = 295 kW.
- The proposed 16,000 t USN battleships: two dynamo rooms, each with 4 × 100 kW sets.

**Bismarck (1940).** Total 7,910 kW at 220 V:
- 8 × 500 kW diesel generators in Electric Plants 1 and 2;
- 5 × 690 kW turbo-generators plus 1 × 460 kW in Plants 3 and 4.

The page's own table shows 3,450 kW for Plants 3 and 4, so the 460 kW set is left out there [UNCERTAIN]. The page names the consumers (weaponry, steering, lighting, cranes, fans, gyros) but gives no kW per consumer. — [kbismarck.com, propulsion](https://kbismarck.com/propulsioni.html)

**New Mexico (turbo-electric).** "Two 300 kW turbine-driven DC generators, 120/240 V" are described as the exciter units. — [General Electric Review, April 1919](https://worldradiohistory.com/Archive-Company-Publications/GE-Review/1919/General-Electric-Review-1919-04.pdf). [UNCERTAIN] Whether these also carried the ship's service load is not stated in the part read.

**Maryland and West Virginia (c.1931).** An experimental diesel ship's service plant: each ship had two Busch-Sulzer 6-cylinder engines of 950 bhp, each driving a 400 kW DC generator. No load breakdown is given. — [USNI Proceedings Nov 1931, "Diesel-Electric Installation for Auxiliaries"](https://www.usni.org/magazines/proceedings/1931/november/diesel-electric-installation-auxiliaries)

### Inferences
**Kearsarge shares of the 655 kW [EST].**

| Group | Share |
|---|---|
| Boat cranes and winches | 33.0% |
| Training, elevating, rammers | 26.2% |
| Ammunition hoists | 17.6% |
| Ventilation | 15.0% |
| Lighting | 7.3% |
| Searchlights | 0.9% |

- Turret machinery plus hoists is 43.8%, matching the earlier note's c.1911 ~40%.
- Plant over connected = 0.53.
- A battle load drops the cranes and winches: turret, hoists, ventilation and lighting come to 433 kW, or 1.24 × plant if all ran together. With turret training and hoists intermittent, a diversity of about 0.6-0.7 on the gun machinery brings it to about 300-350 kW, near the plant rating [EST].
- [UNCERTAIN] Kearsarge's searchlight line (6,000 W for four 30-inch) is far below the 70-80 A × 80 V ≈ 6 kW per light that the same paper's currents imply. It may count arc power for fewer lights, or be an OCR error for 16,000/26,000. Use about 6 kW per light.

**Kaiser Friedrich III shares of the 221 kW [EST].**

| Group | Share |
|---|---|
| Ammunition hoists | 35.4% |
| Searchlights | 25.1% |
| Lighting | 17.3% |
| Ventilation | 12.8% |
| Gun training | 4.0% |
| Coal winches | 3.3% |
| Refrigeration | 2.0% |

- Connected over plant = 0.68.
- A night-battle load (hoists, training, searchlights, lighting, ventilation) is 209 kW, about 65% of plant. German practice therefore sized the plant above the summed battle load, while USN practice sized it at about half of connected.
- The small gun-training figure (8.9 kW) suggests her turrets trained mostly by other means (hydraulic or hand) [EST].
- The big searchlight share (55.5 kW) suggests large lights. The count was not found; at about 9 kW each, six lights fit.

**Plant per ton [EST].**

| Ship | Plant per ton |
|---|---|
| Indiana 1895 | 7 W/t |
| Kearsarge 1900 | 30 W/t (connected 57 W/t) |
| Kaiser Friedrich III | 29 W/t |
| Bismarck 1940 | ≈190 W/t (on ~41,700 t standard, from general knowledge) |

**For the game [EST].** Sum the battle load from the consumers above. A pre-1905 ship's sum should land at about 0.65-1.25 of her recorded plant, depending on navy and diversity assumptions.

### Gaps
- **The Ships' Data, U.S. Naval Vessels books** (1911/1918/1921) were not reached. Whether they list motor loads, or only generators, is unanswered. shipscribe.com was not fetched within budget.
- No WWII battle-against-installed figures were found for Iowa, North Carolina or Fletcher. Only DD-692's 400 kW turbo-generator rating and the emergency diesels are sourced here.
- No sourced figure was found for ventilation's share in WWII ships.

## Q3. Which consumers were electric or steam, by navy and period

### Takeaway
**USN.** The navy electrified turret training, hoists, boat cranes, winches and ventilation from Kearsarge (1900). Steering and pumps were "not" electric in 1902. Electric pumps, galley and forced-draught blowers followed by about 1911.

In WWII destroyers, steering (electro-hydraulic), windlass, refrigeration and air-conditioning were electric. The big machinery auxiliaries stayed steam turbines: main circulating pump, fuel-oil service pump and HP air compressor. Diesel-electric DEs were all-electric.

**German.** Electric ventilation, ammunition hoists, coal winches and refrigeration appear around 1900 (Kaiser Friedrich III). By Bismarck, weapons, steering, cranes and fans were on the electric plant.

**Japanese.** Not established.

### Cited Findings
- **USN, 1902:** electric steering gear and pumps were not then used in the U.S. Navy (as summarised from George). Electric loads were turret turning, elevating, rammers, hoists, boat cranes, winches, blowers and fans. — [George 1902](https://zenodo.org/records/2210124/files/article.pdf)
- **Retvizan** (US-built, Cramp) motor count: 4 turret-turning, 4 elevating, 4 rammer, 4 turret hoists, 12 forced-draft blowers, 18 hull ventilation, 6 pumps, 2 boat cranes, 1 capstan, 8 ash hoists, 1 steering, 30 battery hoists, 2 submerged torpedo, 1 workshop, 1 torpedo lathe, 1 bread mixer, 1 laundry. — [George 1902](https://zenodo.org/records/2210124/files/article.pdf)
- **USN, 1907:** torpedo air compressor (80 hp), power doors and hatches, laundry, galley machines and workshop tools were electric. — [USNI 1907](https://www.usni.org/magazines/proceedings/1907/april/electrical-installations-united-states-navy-continued)
- **Kaiser Friedrich III:** electric lighting, searchlights, ventilation, ammunition hoists, gun training, coal winches and refrigeration (her load table). — [George 1902](https://zenodo.org/records/2210124/files/article.pdf)
- **Sumner (DD-692).** — [dd-692.com](https://dd-692.com/machinery.htm)
  - Steam: the ship-service turbo-generator, the HP air compressor (43 bhp turbine), the main circulating pump (300 bhp turbine, 26,500 gpm) and the fuel-oil service pump.
  - Electric: AC and refrigeration compressors, the windlass and the steering gear.
  - Forced-draught blowers and feed pumps are not covered on the page.
- **Slater (DE, diesel-electric):** fire and bilge pumps, air compressors and motor-generators all electric. — [ussslater.org](https://ussslater.org/motor-room)
- **Bismarck:** weapons, steering, lighting, cranes, fans and gyros on the electric plant (no kW). — [kbismarck.com](https://kbismarck.com/propulsioni.html)
- **USN voltage history:** 80 V DC at first, standardised at 125 V DC in 1905, 120/240 V three-wire DC on Arizona (1916), 440 V three-phase AC from 1932. — [Amy & Doerry 2019 slides](https://www.doerry.org/norbert/papers/20190524%20ests%20tutorial%20-%20doerry-amy%20-%20historical%20perspective%20-%20distro%20A.pdf)
- **RN voltage history:** 100 V DC at first, 220 V DC for major warships from 1908, 110 V for smaller ones. — [Amy & Doerry 2019 slides](https://www.doerry.org/norbert/papers/20190524%20ests%20tutorial%20-%20doerry-amy%20-%20historical%20perspective%20-%20distro%20A.pdf)

### Inferences
- [EST] For the game, in steam-turbine WWII USN destroyers, the electric battle load excludes the circulating, fuel-service and air-compressor pumps. Those draw on steam. The steering, fire pumps (electric in the DE; DD not established), ventilation, gun drives and fire control are electric.
- [EST] Retvizan's electric steering and forced-draught blowers show a US builder offering them in 1900, even though the 1902 paper says the USN itself did not yet use electric steering.

### Gaps
- **Japanese practice.** USNTMJ Report S-01-5, "Shipboard Electrical Equipment" (about 430 pp.), is the key source and is hosted at [fischer-tropsch.org](https://fischer-tropsch.org/primary_documents/gvt_reports/USNAVY/USNTMJ%20Reports/USNTMJ-200G-0230-0662%20Report%20S-01-5.pdf), but the fetch failed. S-01-11, "Main and Auxiliary Machinery", ([PDF](https://fischer-tropsch.org/primary_documents/gvt_reports/USNAVY/USNTMJ%20Reports/USNTMJ-200H-0386-0415%20Report%20S-01-11.pdf)) is a scan with no text layer. Both need OCR. The index is at [USNTMJ toc](https://fischer-tropsch.org/primary_documents/gvt_reports/USNAVY/USNTMJ%20Reports/USNTMJ_toc.htm).
- German WWI drive types for ventilation and pumps were not established.

## Q4. The load-analysis method: conditions, load factors, demand factors

### Takeaway
The USN Electric Power Load Analysis has these elements:
- Each consumer's connected kW × a load factor for each operating condition gives that condition's load.
- The conditions are anchor, shore, cruising, functional (the modern successor of "battle") and emergency.
- Demand factors for groups run about 0.45-0.95, falling with connected load.
- A motor at full load continuously takes a load factor of 0.9.

The sourced document is the 2012 revision (DDS 310-1 Rev 1). The WWII-era procedure itself was not found.

### Cited Findings
- **NAVSEA Design Data Sheet DDS 310-1 Rev 1** (17 Sep 2012), "Electric Power Load Analysis". It supersedes a 1 July 1980 edition. — [DTIC ADA568950](https://apps.dtic.mil/sti/pdfs/ADA568950.pdf)
  - Operating conditions: anchor, shore, cruising, functional and emergency.
  - Load factor = operating over connected load. 0.9 is "typically used" for a motor at full load for an extended period.
  - Worked example: a 19.1 kW potable-water pump with load factors 0.3/0.2/0.3/0.3/0 gives 5.7/3.8/5.7/5.7/0 kW.
  - Demand-factor curves (from MS 18299) run from about 0.45 to 0.95 against connected load.
  - 24-hour average = P_prop+steering + 0.75 × (P_cruise − P_prop+steering), or 0.60 for carriers and large-deck amphibious ships.
- Period design rules (George 1902):
  - generators to carry a 33⅓% overload for two hours;
  - voltage drop 3% on lighting and 5% on motors;
  - motor feeders sized 1,000 cmil/A continuous and 500 cmil/A intermittent.

  This is a designers' distinction between continuous and intermittent motor duty, not a numbered load analysis. — [George 1902](https://zenodo.org/records/2210124/files/article.pdf)

### Inferences
- [EST] For the game, apply this method per ship: battle load = Σ (connected kW × a battle load factor per consumer group).
  - Suggested starting factors: gun drives and hoists 0.5-0.7 (intermittent), ventilation 0.9, lighting 0.6-0.8, searchlights 1.0 at night and 0 by day, steering 0.3-0.5, fire and drainage pumps 0.5 (more when damaged), galley, laundry, workshop and boat cranes 0.
  - Check the sum against plant: about 0.5-1.0 of the recorded plant for a USN ship before 1920 (from Kearsarge and the c.1911 list), and about 0.65 for Kaiser Friedrich III.
  - The 33% two-hour overload gives a margin before a plant is "overloaded".
- [EST] The WWII term was likely "battle" rather than "functional". Not sourced.

### Gaps
- The WWII BuShips procedure, the 1980 DDS 310-1 and MS 18299 were not reached.
- The 1941/1946 "Naval Machinery" electrical chapters were not reached. maritime.org's document index lists no URLs in the fetched page.
- No Journal of ASNE paper (1905-1925) was reached; the archive.org full-text search was not run within budget.
- The Doerry-Amy 2019 historical slides contain no load breakdown, battle-against-cruising figures or load-growth curve before 1950. They give only voltages and a few plants: Trenton 1883, a 110 V 120 A dynamo; Jupiter 1912, 3 × 35 kW ship's service; Slater. Doerry's "Evolution of the Electric Warship" (NEJ 2014) covers 1989-2014 only. — [doerry.org paper list](http://www.doerry.org/norbert/papers/papers.htm)
