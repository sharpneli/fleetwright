# Shell templates

Written by `shipgen templates` from `src/Fleetwright.Shipgen/ShellTemplates.cs`; don't edit by hand. The game's
loadout catalogue and the designer's templates are `Content/Templates/shells.json`, from the same table.

A template is an `ammunition` block (docs/shipgen/README.md): calibre-free, so a navy's family of guns shares it.
Mass is k·D³ (D in m); k is shown here also as W/D³ in lb/in³, the literature's unit (1 lb/in³ = 27,680 kg/m³).
Shares are of the shell's mass, thresholds in calibres. A design copies the blocks it uses; a battery that gives
no shells carries the default loadout of the **1912 standard** family (main 70% AP / 30% HE, secondary 30% / 70%).
Light guns (under 200 mm) of a family take its light blocks where it has them. Notes cite the research
(`research/simulation/armor/04-shells-fuzes.md` §1.7 and §3.5, `armor/00` §5.4, `damage/13` §2.5 and §3.2); [EST]
marks numbers no source gives.

Columns: kind; W/D³; nose crh and drag form factor; cap (share); body break-up (obliquity °, T/D) and quality;
filler (share, explosive, booster); fuze (position, delay s, start calibres at normal and their share from 61°,
plates, delay scatter, blinds); steel (fragment size, speed). What the explosive and booster do (bursting in the
plate, full detonation) is the explosive's (`explosive-templates.md`).

## 1867 chilled iron

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| Palliser shell (1867) | ap | 0.41 | 1.5 / 1.80 | none | 0°, 0.3; 0.85 | 1.5% Black powder, basic | base 0 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 0.6/0.8 |
| Common shell, cast iron (1867) | common | 0.36 | 1.5 / 1.80 | none | 0°, 0.15; 0.85 | 6% Black powder, basic | nose | 0.6/0.8 |

- **Palliser shell (1867):** Palliser chilled cast iron: the 12" RML's 714 lb shot, W/D³ 0.41. ~1.5% powder ignited by the impact, no fuze. Shatters on any steel face.
- **Common shell, cast iron (1867):** Cast iron, ~6% powder, a percussion nose fuze (instant: useless for penetration). W/D³ [EST].

## 1895 forged steel

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| Forged steel APC, soft cap (1895) | ap | 0.47 | 2 / 1.80 | soft 6% | 15°, 0.5; 0.95 | 2.5% Black powder, basic | base 0.005 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| Common shell, forged steel (1895) | common | 0.45 | 2 / 1.80 | none | 10°, 0.33; 0.90 | 7% Black powder, basic | base 0 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |

- **Forged steel APC, soft cap (1895):** Water-hardened Ni steel under a Makarov soft cap (~+15% below 15°, gone above 20°). W/D³ between Russia's 0.42 and the RN's 0.50 [EST]; filler and the inertial tube's ~0.005 s [EST].
- **Common shell, forged steel (1895):** Forged steel common, ~7% powder, base percussion fuze. [EST] from the RN's and Japan's.

## RU 1895

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| RU 1895 AP (pyroxylin) | ap | 0.42 | 2 / 1.80 | none | 15°, 0.5; 0.95 | 1.6% Wet guncotton, basic | base 0.01 s, 0.07 cal (×0.45), single, ±10%, 10% blind | 1/1 |
| RU 1895 HE (pyroxylin) | common | 0.42 | 2 / 1.80 | none | 10°, 0.25; 0.90 | 3.7% Wet guncotton, basic | base 0.005 s, 0.07 cal (×0.45), single, ±10%, 10% blind | 1/1 |

- **RU 1895 AP (pyroxylin):** 12" AP 'old model' 331.7 kg (W/D³ 0.42), uncapped, 5.3 kg wet pyroxylin, Brink delayed tube (delay [EST]). Light shells did little (Tsushima).
- **RU 1895 HE (pyroxylin):** 12" HE 'old model': thin-walled, base-fuzed (the 1894 tube, ~0.005 s), 12.4 kg pyroxylin. A common shell by its fuze.

## JP 1904

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| JP 1904 AP (Shimose) | ap | 0.49 | 2 / 1.80 | none | 15°, 0.5; 0.95 | 5% Shimose, basic | base 0 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| JP 1904 common (powder) | common | 0.49 | 2 / 1.80 | none | 10°, 0.33; 0.90 | 10% Black powder, basic | base 0 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |

- **JP 1904 AP (Shimose):** 12" 'Furoshiki' 386 kg (W/D³ 0.49), 19.3 kg Shimose, the sensitive Ijuin base fuze: bursts on contact, on the water too (0.1 ms: Semenov's "exploded the moment they touched its surface", damage/15 §2.3). No 12" known to pierce over ~6".
- **JP 1904 common (powder):** 12" common, 39 kg powder (10%): the upper-works fires of Tsushima.

## US 1906

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| US 1906 AP (Explosive D) | ap | 0.50 | 2 / 1.50 | soft 6% | 15°, 0.5; 0.95 | 2.5% Explosive D, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| US 1906 common (powder) | common | 0.50 | 2 / 1.50 | none | 10°, 0.33; 0.90 | 6% Black powder, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |

- **US 1906 AP (Explosive D):** 12"/45 870 lb (W/D³ 0.50). Explosive D from 1907, the pre-1920s non-delay TNT-boosted base fuze (~0.003 s). Cap and filler share [EST].
- **US 1906 common (powder):** Common shell for 1/3-1/2 calibre plate (NOG). Filler [EST].

## DE 1902

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| DE 1902 Psgr (TNT) | ap | 0.40 | 2 / 1.80 | soft 6% | 20°, 0.6; 0.95 | 2.7% TNT, basic | base 0.003 s, 0.05 cal (×0.45), two, ±10%, 10% blind | 1.25/1 |
| DE 1902 Sprgr Bdz (TNT) | common | 0.40 | 2 / 1.80 | none | 10°, 0.33; 0.90 | 5% TNT, basic | base 0.003 s, 0.05 cal (×0.45), two, ±10%, 10% blind | 1/1 |
| DE 1902 Sprgr Kz (TNT) | he | 0.40 | 2 / 1.80 | none | 0°, 0.2; 1.00 | 7% TNT, basic | nose | 1/1 |

- **DE 1902 Psgr (TNT):** 28 cm/40 240 kg (W/D³ 0.40). TNT from 1902; non-delay before the 1911 Bdz. Cap, filler and fuze [EST].
- **DE 1902 Sprgr Bdz (TNT):** Base-fuzed shell. Filler [EST].
- **DE 1902 Sprgr Kz (TNT):** Nose-fuzed shell. Filler [EST].

## FR 1893

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| FR 1893 AP (powder) | ap | 0.41 | 2 / 1.80 | none | 15°, 0.5; 0.95 | 2.5% Black powder, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| FR 1893 common (Melinite) | common | 0.41 | 2 / 1.80 | none | 10°, 0.33; 0.90 | 6% Lyddite, basic | base 0 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |

- **FR 1893 AP (powder):** Light shells: the 305 Mle 1887's 292 kg (0.37), the 340's 490 kg (0.45). Forged steel, uncapped; filler and fuze [EST].
- **FR 1893 common (Melinite):** Melinite (picric acid, catalogued as Lyddite) from 1887. Filler [EST].

## RN 1906

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| RN 1906 APC (powder) | ap | 0.49 | 2 / 1.50 | soft 8.6% | 15°, 0.4; 0.85 | 3.1% Black powder, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| RN 1906 CPC (powder) | common | 0.49 | 2 / 1.50 | none | 10°, 0.33; 0.90 | 9.4% Black powder, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| RN 1906 HE (lyddite) | he | 0.49 | 2 / 1.50 | none | 0°, 0.2; 1.00 | 12.5% Lyddite, basic | nose | 1/1 |
| RN 1906 light CP (powder) (light) | common | 0.48 | 2 / 1.50 | none | 10°, 0.33; 0.90 | 7% Black powder, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| RN 1906 light HE (lyddite) (light) | he | 0.48 | 2 / 1.50 | none | 0°, 0.2; 1.00 | 9% Lyddite, basic | nose | 1/1 |

- **RN 1906 APC (powder):** 12" APC Mk VI 2crh 850 lb (W/D³ 0.49), Hadfield soft cap 73 lb (8.6%), powder-filled (lyddite from 1909-10), No. 16 non-delay. Lord Nelsons kept 2crh.
- **RN 1906 CPC (powder):** 12" CPC Mk VIIa: 80 lb powder (9.4%), base fuze.
- **RN 1906 HE (lyddite):** 12" HE Mk IIa: 106.5 lb lyddite (12.5%), No. 13 instantaneous nose fuze.
- **RN 1906 light CP (powder):** The 4"-6" guns' common pointed (4"/50 31 lb, 6" 100 lb: W/D³ 0.47-0.48). Filler [EST]. Also the RN 1909 and 1912 families' light guns.
- **RN 1906 light HE (lyddite):** The 4"-6" guns' lyddite HE. Filler [EST]. Also the RN 1909 and 1912 families' light guns.

## RN 1909

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| RN 1909 APC (lyddite) | ap | 0.50 | 4 / 1.50 | soft 4% | 15°, 0.4; 0.85 | 3.2% Lyddite, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| RN 1909 CPC (powder) | common | 0.49 | 4 / 1.50 | none | 10°, 0.33; 0.90 | 9.4% Black powder, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| RN 1909 HE (lyddite) | he | 0.49 | 4 / 1.50 | none | 0°, 0.2; 1.00 | 12.5% Lyddite, basic | nose | 1/1 |

- **RN 1909 APC (lyddite):** 12" APC Mk VIa 4crh 859 lb (W/D³ 0.50), soft cap 4%, 27.3 lb lyddite, No. 16 non-delay. Breaks up at 15-20° on 0.4 calibre; bursts in the plate (Jutland).
- **RN 1909 CPC (powder):** 12" CPC Mk VIIa 4crh: 80 lb powder (9.4%).
- **RN 1909 HE (lyddite):** 12" HE: lyddite 12.5%, No. 13 nose fuze (withdrawn from ships after Jutland).

## RN 1912

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| RN 1912 APC (lyddite) | ap | 0.57 | 4 / 1.50 | soft 4% | 15°, 0.4; 0.85 | 3.2% Lyddite, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| RN 1912 CPC (powder) | common | 0.57 | 4 / 1.50 | none | 10°, 0.33; 0.90 | 6.7% Black powder, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| RN 1912 HE (lyddite) | he | 0.57 | 4 / 1.50 | none | 0°, 0.2; 1.00 | 10% Lyddite, basic | nose | 1/1 |

- **RN 1912 APC (lyddite):** 15" APC Mk Ia 4crh 1,920 lb (W/D³ 0.57; the 13.5" heavy 1,400 lb the same), 60.5 lb lyddite (3.2%), No. 16 non-delay.
- **RN 1912 CPC (powder):** 15" CPC Mk IIa 1,929 lb, 129.3 lb powder (6.7%).
- **RN 1912 HE (lyddite):** 15" HE, lyddite. Filler [EST].

## DE 1911

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| DE 1911 Psgr L/3,4 (TNT) | ap | 0.52 | 3 / 1.50 | hard2 8% | 20°, 0.6; 0.95 | 2.7% TNT, basic | base 0.025 s, 0.05 cal (×0.45), two, ±50%, 15% blind | 1.25/1 |
| DE 1911 Spgr L/3,8 Bdz (TNT) | common | 0.52 | 3 / 1.50 | none | 10°, 0.33; 0.90 | 6.1% TNT, basic | base 0.025 s, 0.05 cal (×0.45), two, ±50%, 15% blind | 1/1 |
| DE 1911 Spgr Kz (TNT) | he | 0.52 | 3 / 1.50 | none | 0°, 0.2; 1.00 | 6.6% TNT, basic | nose | 1/1 |

- **DE 1911 Psgr L/3,4 (TNT):** 30.5 cm Psgr. L/3,4 405 kg (W/D³ 0.52), Krupp Type 2 cap (share [EST]), 10.8 kg TNT, Bdz ~0.025 s nominal: at most half came near it, many duds.
- **DE 1911 Spgr L/3,8 Bdz (TNT):** 30.5 cm Spgr. L/3,8 Bdz 405 kg, 24.8 kg TNT: base-fuzed, bursts 2-6 m into unarmoured structure.
- **DE 1911 Spgr Kz (TNT):** Nose-fuzed Sprenggranate (the 15 cm's 45.3 kg, 3.0 kg TNT). German light shells ran 0.49-0.54, so the family's blocks serve every gun.

## FR 1910

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| FR 1910 OPf (Melinite) | ap | 0.55 | 3 / 1.50 | soft 6% | 15°, 0.5; 0.95 | 3% Lyddite, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| FR 1910 OEA (Melinite) | common | 0.55 | 3 / 1.50 | none | 10°, 0.33; 0.90 | 6% Lyddite, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| FR 1910 HE (Melinite) | he | 0.55 | 3 / 1.50 | none | 0°, 0.2; 1.00 | 10% Lyddite, basic | nose | 1/1 |

- **FR 1910 OPf (Melinite):** 305 Mle 1906 OPf 432 kg (W/D³ 0.55), melinite. Cap, filler and fuze [EST]: French fuzes weren't found.
- **FR 1910 OEA (Melinite):** Semi-AP explosive shell. [EST].
- **FR 1910 HE (Melinite):** [EST].

## 1912 standard

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| Standard APC (1912) | ap | 0.52 | 3 / 1.50 | soft 6% | 20°, 0.6; 0.95 | 2.5% TNT, basic | base 0.02 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| Standard common (1912) | common | 0.52 | 3 / 1.50 | none | 10°, 0.33; 0.90 | 5% TNT, basic | base 0.02 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| Standard HE (1912) | he | 0.52 | 3 / 1.50 | none | 0°, 0.2; 1.00 | 8% TNT, basic | nose | 1/1 |

- **Standard APC (1912):** A middling Great War APC: W/D³ 0.52, soft cap, TNT 2.5%, a short delay. [EST] throughout, from the RN's and Germany's.
- **Standard common (1912):** [EST].
- **Standard HE (1912):** [EST].

## US 1916

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| US 1916 AP (Explosive D) | ap | 0.51 | 4 / 1.50 | soft 6% | 15°, 0.5; 0.95 | 2.5% Explosive D, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| US 1916 common (Explosive D) | common | 0.51 | 4 / 1.50 | none | 10°, 0.33; 0.90 | 5% Explosive D, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| US 1916 HE (TNT) | he | 0.51 | 4 / 1.50 | none | 0°, 0.2; 1.00 | 7% TNT, basic | nose | 1/1 |
| US 1916 5in common (Explosive D) (light) | common | 0.40 | 3 / 1.50 | none | 10°, 0.33; 0.90 | 3% Explosive D, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| US 1916 5in HE (TNT) (light) | he | 0.40 | 3 / 1.50 | none | 0°, 0.2; 1.00 | 6% TNT, basic | nose | 1/1 |

- **US 1916 AP (Explosive D):** 14"/45 1,400 lb (W/D³ 0.51), contractor caps, non-delay TNT-boosted base fuze. Filler [EST].
- **US 1916 common (Explosive D):** [EST].
- **US 1916 HE (TNT):** [EST].
- **US 1916 5in common (Explosive D):** 5"/51's 50 lb (W/D³ 0.40). Filler [EST]. Also the 1921 family's.
- **US 1916 5in HE (TNT):** [EST]. Also the 1921 family's.

## US 1921

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| US 1921 AP Mk 3 (Explosive D) | ap | 0.52 | 4 / 1.15 | soft 6% | 15°, 0.5; 0.95 | 2.7% Explosive D, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| US 1921 HC (Explosive D) | he | 0.46 | 4 / 1.15 | none | 0°, 0.2; 1.00 | 7% Explosive D, basic | nose | 1/1 |

- **US 1921 AP Mk 3 (Explosive D):** 16"/45 Mk 3 2,110 lb (W/D³ 0.52), 57.5 lb Explosive D (2.7%), non-delay until the late 1920s.
- **US 1921 HC (Explosive D):** [EST] from the later HC.

## RN 1927

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| RN 1927 APC (Shellite) | ap | 0.50 | 4 / 1.15 | hard 11.6% | 20°, 0.67; 0.96 | 2.4% Shellite, basic | base 0.025 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| RN 1927 HE (TNT) | he | 0.50 | 4 / 1.15 | none | 0°, 0.2; 1.00 | 7% TNT, basic | nose | 1/1 |
| RN 1927 light SAP (TNT/wax) (light) | common | 0.47 | 4 / 1.15 | none | 10°, 0.33; 0.90 | 3.3% TNT/wax, basic | base 0.01 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| RN 1927 light HE (TNT) (light) | he | 0.47 | 4 / 1.15 | none | 0°, 0.2; 1.00 | 6% TNT, basic | nose | 1/1 |

- **RN 1927 APC (Shellite):** Greenboy practice (hard cap 11.6%, Shellite 2.4%, No. 16D 0.025 s) at the light W/D³ of Nelson's 16" (2,048 lb, 0.50) and the 8" (256 lb, 0.50). CE booster only from the late 1920s.
- **RN 1927 HE (TNT):** [EST].
- **RN 1927 light SAP (TNT/wax):** 6" CPBC 100 lb (W/D³ 0.47), TNT/beeswax. Filler [EST].
- **RN 1927 light HE (TNT):** [EST].

## JP 1913

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| JP 1913 APC (Shimose) | ap | 0.51 | 4 / 1.50 | soft 4% | 15°, 0.5; 0.95 | 3% Shimose, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| JP 1913 common (Shimose) | common | 0.51 | 4 / 1.50 | none | 10°, 0.33; 0.90 | 6% Shimose, basic | base 0.003 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| JP 1913 HE (Shimose) | he | 0.51 | 4 / 1.50 | none | 0°, 0.2; 1.00 | 10% Shimose, basic | nose | 1/1 |

- **JP 1913 APC (Shimose):** Vickers 14"/45 1,400 lb (W/D³ 0.51). Shimose to the 1920s. Filler and fuze [EST].
- **JP 1913 common (Shimose):** [EST].
- **JP 1913 HE (Shimose):** [EST].

## JP 1925

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| JP 1925 APC (TNT/wax) | ap | 0.54 | 4 / 1.15 | hard 9% | 20°, 0.6; 0.95 | 2.5% TNT/wax, basic | base 0.03 s, 0.05 cal (×0.45), two, ±10%, 10% blind | 1.25/1 |
| JP 1925 HE (TNT) | he | 0.50 | 4 / 1.15 | none | 0°, 0.2; 1.00 | 6% TNT, basic | nose | 1/1 |
| JP 1925 light common (TNT/wax) (light) | common | 0.43 | 4 / 1.15 | none | 10°, 0.33; 0.90 | 3% TNT/wax, basic | base 0.01 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| JP 1925 light HE (TNT) (light) | he | 0.43 | 4 / 1.15 | none | 0°, 0.2; 1.00 | 8% TNT, basic | nose | 1/1 |

- **JP 1925 APC (TNT/wax):** APC Model 1925 on the German pattern (wax-TNT, buffered cavity, 1922): 41 cm 1,020 kg (W/D³ 0.54). Still prematures and low-order bursts. Cap and filler [EST]; delay 0.025-0.035 (Okun).
- **JP 1925 HE (TNT):** [EST].
- **JP 1925 light common (TNT/wax):** 12.7 cm 23 kg (W/D³ 0.41) and 14 cm 38 kg (0.46). Filler [EST].
- **JP 1925 light HE (TNT):** [EST].

## RU 1911

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| RU 1911 AP (TNT) | ap | 0.60 | 4 / 1.15 | hard 8% | 20°, 0.6; 0.95 | 2.7% TNT, basic | base 0.025 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| RU 1911 HE (TNT) | he | 0.60 | 4 / 1.15 | none | 0°, 0.2; 1.00 | 12.5% TNT, basic | nose | 1/1 |
| RU 1911 light common (TNT) (light) | common | 0.53 | 4 / 1.15 | none | 10°, 0.33; 0.90 | 4% TNT, basic | base 0.01 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| RU 1911 light HE (TNT) (light) | he | 0.53 | 4 / 1.15 | none | 0°, 0.2; 1.00 | 8% TNT, basic | nose | 1/1 |

- **RU 1911 AP (TNT):** 12" AP mod 1911 470.9 kg (W/D³ 0.60, super-heavy), 12.84 kg. Cap, explosive and fuze [EST].
- **RU 1911 HE (TNT):** 12" HE mod 1911 471 kg, 58.8 kg TNT.
- **RU 1911 light common (TNT):** 120/50 28.97 kg (W/D³ 0.53). Filler [EST].
- **RU 1911 light HE (TNT):** [EST].

## RN 1938

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| RN 1938 APC (Shellite) | ap | 0.58 | 6 / 1.05 | hard 10% | 30°, 1; 1.00 | 2.5% Shellite, ce | base 0.025 s, 0.063 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| RN 1938 HE (TNT) | he | 0.58 | 6 / 1.05 | none | 0°, 0.2; 1.00 | 6.7% TNT, ce | nose | 1/1 |
| RN 1938 light SAP (TNT/wax) (light) | common | 0.53 | 6 / 1.05 | none | 10°, 0.33; 0.90 | 3% TNT/wax, ce | base 0.01 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| RN 1938 light HE (TNT) (light) | he | 0.53 | 6 / 1.05 | none | 0°, 0.2; 1.00 | 7% TNT, ce | nose | 1/1 |

- **RN 1938 APC (Shellite):** 14" APC Mk VIIb 6crh 1,590 lb (W/D³ 0.58), Shellite 2.5% with CE, No. 159 (1" at normal on a 16": 0.0625 cal), 0.025 s. Cap share [EST].
- **RN 1938 HE (TNT):** 15" HE Mk VII 879 kg, 59 kg TNT (6.7%).
- **RN 1938 light SAP (TNT/wax):** 6" CPBC 112 lb (W/D³ 0.52), 5.25" 80 lb (0.56). Filler [EST].
- **RN 1938 light HE (TNT):** [EST].

## DE 1939

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| DE 1939 Psgr L/4,4 m.Hb (TNT/wax) | ap | 0.53 | 4.4 / 1.05 | hard 10% | 20°, 0.8; 1.00 | 2.5% TNT/wax, basic | base 0.035 s, 0.05 cal (×0.45), two, ±10%, 5% blind | 1.25/1 |
| DE 1939 Spgr L/4,5 Kz (TNT) | he | 0.53 | 4.4 / 1.05 | none | 0°, 0.2; 1.00 | 8.3% TNT, basic | nose | 1/1 |
| DE 1939 15cm Spgr Bdz (TNT) (light) | common | 0.48 | 4.4 / 1.05 | none | 10°, 0.33; 0.90 | 5% TNT, basic | base 0.015 s, 0.05 cal (×0.45), two, ±10%, 5% blind | 1/1 |
| DE 1939 15cm Spgr Kz (TNT) (light) | he | 0.48 | 4.4 / 1.05 | none | 0°, 0.2; 1.00 | 8.6% TNT, basic | nose | 1/1 |

- **DE 1939 Psgr L/4,4 m.Hb (TNT/wax):** 38 cm Psgr. L/4,4 m.Hb 800 kg (W/D³ 0.53), hard cap and 10-calibre ballistic cap, 20.3 kg Fp, Bd.Z. 38 0.035 s; a picric booster to 1945. Cap share [EST].
- **DE 1939 Spgr L/4,5 Kz (TNT):** 38 cm nose-fuzed HE 800 kg, 66.2 kg TNT.
- **DE 1939 15cm Spgr Bdz (TNT):** 15 cm 45.3 kg (W/D³ 0.485), 10.5 cm 15.1 kg (0.47); Bd.Z. KV 0.015 s. Filler [EST].
- **DE 1939 15cm Spgr Kz (TNT):** 15 cm Sprgr. L/4,6 KZ, 3.9 kg TNT.

## IT 1940

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| IT 1940 APC (TNT) | ap | 0.58 | 4 / 1.05 | hard 10% | 25°, 0.9; 0.95 | 1.15% TNT, basic | base 0.03 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| IT 1940 HE (TNT) | he | 0.54 | 4 / 1.05 | none | 0°, 0.2; 1.00 | 7% TNT, basic | nose | 1/1 |
| IT 1940 light SAP (TNT) (light) | common | 0.50 | 4 / 1.05 | none | 10°, 0.33; 0.90 | 3% TNT, basic | base 0.01 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| IT 1940 light HE (TNT) (light) | he | 0.50 | 4 / 1.05 | none | 0°, 0.2; 1.00 | 7% TNT, basic | nose | 1/1 |

- **IT 1940 APC (TNT):** 381 mm APC 884.8 kg (W/D³ 0.58), cast TNT 10.16 kg (1.15%). Uneven makers: quality 0.95. Cap and fuze [EST].
- **IT 1940 HE (TNT):** [EST].
- **IT 1940 light SAP (TNT):** 152/55 50 kg (W/D³ 0.51), 90/50 and 100/47 0.50. Filler [EST].
- **IT 1940 light HE (TNT):** [EST].

## FR 1936

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| FR 1936 OPfK (TNT) | ap | 0.58 | 4 / 1.05 | hard 9.5% | 30°, 1; 1.00 | 2.5% TNT, basic | base 0.03 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1.25/1 |
| FR 1936 OEA (Melinite) | he | 0.55 | 4 / 1.05 | none | 0°, 0.2; 1.00 | 8% Lyddite, basic | nose | 1/1 |
| FR 1936 light SAP (TNT) (light) | common | 0.53 | 4 / 1.05 | none | 10°, 0.33; 0.90 | 3% TNT, basic | base 0.01 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| FR 1936 light HE (TNT) (light) | he | 0.53 | 4 / 1.05 | none | 0°, 0.2; 1.00 | 7% TNT, basic | nose | 1/1 |

- **FR 1936 OPfK (TNT):** 380 mm OPfK Mle 1936 884 kg (W/D³ 0.58), cap 84 kg (9.5%), 21.9 kg filler (explosive [EST]). Fuze [EST].
- **FR 1936 OEA (Melinite):** [EST].
- **FR 1936 light SAP (TNT):** 152/55 Mle 1930 54 kg (W/D³ 0.555), 130/45 32 kg (0.53), 100/45 13.5 kg (0.49). Filler [EST].
- **FR 1936 light HE (TNT):** [EST].

## US 1941

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| US 1941 AP Mk 8 (Explosive D) | ap | 0.66 | 9 / 1.05 | hard 12% | 45°, 1.2; 1.10 | 1.5% Explosive D, ce | base 0.033 s, 0.094 cal (×0.25), single, ±10%, 5% blind | 1.25/1 |
| US 1941 HC (Explosive D) | he | 0.48 | 9 / 1.05 | none | 0°, 0.2; 1.00 | 8.1% Explosive D, ce | nose | 1/1 |
| US 1941 5in Common (Explosive D) (light) | common | 0.44 | 4 / 1.05 | none | 10°, 0.33; 0.90 | 4.9% Explosive D, ce | base 0.01 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| US 1941 5in AAC (Explosive D) (light) | he | 0.44 | 4 / 1.05 | none | 0°, 0.2; 1.00 | 13% Explosive D, ce | nose | 1/1 |

- **US 1941 AP Mk 8 (Explosive D):** 16"/50 Mk 8 2,700 lb (W/D³ 0.66, super-heavy; the 8" and 6" SH the same), 9crh under a windscreen, 40.9 lb Explosive D (1.5%), Mk 21 BDF 0.033 s (1.5" at 0°, 0.375" at 65°). Cap share [EST].
- **US 1941 HC (Explosive D):** 16" HC Mk 13 1,900 lb (W/D³ 0.46), 8" 0.51, 6" 0.49: 0.48 between. 8.1% Explosive D, PDF Mk 29.
- **US 1941 5in Common (Explosive D):** 5"/38 Common 24.5 kg (W/D³ 0.44), 1.2 kg Explosive D, 0.01 s.
- **US 1941 5in AAC (Explosive D):** 5"/38 AAC 25 kg, 3.3 kg Explosive D.

## JP 1931

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| JP 1931 Type 91 AP (TNA) | ap | 0.54 | 4 / 1.05 | break_away 10% | 20°, 0.67; 0.96 | 1.6% TNA, ce | base 0.4 s, 0.05 cal (×0.45), two, ±10%, 5% blind | 1.25/1 |
| JP 1931 Type 0 HE (TNA) | he | 0.51 | 4 / 1.05 | none | 0°, 0.2; 1.00 | 4.5% TNA, ce | nose | 1/1 |
| JP 1931 light common (TNA) (light) | common | 0.44 | 4 / 1.05 | none | 10°, 0.33; 0.90 | 3% TNA, ce | base 0.01 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| JP 1931 light HE (TNA) (light) | he | 0.44 | 4 / 1.05 | none | 0°, 0.2; 1.00 | 8% TNA, ce | nose | 1/1 |

- **JP 1931 Type 91 AP (TNA):** 46 cm Type 91 1,460 kg (W/D³ 0.54), the break-away cap head (diving: off above 45°), 23.9 kg TNA with its liner, 0.4 s quoted [UNCERTAIN]. Cap share and booster [EST].
- **JP 1931 Type 0 HE (TNA):** 46 cm Type 0 HE 1,360 kg, 61.7 kg TNA.
- **JP 1931 light common (TNA):** 15.5 cm and 12.7 cm (W/D³ 0.41-0.48). Filler [EST].
- **JP 1931 light HE (TNA):** 12.7 cm Type 0 HE 23 kg, 1.88 kg TNA.

## 1950 dual-purpose

| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |
|---|---|---|---|---|---|---|---|---|
| DP SAP (1950) | common | 0.56 | 6 / 1.00 | none | 10°, 0.33; 0.90 | 4% TNT, ce | base 0.01 s, 0.07 cal (×0.45), single, ±10%, 5% blind | 1/1 |
| DP HE (1950) | he | 0.56 | 6 / 1.00 | none | 0°, 0.2; 1.00 | 10% Comp B, ce | nose | 1/1 |

- **DP SAP (1950):** The post-war 4.5"-6" dual-purpose guns' shells (5"/54 31.75 kg, W/D³ 0.56). [EST].
- **DP HE (1950):** Comp B filled, nose fuzed. [EST].
