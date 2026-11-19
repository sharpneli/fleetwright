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
| AL1 | **What a unit brings:** strength, speed, attack, screening, eye, per unit at a think, from the matchup profiles | **done 2026-11-16** (no eye measure: AL4 takes lookouts from the squadron's own alike ships) |
| AL2 | **The jobs by task**, the table above | **done 2026-11-16** (scouts, the scouting force's match, the screen) |
| AL3 | **The commander's allocation** of his top units: replaces `Roles.Derived` and the main body by capital tonnage; set roles held | **done 2026-11-16**, with the force admirals' members (AL4's pass) |
| AL4 | **The cascade:** the force admirals by the same pass (`Dispose`); a squadron's stations (lookouts on its wings) | **done 2026-11-17** (the force admirals with AL3) |
| AL5 | **At contact:** the enemy's main body by strength, the fast wing's choice, the line by guns | **done 2026-11-17** (below) |
| AL6 | **Regrouping** the worn-down squadrons | **done 2026-11-17** (below) |
| AL7 | **The types out of decisions:** contacts' size estimate, every consumer onto the matchup | **done 2026-11-17**: AL7a a contact not made out is one not yet accounted for; AL7b the size estimate, the matchup by it; AL7c every decision by the matchup |
| AL8 | **The panel and the early wave**, the prizes' war and the Bosporus looked at again | **done 2026-11-19** (below): Åland back in balance by itself; the Bosporus and the Jutland's German light forces to P11 |

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

### AL1 What a unit brings (built 2026-11-16)

`ForceMeasures` (`AiSystem.Measures`; the bench's columns), each unit's figures against the enemy as his side knows him:
good intelligence, his order of battle as it is (decision 2); with no enemy, the side's own fleet (a reference as rough
as the old one, a side's own heaviest class). The side's references are reckoned from squadrons and ships in none,
whatever forces gather them (a fleet's twenty-four battleships in one force would hide its line otherwise):

- **Strength:** a gunfight against as much tonnage of the enemy's mix as the unit's own, at a fifth, two, three and four
  fifths of the longest main guns' reach between them (the ranges come from the guns, so it holds in any era), the share
  of his it puts out over all four against the share of its own it loses, times its tonnage. Two tries went first: the
  matchup's judgement at the range the enemy picks made a ratio of two near-nothings of a fast enemy keeping out of a
  slow line's reach (the Yalu's Dingyuans worth a quarter of China's cruisers), and its style let his torpedo attack
  count against a line (the screen's to answer). The torpedo threat stays out of a line's worth.
- **Quality** (strength a tonne) and **strength a ship** (quality at the tonnage-weighted mean size of its ships, Σt²/Σt):
  what one of its ships is worth in the line whatever the unit's size.
- **Speed:** its slowest ship's, as she is now.
- **Attacker:** its torpedoes beat its guns against his main body, in half its tonnage or more (a battlecruiser squadron
  with its destroyers isn't one).
- **Boat:** an attacker half of whose ships are under `RoleScoutSizeRatio` (1.5) times the typical boat's size (the most
  numerous class among both sides' attackers): the old relative rule (O7c), kept. A light cruiser and a destroyer are
  alike against a battleship and alike in every fight the matchup weighs; her size is what sets the cruiser apart.
  Germany's light cruisers fare a tenth as well as Britain's against the other's mix, and by their fighting alone they
  were boats.
- **Screening** and **scouting:** the same gunfight against as much of his attackers, or of his scouts (his squadrons
  faster than his line), as its own.

### AL2-AL3 The jobs and the allocation (built 2026-11-16)

`Roles` allocates each admiral's units: the side's top units by its commander's task (`SideNeeds`: on passage, a Reach
pursued, or withdrawing, a screen; else the approach, three scouts, a scouting force a match for his and a screen), a
force's members by the force's task (`ForceNeeds`: closing, scouting or on its orders, three scouts and a screen;
searching, attacking or screening, nothing; on passage, a screen). One pass (`Roles.Allocate`):

1. No main battery: kept back.
2. **The line:** of the armed units that aren't attackers (all, if every one is), the strong (strength a ship at
   `AllocationLineShare` 0.5 of the best or more) within `AllocationSpareKn` (1.5 kn) of the slowest strong one's
   speed; the strongest is the main body (`Roles.IsMain`, the commander's main body now).
3. **Scouts:** the strong with speed to spare, always (the scouting force, the fast wing); then for the task's
   `AllocationScouts` positions the units at least the margin faster than the line that aren't boats, best against his
   scouts first (his line, if he has none), and more while the scouting force is no match for his (doctrine's bar).
4. **The rest:** attackers to the flotillas; a match for his attackers and able to keep station, to the screen when the
   task wants one; else with the line.

Thought over at the AI's thinks only, every `AllocationPeriodMs` (60 s, the commander's own period) and whenever the
squadrons change (a hash of their parents and members), never when the map asks: a replay allocates alike. Hashed.
**Its cost:** about 50 ms a rethink at the Jutland (every squadron's figures against the enemy's mix, four gunfights
each); every 10 s that made a Jutland seed 8.5 minutes, every 60 s 5 (5.9 before AL1, another battle). An exact cache of
the figures (direct-mapped, keyed by each side's classes, counts and condition) spares the rethinks where nothing
changed, before contact; in battle a ship's every change of state counts. Left for the workers (W15) if it shows. The tests' roles all hold as they
were written (battleships Heavy, light cruisers scouting, destroyers screening, a flotilla its light cruisers lead sent
in whole); the zero-garbage tests too, after one fix to a test: the flotilla attack's now runs to its first gas (a
burst's in game time), whose first steps in a process allocate 32 bytes (the runtime's first run of the solver's loop:
a second run in the same process allocates nothing), so its allocation check is on the attack's replay, every path having
run once.

The bench now (`docs/battle/allocation/al3.txt`):

- **Battle fleets:** Britain's battleships the line, the battlecruisers and light cruisers scouting, the destroyers a
  flotilla; Germany's High Seas Fleet the same inside its force (I and II Scouting Groups scouting, the flotilla screening).
- **Destroyers alone:** the slower flotilla the line, the faster scouting (the strong with speed to spare); Germany's two
  of a speed both the line.
- **The prizes' war:** each force's cruisers its line, its destroyers scouting ahead (not attackers against cruisers,
  and the task wants scouts).
- **The Yalu:** Japan's Flying Squadron is so much the stronger a ship (3.5 a tonne against the Main Squadron's 1.2)
  that the Main Squadron isn't strong beside it: both Japan's line. China's ironclads and cruisers its line, the armoured
  cruisers a flotilla (boats beside the ironclads' sizes).
- **The Jutland:** Britain's Grand Fleet the main body; the Battlecruiser Fleet, the 5th Battle Squadron, Hood's
  squadron, the armoured cruisers' screen, Yarmouth and the Battlecruiser Fleet's light cruisers scouting; the
  destroyers and the other light cruisers flotillas; Hampshire screening. Germany's High Seas Fleet the main body,
  Hipper's I and the II Scouting Group scouting, the torpedo boats flotillas.
- **The battle squadron alone** keeps all four in its line: its lookouts on the bows are AL4's.

**What it moved** (`docs/battle/allocation/al3/`: the panel at 8 seeds, the early wave at 16, beside balance.md's
"The capital line made relative"): 9 of 14 sides within about twice the record (7); the record's loser lost more in 24 of
44 (25). Heligoland comes into its band (Britain 0.1, Germany 2.8 of 16, record 0 and 4; 4.8 and 10.5), the Jutland's
Britain stays in it (14.0), Germany's 49.0 is where it was (about 4x, BL3's volume of fire); the Dogger Bank's Germany
5.6 (2.0; record 1). The early wave: the Bosporus's Russia lands 10 of 16 (15: its boats attack as before, the
Ottomans' ironclads now a line), Åland 8 of 16 each way (Finland's aims 5 of 16), Pungdo's China 8 of 16, Port Arthur's
Russia 5 of 16; the Yalu and Conrad's war as they were. The prizes' war still doesn't fight (0.0 / 0.3): each force's
cruisers its line, its destroyers scouting ahead, both commanders declining an even fight (bugs.md). The panel 627 s (701).

### AL4 Lookouts on the bows (built 2026-11-17)

The force admirals' pass came with AL3; what was left is the job taken from the main body when nothing better is left
("a battleship on each wing of a squadron with no cruisers"). A lookout is a station in her squadron's formation, not a
unit (decision 1): `GroupSystem.Group.WingM`, given by the squadron's admiral (`GroupSystem.SetWings`), puts the last
ships of a squadron in line ahead out on its guide's bows, 45° off, the last to starboard and the one before her to
port, up to two and always leaving two in the column. Every reader of a formation reads them (`StationOf`,
`LineOffsets`: the captains' stations, the rails, the map); the coast's clearance leaves them out (each slides off the
land on her own). Hashed only when out, so a world without them hashes as before.

**Who puts them out** (`Roles.Lookouts`, `AdmiralLayer.WingsStep`): the squadron leading its side's main body (the main
body, or the guide of its line down the forces) when nothing of its side was allocated to scout; in line ahead with
ships to spare, its side's commander's (not the player's, nor on the conn), before contact (no enemy held, lost, or going
home) and by day. Out at `LookoutVisibilityShare` (0.3, [EST]) of the visibility: 12 km in clear air (8.5 km ahead and
abeam of the guide, each seeing as far again), 4.8 km in 16 km. In at contact and by night; given again when the
visibility moves their distance by more than a quarter. They get out on the speed they have over the squadron's, so
from the column's tail to 9 km at 6 kn spare takes about an hour.

The eye measure (AL1) isn't needed for it: a squadron's ships are alike, and units' eyes come into it only when lookouts
are taken from other units, which the scouts already are. On passage (no scouts wanted) the cruisers screen and the
squadron's own ships are its lookouts.

The bench (`docs/battle/allocation/al4/bench.txt`, a lookouts line under each squadron with them): the battle squadron
alone, both sides, two battleships 4.6 km off at ±45° half an hour in; Germany's destroyers alone (two of a speed, no
scouts) put two boats out; the battle fleets, the prizes' war, the Yalu (in contact before dawn) and the Jutland have
scouts or no time for it.

**What it moved** (`docs/battle/allocation/al4/`: the panel at 8 seeds, the early wave at 16, beside AL7a's): where
nothing scouts. The panel 7 of 14 within about twice the record (7), the record's loser lost more in 27 of 44 (26); only
Coronel and Åland move, within their seeds (Åland's Finland 6.9 of 13 lost at 8 seeds, 5.9 at 16, against 4.8 and 4.9).
The Bosporus's lines both put lookouts out (Russia's three-ship squadron one, the Ottoman ironclads two): Russia lands
7 of 16 (10), the Ottomans losing 7.6 to her 0.1; Pungdo's both sides: Japan loses none (0.2), China 2.4 (2.2); the
Yalu's Japan once the enemy's lost (the search), 0.3 lost to China's 1.8. Port Arthur, Manila, Conrad's war and the
prizes' war as they were, to the hash. fc-history as it was. The panel 745 s (AL7a's run on a busier machine).

### AL5 At contact (built 2026-11-17)

**The enemy's main body by strength.** Each group of contacts a side sees (`TacticalPicture`) is measured as his units are
(`ForceMeasures.Enemy`): its strength against the side's fleet, its strength a ship, and whether those are ships of his line
(`AllocationLineShare` of his strongest unit's strength a ship or more, the rule his own allocation draws his line by,
his order of battle being known). His main body is the strongest group of his line's ships: never a group of destroyers
however many, and with only his battlecruisers in sight, they are what he holds of it.

**The line by guns.** A force deploys into its line of battle against his main body when two ships or more are in its
line and they fight him with their guns (`ForceMeasures.Guns`: less than half its tonnage would rather use its
torpedoes, `Matchup.Attackers`), not when two of them are capital ships: a cruiser force deploys against cruisers, a
flotilla never against a battle line.

**The fast wing's choice** (`FastWing`, decision 4). A fast wing is a unit allocated to scout because it's strong with
speed to spare (`Roles.IsFastWing`). Once the enemy's held and its commander accepts battle (Contact or Battle), it isn't
sent by gravity with the rest (`Assign`); its commander (the side's for his top units, a force's admiral for his
members, `DisposeStep`) chooses where it goes, the strongest wing first:

- **The line:** if the rest (the line and the screen, and wings sent to the line before it) is outmatched by his main
  body without it (doctrine's bar), it goes for his main body's van.
- **Else the most for the least:** each group he holds that the wing alone isn't hopeless against (doctrine's bar) is
  worth what the wing puts out of it (the matchup's share of it lost) times what the group is worth: its strength
  against the side's fleet, or for a group whose torpedoes are its weapon against the line, what its attack would take
  of the line, if that's more (his flotillas broken up before they reach the line). His main body taken is its van,
  flanked; another group its biggest ship. Kept unless another is worth `CommanderRetargetShare` times as much.
- Nothing repays it: it keeps scouting off its main body's bow, clear of the fight.

The wing closes the ship chosen (an intercept, its scouting over) and fights her once she's in sight within
`FightCompareM` (a Fight on her: the course its fight keeps is on her; its guns still go where they do the most). Not
built from the list under "At contact": the screen's worth as the opening it makes for our attack, beside our own attack,
across his way home (an aim's Stop), his cripples once he breaks off (only as a group that's worth the most for the least).

**The bench** (`shipgen allocation`, `allocation/al5/bench.txt`) lists each side's groups of contacts with their strength
and the ships counted his line, and under each fast wing its task, its order and on whom. The battle fleets: Britain's
1st BCS flanks the I Scouting Group's van (Lützow), Hipper's ships the only line held; Germany's I Scouting Group, inside
the High Seas Fleet's force, finds nothing it isn't hopeless against (the British battleships) and keeps scouting.
Destroyers alone: Britain's wing goes to the line, on V45, its own flotilla outmatched without it. The Yalu has no fast
wing: both Japan's squadrons are her line (AL3), and a fast main body splitting off from a slower consort to fight as a
wing isn't built. `CommanderTests.A_fast_wing_goes_for_the_enemys_battlecruisers`: four Lions beside four dreadnoughts against four
Seydlitzes ahead of four dreadnoughts; the Lions intercept the Seydlitzes' van and fight her.

**What it moved** (`docs/battle/allocation/al5/`: the panel at 8 seeds, the early wave at 16, beside AL4's): 7 of 14
sides within about twice the record (7), the record's loser lost more in 28 of 44 (27). Tsushima's Russia 7.9 of 13 lost
against the record's 7 (5.8), her loser losing more in 7 of 8 (4); the Jutland's Britain 14.3 against 14 (20.5), Germany
39.5 (38.8); Heligoland's Germany 5.0 against 4 (2.8), its heavy guns now at 4.7 km, in the record's 4-8 (3.8, CLOSE);
Dogger Bank's Germany 6.0 (7.6), still high against Blücher alone. Åland's Sweden loses 4.0 of 8 (3.3), Finland 5.6 of 13
(5.9); the Yalu fights a little more (Japan 0.7, China 1.9; 0.3 and 1.8), Manila's United States 1.4 (1.0), Conrad's war
Italy 2.3 (3.3); Pungdo, Port Arthur, the Bosporus (Russia lands 8 of 16) and the prizes' war as they were, within their
seeds. fc-history as it was. The panel 1306 s, on a machine running the test suite beside it for part of the time.

### AL6 Regrouping (built 2026-11-17)

**A worn squadron** is one down to `RegroupShare` (a half) of the most ships it has had (`Regrouping`: two of four, three
of six, one of two; a squadron of one is a lone ship from the start, never worn). Ships leave a squadron sunk, given up,
fallen out (home, by their captain's call) or staying behind as a rearguard; nothing else the AI does leaves a ship
alone, so a squadron's remnant is its straggler, and "stragglers of a squadron gone" need no rule of their own.

**Its admiral merges it** into the nearest squadron of his on the same job (`Roles.Of`) in sight of it (within the
visibility at its guide) whose ships are alike: speeds within `AllocationSpareKn`, strength a ship within
`AllocationLineShare` of each other, the rules the allocation draws its line by, so a remnant of battlecruisers never
goes into a light cruiser squadron of a speed, nor a slow one slows a fast one. Its ships go to the end of that one's line
in their order (`GroupSystem.Add`), and it's gone; the allocation thinks the units over as they change. The fewest first,
each squadron once a pass. The side's commander does it for his top units (at his think, not those reaching for a zone
nor those in on an attack), each force's admiral for his members (at his disposition, not the player's, those on
someone else's task, nor those in on the attack he sent them on), and only while the side's commander has the unit: a
player's own units the player organises. Each merge is told on `scenario --play`'s timeline ("regroups into"), and the
spread over seeds counts them.

Not built: a worn top unit into a squadron inside a force (each admiral regroups his own), nor two worn squadrons of
unlike ships into one; a worn squadron with no like one in sight stays as it is.

### AL7a A ship not made out is one not yet accounted for (built 2026-11-17)

The prizes' war's commanders weren't declining an even fight: they misread it. Italy's held France's three cruisers made
out to their type (Capital, the relative line) and her three destroyers, in sight, only as ships; a ship not yet made
out to her type was taken for the average of the enemy's fleet, half of it cruisers here, so the destroyers counted
half a cruiser each: four and a half cruisers against his three, 0.39 against the bar's 0.4. Against France as she was
he read 0.86-0.92. With good intelligence (decision 2) a commander knows what's at sea and which of it he's made out, so a
ship he can't make out is one of the others (`Matchups.OfType`, `Matchups.Unmade`, a navy's own, once a moment and
picture): a contact made out to her type is one of that type less the ships made out to their class; one made out no
further than a ship, one of the side's ships less those made out to their class and, a share of each class by number,
those made out to their type. Over-counted (more taken for a type than are at sea), the side's whole census, as before.
Contacts' tonnage (`TonnageOf`, the held groups') reads the same.

**What it moved** (`docs/battle/allocation/al7a/`): the prizes' war fights, 143 and 100 hits (41 and 20 before), France
0.8 and Italy 0.4 lost of 16 seeds (0.0 and 0.3): a light-cruiser action at 6-10 km, 105 mm guns at 3%, the destroyers
not attacking cruisers by day; decisive only in a few seeds. Commanders reading the enemy truer fight more across the
panel: 7 of 14 sides within about twice the record (9); the record's loser lost more in 26 of 44 (24). Out of band now are
sides the record has losing nothing: Coronel's Germany 1.1, the Dogger Bank's Britain 1.3; nearer the record: Cape
Sarych's Russia 1.6 (3.1), the Jutland's Germany 38.8 (49.0, Britain 20.5 still in it), the Dogger Bank's Germany 7.6
(5.6) further. The early wave as it was within a seed or two (Åland 4.9 of 13 v 3.2 of 8, the Bosporus's Russia 10 of
16, the Yalu's Japan 0.2 lost to China's 1.9).

### AL7b A ship made out to her type is estimated, not typed (built 2026-11-17)

**The estimate.** Once a navy makes a ship out to her type its lookouts judge her size and whether she's armed
(`Contact.SizeT`, `SizeSigma`, `Armed`; `Sightings.Size`). Size is her bulk: what she displaces, as the eye takes it from
her length, freeboard and upperworks. It's off by a log-normal error, `SightSizeSigma` (0.35 [EST]) at the type's margin,
shrinking to none at the class's; the draw is hers for each navy (a stream of its own, so no other draw moves), and the
error is the least yet, so the estimate only comes nearer as he makes her out better. Made out to her type, he has her
kind right: the types' bands are five times apart. Asked of the research companion (REQUESTS.md 2026-11-17).

*First built on what she shows side on (her silhouette's area), the eye's own measure; AL7c moved it to her bulk.* Side
area hardly tells the kinds of the 1890s apart (Pelayo 1,009 m², Carlos V 703, the crunched Audaz destroyers 533), so a
battleship made out to her type was a third a destroyer: Manila's, Port Arthur's and Pungdo's hunters couldn't tell the
ships they were after from their escorts, and the side reaching made it twice as often (found by an on/off ladder of
AL7c's changes, every one back to the type's rule, `allocation/al7c/`'s notes). Displacement parts them twentyfold.

**What reads it.** The matchup takes her for her side's classes not yet made out (AL7a's pool, as armed as she is), each
weighed by its ships and by how near its size is to the estimate (a normal in the log of the displacement, of her error, never
under `SightSizeFloor`, 0.1 [EST]: a ship made out well is still any of the classes about her size); `Matchups.Weigh`
replaces the census by type, so her tonnage (`TonnageOf`), her matchup entries and what's left unaccounted for read it.
A torpedo's aim at her takes the length and draught of the ships placed near her size (`ShipTypes.At`), and before
she's made out to her type the warships' mean (`ShipTypes.Mean`). The type stays on the contact as the map's and the
reports' word; nothing in the matchup reads it now.

**What it moved** (`allocation/al7b/`, against `al6/`; its early wave run again from its own commit, the first try having
read AL7c's settings): 9 of 14 sides within 2x (7); the record's loser lost more in 26 of 44 (28). Coronel's Germany
0.8 (1.1) and the Dogger Bank's Britain 0.9 (1.9) come into band; Heligoland's Germany 3.4 (5.0), Tsushima 7.6 v 5.4 (7.4
v 5.6) much as they were; the Jutland bloodier for Britain, 23.0 v 42.5 (12.3 v 40.3; 11-40 over its four seeds), German
torpedo hits 13.3 a seed (8.0). The early wave within a seed or two, but for Åland's aims: Sweden meets hers in 13 of 16
(8), Finland 2 (8), the losses much as they were (5.4 of 13 v 2.6 of 8; 5.6 v 3.8): BL6's balance there leaning Swedish.

### AL7c Every decision by the matchup, not the type (built 2026-11-17)

Nothing in the AI reads a contact's type now; what did reads her matchup:

- **Her size as a rank** (`Knowledge.SizeRank`): her tonnage as the matchup takes her (AL7b's weighing; before she's
  made out to her type, the mean of the ships not yet accounted for), as a rank from 0 (a small craft) to 4 (a capital
  ship), smoothly: the old types' ranks, each at the middle of its type's band of the yardstick (`ShipTypes`) and
  between them by the log of her tonnage, a ship made out unarmed 5. A secondary battery takes the smallest first
  (`FireSizeShare` a step), a chief battery fighting to win the largest (`FireHeavyShare`, the user's BL5g call), as
  before but with no step at a type's edge; an unknown's smaller size is still a guess against the one held.
- **Torpedoes for the biggest** (a captain's own and a flotilla's admiral's target): her run or range counts longer by
  `TorpedoSizeShare` (1 [EST]) for each step of her size under a capital ship's, so of two alike the nearer, where the
  ranks came strictly first; an unarmed ship by her size too.
- **A torpedo threat** (the line turning away from a contact, a ship closing up her open guns against torpedo craft
  in reach): one that's an attacker against her by the matchup (`MatchupJudge.AttackerShare`: of the classes she may
  be, most), where a destroyer's type or smaller was.
- **The enemy's heavy ships** a boat keeps clear of (the screen's `SafeRange`, the attack's way round them): those his
  boats would attack rather than fight, where capital ships and cruisers were.
- **The aims' wanted** (`AimBook.Wanted`, 0-1): a Sink wants a ship made out unarmed; a Stop the share of what she may be
  that's of the Reach's ships still on their way, where their types were; one taken for a class her side hasn't at sea
  she can't be (good intelligence, the scenarios' stance: in the full game a mistaken class may mislead, a withdrawal
  included, the user 2026-11-17), so she's weighed by her size. A captain's target counts nearer by the share,
  his course round an escort pushes by the rest, and the commander's wanted ship is one mostly of them.
- **The enemy's line** through a target: the ships chained to her of a size with her (tonnage within
  `AllocationLineShare`), where it was her type; and a battery's shell is judged again when her size estimate moves.

The held groups' count of capital ships is gone (read by nothing but a fallback). The type is the map's, the rail's and
the reports' word (`ScenarioPlay`, `LossBench`, `MapScene`). Found on the way: the allocation's sets (`Roles`: the
main bodies, lookouts and fast wing) were made empty, so the first lookout put out mid-fight was 104 bytes of garbage
(`TorpedoTests`' flotilla attack, once the boats' targets moved); they're sized up front now, and the matchup's pools of
ships not yet accounted for are made with their sources, empty or not.

**What it moved** (`allocation/al7c/`, against `al6/`, with the estimate on displacement; `ladder.txt` the on/off ladder
that found side area wanting): 8 of 14 sides within 2x (7); the record's loser lost more in 28 of 44 (28). The Jutland
19.8 v 27.8 (12.3 v 40.3): Germany's losses come down toward the record's 11, Britain's within it; Tsushima 7.1 v 6.9
(7.4 v 5.6); Coronel both in band (2.0 v 0.9); Heligoland's Germany 2.4 (5.0), Britain none; the Dogger Bank's Britain
2.6 (1.9); Cape Sarych's Russia 1.5 (2.5). The early wave as AL6's within a seed or two (Pungdo's reach 4 of 16, Port
Arthur's 6, Manila's 5; the Bosporus's Russia 13; the prizes' war fights, 0.4 v 0.8 of 16), but for Åland: Sweden meets
her aims in 14 of 16 (8), Finland losing 6.2 of 13 to her 1.9 of 8 (5.6 v 3.8), the lean AL7b began; for AL8.

### AL8 The panel and the early wave, again (2026-11-19)

On today's code (AL7c, then the shell consumption's SC1-SC4: `shell-consumption.md`), the early wave at 16 seeds and the
Jutland through the loss bench (`allocation/al8/`). The user: in scenarios only inspired by history (Åland and the rest of
the hypotheticals) the ship counts are ours to tweak.

**The panel** is `balance/sc3.json`'s: 7 of 14 sides within about twice the record, the record's loser losing more in 26
of 44 seeds.

**The reach scenarios** (the reaching side's aims met, of 16):

| Scenario | AL7c | Now | |
|---|---|---|---|
| Åland (Sweden) | 14 | **8** | Finland 5.6 of 13 lost, Sweden 3.1 of 8: back in balance with no change (the lean AL7b began is gone with the slower fire) |
| Pungdo (China) | 4 | 8 | scores 0.50 / 0.50 |
| Port Arthur (Russia) | 6 | 8 | |
| Manila (Spain / the US) | 5 | 7 / 6 | |
| The Bosporus (Russia) | 13 | **13** | Russia 0.6 of 7 lost, the Ottomans 7.7 of 16 |

**The Bosporus doesn't answer to ship counts.** Two more Ottoman ironclads (Orhaniye, Mahmudiye): Russia lands 14 of 16;
a fourth division of three boats: 13; one ironclad and three boats: 11, Russia still losing 0.0-0.1 ships
(`al8/bosporus-a`, `-b`, `-c`). As BL6 found for the Russian side, the landing hardly reads the forces. The timeline
(`al8/bosporus.txt`'s seed 1896, one of the three the Ottomans win) shows why: at first sight, 07:14 in daylight, the
commander sends all twelve boats at the three battleships; within the hour nine fall out (five for **their 47 mm gun**,
the torpedo boat's tubes untouched: `Fitness` counts the chief gun battery only) and two divisions are beaten off, a
torpedo or two fired; the ironclads then stop the landing alone. The matchup's forecast for that attack
(`shipgen matchup ottoman_tb:12 ekaterina:3`): 36% of the boats lost and 1.15 torpedo hits, 3.5 t taken for each tonne
lost; in play 75% lost and nothing hit before they're beaten off. `MatchupRunInHitShare` 0.4 was set when "the fine
model's flotilla loses about half its boats over a whole run"; the fine model has moved since. That's P11
(`balance.md`: measure the run-in's exchange as the day attack now is, then decide), and the boats' fall-out on their gun.

**The prizes' war** fights and is even (each side 0.6 of 6 lost; seed 1920, `al8/prizes-1920.txt`: both commanders choose
battle, the cruisers fight from 08:30 to 10:48, 6,094 shells for 137 hits and nothing sunk, then Italy withdraws
outmatched and France chases a faster enemy to the clock). Hours of light-cruiser gunnery with nobody sunk is the
record's too (the Otranto Straits, 1917); a chase that can't close kept up for four hours is BL5's (fights that run to the
clock).

**The Jutland** (`al8/jutland-losses.txt`, 4 seeds): Britain 9.0 lost, Germany 48.3 (record 14 and 11). Germany's capital
ships are in the record's range (2.3 a seed of 27); the excess is her light forces, cruisers 12.0 of 17 and destroyers
34.0 of 55, lost from the sixth hour to the eleventh (19:00-01:00), most of them first struck on an attack (Gain, Fire,
Run, Away) and slowed (OutSpeed), then finished off as cripples (21.8 a seed). Not new: the loss bench had Germany at
40-45 since BL3 (`bl3/baseline`, `bl6/baseline`); AL7c's 27.8 was the low one. In the seed played (`al8/jutland-1916.txt`)
one German flotilla attack shows in the timeline (III, beaten off) and the II Flotilla's big boats are broken in two by British
battleships at 22:06-22:33 as the High Seas Fleet retires. Their run-ins and their fall-out on their guns are P11's too;
the gunnery's accepted 3.5x over the record (error-sources.md) is the rest.

Nothing changed in the scenarios: AL8 hands the Bosporus and the Jutland's light forces to P11.

**Falling out on a torpedo attack** (the user, 2026-11-19: "What the ship is doing"). On a torpedo attack with torpedoes
left (her own, pressing it, or her flotilla's: her torpedoes held for its signal, or its attack under way) her torpedoes
are her weapon, so doctrine's guns share doesn't take her out (`FallOutRule.Of`'s `torpedoes`; the captain judges again
when that changes: `Captain.FitTorpedoes`). Her tubes wrecked or her torpedoes gone, she's judged by her guns again: a boat
with guns but no torpedoes keeps her station in the run, the diversion, and one with neither falls out
(`FallOutTests.On_a_torpedo_attack_she_falls_out_by_her_tubes_not_her_guns`). The user: a boat without torpedoes isn't torpedo
craft, and whether she goes in as a diversion is her squadron admiral's call; a squadron with no torpedoes left never starts
a rush (it doesn't: `FlotillaAttacks.Free` and the admiral's attack read `AnyTorpedoes`, working tubes with torpedoes). The
admiral's call on a boat without torpedoes (does she draw more fire from the torpedo boats than she costs) is the run-in's
exchange P11 measures, so it's built there.
What it moved (`al8/tubes/`): the Bosporus's boats no longer fall out for their gun (none of 16 seeds), 12.1 torpedoes a game
(10.0), Russia lands 11 of 16 (13); Åland 9, Pungdo 7, Port Arthur 9 (8, 8, 8); the Jutland at 8 seeds against the same
code before it (`jutland8-before` / `-after`): Britain 13.0 → 16.1, Germany 42.4 → 41.1, capital ships 2.9 / 6.5 → 4.0 / 6.4,
within its spread (25-111 lost a seed).

## Questions for the user

1. ~~The intelligence before contact~~: good intelligence, the enemy's order of battle (decision 2).
2. ~~The fast wing's choices~~: the line, the flank, his screen and the five more under "At contact", no reserve
   (approved 2026-11-16).
