# 10 — Magazines: the Flash Chain and the Venting Race, 1890-1925
Status: draft    Updated: 2026-10-20    Request: [REQUESTS.md "Magazines: the flash chain and the venting race, 1890-1925 first"](../../REQUESTS.md) (the plan: [docs/battle/magazines.md](../../../docs/battle/magazines.md))

*The numbers the magazine plan's chain and race need, as ranges by type and period: a general system, not class replicas. This note follows [07](07-magazine-explosions.md), which set out the race. Here it is given constants, a reference integration and its anchors. Where a source gave a number it is cited. Where none did, the number is fitted to the anchors or estimated, and tagged.*

Tags as in the other damage docs: **[SRC]** sourced, **[INFERRED]** my reasoning or a fit, **[EST]** an engineering estimate with no direct source, **[UNCERTAIN]** thin or conflicting sources.

**Companion code:** [`magazine_race_ref.py`](magazine_race_ref.py) (pure Python, about 10 s). It holds the burn law, the lift pressure fits, the knife-edge table, the race and every case in §6. Every derived number below comes out of it.

---

## Summary

1. **The race is decided by how much propellant burns at once, not by the burn law's fine print.** At magazine pressures (0.1-1 MPa) cordite burns at 0.4-1.8 mm/s, so one charge takes about 15 s to burn out in the open. A few square metres of vent carry away the gas of 1-5 t burning together. A magazine holds 20-60 t. So **packaging and handling decide the outcome**:
   - Charges lying open (bags out of their cases) light within about half a second, and the gas wins: **tier 3**.
   - Charges still in their packages light one from the next as a branching process. If each burning charge lights fewer than about 1.5 neighbours, the fire never gets ahead of the vents: **tier 0-1**.
2. **Burn law r = β·P^α, two branches** [SRC for MD, single-base and black powder; INFERRED for the rest]:
   - Below a knee at about 0.15 MPa, α ≈ 0.2-0.4. A vented fire stays near 1 atm and just burns.
   - Above the knee, α is 0.75-0.8 for cordite and RP and about 0.9 for single-base, so the plateau exponent 1/(1−α) is 4-10 and the race tips fast.
   - β at 1 MPa: cordite MD 1.8, Mk I ~3, RP C/12 ~1.6, single-base ~1.0 mm/s.
   - Black powder burns 30× faster than cordite at 1 atm (12 mm/s, α 0.21). Its whole surface lights at once, so it always detonates the magazine.
3. **The gunhouse lifts at 40-140 kPa gauge**, as W·g over the barbette area:
   - revolving weight W ≈ 229 t · n^0.96 · (cal/305)^2.70 (×/÷1.26);
   - barbette inner diameter D ≈ 1.28·W^0.30 m (×/÷1.05).
   - Pre-dreadnought and cruiser turrets lift at 40-90 kPa, WWI capital turrets at 90-140 kPa.
   - Once it lifts, the magazine's gas still leaves through the handing room and the hoist trunk (2-6 m²), not the whole barbette. That is why a lifted turret does not save a magazine whose stow is all alight.
4. **Charges in the chain, by practice:**
   - RN drill: about 8 charges between magazine and guns in a twin turret, about 2 per station [SRC: Brown].
   - Lion's Q turret at Jutland had exactly 8 (2,344 lb) [SRC].
   - BCF "rate first": the 1913 outfit rose from 80 to 120 rounds a gun, with the surplus outside the magazines. Doors were propped or removed and lids taken off early [SRC].
   - German practice before Dogger Bank had a full ready stow in the working chamber (Blücher's 21 cm: 26 main + 52 fore). After it, one charge per station, kept in its tin [SRC].
5. **Flooding:**
   - Valves were worked by hand in a flat beside or above the magazine. Complete flooding took **10-30 min** [INFERRED: no period figure found; valve physics]. The bottom tiers wet in a few minutes.
   - The race shows flooding has to reach about 80-90% of the charges before it saves a lids-off magazine. It saves a dry magazine outright if it is done before the flash arrives (Lion).
   - Sprinklers in magazines are a 1930s USN fit (by 1942 "for the past few years", in place of sea flooding) [SRC]. None of the first game's navies had them in magazines.
6. **Open and shielded guns: no case found of a flash going down an open mount's hoist to its magazine,** 1890-1925.
   - Every path to a magazine ran down a turret's trunk (Borodino's 6″ turret, Seydlitz, Lion, the battlecruisers) or along ammunition passages with charges lying in them (Defence, Bulwark).
   - The nearest case is Kent's **casemate** at the Falklands. Flash went down the hoist into the ammunition passage and stopped at the shut magazine door.
   - Decision 4 stands, with that casemate path as its one extension.

---

## 1. Propellant burn rates

### 1.1 The burn law

The linear burning rate is **r = β·P^α** (Vieille 1893, on Poudre B). The interior-ballistics constants come from closed vessels at 50-400 MPa. A magazine fire runs at 0.1-5 MPa, where the data are strand-burner tests.

| Propellant (service) | β, mm/s at 1 MPa | α above the knee | α below (~0.15 MPa) | r at 1 atm, mm/s | Impetus J/g | Grain | Source |
|---|---|---|---|---|---|---|---|
| **Cordite Mk I** (RN 1893-1901; IJN to 1917) | ~3.0 | 0.70-0.80 | 0.2-0.3 | 0.5-1.0 | ~1,150-1,200 | solid cord | hotter than MD (58% NG); β scaled by flame temperature [INFERRED] |
| **Cordite MD** (RN 1901-27; IJN C2 1917-) | **1.8** | **0.78** | 0.2-0.3 | 0.3-0.7 | ~1,050-1,080 | cord, MD45 = 0.45 in | strand 4.6 / 6.1 / 7.9 mm/s at 3.4 / 4.9 / 6.9 MPa for a non-catalysed NC/NG [SRC: DSJ]; α 0.7 over 0.1-10 MPa, ~0.2 below 0.15 MPa, out below 0.035 MPa [SRC: Kubota, DTIC AD0763786] |
| **RP C/12** (German 1912-) | ~1.6 (1.3-1.9) | 0.70-0.80 | 0.2-0.3 | 0.3-0.6 | ~950-1,050 | tube 18/8 mm (web 5 mm) | low-N NC with centralite, cooler than MD [INFERRED]; grain from navweaps' "820/410 × 18/8" [SRC] |
| **Single-base NC** (USN SPD, Poudre B, Russian pyroxylin) | **~1.0** (1.0-1.5) | **~0.9** (3-14 MPa); 0.73 above 14 MPa | 0.3-0.5 | 0.1-0.3 | ~950-1,000 | multi-perforated | strand 3.12 / 11.05 / 18.9 mm/s at 3.45 / 13.8 / 27.6 MPa (12.65% N) [SRC: DTIC ADA261009]; "burned quite slowly" in the open [SRC: Naval Gazing] |
| **Black powder** (to ~1890; primers, saluting charges) | 19.5 | **0.21** | same | **12** (11.5-17) | ~280-300 | 1 cm pebble or prism | r = 12 mm/s · (P/0.1 MPa)^0.21 [SRC: Trębiński 2022] |
| Brown prismatic (1885-95) | 6-12 | 0.3-0.5 | same | 3-6 | ~280 | prism | slower than black [INFERRED] |
| Checks: SC (1927), RP C/32, Gudol | 1.0-2.0 | 0.70-0.80 | 0.2-0.3 | 0.2-0.6 | 900-1,050 | — | cooler than their predecessors [INFERRED] |

The ref's rates at magazine pressures, in mm/s:

| | 0.1 MPa | 0.2 | 0.5 | 1 | 5 |
|---|---|---|---|---|---|
| cordite MD | 0.37 | 0.51 | 1.05 | 1.80 | 6.3 |
| cordite Mk I | 0.65 | 0.90 | 1.78 | 3.00 | 10.0 |
| RP C/12 | 0.35 | 0.48 | 0.95 | 1.60 | 5.4 |
| single-base | 0.16 | 0.24 | 0.54 | 1.00 | 4.1 |
| black powder | 12.0 | 13.9 | 16.9 | 19.5 | 27.3 |

**What the two branches mean.** Below the knee, α ≈ 0.2. The plateau exponent 1/(1−α) is about 1.25, so a fire with any vent stays near atmospheric pressure. Seydlitz, Gneisenau and Boise "just burned". Above the knee, α ≈ 0.78 and the exponent is about 4.5. Once confinement pushes the pressure past about 0.2 MPa, a small shortfall of vent runs away. That is 07's knife-edge, with a floor under it.

**Burn-out time** of one charge in the open is the web over r at 1 atm: cordite MD about 15 s (a solid cord burns from outside), Mk I 9 s, RP C/12 7 s (a thin tube burns from both faces), single-base 12 s, black powder 0.4 s. "Violent but not instantaneous" (Hood) is a stow lighting over a second or so, not the burn itself.

### 1.2 How readily one charge lights the next (sensitivity)

- "A spherical flash which would ignite one unit of US single-base propellant would set off 75 units of cordite" [SRC: Naval Gazing, uncited]. I couldn't trace it to a primary report. The Hood analysis supports the direction ("significantly more susceptible to accidental ignition than American powder") without giving a ratio. Treat 75× as anecdote, with a 5-75× range and about 20× as the middle [INFERRED].
- Okun: US single-base "did not … burn as fast in a magazine". Boise's forward magazine pressure rose "slowly enough to blow open" the door, and "British cordite would have blown up" [SRC: navweaps tech-007].

### 1.3 Bags against cases: how many charges light together

Packaging is the decisive input (07 §2.3; plan decision 1). The record:

| Packaging | What it did | Source |
|---|---|---|
| RN silk bags, stowed 2 quarter-charges to a Clarkson case | Lids came off early in the BCF. Every exposed bag in a space lights at once from one flash | [SRC: warshipprojects, Brown; UNCERTAIN: Friedman and Brown disagree on when cases were opened] |
| German fore charge in silk inside a tin; main charge in a brass case | Dogger Bank: 62 complete charges burned in Seydlitz's C and D turrets with no explosion. "Main charges inside their tins mostly survived." Jutland: only 2 main and 2 fore charges in C burned. Lützow's B: a fore charge on the hoist burned and the main charge above it did not | [SRC: navweaps 28 cm, weaponsandwarfare, navygeneralboard] |
| German RP C/32 in cases (Gneisenau, 1942) | Over 23 t burned with no detonation; turret Anton lifted at least 50 cm | [SRC: navweaps tech-100] |
| USN single-base bags in airtight metal tanks | Boise: about 3,000 lb burned, door blown out, the sea put the fire out | [SRC: WDR 24] |

**The model** (the ref's race):
- **Open charges** (bags out of their cases, charges in transit) light within ~0.5 s of a flash reaching their space.
- **Packaged charges** light as a branching process. Each burning charge lights R neighbours before it burns out, and the unlit pool runs down.
- R is fitted [EST]: brass case **~1.4**, USN tank **~1.2**, Clarkson case with lid on **~4**.
- With R under ~1.5 the fire burns slowly through the stow and the vents keep up (Gneisenau). With R over ~6 it behaves like open bags (§6).

---

## 2. Turret revolving weights, barbette diameters and the lift

Sourced diameters exist only for dreadnoughts and later. No pre-dreadnought barbette diameter was found. NavWeaps' "weight" is mostly the revolving weight, but not always (±5-15%).

### 2.1 Sourced mounts

| Navy, mount, year | Guns × cal | Revolving W, t | D, m | Lift, kPa | Source |
|---|---|---|---|---|---|
| RN 15″/42 Mk I (QE, R, Renown), 1913 | 2 × 381 | 782 | 9.30 ID | 113 | navweaps; warships1 t4202 |
| GER 38 cm Drh LC/1913 (Bayern), 1916 | 2 × 380 | 850-870 | 8.75 RP (ball race) | 141 on RP | navweaps |
| USN 14″/45 twin (New York), 1914 | 2 × 356 | 514-541 | 8.53 ID | 88-93 | navweaps; t4202 |
| USN 14″/45 triple (Nevada, Pennsylvania), 1916 | 3 × 356 | 725-760 | 8.84-9.14 ID | 114-117 | same |
| USN 14″/50 triple (Tennessee), 1920 | 3 × 356 | 973 | 9.45 ID | 136 | t4202 |
| USN 16″/45 twin (Colorado), 1921 | 2 × 406 | 914 | 9.45 ID | 128 | t4202 |
| RN 14″ twin / quad (KGV), 1940 | 2 / 4 × 356 | 930 / 1,607 | 8.99 / 12.19 ID | 144 / 135 | navweaps; t4202 |
| IJN 46 cm triple (Yamato), 1941 | 3 × 460 | 2,510 | 12.3-13.1 RP | 184-210 | navweaps; IMarEST |

Weights without a sourced diameter [SRC: navweaps], each with D from the fit and its lift:

| Mount | Year | W, t | D (fit), m | Lift, kPa |
|---|---|---|---|---|
| RN 12″ BVIII-BX | 1906 | 457-508 | 7.9 | 96 |
| RN 13.5″ Mk II | 1910 | 610 | 8.5 | 105 |
| RN 9.2″ Mk V single | 1899 | 112 | 5.2 | 53 |
| RN 7.5″ single | 1905 | 100 | 5.0 | 50 |
| GER 28 cm C/1906 | — | 400 | 7.5 | 89 |
| GER 28 cm C/1907 | — | 425-435 | 7.7 | 91 |
| GER 30.5 cm C/1908-13 | — | 543-558 | 8.2 | 101 |
| USN 12″/45 | 1910 | 421-457 | 7.8 | 93 |
| IJN 14″ (Kongō, Fusō) | — | 664-699 | 8.7-8.8 | 109-111 |
| Italian 12″ twin and triple | — | 508-681 | 8.1-8.8 | 98-110 |
| French 305 mm M1906 | — | 561 | 8.3 | 102 |
| Russian 12″/40 (Borodino) | — | 182-222 | 5.9-6.3 | 64-70 |
| RN 12″/35 (Majestic) | — | 187 (268 with ammunition?) | — | 65-95 |

Pre-dreadnought twins weigh 0.4-0.6× the dreadnought rule: lighter guns, thinner gunhouses, hand-worked gear. French and Russian turrets carried their machinery on a central hydraulic pivot.

### 2.2 Fits (log least squares) [INFERRED]

- **W ≈ 229 t · n^0.96 · (cal/305 mm)^2.70**, 1906-1941, N = 25, scatter ×/÷1.26. Late heavy mounts (KGV, Hood, Tennessee) sit about 20% above; German ones about 10% below. Pre-dreadnoughts: × 0.4-0.6.
- **D ≈ 1.28 m · W^0.30** (N = 11, ×/÷1.05), or D ≈ 5.47 m · n^0.38 · (cal/305)^1.08 (×/÷1.07). The barbette inner diameter is 0.6-1.5 m more than the roller-path diameter.
- **P_lift ≈ 7.7 kPa · W^0.41**:

| Mount | 2 × 152 | 1 × 190 | 1 × 234 | 2 × 280 | 2 × 305 | 2 × 343 | 3 × 356 | 2 × 381 | 3 × 406 |
|---|---|---|---|---|---|---|---|---|---|
| W, t | 68 | 64 | 112 | 354 | 445 | 612 | 998 | 812 | 1,423 |
| D, m | 4.5 | 4.5 | 5.3 | 7.4 | 8.0 | 8.8 | 10.2 | 9.6 | 11.3 |
| lift, kPa | 41 | 40 | 50 | 80 | 87 | 99 | 121 | 111 | 139 |

### 2.3 What holds a gunhouse down

- **Gravity first.** Turrets sit on a roller path, or a ball race in German turrets. Holding-down clips hooked round the roller-path foundation grew with gun power until they "would keep the turret in place even if the ship capsized" [SRC: navweaps tech-118]. Oklahoma's turrets held when she capsized. Bismarck's fell out, and so did Bayern's when she was salvaged. French Schneider turrets sat on a hydraulic central pivot that lifted the turret to inspect the rollers, which suggests no clips at all [SRC: Dredge 1900].
- The clips were sized for recoil and roll moments, not gas. Suggested multiplier on the lift [INFERRED]: k_hold ≈ 1.0 for pre-1900, French, Russian and German turrets; ~1.5 for RN and USN 1906-20; 2-3 for USN after 1920.
- **The roof goes first at lower pressure.** Roofs were 2.5-5″ plate, bolted, much lighter than the gunhouse: RN 12″ 3″; 15″ 4.25-5″; German 28 cm 6-9 cm. Invincible "blew off the roof" [SRC: Wikipedia]. A roof failing is a vent of most of the gunhouse's plan area at a fraction of the lift pressure [EST: ~0.3-0.5 × P_lift].

### 2.4 Against shipgen today

`Geometry.MakeTurretType` and `BarbetteR` give, in metres:

| Mount | Shipgen D | Real D |
|---|---|---|
| 2 × 381 | 9.40 | 9.30 |
| 2 × 343 | 8.61 | ~8.5 (fit) |
| 2 × 305 | 7.82 | 7.8-8.2 (fit) |
| 2 × 152 | 4.98 | 4.5 (fit); Leander's 5.33 |
| 3 × 356 | 11.12 | 8.84-9.45 |
| 3 × 406 | 12.48 | 11.35-11.73 |

Twins are close. Triples come out 10-25% wide: the gunhouse width formula spaces three guns as if each needed a twin's room. The lift pressure goes as 1/D², so a triple's lift comes out 20-40% low if read from shipgen's radius.

---

## 3. Flash doors, scuttles, interlocks and charges by station

### 3.1 Doors and practice by navy and period

| Navy, period | What was fitted | How it was used |
|---|---|---|
| **RN to May 1916** | Magazine doors opened inward to the handing room. Flash doors in the trunk and on the cages were too weak to resist a burning charge [SRC: Brown 1989]. Charges were to travel in Clarkson cases, opened just before loading | **BCF:** flash doors left open or removed, Clarkson cases dropped, the turret and barbette in effect a ready-use locker. All of Beatty's ships but Lion, where gunner Grant and Chatfield kept the rules [SRC: warshipprojects; Brown; Naval Gazing]. Outfit raised from 80 to 120 rounds a gun in 1913, with the surplus outside the magazines [SRC: amuedge]. Secondary cordite supply "even worse" (Barham, Defence) [SRC: Brown]. Grand Fleet battleships stricter: no source found that sets it out [UNCERTAIN] |
| **RN 1916-19** | Within days, Whale Island issued sketches of temporary anti-flash measures. By spring 1917: fearnought and leather screens, Clarkson cases back for secondary cordite, handing rooms for secondaries. A fleet committee recommended air-tight handling scuttles for the turret-magazine doors. Trials: Vengeance (Aug 1917) held 2 full charges burning in the handing room; 15″ charges later distorted the doors; Prince of Wales (Jul 1919) passed with 2 × 15″ charges [SRC: Brown; Sims letter] | Magazine rules enforced |
| **RN 1930s-40s (check)** | KGV: inner watertight door and outer flash-tight door at each magazine opening; revolving handing-room scuttles of 2 charges, flash-tight at 120°; cage flash doors interlocked; the hoist can't rise until the trunk doors are shut [SRC: navweaps tech-122] | — |
| **German to Jan 1915** | Double doors from a turret's cartridge room to the next turret's. Main charges in brass, fore charges in silk inside tins | Charges out of their tins, ready. Blücher's wing turrets: 26 main + 52 fore in the working chamber [SRC: navweaps 21 cm]. At Dogger Bank, Seydlitz's connecting door was opened by men escaping; the second door was blown off [SRC: weaponsandwarfare] |
| **German 1915-18** | Hoist doors closing after the cage passes; double flap doors at both ends of the cartridge hoists; doors between magazines padlocked, key with the turret officer; separate magazines per turret in new ships [SRC: Castles of Steel; navweaps] | Tins opened only at the breech; no more fore charges out than main; no ready charges stacked in the gunhouse. Not complete by Jutland. Brown: the Germans limited the charges out but did little to make the route flash-resistant [SRC] |
| **USN 1904-08** | After Missouri (1904): automatic shutters at the ends of the hoists, breech gas ejectors. After Kearsarge (1906): sparking gear out of the mountings, bulkheads between the guns [SRC: navweaps 12″/40] | In both fires a man shut the magazine door or hatch in time [SRC] |
| **USN 1920s-50s (check)** | Iowa: flame-tight bulkheads between guns, powder scuttles from magazine to handling room, hoist doors interlocked so loading and delivery doors can't be open together, one car one charge. Cased-ammunition mounts: revolving two-pocket scuttles [SRC: NavPers 10797-A] | — |
| **IJN, Russian, French, Italian, A-H** | No data found [UNCERTAIN]. The IJN used British cordite and British-built turrets to 1917, so take RN pre-1916 drill. Russian and French turrets carried their machinery in the turret on a pivot | — |

### 3.2 Charges by station

| Practice | Gunhouse | Working chamber | Hoists / cages | Handing room | Source |
|---|---|---|---|---|---|
| RN drill, twin turret, in action | ~2 (at the breeches) | ~2 | ~2 | ~2 | "8 charges between magazines and guns in action" [SRC: Brown]; the split by station is [INFERRED] |
| Lion's Q, Jutland | loose cordite in the gun-well | — | both gun-loading cages loaded | — | 8 charges, 2,344 lb (= 8 × 293 lb) [SRC: Naval Gazing; navweaps] |
| BCF "rate first", 1916 | charges present | present | in the revolving trunk | present: a stack | Report on Indefatigable, "a direct train of cordite" [SRC: TNA ADM 1/8477/308]. The surplus of the 80 → 120 outfit is up to ~40 rounds a gun outside the magazines [INFERRED from amuedge] |
| German before Dogger Bank (21 cm wing turret) | — | 26 main + 52 fore | — | — | [SRC: navweaps 21 cm] |
| Seydlitz, Dogger Bank | 62 complete charges burned in two turrets' trains, ~31 each | — | — | — | [SRC: navweaps 28 cm] |
| German after Dogger Bank | none stacked | ≤ 1 per gun, in its tin | 1 per cage | — | Jutland, Seydlitz's C: 2 main + 2 fore burned [SRC] |
| Bulwark (1914, at anchor) | 20 per casemate | — | 20 in cases at each end of each cross-passage, on hooks by steam pipes | ~30 loose full charges per cross-passage | court of inquiry [SRC: greatwarforum] |

### 3.3 How often doors stood open

No source gave a share. From the hoists' cycle [EST]:
- **Safe** (interlocked, revolving scuttles): never open through. A flash meets a shut door at each step.
- **Drill:** each door opens once a cycle while the cage or scuttle passes. A door open ~1-3 s in a 30-60 s cycle is open **5-15%** of the time; two doors in series are both open well under 5%.
- **Rate first:** doors propped or removed, **100%** open (the BCF; Seydlitz's connecting door).

A door holds only up to its rating. RN doors before 1916 were "too weak to resist the pressure of a burning charge" [SRC: Brown]. Lion's magazine doors buckled from 8 charges burning outside them and held, with water behind them [SRC]. After 1917 a door held 2 full charges burning in the handing room [SRC: Brown].

Ratings for the train [EST]:
- pre-1916 RN and pre-1915 German flash doors fail at **20-40 kPa**;
- post-1917 flash-tight doors and scuttles hold **60-100 kPa**;
- a watertight magazine door holds **~100 kPa** (10 m of head).

---

## 4. Flooding and sprinkling

### 4.1 Who had what, when

| Navy | 1890-1918 | After |
|---|---|---|
| RN | Kingston valves (sea cocks) under each magazine, standard. Flooding was ordered by voice pipe from the turret and the valves opened in a flat near the magazine [SRC: dreadnoughtproject; devonheritage]. After Jutland the emphasis went to flash doors; the Grand Fleet's orders held that shutting Lion's door, not flooding, probably saved her [SRC: greatwarforum] | sprinklers in RN magazines by WWII [UNCERTAIN on the date]; modern density 30 L/min/m² |
| German | Flooding valves by hand near the magazine (Heidkamp turned them red-hot) [SRC]. After Dogger Bank: water had run through shared vent trunks, so each compartment got its own vents; drains for flooded magazines recommended [SRC: weaponsandwarfare] | — |
| USN | Flooding from the sea; magazine vent exhausts with check valves inside the barbette [SRC: eugeneleeslover] | 1932 C&R bulletin on "magazine flooding and sprinkling"; by 1942 sprinkling "for the past few years" in place of sea flooding [SRC: WDR 29 ¶92]. Modern 0.80 gpm/ft² (32 L/min/m²), trip at 160°F [SRC: FAS] |
| French | Flooding valves *below* the magazines. Liberté (1911): the arrangements were found insufficient [SRC] | modest changes [SRC] |
| Russian | Fitted. Oslyabya flooded a magazine to correct a list (1905); Imperatritsa Mariya flooded hers by hand (1916) [SRC] | — |
| Japanese, Italian | no data [UNCERTAIN] | — |

**No sprinklers in magazines** for any navy in the first game's period, as far as the sources go.

### 4.2 How long

No period "flood in N minutes" rule was found. From valve physics [INFERRED]: a sea valve at ~5 m head flows at ~10 m/s. A 150 mm valve fills a 300 m³ magazine in ~28 min, a 250 mm valve in ~10 min. So:
- **full flooding 10-30 min** (1890-1918);
- **the bottom tier of charges wet in 2-5 min**;
- **the order and the walk to the valves**: 1-5 min after the decision (Lion: the gunner was at the magazine 2-3 min after the hit; the flooding order came "later" [SRC: greatwarforum]).

Water rises from the floor, so the flooded share of the stow is the water's depth over the stow's height.

### 4.3 Cases

| Ship, event | Order and timing | Outcome |
|---|---|---|
| Seydlitz, Dogger Bank 1915 | Barbette hit at ~10:40; both after magazines flooded by hand through red-hot valves. Water ran through the vent trunks; stern 1.05 m deeper [SRC] | saved; 62 charges (6.6 t) burned above |
| Lion's Q, Jutland | Hit at 16:00; "close doors, open flood valves". Charges in the chamber and cages smouldered and lit at 16:28 (28 min); the flash buckled the magazine doors [SRC] | saved |
| Lion, Dogger Bank | A magazine partly flooded on a false fire report [SRC] | — |
| Derfflinger C and D, Jutland | "Timely closing and flooding of the magazines" [SRC: USNI 1925] | saved; 73 of 78 and 80 of 80 dead |
| König, Jutland | Two 15 cm casemate magazines flooded after splinters lit charges [SRC] | saved |
| Tiger, Jutland | Water main cut; midships 6″ magazines flooded [SRC] | saved |
| Kent, Falklands 1914 | Casemate flash down the hoist to the passage; Sgt Mayes threw a burning charge clear and flooded the *compartment* with a hose [SRC] | saved |
| Glatton, 1918 (accident) | Midships 6″ magazine fire; forward magazines flooded; flames cut off the after controls [SRC] | scuttled after 2 h |
| Liberté, 1911 (accident) | Smoke at 05:31; three tries to reach the valves below the magazine, two driven back; blew at 05:53 (22 min) [SRC] | lost |
| Iéna, 1907 (accident) | In dry dock: no sea to flood with [SRC] | lost |
| Imperatritsa Mariya, 1916 | First explosion before it could be fought; magazines flooded by hand; second ~40 min later [SRC] | capsized |
| Boise, 1942 (check) | The sea came in "immediately through the projectile hole" and put out the fire [SRC: WDR 24] | saved |
| Astoria, 1942 (check) | 8″ magazines flooded; 5″ not: hand station unreachable, electric controls dead; blew ~8.5 h later [SRC: WDR 29] | lost |

**How flooding failed:** valves under or beside the fire (Liberté), heat at the valve (Seydlitz), fire over the control flat (Glatton, Astoria), power lost to remote controls (Astoria), mains cut (Tiger, for hoses and sprinklers), no sea (Iéna), delay in the order (Lion), the crew killed (Liberté, Imperatritsa Mariya). These are the plan's "controls cut" inputs.

---

## 5. Open and shielded guns: the flash's paths

| Case | Mount | What burned; how far it went | Magazine? |
|---|---|---|---|
| Matsushima, Yalu 1894 | gun-deck battery, 12 cm | 30.5 cm hit at No. 4 gun while loading; 28 killed, 68 wounded, 3 guns out; a petty officer kept the fire off the magazine [SRC] | no |
| Missouri, 1904 | turret (accident) | flareback; fire went down into the handling room, second larger blast, 33-36 dead [SRC] | no, door shut |
| Kearsarge, 1906 | turret (accident) | burning powder fell into the handling room; 10 dead [SRC] | no, hatch shut |
| Borodino, Tsushima 1905 | 6″ turret | a 12″ hit below the turret lit ready charges; the fire reached a 6″ magazine [SRC] | **yes, by a turret's trunk** |
| Kent, Falklands 1914 | 6″ **casemate** | burst lit several charges; flash **down the hoist into the ammunition passage** [SRC] | no, stopped at the passage |
| Bulwark, 1914 | ammunition passages (accident) | cordite stacked in passages [SRC] | yes |
| Seydlitz, 1915 | turret | chamber → handing room → through the connecting door to the next turret [SRC] | no, flooded |
| Defence, Jutland | 9.2″ turret, then passages, then 7.5″ turrets | aft 9.2″ magazine; fire along the passages to the 7.5″ magazines [SRC] | yes, by passages |
| Malaya, Jutland | 6″ casemate battery | two 12″ hits burned out the battery, most of her 63-65 dead there [SRC]; charge count unknown | no |
| Chester, Jutland | open-backed shielded 5.5″ | 17 × 15 cm hits; 29 killed; no cordite fire reaching a hoist recorded [SRC] | no |
| Tiger, Jutland | passage | a shell lit "ready cordite in the passageway" [SRC] | no |
| Good Hope, Monmouth (Coronel) | turrets and casemates | Good Hope blew up between mainmast and funnel; Monmouth's fore 6″ turret blown off; paths unrecorded [UNCERTAIN] | probably |

**Verdict.** No case was found, 1890-1925, of a flash going down an open or shielded deck mount's hoist (a dredger hoist or a whip) to its magazine. The paths to a magazine were:

1. a turret's trunk and handing room;
2. ammunition passages with charges lying in them (Defence, Bulwark);
3. a casemate's hoist into the passage below it (Kent, stopped there).

This is an absence in the sources reached, not proof. Campbell's *Jutland* could overturn it for Malaya, Southampton or Black Prince.

---

## 6. The race run on the anchors

The ref's race integrates one gas volume:
- **Gas made:** ρ·S(t)·r(P), from the lit charges' surface. A cord's surface shrinks as √(1−z), a tube's stays constant.
- **Gas out:** choked or subsonic flow through the open vents (C_d 0.7, γ 1.25), with half the impetus kept as pressure in a cold steel room [EST].
- **Boundaries** fail at their pressure and add their area [EST, after 07 §2.4]:

| Boundary | Fails at | Vent added | d |
|---|---|---|---|
| magazine doors | 40 kPa | 1 m² | — |
| gunhouse lift | P_lift | the trunk's 2-6 m² | — |
| bulkhead to the next space | 250 kPa | 4 m² | a neighbour magazine is tier 4 |
| armoured deck | 700 kPa | 10 m²: opens into the closed 'tween deck | 0.35 |
| sides | 1,000 kPa | 60 m² | 0.45 |
| bottom | 2,000 kPa | — | 0.2 |

### 6.1 The knife edge as one number

The table gives the tonnes of fresh propellant that may burn at once and keep the gas under p:

| Vent | p (gauge) | cordite MD | RP C/12 | single-base | black powder |
|---|---|---|---|---|---|
| an open flash door, 0.5 m² | 110 kPa | 0.36 | 0.35 | 0.58 | 0.02 |
| the hoist trunk, 2 m² | 110 | 1.4 | 1.4 | 2.3 | 0.06 |
| the hoist trunk, 2 m² | 700 | 1.9 | 1.9 | 2.7 | 0.17 |
| a holed gunhouse, 5 m² | 110 | 3.6 | 3.5 | 5.8 | 0.15 |
| a lifted 15″ barbette, clear, 34 m² | 110 | 24 | 23 | 39 | 1.0 |

So a turret's own train (8-60 charges, 1-6 t) vents through its gunhouse: Lion's 8, Seydlitz's 31 per turret. A magazine (20-60 t) vents only if its charges light a few at a time.

### 6.2 Cases

| Case | Inputs | Peak, kPa | Burned, t | Tier | Record |
|---|---|---|---|---|---|
| BCF 1916: flash in a 13.5″ twin's magazine, lids off | 220 × 133 kg open | 1,003 | 29 | **3** | tier 3 ✔ |
| the same, lids on | 220 in Clarkson cases (R 4) | 273 | 28 | 1 | — |
| a two-turret group, lids off | 440 open | 1,016 | 58 | **3** | Queen Mary ✔ |
| Lion's Q: 8 charges, gunhouse holed | 8 open, 5 m² | 3 | 1.1 | 0 (a flame column) | ✔ |
| Lion's Q had the magazine been dry | 8 open + 220 lids on, door 60 kPa | 368 | 28 | 1 | debated [UNCERTAIN] |
| Seydlitz, one turret's train | 31 open, 6 m² | 26 | 3.3 | 0 | ✔ |
| both turrets' 62 in one train | 62 open | 87 | 6.6 | 1 | worse than the record |
| a dry 28 cm magazine, brass | 4 open + 180 cased (R 1.4) | 87 | 10 | 1 | — |
| Gneisenau 1942 | 23 t cased (R 1.4) | 127 | 11.5 (of 23) | **1**, turret lifted | ✔ |
| Boise 1942 | single-base in tanks (R 1.2) | 40 | 5.5 | **0**, door blown | ✔ |
| black powder, 12″ group | 30 t | 1,031 | 30 | **4** | ✔ (pre-1890) |

Flooding (the BCF lids-off case):

| Flooded share | 0 | 50% | 80% | 90% | 95% | 99% |
|---|---|---|---|---|---|---|
| tier | 3 | 2 | 1 | 1 | 0 | 0 |

Packaging R swept (a 15″ magazine, 200 × 194 kg, lids on):

| R | 0.8 | 1.2 | 1.4 | 2 | 3 | 4 | 6 | 8 |
|---|---|---|---|---|---|---|---|---|
| tier | 0 | 0 | 0 | 1 | 1 | 1 | 1 | 2 |
| peak kPa | 3 | 23 | 52 | 116 | 253 | 361 | 699 | 720 |

Read with care:
- The tiers are the ref's thresholds, which are [EST]. **What is robust is the ordering:** open bags ≫ Clarkson lids on > brass ≈ tanks, and flooding to 80-95% before the flash.
- **The "Lion dry" case** says tier 1 with lids on. Harvey's VC and the Grand Fleet's verdict say flooding (or shutting the door) saved her. That fits if the BCF's lids were off, Lion's own were on (Grant), and R for a Clarkson case lies near 4-8 rather than 2. That is the most uncertain constant here.

---

## 7. Recommendations for this codebase

For [docs/battle/magazines.md](../../../docs/battle/magazines.md), step by step:

- **V2, the train:**
  - Each station holds `open` charges by the discipline:
    - safe: 1 per gun at the breech and 1 per cage;
    - drill: ~2 per station for a twin (8 in all);
    - rate first: drill plus a stack of ~20-40% of the magazine's rounds in the handing room and working chamber, lids off.
  - Doors: safe never open, drill open ~10%, rate first 100%.
  - Door ratings by period: pre-1916 20-40 kPa, post-1917 60-100 kPa.
  - A casemate's hoist leads to its passage, then the magazine door; an open or shielded mount's train starts below the deck (decision 4, unchanged).
- **V3, the race:**
  - Use the two-branch burn law with the §1.1 constants.
  - Burn surface per kg: 4/(ρ·d) for a cord, 1/(ρ·half-web) for a tube.
  - Packaging as two numbers per gun block: the stow's open share (by discipline) and R (bags out of cases ~∞; Clarkson ~4 [fit 4-8]; brass ~1.4; tank ~1.2).
  - The gunhouse lift from W·g/(π·D²/4) × k_hold, with the vent it adds the trunk's section (2-6 m²), not the barbette's.
  - The roof plate as a vent at ~0.3-0.5 P_lift.
  - Black powder (primers, saluting charges, pre-1890 ships) whole-surface at once.
  - `MagazineFastShare` goes; the look reads the burned share and the failure times.
- **V3, the turret weight:** `Batteries.MountWeights` already weighs the turret, so read W from it and check it against the §2.2 fit. **`Geometry.BarbetteR` makes triples 10-25% too wide** (the gunhouse width spacing): worth a look when V1 touches the turret types. That's a golden change of its own.
- **V6, the valves:**
  - Full flooding 10-30 min by valve size, the bottom tier wet in 2-5 min, the flooded share as depth over stow height.
  - The order takes 1-5 min.
  - The controls are cut by fire over the valve flat, by lost power (remote controls only), by a cut main (hoses), and in dry dock.
  - No magazine sprinklers before the late 1920s USN; a later-cut design option.
- **V8, the yardstick:** the §6.2 cases with their inputs are the anchors. Fit R for the Clarkson case and the boundary pressures there, keeping the ordering.

## 8. Open questions

- **R for a Clarkson case** and whether BCF magazines had their lids off in the magazine itself, or only in the handing room. Friedman and Brown disagree (Naval Gazing fn 5).
- **No primary source for β, α of cordite MD or RP C/12 at low pressure.** The values are a modern double-base strand set and Kubota's general curve. Corner (1950), the Textbook of Ballistics and Gunnery, and TM 9-1300-214 would settle the impetus and flame temperatures, tagged here as recalled.
- **Period flooding times and valve bores** (Friedman, *British Battleships*; Brown, *The Grand Fleet*), Seydlitz's water tonnage, and Japanese practice.
- **Pre-dreadnought barbette diameters**: none found; the fit is extrapolated.
- **Malaya's battery fire, Southampton, Black Prince and Good Hope** in Campbell would test §5's verdict.

## Sources

- Burn rates: [DSJ, double-base 3.4-6.9 MPa](https://publications.drdo.gov.in/ojs/index.php/dsj/article/download/6273/3351); [DTIC ADA261009, nitrocellulose strands](https://apps.dtic.mil/sti/pdfs/ADA261009.pdf); [DTIC AD0763786, Kubota, double-base 0.1-10 MPa](https://apps.dtic.mil/sti/pdfs/AD0763786.pdf); [DTIC AD0768195](https://apps.dtic.mil/sti/pdfs/AD0768195.pdf); [DTIC ADA492240, ARE 1951 closed vessel](https://apps.dtic.mil/sti/pdfs/ADA492240.pdf); [Trębiński et al., Energies 2022, black powder](https://www.mdpi.com/1996-1073/15/7/2680); [DTIC ADA359800](https://apps.dtic.mil/sti/pdfs/ADA359800.pdf); [Leciejewski, CEJEM 2008](https://yadda.icm.edu.pl/baztech/element/bwmeta1.element.baztech-article-BAT1-0034-0043/c/httpwww_wydawnictwa_ipo_waw_plcejem1-2008leciejewski.pdf)
- Propellants and packaging: [NavWeaps tech-100](https://navweaps.com/index_tech/tech-100.php); [NavWeaps tech-007 (Okun)](https://navweaps.com/index_tech/tech-007.php); [NavWeaps 28 cm SK L/50](https://navweaps.com/Weapons/WNGER_11-50_skc09.php); [Naval Gazing, Powder Part 3](https://www.navalgazing.net/Powder-Part-3), [Part 4](https://navalgazing.net/Powder-Part-4); [Naval Gazing, "Something Wrong With Our Bloody Ships"](https://www.navalgazing.net/There-Seems-To-Be-Something-Wrong-With-Our-Bloody-Ships-Today); [Wikipedia, Cordite](https://en.wikipedia.org/wiki/Cordite), [Poudre B](https://en.wikipedia.org/wiki/Poudre_B), [Brown powder](https://en.wikipedia.org/wiki/Brown_powder)
- Turrets: NavWeaps gun pages ([15″ Mk I](https://navweaps.com/Weapons/WNBR_15-42_mk1.php), [12″ Mk X](https://navweaps.com/Weapons/WNBR_12-45_mk10.php), [13.5″](https://navweaps.com/Weapons/WNBR_135-45_mk5.php), [14″ KGV](https://navweaps.com/Weapons/WNBR_14-45_mk7.php), [38 cm](https://navweaps.com/Weapons/WNGER_15-45_skc13.php), [30.5 cm](https://navweaps.com/Weapons/WNGER_12-50_skc12.php), [US 14″/45](https://navweaps.com/Weapons/WNUS_14-45_mk1.php), [US 16″/45](https://navweaps.com/Weapons/WNUS_16-45_mk1.php), and others listed in the ref); [NavWeaps tech-118 (holding down)](https://navweaps.com/index_tech/tech-118.php); [warships1 forum, barbette diameters](https://www.tapatalk.com/groups/warships1discussionboards/barbette-diameter-question-t4202.html); [IMarEST, Yamato](https://library.imarest.org/record/7790/files/v01b3p05a.pdf); [kbismarck.com](https://kbismarck.com/armament.html); [Dredge, *Works of Schneider* (1900)](https://tekniskkulturarv.dk/book/2a25bfc0-ca8c-42e4-9313-1506afd41523?page=582)
- Doors and practice: [Brown, "Surface Fleets of WWI pt II", J. Naval Eng. 1989](https://library.imarest.org/record/8624/files/v32b1p19a.pdf); [TNA ADM 1/8477/308](https://www.nationalarchives.gov.uk/education/resources/jutland-death-sea/source-4/); [Sims to Daniels, April 1917](https://www.history.navy.mil/research/publications/documentary-histories/wwi/april-1917/rear-admiral-william-17.html); [warshipprojects](https://warshipprojects.com/2017/09/25/washington-cherrytrees-2-ii/); [NavWeaps tech-122](https://navweaps.com/index_tech/tech-122.php); [weaponsandwarfare, Dogger Bank conclusions](https://weaponsandwarfare.com/?p=20103); [Castles of Steel ch. 23](https://erenow.org/ww/castles-of-steel/23.php); [NavWeaps 21 cm](https://navweaps.com/Weapons/WNGER_827-45_skc05.php); [NavWeaps US 12″/40](https://navweaps.com/Weapons/WNUS_12-40_mk3.php); [eugeneleeslover, NavPers 10797-A](https://www.eugeneleeslover.com/USNAVY/CHAPTER-7-A.html); [amuedge](https://amuedge.com/maritime-risk-and-safety-battle-of-jutland-exposed-flaws-in-british-naval-procedures/); [jutland1916](https://www.jutland1916.com/tactics-and-technologies-4/battleship-design-and-anti-flash); [navygeneralboard, Lützow](https://www.navygeneralboard.com/?p=1083); [greatwarforum, Bulwark](https://www.greatwarforum.org/topic/13957-hms-bulwark/)
- Flooding: [Wikipedia, Heidkamp](https://en.wikipedia.org/wiki/Wilhelm_Heidkamp); [Seydlitz](https://en.wikipedia.org/wiki/SMS_Seydlitz); [Lion](https://en.wikipedia.org/wiki/HMS_Lion_(1910)); [greatwarforum, CPO Grant](https://www.greatwarforum.org/topic/240010-cpo-grant-hms-lion/); [devonheritage, Lion](https://www.devonheritage.org/Places/DevonCounty/JutlandHMSLion.htm); [USNI 1925, lessons of Jutland](https://www.usni.org/magazines/proceedings/1925/april/lessons-jutland-affecting-design-turret-armor); [König](https://en.wikipedia.org/wiki/SMS_K%C3%B6nig); [Glatton](https://en.wikipedia.org/wiki/HMS_Glatton_(1914)); [Iéna](https://en.wikipedia.org/wiki/French_battleship_I%C3%A9na); [Liberté](https://en.wikipedia.org/wiki/French_battleship_Libert%C3%A9); [dawlishchronicles](https://dawlishchronicles.com/ina-and-libert-disasters-1907-and-1911/); [Imperatritsa Mariya](https://en.wikipedia.org/wiki/Russian_battleship_Imperatritsa_Mariya); [Oslyabya](https://en.wikipedia.org/wiki/Russian_battleship_Oslyabya); [dreadnoughtproject, Colossus protection](https://www.dreadnoughtproject.org/tech/colossus/protection2.php); [eugeneleeslover, NC magazines](https://www.eugeneleeslover.com/US-NAVY-BOOKS/POWDER-MAGAZINE-NORTH-CAROLINA.php); [UNT, C&R Bulletin 5 (1932)](https://mocat.library.unt.edu/catalog/453-ch740); [WDR 24, Boise](https://ibiblio.org/hyperwar/USN/WarDamageReports/WarDamageReportCL47/WarDamageReportCL47.html); [WDR 29, Savo cruisers](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-quincy-ca39-astoria-ca34-vincennes-ca44-war-damage-report-no29.html); [FAS, magazine sprinklers](https://man.fas.org/dod-101/navy/docs/swos/gunno/INFO21.html); [firesafetysearch](https://www.firesafetysearch.com/sprinklers-in-ammunition-magazines-on-naval-vessels/)
- Open guns and passages: [Kent](https://en.wikipedia.org/wiki/HMS_Kent_(1901)); [royalmarineshistory, Falklands](https://royalmarineshistory.com/post/2019/12/09/the-battle-of-the-falklands-8th-december-1914); [Defence](https://en.wikipedia.org/wiki/HMS_Defence_(1907)); [mastermariners, Malaya](https://www.mastermariners.org.au/stories-from-the-past/4442-hms-malaya-at-jutland); [Chester](https://en.wikipedia.org/wiki/HMS_Chester_(1915)); [Matsushima](https://en.wikipedia.org/wiki/Japanese_cruiser_Matsushima); [Borodino](https://en.wikipedia.org/wiki/Russian_battleship_Borodino); [Missouri (Cox)](https://en.wikipedia.org/wiki/Robert_Edward_Cox); [Kearsarge (Breeman)](https://en.wikipedia.org/wiki/George_Breeman); [maritimequest, turret explosions](https://maritimequest.com/daily_event_archive/2015/04_apr/13_turret_explosions.htm); [teignheritage, Monmouth](https://www.teignheritageworldwar.org.uk/index.php/hms-monmouth)
- Project docs: [07](07-magazine-explosions.md), [02](02-components.md) §1, [docs/battle/magazines.md](../../../docs/battle/magazines.md)
