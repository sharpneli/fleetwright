# 06 — Early-Era Armour and Penetration, ~1860–1910 (ironclads to early dreadnoughts)
Status: draft    Updated: 2026-10-16    Request: —

*Research note for the armour-penetration model. Scope: how guns and armour were matched, tested and rated from wrought iron to early Krupp cemented (KC). Covers the period's formulae, its "wrought-iron-equivalent" rating system, key proof trials, shell technology (Palliser, forged steel, caps), combat evidence through Tsushima, and pre-WWI practice. WWI/WWII face-hardened armour, fuzes and multi-layer plate are covered by other notes.*

**Tags.** **[INFERRED]** = my own derivation or calculation from cited numbers. **[UNCERTAIN]** = the source is weak or OCR-garbled, or sources conflict. Every number carries an inline link to its source. Where I recomputed a formula, the script is described in §1.6.

---

## 0. Executive summary

1. **The period's currency was "inches of wrought iron."** Gun tables quoted penetration in wrought iron. Plates were rated by a **"figure of merit"**: resistance to perforation compared with wrought iron, *against uncapped AP shot of roughly the plate's calibre* ([EB1911 "Armour Plates"](https://gutenberg.org/cache/epub/34162/pg34162-images.html)). The figure of merit was not a property of the plate alone. It changed with the shot (chilled iron, uncapped steel, capped steel), with velocity, and with which wrought-iron formula was used as the reference (§1.5, §2.3).
2. **The British reference formula was Tresidder's:** t² = w·v³/(d·A), with log A = 8.8410 (t and d in inches, w in lb, v in ft/s) ([EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html)). I checked numerically that Okun's T/D = 0.00003798 (W/D³)^0.5 V^1.5 ([Okun, NavWeaps](http://www.navweaps.com/index_nathan/Hstfrmla.php)) is the **same formula** **[INFERRED: constants agree to 4 significant figures]**. The **Krupp KC-vs-uncapped** formula t² = w·v²/(d·A₁), log A₁ = 6.3532 (EB1911), equals Okun's "Krupp KC vs uncapped AP" T/D = 0.0006659 (W/D³ V²)^0.5. The **De Marre** formula v = A₁·t^0.7·d^0.75/w^0.5, log A₁ = 3.0094 (EB1911), equals Okun's "De Marre nickel-steel" form with C = 1 **[INFERRED]**.
3. **Consensus equivalence ladder** (thickness multiplier relative to wrought iron, against the shot of the day). Compound or mild steel ≈ **1.25–1.3**. Nickel steel ≈ **1.3** (more in trials). Harvey ≈ **1.8–2.1** against uncapped shot. Krupp non-cemented ≈ **2.25**. KC ≈ **2.3–2.7** against uncapped shot but only **≈ 2.0 against capped shot** ([EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html); [USN *Naval Ordnance* 1937](https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php); [USNI 1894](https://www.usni.org/magazines/proceedings/1894/october/professional-notes); [Naval Gazing](https://www.navalgazing.net/Armor-Part-1)).
4. **Uncapped forged-steel AP shattered on Harvey and KC faces.** The cap (Russian trials 1894; Johnson cap trial at Indian Head, March 1894) restored penetration. It gave roughly **+15%** in general ([Naval Gazing, Shells 2](https://www.navalgazing.net/Shells-Part-2)), but **failed below about 1,700 ft/s and beyond about 30° obliquity** ([EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html)). In 1910 the British DNO expected 12-in APC to fail against even 4 in KC beyond 20° ([NavWeaps 12"/45 Mk X](https://navweaps.com/Weapons/WNBR_12-45_mk10.htm)).
5. **Combat 1894–1905 shows almost no penetrations of thick belts.** At the Yalu, no hit went deeper than 4 in into the Chinese belts ([Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_Yalu_River_(1894))). At Santiago, no Spanish side armour was pierced ([USNI 1899](https://www.usni.org/magazines/proceedings/1899/april/sketches-spanish-american-war)). At the Yellow Sea, at least 10 Japanese AP hits struck armour and none penetrated. Through the whole war, Japanese 12-in shells never penetrated Russian armour over about 6 in ([NavWeaps Japanese 12"/40](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php)). At Tsushima, *Oryol* took 4 hits on side armour and none penetrated ([Wikipedia](https://en.wikipedia.org/wiki/Russian_battleship_Oryol)).

---

## 1. Penetration formulae and rules in use, 1860–1910

### 1.1 The 1860s–70s "energy per inch of shot circumference" rule (Fairbairn/Noble era)

The 1860s Special Committee on Iron (Fairbairn was a member) and its successors rated guns by **striking energy per inch of shot circumference**, in foot-tons. Their rule was that resistance rises roughly with the **square of plate thickness** ([Wikipedia, Iron armour](https://en.wikipedia.org/wiki/Iron_armour); [Appleton's Cyclopaedia 1880 "Armor"](https://www.eugeneleeslover.com/ENGINEERING/Fullam/ARMOR_Article_Appletons_1880.pdf)). Appleton's quotes:

| Case | Required/achieved energy (ft-tons per inch circ.) | Source |
|---|---|---|
| 4.5 in unbacked plate, direct fire | 28 | [Appleton 1880](https://www.eugeneleeslover.com/ENGINEERING/Fullam/ARMOR_Article_Appletons_1880.pdf) |
| 14 in plate | 230 | same |
| 18- and 25-ton guns | 170 | same |
| Compound target at Spezia | 275 to penetrate | same |
| 100-ton gun at Spezia (mean) | 550 | same |

**[INFERRED]** These numbers fit **t ≈ 0.85–0.92 × √(E/circ)** (t in inches, E/circ in ft-tons per inch). This is the Fairbairn form T² ∝ W·V²/D. Okun's printed Fairbairn constant (T/D = 0.0007692·[(W/D³)V²]^0.5, [NavWeaps](http://www.navweaps.com/index_nathan/Hstfrmla.php)) gives t ≈ 0.52 √(E/circ), only about 60% of Appleton's values **[UNCERTAIN: probably a thin-plate or low-velocity calibration]**. As a cross-check, the 100-ton gun's 2,000 lb shot at 1,446 ft/s ([Wikipedia RML 17.72-in](https://en.wikipedia.org/wiki/RML_17.72-inch_gun)) gives 521 ft-tons per inch of circumference, against Appleton's mean of 550 **[INFERRED]**.

Appleton also noted that **laminated plate was inferior to solid plate**. A shot stopped by 4 in of solid plate went through 6 in of laminated plate. It also reported that oblique impact scaled with the sine of the angle of incidence ([Appleton 1880](https://www.eugeneleeslover.com/ENGINEERING/Fullam/ARMOR_Article_Appletons_1880.pdf)). The text cites "Noble's formula" but does not state it. I did not find it elsewhere.

### 1.2 The canonical formulae (exact forms)

Units, unless stated: T or t = plate thickness (in), D or d = calibre (in), W or w = shot weight (lb), V or v = striking velocity (ft/s), at normal impact.

| # | Name, date | Exact form as published | Armour / shot | Source |
|---|---|---|---|---|
| F1 | **Fairbairn** (Eng., c.1865) | T/D = 0.0007692·[(W/D³)V²]^0.5 | Wrought iron, thin plates | [Okun](http://www.navweaps.com/index_nathan/Hstfrmla.php) |
| F2 | **Tresidder** (Eng., early 1870s; standard through the 1900s) | t² = w·v³ /(d·A), **log A = 8.8410** ⇔ T/D = 0.00003798·(W/D³)^0.5·V^1.5 | Wrought iron, about 0.25–0.75 calibre, non-deforming shot | [EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html); [Okun](http://www.navweaps.com/index_nathan/Hstfrmla.php) |
| F2c | **Tresidder for KC vs capped** | t² = w·v³ /(4·d·A) (same A) | KC, capped AP | [EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html) |
| F2r | Rough rule, capped vs KC | t/d = v/1900 | KC, capped | EB1911 |
| F3 | **Krupp wrought iron** (Ger., early 1870s) | T/D = 0.00004643·[(W/D³)V²]^0.75 | Wrought iron | [Okun](http://www.navweaps.com/index_nathan/Hstfrmla.php) |
| F4 | **De Marre wrought iron** (Fr., late 1870s) | T/D = 0.00002778·D^0.1542·[(W/D³)V²]^0.7695 | Wrought iron | Okun |
| F5 | **Gâvre** (Fr., 1870s) | T/D = 0.00002887·D^0.42857·[(W/D³)V²]^0.71429 | Wrought iron; **used by the USN for acceptance-test wrought-iron equivalence** | Okun; [USNI 1894](https://www.usni.org/magazines/proceedings/1894/october/professional-notes) |
| F6 | **De Marre (1886/1890), "nickel-steel" or universal** | Metric: v = A·e^0.7·a^0.75 / p^0.5, log A = 1.7347 (v in m/s, e and a in cm, p in kg). English: v = A₁·t^0.7·d^0.75/w^0.5, **log A₁ = 3.0094**. Okun form: T/D = 0.00005021·D^0.07144·[(W/D³)(V/C)²·cos³θ]^0.71429 | Homogeneous steel (C = 1 for an average French 1890 nickel-steel plate); "basis of U.S. Navy ballistic acceptance tests" | [EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html); [Okun](http://www.navweaps.com/index_nathan/Hstfrmla.php) |
| F7 | **Krupp KC vs uncapped** (c.1895) | t² = w·v²/(d·A₁), **log A₁ = 6.3532** ⇔ T/D = 0.0006659·[(W/D³)V²]^0.5. Castner's metric version: p·v² = 5800·a·E² | KC, uncapped AP ("punching") | EB1911; Okun; [USNI 1896](https://www.usni.org/magazines/proceedings/1896/july/professional-notes) |
| F7r | Rule of thumb, uncapped punching (sectional density 0.46) | t/d = v/2200 | KC, uncapped | EB1911 |
| F7k | Krupp rule of thumb | "2000 ft/s striking velocity will give about the calibre perforation" | Hard-faced | [USNI 1896](https://www.usni.org/magazines/proceedings/1896/july/professional-notes) |
| F8 | **Davis, Harvey nickel steel vs uncapped** (US, c.1895) | T/D = 0.00003466·D^0.3333·[(W/D³)V²]^0.6667 | Harvey (about 1 in hard face) | Okun |
| F9 | **Davis, Harvey vs capped** (US, c.1900) | T/D = 0.00008582·D^0.25·[(W/D³)V²]^0.625 | Harvey, capped AP | Okun |
| F10 | Obliquity | Replace t by t·sec θ, valid up to 30° (EB1911). USN 1937: required velocity rises roughly with the **fourth power of sec θ**. De Marre: cos³θ term | — | [EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html); [USN 1937](https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php); Okun |

**Formulae requested but not found.**
- **"Garvie"**: I found no formula by this name. It is very probably a corruption of **Gâvre** (F5) **[INFERRED]**.
- **Makarov**: no Makarov penetration formula surfaced. Makarov is linked to the **cap**, not a formula ([Wikipedia](https://en.wikipedia.org/wiki/Stepan_Makarov)) **[UNCERTAIN]**.
- **"Jacob de Marre's later variant"**: the 1886 wrought-iron form (F4) and the c.1890 nickel-steel or universal form (F6) are the two versions. Later users added the plate-quality coefficient C and dropped cos³θ ([Okun](http://www.navweaps.com/index_nathan/Hstfrmla.php)).
- **Maitland**: no formula found.

### 1.3 USN usage

- **Acceptance tests for Harvey plates, 1894.**
  - First shot: velocity set by **Gâvre** to perforate a **wrought-iron plate 10% thicker** on 36 in of oak. No through-crack may run to an edge.
  - Second shot: velocity set by **De Marre** to perforate a **Creusot steel plate 15% thicker**. No part of the shot may pass through plate and backing.
  - These tests were "15% more severe" than those for oil-tempered nickel steel ([USNI Oct 1894](https://www.usni.org/magazines/proceedings/1894/october/professional-notes)).
- **Worked examples in the US 1937 text.** A 10 in, 500 lb shot at 1,772 ft/s gives **23.1 in of wrought iron** and **15.77 in of nickel steel**, a ratio of 1.46. For face-hardened plate a reduction coefficient K is applied to De Marre: "factor of performance", best between about 1,400 and 2,000 ft/s ([USN *Naval Ordnance* 1937, Ch. XII](https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php)).
  - **[INFERRED]** Tresidder gives 23.4 in for this case, so the 1937 wrought-iron figure is Tresidder-like. I checked the De Marre example to ±0.01%: my recomputation gives 1,771.9 ft/s.

### 1.4 The "penetrates X in of wrought iron" convention in gun tables

Gun tables (the *Text Book of Gunnery* and Brassey's) quoted wrought-iron perforation at the muzzle or at 1,000 yd. Examples I could confirm:

| Gun | Propellant / MV | Shell | Quoted penetration | Source |
|---|---|---|---|---|
| RML 17.72-in 100-ton (Armstrong) | Black powder; 1,446 ft/s at the 1876 trial; 1,550 spec; 1,585 chambered (1878) | 2,000 lb Palliser | "21 in of steel at 2,000 yd"; shot passed through wrought-iron plates with about 180 m/s to spare; broke up on Creusot steel | [Wikipedia](https://en.wikipedia.org/wiki/RML_17.72-inch_gun) |
| RML 12.5-in 38-ton | P2/Prism2 powder; 1,425 (Mk I) and 1,575 (Mk II) ft/s | 800–809 lb | — | [Wikipedia](https://en.wikipedia.org/wiki/RML_12.5-inch_38-ton_gun) |
| BL 13.5-in Mk I–IV | 630 lb brown SBC, 2,016 ft/s; 187 lb cordite, 2,099 ft/s | 1,250 lb | **28.2 in wrought iron at 1,000 yd**; 11 in "Krupp steel" at 3,000 yd | [Wikipedia](https://en.wikipedia.org/wiki/BL_13.5-inch_Mk_I_–_IV_naval_gun); [NavWeaps](http://www.navweaps.com/Weapons/WNBR_135-30_mk1.php) |
| BL 12-in Mk VIII (1890s) | 174 lb cordite; 2,350–2,367 ft/s | 850 lb | **33 in iron at 1,000 yd; 12 in steel at 3,100 yd** (Parkes) | [NavWeaps](http://www.navweaps.com/Weapons/WNBR_12-35_mk8.php); [Wikipedia](https://en.wikipedia.org/wiki/BL_12-inch_Mk_VIII_naval_gun) |
| BL 12-in Mk IX (1901) | Cordite; 2,525–2,612 ft/s | 850 lb APC Mk V | 12 in KC at 4,800 yd (KE VII class) | [NavWeaps](http://www.navweaps.com/Weapons/WNBR_12-40_mk9.php) |
| QF 6-in /40 | 27 lb 12 oz powder, 1,882 ft/s; 13 lb 4 oz cordite, 2,154 ft/s | 100 lb | — | [Wikipedia](https://en.wikipedia.org/wiki/QF_6-inch_/40_naval_gun) |
| Elswick 16.25-in | 2,087 ft/s | 1,800 lb | — | [Wikipedia](https://en.wikipedia.org/wiki/BL_16.25-inch_Mk_I_naval_gun) |

**[INFERRED] Tresidder reproduces the quoted figures.** It gives 28.2 in for the 13.5-in at a striking velocity of about 1,812 ft/s, and 33 in for the 12-in Mk VIII at about 2,200 ft/s. Both are plausible 1,000-yd remaining velocities from MVs of 2,016 and 2,367 ft/s. The 12-in Mk VIII's "33 in iron" and "12 in steel at 3,100 yd" imply a steel or iron ratio of about 2.3–2.5 once range losses are allowed for. That is consistent with the steel in question being Harvey or KC, not mild steel **[INFERRED]**.

### 1.5 How the formulae compare (calculated)

The table below is my calculation **[INFERRED]**: perforation in inches at normal impact.

| Shot | V (ft/s) | Tresidder WI | Gâvre WI | De Marre NS (C = 1) | Krupp KC uncapped | Davis Harvey capped | Implied FoM KC/WI = Tres/KC |
|---|---|---|---|---|---|---|---|
| 6 in, 100 lb | 1,700 | 10.9 | 8.9 | 8.1 | 4.6 | 5.4 | 2.35 |
| 6 in, 100 lb | 2,000 | 13.9 | 11.2 | 10.3 | 5.4 | 6.7 | 2.55 |
| 10 in, 500 lb | 1,700 | 18.8 | 19.5 | 14.9 | 8.0 | 10.8 | 2.35 |
| 12 in, 850 lb | 2,000 | 28.6 | 31.5 | 22.5 | 11.2 | 16.5 | 2.55 |
| 13.5 in, 1,250 lb | 2,000 | 32.7 | 38.1 | 26.2 | 12.8 | 19.5 | 2.55 |

What the calculations show **[INFERRED]**:
- **Tresidder / De Marre-NS ≈ 1.22–1.36** across 6–13.5 in and 1,400–2,300 ft/s. This reproduces the period rule that steel ≈ 1.3 × iron.
- **Tresidder / Krupp-KC = 2.13 at 1,400, 2.35 at 1,700, 2.55 at 2,000 and 2.74 at 2,300 ft/s.** The ratio scales as V^0.5 and does not depend on calibre. So the uncapped-shot figure of merit of KC rises with velocity. That matches EB1911's 2.3–2.7 band.
- **Gâvre vs Tresidder.** Gâvre gives about 20% *less* wrought iron than Tresidder for 6-in shot but about 15% *more* for 13.5-in shot (its D^0.43 scale term). **A figure of merit computed on a Gâvre basis (USN) is therefore lower for small calibres than one computed on a Tresidder basis (RN).** See §2.3.

### 1.6 Method note

I implemented formulas F1–F9 from the Okun constants. I checked them against the EB1911 logarithmic constants for Tresidder (A = 6.934×10⁸; Okun's constant squared gives 6.932×10⁸), Krupp KC (2.2556×10⁶) and De Marre (1,021.9 vs 1,022). All agree to ≤0.1%. I then applied them to the trial data in §3.

---

## 2. Plate-type equivalences

### 2.1 Published ratios (thickness of wrought iron that one inch of plate equals)

| Plate type (introduction) | Ratio to wrought iron | Basis / shot | Source |
|---|---|---|---|
| Wrought iron on teak (1859–c.1880) | 1.0 | — | — |
| Chilled cast iron, Gruson (forts, 1868+) | n/a (forts only) | — | [EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html) |
| Mild steel (Schneider 1876) and compound (Cammell/Wilson 1877) | **"about 1.3 to 1"** (1880–1890) | Steel shot | EB1911 |
| Same | **1.26** (10 in steel or compound = 12.6 in iron) | Energy basis | [USN 1937](https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php) |
| Same | **1.25** ("about 25% more effective") | — | [Naval Gazing Armor 1](https://www.navalgazing.net/Armor-Part-1); [Wikipedia Compound armour](https://en.wikipedia.org/wiki/Compound_armour) |
| First compound plate test, 9 in vs 7-in rifle | Penetration 3⅛ in vs about 8 in in wrought iron → **about 2.5 locally** **[UNCERTAIN: chilled shot; date printed 1887, probably 1877]** | Palliser | [USNI 1895](https://www.usni.org/magazines/proceedings/1895/october/armor-ships-war) |
| Nickel steel (Schneider 1889) | **1.3** (10 in = about 13 in iron); about 5% better than mild steel or compound | — | [USN 1937](https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php); [Naval Gazing](https://www.navalgazing.net/Armor-Part-1) |
| Harvey (1891) | **"about twice"** (above 2 for 6–8 in, slightly lower for thicker plates) | Uncapped | EB1911 |
| Harvey | **1.93** (Carnegie 6 in, 6-in shot up to 2,110 ft/s); **2.14** (Bethlehem 8 in, 2,004 ft/s); **1.83** (English 6 in) | Uncapped, USN basis | [USNI Oct 1894](https://www.usni.org/magazines/proceedings/1894/october/professional-notes) |
| Harvey | **1.80 × iron, 1.35 × "simple steel"** (French estimate); Harveying adds "fully 35 per cent, and perhaps 50 per cent" | Uncapped | same |
| Harvey vs nickel steel | 13 in Harvey = 15.5 in nickel steel (**×1.19**); "15–20%" | — | [Wikipedia Harvey](https://en.wikipedia.org/wiki/Harvey_armor); [USN 1937](https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php); [Naval Gazing](https://www.navalgazing.net/Armor-Part-1) |
| Krupp non-cemented (KNC) | **2.25** on average | Uncapped | EB1911 |
| Krupp cemented (KC, 1893–95) | **2.7** (good 6 in plate), falling to **2.3** (12 in) | Uncapped, shot calibre ≈ plate thickness | EB1911 |
| KC | **≈2.0** vs capped against ≈2.5 vs uncapped; 6 in plate: 2.6 vs 6-in shot but 2.2 vs 9.2-in shot | Capped / uncapped | EB1911 |
| KC vs Harvey | 11.9 in KC = 13 in Harvey (**×1.09–1.15**, "about 15%" above 5 in); 10.2 in = 12 in (**×1.18**); 12 in KC = 15 in Harvey (**×1.25**, BuOrd); "20–30%" from European trials | Mixed | [USN 1937](https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php); [Wikipedia Krupp](https://en.wikipedia.org/wiki/Krupp_armour); [Slover, History of Armor Plate](http://www.eugeneleeslover.com/AMMUNITION/History_of_Armor_Plate.pdf) |
| KC vs Harvey (textbook problem VII) | **1.216** | 10 in, 500 lb, near the limit | [USN 1937](https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php) |
| KC vs best WWII armour | Best WWII armour up to **40% better** than early Krupp | — | [Naval Gazing](https://www.navalgazing.net/Armor-Part-1) |

### 2.2 Game and secondary tables (for comparison, not authority)

The *Battlefleet 1900* rules give thickness modifiers **Krupp 1.25 (≥6 in only), nickel steel 1.00, steel 0.90, compound 0.70, wrought iron 0.50**. They cite Conway's 1860–1905 and Jane's 1905–06 ([WTJ](https://www.wtj.com/games/battlefleet_1900/docu_design_formulas.htm)). Re-based on wrought iron, that is compound 1.4, steel 1.8, nickel steel 2.0, Krupp 2.5. Compared with the period sources:
- The **steel and nickel-steel values are much too high** relative to EB1911 and USN (about 1.3).
- **Krupp at 2.5 matches the uncapped figure.**

A forum post claims Jane's equated the Garibaldi class's 6 in Harvey belt with "12 in of wrought iron", i.e. ×2.0 ([alternatehistory.com](https://www.alternatehistory.com/forum/threads/anglo-german-war-in-1899-1900-who-wins.389876/post-12484664)) **[UNCERTAIN: secondhand]**.

**Jane's penetration tables.** I could **not** retrieve the early *Fighting Ships* gun-versus-armour tables online. The 1898 USNI review describes the book's classification by fighting and defensive power and its armour plans, but no penetration table ([USNI 1898](https://www.usni.org/magazines/proceedings/1898/january/book-notice)). Treat any claimed Jane's ratios as **[UNCERTAIN]** until a scanned edition is checked (archive.org holds 1898–1914 editions).

### 2.3 Where the sources disagree, and why

1. **Reference formula.** RN figures used Tresidder; USN figures used Gâvre for wrought iron. Recomputing the Carnegie 6 in Harvey case (6-in, 100 lb, 2,110 ft/s) gives **2.50 on a Tresidder basis and 2.01 on a Gâvre basis**, against the published **1.93** **[INFERRED]**. US ratios for small calibres therefore look about 20% lower than British ones.
2. **Shot type.** Face-hardened figures of merit fall from about 2.5 to about 2.0 when the shot is capped (EB1911). Against chilled Palliser shot, steel-faced plates scored far higher because the shot shattered. The 1883 Cammell 12 in compound plate stopped a 16-in Palliser shot of about 30,000 ft-tons, calculated as **25 in of wrought-iron perforation**, giving FoM ≈ 2 ([EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html)).
3. **Scale and size.** For the same plate, the figure of merit is lower against a larger shot (6 in KC: 2.6 vs 6-in shot, 2.2 vs 9.2-in shot). Thick face-hardened plates rate lower (KC: 2.7 at 6 in, 2.3 at 12 in) (EB1911).
4. **Velocity.** The Tresidder and Krupp exponents differ (1.5 vs 1.0 on V), so face-hardened figures of merit rise as V^0.5 (§1.5).

---

## 3. Key proof trials (with numbers)

### 3.1 Spezia (Muggiano), September–November 1876

- **Plates.** 22 in Cammell and Marrel wrought iron; 22 in Schneider mild steel (about 0.45% C, oil-quenched). Each plate 11 ft 6 in × 4 ft 7 in. Backing of two thicknesses of timber plus an inner skin of two ¾ in iron plates. Compound-type targets were also tested: 12 in iron over 10 in, and 8 in iron over 14 in chilled cast iron ([Appleton 1880](https://www.eugeneleeslover.com/ENGINEERING/Fullam/ARMOR_Article_Appletons_1880.pdf); [USN 1937](https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php)).
- **Guns.** The Armstrong 100-ton 17.72-in RML (2,000 lb Palliser shot, about 1,446 ft/s with 150 kg of powder; 39 rounds between 20 October and 8 November 1876), plus 10-in and 11-in Woolwich rifles ([Wikipedia RML 17.72](https://en.wikipedia.org/wiki/RML_17.72-inch_gun); [USNI 1895](https://www.usni.org/magazines/proceedings/1895/october/armor-ships-war)).
- **Result.** The iron plates were **"pierced completely"**, with the shot keeping about 180 m/s. The steel plate stopped all shot, including a 100-ton round, though steel tended to crack ([Appleton](https://www.eugeneleeslover.com/ENGINEERING/Fullam/ARMOR_Article_Appletons_1880.pdf); [Wikipedia RML 17.72](https://en.wikipedia.org/wiki/RML_17.72-inch_gun); [Wikipedia Compound](https://en.wikipedia.org/wiki/Compound_armour)).
- **[INFERRED]** Tresidder gives 22.3 in of wrought iron for the 100-ton gun at 1,446 ft/s. That fits the 22 in iron plates being just perforated and matches the "22 inches of iron needed to defeat the heaviest guns" rule ([USN 1937](https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php)).

### 3.2 Later Spezia and Gâvres trials (1880–1884)

- **1880, Gâvres.** Compound judged "superior to all-steel plates" ([Wikipedia](https://en.wikipedia.org/wiki/Compound_armour)).
- **1882 (Lepanto trials).** Two shots from the 100-ton gun wrecked each 18.9 in compound plate. The steel plate survived three ([USNI 1895](https://www.usni.org/magazines/proceedings/1895/october/armor-ships-war)).
- **1884.** A 20 in compound plate was demolished by 2 rounds from the 10-in guns meant for *Lepanto*. The same projectiles shattered on 20 in of Creusot steel ([Wikipedia](https://en.wikipedia.org/wiki/Compound_armour)).
- **1884, Copenhagen.** "Little difference" between compound and steel ([Wikipedia](https://en.wikipedia.org/wiki/Compound_armour)).

Trials at Amager (1879) and Muggiano (November 1882, October–November 1884) are listed in Dredge, *The Works of Schneider & Co.* (1900) ([tekniskkulturarv.dk scan](https://tekniskkulturarv.dk/book/2a25bfc0-ca8c-42e4-9313-1506afd41523?page=168)) **[the detailed tables could not be read: OCR]**.

### 3.3 Portsmouth / Shoeburyness (1860s–1877)

- **Shoeburyness, 1864.** A 68-pdr and an Armstrong 110-pdr were fired at a wool-packed boiler from 100 yd ([Appleton](https://www.eugeneleeslover.com/ENGINEERING/Fullam/ARMOR_Article_Appletons_1880.pdf)).
- **Portsmouth, 1877.** Compound and steel candidates were tested with a **250 lb Palliser shot from a 9-in 12-ton MLR at 30 ft**:
  - Cammell solid steel split but held.
  - 4 in steel (0.64% C) on 5 in iron was easily penetrated.
  - An iron/steel/iron sandwich failed (Appleton).
- **1878, Shoeburyness.** Lt.-Col. T. English proposed wrought-iron caps. Shoeburyness tests "did not show that any advantage was gained", probably because velocities were low ([EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html)).

### 3.4 Annapolis, 18 September 1890 (all-steel vs nickel-steel vs compound)

Source for the whole subsection: [USNI Proceedings, Dec 1890, Board report](https://www.usni.org/magazines/proceedings/1890/december/report-board-competitive-trial-armor-plates).

| Plate | Maker | Thickness (mean caliper) | Weight |
|---|---|---|---|
| All-steel | Schneider | 10.57 in | 20,776 lb |
| Nickel steel | Schneider | 10.42 in | 20,670 lb |
| Compound | Cammell | 10.72 in | 20,092 lb |

Guns and shot:
- **6-in BL Mk III/35**: 44.5 lb brown prismatic powder, **2,075 ft/s striking**, 100 lb **Holtzer** AP (sand-weighted), 2,988 ft-tons.
- **8-in BL**: 85 lb powder, **1,850 ft/s**, 210 lb **Firth** AP, 4,988 ft-tons.

Results:
- **Compound**: perforated by every round. Two shells went fully through plate and backing, and the steel face was destroyed.
- **Steel**: stopped every shot. 6-in shells penetrated about 12–12.5 in, i.e. into the backing. The 8-in shot penetrated 15 in, broke in three, and left 4 through-cracks.
- **Nickel steel**: stopped every shot and was uncracked by the 8-in.
- **Ranking**: nickel steel, then steel, then compound.

**[INFERRED]** On a Tresidder basis these shots carried 14.7 in (6-in) and 15.5 in (8-in) of wrought-iron perforation. The ~10.5 in steel plates therefore showed FoM ≳ 1.4–1.5, while the compound plate fell below about 1.4.

### 3.5 Indian Head and the US Harvey/nickel acceptance era (1891–1898)

- **1890.** The first Harveyized plate (a Creusot 10.5 in) was made at the Washington Navy Yard ([USN 1937](https://www.eugeneleeslover.com/ARMOR-CHAPTER-XII-A.php)).
- **1891.** Three Bethlehem and three Carnegie plates were tried, five nickel steel and three Harveyed ([USNI 1895](https://www.usni.org/magazines/proceedings/1895/october/armor-ships-war)). By 1895 Harveyed plates of 6, 14 and 17 in were reported "invulnerable" to 6-, 10- and 12-in guns respectively (same).
- **February 1893, 14 in Harvey nickel-steel plate** (Indiana type, 16 tons, oak backing), 10-in BL, 500 lb Holtzer AP at 385 ft ([USNI Jan 1893](https://www.usni.org/magazines/proceedings/1893/january/professional-notes)):

| Rd | Velocity (ft/s) | Penetration | Plate |
|---|---|---|---|
| 1 | 1,472 | about 2 in | uncracked |
| 2 | 1,859 | 5 in | through-crack |
| 3 | 1,959 | about 6 in | in 3 pieces |
| 4 | 2,059 | 10–11 in | in 7 pieces, **not perforated** |

  **[INFERRED]** Round 4 carried 25.1 in of wrought-iron perforation (Tresidder), so this plate's FoM > 1.8 against uncapped Holtzer shot.
- **July 1893, Indian Head.** A Bethlehem 17 in nickel-steel plate was fired on by a 12-in with Carpenter forged-steel shot, which rebounded apparently uninjured at 1,838 ft/s ([USNI Oct 1893](https://www.usni.org/magazines/proceedings/1893/october/professional-notes)).
- **Johnson cast-steel shot trials, 1892–94** ([USNI Jan 1894](https://www.usni.org/magazines/proceedings/1894/january/johnson-cast-steel-armor-piercing-shot)). All 10-in, 500 lb shot unless noted.

| Date | Plate | Shot, velocity | Result | FoM_T implied **[INFERRED]** |
|---|---|---|---|---|
| 11 Aug 1892 | 13 in nickel (*Monterey*) | 10-in, 1,700 | Penetrated about 7 in; plate split | >1.45 |
| 18 Mar 1893 | 10.5 in nickel (*New York*) | 1,500 | **Perforated** plate and backing | <1.49 |
| same | same | 1,300 | Penetrated 6 in; rebounded | — |
| 5 Sep 1893 | 17 in curved nickel (*Indiana*) | 1,890 | Penetrated 19 in | ≈1.3 |
| 9–10 Jan 1894 | 10 in nickel (*Maine*) | 1,570 / 1,520 | **Perforated** | <1.6 |
| same | same | 1,490 | Lodged in bulge | ≈1.54 (limit) |
| 21–23 Mar 1894 | 12 in Harvey nickel (Carnegie) | 1,600, uncapped | Penetrated 4.5–6.5 in; shot broke up | >1.43 |
| 23 Mar 1894 | 12 in Harvey | 1,600, **soft-steel cap** (5 in dia × 3.5 in, 510 lb total) | Got to **within 3 in of the back** (≈9 in); plate split in 8; point undeformed | — |

  The 23 March 1894 capped round is the **first documented US capped-shot success**. It added roughly 40–100% depth against Harvey at only 1,600 ft/s **[INFERRED]**. That contrasts with EB1911's claim that caps fail below about 1,700 ft/s.
- **Schneider 15.9 in nickel plate for *Tri Sviatitelia*, Creusot 1893.** 9.4-in gun, 317 lb **Holtzer chrome-steel** shot at 2,001, 1,948, 1,923 and 1,962 ft/s. Point penetration 14.1, 10.9, 14.0 and 9.9 in. Shot smashed or broke up. Plate showed only fine cracks ([USNI Oct 1893](https://www.usni.org/magazines/proceedings/1893/october/professional-notes)). FoM_T ≥ 1.24 **[INFERRED]**. Dredge records a 406 mm *cemented* plate tested against a 24 cm gun in August 1893 for the same ship ([Dredge scan p.175](https://tekniskkulturarv.dk/book/2a25bfc0-ca8c-42e4-9313-1506afd41523?page=175)) **[UNCERTAIN: OCR]**.
- **Harvey, 1893–94 figures of merit.** Carnegie 6 in vs 6-in shot up to 2,110 ft/s: **1.93**. Bethlehem 8 in vs 8-in shot at 2,004 ft/s: **2.14**. English 6 in: **1.83** ([USNI Oct 1894](https://www.usni.org/magazines/proceedings/1894/october/professional-notes)).
- **Krupp process in the USA, 1898** ([Slover, *History of Armor Plate*](http://www.eugeneleeslover.com/AMMUNITION/History_of_Armor_Plate.pdf); OCR-garbled, **[UNCERTAIN]**):
  - A Carnegie Krupp plate withstood fire about **25% more severe** than the best Harvey plate of equal thickness. It resisted a 6-in at about 2,360 ft/s.
  - A 6 in Bethlehem Krupp plate took six 8-in rounds at 1,621–1,823 ft/s without cracking.
  - A 9 in Harvey plate hit by an 8-in Wheeler-Sterling AP at 1,734 ft/s was penetrated 4.6 in.
  - BuOrd: 12 in Krupp ≈ 15 in Harvey. Krupp price $545/ton.

### 3.6 Krupp, Meppen, and British acceptance tests of KC (1895–1905)

Source for this subsection: [EB1911 "Armour Plates"](https://gutenberg.org/cache/epub/34162/pg34162-images.html).

| Year | Plate | Attack | Result / FoM | FoM_T recomputed **[INFERRED]** |
|---|---|---|---|---|
| 1895 | Krupp 11.8 in cemented | Three 12-in shot of 712.7–716.1 lb; round 3 at 1,993 ft/s (= **25.9 in wrought iron**) | FoM > 2.2 | Tresidder gives 26.1 in (exact match) |
| c.1900 | Armstrong Whitworth 9 in KC | Round 4 could perforate 22 in of wrought iron | FoM > 2.45 | — |
| 11 Oct 1901 | Beardmore 6 in KC, Eskmeals | 100 lb shot at 1,996–2,177; limit passed at **2,261** | FoM **2.6–2.8** | 2.78 |
| 1901 | same | **Johnson capped** 105.9 lb at **1,945**: perforated | FoM < 2.2 | 2.28 |
| Nov 1905 | Vickers 3 in KNC | 12.5 lb 3-in shot, limit 2,558; 4.7-in, 45 lb beaten at 1,630 | FoM about 2.6 | 3.3 **[UNCERTAIN: formula outside its range]** |
| — | Krupp 250 mm (9.8 in) | **150 mm capped** shot: perforated at 2,302 and 2,281; point through at 2,244; lodged at 2,232 | Capped limit about 2,240 ft/s | 1.79 |
| — | Harvey 4.5 in shield | Resisted 5-in shot to nearly 1,800; beaten by 6-in **capped** at 1,842 | — | — |
| — | Hadfield shield, 5.8 in | Limit by uncapped 4.7-in AP at 2,128 | — | — |
| 1902 | Krupp cast steel, 5.9 in | Uncapped 5.9-in, 112 lb | FoM > 2.2 | — |

- **Composition (EB1911).** Ni 3–4%, Cr 1.0–2.0%, C 0.25–0.35%, Mn 0.3–0.7%. Cap 3–5% of shot weight. An uncapped punch takes out a plug up to about 4× calibre in diameter.
- **Common shell** punches about **2/3 as well as uncapped AP**.
- **Meppen.** Gun-acceptance data exists (e.g. the 40 cm for Italy, 1885: 920 kg at 550 m/s; [Wikipedia](https://en.wikipedia.org/wiki/40_cm_MRK_L/35)). No 1890s Meppen plate-trial tables were retrievable **[UNCERTAIN]**.

### 3.7 Russian trials: Ochta and the 1894 cap

- **EB1911:** "experiments carried out in Russia with so-called 'magnetic' shot against plates of Harveyed steel" (1894) showed that "the perforating power of an armour-piercing projectile was considerably augmented where hard-faced plates were concerned" ([EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html)).
- **Naval Gazing:** the soft wrought-iron cap idea (from 1878) was "ignored until the Russians tried it in 1894" ([Naval Gazing, Shells 2](https://www.navalgazing.net/Shells-Part-2)).
- **Attribution and adoption.** Makarov is credited with the cap while Chief Inspector of Naval Ordnance, 1890–94 ([Wikipedia](https://en.wikipedia.org/wiki/Stepan_Makarov)). Russian APC was "introduced in 1894" but "so expensive that only a few were available" in 1904–05. The fleet's "old model" 12-in AP lacked a cap ([NavWeaps Russian 12"/40](https://navweaps.com/Weapons/WNRussian_12-40_m1895.php)).
- **Ochta trial tables:** not found online **[UNCERTAIN]**.

---

## 4. Gun and shell characteristics, 1860–1910

### 4.1 Velocity bands

| Era | Typical MV | Examples |
|---|---|---|
| 1860s–70s RML, black powder | 1,300–1,600 ft/s | 12.5-in 38-ton: 1,425 / 1,575 ([Wikipedia](https://en.wikipedia.org/wiki/RML_12.5-inch_38-ton_gun)). 100-ton: 1,446–1,585 ([Wikipedia](https://en.wikipedia.org/wiki/RML_17.72-inch_gun)) |
| 1880s BL, prismatic/brown (SBC) powder | 1,850–2,100 | 13.5-in: 2,016 ([NavWeaps](http://www.navweaps.com/Weapons/WNBR_135-30_mk1.php)). 6-in BL: 1,882 ([Wikipedia](https://en.wikipedia.org/wiki/QF_6-inch_/40_naval_gun)). US 6-in at 2,075 striking (Annapolis 1890) |
| 1890s cordite / smokeless | 2,100–2,400 | 13.5-in on cordite: 2,099. 6-in QF: 2,154. 12-in Mk VIII: 2,367. Japanese 12-in/40: 2,400 ([NavWeaps](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php)). US 12-in/35: 2,100 ([Wikipedia](https://en.wikipedia.org/wiki/12-inch/35-caliber_gun)) |
| 1900s pre-dreadnought / early dreadnought | 2,500–2,850 | 12-in Mk IX: 2,525–2,612 ([NavWeaps](http://www.navweaps.com/Weapons/WNBR_12-40_mk9.php)). 12-in Mk X: 2,700–2,850 ([NavWeaps](https://navweaps.com/Weapons/WNBR_12-45_mk10.htm)). Russian 12-in/40: 2,598 ([NavWeaps](https://navweaps.com/Weapons/WNRussian_12-40_m1895.php)) |

Cordite cut the 13.5-in charge from 630 lb to 187 lb for a gain of only about 4% in MV. The large velocity gains came with the longer guns of the late 1890s and 1900s built for cordite **[INFERRED]**.

### 4.2 Shell types and their effective quality

- **Palliser chilled cast-iron shot and shell** (1867 onward; the shell with a small burster dated to 1877): cast point-down so the head chills. "Very effective against wrought iron" but not against steel or compound ([Wikipedia AP shell](https://en.wikipedia.org/wiki/Armor-piercing_shot_and_shell); [Naval Gazing](https://www.navalgazing.net/Shells-Part-2)). On steel faces it shatters, as at Spezia 1876 and with the 1883 Cammell plate.
- **Forged steel AP (1880s–90s).** Holtzer (French chrome steel, forged-chrome shot from 1886: [Wikipedia Compound](https://en.wikipedia.org/wiki/Compound_armour)), Firth, Carpenter, Wheeler-Sterling. Cast steel was "not particularly suitable" ([Naval Gazing](https://www.navalgazing.net/Shells-Part-2)). Johnson's oil-tempered cast-steel shot did comparably in 1892–94 ([USNI 1894](https://www.usni.org/magazines/proceedings/1894/january/johnson-cast-steel-armor-piercing-shot)). Against Harvey or KC, "even a forged-steel shell would usually shatter" ([Naval Gazing](https://www.navalgazing.net/Shells-Part-2)).
- **Common shell** (black powder; later lyddite, shimose or guncotton). Punches about 2/3 as well as AP (EB1911). Japanese AP carried 42.5 lb of shimose; Japanese common shell about 85 lb of black powder ([NavWeaps](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php)). Russian AP held about 11.7–12.2 lb of wet guncotton ([NavWeaps](https://navweaps.com/Weapons/WNRussian_12-40_m1895.php)).
- **Capped AP (APC) and capped common (CPC).**
  - Effect: about **+15% "so long as obliquity was below about 15°"** ([Naval Gazing](https://www.navalgazing.net/Shells-Part-2)). About **+10–20% at low and +30–50% at high velocities** against KC (Raven & Roberts via [NavWeaps 12"/45](https://navweaps.com/Weapons/WNBR_12-45_mk10.htm)).
  - Limits: the cap "fails to act" near 1,700 ft/s or below, and fails beyond about 30° ([EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html)).
  - CPC penetrates about **1/2–1/3 calibre** ([Naval Gazing, Shells at Jutland](https://navalgazing.net/Shells-at-Jutland)).
  - Lyddite tended to detonate during penetration ([Naval Gazing](https://www.navalgazing.net/Shells-Part-2)).

### 4.3 When caps arrived (by navy)

| Navy | Evidence | Source |
|---|---|---|
| Russia | Trialled 1894; few in service in 1904–05; mod 1907 AP was capped | [NavWeaps](https://navweaps.com/Weapons/WNRussian_12-40_m1895.php); [EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html) |
| USA | Johnson cap trial Mar 1894; Davis capped-Harvey formula c.1900; by 1914 only APC carried | [USNI 1894](https://www.usni.org/magazines/proceedings/1894/january/johnson-cast-steel-armor-piercing-shot); [Okun](http://www.navweaps.com/index_nathan/Hstfrmla.php); [Naval Gazing](https://www.navalgazing.net/Shells-Part-2) |
| Britain | Johnson-capped shot used in a 1901 acceptance test; 12-in Mk IX carried APC Mk V (in service 1901, date of the cap uncertain); 4crh caps from 1909; tested at normal impact only | [EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html); [NavWeaps](http://www.navweaps.com/Weapons/WNBR_12-40_mk9.php); [Naval Gazing](https://navalgazing.net/Shells-at-Jutland) **[UNCERTAIN: exact RN adoption year]** |
| Japan | After lyddite AP failed at the Yellow Sea, relied on powder-filled common shell | [Naval Gazing](https://www.navalgazing.net/Shells-Part-2); [NavWeaps](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php) |
| Germany | Krupp brazed caps that resisted being knocked off obliquely; early adopter of TNT | [Naval Gazing](https://www.navalgazing.net/Shells-Part-2) |

---

## 5. Combat evidence

| Battle | Armour hit | Evidence | Source |
|---|---|---|---|
| **Lissa 1866** | Italian *Re d'Italia* and *Re di Portogallo*: 4.5 in iron (7 in per Wikipedia's figure, **[UNCERTAIN]**); Austrian ironclads 4 in | Decided by ramming. No armour-penetration statistics. Austrian guns were obsolete muzzle-loaders. A 300-pdr Armstrong hit on *Kaiser* (wooden) killed or wounded 20 | [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_Lissa_(1866)) |
| **Yalu 1894** | *Dingyuan* / *Zhenyuan*: 14 in compound belt, 12–14 in barbettes | "No other shells had penetrated their armor belts deeper than 4 inches." Citadel "impervious". Damage came from QF fire and fires. Japanese Canet 12.6-in failed to score | [Wikipedia Yalu](https://en.wikipedia.org/wiki/Battle_of_the_Yalu_River_(1894)); [Wikipedia Dingyuan](https://en.wikipedia.org/wiki/Chinese_ironclad_Dingyuan) |
| **Santiago 1898** | Spanish cruisers: 10–12 in steel/compound belt | *Teresa*: 2×12-in, 3×8-in, 5×5-in, 1×4-in and about 20 secondary hits. *Oquendo*: 3×8-in, 5×5-in, 5×4-in and about 42 small. No side armour pierced. Shells ≥15 cm often passed through unburst. Fire did most of the damage | [USNI 1899](https://www.usni.org/magazines/proceedings/1899/april/sketches-spanish-american-war); [spanamwar.com](https://www.spanamwar.com/Oquendo.htm) |
| **Yellow Sea 1904** | Russian 9–14.5 in Harvey/Krupp; Japanese 9 in Krupp | Japanese 12-in: of 279 AP fired, at least 10 hit armour (mostly turrets) and **none penetrated**. 30 hits from 603 rounds (4.7%). *Mikasa* hit 20 times. *Tsesarevich*: 13×12-in and 2×8-in hits. Overall hit rate 1.7% | [NavWeaps](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php); [Wikipedia](https://en.wikipedia.org/wiki/Battle_of_the_Yellow_Sea) |
| **Tsushima 1905** | *Oryol*: 145–194 mm Krupp belt | *Oryol*: 5×12-in, 2×10-in, 9×8-in, 39×6-in and 21 smaller hits. **4 hits on side armour (1×12-in, 3×6-in), none penetrated.** Japanese 12-in never penetrated armour over about 6 in in the whole war. About 40 hits from 446 12-in rounds (9%) at ≤6,500 yd. *Mikasa* hit by more than 40 (10×12-in, 22×6-in) | [Wikipedia Oryol](https://en.wikipedia.org/wiki/Russian_battleship_Oryol); [NavWeaps](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php); [Wikipedia Mikasa](https://en.wikipedia.org/wiki/Japanese_battleship_Mikasa) |

On Tsushima, *Scientific American* (1905) also reported:
- Japanese 12-in hits on unarmoured sides opened holes about 7 × 6 ft.
- Three 12-in hits loosened and tore off a waterline plate on *Oslyabya*.
- The Japanese fired 1,275 12-in rounds with a claimed 19.6% hit rate ([Scientific American](https://www.scientificamerican.com/article/with-the-baltic-fleet-at-tsushima)) **[UNCERTAIN: wartime figure, conflicts with NavWeaps]**.

**Interpretation [INFERRED].** In 1894–1905, belts of 6 in or more of compound, Harvey or KC were almost never perforated in battle. The reasons:
1. The shells that hit were mostly common or HE, with sensitive fuzes (shimose, Ijuin).
2. AP was uncapped and shattered.
3. Ranges of 3,000–7,000 yd cut striking velocities to about 1,600–2,000 ft/s.

Ships were lost to fires, to hits on unarmoured ends and upper works, to flooding, and to ramming (Lissa), not to belt perforation. *Oslyabya* is the exception: plates loosened by repeated hits near the waterline. The same pattern persisted to Jutland, where only 1 of 17 British heavy shells striking armour over 9 in penetrated and burst inside ([Naval Gazing](https://navalgazing.net/Shells-at-Jutland)).

---

## 6. Early dreadnought era (1906–1914), before combat

- **KC everywhere.** By about 1905, KC-type plate "had made all other forms of full-strength armor steel obsolete" ([Okun, metallurgical table](https://www.navweaps.com/index_nathan/metalprpsept2009.php)). Original KC a/A had a face of about 500 Brinell and a back of about 225 BHN. Pre-WWI US Class A varied: Midvale used a deep chill (over 80%). Bethlehem's was "very brittle… barely above the minimum acceptance level" (same). Britain stopped using non-cemented plate for 4 in and under in 1902 ([EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html)).
- **RN, HMS *Edinburgh* (1908–1910).** Converted to a target in 1908 with "fully backed and supported modern armour plates". Tested lyddite AP at oblique impact. Found "major shortcomings". Jellicoe, as Controller, ordered a better shell, but the order lapsed when he went to sea ([Wikipedia](https://en.wikipedia.org/wiki/HMS_Edinburgh_(1882))). No plate-by-plate data was retrievable **[UNCERTAIN]**.
  - **DNO memorandum, 24 October 1910:** 12-in APC striking at more than **20°** was unlikely to penetrate even **4 in KC**, and shells would likely break up at **30° against 6 in KC** (Roberts, *Battlecruisers*, via [NavWeaps 12"/45](https://navweaps.com/Weapons/WNBR_12-45_mk10.htm)).
  - **DNO 1912 penetration of KC in calibres, 2crh AP:** 4/3 at ≤6,000 yd, 3/4 at 6–8 kyd, 7/12 at 8–10 kyd, 5/12 at 10–12 kyd. **4crh AP:** 1, 7/8, 3/4, 2/3. **4crh CPC:** 4/5, 3/4, 2/3, 1/3 (Friedman via [NavWeaps](https://navweaps.com/Weapons/WNBR_12-45_mk10.htm)).
  - RN AP was proofed only at normal impact ([Naval Gazing](https://navalgazing.net/Shells-at-Jutland)).
  - **[INFERRED]** "4/3 calibre at ≤6,000 yd" for 2crh AP implies an uncapped-style KC rating near the EB1911 rules (t/d = v/2200 to v/1900).
- **USN, *San Marcos* (ex-*Texas*), March 1911.** *New Hampshire* fired 12-in over three days. The target sank in shallow water on 22 March 1911. The findings fed directly into the all-or-nothing scheme ("based on the data furnished by the firing at the San Marcos"). The 1912 cage-mast test used *Tallahassee*'s 12-in at 1,000 yd, with 9 hits ([USNI *Naval History* 2018](https://www.usni.org/magazines/naval-history-magazine/2018/april/historic-ships-old-hoodoo-target-ship); [Wikipedia](https://en.wikipedia.org/wiki/USS_Texas_(1892))). The target's belt was 12 in Harvey (Wikipedia; **[UNCERTAIN: may be nickel steel]**). No penetration tables retrieved.
- **Germany.** No pre-1914 plate-trial tables retrieved. The ex-*Baden* (re-armoured with Krupp nickel steel in 1895–97) was briefly a target from early 1911 ([Wikipedia](https://en.wikipedia.org/wiki/SMS_Baden_(1880))) **[UNCERTAIN: no results published there]**. German practice: capped AP with brazed caps, TNT bursters, base fuzes with delay from about 1911 ([Naval Gazing](https://www.navalgazing.net/Shells-Part-2); [Okun Misc. armour](https://navweaps.com/index_nathan/Miscarmr.php)).
- **Russia.** The mod 1907 and 1911 AP were capped. The 470.9 kg mod 1911 shell at 2,297 ft/s was rated for 12.25 in KC at 2,000 yd and 7.91 in at 6,000 yd ([NavWeaps](https://navweaps.com/Weapons/WNRussian_12-40_m1895.php)).

---

## 7. Recommendation: era-banded parameter tables

### 7.1 Modelling approach

The cleanest way to use period data is a two-step model **[INFERRED]**:

1. **Compute the shot's "wrought-iron perforation" P_WI.** Use Tresidder: P_WI = √(W·V³/(D·6.934×10⁸)) (inches, lb, ft/s). Multiply by a **shell factor S** for shot type and condition.
2. **Convert the plate to wrought-iron equivalent:** T_WI = T × **Q(armour, shot class)**, with the obliquity correction T·sec θ (up to 30°; beyond that apply the cap and shatter rules below). Perforation if P_WI·S ≥ T_WI.

This mirrors exactly how the period rated plates, and it reproduces EB1911's numbers: for the 11.8 in KC trial, Tresidder gives 26.1 in against the quoted 25.9 in.

**Why Q depends on the shot class.** Face-hardened Q is inherently a function of shot type: 2.5 against uncapped and 2.0 against capped steel for KC. Q for face-hardened plate also rises as about (V/2000)^0.5 against uncapped shot, because Tresidder and Krupp scale differently with V. For the game, a fixed Q per band evaluated near 1,900 ft/s is adequate. Optionally use Q_FH(V) = Q₂₀₀₀ × (V/2000)^0.5 for uncapped shot only.

### 7.2 Armour quality factor Q (thickness multiplier to wrought-iron-equivalent)

| Armour (service years) | Q vs chilled Palliser | Q vs uncapped forged steel AP | Q vs early capped AP (≥1,700 ft/s, ≤20–30°) | Rel. to WWII Class A / KC n/A (vs capped) **[INFERRED]** | Basis |
|---|---|---|---|---|---|
| Wrought iron on teak (1859–1880) | 1.0 | 1.0 | 1.0 | ≈0.36 | Definition |
| Laminated / sandwich iron | 0.8 | 0.8 | 0.8 | ≈0.29 | Appleton (6 in laminated ≈ 4 in solid → ≈0.67–0.8) **[UNCERTAIN]** |
| Mild steel, Schneider (1876–1890) | 1.6–2.0 (shot shatters) | 1.3 | 1.3 | ≈0.46 | EB1911; USN 1937; Spezia, Lepanto |
| Compound (1877–1891) | 1.6–2.0 (until face shattered) | 1.25 | 1.25 | ≈0.45 | EB1911; Annapolis 1890 (worst of three); 1883 Cammell vs Palliser ≈2.0 |
| Nickel steel, homogeneous (1889–1896) | ≥2.0 | 1.35–1.5 (1.3 conservative) | 1.35 | ≈0.50 | USN 1937 (1.3); Annapolis and Johnson trials (1.45–1.55, Tresidder basis) |
| Harvey nickel steel (1891–1898) | ≥2.5 | **1.9–2.1** (thin), 1.8 (thick, >10 in) | **1.45** | ≈0.53 | EB1911 (≈2.0); USNI 1894 (1.83–2.14); French 1.80; Johnson-cap trial and USN 1937 problem VII (≈1.35–1.4 vs capped) |
| Krupp non-cemented, thin (≤4 in, 1895–1914) | — | 2.25 | 1.9 **[UNCERTAIN]** | ≈0.70 | EB1911 |
| Early KC (1894–1905) | — | **2.7 at ≤6 in → 2.3 at 12 in** | **2.0** | ≈0.75 | EB1911; Beardmore 1901 (2.6–2.8 vs 2.2 capped) |
| Mature pre-war KC / Class A (1905–1914) | — | 2.5 (6–12 in average) | **2.1–2.2** | ≈0.80 | [INFERRED] ≈+5–10% over early KC from refinements; Naval Gazing's "up to 40%" gain by WWII spans 1895–1940 |
| WWII Class A / KC n/A (reference) | — | (≈3.0) | ≈2.7–2.8 | 1.00 | [INFERRED]: early KC × 1.3–1.4 (Naval Gazing) |

The "relative to WWII Class A" column assumes WWII Class A ≈ 2.75 × wrought iron against capped AP. That is EB1911's 2.0 × 1.35–1.4 **[INFERRED]**. The WWI/WWII armour notes should calibrate it against their Class A baseline. A **thin-plate penalty** applies to face-hardened plate under about 3 in, which is "easier to penetrate" ([Naval Gazing Armor 2](https://www.navalgazing.net/Armor-Part-2)). Use homogeneous Q there.

### 7.3 Shell factor S (multiplier on Tresidder P_WI)

| Shell (years) | S vs wrought iron / homogeneous | S vs face-hardened (Harvey/KC) | Notes / source |
|---|---|---|---|
| Round shot, smoothbore (1860s) | 0.8 **[INFERRED]** | — | Blunt; Appleton "foot-tons per inch" era |
| Palliser chilled iron (1867–c.1888) | 1.0 | Use Q column 1 (shatters) | [Wikipedia](https://en.wikipedia.org/wiki/Armor-piercing_shot_and_shell) |
| Forged steel uncapped AP (Holtzer, Firth, Carpenter; 1886–c.1905) | 1.0 | 1.0 (use the uncapped Q column). **Shatter check: if V > 2,000 ft/s and T ≥ 0.6·D, 50% chance of break-up (halve S)** [INFERRED from Annapolis, Indian Head 1893, *Tri Sviatitelia*] | |
| Early capped AP (Russia 1894+, USN c.1898+, RN c.1903+) | 1.0 | Use the capped Q column; caps ineffective below 1,700 ft/s or above 20–30° (then revert to uncapped Q and the shatter check) | [EB1911](https://gutenberg.org/cache/epub/34162/pg34162-images.html); [NavWeaps](https://navweaps.com/Weapons/WNBR_12-45_mk10.htm) |
| Common shell, black powder (1870s–1914) | 0.67 | 0.5 | EB1911 ("about 2/3") |
| CPC capped common (1900s–1914) | 0.6 | 0.4–0.5 | Naval Gazing ("1/2–1/3 calibre") |
| HE / lyddite / shimose, nose-fuzed | 0.3 (bursts on the face) | 0.25 | [INFERRED] from the Japanese experience ([NavWeaps](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php)) |

### 7.4 Year-indexed lookup (simple version)

| Year band | Best armour in service | Q(best) vs the era's standard AP | Standard AP | Typical heavy-gun MV |
|---|---|---|---|---|
| 1860–1876 | Wrought iron | 1.0 | Palliser | 1,300–1,600 |
| 1877–1889 | Compound / steel | 1.25–1.3 (≈1.8 vs Palliser) | Palliser → forged steel (1886+) | 1,600–2,050 |
| 1889–1892 | Nickel steel | 1.4 | Forged steel | 1,900–2,100 |
| 1892–1897 | Harvey | 2.0 | Forged steel, uncapped | 2,000–2,200 |
| 1897–1905 | Early KC | 2.5 (uncapped) / 2.0 (capped) | Mostly uncapped (RJW navies); USN capped | 2,100–2,600 |
| 1905–1914 | KC / Class A | 2.5 / 2.1–2.2 | Capped (2crh → 4crh from 1909) | 2,500–2,850 |

---

## What a model needs from this

1. **One penetration engine plus armour and shell multipliers** is historically faithful. Tresidder (P_WI = √(W V³ / (D·6.934e8))) is the period British standard and reproduces gun-table and trial figures to within about 5%. Rate plates as Q × thickness in wrought-iron inches.
2. **Q must depend on shot class** (Palliser, uncapped steel, capped steel), and ideally on velocity for face-hardened plate against uncapped shot (Q ∝ V^0.5, from Tresidder/Krupp). Use the §7.2 values: WI 1.0, compound or steel 1.25–1.3, nickel steel about 1.4, Harvey about 2.0 uncapped and about 1.45 capped, KC 2.3–2.7 uncapped and about 2.0 capped.
3. **Shatter and cap rules carry the face-hardened era.**
   - Uncapped steel against Harvey or KC breaks up often when the plate is ≥ about 0.6 calibre and V is about 2,000 ft/s or more.
   - Caps help only at ≥ about 1,700 ft/s and ≤ about 20–30° obliquity.
   - Common, CPC and HE shells get S = 0.3–0.67.
4. **Obliquity.** Use T·sec θ up to 30° (period practice). Beyond 20–30°, caps fail and shells break up (DNO 1910).
5. **Calibration targets** for unit tests:
   - Annapolis 1890: compound perforated, steel and nickel stopped by 6-in/100 lb at 2,075 ft/s.
   - Indian Head 1893: 14 in Harvey not perforated by 10-in/500 lb uncapped at 2,059.
   - 23 March 1894: capped 10-in reaches about 9 in into 12 in Harvey at 1,600, vs uncapped about 5 in.
   - 1901 Beardmore: 6 in KC limit 2,261 ft/s against 100 lb uncapped, perforated at 1,945 by capped.
   - Spezia 1876: 22 in iron perforated by the 100-ton gun at about 1,450 ft/s.
6. **Combat sanity check.** In 1894–1905 battles, belts of 6 in or more were essentially never perforated by the shells actually fired (Yalu, Santiago, Yellow Sea, Tsushima). The model should yield very low perforation rates for 3–7 kyd fights with uncapped AP or common shell, and damage should come from unarmoured areas and fire.
7. **Basis caveat.** RN (Tresidder-basis) and USN (Gâvre/De Marre-basis) published ratios differ by about 20% at small calibres. Do not mix published figures of merit without rebasing.
8. **Open gaps.** Jane's early penetration tables, Ochta trial data, the HMS *Edinburgh* plate results, German pre-war tests, and the exact RN cap-adoption year. Check scanned Brassey's, Jane's and *Text Book of Gunnery* volumes on archive.org before relying on them.
