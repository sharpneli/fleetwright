# Ship designer UI mockup (2026-10-09)

This is the visual reference for the in-game ship designer. The reasoning is in `designer-ui-research.md` (also in the cloud project as claude/designer-ui-research.md). The current proposal is §15.6–15.7, roles and matchups are §14, and the engine asks are §12 and §14.4.

**It is a mockup, not code to port.** Take the layout, hierarchy, colours and interactions, not the HTML.

## Open it

Open `follow.html` in a browser; it works offline. Click around, because the transitions are the point. It needs `dc-lite.js`, a tiny template runtime that is not part of the design, and `assets/`.

## The design: one screen

The screen has four fixed parts:

- **Left rail:** sections 0–8. Pick one by clicking it, pressing its number key, or clicking a part of the ship, a Legend line or a remark.
- **Centre:** the ship sprite is pinned at the top and never moves. The space under it rearranges itself for the section.
- **Right:** the Legend. It holds the particulars, the weight bar with the request line, a mini "catches · escapes · beats" chart and the remarks.
- **Top bar:** sketches A/B, Compare, the request badge (amber/green, never red) and the classified role.

### Sections

**0 · Overview** is the default and opens first. It is a trim sheet for last-minute weight tuning:

- a medium side profile with the armour thicknesses on the plates
- a lever row each for speed, range, belt, armour deck, turret faces, rounds per gun and secondaries per side. Each row shows what one step saves.
- standard displacement vs the request, with the margin
- full load, length and GM
- role scores with trend arrows
- Reset and "Fit with speed".

Everything reacts live.

**1–8** are for detail work. Picking one **animates its widget into the space under the ship**:

- **Protection:** the side profile grows into the armour editor.
- **Hull:** the profile grows half-way.
- **Role:** the catches · escapes · beats chart flies out of the Legend into the centre.
- **Main battery, Speed, Fire control, Crew:** the widget grows out of the dock row at the bottom while the previous one shrinks back.

Each widget is drawn at its big size and moved with a translate + scale transform between a docked rect and a focused rect (0.55 s ease-out). The section's panel fades in after the widget lands. Only one thing grows at a time, everything returns to where it came from, and the ship and the Legend numbers never move.

## Screenshots (`png/`)

| File | State |
|---|---|
| `follow-overview.png` | 0 · Overview as opened: 970 t over the request |
| `follow-overview-trimmed.png` | after −0.5 kn, −0.5 in belt, −20 rounds: 147 t under, Line of battle 0.84 → 0.81 ▼, badge green |
| `follow-protection-mid-transition.png` | 250 ms after picking Protection: the profile growing |
| `follow-protection.png` | Protection: armour editor with the thicknesses on the plates |
| `follow-back-to-overview-mid.png` | mid-transition back to Overview |
| `follow-role-mid-transition.png` | 250 ms after picking Role: the chart in flight from the Legend |
| `follow-role.png` | Role: big chart and fitness panel, with an empty slot in the Legend |
| `follow-main.png` | Main battery: arrangement strip grown from the dock |
| `follow-machinery.png` | Speed: the speed→displacement curve |
| `follow-hull.png` | Hull: profile half-grown with the forecastle and tower labels |

## Which numbers are real

**Real shipgen output:**

- the Dreadnought sprite and base particulars
- the speed curve (17–25 kn builds of `dreadnought.json`)
- the 20.47 kn goal-seek result.

**Illustrative:**

- the other tonnage-per-step figures in the trim sheet
- the enemy classes and positions in the matchup chart
- the role scores.

## Roles in one line

The class is an output. The player's assigned role is an order for their own fleet AI, while enemy AI and intel use only the assessed class. Gameplay limits are amber consequences, never red refusals.

## Design tokens

| Role | Value |
|---|---|
| Ground | `#141D24` |
| Panels | `#1B2730`, `#22313B`; inset `#15242E` |
| Lines | `#304350` |
| Text | `#E3E9EC`, muted `#93A6B0` |
| Accent / selection | brass `#E0AE4F` |
| Main battery | blue `#5B9BE0` |
| OK | `#9FDDB7` on `#1C3329` |
| Consequence (never red) | `#F1D79C` on `#2E2717` |
| Legend paper | `#EFE7D6`, ink `#26221C`, amber ink `#8A5A10` |
| Armour ramp (thin → thick) | `#F3E3A1` `#E9B66B` `#DE9455` `#CC6F43` `#B24E36` `#8E3328` |

Fonts (OFL, bundled in `assets/fonts`):

- UI: IBM Plex Sans Condensed
- Figures: IBM Plex Mono
- Legend: Libre Caslon Text

The mockup is fixed at 1600×960.
