# Gunnery without computers: from the ironclads to the MK1 eyeball
Status: final    Updated: 2026-10-09    Request: -

*Extends [`fire-control-research.md`](fire-control-research.md) backwards (1860–1912) and sideways into emergencies. The fire-control note starts at the Dumaresq and the Dreyer table. This one covers what came before: one man at one gun, laying by eye on a rolling ship through his own smoke. It also covers what came back when a WWII or later ship lost her director, power or radar.*

**Companion code:** [`manual_gunnery_ref.py`](manual_gunnery_ref.py) (numpy, about 700 lines; imports [`fire_control_ref.py`](fire_control_ref.py) for ballistics and the hull hit test). `python3 manual_gunnery_ref.py` prints tables 1–9 in about 5 minutes; `quick` prints only the physics and prize-firing tables.
- **What it models:** every gun fires on its own unless salvos are ordered. Per shot it covers the gunlayer's error (firing on the roll, end of roll, or continuous aim), the sight, the gun's own scatter, the range on the sight (eye, stadimeter or rangefinder, passed by voice pipe, range rate ignored before the Dumaresq), the aim-off for enemy speed, smoke blinding, splash identification, spotting corrections and blunders.

**Raw research appendices** (full sourcing):
- [`predreadnought-tech.md`](fire-control-research/predreadnought-tech.md): laying, sights, mounts, smoke, rangefinding, training.
- [`early-hit-rates.md`](fire-control-research/early-hit-rates.md): battles and practice, 1862–1912.
- [`emergency-eyeball.md`](fire-control-research/emergency-eyeball.md): local control, night melee, open sights, light guns, post-war manual backups.

**Tags:** [S] sourced, [INFERRED] my derivation or tuning, [UNCERTAIN] thin sources.

---

## 0. TL;DR for the game

1. **Before about 1900 the limit was the man, not the machine.** A gunlayer firing on the roll had to press the trigger at the instant the roll swept his sight across the target.
   - The decision-to-shot delay was about 0.2–0.3 s [S]. With a roll sweeping at up to about 2–3°/s, that costs about 30 arcmin of elevation. At 2,000 yd that is about 500 yd over or short [S, USNI 1901 Amphitrite study].
   - Scott judged only 1–2% of trained seamen could hit reliably this way [S]. The model's average layer has about 13 arcmin of error in a moderate sea; a crack layer about 6 (table 1).
2. **Continuous aim plus a telescope was the single biggest jump in the whole history of gunnery** (Scott 1898, Sims 1901). The layer tracks the roll with a re-geared elevating wheel and fires when on, which cuts error to about 2–3 arcmin.
   - **Prize firing:** fleet average about 24–35% in 1897; Scott's ships 80% by 1899–1901; fleet average 79% by 1907 [S]. The model gives 18–19%, about 80% and 76%.
   - **Heavy guns** could not be geared fast enough, so they kept firing on the roll until directors arrived (1912+).
3. **Then the bottleneck moved to knowing the range.**
   - **The eye** is good for about 15% of range [S Sims 1904]. That is enough inside about 1,000 m for any gun and useless beyond about 2,000 m (table 3).
   - **Barr & Stroud's 4.5 ft rangefinder (1893)** and the 9 ft (1906) pushed usable range to about 6,000 and 8,000+ m.
   - **Range rate** was ignored, or guessed by rule of thumb, until the Dumaresq (1902–04).
4. **And then to knowing whose splash it was.** Independent fire from a dozen guns of one calibre, plus other ships firing at the same target, makes spotting guesswork. Salvo firing (about 1905–08) fixed it; the gain is modest for one ship and large under concentration (table 7).
5. **Black and brown powder halved the aimed rate of fire.** A 13-inch round blinded the layer for about 25 s against 5 s for smokeless [S]. In the model, layers either wait for the smoke to clear or fire into it at where the enemy was (table 8).
6. **Calibration** (table 6): within about ×2 of the record for Angamos 1879, Manila and Santiago 1898, and the Yellow Sea and Tsushima 1905 (12-inch). The Yalu is lower than contemporary estimates, which are themselves [UNCERTAIN]. Japanese 6-inch fire at Tsushima comes out about 3× higher than the rough record.
7. **MK1 eyeball in a WWII emergency** (table 9). With the director gone, a destroyer's 5-inch mounts shooting on eye-estimated ranges keep:

   | Range | Share of director hits kept |
   |---|---|
   | 2,000 yd | about 55% |
   | 3,000 yd | about 25% |
   | 5,000 yd | about 10% |
   | 8,000 yd | almost none |

   The eye range is the killer, not the sights or the power. A battleship turret with its own rangefinder keeps about 60% of director hits at 8 kyd. Without a rangefinder it keeps about 10%.

---

## 1. What changed, 1860–1912

| Era | Laying | Sight | Propellant | Range | Rate aid | Spotting | Typical battle range |
|---|---|---|---|---|---|---|---|
| 1860s–70s ironclads | Gun captain fires on the roll | Notch and bead | Black powder | Eye | None | None | "Point blank", ramming (Lissa) [S] |
| 1880s | On the roll; some fixed "directors" to fire broadsides [S] | Open, tangent | Brown ("cocoa") | Eye; Watkin/Mekometer short range | None | Gun captains, by eye | 800–1,100 yd [S] |
| 1894 (Yalu) | On the roll | Open, a few telescopes | QF guns go smokeless (cordite 1889–92) | Eye; first B&S FA | None | Own splash if visible | ~2,000–3,000 m [S] |
| 1898 (Manila, Santiago) | On the roll | Early telescopes, badly mounted | USN still largely brown/black | Stadimeter aloft (Fiske) | None | Pointers blinded by smoke | 1,100–6,000 yd [S] |
| 1899–1905 | **Continuous aim** on medium guns; heavy guns still on the roll | Telescopes ×4–8 [S] | Smokeless | B&S FA2/FA3 | Rules of thumb; Dumaresq 1902–04 | Officer in the top | 5–8 km (Yellow Sea, Tsushima) [S] |
| 1906–12 | Gunlayers' firing | Telescopes | Smokeless | 9 ft RF, 1% to 7,000 yd [S] | Dumaresq + Vickers clock (1906) | **Salvos** | 6–10 kyd |
| 1912+ | **Director firing** (one layer aloft fires all guns) | Director telescope | Smokeless | 9–15 ft RF | Dreyer table | Salvos | → [`fire-control-research.md`](fire-control-research.md) |

---

## 2. The gunlayer: human limits

### 2.1 Firing on the roll

- **Mechanism.** The layer set elevation, waited for the roll to carry his sight across the target, and fired.
- **USS Amphitrite study (1901)** [S]:
  - Roll ±8° with a 6 s period, so the muzzle sweeps at up to about 2⅔°/s mid-roll.
  - The delay from decision to shot was assumed at 0.2 s; one officer measured 0.3 s on average.
  - Mid-roll, that delay is about 32 arcmin of elevation: at 2,100 yd, about 450 yd over or 557 yd short.
- **Model:** elevation error = roll rate at the moment of firing × (uncompensated delay + timing scatter), with the sign flipping on the up-roll and down-roll.
- **Firing at the end of the roll** (rate near zero) was recommended in 1901. Later doctrine preferred mid-roll [S, conflicting]. The model shows why: the end point varies from roll to roll by about ±12% [INFERRED], so end-of-roll firing is *worse* in any real sea (table 1: 22 against 13 arcmin, moderate sea, average layer).

### 2.2 Continuous aim

- **Mechanism:** keep the cross-wire on the target through the roll, then fire when on. It needs elevating gear faster than the roll.
  - The US Bureau of Ordnance argued five men could not follow a 5° roll in 10 s on 6-inch gear [S]. Sims showed the gear ratio, not the men, was the limit.
  - Heavy turrets of the 1890s probably moved only about 1–3°/s [INFERRED], so they stayed on the roll.
- **Residual error** under good conditions (Sims): pointer about 1.4 arcmin, gun about ±3 arcmin [S].

### 2.3 The sight

- **1892 Yorktown trial:** open sights had about 3× the error of a telescope (mean 6.5 ft vertical and 8.9 ft lateral, against 2.25 and 2.75 ft) [S].
- **Early telescopes** (1.5×, 17° field) fogged and were "nearly useless" in damp air. Recoil cut eyes and knocked sights out of adjustment [S].
- **The US 1906 sight:** 8×, rubber eyecup [S].
- **Model, 1σ:** open 4 arcmin; early telescope 2.5; telescope 1.2; ring sight 6 [INFERRED].

### 2.4 Model table 1: elevation error, 1σ arcmin (sight + method + gun's own 2 arcmin)

| Method / sight / skill | Calm (1°, 8 s) | Moderate (3°, 10 s) | Rough (6°, 12 s) |
|---|---|---|---|
| Roll / open / green | 9.5 | 20.3 | 33.0 |
| Roll / open / average | 6.8 | 12.6 | 20.3 |
| Roll / open / crack | 4.7 | 5.9 | 7.6 |
| End of roll / open / average | 8.4 | 22.0 | 43.2 |
| Roll / telescope / trained | 3.7 | 7.1 | 11.4 |
| Continuous / early telescope / average | 3.4 | 4.1 | 5.4 |
| Continuous / telescope / trained | 2.4 | 2.7 | 3.2 |
| Continuous / telescope / crack | 2.4 | 2.5 | 2.7 |
| Stabilised (director) / telescope | 2.3 | 2.3 | 2.3 |

**What 10 arcmin of elevation error does to range** (m at the target). Short range is where an elevation error is most expensive, because the trajectory is flat:

| Gun | 1,000 m | 2,000 m | 4,000 m | 6,000 m | 8,000 m |
|---|---|---|---|---|---|
| 6″/40 QF | 225 | 188 | 125 | 83 | 62 |
| 12″/40 | 293 | 269 | 223 | 184 | 149 |

### 2.5 Skill and the human body

| Skill level in the model | Timing scatter | Uncompensated delay | Tracking error | Blunders per shot |
|---|---|---|---|---|
| Green | 0.15 s | 0.15 s | 3.5′ per °/s | 5% |
| Average | 0.10 s | 0.08 s | 2.0′ per °/s | 3% |
| Trained | 0.06 s | 0.04 s | 1.0′ per °/s | 1.2% |
| Crack | 0.035 s | 0.02 s | 0.6′ per °/s | 0.5% |

These are [INFERRED], fitted to prize firing. A blunder is a wrong range on the sight, the wrong ship, or a shot fired off target.

Other human factors [S unless marked]:
- **Visual acuity:** about 1 arcmin. A 4–8× telescope makes platform jitter, not the eye, the limit [INFERRED].
- **Human tracking loop:** about 0.3–0.5 s lag and 1–3 Hz bandwidth, adequate for 8–14 s roll periods if the gearing allows [INFERRED].
- **Rate of fire in battle:** about 30–60% of drill. Japanese 12″: 0.5–1.5 rpm best, 0.2–0.75 rpm in combat. Jellicoe: 2 rpm in the gunlayer's test, 1 rpm in battle practice [S].
- **Gunlayer improvement:** the best 6-inch layers hit at close to the gun's mechanical rate. PO Grounds made 8 hits from 8 rounds in a minute in 1901 [S].
- **Training devices:** Scott's dotter (1899) and deflection teacher let layers practise continuous aim without ammunition. The USN bought 10 dotters in 1901 [S].
- **Heat, fumes and exhaustion in casemates and turrets** are well described but not quantified. The model folds them into `combat` (×1.5 on errors in battle; ×2 for crews in their first battle) and `rate_k` (×0.5 rate) [INFERRED].

---

## 3. Knowing the range

### 3.1 One reading, 1σ including typical bias, m (model table 2)

| Method | 1,000 m | 2,000 m | 4,000 m | 6,000 m | 8,000 m |
|---|---|---|---|---|---|
| Eye estimate (~15% + personal bias) [S Sims 1904] | 180 | 360 | 726 | 1,082 | 1,460 |
| Stadimeter (1% + mast-height guess; to ~7 km) [S] | 50 | 102 | 203 | 305 | (eye) |
| B&S 4.5 ft FA (1893), ×2.5 at sea [S] | 8 | 32 | 128 | 289 | 516 |
| B&S 9 ft (1906), ×2.5 at sea [S] | 3 | 14 | 55 | 120 | 217 |

Sims's 1904 test [S]:
- At 1,193 yd broadside, 19 of 20 eye estimates fell within half the danger space; end-on only 8 did.
- Beyond 2,100 yd, no more than one observer per ship was that good. End-on errors reached 2,400 yd.

### 3.2 Range rate was ignored

Before the Dumaresq (1902–04) and the Vickers clock (1906), the range on the sights was the last estimate passed down the voice pipe. Every second the enemy opened or closed made it staler. A British pamphlet ran to five pages of rules of thumb [S]. In the model:
- The control position passes a new range every 30–120 s with 3–20 s lag.
- With no rate aid, the sight carries that range unchanged.

### 3.3 Point blank: how far can you fight on an eye-estimated range? (model table 3)

The window is the danger space of an 8 m-high hull plus its 20 m beam (broadside). The percentage is the chance an eye range lands inside it:

| Gun | 500 m | 1,000 m | 2,000 m | 3,000 m | 4,000 m | 6,000 m |
|---|---|---|---|---|---|---|
| 12-pdr | 1,721 m: 100% | 753: 96% | 273: 29% | 129: 10% | 76: 4% | 44: 2% |
| 6″/40 QF | 1,358: 100% | 636: 92% | 278: 30% | 161: 12% | 106: 6% | 58: 2% |
| 12″/40 | 1,702: 100% | 823: 98% | 388: 41% | 245: 18% | 174: 10% | 104: 4% |
| 5″/38 (WWII) | 1,780: 100% | 887: 99% | 383: 40% | 221: 16% | 143: 8% | 72: 3% |
| 16″/50 (WWII) | 1,856: 100% | 942: 99% | 467: 48% | 309: 23% | 230: 13% | 152: 6% |

**The MK1 eyeball's natural limit is about 1,000–1,500 m for any gun of any era.** Inside it, range hardly matters and only bearing and target identification count. Beyond about 2,000 m an eye-ranged gun mostly misses until splashes correct it. This matches why 1880s battle ranges were 800–1,100 yd [S], and why WWII night melees at 1,000–3,000 yd were so brutal (§8).

---

## 4. Rate of fire and smoke

**Drill rates** [S, appendix §3]:

| Gun | Rate |
|---|---|
| 12″ RML (1870s) | ~1 round per 2–4 min [INFERRED] |
| 13.5″/30 (1880s) | 0.3–0.5 rpm; loads only at 0° train |
| USN 13″/35 on brown powder | ~0.2 rpm |
| Canet 32 cm | ~2 rounds per hour (4 shots in the whole Yalu battle) |
| 12″/35 (1895) | 1 per 70–100 s |
| 12″/40 (1901) | ~1.5 rpm |
| QF 6″ | 5–7 rpm |
| QF 12-pdr | 15 rpm |

Early heavy mounts had to return to a fixed loading position after every round [S].

**Smoke** (model table 8: battery of six 6-inch-class guns at 3 km, battle):

| Powder / wind | Rounds per gun per minute | Hit % | Rounds fired blind |
|---|---|---|---|
| Black, calm or wind down the range | 1.47 | 11.7 | 87% |
| Black, brisk favourable wind | 1.84 | 10.9 | 36% |
| Brown, calm | 1.50 | 10.0 | 81% |
| Brown, favourable wind | 2.00 | 10.9 | 22% |
| Smokeless | 2.71–2.82 | 10.7–11.2 | 0% |

Smoke mostly costs **rate**: black and brown powder roughly halve the aimed rounds per minute in calm air. In battle, layers fired into the smoke at where the enemy had been. At 3 km that costs little accuracy; at longer range it is ruinous. Pointers also could not see their own fall of shot through smoke and gas, which moved spotting aloft [S, Sims 1904].

---

## 5. Whose splash is it?

- **The problem** [S]: with many guns firing independently, nobody can tell one gun's splash from another's.
  - Times of flight at 6 miles were about 19 s (12″) and 27 s (6″), so a 6-inch gun firing every 10 s had 2–3 shells in the air at once.
  - Mixed calibres (12″, 9.2″, 6″) made it worse; this was an argument for the all-big-gun ship.
- **The model:**
  - The chance of identifying a splash falls as 1/(1 + 0.6·N), where N is the other splashes of the same calibre in the window.
  - Other ships firing at the same target add their splashes.
  - A control officer aloft corrects the group about every 15 s from the balance of overs and shorts.
  - Salvos are always identified.

Model table 7 (6-inch battery, trained, 5 km, battle; hit % per round):

| Guns / ships on the target | Independent | Salvos |
|---|---|---|
| 2 guns, no other ships | 22.5 | 20.6 |
| 6 guns | 20.0 | 20.0 |
| 12 guns | 19.4 | 16.6 |
| 6 guns, 2 other ships | 16.8 | 20.0 |
| 12 guns, 4 other ships | 12.6 | 16.6 |

For one ship firing alone, independent fire with an officer aloft is nearly as good as salvos; salvos cost some rate. **Under concentration of fire, salvos win clearly.** That is where the fleets of 1905–08 found themselves.

---

## 6. Calibration

### 6.1 Royal Navy prize firing

Target: a screen 20 × 17 ft at about 1,500 yd, firing ship under way, smokeless powder. Model table 4:

| Case | Model | Record [S] |
|---|---|---|
| 1897 fleet, 6″: open sights, on the roll, average | 19.0% | 24% |
| 1897 fleet, 4.7″: same | 18.3% | 35% |
| Scylla 1897, 6″: green | 9.3% | 8% |
| Scylla 1899, 4.7″: telescope + continuous aim, crack | 79.3% | 80% |
| Terrible 1901, 6″: same | 82.6% | 80% (88% in 1902) |
| Fleet 1905–07, 6″: telescope + continuous aim, trained | 76.2% | 79% |

### 6.2 Battles and the 1912 trial

Model table 6; hit % per round. Settings of note:
- **Combat stress:** ×1.5 on human errors (×2 for crews in their first battle), rate halved [INFERRED].
- **1898 US crews** are "green": they had barely practised [S, Sims, Morison]. Their pointers could not spot through brown-powder smoke [S].
- **Yellow Sea and Tsushima** include concentration by 3–4 other ships.
- **The 1912 trial target** is assumed ship-sized [UNCERTAIN].

| Case | Model | Record [S] |
|---|---|---|
| Angamos 1879: Chilean 9″ RML on Huáscar, closing to ~600 m | 39.6 | ~⅓ (27 heavy hits) [UNCERTAIN] |
| Yalu 1894: Japanese QF 4.7/6″ on Chinese ironclads, 2.5 km | 4.5 | ~10–15% (McGiffin estimate) [UNCERTAIN] |
| Yalu 1894: Chinese 12″ Krupp on Japanese cruisers | 1.6 | ~4–6% [UNCERTAIN] |
| Manila Bay 1898: US cruisers on anchored ships, 2–5 kyd | 1.6 | 2.4% (perhaps ~4% with uncounted hits) |
| Santiago 1898: US battleship on fleeing cruisers, ~3 kyd | 3.0 (13″ 9.0, 8″ 6.0, 6-pdr 2.6) | 1.3% all; 3.5% major calibre |
| Yellow Sea 1904: Japanese 12″ and 6″, ~8 km | 3.0 (12″ 5.1, 6″ 2.8) | 12″ 4.7%; all ~1.7% |
| Tsushima 1905: Japanese 12″ and 6″, ~5.5 km, rough | 7.6 (12″ 8.1, 6″ 7.6) | 12″ ~9%; 6″/8″ ~2% [UNCERTAIN] |
| Orion trial 1912: gunlayers' firing, 13.5″, 9,000 yd, heavy sea, smoke | 9.6 | 4/27 = 15% |
| Thunderer, same trial, director firing | 22.4 | 26/39 = 67% |

**Reading the misfits:**
- **Japanese 6-inch at Tsushima (model ×3 high).** The record figure is Sims's estimate of roughly 16,875 rounds [UNCERTAIN]. Japanese medium guns also fired at many targets, including smoke-hidden ones, which the model does not capture.
- **Yalu (model ×2–3 low).** The records are contemporary estimates and have no reliable round counts.
- **1912 trial (model has the director ×2.3 better; record ×4.4).** The trial's target and ranging procedure are unknown. The direction and rough size of the director's advantage are right.
- Historical surveys undercount hits by up to 2× (burnt or sunken areas), and participants overcount [S].

---

## 7. The full gamut before computers (model table 5)

One battery of six 6-inch-class guns, using each era's human and technical means, in battle (combat 1.5, rates halved), moderate sea, against a pre-dreadnought on a parallel course at 10 kn. Each cell is hit % / hits per gun per minute:

| Era | 1,000 m | 2,000 m | 3,000 m | 5,000 m | 7,000 m |
|---|---|---|---|---|---|
| 1866 muzzle-loader, open sights, black powder, eye | 56.4 / 0.15 | 23.0 / 0.06 | 10.2 / 0.03 | 2.0 / 0.01 | 1.4 / 0.00 |
| 1885 breech-loader, open sights, brown powder, eye | 52.9 / 0.26 | 21.7 / 0.11 | 11.6 / 0.06 | 4.4 / 0.02 | 0.9 / 0.00 |
| 1894 QF, smokeless, open sights, on the roll | 57.7 / 1.57 | 24.8 / 0.67 | 10.9 / 0.29 | 3.1 / 0.08 | 1.3 / 0.04 |
| 1898 + early telescope, stadimeter aloft | 59.0 / 1.60 | 31.7 / 0.86 | 21.1 / 0.57 | 10.4 / 0.28 | 4.8 / 0.13 |
| 1901 + good telescope, continuous aim (Scott) | 97.9 / 2.65 | 87.5 / 2.37 | 65.8 / 1.79 | 17.7 / 0.48 | 6.5 / 0.18 |
| 1905 + FA rangefinder, officer spots from the top | 98.0 / 2.65 | 92.3 / 2.50 | 68.7 / 1.87 | 20.1 / 0.55 | 8.0 / 0.22 |
| 1908 + 9 ft RF, Dumaresq/clock, salvo spotting | 97.7 / 1.81 | 94.1 / 1.74 | 76.8 / 1.42 | 26.7 / 0.49 | 10.5 / 0.19 |
| 1912 + director firing | 97.1 / 1.79 | 95.9 / 1.77 | 81.3 / 1.50 | 29.0 / 0.54 | 10.4 / 0.19 |

**What each step bought:**
- **Inside 1,000 m everyone hits.** The eyeball's range is good enough and the danger space is huge. That is why the ironclad era fought there.
- **QF guns (1894)** multiplied hits per gun per minute by about 6 without making a single shot more accurate.
- **Continuous aim (1901)** is the decisive jump at every range from 2,000 to 5,000 m: hit rate ×2–4.
- **Rangefinders and spotting from the top (1905)** extend the effective range. Salvos and range-rate aids (1908) and the director (1912) add the last 10–40% at 3–7 km.
  - The director's large advantages came in rough seas, smoke and spray at turret level (the 1912 trial), and at the longer WWI ranges.
  - Neither is in this moderate-sea table; see the fire-control note.
- **This table is for a well-run ship of each era.** Real first battles were far worse: Santiago 1.3% at ~3 kyd against the 1898 row's 21%. The gap is combat shock, untrained crews, brown-powder smoke and fleeing targets (table 6).
- **Hand-off:** the 1908–1912 rows correspond to [`fire_control_ref.py`](fire_control_ref.py) levels 2–3 (Dumaresq + clock, Dreyer + director). Levels 0–1 there are coarser versions of the 1894–1905 rows here.

---

## 8. MK1 eyeball: emergencies from WWI to the missile age

**What happened when fire control was lost** [S, appendix 09]:
- **Bismarck, 27 May 1941.** The foretop director was hit at 09:02. The after station fired about 4 salvos until destroyed around 09:13. Turrets C and D then fired in local control, sporadically and with no recorded hits; D was wrecked by its own shell bursting in the barrel. Bismarck was also unsteerable and pitching.
- **USS Hoel at Samar.** With director and radar gone, her guns in local control by telescope were "very accurate". The report urged local-control practice above all.
- **South Dakota, Guadalcanal.** Lost power aft for about 1 minute, and all fire-control circuits for about 3 minutes.
- **Night melees** at 1,000–3,000 yd, often by eye and searchlight: Hiei took 85+ hits, Vincennes up to 74, Aoba up to 40.
  - Every ship that switched on a searchlight was hit hard quickly.
  - Gunners were blinded by their own flash; starshell often failed or was hidden by smoke.
  - At North Cape, Scharnhorst, with her radar wrecked, aimed at British muzzle flashes.
- **Hand drive.** 5″/38 handwheels gave 5° of elevation and 10° of train per turn against 15–34°/s under power; manual drive was "slow and arduous" [S]. Hand-ramming without bore-clearing air caused the Samuel B. Roberts cook-off [S].
- **Light guns by eye** [S]:
  - 20 mm gunners could judge range only to about 400 yd and aimed by watching their tracers.
  - Mk 14 gyro-sight guns scored 78.6% of ships' kamikaze kills.
  - The manual, unstabilised 25 mm Mk 38 Mod 1 had a third to half the hit probability of the stabilised Mod 2 against small boats (the Cole lesson).
  - See [`evasion-research.md`](evasion-research.md) table 7 for 40 mm against MTBs.

**Model table 9** (combat; hit % per round; reference rows from [`fire_control_ref.py`](fire_control_ref.py)). Destroyer battery, 5 × 5″/38, at a destroyer on a parallel course (both 30 kn), moderate sea:

| Case | 2,000 yd | 3,000 yd | 5,000 yd | 8,000 yd |
|---|---|---|---|---|
| Director + radar (Mk 37 + Mk 12), reference | 51.9 | 44.7 | 23.6 | 7.5 |
| Local control, power on, telescopes, eye range | 28.0 | 10.8 | 2.4 | 0.7 |
| Same, open sights only | 27.4 | 12.6 | 2.9 | 0.6 |
| Same, power lost: hand drive, firing on the roll | 22.9 | 9.6 | 3.7 | 0.9 |
| Local control at night, starshell, flash-blinded (3 km visibility) | 28.4 | 10.6 | — | — |

Battleship, 9 × 16″/50, at a battleship (parallel, 20 kn), moderate sea:

| Case | 8,000 yd | 12,000 yd | 16,000 yd |
|---|---|---|---|
| Director + radar (Level 6), reference | 34.3 | 21.3 | 12.2 |
| Turret local control (Level L), reference | 21.1 | 11.3 | 7.1 |
| Turret rangefinder, power on, continuous aim (this model) | 32.2 | 10.6 | 4.7 |
| Turret rangefinder, power lost: hand elevation, on the roll | 19.8 | 10.2 | 3.8 |
| No rangefinder: eye range, power on | 4.5 | 1.9 | 1.4 |

**What the emergency tables say:**
- **The range is what you lose.** Open sights against telescopes, and hand drive against power, change little: a WWII layer with a telescope can still track the roll by hand at these rates. Losing every rangefinder drops a battleship to about a seventh of her local-control hit rate.
- **A turret rangefinder plus local spotting** keeps a WWII battleship fighting at 8–12 kyd. The calibrated local-control level in the fire-control model gives about 50–60% of director hits.
  - The manual model's power-on row is optimistic at 8 kyd; it has no salvo-to-salvo error term.
- **Point blank is where the eyeball still works.** Inside about 2,000 yd an eye-ranged destroyer battery keeps more than half its director hit rate. That fits Hoel's "very accurate" local control at Samar and the night melees.
- **Practical game ranges for MK1-eyeball fire:** about 1,000 m for anything; about 2,000 yd for WWII medium guns with tracer or splash correction. Beyond 4–5 kyd, a ship without a rangefinder is shooting for morale.

---

## 9. Implementation notes for the game

1. **One gunlayer per gun mount** for pre-director ships, carrying:
   - **Skill:** green / average / trained / crack, with training improving it over a campaign (prize-firing style).
   - **Sight type** and **laying method:** roll or continuous, the latter only if mount gearing exceeds the current roll rate.
   - **A smoke-blind timer.**

   Per-shot cost is a handful of random draws, cheap enough for every gun of a fleet.
2. **Roll is a first-class environmental variable.** It decides whether continuous aim works (gear rate against roll rate) and how bad roll firing is. Pre-dreadnoughts rolled with periods of about 8–14 s [INFERRED]. The Royal Sovereigns ("rolling Ressies") rolled badly until bilge keels were fitted in 1894–95 [S]: a nice ship-design trait.
3. **Range knowledge is a separate system.** The range comes from the control position (eye, stadimeter, rangefinder), arrives after a lag, and is stale (no rate aid before the Dumaresq). Show the player the current range estimate with its age; the aging is the gameplay.
4. **Splash identification** is what makes salvo firing, calibre mixing and concentration of fire matter. One function covers it: 1/(1 + 0.6·N_same-calibre).
5. **Smoke from black and brown powder** gates the rate of aimed fire. The wind decides which battery is blind: a tactical reason to fight from windward, as Sturdee did [S].
6. **Emergency fallbacks** for WWII and later ships, by damage state:

   | Damage | Fallback |
   |---|---|
   | Director lost | Turrets use their own rangefinder and sight (Level L) |
   | Turret rangefinder lost too | Eye range + splash correction (this model) |
   | Power lost | Hand drive, firing on the roll, slower loading |
   | Night with no radar | Visibility limited to illumination; flash blinding; searchlights reveal the user |

7. **UI ideas:**
   - A roll indicator with the firing window for roll-fired guns.
   - "Range on sights" with its age.
   - A splash-identification confidence meter for the spotter.
   - Smoke drift on the battle map.

---

## 10. Gaps and uncertainties

- **Mount data:** no sourced gear ratios or elevation and training speeds for pre-dreadnought mounts, and no hand-crank rates for WWII heavy turrets.
- **Roll data:** no roll periods for specific pre-dreadnoughts.
- **Battle counts:** round counts for Lissa, the Yalu, Ulsan and Angamos are thin. Japanese medium-gun totals at Tsushima are an estimate.
- **Practice records:** RN battle-practice percentages for 1904–1910 are in HMSO returns, not online.
- **The 1912 Orion/Thunderer trial:** target size and procedure are unknown.
- **Model terms that are inferences:** skill parameters, blunder rates, splash-visibility range by calibre (40 × bore) and the combat multipliers are all [INFERRED], fitted to the calibration tables above.
- **No WWII source gives a quantified local-control-versus-director ratio.** The 1912 trial and the model are the only numbers.

---

## Sources (main; full lists in the appendices)

- USNI 1901, Notes on firing interval (Amphitrite roll study): https://www.usni.org/magazines/proceedings/1901/january/notes-firing-interval-examples
- USNI 1904, Sims, Training ranges and long-range firing: https://www.usni.org/magazines/proceedings/1904/july/training-ranges-and-long-range-firing
- USNI 1901, Smokeless powder (smoke clearing times): https://www.usni.org/magazines/proceedings/1901/october/smokeless-powder
- USNI 1949, Admiral Sir Percy Scott and British naval gunnery: https://www.usni.org/magazines/proceedings/1949/april/admiral-sir-percy-scott-and-british-naval-gunnery
- USNI Naval History 2015, Continuous-aim fire: https://www.usni.org/magazines/naval-history-magazine/2015/april/continuous-aim-fire-learning-how-shoot
- Scott's memoir: https://www.naval-history.net/WW0Book-Adm_Scott-50YearsinRN.htm
- Morison, Men, Machines and Modern Times: https://cse.iitk.ac.in/users/amit/books/morison-1966-men-machines-modern.html
- USNI 1909, Invention of the naval telescope sight: https://www.usni.org/magazines/proceedings/1909/june/invention-and-development-naval-telescope-sight
- USNI 2024, The Barr & Stroud rangefinder: https://www.usni.org/magazines/naval-history-magazine/2024/february/barr-and-stroud-rangefinder
- Admiralty Trilogy, Development of Optical Rangefinders: https://admiraltytrilogy.com/pdf/FI2024_Development_of_Optical_Rangefinders.pdf
- USNI 1905, Stadimeter fire control: https://www.usni.org/magazines/proceedings/1905/october/stadimeter-fire-control
- Naval Gazing, Directors: https://www.navalgazing.net/Directors
- Naval Gazing, Rangekeeping Part 1: https://www.navalgazing.net/Rangekeeping-Part-1
- Naval Gazing, Huáscar Part 2: https://navalgazing.net/Huascar-Part-2
- Naval Gazing, Spanish-American War Part 8: https://navalgazing.net/Spanish-American-War-Part-8
- navweaps, Japanese 12″/40 EOC (Yellow Sea, Tsushima counts): https://navweaps.com/Weapons/WNJAP_12-40_EOC.php
- navweaps, BL 13.5″/30 Mk I: https://www.navweaps.com/Weapons/WNBR_135-30_mk1.php
- navweaps, BL 12″/35 Mk VIII: https://www.navweaps.com/Weapons/WNBR_12-35_mk8.php
- navweaps, BL 12″/40 Mk IX: https://www.navweaps.com/Weapons/WNBR_12-40_mk9.php
- navweaps, USN 13″/35: https://www.navweaps.com/Weapons/WNUS_13-35_mk1.php
- USNI 1899, Effect of gun fire at Manila Bay: https://www.usni.org/magazines/proceedings/1899/april/effect-gun-fire-battle-manila-bay-may-1-1898
- NHHC H-Gram 020 (Santiago major-calibre hits): https://www.history.navy.mil/about-us/leadership/director/directors-corner/h-grams/h-gram-020/h-020-6-victory-at-santiago-.html
- Wikipedia, Battle of the Yellow Sea: https://en.wikipedia.org/wiki/Battle_of_the_Yellow_Sea
- Wikipedia, Percy Scott: https://en.wikipedia.org/wiki/Percy_Scott
- Wikipedia, William Sims: https://en.wikipedia.org/wiki/William_Sims
- kbismarck (Bismarck's last battle): https://kbismarck.com/bismarck-last-battle.html
- Battle Experience, Leyte 78.3 (Hoel): https://ibiblio.org/hyperwar/USN/rep/Leyte/BatExp/Leyte-BE-78.3.html
- NavPers Ch. 20, turret equipment and local control: https://eugeneleeslover.com/USNAVY/CHAPTER-20-F.html
- USS South Dakota War Damage Report 57: https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-south-dakota-bb57-war-damage-report-no57.html
