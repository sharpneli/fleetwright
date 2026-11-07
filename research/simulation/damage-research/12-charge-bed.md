# 12 — A Magazine's Charges as a Packed Bed: How They Were Stacked, How Confined They Burn
Status: draft    Updated: 2026-11-07    Request: [REQUESTS.md "A magazine's charges as a packed bed"](../../REQUESTS.md) (the rule: [docs/battle/explosions.md "The stow's own confinement"](../../../docs/battle/explosions.md))

*How charges were stowed, and whether a stack of them can burn at a pressure above its room's. Little was found on stowage beyond the shapes of the packages, so most of this note is first principles, worked in a reference script and tagged. The answer to the main question goes against the rule as built.*

Tags as in the other damage docs: **[SRC]** sourced, **[INFERRED]** my reasoning or a fit, **[EST]** an engineering estimate with no direct source, **[UNCERTAIN]** thin or conflicting sources.

**Companion code:** [`charge_bed_ref.py`](charge_bed_ref.py) (pure Python, about 1.5 s). It has the stack bulks, Ergun's terms at both scales, the steady bed against the code's lumped form, a bed that holds its gas and moves, and the packages' own mouths. Every derived number below comes out of it. It uses the code's propellant rows (`Propellants.cs`) and its `MagazineHeatKeep` 0.5.

---

## Summary

1. **A stack of charges can't stay ahead of its room's pressure for longer than it takes to blow itself into the aisle: about 10-20 ms.** Nothing holds a charge in a rack against MPa-sized pressure differences; even 50 kPa of hold changes nothing [INFERRED, §4]. A 1.5 m bed of bags lit all at once peaks at about 5.5 MPa over its room 12 ms after lighting. Its face is choked: gas leaves the gaps at the speed of sound. By 15 ms the bed has stretched by half and is throwing charges across the aisle at about 150 m/s. It has burned **2.5% of itself** by then, against 0.03% at the room's pressure. Lit over 0.2 s (`MagazineBagSpreadS`'s range), the same bed gets no more than 0.2-0.4 MPa ahead and burns 0.5-1%. **To stay ahead for a charge's whole burn (1-15 s), a bed needs walls that hold: a few Pa to 120 Pa is all that inertia alone keeps** [INFERRED].
2. **So the rule as built overstates by a long way.** `BedPressure` holds Lion's Q at 3-17 MPa among the charges for the whole burn. The physics gives a transient of about a hundredth of a second, then the room's pressure. Two smaller errors in its algebra (§3.3): the obstacle is too big and the voids too loose (the drag's coefficient 7.7 times low), and the lumped form over-reads the steady pressure. Both are moot beside the first: the steady bed it solves doesn't exist outside a strong container.
3. **What a bed's pressure does do is throw the stack.** That is fast flame spread: burning charges at 30-250 m/s across a 1-1.5 m aisle (§4). The code's `MagazineBagSpreadS` is already its place in the model.
4. **The confinement that does hold is a package's own walls, and it is small** (§5). A lit RN case with its lid off runs at 0.2-0.4 MPa absolute in a 1 atm room (rate ×1.3-2.3). The near cartridge is thrown out of its case in about 60 ms. A German brass main charge lit inside runs at about 1.2 MPa (rate ×5) until its bundle is pushed out (55 ms). A USN tank with its lid off runs at 0.14 MPa. Each is near nothing once the room passes about 1 MPa. **One grain-scale effect is new: the bore of an RP C/12 tube** (8 mm, 1.23 m long) chokes its inner face's gas, at 0.8-3 MPa inside (that face's rate ×2-10 in a room at 0.1-0.3 MPa) [INFERRED, untested].
5. **How charges were stowed** (§1):
   - **RN main calibre:** two quarter charges (cartridges) to a metal magazine case with a lid at each end, racked lid end to the passage. Cartridges were drawn out and the empty case stayed in its rack [SRC: Harley, from the period drill books].
   - **"Clarkson's case" was something else:** a cork, leather and canvas case for carrying the smaller calibres [SRC: Harley]. So `Packaging.Clarkson` names the wrong thing for heavy guns. The BCF accounts' "Clarkson" (warshipprojects) are probably the same mix-up [UNCERTAIN].
   - **German:** the main charge in a brass case, 305 × 1,271 mm (30.5 cm), 70 kg empty, holding 91 kg; the fore charge in silk inside a tin [SRC: navweaps].
   - **USN:** bags in airtight tanks. Two tanks a charge from 8 to 14 in, 2½ for the 16"/45, 3 for the 16"/50 (3 bags of 110 lb each). Stowed horizontally in tiers and locked together by rings, with no racks; under 300 lb loaded [SRC: Naval Ordnance 1937; navweaps; Slover].
   - **Stack bulk, kg of propellant a m³ of the stack** (aisles and shells out) [INFERRED from the sizes, which are mostly EST]:

     | Packaging | kg/m³ |
     |---|---:|
     | RN cases | 520-630 |
     | German brass and tins | 500-590 |
     | USN tanks | 340-500 |
     | Bags piled loose | 580-770 |

     **`MagazineBedKgM3` 600 is a fair middle.**
   - **Depth:** a racked package's gas comes out of its own mouth, not through a bed. Only bags piled loose (stacks in handling rooms and passages) form a bed, as deep as the pile [INFERRED].
   - **No magazine plan of Lion, Queen Mary, Iron Duke or Seydlitz was found online.**
6. **Ergun's inertial term fits inside a bed, with the obstacle a charge's 6V/S** (§2):
   - Reynolds numbers of 10⁶-10⁷ put the viscous term 10⁻⁷ times below the inertial one.
   - Gun interior ballistics uses the same high-Reynolds limit, 1.75 with a tortuosity extension, with the grain as the obstacle (6V/S) and the bag as a membrane with a quadratic friction factor [SRC: Gough 1983].
   - At magazine scale the obstacle is the cartridge: 0.34 m for a 13.5 in quarter charge, not the whole charge's cube root (0.44 m).
   - The voids come from the bag's density (1.0-1.2 t/m³): 0.35-0.45, not 0.62.
   - Ergun's 1.75 has a range of about 0.9-4 for rough or irregular obstacles [UNCERTAIN, recalled]. It moves the transient's peak by only ±20%, because the face chokes first.
7. **Detonation: no case for the propellant itself at magazine bulks and confinement** (§6):
   - Burning to detonation in a porous bed needs a strong container and a dense, fine bed. Even secondary explosives at 45-70% of their solid density rupture plastic tubes rather than transit [SRC: Sandusky & Bernecker 1980].
   - Gun propellant beds burn convectively and blow their tube at its strength: 3 MPa thin, 75 MPa thick [SRC: Romo 2018].
   - Bulk M1, M6 and M30A1 inside an 8 in concrete pipe gave airblast of about 100% TNT, but "a somewhat less severe reaction" than a detonation on the witness plates [SRC: Swisdak 1994].
   - A ship's bulkheads are the thin plastic tube in that comparison.
   - The detonation tier stays with bursters and the black-powder igniters. A USN 16 in bag carried 0.36 kg of them, so an Iowa turret's magazines held about 1 t [SRC navweaps; INFERRED].

---

## 1. How charges were stowed

### 1.1 By navy and period

| Navy, period | Package | Stowage | Source |
|---|---|---|---|
| RN, BL heavy guns 1900-1945 | 4 cartridges (quarter charges) a round, each cord in a silk bag with a gunpowder igniter under a tear-off disc. **Two to a metal magazine case, lids at both ends** | Racks; the lid unlocked with a key, the cartridges drawn out, the case left in place. 4 men in the magazine, 5 in the handing room, at most 16 cartridges a minute. Lids "often found to be very loose" when cases were turned to the stowage position (Vanguard's court) | [SRC: Harley thread; Vanguard court para 18] |
| RN, secondary and transit | **Clarkson's case**: cork, leather and canvas, a cartridge in transit | Handing rooms, and after Jutland the secondary supply | [SRC: Harley; Brown via navweaps] |
| RN, KGV (1940): A turret | 821 cases (2 quarters each) in 8 magazines: 134-177 in the main ones, 20-66 in the wings and aft | Roller chutes from case to lift; rails take cases between magazines | [SRC: navweaps tech-122] |
| German, 28-38 cm, 1909-45 | Main charge in a brass case (30.5 cm: 305 × 1,271 mm, rim 350, 70 kg, 91 kg RP C/12); fore charge in a double silk bag (2.4 kg) in a tin | Magazines on the lower platform deck, over the shell rooms in battleships | [SRC: navweaps; Dreadnought Project "Direct Train", N.I.D.] |
| USN, 8 in and up, 1900s-45 | Bags in airtight tanks of steel, aluminium or copper (5 psi held 3 min). Two a charge for 8-14 in, 2½ for the 16"/45, 3 for the 16"/50 (3 × 110 lb) | Horizontally in tiers, locked by rings, the lowest tier to deck lugs; older tanks for racks. Iowa: 743-928 tanks a turret | [SRC: Naval Ordnance 1937 ch. XIV; navweaps 16"/50; Slover] |
| Pre-dreadnought (Colossus, brown prismatic) | Powder in cases | Wood-lined iron magazine tanks; 27 charges, 7,965 lb in her lower aft magazine | [SRC: Dreadnought Project] |

**Not found:** magazine plans or case dimensions for Lion, Queen Mary, Iron Duke and Seydlitz, or the RN case's size. Campbell's *Jutland*, Brown's *The Grand Fleet* and the ships' covers at the NMM are where they would be.

### 1.2 Sizes and bulk [EST unless marked]

The ref sizes the packages from the charge. Bag density is 1.0-1.2 t/m³ (cords tied in a bundle); 1.1 t/m³ gives a 13.5 in cartridge of 0.33 × 0.35 m and a 15 in one of 0.37 × 0.41 m. Each case is 2 cm wider and 5 cm longer than its pair. Racks are at 2-3 cm over the package's diameter, square (racks) or hexagonal (tiers).

| Package | kg propellant a package | kg/m³ of the stack, square-hex |
|---|---:|---:|
| RN 13.5 in, 2 × 33 kg in a case | 66.5 | 520-600 |
| RN 15 in, 2 × 48.5 kg in a case | 97 | 545-630 |
| German 30.5 cm main, brass case [SRC size] | 91 | 510-590 |
| German 30.5 cm fore charge, tin | 34.5 | 500-580 |
| USN 16"/50 tank, 3 bags | 150 | 340-400 |
| USN 14 in tank, 2 bags | 96 | 430-500 |
| Bags piled loose, voids 0.36-0.42 | — | 580-770 |
| A whole magazine room (Lion's Q: 29 t in 340 m³ with shells) | — | 85 |

**Depth:** for racked packages, the gas path to the passage is inside the package: 0.8-0.9 m (RN case), 1.27 m (German brass), 1.3-1.95 m (USN tank). Between packages there is no flow, because the packages are closed tubes. A bed forms only where bags lie loose: stacks in handing rooms and working chambers ("rate first"), and bags out of cases in passages (Bulwark, Defence). It is as deep as the pile, 0.5-2 m [EST].

## 2. The drag law

Ergun: dP/dx = 150·μ(1−ε)²u/(ε³d²) + 1.75·(1−ε)ρu²/(ε³d), with u = G/ρ the superficial velocity and d = 6V/S.

| Scale | ε | d, mm | k = 1.75(1−ε)/(ε³d), /m |
|---|---:|---:|---:|
| The code: a 133 kg charge, voids from cordite's density at 600 kg/m³ | 0.62 | 438 | 6.4 |
| Bags piled, a quarter charge 0.33 × 0.35 m, voids 0.40 | 0.40 | 336 | 49 |
| The same, voids 0.36 | 0.36 | 336 | 71 |
| Among the cords in a bag (MD45, across) | 0.30 | 17 | 2,540 |

- **Reynolds and the viscous term:** in the moving bed's peak (face choked at about 5.7 MPa) G is about 1,400 kg/m²s, so Re_p ≈ 10⁷. The viscous term is 10⁻⁷ of the inertial one. **Ergun's inertial term is the right one** [INFERRED].
- **Ergun's coefficient:** 1.75 is fitted to smooth spheres. Rough and irregular particles run higher (about 1.8 smooth to 4 rough, Macdonald et al. 1979) [UNCERTAIN: recalled, not rechecked here]. A bed only 3-5 obstacles deep, against a wall, channels and reads lower. **A range of 0.9-4 for the coefficient**; in the transient it moves the peak by -13% to +19% (ref §4, sensitivity).
- **The obstacle:** the gun codes (TDNOVA, Gough 1983) use Ergun's high-Re limit 1.75 with Andersson's tortuosity factor (0.3 at high porosity), bed porosity "typically of the order of 0.4", d = 6V/S of the grain, and the bag as a membrane with a quadratic friction factor [SRC]. At magazine scale **the obstacle is the cartridge (or tank, or case) by 6V/S, and the voids are the pile's (0.35-0.45)**.
- **Mach:** in a bed alight all at once the gas can't leave its own gaps below sonic speed until its pressure is several MPa. **The face chokes before Ergun binds** (ref §4: "choked" in every all-at-once row). The ref allows for this; the code doesn't.
- **The cords inside a bag** add their own rise. A quarter charge alone in the open is at 0.150 MPa among its cords in a 1 atm room (rate ×1.08), and gains the same in a room at 1 MPa. Small.

## 3. A bed held still

### 3.1 The steady bed

The ref solves the steady 1-D bed exactly: dG/dx = q(P), P·dP/dx = −k·RT·G², closed at the back, open (or choked) at the face. The pressure at the back, MPa absolute, MD45, all alight:

| Depth m | Room | Code lumped | Exact, code's k | Exact, piled bags |
|---:|---:|---:|---:|---:|
| 0.5 | 0.3 | 0.33 | 0.31 | 0.97 |
| 1.0 | 0.3 | 0.69 | 1.51 | 43 |
| 1.5 | 0.3 | 3.7 | 12 | 450 |
| 2.5 | 0.3 | 100 | 190 | 9,800 |

**None of these exists.** At 1.5 m the steady bed's gaps would need to hold about 0.6 m³ of gas a m² of face at hundreds of MPa: more than half of its propellant, made in no time. Meanwhile 1-50 MPa over the face throws the bed out of its rack in a few milliseconds.

### 3.2 The law's exponent

P ~ (k·L³)^(1/(2−2α)): L^6.8 for cordite (α 0.78), L^12.5 for single-base (α 0.88). At α ≥ 1 there's no steady bed at all. The knife edge is real, but only for a bed whose walls hold.

### 3.3 The code's algebra, for the record

- **The obstacle and the voids:** k is 7.7 times low against bags piled at 0.40 (§2).
- **The lumped form:** `P(P − room) = K·L·RT·(G/r)²·r²` takes the face's flux over the whole depth and the back's density. The exact profile has P_b² − P_f² = (2/3)·k·RT·G_f²·L for even burning, and the mass-mean pressure the charges see is below the back's. The face isn't choked in it.

These would matter only in a bed with walls.

## 4. A bed that holds its gas and moves

The ref's lumped bed, a m² of face against a bulkhead:
- The gas in its gaps is P = m·RT/V, with V = ε·L.
- It is made at r(P) by the share alight, and leaves through the bed (the exact Ergun form) and its face (choked through its gaps if need be).
- The bed is driven by P − room and stretches uniformly (a third of the column's mass moves), with racks holding `held` Pa.
- The steps converge: 4, 2 and 1 µs agree to 0.01%.

MD45 bags piled, voids 0.40, 660 kg/m³:

| Depth | Room MPa | Lit over | Peak over room, kPa | At ms | Open ms | Burned by then | Thrown m/s | Held still, 50 ms on |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1.0 | 0.3 | at once | 2,350 | 10 | 14 | 1.4% | 98 | 28 MPa |
| 1.0 | 0.3 | 0.2 s | 56 | 80 | 101 | 0.7% | 15 | 22 MPa |
| 1.5 | 0.1 | at once | 5,490 | 14 | 16 | 2.4% | 149 | 112 MPa |
| 1.5 | 0.3 | at once | 5,700 | 12 | 15 | 2.5% | 153 | 112 MPa |
| 1.5 | 0.3 | 0.01 s | 5,390 | 18 | 21 | 2.4% | 147 | 112 MPa |
| 1.5 | 0.3 | 0.2 s | 188 | 72 | 89 | 0.7% | 28 | 65 MPa |
| 1.5 | 1.0 | at once | 6,170 | 10 | 13 | 2.8% | 159 | 112 MPa |
| 2.5 | 0.3 | at once | 14,900 | 15 | 17 | 5.2% | 245 | 315 MPa |
| 2.5 | 0.3 | 0.2 s | 1,280 | 77 | 77 | 0.9% | 67 | 186 MPa |

- **Racks don't matter:** 50 kPa of hold changes nothing.
- **Over a whole burn:** what inertia alone keeps over a charge's burn (moving the column 0.15 m in 15 s at 1 atm, 3 s at 1 MPa, 0.9 s at 5 MPa) is 0.4, 10 and 120 Pa.
- **Other propellants:** single-base 7-perf in tank-sized bags and RP C/12 in brass-case-sized bundles behave the same: 2.0-2.4 MPa peaks, opening at 20 ms, 1.5-1.6% burned.
- **Sensitivity** (1.5 m, 0.3 MPa, all at once): voids 0.30 / 0.40 / 0.50 give 11-14 / 5-7 / 2-3 MPa peaks and 3.1-3.3 / 2.4-2.6 / 1.9-2.0% burned, across Ergun's coefficient of 0.9-4.
- **The held column** (the last column) is what self-confinement does with walls that hold: 20-300 MPa in 50 ms. That is the critical-height test's regime (§6). It is the gun chamber's, not the magazine's.

**The answer to "how far can a cool bed's pressure run ahead of its room's":** for a stack with a free face, **1-15 MPa for 10-20 ms if it is lit all at once, 0.05-1.5 MPa for about 0.1 s if lit over 0.2 s, then nothing.** Its lasting effect is **an extra 0.5-5% of the stack burned up front** (0.5-1% at the code's bag spread) **and the stack thrown across the room at 15-250 m/s.**

## 5. A lit package: its own mouth

A package alight inside, its gas out of its mouth (the lid off or blown), choked or subsonic. MPa absolute:

| Package | Room 0.1 | 0.3 | 1.0 | Hot gas (RT = f), room 0.1 | Wall's burst [EST] | Rate gain at 0.1 |
|---|---:|---:|---:|---:|---:|---:|
| RN 13.5 in case, lid off, both cartridges lit | 0.19 | 0.38 | 1.13 | 0.89 | 1.95 (1.2 mm) | ×1.3 |
| The same, the near cartridge gone | 0.11 | 0.31 | 1.03 | 0.13 | | ×1.0 |
| RN 15 in case, lid off, both lit | 0.38 | 0.45 | 1.19 | 1.86 | 1.76 | ×2.3 |
| German 30.5 cm brass case, lit inside | 1.19 | 1.19 | 1.44 | 4.8 | 5.7 (2.5 mm) | ×5.2 |
| German fore charge, tin lid off | 0.11 | 0.31 | 1.02 | 0.13 | 1.2 | ×1.0 |
| USN 16"/50 tank, lid off, 3 bags | 0.14 | 0.36 | 1.14 | 1.02 | 1.7 | ×1.1 |

- **How long the walls hold:** the near 13.5 in cartridge is thrown out of its case in about 60 ms, and a German main charge's bundle out of its brass case in 55 ms. A package's confinement lasts as long as its contents stay in. In that time a charge burns 0.03-0.1 mm of a 2.5-5.7 mm web.
- **A shut package:** it confines until its lid or seam gives. The USN tank's test is 5 psi held [SRC]; its lid will give at about 0.1-1 MPa [EST]. Then it is the table's row.
- **Hot or cooled gas:** "hot gas" is the gas before it has lost heat to the steel. In a package's first tens of milliseconds it is closer to the truth than the code's 0.5. Either way the package's own rise fades into the room's once the room passes about 1 MPa.
- **RP C/12's bore:** a tube 1,230 × 18/8 mm burning inside chokes its inner face's gas at its two 8 mm ends. Inside the bore the gas is at 0.8 MPa (RT = 0.5 f) to 3 MPa (hot), so the inner face burns ×3.7-10.6 in a 1 atm room, ×2-5.7 at 0.3 MPa, ×1.2-2.3 at 1 MPa. Friction along the bore would raise it further. This is the long-tube effect of interior ballistics. It would make a lit German charge burn out faster than the code's strand rate, in the wrong direction for Seydlitz [INFERRED, not checked against any trial].

## 6. When a stack goes over to detonation

| Evidence | Confinement | Bed | What happened | Source |
|---|---|---|---|---|
| Picric acid, tetryl, RDX/wax, 20-470 µm | Steel tubes against Lexan 25 mm | 46-71% TMD | Steel: transit after 72-147 mm of run. Lexan: tubes ruptured, no detonation (coarse tetryl the exception) | [SRC: Sandusky & Bernecker 1980, NSWC TR 79-79] |
| Granular beds, model | Closed both ends | 60% solids, 0.1 mm | HMX composite: 350 kpsi, shocks. Single-base: 25 kpsi, half the bed lit, no shock | [SRC: Hoffman & Krier 1980, model] |
| M1 7-perf, 16-22 cm beds | Polycarbonate 3 mm, 25 mm | 48-58% TMD | Convective burning; thin tubes vented at 3.1 MPa (part of the bed lit), thick at 75 MPa (all of it). No DDT reported | [SRC: Romo 2018] |
| M1, M6, M30A1, ~900 kg in shipping containers | Concrete pipe 2.1 m ID, 1.8 m tall, 8 in wall, 1.3 t lid | about 140 kg/m³ over the pipe | Airblast ~100% TNT, "approached a detonation", witness plates "somewhat less severe" | [SRC: Swisdak 1994] |
| M1 in drums, 8 m³ concrete magazine | Vented 79 or 39 cm | 15 and 63 kg/m³ | Peaks 6.5 and 234 kPa. Burning rate fitted ∝ P^1.94 in an unvented bed of 897 kg/m³ | [SRC: Bojko] |
| Critical height test | Schedule-40 steel pipe open at the top, flame at the bottom | the material's own height | "Go" when the pipe is damaged. Used to keep hopper loads below it, else hazard 1.1 | [SRC: Ford; ADA096248]. No propellant values found |
| MSIAC O-213 | a magazine | loading density → QSP | DDT if QSP drives the burn into runaway. For munitions passing fast cook-off, it needs loading densities "not practically achievable". Venting not counted | [SRC] |
| Ship magazines 1890-1945 | Bulkheads 6-25 mm, giving at 0.1-1.5 MPa | 85 kg/m³ over the room, 500-700 in the stack | Every cordite, RP and single-base case reads as a deflagration that vents (Hood: "violent, but not instantaneous") | [SRC: 07 §2.1] |

**By type** [INFERRED from the table]:
- **Cord and tube propellants (MD, RP), 5-18 mm grains:** no transit at any magazine bulk or boundary. The bed's permeability is huge, and the walls give at 0.1-2 MPa, three orders below the tens to hundreds of MPa a held bed would need.
- **Multi-perforated single-base (USN SPD, M1):** convective burning (the P^1.94 fit) and violent explosions in strong confinement (Swisdak), but not in a ship's boundaries.
- **Black powder:** igniters, primers and saluting charges detonate readily confined. This is the magazine's real path to detonation, with the shells' bursters (07 §2.5).

## 7. Recommendations for this codebase

1. **Take the steady bed out, or make it a transient.** `GasNetwork.BedPressure` holds the bed's pressure for the whole burn, which no stack with a free face can do (§4). Two ways:
   - **Cheapest:** `MagazineBedM` 0, the old model, plus a **kick**: when a pile lights, an extra share of it burns at once. The share is 1.3-1.7% at 1 m, 2.4-2.8% at 1.5 m and 5-5.6% at 2.5 m lit together, or 0.4-1.3% lit over 0.2 s (§4). Roughly 0.016·(L/1 m)^1.3 lit together [INFERRED fit] (by 1.5 m depth, `MagazineBagSpreadS`).
   - **Faithful:** the ref's lumped bed per stow: gas in the gaps, a choked face, the column's motion, three numbers a stow (m_g, s, v). When it has stretched by half it is gone and the stow burns at the room's pressure. The step needs to be about 10 µs while it lasts (about 20 ms); sub-step it inside `BurnStow`.
   - **Either way, apply it only to charges lying loose:** bags alone, stacks and passages. A racked package has no bed (§1.2).
   - **This takes back what moved Lion's Q to 1.05-1.10.** Under decision 3 nothing is to be tuned back in; the user's call.
2. **The flame spread is where a bed's pressure belongs.** A lit pile throws burning charges at 15-250 m/s across a 1-1.5 m aisle within about 0.1 s. That argues for `MagazineBagSpreadS` near its low end (0.2 s) for open stows. **It is a reason for a higher R in a magazine with lids off:** the cases opposite are struck by burning cartridges [INFERRED].
3. **If the bed is kept, mend its algebra:**
   - the obstacle a cartridge's 6V/S (0.30-0.40 m; a destroyer's charge 0.12-0.18), not the whole charge's cube root;
   - the voids from the bag's density (`ε = 1 − bulk/ρ_bag`, ρ_bag 1,000-1,200), 0.35-0.45;
   - the exact P_b² − P_f² form, and the face's choke;
   - Ergun's 1.75 kept, with a range of 0.9-4.
4. **`MagazineBedKgM3` 600** sits in the middle of the stowage (500-630 cases and brass, 340-500 USN tanks, 580-770 loose bags). Keep it, tagged [INFERRED from sizes].
5. **Rename `Packaging.Clarkson` to the RN magazine case** (metal, lids at both ends, two cartridges). Clarkson's was the transit case of the smaller calibres. The `MagazinePackageR` fit stands for what it was fitted to.
6. **A package's own rise** (§5) is ×1-2 on the rate for RN cases and USN tanks, ×5 for a German brass case lit inside, and fades once the room passes 1 MPa. The two candidates if a later pass wants it: a lit case with its lid off at its mouth's pressure until its cartridges are out (about 60 ms), and RP tubes' bore (×2-10 on the inner face in a cool room). **Neither is recommended until something in the record asks for it.**
7. **Leave detonation of the propellant out.** The detonation tier belongs to bursters and black powder. A later pass could count the igniters: USN 0.36 kg a 16 in bag; RN a gunpowder igniter a cartridge, size not found.

## 8. Open questions

- The RN magazine case's size and wall, and the racks' layout: Campbell's *Jutland*, Brown's *The Grand Fleet*, and the ships' covers or plans at the NMM.
- Whether BCF lids came off in the magazines or only in the handing rooms (still Friedman against Brown, 10 §8).
- Any trial of a burning stack of charges, 1916-1930: Jackson asked for "experiments with cordite in the magazines of some old ship"; no results found. The Vengeance (1917) and Prince of Wales (1919) trials were of handing-room doors (10 §3.1).
- "Hemus" in the request: no source of that name was found on magazine fires.
- Critical heights for any gun propellant.
- The RN igniter's mass.

## Sources

- Simon Harley, thread on RN cordite handling before Jutland, from the period drill books: [threadreaderapp](https://threadreaderapp.com/thread/1399365540913856525)
- [NavWeaps, 13.5"/45 Mark V](https://www.navweaps.com/Weapons/WNBR_135-45_mk5.php); [30.5 cm SK L/50](https://navweaps.com/Weapons/WNGER_12-50_skc12.php); [16"/50 Mark 7](https://www.navweaps.com/Weapons/WNUS_16-50_mk7.php); [tech-122, KGV's A turret magazines](https://navweaps.com/index_tech/tech-122.php)
- [Naval Ordnance 1937, ch. XIV, Ammunition (Slover)](https://eugeneleeslover.com/USNAVY/CHAPTER-XIV-AMUNITION-1.html); [USS North Carolina's powder magazine (Slover)](https://www.eugeneleeslover.com/US-NAVY-BOOKS/POWDER-MAGAZINE-NORTH-CAROLINA.php)
- [The Dreadnought Project, "A Direct Train of Cordite"](https://dreadnoughtproject.org/tfs/index.php/A_Direct_Train_of_Cordite); [Colossus's protection](https://dreadnoughtproject.org/tech/colossus/protection2.php)
- [Report of the Court of Enquiry into the Loss of Vanguard (gwpda)](http://www.gwpda.org/naval/a1373681.htm)
- [warshipprojects, "Washington Cherry Trees II"](https://warshipprojects.com/2017/09/25/washington-cherrytrees-2-ii/)
- P. Gough, TDNOVA multi-increment bagged charges, ARBRL-CR-00503 (1983): [DTIC ADA125482](https://apps.dtic.mil/sti/pdfs/ADA125482.pdf)
- Hoffman & Krier, fluid mechanics of DDT in packed beds (AFOSR 1980): [DTIC ADA090558](https://apps.dtic.mil/sti/pdfs/ADA090558.pdf)
- Sandusky & Bernecker, burning-to-detonation in plastic tubes, NSWC TR 79-79 (1980): [DTIC ADA094423](https://apps.dtic.mil/sti/pdfs/ADA094423.pdf)
- Romo et al., M1 convective combustion tests (IESS 2018): [NDIA](https://ndia.dtic.mil/wp-content/uploads/2018/intexpsafety/Romo.pdf)
- Bojko et al., M1 convective combustion CFD and the Kasun magazine (IESS 2018): [NDIA](https://ndia.dtic.mil/wp-content/uploads/2018/intexpsafety/BojkoPaper.pdf)
- M. Swisdak, TNT equivalence of M1, M6 and M30A1 (26th DoD Explosives Safety Seminar, 1994): [DTIC ADA514145](https://apps.dtic.mil/sti/pdfs/ADA514145.pdf)
- Critical height (mass) test: [Ford, in-process hazard classification](https://ndia.dtic.mil/wp-content/uploads/2018/intexpsafety/Ford3Paper.pdf); [DTIC ADA096248](https://apps.dtic.mil/sti/pdfs/ADA096248.pdf)
- [MSIAC O-213, Magazine Loading Density Detonation Estimation](https://www.msiac.nato.int/publication/o-213-magazine-loading-density-detonation-estimation/)
- Ergun (1952) and Macdonald et al. (1979) for the drag law's coefficients, recalled, not re-checked in this pass.
