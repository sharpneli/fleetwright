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
  in a turn it may differ from the arrowhead.
- **A ship lost**: a hollow diamond, an 8 px course tick, and the 2σ ellipse. It's the one place the ellipse says something:
  it comes in as it grows past 10-20 px and goes between 80 and 300 px (major semi-axis on screen, reference px), where it
  no longer locates her; hovered or selected it's shown whatever its size.
- **Smoke**: the smoke itself, from the field, is the mark (user): no ellipse, no smudge. Its label while it's in sight;
  its bearing only on intent. A held ship seen only by her smoke keeps her diamond and tick, no ellipse. Smoke alone no
  longer in sight has nothing on the map, no label and no pick; the rail still lists it.
- **Unknowns** (friend or foe not made out) keep the quatrefoil and tick; **lights** their spark and bearing.
- **The course line only on intent**: selected, hovered, Tab held.

## Open

- Pips crowd where targets lie close together (the German line at 16:15); a count beside one pip past three, if
  playtesting says so.
- Arrowheads stack in a tight column; 2 px if it bothers in play.
