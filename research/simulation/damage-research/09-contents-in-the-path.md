# 09 — Contents in a Shell's Path: Machinery, Liquids, Coal, Magazines, Turrets, Shafts
Status: draft    Updated: 2026-10-19    Request: [REQUESTS.md "What inside a ship stops or slows a shell, 1890-1925 first"](../../REQUESTS.md) (R1-R6 in [docs/battle/shell-contents.md](../../../docs/battle/shell-contents.md))

*What a shell meets inside a ship's spaces, as a few numbers per kind of space that the plate walk in `Terminal` can take. It is built for a game: one general rule with data by room kind, not a model of every boiler. Where a source gave a number it is cited. Most numbers are engineering estimates from typical dimensions, and are tagged.*

Tags as in the other damage docs: **[INFERRED]** is my reasoning or a fit, **[EST]** is an engineering estimate from typical dimensions with no direct source, **[UNCERTAIN]** marks thin or conflicting sources.

**Companion code:** [`contents_ref.py`](contents_ref.py) (pure Python, instant). Every derived number below (decay lengths, coal equivalence, mean chords, obliquity draws, meet chances, shaft diameters) comes out of it.

---

## Summary

1. **Two kinds of content, two rules.**
   - **Media** (liquids, coal, boiler and condenser tube nests, turbine blading, stacked charges) slow the shell continuously as **v·e^(−x/L)**, the same law as `WaterRun`. L in calibres is a constant over the medium's effective density: **L = C/ρ**.
     - Liquids: **C ≈ 25,600** (the sea's 25 calibres × 1,025). The liquid inside the hull is the same physics as the sea outside.
     - Granular and distributed solids: **C ≈ 96,000** (120 calibres at coal's 800 kg/m³). Fitted to "2 ft of coal ≈ 1 in of armour".
   - **Solids** (drums, rotors, cylinders, crankshafts, gears, guns, stowed shells, shafts, rudder heads) are met at random: a line crossing L m of the solids' height band meets one with chance **1 − e^(−L/λ)**, where **λ = chord / f**. The struck solid is then **one plate** through the existing plate model: a thickness with a spread, a material, and an obliquity drawn for its shape.
2. **Most solids are round**, so the obliquity comes from the shape. For a cylinder struck across its axis, **sin θ is uniform** (mean 33°, 9.5% beyond the 65° glance). For a sphere or drum end, **sin²θ is uniform** (mean 45°, 18% glance). For round parts, draw the impact offset b and take both the obliquity and the steel chord from it (§1.3).
3. **Machinery stops shells; tube nests mostly don't.** The things that burst or stop a big AP shell are the turbine rotors, gearing, crankshafts and the heavy cast-iron cylinders. The equivalent thicknesses run **100-400 mm**. Boiler tube banks and condensers slow a shell by **5-25%**, and a full cylindrical boiler by about **25%** for a 6".
4. **Coal works mostly against splinters.** 2 ft of coal costs a 6" shell about 3% of its speed and a 15" about 1.3%, roughly 0.9-1.4 in of steel. A tumbling splinter is stopped within a metre. That is the period's view: valuable against fragments, small against shells.
5. **Liquids are a strong slowing layer** at the sea's rate: 3 m of oil keeps a 12" shell at 69% of its speed, and a 4 m liquid TDS layer keeps a 15" at 66%. Fill is a level from the ship's state.
6. **Guns are the hardest thing aboard.** A 12" gun at its slide presents a mean of about **475 mm of forged steel** (1.55 calibres). In a gun house the guns, slides and breeches fill about a third of the volume in the gun band.
7. **Fuze:** a struck solid starts a base fuze exactly as a plate does (this replaces `MachineryArmChance`). Media never start one: they always sit behind a plate that already did, and the sea's entry rule stays as it is.

---

## 1. The model

### 1.1 Media

The decay over a run x through a medium of effective density ρ:

- **v(x) = v₀·e^(−x/L)**, with **L = (C/ρ) × calibre**.
- **Liquids:** `C_liquid = UnderwaterDecayCalibres × 1,025` (25,600), or the diving value × 1,025 (51,200) for a diving shell. A shell in liquid cavitates and tumbles whether the liquid is the sea or a tank. That makes "the sea outside slows a shell, the same water inside doesn't" go away with one rule.
- **Granular and distributed solids:** `C_granular ≈ 96,000` **[INFERRED]**.
  - A shell keeps its attitude in coal (no cavity), so its drag is close to a stable shell's in water. A stable shell's e-fold length is 169 calibres in water (USNI 1921: 6", 3,000 → 148 ft/s over 255 ft).
  - Scaled by density, that is about 215 calibres at 800 kg/m³. The coal's crushing strength and the yaw from the plate before it bring it down.
  - 120 calibres at 800 matches "2 ft of coal ≈ 1 in" for 6-12" shells (§4).
- Tube nests, blading and charges are "granular" with their **effective** density: the metal and water per cubic metre of the nest (§2.2).
- **How full:** a medium fills its space **from the floor up to a level**: fill share × the space's height, the free surface flat. That is right for oil, water and coal (which settles). A path above the level crosses empty space.

### 1.2 Solids

Each room kind has a **band** (the share of the room's height, from the floor, where its contents sit), a **fill f** (the share of the band's volume the solids take), and a **mean chord** (Cauchy: 4V/S of a typical object).

- **λ = chord / f.** The chance a path meets a solid over L m inside the band is **1 − e^(−L/λ)**. It depends on the length crossed, not on how the room is cut into cells (the request's requirement).
- What is met is drawn from the room kind's **mix**: object types with shares. Each object has:
  - a **skin** (a plate: t, spread, material, shape),
  - an optional **fill** (a medium over a share of the object's chord),
  - an optional **core** (a second plate, met with a chance).
  
  One structure covers a boiler (casing, then tubes and water, sometimes a drum), a turbine (casing, blading, rotor) and a stowed shell (skin only).
- **Through it, it walks on:** the next solid comes after another draw of λ. For shells in a stack, use the in-stack λ in §5.

### 1.3 Round solids: thickness and obliquity together

For a tube of outer radius R and bore r, hit across its axis at offset b (b uniform on 0..R):

- Steel chord **s(b) = 2(√(R²−b²) − √(r²−b²))** for b < r, and **2√(R²−b²)** for b ≥ r.
- Obliquity at the outer face: **θ = asin(b/R)**.

That gives one draw for both, and the mean steel chord is (π/4)(OD² − ID²)/OD. Used for guns, shafts, crankshafts, rudder stocks, drums and stowed shells. A shell striking a round thing obliquely glances (the existing P5 glance from 65°) or turns (the existing turn): **no new turning rule is needed** (open question 2).

| Round solid | Mean steel chord |
|---|---|
| 12" gun at the slide (OD 2.4 cal) | 475 mm |
| 12" gun at the chase (OD 1.6 cal) | 234 mm |
| 15" AP shell (cavity 0.3 cal) | 272 mm |
| 15" HE / common (cavity 0.55 cal) | 209 mm |
| Hollow shaft 400 mm, bore 0.45 | 251 mm |
| Solid crankpin 450 mm | 353 mm |

### 1.4 Materials

Contents need two qualities that the material table does not have yet [EST]:

- **Cast iron** (engine cylinders, early turbine casings, steering-engine cylinders): **Q ≈ 0.35**. It is brittle; the chilled cast iron of the 1870s was poor against steel shot.
- **Forged steel** (rotors, crankshafts, gears, guns, shafts, rudder stocks, shell bodies): use the existing **"high-tensile steel" (Q 0.80)**.
- **Mild steel** (casings, boiler shells, drums, hoists): the existing **"mild steel" (Q 0.70)**.

### 1.5 What a strike does

- **Damage:** it damages the object struck: the room's node or the part. This replaces the `RoomVital` roll for these kinds.
- **Fuze:** a solid starts a base fuze as a plate does, with the same 0.07-calibre threshold on its equivalent thickness. This replaces `MachineryArmChance`. A medium does not start one.
- **Burst:** with C0's "the fuze runs on time", every slowing brings the burst in, so a shell slowed by a boiler bursts in or near the boiler room. That is what the destroyer test case was missing.

---

## 2. R1 Machinery

### 2.1 Boiler types and engine types by period

Used to pick the variant. These are broad lines, not class lists **[INFERRED from the class histories; UNCERTAIN in the details]**.

| Variant | Who and when |
|---|---|
| **Cylindrical** (Scotch / return-tube) | All navies' capital ships and cruisers to about 1895-1900 (USN Maine and Kearsarge era, RN to the Majestics). Merchants throughout. |
| **Large-tube** (Belleville, Niclausse, Babcock & Wilcox, Dürr) | France and Russia from the 1890s. RN 1897-1905 (Canopus to the King Edwards). USN B&W from about 1900. RN dreadnoughts with B&W (Dreadnought). |
| **Small-tube** (Yarrow, Thornycroft, Normand, Schulz-Thornycroft, White-Forster, Kampon) | All destroyers and torpedo boats. German capital ships from about 1900. RN dreadnoughts with Yarrow. Most new ships after about 1910. |
| **VTE / quadruple expansion** | Everything to about 1906. Then German capital ships to Helgoland (1909), several USN battleships to about 1916, and many cruisers and merchants. |
| **Direct-drive turbines** | RN destroyers from about 1907 and capital ships from 1906. Germany from Von der Tann and Kaiser. Most navies by 1910-1914. |
| **Geared turbines** | Some destroyers from 1916-1918, cruisers late in WWI, Hood (1920). Standard in the 1920s. |
| **Diesel** | Submarines throughout; surface ships after 1925 (out of scope). |

### 2.2 Boiler rooms

Band, fill and chord come from typical arrangements: boilers in rows with a stokehold (firing aisle) between. They fill most of the room's height, with uptakes above. Mean chords from contents_ref: a Scotch boiler of 4.5 m dia × 3.2 m is 2.64 m; a small-tube boiler of 3 × 3.5 × 3 m is 2.10 m (a destroyer's 1.76 m).

| Variant | Band | f | Chord | λ | Skin | Fill (medium) | Core |
|---|---|---|---|---|---|---|---|
| Cylindrical | 0-0.85 | 0.55 | 2.6 m | 4.7 m | Shell plates 2 × 22-38 mm = **60 mm** (45-75), mild steel, cylinder | Water + fire tubes, **ρ 1,250**, over 0.75 of the chord (the steam space above is empty) | Furnace / combustion chamber, 2 × 15-20 mm = **35 mm** (25-45), chance 0.4 |
| Large-tube | 0-0.85 | 0.50 | 2.0 m | 4.0 m | Casing and brick, **10 mm** steel-equivalent, flat | Tube elements, **ρ 1,100**, over 0.5 of the chord (the furnace below is empty) | Steam collector or drum, **30 mm** (20-40), chance 0.15, cylinder |
| Small-tube, large ship | 0-0.80 | 0.55 | 2.1 m | 3.8 m | Casing **8 mm**, flat | Tube banks, **ρ 1,700**, over 0.4 of the chord | Steam drum (1.0-1.4 m dia, 20-30 mm wall, half water), **50 mm** (40-70), chance 0.2; water drums **35 mm**, chance 0.1 |
| Small-tube, destroyer | 0-0.80 | 0.55 | 1.7 m | 3.1 m | Casing **6 mm** | Tube banks, **ρ 1,700**, over 0.45 of the chord | Steam drum **35 mm** (25-50), chance 0.2 |

Effective densities [EST]:

- **Small-tube bank:** tubes of 1-1.75 in OD on about 1.25 OD pitch, wall 3-3.5 mm. That is about 17% steel and about 40% water by volume: **1,500-1,900 kg/m³** when full.
- **Large-tube elements:** 4-5 in tubes, wider spacing: **900-1,300**.
- **Scotch boiler below the water line:** water, fire tubes (2.5-3.5 in, about 6 mm walls) and stays: **1,100-1,450**.
- An **empty or steaming-down** boiler has fill × 0.3 (steam, no water).

What the media do (from contents_ref, pointed shell):

| Case | Speed kept |
|---|---|
| 6" through a Scotch boiler's 3.4 m interior | 75% (plus 60 mm of plate) |
| 6" through two small-tube banks, 1.2 m | 87% |
| 15" through two small-tube banks, 1.2 m | 95% |

So a water-tube boiler mostly **arms** a big shell (skin and drum) and rarely stops it. A Scotch boiler is a serious stopper for a 6", which fits 1890s practice of putting boilers and bunkers behind thin sides.

### 2.3 Engine rooms

| Variant | Band | f | Chord | λ | Mix (share: what's struck) |
|---|---|---|---|---|---|
| **VTE** (large ship) | 0-0.90 | 0.35 | 1.6 m | 4.6 m | 35%: cylinders, cast iron 2 × 45-65 mm = **110 mm** (80-150), cylinder; core piston/rod, forged **150 mm**, chance 0.3. 25%: columns, rods, valve gear, **40 mm** (20-80), HT, cylinder. 20%: crankshaft and bedplate, round solid **D 400-500 mm** (cruisers 300-400, destroyers and TBs 150-220), forged. 20%: condensers and pumps, skin 12 mm + tube nest **ρ 1,100** over 0.85 |
| **Direct turbines** (large ship) | 0-0.55 | 0.45 | 1.4 m | 3.1 m | 55%: turbine, casing **70 mm** (2 × 30-45, cast iron before about 1912, then cast steel), blading **ρ 2,000** over 0.25 of the chord, core rotor drum **140 mm** (2 × 50-90) forged, chance 0.8. 35%: condenser, 12 mm + **ρ 1,100**. 10%: thrust blocks, shafts, pumps, **150 mm** forged |
| **Direct turbines** (destroyer) | 0-0.55 | 0.45 | 1.0 m | 2.2 m | As above with casing **40 mm**, rotor **80 mm** |
| **Geared turbines** | 0-0.60 | 0.45 | 1.3 m | 2.9 m | 45% turbine (as above). 15% gearing: wheel and pinions, **250 mm** (150-400) forged, cylinder. 30% condenser. 10% other |
| **Auxiliary machinery** (dynamo, hydraulic, pump rooms) | 0-0.50 | 0.30 | 0.8 m | 2.7 m | **40 mm** (20-80), cast iron / mild steel |

Meet chances for a full crossing at a random height (band × (1 − e^(−L/λ))):

| Case | Chance of meeting a solid |
|---|---|
| Destroyer turbine room, 8.5 m beam | **0.53** |
| Destroyer small-tube boiler room | **0.75** |
| Battleship VTE wing engine room, 9 m | **0.77** |
| Battleship Scotch boiler room, 20 m | **0.84** |
| Plunging hit through a destroyer turbine room (Johnston-like) | **0.64** per hit |

For comparison, today's `MachineryArmChance` is 0.45 per cell. That is the right order, but the new rule ties the arming to the crossing length and puts the burst on the thing struck.

### 2.4 Anchors

| Case | What it shows |
|---|---|
| **WDR 51** (USN, on Samar) | AP "normally pass[es] all the way through a lightly built ship unless a heavy unit of machinery… is encountered" (in [08 §0](08-gunfire-effects.md)). The 8.5 m destroyer crossing above gives about 47% pass-through for a machinery-room hit, and less for plunging fire. |
| **Johnston, Samar 1944** | All three heavy shells "detonated after impact with the after reduction gear, the after H.P. turbine and No. 4 boiler" (WDR 51 via [08 §3](08-gunfire-effects.md)): gear, rotor and drum, the three cores above. |
| **Blücher, Dogger Bank 1915** | Heavy shells "pierced her decks and exploded below in bunkers, stoke holds and engine rooms" (USNI 1925). Plunging hits bursting in coal-era machinery spaces. |
| **Tiger, Jutland** | A shell "punched a hole through the bracket which held the main steam pipe at the forward bulkhead of the port engine room" (USNI 1925). A struck light solid: damage without a stop. |
| **Warrior, Jutland** | At least 15 × 28 cm and 6 × 15 cm hits. The engine room crew, "of whom only three survived", kept the engines going (Wikipedia). |
| **Scharnhorst 1943 / Dunkerque 1940** | A 14" through the upper belt burst in No. 1 boiler room; a 15" through belt and torpedo bulkhead burst in boiler room 2 ([08 §3](08-gunfire-effects.md)). With a base fuze started at the belt, the burst lands in the boiler room. |

---

## 3. R2 Liquids inside the hull

- **Decay:** L = 25 calibres × 1,025/ρ, the sea's rule scaled by density.

  | Liquid | ρ (kg/m³) | L (calibres) |
  |---|---|---|
  | Sea water / flooded cell | 1,025 | 25 |
  | Fresh, feed or reserve feed water | 1,000 | 25.6 |
  | Fuel oil, 1910-25 (Admiralty furnace oil 0.92-0.97, heavier oils to 0.99) | 950 | 27 |
  | Diesel / light oil | 850 | 30 |

  Diving shells take the diving value (50 calibres × 1,025/ρ). Differences between liquids are small; density is enough.
- **Why the same as the sea:** a pointed shell in any liquid runs in a cavity and tumbles within a few metres. That is the physics the repo's 25 calibres folds in (fitted to 30-50% of velocity a few metres down, and Tosa). Inside the hull the shell enters through a plate, which yaws it further. A stable shell would decay over about 170 calibres (USNI 1921), so **25 is the tumbling value** and the right default. See open question A at the end.
- **Fill:** a level. Fuel tanks and liquid TDS layers take their fill from the fuel state and the design's TDS loading. The double bottom comes from the design (void, feed water or oil). Flooded cells take the flooding model's water.
  - Oil tanks compensated with sea water (a later practice) stay full and change density as oil is burnt.
- **Fuze:** a liquid does not start a base fuze inside the hull (it is always behind a plate). An already-started fuze runs on time, so a liquid layer brings the burst **into** it or just short of the next bulkhead.
  - The Japanese diving-shell work (Tosa 1924: 25 m under water, 76 mm, burst in the engine room; [armor/05 §6](../armor/05-obliquity-multilayer.md)) and the abnormally long delay observed there both argue for this.
- **What it does:**

  | Case | Speed kept |
  |---|---|
  | 3 m of oil, 12" shell | **69%** |
  | 4 m liquid TDS layer, 15" shell | **66%** |
  | Two double-bottom layers of about 1.2 m each, any heavy shell | about **90%** |

  This makes liquid-loaded side protection a real, emergent defence against diving shells. That is why the USN and Japan adopted it [INFERRED].
- **Trials:** besides Tosa and USNI 1921, I found no trial of shells through liquid-filled ship compartments before 1925 **[UNCERTAIN]**. Hydrodynamic ram (a shell in a full tank bursting its walls) is real but belongs to the flooding model, later.

---

## 4. R3 Coal

- **Density:** bunkered coal stowed at 40-48 ft³/ton is **750-900 kg/m³**. Use **800** (as [armor/03](../armor/03-materials.md)).
- **"2 ft ≈ 1 in":** with L = 120 calibres the model gives, for 2 ft at 500 m/s (DeMarre for the steel side):

  | Shell | Speed lost | ≈ mild steel |
  |---|---|---|
  | 4" | 4.9% | 0.76 in |
  | 6" | 3.3% | 0.87 in |
  | 8" | 2.5% | 1.02 in |
  | 12" | 1.7% | 1.18 in |
  | 15" | 1.3% | 1.41 in |

  So the rule holds across calibres within its own precision, slightly in favour of coal against big shells. Use **L = 120 calibres (range 100-190)**. The upper end is the stable-shell bound; nothing found supports below 100.
- **Shells vs splinters:** they differ by shape, not by rule.
  - A shell keeps its nose forward, so coal costs it little.
  - A splinter tumbles from the start: its e-fold is about **15 of its own sizes** in coal [EST]. A 20 mm splinter is stopped within about 0.9 m (3 e-folds).
  - That is the sources' picture: coal is "quite effective at catching fragments" and "absorbed a great deal of energy when pulverized" (Naval Gazing, UW Protection 1), with low value against shells ([damage-model-research §3.3](../damage-model-research.md)).
  - In the game, splinters from a burst in or near a bunker should be stopped by the coal along their path, using the same medium rule at their own size.
- **How full in service:**
  - Bunkers typically started **about 60% full** by volume (Naval Gazing).
  - Design "normal" loads were about a third to a half of maximum stowage, e.g. Dreadnought 900 t normal against about 2,900 t maximum **[UNCERTAIN on the exact figures by class]**.
  - Ships coaled before sorties, and in battle the fill ranged from **about 0.9 just after coaling** down to **0.3-0.5 after a week at sea**. At the Falklands, Sturdee's battlecruisers were coaling when von Spee arrived.
  - Burn rates: about 100-150 t/day cruising and 300-500 t/day at full power for a capital ship [EST].
- **Which bunkers emptied first:** captains tried to keep the side bunkers at the waterline full for protection and burnt from the others first **[UNCERTAIN: practice, not a found rule]**. A doctrine knob: "protective bunkers last" (default on).
- **The level rule matters for coal:** a half-empty upper bunker protects its lower half only, so a hit at the waterline above the coal crosses air.

---

## 5. R4 Magazines and handling rooms

The repo has one `Magazine` kind and one `Handling` kind. Shells and charges are stowed in **separate layers**: a shell room and a charge magazine, one above the other, which one on top by navy and period **[UNCERTAIN]**. Model a magazine as two bands (or two kinds if the layout already splits them).

| Space | Band | Contents | Rule |
|---|---|---|---|
| **Shell room** | 0-0.45 (racks or bins to about 1.2-1.5 m; USN vertical stowage to a shell's height) | Stowed shells: **f 0.5**, chord 1.0 m, **λ ≈ 2 m** | A struck shell is a **round solid** (§1.3) of its calibre, forged. Mean chord: AP **0.7 d**, common/HE **0.55 d**. Through one, the next comes after **λ_in ≈ 1.4 d** while still in the stack (chance 0.6 of still being in it) |
| **Charge magazine** | 0-0.80 | Charges in silk, in cases or racks: medium **ρ 400-750 (550)** | L ≈ 175 calibres: negligible slowing. What matters is ignition, which belongs to the magazine model |
| **Handling room** | 0-0.80 | Hoists, cages, trays, trunk walls: **f 0.2**, chord 0.6 m, **λ 3 m** | Skin **20 mm** (10-35), mild steel, flat. In action, add rounds in transit: 1-2 shells per hoist as round solids |

- **Do stacked shells stop a shell?** Yes, by size: a struck 12" shell is about 210-270 mm of forged steel at a random obliquity. It stops anything below about 8" and breaks or deflects most heavy shells that strike it squarely.
  - The question that matters more is whether a struck HE or common shell with a sensitive filler (lyddite, Shimose) detonates. Leave that to the magazine model as a chance per struck filled shell [INFERRED].
- **Cases:**
  - Kent at the Falklands (1914): a shell burst in a 6" casemate and flash went down the hoist to the ammunition passage; Sergeant Mayes flooded it. Handling-path flash, not slowing (Wikipedia, HMS Kent).
  - Seydlitz at Dogger Bank: a shell "pierced the turret foundations and set off the exposed powder in the turret" (USNI 1925).
  - I found **no clean 1890-1925 case of a shell slowed or stopped by stowed shells**. The numbers rest on geometry **[UNCERTAIN]**.

---

## 6. R5 Turret and barbette interiors

Scaled by the mount's own gun calibre **d**, so one row serves every mount.

**Gun house:** band 0.15-0.85 of its height, **f 0.35**, chord 1.0 m, **λ 2.9 m**.

| Share | What's struck | Equivalent | Material, shape |
|---|---|---|---|
| 25% | Gun (at the slide and trunnions) | Round solid, OD **2.2-2.6 d**, bore d: mean **≈ 1.55 d** (12": 475 mm) | Forged, cylinder |
| 25% | Cradle, slide, trunnions, recoil cylinders | **0.35 d** (0.2-0.5 d) | Forged / cast steel, flat |
| 15% | Breech end and mechanism | **1.0 d** (0.8-1.2 d) | Forged, flat (struck from ahead) |
| 35% | Rammers, loading trays, cages, sights, platforms | **20 mm** (10-40) | Mild steel, flat |

- **Why a third of the gun band:** a twin 12" house of about 8.5 × 3 m face has two guns with their cradles, roughly 1.5 × 1.4 m each in section: about 17% of the face. Behind them, the breeches, rammers and trays take a similar share [EST].
- A shell through the face of a turret trained toward the firer travels nearly along the guns, so a strike there is often on the breech end or slide head-on. The shares above fold that in.
- **Barbette interior** (below the gun house):
  - band 0-1, **f 0.15**, chord 0.8 m, **λ ≈ 5 m**: central trunk walls, cages, hydraulic pipes and rails, **20 mm** (10-40), mild steel.
  - The **top 0.12** of the barbette (turntable, working chamber floor): **f 0.6**, **50 mm** (30-80).
  - The roller path belongs to the barbette part's geometry, not the contents.
- **Cases** (USNI 1925 unless noted):
  - **Lion, Q turret:** the shell "exploded in the turret and put both guns out of action". The blast "had thrown back the breech operating lever of one of the loaded guns and unseated the projectile, which in turn pushed out the cordite powder bags into the gun well".
  - **Derfflinger:** "a heavy shell pierced the armor of her Caesar turret and exploded inside", setting off the exposed charges; Dora turret the same through the roof.
  - **Tiger, X turret:** "Both loading cages were damaged". **Malaya:** "One ammunition hoist cage was jammed". These are struck light contents.
  - **Seydlitz, turret 4:** a shell "perforated barbette armor … and ignited a charge".
  - Guns struck **outside** the house (the chase) are hits on the mount's exterior and are not part of this.

---

## 7. R6 Shafts and steering gear

### 7.1 Shafts

- **Diameter:** the cube-root rule **OD ≈ k·(P/N)^⅓** mm (P kW per shaft, N rpm at full power). Use k ≈ 115-130 for warships' hollow forged shafts, bore about 0.45 OD (k ≈ 100 for merchants' solid shafts) **[EST: the standard rule form; k not checked against drawings]**.

  | Type | OD (mm) | OD (in) |
  |---|---|---|
  | Pre-dreadnought VTE, 2 shafts at about 100 rpm | 410-460 | 16-18 |
  | Armoured cruiser VTE, 1905 | 470-530 | 18.5-21 |
  | Dreadnought, direct turbines, 4 at 320 rpm | 270-310 | 11-12 |
  | Battlecruiser (Lion), 4 at 275 rpm | 415-470 | 16.5-18.5 |
  | Light cruiser, 1914 | 265-300 | 10.5-12 |
  | Destroyer VTE (River) | 220-250 | 8.5-10 |
  | Destroyer direct turbines (M class) | 245-275 | 9.5-11 |
  | Hood, geared | 580-655 | 23-26 |

  Turbines' high rpm made shafts thinner than the slow reciprocating engines' for the same power.
- **Struck:** a round solid (§1.3), forged. A 400 mm hollow shaft gives a mean of **251 mm**. Shafts are parts (`ComponentKind.Shaft`), so the hit test is the part's own geometry. The shaft alley's other contents (plummer blocks, a walkway) are negligible: alley f ≈ 0.05, **30 mm**.
- **Cases:** I found no 1890-1925 shell hit recorded as cutting a shaft. Shaft losses in the cases are torpedoes and mines **[UNCERTAIN]**.

### 7.2 Steering gear

Room band 0-0.70, **f 0.30**, chord 0.8 m, **λ ≈ 2.7 m**.

| Share | What's struck | Equivalent |
|---|---|---|
| 25% | Rudder stock / head | Round solid: battleship 1900-25 **D 380-560 mm**, cruiser 250-400, destroyer 120-200 [EST]; forged |
| 35% | Tiller, quadrant, Harfield or screw gear | **120 mm** (60-200), forged, flat |
| 25% | Steering engine | Cast iron, **60 mm** (40-90), cylinder |
| 15% | Other (piping, telemotor) | **20 mm** |

- **Cases:** Warspite's jam at Jutland followed a hit near the wing engine room ([08 §3](08-gunfire-effects.md)). It was in the leads and gear, not the rudder head. In most period cases the steering is lost through leads, telemotor lines and the helm position, not the gear itself, so the service-network model matters more than the room's contents here [INFERRED].

---

## 8. Recommendations for this codebase

1. **One table, keyed by `RoomKind` (+ variant) and `ComponentKind`.** Each row holds:
   - band from/to, f, chord;
   - a mix of objects: share, skin {t, spread, material, shape}, fill {ρ, chord share, kind}, core {chance, t, material, shape};
   - or, for media rooms, the medium {ρ, kind} with its fill from the ship's state.

   The variants are the boiler type (cylindrical, large-tube, small-tube, destroyer small-tube) and the engine type (VTE, direct turbine, geared, auxiliary), read from the design's powerplant block.
2. **`Terminal` walk:** inside a room's band, draw a distance to the next solid as Exp(λ) on the hit's stream. If the next plate or cell exit comes first, carry the remaining distance on: memoryless, so this is cell-count independent and allocation-free. On a strike, draw the object from the mix and put its skin, then the fill (a medium run), then the core (with its chance) through the existing plate code.
   - The plate crossings start the fuze (replacing `MachineryArmChance`) and damage the node (replacing `RoomVital` for these kinds).
3. **Media:** generalise `WaterRun.DecayM` to take a density and a kind: `L = calibre × C_kind / ρ`. The sea stays exactly as it is (`C_liquid = UnderwaterDecayCalibres × 1,025`). Add `SimSettings.GranularDecayC = 96,000` and a splinter value (15 sizes).
4. **Fill state:** bunkers, fuel tanks and TDS liquid layers need a fill share per room from the ship's fuel state; flooded cells use the flooding model's water. Fill as a level (§1.1).
5. **Materials:** add "cast iron", Q ≈ 0.35 [EST]. Reuse "high-tensile steel" for forged contents and "mild steel" for casings and boiler plate.
6. **Round solids:** one helper for (b → steel chord, obliquity) (§1.3), used for guns, shafts, crankshafts, rudder stocks, drums and stowed shells. It answers open question 1. Flat objects take the incoming path's obliquity to the room's axes, or 0 for simplicity.
7. **Order:** this follows the doc's C1-C4.
   - Anchor C1 on the destroyer test: a 15" AP through a turbine room should burst inside about half the time (§2.3), and pass through the rest.
   - Anchor C2 on a 15" through a 4 m liquid layer: about 66% speed kept.
   - Anchor C3 on 2 ft of coal ≈ 1 in at 6-12".

**Open choices for Claude Code**

- **A. Liquids at the sea's 25 calibres, or at a stable shell's value** (about 170 calibres scaled by density) when the run inside is short? The note recommends 25 for one rule. The alternative makes liquid layers about 7× weaker against shells.
- **B. Contents' own splinters** (open question 4): leave for later. A struck solid's fragments would add to the burst's own; nothing here needs it.

---

## Sources

- In this repo:
  - [08-gunfire-effects.md](08-gunfire-effects.md): WDR 51 quote, Johnston, Hoel, Gambier Bay, Scharnhorst, Dunkerque, Warspite.
  - [armor/05-obliquity-multilayer.md §6](../armor/05-obliquity-multilayer.md): Tosa 1924 and USNI 1921 underwater trajectories.
  - [armor/03-materials.md](../armor/03-materials.md): COAL, the "2 ft ≈ 1 in" quote from the Dreadnought Project.
  - [damage-model-research.md §3.3](../damage-model-research.md) and [01-capital-subdivision.md](01-capital-subdivision.md): bunkers.
  - [02-components.md §6](02-components.md): machinery layout, Dreadnought's plant.
- [Lessons of Jutland Affecting Design: Turret Armor, USNI Proceedings, April 1925](https://www.usni.org/magazines/proceedings/1925/april/lessons-jutland-affecting-design-turret-armor): Lion Q, Derfflinger, Tiger, Malaya, Seydlitz, Blücher.
- [Naval Gazing, Underwater Protection Part 1](https://www.navalgazing.net/Underwater-Protection-Part-1): coal pulverised, fragments, 60% fill, voids outboard of coal.
- [USNI Proceedings 1921, Under-water trajectories](https://www.usni.org/magazines/proceedings/1921/february/under-water-trajectories): the stable decay.
- [Wikipedia, HMS Warrior (1905)](https://en.wikipedia.org/wiki/HMS_Warrior_(1905)): Jutland hits and engine room.
- [DANFS, Johnston (DD-557)](https://ibiblio.org/hyperwar/USN/ships/dafs/DD/dd557.html).
- [Wikipedia, Protected cruiser](https://en.wikipedia.org/wiki/Protected_cruiser): bunkers' protection lost with oil firing.
- Kent at the Falklands: Wikipedia, HMS Kent (1901).
- Not found and worth a library visit: the 1880s-1900s British and US coal-bunker firing trials; the RN's bunker-burning orders in action; machinery drawings for shaft and rotor dimensions to check the [EST] rows.
