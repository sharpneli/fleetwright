# Drainage templates

Written by `shipgen templates` from `src/Fleetwright.Shipgen/DrainageTemplates.cs`; don't edit by hand. The
designer's templates are `Content/Templates/drainage.json`, from the same table.

A design's `drainage` block says what's built in against water (`docs/battle/damage-control.md` DC4;
`docs/battle/09_damage_control.md` §5): the fixed drainage pumps (t/h for each 1,000 t of displacement, on the
ship's power), the portable pumps her repair parties rig (each one's t/h, no power needed), how fast her
counterflooding valves fill the tanks on the high side (t/min for each 10,000 t; 0: none), and the tightness of
her intact boundaries (× the base leak: 1 air-tested, more where glands and penetrations were never tested). A
key not given takes its navy's template in its plant's year (old designs got the whole block so when the format
moved to 11). The numbers are estimates by period [EST]; no weight is booked for them yet.

```json
"drainage": {"name": "RN Great War (1914)", "pumps_tph_per_1000t": 50, "portable_pumps": 4, "portable_tph": 15,
  "counterflood_tpm_per_10000t": 10, "tightness": 1.5}
```

| Template | Pumps, t/h per 1,000 t | Portable pumps | Each, t/h | Counterflooding, t/min per 10,000 t | Tightness |
|---|---|---|---|---|---|
| Victorian (1890) | 30 | 4 | 5 | 0 | 3 |
| Pre-dreadnought (1905) | 40 | 4 | 8 | 8 | 2 |
| RN Great War (1914) | 50 | 4 | 15 | 10 | 1.5 |
| German Great War (1914) | 50 | 4 | 15 | 20 | 1 |
| Interwar (1939) | 60 | 6 | 30 | 18 | 1 |
| USN 1943 | 70 | 8 | 60 | 20 | 1 |

- **Victorian (1890):** Steam drainage pumps and hand pumps; no counterflooding system (the sea valves are for the magazines); bulkheads never air-tested, many pipes and cables through them [EST].
- **Pre-dreadnought (1905):** Main drainage with steam ejectors; Makarov's and Krylov's counterflooding tables, a few tanks flooded by hand valves (Orel, Retvizan) [EST].
- **RN Great War (1914):** Main drainage on the dynamos; counterflooding by hand at the valves, little organised for it (no unified damage control until 1943) [EST].
- **German Great War (1914):** Close subdivision, good closure discipline and systematic counterflooding (Seydlitz, Lützow trimmed by it) [EST].
- **Interwar (1939):** Electric drainage, submersibles; remote counterflood valves (North Carolina: ~80 t/min on 45,000 t) [EST from WDR 61].
- **USN 1943:** Wartime lessons: handy-billies (14 t/h) and P-500s (115 t/h) standard, emergency diesels, air-tested compartments (09 §5.2) [EST].
