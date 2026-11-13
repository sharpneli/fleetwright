# 16 — Shells Skipping off the Water: What a Ricochet Kept, Where It Went, and Whether It Hit
Status: draft    Updated: 2026-11-13    Request: [REQUESTS.md "Shells skipping off the water"](../../REQUESTS.md) (the code: [docs/battle/shells-armour-damage.md](../../../docs/battle/shells-armour-damage.md) E7 "The ricochet", `ShellSystem.Skip`, `SimSettings.WaterRicochet*`)

*Four questions about a pointed shell that meets the sea flatter than the ricochet limit: (1) how much speed it keeps and the angle it leaves at; (2) how erratic the skip is, whether the shell yaws or tumbles after, and how far it flies; (3) whether the fuze starts on the graze; (4) how often ricochets hit ships in the record, and what a yawed shell does to plate. No navy's trials of big shells skipping off the sea were found. The answers come from a ladder of evidence: the ricochet physics, small-arms and 20 mm firings into water (the closest measured bodies), a handful of naval observations, and the record's ricochet hits.*

Tags as in the other damage docs: **[SRC]** sourced, **[INFERRED]** my reasoning or a fit, **[EST]** an engineering estimate with no direct source, **[UNCERTAIN]** thin or conflicting sources.

**Companion code:** [`water_ricochet_ref.py`](water_ricochet_ref.py) (pure Python, about 2 minutes, most of it W4's Monte Carlo). It covers: shells' bulk density and the sphere and cylinder limits (W0); the falls and striking speeds of the record's cases by a point-mass flight fitted to each gun's maximum range (W1); speed kept, the code against the measured fits (W2); where a skipped shell goes (W3); hits after a skip by exit law (W4); the graze's deceleration and the RN's 1921 nose-fuze minima (W5); yaw (W6). Every derived number below comes out of it.

---

## Summary

1. **The USNI 1921 "same angle" isn't a skip off the sea.** Hawkesworth's 6 in example is a shell fired level into a water tank, striking the tank's *floor* at 3°40' after 255 ft under water. He *calculates* a rebound "at the same or nearly the same angle". He then says that overlooks the energy the blow absorbs and that it "likely lowers the rebound angle" [SRC USNI Feb 1921]. It is not evidence for the surface skip's exit angle.

2. **A pointed shell doesn't mirror; it leaves steeper than it fell, and the spread is wide.**
   - **Spheres** come off slightly flatter: brass balls at 620 m/s leave at 0.9 × the entry angle (Ramsauer) [SRC via Johnson 1998].
   - **Elongated bodies** off water leave *steeper* than they met it at low angles, the excess shrinking as the entry steepens [SRC]:
     - Forensic work: yielding surfaces (water, sand) give ricochet angles *greater* than the incidence; hard ones give smaller [SRC AAFS 2010, 2012].
     - BRL (cal .50 AP and 20 mm): the ratio of the exit angle to the entry angle "rapidly decreases" as the entry steepens, and falls with sectional density [SRC AD0479824, snippet only].
     - L/D 5 bodies at 95-172 m/s leave at about 15° whatever they met it at (15.4 − 0.16 θ) [SRC Chen 2020].
     - An anecdotal pistol series: 5° in → 12° and 26° out; 8.5° → 9.5° and 14° [UNCERTAIN, American Handgunner].
   - **The naval glimpses:** a USN 5"/50 at 3,000 ft/s, "almost point blank" at 2,500 yd (fall about 1°, W1): ricochets "reaching a great height, sometimes curving upward a thousand yards or more", then "a series of vast sweeping curves extending miles beyond the target" [SRC Scientific American 1911]. Albert W. Grant's 6 in ricochet hit in 1944 had a path "slightly upward" [SRC WDR 51].
   - **Why:** the water's pressure on the lower face of the ogive acts ahead of the centre of mass, so the shell pitches nose up while it planes and leaves yawed, rising. The rotation ∝ ρ_water/ρ_shell and doesn't depend on size or speed [INFERRED: geometric similarity], so bullets and 20 mm are fair models for shells if the shape and twist in calibres are alike (they are: about 1 turn in 30 calibres).
   - **Proposed** [EST]: exit median **5° + ½ × fall**, lognormal σ 0.6 (5-95%: ×0.37 to ×2.7), capped at 35°.

3. **Speed kept: 0.95 at 2°, 0.65-0.8 at 5°, half or less near 7-8°.**

   | Fall | code k = 1 | code k = 2 | .50 M2 off water | BRL .50 AP / 20 mm | Steel spheres |
   |---:|---:|---:|---:|---:|---:|
   | 2° | 0.93 | 0.87 | 0.95 | | |
   | 4° | 0.87 | 0.76 | 0.78 | | |
   | 5° | 0.84 | 0.71 | 0.66 | 0.72 | 0.81 (mean) |
   | 6° | 0.81 | 0.66 | 0.53 | | |
   | 8° | 0.76 | 0.57 | 0.30 | | |

   - **Sources:** .50 M2 is the Weibull fit to Baillargeon & Bergeron's pool firings, kept = exp(−(θ/7.37°)^2.258) [SRC Mirshak & Beech 2018]. BRL at 5°: 1,809 ft/s after the skip against 2,515 in air [SRC AD0479824]. Spheres [SRC Soliman, Reid & Johnson 1976].
   - **For shells:** the loss rises steeply toward the limit, which `exp(-2θk)` doesn't. Scaled by √SG to a shell's lower density (W0), the .50's 7.37° becomes about 9.5° (0.79 at 5°) [INFERRED].
   - **Proposed:** kept = exp(−(θ/8.5°)^2.3), range 7.4-9.5° for the scale [EST]. That is 0.95 at 2°, 0.72 at 5°, 0.43 at 7°. As a one-line change, `WaterRicochetLossK` 1.5 (0.77 at 5°) is inside the spread at 4-6° and too lossy only below 2°.

4. **Across: mostly to the right, sometimes wild.** Spin gives a skip a sideways kick in the sense of the twist:
   - "Naval operators commonly report that when projectiles impact the ocean surface, they ricochet mainly to the right but that some rounds ricochet in wildly unpredictable directions" [SRC Mirshak & Beech 2018].
   - Their pool data put most .50 ricochets about 10° to the right [SRC]. A 9 mm burst across a lake went about 10° right at each skip [UNCERTAIN]. BRL found most rounds after the skip pointing "upward and to the right" [SRC]. Hawkesworth expected his 6 in to "corkscrew" right [SRC].
   - Waves make it wild: wave faces along the line of fire cut ricochets and their range; waves across it hardly matter [SRC Mirshak & Beech]. Douglas (1855) limited sea ricochet to calm water for the same reason [SRC via Johnson 1998].
   - **Proposed** [EST]: mean 5° right, σ 4°, instead of 3° each way.

5. **How far: kilometres, not 300-800 m.**
   - A 12 in at 4 km (fall 2.2°, 674 m/s) leaving at 2-5° grazes again 2.6-5.6 km on; even a 1° exit flies 1.4 km (W3).
   - Round shot did the same: 26 cm Krupp shells grazed frozen ground at 1,500 m and bounded a further 8,000 m. Excellent's 1838 tables give up to 34 grazes, first at 410 yd and last at 2,943 yd [SRC via Johnson 1998].
   - The splash note's "300-800 m downrange" second splash (vfx/shell-splashes.md) has no source, and its 1911 citation is the 5"/50 battery, not 12 in practice. The source says "miles beyond the target".

6. **The limit is likely higher than 6.4° for pointed shells.**
   - **The sphere law at a shell's real density:** shells weigh 5-6.8 × the water they displace, not 7.9, because of the cavity and the ogive. That gives 6.9-8.1° by 18/√SG, and 7.2-8.4° by Hutchings' cylinder law (W0).
   - **Elongated bodies skip far steeper than spheres:**
     - Rifle and .50 bullets: 15-30° [SRC Baillargeon & Bergeron 2012]. The .50 M2 skips 80% of the time at 11.1° and 36% at 15° [SRC Mirshak & Beech].
     - Shells off a smooth sea: below 25° (von Chrismar via Cranz & Becker) [SRC via Johnson 1998].
     - Canopus's inert 12 in that skipped into Gneisenau's after funnel at 11,000 yd was falling at about 14° (W1) [SRC event; INFERRED angle].
     - The RN's diving shells, tried since 1902: "None of them have been successful at any reasonably low angle of impact on the water" [SRC ADM 186/251].
   - **Proposed** [EST]: a hard cutoff at 9°, or a band, P(skip) = 1/(1 + e^((θ−11°)/1.5°)). The band gives 0.93 at 7°, 0.5 at 11° and 0.07 at 15°. Near the top the speed kept is small (item 3), so those skips are slow and short.

7. **The fuze on a graze.**
   - **Nose fuzes** need a minimum fall. The RN's 1921 minima on water for reliable functioning (No. 44 fuze, HE): just under 2° for the 4 in Mk IV at 2,100 yd, 1°49' for the 4 in Mk V and Mk IX at 2,400 yd, 5°20' for the 4.7 in Mk I at 4,100 yd [SRC ADM 186/251 §59]. That is a normal component of about 16-17 m/s for the 4 in and 45 m/s for the 4.7 in (W5) [INFERRED]. Flatter than that, a nose fuze skips unburst. The code bursts 95% of nose fuzes on any graze.
   - **Base fuzes** split by sensitivity:
     - **Sensitive ones started on the graze.** At the Yellow Sea, a Japanese 12 in AP "fell undershot, ricocheted off the surface of the water, and then the fuse (bottom) went off". Only its head flew on to Tsesarevich's conning tower, wrecking the telegraphs, voice pipes, wheel and compass [SRC Russian analysis].
     - **Robust heavy-AP base fuzes mostly didn't.** Both of Albert W. Grant's ricochet hits (US 6 in AP, Surigao 1944) were unexploded. One "dropped unexploded" inside the hull; the other cut through two 40 mm shields "without exploding", though it set off the ready ammunition [SRC WDR 51].
     - **The 1921 RN:** base fuzes were "liable to go blind through thin structures". Future base fuzes were wanted for shells "required to function on water at low angles of arrival" [SRC ADM 186/251]. The USN's WWII BD Mk 19 "will function on four inch plate and on water" [SRC OP via bulletpicker]. Okun: the Mk 21 "had no 'graze' feature" [SRC].
   - **The physics:** a graze decelerates a shell along its axis about half as hard as a steep entry. That is 130-730 g for a 12 in at 300-700 m/s, 90-500 g for a 15 in, and 400-2,200 g for a 4 in (W5) [INFERRED]. Small shells' base fuzes start on a graze far more readily than big ones'.
   - **Proposed** [EST]: a graze-start chance per fuze. Sensitive base fuzes (the IJN's 1904, anything that starts on 1 in plating) 0.6-0.9. Robust heavy-AP fuzes 0.1-0.3, more for small calibres. A started fuze bursts its delay on: 5-20 m past the graze at 450-600 m/s, in the air over the water. It hits the ship only if it skipped that close.

8. **Hits after a skip: real, but a minority.**
   - **The cases found:** Tsesarevich 1904 (one of 14 heavy hits), Orel 1905 (an 8 in "ricocheting off the water" into the conning-tower slit, which smashed the Barr & Stroud) [SRC Shvede via Russian analysis], Gneisenau 1914 (Canopus's practice shell) and Albert W. Grant 1944 (2 of 13 hits).
   - **What doctrine said:** the USN's 1958 gunnery text lets the "hitting space" include "a distance in front of the target within which impacts are likely to produce underwater or ricochet hits", without a number [SRC NavPers 10798-A].
   - **What the cases give:** where counted, ricochets were roughly 7-15% of the hits in close actions [UNCERTAIN: two ships].
   - **The sim:** `duel`'s super-dreadnought on a destroyer at 4 km gets 21% of its hits after a skip (+14.6 on 55), Mikasa at 3 km 30%. The exit law decides it (W4). Skipping 100 m short of a destroyer at 4 km, the code's mirror-and-3° hits 56-57%; the proposed exit hits 13-15%. At 50 m short: about 80% against 43%.

9. **A skipped shell should strike yawed.** BRL's rounds pointed in all quadrants after the skip [SRC]. Doppler radar found 9 mm bullets destabilised after a water ricochet, with a lower ballistic coefficient [SRC Haag 2017, abstract]. The "curving upward" of 1911 is the lift of a nose-up yawed shell [INFERRED].
   - **What yaw does at a plate:** this repo's armour research already says what the plate does with yaw. Under 10°, add ½ the yaw to the obliquity; more than that is tumbling ([armor/00](../armor/00-overview.md) §5). The USN's 1944 firings of yawed .50 AP raised the limit velocity 0.5-0.7% a degree up to 30°, with a maximum at 40-50° [SRC NPG Report 10-43].
   - **Proposed** [EST]: the yaw at the exit is the pitch that turned the path, |exit − fall| plus |N(0, 3°)|, decaying over about 2,000 calibres. Over 20-25° the shell tumbles. Drag rises as 1 + 10-20·ψ² (ψ in rad, W6).

---

## 1. The limit: when a pointed shell skips

### 1.1 Spheres, cylinders and a shell's real density (ref W0)

- **The sphere law** [SRC]: spheres skip below θc = 18°/√SG [Johnson & Reid 1975]. Birkhoff's 1944 theory gives the same form. The limit rises with speed: Soliman's steel spheres skipped below 3° at 50 ft/s and 7° at 240 ft/s [SRC via Johnson 1998].
- **Cylinders** [SRC Hutchings 1976 eq. 25]: 18.7°/√SG without spin. The length drops out. His spin is a cylinder rolling across its path (Wallis's backspin raises the limit). A shell spins about its line of flight, which the law doesn't cover.
- **The code** takes SG 7.9, solid steel: 6.4°. A shell is hollow and pointed, so its mass over its outside volume is lower. Typical lengths and heads give [INFERRED, the lengths EST]:

  | Shell | SG | 18/√SG | 18.7/√SG |
  |---|---:|---:|---:|
  | 4 in 31 lb HE | 5.6 | 7.6° | 7.9° |
  | 6 in 100 lb CPC | 5.0 | 8.1° | 8.4° |
  | 12 in 850 lb APC 4crh | 5.4 | 7.7° | 8.0° |
  | 13.5 in 1,400 lb APC | 6.3 | 7.2° | 7.5° |
  | 15 in Greenboy | 6.3 | 7.2° | 7.4° |
  | 16 in 2,700 lb Mk 8 | 6.8 | 6.9° | 7.2° |

### 1.2 Elongated bodies skip far steeper [SRC]

- **Pistol bullets:** 9 mm skipped below 6.5° (Uzi, a tank) [NCJRS 134446] and about 8° (FMJ, AAFS 2012). The critical angle is "approximately seven degrees" in a 2018 series.
- **Rifle and .50 bullets** in a pool: critical angles of 15-30° [Baillargeon & Bergeron 2012]. Mirshak & Beech's logistic fit to the .50 M2 data, P = e^(6.96 − θ/1.99)/(1 + e^(…)), gives 0.80 at 11.1° and 0.36 at 15°. Ricochets and dives are mixed between 10° and 16°.
- **L/D 5 bodies at 95-172 m/s** skipped at up to 35°, cone noses most readily [Chen 2020].
- **Shells:** Cranz & Becker, citing von Chrismar, give shells skipping off a smooth sea below 25°. On land they give 10 cm French shells off sand below 10° and 32 cm below 28° [via Johnson 1998].
- **Why a pointed body beats the sphere law:** the sphere law asks whether the water's lift can turn the path before the body is under. A pointed shell is also *turned* by the pressure on its nose's lower face. Johnson calls this broaching: rebound "even though the top surface or indeed whole body" went under. Richardson's trajectory types include "break-surface often followed shortly by re-entry" [SRC Johnson 1998]. Shlien saw spheres "apparently ricochet" at up to 45° after running at depth [SRC Shlien 1994].
- **The navies knew it.** Anti-submarine shells had to be fired at high angle "so that it will dive into the sea instead of ricocheting" [SRC Popular Science 1918]. The RN's diving shells, under trial since 1902, had "none ... successful at any reasonably low angle of impact on the water" [SRC ADM 186/251 §50].

### 1.3 Canopus, 8 December 1914

- Canopus, grounded as guardship at Port Stanley, opened fire over the land at 11,000 yards (Sturdee's despatch) [SRC London Gazette]. Her after turret's guns were loaded with practice rounds, "and one of them ricocheted off the water and struck the rear funnel" of Gneisenau [SRC RMG].
- One popular history instead has splinters from a near miss [SRC history.co.uk]. Most accounts give the ricochet.
- **The angle:** the 12"/35 Mk VIII (850 lb, 2,375 ft/s, 13,900 yd at 13.5°) falls at about 14° at 11,000 yd and 17° at 12,000, striking at 340-360 m/s (W1, a point-mass flight fitted to that maximum range) [INFERRED].
- That puts a skip far above any sphere-law limit for a shell, though within von Chrismar's 25°. An inert practice shell has about the same weight and form. Slow, steep and lucky: the proposed band (R1) gives such a fall a chance of about 0.1.

### 1.4 The sea

- **Waves change the angle the shell actually meets.** Mirshak & Beech model the "wildly unpredictable directions" naval operators report at sea as wave slopes. Waves running along the line of fire lower the ricochet probability and shorten the hazard range beyond 1,000 m; waves across it are "marginal". Steeper waves shrink the hazard zone [SRC].
- **Scale for a shell** [EST]: a shell planes over metres, so the slopes that matter are those of waves longer than about 5 m. Those have an rms of a few degrees in a moderate sea, so near the limit the skip is a matter of chance either way.

---

## 2. What it keeps and where it points

### 2.1 Speed (ref W2)

- **The measured losses** [SRC]:
  - BRL fired cal .50 AP M2 and 20 mm (AP shot M75, shell Mk 1) at depressions of 5° and 10°. At 5° the .50 left the water at 1,809 ft/s on average against 2,515 ft/s had it flown on (0.72). At 10° the one round that skipped kept 329 ft/s [AD0479824, from the search snippet; the full report wasn't reachable].
  - Baillargeon's .50 Ball M2: Weibull, scale 7.37°, shape 2.258 [Mirshak & Beech eq. 3].
  - Steel spheres off water kept 0.81 [Soliman 1976].
  - L/D 5 bodies: 0.802 − 0.0164θ, 0.7-0.8 at 5° falling to 0.18-0.24 at 35° [Chen 2020; low speed, the 20% at 0° is theirs].
  - Popular: up to 35% lost a ricochet [UNCERTAIN, Wikipedia].
- **The code** keeps exp(−2θ·k) with k 1: a body planing through its turn of 2θ at a drag-to-lift ratio of 1. That keeps 0.93 at 2° (the data's 0.95) but 0.84 at 5° (the data's 0.66-0.72) and 0.80 at 6.4° (the .50's 0.49).
- **The shape is wrong near the limit.** Near the limit the body goes deeper, wets more and planes longer, so the loss runs away. The exponential in θ doesn't do that.
- **Scaling to shells** [INFERRED]: if the .50's angle scale goes with its critical angle as √(1/SG), SG 10.5 → 6 moves 7.37° to about 9.5°. The proposal's 8.5° sits between the unscaled and the scaled.

### 2.2 The exit angle up and down

- **The evidence by body:**

  | Body | Entry | Exit | Source |
  |---|---|---|---|
  | Brass spheres, 620 m/s | ≤ 7° | 0.9 × entry | [SRC Ramsauer via Johnson 1998] |
  | Spheres, Richardson | | "somewhat less" than entry | [SRC via Johnson 1998] |
  | Bullets, yielding surfaces | | greater than entry | [SRC AAFS 2012] |
  | .50 AP, 20 mm (BRL) | 5-10° | ratio to entry falls fast as entry rises, and with sectional density | [SRC AD0479824 snippet] |
  | L/D 5, 95-172 m/s | 5-35° | 15.4 − 0.16θ (about 15°) | [SRC Chen 2020] |
  | 9 mm / .223, a lake | 5°; 8.5°; 12.5° | 12° and 26°; 9.5° and 14°; 9.5° or none | [UNCERTAIN American Handgunner] |
  | .50 M2, pool (fitted) | ≤ 10° | the sigmoid ψ0 8.16 + 43.31/(1 + e^((θ−11.83)/0.61)) | [SRC Mirshak & Beech eq. 6] |
  | USN 5"/50, 1911 | about 1° (W1) | "a great height", "curving upward" | [SRC SA 1911] |
  | US 6 in AP, Albert W. Grant 1944 | about 2.4° (W1 at 4 km; the range EST) | "path slightly upward" | [SRC WDR 51] |

- **The .50 sigmoid** gives about 50° at small entries. I read it as the angle to a near witness panel of rounds already tumbling, not a flight path, and don't use it [UNCERTAIN].
- **What the evidence says together:** the exit is steeper than the entry at small entries (by 2-5× at 1-5°). It levels toward the entry, or below it, near the limit. The spread is wide, and the lighter the body for its size, the steeper.
- **Shells are lighter than bullets for their size** (SG 5-7 against 9-11), which by the scaling in Summary 2 would make them pitch *more* [INFERRED]. That is why the proposal's 5° base is a middle value, not a floor.
- **Proposed** [EST]: exit median 5° + ½ × fall, lognormal σ 0.6, capped at 35°. At a 2° fall: median 6°, 5-95% 2.2-16°. At a 5° fall: median 7.5°, 2.8-20°. A bullet-like 10° + ½ × fall is the steep end of the plausible; the code's mirror ± 3° is the flat end.

### 2.3 Across

- **The evidence** [SRC unless tagged]:
  - Mirshak & Beech: most .50 ricochets go about 10° right, and waves across the line hardly change that.
  - BRL: most rounds after the skip pointed upward and to the right.
  - Hawkesworth: a right-hand-rifled 6 in would "corkscrew" into the right-hand wall.
  - A 9 mm across a lake: about 10° right per skip [UNCERTAIN].
  - The scatter across grows as the speed kept falls: "The azimuth ranges show the highest variability at low postricochet velocity" [Baillargeon & Bergeron].
- **Why it should hold for shells** [INFERRED]: the right is the twist's side. A naval gun's twist of about 1 turn in 30 calibres is the .50's (1 in 15 in, 30 calibres), so the wetted surface slides at the same 0.1 of the forward speed.
- **Proposed** [EST]: across = 5° right + N(0, 4°), widened by 1/kept below 0.6 kept.

### 2.4 How far it flies (ref W3)

A 12 in Mk X shell meeting the sea 4 km out at 2.16° and 674 m/s, keeping 0.94:

| Exit | 100 m on | 300 m on | 1,000 m on | Next graze |
|---:|---:|---:|---:|---|
| 1° | 1.6 m | 4.1 m | 4.7 m | 1.4 km at 1.1°, 585 m/s |
| 2° | 3.4 m | 9.4 m | 22 m | 2.6 km (2.2 with yaw drag ×3) |
| 3° | 5.1 m | 15 m | 40 m | 3.7 km (2.9) |
| 5° | 8.6 m | 25 m | 75 m | 5.6 km (4.0) |
| 8° | 14 m | 41 m | 128 m | 8.0 km (5.3) |
| 12° | 21 m | 63 m | 199 m | 10.5 km (6.7) |

- **Yaw drag** changes the far end, not the first few hundred metres.
- **A second splash 300-800 m on** needs an exit under 1° and a large loss, which the data don't support. The SA 1911 "miles beyond the target" is what the physics gives.
- **The code** flies the skip by the ballistics from the sea, which is right. Only the exit and the speed it hands that flight change.

### 2.5 Yaw and tumbling after the skip

- **The evidence** [SRC]:
  - BRL: orientation after the skip "occurs in all quadrants", most pointing up and right.
  - Haag's Doppler radar measured 9 mm bullets after a water ricochet and their "post-impact bullet stability", with a measured effective ballistic coefficient of the destabilised bullets [AFTE 2017 abstract].
  - Bullets past the ricochet were "in destabilized flight, resulting in somewhat unpredictable trajectories" [AAFS 2010].
  - Mirshak & Beech: rounds that lose much speed "become unstable and wobble".
- **For a shell** [INFERRED]: the pitch that turned its path is still in its axis when it leaves. A spin-stabilised shell with a 5-15° yaw precesses and damps over 1,000-3,000 calibres (0.3-1 km for a 12 in) [EST]. Over about 20-25° the overturning moment wins and it tumbles [EST].
- **Lift and drag** [INFERRED]: lift from a nose-up yaw curves the path up (SA 1911's "curving upward"), then to the side as the yaw precesses. Drag rises as 1 + k·ψ², k = C_Dδ²/C_D0 ≈ 10-20 a radian² for supersonic shells [EST, McCoy-type values]. That is ×1.3-1.6 at 10° and ×2.2-3.4 at 20° (W6).

---

## 3. The fuze on a graze

### 3.1 Nose fuzes (ref W5)

- **The RN's 1921 minima for reliable functioning on water**, No. 44 fuze, HE [SRC ADM 186/251 §59]:

  | Gun, shell | Range | Minimum fall | v_s (W1) | Normal component |
  |---|---:|---:|---:|---:|
  | 4 in QF Mk IV, 31 lb | 2,100 yd | just under 2° | 473 m/s | 16 m/s |
  | 4 in QF Mk V and BL Mk IX, 31 lb | 2,400 yd | 1°49' | 546 m/s | 17 m/s |
  | 4.7 in BL Mk I, 50 lb | 4,100 yd | 5°20' | 485 m/s | 45 m/s |

  W1's falls at those ranges, 1.8°, 1.6° and 3.2°, match the 4 in figures. The 4.7 in's stated 5°20' is steeper than its flight gives, so that trial may have used a reduced charge or another shell. I use the source's angles [INFERRED].
- **In battle:** at Jutland the 1st Battle Squadron saw 4 in or 6 in shells "burst on the water just short of 'Marlborough'" [SRC].
- **Reading** [INFERRED]: a nose fuze fires on a graze once the normal component passes about 15-45 m/s. Flatter, it skips unburst and may fire on the next graze or on the ship.

### 3.2 Base fuzes

- **The cases** [SRC]:
  - **Tsesarevich, 10 August 1904:** a 12 in "fell undershot, ricocheted off the surface of the water, and then the fuse (bottom) went off". Only the head reached the conning tower and wrecked the steering and telegraphs. The ship "temporarily lost controllability".
  - **Orel, 27 May 1905:** an 8 in "ricocheting off the water" burst in the conning-tower slit. Its fragments smashed the Barr & Stroud rangefinder, the combat indicators, voice pipes, compass and wheel (Shvede's report). The Japanese HE then had sensitive fuzes; the repo gives the IJN's Ijuin fuze 0.1 ms on water.
  - **Albert W. Grant, Surigao 1944:** both ricochets of the 13 hits (US 6 in AP) were unexploded. So were four of the other 6 in AP hits, and a fifth broke up low order [SRC WDR 51].
- **The fuzes' own record** [SRC]:
  - RN 1921: base fuzes "liable to go blind through thin structures". The need for base fuzes "required to function on water at low angles of arrival" was still open.
  - USN BD Mk 19: "will function on four inch plate and on water".
  - Okun: the US Mk 21 "had no 'graze' feature", needing a single plate of about 0.07 calibre at right angles.
  - Normal AP fuzes started on a steep water entry and burst "very shortly after" [Okun, tech-041].
- **The physics** (W5) [INFERRED]: with half the section wetted at Cd 0.3, the graze's axial deceleration is about half a full entry's:

  | Shell | 300 m/s | 500 m/s | 700 m/s |
  |---|---:|---:|---:|
  | 4 in 31 lb | 410 g | 1,130 g | 2,210 g |
  | 6 in 100 lb | 280 g | 790 g | 1,540 g |
  | 12 in 850 lb | 130 g | 370 g | 730 g |
  | 15 in 1,920 lb | 90 g | 260 g | 500 g |

  It scales as v²/(SG·D), so a 4 in feels three times a 12 in's graze. Fuzes made not to start on thin plating (the WWII heavy-AP base fuzes) mostly won't on a big shell's graze. The sensitive ones of 1904 did.
- **Proposed:** see Recommendations R4.

---

## 4. Hits after a skip

### 4.1 In the record

| Case | Hits | Ricochets | What they did | Source |
|---|---|---|---|---|
| Yellow Sea 1904, Tsesarevich | 14 heavy | 1 (12 in) | burst after the graze; the head wrecked the conning tower's steering; the ship lost control | [SRC Russian analysis] |
| Tsushima 1905, Orel | several dozen | 1 noted (8 in) | burst in the conning-tower slit; rangefinder and steering gear wrecked | [SRC Shvede, 1906] |
| Falklands 1914, Gneisenau | 1-2 | 1 (12 in practice) | the after funnel; von Spee turned away | [SRC RMG; Gazette] |
| Surigao 1944, Albert W. Grant | 13 | 2 (6 in AP) | both unexploded; one through two 40 mm shields setting off ready ammunition | [SRC WDR 51] |
| Santiago 1898, Jutland 1916 | | not reached | Campbell's notes per hit not available online | |

- **Rate** [UNCERTAIN, two counted ships]: 7-15% of the hits in close actions were ricochets. Where they struck, it was low and rising: conning towers, gun shields, the hull near the waterline.
- **Doctrine** knew of ricochet hits but gave no number [SRC NavPers 10798-A 1958].
- **Practice:** shorts in practice flew "miles beyond the target" [SRC SA 1911].

### 4.2 What the exit law does to them (ref W4)

The share of shells skipping s metres short of a beam-on target that strike it on the way past:

| Shooter, target | s short | code mirror ± 3° | proposed 5° + ½fall | bullet-like 10° + ½fall |
|---|---:|---:|---:|---:|
| 12 in at 4 km, destroyer | 50 m | 0.83 | 0.43 | 0.14 |
| | 100 m | 0.57 | 0.13 | 0.03 |
| | 200 m | 0.42 | 0.02 | 0 |
| | 300 m | 0.10 | 0.01 | 0 |
| 13.5 in at 4 km, battleship | 100 m | 0.86 | 0.50 | 0.27 |
| | 300 m | 0.29 | 0.13 | 0.03 |
| 13.5 in at 8 km, battleship | 100 m | 0.61 | 0.43 | 0.21 |
| | 300 m | 0.17 | 0.10 | 0.02 |

- **The targets** [EST]: destroyer freeboard 3.5 m with upper works to 8 m over 35% of her length; battleship 7.5 m, works to 20 m over 45%. The speed kept is the .50 fit's.
- **The sim's figure:** "a shell aimed 300 m short of a destroyer at 4 km hits her about half the time after skipping" is the code's flat exit; aimed 300 m short, its pattern puts many skips much closer. With a pitched-up exit, hits come mostly from skips within 50-100 m of a destroyer and 100-300 m of a battleship. That fits the record's cases: low hits, conning towers, shields.
- **The proposal** cuts the destroyer's hits after a skip by roughly 2-4× over the first 300 m and a battleship's by about 1.5-2×. From `duel`'s +14.6% of shells fired, that is perhaps +4-7%, or 7-11% of the hits [INFERRED, to check with `duel`].

---

## 5. A yawed shell at a plate

- **The plate:**
  - **Small yaw** [SRC armor/05 §4c, Okun]: add half the yaw to the obliquity under 10°. Larger yaw "is treated as damage".
  - **Large yaw:** the USN Naval Proving Ground's 1944 firings of fully yawed .50 AP M2 at special treatment steel found the limit velocity "approximately linear up to 30° yaw", rising 0.5-0.7% per degree. It reached a maximum at 40-50° and then held or fell off toward 90° [SRC NPG Report 10-43, ADA955279].
  - Watertown 1950: yaw onto a *lower* effective obliquity lowered protection by about 200 ft/s [SRC armor/05].
- **The shell** [INFERRED from armour research, no water case]: a yawed capped shell strikes off its cap's axis, so the cap does less and break-up is likelier at a given plate. A base fuze's striker meets a side blow it isn't built for: the Grant's two ricochets and four other 6 in AP went through without bursting.
- **So** [EST]: under 10° of yaw at the plate, Ob + ½ψ, as the terminal could take now. At 10-25°, the limit velocity × (1 + 0.006ψ), the cap ignored and the base fuze's start halved. Over 25° the shell is tumbling: no armour penetration past about 0.3 calibre, burst by chance.

---

## Recommendations for this codebase

Advisory; Claude Code decides. The user's frame (2026-11-13) stands: the skip may stay a hard cutoff so long as the skipped shell flies on and can hit what's beyond.

**R1. The limit (`WaterRicochetDeg` 6.4).** It is the steel sphere's.
- A shell's bulk SG gives 6.9-8.4° (W0). Elongated bodies and the period's own evidence go well beyond (§1.2-1.3).
- Hard cutoff: 9° [EST]. Or a band, P(skip) = 1/(1 + e^((θ − 11°)/1.5°)), drawn from the "skips" stream.
- Either way the near-limit skips keep little speed (R2), so the change matters most for falls of 6-9° (heavy guns at about 7-11 km). Canopus's 14° would skip about one time in ten.
- `duel`'s 12 km row (no skips) would start skipping a few.

**R2. Speed kept (`WaterRicochetLossK` 1).**
- Replace exp(−2θk) by exp(−(θ/θv)^2.3) with θv 8.5° [EST, 7.4-9.5°] (§2.1).
- If the exponential stays, k 1.5 is the least-bad single value: 0.77 at 5°, too lossy only under 2°.

**R3. The exit and its spread (`WaterRicochetScatterDeg` 3).**
- **Up:** median 5° + ½·fall, lognormal σ 0.6, capped at 35° [EST] (§2.2).
- **Across:** 5° right + N(0, 4°), the sign from the twist [EST] (§2.3).
- Both draw from the "skips" stream as now. A seaway can widen both by its slope (§1.4).
- With R1-R3, the yawed exits' extra drag can be applied for the first kilometre or so: × (1 + 2(exit/20°)²), the ref's stand-in. This changes where the second graze lands, not hits close by.

**R4. The fuze on the graze.**
- **Nose fuzes** (`NearMiss.Burst`): fire on a graze only if v·sin(fall) ≥ `water_vn` [EST: 15 m/s sensitive, 45 robust]. Otherwise the shell skips unburst and its nose fuze may fire at the next graze or on the ship.
- **Base fuzes:** a `graze_starts` chance on the fuze block [EST]: 0.6-0.9 for sensitive fuzes (JP 1904's Ijuin, anything that starts on 1 in plating), 0.1-0.3 for robust heavy-AP fuzes. Scale it by the W5 deceleration against the fuze's (e.g. ×(a_graze/400 g), capped at 1).
- A started base fuze bursts after its delay, in the air beyond the graze (a near miss's splinters, 13's field). The rest flies on with its fuze unstarted.
- Today's v0 rule (base fuze never started) is the robust end. It is right for the WWII heavy AP and wrong for 1904.

**R5. Yaw at the strike** (§5; the terminal has none today).
- Carry ψ on a skipped shell: ψ0 = |exit − fall| + |N(0, 3°)| [EST], decaying as e^(−x/(2,000·D)).
- At the plate:
  - ψ ≤ 10°: Ob + ½ψ.
  - 10-25°: limit velocity × (1 + 0.006ψ), cap ignored, base fuze's start ×0.5.
  - over 25°: tumbling (penetrates ≤ 0.3 calibre, bursts by chance).
- A cheaper v1 is a flag "skipped" that does the 10-25° row for every hit after a skip.

**R6. Checks** (wide bands, [EST] from §4.1):
- **Ricochet hits as a share of all hits:** 5-15% in close actions (≤ 5 km) on destroyers and battleships. The super-dreadnought-on-destroyer duel at 4 km now gives 21%.
- **Speed and grazes:** a skipped 12 in at 4 km keeps 0.9-0.97 and grazes again 1.4-10 km on (W3).
- **The 1904 case:** a nose fuze on a 1-2° graze at 600 m/s doesn't fire; a 1904 IJN base fuze mostly does.

**R7. Elsewhere.** vfx/shell-splashes.md's ricochet line wants correcting:
- The 1911 source is the 5"/50 battery, not 12 in.
- The second splash lands kilometres on, not 300-800 m. The game spawns it where the skip's flight lands.

---

## Open, not found

- Any navy's trial of big shells skipping off the sea: exit angles, speed, yaw. The RN's Shoeburyness and Excellent reports and the USN's Dahlgren "ricochet" series weren't reached. The 1921 ADM 186/251 has diving-shell and fuze notes only.
- BRL AD0479824 in full: DTIC refused the fetch, so only the search snippet's figures are used.
- Campbell's Jutland per-hit notes (which hits were ricochets), the Santiago board's hit survey, Semenov on the Japanese fuzes on water.
- Fuze graze thresholds in g for the period's base fuzes (RN No. 16, German, Ijuin).
- The 1958 USN text's number, if any, for the "distance in front of the target".

---

## Sources

- Hawkesworth, "Under-Water Trajectories", USNI Proceedings, Feb 1921: https://www.usni.org/magazines/proceedings/1921/february/under-water-trajectories (and Arnaud's discussion, Sep 1921: https://www.usni.org/magazines/proceedings/1921/september/discussion-under-water-trajectories)
- Johnson, "Ricochet of non-spinning projectiles, mainly from water, Part I", IJIE 21 (1998): https://blog.espci.fr/pse1/files/2018/03/ricochet_johnson1.pdf
- Johnson & Reid, "Ricochet of spheres off water", JMES 17 (1975): https://doi.org/10.1243/JMES_JOUR_1975_017_013_02
- Hutchings, "The ricochet of spheres and cylinders from the surface of water", IJMS 18 (1976): https://www3.eng.cam.ac.uk/~hemh1/dambusters/ricochet_hutchings1.pdf
- Shlien, "Unexpected ricochet of spheres off water", Exp. Fluids 17 (1994): https://blog.espci.fr/pse1/files/2018/03/shlien1994.pdf
- Truscott, Belden & Hurd, "Water-skipping stones and spheres", Physics Today (2014): https://physicstoday.aip.org/quick-study/water-skipping-stones-and-spheres
- Baillargeon & Bergeron, "Prediction of Projectile Ricochet Behavior After Water Impact", J. Forensic Sci. 57 (2012): https://onlinelibrary.wiley.com/doi/abs/10.1111/j.1556-4029.2012.02170.x; abstract https://ojp.gov/ncjrs/virtual-library/abstracts/prediction-projectile-ricochet-behavior-after-water-impact
- Mirshak & Beech, "Estimating Ricochet Hazard Zones at Sea", JDMS 15 (2018): https://journals.sagepub.com/doi/pdf/10.1177/1548512918771769
- BRL, "Effects of ricochet on the motion of projectiles", AD0479824: https://apps.dtic.mil/sti/tr/pdf/AD0479824.pdf (search snippet only)
- Chen et al., "Motion Characteristics After Ricochet", Polish Maritime Research 27 (2020): https://doi.org/10.2478/pomr-2020-0021
- AAFS abstracts: 2010 A199 https://www.aafs.org/sites/default/files/media/documents/AAFS-2010-A199.pdf; 2012 A102 https://www.aafs.org/sites/default/files/media/documents/AAFS-2012-A102.pdf; 2018 B99 https://www.aafs.org/sites/default/files/media/documents/AAFS-2018-B99.pdf
- 9 mm Uzi into water, 6.5°: https://www.ncjrs.gov/App/Publications/abstract.aspx?ID=134446
- Haag, "The Application of Doppler Radar to Bullet Ricochets from Water", AFTE J. 49 (2017), abstract: https://afte.org/?p=15939
- American Handgunner, "The Art of the Ricochet" (anecdotal): https://americanhandgunner.com/handguns/the-art-of-the-ricochet/
- Scientific American, "A Landsman's Log Aboard the Battleship North Dakota III", 11 Nov 1911: https://www.scientificamerican.com/article/a-landsmans-log-aboard-the-battlesh-1911-11-11/
- Admiralty, Progress in Gunnery Material 1921 (ADM 186/251): https://vif2ne.org/nvk/forum/files/Realswat/(160312234717)_adm186-251.pdf
- Popular Science Monthly 92 (1918), the Isham shell: https://kwx.liehendr.com/content/wikisource_en_all_maxi_2022-09/A/Page%3APopular_Science_Monthly_Volume_92.djvu/870
- Russian analysis of Japanese gunnery (Yellow Sea, Tsushima; Shvede's report): https://military-review.com/12511249-reflections-on-the-effectiveness-of-japanese-medium-caliber-artillery-in-tsushima-part-2
- Canopus: RMG https://www.rmg.co.uk/collections/objects/rmgc-object-115189; Sturdee's despatch, London Gazette 29087: https://www.thegazette.co.uk/London/issue/29087/supplement/2208/data.pdf; history.co.uk: https://www.history.co.uk/article/the-empire-strikes-back-the-first-battle-of-the-falkland-islands-1914
- BuShips War Damage Report 51, Destroyer Report, Gunfire, Bomb and Kamikaze Damage: https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-damage-reports/destroyer-report-gunfire-bomb-kamikaze-damage.html
- Jutland, reports from the First Battle Squadron: https://battleofjutlandcrewlists.miraheze.org/wiki/Reports_from_First_Battle_Squadron
- Naval Ordnance and Gunnery vol. 2, NavPers 10798-A (1958), ch. 18: https://www.eugeneleeslover.com/USNAVY/CHAPTER-18.php
- Okun, underwater travel of projectiles: https://navweaps.com/index_tech/tech-041.php; base fuze minimums: https://navweaps.com/index_nathan/baseFuzePlateThicknessMinimums.php
- BD Mk 19: https://bulletpicker.com/bd_-mk-19-mod-0-3.html
- Naval Proving Ground Report 10-43 (1944), yawed .50 AP: https://apps.dtic.mil/sti/tr/pdf/ADA955279.pdf
- Guns (W1): navweaps 12"/35 Mk VIII https://www.navweaps.com/Weapons/WNBR_12-35_mk8.php, 4"/40 QF Mk IV https://www.navweaps.com/Weapons/WNBR_4-40_mk4.php, 4"/45 BL Mk IX https://www.navweaps.com/Weapons/WNBR_4-45_mk9.php, 4.7"/45 BL Mk I https://www.navweaps.com/Weapons/WNBR_47-45_mk1.php; the rest as fire_control_ref.py
