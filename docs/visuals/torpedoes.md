# Torpedoes in the world view: tracks, launches and hits (plan agreed 2026-10-15)

The table already has its marks (`docs/visuals/battle-marks.md`, `MapScene.Shells.cs` `DrawTorpedoes`): a thin white
line laid behind each torpedo and fading over 90 s, a faint dot at the torpedo, and at a hit a white disc with a warm
core and a ring. They're drawn at pixel sizes in the world view too, because nothing else is. This plan covers the
**world view** only, as `splashes.md`, `muzzle-flashes.md` and `magazine-explosions.md` did: what it draws × the
world's share (`WorldView.Factor`), the table's marks × (1 − it).

Scope: a torpedo from its launch to its end, by day, dusk and night, in a calm and a rough sea. What it makes:
- the **bubble track** on the water, at its true width and place behind the torpedo;
- the **torpedo** itself under the water;
- the **launch**: a deck mount's impulse puff and entry splash, a submerged tube's air boil;
- the **end of the run**: the track stops, and the bubbles already released go on surfacing for a while;
- the **hit**: the column against the hull, its smoke, its foam.

What the hit does to the ship (the hole, the list, the flooding) is drawn as it is now.

## Research

There is no research note for torpedoes, unlike the other passes. The web had little that was quantitative (searched
2026-10-15). What sources do say:
- Compressed-air and wet-heater torpedoes leave a bubble track from their exhaust (mostly the air's nitrogen). It's
  seen at 800-1,000 yards in a calm sea and is hard to make out even at 100 yards in a rough one (*A Submarine
  Torpedo: What It Is and How It Works*, JSTOR 45328346).
- The track is what lookouts watch for (US Navy's 1917 Armed Guard notes). Ships at Jutland saw tracks from aloft and
  combed them (Jellicoe).
- Steam torpedoes of WWII still left a track; electric ones (the Mark 18, the G7e) and Japanese oxygen ones left almost
  none.
- A hit throws up a column of water and smoke, higher than the masts in the Pearl Harbor photos. Nobody gives a
  measured height.

Everything else comes from physics and is flagged below as an estimate (in `TorpedoLook`), to be checked against
photos in R6. A research request is filed (`research/REQUESTS.md`, 2026-10-15, "Torpedo tracks, launches and hits as
seen"). The plan doesn't wait for it; when the note lands its numbers replace the estimates.

## What the physics gives

| What | Estimate | From |
|---|---|---|
| Where the track starts | The bubbles rise from the torpedo's depth d (the sim's `DepthM`: a share of the target's draught, ~3-5 m). The first reach the surface after d / w_fast and the bulk by d / w_slow, with w_fast ≈ 0.8 m/s (a bubbly plume drags its water up) and w_slow ≈ 0.25 m/s (mm bubbles on their own). At 4 m and 41 kn: the head 5 s (105 m) behind the torpedo, full strength 16 s (340 m) behind | Bubble terminal velocities; the table's 5 s lag agrees |
| Width | ~1 m + 0.25 d at the surface (the plume spreads as it rises), then spreading as √(age), as the ships' foam lanes do: ~2 m at the head to ~6-8 m after 90 s | Estimate |
| Look | A pale line of aerated water with sparse, fine surface foam where the bubbles burst. Not the ships' churned white: no streaks, no clumps | Photos of tracks; the wakes' two layers (`wakes.md` K5, fourth round) |
| Life | Strongest while the bubbles are still surfacing, then fading. The table's 90 s, kept as the one life (R1) | The table's mark |
| In a seaway | Lost in the whitecaps and the waves' glitter | JSTOR above; the sea's own shading does this once the track is a film |
| The torpedo | A 7 m by 0.45-0.53 m body 3-5 m down. In North Sea water (diffuse attenuation ~0.2-0.5 /m) its contrast is e^(−2Kd) ≈ 0.1-0.45: a faint dark shape, 7 px long at 1 m/px | Water optics |
| Launch, deck mount | Thrown out by a small impulse charge (cordite in the RN, air in others): a puff of smoke at the tube's mouth. The torpedo drops 3-5 m into the water 5-10 m out from the side, raising a small splash (a few metres) and a boil. It dives below its set depth and recovers over 100-200 m | Period practice; an estimate |
| Launch, submerged tube | The air that ejects it surfaces beside the ship as a boil, a few metres across | Estimate |
| End of the run | The engine stops and the torpedo sinks (set to in war). No new bubbles, but those already released surface for another d / w_slow | Physics |
| Hit | A contact explosion of W kg (the row's `ChargeKg`, ~180 kg in the Great War) 3-5 m down against the hull. The column H ≈ 12 W^⅓ m (68 m at 180 kg, 80 m at 300 kg), ~0.4 H wide, rising for ~3 s and down by ~9 s (the table's column time). White water, greyed by the explosion's smoke | An estimate, to be held up against the Pearl Harbor photos |

## What the sim gives today, and what it needs

`Snapshot.Torpedoes` (`TorpedoesView`) gives each torpedo running: where and when it entered the water, its heading,
its speed, when its run ends, the shooter and the ship it'll hit. `TorpedoEnds` gives its end: where, when, the ship
hit or −1, and the calibre. The look also needs:
- the **depth** (where the track starts and how wide it is);
- the **launcher's kind** (mount or tube, for the launch) and its **height** over the water (the drop);
- **how the run ended**: the hit, the run's end, or a failure on the way (the track's tail is the same for all three;
  only a hit draws a column);
- the **charge** at a hit (the column's height).

R0 adds them to the record. They're facts the sim has, so nothing about any result changes. R0 also gives each
torpedo a serial: the map now keys the runs it remembers on `(LaunchMs, Shooter, Forward.X)`, which two torpedoes of
one spread could share.

## What we build

| Real | Here | Kept |
|---|---|---|
| Bubbles rising from depth, spread in size | **A closed-form surfacing curve** along the track: how much of the gas released at a point has surfaced, by its age since the torpedo passed, between d / w_fast and d / w_slow, then fading over the track's life (`TorpedoTrack`, the one model, which the table's line also reads) | The soft head well behind the torpedo; the tail that goes on surfacing after the run ends |
| The track on the water | **A ribbon** along the run's great circle, built on the CPU each frame from the closed form, in the **foam lanes' draw** (`FoamTrails`, `wakeribbon`) with a bubble style: mostly the aerated layer, a sparse fine speckle, none of the 20 m streaks | Lit by the sea's shading like the ships' lanes, under the hulls, lost in a rough sea. No new pass, target or draw |
| The torpedo under the water | **A dark, soft-edged quad** at its true place, size and heading, at its physical contrast (in the wake target as a darkening the sea's shading applies, or premultiplied after the sea, whichever is cheaper, R3) | Emphasised past its physical contrast (decision 2) |
| The impulse puff | **A `SmokeBook` puff** at the tube's mouth, grey-white, small | On the gun smoke's system, as the magazine's after-smoke |
| The entry splash and boil | **A `SplashBook.Add`** splash the size of a small shell's (a few-metre column) where it falls in, and its foam | One splash model |
| The tube's air boil | **A foam-only splash** (the splash foam's disc, no column) beside the ship over the tube | |
| The dive and recovery | The track's depth runs deeper over its first 100-200 m, so the bubbles surface later and wider there: a break, then the track | |
| The hit's column | **The splash model's torpedo kind** (`splash.glsl`, as `splashes.md` "Later" planned; decision 1): height and width from the charge, no lean, a share of smoke grey in the water's colour. Through the effects camera, with its shadow and foam | One column model, its shadow and foam for free |
| The hit's smoke | **A `SmokeBook` puff** of the explosion's grey-brown, drifting off | |
| The flash | At night, **a dim, short warm light** in `FlashLights` at the hull's waterline (it's underwater, so little shows). By day none | |

## Time

- **The track ages on world time**, as the ships' foam lanes and the table's line do. Where the torpedo is follows
  from its launch in closed form, so the track sits under the torpedo at any rate. The speckle's churn is on the sea's
  clock.
- **The launch's puff and foam and the hit's foam and smoke** are on the sea's clock; **the hit's column** on the
  effects' clock (`SplashBook` already does both). Never thinned: a torpedo hit is rare and the eye must catch it.
- **The coarse tier** draws the table's marks only.

## Restrictions

- **Display only.** The sim gains a few facts on its record (R0) and reads nothing drawn. The lookouts
  (`TorpedoWatch`) keep seeing a torpedo by its range, as now.
- **One model.** `TorpedoTrack` holds the surfacing, width and life, and the table's line and the world's ribbon both
  read it. The table's `TrackS` and `TrackLagS` move into it.
- **Realistic, then tune.** The estimates above come first. Departures are named knobs in a `-tracks=` flag
  (`TorpedoLook`: `-torpedo=` is already the dev's spread), each recorded here.
- **Lightweight, measured.** 64 tracks in view at 1 m/px, 1080p: the ribbons under 0.05 ms GPU (`-gputime`), their
  build under 0.05 ms CPU. Nothing allocated a frame: the ribbon's vertices go in the foam lanes' grown array.
- **Pipeline:** no new pass and no new target. The track and the foam go in the wake pre-pass, the column and the
  smoke with the splashes and the gun smoke.

## Steps

- **R0. The record.** `TorpedoesView` gains the depth, the launcher's kind and height, and a serial. `TorpedoEnds`
  gains how the run ended and the charge. The map's remembered runs become a `TorpedoBook` (in `Map/Torpedoes/`, as
  `ShotBook` and `MagazineBook`), keyed by the serial, holding each run until its track has faded. Tests: a failed
  torpedo records a failure, a hit records the charge, two torpedoes of one spread keep two entries.
- **R1. The model.** `TorpedoTrack`: the surfacing curve, the width, the life, the dive near the start, the tail after
  the end. The table's line reads it. Tests: at 4 m and 41 kn the head is 5 s behind; the strength rises, then falls,
  never negative; a run that ends goes on surfacing for d / w_slow, then nothing; the table draws as before (its lag
  was the same 5 s).
- **R2. The track.** The bubble style in `wakeribbon` (a kind on the vertex, kept at 32 bytes), the ribbon built along
  the great circle (straight, so a few points, thinned by the pixel as the lanes are), the table's line × (1 −
  factor). Stills of a spread (the dev's `-torpedo=SHIP:TARGET:N`, with a `-tracks=at:S` still as the magazine's) at 0.5, 1 and
  4 m/px; noon, 19:30 and 23:30; a calm sea and a rough one.
- **R3. The torpedo.** The dark shape at its physical contrast. Stills beside R2's, and the table's dot × (1 −
  factor).
- **R4. The launch.** The impulse puff, the entry splash and boil, the dive's break in the track, the tube's air boil.
  Stills of a destroyer's mount and a battleship's tube at 0, 1, 3 and 10 s.
- **R5. The hit.** The splash model's torpedo kind, its smoke puff, its foam, the night glow; the table's column, ring
  and flash × (1 − factor). Stills at 0.5, 2, 4 and 9 s, noon and night, held against the Pearl Harbor photos for the
  height.
- **R6. Time, cost, tuning.** Live at 1x, 10x, 60x and 240x, and through the zoom boundary; the cost with 64 tracks in
  view (`-gputime`, DevRelease `-frametimes`); tuning by the user's eye.

### Done

**R0 (2026-10-15).** The sim's torpedo takes its `Serial` and whether it `Failed` when fired. `TorpedoesView` gains
the serial, the depth, the shooter's class, the launcher and the torpedo's row (the map reads the launcher's kind and
place, and the charge, from the class). `Snapshot.TorpedoEnds` is now a `TorpedoEndsView` with the serial, how the
run ended (`TorpedoEnd`: hit, run out, failed) and the charge. No hash or result moved. The map's `Map/Torpedoes/
TorpedoBook` keeps each run by its serial until its track has faded, and the table's marks read it. Tests:
`TorpedoTests.The_snapshot_tells_each_torpedo_apart_and_how_its_run_ended`, `TorpedoBookTests`.

**R1 (2026-10-15).** `Map/Torpedoes/TorpedoTrack`: the surfacing (smoothstep from d / 0.8 to d / 0.25 m/s after the
torpedo passed), the strength (that × the square of the life left, the table's old fade), the width (1 m + 0.25 d,
spreading 0.5 m²/s), a mount's dive (to twice its depth a third of the way along 150 m), the head's lag and the keep.
The table's line reads it (its 5 s lag ramp is now the surfacing). `TorpedoLook` and `-tracks=` (fast, slow, life,
width, spread, dive, on). Tests: `TorpedoTrackTests`.

**R2 (2026-10-15).** `TorpedoRibbons` lays each run in the book as a ribbon along its great circle, from its head (d / 0.8
behind the torpedo) back to where it has faded, in the foam lanes' draw: `FoamVertex` gained a kind (36 bytes), and
`wakeribbon.frag` a bubble branch: a patchy film of aerated water (the lanes' 0-0.4 layer), a sparse fine speckle of
bursting bubbles churning while young, and the slick (negative variance; `seaShade` keeps a quarter of the sea's own).
The table's line fades out over the cross-fade. Knobs: `gain`, `floor` (2 px), `slick` (0 until judged), `at:S` (every
torpedo drawn S seconds after the newest launch, for stills).

Seen (a destroyer's spread at a super-dreadnought, 2.4 km):
- at 1 m/px noon: thin pale lines, their heads soft, starting ~150-200 m behind the torpedoes (a 6 m depth for a big
  ship's draught); at 0.3 m/px a soft pale turquoise film a few metres wide, patchy; the speckle barely shows;
- at 4 m/px: faint lines; under the floor the film thinned by all of its widening was nearly gone, so it's thinned by
  the square root of it (a named departure: the track is the player's cue zoomed out);
- at night: dim grey lines under the moon (the lookouts' 400 m by night agrees);
- the slick at 0.1-0.4 in the glint: the track darker and smoother, the glint breaking into sparkles along it. For the
  user to judge (decision 3); off until then.

**R3 (2026-10-15).** `TorpedoRenderer` (`torpedo.vert/.frag`): each torpedo running a dark, soft shape the colour of
deep water under the surface at its place and heading, 13.5 calibres long (a 21-inch 7.2 m), blunt head and tapering
tail, its edge blurred by its depth; one instanced draw after the sea and before the ships. Emphasised (decision 2):
its darkness is `Body` 3.5 times the water's physical contrast e^(−2·0.25·d) (0.47 at 4 m, 0.35 at 6 m, at most 0.75),
and it's never shorter than `BodyFloorPx` 10 px on the screen. The table's dot fades out over the cross-fade. Seen at
40 s: at 0.3 m/px soft dark shapes a hundred metres and more ahead of their tracks; at 1 and 4 m/px dark dashes that
lead each track.

## Decisions (the user, 2026-10-15)

1. **The hit's column now**, built on the shell splash's column (`splash.glsl`) or closely after it: a torpedo kind of
   the one model, not a model of its own.
2. **The torpedo is emphasised** beyond its physical contrast: where it is matters to the player, and the track runs
   a hundred metres or more behind it. A named departure (`-tracks=body:G`), set by eye in R3.
3. **The slick is judged** from R2's captures: built behind a knob, kept or dropped by what they show.
4. **Build on the estimates** now; the research note's numbers replace them when it lands.

## Later

- **Wakeless torpedoes**, for the later cuts: a propulsion field on `TorpedoRow` (cold air, wet heater, oxygen,
  electric) scaling the track, and the lookouts' `TorpedoSeenM` with it, so the look and the sim agree.
- Night bioluminescence along the tracks, where the waters have it.
- A torpedo broaching, porpoising or running a circle.
- The torpedo's own surface wave when it runs shallow.
- The hit's effects on the ship: the hole at the waterline, oil spreading from the ruptured bunkers, the list's water
  rushing in. With the hit-effects pass.
- Ring waves from the hit in the wake target's slopes, as for the splashes.
