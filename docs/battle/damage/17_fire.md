# 17 — Fire after it starts: growth, burning time, fire load by era, and how fast it was fought

*Research for the damage model, after `16_casualties.md`. The question:*

> *Fire: how fast it spreads, how long it burns, the fire load by era, how quickly it's fought. The damage research
> has rates for starting fires, but little on what happens after.*

*Fire starts are already covered: `shipgen/research/08_gunfire_effects.md` §3.9 gives P(fire) per burst by shell
and era, and `12_explosive_fillers.md` scales it by filler. `fighting.md` 4.3b built **Fire v0**: a burning cell is
harmed over time, spreads with a chance a minute, and is put out with a chance a minute by the crew left.
The project doc `claude/damage-research/10_damage_control_holistic.md` (in the claude.ai project, not in this
folder) proposed a fire layer, a logistic intensity with spread times through steel of 12/25/40 min. Nearly all of
its fire numbers were [EST]. This note replaces those estimates with test physics and the record. Where it agrees
with 10 it says so. It leaves magazines and cook-off to `07` and `10`, and smoke deaths to `16`.*

*Tags as in the other damage docs: **[SRC]** sourced, **[DERIVED]** computed from sourced numbers (the arithmetic is
shown), **[FIT]** set here to the record, **[EST]** judgement, **[UNCERTAIN]** thin or conflicting sources.*

**Companion file:** `fire_ref.py`, pure Python, about 2 minutes. It holds the fire-load table, the compartment physics
(growth, flashover, ventilation, burning time, hose attack, water left aboard) and a ship-level Monte Carlo with
parties, fire main, smoke into the machinery and spread. Every table in §9 comes out of it.
`python fire_ref.py physics` prints only the single-compartment tables, instantly.

---

## 0. Short answer

1. **Fires were put out in minutes or not at all.** In the record, outcomes split into two groups:
   - **Quick:** Matsushima's deck fire was "immediately put out" (Yalu, 1894). Boise's cabin fire took 12 min, the
     kamikaze destroyers 15–25 min and Manila Bay's hangar 16 min. San Francisco brought ~22 fires "under control
     quickly", and South Dakota's 13 were "quickly extinguished before spreading".
   - **Never:** Teresa, Oquendo and Vizcaya at Santiago; Suvorov and Oslyabya at Tsushima; Astoria, Quincy and
     Vincennes at Savo; Ommaney Bay.
   - **The middle is rare and long** (2–12 h): Dingyuan, Laiyuan, Ralph Talbot, Pensacola, Liverpool.

   **The fire main decides which group a fire falls in.** Every "never" case whose cause is recorded had its main cut
   or its hoses shredded. San Francisco's report calls the intact fire main "probably the most effective contributing
   factor in quick control of the fires" [SRC].
2. **Why it splits, physically:** a compartment fire grows as t² and flashes over after about **3–8 minutes**. Before
   flashover, one hose wins easily. After it, the space needs about **4 L/min of fog per m² of deck**, so a 90 m²
   messdeck needs ~360 L/min: one fog hose, three 1942 all-purpose hoses, or about three straight jets. The first
   minutes buy a factor of ten in water. In `fire_ref.py`, a single 250 L/min hose on a 90 m² 1939 messdeck has it out
   in 16 min if water arrives by minute 4. From minute 6 on, the same hose never puts it out (§9 T4). This is the
   physics behind the destroyer report's "one hose within a minute beats several a few minutes later".
3. **How long it burns: most below-decks fires are starved of air.** A flashed-over compartment burns at the rate its
   openings let air in:
   - **One open door:** about **2 MW**, whatever the fuel. A 40 m² berthing space with one door burns ~3 h, and a
     90 m² wardroom of 1890 for 6 h or more.
   - **Shell holes shorten and intensify the fire:** a door plus 3 m² of holes gives 6 MW for 1.2 h.
   - **Closed up (Zebra, or the doors simply shut):** the fire starves, smoulders, and can flash back when a door is
     opened.

   This fits the record: unfought hulks burned for days (Graf Spee ~2 days, Sheffield 6 days). Lion's Q turret fire,
   thought to be out, flashed 28 min later [SRC].
4. **Fire load by era** (MJ per m² of deck, §2):

   | Space | 1880–1905 | 1904–05 (stripped) | 1914–18 | 1919–42 | 1943–45 (stripped) |
   |---|---|---|---|---|---|
   | Officers' cabins, wardroom | 930 | 660 | 500 | 750 | 290 |
   | Messdecks, berthing | 480 | 440 | 450 | 480 | 250 |
   | Offices, plot, radio | 450 | 480 | 470 | 600 | 310 |
   | Open deck (what burns on it) | 400 | 350 | 150 | 250 | 120 |

   - **The thresholds come from Navy tests:**
     - below **~230 MJ/m²** a space doesn't flash over;
     - **230–450** is marginal;
     - above **450** it flashes over in about 5–15 min (DTIC ADA274791; NBS 1979) [SRC].
   - **What stripping did:** it moved living spaces from "flashes over" to "marginal". That is why late-war USN fires
     stayed local.
   - **Where the load came from:** pre-1905 ships carried varnished wooden partitions ("alleys of roaring flame" at
     the Yalu). Interwar ships carried linoleum, cork, upholstery, aircraft and **up to 80–90 coats of paint**.
5. **Spread** (§5):
   - **Through an open door, hatch, hole or vent duct:** about 1–3 minutes.
   - **Through steel, by conduction:** about 10–25 minutes **upward**, 30–60 minutes **sideways**, and hours
     **downward**. Navy tests measured 270–335 °C in the space above a 20-minute fire and 480–540 °C on the shared
     bulkhead, against 232 °C for paper and cable to ignite [SRC]. Boundary cooling at only ~2 L/min/m² stops it [SRC].
   - **Smoke** travels further than flame, through the supply fans into the firerooms. The firerooms are abandoned,
     the pumps stop, and the fire main dies with them. That was Savo, and Orel at Tsushima before it.
6. **Firefighting by era** (§6):

   | Era | Equipment | Result |
   |---|---|---|
   | 1890s | 2.5-in canvas hose and straight jets; no breathing sets; often no fire organisation | Unstripped ships under 30 hits burned out (Santiago: beached at 40, 55 and 100 min) |
   | Prepared ships of the 1890s | Hoses led out and pumps running before action (Zhenyuan) | Fires held under moderate fire |
   | 1914–18 | Ships stripped at mobilisation | Fires were rare and short; propellant was the danger |
   | 1942 USN | Steam pumps, one unlooped main, all-purpose nozzles, few breathing sets | Savo |
   | 1943+ USN | Fog nozzles, fire schools, looped mains, 1,000 gpm diesel pumps, breathing sets, stripped ships | 15–25 min for a kamikaze; ~22 fires under control on San Francisco |

7. **Firefighting water stays aboard.** A fire boils off at most HRR ÷ 2.6 MJ/kg of water: about 0.8 L/s for a 2 MW
   fire. A hose delivers 4–10 L/s, so **80–90% of hose water stays in the ship**: 12–21 t per hose-hour. A held fire
   fought for hours puts hundreds of tonnes high in the ship. This was Orel's heel, Exeter's list, and Ralph Talbot's
   200 t. It must feed the flooding model.
8. **What mattered most, in the model** (§9 T8): the same 50 hits on a 1942 cruiser are never put out in 83% of runs.
   - **Changes tried one at a time:**

     | Change | Never put out |
     |---|---|
     | None (1942 fit) | 83% |
     | Fog nozzles | 15% |
     | Stripping | 43% |
     | Breathing sets | 48% |
     | Looped main | 53% |
     | Aircraft landed | 75% |
     | Fans secured | 80% |
     | First hose at 1 min | 83% |
     | All of these | 2% |

   - **Caveat:** the fog figure rests on the jet-versus-fog efficiency (10% vs ~30–60% of the water doing work), which
     is the least certain big number here.
9. **Proposed model** (§8): a few numbers per burning space replace Fire v0's chance a minute:
   - heat release Q, the fuel left, a phase, and how long it has burned.
   - Q grows as t².
   - Q is capped by the fuel (load per m²) and by the air (the openings, including shell holes).
   - The space flashes over at Thomas's threshold. It is beaten when the fog-equivalent flow reaches 4 L/min per m²
     involved, and held below that.
   - It spreads through open paths and through hot steel, with the delays above.
   - Hose water goes to flooding, smoke goes to the machinery, and heat goes to `07`/`10`'s magazines.

   It costs a few multiplies per burning space per step.

---

## 1. What a ship fire does, stage by stage

| Stage | Time | What happens | Anchor |
|---|---|---|---|
| Seat | 0 | A burst, fragments, or burning filler ignites bedding, paper, wood, paint or charges. HE leaves a 0.1–0.5 MW seat; gasoline 3+ MW | 08 §3.9; Darwin 1988 (missile hit: flashover in 1–2 min) [SRC] |
| Growth | 1–8 min | Q = α·t². Furnished rooms are "medium" (1 MW at 300 s); stores and wood "fast" (150 s); gasoline "ultra-fast" (75 s) | Eurocode EN 1991-1-2 Table E.5; NKB [SRC] |
| Flashover | ~3–8 min (WWII berthing), 2–3 min (carpet, varnished wood) | The upper layer reaches 500–600 °C; everything in the room ignites. A 9.6 m² Navy berthing room needed ~0.67 MW | NBS 76-1052 / 79-1714 [SRC] |
| Developed | tens of minutes to hours | Burns as fast as air arrives: ~1.5·A√H MW through the openings. One door ≈ 2 MW; Shadwell's design fire 4.5–16 MW through 2 doors and 7 vents | Kawagoe; ADA364776 [SRC] |
| Spread | 1–3 min through openings; 10–60 min through steel | See §5 | Shadwell Ph V/VI [SRC] |
| Decay | the last ~20% of the fuel | Q falls; the steel stays hot for an hour | Eurocode cooling rates [SRC] |
| Smoulder, reflash | minutes to days | A starved fire smoulders; opening it, or heat stored in the steel, re-lights it | Lion Q 28 min; Samuel B. Roberts reflashes; CO₂-smothered holds re-lit 10 days later (AFO 2031/44) [SRC] |

---

## 2. Fire load by era

### 2.1 What burned

**1880–1905: wood, varnish, hammocks, boats, paint, and sometimes coal on deck.**
- **Yalu, 1894:** Chaoyong and Yangwei were lost early because "the partitions and bulkheads being of wood highly
  varnished and oiled" turned passageways into "alleys of roaring flame". Matsushima's 12 cm ready-use charges, piled
  on deck, exploded and caused ~100 casualties (McGiffin, *Century*) [SRC].
- **Chinese stripping before the Yalu:** they landed "all unnecessary woodwork, rigging" and all boats but one gig
  each [SRC]. It wasn't enough.
- **Santiago, 1898:** "The deck is entirely burned away" on Vizcaya and Teresa. Naval Gazing puts the losses down to
  "the extensive use of wood on the Spanish ships and their lack of firefighting capability after the mains were
  damaged" [SRC].
- **Tsushima, 1905:** the Russians had "eliminated woodwork as far as possible", yet Orel had **34 fires** in one day
  [SRC]:
  - hammocks (three times, each one clearing the conning tower);
  - hawsers, "hard to put out";
  - the admiral's quarters;
  - two 6-inch turrets.

  On Suvorov, the 11 wooden boats stacked on deck became a bonfire, and "war paint over the whole of her side facing
  the enemy" burned on Alexander III. The red lead underneath did not burn [SRC].
- **Coal:** the Borodinos took ~2,200 t against a design 1,100 t, some of it on the main deck [SRC]. Bulk coal burns
  poorly from a shell. It shows up in the record as dust and as stability, not as flame.
- **Bunker spontaneous combustion** is a real hazard of its own:
  - New York and Cincinnati both had bunker fires; Cincinnati's charred the woodwork inside the next magazine.
  - The USN rule became "no magazine separated from a coal bunker by a single bulkhead only" (NHHC, Maine
    analysis) [SRC].

**1914–18: stripped at mobilisation.** Queen Mary's diaries record the stripping in August 1914 [SRC]:
- surplus boats landed;
- "superfluous wooden and inflammable fittings" removed: paint and varnish stores, timber, furniture, trunks;
- cabin furniture "dismantled and burnt".

Wooden weather decks stayed and were wetted before action. Sims (1917) says the RN had not taken out wooden decks or
linoleum [SRC]. D.K. Brown: the RN worked hard on fire risk after 1898, and fires were rare in WWI [SRC]. At Jutland
the danger was propellant, not structure.

**1919–42: comfort comes back.** Savo, the clearest inventory (WDR 29) [SRC]:
- "topside paint, life jackets, signal flags, airplanes, ships' boats, lubricating oil and kerosene";
- in Astoria's wardroom, "bedding, papers and clothing", where "the paint in this space had not been removed";
- flag bags, the carpenter shop, movie lockers, a hawser reel, and cane fenders that "could not be extinguished".

The report calls fire "the direct cause of the loss of ASTORIA". Other WWII-era fuels include:
- **Linoleum:** about 5 kg/m², 110 MJ/m² where laid [DERIVED, thickness EST].
- **Cork insulation and upholstery.**
- **Aircraft:** an OS2U holds 120 gal, about **14 GJ per fuelled floatplane** [DERIVED]. That is as much heat as
  ~60 m² of messdeck.
- **Fuel oil** rarely burned: Navy Special's flash point is 55 °C or more, and it sits in tanks below the waterline.
  It did burn when atomised (Salt Lake City's fireroom) or pooled after a torpedo hit (Pensacola: last fire out after
  12 h; Liverpool burned through the night) [SRC].

**1943–45: stripped.**
- **USN:** after Savo the order went out to remove "unessential material presenting fire hazards", and clothes,
  records and paint were left ashore (WDR 29 §§5, 83) [SRC]. Cork, linoleum, rugs and curtains were banned. TIME,
  July 1943: "taking up the linoleum, scraping off the paint, leaving the steel surface bare" [SRC]. San Francisco had
  already landed inflammables and removed ~60% of her paint by November 1942 (WDR 26) [SRC]. Wooden weather decks
  stayed.
- **IJN:** filled the voids around avgas tanks with concrete and drained avgas lines before battle [SRC]. Stripping
  of surface ships wasn't found.

### 2.2 Numbers

**Heats of combustion** (EN 1991-1-2 E.3) [SRC]:
- wood 17.5 MJ/kg;
- cork, cotton, paper, wool, linoleum 20;
- coal 30;
- gasoline and diesel 45.

**Measured loads:**
- **USN berthing, 1979 (NBS IR 79-1714)** [SRC]: bedding 5.8 kg/m² plus lockers ≈ **240 MJ/m² of deck**, with 18–27 kg
  of clothing per man.
- **Eurocode occupancy loads** [SRC], useful as analogues:
  - dwelling 780 MJ/m²;
  - office 420;
  - hotel room 310;
  - library 1,500.

**Navy hazard bands (DTIC ADA274791)** [SRC]:

| Load | Behaviour |
|---|---|
| 0–180 MJ/m² | The fire isn't sustained |
| 230–450 MJ/m² | Flashover marginal ("below 350 MJ/m², flashover and complete combustion does not occur") |
| 450–680 MJ/m² | Flashover |
| 900–1,500 MJ/m² | Long-duration fire |

The table in §0 (and `FIRE_LOAD` in the code) builds each space up from these [EST except where marked]:
- **1880–1905 cabin:** 26 m² of 15 mm pine panelling on 9 m² of deck plus furniture and kit ≈ 46 kg/m² ≈ 900 MJ/m².
  That is the Eurocode dwelling figure [DERIVED].
- **Messdeck:** about 2 m² of deck per man, with ~30 kg of kit and bedding plus tables and lockers ≈ 450 MJ/m². 1943
  takes the NBS 240.
- **Open deck:** the planking is nominally ~860 MJ/m² (76 mm at 650 kg/m³). Wetted, on a steel heat sink and burning on
  one face, only the top centimetre or two takes part, plus boats, hammocks and gear. So 400 MJ/m² in 1890, falling
  to 120 in 1943.
- **Paint add-on:** see §2.3.

### 2.3 Paint

- **How much accumulated:**
  - up to 80 coats on 1970s RN frigates, where removing it took off 45 t and "significantly improved stability"
    (Naval Gazing) [SRC];
  - up to 90 coats inside some ships (Wake, MRL-TR-91-31) [SRC].
- **Unretarded alkyd** "flashed into a violent fire" at 200–225 µm, the thinnest film tested [SRC].
- **Fire-retardant paints** (chlorinated resins, high pigment loading) came out of WWII after fires spread by
  conduction across bulkheads (Wake) [SRC].
- **Its heat** [DERIVED, coat thickness EST]: 20 coats × ~40 µm ≈ 0.8 mm ≈ 1.3 kg/m² of surface ≈ 20 MJ/m² of
  surface. A compartment has ~2.5 m² of bulkhead and deckhead per m² of deck, so 20 coats add ~50 MJ/m² of deck and 80
  coats ~200. That is small beside the contents.
- **What matters is that paint carries flame across steel:** along passageways, up a ship's side (Alexander III), and
  on the cool side of a hot bulkhead. Boise's short fires left paint only "singed and blistered" [SRC], so paint
  spreads fires but doesn't start them.
- **In the model:** paint adds its load, and heavy paint (≥ 60 MJ/m²) multiplies spread through steel by 1.5 [EST].

---

## 3. Growth and flashover

**Growth: the t² fire, Q = α·t²** [SRC: Eurocode E.5, NKB, SFPE]:

| Class | α (kW/s²) | Time to 1 MW | Uses |
|---|---|---|---|
| Slow | 0.0029 | 600 s | transport spaces, sparse loads |
| Medium | 0.0117 | 300 s | dwelling, hotel room, office: **berthing and cabins** |
| Fast | 0.047 | 150 s | library, wood pallets: **stores** |
| Ultra-fast | 0.19 | 75 s | shops; **paint, gasoline** |

- **Measured on ships** [SRC]:
  - a low-load US Coast Guard space grew slower than "slow";
  - a Navy berthing bunk fire took 17–22 min to involve the room through a half-open door;
  - an acrylic carpet made it "an inferno" within 10 min;
  - bare neoprene mattresses flashed over in 3 min.
- **Weapon-started fires:** "flashover can occur within one or two minutes" (Darwin 1988, after Stark) [SRC].
- **The model's growth rate:** medium × an era factor [EST]:

  | Era | Factor | Why |
  |---|---|---|
  | 1890 | ×2 | varnished wood |
  | 1905 | ×1.5 | |
  | 1914 | ×1 | |
  | 1939 | ×1.3 | linoleum, upholstery |
  | 1943 | ×0.7 | bare steel, retardant fabrics |

  On top of the era factor, stores count ×2, paint and hangars ×4, and gasoline ×4.

**Flashover threshold.**
- **Thomas** [SRC]: Q_fo = 7.8·A_T + 378·A_o√H_o kW. A_T is the room's inner surface; A_o√H_o is the openings'
  ventilation factor.
- **The NBS check:** for the 9.6 m² NBS room Thomas gives 1.04 MW, and the room measured 0.67 MW, so the model takes
  **0.65 × Thomas** [FIT].
- **Values that gives:** a 40 m² space with one door flashes over at ~1.1 MW, a 90 m² one at ~1.8 MW.

A space can only flash over if its fuel can feed that much: the fuel-limited HRR is A × r(q), with r rising from 0 at
230 MJ/m² to the Eurocode 250 kW/m² at 650 MJ/m² [FIT to the DTIC bands]. So the model gives three cases:
- **Thin load (1943 berthing, 250 MJ/m²):** the fire peaks at ~0.5 MW, burns the bunks near the seat, and dies in ~20 min
  ("local"). This matches the NBS berthing tests without carpet, which never flashed over [SRC], and South Dakota's
  "small, quickly extinguished" fires.
- **Normal or heavy load with air:** flashover in **3–8 min** (§9 T2).
- **Heavy load closed up:** the fire is air-starved below flashover. It smoulders for up to half an hour, and if a
  door is opened or a shell holes the space, it flashes (backdraft). NIST/NRL ran Shadwell fires with the doors shut:
  the 1 MW diesel fire never reached steady state, and temperatures fell as the oxygen ran out (NISTIR 4961) [SRC].

**This is where the "first minute" rule comes from.**
- **Before flashover:** the involved area is a fraction of the room, and one hose has margin.
- **After flashover:** the whole deck is involved and needs 4 L/min/m².

With medium growth, the window for one hose closes at ~4–6 min. With 1890s varnish it closes at ~3–4 (T4).

---

## 4. How long it burns

**Ventilation-controlled burning** [SRC]:
- Burning rate ≈ 0.1·A√H kg/s of wood (Gross & Robertson, NBS 8147), i.e. **Q ≈ 1.4–1.5·A√H MW** (Eurocode Annex E;
  Kawagoe).
- One open watertight door (0.66 × 1.68 m) has A√H = 1.44 m^2.5, so the fire runs at **~2 MW** whatever is inside.
- The fuel-controlled cap is ~250 kW/m² of deck, which only matters for small, well-ventilated spaces and open decks.

**Duration ≈ fuel ÷ HRR** [DERIVED; the model's T2]:

| Space | Openings | Peak | Burns |
|---|---|---|---|
| 40 m² messdeck, 1939 (480 MJ/m²) | one door | 2.2 MW | ~3 h |
| same | closed, plus a 1 m² shell hole | 1.6 MW | ~4.4 h |
| same | door plus 3 m² of holes | 6 MW | ~1.2 h |
| same | closed up | 0.08 MW, starved | smoulders out in ~30 min; hot |
| 90 m² wardroom, 1890 (930 MJ/m²) | one door | 2.2 MW | > 6 h |
| same | 4 m² of holes | 10.7 MW | ~2.9 h |
| 40 m² messdeck, 1943 (250 MJ/m²) | one door | 0.5 MW | ~20 min, local, no flashover |

**Checks against the record and tests:**
- **Eurocode parametric fire** for the 40 m² case (O = 0.010): t_max ≈ 2.8 h [DERIVED].
- **USCG CPO-berthing burn-outs:** 60 min at 818–965 °C (TRID 388432) [SRC].
- **Stark (1987):** firefighters were held 40 ft off for "many hours" (Darwin) [SRC].
- **Hulks:** Graf Spee burned ~2 days, Sheffield 6, and Bonhomme Richard 4 alongside the pier [SRC].
- **Ships burning under fire:** Suvorov ~5 h from 14:25 to 19:20; Scharnhorst "a blaze from one end to the other" for
  ~3 h; Mikuma ≥ 4 h [SRC].

**What it means for the game:**
- **A space doesn't burn out in the length of a battle.** Fires end by being put out, by flooding (Boise's magazine
  was quenched by the sea coming through the shell hole [SRC]), or by the ship sinking.
- **Shell holes matter twice.** They ventilate fires: hotter and shorter, with more flame out of the openings to the
  next space. And they let firefighting water out.
- **"Out" isn't always out.** A space that burned developed for more than ~10 min holds heat in its steel. Lion's
  smouldering Q turret flashed its charges 28 min after it was thought out [SRC]. Samuel B. Roberts reflashed from heat
  stored in the steel [SRC]. The model gives such a space a reflash chance of 1/20 min for 30 min, ×0.1 with a reflash
  watch (USN doctrine) [EST].

---

## 5. Spread

### 5.1 Through openings

- **What counts as an open path:** an open door or hatch, a shell hole, a hoist, a vent duct, or a sprung bulkhead.
  Each carries flame and hot gas in a minute or two.
- **Boise:** flames went "directly up to the second deck" through ventilation ducts, and from the forward magazine to
  turrets I–III through scuttles and ducts (WDR 24) [SRC].
- **Shadwell's fuel-rich fires** vented flame through the doors and hull vents onto the main deck [SRC].
- **In the model:**
  - an open path to a developed fire gives a hazard of **1/2 min** (scaled by Q/2 MW);
  - the hazard doubles when the fuel could feed 1.5× more than the air allows, i.e. flames out of the openings
    [EST, from the Shadwell observation].

### 5.2 Through steel

- **The ignition criterion:** 232 °C (450 °F) on the unexposed face, for paper and cable (Shadwell Phase 4) [SRC].
- **Upward, Shadwell Phase V/VI, at 20 min** [SRC]:
  - the fire-side overhead was 640–830 °C;
  - the air in the compartment above was 228–335 °C, with 15–25 kW/m² of flux;
  - deck temperatures there were "in the ignition range".
- **Sideways** [SRC]:
  - the shared bulkhead's face reached 480–540 °C, but the next space's air only 54–100 °C;
  - so things *touching* the bulkhead ignite, not the room;
  - a bare A-0 steel bulkhead fails the 139 °C rise criterion in ~4 min in a furnace and radiates ~52 kW/m² at 60 min
    (USCG CG-D-01-94);
  - wood and paper auto-ignite at ≥ 25 kW/m².
- **Downward:** no test numbers were found. The fire's floor sits in the cool lower layer. Savo's fires "gradually
  worked downward" to the 5-inch hoists, most likely through the hoists and openings (WDR 29) [SRC/UNCERTAIN].
- **Insulation, paint and coatings did not delay** heat transfer much: mineral wool falls off at ~1,000 °C. Stark's
  1-inch batt held the far side under 232 °C for 30 min [SRC].
- **Stark (1987) as a case:** the fire reached the space above the living complex by conduction within 1–2 h, and CIC
  ~2 h later [SRC].

**Model:** a neighbour of a developed fire gets an ignition hazard after a delay, scaled by its own load
(×load/400, 0.3–1.5) and ×1.5 for heavy paint [FIT to the above]:

| Direction | Delay | Then hazard | Median from flashover [DERIVED] |
|---|---|---|---|
| Up | 8 min | 1/15 min | ~18 min |
| Sideways | 12 min | 1/40 min | ~40 min |
| Down | 40 min | 1/180 min | ~2.5 h |

Doc 10's 12/25/40 min is about right upward, and faster than the tests support sideways and downward.
- **Boundary cooling** at **2 L/min/m²** holds a boundary at 100 °C (NRL Phase VI) [SRC]. It cuts the hazard to 0.3
  in the model.
- **The cost of cooling:** the steam it raises cuts the stay time of men working above to 7–14 min, against 20–23 min
  horizontally [SRC].

### 5.3 On the open deck

- **Open-deck fires burn their items and stop** unless there's enough on the deck to keep going: dry varnished wood,
  boats, hammocks, coal, aircraft. The model sustains an open-deck fire only above **300 MJ/m²** (×0.6 if the decks
  were wetted). It spreads to the next section at 1/6 min × load/300 [EST].
- **The record:** Savo's well-deck fire "spread very rapidly", fed by aircraft gasoline [SRC]. Charring rates are
  0.5–0.8 mm/min (EN 1995-1-2) [SRC], so planking needs ~1.5–3 h of fire above it to burn through [DERIVED]. Santiago's
  decks did, with the mains cut.

### 5.4 Smoke: how fire stops a ship

Fire spreads further as smoke than as flame:
- **Orel (1905):** blowers drew smoke into the forward fireroom, which was abandoned [SRC].
- **Teresa (1898):** smoke drawn into the ventilation blocked the magazine flood valves [SRC].
- **Savo (1942):** No. 4 fireroom had to be abandoned within ~13 min from the heat of the well-deck fire, and
  Vincennes' blowers drew smoke into her firerooms [SRC].
- **Akagi, Franklin, Bunker Hill, Illustrious:** the same chain (doc 10 §3).

**The chain:** firerooms abandoned → steam lost → the fire pumps stop → no water anywhere.

**In the model:** a machinery space under a burning space is abandoned at 1/8 min with supply fans running, and 1/60
min with them secured [EST]. When half the machinery spaces are gone, the main fire pumps stop. Only a diesel pump on
a looped main (post-Savo) keeps 0.6 of the pressure.

---

## 6. Fighting it

### 6.1 How much water

- **Control** of a fully involved space needs about **4 L/min per m² of deck** [SRC]:
  - Grimwood: area × 4;
  - the Iowa formula (gpm = ft³/100, from Navy and Coast Guard WWII steam work) is ≈ area × 4 at 3 m deck height;
  - a tactical rule of area × 6.
- **To put it out:** Särdqvist (Lund) found the flow that minimises total water ≈ 12 L/min/m², with ~60 L of water per
  m² and ~5 min of work [SRC; unit UNCERTAIN].
- **Physics** [DERIVED]: water absorbs 2.6 MJ/kg from 20 °C to steam. A 2 MW fire therefore boils at most 0.77 kg/s
  (46 L/min).

**Nozzles:**
- **Straight jet:** ~10% of the water does cooling work (Gałaj 2018) [SRC].
- **Fog:** no measured figure was found; 30–60% [UNCERTAIN].
- **In the model:** a jet counts **a third** of its flow and fog all of it; the 1942 USN all-purpose nozzle counts half
  [EST].

**Shadwell 1994** (ADA319156) is the calibration test [SRC]:
- 74 m³ compartment, 4-person team in OBAs, a 95 gpm (360 L/min) 1.5-in line;
- **fog:** under control in 2 min, done in 9.1 ± 0.6 min, no burns;
- **straight stream:** the fire reflashed or flashed over within 2 min, the team retreated and re-entered: 10.9 min,
  with burns.

The model gives fog control in 1.1 min, out at 10 min from ignition; jet control in 3.2 min, out at 12 (§9 T3).

**What one hose can do** [DERIVED]:

| Hose | Flow | Fog-equivalent | Controls a flashed-over space of |
|---|---|---|---|
| 1.5-in fog (USN 1943+) | 360 L/min | 360 | ~90 m² |
| 1.5-in all-purpose (USN 1942) | 250 L/min | 125 | ~30 m² |
| RN large jet, 35–115 psi | 417–750 L/min | 140–250 | 35–60 m² |
| 2.5-in canvas jet, 1890s–1914 [EST flow] | ~600 L/min | 200 | ~50 m² |
| Buckets and CO₂ (main cut) | ~60 L/min | 20 | only a fire caught early |

- **Above the requirement:** the fire is knocked down with τ = 90 s × F_req/F [FIT to Shadwell's 2 min], then
  mopped up with 60 L per m² involved [SRC Särdqvist].
- **Below it:** the fire is **held**, burning at (1 − F/F_req) of its strength, not beaten. Ralph Talbot's 7 h on
  CO₂ and buckets, and Aaron Ward's 2 h, look like held fires.
- **A held fire burns longer and pours water into the ship.** One 150 L/min jet on a 30 m² fire: 155 min and 19 t
  (§9 T3).

### 6.2 Equipment by era

| Era / ship | Pumps | Main | Hose / nozzle | Other | Source |
|---|---|---|---|---|---|
| 1890–1910 | Steam fire pumps, Downton hand pumps; numbers not found | Single, partly on the upper deck | 2.5-in canvas, jets | Buckets, sand; stripping before action; "hoses led out and pumps running" on prepared ships (Zhenyuan, per Sims) | USNI 1895; Naval Gazing [SRC]; pumps [UNCERTAIN] |
| WWI dreadnought | Not found | RN 50 psi pre-WWII is the nearest | 2.5-in canvas jets | USN handy-billy 50 gpm | [UNCERTAIN] |
| RN WWII large ship | Total ~1,500 t/h (25,000 L/min); 6-in main | 50 → 75 psi in the war | 2.5-in; jets 9–45 t/h; spray +20% water | Diesel portable pumps; foam 680 gal/min of foam from 80 gal/min of water; CO₂; Salvus and smoke helmets | Carter & Hogger 1956; AFO 2031/44 [SRC] |
| RN WWII frigate/destroyer | ~160 t/h (2,700 L/min); 3.5-in main | same | 2.5-in | Two-man manual pumps | same |
| USN 1942 heavy cruiser | Steam pumps; single main | — | 1.5 / 2.5-in all-purpose | 6 handy-billies, 12 submersibles; few rescue breathers; CO₂ "soon exhausted" | WDR 29 [SRC] |
| USN 1943+ heavy cruiser | + 1,000 gpm diesel pump forward, looped main | — | Fog, applicators | 100 kW emergency diesels | WDR 29 [SRC] |
| USN Fletcher DD | 4 fire-and-bilge + 2 fire-and-flushing ≈ 30 fog nozzles at once (~2,000 gpm) | — | 1.5 / 2.5-in | 2 × P-500, handy-billy, 5 submersibles; 3 repair parties × ~10 | DD report [SRC] |
| USN Essex CV | 14 pumps | — | 100 + 50 ft per plug | Sprinklers, curtains, foam; 106 rescue breathers | WDR 56 [SRC] |

- **Training:** USN fire schools opened in June 1942: two days for all hands, six days for leaders (boshw.us) [SRC].
- **Breathing sets:** the WWII A-1 rescue breather lasted ~45 min, and the 1946 Chemox OBA ~60 min [SRC].
- **Stay time in heat:** 20–23 min, so rotate [SRC].
- **Hose team:** 4 at Shadwell; 5 in 1998 doctrine [SRC].

### 6.3 Time to first water

| | First hose | Parties muster | Source |
|---|---|---|---|
| Unorganised (Chinese 1894, Spanish 1898) | none until a party arrives | ~6 min [EST] | White, Halsey (USNI 1895); Santiago [SRC, qualitative] |
| Prepared 1890s (hoses laid out, pumps on) | ~2 min [EST] | ~3 min | Zhenyuan; Sims' fire bill [SRC] |
| Russian 1905 | ~1.5 min | ~3 min; hoses "torn to ribbons" by 14:20 on Suvorov | Semenov [SRC] |
| RN 1916 | ~3 min [EST] | ~4 min | — |
| USN 1942 | ~2 min [EST] | ~3 min | DD report |
| USN 1943+ | ~1 min (fire watch at the locker) | ~2 min | DD report "within a minute" [SRC] |

- **Hoses get cut in action:** Suvorov's were replaced "again and again" until the spares ran out [SRC].
- **The model's hose-cut chance:** 0.3–0.45 per hit in the section in 1894–1905, and 0.15–0.2 later [EST].

### 6.4 The fire main

The fire main is the decider, as doc 10 §2.6 already argues. What's added here:
- **Single, unlooped mains** lose everything beyond a break until it is isolated: 4–22 min in the 1998 Shadwell runs
  [SRC]. "Never" in 1898 and 1905.
- **A jumper hose** gives half pressure to a cut section (doc 10). Model: 6–15 min by era [EST].
- **Break chance per hit:** 0.25 (1942 USN cruiser, risers exposed), scaled ×1 for a destroyer, ×0.5 for a cruiser and
  ×0.35 for a battleship. Big ships have more risers and more ship between them [EST, set so Savo loses its main and
  South Dakota keeps hers].

### 6.5 Firefighting water

- **A fire can boil only HRR/2.6 MJ/kg of the water,** at most ~15% of a jet's and ~80% of fog's. On a 2 MW fire,
  hoses leave **12–21 t per hour** aboard (§9 T5).
- **Model:** retained water = applied − min(applied × e, Q·dt/2.6). It enters the flooding model in the cell being
  fought, as 4.3b's water.
- **Where it leaves the ship:** through the space's holes, scuppers and the pumps.
- **The record:**
  - Orel's battery deck flooded from hose water and heeled;
  - Exeter's firefighting water worsened her list;
  - Ralph Talbot reached 20° (doc 10);
  - Franklin took an 8° list.

---

## 7. The record

Times are local. The fuller collection is in the research notes behind this file; the main cases:

| Ship | Action | Fires | Time to control / out | Why | Source |
|---|---|---|---|---|---|
| Matsushima | Yalu 1894 | Ready 12 cm charges on deck | "Immediately put out" | Organised crew | sinojapanesewar.com; USNI 1895 |
| Dingyuan | Yalu | Upper deck "entirely destroyed by fire" | ≥ 2 h | Wood; no fire brigade | same |
| Laiyuan | Yalu | Stern fire 14:20, gutted, deck red-hot | "Several hours"; reached port | Wood | same; Wikipedia |
| Zhenyuan | Yalu | Several fires | Kept in hand | Hoses out, pumps running before action | USNI 1895 |
| Chaoyong, Yangwei | Yalu | "Enveloped in flames" | Never; sank / stranded | Varnished wooden partitions | McGiffin; sinojapanesewar |
| Infanta María Teresa | Santiago 1898 | Flag cabin, aft | Never; **fire main cut** by an early shot; beached at **40 min** | Wood; smoke at the flood valves | Sampson's report; Naval Gazing |
| Oquendo, Vizcaya | Santiago | General | Never; beached at 55 and 100 min | Wood furnishings | same |
| Reina Cristina | Manila Bay | First fires out; more each pass | Scuttled burning | Hits outran the crew | Wikipedia |
| Knyaz Suvorov | Tsushima 1905 | Quarters, paint, hammocks, 11 boats | Early fires out; **ablaze fore and aft by 14:25 (~30 min)**; burning till sunk 19:20 | Hoses shredded, supply exhausted | Semenov; USNI 1906 |
| Oslyabya | Tsushima | ≥ 6 large | Early ones out; main broken, hoses torn | | topwar |
| Orel | Tsushima | **34** | Each fought; survived | Hose water heeled her; smoke into a fireroom | USNI 1906 |
| Seydlitz | Dogger Bank 1915 | Charges in both aft turrets | Minutes; magazines flooded | Propellant | Wikipedia |
| Lion Q turret | Jutland 1916 | Smouldering, "thought extinguished" | **Flashed 28 min later** | Hidden smoulder | USNI 1925 |
| Malaya 6-in battery | Jutland | Charges on trolleys | ~5 min (one eyewitness) | Charges staged at the guns | forum quotes |
| Good Hope | Coronel 1914 | Forecastle on the 3rd salvo | Never; magazine at ~40 min | | Wikipedia |
| Emden | Cocos 1914 | Ready ammunition | Never; beached at ~75 min | | Wikipedia |
| Exeter | River Plate 1939 | Messdecks, paint shop, ready locker | Locker with sand; messdecks burning > 30 min | Hose water worsened the list | navyhistory.au |
| Bretagne | Mers-el-Kébir 1940 | AA ready ammunition | Never; exploded at 10 min | | Wikipedia |
| Liverpool | 1940 | Avgas vapour after a torpedo | Fought through the night | Needed boilers relit for pump steam | lancaster.ac.uk |
| Astoria | Savo 1942 | Paint, boats, planes, ready ammunition | **Never**; firerooms abandoned 13 min after the first hits; sank ~10 h later | Risers cut, few CO₂ bottles, planes and kerosene | WDR 29 |
| Quincy, Vincennes | Savo | Hangar, gasoline, cane fenders | Never; capsized at 40–55 min | Mains cut | WDR 29 |
| Boise | Cape Esperance 1942 | Cabin; forward magazine | Cabin **12 min**; magazine quenched by the sea through the hole | Ducts and scuttles carried flame | WDR 24 |
| San Francisco | Guadalcanal 1942 | **~22** | "Under control quickly" | Main intact; inflammables landed; 60% of paint off | WDR 26 |
| South Dakota | Guadalcanal 1942 | **13**: life jackets, bedding, clothing | "Quickly extinguished before spreading" | Main in 4 sections, pumps running; hoses run up the outside | WDR 57 |
| Pensacola | Tassafaronga 1942 | Oil after a torpedo, turret 3 ammunition | Last fire out after **12 h** | Ruptured oil tanks | DANFS |
| Ralph Talbot | 1942 | Forward superstructure | **~7 h** | Main broken; CO₂, buckets; 200 t in | DD report |
| Zellars, Walke, Stormes, Hadley | 1945 | Gasoline and ammunition | **15, 15, 20, 25 min** | Fast hoses | DD report |
| Aaron Ward (DM) | 1945 | Gasoline and ammunition | ~2 h, with LCS-83 alongside | | DD report |
| Manila Bay (CVE) | 1945 | Flight deck, hangar | Deck 9 min; hangar **~16 min** | | CVE report |
| Ommaney Bay (CVE) | 1945 | Hangar | Never; aft main pressure lost | | CVE report |
| Yamato | 1945 | Next to the 155 mm magazine | "Cannot be extinguished"; exploded ~1 h 40 min later | DC officers killed | combinedfleet |
| Post-war anchors | | | Oriskany 3 h / 8 h; Forrestal deck ~1 h, below 17 h; Sheffield abandoned ~4 h with main breached; Glamorgan 4.4 h; Stark ~24 h; Samuel B. Roberts 4+ h | Fuel, cable, aluminium, propellant | various |

**Patterns** [DERIVED]:
1. **Bimodal, decided by water.** In every case with a recorded cause, "never" meant the main was cut, the hoses were
   shredded, or the pumps were lost to smoke.
2. **1894–1905:** under heavy fire, unstripped ships were burning beyond help in 30–100 min (Santiago, Suvorov,
   Chaoyong). Organised crews put out fires one by one (Orel's 34, Matsushima) but drowned their own decks doing it.
3. **1914–18:** structure seldom burned. Fires were propellant, over in seconds to minutes. They threatened the ship
   through the magazine (`07`), with a hidden-smoulder tail (Lion).
4. **1939–45:** an intact main and a stripped ship handled 13–22 fires within the action (South Dakota, San
   Francisco). Gasoline, aircraft and torpedo warheads turned controllable fires into losses within 20–90 min. Oil
   fires after torpedoes ran 10–12 h.
5. **Burning hulks with nobody fighting:** days.

---

## 8. Proposed game model

### 8.1 State per cell (on top of 4.3b)

| Field | Meaning |
|---|---|
| `q` | Fire load, MJ/m², from the room's use, the era/outfit and the paint (§2; the designer's "stripped for war" toggle) |
| `extra` | Gasoline, aircraft, oil spill, MJ |
| `E`, `E0` | Fuel left and at ignition |
| `Q` | HRR, MW |
| `phase` | `grow`, `local`, `starved`, `dev`, `decay`, `ctrl`, `out` |
| `tIgn`, `devS` | Clocks |
| `holes`, `doorsOpen` | For the vent (holes come from `Effects`; doors from the material condition) |
| `hot` | Steel still hot after a developed fire (for reflash) |

### 8.2 Step (the 4.3b fire step, any dt up to ~15 s)

1. **Caps:**
   - `Qvent = 1.5·(0.05 + doors·1.44 + holes^1.25)` MW (∞ on an open deck);
   - `Qfuel = area·0.25·clamp((q−230)/420, 0.01, 1)` (+2 MW with gasoline);
   - `Qfo = 0.65·(0.0078·A_T + 0.378·A√H)`.
2. **Grow:** `Q = Q0 + α·t²·(1 − held)²`. Then:
   - Q reaches Qfo with both caps above it → **flashover** (`dev`);
   - Qfuel is below Qfo → **`local`**: dies when the seat's items are gone (~20 min or 30% of the load);
   - Qvent is below Qfo → **`starved`**: smoulders; a new hole or opened door flashes it; it goes out after ~30 min,
     hot.
3. **Developed:** `Q = min(Qvent, Qfuel)·(1 − 0.7·held)`. Below 20% of the fuel it decays, and it is out when the fuel
   is gone.
4. **Water:**
   - `Freq = max(40, 4·A_involved)` L/min, where A_involved is the whole cell when developed, else the cell × Q/Qfo
     (on open decks Q/0.1 MW per m²).
   - `F` is the fog-equivalent flow on the cell: hoses × flow × √pressure × nozzle factor, ×0.5 from the doorway in
     smoke without breathing sets, ×0.8 for rotation, ×0.5 on gasoline unless foam.
   - `held = min(1, F/Freq)`.
   - When F ≥ Freq: `Q *= exp(−dt/τ)` with `τ = 90 s·Freq/F`, then `ctrl`.
   - In `ctrl`: out after 60 L per m² involved. If water stops before then, a 30% chance it rekindles.
5. **Spread:** the open-path, steel and open-deck rules of §5, with the delays measured from flashover.
6. **Effects:**
   - Hose water left aboard goes to the cell's water (§6.5).
   - Smoke takes the machinery spaces above which fires burn (§5.4); that stops the steam pumps.
   - A developed fire next to a magazine adds heat to it (`07`/`10`).
   - Parts in a developed cell are damaged as Fire v0 already does.
   - Men in it are casualties (`16`).
7. **Reflash:** a cell that went out hot has a chance of 1/20 min over 30 min, ×0.1 with a reflash watch.

### 8.3 Crews

**Parties** (doc 10 §5 and `09`):
- They go to the worst fire, weighting fires next to magazines ×3.
- A party arrives after its muster time (first job only) plus 30 s a section.
- Each party gives `min(3, men/crew_per_hose)` hoses: 6 men a hose for 2.5-in canvas, 4 for 1.5-in [SRC Shadwell].
- **A local first hose** (fire watch, hoses laid out) comes at the era's first-hose time if the section has pressure.
- **Hits in the section:** each cuts each working hose with the era's chance and can kill party men. Spare lengths
  replace cut hoses in 2 min.
- **Turn-to:** extra parties from the gun crews come 15 min after the last hit, with skill 0.7 (doc 10).

**Era and doctrine values (`DOCTRINES` in the code):**

| | First hose | Muster | Flow, nozzle | Men/hose | Main break | Hose cut | Looped | Isolate | Breathing sets | Fans secured |
|---|---|---|---|---|---|---|---|---|---|---|
| Unorganised 1890s | none | 6 min | 600 jet | 6 | 0.25 | 0.30 | no | never | no | no |
| Prepared 1890s | 2 | 3 | 600 jet | 6 | 0.20 | 0.30 | no | 15 | no | no |
| Russian 1905 | 1.5 | 3 | 600 jet | 6 | 0.20 | 0.45 | no | 15 | no | no |
| RN 1916 | 3 | 4 | 420 jet | 5 | 0.10 | 0.15 | no | 15 | no | no |
| USN 1942 | 2 | 3 | 250 all-purpose | 4 | 0.25 | 0.20 | no | 8 | no | no |
| USN 1943+ | 1 | 2 | 360 fog | 4 | 0.15 | 0.20 | yes + diesel | 4 | yes | yes |

The RN 1916 and USN doctrines also wet the decks (1890s prepared and 1905 too), and the post-Savo doctrine cools
boundaries and posts reflash watches.

### 8.4 What the player decides (all historical)

**Before action:**
- strip ship: the "stripped for war" toggle (§2);
- land or drain the aircraft;
- lay out hoses and run the fire pumps;
- wet the decks;
- set the material condition: closed doors starve fires but slow the parties;
- secure the supply fans: smoke-free machinery, at the cost of heat in the magazines;
- section the main.

**In action:**
- how many men leave the guns for fire parties;
- fire-first or flood-first (doc 10 §5);
- flood or sprinkle a threatened magazine;
- jettison ready ammunition.

**After the action:** turn to. Speed and heading matter on deck fires: wind feeds them, and a hard turn spills
burning fuel (Bunker Hill).

### 8.5 What the player sees

- **On the schematic:** per cell, Q as flame size (local / developed / starved), heat in the steel, and smoke.
- **Pressure** per fire-main section.
- **The log tells cause and effect:** "Fire in after messdeck flashed over (no water: main cut at frame 112) → spreading
  to wardroom above in ~10 min."
- **The enemy sees:** "burning" for a developed or open-deck fire, brighter with Q: a target at night (Savo, Fire
  v0's flag). A starved fire inside shows nothing.

### 8.6 Cost and tiers

- **Cost:** a few dozen floating-point operations per burning cell per step. Nothing runs on cells that aren't
  burning or hot.
- **Ship-level Monte Carlo:** the Python takes ~0.5–2 s per 4-hour run; C# should be 100× faster.
- **The coarse tier (4.9):** fit a per-ship rule (fires burning, a controlled/uncontrolled flag) to this model's runs,
  as plan.md intends.

### 8.7 Tests (wide bands)

1. One HE fire in a 1943 messdeck with a fog hose at 1 min: out in under 5 min, no flashover. The same fire with no
   water: flashover in 3–8 min, then 2 MW through one door.
2. A Shadwell-size fire with one fog hose at entry: control ≤ 3 min, out within ~10 min of entry. Straight stream:
   slower, with reflash.
3. An unfought 40 m² space with one door burns 2–4 h. Closed up, it starves. A shell hole lets it flash.
4. 30 hits on an unstripped 1890s cruiser with no organisation: ablaze (half the sections) within ~1 h in most runs.
5. 26 hits on a stripped 1942 battleship with an intact main: every fire out within ~30 min in most runs.
6. Savo hits on the 1942 fit: never out in most runs. On the post-Savo fit: out within ~30 min in nearly all.
7. Water from hoses enters flooding at 12–21 t per hose-hour.
8. Replays hold at different steps (dt 5 s vs 15 s agree within the noise).

---

## 9. What the model gives (`fire_ref.py`)

### T2. One space, unfought

See §4. Flashover comes at **2.8–7.6 min** for normal loads. The 1943 messdeck never flashes over: it burns locally
for ~20 min.

### T3. One hose on a Shadwell-size fire (30 m², 74 m³), 1939 load

| Hose | Water at | Control after water | Out after ignition | Water aboard |
|---|---|---|---|---|
| Fog 360 L/min | 4 min | 1.1 min | 10.1 min | 2.2 t |
| Jet 360 L/min | 4 min | 3.2 min | 12.2 min | 2.9 t |
| Fog 360 L/min | 1 min | 0.1 min | 1.9 min (no flashover) | 0.3 t |
| All-purpose 250 L/min | 4 min | 3.1 min | 14.3 min | 2.6 t |
| Jet 150 L/min | 4 min | never | 155 min (held) | 19.2 t |

Shadwell measured fog at 2 and 9.1 min, and straight stream with reflash at 10.9 min.

### T4. When the first water arrives (90 m² messdeck, 1939 load)

| Water at | One 250 L/min all-purpose hose | Three hoses |
|---|---|---|
| 1 min | out at 4 min | out at 2 min |
| 3 min | 12 min | 6 min |
| 4 min | 16 min | 9 min |
| **6 min** | **never (flashed; held)** | 16 min |
| 10 min | never | 20 min |
| 15 min | never | 25 min |

### T6. Ships (60 runs each, 4 hours)

| Scenario | Hit fires (median) | Flashovers | Never out | Ablaze (half the sections) | Water t (median) | Record |
|---|---|---|---|---|---|---|
| Santiago: 30 hits/45 min, unstripped, unorganised | 8 | 12 | 98% | 98%, median **19 min** | 422 | Beached at 40, 55, 100 min, burned out |
| Same hits, stripped and prepared | 13 | 10 | 87% | 85%, 32 min | 383 | — |
| 12 hits/45 min, unstripped, unorganised | 5 | 12 | 92% | 92%, 26 min | 414 | Chaoyong, Yangwei |
| 12 hits/45 min, stripped and prepared | 7 | 0 | **32%** | 23% | 0 | Matsushima, Hiei, Zhenyuan |
| Tsushima: battleship, 60 hits/40 min | 19 | 18 | 98% | 92%, **28 min** | 767 | Suvorov ablaze ~30 min |
| Jutland: RN battleship, 15 heavy hits | 3 | 0 | 2% | 0% | 0 | Fires rare and short |
| Savo: 1942 cruiser, 50 hits/20 min | 14 | 4 | **83%** | 52%, 21 min | 7 (power lost) | Astoria never |
| Same hits, post-Savo fit | 10 | 0 | **0%** | 0% | 0 | — |
| San Francisco: 45 hits/70 min, stripped | 10 | 0 | 7% | 0% | 0 | ~22 fires, all controlled |
| South Dakota: 26 hits/17 min | 5 | 0 | 18% | 0% | 0 | 13 fires, quickly out |
| Destroyer, kamikaze, 1945 doctrine | 1 | 3 | 3%, out in **12 min** | — | 5 | 15–25 min |
| Same, 1942 doctrine | 1 | 3 | 93% | — | 240 | Abner Read lost |

How to read the table:
- **Never out** means fires were still burning at 4 h.
- **The water column and the power loss interact.** On Savo the water is low because the pumps stopped. On Santiago
  it is high because jets fought held fires for hours.

**What the runs say:**
- **The record's split appears without being forced:** Santiago and Suvorov burn, the 1942 USN battleship and cruiser
  mostly don't, and Savo does.
- **Preparation buys time under heavy fire and wins under moderate fire.** Under 30 hits in 45 min, stripping and
  laying out hoses only delay "ablaze" by ~13 min. Under 12 hits it saves two ships in three.
- **The model runs fast and loose in three places:**
  - **"Ablaze" comes too early.** Santiago reaches it in ~19 min, against beaching at 40–100 min. The crews kept
    fighting longer than "half the sections burning" implies.
  - **The destroyer kamikaze is put out too fast:** 12 min against the record's 15–25. The model has no cook-off and
    no fire-watch casualties beyond the crash space.
  - **Some battleship runs leave a held fire burning:** 18% of South Dakota runs, in a below-decks store without
    breathing sets. Her real hits were higher.
- **Water is a real number:** hundreds of tonnes on the burning 1890s ships. The game should drain it through holes
  and scuppers rather than keep it all.

### T7. The split itself (spaces still burning at 4 h)

| | Out | 1–3 | 4–9 | 10+ |
|---|---|---|---|---|
| Santiago, unprepared | 1 | 0 | 0 | 59 |
| Santiago, prepared | 8 | 1 | 5 | 46 |
| 12 hits, unprepared | 5 | 0 | 0 | 55 |
| 12 hits, prepared | 41 | 3 | 4 | 12 |
| Savo, 1942 fit | 10 | 14 | 18 | 18 |
| San Francisco | 56 | 4 | 0 | 0 |

### T8. What was worth what (Savo hits, one change at a time)

| Change from the 1942 fit | Never out | Spaces burned | Power lost | Water t |
|---|---|---|---|---|
| None (as fought) | 83% | 19 | 43% | 264 |
| Aircraft landed | 75% | 19 | 35% | 218 |
| Stripped (1943 load) | 43% | 11 | 8% | 182 |
| Supply fans secured | 80% | 18 | 30% | 365 |
| Looped main, diesel pump, jumpers | 53% | 15 | 20% | 193 |
| **Fog nozzles** | **15%** | 16 | 5% | 39 |
| Breathing sets | 48% | 18 | 37% | 178 |
| First hose at 1 min | 83% | 19 | 40% | 268 |
| All of it | 2% | 9 | 0% | 8 |

**Reading it:**
- **Under a flood of hits, the first hose matters little.** Too many fires start at once for local hoses to catch them,
  and they cannot reach every seat. The first minute matters for single fires (T4) and kamikazes, not for a night
  surprise.
- **Fog's lead** rests on the jet-versus-fog efficiency.
- **Doc 10** found the post-Savo package worth 11/12 → 4/12 losses. That is consistent, though doc 10 also counted
  flooding.

---

## 10. Uncertainties and next sources

- **Fog efficiency:** no measured fraction of fog water doing work was found, and T8's biggest lever rests on it.
  Särdqvist's thesis and the Shadwell water totals (not reported) would settle it.
- **Spread down and sideways through steel:** only the upward numbers are measured. The full text of Scheffey, Darwin
  & Leonard (IMAS 94) has the curves.
- **Pre-1914 equipment:** pump numbers and capacities, main pressure and hose sizes were not found. Brassey's,
  *Naval Annual* and the RN Gunnery and Seamanship manuals are the next places to look.
- **Fire loads before 1942** are built up from analogues. No tonnage of woodwork removed was found for any navy.
  Kostenko's *On the Orel at Tsushima* and Campbell's *Jutland* would help.
- **Growth by era** (the ×2 for varnished wood, ×1.3 for linoleum) is judgement, anchored only to ISO room tests by
  lining class (Class D wood panelling flashes over in 2–10 min) [SRC].
- **The record's timings** are scattered. Orel's 34 fires have no times. Times for Derfflinger, Lützow, Warspite and
  Kirishima weren't found.
- **The ship templates are generic,** with flat compartments of 45–90 m². Real ships have small cabins and big
  messdecks. The flashover threshold falls per m² as rooms grow, so subdivision changes the results.
- **Smoke abandonment rates** (1/8 min, 1/60 min) are set to Savo (fireroom out in ~13 min), not measured.

---

## Sources

**Tests and physics**
- NRL ex-USS Shadwell: [Phase V](https://apps.dtic.mil/sti/pdfs/ADA364776.pdf) · [Phase VI boundary cooling](https://apps.dtic.mil/sti/tr/pdf/ADA278790.pdf) · [Fog attack, NHRC 96-22](https://apps.dtic.mil/sti/pdfs/ADA319156.pdf) · [DC-ARM 1998](https://apps.dtic.mil/sti/tr/pdf/ADA376944.pdf) · [Ventilation, ADA285409](https://apps.dtic.mil/sti/pdfs/ADA285409.pdf) · [firedoc L3cy](https://firedoc.nist.gov/article/L3cyXYQBWEcjUZEYgJPv) · [firedoc 8Xcy](https://firedoc.nist.gov/article/8XcyXYQBWEcjUZEYY37u) · [IMAS 94 abstract](https://trid.trb.org/View/449578)
- NBS/NIST: [Berthing fires, NBSIR 76-1052](https://nvlpubs.nist.gov/nistpubs/Legacy/IR/nbsir76-1052.pdf) · [NBSIR 79-1714](https://nvlpubs.nist.gov/nistpubs/Legacy/IR/nbsir79-1714.pdf) · [Mattresses, NBSIR 77-1295](https://nvlpubs.nist.gov/nistpubs/Legacy/IR/nbsir77-1295.pdf) · [NISTIR 4961](https://nvlpubs.nist.gov/nistpubs/Legacy/IR/nistir4961.pdf) · [NISTIR 4499](https://nvlpubs.nist.gov/nistpubs/Legacy/IR/nistir4499.pdf) · [Gross & Robertson, NBS 8147](https://nvlpubs.nist.gov/nistpubs/Legacy/RPT/nbsreport8147.pdf)
- [Ship fire-hazard bands, DTIC ADA274791](https://apps.dtic.mil/sti/tr/pdf/ADA274791.pdf) · [USCG vessel space tests, ADA339207](https://apps.dtic.mil/sti/pdfs/ADA339207.pdf) · [USCG CPO berthing burn-outs](https://trid.trb.org/View/388432) · [Darwin 1988, Stark lessons (IMarEST)](https://library.imarest.org/record/8755/files/v31b2p07a.pdf) · [Law, steel vs aluminium](https://publications.iafss.org/publications/frn/675/-1/view/frn_675.pdf)
- [EN 1991-1-2 Tables E.3–E.5 (dnaop)](https://dnaop.com/html/62830_12.html) · [Parametric fire worked example](https://dan.ct.upt.ro/fire/files/ParametricFireCalc.pdf) · [infosteel SSB07](https://www.infosteel.be/images/Steel-Buildings-in-Europe/SSB07/24) · [Brandforsk 314](https://www.brandforsk.se/wp-content/uploads/2020/02/bf04_rapport_314_001.pdf) · [Fire protection fundamentals (t² classes)](https://pdhonline.com/courses/m282/FireProtectionFundamentals.pdf) · [Charring rates](https://designingbuildings.co.uk/wiki/Charring_rate)
- Paint: [Wake, MRL-TR-91-31](https://apps.dtic.mil/sti/pdfs/ADA243241.pdf) · [DTNSRDC paint thickness](https://trid.trb.org/View/393885)
- Water: [Iowa rate-of-flow formula](https://www.fireengineering.com/firefighting/iowa-rate-of-flow-formula-for-fire-control/) · [Keeping it simple (Grimwood rule)](https://www.firefightingincanada.com/keeping-it-simple-7494/) · [Särdqvist, Lund](https://lup.lub.lu.se/search/files/4546393/1049443.pdf) · [Gałaj et al. 2018](https://jau.vgtu.lt/index.php/JCEM/article/download/256/207) · [Fog nozzle history](https://www.firefighternation.com/leadership/the-history-of-the-fog-nozzle-indirect-fire-attack/) · [Fog attack for ship fires](https://www.fireengineering.com/firefighting/fog-attack-for-ship-fires/) · [USCG Liberty-ship tests 1944](https://www.fireengineering.com/firefighting/coast-guard-conducts-tests-on-engine-room-fires-part-iii/)

**Equipment and doctrine**
- [RN AFO 2031/44](https://seapower.navy.gov.au/sites/default/files/documents/2031.pdf) · [Carter & Hogger 1956 (IMarEST)](https://library.imarest.org/record/9949/files/v10b3p02a.pdf) · [D.K. Brown on Russo-Japanese War lessons (IMarEST)](https://library.imarest.org/record/8345/files/v36b3p16a.pdf) · [USN fire schools](https://boshw.us/?p=1064) · [USNI 1943, damage control](https://www.usni.org/magazines/proceedings/1943/november/damage-control-fun) · [Handy-billy](https://yms299.org/archives/explandict/handy-billy) · [A-1 rescue breather](https://www.ima-usa.com/products/original-u-s-wwii-navy-oxygen-breathing-rescue-apparatus-type-a-1) · [Sims, April 1917](https://www.history.navy.mil/research/publications/documentary-histories/wwi/april-1917/rear-admiral-william-17.html)

**The record**
- USN War Damage Reports: [Savo, WDR 29](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-quincy-ca39-astoria-ca34-vincennes-ca44-war-damage-report-no29.html) · [Boise, WDR 24](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-boise-cl47-war-damage-report-no-24.html) · [San Francisco, WDR 26](https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-sanfrancisco-ca38-war-damage-report-no26.html) · [South Dakota, WDR 57](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/uss-south-dakota-bb57-war-damage-report-no57.html) · [Destroyers: gunfire, bomb, kamikaze](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/destroyer-report-gunfire-bomb-kamikaze-damage.html) · [Franklin, WDR 56](https://ibiblio.org/hyperwar/USN/rep/WDR/WDR56/index.html) · [Summary of war damage 1941–42](https://history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/summary-of-war-damage-17-oct-1941-to-7-dec-1942.html) · [Escort carriers](https://www.ibiblio.org/hyperwar/USN/rep/WDR/ESCORT%20CARRIERS%20GUNFIRE,%20BOMB%20AND%20KAMIKAZE%20DAMAGE%20AND%20LOSS%20During%20World%20War%20II.pdf)
- Yalu: [sinojapanesewar.com](https://sinojapanesewar.com/yalu.htm) · [USNI 1895, The Battle of the Yalu](https://www.usni.org/magazines/proceedings/1895/july/battle-yalu) · [USNI 1895, professional notes](https://www.usni.org/magazines/proceedings/1895/october/professional-notes) · [McGiffin, Century Magazine](https://www.usni.org/sites/default/files/inline-files/Pages%20from%20The_Century_Illustrated_Monthly_Magazine.pdf)
- Santiago and Manila: [Sampson's report (NHHC)](https://history.navy.mil/research/publications/documentary-histories/united-states-navy-s/the-battle-of-santia/rear-admiral-william.html) · [Naval Gazing, SAW pt 7](https://navalgazing.net/Spanish-American-War-Part-7) · [pt 8](https://navalgazing.net/Spanish-American-War-Part-8) · [NHHC, Maine analysis (bunker fires)](https://history.navy.mil/research/publications/documentary-histories/united-states-navy-s/destruction-of-the-m/analysis-of-the-dest-0.html)
- Tsushima: [Semenov, Rasplata (Gutenberg)](https://gutenberg.org/files/57324/57324-h/57324-h.htm) · [USNI 1906, With the Baltic Fleet at Tsushima](https://www.usni.org/magazines/proceedings/1906/april/baltic-fleet-tsushima) · [Scientific American](https://www.scientificamerican.com/article/with-the-baltic-fleet-at-tsushima) · [Oslyabya (topwar)](https://en.topwar.ru/257763-broennosec-osljabja-v-cusimskom-srazhenii.html)
- WWI and after: [USNI 1925, Jutland lessons](https://www.usni.org/magazines/proceedings/1925/april/lessons-jutland-affecting-design-turret-armor) · [Queen Mary 1914 diaries](https://battleofjutlandcrewlists.miraheze.org/wiki/1000_Days-1914) · [Exeter at the River Plate](https://navyhistory.au/hms-exeter-at-the-battle-of-river-plate/3/) · [Liverpool torpedo](https://www.lancaster.ac.uk/staff/ecagrs/torpedo) · [Bismarck debriefing](https://kbismarck.com/archives/debriefing5.html) · [combinedfleet: Yamato](http://www.combinedfleet.com/yamato.htm) · [Pensacola (DANFS)](https://history.navy.mil/research/histories/ship-histories/danfs/p/pensacola-iii.html) · [Forrestal TDNH](https://navalsafetycommand.navy.mil/Portals/100/Documents/TDNH_USS_Forrestal_Fire_29JUL67.pdf) · [Stark at 30](https://news.usni.org/2017/05/17/the-attack-uss-stark-at-30) · [Samuel B. Roberts](https://news.usni.org/2015/05/22/the-day-frigate-samuel-b-roberts-was-mined)
- Loads and outfit: [Naval Gazing, Fire](https://www.navalgazing.net/Survivability-Fire) · [TIME, July 1943](https://time.com/archive/6766227/army-navy-rest-for-the-weary/) · [Deck coverings](https://www.shipcamouflage.com/3_10.htm) · [OS2U-3 specification](https://www.vought.org/products/html/os2u-3spec.html) · [Navy Special fuel oil (EPA)](https://www3.epa.gov/ghgreporting/help/tool2014/definitions/residual-fuel-5.html)
- Wikipedia (cited facts): [Laiyuan](https://en.wikipedia.org/wiki/Chinese_cruiser_Laiyuan) · [Reina Cristina](https://en.wikipedia.org/wiki/Spanish_cruiser_Reina_Cristina) · [Seydlitz](https://en.wikipedia.org/wiki/SMS_Seydlitz) · [Lion](https://en.wikipedia.org/wiki/HMS_Lion_(1910)) · [Good Hope](https://en.wikipedia.org/wiki/HMS_Good_Hope_(1901)) · [Emden](https://en.wikipedia.org/wiki/SMS_Emden_(1908)) · [Bretagne](https://en.wikipedia.org/wiki/French_battleship_Bretagne) · [Mikuma](https://en.wikipedia.org/wiki/Japanese_cruiser_Mikuma) · [Sheffield](https://en.wikipedia.org/wiki/HMS_Sheffield_(D80)) · [Glamorgan](https://en.wikipedia.org/wiki/HMS_Glamorgan_(D19)) · [Oriskany fire](https://en.wikipedia.org/wiki/USS_Oriskany_fire) · [Graf Spee](https://en.wikipedia.org/wiki/German_cruiser_Admiral_Graf_Spee)
