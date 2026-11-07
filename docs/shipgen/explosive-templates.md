# Explosive templates

Written by `shipgen templates` from `src/Fleetwright.Shipgen/Explosives.cs`; don't edit by hand. The game's catalogue
is `Content/Templates/explosives.json`, from the same table.

A shell's `filler.explosive` names one; a design copies the ones it names into its `explosives`, and a filler can
override any number inline. Sources: `docs/battle/damage/12_explosive_fillers.md` §4.1 (re, in-plate, full
detonation, fire), `11_blast_confinement.md` §2.4 (heats, air, eta), `13_fragmentation.md` §2.1 and §3.1 (Gurney,
brisance). The sim reads `re` (the blast: W = filler kg × re) from S1 on; the rest ride along for P3 and phase E. As a
store (a torpedo's warhead; docs/battle/explosions.md EX6c), research 15 §1.1 and §3.2: Held's constant, what a fragment
must keep through the case (v²d, mm³/µs²; 0: nothing from outside sets it off), and its cook-off temperature.

| Explosive | From | re | Q det / comb MJ/kg | Air m³/kg | eta | Gurney m/s | Brisance | In plate | Full basic / CE | Fire | Held K | Cook-off K |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Black powder | 1860 | 0.55 | 2.9 / 2.9 | 0.00 | 0.55 | 640 | 0.15 | 0.15 | 1.00 / 1.00 | 1.00 | 0 | 0 |
| Wet guncotton | 1890 | 0.85 | 3.0 / 7.0 | 1.00 | 1.00 | 2100 | 0.90 | 0.08 | 0.55 / 0.85 | 0.20 | 0 | 0 |
| Lyddite | 1888 | 1.17 | 4.4 / 11.4 | 1.63 | 1.00 | 2550 | 1.15 | 0.35 | 0.95 / 0.97 | 0.40 | 26 | 530 |
| Shimose | 1893 | 1.17 | 4.4 / 11.4 | 1.63 | 1.00 | 2600 | 1.20 | 0.45 | 0.95 / 0.97 | 0.50 | 26 | 530 |
| TNT | 1902 | 1.00 | 4.5 / 15.0 | 2.66 | 1.00 | 2438 | 1.00 | 0.10 | 0.60 / 0.95 | 0.25 | 52 | 561 |
| TNT/wax | 1905 | 0.90 | 4.1 / 17.7 | 3.60 | 1.00 | 2300 | 0.90 | 0.04 | 0.55 / 0.93 | 0.25 | 52 | 561 |
| Explosive D | 1911 | 0.95 | 3.2 / 11.5 | 1.87 | 1.00 | 2350 | 0.90 | 0.02 | 0.50 / 0.92 | 0.20 | 115 | 530 |
| TNT/HND | 1912 | 1.10 | 4.6 / 13.5 | 2.30 | 1.00 | 2500 | 1.10 | 0.10 | 0.60 / 0.95 | 0.25 | 34 | 540 |
| Shellite | 1918 | 1.00 | 4.2 / 12.3 | 2.00 | 1.00 | 2450 | 1.05 | 0.00 | 0.60 / 0.90 | 0.35 | 38 | 530 |
| TNA | 1931 | 1.00 | 4.4 / 13.6 | 2.25 | 1.00 | 2440 | 1.00 | 0.05 | 0.60 / 0.93 | 0.25 | 52 | 561 |
| Amatol 60/40 | 1915 | 1.00 | 4.2 / 9.0 | 1.00 | 1.00 | 2150 | 0.85 | 0.10 | 0.60 / 0.93 | 0.15 | 95 | 530 |
| Amatol 80/20 | 1915 | 0.95 | 4.0 / 4.0 | 0.00 | 1.00 | 1900 | 0.65 | 0.12 | 0.55 / 0.90 | 0.10 | 95 | 530 |
| Torpex | 1942 | 1.50 | 5.6 / 16.0 | 2.00 | 1.00 | 2650 | 1.30 | 0.30 | 0.65 / 0.97 | 0.30 | 16 | 487 |
| RDX/TNT | 1943 | 1.35 | 5.0 / 12.0 | 1.70 | 1.00 | 2700 | 1.35 | 0.30 | 0.65 / 0.97 | 0.25 | 16 | 487 |
| Comp B | 1943 | 1.33 | 5.0 / 12.0 | 1.70 | 1.00 | 2774 | 1.41 | 0.30 | 0.65 / 0.97 | 0.25 | 16 | 487 |

- **Black powder:** Burns, never detonates: always 'full', its re prices it. Gurney 640 m/s (MSIAC L-295, 90th percentile). Few big slow pieces; the most fires.
- **Wet guncotton:** Nitrocellulose at 25-30% water (Russia 1890s-1905; warheads and mines to ~1915). Heats and air [EST]: dry NC's less the water. Under-initiated by weak primers: many duds. As a store nothing sets it off from outside, nor lights it while wet (15 §1.1: only its dry primer goes).
- **Lyddite:** Picric acid (also Melinite). in_plate 0.35 sourced (armor/04 §6, Jutland). Fine splinters. Held 26 [INFERRED], cook-off 530 K [EST].
- **Shimose:** Picric acid undiluted, lacquer and wax lined (Japan). Bore prematures and bursts on the water are 12's hidden stats, not modelled. Held and cook-off as lyddite's.
- **TNT:** Trotyl, Fp 02. Heats measured (LLNL 4.57, 14.96 MJ/kg), Gurney IATG 01.80. Needs a strong booster. Held 52 (cast) [INFERRED], cook-off 561 K [SRC recalled].
- **TNT/wax:** TNT with ~10% wax (Germany's Fp 5-20, the RN's post-war TNT/beeswax). Heats and air [EST]: TNT's with the wax's combustion. Held and cook-off TNT's [EST].
- **Explosive D:** Ammonium picrate (Dunnite, USN). re [EST, unsourced]; 11 has 0.90. The most impact-insensitive filler in quantity. Held 115, cook-off 530 K [EST].
- **TNT/HND:** TNT 60, hexanitrodiphenylamine 40: Germany's hexanite (torpedoes and mines from about 1912) and the IJN's Type 97 (from 1937). Between TNT and tetryl: re, heats, Gurney and brisance [EST]; Held 34 and cook-off 540 K [EST] (15 §1.1: the IJN's own sources disagree on its sensitivity).
- **Shellite:** Picric acid with dinitrophenol (RN Greenboy on). Full 0.90 with CE sourced; low-order without it until the late 1920s. Held 38 ("less sensitive" than lyddite), cook-off 530 K [EST].
- **TNA:** Trinitroanisole, Type 91 bakuyaku (IJN). re [EST, unsourced]. Needed a liner of 1/3-1/2 of the cavity. Held and cook-off TNT's [EST].
- **Amatol 60/40:** TNT 60, ammonium nitrate 40: the shortage filler. Heats and air [EST] between TNT and 80/20. Held 95, cook-off 530 K [EST].
- **Amatol 80/20:** Ammonium nitrate 80, TNT 20: about five times the shells per ton of TNT. Oxygen-balanced: no afterburn. Brisance 0.62 from NOL's fragments. Held 95, cook-off 530 K [EST].
- **Torpex:** RDX 42, TNT 40, aluminium 18: the RN's and USN's torpedoes, mines and depth charges from 1942-43. re 1.5 under water; heats, Gurney and brisance [EST]. Held 16 and cook-off 487 K as Comp B's [SRC].
- **RDX/TNT:** Late-war HE (the RN's 15" HE Mk VII TNT/RDX). Gurney, brisance, in_plate and full [EST]; too sensitive for AP unless phlegmatised. Held and cook-off Comp B's.
- **Comp B:** RDX 60, TNT 40. Gurney 2,774 and brisance 1.41 (IATG) sourced; heats [EST] as RDX/TNT. Held 16 [SRC], cook-off 487 K [SRC recalled].
