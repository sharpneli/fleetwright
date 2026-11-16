# Allocating forces: the jobs an admiral fills

Agreed with the user 2026-11-16, after the relative capital line (bugs.md "An 1890s fleet is all flotilla"). Steps AL0-AL8.

**The rule (the user):** "An Admiral has a bunch of ships and then he allocates them to jobs, not because he wants to find
somehow a correct slot for a ship based on tonnage but because those jobs *need* to be performed." The highest admiral
weighs his own forces against the enemy's and allocates them as well as he can; each admiral below does the same with
what he's given. Not an LLM-tier mind: a cheap best effort, cascading down the chain.

## Decisions (the user, 2026-11-16)

1. **The squadrons are the atoms.** The commander allocates the units the scenario gives (in the grand campaign his AI
   makes the squadrons). When squadrons are worn down in a scenario he may regroup them, so the fleet isn't a string of
   stragglers.
2. **Contact changes the assessment.** The opening disposition is the admiral's decision on the intelligence he had
   beforehand; once the enemy is held he judges against what he holds. **The intelligence is good** (the user: "knowing
   the ships after all is just an accurate intel report in the end"): before contact he knows the enemy's order of
   battle as the scenario has it, his ships and units but not where they are.
3. **The task sets the need.** A unit on passage, defensive, doesn't spread into a search pattern; a search spreads as
   wide as the search needs.
4. **The fast heavy ships are the admiral's call in battle:** join the line, flank the enemy rather than join the
   congaline of steel, or crush his cruiser screen (battlecruisers are "hilariously effective" at that).
5. **Organisation never says how a ship fights.** A destroyer flotilla's core is its destroyers; they still fight like
   destroyers (fast, unarmoured, torpedoes) because the matchup says so against what's in front of them (admirals-orders.md
   decision 7).
6. **No size lines** (the user, the same day): what made a capital ship depended on the era; nothing in the AI is decided
   by a fixed tonnage or calibre.

## What there is

- **A unit's role is labelled from the unit alone** (`Roles.Derived`): capital ships make it Heavy, half its ships boats
  a Flotilla, else a Scout. What else the fleet has doesn't enter, nor speed. A role the player sets holds (O6).
- **The commander's main body** is the unit with the most capital tonnage (`CommanderLayer.Command`); the others' idle
  tasks follow their role (`IdleTask`: heavy units join it, scouts scout off its bow, flotillas and screens screen the
  nearest heavy unit).
- **A force's admiral** keeps his strongest role in his line and sends the rest to attend it (`AdmiralLayer.Dispose`); a
  force with two capital ships deploys into line (`BattleForce`).
- **At contact** the commander sends his free units against the held groups, gravest first, by the matchup (`Assign`),
  and judges his side against all he holds (`Balance`). This is already "his forces against the enemy's"; it keeps.
- **The enemy's main body** is his biggest held group with a capital ship (`TacticalPicture.MainBody`).

What it gets wrong: the prizes' war fought only because each cruiser-and-destroyer force read as one flotilla; a lone
battleship squadron has no lookouts on its wings; battlecruisers join the line by their tonnage.

## The model

### What a unit brings

Measured per unit at a think, every one relative to his own fleet or to the enemy, from the matchup profiles
(`Combat.Matchup`, `MatchupJudge`), never from a type:

- **Strength:** what it puts out and what it takes in the matchup, against the reference (decision 2: before contact his
  intelligence, after it the enemy he holds).
- **Speed:** its sustained speed, the slowest ship's as she is now (damage and all).
- **Attack:** whether its torpedoes beat its guns against the enemy's main body (`AttackerAgainst`, as now).
- **Screening:** how its guns do against the enemy's attackers (the matchup against them), and whether it can keep
  station on the main body.
- **Eye:** how far it sees and is seen (its tops, `Sightings`' reach): what a lookout on a wing is for.

### The jobs, set by the task

A task names the jobs it needs and how much of each (decision 3); jobs nobody can fill go unfilled (a battleship
squadron alone has no screen):

| Task | Jobs |
|---|---|
| Passage (go to, join, withdraw) | main body; lookouts on the bows (a ship each wing, about 45° and a visibility out); a screen if attackers are expected |
| Search | a search line as wide as the search needs, by the visibility (`SearchLine`, `SeaRoom`) |
| Approach to a fleet action | main body; scouts ahead and on the flanks, strong enough to drive off the enemy's; a screen against his attackers |
| Escort, cover | a screen round the charge; a covering force between it and the threat |
| Battle | the line; the fast wing (decision 4); the attack, when the commander sends it in; the screen |
| Withdrawal | main body; a rearguard (`JudgeRearguard`, as now) |

### The allocation

A greedy pass by comparative advantage, cheap enough for every think:

1. **The main body:** the strongest unit, joined by the units of comparable strength that have no speed to spare. Among
   the strong units the slower core is the line; the faster are what he can send ahead. A fleet's only strong unit is its
   main body however fast it is.
2. **The other jobs, most needed first:** each takes the unit best suited to it (scouts by speed over the main body, then
   strength against what the enemy scouts with; a screen by its guns against his attackers; lookouts by their eye) whose
   loss costs the main body least. When nothing better is left, a job takes a unit of the main body: a battleship on each
   wing of a squadron with no cruisers.
3. **What's left** stays with the main body, or is kept back (no main battery: merchants, tenders).

The thresholds ("comparable", "speed to spare", "enough strength") are the admiral's judgement: doctrine values,
relative to his own fleet, as with his other calls (an admiral strips his line for scouts more readily than another).

### The cascade

The commander allocates his top units; a force given a job is a task to its admiral, who allocates his members to the
jobs that task needs by the same pass; a squadron's admiral stations his own ships (a lookout on a wing is a station in
his formation, not a unit of its own: decision 1). The player's roles and tasks hold over all of it (a role set by hand is
a job given by hand).

### At contact

The assessment is made again against what he holds (decision 2):

- **The enemy's main body** is his strongest held group, by the matchup, not the one with a capital ship.
- **The fast wing's choice** (decision 4): the line, if the line is outmatched without it; else where its strength does
  the most for the least, each choice an enemy group or a place valued by what the wing does there against what it
  loses:
  - his line's van, flanking it rather than joining the line;
  - his scouts and screen (his screen gone opens his line to the attack);
  - his fast wing, like against like, kept off our line (Beatty and Hipper; Kamimura and the Vladivostok cruisers);
  - his flotillas going in, broken up before they reach our line;
  - beside our own attack, drawing his secondaries and screen off our flotillas (Hipper's "death ride");
  - across his way home or to his aim, when he's the one leaving (a Stop by the fast part of the fleet);
  - his cripples and stragglers, once he's beaten or breaking off: only the fast wing catches anyone.

  No reserve held back as a choice of its own: a wing that no choice repays already stays clear (the matchup declines),
  and committing a reserve at the turning point is beyond a cheap best effort (approved by the user 2026-11-16).
- **The line forms** when the main body fights with its guns against what's in front of it (in place of `BattleForce`'s
  two capital ships).
- **The attack and the screen** as now, the attack by the matchup against his main body.

### Regrouping (decision 1)

A squadron worn below what its job needs, with a unit of the same job near, is merged into it (the task menu's merge,
O9); stragglers of a squadron gone are gathered into one.

### The types

Once nothing decides by them, `ShipType` is the map's and the reports' word only. A contact made out no further than her
type carries an estimate of her size (the lookout's, its error shrinking as he makes her out) and whether she's armed;
what's estimated from it (her side's classes near that size, a torpedo's aim) reads the estimate. Every decision now
reading a type (the captains' targets, the line's torpedo threat before her class is known, the screen's keep-clear, the
aims' kinds) reads her matchup instead.

## Steps

| Step | What | State |
|---|---|---|
| AL0 | **The bench:** `shipgen allocation SCENARIO`: each side's units, their measures and jobs at the start and at first contact; fixtures for the cases below | **done 2026-11-16** (below) |
| AL1 | **What a unit brings:** strength, speed, attack, screening, eye, per unit at a think, from the matchup profiles | |
| AL2 | **The jobs by task**, the table above | |
| AL3 | **The commander's allocation** of his top units: replaces `Roles.Derived` and the main body by capital tonnage; set roles held | |
| AL4 | **The cascade:** the force admirals by the same pass (`Dispose`); a squadron's stations (lookouts on its wings) | |
| AL5 | **At contact:** the enemy's main body by strength, the fast wing's choice, the line by guns | |
| AL6 | **Regrouping** the worn-down squadrons | |
| AL7 | **The types out of decisions:** contacts' size estimate, every consumer onto the matchup | |
| AL8 | **The panel and the early wave**, the prizes' war and the Bosporus looked at again | |

### AL0 The bench (built 2026-11-16)

`shipgen allocation [SCENARIO...] [--minutes 240] [--no-land]`: every unit down each side's tree, its ships, tonnage and
slowest speed, its role and task, at the start and five minutes after the first contact (the commanders' thinks on it).
With no scenario, the cases below: three synthetic ones in `tests/Fleetwright.Sim.Tests/Allocation/` (a battle squadron
alone; battle fleets, Britain's as the commander's top units, Germany's one force under its admiral; destroyers alone),
then the prizes' war, the Yalu and the Jutland. The baseline, before any change: `docs/battle/allocation/al0-baseline.txt`.
What it shows:

- The battle squadron alone keeps its line on passage: no ship on a bow.
- Battlecruisers are Heavy by their tonnage and join the line (Britain's 1st BCS; Germany's I Scouting Group stays in
  the High Seas Fleet's line).
- Destroyers alone are Heavy (the relative capital line), and the prizes' war's and Yalu's roles as found in BL6's
  types pass: the cruisers' forces Heavy, their destroyers sent off to scout; Japan's Main Squadron a Scout; the
  Beiyang Fleet's armoured cruisers and cruisers Flotillas, screening its ironclads.
- At the Jutland the Cruiser screen (armoured cruisers) and Hampshire are Heavy, sent to scout only because the
  scenario gave them that task.

The cases the bench keeps (AL0), each with what an admiral would do:

- A battleship squadron alone on passage: a battleship on each bow as a lookout, no screen.
- Battleships and battlecruisers: the battleships the line, the battlecruisers scouting; in battle the fast wing's choice.
- Destroyers alone: the destroyers are the main body, and fight as destroyers.
- The prizes' war: each force's cruisers its main body, its destroyers its screen (not attackers against cruisers).
- The Yalu: China's ironclads the main body; Japan's Main Squadron the line, the Flying Squadron scouting, then the fast
  wing.
- The Jutland's Grand Fleet: the battleships the line; the battlecruisers and Hood's squadron scouting ahead, the
  armoured cruisers a scouting line; the light cruisers and destroyers screening, the destroyers sent in to attack
  (`research/jutland-1400-dispositions.md` for the record's dispositions).

## Questions for the user

1. ~~The intelligence before contact~~: good intelligence, the enemy's order of battle (decision 2).
2. ~~The fast wing's choices~~: the line, the flank, his screen and the five more under "At contact", no reserve
   (approved 2026-11-16).
