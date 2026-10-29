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
whole block by their navy so when the format moved to 14; format 16 moved the blocks still as a template gave
them to the research's numbers, Q3b). The numbers are from `research/simulation/night-fighting.md` §1 where it
found them ([SRC] in the notes), estimates where it didn't ([EST]).

```json
"searchlights": {"name": "Great War (1914)", "capital_count": 8, "mirror_cm": 91, "small_mirror_cm": 61,
  "lamp_cd_cm2": 16000, "spread_deg": 3, "control": "local"}
```

| Template | Capital ship | Mirror, cm | Small, cm | Crater, cd/cm² | Beam (capital), cd | Spread | Control |
|---|---|---|---|---|---|---|---|
| Victorian (1890) | 6 | 90 | 40 | 16,000 | 7.8e7 | 3.0° | local |
| Pre-dreadnought (1904) | 6 | 75 | 40 | 16,000 | 5.4e7 | 3.0° | remote |
| RN pre-dreadnought (1904) | 6 | 61 | 51 | 16,000 | 3.6e7 | 3.0° | local |
| Great War (1914) | 8 | 91 | 61 | 16,000 | 8.0e7 | 3.0° | local |
| RN post-Jutland (1918) | 8 | 91 | 61 | 16,000 | 8.0e7 | 3.0° | remote |
| German Great War (1914) | 8 | 110 | 60 | 16,000 | 1.2e8 | 3.0° | remote |
| USN high-intensity (1918) | 8 | 91 | 61 | 60,000 | 3.0e8 | 1.8° | remote |
| Interwar (1930) | 6 | 110 | 61 | 60,000 | 4.4e8 | 1.4° | remote |
| Second War (1940) | 6 | 150 | 61 | 75,000 | 1.0e9 | 1.2° | director |
| RN Victorian (1890) | 6 | 61 | 40 | 16,000 | 3.6e7 | 3.0° | local |
| RN dreadnought (1910) | 8 | 61 | 51 | 16,000 | 3.6e7 | 3.0° | local |

- **Victorian (1890):** 90 cm Mangin or Schuckert projectors on the continental battleships, not less than six on a large ship (1894), plain carbon arcs (a crater of 1.6-1.8e4 cd/cm2), a 3 deg beam, trained at the light, to search for torpedo boats [SRC]; the boats a small one [EST].
- **Pre-dreadnought (1904):** Russia's six 75 cm Mangin lights in pairs (the fore bridge, amidships, the after bridge or the mainmast) with electric remote control, 40 cm on the boats, 1898-1905 [SRC]; the other navies' of the period by it [EST].
- **RN pre-dreadnought (1904):** 24-inch on the big ships (landed from the tops to the shelter deck in 1906), 20-inch on the destroyers, each trained at the light [SRC]; the count [EST].
- **Great War (1914):** 36-inch plain arcs on the big ships (searchlight towers by the funnels, the bridge's wings; the RN's from mid-1915), 24-inch on the destroyers, each light trained by its own crew to orders by voice pipe ('never really efficient', the RN at Jutland) [SRC]; the count [EST].
- **RN post-Jutland (1918):** As in 1916, now four 36-inch over a searchlight control position with directing sights (August 1917) [SRC].
- **German Great War (1914):** Plain arcs kept (the navy rejected Beck's high-intensity arc: worse in the Baltic mist), iris shutters, the lights trained dark from a control position before they were switched on: the lights that found the 4th Flotilla at Jutland, 'much more powerful than ours' (Jellicoe) [SRC]; the 110 cm and the count [EST].
- **USN high-intensity (1918):** 36-inch high-intensity arcs (Beck in Texas 1914, the 1917 specification's 5e4 cd/cm2 and more, Sperry's 9e4), eight on a battleship, distant electric control, tied into fire control by 1918; the beam's spread about 2/3 the plain arc's [SRC/INFERRED].
- **Interwar (1930):** High-intensity arcs everywhere, fewer and bigger lights, trained from control positions [EST].
- **Second War (1940):** 150 cm (44-inch in the RN) high-intensity lights slaved to the fire-control directors [EST].
- **RN Victorian (1890):** 24-inch projectors with plain carbon arcs on the bridges, the fighting tops and the flying deck, to search for torpedo boats [SRC]; the count and the boats' [EST].
- **RN dreadnought (1910):** Twin 24-inch mountings (St Vincent, Colossus, Iron Duke), not 36-inch, hand-worked to orders by voice pipe, until the 36-inch lights of mid-1915 [SRC]; the count [EST].
