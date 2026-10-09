# Procedural gun sounds — initial investigation
Status: final    Updated: 2026-10-03    Request: -

## Verdict
Yes, feasible. Best path is a **hybrid**: synthesize the blast pulse procedurally (that's where caliber lives), use a few base samples for the hard-to-synthesize textures (fireball crackle, mechanical clanks), and do distance/environment as processing. Bake variants offline or in a worker; runtime cost is then just playback.

## Anatomy of a naval gun shot (layers)
1. **Blast pulse** — Friedlander waveform `p(t) = P0 (1 - t/T) e^(-b t/T)`. Gives the "thump". Caliber-dependent.
2. **Fireball roar / crackle** — filtered noise with random impulse bursts. Partly caliber-dependent (density, low-pass).
3. **Body** — resonant response of barrel, turret, deck/hull: a modal filter bank (handful of damped sines). Ship/mount-dependent → main source of per-weapon identity.
4. **Mechanical** — breech, rammer, hoists, recoil. Sample-based; does NOT scale with time like the blast.
5. **Environment** — sea-surface reflection (Lloyd's mirror comb), echoes off other ships, rolling tail. Depends on listener, not weapon.
6. **Shell flight (for the target)** — supersonic N-wave crack + "freight train" roar of incoming rounds. Only heard down-range.

## The key scaling law
Blast scaling (Hopkinson–Cranz cube-root): pulse duration T ∝ W^(1/3), W = charge energy. Propellant mass scales roughly with caliber³, so **T ∝ caliber**: spectral peak drops ~1 octave per doubling of caliber.
- 5"/38: ~7 kg propellant. 16"/50 (Iowa): ~297 kg. Ratio ~44 → cube root ~3.5 ≈ caliber ratio 3.2. Checks out.
- Consequence: **varispeed resampling of one blast recording by (ref_caliber / caliber) is nearly physically correct** — slows and pitches down together. Apply it only to the blast/fireball layers; mechanical and environment layers stay unscaled.

## Distinct-but-related sounds per weapon
- Hash weapon ID → seed. Seed sets small persistent offsets: pulse duration ±5%, Friedlander decay b, crackle density, modal bank frequencies/Q, EQ tilt.
- Per-shot jitter smaller than per-weapon offset, so identity survives repetition.
- Mount/ship class picks the modal body (open mount vs. turret vs. casemate; destroyer vs. battleship hull).
- Barrel wear / charge (full vs. reduced) can nudge parameters for free variety.

## Distance (big lever for a naval game)
- Air absorption: strong high-frequency loss with range → far guns are pure low rumble.
- Sound delay 343 m/s: 20 km = ~58 s. Flash first, boom much later. Likely needs time compression for gameplay, but a few seconds of flash→boom delay sells scale.
- Multiple-reflection tail lengthens with distance (rolling thunder).

## Approach options
| Approach | Pros | Cons |
|---|---|---|
| Pure samples + pitch/EQ | Fast, sounds real | Repetitive, limited variants |
| Pure procedural (real-time) | Infinite variants, tiny footprint | Crackle/mechanics sound synthetic; CPU at barrage scale |
| **Hybrid, baked** (recommended) | Physical scaling + real texture; cheap runtime | Need a bake step / cache |

## Prototype v0.1 — Gun Voice Bench (2026-10-03)
Browser demo, fully synthesized (no samples yet): https://claude.ai/artifact/AeL5XsBUYQWHnWc7SJmwkH
- Recipe: T₊ = 5 ms × (caliber/127 mm); boom at ~0.45/T₊; fireball noise LP at 2600/s^0.8 Hz with Poisson crackle; 5 seeded body modes per mount type (aa/open/turret/heavy); breech clanks unscaled; sea reflection, 5 echoes, rolling tail, air-absorption LP 16 kHz/(1+1.2·km)^1.1; tanh overload at close range.
- 10 mounts (20 mm → 16"), two sibling pairs (5"/38 Mount 51/52, 16"/50 Turret I/II) for testing distinctness; custom caliber/mount/seed card.
- Listener range presets (30 m, 800 m, 5 km, 15 km), optional flash→boom delay at time ÷ 10, shot-to-shot jitter slider.
- Blind pair test logs to db collection `abtests` (target 65–80% correct = related but tellable); feedback form logs to `feedback`.
- Next: swap fireball/mech layers for real samples, compare against pure synthesis.

## Prototype v0.2 — Naval ZzFX Kit (2026-10-08)
Pure ZzFX (v1.4.0, MIT, ~1 KB micro build) take on the shot plus background music: https://claude.ai/artifact/Kqa8qtVbgfsLrSQm1Bd5ZV
- Gun = 5 ZzFX layers, each normalized then mixed: crack (high-passed noise burst, .45), boom (62 Hz sine sliding down, 1.0), roar (noise LP 520 Hz, .7), tail (noise LP 260 Hz + 3 delayed copies for sea reflection/echoes, .55), breech clank (unscaled, near range only, .22). Bus → tanh shaper → air low-pass (same 16 kHz/(1+1.2·km)^1.1 curve) → shared convolver reverb → compressor.
- Caliber via playbackRate = 203 mm ÷ caliber on blast layers only (T ∝ caliber law). Salvo = 3 guns, 110–240 ms stagger, ±4 % pitch.
- ZzFX gotcha: the `filter` param is negative = low-pass, positive = high-pass.
- Music "Night Watch": D minor, 66 BPM, 8 bars (Dm–B♭–F–C–Dm–Gm–B♭–A). Triangle pads, sine bass, noise swell, lead every 2nd cycle, two ship's-bell strikes (inharmonic partials ×2.76, ×5.4). Custom lookahead scheduler over `ZZFX.buildSamples` (ZzFXM isn't on npm); note buffers cached.
- To test: ZzFX is likely enough for UI, small guns and placeholder music; heavy guns may still need the Friedlander/modal synth of v0.1 or real samples.

## References
- Hacıhabiboğlu, *Procedural Synthesis of Gunshot Sounds Based on Physically Motivated Models* (2017)
- Mengual, Moffat, Reiss, *Modal Synthesis of Weapon Sounds* (QMUL, 2016)
- Andy Farnell, *Designing Sound* (MIT Press) — Pure Data gunshot/explosion recipes
- verniyyy/battle_ship PR #18 — web game doing Friedlander blast + sea reflection + echoes in a Web Worker (~600 ms per barrage bake)
- Arcella Sound — transient / mechanical / tail modular layering
- ZzFX — https://github.com/KilledByAPixel/ZzFX

## Open questions
- Engine/audio stack (Web Audio? FMOD/Wwise? Godot?) decides real-time vs. bake.
- How much time compression for sound delay.
- Listener perspective: on own ship vs. camera above fleet.
