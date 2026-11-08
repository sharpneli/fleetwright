# Plan: the sim's workers (the job system)
Status: **agreed 2026-10-24** (the decisions below answered); W16 (explosions off the sim thread) built 2026-11-07, to be replaced by "Explosions in time" (ET, agreed 2026-11-08); W0-W4 and W6 built 2026-10-24; W7 tried and left serial; W8's curve measured; the default count capped at 2 (the user, 2026-10-24), reassessed whenever more goes onto the workers. The AI on workers (W9-W15, superseding W5) drafted 2026-11-01; W9-W13 built 2026-11-01, W14 2026-11-02, W15's curve measured 2026-11-02 (the cap's answer waits on the user).
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

### W14 The admirals' and commander's heavy parts - done 2026-11-02

Spread over the ticks (W12), a tick has two or three admirals due, the commander one side: too few items to share
between threads, and very uneven (a fighting force's call is most of it). So the admirals and the commander think as a
layer each tick on the sim thread, as planned for the dispatch, and what's heavy inside a think goes to the workers:

- **The fall-back search** (`MatchupJudge.FallBackShip`): the nearest force judged first, alone (most often the match);
  past it, the rest as a job, each force's matchup against the enemy round the guide on a worker with its lane's own
  side, the first over the bar in nearest-first order taken: the same answer as asking them one by one.
- **The commander's held groups** (`JudgeGroups`): each enemy group he holds against all of his, for `Assign`'s
  gravest-first order and `Enemy`'s target, as a job; the greedy sets after it stay in order (each judgement leans on
  the set so far).
- **Not to rebuild what only a look changes:** since W12 every tick is a moment, so the knowledge built once a moment
  was built up to 20 times a second (the tactical picture alone 6.3 of 117 s traced, through deploying admirals). A
  navy's picture now has a version (`Picture.Version`, changed by a look's every write), and the tactical picture and
  the held groups are built again when it changes or a think's period has passed; the flotillas' heavy units once a
  period.
- **Nested work runs where it is:** a job is never started from inside another (`Workers.CanDispatch`: none under way,
  inline or on the threads, and the sim thread's); a lone captain's fall-back search inside the captains' job runs on
  his thread. Found by the shuffled check: an inline job's shuffled order was overwritten by a job started inside it.
- **Checked:** Tsushima and the Jutland the same end hash on 0, 8, 2 shuffled and 0 shuffled workers.

The Jutland back to back with W13 (2 workers): 242.0 -> 212.7 s; the admirals 61.5 -> 49.3 µs a tick. The machine
drifted between the two (the ships' step 29.6 -> 22.7 µs a tick), so part of it is the machine.

W13-W14's panel (their output changes: each ship's own doctrine in her scratch, the knowledge built by version and
period): Dover 0.6 / 1.5, Coronel 1.1 / 0.8, Cape Sarych 2.3 / 0.6, Prizes 1.1 / 0.6, Åland 4.0 / 5.3, Heligoland
2.1 / 12.8, Dogger Bank 6.6 / 10.9, Tsushima 3.8 / 5.5, the Jutland 30.5 / 51.3; 6 of 14 sides within about twice the
record, the record's loser losing more in 25 of 44 seeds: as W12's but for Heligoland and the Jutland's spread.

### W15 The curve - measured 2026-11-02

The Jutland (`--play`, W9-W14, this machine's 16 cores, nothing else running, one after another), every run the same
battle and the same hash:

| workers | played in | vs inline | the step, a tick | sighting, a look | the captains' job, a tick | the admirals, a tick |
|---|---|---|---|---|---|---|
| 0 | 299.9 s | | 46.1 µs | 1342 µs | 64.5 µs | 44.9 µs |
| 1 | 269.5 s | -10% | 33.9 µs | 1076 µs | 54.2 µs | 50.5 µs |
| 2 | 211.2 s | -30% | 23.1 µs | 702 µs | 37.4 µs | 49.6 µs |
| 4 | 185.3 s | -38% | 18.3 µs | 520 µs | 29.9 µs | 49.0 µs |
| 8 | 163.7 s | -45% | 11.9 µs | 391 µs | 25.5 µs | 48.0 µs |

Against October's curve (W8: 2 -> 4 workers bought 7%, 4 -> 8 another 5%), more of a play now goes wide: 2 -> 4 buys 12%
and 4 -> 8 another 12%. The serial floor at 8 workers is the admirals (29% of the play: their thinks themselves, a few a
tick), the smoke's upkeep (11%), the commander (4%), the applies. **The cap, for the user** (the user, 2026-11-01: reassessed
whenever more goes onto the workers): 4 gives two thirds of 8's gain over 2; proposed 4, kept at 2 until answered.

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

## Explosions off the sim thread (W16) - agreed 2026-11-07

A magazine's race costs 60-700 ms (explosions.md "Cost and convergence"), run inside the event that set it off, so the sim
publishes nothing while it runs and the map freezes, then jumps. Not a cost to cut (the model needs its steps): a cost to move.
The user, 2026-11-07: "What about tossing hits to a separate thread and processing them a tick or two later? That should not
have material impact on gameplay. On coarse tier it can be instant." And: "Second of gametime is peanuts at these scales. And
it's fine if it doesn't save anything for the benches, we want the game to feel interactive. Though we probably still need
some smoothing for high time compressions."

### The rule

1. **In the fine tier an explosion on a ship lands on the damage lattice at least a second after it happened**
   (`HitDelayMs`, 1000, `FloodMs`'s multiple): a hit at T lands at the first lattice moment at or after T + 1 s, every hit
   that lands then in the order they came. An explosion is any hit (shell, near miss, torpedo, her own store) and a magazine
   a fire step sets off (W7: that's where the cost was). In the coarse tier, as the world is when it happens, it lands at
   once, as today. 0 lands everything at once (the benches that hit a ship and read what it did keep `Strike`, which is
   immediate).
2. **The world never waits on a thread to decide anything:** what lands, and when, is the rule's alone; the thread only
   works it out early. Land time comes and it isn't done: the sim thread waits for it. Same hash with the thread and without,
   on any number of workers, and a replay is a replay.
3. **The ship's damage is the batch's until it lands.** The batch is worked out on a copy of her condition taken when it
   closes; her live condition, which everything else reads (gunnery, motion, the AI, the snapshot), stands as it was until the
   batch lands. What would change her condition meanwhile waits behind it, in order: her flood and fire step, her timed
   outages' clear, a straddle's or her own guns' blast on her men; a command on her (dev, giving up, posture) lands the batch
   first, then runs. A Debug and test guard: her condition's hash when the copy was taken, checked when it lands.
4. **Further hits on her while a batch is out** go in the next batch, so at most about two seconds late under heavy fire.

What moves: a hit's damage, the log's line for it and what the screens show of it land up to two seconds after the shell
did (its splash and mark don't: they're the shell's). Fire control spots by the fall, so nothing there.

### The steps

| | |
|---|---|
| W16a | `ShipCondition.CopyFrom` (pooled, no allocation in play) and a reflection test that every field, private ones too, is copied |
| W16b | `Strike` split: what a hit does to her condition (pure: the condition, the class, the settings, and what the hit captured from the world at T: turret bearings, rounds, experience, the sea) and the world's part after it (log, recompute's holds, losses, blasts, wakes, stores going off). Still landed at once: the output bit for bit as before |
| W16c | The rule (1, 3, 4) in the world, worked out on the sim thread when the batch lands: the output moves (timing), the panel and the parity bench read it |
| W16d | The thread: the batch worked out on the copy as soon as it closes, the sim waiting at land time if it must. 0 = 1 thread = shuffled, zero-allocation tests |
| W16e | The map's clock leads through a stall: today it never runs more than a frame past the newest snapshot (plan.md 2.7c), so a sim stall is a freeze and a jump; it may run up to about 0.3 s of real time on at the speed the world kept, the ships extrapolated along their arcs as they are now. For high compressions, where a second of game time is milliseconds and the sim waits on the race anyway |
| W16f | Measured in the game: the longest gap between snapshots and the map's longest still, a magazine race at 1x, 30x and 240x, before and after |

### W16a-f built (2026-11-07)

- **W16a** `ShipCondition.CopyFrom`; `ConditionCopyTests` fills every field by reflection.
- **On the way:** a raced magazine drew by the next hit's count (`hits++`), moving every later hit's draws on every ship; now
  its own hit's index.
- **W16b** `Strike` = `Capture` + `Work` (pure) + `Land`; bit for bit as before (hits of every source, damage-history, two
  scenarios with their hashes).
- **W16c** the rule, worked on the copy at landing. A batch's explosions are worked in order first, then each lands, so a loss
  is on her condition in `Work`. Copies and batches pooled per class from when it comes in (the class's electric load, built
  lazily on its first read, now then too: a race with the thread, and an allocation in play). The panel (`balance --fc-runs
  0`): 8 of 14 sides within twice the record, as at EX6; the record's loser lost more in 29 of 44 seeds (26); capsized 51%,
  broke in two 28%, foundered 21%; scenario by scenario within the seeds' spread (Dogger Bank's Germany 11.8 lost against 7.5,
  Åland's Sweden 1.4 against 3.5).
- **W16d** `DamageSystem.Threaded` (the host's; `SimHost` turns it on): the same hash with it and without (`DeferredHitTests`,
  squadrons minute by minute).
- **W16e** the map's clock leads up to 0.3 s of real time while no snapshot comes.
- **W16f** `-frametimes` prints the sim's longest gap between snapshots and the map's longest still. A 2000 mm HE salvo on
  Bismarck (`-ships=big:1:0@56.5,4.0,0,0 -ships=bismarck:1:1@... -fire=1:0:he:...`):

  | | the sim's longest gap | the map's longest still |
  |---|---|---|
  | 3 km, 1x, at once | 253 ms at the hit | 37 stale frames |
  | 3 km, 1x, W16 | none at the hit (92 ms at the load) | 10 |
  | 3 km, 10x, at once / W16 | 240 / 112 ms | |
  | 18 km, 30x, at once / W16 | 1,159 ms / none at the hit | 102 frames / 1 |
  | 18 km, 100x, at once / W16 | 1,198 / 1,089 ms | 107 / 93 frames |

**Open:** at high speeds a race longer than its slack still stalls the sim: at 100x a second of game time is 10 ms, and
Bismarck's races at 18 km run 1.1-1.2 s (several magazines, slow burns). The lead hides up to 0.3 s. Left: a longer lead
(the ships drawn further past the world), or a cheaper race (explosions.md item 7: the step's count, the cells a step
visits). The delay can't grow with the speed (`same-at-any-compression`).

## Explosions in time (ET) - agreed 2026-11-08, replaces W16's batches

The user, 2026-11-08: "The whole thing compresses up to 2 minutes of gas propagation into a single tick. I think we probably
should just scrap it and make it tick based, so that 2 minutes of gas propagation and effects would take 2 minutes to happen in
game."

**What W16 does, read again.** An explosion's gas is worked to its end (`GasNetwork.Run`, up to `GasRunS` 180 s of gas, a slow
burn 4,500-8,600 steps) at one moment, as today's model always did; W16 only moved that moment a second later and the work onto a
thread. So a magazine whose stow burns for two minutes has torn every plate, killed every man its gas reached and broken her girder
in the second after the flash, while nothing else in the world moved. The batches are the price of keeping that one moment
consistent: a copy of her condition, a guard on it, her flood and fire step, her outages' clear and a salvo's shock all waiting behind
the batch, a flush before any command on her, two reentrancy flags. And the cost is still one lump: at 100x a race longer than its
slack stalls the sim (W16f's 1.1 s).

**The rule instead: an explosion's gas is a process on her, and the world steps it.**

1. **Her gas is her own, live:** one run a ship (the network's working state, today one a thread and reset every run, becomes a
   ship's, pooled by class: `GasRun`), from her first explosion until it's done (every panel caught up, nothing burning, nothing
   more can give: today's `Done`), at most `GasRunS` after the newest explosion joined it (a run kept alive by new sources isn't
   cut off). A shell's gas is gone in a slice or two and salvos come tens of seconds apart, so runs end between them; only what
   keeps making gas (a burning stow) keeps one live. A space whose gas is back at the atmosphere with no load on its boundaries
   leaves the stepped set and joins again when gas reaches it, so a long run costs where its gas is, not where it has been.
2. **It's stepped on a lattice of world time,** `GasSliceMs` (100 [EST], a divisor of `FloodMs`): an event a slice while she has a
   run. Within a slice the solver keeps its own steps (the step's control as now, clipped at the slice's end, and the step it wanted
   carried over to the next).
3. **Every advance lands what it did on her live condition at once:** what tore (today's `Commit`, already written as deltas), the
   holes to the sea (her water's step wakes for them), the men and gear in the spaces it reached as their boundaries' felt rises
   (`Casualties.Gas` takes the conditional risk from the last level to the new within an episode, decision 4), a train's
   handling room gone hot (the flash), a gunhouse vented or lifted, a magazine lit (spent from then: her mounts there have no more
   rounds), her girder read when her section lost something, and what she can do worked out again when anything changed. There's
   nothing to guard, copy or hold back: everything reads her as she is.
4. **An explosion joins her run at its moment:** the run advanced to then, its near field applied at once (shock and holes are
   microseconds: the hit's marks and the screens' ring keep them), its gas into the network from then. A salvo's second shell, or a
   magazine her fire sets off while the first one's gas is still in her, meets that gas: one gas in one ship (today each run starts
   from still air and only the tears carry over).
5. **The same in every tier.** The lattice is world time, so a race takes its two minutes at any compression
   (`same-at-any-compression`); in the coarse tier too, as the flood step already runs there. `HitDelayMs` and the coarse tier's
   "at once" go: a hit's own parts (the walk, fragments, spall, the near field, fires started, stores struck) land when it comes,
   in every tier.
6. **Read once a slice:** her liquid in each cell (cached once a run now), her waterline (her list moves the faces under the sea),
   and her stows' flooded share, so a magazine flooding mid-race (valves, holes) burns less from then. Her own water's step stays at
   `FloodMs`.

**What it costs.** The same steps as today, spread over the gas's own time. A slice's worst case is bounded by the solver's
shortest step (`DtMin` 0.1 ms): at most 1,000 steps in 100 ms, about 48 ms on Bismarck at 48 µs a step, and only in a violent
phase's first slices; a slow burn at `DtMax` is 5 steps a slice, about 0.25 ms. At 1x that's under the map's 0.3 s lead (W16e); at
100x a race's 700 ms spreads over the 1.2 s of real time its two minutes take, so the achieved speed dips instead of the map
freezing. Live runs at once are few (a shell's gas is done in a slice or two), so threads wait: the slices of several ships are
independent, a worker pass if ET5's numbers ask.

**What moves** (nothing tuned; read in ET4): a race's damage lands over its minutes, not in its first second; a ship can flood
through a torn side, counterflood or list while her magazine still burns, and break late (or not) as the moment and the section say;
her unharmed turrets fight on meanwhile; a second explosion meets the first's gas; a flooded magazine stops feeding the fire.
Historically that's the record's texture (gunhouses burning out for a minute, Seydlitz fighting on); for the look, the magazine
explosion's event (tier 3) comes when she's lost, which is now when her girder gives, not at the flash.

**What goes:** `HitDelayMs`, the batches (`DamageSystem.Batches.cs`: gather, close, land, flush, guard, the waits), the copies' pool,
`BatchWorker` and `DamageSystem.Threaded`, `steppingSlot` and `landingSlot`, and `ShipCondition.CopyFrom` with its reflection
test (nothing else reads it). **What stays:** W16b's `Capture`/`Work`/`Land` (a hit's own parts, at its moment), W16e's lead
(any stall, not only this one), W16f's figures in `-frametimes`.

### The steps

| | |
|---|---|
| ET0 | Measured first: over the 75 races of "Cost and convergence" and a shell burst of each size, the steps and the ms in each 100 ms of gas time (`explode --slices`), so `GasSliceMs` and the worst slice are numbers, not the bound above |
| ET1 | W16c-d out: back to every explosion worked at once (the output of `1817567b`, the freeze with it for a step), the items under "What goes" removed |
| ET2 | `GasRun`: the network's working state a ship's, pooled by class and made when the class comes in (no allocation in play); `Begin`, `Add` (a source at its moment), `Advance(to)` (steps to then and lands what they did), `Done`. Still run to its end at once: byte for byte as ET1 (benches and scenarios with `--hashes`) |
| ET3 | The world steps it (rule 1-6): the slice's event, the per-slice landing (casualties and gear by the rise in felt, the flash, vents, the girder, recompute, the wake), a source joining a live run, the stows' flooding read, the boundaries' and spaces' episodes (decision 4), spaces leaving and rejoining the stepped set, `GasRunS` from the newest source. The run's state in the hash, with a reflection test that it reads every field. Zero-allocation and replay tests; same hash on 1 and N workers |
| ET4 | The benches through the same slices: `Strike` then the run out (`DamageSystem.Settle` advances it slice by slice), `explode`, `magazine --race`, `hits`, the three history benches. The convergence check again with the slices' clip (`explode --step`; "Check again whenever the inputs move"). Readouts: Lion's four, `losses`, the panel (`balance --fc-runs 0`) beside W16c's |
| ET5 | Measured in the game, as W16f: the 2000 mm salvo at 1x, 30x, 100x, 240x, the sim's longest gap and the map's longest still; the Jutland's ms a game second while a race runs. A worker pass for the slices only if this asks |
| ET6 | Docs: explosions.md's model ("It runs to the end at the moment of the event" goes), CLAUDE.md, `magazine-explosions.md`'s event timing |

### Decisions (the user, 2026-11-08: 1-3 as proposed; 4 reworked on the user's question)

1. **The slice: 100 ms**, not `FloodMs`'s 1 s. ET0 checks the worst slice.
2. **One gas a ship** (rule 4): a second explosion joins the live run.
3. **The coarse tier steps it too** (rule 5).
4. **A boundary's load comes in episodes, not runs.** The user: "if we have one gas per ship and we get lots of small booms when
   does the run ever end?" Today a held boundary's seams are drawn once a run by the most it felt (review finding 1), and two shells'
   runs draw twice and add up (`JoinSeamM2`). Drawn at the end of one long run, a ship under steady fire would draw once by the
   highest peak, or never. So each boundary keeps its own episode: from when the load across it rises to when it falls back under
   `LeakWeepD` (the load that leaves no leak: no new threshold); its seams and closures are drawn then by that episode's peak, and
   the peak starts again. A space's men the same: their risk by the rise in felt within an episode, a new episode a new risk. Each
   small boom is its own episode, as each shell is its own run today, whether or not the run ends.

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
