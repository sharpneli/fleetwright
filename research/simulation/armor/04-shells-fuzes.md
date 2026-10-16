# 04 — Projectiles and Fuzes 1860–1945: penetrating armour and bursting behind it
Status: draft    Updated: 2026-10-16    Request: —

Foundational research for the armour-penetration model. This is **not** a model. It gathers inputs: what the projectiles were, what they were filled with, how the fuzes armed, started and delayed, where shells burst, and how often they failed.

**Conventions**
- Every figure carries an inline source link. Short labels such as `[NW-15in]` resolve in the **Sources** list at the end.
- **[INFERRED]** marks my arithmetic or reasoning from cited figures.
- **[UNCERTAIN]** marks a claim that is conflicting, from one secondary source only, or not verified this pass.
- **[NOT VERIFIED]** marks a claim the project already holds that I could not re-check. One reason: Okun's `baseFuzePlateThicknessMinimums.php` page could not be fetched with the tools available (blocked at fetch time). I did not try to work around that.
- W/D³ is shell weight in lb divided by calibre in inches, cubed (lb/in³). I computed every W/D³ and every filler or cap percentage from the cited weights **[INFERRED arithmetic]**.

---

## 0. Corrections and refinements to what the project already holds

| Project belief | Finding this pass | Status |
|---|---|---|
| AP filler 1.5–3.5% | Okun's classes: AP ≈1.4–3.5%, Common/SAP ≈3.51–5.99%, HE/HC ≥6.0% ([NW-Misc]). Real outliers fall outside this. The Italian 381 mm APC is **1.15%** ([NW-IT381]). Japanese 1904–05 "AP" (Furoshiki) is **5.0%** Shimose ([NW-JP12]). | Refine: allow 1.1–3.5% for APC. |
| SAP 4–8% | The German 38 cm "HE base-fuzed" round was capped (a de facto SAPC) at **4.4%** ([NW-GE38]). The Italian 381 SAPBC is **3.6%** ([NW-IT381]). The navalgazing generic figure is SAP 7.5–9% ([NG-S2]). | Range is really 3.5–9%, and it is nation-dependent. |
| HE 6–13% | RN 12" HE with lyddite is **12.5%** ([NW-12in]). The Russian 12" HE mod 1911 is **13.1%** ([NW-RU12]). The USN 16" HC Mk 13 is **8.1%** ([NW-US16-50]). Navalgazing's generic figure is 12–15% ([NG-S2]). | OK. Extend to 15%. |
| RN delay 0.025 s | Confirmed: No. 16D APC base fuze nominal 0.025 s, one source 0.024 s. With the bypass plate fitted, inertial ~0.003 s ([NW-BrFz], [NG-F2]). | Confirmed. |
| USN Mk 21 0.033–0.035 s | Mk 21 is fixed at 0.033 s total (black powder plus inertial) ([NW-Misc], [NW-US16-50]). The general USN base fuze is "about 0.035 s for battleship shells, 0.01 s for 6-inch" ([NG-F3]). The 1957 textbook gives 0.02–0.033 s for delay BDFs ([NOG-3]). | Confirmed. |
| KM ~0.035 s | Bd.Z. 38 is selectable at 0, 0.015 or 0.035 s. The 38 cm and 30.5 cm versions are fixed at 0.035 s; the KV variants at 0.015 s ([NW-GerFz]). | Confirmed, with a selectable range. |
| IJN Type 91 delay 0.4 s | One Okun page says 0.4 s ([NW-T92]). A second Okun page gives "super-long delays (0.08–0.4 second)" for Type 88/91 diving projectiles, but says Japan actually used "0.025–0.035 second" black-powder delay elements ([NW-Misc]). | **[UNCERTAIN]**. Treat 0.4 s as the upper bound. The fuze designation was not found. |
| Okun Tfzmin 0.07 / 0.05 | Confirmed: Tfzmin = 0.07 for fuzes needing a single thick plate, 0.05 for two-plate fuzes. Ob is frozen at 61° above that angle ([NW-Misc]). | Confirmed. See §5 for the 61° cap and a cross-check. |
| Jutland 1 of 17 >9" | Confirmed: 17 hits on >9" plate. Of these, 1 was nose-fuzed HE and 3 ricocheted. Of the remaining 13, **one** penetrated and burst inside (15" on *Derfflinger*'s barbette), 5 holed the plate with little effect, and 7 did not hole it ([NG-Jut]). | Confirmed. More detail in §6. |
| Greenboy from 1918 | Confirmed: mass production began Dec 1917 and the shells reached the fleet in April 1918 ([NG-Jut]). | Confirmed. |
| German C/11 fuze unreliability | Confirmed in its mechanism: the detonator was over-powerful and destroyed the black-powder wafer. The "tortuous tunnel" fix often attenuated the flash too much, giving duds ([NW-BrFz], [NG-F2]). | Confirmed. |
| Hood 1919 burst 34–40 ft behind plate | Not found this pass. The nearest verified data are Baden 1921: burst 11.6 m (38 ft) behind a 25 cm belt ([NW-15in]). The RN interwar fuze-development record gives mean delays of 6.4–15.8 ft ([DTIC-RD]). | **[NOT VERIFIED]**. The Baden figure is consistent with it. |

---

## 1. Projectile types by era

### 1.1 Smoothbore to early rifled (1860–1867)

- **Round shot and spherical shell.** Paixhans-type shells (1820s) used a forward-pointing time fuze lit by the propellant gases. The fuze was cut longer than the time of flight so that the shell lodged in the target before bursting ([NG-S1]). Shell-gun effect at Sinop, 1853: Ottoman frigates destroyed in about 30 minutes, almost 3,000 Turkish dead against 37 Russian ([NG-S1]).
- **Time and concussion fuzes** were the norm. The Bormann time fuze of the 1860s had a ¾-circle pressed-powder train marked in ¼-second increments ([NG-F1]). Concussion fuzes, such as plaster or zinc cones, broke on impact to pass the flash. Round shot could not be relied on to land in a set orientation, so impact fuzing did not work for it ([NG-F1]).
- **Model note:** pre-rifled shells have **no penetrating fuze function** against armour. Treat them as surface-burst or fire-start only.

### 1.2 Rifled cast iron: Palliser and common shell (1867–c.1890)

- **Palliser chilled cast-iron shot and shell** were approved on 21 Oct 1867. The nose was chilled in a water-cooled iron mould section while the body cooled slowly, giving a hard head and a tougher body ([WP-Pall], [WP-AP]). The shell needed no fuze: impact friction or heat ignited the powder ([WP-Pall]).
- **Palliser filler:** about 1–1.5% powder, against about 6% in contemporary common shell ([NG-S1]). Wikipedia gives about 1.5% for the 1877 Palliser shell ([WP-AP]).
- **Performance:** effective against wrought iron, defeated by compound and steel armour from the 1880s ([WP-AP]). In the 1880s the RN concluded that only *shot* could penetrate the new hardened armour, so the shell's powder filling was dropped ([WP-Pall]). Palliser stores were withdrawn from naval and fortress use in 1909 ([WP-Pall]).
- **Combat datum:** at Angamos (8 Oct 1879), Chilean ironclads fired 250 lb Palliser shot at *Huáscar* with "devastating results" ([WP-Pall]).
- **Common shell:** cast iron with black powder, fitted with an impact nose fuze. This became practical once rifling made shells land nose-first ([NG-S1]). The filling was often sensitive enough to go off before penetrating, so common shell was effective only against the outside of armoured targets ([NG-S1]).
- **Early percussion nose fuze:** a plunger with a percussion cap, held by a pin that sheared on firing. The USN added a reversible safety cap that was turned to arm before loading ([NG-F1]). It was instantaneous, so useless for penetration ([NG-F1]).

### 1.3 Forged steel AP (c.1880–1905): Holtzer, Firth, Krupp, Carpenter, Midvale, Sterling, Johnson

- Water-hardened forged steel replaced Palliser. Plain carbon steel came first, then nickel-chromium steel in the 1890s to match cemented armour ([WP-AP]).
- **Manufacturing:** forged, not cast; cast steel was found unsuitable ([NG-S2]). The process was annealing "for several days", machining, then differential hardening, from molten-lead baths through to cold-water quenches ([NG-S2]). Lots were proof-fired by firing "a few randomly-selected shells from each batch" at a plate ([NG-S2]).
- **US practice, from the Johnson trials (USNI 1894):**
  - Forged Carpenter AP was the benchmark. It was expected to penetrate non-Harveyized nickel steel of **1.25 calibres** at 1,500 yd with full charge ([USNI-J]).
  - Johnson cast steel shot was competitive when oil-tempered. Untempered 12" shot "mushroomed" ([USNI-J]).
  - On 23 Mar 1894, 10" shot against 12" Harvey nickel steel at 1,600 ft/s:
    - Uncapped shot B-5 broke up after about 4.5 in.
    - The same shot **with a soft steel cap** (B-3) reached to within 3 in of the back, and the plate split into 8 pieces ([USNI-J]).
  - A Johnson *cavity shell* (P-12/2) penetrated only about 8 in of a 17" plate and was "smashed". A solid shot at the same 1,400 ft/s reached 13.5 in ([USNI-J]). **[INFERRED]** Shells with a burster cavity lose a large fraction of penetration in this era.
- **Holtzer (French), Firth and Hadfield (British), Midvale, Sterling (US):** named in the brief, but no specific data found this pass **[UNCERTAIN]**. Hadfield cap weights appear in RN data (§1.5).
- **Russian practice:** shell makers chose their own shapes until about 1900, which made fire control nearly impossible ([NG-S2]).

### 1.4 Caps: Makarov to hard caps

- **Early history:**
  - An 1878 trial showed a wrought-iron layer helped steel shot penetrate. A soft wrought-iron cap was proposed but ignored "until the Russians tried it in 1894" ([NG-S2]).
  - Makarov soft caps cushioned impact, protected the point and helped at obliquity ([WP-AP]).
  - Early caps gave about **15%** better penetration over uncapped shells, but only at obliquities below about 15° ([NG-S2]).
- **Soft vs hard caps:**
  - Most navies used soft caps (wrought iron, later soft steel) through WWI.
  - Exceptions were Krupp's 1911 Psgr. m.K. L/3,4 and the 1918 Greenboy, both with harder caps ([NW-Misc]).
  - Krupp attached caps with a high-temperature solder, which apparently made them harder to knock off at obliquity ([NG-S2]).
  - USN WWII practice was a cap of the same steel as the body, hardened by "a single immersion in molten lead", soldered and peened on. Its jobs were to raise the biting angle, spread shock over the nose and pre-stress the plate ([NOG-4B]).
- **Decapping:**
  - Any face-hardened plate, or homogeneous plate above about 400 BHN, always removes the cap ([NW-Misc]).
  - Homogeneous plate of about **0.08 calibre** gives a 50/50 chance of decapping; the stated threshold is 0.0805 D ([NW-Misc]).
  - Any solid impact removes the windscreen ([NW-Misc]).
  - Iowa's 60 lb STS outer hull (1.5") near the belt was expected to decap shells and raise belt resistance by "30% or more" ([NG-A4]).
  - Littorio's 70 mm outer plate, 250 mm ahead of the 280 mm belt, had a cellulite-filled gap intended to decap AP ([WP-Litt]).
- **Japanese break-away cap heads:** Type 88 and Type 91 "diving" AP had caps about 0.69 calibre at the base. These broke off above 45° obliquity and stayed on at 45° or less ([NW-Misc]).

**Cap mass as % of shell weight [INFERRED]:**

| Shell | Cap | % of shell | Source |
|---|---|---|---|
| RN 12" APC Mk VI 2crh (pre-war) | 73.0 lb / 850 lb | 8.6% | [NW-12in] |
| RN 12" APC Mk VIa 4crh | 34.75 lb / 859.4 lb | 4.0% | [NW-12in] |
| RN 12" APC Mk VIIa "Greenboy" | 99 lb / 854 lb | **11.6%** | [NW-12in] |
| French 380 mm OPfK Mle 1936 | 84 kg / 884 kg | 9.5% | [NW-FR380] |

Hadfield caps are listed; other makers varied by up to ±9 lb ([NW-12in]). Model cap classes:
- none
- soft, about 4–9%
- hard and heavy, about 9–12% (Greenboy and later)

### 1.5 RN shell family 1900–1918 (APC, CP, CPC, HE)

12"/45 Mk X outfit ([NW-12in]):
- **Peacetime outfit per gun:** 24 APC, 40 CP, 16 HE, all 2crh. APC and CP were black-powder filled; HE was lyddite.
- **APC Mk VI:** 850 lb, 26.3 lb lyddite (originally black powder). That is **3.1%** [INFERRED].
- **CPC Mk VIIa:** 80 lb black powder, **9.4%**.
- **HE Mk IIa:** 106.5 lb lyddite, **12.5%**.
- **Pre-war DNO memo (1910):** 12" APC striking at over 20° was unlikely to penetrate 4" KC, and likely to break up at 30° against 6" ([NW-12in]).
- **Nose shape:** 4crh was tested from 1908 (by swapping caps) and issued from 1909. *Lord Nelson*s kept 2crh ([NW-12in]).
- **CPC capability:** about ½ to ⅓ of calibre in armour at normal ([NG-Jut]). The USN common shell was likewise designed for "1/3 to 1/2 caliber" plate, and AP for "about one-caliber" ([NOG-4B]).
- **Ballistic caps:** the RN "crh" figures are more properly 3.05/4crh and 5/10crh, i.e. ballistic cap on top of the body ogive ([NW-15in]).

### 1.6 Post-Jutland and interwar: Greenboy to WWII APC

- **Greenboy changes:** cap made heavier and harder, body strengthened, filler changed to Shellite. The new proof standards were ([NG-Jut]):
  - 15" shell to defeat a 10" plate at 20°.
  - 12" shell to defeat the 6" plate that had broken a Jutland 15" at 20°.
- **Interwar improvement:** WWII-era AP could penetrate about **40% more** than an immediate post-war shell of the same calibre ([NG-S3]) **[UNCERTAIN, as a general figure]**.
- **USN "super-heavy" line:**
  - 16" Mk 3 (Colorado class): 2,100–2,110 lb ([NW-US16-45]).
  - 16" Mk 5: 2,240 lb, "hard-capped, sheath-hardened", filler cut to 1.5% ([NW-US16-45-5]).
  - 16" Mk 8: 2,700 lb, 9crh ogive (144 in radius) ([NW-US16-50]).
- **USN AP construction:**
  - Nose decrementally hardened by dipping in molten lead and water-cooling, "repeated twice".
  - Body tough and ductile to survive transverse loads at obliquity.
  - Steel like Class A armour but with higher carbon ([NOG-4B]).
  - The windscreen (false ogive) reconciles ballistic shape with a blunt, heavy penetrating nose ([NOG-4B]).
- **Nose mass:** the solid nose piece is about **33%** of AP body weight and about **25%** for Common/SAP ([NW-Misc]).
- **German WWII alloy:** Germany replaced Ni-Cr-Mo steels with a Si-Mn-Cr alloy that was more brittle on sloped armour ([WP-AP]) **[UNCERTAIN whether this applies to naval heavy shells or only to land AT rounds]**.
- **Italian quality problems:** some firms "did not all produce projectiles of proper quality". Some actions saw runs of good shells, others "terribly bad examples" ([NW-IT381]).
- **French 380 mm:** the original base cap broke under firing pressure. It was redesigned, with gas cavities filled with cement ([NW-FR380]).

### 1.7 Representative shell table

W/D³ in lb/in³; filler % is filler weight over shell weight [INFERRED arithmetic from the cited weights].

| Era | Nation / shell | Cal. | Mass | W/D³ | Cap | Filler (type, %) | Fuze / delay | Src |
|---|---|---|---|---|---|---|---|---|
| 1867–1909 | UK Palliser shell | various | — | — | none (chilled nose) | BP ~1–1.5% | none (impact-ignited) | [NG-S1], [WP-Pall] |
| 1894–1905 | Russia 12" AP "old model" (1895) | 12" | 331.7 kg / 731 lb | **0.42** | uncapped (APC rare) | wet pyroxylin, 5.3 kg = 1.6% (other sources 1.3–1.5%) | Brink base tube (delayed) | [NW-RU12], [TW-RU] |
| 1894–1905 | Russia 12" HE "old model" | 12" | 731 lb | 0.42 | — | 12.4 kg = 3.7%; 2nd Sqn: smokeless powder ~6 kg | 1894 base inertial tube ~0.005 s | [NW-RU12], [TW-RU2] |
| 1904–05 | Japan 12" AP "Furoshiki" | 12" | 386 kg / 850 lb | 0.49 | — | Shimose 19.3 kg = **5.0%** | Ijuin base (sensitive) | [NW-JP12], [WP-Tsu] |
| 1904–05 | Japan 12" Common | 12" | 850 lb | 0.49 | — | BP 39 kg = 10.1% | Krupp-pattern fuze | [NW-JP12] |
| 1908–16 | UK 12" APC Mk VIa 4crh | 12" | 859 lb | 0.50 | soft, 4.0% | Lyddite 27.3 lb = 3.2% | No. 16 base (non-delay) | [NW-12in], [NW-BrFz] |
| 1908–16 | UK 12" CPC Mk VIIa | 12" | 850 lb | 0.49 | — | BP 80 lb = 9.4% | base | [NW-12in] |
| 1915–16 | UK 15" APC Mk Ia 4crh | 15" | 1,920 lb | 0.57 | soft | Lyddite 60.5 lb = 3.2% | No. 16 base | [NW-15in] |
| 1915–16 | UK 15" CPC Mk IIa | 15" | 1,929 lb | 0.57 | — | BP 129.3 lb = 6.7% | base | [NW-15in] |
| 1911–18 | Germany 30.5 cm Psgr. L/3,4 (APC) | 30.5 cm | 405 kg / 893 lb | 0.52 | hard-ish (Krupp) | TNT 10.8 kg = 2.7% | Bdz delay ~0.025 s (nominal) | [NW-GE305], [NW-BrFz] |
| 1911–18 | Germany 30.5 cm Spgr. L/3,8 Bdz | 30.5 cm | 405 kg | 0.52 | — | TNT 24.8 kg = 6.1% | base; 2–6 m travel in unarmoured structure | [NW-GE305] |
| 1918– | UK 12" APC Mk VIIa Greenboy | 12" | 854 lb | 0.49 | hard, **11.6%** | Shellite 20.3 lb = 2.4% | No. 16D, 0.025 s | [NW-12in], [NW-BrFz] |
| 1918– | UK 15" APC Mk IIIa Greenboy | 15" | 1,910 lb | 0.57 | hard | Shellite 45.3 lb = 2.4% | No. 16D, 0.025 s | [NW-15in] |
| 1920s | Russia/USSR 12" AP mod 1911 | 12" | 470.9 kg / 1,038 lb | **0.60** | APC | 12.84 kg = 2.7% | — | [NW-RU12] |
| 1921–41 | US 16" AP Mk 3 (Colorado) | 16" | 2,110 lb | 0.52 | contractor-designed | Expl D 57.5 lb = 2.7% | delay, tetryl-boosted BDF (from late 1920s/early 1930s) | [NW-US16-45] |
| c.1927 | UK 16" APC (Nelson, "light") | 16" | 2,048 lb [UNCERTAIN, from memory] | 0.50 | — | TNT/beeswax | — | [NW-BrFz] (light APC noted) |
| 1938–45 | US 16" AP Mk 5 | 16" | 2,240 lb | 0.55 | hard, wide | Expl D 33.6 lb = 1.5% | BDF | [NW-US16-45-5] |
| 1941–45 | US 16" AP Mk 8 | 16" | 2,700 lb | **0.66** | heavier cap (Mod 8) | Expl D 40.9 lb = 1.5% | **Mk 21 BDF, 0.033 s**; needs 1.5" at 0° / 0.375" at 65° | [NW-US16-50] |
| 1940–45 | UK 14" APC Mk VIIb 6crh | 14" | 1,590 lb | 0.58 | hard | Shellite 39.8 lb = 2.5% | No. 159? 0.025 s [UNCERTAIN] | [NW-14in], [DTIC-RD] |
| 1938–45 | UK 15" APC Mk XVIIb 6crh | 15" | 1,938 lb | 0.57 | hard | Shellite 48.5 lb = 2.5% | delay base | [NW-15in] |
| 1939–45 | Germany 38 cm Psgr. L/4,4 m.Hb | 38 cm | 800 kg / 1,764 lb | 0.53 | hard + 10 cal ballistic cap | TNT/wax (Fp) 20.3 kg = 2.5% | Bd.Z. 38 (C/38), 0.035 s | [NW-GE38], [NW-GerFz] |
| 1939–45 | Germany 38 cm Spgr. L/4,5 Bdz (capped) | 38 cm | 800 kg | 0.53 | AP cap | TNT 35.0 kg = 4.4% | Bd.Z., 0 / 0.015 / 0.035 s | [NW-GE38], [NW-GerFz] |
| 1940–43 | Italy 381 mm APC | 381 mm | 884.8 kg / 1,951 lb | 0.58 | yes | cast TNT 10.16 kg = **1.15%** | base [designation UNCERTAIN] | [NW-IT381] |
| 1940–43 | Italy 381 mm SAPBC | 381 mm | 824.3 kg | 0.54 | — | TNT 29.5 kg = 3.6% (two cavities, two fuzes) | base | [NW-IT381] |
| 1940– | France 380 mm OPfK Mle 1936 | 380 mm | 884 kg / 1,949 lb | 0.58 | 84 kg = 9.5% | 21.9 kg = 2.5% | [UNCERTAIN] (US-built shells: Mk 21) | [NW-FR380] |
| 1931–45 | Japan 46 cm Type 91 / Type 1 APC | 46 cm | 1,460 kg / 3,219 lb | 0.54 | break-away cap head + windscreen | TNA 23.9 kg = 1.6% (with shock liner) | Type 91 long delay; 0.4 s quoted [UNCERTAIN] | [NW-JP46], [NW-T92], [NW-Misc] |
| 1941–45 | US 16" HC Mk 13 | 16" | 1,900 lb | 0.46 | none | Expl D 153.6 lb = 8.1% | PDF Mk 29 nose + base Mk 28/39 (inst.) / Mk 48 (0.01 s) | [NW-US16-50] |

**W/D³ trends [INFERRED]:**
- Light shells, about 0.42–0.50: Russian 1895, RN 12" pre-WWI, Japanese 1905, Nelson 16".
- Standard shells, about 0.50–0.58: most WWI and WWII designs.
- Super-heavy shells, about 0.60–0.66: US Mk 8 and the Russian mod 1911.

---

## 2. Bursting charges

### 2.1 Table

| Filler | Era / users | RE (TNT = 1) | Det. velocity | Impact sensitivity / AP suitability | Src |
|---|---|---|---|---|---|
| Black powder | All navies to c.1900; RN CPC to the 1920s | **0.55** | ~400 m/s (deflagrates) | Ignites on impact. Common shell often went off before penetrating. Good incendiary effect and structural "heave". Palliser shell needed no fuze. | [WP-TNTe], [NG-S1], [NG-S2] |
| Brown/smokeless powder as filler | Russia 1904–05 HE (2nd Pacific Sqn) | low **[INFERRED]** | — | Insensitive. Author estimates it 1.25–1.67× weaker than pyroxylin. Caused no storage or firing problems. | [TW-RU2], [TW-RU3] |
| Guncotton / pyroxylin (wet) | Russia 1890s–1905; early RN | ~1.10 (dry NC) | 6,400 m/s (dry) | Wet at **25–30%** moisture for safety, density about 1.38 g/cc. Wet guncotton "often failed to detonate after penetrating". The 1894 Russian tube's primer was too weak for pyroxylin. | [WP-TNTe], [TW-RU2], [NG-S2], [TW-RU] |
| Picric acid: Melinite (FR 1887), Lyddite (UK 1888), Shimose (JP) | 1890s–1918 (UK HE to 1918+, JP to 1931) | **1.17** | 7,350–7,480 m/s | Sensitive. Forms metal picrates that are even more sensitive. Lyddite "rather too sensitive", detonating while penetrating. Shimose pure picric acid sealed with lacquer and wax. Caused bore prematures, and many Japanese shells burst on hitting water. | [WP-TNTe], [WP-Pic], [NG-S2], [WP-Tsu], [NW-JP12] |
| TNT (Fp 02, Trotyl) | Germany from 1902; widely post-1918 | 1.00 | 6,900 m/s | Insensitive enough for AP; Germany pioneered it. Needs a booster; Germany used a picric booster to 1945, judged "not quite powerful enough". Wooden block in the nose cavity as a shock absorber. Weak booster caused duds and low-order bursts. | [WP-Pic], [NW-GerFz], [NW-GE305], [NW-BrFz] |
| TNT / beeswax | UK post-WWI 6" CPBC, 8" SAPC, Nelson 16" light APC; Japan 1922 (German pattern) | ~0.95 [INFERRED] | — | Desensitised with wax. | [NW-BrFz], [NW-JapProj] |
| Shellite (UK) | 1917/1918–1945 RN APC and HE | not found; "less powerful" than lyddite | — | Wartime mix 60% lyddite / 40% dinitrophenol; 70/30 adopted postwar (50/50 gave too little fragmentation). Stays inert under armour impact, so delay fuzes can work. Needed a CE (tetryl) booster from the late 1920s for 90% full detonation. | [NW-15in], [NG-Jut], [NW-BrFz], [WP-Shel] |
| Explosive D / Dunnite (ammonium picrate) | USN AP from 1907 to WWII and beyond | not found [UNCERTAIN; commonly cited about 0.95–1.0] | — | "Insensitive enough to permit penetration without premature detonation." Packing density raised in the mid-1920s. | [WP-Pic], [WP-AmPic], [NOG-3], [NW-US16-45] |
| TNA (trinitroanisole), "Type 91 bakuyaku" | IJN from 1931 | not found [INFERRED about 1.0–1.1 from VoD] | 7,200 m/s at 1.61 g/cc | More stable than Shimose but needed heavy cushioning. Type 91 AP had a liner filling ⅓–½ of the cavity. Okun: Japan "wanted the most powerful explosive" it could get, accepting a small filler. | [WP-TNA], [NW-JapProj], [NW-JP46], [NW-T92] |
| Tetryl (CE) | booster: US Army 1918, UK late 1920s; USN delay BDF late 1920s/early 1930s | 1.25 | 7,770 m/s | Booster only. | [WP-TNTe], [NW-GerFz], [NW-US16-45] |
| RDX mixes (Comp A-3, Comp B, TNT/RDX) | WWII HE (RN 15" HE Mk VII TNT/RDX); Comp A-3 replaced Expl D after WWII | 1.33 (Comp B), 1.60 (RDX) | 7,840–8,600 m/s | Too sensitive for AP unless phlegmatised. | [WP-TNTe], [NW-15in], [WP-AmPic] |

### 2.2 Why AP used insensitive fillers, and what went wrong

- **Penetration is a shock event.** A sensitive filler (picric acid) can detonate from the impact shock alone, before or during penetration. With RN lyddite APC at Jutland this was "partly" why penetration was poor: the bursters often burst immediately on impact ([NW-15in]). The pre-Dreyer list of faults ([NG-Jut]):
  - lyddite detonated on impact
  - fuzes had no effective delay
  - caps were soft and tore off at obliquity
  - bodies were weak and broke up
- **Insensitive fillers need a strong booster.** If the booster is too weak, the result is a low-order burst or a dud:
  - German TNT with a picric booster and an over-strong detonator gave many duds and low-order bursts ([NW-BrFz]).
  - British Shellite with only a black-powder fuze magazine often gave "low-power explosions or partial detonations" until the CE booster ([NW-BrFz]).
  - The RN 1922–25 R.D. gaine programme was explicitly motivated by the Jutland finding that "German shell fillings detonated while British fillings only exploded". The *Monarch* trials of 1925 showed greater fragment effect from true detonation ([DTIC-RD]).
- **Burster cavity and shell strength trade off.** More filler means thinner walls, which means more break-up. Hence USN AP at 1.5% and Italian AP at 1.15%. Japan chose TNA plus a liner for the same reason ([NW-US16-45-5], [NW-IT381], [NW-T92]).
- **Fragment character:** lyddite gives many small splinters, powder more structural damage and fires ([NG-S2]). At Baden (1921), CPC "had blast effect, but its fragments were too small" ([NW-15in]).

### 2.3 Low-order and filler-failure outcomes (Okun taxonomy)

The outcomes are ([NW-Misc]):
- full detonation
- partial detonation, which still usually breaks the body into many small pieces
- deflagration, a non-detonating explosion that "merely" breaks the projectile apart
- inert dud

A damaged projectile that can still explode should have its residual velocity cut "by a large amount (cut it in half)" ([NW-Misc]).

---

## 3. Fuzes

### 3.1 Nose vs base

- **Nose (point-detonating, PDF)** fuzes fire on first contact and act faster than base fuzes ([NOG-3]). They are fitted to HE, HC and common shell.
  - Okun on the USN 16" HC with an instantaneous PDF: the shell "destroys itself before its nose tip can move more than a few inches forward" and cannot penetrate 3" of homogeneous armour ([NW-T92]).
  - A deck or hatch-casing hit "almost certainly" sets off a nose fuze before the shell reaches a barbette ([NW-T92]).
  - German nose-fuzed HE with ballistic caps had a rod running from the shell nose to the fuze to function on oblique strikes ([NW-GE38]).
- **Base** fuzes sit in the base, protected by the shell body, and are "operated by the inertia of the shell slowing down as it strikes the target" ([NG-F2]). They are used alone in AP and common shell ([NOG-3]).
- **Auxiliary Detonating Fuze (ADF):** a USN safety relay below the nose fuze ([NG-F3]).
  - Tests showed homogeneous plate of **0.075 calibre or more**, at any obliquity, would set off the ADF when the base fuze was used alone ([NW-Misc]).
  - ADF function window for contact HE hits: striking velocity **929–2,300 ft/s** ([NW-Misc]).

### 3.2 How an AP base fuze arms

- **Set-back:** the firing acceleration releases a lock. A 75 mm shell sees about 20,000 g ([USNI-1917]).
  - In the German Bd.Z., a set-back pin releases an interlocked ring of shutters ([NW-GerFz]).
  - In the Russian 1894 tube, the inertia "extensor" compresses a safety spring on firing and frees the striker ([TW-RU]).
- **Centrifugal (spin)** arming completes the sequence. Mechanisms included ([NW-T20]):
  - screw threads that spin a few turns
  - ball bearings (US Mk 23)
  - shutters that swing outward in sequence
  - locking rings with thrown slides (US WWII BDFs)
- Most post-WWI designs delay arming until the shell is clear of the muzzle ([NW-T20]).
- **RN interwar DD/L/3000-family arming:**
  - A long-stroke detent locks in the armed position under spin.
  - An upper centrifugal bolt releases once spin overcomes friction; the exact arming point is unknown.
  - A lower centrifugal bolt clears the flash path ([DTIC-RD]).
- **Creep spring:** spring strength, pellet mass and detonator sensitivity are tuned so that "slight obstructions do not fire the fuze". Creep spring values were 86 oz (Fuze 260), 100 oz (No. 345), 160 oz (No. 479), with a range of 120–200 oz tested ([DTIC-RD]). **[INFERRED]** This spring is the physical origin of a minimum-plate threshold.

### 3.3 How it fires and delays

- **Firing:** on deceleration the inertia pellet or primer weight moves forward onto a fixed needle.
  - RN: "the inertia pellet moves forward onto the needle" ([DTIC-RD]).
  - German: "the primer weight slides onto the pin on impact" ([NW-GerFz]).
  - US standard BDF ([NG-F3]): impact drives the plunger forward and aligns the flash holes. The primer gas locks the plunger and frees a secondary firing pin, which sets off a secondary primer and then the delay element. The design was probably meant for oblique-impact reliability.
- **Delay elements:**
  - Mechanical function alone takes about **0.003 s**, the minimum for a non-electrical fuze ([NW-T20]). The German Bd.Z. "None" setting was still about 0.003 s ([NW-GerFz]).
  - Black-powder delay elements: "usually 0.006–0.03 second" ([NW-T20]).
- **Reverse-action (US Mk 23):** impact compresses a heavy spring, which throws the plunger *backward* onto the firing pin when deceleration *ends* ([NG-F3], [NW-T20]).
  - Intended for thin plates and high obliquity.
  - Pellet delays were 0.01 s (thin) to 0.025 s (thick), fixed at 0.025 s in 1941 ([NW-Misc]).
  - It "could not be reliably mass produced" and was withdrawn in 1941–42, replaced by the Mk 21 ([NW-Misc], [NW-T20]).
- **Corrosion:** filler fumes corroded US fuze internals within about 6 months; a Bakelite coating fixed it ([NG-F3]).

### 3.4 Minimum plate to start a base fuze

- **Physics:** a heavy shell hitting a thin plate "can slow too little" to drive the inertia element against its creep spring and safeties, so it passes through un-started ([NG-F2]). **[INFERRED]** The relevant quantity is the deceleration impulse, which scales roughly with plate areal density divided by shell sectional density (W/D²). That is why the threshold is quoted in **calibres**.
- **Okun's thresholds** ([NW-Misc]; formula as already held by the project; page `baseFuzePlateThicknessMinimums.php` [NOT VERIFIED this pass]):
  - Tfzmin = 0.07 D for single-plate fuzes, 0.05 D for two-plate fuzes.
  - Ob is frozen at 61° above that angle.
  - Face-hardened armour sets off any base fuze at any obliquity, unless the fuze is blinded.
- **Documented single values:**

| Fuze / shell | Threshold | In calibres [INFERRED] | Source |
|---|---|---|---|
| US Mk 21 BDF in 16" AP Mk 8 | 1.5" at 0°; 0.375" at 65° | 0.094 D; 0.023 D | [NW-US16-50] |
| RN No. 159 fuze in 16" APC (WWII) | "1" mild steel plate, if struck at normal" | 0.0625 D | [TT-Rod] (DNO Bevir, 29 Jan 1942) |
| USN ADF (not the base fuze) | 0.075 D homogeneous, any obliquity | 0.075 D | [NW-Misc] |

- **Cross-check of Okun's formula [INFERRED]:**
  - For D = 16", Tfzmin 0.07 gives 1.12" at 0°.
  - At 65° (Ob frozen at 61°): the bracket is (1+cos122°)/2 + 0.4537·sin(61°)^5.7019 = 0.235 + 0.211 = 0.446, giving **0.50"**.
  - The USN figures (1.5" / 0.375") imply a **stronger obliquity reduction (4:1)** than the formula (2.2:1).
  - The RN No. 159 (1") sits in between.
  - **Conclusion:** treat Tfzmin as fuze-specific, in the range 0.06–0.10 D at normal.
- **Combat confirmation:** DNO explained *Rodney*'s 16" shells passing through *Bismarck*'s superstructure without bursting by the 1" threshold ([TT-Rod]). Captured German officers said British 12" shells "seemed to go right through the ship without exploding in most cases" ([J1916]).
- **Obliquity and blinding:**
  - Earlier fuzes often failed at medium-to-high obliquity, because firing pins could be "cocked off at an angle", blinding the projectile ([NW-Misc]).
  - The US Mk 10 firing pin was "easily knocked out of alignment during oblique impacts" and was phased out ([NG-F2]).
  - **[INFERRED]** At obliquity the deceleration vector has a large lateral component, which both lowers the axial pulse (the fuze needs less plate along the path) and side-loads the striker (blinding risk).

### 3.5 Specific fuzes, by navy

**Royal Navy**

| Fuze | Era | Use | Delay | Notes / src |
|---|---|---|---|---|
| "Hotchkiss" base fuze | pre-1914 | early RN base percussion | — | **[UNCERTAIN]**: not found in any source this pass. |
| No. 11 / No. 12 / No. 15 | pre-1916 | — | — | **[UNCERTAIN]**: not found. |
| No. 13 | WWI | HE (lyddite) shell | instantaneous | Could detonate after a "hard knock" in handling, so HE was withdrawn for ship use after Jutland ([NW-12in], [NW-15in]). |
| No. 16 | WWI | APC / CPC base | non-delay | "Older, non-delay design" ([NW-BrFz]). Adapted to the Coventry Ordnance Works gaine in 1915 ([DTIC-RD]). |
| No. 16D | 1917/18– (Greenboy) | APC base | **0.025 s** (one source 0.024); bypass plate gives ~0.003 s | [NW-BrFz], [NG-F2] |
| DD/L/1000 (1924), DD/L/3000 (1928), DD/L/3000U (1931); medium DD/L/2000 = No. 500 | interwar development | large/medium BDF with R.D. gaine | DD/L/3000 non-delay could not burst within 10 ft of the plate. DD/L/2000 required 4–6 ft delay in 3" and 4.7" SAP. | [DTIC-RD] |
| No. 159, 345, 479, 480, 501 | WWII naval armour-attack base fuzes | 16" APC (No. 159); 6" CPBC (No. 480 set non-delay at *Bismarck*) | delay | [DTIC-RD], [TT-Rod]. "No. 352" was **not found**; probably a mis-recollection of No. 345 **[UNCERTAIN]**. |

The RN did not change its 0.025 s delay after WWI, to Okun's knowledge ([NW-BrFz]).

**US Navy**

| Fuze | Era | Use | Delay | Src |
|---|---|---|---|---|
| Pre-1920s base fuze | to late 1920s | AP | **non-delay, TNT-boosted** | [NW-US16-45] |
| Delay, tetryl-boosted BDF | late 1920s/early 1930s on | AP | — | [NW-US16-45] |
| Mk 11 | 1930s | AP (variable by target) | deceleration-embedded tube selects delay | Less reliable, reworked into Mk 23 ([NG-F3]). |
| Mk 17–20 BDF | 1930s–WWII | AP / common | 0.01 s, or none (~0.003 s inertial) | [NW-Misc] |
| Mk 23 BDF | to 1941–42 | AP / "Special Common", thin plates | 0.01–0.025 s pellet; fixed 0.025 s in 1941 | [NW-Misc], [NG-F3] |
| **Mk 21 BDF** | mid-WWII on | AP | **0.033 s** total | [NW-Misc], [NW-US16-50] |
| Mk 28 / 39 (inst.), Mk 48 (0.01 s) | WWII | HC base | 0 / 0.01 s | [NW-US16-50] |
| Generic 1957 doctrine | — | delay BDF | 0.02–0.033 s | [NOG-3] |

**Germany**
- **WWI** ([NG-F2], [NW-BrFz], [NW-Misc]):
  - Krupp/Skoda delay base fuze, introduced 1911 with block TNT fillers.
  - Black-powder delay of about 0.025 s, roughly 15 m of travel.
  - The detonator was too powerful and "blew through the delay or damaged the shell".
  - The corkscrew "tortuous tunnel" fix was not effective, and "a lot of their shells were duds".
  - Adding loose black powder was unreliable and lengthened the delay.
  - British estimate: nominal 0.025 s, but at most half the shells achieved anything near it ([NW-BrFz]).
- **WWII** ([NW-GerFz]):
  - Bd.Z. 38 eV / 36 eV: 0, 0.015 or 0.035 s, selectable.
  - Bd.Z. 38 (originally "C/38" for *Bismarck*): 0.035 s.
  - Bd.Z. 38 KV / 36 KV: 0.015 s.
  - Bd.Z. 36 SF: 0.015 or 0.035 s.
  - Bd.Z. 40 K: 0.015 s (?).
  - Bd.Z. 1521: no delay.
  - Fixed firing pin; set-back pin releases the shutter ring.
  - **"Bdz C/27" was not found** **[UNCERTAIN]**.
- Most German and Japanese fuzes of both wars *could* be started by multiple thin spaced plates. Some British fuzes and the US Mk 17–23 could not ([NW-Misc]).

**Japan**
- **1904–05:**
  - Ijuin base fuze, "sensitive", designed to burst on contact and wreck upperworks ([WP-Tsu]).
  - Bore prematures with Shimose at the Yellow Sea: *Asahi* lost both aft-turret guns and *Shikishima* one ([NW-JP12]).
  - At Tsushima, *Mikasa* and *Shikishima* each lost a barrel to bore prematures ([NW-JP12]).
  - Faulty fuzes were replaced with Krupp fuzes before Tsushima ([NW-JP12]).
- **1925–45:**
  - APC Model 1925 still had premature or low-order detonations. Type 88 (1928) improved the burster container ([NW-JapProj]).
  - Type 91 (1931) used TNA with a liner ([NW-JapProj]).
  - Delay is quoted as 0.4 s ([NW-T92]) or a range of 0.08–0.4 s ([NW-Misc]).
  - Okun also says the black-powder delay Japan actually used was 0.025–0.035 s ([NW-Misc]) **[UNCERTAIN, sources conflict]**.
  - Post-1920s Japanese fuzes changed only by getting *longer* delays, so shells often passed through targets before bursting, "a great boon to Allied navies" ([NG-F3]).
  - The Type 13 and Type 88 fuze designations were not verified.
- **Underwater hits** need "roughly an order of magnitude more delay than normal" ([NG-F3]).

**Russia (1904–05)**
- **1894 tube (Baranovsky modification)** ([TW-RU]):
  - Bottom (base) inertial impact tube, typical action about 0.005 s.
  - Suitable only for powder-filled shells; its capsule was too weak for pyroxylin.
  - Author's model: 6" shell vs 12 mm plate bursts about 5–5.5 m behind; a naval manual reports 5–6 m.
  - 12" HE vs thin plate: 15–41 m behind. Against cemented armour it bursts *while penetrating*.
- **Brink double-action tube:** used on steel AP and HE with pyroxylin ([TW-RU]). Wikipedia describes Russian AP as using guncotton charges with "insensitive delayed-detonation fuses" (Forczyk) ([WP-Tsu]). No delay figure was found **[UNCERTAIN]**.

**Italy and France**
- Fuze designations and delays were not found **[UNCERTAIN]**.
- The Italian 381 SAPBC had two cavities, each with its own fuze ([NW-IT381]).
- US-built French 380 APC used the Mk 21 BDF ([NW-FR380]).

---

## 4. Burst location physics

### 4.1 Kinematics [INFERRED]

- Burst distance behind the point of fuze start is roughly **s ≈ t_delay × v_residual**. Fuze start is plate impact for thick plate, or the first plate above threshold. Adjust for slowing by intervening structure.
- Okun's illustration: 0.003 s at 1,000 ft/s average is about 3 ft ([NW-Misc]).
- German WWI 0.025 s is about 15 m ([NG-F2]).

| Delay | v_res 800 ft/s | 1,200 ft/s | 1,600 ft/s |
|---|---|---|---|
| 0.003 s (inertial only) | 2.4 ft | 3.6 ft | 4.8 ft |
| 0.015 s (KM KV) | 12 ft | 18 ft | 24 ft |
| 0.025 s (RN No. 16D / US Mk 23 late) | 20 ft | 30 ft | 40 ft |
| 0.033 s (US Mk 21) | 26 ft | 40 ft | 53 ft |
| 0.035 s (KM Bd.Z. 38) | 28 ft | 42 ft | 56 ft |
| 0.4 s (IJN Type 91, upper bound) | 320 ft | 480 ft | 640 ft (through and out of any ship) |

These figures ignore the drop in residual velocity from structure and are an upper bound. The project's held Hood 1919 figure (34–40 ft) matches 0.025 s at about 1,400–1,600 ft/s **[INFERRED]**.

### 4.2 Measured / reported burst positions

- **Baden 1921 (15" Greenboy, reduced charges 1,550 ft/s):** an APC at 14.5° penetrated a 25 cm belt, passed a splinter bulkhead and the main deck, and burst about **11.6 m (38 ft)** behind ([NW-15in]). The overall conclusion was that penetration was satisfactory but "fuzing was not successful" ([NW-15in]). Other results ([NW-15in]):
  - Penetrated 35 cm turret face at 18.5° and barbette at 11°.
  - Defeated by a 35 cm conning tower at 30°.
  - 4 of 5 APC failed to penetrate a 10 cm turret roof.
  - SAPC showed no advantage over APC.
- **RN interwar fuze development** ([DTIC-RD]):
  - Mean delays of **15.8 ft** (perforated pellet), **11 ft** (grain powder) and **6.4 ft** (grain powder with capsule, R.D.461V).
  - The double flash-channel design gave about 9 ft non-delay.
  - "Drastic trial" range about 5.5–7 ft (partly illegible).
  - Medium SAP fuze requirement: 4–6 ft.
- **German WWI base-fuzed HE:** penetrated 2–6 m through unarmoured structure before bursting ([NW-GE305]).
- **Russian 1894 tube:** 5–6 m behind 12 mm plate (manual); 15–41 m for 12" (author model) ([TW-RU]).
- **Rodney vs Bismarck 1941:**
  - "no 16-inch shell were seen to burst on striking the armor plate", while 6" CPBC (No. 480 set non-delay) detonated on impact with armour ([TT-Rod]).
  - DNO also noted "the majority of BISMARCK shell failed to function when striking the water", which was contrary to experience with most German shell ([TT-Rod]).
- **IJN Type 91 at Cape Esperance:** one 8" hit reached *Boise*'s forward magazine via the underwater path. Flooding quenched the fire ([NG-S3]). This is the only confirmed "diving" success.
- **South Dakota (Okun reconstruction, 18" or 14" hit) ([NW-T92]):**
  - The deck impact removed the AP cap; "the nose shattered, preventing penetration".
  - The shell rebounded and "stood on its head", then detonated against the barbette.
  - **[INFERRED]** A long-delay fuze still fires after a shattered-nose, non-penetrating impact, i.e. a contact burst.

### 4.3 What happens if the shell stops in the plate, breaks up, or bursts while penetrating

- **Stopped or "stuck in plate" (SIP):** USN test codes are CP (complete), IP (incomplete), PP (partial, broken projectile) and SIP. Projectile state is coded E (intact) / NB (nose broken) / X (shattered), and filler "e" (effective) / "i" (ineffective) ([NW-14A]).
  - **[INFERRED]** A started fuze in a shell stuck in the plate gives a contact or "in-plate" burst. Effect behind the plate is then limited to plug, spall and blast through the hole.
- **Contact burst hole-size limits (Okun) ([NW-Misc]):**
  - A shell detonating on contact "barely" holes 0.156 calibre of STS.
  - STS up to 20% thicker than T_phe is dented and cracked.
  - "Penetration is not sharp" for contact explosions, ranging from a crack to a calibre-sized hole.
- **Burst while penetrating** (lyddite, Jutland) ([NG-Jut]):
  - Against 6"–9" plates, 17 shells hit: 3 penetrated cleanly, 6 burst while penetrating, and the rest failed to hole the plate or burst with no inside effect.
  - Note: the total is 19, not 17, because the article's arithmetic does not reconcile **[UNCERTAIN]**.
  - Austro-Hungarian attaché report: British shells "detonated on impact or at the latest when they passed through" ([TT-AH]).
- **Break-up:** a 15" APC broke up on a 6" plate at 20° (Projectile Committee, March 1917) ([NG-Jut]). Shell bodies "often broke up, preventing the filling from detonating or reducing its effect" ([NG-Jut]).
- **"Effective" vs "complete" penetration in acceptance testing:**
  - The USN Class A plate test (O.S. 751, 1947) let a plate *pass* if no more than half the broken projectile body came to rest behind it ([NW-OS751]).
  - The projectile had to show at least ⅓ of the nose gone.
  - Filler "ineffective" damage "was not required".
  - **[INFERRED]** Plate-acceptance and projectile-acceptance criteria are not the same as "fit to burst" behind the plate. A model should separate:
    - (a) perforation
    - (b) penetration with the body intact and the fuze working ("effective")
    - (c) partial or broken penetration (fragments only)

---

## 5. Multi-plate interaction with fuzing

- **Spaced plates starting the fuze:**
  - Okun's Tfzmin = 0.05 applies to fuzes "set off by two spaced plates of lesser thickness" ([NW-Misc]).
  - Most German and Japanese fuzes could be started by rapid successive thin plates. Some British and US Mk 17–23 fuzes could not ([NW-Misc]).
  - **[INFERRED] Model rule:** keep a running "fuze impulse" across plates within a short window. Fuzes in the "single-plate" class reset between plates; "two-plate" fuzes integrate.
- **Bomb decks / upper decks as fuze starters:**
  - The Iowa main (bomb) deck of 60 lb STS (1.5") was intended to detonate light-case bombs high in the ship and "start fuze action in AP bombs, ideally detonating them before they reach the armored deck" ([NG-A4]).
  - By the USN's own threshold, 1.5" at 0° just meets the Mk 21 requirement for a 16" shell. At deck-hit obliquities of 60–70°, the 0.375" figure means a 1.5" deck would very likely start a 16" BDF **[INFERRED]**.
  - Shell delay is then counted from the bomb deck. A 0.033 s delay at about 1,300 ft/s is roughly 43 ft of travel, or 9–15 m at a steep slant. That can put the burst at or just below the 4.75"+ armour deck, or on it **[INFERRED]**.
- **Bismarck 50 mm upper deck:** Wikipedia gives the thickness only, with no stated purpose ([WP-BisC]). **[UNCERTAIN]** The claim that it was intended to start fuzes or decap was not verified this pass. 50 mm is about 0.12 D for a 16" shell, which exceeds Okun's 0.07 D, so it would start most base fuzes at normal **[INFERRED]**.
- **Littorio:** the 36 mm weather deck has no stated purpose ([WP-Litt]). The 70 mm outer belt plate with a cellulite-filled 250 mm gap was intended to decap AP ([WP-Litt]). 70 mm is about 0.18 D for a 381–406 mm shell, enough to start a base fuze in addition to decapping **[INFERRED]**. **Consequence:** the delay clock starts at the outer plate, about 0.25 m before the main belt. At 0.035 s and about 1,500 ft/s the shell travels about 16 m before bursting **[INFERRED]**.
- **Decapping vs fuze start:** the same thin plate can both remove the cap (≥0.08 D homogeneous, 50/50) ([NW-Misc]) and start the fuze (≥0.05–0.07 D) **[INFERRED]**. This degrades the shell's attack on the main belt *and* starts the clock early.
- **Thin structure without starting the fuze:** shells passing through superstructure or unarmoured ends without starting the fuze explain overpenetration. Examples:
  - *Rodney* vs *Bismarck* ([TT-Rod]).
  - German 12" through British ships at Jutland ([J1916]).
  - The USN note that AP "can pass through unarmored craft without bursting" ([NOG-4B]).

---

## 6. Dud and failure statistics

| Battle / trial | Shell | What happened | Numbers | Src |
|---|---|---|---|---|
| Yalu 1894 | Chinese heavy | Many shells "filled with cement or porcelain", or the wrong calibre; condemned powder | qualitative | [WP-Yalu], [WW-Yalu] |
| Yalu 1894 | Japanese vs *Dingyuan*/*Zhenyuan* | No penetration of belts deeper than about 4 in | — | [WP-Yalu] |
| Santiago 1898 | US | <3% hits. Hits from guns ≥4": *Teresa* 10, *Vizcaya* 14, *Oquendo* 14 | dud rate not found | [NG-SAW8] |
| Yellow Sea 1904 | Japanese AP (Shimose) | 279 AP fired; ≥10 hit armour (mostly turrets); **0 penetrated**. Bore prematures on *Asahi* (2 guns) and *Shikishima* (1). | 0/10+ | [NW-JP12] |
| Yellow Sea 1904 | Japanese 12"/10" overall | 53 hits from 603 12" + 33 10" ≈ 8.3% | — | [TW-RU3] |
| Tsushima 1905 | Japanese | 446 12" fired, about 40 hits; no 12" penetrated >6" armour in the war; 2 bore prematures; est. 80–133 shells "lost" | — | [NW-JP12], [TW-RU3] |
| Tsushima 1905 | Russian | Light shells (331.7 kg) with small wet-pyroxylin or powder charges; did not burst on water | Dud % not found **[UNCERTAIN]** | [WP-Tsu], [TW-RU2] |
| Jutland 1916 | British heavy, >9" plate | 17 hits: 1 HE, 3 ricochets; of 13, 1 penetrated and burst, 5 holed with little effect, 7 did not hole | **1/13 effective** | [NG-Jut] |
| Jutland 1916 | British heavy, 6–9" plate | 3 clean penetrations, 6 burst while penetrating, rest failed | ~3/17 effective | [NG-Jut] |
| Jutland 1916 | *Seydlitz* | ≥16 (possibly 22) heavy hits, 5 pierced armour | 5/16–22 | [NG-Jut] |
| Jutland 1916 | British expenditure | 1,750 APC, 818 CPC, 567 HE (all 12"), 1,045 unknown | — | [NG-Jut] (commenter "bean") |
| Jutland 1916 | German | "At most half" of fuzes achieved near-nominal delay; many duds and low-order | ~≤50% proper function **[INFERRED]** | [NW-BrFz] |
| Baden 1921 | 15" Greenboy | Penetration satisfactory, fuzing "not successful"; 4/5 failed on 10 cm roof | qualitative | [NW-15in] |
| RN 1920s | Shellite APC | Without CE booster, low-power or partial detonations; CE booster raised full detonation to the "desired 90%" standard | ≥90% target | [NW-BrFz] |
| RN interwar | Fuze blinds | Failures "very greatly reduced", but the ignitory system had not reached "the same perfection"; most blinds traced to the ignitory | qualitative | [DTIC-RD] |
| Bismarck 1941 | German | "Majority" failed to function on water (unusual for German shell) | — | [TT-Rod] |
| WWII Italy | 381 mm | Quality variable by maker | qualitative | [NW-IT381] |

**Proof and acceptance testing:**
- **RN pre-Jutland:** tested only at normal impact ([NG-Jut]). Shells were issued even after failed proofs ([NG-Jut]). Jellicoe's 1910 call for realistic oblique trials was rejected on cost ([J1916]). One commenter claims a protocol that could pass lots with more than 50% defective ([NG-S3]) **[UNCERTAIN; reader claim]**. Unsteady shells at plate proof could invalidate results, and each thick-armour round could cost £2,000 or more ([NW-15in]).
- **RN Greenboy standards:** 15" vs 10" at 20°, and 12" vs 6" ([NG-Jut]).
- **USN plate tests (O.S. 751):** used the best service projectiles. A 16" AP Mk 8 against 20.6–20.9" Class A was struck at about 2,000–2,100 ft/s at about 29–30° ([NW-OS751]). Projectile lot acceptance shots also appear in the 14" Mk 16 data: 9.2–17.9" plates at 15–35.5° and 1,340–2,153 ft/s ([NW-14A]).
- The projectile-lot pass rule (e.g. "x of n must perforate intact") was **not found** this pass **[UNCERTAIN]**.

---

## 7. HE and common shell against armour

- **Nose-fuzed instantaneous HE:** effectively zero penetration of real armour.
  - USN 16" HC with PDF cannot penetrate 3" homogeneous ([NW-T92]).
  - Contact detonation holes about **0.156 D** of STS, and cracks plate up to about 20% thicker ([NW-Misc]).
  - Okun: a Type 0 HE (about 80 lb TNA) would "make little more than a scratch" on barbette armour ([NW-T92]).
  - **[INFERRED]** For a 16" shell, the holing limit is about 2.5" STS; for 12", about 1.9".
- **Base-fuzed common and CPC:** designed for ⅓–½ D plate at normal (USN common; RN CPC) ([NOG-4B], [NG-Jut]).
  - At Baden, 15" CPC at 1,380 ft/s penetrated 17 cm (0.45 D) but not 35 cm at 12° ([NW-15in]).
  - German capped Spgr. L/4,5 Bdz was "more of a SAPC" ([NW-GE38]).
- **Pre-dreadnought common shell:** black powder at about 6–10%, impact-fuzed. Effective on upperworks and fires; most Russian damage at Tsushima came from powder Common shells, which set "large fires in the upper works" ([NW-JP12]).
  - The Japanese switched from failed lyddite AP to powder Common after the Yellow Sea ([NG-S2]).
  - Japanese Shimose HE often burst on hitting water ([WP-Tsu]).
- **HE splinters vs thin plate:** not researched here. See the fragmentation docs.

---

## 8. What a model needs from this

Suggested projectile and fuze parameter set, with era defaults drawn from the evidence above. All defaults are **[INFERRED]** syntheses.

### 8.1 Projectile parameters

| Parameter | Classes / range | Evidence |
|---|---|---|
| **Body class** | Chilled iron (Palliser); early forged steel; Ni-Cr forged (1890s–1916); improved (Greenboy, 1918–30); modern WWII (about 40% better than 1918) | §1.2–1.6 |
| **Hardness profile** | uniform; hard nose / tough body (differential); WWII decremental with tough body | [NOG-4B], [WP-AP] |
| **Cap class** | none; soft (≈4–9% mass, effective ≤15° Ob); hard (≈9–12%, Krupp 1911, Greenboy 1918, all WWII APC); Japanese break-away head | §1.4 |
| **Windscreen / ballistic cap** | none / yes (lost on any impact) | [NW-Misc] |
| **Nose shape** | 2 crh (pre-1909 RN) → 4 crh → 6 crh / 5/10 crh → 9 crh US Mk 8; plus blunt vs pointed for deck performance (US Mk 8 Mod 6 blunter) | [NW-12in], [NW-15in], [NW-US16-50] |
| **W/D³ (lb/in³)** | light 0.42–0.50; standard 0.50–0.58; super-heavy 0.60–0.66 | §1.7 |
| **Filler %** | AP 1.1–3.5; SAP/CPC 3.5–9.4; HE 6–15 | §1.7, [NW-Misc] |
| **Body integrity vs obliquity** | WWI RN APC breaks at ≥20° on 0.4–0.5 D plate; Greenboy OK to about 20° on 0.67 D | [NG-Jut], [NW-12in] |

### 8.2 Filler parameters

| Filler | RE | Impact-shock class | p(premature/in-plate burst) default | p(low-order) default |
|---|---|---|---|---|
| Black powder | 0.55 | ignites on impact | high (common shell) | n/a (deflagrates) |
| Wet guncotton (RU) | ~1.1 dry | insensitive | low | high (weak primer) [UNCERTAIN] |
| Picric (lyddite/Shimose/melinite) | 1.17 | **sensitive** | high: Jutland 6/17 burst while penetrating on 6–9" [NG-Jut] | low |
| TNT (+ picric booster, KM) | 1.0 | insensitive | low | moderate (WWI high) |
| Shellite (+ CE from late 1920s) | ~1.0 [UNC] | insensitive | low | ≤10% with CE; higher before |
| Explosive D | ~1.0 [UNC] | very insensitive | very low | low |
| TNA + liner | ~1.0–1.1 [INF] | moderate (needs liner) | low–moderate | moderate (Model 1925 shell) |

### 8.3 Fuze parameters

| Parameter | Values |
|---|---|
| **Position** | nose (instant, about 0.001 s) / base |
| **Arming** | set-back + spin; assume armed beyond the muzzle for all post-WWI fuzes |
| **Start threshold Tfz (calibres at normal)** | ≈0.06 D (RN No. 159) to ≈0.094 D (US Mk 21). Okun default 0.07 (single-plate) / 0.05 (two-plate). Face-hardened plate always starts it. |
| **Obliquity reduction** | Okun bracket (≈0.45 at ≥61°) vs US Mk 21 data (0.25 at 65°). Use a fuze-specific factor. |
| **Multi-plate integration** | "two-plate" class (most KM/IJN) integrates; "single-plate" class (some RN, US Mk 17–23) does not |
| **Delay** | 0.003 s (inertial/non-delay); 0.01–0.015 s (light/KV); 0.025 s (RN No. 16D, KM WWI nominal, US Mk 23 late); 0.033–0.035 s (US Mk 21, KM Bd.Z. 38); 0.08–0.4 s (IJN diving, [UNCERTAIN]) |
| **Delay scatter** | KM WWI: ≥50% of fuzes far off nominal [NW-BrFz]. RN interwar: mean 6–16 ft by design variant [DTIC-RD]. Suggest σ ≈ 20–30% of nominal [INFERRED]. |
| **Blind / dud (fuze)** | Era defaults [INFERRED]: pre-1905 high (Russian 1894 tube unsuited to pyroxylin); WWI KM ~30–50%; WWI RN no effective delay (bursts early, not duds); 1920s+ RN/US/KM ~5–10%; Mk 23 unreliable (withdrawn) |
| **Oblique blinding** | added failure chance rising with obliquity for older fuzes (US Mk 10, pre-WWI) [NW-Misc], [NG-F2] |
| **Water-entry behaviour** | KM WWII usually functions on water (but *Bismarck* 1941 mostly did not) [TT-Rod]; Japanese 1904 Shimose bursts on water [WP-Tsu]; IJN Type 91 designed to survive water |

### 8.4 Outcome states the model should carry

Following the USN test taxonomy ([NW-14A], [NW-OS751]):
- Penetration: {CP, PP (broken), IP, SIP, ricochet}
- Body: {E, NB, X}
- Filler: {effective, ineffective}
- Fuze: {started, not started, blinded}
- Burst: {before plate (premature), in plate, behind plate at s = t_delay × v_res, no burst (dud / overpenetration)}
- Burst order: {high-order, partial, deflagration}

The "effective" result needs CP or PP with E or a minor-NB body, the fuze started and working, and the filler effective.

---

## Sources

- [NG-S1] Naval Gazing, *Shells Part 1* — https://www.navalgazing.net/Shells-Part-1
- [NG-S2] Naval Gazing, *Shells Part 2* — https://www.navalgazing.net/Shells-Part-2
- [NG-S3] Naval Gazing, *Shells Part 3* — https://www.navalgazing.net/Shells-Part-3
- [NG-F1] Naval Gazing, *Fuzes Part 1* — https://www.navalgazing.net/Fuzes-Part-1
- [NG-F2] Naval Gazing, *Fuzes Part 2* — https://www.navalgazing.net/Fuzes-Part-2
- [NG-F3] Naval Gazing, *Fuzes Part 3* — https://www.navalgazing.net/Fuzes-Part-3
- [NG-Jut] Naval Gazing, *Shells at Jutland* (after Campbell / Brooks) — https://navalgazing.net/Shells-at-Jutland
- [NG-A4] Naval Gazing, *Armor Part 4* (Iowa) — https://www.navalgazing.net/Armor-Part-4
- [NG-SAW8] Naval Gazing, *Spanish-American War Part 8* — https://navalgazing.net/Spanish-American-War-Part-8
- [NW-BrFz] Okun, *British Fuzes after Jutland* — https://navweaps.com/index_nathan/British_Fuzes_after_Jutland.php
- [NW-Misc] Okun, *Miscellaneous armor/projectile notes* (Miscarmr) — http://www.navweaps.com/index_nathan/Miscarmr.php (first ~100k chars read)
- [NW-GerFz] Okun, *German WWII fuzes* — https://navweaps.com/index_nathan/germanWWIIFuzes.php
- [NW-14A] Okun, *14" Mk 16 Mod 4 vs Class A* — https://navweaps.com/index_nathan/14inchMk16Mod4VsClassA.php
- [NW-OS751] Okun, *O.S. 751 ballistic tests* — https://navweaps.com/index_nathan/os751BalTests.php
- [NW-T20] NavWeaps Tech Board 020 (fuzes) — https://www.navweaps.com/index_tech/tech-020.php
- [NW-T92] NavWeaps Tech Board 092 — https://navweaps.com/index_tech/tech-092.php
- [NW-12in] NavWeaps, British 12"/45 Mk X — https://navweaps.com/Weapons/WNBR_12-45_mk10.htm
- [NW-15in] NavWeaps, British 15"/42 Mk I — http://www.navweaps.com/Weapons/WNBR_15-42_mk1.php
- [NW-14in] NavWeaps, British 14"/45 Mk VII — http://www.navweaps.com/Weapons/WNBR_14-45_mk7.php
- [NW-US16-45] NavWeaps, US 16"/45 Mk 1 — http://www.navweaps.com/Weapons/WNUS_16-45_mk1.php
- [NW-US16-45-5] NavWeaps, US 16"/45 Mk 6 (Mark 5 page) — http://www.navweaps.com/Weapons/WNUS_16-45_mk5.php
- [NW-US16-50] NavWeaps, US 16"/50 Mk 7 — http://www.navweaps.com/Weapons/WNUS_16-50_mk7.php
- [NW-GE305] NavWeaps, German 30.5 cm SK L/50 — http://www.navweaps.com/Weapons/WNGER_12-50_skc12.php
- [NW-GE38] NavWeaps, German 38 cm SK C/34 — http://www.navweaps.com/Weapons/WNGER_15-52_skc34.php
- [NW-IT381] NavWeaps, Italian 381/50 — http://www.navweaps.com/Weapons/WNIT_15-50_m1934.php
- [NW-FR380] NavWeaps, French 380/45 Mle 1935 — http://www.navweaps.com/Weapons/WNFR_15-45_m1935.php
- [NW-JP12] NavWeaps, Japan 12"/40 EOC — https://navweaps.com/Weapons/WNJAP_12-40_EOC.php
- [NW-JP46] NavWeaps, Japan 46 cm Type 94 — http://www.navweaps.com/Weapons/WNJAP_18-45_t94.php
- [NW-JapProj] NavWeaps, Japanese projectile terminology — https://navweaps.com/Weapons/WNJAP_projectiles.php
- [NW-RU12] NavWeaps, Russian 12"/40 Pattern 1895 — http://www.navweaps.com/Weapons/WNRussian_12-40_m1895.php
- [NOG-3] *Naval Ordnance and Gunnery* (NAVPERS, 1957) ch. 3 — https://eugeneleeslover.com/USNAVY/CHAPTER-3-4.html
- [NOG-4B] *Naval Ordnance and Gunnery* ch. 4 — https://eugeneleeslover.com/USNAVY/CHAPTER-4-B.html
- [DTIC-RD] T.C. Roberts, *The R.D. Gaine and its application to base detonating fuzes*, UK MoS PR&D 4.043 (1952) — https://apps.dtic.mil/sti/pdfs/ADA492806.pdf
- [USNI-J] Tisdale, "The Johnson Cast Steel Armor-Piercing Shot", USNI Proceedings, Jan 1894 — https://www.usni.org/magazines/proceedings/1894/january/johnson-cast-steel-armor-piercing-shot
- [USNI-1917] "Projectiles Containing Explosives", USNI Proceedings, Apr 1917 — https://www.usni.org/magazines/proceedings/1917/april/projectiles-containing-explosives
- [TT-Rod] Warships1 forum thread quoting *Rodney* CO report and DNO Bevir minute (29 Jan 1942) — https://www.tapatalk.com/groups/alltheworldsbattlecruisers/viewtopic.php?p=29030
- [TT-AH] Warships1 forum thread (Austro-Hungarian attaché on Jutland) — https://www.tapatalk.com/groups/warships1discussionboards/viewtopic.php?p=1095109
- [J1916] jutland1916.com, *Ordnance* — https://www.jutland1916.com/tactics-and-technologies-4/ordnance-2/
- [TW-RU] Topwar, *Fuses of Russian naval artillery … Tube arr. 1894* — https://en.topwar.ru/239438-vzryvateli-russkoj-morskoj-artillerii-perioda-russko-japonskoj-vojny-trubka-obr-1894-g.html
- [TW-RU2] Topwar, *On the power of Russian lightweight 305-mm shells* — https://en.topwar.ru/181233-o-moguschestve-russkih-oblegchennyh-305-mm-snarjadah-vremen-russko-japonskoj-vojny.html
- [TW-RU3] Topwar, *Failures of the Russian fleet … Projectile version* — https://en.topwar.ru/amp/246581-neudachi-rossijskogo-flota-v-russko-japonskoj-vojne-snarjadnaja-versija-nachalo.html
- [WP-Pall] Wikipedia, *Palliser shot and shell* — https://en.wikipedia.org/wiki/Palliser_shot_and_shell
- [WP-AP] Wikipedia, *Armor-piercing shell* — https://en.wikipedia.org/wiki/Armor-piercing_shell
- [WP-Shel] Wikipedia, *Shellite* — https://en.wikipedia.org/wiki/Shellite_(explosive)
- [WP-AmPic] Wikipedia, *Ammonium picrate* — https://en.wikipedia.org/wiki/Ammonium_picrate
- [WP-Pic] Wikipedia, *Picric acid* — https://en.wikipedia.org/wiki/Picric_acid
- [WP-TNTe] Wikipedia, *TNT equivalent* (RE table) — https://en.wikipedia.org/wiki/TNT_equivalent
- [WP-TNA] Wikipedia, *Trinitroanisole* — https://en.wikipedia.org/wiki/Trinitroanisole
- [WP-Tsu] Wikipedia, *Battle of Tsushima* — https://en.wikipedia.org/wiki/Battle_of_Tsushima
- [WP-Yalu] Wikipedia, *Battle of the Yalu River (1894)* — https://en.wikipedia.org/wiki/Battle_of_the_Yalu_River_(1894)
- [WW-Yalu] Weapons and Warfare, *The battle of the Yalu river* — https://weaponsandwarfare.com/?p=4001
- [WP-Litt] Wikipedia, *Littorio-class battleship* — https://en.wikipedia.org/wiki/Littorio-class_battleship
- [WP-BisC] Wikipedia, *Bismarck-class battleship* — https://en.wikipedia.org/wiki/Bismarck-class_battleship

**Not reached or blocked this pass:**
- navweaps `baseFuzePlateThicknessMinimums.php`: fetch refused by the tool. The formula is retained from project knowledge.
- dreadnoughtproject.org: fetch errors.
- 1915–1939 USN *Naval Ordnance* texts and the 1902 UK *Text-book of Gunnery*: not opened. The Fullam & Hart 1903 PDF index is at https://www.eugeneleeslover.com/ENGINEERING/Fullam/Fullam_Hart_COMPLETE_Pg_325d-356.pdf (Vol. 2 ch. XXVI projectiles p. 319, XXVII fuzes p. 332). This is the best next source for 1890s–1903 shells, fuzes and proof rules.

**Open items for a follow-up pass:**
- RN WWI fuze designations (Hotchkiss, Nos. 11/12/15)
- Japanese fuze designation and delay (0.4 s vs 0.025–0.035 s)
- Italian and French fuzes
- USN projectile-lot acceptance rule
- Hood 1919 trial
- Russian Brink tube delay and dud rate
- RE of Explosive D, Shellite and TNA
