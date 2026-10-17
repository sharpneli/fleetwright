# Material templates

Written by `shipgen templates` from `src/Fleetwright.Shipgen/MaterialTemplates.cs`; don't edit by hand. The
designer's pickers and the game's catalogue are `Content/Templates/materials.json`, from the same table.

A part names its material (`armour.materials`, a deck's or battery's `material`, `hull.plating.material`,
`superstructure.material`); the name picks the design's block in `materials`, else the catalogue's, else
steel's (with a warning). A design copies the blocks its parts name. `q` is the plate's resistance as a share
of the same thickness of the penetration formula's plate (Krupp cemented and STS 1): the game's old armour
qualities, unchanged by P0 of docs/battle/shells-armour-damage.md; P1b puts the research's in
(`research/simulation/armor/00-overview.md` §4.2). The density is armor/03 §7's; the weights read it from P1c
(until then every plate weighs as steel, 7.85 t/m³). The structure group is the plating's and
superstructure's picker; the armour picker offers every material.

| Material | Group | q | Density t/m³ |
|---|---|---|---|
| steel | structure | 0.55 | 7.85 |
| mild steel | structure | 0.50 | 7.85 |
| high-tensile steel | structure | 0.70 | 7.85 |
| wrought iron | structure | 0.45 | 7.70 |
| wood | structure | 0.05 | 0.70 |
| aluminium alloy | structure | 0.25 | 2.70 |
| compound | armour | 0.60 | 7.75 |
| nickel steel | armour | 0.75 | 7.85 |
| Harvey nickel steel | armour | 0.80 | 7.85 |
| Krupp cemented | armour | 1.00 | 7.85 |
| Krupp cemented (KC n/A) | armour | 1.00 | 7.85 |
| cemented armour | armour | 1.00 | 7.85 |
| Vickers cemented | armour | 1.00 | 7.85 |
| Terni cemented | armour | 1.00 | 7.85 |
| Witkowitz cemented | armour | 1.00 | 7.85 |
| Class A | armour | 1.00 | 7.85 |
| Class A armour | armour | 1.00 | 7.85 |
| non-cemented armour | armour | 0.90 | 7.85 |
| Krupp non-cemented | armour | 0.60 | 7.85 |
| Vickers hardened | armour | 0.90 | 7.85 |
| Class B | armour | 1.00 | 7.85 |
| STS | armour | 1.00 | 7.85 |
| special treatment steel | armour | 0.60 | 7.85 |
| special steel | armour | 0.95 | 7.85 |
| Molybdenum steel | armour | 0.60 | 7.85 |
| Wotan | armour | 0.60 | 7.85 |
| Wotan hart | armour | 0.60 | 7.85 |

- **steel:** Shipbuilding steel, the plating's default name.
- **mild steel:** Mild (carbon) steel: hull plate, and the splinter decks of the 1890s on.
- **high-tensile steel:** HT and Ducol-type manganese steels: strength plating and protective decks.
- **wrought iron:** The ironclads' plate and hulls (s.g. 7.6-7.9).
- **wood:** Planking and wooden hulls (oak and teak 0.65-0.75).
- **aluminium alloy:** Light alloy deckhouses.
- **compound:** Steel face welded to a wrought-iron back, about a third and two thirds (1877-1890s).
- **nickel steel:** Homogeneous nickel steel (1889 on).
- **Harvey nickel steel:** Harveyized: carburized nickel steel, face-hardened (1891 on).
- **Krupp cemented:** KC: the face-hardened Ni-Cr plate of 1894 on.
- **Krupp cemented (KC n/A):** KC neuer Art, the German plate of the 1930s.
- **cemented armour:** British cemented, Krupp's process (KC under licence).
- **Vickers cemented:** VC: British face-hardened plate.
- **Terni cemented:** Italian face-hardened plate.
- **Witkowitz cemented:** Austro-Hungarian face-hardened plate.
- **Class A:** US face-hardened plate.
- **Class A armour:** US face-hardened plate (the older designs' name).
- **non-cemented armour:** British homogeneous armour (NC).
- **Krupp non-cemented:** KNC: Krupp's homogeneous plate. q 0.6: not in the old quality table, so its catch-all (P1b sets it).
- **Vickers hardened:** VH: British homogeneous hardened plate.
- **Class B:** US homogeneous plate.
- **STS:** Special treatment steel: the US homogeneous plate of the 1930s on.
- **special treatment steel:** STS by its long name. q 0.6: not in the old quality table, so its catch-all (P1b sets it).
- **special steel:** Homogeneous Ni-Cr deck plate.
- **Molybdenum steel:** Molybdenum-alloyed homogeneous plate (Japan, Italy). q 0.6: not in the old quality table, so its catch-all (P1b sets it).
- **Wotan:** Wotan weich, Krupp's homogeneous deck plate of the 1930s. q 0.6: not in the old quality table, so its catch-all (P1b sets it).
- **Wotan hart:** Wotan hart, the hard homogeneous deck and bulkhead plate. q 0.6: not in the old quality table, so its catch-all (P1b sets it).
