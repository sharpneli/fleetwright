# Plan: striking colours, abandoning and scuttling

Status: **drafted 2026-10-31**; the user's decisions the same day ("Decided"); the research in the same day
(`research/simulation/striking-colours.md`, cited as SC with its R1-R8); planned against it and the code ("The plan,
after the research"), then built.
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

## The plan, after the research (2026-10-31)

### What the research changes

- **The trigger is three things at once, not a casualty count** (SC summary 1-2): she can't hurt the enemy, can't get
  away, and an enemy is in reach. Killed and wounded ran 7-50% at the decision, so the men enter only through the guns
  they work (crew.md N2 already does that) and through the crew's state in decision 2's sense, as a nudge, never a
  trigger.
- **Abandoning a sinking ship needs no enemy** (SC R1): it isn't giving up the fight, it's saving the men, and every
  navy did it. So the call has two halves: *give her up* (strike or scuttle: hopeless, the enemy near) and *leave her*
  (abandon: she won't float long enough).
- **Navies differed more in the end chosen than in when** (SC summary 4, 6): the British and Germans 1914-18 never
  struck, the Germans scuttled, the Russians scuttled first and struck only to save a sinking ship's men, the Spanish
  struck once aground. So doctrine needs two fields, not one: how readily a hopeless ship strikes, and how readily one
  not struck is scuttled. A navy that does neither fights on until the water decides (Monmouth, Wiesbaden, Shark).
- **The sign that counts is the colours down on a ship stopped and quiet** (SC summary 7, R4); fire ceases within
  minutes once that's read. The enemy's reading is the sightings' job, as everything else he sees.
- **Scuttling is holes, so the time is the flooding model's** (SC R6): charges in the machinery spaces, 10-30 min.
- **Survivors are set by the mode of loss and whether a rescuer was near** (SC summary 9, §3.4), not by size; a rough
  regime table is enough (decision 5).

### How it fits the game

- **The ends.** A ship struck or abandoned is out of the fight for every rule that keeps one open: she's not afloat
  for the aims (`Aims.Afloat`, so "decided" and "every side done" stop waiting for her), she heats no encounter (so
  "quiet" can come), she's no longer in her group. That's the user's answer 4: the system ends the hold, no end-rule
  patch.
- **The losses.** The record counts a struck or scuttled ship as lost, and so does the result: `Lost` takes her, and the
  result says how each went (sunk, taken, struck, scuttled, abandoned) and the men saved. The balance panel's losses
  move only as much as ships the enemy left alone afloat now go (scuttled, abandoned) or stay up (struck and spared).
- **The player.** Decision 2: his ships strike or scuttle by the doctrine he picked and their captains' tempers, with
  no popup; the log says so. A ship he holds by hand (`Whose.Player`, the conn) never strikes nor scuttles: "a ship
  ordered by hand to fight on does". She still abandons once she's going (the water isn't an order he can overrule,
  and the men's lives change nothing in the fight: her guns stop when she founders anyway). A struck or abandoned
  ship takes no more orders: her colours are down for good (striking again after firing is a ruse, Baralong's lesson).
- **The enemy AI.** A ship whose colours his navy has seen down is out as a target, a threat and a matchup entry (as one
  seen going down is); fire on her stops at the captains' next think. An abandoned one with her colours up stays what
  she looks: a stopped, silent wreck, which doctrine's `Wrecks` already leaves alone or finishes (Blücher, Mainz).
- **The spread over captains** (decision 1). Scenarios name few captains' tempers, so most are 0; without a spread a
  side's captains would all decide alike. The call reads each captain's temper plus a draw of his own (seeded by the
  world and the ship, so replays hold), as the research's R2 proposes, only for this call: the rest of what tempers
  move (posture, fighting, falling out) stays as balanced. Drawing every captain's temper at load is a later choice for
  the user, named below.
- **Every tier, any compression.** The call is the captains' think's, in both tiers (the fall-out's pass); its clocks
  are world time, so the fine tier says the same at any speed (memory: same at any compression).

### The model

**The pressures** (SC R1), from what her captain knows exactly (her `Fitness`, her condition) and his navy's picture:

| Pressure | Read from | Shape |
|---|---|---|
| Can't hurt | her chief guns working (lasting) as a share; her chief magazines empty | 0 at 0.3 of her guns or more, 1 at 0.1 or less, linear between; 1 out of rounds [EST, Emden and Königsberg at 1 of 10] |
| Can't get away | stopped (under `DoneForKn`), her steering lost or jammed, or slower than an enemy in reach as seen | yes or no |
| An enemy in reach | a foe her navy holds in sight within `GiveUpEnemyM` (12 km [SC: 6-12 km in every case]) | yes or no |
| Won't float | her water's and list's trend (her captain's own, over his looks) against her reserve and `CapsizeHeelDeg`: the time left | the time left, against the time to get her men off |

**The call**, judged at each fighting or hurt ship's think while her condition changes or a candidacy runs:

1. **Leave her** once the time left is under the time to get her men off (`AbandonLeadS` plus her complement at
   `AbandonManS` each [EST, SC §3.2: several ladders at 15-25 men a minute]): abandon, in any navy, with or without an
   enemy, struck or not.
2. **Give her up** once she can't hurt, can't get away and an enemy is in reach, after a delay (SC R3: 5-50 min, median
   ~20, from the moment she became a candidate; a bold captain later, a timid one sooner; reset if she stops being
   one). Then, by his own draws:
   - **strike** if his draw is under her doctrine's `Strike`, moved by his temper (with his own spread) and how shaken
   her crew is (the shaken and frozen share of her men at their stations, decision 2), and more readily if she's also
   sinking (Sissoi: the enemy will take her men off);
   - else **scuttle** if his draw is under doctrine's `Scuttle` and she has the time (she isn't going anyway): charges
   set, then the men off;
   - else **fight on** (Monmouth, Wiesbaden, Shark): she stays as she is until the water decides (1).
   The player's held ships skip 2.

**What each does to her** (world state, the damage system's commands, logged and replayed):

- **Struck:** her colours down (a fact the enemy can see), her guns held, her engines stopped, out of her group. Her
  men stay: damage control goes on (Sissoi's pumps, Spartak's prisoners at them).
- **Abandoned:** her guns quiet and her engines stopped at the order; her men off over `AbandonLeadS` + complement ×
  `AbandonManS`; then nobody aboard (no stations, no parties, no pumps, no power): she floods as her holes let her and
  sinks or drifts.
- **Scuttled:** charges in her machinery spaces (`ScuttleSetS` to set, then holes of `ScuttleChargeM2` a space at their
  bottom [EST, sized so a light cruiser goes in 10-30 min, SC §4.2]), and she's abandoned; the flooding model sinks her.

**Read by the enemy** (SC R4): a new seen fact, `SeenFlags.Struck`, made out where her colours can be (the sightings'
own colours test: her type by day, her class in the dark). Seen, she's out (`Contact.Out`, which `Lost` becomes: going
down, gone, or struck), so captains, admirals and the matchup drop her.

**The end and the result:** `Aims.Afloat` is false for a ship struck or abandoned, and encounters don't count her; the
result counts each side's ships sunk, taken (struck, afloat at the end, an enemy nearer her than any friend within
`TakenM`), struck (a hulk nobody holds), scuttled and abandoned, and the men saved (SK6, rough, at the result: SC §3.4's
regimes by how she went, how long from the order to her end, and whether a ship of either side was near).

**Doctrine fields** (`doctrine.json`, the scenarios set them per navy from the record, crew.md decision 10):

| Field | Meaning | Default preset | Scenario values [EST, SC §1.3] |
|---|---|---|---|
| `Strike` (0-1) | how readily a hopeless ship strikes: 0 never (Monmouth), 1 when hopeless | 0.3 | Britain 1914-18 0, Germany 1914-18 0.1, Russia 1904-05 0.2, Japan 0.1 |
| `Scuttle` (0-1) | how readily a hopeless ship not struck is scuttled rather than fought on | 0.5 | Germany 0.9, Russia 0.8, Britain 0.3 |

The research's `StandBy`, `Rescue` and `Trust` are left out of this cut (friends standing by, the victor's rescue and a
partial sign's slower cease-fire): the survivors' rough count stands in for the first two, and a sign here is always
whole.

### Not in this cut (named)

Beaching (needs the shore along her course: SC R2's first choice where land is near, Santiago, Emden); a consort's
torpedo to finish a friend; the admiral's strike of a sound unit (Nebogatov, rare: the admiral's layer, SC R2); prize
crews and tows (decision 3); running for a neutral port; white flags and partial signs; the victor's rescue as a task
holding his ships; sea temperatures by area and month for the survivors; every captain's temper drawn at load (a choice
for the user: it would move the whole balance, not only this call).

## The steps (as built)

- **SK0 The yardstick:** the panel at `f0f12952` (`balance/bl5d.json`) is the before: 40 of 68 plays by the clock.
- **SK1 Her state:** `ShipCondition.GivenUp` (Struck, Abandoned, Scuttled) and when, the damage system's commands
  and their effects; the snapshot, the hash, the combat log, the card and the rail.
- **SK2 The captain's call:** the pressures and the call above, the doctrine fields, the settings.
- **SK3 Read by the enemy:** `SeenFlags.Struck`, `Contact.Out`.
- **SK4-SK5 The ends and the result:** `Aims.Afloat`, encounters, the result's ways of going.
- **SK6 Survivors:** rough, at the result.
- **SK7 The panel:** before and after; the scenarios' doctrine from the record.

## The steps (proposed, as drafted)

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
