# Material templates

Written by `shipgen templates` from `src/Fleetwright.Shipgen/MaterialTemplates.cs`; don't edit by hand. The
designer's pickers and the game's catalogue are `Content/Templates/materials.json`, from the same table.

A part names its material (`armour.materials`, a deck's or battery's `material`, `hull.plating.material`,
`superstructure.material`); the name picks the design's block in `materials`, else the catalogue's, else
steel's (with a warning). A design copies the blocks its parts name (`shipgen upgrade --materials` refreshes
them from here). `q` is the plate's resistance as a share of the same thickness of the penetration formula's
plate (Thompson's scale, WWII STS 1) against an 8" capped shell: `research/simulation/armor/00-overview.md`
§4.2's reconciled table (P1b of docs/battle/shells-armour-damage.md; the research's id ends each note).
Face-hardened plate is × (D/8")^−k past 8" (held below), with no face-hardened advantage (×0.9) under max(the
thinnest made, 0.3 calibres). Homogeneous plate under 25% elongation loses to shells over 8" by Okun's M79
penalty, 1 − (1 − √(El/25))·min(D − 8", 8")/8". Both rules are the sim's (`MaterialRow.Against`). The density
is armor/03 §7's: every plate is weighed with its own (the hull's structure with its plating's). The
structure group is the plating's and superstructure's picker; the armour picker offers every material.

| Material | Group | q | q at 16" | k | Face (share, from mm) | Elongation % | Density t/m³ |
|---|---|---|---|---|---|---|---|
| steel | structure | 0.70 | 0.70 | - | - | - | 7.85 |
| mild steel | structure | 0.70 | 0.70 | - | - | - | 7.85 |
| high-tensile steel | structure | 0.80 | 0.80 | - | - | - | 7.85 |
| wrought iron | structure | 0.53 | 0.53 | - | - | - | 7.70 |
| wood | structure | 0.01 | 0.01 | - | - | - | 0.70 |
| aluminium alloy | structure | 0.33 | 0.33 | - | - | - | 2.70 |
| compound | armour | 0.72 | 0.72 | 0.00 | 0.30, 0 | - | 7.75 |
| nickel steel | armour | 0.85 | 0.85 | - | - | - | 7.85 |
| Harvey nickel steel | armour | 0.82 | 0.82 | 0.00 | 0.08, 81 | - | 7.85 |
| Krupp cemented | armour | 0.88 | 0.85 | 0.05 | 0.34, 81 | - | 7.85 |
| Krupp cemented (KC n/A) | armour | 1.01 | 0.99 | 0.03 | 0.41, 99 | - | 7.85 |
| cemented armour | armour | 0.85 | 0.85 | 0.00 | 0.30, 102 | - | 7.85 |
| cemented armour (CA) | armour | 0.99 | 1.02 | -0.05 | 0.30, 102 | - | 7.85 |
| Vickers cemented | armour | 0.85 | 0.85 | 0.00 | 0.30, 102 | - | 7.85 |
| Terni cemented | armour | 0.85 | 0.85 | 0.00 | 0.30, 102 | - | 7.85 |
| Terni cemented (1930s) | armour | 1.03 | 1.09 | -0.08 | 0.45, 102 | - | 7.85 |
| Witkowitz cemented | armour | 0.97 | 0.97 | 0.00 | 0.34, 81 | - | 7.85 |
| French cemented (1930s) | armour | 0.95 | 0.92 | 0.05 | 0.35, 102 | - | 7.85 |
| Class A armour | armour | 0.88 | 0.85 | 0.05 | 0.34, 81 | - | 7.85 |
| Class A | armour | 1.03 | 0.92 | 0.17 | 0.55, 127 | - | 7.85 |
| Vickers hardened | armour | 0.89 | 0.89 | 0.00 | 0.35, 279 | - | 7.85 |
| Krupp non-cemented | armour | 0.92 | 0.86 | - | - | 22 | 7.85 |
| special treatment steel | armour | 0.95 | 0.91 | - | - | 23 | 7.85 |
| Class B | armour | 1.00 | 1.00 | - | - | 25 | 7.85 |
| STS | armour | 1.00 | 1.00 | - | - | 25 | 7.85 |
| non-cemented armour | armour | 1.00 | 1.00 | - | - | 25 | 7.85 |
| Wotan hart | armour | 1.00 | 0.85 | - | - | 18 | 7.85 |
| Wotan | armour | 0.95 | 0.89 | - | - | 22 | 7.85 |
| Molybdenum steel | armour | 0.97 | 0.93 | - | - | 23 | 7.85 |
| special steel | armour | 0.97 | 0.93 | - | - | 23 | 7.85 |

- **steel:** Shipbuilding (mild) steel, the plating's default name: Okun's backing ladder 0.7 (MS).
- **mild steel:** Mild steel: hull plate, and the splinter decks of the 1890s on. Okun's ladder 0.7 (MS).
- **high-tensile steel:** HT and Krupp low-nickel structural steel, the laminated decks of 1900-1925: Okun's ladder 0.8 (HT).
- **wrought iron:** The ironclads' plate and hulls (s.g. 7.6-7.9). Spezia 1876 ~0.50, Tresidder/Thompson 0.56, Okun 0.6 (WI).
- **wood:** Planking and wooden hulls (oak and teak 0.65-0.75). Okun counts teak at 1/100 of its thickness.
- **aluminium alloy:** Light alloy deckhouses. [EST] about steel's protection per weight (2.7/7.85); no research row.
- **compound:** Steel face welded to a wrought-iron back, a third and two thirds (1877-1891). Annapolis < 0.75-0.80; shatters chilled shot only (CMP).
- **nickel steel:** Homogeneous nickel steel (1889-1896). Annapolis > 0.77-0.83, Johnson's limit 0.92 (NIS).
- **Harvey nickel steel:** Harveyized: carburized nickel steel, a thin hard face (1891-1899). NO 1937's limit 0.79 (HRV).
- **Krupp cemented:** KC a/A and its licensees (1894-1918): decremental face-hardened Ni-Cr. Capped trials 0.87-0.97, Okun 0.83 of WWII FH (KCA).
- **Krupp cemented (KC n/A):** KC neuer Art, the German plate of 1928-1945. NPG 1946-47 graded it 83-113% (KCNA).
- **cemented armour:** British (and licensees') KC of 1900-1932: "inferior to KC a/A" (Okun) (BKC).
- **cemented armour (CA):** British Cemented Armour of 1933-1945: a thin face on a tough back, slightly better against bigger shells (CA).
- **Vickers cemented:** VC, Vickers' KC (Japan's of the Kongo and Nagato era): as British WWI KC (BKC).
- **Terni cemented:** Italian KC before 1918, as British WWI KC (BKC).
- **Terni cemented (1930s):** Italian Terni cemented of 1929-1943: the best of the five at 15-18" in Okun's FACEHARD tables; little test data (TKC).
- **Witkowitz cemented:** Witkowitz KC (Austria-Hungary, 1905-1918): "~95% of WWII FH" (Okun) (WKC).
- **French cemented (1930s):** French face-hardened plate of 1932-1945: Okun assumed it equal to US; German tests found it inferior [UNCERTAIN] (FRFH).
- **Class A armour:** US face-hardened plate before the thick chill (Pennsylvania, Colorado: KC-type). [EST] as KC a/A; no research row.
- **Class A:** US Class A of 1933-1945, the thick chill: FACEHARD (NL) 1.03 at 8", 0.92 at 16"; OS 751 agrees (USA).
- **Vickers hardened:** Japanese VH of 1937-1945, only over 11": FACEHARD NL 0.89-0.90; Shinano's 26" plate ~0.8 (VH).
- **Krupp non-cemented:** KNC, Krupp-type homogeneous Ni-Cr (1894-1925): Okun 0.95 homogeneous to WWI (KNC).
- **special treatment steel:** US Class B and STS of 1910-1932 (CB1).
- **Class B:** US Class B of the 1930s and WWII: the standard (STS).
- **STS:** US special treatment steel of 1930-1945, the scale's reference: NPG Class B 91-106% (STS).
- **non-cemented armour:** British NCA of 1926-1945 (NCA).
- **Wotan hart:** Wotan hart (1925-1945), 18% elongation: 0.85 against a 16" (WH).
- **Wotan:** Wotan weich (1925-1945), the softer deck plate (WW).
- **Molybdenum steel:** Japanese homogeneous NVNC/CNC (1926-1945): Okun's M79 Q 0.97.
- **special steel:** French homogeneous deck plate of the 1930s. [EST] as Japanese NVNC; no research row.
