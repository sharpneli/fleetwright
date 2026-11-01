# Plan: the sim's workers (the job system)
Status: **agreed 2026-10-24** (the decisions below answered); W0-W4 and W6 built 2026-10-24; W7 tried and left serial; W8's curve measured; the default count capped at 2 (the user, 2026-10-24), reassessed whenever more goes onto the workers. The AI on workers (W9-W15, superseding W5) drafted 2026-11-01; W9-W13 built 2026-11-01, W14 next.
    Updated: 2026-11-01

The job system the architecture agreed from the start (`docs/world/architecture.md` "Threaded throughout", decision 1)
and `docs/battle/plan.md` 2.7d left for when the numbers asked for it: a few fixed worker threads under the sim thread,
taking the per-ship, per-pair and per-battery work of a moment, with results that never depend on how many workers
there are. The numbers ask now: the Jutland's exact-output pass (`docs/profiling.md`, the log of 2026-10-24) took a
whole play from 185 s to 139 s, and what's left is arithmetic, linear in ships, that only more cores make faster.

## The aim

1. **The Jutland played in well under half its time** on the reference 8 workers (plan.md 2.7, the user's answer 5),
   and an old quad-core running it smoothly.
2. **The same world on any number of workers:** a play on 1, 2 and 8 workers gives the same state hash and the same
   output, bit for bit, and a replay is a replay whatever machine it runs on.
3. **The game's soft real time kept:** nothing allocated in play on any thread, no worker holding the render thread
   back, small fights paying nothing for it.

## What there is today (measured 2026-10-24)

`shipgen scenario jutland.json --play`: 250 ships, 14 game hours, all of it in the fine tier, 139 s, no collections in
play. Where it goes (the probe table):

| | s | the work | how it splits |
|---|---|---|---|
| Ships: step | 40 | `ShipMotion.StepAll`, 20 ticks a second, 160 ns a ship a tick | each ship writes only its own rows; `Shore` is immutable ("any thread may query it") |
| Sighting | 32 | `Sightings.Look` once a second: per encounter a grid, per target and hostile side the nearest observers' eyes; 9.6 s of it the smoke on the lines (`SmokeSystem.Tau`) | the margins are pure; the draws are keyed (`stream.Derive(side, entity, time)`); `Fix` writes the side's picture |
| Smoke upkeep | 18 | the index rebuilt (`Rebuild`, every 2.5 s; its young part `Fresh`), puffs merged, laid and walked | each puff's state (`SmokeModel.State`) is pure; the plumes' sums are float sums in trail order |
| Captains | 18 | target picks (candidates, lines of fire), fall-backs and judgements | the picks are sequential by design: each sees the batteries the ships before it just put on a target |
| Judgements | ~9 (inside the AI) | `Matchup.Judge`, pure, ~5 a game second at ~40 µs | pure once `Matchups` is warmed (profiles, census, pair tables) |
| Gunnery | 9 | each battery's second (`GunnerySystem.Run`): readings, solution, salvo | draws keyed by battery and time; salvos go to the shells and the log |
| Damage | 9 | each ship's flooding and fire step, on the 1 s lattice (`FloodMs`), one event a ship | draws keyed by ship and time; log entries, recomputes and losses after |
| the rest | ~4 | commander, admirals' own work, shells, torpedoes, rails, encounters | small |

Two facts carry the plan:

- **Every random draw is a keyed stream** (`RandomStream.Derive` by side, ship, battery, time), never a shared
  sequence: work done in another order draws the same numbers.
- **Most of each system's cost is read-only compute** followed by a few writes to shared state (a picture, the log, the
  shells in flight, the pair cache, the event queue). Split there, the compute goes wide and the writes stay in
  today's order.

Threads already running in the app: the render thread (`Engine`), the sim thread (`SimHost`), the class builders (up
to 4, below normal), the designer's worker and probes (below normal). Headless: the sim thread and the builders.

## The principles

1. **Compute, then apply.** A system's moment is split into a compute pass over items (ships, targets × sides,
   batteries, pairs) that reads the world and writes only its own item's slot in a scratch result, and an apply pass on
   the sim thread, in today's order, that writes the shared state. Only the compute goes to workers.
2. **Results never depend on the workers.** Items are cut into chunks of a fixed size (a setting of the job, never
   from the worker count); a reduction (a sum, a count) is made per chunk and the chunks' parts added in chunk order. So
   1 worker and 8 do the same additions in the same order.
3. **One code path** (one prime implementation of each model): no sequential copy kept beside the parallel one. With 0
   workers, or a batch under its job's threshold, the same job runs inline on the sim thread.
4. **Nothing allocates per dispatch.** Jobs are structs; a dispatch is a few writes and a wake. Scratch is per worker,
   grown at most to the largest batch, as the sim's scratch is today.
5. **Order bugs caught deterministically, not by luck:** a test mode runs every job's chunks shuffled (by a seed) on
   one thread. A job that depends on its chunks' order fails it every time, not one run in a thousand.
6. **Not world state.** The worker count is the host's, like the registry's builder threads: never in `SimSettings`,
   never hashed, never in a save.

## The job system (W1)

In `Fleetwright.Sim.Core` (`Workers.cs`), owned by the world's host and handed to the `World` (null: inline).

- **Fixed threads,** named ("Sim worker 1".."N"), started with the world, `Normal` priority (the sim thread's), no
  thread pool: the pool's latency and its other users would decide the sim's timing.
- **A dispatch:** `workers.For(ref job, count)` with `job` a struct implementing `IJob { void Run(int item, int worker); }`
  (or a chunk `Run(int from, int to, int worker)` where the per-item call costs). The sim thread takes chunks too, and
  returns when every chunk is done: a moment's passes stay where they are in the event's handler, nothing else changes
  in the event loop. Struct jobs without boxing: a static generic slot per job type holds the job and a cached delegate
  the workers call (made once per type, at the first dispatch).
- **Waking:** a worker spins briefly (tens of µs) after its last chunk, then waits on an event. At full tilt the next
  dispatch comes within the spin, so a tick pays no wake-up; at 1x the workers sleep and use nothing. The spin window is
  the host's knob (decision 2).
- **Small batches inline:** each job has a threshold (items, or measured µs); under it, no dispatch. A squadrons fight
  pays nothing.
- **Exceptions:** a worker's caught and rethrown on the sim thread after the dispatch, so `ScenarioPlay`'s "the sim
  broke" catch and the tests see it as today.
- **Probes:** a dispatch is timed on the sim thread as now (the probe round the system's pass), and the workers' busy
  time summed per job, so the play's table shows each job's wall time and its parallel share.
- **Allocations:** `SimHost`'s and the benches' `GetAllocatedBytesForCurrentThread` miss the workers; each worker
  keeps its own count (read at the end of a dispatch), summed into the snapshot's and the play's numbers. The zero-
  allocation tests read the sum.

Tests: a dispatch of N items on 0, 1, 3 and 8 workers fills the same slots; a chunked sum is the same bit for bit; the
shuffled-chunk mode; an exception reaches the caller; a dispatch allocates nothing after the first (per job type).

## The steps

Each step: the probe before and after on squadrons, Tsushima and the Jutland, the play's output on 1 worker the same
as today's (the step restructures, it doesn't change the sim), and on 8 workers the same as on 1.

### W0 The bench (small) - done 2026-10-24

`shipgen scenario X --play --workers N` (0 default until W1), and `--shuffle SEED` for principle 5. A check in the
bench: the state hash at the end printed, so 1 and N are compared by one line. A micro-bench of a dispatch's cost
(empty jobs, 250 items) for W1's numbers.

Built: `--workers N` (default the headless count), `--shuffle SEED`, `--spin US`; the hash on the play's last lines
(a seed's line with `--seeds`). `shipgen workers [--items 250] [--item-ns 160] [--gap-us 100]`: 250 items of 160 ns
(the Jutland's step) back to back and with 100 µs of serial work between dispatches (the rest of a tick):

| workers | spin µs | back to back µs | with the gap µs |
|---|---|---|---|
| 0 | | 37.5 | 34.5 |
| 4 | 0 / 50 / 200 | 18.2 / 9.7 / 10.1 | 23.9 / 24.0 / 10.1 |
| 8 | 0 / 50 / 200 | 15.3 / 6.5 / 6.6 | 23.9 / 23.0 / 6.5 |

A worker woken from its sleep on Windows comes in about as late as the whole 40 µs job (decision 2's worry), so the
spin has to outlast the gap between dispatches: 50 µs doesn't cover a tick's, 200 does. The default is 250 µs.

### W1 The job system - done 2026-10-24

As above, with its tests. No system uses it yet.

Built as above (`Workers.cs`, `WorkersTests`): one chunked interface (`IJob.Run(from, to, worker)`); struct jobs reach
the workers through a holder per job type and worker set, made at its first dispatch; the dispatch's generation rides in
the top half of the chunk ticket, so a worker late from the last dispatch can never take a chunk of the next under the
last one's count; a woken worker wakes the next sleeping one (the wake-ups' system calls spread, not queued on the sim
thread); `Workers.Inline` (no threads) is every world's default. The app makes one set a world (`WorldSession`, the
app's count), disposed after its host.

### W2 Ships' step - done 2026-10-24

`StepAll` as a job over the fine list: each ship's `Step` writes only its rows (`Slide`'s too: `landSide`,
`routeNext`, the pose), `Shore` is read-only. The first real use, and the cheapest proof: 40 s of 139, a dispatch every
50 game ms (a million in a Jutland), so it measures the dispatch's cost where it matters most. Expected: 40 s to under
10 s on 8 workers if a dispatch costs a few µs; if it costs more, the chunking (a chunk of 32 ships) and the spin are
tuned here.

Built: `ShipMotion.StepAll` is a job over the fine list, chunks of 16, inline under 64 ships. The play's output the same
as before on 0 and 8 workers (squadrons, Tsushima, the Jutland), the same hash on both. The Jutland (`--play`, 8
workers, 250 µs spin, the same machine back to back): the step 46.6 -> 17.5 µs a tick (2.7x, the micro-bench's 6x
less the ticks a long AI or sighting moment lets the workers fall asleep before), the play 166.5 -> 137.3 s. Tsushima
(~70 ships off rails) gains nothing yet: 11.5 -> 12.0 µs; the threshold and the chunk are W8's to tune, with the other
jobs in.

### W3 Sighting - done 2026-10-24

`LookIn` split: per (target, hostile side) in today's order, the compute (the nearest observers, each eye's margin, the
rate, the best and the fix's candidate, whether smoke is wanted and for which pairs) into a result row; then the apply
on the sim thread in the same order: the draw (keyed, so either side of the split), `Fix`, going out of sight, the
smoke look. The grid build stays on the sim thread (cheap). Per-worker scratch: `nearIdx`, `nearD`, the smoke span. To
check first: whether a `Fix` in the apply can change what a later target's compute read (a picture's indices when a
contact is added; `inSight` of another contact); the compute reads only its own target's contact, so it shouldn't.

Built: the look's items (target, side) in today's order, computed as a job (chunks of 4, inline under 16): the nearest
observers (a worker's own squared distances), the margins, the rate and the fix's eye, the smoke pairs it asked; then
applied in order: the picture made (`Get`), the cache's bookkeeping for its pairs, the draw, `Fix`, out of sight, the
smoke look. The check held: an item's compute reads only its own contact, which only its own apply changes; a ship is in
one encounter, so no item is looked at twice. The Jutland's look 746 -> 277 µs on 8 workers.

### W4 The smoke's queries and index - done 2026-10-24

- **Queries:** `Work` keeps its scratch in fields (`stamp`, `chunkStamp`, the rows and bands, the counters): a query
  context per worker instead. W3's compute collects the pairs a look needs; the cache's misses are worked as a job; the
  answers go into the cache on the sim thread in the order the pairs were asked (the cache's slots, its eviction and its
  hash as today).
- **The index:** `Rebuild` and `Fresh` compute every puff's state (`SmokeModel.State`, the eddies: the cost) as a job
  into the index's slots (each source's puffs at a known offset); the plumes' sums, the compaction and the filing stay
  on the sim thread in trail order, so their float sums are added as now.

Built: a query's scratch (`SmokeSystem.Query`: the lines, the depths, the chunk stamps good for one filing of the index,
the counters) one per worker; `Tau` is now `Cached` (reading only), `WorkOut` (a worker's scratch) and `Answered` (the
bookkeeping, on the sim thread in the order asked, deciding hit or miss itself, so a pair asked twice in a pass is worked
twice to the same depths and its second answer is a hit, as serially). No pair is asked twice in a look. The index: the
store laid out in trail order first, then every puff's state and line as a job (chunks of 64, inline under 512), then the
plumes' sums, the chunks and the filing in trail order; the young part's lines likewise. The eddies' clock (`Turn`) is
set on the sim thread before the dispatch: it was the one lazy cache the states shared. The index 531 -> 340 µs a rebuild.

W2-W4 on the Jutland (8 workers, nothing else running): **159.2 -> 106.7 s**, the same output and hash on 0 and 8
workers and with the chunks shuffled. What's left serial-heavy: the AI 32 s (captains 20 s), the ships' step 17 s (the
workers asleep after long moments), smoke upkeep 18 s (the merge, the emission, the index's filing), sighting 14 s,
gunnery 11 s, damage 10 s.

### W5 The AI's read-only part - superseded by W9-W15 (2026-11-01)

The decisions stay sequential (a pick sees the batteries the ships before it put on a target, `CaptainLayer.Fire.cs`
"the next ships' picks see it at once"; an admiral's orders reach the captains after). What goes wide is what they read:

- at the start of a think, every unit in an encounter's judgement (`MatchupJudge.JudgedNear`, pure, cached a moment) as
  a job, so the layers find them made;
- each fighting captain's candidates and their lines of fire (`Candidates`, `LineOfFire`), made before the sequential
  picks walk them.

`Matchups` is warmed before (profiles, the census, every gun's table against every class: `Warm` makes them today); a
lazy creation inside a job is an error (it would race), asserted in Debug. This step only pays where the judgements and
candidates are the cost; W0's numbers decide how much of it to do.

### W6 Gunnery - done 2026-10-24

Each battery's second split: the readings, the tracker and the solution (the cost) as a job, by battery; the salvo,
its shells into `ShellSystem`, the log, the state changes applied on the sim thread in battery order.

Built: a battery's second begins on the sim thread in battery order (`Start`: let go if its ship or target is gone,
silent off the fine tier, its instruments picked again: `AddSensors` reads and flags its ship's other batteries, so it
stays in order); the rest (`Second`: readings, tracker, solution, the mounts ordered, the salvo timed) is the job
(chunks of 4, inline under 16), its firing flags per worker, its smoke pair from the cache or worked on the worker; then
in battery order the pair's bookkeeping and the salvo's event. The salvo itself is an event, as before. The Jutland's
tick 87 -> 44.5 µs; the play 156.4 -> 101.6 s on 8 workers, the same output and hash.

The step now runs over slot ranges (chunks of 32, `ShipStore.IsFine`), so a chunk writes its own stretch of the store's
columns; and a probe sums every thread's time in it ("Ships: step, all threads"). That sum is 85 µs a tick on 8 workers
(83 on 4) against 43 inline: the per-ship work costs ~2x on the workers, the same with the slots in order, so not lines
shared between workers but the ships' data cold in the other cores' caches (this machine's 16 cores are two dies).
W8's to look at (keeping the workers on the sim thread's die).

### W7 Damage steps - tried 2026-10-24, left serial

The steps already fall on one lattice (`FloodMs`), one event a ship in entity order. Made one event a moment for every
ship due (as gunnery's tick is), the step split: the repairs, valves, flooding, settling and fires (the cost, the ship's
own condition only) as a job; the log entries, recomputes, holds and losses applied in entity order. A loss or a blast
in the apply that reaches another ship of the batch (a magazine's blast on a neighbour) makes the later ships' steps be
computed again, on the sim thread, after it: rare, and exact. Batching moves the steps relative to other systems' events
at the same moment, so it may change the output once against today (decision 3).

Tried (one steps event a moment, every due ship's own part on the workers from the moment's state, the applies in entity
order): the same hash on 0 and 8 workers and shuffled, squadrons over 24 seeds the same within the spread. But the
trace put almost none of the cost in the part that goes wide (`StepOwn`, ~0.1 s of a 100 s trace) and nearly all of it
in the serial apply: `Magazines.SetOff`, the venting race (10.6 s). So it bought no speed, changed the output, and the
Jutland's 4 seeds lost more tonnage after it than before (Germany 106k -> 180k t, mostly one seed: not settled). Taken
out: decision 3's other branch, damage per ship and serial. What would pay instead is `SetOff`'s own cost (how often a
burning magazine runs its race), a model question for the damage plan, not the workers'.

### W8 Measured and tuned - the curve 2026-10-24

The worker curve (0, 1, 2, 4, 8) on the Jutland; the quad-core case (4 workers beside the render thread); the app's
default count with the render thread and the builders; the plan.md 2.7 target (the sim thread at most a quarter busy
at 10x) checked again. The numbers into `docs/profiling.md`'s log and plan.md 2.7's "Measured" table.

The Jutland (`--play`, W0-W4 and W6, this machine's 16 cores, nothing else running), every run the same battle and the
same hash:

| workers | played in | vs inline | the step, a tick | its time on all threads |
|---|---|---|---|---|
| 0 | 156.4 s | | 43.0 µs | 43.0 µs |
| 1 | 135.6 s | -13% | 29.4 µs | 55.6 µs |
| 2 | 115.0 s | -26% | 25.2 µs | 65.3 µs |
| 3 | 111.2 s | -29% | 19.8 µs | 63.8 µs |
| 4 | 106.7 s | -32% | 18.0 µs | 63.7 µs |
| 8 | 101.6 s | -35% | 16.5 µs | 84.7 µs |

Two workers give three quarters of the gain; past four it's nearly flat while each worker spins a core (the user's
point, 2026-10-24: too little work over too many threads is overhead). The serial floor is what's left: the AI (~30
s), smoke upkeep's merge and emission, the applies, `SetOff`. "Well under half" is not reachable by more workers;
it needs the serial part cut (W5's reads, or exact-output passes on the serial code). Plan 2.7's speed target (10x or
better) holds with room: the whole 14 hours average ~2 ms of sim a game second.

**The default count, the user's (2026-10-24): at most 2** (`Workers.DefaultCap`), headless and in the app; `--workers N`
still takes any count. AI threads of their own may come later. W5 waits: features first.

## The AI on workers (W9-W15) - drafted 2026-11-01

The AI has grown from ~30 s of the Jutland to the largest serial part of a play, and the user's frame for it
(2026-11-01) is bigger than W5's judgements made ahead: every captain, admiral and commander a think that takes the
game state in and gives commands out, on the workers.

### Measured 2026-11-01

`shipgen scenario jutland.json --play`, Release, the default 2 workers: **281 s** (115 s on 2026-10-24; the step's
23 µs a tick unchanged, so not the machine). The probe table:

| | share | s | on workers |
|---|---|---|---|
| AI | 49% | 138 | no |
| of it the captains | 40% | 111 | no |
| the admirals | 5% | 14 | no |
| the commander | 5% | 13 (15 ms a think, 47 the longest) | no |
| Sighting | 20% | 55 | W3 (heavier with the night's lights since) |
| Gunnery | 8% | 24 | W6 |
| Ships' step | 8% | 24 | W2 |
| Smoke, upkeep and queries | ~16% | ~45 | W4 in part |
| Damage | 5% | 15 | no (W7) |

A trace of the first 90 s (the sim thread): `Matchup.Judge` 21% of it (17.7 s of 85), asked by the captains' fall-back
8.6 s (`FallBack` judges every own force within `FallBackM`, 150 km: every force on the map, nearest first until one is
a match, cached a moment), `JudgedNear` 3.4 s (from `FightThink` and the admirals' `AttackStep`), the commander 4.9 s
(`Assign`, `Enemy`, `Balance`). The captains' fire pass 19 s: the picks 6.7 s (their candidates 2.8 s), torpedoes 3.1 s
(`NightLight.At` in it), night lights 2.9 s, the torpedo watch 1.2 s.

### What holds it on one thread today

1. **Orders act at once.** `ctx.Issue` and `ctx.Helm` go to `World.Issue`, which applies the command and runs every
   system's `Applied`: the next captain in the loop reads a world his predecessor just changed.
2. **Captains read each other on purpose.** The crowd counts (`onChief`, `onOther`: "the next ships' picks see it at
   once"), the challenge's "a ship of his navy asking her already", the lights and star shell likewise.
3. **Layers reach into each other.** Admirals call `captains.FireOnSignal` (a launch, then and there), `HoldTorpedoes`,
   `Release`, `Ask`, and read `FireOf`, `SpeedOf`, `Combing`, `Holds`, `SuperiorsAt`; captains read
   `Admirals.LineConned`, `Tasks.Of`, `Tasks.Avoids`; the commander reads the tasks. The layers cascade at a shared
   moment, top down, so an admiral's order reaches his captains the same second.
4. **Shared lazy caches and scratch.** The judge's moment caches (`units`, `pairs`), `Matchups`' profiles and census,
   `SeenContacts`' grids; the layers' scratch (`cand*`, `fallCands`, `LineOfFire`, `TorpedoWatch`, the judge's `line`).

The draws are keyed already (`World.Stream(name).Derive(entity...)`), and each layer's state is per unit.

### The model (the user's, 2026-11-01)

- **A think takes the game state in and gives commands out.** A unit writes only its own record and its outbox.
- **Others see only a unit's outputs:** what its ship does (course, speed, guns on a target, lights, smoke), the
  orders it sent down, the requests and reports it sent up. Never another unit's inner state, peers or other layers.
- **Orders land on the next think.** No telepathic cascade within a moment; no signal delay simulated either (the
  outbox is where one would go, if ever).
- **One dispatch a layer, the thinks spread over time.** Each unit keeps its period (captains and admirals 1 s, the
  commander 60 s) but thinks at a phase of its own within it: each fine tick (50 ms, 20 a second) runs the units whose
  phase it is, a dispatch a layer, each layer's outbox applied after its dispatch in unit order. Every unit thinks
  every period however many there are (the user: enough slots that every ship has its think in time, no captain
  waiting on others thinking); no budget a tick, no queue.
- **The phase** is the unit's id modulo the period's ticks (a ship's entity, a group's id, a side): ids come in order,
  so the slots stay even and a squadron's ships think on ticks one after another; nothing to keep or hash, the same on
  any worker count, after a load and in a replay (W12, simpler than the least-filled slot first drafted). The coarse
  tier and rails keep their step (cheap; "What stays out").

Results stay the workers' principles: the same on 0, 2 and 8 workers and shuffled, nothing allocated in play.

### Judgement: the force's and the ship's (the user's, 2026-11-01)

- **The admiral judges from the force's viewpoint**, once a period: his unit against the enemy round it, and its
  output is the order: fight (and the band), fall back on a force (his is the search of own forces, `FallBack`'s
  today), hold off, attack.
- **The captain judges from his ship's viewpoint**: his ship against what's near her, for the decisions that are his
  within his orders (targets, torpedoes, evading, combing, lights, a ship hopeless where she is turning away, falling
  out, giving up). One class against the near enemy, so cheaper than his unit's.
- **A ship alone** is her own admiral: both are hers.

This takes away the duplicated judgement (each captain of a squadron judging the same unit, which spread over the
ticks would be up to 20 times today's) and fits the admirals' own calls (the user, 2026-10-31: tactical choices are the leading admiral's, with thresholds).

### What follows

- **Herding mostly goes.** A line's captains think on different ticks, so a later one sees the earlier ships' guns on
  a target (an output). The crowd counts become a reading of the world's battery targets, not shared scratch; only
  captains on the same tick don't see each other, which the cost of a switch covers (a new target restarts the ranging
  and the solution, which gunnery models: the pick weighs it, physics, not a rule). The line's fire distribution as
  doctrine only if herding is left (the user agreed).
- **The layer-wide passes split.** `MarkUnderFire`, `CountOn`, `HurtPass`, `EndPass`, `HeldLightsPass`, `Recount` walk
  every ship once a think; on every tick they'd cost 20 times. Each becomes the captain's own, a read-only build before
  the dispatch from the world (who fires at whom is an output), or kept as it changes.
- **Spikes flatten.** 250 captains are ~12 a tick; the commander's two sides on different ticks. Better for real time
  than one moment made parallel, and the gain no longer waits on the worker count.
- **The commander** is still one item of ~7.5 ms a side on his tick: his per-group judgements (`Assign`, `Enemy`,
  `Balance`) as a job. Thinking on over several ticks, orders landing at a set game moment, only if that's not enough.
- **The output changes once** (W10-W12): the balance panel (`shipgen balance`) before and after each, fire
  distribution by eye.

### The steps

| | |
|---|---|
| W9 | The outbox and a Debug guard (`World.Issue` throws during an AI dispatch); orders still applied at once in today's order: output identical. The audit: a think that reads back the world after its own order (it'd read stale with an outbox) keeps what it ordered itself |
| W10 | Reads become outputs: reports up (fallen out, out of rounds, combing, asking), orders held as received, the direct calls as commands; orders land on the next think. Output changes: the panel |
| W11 | The force's and the ship's judgement: the admiral's order carries the unit's call, the captain judges his ship. The panel |
| W12 | Phases: each unit its slot, the layer-wide passes split, the crowd counts from the world and the switch's cost in the pick. The panel, fire distribution by eye |
| W13 | Per-worker scratch, the warm step (grids, profiles, census, doctrine before a dispatch, lazy creation inside one an error asserted in Debug); captains as a job a tick: 0 = 2 = 8 workers, shuffled, zero-allocation tests |
| W14 | Admirals, tasks and the commander as jobs; the commander's judgements split |
| W15 | The curve measured (0, 1, 2, 4, 8); the worker cap reassessed (the user: whenever more goes onto the workers) |

### W9 The outbox - done 2026-11-01

`Sim.Ai/Outbox.cs`: a think's orders (`AiContext.Issue`, `HelmOrders`) go to the AI system's outbox, applied on the sim
thread in the order given; `World.Thinking` is set while a layer thinks, and `World.Issue` (and, W10, `World.Matters`)
refuse a call then, so every order of a think goes through the outbox. Orders still applied as given: squadrons,
Tsushima and the Jutland the same output and end hash as before.

### W10 Orders after the think - done 2026-11-01

- The outbox defers: a layer's orders are applied after its think, in the order given.
- The admirals' calls into the captains became commands (`AiCommand.TorpedoHold`, `TorpedoSignal`, `Ask`, `Release`,
  between layers only): a flotilla's admiral makes the signal once, each boat holds it and fires on it at her own think
  (the coarse fire watch is the captains' now); the housekeeping release of units out of a line is an order too.
- A by-hand task's end reaches the player through the outbox (`AiContext.Matters`).
- **Reads of another layer:** with one dispatch a layer, a layer's records change only in its own think and when orders
  are applied, so what another layer reads (a captain's orders as held, an admiral's order and attack, a task's state)
  is that unit's output as it last left it. They stay reads; what has to go is a unit reading a peer of its own layer
  thinking at the same time (W12, W13).
- **What it changed:** a think no longer reads back its own orders. Two tests leaned on that: the lit cruiser engaged and
  lit a destroyer in the same think because gunnery hadn't judged yet whether it could see a target engaged that instant
  (now it checks a second later, sees her by the full moon, keeps dark: the test now holds the destroyer's beam by the
  player's hand, as it meant); a force's deployment takes 2 s longer, its orders reaching divisions and ships a think
  apart (the bound 30 -> 32 min). A line's station keeping now matches the next ahead's speed order a think late.

### W11 The unit's call - done 2026-11-01

`MatchupJudge.Call`, `Told`, `Publish`: a unit's call (its judgement against the enemy in sight round its guide, away
under doctrine's bar and back over it by `FightRallyShare`, the own force it falls back on) is made by its head once a
think: the admiral of the outermost group a ship moves with, or her captain alone. The calls made in a layer's think are
told to the chain after it; the guide's captain fights by his unit's call (alone, his own), the flotilla's attack, a
force's handed attacks and a scout's task read it as told. The fall-back search moved from the captains to the judge
(`FallBackShip`), its per-moment cache gone. The captain's own latitude (targets, torpedoes, evading, combing, lights,
falling out, giving up) is as before; a judgement of his ship alone wasn't needed for any of it yet (open item).

### W12 Phases - done 2026-11-01

Each layer at its own period (`IAiLayer.OwnPeriodMs`) thinks every ship tick (50 ms), each unit at its phase: its id
modulo the period's ticks (`AiContext.Due`; captains and admirals 20 slots, tasks 200, the commander 1200: his two sides
on ticks of their own). At a slower period (the coarse step, the rails') every unit thinks at once, as before. The
layer-wide passes take only the units due (the main loop, the hurt, end and held-lights passes, the fire pass, which
counts every engaged ship for the layer's period); the screens' arcs are worked out once a period, the commander's
gathering and aims only on a side's tick. The crowd counts are the world's battery targets at the tick (the ships that
thought on earlier ticks with their picks), never this tick's picks: a captain's own as the world has them are taken out.
The pick's own hysteresis (`FireSwitchShare`, `FireKeepShare`) is the switch's cost; no new rule.

A fix to W11 found here: every head made his unit's call every think, so every unit under the bar ran the fall-back
search whether anyone fought or not (the Jutland 281 -> 426 s, the admirals 52%). Now a head calls while his guide
fights, he fights, or the unit scouts, the fall-back search only while the guide fights, and takes the call back
(`MatchupJudge.Quiet`) when nobody needs it.

The Jutland (Release, 2 workers): **189.8 s** (281 s before W10; a different battle, so rough): the captains 58.7 µs a
tick (1.2 ms a game second against 2.2 before), the admirals 37.4 µs a tick (0.75 ms a game second against 0.27: their
housekeeping runs every tick), the commander 11 µs a tick, his longest 25 ms (47 before).

W12's panel (lost a run, as the table below): Dover 0.6 / 1.5, Coronel 1.0 / 0.6, Cape Sarych 2.3 / 0.6, Prizes 1.1 /
0.6, Åland 4.0 / 5.3, Heligoland 1.4 / 12.6, Dogger Bank 6.6 / 10.9, Tsushima 3.8 / 5.9, the Jutland 26.8 / 53.0; 7 of 14
sides within about twice the record, the record's loser losing more in 24 of 44 seeds.

### W13 The captains as a job - done 2026-11-01

A captains' think is a job (`CaptainLayer.Workers.cs`): its items the ships due at the tick by slot, each ship's whole
part in its old order (her captain's own think, her men's reliefs if hurt out of the fight, her guns if in it, his call
on her end, the player's lights held), a chunk a ship, each chunk's orders in an outbox of its own, moved into the
think's in chunk order after. Before the dispatch, on the sim thread: captains whose ships are gone dropped, the ones
the items need made (none is made or dropped during it: refused), who's under fire and the layer's fighting counted, the
guns' and star shell's reaches, the squadrons' index, the judge's rooms and the matchups' profiles and tables (against
given loadouts too) warmed. After it: the waits for the coarse fire watch collected in item order, the counts made.

- **Scratch per worker lane** (`Workers.Lane`: a worker's number, fixed for its life, 0 for the sim thread): the
  captains' own (`Mind`: candidates, reaches, crews' lists, the threat memo), the judge's sides, the contacts' found
  lists and every grid query's (`CubeIndex.Query`).
- **What's worked out a moment** (each navy's contacts in grids, friends' and torpedoes' grids, the census, each side's
  enemies for the crews) is built by the first thread to ask, under a lock, the rest reading it after: a pure function
  of the frozen world, so whoever builds it, it's the same. A question wider than the contacts' cubes during a
  dispatch looks at every contact, and widens the cubes after.
- **A bug found by it, as principle 5 meant:** a ship's think read the doctrine a previous ship's guns had left in the
  layer's scratch (`Opens` from `Flee` before `Aim`, from her end's judgement, homeward): each ship's item now starts
  with her own. The shuffled chunks and `--hashes` (each system's hash part every game minute: where two runs part)
  found it on the Jutland's first minute.
- **Checked:** squadrons, Tsushima and the Jutland the same end hash on 0, 8 and 2 workers shuffled;
  `WorkersTests.Captains_think_the_same_on_any_number_of_workers` (80 ships fighting, 0 = 4 workers = shuffled).

The Jutland (2 workers, the machine's quick state, the step 22.5 µs a tick): **222.9 s**; the captains 46.6 µs a tick
(58.7 before at the same state), of it 8.1 µs prepared (24.4 before the moment's builds went lazy) and 37.1 µs the job.
A tick's dozen ships are uneven (a fighter with many candidates is most of it), so three threads buy about a fifth;
the machine's own swing is wider than that (the same W12 build played 189.8 s and 255.3 s an hour apart: compare only
runs back to back).

### The panel, W10 and W11 (2026-11-01)

`shipgen balance`, 8 seeds (the Jutland 4), lost a run before -> after W10 -> after W11:

| | before | W10 | W11 |
|---|---|---|---|
| Dover Strait, Britain / Germany | 0.4 / 1.4 | 0.6 / 0.6 | 0.6 / 1.8 |
| Coronel | 0.9 / 0.5 | 1.4 / 1.3 | 0.9 / 0.8 |
| Cape Sarych | 1.5 / 0.8 | 2.1 / 0.9 | 3.0 / 0.8 |
| Prizes' war | 0.1 / 0.6 | 1.1 / 0.9 | 1.5 / 0.8 |
| Åland | 4.6 / 3.4 | 5.6 / 4.9 | 5.9 / 5.3 |
| Heligoland | 2.8 / 15.9 | 3.1 / 16.8 | 3.8 / 16.1 |
| Dogger Bank | 5.9 / 5.6 | 7.0 / 8.8 | 7.1 / 8.8 |
| Tsushima, Russia / Japan | 2.6 / 3.8 | 4.4 / 3.4 | 4.3 / 3.5 |
| Jutland | 33.5 / 53.5 | 25.8 / 51.3 | 30.5 / 52.5 |
| sides within ~2x the record | 4 of 14 | 5 of 14 | 5 of 14 |
| the record's loser lost more | 23 of 44 | 23 of 44 | 25 of 44 |

Mostly a reshuffle, a little more fighting in the small actions; squadrons (not in the panel) fought less over 8 seeds
(Germany's hits 454 -> 201), its first difference a line's station keeping a think late, then the chaos of a
different battle. The Jutland's German torpedo hits 6 -> 18 a run after W11.

### Open items

- The captain's latitude under orders, item by item, against the player's overrule (the player's
  orders beat the AI's; by-hand orders hold).

## What to expect

Of the 139 s, roughly 95 s is in passes these steps split (the step, most of sighting, the smoke's states and queries,
the AI's reads, gunnery's and damage's compute) and about 45 s stays serial (the applies, the decisions, the event loop,
the small systems). On 8 workers at a 70% efficiency that's about 60 s for the Jutland, on 4 about 75 s: estimates for
W8 to replace. The serial part is then the next pass's target (Amdahl), and the exact-output kind of pass applies to it
again.

## What stays out

- **Speculative or out-of-order events,** a parallel event queue: the event loop stays one thread, in its order.
- **Float changes for speed** (fused sin and cos, reordered sums): the output stays the same; a separate decision.
- **The coarse tier and rails:** cheap already; jobs there only if a scale test asks.
- **Shipgen's and the land's own parallel builds** (`Shore`, the class builders): they stay as they are, outside the
  sim's workers.

## Decisions (answered 2026-10-24)

The user's answers: 1. capped at 8 (all cores blows up on high core counts); 2. a short spin (Windows is picky about
when it wakes a thread): measured in W0, 250 µs; 3. W7's one-time change is acceptable; 4. the order is fine.

1. **The worker count.** Headless: all cores less one, at most 8 (the reference). The app: cores less two (the render
   thread, the builders), at most 8, at least 0; a setting later if players ask.
2. **Spin or sleep.** A short spin keeps a fast run's ticks cheap but burns a core while running at speed; sleeping
   costs a wake-up per dispatch. Proposed: spin ~50 µs, then sleep, the host's knob.
3. **W7's batching** may change the output once against today (the damage steps' place among the same moment's other
   events); after it, 1 and N workers agree as everywhere. Acceptable, or keep damage per ship and leave it serial?
4. **The order:** W0-W2 first (the system and the proof on the biggest single cost), then W3-W4 (sighting and smoke,
   the next biggest), then W5-W7 as their numbers ask.

## Open items

- W3's check that an apply can't change a later compute's read (pictures' indices).
- Whether the app's sim at 1x needs the workers at all (a tick's 40 µs fits in a frame many times over): the
  threshold may keep them asleep until the asked speed is high, which is also where the render thread most needs its
  core.
