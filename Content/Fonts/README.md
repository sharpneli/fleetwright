# Fonts

The game UI's fonts (`UiFonts`; docs/ui-language.md "Type"), latin subsets converted from the WOFF2 files in `docs/designer-ui/assets/fonts`
(fontTools, flavor dropped; the glyphs are untouched). All are under the SIL Open Font License 1.1
(https://openfontlicense.org), which allows bundling them with the game.

| file | family | copyright |
|---|---|---|
| `barlow-condensed-latin-{400,500,600}-normal.ttf` | Barlow Condensed (the UI) | Copyright 2017 The Barlow Project Authors |
| `saira-extra-condensed-latin-{500,700}-normal.ttf` | Saira Extra Condensed (title strips; 500 lends Barlow its ↑ ↓) | Copyright 2016 The Saira Project Authors |
| `red-hat-mono-latin-{400,500}-normal.ttf` | Red Hat Mono (figures) | Copyright 2021 The Red Hat Project Authors |
| `ibm-plex-sans-condensed-latin-{400,600}-normal.ttf` | IBM Plex Sans Condensed (the ship card's poster) | Copyright 2019 IBM Corp. |

Saira and Red Hat Mono came from fontsource's latin subsets (@fontsource 5.1.0); their WOFF2 files are beside the
others in `docs/designer-ui/assets/fonts`.

Before a release, add the OFL's full text here as `OFL.txt` (the license asks for it to travel with the fonts).

The subsets cover Latin-1 plus general punctuation (dashes, primes, ellipsis, ·, ×, −, °); no arrows or check marks,
so the UI draws those as shapes.
