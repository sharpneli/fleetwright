# 14 — Behind the armour: spall, scabs and plugs when the plate holds

*Research for the damage model, after `11_blast_confinement.md`, `12_explosive_fillers.md` and `13_fragmentation.md`. It answers the request
for plan step **E2** in `shells-armour-damage.md`: "Spall and a plug off thin or face-hardened plate near its limit: a
30–45° cone, 3–5 m, as a weaker burst inside (Tiger's roof)". That line was a guess. This note asks how big the cone
is, how far it reaches and how much it does, and adapts the land-warfare work on behind-armour debris (BAD) to it.*

*Tags as in the other damage docs: **[SRC]** sourced, **[FIT]** fitted here to the cases, **[EST]** judgement,
**[UNCERTAIN]** thin or conflicting sources.*

**Companion code:** `spall_ref.py` (pure Python, runs in 0.05 s). Every table in §3–§6 comes out of it.

---

## 0. Short answer

1. **The guess is about right on the cone and the reach. It is wrong on what the thing is.**
   - **The cone.** Land BAD measurements put the edge of the debris cloud at a half-angle of **30–50°**, and the
     speed falls to nothing by about 47° [SRC TNO]. Read the plan's 30–45° as a half-angle and it fits. Most of the
     pieces fly at **15–20°**, not on the axis.
   - **The axis.** It isn't the shot line or the plate's normal. It lies **halfway between them** [SRC TNO; the
     T-54 HESH trial's scab left at 30° to the normal from a 60° hit]. On a deck hit at 70°, the cloud goes down and
     forward at about 35° to the vertical.
   - **The reach.** Inside a ship, drag doesn't stop anything; the first bulkhead does. Casualties are set by how
     dense the cloud is. A near-limit hit by a gun of 8″ or more disables half the people **out to 3–4 m** on its
     densest ray, and one in ten **out to 7–10 m** (§5). So "3–5 m" is the 50% radius, not where the cloud ends.
   - **What it is.** It is **not a burst.** There's no gas, no shock, no fire and no bulkhead breach. It's
     **10–50 heavy pieces at 40–120 m/s**, plus one large piece. That suits the fragment path of `Effects` (E3,
     `13_fragmentation.md`), masked to a cone. The blast and burst path of 11 isn't the place for it.
2. **When it happens.** The trigger is a shell that stops in the plate, or shatters on it, with u = V/V_L between the
   spall onset u_s and the holing limit u_H. Above u_H the plate is holed, which is E1's case.
   - **Face-hardened** (KC, Class A): spall from u ≈ 0.85; holed from 0.94 [EST; SRC armor/00: holing 2–12% below the
     Navy limit].
   - **Ductile homogeneous** (STS, Class B, NCA, Wh): from 0.90 to 1.00. These plates bulge and tear. The USN wrote
     "no large splinters thrown back" into the Class B acceptance test [SRC O.S. 751].
   - **Brittle** (compound, cast, high-hardness, poor lots): from 0.75.
   - **On the range scale** this is a band of about **2–3 km just beyond the range where the belt starts to hold**
     (Iowa-type belt against the 16″ Mk 8: 27–29.5 km in this model, §2.3).
3. **How much.**
   - **The mass that leaves** is a disc of the back face about **D + T across and 0.2 T deep**, growing with u through
     the band. That is **5–200 kg**, from a 6″ to a 16″ shell. HESH scabs run 0.17–0.25 T deep and 1.3–2.4 T across
     [SRC]. One of Okun's test plates threw a broken plug 17″ wide off 8″ of plate [SRC].
   - **The speed** is **40–120 m/s** on the axis. HESH scabs measured 36–75 m/s [SRC].
   - **The pieces** are **flakes of 50 g to 5 kg**, each carrying 0.1–20 kJ. The standard hazardous threshold is
     79 J [SRC], so nearly every piece off a heavy plate is a casualty-maker.
   - **The plate behind:** the flakes go through about **1–4 mm** of mild steel (thick-plate chunks about 10 mm,
     extrapolated, §5.2). So they wreck people, instruments, cables, pipes and light joiner work, and stop at
     structural bulkheads.
   - **The big piece** (the plug, or the scab, 7–110 kJ) wrecks whatever single item it hits: Tiger's rangefinder,
     Dunkerque's run-out cylinder, the final drive in the British HESH trial on a Tiger II.
4. **Contact bursts on a plate they can't hole throw almost nothing behind.**
   - A naval shell bursting on a face it can't hole (thicker than about 0.156 calibre, Okun) dents and cracks the
     plate. It throws under 4 kg, at under 80 J a piece (§3.3).
   - HESH is the land exception because it spreads its charge across the plate. Naval shells don't.
   - **Tiger's Q roof was not a plate that held.** The shell burst at the edge of the centre sighting hood and "blew a
     large hole in the roof plate" [SRC Dreadnought Project]. The roof's own plate was blown inboard.
   - That case, **a contact burst holing a plate at an opening, joint or edge**, is real and nasty. It reproduces
     Tiger's 9 casualties (§5.3). It belongs with the edges-and-joints step (P, "later") and with E2.
5. **The model** (§6) is one more effect after `Terminal`:
   - **The cloud:** from u, the plate class and the geometry, a mass, piece count, speed, axis and half-angle.
   - **Its effects:** crew casualties by the number of pieces per steradian; parts in the cone hit by pieces; one
     heavy hit near the axis; a flash roll where charges lie in the open.
   - **Where it stops:** the first boundary of 5 mm or more, or a backing layer.
   - **Cost:** no allocation, a few µs. The plan's "weaker burst inside" is dropped.
6. **Backing matters.** The ironclads' teak, a splinter bulkhead and a spall liner all catch exactly this debris
   [SRC: land spall liners exist for it]. P7's `armour.backing` should cut the cloud.

---

## 1. What comes off the back of a plate

### 1.1 Mechanisms

| Mechanism | When | What leaves the back | Source |
|---|---|---|---|
| **Spall / scab** (tensile failure) | The compressive wave from the impact reflects off the free back face as tension. Where the tension beats the spall strength (RHA about 6 GPa, Bless), a layer parts | A disc ("scab") about 0.2 T thick, often broken into flakes | [SRC FOI-R-1678] |
| **Plugging** | Blunt, or nose-broken, shell on plate of T/D about 0.3–1. Shear bands round the nose | A cylindrical or conical plug, D at the face and wider at the back | [SRC Okun test log: "conical plug thrown into backing"; "broken plug 17″ wide thrown" off 8″] |
| **Back bulge and cracking** | Ductile plate near its limit | Usually nothing: a bulge that tears open (8″: "3.5″ × 21″ back bulge, split open 9″ wide") | [SRC Okun] |
| **Discing / petalling** | Thin plates (T/D under about 0.25) | Petals bent inward. Pieces only where they tear off | [SRC general BAD literature] |
| **Shatter on the face** | Uncapped or decapped shell on face-hardened plate | The shell's pieces mostly outside. "The AP shattered and did little to the plate" | [SRC Okun] |

**Face-hardened against homogeneous.**
- A face-hardened plate's hard face cracks, and its soft back is punched out as a plug [SRC Okun].
- A ductile homogeneous plate bulges and tears. That is why the USN's Class B (homogeneous) acceptance test penalised
  plates that threw back "large splinters, roughly half a calibre across and near the plate's thickness long". It
  allowed only "relatively small pieces … punched out by a blunt projectile nose" [SRC O.S. 751].
- So spall was a known plate fault, and the good WWII homogeneous plates were made to avoid it.

### 1.2 The limits that bracket it

The project's armour research already has the ladder (armor/00 §1):

| Limit | Criterion | Relation |
|---|---|---|
| Army / protection | A witness screen behind is holed, by spall or pieces | 25–40% below Navy in velocity, small arms [SRC armor/00] |
| **Holing (HBL)** | A calibre-sized hole; the shell broken | FH: 2–12% below Navy |
| **Navy (NBL)** | The whole shell through | Reference (Thompson's V_L) |

**E2 lives between the protection limit and the holing limit.**
- The 25–40% gap is a small-arms figure, where a witness screen catches a single flake.
- At naval scale the plates were tested for the shell, not the flakes, so there's no measured onset.
- The u_s values in §2 are therefore estimates **[EST]**. They are bracketed by:
  - the small-arms gap;
  - the FH holing gap;
  - the Class B criterion (ductile plates spall late).

---

## 2. When: the band

### 2.1 Stopped shells

`s = (u − u_s) / (u_H − u_s)`, clamped to 0–1. Here `u = V/V_L`, the striking speed over the Navy limit at the hit's
obliquity, after the shatter rule if the shell shatters.

| Plate class (material `fragments`) | u_s | u_H | Big piece | Why |
|---|---|---|---|---|
| `fh`: KC, Class A, Harvey, VH | 0.85 | 0.94 | 50% | Cracked face, soft back plugs; holing 2–12% below NBL [SRC] |
| `ductile`: STS, Class B, NCA, Wh, PO, NVNC | 0.90 | 1.00 | 50% | Bulges and tears; O.S. 751 [SRC] |
| `brittle`: compound, cast, high-hardness, poor lots | 0.75 | 0.95 | 30% | Back flakes freely [EST] |
| `mild`: structural mild and HT steel | 0.90 | 1.00 | 50% | Dishes and petals |

`brittle` is a fourth class beside the plan's three (`mild`, `hard`, `fh`). The compound plates of the 1880s and the
cast turret armour of some navies need it. Wrought iron can sit in `mild`; its backing (§4.3) matters more.

### 2.2 Other triggers

- **A shell that shatters on the face** (P2's +30–40% at normal): use u against the *shattered* limit. Shatter spends
  the energy on the shell, so the cloud is smaller. It agrees with "the AP shattered and did little to the plate"
  [SRC Okun].
- **A contact burst on an intact face** (HE, or a fuze that fires in the plate), the plate not holed:
  - Okun: a contact detonation "barely" holes 0.156 calibre of STS, and dents and cracks plate up to 20% thicker
    [SRC armor/04 §4.3].
  - Scab window: t_h to 1.5 t_h, t_h = 0.156 D (HE) or about 0.10 D (AP's small filler) [EST].
  - It throws little (§3.3).
- **A contact burst at an opening, joint or edge** (a sighting hood or port, a roof joint, within about 1 D of an
  edge): the plate is holed from outside, and the hole's own plate goes inboard (§3.4). Tiger's Q turret.
- **A heavy ricochet off a thin roof** (T/D under about 0.4): the plate or its fixings can be driven in.
  - Dunkerque at Mers-el-Kébir: a 15″ shell glanced off the 150 mm roof of turret II above the right-hand gun. It
    "shoved in the armor plate". The dislodged armour "destroyed the run-out cylinder for the right gun", and
    propellant in the right half caught fire [SRC Wikipedia/Jordan & Dumas].
  - Thompson's V_L at roof obliquity is far above V, so the u rule never fires here. It needs its own roll: about 30%
    for a shell of D ≥ 2.5 T glancing off a roof, giving a cloud at s = 0.5 with d_s = 2D [EST, one case].

### 2.3 Where on the range scale

An Iowa-type belt (307 mm Class A, 19° incline, Q 0.92) against the 16″ Mk 8, beam-on (`spall_ref.py` §8):

| Range | u | Behind the belt |
|---|---|---|
| ≤ 25.6 km | ≥ 1.0 | Holed or perforated (E1, or a burst inside) |
| 26.5 km | 0.97 | Holed (FH holes from 0.94) |
| 27.4 km | 0.93 | Spall, s = 0.92 |
| 28.3 km | 0.90 | Spall, s = 0.53 |
| 29.3 km | 0.86 | Spall, s = 0.15 |
| ≥ 30.2 km | ≤ 0.83 | Nothing |

- **So the spall band is the first 2–3 km of the belt's immune zone.**
  - The ±6% per-plate and ±3% per-shot scatter (P7) spreads it further.
  - So does the plate's thickness varying over its height.
- **Decks show the mirror image:** spall in the last 2–3 km before a deck is defeated.
- **That is a real share of hits on armour at the ranges battleships fought at.** Most of it went unrecorded, for two
  reasons:
  - Behind a belt there is usually a bunker, a void or a wing tank, and nobody was in it.
  - Behind a turret face, a barbette or a conning tower there were people, and those are the cases in the record (§5.3).

---

## 3. How much comes off

### 3.1 The land data, adapted

| Source | Case | Mass | Speed | Angle |
|---|---|---|---|---|
| TNO PML 1997-A65 (Verolme) | Tungsten and steel KE rods through 10–20 mm RHA, 1.4–2.4 km/s | Plate debris behind ≈ 0.9 × the hole's mass (about 20% goes back out the front) | Axis ≈ the cloud's front; `V(θ) = V₁·cos(1.92θ)` | Edge at 30–40°, 50° at the highest speed; densest at 0.4 of the edge; axis deflected by half the obliquity |
| Wijk / Børvik tests (FOI-R-1678 table 9) | .30 AP M2 through 6–10 mm RHA and high-hardness plate at 845 m/s | 3–15 pieces, 1–2 g in all | — | "Cone" 41–68° (half or full angle unclear) **[UNCERTAIN]** |
| Plasan | Steel armour, generic | — | — | Spall cones of 30–60° |
| tankarchives: 120 mm HESH on T-54/55 RHA at 60° | Scab off 140 and 150 mm plate | 200 × 190 mm, 20–25 mm thick; 250 × 285 mm, 30 mm deep; one piece 5.3 kg | 36 m/s (the piece at 43°), 75 m/s (at 30°) | 30° and 43° from the plate's normal |
| tankhistoria: 165 mm HESH on Tiger II | 150 mm glacis, 100 mm turret front, 80 mm side | 27.6–33.6 kg glacis scabs (15.5 × 13 × 1.5 in); 109 kg off the 80 mm side | Not given; scabs "up to 50% of the explosive's velocity" | Wrecked the final drive, the main gun's breech, the turret race bolts |
| Okun's plate-test log | Naval AP near the limit | "Broken plug 17″ wide thrown" (8″ plate); "large back spall" (6″); "conical plug" | — | — |

**What carries over:**
- **The geometry.** The edge at about 45°, densest at 18°, the axis halfway: all are measured.
- **The scab's depth** of about 0.2 T.
- **The speed scale.** For KE perforation the debris is fast because the penetrator carries on. A plate that *held*
  is the HESH-like case: the back face flies at tens of m/s.

### 3.2 The game's numbers

| Quantity | Rule | Tag |
|---|---|---|
| Disc across | d_s = D + T (stopped shell); max(2T, D) (contact scab) | [EST, from the conical plug and the HESH scabs] |
| Disc depth | t_s = 0.2 T | [SRC HESH 0.17–0.25] |
| Mass | m = s · ρ · π/4 · d_s² · t_s | [EST] |
| Big piece | 50% of m once s ≥ 0.5 (30% brittle), at 0.6 × the axial speed | [EST] |
| Flakes | t_s × 1.5 t_s × 1.5 t_s, the rest of the mass | [EST] |
| Axial speed | 0.25 · V cos(Ob) · (0.4 + 0.6 s) (stopped); 40 + 60 s (contact) | [EST, against HESH 36–75] |
| Speed by angle | v(θ) = v_ax · cos(1.92 θ) | [SRC Verolme] |
| Edge, peak | 45°, densest at 18° | [SRC TNO] |
| Axis | Halfway between the plate's normal and the shot line | [SRC TNO, HESH] |

**Near-limit clouds** (s = 0.75, `spall_ref.py` §3):

| Shell on plate | Mass | Big piece | Flakes | v_ax | KE / flake | r50 | r10 |
|---|---|---|---|---|---|---|---|
| 6″ on 76 mm STS, 10° | 3.7 kg | 1.8 kg (1 kJ) | 29 × 62 g | 56 m/s | 0.10 kJ | — | — |
| 8″ on 127 mm KC, 20° | 13 kg | 6.4 kg (4 kJ) | 22 × 289 g | 59 | 0.5 | 2.8 m | 7.3 m |
| 12″ on 229 mm KC, 10° | 60 kg | 30 kg (33 kJ) | 18 × 1.7 kg | 77 | 5.1 | 3.9 | 10.0 |
| 14″ on 152 mm STS deck, 65° | 36 kg | 18 kg (9 kJ) | 37 × 0.5 kg | 53 | 0.7 | 4.0 | 10.2 |
| 16″ on 152 mm STS deck, 70° | 44 kg | 22 kg (7 kJ) | 44 × 0.5 kg | 42 | 0.4 | 3.8 | 9.8 |
| 16″ on 305 mm KC, 15° | 143 kg | 71 kg (58 kJ) | 18 × 4 kg | 67 | 9.0 | 3.9 | 10.0 |

- r50 and r10 are the distances at which a person on the densest ray is a casualty with 50% and 10% chance (§5.1).
- **The 6″ case does almost nothing:** its flakes carry about 100 J, near the 79 J threshold.
- **From 8″ up the numbers converge.** A heavier shell makes fewer but bigger pieces, and the 50% radius sits near
  3–4 m whatever the gun. That is why a single "3–5 m" guess was not far off.

**By severity**, a 15″ Greenboy on 330 mm KC (V_L 409 m/s):

| u | s | Mass | Big | Flakes | v_ax | r50 | r10 |
|---|---|---|---|---|---|---|---|
| 0.84 | 0 | — | — | — | — | — | — |
| 0.87 | 0.22 | 46 kg | — | 9 × 5 kg | 45 m/s | 2.8 m | 7.1 m |
| 0.90 | 0.56 | 114 kg | 57 kg (41 kJ) | 11 × 5 kg | 63 | 3.1 | 7.9 |
| 0.93 | 0.89 | 183 kg | 91 kg (114 kJ) | 18 × 5 kg | 83 | 3.9 | 10.1 |

### 3.3 Contact bursts on a plate they don't hole

`spall_ref.py` §4. An 11″ HE on 64 mm throws 0.6 kg at about 40 J a flake. A 14″ HE on 76 mm throws 3 kg at 0.1 kJ,
with r50 of zero. Above 1.5 t_h nothing comes off.
- This is consistent with Okun's "dented and cracked".
- It is consistent with the South Dakota barbette III hit: 1.5″ gouged, nothing reported inside [SRC 08 §3.3].
- It is consistent with Jean Bart's turret and glacis hits: "firing again the same day" [SRC 08 §3.7].

### 3.4 Contact bursts that hole the plate at a weak spot

The hole's own plate, radius R_hole = 0.45 W^⅓ √(6/t) (kept by 11 §3.1), goes half inboard at about 150 m/s, in
pieces about 0.5 T across [EST]:

| Case | Hole radius | Mass inboard | Pieces | r50 |
|---|---|---|---|---|
| 11″ APC (≈ 8 kg filler) on a 51 mm roof at a hood: **Tiger Q** | 0.31 m | 60 kg | 205 × 0.3 kg | 12.7 m (inside a 4–8 m gunhouse: everyone in the cone) |
| 11″ APC on 83 mm at a port | 0.24 m | 60 kg | 47 × 1.3 kg | 6.4 m |
| 14″ HC (45 kg) on 76 mm at a hood | 0.45 m | 190 kg | 196 × 1 kg | 12.9 m |

**This is the dangerous case, and the old guess's anchor.** The game's burst-outside rule (HE on plate ≥ 0.2 calibre
bursts outside) gives nothing behind today. At an opening it should give this cloud, plus the burst's own fragments
through the hole (E3).

---

## 4. Where it goes and what stops it

### 4.1 The cone

- **Axis:** halfway between the plate's normal (pointing inboard) and the shell's line [SRC TNO "deviates by roughly
  half the obliquity"; tankarchives: 30° from the normal for a 60° hit].
- **Spread:**
  - The pieces' density rises from the axis to a peak at 18° and falls to nothing at 45°.
  - Their speed falls as cos(1.92θ), so the edge pieces are slow as well as sparse.
  - About 70% of the pieces lie inside 30° and a third inside 20° (`spall_ref.py`'s density) [FIT].
- **Decks:** a deck hit at 70° obliquity sends its cloud 35° off the vertical, down and forward along the shell's
  heading. The space under an armour deck (machinery, magazines, the transmitting station) is what it reaches.

### 4.2 Reach

- **Drag doesn't matter.** A 50 g flake loses half its speed only after about 60 m of air (13's drag law, V₀·e^(−R/(247·m^⅓))). The cloud flies
  until it hits something.
- **It thins as 1/r².** A space is reached as far as its far wall. The pieces that do the harm thin out, so the
  hazard to a person (A = 0.56 m², the AASTP body area [SRC]) falls off as follows. This is the 15″ case, s = 0.75:

| r | On the 18° ray | On the 5° ray | On the 40° ray |
|---|---|---|---|
| 1 m | 1.00 | 1.00 | 0.65 |
| 2 m | 0.89 | 0.82 | 0.23 |
| 3 m | 0.63 | 0.53 | 0.11 |
| 4 m | 0.43 | 0.35 | 0.06 |
| 5 m | 0.30 | 0.24 | 0.04 |
| 7 m | 0.17 | 0.13 | 0.02 |
| 10 m | 0.09 | 0.07 | 0.01 |

### 4.3 What stops it

- **Flakes go through about 1–4 mm of mild steel** (THOR mild-steel limit, extrapolated beyond its 53 g and 180 m/s
  floor, §5.2). The 4–5 kg chunks off a 300 mm plate go through about 9–11 mm.
- **A splinter bulkhead or splinter deck of 15–25 mm stops the whole cloud.** So does a structural WT bulkhead of 6–10
  mm against all but the chunks.
- **Backing behind the armour catches it**, which is what backing was for:
  - the ironclads' teak;
  - Littorio's oak and US Class A's concrete (plan P7's `armour.backing`);
  - the inner skin.
  - The land equivalent is the spall liner, sold to shrink the cone of 30–60° [SRC Plasan, Permali, Rheinmetall].
  - Rule: a backing layer of total mass per area ≥ the scab's (ρ·t_s) absorbs the flakes, and the big piece only if it
    is ≥ 2× [EST]. Below that, thin the cloud by the ratio.

---

## 5. What it does

### 5.1 People

- **Threshold.** A piece of 79 J or more is "hazardous" [SRC AASTP-1/DDESB]. The Swiss blunt-injury model gives 2.8%
  lethal at 79 J and 37% at 300 J, weighted over the body [SRC]. Spall flakes are 0.1–20 kJ: past 1 kJ, nearly every
  hit incapacitates.
- **Expected hits** on a person at (r, θ): λ = N · f(θ) · A / r² (f the angular density per steradian). P = 1 − e^−λ.
  The big piece adds one more chance within 15° of the axis.
- **In a gunhouse** (one man per 2 m², the far wall at 4 m):

| Case | Casualties (model) | Record |
|---|---|---|
| Tiger Q: 11″ burst holes a 51 mm roof at the hood (§3.4) | **8.3** | 3 killed (the trainer, No. 4 of the left gun, the midshipman), 6 slightly wounded: **9** [SRC Dreadnought Project, Main Battery Report] |
| 15″ on a 330 mm KC face, u = 0.90 | 3.4 | — |
| 16″ on a 150 mm STS roof at 70°, u = 0.97 | 2.4 | — |
| 14″ HE on a 76 mm roof, contact, not holed | 0.1 | South Dakota III barbette, Jean Bart: nothing inside |

### 5.2 Parts

- **Flakes:**
  - the hits on a part are λ = N · f(θ) · A_part / r²;
  - a part fails if a hit's KE beats its fragility class (instrument or sight about 100 J; cable or pipe about 500 J;
    a machine's casing about 5 kJ) [EST].
- **What a flake goes through** (THOR mild steel limit, extrapolated, `spall_ref.py` §6):

| Flake | Speed | Area | Mild steel defeated |
|---|---|---|---|
| 50 g | 60 m/s | 4 cm² | ~1 mm |
| 420 g | 100 m/s | 18 cm² | ~4 mm |
| 2 kg | 120 m/s | 50 cm² | ~9 mm |
| 5 kg | 110 m/s | 100 cm² | ~11 mm |

- **The big piece:** one hit, on the part nearest the axis within 15°, of 7–110 kJ. That wrecks any instrument,
  mechanism or light machine.
  - Tiger: the rangefinder, periscope and officer's hood, and both loading cages jammed (one by sight fragments).
  - Dunkerque: the right gun's run-out cylinder.
  - The British HESH trial on a Tiger II: the final drive and the breech.

### 5.3 The naval cases

| Case | What hit what | Result | What it says |
|---|---|---|---|
| **Tiger, Q turret, Jutland** | German 11″ burst on the roof at the edge of the centre sighting hood (roof ~2″ from a photo, USNI) | "Blew a large hole in the roof plate"; pieces of roof armour blown inward. Hood, rangefinder and periscope destroyed, cages jammed, electrics lost; 3 killed, 6 wounded; the turret fought on in percussion firing [SRC] | §3.4: holed at an opening, not a plate that held |
| **Dunkerque, turret II, Mers-el-Kébir** | Hood's 15″ glanced off the 150 mm roof above the right gun | Plate "shoved in"; dislodged armour destroyed the right gun's run-out cylinder; charges in the right half caught fire and the men in it were asphyxiated; left half fought on [SRC] | §2.2: a ricochet push-in; a flash source |
| **Derfflinger, conning tower, Jutland** | Heavy shell, "failed to pierce" | "Huge pieces were torn out"; fragments wounded several, the navigator among them [SRC USNI] | Face spall from a thick plate near its limit, or through the slits |
| **South Dakota, barbette III, 1942** | Kirishima's 14″ | Gouged 1.5″; nothing inside [SRC 08] | u far below u_s: nothing |
| **Baden trials, 1921** | 15″ Greenboy against the 100 mm homogeneous roof, high obliquity | 4 of 5 failed to penetrate [SRC armor/07]; effects behind not recorded | — |
| **Shinano face plate, 1945 test** | 16″ Mk 8 against 26″ VH just under the limit | Holed the plate but the nose stopped at 21″; "numerous small cracks"; a "crack-off piece" could jam a turret [SRC navweaps tech-040] | Thick FH near the limit cracks rather than spalls |

**Battle record and gaps.** The battle record has spall in turrets, roofs and conning towers. Belt cases are missing,
which §2.3 explains. No source found measures a naval spall cloud. The naval cases fix *what* happens, and the land
data fix *how big*.

---

## 6. Proposed game model (E2 rewritten)

### 6.1 Inputs, all already in `Terminal` after P2

- **The shell:** D, V, the obliquity, its state (intact, decapped, shattered), and whether it stopped, ricocheted, or
  burst on the face.
- **The plate:** T, V_L (Thompson, P1a), the material's `fragments` class (`mild`, `ductile` for the plan's `hard`,
  `fh`, plus `brittle`), and the backing behind it (P7).
- **Openings:** the hitbox flags a hit within about 1 D of a port, hood or plate edge. This comes from the "edges and
  joints" step; until then, a component kind (`sighting_hood`, `port`).

### 6.2 The cloud

1. **The trigger, in order:**
   - **Stopped or shattered:** s from u (§2.1).
   - **Contact at an opening:** the punched case (§3.4).
   - **Contact on an intact face:** the scab (§2.2). It is small; v0 may skip it.
   - **A heavy ricochet off a thin roof:** a roll (§2.2).
2. **The cloud:** mass, big piece, flake count and mass, axial speed, axis (halfway), half-angle 45° (§3.2). That is
   eight floats on the stack.
3. **Backing:** thin the flakes by the backing's mass per area over ρ·t_s. Keep the big piece unless the backing is
   twice that (§4.3).

### 6.3 Its effects (in `Effects`, after the plate)

- **The space behind:**
  - It is the cell the path enters past the plate. The far boundary along the axis caps r.
  - Nothing passes a boundary of 5 mm or more, except flakes that THOR says get through (chunks off plates over about
    250 mm).
- **Crew:** casualties = crew density × ∫ P(r, θ) over the cone's slab up to the far wall (`casualties_in_cone`). This
  is a precomputed table by (N, m_flake, v_ax, r_wall) or a 3×3 quadrature. No per-person loop.
- **Parts:** for each part in the cell whose bearing from the hit lies within 45° of the axis, λ = N f(θ) A / r². Then
  roll against its fragility. The big piece goes to the nearest part within 15° of the axis.
- **Flash:** if the cone reaches charges in the open (a gunhouse or working chamber with bagged charges, a handling
  room), roll for ignition by the pieces' KE, about 20% at s ≥ 0.5 [EST]. Dunkerque lit; Tiger did not. Feed it to the
  magazine chain as a flash source, like E1's.
- **The combat log:** "spall: n pieces into <space>, 2 killed, rangefinder destroyed".

### 6.4 What it replaces and what it doesn't

- **It replaces:**
  - 08 §3.7's "spall cone 30–45°, 3–5 m" rule for thin roofs;
  - the plan's "weaker burst inside".
- **It doesn't replace:**
  - the non-penetrating heavy-hit table of 08 §3.7 (jams, sights, stunned crew, seam leaks), which is the plate and
    mount being *shaken*, not debris;
  - E1 (holed, broken shell: the shell's fragments through the hole). Above u_H the plug and the shell's pieces are
    E1's, and the plug's speed follows Recht–Ipson, V_r = m/(m+m_p)·√(V² − V_L²) [SRC armor/01].
- **It squares with 12's in-plate rule.** That rule gives "15% to the spaces behind (spall)" for a shell that bursts in
  the plate. With this model, an in-plate burst gives the scab or punched cloud by plate thickness, plus blast through
  the hole if holed. The flat 15% can go.
- **The matchup (`Matchup.cs`)** reads a spall term: P(spall band | range) × expected crew and parts lost behind
  turret faces, roofs and the CT. That is one number per (gun, plate) profile.

### 6.5 Tests (wide bands)

1. A 6″ shell stopped by a 3″ STS plate hurts nobody behind it.
2. A 15–16″ shell stopped near the limit by a turret face (u 0.88–0.93) disables 2–5 of a gunhouse crew and wrecks
   one or two instruments. The turret keeps firing.
3. A contact burst holing a 2″ roof at a sighting hood (Tiger) gives 5–12 casualties and wrecks the rangefinder.
4. A belt hit in the spall band with a coal bunker or wing void behind it does nothing to the ship's functions.
5. A heavy shell stopped by an armour deck in its spall band hits the machinery below (pipes, cables, gauges) but
   holes no bulkhead.
6. A thick backing (teak at least 2× the scab's mass per area) removes all but the big piece.
7. No allocation; replays hold.

### 6.6 Cost

Per hit that triggers it (a minority of armour hits): a handful of square roots and exponentials, a loop over the
cell's parts (already done for bursts), and one table lookup for crew. Well under the 200 µs per-hit budget.

---

## 7. Uncertainties

- **The onsets u_s** are estimates. Nobody published naval spall onset velocities; the plates were tested for the
  shell. They are the step's main tuning knob: a band of 0.80–0.92 is defensible for each class.
- **The axial speed** (0.25 × the normal striking speed at s = 1) is bracketed only by HESH scabs (36–75 m/s). A naval
  plug pushed by a stopping 16″ shell could be faster. Casualties are not very sensitive to it, because the flakes are
  far above 79 J anyway. What it changes is the THOR penetration of light bulkheads.
- **THOR** is extrapolated beyond its fragment range (heavier and slower).
- **The punched case** (§3.4): its 50% inboard share and 150 m/s are fitted to one case, Tiger, whose roof thickness
  (2–3.25″) and shell filler (≈ 8 kg) are themselves uncertain.
- **The land BAD geometry** comes from KE perforation at 1.4–2.4 km/s and from HESH. A slow plate failure may throw a
  wider, flatter cloud. The edge is 45° ± 10° **[UNCERTAIN]**.
- **Not found online:** the RN's post-Jutland turret-roof trials and the USN's BuOrd "back spall" plate data. The
  latter is mentioned as a Class B criterion, but the numbers aren't public. The FOI survey (FOI-R--1678--SE) and TNO
  PML 1997-A65 are the best open BAD sources. Arnold & Rottenkolber's vulnerability-model papers (IJIE 2001, 2008)
  are behind a paywall.

---

## Sources

**Behind-armour debris (land)**
- TNO PML 1997-A65, Verolme & Szymczak, *Behind-armour debris modelling for high-velocity fragments*:
  https://apps.dtic.mil/sti/pdfs/ADA341500.pdf
- FOI-R--1678--SE, *Behind-Armour Debris: modelling and simulation, a literature survey*:
  https://www.foi.se/rest-api/report/FOI-R--1678--SE
- Arnold & Rottenkolber, *Behind armor debris investigation and their application into a new vulnerability model*
  (IJIE 2001): https://www.sciencedirect.com/science/article/abs/pii/S0734743X01000719
- Yarrington, *Model for ballistic fragmentation and behind-armor debris* (IJIE 2000):
  https://ui.adsabs.harvard.edu/abs/2000IJIE...24..171Y/abstract
- EFP behind-armour debris distribution: https://www.sciencedirect.com/science/article/pii/S2214914719301096
- DRDC TM 2003-122, witness-plate hole growth: https://apps.dtic.mil/sti/pdfs/ADA426699.pdf
- Spall cones and liners: https://plasan.com/advanced-spall-liner-technology-protecting-infantry-fighting-vehicle-crews-from-ballistic-threats/
  ; https://www.permali.co.uk/land-defence-composites/spall-liner/ ; US 5402703 (liner system to reduce spall):
  https://patents.google.com/patent/US5402703
- HESH on T-54/55 armour: https://www.tankarchives.com/2018/01/120-mm-hesh-vs-125-mm-he.html
- HESH on a Tiger II: https://tankhistoria.com/experimental/tiger-ii-vs-hesh/

**Injury criteria**
- 79 J hazardous fragment, AASTP-1 and the Swiss blunt model (IESSE 2025):
  https://www.denix.osd.mil/ddes/denix-files/sites/32/2026/04/25IESSE-0029-Comparison-of-79-J-hazardous-fragment_REV1.pdf

**Naval plates and tests**
- Okun, *Armor-piercing shells versus armor* (test log: plugs, back spall, bulges):
  http://www.navweaps.com/index_nathan/apShellVsArmor.php
- Okun, *Projectile damage due to impact*:
  https://www.navweaps.com/index_nathan/Projectile%20Damage%20Due%20to%20Impact%20--%20An%20Overview.pdf
- O.S. 751 acceptance criteria: https://www.navweaps.com/index_nathan/os751BalTests.php
- Shinano turret face test: http://www.navweaps.com/index_tech/tech-040.php

**Cases**
- USNI Proceedings 1925, *Lessons of Jutland affecting design of turret armor*:
  https://www.usni.org/magazines/proceedings/1925/april/lessons-jutland-affecting-design-turret-armor
- The Dreadnought Project, *H.M.S. Tiger at the Battle of Jutland*:
  https://dreadnoughtproject.org/tfs/index.php/H.M.S._Tiger_at_the_Battle_of_Jutland
- French battleship Dunkerque (after Jordan & Dumas, *French Battleships 1922–1956*):
  https://en.wikipedia.org/wiki/French_battleship_Dunkerque

**Project docs:** `shells-armour-damage.md` (E2, P2, P7), `11_blast_confinement.md` (R_hole), `13_fragmentation.md`
(the burst's own fragments, the drag law, THOR per mass class), `12_explosive_fillers.md`
(the in-plate rule), `research/simulation/armor/` 00 §1 (the limits), 01 §3.10 (THOR) and §3.11 (Recht–Ipson), 04 §4.3
(contact bursts), `08_gunfire_effects.md` §3.3 and §3.7.
