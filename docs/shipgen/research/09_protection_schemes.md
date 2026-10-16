# 09 — Protection Schemes: what stands between the sea and the magazine (c. 1860–1950)

Research for the ship generator (2026-10-16). The question: today every generated ship has a main belt with the magazines and machinery straight behind it, and an unarmoured ship (a destroyer) has one layer of shell plating with the magazine straight behind that (TODO.md, "Small ships' magazine protection"). Real ships were never built like that. This doc sorts the historical schemes into families, describes the **layers from the outer shell inward** for each one, and ends with what the generator would need to reproduce them.

It builds on, and doesn't repeat, `01_capital_subdivision.md` §1.4 and §2–3 (belt, decks, TDS tables) and `03_small_vessels.md` §1–4 (cruiser and destroyer facts). Tags as in those docs: **[INFERRED]** for reasoning not taken straight from a source, **[UNCERTAIN]** for numbers seen only in weak sources.

---

## 0. Summary for the generator

1. **A protection scheme is a stack of layers, not a belt.** Every armoured design from about 1880 has at least two lines between the side and a magazine: an outer line (belt, or just the shell) and an inner line (a sloped deck, a torpedo/splinter bulkhead, a magazine box, or simple distance through bunkers, tanks and stores). The inner line is what turns "a shell got through the belt" into "it burst in a coal bunker" instead of "it burst in the magazine."
2. **Magazines are put where shells can't easily reach, before any armour is added**: low (crowns at or under the waterline, so a flat-trajectory shell has to pass through water or a deck to get there), inboard (behind wing spaces), and away from the side at the ends where the hull is narrow. Unarmoured ships rely on this alone. A destroyer's magazine sits on the hold level under its mount, below the waterline. It is not behind the side plating at gun height.
3. **Protection varied along the citadel.** Magazines usually got more than machinery: a thicker belt (KGV 374 vs 348 mm, Nelson 14 vs 13 in, Takao 127 vs 102 mm), a thicker deck (Belfast 3 in vs 2 in), a thicker holding bulkhead (Dunkerque 40–50 vs 30 mm). Treaty cruisers often skipped the belt at the magazines entirely and armoured them as **boxes**.
4. **There are about a dozen recognisable families** (§1–§5). Most are combinations of a small number of parts the generator already has or nearly has: belt (with inclination and position), upper belt, end belts, decks (flat, or flat plus slopes), bulkheads, TDS, plus four parts it lacks: **sloped deck edges, a splinter/holding bulkhead behind the belt, magazine boxes, and wing spaces on non-TDS ships**.
5. **Not all protection is plate.** Water above the magazine crown, coal in wing bunkers, cellulose cofferdams, flooding and sprinkling, and flash-tight handling rooms all did real work. Some belong in the generator (where things go), some in the damage model (what fire and flash do).

---

## 1. Ironclad and early steel era (c. 1860–1890)

The generator already ships Victorian designs (Duilio, Inflexible, Brennus, Bouvet), so these matter.

### 1.1 Full-length waterline belt + battery box ("central battery" / "box battery")
- **What:** a narrow belt along the whole waterline, plus an armoured box amidships around the guns (HMS *Bellerophon* 1865, *Hercules* 1868). The ends are unarmoured above the belt.
- **Layers at a magazine:** shell/belt (iron on thick teak backing) → hold. Magazines are low at the ends of the boiler rooms, under the waterline. Coal bunkers along the sides of the machinery.
- **Generator:** belt + `end_belts` reach 1 + an `upper_belt` over the battery stretch. Works today.

### 1.2 Breastwork monitor
- **What:** a low freeboard hull with a full-length belt and a raised armoured **breastwork** amidships enclosing the turret bases, funnel uptakes and hatches (HMS *Devastation* 1871). It is the ancestor of the barbette and the citadel.
- **Generator:** `hull.raised` from `fore_group` to `aft_group` + an `upper_belt` to that raised deck. Works today.

### 1.3 Central citadel with "raft" ends (citadel ship)
- **What:** armour concentrated in a short, very thick central citadel (HMS *Inflexible* 1876; Italian *Duilio*). The ends have no side armour at all. They get an **underwater protective deck**, with the space above it finely subdivided and packed with cork, coal and stores, so that the ends keep their buoyancy when riddled. The design argument was that the citadel alone would float the ship.
- **Layers at a magazine:** citadel side armour (very thick) → magazine inside the citadel. Beyond the citadel: unarmoured side → cork/coal cells → submerged protective deck → stores below.
- **Generator:** short citadel, `end_belts` 0, `ends` deck one or two decks down. The calibration note "Duilio and Inflexible +70 % heavy (long armoured middle)" suggests the generated citadel is too long for this scheme. A real citadel ship's citadel was only the machinery plus the turret bases (*Inflexible*: about 110 ft of a 320 ft hull).
- **Missing:** cork/cellular fill as a flooding-limiting layer (see §6.6).

### 1.4 French full-length narrow belt + cellular layer ("ceinture" + "tranche cellulaire")
- **What:** French practice into the 1890s: a narrow, very thick belt along the whole waterline, an armoured deck on its top edge, and above that deck a **cellular layer** (cofferdams and small cells, often cellulose-filled) a deck high along the sides, then a thin splinter deck. The cellular layer was meant to keep the waterline watertight once the unarmoured side above the belt was holed.
- **Layers at the waterline:** belt → magazine/machinery under the armoured deck. Above the belt: thin side → cellulose cofferdam → cells → interior.
- **Generator:** belt + `end_belts` reach 1 + two decks. The cellulose layer is missing (§6.6).

---

## 2. Pre-dreadnought and dreadnought era (c. 1890–1920): the two-line scheme

### 2.1 Protected deck behind the belt ("turtleback" or "belt + slopes")
This is **the** standard capital-ship scheme of 1890–1915, and the shape behind most later European ones.
- **What:** the main armour deck is flat a little above the waterline amidships, then **slopes down at the sides to meet the lower edge of the belt.** Coal bunkers fill the wing space above and inboard of the slopes.
- **Examples:** *Majestic* 9 in belt, 3 in flat / 4 in slopes; *Formidable* 2 in / 3 in; *Dreadnought* 1.75 in / 2.75 in; *Nassau* 38 / 58 mm (figures in `01_capital_subdivision.md` §2.2).
- **Layers at a magazine (side hit at the waterline):** belt → coal bunker → slope (at roughly 30–45° to the shell's path) → magazine. A shell that beats the belt usually bursts in the coal or has to beat the slope at a bad angle. A shell over the belt meets the flat deck at a glancing angle.
- **Why it worked:** the flat-trajectory fire of the time hit sides, not decks. The slope doubled the side protection at low weight. The price is a low armour deck: everything between it and the main deck (crew, secondary ammunition passages, communications) is outside the box.
- **Generator status:** turtleback decks were **left out on purpose** (DECISIONS 2026-10-03: "several flat armour decks are enough for now"). For the pre-dreadnought and dreadnought designs that means the second line simply isn't there, and the magazine sits behind the belt alone. §6.2 proposes a cheap version.

### 2.2 Incremental ("everything protected a little")
- **What:** a thick main belt + upper belt + casemate armour + thinner end belts + protective deck at the ends. Intent: keep HE and splinters out of the whole waterline and the battery, keep the ends buoyant (RN and German WWI practice).
- **Generator:** already supported (`dreadnought.json`, `nassau.json`, `kongo.json`).

### 2.3 German wing bunker + torpedo bulkhead
- **What:** German dreadnoughts added a **longitudinal torpedo bulkhead** (30–50 mm) running the length of the citadel some metres inboard of the side, from the double bottom up to the armour deck. The coal bunkers lie between it and the side. It is both the holding bulkhead against torpedoes and **the backing line behind the belt** against shells and splinters. RN *Dreadnought* had such bulkheads only abreast the magazines.
- **Layers at a magazine:** belt → coal bunker → slope (the slope's foot meets the bulkhead) → 30–50 mm torpedo bulkhead → magazine.
- **Generator:** `armour.tds_m` gives the depth, and the TDS is weighed at 12 mm per metre of depth, but it isn't an armour piece in the hitboxes for shell paths. §6.3.

### 2.4 Battlecruiser
- The same layering as the dreadnought of its navy with a thinner belt (*Invincible* 6 in). The Jutland losses were mostly **flash** going down an unprotected handling chain (cordite stacked in handling rooms, flash doors removed), not shells reaching the magazine through the side (see the Naval Gazing piece in the sources). For the generator that is a damage-model point (§6.7), not a layout one.

### 2.5 All or nothing + raft body (US Standard type, 1912 →)
- **What:** no medium armour. A very thick belt over the citadel, a thick flat deck **on top of the belt**, heavy bulkheads, and a citadel buoyant enough to float the ship with the ends flooded.
- **Layers at a magazine:** belt → **deep layered TDS** (from *Tennessee* on: four or five bulkheads, liquid and void layers) → magazine. Above: flat armour deck → (later) splinter deck → magazine crown.
- **Generator:** supported (`all_forward.json`, `battleship.json`), and the TDS gives the distance. This is the only family where "magazine behind the belt" is roughly right, and even here the magazine sits behind 5 m of TDS.

---

## 3. Treaty and WWII capital ships (1920–1950): the second line returns

Every navy settled on all or nothing for the citadel, then disagreed about the second line.

| Family | Ships | Outer line | Between | Inner line | Notes |
|---|---|---|---|---|---|
| **High flat deck, external vertical belt** | KGV, North Carolina | vertical belt to the armour deck | TDS | holding bulkhead; splinter deck under the armour deck | the biggest protected volume; belt alone against flat fire |
| **Internal inclined belt** | Nelson (18°), South Dakota and Iowa (19°) | thin outer shell | — | inclined belt inside the hull, its lower part continuing as a TDS bulkhead down to the bottom | an inclined plate is thicker against plunging fire for its weight and shorter for the same coverage; outer shell in front must be repaired after hits |
| **External inclined belt** | Hood (12°), Dunkerque (11°), Richelieu (15°), Yamato (20°) | inclined belt in the side | TDS behind | splinter/lower deck with slopes (French); lower belt to the bottom (Yamato) | |
| **Turtleback low deck + upper belt** | Bismarck, Scharnhorst (and Hipper as a cruiser) | 145 mm upper belt + 50 mm upper deck (decapping, yaw) | 320 mm main belt | 80–120 mm deck with 22° slopes behind the belt; 30 mm splinter bulkheads | excellent close in; everything above the low deck is outside protection |
| **Spaced (decapping) belt** | Littorio | 70 mm outer plate | 250 mm gap (foam filled) | 280 mm inclined main belt; Pugliese drum below | the outer plate strips the cap, the main plate stops the uncapped shell |
| **Bomb deck + armour deck + splinter deck** | Iowa, North Carolina, KGV (as a thin weather deck) | 32–38 mm weather deck (arms fuzes, strips caps) | armour deck | 16 mm splinter deck | three decks: fuze, stop, catch |
| **Short concentrated citadel** | Nelson, Dunkerque, Richelieu (all guns forward) | | | | putting all turrets together shortens the citadel and lets the same weight buy a thicker belt |

Details for most of these are in `01_capital_subdivision.md` §2.1–2.3 and §3.2. New points for the generator:

- **Dunkerque** (Wikipedia): 225 mm belt inclined 11° on 60 mm teak, running from the forward magazine to the aft magazine; 115–125 mm upper deck on top of the belt, with a 15 mm steel deck beneath; a 40 mm lower deck **with slopes meeting the belt's lower edge** (50 mm on Strasbourg). The 30 mm holding bulkhead is **thickened to 40–50 mm beside the magazines**, and the TDS is ébonite-mousse-filled, about 7.5 m deep at the machinery. **The magazines' bottoms are raised above the double bottom and plated with 30 mm against mines.** Two decks, the lower one turtlebacked: an "upper deck stops, lower deck catches" scheme with slopes as the second line.
- **Nelson**: the 14 in / 13 in internal inclined belt is the same plate thickened over the magazines. Single heavy deck (6.25 in over the magazines, 3.75 in over machinery), 0.5 in weather deck. Nelson is also usually credited with putting the cordite **magazines below the shell rooms**, reversing earlier RN practice, so the cordite is the deepest and most protected thing in the ship. **[UNCERTAIN: commonly stated, not confirmed in a primary source in this session.]** US 16 in ships similarly kept projectiles high (on the shell decks in the barbette) and powder at the bottom.
- **Bismarck's splinter cells**: 30 mm longitudinal splinter bulkheads between the decks form 51 armoured cells (`01` §1.4). This is a second line *above* the armour deck that the generator has nothing like.

---

## 4. Cruisers (c. 1880–1950)

### 4.1 Protected cruiser: deck only
- **What:** no belt. A full-length armour deck, flat at or just above the waterline and **sloping below it at the sides**, slopes thicker than the flat (*Olympia* 2 in flat, 4.75 in slopes). Coal bunkers and a cellulose cofferdam line the side above the slopes (`03_small_vessels.md` §1.1).
- **Layers at a magazine:** thin side → cofferdam / coal → slope → magazine (under the deck, below the waterline).
- **Generator:** impossible today without slopes. A flat deck at the waterline with nothing on the side gives "magazine straight behind the shell" for a whole ship type that was designed precisely so that never happened.

### 4.2 Armoured cruiser: belt + slope
- The protected cruiser's turtleback deck with a waterline belt added outside it (*Cressy* 6 in). Layers: belt → coal → slope → magazine. Same generator gap as §2.1.

### 4.3 WWI light cruiser: belt as hull structure
- The belt is 1–3 in high-tensile steel that **is** the side shell over the machinery, counted in the hull girder (*Arethusa*, the C class), with a 1 in deck. Magazines low at the ends, often with just a thin deck over them.
- **Generator:** `hull.plating.shell_mm` (a thicker side shell) or a thin belt. Close enough.

### 4.4 Treaty cruiser: machinery belt + magazine boxes
Weight-starved 10,000 t cruisers stopped armouring the side over the magazines and gave each magazine group its own armoured **box** low in the hull instead.
- **Kent (County) class**, as designed (Wikipedia): **no belt at all** (1 in side plating), a 1.25 in deck over the machinery, and **box citadels around the magazines and shell rooms: 4 in sides, 2.5 in crowns, 2.5 in end bulkheads.** A 1.5 in arch over the steering gear. Turrets and barbettes 1 in. External bulges. A 4.5 in machinery belt was added in the mid-1930s refits, London got 3.5 in.
- **Leander**: belt over the machinery only; magazines in a **3 in box**; 1 in deck and turrets. The last three ships (Amphion/Perth) split the machinery into two units, and the belt was lengthened from 84 to 141 ft to cover both.
- **New Orleans**: belt shortened so it could be thicker over the machinery; magazines **boxed deep below the waterline with about 4 in sides** (`03` §2.2). Good against shells, but deep magazines are where torpedoes strike (Tassafaronga).
- **Brooklyn**: 5 in belt on 0.625 in STS at the machinery, **only 2 in at the magazines, which were deeper and boxed.**
- **Layers at a magazine (Kent):** 1 in side → wing space (bulge, tanks, stores) → **4 in box side** → magazine. Above: 1 in decks → **2.5 in box crown**.
- **Why:** a box around a compact low magazine has far less area than a belt plus deck covering the same stretch. The magazine lies below the waterline, so the box side only has to cover a shell that has travelled through water or come down through decks.

### 4.5 Treaty cruiser: long inclined belt
- Japanese heavy cruisers (Myoko, Takao, Mogami) ran an **inclined belt** over both machinery and magazines (Takao: 102 mm machinery, 127 mm magazines) whose lower part tapered down and continued as the torpedo bulkhead **[UNCERTAIN in detail: inclination and lower-edge taper from secondary sources]**. Zara (Italy) was the outlier, with a 150 mm belt and 70 mm deck at the cost of going far over the limit.

### 4.6 German cruiser turtleback
- Hipper: 80 mm belt, a 30 mm deck with sloped edges behind it, and a 30 mm upper deck (`03` §2.2). Bismarck's scheme in miniature.

### 4.7 Late WWII light/AA cruisers
- Belfast: 4.5 in belt, **3 in deck over the magazines vs 2 in over machinery**, 2.5 in bulkheads. Atlanta: belt 1.1–3.75 in, 1.25 in deck: an AA cruiser at the boundary with splinter protection.

---

## 5. Unarmoured ships: protection by arrangement

### 5.1 Destroyers and torpedo boats
Destroyers had no armour (`03` §4.1). They still weren't built with the magazine behind the shell at gun height. What protected them:

1. **Depth.** Magazines are on the lowest platform or hold level under the mounts they serve (the USN report's LINDSEY example: a 5 in powder magazine on the third platform level). Their crowns are around or below the waterline, with the handling room above. A flat-trajectory shell striking the side above the waterline travels down at only a few degrees and bursts in the berthing or handling spaces first; to reach the magazine it has to enter through the water or plunge through two or three decks. **[INFERRED from standard layout; the sources give the level, not a rule.]**
2. **Position along the hull.** Magazines at the ends, under the forward and after mounts, separated from the machinery block (`03` §4.3: forward magazines 0.05–0.25 L, after ones 0.65–0.85 L). The ends are narrow, so the magazine fills most of the beam, but it does so below the waterline.
3. **Things around them.** Refrigerated spaces, stores, and handling rooms between magazines and outer spaces (LINDSEY: magazines separated by refrigerated spaces). Not armour, but fragments lose energy in every bulkhead and filled space.
4. **Flooding and sprinkling.** "A propellant fire is unlikely to cause an explosion if the sprinkling system is run promptly. Rapid flooding from the sea through shell damage can also extinguish a fire" (USN destroyer report). KILLEN and STORMES had direct hits in a magazine or handling room; inrushing water put the fires out.
5. **Splinter plating on control and guns**, not the hull. The 1945 CNO policy: **30 lb STS (about 19 mm) around important ship and fire-control stations, 10 lb STS (about 6 mm) for other exposed personnel**; 25 lb (about 16 mm) is the least that is reasonably effective against close bursts. Gunhouses and bridges of the 1930s–40s are in this band. Some classes had a thicker side over the machinery (Fletcher is often quoted at 0.5–0.75 in) **[UNCERTAIN, see `03` §4.1]**.

What still killed them: in the USN's 30 destroyer losses to gunfire, bombs and kamikazes, 5 were magazine explosions, and of those only LONGSHAW blew up from a direct hit; CUSHING, MONSSEN, ABNER READ and MAHAN went **after fires reached the magazines**. 5 in AA rounds were the type most prone to mass detonation; 20 mm, 40 mm and 3 in tended to burn or go off singly. So for a destroyer, **the magazine's danger is mainly fire and flash, not a shell flying straight into it.** A generator that puts the magazine at gun height behind 6 mm of plate turns a rare event (direct penetration) into a common one, which is exactly what the 20 % lost anchor in TODO.md is papering over.

### 5.2 Escorts, sloops, armed merchants
- No hull armour. Plastic armour (asphalt and granite chips in steel forms) or concrete slabs around wheelhouses and gun positions; sandbags. Magazines low aft or forward, as on destroyers. The generator's merchant "plastic-armoured wheelhouse" via `control_mm` covers it.

### 5.3 Coastal forces
- Wooden or light-alloy hulls; protection is a little steel around the bridge and engines. Petrol is the main hazard (`03` §0.7). Out of scope here.

---

## 5A. Carriers (brief)

The generator builds carriers, and their schemes are distinct:
- **Hangar-deck armour, open hangar on top** (Essex): the hangar deck is the strength deck with about 3.5 in on it, the flight deck unarmoured superstructure. Belt over the machinery. Magazines and avgas low, under the hangar deck.
- **Armoured box hangar** (Illustrious): 3 in flight deck as the strength deck, 4.5 in hangar sides, hangar ends closed; magazines with 4.5 in-class side armour (180 lb) and 2.5–3 in decks (100–120 lb); a 14 ft deep side protection system. Implacable cut hangar sides to 1.5 in (60 lb). Costs aircraft numbers.
- **Both**: Midway added 2 in flight deck armour over Essex's 3.5 in hangar deck.
- **Avgas** was stored in tanks surrounded by water-filled or inerted spaces, since vapour, not the fuel itself, was what exploded **[INFERRED from general literature; not fetched this session]**.

---

## 6. What the generator needs

Grouped by part. Each lists the history it unlocks; nothing here is code yet.

### 6.1 Put magazines low and inboard on every ship (layout, cheap, biggest win)
- **Magazine crown at or below the waterline** whenever the draught allows, on every style: today the tops are only capped by the lowest armour deck, so an unarmoured ship's magazine can rise into the above-water band. Proposal: cap the magazine top at `WL − margin` (margin about 0.3–0.5 m, a tuning value); a magazine too big for the space under the waterline gets longer, not taller, and spills upward only with a warning. Handling rooms take the deck space above.
- **Wing space between shell and magazine on ships without a TDS**: a `wing` layer (say `armour.magazine_wing_m`, default from beam, about 0.08–0.12 B [INFERRED]) filled with stores, refrigerated rooms, voids or water. Not fuel oil directly against a magazine: real practice put a cofferdam between oil tanks and magazines (fire). The subdivision already makes wing cells; this just reserves them next to magazines.
- **Destroyer outcome:** a side hit at gun height meets berthing or handling spaces, then a deck, then the magazine crown, which is below the sea outside. Direct magazine penetration becomes rare, and the remaining risk goes through fire and flash (§6.7), as in the USN record.

### 6.2 Sloped deck edges (turtleback) — the pre-1915 second line
- Revisit the 2026-10-03 decision. A cheap version: a deck entry gains `slope_mm` (0 = flat) and `slope_deg` (about 25–45°). The flat part spans the inner beam less the slope's horizontal reach; the slope runs down to the belt's lower edge (or to `belt_depth_m` below the waterline on a beltless ship). Weight is slope area × thickness. In the hitboxes the slope is one more plate, inclined, so the ballistics see the angle without a new mechanism.
- **Unlocks:** protected cruisers (no other way to make one), armoured cruisers, every pre-dreadnought and dreadnought, Bismarck, Hipper, Richelieu and Dunkerque's lower deck.
- The roof of the vital spaces stays the flat part's height; under the slopes the machinery space narrows, as it did.

### 6.3 Belt inclination and position
- `belt_incline_deg` (0–25) and `belt_position: external | internal`. Inclined plate: thickness / cos(angle) against flat fire, more against plunging; weight per metre of height changes by 1/cos. Internal: the outer shell stays in front (a decapping/fuzing layer for free, and the space between floods), and the belt is shorter in height for the same coverage.
- **Unlocks:** Hood, Nelson, South Dakota/Iowa, Dunkerque, Richelieu, Yamato, Littorio, Japanese cruisers.

### 6.4 A real inner line behind the belt
- Make the TDS holding bulkhead (and an optional `splinter_bulkhead_mm`) an **armour piece in the hitboxes**, with its own thickness, from the inner bottom to the armour deck at `tds_m` inboard. Today it's weighed (12 mm per metre) but shells don't see it.
- Allow it **thicker beside the magazines** (Dunkerque 30 → 40–50 mm).
- Optional splinter bulkheads between decks above the armour deck (Bismarck's 30 mm cells).
- **Unlocks:** German WWI torpedo bulkheads as shell backing, Bismarck, Dunkerque; and a second line for any all-or-nothing ship.

### 6.5 Zones along the citadel and magazine boxes
- **Per-zone thickness**: `belt_mm` and deck `mm` may be given separately for the magazine and machinery stretches (`{"magazines": 374, "machinery": 348}`). The layout already knows where each is.
- **Magazine boxes**: `armour.magazine_box {side_mm, crown_mm, end_mm}`, an armoured box around each magazine group (and the shell rooms with it), independent of the belt and usable on any style, a destroyer included. Area is the magazine's own, so it is cheap for a compact low magazine. A box under a belt adds up with it (Brooklyn: 2 in belt plus the box).
- **Belt extent**: let the belt cover the machinery only (`belt.extent: "machinery" | "citadel"`), so the treaty-cruiser scheme is "machinery belt + boxes" (Kent after refit, Leander, New Orleans).
- **Magazine floor plate** (`magazine_box.floor_mm`): Dunkerque's 30 mm against mines; Yamato also armoured under its magazines.
- **Unlocks:** every treaty cruiser, and an "up-armoured destroyer" option for players who want to pay for it.

### 6.6 Non-plate layers (subdivision + damage model)
- **Coal as protection**: wing coal bunkers already exist (`machinery.bunkers: wing`). Give them a fill state in the hitboxes so the damage model can treat coal as a fragment- and blast-absorbing medium that thins as it is burnt (`01` §1.4, `03` §1.1).
- **Cellulose / cork / foam cells**: a cell fill `cellulose` (or `foam`: Littorio's Cellulite, Richelieu's ébonite mousse) that limits flooding through holes in it. Unlocks the French cellular layer, protected cruisers' cofferdams, Inflexible's raft ends.
- **Water outside the magazine**: free once §6.1 puts crowns under the waterline; the damage model already handles shells entering water.

### 6.7 The ammunition chain (damage model, not plate)
- **Order in the magazine stack**: a flag for propellant below shells (Nelson-style) vs shells below propellant (earlier RN practice). It decides which one a deep hit or a fire reaches first.
- **Flash-tight handling rooms**: the handling room between the turret and the magazine is a flash barrier. A navy (or a doctrine slider) that keeps the doors open for rate of fire gets faster loading and the Jutland failure mode.
- **Flooding and sprinkling** per magazine (time to flood), and **propellant type** (cased vs bagged; double-base cordite ignites more easily).
- **Ammunition sensitivity**: 5 in AA rounds mass-detonate; 20/40 mm and 3 in burn or go off singly (USN report). This is in `shell-templates.md`/`12_explosive_fillers.md` territory.

### 6.8 Suggested scheme templates
Like `plant-templates.md`: named blocks a design copies, no year inside. Values are typical, not one ship.

| Template | Belt | Deck(s) | Inner line | Magazines | Ships it should reproduce |
|---|---|---|---|---|---|
| `central_battery` | full length, thick, teak-backed | thin, at belt top | — | low, behind coal | Bellerophon, Hercules |
| `citadel_raft` | short thick citadel only | submerged deck at ends | cork/coal cells at ends | in citadel | Inflexible, Duilio |
| `french_cellular` | full length, narrow | armour deck on belt top + splinter deck | cellulose cofferdams above belt | under armour deck | Brennus, Bouvet |
| `protected_deck` | none | turtleback, slopes thicker than flat | coal + cofferdam above slopes | under the deck, below WL | Olympia, Esmeralda |
| `belt_and_slope` | belt + upper belt + ends | turtleback | coal bunkers; torpedo bulkhead at magazines | under deck, behind coal | Majestic, Mikasa, Dreadnought, Cressy |
| `german_bulkhead` | belt + citadel belt + ends | turtleback | coal + 30–50 mm torpedo bulkhead full length | behind bulkhead | Nassau, Kaiser, Derfflinger |
| `all_or_nothing` | thick vertical, citadel only | flat on belt top + splinter | layered TDS | behind TDS | Nevada → North Carolina, KGV |
| `inclined_internal` | internal, 18–19°, lower belt to bottom | bomb + armour + splinter | TDS, belt is a TDS bulkhead | behind TDS; propellant low | Nelson, South Dakota, Iowa |
| `turtleback_modern` | upper belt + main belt | upper deck + low deck with 22° slopes | splinter bulkheads, TDS | behind slope + bulkhead | Bismarck, Scharnhorst, Hipper |
| `french_modern` | external inclined 11–15° | heavy upper deck + light lower with slopes | deep foam-filled TDS, thicker at magazines | floor-plated | Dunkerque, Richelieu |
| `spaced` | thin outer plate + gap + inclined main | two decks | Pugliese / TDS | | Littorio |
| `cruiser_box` | machinery only (or none) | thin over machinery | — | armoured boxes, low | Kent, Leander, New Orleans, Brooklyn |
| `cruiser_belt_structural` | HT side shell 25–76 mm | 25 mm | — | low, ends | Arethusa, C class |
| `unarmoured` | none | none | wing stores / voids | below WL, ends, sprinklered | destroyers, escorts |
| `unarmoured_splinter` | none (or thin STS at machinery) | none | wing stores | below WL; 6–19 mm STS on bridge, guns | Fletcher, Tribal, late-war escorts |

### 6.9 Order of work, if this goes ahead
1. §6.1 (magazines low and inboard): pure layout, fixes the destroyer problem in TODO.md, no new inputs.
2. §6.5 per-zone thickness and magazine boxes: small inputs, unlocks treaty cruisers.
3. §6.4 inner line as armour: the TDS already exists; mostly an export change.
4. §6.2 slopes and §6.3 inclination: the largest change (geometry, weights, hitboxes), and the one that makes pre-1915 ships and Bismarck right.
5. §6.6–6.7 belong to the damage-model work in `docs/battle/`.

---

## Sources

Fetched in this session:
- [County-class cruiser — Wikipedia](https://en.wikipedia.org/wiki/County-class_cruiser) (box citadels 4 in / 2.5 in / 2.5 in, later belts)
- [Leander-class cruiser (1931) — Wikipedia](https://en.wikipedia.org/wiki/Leander-class_cruiser_(1931)) (3 in magazine box, belt lengthened for unit machinery)
- [Dunkerque-class battleship — Wikipedia](https://en.wikipedia.org/wiki/Dunkerque-class_battleship) (inclined belt, two decks with slopes, bulkhead thickened at magazines, magazine floor plate)
- [Nelson-class battleships — naval-encyclopedia](https://naval-encyclopedia.com/ww2/uk/nelson-class-battleships.php?amp=1) (internal 18° belt, 14 / 13 in by zone, single heavy deck)
- [USN Destroyer Report — Gunfire, Bomb and Kamikaze Damage](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/destroyer-report-gunfire-bomb-kamikaze-damage.html) (magazine losses, sprinkling, flooding, STS splinter policy)
- [Armor Protection on American and British Carriers — NavWeaps](https://navweaps.com/index_tech/tech-030.php)
- ["There Seems To Be Something Wrong With Our Bloody Ships Today" — Naval Gazing](https://www.navalgazing.net/There-Seems-To-Be-Something-Wrong-With-Our-Bloody-Ships-Today) (Jutland: flash and handling practice, not penetration)
- [Tribal-class destroyer (1936) — Wikipedia](https://en.wikipedia.org/wiki/Tribal-class_destroyer_(1936))
- [Fletcher-class destroyer — Wikipedia](https://en.wikipedia.org/wiki/Fletcher-class_destroyer) (no thicknesses given; hence the uncertain tag)

Carried over from the earlier research docs (`01_capital_subdivision.md`, `03_small_vessels.md`), which cite their own sources: belt, deck, TDS and cruiser figures for Majestic, Formidable, Dreadnought, Nassau, Kaiser, Hood, KGV, North Carolina, South Dakota, Iowa, Bismarck, Littorio, Richelieu, Yamato, Olympia, Takao, Mogami, Zara, Hipper, New Orleans, Brooklyn, Belfast, Atlanta, Arethusa.

General knowledge, not fetched this session (worth checking before tuning on them): Bellerophon/Hercules central battery, Devastation breastwork, Inflexible's citadel and cork-filled ends, the French cellular layer, Nelson's magazines below the shell rooms, Japanese cruiser belt taper, carrier avgas stowage.
