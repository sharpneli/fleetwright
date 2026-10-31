# Plan: striking colours, abandoning and scuttling

Status: **drafted 2026-10-31**; the user's decisions the same day ("Decided"); waiting for the research
(`research/REQUESTS.md`, 2026-10-31: "Striking colours, abandoning and scuttling"), then a session of its own.
Updated: 2026-10-31

Step letters **SK** (free in the docs).

## Why now

`balance.md` BL5, the user's answer 4 (2026-10-31): a stopped ship keeps a fight's end open, and that stays until this
exists: "We'd rather do the striking colors/abandon ship next." `crew.md` decision 3 left it as the captain's call,
after the crew plan: a command step, once a ship with nobody left to fight her exists (N2 made that ship).

## The aim

A ship that can no longer fight, or no longer float, ends her part in the fight the way the record's ships did, by
her captain's call:

1. **She strikes** (Oryol, Nikolai I): stops, her colours come down, her guns go quiet; the enemy, once he reads it,
   ceases fire on her. She's taken if he holds the sea about her at the end, else she's a hulk.
2. **She's abandoned** (Mainz, Blücher, Cöln): her men take to the boats and rafts and over the side; she's left,
   to sink or drift.
3. **She's scuttled** (Rurik, Varyag, Lützow): her own people sink her, sea cocks or charges, or a consort's
   torpedo, rather than let her be taken.

And the fight's ends see her as done: no stopped wreck holds "every side done" or "quiet" open, because she's
struck, abandoned or gone, which is what decision 4 asked for in place of an end-rule patch.

## What there is today

- **Falling out** (`Fitness`, `FallOutRule`, doctrine's FallOut fields): a ship past her thresholds leaves the line for
  home (`CaptainOrder.Home`).
- **Ships seen done for** (`crew.md` N5b, `fire-control.md` F10): captains stop firing at a burning, silent wreck, and
  fire again the moment one of her guns does.
- **Her condition:** `ShipStatus` is Afloat or GoingDown; the crew's killed and wounded, her stations manned
  (`crew.md` N2-N3); the water, list, power and speed she can make (`Flooding`, `Functions`).
- **What the enemy sees:** `SeenFlags` Stopped, Silent, Burning, Low, Listing, GoingDown, Gone.
- **The ends** (`ScenarioEnd`): decided, quiet, done, time.

## The steps (proposed)

- **SK0 The yardstick.** The panel's plays that end by the clock with ships stopped afloat in an encounter (how many,
  in what state: guns, speed, water, men), beside the research's cases once it's in. No new bench unless the cases
  need one.
- **SK1 Her state.** `ShipStatus` gains Struck and Abandoned (scuttling ends in GoingDown, its cause recorded): in the
  condition, the snapshot, the hash, the combat log, the card and the result.
- **SK2 The captain's call** (`CaptainLayer`, as his own judgement on what he knows, thresholds named; memory: the
  admiral's or captain's judgement, not fixed rules):
  - *Abandon* once she's past saving: sinking (her water and list's trend against what damage control gains, the
    "doom clock" of `09_damage_control.md`), or burning beyond her parties.
  - *Strike* once she can't fight (no gun that bears and works, or no men to fight them) and can't get away (stopped,
    or slower than the enemy in touch, `MatchupJudge.Outruns`), with the enemy closing.
  - *Scuttle* in place of striking where doctrine says so and her people can still do it; a consort's torpedo if one
    is by.
  - The side's doctrine sets how far: a new field (decision 1); the captain's own temper moves it, past his doctrine
    at either end, and the crew's state under fire with it (decision 2).
- **SK3 Read by the enemy.** A strike is seen as a navy sees anything (range, light, haze): her colours down, stopped,
  quiet. Once read (a new `SeenFlags.Struck`), captains cease fire on her; she isn't a target, a threat or a matchup
  entry. A struck ship whose gun fires again is a foe again, as N5b has it.
- **SK4 The victor.** Taken if his side holds the sea about her at the end (decision 3); no prize crews or tows sailed
  yet.
- **SK5 The ends and the result.** Struck, abandoned and scuttled ships are done for "every side done" and hold no
  encounter open; the result counts taken, scuttled and abandoned apart from sunk.
- **SK6 Survivors** (rough, memory: rough for non-core systems): an abandoning crew's share got off by the time and
  list it had; picked up by friends, by the enemy or not at all, as the research's shares have it. Counted, not drawn.
- **SK7 The panel.** The plays by the clock, and losses, before and after.

## Decided (the user, 2026-10-31)

1. **Doctrine sets it, and the captain moves it:** "Even with merciful doctrine someone will fight to the end, and even
   with Stalin tier 'not one step back' some will surrender." The side's doctrine field (never to when hopeless) is
   the bar; each captain's own temper (`DoctrineField.Temper`, the person: memory "captain is the person") shifts it
   either way, wide enough that a captain can go past his doctrine at both ends. So SK2's thresholds are a spread
   over captains, not one number a side.
2. **The player's ships strike without asking,** "as per how much of a wimp the captain is and how shaken the crew
   is": the captain's temper and the crew's state under fire (`crew.md` N3b's steadiness, read from Experience and
   WorkUp, and what they've been through) both enter the call. No popup; the log says so; a ship ordered by hand to
   fight on does.
3. **Prizes** as recommended: taken if the victor holds the sea about her at the end; prize crews and tows later.
4. **Scuttling** as recommended: in this cut.
5. **Survivors** as recommended: counted, roughly (SK6).

## For the user (as asked, 2026-10-31)

| # | The question | Recommended |
|---|---|---|
| 1 | How is striking set: a doctrine field a side chooses (never, when hopeless), with period presets like damage control's? | Yes: a doctrine field, "never" (Monmouth, fought to the end) to "when hopeless"; the default "when hopeless". It's the player's to choose, not a navy's number (memory: no navy damage values). |
| 2 | The player's own ships: does her captain strike without asking? | Yes, by the doctrine the player set (no popups in combat); the log says so. Ordered to fight on by hand, she does (the player overrules). |
| 3 | Prizes: taken if the victor holds the sea about her at the end, or sailed home by a prize crew? | Held at the end is enough for this cut; prize crews and tows later, with the campaign. |
| 4 | Scuttling: in this cut, or striking and abandoning first? | In it: it's the same call with a different end, and several of the era's cases are scuttlings. |
| 5 | Survivors: counted in this cut (SK6), or left for later? | Counted, roughly: the result screen's killed is what players read, and abandoning ships make most of the era's survivors. |
