# 12 — Explosive fillers: the shell's filler as a parameter, and whether it is a player choice

*Research for the damage model, after `11_blast_confinement.md`. It answers `shells-armour-damage.md` open question 2,
**explosives as parameters**: a catalogue of the fillers with TNT equivalence, brisance, incendiary effect, sensitivity
(bursting in the plate), low-order and failure rates with their booster, and the years they served. It also asks the
design question behind it: is choosing the filler a reasonable player decision, given availability?*

*Tags as in the other damage docs: **[SRC]** sourced, **[FIT]** fitted here to the cases, **[EST]** judgement,
**[UNCERTAIN]** thin or conflicting sources.*

**Companion code:** `filler_ref.py` (pure Python). Every table in §4 comes out of it.

---

## 0. Short answer

1. **What decides a hit is whether and where the filler goes off, not how strong it is.**
   - The fillers in service differ by 10–20% in blast. Burst radius goes with the cube root of the charge, so that is
     ±3–6% of radius.
   - Bursting in the plate instead of behind it, or going low-order instead of full detonation, loses 70–85% of the
     effect.
   - The RN's post-Jutland record puts it as German fillings detonating while British ones [merely
     exploded](https://apps.dtic.mil/sti/pdfs/ADA492806.pdf) [SRC].
2. **The filler and its booster are one package.** Insensitive fillers (TNT, TNT/wax, Explosive D, Shellite, TNA) need a
   strong booster. With only black powder they often go low-order (Shellite before its CE booster, late 1920s
   [SRC]; German TNT with a weak picric booster [SRC]). Picric fillers go off reliably from black powder.
3. **Yes, it is a player choice, mostly in the early eras and under shortage:**
   - **Before a CE booster,** picric (Lyddite, Shimose) beats TNT on every target in the model, armour included.
   - **After it,** insensitive fillers win against armour, picric still wins against soft targets by ~20%, and black
     powder alone sets fires.
   - **Under a TNT shortage,** amatol keeps ~95% of TNT's effect at a fraction of the TNT.
   - **Late in the game** the choice collapses to "insensitive for AP, strongest for HE". That is historically right.
4. **Correction to the first draft of this note** (the Docs page of 2026-10-16). With armor/04's sourced in-plate rate
   for lyddite (0.35, not the draft's 0.9 estimate), the filler alone costs lyddite APC only ~5–10% against heavy
   armour. Jutland's 1 in 17 came mostly from the shells' bodies, caps and fuzes, which the shell block already rolls
   (armor/04 §2.2). The filler matters, but it was not most of that failure.
5. **Choose at procurement and refit, per shell kind,** not per salvo. Ships carry what the factory filled. The
   tactical lever is the outfit (AP/HE shares), already in S2.

---

## 1. What the filler changes in a hit

Six properties. Only the first is "how big a bang".

| Property | What it does | Where it lands in the model |
|---|---|---|
| Blast (TNT equivalence) | Energy; burst radius ∝ W^(1/3) | `filler.re` → W in `Effects` |
| Brisance | How finely the casing breaks; local plate rupture | Fragment count and reach (08) |
| Sensitivity | Survives the armour impact or bursts in the plate | `filler.in_plate` |
| Initiation | Full detonation, low-order (partial), or dud | `filler.low_order`, by booster |
| Incendiary effect | Hot, slow products; smoke | Fire start chance (`Fires`) |
| Stability | Reacts with steel, takes on moisture, ages | Hidden: dud drift, magazine hazard, in-bore prematures |

Okun's outcome taxonomy (armor/04 §2.3) already has the four outcomes: full detonation, partial detonation,
deflagration, inert dud. This note supplies each explosive's rates for them.

---

## 2. The catalogue

Eleven fillers cover naval gunnery from the 1880s to 1945. Each answered a failing of the one before it.

| Explosive | Basis | Naval service | Users | Strengths | Weaknesses |
|---|---|---|---|---|---|
| Black powder | Low explosive: burns, never detonates | To ~1908 RN AP and common; common into WWI | All; IJN common at Tsushima | Cheap, sure to ignite, sets fires, tolerates impact | Low blast, few big fragments, heavy smoke |
| Wet guncotton (pyroxylin) | Nitrocellulose kept damp | 1890s–1905 shells; torpedoes, mines longer | Russia 1904–05 | Very safe to store wet | Needs a dry primer; Russian tube's primer too weak; many duds |
| Lyddite / Melinite | Picric acid, lightly desensitised | 1888–WWI | Britain, France | Strongest practical filler of its day | Shock-sensitive; forms touchy metal picrates with shell steel |
| Shimose | Picric acid, undiluted, cavity lined with lacquer and wax | 1893–1920s | Japan | Most blast and heat of its era; fine splinters, choking smoke | Bore prematures (1904–05); bursts on armour |
| Fp C/88 (Picrine) | German picric acid | 1888–1902 | Germany | As Lyddite | As Lyddite; dropped early |
| TNT (Fp C/02, Trotyl) | Trinitrotoluene | Germany 1902+; RN HE from 1907 | Germany, then all | Insensitive, stable, cast-loadable | Needs a strong booster; toluene supply |
| TNT/wax (Fp 5–Fp 20) | TNT with 5–20% wax | WWI–WWII AP and SAP | Germany; RN post-WWI cruiser and 16" | Survives armour impact very well | 5–20% less explosive per kg |
| Explosive D (Dunnite) | Ammonium picrate | USN AP 1911–WWII | United States | Most impact-insensitive filler used in quantity | Medium power; pressed, not cast |
| Shellite | Picric acid with dinitrophenol (60/40 wartime, 70/30 postwar) | 1918–1945 | Britain | Picric power, far less sensitive; easy to fill | Low-order without a CE booster, until the late 1920s |
| TNA (Type 91 explosive) | Trinitroanisole | IJN 1931–1945 | Japan | More stable than Shimose | Needed a heavy liner (⅓–½ of the cavity) |
| Amatol | TNT extended with ammonium nitrate | 1915+ | Britain, US and others: HE, mines, bombs | Up to 5× the shells per ton of TNT; near-TNT blast at ≥60% TNT | Less brisance, poor fragmentation; absorbs moisture |

Out of scope here: RDX mixes (Comp B, the RN's late 15" TNT/RDX HE; armor/04 §2.1 lists them) as a late-WWII HE tier,
and Torpex, Minol and DBX, which belong to the underwater model.

---

## 3. History: the calibration cases

**Russo-Japanese War, 1904–05: fire and blast beat penetration.** Japanese 12" AP used Shimose and suffered bore
prematures; *Mikasa* and *Shikishima* each lost a barrel at Tsushima (armor/04). No Japanese 12" shell is known to
have pierced Russian armour thicker than about 6". At Tsushima the Japanese seem to have fired only common shells, and
most damage came from their black-powder bursters setting large upper-works fires
([NavWeaps](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php)) [SRC]. A 1905 account describes Shimose hits tearing
large holes in unarmoured plating, with fine splinters and suffocating smoke
([Scientific American](https://www.scientificamerican.com/article/with-the-baltic-fleet-at-tsushima)) [SRC]. Russian
bursters were wet guncotton ([NavWeaps](https://navweaps.com/Weapons/WNRussian_12-40_m1895.php)) [SRC].
Rozhestvensky's report claimed two-thirds failed to explode
([press, 1905](https://paperspast.natlib.govt.nz/newspapers/FS19050919.2.4)); treat that as an upper bound
**[UNCERTAIN]**.

**Jutland, 1916: sensitivity, initiation and the shell.** Britain had moved APC to Lyddite from 1909–10 though tests
showed it more shock-sensitive than gunpowder
([Great War Forum, citing NavWeaps](https://www.greatwarforum.org/topic/97868-lyddite-shells/page/2/)) [SRC]. One of
17 heavy British shells striking thick German armour penetrated and burst inside
([Naval Gazing](https://navalgazing.net/Shells-at-Jutland)) [SRC]. The faults were the lyddite, fuzes without
effective delay, soft caps and weak bodies together (armor/04 §2.2). Germany had used TNT since 1902 and wax-desensitised
its AP and SAP bursters ([NavWeaps](https://navweaps.com/Weapons/WNGER_projectiles.php)) [SRC].

**Interwar: the filler and booster as one package.** Shellite replaced Lyddite in APC. With only a black-powder
booster it gave many weak or partial explosions; a tetryl (CE) booster came in the late 1920s
([NavWeaps, Okun](https://navweaps.com/index_nathan/British_Fuzes_after_Jutland.php)) [SRC]. Japan copied German
wax-TNT and buffered cavities, then moved to TNA in 1931
([NavWeaps](https://navweaps.com/Weapons/WNJAP_projectiles.php)) [SRC]. The USN kept Explosive D, which typically
survived heavy armour and was set off afterwards by the base fuze ([Wikipedia](https://en.wikipedia.org/wiki/Dunnite))
[SRC].

**Shortage: amatol.** The 1915 TNT shortage gave amatol. The 80/20 mix filled about five times the shells per ton of
TNT ([Nature, 1936](https://www.nature.com/articles/138578a0)) [SRC], with little loss so long as TNT stayed at or
above ~60% ([Wikipedia](https://en.wikipedia.org/wiki/Amatol)) [SRC].

---

## 4. The model

Three stages per hit, each a field of the shell block's `filler`.

**Stage 1, impact.** r = armour thickness ÷ the shell's penetration there (0 for an unarmoured hit; r > 1 is the
existing failure to penetrate).

```
P(in plate) = min(1, in_plate · r²)
```

`in_plate` is the explosive's rate at r = 1. A plate burst spends its effect outside and on the plate. The spaces behind
get 15% (spall) [EST].

**Stage 2, initiation.** A shell that survives intact rolls full detonation against `full_basic` or `full_ce` (the
player's booster tech). The rest are low-order 75% / dud 25% [EST]. A low-order burst gives 30% of the blast and no
fragment bonus [EST]. Black powder never detonates; its `re` already prices that, so it is always "full".

**Stage 3, effect.** W = filler kg × `re` × outcome share (1, 0.3, 0) sizes the burst as `Effects` does today. Brisance
scales fragments, and `fire` scales the fire start chance against the space's flammables. Inside a closed space,
11's confinement model takes W from here.

**Hidden stats.** Storage hazard adds a small monthly chance of dud drift or a magazine incident on old stock. Shimose
also gets an in-bore premature chance per shot, which damages the firing gun.

### 4.1 The catalogue's numbers

`re` follows armor/04 §2.1 where sourced (black powder 0.55, picric 1.17, TNT 1.00) [SRC]. Lyddite's `in_plate` 0.35
is armor/04 §6's sourced rate [SRC], and Shellite's 0.0 is the block's example [SRC]. Shellite's `full_ce` of 0.90 is
the sourced "90% full detonation" with CE [SRC]. The other rates are placed relative to these by the qualitative record
[FIT], and the remaining columns are [EST]. Explosive D's and TNA's `re` are **[UNCERTAIN]** (armor/04 found none).

| Explosive | re | Brisance | in_plate | Full, basic / CE booster | Fire | Storage | Cost/t | Filling | From |
|---|---|---|---|---|---|---|---|---|---|
| Black powder | 0.55 | 0.15 | 0.15 | 1.00 / 1.00 | 1.00 | low (damp → duds) | 0.30 | cast | 1860 |
| Wet guncotton | 0.85 | 0.90 | 0.08 | 0.55 / 0.85 | 0.20 | low | 0.90 | pressed | 1890 |
| Lyddite | 1.17 | 1.15 | 0.35 | 0.95 / 0.97 | 0.40 | high | 0.90 | cast | 1888 |
| Shimose | 1.17 | 1.20 | 0.45 | 0.95 / 0.97 | 0.50 | high + in-bore | 1.00 | cast | 1893 |
| TNT | 1.00 | 1.00 | 0.10 | 0.60 / 0.95 | 0.25 | very low | 1.00 | cast | 1902 |
| TNT/wax 90/10 | 0.90 | 0.90 | 0.04 | 0.55 / 0.93 | 0.25 | very low | 1.00 | pressed | 1905 |
| Explosive D | 0.95 | 0.90 | 0.02 | 0.50 / 0.92 | 0.20 | low | 1.10 | pressed | 1911 |
| Shellite | 1.00 | 1.05 | 0.00 | 0.60 / 0.90 | 0.35 | medium (ages) | 0.90 | cast | 1918 |
| TNA | 1.00 | 1.00 | 0.05 | 0.60 / 0.93 | 0.25 | low | 1.20 | cast | 1931 |
| Amatol 60/40 | 1.00 | 0.85 | 0.10 | 0.60 / 0.93 | 0.15 | medium (damp) | 0.75 | cast | 1915 |
| Amatol 80/20 | 0.95 | 0.65 | 0.12 | 0.55 / 0.90 | 0.10 | medium (damp) | 0.45 | pressed | 1915 |

### 4.2 Worked cases

Expected TNT-equivalent kg delivered **inside** the target per hit, from `filler_ref.py`. A and B are a heavy APC
(15 kg filler) against a belt it barely defeats (r = 0.9). C is the same shell against thin armour (r = 0.5). D and E
are an HE shell (50 kg) on an unarmoured hit. Brisance and fire ride on top as their own rolls.

| Explosive | A: AP r 0.9, CE | B: AP r 0.9, basic | C: AP r 0.5, CE | D: HE, CE | E: HE, basic |
|---|---|---|---|---|---|
| Black powder | 7.4 | 7.4 | 8.0 | 27.5 (most fires) | 27.5 (most fires) |
| Wet guncotton | 10.7 | 7.9 | 11.1 | 37.6 | 27.7 |
| Lyddite | 13.0 | **12.8** | **15.9** | **57.1** | **56.2** |
| Shimose | 11.9 | 11.7 | 15.5 | **57.1** | **56.2** |
| TNT | 13.4 | 9.7 | 14.1 | 48.1 | 34.5 |
| TNT/wax 90/10 | 12.4 | 8.6 | 12.7 | 42.6 | 29.3 |
| Explosive D | 13.2 | 8.6 | 13.3 | 44.6 | 29.1 |
| Shellite | **13.8** | 10.3 | 13.8 | 46.1 | 34.5 |
| TNA | 13.7 | 10.0 | 14.0 | 47.3 | 34.5 |
| Amatol 60/40 | 13.2 | 9.7 | 13.9 | 47.3 | 34.5 |
| Amatol 80/20 | 12.1 | 8.6 | 12.8 | 43.8 | 30.9 |

What the table says:

- **Heavy armour with a CE booster (A):** the insensitive fillers lead, but only by 5–15% over Lyddite once its
  sourced rate is used. Lyddite's real cost at Jutland was mostly in the shell, not in this table.
- **No booster tech (B, E):** picric wins everywhere, by ~30% against armour and ~60% against soft targets. That is
  why every navy but Germany filled with picric before about 1905, and the early game's real trade-off.
- **Thin armour (C):** picric's extra blast shows; the filler choice barely matters otherwise. This is cruiser work.
- **Soft targets (D):** picric leads on blast by ~20%. Black powder trails on blast but leads on fire, which is how
  Tsushima was won.
- **Amatol** stays within ~5–10% of TNT using a fraction of the TNT: the shortage choice for HE, a weak one for AP.

**Tuning knob to watch:** the r² in Stage 1. If calibration against the 1921 *Baden* trials or the Jutland sample says
picric's plate bursts start at lower r, a steeper curve widens the gap in A. Settle it with S1's templates.

---

## 5. Availability and logistics

Fillers draw on different raw materials and plants, so a player short of one can pivot to another.

| Feedstock | Feeds | Historical bottleneck | Game lever |
|---|---|---|---|
| Toluene (coal tar, later petroleum) | TNT, TNT/wax, amatol, TNA in part | Britain's 1915 shell crisis | Coking and refinery capacity; imports |
| Phenol / benzene (coal tar) | Lyddite, Shimose, Shellite, Explosive D | Shared with the dye and chemical industry | Chemical industry level |
| Nitrates (Chilean, later synthetic) | Every high explosive and propellant; most of amatol | Blockade of Chilean nitrate | Blockade risk; synthetic-nitrogen tech |
| Saltpetre, sulphur, charcoal | Black powder | Rarely binding | Always available, lowest tier |

- **Filling throughput.** Cast-loadable fillers fill fast. Pressed ones (Explosive D, 80/20 amatol, wax-TNT pellets) need
  presses; give them a lower rate per plant.
- **Booster tech.** A CE (tetryl) booster unlock moves every insensitive filler from `full_basic` to `full_ce`
  (historically 1918 US Army, late 1920s RN and USN delay fuzes; armor/04 §2.1).
- **Switching cost.** A filler change is a factory and stock decision. Britain took about a year from trial orders
  (May 1917) to Greenboys in the fleet (April 1918). A refit or resupply cycle is the right grain.
- **Storage.** Picric stock gets a rising hazard and dud chance with age, amatol in damp climates a dud drift. TNT and
  Explosive D stay stable, a hidden peacetime advantage.

---

## 6. Recommendations for the game

1. **The block names an explosive from the catalogue** (overridable), as open question 2 proposes. `re`, `in_plate` and
   the full-detonation rate come from the catalogue, the last picked by the ship's booster tech. `low_order` follows
   as 1 − full.
2. **Choose per shell kind, at procurement and refit.** AP and HE fillers are separate decisions; the outfit stays the
   tactical lever.
3. **Show four stats to the player:** power (`re`), shock tolerance (1 − `in_plate`), reliability for the current
   booster, fire. Keep brisance, storage and in-bore prematures in tooltips or hidden.
4. **Gate by year and tech:** black powder and wet guncotton from the start; picric ~1888; TNT ~1902; Explosive D
   ~1907–11; amatol 1915; Shellite 1918; CE booster 1918–28; TNA 1931; RDX mixes late WWII.
5. **Make history's dilemmas reachable:**
   - 1905 Japan: Shimose's blast and splinters with bore prematures, or black powder's fires and safety.
   - Pre-Jutland Britain: Lyddite APC that sometimes bursts on the plate, or TNT that goes low-order without a booster.
   - 1915 shortage: pure TNT for few shells, or amatol for many.
   - Blockade: nitrates cut; black powder and stock rationing become live.
6. **Report outcomes per hit** ("burst on plate", "low-order", "dud") so players learn what their filler did.

## 7. Open questions

- [ ] Calibrate `in_plate` and the r² curve against the Jutland sample and the 1921 *Baden* trials.
- [ ] Source `re` for Explosive D and TNA (armor/04 found none).
- [ ] Does the explosive change the shell (cavity share, liner), or only the fill? Japan gave up cavity for TNA's liner.
- [ ] 11's confinement model needs heat of detonation and combustion per explosive; add them to this catalogue so the
      two notes share one table.
- [ ] Underwater fillers (Torpex, Minol, DBX) for the torpedo and mine model.

## Sources

- [NavWeaps — Japanese 12"/40 EOC](https://navweaps.com/Weapons/WNJAP_12-40_EOC.php)
- [NavWeaps — Japanese projectile terms](https://navweaps.com/Weapons/WNJAP_projectiles.php)
- [NavWeaps — German projectile terms](https://navweaps.com/Weapons/WNGER_projectiles.php)
- [NavWeaps — Russian 12"/40 Pattern 1895](https://navweaps.com/Weapons/WNRussian_12-40_m1895.php)
- [NavWeaps — Okun, British fuzes after Jutland](https://navweaps.com/index_nathan/British_Fuzes_after_Jutland.php)
- [NavWeaps — Okun, Nose-only shatter (Explosive D)](https://www.navweaps.com/index_nathan/Nose-Only%20Shatter%20Defined.rtf)
- [Naval Gazing — Shells at Jutland](https://navalgazing.net/Shells-at-Jutland)
- [Naval Gazing — Shells Part 3](https://www.navalgazing.net/Shells-Part-3)
- [Ministry of Supply R&D records (DTIC ADA492806)](https://apps.dtic.mil/sti/pdfs/ADA492806.pdf)
- [Great War Forum — Lyddite shells](https://www.greatwarforum.org/topic/97868-lyddite-shells/page/2/)
- [Scientific American (1905) — With the Baltic Fleet at Tsushima](https://www.scientificamerican.com/article/with-the-baltic-fleet-at-tsushima)
- [Feilding Star (1905) — Rozhestvensky's report](https://paperspast.natlib.govt.nz/newspapers/FS19050919.2.4)
- [Nature (1936) — E. R. Deacon obituary (amatol)](https://www.nature.com/articles/138578a0)
- [USNI Proceedings (1915) — Military explosives](https://www.usni.org/magazines/proceedings/1915/november/military-explosives)
- Wikipedia: [Shimose powder](https://en.wikipedia.org/wiki/Shimose_powder),
  [Shellite](https://en.wikipedia.org/wiki/Shellite_(explosive)), [Dunnite](https://en.wikipedia.org/wiki/Dunnite),
  [Amatol](https://en.wikipedia.org/wiki/Amatol),
  [WWII explosives](https://en.wikipedia.org/wiki/List_of_explosives_used_during_World_War_II)
- Project notes: armor/04 (`04_shells_fuzes.md`), `shells-armour-damage.md`, `11_blast_confinement.md`