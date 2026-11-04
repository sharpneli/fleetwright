# B. Gun mount drives by navy and period, 1890-1945, and their electric load
Status: draft    Updated: 2026-11-04    Request: [REQUESTS.md, 2026-11-04](../../REQUESTS.md); an appendix of [electric-plant.md](../electric-plant.md)

Scope note: 20 tool calls were used. The strongest sourced figures are for the USN (navweaps motor ratings per turret, the USNI Waterbury article), Nelson, Yamato, the German WWI 30.5 cm and Bismarck's 38 cm. The weakest are the RN's hydraulic pumping engines (number, rating, steam or electric) before 1927, France, Russia, Italy and Austria-Hungary, and hand-drive rates. These are flagged in the Gaps sections. Source security: navweaps.com is a well-regarded aggregator (Tony DiGiulian) that mostly cites Friedman, Campbell and Breyer. It is good for motor hp and rates, but it is secondary. USNI Naval History is a reviewed magazine but is secondary. Hansard is primary. Wikipedia is tertiary, so I used it only for qualitative statements. 1 hp = 0.746 kW.

## Summary table (sourced figures; [EST] = inference; blank = not found)

| Navy / period | Mount | Drive (train / elev / hoists / rammer) | Train °/s | Elev °/s | Cycle | Motor/engine power per turret | Weight | Source |
|---|---|---|---|---|---|---|---|---|
| RN 1890s-1901 | heavy turrets and barbettes | hydraulic; electricity only for "parts of the gun machinery" in Renown, Barfleur, Centurion; electric turrets proposed for some cruisers | | | | | | Hansard 1901 |
| RN 1913-20 | 15-in Mk I/I* (QE, R), Mk II (Hood) | hydraulic from a ring main, water/soluble oil; elevation by hydraulic cylinder; Hood: pneumatic run-out | 2 | 5 | 36 s (Mk I), 32 s (Hood Mk II); 30.2 s best (1917) | not found | 770 t rev. (Mk I), 880 t (Mk II) | navweaps 15-in |
| RN 1927 | 16-in Mk I (Nelson) | hydraulic, light mineral oil; one 750 hp **steam** pumping engine per turret, cross-connected; training by two 400 hp swashplate engines (one in use); compressed-air run-out | 4 (2 if roll >5°) | 10 | 30 s design, 35 s actual; 60-65 s with alternate salvos | 750 hp steam per turret (~560 kW mechanical, not electric) | 1,464-1,483 t | navweaps 16-in Mk I |
| RN 1946 | 15-in on Vanguard | hydraulic, with RPC for training (none for elevation) | | | | | | navweaps 15-in |
| USN 1906 | Virginia (BB-13) | first Waterbury variable-speed gear, on the elevating screws | | | | | | USNI 2023 |
| USN pre-1906 | electric turrets | DC motors with resistor speed control; elevating screw and pinion training | | | | | | USNI 2023 |
| USN 1909+ | all new capital ships | Waterbury speed gear (constant-speed electric motor driving an A-end pump and B-end motor) on training, hoists, rammers, steering and boat cranes | | | | | | USNI 2023 |
| USN 1914 | 14-in/45 twin (New York, Texas) | electric motors through hydraulic drive gear; powered shell rammer; charges by hand; one hand-worked powder hoist per turret | 1.7 | 4 | 1.25-1.5 rpm | train 25 hp + elev 2×15 hp + lower shell hoist 10 hp + bucket hoists 2×40 hp = **145 hp (108 kW)** | 506 t | navweaps 14-in |
| USN 1916 | 14-in/45 triple (Nevada) | electric-hydraulic; shells and bags rammed mechanically | 1.7 | 4 | 1.5-1.75 rpm | train 50 + elev 40 + shell hoists 30 (count not stated) + 2×10 powder = ≥140 hp | 748 t | navweaps 14-in |
| USN 1916 | 14-in/45 triple (Pennsylvania) | electric-hydraulic | 1.7 | 4 | 1.5-1.75 rpm | train 50 + elev 40 + shell hoists 60 (count not stated) + 2×10 powder = ≥170 hp | 714-724 t | navweaps 14-in |
| USN 1921 | 16-in/45 twin (Colorado) | electric through hydraulic drive gear | ~2 | ~8 | ~1.5 rpm | train 50 + elev 2×50 + ram and upper charge hoist 90 (per gun [UNCERTAIN]) + shell hoist 35 + lower charge hoist 7.5 = 282.5 hp if the hoists are per turret, ~415 hp if per gun (**~210-310 kW**) | 880-920 t | navweaps 16-in Mk 1 |
| USN 1930s-45 | 5-in/38 twin DP (Mk 28, 29, 32, 38) | electric motors through hydraulic gear; RPC | 25 | 15 | 15-22 rpm (base ring with hoist) | train "4 hp" [UNCERTAIN, see below] + elev 7.5-10 hp + per gun hoist 7.5-10 and rammer 5-7 hp | 95,700-170,635 lb | navweaps 5-in/38 |
| USN 1930s-45 | 5-in/38 single enclosed (Mk 25, 30) | one 10 hp motor for train and elevation, plus a 7.5 hp hoist motor | | | | **17.5 hp (13 kW)** | | navweaps 5-in/38 |
| Germany 1909-17 | 30.5 cm Drh L C/1908-1913 (Helgoland to Derfflinger) | **electric training**; hydraulic elevation, rammer and breech from two electrically driven triple pumps; compressed-air run-out; two smoke-suction fans with motors | 3 | 4 | ~20 s (Kaiser: 3 rounds in 48 s) | not found | 534-549 t | navweaps 30.5 cm |
| Germany 1940 | 38 cm Drh LC/34 (Bismarck) | "in general hydraulically powered"; training gear, auxiliary elevation, auxiliary hoists and some loading gear electric | 5.4 | 6 | 2.5 rpm design; ~1 rpm in battle | not found | 1,048-1,056 t | Wikipedia 38 cm |
| France 1897 | Jauréguiberry | main turrets driven by electric motors (the first French battleship to have them); secondaries in hand-worked twin turrets | | | | | | Wikipedia |
| Japan 1941 | 46 cm Type 94 (Yamato) | training by a 500 hp swashplate (hydraulic) motor, two sets fitted, one in use; prime mover not stated | 2 | 8 (6 planned) | ~30 s at +3° loading angle; ~40-41 s at 41° | 500 hp hydraulic training motor | 2,510-2,774 t | navweaps 18-in |

## Royal Navy: hydraulic turrets and what drove the pumps

### Takeaway
RN heavy turrets were hydraulic throughout. In 1901 the Admiralty stated flatly that turrets and barbettes were "not worked by electricity but by hydraulic power". Nelson's 1927 turrets still had a 750 hp steam pumping engine each. A WWI to 1920s RN capital ship's main armament therefore needed little ship's electricity, and its pumps came from steam. I found no source dating the change to electric-motor-driven pumps (KGV, the 5.25-in mounts, the 1930s cruisers).

### Cited Findings
- In 1901 the House of Commons was told that turrets and barbettes are "not worked by electricity but by hydraulic power"; hydraulic machinery "has been preferred and has given excellent results". Electricity worked "parts of the gun machinery" in Renown, Barfleur and Centurion and would be used more on ships building. Electric machinery was proposed for the turrets, turntables and shields of several cruisers as an experiment to decide wider use. — [Hansard HC Deb 15 July 1901 vol 97 cc427-8](https://api.parliament.uk/historic-hansard/commons/1901/jul/15/electrical-machinery-on-hm-ships) (primary; secure)
- 15-in Mk I/I* (Queen Elizabeth, Revenge): "hydraulically powered from a ring main using a water/soluble oil mixture"; elevation by a hydraulic cylinder on the slide. Rates were 2°/s training and 5°/s elevation. The nominal firing cycle was 36 s, 32 s for Hood's Mk II, and one ship achieved 30.2 s on 1 Oct 1917. Revolving weight was 770 t for Mk I and 880 t for Mk II. Hood's Mk II replaced hydraulic run-out with pneumatic and added forced lubrication of the training worm shaft. — [navweaps 15-in/42 Mk I](https://navweaps.com/Weapons/WNBR_15-42_mk1.php)
- Renown in WWII: Lt. Peter Elvin reported that the "15-inch gun training hydraulic engines" failed at list angles "far less than the designed failure angle". The fallback limit was list, not electricity. — [navweaps 15-in/42 Mk I](https://navweaps.com/Weapons/WNBR_15-42_mk1.php)
- Vanguard's 15-in mountings had RPC for training only; RPC for elevation was not fitted. — [navweaps 15-in/42 Mk I](https://navweaps.com/Weapons/WNBR_15-42_mk1.php)
- Nelson and Rodney's 16-in Mk I: each turret had its own 750 hp **steam engine** in the fixed structure, cross-connected so any turret could draw on any pump. The medium changed from water to light mineral oil, which allowed steel pipes. Training used two 400 hp swashplate engines, one in use at a time, through a worm. Run-out was by compressed air. Rates were 4°/s training (2°/s when the roll exceeded 5°) and 10°/s elevation. The loading cycle was 30 s designed and 35 s actual. There were "nearly 50 mechanical interlocks" per gun, and the mountings were "never trouble-free". — [navweaps 16-in/45 Mk I](https://navweaps.com/Weapons/WNBR_16-45_mk1.php)
- At Jutland, Marlborough's torpedo hit was in the "Diesel engine room" (two killed). The shock "was sufficient to shake off switches on lower power board" and blew fuzes in telephone circuits; these "were very quickly replaced and all control instruments were found to be in step". She listed but kept firing. — [jutland1916.com, Marlborough](https://www.jutland1916.com/?p=34113) (quotes the ship's report; fairly secure). The page did not mention a hydraulic engine room.
- The British pioneered turret hydraulics with very high-pressure water. Training ran through "walking pipes" with rotating elbows, whose seals leaked repeatedly. — [USNI Naval History, Oct 2023, "Waterbury Pump"](https://www.usni.org/magazines/naval-history-magazine/2023/october/waterbury-pump)

### Inferences
- [EST] A WWI RN turret's training, elevation, rammers and (hydraulic) hoists did not depend on the electric plant, since the pumps were steam-driven at least as late as Nelson in 1927. An electric trip would cost a turret its fire-control instruments (director, transmitters and receivers), its firing circuits, its lighting and its telephones, not its power drives. Marlborough shows that the instruments' power board was itself vulnerable to shock.
- [EST] In a game, a WWI to 1920s RN capital ship could be "low electrical dependency" for gun drives and "medium" for fire control. Her drives hang on the steam supply to the hydraulic engines and on the ring main (a fractured main can be cross-connected). Nelson's cross-connection is sourced. Whether WWI ships could cross-connect is not sourced here.
- [EST] Oil hydraulics (Nelson onward) bring a fire risk that water/soluble-oil systems (WWI) did not.

### Gaps
- No source found for the number and rating of hydraulic pumping engines in Dreadnought, Iron Duke, Queen Elizabeth or Hood, or for system pressure (the ~1,000 psi in the brief is unverified), or for accumulators. Friedman's *British Battleships of the Victorian Era* / *of WWII* and the dreadnoughtproject's "Hydraulic Engine" pages are the likely sources but were not reached.
- No source on when electric-motor-driven hydraulic pumps came in (KGV 14-in, the 5.25-in mounts, 1930s cruisers' 6-in and 8-in). Many accounts say the KGV-class 14-in turrets had electric-driven hydraulic pumps, but this is unverified here.
- I could not confirm whether RN WWI 6-in casemate hoists were electric, or whether WWI shell and cordite hoists were hydraulic or electric.
- No figure for hand training of an RN heavy turret.

## United States: steam, early electric, Waterbury, and motor ratings

### Takeaway
The USN went from water hydraulics on the British model, to DC electric turrets with resistor speed control, to the Waterbury electro-hydraulic gear (constant-speed electric motor, variable-stroke pump and motor). The Waterbury gear went on Virginia's elevating screws in 1906 and on every new capital ship from 1909, for training, hoists, rammers, steering and boat cranes. So every USN dreadnought's gun drives were electric at the root. Motor ratings rise from about 145 hp per turret (New York twin 14-in, 1914) to about 280-415 hp (Colorado twin 16-in, 1921).

### Cited Findings
- Early US water hydraulics followed British designs and fell out of favour. Electric turrets used an elevating screw and pinion training. Starting from rest needed skill, because an overload could trip the breaker "at the moment the gear was needed most". DC variable-speed control needed separate heavy resistors per speed, which ran hot. The turrets worked in calm water but struggled at speed in a seaway, and low-speed continuous aiming was unsatisfactory. Electrical equipment was blamed for several serious turret fires, and a 1907 investigation recommended removing all electrical switchgear from gunhouses. — [USNI Naval History, Oct 2023](https://www.usni.org/magazines/naval-history-magazine/2023/october/waterbury-pump)
- In the Waterbury gear (Williams and Janney, Waterbury Tool Co.), a constant-speed electric motor drives an A-end pump with nine pistons and a tilting swashplate, which drives a B-end motor. Output speed runs from zero to full, and the drive reverses without reversing the motor. It is smooth at low speed but expensive to machine. It was first fitted in 1906 on Virginia's (BB-13) turret elevating screws, later used for training, and by 1909 was adopted for all new capital ships for hoists, ramming gear, rudders and boat cranes. — [USNI Naval History, Oct 2023](https://www.usni.org/magazines/naval-history-magazine/2023/october/waterbury-pump)
- Contemporary debate: USNI Proceedings carried "Cutler-Hammer Manufacturing Company's Differential System of Turret [Control]" (June 1910) and "Alternating Current versus Direct Current Use Aboard Ship" (June 1911). — [USNI 1910](https://www.usni.org/magazines/proceedings/1910/june/cutler-hammer-manufacturing-companys-differential-system-turret); [USNI 1911](https://www.usni.org/magazines/proceedings/1911/june/alternating-current-versus-direct-current-use-board-ship) (titles only; not read)
- 14-in/45 (New York and Texas, twin): "All turrets were electrically powered with hydraulic drive gears for training and elevation." Training motor 25 hp; elevation 15 hp per gun; lower projectile hoist 10 hp; bucket hoist 40 hp per gun; one manually worked powder hoist per turret. Shells were rammed by power and charges by hand. Shells were moved by man-powered block and tackle, and "Breech mechanisms were hand operated." Rates were 1.7°/s training and 4°/s elevation, at 1.25-1.5 rpm. Turret 506 t. — [navweaps 14-in/45](https://www.navweaps.com/Weapons/WNUS_14-45_mk1.php)
- Nevada: training 50 hp. Twin turrets had a 30 hp elevation motor per gun. Triple turrets had a single 40 hp elevation motor, 30 hp shell hoist motors and two 10 hp powder hoists, with shells and bags rammed mechanically and powder passed by hand from the magazine to the hoists. Pennsylvania (triple): training 50 hp, elevation 40 hp, 60 hp shell hoists, two 10 hp powder hoists. Both classes fired 1.5-1.75 rpm. Weights: Nevada twin 532 t, triple 748 t; Pennsylvania 714-724 t. — [navweaps 14-in/45](https://www.navweaps.com/Weapons/WNUS_14-45_mk1.php)
- 16-in/45 Mk 1 (Colorado, twin): "electrically powered through hydraulic drive gear". Training motor 50 hp; 50 hp per gun for elevation; "a 90 hp motor for ramming and to drive the upper charge hoist"; 35 hp shell hoist; 7.5 hp lower charge hoist. Rates were about 2°/s training and 8°/s elevation, at ~1.5 rpm. Turret 880-920 t. — [navweaps 16-in/45 Mk 1](https://navweaps.com/Weapons/WNUS_16-45_mk1.php)
- 5-in/38 twin DP (Mk 28, 29, 32, 38): "All twin DP mountings were equipped with RPC gear and powered by electric motors through hydraulic gear." The fetched text reads "Training was by a 4 hp motor while the guns were elevated by a 7.5 or 10 hp motor"; "Each gun had a 7.5 or 10 hp motor for the hoists and a 5 or 7 hp motor for the rammer." Rates were 25°/s training and 15°/s elevation. Mk 25 and enclosed Mk 30 single base-ring mounts used "a single 10 hp motor to work both elevation and train" plus a 7.5 hp hoist. Guns could be rammed by hand in an emergency, but not at high elevation. Mk 21 pedestal handwheels gave 5° elevation and 10° training per turn. — [navweaps 5-in/38](https://navweaps.com/Weapons/WNUS_5-38_mk12.php). [UNCERTAIN] A 4 hp motor driving a ~50 t twin mount at 25°/s is implausibly small. It may be a transcription error by the summarising tool (40 hp?). Recheck against the navweaps page or OP 1112 / *Naval Ordnance and Gunnery* 1944.
- West Virginia, with modern turret and fire control, made the most hits in 13 rapid salvos at Surigao Strait (25 Oct 1944). Receiver-regulators ended "hunting", so turrets could be laid automatically from directors. — [USNI Naval History, Oct 2023](https://www.usni.org/magazines/naval-history-magazine/2023/october/waterbury-pump)

### Inferences
- [EST] Connected motor load per heavy USN turret (sum of nameplates):
  - New York twin 14-in: 145 hp ≈ 108 kW.
  - Pennsylvania triple: ≥170 hp ≈ 127 kW. With one shell hoist per gun (3 × 60) the total is ~290 hp ≈ 216 kW; the count is not stated.
  - Colorado twin 16-in: ~282-415 hp ≈ 210-310 kW.
  - Running load is lower, because training and elevation motors in a Waterbury gear run at constant speed and idle unloaded when the stroke is zero, and hoists are intermittent.
  - A four-turret 16-in ship might therefore connect ~1 MW of turret motors. Steady draw might be about a third of that, a guess.
- [EST] USN battleships from 1909 are "high electrical dependency": lose the turret's power supply and training, elevation, hoists and rammers stop, leaving hand gear. Pre-1906 electric turrets were also vulnerable to breaker trips under overload, which is a game mechanic in its own right.
- [EST] 5-in/38 single enclosed mounts at ~17.5 hp (~13 kW) and twins at maybe 40-80 hp (~30-60 kW) [UNCERTAIN, from the motor list] set the load of a light/secondary mount.

### Gaps
- Iowa and North Carolina 16-in turret motor ratings were not fetched (navweaps 16-in/45 Mk 6 and 16-in/50 Mk 7 pages are the likely sources).
- The first US electric turrets were not confirmed: Brooklyn 1896, Kearsarge/Kentucky 1900 and Indiana's steam turrets were all unverified.
- No figures for hand training of a heavy USN turret (men, °/s).
- Amplidyne and Ward-Leonard use in USN mounts was not found.

## Germany

### Takeaway
German WWI dreadnought turrets (30.5 cm Drh L C/1908-1913) trained electrically and elevated, rammed and worked their breeches hydraulically from electric pumps. Both halves therefore depended on the electric plant. Bismarck's 38 cm turrets were "in general hydraulically powered" with electric training and auxiliaries.

### Cited Findings
- 30.5 cm SK L/50 mountings (Kaiser to Derfflinger; one description for C/1908-C/1913): "Training of the gun turrets was electric and gun elevation was hydraulic." "Hydraulic power was provided by two electrically-driven triple pumps." Rammers were hydraulic, fed from the elevation pump. Wedge breeches were hydraulic "but could be manually operated". Run-out was by compressed air. There were two smoke-suction systems, each with its own motor. Rates were 3°/s training and 4°/s elevation; most ships fired at ~20 s, and Kaiser fired "three rounds in 48 seconds". Turret 534-549 t. — [navweaps 30.5 cm SK L/50](https://navweaps.com/Weapons/WNGER_12-50_skc12.php)
- 38 cm SK C/34 (Bismarck): "In general the turret was hydraulically powered" but "the training gear, auxiliary elevation, auxiliary hoists and some loading gear was electrically powered." Rates were 6°/s elevation and 5.4°/s traverse. Rate of fire was 2.5 rpm design (3.125 on test), averaging roughly 1 rpm in battle. Turret 1,048-1,056 t. — [Wikipedia, 38 cm SK C/34](https://en.wikipedia.org/wiki/38_cm_SK_C/34_naval_gun) (tertiary)

### Inferences
- [EST] German WWI dreadnoughts are "high electrical dependency" (training and the pumps are both electric), unlike their RN opponents at Jutland. A dynamo-room flood would silence a German turret's power drives but not a British one's.

### Gaps
- No motor or pump kW found for German turrets, either WWI or Bismarck.
- Bismarck's hydraulic pumps were not confirmed as electric-driven. They are very probably electric, since the ship had a large turbo-generator plant, but this is unverified.
- Krupp drives of the 1890s were not found.
- Hand-drive provision for training and elevation was not found.

## France, Russia, Japan, Italy, Austria-Hungary

### Takeaway
France was an early adopter of electric turrets (Jauréguiberry, completed 1897, was the first French battleship with electrically worked main turrets) and kept hand-worked secondary turrets. For Yamato only the training motor is sourced. Russia, Italy and Austria-Hungary were not covered.

### Cited Findings
- Jauréguiberry "was the first French battleship to use electric motors to operate her main-battery turrets". Her secondaries were in "manually operated twin-gun turrets". — [Wikipedia, French battleship Jauréguiberry](https://en.wikipedia.org/wiki/French_battleship_Jaur%C3%A9guiberry) (tertiary)
- Yamato's 46 cm Type 94: training "driven by a 500 hp vertical swashplate motor driving a train of straight-toothed pinion gears", with two independent sets, one in use. Rates were 2°/s training and 8°/s elevation in practice (6°/s planned). The cycle was about 30 s at the +3° loading angle and about 11 s more at 41°. Revolving weight was 2,510 t (USNTMJ). The USNTMJ found "no complicated hydraulic safety interlocks and comparatively few mechanical ones." — [navweaps 18-in/45 Type 94](https://navweaps.com/Weapons/WNJAP_18-45_t94.php)

### Inferences
- [EST] A 500 hp swashplate *motor* implies a hydraulic system with a pumping plant of several hundred hp per turret. Whether that plant was steam-turbine or electric-driven was not established from the sources read.

### Gaps
- Yamato's pump prime mover (the brief suggests steam turbines) was not confirmed.
- Bouvet and Charlemagne drives were not checked.
- Russian Borodino and Tsesarevich electric turrets were not found.
- Italian and Austro-Hungarian drives (e.g. Tegetthoff's) were not found.
- Japanese British-built ships (Mikasa, Kongō) presumably used Vickers/Armstrong hydraulics, but this is unverified.

## Secondary and light guns; hand fallback; comparative arguments

### Takeaway
Light mounts went from hand-worked (French secondary turrets of the 1890s, pedestal guns) to small electro-hydraulic RPC mounts by WWII: USN 5-in/38 singles at ~17.5 hp and twins with a set of 4-10 hp motors. Sourced hand-drive figures are almost absent.

### Cited Findings
- Comparative arguments, by source:
  - Hydraulics (RN): "excellent results" [Hansard 1901](https://api.parliament.uk/historic-hansard/commons/1901/jul/15/electrical-machinery-on-hm-ships), but walking-pipe leakage [USNI 2023](https://www.usni.org/magazines/naval-history-magazine/2023/october/waterbury-pump).
  - DC electric (USN to 1906): resistor heat, breaker trips at start, poor low-speed aiming in a seaway, and switchgear blamed for turret fires, hence the 1907 recommendation to remove switchgear from gunhouses [USNI 2023](https://www.usni.org/magazines/naval-history-magazine/2023/october/waterbury-pump).
  - Waterbury: smooth, reversible, good low-speed control; expensive fine tolerances [USNI 2023](https://www.usni.org/magazines/naval-history-magazine/2023/october/waterbury-pump).
  - Oil hydraulics: allowed steel pipes in Nelson [navweaps 16-in Mk I](https://navweaps.com/Weapons/WNBR_16-45_mk1.php).
- Hand fallbacks sourced:
  - New York 14-in: hand breech, hand powder hoist, block-and-tackle shells [navweaps 14-in](https://www.navweaps.com/Weapons/WNUS_14-45_mk1.php).
  - German 30.5 cm: hand breech [navweaps 30.5 cm](https://navweaps.com/Weapons/WNGER_12-50_skc12.php).
  - 5-in/38: hand ramming except at high elevation; Mk 21 handwheel ratios [navweaps 5-in/38](https://navweaps.com/Weapons/WNUS_5-38_mk12.php).
  - Bismarck: electric "auxiliary elevation" and "auxiliary hoists" as the back-up for the hydraulics [Wikipedia 38 cm](https://en.wikipedia.org/wiki/38_cm_SK_C/34_naval_gun).

### Inferences
- [EST] Suggested electrification ranks for the game:
  - RN 1890-1930 heavy turrets: low (steam-hydraulic).
  - French electric turrets of the 1890s: high.
  - USN 1898-1905: high and trip-prone.
  - USN 1906/1909 onward: high.
  - German 1908-18: high (electric training plus electric pumps).
  - Bismarck: high.
  - Nelson: low (steam pumps).

### Gaps
- No sourced hand-training rates (°/s, number of men) for any heavy turret.
- No sourced weight of drive machinery separate from turret weight.
- RN 6-in hoist drives were not found, nor RPC kW for 1930s-40s RN/USN mounts (e.g. the RN 4.5-in or 5.25-in).
- Amplidyne and metadyne (RN) applications and loads were not found.
- Contemporary Brassey's or Naval Annual arguments were not reached.
- Recommended follow-ups:
  - navweaps WNUS 16-50 Mk 7 and 16-45 Mk 6 (Iowa and North Carolina motor hp).
  - navweaps WNBR 14-45 Mk VII (KGV pumps).
  - navweaps WNBR 5.25-in.
  - dreadnoughtproject "Hydraulic Engine".
  - *Naval Ordnance* 1937 Ch. IX PDF on eugeneleeslover.com (Waterbury gear and 14-in triple mount).
