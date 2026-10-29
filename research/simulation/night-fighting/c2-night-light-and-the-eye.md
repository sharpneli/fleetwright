# C2: Photometry of the night and the eye's threshold
Status: draft    Updated: 2026-10-29    Request: [REQUESTS.md "Night fighting, 1890-1925 first"](../../REQUESTS.md); an appendix of [night-fighting.md](../night-fighting.md)

Strand: light levels from twilight to the moonless overcast night, sky luminance by azimuth, moonlight, and the eye's
thresholds for large targets, point lights, glare and flash. Tags: **[SRC]** read in the source (or a direct quote or
abstract of it), **[INFERRED]** my fit or arithmetic on sourced numbers, **[EST]** an engineering estimate,
**[UNCERTAIN]** source garbled or conflicting. The fits and conversions are in [c2_photometry_calc.py](c2_photometry_calc.py) and [c2_photometry_calc2.py](c2_photometry_calc2.py) next to this
file. Units: 1 fc = 10.764 lux; 1 cd/ft² = 10.764 cd/m²; 1 nL = 3.183·10⁻⁶ cd/m²; 1 mL = 3.183 cd/m²;
V mag/arcsec² → cd/m² ≈ 1.08·10⁵·10^(−0.4V).

---

## 1. Illuminance on a level surface vs the sun's elevation (clear sky)

| Sun elev. | Schlyter table [1] (lux) | USNO Circ. 171 model [2], sun term only (lux) [INFERRED: evaluated] | Koomen et al. 1952, Sacramento Peak (2800 m), sky on a horizontal opal plate [3] | Game |
|---|---|---|---|---|
| +10° | 11,100 | 11,100 | – | 15,000 |
| +5° | 4,540 | 4,090 | 190 fc = 2,050 lux (sky only?) | 6,000 |
| 0° | 759 | 757 | 40 fc = 430 lux | 500 |
| −2° | 189 | – | – | – |
| −3° | ≈75 (log-interp.) | 66 | 4 fc = 43 lux | 60 |
| −4° | 29.9 | – | – | – |
| −6° | 3.41 | 2.99 | 0.125 fc = 1.35 lux | 3 |
| −8° | 0.348 | – | – | – |
| −9° | ≈0.12 (interp.) | 0.116 | ≈0.005 fc = 0.054 lux [UNCERTAIN: decoded, see §2] | 0.1 |
| −10° | 0.0421 | – | – | – |
| −12° | 0.00806 | 0.0044 | – | 0.008 |
| −14° | 0.00292 | – | – | – |
| −15° | ≈0.002 | 1.7·10⁻⁴ | – | – |
| −16° | 0.00144 | – | – | – |
| −18° | 6.45·10⁻⁴ | 6.6·10⁻⁶ | – | 7·10⁻⁴ |

- Schlyter's table runs in 0.5° steps from 90° to −18° and gives no primary source [SRC]. Below about −14° its values
  flatten toward the starlight floor, so its −16°/−18° rows **include the night sky**. The USNO model is sun only and
  adds a separate 0.0005 lux night term [SRC, 2].
- USNO Circular 171 (Janiczek & DeYoung 1987): sun = 133,775·M(h) lux, with one atmospheric-path function M for every
  altitude, including negative ones [SRC, code in the `skylight` R package, 2a, 2b]. The circular says its values are
  good to "one or two digits", and that real levels can differ by 10× or more with local conditions [SRC].
- Koomen et al. measured with an opal-glass plate "exposed to the sky". Their values run 0.4–0.5× Schlyter's through
  twilight. They may exclude the direct sun, the plate's cosine response may be poor, or the sites may differ
  [UNCERTAIN].
- Koomen et al.: from about −3° to −11° "the entire sky changed in brightness ... at the rate of about a factor of 10
  for each 2° change in H" [SRC, 3]. The Schlyter and USNO columns agree: −6° to −10° is 3.41 → 0.042 lux, 1.9
  decades in 4° [INFERRED].
- Biberman et al. (IDA P-232, 1966) reprints Brown's 1952 BuShips curves only in an appendix that wasn't in the
  extract. Brown's own numbers were **not reached**.

## 2. Twilight sky luminance at the horizon by azimuth from the sun

Source: Koomen, Lock, Packer, Scolnik, Tousey & Hulburt, JOSA 42, 353 (1952) [3]. It is a photopic recording
photometer at Sacramento Peak (2800 m) and Maryland (30 m), on clear skies, for sun H = +5° to −15°, at
azimuths from the sun Z = 0, 22.5, 45, 90, 135, 180° and altitudes P = 0, 10, 30, 50, 70, 90°. Units: cd/ft².

**Reading the tables [INFERRED, important]:** the OCR shows deep-twilight entries such as "0.0236", "0.0375" and
"0.0476". At face value these would make the sky brighten after −9°, which the paper's own text rules out. They read
as the old subscript notation 0.0ₙxx, meaning n zeros after the decimal point: "0.0236" = 0.0036, "0.0375" = 0.00075,
"0.0476" = 0.000076. Decoded this way, every column falls by about 10× per 2–3° as the text says. The zenith at −15°
then comes to 2·10⁻⁵ cd/ft² = 2.2·10⁻⁴ cd/m², which matches the dark-sky zenith in §3. The decoded values are used
below.

### 2a. Horizon (P = 0°) luminance, Sacramento Peak, cd/ft² (×10.76 for cd/m²)

| Sun H | Z=0° | 22.5° | 45° | 90° | 135° | 180° | toward/away | zenith (Z=180,P=90) |
|---|---|---|---|---|---|---|---|---|
| +5° | 1000 | 600 | 200 | 130 | 160 | 190 | 5.3 | 31 |
| 0° | 150 | 102 | 62 | 37 | 32 | 38 | 3.9 | 8.0 |
| −3° | 20 | 15 | 11 | 4.2 | 2.4 | 2.4 | 8.3 | 1.0 |
| −6° | 0.9 | 0.68 | 0.4 | 0.11 | 0.078 | 0.078 | 11.5 | 0.022 |
| −9° | 0.04 | 0.028 | 0.017 | 0.005 | 0.0036 | 0.0036 | 11.1 | 0.00075 |
| −12° | 0.0018 | 0.0014 | 0.00076 | 0.00036 | 0.00026 | 0.00028 | 6.4 | 0.000076 |
| −15° | 0.00018 | 0.00013 | 0.00008 | 0.00005 | 0.00005 | 0.000058 | 3.1 | 0.000020 |

[SRC for +5° to −6°. Decoded [INFERRED] for −9° and below. The 0° row's Z=0 entries are partly garbled [UNCERTAIN].]
Maryland's horizon gives the same picture: toward/away = 0.8/0.09 = 8.9 at −6°, ≈11 at −9°, ≈6.7 at −12° [INFERRED,
decoded]. The paper says the two sites were "about the same" at the horizon, while elsewhere Sacramento Peak was
about 3:2 brighter [SRC].

**How many times darker opposite the sun [INFERRED]:** about 4–5× at sunset, **8× at −3°, 11× at −6° and −9°, 6×
at −12°, 3× at −15°**. The asymmetry peaks around −6° to −9°, not at night.

### 2b. Fits to the azimuth profile (ln L vs Z, six azimuths, Sacramento Peak horizon)

| Sun H | k in L ∝ e^(k cos Z) (rms in ln) | glow + floor: L = F + G·e^(k(cos Z − 1)): k, F/G (rms) |
|---|---|---|
| +5° | 0.68 (0.52) | 9.8, 0.18 (0.11) |
| 0° | 0.63 (0.28) | 5.0, 0.33 (0.06) |
| −3° | 1.07 (0.18) | 2.1, 0.125 (0.05) |
| −6° | 1.24 (0.30) | **3.1, 0.094 (0.02)** |
| −9° | 1.19 (0.30) | **3.2, 0.10 (0.04)** |
| −12° | 0.91 (0.27) | 3.4, 0.19 (0.06) |
| −15° | 0.51 (0.28) | 5.4, 0.42 (0.05) |

[INFERRED, least squares in ln L.] The pure e^(k cos Z) form fits badly. The sky is a glow concentrated toward the sun
(it falls ×8 from Z=0 to Z=90) over a near-flat floor from Z=90° to 180°. The two-term form fits within 2–6 % in
twilight, with **k ≈ 3.2 and floor ≈ 0.1 of the glow peak for −6° to −12°**. The best single e^(k cos Z) is
**k ≈ 1.2**, which gives toward/away e^(2.4) ≈ 11.

### 2c. Horizon luminance against the level illuminance (Sacramento Peak, Table III E)

| Sun H | E/π (cd/ft²) | toward / (E/π) | away / (E/π) | horizon azimuth mean / (E/π) |
|---|---|---|---|---|
| +5° | 60.5 | 16.5 | 3.1 | 4.4 |
| 0° | 12.7 | 11.8 | 3.0 | 4.3 |
| −3° | 1.27 | 15.7 | 1.9 | 5.5 |
| −6° | 0.040 | 22.6 | 2.0 | 6.6 |
| −9° | 0.0016 | 25 | 2.3 | 7.2 |

[INFERRED.] The horizon is on average 4–7× brighter than E/π. The side away from the sun alone is about 2–3× E/π,
and the zenith is about 0.5× E/π at −6°.

### 2d. Dependence on altitude above the horizon (Sacramento Peak)

- Away from the sun (Z=180°), horizon/zenith: 3.5 at −6°, 4.8 at −9°, 3.7 at −12°, 2.9 at −15° [INFERRED, decoded].
  At −6° the profile is 0.078, 0.068, 0.043, 0.035, 0.025, 0.022 cd/ft² for P = 0, 10, 30, 50, 70, 90° [SRC].
- Toward the sun (Z=0°), at −6°: 0.9, 0.7, 0.18, 0.06, 0.033, 0.022 [SRC]. The glow is a low arch: it is down 5× by
  30° altitude and 40× by the zenith. At −9° horizon/zenith is ≈53 [INFERRED].
- Maryland, away side at −6°: 0.09 → 0.06 (×1.5) [SRC]. A hazier, low site gives a flatter sky.

### 2e. Zenith twilight, a second source

Patat et al. 2006 (ESO Paranal, V band) fit the zenith as V = 11.84 + 1.518(ζ−95) − 0.057(ζ−95)² mag/arcsec² for
ζ = 95–105° [SRC, 5]. Converted [INFERRED], the zenith is 2.0 cd/m² at sun −5°, 0.52 at −6°, 0.048 at −8°, 0.0068 at
−10°, 0.0015 at −12°, 3.2·10⁻⁴ at −15°. The night level is reached at ζ ≈ 105–106° [SRC]. Koomen's zenith at −6° is
0.24 (Sacramento Peak) and 0.65 (Maryland) cd/m², and at −9° 0.008: the same within ×2.

## 3. The moonless night, moonlight, overcast

### 3a. Clear moonless night

| Quantity | Value | Source |
|---|---|---|
| Level illuminance, starlight + airglow + zodiacal | 1.1·10⁻³ lux (10⁻⁴ fc) | Biberman et al. 1966 [4] [SRC] |
| same | 2·10⁻³ lux (starlight only 2·10⁻⁴) | Schlyter [1] [SRC] |
| same, measured, dark rural sites | 0.92–1.10 mlx (Lake Stechlin), 0.91–0.96 mlx (Bonnievale) | Jechow, Hölker & Kyba 2019 [6] [SRC] |
| model constant | 5·10⁻⁴ lux | USNO Circ. 171 [2] [SRC]; MoonShineR default 8·10⁻⁴ [7] [SRC] |
| Zenith luminance, measured | 0.24–0.30 mcd/m² | [6] [SRC] |
| Zenith, dark observatories | V = 21.5–21.9 mag/arcsec² ≈ 1.9–2.6·10⁻⁴ cd/m² | Patat 2003 Table 5 (Benn & Ellison) [8] [SRC]; conversion [INFERRED] |
| Horizon luminance | ≈10⁻³ cd/m² (V≈20) | Schlyter [1] [SRC] |
| Horizon/zenith | ≈1.7–2.7 (airglow's van Rhijn brightening against extinction, k = 0.3–0.17 mag/airmass) | from Krisciunas & Schaefer's dark-sky form B₀·X·10^(−0.4k(X−1)), X≈5 [9] [INFERRED] |
| Koomen −15° zenith/horizon (twilight almost gone) | 2.2·10⁻⁴ / 6–19·10⁻⁴ cd/m² | [3] [INFERRED, decoded] |

- Spread over the solar cycle: the cross-site table pairs solar flux S10.7 = 2.0 MJy with V = 21.5 and S10.7 = 0.8 with
  21.9. That is about 0.4 mag, ×1.4, maximum against minimum [INFERRED from [8], cross-site, weak]. About a third of
  the night sky's visible light is airglow: "150 S10(vis) units or one-third of the visible light-of-the-night-sky is
  of geophysical ... origin" [SRC, 4]. **Latitude was not reached.**
- The night sky's E/π: 1 mlx/π = 0.32 mcd/m², which is the measured zenith luminance [INFERRED]. The horizon is about
  2–3× that.

### 3b. Overcast moonless night

| Quantity | Value | Source |
|---|---|---|
| Overcast night sky | 10⁻⁵ fc = 1.1·10⁻⁴ lux (1/10 of clear) | Biberman [4] [SRC] |
| Overcast night | 10⁻⁴ lux; horizon 10⁻⁴ cd/m² (1/10 of clear) | Schlyter [1] [SRC] |
| Measured, rural | horizontal 0.68–0.76 mlx under overcast vs 0.96–1.10 clear (×1.25–1.6, residual artificial light). Natural upper limit under overcast 0.34 mlx (≥×3 darker); zenith ×1.25–1.5 darker; near the horizon up to ×5–11 darker | Jechow et al. 2019 [6] [SRC] |

### 3c. Moonlight

- Full moon at zenith, level: 0.267 lux (Schlyter [1]) [SRC]. The USNO model gives 0.38 lux, which includes the
  opposition surge [INFERRED, evaluated]. Krisciunas & Schaefer give m = −12.73 for full moon without the surge, about
  0.32 lux outside the air and 0.23–0.25 lux on the ground at the zenith [INFERRED]. "When the Moon is exactly full it
  is about 35% brighter than the extrapolation would predict", only within about a day of full [SRC, 9].
- Phase law, Krisciunas & Schaefer 1991 eq. 9: m = −12.73 + 0.026|α| + 4·10⁻⁹α⁴, with α the phase angle in degrees
  [SRC]. Evaluated [INFERRED]:

| Phase angle α | Lit fraction | Relative to full | Outside air (lux) |
|---|---|---|---|
| 0° (full) | 1.00 | 1.000 | 0.32 |
| 30° | 0.93 | 0.49 | 0.15 |
| 45° | 0.85 | 0.34 | 0.11 |
| 60° | 0.75 | 0.23 | 0.072 |
| 90° (quarter) | 0.50 | 0.091 | 0.029 |
| 120° | 0.25 | 0.026 | 0.0083 |
| 150° | 0.07 | 0.004 | 0.0014 |

  Schlyter has the half moon "only about 1/10 as bright as the Full Moon" [SRC].
- Full moon by altitude on a level surface [INFERRED, both evaluated]:

| Moon altitude | 90° | 60° | 45° | 30° | 20° | 10° | 5° |
|---|---|---|---|---|---|---|---|
| USNO model (lux) | 0.38 | 0.32 | 0.25 | 0.16 | 0.093 | 0.034 | 0.012 |
| K&S airmass, k = 0.2, no surge (lux) | 0.25 | 0.21 | 0.16 | 0.10 | 0.060 | 0.018 | 0.004 |

- Clouds on moonlight: the USNO divisors are 1 clear (<30–70 % cloud), **2 thin clouds, 3 average clouds, 10 dark
  stratus** [SRC, 2, 2a]. Biberman: "heavy overcast by 10" (the cloudy-sky figure is illegible) [SRC/UNCERTAIN, 4].

## 4. The eye

### 4a. Large-target threshold contrast vs adapting luminance

These are Crumey's (2014) fits to Blackwell's 1946 data [10] [SRC], at 50 % detection with long viewing:

- Scotopic (B < 0.354 cd/m²): C∞ = 7.633·10⁻³·B^(−1/4) − 7.174·10⁻³.
- Photopic: C∞ = 2.720·10⁻³ (the Weber region, flat).
- Riccò area A_R = R/C∞. Scotopic R = (6.505·10⁻⁴B^(−1/4) − 8.461·10⁻⁴)² below 0.0708 cd/m²; photopic R =
  (1.772·10⁻⁴B^(−1/4) + 7.167·10⁻⁵)². Below A_R, C = R/A (Riccò's law).

Evaluated [INFERRED]:

| B (cd/m²) | C∞ | C∞ / C∞(30 cd/m²) | game's (1+√(10⁻⁴/B)) | Riccò radius (arcmin) | Riccò area / day's |
|---|---|---|---|---|---|
| 10⁻⁵ | 0.129 | 47 | 4.2 | 58 | 111 |
| 3·10⁻⁵ | 0.096 | 35 | 2.8 | 50 | 82 |
| 10⁻⁴ | 0.069 | 25 | 2.0 | 42 | 58 |
| 3·10⁻⁴ | 0.051 | 19 | 1.6 | 35 | 41 |
| 10⁻³ | 0.036 | 13 | 1.3 | 29 | 28 |
| 10⁻² | 0.017 | 6.2 | 1.1 | 18 | 11 |
| 0.1 | 0.0064 | 2.4 | 1.03 | 9.4 | 2.9 |
| 1 | 0.0027 | 1.0 | 1.0 | 9.3 | 2.9 |
| 30 | 0.0027 | 1.0 | 1.0 | 5.5 | 1 |

Cross-check: Taylor (Visibility Lab 1960) measured a 6° disc at 10⁻⁴ fL (3.4·10⁻⁴ cd/m²) at C_t ≈ 0.048, and at
10⁻⁵ fL (3.4·10⁻⁵ cd/m²) at ≈0.099. He reports these match Blackwell [SRC, 11]. Crumey's formula gives 0.049 and 0.093
[INFERRED].

Knees [INFERRED from the fits]: the rod/cone split for C∞ is at 0.354 cd/m² and for the Riccò term at 0.07 cd/m². The
scotopic C∞ follows B^(−1/4), not the de Vries–Rose B^(−1/2). Crumey notes that √R plotted against B^(−1/4) is "two
linear sections" [SRC]. Webvision puts the duplicity boundary at about 0.03 cd/m², rod Weber fraction 0.14, cones
0.02–0.03 [SRC, 12].

Day, by size (Taylor 1961, 75 fL ≈ 257 cd/m², 0.33 s, foveal): 1′: 0.54; 1.74′: 0.20; 3.6′: 0.049; 15′: 0.016; 120′:
0.0078 [SRC, 13].

### 4b. Point sources

- Knoll, Tousey & Hulburt 1946, abstract: for a 1′ source on fields from zero to ≈1500 cd/ft², the threshold is
  **i = 10⁻¹⁰(1 + b)^½ fc, with b in nL**, "within a factor of 3 over the entire range" [SRC, 14]. (The OCR shows "12"
  for the exponent; the form is (1+b)^½ [INFERRED].) A bend appears near 1000 nL at the foveal/extra-foveal
  transition. In lux [INFERRED]:

| Background L (cd/m²) | 0 | 10⁻⁵ | 10⁻⁴ | 3·10⁻⁴ | 10⁻³ | 10⁻² | 1 | 100 | 10⁴ |
|---|---|---|---|---|---|---|---|---|---|
| KTH lab threshold (lux) | 1.1·10⁻⁹ | 2.2·10⁻⁹ | 6.1·10⁻⁹ | 1.05·10⁻⁸ | 1.9·10⁻⁸ | 6.0·10⁻⁸ | 6.0·10⁻⁷ | 6.0·10⁻⁶ | 6.0·10⁻⁵ |
| Crumey scotopic ΔI (F=1) (lux) | – | 1.2·10⁻⁹ | 3.2·10⁻⁹ | 5.0·10⁻⁹ | 7.9·10⁻⁹ | 1.5·10⁻⁸ | – | – | – |

- The practical thresholds for lights are higher. IALA E-200-2: night **2·10⁻⁷ lux**; minor background lighting
  2·10⁻⁶; substantial background lighting 2·10⁻⁵; day 10⁻³ lux. The day value rises with sky luminance: 1.3·10⁻⁵ lux
  at 100 cd/m², 1.07·10⁻⁴ at 1000, 10⁻³ at 10,000, 4.9·10⁻³ at 50,000 [SRC, 15]. The AMS glossary says a
  dark-adapted observer "can usually see a nonflashing light that produces ∼1.5×10⁻⁷ lux" [SRC, 16]. Blondel & Rey
  (1911): a laboratory threshold of about 0.5–0.6·10⁻⁷ lux, "much lower than the commonly used figure of 10⁻⁷ lux"
  [SRC/UNCERTAIN exponent, 17].
- So the practical night value of 2·10⁻⁷ lux is about 10× KTH's laboratory threshold on a 10⁻³ cd/m² sea-horizon sky,
  and 25–60× Crumey's 50 % threshold [INFERRED]. **10 × KTH** reproduces IALA within ×2 by day and by night:
  10⁻³ cd/m² → 1.9·10⁻⁷ (IALA 2·10⁻⁷); 10⁴ cd/m² → 6·10⁻⁴ (IALA 10⁻³); 100 cd/m² → 6·10⁻⁶ (IALA 1.3·10⁻⁵)
  [INFERRED].

### 4c. Searching vs knowing where to look; detection level

- AMS glossary: "Unalerted observers may need Cthresh values five or more times higher than alerted observers"; the
  detection probability used is usually 50–99 % [SRC, 18].
- Crumey: a field factor F = 2 is notional for naked-eye stars, with a plausible range of 1.4–2.4. Blackwell's 90 %
  "confident" level is ×1.62 over 50 %; Blackwell & Blackwell's "commonsense seeing" is ×2.4; Knoll et al.'s
  adjustment method needs ×1.814 (Tousey & Hulburt proposed 2); monocular ×√2; ×6.9 for 95 % of 65-year-olds against
  50 % of 20-year-olds [SRC, 10].
- Taylor's thresholds rise steeply off-axis: 1′ at 12.5° is about 10× the foveal value [SRC, 13]. **A measured search
  factor for unknown position was not reached** beyond the AMS "5 or more".

### 4d. Dark adaptation

The rod-cone break comes at about 5–8 min. Rod sensitivity "improves considerably after 5–10 minutes", and the curve
levels off after about 40 min, near 10⁻⁵ cd/m². Cones mediate above about 0.03 cd/m² [SRC, 12]. Webvision has no
log-units-per-minute rate. Its bleaching figures are garbled [UNCERTAIN].

### 4e. Flash blindness and recovery

- Czeh, Casper & Segraves 1965 (GE for USAF SAM) regress recovery time on flash energy, target luminance and acuity
  [SRC, 19]. Recovery time rises with flash energy, then plateaus (Miller's data, about 450–1420 L·s). Brown gives a
  minimum of about 0.2 s, the reaction time. "Recovery time increases as target luminance decreases" [SRC]. The fit is
  valid for 3.2·10⁴–8.9·10⁷ td·s and target luminance 7.6·10⁻³–1.9·10⁴ mL (≥0.024 cd/m², brighter than any night
  target). Its 68 % band is 0.64–1.36 T [SRC]. Evaluated [INFERRED]:

| Target L | detail | 3·10⁴ td·s | 10⁵ | 10⁶ | 10⁷ | 9·10⁷ |
|---|---|---|---|---|---|---|
| 0.024 cd/m² | coarse (12′) | 5 s | 7 s | 21 s | 62 s | 108 s |
| 0.024 cd/m² | fine (3′) | 7 s | 10 s | 28 s | 84 s | 146 s |
| 0.95 cd/m² | coarse | 1.6 s | 2.3 s | 6.5 s | 20 s | 34 s |
| 9.5 cd/m² | coarse | 0.7 s | 1.0 s | 2.9 s | 9 s | 15 s |

- Cushman 1971 (cockpit, 2 ms flash, observers 2 min under red light): mean recovery to read an altimeter
  (0.3 mL ≈ 1 cd/m²) was 1.7 s with a 1° flash field, 3.3 s at 5° and 6.8 s at 15°. For the dimmer airspeed
  indicator it was 4.0, 33 and 44 s [SRC, 20]. The bigger the flash field, the longer the recovery.

## 5. Disability glare

- Stiles–Holladay (CIE standard observer): L_veil = 10·E_glare/θ² cd/m², with E in lux at the eye and θ in degrees
  [SRC, 21]. Validity is given as 1°–30° (CIE age-adjusted form 10(1+(A/70)⁴)/θ², [22]) or 3°–30° (LRC, [21]) [SRC].
- The CIE general glare equation, valid from 0.1° to about 100° [SRC, 23], is
  **Ψ = L_veil/E = 10/θ³ + (5/θ² + 0.1p/θ)[1 + (A/62.5)⁴] + 2.5·10⁻³p**, with A the age in years and p the
  pigmentation (0 dark, 0.5 brown, 1.0 blue-green) [SRC].
- For age 25 and p = 0.5 [INFERRED], compared with SH's 10/θ²:
  - 0.3°: 427 against 111.
  - 1°: 15.2 against 10.
  - 3°: 0.95 against 1.11.
  - 10°: 0.07 against 0.10.

  Below 1° the 10/θ³ term dominates, so 10E/θ² underestimates the veil there by ×1.5 at 1° and ×4 at 0.3°.

## Against the game's model

| Game value | Verdict | Suggested |
|---|---|---|
| −18°: 7·10⁻⁴ lux | **revise** if the starlight term is added on top. Schlyter's 6.45·10⁻⁴ already includes the night sky; the sun's term alone at −18° is ~10⁻⁵ (USNO 6.6·10⁻⁶). | twilight term −15°: 2·10⁻⁴; −18°: 1·10⁻⁵ (or 0), plus the night term |
| −12°: 8·10⁻³ | confirmed (Schlyter 8.1·10⁻³; USNO 4.4·10⁻³ + 5·10⁻⁴) | 6·10⁻³ (either is within the sources' spread) |
| −9°: 0.1 | confirmed (0.12, 0.116) | 0.12 |
| −6°: 3 | confirmed (3.4, 3.0; Koomen 1.35) | 3 |
| −3°: 60 | confirmed (75, 66; Koomen 43) | 70 |
| 0°: 500 | **revise** (759, 757) | 750 |
| +5°: 6,000 | **revise** (4,540, 4,090) | 4,500 |
| +10°: 15,000 | **revise** (11,100, 11,100) | 11,000 |
| Starlight + airglow 10⁻³ lux | confirmed (1.1·10⁻³ Biberman; 0.9–1.1 mlx measured; models 5–8·10⁻⁴) | 10⁻³ (×0.5–2 by solar cycle and site) |
| Full moon overhead 0.25 lux | confirmed (0.23–0.27 without the surge; 0.32–0.38 with it, within about a day of full) | 0.27, ×1.35 within ±7° of full; phase by K&S eq. 9 (quarter 0.09 of full) |
| Overcast passes 0.15 | roughly confirmed; the sources split by cloud type | thin 0.5, average 0.33, thick/stratus 0.1; a moonless overcast night ≈ 1–3·10⁻⁴ lux |
| Horizon L = E/π, mean of the arch | **revise**: the horizon is 4–7× E/π in twilight (azimuth mean) and ≈2–3× E/π on a moonless night | horizon_mean ≈ 5·E/π in twilight, ≈2.5·E/π at night |
| von Mises e^(1.5 cos Δ)/I₀(1.5): away = 1/20 of toward | **revise**: measured toward/away is 4–5 at sunset, 8 at −3°, 11 at −6° and −9°, 6 at −12°, 3 at −15°. The shape is a glow plus a floor, not e^(k cos Δ). | L(Δ) = (E/π)·[F + G·e^(3.2(cos Δ − 1))] with F ≈ 2 and G ≈ 20 for −3° to −12°. G falls toward night (−15°: toward/away 3); at sunset k ≈ 5, F/G ≈ 0.3. If a single e^(k cos Δ) is kept: k ≈ 1.2 (−3° to −9°), 0.9 (−12°), 0.5 (−15°). |
| Arch forming as the sun goes 0–6° down | **revise**: it is there before sunset (5:1 at +5°, 4:1 at 0°), strongest at −6° to −9°, fading by −15° | peak asymmetry between −5° and −10° |
| Eye: C ×(1+√(L_k/L)), L_k = 10⁻⁴ cd/m² | **revise strongly**: Blackwell (Crumey's fit, Taylor's check) gives ×25 at 10⁻⁴, ×13 at 10⁻³, ×6 at 10⁻², not ×2/×1.3/×1.1 | C∞ = max(0.0027, 0.00763·L^(−1/4) − 0.00717); or in the game's form C/C₃₀ = 1 + (0.2/L)^0.43 (within ×1.7 over 10⁻⁵–30 cd/m²) [INFERRED fit] |
| Summation knee 0.2 cd/m², Riccò area ×100 at night | roughly confirmed: the area is ×3 at 0.1–1, ×11 at 10⁻², ×28 at 10⁻³, ×58 at 10⁻⁴, ×110 at 10⁻⁵ | knee ≈ 0.1–0.3; ×50 at 10⁻⁴ cd/m² |
| Ship's inherent contrast 0.35 by day, 0.6 at night | **no evidence fetched** | at night a ship lit only by the sky is a silhouette: with a reflectance of 0.1–0.2 and vertical illuminance ≈ π·L_sky/2, C ≈ −0.9 against the horizon sky [EST]; ~0.5–0.7 against the sea just below the horizon (sea luminance ≈ 0.3–0.6 of the sky's at grazing angles [EST, not sourced]). Keep 0.6 against the sea, use ≈0.9 against the sky |
| Point light 2·10⁻⁷ lux at the eye (Allard), rising as √(sky L) | confirmed: IALA 2·10⁻⁷ at night, AMS 1.5·10⁻⁷, and KTH's law is √(1+b) | E_t = 1.1·10⁻⁸·√(1 + L/3.2·10⁻⁶ cd/m²) lux (= 10× KTH): 1.9·10⁻⁷ at a 10⁻³ cd/m² sky, 6·10⁻⁴ by day (IALA 10⁻³) |
| Glare 10·E/θ², θ ≥ 1° | confirmed for 1–30°; for θ < 1° use the CIE general equation | Ψ = 10/θ³ + 5.1/θ² + 0.05/θ + 0.0013 (age 25, p = 0.5), θ ≥ 0.1° |
| Own gun-flash dazzle τ = 1 s | **probably too short**. The flash-blindness data give 2–7 s to read a ~1 cd/m² target after the weakest flashes tested and 5–20 s for a 0.02 cd/m² target, and it grows with flash field size and with darker targets. Night targets (10⁻³ cd/m²) lie below the data, so longer still. | τ ≈ 3–10 s for a night target after a near flash, longer (tens of s) for a big, bright flash field [EST]; the gun flash's retinal energy at the bridge was not found |

## Not reached / open

- Brown 1952 (BuShips 374-1), Bond & Henderson 1963, Rozenberg 1966 and Hulburt 1953: not read. The search budget ran
  out (the session's web-search limit) before Rozenberg's and Hulburt's azimuth data and Brown's curves could be
  found.
- Blackwell's original 1946 table (all target sizes 0.6′–360′ by luminance): only through Crumey's fit and Taylor's
  check. Small-target night thresholds are only via Crumey's Riccò term.
- How moonlight and the night sky fall through measured cloud types (only the USNO divisors and Biberman's ×10).
- The night sky's dependence on latitude; the solar-cycle swing from one site over time (only cross-site).
- A measured searching vs known-position factor at night (only the AMS "5 or more" and Crumey's F).
- Photometry of naval gun flash at the eye (lux·s or td·s), to place the flash on Czeh's curve.
- The sea's luminance below the horizon at night (needed for the hull's contrast against the sea).
- The Koomen table decoding (the 0.0ₙ notation) should be checked against a clean scan; it is self-consistent here.

## Sources

1. P. Schlyter, "Radiometry and photometry in astronomy", §10–11 tables. https://stjarnhimlen.se/comp/radfaq.html
2. P. M. Janiczek & J. A. DeYoung, *Computer Programs for Sun and Moon Illuminance With Contingent Tables and
   Diagrams*, USNO Circular 171, 1987. https://apps.dtic.mil/sti/pdfs/AD1112511.pdf
   2a. `skylight` R package (its transcription): https://search.r-project.org/CRAN/refmans/skylight/html/skylight.html
   2b. its code: https://raw.githubusercontent.com/bluegreen-labs/skylight/master/src/skylight.cpp and
   https://raw.githubusercontent.com/bluegreen-labs/skylight/master/src/subroutines.h
3. M. J. Koomen, C. Lock, D. M. Packer, R. Scolnik, R. Tousey, E. O. Hulburt, "Measurements of the Brightness of the
   Twilight Sky", JOSA 42(5), 353–356, 1952.
   https://static1.squarespace.com/static/54694fa6e4b0eaec4530f99d/t/5e876886cdfbaa441f538327/1585932427823/Measurements+of+the+Brightness+of+the+Twilight+Sky+1952.pdf
4. L. M. Biberman, L. Dunkelman, R. G. Finke et al., *Levels of Nocturnal Illumination*, IDA Research Paper P-232,
   1966. https://apps.dtic.mil/sti/pdfs/AD0632918.pdf
5. F. Patat et al., "UBVRI twilight sky brightness at ESO-Paranal", A&A 2006. https://arxiv.org/pdf/astro-ph/0604128
6. A. Jechow, F. Hölker, C. Kyba, "Using all-sky differential photometry to investigate how nocturnal clouds darken the
   night sky in rural areas", Sci. Rep. 9, 1391 (2019). https://pmc.ncbi.nlm.nih.gov/articles/PMC6361923
7. MoonShineR package manual. https://lokpoon.r-universe.dev/MoonShineR/doc/manual.html
8. F. Patat, "UBVRI night sky brightness during sunspot maximum at ESO-Paranal", A&A 2003, Table 5.
   https://www.aanda.org/articles/aa/full/2003/12/aa3223/node7.html
9. K. Krisciunas & B. Schaefer, "A model of the brightness of moonlight", PASP 103, 1033 (1991).
   https://cta-redmine.irap.omp.eu/attachments/download/3619/1991PASP__103_1033K.pdf
10. A. Crumey, "Human contrast threshold and astronomical visibility", MNRAS 2014. https://arxiv.org/pdf/1405.4209
11. J. H. Taylor, "Visual Contrast Thresholds for Large Targets, Part I: Low Adapting Luminances", SIO Ref. 60-25, 1960.
    https://misclab.umeoce.maine.edu/education/VisibilityLab/reports/SIO_60-25.pdf
12. M. Kalloniatis & C. Luu, "Light and Dark Adaptation", Webvision.
    https://www.webvision.pitt.edu/book/part-viii-psychophysics-of-vision/light-and-dark-adaptation/
13. J. H. Taylor, "Contrast Thresholds as a Function of Retinal Position and Target Size for the Light-Adapted Eye",
    SIO Ref. 61-10, 1961 (and 63-3 data supplement).
    https://misclab.umeoce.maine.edu/education/VisibilityLab/reports/SIO_61-10.pdf ;
    https://misclab.umeoce.maine.edu/education/VisibilityLab/reports/SIO_63-3.pdf
14. H. A. Knoll, R. Tousey, E. O. Hulburt, "Visual Thresholds of Steady Point Sources of Light in Fields of Brightness
    from Dark to Daylight", JOSA 36(8), 480 (1946), abstract. https://opg.optica.org/josa/abstract.cfm?uri=josa-36-8-480
15. IALA Recommendation E-200-2, Luminous range. https://www.ancientportsantiques.com/wp-content/uploads/Documents/ETUDESarchivees/IALA%20Navguide/IALA-E200-2.pdf
16. AMS Glossary, "Threshold illuminance". https://glossarytest.ametsoc.net/wiki/Threshold_illuminance
17. A. Blondel & J. Rey (1911), tr. Davidson & Hardy, MIT IL E-1492, 1964. https://ibiblio.org/apollo/Documents/19780070349_1978070349.pdf
18. AMS Glossary, "Threshold contrast". https://glossary.ametsoc.org/wiki/Threshold_contrast
19. R. S. Czeh, A. W. Casper, E. C. Segraves, *A Mathematical Model of Flashblindness*, GE for USAF SAM, 1965.
    https://apps.dtic.mil/sti/pdfs/AD0627332.pdf
20. W. H. Cushman, "Effect of Flash Field Size on Flashblindness in an Aircraft Cockpit", Aerospace Med. 42, 1971.
    https://apps.dtic.mil/sti/pdfs/AD0742864.pdf
21. LRC, "Glare basics". https://www.lrc.rpi.edu/programs/Transportation/pdf/glareBasics.pdf
22. Vos 2003, "On the cause of disability glare..." (summary page). https://umcg-anes.netlify.app/publications/vos03
23. "Skyglow inside your eyes: intraocular scattering and artificial brightness of the night sky" (quotes the general
    glare equation, after van den Berg). https://arxiv.org/pdf/2212.09103
24. J. C. Keating, *Night-Sky-Background Conditions and Launch Windows*, NASA TM X-1772, 1969 (consulted; not used
    for values). https://ntrs.nasa.gov/api/citations/19690013604/downloads/19690013604.pdf
25. Blackwell & Moldauer, *Detection Thresholds for Point Sources in the Near Periphery*, ERI 2455, 1958 (consulted;
    tables illegible). https://apps.dtic.mil/sti/pdfs/AD0759739.pdf
