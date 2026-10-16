# Plan: shells, armour and damage (a 4.5 deepening)
Status: agreed, steps reviewed for implementation    Updated: 2026-10-16

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
4. **The shell tech scales with calibre** (S, "How a block scales"): a default template, plus more beside it ("we may
   want to have silly shells later"). Nothing in the block is limited by period or plausibility, only by what can
   physically be (memory: allow silliness), so a player's odd shell is a block like any other.
5. **Thompson is the engine from P1a** (P1a, "The formula"), on Claude's recommendation: the same answers as today's fit
   within 1–4% where tried, cheaper, the basis the material table is rated on, and in the limit velocity V_L that P2–P5's
   rules are written in (holed and effective limits, shatter and break-up near the limit, the ricochet gate, the
   residual). The plate bench checks it against the trials before the old fit goes.
6. **The loadout is the admiral's, the armour the designer's** (user, 2026-10-16: "Admiral naturally may want to change
   the ammunition loadouts of their ships even ship by ship basis, whereas they can't just change armor willy nilly").
   The design carries a **default loadout**; each ship may carry another, set outside the design (S5).
7. **Default outfits and the magazine order** (user, 2026-10-16): main batteries 70% AP and 30% HE, secondaries 30% AP
   and 70% HE. Magazines empty AP first on the main battery and HE first on the secondaries; the AI's behaviour is
   otherwise as it is. A dev toggle on the right-click fire picks the kind to shoot (S2).

## What shipgen needs

### Ammunition, per battery (phase S)

A **shell tech** block, calibre-free like `machinery.tech`, so one block serves a navy's whole family of guns. The
blocks live in a design-level **`ammunition`** table keyed by name, as the materials do (decision 2's reasoning: a
navy's 15″ main and 6″ secondary share one block, written once):

| Field | Example (RN 15″ Greenboy, 1918) | Used for |
|---|---|---|
| `kind` | `ap` / `common` / `he` | which fuze, filler share |
| `mass_k_kg_m3` | 15,800 (0.57 lb/in³; 1 lb/in³ = 27,680 kg/m³) | mass = k·D³ (D in m): flight, penetration, **magazine weight** |
| `nose_crh`, `form_factor` | 4 crh, 1.1 | drag (fire control's tables); nose ±10–15% |
| `cap` | `{kind: hard, share: 0.116}` (`none`, `soft`, `hard`, `hard2`, `break_away`) | decapping, shatter, the cap's lost mass; `break_away` is the diving shell (`ShellTypeRow.Diving`, P6) |
| `windscreen` | true | lost on any impact |
| `body` | `{break_ob_deg: 20, break_t_d: 0.67, quality: 1.0}` | when it breaks up; the thickness multiplier (armor/00 §5.4: 0.85–1.11) |
| `filler` | `{share: 0.024, explosive: "Shellite", re: 1.0, in_plate: 0.0, low_order: 0.1}` | burst size; the rates of bursting in the plate (lyddite's 0.35) and low-order, each sourced (armor/04 §6, §8.2) |
| `fuze` | `{position: base, delay_s: 0.025, start_cal: 0.07, oblique: 0.25, plates: single, scatter: 0.25, blind: 0.05}` | start threshold, burst point, duds |

Metric like the rest of shipgen; the templates' notes give lb/in³, the literature's unit.

Per battery, **`shells`**: the outfit, entries `{ammo: <name in the table>, share}`, each with an optional `mass_kg`
(one battery's own shell, over k·D³). And the battery's **`muzzle_velocity_ms`**, the gun's with its heaviest shell
(the placeholder's 280 + 11·L when absent, until a gun tech exists); a lighter kind goes faster by the placeholder's
rule, (m_heavy/m)^0.25, unless its entry gives its own. `rounds_per_gun` is already there.

A design carries the blocks it uses; templates are copied in (by the designer, by a tool), never referenced by name,
as `machinery.tech` is.

**Design default and loadout (decision 6).** What the design fixes and what the admiral may change:

| The design's (fixed with the ship) | The loadout's (per ship, changeable in port) |
|---|---|
| the guns: calibre, length, mounts | which shell blocks each battery carries (any block of the gun's calibre) |
| the magazines: `rounds_per_gun` (DECISIONS.md, 2026-10-07) | the shares of the outfit within those rounds |
| the armour and its materials | — |

So `ammunition` and the batteries' `shells` in a design are its **default loadout**: what shipgen weighs the magazines
with, and what a ship gets when nobody says otherwise. Nothing checks that a loadout's shell suits the gun beyond its
calibre (memory: allow silliness); a heavier shell leaves the gun slower by the same fourth-root rule as the kinds.

A loadout heavier or lighter than the default changes the ship's displacement: Iowa's 1,170 rounds all AP rather than
all HC is about 425 t on 48,000 t, under 1%. v0 leaves the sim's hydrostatics on the design's weight, flagged; the
admiral's screen can show the difference later.

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

**Not added in this plan** (the schema leaves room; each is its own step later):

- `charge`: `{kg, propellant, packaging: bag | case}`, for the magazines' venting race (`07-magazine-explosions.md`).
  Today it's `DesignerPlaceholders.Charge`, which the muzzle flashes read too (`docs/visuals/muzzle-flashes.md`), and a
  side's propellant factor in the scenario. Moving it touches the VFX, so it stays out.
- The dye (already a sim fact).

**Shipgen's own use:** the magazines are weighed with one W/D³ for every gun and era (`Batteries.ShellK`, the 16″
Mk 8's). The outfit's mean shell mass replaces it (the charge keeps its share in `AmmoMult` until the charge is added),
so a light Victorian shell weighs less than a super-heavy one (memory: weights follow physics; reported, goldens
updated).

**Out of shipgen** (`report.json`, then `ShipClassCompiler`): each battery's resolved shells (kind, mass in kg, muzzle
velocity, form factor, cap, body, filler kg and rates, fuze) and the outfit's shares. `DesignerPlaceholders.Shell` and
`ShellMix` are deleted. `Fires` (guns of 200 mm and up fire AP) gives way to the magazine order (decision 7, S2): a
firing rule in the sim, reading the outfit, until the AI's pass (decision 3).

**One source for the catalogues.** The built-in blocks (ammunition and materials) live in shipgen as C# tables, as the
crew standards (`Crew.cs`) and the default plant (`Powerplant.cs`) do, because `DesignFormat`'s upgrade and the defaults
need them and shipgen reads no game assets. `Content/Templates/*.json` and the templates pages are written from them by
the tool, never edited by hand (memory: single implementation).

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

Old designs (shipped, fuzz, scenario and the players' saves) are upgraded by `DesignFormat` (version 2): every name the
catalogue knows becomes its block. A name nobody knows gets the steel block and a warning in the report, never an error
(DECISIONS.md: the generator is permissive; a missing anchor falls back with a warning).

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

- **`docs/shipgen/shell-templates.md`** and `Content/Templates/shells.json`: shell tech by navy and year, the
  designer's templates and the game's loadout catalogue both (the tech tree filters it in the game), from
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
  **Materials**. The material knobs (`Knobs.SchemeMaterial` and its kin) list the design's table and the catalogue
  instead of `Templates.MaterialNames`. UI work follows the designer's way (memory: one tab a session, mockup then port).

## Phase S: what's hitting

## Rules for every step

- **Benches before and after,** in the step's commit message and its section here: the step's own benches, plus
  `shipgen hits`, the matchup bench and the squadrons' scenario for any step that moves outcomes.
- **The matchup mirrors the terminal.** `Matchup.cs` estimates outcomes analytically from the same facts (`PenMm`,
  `ShellOutcomes`). Every step that changes `Terminal` changes the matchup's estimate in the same commit, so the AI's
  judgement never reads a different model from the one that fights.
- **Test bands are the anchors.** A step that moves a `DamageTests` anchor out of its band reports it; the band is
  widened only with the user's say, as P6's outcomes were (user, 2026-10-16: accepted, not tuned, until damage control).
- **No allocation per hit,** and no new per-hit cost worth measuring (about 200 µs a hit today).

## Phase S: what's hitting

- **S0 Shells as design data (parity).**
  - Shipgen reads `ammunition` and the batteries' `shells`, validates them, and writes the resolved shells to the
    report. The compiler reads them instead of `DesignerPlaceholders.Shell` and `ShellMix`.
  - A tool writes every shipped and scenario design's ammunition *from today's placeholder*, so every number holds:
    - each battery's `mass_kg` per entry (the placeholder's k varies with calibre, so one block's k can't hold it yet),
      `muzzle_velocity_ms`, the form factor, filler shares and fuze delays, and the Type 91's `break_away` cap;
    - **a transitional `outcomes` field** naming today's `ShellOutcomes` row (`rn1916`, `km1916`, `rn1918`, `ww2`,
      `ijn_t91`), until P3 replaces the rows with the fuze's and filler's rates and deletes it.
  - Checks: the sim's hashes, `shipgen ballistics`, `duel` and `weapons` unchanged; the goldens gain the new keys only
    (`golden-update`, no value moves).
  - The magazine weight keeps `ShellK` in this step.
- **S1 The templates.** The catalogue's ammunition, `shell-templates.md` and `shells.json`; the designs' blocks are
  switched to the templates of their navy and year (Dreadnought gets the RN's 1906 APC, Bismarck the 38 cm L/4,4 with
  the Bd.Z. 38, Yamato the Type 91), and the parity `mass_kg` overrides go.
  - This moves numbers: masses (k from 0.42–0.66 lb/in³ instead of one curve), the drag form factor, filler. The
    `outcomes` field stays as S0 set it.
  - **The filler's TNT equivalent:** `Effects` sizes every burst by W = filler kg × `FillerTnt`, which is 1 for every
    explosive today. The block's `filler.re` replaces it (black powder 0.55, lyddite 1.17, TNT 1, armor/04 §2.1), so a
    Victorian powder-filled common shell bursts smaller than its kilograms say.
- **S2 Default outfits and the magazine order** (decision 7).
  - **Outfits:** every design's default loadout is 70% AP and 30% HE on its main batteries and 30% AP and 70% HE on its
    secondaries, replacing `ShellMix`'s mix by calibre and era. Common shell leaves the defaults; its templates stay for
    loadouts and players.
  - **Rounds by kind:** a mount counts its rounds of each kind (today one count a mount, `CombatStore` `Rounds`), out of
    the battery's `rounds_per_gun` by the outfit's shares.
  - **The magazine order:** a main battery fires AP until it's spent, then HE; a secondary battery HE first, then AP.
    This replaces `Fires`' 200 mm rule, a hidden size threshold (DECISIONS.md: no arbitrary size cutoffs). The AI is
    otherwise unchanged.
  - **The dev toggle:** the strip's Fire row (`MapScene.Shells.cs`, `fireArmed`; right-click to shoot) gets a kind:
    the magazine order (the default), AP, HE, or common when the battery carries it. `ShellSystem.FireAt` takes it, and
    so does the flag: `-fire=ship[:battery][:kind]:lat,lon`. A kind the battery has none of left fires nothing, and the
    strip says so.
  - **What moves:** small guns on a destroyer's or cruiser's *main* battery now fire AP first (today HE, under 200 mm),
    and heavy secondaries HE first. Before and after: `shipgen hits`, the matchup bench (the destroyer and light-cruiser
    pairs most), the squadrons and the flotilla scenarios.
  - Report `shipgen ballistics` (range tables), `duel` (fire control's calibration, hit % and time to first hit) and
    `weapons`, before and after.
- **S3 Ammunition weight.** The magazines weighed from the default outfit's mean shell mass (the charge stays in
  `AmmoMult`). Report the displacement shifts,
  run `golden-update` with a `ShipDesign.Version` bump and `mono-check`.
- **S4 The designer.** A shell template picker in the Main battery and Secondary tabs (the template column); the
  battery table shows the kind, mass and fuze. A UI session of its own (mockup, then port); it can run any time after
  S1, alongside phase P.
- **S5 Loadouts, ship by ship** (decision 6).
  - **In the sim:** shell types already live in a table shared across classes by id (a gun's trajectory table is built
    once); a loadout registers the types it brings there. Each ship holds its own loads per battery (in `CombatStore`),
    defaulting to its class's, and its rounds by kind. Gunnery, the terminal and `Matchup.cs` read the ship's loads, not
    the class's.
  - **Where it's set:** a scenario's or mission's ship or unit takes `"loadout": {battery: [{ammo, share}, ...]}`, the
    ammo a catalogue name (`Content/Templates/shells.json`: the game reads its assets, unlike shipgen) or an inline
    block; a dev flag `-loadout=ship:battery:name[:share],...` for benches and screenshots; `shipgen hits --loadout`.
  - **Changed in port only:** the command sets it on a ship that isn't at sea (the campaign's ports come later; until
    then at spawn). The admiral's screen for it belongs to the command UI, not this plan.
  - Checks: a ship with the default loadout is byte-identical to S1's; one with HE in its heavy guns shows it in the
    hits bench (passes through destroyers far less).
- **Not here: the shell by target** (decided). A battery fires by the magazine order (S2). This plan gives
  every battery its outfit of kinds and every ship its loadout; firing HE or common at unarmoured targets is the AI's
  pass (the matchup bench showed it matters: four light cruisers against a battleship).

## Phase P: how it hits

- **P0 Materials as data (parity).**
  - The `materials` table in the design, the `DesignFormat` upgrade, the catalogue's blocks with `q` set to today's
    `ArmourQuality` values, and the compiler's material rows. `ArmourQuality` leaves the settings.
  - Mass still 7.85 in this step. Nothing moves: hashes and golden values hold (the goldens gain the table).
- **P1a Thompson and the plate bench** (sim only; the materials' `q` as P0 left them).
  - `Terminal`'s plate step on Thompson's V_L, the residual from V and V_L (the game's rule, P5), the deck multiplier
    below, and the inverse for `Matchup.cs` and the bench's readouts.
  - **The plate-only bench** (`shipgen pen-check`): the game's `Terminal` plate step against `armor/07-*.csv` (the USN
    16″ table, NPG 1946–47, the Italian obliquity tables, Annapolis, Indian Head, Beardmore, Baden), and both models'
    decks. No rolls. It reads the CSVs from the repo, as a tool may; nothing the release needs.
  - Expect small moves (1–4%, thin decks under small shells 13%).
  - **Why the game has the thickness form today:** no decision was made. The damage research's game shortcut
    (`05-mechanics-and-games.md` §A3: "precompute per gun `pen_belt(range)`, `pen_deck(range)` ... penetrates if pen ×
    rand > eff") and `gunfire_ref.py`'s `pen_mm` both ask what thickness a shell beats, because the tables they were
    fitted to (navweaps') are printed that way, as RTW's and the tabletop games' are. Garzke & Dulin is itself a fit of
    thickness, so it has no limit-velocity form. 4.3a ported the reference as it was (fighting.md 4.3a, "Penetration is
    the reference's own fit") and asked the user to keep it or not (fighting.md question 1). The thickness form stays
    right for readouts ("400 mm at 15 km"); the plate step wants the velocity.
  - **The formula: Thompson** (decided, 2026-10-16). It is the basis the Q table is on, and it gives the same answers as today's
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
    - **Decks hit at 50–90° of obliquity** (fall under 40°, nearly every battleship deck hit at fighting range, and
      cruiser decks under 8″ fire at 15–25 km) are where Thompson and Okun's M79 disagree: M79 has an STS deck holding
      15–20% more there, and 10–16% less on truly plunging hits at 30–50° (`armor_pen_ref.py` §N). Which is right is
      open: Thompson has no scale term, but the Hood 1919 deck trials lean the other way, and the Iowa immune zone is
      Thompson's own, so it can't decide. So: a settings multiplier on a deck's V_L by obliquity, 1 (pure Thompson) by
      default; the bench prints both models' decks; the battle anchors (Scharnhorst's boiler room, Jean Bart's decks,
      cruiser decks) move it if they ask. It shifts the immune zone's far edge by 1–3 km.
- **P1b The real Q** (sim only).
  - `q` from armor/00 §4.2 with Q(D), the ductility penalty and the thin face-hardened rule. WWI KC goes from 1.0 to
    about 0.85–0.88.
  - Structural steels get their own Q instead of the 0.6 catch-all.
  - The plate bench before and after.
- **P1c Densities** (shipgen only).
  - Mass by the material's density: report the shifts (the ironclads, PT boats, aluminium houses), `golden-update`
    with a `ShipDesign.Version` bump, `mono-check`.
- **P2 Caps, decapping, shatter and break-up as states.**
  - The cap is lost on face-hardened plate or past its threshold, with a gap test before the next face-hardened plate.
  - Shatter of uncapped or decapped shells on face-hardened plate: +30–40% at normal, fading to nothing by 55°.
  - Break-up by the body's obliquity and T/D limits, most likely near the limit around 45°.
  - `Broken` leaves `ShellOutcomes`. A broken shell is still `Outcome.BrokeUp` here; P4 splits it into holed and
    through.
  - Benches: the matchup (Arethusa, Iron Duke), Jutland's 1/13 on more than 9″, the 1894–1905 battles, `shipgen hits`
    on the destroyers.
- **P3 Fuzes per fuze.**
  - Start threshold and obliquity factor per fuze; the two-plate fuzes adding up thin plates in quick succession; face-hardened plate always starts it.
  - Delay scatter per fuze; blinds per fuze.
  - The `ShellOutcomes` rows and the transitional `outcomes` field are gone: what's left lives in the shell's
    `fuze.blind`, `filler.in_plate` and `filler.low_order`. The templates carry the sourced rates; S0's parity designs
    get them from their row.
  - `FuzeStartsOnWater` on (P6's scaffold).
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
- **P6 Under water** (mostly done; what's left is P3's switch). The shell's speed by the distance it travels in the
  water (`e^(−x/L)`), the fuze clock starting at the water, and the Type 91 as the exception that keeps going. This
  replaces `UnderwaterSpeedShare`'s flat 0.4.
  - **Done ahead of the pass (2026-10-16):** the speed by the run (`Sim.Combat/WaterRun.cs`: L 25 calibres pointed,
    50 diving; `ShellTypeRow.Diving`, set by `DesignerPlaceholders.Shell` for Type 91 AP until the shell block's
    `cap: break_away` gives it), the run carried on the hit (`Hit.WaterRunM`, measured to the ship's waterplane at its
    flotation), and no half-calibre cap. The flat sea and the straight line stay.
  - **Scaffolded, off:** `FuzeStartsOnWater`. On, a base fuze's delay runs from the water: the reach is cut to where
    it fires (`WaterRun.FuzeRunM`, L·ln(1 + v·t/L)) and the shell arms at the hull with what's left (`Terminal`). The
    pass turns it on with P3's fuzes, so the fuze, not the reach (`UnderwaterReachDecays` 3: 5% of the speed), ends an
    ordinary shell's run.
  - **Outcomes accepted, not tuned (user, 2026-10-16):** with the 3-decay reach about a quarter more hits count and
    more ships flood and sink (the squadrons' Germans lose ~0.7 ships a run, none before). Without damage control
    that's expected; tuning to historical rates now would make ships immortal once it comes.
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
    (`08-gunfire-effects.md` §8). The splash system has the miss's position already. It needs a new `Hit` source, a
    burst at a point outside the hull, which `DamageSystem` takes like any hit and `Effects` handles from its burst
    on (no terminal walk). A step of its own, after the THOR fragments.
- **E4 The battle anchors.** With S and P in, the whole chain against the battles: Jutland, Bismarck's ~3 belt holes in
  ~400 hits, South Dakota, Samar, the 1894–1905 battles, the destroyer benches. The leftover rolls are tuned only within
  their sourced bands, and the deck multiplier (P1a) only if an anchor asks.

## What stays out

- Bombs: the deck physics serves them, but bomb shapes and fuzes aren't researched yet.
- The magazines' venting race, flooding, fire spread and the service networks: their own 4.5 sessions, fed by the
  ammunition's `charge` (S0) and better burst points (P3).
- The AI choosing the shell by target (the AI's pass).
- Variance in construction quality per part (a block per part; decision 2).

## Questions for the user

None open (2026-10-16).

## Open questions (research coming)

The user will write research notes on these (2026-10-16; requested in `research/REQUESTS.md`). Neither blocks a step:
each lands where it says once its note is in.

1. **Confinement.** A burst is sized by its charge alone today (`Effects`: the same radii in a small closed room as on
   an open deck). A confined burst hits harder: pressure against the room's volume and vents, bulkheads and doors blown
   out, neighbouring spaces reached. Lands in phase E, beside the burst's other effects.
2. **Explosives as parameters.** The shell block's `filler` names an explosive and carries its `re`. A catalogue of
   explosives with their own numbers would replace that: TNT equivalence for blast, brisance for fragments,
   incendiary effect, sensitivity (bursting in the plate), low-order and failure rates with their booster, and the
   years they served. The block would then name an explosive from the catalogue (overridable), like the materials. Lands
   with S1's templates or as a step of its own after them; until then `filler.re` and the rates stand in.

## Steps at a glance

| Step | Needs | Shipgen | Sim | Moves numbers? |
|---|---|---|---|---|
| S0 | — | ammunition read, reported | compiler reads it | no |
| S1 | S0 | ammunition catalogue, templates | — | ballistics, fire control |
| S2 | S0 | default outfits | rounds by kind, the magazine order, the dev fire's kind | who fires what |
| S3 | S2 | ammunition weight | — | displacement |
| S4 | S1 | designer picker (UI session) | — | no |
| S5 | S2 | — | per-ship loadouts: scenarios, dev flag, bench | only for ships given one |
| P0 | — | materials table, upgrade | material rows | no |
| P1a | P0 | — | Thompson, pen-check bench, deck multiplier | penetration (1–4%) |
| P1b | P1a | material catalogue | Q(D) | penetration |
| P1c | P0 | densities | — | weights |
| P2 | S2, P1b | — | caps, shatter, break-up | outcomes |
| P3 | P2 | — | fuzes; rows gone; water fuze on | burst points |
| P4 | P2 | — | three limits | outcomes |
| P5 | P1a | — | angles | ricochets, paths |
| P6 | — | — | under water (done but P3's switch) | underwater hits |
| P7 | P0 | gratings, backing | scatter | plates |
| E1 | P4 | — | holed-but-broken effects, flash | damage |
| E2 | P2 | — | spall | damage |
| E3 | P0 | — | THOR fragments; then near misses (new hit source) | damage |
| E4 | all | — | tuning | the anchors |

S and P0 are independent, so P0 can come first or between S steps; P1a needs no shells. Everything after P1b needs
both phases' data.
