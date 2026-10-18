# Outfit templates

Written by `shipgen templates` from `src/Fleetwright.Shipgen/Outfits.cs`; don't edit by hand. The designer's
templates are `Content/Templates/outfits.json`, from the same table.

A design's `outfit` block says what burns aboard (`docs/battle/fire.md`; `docs/battle/damage/17_fire.md` §2):
each kind of space's fire load, MJ per m² of deck, as fitted and as stripped for war (the ship's orders before
action pick one), the paint's load on every space but the weather deck, and the growth factor over a furnished
room's "medium" t² fire (1 MW at 300 s). The sim reads the numbers: a space flashes over only on a load that can
feed it (about 230 MJ/m² and up), burns as long as its load lasts at the heat its openings let through, and a
wooden weather deck burns at the deck load, a steel one at half. A key not given takes its navy's template in
its plant's year (old designs got the whole block so when the format moved to 8).

```json
"outfit": {"name": "Great War (1914)", "fitted": {"quarters": 420, "cabins": 750, "offices": 420, "stores": 1500,
  "machinery": 150, "deck": 300}, "stripped": {"quarters": 400, "cabins": 450, "offices": 420, "stores": 1500,
  "machinery": 150, "deck": 150}, "paint_mj_m2": 50, "growth": 1}
```

| Template | Quarters | Cabins | Offices | Stores | Machinery | Deck | Paint | Growth |
|---|---|---|---|---|---|---|---|---|
| Victorian (1890) | 450 / 380 | 900 / 600 | 420 | 1500 | 150 | 400 / 350 | 30 | 2.0 |
| Pre-dreadnought (1905) | 450 / 380 | 900 / 600 | 420 | 1500 | 150 | 400 / 350 | 60 | 1.5 |
| Great War (1914) | 420 / 400 | 750 / 450 | 420 | 1500 | 150 | 300 / 150 | 50 | 1.0 |
| Interwar (1939) | 380 / 240 | 650 / 280 | 500 / 300 | 1500 / 1200 | 150 / 120 | 250 / 120 | 100 | 1.3 |
| Post-Savo (1943) | 240 | 280 | 300 | 1200 | 120 | 120 | 10 | 0.7 |

As fitted / stripped where they differ, MJ/m².

- **Victorian (1890):** Varnished and oiled wooden partitions, panelled cabins, boats and hammocks on deck (the Yalu's "alleys of roaring flame", Santiago). Stripped: the woodwork and boats landed before action, as the Chinese did before the Yalu; not enough. Growth twice a furnished room's [EST].
- **Pre-dreadnought (1905):** Less varnished wood, more coats of paint ("war paint" burned on Alexander III). Stripped as the Russians were at Tsushima: woodwork out "as far as possible", yet Orel had 34 fires.
- **Great War (1914):** Stripped at mobilisation (Queen Mary, August 1914: boats, wooden fittings, furniture landed); wooden weather decks stay, wetted before action. As fitted is the peacetime ship [EST].
- **Interwar (1939):** Comfort back: linoleum, cork, upholstery, up to 80-90 coats of paint (Savo's inventory). Stripped: inflammables landed, the paint left on (scraping it is a refit).
- **Post-Savo (1943):** The USN after Savo: linoleum up, paint scraped to bare steel, cork, rugs and curtains banned, retardant fabrics (NBS berthing 240 MJ/m²); already stripped.
