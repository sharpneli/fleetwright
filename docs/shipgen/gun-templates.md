# Gun and torpedo templates

Written by `shipgen templates` from `src/Fleetwright.Shipgen/GunTemplates.cs`; don't edit by hand. The designer's
templates are `Content/Templates/guns.json` and `torpedoes.json`, from the same tables.

A battery's `gun` names its gun block, and `torpedoes.torpedo` its torpedo block: the design's own (`gun_tech`,
`torpedo_tech`), else the catalogue's below; a battery that names none fires with the Second War's double-base
block, and torpedoes that name none are the Second War's. The blocks are calibre-free, shares of the sim's
placeholder rules (`DesignerPlaceholders.Gun`, `Charge`, `Pattern`, `Torpedo`: the sources are there), so one
block serves a period's family of guns. The look never sets them: it's the drawing's only. Designs from before
format 9 got the blocks their look's era and navy stood for, so they fight as before.

```json
"secondary": [{"calibre_mm": 152, "calibre_length": 50, "per_side": 6, "mount": "casemate", "gun": "Second War, double-base"}],
"torpedoes": {"tubes": 4, "torpedo": "Great War torpedo"}
```

| Gun block | Reload | Slower at 300 mm (≥40 / <40 / <30 cal) | Training | DP | Elevation | DP | Pattern % (305 mm, power, light) | Propellant | Packed |
|---|---|---|---|---|---|---|---|---|---|
| Victorian, double-base | ×1.0 | ×1.3 / ×2.5 / ×4.0 | ×0.7 | ×1.0 | 13° | 13° | 3.5, 0, 3.5 | double-base, black powder under 36 cal | clarkson |
| Victorian, double-base, brass-cased | ×1.0 | ×1.3 / ×2.5 / ×4.0 | ×0.7 | ×1.0 | 13° | 13° | 3.5, 0, 3.5 | double-base, black powder under 36 cal | brass |
| Victorian, single-base | ×1.0 | ×1.3 / ×2.5 / ×4.0 | ×0.7 | ×1.0 | 13° | 13° | 3.5, 0, 3.5 | single-base, black powder under 36 cal | tank |
| Great War, double-base | ×1.1 | ×1.0 / ×1.0 / ×1.0 | ×1.0 | ×1.0 | 18° | 18° | 3.3, 3, 2.5 | double-base | clarkson |
| Great War, double-base, brass-cased | ×1.1 | ×1.0 / ×1.0 / ×1.0 | ×1.0 | ×1.0 | 18° | 18° | 3.3, 3, 2.5 | double-base | brass |
| Great War, single-base | ×1.1 | ×1.0 / ×1.0 / ×1.0 | ×1.0 | ×1.0 | 18° | 18° | 3.3, 3, 2.5 | single-base | tank |
| Treaty, double-base | ×1.0 | ×1.0 / ×1.0 / ×1.0 | ×1.2 | ×1.6 | 30° | 45° | 1.9, 0, 1.8 | double-base | clarkson |
| Treaty, double-base, brass-cased | ×1.0 | ×1.0 / ×1.0 / ×1.0 | ×1.2 | ×1.6 | 30° | 45° | 1.9, 0, 1.8 | double-base | brass |
| Treaty, single-base | ×1.0 | ×1.0 / ×1.0 / ×1.0 | ×1.2 | ×1.6 | 30° | 45° | 1.9, 0, 1.8 | single-base | tank |
| Treaty, diglycol | ×1.0 | ×1.0 / ×1.0 / ×1.0 | ×1.2 | ×1.6 | 30° | 45° | 1.9, 0, 1.8 | diglycol | brass |
| Second War, double-base | ×0.9 | ×1.0 / ×1.0 / ×1.0 | ×1.5 | ×1.6 | 40° | 45° | 1.9, 0, 1.8 | double-base | clarkson |
| Second War, double-base, brass-cased | ×0.9 | ×1.0 / ×1.0 / ×1.0 | ×1.5 | ×1.6 | 40° | 45° | 1.9, 0, 1.8 | double-base | brass |
| Second War, single-base | ×0.9 | ×1.0 / ×1.0 / ×1.0 | ×1.5 | ×1.6 | 40° | 45° | 1.9, 0, 1.8 | single-base | tank |
| Second War, diglycol | ×0.9 | ×1.0 / ×1.0 / ×1.0 | ×1.5 | ×1.6 | 40° | 45° | 1.9, 0, 1.8 | diglycol | brass |
| Cold War, triple-base | ×0.5 | ×1.0 / ×1.0 / ×1.0 | ×3.0 | ×1.6 | 45° | 45° | 1.5, 0, 1.5 | triple-base | brass |

Shares of the rules' reload, 7·(d/102 mm)^1.5 s (at most 36), and training, 2·(381 mm/d)^1.5 °/s.

- **Victorian, double-base:** Before 1906: heavy guns slow to load (12"/35 70-100 s), hand-worked mounts, 13° elevation, the brown-powder short guns. Double-base: cordite MD and SC, the German RPC/12, Japan's and Italy's.
- **Victorian, double-base, brass-cased:** Before 1906: heavy guns slow to load (12"/35 70-100 s), hand-worked mounts, 13° elevation, the brown-powder short guns. The main charge in a brass cartridge case, the fore charge in silk in a tin (Germany: RP C/12; Seydlitz's 62 charges burned at Dogger Bank and her magazines held).
- **Victorian, single-base:** Before 1906: heavy guns slow to load (12"/35 70-100 s), hand-worked mounts, 13° elevation, the brown-powder short guns. Single-base nitrocellulose: the USN's SPD, the French poudre B, the Russian pyroxylin.
- **Great War, double-base:** 1906-1921: RN 13.5" and 15" 20°, German 13.5-16°; the 12" 3.3-3.5%, the 15" 1.7 ("the most accurate battleship gun"). Double-base: cordite MD and SC, the German RPC/12, Japan's and Italy's.
- **Great War, double-base, brass-cased:** 1906-1921: RN 13.5" and 15" 20°, German 13.5-16°; the 12" 3.3-3.5%, the 15" 1.7 ("the most accurate battleship gun"). The main charge in a brass cartridge case, the fore charge in silk in a tin (Germany: RP C/12; Seydlitz's 62 charges burned at Dogger Bank and her magazines held).
- **Great War, single-base:** 1906-1921: RN 13.5" and 15" 20°, German 13.5-16°; the 12" 3.3-3.5%, the 15" 1.7 ("the most accurate battleship gun"). Single-base nitrocellulose: the USN's SPD, the French poudre B, the Russian pyroxylin.
- **Treaty, double-base:** 1922-1936: delay coils and stiffer mounts (16" 1.9%, 8" 2.0); dual-purpose mounts at 45°. Double-base: cordite MD and SC, the German RPC/12, Japan's and Italy's.
- **Treaty, double-base, brass-cased:** 1922-1936: delay coils and stiffer mounts (16" 1.9%, 8" 2.0); dual-purpose mounts at 45°. The main charge in a brass cartridge case, the fore charge in silk in a tin (Germany: RP C/12; Seydlitz's 62 charges burned at Dogger Bank and her magazines held).
- **Treaty, single-base:** 1922-1936: delay coils and stiffer mounts (16" 1.9%, 8" 2.0); dual-purpose mounts at 45°. Single-base nitrocellulose: the USN's SPD, the French poudre B, the Russian pyroxylin.
- **Treaty, diglycol:** 1922-1936: delay coils and stiffer mounts (16" 1.9%, 8" 2.0); dual-purpose mounts at 45°. Diglycol, cool double-base: the German RPC/38.
- **Second War, double-base:** 1937-1949: USN 16" training 4 °/s, 38 cm 5; the 5"/38 25 and 1.7%. Double-base: cordite MD and SC, the German RPC/12, Japan's and Italy's.
- **Second War, double-base, brass-cased:** 1937-1949: USN 16" training 4 °/s, 38 cm 5; the 5"/38 25 and 1.7%. The main charge in a brass cartridge case, the fore charge in silk in a tin (Germany: RP C/12; Seydlitz's 62 charges burned at Dogger Bank and her magazines held).
- **Second War, single-base:** 1937-1949: USN 16" training 4 °/s, 38 cm 5; the 5"/38 25 and 1.7%. Single-base nitrocellulose: the USN's SPD, the French poudre B, the Russian pyroxylin.
- **Second War, diglycol:** 1937-1949: USN 16" training 4 °/s, 38 cm 5; the 5"/38 25 and 1.7%. Diglycol, cool double-base: the German RPC/38.
- **Cold War, triple-base:** From 1950: automatic guns, power-worked mounts. Triple-base: Cordite N/NQ, SPCG, M30/M31.

| Torpedo block | Fast (21") | Slow (21") | Warhead TNT share | Mount training | Reload (mount / tube) | Gyro | Fail | Dud |
|---|---|---|---|---|---|---|---|---|
| Victorian torpedo | 30 kn, 1,000 m | 24 kn, 2,700 m | 0.45 | 3 °/s | 600 / 180 s | 5° | 0.25 | 0.15 |
| Great War torpedo | 41 kn, 3,900 m | 28 kn, 9,200 m | 0.6 | 3 °/s | 600 / 180 s | 20° | 0.12 | 0.1 |
| Treaty torpedo | 43 kn, 5,000 m | 30 kn, 11,000 m | 0.85 | 3 °/s | 600 / 180 s | 90° | 0.1 | 0.08 |
| Second War torpedo | 45 kn, 6,000 m | 32 kn, 12,500 m | 1.0 | 6 °/s | 300 / 180 s | 90° | 0.08 | 0.1 |
| Cold War torpedo | 45 kn, 9,000 m | 35 kn, 18,000 m | 1.0 | 6 °/s | 300 / 180 s | 90° | 0.05 | 0.05 |

- **Victorian torpedo:** Before 1906: the 1890s' Whiteheads, wet guncotton; no gyro angling to speak of.
- **Great War torpedo:** 1906-1921: the RN's 21" Mk II 45 kn for 3,840 m or 29 kn for 9,100; the German G/7 37 kn for 4,000 m or 27 kn for 9,300 and its 195 kg of hexanite.
- **Treaty torpedo:** 1922-1936.
- **Second War torpedo:** 1937-1949: Mk 15 45 kn for 5,500 m, G7a 44 for 5,000; power-trained mounts, the IJN's reload gear. Oxygen torpedoes (the Type 93's 20 km at 48 kn) aren't told apart.
- **Cold War torpedo:** From 1950.
