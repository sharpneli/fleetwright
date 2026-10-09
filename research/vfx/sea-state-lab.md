# Sea State Lab
Status: final    Updated: 2026-10-07    Request: -

## Summary

Sea State Lab is the interactive prototype behind [`ocean-surface-research.md`](ocean-surface-research.md): a single-file WebGL2 page that renders a top-down open ocean for any Beaufort force, from orbit (~900 m per pixel) down to ~6 mm per pixel, with no water mesh. The runnable page is kept next to this note as [`sea_state_lab.html`](sea_state_lab.html) (open it in a current desktop browser; it needs WebGL2 and `EXT_color_buffer_float`). It was also published as the "Sea State Lab" Claude artifact; the HTML here is the same page.

This note is the Markdown version of that artifact: what the page simulates, what each control does, and what the readouts mean. The research, measurements and design reasoning are in [`ocean-surface-research.md`](ocean-surface-research.md); this note does not repeat them.

## Details

### What the page computes

1. **Spectrum (CPU, once per change).** JONSWAP wind sea from 10 m wind speed and fetch, capped at Pierson–Moskowitz full development, plus a JONSWAP swell (γ = 5) scaled so its own Hs matches the slider. Directional spreading is Longuet-Higgins cos^2s(θ/2); the wind sea's s falls away from the peak, swell uses s = 40. Beaufort maps to the mid-range 10 m wind (0.2–36 m/s); the WMO "probable" wave height is shown for comparison.
2. **Three FFT cascades (GPU, fragment shaders).** Tiles of 1987 m, 251 m and 31.7 m, band-split at 4 tile-wavelengths so no wave is counted twice. Each step runs a spectrum pass, 2·log₂N radix-2 FFT passes and a combine pass that writes:
   - a moments texture (slope x, slope z, slope x², slope z²), mipmapped;
   - a displacement texture (Dx, Dz, height, foam), mipmapped and ping-ponged so foam can persist.
3. **Shading (one full-screen pass).**
   - Undo choppy horizontal displacement with one fixed-point step (faded out beyond ~1.5 m/px).
   - Hex-tile bombing on cascades 0–1 (after Mikkelsen 2022): each hexagonal cell samples the tile at a random offset and small rotation, blended to preserve variance.
   - LEAN-style slope moments: mean slope plus filtered variance, topped up to the Cox–Munk clean-surface slope variance for the wind, so unresolved waves become BRDF roughness and the sun-glint patch has the observed size at any zoom.
   - Gust patches: 1–9 km noise that modulates roughness and foam at theatre zoom.
   - Foam from the Jacobian of the choppy displacement, cascade 1 only, with exponential decay, crest clustering and wind-aligned wave groups; a mean-preserving noise breakup that fades to its average once cells are sub-pixel.
   - Lighting: Fresnel sky reflection, an anisotropic Gaussian glint (Cox–Munk / Bruneton), a constant deep-water body colour, simple exponential tone map.
   - A 114 m destroyer planform with a sun shadow, for scale only.
4. **Foam calibration loop.** Every 12 frames the page reads the mean foam of cascade 1 from its 1×1 mip and nudges the Jacobian threshold until rendered whitecap cover matches the target: foam amount × Monahan (1980) W = 3.84·10⁻⁶·U^3.41, capped at 12 % × (amount / 0.35).

### Controls

| Group | Control | Range (default) | Effect |
|---|---|---|---|
| Wind sea | Beaufort | 0–12 (5) | 10 m wind speed; drives the wind-sea spectrum, Cox–Munk roughness and whitecap target |
| | Fetch | 10^1–10^3.3 km (~160 km) | Fetch-limited JONSWAP until the PM cap is reached |
| | Wind from | 0–359° (250°) | Compass direction the wind blows from |
| Swell | Height Hs | 0–6 m (1.2 m) | Swell significant wave height |
| | Period Tp | 6–20 s (13 s) | Swell peak period; the readout also shows its wavelength |
| | From | 0–359° (300°) | Swell direction |
| Surface | Choppiness λ | 0–1.6 (1.0) | Horizontal displacement scale; also sets how easily the Jacobian folds into foam |
| | Foam decay τ | 0.5–15 s (4 s) | Foam persistence |
| | Gust patches | 0–1 (0.5) | Strength of the large-scale roughness/foam modulation |
| | Foam amount | 0–1 (0.25) | Fraction of Monahan's whitecap cover used as the calibration target |
| | Wave groups | 0–1 (0.4) | Clustering of breaking into downwind-travelling groups |
| | Crest clustering | 0–1 (0) | Concentrates breaking on crests of the dominant waves |
| | Calibrate whitecap cover | on | Enables the foam feedback loop |
| Light | Sun elevation / azimuth | −4–88° (38°) / 0–359° (150°) | Sun direction |
| | Glint eye FOV | 0–70° (40°) | Perspective for the glint view vector; 0 = orthographic |
| | Exposure | 0.2–3 (1.0) | Pre-tonemap exposure |
| Simulation cost | FFT size | 64 / 128 / 256 (128) | Per-cascade resolution; readout shows texture memory |
| | Sim rate | every frame / 30 Hz / 15 Hz | FFT update rate |
| | Cascade 0 / 1 / 2 | on | Toggle each cascade |
| | Hex-tile bombing | on | Anti-tiling on cascades 0–1 |
| | Loopable dispersion | off | Quantises ω to multiples of 2π/64 s so the field loops (bakeable) |
| | Pause time | off | Freezes the simulation clock |
| Inspect | View | Shaded | Debug views: normals, slope variance, foam coverage, height, cascade share of variance, tile seams |
| | Scale reference hull | on | Shows the 114 m destroyer planform |

Zoom presets along the top of the view: Orbit (~1700 km across), Theatre (75 km), Squadron (6 km), Ship (650 m), Deck (75 m), Detail (11 m). Drag pans; wheel or pinch zooms about the pointer.

### Readouts

The Beaufort card shows wind in m/s and knots and the Douglas sea state for the current Hs. The table shows:
- Hs of the target spectrum and of the discrete spectrum actually uploaded, beside the WMO probable height for the Beaufort number;
- wind-sea peak period and wavelength, and whether the sea is fetch-limited or fully developed;
- Cox–Munk slope variance, the part the FFT resolves, and the BRDF top-up;
- whitecap target vs rendered cover (green when within tolerance) and the current foam threshold;
- GPU sim and shade time (when `EXT_disjoint_timer_query_webgl2` is available) and frame rate.

`window.__ocean.heightStats()` reads back each cascade's realised height variance against the expected value, for verification.

## Recommendations for this codebase

- Treat the page as the reference implementation for a water pass in the SDL_GPU renderer. Its GLSL ES 3.0 shaders (spectrum, FFT, combine, shade) can be ported to the GLSL 450 sources in `Content/Shaders/Source` and compiled by `compile_shaders.bat` like the existing shaders.
- Follow the engine recommendations in [`ocean-surface-research.md`](ocean-surface-research.md) §4.2 rather than copying the prototype's pass structure: FFT in compute (about 2 dispatches per cascade instead of ~16 fragment passes), RGBA16F working set, sim at 30 Hz (15 Hz for cascade 0).
- Keep the CPU spectrum code (`seaParams`, `buildH0`) deterministic and shared with gameplay, so ship buoyancy can evaluate the same spectral bins on the CPU (§5.2 of the research note).
- Water-layer channels (mean slope, slope variance, foam) should follow the shared contract in §5.1 so wakes ([`wake-vfx-research.md`](wake-vfx-research.md)), slicks ([`sinking-vfx-research.md`](sinking-vfx-research.md)) and splashes ([`shell-splashes.md`](shell-splashes.md)) composite into one shading pass.

## Sources

- Full source list and measurements: [`ocean-surface-research.md`](ocean-surface-research.md) (Sources section).
- Cox & Munk (1954), sea-surface slope statistics; Monahan & O'Muircheartaigh (1980), whitecap coverage; Hasselmann et al. (1973), JONSWAP; Pierson & Moskowitz (1964); Longuet-Higgins directional spreading; Mikkelsen (2022), "Practical Real-Time Hex-Tiling", JCGT — as named in the prototype's code and cited in the research note.
