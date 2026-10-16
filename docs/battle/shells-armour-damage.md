# Plan: shells, armour and damage (a 4.5 deepening)
Status: draft, for the user to agree    Updated: 2026-10-16

The fine-tune pass for the **Terminal** link of `fighting.md` 4.5 ("penetration proper: material qualities, capped and
uncapped shells, spaced armour, edge effects; shell quality by navy and year; diving shells and underwater hits;
near-miss splinters; a ricochet table"). Its research is `research/simulation/armor/` and `damage-research/`, read
together in `research/simulation/armour-and-damage.md`.

## The aim

The game already gets the *formula* right: its penetration fit is within 1–4% of Thompson's (armour-and-damage.md §2).
What it lacks is the shell's *state* after each plate, the thing that decides what a hit does. Today that comes from one
roll per era once a fuze starts (`ShellOutcomes`). This pass replaces that roll with causes, in the order the user
gave:

1. **S, what's hitting:** the shell as the design's data, not a placeholder from the look's era.
2. **P, how it hits:** plates as physical materials, and the shell's state plate by plate (cap, body, fuze, burst point).
3. **E, what it does:** the new states into the effects (fragments without a burst, flash through a holed turret, spall,
   splinters).

Flooding, fire, magazines' venting race and the service networks stay their own 4.5 sessions. This plan feeds them
better burst points and states; it doesn't rebuild them.

## The principles

- **Physics as numbers in the design, never by period** (`docs/shipgen/DECISIONS.md`: "differences between periods
  come from material science, given in the design as numbers like `machinery.tech`"). A shell and a plate carry their
  numbers. A year is only a template's label. Today the sim picks shells by the *look's* era, a paint scheme; this
  ends that.
- **Templates like the machinery's.** A tech block of physical numbers with a name as its label
  (`docs/shipgen/plant-templates.md`). The catalogue is written by a tool from the research tables
  (`tools/designer_templates.py`), and a design carries the blocks it uses. In the game the tech tree filters them.
- **Parity first, then change.** Each phase opens by moving the data without changing a number (hashes and goldens hold),
  then changes the behaviour one step at a time, with the bench's numbers before and after (as 4.5 asks).
- **Two layers of calibration** (armour-and-damage.md §5): the plate model alone against firing trials (no rolls), then
  the whole chain against battles, where the remaining roll is only fuze and filler failure, each with a sourced rate.
- **One implementation** (memory): the benches run the game's `Terminal`; `armor_pen_ref.py` stays a reference, not
  a second engine.

## Decided (the user, 2026-10-16)

1. **Materials become data.** DECISIONS.md's 2026-10-03 "plain string" for armour materials is reversed: "it was more
   of a placeholder at that point. Now that we have the research we have some actual values to input to the designer
   side." Recorded in DECISIONS.md. What changes:
   - A material becomes a block of numbers (below). Its name is still the label the parts use.
   - **Mass follows the material's density.** Steel armours differ under 0.5%, so most ships don't move. Wrought iron
     (7.7), compound (7.75), wood (0.65–0.75) and aluminium alloy (2.7) do. Wooden PT boats, aluminium deckhouses and
     the ironclads shift, and they're reported (memory: weights follow physics).
2. **A design-level `materials` table**, keyed by name. A block per part would be the way to model variance in
   construction quality; that's not wanted yet, and the table can change to it later.
3. **Choosing the shell by target belongs to the AI's pass.** This plan builds the tools (the ammunition, the
   outcomes); the AI's pass decides how to use them.

Proposed, for the user to confirm: the shell tech that scales with calibre (S, "How a block scales"), and Thompson as
the engine from P1 (P1, "The formula").

## What shipgen needs

### Ammunition, per battery (phase S)

A **shell tech** block, calibre-free like `machinery.tech`, so one block serves a navy's whole family of guns:

| Field | Example (RN 15″ Greenboy, 1918) | Used for |
|---|---|---|
| `name` | "Greenboy APC, Shellite (1918)" | label |
| `kind` | `ap` / `common` / `he` | which fuze, filler share |
| `w_d3` | 0.57 lb/in³ (or `mass_kg` per battery) | mass: flight, penetration, **magazine weight** |
| `nose_crh`, `form_factor` | 4 crh, 1.1 | drag (fire control's tables); nose ±10–15% |
| `cap` | `{kind: hard, share: 0.116}` (`none`, `soft`, `hard`, `hard2`, `break_away`) | decapping, shatter, the cap's lost mass |
| `windscreen` | true | lost on any impact |
| `body` | `{break_ob_deg: 20, break_t_d: 0.67, quality: 1.0}` | when it breaks up; the thickness multiplier (armor/00 §5.4: 0.85–1.11) |
| `filler` | `{share: 0.024, explosive: "Shellite", re: 1.0, sensitive: false, booster: true}` | burst size, bursts in the plate, low-order rate |
| `fuze` | `{position: base, delay_s: 0.025, start_cal: 0.07, oblique: 0.25, plates: single, scatter: 0.25, blind: 0.05}` | start threshold, burst point, duds |

Per battery: `shells` (the kinds carried, each a tech block or a template name, with the outfit's shares), and an
optional `muzzle_velocity_ms` (the gun's, until a gun tech exists; the placeholder's 280 + 11·L meanwhile).
`rounds_per_gun` is already there.

**How a block scales with calibre.** Everything in it is either a share or a calibre-relative number, because that's
how the physics works and how the sources quote it:

- **Mass = `w_d3` × D³.** Within one navy's generation of shells, W/D³ is nearly flat from about 8″ up
  (armour-and-damage research tables, lb/in³): the IJN's Type 91 0.54 at 20, 36 and 46 cm; the Kriegsmarine's 0.53 at
  20.3, 28 and 38 cm; the RN's WWII APC 0.57–0.58 at 14″ and 15″ (Nelson's light 16″ 0.50, a choice); the USN's
  super-heavy 0.65–0.66 at 8″ and 16″.
- **Filler, cap and windscreen are shares of the mass:** AP 1.5–3.5%, a hard cap 9–12%.
- **The fuze's start threshold, the decap thresholds and the break-up limits are in calibres or T/D.** A fuze starting
  on 0.07 calibre starts on 28 mm for a 16″ shell and 11 mm for a 6″, which is why big AP passes through destroyers.
- **The delay is in seconds**, the same for every calibre (the burst distance follows the residual velocity).
- **The nose (crh) and the drag form factor are shapes.**

What doesn't fit one block:
- **Light, hand-worked guns ran lighter:** the USN 5″/38 0.44, the IJN 12.7 cm 0.41, the KM 15 cm 0.49. They are their
  own templates ("USN 1941 5″ AA common"), as the plant templates have a destroyer's block beside a battleship's.
- **A new generation is a new block:** the USN 14″/45's older AP at 0.55 beside the Mk 8 at 0.66.
- **One battery's own shell:** `mass_kg` on the battery overrides `w_d3` (the 6″/47's 59 kg super-heavy, a Nelson
  shell).

So a navy and year has two or three blocks (heavy AP, heavy HE or common, light guns), and a design picks one per kind
for each battery. That's the same count as its plant templates.

Two more things the ammunition carries, used by later sessions rather than this one:

- `charge`: `{kg, propellant, packaging: bag | case}`, for the magazines' venting race (`07-magazine-explosions.md`;
  today `DesignerPlaceholders.Charge` and a side's propellant factor in the scenario).
- The dye (already a sim fact) could move here too.

**Shipgen's own use:** the magazines are weighed with one W/D³ for every gun and era (`Batteries.ShellK`, the 16″
Mk 8's). The block's W/D³ and the charge's mass replace it, so a light Victorian shell weighs less than a super-heavy
one (memory: weights follow physics; reported, goldens updated).

**Out of shipgen** (`report.json`, then `ShipClassCompiler`): each battery's resolved shells (masses in kg, fuze,
filler kg), which `DesignerPlaceholders.Shell`, `ShellMix` and `Fires` give today. Those three placeholders are deleted.

### Materials (phase P)

A design-level table, **`materials`**, keyed by the names the parts already use (`armour.materials`, per-deck and
per-battery `material`, `hull.plating.material`, `superstructure.material`):

| Field | Example (KC n/A) | Used for | Source |
|---|---|---|---|
| `density_t_m3` | 7.85 | weight | armor/03 §7 |
| `q` | 1.01 | thickness multiplier, STS = 1 at 8″ | armor/00 §4.2 |
| `q_scale` | 0.03 | Q(D) = q·(D/8″)^−k above 8″ | armor/00 §4.2 notes |
| `face_hardened` | `{share: 0.41, min_mm: 100}` | shatter, always decaps, always starts a fuze; homogeneous below min | armor/00 §4.2, 03 §5 |
| `elongation_pct` | 18 (Wotan Hart) | Okun's penalty for homogeneous plate against shells over 8″ | armor/00 §4.2 |
| `backing` | 0 (teak 0.01, cement 0.04) | weight-only layers that count little | armor/05 §5 |
| `fragments` | `mild` / `hard` / `fh` | THOR's constants (phase E) | armor/01 §3.10 |

Unknown names are an error once the table exists. Old designs are upgraded by `DesignFormat` (version 2): every name
the catalogue knows becomes its block.

Two more armour inputs:

- **`armour.gratings`**: the armour grating over an uptake's opening, as a share of the deck (default about 0.4: a 15″
  holed grating stops what 6″ of solid plate does, O.S. 751). The open gratings design input (memory) gets its first
  number. Today the tracer makes a grating as thick as the deck (`HitPath.cs`).
- **`armour.backing`** per belt: `{material, mm}` (Littorio's oak, US Class A's 2″ of concrete, the ironclads' teak).
  Weight, and a near-zero plate in the path.

**Out of shipgen:** the materials table in `report.json`; hitboxes keep the names; `ShipClassCompiler` compiles a
material row per name (the class's material table exists, with names only). `settings.json` `ArmourQuality` and
`ArmourQualityOther` are deleted.

### Templates

- **`docs/shipgen/shell-templates.md`** and `Content/Templates/shells.json`: shell tech by navy and year, from
  armor/04 §1.7 (the representative shell table), armor/02 §4.6 and armor/00 §5.4 (shell quality by family), and
  gunfire_ref.py's SHELLS. Examples: Palliser (1867), uncapped forged steel (1890), Makarov soft-capped (1895),
  RN lyddite APC (1914), German APC with the C/11 fuze (1914), Greenboy (1918), USN Mk 8 with the Mk 21 (1941), IJN
  Type 91 (1931), and HE and common of each family.
- **`docs/shipgen/material-templates.md`** and `materials.json` become blocks: the 35 materials of armor/00 §4.2 and 03
  §8 (00's reconciled Q where the two disagree), plus the structural ones (mild steel, HT, Ducol, STS, aluminium alloy,
  wood).
- **`armour.json`** stays the *scheme* templates (part → material name). Applying one adds the blocks it names to the
  design's table.
- The designer's template column (`DesignerScene.Layout.cs`: Plant, Hull, Crew, Armour) gains **Shells** and
  **Materials**.

## Phase S: what's hitting

- **S0 Shells as design data (parity).**
  - Shipgen reads `main[k].shells` and the secondaries', validates them, and writes the resolved shells to the report.
  - The compiler reads them instead of `DesignerPlaceholders.Shell`.
  - A tool writes every shipped design's shells *from today's placeholder*, so every number holds: hashes, `shipgen
    ballistics`, `duel`, and the goldens apart from the new keys.
  - The magazine weight keeps `ShellK` in this step.
- **S1 The templates.** `shell-templates.md` and `shells.json`; the designs' blocks are switched to the templates of
  their navy and year (Dreadnought gets the RN's 1906 APC, Bismarck the 38 cm L/4,4 with the Bd.Z. 38, Yamato the Type
  91).
  - This moves numbers: masses (W/D³ 0.42–0.66 instead of one curve), the drag form factor, filler.
  - Report `shipgen ballistics` (range tables), `duel` (fire control's calibration, hit % and time to first hit) and
    `weapons`, before and after.
- **S2 Ammunition weight.** The magazines weighed from the block's W/D³ and the charge. Report the displacement shifts,
  run `golden-update` with a `ShipDesign.Version` bump and `mono-check`.
- **S3 The designer.** A shell template picker in the Main battery and Secondary tabs (the template column); the
  battery table shows the kind, mass and fuze. Light, in the tabs' existing style.
- **Not here: the shell by target** (decided). A battery fires one kind in v0 (AP from heavy guns). This plan gives
  every battery its outfit of kinds; firing HE or common at unarmoured targets is the AI's pass (the matchup bench showed
  it matters: four light cruisers against a battleship).

## Phase P: how it hits

- **P0 Materials as data (parity).**
  - The `materials` table in the design, the `DesignFormat` upgrade, the catalogue's blocks with `q` set to today's
    `ArmourQuality` values, and the compiler's material rows.
  - Mass still 7.85 in this step. Nothing moves.
- **P1 Densities and the real Q.**
  - Mass by the material's density (report the shifts: the ironclads, PT boats, aluminium houses).
  - `q` from armor/00 §4.2 with Q(D) and the thin face-hardened rule. WWI KC goes from 1.0 to about 0.85–0.88.
  - Structural steels get their own Q instead of the 0.6 catch-all.
  - **The plate-only bench** (`shipgen pen-check`): the game's `Terminal` plate step against `armor/07-*.csv` (the USN
    16″ table, NPG 1946–47, the Italian obliquity tables, Annapolis, Indian Head, Beardmore, Baden). No rolls.
  - **The formula: Thompson** (proposed). It is the basis the Q table is on, and it gives the same answers as today's
    fit except on thin decks under small shells (+13%). It's cheaper per plate:
    - `V_L = 0.024056·D·√(T/W)·F / cos Ob`, with `F = 6·(T/D − 0.45)·(Ob² + 2000) + 40000` (Ob in degrees): one
      square root and a few multiplies. Today's fit takes four `Math.Pow` calls.
    - It gives the limit *velocity* of a plate, which is what the plate step wants: compare the shell's speed with
      `V_L`, and the residual follows from both.
    - The inverse (how thick a plate a shell beats at a range) needs a few Newton steps. Only readouts want it: the
      matchup's profiles (`Matchup.cs`), `shipgen matchup`, and the designer's immune zone later. A hit never does.
    - Valid to 75° of obliquity; past it the cosine carries on, and the ricochet gate (P5) takes over from 80°.
    - Either way it's noise: a hit costs about 200 µs in all (fighting.md 4.3a).
    - The plate bench still checks it against the trials before the old fit goes.
- **P2 Caps, decapping, shatter and break-up as states.**
  - The cap is lost on face-hardened plate or past its threshold, with a gap test before the next face-hardened plate.
  - Shatter of uncapped or decapped shells on face-hardened plate: +30–40% at normal, fading to nothing by 55°.
  - Break-up by the body's obliquity and T/D limits, most likely near the limit around 45°.
  - `Broken` leaves `ShellOutcomes`.
  - Benches: the matchup (Arethusa, Iron Duke), Jutland's 1/13 on more than 9″, the 1894–1905 battles, `shipgen hits`
    on the destroyers.
- **P3 Fuzes per fuze.**
  - Start threshold and obliquity factor per fuze; the two-plate fuzes adding up thin plates in quick succession; face-hardened plate always starts it.
  - Delay scatter per fuze; blinds per fuze.
  - The `ShellOutcomes` rows are gone: what's left lives in the shell's `fuze.blind` and `filler`.
  - Benches: bursts behind the plate (Baden 38 ft), Iowa's bomb deck moving bursts up, South Dakota's pass-throughs.
- **P4 Three limits.** Holed (a hole, the shell broken, fragments only), Navy (the whole shell through) and effective (through,
  whole, fuze working), with a new `Outcome` for holed-but-broken.
  - The damage report's line says which.
- **P5 Angles.**
  - The ricochet gate at 65–80°, by V/V_L, certain from 80°, none for broken shells.
  - Compound obliquity with heel and barbette curvature.
  - Normalisation toward the plate's normal on the way through.
  - The residual-velocity rule stays the game's (it fits the spaced deck better); its burst distances are checked
    against Baden and the Hood deck series.
- **P6 Under water.** The shell's speed by the distance it travels in the water (`e^(−x/L)`), the fuze clock starting
  at the water, and the Type 91 as the exception that keeps going. This replaces `UnderwaterSpeedShare`'s flat 0.4.
- **P7 Gratings, backing and scatter.**
  - `armour.gratings` and `armour.backing` in the tracer.
  - Plate quality drawn per ship (about N(1, 0.06)) and per shot (N(0, 0.03)), instead of the ±10% per shot.
- **Later, if wanted:** edges and joints (up to 15% within 1.5 calibres of a plate's edge, from the hitbox's extents),
  and a hole as a free edge for repeated hits.

## Phase E: what it does

- **E1 Holed but broken.**
  - Fragments at about 0.15× through the hole, no full burst.
  - On a gunhouse, working chamber or barbette, a flash source for the magazine roll (Seydlitz at Dogger Bank).
- **E2 Behind a plate that held.** Spall and a plug off thin or face-hardened plate near its limit: a 30–45° cone, 3–5
  m, as a weaker burst inside (Tiger's roof).
- **E3 Fragments.**
  - THOR's equations for fragments against the plating, by the material's `fragments` class.
  - This replaces Okun's table in `Effects.FragPenMm` for parts boxed by armour and splinter decks.
  - **Near-miss splinters:** a shell bursting on the water near a ship sends fragments into its side and upper works
    (`08-gunfire-effects.md` §8). The splash system has the miss's position already.
- **E4 The battle anchors.** With S and P in, the whole chain against the battles: Jutland, Bismarck's ~3 belt holes in
  ~400 hits, South Dakota, Samar, the 1894–1905 battles, the destroyer benches. The leftover rolls are tuned only within
  their sourced bands. `fighting.md` question 1 (the penetration fit) is answered here.

## What stays out

- Bombs: the deck physics serves them, but bomb shapes and fuzes aren't researched yet.
- The magazines' venting race, flooding, fire spread and the service networks: their own 4.5 sessions, fed by the
  ammunition's `charge` (S0) and better burst points (P3).
- The AI choosing the shell by target (the AI's pass).
- Variance in construction quality per part (a block per part; decision 2).

## Questions for the user

1. **The shell tech that scales with calibre** ("How a block scales"): two or three blocks a navy and year, a
   battery's `mass_kg` overriding. Agreed?
2. **Thompson from P1**, checked by the plate bench first. Agreed?

## Steps at a glance

| Step | Shipgen | Sim | Moves numbers? |
|---|---|---|---|
| S0 | shells read, reported | compiler reads them | no |
| S1 | shell templates | — | ballistics, fire control |
| S2 | ammunition weight | — | displacement |
| S3 | designer picker | — | no |
| P0 | materials table, upgrade | material rows | no |
| P1 | densities | Q(D), pen-check bench | weights, penetration |
| P2 | — | caps, shatter, break-up | outcomes |
| P3 | — | fuzes | burst points |
| P4 | — | three limits | outcomes |
| P5 | — | angles | ricochets, paths |
| P6 | — | under water | underwater hits |
| P7 | gratings, backing | scatter | plates |
| E1–E3 | — | effects | damage |
| E4 | — | tuning | the anchors |
