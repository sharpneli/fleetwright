# Fonts

The game UI's fonts (`UiFonts`), latin subsets converted from the WOFF2 files in `docs/designer-ui/assets/fonts`
(fontTools, flavor dropped; the glyphs are untouched). All three are under the SIL Open Font License 1.1
(https://openfontlicense.org), which allows bundling them with the game.

| file | family | copyright |
|---|---|---|
| `ibm-plex-sans-condensed-latin-{400,600}-normal.ttf` | IBM Plex Sans Condensed | Copyright 2019 IBM Corp. |
| `ibm-plex-mono-latin-400-normal.ttf` | IBM Plex Mono | Copyright 2017 IBM Corp. |
| `libre-caslon-text-latin-{400,700}-normal.ttf` | Libre Caslon Text | Copyright 2012 The Libre Caslon Text Project Authors |

Before a release, add the OFL's full text here as `OFL.txt` (the license asks for it to travel with the fonts).

The subsets cover Latin-1 plus general punctuation (dashes, primes, ellipsis, ·, ×, −, °); no arrows or check marks,
so the UI draws those as shapes.
