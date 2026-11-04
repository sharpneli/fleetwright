# Fire marks: who fires at whom, without the lines (2026-10-26)

Every engaged battery drew a line from its ship to its target, brass for the selection, friend for ours, red for
theirs: two or three a ship (the main battery and the secondaries each side), hundreds in a fleet action, the user's
main clutter complaint after the first test game of the input rework (`input.md`). What the lines said is mostly said
elsewhere (the rail's target and FIRING, the card, the shells and splashes on the map); what they said badly is fire
distribution (an enemy unengaged, two of ours on one ship), the part worth showing.

## What's drawn (`MapScene.Targets.cs`)

- **Pips on the target**: one a ship firing on her, on an arc over her ring: friend on an enemy we fire at, hostile red
  on one of ours under fire; at most five. A ship laid on but silent has no pip (her tick says it).
- **A bearing tick at the shooter**: a short stub off each ship toward her main battery's target, in her side's colour,
  bright while firing, faint while silent, a moment longer and brighter at each salvo.
- **Unengaged**: an enemy in sight inside one of our open encounters that none of ours fires at, nor lays on: a faint
  dotted ring (decision 2).
- **Lines only on intent**: the selection's (every battery, the main in brass), the hovered ship's (her main battery's,
  and the main batteries on her), and every main battery's while **Tab is held** (decision 1; a tap is still the next
  ship). Lines fade from the shooter toward the target. Secondary batteries draw nothing else.

## Judged side by side (decision 3)

The Jutland at 16:15, 240×, the camera on Lion (`-screenshot=@16:15`, a capture by the world's clock, so before and
after are the same moment): `png/fire-marks-before.png`, `png/fire-marks-after.png`, `png/fire-marks-tab.png` (Tab
held), the fight enlarged in `png/fire-marks-zoom.png` (before, after, Tab); the unengaged ring in
`png/fire-marks-unengaged.png` (left: unengaged; right: under our fire, its pip).

## Decisions (the user, 2026-10-26)

1. **The reveal is a held key**: Tab held (the user never stepped through ships with Tab; the tap still does).
2. **Unengaged enemies marked, subtly.**
3. **Side by side in the game**, not a mockup: it's not a new UI style.

## Contacts (2026-11-04)

The same complaint from the enemy's side: a line of ships read as "a pile of straws with a seizure". A ship in sight is
already drawn whole, as she is (`Snapshot.Sighted`); the diamond, ellipse and five-minute course line sat on top as a
noisier copy. The line magnified the track's noise: over five minutes of the Jutland (16:10-16:15, 33 contacts) a
course swung ~7° within a minute and the line's tip ~33 m a look. It was often wrong too, the constant-velocity track
lagging her turns: course error 3° (p90 10°) on a steady ship, 25° (p90 56°) on one that had turned in the last minute,
~90% of the readings. Side by side at 16:15, 20 m/px: `png/contacts-zoom.png` (before, after).

- **A ship in sight**: her icon and a 3 px arrowhead off her bow, from her own heading, so it can't twitch; no marker, no
  ellipse. Faded out over the world's boundary, where the sprite shows her bow. The card keeps the estimate (`~072°`), so
  in a turn it may differ from the arrowhead. **Every mark on her goes where she's drawn** (`ContactAt`, user: the player
  needn't see the spotting error): the fire pips, label, course line, night ticks, lit rings. The sim still aims by the
  estimate, and the rail's and card's figures are still the navy's.
- **A ship lost**: nothing on the map, no mark, label, pips or pick (user: with ships going in and out of view, the ellipses
  looked like the enemy formation blowing bubbles; the diamond and course tick went first). The rail still lists her;
  selected there, her 2σ ellipse (5 px at least) and course line show where she's reckoned. Fire lines to her stay on
  intent and each shooter keeps its bearing tick.
- **Smoke**: the smoke itself, from the field, is the mark (user): no ellipse, no smudge. Its bearing only on intent. A ship
  seen only by her smoke can be hovered at her reckoned place, then shows her ellipse. Smoke no longer in sight has nothing
  on the map and no pick.
- **Unknowns** (friend or foe not made out) the same, in the unknown's cream: her icon while in sight; the quatrefoil stays
  in the rail. **Lights** their spark and bearing.
- **The course line only on intent**: selected or hovered; with Tab held, every ship in sight.

The diamonds weren't doubles of the ships beside them (the marks of 2026-11-04's second pass, before the ellipses went). At the Jutland (16:10-16:15, side 0) no ship had both: about 26
contacts were in sight and 14 out, 5 of those by their smoke. But a lost ship's mark can fall right by ships still in the
line (three of the six at 16:15 within 200-400 m of one), so it read as a copy. The pips did float, on the estimate, clearly
off the ships they marked. Side by side at 16:15, 6 m/px: `png/contacts-lost.png` (before, after).

## Open

- Pips crowd where targets lie close together (the German line at 16:15); a count beside one pip past three, if
  playtesting says so.
- Arrowheads stack in a tight column; 2 px if it bothers in play.
- Contact labels never show (`docs/battle/bugs.md`); the hover names a ship.
