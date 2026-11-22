# Torpedo use: torpedoes for the ships worth them

Agreed with the user 2026-11-22. Steps TU0-TU5.

**The complaint (the user):** "destroyer flotillas tend to spam their torps quite readily against individual small targets
such as destroyers. What I'd prefer: they should try to use their guns and be careful especially when escorting a force
of heavier ships. Flotilla torpedo attack should be a specific command which the player or a layer above can order
against an enemy formation, and those should happen mostly against formations of heavier ships."

**How we decide (the user):** "We need to make decisions based on data. Anecdotes give a reason to gather the data to
base the decision on." So TU0 measures first, by cause, across the shipped scenarios; each later step is decided on it
and measured again with the same tally.

## The paths that fire torpedoes today (read 2026-11-22)

1. **Torpedoes at will** (`CaptainLayer.Torpedoes`, `CaptainLayer.Fire.cs`): with doctrine's `Torpedoes` at will (the
   default, `Content/Sim/doctrine.json`), a captain fires at the ship he's engaging, else at any foe in reach whose
   `TorpedoChance` is `TorpedoMinChance` (0.1) or more, the bigger first (`TorpedoSized`). Size ranks targets but never
   turns one down. The chance counts the target's line within `TorpedoLineM` (3 km) as presented length, so a column of
   destroyers presents as much as a battle line. By day the reach is `TorpedoDayShare` of the slow setting's range.
2. **A captain's own attack** (`FightThink`): in Fight, with torpedoes left, he presses an attack if ordered to, in his
   flotilla's attack, or when his unit's matchup has his torpedoes as the better weapon (`MatchupJudge.Attacks`). The
   spread itself still goes through path 1.
3. **The flotilla's attack together** (`AdmiralLayer.AttackStep`, docs/battle/command.md 5.1b): on an Attack order (the
   player's Attack…, A; a task; or `FlotillaAttacks.SendIn`, the side's commander or a force's admiral), or taken up by the
   admiral himself under doctrine's `TorpedoAttack` together when the matchup has the attack worth making. The boats hold
   their torpedoes for his signal.

## What we want

- Torpedoes are scarce: a WW1 boat has two to four tubes and few reloads. One spent on a destroyer is one fewer for the
  battle line. Against light craft the guns do the work.
- **At will, only where torpedoes are the better weapon.** The matchup already says it (`MatchupJudge.AttackerShare`,
  used by `RoundHeavies`): whether her torpedoes do more than her guns against the class the target may be. Relative,
  never a tonnage cut-off or a type switch.
- **Point blank, by the chance** (the user, 2026-11-22: yes): a night mêlée or a sudden meeting at a few hundred metres
  leaves no time to dodge (Dover Strait 1917). At-will fire at a target torpedoes aren't the better weapon against needs
  a high chance, which only a short run reaches. No ship-type rule.
- **Attacks only when sent.** No self-started attack, neither the admiral's by doctrine nor the captain's by his matchup:
  the player or a layer above (`FlotillaAttacks`, the task layer) sends a flotilla in. An uncommanded flotilla is
  judged by `FlotillaAttacks`' `CommanderPhase.None`.
- **Escorts keep their torpedoes for the force.** A unit screening holds them for its admiral's order; its
  counter-attack on enemy boats is a gunfight.
- **The player may overrule** (docs/battle/command.md): his Attack and his "Fire at…" go on any target.

## Steps

- **TU0 The measure.** `shipgen scenario X --play --torpedo-use` (`ScenarioPlay.TorpedoUse.cs`): every spread by why it
  was fired (`CaptainLayer.LaunchOf`: at will, engaged, own attack, ordered attack, the flotilla's signal and whether its
  attack was ordered: `AdmiralLayer.AttackOrderedOf`), the shooter's task, day or night, what she is and what she fired
  at; the chance her captain saw, the range, whether the matchup has her torpedoes as the better weapon against the
  target ("better"), whether such a target was in sight within `FightCompareM` when she fired at another ("near"); the
  hits on the target and on others. `TU_CSV=FILE` appends a line a spread for the panel's totals. Built 2026-11-22;
  baseline below.
- **TU1 At will by the torpedo's worth** (revised by TU0's findings 3 and 4, awaiting the user): a target taken when what
  a torpedo is likely to take from her (the matchup's forecast times her size) stands against what it's worth kept for
  the heavy enemy the side holds, rather than by the better-weapon test; point blank by that same forecast, not by
  `TorpedoChance`. Measured.
- **TU2 Attacks only when sent**: the two self-starts removed; every AI-held flotilla confirmed to reach
  `FlotillaAttacks`. Measured, with the flotilla and night scenarios watched for attacks that no longer happen.
- **TU3 Escorts hold for orders.** Measured.
- **TU4 The player's side:** the Attack cursor says when the target is one the boats' torpedoes aren't the better weapon
  against.
- **TU5 The panel again** (balance.md's), each scenario's losses beside the baseline; a row in plan.md's status.

## TU0 baseline (2026-11-22)

`scenario X --play --seeds 6 --torpedo-use` (the Jutland 3) over every shipped scenario but the ladders and tests: 22
scenarios, 1,964 spreads, 2,873 torpedoes, 158 hits on the target aimed at and 52 on others. The tonne bands go by the
target's full displacement (WW1 destroyers are about twice the record's, docs/battle/bugs.md "destroyer crunch"). The
types are relative to each scenario (`ShipTypes`: Dover Strait's leaders and the Southampton's light cruisers count as
"Capital"), so the cuts are by tonnes:

| path | target | torpedoes | of all | hit % | t struck a torpedo | a better target near |
|---|---|---|---|---|---|---|
| at will | under 1,500 t | 285 | 10% | 6.0% | 63 | 5% |
| at will | 1,500-6,000 t | 1,520 | 53% | 3.6% | 98 | 9% |
| at will | 6,000-15,000 t | 245 | 9% | 6.1% | 543 | 36% |
| at will | 15,000 t and over | 335 | 12% | 11.9% | 4,041 | 9% |
| attack | 6,000-15,000 t | 170 | 6% | 8.8% | 1,045 | 8% |
| attack | 15,000 t and over | 307 | 11% | 4.9% | 1,081 | 28% |

What it says:

1. **The spam is at will.** 83% of the torpedoes went at will. Only 1.4% went in attacks the boats started themselves
   (the captain's own attack, the admiral's by doctrine): TU2 alone would change almost nothing.
2. **Most torpedoes go at light ships, for little.** 63% went at will at ships under 6,000 t, for under 100 t struck a
   torpedo. At will at ships of 15,000 t and over they took about 4,000 t a torpedo, some 40 times as much.
   Heligoland Bight (745 torpedoes, 29 hits) and Åland (262 torpedoes, 1 hit at a median 5.2 km) are nearly all of this kind.
3. **"Better weapon" is the wrong gate for at will.** The matchup's test (torpedoes do more than her guns) is right for
   whether a unit *attacks*. For a spread fired beside the guns it shuts out a heavy ship's own tubes at a heavy ship.
   "Capital at Capital" was 422 torpedoes with 44 hits, the most productive use in the game, and it reads 3% "better"
   because her guns are better still. The question for at will is the torpedo's opportunity cost: what this target is
   worth to a torpedo against what the torpedo is worth kept for the heavy enemy.
4. **The captain's chance doesn't measure hits.** At will, by the `TorpedoChance` the captain saw (day): under 0.2, 1.4%
   hits a torpedo; 0.2-0.3, 4.0%; 0.3-0.4, 1.4%; 0.4-0.5, 5.9%; 0.5 and over (median 0.67), 7.7%. The range says far
   more: under 1 km 30% (night 20%), 1-2 km 16% (8.5%), 2-4 km 6% (3.4%), 4-6 km 1%, 6 km and over none. So a
   point-blank floor on `TorpedoChance` would not pick out point blank. The matchup's forecast (`Matchup.TorpedoHit`,
   fitted in balance.md P11 against `ScenarioPlay.TorpedoTally`) is a second model of the same thing; the captain should
   use that one (one model behind each).
5. **Escorts.** Screening units fired 358 torpedoes. Of their spreads at targets torpedoes weren't the better weapon
   against, 51% were fired with a target worth them in sight.
6. **Ordered attacks at night** were sometimes aimed at a contact made out only as a shape (the Jutland's night, 92
   torpedoes): "better" reads under 0.5 for her estimate, not for what she is.
