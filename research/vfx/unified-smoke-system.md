# Unified Smoke System — Data Structure Research
Status: final    Updated: 2026-10-08    Request: -

## Summary and recommendation

**Make the authoritative smoke state a list of Lagrangian Gaussian puffs, not a grid.** Every grid (dense, sparse, clipmap) becomes a derived, throw-away view built from the puffs for one consumer: line-of-sight queries, rendering, or local wind effects. This extends the puff model the muzzle-flash doc already uses (`A`, `σ_h`, `σ_z`, closed-form τ) from gun smoke to funnel smoke, smoke screens and fires.

Why, in one line each:

- **Memory scales with smoke, not with the map.** A full Jutland day (\~250 ships, 30 min smoke life) is \~110k puffs, \~5 MiB. A dense 25 m grid of a 200 × 200 km theatre is \~1 GiB with velocities.
- **Simulation is local by construction.** Smoke over open sea is a passive scalar: drift with the wind, spread, decay. No pressure solve, so each puff updates on its own in O(1). There is nothing global to keep local.
- **Resolution follows age for free.** Diffusion makes old smoke wide and smooth; a wide puff is one record whatever its size. A grid has to be told to coarsen; puffs coarsen themselves.
- **Visibility queries are exact and cheap.** τ along a segment is a closed-form erf per puff (\~30 flops). A multi-level spatial hash keeps the puffs tested per query to the few dozen near the line.
- **One source of truth.** Render alpha and gameplay τ come from the same puffs, so what the player sees blocks sight by construction.

Use a **sparse brick grid** only as a camera-local, visual-only layer when you want interactive detail (blast tunnels, wake stirring, eddies) that puffs do not show. It never feeds gameplay, so it can be non-deterministic and dropped on low-end machines.

The main risk is puff count in dense close-quarters action. It is bounded by an explicit budget, age-based merging and moment-preserving merge (Runnalls 2007), so the cost degrades in accuracy, never in frame time.

## Problem framing: what "Jutland must work" means

The design target is \~250 ships in one engagement, each a continuous smoke source, over a theatre of order 200 × 200 km, for hours of game time. [World History Encyclopedia](https://www.worldhistory.org/audioplayer/en/2-2853/2406854eb689b8959a6c1e45e4f3d068/) puts Jutland at about 250 ships.

The record says smoke was a first-order tactical factor, which is what the system must reproduce:

- **Funnel smoke from your own side blinds you.** Jellicoe reports the battlecruisers 4–5 miles ahead could not be made out, mainly because of funnel smoke from the light forces trying to take station ahead ([Jellicoe, ch. 13c](https://www.wtj.com/archives/jellicoe/jellicoe13c.htm)).
- **Visibility is directional and variable.** About 12,000 yd to the south but much less on other bearings; rangefinders on Iron Duke at times limited to 9,000 yd; a brief clear channel gave 15,000 yd. With a WSW force-2 wind, German funnel smoke drifted onto the British line ([Jellicoe, ch. 13b](https://www.wtj.com/archives/jellicoe/jellicoe13b.htm)). So τ must depend on bearing and on where the wind carries each fleet's smoke.
- **Looking along a smoky column is far worse than across it.** Trailing funnel smoke often made signals unreadable beyond the next ship in line ([New World Encyclopedia](https://www.newworldencyclopedia.org/entry/Battle_of_Jutland)).
- **Deliberate screens decide engagements.** Iron Duke had trained on a battlecruiser when it slipped behind a destroyer smoke screen ([USNI Proceedings, 1920](https://www.usni.org/magazines/proceedings/1920/january/description-battle-jutland-concluded)); the German fleet covered its turn-away with destroyer smoke ([Jellicoe, ch. 13c](https://www.wtj.com/archives/jellicoe/jellicoe13c.htm)).

### Requirements this gives the data structure

| Requirement | Number to design for |
| --- | --- |
| Theatre extent | 200 × 200 km, unbounded index space preferred |
| Smoke sources | \~250 continuous (funnels), plus bursts (guns, hits, fires), plus line sources (screens) |
| Smallest feature that matters | \~10–20 m (a fresh funnel plume, a 5″ shot puff) |
| Largest feature | km-wide aged plumes, multi-km screens |
| Smoke lifetime | 10–60 min (tunable) |
| LOS queries | 31,125 unordered ship pairs at 250 ships, plus spotting aircraft and shell-splash spotting |
| Determinism | Gameplay τ must be deterministic for lockstep or replay; visuals need not be |

The span from 10 m features to 200 km extent is four orders of magnitude, and the smallest features exist only near their sources while the large ones are old. That age-structure is the main lever for the whole design.

## What smoke physics we actually need

**Model smoke as a passive scalar: advection by a prescribed wind, turbulent diffusion, and decay. Do not solve for the air flow.** This one decision is what makes local simulation possible.

Full smoke solvers (Stam-style stable fluids, the SPGrid smoke work) spend most of their time on the pressure projection, a global Poisson solve that couples every cell to every other. That is what makes them hard to localise. Over open sea at 1–100 km we do not need it:

- The wind is a large-scale given (weather), not something smoke creates. Prescribe it as a smooth field: a uniform base vector plus optional slow low-frequency variation (a coarse 8–16 km wind grid, or a few analytic vortices). Shear with height is one optional term.
- Small-scale turbulence is represented statistically, as growth of σ, exactly as atmospheric dispersion models do. CALPUFF is a non-steady-state Gaussian puff model ([OSTI, EPA-454/B-95/006](https://www.osti.gov/biblio/218038)); SCIPUFF represents a release as a collection of overlapping Gaussian puffs ([SCIPUFF paper](https://ams.confex.com/ams/pdfpapers/161708.pdf)).
- Buoyancy matters for the first seconds to minutes (hot funnel gas rises, gun smoke rises 0.6 λ\_f per the muzzle-flash doc). Model it per puff as a rise curve toward an equilibrium height, not as a flow.
- Interaction with obstacles is negligible at sea: islands are rare and coasts can be handled with a ground-height clamp.

### The per-puff equations

```
position:   dx/dt = w(x, z, t)                    (sample the wind field at the puff centre)
height:     z(t)  → z_eq along a rise curve        (buoyant phase, then neutral)
spread:     σ_h² = σ_h0² + 2 K_h t,  σ_z² = σ_z0² + 2 K_z t     (or Pasquill–Gifford-style curves)
extinction: A(t)  = A0 · exp(−t/τ_life)             (washout, dilution below threshold, artistic fade)
```

`A` is the extinction area in m², conserved apart from decay, as in the muzzle-flash doc. K values are tuning constants; at K\_h ≈ 5 m²/s a 15 m puff grows to σ\_h ≈ 135 m in 30 minutes **\[INFERRED\]**.

### Calibrating funnel smoke

A moving ship leaves a line source with extinction per metre of trail `λ = Ȧ / v_rel` (v\_rel = ship speed relative to the air). The reference script checks one tuning, Ȧ = 1500 m²/s at v\_rel = 10 m/s **\[INFERRED tuning value\]**:

| Plume age (σ\_h, σ\_z) | Vertical τ (top-down alpha) | Crossing it at plume height: T | 500 m along the plume axis: τ |
| --- | --- | --- | --- |
| Young (15 m, 10 m) | 4.0 | 0.003 | 80 |
| Mid (30 m, 20 m) | 2.0 | 0.05 | 20 |
| Old (80 m, 40 m) | 0.75 | 0.22 | 3.7 |

This reproduces the record qualitatively: looking across one ship's trail is a dark band; looking along a column's smoke is opaque, which is why signals failed beyond the next ship. Tune Ȧ per plant using the k\_smoke factors from the powerplant doc (coal natural draught 4 down to gas turbine 0.3).

## Option A: dense grid (baseline)

**A dense theatre grid fails on memory before it fails on anything else, and it stores mostly empty sea.** Numbers for 200 × 200 km, one f32 density channel double-buffered, and with two f32 wind components added:

| Cell size | Cells | Density only | Density + velocity |
| --- | --- | --- | --- |
| 10 m | 400 M | 3.0 GiB | 6.0 GiB |
| 25 m | 64 M | 488 MiB | 977 MiB |
| 50 m | 16 M | 122 MiB | 244 MiB |

Other problems, even at an affordable 50 m:

- **Too coarse near sources and too fine far away.** A fresh plume is 10–30 m wide, so it is under-resolved; a 30-minute-old plume is 300+ m wide and wastes 6× more cells per feature than it needs.
- **Numerical diffusion.** Semi-Lagrangian advection smears a 2-cell plume within a few hundred steps. With wind moving smoke 5–10 m/s for an hour, a static grid advects everything every step.
- **Cost per step is the whole map.** 16 M cells × a few ops at 10 Hz is fine on a GPU but wasted on 99 % empty water, and it puts gameplay τ on the GPU, which complicates determinism.
- **LOS queries are long marches.** A 20 km sight line crosses 400 cells at 50 m; × 31k pairs = 12 M samples per evaluation. Mip-mapped empty-space skipping helps but does not remove it.

The dense grid is still the right *render* target, but only camera-local (see Rendering).

## Option B: sparse brick grids

**A sparse, multi-resolution brick grid in a wind-moving frame fits the Jutland budget (\~17 MiB upper bound) and keeps simulation local. It is the strongest grid design, and the right choice if smoke must interact with a resolved flow.** It loses to puffs on complexity, determinism and query cost, not on memory.

### Prior art

- **VDB / OpenVDB.** A hierarchical structure for sparse, time-varying volumes with an effectively infinite index space, compact storage and fast random and sequential access ([OpenVDB docs](https://www.openvdb.org/documentation/doxygen/)). A shallow, wide B+tree: hashed root → internal nodes → small dense leaves. NanoVDB is the GPU-friendly read-only variant, and Museth's hierarchical DDA does ray marching that skips empty nodes ([OpenVDB publications](https://openvdb.org/documentation/)).
- **SPGrid.** Stores sparse uniform grids through the x86 virtual-memory system, reaching stencil bandwidth comparable to dense grids, and builds adaptivity from a pyramid of sparse uniform grids rather than a pointer tree ([Setaluri et al. 2014](https://pages.cs.wisc.edu/~sifakis/project_pages/SPGrid.html)). Physical memory is committed only for active pages ([arXiv 2512.11473](https://arxiv.org/html/2512.11473v1)).
- **Counter-Strike 2 smokes.** Voxel volumes that flood-fill the available space and can be holed by gunfire and cleared by HE ([esports.gg](https://esports.gg/news/cs-go/counter-strike-2-innovates-in-the-way-smokes-are-used-in-game)); hobby reimplementations use a bounded voxel flood fill plus a ray marcher ([Gunnell, GitHub](https://github.com/GarrettGunnell/CS2-Smoke-Grenades)). Good evidence that bounded local grids work for interactive detail, at room scale.

### Layout for 2D/2.5D smoke

```
BrickKey  = (level:u8, bx:i32, by:i32)              // brick coords in the frame of its level
Brick     = 16×16 cells (+1-cell apron in a scratch copy), SoA:
            density[256]: f16/f32, optional top_height[256] or 2–4 height layers
Pool      = slab of bricks, free list; index = u32
Index     = open-addressing hash BrickKey → pool index, per level
Mask      = per-brick bitmask of nonzero cells (fast empty test, DDA skipping)
```

That is a two-level VDB in 2D: hash root → 16² leaf. In 2D, depth beyond that buys little.

### Keeping the simulation local

1. **Move the frame with the mean wind.** Each level's grid origin drifts at the mean wind `w̄`. Advection by `w̄` then costs nothing and adds no numerical diffusion; only the residual `w − w̄` and the emitters' motion relative to the air are advected. This removes most of the cost and most of the smearing of a fixed grid.
2. **Activity = data, plus one ring.** A brick is active if its max density > ε, or if a neighbour is (so smoke can flow in). Retire bricks after N steps below ε. Active bricks are the only ones touched.
3. **Per-brick kernels with an apron.** Gather a 1-cell apron from neighbours, run advect-diffuse-decay on the 18² tile, write back. Fully parallel, no global solve. Residual-wind CFL ≤ 1 cell keeps the stencil at one ring.
4. **Resolution by age (the key trick).** Emitters write only to level 0 (\~8 m). After age t\_L, when `√(2 K t_L)` passes \~2 cells of level L+1, smoke is restricted (2×2 average) to the next level and cleared below. Diffusion has already removed the detail a coarser level cannot hold, so nothing visible is lost. Levels 8 → 16 → 32 → 64 → 128 → 256 m cover the whole lifetime.
5. **Multirate stepping.** The explicit diffusion limit is Δt ∝ Δx²/K, so a level twice as coarse can step 4× less often. Old smoke costs almost nothing per frame.

### Footprint

For one 18 km, 30-minute trail with cell size ≈ σ\_h/2: \~2,200 cells, \~8,700 with 4× brick waste. For 250 non-overlapping trails at 2 × f32: **\~17 MiB** (reference script). A dense 10 × 10 km melee box at 16 m is 390k cells, a fixed cost however many ships fire into it. That bounded cost under overlap is the one real advantage over puffs.

### Weak points

- Restriction, prolongation, apron gathers and frame shifts are a lot of machinery to get right and deterministic on CPU.
- LOS queries march per level (DDA), \~1 sample per cell crossed. Cheaper than dense, still far more than evaluating a few dozen analytic puffs.
- Height is awkward. A 2D grid loses the distinction between smoke below and above the masthead that the muzzle-flash doc relies on; adding layers multiplies memory.

## Option C: Lagrangian Gaussian puffs (recommended)

**Store smoke as \~10⁵ Gaussian puffs of 48 bytes each; a full Jutland day fits in \~5 MiB, every puff updates independently in O(1), and old puffs coarsen on their own.** This is the method of operational dispersion models and of the project's muzzle-flash doc; what follows scales it to fleet level.

### Record

```
struct Puff {             // 48 B, SoA in practice
  f32 x, y, z;            // centre (m); f32 resolves ~2.4 cm at 200 km
  f32 sxx, sxy, syy;      // horizontal covariance (m²); isotropic case: sxx = syy = σ_h², sxy = 0
  f32 szz;                // vertical variance σ_z²
  f32 A;                  // extinction area (m²), as in the muzzle-flash doc
  f32 t_birth;            // for growth curves and age classes
  u32 emitter;            // ship / gun / fire / screen id
  u32 prev, next;         // neighbours in the same trail (merge candidates in O(1))
};
```

The full covariance is optional. It lets wind shear stretch a puff into an ellipse, so a trail segment needs fewer puffs; the closed-form τ still holds after a 2×2 whitening transform (\~10 extra flops).

### Lifecycle

1. **Emit.**
   - Guns, hits, explosions: one puff per event (muzzle-flash and magazine docs give A, σ0, rise).
   - Funnels: a continuous emitter drops a puff whenever its distance from the last puff, in the air frame, exceeds \~σ0 (≈ 2 s at 10 m/s). Carry the emitted A since the last drop, so the rate is exact whatever the frame rate.
   - Smoke screens: the same funnel emitter with a large Ȧ, switched on by the player.
   - Fires: a funnel-like emitter fixed to the burning hull.
2. **Update** (each sim tick, embarrassingly parallel): advect by the wind at the centre, apply the rise curve, grow σ, decay A. No neighbour access.
3. **Merge** (spread over frames, a slice of trails per tick): walk each trail's linked list and merge neighbours whose spacing is below \~0.5 σ. Use the moment-preserving merge: sum A, A-weighted mean, covariance = weighted covariances plus the spread of the means. Runnalls shows how to choose which pair to merge with an easily computed upper bound on the KL divergence ([Runnalls 2007](https://www.cs.kent.ac.uk/pubs/2007/2797)). Because σ grows as √t, merging gives about half the unmerged count over a 30-min trail (449 vs 900 in the reference script).
4. **Split** (rare): only when a puff becomes much wider than the wind field's own scale, so one wind sample no longer represents it. HYSPLIT splits one Gaussian puff into five in this case and merges puffs that come back together ([NOAA HYSPLIT tutorial](https://ready2.arl.noaa.gov/documents/Tutorial/html/conc_split.html)). With an 8–16 km wind grid this almost never triggers.
5. **Retire**: when peak vertical τ, `A / (2π σ_h²)`, drops below \~0.005 (invisible, irrelevant to LOS).

### Budget

| Source | Puffs at Jutland scale |
| --- | --- |
| Funnel trails, 250 ships, 30 min, merged | \~112,000 (5.1 MiB) |
| Gun and hit smoke, merged per mount (muzzle-flash doc §5.5) | \~10⁴ **\[INFERRED\]** |
| Screens and fires | \~10³–10⁴ **\[INFERRED\]** |

Set a hard cap (say 256k). Over the cap, raise the merge spacing for the oldest age classes first, then allow merging across emitters within a spatial cell. Accuracy degrades gracefully and the frame time does not.

### Where puffs are weaker

- **Dense overlap.** Ten ships firing into the same 2 km box can stack thousands of puffs on one sight line. Cross-emitter merging handles it, but cost grows with overlapping sources, whereas a grid's does not.
- **Resolved flow effects.** Eddies, blast tunnels and wake stirring are not represented. The blast-wave doc already pushes puffs in the muzzle cone (Δxy, σ × 1.2, A unchanged); that is enough for gameplay. Anything richer belongs in the visual-only local grid.

## Fast visibility queries

**A sight-line query is a sum of closed-form puff integrals over the few dozen puffs a multi-level hash returns, with an early exit once the line is opaque: \~2–5 µs each.** Pair culling and priority-based refresh keep a 250-ship battle to \~10⁴ queries per second.

### What a query returns

Optical depth along the segment, smoke plus background haze:

```
τ(a→b) = σ_bg · |b − a|  +  Σ_puffs τ_i(a→b)
T      = exp(−τ)
seen   ⇔ C₀ · T > ε      (ε ≈ 0.02–0.05)
```

Koschmieder's rule gives visual range `V = ln(1/ε)/σ`, which with ε = 0.02 is the familiar `3.912/σ` ([visibility reference](https://reference.org/facts/visibility/reqevMCQ)). Setting σ\_bg = 3.912 / V\_weather puts weather haze and smoke on one scale, so "12,000 yd to the south, much less on other bearings" falls out of the data. White smoke in front of a dark hull also adds airlight, which the muzzle-flash doc covers.

The per-puff term is the muzzle-flash doc's §5.4 formula (Gaussian factor in miss distance and height × ½\[erf − erf\] for the finite segment). The reference script checks the 2D form against numerical quadrature: 6.0161 × 10⁻³ both ways. For steep lines (aircraft spotting, plunging fire), use the full 3D Gaussian line integral, which is also closed form: project the segment through Σ⁻¹.

### Spatial index: multi-level hash grid

A single 256 m bin grid (the muzzle-flash doc's acceleration) breaks at fleet scale, because puff size spans 3 m to over 1 km. Small cells duplicate big puffs into hundreds of cells; big cells pack thousands of small puffs per cell.

Use one uniform grid per size class instead:

- Level k has cell size `c_k = 64 m · 2^k`, k = 0…7 (64 m to 8 km).
- A puff goes into exactly one cell: the one holding its centre, at the smallest level where `c_k ≥ 6 σ_h`. Its 3σ footprint then lies inside that cell's 3×3 neighbourhood.
- Rebuild every tick by sorting (level, cell) keys. A counting or radix sort is O(n): \~110k puffs in well under 1 ms on one core **\[INFERRED\]**. No incremental bookkeeping, no deletions.
- Store per cell: start index into the sorted puff array, count, and the cell's summed A (for coarse bounds).

Query:

1. For each non-empty level, walk the segment with an Amanatides–Woo DDA, two comparisons and one add per cell step ([Amanatides & Woo 1987](https://diglib.eg.org/items/60c72224-00f3-416d-9952-ee41e8c408da)), dilated by one cell to cover the 3×3 rule.
2. Optional bound: skip a cell when its summed A could add at most a negligible τ (A / (2π σ\_min σ\_z) × cell length).
3. Evaluate the closed form for each gathered puff, in a fixed order. Stop once τ > 6 (T < 0.25 %).

A 20 km line crosses \~310 cells at level 0 and \~3 at level 7 (×3 with the dilation), so under \~2,000 cheap cell visits, most of them empty and 20–200 puff evaluations. At \~30 flops per puff, that is 2–5 µs, in line with the muzzle-flash doc's 100 puffs × 50 pairs ≈ 0.1 ms **\[INFERRED\]**.

### Not asking 31,125 questions every tick

1. **Geometry first.** Only pairs inside the geometric horizon (\~30 km, mast to mast) and inside haze range (`σ_bg · L < ln(C₀/ε)`) are candidates. Find them with a coarse ship hash.
2. **Priority refresh.** Engaged pairs (firing solution, or within 20 % of the threshold) refresh every tick; clearly visible or clearly hidden pairs every 2–5 s. Smoke changes slowly apart from fresh screens and salvos, so stale answers are rarely wrong.
3. **Event invalidation.** A new screen emitter or a heavy salvo marks its level-0 and level-1 cells dirty; pairs whose lines cross dirty cells jump the queue.
4. **Hysteresis.** Separate thresholds for gaining and losing contact (e.g. T > 0.05 to spot, T < 0.02 to lose) so contacts do not flicker at the edge.

At \~10⁴ refreshed pairs per second × 5 µs this is \~50 ms of CPU per second, under 1 % of an 8-core budget **\[INFERRED\]**.

### Per-observer opacity fans (player view)

For fog-of-war overlays and "what can this ship see" displays, cast a fan of 360–720 rays from each player ship and store τ at range bins (a polar map, in the spirit of a deep shadow map). Any target is then an O(1) lookup, and the fan can be drawn directly as the visible-sea overlay. Rebuild one or two observers per frame, round-robin.

### Determinism

If the game is lockstep or replay-based, gameplay τ must be bit-identical on every machine:

- Evaluate puffs in sorted (level, cell, id) order.
- Use your own polynomial erf and exp, not libm.
- Compile the smoke module without FMA contraction or fast-math; or use fixed-point for positions.
- Keep render-only effects (the local grid, noise) out of the τ path.

## Rendering from the same data

**Splat the puffs into a camera-local, mip-levelled τ texture each frame, then shade that texture; never keep a persistent world-sized grid for visuals.** Render alpha is `1 − e^(−τ_v)` from the same puffs gameplay uses, so the agreement the muzzle-flash doc established holds at every zoom.

### Two passes

1. **Accumulate.** Draw each visible puff as one instanced quad into an R16F τ\_v target, additive, with the analytic Gaussian footprint (normalised analytically, per the muzzle-flash doc's prototype bug). Each puff picks the mip of the target where its σ is \~1–2 texels. A 1 km-wide old puff then writes \~16 texels at a coarse mip instead of covering the screen at full resolution. This bounds overdraw at theatre zoom, where all \~10⁵ puffs are on screen.
2. **Resolve and shade.** Reconstruct τ\_v from the mip chain (sum of upsampled levels), then light it with the existing smoke shading (6-way lit flipbook look, analytic shadows along the sun). Add detail with tiling noise that is advected in the wind frame, so texture drifts with the smoke instead of swimming. Modulate the noise multiplicatively around 1 so mean τ is unchanged.

At close zoom the per-puff billboards of the magazine and muzzle-flash docs can still draw on top for fresh, high-detail smoke (age < \~60 s). They switch off by age, not by distance, which matches what a grid would do with its age-based levels.

### Optional local detail grid (visual only)

When the camera covers less than \~3 km, run a small sparse brick grid (Option B mechanics, one or two levels, \~256² cells) around the view:

- **Seed** it each frame from the puffs' τ\_v as a low-frequency target.
- **Simulate** only a perturbation: curl-noise eddies, wake stirring, impulses from the blast-wave doc's jet punch and from shell bursts, in the CS2 "shoot a hole in it" style.
- **Output** a multiplier on the puff τ\_v that relaxes back to 1 over a few seconds, so the grid can never create or destroy smoke for long.

Because it only decorates, it can run on the GPU, ignore determinism, scale with quality settings, and be thrown away when the camera moves.

## Recommended architecture

**One persistent, deterministic puff store; three derived views rebuilt from it each tick or frame; nothing world-sized.** Memory for the smoke itself is the smallest of every option, and everything above it is scratch.

&#91;embedded content: reference script estimates · 250 ships, 30 min smoke life, K\_h = 5 m²/s\]

The multi-resolution sparse grid is in the same range as puffs, so memory alone does not decide it. Puffs win on locality, exact queries, height, and less machinery.

### Data flow

&#91;embedded content: smoke system data flow · 1 persistent store, 3 derived views\]

Gameplay reads only the hash path; the dashed grid never feeds back into the store.

### Per-tick schedule (simulation, fixed rate, e.g. 10 Hz)

1. **Emit.** Emitters append puffs in emitter-id order (deterministic).
2. **Update.** All puffs in parallel: drift, rise, grow, decay.
3. **Merge and retire.** A rotating slice of trails (e.g. 1/8 per tick), plus budget enforcement.
4. **Index.** Radix-sort puffs into the multi-level hash.
5. **Query.** Drain the LOS queue by priority; update contacts with hysteresis.

Per rendered frame: cull puffs to the view, splat τ\_v by mip, resolve, apply the local detail grid if active, shade and composite (composite order as in the muzzle-flash doc §7.6).

### API (extends the muzzle-flash doc §7.5)

```
smoke_emit_burst(pos, A, σ0, rise, emitter)
smoke_emitter_set(id, pos, Ȧ, σ0, rise)        // funnels, screens, fires; Ȧ = 0 turns it off
smoke_tau(a: vec3, b: vec3, tau_max = 6) -> f32  // multi-level hash + closed form, early exit
smoke_tau_vertical(x, y) -> f32                  // aircraft spotting, CPU fallback for render
smoke_visible(observer, target) -> Contact        // cached, priority-refreshed, hysteresis
smoke_fan(observer, n_rays, r_max) -> PolarTau    // player fog-of-war overlay
```

### Build order

1. Port the puff code from [`muzzle_flash_ref.py`](muzzle_flash_ref.py) into the store; add the funnel emitter and Ȧ calibration.
2. Multi-level hash and DDA. Benchmark 250 ships, 30 min, 31k pairs, before anything else.
3. Merging and the global budget; test the 40-ship melee case.
4. Splat renderer with mip-by-σ.
5. Pair scheduler, event invalidation, hysteresis.
6. Local detail grid, last and optional.

## Risks and open questions

| Risk | Why it matters | Mitigation |
| --- | --- | --- |
| Puff pile-up in close melee | Query cost grows with overlapping sources; a grid's does not | Cross-emitter merge inside level-0/1 cells when a cell exceeds \~64 puffs; prototype a 40-ship gunnery duel in a 5 km box first |
| Funnel Ȧ and K tuning | Sets how much sight is lost; drives the whole tactical feel | Calibrate to the Jutland anchors: across-trail dark band, along-column opaque, 9–15 kyd typical ranges |
| Smoke height | Whether smoke passes over or through a 10–30 m sight line flips results | Keep σ\_z and the rise curve per source; tune against photos first, as the muzzle-flash doc advises |
| Wind field resolution | Puffs read the wind only at their centre | Smooth wind (≥ 8 km scale); split puffs that outgrow it |
| Determinism | erf/exp and summation order differ across compilers and CPUs | Own polynomials, fixed order, no FMA contraction (Determinism, above) |
| Merge artefacts | Merged trails can look beaded or lumpy at close zoom | Merge only by age class; fresh smoke (< 60 s) never merges; noise in the resolve pass hides the Gaussian shapes |

Open questions:

- [ ] Is the game lockstep, server-authoritative, or single-player? This decides how strict determinism must be.
- [ ] Do aircraft spot from above? If so, ship the full 3D line integral and the vertical τ query from day one.
- [ ] Should smoke drift onto land (Skagerrak coast scenarios)? If so, add a terrain clamp or a coarse land mask to puff advection.
- [ ] Target hardware: is a GPU splat pass guaranteed, or is a CPU-only τ\_v path needed for servers?

## Sources

Jutland:

- [Jellicoe, The Grand Fleet, ch. 13b](https://www.wtj.com/archives/jellicoe/jellicoe13b.htm) — variable, bearing-dependent visibility; funnel smoke drifting with the wind.
- [Jellicoe, The Grand Fleet, ch. 13c](https://www.wtj.com/archives/jellicoe/jellicoe13c.htm) — own-side funnel smoke; destroyer smoke screens.
- [USNI Proceedings, Jan 1920](https://www.usni.org/magazines/proceedings/1920/january/description-battle-jutland-concluded) — target lost behind a screen.
- [New World Encyclopedia: Battle of Jutland](https://www.newworldencyclopedia.org/entry/Battle_of_Jutland) — signals unreadable along smoky columns.
- [World History Encyclopedia: Battle of Jutland](https://www.worldhistory.org/audioplayer/en/2-2853/2406854eb689b8959a6c1e45e4f3d068/) — \~250 ships.

Data structures and methods:

- [OpenVDB documentation](https://www.openvdb.org/documentation/doxygen/) and [publications](https://openvdb.org/documentation/) — VDB, NanoVDB, hierarchical DDA.
- [SPGrid (Setaluri et al., SIGGRAPH Asia 2014)](https://pages.cs.wisc.edu/~sifakis/project_pages/SPGrid.html) and [arXiv 2512.11473](https://arxiv.org/html/2512.11473v1).
- [Amanatides & Woo 1987, fast voxel traversal](https://diglib.eg.org/items/60c72224-00f3-416d-9952-ee41e8c408da).
- [Runnalls 2007, KL approach to Gaussian mixture reduction](https://www.cs.kent.ac.uk/pubs/2007/2797).
- [CALPUFF user's guide (EPA-454/B-95/006)](https://www.osti.gov/biblio/218038), [SCIPUFF](https://ams.confex.com/ams/pdfpapers/161708.pdf), [HYSPLIT puff splitting](https://ready2.arl.noaa.gov/documents/Tutorial/html/conc_split.html).
- [Koschmieder visual range](https://reference.org/facts/visibility/reqevMCQ).
- [Counter-Strike 2 responsive smokes](https://esports.gg/news/cs-go/counter-strike-2-innovates-in-the-way-smokes-are-used-in-game) and [Gunnell's reimplementation](https://github.com/GarrettGunnell/CS2-Smoke-Grenades).

Project docs this builds on: [`muzzle-flash-smoke-research.md`](muzzle-flash-smoke-research.md) (puff A, §5.4 closed form, §7.5 API), [`blast-wave-visibles-research.md`](blast-wave-visibles-research.md) (jet punch on puffs), [`magazine-explosion-vfx-research.md`](magazine-explosion-vfx-research.md) (puff merging, smoke rendering), [`powerplant-model.md`](../simulation/powerplant-model.md) (k\_smoke per plant).
