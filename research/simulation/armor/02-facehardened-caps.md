# 02 — Face-hardened (cemented) armour vs projectiles; caps, shatter and shell quality
Status: draft    Updated: 2026-10-16    Request: —

Research inputs for the naval wargame armour-penetration model (period about 1860–1945). This is not a finished model. It collects mechanisms, formulas, constants, validity ranges and data to fit one later.

Conventions:
- T = plate thickness, D = projectile diameter (calibre), W = projectile weight, V = striking velocity (ft/s unless stated), Ob = obliquity measured from the plate normal.
- T/D is thickness in calibres.
- "NBL" is the US Navy Ballistic Limit: the whole projectile gets through, usually with nothing left in the plate.
- [INFERRED] marks my own inference or calculation. [UNCERTAIN] marks a conflict or a weak source.
- Most quantitative material is from Nathan F. Okun, much of it on navweaps.com. Okun's FACEHARD program is closed-source, so its internals are known only from his articles and changelog, and from forum users who ran it.

Sources that could not be read in this session:
- `navweaps.com/index_nathan/metalprpsept2001.php` (fetch error). The 2009 and combinedfleet versions were used instead.
- Okun's RTF papers "Problems Involved in Evaluating Armor Penetration" and "Nose-Only Shatter Defined" (binary/blocked).
- The armchairgeneral "shatter gap" threads (fetch error).

---

## 1. How face-hardened plate defeats projectiles

### 1.1 Construction: three layers

| Layer | Typical properties | Source |
|---|---|---|
| Cemented (carburised) skin | About 1" (2.54 cm) deep and 1–1.5% C. Hardness 575–700 BHN: Krupp aimed for 650–700 (typical max 670–680), most pre-1930 makers settled for 575–625, British WWII CA about 600 BHN by design. The skin was "destroyed on impact anyway". | [metalprp 2009](https://www.navweaps.com/index_nathan/metalprpsept2009.php) |
| Deep "decremental" face (chill) | About 500 BHN just behind the cemented layer (535 BHN highest known). In KC a/A it falls to about 350 BHN at about 20% depth, then through a transition to the back. | [metalprp 2009](https://www.navweaps.com/index_nathan/metalprpsept2009.php) |
| Back | About 225 BHN (KC a/A). KC n/A raised it to 240 BHN, "the maximum" known. Tough and ductile. | same |

**Chill (face) depth as % of plate thickness, by armour type**

| Armour | Face % |
|---|---|
| Krupp KC a/A (1894–) | 33–35% |
| German KC n/A (1930s) | 41% |
| British post-1935 CA | 30% |
| US Bethlehem "Thin Chill" Class A | 15–20% (almost all cemented layer, steep transition) |
| US WWII "Thick Chill" Class A | ~55% average; individual plates 41–53% |
| Midvale Non-Cemented Class A (WWI era) | >80% |
| Wikipedia / NO&G general Krupp process | heat penetrates "30% to 40%" of thickness |
| Harvey (no deep face; cemented skin about 1" deep, so the face fraction falls with thickness) | 33–50% on 3", 15–25% on 6", 8–12.5% on 12", 5.8–9% on 17" |

Sources for the table:
- KC n/A, British CA and the US Class A range: [Okun, Bismarck armour](https://www.combinedfleet.com/okun_biz.htm).
- Bethlehem Thin Chill: [14" Mk16 vs Class A](https://navweaps.com/index_nathan/14inchMk16Mod4VsClassA.php).
- US WWII Thick Chill, 55% average: [14" Mk16 vs Class A](https://navweaps.com/index_nathan/14inchMk16Mod4VsClassA.php) and [OS 751](https://navweaps.com/index_nathan/os751BalTests.php).
- US individual plates 41–53%: [OS 751](https://navweaps.com/index_nathan/os751BalTests.php) (the 1942 20.6" plates were 52% and 41%) and [14" Mk16 page](https://navweaps.com/index_nathan/14inchMk16Mod4VsClassA.php) (53% and 49.5%).
- Midvale Non-Cemented: [tech-017](https://www.navweaps.com/index_tech/tech-017.php).
- General Krupp process: [Wikipedia Krupp armour](https://en.wikipedia.org/wiki/Krupp_armour) and [NO&G 1957 ch.4](https://www.eugeneleeslover.com/USNAVY/CHAPTER-4.php).
- Harvey face fractions: [Okun, AP shell vs Harvey tests](https://navweaps.com/index_nathan/apShellVsArmor.php).

Harvey process: charcoal cementation for 2–3 weeks at about 1200 °C. Face carbon reaches about 1%, falling to 0.1–0.2% at about 1" ([Wikipedia Harvey](https://en.wikipedia.org/wiki/Harvey_armor)). The 1957 USN text says the outer inch went from "about one-fifth of 1 percent" to "slightly more than 1 percent" C ([NO&G 1957](https://www.eugeneleeslover.com/USNAVY/CHAPTER-4.php)).

**Typical compositions**
- Krupp KC: C 0.35, Ni 3.90, Cr 2.00, Mn 0.35 ([Wikipedia Krupp](https://en.wikipedia.org/wiki/Krupp_armour); [NO&G 1937 ch. XII](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-C.html)).
- 1950s USN Class A: C 0.33, Ni 3.33, Cr 2.00 ([NO&G 1957](https://www.eugeneleeslover.com/USNAVY/CHAPTER-4.php)).
- Chromium at about 1.5–3% is what made deep decremental hardening possible (Krupp, 1894) ([metalprp 2009](https://www.navweaps.com/index_nathan/metalprpsept2009.php)).

### 1.2 Failure sequence (what the plate does to the shell, and how the plate fails)

1. **Nose attack.** The hard face is meant to damage the projectile nose, not to resist ductile hole enlargement. The face "fails from shock before the whole projectile engages", and only the forward (nose) part of the shell's mass is involved at that stage ([tech-033](https://www.navweaps.com/index_tech/tech-033.php)).
   - The soft back acts as a "shock energy sink" that keeps the brittle face from shattering. Without it the weight exponent would be even lower than 0.2 (same source).
2. **Face crack and plug.** The plate fails when the material in the projectile's path is punched out of the back as a plug, which can act as a second projectile. A brittle face can eject that plug at striking velocities *below* those needed for the projectile itself to get through ([metalprp 2009](https://www.navweaps.com/index_nathan/metalprpsept2009.php)).
   - This produces two ballistic limits:
     - HBL: the plate is holed and pieces get through.
     - NBL: the whole projectile gets through.
   - Okun attributes much of the HBL–NBL gap to the broken nose "rebounding" into the shell body after plug formation, which absorbs energy ([AP shell vs Harvey](https://navweaps.com/index_nathan/apShellVsArmor.php)).
   - In Okun's 1890s Harvey analyses the FACEHARD HBL is typically 2–7% below the NBL, for example HBL 2038 vs NBL 2080 ft/s for a 10" APC ([same](https://navweaps.com/index_nathan/apShellVsArmor.php)).
3. **Brittle (surface) vs ductile (volume) work.** Brittle fracture of the hard face is a *surface* effect that scales with the square of size. Ductile tearing of the soft back is a *volume* effect that scales with the cube, like projectile weight ([metalprp 2009](https://www.navweaps.com/index_nathan/metalprpsept2009.php)). This is Okun's physical basis for the FH scaling effect (§1.4).
4. **Projectile damage classes.** These are used in USN acceptance and in FACEHARD:
   - Primary shatter: the hard nose breaks from shock on striking a hard plate.
   - Secondary shatter: the body crushes the nose against the plate after the initial impact. This was rare against most WWI armours because brittle plates "caved in first". The exceptions were Midvale Non-Cemented Class A and Austro-Hungarian Witkowitz KC ([tech-055](https://www.navweaps.com/index_tech/tech-055.php)).
   - Other damage modes: "chewing, shearing, wiping" of the nose, and body or base breakage ([OS 751](https://navweaps.com/index_nathan/os751BalTests.php)).

### 1.3 Why thin FH plate behaves differently

- **The back is what stops the shell.** For plates under roughly 3", face-hardening makes penetration *easier*, because it thins the tough back. The tough back's thickness is the key factor ([Naval Gazing, Armor Part 2](https://www.navalgazing.net/Armor-Part-2)).
- **Minimum thickness for the deep-face process** was usually about 4" (102 mm). Krupp and Witkowitz went down to 3.2" (8 cm) through WWI, then raised the minimum to 4" ([metalprp 2009](https://www.navweaps.com/index_nathan/metalprpsept2009.php)).
- **Thin USN Class A was a different alloy.** The thinnest standard KC-type Class A was about 4–5". The 2.25"-minimum thin Class A was "of a different, weaker composition" ([OS 751](https://navweaps.com/index_nathan/os751BalTests.php)). The OS 696 Class A shell-test plates were actually US Army light-tank face-hardened plates, made by a form of the Harvey process ([OS 696 specs](https://navweaps.com/index_nathan/os696BalSpecs.php)).
- **Harvey has no deep face.** Its face fraction falls with thickness (about 33–50% at 3" down to about 6–9% at 17"). Okun found Harvey "getting rather weak in the face" by 10–18" thickness ([AP shell vs Harvey](https://navweaps.com/index_nathan/apShellVsArmor.php)).
- **FACEHARD's Harvey treatment.** The program carries a Harvey "Shatter Thickness Multiplier" that is 1.00 for thick Harvey and *rises as thin Harvey gets thinner*. Harvey resistance fell "steeply and linearly" from 3" to 8", then "more slowly and linearly" to 18". Capped and uncapped results "track each other from 8" plates up" ([FACEHARD 6.x changelog](https://navweaps.com/index_nathan/facehard69ChangeInfo.php)).
- **Thin-cemented plate is easily defeated by a cap.** On thin-faced plate, caps "do not have to work very long… just long enough to crack the thin cemented surface layer" ([AP shell vs Harvey](https://navweaps.com/index_nathan/apShellVsArmor.php)).
- **Pragmatic consequence [INFERRED].** Below about 0.3–0.4 calibre, or below about 3–4" absolute, treat FH as roughly homogeneous or worse. Above about 0.5 calibre FH advantages appear, but only against projectiles it can damage.

### 1.4 Scaling effect: Okun's explanation of T/D and size

**Definition.** Scaling is the drop in an armour type's ballistic limit (in calibres) when projectile and plate are both enlarged by the same factor, all else constant ([metalprp 2009](https://www.navweaps.com/index_nathan/metalprpsept2009.php)).

**For FH, the effect depends on back-layer thickness (BLT).** The scaling effect "rises sharply when the unhardened back falls below about 65% of total thickness" ([metalprp 2009](https://www.navweaps.com/index_nathan/metalprpsept2009.php)).
- So thick-faced plate (US Thick Chill at 55% face, i.e. 45% back; Midvale NC at >80% face) is relatively *strong against small shells and weak against large ones*.
- Thin-faced plate (British CA at 30% face) is the opposite.
- For homogeneous armour, scaling depends on elongation. Example: for Wh (about 18% elongation) the NBL drops about 13.2% going from an 8" to a 14.96" projectile, against about 1.4% for STS (about 25% elongation) ([metalprp 2009](https://www.navweaps.com/index_nathan/metalprpsept2009.php)). HCWCALC applies the scaling only for D > 8" and elongation < 25% ([HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)).

**Direct answer to the brief's question.** At the *same T/D*, FH plate becomes *less* effective (in calibres) against *larger* calibres. The loss is larger the thinner the ductile back. Okun's 2004 forum statement was that "large projectiles penetrate much more easily than small projectiles against scaled plates of identical composition" ([forum quote](https://www.tapatalk.com/groups/warships1discussionboards/various-iz-graphs-t34037-s60.html)).

**Historical evidence**
- **Midvale NC.**
  - Against 12" soft-capped shells it was about 30% better than CKC (thickness-equivalent), and even better against 8".
  - Against 14" and 16" Midvale Unbreakable shells, "plate resistance dropped sharply". Against 16" it was no better than old Harvey nickel steel against unshattered shells ([tech-017](https://www.navweaps.com/index_tech/tech-017.php)).
  - Post-WWI hard caps "eliminated most of the thick face's benefit, even against 12"" (same source).
- **WWII US Class A.**
  - With its ~55% face it was "best against guns up to 8"", but declined relative to thinner-faced foreign armours against larger shells ([tech-017](https://www.navweaps.com/index_tech/tech-017.php)).
  - Okun: thin-face British WWII CA was "among the best face-hardened plates against large-caliber shells" ([forum, Okun post](https://www.tapatalk.com/groups/warships1discussionboards/comparison-gkdos-100-facehard-m79apclc-t16291-s10.html)).
  - British CA was the most resistant type at battleship thicknesses (10"+). For 7" or less, US Class A was superior ([Okun, Bismarck](https://www.combinedfleet.com/okun_biz.htm)).
- **Tradeoff [Robertson].** Thick-face plate breaks more shells but gives longer-range partial penetrations. Thin-face plate lets intact shells through at longer range ([forum](https://www.tapatalk.com/groups/warships1discussionboards/comparison-gkdos-100-facehard-m79apclc-t16291-s10.html)).
- **Historic figure of merit (iron-equivalent) was thickness-dependent.** It was highest for medium plates and "falls off steadily for thick plates" (Robertson, [forum](https://www.tapatalk.com/groups/warships1discussionboards/different-definitions-of-armor-quality-t34968.html)).
- **Sliding alloy content by thickness.** Krupp's WWII armours reduced molybdenum as plates got thicker ([metalprp 2009](https://www.navweaps.com/index_nathan/metalprpsept2009.php)).
- **Process limits in very thick plate.** The 26" VH (Yamato turret face) was excessively brittle because the pre-WWI KC hardening process was used on plates over about 17", for which it was not designed. Slow cooling formed upper bainite ([tech-040](https://www.navweaps.com/index_tech/tech-040.php)).

**Quantitative illustration from USN acceptance velocities** [INFERRED] ([OS 751](https://navweaps.com/index_nathan/os751BalTests.php)). Class A velocities at 30° and T/D = 1.0:

| Shell | W/D³ (lb/in³) | V (ft/s) | V normalised to W/D³ = 0.65 using Okun's 0.2/1.21 exponents |
|---|---|---|---|
| 6" Mk 35 | about 0.60 | 1800 | ≈1776 |
| 8" Mk 21 Mod 5 | 0.654 | 1620 | ≈1620 |
| 14" Mk 16 Mod 8 | 0.547 | 1610 | ≈1565 |
| 16" Mk 8 Mod 6 (pre-Mod 6 allowance) | 0.659 | 1595 | ≈1599 |

- From 6" to 14–16", the required velocity at constant T/D drops by about 10–12%, which is about 12–15% in thickness terms.
- That is consistent with a modest scaling effect for 55%-face Class A. It is confounded by projectile quality (the 6" Mk 35 had a super-hard cap) and by the fact that these are plate-acceptance velocities, not NBLs.

---

## 2. Okun's FACEHARD program and closed-form FH fits

### 2.1 Core FH relation

From [tech-033](https://www.navweaps.com/index_tech/tech-033.php):

- Homogeneous general form: **T = K·[½(W/g)V²]^p**, with p from 0.5 to 1.0 and several K/p sets needed to cover all thicknesses.
- Face-hardened form: **T = C · W^0.2 · V^1.21**.
  - The velocity exponent 1.21 corresponds to an equivalent p of 0.605. That is halfway between De Marre's p = 0.714 (V^1.4286) and the ideal full-calibre punch p = 0.5 (V^1.0).
  - One exponent set covers all thicknesses.
  - Changing W (a longer shell or a smaller cavity) has little effect; changing V has a large effect.

**Interpretation and caution [INFERRED].** W^0.2 is the effect of changing *weight at fixed calibre*. C must contain a calibre dependence.
- The USN acceptance data above show nearly the same V at the same T/D for geometrically similar 8" and 16" shells.
- A literal T ∝ W^0.2 under geometric scaling (W ∝ D³) would give T ∝ D^0.6, which contradicts that data.
- Suggested model form:
  > T/D = C(plate, cap, nose, Ob) · (W/D³)^0.2 · V^1.21 · S(D, BLT)
  - S is a weak scaling term: falling with D for low-BLT plate, close to 1 for high-BLT plate.

**Check against USN specs** ([OS 696 comments](https://navweaps.com/index_nathan/os696Comments.php)):
- 16" Mk 8 at 35° needs 1,490 ft/s against 11" Class A and 2,130 against 17".
- (2130/1490)^1.21 = 1.541, against a thickness ratio of 17/11 = 1.545.
- So the specs implicitly used about V^1.21.
- The 16" Mk 5 (2,240 lb) needs 2,330 ft/s against 17" Class A, against 2,130 for the Mk 8 (2,700 lb). That implies a weight exponent of about 0.48 (homogeneous KE theory gives 0.605; FACEHARD uses 0.2).
- Okun notes cap and nose-damage differences "blur" this, and that the specs were more conservative than FACEHARD.

### 2.2 What FACEHARD contains (as far as published)

**Projectile inputs**
- Diameter, weight and body weight (cap and windscreen weights tracked separately).
- Nose shape (CRH / ogive). HCWCALC's "equivalent elliptical nose" logic is similar ([HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)).
- Projectile type: AP shot, AP shell, CP, CPC, SAP, SAPC, APC. 1890s AP shot and shell were merged ([changelog](https://navweaps.com/index_nathan/facehard69ChangeInfo.php)).
- Cap selection code ([changelog](https://navweaps.com/index_nathan/facehard69ChangeInfo.php)):

  | Code | Cap |
  |---|---|
  | −1 | hood |
  | 0 | none |
  | 1 | soft mild-steel cap |
  | 2 | fully functional hard cap |
  | 3 | "tough" cap (high-Ni, reduced-effectiveness hard cap; some Skoda, Krupp, Russian) |

- Flags: LTCASE (light case / large filler; required for fillers over about 4.5%), SHATRES (shatter resistance; 1 unless full AP cap), SOFTSHAT.
- Projectile quality and damage multiplier, QD. QD is "usually the same as the Q factor" of the plate, per Robertson ([forum](https://www.tapatalk.com/groups/warships1discussionboards/different-definitions-of-armor-quality-t34968.html)).

**Plate inputs**
- Armour type: Harvey; Krupp KC (various); Gruson/Terni cemented; Gruson chilled cast iron; THINCHILL (Harvey and Bethlehem Thin Chill); COMPOUND ([changelog](https://navweaps.com/index_nathan/facehard69ChangeInfo.php)).
- Nation/era armour presets with quality Q and back-layer thickness (§6).
- Thickness, obliquity, and curved-plate rule (for >45° on barbettes).

**Quality bonuses and reference**
- COMPOUND: +0% vs cast-iron shot, +10% vs 1890s steel AP, +20% otherwise.
- THINCHILL: +10% vs post-1900 steel shells.
- Reference is "Quality 1.00" KC ([changelog](https://navweaps.com/index_nathan/facehard69ChangeInfo.php)).

**Sub-models**
- Shatter is restricted to steel projectiles; cast iron is excluded as "too brittle".
- Breakage is evaluated in three velocity regions: below HBL, between HBL and NBL, and above NBL.
- Shatter reduces KC resistance more than Harvey resistance ([changelog](https://navweaps.com/index_nathan/facehard69ChangeInfo.php)).
- Shatter typically raises the striking velocity needed by about 30% (range 20–40%) ([HCWCALC page](https://navweaps.com/index_nathan/stsPlatePenProgram.php)). In [tech-055](https://www.navweaps.com/index_tech/tech-055.php) Okun phrases this as a plate-thickness bonus of about 30% at normal, falling to 0 at about 55°. [UNCERTAIN] whether 30% is on V or on T; it makes a factor of about 1.3^1.21 ≈ 1.37 difference in T.
- Obliquity: Okun's FH obliquity function is fitted to German GKdos 100 curves and applies only to FH ([forum quote](https://www.tapatalk.com/groups/warships1discussionboards/various-iz-graphs-t34037-s60.html)). The functional form is not published.
- Plug energy: FACEHARD computes energy absorbed by a punched plug ([HCWCALC page](https://navweaps.com/index_nathan/stsPlatePenProgram.php)).
- Decapping and windscreen logic follows tech-085 (§3.5).

**Outputs**
- HBL (holing), NBL (whole projectile through), and EBL (projectile through "effective", i.e. fit to burst). EBL is "NEVER" when the shell shatters.
- The navweaps penetration tables show columns PP / H / NL / EFF ([US tables](https://www.navweaps.com/index_nathan/Penetration_United_States.php), [British tables](https://www.navweaps.com/index_nathan/Penetration_Britain.php)). They are not defined on those pages. [UNCERTAIN, my reading]: H = holing, NL = Navy limit, EFF = effective/fit-to-burst, PP = probably partial/plate penetration (used at high obliquity).
- EFF is typically 1–10% thinner than H. Example: 16" Mk 8 Mod 6 at 0 yd gives PP 37.8" vs EFF 33.7" of US Class A.

**Accuracy**
- Okun reports FACEHARD reproduced the French post-WWII calculated test result for the 380 mm Mk 1 "to all four digits", without using that test to build the program ([tech-101](https://www.navweaps.com/index_tech/tech-101.php)).
- Against 1890s Harvey tests the shatter-plus-maximum-damage case was "the best fit overall", typically within about 1–3% where NBL could be bracketed ([AP shell vs Harvey](https://navweaps.com/index_nathan/apShellVsArmor.php)). Some cases were "way too high/low".
- Garzke & Dulin's (homogeneous-based) values agree with FACEHARD's British CA values within about 3% on average, with RMSD 3.0" ([tech-109](https://www.navweaps.com/index_tech/tech-109.php)).
- Users caution that "FaceHard is far from perfect" ([forum](https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=886270)).

### 2.3 Historical closed-form FH formulas

From [Okun, Historical formulae](https://navweaps.com/index_nathan/Hstfrmla.php) unless noted. English units.

| Formula | Form | Exponents (implied) | Notes |
|---|---|---|---|
| Krupp KC vs uncapped AP (c.1895) | T/D = 0.0006659·[(W/D³)V²]^0.5 | T ∝ V^1.0 | Linear in V; large scatter. |
| Davis, Harvey Ni-steel vs uncapped AP (c.1895) | T/D = 0.00003466·D^0.3333·[(W/D³)V²]^0.6667 | V^1.333 | |
| Davis, Harvey vs capped AP (c.1900) | T/D = 0.00008582·D^0.25·[(W/D³)V²]^0.625 | V^1.25, scaling D^0.25 | Okun: the W exponent conflicts with his 0.2. Robertson: the scaling term implies capped shells did not break up ([forum](https://www.tapatalk.com/groups/warships1discussionboards/changing-views-on-kc-penetration-formulas-t49927.html)). |
| De Marre Ni-steel (c.1890) | T/D = 0.00005021·D^0.07144·[(W/D³)(V/C)²·cos³Ob]^0.71429 | V^1.43 | For FH, a table of C is needed. Cr-Ni C ≈ 1.2–1.25 at normal. |
| Krupp "all-purpose" (1930s) | T/D = 0.30386·D^0.25·[(W/D³)(V/C)²]^0.625 | V^1.25 | Normal impact only. C ≈ 525 mild steel, 655–694 Wh, "up to 804 for the weakest APC vs KC n/A". |
| Thompson "F" (USN 1930, the 1931 "Standard Form") | V_NBL = 0.024056·[(T/D)/(W/D³)]^0.5·F/cos(Ob); F_STD = 6[(T/D)−0.45][Ob²+2000]+40000 | — | 0–75°; Cr-Ni and STS, standard 8" projectile. Okun: "not suited to FH". |
| US Immunity-Zone slide rule Mk 1, Class A (1930s) | (T/D) ∝ (W/D³)^0.75·V^1.51 | no scaling | Per Robertson citing Okun 1977 ([forum](https://www.tapatalk.com/groups/warships1discussionboards/changing-views-on-kc-penetration-formulas-t49927.html)). Jurens: a re-expression of Thompson F variants. |

**Using the Krupp formula as an armour-quality ratio [INFERRED].** Because T ∝ C^-1.25, KC n/A against weak APC resists (804/675)^1.25 ≈ **1.24×** Wh (range 1.20–1.29). That is a normal-impact FH-vs-homogeneous multiplier against a *breakable* shell.

**The Thompson F standard was used as the USN Class A grading scale in WWII.** [Okun's corrected 1946–47 test table](https://navweaps.com/index_nathan/britUSGermanArmorTests.php) grades both Class A/KC and Class B/Wh plates as "% of NBL" against the *same* Thompson-F standard.
- I reproduced every NBL in that table with the formula above to within 0.0–0.4% [INFERRED, by calculation].
  - 8" Mk 21, 335 lb, 8.5" at 30° → 1793 ft/s.
  - 14" Mk 16, 1500 lb, 13.5" at 30° → 1806 ft/s.
- Graded values: average US Class A scored 87–97% of standard, "best" US Class A 90–105%, KC n/A 82–113%, US Class B 92–105%, Wh 88–105%.
- Implication [INFERRED]: at 30–35° obliquity, T/D about 0.7–1.25, and modern hard-capped US shells, **Class A and Class B were within about ±10% of each other in velocity** (about ±12% in thickness).

**Later USN "% NBL" grading of 8" plates** (NPG report quoted by Wahl, [forum](https://www.tapatalk.com/groups/warships1discussionboards/different-definitions-of-armor-quality-t34968.html)):
- Against Mk 21-3 at 30°: Japanese VH 7.21" at 118%, average US 112.8%, best US 116–117%.
- Against Mk 21-5 (super-hard cap): VH 110–111%, average US 109.7%, German 8.5" KC n/A 113%.
- The harder cap cost the plates about 7% in velocity [INFERRED from 118 → 110].

**Naval Ordnance 1937 practice** ([ch. XII](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-C.html)):
- KC/Class A was rated with a De Marre "factor of performance" coefficient, valid for 1,400–2,000 ft/s and D/T 0.7–1.2.
  - 12" KC against an 870-lb shell: about 1.12–1.16.
  - 14" Class A: about 1.108 at 1,570 ft/s.
- Harvey : Krupp = 1 : 1.216 (velocity basis).
- Equivalences: 11.9" KC ≈ 13" Harvey ≈ 15.5" nickel steel. KC is about 15% more efficient than Harvey above about 5".
- Velocity to defeat at obliquity "varies about as the fourth power of the secant" [UNCERTAIN, probably mis-transcribed; formula given as image only].

---

## 3. Shatter, caps and windscreens

### 3.1 Shatter physics and penalties

**Primary and secondary shatter** are defined in §1.2 ([tech-055](https://www.navweaps.com/index_tech/tech-055.php)).

**Penalty size.** Shatter "greatly reduces penetration and often explosive effectiveness".
- Okun's rule: shatter gives the plate about +30% at normal incidence, falling linearly(?) to 0 at about 55° ([tech-055](https://www.navweaps.com/index_tech/tech-055.php)).
- Above 55° shatter *helps* penetration by suppressing ricochet (a broken nose "digs in").
- FACEHARD: the velocity needed rises by about 30% (20–40%) ([HCWCALC page](https://navweaps.com/index_nathan/stsPlatePenProgram.php)).
- [UNCERTAIN] whether the 30% is on V or on T (§2.2).

**What a shattered shell still achieves**
- **On thin-faced (Harvey) plate**, shatter "merely starts the damage". Most of the loss comes from the later push-through. Shatter alone sometimes gave *better* penetration than no shatter, plausibly only at high obliquity ([AP shell vs Harvey](https://navweaps.com/index_nathan/apShellVsArmor.php)). FACEHARD models "shatter only" and "shatter + maximum damage" separately. On the 10" Harvey example these differ by about 20% in NBL (1981 vs 2390 ft/s).
- **On thick-faced plate**, where the shatter occurs matters more, since damage near the surface "can be amplified deeper in" (same source).
- **German 38 cm L/4.4 against 300 mm KC n/A at 40°** (AKB 9214, per Robertson): penetrated "in broken condition at 550 m/s" but needed about 625 m/s to go through intact ([forum](https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=886270)).
  - [INFERRED] The broken-holing limit is about 12% below the intact limit in V.
  - A model should therefore separate "plate holed by fragments" (blast and fragment effects only, no proper detonation inside) from "intact penetration" (full HE effect).
- **USN acceptance standard**: Class A had to inflict serious nose damage, at minimum "at least 1/3 of the nose gone" ([OS 751](https://navweaps.com/index_nathan/os751BalTests.php)).
  - Late-war shells (Mk 21 Mod 5, Mk 16 Mod 8, Mk 8 Mod 6) were so resistant that this criterion was sometimes dropped.
  - A 26" VH test left the 16" Mk 8 Mod 6 "undamaged apart from windscreen and AP cap" at 1,707 and 1,992 ft/s at about 0° ([tech-040](https://www.navweaps.com/index_tech/tech-040.php)).

**Shatter boundary for hard caps hitting decapping plates** ([tech-085](https://www.navweaps.com/index_tech/tech-085.php)):
> T_shatter-cap/D = 0.515·cos^0.626541(Ob)

This gives 0.515 cal at 0°, 0.471 at 30°, 0.415 at 45° and 0.334 at 60°. It is extrapolated beyond 60° and assumed for all hard caps. HCWCALC uses 0.66 cal (Cap #1) and 0.528 cal (Cap #2) as the minimum cap-shatter thickness at normal against homogeneous, decreasing to 65° ([HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)). [UNCERTAIN: the two documents give different numbers; they apply to different plate contexts.]

### 3.2 The "shatter gap"

**Concept (WWII Army and RAC experience)**
- A capped AP shot that *over-matches* a FH or high-hardness plate shatters and fails at velocities *above* those at which it would have penetrated intact. Penetration probability against velocity becomes non-monotonic.
- British tests, as reported by a WWII-armour researcher: "6 pounder APCBC would shatter and fail when it hit less face-hardened thickness than it could penetrate" ([forum, rexford 2001, citing Krogfus/AFV News and British trials](https://community.battlefront.com/topic/27670-sherman-armor-and-penetration-topics/)).
- US 76 mm APCBC against 3" at 30° showed failure modes at velocities above about 1,954 ft/s.
- The same poster estimates the gap begins around 2,000 ft/s striking velocity, and cites US Navy 76 mm results 5–15% over the 50% limit against 250–490 BHN plate ([battlefront 26109](https://community.battlefront.com/topic/26109-face-hardened-armor-on-german-tanks/)). [UNCERTAIN: secondary forum sources; Bird & Livingston, *WWII Ballistics: Armor and Gunnery* (2001), is the usual primary reference but was not read.]
- Rexford cites German APCBC nose hardness of 61 HRC against US 54.5 HRC as the reason German hits stayed outside the gap. [UNCERTAIN]

**Quantitative modern analogue** ([ARL-TR-1577, Chang & Bodt 1997](https://apps.dtic.mil/sti/pdfs/ADA335629.pdf)):
- Setup: .50 AP M2 against ceramic/Kevlar and against VAR 4340 steel (HRC 48–52).
- Results:

  | Striking velocity (ft/s) | Outcome |
  |---|---|
  | up to about 1,700 | mostly intact behaviour |
  | 1,700–2,000 | shattering increases |
  | 2,000–2,400 | no complete penetrations |
  | above 2,400 | penetration probability recovers |

- Three-state model (Adams & Zwissler):
  > Pc(V) = (1 − Φ_T(V))·Φ_I(V) + Φ_T(V)·Φ_S(V)
  - Φ are normal CDFs.
  - Intact limit I, transition T and shattered limit S have means and σ. For 4340 steel: I = 1425/125, T = 1700/125, S = 1825/125 ft/s.
- **This is a directly usable functional form** for a game's shatter gap [INFERRED].

**Naval relevance [INFERRED]**
- The naval analogue is the band where a shell over-matches face-hardened plate (high V, low-to-moderate T/D) and nose stresses exceed nose or cap strength.
- Okun's notes on Midvale NC shattering Midvale Unbreakable 14"/16" shells, and the Japanese and other cases where a shell "holes but shatters", are the same phenomenon.
- Most naval engagements (striking V about 1,400–2,000 ft/s, T/D about 0.5–1.2) sit near the lower edge of tank-gun gap velocities. Expect a gap for poor-quality or decapped shells near point-blank range, not for good capped WWII shells.

### 3.3 Cap types and how they work

**Soft caps (< ~300 BHN)**
- Makarov's 1894 idea: the Russians tried it in 1894 and most navies followed within a decade ([Naval Gazing Shells 2](https://www.navalgazing.net/Shells-Part-2)).
- They deform and flatten around the nose. The cap absorbs the sideways shock wave so the nose stays intact; the cap then blows away sideways ([tech-055](https://www.navweaps.com/index_tech/tech-055.php)).
- Okun calls the older "shoulder support" and "lubrication" theories wrong.
- They begin to tear free at about **15°** obliquity and are "virtually useless by just over 20°". Any air gap negates them.
- Naval Gazing: about **15% improvement** in penetration over uncapped at obliquity below about 15°; above that they often tore away ([Shells 2](https://www.navalgazing.net/Shells-Part-2)).
- On Harvey plate, soft caps seemed to work at slightly higher obliquity (one 21° test acted "as if the AP cap worked perfectly") ([AP shell vs Harvey](https://navweaps.com/index_nathan/apShellVsArmor.php)).
- **1890s US caps**: Johnson soft-steel cylindrical nose-tip cap, aluminium-bronze, copper contoured, and a 1" soft-steel "French type?" (same source).

**Hard caps (> ~300 BHN)**
- Users: Krupp/Austro-Hungarian post-1911, British post-Jutland "Greenboy", nearly all post-WWI designs ([tech-055](https://www.navweaps.com/index_tech/tech-055.php)).
- They break up rather than deform. They crush into and pit the face, acting as a **centre punch** that thins the plate locally, and they pre-stress the face.
- Obliquity matters little *provided the cap contacts the plate before the nose does*.
- Thicker caps help, with diminishing returns.
- USN 1957 textbook: the cap "increases the biting angle, spreads impact shock over the nose, and pre-stresses the plate" ([NO&G 1957](https://www.eugeneleeslover.com/USNAVY/CHAPTER-4.php)).
- Naval Gazing: hard caps resisted peeling, stayed effective beyond about 20°, and punched a divot in the face ([Armor Part 2](https://www.navalgazing.net/Armor-Part-2)).

**"Super-hard" US caps**: 6" Mk 35 Mods 9–10 and 8" Mk 21 Mod 5 used 650–680 BHN caps, hardened through to the nose surface. That is harder than most KC faces, "very good results" ([tech-055](https://www.navweaps.com/index_tech/tech-055.php); [OS 751](https://navweaps.com/index_nathan/os751BalTests.php)). Mk 21 Mod 3's cap was 555–583 BHN ([britUSGerman](https://navweaps.com/index_nathan/britUSGermanArmorTests.php)).

**Type 1 vs Type 2 hard caps** ([tech-085](https://www.navweaps.com/index_tech/tech-085.php)):
- They are identical in design. Type 2 uses high-melting, extra-strong solder and needs about **2×** the plate to knock it off at 30–45°, and 50–75% more at normal and above 45°.
- Type 2 users: Krupp Psgr. m. K. from the 1911 28 cm L/3.2 onward (30.5 cm, 38 cm L/3.4), USN 3" Mk 29/30, US Army 76.2 mm M62.
- Krupp's high-temperature solder also noted by [Naval Gazing](https://www.navalgazing.net/Shells-Part-2).

**"Tough" caps**: high-nickel steels (up to about 25% Ni), Skoda/Krupp/Russian. FACEHARD treats them as reduced-effectiveness hard caps ([changelog](https://navweaps.com/index_nathan/facehard69ChangeInfo.php)).

**Hoods**
- Light caps used to carry a windscreen on Common/SAP shells.
- They behave as soft caps for decapping and protect the nose against KC (like soft caps), but have no effect against Compound or Harvey ([changelog](https://navweaps.com/index_nathan/facehard69ChangeInfo.php)).
- Tech-045 says a hood "does not protect the nose from shattering against FH" ([tech-045](https://www.navweaps.com/index_tech/tech-045.php)). [UNCERTAIN: the two Okun sources conflict; FACEHARD 6.x is later.]

### 3.4 Cap mass fraction, windscreens, nose and body

**Cap mass as % of total projectile weight**

| Projectile / group | Cap % | Source |
|---|---|---|
| Most capped AP and Common projectiles | 10–16%, averaging about 12% | [tech-055](https://www.navweaps.com/index_tech/tech-055.php) |
| US 6" Mk 35 (late war) | about 20% | same |
| US 8" Mk 21 | about 17% | same |
| German | about 15%, "saddle shape" | [forum, Wahl](https://www.tapatalk.com/groups/warships1discussionboards/comparison-gkdos-100-facehard-m79apclc-t16291-s10.html) |
| British 15" from Greenboy on | about 230 lb (≈12% of 1,938 lb) | [forum](https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=886270) |
| 8" Mk 21 Mod 5 | 63 lb (335 → 271.9 lb body) | [britUSGerman](https://navweaps.com/index_nathan/britUSGermanArmorTests.php) |
| 14" Mk 16 Mod 8 | 187.5 lb (1,500 → 1,312.5) | same |
| 12" Mk 18 | 138 lb (1,140 → 1,002) | same |

**Windscreens and hoods (HCWCALC defaults)** ([HCWCALC](https://navweaps.com/index_nathan/stsPlatePenProgram.php)):
- Windscreen weight basis 5.1%; intact below 0.03 cal of plate (0.015 cal for perforated designs).
- Hood weight basis 5.2%; knocked off below 0.0805 cal and breaks up above 0.1 cal.
- The windscreen is lost on any plate, and the Japanese Type 91 cap head and windscreen even on water impact ([Type 91](https://www.navweaps.com/index_nathan/japType91ProjData.php)).

**Nose shape**
- German 1936–38 tests: round ogives best against high-strength plates, sharp ogives against low-strength. The standard was 1.3 D, chosen for overall performance at 30–45°. Cap hardening depth on the perimeter had "a substantial effect" (Wahl, [forum](https://www.tapatalk.com/groups/warships1discussionboards/comparison-gkdos-100-facehard-m79apclc-t16291-s10.html)).
- For e/d > 1 sharp noses penetrate best (ductile hole enlargement); for e/d < 1 blunt noses do (plugging) ([Hurlich 1951, WAL 710/930-2](https://apps.dtic.mil/sti/pdfs/ADA954868.pdf)).

**Body**: uncapped shells have a rearward CG, giving larger bending moments at obliquity and more breakup ([forum](https://www.tapatalk.com/groups/warships1discussionboards/comparison-gkdos-100-facehard-m79apclc-t16291-s10.html)).

### 3.5 Decapping (affects what reaches the FH main plate)

From [tech-085 "Decapping Revisited"](https://www.navweaps.com/index_tech/tech-085.php), which supersedes [tech-045](https://www.navweaps.com/index_tech/tech-045.php).

**Decapping thresholds (T/D)**

| Obliquity | Soft cap / hood | Type 1 hard cap | Type 2 hard cap |
|---|---|---|---|
| 0–<15° | (not separate) | ≥0.12 always; 0.105–0.12 = 50/50 | ≥0.17 always; 0.155–0.17 = 50/50 |
| 15–20° | step at 15–20° (coin flip) | 0.0805 | as 0° |
| 30–45° | 0.0805 | 0.0805 | ≥0.17; 0.155–0.17 = 50/50 |
| ≥45° | 0.0805 | 0.0805 (to 80°) | ≥0.14; 0.12–0.14 = 50/50 |

**Other decapping rules**
- Intact-cap pass-through limits: 0.35 cal at 30°, falling linearly to 0.25 at 45°, then 0.14 cal at ≥45°.
- Decapping gap (distance to the main plate needed so the nose is bare on arrival): 1–3 calibres at normal, 1 calibre above 30°; ≥0.5 cal for soft and broken caps.
  - The printed coefficient in DGN = 3 − (X − 0.105)(0.111) appears wrong; the described line (3 at 0.105 to 1.6 at 0.26) implies a slope of about 9.03 [UNCERTAIN].
- Laminated plates: T_decap = (T_spaced + T_added)/2, with T_spaced = (ΣT_i^1.4)^0.714.
- Older rule (tech-045): ≥0.0805 cal always decaps; 0.08 = 50%; +50% for shells ≤40 mm.
- Army data: a plate about 1/12 calibre decapped standard APC; Japanese 47 mm APC needed about 1/10 calibre ([WAL 710/930-1, 1950](https://apps.dtic.mil/sti/pdfs/ADA954865.pdf)).

**Consequence**: decapping "greatly reduces penetration at impact angles under 45°" against FH, because the bare nose shatters. Against homogeneous plate, the change is small: mainly the loss of cap weight ([tech-045](https://www.navweaps.com/index_tech/tech-045.php)).
- WAL 1950: shot that is made to shatter gives spaced-armour weight savings of 30–50%. Shot that is only decapped or yawed gives 10–20%, or a *loss* if yaw lowers the obliquity ([ADA954865](https://apps.dtic.mil/sti/pdfs/ADA954865.pdf)).

---

## 4. Shell quality by navy and era (against FH)

### 4.1 Britain

**Pre-Jutland APC**
- Lyddite filler with black-powder boosters and the No. 16 non-delay base fuze ([British fuzes after Jutland](https://navweaps.com/index_nathan/British_Fuzes_after_Jutland.php)).
- Shells were tested only at normal impact. Caps were too soft and tore off, bodies broke up, lyddite detonated on impact, and fuzes lacked delay ([Naval Gazing, Shells at Jutland](https://navalgazing.net/Shells-at-Jutland)).
- The Projectile Committee (report March 1917) found the 15" APC "would break up on a 6" plate at 20°" (same). Since 6" is 0.4 cal, the T/D threshold at 20° was ≤0.4 cal.
- Forum summary: "serious breakage problems" above 15° obliquity ([forum](https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=1095109)).

**Jutland record**
- Of 17 heavy British hits on armour over 9", one penetrated and burst inside.
- Against 6–9" plates, only 3 of 17 penetrated cleanly ([Naval Gazing](https://navalgazing.net/Shells-at-Jutland)).

**Greenboy (1917–18)**
- Heavier and harder caps, stronger bodies, Shellite filler and the No. 16D fuze (0.025 s delay).
- Shellite composition is given as 70/30 lyddite/dinitrophenol ([Okun](https://navweaps.com/index_nathan/British_Fuzes_after_Jutland.php); [tech-017](https://www.navweaps.com/index_tech/tech-017.php)) or 60/40 ([Naval Gazing](https://navalgazing.net/Shells-at-Jutland)) [UNCERTAIN].
- New proof standard: 15" against 10" plate (0.67 cal) at 20°. The 12" had to pass the 6" plate that had defeated the old 15".
- Okun: "much superior to any other AP shells of their time", mainly because they worked at rather large obliquity ([British fuzes](https://navweaps.com/index_nathan/British_Fuzes_after_Jutland.php)).

**Inter-war and WWII**
- 15" Mk V.A (1920) defeated 10" C at 40°.
- Cardonald 15" proof: 12" C at 40° (0.8 cal).
- Hadfields 14" "could be pushed up to 10" C at 45°".
- Cardonald against Blue Band at 40°: 1,899 vs 1,652 ft/s (per Chodnicki's FACEHARD runs).
- All forum figures ([forum](https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=886270)) [UNCERTAIN, forum].

**FACEHARD tables** ([British tables](https://www.navweaps.com/index_nathan/Penetration_Britain.php)): 15"/42 at 0 yd and 2,467 ft/s against US Class A ("EFF" column):

| Shell | EFF (in) |
|---|---|
| Greenboy Mk 3A | 33.2 |
| Blue Band Mk 5A | 33.2 |
| Mk 17B | 34.1 at 2,458 ft/s |

- So the three British generations differ by about 3% intact-effective at short range.
- At 20 kyd: Greenboy 9.7" EFF against 14.4" (Blue Band) and 17.7" (Mk 17B at higher V).
- [INFERRED] The obliquity tolerance of the Greenboy design is far worse in FACEHARD's model at plunging angles.

### 4.2 Germany

**WWI**
- In 1911 Krupp replaced lyddite with block TNT and introduced Type 2 high-temperature-solder hard caps (from the 1911 28 cm L/3.2) ([tech-017](https://www.navweaps.com/index_tech/tech-017.php); [tech-085](https://www.navweaps.com/index_tech/tech-085.php)).
- Insensitive TNT with a small booster gave many duds and low-order bursts. The design fuze delay was 0.025 s, but Okun estimates at most half achieved anything near it ([British fuzes](https://navweaps.com/index_nathan/British_Fuzes_after_Jutland.php)).
- In body and cap terms, the German APC was the better WWI design: British shells broke up at 15–20° while German ones held together [INFERRED from the above; consistent with the brief].

**WWII**
- Much tougher starting steel "greatly improving penetration". The same "tuna-can" base plug, nose-only sheath hardening, and block TNT ([tech-017](https://www.navweaps.com/index_tech/tech-017.php)).
- Post-war USN tests: Krupp L/4.4 16" shells were "more prone to breakup on Class A impacts", with a weak ring behind the forward bourrelet (Okun, [forum](https://www.tapatalk.com/groups/warships1discussionboards/comparison-gkdos-100-facehard-m79apclc-t16291-s10.html)).
- German shells "showed a greater tendency to break when penetrating heavy side armor starting from about 20° obliquity upwards" (Robertson, [forum](https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=886270)).
- Acceptance tests often used the *pre-WWI* 30.5 cm L/3.4 Psgr. m. K. for KC n/A plates (delcyros, [forum](https://www.tapatalk.com/groups/warships1discussionboards/changing-views-on-kc-penetration-formulas-t49927.html)).

### 4.3 United States

**WWI era**
- Soft-capped 12" shells.
- Midvale "Unbreakable" 14"/16" AP shattered against Midvale NC plate ([tech-017](https://www.navweaps.com/index_tech/tech-017.php)).
- The US adopted Dunnite (Explosive D) for APC ([Naval Gazing](https://www.navalgazing.net/Shells-Part-2)).

**Post-1930**
- Hard caps; tetryl booster (1928).
- Fuze delays of 0.033–0.035 s ([British fuzes](https://navweaps.com/index_nathan/British_Fuzes_after_Jutland.php)).

**Late WWII mods**
- 6" Mk 35 Mod 9/10, 8" Mk 21 Mod 5 (super-hard caps), 14" Mk 16 Mod 8 ("best all-round BB AP"), 16" Mk 8 Mod 6 ("best super-heavy") ([OS 751](https://navweaps.com/index_nathan/os751BalTests.php)).
- These were so damage-resistant that USN plate acceptance had to drop the nose-damage criterion for them.
- Okun: US AP projectiles were "the best at resisting damage, though sometimes slightly weaker in penetration" ([Okun, Bismarck](https://www.combinedfleet.com/okun_biz.htm)).

**Generation effect in FACEHARD** ([US tables](https://www.navweaps.com/index_nathan/Penetration_United_States.php)):

| 16"/45 shell | US Class A PP at 0 yd | at 20 kyd |
|---|---|---|
| Mk 8 Mods 1–5 | 34.0" | 19.0" |
| Mk 8 Mods 6–8 | 37.8" | 21.1" |

That is a **≈+11% quality step** from late-war metallurgy at the same weight and velocity.

**14" Mk 16 Mod 8**: its superiority over Mod 4 came from nose and body toughness, not the cap ([tech-101](https://www.navweaps.com/index_tech/tech-101.php)).

### 4.4 Japan (Type 91)

- The design kept the circa-1921 British 15" Mk V test spec: **20° against 0.67-cal VC plate**. Most foreign WWII tests were at 30°, and US tests up to 35–40° ([Type 91 page](https://www.navweaps.com/index_nathan/japType91ProjData.php)).
- Okun: "None were up to most foreign designs of the same period in AP capability". Priorities were the underwater-hit concept and low drag.
- The 46 cm cap improvements aimed at 30–35° were "rather small, if any".
- The cap head and windscreen are knocked off by water; FACEHARD handles cap-head loss.
- The 20.3 cm uncapped shell nonetheless had "excellent homogeneous armor penetration".
- Filler: TNA with up to 40% cavity cushioning, and a lyddite booster ([tech-017](https://www.navweaps.com/index_tech/tech-017.php)).

### 4.5 France and Italy

**France**
- 5-calibre-long APC; minimum-drag shape.
- Caps were thinner than most foreign WWII caps except over the tip. Hardened and pointed, with the lower edge forced into a groove ring ([tech-101](https://www.navweaps.com/index_tech/tech-101.php)).
- The US-made 380 mm Mk 1 "Crucible" shell (Mk 16 Mod 8-like cap, 555 → 225 BHN) was "virtually identical in penetration to the US shells they were based on" in French post-war tests against KC.
- Okun does not rate the 1936 French shell explicitly [UNCERTAIN].

**Italy**
- Very little published. 381 mm about 4.46 cal long; the 320 mm assumed to be a scaled 381 ([tech-101](https://www.navweaps.com/index_tech/tech-101.php)).

### 4.6 Suggested shell-quality multipliers [INFERRED from the above]

Multiplier on effective penetration (thickness) against FH at ≤30°, relative to a WWII US Mk 8 Mods 1–5-class shell = 1.00:

| Shell family | Factor | Basis |
|---|---|---|
| Uncapped steel AP (1890s–1900s) vs KC/Harvey | 0.70–0.77 when it shatters (≈1/1.3–1/1.37), 1.0 if not | Okun shatter +30% (V or T) |
| Soft-capped (1895–1914), Ob ≤15° | 0.90–0.95 | soft cap ≈+15% over uncapped below 15°; tears off above ~20° |
| British pre-1917 APC (lyddite) | 0.85 at 0–10°; treat as shatter (0.7) at ≥15–20° when T/D ≳ 0.4 | Projectile Committee; Jutland record |
| German WWI APC (Type 2 cap, TNT) | 0.95; filler duds high | tech-017, tech-085 |
| Greenboy / inter-war British, Japanese Type 91 | 0.95 at ≤20°; degrade above 20–30° | Type 91 20° spec; Greenboy 20° spec |
| WWII British Mk 17B, German L/4.4, US Mods 1–5 | 1.00 (German −5% above 20° for breakup) | forum, Okun |
| US late-war (Mk 8 Mod 6, Mk 16 Mod 8, super-hard caps) | 1.08–1.11 | FACEHARD 34.0 → 37.8 |

---

## 5. Obliquity limits for FH

**Function of FH armour.** Class A was for vertical surfaces (belts, turrets, barbettes) where obliquity is small. Plate defeats the shell "by stopping it, breaking it up, or rupturing its explosive cavity". Homogeneous Class B is for decks and other high-obliquity surfaces, relying on ricochet ([NO&G 1957](https://www.eugeneleeslover.com/USNAVY/CHAPTER-4.php)).
- Class A was never used for horizontal protection facing highly oblique impacts ([OS 751](https://navweaps.com/index_nathan/os751BalTests.php)).
- FH performs poorly against glancing impacts because the brittle face is counter-productive there ([Wikipedia Krupp](https://en.wikipedia.org/wiki/Krupp_armour)).
- Richelieu's FH turret tops are the only horizontal FH known to Naval Gazing ([Armor Part 2](https://www.navalgazing.net/Armor-Part-2)).

**Cap limits**
- Soft cap: effective to about 15°, useless by about 20°.
- Hard cap: effective as long as it contacts first ([tech-055](https://www.navweaps.com/index_tech/tech-055.php)).
- Intact-cap pass-through falls to 0.14 cal at ≥45° ([tech-085](https://www.navweaps.com/index_tech/tech-085.php)).

**Shatter benefit to the plate**: maximum at 0°, zero at about 55°. Above 55° shatter *aids* penetration by suppressing ricochet ([tech-055](https://www.navweaps.com/index_tech/tech-055.php)). Harvey with a 20% face "showed little effect on APC shells unless obliquity reached about 40°" ([AP shell vs Harvey](https://navweaps.com/index_nathan/apShellVsArmor.php)).

**Test standards (proxies for where shells held together)**

| Era | Standard |
|---|---|
| British pre-1917 | 0° only |
| Greenboy / Japanese Type 91 | 20° at 0.67 cal |
| British WWII, most foreign | 30–40° at about 0.8 cal |
| US | 35–40° at about 1 cal ([Type 91](https://www.navweaps.com/index_nathan/japType91ProjData.php)) |
| German KC n/A acceptance | 30° with a half-calibre shell ([tech-017](https://www.navweaps.com/index_tech/tech-017.php)) |

**Effect of obliquity on velocity**
- US Thompson-F standard, V(Ob)/V(0) at T/D = 1 [INFERRED by calculation]:

  | Ob | 10° | 20° | 30° | 40° | 50° | 60° |
  |---|---|---|---|---|---|---|
  | V(Ob)/V(0) | 1.02 | 1.09 | 1.23 | 1.45 | 1.83 | 2.51 |

- FACEHARD example: a plate at 45° needed a shell about 22% faster than a 1.414× thicker plate at 0° of equal weight per area ([Naval Gazing Armor 2, fn. 2](https://www.navalgazing.net/Armor-Part-2)).
- Example test series, Bethlehem 11.37" Class A against a 14" shell: CP-E at 20° and 1,392 ft/s; IP at 30° and 1,578 ft/s; IP-X (shattered) at 35° and 1,680 ft/s ([14" Mk16 page](https://navweaps.com/index_nathan/14inchMk16Mod4VsClassA.php)).

**Ricochet** against FH is not separately parameterised in the sources read. Okun's FH obliquity curve comes from German GKdos 100 data. One observed feature: German *base-fuzed HE* curves begin only at about 23.5–31°, against APC at 20°, meaning breakup at shallower angles for thin-walled shells ([forum](https://www.tapatalk.com/groups/warships1discussionboards/comparison-gkdos-100-facehard-m79apclc-t16291-s10.html)). [INFERRED] Use the deck/ricochet model from the homogeneous research, with a modest FH bonus to ricochet for intact shells.

---

## 6. Data

### 6.1 FH armour quality by nation and era

**Okun's "Q"** (US late-period = 1.00; defined at 8" plates, normal impact). Reported by Robertson ([forum](https://www.tapatalk.com/groups/warships1discussionboards/different-definitions-of-armor-quality-t34968.html)):

| Armour | Q | Back layer (BLT) |
|---|---|---|
| US Class A (WWII) | 1.00 | 45% |
| Italian Terni TC | 0.98 | 55% (?) |
| German KC n/A | 0.96 | 59% |
| British CA (post-1930) | 0.928 | 70% |
| French (assumed) | 1.00 | 65% |
| Japanese VH | "not comparable" (WWI-era spec) | — |

- Robertson: for a given era the Q values "are primarily artefacts of the rather arbitrary definition", because the 8" baseline favours thick-faced plate.

**Same armours against a 16" shell** [INFERRED by calculation from published FACEHARD 6.8 outputs].
- Velocity to defeat a 16" plate at 30° with the 16" Mk 8, from a delcyros FACEHARD run ([forum](https://www.tapatalk.com/groups/warships1discussionboards/comparison-gkdos-100-facehard-m79apclc-t16291-s10.html)).
- Converted to thickness-equivalent quality relative to WWII US Class A using V^1.21:

| Armour | V (ft/s) | Rel. V | Thickness-equiv. Q vs US WWII Class A |
|---|---|---|---|
| Midvale NC Class A (1907–12, 1922) | 1224 | 0.789 | 0.75 |
| US pre-1911 average Class A | 1524 | 0.983 | 0.98 |
| Japanese VH | 1539 | 0.992 | 0.99 |
| US WWII Class A (1935–43) | 1551 | 1.000 | 1.00 |
| US post-1944 Class A | 1583 | 1.021 | 1.03 |
| US average Class A (1911–25) | 1614 | 1.041 | 1.05 |
| German KC n/A | 1666 | 1.074 | 1.09 |
| Bethlehem Thin Chill (1921–25) | 1672 | 1.078 | 1.10 |
| British CA | 1727 | 1.113 | 1.14 |
| Italian Terni TC | 1806 | 1.164 | 1.20 |

- Cross-check: the navweaps 16"/45 Mk 2B FACEHARD table ([British tables](https://www.navweaps.com/index_nathan/Penetration_Britain.php)), "H" column at 0/10/20 kyd, gives identical ratios against US Class A: British 1.14, Terni 1.20, KC n/A 1.04 ("H") / 1.09 ("EFF"), VH 0.99.
- At 16" calibre the ranking **reverses** the 8"-based Q ordering. This is the FH scaling effect in numbers.
- A related run against 13.5" plate (ikeda): with the 12" shell, British post-1930 > US 1944–50 > US 1935–43 > Bethlehem Thin Chill > Midvale NC. With the 14" Mk 16, British is still best. The plate ranking shifts with shell size ([forum](https://www.tapatalk.com/groups/warships1discussionboards/comparison-gkdos-100-facehard-m79apclc-t16291-s10.html)).
- [UNCERTAIN] Chodnicki reports that with Q forced to 1.0, British armour comes out about 25% stronger than US. He suggests the low British Q may be a normalisation ([forum](https://www.tapatalk.com/groups/warships1discussionboards/different-definitions-of-armor-quality-t34968.html)).

**Japanese VH, from test**
- 26" Shinano plate: NBL 1,839 ft/s ±3% at 0° against the 16" Mk 8 Mod 6. Estimated quality 0.839 vs US Class A; "about equal to the best WWI British KC" ([tech-040](https://www.navweaps.com/index_tech/tech-040.php)).
- An experimental 7.21" VH plate (#3133) was "the best plate of its thickness range ever tested by the U.S. Navy" (same; [forum](https://www.tapatalk.com/groups/warships1discussionboards/different-definitions-of-armor-quality-t34968.html)).
- The VH 0.839 (26", 16" shell) vs 0.99 (16" plate, FACEHARD) discrepancy is plausibly thickness and brittleness [UNCERTAIN].

**German KC n/A against US Class A in 1946–47 tests** ([britUSGerman](https://navweaps.com/index_nathan/britUSGermanArmorTests.php)): four KC plates "significantly superior" to the closest US plate, one about equal, one "significantly inferior".

| KC n/A plate | Shell, obliquity | % of USN standard | US average |
|---|---|---|---|
| 8.5" | 8" Mk 21-5, 30° | 113% | — |
| 10" | 12" Mk 18, 35° | 106% | 97% |
| 14" | 14" Mk 16-8, 30° | 95% | 90% |
| 15" | 14" Mk 16-8, 30° | 97.5% | 90% |
| 10" | 14", 35° | <82% | 87% |

**US makers (WWI era)** ([tech-017](https://www.navweaps.com/index_tech/tech-017.php)):
- Carnegie CKC ≈ slightly better than captured Krupp KC against soft-capped 12".
- Bethlehem BNC was "very brittle and of minimum acceptable quality".
- Midvale MNC was +30% against 12" soft-capped, but collapsed against 14"/16".
- After WWI, British and several US WWI KC armours were found superior to Krupp's own WWI KC a/A (same).

**Historic progression (thickness-equivalent factors)**

| Comparison | Factor | Source |
|---|---|---|
| Mild steel / compound vs wrought iron | +25% | [Naval Gazing Armor 1](https://www.navalgazing.net/Armor-Part-1) |
| Nickel steel vs mild steel | +5% | same |
| Harvey vs nickel steel | +15–20% (13" Harvey = 15.5" Ni-steel, i.e. 1.19) | Naval Gazing; [Wikipedia Harvey](https://en.wikipedia.org/wiki/Harvey_armor); [NO&G 1937](https://eugeneleeslover.com/ARMOR-CHAPTER-XII-C.html) |
| Early Krupp vs Harvey | +15% (10.2" KC = 12" Harvey, i.e. 1.18; NO&G 1937: 11.9" KC = 13" Harvey, 1.09; velocity ratio 1.216) | [Wikipedia Krupp](https://en.wikipedia.org/wiki/Krupp_armour); NO&G 1937 |
| Best WWII armours vs early Krupp | up to +40% | [Naval Gazing Armor 1](https://www.navalgazing.net/Armor-Part-1) |
| Figure of merit (vs wrought iron): 12" KC ≈ 28" WI | 2.33 | [forum, Robertson](https://www.tapatalk.com/groups/warships1discussionboards/different-definitions-of-armor-quality-t34968.html) |

### 6.2 USN Class A acceptance velocities

**Plate tests**, O.S. 751 Rev. K, 1947. Velocities (ft/s) the plate must resist ([OS 751](https://navweaps.com/index_nathan/os751BalTests.php)); * = weaker earlier Mod.

| T (in) | 6" Mk35-9 30° | 8" Mk21-5 30° | 12" Mk18 35° | 14" Mk16-8 30° | 14" Mk16-8 35° | 16" Mk8-6 30° | 16" Mk8-6 35° |
|---|---|---|---|---|---|---|---|
| 4.0 | 1360 | — | — | — | — | — | — |
| 5.0 | 1580 | 1150 | — | — | — | — | — |
| 6.0 | 1800 | 1305 | — | — | — | — | — |
| 8.0 | — | 1620 | 1220 | — | — | — | — |
| 10.0 | — | — | 1430 | — | 1265 | — | — |
| 12.0 | — | — | 1640 | 1410 | 1460 | — | — |
| 14.0 | — | — | — | 1610 | 1660 | 1435* | 1500 |
| 16.0 | — | — | — | 1810 | — | 1595* | 1660 |
| 18.0 | — | — | — | 2005 | — | 1750* | 1815 |

**Projectile tests**, O.S. 696: the shell must penetrate Class A *effective* ([OS 696 specs](https://navweaps.com/index_nathan/os696BalSpecs.php)). The plates are Harvey-process Army plates.

| Shell | Requirement |
|---|---|
| 8" Mk 21, 30° | 5.0" at 1,290 ft/s to 6.5" at 1,570 |
| 12" Mk 18, 35° | 9" at 1,580 to 13" at 2,160 |
| 14" Mk 16/20, 35° | 11" at 1,810 to 16" at 2,480 |
| 16" Mk 5/12, 35° | 11" at 1,620 to 17" at 2,330 |
| 16" Mk 8, 35° | 11" at 1,490 to 17" at 2,130 |

**Thickest Class A test (1942)**: 20.64" plates against the 16" Mk 8 Mod 3 at about 30°.
- Bethlehem, 52% face: penetrated at 2,101 ft/s, but the base snapped off.
- Midvale, 41% face, 522 BHN surface: stopped the shell at 2,011 and 2,084 ft/s with the nose broken.
- Estimated acceptance about 2,000 ft/s ([OS 751](https://navweaps.com/index_nathan/os751BalTests.php)).

### 6.3 FACEHARD range tables (excerpt)

16"/45 Mk 8 against US Class A ([US tables](https://www.navweaps.com/index_nathan/Penetration_United_States.php)):

| Range (kyd) | Angle of fall | V (ft/s) | Mods 1–5 PP / EFF | Mods 6–8 PP / EFF |
|---|---|---|---|---|
| 0 | 0° | 2300 | 34.0 / 30.3 | 37.8 / 33.7 |
| 10 | 6.8° | 1900 | 25.4 / 22.7 | 28.3 / 25.2 |
| 20 | 17.9° | 1604 | 19.0 / 16.9 | 21.1 / 18.8 |
| 30 | 34° | 1490 | 14.6 / 13.0 | 16.2 / 14.4 |
| 36 | 47.2° | 1539 | 12.4 / 10.0 | 13.2 / 11.7 |

Note: the tables assume target obliquity equals angle of fall (vertical plate, no target heel or course angle) [INFERRED].

### 6.4 1890s Harvey test examples (calibration points for the early period)

From [AP shell vs Harvey](https://navweaps.com/index_nathan/apShellVsArmor.php):
- 10" capped AP (500 lb): FACEHARD APC NBL 2,080 ft/s against an estimated actual ~2,100.
- Cleland Davis capped formula gives 2,558 ("way too high").
- The homogeneous M79 formula gives 1,936 ("way too low").
- **This shows the error from applying homogeneous formulas to FH (about −8%) or the period Davis formula (about +22%) at T/D ≈ 1.8.**

---

## 7. Practical modelling advice: a single formula with an FH multiplier

**Approach.** Assume the game uses one homogeneous-style law (for example T/D = k·(W/D³)^a·V^b·g(Ob)) plus a plate-quality multiplier M on thickness.

**Recommended effective-thickness multiplier M_FH (FH relative to homogeneous Class B/STS of the same era) [INFERRED]:**

| Condition | M_FH | Rationale |
|---|---|---|
| Uncapped or decapped steel shell, Ob ≤ 30°, T/D ≥ 0.5 | 1.35–1.45 (shell usually shatters) | shatter +30% (V basis → ×1.37 on T); Krupp C ratio ≈1.24 vs weak APC plus scatter |
| Same, Ob 30–55° | linearly down to 1.0 at 55° | Okun shatter bonus → 0 at 55° |
| Soft-capped, Ob ≤ 15° | 1.10–1.20 | cap ≈+15% over uncapped |
| Soft-capped, Ob ≥ 20° | treat as uncapped | caps tear off |
| Good hard-capped WWII shell, Ob ≤ 35°, T/D 0.5–1.3 | 0.95–1.10 (default 1.0–1.05) | USN graded A and B on the same Thompson-F scale, within ±10% |
| Good hard-capped, Ob 35–55° | 0.95–1.0 | FH loses relative effectiveness at obliquity; brittle face counter-productive |
| Any shell, Ob > 55° | 0.85–0.95 | FH worse than homogeneous for glancing hits; shatter aids digging in |
| T < ~0.3 cal or < ~3–4" absolute | 0.8–0.9 | thin back; non-KC thin "Class A" |
| Calibre scaling (thick-face, BLT ≤ 50%) | × (D/8")^(−0.1 … −0.15) beyond 8" | 55%-face US Class A ~10–15% weaker at 14–16" than at 6–8" |
| Calibre scaling (thin-face, BLT ≥ 65%) | ≈1.0 | Okun: scaling small above 65% BLT |

**Errors a single-multiplier model introduces**
1. **Wrong velocity sensitivity.** FH resistance scales as T ∝ V^1.21 (and W^0.2 at fixed D). Homogeneous De Marre-type laws use V^1.4 and W^0.7.
   - Calibrating at mid-range and applying at point-blank (or vice versa) gives errors of roughly ±8–15%. Example: with a 1.4 exponent, a velocity ratio of 1.5 gives 1.5^1.43 = 1.79 against 1.5^1.21 = 1.63, a 10% gap [INFERRED].
   - Heavy-shell advantage is overstated against FH. The super-heavy US 2,700 lb shell gains less against Class A than against Class B.
2. **No shatter discontinuity.** A multiplier cannot reproduce the step when a cap is lost or a shell fails, which is worth about 30% (V) or 37% (T). Nor can it reproduce the non-monotonic shatter gap.
3. **Rank reversal with calibre.** A nation multiplier fitted at 8" (Okun Q) ranks US Class A first. At 16" it ranks it 4th of 5 (§6.1). Q must depend on calibre or T/D and on BLT, or a single multiplier will be wrong by up to about 15–20%.
4. **Obliquity interaction.** FH and homogeneous cross over between about 35° and 55°. A constant multiplier is wrong at one end.
5. **Holed vs effective.** FH often yields "holed but shell broken" outcomes. Okun's EFF thickness is about 10% below the PP/H thickness. A pass/fail model should output at least three states: stopped, holed (fragments and blast only), and penetrated effective.

---

## What a model needs from this

**Plate parameters**

| Parameter | Suggested default |
|---|---|
| `plate.type` | {wrought iron, compound, steel/Ni-steel, Harvey, KC-type FH, non-cemented FH (Midvale/VH), homogeneous Cr-Ni} |
| `plate.face_frac` (chill depth fraction) | KC a/A 0.34; KC n/A 0.41; British CA 0.30; US Thick Chill 0.55; Bethlehem Thin Chill 0.18; Midvale NC 0.80; VH about 0.4 [UNCERTAIN]; Terni about 0.45 (Robertson BLT 55%?) [UNCERTAIN]. Harvey: 1"/T. |
| `plate.BLT` (back-layer thickness) | 1 − face_frac. Drives scaling. Scaling switches on below 0.65. |
| `plate.Q` (thickness-equivalent quality vs WWII US Class A at 8") | US Class A 1.00; Terni 0.98; KC n/A 0.96; British CA 0.928; French 1.00 (assumed); VH 0.84–0.99. Pre-WWI KC about 0.85–0.9 [INFERRED from "best WWI British KC ≈ VH 0.839"]. Harvey ≈ KC/1.15–1.2. |
| `plate.scaling(D)` | S = 1 for BLT ≥ 0.65; else S = (D/8")^(−κ) with κ ≈ 0.1–0.2·(0.65 − BLT)/0.2 [INFERRED]. This should reproduce Q(16") ≈ US 1.00, KC n/A 1.09, British 1.14, Terni 1.20 relative to US Class A. |
| `plate.min_FH_thickness` | 4" (3.2" Krupp/Witkowitz pre-1918). Below that, use thin-plate behaviour (M_FH ≈ 0.85). |
| `plate.face_hardness` (only to decide whether a cap out-hardens the face) | 600–700 BHN. British WWII 600, Krupp 670–680, US cap 650–680. |

**Projectile parameters**

| Parameter | Suggested default |
|---|---|
| `shell.cap` | {none, hood, soft, hard_type1, hard_type2, tough, superhard} |
| `shell.cap_frac` | 0.12 (range 0.10–0.20) |
| `shell.windscreen_frac` | 0.05 |
| Decapping (T/D) | Type 1: 0.0805 at ≥15°, 0.105–0.12 at <15°. Type 2: 0.155–0.17 (0.12–0.14 at ≥45°). Soft/hood: 0.0805. |
| Cap obliquity limit | soft 15–20°; hard: effective while intact (intact pass-through ≤0.35 cal at 30°, ≤0.14 cal at ≥45°) |
| Cap shatter on decapping plate | T/D ≥ 0.515·cos^0.627(Ob) |
| `shell.quality` (thickness multiplier, §4.6) | Uncapped 1890s 0.75 (on shatter); British pre-1917 0.85 (shatters ≥15–20° when T/D ≳ 0.4); German WWI 0.95; Greenboy/Type 91 0.95 (limit 20–30°); WWII standard 1.00; US late-war 1.10 |
| `shell.oblique_spec` (obliquity up to which the body survives a calibre-thick FH plate) | British pre-1917 ~10–15°; Greenboy and Type 91 20°; German WWII 20–30° (breakup above); British WWII 40°; US late-war 35–45° |

**Core laws**
- FH penetration: T/D = C·Q⁻¹·(W/D³)^0.2·V^1.21·g_FH(Ob)·S(D). Calibrate C against §6.2/§6.3.
- Alternative: the Thompson-F standard (§2.3) as a reference scale with a per-plate "% NBL" multiplier. It reproduces USN WWII grading exactly.
- Obliquity g_FH(Ob): use the Thompson-F ratio as the default (V multipliers 1.02 / 1.09 / 1.23 / 1.45 / 1.83 at 10–50° for T/D = 1).
- Shatter penalty: required V × 1.30 (range 1.20–1.40) at 0°, linear to ×1.00 at 55°, and a slight bonus (<1) above 55°. Applies when the shell is uncapped, decapped, soft-capped above 20°, or of poor body quality.
- Shatter gap (optional): the three-state Pc(V) = (1 − Φ_T)·Φ_I + Φ_T·Φ_S, with σ ≈ 5–7% of V. Use it for over-matching high-V hits on FH.

**Output states and suggested gaps**
- {stopped/rebound, holed-shell-broken (fragments, partial blast, no proper detonation inside), penetrated-effective}.
- Holing limit ≈ 0.88–0.95 × the effective limit in V. The German 38 cm example gives 0.88. FACEHARD H/EFF thickness ratios run about 1.01–1.12.

**Validation targets**

| Case | Target |
|---|---|
| 16" Mk 8 Mod 6 vs 26" VH, 0° | NBL 1,839 ft/s ±3% |
| 14" Mk 16 vs 13.5" Class A, 30° | NBL 1,806 ft/s (USN standard) |
| 8" Mk 21 vs 8.5" plate, 30° | NBL 1,793 ft/s |
| 16" Mk 8 Mods 1–5 at 20 kyd (1,604 ft/s, 17.9°) | about 19.0" Class A PP |
| 15" APC pre-1917 vs 6" KC, 20° | must break up |
| Greenboy 15" vs 10" plate, 20° | must pass |
