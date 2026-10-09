# 10 — Physical cost of fire-control components (1900–1990)
Status: final    Updated: 2026-10-09    Request: -

Scope: mass, size, crew, power and height of rangefinders, directors, computers, stable elements and FC radars, so a ship designer can trade them against accuracy. Tags: **[INFERRED]** means my estimate or derivation. **[UNCERTAIN]** means the source is thin or sources conflict. Every sourced number has its URL next to it.

**Coverage.** Hard weights in open web sources are sparse. Primary figures exist for these: Mk 37 director, Mk 1A computer, KDO 36/4 m RF, SL-series Leitstände, FT27 80 cm RF, Japanese 2.2 m height finder, Mk 3/4/8/10 radar shipping weights, SK, SPG-53, Japanese radars and Type 14. Dreyer, AFCT, DCT, Mk 34/38, the 15 m rangefinder and HACS have **no public mass figure** that I could find. Their values below are marked [INFERRED].

---

## 1. Optical rangefinders

| Instrument | Base | Mag. | Type | Mass | Crew | Notes / source |
|---|---|---|---|---|---|---|
| Barr & Stroud FA2 | 1.37 m (4.5 ft) | 24× | coincidence | — | 1 | 6,500 yd useful range. https://admiraltytrilogy.com/pdf/FI2024_Development_of_Optical_Rangefinders.pdf |
| Original B&S (1890s) | 4.5 ft | — | coincidence | — | 1 | 1% error at 3,000 yd, 3.5 in tube. https://www.usni.org/magazines/naval-history-magazine/2024/february/barr-and-stroud-rangefinder |
| B&S FQ2 (1906) | 9 ft (2.74 m) | 28× | coincidence | — | 1 | 1% at 7,000 yd. Effective to 14,500 yd. Main RF of most Grand Fleet dreadnoughts at Jutland. https://www.usni.org/... (above); FI2024 (above); https://www.jutland1916.com/tactics-and-technologies-4/range-finding-and-course-plotting-2/ |
| B&S FT24 (c.1913–15) | 15 ft (4.57 m) | 28× | coincidence | — | 1 | Over 20,000 yd. 45 delivered by 1915, 84 by 1916. FI2024; jutland1916 (above) |
| B&S FT27 (1914, field) | 0.80 m | — | coincidence | **4.69 kg** (180×910×110 mm) | 1 | https://collection.sciencemuseumgroup.org.uk/objects/co3446/ft27-rangefinder |
| Zeiss Bg 3 m (WWI) | 3 m | 25× | stereo | — | 2 per finder (Derfflinger had 7 finders) | About 165 m error at 16 km. FI2024; jutland1916 |
| Zeiss 4 m R(H)36 (Flak) | 4 m | 24×/12× | stereo | **175 kg** (instrument) | 1–2 | On the Kommandogerät 36 (1,375 kg). https://www.landmarkscout.com/kommandogerat-36-with-entfernungsmesser-4-m-rh-36-german-fire-control-computer-with-rangefinder/ |
| Japanese 2.2 m height finder (AA) | 7 ft 2.5 in | 20× | stereo | **about 227 kg (500 lb) total** incl. mount | 1 tracker | https://lonesentry.com/articles/ttt/japanese-height-finder-artillery.html |
| US Mk 58 / Mk 65 (in Mk 50 director / deck mount) | 1.5 m | 8× | stereo | — | 1 | https://mathscinotes.com/wp-content/uploads/2015/12/OP1171_Optical_Equipment.pdf |
| US Mk 42 (Mk 37 dir.) | 15 ft | — | stereo | — | 1 | https://www.eugeneleeslover.com/USNAVY/CHAPTER-25.php |
| US Mk 45 (Mk 34 dir.) | 18 ft | — | stereo | — | 1 | 1,500–50,000 yd scale. https://eugeneleeslover.com/USNAVY/CHAPTER-20-C.html |
| US Mk 48 (Mk 38 dir.) | 26.5 ft (8.08 m) | 25× | stereo | — | 1 | About ±24 m at 10 km, ±62 m at 16 km. https://www.navalgazing.net/Spot-1 ; https://navweaps.com/index_tech/tech-078.php |
| US Mk 52 / Mk 53 (Iowa turrets) | 46 ft (14 m) | — | stereo / coincidence | — | 1–2 | Iowa carried 9 RFs in all. The Mk 53 in turret I was later removed. https://navalgazing.net/Rangefinding |
| Bismarck main | 10.5 m (×5), 7 m, 6.5 m (×2), 4 m (×4 SL-8), 3 m (×2) | 50× max (foretop) | stereo | — | — | Foretop about 31 m above the sea. Turret Anton's RF removed winter 1940/41. https://kbismarck.com/controltiri.html ; https://kbismarck.com/genedata.html |
| Yamato main | 15 m | 30× | stereo (+coincidence) | **unknown; often quoted at tens of tonnes incl. turret structure [UNCERTAIN]** | several | https://unilab.gbb60166.jp/prekou/pdf/sokkyogi.pdf ; https://www.combinedfleet.com/b_fire.htm |
| Main RF base by ship (combinedfleet) | Iowa 13.5 m; Richelieu and Littorio 12 m; KGV 4.6 m (6.75 m later), turret 9.25 m | | | | | https://www.combinedfleet.com/b_fire.htm |
| IJN Fuso / Kongo | 8 m main; 4.5 m HA; 3.5 m nav; 1.5 m | | | | | https://weaponsandwarfare.com/?p=22992 |

**Accuracy law (sourced).** Range error ∝ R²/(B·M). Doubling the base halves the error. With 10″ eye resolution:

| Instrument | Error at 2 km | Error at 10 km | Error at 16 km |
|---|---|---|---|
| 3 m base, 25× | ±2.6 m | ±65 m | ±165 m |
| 8.08 m base, 25× | ±0.97 m | ±24 m | ±62 m |

Source: https://navweaps.com/index_tech/tech-078.php

Operator standards:
- The German limit was 400 m at 20 km (2%).
- The US/UK design assumption was 12″ resolution. Under 5% of people qualified as stereo operators.

Source: FI2024 (above).

**Mass scaling law [INFERRED].** Two instrument-only points give mass ∝ B^2.25, with M ≈ 175 kg·(B/4 m)^2.25:
- 0.8 m: 4.69 kg
- 4 m: 175 kg

The physical basis is that tube diameter must grow with length to stay rigid, so mass goes roughly as B²–B^2.5. Predicted bare instrument mass (B² to B^2.25):

| Base | Predicted mass |
|---|---|
| 3 m | 0.09–0.10 t |
| 4.6 m (15 ft) | about 0.23–0.24 t |
| 8 m | about 0.7–0.85 t |
| 10.5 m | about 1.2–1.5 t |
| 15 m | about 2.5–3.4 t |

Add mounting, training gear, armoured hood and crew space:
- Unarmoured mountings: ×3–5 [INFERRED].
- Armoured hoods: ×10–20 [INFERRED].

Treat any quoted rotating RF-tower weight (tens of tonnes) as hood plus armour plus structure, not optics. The Japanese 2.2 m set (227 kg with mount) is about 4× the bare-tube curve, which supports the mounting multiplier.

**Design levers:**
- Base length buys accuracy linearly.
- Magnification buys accuracy linearly but cuts the field of view and is limited by vibration.
- Height buys horizon range: d ≈ 2.08·(√h_eye + √h_target) nm.
- Stereo vs coincidence: stereo is heavier and better in poor visibility. Coincidence is lighter, faster to a first range in good visibility, and operated by one man. NDRC trials in 1941 found no important difference in precision (FI2024; https://en.wikipedia.org/wiki/Coincidence_rangefinder).

---

## 2. Directors / control positions

| Director | Mass | Crew | Armour | Contents | Height / notes | Source |
|---|---|---|---|---|---|---|
| **USN Mk 37** (5″ DP, 1939–) | **about 21 t** (BB fit, 1.5″ armour); **about 16 t** (DD fit, 0.5″). Later radar fits added about 8,000 lb (3.6 t). | 6 at GQ (officer, asst. control officer, pointer, trainer, RF operator, radar operator); 4 in Condition II | 1.5″ on BB; 0.75″ shield, barbette and tube on DD (okieboat) | Mk 42 15 ft stereo RF, pointer and trainer scopes, Mk 4 → Mk 12/22 → Mk 25 radar | Barbette 9 ft 4.5 in dia × 14 ft 3 in tall. Highest manned station. Trains ±375°, elevates −25° to +110°. 841 built, over $148 M | https://en.wikipedia.org/wiki/Mark_37_Gun_Fire_Control_System ; https://okieboat.com/Gun%20Director.html ; https://www.eugeneleeslover.com/USNAVY/CHAPTER-25.php |
| **USN Mk 34** (CA/CL and BB secondary-main) | — [INFERRED about 15–20 t] | 8 stations in shield | barbette plus rotating shield (thickness n/a) | Mk 45 18 ft RF; Mk 8 → Mk 13 radar | | https://eugeneleeslover.com/USNAVY/CHAPTER-20-C.html ; https://www.navsource.org/archives/01/57r.htm |
| **USN Mk 38** (NC/SoDak/Iowa main) | — [INFERRED about 25–35 t with 1.5″ STS hood] | 7 (spotter, trainer, pointer, cross-leveller, RF operator, 2 talkers) | 1.5″ STS | Mk 48 26.5 ft 25× RF; Mk 8 → Mk 13 radar | Air-defence level about 100 ft. Spot 1 view "about 150 ft" [UNCERTAIN] | https://www.navalgazing.net/Spot-1 |
| **USN Mk 51** (40 mm) | very light (sight box about 2 ft square) [INFERRED about 0.5 t] | 1 | none | gyro lead sight Mk 14 | | https://navweaps.com/index_tech/tech-049.php |
| **USN Mk 56** (1950s DP) | n/a [INFERRED about 8–10 t] | 4 (2 in director, 2 below) | none | Mk 35 X-band radar | Solution in about 2 s | https://eugeneleeslover.com/USNAVY/CHAPTER-26-E.html |
| **USN Mk 63** (3″/40 mm) | n/a | 6 (4 topside) | none | Mk 34 / SPG-34 radar on the gun mount | | same |
| **RN DCT (WWI director platform)** | n/a | Director platform: 4 POs + 20 men; transfer room 1 + 12; switchroom 1 WO + 3 | | Director sight; RF separate | WWI director purchases cost the RN over £1.75 M | https://www.jutland1916.com/tactics-and-technologies-4/range-finding-and-course-plotting-2/ ; https://www.worldnavalships.com/forums/thread.php?threadid=1924 |
| **RN DCT (WWII destroyer)** | — | 5 (control officer, rate officer, layer, trainer, rangetaker) | splinter | separate RF director (Mk 3W) with a UR1 RF; MR24 mounting assembly 12 ft long | | https://www.jproc.ca/haida/fcs_1943_1949.html |
| **RN HACS director Mk III–VI** | "several tons"; Mk V-era director "5 or 6 tons" | HACS Mk IV system about 17 operators (about 19 in Ark Royal), of which only part are in the director | none / weather | 12–15 ft HF/RF (Mk V duplex 15 ft); Type 285 → Type 275 | | http://www.navweaps.com/index_tech/tech-117.pdf ; https://en.wikipedia.org/wiki/HACS |
| **German SL-1 / SL-6 / SL-8** (triaxially stabilised AA Leitstand) | **SL-1 21 t; SL-6 46 t; SL-8 about 40 t** (incl. 5 t ballast); M42 6 t | 4 | splinter dome | 3 m (SL-1), 4 m (SL-6/8) RF; Würzburg on one Tirpitz SL in 1944 | Too heavy, hurt stability, fragile | https://en.wikipedia.org/wiki/Stabilisierter_Leitstand |
| **German main Kommandostand** | n/a | — | conning tower 220–350 mm (Bismarck) | 10.5 m RF + FuMO 23 in the rotating cupola | Foretop about 31 m | https://kbismarck.com/genedata.html ; https://kbismarck.com/controltiri.html |
| **IJN Type 94 Hoiban** (main director) | n/a | larger than contemporaries (no figure) | — | director sights; RF separate (Type 92 Sokutekiban, 8 crew, two decks below, ~300° train) | | https://navweaps.com/index_tech/tech-086.php |

**Rules of thumb [INFERRED]:**
- An unarmoured DP director with a 15 ft RF and radar runs about 10–16 t.
- Each 1″ of hood armour adds about 4–5 t on a Mk 37-sized shell. Steel is 40 lb/ft² per inch (navalgazing), and a Mk 37 shell is roughly 250 ft² of plate.
- A stabilised AA director runs 20–46 t (German).
- Weight times height above the waterline is the stability cost, not the weight alone.

---

## 3. Fire-control computers and tables (below armour, in the TS or plotting room)

| Device | Mass | Size | Crew | Power | Source |
|---|---|---|---|---|---|
| Dreyer Table Mk I | n/a [INFERRED about 1–2 t] | 5′8″ × 5′1.5″, 3′4″–3′9.5″ tall | 7–8 | hand-cranked, clockwork Vickers clock | https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php |
| Dreyer Mk III | n/a | 9′1.5″ × 4′2″, up to 5′9″ | 7 + 1 spare | electric motor, hand backup | same |
| Dreyer Mk IV / IV* | n/a [INFERRED about 2–4 t] | 9′3″–9′10.5″ × 4′6.5″ | 7 + 1 | electric | same |
| Dreyer Mk V (Hood) | n/a | 10′2″ × 4′3.5″ | 7 + 1 | electric | same |
| Whole Dreyer TS team | | | 5–12 at the table, plus 10–30 assisting | | same |
| AFCT Mk I (Nelson), Mk VII (QE, Renown), Mk IX (KGV), Mk X (Vanguard) | n/a [INFERRED several tonnes, larger than the Ford Mk 1] | "a quarter-size billiard table" | n/a | electric | https://en.wikipedia.org/wiki/Admiralty_Fire_Control_Table ; worldnavalships forum (above) |
| AFCC (destroyers) Mk III | n/a | | part of 14-man FKC/TS team | | https://www.jproc.ca/haida/transmitting_station.html |
| **Ford Mk 1 / 1A** (with Mk 37) | **about 3,125 lb (1.42 t)**; Star Shell Computer +215 lb | 62 × 38 × 45 in (1.57 × 0.97 × 1.14 m) | team stood around it (no number) | 115 V 60 Hz single-phase, a few A normally; synchro worst case 140 A (about 15 kW) | https://en.wikipedia.org/wiki/Mark_37_Gun_Fire_Control_System ; https://en.wikipedia.org/wiki/Mark_I_Fire_Control_Computer |
| Ford Mk 8 rangekeeper (BB/CA main) | n/a [INFERRED about 2–3 t, Mk 1-class] | — | 1 operator | — | https://navweaps.com/index_tech/tech-086.php |
| IJN Type 92 Shagekiban | n/a | — | 7 | — | same |
| German Kommandogerät 36 (Flak, with 4 m RF) | **1,375 kg** plus 175 kg RF | — | 13 + leader (army battery) | — | landmarkscout (above) |

**Plotting-room equipment, main battery (USN):** Mk 41 stable vertical, Mk 8 rangekeeper with graphic plotter, Mk 13 radar console, 5-panel switchboard, train and range indicators, and DRT Mk 6. https://eugeneleeslover.com/USNAVY/CHAPTER-20-E.html

**Stable elements per ship:** one on a DD, two on a CA/CV, four on a BB. https://www.eugeneleeslover.com/USNAVY/CHAPTER-25.php

---

## 4. Stable verticals and gyros

| Device | Mass / size | Power | Spin-up | Source |
|---|---|---|---|---|
| Mk 41 stable vertical (CA/BB) | Cabinet about 36 in square × 48 in tall. Gyro rotor about 30 lb at 12,000 rpm. Whole unit [INFERRED about 0.5–1 t]. | 400 Hz 3-phase; 115 V AC magnet | 30–60 min to speed; finds vertical in ≤5 min | https://www.navweaps.com/index_tech/tech-074.php |
| Mk 6 stable element (Mk 37) | Rotor about 10 lb at 12,000 rpm | — | about 30 min to speed; finds vertical in ≤1 min | same; housing turns at about 18 rpm (Wikipedia Mk 37) |
| Sperry FC gyrocompass | Rotor about 72 lb at 12,000 rpm | — | about 4 h to full speed | same |
| Mk 15 gyro sight (Mk 63) | air-driven gyros at 8,300 rpm | dedicated air supply | 30 min warm-up for damping fluid | https://eugeneleeslover.com/USNAVY/CHAPTER-26-E.html |

---

## 5. Fire-control and related radars

| Set | λ / band | Peak power | Pulse | Antenna | Beam | Mass | Range | Source |
|---|---|---|---|---|---|---|---|---|
| Mk 3 (FC) | 40 cm | 15–20 kW | — | 12 × 3 ft (or 6 × 6 ft) | — | **3,700 lb packed** | 16 kyd on DD, 28–40 kyd on BB; ±40 yd, 2–4 mil | https://navweaps.com/Weapons/WNUS_Radar_WWII.php ; https://ibiblio.org/hyperwar/USN/ref/Radar/Radar-2.html |
| Mk 4 (FD) | 40 cm | — | — | 6 × 6 ft | — | **3,800 lb packed** | aircraft 35–40 kyd; BB 25–30 kyd | same |
| Mk 8 (FH) | 10 cm | 15–20 kW, later 20–30 kW | — | 10.2 × 3.3 ft, 42 polyrods | — | **14,485 lb shipment incl. 1 yr of spares** | 40 kyd on BB (antenna 120 ft up); ±15 yd; crew 2 | same; https://www.navsource.org/archives/01/57r.htm |
| Mk 10 (AA, Mk 50 dir.) | — | — | — | — | — | **about 1,200 lb installed** | 10–15 kyd | ibiblio Radar-2 |
| Mk 12 | 33 cm | 100–110 kW | — | 6 × 6 ft | — | — | aircraft 45 kyd; ship 40 kyd; ±20 yd | navweaps WNUS_Radar_WWII |
| Mk 13 | 3 cm | 50 kW | 0.3 µs, PRF 1,800 | 8 × 2 ft parabola, gain 14,000 | 0.9° × 3.6° | — | 40 kyd+; splashes beyond 42 kyd | navweaps; navsource 57r |
| Mk 22 (height) | 3 cm | 25–35 kW | — | 1.5 × 6 ft "orange peel" | — | — | ≈ Mk 12 | navweaps |
| Mk 28 / Mk 25-class | 3 cm (X) | 30 kW | PRF 1,800 | 45 in dish | — | — | 15 kyd fighter | navweaps |
| Mk 34 / SPG-34 (Mk 63) | X | 25–50 kW | 0.3 µs | dish on gun mount | 2.4–3°, nutating | — | 23 km | https://en.wikipedia.org/wiki/Mark_63_Gun_Fire_Control_System |
| Mk 35 / SPG-35 (Mk 56) | X | 50 kW | 0.1–0.15 µs, PRF 3,000 | dish | 2° | — | 27 km; ±9 m | https://en.wikipedia.org/wiki/Mark_56_Gun_Fire_Control_System |
| SPG-53 (Mk 68) | X | 250 kW | — | 60 in dish | 1.6° | **antenna 163 lb; system 5,000 lb** | — | https://www.militaryperiscope.com/weapons/sensorselectronics/naval-radars/anspg-53/overview |
| SPG-60 (Mk 86) / SPQ-9 | X | — / 1.2 kW listed | — | SPQ-9: back-to-back planar arrays in radome | — | — | SPQ-9 to 20 nm | https://en.wikipedia.org/wiki/AN/SPQ-9 |
| Signaal WM-20/25 | X | 180 kW (WM-25: 200 kW via 1 MW CFA) | — | radome | — | — | 32 nm instrumented | https://www.radartutorial.eu/19.kartei/11.ancient4/karte044.en.html |
| RN Type 284 (M/P) | 50 cm | 25 kW (150 kW) | — | twin troughs on main DCT | — | — | about 10 nm | https://en.wikipedia.org/wiki/List_of_World_War_II_British_naval_radar ; https://www.hmshood.org.uk/ship/radar.htm |
| RN Type 285 (M/P) | 50 cm | 25 kW (150 kW) | 2 µs | 6 Yagis (3 Tx, 3 Rx) | 18° × 43° | — | 18 kyd; cruiser at 7 nm; ±100 yd | https://en.wikipedia.org/wiki/Type_285_radar ; jproc Haida |
| RN Type 274 | 9.1 cm | 400 kW | 0.5 µs, PRF 500 | double cheese, 2 × 14 ft × 14.5 in | 13° vertical | — | 16 nm instrumented | https://www.radartutorial.eu/19.kartei/11.ancient3/karte029.en.html |
| RN Type 275 | 8.5 cm | 400 kW | — | twin dishes | — | — | — | Wikipedia list (above) |
| RN Type 282 | 50 cm | 25–150 kW | — | twin Yagis | — | — | 0–5 kyd scale | same |
| FuMO 23 / Seetakt | 81.5 cm (368 MHz) | 8 kW (Dete 1), PRF 500 | — | 2 × 4 m mattress (FuMO 26: up to 3 × 6 m) | — | — | about 22–25 km best case; ±50 m, about 1° (lobe-switched ±0.25–0.3°) | https://kbismarck.com/controltiri.html ; https://navweaps.com/Weapons/WNGER_Radar.php ; https://en.wikipedia.org/wiki/Seetakt |
| IJN Type 22 (Mk 2 Mod 2) | 10 cm | 2 kW | 2–10 µs | separate Tx/Rx horns | — | **1,320 kg** (2,140 kg on submarines) | large ship 34.5 km; aircraft 17 km | https://www.secretprojects.co.uk/threads/japanese-radar-type-designation-systems.37818/post-479930 |
| IJN Type 32 | 10 cm | 2 kW | 10 µs | horn, lobe switching | — | 1,000 kg | large ship 30–35 km | same |
| IJN Type 21 | 1.5 m | 5 kW | 10 µs | dipole mattress | — | 840 kg | aircraft 70–100 km; ship 20 km | same |
| IJN Type 13 | 2 m | 10 kW | 10 µs | 4 Yagis, 4.2 m tall | — | **110 kg** (man-portable) | aircraft 50–100 km | same; https://p2k.stekom.ac.id/ensiklopedia/Radar_Tipe_13 |
| Italian EC.3 Gufo | 75 cm | 1 kW | — | — | — | — | range-only | combinedfleet b_fire |
| *Search reference:* SG | 10 cm | 50 kW | 1.3–2 µs, PRF 775–825 | — | 5.6° × 15° | — | BB 35–55 kyd; DD 18–35 kyd | https://en.wikipedia.org/wiki/SG_radar ; https://ibiblio.org/hyperwar/USN/ref/RADTHREE/RADTHREE-4.html |
| *Search:* SK | 1.5 m (about 200 MHz) | 250 kW | 5 µs | 15 × 16.75 ft | 10° | **antenna 2,400 lb; set about 5,000 lb** | 100 mi on medium bomber (antenna at 100 ft) | https://en.wikipedia.org/wiki/SK_radar |
| *Search:* RN 293Q | S | 500 kW | 0.7/1.9 µs | 12 ft cheese | 2° | — | 25 nm | https://www.radartutorial.eu/19.kartei/11.ancient2/karte068.en.html |
| *Search:* IJN Type 14 | 6 m | 100 kW | 20 µs | 4 Yagis | — | **30,000 kg** | 250 km single aircraft | https://en.wikipedia.org/wiki/List_of_Japanese_World_War_II_radars |

**Radar mass model [INFERRED]:**
- Set mass splits into a topside antenna (about 5–50% of the total) and below-deck electronics.
- Rule of thumb for a 1940s set: 0.5–2 t total. A 1950s set with acquisition/track: 2–3 t (SPG-53 at 2.3 t).
- Only the antenna and pedestal sit high; electronics can sit low.
- Shorter wavelength buys a narrower beam from the same aperture (beamwidth ≈ 70·λ/D degrees). That is why the 3 cm Mk 13 (8 ft) beats the 10 cm Mk 8 (10 ft) on bearing and splash resolution.

---

## 6. Height, vibration and topweight

**Height.**
- Bismarck's foretop RF sat at about 31 m (https://kbismarck.com/controltiri.html).
- The US Mk 8 radar's 40 kyd figure assumes the antenna is 120 ft up (ibiblio Radar-2).
- SK's 100 mi figure assumes the antenna is at 100 ft (Wikipedia SK).
- Mk 37 is "the highest manned station" (okieboat).

**Vibration.**
- German analysis names rhythmic oscillation from machinery, especially gearsets, as one of the main causes of poor RF results. It also cites one-sided heating from sun and funnel gas (https://navweaps.com/index_tech/tech-078.php).
- British coincidence accuracy "significantly deteriorated" with ship vibration (jutland1916).
- Temperature bending of the tube needed low-expansion bars (https://navalgazing.net/Rangefinding).
- Game suggestion [INFERRED]: an error multiplier of about 1.2–2× for tall, lightly braced tripod/pole masts or for funnels near the RF, and about 1.0× for heavy tower or pagoda mounts.

**Topweight cases:**
- **Germany:** the SL-6 (46 t) and SL-8 (40 t, incl. 5 t ballast) were judged too heavy and hurt stability (https://en.wikipedia.org/wiki/Stabilisierter_Leitstand). Bismarck's turret A RF was removed in 1940/41 (kbismarck).
- **Japan, Tomozuru reforms (Hatsuharu class):**
  - Compass bridge lowered one level, and bridge splinter armour removed.
  - After deckhouse and **rangefinder removed**.
  - MG platform lowered 1.5 m and searchlight platform lowered 2 m.
  - Funnels and masts cut 1–1.5 m.
  - 70 t of ballast added; fixed ballast later raised from 64 to 84 t.
  - Ships ended up 23% over design weight.
  
  Source: https://en.wikipedia.org/wiki/Hatsuharu-class_destroyer. Separately, the Mogami class ran 2,669 t over its declared displacement and needed bulges (https://en.wikipedia.org/wiki/Mogami-class_cruiser).
- **RN C/D destroyers:** the bridge DCT and rangefinder were removed and replaced by Type 271 radar plus Type 286 (https://en.wikipedia.org/wiki/C_and_D-class_destroyer).
- **USN:**
  - Sims: 150 t overweight, needed lead ballast, and lost a 5″ gun and tube mounts. The class introduced the Mk 37 and a deep plotting room (https://en.wikipedia.org/wiki/Sims-class_destroyer).
  - Fletcher: beam widened 18 in versus Sims. The forward tube mount was removed in 1945 for AA (https://en.wikipedia.org/wiki/Fletcher-class_destroyer).
  - Atlanta: Mk 4 → Mk 12/22 radar plus SC/SG plus Mk 44/51 directors "seriously impaired stability", and depth-charge projectors were removed to compensate (https://en.wikipedia.org/wiki/Atlanta-class_cruiser).
  - The Mk 37's later radar fits added about 3.6 t aloft (Wikipedia Mk 37).

**Designer model [INFERRED]:**
- Stability cost = Σ mass × (height above KG).
- A Mk 37 (16–21 t) about 20 m above KG costs about 320–420 t·m. That is equivalent to about 40–50 t of ballast 8 m below KG.
- Main-battery director towers with armoured hoods and long-base RFs at 30 m+ dominate the topweight budget on cruisers.
- Allow trade-offs: lower height for less horizon and less spray but less vibration; shorter base or a removed hood for less mass but more error; centimetric radar for a small antenna mass and a precise bearing.
