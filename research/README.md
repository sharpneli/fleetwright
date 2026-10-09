# research/

Shared workspace between Claude Code (in the repo) and Claude (research companion in the Claude app).

## Layout

- `REQUESTS.md` - open research requests. Anyone (you, Claude Code) adds a request; the research companion picks it up.
- `<topic>.md` - one file per finished research topic (kebab-case names, e.g. `sdl3-gpu-bindless-textures.md`).
- `vfx/` - graphics and rendering research: water surface, wakes, splashes, smoke, flash, explosions, sprites.
- `simulation/` - game-model research: fire control and gunnery, damage, ship design (hull, powerplant, crew, superstructure).
- `<project>/` - imported research from other projects (e.g. `naval/`), kept as-is.

Python reference scripts (`*_ref.py` and helpers) sit next to the notes they support, in the same folder, so imports between them keep working (`python3 fire_control_ref.py` from `simulation/`). Script names stay snake_case because they are importable modules.

## Conventions for research notes

Each note starts with:

```
# Title
Status: draft | final    Updated: YYYY-MM-DD    Request: <link to REQUESTS.md entry, if any>
```

Then: **Summary** (the answer in a few lines) -> **Details** -> **Recommendations for this codebase** (file/class names where relevant) -> **Sources**.

Research notes are advisory. Claude Code decides what to implement and owns builds, tests, and git.

## Index

Imported on 2026-10-09 from the Naval project (the research done before this repo existed) and three of its Claude artifacts. Those notes keep their original body structure; only the header line was added, file names were made kebab-case, and cross-references were turned into relative links. The Naval docs predate this C#/SDL3 codebase, so their code references (`geometry.py`, `navarch.py`, `layout.py`, the shipgen prototype) point to the Python prototype, not to files here.

### vfx/

| Note | What it covers | Scripts / files |
|---|---|---|
| [ocean-surface-research.md](vfx/ocean-surface-research.md) | Open-ocean surface for every sea state: Tessendorf FFT, 3 cascades, slope-moment filtering from orbit to 1 cm/px, hex-tile anti-tiling, calibrated whitecaps, measured cost | [sea_state_lab.html](vfx/sea_state_lab.html) |
| [sea-state-lab.md](vfx/sea-state-lab.md) | The "Sea State Lab" artifact as Markdown: what the WebGL2 prototype computes, its controls and readouts | [sea_state_lab.html](vfx/sea_state_lab.html) |
| [wake-vfx-research.md](vfx/wake-vfx-research.md) | Top-down bow wave and wake that scales from FAC to Iowa, baked and live | [wake_bake_ref.py](vfx/wake_bake_ref.py), [wake_bake_ref_v2.py](vfx/wake_bake_ref_v2.py), [wake_turn_ref.py](vfx/wake_turn_ref.py) |
| [shell-splashes.md](vfx/shell-splashes.md) | Shell splashes (artifact "Shell Splashes — Research & Rendering Plan"): splash anatomy, size by calibre, angle of fall, dye loads, 3/4 vs top-down camera, GPU-instanced LOD tiers for thousands of splashes | — |
| [muzzle-blast-water-vfx.md](vfx/muzzle-blast-water-vfx.md) | Muzzle blast on the water surface: the frost disc, blast length scale shared by the other gun docs | [muzzle_blast_ref.py](vfx/muzzle_blast_ref.py) |
| [muzzle-flash-smoke-research.md](vfx/muzzle-flash-smoke-research.md) | Muzzle flash and propellant smoke for any calibre: HDR emitters, smoke puffs, flash detection range, smoke line-of-sight | [muzzle_flash_ref.py](vfx/muzzle_flash_ref.py), [figs_muzzle_flash.py](vfx/figs_muzzle_flash.py) |
| [blast-wave-visibles-research.md](vfx/blast-wave-visibles-research.md) | What the muzzle pressure wave makes visible: condensation (Wilson) cloud, sun shadowgraph line, spray, smoke pushed by the wave and jet | [blast_wave_visibles_ref.py](vfx/blast_wave_visibles_ref.py) |
| [unified-smoke-system.md](vfx/unified-smoke-system.md) | Smoke system data structure (artifact "Unified Smoke System — Data Structure Research"): Lagrangian Gaussian puffs vs dense and sparse grids, LOS queries, rendering from the same data | — |
| [magazine-explosion-vfx-research.md](vfx/magazine-explosion-vfx-research.md) | Magazine explosion VFX: period sources, scaling from the mechanics model, top-down rendering, event timeline, budget | [magazine_explosion_ref.py](vfx/magazine_explosion_ref.py), [magazine_explosion_side.py](vfx/magazine_explosion_side.py), [magazine_explosion_video.py](vfx/magazine_explosion_video.py), [magazine_jet_clip.py](vfx/magazine_jet_clip.py) |
| [sinking-vfx-research.md](vfx/sinking-vfx-research.md) | Sinking ship surface VFX: foam, slicks, debris for bow-first, stern-first, capsize and break-in-two | [sinking_foam_ref.py](vfx/sinking_foam_ref.py) |
| [sprite-pipeline.md](vfx/sprite-pipeline.md) | Ship sprite and design pipeline decisions (clean vector, top-down 2D on 3D) | — |

### simulation/

**Fire control and gunnery**

| Note | What it covers | Scripts |
|---|---|---|
| [fire-control-research.md](simulation/fire-control-research.md) | Main gunnery model: sighting, ranging, computing, laying, dispersion, hit test and spotting, 1890s to 1970s; 14 fire-control levels calibrated against history | [fire_control_ref.py](simulation/fire_control_ref.py) |
| [manual-gunnery-research.md](simulation/manual-gunnery-research.md) | Gunnery without computers, 1860–1912, and the MK1 eyeball in emergencies | [manual_gunnery_ref.py](simulation/manual_gunnery_ref.py) |
| [evasion-research.md](simulation/evasion-research.md) | Evasion vs fire control: how each ship class should manoeuvre, and the shooter's counters | [evasion_ref.py](simulation/evasion_ref.py) |
| [fire-control-designer.md](simulation/fire-control-designer.md) | Ship-designer choices (rangefinders, directors, radars) from tech values to combat-model inputs | [designer_ref.py](simulation/designer_ref.py) |
| [fire-control-next-session.md](simulation/fire-control-next-session.md) | Open items: join the manual and director combat paths, stability → roll → gunnery, pre-ironclad guns | — |

Raw appendices in [simulation/fire-control-research/](simulation/fire-control-research/):

| Note | What it covers |
|---|---|
| [computing.md](simulation/fire-control-research/computing.md) | Computing stage: ranges and bearings to gun orders, each computer generation, 1900–1990 |
| [radar.md](simulation/fire-control-research/radar.md) | Gunnery and fire-control radar, 1939–1990 |
| [dispersion-and-hits.md](simulation/fire-control-research/dispersion-and-hits.md) | Dispersion, danger space, hit probability and historical hit rates |
| [manoeuvring.md](simulation/fire-control-research/manoeuvring.md) | Turning, speed-change and rudder data per ship class for a Nomoto-style steering model |
| [evasion-history.md](simulation/fire-control-research/evasion-history.md) | Evading gunfire: history, doctrine and operations research, 1900–1990 |
| [predreadnought-tech.md](simulation/fire-control-research/predreadnought-tech.md) | Pre-director gunnery, 1860–1910: limits of the gunlayer and gun crew |
| [early-hit-rates.md](simulation/fire-control-research/early-hit-rates.md) | Historical hit rates, 1860–1910, for calibration |
| [emergency-eyeball.md](simulation/fire-control-research/emergency-eyeball.md) | Emergency and local-control gunnery, 1914–1990 |
| [component-costs.md](simulation/fire-control-research/component-costs.md) | Mass, size, crew, power and height of fire-control components, 1900–1990 |

Ballistics has no note of its own: the point-mass ballistics fitted to each gun live in [fire_control_ref.py](simulation/fire_control_ref.py) and are summarised in the companion-code paragraph of [fire-control-research.md](simulation/fire-control-research.md); penetration formulas are in [05-mechanics-and-games.md](simulation/damage-research/05-mechanics-and-games.md).

**Damage**

| Note | What it covers | Scripts |
|---|---|---|
| [damage-model-research.md](simulation/damage-model-research.md) | Synthesis: warship internals and damage modelling, 1890–1945; dependency graph; what the generator should emit | — |
| [01-capital-subdivision.md](simulation/damage-research/01-capital-subdivision.md) | Capital ship subdivision, armour schemes, torpedo protection | — |
| [02-components.md](simulation/damage-research/02-components.md) | Internal components and systems and how they fail | — |
| [03-small-vessels.md](simulation/damage-research/03-small-vessels.md) | Cruisers, destroyers, escorts, coastal forces, submarines | — |
| [04-carriers.md](simulation/damage-research/04-carriers.md) | Aircraft carriers 1920s–1945 | — |
| [05-mechanics-and-games.md](simulation/damage-research/05-mechanics-and-games.md) | Damage physics and statistics; prior art in games | — |
| [06-service-networks.md](simulation/damage-research/06-service-networks.md) | Hydraulics, electrics, electronics: designer outputs and damage/repair models (draft) | — |
| [07-magazine-explosions.md](simulation/damage-research/07-magazine-explosions.md) | Magazine explosions after ignition | — |
| [08-gunfire-effects.md](simulation/damage-research/08-gunfire-effects.md) | What a shell does when it hits | [gunfire_ref.py](simulation/damage-research/gunfire_ref.py) |

**Ship design model**

| Note | What it covers | Scripts |
|---|---|---|
| [powerplant-model.md](simulation/powerplant-model.md) | Machinery length, weight, fuel use and crew by tech family and maturity | — |
| [hull-weight-model.md](simulation/hull-weight-model.md) | Hull structure weight, including construction technology | [hull_weight_ref.py](simulation/hull_weight_ref.py) |
| [crew-space-model.md](simulation/crew-space-model.md) | Crew living space, provisions and water from complement and endurance | — |
| [superstructure-research.md](simulation/superstructure-research.md) | Superstructure contents, size and height, 1890–1970 | — |

### naval/

Other Naval project docs that are neither rendering nor simulation.

| Note | What it covers |
|---|---|
| [realtime-tech-stack-plan.md](naval/realtime-tech-stack-plan.md) | Realtime implementation plan: C#/.NET + SDL_GPU (draft); the plan this repo grew from |
| [sound-design-gunfire.md](naval/sound-design-gunfire.md) | Procedural gun sounds: feasibility and approach |
| [trailer-soundtrack-brief.md](naval/trailer-soundtrack-brief.md) | Brief for a code-generated trailer soundtrack (ZzFX) |
| [name-research.md](naval/name-research.md) | Game-name availability check (Oct 2026) |
