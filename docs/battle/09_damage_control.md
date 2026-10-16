# 09 — Damage control: flooding, repair, and how long it takes

*Research + model proposal, 2026-10-14. Builds on `damage-model-research.md` (§3 subdivision, §8.4 flooding, §9 dependency graph), `01_capital_subdivision.md` §5 (counterflooding facts, organisation) and `06_service_networks.md` §5 (repair of power/hydraulics, DC parties as agents). This file is about **water**: holes, leaks, bulkheads, pumps, and the people who fight them. Fire-fighting DC is only touched where it competes for the same crews. Not a decision document.*

*Reference implementation: `claude/damage_control_ref.py` (runnable, ~600 lines, results in §8).*

*See also `10_damage_control_holistic.md`: fire, smoke, ammunition, the fire main, firefighting water and how all threats couple, with `dc_holistic_ref.py` building on this file's flooding engine.*

Markers: **[EST]** = my estimate or designer choice. **(?)** = weakly sourced.

---

## 0. Short answer

1. **There are two flooding battles, and they are won by different things.**
   - *Initial flooding* — the holed compartment fills in seconds to a few minutes. Nobody stops it. It is decided by **design**: subdivision, TDS depth, liquid loading.
   - *Progressive flooding* — water creeping through sprung seams, glands, vents, doors and weakened bulkheads over **minutes to hours**. This is where crews win or lose ships. NC's progressive flooding was "negligible"; Shinano's sank her in ~7 h.
2. **Pumps cannot beat a real hole.** A single 30 cm shell hole 2 m under water lets in ~1,000 t/h. A P-500 portable pump moves ~115 t/h, a handy-billy ~14 t/h. So the doctrine is: **write off the holed space, hold the boundary around it, plug and pump the seepage.** A game where pumps "heal" a torpedo hit is wrong.
3. **Small leaks do sink ships — slowly, and only if nobody fights them.** 30 splinter holes (3–12 cm) along one side of a battleship: with no damage control the model reaches ~3,000 t and an 18° list in 8 h. A drilled crew holds it at ~120 t. This is the place where a damage-control model earns its keep, exactly as you suspected.
4. **Speed is a flooding multiplier.** Forward holes see an extra ram head of roughly k·v²/2g: ~5 m at 28 kn near the stem (k≈0.5, [EST]). Same hit, same ship: holding 28 kn → bulkheads collapse, ~10,000 t; slowing to 12 kn within 4 min → ~1,900 t. Seydlitz ran astern for this reason; Bismarck's leak-sail repair was abandoned because she would not slow.
5. **Repairs are a menu of tasks with sizes, crews and times**: close fittings (≈1 min), plug (≈3 min, holes up to ~15 cm), patch (15–40 min, up to ~0.3 m², only out of the water flow), shore a bulkhead (10–30 min), caulk seams and glands, rig a pump (5–15 min), counterflood (NC: 480 t in 6 min). Anything bigger than ~0.5 m² below water is not repairable at sea; it waits for a tender or a dock (§4).
6. **The crew is the DC resource, and it must be in the right place.** 5–6 area repair parties of ~20–40 men on a battleship, ~3 × ~10 on a destroyer [EST], dispersed so one hit cannot kill them all (Yorktown lost Repair V at Coral Sea). DC Central coordinates counterflooding and priorities over sound-powered phones.
7. **Pre-battle state matters as much as skill**: material condition (doors and vents closed), whether compartments were air-tested, liquid loading, counterflood capacity left. Shinano, Audacious and Victoria were lost to leaking closures, not to holes.
8. **Proposed model** (§7): compartment flooding as orifice flow + bulkhead leak areas + bulkhead ratings; holes and links as the only things DC can change; parties as agents taking timed tasks from a priority list; DC Central counterflooding; closed-form hydrostatics. It costs ~0.5 ms per ship-second in plain Python and reproduces NC, a Seydlitz/Bismarck-type bow hit and a Shinano-type loss in the right ballpark (§8).

---

## 1. What happens after a hit: the timeline

| Phase | Time after hit | What happens | Who decides it | Anchor |
|---|---|---|---|---|
| Blast | 0–1 s | Hole, sprung seams in neighbouring bulkheads, shock (breakers trip, pipes crack), flash along open paths | Design | NC: holding bulkhead seams opened by rivet failure, flash through two open WT doors |
| Initial flooding | seconds – ~5 min | Holed spaces fill to the outside waterline; list and trim appear | Design (subdivision, TDS, liquid loading) | NC list 5.5° within the first minutes |
| First response | 1–10 min | Set Zebra if not set, report, isolate, counterflood, slow down | DC Central, captain | NC list removed in 6 min |
| Progressive flooding | 10 min – hours | Water seeps through sprung seams, glands, ducts, doors; slack water shifts; bulkheads near overload may fail | **Repair parties** | NC "negligible"; Shinano ~7 h to capsize; Musashi ~9 h |
| Stabilised | hours | Boundary holds, seepage pumped, counterflood water slowly pumped back out, fuel shifted | Engineering + DC | NC kept station 5 h, then left for repairs |
| Temporary repair | days | Tender or base: divers cut lips, trim ship to dewater, cofferdams, extra shoring | Tender | NC at Tonga: 19–21 Sep 1942, then 18 kn sustained, 24 kn briefly |
| Permanent repair | weeks–months | Drydock | Shipyard | Seydlitz 15 Jun – 1 Oct 1916 |

The 1945 USN handbook rule of thumb (already in `damage-model-research.md` §8.4): a ship that survives the first few minutes will usually live for hours. That gap is the window for damage control, and therefore the window for **player decisions**.

---

## 2. Physics the model must respect

### 2.1 Inflow through a hole

Every serious source uses Torricelli with a discharge coefficient: `Q = Cd · A · √(2 g Δh)`, where Δh is the head difference across the hole (outside waterline minus inside water level, both measured from the hole) ([MDPI JMSE 2024](https://www.mdpi.com/2077-1312/12/9/1502); [Southampton, orifice flow](https://eprints.soton.ac.uk/345880)). Constant Cd ≈ 0.6 is the standard simplification; CFD work shows real Cd varies with hole shape, which matters for engineers but not for a game.

| Hole (Cd 0.6) | 0.5 m down | 2 m | 5 m | 8 m |
|---|---|---|---|---|
| Splinter 5 cm | 14 t/h | 27 | 43 | 54 |
| Splinter 10 cm | 54 | 109 | 172 | 218 |
| Shell hole 30 cm | 490 | 980 | 1,550 | 1,960 |
| Shell hole 50 cm | 1,360 | 2,720 | 4,310 | 5,450 |
| Tear 1 m² | 6,900 | 13,900 | 21,900 | 27,700 |
| Torpedo 50 m² (NC: 32 × 18 ft) | ~350,000 | ~700,000 | ~1.1 M | ~1.4 M |

Consequences:
- A torpedo-holed compartment is **full in seconds**. Its flooding is not a rate to fight, it is a step change in weight. Model it as instantaneous (the Admiralty Trilogy also treats torpedo flooding as immediate).
- Shell and splinter holes near the waterline produce **rates** comparable to pumps and crews. That is the playable part.
- Inflow **stops by itself** when the inside level reaches the outside level. A flooded compartment is not "still flooding"; the danger is what leaks out of it into its neighbours.
- **Holes just above the waterline** matter: list, trim, sinkage, rolling and bow wave put them under water intermittently.

### 2.2 Ram head from speed

A hole near the bow under way sees part of the stagnation pressure: extra head ≈ k · v²/(2g). With k ≈ 0.5 at the stem, fading to 0 by ~¼ L [EST shape]: 12 kn → ~1 m, 20 kn → ~2.7 m, 28 kn → ~5.3 m. The same pressure loads the forward transverse bulkheads.
- **Seydlitz** (Jutland, 5,308 t aboard): fell to 7 kn, then **steamed astern** to take the load off her forward bulkheads; pump steamers met her ([Wikipedia](https://en.wikipedia.org/wiki/SMS_Seydlitz)).
- **Bismarck**: a shell through the bow let in water; a leak sail was brought out and divers sent, but the repair was given up because the captain would not reduce speed ([kbismarck.com, survivor testimony](https://kbismarck.com/archives/debriefing5.html); [testimony 2](https://kbismarck.com/archives/debriefing2.html)).
- **NC**: after shoring, sustained speed limited to 18 kn, 24 kn only briefly "due to the stress against shored bulkheads" ([WDR 61](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-north-carolina-bb55-war-damage-report-no-61.html)).

### 2.3 Leak paths between compartments

Progressive flooding uses the same orifice law with small areas. Typical leak areas per bulkhead [EST, order of magnitude]:

| Path | Area | Notes |
|---|---|---|
| Intact, maintained, air-tested bulkhead (glands, gaskets) | ~5–20 cm² | Ships tested this by pressurising compartments and watching the pressure fall ([Naval Gazing](https://www.navalgazing.net/Survivability-Flooding)) |
| Untested / unsealed penetrations (cables, ducts, pipes) | 100–1,000 cm² | Shinano: compartments never air-tested, penetrations unsealed |
| WT door left open | ~1–1.5 m² | NC was in a relaxed condition when hit; two quick-acting WT doors were open ([WDR 61](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-north-carolina-bb55-war-damage-report-no-61.html)) |
| Ventilation duct through a damaged space | 0.01–0.1 m² | NC: about 2 ft of oil and water reached turret I's lower handling room through a vent |
| Blast-sprung riveted seam | 0.01–0.3 m² | NC holding bulkhead: a seam opened about a foot over several frames |
| Shaft gland wrecked by a whirling shaft | ~0.1–1 m² per bulkhead, several in a row | Prince of Wales |
| Splinter-riddled light bulkhead | many × 10–100 cm² | Naval Gazing photo of a riddled bulkhead |

The flooding-sources study from the 1946 atomic tests noted that many seemingly minor leaks, added together, produced serious flooding, and that progressive flooding turned what should have been slight repairs into major damage ([DTIC AD0366699](https://apps.dtic.mil/sti/pdfs/AD0366699.pdf)). This is the "small leaks" problem in one sentence.

### 2.4 Bulkhead strength

Main transverse bulkheads are designed for the head up to the bulkhead deck, with margin. Model the collapse rating as `rating ≈ 1.3 × (top of bulkhead − leak height)` [EST], cut ~20–40 % for blast-sprung bulkheads, raised ~60 % per shoring set (max 2) [EST], and loaded by about half the ram head when under way forward.
- Shoring exists to support a damaged bulkhead **without pushing it**, and the load is greatest near the bottom, so shores go low and perpendicular ([seasources DC questions](https://www.seasources.net/Preview%20Damage%20Control%20Questions.html); [Brainscape DC cards](https://brainscape.com/flashcards/damage-control-12654001/packs/21202042) (?)).

### 2.5 List, free surface and counterflooding

Already covered in `damage-model-research.md` §8.4 and `01` §5. Two additions from building the reference model:
- **Free surface must be capped for shallow water.** The textbook `ρ·l·b³/12` term assumes the water stays a full-depth layer. A few centimetres of water on a 33 m wide flat would make the ship capsize with 600 t aboard. Shallow water pools at the low side instead. A cheap rule: the transverse shift of a slack space's water is `min(b²·tanφ/(12·d), b/3)`, with d its mean depth, solved by fixed-point iteration on φ (3–12 iterations).
- **Counterflood tanks should be filled one at a time, to full.** A half-filled tank has free surface and works against you. USN BuShips doctrine: counterflood first, then restore reserve buoyancy by pumping out damage water and shifting fuel, then correct trim ([WDR 61 §V-C-10](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-north-carolina-bb55-war-damage-report-no-61.html)).
- **Counterflood capacity is finite.** When the high side's voids are full, list can only grow. That is how Yamato and Musashi ended; it is also a clear, readable "doom clock" for the player.

---

## 3. Doctrine: what a good crew actually does, in order

From the USN BuShips Manual Ch. 88 as quoted in WDR 61, the Naval Gazing summary of standard practice, and the merchant/USN training material:

1. **Close up**: set Zebra (all WT doors, hatches, vent closures, valves). Fittings left open are the commonest cause of progressive flooding in both naval and civil losses (USCG safety alerts; NTSB on *Cape Douglas*, 2024: the flooding might have been contained with the WT doors shut ([National Fisherman](https://www.nationalfisherman.com/ntsb-cites-open-watertight-doors-in-alaska-sinking))).
2. **Find and report**: compare soundings, check boundaries of the damaged area, report to DC Central. Locating a hole once it is under water is hard.
3. **Remove list** fast by counterflooding (NC: within six minutes). Speed beats elegance; reserve buoyancy can be recovered later.
4. **Establish the flooding boundary**: decide which spaces are lost and where to stand. Choose it from the damage, the leakage rates of the candidate bulkheads, and the pumps available ([Naval Gazing](https://www.navalgazing.net/Survivability-Flooding)).
5. **Hold the boundary**: shore the loaded bulkheads and doors, stop seepage with plugs, wedges, oakum, blank flanges, cofferdams around doors (NC shored 14 doors and bulkheads "soon after the damage").
6. **Dewater** spaces whose inflow is now smaller than the pumps; then enter, close what was left open, patch, dry out.
7. **Restore buoyancy and trim**: pump out counterflood water, transfer fuel across and fore-and-aft, pump liquids overboard at the low end.
8. **Manage speed** against bulkhead stress.

The same order works for the AI and is a sensible default priority for the player's DC Central.

---

## 4. Repair catalogue: what, how big, how many men, how long

Times are for a trained crew, lit spaces, level ship, at the damage site. The model multiplies them by skill, darkness (no power ≈ ×1.6), list (×(1 + list°/10)), and travel time. Most are **[EST]**, anchored where shown.

| Task | Fixes | Size limit | Men | Time | Result | Needs | Anchor |
|---|---|---|---|---|---|---|---|
| Close & dog a fitting | open door / hatch / vent | — | 1–2 | 0.5–2 min | leak area → base | access; at GQ mostly pre-set | Zebra drill |
| Plug & wedge | splinter or small shell hole | ≤ ~150 cm² (≈15 cm) | 1–2 | 1–5 min | inflow −90 % | wooden plugs/wedges; hole reachable, water < waist deep | conical + square plugs in combination for ragged holes ([DC notes](https://www.scribd.com/doc/177358317/Damage)) |
| Soft patch (pillow, mattress, rags + shore/strongback) | medium hole, ragged | ≤ ~0.1 m² | 3–4 | 10–20 min | −80–90 % | timber, strongback | Bismarck: hammocks sealed a bulkhead leak in the generator room ([kbismarck](https://kbismarck.com/archives/debriefing5.html)) |
| Box / hinged-plate patch | larger hole in plating | ≤ ~0.3 m² | 4–6 | 15–40 min | −90–95 % | plate, gasket, strongback, shores; **works poorly against active flow** | NRL ex-Shadwell tests: no method reliably sealed actively flowing ruptures; a plate patch worked when installed without flow; a bucket patch was quicker and easier than plugs and wedges ([NRL Ph.2](https://firedoc.nist.gov/article/6ncyXYQBWEcjUZEYBTpt); [Ph.1](https://firedoc.nist.gov/article/SncyXYQBWEcjUZEYvL2l)) |
| Leak sail / collision mat (outside) | large hole near WL, bow | ~0.3–5 m² | 20–40 on deck (+ divers) | 1–4 h | −50–80 % | **ship slowed to a crawl**, calm-ish sea | Bismarck attempt abandoned when speed was not reduced |
| Shore a bulkhead or door | overloaded or sprung bulkhead | one set per ~4–8 m² [EST] | 3–5 | 10–30 min | rating +60 % (≤2 sets) | timber/steel shores, wedges; re-wedge every watch | NRL: all-wood K-shore ≈ 2× time of steel-and-wood; I-shores similar for both |
| Caulk seams, glands, vent ducts | sprung seams, small leaks | — | 2–3 | 5–30 min per path | leak −80 % | oakum, wedges, blank flanges, rags | NC at Tonga: leaks round boundaries, drains, fittings caulked or blanked |
| Isolate pipe / fire-main break | rupture flooding | — | 1–2 | 1–5 min | stops source | knowing where; valve access | `06` §5 |
| Pipe patch (jubilee/soft/banding) | holed pipe | pipe | 2 | 5–30 min | leak stopped or reduced | kit | NRL: jubilee worked on pressurised pipes; banding kit did not |
| Rig portable pump | slow flooding behind boundary | inflow < pump rate | 3–4 | 5–15 min | removes 14–115 t/h per pump | pump, hoses, discharge route, strainer; fuel or power | NRL: rigging times similar, time-to-first-water and rate differed a lot |
| Counterflood | list | design capacity | DC Central + valve men | NC 480 t in 6 min (≈80 t/min) | list → 0 | remote/hand valves, intact voids, judgement | WDR 61 |
| Shift fuel / pump liquids overboard | buoyancy, trim, de-counterflood | — | engineers | hours (slow) | restores reserve | transfer pumps, power | WDR 61: "relatively slow process" |
| Cofferdam a door to enter a flooded space | access | per door | 4–8 | hours | space can be pumped and entered | usually trim the ship to lower the water first | NC at Tonga, with tender |
| Divers: inspect, cut lips, close inside fittings | external damage | — | 2–4 + tender | hours | lower drag; better patch | ship stopped / anchored | NC at Tonga, VESTAL |
| Not repairable at sea | torpedo hole, ruptured TDS, wrecked shaft glands, holing below ~5 m | — | — | dockyard | — | — | NC, Seydlitz |

**Access rules that make the table bite** [EST]:
- You can only plug or patch from inside if the space is **not full and the water is below about waist depth**, and the hole is within reach of the surface.
- You caulk and shore a bulkhead from its **dry side**.
- A party passing a closed fitting loses ~0.5–1 min per door (Zebra slows DC; this is the cost of buttoning up).
- Smoke, fire, steam, darkness and list slow or block movement (`06` §5.2).

---

## 5. Crew and equipment

### 5.1 Repair parties

| Ship | Organisation | Size [EST unless noted] |
|---|---|---|
| Battleship / large carrier | DC Central + 5–6 area repair parties (Repair I–V), plus specialists (gasoline party on carriers, electrical, engineering repair) | ~20–40 men each; 150–250 men total in DC roles at GQ |
| Cruiser | DC Central + 3–4 parties | ~15–25 each |
| Destroyer | 3 parties | ~10 each (`06`) |
| Destroyer escort | Construction & Repair dept. forms the DC parties at GQ | C&R dept: 81 men, also supplied loaders ([USS Slater](https://ussslater.org/s/Crew-Organization.pdf)) |
| Coastal craft | whole crew | 2–4 men available |

- Yorktown's DC organisation had five area parties plus a gasoline party, each able to act alone, dispersed so one hit could not remove all response. Repair V was destroyed at Coral Sea and a temporary Repair VII was formed for Midway ([USNI, "Fighting for Survival"](https://www.usni.org/magazines/naval-history-magazine/2019/december/fighting-survival)).
- At Midway, Yorktown's parties had nearly all fires out in about 90 minutes and the engineers restored 23 kn about an hour after the boilers went out. A 175-man salvage party later pumped her list from 26° to 24° before submarine torpedoes ended it (same source).
- IJN practice drew DC men from crews with other jobs and split responsibility (`01` §5.2).

**Model consequence:** parties are a finite, located, killable resource. Crew casualties in a party's station reduce `men`; a party with fewer men than a task needs works proportionally slower.

### 5.2 Pumps and drainage

| Equipment | Rate | Needs | Source |
|---|---|---|---|
| Handy-billy (P-60), petrol | 60 gpm ≈ 14 t/h (some sources: P-50, 50 gpm) | 2 men to carry (~160 lb) | [Wikipedia](https://en.wikipedia.org/wiki/Handy_billy); [YMS-299](https://yms299.org/?p=1278) |
| P-500 portable, petrol | ~500 gpm ≈ 115 t/h (rate from designation) | 4+ men, hoses | 1950 USN training film ([Periscope](https://stock.periscopefilm.com/?p=354817)) |
| Electric submersible | tens of t/h [EST] | power, cable; discharge via vent trunk | NC rigged two, discharging into an isolated fire-main riser |
| Eductor (jet pump) | tens–100+ t/h [EST] | fire-main pressure | same film |
| Fixed drainage main (steam/electric pumps) | hundreds of t/h per ship [EST] | power or steam, intact piping | — |
| Counterflood valves | NC ≈ 80 t/min ≈ 4,800 t/h | remote/hand valves | WDR 61 |

The comparison with §2.1 is the whole point: pumps are for **seepage and dewatering after the inflow is stopped**, not for holes. NC after repairs needed only 15 minutes of pumping per watch to keep a space dry.

### 5.3 Era and nation knobs (additions to `damage-model-research.md` §10)

| Knob | Range | Notes |
|---|---|---|
| `dc_skill` | 0.4–1.0 | Task speed multiplier; also counterflood accuracy (poor crews overshoot, Shōkaku) |
| `tightness` | 1 (air-tested) – 25 (unsealed) | Multiplies base bulkhead leak areas. Shinano ≈ worst case |
| `closure_discipline` | X / Y / Z set at battle stations; probability fittings are really shut | NC was in a relaxed condition when hit |
| `counterflood_rate`, `counterflood_capacity` | design | Remote valves (USN, IJN) vs hand-operated |
| `portable_pumps`, `pump_rate` | equipment | Wartime lessons pushed the USN to standardise portable pumps and emergency power |
| `repair_party_count`, `party_size` | organisation | Dispersed parties survive single hits |
| `stores` | plugs, timber, oakum, patch plates, casualty cable | Consumable; carries over between battles |

---

## 6. After the battle: temporary and permanent repair

| Level | Where | Time | What it restores | Anchor |
|---|---|---|---|---|
| Ship's force at sea | anywhere | hours | boundaries, seepage, list | NC, Yorktown |
| Tender / advanced base | anchorage | days | dewater spaces, cofferdams, cut protruding plating, more shoring; speed back partly | NC at Tonga, 2 days → 18 kn sustained |
| Dockyard | home or forward yard | weeks–months | structure, TDS, armour | Seydlitz Jutland: 3.5 months; Seydlitz Dogger Bank: ~2 months |

Campaign model: each hole/sprung bulkhead/shoring keeps a `temporary` flag that caps speed (bulkhead stress) and reduces resistance to the next hit until dockyard repair. Dock time ∝ structural damage [EST]; this is where `ship-cost-model.md` can price repairs.

---

## 7. Proposed game model

### 7.1 State

Per **compartment** (from the generator's layout; 20–30 sections × 3–5 spaces for capitals, fewer for small ships):
`{x, y, width, z0, z1, permeability, water_m3, written_off, pump_rate}`

Per **hole**: `{compartment, area_m2, z, patch_factor}` (patch_factor 1 = open, 0.1 = plugged, 0.08 = patched).

Per **link** (bulkhead or deck between two compartments): `{a, b, z_leak, base_area, open_fittings_area, sprung_area, rating_m, shores, failed}`.

Per **ship**: speed, GM, displacement, waterplane, counterflood rate and remaining capacity, fixed drainage (needs power from `06`), portable pumps, `dc_skill`, party list.

### 7.2 Step (once per second of game time is plenty)

1. **Sea level at each hole** = local waterline (draught + sinkage + trim + heel) + ram head.
2. **Hole flows** by orifice law, clamped so one step never overshoots equal levels (stable at any time step; no adaptive stepping needed).
3. **Link flows** the same way; a full compartment is "pressurised" to the level of its highest source.
4. **Bulkhead check**: head difference (+ ½ ram forward) > rating × shoring → collapse → link becomes a large opening and a log event.
5. **Pumps** remove water.
6. **Hydrostatics**: sinkage = Σw / (ρ·A_wp); trim from longitudinal moments (GM_L ≈ L); heel by fixed-point iteration with capped free surface (§2.5).
7. **DC Central**: counterflood one tank at a time while |list| > 1°; log when capacity is exhausted.
8. **Parties** (every 30 s): finish due tasks, then take the highest-priority reachable task (§3 order), travel, work for the timed duration (§4).
9. **Loss**: list > ~20–25° (capsize) or main deck immersed along > ~30 % of length (founder).

### 7.3 Player decisions this creates (all historical)

- **Speed vs flooding**: run and flood, or slow and survive (Seydlitz, Bismarck).
- **Counterflood now or save capacity**: NC's doctrine says now; the cost shows when the next torpedo comes from the other side (WDR 61 §V-C-12 discusses exactly this trade).
- **Where to draw the boundary**: give up a magazine group or an engine room to save the ship.
- **Party allocation**: send Repair 2 to shore the bulkhead next to the boiler room, or to plug splinter holes in the bow.
- **Pre-battle condition**: Zebra early (tighter ship, slower crews, hot unventilated spaces) or late (risk NC's open doors).
- **Abandon or fight on**: the doom clock (counterflood capacity, trend of list and water) tells the player when.

### 7.4 What the player should see

- A **flooding schematic**: compartments coloured by water depth, holes as icons, links glowing when they leak or are near their rating, parties as tokens with progress bars.
- A **water budget**: tons aboard, inflow rate, pumping rate, counterflood used/left, time-to-trouble at the current trend.
- Event log in **symptom → cause** form (`06` §6): "List growing 0.5°/10 min ← counterflood capacity port exhausted ← seepage from wing 11 into inner 11 (sprung holding bulkhead)".

### 7.5 LOD

Same split as `06` §4: full model for ships under the camera; off-screen ships keep only total water, list and a seepage rate that decays with `dc_skill` and grows with `tightness`.

---

## 8. Reference implementation and calibration

`claude/damage_control_ref.py` builds a ~45,000 t battleship (217 × 33 m, T 10 m, GM 2.5 m; 20 sections; TDS wings 5.5 m with permeability 0.5 for their liquid layers; inner spaces split port/stbd; one between-decks space per section), then runs these cases (whole run ≈ 2.5 min in plain Python):

| Case | Model result | Historical / target |
|---|---|---|
| NC torpedo, drilled DC | ~1,000 t damage water; peak list 3.9°; list < 1° at 10 min; ~1,240 t counterflood; stable | 970 t; list 5.5° removed in 6 min with ~480 t; stable, kept station |
| NC torpedo, no DC | ~1,660 t, 7.3° list after 3 h, survives | (counterfactual) |
| Bow shell, holds 28 kn | forward bulkheads collapse at 8 min; ~10,400 t; survives low by the head | Lützow-type spread; Seydlitz 5,308 t |
| Bow shell, slows to 12 kn at 4 min | ~1,900 t | Bismarck ~1,000–2,000 t (?) |
| 30 splinter holes, drilled DC | ~120 t after 8 h | — |
| 30 splinter holes, no DC | ~2,950 t, 18° list after 8 h | "many small leaks add up" (DTIC) |
| 30 splinter holes, drilled DC, no power | ~260 t | darkness slows, portable pumps still work |
| 4 torpedoes one side, Shinano-like (untested ×25, doors open, weak DC) | **founders at 4.5 h**, ~24,700 t | Shinano capsized after ~7 h |
| 4 torpedoes, tight hull + Zebra, weak DC | ~4,000 t, list growing to ~9° after counterflood ran out at ~4.8 h | survives the day |
| 4 torpedoes, tight hull + Zebra, drilled DC | same within a few % | big holes are decided by design and closure, not party skill |

What the calibration says:
- **Party skill decides the small-leak battle; design and closure decide the big-hole battle.** That is consistent with the history (USN parties shine on bombs and shells; Shinano and Audacious were lost to tightness and closures).
- **Counterflood volume is ~2.5× NC's.** Probably because NC's flooded oil tanks displaced oil rather than adding weight, and the model's 5.5 m TDS layer is coarser than NC's 5-bulkhead system. Tune per class, not globally.
- **Ram head constant is the most sensitive guess.** k = 0.5 gives believable outcomes; there is no source for it.

---

## 9. Uncertainties

- **Task times** in §4 are mostly estimates. The NRL ex-Shadwell reports give relative, not absolute, times in their abstracts; the full reports (NRL/MR/6180-96/7903 and Phase 1) would give minutes per shore and pump rigging and are worth fetching.
- **Pump capacities**: handy-billy given as 60 gpm (Wikipedia) and 50 gpm (YMS-299). P-500 rate inferred from its designation.
- **Repair-party sizes** for battleships: no primary source found this session.
- **Bulkhead ratings and leak areas**: derived from design practice and the cases, not measured.
- **Yamato 1945 numbers** (2,350 t from the first torpedo, 604 t counterflood) come from a secondary online source and are not used for calibration.
- **Free-surface cap** (b/3 lever) is a game rule, not naval architecture.

---

## 10. Sources

- USN War Damage Reports (NHHC): [North Carolina WDR 61](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-north-carolina-bb55-war-damage-report-no-61.html) · [Reno WDR 55](https://www.ibiblio.org/hyperwar/USN/rep/WDR/U.S.S.%20RENO%20(CL-96),%20TORPEDO%20DAMAGE%20-%20Off%20Luzon,%20November%203,%201944.pdf) · [Franklin WDR 56](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-franklin-cv-13-war-damage-report-no-56.html) · [South Dakota WDR 57](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-south-dakota-bb57-war-damage-report-no57.html)
- [DTIC AD0366699, study of flooding sources (1946 tests)](https://apps.dtic.mil/sti/pdfs/AD0366699.pdf)
- NRL ex-USS Shadwell DC tactics and equipment tests: [Phase 1](https://firedoc.nist.gov/article/SncyXYQBWEcjUZEYvL2l) · [Phase 2](https://firedoc.nist.gov/article/6ncyXYQBWEcjUZEYBTpt)
- Orifice flow: [MDPI JMSE 12/1502 (2024)](https://www.mdpi.com/2077-1312/12/9/1502) · [Southampton, RINA 2011](https://eprints.soton.ac.uk/345880) · [Mironiuk, flooding time (Gdynia)](https://yadda.icm.edu.pl/baztech/element/bwmeta1.element.baztech-article-BWM4-0016-0053/c/httpwww_amw_gdynia_pllibraryfilezeszytynaukowe2008mironiukw.pdf)
- [Naval Gazing: Survivability – Flooding](https://www.navalgazing.net/Survivability-Flooding) · [Mission kills](https://navalgazing.net/Survivability-Mission-Kills)
- [USNI Naval History 2019: Fighting for Survival (Yorktown DC)](https://www.usni.org/magazines/naval-history-magazine/2019/december/fighting-survival) · [NHHC blog on Lexington/Yorktown DC](https://usnhistory.navylive.dodlive.mil/Recent/Article-View/Article/2686889/even-when-all-seems-lost-sailor-toughness-and-resilience-make-all-the-difference)
- Bismarck survivor testimony: [Lorenzen](https://kbismarck.com/archives/debriefing5.html) · [Manthey](https://kbismarck.com/archives/debriefing2.html)
- [SMS Seydlitz (Wikipedia)](https://en.wikipedia.org/wiki/SMS_Seydlitz)
- Shinano: [Wikipedia, J. Enright](https://en.wikipedia.org/wiki/Joseph_F._Enright) · [WW2DB](https://m.ww2db.com/ship_spec.php?ship_id=19) · [GlobalSecurity](https://www.globalsecurity.org/military/world/japan/shinano-cv.htm)
- Musashi/Yamato: [Padre Steve (Sibuyan Sea)](https://padresteve.com/2020/10/22/) · [Warfare History Network](https://warfarehistorynetwork.com/article/mark-stilles-leyte-gulf/)
- Pumps: [Handy billy (Wikipedia)](https://en.wikipedia.org/wiki/Handy_billy) · [YMS-299](https://yms299.org/?p=1278) · [1950 USN film, Unwatering Flooded Compartments](https://stock.periscopefilm.com/?p=354817)
- Closures: [USCG Safety Alert 1-08](https://www.dco.uscg.mil/Portals/9/DCO%20Documents/5p/CG-5PC/INV/Alerts/0108.pdf) · [NTSB on Cape Douglas](https://www.nationalfisherman.com/ntsb-cites-open-watertight-doors-in-alaska-sinking)
- Crew organisation: [USS Slater](https://ussslater.org/s/Crew-Organization.pdf)
