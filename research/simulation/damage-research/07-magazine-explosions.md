# 07 — Magazine Explosions: What Happens After Ignition
Status: final    Updated: 2026-10-06    Request: -

*Research for the damage model. Companion to [`02-components.md`](02-components.md) §1, which covers what ignites a magazine: the flash chain, propellant sensitivity, cook-off and the case list. This doc picks up at the moment the propellant is burning. It covers where the gas goes, what breaks, whether the ship splits, which piece comes off, and how the pieces sink.*

Tags follow the other damage docs: **[INFERRED]** marks my own reasoning or calculation, and **[UNCERTAIN]** marks conflicting or thin sources.

---

## 0. TL;DR for the model

1. **A magazine fire is a pressure race, not a switch.** Burning propellant makes gas, and the openings around the magazine let it out. The outcome depends on whether pressure stays low enough for the gas to vent (the flame column) or climbs past what the surrounding structure can hold (the explosion).
2. **The structure gives way at very low pressure compared with what the propellant can make.** A 15-inch gunhouse is held down only by its own weight. It lifts at about **1.3 bar**. Fully burned in a closed magazine, an aft magazine group's propellant would reach **150–600 bar** **[INFERRED, §2.2]**. Burning only a fraction of a percent of the propellant is enough to lift the turret. So the question is never *whether* structure fails. It is **which structure fails first, and whether that failure lets enough gas out.**
3. **Turret first = flame column and a burned-out turret** (Seydlitz, Derfflinger, Lion). **Decks, sides and bottom first = hull girder severed** (Hood, Mutsu, Invincible, Indefatigable).
4. **The break happens at the magazine.** Where the magazine sits along the hull decides which piece comes off:
   - forward group (0.15–0.35 L) → **bow comes off**
   - midships turret → **two halves**
   - aft group (0.65–0.85 L) → **stern comes off**
5. **After the break, each piece floats or sinks on its own.**
   - A short end piece with intact compartments trims down at its open end and stands up. It can stay afloat for minutes to hours: Mutsu's stern lasted about 14 h, Pommern's stern at least 20 min.
   - A long piece with flooded machinery capsizes quickly.
6. **Several magazines going off together destroys the midships section entirely,** leaving two intact ends and a debris field (Vanguard, Queen Mary).
7. **Turrets are not fastened down.** Gas lifts them, and capsizing tips them out of the ship. Turrets thrown overboard, or lying upside down on the bottom, are historically correct.

---

## 1. Case data: what the wrecks show

| Ship (date) | What went up | Where along hull | Structural result | Pieces / sinking behaviour | Source |
|---|---|---|---|---|---|
| **Seydlitz** (Dogger Bank 1915) | Charges in transit, two aft turrets; RP C/12 in brass cases | aft | **No explosion.** Both turrets burned out, 159 dead. Magazines flooded by hand | Ship survived | [02-components](02-components.md) §1.3 |
| **Derfflinger** (Jutland 1916) | Caesar and Dora turrets | aft | Burned and did not explode. 73 of 78 and 80 of 80 dead | Survived | von Hase via 02 |
| **Lion** Q turret (Jutland) | Charges in working chamber and hoists, about 28 min after the hit | midships | Flame "masthead high". The flooded magazine held; its doors buckled | Survived | 02 §1.3 |
| **Indefatigable** (Jutland) | X magazine, then forward magazines at 16:03 | aft, then forward | **X detonation blew off about 40 m of stern.** She settled by the stern with a list to port. The forward explosion threw "large pieces… 200 feet into the air" | Two halves **more than 500 m apart** on the seabed | [Wikipedia](https://en.wikipedia.org/wiki/HMS_Indefatigable_(1909)) |
| **Queen Mary** (Jutland) | Forward 4-inch magazine, then forward main magazine(s); a second explosion aft as she rolled | forward (near foremast) | **Broke in two near the foremast** | Wreck in **3 sections**: the forward two are smashed, the **aft end is inverted and relatively complete**. One turret lies upside down | [Wikipedia](https://en.wikipedia.org/wiki/HMS_Queen_Mary); [Divernet](https://divernet.com/scuba-diving/general-wrecks/big-guns-of-jutland/) |
| **Invincible** (Jutland) | Q (midships) magazine; possibly a low-order explosion in X | midships | Shell "penetrated the front of Q turret, **blew off the roof** and detonated the midships magazines, which **blew the ship in half**". Sank in about 90 s | **Bow and stern stood up out of the water** with the middle on the bottom (about 55 m depth). Stern upright, bow inverted. A twin 12-inch turret lies upside down | [Wikipedia](https://en.wikipedia.org/wiki/HMS_Invincible_(1907)); [Divernet](https://divernet.com/scuba-diving/general-wrecks/big-guns-of-jutland/) |
| **Defence** (Jutland) | Aft 9.2-inch magazine, then the 7.5-inch magazines in turn, the fire running **along the ammunition passages** | aft → along ship | Chain of explosions; lost with all hands | The wreck was found **largely intact** (McCartney, 2001) | [Wikipedia](https://en.wikipedia.org/wiki/HMS_Defence_(1907)) |
| **Pommern** (Jutland) | 17 cm magazine, after a torpedo hit | — | "A tremendous explosion broke the ship in half" | **The stern capsized and floated for at least 20 min with its propellers in the air** | [Wikipedia](https://en.wikipedia.org/wiki/SMS_Pommern) |
| **Vanguard** (Scapa 1917) | 4-inch magazine fire (unstable cordite) → P and/or Q magazines | midships (two turrets) | **"The amidships portion of the ship is almost completely gone."** P and Q turrets were thrown about **40 m**. A 6 × 4 ft plate landed on Bellerophon | **Bow and stern largely intact** | [Wikipedia](https://en.wikipedia.org/wiki/HMS_Vanguard_(1909)) |
| **Hood** (Denmark Strait 1941) | Aft 4-inch magazine → aft 15-inch magazines | aft | Flame burst out "from the vicinity of the mainmast", **vented through the engine-room ventilators**. Gas went "out through the sides of the ship as well as forward and upwards via the engine room vents, **expelling the aft main battery turrets and causing the stern to be detached**" (Jurens et al. 2019). Sank in about 3 min; the **bow rose nearly vertical** | Stern section, inverted midships section and bow remains. Midships starboard side gone "down to the inner wall of the fuel tanks and the plates of the hull are **curling outward**". Bow missing just forward of A turret: either a forward magazine partial explosion, or (Jurens) **a bending failure as the bow stood vertical** **[UNCERTAIN]** | [Wikipedia](https://en.wikipedia.org/wiki/HMS_Hood) |
| **Barham** (1941) | 4-inch magazines → 15-inch magazines | aft | Exploded about 4 min after three torpedo hits, **while lying on her side** | Sank at once | [Wikipedia](https://en.wikipedia.org/wiki/HMS_Barham_(04)) |
| **Arizona** (1941) | Forward magazines (black powder possibly first), about 7 s after a bomb hit | forward | Forward interior structure destroyed. **Turrets and conning tower dropped 25–30 ft.** Foremast and funnel fell forward. The blast vented **through the sides** | Settled on the harbour bottom | [Wikipedia](https://en.wikipedia.org/wiki/USS_Arizona_(BB-39)) |
| **New Orleans** (Tassafaronga 1942) | Forward magazines, after a torpedo hit | extreme bow | **Lost about 1/3 of the ship including the bow** | **Survived** and steamed stern-first to port | [03-small-vessels](03-small-vessels.md) §2.6 |
| **Mutsu** (1943, at anchor) | No. 3 turret magazine | aft-midships | **Cut in two in calm water** | **150 m forward piece: machinery flooded, it capsized to starboard and sank almost at once. 45 m stern piece: upended and floated until about 02:00 the next day (~14 h)** | [Wikipedia](https://en.wikipedia.org/wiki/Japanese_battleship_Mutsu) |
| **Roma** (1943) | No. 2 turret magazine, after a Fritz X burst in the forward engine room | forward | **No. 2 turret "blown over the side"**; she capsized and **broke in two** | Sank by the bow | [Wikipedia](https://en.wikipedia.org/wiki/Italian_battleship_Roma_(1940)) |
| **Yamato** (1945) | One bow magazine **at about 120° of roll** | forward | **Main turrets fell out as she capsized** | Two main pieces: the **bow third** and the stern | [Wikipedia](https://en.wikipedia.org/wiki/Japanese_battleship_Yamato) |
| **Gneisenau** (1942, dock) | 23 t of RP C/32 | forward | "No explosion"; the **turret was displaced** | Ship not lost | 02 §1.2 |

### Patterns

- **Burn-out vs explosion is mostly decided by propellant and packaging,** not by how many tonnes are present. German brass-cased charges burned. British cordite in silk bags, with Clarkson cases opened, exploded. §2.3 gives a mechanism for this.
- **Every single-magazine detonation in a battleship-sized ship severed the hull girder.** Partial survivals are cruiser *ends* (New Orleans).
- **Where the ship breaks = where the magazine is.** Indefatigable, Hood and Mutsu (aft) lost their sterns. Queen Mary, Roma and Yamato (forward) lost their bows. Invincible (midships Q) broke into halves.
- **Two or more main magazines going off together removes the middle** (Vanguard). The ends survive as intact pieces.
- **Gas leaves through every opening:** barbettes (turret roofs blown off or turrets thrown), engine-room vents (Hood), sides (Hood, Arizona) and through the decks upward. Debris is thrown high and far.
- **Secondary (4-inch) magazines next to the main magazines start the fire** in Queen Mary, Hood, Barham and Vanguard. Connecting ammunition passages carry it along the ship (Defence).
- **A ship heeled over or capsized can still explode:** Barham on her side, Yamato at about 120°, Kongō. Turrets fall out.
- **Pieces that broke away float in a characteristic way.** The **short end piece floats upended**, closed end up (Mutsu stern ~14 h, Pommern stern 20+ min, Hood's bow briefly). **The long piece with flooded machinery capsizes** (Mutsu forward piece).
- **A wreck in shallow water can stand on the bottom with an end showing:** Invincible in about 55 m, with ~170 m length and two halves of ~85 m each.

---

## 2. Mechanism

### 2.1 Sequence

```
ignition (flash down hoist / shell burst / fire / cook-off)
  → deflagration in the magazine: burning rate rises with pressure
  → pressure rises in the magazine space
  → weakest boundary fails → new vent area → pressure relief or not
       ├─ vent big enough early (turret lifts / flash path open / magazine flooded)
       │     → pressure plateaus low → sustained flame column out of barbette/vents
       │     → turret crew dead, turret burned out, fires; hull girder intact
       └─ vents too small for the gas made
             → pressure keeps climbing → decks, sides, bottom and transverse bulkheads fail
             → hull girder cross-section removed at the magazine station
             → (possible transition to detonation in shells, bursters, black powder → much more local damage)
             → neighbouring magazines ignite (through bulkheads, ammunition passages, hoists)
  → hull bending moment > residual strength → break
  → each piece floods, trims and sinks on its own
```

This is a **deflagration**, not a detonation: the propellant burns very fast but does not set off a shock wave through itself. NATO MSIAC work on magazine fires describes the same feedback. A transition from burning to detonation "takes place due to the increased rate of deflagration burning as a result of increasing internal magazine Quasi Static Pressure", and "venting is vital for reducing… response violence" ([MSIAC O-213](https://www.msiac.nato.int/publication/o-213-magazine-loading-density-detonation-estimation/)). The Hood inquiry's expert witnesses described the aft explosion in the same terms: "a violent — but not instantaneous — deflagration" venting through the engine-room ventilators ([Wikipedia — Hood](https://en.wikipedia.org/wiki/HMS_Hood)).

Pre-war British trials missed the problem because "no tests of confined cordite were made" ([Naval Gazing](https://www.navalgazing.net/There-Seems-To-Be-Something-Wrong-With-Our-Bloody-Ships-Today)). Cordite burning in the open is a flame column. Cordite burning in a closed space is a bomb.

### 2.2 Order-of-magnitude numbers [INFERRED]

These are my own back-of-envelope figures, for tuning scale only.

| Quantity | Value | Basis |
|---|---|---|
| Propellant per 15-inch round | ~194 kg cordite | NavWeaps 15"/42 full charge (428 lb) |
| Aft group (2 turrets × 2 guns × 120 rpg) | **~93 t** | Hood-like |
| Propellant "impetus" (energy per kg as gas pressure × volume) | ~1 MJ/kg | Typical gun propellant 0.95–1.15 MJ/kg |
| Closed-volume pressure if all burned, `P ≈ f × m/V` | 1,500 m³ → **620 bar**; 3,000 m³ → **310 bar**; 6,000 m³ → **155 bar** | Ignores heat loss and venting |
| Pressure that lifts a 15-inch gunhouse | `W·g / A_barbette` = 750 t × 9.81 / 57 m² ≈ **1.3 bar** | Revolving weight ~750 t **[UNCERTAIN ±15%]**, barbette ~8.5 m diameter |
| Propellant that must burn to reach 1.3 bar | **~0.2–0.8 %** of the group, roughly **one to four charges** | |
| Flooding-design head of a transverse bulkhead | ~1 bar (about 10 m of water) | Bulkheads are designed against water, not gas |

**Conclusion:** the turret, hatches and unarmoured bulkheads all fail almost as soon as the fire starts. What matters is **how fast the propellant burns compared with how fast gas can escape**, not how much propellant there is in total.

### 2.3 The venting balance (core model idea) [INFERRED]

The burning rate of a propellant rises with pressure: `r = β·Pᵅ`, with α typically about 0.7–0.9.

- **Gas made:** `ṁ_gen ∝ S_burn · β · Pᵅ`, where `S_burn` is the exposed burning surface.
- **Gas vented:** gas escaping through an opening at choked flow goes roughly as `ṁ_vent ∝ A_vent · P`.
- **Balance:** with α < 1 the two curves cross at a stable plateau pressure:

```
P* ≈ K · (S_burn / A_vent)^(1/(1−α))
```

The exponent `1/(1−α)` is about **3–10**. A small change in the ratio of burning surface to vent area therefore gives a **huge** change in plateau pressure. This is the knife-edge between Seydlitz and Queen Mary.

| Factor | Effect on S_burn / A_vent |
|---|---|
| **Brass cases** (German main charge) | Only exposed fore-charges burn at first, and cases ignite one by one. **Low S** → low P* → vents through the turret |
| **Silk bags; Clarkson cases opened before battle; extra charges stowed in handling rooms** (RN 1916) | Everything ignites almost together. **High S** → high P* |
| **Propellant sensitivity** | Cordite: a flash that lights 1 unit of US single-base lights **75 units** of cordite ([Naval Gazing](https://www.navalgazing.net/There-Seems-To-Be-Something-Wrong-With-Our-Bloody-Ships-Today)). That means fast flame spread across the whole stow |
| **Flash path open to the turret** (doors propped open) | More vent area, but also more ignition. Net: worse, because ignition comes first |
| **Magazine deep under an armoured deck, below the waterline** | Small A_vent until something gives way. Water outside backs up the bottom and sides |
| **Flooded or sprinkled** | Lowers S_burn towards 0 (Lion) |
| **Fill level** | Ammunition already fired is propellant that can't burn. Late in a battle, magazines are less dangerous |

When P* goes above a boundary's failure pressure, that boundary fails and **adds its area to A_vent**. If the new P* stays above the next boundary's failure pressure, the failures **run on** through the ship. That cascade is the explosion.

### 2.4 Which boundaries fail, in order [INFERRED]

The thresholds below are illustrative tuning values, not engineering ratings.

| Boundary | Rough failure pressure | Vent area it opens | Consequence |
|---|---|---|---|
| Flash doors, scuttles, hoist trunks | < 1 bar | small | Flame into handling rooms and turret |
| **Turret gunhouse lift-off** | ~1–2 bar (weight / barbette area) | **large (whole barbette)** | Flame column. With more pressure, the turret is thrown (Vanguard 40 m, Roma overboard, Invincible "roof blown off") |
| Unarmoured transverse bulkheads, ammunition passages | 1–3 bar | into the next compartments | **Spreads to neighbouring magazines** (Defence, Hood 4-inch → 15-inch) and machinery spaces |
| Vents, uptakes, engine-room trunks | — (already open once the neighbouring space is reached) | medium | Flame from vents and funnels far from the turret (Hood's mainmast column) |
| Armoured deck and its beams | ~5–15 bar | very large | Decks lifted and peeled. **Hull girder top flange lost** |
| Side shell and torpedo-defence bulkheads | ~5–15 bar (backed by water below the waterline) | very large | Plates **curled outward** (Hood). Sides lost |
| Bottom shell | highest (backed by water) | — | Usually the last to go. Keel broken |

**Arizona** shows what happens when the supporting structure goes: the turrets *dropped* 25–30 ft because the decks and bulkheads under them were destroyed. Model that as a possible result too, not only the turret flying upward.

### 2.5 Detonation tier

True detonation (a supersonic shock through the material) is more likely in:

- **HE-filled shells or burster charges** in shell rooms next to the burning magazine
- **black-powder primers and saluting charges** (Arizona, Mutsu theories; 02 §1.2)
- **torpedo warheads and depth charges** (03 §2.5)
- propellant at **very high loading density and confinement** (MSIAC)

Detonation does more local damage (brisance: plates shattered rather than torn) and is less sensitive to venting. In the game it should be a **multiplier on hull damage and debris**, with little effect on flame-column duration.

---

## 3. Hull girder: when does the ship break?

### 3.1 Model

A ship is a long, hollow beam. The deck, the sides, the bottom and the longitudinal bulkheads form its cross-section. A magazine sits right in that cross-section, under the deck and above the bottom. A cascade (§2.4) removes some fraction `d` of it.

```
Z_res(x)      = Z(x) · (1 − d)                      # residual section modulus at station x
M_cap(x)      = σ_u · Z_res(x)                      # ultimate moment the section can carry
M_load(x)     = M_sw(x) + M_wave(x) + M_whip
break if M_load(x) > M_cap(x)
```

**Still-water moment `M_sw(x)`.**
- Heavy turrets near the ends, supported by fine-ended buoyancy, usually put a battleship in **hogging** (the middle pushed up, the ends sagging). **[INFERRED]**
- Use roughly `M_sw_max · sin(πx/L)`, or better, integrate weight minus buoyancy from the parametric generator. [`hull-weight-model.md`](../hull-weight-model.md) already holds the weight distribution.

**Wave moment `M_wave(x)`.**
- Shape: classification-society practice (IACS UR S11) uses a factor of 1.0 over 0.4–0.65 L, tapering linearly to 0 at the ends.
- Size: scale with sea state.

**Whipping `M_whip`.**
- The gas impulse also shakes the whole hull. This is the same whipping effect as an underwater explosion.
- Model it as a short spike, a multiple of the design moment, lasting about 1 s.

**Design margin.**
- A sound hull has `M_cap ≈ 2–3 × (design M_sw + M_wave)` **[INFERRED]**.
- So: `d < ~0.5` → survives in calm water; `d ≈ 0.5–0.8` → breaks in a seaway or under whipping; `d > 0.8` → **breaks even at anchor** (Mutsu).

**Gap.** If the cascade destroys a *length* of hull, not just a section, delete that segment entirely: Indefatigable lost ~40 m of stern; Vanguard's midships were "almost completely gone". The result is two end pieces plus debris.

### 3.2 Why location matters

- **Midships magazine (Q turret).** The highest bending moment acts on the damaged section, so it is certain to break. The two pieces are similar in size and both may stand up (Invincible).
- **Quarter-length magazines (0.2–0.35 L, 0.65–0.8 L).** The moment is lower but the cross-section is wiped out, so the ship still breaks.
  - The **short piece** includes the end. It is usually 40–60 m (Mutsu 45 m, Indefatigable ~40 m).
  - The long piece keeps the machinery, which is now open to the sea at the break.
- **Extreme-end magazine (cruiser forward magazine at < 0.12 L).** The end comes off but the bending moment there is near zero, so the **main hull can survive** with a collision bulkhead's worth of flooding (New Orleans).
- **A secondary magazine on its own** (outboard 4-inch). It is a smaller charge, more vented and off-centre. It might only breach the side, but in the historical cases it was **almost always the trigger** for the main magazine beside it. Model the link.

### 3.3 Separation behaviour

Once broken, the pieces can tear apart at once, or stay hinged on the bottom plating or keel for some seconds. When they hinge, they fold into a V with both ends rising before they part. This is the "break in two" mode in [`sinking-vfx-research.md`](../../vfx/sinking-vfx-research.md).
- Give the hinge a residual strength that **decays with time and with relative rotation**.
- The pieces separate when the hinge is exhausted.
- Separated pieces drift apart on the bottom: Indefatigable's halves lie more than 500 m apart, Mutsu's "a few hundred feet".

---

## 4. Each piece after the break

Treat every piece as its own floating body: mass and centre of gravity from its own weights, buoyancy from its own compartments. Then:

1. **Open the cut face.** Compartments at the break, and anything on the cascade path, flood at once.
2. **Trim towards the open end.** The intact far end rises.
   - A short end piece with sealed compartments **upends and floats** for minutes to hours, closed end up: Mutsu stern ~14 h, Pommern stern 20+ min "propellers jutting into the air", Hood's bow nearly vertical.
3. **A long piece with machinery open to the sea loses stability, not just buoyancy.** It **capsizes** and goes down quickly (Mutsu's forward 150 m "capsized to starboard and sank almost immediately").
4. **Turrets fall out once the heel passes roughly 90°** (Yamato). Gunhouses are held by gravity and roller-path clips only. Spawn them as separate falling bodies; they land inverted near the wreck (Invincible, Queen Mary).
5. **Vertical pieces carry bending loads that weren't there before.** Hood's missing bow just forward of A turret may be exactly this kind of failure (Jurens). Option: a second, weaker break check on an upended piece.
6. **Depth check.** If `piece_length × sin(pitch) > water_depth`, the piece **rests on the bottom with its end above water** (Invincible). This is great in shallow seas (North Sea, Jutland, harbours) and impossible in deep water (Hood at 2,800 m).
7. **Explosions can happen after capsize** (Barham, Yamato, Kongō): power is lost, pumps can't flood the magazines, and fire or friction ignites the propellant. Keep the magazine model running while the ship sinks. An inverted ship vents upward *through its bottom*, which gives a big plume at the sinking point.

Output to VFX matches [`sinking-vfx-research.md`](../../vfx/sinking-vfx-research.md) §5: a per-segment pose `(s, θ, φ)` with the pivot at the break, plus per-compartment flooded fraction.

---

## 5. Visual and audio cues tied to the mechanism

| Model state | Cue | Reference |
|---|---|---|
| Low plateau, venting through the barbette | Sustained **flame column** above the turret ("masthead high"), several seconds to tens of seconds; turret blackened and silent | Lion, Seydlitz |
| Turret lift | Gunhouse hops and drops askew, or flies (velocity scales with the pressure excess). Roof plates spin off | Invincible "roof blown off", Gneisenau displaced, Vanguard 40 m, Roma overboard |
| Cascade into machinery | **Flame from vents, uptakes and funnels** far from the turret. Hood's column rose "from the vicinity of the mainmast" | Hood |
| Hull girder severed | Brown-black cordite smoke column (hundreds of metres); **debris thrown 60 m up**, plates landing on neighbouring ships | Indefatigable "200 feet"; Vanguard plate on Bellerophon |
| Side failure | Shell plating curled outward at the break | Hood wreck |
| Two pieces | V-fold, then end rise. Propellers turning in the air on the stern piece | Pommern, Mutsu |
| Post-capsize explosion | Bottom-up plume at the sinking point | Barham film, Yamato |

Survivors: catastrophic cases leave only single digits alive (Indefatigable 2–3, Invincible 6, Queen Mary ~9, Hood 3). A burn-out kills the turret crew and the magazine and handling-room crews, roughly 70–160 men.

---

## 6. Proposed implementation

### 6.1 Per-magazine data (extends the 02 / damage-model-research magazine block)

```
Magazine {
  station_x            # along-hull position (0..1 L)
  group_id             # magazines in one group share a burning space
  propellant_type      # → sensitivity, alpha, beta, impetus f
  packaging            # bag | clarkson_open | clarkson_closed | brass_case → S_burn factor
  fill                 # 0..1, drops as the guns fire
  volume_m3
  flood_state          # dry | sprinkled | flooding(t) | flooded
  neighbours[]         # (magazine_id, link_type: bulkhead|passage|hoist, failure_p)
  boundaries[]         # (type, failure_p, vent_area, hull_section_fraction, x_extent)
  detonables[]         # shell room HE, black powder, torpedo warheads → detonation tier
}
```

### 6.2 Resolution step (a few seconds of sim time at ~10 Hz)

```
on ignite(mag):
  burning = {mag}; A_vent = mag.flash_path_area; d_section = 0
  loop each tick:
    S = Σ burning.fill · packaging_factor · sensitivity · (1 − flood)
    P = K · (S / max(A_vent, ε)) ^ (1/(1−α))           # plateau approx; or integrate ODE
    for b in sorted(boundaries of burning, by failure_p):
        if P > b.failure_p and not b.failed:
            b.failed = true; A_vent += b.vent_area
            d_section(b.x) += b.hull_section_fraction
            if b is turret: launch_turret(v ∝ sqrt(P − b.failure_p))
    for n in neighbours: if P > n.failure_p: ignite(n) with delay (s)
    if any detonable reached and P > P_ddt: detonation → d_section ×k, debris ×k
    if propellant exhausted or P stable below all thresholds: end (burn-out)
  hull_break_check(d_section(x), sea_state, whip_spike)
```

- **Integrate rather than solve for the plateau** if you want timing: Hood's "violent but not instantaneous", Lion's 28-minute delay before charges in transit ignited, Arizona's 7 s after the bomb. The ODE is `dP/dt = (f·ṁ_gen − c·P·A_vent·…) / V`. It is cheap.
- **Randomise per event:** spread failure thresholds by ±30 %, and draw packaging/ignition simultaneity with some variance. This gives the right mix of outcomes: German ships *usually* burn out, British 1916 ships *usually* explode, and neither always.

### 6.3 Outcome tiers (for UI, logging and AI)

| Tier | Condition | Result |
|---|---|---|
| **0 — Contained** | Flooded or sprinkled before P > turret lift | Turret out, fire, casualties in the turret |
| **1 — Burn-out / flame column** | Turret lifts, P plateaus below deck and side thresholds | Turret destroyed, crew dead, heavy fire; **ship survives** if fires are fought |
| **2 — Local structural** | Some deck or side fails, d < ~0.5 | Large breach and flooding; break only in a seaway or with later bending (needs damage control) |
| **3 — Severed** | d > ~0.8 at one station, or `M_load > M_cap` | Two pieces, behaviour per §4 |
| **4 — Disintegration** | Two or more groups, or detonation tier | Midships gone, two end pieces plus debris; turrets thrown |

### 6.4 Tuning anchors (sanity tests the model should reproduce)

1. Seydlitz or Derfflinger setup (brass cases, doors partly open) → tier 1 in most runs.
2. Lion setup (magazine flooded, charges in transit) → tier 1 with delay; magazine doors buckle and hold.
3. Hood or Indefatigable setup (cordite, aft group, 4-inch magazine beside it) → tier 3, stern detached, sinks in minutes; flame from midships vents.
4. Invincible setup (midships Q, 55 m depth) → tier 3, both halves standing on the bottom.
5. Mutsu setup (calm water, at anchor) → tier 3 even with no wave moment. Stern floats for hours; forward piece capsizes.
6. Vanguard setup (two adjacent groups) → tier 4, midships gone, ends intact.
7. New Orleans setup (cruiser, extreme-bow magazine) → bow lost, ship survives.
8. Barham or Yamato setup (capsized, no power) → explosion during or after capsize; turrets fall out.

---

## 7. Open questions

- **Turret revolving weights and barbette diameters by class.** NavWeaps has most of them; pull them into the generator so the lift pressure comes out of the design rather than a constant.
- **Burn-rate parameters (β, α) for period propellants** are not in the sources read here. Treat them as tuning values and fit them to the anchors in §6.4.
- **How much the Jutland losses were due to design vs procedure** is still debated (02 §1.3). The model handles this by making packaging and doors explicit inputs.
- **Hood's bow failure** — a forward magazine partial explosion, or bending when vertical? Supporting both in the model costs nothing.
- **Rough hull-section fractions per boundary type.** Derive them from the generator's section (deck, side and bottom areas × thickness) rather than fixed numbers.

## Sources

- [Wikipedia — HMS Hood](https://en.wikipedia.org/wiki/HMS_Hood) (incl. Jurens et al. 2019 summary, 2001 wreck survey)
- [Wikipedia — HMS Indefatigable](https://en.wikipedia.org/wiki/HMS_Indefatigable_(1909))
- [Wikipedia — HMS Queen Mary](https://en.wikipedia.org/wiki/HMS_Queen_Mary)
- [Wikipedia — HMS Invincible](https://en.wikipedia.org/wiki/HMS_Invincible_(1907))
- [Wikipedia — HMS Defence](https://en.wikipedia.org/wiki/HMS_Defence_(1907))
- [Wikipedia — SMS Pommern](https://en.wikipedia.org/wiki/SMS_Pommern)
- [Wikipedia — HMS Vanguard (1909)](https://en.wikipedia.org/wiki/HMS_Vanguard_(1909))
- [Wikipedia — HMS Barham](https://en.wikipedia.org/wiki/HMS_Barham_(04))
- [Wikipedia — USS Arizona](https://en.wikipedia.org/wiki/USS_Arizona_(BB-39))
- [Wikipedia — Japanese battleship Mutsu](https://en.wikipedia.org/wiki/Japanese_battleship_Mutsu)
- [Wikipedia — Italian battleship Roma](https://en.wikipedia.org/wiki/Italian_battleship_Roma_(1940))
- [Wikipedia — Japanese battleship Yamato](https://en.wikipedia.org/wiki/Japanese_battleship_Yamato)
- [Divernet — Big Guns of Jutland (wreck dives)](https://divernet.com/scuba-diving/general-wrecks/big-guns-of-jutland/)
- [Naval Gazing — There Seems To Be Something Wrong With Our Bloody Ships Today](https://www.navalgazing.net/There-Seems-To-Be-Something-Wrong-With-Our-Bloody-Ships-Today)
- [NATO MSIAC — O-213 Magazine Loading Density Detonation Estimation](https://www.msiac.nato.int/publication/o-213-magazine-loading-density-detonation-estimation/)
- Project docs: [`02-components.md`](02-components.md) §1, [`03-small-vessels.md`](03-small-vessels.md) §2.6, [`sinking-vfx-research.md`](../../vfx/sinking-vfx-research.md), [`hull-weight-model.md`](../hull-weight-model.md)
