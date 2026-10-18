# Fire-control templates

Written by `shipgen templates` from `src/Fleetwright.Shipgen/FireControlTemplates.cs`; don't edit by hand. The
designer's templates are `Content/Templates/fire-control.json`, from the same table.

A battery's fire control is a fit of tech blocks (`docs/battle/fire-control.md` F3): one optics block (what its
rangefinders are and how they read), one computer (how it predicts) and one mount block (how its guns are
laid). The fit names them, the design copies them into `fire_control_tech`, and a name the design doesn't carry
takes the catalogue's:

```json
"fire_control": {"main": {"directors": 1, "rangefinder_m": 4.6, "fit": ["Barr & Stroud FQ/FT (O2)",
  "Dreyer table Mk III-V", "Director, follow the pointer"]}}
```

A block is a machine or a make, never a year (the tech tree decides who has it); the combat model sees only its
numbers. The presets are named fits standing for the reference's levels (`research/simulation/fire_control_ref.py`
LEVELS 0-8, then its rows beside them): a level is a preset, not a type. Blocks named as a radar's stand-in carry
a radar's numbers until the radar pass. Sources: `research/simulation/fire-control-designer.md` §2 (the tech
values, tiers O1-O4, C0-C6, M0-M4), `fire-control-research.md` §3.3-3.5 and §5.3, and
`fire-control-research/computing.md` §3; [EST] marks numbers no source gives.

Read by the sim so far: the optics' magnification and readings a minute; every computer number above the mass;
the mounts' director firing, laying error and deflection wander. Shipgen reads the computer's mass and crew: its
transmitting station's weight and men (F4c). The rest (acuity, rigid base and flex, the top magnification,
coatings, the gyro mounting, the optics' and mounts' masses and crews, power, the stable vertical) is carried for
the steps that read it (F5: the sensors proper; ship motion; power with the power networks).

## Presets

| Preset | Optics | Computer | Mount | |
|---|---|---|---|---|
| eyeball | By eye | Eye and hand plot | Open sights | Level 0, c.1890: ranges by eye, each gun laid by its own layer. |
| rangefinder | Barr & Stroud FA (O1) | Hand plot | Early telescopic sights | Level 1, 1905: an early rangefinder. |
| clock | Barr & Stroud FQ/FT (O2) | Dumaresq and Vickers clock | Telescopic sights | Level 2, 1910: Dumaresq and range clock. |
| director | Barr & Stroud FQ/FT (O2) | Dreyer table Mk III-V | Director, follow the pointer | Level 3, 1916: the Dreyer table and director firing. |
| table | Stereo, long base (O3) | Admiralty Fire Control Table | Director, power drive | Level 4, the 1930s: the interwar central table. |
| rangekeeper | Coated, stabilised (O4) | Ford Mk 8 rangekeeper | Stable vertical, remote power control | Level 5, 1941-42: rangekeeper and stable vertical. |
| radar-spotting | Coated, stabilised (O4) | Ford Mk 8 rangekeeper, radar ranging (stand-in) | Stable vertical, remote power control | Level 6, 1945: radar spotting (its radar a stand-in until the radar pass). |
| auto-track | Auto-track ranging (radar stand-in) | Mk 1A computer | Stable vertical, power servo | Level 7, 1955: auto-tracking radar. |
| digital | Digital ranging (radar and laser stand-in) | Digital turn filter (Mk 86, WM-25) | Digital servo | Level 8, 1975: digital. |
| judgement | By eye | Judgement | Open sights | A sailor's judgement: the worst computer, for comparison (F2). |
| argo | Barr & Stroud FQ/FT on the Argo mounting | Argo clock Mk IV | Director, follow the pointer | Pollen's Argo clock (level 3b). |
| table-turn | Stereo, long base (O3) | Admiralty Fire Control Table, turn setting | Director, power drive | The AFCT with its turn setting (level 4t, decision 5). |
| japanese-plot | Stereo, long base (O3, ×25) | Type 92 Shagekiban | Director, follow the pointer (no RPC) | The Japanese plot of the 1930s (level 4j). |

## Optics

Kind; acuity ″; rigid base m and flex exponent; magnification (top at a full image); readings a minute; coated,
gyro-stabilised; mass kg per m² of base.

| Block | Kind | δ ″ | Rigid m / flex | × (max) | A minute | Coated, gyro | kg/m² |
|---|---|---|---|---|---|---|---|
| By eye | eye | 0 | 0 / 0 | 0 (0) | 1 | -, - | 0 |
| Barr & Stroud FA (O1) | coincidence | 14 | 2 / 0.75 | 24 (20) | 2 | -, - | 30 |
| Barr & Stroud FQ/FT (O2) | coincidence | 12 | 4 / 0.75 | 28 (28) | 2 | -, - | 28 |
| Barr & Stroud FQ/FT on the Argo mounting | coincidence | 12 | 4 / 0.75 | 28 (28) | 8 | -, gyro | 28 |
| Zeiss stereo (WWI) | stereo | 10 | 4 / 0.75 | 23 (28) | 2 | -, - | 28 |
| Stereo, long base (O3) | stereo | 12 | 5 / 0.75 | 28 (30) | 4 | -, - | 27 |
| Stereo, long base (O3, ×23) | stereo | 12 | 5 / 0.75 | 23 (30) | 4 | -, - | 27 |
| Stereo, long base (O3, ×25) | stereo | 12 | 5 / 0.75 | 25 (30) | 4 | -, - | 27 |
| Coated, stabilised (O4) | stereo | 12 | 6 / 0.8 | 25 (30) | 6 | coated, gyro | 25 |
| Auto-track ranging (radar stand-in) | stereo | 12 | 6 / 0.8 | 25 (30) | 60 | coated, gyro | 25 |
| Digital ranging (radar and laser stand-in) | stereo | 12 | 6 / 0.8 | 25 (30) | 30 | coated, gyro | 25 |

## Computers

Tracker, helm-free; latency s; q m²/s³; the clock's limits (m/s, °/s); the eye's first guess (kn, °); the residual
bias (% of range, mil); own turn's kick m; first ladder step m; salvo-to-salvo error % of range; and the filters'
own numbers. Mass t, crew, power kW.

| Block | Tracker | Latency | q | Limits | Prior | Bias | Kick | Step | Salvo | Filter | t, crew, kW |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Judgement | judgement | 5 | 0 | - | 5, 30 | 2.5, 4 | 0 | 550 | 2.5 | looks again 60 s | 0, 0, 0 |
| Eye and hand plot | rate_clock | 20 | 0.02 | - | 5, 30 | 2.5, 4 | 0 | 550 | 2.5 | - | 0.2, 3, 0 |
| Hand plot | rate_clock | 15 | 0.02 | - | 4, 25 | 2, 3 | 0 | 366 | 2 | - | 0.2, 3, 0 |
| Dumaresq and Vickers clock | rate_clock | 15 | 0.03 | 18.3, 0 | 3, 20 | 1.5, 2.5 | 0 | 366 | 1.8 | - | 0.8, 4, 0 |
| German range clock (EU-Anzeiger) | rate_clock | 15 | 0.03 | 18.3, 0 | 3, 20 | 1.5, 2.5 | 0 | 366 | 1.8 | - | 0.8, 4, 0 |
| Dreyer table Mk I-II | rate_clock | 15 | 0.03 | 18.3, 0.25 | 2.5, 15 | 1.2, 2 | 300 | 366 | 1.5 | - | 4, 10, 1 |
| Dreyer table Mk III-V | rate_clock, helm-free | 15 | 0.03 | 18.3, 0.25 | 2.5, 15 | 1.2, 2 | 300 | 366 | 1.5 | - | 4, 10, 1 |
| German clock and plot (EU-Anzeiger, C12) | rate_clock, helm-free | 15 | 0.03 | 18.3, 0.25 | 2.5, 15 | 1.2, 2 | 300 | 366 | 1.5 | - | 4, 10, 1 |
| Argo clock Mk IV | true_course, helm-free | 8 | 0.03 | - | 2.5, 15 | 1.2, 2 | 200 | 366 | 1.5 | - | 2, 4, 1 |
| Admiralty Fire Control Table | true_course, helm-free | 8 | 0.03 | - | 2, 12 | 0.6, 1.5 | 250 | 366 | 1.2 | - | 5, 8, 5 |
| Admiralty Fire Control Clock | true_course, helm-free | 8 | 0.03 | - | 2, 12 | 0.6, 1.5 | 250 | 366 | 1.2 | - | 3, 4, 3 |
| Admiralty Fire Control Table, turn setting | turn_rate, helm-free | 8 | 0.03 | - | 2, 12 | 0.6, 1.5 | 250 | 366 | 1.2 | turn 0, speed 0.1 | 5, 8, 5 |
| German Rechenstelle (1930s) | true_course, helm-free | 8 | 0.03 | - | 2, 12 | 0.6, 1.5 | 250 | 366 | 1.2 | - | 5, 8, 5 |
| Type 92 Shagekiban | rate_clock, helm-free | 15 | 0.03 | - | 2, 12 | 0.6, 1.5 | 400 | 366 | 1.3 | - | 4, 15, 2 |
| Ford Mk 8 rangekeeper | true_course, helm-free | 3 | 0.05 | - | 2, 12 | 0.4, 1 | 150 | 366 | 1 | - | 3, 4, 10 |
| Ford Mk 8 rangekeeper, radar ranging (stand-in) | true_course, helm-free | 2 | 0.05 | - | 2, 12 | 0.4, 1 | 150 | 366 | 1 | - | 3, 4, 10 |
| Mk 1A computer | acceleration, helm-free | 1 | 0.02 | - | 2, 12 | 0.4, 0.8 | 50 | 366 | 0.7 | τ 20 s, σ 0.1 m/s²; detects turns | 2.5, 3, 10 |
| Digital turn filter (Mk 86, WM-25) | turn_rate, helm-free | 0.5 | 0.01 | - | 2, 12 | 0.3, 0.5 | 20 | 366 | 0.4 | turn 0, speed 0.1; detects turns | 0.8, 2, 8 |

## Mounts

| Block | Director firing | Stable vertical | Laying mil | Deflection wander mil |
|---|---|---|---|---|
| Open sights | no | no | 3.1 | 3 |
| Telescopic sights, fired on the roll | no | no | 2 | 2.5 |
| Early telescopic sights | no | no | 0.85 | 2.5 |
| Telescopic sights | no | no | 0.55 | 2.5 |
| Director, follow the pointer | yes | no | 1 | 2 |
| Director, follow the pointer (no RPC) | yes | no | 1 | 1.5 |
| Director, power drive | yes | no | 0.8 | 1.5 |
| Stable vertical, remote power control | yes | yes | 0.5 | 1.2 |
| Stable vertical, power servo | yes | yes | 0.4 | 0.8 |
| Digital servo | yes | yes | 0.3 | 0.5 |

- **By eye:** No instrument: ranges by eye, about 20% of the range (fire-control-research.md §3.1). A director without a rangefinder, or one whose instrument isn't read.
- **Barr & Stroud FA (O1):** Early coincidence, c.1895-1906: the FA2 and FA3 4.5 ft (1.37 m) at ×24, the Japanese ships' at Tsushima and Oslyabya's (§3.3; early-hit-rates.md). O1: 14″, rigid to 2 m, ×20 at a full image (designer §2.5). Unstabilised, 2 readings a minute (§3.5).
- **Barr & Stroud FQ/FT (O2):** Mature coincidence, c.1906-1925: the FQ2 9 ft (2.74 m) and FT24 15 ft (4.57 m) at ×28 (§3.3). O2: 12″, rigid to 4 m (designer §2.5). Unstabilised, 2 readings a minute (§3.5).
- **Barr & Stroud FQ/FT on the Argo mounting:** The O2 instruments on Pollen's gyro-stabilised mounting: range-taking about fivefold, 8 readings a minute (§3.5).
- **Zeiss stereo (WWI):** The German 3 m stereo of 1914-18 at ×23: Lienau's ±65 m at 10 km and 165 m at 16 km give 10″ (§3.3). Rigid base as O2's [EST]; unstabilised, 2 readings a minute (§3.5). Stereo's haze and contrast advantages are the sensors' (F5).
- **Stereo, long base (O3):** Interwar stereo and long bases (Zeiss, interwar B&S), c.1925-40: O3, 12″, rigid to 5 m (designer §2.5). 4 readings a minute (§3.5's WWII director 4-6).
- **Stereo, long base (O3, ×23):** O3 at ×23: Bismarck's 10.5 m stereo (§3.3, its magnification [INFERRED]).
- **Stereo, long base (O3, ×25):** O3 at ×25: the Japanese plot's preset (fire_control_ref.py LEVELS).
- **Coated, stabilised (O4):** USN 1943: coated, gyro-stabilised, rigid to 6 m (designer §2.5); the Mk 48 26.5 ft at ×25 (§3.3); 6 readings a minute (§3.5).
- **Auto-track ranging (radar stand-in):** O4's instruments read as an auto-tracking radar's range: 60 a minute (§3.5). Stands in for the radar block until the radar pass.
- **Digital ranging (radar and laser stand-in):** O4's instruments read as the 1970s' radar and laser: 30 a minute (fire_control_ref.py LEVELS 8). Stands in for the radar block.
- **Judgement:** A sailor's eye and head: the range guessed, the target's course and speed guessed from her look and led by it, looked at again each minute, never a rate learnt (F2: the worst computer there is).
- **Eye and hand plot:** Ranges by eye, rates by a hand plot (fire_control_ref.py level 0; designer C0).
- **Hand plot:** Rangefinder ranges, rates from a hand plot and estimates, no machine (level 1: the Tsushima era; designer C0).
- **Dumaresq and Vickers clock:** The Dumaresq's rates run on by the Vickers range clock (1906): constant rates only, the roller slipping on a change (computing.md §3; level 2, designer C1).
- **German range clock (EU-Anzeiger):** The German WWI range-rate indicator and clock without the plot, the cruisers' (computing.md §3): no specifications found; taken as the Dumaresq and Vickers clock's [EST].
- **Dreyer table Mk I-II:** The Dreyer table's range and bearing plots and clock before the gyro feed (1911-12): the Mk III's numbers without helm-free, so own ship's turns are re-learnt (computing.md §3: helm-free from the Mk III).
- **Dreyer table Mk III-V:** The Dreyer table from the Mk III (1914-22), helm-free by the gyro: a rate clock limited to ±1,200 yd/min and ±15°/min, its bearing plot the weak point (computing.md §3; level 3, designer C2). Crew 7-8 at the table, up to 30 in the station.
- **German clock and plot (EU-Anzeiger, C12):** The German WWI range-rate clock and plot (computing.md §3). No specifications found; taken as the Dreyer Mk III's, helm-free by the gyro [EST], the two navies' 1916 machines doing the same job.
- **Argo clock Mk IV:** Pollen's true-course plotter and variable-speed clock (1912-13): follows a changing range rate, gyro-corrected; about six in RN service at Jutland (computing.md §3; fire_control_ref.py level 3b). Mass and crew [EST].
- **Admiralty Fire Control Table:** The AFCT Mk I-X (1927-45): clock and MPI plot, helm-free, a straight-course target (computing.md §3; level 4, designer C3).
- **Admiralty Fire Control Clock:** The AFCC, the AFCT without its plot, for destroyers and secondaries (computing.md §3; Haida's Mk III limited to 16,000 yd): the table's numbers [EST]; mass and crew [EST].
- **Admiralty Fire Control Table, turn setting:** The AFCT with a hand-set enemy rate of turn: a turn filter whose rate adapts slowly (level 4t; [UNCERTAIN] in the research, a computer option anyone may fit, decision 5).
- **German Rechenstelle (1930s):** The German computing rooms of the 1930s, fore and aft under armour (computing.md §3): no specifications found; the research's table puts them with the AFCT, whose numbers they take [EST].
- **Type 92 Shagekiban:** The Japanese Type 92 (1932) with its Sokutekiban: rates from a range-rate plot, deflection by hand, no gyro horizon; rates rebuilt after a manoeuvre (computing.md §3; level 4j). Crew 7 + 8; mass [EST].
- **Ford Mk 8 rangekeeper:** The Ford Mk 8 with the stable vertical (1941-42): continuous, helm-free, tilt, wind and drift corrected; one operator (computing.md §3; level 5, designer C4).
- **Ford Mk 8 rangekeeper, radar ranging (stand-in):** The Mk 8 fed by radar spotting (level 6): its shorter latency is the radar's, standing in until the radar pass.
- **Mk 1A computer:** The 1950s analog with a curved-course (acceleration) predictor and the "target just turned" button (level 7, designer C5).
- **Digital turn filter (Mk 86, WM-25):** The 1970s digital systems with a turn-following filter (level 8, designer C6; the filter [INFERRED]). Its speed noise is the sim's 0.1, not the reference's 0.05² (F2: ships lose way in a turn).
- **Open sights:** Each gun laid by its layer over open sights, fired on the roll (table 1: 12.6′ an average layer, 10.8′ trained less the gun's 2′: 3.1 mil): before continuous aim, to the late 1890s (level 0).
- **Telescopic sights, fired on the roll:** Each gun by its own telescope, fired as the roll brings the sight on (table 1: roll, telescope, trained 7.1′, less the gun's: 2.0 mil): heavy guns until power laying, continuous aim working only to about 9.2″ (fire-control-research.md §6).
- **Early telescopic sights:** Each gun by its own early telescope, continuous aim (table 1: continuous, early telescope, average 4.1′, 3.6′ trained less the gun's: 0.85 mil; M0): the medium guns of 1899-1905, the Tsushima era (level 1).
- **Telescopic sights:** Each gun by a good telescope, continuous aim (table 1: continuous, telescope, trained 2.7′, less the gun's: 0.55 mil), no director (level 2).
- **Director, follow the pointer:** Director firing: the guns laid and fired from the director, the turrets following its pointers (M1, 1912-16; level 3).
- **Director, follow the pointer (no RPC):** Director firing without remote power control, the Japanese 1930s (no RPC on Yamato, computing.md §3): the Japanese plot's preset numbers.
- **Director, power drive:** Director firing with power training and elevation (M2, interwar; level 4).
- **Stable vertical, remote power control:** A stable vertical and gyro firing, the mounts under remote power control (M3, 1941-45; levels 5-6).
- **Stable vertical, power servo:** The 1950s' servos (fire_control_ref.py level 7), between M3 and M4.
- **Digital servo:** Digital servos (M4, 1970s; level 8).
