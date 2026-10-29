# Searchlight templates

Written by `shipgen templates` from `src/Fleetwright.Shipgen/SearchlightTemplates.cs`; don't edit by hand. The
designer's templates are `Content/Templates/searchlights.json`, from the same table.

A design's `searchlights` block says what lights she carries (`docs/battle/night.md` Q3), and shipgen sizes and
sites them itself: a capital ship's count (180 × 27 m of waterplane; a smaller ship in proportion to her waterplane,
a bigger one by its 0.25 power, at least one) unless `count` gives hers, and her mirror between a torpedo craft's (600
m² and under) and a capital ship's by the logarithm of her waterplane. They stand on the bridge's wings, on
platforms on the funnels' sides, on after control, on the masts' tops and the other roofs, the groups taking turns
so they spread fore and aft, pairs abreast; the deck only where nothing else has room. The arc's crater (cd/cm²)
and the mirror give the beam's peak (crater × mirror's face × 0.9 clear × 0.85 silvered glass), the spread its
width; control is local (each light by its crew), remote (from a searchlight control position) or director.
A light's weight (1.2 t a 36-inch, by its mirror's area), draw (6.5 kW a 36-inch plain arc) and crew (three at a
36-inch) are booked from its mirror. A key not given takes its template in the plant's year (old designs got the
whole block by their navy so when the format moved to 14). The numbers are estimates by period [EST] until the
research request "Night fighting, 1890-1925" comes back.

```json
"searchlights": {"name": "Great War (1914)", "capital_count": 8, "mirror_cm": 91, "small_mirror_cm": 61,
  "lamp_cd_cm2": 16000, "spread_deg": 2, "control": "local"}
```

| Template | Capital ship | Mirror, cm | Small, cm | Crater, cd/cm² | Beam (capital), cd | Spread | Control |
|---|---|---|---|---|---|---|---|
| Victorian (1890) | 5 | 61 | 40 | 12,000 | 2.7e7 | 3.0° | local |
| Pre-dreadnought (1904) | 6 | 75 | 45 | 14,000 | 4.7e7 | 2.5° | local |
| RN pre-dreadnought (1904) | 6 | 61 | 51 | 14,000 | 3.1e7 | 2.5° | local |
| Great War (1914) | 8 | 91 | 61 | 16,000 | 8.0e7 | 2.0° | local |
| RN post-Jutland (1918) | 8 | 91 | 61 | 16,000 | 8.0e7 | 2.0° | remote |
| German Great War (1914) | 8 | 110 | 60 | 16,000 | 1.2e8 | 1.8° | remote |
| USN high-intensity (1918) | 8 | 91 | 61 | 60,000 | 3.0e8 | 1.4° | local |
| Interwar (1930) | 6 | 110 | 61 | 60,000 | 4.4e8 | 1.4° | remote |
| Second War (1940) | 6 | 150 | 61 | 75,000 | 1.0e9 | 1.2° | director |

- **Victorian (1890):** 24-inch projectors with plain carbon arcs on the bridges, the fighting tops and the flying deck, to search for torpedo boats; the boats a small one or none [EST].
- **Pre-dreadnought (1904):** 75 cm projectors (the Russian and Japanese fleets' at Port Arthur and Tsushima), trained by hand at each light on orders by voice pipe [EST].
- **RN pre-dreadnought (1904):** 24-inch on the big ships, 20-inch on the destroyers, each trained at the light [EST].
- **Great War (1914):** 36-inch on the big ships (searchlight towers by the funnels, the bridge's wings), 24-inch on the destroyers, plain carbon arcs, each light trained by its own crew [EST].
- **RN post-Jutland (1918):** As in 1914, now trained from searchlight control positions with directing sights after the night of Jutland [EST].
- **German Great War (1914):** 110 cm on the big ships, 60 cm on the boats, worked from a searchlight control position by a drilled crew: the lights that found the 4th Flotilla at Jutland [EST].
- **USN high-intensity (1918):** 36-inch Sperry high-intensity arcs: about four times the plain arc's beam, narrower [EST].
- **Interwar (1930):** High-intensity arcs everywhere, fewer and bigger lights, trained from control positions [EST].
- **Second War (1940):** 150 cm (44-inch in the RN) high-intensity lights slaved to the fire-control directors [EST].
