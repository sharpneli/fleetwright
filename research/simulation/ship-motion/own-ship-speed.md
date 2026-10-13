# Own-ship speed as a degrader of naval gunnery, 1890s–1980s
Status: draft    Updated: 2026-10-12    Request: -    Appendix to: [ship-motion-gunnery-research.md](ship-motion-gunnery-research.md)

Scope: how the firing ship's own speed hurt (or entered) gunnery, by mechanism. Tags: [S] = sourced (URL given beside it); [INF] = my inference; [UNC] = uncertain or single weak source. The research stopped early because the shared web-search budget ran out. The gaps listed under each question are real gaps, not omissions.

Overall finding: the open web gives **almost no hard numbers** for speed effects (no vibration amplitudes, no rangefinder error versus knots, no published doctrine saying "slow to X kn to shoot"). What survives are **qualitative failure thresholds tied to specific ships and events**. The mechanisms are smoke, spray or green water, vibration, and the ballistic own-speed term. The most usable quantitative pieces are (a) the NavPers own-speed ballistic geometry, (b) the seakeeping physics (encounter frequency, deck-wetness probability), and (c) a few "had to slow to N kn to keep turrets in action" data points.

---

## 1. Hull and mast vibration at high speed

### Takeaway
Machinery vibration was a recognised, primary cause of poor rangefinder results. The German post-WWI assessment names it explicitly, and RN WWI sources say the director rangefinder "significantly deteriorated" with normal ship vibration. No amplitudes, frequencies or speed thresholds were found in open sources. Most documented "mast shake" in action came from own gun blast or battle damage, not from speed.

### Cited Findings
- Lienau ("German Optical Rangefinders") lists among the *primary causes of poor results* the "permanent rhythmic oscillation found on ships created by the propulsion machinery, mainly the gearsets." Other primary causes were one-sided heating of the optics by sun, **funnel smoke and engine exhaust air**. The listed fixes are thermal isolation, air dryers and blowers, rotating clear-view screens, and from 1918 "internal correction" (calibration), described as "workable" but "not really satisfactory." No vibration-specific fix is named. — [navweaps tech-078](https://navweaps.com/index_tech/tech-078.php)
- RN WWI: the director's Barr & Stroud rangefinder's capability "significantly deteriorated with a ship's normal vibration". Hand-held rangefinding was harder with engine shaking, rolling and pitching, cordite smoke across the view, and spray. — [Jutland 1916 Centenary, Range Finding and Course Plotting](https://www.jutland1916.com/tactics-and-technologies-4/range-finding-and-course-plotting-2)
- Invincible at the Falklands (8 Dec 1914): the gunnery report notes the mast "shook noticeably" when A turret fired. The report attributes this mainly to **damage to the starboard foremast strut** (i.e. blast plus damage, not speed). The fore-top rangefinder was adjusted before action but "found badly out shortly after firing began." — [tapatalk alltheworldsbattlecruisers, quoting the Invincible gunnery report](https://www.tapatalk.com/groups/alltheworldsbattlecruisers/viewtopic.php?p=23270) [UNC: forum transcription of a primary document]
- Invincible's speed in that action ran from 22 up to about 26 kn between 12.20 and 12.50, and "308 revolutions or 27 knots" is recorded. Sturdee's telegram: "Range finders found little use due to long range, spray from enemy, short shots, and funnel smoke." The telegram does **not** list vibration. — [Dreadnought Project, HMS Invincible at the Battle of the Falkland Islands](https://dreadnoughtproject.org/tfs/index.php/H.M.S._Invincible_at_the_Battle_of_the_Falkland_Islands)
- Iowa-class at full power: vibration was felt "only aft of frame 166" (the aft armoured bulkhead) and nearly disappeared forward of it. A compartment aft felt "like standing on a jackhammer," and notebook handwriting became illegible. A rooster-tail appeared at about 26 kn. Trials ran to 31–32 kn and about 200 shaft rpm. — [navweaps tech-054, Landsgraff, "Full Speed Ahead and Crash Stops on Iowa Battleships"](https://navweaps.com/index_tech/tech-054.php)
- New Jersey ship handling (1988): the only vibration described came from heavy swells torquing the tapered bow, "a subtle vibration throughout the ship." Bridge windows had to be lowered before 16-in firing because blast and vibration would shatter them. — [USNI Proceedings 1988, Handling the Battleship](https://www.usni.org/magazines/proceedings/1988/april/handling-battleship)
- Propeller cavitation was "the single-most important consideration at speeds in excess of 30 knots" (Raven, Feiler & Jesperson 1947, quoted). — [Axis History Forum](https://forum.axishistory.com/viewtopic.php?p=2325126) [UNC: secondary quote in a forum]
- DTMB measured radar-mast vibration (maximum amplitudes and accelerations at propeller-blade and shaft frequencies, and during manoeuvres) on USS Long Beach (CGN-9) and Okinawa (LPH-3). Only the abstract was available; it contains no numbers. — [DTIC AD0428719](https://apps.dtic.mil/sti/html/tr/AD0428719/index.html)
- A DTMB 1950 report by N. Jasper, "Structural Vibration Problems of Ships – A Study of the DD692 [Allen M. Sumner] Class of Destroyers," exists. The PDF could not be retrieved. — [MIT DOME](https://dome.mit.edu/handle/1721.3/51138?show=full)
- Courageous "sustained structural damage while running at full speed" in a rough head sea on trials and was stiffened afterwards. Glorious was not stiffened until 1918. — [Wikipedia, HMS Glorious](https://en.wikipedia.org/wiki/HMS_Glorious)
- KGV (1911) class: the director and enlarged spotting top were too heavy for the unsupported pole foremast, which was reinforced in 1913–14 (flanges on KGV, half-height tripod legs on the others, full tripod on KGV in 1918). — [Wikipedia, King George V-class battleship (1911), citing Brooks 1995/1996](https://en.wikipedia.org/wiki/King_George_V-class_battleship_(1911))
- Lion: after the funnel/mast swap the foremast became a pole. It reverted to a tripod after 1916 "because of the weight of masthead fire-control equipment." — [Wikipedia, Lion-class battlecruiser](https://en.wikipedia.org/wiki/Lion-class_battlecruiser)

### Inferences
- [INF] Vibration acts in the game mainly as a **magnification-cap / random-error inflator on optics mounted on tall, light supports**. The project's `designer_ref.py` already has `M_vib` by support type. Speed should scale it. A plausible form: k_vib(v) = 1 + a·(v/v_max)^n with n ≈ 2–3, since blade-rate excitation forces scale roughly with propeller loading (∝ speed², power ∝ v³). Make `a` larger for (i) pole masts versus tripods versus tower/turret, (ii) light, high-power hulls (cruisers, destroyers, Courageous-type), and (iii) geared-turbine ships, per Lienau. Suggested starting values: a ≈ 0.3 for a battleship tower, 0.6 for a tripod, 1.0 for a pole or light-cruiser foretop (so k ≈ 1.3–2 at full power). These are uncalibrated.
- [INF] Iowa evidence suggests vibration was strongly localised aft, near the screws. Forward directors and rangefinders on big ships were probably little affected. Aft directors and after turrets suffered more. This argues for a position term (aft > forward).
- [INF] The project's existing claim "At the Falklands vibration and mast shake made a stereo rangefinder useless at high speed" is **not supported** by the primary material found here. The Invincible report attributes mast shake to A-turret blast plus a damaged strut, and Sturdee blames range, spray, short shots and funnel smoke. RN battlecruisers in 1914 carried B&S coincidence instruments (FQ2 9 ft), not stereo. The claim should be re-sourced or downgraded to [UNC].

### Gaps
- No numeric vibration amplitudes or frequencies for any director or rangefinder platform 1900–1980. These are presumably in DTMB reports AD0428719 and the Jasper 1950 report; neither full text could be retrieved.
- No source found for specific vibration complaints on Lion/Queen Mary/Tiger, Hood, Renown/Repulse, Italian cruisers or Spruance (searches returned nothing usable).
- No speed above which rangefinding became "impossible" was found.

---

## 2. Spray and green water at speed into a head or bow sea

### Takeaway
This is the best-documented speed mechanism. Low turret rangefinders and forward turrets were repeatedly lost at about 25–30 kn into a sea. Several navies removed forward-turret rangefinders for this reason. Renown in 1940 had to drop to **20 kn** to keep her forward turrets in action, which gives one clear speed threshold.

### Cited Findings
- Denmark Strait (24 May 1941): the British steamed at 28 kn into the wind. Spray drenched the lenses of Prince of Wales's A-turret 42 ft B&S rangefinder, and B turret's was affected too, so the shorter director-tower rangefinders had to be used. Only 10 of 18 British heavy guns could bear. — [Wikipedia, Battle of the Denmark Strait](https://en.wikipedia.org/wiki/Battle_of_the_Denmark_Strait). Speeds of 28 kn for both sides are also given at [kbismarck, Denmark Strait](https://kbismarck.com/denmark-strait-battle.html)
- Bismarck: "The 10.5-meter base rangefinder in turret 'Anton' was removed in Hamburg during the winter of 1940/41, because its lack of utility due to sea water fogging at high speeds." The foretop rangefinder sat at about 31 m. — [kbismarck, fire control](https://kbismarck.com/controltiri.html)
- Iowa: the turret I rangefinder was removed because it "tended to get blinded by spray" — [Naval Gazing, Fire Control Part 2](https://www.navalgazing.net/Fire-Control-Part-2). It was removed in the 1950s — [Naval Gazing, Pictures Iowa Turret One](https://navalgazing.net/Pictures-Iowa-Turret-One)
- Action off Lofoten (9 Apr 1940), Renown vs Scharnhorst/Gneisenau. Renown **reduced speed to 20 kn to keep her forward turrets in action**. Her course put massive water over the bow, "washing out fire-control optics" and leaking into the turrets through shredded blast bags. Water went down the barrels and boiled off when the breech opened. Scharnhorst's forward turrets flooded through the powder-case ejection ports because she had to train them aft while running. Gneisenau's A turret was put out by water entering through a damaged rangefinder hood. — [Naval Gazing, Norway Part 8](https://www.navalgazing.net/Norway-Part-8)
- Same action: firing was broken off for 20 minutes because of "waves breaking over Renown's forward turrets". Renown fired 230 × 15-in and 1,065 × 4.5-in rounds for 2–3 hits. Scharnhorst fired 182 × 11-in and Gneisenau 54 × 11-in for 2 hits. — [Wikipedia, Action off Lofoten (Garzke 1985; Haarr 2010)](https://en.wikipedia.org/wiki/Action_off_Lofoten)
- Scharnhorst escaping Renown: heavy seas and high speed made the ships ship large amounts of water forward, which put the forward turret out of action. On 1939 trials she showed "a dangerous tendency to ship considerable amounts of water in heavy seas", with flooding that damaged forward turret electrics, which led to the Atlantic bow refit. — [Military Wiki, German battleship Scharnhorst](https://military-history.fandom.com/wiki/German_battleship_Scharnhorst)
- Dunkerque (25 Nov 1939, off Iceland): her bow was "repeatedly submerged" and she slowed to **10 kn** to avoid damage. Insufficient freeboard and weak construction could not be easily corrected. — [Wikipedia, French battleship Dunkerque (Jordan & Dumas)](https://en.wikipedia.org/wiki/French_battleship_Dunkerque)
- KGV (1936) class: almost no sheer (a requirement for zero-elevation fire ahead) made the ships "very wet forward." — [Naval Gazing, The King George V class](https://navalgazing.net/The-King-George-V-class)
- Hood: "At full speed, or in heavy seas, water would flow over the ship's quarterdeck." Her upper-deck 5.5-in guns could be worked in heavy weather, unlike earlier casemates. — [Wikipedia, HMS Hood (Taylor; Burt)](https://en.wikipedia.org/wiki/HMS_Hood)
- KGV (1911): hull casemate 4-in guns were "often unusable in heavy seas" and were removed during the war. — [Wikipedia, KGV (1911)](https://en.wikipedia.org/wiki/King_George_V-class_battleship_(1911))
- Fubuki: the forecastle was raised one deck "to reduce the effect of heavy seas on the forward gun mount." — [Wikipedia, Fubuki-class destroyer](https://en.wikipedia.org/wiki/Fubuki-class_destroyer)
- Iowa (1988): the bow "takes water only occasionally in heavy seas" (prominent sheer). — [USNI 1988, Handling the Battleship](https://www.usni.org/magazines/proceedings/1988/april/handling-battleship)
- Falklands 1914: range-taking was "nearly impossible" for much of the action from funnel smoke, gun smoke and spray. Enemy shorts threw spray over the glasses, and a treatment applied to the glasses "worked well." — [tapatalk forum transcription of the Invincible report](https://www.tapatalk.com/groups/alltheworldsbattlecruisers/viewtopic.php?p=23270) [UNC]

### Inferences
- [INF] A simple threshold model fits the cases. A forward low instrument (turret rangefinder, forward turret sight, destroyer A mount) is lost or blinded when deck wetness at the bow exceeds a threshold. Deck wetness rises steeply with speed into the sea (see section 3, P(wet) = exp(−F²/2m₀)). Anchors:
  - Renown (about 30 kn class, low freeboard) at sea state ~6–7: forward turrets were workable at ≤20 kn and not at higher speed.
  - Big fast battleships at 28 kn into a moderate sea (Denmark Strait, about Beaufort 4–5): the turret rangefinders were lost but the turrets still fought.
  - Dunkerque in an Iceland gale: 10 kn needed just to avoid damage.
- [INF] Suggested game rule: compute wetness index W = (v/v_max)² × sea-state factor × heading factor (1 head, 0.6 bow, 0.2 beam, ~0 quarter/stern) ÷ freeboard factor (wet designs such as KGV, Dunkerque, Hood, Scharnhorst pre-1939 and early destroyers ×1.5; high-sheer ships such as Iowa or Atlantic-bow ships ×0.7). Thresholds on W then drop turret rangefinders (k_optic → 2–∞), then forward-mount local control, then forward turrets.

### Gaps
- No quantitative destroyer case (Fletcher, Tribal, Fubuki A mount unusable above X kn in sea state Y) was found.
- No source for a "Bismarck A turret wet" claim beyond the rangefinder removal.

---

## 3. Pitch, heave and slamming versus speed (encounter frequency)

### Takeaway
Speed into a head sea raises the encounter frequency and the relative bow motion. This sharply increases deck wetness and slamming, so captains slow down. Ship size dominates sustainable speed. Sourced numbers are generic seakeeping figures, not gunnery-specific.

### Cited Findings
- Encounter frequency: ω_e = ω − (V ω²/g) cos χ, with head seas giving cos χ < 0 and so a higher ω_e. Pitch and heave dominate in head seas, roll and sway in beam seas. Captains in head seas reduce speed to avoid green water, high vertical accelerations and slamming. Deck wetness occurs when relative motion exceeds freeboard F: P(wet) = exp(−F²/2m₀), with a consensus limit of P(wet) ≈ 0.05 (5 shipped seas per 100 oscillations). — [Nicholson & Price, IOA paper](https://www.ioa.org.uk/system/files/publications/K%20NICHOLSON%20WG%20PRICE%20EVALUATION%20OF%20THE%20MOTION%20AND%20SPEED%20CHARACTERISTICS%20OF%20A%20SHIP%20IN%20IRREGULAR%20SEAS.pdf) [OCR of formula slightly garbled; standard form confirmed by textbook knowledge]
- Sustainable head-sea speeds (USNI 1983): in sea state 5, a 400-ft frigate makes about 20 kn, a destroyer about 25 kn and a cruiser about 30 kn. In sea state 7, these ships make about 10–17 kn depending on size. A 400-ft frigate can use full power about 40% of the time year-round in the North Atlantic, a 550-ft destroyer about 70%. Unstabilised ships are "often restricted by roll motions" to headings into the sea. Unstabilised missile launchers "often would be inoperable" at high northern latitudes. — [USNI Proceedings 1983, Seakeeping](https://www.usni.org/magazines/proceedings/1983/september/seakeeping)
- Hull flexing in a seaway changes the parallelism of turret roller paths (trunnion tilt), and so pattern size. Hogging and rainbowing are greatest when crossing waves at 90°, bending and twisting at 45°. Any pattern size applies only to "that ship, on that day, at that course relative to the waves." — [NavPers, Accuracy of Shipboard Gun Fire (eugeneleeslover)](https://www.eugeneleeslover.com/USN-GUNS-AND-RANGE-TABLES/ACCURACY-OF-SHIPBOARD-GUN-FIRE.html)
- In excessive motion, US directors used "selected level (or cross-level)," holding one axis at a fixed mid value and firing on gyro contact. — [Naval Gazing, Fire Control Part 2](https://www.navalgazing.net/Fire-Control-Part-2)

### Inferences
- [INF] For the game, pitch rate (which drives laying error for unstabilised or hand-laid guns and optics) scales roughly with ω_e × pitch amplitude. In head seas the amplitude also grows with speed until about resonance. A cheap proxy: multiply the existing sea-state laying error by (1 + 0.5·(v/v_max)·cos²(heading-to-sea)) for head and bow seas, and reduce it for following seas. This is uncalibrated.
- [INF] Slamming mainly matters as a speed cap (ships slow down) and a vibration source (whipping). The game can treat it as a forced speed limit per sea state × hull length, using the USNI 1983 table.

### Gaps
- No gunnery-specific seakeeping criteria with numbers (e.g. gun operability RMS roll or pitch limits, STANAG 4154) were obtained.

---

## 4. Funnel smoke and gas at high power

### Takeaway
At high power, especially with coal plus oil sprays, funnel smoke was a first-order WWI degrader, arguably larger than vibration. It drove turrets into local control, blinded fore-top spotting, and in Lion's original layout made the spotting top "completely unworkable" at high speed. Remedies were moving the funnel or mast, raising funnels, funnel caps, and relocating rangefinders.

### Cited Findings
- Invincible at the Falklands: "The funnel smoke which throughout had caused great inconvenience to the control of fire was now specially bad". The "fore control and P, Q and X turrets [were] seriously obstructed by smoke," and "primary control from 'A' turret and local control from all turrets was reverted to on account of funnels smoking." — [Dreadnought Project, Invincible at the Falklands](https://dreadnoughtproject.org/tfs/index.php/H.M.S._Invincible_at_the_Battle_of_the_Falkland_Islands). Turrets switched to local control three times for 2–3 minutes each, and Q turret could not use its rangefinder for a long period. — [tapatalk transcription](https://www.tapatalk.com/groups/alltheworldsbattlecruisers/viewtopic.php?p=23270) [UNC]
- Falklands control notes: smoke is linked explicitly to over-speed: "the dense volumes of smoke caused by the combined use of coal and oil at a speed above the ship's designed one" would make the weather position impossible for fleets. "Invincible's smoke" was a source of irritation and interruption for Inflexible. — [Dreadnought Project, Notes on the Control of Fire at the Falklands](https://dreadnoughtproject.org/tfs/index.php/Notes_on_the_Control_of_Fire_at_the_Battle_of_the_Falkland_Islands). "The smoke from the battlecruisers was making gunnery very difficult for both sides." — [Dreadnought Project, Battle of the Falkland Islands](https://dreadnoughtproject.org/tfs/index.php/Battle_of_the_Falkland_Islands)
- Lion (original layout) and KGV (1911): hot clinkers and flue gases from the fore funnel made the foremast spotting top "completely unworkable" at high speed (or when the forward boilers were alight), and the upper bridge could be rendered uninhabitable depending on wind. The remedy was to swap the funnel and mast and move the 9 ft Argo rangefinder to the conning tower roof, at a cost of £68,170 for Lion and Princess Royal. — [Wikipedia, Lion-class (Roberts; Burt)](https://en.wikipedia.org/wiki/Lion-class_battlecruiser); [Wikipedia, KGV 1911](https://en.wikipedia.org/wiki/King_George_V-class_battleship_(1911))
- Dogger Bank (1915): the light forces were stationed "not to foul our range with their smoke." Tiger shifted target when "interfered with by smoke." Speeds reached 28.5 kn. Lion fired 243 heavy shells for 4 hits (Corbett). — [naval-history.net, Dogger Bank](https://www.naval-history.net/WW1Battle1501DoggerBank.htm). Tiger fired 355 × 13.5-in for 2 hits. — [Wikipedia, HMS Tiger](https://en.wikipedia.org/wiki/HMS_Tiger_(1913))
- Jutland, run to the south: the wind carried British gun and funnel smoke into their line of fire. — [Naval Gazing, Jutland Part 2](https://navalgazing.net/Jutland-Part-2). Beatty's despatch: the force "increased speed to 25 knots" and formed "on a line of bearing to clear the smoke"; destroyers caused "considerable inconvenience from smoke." — [Beatty letter BCF 01 of 12 June 1916](https://battleofjutlandcrewlists.miraheze.org/wiki/Letter_from_Vice-Admiral_Commanding_Battle_Cruiser_Fleet,_No._B.C.F,_01_of_12th_June_1916)
- Physics: in forward motion, low pressure behind the funnel draws gases down onto the deck aft. Following and cross winds do the same. — [US Patent 2,601,663](https://patents.google.com/patent/US2601663A/en)
- Funnel smoke and engine exhaust caused one-sided heating of rangefinder optics, a primary German error source. — [navweaps tech-078](https://navweaps.com/index_tech/tech-078.php)
- Dunkerque received a funnel cap in 1937. No reason is stated in the source. — [Wikipedia, Dunkerque](https://en.wikipedia.org/wiki/French_battleship_Dunkerque)

### Inferences
- [INF] Model funnel smoke as a p_obscured contribution for **own** directors and tops. It is a function of (i) fuel era (coal ≫ mixed coal/oil sprayed > oil > 1930s oil with caps and high funnels ≈ small), (ii) power fraction (strongly nonlinear above about 90% of design, "above designed speed"), (iii) relative wind: worst when the relative wind blows from the funnel toward the director or top, or down the line of fire toward the target, and (iv) layout (a mast abaft the fore funnel is the Lion case). Also add smoke from ships ahead in line (the Inflexible-behind-Invincible case) and from own destroyers. Suggested values: WWI coal battlecruisers at full power with an adverse wind give p_obscured 0.2–0.4 for the fore top. Turret control can then take over at worse accuracy, which matches the Invincible local-control episodes.
- [INF] The RN's 1914 Falklands and 1915 Dogger Bank hit rates (Lion about 1.6%, Tiger about 0.6%) were at 25–28.5 kn with heavy smoke. They cannot be separated from range, inexperience and target behaviour, so they should not be used to calibrate a speed term on their own.

### Gaps
- No WWII or later case of funnel smoke materially blinding a director was found, though this is plausibly real for destroyers. No data on 1930s funnel caps' effectiveness.

---

## 5. Own speed in the ballistic solution, own-speed inputs and log errors

### Takeaway
Own speed enters the solution only through the velocity the shell inherits at the muzzle. NavPers resolves it into along-LOS (range) and across-LOS (deflection) components, which partly cancel against the ship-made apparent wind. The correction is computed automatically from the log (Forbes in the RN WWI, pitometer in the USN WWII). Errors therefore scale with **log error × time of flight** and are small unless the log is badly wrong or speed is changing.

### Cited Findings
- "Motion of the firing ship after the projectile has left the gun can in no way affect the trajectory." Firing ahead adds range, astern subtracts, and a beam component deflects the shot in the direction of motion. At 10 kn dead ahead the projectile gains 16.89 ft/s, a vacuum range increase of 16.89·Tf/3 yards. Worked example: 18 kn at relative bearing 300° gives about 9.0 kn along the LOS (over) and 15.6 kn across (right). The combined gun ballistic correction for that case is **add 368 yd, right 26 yd** (gun motion plus wind and related terms). Ship motion creates an apparent headwind that partly cancels; true versus apparent wind tables must not double-count it. — [NavPers Naval Ordnance and Gunnery, Ch. 17-C](https://eugeneleeslover.com/USNAVY/CHAPTER-17-C.html)
- USN rangekeeper: own speed So is "received automatically from the pitometer log" and resolved with generated relative bearing into Yo and Xo. — [NavPers Ch. 19-F](https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html). For Iowa's Mk 8: pitometer speed, gyro course, stable-vertical roll/pitch and anemometer relative wind are inputs. — [Wikipedia, Armament of the Iowa class](https://en.wikipedia.org/wiki/Armament_of_the_Iowa-class_battleship); "Iowa's own movement is fully compensated for." — [Naval Gazing, Fire Control Part 2](https://www.navalgazing.net/Fire-Control-Part-2)
- RN Dreyer tables: own speed came "From Forbes log", "continuous, manually fed to Dumaresq" (early Mk III station). Change-of-bearing gear on Mk II/III was limited to ±15°/min. The Dreyer assumed range changed linearly, and other dynamics needed manual correction. No own-speed scale limit is stated. — [Dreadnought Project, Handbook of Dreyer Fire Control Tables 1918](https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php)
- The manually set Dumaresq "could use only a number of pre-set speeds." The Dreyer Mk I was "incapable of adapting to rapid speed changes." — [Jutland 1916, Range Finding](https://www.jutland1916.com/tactics-and-technologies-4/range-finding-and-course-plotting-2)
- Pitometer logs are inaccurate at low speed (small pressure difference). They were often connected directly to warship fire control. — [Wikipedia, Pitometer log](https://en.wikipedia.org/wiki/Pitometer_log)
- A Fletcher full-power trial in 1964 gave "Pit Log showed about 36 knots" at more than 400 rpm. — [USS Kidd, How fast could she go](https://www.usskidd.com/how-fast-could-she-go/)

### Inferences
- [INF] Size of the effect. A log error δv (kn) gives a range error of about δv × 0.563 yd/s × (Tf/3) at the vacuum level along the line of fire (from the NavPers 16.89 ft/s per 10 kn figure). With δv = 0.5 kn and Tf = 30 s, that is about 3 yd: negligible. The relative-motion (rangekeeping) error from δv is larger: δv × Tf ≈ 0.5 kn × 30 s ≈ 8 yd of target-motion prediction error, plus range-rate bias accumulating between spots. **Conclusion: in a model, own-speed ballistic error is negligible next to rangefinding and dispersion, except** (a) with WWI Dumaresq/Dreyer during own speed or course changes (no automatic own-motion input on early marks, preset speeds), and (b) during acceleration or turns, when logs lag. Model this as extra range-rate error proportional to |dv/dt| and |turn rate| for pre-1916 RN-type systems. It is near zero for Ford/Mk 8/Argo-type systems.
- [INF] Pitometer error was likely ~1–2% of speed, but this is unsourced. I found no sourced magnitude for Forbes or pitometer log error in service.

### Gaps
- No sourced log error figures (Forbes, pitometer, Chernikeeff).
- No sourced maximum own-speed setting for any rangekeeper or Dreyer.

---

## 6. Doctrine: did navies slow down to shoot better?

### Takeaway
I found no explicit doctrine with numbers saying "reduce to N knots for better gunnery." Practice speeds were moderate (USN battle practice 1914: 15 kn). Battle accounts show ad hoc slowing for seakeeping (Renown 20 kn), and Sturdee eased to 20–24 kn largely to let cruisers close. Contemporary writers traded speed against fire superiority rather than treating speed as an accuracy penalty.

### Cited Findings
- USN battle practice (Marine Review, Aug 1914): the firing division ran at **15 kn** for 36 minutes. Scoring weighted rate of fire as heavily as accuracy. — [Maritime History of the Great Lakes, Marine Review 1914 p. 292](https://images.maritimehistoryofthegreatlakes.ca/125026/page/9)
- Falklands: battlecruiser speed was "eased to 24 knots" and later "to 20 knots at 11.15 a.m. to enable the other cruisers to get into station". The stated reason was formation, not gunnery. — [naval-history.net, Falklands (Sturdee despatch)](https://naval-history.net/WW1Battle1412Falklands.htm)
- Renown, 9 Apr 1940: reduced to 20 kn to keep her forward turrets in action. — [Naval Gazing, Norway Part 8](https://www.navalgazing.net/Norway-Part-8)
- USNI 1907: "Thirty percent fire superiority may be considered as equalizing 3 knots speed". Using half of one's prospective gunners in the stokeholds during high-speed pursuit was cited as a disadvantage (a crew-allocation cost of speed, coal era). — [USNI Proceedings 1907, Value of Speed in Tactics and Strategy](https://www.usni.org/magazines/proceedings/1907/october/value-speed-tactics-and-strategy)
- Surigao Strait (1944): the US battle line fired at 15 kn, the cruisers at 10 kn. — [navweaps tech-079, Czarnecki](https://www.navweaps.com/index_tech/tech-079.php)
- New Jersey (1980s): standing order that rudder angle plus ordered speed should not exceed 30. — [USNI 1988, Handling the Battleship](https://www.usni.org/magazines/proceedings/1988/april/handling-battleship)

### Inferences
- [INF] The pattern is that battle lines fought at 15–20 kn when they had a choice (Surigao, practice), and fast forces fought at 25–28.5 kn when chasing or being chased (Falklands, Dogger Bank, Jutland run to the south, Denmark Strait). The second group shows the documented smoke, spray and vibration failures. A game can therefore make "fight at best gunnery speed" versus "full speed" a real player choice. The penalty should mostly come from smoke (coal era) and spray (head sea), with vibration secondary.
- [INF] Coal-era crew effect: at full power, extra stokers were drawn from gun crews. This could be modelled as a small rate-of-fire penalty for coal ships at more than 90% power (USNI 1907, qualitative).

### Gaps
- No RN, IJN or German doctrinal statement quantifying a best gunnery speed was found. Friedman's *Naval Firepower* and Brooks probably discuss this but were not accessible.

---

## 7. Heel when turning at speed

### Takeaway
Heel in high-speed turns is real but rarely quantified in open sources. Stable-vertical systems (USN Mk 41 and similar, WWII) corrected level and cross-level continuously. Earlier hand-laid or director systems relied on firing at a level moment.

### Cited Findings
- Iowa heels "noticeably only during tight turns at highest speeds". At 15 kn with 35° rudder, a 180° turn takes about 2 minutes with a 760 yd tactical diameter. No heel angle is given. — [USNI 1988, Handling the Battleship](https://www.usni.org/magazines/proceedings/1988/april/handling-battleship)
- Fletcher tactical diameter: 950 yd at 30 kn. — [destroyerhistory.org, Fletcher class](https://destroyerhistory.org/fletcherclass/index.asp?pid=200)
- The Mk 8 rangekeeper takes roll and pitch from the Mk 41 stable vertical. The rangekeeper handles deck-tilt and trunnion-tilt corrections. — [Wikipedia, Iowa armament](https://en.wikipedia.org/wiki/Armament_of_the_Iowa-class_battleship); [NavPers Ch. 19-F](https://eugeneleeslover.com/USNAVY/CHAPTER-19-F.html)
- After Jutland, British equipment gained gyro firing gear so guns fired only when the ship's roll was neutral. — [Jutland 1916, Range Finding](https://www.jutland1916.com/tactics-and-technologies-4/range-finding-and-course-plotting-2)
- Director firing (all guns together) reduced the effect of roll on shell dispersion. — [Wikipedia, KGV 1911 (Brooks 2005)](https://en.wikipedia.org/wiki/King_George_V-class_battleship_(1911))

### Inferences
- [INF] Steady heel in a turn behaves as a constant cross-level offset. With a stable vertical it is corrected. Without one, director-firing ships fire at the "level" gyro contact, which costs rate of fire rather than accuracy while the ship is heeled. The bigger turn penalty is rangekeeping (own course changing; the Dumaresq and Dreyer could not follow rapid own changes) and transient roll on coming out of the turn. Suggested model: while turning faster than about 1°/s, pre-1916 systems take ×1.5–2 range-rate error and reduced rate of fire. WWII stable-vertical plus rangekeeper systems take ~×1.1.
- [INF] Typical heel values for large ships in full-speed, hard-over turns are reportedly a few degrees (battleships) to around 10°+ (destroyers). This comes from general naval-architecture knowledge, **not sourced here**.

### Gaps
- No sourced heel angles for any warship class in a high-speed turn.
