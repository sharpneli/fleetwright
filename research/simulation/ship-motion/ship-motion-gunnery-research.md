# Speed, Roll and the Moving Gun Platform
Status: draft    Updated: 2026-10-12    Request: -

*This report extends [`fire-control-research.md`](../fire-control-research.md) (five miss causes, rate-error floors, `k_optic`, the sea-state laying penalty, laying error by era, `own_turn_kick`), [`manual-gunnery-research.md`](../manual-gunnery-research.md) / [`manual_gunnery_ref.py`](../manual_gunnery_ref.py) (the `Sea` class with free `roll_A` / `roll_P`, roll-firing error = roll rate × timing error, and the continuous-aim gear-rate test), and the roll-period item planned in `fire-control-next-session.md`. Turning, speed loss and heel in turns are covered in [`evasion-research.md`](../evasion-research.md) and [`manoeuvring.md`](../fire-control-research/manoeuvring.md) and are only referred to here. It covers 1890–1990 and splits the "ship motion and speed" question into separate mechanisms, each with a parameterisation the model can use. One existing claim is corrected (§1.1).*


**Raw research appendices** (this folder, full sourcing): [own-ship-speed.md](own-ship-speed.md), [target-relative-speed.md](target-relative-speed.md), [roll-physics.md](roll-physics.md), [stabilisation-tech.md](stabilisation-tech.md), [heavy-weather-cases.md](heavy-weather-cases.md). §7 correlates the findings with this repo's code and plans.

Tags: **[S]** = sourced, with the URL beside it or in the source list; **[INFERRED]** = my derivation, arithmetic or tuning value; **[UNCERTAIN]** = thin, forum-level or conflicting sources. Angles in mil are milliradians (1 mil ≈ 3.44 arcmin). Roll period T is the full cycle. Roll amplitudes are single amplitudes (one side of upright) unless stated otherwise.

---

## 0. TL;DR for the model

1. **Own speed hurts gunnery mainly through spray and smoke, not vibration.** The documented failures at 25–30 kn are turret rangefinders blinded by spray (Denmark Strait, Bismarck, Iowa), forward turrets swamped (Lofoten: Renown had to slow to 20 kn), and funnel smoke over directors in coal ships (Falklands, Dogger Bank, Lion). Vibration was a real rangefinder error source **[S]**, but no open source puts numbers on it. Model it as a modest speed² term on optics mounted on tall, light supports **[INFERRED]**.
2. **Correction:** the existing statement that at the Falklands "vibration and mast shake made a stereo rangefinder useless at high speed" is not supported. Invincible's report blames mast shake on **A-turret blast plus a damaged foremast strut**. Sturdee blamed **long range, enemy spray, short shots and funnel smoke**. The British instruments were coincidence types, not stereo. See §1.1.
3. **Own speed in the ballistic solution is negligible** once a log feeds the computer automatically: a 0.5 kn log error over 30 s time of flight is about 3 yd ballistically and about 8 yd in prediction **[INFERRED]**. The only real own-speed term is in Dumaresq/Dreyer-era systems while own speed or course is changing.
4. **Target speed enters as rate-error variance:** `σ_rate² = floor² + (k·V_target)²`. Course-estimate error converts to a speed-across error of V·Δψ, so the same 10–20° misjudgement costs twice as much against a 30 kn target as against a 15 kn one **[S geometry / INFERRED form]**. Floors and k values are calibrated to the existing settled values at 20 kn (§2).
5. **Computer and gear limits:** the Dreyer range clock pins at ±1,200 yd/min (≈35.5 kn of range rate). Dreyer Mk III bearing gear pins at ±15°/min, which binds only inside about 5,000 yd. The Mk 1A auto-rate needs targets of at least 15 kn. Constant-rate clocks accumulate drift ≈ ½·(V_cross²/R)·t² **[S limits / INFERRED drift]**.
6. **Roll period:** `T = 2π·κ·B/√(g·GM)` with **κ ≈ 0.34 for stiff ships, rising to ≈ 0.38 for tender ones**. Back-calculated warship κ values are 0.30–0.39 (Nelson, Belknap, Knox, RN frigate) **[S data / INFERRED fit]**. British pre-dreadnoughts come out at **15–18 s, not 8–14 s**; stiff German ships at 12–14 s; destroyers at 8–10 s.
7. **Roll amplitude** is a lightly damped resonance with the encounter wave period. Use a table of amplitude by sea state, period ratio and damping (§3.2). Bilge keels ×0.5 at resonance; Frahm tanks ×0.67; gyros ×0.2–0.3; Denny-Brown fins ×0.2 at ≥18 kn, ×0.35 at 12 kn, ×1.0 below 6 kn **[S]**.
8. **Laying error from motion:** `σ_lay = √(base² + (τ·ω_peak·1000)²)` mil, with ω_peak = 2π·A/T in rad/s. τ ≈ 0.3 s for an untrained pointer, 0.1 s for a trained director layer **[S, USNI 1900]**, 0.02–0.05 s for gyro or selected-level contacts, ≈ 0.02 s equivalent for USN stable-vertical continuous fire, and ≈ 0.01 s for post-war rate-gyro systems **[INFERRED]**. The flat `×(1 + 0.25·(SS − 3))` is too shallow for human-timed fire and too steep for post-war systems.
9. **Uncorrected cross-level is a deflection error** of ≈ atan(sin Z·tan E). At 15° elevation, 2° of uncorrected cross-level gives about 9 mil and a steady 8° turning heel about 37 mil **[S geometry / INFERRED numbers]**. Roll becomes cross-level for targets near the bow or stern; pitch becomes cross-level for targets abeam.
10. **Selected-level fire costs rate of fire, not accuracy:** at most one firing opportunity per roll cycle. For a 30 s reload and 14 s period the mean interval is about 37 s. For a 4 s secondary reload and 9 s period it is about 8.5 s, so the rate is halved **[INFERRED]**.
11. **Turret limits:** Bismarck's turret alignment held to about ±3–4° of roll and broke down at about 6°. Nelson's training was limited to 2°/s above 5° of roll **[S]**.
12. **Historical heavy-weather lesson:** heading relative to the sea mattered more than sea state alone. The side that kept its weapons dry out-shot the side steaming into it (Coronel, Lofoten, Denmark Strait, North Cape) **[S cases / INFERRED pattern]**. A steady firing ship against a slow target still hit about 11–12% in force 8 (Bismarck's end) **[UNCERTAIN counts]**.

---

## 1. Own-ship speed

### 1.1 Vibration and mast shake

- German post-WWI assessment (Lienau) lists among the *primary* causes of poor rangefinder results the "permanent rhythmic oscillation … created by the propulsion machinery, mainly the gearsets". Other primary causes were one-sided solar heating and funnel smoke and exhaust. No vibration-specific fix was found; "internal correction" from 1918 was "workable" but "not really satisfactory" **[S]** ([navweaps tech-078](https://navweaps.com/index_tech/tech-078.php)).
- RN WWI: the director's Barr & Stroud rangefinder "significantly deteriorated with a ship's normal vibration" **[S]** ([Jutland 1916](https://www.jutland1916.com/tactics-and-technologies-4/range-finding-and-course-plotting-2)).
- Vibration was local and concentrated aft. On Iowa at full power it was felt "only aft of frame 166"; one after compartment was "like standing on a jackhammer" **[S]** ([navweaps tech-054](https://navweaps.com/index_tech/tech-054.php)). Above 30 kn, cavitation was "the single-most important consideration" **[UNCERTAIN, forum quote]** ([Axis History Forum](https://forum.axishistory.com/viewtopic.php?p=2325126)).
- Mast stiffness was a recurring fix, though the motive was the weight of fire-control equipment rather than speed. KGV (1911) pole masts were reinforced in 1913–14 **[S]** ([Wikipedia KGV 1911](https://en.wikipedia.org/wiki/King_George_V-class_battleship_(1911))). Lion went back to a tripod after 1916 **[S]** ([Wikipedia Lion class](https://en.wikipedia.org/wiki/Lion-class_battlecruiser)). Courageous suffered structural damage at full speed into a head sea **[S]** ([Wikipedia Glorious](https://en.wikipedia.org/wiki/HMS_Glorious)).
- No open source gives vibration amplitudes, frequencies, or a speed above which rangefinding failed. The DTMB reports that would contain them (AD0428719; Jasper 1950 on the DD692 class) were not readable.

**Correction to [`fire-control-research.md`](../fire-control-research.md).** The Falklands claim is not supported:
- Invincible's gunnery report says the mast "shook noticeably" when **A turret fired**. It attributes this mainly to **damage to the starboard foremast strut**. The fore-top rangefinder was "found badly out shortly after firing began" **[UNCERTAIN, forum transcription of the primary report]** ([tapatalk](https://www.tapatalk.com/groups/alltheworldsbattlecruisers/viewtopic.php?p=23270)).
- Sturdee's telegram: "Range finders found little use due to long range, spray from enemy, short shots, and funnel smoke." It does not mention vibration **[S]** ([Dreadnought Project](https://dreadnoughtproject.org/tfs/index.php/H.M.S._Invincible_at_the_Battle_of_the_Falkland_Islands)).
- Speed was 22–27 kn and the sea was "placid" **[S]** (ibid.; [Wikipedia Falklands](https://en.wikipedia.org/wiki/Battle_of_the_Falkland_Islands)).
- RN battlecruisers of 1914 carried coincidence instruments (B&S FQ2, 9 ft), not stereo **[INFERRED from general knowledge, not sourced here]**.

Rewrite the line as: *"At the Falklands, own-gun blast plus a damaged mast strut put the fore-top rangefinder out of adjustment; funnel smoke, enemy splash spray and long range made range-taking nearly impossible [S]."*

> **MODEL NOTE — vibration [INFERRED].**
> `k_vib = 1 + a·(v/v_max)²`, applied as a multiplier on rangefinder and optical random error (part of `k_optic`, see §6).
> - a = 0.3 for a battleship tower or turret-mounted instrument; 0.6 for a tripod top; 1.0 for a pole-mast top or light-hull (cruiser, destroyer, Courageous-type) top.
> - Multiply a by 1.3 for instruments in the after third of the hull (the Iowa evidence) and by 1.2 for geared-turbine plants (Lienau).
> - Resulting `k_vib` values:
>
> | v/v_max | a = 0.3 | a = 0.6 | a = 1.0 |
> |---|---|---|---|
> | 0.50 | 1.07 | 1.15 | 1.25 |
> | 0.75 | 1.17 | 1.34 | 1.56 |
> | 0.90 | 1.24 | 1.49 | 1.81 |
> | 1.00 | 1.30 | 1.60 | 2.00 |
>
> Own-gun blast shake is a separate, short transient. Add +50% to rangefinder error for about 5 s after each own salvo from turrets within about 30 m of the instrument. A battle-damage flag on a mast strut makes this permanent.

### 1.2 Spray and green water

This is the best-documented speed mechanism, with one hard threshold:

| Case | Speed / sea | Effect | Source |
|---|---|---|---|
| Denmark Strait, 1941 | 28 kn into the wind, rough sea | Spray drenched PoW's A-turret 42 ft rangefinder and both British B-turret 30 ft rangefinders; ships fell back to the 15 ft director rangefinders | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_Denmark_Strait) **[S]** |
| Bismarck, 1940/41 | High speed | The 10.5 m rangefinder in turret Anton was **removed** for "sea water fogging at high speeds" | [kbismarck](https://kbismarck.com/controltiri.html) **[S]** |
| Iowa, 1950s | — | Turret I rangefinder removed; it "tended to get blinded by spray" | [Naval Gazing](https://www.navalgazing.net/Fire-Control-Part-2) **[S]** |
| Lofoten, 1940 | Storm | Renown **cut to 20 kn to keep her forward turrets in action**; optics washed out; water through blast bags; firing broken off for 20 min. Scharnhorst's forward turrets flooded through the case-ejection ports; Gneisenau's A turret flooded through a shot-away rangefinder hood | [Naval Gazing](https://www.navalgazing.net/Norway-Part-8); [Wikipedia](https://en.wikipedia.org/wiki/Action_off_Lofoten) **[S]** |
| Dunkerque, Nov 1939 | Gale off Iceland | Bow "repeatedly submerged"; slowed to **10 kn** to avoid damage | [Wikipedia](https://en.wikipedia.org/wiki/French_battleship_Dunkerque) **[S]** |
| KGV (1940) class | — | Almost no sheer, "very wet forward" | [Naval Gazing](https://navalgazing.net/The-King-George-V-class) **[S]** |
| Hood | Full speed or heavy sea | Quarterdeck awash | [Wikipedia](https://en.wikipedia.org/wiki/HMS_Hood) **[S]** |
| Iowa, 1988 | Heavy sea | Bow "takes water only occasionally" (prominent sheer) | [USNI 1988](https://www.usni.org/magazines/proceedings/1988/april/handling-battleship) **[S]** |
| US pre-dreadnoughts (Great White Fleet) | At speed | Bow casemates "untenable due to wetness"; secondaries "useless at speed" | [liquisearch / Wikipedia](https://www.liquisearch.com/great_white_fleet/effects_on_us_capital_ship_design) **[S]** |
| KGV (1911); Iron Duke | Heavy sea | Hull casemate guns "often unusable"; 6-in ports washed away | [Wikipedia KGV](https://en.wikipedia.org/wiki/King_George_V-class_battleship_(1911)); [Iron Duke](https://en.wikipedia.org/wiki/Iron_Duke-class_battleship) **[S]** |

The physics follows standard seakeeping **[S]** ([Nicholson & Price, IOA](https://www.ioa.org.uk/system/files/publications/K%20NICHOLSON%20WG%20PRICE%20EVALUATION%20OF%20THE%20MOTION%20AND%20SPEED%20CHARACTERISTICS%20OF%20A%20SHIP%20IN%20IRREGULAR%20SEAS.pdf)):
- Deck wetness happens when bow relative motion exceeds freeboard F, with probability P(wet) = exp(−F²/2m₀).
- The usual limit is P(wet) ≈ 0.05.
- Speed into a head sea raises the encounter frequency and the relative bow motion.

> **MODEL NOTE — wetness index [INFERRED, calibrated to the cases above].**
> `W = (H_s / 3.25 m) · h(μ) · f_fb · (0.3 + (V_kn / √L_ft)²)`
> - H_s is significant wave height (3.25 m ≈ mid sea state 5).
> - h(μ) is the heading factor: 1.0 head sea, 0.6 bow (≤ 45° off the bow), 0.2 beam, 0.2 quarter or following. A following sea can still put the bow under (Duke of York at North Cape), but that did not stop her firing.
> - f_fb is the freeboard factor: 1.5 for wet designs (KGV 1940, Dunkerque, Hood aft, pre-1939 Scharnhorst, Renown, early destroyers), 1.0 normal, 0.7 for high sheer or Atlantic bow (Iowa, Scharnhorst after refit, raised-forecastle Fubuki).
> - V/√L is the Taylor speed-length ratio.
>
> Thresholds (forward instruments and mounts only, i.e. forward of the bridge):
>
> | W | Effect |
> |---|---|
> | > 1.5 | Turret and low forward rangefinders blinded: that instrument's `k_spray` → ∞, so it falls back to the next instrument (director RF, shorter base, i.e. 2–2.8× the range error by base ratio) |
> | > 2.5 | Forward turret or mount local sights unusable; forward destroyer mounts fire intermittently (50% duty) |
> | > 3.5 | Forward turrets out of action, with a chance of a lasting flooding failure (e.g. 2%/min); firing interruptions |
>
> Calibration points:
>
> | Case | W | Model result | Historical |
> |---|---|---|---|
> | Renown, 20 kn, H_s 6 m | 2.2 | RF blinded, turrets fight | ✓ |
> | Renown, 28 kn, same sea | 3.6 | Turrets out | ✓ |
> | PoW, 28 kn, SS5 | 2.0 | RF lost, turrets fight | ✓ |
> | Scharnhorst, 30 kn, H_s 6 m | 4.1 | Turrets flooded | ✓ |
> | Spee, 20 kn, H_s 5 m | 1.8 | Crews soaked but fighting | ✓ |
> | Iowa, 28 kn, SS5 | 0.8 | Dry | ✓ |
> | Fletcher, 30 kn, SS5 head | 2.7 | A mount intermittent | Plausible, unsourced |
>
> **Casemates and low mounts.** A gun with sill height z (m above the waterline) is unusable when
> `z < H_s · c_side · c_pos · (1 + 0.5·(V/25)²·h(μ))`
> - c_side = 0.6 on the weather side, 0.35 on the lee side.
> - c_pos = 2.0 for bow casemates, 1.0 amidships.
>
> This puts Coronel-era main-deck casemates (z ≈ 3 m) out on the weather side at about H_s 5 m, US bow casemates out from about SS4–5, and Iron Duke's casemates (z ≈ 4.5 m) intermittent at SS6. Superstructure and turret secondaries are unaffected until their optics are hit by spray.

### 1.3 Pitch, slamming and encounter frequency

- Encounter frequency is ω_e = ω − (V·ω²/g)·cos χ; head seas raise it. Captains slow down to avoid green water and slamming **[S]** ([IOA](https://www.ioa.org.uk/system/files/publications/K%20NICHOLSON%20WG%20PRICE%20EVALUATION%20OF%20THE%20MOTION%20AND%20SPEED%20CHARACTERISTICS%20OF%20A%20SHIP%20IN%20IRREGULAR%20SEAS.pdf)).
- Sustainable head-sea speed **[S]** ([USNI 1983](https://www.usni.org/magazines/proceedings/1983/september/seakeeping)):

  | Ship | Sea state 5 | Sea state 7 |
  |---|---|---|
  | 400 ft frigate | ≈ 20 kn | ≈ 10–17 kn, by size |
  | Destroyer | ≈ 25 kn | ≈ 10–17 kn, by size |
  | Cruiser | ≈ 30 kn | ≈ 10–17 kn, by size |

- Destroyer operability limits: 3° pitch, 8° roll, 0.4 g vertical acceleration at the bridge, 30 wetnesses/h, 20 slams/h **[S]** ([DTIC ADA159804](https://apps.dtic.mil/sti/pdfs/ADA159804.pdf)).
- Hull flexing in a seaway changes turret roller-path parallelism. Pattern size applies only to "that ship, on that day, at that course relative to the waves" **[S]** ([NavPers, Accuracy of Shipboard Gun Fire](https://www.eugeneleeslover.com/USN-GUNS-AND-RANGE-TABLES/ACCURACY-OF-SHIPBOARD-GUN-FIRE.html)).
- Significant pitch from carrier data: 0.9° in SS5 and 2.5° in SS6 for a 300 m carrier **[S]** ([DTIC ADA071890](https://apps.dtic.mil/sti/pdfs/ADA071890.pdf)). There are no sourced destroyer or battleship pitch figures.

> **MODEL NOTE — pitch [INFERRED].**
> Significant pitch amplitude in head or bow seas:
>
> | Ship | SS4 | SS5 | SS6 |
> |---|---|---|---|
> | Destroyer (100–120 m) | 1.5–2.5° | 2.5–4° | 4–6° |
> | Battleship (200–270 m) | 0.4–0.8° | 0.8–1.5° | 1.5–3° |
>
> Scale by |cos χ|^0.5, so pitch is largest head-on.
> - Encounter period T_e = 2π/ω_e. Pitch rate = 2π·A_pitch/T_e.
> - Worked example: a destroyer at 25 kn into SS5 (T_w 9.5 s) has T_e ≈ 5.1 s. A 3° pitch then gives 3.7°/s, the same order as a destroyer's roll rate. At τ = 0.1 s that is 6.5 mil.
> - Feed pitch into the same laying formula as roll (§4). Pitch appears as **level** (range) error for targets ahead or astern and as **cross-level** (deflection) error for targets abeam.
> - Slamming acts as a forced speed cap from the USNI 1983 table, scaled by hull length.

### 1.4 Funnel smoke at high power

At high power, smoke was a first-order WWI degrader, probably larger than vibration:
- Invincible at the Falklands: the fore control and P, Q and X turrets were "seriously obstructed by smoke", so control reverted to A turret and to local control **[S]** ([Dreadnought Project](https://dreadnoughtproject.org/tfs/index.php/H.M.S._Invincible_at_the_Battle_of_the_Falkland_Islands)). The forum transcription adds three local-control spells of 2–3 min each **[UNCERTAIN]**.
- Contemporary notes link smoke explicitly to the "combined use of coal and oil at a speed above the ship's designed one" **[S]** ([Dreadnought Project, Notes on the Control of Fire](https://dreadnoughtproject.org/tfs/index.php/Notes_on_the_Control_of_Fire_at_the_Battle_of_the_Falkland_Islands)).
- Lion (original layout) and KGV (1911): the fore-funnel gases made the spotting top "completely unworkable" at high speed. Swapping the funnel and mast cost £68,170 for Lion and Princess Royal **[S]** ([Wikipedia Lion class](https://en.wikipedia.org/wiki/Lion-class_battlecruiser)).
- Dogger Bank: light forces were stationed so as not to "foul our range with their smoke", and Tiger shifted target when smoke interfered **[S]** ([naval-history.net](https://www.naval-history.net/WW1Battle1501DoggerBank.htm)).
- Jutland, run to the south: British smoke blew down the line of fire **[S]** ([Naval Gazing](https://navalgazing.net/Jutland-Part-2)).
- Physics: forward motion creates low pressure behind the funnel and draws gases down onto the deck; following and cross winds do the same **[S]** ([US2601663A](https://patents.google.com/patent/US2601663A/en)).
- No WWII or later case was found of funnel smoke blinding a director.

> **MODEL NOTE — own smoke [INFERRED].**
> `p_obsc_own = s_fuel · s_power · s_wind · s_layout`, applied per director or top and capped at 0.8.
>
> | Factor | Values |
> |---|---|
> | s_fuel | Coal 0.35; coal + oil spray 0.45; oil, 1910s–20s 0.12; oil with caps and tall funnels, 1930s+ 0.04; diesel or gas turbine 0.02 |
> | s_power (P = power fraction) | 0.3 at P ≤ 0.7, rising linearly to 1.0 at P = 0.9, then ×1.5 above design power ("above designed speed") |
> | s_wind | max(0, cos θ), where θ is the angle between the relative-wind vector (direction it blows toward) and the funnel→instrument vector; add 0.3·max(0, cos φ) for relative wind blowing along the line of fire toward the target |
> | s_layout | 2.0 for a mast directly abaft the fore funnel (Lion/KGV-1911); 1.0 otherwise |
>
> Add smoke from ships ahead in line (Inflexible behind Invincible) at 0.5× their own value. Add smoke from own escorts placed upwind.
> When obscured, a top falls back to turret control: worse laying base, shorter rangefinder.
> A WWI coal battlecruiser at full power with adverse wind gives p ≈ 0.2–0.4 for the fore top.
> Coal ships above 90% power also lose about 10% of rate of fire, because stokers were drawn from the gun crews (USNI 1907, qualitative **[S]**, [USNI 1907](https://www.usni.org/magazines/proceedings/1907/october/value-speed-tactics-and-strategy)).

### 1.5 Own speed in the ballistic solution, and logs

- "Motion of the firing ship after the projectile has left the gun can in no way affect the trajectory" **[S]** ([NavPers Ch.17-C](https://eugeneleeslover.com/USNAVY/CHAPTER-17-C.html)).
  - The shell inherits own velocity: 10 kn equals 16.89 ft/s.
  - Firing ahead adds range, astern subtracts, and the beam component deflects in the direction of motion.
  - The worked example at 18 kn, bearing 300°, gives "add 368 yd, right 26 yd" for all gun-motion and wind terms combined.
  - The ship-made apparent wind partly cancels these terms and must not be double-counted.
- From the WWII era own speed is fed automatically. The USN rangekeeper takes it "automatically from the pitometer log" **[S]** ([NavPers Ch.19-F](https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html)), and "Iowa's own movement is fully compensated for" **[S]** ([Naval Gazing](https://www.navalgazing.net/Fire-Control-Part-2)).
- The RN Dreyer took own speed "from Forbes log … manually fed to Dumaresq" **[S]** ([Dreyer Handbook 1918](https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php)). The Dumaresq "could use only a number of pre-set speeds" and Dreyer Mk I was "incapable of adapting to rapid speed changes" **[S]** ([Jutland 1916](https://www.jutland1916.com/tactics-and-technologies-4/range-finding-and-course-plotting-2)).
- Pitometer logs are inaccurate at low speed **[S]** ([Wikipedia Pitometer log](https://en.wikipedia.org/wiki/Pitometer_log)). No sourced service log error was found.

> **MODEL NOTE — own speed in the solution [INFERRED].**
> - Steady speed: ignore (a log error of about 1–2% of speed gives under 10 yd).
> - While own speed is changing or the ship is turning, for systems with manual own-speed input (Dumaresq, Dreyer Mk I–III, Vickers clock, Japanese follow-the-pointer):
>   `rate_err_extra (kn) = 0.3·|dV/dt| (kn/min) + 0.05·|turn rate| (°/min)`
>   This decays with a 1 min time constant after the change ends.
> - For automatic-log systems (Ford Mk 1 onward is partial; Mk 8, AFCT, C/38, post-war) use 10% of that.
> - This supplements, not replaces, `own_turn_kick`.

---

## 2. Target speed and relative speed

### 2.1 Rate error scales with target speed

- Range and deflection corrections are target velocity components × time of flight. USN range-table columns 15 and 18 scale linearly per 10 kn: for example 124 yd per 10 kn at 10,000 yd **[S]** ([Range Tables](https://www.eugeneleeslover.com/USN-GUNS-AND-RANGE-TABLES/USN-RANGE-TABLES-COLUMN-15-AND-18.php)).
- Deflection is Dt = 3,438·RdBs·Tf/R (arcmin) **[S]** ([NavPers Ch.19-F](https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html)).
- Target course and speed are "the chief cause of an incorrect rangekeeper set-up, and thus the chief source of MPI error". Speed "can at best be only roughly estimated" **[S]** ([NavPers Ch.18-B](https://eugeneleeslover.com/USNAVY/CHAPTER-18-B.html)).
- The only measured estimation errors are for drones. Pennsylvania estimated 86 kn on 163° for a drone actually making 131 kn on 145°, an 18° course error. Averaged after spotting, speed errors were 1–2 kn, with the absolute error nearly double that **[S]** ([INRO BB Gunnery](https://navweaps.com/index_inro/INRO_BB-Gunnery.php)).
- Dazzle produced about 7% perceived-speed error in lab tests. A "horizon effect" makes a ship angled 25° toward or away appear to run along the horizon **[S]** ([Wikipedia Dazzle](https://en.wikipedia.org/wiki/Dazzle_camouflage)).

A course error Δψ turns into a speed-across error ΔV ≈ V·Δψ **[INFERRED]**:

| Target speed | Δψ = 10° | Δψ = 20° |
|---|---|---|
| 15 kn | 2.6 kn | 5.2 kn |
| 20 kn | 3.5 kn | 7.0 kn |
| 30 kn | 5.2 kn | 10.5 kn |

At 10,000 yd, 20° misjudged on a 30 kn destroyer gives about 130 yd of prediction error. On a 15 kn battleship the same misjudgement gives about 65 yd.

> **MODEL NOTE — rate error vs target speed [INFERRED].**
> **Initial (unsettled) estimate:** σ_along = 1 + 0.07·V kn; σ_cross = V·σ_ψ, with σ_ψ = 15° by eye (×1.5 within 25° of bow-on or stern-on; ×1.3 for dazzle, pre-radar).
>
> **Settled:** `σ_rate = √(floor² + (k·V)²)`, with floor and k chosen so the value at V = 20 kn matches the existing settled numbers:
>
> | System | floor | k | V = 0 | 10 kn | 20 kn | 30 kn | 35 kn |
> |---|---|---|---|---|---|---|---|
> | Human plot (existing 5–8) | 5.8 | 0.15 | 5.8 | 6.0 | 6.5 | 7.3 | 7.8 |
> | Mk 8 / AFCT class (existing 1.5) | 1.27 | 0.04 | 1.27 | 1.33 | 1.5 | 1.75 | 1.89 |
> | Auto-track (existing 1.0) | 0.8 | 0.03 | 0.8 | 0.85 | 1.0 | 1.2 | 1.32 |
> | Digital (existing 0.7) | 0.57 | 0.02 | 0.57 | 0.61 | 0.7 | 0.83 | 0.91 |
>
> - Settling: σ(t) = max(σ_settled, σ_init·e^(−t/τ_s)). For the Mk 1A, 1–2 time constants (Tc = 2R/R′) remove 65–85% of the initial error **[S]** ([OP 1064](https://ibiblio.org/hyperwar/USN/ref/Ordnance/FCS-Mark37/index.html)).
> - A target turn of Δψ resets σ_cross to at least 2V·sin(Δψ/2). This connects to [`evasion-research.md`](../evasion-research.md).
> - Resolve rate error into range and line by aspect. A stern chase puts the speed error almost entirely into range, where deflection spotting cannot see it.

### 2.2 Design limits of the computers

| System | Sourced limit | Source |
|---|---|---|
| Dreyer range clocks | ±1,200 yd/min (≈ ±35.5 kn of range rate); output in 25 yd/min steps | [Dreyer Handbook](https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php); [DP Dreyer Table](https://dreadnoughtproject.org/tfs/index.php/Dreyer_Fire_Control_Table) **[S]** |
| Dreyer Mk III bearing gear | ±15°/min, constant, set by hand (range-sensitive from Mk III*) | same **[S]** |
| Vickers clock, Dumaresq | Constant rate only; changing rate risked damage, so changes were made in steps; "worked only when range rate was low and not changing quickly" | [Wikipedia](https://en.wikipedia.org/wiki/Vickers_range_clock); [Naval Gazing](https://navalgazing.net/Rangekeeping-Part-2) **[S]** |
| Argo (Pollen) | Continuous rate update; handled high, variable rates | [Naval Gazing](https://navalgazing.net/Rangekeeping-Part-2) **[S]** |
| Ford Mk 1 (1917) | Own speed by hand; steady target assumed | [Naval History 2015](https://www.usni.org/magazines/naval-history-magazine/2015/october/revolutionary-rangekeeper) **[S]** |
| Mk 1A (Mk 37) | Auto rate for targets of 15–800 kn; manual below 15 kn; surface R′ fixed at 3,000 yd; cross-level stop 17.5° | [NavPers Ch.25-C](https://eugeneleeslover.com/USNAVY/CHAPTER-25-C.html) **[S]** |
| Mk 1 | Target speed stop at 300 kn, later 600 and 1,200 kn | [Wikipedia](https://en.wikipedia.org/wiki/Mark_I_Fire_Control_Computer) **[S]** |
| Last AFCT | The only interwar or WWII computer able to handle a turning target, and only a constant turn | [Warships1 forum citing Friedman](https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=884028) **[UNCERTAIN]** |
| Mk 8, AFCT, Type 92/98, C/38 | No numeric limits found (OP 1064 "Limits of Accurate Computation" was blocked) | — |

> **MODEL NOTE — limits [INFERRED].**
> - **Clock pinning:** if |range rate| > 1,200 yd/min on a Dreyer, generated range drifts at (|Ṙ| − 1,200) yd/min until fed by hand from rangefinder cuts. A head-on 25 + 25 kn closure is about 1,690 yd/min.
> - **Bearing gear:** 15°/min corresponds to a speed across of 77 kn at 10,000 yd, 39 kn at 5,000, 23 kn at 3,000 and 15 kn at 2,000. It binds only in night or destroyer ranges.
> - **Constant-rate clock drift** (Vickers, Dreyer Mk I–III, Dumaresq without continuous update): range acceleration is V_cross²/R, so an unadjusted clock drifts ½·(V_cross²/R)·t². At 40 kn across and 10,000 yd that is about 0.05 yd/s², or about 90 yd after 1 min; it doubles at 5,000 yd. Argo and all USN rangekeepers resolve the geometry and do not drift.
> - **Mk 1A below 15 kn:** manual rate. Use the Mk 8-class floor ×1.5, not the plot-era floor.

### 2.3 Bearing rate at short range

- Angular bearing rate (arcmin/s) = 1,936·V_cross(kn)/R(yd) **[S]** ([NavPers Ch.19-F](https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html)). At 40 kn across this is 0.26°/s at 5,000 yd, 0.65°/s at 2,000 yd and 1.3°/s at 1,000 yd **[INFERRED]**.
- Iowa turrets train at about 4°/s **[S]** ([Wikipedia Iowa armament](https://en.wikipedia.org/wiki/Armament_of_the_Iowa-class_battleship)). That binds only at about 120 kn across at 1,000 yd, i.e. never in practice **[INFERRED]**.
- Mk 37 radar cannot track above 25°/s **[S]** ([OP 1064](https://ibiblio.org/hyperwar/USN/ref/Ordnance/FCS-Mark37/index.html)).
- Pointer-tracked range dispersion "depends largely on how accurately the pointer tracks the target" **[S]** (ibid.). Radical own-ship manoeuvring disrupted director laying, especially in destroyers **[S]** ([CINCPAC Gunnery Bulletin 2-42](https://ibiblio.org/hyperwar/USN/rep/CINCPAC/GunBull/2-42/index.html)).

> **MODEL NOTE [INFERRED, UNCERTAIN].** Add a tracking term `σ_track = 0.7 mil + 0.7 mil per °/s of bearing rate` for hand-trained or follow-the-pointer systems; use half that for RPC systems. A mount whose train rate is below the bearing rate cannot fire. Use about 2°/s for WWI hand or hydraulic turrets (unsourced) and 4°/s for WWII heavy turrets.

### 2.4 Practice versus chase evidence

| Engagement | Situation | Hit rate | Source |
|---|---|---|---|
| USN long-range battle practice, 1932–33 | Towed targets mostly < 10 kn, 27–32 kyd | 3.7–5.4% | [INRO](https://navweaps.com/index_inro/INRO_BB-Gunnery.php) **[S]** |
| Santiago, 1898 | Stern chase, ≤ 6,000 yd | < 3% | [Naval Gazing](https://www.navalgazing.net/Spanish-American-War-Part-8) **[S]** |
| Dogger Bank, 1915 | Chase at up to 27–28.5 kn | British 0.6–1.4%; German 2.2–2.5% | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Dogger_Bank_(1915)) **[S]** |
| Komandorski, 1943 | Calm sea, 25–33 kn retirement, 18–23 kyd | A handful of hits from 3,465 rounds | [ONI narrative](https://ibiblio.org/hyperwar/USN/Aleutians/USN-CN-Aleutians-9.html) **[S]** |
| Truk, 1944 | Iowa vs Nowaki, 30 kn, 37.5 kyd | 0/40 | [Naval Gazing](https://www.navalgazing.net/Iowa-Part-2) **[S]** |
| 2nd Guadalcanal, 1942 | Washington vs Kirishima, 8,400 yd | 9/75 ≈ 12% | [Wikipedia Rangekeeper](https://en.wikipedia.org/wiki/Rangekeeper) **[S]** |

Notes on the table:
- In the Truk action the slow Katori was straddled by all 8 salvos (same source).
- Practice targets were slow for safety. Near-radial high-range-rate tows were barred **[S]** (INRO).
- No source isolates target speed statistically. Range, smoke and evasion are confounded in every chase case.

> **MODEL NOTE [INFERRED].** Do not add an explicit "chase penalty". Let it emerge from rate error ∝ V (§2.1), clock limits and drift (§2.2), zigzag resets, own smoke blowing down range (§1.4), and spray when the pursuer heads into the sea (§1.2). If the emergent chase hit rate comes out above about 2–3% at 15–20 kyd for WWI-era systems, raise σ_ψ.

### 2.5 Speed as a defence

- The doctrine was positional. Fisher's "speed is armour" (1919) and the 1905 Committee on Designs line "Speed is necessary to ensure safety" refer to choosing range and position **[S]** ([Navy General Board](https://www.navygeneralboard.com/admiral-sir-john-fisher-and-the-first-fast-battleships/)).
- The faster fleet controls range change while the slower one must guess it **[S]** ([USNI 1908](https://www.usni.org/magazines/proceedings/1908/january/another-argument-speed-battleship-design)).
- Critics: "the factor which determines the range … is the Clerk of the Weather" and "speed is the most vulnerable of the characteristics". At Jutland, destroyers zigzagging at high speed in smoke and splashes were much harder to hit **[S]** ([USNI 1929](https://www.usni.org/magazines/proceedings/1929/april/speed-battleships)).
- Nowaki escaped with "just enough margin of speed" **[S]** ([Naval Gazing](https://www.navalgazing.net/Iowa-Part-2)).
- No statistical study links ship speed to hits received.

> **MODEL NOTE [INFERRED].** Speed defends through two channels already in the model: the ability to set range and aspect (tactical AI), and the V-scaling of rate error and turn resets. No separate "speed armour" modifier is needed.

---

## 3. Roll physics

### 3.1 Roll period from GM and beam

The formula is `T = 2π·k_eff/√(g·GM)` with k_eff = κ·B, where k_eff includes added inertia. Equivalently, T ≈ C·B/√GM with C = 2.006·κ (SI units) **[S form]** ([Krata et al., IMDC 2024](https://proceedings.open.tudelft.nl/imdc24/article/download/881/893/1240)).

Reference roll factors from that paper **[S]**:

| Reference | Roll factor |
|---|---|
| Class societies (DNV highest) | 0.39–0.45 |
| Lewis (PNA) | 0.36, range 0.29–0.43 |
| IMO / JSRA | C = 0.373 + 0.023·B/d − 0.043·L/100, but r² is only 0.59 |
| Kato constant, navy vs merchant | 0.172 vs 0.125 (navy ships have a relatively larger gyradius) |

Warship data, with κ back-calculated **[S data / INFERRED κ]**:

| Ship | B | GM | Sourced T | κ |
|---|---|---|---|---|
| Nelson (deep load) | 32.3 m | 3.1 m | 11.2–13.6 s ([Wikipedia](https://en.wikipedia.org/wiki/Nelson-class_battleship)) | 0.30–0.37 |
| Belknap DLG-26 | 16.6 m | 1.70 m | 8.86 s est. ([DTIC ADA002233](https://apps.dtic.mil/sti/pdfs/ADA002233.pdf)) | 0.347 |
| Knox DE-1078 | 14.1 m **[UNCERTAIN OCR]** | 1.36 m | 8.39 s est. (ibid.) | 0.346 |
| RN frigate/destroyer | 14 m | 1.5 m | ω_n 0.7 rad/s ≈ 9 s ([IMarEST](https://library.imarest.org/record/8618/files/v32b1p13a.pdf)) | 0.39 |
| Delft reference frigate | 13.1 m | 0.5 m | 18.1 s ([IMDC 2024](https://proceedings.open.tudelft.nl/imdc24/article/download/881/893/1240)) | 0.49 (very low GM) |

> **MODEL NOTE — roll period [INFERRED, from the table above].**
> `κ = 0.34 + 0.04·clamp((0.08 − GM/B)/0.06, 0, 1) + 0.02·topweight_flag`
> `T_roll = 2π·κ·B / √(9.81·GM)`
>
> - Use loading-dependent GM: full, half or light load. Destroyers lose GM as fuel burns (Type 1934: 0.79 m full, 0.6 m half; Spence) **[S]** ([Wikipedia Type 1934](https://en.wikipedia.org/wiki/Type_1934_destroyers)).
> - Apply free-surface correction: DLG-9 lost 16% of GM to free surface **[S]** ([DTIC ADA017655](https://apps.dtic.mil/sti/pdfs/ADA017655.pdf)).
>
> Predicted periods (GM sources: Wikipedia class pages, [navweaps INRO Bismarck](https://navweaps.com/index_inro/INRO_Bismarck.php), [gwpda](http://www.gwpda.org/naval/gmdefn.htm); beams from class pages; arithmetic checked in Python):
>
> | Ship | GM (m) | GM/B | κ | T (s) |
> |---|---|---|---|---|
> | Royal Sovereign (1892) | 1.10 | 0.048 | 0.361 | 15.8 |
> | Dreadnought | 1.71 | 0.068 | 0.348 | 13.3 |
> | Queen Elizabeth | 1.98 | 0.072 | 0.346 | 13.6 |
> | Revenge, unbulged / bulged | 1.04 / 1.60 | 0.039 / 0.051 | 0.368 / 0.359 | 19.5 / 17.8 |
> | Hood (1920) | 1.28 | 0.040 | 0.366 | 20.7 |
> | Kaiser/König, Derfflinger | 2.6 | 0.09 | 0.340 | 12.3 |
> | Nelson | 3.1 | 0.096 | 0.340 | 12.5 (sourced 11.2–13.6 ✓) |
> | KGV (1940) | 2.16 | 0.069 | 0.347 | 14.9 |
> | North Carolina | 2.53 | 0.077 | 0.342 | 14.2 |
> | Iowa | 2.82 | 0.085 | 0.340 | 13.4 |
> | Bismarck | 4.09 | 0.114 | 0.340 | 12.1 |
> | Yamato | 2.99 **[UNCERTAIN; 2.6 elsewhere]** | 0.077 | 0.342 | 15.4 |
> | Vittorio Veneto | 1.61 | 0.049 | 0.361 | 18.8 |
> | Richelieu | 2.83 | 0.086 | 0.340 | 13.4 |
> | Type 1934 destroyer (full load) | 0.79 | 0.070 | 0.347 | 8.8 |
> | Belknap | 1.70 | 0.103 | 0.340 | 8.7 (sourced 8.86 ✓) |
> | Knox | 1.36 | 0.097 | 0.340 | 8.2 (sourced 8.39 ✓) |
>
> **The existing pre-dreadnought assumption of 8–14 s is too short for British ships (GM ≈ 1.0–1.2 m → 15–18 s).** It is about right for stiff German ships.
> The Royal Sovereign check works: Hood (1891), with GM 4.1 ft against 3.6 ft, should have a period √(3.6/4.1) = 0.937, i.e. 6% shorter. The source says "around 7%" shorter and "gunnery less accurate" **[S]** ([Wikipedia RS class](https://en.wikipedia.org/wiki/Royal_Sovereign-class_battleship)).

### 3.2 Roll amplitude: sea state, resonance, heading

- Heaviest rolling occurs when T_ship/T_wave = 0.9–1.1. Damping per swing follows Froude's θ₀ − θ₁ = b·θ₀². Waves under 400 ft "hardly affect" a ship of 60 ft beam or more. Rolls of 20–35° make serving guns "quite a problem" **[S]** ([USNI 1931](https://cdn02.usni.org/magazines/proceedings/1931/november/why-they-roll)).
- Nassau's period "coincided with that of the average North Sea swell": severe rolling despite large GM **[S]** ([Wikipedia Nassau](https://en.wikipedia.org/wiki/Nassau-class_battleship)).
- Forrestal (CVA-59), significant values at worst heading, 20–25 kn **[S]** ([DTIC ADA071890](https://apps.dtic.mil/sti/pdfs/ADA071890.pdf)):

  | Sea state | SS2 | SS3 | SS4 | SS5 | SS6 |
  |---|---|---|---|---|---|
  | Roll | 0.7° | 2.7° | 4.8° | 8.2° | 17.2° |

  In SS6, roll velocity was 5.3°/s. The worst roll heading was quartering.
- RN frigate: about ±12.5° unstabilised in about SS5 **[S]** ([IMarEST](https://library.imarest.org/record/8618/files/v32b1p13a.pdf)). US design criterion: 5° significant in SS5 **[S]** ([DTIC ADA017655](https://apps.dtic.mil/sti/pdfs/ADA017655.pdf)).
- Extremes: Collingwood rolled 20° each way in a beam swell **[S]** ([Gutenberg, British Battle Fleet](https://gutenberg.org/cache/epub/75617/pg75617-images.html)). Flower corvettes rolled 40° each side "fairly commonly" **[S]** ([Wikipedia](https://en.wikipedia.org/wiki/Flower-class_corvette)). Typhoon Cobra destroyers rolled 70–75° **[S]** ([Wikipedia](https://en.wikipedia.org/wiki/Typhoon_Cobra)).

> **MODEL NOTE — roll amplitude [INFERRED].**
> I ran a linear one-degree-of-freedom roll model driven by wave slope over a Bretschneider spectrum, using these sea-state means:
>
> | Sea state | H_s | T_p |
> |---|---|---|
> | SS3 | 0.9 m | 6 s |
> | SS4 | 1.9 m | 7.5 s |
> | SS5 | 3.25 m | 9.5 s |
> | SS6 | 5 m | 11.5 s |
> | SS7 | 7.5 m | 14 s |
>
> Excitation coefficient 0.85 (beam sea, zero speed). It reproduces the RN frigate's ±12° in SS5 at ζ = 0.10.
>
> **Significant roll single amplitude (°), beam sea, by r = T_roll / T_p:**
>
> | r | ζ = 0.04 (no keels) SS3/4/5/6/7 | ζ = 0.10 (bilge keels) | ζ = 0.25 (fins at speed, ≈) |
> |---|---|---|---|
> | 0.6 | 21 / 29 / 31 / 33 / 33 | 13 / 18 / 19 / 20 / 20 | 7 / 10 / 11 / 12 / 12 |
> | 0.8 | 17 / 24 / 26 / 27 / 28 | 10 / 14 / 15 / 16 / 16 | 6 / 8 / 8 / 9 / 9 |
> | 1.0 | 12 / 17 / 18 / 19 / 19 | 7 / 10 / 11 / 11 / 11 | 4 / 6 / 6 / 6 / 6 |
> | 1.2 | 7 / 9 / 10 / 10 / 11 | 4 / 6 / 6 / 7 / 7 | 3 / 4 / 4 / 4 / 4 |
> | 1.4 | 3 / 4 / 5 / 5 / 5 | 2.4 / 3.3 / 3.6 / 3.8 / 3.8 | 1.8 / 2.5 / 2.7 / 2.9 / 2.9 |
> | 1.6 | 1.6 / 2.1 / 2.3 / 2.4 / 2.5 | 1.5 / 2.0 / 2.2 / 2.3 / 2.3 | 1.3 / 1.8 / 1.9 / 2.0 / 2.0 |
> | 2.0 | ≈ 1 everywhere | ≈ 1 | ≈ 1 |
> | 2.5 | ≈ 0.6 | ≈ 0.6 | ≈ 0.6 |
>
> Read the r < 1 rows as "short-period ship in long waves". The response there is effectively the wave slope, and big amplitudes arise only for small ships.
>
> Heading and speed:
> - Replace T_p by the encounter period T_e = T_p / |1 − (ω_p·U·cos μ)/g|, where μ = 0 for a following sea and 180° for a head sea.
> - Multiply by |sin μ|.
> - For oblique headings, multiply by 0.6 for short-crestedness. This brings the model's quartering-sea maxima down to the Forrestal values.
> - Quartering seas stretch T_e toward long roll periods. This is why big, long-period ships roll most at about 45° off the stern (Forrestal; Bismarck's roll–yaw coupling in following seas **[S]**, [navweaps INRO](https://navweaps.com/index_inro/INRO_Bismarck.php)).
> - Head and bow seas cut roll to about 0.2–0.3× beam values.
>
> Device multipliers on resonant amplitude (§3.3) are applied after the table, or folded in as ζ.
> Maximum roll ≈ 1.9 × significant (1 in 1,000 waves).
> `Sea.roll_A = significant amplitude`; `Sea.roll_P = T_roll` (the ship rolls at its natural period near resonance and at T_e far from it; use T_roll when 0.7 < r < 1.4, otherwise T_e).
>
> **Worked: significant roll / peak roll rate (°/s), beam sea, zero speed:**
>
> | Ship (T) | SS4 | SS5 | SS6 | SS7 |
> |---|---|---|---|---|
> | Royal Sovereign, no keels (15.8 s) | 1° / 0.4 | 2° / 0.8 | 5° / 2.1 | 13° / 5.3 |
> | Royal Sovereign, keels | 1° / 0.4 | 2° / 0.8 | 4° / 1.6 | 8° / 3.3 |
> | Dreadnought (13.3 s) | 1° / 0.7 | 4° / 1.7 | 8° / 3.6 | 13° / 6.0 |
> | Hood 1920 (20.7 s) | 0° / 0.1 | 1° / 0.3 | 2° / 0.5 | 3° / 0.9 |
> | Bismarck (12.1 s) | 2° / 1.0 | 5° / 2.7 | 10° / 5.2 | 15° / 7.7 |
> | Iowa (13.4 s) | 1° / 0.7 | 3° / 1.6 | 7° / 3.5 | 12° / 5.8 |
> | Type 1934 destroyer (8.8 s) | 6° / 4.6 | 12° / 8.9 | 17° / 12.0 | 20° / 14.1 |
> | Escort with fins, ≥ 18 kn (8.8 s) | 1° / 0.9 | 2° / 1.8 | 3° / 2.4 | 4° / 2.8 |
>
> These match the qualitative record:
> - Hood (1920) as "a steady gun platform" **[S]** ([Wikipedia Hood](https://en.wikipedia.org/wiki/HMS_Hood)).
> - Bismarck as "lively" and "jerky" **[S]** ([navweaps INRO](https://navweaps.com/index_inro/INRO_Bismarck.php); [Navy General Board](https://www.navygeneralboard.com/?p=4021)).
> - The USN 8° destroyer roll limit is reached at about SS5 **[S]** ([DTIC ADA159804](https://apps.dtic.mil/sti/pdfs/ADA159804.pdf)).
> - Battleship amplitudes in long North Atlantic swell (SS7 with T_p ≥ 14 s) climb into the 10–15° band, which is where Bismarck's turret alignment fails (§4.4).

### 3.3 Damping devices

| Device | Era | Sourced effect | Model multiplier on resonant roll | Cost |
|---|---|---|---|---|
| Bilge keels | Froude 1870s; universal after Royal Sovereign 1894–95 | Perseus 23° vs Greyhound 11½° (≈ half); Serapis −10° each way; ≤ 2.5% resistance ([USNI 1885](https://www.usni.org/magazines/proceedings/1885/january/notes-bilge-keels)). Royal Sovereigns "completely cured" ([Gutenberg](https://gutenberg.org/cache/epub/75617/pg75617-images.html)). Captain-class DEs: deeper keels plus lower GM almost halved maximum roll ([IMarEST Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf)) **[S]** | ×0.5 (ζ 0.04 → 0.10) | 1–2% speed **[INFERRED]** |
| Passive tanks (Inflexible 1881; Frahm) | 1881; 1909–14 | Inflexible "useless" ([Wikipedia](https://en.wikipedia.org/wiki/HMS_Inflexible_(1876))); Von der Tann −33%, removed for 180 t of coal ([Wikipedia](https://en.wikipedia.org/wiki/SMS_Von_der_Tann)); Derfflinger "inconclusive" ([Wikipedia](https://en.wikipedia.org/wiki/Derfflinger-class_battlecruiser)) **[S]** | ×0.67 near tuned period, ×1.0–1.2 off-tune | Hundreds of tons |
| Controlled passive tanks | 1950s+ | Frigate about 25° → 9° out-to-out **[UNCERTAIN OCR]** ([IMarEST](https://library.imarest.org/record/8618/files/v32b1p13a.pdf)) | ×0.35–0.5, any speed | — |
| Gyro (Sperry) | Worden 1915, Henderson 1917, Hosho, Ryujo, Pigafetta | "3° in the roughest seas" ([Wikipedia](https://en.wikipedia.org/wiki/Anti-rolling_gyro)), yet Henderson "can frankly be called a failure", about 1.5° quench realistically ([USNI 1923](https://www.usni.org/magazines/proceedings/1923/september/ship-stabilization)) **[S/UNCERTAIN]** | ×0.3, saturating above about SS6 | 3–5% of displacement |
| Denny-Brown fins | Bittern 1936–38; > 100 RN ships by 1950 | Frigate from 25° out-to-out: < 2° at ≥ 18 kn, 4–5° at 12 kn, nil below 6 kn ([IMarEST](https://library.imarest.org/record/8618/files/v32b1p13a.pdf)) **[S]** | ×0.2 at ≥ 18 kn, ×0.35 at 12 kn, ×1.0 below 6 kn (interpolate) | — |
| Modern fins | 1956+ (Gyatt, Bronstein; Leander, County) | 64% average reduction in beam seas, 17-ship study ([DTIC ADA159804](https://apps.dtic.mil/sti/pdfs/ADA159804.pdf)) **[S]** | ×0.36 averaged over headings | — |
| Active stabilisers, Type 1934 | 1930s | Replaced by bilge keels in 1938–39 ([Wikipedia](https://en.wikipedia.org/wiki/Type_1934_destroyers)) **[S]** | Treat as unreliable: 30% failure chance | — |

The RN did not fin-stabilise warships fleet-wide after WWII because tachometric fire control with rate gyros already stabilised the line of sight **[S]** ([IMarEST](https://library.imarest.org/record/8618/files/v32b1p13a.pdf)). In game terms, ship stabilisation and fire-control stabilisation are substitutes for gunnery, but only fins help the crew, loading and spray.

### 3.4 Stiff versus tender

Navies chose differently on GM:

| Navy / case | GM choice | Stated reasoning | Source |
|---|---|---|---|
| RN | Low (about 1–2 m) | Long, gentle roll for "superior gunnery"; excessive stability gives rapid rolling that upsets fire control | [Navy General Board](https://www.navygeneralboard.com/?p=4021) **[S]** |
| Revenge | 3.4 ft, later "too low" | When bulged to 5.1–5.5 ft, the ships reported "excessive rolling" | [Wikipedia](https://en.wikipedia.org/wiki/Revenge-class_battleship) **[S]** |
| Germany | High (2.3–4.1 m) | Damage resistance | [gwpda](http://www.gwpda.org/naval/gmdefn.htm) **[S]** |
| Derfflinger design | 2.7–2.9 m rejected | "the fast roll period would have degraded the gunners' ability to aim" | [Wikipedia](https://en.wikipedia.org/wiki/Derfflinger-class_battlecruiser) **[S]** |
| Bismarck | Above the 5–10%-of-beam guideline | "Stiff … tend[s] to degrade weapon systems effectiveness"; stabilised directors offset the rapid roll | [navweaps INRO](https://navweaps.com/index_inro/INRO_Bismarck.php); [Navy General Board](https://www.navygeneralboard.com/?p=4021) **[S]** |
| Captain-class DEs | GM deliberately *lowered*, period 8 → 8½ s | Better motion | [IMarEST Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf) **[S]** |
| Japan | Ballast added fleet-wide after Tomozuru (1934) | Fubuki +40 t ballast, −1 kn | [Wikipedia Tomozuru](https://en.wikipedia.org/wiki/Japanese_torpedo_boat_Tomozuru); [Fubuki](https://en.wikipedia.org/wiki/Fubuki-class_destroyer) **[S]** |

> **MODEL NOTE [INFERRED].** The designer's GM slider should show two linked readouts:
> - **Gunnery steadiness** ∝ 1/peak roll rate at the reference sea state, from §3.1–3.2.
> - **Survivability**: list after flooding ≈ atan(M_heel/(Δ·g·GM)), plus capsize margin.
>
> Tags: GM/B < 0.04 → "tender: steady platform, thin damage margin"; 0.04–0.07 → "RN steady"; 0.09–0.11+ → "stiff/lively". A ship whose T_roll falls inside the T_p band of its home waters (North Sea SS5–6, about 9.5–11.5 s) gets a "synchronous roll" warning (the Nassau case).

---

## 4. Ship motion and laying

### 4.1 Trunnion-tilt and cross-level geometry

- Level (deck tilt along the line of sight) adds to elevation. Cross-level tilts the trunnions. The resulting "deflection error … is normally large and in the direction of the tilt; the range error is small". "The only practicable method of correction for trunnion tilt is by mechanical means" **[S]** ([NavPers Ch.19-A](https://eugeneleeslover.com/USNAVY/CHAPTER-19-A.html); [Ch.19-G](https://eugeneleeslover.com/USNAVY/CHAPTER-19-G.html)).
- Trunnion tilt matters more in AA fire because of the higher elevation **[S]** ([Ch.24-C](https://eugeneleeslover.com/USNAVY/CHAPTER-24-C.html)).
- Exact geometry from a tilt-corrector patent: COB = arctan[sin CLG·tan(GE + LG)] **[S]** ([US2892384A](https://patents.google.com/patent/US2892384A/en)).
- Wild-shot threshold: more than 10 mil off in deflection **[S]** ([navsource](https://www.navsource.org/archives/01/57%20Excess%20pages/57t2.htm)).

**Uncorrected train error (mil) ≈ 1000·atan(sin Z·tan E)** **[INFERRED, recomputed]**:

| Cross-level Z | E = 5° | 10° | 15° | 20° | 30° | 45° (AA) |
|---|---|---|---|---|---|---|
| 1° | 1.5 | 3.1 | 4.7 | 6.4 | 10.1 | 17.5 |
| 2° | 3.1 | 6.2 | 9.4 | 12.7 | 20.1 | 34.9 |
| 3° | 4.6 | 9.2 | 14.0 | 19.0 | 30.2 | 52.3 |
| 5° | 7.6 | 15.4 | 23.3 | 31.7 | 50.3 | 86.9 |
| 10° | 15.2 | 30.6 | 46.5 | 63.1 | 99.9 | 171.9 |

The matching elevation error is under 1.5 mil for Z ≤ 5° and E ≤ 20°. A battleship's 30 m beam at 20 km is about 1.5 mil, so 2–3° of uncorrected cross-level at battle elevations is a guaranteed line miss.

> **MODEL NOTE — tilt projection [INFERRED].**
> Let β be the target's relative bearing (0 = ahead). Instantaneous deck angles:
> - level L = roll·sin β + pitch·cos β (into elevation; partly corrected by laying)
> - cross-level Z = roll·cos β − pitch·sin β + heel·cos β
>
> Residual errors:
> - elevation: δE = (1 − c_level)·L_at_fire
> - train: δB = (1 − c_xlevel)·atan(sin Z·tan E)
>
> Correction fractions:
>
> | System | c_level | c_xlevel |
> |---|---|---|
> | Pointer with horizon telescope (pre-1912) | 0.9 | 0.5 |
> | WWI director with cross-level telescope | 0.95 | 0.7 |
> | Gyro-level / selected-level | 0.97 | 0.85 |
> | USN Mk 41 / Mk 6 stable vertical | 0.99 | 0.98 |
> | Post-war rate-gyro LOS | 0.995 | 0.99 |
>
> **Broadside fire in a beam sea turns roll into range error. Fire toward the bow or stern turns it into line error amplified by tan E.**
> A steady turning heel of 8° (WWI German capitals; [`manoeuvring.md`](../fire-control-research/manoeuvring.md)) gives 37 mil at E = 15° for a target dead ahead if cross-level is uncorrected. At c = 0.7 that is about 11 mil. This is a concrete addition to `own_turn_kick` for pre-stable-vertical systems.

### 4.2 Each navy's solution

| Navy / era | Method | Residual problem | Sources |
|---|---|---|---|
| RN to 1912; USN pre-1912 | Scott's continuous aim (small guns, elevation only); big guns could not elevate fast enough | Gear rate vs roll rate (existing 1.1× test) | [Naval Gazing Directors](https://www.navalgazing.net/Directors); [Navy General Board](https://www.navygeneralboard.com/?p=3893) **[S]** |
| RN 1912 to about 1940 (main battery) | Director firing *on the roll*: layer keys the salvo when the wire passes the target at a set roll point; mid-roll worked better than end of roll; trainer handles yaw; "reduced the rate of fire". Post-Jutland gyro firing gear fires only at neutral roll. Pollen's yaw gyro (1905–06) | Layer timing; cross-level by telescope | [Naval Gazing Directors](https://www.navalgazing.net/Directors); [Jutland 1916](https://www.jutland1916.com/tactics-and-technologies-4/range-finding-and-course-plotting-2); [Wikipedia FCS](https://en.wikipedia.org/wiki/Fire-control_system) **[S]** |
| RN AA and WWII | HACS roll corrector (most HACS directors unstabilised); Gyro Rate Unit and Pom-Pom director 1940; FKC "Gyro Level Corrector" (Haida). No RPC for main guns until late in the war | Main-battery gyro-firing introduction date unknown | [navweaps tech-111](https://www.navweaps.com/index_tech/tech-111.php); [jproc Haida](https://www.jproc.ca/haida/fcs_1943_1949.html); [navweaps tech-052](https://navweaps.com/index_tech/tech-052.php) **[S]** |
| USN 1912–18 | Directors level the guns only; buzzer at the top of the roll; fire on the down-roll when the wires come on | As RN | [Naval Gazing Directors](https://www.navalgazing.net/Directors) **[S]** |
| USN WWII main battery | Stable Vertical Mk 41 in Plot feeds level and cross-level to the Mk 8. **Continuous fire** at any point of roll, tilt-corrected; **selected-level fire** in heavy seas, with contacts at a preset level (usually mid-roll) and a firing-delay compensator. Continuous level stabilises rangefinders and radar aloft. RPC in train and elevation | Must be "At the Vertical" (about 5 min, 30–60 min spin-up); gear backlash shifts with roll | [NavPers Ch.20-E](https://eugeneleeslover.com/USNAVY/CHAPTER-20-E.html); [navweaps tech-074](https://www.navweaps.com/index_tech/tech-074.php) **[S]** |
| USN WWII DP (Mk 37) | Stable Element Mk 6 stabilises sights, rangefinder and radar; erects in about 1 min; cross-level stop at 17.5°; stabilising angles "computed approximately"; selected-level and "throughout the roll" modes | Approximation; stop at 17.5° | [NavPers Ch.25-A](https://eugeneleeslover.com/USNAVY/CHAPTER-25-A.html); [Wikipedia Ship gun FCS](https://en.wikipedia.org/wiki/Ship_gun_fire-control_system) **[S]** |
| Germany WWII | Triaxially stabilised SL AA directors (21–46 t, fragile, judged a failure); gyro-stabilised rangefinders; gun RPC in elevation only | Turret alignment fails at 4–6° of roll (§4.4) | [Wikipedia SL](https://en.wikipedia.org/wiki/Stabilisierter_Leitstand); [kbismarck trials](https://kbismarck.com/artillery-testing-report.html) **[S]** |
| Japan | Kato's 1904 system fires at a consistent roll point; Type 94 Hoiban computes roll and cross-roll by hand-follow; "lacked a gyro for an artificial horizon"; gyros "neither accurate nor reliable"; no RPC on Yamato | Errors grow with fatigue | [navweaps tech-086](https://navweaps.com/index_tech/tech-086.php); [combinedfleet](http://www.combinedfleet.com/b_fire.htm) **[S]** |
| Post-war (1950s–80s) | Director-mounted vertical and rate gyros (Mk 56); radar autotrack within 0.5 mil; Mk 68 cross-level yoke; OTO 76 drives at 35°/s elevation and 60°/s train | Tracking and servo error | [NavPers Ch.26-E](https://eugeneleeslover.com/USNAVY/CHAPTER-26-E.html); [Wikipedia OTO 76](https://en.wikipedia.org/wiki/OTO_Melara_76_mm) **[S]** |

No data on Italian or French practice was found.

### 4.3 Residual laying error vs roll rate

- USNI 1900 (12-in at 2,000 yd): reaction interval about 0.3 s, giving an uncorrected roll error of "some 63 feet". With a corrected 0.1 s at an average 2°/s roll, the mean vertical error is about 21 ft (= 3.5 mil) for average gunners. Electric firing lag is about 1/50 s **[S]** ([USNI 1900](https://www.usni.org/magazines/proceedings/1900/october/errors-gun-fire-sea)).
- Peak roll rate is 2π·A/T **[INFERRED]**. Timing error (mil) for a range of rolls:

| Roll A / T | ω (°/s) | τ = 0.02 s | 0.05 s | 0.1 s | 0.3 s |
|---|---|---|---|---|---|
| 2° / 15 s | 0.84 | 0.3 | 0.7 | 1.5 | 4.4 |
| 3° / 10 s | 1.88 | 0.7 | 1.6 | 3.3 | 9.9 |
| 5° / 12 s | 2.62 | 0.9 | 2.3 | 4.6 | 13.7 |
| 10° / 14 s | 4.49 | 1.6 | 3.9 | 7.8 | 23.5 |
| 15° / 14 s | 6.73 | 2.3 | 5.9 | 11.7 | 35.2 |
| 8° / 9 s (destroyer) | 5.59 | 1.9 | 4.9 | 9.7 | 29.2 |
| 20° / 9 s (destroyer) | 13.96 | 4.9 | 12.2 | 24.4 | 73.1 |

> **MODEL NOTE — laying error with motion [INFERRED].**
> `σ_lay = √(base² + (τ·ω_eff·1000)²)` mil, where ω_eff = √((ω_roll·p_r)² + (ω_pitch·p_p)²) and p are the projection weights from §4.1. Base values are the existing ones by era.
>
> | System | Base (mil) | τ (s) |
> |---|---|---|
> | Local / individual pointer, untrained (1890s) | 3.0 | 0.3 [S] |
> | Trained pointer / RN–USN director layer by key (1905–1930s) | 1.0 | 0.1 [S] |
> | Gyro firing gear / selected-level contacts with delay compensator | 0.8 | 0.04 |
> | USN Mk 41 continuous fire with RPC | 0.5 | 0.02 |
> | Japanese hand-follow cross-roll, poor gyro | 0.8 | 0.07 (×1.3 after 30 min of action) |
> | German WWII main battery (stabilised RF, elevation-only RPC) | 0.8 | 0.04, plus the turret alignment term (§4.4) |
> | Post-war rate-gyro LOS, fast mounts | 0.35 | 0.01 |
>
> Resulting σ_lay (mil), SS3 / 4 / 5 / 6 / 7. These use representative worst-case beam/quartering amplitudes rather than the zero-speed beam values of §3.2: battleship 1.5 / 3 / 5.5 / 11 / 16°, destroyer 4 / 7 / 11.5 / 16 / 20°.
>
> | System | Battleship, T = 14 s | Destroyer, T = 9 s |
> |---|---|---|
> | Local, 1890s | 4.6 / 7.7 / 13 / 26 / 38 | 15 / 26 / 42 / 59 / 73 |
> | Director by key | 1.5 / 2.6 / 4.4 / 8.7 / 12.6 | 5.0 / 8.6 / 14 / 20 / 24 |
> | Gyro / selected level | 0.9 / 1.2 / 1.9 / 3.5 / 5.1 | 2.1 / 3.5 / 5.7 / 7.8 / 9.8 |
> | USN Mk 41 continuous | 0.6 / 0.7 / 1.0 / 1.8 / 2.6 | 1.1 / 1.8 / 2.8 / 3.9 / 4.9 |
> | Post-war | 0.4 / 0.4 / 0.6 / 0.9 / 1.3 | 0.6 / 0.9 / 1.4 / 2.0 / 2.5 |
>
> The growth is closer to quadratic than linear in sea state for battleships, because amplitude rises steeply from SS5 to SS7. Scott's continuous aim replaces τ with gear lag when the gear rate exceeds 1.1 × peak roll rate (existing test). Otherwise use the director-by-key row. Uncorrected cross-level (§4.1) adds in quadrature on the deflection axis.

### 4.4 Selected-level fire, loading angles and turret limits

- Selected-level fire is used "when sea conditions make it difficult for the guns to stay on target". Contacts close at a preset level, "usually … about the middle of the ship's roll" **[S]** ([NavPers Ch.20-E](https://eugeneleeslover.com/USNAVY/CHAPTER-20-E.html)).
- RN director firing "reduced the rate of fire" **[S]** ([Naval Gazing Directors](https://www.navalgazing.net/Directors)). Before B.VIII mountings the layer "had to wait for the roll to bring the guns to the angle set" **[S]** ([Navy General Board](https://www.navygeneralboard.com/?p=3893)).
- Loading angles **[S]**:
  - Yamato loads at +3°; the cycle is about 30 s at the loading angle and about 35 s at battle elevations ([navweaps 46 cm](https://navweaps.com/Weapons/WNJAP_18-45_t94.php)).
  - Iowa loads at +5° and elevates at 12°/s ([navweaps 16"/50](https://navweaps.com/Weapons/WNUS_16-50_mk7.php)).
  - Early RN barbettes loaded at fixed elevation *and* training ([Navy General Board](https://www.navygeneralboard.com/?p=3893)).
- Bismarck trials (1941) **[S]** ([kbismarck](https://kbismarck.com/artillery-testing-report.html)):
  - Turret alignment upper limit about ±3–4° of roll.
  - At about 4° of roll, alignment errors averaged ±6/16° (≈ 6.5 mil), with outliers of ±10/16° (≈ 11 mil).
  - Realignment was "impossible at about 6°".
  - Rangefinder lateral oscillation of about ±1° could lose the target in a 1.5° field at 40×.
- Nelson class: training limited to 2°/s above 5° of roll **[S]** ([navweaps 16"/45](https://navweaps.com/Weapons/WNBR_16-45_mk1.php)).
- Mk 37 cross-level stop at 17.5° **[S]** ([Ch.25-A](https://eugeneleeslover.com/USNAVY/CHAPTER-25-A.html)).

> **MODEL NOTE [INFERRED].**
> **Selected-level or roll-timed fire.** Firing opportunities come once per roll cycle (two per cycle if both crossings of the selected level are allowed; the sources do not say). The salvo interval is the first opportunity after the reload completes:
>
> | Reload | T_roll | Opportunities per cycle | Mean interval |
> |---|---|---|---|
> | 30 s | 14 s | 1 | 37 s (+23%) |
> | 30 s | 14 s | 2 | 33.5 s |
> | 20 s | 12 s | 1 | 26 s |
> | 4 s (secondary or DP) | 9 s | 1 | 8.5 s (rate halved) |
>
> - Switch to selected-level when roll exceeds about 5° (USN) or always (RN 1912–40, Japan). Continuous-fire systems keep full rate.
> - **Loading angle** adds 2·|E_fire − E_load|/elev_rate per round, before waiting for the roll.
> - **German turrets:** add an alignment error σ = 6.5 mil × (A_roll − 3°) per degree above 3° of roll (6.5 mil at 4°, matching the trial). Turrets drop to local control at 6°.
> - **Nelson-class (and similar hydraulic) train limit:** 2°/s above 5° of roll.
> - **Mk 37 / Mk 1A:** cross-level beyond 17.5° freezes stabilisation, so laying falls back to the human row.
> - **Spray on optics** comes from §1.2, not from roll.

### 4.5 Heel and yaw

Heel in turns is in [`manoeuvring.md`](../fire-control-research/manoeuvring.md). The new results are:
- The trunnion-tilt consequence of heel (§4.1).
- Wind heel. tan φ = P·A·h/(Δ·g·GM) gives about 1° for a Nelson-sized ship and about 5° for a 2,500 t destroyer in a 50 kn beam wind **[INFERRED, recomputed]**. This is a steady cross-level offset for fore-and-aft targets.
- Yaw in a seaway is 1–3° significant (Forrestal 0.6–2.6° across SS3–6; yaw rate 0.73°/s in SS6) **[S]** ([DTIC ADA071890](https://apps.dtic.mil/sti/pdfs/ADA071890.pdf)). The trainer corrected it (RN), Pollen added a yaw gyro **[S]**, and German SL yaw stabilisation was "insufficient for sharp turns" **[S]** ([Wikipedia SL](https://en.wikipedia.org/wiki/Stabilisierter_Leitstand)).

> **MODEL NOTE [INFERRED].** The yaw train error is τ·ω_yaw. A 0.73°/s yaw rate at τ = 0.1 s is about 1.3 mil, so it matters only for hand-trained fire. Use yaw = 1.5× the Forrestal value for destroyers and 2× in following or quartering seas.

---

## 5. Historical heavy-weather cases

| Action | Sea / heading / speed | Mechanism | Rounds → hits | Source |
|---|---|---|---|---|
| 1912 Thunderer vs Orion trial | Seaway, smoke (13 Nov); repeated 4 Dec "in better conditions" | Director above the smoke; local laying in a seaway | 39 → 26 (director) vs 27 → 4 (local); in the repeat Orion "apparently beat" Thunderer | [Naval Gazing Directors](https://www.navalgazing.net/Directors); [Wikipedia Thunderer](https://en.wikipedia.org/wiki/HMS_Thunderer_(1911)) **[S]** |
| Coronel, 1 Nov 1914 | Heavy head sea; Germans at 20 kn; Spee's flagship "athwart the sea and rolling heavily" | British main-deck 6-in casemates closed or useless; spray; low sun | Scharnhorst 422 → ≥ 35 (≈ 8%); German light cruisers > 600 → 5 (< 1%); British hits 2 + 4 | [Spee and Luce reports](https://net.lib.byu.edu/~rdh7/wwi/1914/spee.html); [Massie](https://erenow.org/ww/castles-of-steel/13.php); [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Coronel) **[S]** |
| Falklands, 8 Dec 1914 | Placid sea; British 25.5 kn vs German 22.5 kn | Funnel smoke "practically throughout"; local control; blast-shaken mast (control case for speed without sea) | 1,174 12-in rounds; about 40 hits per armoured cruiser **[UNCERTAIN estimate]** | [Dreadnought Project](https://dreadnoughtproject.org/tfs/index.php/H.M.S._Invincible_at_the_Battle_of_the_Falkland_Islands); [navweaps OOB](https://www.navweaps.com/index_oob/OOB_WWI/OOB_WWI_Falklands.php) |
| Dogger Bank, 24 Jan 1915 | Chase at up to 28.5 kn; spray into light-craft gun shields | Smoke, range, spray | British about 1,150 → 7–16 (0.6–1.4%); German 988 → 22–25 | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Dogger_Bank_(1915)); [BYU letters](https://net.lib.byu.edu/~rdh7/wwi/1915/dogger.html) **[S]** |
| Lofoten, 9 Apr 1940 | Storm; Renown at 20 kn; Germans "straight into the seas" | Forward turrets flooded or washed; optics out; 20 min break | Renown 230 × 15-in → 1 (0.4%) **[UNCERTAIN: 1–3]**; Gneisenau 54 → 2 (3.7%); Scharnhorst 182 → 0 | [Naval Gazing](https://www.navalgazing.net/Norway-Part-8); [Wikipedia](https://en.wikipedia.org/wiki/Action_off_Lofoten) **[S]** |
| Denmark Strait, 24 May 1941 | Rough; British at 28 kn into the wind | Turret rangefinders spray-blinded; shift to 15 ft director RFs | PoW → 3 hits; Bismarck 93 + Prinz Eugen 157 → 7 on PoW (plus Hood) | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_Denmark_Strait) **[S]** |
| Bismarck's end, 27 May 1941 | NW force 8; Bismarck at about 7 kn, crippled; Tovey refused to attack from windward | Bismarck an "unstable gun platform"; Rodney train limit at > 5° of roll; KGV mechanical jams | Rodney 375–380 → about 40 (≈ 11%); KGV 339 → about 40 (≈ 12%) **[UNCERTAIN]**; Bismarck 0 | [kbismarck](https://kbismarck.com/bismarck-last-battle.html); [navweaps tech-016](https://navweaps.com/index_tech/tech-016.php); [Wikipedia](https://en.wikipedia.org/wiki/Last_battle_of_the_battleship_Bismarck) |
| North Cape, 26 Dec 1943 | SW force 7–8; DoY bows "constantly under water" (following sea); cruisers capped at 24 kn | Destroyers could not close; British accepted losing 4–6 kn rather than head into the sea | DoY 52 broadsides (≤ 520 rounds) → 10–13 (≈ 2–2.5%); 31/52 salvos straddled; Scharnhorst 0 effective | [Fraser despatch, Gazette 38038](https://thegazette.co.uk/London/issue/38038/data.pdf); [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_North_Cape) **[S]** |
| Komandorski, 26 Mar 1943 | "Cold and calm" | Calm-sea control: long range and evasion | Salt Lake City 832 → a handful | [HistoryNet](https://historynet.com/battle-of-the-komandorski-islands-close-call-in-the-aleutians-during-world-war-ii/); [ONI](https://ibiblio.org/hyperwar/USN/Aleutians/USN-CN-Aleutians-9.html) **[S]** |

Patterns **[INFERRED]**:
1. Heading to the sea decided which side kept its weapons. At Coronel, Lofoten and Denmark Strait the side heading into it lost casemates, turrets or rangefinders. At North Cape Fraser chose a speed loss instead.
2. A steady firing ship against a slow target hit well even in force 8 (Bismarck's end, about 11–12%).
3. Light craft lost speed and torpedo opportunity rather than gunnery accuracy.
4. The Thunderer repeat shows that the director's advantage is a bad-conditions advantage. The model's director-vs-local gap should widen with sea state and smoke, which the §4.3 table does automatically.

> **MODEL NOTE — validation targets [INFERRED].**
> - Lofoten-type scenario (SS7, head sea, 25+ kn): the model should produce turret outages and main-calibre hit rates of about 0.5–4%.
> - Bismarck's-end scenario (force 8, 1930s RN director, slow target at 3–15 kyd): about 10–12%.
> - North Cape (radar, night, SS6–7, following sea, 12–20 kyd): about 2–3% with about 60% straddles.

---

## 6. Proposed parameter changes to the existing model

| Existing parameter / claim | Change | Basis |
|---|---|---|
| Falklands sentence in [`fire-control-research.md`](../fire-control-research.md) | Replace with the corrected wording in §1.1; tag it [S] (Sturdee) plus [UNCERTAIN] (forum transcription) | §1.1 |
| `k_optic` (single multiplier: 1 / 1.5–2 / 2–3) | Split into `k_optic = k_vis · k_vib(v, mount) · k_spray(W, instrument)` plus a separate `p_obsc_own` (own funnel smoke) and the existing gun-smoke term. k_vis keeps haze; k_vib from §1.1 (≤ 2.0); k_spray = 1 for W < 1.0, 1.5 for 1.0–1.5, then instrument lost (fall back to next RF) for low forward RFs at W > 1.5 | §1.1, §1.2, §1.4 |
| Unstabilised laying × (1 + 0.25·(SS − 3)) | Replace with the explicit σ_lay = √(base² + (τ·ω·1000)²) and per-system τ (§4.3). If a linear form must stay, use slopes per sea state of about 1.0 (human-timed), 0.6 (gyro / selected level), 0.4 (Mk 41 continuous), 0.25 (post-war). These are the fitted worst-heading slopes (1.56 / 0.96 / 0.77 / 0.53 for battleships) cut by about a third to a half, because captains choose heading. Destroyers use the same slopes with a higher base roll | §4.3 |
| Laying error base by era (3 / 1.0 / 0.8 / 0.5 / 0.3–0.4) | Keep as the `base` term; add τ by system | §4.3 |
| `Sea.roll_A`, `Sea.roll_P` (free inputs) | Derive them: roll_P = T_roll from the designer GM, B and κ rule; roll_A from the §3.2 table using r = T_roll/T_e, damping ζ or device multiplier, heading |sin μ| and short-crest factor, plus fin speed dependence | §3.1–3.3 |
| Pre-dreadnought roll period "8–14 s" | British low-GM: 15–18 s (Royal Sovereign 15.8 s). German/French stiff: 12–14 s. Cruisers and small craft: 7–11 s. No bilge keels before 1894–95 (RN) means ζ ≈ 0.04 | §3.1, §3.3 |
| Rate error floors (5–8 / 1.5 / 1 / 0.7 kn) | Make them speed-dependent: σ_rate = √(floor² + (k·V)²) with (5.8, 0.15), (1.27, 0.04), (0.8, 0.03), (0.57, 0.02). Initial σ_cross = V·15°. Turn reset 2V·sin(Δψ/2) | §2.1 |
| (new) Clock limits | Dreyer ±1,200 yd/min pin; Mk III ±15°/min; constant-rate drift ½·(V_cross²/R)·t²; Mk 1A manual below 15 kn | §2.2 |
| `own_turn_kick` | Add (a) uncorrected-cross-level deflection from heel (§4.1) for pre-stable-vertical systems; (b) Dumaresq/Dreyer own-speed and turn rate-error term (§1.5) | §1.5, §4.1 |
| Rate of fire | Selected-level or roll-timed fire caps at one opportunity per roll cycle; loading-angle time; coal-ship stoker penalty above 90% power | §1.4, §4.4 |
| Combat multiplier 1.5 | Unchanged. Weather and speed effects are now explicit, so do not raise it to fit Lofoten-type cases | — |
| Designer | Expose GM by loading condition, free-surface correction, κ, T_roll, the steadiness/survivability readouts, a freeboard/sheer class (f_fb), casemate sill height, mast type (a in k_vib), funnel–mast layout (s_layout), and damping devices by era | §1–§4 |

---

## 7. Recommendations for this codebase

*Checked against the repo on 2026-10-12: `docs/battle/plan.md`, `docs/shipgen/README.md` and `DECISIONS.md`, `docs/designer-ui/secondary.md`, [`../../vfx/ocean-surface-research.md`](../../vfx/ocean-surface-research.md), and `src/Fleetwright.Shipgen` (`Batteries.cs`, `Model/Ship.cs`). Advisory, as all research here: verify against the code before applying.*

### 7.1 What the code already has

| Research needs | Already in the repo | Where |
|---|---|---|
| Roll period from GM and beam (§3.1) | `Stability.RollPeriod(L, B, T, gm)`, the IMO formula `T = 2·C·B/√GM`, `C = 0.373 + 0.023·B/T − 0.043·L/100`, at full-load GM. Stored as `Ship.RollPeriodS`; shown in the designer and on the ship card as "GM · roll" | `src/Fleetwright.Shipgen/Batteries.cs`, `Model/Ship.cs`, `Designer/DesignerScene.cs`, `ShipCard.cs` |
| An explicit hand-off | "`roll_period_s` … A stiff ship rolls fast and snappily, a tender one slowly. **The game decides what that does to gunnery.**" This note is that decision's input | `docs/shipgen/README.md` (topweight and windage) |
| GM at full and light load | `GmFull`, `GmLight`; `hydrostatics.gm_t` exported for flooding | `Navarch.cs`; `docs/shipgen/README.md` (hydrostatics) |
| Heel as part of the pose | Step 4.3 heels a flooded ship from `GM_T`; step 4.1's hit query takes pose with sinkage, trim, heel | `docs/battle/plan.md` |
| Casemate height | Secondary positions (deckhouse, deck, upper, lower casemate) with the gun axis height over the full-load waterline shown (e.g. 4.6 m for a lower casemate); "a lower casemate is low and wet" is already the stated trade-off | `docs/designer-ui/secondary.md` |
| Instrument heights and positions | Every director, rangefinder, turret, mast and funnel has a hitbox with x and height; `tower_levels` trades sight for topweight | hitbox model; `docs/shipgen/README.md` |
| Own smoke as sim state | "Smoke is the big one": puffs and screens with position, age and wind drift, read by sighting; coal vs oil smoke under "Coal" | `docs/battle/plan.md`, first cut's era |
| Shell leaves with own motion | Shell record holds "the elevation and muzzle velocity it actually left with (the ship's own motion, roll and pitch folded in; dispersion is a per-shell perturbation)" | `docs/battle/plan.md`, Shells |
| Sea surface parameters | Waves driven by U10 (Beaufort), fetch, and a separate swell with its own Hs, Tp and direction | `research/vfx/ocean-surface-research.md` §2 |
| Model rates | Fire-control solutions at ~1 Hz; pose every tick | `docs/battle/plan.md`, How many ships |

### 7.2 Roll period: keep shipgen's formula, don't add a second one

The IMO formula shipgen uses is the same family as §3.1 (C ≈ κ in SI units) and agrees with the sourced warship periods about as well as the κ rule does. Computed with each ship's own L, B and draught [INFERRED; dimensions from class pages, draughts approximate]:

| Ship | GM (m) | Shipgen (IMO) C / T | §3.1 κ / T | Sourced T |
|---|---|---|---|---|
| Royal Sovereign (1892) | 1.10 | 0.386 / 16.9 s | 0.361 / 15.8 s | — |
| Dreadnought | 1.71 | 0.368 / 14.1 s | 0.348 / 13.3 s | — |
| Queen Elizabeth | 1.98 | 0.362 / 14.2 s | 0.346 / 13.6 s | — |
| Hood (1920) | 1.28 | 0.335 / 18.8 s | 0.366 / 20.7 s | — |
| König | 2.6 | 0.372 / 13.6 s | 0.340 / 12.3 s | — |
| Nelson | 3.1 | 0.358 / 13.1 s | 0.340 / 12.5 s | 11.2–13.6 s |
| KGV (1940) | 2.16 | 0.350 / 15.0 s | 0.347 / 14.9 s | — |
| Belknap DLG-26 | 1.70 | 0.367 / 9.4 s | 0.340 / 8.7 s | 8.86 s |
| Knox DE-1078 | 1.36 | 0.386 / 9.3 s | 0.340 / 8.2 s | 8.39 s |

- Capital ships: shipgen runs about 4–6% longer than the κ rule, inside the formula's own scatter (r² ≈ 0.59, §3.1). Both put British pre-dreadnoughts at 15–18 s, which is what matters (the old 8–14 s guess in `manual_gunnery_ref.py` is wrong).
- Escorts: shipgen runs 6–13% long against the two measured frigates. Acceptable; if it ever matters, cap C at about 0.37 for L < 180 m.
- **Recommendation:** the gunnery model reads `Ship.RollPeriodS` and nothing else, so the designer's readout and the battle agree. If roll is ever needed at another loading (light load, a flooded ship), call the same `Stability.RollPeriod` with that GM rather than a second formula. A ship that has taken water has a lower GM (step 4.3 already computes it), so she rolls slower and heavier: that falls out for free.

### 7.3 Roll amplitude needs the swell, not just the wind sea

The roll amplitude table (§3.2) is keyed on r = T_roll / T_p. The ocean-surface research's wind-sea table (fetch 150 km) gives short peak periods: about 7.3 s at Beaufort 5, 8.5 s at Bft 7, 9.1 s at Bft 8. Against those [INFERRED, from §3.2]:

| Ship | T_roll | r in Bft 7 wind sea alone | Roll (bilge keels) |
|---|---|---|---|
| Destroyer | 9 s | ≈ 1.06 | near resonance, ≈ 10–11° |
| Battleship | 14 s | ≈ 1.65 | ≈ 2° |
| Same battleship in a 13 s swell | 14 s | ≈ 1.08 | ≈ 10° |

So **small ships roll in the wind sea and big ships roll in the swell.** If the battle's weather carries only Beaufort, battleships will almost never roll and the stiff-vs-tender trade-off disappears.

- **Recommendation for the weather state** (step 3.2 brings wind for smoke; add the sea at the same time): wind (U10, direction), fetch or duration, and a swell (Hs, Tp, direction). That's exactly the ocean shader's input, so the sea that is drawn and the sea the ships roll in are the same numbers. The sim reads them; the look reads them; neither feeds the other (the plan's rule for effects).
- Use encounter period, not wave period: T_e from heading and speed (§3.3). A ship can detune herself from resonance by changing course or speed. That's a real captain's choice the game gets for free.

### 7.4 Designer knobs this implies

The designer already exposes GM, roll period, tower height and casemate tier. Missing, in order of value for the 1890–1925 cut:

1. **Roll damping fit** (one control, era-gated): none / bilge keels (RN from 1894–95; ζ ≈ 0.04 → 0.10, about ×0.5 resonant roll, ≤ 2.5% resistance) / anti-roll tanks (Frahm, German 1909–14; ×0.67, weight and volume, Germany dropped them) / gyro (1917+, ×0.3, 3–5% of displacement, abandoned) / fins (1936+, ×0.2 at ≥ 18 kn, nothing below 6 kn). Default: bilge keels after 1895. Each is a weight and resistance line in the existing ledger.
2. **Freeboard and bow form** for the wetness index's `f_fb` (§1.2): the hull form already knows freeboard and sheer forward; derive `f_fb` from freeboard forward over length rather than a hand class, and show "wet above n kn in sea state 5" next to the bow knobs.
3. **Casemate sill height** is already shown. Add the consequence to its tooltip: "unusable above sea state n on the weather side" from §1.2's casemate rule (c_pos 2.0 bow, 1.0 amidships). A Coronel falls out of the designer's own numbers.
4. **Stabilisation per battery.** DECISIONS says fire control is raw numbers per battery; add one enum per battery: `hand_timed | director_roll | gyro_firing | selected_level | continuous | full_stabilised`, giving τ (§4.3) and whether cross-level is corrected. Plus a turret alignment roll limit (Bismarck: fine to ±3–4°, lost at ~6°; Nelson training limited to 2°/s above 5° roll) as a mount property.
5. **Mast type** (pole, tripod, tower) for the vibration factor `a` (§1.1), and nothing else: the funnel–director geometry is already in the hitboxes (below).

### 7.5 The battle sim

- **A roll phase per ship, not a roll simulation.** Each ship carries amplitude A and period T (from §7.2–7.3, refreshed when course, speed, sea or GM change) and a phase. Roll angle and rate at any instant are one `sin`/`cos`; per-ship random phase from the ship's `RandomStream`, so replays stay exact. Cost is nil at the ~1 Hz fire-control rate. Keep roll out of the hit query (the target's roll changes its hitting space negligibly); heel stays in the pose as now.
- **Firing instants from the stabilisation enum.** `selected_level` and `director_roll` fire only when the phase passes the chosen level: one opportunity per roll cycle (§4.4), so a 30 s reload on a 14 s roll averages ~37 s. The shell record already takes the elevation it actually left with; the laying error σ_lay = √(base² + (τ·ω·1000)²) mil (§4.3) is the common-per-salvo part of that perturbation, and ω comes straight from the phase.
- **Heel feeds deflection.** Uncorrected cross-level gives a train error ≈ atan(sin Z·tan E) (§4.1). The pose already has heel from step 4.3's flooding and from turning: a listing pre-1930s ship shoots wide unless her fire control corrects cross-level. Damage and gunnery connect through a number the sim already computes.
- **Wetness is a state, not a modifier.** Evaluate W (§1.2) per ship at the fire-control rate from Hs, heading to the sea, speed and `f_fb`, and switch forward rangefinders, forward mounts and weather-side casemates on and off by its thresholds, with the existing fall-back to the next instrument. Hysteresis of ~10% avoids flicker.
- **Own smoke by geometry, not by chance.** §1.4 proposes a per-minute obscuration chance; with smoke already sim state, do better: the sighting ray from each director or rangefinder to the target is tested against the ship's own funnel puffs, which the wind drifts. Lion's original fore-top (directly abaft the fore funnel) and a following wind then blind themselves without a special rule, and moving the director in the designer fixes it, as it did historically. Puff emission scales with power fraction and fuel (coal, oil, mixed), which the plan's "Coal" item already wants.
- **Vibration** is the one term with no geometry to lean on: `k_vib = 1 + a·(v/v_max)²` (§1.1) on instruments, ×1.3 aft of the after armoured bulkhead (the hitbox model knows it).
- **Rate error grows with target speed** (§2.1): σ_rate² = floor² + (k·V)². V is the target's true speed in the sim, so this costs nothing and makes fast targets harder without a special "speed armour" rule.
- **Settings.** All coefficients (κ cap, damping multipliers, τ by system, wetness thresholds, `a` by mast) belong in `SimSettings` with the plan's other tunables; none is hard-coded.

### 7.6 Captains (step 5.1)

Two historical behaviours fall out of the wetness and roll models and should be orders or captain habits, not scripts:
- **Slow down to fight forward turrets** (Renown at Lofoten, 28 → 20 kn).
- **Don't head into the sea to close** (Fraser at North Cape accepted a 4–6 kn deficit; Spee at Coronel kept the weather gauge so the British casemates drowned). A captain choosing a closing course weighs W and roll, not just range.

### 7.7 Order for the first cut (1890–1925)

1. Roll phase + `hand_timed` / `director_roll` firing with τ and the one-per-cycle limit; `RollPeriodS` as the period. This is most of the era's motion effect.
2. Weather with wind sea + swell, shared with the ocean shader.
3. Wetness: casemates, forward turrets, turret rangefinders.
4. Own funnel smoke on sighting rays (needs smoke as sim state, which the plan has anyway).
5. Bilge keels / Frahm tanks as a designer fit.
6. Later cuts: stable verticals (`selected_level`, `continuous`), cross-level correction, fins, full stabilisation.

### 7.8 Correction to an existing note

[`../fire-control-research.md`](../fire-control-research.md) §3.5 says vibration and mast shake at the Falklands made a stereo rangefinder useless at high speed. The sources say otherwise (§1.1 above: own-gun blast plus a damaged mast strut; Sturdee blamed range, spray, short shots and funnel smoke; the instruments were coincidence type). The corrected wording is in §1.1. That note is left unedited here.

---

## 8. Gaps and uncertainties

- **Vibration:** no amplitudes or frequencies for any director or rangefinder platform. The DTMB reports AD0428719 and Jasper 1950 were not readable. The `a` coefficients in k_vib are uncalibrated.
- **Spray thresholds:** one hard speed threshold (Renown, 20 kn) and qualitative cases only. No destroyer forward-mount data. The W index and its thresholds are fitted to about six cases.
- **Log errors and computer limits:** no sourced Forbes or pitometer service error. No Mk 8, AFCT, Argo, Type 92/98 or C/38 speed or rate limits (OP 1064 "Limits of Accurate Computation" was blocked).
- **Target-speed estimation:** no surface-target table of course or speed estimation error versus target speed. σ_ψ = 15° rests on one drone case and the dazzle literature. The k coefficients in the rate-error formula are tuned, not measured.
- **Roll periods:** no measured natural periods for named WWI–WWII capital ships other than Nelson; Belknap and Knox are estimates. Yamato's GM (2.6 vs 2.99 m) and Iowa's (2.34 vs 2.82 m) conflict by loading condition. Hood's GM conflicts (1.0 vs 1.3 m).
- **Roll amplitude model:** linear, with no warship-specific damping coefficients. The ζ values are inferred from the halving results. The model over-predicts low-sea-state quartering roll for very long-period ships unless the 0.6 short-crest factor is applied, and it needs calibration against any real WWII roll record.
- **Stabilisation accuracy:** no sourced arc-minute accuracy for Mk 41 or Mk 6 verticals, no firing-contact timing error, and no RN main-battery gyro-firing or cross-levelling introduction dates (Brooks, Friedman and the RN "Directors" handbook were inaccessible). τ values below 0.1 s are inferred.
- **Navies:** nothing on Italian or French practice; German main-battery roll-timed firing is unconfirmed; Japanese data are qualitative.
- **Heavy-weather hit counts:** many are disputed (Lofoten 1–3 hits, Bismarck's end about 40 each, North Cape 10–13). No action gives measured roll angles or hits by roll phase. No navy's numeric sea-state firing limit was found.
- **Heel:** no sourced turning-heel values for British, US or Japanese destroyers; wind heel is computed only.

---

## Sources (main)

**Fire control and gunnery texts**
- NavPers *Naval Ordnance and Gunnery*, Slover transcriptions: [Ch.17-C](https://eugeneleeslover.com/USNAVY/CHAPTER-17-C.html), [Ch.18-B](https://eugeneleeslover.com/USNAVY/CHAPTER-18-B.html), [Ch.19-A](https://eugeneleeslover.com/USNAVY/CHAPTER-19-A.html), [Ch.19-F](https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html), [Ch.19-G](https://eugeneleeslover.com/USNAVY/CHAPTER-19-G.html), [Ch.20-E](https://eugeneleeslover.com/USNAVY/CHAPTER-20-E.html), [Ch.24-C](https://eugeneleeslover.com/USNAVY/CHAPTER-24-C.html), [Ch.25-A](https://eugeneleeslover.com/USNAVY/CHAPTER-25-A.html), [Ch.25-C](https://eugeneleeslover.com/USNAVY/CHAPTER-25-C.html), [Ch.26-E](https://eugeneleeslover.com/USNAVY/CHAPTER-26-E.html)
- USN reference material: [USN Range Tables cols 15 & 18](https://www.eugeneleeslover.com/USN-GUNS-AND-RANGE-TABLES/USN-RANGE-TABLES-COLUMN-15-AND-18.php); [Accuracy of Shipboard Gun Fire](https://www.eugeneleeslover.com/USN-GUNS-AND-RANGE-TABLES/ACCURACY-OF-SHIPBOARD-GUN-FIRE.html); [OP 1064 / FCS Mk 37](https://ibiblio.org/hyperwar/USN/ref/Ordnance/FCS-Mark37/index.html); [CINCPAC Gunnery Bulletin 2-42](https://ibiblio.org/hyperwar/USN/rep/CINCPAC/GunBull/2-42/index.html)
- RN fire control: [Dreyer Handbook 1918](https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php); [Dreadnought Project, Dreyer Table](https://dreadnoughtproject.org/tfs/index.php/Dreyer_Fire_Control_Table); [Jutland 1916, Range Finding](https://www.jutland1916.com/tactics-and-technologies-4/range-finding-and-course-plotting-2)
- Naval Gazing: [Directors](https://www.navalgazing.net/Directors), [Rangekeeping 1](https://navalgazing.net/Rangekeeping-Part-1), [Rangekeeping 2](https://navalgazing.net/Rangekeeping-Part-2), [Fire Control 2](https://www.navalgazing.net/Fire-Control-Part-2), [Norway 8](https://www.navalgazing.net/Norway-Part-8), [Iowa 2](https://www.navalgazing.net/Iowa-Part-2)
- navweaps: [tech-016](https://navweaps.com/index_tech/tech-016.php), [tech-052](https://navweaps.com/index_tech/tech-052.php), [tech-054](https://navweaps.com/index_tech/tech-054.php), [tech-074](https://www.navweaps.com/index_tech/tech-074.php), [tech-078](https://navweaps.com/index_tech/tech-078.php), [tech-086](https://navweaps.com/index_tech/tech-086.php), [tech-111](https://www.navweaps.com/index_tech/tech-111.php), [INRO BB Gunnery](https://navweaps.com/index_inro/INRO_BB-Gunnery.php), [INRO Bismarck](https://navweaps.com/index_inro/INRO_Bismarck.php)
- German fire control: [kbismarck artillery trials](https://kbismarck.com/artillery-testing-report.html); [kbismarck fire control](https://kbismarck.com/controltiri.html); [Wikipedia Stabilisierter Leitstand](https://en.wikipedia.org/wiki/Stabilisierter_Leitstand)
- Patents: [US2892384A tilt corrector](https://patents.google.com/patent/US2892384A/en); [US2601663A funnel gases](https://patents.google.com/patent/US2601663A/en)

**USNI Proceedings**
- [1885 Bilge keels](https://www.usni.org/magazines/proceedings/1885/january/notes-bilge-keels)
- [1900 Errors of gun fire at sea](https://www.usni.org/magazines/proceedings/1900/october/errors-gun-fire-sea)
- [1907 Value of speed](https://www.usni.org/magazines/proceedings/1907/october/value-speed-tactics-and-strategy)
- [1908 Argument for speed](https://www.usni.org/magazines/proceedings/1908/january/another-argument-speed-battleship-design)
- [1923 Ship stabilization](https://www.usni.org/magazines/proceedings/1923/september/ship-stabilization)
- [1929 Speed in battleships](https://www.usni.org/magazines/proceedings/1929/april/speed-battleships)
- [1931 Why they roll](https://cdn02.usni.org/magazines/proceedings/1931/november/why-they-roll)
- [1983 Seakeeping](https://www.usni.org/magazines/proceedings/1983/september/seakeeping)
- [1988 Handling the battleship](https://www.usni.org/magazines/proceedings/1988/april/handling-battleship)

**Seakeeping and naval architecture**
- [Krata et al., IMDC 2024 roll-period methods](https://proceedings.open.tudelft.nl/imdc24/article/download/881/893/1240)
- [IMarEST stabilisers paper](https://library.imarest.org/record/8618/files/v32b1p13a.pdf); [IMarEST, D.K. Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf)
- [DTIC ADA071890 carrier motions](https://apps.dtic.mil/sti/pdfs/ADA071890.pdf); [DTIC ADA159804 frigate/destroyer operability](https://apps.dtic.mil/sti/pdfs/ADA159804.pdf); [DTIC ADA002233](https://apps.dtic.mil/sti/pdfs/ADA002233.pdf); [DTIC ADA017655](https://apps.dtic.mil/sti/pdfs/ADA017655.pdf)
- [Nicholson & Price, IOA](https://www.ioa.org.uk/system/files/publications/K%20NICHOLSON%20WG%20PRICE%20EVALUATION%20OF%20THE%20MOTION%20AND%20SPEED%20CHARACTERISTICS%20OF%20A%20SHIP%20IN%20IRREGULAR%20SEAS.pdf)
- [gwpda GM definitions](http://www.gwpda.org/naval/gmdefn.htm); [Navy General Board, stability](https://www.navygeneralboard.com/?p=4021); [Navy General Board, turret machinery](https://www.navygeneralboard.com/?p=3893); [*The British Battle Fleet*, Gutenberg](https://gutenberg.org/cache/epub/75617/pg75617-images.html)

**Action accounts**
- Falklands: [Dreadnought Project, Invincible at the Falklands](https://dreadnoughtproject.org/tfs/index.php/H.M.S._Invincible_at_the_Battle_of_the_Falkland_Islands); [Notes on the Control of Fire](https://dreadnoughtproject.org/tfs/index.php/Notes_on_the_Control_of_Fire_at_the_Battle_of_the_Falkland_Islands)
- Coronel: [Spee and Luce reports, BYU](https://net.lib.byu.edu/~rdh7/wwi/1914/spee.html); [Massie, *Castles of Steel* ch.13](https://erenow.org/ww/castles-of-steel/13.php)
- North Cape: [Fraser despatch, London Gazette 38038](https://thegazette.co.uk/London/issue/38038/data.pdf)
- Bismarck: [kbismarck last battle](https://kbismarck.com/bismarck-last-battle.html)
- Komandorski: [ONI Combat Narrative, Aleutians](https://ibiblio.org/hyperwar/USN/Aleutians/USN-CN-Aleutians-9.html)
- Wikipedia class and battle pages as cited inline: Royal Sovereign, Revenge, Hood, Nassau, Derfflinger, Bismarck class, Denmark Strait, Action off Lofoten, Dogger Bank, Typhoon Cobra, Stabilizer (ship), Anti-rolling gyro, Type 1934

**Forum transcriptions [UNCERTAIN]**
- [Invincible gunnery report transcription (tapatalk)](https://www.tapatalk.com/groups/alltheworldsbattlecruisers/viewtopic.php?p=23270)
- [Warships1 forum citing Friedman](https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=884028)
