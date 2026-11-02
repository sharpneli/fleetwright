# Own course and speed into the fire-control table, and the gyrocompass in a turn
Status: draft    Updated: 2026-11-02    Request: [REQUESTS.md, 2026-11-02 "Own course and speed into the fire-control table, and the gyrocompass in a turn"](../../REQUESTS.md)    Script: [own_motion_ref.py](own_motion_ref.py)

Tags: [S] sourced (source in the Sources list, by number); [INF] my inference or arithmetic; [EST] a number chosen for the model, no source; [UNC] a single weak source.

## Summary

- **The gyrocompass's own error after a turn is not substantial for a helm-free table. FL1 stays on that count.**
  - At the North Sea's latitude, a 90° turn at 20 kn leaves a ballistic residual of about 0.6°, and a 16-point turn about 1.3°. Each decays as an ~85-min swing over one to three hours [S 9, INF].
  - That error turns her course and every target bearing together, because the same gyro fed the Dumaresq and the bearing plot [S 1]. So it cancels in the relative geometry. Only its rate of change gets through: at most 0.05–0.1°/min, which is 7–14 m at 14.6 km over a 45 s horizon (script §3).
  - The 2° test that took the Dreyer's zigzag hits from 83 to 16% put the error into her course alone, with the readings' bearings left true. That case does not occur at sea for a gyro error.
- **Course.**
  - **Geared to the gyro, so "at once" is right:** the Dreyer Mk I* to V ("continuous, automatically fed to Dumaresq and Bearing Plot") [S 1], Pollen's plotter (a gyro relay) [S 4] and the Ford Mk 8 [S 7, 8].
  - **From a gyro repeater, how not said:** the Ford Mk I (1917), "received from a repeating gyrocompass" [S 6].
  - **Set by hand:** the Dreyer Mk I (handwheel) [S 1], the 1917 Ford Mk II (knob, reset "at once" on any change) [S 18], and the **Type 92**, where an operator follows the gyro pointer ("Follow up compass course as received from the gyrocompass") [S 5].
  - **Not found:** the German WWI plot and the AFCT.
- **Speed.**
  - **By hand:** the Dreyer ("From Forbes log … continuous, manually fed to Dumaresq") [S 1], the Ford Mk I and II [S 6, 18] and the Type 92 [S 5].
  - **Automatically:** the Mk 8, from the pitometer [S 7, 8].
  - **Optional:** Pollen's plotter could be driven "by a log or by the propulsive machinery" [S 4].
  - **Not found:** the AFCT. The one clue, from a Belfast veteran, suggests it was set by hand [UNC 17].
  - **How late a hand setting followed a change:** no source gives a rate. The Forbes log had an "instantaneous speed" pointer, and speed repeaters stood in the fire-control office and turrets for the Dumaresq and clock operators [S 3]. That points to a man following a pointer, so seconds to tens of seconds.
  - FL1's 30 s [EST] costs 43 m over the horizon at the turn's worst; 10 s costs 22 m (script §4). Both are below the cliff.
- **The measured costs of own turns** (Jurens, the USN 1927–45) [S 10]:
  - The author names no cause except one, implicitly. He credits the cure to "improved stable verticals".
  - The stable vertical's mercury erection can't tell a turn's acceleration from gravity. It was cut out in a turn, and the gyro then drifted free [S 11, 12, 13].
  - Free drift of a few arc-minutes a minute through a ~4-min 450° turn is 10–20′ of level. That is roughly 150–300 yd at 20–25 kyd, the "several hundred yards" of North Carolina 1945 [INF, script §6]. This belongs to FL7 (the stable vertical), not FL1.
- **Two real own-turn errors are not in the sim, for scale:**
  - **Sideslip:** a ship's track lags her heading in a turn, so the table's "course" (the heading) is off by the drift angle. 3–10° is 1–3.5 kn of her velocity [INF].
  - **Stale apparent-wind corrections** on early machines [S 14].

## Details

### 1. Own speed, by machine

| Machine | Own speed | How late | Source |
|---|---|---|---|
| Dumaresq (1902–) | Sliding assembly on the bar, set by hand on a knot scale; "could use only a number of pre-set speeds" | When the operator resets it | [S 2] Wikipedia Dumaresq; [S 15] jutland1916 |
| Dreyer Mk I | By hand; "incapable of adapting to rapid speed changes" | — | [S 15] |
| Dreyer Mk I*–V | "From Forbes log … knots … continuous, manually fed to Dumaresq" | Not stated. The Forbes repeater beside the operator suggests he followed it continuously [INF] | [S 1] Dreyer Handbook 1918 |
| Dumaresq, some versions | "the speed input was automated using a Forbes Log" | — | [S 2]. This conflicts with the 1918 handbook; probably late or individual fits [INF] |
| Pollen plotter (patent 1913) | Speed shaft by hand, "or it may be driven by a log or by the propulsive machinery of the ship" | — | [S 4] US 1,123,795 |
| Argo clock | Not found [INF: as the plotter] | — | — |
| German WWI (EU-Anzeiger, C12, plot) | Not found | — | Gap. Anschütz's navigational Koppeltisch linked heading and log mechanically, but is not said to feed the guns [S 16] |
| AFCT / AFCC | Not found. "The engine room would be given speed, and that would be sent down here" (Belfast) | — | [UNC 17]. Haida's AFCC list of hand entries (wind, enemy course and speed, air temperature) leaves own speed out [S 19] |
| Type 92 Shagekiban | An operator's "setting of own ship's speed, target speed and target inclination" by crank | Not stated. The same man has the enemy's settings, so he is busy [INF] | [S 5] navweaps tech-086 |
| Ford Mk I (Texas 1917) | "entered manually" | — | [S 6] Wildenberg 2015 |
| Ford Mk II (OP 460, 1917) | Speed knob 0–36 kn; "if, at any time, a change of course or speed is made, the operator must receive the information at once" | Report and reset | [S 18] |
| Ford Mk 8 | "received automatically from the pitometer log" ("via the Stable Vertical Control Cabinet"); by hand in an emergency | The log's response | [S 7] NavPers 10798 19F; [S 8] tech-086 |

**Logs.**
- **Forbes** (RN capital ships by 1912) [S 3]:
  - an "instantaneous speed" pointer and a distance counter, with speed-only repeaters in the fire-control office and turrets, "initially used to set the dumaresq and range clocks";
  - "not entirely accurate" in its 1912 tests;
  - no response time or averaging interval found.
- **Chernikeeff** (RN from the mid-1920s, e.g. Rodney) [S 20]:
  - speed read by timing the interval between flashes against a table in half-knots: 17 s = 10.5 kn, 72 s = 2.5 kn;
  - a 1926 demonstration lists "Correction of the speed every 20 seconds".
  - [INF] Both points are one flash per about 1/20 nm, so a reading is an interval old: about 7 s at 25 kn and 15 s at 12 kn.
- **Pitometer** (USN):
  - speed from √(total − static pressure);
  - poor at low speed;
  - "often interfaced directly into warship fire-control systems" [S 21];
  - no response figure found.
- **Logs in a turn:**
  - A log reads only the fore-and-aft speed through the water; "few ships are fitted with a two component log" [S 22].
  - A later USN own-ship speed computer put in "a small time lag" on purpose, because when the rudder goes over "the ship slides sidewise to some extent" and keeps its old track briefly [S 23].
  - [INF] So in a turn the log under-reads her speed over the ground by about cos β, and the heading is not her track.

**Size** (script §4: 20 kn shedding 25% with a 30 s time constant, a hard turn):

| Lag | Peak speed error | Own reckoning off afterwards | Aim error over a 45 s horizon |
|---|---|---|---|
| 30 s (FL1's hand-fed [EST]) | 1.8 kn | 77 m | 43 m |
| 10 s | 1.0 kn | 26 m | 22 m |
| 3 s (FL1's automatic [EST]) | 0.4 kn | 8 m | 9 m |

The reckoning's offset is common to her place and every reading taken from it, so it cancels once the speed has caught up. Only the transient rate counts, as FL1 has it.

### 2. Own course, by machine, and the transmission

- **Dreyer table, own heading via** [S 1]:
  - Mk I "handwheel input";
  - Mk I*, III, III* "automatic input";
  - Mk II "automatic input ?" (the handbook's question mark);
  - Mk IV–V "electrical".
  - The gyro also fed the **bearing plot**. Course and bearings came through one gyro.
  - The wartime tables had "electrical follow-up, with motors to update the range and bearing clocks … As the ship turned, the circuits would close and the motors would turn" [S 24].
  - The 1930 Mk III* pamphlet's contents list a "Compass Control Gear", a "Compass control unit" and a "Power Follow-up Mechanism" [S 25].
- **Helm-free Dumaresq Mk VI\*:** "permitted a gyrocompass input to automatically track own ship as it altered course" [S 2].
- **Pollen plotter:** the turning shaft follows a scale that "is gyroscopically controlled", via "a relay device operated by the gyroscope in the well known manner"; a hand drive was also fitted [S 4].
- **Ford:**
  - The 1917 patent has course set by a knob [S 26].
  - The Mk I had course "received from a repeating gyrocompass" [S 6]. That was either geared or matched by hand; the source doesn't say [INF].
  - The Mk 8 took course from the gyro automatically [S 7, 8].
- **Type 92:** "Follow up compass course as received from the gyrocompass" is an operator's duty on both the Shagekiban and the Sokutekiban [S 5]. **This is a hand follow-up, not gearing.**
- **The transmission:**
  - Sperry repeaters were step-by-step motors (e.g. 20 steps of 18° per armature revolution), geared down many turns to one card revolution [S 27]. The card step is not stated in any source reached. The 1/6° often quoted is a later figure [UNC].
  - A comparable Admiralty transmitter followed at "24 degrees per second" [S 28], fifty times a battleship's fastest turn.
  - Sperry's follow-up drove the phantom ring by a small motor switched by the sensitive element, with a deliberate small "hunt" against friction [S 9, 27]. No lag figure in a turn was found.
  - [INF] A step of 1/6° to 1/4° is ±21–32 m across at 14.6 km as truncation. That is smaller than the Dreyer's own ¼° bearing quantization, and in the same frame for both course and bearings.

### 3. The gyrocompass's error after a change of course or speed

**Speed (steaming) error** [S 9]:
- tan δ = V cos C / (900 cos φ ± V sin C), V in knots, 900 kn the earth's surface speed at the equator; in degrees about V cos C / (15.7 cos φ).
- The compass reads west of true on northerly courses.
- Chalmers gives 1.25° at 20 kn due north on the equator, and 2.5° (Anschütz tables) or 2.41° (Sperry's) at 60°N.
- Sperry 1911: "about two degrees for a difference of thirty knots" [S 29].
- At 56.5°N, 20 kn: 2.3° due north, 1.6° on NE, 0 on E or W (script §1).
- **How it was corrected:**
  - *Sperry:* latitude and speed dials set by hand; a course-corrector ring moved the lubber line with course automatically [S 9].
  - *Anschütz:* correction by tables [S 9].
  - *Brown:* tables or eccentric repeater cards [S 9].
  - A speed dial left at the old speed leaves a steady error of ΔV cos C / (15.7 cos φ): 0.6° for 20→15 kn heading north at 56.5°N [INF]. Being steady and common to course and bearings, it does nothing to a table's solution.

**Ballistic deflection** [S 9]:
- After a change of north-south speed the pendulous element precesses the gyro by an amount "the same at the equator as in 60 deg.", while the change in the settled error it should match grows as 1/cos φ.
- "In practice the latitude selected is 40 deg." There the deflection is dead-beat: the compass moves straight to its new heading. Elsewhere it is not.
- Settling off the design latitude takes "two or three hours after the new speed has been attained".
- Periods: the early Anschütz just over 61 min undamped; later designs "deliberately made 85 minutes or thereabouts" [S 9]. Schuler published the 84.4 min condition in 1923.
- The part of the change left as error is 1 − cos φ / cos φ₀. With φ₀ = 40° that is +0.28 at 56.5°N, +0.55 at 70°N and −0.31 on the equator [INF, script §2].

| At 56.5°N, 20 kn, tuned at 40° | Change of settled error | Left as error just after | Envelope after 30 / 90 min | Fastest change |
|---|---|---|---|---|
| 90° turn N → E | 2.3° | 0.64° | 0.30 / 0.06° | 0.05°/min |
| 16-point turn N → S | 4.6° | 1.28° | 0.59 / 0.13° | 0.10°/min |
| 16° turn 045 → 061 | 0.5° | 0.14° | 0.06 / 0.01° | 0.01°/min |
| Zigzag ±10° about E, a leg | 0.8° | 0.21° | 0.10 / 0.02° | 0.02°/min |
| Zigzag ±10° about N | 0.03° | — | — | — |

(The decay assumes an 85-min period and each half swing a third of the last [EST, after "two-thirds damping"]. Chalmers's own example uses half.)

- **Check against a measurement:** a 1972 survey measured about 6° after 180° turns at 13.5 kn in Lancaster Sound (74°N) [S 30]. The ballistic residual alone gives 4.0° there (script §2). Same order; the damping error adds to it.
- **IMO 1973 standard** (a modern ceiling): ±2.5° after "a rapid alteration of course of 180° at speeds up to 20 knots", ±1° settled [S 31].
- **Damping error:**
  - The damping system also feels horizontal acceleration ("an out-of-balance torque … causing the oscillation of the gyro which is the damping error") [S 32].
  - Sperry designs of the 1920s could switch the damping out during a manoeuvre. That "does not entirely eliminate" the oscillation [S 33].
- **Rolling (intercardinal) error:**
  - This made the 1908–10 single-gyro Anschütz unsatisfactory at sea. The 1912 three-gyro redesign was "on the whole satisfactory", and the only RN type in early 1913 [S 34].
  - In the 1913 Greenwich trials Sperry's error was "no greater than in the Anschütz". Its "Floating Ballistic" then passed the rolling table but "caused very large errors" in heavy, confused seas [S 34].
  - No degree figures for service were found. It belongs to the seaway, not to a turn.
- **Settled accuracy:**
  - Sperry Mk V (Admiralty pattern, 1943–63): follow-up "azimuth accuracy of 2 minutes" [S 35].
  - FORACS-based model: a settled error of −1° to +1°, with 0.5–1.5° swings after manoeuvres [S 36].
  - No sourced WWI figure found. The research's "about 0.1–0.5°" (fire-control-research.md) stays unsourced.
  - No source tying "wandering gyros" to the Dreyer's results was found.
  - IJN gyros were "neither accurate nor reliable" [S 5].
- **Did tables correct for it?** No source says any table did. Nothing in the inputs lists [S 1, 7, 8] does more than take the gyro's course.

**What reaches the table** [INF, script §3]:
- Write the gyro's error as ε(t). The director's train is relative to her head. The table makes it a true bearing with the gyro (B + ε), and takes her course from the same gyro (H + ε). A plot's target course is found in the same frame.
- The geometry is rotated whole, so a constant ε cancels. The gun's train goes back out relative to her head, so the ε comes off again.
- A changing ε reads as a bearing rate dε/dt that the table extrapolates over its horizon:

| Kick | Fastest change | Aim error at 14.6 km, 45 s horizon |
|---|---|---|
| 0.5° | 0.04°/min | 7 m |
| 1.0° | 0.07°/min | 14 m |
| 2.0° | 0.15°/min | 28 m |

Against that, a 2° error in her course alone, with the readings' bearings left true, is 510 m.

### 4. What own turns cost helm-free fire, and why

All from Jurens, "The Evolution of Battleship Gunnery in the U.S. Navy, 1920–1945", *Warship International* 1991 [S 10]:

| Case | Machine [INF from the period] | Result | Cause given |
|---|---|---|---|
| 1927–28, course changed 20° then 30° in a 15-salvo practice at about 25 kyd (F.T.P. 89) | Ford Mk 1 family (own speed by hand); level by optical horizon | "The average error in placement of the MPI was about 400 yards in range and deflection, and not a single salvo hit" | None. "Even thirty or forty degree turns were often sufficient to throw off the system entirely" |
| California 1941, 150° countermarch at 23 kyd (F.T.P. 210-2) | Mk 1 family [INF]; stable verticals in by 1935 | 212 yd average deflection error, one wild salvo 4,100 yd left | None. Maryland's 1,664 yd salvo was a trainer firing 5° out of train |
| Turns generally | — | "'on' in deflection and 'off,' in range, or vice-versa"; the same seen in Japanese salvos at Samar | — |
| North Carolina 1945, two 450° and two 100° turns at constant revolutions, aiming at the sun and moon | Mk 8 + stable vertical | "could indeed move the MPI several hundreds of yards at battle ranges"; judged "no more than would be desirable when using a rocking ladder"; commanders "might maneuver freely" | Credited to "improved stable verticals" |

- Earle (1922), on Jutland: "Range keeping was not perfected and the changes of course threw out the gunnery" [S 37].
- **The stable vertical in a turn:**
  - The Mk 41's control panel had "switches operating the mercury cut-out valve which prevents malfunctioning of the gyro during high-rate maneuvers of own ship", worked from "synchro inputs of own-ship speed and course" [S 11].
  - Arma's 1945 patent explains why. Mercury control "cannot distinguish between gravity and acceleration forces", and the prior practice of plugging it in a turn left the gyro "derelict and free to drift until the acceleration disappeared" [S 12]. Steady accuracy "within two minutes of angle".
  - The RN found the same in 1921. Gray's vertical-datum gyro was "upset by alterations of course"; his remedy was "to temporarily lock the controlling mechanism during alterations of course". The Sperry/Henderson free gyro wandered "three minutes of arc per minute of time" [S 13].
  - The Mk 6 stable element's manual has a section "Sensitive Element Shows Error on Turn" (p. 189), not reached [S 38].
- **Size** [INF, script §6]:
  - At 25 kn on a 700 m radius the apparent vertical is 83′ off. Uncut, the vertical is dragged toward it.
  - Cut out and drifting at ~3′/min, a 4–5 min 450° turn is 12–15′ of level.
  - A 16-in/45 at 20–25 kyd moves ~940 yd a degree of elevation, ~15 yd an arc-minute [S 39]. That gives ~200 yd, the "several hundred yards".
- **The 1927–28 case predates the stable verticals:**
  - Level came from a director laid on the horizon (selective cross-roll) [S 10].
  - Own speed went into the Mk 1 by hand [S 6].
  - The Mk 1's wind correction was set from the apparent wind, and "whenever the ship changes course the apparent wind changes, which will cause errors unless a corrected ballistic is applied" [S 14].
  - Which mattered most isn't recorded [INF].
- **Not found:** RN measured costs of turns (no Grand Fleet or AFCT-era trial figures), German or Japanese figures, and the stable vertical's figures in a turn.

## Recommendations for this codebase

1. **The gyrocompass: don't build it.** The manoeuvre error at 50–60°N is ≤ 1.3° and decays over hours. A helm-free table sees only its rate: ≤ 15 m at 15 km. If it is ever wanted (high-latitude scenarios: 0.55 of the change at 70°N), put it into `GunnerySystem.Reckon` as a frame rotation ε, never into the course alone:
   - the dead reckoning on `HeadingDeg + ε`;
   - `Reckoned` rotating the reading's offset (`at − ship.Position`) by ε about own ship;
   - `Ordered` laying off at `bearing − ε`.
   A constant ε then cancels exactly, which is a good unit test. `ballistic_residual_fraction` and `envelope` in the script give ε(t).
2. **Course "at once" stands** for the Dreyer Mk III–V, the Argo, the AFCT (the RN's line from the Mk III, gyro-fed [INF]) and the Mk 8. The German clock and plot have no source either way; the 1912 Anschütz with electric repeaters makes geared course likely [INF], so keep it.
   - The catalogue's "Dreyer table Mk I-II" (not helm-free) fits the Mk I only. The handbook has the Mk I* taking heading automatically, and the Mk II probably ("automatic input ?") [S 1]. The difference is a split of the row, not a change to FL1.
3. **One exception worth considering: the Type 92 Shagekiban.** Its course is followed by hand from the gyro [S 5].
   - A man following a pointer lags. 0.5 s is 60 m and 1 s is 120 m at 14.6 km at the zigzag's 0.47°/s (script §5), near the cliff FL1 found.
   - If built: `own_course_lag_s` on the computer row, 0 for geared machines, [EST 0.5–1 s] for the Type 92. Applied as FL1's 2 s test was: the table's course late, the readings' relative bearings made true with it. That inconsistency is real here, because the director's train comes in at once and the course by hand.
   - Run it at 0.5 and 1 s before choosing.
4. **Speed lags.**
   - *Hand-fed 30 s [EST]:* nothing measured either way. The sources point to a man following a log repeater's pointer ("continuous, manually fed"), and the Chernikeeff's speed is a flash interval old (7–15 s) [S 20]. So 10–30 s is the defensible range. The cost difference is 22 v 43 m at the worst moment, so 30 s can stay, or go to 15 s [EST].
   - *AFCT 3 s "automatic":* unsourced. The one clue says hand-set [UNC 17]. Mark it [EST]; consider the hand-fed value if the AFCT's own-speed path turns up hand-fed.
   - *Type 92's 20 s and the Mk 8's 3 s:* consistent with the sources.
5. **Not in the sim, so nothing to do now:** her track lagging her heading in a turn (drift angle).
   - `ShipMotion` moves ships along their heading, so the table's gyro course is exact there.
   - If drift angle comes into the motion model, every machine's own velocity (heading × log speed) picks up a 1–3.5 kn error in a hard turn, with no change to `Reckon`. That is larger than the speed lag.
6. **The real cost behind "several hundred yards" is FL7's, not FL1's.** For stable-vertical fits (level 5+), put the vertical's level and cross-level error in a turn:
   - mercury cut out while the turn's acceleration exceeds a threshold [EST: 0.05 m/s², i.e. ~0.3°];
   - free drift at [EST 2–3′/min, a random walk or a drawn rate per turn];
   - re-erecting afterwards over minutes.
   That gives ~100–250 yd through long turns at 20–25 kyd, and none on a steady course. Pre-stable-vertical director fire (levels 3–4) lays to the horizon and has no such term.
7. **When wind comes in** (the `resid_pct` / `defl_bias_mil` audit): a machine whose wind correction is set by hand from the apparent wind (Ford Mk 1, likely the Dreyer) [S 14] should keep its old setting through a turn until reset. "The later rangekeepers are much improved; most of them need not be corrected for changes in course" [S 14].

## Sources

1. Handbook of Dreyer Fire Control Tables, 1918 (notes). https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php (checked 2026-11-02)
2. Wikipedia, Dumaresq (citing C.B. 1456, 1918). https://en.wikipedia.org/wiki/Dumaresq
3. Wikipedia, Forbes Log. https://en.wikipedia.org/wiki/Forbes_Log
4. Pollen & Isherwood, US 1,123,795 (filed 1913). https://patents.google.com/patent/US1123795
5. navweaps tech-086, Japanese fire control (Type 92, Sokutekiban; Mk 8 inputs). https://navweaps.com/index_tech/tech-086.php (checked 2026-11-02)
6. Wildenberg, "The Revolutionary Rangekeeper", Naval History, Oct 2015. https://www.usni.org/magazines/naval-history-magazine/2015/october/revolutionary-rangekeeper
7. NavPers 10798, Naval Ordnance and Gunnery, Ch. 19F. https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html
8. As 5, Mk 8: "Own Ship Speed (Pitot Log via the Stable Vertical Control Cabinet)".
9. Chalmers, *The Gyroscopic Compass* (1920). https://gutenberg.org/files/57200/57200-h/57200-h.htm (Ch. IX–X read; XI–XVII not reached)
10. Jurens, "The Evolution of Battleship Gunnery in the U.S. Navy, 1920–1945", Warship International (1991). https://navweaps.com/index_inro/INRO_BB-Gunnery.php (main text and fn 11, 32)
11. NavPers 10798, Ch. 20E. https://eugeneleeslover.com/USNAVY/CHAPTER-20-E.html; Ch. 19G https://eugeneleeslover.com/USNAVY/CHAPTER-19-G.html
12. A. P. Davis (Arma), US 2,530,154 (filed 1945). https://patents.google.com/patent/US2530154
13. Progress in Gunnery Material, 1921 (ADM 186/251). https://vif2ne.org/nvk/forum/files/Realswat/(160312234717)_adm186-251.pdf
14. Fire Control Instructions, Ch. 7 (Rangekeeper Mk I Mod 3). https://www.ibiblio.org/hyperwar/USN/ref/FireControlInst/FireControlInstsCh7.html
15. Jutland 1916 Centenary, Range Finding and Course Plotting. https://www.jutland1916.com/tactics-and-technologies-4/range-finding-and-course-plotting-2
16. Broelmann on Anschütz (Deutsches Schiffahrtsarchiv 25, 2002). https://urn.dsm.museum/DSA/DSA25_2002_041055_Broelmann.pdf
17. IWM, HMS Belfast's armament. https://www.iwm.org.uk/history/hms-belfasts-armament-how-to-fire-the-6-inch-guns [UNC]
18. OP 460, Range Keeper Mk II (Oct 1917). https://www.eugeneleeslover.com/ENGINEERING/OD-460-1917.pdf
19. HMCS Haida, transmitting station. https://www.jproc.ca/haida/transmitting_station.html
20. Chernikeeff log: Auckland Sun, 7 Jun 1927 https://dynamic-rsnz.natlib.govt.nz/newspapers/SUNAK19270607.2.81 ; 1926 demonstration programme https://journals.lib.unb.ca/index.php/ihr/article/download/28200/1882520955/1882521203
21. Wikipedia, Pitometer log. https://en.wikipedia.org/wiki/Pitometer_log
22. Robinson, "Ship speed measurement", Journal of Navigation 28(3). https://www.cambridge.org/core/services/aop-cambridge-core/content/view/5D35E7405B7026E418813B610DF52EDC/S0373463300041278a.pdf/ship_speed_measurement.pdf
23. IC Electrician (NAVEDTRA 14120), own-ship speed computer. https://interiorcommunicationselectrician.tpub.com/14120/141200143.htm
24. Naval Gazing, Rangekeeping Part 2. https://navalgazing.net/Rangekeeping-Part-2
25. Pamphlet on the Mark III* Dreyer Table (1930), contents. https://dreadnoughtproject.org/docs/notes/Pamphlet_on_the_Mark_IIIStar_Dreyer_Table.php
26. H. C. Ford, US 1,370,204 (filed 1917). https://patents.google.com/patent/US1370204
27. Sperry patents: US 1,715,311 https://patents.google.com/patent/US1715311 ; US 1,296,440 https://patents.google.com/patent/US1296440 ; US 1,917,017 https://patents.google.com/patent/US1917017
28. Admiralty transmitting magnetic compass. https://journals.lib.unb.ca/index.php/ihr/article/download/27374/1882520130
29. "The Sperry Gyroscopic Compass for Naval Vessels", USNI Proceedings, Sep 1911. https://www.usni.org/magazines/proceedings/1911/september/sperry-gyroscopic-compass-naval-vessels
30. UNB Technical Report 100. https://gge.ext.unb.ca/Pubs/TR100.pdf
31. IMO Resolution A.280(8). https://wwwcdn.imo.org/localresources/en/KnowledgeCentre/IndexofIMOResolutions/AssemblyDocuments/A.280(8).pdf
32. Sperry, US 1,854,870 (filed 1927). https://patents.google.com/patent/US1854870A/en
33. Hight (Sperry), US 1,686,524 (filed 1925). https://patents.google.com/patent/US1686524
34. Admiralty Technical History, gyro-compasses (CB 1515(20) extract). http://www.gwpda.org/naval/th20.htm
35. HMCS Haida, gyrocompass. https://www.jproc.ca/haida/gyrocompass.html
36. NPS thesis (1970), gyrocompass error model. https://calhoun.nps.edu/server/api/core/bitstreams/115f0394-507b-4169-8f70-ff3efebbf360/content
37. Earle, "Some Mutual Relations of Gunnery and Tactics", USNI Proceedings, June 1922. https://www.usni.org/magazines/proceedings/1922/june/some-mutual-relations-gunnery-and-tactics
38. OP 1063, Stable Element Mk 6 (1944), contents. https://eugeneleeslover.com/ENGINEERING/OP1063.html
39. navweaps, 16"/45 Mk 6 range table. http://www.navweaps.com/Weapons/WNUS_16-45_mk6.php

**Not reached:** the dreadnoughtproject wiki pages (Forbes Log, Argo Clock, AFCT, Chernikeeff), which returned 403. Also not reached: Brooks, *Dreadnought Gunnery and the Battle of Jutland*; Friedman, *Naval Firepower*; Chalmers Ch. XI–XVII; BR 1802 (Sperry Mk XIV); OP 1063's text; F.T.P. 89 and 210-2.
