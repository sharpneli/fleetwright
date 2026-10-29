# HDR output (plan drafted 2026-10-29, for later)

An optional HDR swapchain, so the world view can show light past paper white on an HDR display: the beams, star shell,
flashes, fires and glints with headroom instead of rolled to white at 1. The user asked for the plan after the night's
tuning (`night-lights.md` Q10f), where every bright light had to be squeezed into SDR. **Optional**: SDR stays the
default and keeps its look exactly; HDR is a flag or setting that falls back to SDR where it isn't offered.

## What there is today (checked 2026-10-29)

- **The swapchain is SDR**: `SDL_GetGPUSwapchainTextureFormat` gives `B8G8R8A8_UNORM` (Engine.cs `InitDevice`); no
  `SDL_SetGPUSwapchainParameters` call anywhere.
- **The scene renders into a float target** (`R16G16B16A16_FLOAT`, 8x MSAA, `Engine._mainTarget`), but holds
  display-referred, sRGB-coded values capped at 1: every lit shader ends in `shoulder` (`shiplight.glsl`, knee
  `LightTable.Knee`) or a roll to white on luminance (flashes, beams, star shell), then `linearToSrgb`, which clamps
  to 0-1. 12 shaders call a shoulder or roll, 13 `linearToSrgb`.
- **The blit** (`blit.frag`) copies the resolved target to the swapchain as it is.
- **ImGui** draws straight onto the swapchain in its format (`ImGuiRenderer`, `imgui.frag`: vertex colours as given).
- **Captures** (`-screenshot`, `FrameCapture`) blit into a texture of the swapchain's format and read it back.
- **Screen blending** (`EnableBlendingScreen`: src + dst·(1 − src)) for the flashes, beams and star shell: correct only
  while src ≤ 1.
- **The night's response** (`night-lights.md` Q10f): `LightTable.LightKnee` and the beams' and star's departures
  (`BeamRenderer.Gain`, `OverHull`, `FlareRenderer.HaloGain`) were set for SDR's range.

**The probe (2026-10-29, the user's machine, Vulkan, Windows HDR on):** `SDL_WindowSupportsGPUSwapchainComposition`
says yes to all four: `SDR`, `SDR_LINEAR`, `HDR_EXTENDED_LINEAR`, `HDR10_ST2084`. The window's properties:
`SDL_PROP_WINDOW_SDR_WHITE_LEVEL_FLOAT` 3 (paper white ~240 nits, Windows' SDR content slider),
`SDL_PROP_WINDOW_HDR_HEADROOM_FLOAT` 4.23 (peak ~1,000 nits), `SDL_PROP_WINDOW_HDR_ENABLED_BOOLEAN` true.

## The model

- **scRGB** (`HDR_EXTENDED_LINEAR`: `R16G16B16A16_FLOAT`, linear, 1.0 = 80 nits) rather than HDR10's PQ: no transfer
  function to write, the compositor does the rest. PQ only if a driver offers no other.
- **Paper white** = the window's SDR white level (× 80 nits): what 1.0 in our display-referred values means, so the
  UI and the day look sit where the rest of the desktop's SDR content does.
- **Headroom** = the window's HDR headroom over paper white: our new peak. On SDR it's 1, and every curve is as today.
- **One peak for the look**: a `peak` in the light block (`LightUniforms`); the shoulders and the rolls to white approach
  it instead of 1. Below the knee nothing changes, so the day look holds and only highlights (glints, flashes, beams,
  stars, fires) gain range.
- **The values stay display-referred and sRGB-coded** in the main target (the blending convention the look was tuned
  in), extended past 1 (sign-preserving, unclamped encode); the blit decodes to linear and scales by paper white.

## Steps

**H1 The swapchain, the same look** (~half a day).
- `-hdr` (and later a setting): after claiming the window, if `HDR_EXTENDED_LINEAR` is supported, set it with
  `SDL_SetGPUSwapchainParameters` (present mode as now); else stay SDR and say so on the console.
- The blit: decode the stored sRGB-coded value to linear, times paper white / 80 nits (a uniform), out to the float
  swapchain. Re-read the white level and headroom on `SDL_EVENT_WINDOW_HDR_STATE_CHANGED` and display changes.
- ImGui: the same decode and scale (a uniform or a second pipeline), so the UI is the desktop's paper white.
- Captures: their own SDR texture and blit (today's path), so `-screenshot` and the side-by-side comparisons don't move.
- Check: an SDR and an HDR capture of a day, a dusk and a night scene look the same on the HDR display (the look
  unchanged, only the swapchain).

**H2 The peak** (~1-2 days).
- `peak` in the light block, from the headroom (1 on SDR). `shoulder(x, k)` → an approach to the peak; the rolls to
  white (`muzzleflash`, `beam`, `flare`, `magazine_*`, `shoulderHue`) over it.
- `linearToSrgb` → an extended encode past 1 where a value may exceed it (the lit shaders' outputs); the clamp kept
  where 0-1 is meant (masks, alphas).
- Screen blending → additive, capped by the peak, for the lights (flashes, beams, star shell); premultiplied draws as
  they are.
- The table's marks, the overlay, symbols and labels stay at paper white.
- Check: SDR captures byte-close to today's (peak 1); on the HDR display, day glints and the night's lights with
  headroom.

**H3 The night on HDR** (tuning with the user).
- With ~4x headroom the night's departures can relax: `LightKnee` higher, `BeamRenderer.Gain` and `OverHull` toward 1,
  `FlareRenderer.HaloGain` up. Per display: the response follows the peak (one set of knobs, scaled by it, rather than
  two looks).
- The night eye's levels (`NightEyeLevel`) are unchanged: the dark is the same on both; only the lights gain range.

## Risks

- The look is display-referred throughout; H2 touches many shaders. Mechanical, but every view wants a day, dusk and
  night capture against today's.
- HDR through SDL on Vulkan is less travelled than D3D12; other drivers may offer only SDR (the fallback) or only PQ.
- Captures stay SDR, so HDR looks are judged on the display by the user, not by readback.

## Not here

- HDR screenshots (EXR/JXR export), auto-exposure (never: `no-exposure-dimming`), wide-gamut colour (Rec. 2020
  primaries: the look's colours are sRGB's).
