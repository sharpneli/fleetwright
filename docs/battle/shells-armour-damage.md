# Plan: shells, armour and damage (a 4.5 deepening)
Status: agreed, steps reviewed for implementation; research folded in (decisions 8-10); S0 done    Updated: 2026-10-16

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

Flooding, fire, magazines' venting race and the service networks stay their own 4.5 sessions. This plan feeds them
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
   residual). The plate bench checks it against the trials before the old fit goes.
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
| `density_t_m3` | 7.85 | weight | armor/03 §7 |
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
    `fuze.blind` and its explosive's `in_plate` and `full` (12 §4). S0's parity designs get them from their row.
  - **The filler's three stages** (12 §4):
    - in the plate: `P = min(1, in_plate · r²)` with r the plate over what the shell beats. In the plate step's terms
      r ≈ (V_L/V)², so `P ≈ min(1, in_plate / u⁴)`; the exponent is 12's knob to settle against Jutland and Baden;
    - initiation: full by the booster's rate, else low-order 75% (30% of the blast, no fragment bonus) or dud 25%;
      black powder is always "full" (its `re` prices it);
    - the outcome per hit in the log: "burst on plate", "low-order", "dud".
  - `FuzeStartsOnWater` on (P6's scaffold).
  - Benches: bursts behind the plate (Baden 38 ft), Iowa's bomb deck moving bursts up, South Dakota's pass-throughs.
- **P4 Three limits.** Holed (a hole, the shell broken, fragments only), Navy (the whole shell through) and effective (through,
  whole, fuze working), with a new `Outcome` for holed-but-broken.
  - The holed limit is the material's `back.holed_u` (face-hardened holed 2-12% below the Navy limit, armor/00; 14
    §2.1), and the spall band below it (`back.spall_u`) is marked on the outcome for E3.
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
  near misses per heavy-HE hit on a destroyer. The leftover rolls are tuned only within their sourced bands, and the
  deck multiplier (P1a) only if an anchor asks. The anchors are the damage *done* (holes, parts, crew, outcomes per
  hit), not who survived it: losses and sinkings above history are expected until damage control (the rules above).

## What stays out

- Bombs: the deck physics serves them, but bomb shapes and fuzes aren't researched yet.
- The magazines' venting race, flooding, fire spread and the service networks: their own 4.5 sessions, fed by the
  ammunition's `charge` (S0) and better burst points (P3). Their research is in, and this plan hands each a seam:
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

1. **The filler's in-plate curve** (P3): 12's r², here u⁻⁴, against the Jutland sample and Baden.
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
