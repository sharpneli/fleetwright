# Hit view: what a hit did, shown

A debug view of a single hit now. Later, a War Thunder-style hit-cam for players. Started 2026-10-17.

## Done (2026-10-17)

1. **The terminal walk's trace.** `TerminalTrace` is filled by `Terminal.Resolve` when it's given one (the same walk,
   not a copy). It records each plate's crossing, thickness and the plate as met, obliquity, speed in and out, V_L as met
   (lot and shot scatter in it), what the walk made of the plate (`PlateCall`), the turn and whether the cap came off.
   It also records where the fuze started and why (`FuzeCause`). `DamageSystem.Trace` hooks it in; it's null in play
   and not world state. A test checks that tracing never changes a hit (`ShotRangeTests`).
2. **The hitbox viewer fires a shell.** `ShotRange` (Fleetwright.Sim) fires one shell through the world's own damage
   system at a ship of the class. It keeps the bent path, the trace, the outcome and effects, and the condition before
   and after (parts made worse, holes, fires). `HitboxScene.Shot.cs` adds the panel, the Shot window (the walk plate by
   plate, the fuze and burst, what the hit did) and a playback over the view:
   - the shell comes in;
   - it slows at each plate, each flashing in its call's colour;
   - it turns and glances along the bent path;
   - the fuze starts;
   - it bursts at the wreck radius (`Effects.WreckRadiusM`);
   - lines run to the parts it made worse, boxed by state.

   Dev hook: `-hitshot`.

## Later (when it's useful for players)

3. **Battle hits replayed.** A ring of captures in `DamageSystem` (not world state, not hashed): the `Hit`, its index
   in the damage stream (`stream.Derive(hits)`), the mount bearings, the attitude and the leak ring. That's enough to
   replay the terminal walk exactly. Effects read and change the whole condition, so replaying them needs a condition
   snapshot before the hit (a dev toggle) or the effects trace below. A click on a hit mark or log line opens it.
4. **The effects' own trace.** Today the shot diffs the condition, which shows what got worse but not why. An
   effects trace would record each part struck with its cause (line of flight, side or nose spray, nose slug, base
   plug, spall, gas, shock), the spaces the gas reached with their MPa, and the killed by station. This also covers
   damage/13 §6.6: tag every rolled-up number with its source group.
5. **The hit-cam.** The playback on the battle map for players: an inset rendered by `HitboxRenderer` into a small
   target, an x-ray cutaway, fragment cones (drawn rays are for show; the model counts expected hits per part), gas
   filling spaces, cells flooding.

Torpedoes and near misses have no path (`Underwater.Apply`, `NearMiss.Apply`), so they show as a burst and what it
reached.
