# Fresh mockups: sheets on the desk (2026-11-09)

A second visual take on three screens, drawn from scratch to set beside the Dunkelgrau ones. Same content,
same frame (menu, header figures, three columns, keys along the bottom), a different hand.

Open `designer.html`, `launch.html` or `battle.html` in a browser; they work offline. `N` swaps the day and
night skins (or add `#night` to the address). Screenshots at 1920×1080 are in `png/`.

## What is different

- **Paper sheets on a dark desk** (or on the sea), not grey panes. A sheet has a 2 px ink rule along its top
  and its name under it in a Clarendon (Besley), sentence case. No title strips, rivets, sheen or bevels.
- **Ink is the accent.** The chosen row, tab, option and the primary button are inverted: ink ground, paper
  text. No orange. Hue is left to mean state only: red the enemy, amber a consequence, green within limits,
  blue water. The weight groups and the armour ramp keep their colours.
- **Two skins from one set of tokens.** Day is the drawing office; night is the darkened ship, dim warm ink on
  near-black, for night actions. Every colour in `fresh.css` is a variable under `:root` and
  `[data-skin="night"]`.
- **One type family for words and figures**: Sofia Sans Semi Condensed with tabular figures, so no mono face.
  Besley only for sheet names, the ship's or scenario's name and the verdict. Labels are sentence case, never
  capitals.
- **The ship is an ink drawing** with armour as colour washes, plan and profile to one scale (`ship.js` draws
  them from a short spec; it stands in for the game's art).

## Ideas that are more than a skin

| Where | What |
|---|---|
| Designer, trim sheet | Each lever's two step costs are a pair of bars about a centre line (green saves, ink costs), so the cheap and dear levers show at a glance |
| Designer, drawing | The weights run as one bar under the ship, drawn to the request: the gap at its end is the margin |
| Designer, left | Sections replace both the outliner and the numbered tab strip; a section opens to its parts in place |
| Launch, strength | The two sides as bars back to back about the row's name, not a share bar |
| Launch, opening | A paper chart: own ships in ink, the enemy in red (cream cannot show on paper; on the sea it stays) |
| Battle, helm | A compass tape under a fixed lubber line with the ordered course riding on it, and the telegraph as one ladder |
| Battle, card | Water aboard is a blue wash on the profile, hits are red crosses on profile and plan |

## Not done

Only these three screens. The night-fighting HUD, the rail, the strip, the ship card poster and the designer's
other sections are not redrawn. The fonts' Latin subsets have no arrows; keys with arrows need drawing, as now.

## The battle map, rethought (added later the same day)

`plot.html` and `plot.md`: a functional proposal, not a restyle. The rail lists fights, an order is one editable
sentence, a ribbon looks ahead in time, and the helm shows only on the conn.
