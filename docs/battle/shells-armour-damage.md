# Plan: shells, armour and damage (a 4.5 deepening)
Status: agreed, steps reviewed for implementation; research folded in (decisions 8-10); phase S, P0, P1a-c, P2, P3 and P6 done    Updated: 2026-10-17

The fine-tune pass for the **Terminal** link of `fighting.md` 4.5 ("penetration proper: material qualities, capped and
uncapped shells, spaced armour, edge effects; shell quality by navy and year; diving shells and underwater hits;
near-miss splinters; a ricochet table"). Its research is `research/simulation/armor/` and `damage-research/`, read
together in `research/simulation/armour-and-damage.md`, and the notes asked for this pass in `docs/battle/damage/`
(11 blast and confinement, 12 fillers, 13 fragmentation and non-penetrating hits, 14 spall, 15 near misses, 16
casualties; 17's fire, leakage and service networks belong to the other sessions). Each note has a `*_ref.py` that makes
its tables: references to check against, never a second engine. They're a source of detail where a step needs it, not
a spec to follow line by line.

## The aim

The game already gets the *formula* right: its penetration fit is within 1–4% of Thompson's (armour-and-damage.md §2).
What it lacks is the shell's *state* after each plate, the thing that decides what a hit does. Today that comes from one
roll per era once a fuze starts (`ShellOutcomes`). This pass replaces that roll with causes, in the order the user
gave:

1. **S, what's hitting:** the shell as the design's data, not a placeholder from the look's era.
2. **P, how it hits:** plates as physical materials, and the shell's state plate by plate (cap, body, fuze, burst point).
3. **E, what it does:** the new states into the effects (fragments without a burst, flash through a holed turret, spall,
   splinters).

Flooding, fire, the magazines and the service networks stay their own 4.5 sessions. This plan feeds them
better burst points and states; it doesn't rebuild them. Casualties per hit and what a plate that held does (jams, belt
leaks, shock) come into phase E (decision 8): they're `Effects`, and they read the same fragments and blast.

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
- **The research's era tables become design numbers.** Several notes tabulate by period (13's shock columns, 17's
  breaker grades and fire loads). Each column becomes a template default for one number in the design: the shock grade,
  the joints, the explosive and its booster. A WWI ship trips less because it carries little to trip, not because of
  its year.

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
   residual). The plate bench checks it against the trials before the old fit goes. **Vanilla** (user,
   2026-10-17): no deck term and no tuning knob on V_L. Its inverse is closed-form (Cardano).
6. **The loadout is the admiral's, the armour the designer's** (user, 2026-10-16: "Admiral naturally may want to change
   the ammunition loadouts of their ships even ship by ship basis, whereas they can't just change armor willy nilly").
   The design carries a **default loadout**; each ship may carry another, set outside the design (S5).
7. **Default outfits and the magazine order** (user, 2026-10-16): main batteries 70% AP and 30% HE, secondaries 30% AP
   and 70% HE. Magazines empty AP first on the main battery and HE first on the secondaries; the AI's behaviour is
   otherwise as it is. A dev toggle on the right-click fire picks the kind to shoot (S2).
8. **Casualties and the non-penetrating effects join phase E** (user, 2026-10-16). Casualties per hit (16) were
   fighting.md 4.5's "Crew" link, and jams, belt leaks and shock (13) were 4.3a's guesses; both are `Effects`, reading
   the burst's pieces and blast. The crew link keeps the rest: crew as the damage-control resource, the command node,
   posture.
9. **The explosives catalogue comes in S1** (user, 2026-10-16), not as a later step: the shell block's `filler` names
   an explosive from it from the start, so `filler.re` is never written and then migrated.
10. **This plan adds the shock grade and the joints to shipgen** (user, 2026-10-16): `fittings.shock_grade` (G0-G3)
    and `construction`'s riveted or welded shell and internals (E0), read by E4, E5 and E7 and later by the service
    networks and the flooding.

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
| `filler` | `{share: 0.024, explosive: "Shellite", booster: ce}` | the explosive by name from the catalogue below (its numbers copied in, overridable inline); the booster picks its full-detonation rate (decision 9) |
| `steel` | `{size: 1.25, speed: 1.0}` (Ni-Cr AP body) | Mott's B and Gurney's V multipliers: forged carbon 1/1, cast iron 0.6/0.8, cast steel 0.85/0.95, HF-1 0.65/1 (13 §3.2) |
| `casing` | `{nose: 0.33, ogive: 0, body: 0.55, plug: 0.08, tail: 0.04}` | how the shell breaks: the nose slug, the ogive's spray (HE 0.20), the side spray, the base plug (13 §2.5) |
| `fuze` | `{position: base, delay_s: 0.025, start_cal: 0.07, oblique: 0.25, plates: single, scatter: 0.25, blind: 0.05, water_ms: 0.5, water_fires: 1.0}` | start threshold, burst point, duds; on the water, how fast a nose fuze fires (the surface burst's depth) and whether it fires at all (15 §6.2) |

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

- **Mass = `mass_k_kg_m3` × D³.** Within one navy's generation of shells, W/D³ is nearly flat from about 8″ up
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
- **One battery's own shell:** `mass_kg` on the battery overrides `mass_k_kg_m3` (the 6″/47's 59 kg super-heavy, a Nelson
  shell).

So a navy and year has two or three blocks (heavy AP, heavy HE or common, light guns), and a design picks one per kind
for each battery. That's the same count as its plant templates.

**The explosives catalogue** (decision 9; 12 §4.1, 11 §2.4, 13 §2.1 and §3.1). A built-in table keyed by name, like
the materials, written out to `Content/Templates/explosives.json` and `docs/shipgen/explosive-templates.md` by the tool.
The eleven fillers of 12 (black powder, wet guncotton, lyddite, Shimose, TNT, TNT/wax, Explosive D, Shellite, TNA,
amatol 60/40 and 80/20), plus RDX/TNT and Comp B for late HE. Each row (TNT as the example):

| Field | TNT | Used for | Source |
|---|---|---|---|
| `re` | 1.00 | the shock: W = filler kg × re (`Effects`; was `FillerTnt`) | 12 (armor/04 where sourced) |
| `q_det`, `q_comb`, `air_m3_kg`, `eta` | 4.5, 15.0, 2.66, 1.0 MJ/kg | the gas pressure in a closed space and its afterburn (E5) | 11 §2.4 |
| `gurney_ms` | 2,438 | the fragments' speed (E1) | 13 §2.1 |
| `brisance` | 1.00 | B_TNT / B_Mott: the fragments' size, mean mass ∝ 1/brisance² (E1) | 13 §3.1 |
| `in_plate` | 0.10 | bursting in the plate, at the limit (P3) | 12 |
| `full` | `{basic: 0.60, ce: 0.95}` | full detonation by booster; the rest low-order 75%, dud 25% (P3) | 12 |
| `fire` | 0.25 | the fire start's multiplier | 12 |
| `from` | 1902 | a template's label; the tech tree filters by it, the sim never reads it | 12 §6 |

- **Reconcile at S1.** 11 and 12 disagree on four `re` values (black powder 0.40 against 0.55, lyddite 1.12 against
  1.17, Explosive D 0.90 against 0.95, Shellite 1.05 against 1.00). Take 12's (armor/04 sourced) for `re`, 11's heats
  for the gas: black powder's weakness in a closed space then comes from its heats and η, not from a second RE.
- **Unsourced:** Explosive D's and TNA's `re`, and every Gurney constant but TNT's, Comp B's and tetryl's (scaled by
  detonation velocity). Marked in the templates' notes.
- **Not in the catalogue:** storage hazards, dud drift with age and Shimose's bore prematures (12 §4, hidden stats):
  a campaign's, later. Underwater fillers (Torpex, Minol) belong to the torpedoes.

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
| `density_t_per_m3` | 7.85 | weight | armor/03 §7 |
| `q` | 1.01 | thickness multiplier, STS = 1 at 8″ | armor/00 §4.2 |
| `q_scale` | 0.03 | Q(D) = q·(D/8″)^−k above 8″ | armor/00 §4.2 notes |
| `face_hardened` | `{share: 0.41, min_mm: 100}` | shatter, always decaps, always starts a fuze; homogeneous below min | armor/00 §4.2, 03 §5 |
| `elongation_pct` | 18 (Wotan Hart) | Okun's penalty for homogeneous plate against shells over 8″ | armor/00 §4.2 |
| `backing` | 0 (teak 0.01, cement 0.04) | weight-only layers that count little | armor/05 §5 |
| `fragments` | `mild` / `ductile` / `fh` / `brittle` | THOR's constants (phase E); `brittle` for compound, cast and high-hardness plate | armor/01 §3.10, 14 §2.1 |
| `back` | `{spall_u: 0.85, holed_u: 0.94, big: 0.5}` | where spall starts and the plate is holed, in u = V/V_L, and the big piece's share (P4, E3); defaults by `fragments` class: fh 0.85/0.94, ductile and mild 0.90/1.00, brittle 0.75/0.95 (big 0.3) | 14 §2.1 [EST], armor/00 §1 |

Old designs (shipped, fuzz, scenario and the players' saves) are upgraded by `DesignFormat` (version 2): every name the
catalogue knows becomes its block. A name nobody knows gets the steel block and a warning in the report, never an error
(DECISIONS.md: the generator is permissive; a missing anchor falls back with a warning).

Two more armour inputs:

- **`armour.gratings`**: the armour grating over an uptake's opening, as a share of the deck (default about 0.4: a 15″
  holed grating stops what 6″ of solid plate does, O.S. 751). The open gratings design input (memory) gets its first
  number. Today the tracer makes a grating as thick as the deck (`HitPath.cs`).
- **`armour.backing`** per belt: `{material, mm}` (Littorio's oak, US Class A's 2″ of concrete, the ironclads' teak).
  Weight, a near-zero plate in the path, and what catches spall (14 §4.3: a backing of the scab's mass per area takes
  the flakes, twice that the big piece too).

### Hull and fittings (phase E, decision 10)

- **`construction.shell` and `construction.internals`:** `riveted` or `welded`. The hull templates already name them
  ("riveted shell, welded internals", 1933-42), but only `join_factor` carries it. Used by the blast (riveted
  boundaries ×0.85, 11 §3.2), the belt's seep (welded skin ×0.6, 13 §2.3), and the flooding session's seam leak
  (welded ×0.4, 17 leakage §1.4).
- **`fittings.shock_grade`:** G0 unsecured, G1 secured, G2 shock-tested, G3 post-war (17 service §2.2). Templates by
  navy and year (G0 to 1940, RN late 1940 and USN 1942 G1, USN 1943+ G2). One number for 13's armour shock, 15's near
  misses and later the service networks' breakers. The block leaves room for the systems session's lock-in,
  selectivity and LVR.
- Weight: none in v0 (G2's +1-2% of electrical weight is flagged, not built).

**Sim tables, not design** (`settings.json`, until the designer emits them): cover by room kind (16 §4.1), vents by
room kind (11 §5.3: a boiler room's share of its funnel, a casemate's ports), parts' pressure classes (11 §4.3) and
fragility in joules (14 §5.2), the turret outcome table (13 §1.4), shock rates by zone and grade (13 §3.4).

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
- **`docs/shipgen/explosive-templates.md`** and `explosives.json`: the explosives catalogue above. A shell template names
  its explosive and booster.
- **`armour.json`** stays the *scheme* templates (part → material name). Applying one adds the blocks it names to the
  design's table.
- The designer's template column (`DesignerScene.Layout.cs`: Plant, Hull, Crew, Armour) gains **Shells** and
  **Materials**. The material knobs (`Knobs.SchemeMaterial` and its kin) list the design's table and the catalogue
  instead of `Templates.MaterialNames`. UI work follows the designer's way (memory: one tab a session, mockup then port).

## Where to start

**S0**, then down phase S; P0 and E0 are parity steps that can slot in anywhere (Steps at a glance has the order each
step needs). Each step reads only its own section here plus the research it cites; a step's section gains its benches
before and after when it's done. Today's code for the three phases: `DesignerPlaceholders.Shell`, `ShellMix` and
`Fires` (`Fleetwright.Sim.Ships`), `Terminal.cs` (`ShellOutcomes` by `ShellQuality`, `ArmourQuality`), `Effects.cs`,
`WaterRun.cs`, and their settings in `Content/Sim/settings.json`.

## Rules for every step

- **Benches before and after,** in the step's commit message and its section here: the step's own benches, plus
  `shipgen hits`, the matchup bench and the squadrons' scenario for any step that moves outcomes.
- **The matchup mirrors the terminal.** `Matchup.cs` estimates outcomes analytically from the same facts (`PenMm`,
  `ShellOutcomes`). Every step that changes `Terminal` changes the matchup's estimate in the same commit, so the AI's
  judgement never reads a different model from the one that fights.
- **Test bands are the anchors.** A step that moves a `DamageTests` anchor out of its band reports it; the band is
  widened only with the user's say, as P6's outcomes were (user, 2026-10-16: accepted, not tuned, until damage control).
- **No allocation per hit,** and no new per-hit cost worth measuring (about 200 µs a hit today).
- **More sinkings than history are fine** (user, 2026-10-16: "first we break stuff then we try to desperately fix
  stuff"). Damage control comes after this plan (`09_damage_control.md`, `10_damage_control_holistic.md`), so the ships
  here fight without sailors patching them. Don't tune flooding, fire or losses to the historical rates: a ship tuned to
  history now becomes too sturdy once damage control is in. Keep every number physical and sourced, report the losses,
  and leave the record's survival rates for after damage control.

## Phase S: what's hitting

- **S0 Shells as design data (parity).**
  - Shipgen reads `ammunition` and the batteries' `shells`, validates them, and writes the resolved shells to the
    report. The compiler reads them instead of `DesignerPlaceholders.Shell` and `ShellMix`.
  - A tool writes every shipped and scenario design's ammunition *from today's placeholder*, so every number holds:
    - each battery's `mass_kg` per entry (the placeholder's k varies with calibre, so one block's k can't hold it yet),
      `muzzle_velocity_ms`, the form factor, filler shares and fuze delays, and the Type 91's `break_away` cap;
    - the filler's explosive as an inline block with `re` 1 (today's `FillerTnt` for every explosive), until S1 names
      the catalogue's;
    - **a transitional `outcomes` field** naming today's `ShellOutcomes` row (`rn1916`, `km1916`, `rn1918`, `ww2`,
      `ijn_t91`), until P3 replaces the rows with the fuze's and filler's rates and deletes it.
  - Checks: the sim's hashes, `shipgen ballistics`, `duel` and `weapons` unchanged; the goldens gain the new keys only
    (`golden-update`, no value moves).
  - The magazine weight keeps `ShellK` in this step.
  - **Done 2026-10-16.** What it came to:
    - Shipgen: `Ammunition.cs` validates the table and the outfits and resolves each battery's shells into the report
      (`report.shells`: kind, mass, muzzle velocity, form factor, cap, filler kg and `re`, fuze, outcome row). The
      schema is S0's part of the block: `kind`, `mass_k_kg_m3`, `form_factor`, `cap`, `filler {share, explosive {re}}`,
      `fuze {position, delay_s}`, `outcomes`; the rest of the table above comes with S1. `docs/shipgen/README.md` has
      the keys.
    - **The placeholder stays as the fallback** for a battery that gives no `shells` (the fuzz mutants, `-new`, players'
      saves, a battery added in the designer): the compiler fills it from `DesignerPlaceholders.Shell` and `ShellMix`
      as before, the way `DesignerPlaceholders.Fill` fills the motion facts. Shipgen stays free of the look's era.
      S1's default template takes the fallback and deletes them.
    - `muzzle_velocity_ms` isn't written: the default (`Ammunition.DefaultMuzzleVelocity`, the placeholder's 280 + 11·L,
      which the placeholder now calls) gives the same, and it follows the gun's length in the designer.
    - **The designer:** a calibre change drops the battery's `shells` (`Knobs.Calibre`), since its written masses
      belong to the old gun; it falls back to the placeholder, as before S0.
    - `Fires` reads the outfit (AP first from a heavy gun, else HE, else common): the same choices as before.
    - Blocks are named `<kind> <era> <row>` ("AP great_war rn1916"), so the shell type ids (`305/45 AP great_war
      rn1916`) and the layout hash didn't move. 123 designs written (70 shipped, 53 scenario; the escort carrier and the
      tanker have no guns); `shipgen ammo-parity` compiled each before and after and found every shell, share and fired
      type identical. `WeaponFactsTests.Shells_from_the_design_are_the_placeholders` holds it until S1.
    - **Benches, before and after:** `weapons`, `ballistics` (dreadnought, yamato, bismarck, destroyer, seydlitz, lion,
      iron_duke, v25, warrior, queen_mary), `duel` (super_dreadnought, lion), `hits` (v25 by lion, iron_duke by
      seydlitz), `matchup` (arethusa:4 against iron_duke, lion against seydlitz): identical. The scenarios' fights
      (squadrons, flotilla, tsushima --play two seeds) identical shot for shot. **The state hash moved** (squadrons
      F8B07AA06799665A to FD771774D00B565F, flotilla D5EBC2B65B4F4E10 to BF27290A7C2ABF71): `ShipSystem.Hash` folds in
      each class's design hash, and the designs' JSON changed. With that term left out, squadrons, flotilla and 20
      minutes of Jutland hash the same before and after.
    - Goldens: 70 cases gained `ammunition`, `shells` and `report.shells`, nothing else (golden-update, ShipDesign.Version
      7). 372 of 372 match.
- **S1 The templates.** The catalogue's ammunition, `shell-templates.md` and `shells.json`; the designs' blocks are
  switched to the templates of their navy and year (Dreadnought gets the RN's 1906 APC, Bismarck the 38 cm L/4,4 with
  the Bd.Z. 38, Yamato the Type 91), and the parity `mass_kg` overrides go.
  - This moves numbers: masses (k from 0.42–0.66 lb/in³ instead of one curve), the drag form factor, filler. The
    `outcomes` field stays as S0 set it.
  - **The explosives catalogue** (decision 9) and `explosive-templates.md`; each template names its explosive and
    booster. `Effects` sizes every burst by W = filler kg × `FillerTnt`, which is 1 for every explosive today; the
    explosive's `re` replaces it (black powder 0.55, lyddite 1.17, TNT 1, armor/04 §2.1), so a Victorian powder-filled
    common shell bursts smaller than its kilograms say. `FillerTnt` goes.
  - **The new fields ride along unread:** `steel`, `casing`, the heats, Gurney and brisance, `fuze.water_ms`. E1, E5
    and E7 read them; S1 only writes them, so they move nothing yet.
  - **What S0 left for S1 to end:** the compiler's fallback to `DesignerPlaceholders.Shell` and `ShellMix` (a battery
    without shells gets the default template instead, and both go), `shipgen ammo-parity`, the designer dropping a
    battery's shells on a calibre change (`Knobs.Calibre`: blocks scale with the calibre once `mass_kg` goes), and
    `WeaponFactsTests.Shells_from_the_design_are_the_placeholders`.
  - **Done 2026-10-16.** What it came to:
    - **The catalogues:** `ShellTemplates.cs` (83 templates in 26 families, Palliser 1867 to the post-war dual-purpose
      guns) and `Explosives.cs` (the 13 explosives), C# tables in shipgen; `shipgen templates` writes
      `Content/Templates/shells.json` and `explosives.json`, `docs/shipgen/shell-templates.md` and
      `explosive-templates.md`. Every template's note gives its sources; [EST] marks what no source gives (most of the
      French, Italian and Russian blocks, many fillers of the light guns, every cap share but the sourced ones).
    - **The schema** is the whole block of the table above; `explosives` is a design-level table like `ammunition`, and
      a filler names one (or gives a block overriding the named one's numbers). The new fields ride along unread.
    - **The default** (user, 2026-10-16): the **1912 standard** family, main 70% AP / 30% HE, secondary 30 / 70, for a
      battery with no shells (new designs, the fuzz mutants, saves). "We'll add a selector in ship designer so that one
      can toss in what they want" (S4), and a scenario may override, the loadout being the Admiralty's decision (S5).
    - **The mapping** (user, approved as proposed): `shipgen ammo-templates` (one-off) switched the 123 designs, each
      battery keeping its outfit's kinds and shares, each kind taking its family's block (a gun under 200 mm the
      family's light block where it has one):
      - 1867 chilled iron: devastation, inflexible, duilio. 1895 forged steel: doom. RU 1895: borodino, aurora, buyny,
        oslyabya. JP 1904: mikasa, izumo, ikazuchi. US 1906: connecticut. DE 1902: armoured_cruiser, hessen,
        deutschland_1906. FR 1893: babel, bouvet, brennus.
      - RN 1906: dreadnought, invincible, lord_nelson. RN 1909 (12" and under) or RN 1912 (13.5" and up), the light
        guns RN 1906's: every RN Great War ship else (lion, super_dreadnought, tiger_1914, erebus, light_cruiser,
        tb_destroyer and the 29 Jutland ships). DE 1911: seydlitz, karlsruhe, nassau, nassau_casemates (1909 ships in
        a WWII look) and the 16 Jutland Germans. FR 1910: courbet, danton. 1912 standard: battlecruiser, dante,
        tegetthoff, tramp_1914, engadine. US 1916: pennsylvania; US 1921: colorado (its 5" US 1916's). JP 1913: kongo.
      - RN 1927: kent, nelson. JP 1925: nagato, fubuki. RU 1911: gangut. RN 1938: kgv, victory_1944. DE 1939: bismarck,
        deutschland (its 28 cm at the Bismarck generation's 0.53: 330 kg, not the C/28's 300). IT 1940: littorio, zara.
        FR 1936: dunkerque, richelieu. US 1941: the generic WWII designs. JP 1931: yamato. 1950 dual-purpose:
        akizuki_1960, colbert, daring, hamburg, impetuoso, sverdlov, tiger.
    - **The outcome rows** (user, 2026-10-16): the template family's row, not S0's. 14 designs changed row: colorado,
      connecticut, hessen, deutschland_1906, nassau, nassau_casemates to km1916; deutschland, dunkerque, zara,
      victory_1944, akizuki_1960 to ww2; gangut, nagato, fubuki to rn1918 (S0 had given the last two the Type 91's
      row, and its diving, years early).
    - The sim: `ShellTypeRow.FillerRe` (the explosive's `re`) sizes the burst; `FillerTnt` left the settings.
      `DesignerPlaceholders.Shell` and `ShellMix` are deleted; the compiler reads only the report. A light block is
      flagged as such in the catalogue (`Template.Light`).
    - **Benches, S0 to S1:**
      - The shells land on the sourced ones: Dreadnought's 12" APC 384.7 kg (386), Yamato's Type 91 1,455 kg (1,460)
        and Type 0 HE filler 61.9 kg (61.7). The main battery's fired shell by family: RN Great War -7% to +5%, DE 1911
        -7% to +6%, the Victorian Russian, German and French -9% to -22%, Palliser about -20%, Dreadnought -9%,
        Yamato -10%, Colorado -11%, Bismarck -9%; the generic WWII designs up to +22% (US super-heavy), the post-war
        dual-purpose guns about +25%.
      - Fuzes: the RN's pre-1918 No. 16, the USN's pre-1920s base fuze and the early French ones are non-delay
        (~0.003 s) where the placeholder had 0.025 s, so their shells burst sooner. Fillers by the sources: the RN
        CPC 9.4% powder at re 0.55, the lyddite HE 12.5% at 1.17, against the placeholder's 6-7% at 1.
      - `duel`: super_dreadnought 4.7 to 4.8% hits, lion 5.0 to 5.1%; first hits unchanged. `ballistics` follows the
        masses.
      - `hits` v25 by lion's 4" at 6 km, one hit: crew 0.9 to 1.1, holes 0.056 to 0.072 m²; iron_duke by seydlitz at
        14 km: water 86 to 92 t, magazines 0.4 to 0.6%.
      - `matchup` (it reads mass, not filler): arethusa:4 against iron_duke 1.98 to 1.92, lion against seydlitz 0.99
        to 1.07.
      - Squadrons (40 min, one seed): Britain lost 4 by magazines in S0, 2 now, and Germany 1 (Von der Tann). Flotilla:
        a hit or two either way. Tsushima --play: Russia lost 4 and 4 of 13 (seeds 1905, 1906), now 6 and 8; Japan 2
        and 4, now 3 and 2.
      - `ShadowTests` now hold fire (the shadower could be sunk mid-test; the test is about station keeping).
      - **DamageTests anchor** (10 hits of a 4" HE on a destroyer, fighting.md 4.3b): tb_destroyer lost 10% to 17%,
        all of it magazines (lyddite's burst ~46% bigger than the placeholder's); the fleet destroyer 0 to 2%. **Band
        widened to 0-20%** (user, 2026-10-16): "we'll do tuning of the actual hull generator later. Right now we have
        just a single layer of steel and magazine straight behind it. We'll have better protection schemes later so
        high casualty figures are to be expected."
    - Goldens: the designs' shells and `report.shells` only (the fuzz cases gained the default outfit); nothing else
      moved. ShipDesign.Version 8.
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
  - **Done 2026-10-16.** What it came to:
    - **Outfits:** `shipgen ammo-templates --outfits` wrote decision 7's outfit into the 123 designs from each design's
      family. Where a family has no HE (the ones before HE shells: 1867, 1895, RU 1895, JP 1904, US 1906, FR 1893) its
      common shell takes HE's place, the era's explosive shell; where it has no AP (1950 dual-purpose) its SAP takes AP's.
      A light gun of a family without a light AP block takes the family's AP (the 12-pounders' soft-capped APC).
    - **The order** (`MagazineOrder` in Sim.Ships): main AP, common, HE; secondary HE, common, AP. The compiler keeps each
      battery's loads in it, so `Fires` is the lead, the first load. `DesignerPlaceholders.Fires` and `HeavyGunMm` are
      gone.
    - **Rounds by kind:** the class splits each mount's rounds over its battery's loads (largest remainder;
      `ShipClass.LoadRounds`, `MountLoadStart`), CombatStore keeps each ship's own copy, hashed. `MountState.Rounds` stays
      the total for the AI and the matchup. A mount fires the first load it has rounds of; each gun is laid with its own
      shell's table, so a mount out of AP fires HE in the same salvo, and the battery's computer works in its lead's
      table (the first firing mount's). The tables for every load are built when the class enters the world.
    - **The dev toggle:** the strip's Fire row picks the magazine order or a kind the battery carries, with its rounds
      left, and says "no AP left"; `ShellSystem.FireAt` takes the kind (the command's X, kind + 1: 0 is the order, as
      every old log), and `-fire=ship[:battery][:kind]:lat,lon`. The dev salvo now spends its rounds (no reload).
    - **Shell ids:** a template's muzzle velocity follows the outfit's heaviest shell, so the same id on the same gun can
      leave faster in another class (Colorado's 5" beside a heavier AP than Pennsylvania's); the shared-id test compares
      all but the velocity. Ballistic tables are keyed by value, not id.
    - **Tests:** the flotilla replay-and-allocation test pinned to seed 5 (it needs an attack made: over seeds 1-8 the
      boats got in 7 times in S2, 6 in S1; the scenario's own 9 now doesn't); its replay with the same seed; zero
      allocations held with the per-mount tables.
    - **Benches, S1 to S2:**
      - **Light cruisers on destroyers (the expected move):** a 6" main battery now fires AP first and it passes through.
        `hits` v25 by Arethusa at 6 km: crew 2.8 to 0.1 a hit, fire 43% to 1%; tb_destroyer by v25's 88 mm: crew 0.8
        to 0.1, out of the fight after 5 hits 17% to 6%. `matchup` arethusa:4 against v25:6 3.46 to 1.68, town_chatham
        against v25:3 1.13 to 0.51; arethusa:4 against iron_duke 1.92 to 2.01, lion against seydlitz unchanged. The
        shell by target is the AI's pass (decision 3); until then light cruisers fight destroyers poorly.
      - `duel` unchanged (4.8%, 5.1%). `weapons` shows the new outfits in the order.
      - Squadrons (40 min): Britain lost 2 (Princess Royal, then Queen Mary rather than Lion), Germany none (Von der
        Tann survives). Flotilla: S51 2 hits. Tsushima --play (4 seeds): Russia lost 6/8/4/4, now 4/8/6/8; Japan
        3/2/4/3, now 5/4/4/3.
    - Goldens: the designs' shells only (70 cases); ShipDesign.Version 9.
- **S3 Ammunition weight.** The magazines weighed from the default outfit's mean shell mass (the charge stays in
  `AmmoMult`). Report the displacement shifts,
  run `golden-update` with a `ShipDesign.Version` bump and `mono-check`.
  - **Done 2026-10-16.** What it came to:
    - `ShipDesign.Build` stamps each gun battery with its outfit's mean shell mass (`Ammunition.WithShellMasses`, the
      shares by the masses `Resolve` gives; `BatteryInput.ShellKg`, not serialized); the turret type carries it
      (`TurretType.ShellKg`) as it carries `rounds_per_gun`, and `Batteries.MountWeights` and the ready-use rounds weigh
      it times `AmmoMult`. A mount with no outfit (AA guns) keeps `ShellK`'s curve.
    - **Displacement:** every shipped design lighter, median -1.5% standard: victory_1944 2,261 -> 2,141 t (-5.3%), doom
      -4.7%, duilio -3.6%, brennus -3.4%, borodino -2.9%, colorado -2.8%, devastation -2.8%, armoured_cruiser -2.7%;
      the least moved the merchants and carriers (-0.1 to -0.6%). `ShellK` was the 16" Mk 8's super-heavy W/D³, the top
      of the range, so nearly every real shell is lighter (Bismarck's 38 cm 1,004 -> 805 kg). The fuzz mutants: median
      -1.5%, from -7.0%; one valid mutant grows by a sizing jump (fuzz_lim_014 +5.8%, its hull 256 -> 269 m), and one
      invalid build (fuzz_lim_139, out of trim before and after) jumps 76 -> 108 m.
    - **mono-check:** 27 reversals and 15 lumps before, 35 and 13 after. The big ones are the same discontinuities moved
      by the lighter weights (Kongo's 17 m hull jump on main_rounds 130->140 is now on main_calibre 326->336 as a
      -5.8% reversal; the dreadnought's freeboard lump and the fleet carrier's AA lumps unchanged); the added reversals
      are small (range +3, freeboard +2, stress, tower and AA light +1 each, mostly under 1.5%). The sizer's lumpiness is
      the monotone rework's (memory: monotone sizing rework), not this step's.
    - **MagazineBlastTests** (user, 2026-10-16): Bismarck's aft magazine 321 -> 258 t, its fireball's M 21.3 -> 17.1 t
      against vidgen's 22 t: `AmmoMult` counts the round's charge as 0.6 of its shell, so a lighter shell took its
      propellant with it although a gun's charge doesn't depend on it. The band widened to 0.75-1.15 of 22 t (the
      fireball 140-170 m and λ 65-80 m with it) until the ammunition's `charge` gives the propellant as rounds × charge.
    - **FlotillaAttackTests** pinned to seed 1 (seed 7 no longer gets in with the ships' sizes moved; 7 of seeds 1-12
      do, "about half the time" as its comment says).
    - Squadrons (40 min): Britain lost 2 (Princess Royal, Lion), Germany none. Goldens: 315 design, 311 SVG and 69
      sprite.json cases (shipgen/golden/README.md), ShipDesign.Version 10.
- **S4 The designer.** A shell template picker in the Main battery and Secondary tabs (the template column); the
  battery table shows the kind, mass and fuze. A UI session of its own (mockup, then port); it can run any time after
  S1, alongside phase P.
  - **Done 2026-10-16** (`docs/designer-ui/shells.md`, the mockup and the user's answers):
    - **The SHELLS column** takes the material column's place in both tabs (the user's suggestion). The turrets' and
      mounts' material is hidden at the scheme's default ("we'll come back to it later, shells are more important").
    - **The cell** shows the loads as kind chips with their shares, plus the family. Its hover shows the shells as
      fired: block, mass, velocity, fuze and rounds a gun.
    - **The picker:**
      - the family, a tree with the design's navy first and her era marked;
      - a row per load in the magazine order: the block (the same tree, with each block's mass at this calibre and
        its fuze), the share in 5% steps, rounds, mass, m/s, filler and fuze;
      - + a kind, × a load, and the default outfit.
    - **The Templates panel** gains Shells, which sets every battery's family.
    - **One implementation:**
      - The edits (`ShellEdits`) copy the picked templates into the design's `ammunition` and drop the blocks nobody
        names.
      - The read-outs are shipgen's `Ammunition.ResolveBattery`, factored out of `Resolve`.
      - The rounds are `Loadout.Split`, factored out of the class's split.
    - **Tests:** `ShellEditsTests`. Every edit on every shipped design's batteries leaves it valid, its shares at 1 and
      its ammunition exactly what it names; plus the light-block rule, shares, the default outfit written out, and
      the tree's order.
    - Checked by clicks (`-input`):
      - the cell opens the picker;
      - AP's share ▶ makes 75/25, and "+ common" makes 60/20/20;
      - the block tree's RN 1912 APC makes AP 448 kg, so the lighter HE leaves at 805 m/s rather than 775, and the
        family reads "mixed";
      - the Templates row's RN 1912 sets both batteries.
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
  - **Done 2026-10-16.** What it came to:
    - **A loadout is the design with other shells, resolved by shipgen** (one implementation): `Ammunition.WithLoadout`
      puts the given batteries' `shells` into a copy of the design (a battery is `main`, `secondary`, `main[i]` or
      `secondary[i]`; an ammo a template's name from `ShellTemplates`, a block of the design's, a block given with the
      loadout, or a kind, `ap`, `common` or `he`, for the battery's own block of it), validates it, and
      `Ammunition.Resolve(loaded, gun: design)` resolves it with each gun's velocity anchored on the design's own
      heaviest shell, so a heavier shell leaves slower by the fourth-root rule (the dreadnought's 12" firing the US
      1941 Mk 8 block: heavier, slower). The catalogue is read from `ShellTemplates.cs`, the source `shells.json` is
      written from, rather than the JSON.
    - **`Loadout`** (Sim.Ships) holds what the class held: the shell types and ids, each battery's loads in the magazine
      order and its lead, each mount's rounds by load. `ShipClass.Loadout` is the design's own; `ShipClass.Load(spec)`
      makes another (the class keeps its design for it). `BatteryRow.Shells` and `Fires` and the class's shell tables
      are gone, so every reader chose: the sim reads the ship's (`CombatStore.Loadout(slot)`: gunnery, the dev's fire,
      the shells in flight, which carry the loadout they were fired from, the AI's reach for fire at will, the snapshot
      and the strip's Fire row), the tools and the magazine blast's look the class's own.
    - **The matchup:** an own ship is judged with her loadout (`Matchups.Profile(world, loadout)`); every other ship by
      her class's own, since nobody sees another navy's magazines. `Warm` profiles the loadouts given.
    - **Where it's set:** `ShipClassRegistry.AddLoadout` gives an id (the same loadout of a class, the same id) and
      `ShipSystem.Load(entity, id)` fills her magazines with it, only while she's fired nothing (until the campaign's
      ports); refused otherwise. A scenario's unit (for every ship below it) or ship entry takes `"loadout"`; one that
      doesn't fit is a warning and the design's shells stay. The dev flag is
      `-loadout=ship:battery:shell[@share][+shell...]` with `|` between batteries (template names hold commas, so
      the plan's `name[:share],...` couldn't carry them), and `shipgen hits --loadout` takes the same.
    - **The hydrostatics stay on the design's weight** (as planned, flagged): a loadout doesn't reweigh the magazines.
    - **Hash:** a ship of her design's own hashes as before; a given loadout's hash is folded in per ship.
    - **Benches, S3 to S5:** squadrons (40 min) 51158595913FDF99 and flotilla (30 min) 89DDB801FA11445B before and
      after; the layout hashes unchanged. `hits` v25 by Lion's 13.5" at 8 km, the design's APC against `main:he`:
      passed through 85.5% to 0.1% (burst inside 1.4% to 99.9%), crew 0.3 to 16.2 a hit, fire 1% to 70%, holes 0.26 to
      0.87 m², magazines 0 to 2.3%.
    - Tests: `LoadoutTests` (a kind, a template and shares; the fourth-root velocity; errors; a ship fires her loadout
      and refuses another after firing; a scenario unit's loadout and a ship entry's over it).
- **Not here: the shell by target** (decided). A battery fires by the magazine order (S2). This plan gives
  every battery its outfit of kinds and every ship its loadout; firing HE or common at unarmoured targets is the AI's
  pass (the matchup bench showed it matters: four light cruisers against a battleship).

## Phase P: how it hits

- **P0 Materials as data (parity; done 2026-10-17).**
  - The `materials` table in the design, the `DesignFormat` upgrade, the catalogue's blocks with `q` set to today's
    `ArmourQuality` values, and the compiler's material rows. `ArmourQuality` leaves the settings.
  - Mass still 7.85 in this step. Nothing moves: hashes and golden values hold (the goldens gain the table).
  - **How (2026-10-17), after the explosives catalogue (S1):**
    - **The block, `MaterialInput`:** `density_t_per_m3` (machinery.tech's name for it) and `q` only, keyed by name.
      `q` is read from this step; the density
      is the true one (armor/03 §7: 7.85 for the steels, wrought iron 7.7, compound 7.75, wood 0.7, aluminium alloy
      2.7) and is read from P1c on, so P1c doesn't rewrite the designs. Each other field in the Materials table above
      comes with the step that reads it (P1b `q_scale`, `elongation_pct` and `face_hardened`, P4 and E3 `back`, E1
      `fragments`, P7 `backing`): a new optional key needs no format step.
    - **The catalogue, `MaterialTemplates.cs`** (shipgen, like `Explosives.cs`): the 26 names the designs use and
      `aluminium alloy`, `q` as `ArmourQuality` had it, `group` `structure` (the plating's and the superstructure's
      picker) or `armour` (the armour picker lists every material: wood armour is silly, not impossible).
      `shipgen templates` writes `Content/Templates/materials.json` (values now blocks) and
      `docs/shipgen/material-templates.md`; `tools/designer_templates.py` stops writing materials.json.
    - **Parity keeps today's oddities**, for P1b to fix: the names `ArmourQuality` didn't list took 0.6, so
      `special treatment steel`, `Wotan`, `Wotan hart` (Bismarck's decks), `Krupp non-cemented` and `Molybdenum steel`
      are 0.6 here; a plate with no material named stays 1 (the class's -1).
    - **Resolution:** a name takes the design's block, else the catalogue's; a name in neither takes the `steel`
      block with a warning (today's 0.6 catch-all becomes steel's 0.55; no design has one). `report.materials` is the
      resolved block of every name the design's parts give, sorted. Validation: the blocks' numbers (`q` and density
      more than 0) and unknown fields.
    - **Format 2:** `DesignFormat`'s step from 1 adds `materials` with the catalogue's block for each known name the
      file's parts give (any `material` or `deck_material`, and `armour.materials`' values), unless it has one.
      `shipgen upgrade FILES` rewrites design files to the current format in their own layout (the shipped designs'
      lines, the scenarios' indented files): the shipped and scenario designs go to 2, the fuzz cases stay old and
      take the step when read.
    - **The sim:** the compiler gives each material row its `q` from `report.materials` (strict: a name the hitboxes
      carry without a block is a compile error); `ShipClass.MaterialQ` replaces `Terminal.Qualities` and
      `DamageSystem`'s cache; `ArmourQuality` and `ArmourQualityOther` leave `SimSettings` and settings.json. The
      structure hash keeps the names only, so it holds.
    - **Checks:** golden-check after golden-update (report.materials and the inputs' table only, `ShipDesign.Version`
      19); `shipgen hits` (v25 by lion, iron_duke by seydlitz), the matchup (arethusa:4 v iron_duke) and the
      squadrons and flotilla identical; the state hash moves only by the design hashes (as S0).
  - **Done (2026-10-17).** As above, and:
    - **One change outside the plan:** two armour decks on one deck number merged into one plate named `"A + B"`,
      a name with no block. The merged plate now takes the thicker one's material (no case had a merge of two
      materials, so nothing moved).
    - `shipgen upgrade` took the 72 shipped and 53 scenario designs to format 2; the fuzz cases stay at 0 and take
      both steps when read. The designer's material pickers read the catalogue (the armour picker gains wood and
      aluminium alloy); the material knobs still list names, and don't copy a block into the design (the catalogue
      stands in) until the UI session that lists the design's table.
    - **Benches before and after:** goldens 326 cases gain the inputs' table, the version and report.materials
      (golden-update, `ShipDesign.Version` 19), 372/372; `snapshot` of every case identical beyond those. `hits` v25
      by lion and iron_duke by seydlitz, `matchup` arethusa:4 v iron_duke and lion v seydlitz, `duel`
      super_dreadnought: identical but for timings. Squadrons (40 min) and flotilla (30 min) identical shot for
      shot; the state hash moved (squadrons 27C3077B -> 4A00F99D, flotilla A5834BAF -> 23A9E144) only by the design
      hashes: with that term left out both hash the same before and after (BB76AB02, 39392167).
- **P1a Thompson and the plate bench** (sim only; the materials' `q` as P0 left them; done 2026-10-17).
  - `Terminal`'s plate step on Thompson's V_L, the residual from V and V_L (Lambert-Jonas since 2026-10-17, "How"
    below), and the inverse (Cardano, below) for `Matchup.cs` and the bench's readouts.
  - **The plate-only bench** (`shipgen pen-check`): the game's `Terminal` plate step against `armor/07-*.csv` (the USN
    16″ table, NPG 1946–47, the Italian obliquity tables, Annapolis, Indian Head, Beardmore, Baden), and both models'
    decks beside Thompson's (for the record; nothing reads them). No rolls. It reads the CSVs from the repo, as a tool may; nothing the release needs.
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
    - **The inverse is closed-form (Cardano, 2026-10-17).** It answers how thick a plate a shell beats at a range.
      With `u = √T` (inches), V_L is a cubic in u: `a·u³ + b·u = c`, where `a = 6·(Ob² + 2000)/D`,
      `b = 34600 − 2.7·Ob²` and `c = V·cos Ob·√W / (0.024056·D)`. Both a and b are positive at every angle the
      formula takes (b down to 19,412 at 75°), so the left side rises with u and there is exactly one real root.
      With `p = b/a` and `q = c/a` it is `u = 2·√(p/3)·sinh(⅓·asinh((3q/2p)·√(3/p)))`, then `T = u²`. That is
      Cardano's one-real-root case in its hyperbolic form; the textbook form of two cube roots cancels badly here,
      because the linear term dominates (the cubic's share is about 0.35·T/D at normal). The cost is one sqrt,
      asinh and sinh, about the price of one plate step, with no iteration and no bracket. Only readouts want it:
      the matchup's profiles (`Matchup.cs`), `shipgen matchup`, and the designer's immune zone later. A hit never
      does (with Lambert-Jonas, "How" below). A test round-trips it:
      `V_L(T(V)) = V` to 1e-9 over calibres, weights, angles and speeds.
    - Valid to 75° of obliquity. Past it, F takes Ob held at 75° and the 1/cos takes the true angle (as
      `armor_pen_ref.py` does); both are constant in T, so the inverse still holds. The ricochet gate (P5) takes
      over from 80°.
    - Either way it's noise: a hit costs about 200 µs in all (fighting.md 4.3a).
    - The plate bench still checks it against the trials before the old fit goes.
    - **Decks hit at 50–90° of obliquity** (fall under 40°, nearly every battleship deck hit at fighting range, and
      cruiser decks under 8″ fire at 15–25 km) are where Thompson and Okun's M79 disagree: M79 has an STS deck holding
      15–20% more there, and 10–16% less on truly plunging hits at 30–50° (`armor_pen_ref.py` §N). Which is right is
      open: Thompson has no scale term, but the Hood 1919 deck trials lean the other way, and the Iowa immune zone is
      Thompson's own, so it can't decide.
    - **Vanilla Thompson, no deck term** (user, 2026-10-17: "I'm fine with dropping the deck term and just live
      with the consequences. Vanilla Thompson shall be our model"). Decks take the same V_L as any plate. If M79 is
      right, STS decks in the game are 15–20% easier to get through at 50–90° and 10–16% harder at 30–50°, which
      moves the immune zone's far edge by 1–3 km. We accept that. The bench still prints M79's decks beside the
      game's so the gap stays visible, but no knob closes it.
  - **How (2026-10-17):**
    - **`Terminal`'s physics:** `LimitMs(tMm, calibreMm, massKg, ob)` is Thompson's V_L (inches, pounds and ft/s
      inside, SI out; F at Ob held to 75°, the 1/cos at the true angle); `PenMm(v, calibreMm, massKg, ob)` is its
      Cardano inverse; each has an overload taking the shell. The old fit and its `deck` flag go.
    - **The plate step:** the plate's thickness × q, drawn within `PenSpread` (one draw a plate, as before) and for a
      common shell over `CommonPenShare` (the old capability × 0.6); `LimitMs` of that; through if V ≥ V_L.
    - **The residual is Lambert-Jonas** (user, 2026-10-17, on Claude's suggestion: "This is a full rework ... We
      don't need to maintain coherency to the old across all the individual steps"): `Vr = √(V² − V_L²)`, a = 1, p = 2,
      the plate costing the energy that just gets through it (`Terminal.Residual`). Thompson's F rises with T/D (and
      faster at obliquity), so that energy grows faster than the thickness: plates stacked hold less than one of their
      sum, as spaced and laminated armour did (the user's tank example: 2 × 40 mm resists less than 80 mm), and
      ultra-thin laminates gain nothing. Under Thompson, 14″ 680 kg, stack limit over the solid plate's (the t/T rule
      it replaced in brackets): 2+1+2″ STS at 68° **0.75** (0.85; measured 0.74), 2 × 40 mm 0.96 (0.98), 2 × 190 mm
      0.86 (0.92), 4 × 100 mm 0.79 (0.88), 20 × 20 mm 0.73 (0.85). The research's 0.66 for Lambert-Jonas (§3.8)
      included Okun's turn toward the normal and his energy factor above 45°: those are P5's. A hit needs V_L only.
      Not modelled: the plug's mass (a < 1), small for big shells on thinner plates. Shells slow less than under t/T
      (15″ from 500 m/s through 160 mm: 433 m/s against 397), so bursts land deeper: P5's Baden and Hood check.
    - **Not the residual in u (tried and dropped, 2026-10-17):** written as `v·√(1 − u^−1.1)` (u = V/V_L) the rule is
      the old one only while T ∝ V^1.1. Under Thompson a thin plate's V_L goes as √T, so that form took 5–7% of a big
      shell's speed per 20 mm plate, against 2% before and about 1% Lambert-Jonas, about five times the energy
      Thompson itself gives the plate: Yamato on Yamato's bursts beyond the far side fell 19% → 9%. P1a went in with
      the t/T form (Thompson's T, one inverse a plate), then moved to Lambert-Jonas above the same day.
    - **The matchup:** its side and deck profiles read `PenMm` (no deck flag). It also gains the common shell's share,
      which it never had (a mismatch with the terminal, fixed on the way).
    - **`shipgen pen-check`** (no rolls; reads `research/simulation/armor/07-*.csv`): A, the calibration tables (the
      USN 16″ table first) belt and deck against `PenMm`; B, NPG 1946–47's measured limits against `LimitMs`; C, the
      Italian 1942 obliquity ratios; D, the early trials (Annapolis, Indian Head, Meppen, Beardmore, the 1937 problems,
      Baden) through `LimitMs` with the catalogue's `q` (P0's values: P1b's bench moves them); E, Okun's M79 deck
      columns (US STS and German Wh) over the game's, by deck obliquity.
    - **Tests:** the round trip `LimitMs(PenMm(V)) = V` to 1e-9 over calibres, weights, angles (0–89°) and speeds; the
      USN 16″ table within a few percent.
  - **Done (2026-10-17).** The bench, `shipgen pen-check`:
    - A: every USN table (16″/50, 16″/45, 14″/50, 12″/50 Mk 8) belt at 1.00, decks 0.96–0.97 on average (0.68–0.87
      at the shallowest falls, where the tables' few millimetres are rounded); the tables on British and German
      cemented plate come out 1.07–1.23 (face-hardened plate holding more than STS at q 1: P1b's Q), the uncapped
      shells' tables 1.45–1.54 (no shatter until P2).
    - B: NPG 1946–47's quoted NBLs reproduced to 0.99–1.02 (the port is Thompson); measured grades Class A 95%, KC n/A
      99%, Class B 98%, Wh 95%.
    - C: the Italian Palla's T(Ob)/T(0) 0.97/0.89/0.78/0.65 against the table's 0.98/0.91–0.92/0.81–0.82/0.63–0.69 at 10–40°; the
      SAPBC's table falls far less with obliquity than any formula (0.95 at 40°: its own oddity).
    - D: the early trials agree in 7 of 12 that went one way; the five misses are uncapped shells held by
      face-hardened plate (P2's shatter) and the old q of steel and nickel steel (P1b).
    - E: M79's decks over the game's: US STS 1.16 at 30–40°, 1.09 at 40–50°, 0.88/0.80/0.85 at 50–80°; German Wh
      1.36/1.31 at 30–50°, 1.02/0.91/0.97 at 50–80° (the vanilla decision's gap, now on record).
    - **Where the game moved** (old fit → Thompson): big guns' belts −2 to −6% at 0–45°, +10 to 12% at 60°; decks
      −4 to −9% at 60–78°, a third to a half less on grazing hits past 85° (the old cos^1.125 against Thompson's
      1/cos); small shells at long range less (the 4″ at 333 m/s −14%, side 100 → 87 mm, deck 15 → 12 mm).
    - **Benches P0 → P1a:** `hits` iron_duke by seydlitz at 12 km: defeated 34.7 → 40.1%, holes 0.037 → 0.017 m²,
      water after 30 min 40 → 9 t (the 11″ at 431 m/s loses 6%, so fewer holes in the lower belt); light_cruiser by
      iron_duke: ricochets 3.9 → 7.8%; tb_destroyer by light_cruiser AP: defeated 1.2 → 5.1%; yamato by yamato at
      25 km: within a point or two. `matchup` arethusa:4 v iron_duke confidence (11.5 km) 0.617 → 0.577, lion v
      seydlitz 1.125 → 1.164. `duel` identical. Squadrons 40 min: Britain lost 3 → 2, Germany 0 → 0; flotilla
      identical; tsushima --play 2 seeds: Russia lost 5.5 → 4, Japan 5 → 4. Per-hit cost unchanged (noise).
    - The matchup gained the common shell's `CommonPenShare` (it never had it). DamageTests' water-fuze test now
      runs 1 m of water, not 3: after 3 m the 15″ strikes at 365 m/s, and Thompson's 255 mm (old fit 279) sometimes
      leaves it on the dreadnought's lower belt, short of the hull the test is about.
    - **Lambert-Jonas, benches t/T → L-J:** yamato by yamato bursts beyond 17.4 → 19.3%, ricochets 9.2 → 7.5%, water
      309 → 328 t; iron_duke by seydlitz, light_cruiser, tb_destroyer within a few tenths; matchup, duel and flotilla
      identical; squadrons Britain lost 2 → 1 (Princess Royal survives), Germany 0; tsushima --play 2 seeds Russia
      4 → 3.5, Japan 4 → 4.5.
- **P1b The real Q** (done 2026-10-17; it renamed a few designs' materials, so shipgen moved too).
  - `q` from armor/00 §4.2 with Q(D), the ductility penalty and the thin face-hardened rule. WWI KC goes from 1.0 to
    about 0.85–0.88.
  - Structural steels get their own Q instead of the 0.6 catch-all.
  - The plate bench before and after.
  - **How (2026-10-17):**
    - **The block gains** `q_scale` (k: Q(D) = q·(D/8″)^−k for face-hardened plate past 8″, held at q below),
      `face_hardened` `{share, min_mm}` (the face's share of the plate, P2's; under `max(min_mm, 0.3·D)` the plate gets
      no face-hardened advantage, ×0.9 [INFERRED in 00 §4.2]) and `elongation_pct` (homogeneous plate under 25% loses
      to shells over 8″ by Okun's M79 penalty, `1 − (1 − √(El/25))·min(D − 8″, 8″)/8″`; absent, none: the pre-1900
      materials' q comes straight from big-calibre trials). All by the research's table (armor/00 §4.2,
      `armor_pen_ref.py` MATERIALS), keyed to the catalogue's names.
    - **One rule, in the sim:** `MaterialRow.Q(tMm, calibreMm)` (Sim.Ships), compiled from each block; the terminal's
      plate step, the effects' parts and the matchup read it, and `pen-check` compiles the catalogue's blocks through
      it. Fragments (E1's business) take it at calibre 0, q as at 8″.
    - **The matchup's profiles** are built at 8″ and 16″ and the face's answer interpolated by the shell's calibre
      (Q(D) is near linear between; held outside), so the AI reads the same Q(D) the terminal does.
    - **Names that meant two plates are split** (a material is physics, never a period): "cemented armour" stays WWI
      British KC (BKC 0.85), and KGV and the generic WWII battleships take "cemented armour (CA)" (0.99, k −0.05);
      Dunkerque and Richelieu "French cemented (1930s)" (FRFH 0.95, k 0.05); Littorio and Zara "Terni cemented (1930s)"
      (TKC 1.03, k −0.08), Dante keeping "Terni cemented" (Italian WWI KC, 0.85); the Deutschland of 1933 "Krupp
      cemented (KC n/A)"; König "Krupp cemented" (it had "cemented armour", a British name). The rest map one to one:
      Krupp cemented KCA 0.88 k 0.05, KC n/A 1.01 k 0.03, Vickers cemented BKC 0.85, Witkowitz 0.97, Class A (WWII)
      1.03 k 0.17, "Class A armour" (Pennsylvania, Colorado: US KC-type plate before the thick chill) KCA's [EST],
      Harvey 0.82 (face 0.08, from 3.2″), compound 0.72, wrought iron 0.53, nickel steel 0.85, Krupp non-cemented 0.92
      (22%), STS and Class B 1.00 (25%), special treatment steel (the WWI US name) 0.95 (23%), non-cemented armour
      1.00 (25%), Wotan hart 1.00 (18%), Wotan 0.95 (22%), Molybdenum steel (Yamato's decks: NVNC/CNC) 0.97 (23%),
      special steel (French homogeneous) 0.97 (23%) [EST], Vickers hardened 0.89 (face 0.35, from 11″), mild steel and
      steel 0.70 (Okun's ladder), high-tensile steel 0.80, wood 0.01 (Okun's teak), aluminium alloy 0.33 [EST: equal
      protection per weight, 2.7/7.85].
    - **The designs' tables follow the catalogue:** `shipgen upgrade --materials` rewrites each design's `materials`
      to the catalogue's blocks for the names its parts give (after a catalogue change; the designs carry copies).
  - **Done (2026-10-17).** As above; the 125 shipped and scenario designs' tables refreshed, the eight renamed
    (kgv, all_forward, all_forward_flush, dunkerque, richelieu, littorio, zara, deutschland; König in the scenarios).
    Fragments (`Effects`) read q as at 8".
    - **Benches (Lambert-Jonas P1a → P1b):** goldens 326 cases gain the new blocks and the eight their new names
      (`ShipDesign.Version` 20; nothing else moved), 372/372. `pen-check` D: 8 of 12 early trials agree (the misses:
      the two uncapped shells P2's shatter answers; Annapolis's all-steel armour, which the bench maps to the
      structural "steel" 0.70 against Schneider's 0.78; Baden's conning tower at 30°, V_L 456 m/s against 472, q
      0.85 where the plate held at ≥ 0.89).
    - `hits`: yamato by yamato at 25 km ricochets 7.5 → 26.2%, bursts beyond 19.3 → 11.7%, water 328 → 236 t (her
      "Molybdenum steel" decks were the 0.6 catch-all, now 0.93 against an 18″); iron_duke by seydlitz defeated
      40.3 → 38.8% (her WWI cemented armour now 0.85, though the plating's steel went 0.55 → 0.70), water 8 → 4 t;
      light_cruiser and tb_destroyer within a point. `matchup` arethusa:4 v iron_duke 0.577 → 0.543 (Iron Duke's
      305 mm belt now 259 against an 8″ and less), lion v seydlitz 1.164 → 1.228; duel 5.5 → 5.6%. Squadrons 40 min:
      Britain 1 → 1 lost (Princess Royal, not Lion), Germany 0 → 1 (Von der Tann); tsushima --play 2 seeds Russia
      3.5 → 4.5, Japan 4.5 → 3.
- **P1c Densities** (shipgen only; done 2026-10-17).
  - Mass by the material's density: report the shifts (the ironclads, PT boats, aluminium houses), `golden-update`
    with a `ShipDesign.Version` bump, `mono-check`.
  - **Done (2026-10-17).** `MaterialTemplates.Density(design, name)` (the design's block, the catalogue's, steel's; steel
    for a part with no material named) weighs every plate:
    - armour: the belt, zones, strakes, the lower belt, bulkheads and end plates, decks and slopes, the inner bulkhead
      (the bulkheads' material), the magazine boxes (sides and ends the bulkheads', crown and floor the decks'), the
      casings (their roof deck's), the conning tower, the flight deck, turrets and barbettes (`Armour.MountMaterial`,
      one rule now for the hitboxes' and the weights' mount material: `AddMount` takes the battery's material);
    - the hull's structure, its raised stretches and extra shell plating by `hull.plating.material`'s density (on
      `Construction.Rho`), the torpedo protection's plain bulkheads too; the superstructure's extra plating by its
      `material`'s (`Layout.SupRho`). Directors' hoods have no material: steel.
    - Not by density: the box-model hulls' structure (planing craft: `hull_k`, a fitted weight) and the
      superstructure's `t_per_m2` (the design's own knob for a light house), so the wooden PT boats and MTB, with no
      plating over their planking's own gauge, don't move. No design has aluminium.
    - **Shifts** (standard displacement): Devastation 9,168 → 8,890 t (−3.0%), Inflexible 18,224 → 17,761 t (−2.5%),
      Duilio 19,175 → 18,992 t (−1.0%), Brennus 10,974 → 10,882 t (−0.8%): wrought iron 7.7 and compound 7.75, a little
      more than the density's 1.3–1.9% as the lighter ship sizes down; their fuzz mutants with them. Goldens 16 design,
      16 SVG and 4 sprite cases (`ShipDesign.Version` 21), 372/372; `svg-check` and `sprite-check` 372/372; `mono-check`
      27 reversals and 16 lumps, as before. The sim's scenarios don't move (squadrons' hash identical).
- **P2 Caps, decapping, shatter and break-up as states** (done 2026-10-17).
  - The cap is lost on face-hardened plate or past its threshold, with a gap test before the next face-hardened plate.
  - Shatter of uncapped or decapped shells on face-hardened plate: +30–40% at normal, fading to nothing by 55°.
  - Break-up by the body's obliquity and T/D limits, most likely near the limit around 45°.
  - `Broken` leaves `ShellOutcomes`. A broken shell is still `Outcome.BrokeUp` here; P4 splits it into holed and
    through.
  - Benches: the matchup (Arethusa, Iron Duke), Jutland's 1/13 on more than 9″, the 1894–1905 battles, `shipgen hits`
    on the destroyers.
  - **How (2026-10-17):** the shell's state is walked plate by plate in `Terminal` (armor_pen_ref.py's
    `resolve_plate`, 00 §8, 05's plate-sequence outline), with no allocation and a draw only where a rule is a band:
    - **Shipgen:** the report's shells gain the cap's share and the body (`cap_share`, `break_ob_deg`, `break_t_d`,
      `body_quality`; none given: 90°, 0, 1), and `ShellTypeRow` carries them (`Cap` as an enum; `Diving` is the cap
      head's). The material's face gains `shatters` (true when not given): compound's steel face breaks only chilled
      shot (armor_pen_ref.py's `shatter_vs`), and Palliser's body limits (0°, 0.3) do that. Not in the plan's table:
      both were needed to carry the blocks' numbers to the sim (goldens, `ShipDesign.Version` 22).
    - **The cap:** a face-hardened plate always knocks it off, a homogeneous one by tech-085's T/D (soft, Type 1 and the
      cap head 0.105-0.12 under 15°, 0.0805 from it; Type 2 0.155-0.17 under 45°, 0.12-0.14 from it; within a band, a
      chance by where it falls). The plate is fought with the cap on; its mass goes after. Behind it the nose still
      meets a plate as if capped for the gap (2.3 calibres at normal down to 1 from 30°; a soft cap 0.5). A soft cap
      leaves the nose bare from 15° (surely by 20°), the cap head past 45°, and the cap head is gone on the water.
    - **Shatter, on face-hardened plate** (`Terminal.ShatterChance` and `FaceHardMm`, the rules pen-check and the
      matchup call too): a bare nose on a face that shatters steel, from 0.25 calibres (`BreakFromTD`), or a body past
      its proof (on `break_t_d` calibres or more, a chance rising from `break_ob_deg` to sure `BreakSpanDeg` 5° past
      it: RN 1914 APC "broke up at 15-20°", Greenboy survives its proof's 20° and degrades past it). One draw a
      face-hardened plate. The plate holds as if `ShatterBonus` 1.4 times thicker at normal, fading to none at 55°; a
      shell that still gets through is `BrokeUp`. This also carries 02 §4.6's "RN pre-1917: treat as shatter at
      15-20° on 0.4 calibre". A hard cut at the proof angle was tried first: Queen Elizabeth's 15″ then shattered on
      46% of hits on a dreadnought at 15 km (1 in 47 through plate over 9″ and burst, against Jutland's 1 in 13).
    - **The body's quality** (the block's `body.quality`, 0.85-1.10) divides the face-hardened plate's thickness only:
      02 §4.6's multipliers are against face-hardened plate, and on homogeneous plate the USN's tables (pen-check A, the
      Mk 8 at 1.00) are the formula's own. Unassigned in the plan; P2 is the body's step.
    - **Near the limit:** a whole shell through any plate of 0.25 calibres or more at under 1.1 V_L breaks with a chance
      of 0.1 under 30° and past 60°, rising to 0.5 at 45° (05 §1.2, `NearLimitBreak*`) [INFERRED].
    - **`Broken` leaves `ShellOutcomes`;** its share goes to the full burst, since what the research leaves the roll is
      the fuze's and the filler's failures (armour-and-damage.md §5): rn1916 50/35/10/5, km1916 60/15/10/15, rn1918
      60/10/20/10, ww2 80/5/7/8, ijn_t91 75/5/7/13 (burst, in plate, low-order, dud).
    - **The matchup** mirrors it: each face's points carry their face-hardened plate (rated and as thick as it is, and
      whether it shatters), so its estimate divides that plate by the body's quality, keeps out the shattering share (a
      body past its proof, or a bare nose by the cap's share at the angle), and breaks the near-limit share (between
      what the shell beats at its speed and at its speed over 1.1). It takes the cap as on: decapping by plating ahead
      is the terminal's.
    - **Benches:** `shipgen hits` prints decapped, shattered and broken-near-the-limit shares, and Jutland's measure: of
      the hits that met plate over 9″, those through it that burst inside. pen-check D runs each trial's shell (cap and
      body by its family) through `ShatterChance` (at even odds) and `FaceHardMm`.
  - **Done (2026-10-17).** As above. Goldens: 317 design cases gain the shells' four keys and compound's `shatters`
    (`ShipDesign.Version` 22), 372/372; brennus and inflexible's material tables refreshed (`upgrade --materials`).
    Per-hit cost unchanged (iron_duke by seydlitz 540 → 382 µs, yamato 1167 → 1161: noise); a matchup judgement 0.7-3.4 µs.
    - **pen-check D:** 10 of 12 early trials agree (8 before): the uncapped shells now shatter on Harvey and KC
      (Indian Head, Meppen held), Baden's 30° conning tower holds (Greenboy past its proof). The misses: Annapolis's
      all-steel plate (P1b's mapping to structural steel) and the 6″ Holtzer on 10.72″ compound, held by 1% (the
      uncapped body's 0.95 quality on the face).
    - **`shipgen hits`** (before → after; n 2000, 10 a ship, 30 min settle): Jutland's measure, through plate over 9″
      and burst inside: Lion on Seydlitz at 14 km **1 in 9.4**, Queen Elizabeth on Dreadnought at 15 km 1 in 12.4
      (record 1 in 13); Seydlitz on Iron Duke at 12 km 1 in 118 (shattered 18%: the Germans' Type 2 cap holds, their
      body breaks past 20° on 0.6 calibres), defeated 38.8 → 40%, burst inside 9.5 → 12.9%, water 44 → 48 t. Iron
      Duke on the light cruiser: BrokeUp 25.6 → 0%, burst inside 5.3 → 29.5%, water 678 → 892 t, out of the fight 36 →
      53% (her 3″ belt is under 0.25 calibres of a 13.5″: the research's Arethusa case). The 1905 shells: Borodino's
      uncapped 12″ on Mikasa shattered on 70% of hits, water 226 → 10 t, out of the fight 7 → 1% (1894-1905 belts never
      holed). Yamato by Yamato: shattered 25% (the Type 91 past its 20° proof on VH), bursts beyond 11.7 → 11.6%,
      water 2,751 → 2,819 t, magazine 3 → 0%. tb_destroyer by light cruiser AP: BrokeUp 3.8 → 0%, burst inside 0.8 →
      5.3%, out 7 → 11%. v25 by lion's HE: identical.
    - **Matchup:** Iron Duke's hit puts Arethusa out 3.74 → 6.23% (arethusa:4 v iron_duke's judgement 1.988 → 1.415,
      the gunfight at 11.5 km 0.543 → 0.339); lion v seydlitz 1.228 → 1.903; lion v derfflinger 2.39 and iron_duke v
      derfflinger 2.32, out of MatchupTests' even band (0.5-2): **widened to 2.5 on the user's say (2026-10-17),
      revisit at P3**, whose fuzes and in-plate bursts are the British shells' other failures. The cause is the rows:
      rn1916's 40% broken went to the full burst, and now British shells break only where the plates break them (on
      German decks and beam-on belt hits they stay whole). The matchup judges side hits beam-on, where few strike past
      15°; averaging over target angles 50-90° was tried and only brought lion v derfflinger to 2.14, so it stays out.
    - **Scenarios:** squadrons 40 min Britain lost 1 → 2 (Lion and Princess Royal, both magazines), Germany 1 → 0 (Von
      der Tann survives); flotilla moves (state hash 17475F16 → F1FCB5BF); tsushima --play 2 seeds Russia lost 4.5 →
      2, Japan 3 → 3.5: the Japanese 1904 shells (uncapped AP and base-fuzed common) shatter on the Russians' KC and
      Harvey, so the belts hold; Tsushima's damage was the upperworks and fires (P3's fuzes, the fire session).
    - **The magazine test** (`DamageTests`) fired Queen Elizabeth's shells at 37° below the horizontal; they now always
      shatter on Dreadnought's belt, so it takes KGV's WWII shell (the test is about the blast's record).
- **P3 Fuzes per fuze** (done 2026-10-17).
  - Start threshold and obliquity factor per fuze; the two-plate fuzes adding up thin plates in quick succession; face-hardened plate always starts it.
  - Delay scatter per fuze; blinds per fuze.
  - The `ShellOutcomes` rows and the transitional `outcomes` field are gone: what's left lives in the shell's
    `fuze.blind` and its explosive's `in_plate` and `full` (12 §4). S0's parity designs get them from their row.
  - **The filler's three stages** (12 §4):
    - in the plate: `P = min(1, in_plate · r²)` with r the plate over what the shell beats. In the plate step's terms
      r ≈ (V_L/V)², so `P ≈ min(1, in_plate / u⁴)`; the exponent is 12's knob to settle against Jutland and Baden;
    - initiation: full by the booster's rate, else low-order 75% (30% of the blast, no fragment bonus) or dud 25%;
      black powder is always "full" (its `re` prices it);
    - the outcome per hit in the log: "burst on plate", "low-order", "dud".
  - `FuzeStartsOnWater` on (P6's scaffold).
  - Benches: bursts behind the plate (Baden 38 ft), Iowa's bomb deck moving bursts up, South Dakota's pass-throughs.
  - **Revisit MatchupTests' even band** (widened 2 → 2.5 at P2, user 2026-10-17): with the British fuzes and lyddite's
    in-plate bursts as causes, lion and iron_duke v derfflinger should come back under 2. (They didn't: see Done.)
  - **How (2026-10-17):**
    - **Shipgen:** the report's shells carry the fuze's numbers (`fuze_start_cal`, `fuze_oblique`, `fuze_plates`,
      `fuze_scatter`, `fuze_blind`; `FuzeDefaults` 0.07, 0.45, single, 0.25, 0.05 where the block gives none) and the
      explosive's rates with the shell's booster (`filler_in_plate`, `filler_full`). `outcomes` leaves the block, the
      templates, the report and `ShellTypeRow` (`ShellQuality` gone). **Design format 3** (`DesignFormat`): the field is
      dropped, and a block whose fuze gives no `blind` takes its row's dud share; `shipgen upgrade` took the 72 shipped
      and 53 scenario designs to 3 (`ShipDesign.Version` 23, goldens 326 cases).
    - **The start** (`Terminal.FuzeStartMm`): `start_cal` calibres at normal on Okun's curve, stretched so that from 61°
      it's the fuze's `oblique` share (his curve's own is 0.45, the Mk 21's 0.25); the plate's thickness as it is, not
      as rated (the threshold is areal density against the shell's sectional density: structural steel's 0.70 no
      longer thins it). Any face-hardened plate starts it. A two-plate fuze adds up the plates' shares of the threshold
      while each comes within `FuzeSumCalibres` 10 [EST] of the last. The impact starts it even on a plate that then
      holds: running since an earlier plate, the shell bursts against this one, as before; started on this one, it
      bursts on its face (`BurstOutside`'s force, by the initiation's). The old 50% and 35% outside bursts of a
      defeated or glancing shell (the reference's roll) are gone.
    - **Started** (`Terminal.Start`): blind by the fuze's share (a dud); else the filler's initiation, full by its
      explosive's rate with the booster, else low-order (`LowOrderShare` 0.75, `LowOrderK` 0.45 → 0.3 of the blast, 12
      §4) or a dud. The delay is scattered by the fuze's own σ (a normal draw, none under 0), replacing the flat 0.7-1.1
      draw (`DelayLow`, `DelayHigh` gone). A nose fuze has its blinds and the initiation too.
    - **In the plate** (`Terminal.InPlateChance`): `in_plate·ρ²`, ρ = (V_L/v)², rolled on each plate the shell gets
      through whole. Not on a plate that stops it: 12 has ρ > 1 as the failure to penetrate, and extrapolating ρ² past it
      (tried first) burst nearly every TNT shell stopped by Iron Duke's barbettes in them, her magazine losses to 13.5%.
    - **The water's fuze on** (`FuzeStartsOnWater`), with its delay scattered and its blinds; P6 is done.
    - **The matchup:** `Good` from the shell (its blinds, its explosive's full rate, the low-order share), the in-plate
      share by the face's ρ (its plate over what the shell beats), `FuzeStartMm` for the plating. Where the fuze bursts
      it isn't the matchup's: a burst-depth term (the vitals reached by 1 − e^(−d/L), d the delay times the speed left,
      L a share of the beam) was tried and moved lion v derfflinger only 2.85 → 2.5 at any L, so it isn't in.
    - **Benches:** `shipgen hits` prints the bursts after the delay and how far behind the fuze's start they went off.
  - **Done (2026-10-17).** As above; the rows, `FuzeArmCalibres`, `DelayLow` and `DelayHigh` are gone from
    settings.json. Per-hit cost unchanged (iron_duke by seydlitz 348 → 387 µs, lion on seydlitz 680 → 393: noise).
    - **Benches, before (P2) → after** (`shipgen hits`, 2000 hits, 10 a ship, 30 min after):
      - **Baden:** a Greenboy (RN 1927 APC, 0.025 s) on Seydlitz at 15 km bursts **7.7 m** behind where its fuze
        started (6.9 before; Baden measured 11.6 m behind a 25 cm belt); through plate over 9″ and burst 1 in 2.1.
      - **South Dakota:** Yamato's Type 91 (0.4 s) on Colorado at 20 km: passed through 52.5 → 19.8%, burst beyond 10.4 →
        42.1%, so 62% of hits go through or burst beyond, both before and after (the record: 15-18 of 26).
      - **Jutland:** Lion on Seydlitz through plate over 9″ and burst 1 in 9.4 → **1 in 5.6** (record 1 in 13). The
        rows' flat 35% in the plate is now lyddite's 0.35·ρ², little on plate it clears by a margin; ρ¹ (tried) gives 1
        in 6.2, so open question 1's exponent isn't the gap. Under water 27.9 → 1.6% of hits (the 0.003 s fuze fires
        in the water 1.2 m on), water in Seydlitz 268 → 46 t, out of the fight 12 → 6%.
      - **The Germans:** Seydlitz on Iron Duke at 12 km: defeated 40 → 25%, burst inside 12.9 → 23.1%, low-order 2.9 →
        10.9%, dud 5.5 → 15% (the Psgr's 15% blinds and TNT with a basic booster, 60% full), under water 24.7 → 8.8%,
        water 48 → 34 t, magazine 4 → 4.5%, out of the fight 11 → 8%.
      - **Pairs** (out of the fight after 10 hits): Derfflinger on Lion 8%, Lion on Derfflinger 5%; Derfflinger on Iron
        Duke 13%, Iron Duke on Derfflinger 6%. In the terminal the German shells are the better per hit, as at Jutland.
      - **Light targets:** the light cruiser by Iron Duke: in the plate 23.1 → 2.2%, burst inside 29.5 → 56.2%, under
        water 25 → 1.6%, water 892 → 333 t, out of the fight 53 → 66%. tb_destroyer by the light cruiser's AP: passed
        through 82 → 70%, burst inside 5 → 20% (the raw plating now starts the 6″ fuze at obliquity), out 11 → 13%.
        V25 by Lion's 4″ HE: burst inside 99.9 → 90.7%, low-order 2.8%, dud 6.4%, out 34 → 36%.
      - **1905:** Borodino's 12″ on Mikasa: shattered 70 → 57%, burst inside 1.3 → 7.9%, out 1 → 4%. Yamato on Yamato:
        burst beyond 11.6 → 44.5%, passed through 39 → 5%, water 2,819 → 2,875 t, out 27%.
    - **Matchup:** arethusa:4 v iron_duke 1.415 → 1.231 (Iron Duke's hit puts her out 6.2 → 7.4%); lion v seydlitz
      1.903 → 2.257; lion v derfflinger 2.39 → 2.85, iron_duke v derfflinger 2.32 → 2.79. **The matchup and the terminal
      now disagree in direction:** the matchup rates the British shells about three times the Germans' per hit (beam-on,
      no burst depth, no underwater hits), the terminal the Germans higher. MatchupTests' even band widened to 3 on the
      user's say (2026-10-17), and the refit is its own step (**PM** below).
    - **Scenarios:** squadrons 40 min Britain lost 2 → 1 (Princess Royal; Lion survives), Germany 0; flotilla moves
      (hash F1FCB5BF → B025B91C); tsushima --play 2 seeds Russia lost 2 → 4, Japan 3.5 → 2.5 (history: Russia's fleet).
    - **pen-check:** unchanged (10 of 12; no fuzes in it).
    - **Tests:** DamageTests' "HE bursts" anchor counts full and low-order bursts, 0.9-1 (user, 2026-10-17: 0.918 full
      now, the rest the fuze's blinds and the filler's duds); the water-reach and the underwater splash tests take the
      water's fuze off where they test the run or the splash; the lone torpedo boat's attack test runs with hits doing
      no damage (since P3 she was often stopped 20 hits in, short of her torpedoes' range: the test is her attack's
      steps). New: the format 2 → 3 step.
- **PM The matchup refit to the terminal** (user, 2026-10-17; after P3). The matchup judges a hit beam-on, with a burst
  inside as good as any other and no hit under the water, so since P3 it rates the WWI British shells about three
  times the Germans' per hit where the terminal has them worse. Refit its per-hit estimate to `shipgen hits` across
  the bench pairs: burst depth by the fuze's delay, the underwater hits a delay fuze lets reach the hull, and the
  target angle's spread (P2's question). Then MatchupTests' even band goes back to 2.
- **P4 Three limits.** Holed (a hole, the shell broken, fragments only), Navy (the whole shell through) and effective (through,
  whole, fuze working), with a new `Outcome` for holed-but-broken.
  - The holed limit is the material's `back.holed_u` (face-hardened holed 2-12% below the Navy limit, armor/00; 14
    §2.1), and the spall band below it (`back.spall_u`) is marked on the outcome for E3.
  - The damage report's line says which.
- **P5 Angles.**
  - The ricochet gate at 65–80°, by V/V_L, certain from 80°, none for broken shells.
  - Compound obliquity with heel and barbette curvature.
  - Normalisation toward the plate's normal on the way through.
  - The residual is Lambert-Jonas since P1a (it fits the spaced deck: 0.75 against 0.74); with the turn toward the
    normal and Okun's energy factor above 45° added here, its burst distances are checked against Baden and the Hood
    deck series.
- **P6 Under water** (done; P3 turned the water's fuze on, 2026-10-17). The shell's speed by the distance it travels in the
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
  - **The straight path, again (15 §4):** with the fuze on, E7's under-hull bursts come from where the run ends. A
    Type 91 on its straight 55 m run (0.4 s delay, [UNCERTAIN]) ends 14 m down, under or beside a destroyer: most of a
    46 cm AP salvo's structural shock. The real shell flattened, so the straight line errs toward under-keel bursts
    and away from hull hits. E7's AP shock numbers for diving shells are an upper bound until the path is revisited.
  - **Outcomes accepted, not tuned (user, 2026-10-16):** with the 3-decay reach about a quarter more hits count and
    more ships flood and sink (the squadrons' Germans lose ~0.7 ships a run, none before). Without damage control
    that's expected; tuning to historical rates now would make ships immortal once it comes.
- **P7 Gratings, backing and scatter.**
  - `armour.gratings` and `armour.backing` in the tracer (the backing's spall catch is E3's).
  - Plate quality drawn per ship (about N(1, 0.06)) and per shot (N(0, 0.03)), instead of the ±10% per shot.
- **Later, if wanted:** edges and joints (up to 15% within 1.5 calibres of a plate's edge, from the hitbox's extents),
  and a hole as a free edge for repeated hits. Phase E has three more readers for the same plate grid: a belt hit on a
  joint leaks worse and a repeat hit loosens the plate (13 §2.3), and a contact burst at an opening or edge holes the
  plate inboard (14 §3.4, Tiger's Q roof). Until then: a joint is a 25% draw, an opening is a component kind
  (sighting hood, port), and a loosened plate is a flag per plate box.

## Phase E: what it does

Renumbered after the research (2026-10-16): the burst's pieces come first, because every later step reads them. The
notes use the old numbers: their E2 (spall) is E3 here, their E3 (fragments, near misses) is E1 and E7. Each
step names the `settings.json` numbers it retires and the note whose §-tests it adopts (wide bands, as the notes give
them). Every step that moves outcomes runs `shipgen hits` (with killed and wounded once E6 is in), the matchup and the
squadrons, and changes `Matchup.cs` with it.

- **E0 Hull data (parity).** `construction.shell`/`internals` and `fittings.shock_grade` (decision 10) read, validated,
  reported and compiled; the templates set them (hull templates' joints, shock grades by navy and year); `DesignFormat`
  upgrades old designs from their construction's name and their year label, with a warning when it can't tell. Goldens
  gain the keys only.
- **E1 The burst's pieces** (13).
  - **At compile, per shell type:** the body's own charge-to-metal ratio (80-90% of the filler against 55-65% of the
    metal), the bore and wall that follow from it, then:
    - the side spray: Gurney's speed (the explosive's `gurney_ms` × the steel's speed), Mott's ~6 mass classes of
      1 g and up (B from the brisance × the steel's size);
    - HE's ogive spray in a forward cone (30-80° off the nose), the slow tail spray;
    - two single pieces: the nose slug (AP, SAP: a third of the casing) and the base plug;
    - low-order: 0.4× Gurney, 3× B; black powder by its own constants.
  - **At the burst:** the ring ±7.5° square to the path, leaning forward by atan(V₀/v) with the shell's speed then,
    0.15 of the pieces outside it. Per part: density × presented area, a Poisson draw, THOR through its box by the
    material's `fragments` class with linear subtraction (Okun). Drag V₀·e^(−R/(247·m^⅓)).
  - **The nose slug and the base plug** go on through the terminal walk as pieces of their own: the slug a blunt shell
    without filler (quality ~0.6, 0.75 calibre), the plug slowed by its kick and stopped if the shell was slow at the
    burst.
  - **A line-target query,** hits = n·w/(2πR), for cables, pipes and trunks: the service networks' cut rule reads it
    (17 service §4).
  - **Replaces** `FragPenMm` and Okun's constants (`FragDist`, `FragT50`) for parts. Parts take the free field; people
    take E6's scale on it.
  - **Bench** `shipgen frag SHELL`: Okun's 8″ HE table regenerated (13 §4.1: the 50% and 1% columns within ~10% at
    4-20 m), WDR 51's splinter knee (13 §4.3: 25-30 lb STS against 5-8″ HE, 1-1.5″ against capital-ship HE), WDR 51's
    3,200 ft/s for the 6″ HC, Boise's largest piece.
  - **Open:** THOR above 53 g (AP and black-powder pieces of 0.2-20 kg): one rule for pieces over ~0.5 kg, THOR or a
    blunt-fragment Thompson walk.
- **E2 Holed but broken.**
  - The broken shell's pieces through the hole: E1's field at about 0.15×, no full burst (16 §4.3: a weak burst at the
    plate).
  - On a gunhouse, working chamber or barbette, a flash source for the magazine roll (Seydlitz at Dogger Bank).
- **E3 Behind a plate that held** (14). Not a burst: no gas, no shock, no fire, no breach.
  - **Triggers:** a shell stopped or shattered with u between the material's `back.spall_u` and `back.holed_u`
    (severity s through the band; shattered against its shattered limit); a heavy ricochet off a thin roof (about 30%
    for D ≥ 2.5 T, Dunkerque); a contact burst at an opening (until the edges step, by component kind: Tiger's Q roof,
    the hole's own plate half inboard at ~150 m/s). A contact burst on an intact face throws little (14 §3.3) and v0
    may skip it.
  - **The cloud:** a disc D + T across and 0.2 T deep, × s; half of it one big piece once s ≥ 0.5 (0.3 brittle), the
    rest flakes; axial speed 0.25·V·cos Ob·(0.4 + 0.6 s); the axis halfway between the plate's normal and the shot
    line; the edge at 45°, densest at 18°, speed cos(1.92θ).
  - **It stops** at the first boundary of 5 mm or more, but for chunks THOR passes (plates over ~250 mm); backing (P7)
    thins the flakes by its mass per area over the scab's.
  - **Its effects:** parts in the cone by λ = N·f(θ)·A/r² against their fragility (instrument ~100 J, cable or pipe
    ~500 J, a machine's casing ~5 kJ); the big piece on the nearest part within 15° of the axis; crew through E6; a
    flash roll (~20% at s ≥ 0.5) where charges lie open, as E2's.
  - **Replaces** the plan's "weaker burst inside" and 12's flat 15% behind an in-plate burst.
  - **Matchup:** a spall term per (gun, plate) profile. **Tests:** 14 §6.5.
- **E4 What a plate that held shakes** (13 non-penetrating). For `Defeated`, `Ricochet` and `BurstOutside`, from the
  plate, the margin (r, or u) and the impulse I = m·v:
  - **Turrets and barbettes:** the sub-location from the hit point (exposed barbette near the ring, barbette at the deck
    or glacis, face, side, roof); the outcome by 13 §1.4's table × size(I) × margin(r); new mount states: **stiff**
    (trains at 0.4×), **one gun out**, **sights out** (needs a director, else degraded), **stunned** (skips salvoes);
    a jam's length by its cause (debris 3-40 min, deck heaved 1-10 h, ring for the battle; 44% last the action). Thin
    turrets: the barbette row ×1.5 (Savo, Boise). Replaces `JamChance` and the jam repair's 10-60 min.
  - **The belt at the waterline:** the zone by the *current* waterline (heel, trim, sinkage); P(leak) 0.5 within ±1 m,
    0.25 above, 0.8 at the lower edge; a joint ×1.5, a repeat hit ×2 and the plate loosened (defends at 0.85), a welded
    skin ×0.6; the regime (seep 55%, moderate 30%, major 12%, lost 3%) as a hole into the cell behind with its damage
    control row and its reopening above 15 kn; a feed or oil tank behind: a boiler or engine casualty 15% of the time.
    Replaces `SeamLeakMinMm`, `SeamLeakChance` and `SeamLeakM2`.
  - **Shock through the structure** (zone A the struck structure, B its neighbours, C the ship): timed outages on the
    function table (local power, rangefinder, director gyro, gyrocompass, radar, phones and FC circuits; turret or CT
    crew stunned), by the shock grade, scaled by (I / 4×10⁵ N·s)^0.7. A device the ship doesn't carry can't fail, so the
    era columns fall out of the design. The ship-wide cascade stays rare (0.02 a heavy hit at G0). Own guns: radar and
    breaker events per 20 min of main-battery fire.
  - **Tests:** 13 §5.3. **Log:** 13 §5.2's lines.
- **E5 Blast** (11). Open question 1.
  - **Open** (outside the hull, a weather deck, an open mount, a space vented by 0.6·V^⅔ or more): wrecked within
    1.0·W^⅓ (was 1.6), people killed by blast within 1.3·W^⅓, injured to 3.5·W^⅓ as now; the contact hole's `R_hole` as
    now (unbiased against 13 measured holes, 11 §3.1); no bulkhead breach.
  - **Confined:** a near zone of 1.0·W^⅓, boundaries in it holed to 0.5·W^⅓; then the cascade over the cell graph: the
    quasi-static pressure from the explosive's heats, W/V and the space's air (the water in it takes some); the
    blowdown through its vents; what a panel feels; boundaries rupture at 0.06 MPa per mm, doors at half that, deform
    from 0.012 (riveted ×0.85, joiner partitions between a cell's rooms 0.025 MPa/mm); a ruptured boundary joins the
    next space and the pressure falls; repeat until nothing more gives.
  - **What it leaves:** ruptured boundaries as openings in the flooding (¼ of a bulkhead or deck, 1.2 m² for a door,
    shell plating to the sea or air); deformed ones as a seam leak, `r(D)·area` with D = P_felt / P_deform (17
    leakage's curve, lognormal σ 0.8, welded ×0.4), and their doors jammed; parts in every reached space by pressure
    class (fittings 0.03, electrical 0.10, auxiliary 0.25, machinery 0.6 MPa); people through E6 (Bowen).
  - **HE bursting in a thin plate** is near-wall: the contact hole, fragments inward, the cascade vented by the hole.
  - **Needs per cell:** volume (the flooding has it), vents (by room kind until the designer emits them), doors on WT
    bulkheads, trunks (hoists, uptakes, vent trunks) as low-pressure edges that the magazine chain reads too.
  - **Replaces** `WreckRadiusK` as one radius for every burst, `RoomDisableShare` and the breach rule; `RoomVital` keeps
    only its share of what in a room matters.
  - **Matchup:** a burst's effect by the class's typical space volume. **Bench** `shipgen burst DESIGN SPACE SHELL`:
    11 §4.2's cases on our ships. **Tests:** 11 §5.5. Fumes later.
- **E6 Casualties** (16, decision 8).
  - **Men, not area:** per group (a cell's or part's `battle_crew`, in distance bands), the man-stopping flux
    λ = S·N(r)·g·A·T·band/(4πr²), with N(r) E1's pieces still carrying 80 J; a cover share by room kind (the rest take
    0.1 λ); P(casualty) = 1 − e^(−λ), P(killed) = 1 − e^(−qλ). Blast (E5) and spall (E3) as independent risks.
  - **A shell that doesn't burst** kills in its path and drives each perforated plate's disc ahead (1.5 calibres, 10 g
    pieces, a 35° cone, q 0.35): Saumarez's director, and nobody in an empty flat.
  - **Propellant flash** kills a share of everyone it reaches: a turret train 0.92 killed / 0.06 wounded, an open
    casemate battery 0.54 / 0.32.
  - **Condition** keeps killed and wounded apart: 45% of the wounded are out for the action, the rest back at their
    station in 15-45 min.
  - **Fit:** S 8.7 and q 0.04 to start; refit S with `shipgen hits` on one ship per record group (16 §5.3), q only if
    the killed share is off by more than 0.1; check the shape (share of hits killing nobody, the 99th percentile).
  - **Replaces** `CasualtyWreckShare`, `CasualtyBlastShare`, `PassLaneM`, `PassCasualtyShare` and the U(0.3, 1.7).
  - **Tests:** 16 §5.4.
- **E7 Near misses** (15).
  - **A new `Hit` source,** `NearMiss`: a burst point outside the hull, no terminal walk. On a miss, a broadphase of
    R_near = max(15·W^⅓, 7.5·√W) m against the ships' waterplanes; most misses cost that one test.
  - **Nose fuze:** fires with `water_fires`, at z_b = v·`water_ms`·sin(fall) under the surface. Splinters by E1's field,
    the sea as a floor (water drag L₁ 0.3 m/kg^⅓) and the depth as a lid; the lean analytic (15 §6.3); THOR per face;
    perforations in the waterline band become flooding holes; exposed parts and their crew (E6).
  - **Base fuze** (P3's water fuze on): P6's run reaching the hull is P6's hit; otherwise the burst where the run ends,
    shock only.
  - **Shock:** NSF = √W/R·(1 + sin a)/2·√(1 − e^(−2Δt/θ)) at the nearest wetted hull and per machinery space. The
    structure's seam leaks by `r(D)` with D = 5·NSF (× t_bottom/9 mm; replaces 15's two bands); machinery and gear by
    their own NSF and the shock grade; a stern burst rolls the rudder and shafts.
  - **Water ricochet** below ~6.4° of fall: a nose fuze bursts on the graze, a base fuze doesn't (v0).
  - **Tests:** 15 §6.7. **Bench:** 15 §5's salvos at a Fletcher; the River Plate DCT (8× under the record: try the fuze
    time and the geometry before the band).
- **E8 The battle anchors.** With S and P in, the whole chain against the battles: Jutland, Bismarck's ~3 belt holes in
  ~400 hits, South Dakota, Samar, the 1894–1905 battles, the destroyer benches; and the notes' own: 16's killed and
  wounded per hit by group, 13's turret-minutes lost per 100 heavy hits (about half to non-penetrations), 15's
  near misses per heavy-HE hit on a destroyer; and the user's: medium HE cooking off a battleship's magazine stays
  rare, not never (about 1.5% of ships under thirty 6″ HE hits; fighting.md 4.5, Magazines), which E2, E3 and E5 all
  move. The leftover rolls are tuned only within their sourced bands; penetration itself is vanilla Thompson
  and isn't tuned (P1a). The anchors are the damage *done* (holes, parts, crew, outcomes per
  hit), not who survived it: losses and sinkings above history are expected until damage control (the rules above).

## What stays out

- Bombs: the deck physics serves them, but bomb shapes and fuzes aren't researched yet.
- The magazines, flooding, fire spread and the service networks: their own 4.5 sessions (`fighting.md` 4.5 lists
  them), fed by better burst points (P3) and the states phase E adds. Their research is in, and this plan hands each a
  seam:
  - **Magazines** (`07-magazine-explosions.md`; fighting.md 4.5: the venting race, the flash chain by doors, propellant
    by navy, flooding a magazine as an order, the hull broken at the magazine, flash discipline as a choice): E2's
    flash source at a holed gunhouse, working chamber or barbette and E3's flash roll where charges lie open are the
    chain's first link; E5's trunks (hoists, uptakes, vent trunks) are the low-pressure edges it runs along; E6 kills
    by propellant flash. Flash discipline decides where charges lie open, so it moves E3's roll without a change here.
    The venting race needs the ammunition's `charge`, its own later step (Ammunition: "Not added in this plan"); until
    then it's `DesignerPlaceholders.Charge` and a side's propellant factor.
  - **Fire** (`17_fire.md`: growth to flashover, burning by the openings, fire load as design numbers, the fire main
    deciding quick or never, hose water left aboard): phase E gives it the seat, the explosive's `fire`, and the
    boundaries the blast opened and the holes that ventilate.
  - **Flooding** (`17_structural_leakage.md`: fittings, head, working, caulking and shoring on sprung boundaries): E4,
    E5 and E7 already set the seam area by its `r(D)` curve; the rest of the note is the session's.
  - **Service networks** (`17_service_network_faults.md`: breaker fragility by grade, cable cuts per wireway, faults
    walking up the protection): E1's line-target query and E4's and E7's shock (timed outages until then) are what it
    replaces with devices; the shock grade is already on the design (E0).
  - **Crew** (fighting.md 4.5): crew as the damage-control resource, the command node, and posture ("closed up" or
    "under cover", 16 §4.9) stay there; casualties per hit are E6.
- **Damage control** (`09_damage_control.md`, `10_damage_control_holistic.md`): after this plan. Phase E breaks the
  ships; damage control is what then fights it.
- The AI choosing the shell by target (the AI's pass).
- Variance in construction quality per part (a block per part; decision 2).
- The campaign's sides of the explosives: availability by feedstock, filling rates, storage and age (12 §5).

## Questions for the user

None open (2026-10-16).

## Open questions (from the research)

None blocks a step; each is settled by its step's bench.

1. **The filler's in-plate curve** (P3): 12's r², here u⁻⁴, against the Jutland sample and Baden. P3: ρ¹ instead moves
   Lion on Seydlitz's 1 in 5.6 only to 1 in 6.2; the gap to Jutland's 1 in 13 is elsewhere.
2. **THOR above 53 g** (E1): the heavy AP and black-powder pieces.
3. **The casing shares and steel multipliers** (S1, E1) are [EST]: navweaps' cross-sections of the 16″ Mk 8 and Mk 13
   and the 6″ Mk 34 would pin them.
4. **The spall onsets** (E3): no naval measurement exists; 0.80-0.92 is defensible for each class.
5. **The nose fuze's time on water** (E7, 0.5 ms): the most sensitive near-miss number.
6. **The Type 91's path and delay** (P6, E7).
7. **The rupture and deform capacities** (E5, 0.06 and 0.012 MPa/mm): fitted to a handful of cases; the destroyer
   benches may move them within 0.04-0.08.
8. **S and q** (E6): S's ~9× over the free field is fitted, not shown; the refit on our ships says how much of it is
   placement.

## Done with: the research asked for on 2026-10-16

1. **Confinement** came in as `11_blast_confinement.md`: step E5.
2. **Explosives as parameters** came in as `12_explosive_fillers.md` (with 11 §2.4 and 13 §3.1): the catalogue in S1
   (decision 9).

## Steps at a glance

| Step | Needs | Shipgen | Sim | Moves numbers? |
|---|---|---|---|---|
| S0 | — | ammunition read, reported | compiler reads it | no |
| S1 | S0 | ammunition and explosives catalogues, templates | the explosive's `re` | ballistics, fire control, burst size |
| S2 | S0 | default outfits | rounds by kind, the magazine order, the dev fire's kind | who fires what |
| S3 | S2 | ammunition weight | — | displacement |
| S4 | S1 | designer picker (UI session; done) | — | no |
| S5 | S2 | the loadout applied and resolved | per-ship loadouts: scenarios, dev flag, bench (done) | only for ships given one |
| P0 | — | materials table, upgrade (done) | material rows (done) | no |
| P1a | P0 | — | vanilla Thompson, Cardano inverse, Lambert-Jonas, pen-check bench (done) | penetration (−2 to −6% big guns, more for small) |
| P1b | P1a | material catalogue, split names (done) | Q(D) (done) | penetration |
| P1c | P0 | densities (done) | — | weights |
| P2 | S2, P1b | shells' cap and body in the report, the face's `shatters` (done) | caps, shatter, break-up (done) | outcomes |
| P3 | P2 | fuze and filler numbers in the report; `outcomes` gone, format 3 (done) | fuzes; rows gone; water fuze on (done) | burst points |
| PM | P3 | — | the matchup refit to the terminal | AI judgements |
| P4 | P2 | — | three limits | outcomes |
| P5 | P1a | — | angles | ricochets, paths |
| P6 | — | — | under water (done) | underwater hits |
| P7 | P0 | gratings, backing | scatter | plates |
| E0 | — | joints, shock grade | compiled | no |
| E1 | S1, P0 | — | the burst's pieces, THOR, `frag` bench | fragments |
| E2 | P4, E1 | — | holed-but-broken, flash | damage |
| E3 | P4, E1 | — | spall | damage behind armour |
| E4 | E0, P1a | — | turret states, belt leaks, armour shock | jams, leaks, outages |
| E5 | E0, E1 | — | open and confined blast, `burst` bench | wreck reach, openings, leaks |
| E6 | E1, E3, E5 | — | casualties, killed and wounded | crew |
| E7 | E1, E5, P3 | — | near misses (new hit source), shock | near misses |
| E8 | all | — | tuning | the anchors |

S and P0 are independent, so P0 can come first or between S steps; P1a needs no shells. Everything after P1b needs
both phases' data. E0 can go any time; E4 and E5 need only S1, P0 and P1a, so they can run alongside P2-P5.
