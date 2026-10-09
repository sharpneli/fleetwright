# Handoff (2026-10-09)

Notes for the next session. Delete or replace this file once its items are picked up.

## Where things stand

- **The shipgen port is done.** All eight PORTING.md steps are ticked off, and all 20 tests pass (`dotnet test`).
  The only open item there is the user's sign-off of the look in the viewer.
- **Shipgen docs:** `docs/shipgen/` holds them. `README.md` covers inputs and outputs and has a Python-to-C# file
  map. `DECISIONS.md` (shipgen's old HANDOFF) has the user's decisions, which stand. `TODO.md` has the open items.
- **The Python repo** `../shipgen` is frozen. Tags: `golden-capture` (design and sprite goldens) and
  `golden-svg-capture` (SVG goldens).
- **Ways of working with the user:** commit and push to main after each verified step. Verify looks with offscreen
  readback (`-screenshot`, CLI PNGs), never screen captures. Both are in Claude's memory too.

## Next up, in the order I'd suggest (none of it agreed with the user yet)

1. **Route `-screenshot` through the engine's blit shader.** At the moment `CaptureScreenshot` uses
   `SDL_BlitGPUTexture`, so the screenshot skips the shader the window uses. Render the blit pipeline into an offscreen
   texture of the swapchain format and read that back, so the screenshot is what the window shows. Offered to the user;
   not done.
   - Background: the blit had a vertical flip (fixed in `blit.vert.glsl`, commit fe27ea0). The user checked the 3D test
     cube afterwards: lit from above, so the right way up.
2. **The bug fixes held back by "port as is":** `docs/shipgen/TODO.md` (Generator bugs, Hidden thresholds), one
   fix per commit. Each fix updates the goldens on purpose: regenerate them from the C# side, since Python is
   frozen. Decide with the user how golden updates are recorded.
3. **Smaller leftovers:**
   - In the viewer, turret shadows and the height-map shadow darken twice where they overlap. Python took the max.
   - The bake's opacity groups must hold opaque, unnested children; it throws otherwise.
   - README and the game csproj mention `Content/Models/`, which doesn't exist; there are no glTF files in the repo.
4. **Later (user):** the game-facing API of Shipgen and the asset path, refits, texture compression.

## Regression commands (all from the repo root, `-c Release`)

| what | command | expect |
|---|---|---|
| design side | `golden-check` | 371 of 371 |
| drawing (display lists) | `svg-check` | 371 of 371 (325 drawn) |
| sprite layout | `sprite-check` | 371 of 371 (70 with goldens) |
| GPU bake | `png-check` | worst IoU about 0.971 (small turrets); hulls ≥ 0.998 |
| robustness | `fuzz shipgen/designs/*.json --cases 1600` | no crash, hang or memory |
| sprites vs hitboxes | `design shipgen/designs/*.json --out X` then `verify X/*` | ALL OK (worst turret about 0.858) |

- `svg-check` and the SVG goldens only hold while the drawing is unchanged. Once a look or drawing change is
  intended, those goldens retire; `png-check` and `verify` remain.
- `png-check` uses a soft (alpha-weighted) IoU. Hard thresholds mostly measured 8x MSAA steps at edges.

## Traps met this session

- **Shell heredocs mangle escapes.** Bash heredocs feeding Python edit scripts turned `\n` and `\\` inside strings
  into real newlines or single backslashes, and broke C# string literals several times. Write edit scripts with
  the Write tool, or use the Edit tool.
- **GPU tests share one collection** (`[Collection("Gpu")]`). Two `GpuBaker`s at once used to kill each other's
  SDL video subsystem. `GpuBaker` now uses `SDL_QuitSubSystem`, which is reference-counted.
- **The CLI's `Args` treats any `--flag` as taking a value** unless it is listed as a switch (`--no-limits`,
  `--no-sprites`, `--previews`). Add new switches there, or the next argument gets swallowed.
- **`verify`'s hitbox mask samples pixel centres and adds its outline.** PIL's integer sampling made starboard and
  port mounts score differently; see PORTING.md Step 8.
- **Two `Looks` classes:** `Fleetwright.Shipgen.Looks` (validation only) and `Fleetwright.Shipgen.Render.Looks`
  (the real thing) both exist. Code in `Fleetwright.Shipgen.Cli` resolves to the former.

## How the pieces fit (for orientation)

- `ShipDesign.Build(design)` gives the ship dict. `ShipSprites.Build(ship, scale, mips, look)` gives scenes plus
  sprite.json. `Lower.Run(scene)` gives triangles and ops. `GpuBaker.Render` gives an `Image8`, and
  `ShipBake.Save` writes the PNGs and mip atlases.
- **The viewer** (`src/Fleetwright/ShipViewer.cs`, `-ship=`) bakes on the engine's device and uploads mipmapped
  textures. Shaders: `shipview*.glsl`. The bake shaders (`shipbake.*`) are embedded in the Render assembly.
- **Stencil layout in the bake:** bits 0-3 winding, bit 4 opacity-group flag, bits 5-7 clip depth (`GpuBaker` doc
  comment).
