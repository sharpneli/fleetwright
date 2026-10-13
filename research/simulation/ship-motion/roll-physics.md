# Warship roll (plus pitch, yaw, heave) as a gun platform, 1890–1990: physics and data
Status: draft    Updated: 2026-10-12    Request: -    Appendix to: [ship-motion-gunnery-research.md](ship-motion-gunnery-research.md)

Conventions:
- Untagged bullets are sourced; the URL sits next to each fact.
- **[INFERRED]** marks my own calculation or reasoning.
- **[UNCERTAIN]** marks thin, OCR-garbled or conflicting sources.
- Unit conversions I made are tagged [INFERRED].
- In my calculations g = 9.81 m/s². A roll period T is the full cycle (port, starboard, port) unless stated otherwise.

Research limit: the shared web-search budget ran out mid-task (200 searches per turn). Several planned lookups were never made: hmshood.org, Japanese-language Yamato roll data, D.K. Brown excerpts, a pitch/heave formula source, and the Southampton (wumtia) Brown papers, which returned 403. Those items are listed under Gaps.

---

## Q1. Roll-period formula and the radius-of-gyration coefficient for warships

### Takeaway
Use T = 2π·k_eff/√(g·GM) with k_eff = κ·B, where k_eff includes added inertia.

| Ship type | κ |
|---|---|
| Battleships and cruisers | 0.33–0.37 |
| Destroyers and frigates | 0.35–0.40 (up to ~0.45–0.5 at very low GM) |

In metric units this is T ≈ C·B/√GM with C ≈ 2.0·κ, so C ≈ 0.66–0.80. Back-calculating κ from sourced warship (B, GM, T) triples gives a tight band of 0.30–0.39. Merchant and class-society defaults (0.39–0.45) run higher than warship data.

### Cited findings
**Formula forms**
- Basic form: T ≈ 2·√((k_xx² + a_xx²)/GM), equivalently T = 2·k·B/√GM. Most formulations fold the factor 2 into the coefficient — [Krata/TU Delft IMDC 2024, "On empirical methods to predict the rolling period of ships"](https://proceedings.open.tudelft.nl/imdc24/article/download/881/893/1240)
- IMO Intact Stability Code / JSRA form: T = 2·C·B/√GM with C = 0.373 + 0.023·(B/d) − 0.043·(L_wl/100), all in metres. r² is only 0.59 — [IMDC 2024](https://proceedings.open.tudelft.nl/imdc24/article/download/881/893/1240); [Wawrzyński & Krata 2016](https://yadda.icm.edu.pl/baztech/element/bwmeta1.element.baztech-0beb5d56-31b9-4ca9-87fb-dd9da8d76f19/c/Wawrzynski_Krata_Method_54_4_2016.pdf)
- Doyère formula: T = c·√((B² + 4·VCG²)/GM).
  - Five naval vessels in several loadings gave c = 0.26–0.32.
  - Kato's formula uses a constant of 0.172 for navy ships, against 0.125 for passenger/cargo ships, so navy ships have a relatively larger gyradius for their beam.
  - Source: [IMDC 2024](https://proceedings.open.tudelft.nl/imdc24/article/download/881/893/1240)

**Class-society default roll factors**

| Society / reference | Roll factor k |
|---|---|
| Lloyd's Register | 0.41 |
| ABS | 0.40 |
| DNV | 0.45 |
| ClassNK | 0.40 |
| Bureau Veritas | 0.39 |
| Lewis 1989 (PNA) | 0.36, range 0.29–0.43 |

- The "beam method" uses k_B = c·√(B² + D²). A solid beam gives c = 0.289 and a thin tube 0.397; Laurenson gives 0.33–0.39 and Peach 0.30 — [IMDC 2024](https://proceedings.open.tudelft.nl/imdc24/article/download/881/893/1240)
- The paper's reference frigate (Appendix A): B = 13.1 m, k_xx = 5.9 m, a_xx = 3.0 m, GM = 0.5 m, T = 18.1 s (full load) — [IMDC 2024](https://proceedings.open.tudelft.nl/imdc24/article/download/881/893/1240)

**US Navy practice**
- The US Navy damage-control training text gives the roll-period formula in terms of B (ft), GM (ft) and T (s). The formula itself is an image I could not read.
- It describes timing a full roll cycle with a stopwatch at 8–10 kn after cycling the rudder.
- It describes the qualitative regimes: stiff and fast at large GM; "hangs" at the end of the roll at very small GM; lolls at slightly negative GM.
- Source: [FAS/SWOS DCA lesson](https://man.fas.org/dod-101/navy/docs/swos/dca/stg4-06.html)
- NAVSEA 1974 gives the approximate roll natural frequency as ω ≈ f(√(GM/B)). The constant is OCR-garbled — [DTIC ADA017655](https://apps.dtic.mil/sti/pdfs/ADA017655.pdf) [UNCERTAIN]

### Warship (B, GM, T) triples and the back-calculated κ = T·√(g·GM)/(2π·B) [INFERRED]

| Ship | B | GM | T (sourced) | κ | Source |
|---|---|---|---|---|---|
| Nelson class BB (deep load) | 32.3 m | 3.1 m (10.2 ft) | 11.2–13.6 s, "quick roll" | 0.30–0.37 | [Wikipedia Nelson class](https://en.wikipedia.org/wiki/Nelson-class_battleship) |
| DLG-26 Belknap CG | 54.4 ft | 5.58 ft | 8.86 s ("est.") | 0.347 | [DTIC ADA002233](https://apps.dtic.mil/sti/pdfs/ADA002233.pdf) |
| DE-1078 Knox FF | ~46.3 ft (OCR) | 4.47 ft | 8.39 s ("est.") | 0.346 | [DTIC ADA002233](https://apps.dtic.mil/sti/pdfs/ADA002233.pdf) [UNCERTAIN beam] |
| Generic RN frigate/destroyer (Fig. 3 ship) | 14 m | 1.5 m | ω_n 0.7 rad/s ≈ 9 s | 0.39 | [IMarEST, stabiliser paper](https://library.imarest.org/record/8618/files/v32b1p13a.pdf) |
| Reference frigate (full load) | 13.1 m | 0.5 m | 18.1 s | 0.49 | [IMDC 2024](https://proceedings.open.tudelft.nl/imdc24/article/download/881/893/1240) |
| Captain-class DE (WWII), after modification | — | — | 8 → 8½ s | — | [IMarEST Brown paper](https://library.imarest.org/record/8299/files/v35b3p12a.pdf) |

Large carriers, for scale ([DTIC ADA071890](https://apps.dtic.mil/sti/pdfs/ADA071890.pdf)):
- CVA-59 Forrestal (B = 129 ft): "period of maximum roll response" about 20–25 s.
- CVAN-68: about 20 s.
- These are response periods, not natural periods.

### Inferences
- **[INFERRED]** The Delft frigate's κ ≈ 0.49 comes with GM of only 0.5 m. Added inertia matters relatively more when GM is small, so κ creeps up as GM/B falls.
- **[INFERRED]** Suggested game rule:
  `κ = 0.34 + 0.04·clamp((0.08 − GM/B)/0.06, 0, 1)`
  This gives about 0.34 for stiff capital ships (GM/B ≈ 0.08–0.11) and about 0.38 for tender ships (GM/B ≈ 0.02–0.04). Add a +0.02 shift for heavy topweight (high masts or AA fits), which raises the gyradius.
- **[INFERRED]** In metric units T ≈ 2.006·κ·B/√GM. Rule-of-thumb C (= 2.006κ): 0.68–0.70 for battleships, 0.70–0.80 for escorts.

**Predicted periods with κ = 0.35 / 0.40** [INFERRED: GMs from the sources in Q2, beams from Wikipedia]

| Ship | κ = 0.35 | κ = 0.40 |
|---|---|---|
| Royal Sovereign (1.10 m) | 15.3 s | 17.5 s |
| Dreadnought (1.71 m) | 13.4 s | 15.3 s |
| QE (1.98 m) | 13.8 s | 15.7 s |
| Revenge, unbulged (1.04 m) | 18.6 s | 21.2 s |
| Hood 1920 (≈1.28 m) | 19.7 s | 22.6 s |
| Kaiser/König (2.59 m) | 12.7 s | 14.5 s |
| Derfflinger (2.6 m) | 12.6 s | 14.4 s |
| Nelson (3.1 m) | 12.9 s | 14.7 s |
| KGV (2.16 m) | 15.0 s | 17.1 s |
| North Carolina (2.53 m) | 14.6 s | 16.6 s |
| Iowa (2.82 m) | 13.8 s | 15.8 s |
| Bismarck (4.09 m) | 12.5 s | 14.3 s |
| Yamato (2.99 m) | 15.8 s | 18.1 s |
| Vittorio Veneto (1.61 m) | 18.1 s | 20.7 s |
| Richelieu (2.83 m) | 13.8 s | 15.8 s |
| German Type 1934 DD, full load (0.79 m) | 8.9 s | 10.2 s |

- The Nelson check (sourced 11.2–13.6 s against a predicted 12.9 s at κ = 0.35) supports κ ≈ 0.33–0.35 for battleships.
- So the "8–14 s for pre-dreadnoughts" assumption is too short for low-GM British ships (GM ≈ 1.0–1.2 m gives roughly 14–18 s) and about right for stiff German ships.

### Gaps
- No sourced natural roll periods were found for any named pre-dreadnought, for Dreadnought, QE, Hood, KGV, Iowa, Bismarck or Yamato, or for Fletcher, Tribal, Fubuki or Gearing.
- A 1931 USNI article uses a generic "14 s" ship period as its example ([USNI 1931 "Why they roll"](https://cdn02.usni.org/magazines/proceedings/1931/november/why-they-roll)), but it names no ship.
- White's *Manual of Naval Architecture* (HathiTrust) almost certainly tabulates Victorian battleship periods, but I could not access the text.

---

## Q2. Sourced GM and roll data for named warships

### Takeaway
GM values are plentiful; measured roll periods are rare.

Typical GM bands:

| Group | GM |
|---|---|
| British capital ships, deliberately low | ~1.0–2.0 m |
| German WWI and Bismarck, deliberately high | 2.3–4.1 m |
| US/Japanese WWII battleships | 2.5–3.0 m |
| Destroyers | 0.6–1.7 m |

Observed extreme rolls run from about 20° each way (1880s ironclads) to 40° each way (Flower corvettes) and 70–75° (US destroyers in Typhoon Cobra).

### Cited findings: GM (and roll where known)

**Pre-dreadnoughts**
- Royal Sovereign class: GM 3.6 ft (1.1 m). Hood (1891) had 4.1 ft (1.2 m), which made her roll period "around 7%" shorter and her gunnery less accurate. White chose a high GM to minimise rolling and thought bilge keels unnecessary — [Wikipedia RS class](https://en.wikipedia.org/wiki/Royal_Sovereign-class_battleship)
  - **[INFERRED]** √(3.6/4.1) = 0.937, i.e. 6.3% shorter, consistent with the stated 7%.
- Resolution rolled heavily in December 1893, giving the class the nickname "Rolling Ressies." Repulse had bilge keels fitted while fitting out, which "conclusively demonstrated their effectiveness" in comparative trials. Bilge keels were fitted to all the others in 1894–95 — [Wikipedia RS class](https://en.wikipedia.org/wiki/Royal_Sovereign-class_battleship)
- Brassey-era account of the Royal Sovereigns: they "at first rolled very badly … completely cured by the fitting of bilge keels" and afterwards were "everything that could be desired in the way of being steady gun platforms" — [*The British Battle Fleet*, Vol. 2, Gutenberg](https://gutenberg.org/cache/epub/75617/pg75617-images.html)
- Same source on other ships:
  - Collingwood (Admiral class) "rolled 20 degrees each way" in a considerable beam swell. "This does not make it appear as if the Admiral class will be very steady gun-platforms."
  - Conqueror and Hero "roll a great deal."
  - Arethusa-type cruisers rolled "so heavily when the sea was abeam or abaft" that accurate shooting was impossible.
  - The Mersey class were "steady gun platforms."
  - Barnaby treated 10° from vertical as "by no means a heavy roll" for a 70–75 ft beam ship.
  - Source: [Gutenberg](https://gutenberg.org/cache/epub/75617/pg75617-images.html)
- Imperatritsa Mariya (Russian dreadnought): designed GM 1.76 m — [Wikipedia](https://en.wikipedia.org/wiki/Imperatritsa_Mariya-class_battleship)

**Dreadnoughts and battlecruisers, WWI era**

| Ship / class | GM | Other sourced data | Source |
|---|---|---|---|
| Dreadnought (1906) | 5.6 ft (1.7 m), deep load | — | [Wikipedia](https://en.wikipedia.org/wiki/HMS_Dreadnought_(1906)) |
| Queen Elizabeth class | 6.5 ft (2.0 m), deep load | — | [Wikipedia](https://en.wikipedia.org/wiki/Queen_Elizabeth-class_battleship) |
| Revenge class | 3.4 ft (1.0 m) deep, unbulged; 5.1 ft (1.6 m) bulged; Royal Oak 5.5 ft (1.7 m) after 1922–24 | "Reports had been received from the bulged ships of excessive rolling." AEW tested bulge forms "in conjunction with improved bilge keels." | [Wikipedia](https://en.wikipedia.org/wiki/Revenge-class_battleship) |
| Hood (1920) | 4.2 ft (1.3 m), deep load | Described as "a steady gun platform" | [Wikipedia](https://en.wikipedia.org/wiki/HMS_Hood) |

- gwpda (citing Friedman):
  - Revenge and Hood GM ≈ 1.0 m; Moltke 3 m; Seydlitz 3.12 m.
  - Pre-WWI British dreadnoughts ≈ 1.4–1.8 m; German dreadnoughts 2.3–2.6 m.
  - Nassau had a "fast period of roll" resonant with North Sea waves.
  - Source: [gwpda.org](http://www.gwpda.org/naval/gmdefn.htm). The 1.0 m Hood figure conflicts with Wikipedia's 1.3 m deep load, probably a different loading condition [UNCERTAIN].

| German ship / class | GM | Behaviour in turns and at sea | Source |
|---|---|---|---|
| Nassau class | "large" (heavy wing turrets) | Roll period "coincide[d] with that of the average North Sea swell"; "motion was quite stiff", "severe rolling"; bilge keels added later "helped to reduce the rolling problem"; up to 70% speed loss at hard rudder | [Wikipedia](https://en.wikipedia.org/wiki/Nassau-class_battleship) |
| Kaiser class | 2.59 m | "Very stiff"; 8° heel at hard rudder | [Wikipedia](https://en.wikipedia.org/wiki/Kaiser-class_battleship) |
| König class | 2.59 m | 8° heel, 66% speed loss at hard rudder | [Wikipedia](https://en.wikipedia.org/wiki/K%C3%B6nig-class_battleship) |
| Derfflinger | 2.6 m as built | Design 5 at 2.7–2.9 m was rejected as too high because "the fast roll period would have degraded the gunners' ability to aim"; "gentle motion"; 11° heel, 65% speed loss at hard rudder | [Wikipedia](https://en.wikipedia.org/wiki/Derfflinger-class_battlecruiser) |
| Seydlitz | (3.12 m per gwpda) | "Gentle motion"; 9° heel, 60% speed loss | [Wikipedia](https://en.wikipedia.org/wiki/SMS_Seydlitz) |
| Moltke class | — | 9° heel, 60% speed loss; "gentle movement even in heavy seas" | [Wikipedia](https://en.wikipedia.org/wiki/Moltke-class_battlecruiser) |
| Von der Tann | — | 8° heel at hard rudder; "good sea boat with gentle motion" | [Wikipedia](https://en.wikipedia.org/wiki/SMS_Von_der_Tann) |

**WWII battleships**
- GM table in Garzke and Dulin via navweaps (GM in ft; GM/B for Bismarck is 0.1135):

  | Ship | GM (ft) | GM (m) [INFERRED] |
  |---|---|---|
  | Bismarck | 13.43 | 4.09 |
  | Yamato | 9.81 | 2.99 |
  | Richelieu | 9.28 | 2.83 |
  | Iowa (1943) | 9.26 | 2.82 |
  | Anson (1945) | 7.20 | 2.19 |
  | Vittorio Veneto | 5.27 | 1.61 |

  Source: [navweaps INRO Bismarck](https://navweaps.com/index_inro/INRO_Bismarck.php)
- Garzke and Dulin also give a Bismarck metric table: GM 3.60 m (light), 3.55 m (standard), 4.00 m (full), 4.40 m (capacity); ranges of stability 65°/59°/55°/53°.
  - They call her "unusually (perhaps excessively) stable" and "a lively gun platform", with roll–yaw coupling in following seas.
  - They give "5–10% of beam" as the good-design GM guideline.
  - Source: [navweaps INRO Bismarck](https://navweaps.com/index_inro/INRO_Bismarck.php)
- Navy General Board, secondary source:

  | Ship | GM | Characterisation |
  |---|---|---|
  | Bismarck | 13.4 ft | "jerky roll" |
  | Revenge | 3.4 ft | "too low" |
  | KGV | 7.1 ft | "steady gunnery platform" |
  | Vanguard | 8.2 ft | "steady gunnery platform" |

  Source: [navygeneralboard.com](https://www.navygeneralboard.com/?p=4021)
- North Carolina: GM 8.31 ft (2.53 m) at 42,329 t combat displacement — [Wikipedia NC class](https://en.wikipedia.org/wiki/North_Carolina-class_battleship)
- Iowa GM by loading — [navalgazing](https://navalgazing.net/Did-Iowa-Move-Sideways-During-a-Broadside):
  - 4.88 ft at light displacement.
  - 7.68 ft (2.34 m) at 56,314 t optimum battle displacement.
  - These conflict with navweaps' 9.26 ft; the loading conditions differ [UNCERTAIN].
- Iowa in heavy seas "will roll gently and pitch slightly"; she heels noticeably "only during tight turns at highest speeds" — [USNI 1988 "Handling a battleship"](https://www.usni.org/magazines/proceedings/1988/april/handling-battleship)
- Bismarck class: "only slight pitching and rolling, even in the heavy seas of the North Atlantic"; heeled only 3° with the rudder hard over (65% speed loss) — [Wikipedia Bismarck class](https://en.wikipedia.org/wiki/Bismarck-class_battleship)
- Iowa in the "stiff/tender" framing: Slade notes fine lines gave "slow, easy motion in heavy weather" — [navweaps tech-009](https://navweaps.com/index_tech/tech-009.php)
- Yamato: GM 2.6 m (existing project note), against 9.81 ft (2.99 m) in the navweaps table [UNCERTAIN: loading condition]. Capsized after rolling to about 120° — [Wikipedia Yamato](https://en.wikipedia.org/wiki/Japanese_battleship_Yamato)

**Cruisers and other German ships**
- Admiral Hipper class: 14° heel with the rudder hard over at high speed; "gentle motion" — [Wikipedia](https://en.wikipedia.org/wiki/Admiral_Hipper-class_cruiser)
- Deutschland class: 13° heel at hard rudder; "slight roll" — [Wikipedia](https://en.wikipedia.org/wiki/Deutschland-class_cruiser)
- Scharnhorst class: more than 10° heel at hard rudder, up to 13° on trials — [Wikipedia](https://en.wikipedia.org/wiki/Scharnhorst-class_battleship)
- Belknap (DLG-26): 7,922 t, B 54.4 ft, KG 20.02 ft, GM 5.58 ft, estimated roll period 8.86 s — [DTIC ADA002233](https://apps.dtic.mil/sti/pdfs/ADA002233.pdf)
- DLG-9 class: B 51.2 ft, 5,876 t, GM 5.58 ft uncorrected and 4.70 ft after free-surface correction (a 16% loss to free surface) — [DTIC ADA017655](https://apps.dtic.mil/sti/pdfs/ADA017655.pdf)

**Destroyers and escorts**
- German Type 1934: GM 0.79 m at full load and 0.6 m at half load.
  - "Top-heavy in service"; about 30% of fuel had to be kept as ballast.
  - Fitted with active stabilisers, replaced by bilge keels in 1938–39.
  - Source: [Wikipedia](https://en.wikipedia.org/wiki/Type_1934_destroyers)
- Knox (DE-1078): GM 4.47 ft (1.36 m), estimated roll period 8.39 s — [DTIC ADA002233](https://apps.dtic.mil/sti/pdfs/ADA002233.pdf)
- Farragut class (1934): "double the GM" of the Wickes and Clemson flush-deckers, but "considered unstable in heavy weather and in turns."
  - After about 500 tons of wartime additions they were more top-heavy.
  - The court of inquiry found their stability "materially less than other destroyers."
  - Source: [Wikipedia Farragut class](https://en.wikipedia.org/wiki/Farragut-class_destroyer_(1934))
- Typhoon Cobra (December 1944):
  - Surviving destroyers reported rolls of 70° or more: Hull 70°, Dewey 75°, Aylwin 70°, Hickox 70°.
  - Spence "hung at 50°" and capsized; lost ships rolled to leeward 50–80°.
  - Spence had about 10,000 gal of oil and no salt-water ballast.
  - Sources: [USNI 1956 discussion (Nimitz letter)](https://www.usni.org/magazines/proceedings/1956/january/discussions-comments-and-notes); [Wikipedia Typhoon Cobra](https://en.wikipedia.org/wiki/Typhoon_Cobra)
  - Escort carrier Kwajalein rolled 39°; Langley rolled through 70° — [Wikipedia Typhoon Cobra](https://en.wikipedia.org/wiki/Typhoon_Cobra)
- Flower-class corvettes: "80-degree rolls, 40 degrees each side of upright, being fairly common"; "would roll on wet grass" — [Wikipedia](https://en.wikipedia.org/wiki/Flower-class_corvette)
- Captain-class DEs: bilge keels deepened from 18 to 24 in and lengthened, and weight added to lower GM and raise inertia (period 8 → 8½ s). Maximum roll was "almost halved" in side-by-side trials — [IMarEST, D.K. Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf)
- Tribal (1936), Fletcher, Gearing, Fubuki: the Wikipedia pages carry no GM or roll data.
  - Fletcher was widened 18 in after the top-heavy Sims class needed lead ballast — [Wikipedia Fletcher](https://en.wikipedia.org/wiki/Fletcher-class_destroyer)
  - Fubuki's 1935–37 rebuild added 40 t of ballast and took standard displacement from 1,750 to 2,050 t; it also reduced the bridge, shortened the funnels and cut torpedo reloads from 9 to 3 — [Wikipedia Fubuki](https://en.wikipedia.org/wiki/Fubuki-class_destroyer)

### Inferences
- **[INFERRED]** GM/B ratios: about 0.05 for British WWI capital ships (Dreadnought 1.7/25.0 = 0.068; Revenge 1.04/27 = 0.039); about 0.09 for German WWI ships; 0.07–0.09 for WWII US, Japanese and French ships; 0.11 for Bismarck; 0.05 for Vittorio Veneto; 0.07 for Type 1934 and ~0.10 for Knox and Belknap.
- **[INFERRED]** A designer GM that reaches GM/B > 0.10 should be tagged "stiff/lively."
- **[INFERRED]** GM/B < 0.04 should be tagged "tender: steady platform, low damage margin."

### Gaps
- No sourced GM or period for Iron Duke (the Wikipedia page is silent), Littorio (only via VV in the navweaps table), Tribal, Fletcher, Gearing, Fubuki, Black Swan, or MTBs.
- No MTB or PT data was found at all.

---

## Q3. Roll amplitude vs sea state, resonance, damping, heading, synchronism

### Takeaway
Roll is a lightly damped resonance. Amplitude peaks when the encounter wave period is close to T_roll (heaviest rolling between ratio 0.9 and 1.1).

Long-period big ships roll little in ordinary seas: a CVA rolls about 3° significant in SS3 and about 17° in SS6. Destroyer and frigate-sized ships (T ≈ 8–10 s) sit inside the energetic band of SS4–6 seas, with about 12° single amplitude unstabilised in SS5.

The US Navy used about 8° roll as a destroyer operability limit and 5° significant as a desirable SS5 design criterion.

### Cited findings
**USNI 1931, "Why they roll"** ([USNI 1931](https://cdn02.usni.org/magazines/proceedings/1931/november/why-they-roll))
- Heaviest rolling occurs when the ratio of ship period to apparent wave period is 0.9–1.1.
- Unresisted amplitude grows by (π/2)·wave slope per half-wave.
- Damping per swing: θ₀ − θ₁ = b·θ₀² (Froude's quadratic decrement).
- Equilibrium amplitude: b·θ² = (π/2)·slope.
- Example: a ship of 14 s period needs waves about 1,000 ft long to synchronise. Waves under 400 ft "hardly affect" a ship of 60 ft beam or more.
- Rolls of 20–35° a side make serving guns "quite a problem."

**Carrier motions, NAVSEC 1977** ([DTIC ADA071890](https://apps.dtic.mil/sti/pdfs/ADA071890.pdf))

Significant values, maximum over headings, at 20–25 kn. Worst roll heading 45° (quartering-ish in that convention); worst pitch heading 135°.

| CVA-59 Forrestal | SS2 (3.0 ft) | SS3 (4.9 ft) | SS4 (6.5 ft) | SS5 (10.2 ft) | SS6 (16.9 ft) |
|---|---|---|---|---|---|
| Roll (°) | 0.70 | 2.74 | 4.75 | 8.16 | 17.24 |
| Pitch (°) | 0.08 | 0.20 | 0.35 | 0.94 | 2.52 |
| Yaw (°) | 0.35 | 0.61 | 0.83 | 1.33 | 2.64 |

- CVAN-68 in SS6: roll 11.7°, pitch 2.15°, yaw 2.10°.
- CVA-59 in SS6: roll velocity 5.32°/s, roll acceleration 1.65°/s², yaw velocity 0.73°/s, pitch velocity 1.77°/s.

**Other sourced amplitudes**
- RN frigate/destroyer: about 25° out-to-out (±12.5°) unstabilised at its 0.7 rad/s natural frequency in about SS5 — [IMarEST stabilisers](https://library.imarest.org/record/8618/files/v32b1p13a.pdf)
- NAVSEA 1974: example criterion of a "5 degree significant vertical-to-out roll angle" in Sea State 5 (H_s 12 ft) at 15–25 kn — [DTIC ADA017655](https://apps.dtic.mil/sti/pdfs/ADA017655.pdf)
- DTIC study of 17 frigates and destroyers (H_s 3.5 m, 30 kn):
  - Operability limits: 8° roll, 3° pitch, 0.4 g vertical acceleration at the bridge, 30 wetnesses/hr, 20 slams/hr.
  - Anti-roll fins cut roll by an average of 64% in beam seas and 66% at the worst heading.
  - Source: [DTIC ADA159804](https://apps.dtic.mil/sti/pdfs/ADA159804.pdf)
- USCGC Jarvis (1979), using the rudder as a roll stabiliser:
  - 31–49% roll reduction in beam seas, 22% in bow seas, 28% in quartering seas.
  - In quartering seas, 28% in SS4 but only 8% in SS6.
  - Source: [IMarEST Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf)
- Monk's criterion (1987): lateral acceleration at the bilge in SS5 should not exceed 1.5 m/s² — [IMarEST Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf)
- Synchronism: Nassau's period matched the average North Sea swell, which caused severe rolling — [Wikipedia Nassau](https://en.wikipedia.org/wiki/Nassau-class_battleship); [gwpda](http://www.gwpda.org/naval/gmdefn.htm)

**Heading**
- Bismarck rolled and yawed in coupled fashion in following seas — [navweaps INRO](https://navweaps.com/index_inro/INRO_Bismarck.php)
- The Arethusa class rolled worst "when the sea was abeam or abaft" — [Gutenberg](https://gutenberg.org/cache/epub/75617/pg75617-images.html)

### Inferences: amplitude model for the game
- **[INFERRED]** Treat roll as a 1-DOF oscillator driven by wave slope.
  - Steady amplitude: θ = θ_w·H(r), where θ_w ≈ π·H_s/λ is the effective wave slope (λ = g·T_w²/2π) and r = T_roll/T_encounter.
  - Magnification: H(r) = 1/√((1 − r²)² + (2ζr)²).
- **[INFERRED]** Equivalent damping ratio ζ:

  | Configuration | ζ | Resonant magnification 1/(2ζ) |
  |---|---|---|
  | No bilge keels | 0.03–0.05 | 10–17 |
  | Bilge keels | 0.08–0.12 | 4–6 |
  | Fins at speed | +0.2–0.4 | — |

  These are consistent with the halving and two-thirds reductions in Q4, but I found no warship-specific ζ source [UNCERTAIN].
- **[INFERRED]** Encounter period: T_e = T_w / |1 − (ω_w·U·cos μ/g)| with μ the heading to waves.
  - Head seas shorten T_e, moving it away from long roll periods.
  - Quartering and following seas lengthen T_e toward T_roll, which is why big ships roll most at about 45° off the stern (CVA data) and why following seas are bad (Bismarck).
  - Beam seas give T_e = T_w.
- **[INFERRED]** Heading factor for roll excitation: about sin μ (zero in pure head or following seas, maximum abeam), multiplied by the resonance term.
- **[INFERRED]** Typical sea-state wave periods (WMO/PM-type modal periods): SS3 ≈ 5–7 s, SS4 ≈ 6–9 s, SS5 ≈ 8–11 s, SS6 ≈ 10–13 s, SS7 ≈ 12–16 s.
  - Consequence: destroyers (T ≈ 8–10 s) resonate in SS4–5.
  - Stiff battleships (T ≈ 12–14 s, e.g. Nassau, Bismarck) resonate in SS6–7, i.e. North Sea gale swell.
  - Tender British battleships (T ≈ 15–20 s) resonate only with long ocean swell.
  - Not sourced; flag as an approximation.
- **[INFERRED]** Significant single-amplitude roll in beam or quartering seas, from the CVA data, the frigate 25° out-to-out figure and the 8° operability limit:

  | Ship | SS3 | SS4 | SS5 | SS6 | SS7 |
  |---|---|---|---|---|---|
  | Battleship, bilge keels | 1–2° | 2–4° | 4–7° | 8–14° | 12–20° |
  | Destroyer/frigate, bilge keels, no fins | 3–5° | 5–9° | 9–14° | 12–20° | 15–25° |
  | Corvette/sloop (<1,200 t) | 4–6° | 6–12° | 12–18° | 15–25° | 20–40° (Flower "40° each side" as extreme) |
  | Fin-stabilised escort at speed | — | ~2–3° | — | — | — |

  The ~2–3° fin-stabilised figure comes from the "<2° at 18 kn+" result. Maxima are about 1.9× significant (Rayleigh, 1-in-1000 waves) [INFERRED].

### Gaps
- No sourced measured roll time-series for WWI or WWII capital ships in a known sea state.
- No sourced warship roll damping coefficients.
- The NAVSEA 1974 figures with destroyer roll by sea state could not be read.

---

## Q4. Bilge keels, anti-roll tanks, gyros, fins: reductions and adoption

### Takeaway
Bilge keels roughly halve resonant roll (Froude, 1870s) for a speed cost of 2.5% or less of resistance. They became universal after the Royal Sovereign episode (1894–95).

Passive tanks did poorly in warships: Inflexible's were "useless", and Frahm tanks cut roll by about 33% on Von der Tann, after which they were dropped. Gyros worked on Henderson but cost 3–5% of displacement and were abandoned.

Active fins (Denny-Brown, first warship HMS Bittern 1936–38) gave 64–92% reductions at speed. The RN fitted them to sloops for AA gunnery and to over 100 escorts by 1950. Stabilised fire control later reduced the need for whole-ship stabilisation.

### Cited findings
**Bilge keels**
- Froude's trials at Plymouth: with the same period, Perseus rolled up to about 23° while Greyhound, with bilge keels (3 ft 6 in × 100 ft), rolled about 11½°, i.e. roughly half. When one keel tore off, the advantage "greatly diminished" — [USNI 1885 "Notes on bilge keels"](https://www.usni.org/magazines/proceedings/1885/january/notes-bilge-keels)
- Serapis troopship: 27 in keels reduced roll by 10° each way (23° leeward and 14° windward with keels) in a heavy Atlantic beam sea — [USNI 1885](https://www.usni.org/magazines/proceedings/1885/january/notes-bilge-keels)
- Resistance penalty at most about 2.5% — [USNI 1885](https://www.usni.org/magazines/proceedings/1885/january/notes-bilge-keels)
- Brown: for at least a century bilge keels "tended to be too small"; there is no simple sizing rule — [IMarEST Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf)
- Bilge keels were later added to Nassau and Seydlitz — [Wikipedia Nassau](https://en.wikipedia.org/wiki/Nassau-class_battleship); [Seydlitz](https://en.wikipedia.org/wiki/SMS_Seydlitz)
- They also contributed to Nassau's speed loss at hard rudder — [Wikipedia Nassau](https://en.wikipedia.org/wiki/Nassau-class_battleship)

**Anti-roll tanks**
- Inflexible (1881): "water tanks to dampen the roll, which turned out to be useless" — [Wikipedia Inflexible](https://en.wikipedia.org/wiki/HMS_Inflexible_(1876))
  - Described as "very partially successful", with the idea reappearing in 1910 Atlantic liners — [Gutenberg](https://gutenberg.org/cache/epub/75617/pg75617-images.html)
- Leopold dates modern naval stabilisation to anti-roll tanks in British warships at the end of the 19th century — [Wikipedia Stabilizer](https://en.wikipedia.org/wiki/Stabilizer_(ship))
- Frahm tanks (Germany, early 1900s; 1911 trials paper) — [Wikipedia Antiroll tanks](https://en.wikipedia.org/wiki/Antiroll_tanks)
- Von der Tann's Frahm tanks "only reduced rolling by 33%" and were deemed ineffective; the space was reused for 180 t of extra coal — [Wikipedia Von der Tann](https://en.wikipedia.org/wiki/SMS_Von_der_Tann)
- Derfflinger: tanks were fitted to her only, tested in December 1914 with "inconclusive results", and two casemate guns were removed to make room — [Wikipedia Derfflinger](https://en.wikipedia.org/wiki/Derfflinger-class_battlecruiser)
- König: Tirpitz decided against tanks on 22 January 1912 — [Wikipedia König](https://en.wikipedia.org/wiki/K%C3%B6nig-class_battleship)
- Tanks were revived on many naval vessels in the 1950s — [Wikipedia Antiroll tanks](https://en.wikipedia.org/wiki/Antiroll_tanks)
- Controlled passive tanks cut a frigate-type ship's roll from about 25° out-to-out to about 9° (≈64%, OCR "go") and are in HMS Challenger and RMAS vessels — [IMarEST stabilisers](https://library.imarest.org/record/8618/files/v32b1p13a.pdf) [UNCERTAIN OCR]
- Drawbacks: open Frahm tanks corrode and add drag that rises with the square of speed — [Wikipedia Antiroll tanks](https://en.wikipedia.org/wiki/Antiroll_tanks)

**Gyro stabilisers**
- USS Worden (DD-16) was fitted in 1915.
- USS Henderson (1917):
  - Two 25-ton, 9 ft flywheels at 1,100 rpm, driven by 75 hp motors.
  - Roll held to 3° "in the roughest seas."
  - Source: [Wikipedia Stabilizer](https://en.wikipedia.org/wiki/Stabilizer_(ship)); [Anti-rolling gyro](https://en.wikipedia.org/wiki/Anti-rolling_gyro)
- Effective rotors need about 3–5% of displacement; gyros were replaced by fins on cost and weight, and their moment saturates in large waves — [Wikipedia Anti-rolling gyro](https://en.wikipedia.org/wiki/Anti-rolling_gyro)
- USNI 1933:
  - The Conte di Savoia system weighed 750 t in all and cost about $1M; a 30° complete arc was cut to under 3° each side, and yaw also fell.
  - The US Navy dropped gyros "just as [they were] successful."
  - Hosho and Ryujo had installations, as did the Italian destroyer Pigafetta.
  - Source: [USNI 1933 "Gyroscopic stabilization"](https://www.usni.org/magazines/proceedings/1933/april/gyroscopic-stabilization)
- Schlick's gyros gave results described as "disappointing or dangerous in practice" — [Wikipedia Schlick](https://en.wikipedia.org/wiki/Ernst_Otto_Schlick)

**Active fins**
- Motora (Mitsubishi) patented the fin stabiliser in 1922 — [Wikipedia Stabilizer](https://en.wikipedia.org/wiki/Stabilizer_(ship))
- Isle of Sark (1935) had the first practical active fin system — [IMarEST stabilisers](https://library.imarest.org/record/8618/files/v32b1p13a.pdf)
- HMS Bittern was the first warship with Denny-Brown fins; the date is given as 1938 in one paper and 1936 in another — [IMarEST stabilisers](https://library.imarest.org/record/8618/files/v32b1p13a.pdf); [IMarEST Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf)
- The Bittern and Egret classes had Denny-Brown fins plus HACS or Fuze Keeping Clock AA fire control — [Wikipedia Bittern class](https://en.wikipedia.org/wiki/Bittern-class_sloop); [Egret class](https://en.wikipedia.org/wiki/Egret-class_sloop)
- Over 100 sloops, frigates and destroyers had them by 1950 — [Wikipedia Stabilizer](https://en.wikipedia.org/wiki/Stabilizer_(ship)); [IMarEST](https://library.imarest.org/record/8618/files/v32b1p13a.pdf)
- WWII fins: about 2 m², aspect ratio about 2.1 — [IMarEST stabilisers](https://library.imarest.org/record/8618/files/v32b1p13a.pdf)
- Performance on a frigate-type ship, from 25° out-to-out unstabilised — [IMarEST stabilisers](https://library.imarest.org/record/8618/files/v32b1p13a.pdf):

  | Speed | Roll with one fin pair | Reduction [INFERRED] |
  |---|---|---|
  | 18 kn and above | under 2° | ~92% |
  | 12 kn | 4–5° | ~80% |
  | below 6 kn | nil | 0% |

- Some early fins were removed to gain fuel stowage because the control engineering was immature — [IMarEST Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf)
- Post-war RN fits — [IMarEST](https://library.imarest.org/record/8618/files/v32b1p13a.pdf); [Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf):
  - Leander and Type 12: one fin pair.
  - County class: three non-retractable pairs.
  - Also fitted to the Leopard and Tribal (Type 81) classes.
- US Navy fins succeeded on USS Gyatt (1956) and Bronstein (1958); an active tank was tried on Hamilton (DMS-18) — [Wikipedia Stabilizer](https://en.wikipedia.org/wiki/Stabilizer_(ship))
- 17-ship US study: fins averaged a 64% roll reduction in beam seas — [DTIC ADA159804](https://apps.dtic.mil/sti/pdfs/ADA159804.pdf)
- Why the RN did not fit fins ship-wide after WWII: tachometric fire control with rate gyros stabilised the sight line against roll, pitch and yaw. Later drivers were helicopter operations and cheap OPVs — [IMarEST stabilisers](https://library.imarest.org/record/8618/files/v32b1p13a.pdf)
- German Type 1934 destroyers carried active stabilisers, replaced by bilge keels in 1938–39 — [Wikipedia](https://en.wikipedia.org/wiki/Type_1934_destroyers)

### Inferences
**[INFERRED]** Game multipliers on resonant roll amplitude:

| Device | Multiplier on resonant roll | Cost / limits |
|---|---|---|
| Bilge keels | ×0.5 (×0.6–0.7 off-resonance) | 1–2% speed |
| Frahm/passive tank, pre-1920 | ×0.67 | Several hundred tons; works only near the tuned period; can amplify off-tune |
| Controlled passive tank, 1950s+ | ×0.35–0.5 | Works at any speed |
| Gyro (Sperry, 1915–30s) | ×0.2–0.3 up to saturation | 3–5% of displacement |
| Denny-Brown fins, 1936+ | ×0.2 (≥18 kn) / ×0.35 (12 kn) / ×1.0 (<6 kn) | Speed-dependent |
| Modern fins, 1960s+ | ~×0.35 average over headings | Speed-dependent |

### Gaps
- No percentage figures for the Royal Sovereign class before and after keels.
- No Black Swan trial numbers.
- No Schlick torpedo-boat (See Bär, 1906) figures.
- No Henderson "before" angle.

---

## Q5. Pitch, yaw, heave

### Takeaway
Sourced data is thin and mostly for carriers. Pitch is small and well damped: about 1° significant in SS5 and 2–2.5° in SS6 for a 300 m ship. Yaw in a seaway runs about 1–3° significant. The destroyer operability limit was 3° pitch.

### Cited findings
- CVA pitch, yaw and roll velocity/acceleration by sea state (table in Q3); pitch periods 7.6–11.2 s across SS2–6 — [DTIC ADA071890](https://apps.dtic.mil/sti/pdfs/ADA071890.pdf)
- Destroyer operability limits: 3° pitch, 0.4 g vertical acceleration at the bridge — [DTIC ADA159804](https://apps.dtic.mil/sti/pdfs/ADA159804.pdf)
- Hermione and Gurkha (1977 comparative trials): pitch almost indistinguishable and heave differed little — [IMarEST Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf)
- Roll and yaw couple in following seas (Bismarck) — [navweaps INRO](https://navweaps.com/index_inro/INRO_Bismarck.php)
- On Conte di Savoia, yaw fell as roll was stabilised — [USNI 1933](https://www.usni.org/magazines/proceedings/1933/april/gyroscopic-stabilization)
- Roll, pitch and yaw tilt the gun trunnions; at 90° elevation each degree of trunnion tilt is a degree of line-of-fire error, and at 0° elevation none. "Firing on the roll" on the up-roll adds range — [navweaps tech-074](https://www.navweaps.com/index_tech/tech-074.php)
- MIT barge-heave notes: T_n = 2π/√(g·B/(D + C_a·(π/8)·B)), with heave damping about 8% of critical in the example — [MIT 2.019](https://ocw.mit.edu/courses/2-019-design-of-ocean-systems-spring-2011/1b400805815fa9d331f76e106b0bbc16_MIT2_019S11_SK3.pdf) [UNCERTAIN: transcription garbled]

### Inferences
- **[INFERRED]** Heave and pitch natural periods ≈ 2.4–2.6·√d s (d = draught in m): about 7–8 s for battleships (d ≈ 9–10 m) and 5 s for destroyers (d ≈ 4 m). These are heavily damped (ζ ≈ 0.2–0.3), so the motion follows the waves.
- **[INFERRED]** Significant pitch in head seas ≈ (wave slope) × (a length-ratio factor that falls once L > λ). Approximately:

  | Ship | SS4 | SS5 | SS6 |
  |---|---|---|---|
  | Destroyer (100–120 m) | 1.5–2.5° | 2.5–4° | 4–6° |
  | Battleship (200–270 m) | 0.4–0.8° | 0.8–1.5° | 1.5–3° |

- **[INFERRED]** Yaw swing in a seaway under autopilot or helmsman: 1–2° significant in SS4–5 and 2–4° in SS6 for large ships (CVA anchor). Use about 1.5× for destroyers and about 2× in following and quartering seas.
- **[INFERRED]** Heave significant amplitude ≈ 0.3–0.5·H_s for long ships in head seas and ≈ 0.6–0.9·H_s for small ships or beam seas.

### Gaps
- No sourced pitch or heave numbers for destroyers or WWII battleships.
- No sourced pitch-period formula.
- No historic yaw data for pre-1950 ships.

---

## Q6. Stiff vs tender: what contemporaries argued

### Takeaway
The RN deliberately chose low GM (about 1–2 m) for a long, gentle roll and a "steady gun platform" (Revenge, Hood, KGV, Vanguard), and accepted the smaller damage margin. Germany chose high GM (2.6–4 m) for damage resistance and accepted "stiff/lively/jerky" motion, though it rejected Derfflinger designs with GM 2.7–2.9 m because the fast roll degraded aiming. Japan, after Tomozuru (1934) and the Fourth Fleet Incident (1935), added ballast and cut topweight fleet-wide.

### Cited findings
- The RN favoured lower GM for "superior gunnery": long, gentle rolls. Excessive stability gives rapid rolling that upsets fire control. Bismarck's stabilised directors offset her rapid roll — [navygeneralboard](https://www.navygeneralboard.com/?p=4021)
- Revenge's designed 3.4 ft GM was later considered "too low" — [navygeneralboard](https://www.navygeneralboard.com/?p=4021)
- Bulging raised it to 5.1–5.5 ft, and the bulged ships then reported "excessive rolling" — [Wikipedia Revenge](https://en.wikipedia.org/wiki/Revenge-class_battleship)
- Revenge was designed for a "slower roll period" — [gwpda](http://www.gwpda.org/naval/gmdefn.htm)
- Royal Sovereign class: White raised Hood's (1891) GM to reduce rolling, but the 7% shorter period made her gunnery worse — [Wikipedia RS](https://en.wikipedia.org/wiki/Royal_Sovereign-class_battleship)
- Derfflinger design: GM 2.7–2.9 m "too high … would have caused the ship to roll too quickly," and "the fast roll period would have degraded the gunners' ability to aim" — [Wikipedia Derfflinger](https://en.wikipedia.org/wiki/Derfflinger-class_battlecruiser)
- Nassau: a large GM from the wing turrets "should have made them stable gun platforms," but the period matched the North Sea swell — [Wikipedia Nassau](https://en.wikipedia.org/wiki/Nassau-class_battleship)
- Bismarck: GM above the 5–10%-of-beam guideline; "too much stability makes a ship 'stiff' … tend[s] to degrade weapon systems effectiveness" — [navweaps INRO](https://navweaps.com/index_inro/INRO_Bismarck.php)
- The USNI 1931 article notes that naval vessels need large GM to survive damage, which makes them prone to rolling — [USNI 1931](https://cdn02.usni.org/magazines/proceedings/1931/november/why-they-roll)
- Slade: large GM gives a quick but violent roll (WWII DE example); acceptable GM is tied to freeboard, so low-freeboard destroyers accept lower GM — [navweaps tech-009](https://navweaps.com/index_tech/tech-009.php)
- Froude showed low GM gives a long period and less rolling. Brown: misapplying this led some battleships and many liners to be built with inadequate GM — [IMarEST Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf)
- Tomozuru (capsized 12 March 1934, low GM, low fuel):
  - Rebuilt from 535 t to 600 t standard, speed cut from 30 to 28 kn, armament cut.
  - Fleet-wide reviews followed, covering Ryujo, Mogami, Fubuki, Akatsuki, Hatsuharu, Taigei and minelayers.
  - Source: [Wikipedia Tomozuru](https://en.wikipedia.org/wiki/Japanese_torpedo_boat_Tomozuru)
- Fubuki rebuild: +40 t ballast, 1,750 → 2,050 t standard, top speed 35 → 34 kn — [Wikipedia Fubuki](https://en.wikipedia.org/wiki/Fubuki-class_destroyer)
- Mogami: lower welded-aluminium superstructure, bulges (beam to 20.5 m), −2 kn — [Wikipedia Mogami](https://en.wikipedia.org/wiki/Mogami-class_cruiser)
- US Navy: Sims-class topweight needed lead ballast, so the Fletcher beam was widened 18 in — [Wikipedia Fletcher](https://en.wikipedia.org/wiki/Fletcher-class_destroyer)
- Farragut lessons from Typhoon Cobra — [Wikipedia Farragut](https://en.wikipedia.org/wiki/Farragut-class_destroyer_(1934)); [Typhoon Cobra](https://en.wikipedia.org/wiki/Typhoon_Cobra)
- Captain-class DEs: weight was deliberately added low to *reduce* GM and lengthen the period, improving motion — [IMarEST Brown](https://library.imarest.org/record/8299/files/v35b3p12a.pdf)

### Inferences
- **[INFERRED]** Game trade-off, with T from Q1:
  - Gunnery steadiness penalty ∝ peak roll rate ω = 2πθ/T and to roll amplitude θ.
  - Survivability ∝ GM (list after flooding ≈ atan(heeling moment / (Δ·GM)), capsize margin).
  - A ship at GM/B ≈ 0.04–0.06 with bilge keels is the British "steady platform"; GM/B ≈ 0.09–0.11 is German "stiff/lively."
- **[INFERRED]** Fuel burn-off raises KG and reduces GM in destroyers (Spence and the Type 1934 ballast rule). The designer should expose a loading-dependent GM (full, half, light).

### Gaps
- No primary source found for the US Standard-type battleships' stability philosophy (the Wikipedia page is silent).
- No D.K. Brown verbatim quotes, because the search budget ran out.

---

## Q7. Wind heel and heel in turns

### Takeaway
Steady heel at hard rudder and full speed runs 3–9° for battleships, 8–11° for WWI German capitals, and 13–14° for German cruisers. The IMO-style formula (radius R ≈ 5L) gives the order of magnitude but underpredicts. Transient heel is about 1.5–2× steady.

### Cited findings
**Heel at hard rudder**

| Ship / class | Heel | Speed loss | Source |
|---|---|---|---|
| Bismarck | 3° | 65% | [Wikipedia](https://en.wikipedia.org/wiki/Bismarck-class_battleship) |
| König | 8° | 66% | [Wikipedia](https://en.wikipedia.org/wiki/K%C3%B6nig-class_battleship) |
| Kaiser | 8° | 66% | [Wikipedia](https://en.wikipedia.org/wiki/Kaiser-class_battleship) |
| Von der Tann | 8° | 60% | [Wikipedia](https://en.wikipedia.org/wiki/SMS_Von_der_Tann) |
| Moltke | 9° | 60% | [Wikipedia](https://en.wikipedia.org/wiki/Moltke-class_battlecruiser) |
| Seydlitz | 9° | 60% | [Wikipedia](https://en.wikipedia.org/wiki/SMS_Seydlitz) |
| Derfflinger | 11° | 65% | [Wikipedia](https://en.wikipedia.org/wiki/Derfflinger-class_battlecruiser) |
| Scharnhorst | >10° (13° trials) | >50% | [Wikipedia](https://en.wikipedia.org/wiki/Scharnhorst-class_battleship) |
| Deutschland | 13° | — | [Wikipedia](https://en.wikipedia.org/wiki/Deutschland-class_cruiser) |
| Admiral Hipper | 14° | 50% | [Wikipedia](https://en.wikipedia.org/wiki/Admiral_Hipper-class_cruiser) |
| Yamato | 9.0° max (26 kn, 35° rudder) | — | Existing project note manoeuvring.md |

**IS Code heel-in-turn formula**
- Heeling moment: M_R = 0.2·(v₀²/L_wl)·Δ·(KG − d/2), with v₀ in m/s and Δ in t.
- tan φ = M_R/(Δ·g·GM), with φ ≤ 10° as the criterion.
- The 0.2 coefficient assumes R = 5·L_wl.
- Model tests showed steady heel about 1.8× and maximum heel about 3.5× the formula's value; maximum transient ≈ 1.7–2.0× steady.
- Source: [Krata et al., TransNav](https://yadda.icm.edu.pl/baztech/element/bwmeta1.element.baztech-649426d2-8e01-4e67-892a-cf620e54fa9f/c/The_Need_of_the_Revision_of_Passenger_Ships__Stability_Criterion_on_Account_of_Turning-1.pdf)

**Other heel sources**
- Bismarck listed 9° to port after the Hood action from flooding (not a turn) — [navweaps INRO](https://navweaps.com/index_inro/INRO_Bismarck.php)
- Iowa broadside impulse, from navalgazing's energy estimate ([navalgazing](https://navalgazing.net/Did-Iowa-Move-Sideways-During-a-Broadside)); the author calls the estimate an overestimate:
  - Roll rate per salvo 0.82°/s at battle displacement and 1.42°/s light.
  - Maximum heel ≈ 5.7° at battle displacement and 22.5° light.

### Inferences
- **[INFERRED]** Game formula for steady heel (deg), from φ = atan[V²·(KG − d/2) / (g·R·GM)] with V in m/s and R the steady turn radius:

  `φ_ss = atan( V²·(KG − d/2) / (g·R·GM) )·57.3`

  - Check against Bismarck (V ≈ 12 m/s after 60% speed loss from 30 kn, R ≈ 0.45·TD ≈ 420 m, KG − d/2 ≈ 5 m, GM 4 m): ≈ 2.5°, consistent with the sourced 3°.
  - Check against a light cruiser (V ≈ 15 m/s, R 350 m, KG − d/2 ≈ 4 m, GM 1.2 m): ≈ 12°, consistent with Hipper and Deutschland at 13–14°.
- **[INFERRED]** Transient outward heel at the start of the turn: ×1.5–2.0 of steady heel, decaying over about one roll period.
- **[INFERRED]** Wind heel: tan φ = (P·A·h)/(Δ·g·GM), with P ≈ 0.5·ρ_air·C_d·V_w² (C_d ≈ 1.0–1.2) and h the lever from the centre of lateral area to half-draught.
  - Worked example (Nelson-like): A ≈ 3,000 m², h ≈ 15 m, Δ ≈ 38,000 t, GM 3 m, 50 kn wind → P ≈ 400 Pa → ≈ 1° steady.
  - Worked example (destroyer): A 600 m², h 7 m, Δ 2,500 t, GM 0.9 m, 50 kn → ≈ 3–4°.
  - The IMO weather criterion's 0.0514 t/m² (≈ 504 Pa) wind pressure is the conventional design value; the A.562 doc was found in search but not read [UNCERTAIN].

### Gaps
- No sourced heel-in-turn values for British, US or Japanese destroyers or for most battleships (Iowa only qualitative).
- No sourced wind heel data for named warships.
