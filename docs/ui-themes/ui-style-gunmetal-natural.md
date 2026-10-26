# UI style: Gunmetal, natural map

**Status:** chosen direction for Fleetwright's UI (picked 12 Oct 2026 from the theme trials).
**Live reference:** `docs/ui-themes/theme-trials.html`. Open it and press **4**.
**Drop-in tokens:** `docs/ui-themes/gunmetal-natural.css`

> **2026-10-25:** not applied yet; it will be, later, in one batch (the user). It is a colour scheme, not a change of
> layout: it replaces only the token and type sections of `docs/ui-language.md` (`Ui/Theme.cs`, `UiFonts.cs`);
> the layout grammar, the pieces and the don'ts stay. Until then everything uses the bureau tokens.

This replaces the current slate-blue, brass and IBM Plex look, which reads as generic "AI dashboard". Keep the layouts, density and behaviour of the existing mockups. Only the visual treatment changes.

## The idea in one paragraph

The interface is a set of **instruments**: warm graphite panels with no blue cast, cream enamel lettering, engraved condensed capitals for headers, and one cadmium-orange pointer for whatever needs attention. The map underneath is a **plain physical atlas**: muted blue water, lighter blue shallows, pale sage-green land, faint grid. The two layers should read as different materials. The panels are crafted and characterful; the map is quiet, familiar and unstylised, because it carries most of the information.

References: Dreyer Fire Control Table dials, engraved brass instrument plates, AP 507 Home Fleet greys (UI); ordinary physical atlases (map).

## Rules

1. **No blue in the UI chrome.** Every neutral is warm graphite. Blue belongs to the sea only.
2. **One accent.** Orange `#E8772E` marks the active tab, the selection, primary buttons, changed values and panel titles. Never use it decoratively. Warnings use amber `#E8B64A`, which is a different hue.
3. **The map is not themed.** No hatching, glow, scanlines, textures or decorative sea names. Only the land, sea, shoal, grid and contour colours below. Symbols and labels sit on top of it.
4. **Friendly is cream, hostile is red.** Our ships are cream `#F1ECDD` so they stand out on blue water. The same cream marks friendlies everywhere: the forces rail, contact boxes and pips. Hostile is `#E04A3C`.
5. **Three type roles, never mixed:**
   - Headers: **Saira Extra Condensed** 700, UPPERCASE, letter-spacing `.16em`, about 11.5px, in the accent colour.
   - UI text: **Barlow Condensed** 400/500/600 at **13px**. It's narrower than Archivo Narrow, so go up 1px.
   - Numbers and data: **Red Hat Mono** 400/500 at 11px with `font-variant-numeric: tabular-nums`. This replaces IBM Plex Mono everywhere.
6. **Corners are square** (1px radius at most). Panels get a bevel instead of a shadow: a 1px light line at the top inside edge and a dark line at the bottom.
7. **Header ornament:** a small 5px graphite "rivet" dot before each panel title (`.ph::before`). That's the only flourish.
8. **Map labels** use `--maplab` (`#B7C7CF`), not `--muted`, because grey text gets lost on blue water.

## Tokens

Variable names match the existing mockups (`map.html`, `overview.html` and the other designer tabs), so the `:root` block in `gunmetal-natural.css` can replace the old one directly.

| Role | Token | Value |
|---|---|---|
| Page ground | `--ground` | `#0F0F0E` |
| Panel | `--panel` | `#1B1B19` |
| Panel, raised / group row | `--panel2` | `#22221F` |
| Input well | `--inset` | `#0B0B0A` |
| Header strip | `--hd` | `#141412` |
| Row divider | `--line` | `#272724` |
| Hard edge | `--edge` | `#050505` |
| Floating HUD glass | `--glass` | `rgba(27,27,25,.92)` |
| Body text | `--text` | `#D3CFC4` |
| Bright text | `--bright` | `#F6F3EA` |
| Secondary text | `--muted` | `#8A867B` |
| Disabled / faint | `--faint` | `#55524B` |
| Accent (old name: brass) | `--brass` | `#E8772E` |
| Accent border, dark | `--brassd` | `#5C3416` |
| Accent tint, light (changed values) | `--brassl` | `#FFC79A` |
| Good | `--ok` | `#8DBE6B` |
| Warning | `--amber` | `#E8B64A` |
| Critical | `--bad` | `#E04A3C` |
| Friendly | `--fr` | `#F1ECDD` |
| Allied | `--al` | `#C9D4D8` |
| Hostile | `--ho` | `#E04A3C` |
| Neutral | `--ne` | `#7FA86A` |
| Unknown | `--un` | `#E8B64A` |
| **Sea** | `--sea` | `#2E5269` |
| **Shallows** | `--shoal` | `#3D6880` |
| **Land** | `--land` | `#A3AD86` |
| **Coastline** | `--landedge` | `#6F7A57` |
| Map grid | `--grid` | `rgba(235,240,242,.09)` |
| Depth contours | `--contour` | `rgba(200,225,238,.28)` |
| Map labels | `--maplab` | `#B7C7CF` |
| Designer plan backdrop | `--plate` | `#262724` |
| Hull / deck (plan view) | `--hull` / `--deck` | `#A29F95` / `#7C796F` |
| Firing arcs | `--arc` | `rgba(232,119,46,.09)` |

Weight-group colours (ledger, outliner, weight band): armour `#9A86C9`, hull `#B7A27A`, fuel `#8A6A48`, armament `#D0503F`, machinery `#E8772E`, equipment & crew `#7FA86A`, fire control `#8EC1DE`.

## Migrating the mockups

The mockups don't use variables everywhere. `overview.html` alone has about 40 hard-coded hex values, and the map canvas code in `map-ui/assets/*.js` draws with its own literals. Swapping `:root` alone won't fully reskin them. Do this pass:

| Old literal | Meaning | Becomes |
|---|---|---|
| `#3A4651`, `#4A5764`, `#4E5D6B` | control borders, kbd keys, button gradients | `--faint` or `#3A3934` / `#45443E` |
| `#2A333C`, `#2E3842`, `#232B33`, `#38434E` | button gradient stops | `#2E2D29` → `#232220` (hover `#3A3934` → `#2E2D29`) |
| `#0A0D10` | input borders | `--edge` |
| `#151B20`, `#192026`, `#161C21` | bar troughs, group rows, menus | `--inset`, `--panel2`, `--hd` |
| `#4E5A64`, `#56626C`, `#46525C` | disabled text | `--faint` |
| `#AEB9C1`, `#AEB9C3`, `#8E9AA3` | secondary body text | `--text` or `--muted` |
| `#FFE7A8`, `#F1D79C` | changed value / over-limit text | `--brassl` |
| `#6B5A30` → `#4A3E20` | gold primary button | `#E8772E` → `#B85A1E`, text `#140800` |
| `#2E2717` / `#5A4A24` | over-limit verdict box | bg `#2E1C10`, border `--brassd` |
| `#1C3329` / `#2F5A40` / `#9FDDB7` | OK verdict box | bg `rgba(141,190,107,.12)`, border `--ok`, text `--ok` |
| `#3a3014` | selected segment / speed button bg | `rgba(232,119,46,.16)` |
| `rgba(217,184,104,…)` | brass tints (hover, changed rows) | `rgba(232,119,46,…)` at the same alpha |
| `#16283a`, `#0d1a24`, blue map fills | friendly symbol fill, map backgrounds | `#26323A`, `--sea` |
| `#5B9BE0` (`--blue`, `--fr`) | friendly / fire control | `--fr` cream on the map; `#8EC1DE` for the fire-control weight colour |

Fonts: `Barlow Condensed` is already in `designer-ui/assets/fonts/`. Add woff2 files for **Saira Extra Condensed** (500, 700) and **Red Hat Mono** (400, 500), and update `dense-fonts.css`. Drop Archivo Narrow and IBM Plex Mono from these screens.

## Don'ts

- Don't bring back slate-blue greys for panels, even as a "subtle" tint.
- Don't use the orange for more than one thing per panel.
- Don't style the map: no paper texture, glow, hatching or parchment.
- Don't make friendlies blue on the map. They disappear into the sea.
