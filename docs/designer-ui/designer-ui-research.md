# Ship designer UI: research and proposal

*Companion to `fire-control-designer.md`, `crew-space-model.md`, `powerplant-model.md` and `hull-weight-model.md`. Written 2026-10-09 against shipgen as frozen today (Python reference; the live code is `Fleetwright.Shipgen`).*

*This note answers three questions:*
1. *How much does a player really have to decide, once the tech side hands over templates?*
2. *What do Rule the Waves and the other good designers do, and what do players hate about them?*
3. *What should our designer look like, so it is complex but intuitive, with no drudgery?*

**Mockups:** the Design artifact "Ship Designer UI" (https://claude.ai/artifact/1XuQP4E2xvEVrgUWXiHeY3). It has four boards:
- the main screen, with a clickable arrangement strip, a toy size model and the Legend
- the protection elevation editor
- speed and machinery, with the real speed curve and goal-seek
- the compare view of sketches A, B and C, using the real reports of dreadnought, nassau and invincible
- roles and matchups (§14)
- the new-design dialog with the bare hull (§14).

**Tags:** [S] sourced (see Sources), [M] measured on shipgen here, [INFERRED] my proposal or estimate.

---

## TL;DR

- **Our designer runs backwards from Rule the Waves (RTW), and the UI has to embrace that.**
  - In RTW you set displacement and fill it. In shipgen you say what the ship carries, and the hull comes out.
  - So the player's problem is never "what fits in 25,000 t?". It is "**why did it come out at 29,000 t, and what do I give up to get back under the treaty?**"
  - Three tools answer that, and they are the heart of the proposal: **size drivers** (what set the length and beam, and how much slack the other rules leave), **marginal-cost chips** (+1 kn = +2,140 t, +8.5 m), and **goal-seek** (hold ≤ 28,000 t and let the engine solve for speed).
- **The data is smaller than it looks.** A design is 130–200 JSON leaves [M], but:
  - About 50 of them are template blocks: plant tech, hull construction, crew standard and armour materials. Those are **4 picks**.
  - 16 are fire control, which comes down to **3 director picks plus counts**.
  - Most of the rest have good engine defaults already.
  - That leaves about **20 essential controls** on the first layer and about 60 behind "Details". This is two levels of disclosure, never three (NN/g [S]).
- **The main battery editor is topological, not positional.** The player edits an **arrangement strip**: aft group, middle (machinery blocks, wing pairs, Q turrets), fore group. The engine places everything.
  - The player adds turrets, steps them up (superfire), makes wing pairs abreast or echelon, and drags boiler groups to open gaps for Q turrets.
  - This keeps every degree of freedom shipgen has, with none of the dragging and pitch/roll balancing that players hate in Ultimate Admiral: Dreadnoughts (UA:D) [S].
- **One screen, three zones.**
  - Left: the controls, as an accordion of sections.
  - Centre: the live ship. The top-down sprite, a side elevation and overlays, with a ghost of the previous state.
  - Right: the **Legend**, a period-style particulars sheet with a weight bar, ratings with plain verdicts, and warnings with fixes.
  - Bottom: sketch variants (A, B, C, …) and history.
- **Speed is the main technical risk.** One `build()` took **0.4–2.2 s** in the Python reference on your machine's VM [M]. The README quotes 5–170 ms, so measure the C# port.
  - Either way the UI must recompute asynchronously, show "settling", and keep the last good result on screen.
  - Sensitivity probes and goal-seek run in the background at low priority.
- **Overview trim sheet (revision, §15.7, current default).** The designer opens on 0 · Overview: a medium armour profile plus a trim sheet of the main weight levers (speed, range, belt, deck, turrets, rounds, secondaries). Each lever shows what one step saves, and the sheet shows the margin to the request and the role scores. The sections 1–8 are for detail work.
- **Widgets follow the focus (revision, §15.6, current).** This is v1's layout: rail left, ship pinned on top. Picking a section animates its widget into the space under the ship: the side profile grows into the armour editor, the matchup chart flies in from the Legend, and the strip and curve grow out of a dock row. Everything returns to where it came from.
- **Overview ↔ focus (revision, §15.5, alternative).** The overview shows the large ship and 8 section cards. Opening a section zooms it into the centre as a full-size workspace with its controls on the drawings, and the ship becomes a live preview strip whose overlay follows the section. The Legend never moves.
- **One screen (revision, §15).** There are no screens to switch, only a focus. Clicking a section, pressing 1–8, clicking a part of the ship, or clicking a Legend line or a remark changes only the editor band and the highlight. Compare is a toggle that widens the Legend; New design is the empty state; goal-seek is the only popover.
- **Roles (revision, §14).**
  - The class is an output; the player's role is an order. Enemy AI uses only the assessed class.
  - A **matchup view** plots every known enemy class against your design. The axes are relative effective speed and exchange ratio, and the quadrants are Prey, Too fast, Escape and Danger.
  - New designs start from a **bare hull** (shipgen: 30.5 m, 186 t, 8 men) with **goals** taken from an admiralty request, not from a type template.
  - Gameplay limits are amber consequences, never red refusals.
- **Engine asks (§12, plus §14.4):**
  - report what set the length and beam
  - structured warnings with fix hints
  - gun-bearing statistics
  - a fixed-hull build mode for refits and for an RTW-style "budget" mode
  - template references in the design instead of inlined numbers

---

## 1. What the player is really designing

### 1.1 The data audit [M]

Leaf counts in the design JSONs (71 designs in `designs/`):

| Design | Leaves | Template blocks | Fire control | Armour | Armament | Other |
|---|---|---|---|---|---|---|
| destroyer | 131 | 50 | 16 | 19 | 11 | 35 |
| dreadnought | 150 | 50 | 16 | 28 | 17 | 39 |
| bismarck | 150 | 50 | 16 | 26 | 22 | 36 |
| connecticut | 157 | 50 | 16 | 25 | 30 | 36 |
| babel (ten calibres) | 203 | 51 | 16 | 22 | 78 | 36 |

The template blocks are `machinery.tech` (about 19 fields, down to the uptake gas temperature), `crew.standard` (18), `hull.construction` (4) and `armour.materials` (9–11 strings). For the player, each block is **one choice from a catalogue the tech system fills**. The README already says director templates "belong to the game's designer UI".

The real decisions, grouped as a player thinks about them:

| Group | Essential (first layer) | Details (second layer) | Template pick |
|---|---|---|---|
| Role | type label, look (navy, era) | name, style (warship / carrier / merchant / planing) | – |
| Main battery | per battery: gun, barrels, fore / aft / wing / mid counts, superfire | echelon, cross-deck, stands on, turret armour, rounds per gun | gun (calibre, length, mark) |
| Secondary, AA, torpedoes | per battery: gun, count, mount (deck / casemate) | tier, stands on, armour, rounds; AA heavy/light; torpedo mounts and tubes | gun |
| Protection | scheme, belt, deck, turret, bulkhead | taper, band depth and height, upper belt, end belts (reach, tip, bulkhead), deck list, steering box, TDS depth, CT | materials |
| Speed and machinery | speed, range | stress, shafts, transmission, arrangement, bunkers, rudders, centreline bulkhead | plant |
| Hull and superstructure | form (fine ↔ full), freeboard, forecastle preset | raised stretches, deck planking, shell plating, tower levels, deckhouse levels, aft control, levels over bridge, control plating | construction |
| Fire control | director per battery, count | rangefinder base, director armour, radar, computer weight, search radar | director, radar, computer |
| Crew | habitability | endurance days, distiller, water ration, berth ratio, officer fraction | crew standard |

The essential layer comes to about 20 controls, plus about 6 per extra battery.

### 1.2 The hull is an output

- `shipdesign.size` searches for the shortest hull, on a half-metre grid, that:
  - fits the layout at comfortable clearances, and
  - is as slender as the speed requires.
- It then finds the narrowest beam that:
  - fits across,
  - holds GM ≥ 0.06 B,
  - keeps draught ≤ 0.36 B, and
  - keeps L ≤ 10.5 B.
- "The player never enters tonnage or positions." Gameplay limits (treaty, dock, budget) are the designer UI's job.

That reverses the RTW experience. RTW players fight a **fixed budget** (displacement, topside points, dock) [S]. Ours fight **growth**, and growth is lumpy [M]:

| Design | +1 kn changes | Why (inferred from the size rules) |
|---|---|---|
| destroyer | +169 t, +3.0 m | slenderness binds |
| dreadnought | +2,140 t std, +8.5 m, GM +0.16 | slenderness binds; the longer hull is stiffer |
| bismarck | +2,136 t, +7.0 m | slenderness binds |
| yamato | +1,498 t, +7.0 m | |
| gangut | +912 t, **+0.0 m**, GM **−0.47** | layout binds the length (four flush turrets), so the extra machinery goes into a hull of the same length, and the beam search lands somewhere else |

The whole speed curve for `dreadnought.json`, with everything else held [M]:

| kn | 17 | 18 | 19 | 20 | **21** | 22 | 23 | 24 | 25 |
|---|---|---|---|---|---|---|---|---|---|
| std t | 19,229 | 20,235 | 21,756 | 23,326 | **24,970** | 27,110 | 31,127 | 35,101 | 42,033 |
| L m | 154.0 | 159.5 | 169.0 | 177.0 | **183.5** | 192.0 | 211.5 | 225.0 | 251.5 |
| machinery m | 38.0 | 44.4 | 54.8 | 63.0 | **72.1** | 82.0 | 96.1 | 108.3 | 130.1 |

Each knot under 21 kn saves 1,000–1,650 t, while each one over 22 kn costs 4,000–7,000 t. That is the "knee" RTW players talk about [S], and our engine produces it for free. Draw it.

**Goal-seek, run for real** [M]: holding standard ≤ 24,000 t and varying speed, bisection took 6 builds: 20.50 → 24,040 t, 20.25 → 23,606, 20.38 → 23,819, 20.44 → 23,914, **20.47 → 24,000 t (179.5 m)**. Six builds is about 8 s in the Python reference on your machine. If the C# port is 10–50× faster, it will feel instant.

A player who sees only "29,531 t" cannot reason about any of this. The UI's first job is to show **which rule set the size**, because that tells the player what is free to add and what is expensive.

### 1.3 Cost of a recompute [M]

`shipdesign.build()` in the Python reference, timed in the Linux VM on your machine:

| Design | Cold | With length hint |
|---|---|---|
| destroyer | 0.70 s | 0.42 s |
| dreadnought | 1.74 s | 1.22 s |
| bismarck | 2.15 s | 1.58 s |
| yamato | 2.19 s | 1.56 s |

The README says 5–170 ms, which was probably measured on another machine or an earlier build. The C# port's number is the one that counts. The UI below works at either speed, but it has to be asynchronous (§11).

---

## 2. What others do

### 2.1 Rule the Waves (RTW1–3) [S]

**How it works**
- **Type first.** BB, BC, B, CA, CL, DD, KE and the rest. Type sets the legal envelope. A design outside it is reclassified (a BC that's too slow becomes a BB), or rejected ("a 30,000-ton destroyer with 15-inch guns").
- **Displacement is the hull.**
  - Speed, armour and guns fill it. Dock size is a hard cap.
  - Topside points (a budget derived from displacement) limit weapons. Casemates don't use them.
  - Being slightly over weight is allowed, at the cost of stability and flotation.
- **Armour** is a column of fields: belt, extended belt, upper belt, deck, extended deck, turret, turret top, secondary, conning tower. A scheme dropdown sits above it: protected cruiser, belt and sloping deck, flat deck, all-or-nothing.
- **Main turrets are rows.** You "add" a turret, then pick a one-letter position (A, B, X, Y, wing and so on) and the number of guns.
  - **The letter decides the arc. Where the drawing sits is cosmetic.** A "Fire arcs" checkbox overlays them.
  - Positions, wing turrets, superfiring and guns per turret are gated by research.
- **Secondaries are just a count,** assumed half on each side. An "auto place" button lays out the drawings. RTW3 adds visual placement of the secondaries.
- **The graphics tab** is a polygon editor for the superstructure: six layers, mirrored points, saved superstructures.
- **Design check** marks each item Error (illegal) or Note (hint).
- **Auto-design** produces a draft to edit.
- **"Developed from"** charges a fraction of the design cost when the change stays within limits: displacement up to +10 % or +1,000 t, main guns unchanged, ±1 kn, ±1 in belt, ±0.5 in deck.
- **Refits** are a separate "Rebuild?" path with a whitelist of what may change.

**What players love**
- "The high point of the game … conveys real design trade-offs without drowning the player in detail."
- It is "more conceptual than simulationist", like real preliminary design.
- It is a constraint puzzle: budget, dock, treaty, and tech that arrives mid-build.
- It is transparent: change the ammunition and see the weight and speed move.

**What players hate**
- Tiny, fixed UI with no tooltips on most variables and no text scaling.
- "Illegal" results with no explanation, and type thresholds nobody documented.
- Odd weights, for example a 5 in belt that weighs more on a 14k t cruiser than on a 15k t one.
- Bookkeeping: design lists can't be filtered.
- Useful buttons hidden in odd tabs.
- No comparison view and no templates beyond auto-design.

### 2.2 Ultimate Admiral: Dreadnoughts [S]

- You place 3D parts on fixed hulls.
  - Pitch and roll offsets come from where you put things, and you balance them by moving funnels and secondaries.
  - Built-in towers block bigger turrets.
- Naval Gazing calls it "shallower and more cluttered", because it "confuses complexity with depth".
- Workflow complaints:
  - Lag on refit and copy.
  - No editing a design before the first ship is built.
  - A "perfectly balanced" ship can still handle badly, and you only find out in battle.
- **Lesson:** never make the player hand-tune a balance variable that carries no trade-off. Shipgen already balances the centre of gravity over the centre of buoyancy by shifting the arrangement (`layout_shift_m`). Show that result; never make it a chore.

### 2.3 Elsewhere [S unless marked]

| Game or tool | Pattern worth taking | Pattern to avoid |
|---|---|---|
| **Aurora 4X** class design | One dense, copyable plain-text summary. Requirement in, component out: deployment time sizes crew quarters, add fuel until range reaches the target. A prototype checkbox for unresearched parts. | Its density without visuals. |
| **HoI4** ship designer | Hull then slots. Role requirements checked on save. "Upgrade modules" and "generate design" buttons. Treaty limits per type. | Slots too abstract for our physics. |
| **Stellaris** | Section templates (bow, core and stern mixes), and auto-best fill with current tech [unverified]. | – |
| **Automation** (car tycoon) | Layered family → variant → trim. Live dyno curves. Per-component **quality sliders** with cost and engineering time. | One bar mixing R&D quality and slider quality confused players: show the parts separately. |
| **KSP** | Stock delta-v per stage, with a *situation* selector (sea level or vacuum). The Engineer's Report: always visible, non-blocking checks. Players installed mods to get a derived number they needed. | – |
| **SpringSharp** | A text report with weight groups, ratings normalised so 1.00 is average, and short verdicts ("good, steady gun platform", "wet forward") [wording unverified]. Shared on forums as is. | Typing dimensions in by hand. |
| **Cosmoteer** | Illegal parts go **red but you keep building**. Overlays for arcs and similar. | – |
| **From the Depths, Stormworks** | – | Block-level freedom: "straightforward but tedious". |
| **Children of a Dead Earth** | Deep parametric component design that players find "addictive". | Learned by trial and error, "not user-friendly". Depth needs explanation. |
| **CK3 / Victoria 3** | **Nested tooltips:** terms inside a tooltip open their own, and the tooltip locks so you can move into it. "An encyclopedia that follows your cursor". | – |
| **Factorio** (FFF #318) | A tooltip looks the same everywhere. Ratios you need are shown in game, not on the wiki. Alt-mode overlay. Blueprints. | – |
| **Fusion 360** timeline | A history of features with a rollback marker, and **suppress a change without deleting it** to see its effect alone. | – |

### 2.4 UX principles that apply [S]

1. **Progressive disclosure, two levels at most** (NN/g). A wizard fails when the steps depend on each other, and ship design steps all do. So use one screen, not a wizard.
2. **Direct control over the independent variables, with immediate feedback** (Bret Victor). Draw the whole curve, not only the current point. Speed's slider shows displacement against speed with the current point on it, and hovering a point on the curve previews it.
3. **Show the marginal cost.** That is how real constructors argued: H3c saved 1,250 t by dropping the turrets a deck; turbines saved about 1,100 t on Dreadnought.
4. **Non-blocking warnings with the cause and the fix.** This combines KSP's Engineer's Report, Cosmoteer's red tint and HoI4's role check.
5. **Decompose every composite number on hover.** This comes from Automation's complaint, and it is how CK3's tooltips work.

### 2.5 How the admiralties did it [S]

- **Lettered sketch designs, compared in a table.**
  - Dreadnought: A, C, D, E, F, G, H, with H chosen and then D3–D9 refined from it.
  - G3: lettered *backwards* from K for the battlecruisers, each variant judged against docks, Suez and Panama.
  - North Carolina: more than 35 schemes under the 35,000 t treaty, numbered I to XVI-D. XVI-B/C/D "traded guns for speed or armor".
- **Fixed staff requirements first.** The Dreadnought committee started from 12 in guns and at least 21 kn, and recorded decisions with reasons: no superfiring because of blast on the sighting hoods.
- **The Legend.** The approved particulars and the weight statement by group, with a board margin.
- **The design spiral.** Each change goes round proportions, power, arrangement, stability and cost again. Our engine runs the whole spiral in one build. That is the game's superpower, and the UI should make it feel like one.

These map directly onto UI: **Requirements** (fixed, at the top), **Sketch designs A, B, C** (variants), the **Legend** (output sheet) and **decision notes** on variants.

---

## 3. Design principles for our designer

1. **One screen, no wizard.** Everything is reachable from the main screen. Sections are an accordion, not modal tabs, so the ship and the Legend never leave view. (RTW hides useful buttons in tabs [S].)
2. **Requirements in, ship out, and say why.** Every output that moves shows its delta and its *cause*.
3. **Two layers.** The essentials are visible. "Details ▾" opens the rest *in place*. Every detail field starts on **Auto** (the engine's default) and shows the value Auto chose, so opening Details teaches without demanding anything.
4. **Arrangements, not coordinates.** The player edits counts, order and relations: what stands where relative to what. The engine places. No pitch/roll chores.
5. **Never block, always explain.** Invalid designs still build and draw. Errors, warnings and notes each name the cause, link to the control, and offer a fix with its cost.
6. **Templates from tech, choices from the player.** A tech item is a card with a name, picked once. Its numbers are visible on hover, never typed.
7. **Every number is a door.** Hover shows how it was made (nested tooltips). Click jumps to the control that drives it.
8. **Variants are cheap.** Branching a sketch is one click. Comparing is one view. History can be scrubbed.
9. **Period flavour in the output, modern ergonomics in the input.** The Legend reads like a 1910 particulars sheet. Inputs take `12in`, `305`, `13.5"` and arrow keys.
10. **The game decides gameplay limits, and the designer shows them.** Treaty, dock and budget are amber badges and lines on bars, with the price of exceeding them. They are not silently clamped sliders.

---

## 4. Screen anatomy

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Battleship 1911 · Sketch C   [A][B][C●][+]     REQUIREMENTS  ≤28,000 t ✗  Dock 210×30×9.5 ✓  Kiel ✓  │
│                                                              £2.31M / £2.40M ✓   Builds BB ✓   ⟲ ⟳ ⚖  │
├────────────────────────┬─────────────────────────────────────────────────────────┬───────────────────┤
│ ▾ ROLE  BB · Portsmouth│                                                         │ LEGEND            │
│ ▾ MAIN BATTERY         │   ◄ stern             [live top-down sprite]     bow ►  │ 183.5 × 27.2 m    │
│   [arrangement strip]  │        ghost outline of previous state                  │ T 9.6 m  D 14.4 m │
│   Battery 1 card       │                                                         │ 24,970 t std ▲2140│
│   + battery            │   [side elevation: armour, decks, waterline]            │ 29,531 t full     │
│ ▸ SECONDARY · AA · TT  │                                                         │ ▓▓▓▓▓▓▒▒▒░░ wt bar│
│ ▸ PROTECTION           │                                                         │ Size set by       │
│ ▸ SPEED & MACHINERY    │  Overlays: [Arcs] Armour Spaces Magazines Smoke Crew    │  L: speed (12 m   │
│ ▸ HULL & UPPERWORKS    │            Weights Horizon Blast                        │    slack)         │
│ ▸ FIRE CONTROL         │  View: Top · Side · Section · Guns-bearing              │  B: stability     │
│ ▸ CREW & ENDURANCE     │                                                         │ Ratings + verdicts│
│                        │  [settling… ◔]                                          │ Warnings (2)  →   │
├────────────────────────┴─────────────────────────────────────────────────────────┴───────────────────┤
│ HISTORY  ○─○─○─○─●  "+1 kn" "12→13.5 in" "+Q turret" "AoN scheme"     │ COMPARE A·B·C │ GOAL-SEEK ⌖ │
└──────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Left, 340–400 px.** An accordion of sections. Each closed section shows a one-line summary ("2×2 12 in fore, 1 pair wing, 2×2 aft"), so a closed accordion reads as a design summary. Opening one closes none of the others: players compare across sections.
- **Centre.** The ship at the same scale as in game, with the bow to the right (as in `sheet.png`).
  - Hovering a component highlights its row on the left and its weight in the Legend.
  - Clicking a component opens its section.
- **Right, 320 px.** The Legend: the outputs only.
- **Bottom bar.** History, variants, compare and goal-seek. All the tools for "the design as a whole".
- **Stale state.** While a build runs, the outputs that will change dim slightly and a small spinner shows. The previous numbers stay readable. Deltas show once the build lands.

---

## 5. Sections in detail

### 5.1 Role and requirements

*Revised after `ship-roles-design.md` (2026-10-09). There is no type envelope any more. A class is an output, a role is an order, and nothing is ever refused. See §14 for the full model.*

- **Type label** (BB, BC, CA, …) is a free name the player gives the class, as in shipgen.
- **Assigned role** is a chip row: Scout, Commerce raider, Trade protection, Fleet screen, Torpedo attack, Line of battle, Station, Coastal defence.
  - It is an **order**: it decides how the player's fleet AI uses the ship (doctrine, formation slot, which missions it accepts).
  - It never changes the ship.
- **Classified as** is the game's assessment: the top role score against the current world, shown in the top bar next to "Your role".
  - When the two disagree badly (assigned role scores under about 0.45), an **amber note** says so: "She will behave as Line of battle. Assessment: Line of battle 0.00, best fit Scout cruiser 0.81. Enemy intelligence will report her as Scout cruiser and match her accordingly."
  - It is a note, not a warning to fix. The player may know something the assessment doesn't.
- **Admiralty requests**: the campaign's role tests against named enemy classes, as cards with a live checklist ("Catch and defeat *Guichen*: 0.24") and the reward. They replace the "staff requirement" badges.
- **Theatre selector** (North Sea / Atlantic / Mediterranean). Sea states change effective speed (`v_eff`), so the matchups and role scores move with it. Small hulls lose the most.
- **Look**: navy and era chips that update the sprite live.
- **Requirements bar** (top), from the campaign. **All amber, never red**: they are consequences with a price.

  | Requirement | Source | Display |
  |---|---|---|
  | Treaty displacement | diplomacy | a line on the weight bar. Over it: "Comply / Exceed openly / Declare 35,000 t" |
  | Dock length, beam, draught | the yards | ✓, or "No yard can build this: expand Portsmouth No. 3 (£400k, 2 yrs)?" |
  | Canals (Kiel, Suez, Panama) | campaign | badges |
  | Budget, build time, politics | finance | badges with deltas ("3× the last ship: Parliament balks") |
  | Admiralty request goals | role tests | the request card's checklist |

  Each amber badge has a **"Fit…"** button that opens goal-seek with that constraint prefilled (§7).

**Mockups:** "Roles — catches · escapes from · beats" (`Matchups`) and "New design — start from a bare hull" (`NewDesign`).

### 5.2 Main battery: the arrangement strip

The heart of the designer. Shipgen's main battery model is already topological:
- per battery: `fore`, `aft`, `mid`, `wing`, `superfire` (bool or per group), `echelon`, `cross_deck`, `amidships_stands_on`
- list order across batteries (outermost first)
- `machinery.arrangement` (boiler and engine groups, forward to aft, which make the gaps for Q turrets and decide the funnels).

The UI draws that model directly:

```
 STERN ◄                                                                                   ► BOW
 ┌ AFT GROUP ─────────┐ ┌ MIDDLE ───────────────────────────────────────────┐ ┌ FORE GROUP ──────┐
 │ [+] [Y]  [X ⤒]     │ │ [ENG][ENG] │ [BOIL ▮] (Q) [BOIL ▮▮] │ W1 ⇅ abreast  │ │ [B ⤒]  [A]  [+]  │
 └────────────────────┘ └───────────────────────────────────────────────────┘ └──────────────────┘
   Battery colour: ■ 12in/45 twin  ■ 9.2in/47 single
   Bearing ahead 6 · astern 4 · broadside 8 (port) / 8 (stbd) · broadside weight 3,140 kg
```
*(Numbers in sketches are illustrative unless the text says they come from a shipped design.)*

**Interactions**

| Action | What it does |
|---|---|
| **[+] at a group's outer end** | Adds a turret of the selected battery, which becomes the new outermost (A, B, C run on in list order, as in the engine). |
| **Click the step ⤒ on a turret** | Toggles superfire: stepped or flush. The engine rule is that the outermost turret stands on the deck and the first flush turret ends the stepping. The strip enforces that visually: stepping a turret behind a flush one asks "also step the ones before it?". |
| **Drag a turret between groups** | Fore ↔ aft ↔ mid. Dropping it on a gap between boiler blocks makes it a Q turret. |
| **Machinery blocks** | `[BOIL]` and `[ENG]` blocks are draggable to reorder, so `["boiler","boiler","engine"]` opens a gap. The funnels follow the boilers on the sprite as you drag. This is the friendliest way yet to expose `arrangement`, which is currently a power-user list. |
| **Wing pair chip** | Toggles abreast / echelon, cross-deck (echelon only; disabled with a reason on abreast), and stands on deck / deckhouse. |
| **Battery colour** | Each turret shows its battery's colour. Reordering batteries in the cards below reorders the strip (who is outermost, who is inboard among the wing pairs). |
| **Hover a turret** | Shows its arc on the sprite, its letter, magazine, barbette armour and weight (turret + barbette + magazine + crew), and "what if removed: −1,240 t, −9 m". |

**Presets.** A row of thumbnail chips above the strip, filtered by tech: A-B-X-Y, all forward (Nelson), hexagonal (Nassau), lozenge (Bouvet), echelon (Invincible), A-B-Q-X-Y, Gangut. A chip *replaces the strip* and keeps the batteries' guns. This is the Stellaris "section template" idea.

**Tech gating.** Options the navy hasn't researched are **shown locked**, with the tech's name. They are not hidden, so the designer doubles as a research planner. Examples: superfire, wing turrets, triple and quad mounts, cross-deck.

**Battery cards** (below the strip). Each battery is one card:
- **Gun:** catalogue picker "12 in / 45 Mk X". The tech supplies calibre, length and ballistics. The card shows calibre in the player's units and the mark's quality.
- **Barrels:** segmented 1, 2, 3, 4, gated by tech.
- **Turret face armour:** number input with a slider.
- **Rounds per gun:** **Auto** by default (`batteries.rounds_per_gun`'s curve), showing "Auto: 100".

**Gun summary readouts** (a strip under the cards, recomputed from the arcs the engine exports):
- guns bearing ahead, astern and on each beam
- broadside weight
- a small **guns-bearing polar plot**: number of barrels against relative bearing, all batteries stacked. One glance shows the end-on fire versus broadside trade that drove Dreadnought's H, Nassau's hexagon and Invincible's echelon.

### 5.3 Secondary, AA, torpedoes

**Secondary batteries**
- Same card as the main battery: gun, barrels, mount (deck / casemate), count as **per side** (pairs).
- Details: tier (upper / lower, casemate only), stands on, armour and rounds.
- The sprite highlights where the engine put them. Casemates show the 85 %-beam band where they can go.

**AA** (heavy / light counts)
- A stepper for each, plus "fill free roof spots", which adds mounts until the roofs are full, so the player never counts tubs by hand.
- A readout splits them: "on roofs 24 · on deck edge 6".

**Torpedoes**: mounts and tubes per mount.

### 5.4 Protection: the elevation editor

**Today's model.** Shipgen's armour is about 25 numbers: belt (with taper, band depth and height), upper belt (deck, extent), end belts (mm, tip, reach, bulkhead) ×2, bulkheads, the deck list (deck, mm, extent), steering box ×3, TDS depth, plus per-battery turret faces.

**RTW's way.** One column of fields [S].

**Ours: two drawings plus a scheme picker.**
1. **Scheme preset chips** fill all the fields:
   - Protected cruiser
   - Pre-dreadnought (full-length tapered belt, upper belt between the barbettes, protective deck at the ends)
   - Incremental (main + upper + end belts)
   - All-or-nothing
   - All-or-nothing + steering box
   - Layered decks (Iowa)

   These are the README's own scheme list (`armour` section).
2. **Side elevation** (centre panel, "Side" view): the hull profile with the waterline, the deck stack (0, 1, 2, … and raised −1, −2), and every plate drawn at its real extent, **coloured by thickness** (one sequential palette).
   - Click a plate to edit its mm, with a scroll wheel on the plate.
   - Drag the end belt's tip to set `reach`.
   - Drag the belt band's top and bottom edges (or leave them on Auto, the 0.15 T + 1.2 m rule, shown as a dashed line).
   - Drag a deck plate to another deck.
   - Turret, barbette and CT armour show on the turrets.
3. **Midship section** (small inset): the belt band against the waterline, the deck(s) at their heights, the TDS depth as layered bulkheads, and the vital-spaces roof (the lowest deck over the citadel). This is where the "low roof squeezes the machinery" effect becomes visible: the machinery box is drawn under it.

**Readouts**
- Armour weight by part (hover any plate: area × mm → t).
- The **citadel length**, and how much of it covers machinery and magazines.
- *Hook for later:* an **immunity zone** strip against a chosen reference gun (own guns, or "the enemy's 13.5 in Mk V"). This is the metric real designers used, and once the penetration model exists it is the single most informative protection readout.

**Materials:** one "best available" chip per part, opening the catalogue. Most players will never touch it.

### 5.5 Speed and machinery

- **Speed slider** with a **mini curve**: standard displacement against speed, from about −4 kn to +4 kn around the current point.
  - Computed lazily in the background (§11).
  - The current point is marked. A kink shows where slenderness starts to bind.
  - Hovering the curve shows "at 23 kn: 27,800 t, 191 m".
- **Range** slider (nm at cruise), with endurance days shown beside it.
- **Plant card:** picked from the tech catalogue ("Direct-drive turbines, water-tube boilers"). It shows its key figures (kg/kW, sfc, fuel, crew per MW) and is never edited.
  - A **"Newer plant available"** chip appears when research delivers one, with its delta ("−410 t, +0.6 kn at same size").
- **Design rating** slider = `stress`, labelled *Conservative ↔ Forced* and annotated with the effects: weight, fuel and overload margin. The historical presets are tick marks: capital 0–0.2, cruiser 0.5, destroyer 0.8–1.
- **Details:** shafts (Auto), units per shaft (Auto), transmission, bunkers (wing / ends), centreline bulkhead, rudders. The arrangement is edited on the main-battery strip, with a mirror of it here.
- **Outputs:** shp, machinery length, rooms, funnels, smoke reach. The funnel count is Auto (from the gas), and "+ funnel" only adds.

### 5.6 Hull and upperworks

- **Hull form:** slider *Full ↔ Fine* for the block coefficient (0.42–0.68), with the style default as a tick.
- **Freeboard:** *Sheltered ↔ Ocean ↔ High* (0.3–2.0 × standard), annotated with "wetness" from the ratings.
- **Forecastle:** preset chips (Flush, Forecastle to bridge, Long forecastle to funnels, Forecastle + poop, Breastwork, Raised amidships). A small **anchor editor** handles custom stretches: two dropdowns from the README's anchors (bow, fore_group, bridge, funnels, aft_control, aft_group, stern), plus 1 or 2 decks.
- **Construction:** catalogue card (from `hull-templates.md`).
- **Bridge tower levels:** slider with a live **horizon readout** ("bridge eye 14.3 m, horizon 14.6 km; sees over B turret ✓"). It shows the stability cost alongside (GM, gale heel). This is the trade the README describes; show both sides on the slider.
- **Details:** deckhouse levels, aft control on/off ("+ redundancy, + roof room, + topweight"), levels over bridge, control plating, superstructure material/weight, deck planking, shell plating.

### 5.7 Fire control

- **One row per battery** (main, secondary, AA): director template from the catalogue ("Bureau director, 4.6 m rangefinder"), count, and Auto armour.
- **Live sizing readout**, from `fire-control-designer.md` §5: "unit of error at 15 kyd: 62 m; your salvo's spread: 73 m → adequate". There is a design-range selector (cruiser 15–20 kyd, battleship 20–28), and the knee curve for the ship's own guns shows on hover.
- **Search radar** (template) and **director placement** readout: which roof, eye height, horizon, smoke warning. The engine already reports all of these.

### 5.8 Crew and endurance

- **Habitability slider** from *Sleep at station* to *Single cabins*.
  - The crew model rates comfort smoothly. The slider **interpolates the H0–H5 template numbers** between the stops the tech allows, so it is one continuous control instead of 18 fields.
  - It shows the complement by department, quarters in superstructure, and m² per man against the standard.
- **Endurance:** Auto (from range). A longer value shows as "tender: +N days of stores". Distiller toggle and water ration in Details.

### 5.9 Appearance

Navy and era looks, the name, and a class/ship naming scheme. **No polygon drawing.** Shipgen generates the superstructure from rules, which is exactly the RTW graphics-tab drudgery we can delete. The levers that change the look are real design levers: tower levels, deckhouse levels, aft control, forecastle.

---

## 6. The Legend: outputs, ratings, warnings

The right column is a particulars sheet set in a period face (a Caslon-like serif for the headings, tabular figures for the numbers). It is always the same format, the same one the game uses for intelligence reports and class lists, so players learn to read one sheet (the Aurora lesson [S]). A "Copy as text" button exports it, SpringSharp style.

### 6.1 Particulars

| Line | Example (dreadnought.json) |
|---|---|
| Dimensions | 183.5 × 27.2 × 9.64 m (602 × 89 × 31.6 ft) |
| Displacement | 24,970 t standard · 29,531 t full load |
| Machinery | 33,800 shp, 4 shafts, coal; 21 kn; 6,600 nm |
| Armament | 10 × 12 in/45 (5×2) · 10 × 3 in |
| Protection | belt 11 in, upper 8 in, deck 3 in, turrets 11 in |
| Complement | 908 (73 officers) |

Each line is a door: hover for the breakdown, click for the section.

### 6.2 Weight bar

- One stacked horizontal bar by group: armour, hull, fuel, armament, machinery, misc, superstructure, fire control (the engine's `weight_groups_t`).
- A treaty or budget line drawn on it.
- **During a change, a ghost of the previous bar**, with the changed segment labelled "+2,140".
- Hover a segment for its top five items (from `weights`).

### 6.3 Size drivers (new; needs §12.1)

```
Length 183.5 m   set by SPEED (slenderness at 21 kn)
                 layout needs 171.0 m → 12.5 m of free deck amidships
Beam   27.2 m    set by STABILITY (GM ≥ 0.06 B)
                 fit needs 24.1 m · draught rule needs 26.8 m
```

This answers "why is it this big", and it tells the player what is cheap. With 12.5 m of slack, another wing pair or a Q turret costs only its own weight, while +1 kn costs 8.5 m.

### 6.4 Ratings with verdicts

The ratings are normalised so 1.00 is average for the type and era (the SpringSharp idea [S]). Each has a bar and a one-line verdict:

| Rating | From the report | Example verdicts |
|---|---|---|
| Stability | `gm_full_m`, `gm_light_m`, `gale_heel_deg`, `deck_edge_deg` | "Stiff. Heels 5° in a full gale." / "Tender when light: heels 22° light in a gale." |
| Gun platform | `roll_period_s`, GM | "Steady (15.9 s roll)." / "Snappy roll (8 s): spoils gunnery in a seaway." |
| Seakeeping | freeboard ratio, forecastle, length | "Dry forward." / "Wet: low freeboard, no forecastle." |
| Endurance | range, `endurance_days`, distiller | "6,600 nm, stores for 22 days." |
| Habitability | sleep m²/man against the standard, tolerance days | "Crowded: 0.9 m² against 1.1 m² standard." |
| Command | bridge `sees_over_turrets`, horizon, smoke | "Bridge sees over B turret; horizon 14.6 km." |
| Protection | (later: immunity zone) | "Immune to 12 in between 9 and 14 kyd." |

### 6.5 Warnings

There are three severities, shown the RTW way but explained:
- **Error:** the ship cannot be built.
- **Warning:** the ship works but with a cost.
- **Note:** a hint.

Each item:

```
⚠ Aft control stands in the smoke of Funnels 1 and 2: poor visibility from it.
   Cause: smoke reach 41.9 m (coal, forced draught) · aft control within it, abaft Funnel 2
   Fix: move boiler group forward (arrangement) · drop aft control (−Δ t) · accept
   [Show on ship]
```

- **Show on ship** highlights the culprits.
- The fixes are **previewable**: hovering one runs a background build and shows its deltas. Clicking applies it as a history step.
- An "Ignore for this design" option collapses the item into a count.

---

## 7. Hitting a target in a "hull comes out" designer

This is the part that makes or breaks the designer, because it replaces RTW's whole budget loop.

1. **Marginal-cost chips.** Next to each essential control is a small chip with the cost of one step: "+1 kn: +2,140 t · +8.5 m · GM +0.16", "+1 in belt: +310 t".
   - Computed in the background for the **hovered or focused** control only: two builds, one up and one down.
   - Cached against the design's hash.
   - Shown greyed out while stale.
2. **Goal-seek** (the ⌖ button, or "Fit…" on any red requirement badge):

   ```
   HOLD   Standard displacement  ≤  28,000 t        (or: L ≤ 200 m, B ≤ 30 m, cost ≤ £2.4M)
   VARY   Speed                       [between 18 and 24 kn]
   ──────────────────────────────────────────────────────────────
   Result  21.6 kn → 27,960 t   (6 builds)          [Apply]  [Branch as Sketch D]
   ```

   - Bisection over one variable. The response is monotone enough for speed, belt, range, rounds and secondary count. Discrete variables (turret counts) are stepped instead.
   - It offers "Branch as Sketch D", because a goal-seek result is exactly the kind of thing designers compared side by side.
   - **"Trade-off fan"** (an advanced variant): pick two variables, for example speed and belt. The engine runs a coarse grid in the background and draws the iso-displacement line, a small-multiples view after Bret Victor [S]. Expensive, so it is optional and off by default.
3. **Fixed-hull mode** (needs §12.4). A toggle at the top: *Grow to fit* (default) or *Fixed hull*.
   - In Fixed hull, length and beam are locked. Displacement, draught and stability float, and the weight bar fills against a deep-load limit.
   - This is RTW's budget mode for players who think that way, and it is the natural mode for refits and conversions.

---

## 8. Variants, history and lineage

- **Sketch designs.** The tabs in the top bar (A, B, C, …) are cheap branches of the current design. "+" branches from the current sketch.
  - Each sketch has a **decision note** field ("rejected: blast on B's sights"), as the 1905 committee recorded [S].
  - Players who like the history can letter backwards from K, G3-style.
- **Compare view** (bottom bar → Compare):
  - Sketches side by side, **sprites at the same scale** and aligned at the stern.
  - A Legend table with every differing row highlighted, and requirement badges per sketch.
  - "Pick" promotes a sketch to the design.
- **History strip.** Every committed change is a node with an auto label ("+1 kn", "12 → 13.5 in", "AoN scheme").
  - Click to roll back (non-destructive).
  - **Suppress** a node to see the design without that one change (the Fusion idea [S]). That answers "what did that change really cost?", even after five later changes.
- **Lineage.** A design remembers what it was **developed from**. The game can then charge the RTW-style partial design cost when the change stays inside limits, and show those limits as a live badge ("Developed from Lion: 40 % design cost; exceeded by +1,600 t").
- **Refits.** Open a ship's design in Fixed-hull mode. Changes the refit whitelist does not allow are locked with a reason.

---

## 9. Templates and the tech catalogue

The design should store **references plus a snapshot**, not just inlined numbers:

```json
"machinery": {"tech_ref": "ST5-1905", "tech": { ...snapshot... }, "stress": 0.2, ...}
```

The snapshot keeps the design reproducible (and the engine unchanged). The reference lets the UI:
- name it,
- offer "Newer available", and
- run **"Modernise"**: swap every reference to the current best of the same family, and show the deltas first.

**Catalogue families** (each is a card picker with a compare table):

| Family | Feeds | Notes |
|---|---|---|
| Guns (calibre, length, mark) | `calibre_mm`, `calibre_length`, ballistics | the mark's quality grade, as in RTW [S] |
| Mounts (barrels, superfire, wing, echelon, cross-deck) | gating of the strip | locks with tech names |
| Plants | `machinery.tech` | plus fuel variants (coal, oil, mixed) |
| Hull construction | `hull.construction` | |
| Armour materials | `armour.materials` | "best available" default |
| Directors, radars, computers | `fire_control.*` | the bureau templates of `fire-control-designer.md` |
| Crew standards | `crew.standard` | interpolated by the slider |

**Player templates** (no drudgery across a fleet):
- Save an armour scheme, a battery card, a fire-control fit or a whole arrangement strip under a name, and apply it to another design.
- "Copy protection from *Lion*" is the right-click version.

---

## 10. Input ergonomics

- **Units.** Metric or imperial is a global toggle, and inputs accept either: `12in`, `12"`, `305`, `305mm`, `13.5`.
  - Calibres display in the navy's own convention, with the other in a tooltip.
- **Every number field** responds to the scroll wheel (step), shift-scroll (fine), arrow keys, typing, and drag-on-label (scrubbing, as in Blender).
- **Auto pills.** Any field with an engine default shows `Auto · 100`. Typing overrides it. A small ⟲ returns it to Auto.
- **Keyboard.**
  - `1`–`8` open the sections.
  - `Tab` moves through the essential controls only.
  - `Ctrl+Z` / `Ctrl+Y` undo and redo.
  - `Ctrl+B` branches a sketch.
  - `Alt` held shows all overlays (Factorio alt-mode [S]).
- **Scale.** The UI scales from 100 % to 200 %, and there is a colour-blind-safe palette for battery colours and the armour heatmap. RTW players complain about tiny fixed text [S].

---

## 11. Responsiveness architecture

The UI must feel live with builds anywhere from 20 ms to 2 s.

1. **A worker-thread build queue with cancellation.** A new edit cancels the queued build (not the running one) and enqueues the latest design. Slider drags debounce at about 120 ms, and release triggers immediately.
2. **Always show the last good result.** Stale outputs dim; nothing blanks.
3. **Two tiers of estimate while dragging.**
   - Tier 0 (instant): extrapolate from the cached marginal-cost chip for that control.
   - Tier 1 (cheap): `navarch.solve` without the layout.
   - Tier 2 (full): `build()` with `hint` = the previous length (README: about twice as fast).
4. **Cache by design hash.** Undo, redo, compare and suppress all hit the cache.
5. **Background probes at low priority:** marginal-cost chips for the focused control, speed-curve points, fix previews, goal-seek. They yield to the user's build.
6. **Render later.** The sprite in the editor can be the cheap preview (the render side reads only `ship`). Full mips and the height map are made on "Accept design".

If the C# port lands near the README's 5–170 ms, tiers 0 and 1 are just polish. If it is nearer 1 s, they are what keeps the designer usable.

---

## 12. Engine contract additions (for Fleetwright.Shipgen)

1. **Size drivers.** `report.size = {"length": {"m", "driver": "layout" | "speed" | "max", "layout_m", "speed_m"}, "beam": {"m", "driver": "fit" | "gm" | "draught" | "lb", "fit_m", "gm_m", "draught_m", "lb_m"}}`. `size()` and `fit()` already compute every candidate; they only need returning.
2. **Structured warnings.** `{code, severity, text, refs: [component ids], values: {...}, fixes: [{label, patch}]}`, where `patch` is a JSON merge-patch to the design. Today's strings stay as `text`.
3. **Gun-bearing statistics.** `report.guns = {ahead, astern, port, stbd, broadside_weight_kg, bearing_hist: [barrels per 5°]}` from the arcs the engine already exports.
4. **Fixed-hull build.** `build(design, hull={"length_m", "beam_m"})`. `with_hull` already exists internally; it is needed for refits, for the RTW-style mode, and for the requirement check "fits dock X".
5. **Template references.** `*_ref` keys are passed through untouched, so the UI can track lineage and modernise.
6. **Fast estimate.** A documented `estimate(design, hint)` that runs `navarch.solve` with the previous layout's geometry, for tier-1 feedback.
7. **Per-component weight attribution.** `report.weights` already lists about 80 items. Add `component` ids (turret A, battery S1, belt, …), so hover-to-weight works in both directions.

---

## 13. Onboarding and AI help

- **Reference designs.** The 71 designs in `designs/` (Dreadnought, Nassau, Invincible, Bismarck, Yamato, Fletcher, Gangut, Babel) are a ready library: "Start from a reference design", filtered to what the navy's tech allows. Locked parts can be substituted automatically, with a list of what changed.
- **Auto-design.** The AI designer (doctrine → choices; `fire-control-designer.md` §5 already sketches it for fire control) produces a first draft for a type and requirement set. RTW players use auto-design as a starting point [S]. RTW's default ammunition is too low and its layouts are odd [S], so ours should state its doctrine in the decision note.
- **Advisors (flavour).** Warnings can be voiced by period offices: the Director of Naval Construction (stability, weight), the Director of Naval Ordnance (arcs, blast, fire control), the Engineer-in-Chief (machinery, smoke), and the C-in-C (speed, range). They speak in the same precise words, only signed. The North Carolina design history is full of exactly these objections [S].

---

## 14. Roles, matchups and the starting point (revision, 2026-10-09)

This section follows `ship-roles-design.md`: roles are missions scored against the enemy, a class is an output, and nothing is ever refused.

### 14.1 Three different things the UI must keep apart

| Thing | Who sets it | What it does | Where it shows |
|---|---|---|---|
| **Type label** ("Armoured cruiser", "BC") | player, free text | a name only | class lists, Legend title |
| **Assigned role** ("Trade protection") | player, per class (overridable per ship in the fleet screen) | **orders**: own-fleet AI doctrine, formation slot, missions it accepts | "Your role" in the top bar; role chips |
| **Assessed role / classification** ("Scout cruiser 0.81") | the game, continuously | what the ship *is* against the current world. **Enemy AI and intel use only this.** | "Classified as" in the top bar; the Fitness for Service block |

So a player who calls a battleship a cruiser and sends it on cruiser missions gets exactly that behaviour. The enemy sees the assessed class, thinks "nonsense", and matches it with battleship assets.

- **Intel:** an enemy's assessment of *your* ship uses *their* intel of it. A secretly over-treaty ship or a misidentified class is assessed from the false numbers until combat reveals the truth (`ship-roles-design.md` open question 4). The UI can show "Enemy intelligence will report her as …" from their estimated figures.
- **Classes live in the world.** Starting fleets come from the scenario. Every class in service is re-assessed when the world changes: a new enemy class commissions, intel improves, the theatre changes. The fleet screen shows each class's role scores with a trend ("Scout 0.71 → 0.41 since *Gloire* commissioned, 1904"). The designer shows the same thing ahead of time as **Shelf life**: the effect of known enemy construction, weighted by intel confidence.

### 14.2 The matchup view ("catches · escapes from · beats")

This is a centre-panel view, next to Top / Side / Section, and it updates live while any section is edited.

- **The quadrant chart.**
  - x is the enemy's effective speed minus yours, at the theatre's sea state.
  - y is the exchange ratio on a log scale: up means you win.
  - Each known enemy class is a dot, sized by numbers in service and dashed when intel is low-confidence. You are the diamond at the centre.
  - The vertical split is not at 0 but at about −1.75 kn, between the catch (2 kn) and escape (1.5 kn) margins. It is drawn as a soft band, because the tests are sigmoids.
  - The four quadrants are Fisher's logic made visible:
    - **Prey** (slower and weaker): catch and beat.
    - **Too fast** (faster and weaker): you win but can't force it.
    - **Escape** (slower and stronger): run.
    - **Danger** (faster and stronger): you can't run and can't win.
  - Adding a knot slides every dot left. Adding belt or guns moves them down or up. This is what makes the arms race legible.
- **The matchup table** under it: per class, effective speed, then Catch, Escape, Beat and Survive as tinted 0–1 cells, then a verdict word.
  - Clicking a cell explains the primitive, for example: "Catch Guichen: margin +1.6 kn effective (22.8 vs 21.2 at North Sea SS 4–5), needs 2 kn → 0.37".
  - This answers the open question "raw primitives or role scores?": **both**. Role scores sit in the Legend; the primitives are one click away in the table.
- **Fitness for Service** (in the Legend while the view is open): one bar per role, each with a one-line reason naming the enemy class that drives it ("Guichen gets away; Châteaurenault outruns her"). The best fit is highlighted and the assigned role is underlined.
- **Biggest levers** (left panel): the two or three edits that move the assigned role's score most, with the delta ("+1 kn: 0.34 → 0.48"). These are the marginal-cost chips of §7, measured in role score instead of tonnes.

### 14.3 Starting a design: the bare hull

An empty ship can't be built, and a type template smuggles in assumptions. The proposal is a **bare hull** plus **goals**.

- **New design dialog:**
  1. *What is she for?* (optional): pick an admiralty request or a role. This sets **goals, not contents**. The goals are the role's tests, turned into concrete targets against named enemies ("Catch *Guichen*: about 24.5 kn trial in the North Sea; defeat her: 6 in belt and 7.5 in guns or heavier (estimate)").
  2. *Start from*:
     - **Bare hull** (recommended)
     - **Draft for me** (the AI designer, to the goals)
     - **Copy a class in service** (keeps "developed from")
     - **Reference design** (historical layouts, with locked tech swapped for the navy's own).
  3. The navy's current technology is pre-selected: plant, hull construction, armour, crew standard.
- **The bare hull** is a hull, a two-level bridge and the minimum crew: no guns, no armour, no plant, 0 kn.
  - Shipgen built this at 0.1 kn [M]: **30.5 × 7.4 m, 186 t standard, 8 men**. It built at 4, 8 and 12 kn too: 30.5–36.5 m, 160–228 t.
  - It is "Classified as: Hulk", with every role at 0.00, and every enemy dot is in the Danger quadrant. That is the motivation to build.
  - A faint outline shows the length the request implies, so the player sees roughly where they are heading.
  - **It must build with no remarks**, so that the first warning the player sees is one they caused.
- **Why not a role template with guns in it?** Because "Cruiser" is an output. A template that already carries 6 in guns and 20 kn is the old class lock in disguise. The goals give the same guidance without deciding anything. "Draft for me" remains for players who want a starting ship.

### 14.4 Engine asks this adds (to §12)

8. **Speed 0 means no plant.** Shipgen rejects `speed_kn = 0` (`styles.base.DEFINED`). It must accept 0 as "no machinery": no plant weight, no funnels, no engine crew. At 0.1 kn today, the bare hull still gets a 1905 coal plant with **2 funnels** at 0 shp.
9. **A clean bare hull.** The bare hull currently builds with warnings:
   - "very beamy hull (L/B 4.1)"
   - "heels 26° in a beam gale"
   - "provisions for 5 days, but the fuel lasts 42 days".

   For a hulk, the beam rules and the provisions-versus-fuel check should not fire.
10. **Crash: low tower, no turrets.** `superstructure.tower_levels = 1` on a ship with no turrets crashes `layout.build_layout`: `turret_name('ABC', None)` while formatting the "bridge can't see over the turrets" warning. It is reproducible with a bare hull. Check whether the C# port has it.
11. **Role-evaluation inputs.** These are already mostly in the report and hitboxes. The roles code needs, per design:
    - effective-speed inputs: trial speed, length, freeboard, plant type (sustained fraction)
    - gun range per battery
    - belt and deck by zone
    - a damage-capacity estimate (displacement, subdivision cell count, armour coverage)
    - detection height (bridge and director eye heights are already reported).

    Role evaluation itself lives in the game, not in shipgen. It reads the ship dict like the renderer does.

---

## 15. One screen (revision, 2026-10-09)

The question was: how does the player switch screens, and can there be fewer of them? RTW and UA:D both keep the designer on one screen [S], and that is right.

**The answer: there are no screens to switch, only a focus.** The mockup boards were states of one screen. They looked like six screens because each was drawn on its own board. This revision makes the single screen explicit (mockup board "ONE SCREEN", `Designer`).

### 15.1 What never moves

| Zone | Contents | Always there because |
|---|---|---|
| Top bar | name, sketches A/B/C/+, ⇆ Compare, requirement badges, "Classified as / your role" | the goals and the verdict are the point of the whole exercise |
| Left rail | all 8 sections as an accordion; one is open | every control is one click (or one key) away |
| Ship stage | top view **and** side profile, armour colours always on | the ship is what you are making; every part is a door |
| Context band | the open section's own editor (arrangement strip, matchup chart, speed curve, armour schemes, …) | the only part that changes |
| Legend | particulars, weight bar, **mini catches · escapes · beats chart**, remarks | outputs must be visible while you edit anything |
| Bottom bar | history, accept | |

### 15.2 How focus changes, all in place, with no navigation

1. **Click a section header** in the rail, or **press 1–8**.
2. **Click a part of the ship.**
   - A turret opens Main battery and highlights the turret's group.
   - The belt on the side profile opens Protection. The side profile *is* the armour editor, and it grows while Protection is open.
   - A funnel opens Speed & machinery.
   - The bridge or the forecastle opens Hull & upperworks.
3. **Click a Legend line.** "Protection: belt 11 · deck 3" opens Protection, and "Displacement" opens Hull.
4. **Click a remark or a badge.**
   - "Over request … Fit…" opens goal-seek in the Speed band.
   - "Aft control in smoke … Show" highlights it on the ship.
5. **Click the mini matchup chart** ("open") to open Role with the big chart and the table.

Each of these does the same thing: open a section, change the band, move the highlight. The ship and the Legend never leave, so the player never loses their place. Esc closes the band back to the default (Main battery). Tab moves through the open section's essentials.

### 15.3 Where the former "screens" went

| Former board | Now |
|---|---|
| Main (Main battery open) | the default focus |
| Protection | focus on Protection. The always-visible side profile becomes the editor; there is no separate elevation view. |
| Speed & machinery | focus on Speed. The curve sits in the band; goal-seek is a popover on the amber badge. |
| Matchups | focus on Role for the big chart and the table. A **mini chart is always in the Legend**, so the "catches / escapes / beats" verdict is as constant as displacement. |
| Compare | **a toggle**, not a screen. The Legend widens into A·B·C columns with the differing rows shaded, and the other sketches' outlines are drawn on the ship at the same scale. Edits still go to the current sketch. |
| New design | **the empty state of the same screen**. The bare hull loads in, the Legend shows "Hulk, fits no role", and the band shows the start options (bare hull / draft / copy / reference) and the request's goals until the first edit. It is not a modal and not a separate screen. |

That leaves **one screen and one popover** (goal-seek). Everything else is focus.

### 15.4 Costs of the single screen, honestly

- **Space.** At 1600×960 the stage gets about 890 px for a 180 m ship (about 4.3 px/m). That is fine for a battleship, but a 30 m torpedo boat would be tiny. **The stage zooms to fit the hull's length**, with a scale bar. Compare draws everything at the largest sketch's scale.
- **The armour editor is cramped** in a 96 px side profile. While Protection is open, the profile grows to about 250 px and the band shrinks. This is the one place the layout reflows, and it does so in place.
- **The big matchup table** (8+ enemy classes × 4 tests) needs the band's full height. If the theatre has many classes, the table scrolls in the band, and the mini chart in the Legend carries the summary.
- **Smaller screens (1280×720).** The rail collapses to icons with numbers (1–8) and opens as an overlay, and the Legend narrows to particulars plus the mini chart. Nothing becomes a separate screen.

### 15.5 v2: overview ↔ focus (current proposal)

The user's refinement: clicking a section **expands it into the centre**, and the ship shrinks to a smaller live preview. That is clearer than a band under a large ship, and it keeps the screen from feeling like a spreadsheet. Mockup board "ONE SCREEN v2" (`Focus`). These states are drawn:
- the overview
- Protection
- Main battery
- Role.

**Two states of one screen:**

| State | Centre | Ship | How you get there |
|---|---|---|---|
| **Overview** | the ship, large and to scale, plus **8 section cards**. Each card has a mini visual, a one-line summary, a status line and a dot. | large, with hotspots | Esc, "← Overview", or finishing the last section |
| **Focus** | the section's **workspace** at full size: arrangement strip, armour elevation, matchup chart, speed curve, … | a **preview strip** (about 560 px) at the top, with an overlay that follows the section | click a card, a part of the ship, a Legend line or a remark; press 1–8 |

The top bar and the Legend never move in either state.

**Ideas that came out of drawing it:**

1. **Zoom, don't navigate.** The change is animated (about 200 ms): the card grows into the workspace and the ship shrinks into the strip. Esc reverses it. The player always knows where they are, because they watched themselves get there.
2. **Delete the form rail.** With a full-size workspace, the controls sit **on the drawings**:
   - the mm on each armour plate (click to type, scroll to change, drag edges)
   - turret tokens on the strip
   - a draggable speed point on the curve.

   Form fields remain only in "Details ▾" for the rare values. This is the single biggest step away from the spreadsheet look.
3. **One component, two zoom levels.** Each card's mini visual is the workspace's own widget drawn small: the strip, the profile, the quadrant, the curve. They are cheap to build and teach the workspace before you open it. They also make the overview read as a summary of the whole design.
4. **The preview's overlay follows the section**:
   - arcs for Main battery
   - armour for Protection
   - machinery and smoke reach for Speed
   - horizons and blind arcs for Fire control
   - range rings for Role.

   This removes the overlay toggle row from v1. "Alt" still shows all overlays.
5. **Status dots everywhere** (green or amber) on cards and tabs. Amber means the section causes a remark or misses a request goal. The 8 dots double as a checklist without being a wizard.
6. **Tabs + ←/→ + "Next: Speed & machinery →"** in focus give a gentle design order for first-timers (Role, Battery, Secondary, Protection, Speed, Hull, Fire control, Crew) without forcing it. Experts jump with 1–8.
7. **"Last change" in the preview strip:** the delta of the last edit ("belt 10 → 11 in → +310 t"), with a ghost outline of the previous hull when the length moved. Feedback on the ship stays visible even though the ship is small.
8. **Peek:** hold Space in focus to see the ship full size, and release to return. Hovering the preview strip can enlarge it in place.
9. **The overview doubles as the empty state.** In a new design (§14.3) the cards show the request's goals instead of summaries ("Speed: about 24.5 kn to catch Guichen"), and every dot is amber.
10. **Compare from the overview** turns each card's summary into "A vs B" deltas, while the Legend widens to columns (§15.3).

**Workspace budget at 1600×960:** about 1220 × 680 px (centre minus the preview strip and tabs). That is enough for a 1,120 px armour elevation at about 6 px/m, or a 640 px matchup chart plus its table. At 1280×720 the Legend can collapse into a slide-over (the "Catches · escapes · beats" mini chart and the remarks stay pinned in the top bar).

### 15.6 v3: the widgets follow the focus (current proposal)

After trying both, the user preferred v1's layout: the rail on the left and the ship pinned at the top. The improvement is that **the UI elements follow the focus instead of only being highlighted**. Mockup board "ONE SCREEN v3" (`Follow`, interactive with transitions; offline copy `follow.html`).

- **The ship sprite never moves.** It stays pinned at the top of the centre, with its hotspots.
- **The space under the ship rearranges itself for the section.** Every editor is a persistent widget with a small "docked" form and a big "focused" form, and selecting a section animates the widget between them (transform + scale, about 0.55 s, ease-out):

  | Section | What moves |
  |---|---|
  | Protection | the always-present **side profile grows** (780×90 → 860×340) into the armour editor; the thicknesses fade in on the plates |
  | Hull & upperworks | the side profile grows half-way (forecastle and tower labels) |
  | Role | the **catches · escapes · beats chart flies out of the Legend** into the centre and turns dark; its slot in the Legend shows "shown in the centre ↙"; the enemy names fade in |
  | Main battery, Speed, Fire control, Crew | their widget **grows out of the dock row** at the bottom (battery strip, speed curve, director horizon, habitability) while the previous one shrinks back into its slot |
  | Secondary | nothing grows; the casemate band lights up on the ship, and the panel holds the battery cards |

- **The section panel** (presets, details, costs) fades in about 0.3 s after the widget lands, beside or below it, so motion and content don't compete.
- **The rail's section body slides open** (max-height transition). The open section keeps its essentials in the rail, as in v1.
- **Every docked widget is a button.** Clicking the mini speed curve opens Speed, and clicking the Legend's chart opens Role. All routes stay the same: the rail, 1–8, ship hotspots, Legend lines and remarks.
- **Implementation in the game:** each widget is drawn once at its focused size and placed by a transform (translate and scale) that is interpolated between two rects. Labels that only make sense big fade with opacity. This is a few lines of tweening per widget in any immediate-mode or retained UI. Nothing is re-laid-out mid-animation, so it stays cheap.

Rules worth keeping:
1. **Only one thing grows at a time.** The previous focus shrinks while the new one grows, in the same 0.55 s.
2. **Things return to the slot they came from.** The chart always goes back to the Legend, and the strip back to the dock. Spatial memory is what makes it obvious.
3. **The ship and the Legend's numbers never animate position**, so they are always readable mid-transition.
4. **Respect reduced motion:** a setting makes it a 120 ms cross-fade.

### 15.7 v3 + Overview: the default view is a trim sheet (current)

The user's addition: the default view should let you change the main parameters and show more of an overview of the vessel. The sections then become detail work. The default view is for overview and **last-minute weight tuning**: dropping a knot, or shaving a bit of armour. It doesn't need to cover everything (no directors). This is section **0 · Overview** on the same board, and it is the state the designer opens in.

**Overview layout**, under the pinned ship:
- **The side profile at medium size**, with the armour thicknesses on the plates. The labels are counter-scaled so they stay legible. This gives an at-a-glance overview of the protection scheme.
- **The trim sheet.** One row per lever:
  - speed, range
  - belt, armour deck, turret faces
  - rounds per gun
  - secondary guns per side.

  Each row has `− value +`, a position bar with a tick at the opened value, and a chip giving **what one step buys back** ("−0.5 kn: −822 t", "−0.5 in: −155 t", "−10: −70 t"). Reading down the chips compares the knobs directly.
- **Standard vs request:** a big number and a meter against the request line. It reads "over the request by 970 t" (amber) or "under by 147 t ✓" (green), plus "since opened: −1,117 t · −4.4 m".
- **Consequences:** full load, length, GM, and the **role scores with trend arrows** ("Line of battle 0.81 ▼"). A knot or an inch of belt saved is never free, and the verdict shows the price.
- **Reset** (back to the opened values) and **⌖ Fit with speed** (goal-seek on the request).
- Everything else reacts live:
  - the Legend's particulars and weight bar (with its request line)
  - the top badge (amber → green)
  - the "Classified" score
  - the rail summaries
  - the plate labels on the profile.

**Rules:**
1. **The trim sheet only holds continuous levers that change weight.** Anything topological (turret counts, layout, armour scheme, plant type, directors) stays in its section. A 0.5-step tweak never reflows the design.
2. **Edits are shared.** A change made in the trim sheet shows in its section and the other way round; they are the same values.
3. **The step sizes are the "last-minute" grain**: 0.5 kn, 500 nm, 0.5 in of belt, 0.25 in of deck, 10 rounds, one pair of guns. Shift+click should take a ×4 step (not in the mockup). Typing still works.
4. **Each section's essentials live in that section**, and the Overview's levers are a curated subset. A future option: let the player pin any field to the trim sheet.

The tonnage per step in the mockup is a toy model, apart from the speed column, which interpolates the real 17–25 kn builds of `dreadnought.json` (§1.2). In the game these chips are the marginal-cost probes of §7: one background build per hovered lever.

---

## 16. Open questions

1. **The C# build time** on your machine decides how much of §11 is necessary.
2. ~~Type envelopes~~. Settled by `ship-roles-design.md`: there are none. Classes are assessed, not gated (§14).
3. **Design cost and time model** (design study months, "developed from" discount): the campaign layer's call.
4. **Penetration model.** The immunity zone readout waits on it.
5. **How much free placement, if any.** The proposal allows none: arrangements only. Possible later exceptions are turret rest angles and boat or crane positions, if players ask.
6. **Carriers, merchants and planing craft** need their own first-layer controls (air group, cargo deadweight, hull material). The same three-zone screen applies; the section list changes with the style.

---

## Sources

**RTW**
- RTW3 manual: https://ftp.matrixgames.com/pub/RuletheWaves3/RuleTheWaves3ManualPatch2026.pdf
- RTW3 FAQ: https://steamcommunity.com/app/2008100/discussions/2/3826416272370281249
- Naval Gazing, RTW3 review: https://www.navalgazing.net/Review-Rule-the-Waves-3
- Naval Gazing, naval video games survey: https://www.navalgazing.net/Naval-Video-Games
- Naval Gazing, UA:D review: https://www.navalgazing.net/Review-Ultimate-Admiral-Dreadnoughts
- Remaster patch notes: https://www.slitherine.com/news/remaster-update-is-now-available
- Vaporlens review aggregation: https://vaporlens.app/app/2008100/rule_the_waves_3.md
- Steam threads 4356743665188924535, 4363494744041839244, 5264192561397757151, 3826415752024029180, 7134317381878964119: https://steamcommunity.com/app/2008100/discussions/0/<id>

**UA:D**
- https://dreadnoughts.ultimateadmiral.com/feature-ship-design
- https://steamcommunity.com/app/1069660/discussions/0/7007112969440549216
- https://steamcommunity.com/app/1069660/discussions/0/3593338530522653156

**Others**
- Aurora: https://www.navalgazing.net/Aurora-Tutorial-Part-2
- HoI4: https://hoi4.paradoxwikis.com/Ship_designer and https://eip.gg/hoi4/guides/ship-designer-guide/
- Automation: https://discourse.automationgame.com/t/experiment-does-r-d-quality-cost-extra-how-does-it-compare-to-the-quality-slider/36413
- KSP 1.6: https://kerbalspaceprogram.com/news/1-6-to-vee-or-not-to-vee-is-now-available
- Cosmoteer: https://cosmoteer.wiki.gg/wiki/Ship_Editor
- Children of a Dead Earth: https://steamcommunity.com/app/476530/discussions/0/343788552534266122
- SpringSharp: https://lutris.net/games/springsharp/

**UX**
- Progressive disclosure: https://www.nngroup.com/articles/progressive-disclosure/
- Ladder of abstraction: http://worrydream.com/LadderOfAbstraction/
- Nested tooltips: https://www.pcgamesn.com/victoria-3/nested-tooltip-system
- Factorio FFF #318: https://factorio.com/blog/post/fff-318
- Fusion 360 timeline: https://help.autodesk.com/cloudhelp/ENU/Fusion-Assemble/files/ASM-TIMELINE.htm

**History**
- https://en.wikipedia.org/wiki/HMS_Dreadnought_(1906)
- https://www.secretprojects.co.uk/threads/the-development-of-hms-dreadnought.41487/latest
- https://en.wikipedia.org/wiki/G3_battlecruiser
- https://en.wikipedia.org/wiki/North_Carolina-class_battleship
- Design spiral: https://blogs.sw.siemens.com/simcenter/?p=17413

**Measured here:** shipgen `designs/*.json` leaf audit; `shipdesign.build` timings and +1 kn deltas run through device_bash on your machine (Python reference, 2026-10-09).
