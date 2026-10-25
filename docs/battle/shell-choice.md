# Plan: the shell by target (a 4.5 deepening)
Status: **agreed with the user 2026-10-25** ("Decided" below)    Updated: 2026-10-25

`shells-armour-damage.md` decision 3 left choosing the shell by target to the AI's pass: that plan built the shells,
the loadouts and their outcomes, and every battery has fired by the magazine order since (S2, decision 7). This plan is
that pass: a battery fires the shell its target calls for, the gunnery officer orders the change, the guns take a
salvo to come round to it, and the player may order a kind over him.

## The aim

1. **The right shell at the target:** AP at armour, HE (or common) at what AP passes through. A battleship's 13.5"
   AP puts a light cruiser out 3.5% a hit (command.md 5.1g), and big AP through a destroyer is "two shell-sized holes"
   (`damage-research/08` §1: 94% pass through a Fletcher, against ~30% crippled a hit for HE). The Japanese at Samar
   switched to HE for exactly this reason.
2. **The choice made on what the navy knows,** never the truth: the class the contact is taken for, else what its side
   has at sea of the type made out, else of any type (`Matchups`' census, as every AI judgement already works).
3. **A change that costs something:** the round in the gun is fired before the new kind comes up, so a battery doesn't
   flick between kinds, and choosing well beats choosing often.
4. **One model:** the choice reads the matchup's judgement of what a hit does (`Matchup.OutExponent`), the one the
   AI's Fight, turn-away and seek already read. Nothing beside it re-derives penetration.

## What there is today (checked 2026-10-25)

- **The loads:** `Loadout` holds each battery's loads in the magazine order (`MagazineOrder.Rank`: a main battery
  AP, common, HE; a secondary HE, common, AP), each mount's rounds by load (`CombatStore.LoadRounds`). The default
  loadouts are 70% AP and 30% HE for main batteries, so most heavy ships already carry the HE they need.
- **Firing:** `GunnerySystem` (~1110) fires each mount's `CombatStore.NextLoad`, the first load it has rounds of. The
  kind is picked at the instant of firing, not when the gun was loaded. Each mount lays its own shell with its own
  range table (~1211), so a salvo mixing kinds already falls right. The battery's computer works in the lead shell's
  table.
- **A kind on order:** only the dev salvo (`ShellSystem.FireAt(..., ShellKind? kind)`, `-fire=ship:battery:he:...`,
  the F1 salvo row). Nothing in the gunnery path takes one.
- **The captains** (`CaptainLayer.Fire.cs`) pick targets and engage, cease and fire torpedoes. They never touch the
  shell.
- **The matchup** (`Matchup.cs`) judges each battery with its lead shell (`Loadout.Fires(b)`, ~98), so the AI also
  thinks its battleship fires AP at destroyers, which is what she does.
- **The strip** has no shell order (`battle-hud.md` U2's note: "No shell-kind order yet").
- **Doctrine** (`Doctrine.cs`, `Content/Sim/doctrine.json`) has no field for it.

## The steps

**A0 The bench first: does the matchup rank the kinds as the damage model does?** Before the AI reads the matchup's
judgement to choose, check it on the real damage model, kind by kind (physics, measured: no guard rules).
- `shipgen matchup --kinds SHOOTER TARGET...`: for each battery and each kind it carries, by range, the matchup's out
  chance a hit against each target, beside `shipgen hits --kind K` on the same pair (the damage model's own out of
  the fight after k hits). A table a pair: capital on capital, on cruiser, on destroyer; cruiser on cruiser and on
  destroyer; destroyer on destroyer; a pre-dreadnought pair with common.
- Where the two rank the kinds differently, the fix goes into the matchup (it also mends Fight's judgements).
  Expected suspect: HE on a capital's upperworks. The matchup counts it only by the splinters through the deck to the
  vitals, while the damage model also wrecks directors, fire control and crews and starts fires (the Tsushima case,
  where the Japanese chose HE on purpose). Whether that's "out of the fight" is decision 4's to settle by the bench.
- Anchors: Samar (AP through destroyers and CVEs, the switch to HE), Tsushima (Shimose on Russian battleships), the
  Jutland outfits and the British rule of thumb (APC close, CPC long, HE at light craft). A research request asks for
  the record by navy and period (`research/REQUESTS.md`, 2026-10-25). It doesn't block.

  - **Done 2026-10-25.** `shipgen matchup --kinds A[,C...] B[,D...] [--ranges ...] [--hits K] [--ships 200]
    [--settle 30]`: each of A's batteries, each kind it carries (a loadout of that kind alone, `ShipClass.Load`), on
    each of B's classes at 4, 8, 12 and 16 km: out of the fight after K hits beam on (K by B's size, the square root of
    her thousands of tonnes) as the matchup judges it, beside the damage model's (`GivenHits`, 200 ships, 30 min
    after); each one's best kind, a line marked where they differ beyond two standard errors.
  - **First run** (Iron Duke, Lion, Arethusa, Town Chatham, V25, Borodino, Mikasa on Derfflinger, Von der Tann,
    Mikasa, Borodino, Arethusa, Town Chatham, V25): 224 lines, 11 differ. Every one was a 13.5" HE on a light cruiser:
    the matchup AP (Arethusa 12% to 6% in 2 hits), the damage model HE (7.5% to 26%). The matchup kept every
    nose-fuzed shell out of any armour face, where the terminal bursts it just inside a plate under 0.2 calibre (the
    3" belt, to a 13.5" shell).
  - **The fix:** `Terminal.NosePunchMm`, the terminal's rule in one place, and the matchup's face reads it (one model).
    After it 170 lines, 1 differs (Derfflinger's 12" on Arethusa at 16 km, AP 10.0% against HE 10.3% in the matchup:
    a tie that the switch's margin covers). `matchup arethusa:4 iron_duke` 1.14 → 1.127 (Iron Duke's 6" HE now holes
    thin plate). Sim tests 780 of 780.
  - **Capital ships (decision 4):** both models pick AP on every capital pair. 6 hits of 13.5" on a Derfflinger at
    12 km, AP against HE: out of the fight 2% / 1%, fires a hit 11% / 39%, crew out 29 / 85, holes 0.96 / 3.35 m² a
    hit, but her guns (3.6 / 4.0 of 4 working), director and speed are untouched by HE. So by "out of the fight" AP
    is right against armour, and HE's extra harm (men, fires) doesn't reach her fighting power in the damage model as
    it stands. If Tsushima's case is wanted (HE wrecking a battleship's fighting power), it belongs in the damage
    model's upperworks (directors, voice pipes, the conning position), not in the choice.

**A1 The guns loaded.** A mount's guns hold the load they were loaded with (`MountState.Loaded`, the load's index;
hashed), chosen when the gun loads, not when it fires:
- after firing, a mount loads the battery's **load ordered** (A2) if it has rounds of it, else the magazine order's
  next, as now;
- what's in the gun is fired first, so a change takes effect a salvo later (decision 1);
- the dev salvo keeps its meaning (a kind asked, spent from that kind).
- **Parity:** with no order given, every hash stays as it is (`ReplayTests`, the squadrons scenario's hash). This is
  the refactor's proof.

**A2 The order in the sim.** `GunnerySystem.Load(shooter, battery, kind?)`: the battery's load ordered (null: the
magazine order), a command, logged and replayed like `Engage`.
- The snapshot carries each battery's kind ordered, each mount's kind in the guns, and rounds left by kind.
- The combat log notes a change ("Lion's main battery: load HE").

**A3 The captain's choice.** For each engaged battery that carries more than one kind, in the fire pass the captain
already runs each second:
- **The judgement:** for each kind the battery has rounds of, the expected effect of a round fired at the target as
  the picture takes it, hit chance × out chance, from the matchup's tables. The tables are by range against each
  class (made once a pair, now one a load; a few hundred bytes each), the target spread over its census when its
  class isn't made out, its seen condition counted as the matchup counts it.
- **The change:** only when a kind beats the one ordered by `SimSettings.ShellSwitchGain` (a first guess 1.25). Each
  change costs a salvo of the old kind, so a target at the edge between two kinds doesn't flick them. It is judged
  again when the target changes, its contact is made out further, the range moves a step, or a kind runs out.
- **Doctrine:** a field `Shells`: "by target" (the default) or "magazine order" (the old way, kept as a choice: a navy
  that fights by its outfit, or a difficulty slider later). Per side and per unit, as every field.
- **Allocation:** nothing in the pass (the tables are made when the classes come into the world, as `Matchups.Warm`
  does now).

**A4 The matchup reads the choice.** `MatchupGun` judges each target with the kind its captain would choose there:
the best of its loads by range, held in the same pair table. The AI's judgements then match what's fired, and the
light cruisers that weren't hopeless against a battleship become so. The confidence a commander has in a fight moves
with it, so A6 re-measures.

**A5 The player's order.** The player may overrule the captain (as with every order):
- **The strip:** the GUNS column gets **Shells ▾**: *by target* (the captain's, shown with what he's chosen) and each
  kind the selection's main guns carry, for the current selection (decision 3: the chief batteries only, for now).
- **A kind ordered holds against doctrine** until the battery is out of it. Then it goes back to the captain's choice,
  and the ticker says so ("Lion: out of HE").
- **The card:** each battery's kind in the guns and its rounds by kind.
- **battle-hud.md's note goes.**

**A6 Measured.** Before and after on each, the user's eye on the scenarios after:
- `matchup arethusa:4 iron_duke`, `town_chatham` against `v25:3`;
- the squadrons and flotilla scenarios;
- Tsushima and the Jutland played (`--play --seeds 5`): losses by type and who sank whom. The Jutland's destroyers
  should die more to heavy ships' fire. The record: 8 British destroyers and 5 German torpedo boats lost, most to
  secondaries and light cruisers;
- the fire pass's cost a ship and the matchup's table memory (`Matchups.Warm`).

## Decided (the user, 2026-10-25)

1. **A change costs the round in the gun** (one salvo); the ready-use racks hold the battery's mix.
2. **Doctrine's default is "by target" in every period;** a period's differences come from its loadouts.
3. **The player's order goes to the current selection, main guns only for now:** every selected ship's chief batteries
   (`BatteryRow.Chief`) that carry the kind; the secondaries stay the captain's.
4. **"Best" is what A0 shows:** if the matchup misses what HE does to a capital ship, the fix is the matchup's (Fight
   gains it too), not a measure of its own for shells.
5. **No keeping AP back:** sparing it falls out of the choice by target.

## Not here

- Fuze settings by target (delay against light ships): the shells' `FUZE` cell stays the design's.
- Shells by the fire discipline's phase (ranging with one kind, effect with another).
- Changing the loadout at sea or between scenarios (the admiral's loadout, S5, stays the scenario's).
- AI admirals ordering a kind to a squadron (the captains choose; the player may order one).
