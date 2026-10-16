# Explosive templates

Written by `shipgen templates` from `src/Fleetwright.Shipgen/Explosives.cs`; don't edit by hand. The game's catalogue
is `Content/Templates/explosives.json`, from the same table.

A shell's `filler.explosive` names one; a design copies the ones it names into its `explosives`, and a filler can
override any number inline. Sources: `docs/battle/damage/12_explosive_fillers.md` §4.1 (re, in-plate, full
detonation, fire), `11_blast_confinement.md` §2.4 (heats, air, eta), `13_fragmentation.md` §2.1 and §3.1 (Gurney,
brisance). The sim reads `re` (the blast: W = filler kg × re) from S1 on; the rest ride along for P3 and phase E.

| Explosive | From | re | Q det / comb MJ/kg | Air m³/kg | eta | Gurney m/s | Brisance | In plate | Full basic / CE | Fire |
|---|---|---|---|---|---|---|---|---|---|---|
| Black powder | 1860 | 0.55 | 2.9 / 2.9 | 0.00 | 0.55 | 640 | 0.15 | 0.15 | 1.00 / 1.00 | 1.00 |
| Wet guncotton | 1890 | 0.85 | 3.0 / 7.0 | 1.00 | 1.00 | 2100 | 0.90 | 0.08 | 0.55 / 0.85 | 0.20 |
| Lyddite | 1888 | 1.17 | 4.4 / 11.4 | 1.63 | 1.00 | 2550 | 1.15 | 0.35 | 0.95 / 0.97 | 0.40 |
| Shimose | 1893 | 1.17 | 4.4 / 11.4 | 1.63 | 1.00 | 2600 | 1.20 | 0.45 | 0.95 / 0.97 | 0.50 |
| TNT | 1902 | 1.00 | 4.5 / 15.0 | 2.66 | 1.00 | 2438 | 1.00 | 0.10 | 0.60 / 0.95 | 0.25 |
| TNT/wax | 1905 | 0.90 | 4.1 / 17.7 | 3.60 | 1.00 | 2300 | 0.90 | 0.04 | 0.55 / 0.93 | 0.25 |
| Explosive D | 1911 | 0.95 | 3.2 / 11.5 | 1.87 | 1.00 | 2350 | 0.90 | 0.02 | 0.50 / 0.92 | 0.20 |
| Shellite | 1918 | 1.00 | 4.2 / 12.3 | 2.00 | 1.00 | 2450 | 1.05 | 0.00 | 0.60 / 0.90 | 0.35 |
| TNA | 1931 | 1.00 | 4.4 / 13.6 | 2.25 | 1.00 | 2440 | 1.00 | 0.05 | 0.60 / 0.93 | 0.25 |
| Amatol 60/40 | 1915 | 1.00 | 4.2 / 9.0 | 1.00 | 1.00 | 2150 | 0.85 | 0.10 | 0.60 / 0.93 | 0.15 |
| Amatol 80/20 | 1915 | 0.95 | 4.0 / 4.0 | 0.00 | 1.00 | 1900 | 0.65 | 0.12 | 0.55 / 0.90 | 0.10 |
| RDX/TNT | 1943 | 1.35 | 5.0 / 12.0 | 1.70 | 1.00 | 2700 | 1.35 | 0.30 | 0.65 / 0.97 | 0.25 |
| Comp B | 1943 | 1.33 | 5.0 / 12.0 | 1.70 | 1.00 | 2774 | 1.41 | 0.30 | 0.65 / 0.97 | 0.25 |

- **Black powder:** Burns, never detonates: always 'full', its re prices it. Gurney 640 m/s (MSIAC L-295, 90th percentile). Few big slow pieces; the most fires.
- **Wet guncotton:** Nitrocellulose at 25-30% water (Russia 1890s-1905). Heats and air [EST]: dry NC's less the water. Under-initiated by weak primers: many duds.
- **Lyddite:** Picric acid (also Melinite). in_plate 0.35 sourced (armor/04 §6, Jutland). Fine splinters.
- **Shimose:** Picric acid undiluted, lacquer and wax lined (Japan). Bore prematures and bursts on the water are 12's hidden stats, not modelled.
- **TNT:** Trotyl, Fp 02. Heats measured (LLNL 4.57, 14.96 MJ/kg), Gurney IATG 01.80. Needs a strong booster.
- **TNT/wax:** TNT with ~10% wax (Germany's Fp 5-20, the RN's post-war TNT/beeswax). Heats and air [EST]: TNT's with the wax's combustion.
- **Explosive D:** Ammonium picrate (Dunnite, USN). re [EST, unsourced]; 11 has 0.90. The most impact-insensitive filler in quantity.
- **Shellite:** Picric acid with dinitrophenol (RN Greenboy on). Full 0.90 with CE sourced; low-order without it until the late 1920s.
- **TNA:** Trinitroanisole, Type 91 bakuyaku (IJN). re [EST, unsourced]. Needed a liner of 1/3-1/2 of the cavity.
- **Amatol 60/40:** TNT 60, ammonium nitrate 40: the shortage filler. Heats and air [EST] between TNT and 80/20.
- **Amatol 80/20:** Ammonium nitrate 80, TNT 20: about five times the shells per ton of TNT. Oxygen-balanced: no afterburn. Brisance 0.62 from NOL's fragments.
- **RDX/TNT:** Late-war HE (the RN's 15" HE Mk VII TNT/RDX). Gurney, brisance, in_plate and full [EST]; too sensitive for AP unless phlegmatised.
- **Comp B:** RDX 60, TNT 40. Gurney 2,774 and brisance 1.41 (IATG) sourced; heats [EST] as RDX/TNT.
