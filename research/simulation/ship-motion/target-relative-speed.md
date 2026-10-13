# Target speed and relative (own + target) speed effects on naval gunfire accuracy, 1900–1980s
Status: draft    Updated: 2026-10-12    Request: -    Appendix to: [ship-motion-gunnery-research.md](ship-motion-gunnery-research.md)

Scope note: I worked within a capped search budget, and the shared web-search limit ran out near the end. Several primary documents could not be opened: the OP 1064 Appendix "Limits of Accurate Computation" (p. 378) was blocked by the egress proxy, and the Mk 8 rangekeeper OP is not online in any form I found. Their numbers are therefore gaps, not findings. Tags used below: [S] = sourced, [INF] = my inference or arithmetic, [UNC] = uncertain or low-reliability source.

---

## 1. Do rate-estimation errors scale with target speed? (course error × speed → speed-across error)

### Takeaway
No source I found gives a measured table of course or speed estimation error against target speed for surface ships. The geometry, however, is sourced. Range and deflection corrections are the target's velocity components multiplied by time of flight, so any course-estimate error turns into a velocity error proportional to target speed: |ΔV| = 2V·sin(Δψ/2) ≈ V·Δψ. Speed-estimate error also plausibly grows with speed, since speed was "only roughly estimated" from bow wave and formation. A fast target therefore has a bigger rate error for the same angular misjudgement, in proportion.

### Cited Findings
- Range rate is the sum of own and target velocity components along the line of sight, dR = Yo + Yt. Linear bearing rate is the sum of the across-line components, RdBs = Xo + Xt, with Xo = So·sin Br and Yo = So·cos Br. Target components are resolved the same way from estimated target speed and target angle. — [NavPers Naval Ordnance & Gunnery Ch. 19-F](https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html)
- Deflection prediction for relative motion: Dt = 3,438 × RdBs × Tf / R2 (arc-minutes; RdBs in yd/s, Tf in s, R2 in yd). It is directly proportional to cross-line relative speed times time of flight. — [Ch. 19-F](https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html)
- 1 knot = 0.563 yd/s, so a ship at 30 kn moves about 16.9 yd/s. — [Ch. 19-F](https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html)
- USN range tables, Columns 15 and 18, give the change of range and the deflection for 10 kn of target motion in, or across, the line of fire. Each equals the 10 kn distance × time of flight, and other speeds are scaled linearly (speed/10). Example at 10,000 yd: 124 yd per 10 kn, so 16 kn in the line of fire gives 198 yd and 13 kn across gives 161 yd. — [USN Range Tables Columns 15 & 18](https://www.eugeneleeslover.com/USN-GUNS-AND-RANGE-TABLES/USN-RANGE-TABLES-COLUMN-15-AND-18.php)
- Target speed "can at best be only roughly estimated". Against a battle line, the manual says to estimate about 1–2 kn below the maximum speed of the slowest ship in the formation, adjusted for damage reports. Target angle is estimated from how the apparent spacing of masts, stacks and bridges opens or closes. — [NavPers Ch. 18-B](https://eugeneleeslover.com/USNAVY/CHAPTER-18-B.html)
- Target course and speed are "the chief cause of an incorrect rangekeeper set-up, and thus the chief source of MPI error." — [NavPers Ch. 18-B](https://eugeneleeslover.com/USNAVY/CHAPTER-18-B.html)
- Target course is "the most difficult piece of target data to obtain", which is why angle on the bow (target angle) was estimated instead. — [Wikipedia, Mathematical discussion of rangekeeping](https://en.wikipedia.org/wiki/Mathematical_discussion_of_rangekeeping)
- Measured estimation errors exist only for aircraft drones. Pennsylvania's director estimated 86 kn on a 163° course for a drone actually making 131 kn on 145°, an 18° course error and a 34% speed error. Averaged over runs, speed estimates after spotting were only 1–2 kn off, but the average absolute error was nearly twice that because over- and under-estimates cancelled. — [INRO, Jurens/Fischer/… "US BB gunnery" fn 54, 58](https://navweaps.com/index_inro/INRO_BB-Gunnery.php)
- Dumaresq-era (RN) target course and speed "had to be guessed", but an experienced observer could estimate them "with a reasonable degree of accuracy" (no figure given). — [Naval Gazing, Rangekeeping Part 1](https://navalgazing.net/Rangekeeping-Part-1)
- 1908: "the best spotter known has an average error of over two hundred yards at 6000 yards". The author argued that the slower fleet, which has to estimate the enemy's course from observation, is the one left guessing at range change. — [USNI Proceedings 1908, "Another argument for speed in battleship design"](https://www.usni.org/magazines/proceedings/1908/january/another-argument-speed-battleship-design)
- Lab (non-naval) work found dazzle patterns caused about a 7% error in perceived speed. Recent Aston University tests found dazzle's "twist" effect on perceived heading "quite small", but a "horizon effect" made a ship angled 25° toward or away appear to run along the horizon. That is a large course-estimation error near bow-on or stern-on aspects. — [Wikipedia, Dazzle camouflage](https://en.wikipedia.org/wiki/Dazzle_camouflage)

### Inferences
- [INF] Speed-across error from course error: ΔV ≈ V·Δψ(rad). At 10° course error: 15 kn → 2.6 kn, 20 kn → 3.5 kn, 30 kn → 5.2 kn. At 20°: 20 kn → 6.9 kn, 30 kn → 10.4 kn. With the range-table relation (about 12.4 yd per kn at 10 kyd for the table shown; about 17 yd per kn at 30 s TOF), a 20° misjudgement on a 30 kn destroyer at 10 kyd gives about 130 yd of prediction error. On a 15 kn battleship the same misjudgement gives about 65 yd.
- [INF] Suggested model form for the error of an estimated (eye or plot) target velocity vector, split into along-track and across-track parts:
  - along-track: σ_speed ≈ a + b·V, with a ≈ 1 kn and b ≈ 0.05–0.1. This anchors to the 1–2 kn after-cancellation figure and about a 7% perceptual speed error.
  - across-track: σ_cross ≈ V·σ_ψ, with σ_ψ ≈ 10–20° by eye, worse near bow-on and stern-on aspects. This is a [UNC] anchor from the 18° drone case and the horizon-effect finding.
  - Settled rate error from a computer then shrinks this toward the existing per-system floor (5–8 / 1.5 / 1 / 0.7 kn). Because rate control nulls the observed mismatch, the floor plausibly scales only weakly with V. A small proportional term (e.g. +2–3% of V) is defensible but unsourced.
- [INF] Bow-on and stern-on aspects are worst for course reading, since the inclinometer relation goes as cos(target angle) and is flat near 0° and 180°. They are also where target speed moves almost entirely into range rate. A stern chase therefore puts a large speed-estimation error straight into range, which spotting must remove.

### Gaps
- No sourced surface-target table of target-angle or speed estimation error against target speed or range, from RN, USN or IJN trials. Candidates I could not reach: RN Gunnery Manuals and *Dreyer's handbook* text on "enemy speed" errors; Blodgett's 1920 MIT dazzle tests (results not quantified on Wikipedia).
- No sourced figure for how settled rangekeeper rate error (Mk 8, AFCT) varies with target speed.

---

## 2. Rangekeeper and fire-control design limits (target speed, own speed, range rate, bearing rate)

### Takeaway
Few hard limits are documented online. The sourced ones: Dreyer range clocks at ±1,200 yd/min, which is only about ±35.5 kn of range rate; Dreyer Mk III bearing gear at ±15°/min, constant rate set by hand; Mk 1A automatic rate control valid for targets of 15–800 kn, with manual rate below 15 kn and a fixed 3,000-yd sensitivity setting for surface targets; Mk 1 computer target speed originally stopped at 300 kn, later 600 and 1,200 kn. For the Dumaresq, Vickers clock, Argo, Ford Mk 1, Mk 8, AFCT, Type 92/98 and German C/38, no numeric speed limits were found. Their documented weakness is constant-rate assumptions and inability to follow changing rates.

### Cited Findings
- Dreyer tables: "Generally, the range clocks ran from 1200 yards per minute closing to 1200 yards per minute opening." Mk III change-of-bearing gear was a manually set constant rate limited to ±15°/min. Mk III* and later made the bearing rate range-sensitive, with no limit listed. Range scales started at 2,000 yd, e.g. Mk I 2,000–20,000 yd, Mk V 2,000–29,000 yd. Own speed came from the Forbes log by hand. The handbook does not say what happened when limits were exceeded. — [Dreadnought Project, Handbook of Dreyer Fire Control Tables 1918](https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php)
- Dreyer range-rate output came in 25 yd/min steps, and gun deflection in 1 kn steps. Early bearing transmission was in 1/4° steps (4 arc-minutes in Ramillies from July 1917). The original bearing plot spanned 120° across 16 in of paper and was "hard to read for moderate bearing rates." By 1919, range-cut latency, scarcity and reliability were judged insufficient for taking range rate from the plot's trend. — [Dreadnought Project, Dreyer Fire Control Table](https://dreadnoughtproject.org/tfs/index.php/Dreyer_Fire_Control_Table)
- Vickers clock: it generated ranges only at a constant range rate. Changing the rate while running slid the wheel across the disc and risked damage, so operators made quick discrete steps, "accepting that the generated ranges would deviate moderately" from the true hyperbolic range curve. The rate dial was graduated in knots and "seconds per 50 yards" until 1909, then in yd/min. — [Wikipedia, Vickers range clock](https://en.wikipedia.org/wiki/Vickers_range_clock); [Naval Gazing Rangekeeping Part 1](https://navalgazing.net/Rangekeeping-Part-1)
- Dumaresq and Vickers clock "worked only when range rate was low and not changing quickly"; maneuvering targets could throw them off. Pollen's integrator (Argo) updated rates continuously, which helped with high, variable range rates such as battlecruiser actions. — [Naval Gazing, Rangekeeping Part 2](https://navalgazing.net/Rangekeeping-Part-2)
- The Dumaresq of Dreyer Mk I could use only a number of preset speeds and could not adapt to rapid speed changes. — [Jutland 1916 Centenary, Range finding and course plotting](https://jutland1916.com/?p=33066)
- Dumaresq: own and enemy speeds set on knot scales, with no maximum given. Outputs were range rate in yd/min and speed-across (deflection) in knots. The Mk IV kept relative enemy direction within "a few degrees" during an own-ship turn. — [Wikipedia, Dumaresq](https://en.wikipedia.org/wiki/Dumaresq)
- Mk 1A computer (Mk 37 GFCS): automatic rate control computes target motion for 15–800 kn targets. Below 15 kn the mechanism is inaccurate and manual rate control must be used. The Sh counter reads 0–800 kn. Automatic rate is normal for air targets and for surface targets ≥15 kn. Rate-network time constant Tc = 2·R/R′. For surface targets R′ is fixed at 3,000 yd unless the sensitivity button is pressed, which holds R′ at 8,000 yd for about 2 s. For air targets R′ is limited to 500–8,000 yd. Crosslevel limit switch at Zd = 17.5°. — [NavPers Ch. 25-C](https://eugeneleeslover.com/USNAVY/CHAPTER-25-C.html)
- Mk 1 computer target speed was "limited to 300 knots by a mechanical stop", raised by gear changes to 600 kn and then 1,200 kn. Jets outpaced it. — [Wikipedia, Mark I Fire Control Computer](https://en.wikipedia.org/wiki/Mark_I_Fire_Control_Computer)
- Mk 37 (OP 1064 hyperwar edition): using the air time constant for surface fire would make each small director-train perturbation change the target set-up. Post-Ordalt 2331A, an AIR/SURFACE selector sets a surface time-constant dial (3). Radar tracking cannot follow angular rates above 25°/s (air context). Tracking for one or two time constants with rate control reduces an initial rate error by about 65–85%. — [OP 1064 / FCS Mk 37, hyperwar](https://ibiblio.org/hyperwar/USN/ref/Ordnance/FCS-Mark37/index.html)
- Ford Mk 1 rangekeeper (Texas, 1917): own speed was entered by hand, target speed and course were estimated by the gunnery officer, and a steady target course and speed were assumed. TOF at maximum range was about 1.5 min, and a 20 kn ship covers 675 yd/min. — [Naval History, "The Revolutionary Rangekeeper" (2015)](https://www.usni.org/magazines/naval-history-magazine/2015/october/revolutionary-rangekeeper)
- USN rangekeepers assume target relative motion is constant over the short prediction interval. A target changing course or speed while unseen makes regenerated gun orders inaccurate. — [NavPers Ch. 19-F](https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html)
- Japanese system: Shagekiban operators passed data by follow-the-pointer, with no way to gauge solution accuracy. Errors (optical-only ranging, poor gyros, manual follow-ups) grew as battles wore on and crews tired. No numeric limits are given. Example ΔR term "333 yds/min or 10 knots". — [NavWeaps tech-086 (Japanese vs US fire control)](https://navweaps.com/index_tech/tech-086.php)
- Citing Friedman's *Naval Firepower*: the only interwar or WWII computer able to handle a turning target was the last Admiralty Fire Control Table, and only for a constant turn. — [Warships1 forum (abclark98)](https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=884028) [UNC, forum citing a book]

### Inferences
- [INF] The Dreyer clock limit of ±1,200 yd/min equals about ±35.5 kn of range rate (1 kn = 33.75 yd/min). Two ships closing head-on at 25 + 25 kn (about 1,690 yd/min) exceed it. A battlecruiser at 28 kn running down a 20 kn target on a near-radial course is close to the limit. In the game, a range rate above the clock limit can be treated as "range clock pinned": generated range drifts by (true rate − 1,200 yd/min) × time and must be fed by hand from rangefinder cuts.
- [INF] Converting the Mk III bearing gear limit of ±15°/min (0.25°/s) to speed across with the Ch. 19-F formula (arcmin/s = 1,936 × kn / yd):
  - at 10,000 yd, 15 arcmin/s ≈ 77 kn across, so never limiting;
  - at 5,000 yd ≈ 39 kn;
  - at 3,000 yd ≈ 23 kn;
  - at 2,000 yd ≈ 15 kn.
  So the limit binds only in short-range (night or destroyer) actions with large relative crossing speeds.
- [INF] Mk 1A ≥15 kn auto-rate rule: in the game, slow surface targets below 15 kn fall back to manual rate control. Paradoxically, a slow or stopped target is then handled by the human rate floor (~5 kn?). Uncertain: manual rate on a Mk 1A with a director tracking continuously is probably much better than the plot-era 5–8 kn.

### Gaps
- Mk 8 rangekeeper and AFCT target-speed, range-rate and bearing-rate limits: not found online (OP 1064 Appendix p. 378 "Limits of Accurate Computation" exists but could not be downloaded; proxy-blocked).
- Argo clock, Type 92 Shagekiban, Type 98 Hoiban and German C/38 Rechenstelle: no numeric limits found.
- No sourced account of what crews did when a range clock pinned at its stop. Probably they fed range by hand, but this is unconfirmed.

---

## 3. High bearing rates at short range: director and turret train limits, tracking jitter

### Takeaway
Bearing rate goes as relative cross speed divided by range, so it blows up at short range. Turret train rates of about 4°/s (Iowa) only bind inside about 1,000 yd. The likelier real limit is the human pointer and trainer and the computer's smoothing. Sourced statements say Method 1 (pointer-tracked) range dispersion "depends largely on how accurately the pointer tracks the target" and that radical own-ship manoeuvring disrupts director laying. No quantitative trainer-jitter-versus-bearing-rate data was found.

### Cited Findings
- Angular bearing rate dBs (arcmin/s) = 1,936 × RdBs (kn) / R (yd), "therefore grows as range shrinks." The angular deflection correction grows as advance range decreases. — [NavPers Ch. 19-F](https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html)
- Iowa-class turrets train about 300° at roughly 4°/s; guns elevate at up to 12°/s. — [Wikipedia, 16"/50 Mk 7](https://en.wikipedia.org/wiki/16-inch/50-caliber_Mark_7_gun); [Wikipedia, Armament of the Iowa class](https://en.wikipedia.org/wiki/Armament_of_the_Iowa-class_battleship)
- Mk 37 director: train 375° either way from neutral (cable twist), elevation −25° to +110°. Slew rates not given. — [NavPers Ch. 25-C](https://eugeneleeslover.com/USNAVY/CHAPTER-25-C.html)
- Radar tracking (Mk 37) cannot follow angular rates above 25°/s. — [OP 1064 hyperwar](https://ibiblio.org/hyperwar/USN/ref/Ordnance/FCS-Mark37/index.html)
- Method 1 (pointer on the waterline): range dispersion "depends largely on how accurately the pointer tracks the target." Method 2 (elevation from the stable element) usually gives smaller dispersion. — [OP 1064 hyperwar](https://ibiblio.org/hyperwar/USN/ref/Ordnance/FCS-Mark37/index.html)
- Before Ordalt 2331A, Mk 1A time constants at short range were too low, so small tracking irregularities caused oscillation of the solution. — [OP 1064 hyperwar](https://ibiblio.org/hyperwar/USN/ref/Ordnance/FCS-Mark37/index.html)
- 1942 Pacific Gunnery Bulletin: radical manoeuvring to avoid bombs disrupted rangekeeper solutions and director laying, especially on destroyers. — [CINCPAC Gunnery Bulletin 2-42](https://ibiblio.org/hyperwar/USN/rep/CINCPAC/GunBull/2-42/index.html)
- 1900 Malta practice: maximum range fired was 1,500 yd. A 1° bearing error at about 5,000 yd is about 262 ft, enough to turn a hit on a battleship into a miss. — [Naval Gazing, Rangekeeping Part 2](https://navalgazing.net/Rangekeeping-Part-2)
- Massachusetts at Casablanca had no receiver-regulators and fired follow-the-pointer the whole action; the author links fatigue errors to this. — [NavWeaps tech-086](https://navweaps.com/index_tech/tech-086.php)

### Inferences
- [INF] Bearing-rate examples (Ch. 19-F formula):
  - 40 kn across at 5,000 yd ≈ 0.26°/s;
  - at 2,000 yd ≈ 0.65°/s;
  - at 1,000 yd ≈ 1.3°/s.
  - 4°/s turret train would need about 120 kn across at 1,000 yd or 60 kn at 500 yd.
  So for heavy turrets, train rate binds only at point-blank range (e.g. Guadalcanal-style mêlées). Older or hand-trained mounts (about 1–2°/s?) would bind earlier; that range is unsourced.
- [INF] Tracking noise model: trainer error σ_train ≈ σ0 + k·(bearing rate) is plausible but unsourced. A suggested game value is σ0 ≈ 0.5–1 mil and k ≈ 0.5–1 mil per °/s, flagged [UNC].

### Gaps
- No quantitative data found on trainer or pointer error against bearing rate, or on WWI hand-training rates against bearing rate.
- No data on older (1900–1920) turret train rates from these sources.

---

## 4. Historical and practice hit rates against fast vs slow targets

### Takeaway
Practice targets were mostly slow (main-battery towed targets under 10 kn, often drifting), with 20–24 kn sleds for secondary batteries. Hit rates in practice were therefore set mostly by range, not speed. Battle data shows chases and high-speed long-range actions with low hit percentages: Dogger Bank about 1–2% for the British, Santiago under 3%, Jutland about 3%, Komandorski very low, Iowa vs Nowaki 0/40. Slow or close targets were hit much more often: Washington vs Kirishima 9/75 at 8,400 yd, and North Cape, where most hits came once Scharnhorst slowed and range closed. Confounders (range, visibility, smoke, training) dominate. No source isolates target speed statistically.

### Cited Findings
- USN practice target speeds: standard battleship target 785 ft at 20 kn; secondary-battery destroyer sleds about 20–24 kn closing from about 70° off the bow; main-battery towed targets "usually under 10 kts", with drifting or stopped targets common. Towing at high range rates (near-radial) was barred for safety. — [INRO BB Gunnery](https://navweaps.com/index_inro/INRO_BB-Gunnery.php)
- USN practice hit data:
  - LRBP 1932–33: Colorado 4.2% at 27.5 kyd, Maryland 5.4% at 29.9 kyd, West Virginia 3.7% at 32.1 kyd.
  - Battle-raft LRBP: 60/1,179 ≈ 5% at 27,450 yd.
  - Night Battle Practice B 1930–31 at about 5,000 yd: 6.6% (Texas) to 40.5% (California).
  — [INRO BB Gunnery](https://navweaps.com/index_inro/INRO_BB-Gunnery.php)
- INRO author's estimate: destroyers at 30 kn closing from 16,000 to 10,000 yd (about 7 min) under 6-gun fire at 0.35 hits per gun per minute would take about 14 hits. Spread across six destroyers at 30% barrage efficiency, that is under one hit per ship. Real aircraft changing speed mid-run defeat a prior solution. — [INRO BB Gunnery](https://navweaps.com/index_inro/INRO_BB-Gunnery.php)
- 1918, BatDiv 9 (US in the Grand Fleet): ships at 18 kn, towing ship zigzagging every 3 min, mean ranges about 11,758–12,900 yd. Two or three straddles per run, the first usually on the 2nd or 3rd salvo. Rangefinder rates were unreliable for New York and Texas. — [NHHC, Rodman to SecNav, April 1918](https://history.navy.mil/research/publications/documentary-histories/wwi/april-1918/rear-admiral-hugh-ro-0.html)
- Santiago 1898 (stern chase of fleeing Spanish cruisers): fewer than 3% of US shells hit, far below practice predictions. Longest combat range about 6,000 yd. Range, stress and smoke are cited. — [Naval Gazing, Spanish-American War Part 8](https://www.navalgazing.net/Spanish-American-War-Part-8)
- Falklands 1914 (stern chase, BCs 25.5 kn vs Spee 22.5 kn): Invincible fired 513 and Inflexible 661 12-in rounds, 1,174 in total. NavWeaps OOB estimates about 40 12-in hits on each armoured cruiser [UNC: estimate; a ~7% rate if taken at face value]. British funnel smoke obscured the targets "practically throughout the action". Ranges were about 13,500–16,450 yd. — [NavWeaps OOB Falklands](https://www.navweaps.com/index_oob/OOB_WWI/OOB_WWI_Falklands.php); [Wikipedia, Battle of the Falkland Islands](https://en.wikipedia.org/wiki/Battle_of_the_Falkland_Islands); [naval-history.net](https://naval-history.net/WW1Battle1412Falklands.htm)
- Dogger Bank 1915 (high-speed chase, up to 27 kn, opening at about 20,000 yd): British fired about 1,150 heavy shells for about 7–16 hits depending on source (about 0.6–1.4%). Germans fired 988 for about 22–25 (about 2.2–2.5%). "Long-range fire at high speed was unprecedented." Tiger mistook Lion's splashes for her own. — [Wikipedia, Battle of Dogger Bank (1915)](https://en.wikipedia.org/wiki/Battle_of_Dogger_Bank_(1915))
- Jutland, British about 3% hits overall. — [Wikipedia, Rangekeeper](https://en.wikipedia.org/wiki/Rangekeeper). New Zealand and Tiger: 723 rounds, 6 hits. — [Jutland 1916 Centenary, Gunnery performance](https://www.jutland1916.com/tactics-and-technologies-4/gunnery-performance)
- Komandorski 1943 (running retirement at 25–33 kn, enemy up to 30–31 kn, ranges about 18,000–22,800 yd): Salt Lake City fired 806 8-in AP + 26 HC + 95 5-in, and the group fired 3,465 rounds in total. Salt Lake City scored a handful of hits (at least 2 at 0842, several at 0907). She took about 4 hits while about 200 rounds fell within 50 yd. Japanese patterns were "excellent" (200 × 30 yd). Salt Lake City, Dale and Bailey "chased the salvos." — [Hyperwar ONI Combat Narrative, Aleutians](https://ibiblio.org/hyperwar/USN/Aleutians/USN-CN-Aleutians-9.html)
- 2nd Guadalcanal 1942: Washington put 9 of 75 16-in rounds into Kirishima (about 12%) at 8,400 yd at night. — [Wikipedia, Rangekeeper](https://en.wikipedia.org/wiki/Rangekeeper). Washington did not re-engage a five-ship group tracked at 13,800 yd at an estimated 26–29 kn. — [NavWeaps Lundgren, BB action Guadalcanal](https://navweaps.com/index_lundgren/bbActionGuadalcanal.php)
- Truk 1944: Iowa and New Jersey, at 30 kn, opened on the destroyer Nowaki at 37,500 yd. The first salvo straddled; five salvos (40 rounds) from Iowa scored no hits. Nowaki had "just enough margin of speed to escape." HC ballistic cams were missing and corrections were computed by hand. Earlier, Katori (slow light cruiser) was straddled by all 8 salvos. — [Naval Gazing, Iowa Part 2](https://www.navalgazing.net/Iowa-Part-2)
- North Cape 1943: Duke of York opened at about 12,000 yd; range opened to 21,400 yd by 18:24 as Scharnhorst ran. 52 broadsides were fired, with about 10 14-in hits credited. A boiler-room hit slowed Scharnhorst (10 kn by 19:16). DoY reopened at 10,400 yd. — [Wikipedia, HMS Duke of York (17)](https://en.wikipedia.org/wiki/HMS_Duke_of_York_(17))
- 1942 AA bulletin: "Some ships checked fire to spot, which is disastrous against a fast target." Continuous rate control without spotting was preferred. — [CINCPAC Gunnery Bulletin 2-42](https://ibiblio.org/hyperwar/USN/rep/CINCPAC/GunBull/2-42/index.html)
- Comparison of Iowa vs Nowaki with Hiei vs Edsall (both poor against fleeing or evasive destroyers). — [Warships1 forum (TOS1956)](https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=884028) [UNC, forum]

### Inferences
- [INF] The battle data are consistent with a speed or chase penalty but cannot separate it from range and smoke. Stern chases (Santiago, Falklands, Dogger Bank, Komandorski, Nowaki, North Cape opening phase) had low hit rates. They also had smoke blowing down the range and opening or long ranges. A defensible game approach: let the penalty emerge from the mechanics (rate error ∝ V, re-convergence after zigzags, splash-chasing, and clock limits) rather than from an explicit speed modifier.
- [INF] A stern chase looks easy geometrically (low bearing rate) but is hard in practice. Target speed sits almost entirely in range rate, where errors are least visible to deflection spotting; smoke blows toward the pursuer; and the fleeing target can "chase salvos" with small course changes that cost little speed.
- [INF] North Cape suggests hits concentrate once the target's speed drops. That is consistent with a combined effect of lower rate error and a closing range.

### Gaps
- No USN or RN practice analysis found that tabulates hit rate against target speed (Utah AG-16 target runs: DANFS gives no speeds, only "varying rates of speed": [DANFS Utah](https://www.history.navy.mil/research/histories/ship-histories/danfs/u/utah.html)).
- RN battle practice target speeds (pre-1914) not found.
- Japanese rounds fired at Komandorski and at Samar (vs. 30+ kn US destroyers) not found in sources opened.

---

## 5. Range-rate effects (high closing/opening rate, clock error, spotting lag, rate spotting)

### Takeaway
Sourced: constant-rate clocks (Vickers, Dreyer) were accurate only while range rate was low and steady. The true range rate changes continuously even on steady courses, because range follows a hyperbola, and step adjustments left "moderate" deviations. Range rate taken from plotting range cuts was judged too slow and unreliable by 1919. Spotting must correct both range and range rate when salvos walk consistently short or over. Splash-formation lag shifts apparent deflection on a high-speed target.

### Cited Findings
- Ships on constant courses do not necessarily have a constant range rate. The Vickers clock's rate was hard to change while running. — [Naval Gazing, Rangekeeping Part 1](https://navalgazing.net/Rangekeeping-Part-1)
- The range clock assumes range changes linearly with time; other dynamics need manual correction. The Mk I range clock lacked power, and an operator chased its pointer by hand. — [Dreadnought Project, Dreyer Handbook 1918](https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php)
- If salvos straddle and then fall consistently short or long, both range and range rate must be adjusted ("rate" correction by spotting). — [Naval Gazing, Rangekeeping Part 1](https://navalgazing.net/Rangekeeping-Part-1)
- Splashes take "a few seconds at most" to form, long enough for a high-speed target to move noticeably, so the spotter holds the apparent deflection MPI abaft the point of aim. Pyramiding occurs if the spotting interval is shorter than TOF plus the spotting interval. — [NavPers Ch. 18-B](https://eugeneleeslover.com/USNAVY/CHAPTER-18-B.html)
- 1908 Proceedings: "Knowing the range at a given instant will be of small value if the change and the sign of the change is unknown." In a worked example (18 kn vs 15 kn at 6,000 yd), computed sight-bar ranges came out about 60–65 yd over the actual distances. — [USNI Proceedings 1908](https://www.usni.org/magazines/proceedings/1908/january/another-argument-speed-battleship-design)
- A destroyer reported that five radar ranges gave "a perfect setup of range rate" (1942). — [CINCPAC Gunnery Bulletin 2-42](https://ibiblio.org/hyperwar/USN/rep/CINCPAC/GunBull/2-42/index.html)
- At 20 kn a ship covers 675 yd/min; heavy-gun TOF at maximum range is about 1.5 min. — [Naval History 2015](https://www.usni.org/magazines/naval-history-magazine/2015/october/revolutionary-rangekeeper)

### Inferences
- [INF] Rate-of-change-of-range-rate (range acceleration) for straight-line motion is Ṙ̈ = (cross relative speed)² / R. At 40 kn across (22.5 yd/s) and 10,000 yd that is about 0.05 yd/s² ≈ 3 yd/min per second, i.e. about 180 yd/min of range-rate change per minute. A constant-rate clock left unadjusted for 1 min accumulates about ½·a·t² ≈ 90 yd of range error. At 5,000 yd this doubles to about 180 yd. This gives a speed² / range scaling for constant-rate-clock era (Vickers, Dreyer Mk I–III) "clock drift", independent of estimation error. Pollen's Argo and all USN rangekeepers resolve geometry and avoid it.
- [INF] Spotting latency: with TOF T and a spotting cycle S, a range-rate error ΔṘ produces a walk of ΔṘ·(T + S) per spot. High range rates amplify any fractional rate error. If rate error is a fixed fraction of range rate (plot-era), miss grows linearly with closing/opening speed.

### Gaps
- No sourced numbers on how RN range-rate errors scaled with high closing rates at Jutland or Dogger Bank (Brooks' *Dreadnought Gunnery and the Battle of Jutland* has this but was not accessible online).

---

## 6. Operations-research and textbook treatments (formulas for hit probability vs target speed or manoeuvre)

### Takeaway
The textbooks (NavPers Ordnance & Gunnery, OP 1064, range tables) give the deterministic relations: offset = V·TOF per component, angular deflection ∝ V_cross·TOF/R, and hit probability from Gaussian MPI error plus dispersion over the hitting space. They do not give hit probability as an explicit function of target speed. Speed enters only through the MPI error the rate error produces. Modern OR (DSTO 2004, AA) treats manoeuvre as an "escape distance" set by acceleration × TOF².

### Cited Findings
- Hit-probability worked example (5"/38, 10-shot salvo, 8,500 yd): MPI centred gives range P 0.379, deflection 0.981, combined about 0.372. MPI offset by half a pattern (130 yd) drops combined P to about 0.116. Hitting space in range is 80 yd. The source gives no function of target speed. — [NavPers Ch. 18-B](https://eugeneleeslover.com/USNAVY/CHAPTER-18-B.html)
- Total dispersion c = √(a² + b²) (aiming error and natural dispersion). "Most hits come when the dispersion is 4/5 of the fire-control error." — [USNI Proceedings 1917, "Estimate of the value of accuracy of pointing"](https://www.usni.org/magazines/proceedings/1917/june/estimate-value-accuracy-pointing-long-range-naval-gunnery)
- Range table columns 15 and 18 give the linear per-10-kn offsets (= 10 kn × TOF). — [USN Range Tables](https://www.eugeneleeslover.com/USN-GUNS-AND-RANGE-TABLES/USN-RANGE-TABLES-COLUMN-15-AND-18.php)
- Rate control with a Mk 1A reduces initial rate error by 65–85% after one or two time constants (Tc = 2R/R′). — [OP 1064 hyperwar](https://ibiblio.org/hyperwar/USN/ref/Ordnance/FCS-Mark37/index.html); [Ch. 25-C](https://eugeneleeslover.com/USNAVY/CHAPTER-25-C.html)
- DSTO-TN-0565 (2004), gun vs manoeuvring stand-off weapon: a gun with 1.5 mrad dispersion can engage a 1 g manoeuvring target only inside about 600 m, and inside about 100 m at 5 g. At 500 m, rounds for a 50% hit rise from 21 (1 g) to 455 (5 g). Side-on geometry is far more favourable. — [DTIC ADA426717](https://apps.dtic.mil/sti/tr/pdf/ADA426717.pdf) (air-defence context; included only for the functional form)
- Thorsten Wahl argues that a target can usually escape the predicted zone only with roughly 30 s or more of flight time, and that course changes cost speed. — [Warships1 forum](https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=884028) [UNC, forum]

### Inferences
- [INF] A consistent model chain: σ_V (velocity-estimate error, scaled with V as in §1) → after rate control σ_V,settled = max(floor_system, σ_V·e^(−t/τ)) → MPI offset = σ_V,settled × TOF (decomposed into range and deflection by aspect) → P_hit from the Gaussian over the hitting space (NavPers method). Target manoeuvre adds an acceleration term ½·a·TOF² (DSTO form) and resets σ_V by 2V·sin(Δψ/2).
- [INF] Because both the course-error term (V·Δψ) and the manoeuvre term (2V·sin(Δψ/2) and a = V·ω) scale linearly with V, a single "target speed multiplier" on rate-error variance is a reasonable simplification: σ_rate² = floor² + (k·V)². The additive manoeuvre reset handles the rest.

### Gaps
- Isaacs, NAVPERS 91900 "Fire Control Fundamentals", OP 1140 and USN OEG reports: not reached. A dedicated OEG or DTIC study of surface gunnery hit probability against target speed (if one exists) was not found.

---

## 7. Speed as defence: did faster ships get hit less (statistics or doctrine)?

### Takeaway
Doctrine yes, statistics no. Fisher's late slogan "speed is armour" (1919) and the 1905 Committee on Designs ("Speed is necessary to ensure safety") framed speed as protection, mainly through choosing range and position rather than through fire-control difficulty. Interwar critics argued that visibility, not speed, sets range, and that speed is fragile. One explicit observation: at Jutland, destroyers zigzagging at high speed in smoke and splashes were much harder to hit. No statistical analysis linking ship speed to hits received was found.

### Cited Findings
- Fisher's 1919 slogan "speed is armour" was written in criticism of Hood. The Committee on Designs: "Speed is necessary to ensure safety – armour protection to ensure vision." Dannreuther (1902) wanted at least a 3 kn speed advantage for the van. Jackson cited Russo-Japanese War reports for speed's "overwhelming tactical value." — [Navy General Board, "Fisher and the first fast battleships"](https://www.navygeneralboard.com/admiral-sir-john-fisher-and-the-first-fast-battleships/)
- 1929 Proceedings prize essay:
  - Fisher's theory was "an attractive theory" but flawed, because "the factor which determines the range at which you will fight is the Clerk of the Weather" (Chatfield).
  - At Jutland, destroyers zigzagging at high speed, partly hidden by smoke and splashes, were much harder to hit than slow, straight-running targets; two British destroyers attacked battlecruisers from 5,000 to 3,500 yd and escaped, with German hits coming only after they turned away.
  - "Speed is the most vulnerable of the characteristics of a fighting ship."
  - At 30,000 yd, changing bearing takes about 3 hours versus 1 hour at 10,000 yd, which reduces speed's tactical value.
  — [USNI Proceedings 1929, "Speed in battleships"](https://www.usni.org/magazines/proceedings/1929/april/speed-battleships)
- 1908 Proceedings: the faster fleet "is holding the range with ease and is hammering its shells home", because it controls range change while the slower fleet must guess it. — [USNI Proceedings 1908](https://www.usni.org/magazines/proceedings/1908/january/another-argument-speed-battleship-design)
- Iowas "had enough speed to dictate the range." — [INRO BB Gunnery](https://navweaps.com/index_inro/INRO_BB-Gunnery.php)
- Nowaki escaped Iowa and New Jersey with "just enough margin of speed". — [Naval Gazing, Iowa Part 2](https://www.navalgazing.net/Iowa-Part-2)
- Komandorski: the Japanese used their speed advantage to zigzag while firing full broadsides, which kept the range open but cancelled the speed advantage for closing. — [USNI Proceedings 1995, "Komandorskis"](https://www.usni.org/magazines/proceedings/1995/june/komandorskis)

### Inferences
- [INF] Two separate mechanisms for the game. (a) Positional: a speed margin lets a ship set range and aspect, including breaking off; this is sourced doctrine. (b) Fire-control: higher V raises rate-error variance and makes evasive turns more effective (2V·sin(Δψ/2) per turn). (b) is supported by the geometry and the Jutland destroyer observation, but no historical statistic quantifies it.
- [INF] Speed also hurts the shooter. Own speed contributes to relative rate (own course and speed errors also cause MPI error per NavPers 18-B), and high own speed brings vibration that degrades rangefinders. Coincidence rangefinder performance "significantly deteriorated" with normal ship vibration ([Jutland 1916](https://jutland1916.com/?p=33066)), and Bismarck's turret A rangefinder fogged with spray at high speed ([kbismarck.com](https://kbismarck.com/controltiri.html)).

### Gaps
- No statistical study found comparing hits received against ship speed in either World War. A candidate data source would be the WWII USN damage reports cross-referenced with action speeds, but nothing found was pre-digested.
