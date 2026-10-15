# Battle marks: shells, splashes, torpedoes, hits and damage on the table (2026-10-15)

The battle's stylised marks (`MapScene.Shells.cs`, `MapScene.Damage.cs`) were placeholders: shells as short dashes,
splashes as thin rings, nothing at the guns, and icons that look the same sunk or whole. This pass makes them the
real thing **for the table**: the zoomed-out chart where ships are icons and nothing is at its true size. The world
view's own effects (columns over the waves, foam, smoke) are another session's; until then these marks stay drawn in
both, as `WorldView` already says of them.

Research behind it: `research/vfx/shell-splashes.md` (anatomy, sizes and timings by calibre, dye, the LOD tiers: its
"T0 stain" and "glint" tier is this one), `muzzle-blast-water-vfx.md`, `wake-vfx-research.md`,
`sinking-vfx-research.md`, `magazine-explosion-vfx-research.md`.

## What the table needs from a mark

A chart isn't a photograph from altitude: at 25 m a pixel a 12" splash is a third of a pixel and a shell is nothing.
So every mark is a **symbol drawn at a pixel size**, sized by calibre, timed by the physics. It must be:

- **Light.** Everything is a few strips and fans in the overlay's line buffer (`MapLines`), no new pass, target or
  texture; no allocation per frame.
- **Recognisable at a glance**, and apart from each other: a shell from a torpedo, a miss from a hit, our fire from
  theirs, a ship burning from a ship sinking.
- **Of the map's style**: flat colour, soft edges, no particles or textures.

## The language

Three rules, shared by every mark, so the player learns them once:

1. **What moves is a streak**: a bright head and a tail that tapers and fades to nothing behind it (the user's
   pointer). A shell's tail is short and hot; a torpedo's track is long, thin and white, and lies on the water where
   it was laid, fading with age.
2. **What happens is a pop, then a trace**: a short bright burst that catches the eye (the splash's column, a hit's
   flash, a gun's flash), then a faint residue that lingers long enough to read the pattern after the fact (the
   foam ring of a salvo's fall).
3. **Colour says what it is**, the side's tint says whose: white is water (splashes, tracks), warm is fire and
   explosions (shell heads, flashes, burning), grey is smoke. The side's tint (the icons' own, `SideTints`) goes on
   shell tails and, faintly, on the foam rings, as the period's dye loads did; the selected ship's fire stays bright
   and the rest dimmer, as before.

Timing is world time, from the physics (a 12" column rises in ~3.3 s and is down by ~8 s; foam lingers 20 s). A pop
also has a **floor in real time** (~0.15-0.25 s on the screen), so at 30x it still shows as a blink instead of
vanishing between frames; the floor is display-only and bounded by what the snapshot keeps.

## The marks

**Shell.** A comet: a warm-white head (2-3 px by calibre) and a tail in the shooter's tint covering the last ~1.5 s
of flight, tapering to nothing, bounded to 8-40 px. The head swells toward the apex (up to ~1.6x) and shrinks as the
shell plunges: from above, the arc reads as the shell rising toward the eye and falling away, and long-range plunging
fire reads apart from a flat close-range shot.

**Gun flash.** At the muzzle, the instant a shell leaves: a warm glow, a fraction of a second (with the floor). A
broadside lights the ship up; it says who is firing without a line. Gun smoke waits for the smoke field.

**Splash (a miss).** The pop is the column: a white soft disc that grows to its calibre's size over the rise and
fades through the fall (`shell-splashes.md`'s table: ~8 s for a 12", ~5 s for a 5"). The trace is the foam: a thin
ring that widens a little and fades over the snapshot's 20 s, faintly tinted by the shooter's side. A salvo leaves
its pattern on the water for a while: over, short, straddle.

**Hit.** A warm flash (white core, orange glow), bigger and shorter than a splash, with a few sparks flying out and
fading. No water column. The lasting trace of a hit is on the ship (below).

**Torpedo.** The track: a thin white line laid on the water behind the torpedo, fading with its age over ~90 s of
run, so a spread is a fan of long fading lines. The torpedo itself is a faint dot just ahead of where the track
begins (the bubbles surface behind it). A torpedo's hit: a tall white column (a big white pop with a warm core, longer
than any shell's) and a ring.

**Ship damage, on the icon.** Ships on the table are icons, so their state shows on the icon, from what an observer
sees (the snapshot's attitude, parts and fires: an enemy in sight shows the same):

- **Settling: the sea creeps over the icon.** As she settles, the icon's deck goes under from the end and side she's
  going down by: trim takes the bow or the stern, a list one side, sinkage all round. A ship holding her water looks
  whole; one going down sinks into the sea mark by mark.
- **Heel narrows it**: a ship heeled over shows less deck from above, and a capsizing one rolls to a sliver.
- **Wrecked parts** leave dark scorches on the icon where they are along her length.
- **Fire**: a flickering warm glow on the icon at the burning spaces (mapped onto the icon, not the true hull), and a
  smoke streak drifting downwind (the weather's wind), longer and darker the more is burning.
- **Going down**: the icon goes under over her last minutes; broken in two, its halves part; a magazine, a great
  flash and a dark pall. Then the wreck marker, as now.

## Steps

- **M1. Primitives.** `MapLines` gets per-point colour and width (tapers) and a soft disc (a fan, centre to rim);
  `MapScene` a streak and a pop on them, and the real-time floor.
- **M2. Shells and guns.** Streaks with the apex swell; the gun flash.
- **M3. Splashes, hits and torpedoes.** Pop and trace; sparks; tracks; the torpedo's hit.
- **M4. The icons.** Fires and smoke on the icon; the sea over the icon, heel, scorches; going down.
- **M5. Tuning by eye** with captures at table zooms (`-screenshot`), and the user's eye on the live fight.

Knobs live as named constants at the top of each file, with the physical number they start from beside them.

**M1-M4 done 2026-10-15**, tuned from captures; the user approved the live look as it is, so M5 has nothing left to do for now. Where they live:
`MapLines.Point(x, y, rgba, width)` and `MapLines.Disc`; `MapScene.Shells.cs` (streaks, the gun flash, splashes,
hits, torpedo tracks); `MapScene.Damage.cs` (scorches, fires, smoke, going down, the magazine); the icon's sea in
`ShipDrawList.IconSea` and `shipmap.frag`'s icon path. Notes from building it:

- **Departures from the physical, named:** `ShipDrawList.IconFreeboard` = 0.5 (the icon's deck at half the true
  freeboard, so settling shows before the deck is awash); fire smoke and the magazine's pall are mid-grey, not
  black (black smoke vanishes on the chart's navy sea); fire smoke reaches 1.5 km or 40 px.
- **Kept past the snapshot:** a torpedo's track outlives the torpedo (the map keeps each run's record until its
  track has faded); a magazine's blast and pall are drawn from the remembered loss, since the ship leaves the
  snapshot within seconds.
- **Cost:** discs and rings take fewer sides when they're small; a Jutland-sized fight is estimated at ~100k overlay
  vertices of the ~262k the buffer holds (F2's stats show the live count).
- **Left for later:** heel narrowing the icon and a ship broken in two parting in halves (the sea over the icon
  carries both for now); gun smoke (the smoke field); ricochets and the salvo's ripple (sim-side timing); per-ship
  dye colours (the side's tint stands in).
