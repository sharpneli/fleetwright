# 07 — Armour-penetration calibration dataset: notes
Status: draft    Updated: 2026-10-16    Request: —

**Published layout (), compacted from the working files described below:**
- `07-calibration-tables.csv` — 303 rows: every published table that is *not* Okun FACEHARD (USN 1905/1918/1942, Garzke & Dulin, Raven & Roberts, Italian official 1942, German Meppen/Whitley, Russian, DNO 1912, etc.). Columns as below plus `src` (key into `07-table-sources.csv`) and `note` (original imperial values, obliquity for the Italian rows).
- `07-okun-facehard-tables.csv` — the 18 Okun/Lundgren FACEHARD v5.0 tables in wide form, 357 rows (one per shell × range): `strike_v_ms`, `fall_deg`, then for each belt plate `uk` (British CA), `us` (US Class A 1935–43), `it` (Italian Terni), `de` (German KC n/A), `jp` (Japanese VH) the four criteria `PP/H/NL/EFF` in mm, and `*_deck_mm` = that nation's homogeneous deck (UK NCA 25% el., US Class B/STS 25%, Italian 17.1%, German Wh 18%, Japanese NVNC/CNC Q 0.97). Converted from inches (rounding ±0.5 mm, coarse for thin decks). Not every table has every criterion: the US, Italian 381 and most French tables give only PP and EFF.
- `07-table-sources.csv` — source note and URL per `src` key.
- `07-trials.csv` — unchanged (225 rows).
- The raw transcription scripts (`build/`) were not published; the counts below refer to the original long-format file (2,088 rows = 303 + 357×5).

---

Files (same folder):
- `07-calibration-tables.csv`: 2,088 rows covering 41 guns. Each row is a published penetration value for one gun, shell, range and plate type.
- `07-trials.csv`: 225 rows. These are individual firings, measured ballistic limits, and US acceptance-test velocities.
- `build/`: the raw transcriptions (`data_a/b/c.py`, `okun/*.csv`, `trials.py`) and `build.py`, which regenerates both CSVs. Fix a typo in the raw files and re-run `python3 build.py`.

All values were transcribed with WebFetch from navweaps.com pages, USNI Proceedings (1890, 1893, 1897, 1901) and Naval Ordnance 1937. The original imperial values are kept in `table_source_note` ("orig: …") or `notes`. I used these conversions: yd×0.9144, fps×0.3048, in×25.4, lb×0.45359. Nothing was interpolated. The one exception is that SV and angle of fall were copied from the same page's range table, and only when the range **and** MV matched exactly; the note says when this was done. Cells with no source value are blank.

## 1. What is in the calibration table

| Era | Guns (rows) |
|---|---|
| 1880s–1905 | UK 12"/35 Mk VIII (wrought iron, steel, KC), UK 12"/40 Mk IX, US 13"/35, US 12"/40 Mk 3/4 (Harvey 1905 + 1918 data), Russia 12"/40 M1895 (KC, full range curve with SV), Germany 28 cm SK L/40 |
| WWI era | UK 12"/45 Mk X (incl. DNO 1912 calibre fractions), UK 13.5"/45 Mk V(L)/(H), UK 15"/42 (Big Gun / Raven & Roberts uncapped vs KC with 0/20/30° obliquity / 1935 theoretical / G&D / Okun Greenboy), US 12"/50 Mk 7, US 14"/45, Germany 28 cm SK L/45, 30.5 cm SK L/50, 38 cm SK L/45 (incl. M.D.V. 352 30° fit-to-burst), Russia 12"/52 M1907 |
| WWII | US 16"/50, 16"/45 Mk 6, 16"/45 Mk 5, 14"/50, 12"/50 Mk 8, 8"/55 SH, 5"/38 (Special Common); UK 16"/45 Mk I, 15"/42 (Cardonald), 14"/45 Mk VII, 8"/50 SAPC, 6"/50 CPBC; Germany 38 cm C/34, 28 cm C/34, 20.3 cm C/34, 15 cm C/28; Japan 46 cm, 41 cm, 36 cm, 20 cm; Italy 381/50 (official KC tables with obliquity), 203/53 (official KC + homogeneous with obliquity); France 380/45, 330/50; G&D muzzle figures for H-class 40.6 cm and Lion 16" |

**Okun/Lundgren FACEHARD tables.** These make up most of the rows. There are 18 gun/shell tables, each evaluated against 5 plate types: British CA, US Class A, Italian Terni cemented, German KC n/A and Japanese VH. Each range step of 2 kyd also has a deck value against that nation's homogeneous deck plate. `belt_pen_mm` holds the **EFF** (effective, fit-to-burst) limit. When EFF is "-", it falls back to NL, then H, then PP. All four criteria are kept in the note. The plates are vertical, so obliquity equals the angle of descent. These tables cover these guns:
- US 16"/50 Mk 8 Mods 1–5 and 14"/50 Mk 16 Mods 1–6.
- UK 16"/45 Mk IB, 15"/42 Mk XVIIB Cardonald, 15"/42 Mk IIIa Greenboy, 14"/45 Mk IB, and 8"/50 SAPC.
- Germany 38/52, 28/54.5, 20.3/60 and 15/55.
- Japan 46, 41, 36 and 20 cm.
- Italy 381/50.
- France 380/45 and 330/50.

**Plate-type column.** I recorded the plate type exactly as the page states it. Most G&D and USN 1942-curve tables give **no plate type**. For those I wrote "not stated", sometimes adding what is implied.

## 2. How each family of numbers was produced (and how far to trust it)

1. **Garzke & Dulin ("Battleships" trilogy).** The pages say these are "based upon the USN Empirical Formula". Priestle reverse-engineered the muzzle values as T(in) = 0.0004689·W^0.55·D^-0.65·V^1.1, with an RMS error of 0.012" across 14 guns. In other words they are **one formula, not data**: fitting to them only recovers that formula. They give no plate type or obliquity. Some pages say they are "in substantial agreement with" the 1942 USN curves.
   - The British G&D rows (14"/45, 16"/45 Mk I, 15"/42) are **much lower** than Okun's. For example, at 18.3 km the 16"/45 is 310 mm in G&D against 480 mm (US Class A EFF) in Okun. Navweaps says these are "partly based upon official data". Treat them as suspect.
   - The Japanese 41 cm G&D figure is also low: 269 mm at 20 km against 391 mm in Okun.
2. **USN 1942 armour-penetration curves** (Friedman "US Naval Weapons"). These are official USN figures and probably assume Class A at normal impact. They are given as ranges for round thicknesses (5"/38, 8"/55, 12"/50 Mk 7, 16"/45 Mk 5). Trust is medium.
3. **"Elements of US Naval Guns" 1918 and "Ordnance Data Sheets" 1905.** These are official USN figures. The 1918 data is "corrected for angle of fall". The 1905 12"/40 data is explicitly for Harvey plate. These are good pre-dreadnought and WWI anchors.
4. **Raven & Roberts.** Their WWI figures are for **uncapped AP vs KC at 90°** and use proof-level MVs. They also have a "1935 theoretical calculation" table for 15"/42 and 14"/45, which does not state the MV.
5. **German official or test-based figures.** These are the best German rows:
   - 28 cm C/34 **Meppen pre-war test shoots** against KC at 70° impact angle, i.e. 20° obliquity.
   - 38 cm C/34 and 20.3 cm "German face-hardened/homogeneous penetration curves" (from Whitley).
   - M.D.V. 352: 38 cm L/45 fit-to-burst at 30° against improved WWI KC. Only 2 rows were captured; the page may have more.
6. **Italian official tables** ("Direttive e norme…", 1942). These give KC penetration at **0/10/20/30/40° obliquity**. I converted from the Italian convention, where 90° means normal impact. These are the **highest-value rows for fitting the obliquity term**. The SAPBC values barely change with obliquity, as printed.
7. **Okun FACEHARD v5.0.** This is a physics-based model calibrated on real US, British and German tests. It is internally consistent and gives plate-type multipliers across 5 armours. It is still a model, not data, so use it as a smooth prior or as the shape of curves. The page notes say later FACEHARD versions differ slightly.
8. **DNO 1912 calibre fractions** (12"/45). The ranges are bands. I used the band midpoint, and the band is given in the row note. The "4/3 cal at ≤6,000 yd" entry for 2crh AP is printed as-is and looks anomalous.

## 3. Discrepancies and typos found (recorded, not silently fixed)

- **16"/45 Mk 6, 10 kyd deck.** The page prints 1.87" as "(28 mm)". I used the inch value.
- **16"/45 Mk 5.** The penetration table shows SV 2,600 fps at 0 yd, but the MV is 2,520. The fall angle at 35 kyd is 49.47°, while the range table gives 38.65° at 34 kyd. Both are recorded as printed.
- **15"/42 G&D.** The mm values for the 3.10" and 4.30" deck figures are inconsistent with the inches. I used the inch values.
- **30.5 cm SK L/50.** The page equates 16,000 yd with 15,000 m. I used the yards.
- **12"/40 Mk 3.** The page prints the 1905 MV as "2,600 fps (640 mps)". The fps value is the correct one.
- **Shinano test 2.** The article prints 1,707 fps as 502.3 m/s. 1,707 fps is 520.3 m/s, and I used the fps value.
- **203/53 Mariperman deck.** The page prints "91 mm (1.54 in)". I recorded 91 mm and flagged it.
- **380/45.** Two books give different MVs (785 vs 820 m/s) but the same penetration figures.
- **Okun 13"/50 (French).** The US Class A values drop below the British ones from 24 kyd onward. This is as transcribed and may be a page error.
- **Gun pages vs Okun.** Comparing G&D or official values with Okun at the same range: US guns agree within ~5% out to 30 kyd. Okun deck values are 10–20% lower than G&D at long range, and Okun's belt values at the muzzle are higher (Okun 16"/50: 853 mm vs G&D 829 mm).
- **Transcription risk.** The Okun tables were pulled through an LLM page summariser, row by row. I spot-checked monotonicity, and the values are smooth. Still, verify a few cells against the live page before the final fit. The OS 751 Class B column assignment also comes from that extraction.

## 4. Trials file (`07-trials.csv`)

- **Real individual firings (≈50 rows):**
  - Annapolis 1890: 15 shots of 6" and 8" uncapped AP against Schneider steel, nickel-steel and Cammell compound plates, all ~10.5". All shots have SV, and the 6" shots were at normal impact.
  - Indian Head 1893: 4 shots of a 10" Holtzer shell against 14" Harveyed nickel steel at 1,472–2,059 fps. Penetrations are estimates.
  - USNI 1897 and 1901 examples: Carnegie, Bethlehem and Krupp plates, including the Krupp Meppen 1895 shot.
  - Naval Ordnance 1937 textbook problems: ballistic limits for 12" against KC and Class A, and 10" against Harvey and KC. These may be illustrative.
  - Baden 1921: Greenboy shells at 1,550 fps, with obliquity, against 350 mm and 250 mm KC.
  - German WWI 38 cm development shots and British tests of Russian M1911 APC against 8" plate at 20°: the shell broke at 1,447 fps and was intact at 1,615 fps.
  - 1942 US 16" Mk 8 shots against 20.6" Class A.
  - The 1946 Shinano 26" VH turret-face shots: perforated at 1,992 fps, partial at 1,707 fps, both at about 0°.
- **Measured ballistic limits (30 rows).** These come from NPG Dahlgren tests in 1946–47 of German KC n/A and Wh plates alongside US Class A and B, using 8" Mk 21, 12" Mk 18 and 14" Mk 16 at 30–35° obliquity (ADM 281/127 via Okun). The "% of NBL" column is the measured limit divided by the US Navy ballistic-limit curve. **These are the most trustworthy rows in the whole dataset** for heavy APC against modern plate at obliquity. Rows marked "under" are upper bounds only.
- **US acceptance-specification velocities (≈140 rows).** These come from O.S. 751 Rev K (1947): Class A Tables Ia/Ib for 3"–16" shells at 20–40°, and Class B for 12"/14"/16" at 30–40°. An acceptable plate must stop the shell (no more than ½ the body passing through) at the listed velocity, so these are **approximately lower-bound Navy ballistic limits for average-good plate**. Rows tagged fn3 waive the complete-penetration criterion and are less useful. Shell weights for the 3" shell are unknown.

## 5. Most trustworthy rows, in rough order

1. NPG 1946–47 measured ballistic limits (trials) and the 1942 and 1946 individual 16" shots.
2. O.S. 751 acceptance velocities, for the shape of thickness vs velocity at fixed obliquity.
3. Italian official obliquity tables (381/50, 203/53) and the German Meppen 28 cm test-based table.
4. USN 1905/1918/1942 official curves.
5. Annapolis 1890, Indian Head 1893 and Baden 1921 single shots (exact SV, plate known; few shots).
6. Okun FACEHARD (consistent model output across 5 plate types; use for plate-type ratios and curve shape).
7. Garzke & Dulin (a single empirical formula; British and Japanese values doubtful).
8. Secondary-book single figures (Big Gun, Parkes, Campbell, Staff, "Main Caliber of the Battleships").

## 6. Gaps

- There is no numeric penetration table on navweaps for these guns: US 6"/47 Mk 16 (range table only), German 15 cm C/28 (covered by Okun only), Japanese 46 cm and 36 cm (Okun and the G&D muzzle value only), Japanese 12"/40 and 12"/45 (combat anecdotes only), and British 12"/50.
- Not found online in fetchable form: Hood 1919 trials, Tosa 1924 numeric results, Indian Head Harvey test series tables (the 1897 article omits its appendix), the Krupp KC acceptance series, the Chesma 1913 trials, Okun's metallurgy and quality-factor table (`metalprpsept2001.php` failed to fetch), and the O.S. 751 Class B 6"/8" tables (refused or mislabelled in extraction; only the 6" Mk 35 table is partly known).
- Destroyer guns: only the US 5"/38 has a table (Special Common). The Okun Japan page has 4.7"–6" common-shell tables that I did not extract.
- Also not extracted: Okun tables for US 16"/45 Mk 5/Mk 8, 14"/45 Mk 16, and the second British 15"/42 tables (Mk 13A, Mk 5A Blue Band, Non-Cardonald), plus German 11.1"/52 and 5.9"/60, French 380 variants and 12"/45, Italian 12.6"/44, Japanese 6.1"/60, and UK 9.2"/47. Any of these can be added with the same workflow.
