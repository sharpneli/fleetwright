# Night fighting without radar, 1890-1925
Status: draft    Updated: 2026-10-29    Request: [REQUESTS.md "Night fighting, 1890-1925 first"](../REQUESTS.md) (the plan: [docs/battle/night.md](../../docs/battle/night.md))

*How navies saw, lit, identified and fought each other at night before radar, as numbers by navy and period, written
for night.md's Q-steps and their [EST]s. The research ran in eight strands; each strand's full findings, case tables and
source list are kept as appendices in [night-fighting/](night-fighting/) and this note cites them by strand and source
number: [A2] is source 2 of appendix A. The strands: [A searchlights](night-fighting/a-searchlights.md),
[B star shell](night-fighting/b-star-shell.md), [C1 lookouts and glasses](night-fighting/c1-lookouts-and-glasses.md),
[C2 the night's light and the eye](night-fighting/c2-night-light-and-the-eye.md),
[D identification and doctrine](night-fighting/d-identification-and-doctrine.md),
[E1 cases 1890-1905](night-fighting/e1-cases-1890-1905.md), [E2 Jutland's night](night-fighting/e2-jutland-night.md),
[E3 cases 1911-1918](night-fighting/e3-cases-1911-1918.md). Section 7.4 checks the sim's fitted sight law against the
record with [night_ref.py](night_ref.py), a sibling of [sighting_ref.py](sighting_ref.py).*

*Primary texts (the 1920 Jutland Official Despatches, Fawcett and Hooper, Jellicoe, Scheer, Corbett and Newbolt, Bacon,
Evans, Semenov, Novikoff-Priboy, Klado, Wilson's Ironclads in Action, the USN's 1918 Searchlights and Signal Lights,
Proceedings USNI 1894-1946, Koomen et al. 1952) were mostly read through archive.org's full-text search: numbers came
through intact, wording is near-verbatim. The session's shared web-search budget ran out in every strand, so several
leads stay open (see "Open questions"). Sun and moon for every case were computed with PyEphem
([night-fighting/case_light.py](night-fighting/case_light.py)).*

Tags: **[SRC]** a cited text says it; **[INFERRED]** reasoning or a fit from sourced facts (all computed sun and moon
figures are this); **[EST]** an estimate with no direct source; **[UNCERTAIN]** thin, secondary or conflicting.

## Summary

1. **Night ranges follow the light, and were short.** On dark nights destroyers met at point blank: under a cable to
   600 yd in the Dover Strait raids of 1916-18, launch ranges of 50-650 yd against ships at anchor (1891-1904), under
   600 yd at Tsushima. Jutland's night, a new moon with the sun never below −11.5° (nautical twilight all night) and
   mist, was fought at 150-2,000 yd, typically 600-1,500, with hulls lost beyond about ¾ mile; at −9° lights went on at
   2,500 yd; at −6° to −5° (01:45-02:10) torpedoes went at 1,700-3,500 yd; at −3°, 4,000 yd. Japanese lookouts with
   12 cm glasses in 1942 sighted at 2.7-4.6 km on dark nights. **[SRC]** cases, **[INFERRED]** the light (§5, §7.1)
2. **The sim's night is too generous in twilight and moonless dark, by ×2-5 in range.** Its eye law rises far less in
   the dark than Blackwell's measured thresholds (×1.4 against ×15 on the large-target threshold, a 10⁻³ cd/m² sky through a 7×50),
   while its summation term grows far more. Run through the same sight model, the record implies the sim's threshold
   should be ×1.5 higher under a black overcast, ×3 on a clear moonless night and ×4-5 in Jutland's twilight. A law with
   Blackwell's shape, a ship's night contrast of 0.9 against the sky and a field factor easing below a 3·10⁻³ cd/m² sky
   puts every anchor inside the record's band and leaves the day as it was (§7.4). **[INFERRED]**
3. **The twilight sky is a glow toward the sun over a flat floor.** The horizon away from the sun is about 11× darker
   than toward it at −6° to −9°, 6× at −12° and 3× at −15° (the sim: 20×), and the horizon's mean is about 5× E/π in
   twilight and 2.5× E/π at night (the sim: 1×). Level illuminance, starlight (1.0 mlx), the full moon (0.27 lux) and
   an overcast's cut are confirmed. **[SRC]** Koomen et al. 1952, Schlyter, Jechow 2019, Krisciunas and Schaefer 1991
4. **At Jutland the British looked toward the light.** The Germans came from the W to NW of the British rear, so
   British lookouts saw them against the brighter northern sky ("silhouetted against the evening sky … while we
   remained in darkness", Nottingham) and usually saw first. The Germans still struck first: lights trained before
   exposure, part of the British challenge known, secondaries firing within seconds. The bench has the Germans seeing
   first by looking north; the cases' geometry wants a check (§5.3). **[SRC]** reports, **[INFERRED]** geometry
5. **Showing a light got a ship hit; keeping dark kept her safe.** Lit ships drew fire within seconds and torpedoes
   within minutes (Retvizan and Nakhimov 1904-05, Flirt and Llewellyn 1916-17, Southampton, Tipperary, Black Prince),
   while dark ships in the same line were left alone (Nottingham and Birmingham beside Southampton; Nebogatov's
   division at Tsushima; Diana on the Yellow Sea night). Fixed shore lights with guns behind them were the exception:
   they held attackers off (Port Arthur's "wall of light" kept destroyers past 3 miles; Santiago; the Dardanelles).
   **[SRC]**
6. **Searchlights, 1890-1918:** a plain carbon arc's crater gave 1.6-1.8·10⁴ cd/cm², so a 90 cm or 36-inch beam peaked
   near 8-9·10⁷ cd with a 2.5-3° spread; the high-intensity arc (Beck in USS Texas 1914, USN specifications 1917,
   Sperry 1918, France 1917; the German navy kept the yellow plain arc) gave 5-9·10⁴ cd/cm² and about twice the range.
   The RN's pre-war dreadnoughts carried twin 24-inch projectors; 36-inch lights and searchlight control gear came from
   mid-1915; Russia's pre-dreadnoughts had six 75 cm Mangin lights with electric remote control. Useful range: a plain
   36-inch lit a target well to 3,000 yd and to about 4,000 with glasses; a high-intensity one to 6,000 and 8,000.
   Motor-trained remote control already existed in 1894, with a search sweep of 1.5°/s. **[SRC]** USN 1918, Proceedings
   1894, TDP
7. **Star shell:** the Germans used it at Jutland, to find targets as well as to light them, star shell first and then
   searchlights already trained; the 15 cm Leuchtgeschoss weighed 37 kg and was fired at 500 m/s (HE: 835). The RN had
   none in 1916 and issued a copy of the German shell to all ships during 1917; the USN had 3-5-inch parachute star
   shell by 1922 (800,000 cp claimed, about 30 s, bursting about 1,000 ft above the target); the Russians had it before
   1916. Burst heights of 300-370 m are sourced; a non-parachute star burned about 10-15 s; the "burst beyond the target"
   rule, the candle's intensity and the dud rate were not found for 1916. **[SRC]**
8. **The challenge gave the challenger away.** Every British challenge at short range at Jutland was answered by
   searchlights and fire within seconds; Paragon was torpedoed before her challenge was finished (1917); Borea was shot
   up as she made her recognition signal at 1,000 m (Otranto). The Germans had the first two signs of the British
   challenge of the day; Flirt got her own challenge echoed back (1916); Lynx passed three German cruisers by copying a
   German reply (1914). Identification was settled at 150-2,000 yd, median 600-800 yd. **[SRC]**
9. **Holding fire on unknowns was the British norm and cost them.** Six of seven British battleships that met German
   ships after midnight at Jutland neither fired nor reported, one after a wrong reply to her challenge; in the Dover
   raids patrols took unanswered challenges for friends. After Jutland the RN ordered screens to fire on every ship that
   approached, without challenging, where the enemy was expected; Bacon drew the same lesson. Raiders never challenged
   first. **[SRC]**
10. **Night torpedoes hit ships at anchor, rarely ships under way.** Hits per torpedo: about 1 in 2.5 at Caldera (1891),
    1 in 5 at Port Arthur's first wave, 1 in 7 at Weihaiwei; under way 1 in 9-13 at Tsushima (54 torpedoes plus 32
    linked mines for 4-6 hits), about 1 in 14 for the 4th Flotilla and 1 in 17 for the 12th at Jutland (British night
    total about 37 for 3), 0 in 17 against Oleg and Aurora. Attackers lost more boats to rocks, booms and collisions than
    to guns; Jutland's night had four collisions in one flotilla action and two of its eleven losses were by collision.
    **[SRC]** counts, **[INFERRED]** rates
11. **Raiders chose the dark.** Every Dover raid of 1916-18 came on a moonless or moon-set night; the 1891-1905
    attacks came after moonset or before moonrise. **[INFERRED]** from computed moons and **[SRC]** Bacon
12. **Doctrine, written and done:** the RN of 1914-16 avoided a fleet night action by design (Jellicoe), stationed its
    destroyers astern so its battleships would not fire on them, and had poor lights, no star shell and almost no
    recognition system ("Ours is practically nil"); the Germans trained for night action to "a remarkably high
    standard"; Russian ships at Tsushima that searched with their lights were torpedoed. **[SRC]**

## 1. Searchlights

### 1.1 The technology line

| Date | What | Numbers | Tag |
|---|---|---|---|
| 1876 | Mangin's silvered-glass lens-mirror | small effective angle | [SRC] [A1] |
| 1886 | Schuckert's parabolic glass mirror | collects 145°; beam "less than 3 degrees" | [SRC] [A1][A3] |
| 1894 | Mangin outnumbers Schuckert at sea about ten to one; Schuckert divergence 3° | 90 cm: 80-120 A at 45-55 V (3.6-6.6 kW at the arc), arc ~40,000 cp | [SRC] [A2] |
| 1894 | Motor training already in use | two motors on the pedestal, worked from a switchboard at a distance | [SRC] [A2] |
| to ~1914 | Plain carbon arc | crater ~180 cp/mm² = 1.8·10⁴ cd/cm² | [SRC] [A7] |
| 1914 | Beck high-intensity arc in USS Texas | "twice the range of the old type" | [SRC] [A3] |
| 1917-18 | USN 36-inch high-power spec; Sperry's arc | ≥500 cp/mm² (5·10⁴ cd/cm²) spec; Sperry ~900 cp/mm² | [SRC] [A1][A8] |
| 1917 | France adopts Sperry | — | [SRC] [A9] |
| WWI | German navy rejects Beck | its blue-white light did worse than yellow in Baltic mist | [SRC, secondary] [A3] |

### 1.2 By navy and year

| Navy | Period | Fit | Tag |
|---|---|---|---|
| Continental (general) | 1890s | 90 cm the battleship light (Schuckert, "45,000-candlepower" arc, "more than 3,500 m"); 60 and 40 cm smaller | [SRC] [A2][A3] |
| Italy | 1894 | "not less than six" on large ships | [SRC] [A2] |
| USN | to 1907 / 1907-10 / 1910-13 / 1914 | 6 / 8 × 36-in / 16 (a trial) / 8 a battleship or armoured cruiser, 2-4 a protected cruiser, 1-2 a gunboat; distant electric control in the 1914 spec | [SRC] [A4] |
| USN | 1914-18 | 36-in high-intensity, 8 a battleship; tied into fire control by 1918 | [SRC] [A1][A3] |
| Russia | 1898-1904 | 6 × 75 cm Mangin, electric remote control, in pairs (fore bridge, amidships, after bridge or mainmast); arcs 180-220°; 40 cm on boats | [SRC] [A19-21] |
| RN | to 1906 | 24-inch, landed from the tops to the shelter deck in 1906 | [SRC] [A12] |
| RN | 1908-1914 | twin 24-inch mountings (St Vincent, Colossus, Iron Duke), not 36-inch | [SRC] [A13][A14] |
| RN | mid-1915 on | 36-inch on foremast-strut platforms and searchlight control gear (Dreadnought, June 1915); four 36-in over a control position (Aug 1917) | [SRC] [A16] |
| RN | WWI | Iron Duke's "coffee box" protectors: lights lowered into them in action | [SRC] [A17] |
| Germany | 1916 | iris shutters for fast exposure; lights trained before switching on; "very powerful (much more so than ours)" (Jellicoe) | [SRC] [A18][E2-8] |
| Germany, Japan, France, Italy, A-H | 1904-18 | class counts and sizes **not found** (the sim's German 110 cm × 8 is unconfirmed) | — |

### 1.3 Beam, power, crew

- **Beam peak.** No measured beam candlepower for an 1890-1918 naval projector was found. The plan's rule (crater ×
  mirror face × 0.9 × 0.85) on the sourced craters gives 8-9·10⁷ cd for a plain 36-inch or 90 cm and 2.5-4.5·10⁸ for a
  high-intensity 36-inch; WWII rated beams (German 150 cm 9.9·10⁸, US 60-in 8·10⁸) imply 5.7-7.3·10⁴ cd/cm². **[INFERRED]**
  [A1][A7][A8][A22][A23]
- **Spread.** Plain arc 3° (1894, 1886) **[SRC]**; high intensity "very small dispersion", no figure; by the crater's
  size about ½-⅔ of the plain arc's **[INFERRED]**. Lights were deliberately spread for search: 7° on the sweeping
  ships, 20° on the rear ships' and harbour fans (1894) **[SRC]** [A2].
- **Power.** Plain 90 cm 3.6-7.2 kW at the arc; USN 36-in high-intensity 150 A × 75 V ≈ 11 kW at the arc, ≈ 18-19 kW from
  the 120-125 V bus (ballast); 24-in HI 75 A × 55-60 V. Carbons for 2.5 h (1917 spec). **[SRC]** [A1][A2][A10]
- **Arc kept burning behind the shutter.** USN 1918: iris shutters on every light, the arc to stand 20 min burning
  behind the closed shutter; so exposure is a shutter's work, not a strike **[SRC]/[INFERRED]** [A1].
- **Weight and crew:** no naval figure. A coast 36-inch with its stand weighed 4,000 lb; army 60 cm lights had crews of
  three **[SRC]** [A6][A22].

### 1.4 Control, sweeping, exposure

- 1894: a fleet's cruisers each swept a 90° sector once a minute at **1½°/s**, every point lit 4½ s a minute;
  operators needed "several years of drill" **[SRC]** [A2].
- 1894: "the observer's line of sight must make an angle of from 25° to 60° with the axis of the projected beam":
  the operator sees worse than an eye off the beam (backscatter) **[SRC]** [A2]. WWII army control stations stood
  several hundred feet from the light **[SRC]** [A31].
- USN 1914: distant electric control; the light could oscillate 2-40° in train every 10 s (up to ~8°/s) **[SRC]** [A4].
  1917: 3°/min to 360°/min distant training **[SRC]** [A1].
- RN 1914-16: hand-worked "to orders received through voice pipes", "never really efficient"; Jellicoe: "neither our
  search-lights nor their control arrangements were at this time of the best type" **[SRC]** [C1-18][D3].
- German 1916: "Searchlights were already trained on the ship before being switched on"; star shell first, then the
  lights, then fire within seconds **[SRC]** [B1][D1].

### 1.5 Useful range

| Light | Range | Tag |
|---|---|---|
| 90 cm plain, 1894, best conditions | detects "up to 3500 meters"; betrays the fleet "far beyond" | [SRC] [A2] |
| British army 90 cm DEL, 1890s | 3,650 m | [SRC] [A10] |
| 36-in plain arc (USN 1918) | good light to **3,000 yd (2.7 km)**, ~**4,000 yd (3.7 km)** with binoculars; rangefinder readings "not much over 2,000 yards" | [SRC] [A1] |
| 36-in high intensity (USN 1918) | plainly seen at **6,000 yd (5.5 km)**, plain with binoculars at **8,000 yd (7.3 km)** | [SRC] [A1] |
| A ship's own lights against torpedo craft, 1904 | "about 1.5-2 miles, depending on the weather" (Semenov) | [SRC] [E1-18] |
| Haze | the Russian lights made "only a luminous mist" in haze (1905); 5× beam gives +50% range in clear air, +15% in medium (1948) | [SRC] [E1-18][A26] |
| WWII USN doctrine | effective at 4,000-6,000 yd; star shell seen farther and lights more | [SRC] [B26] |

Lights went on in action at 150 yd to about 2,500 yd at Jutland (§5.3) **[SRC]**.

### 1.6 Damage

Southampton's searchlight crews lost 6 killed and 14 wounded of about 33 in the 3½-4 minutes, "a shell burst that
carried the lights away" **[SRC]** [E2-7][A28]. Oldenburg's forward light was hit by a 4-inch (4 officers killed);
Rheinland's searchlight cables were cut; Broke's after light was knocked out (1917) **[SRC]** [E2-1][E2-17][E3-6].
At Tsushima the Japanese quick-firers destroyed Russian lights (Oryol lost all six to splinters) so that crippled ships
had none against the night's attacks **[SRC]** [A29][E1-20]. The USN 1918 spec mounted the front door on springs
against breakage by its own guns' blast **[SRC]** [A1]. The Dardanelles' fortress lights were made to flicker by ships'
fire but never hit **[SRC]** [E3-20]. No rate of loss was found.

## 2. Star shell

### 2.1 By navy and year

| Navy | Round | From | Light, burn, fall, burst | Tag |
|---|---|---|---|---|
| Germany | 15 cm Lg L/3,6: 37.0 kg at 500 m/s (HE 45.3 kg at 835); 10.5 cm Lg L/3,3: 12.3 kg at 450 m/s; 8.8 cm Lg 9.4 kg at 650 m/s | in fleet use 31 May 1916; the 10.5 and 8.8 cm rounds' dates uncertain | not found; one Jutland flare "lasting quite 15 seconds" [UNCERTAIN it was a star shell]; one "opened up like a parachute" | [SRC] [B1][B5-9] |
| Germany (army) | 7.7 cm Leuchtgeschoss | 1914 | max fuze 28.8 s | [UNCERTAIN] [B16] |
| Krupp (export) | "searchlight shell", 6 stars | 1913 | 45 s to several minutes; best height 300 m | [SRC] [B17] |
| Russia | 130 mm star shell "made before 1916"; 102 mm star shell 15.08 kg | ≤1916 | — | [SRC] [B10][B11] |
| Russia (army, Port Arthur 1904) | star shells with searchlights | 1904 | "for a few moments", stars "sinking slowly" | [SRC] [B18] |
| RN | none at Jutland ("at that time unfamiliar to us"); a copy of a German shell found ashore | **issued "during 1917 to all our ships"**, Dover and Harwich first; fired by Botha and Terror 21 Mar 1918 | — | [SRC] [B2][B4][E2-8] |
| USN | 3-, 4- and 5-inch parachute star shell | by March 1922 | 800,000 cp, ~30 s, burst ~1,000 ft above the target [SRC, popular press] | [B12] |
| USN (WWII check) | 5"/38 star shell | — | ~50 s | [SRC] [B21] |
| Japan, France, A-H, Italy | — | not found for 1890-1925 | — | — |

Other illuminants for scale: WWI parachute aircraft flares ≥350,000 cp for 7 min; non-parachute stars "about ten
seconds", parachute stars "less than a minute" (Luckiesh 1920) **[SRC]** [B17][B20].

### 2.2 How it was used

- **German, Jutland:** from dusk (first seen 20:45-21:30 GMT) through the night. "On each occasion German ships fired
  a white star shell … before switching on their searchlights"; "their use of star shells rendered a surprise torpedo
  attack almost impossible"; destroyers fired it "preparatory to torpedo attack"; Jellicoe: of the greatest use "in
  locating our destroyers without revealing their own positions". So: **to search and find, then the searchlight holds
  the target for the guns.** One lit "an area of about 1,000 yards". Fired by light cruisers, destroyers and the battle
  cruisers; Thüringen's 8.8 cm before and after engaging Turbulent. **[SRC]** [B1][E2-8][E2-19]
- **Placement:** heights 300 m (Krupp 1913), ~305 m above the target (USN 1922), ~366 m (Italy, WWII) **[SRC]**. No
  source states the burst-beyond-the-target rule before WWII; the USN's 1944 rule that bursts short "ruin the
  illumination" implies it **[SRC]/[INFERRED]** [B26].
- **Keeping a target lit (WWII check):** Fletcher at Ormoc, 1944: three 5-inch guns, 92 star shell in about 7 minutes
  (~4 rounds a gun a minute); the first salvos lit nothing **[SRC]** [B27].
- **Failures:** Botha's first star shell "nothing could be seen"; star shell showed the British approach to the German
  boats off Dunkirk (it gives the firer's direction away) **[SRC]** [B4]. No dud rate found.
- **Outfit share (WWII check):** capital-ship and cruiser secondaries 2-6% star shell; destroyers 15-20% (USN pre-war)
  **[SRC]** [B15][B28].

## 3. Seeing in the dark

### 3.1 Glasses by navy and year

| Navy | Glass | From | Exit pupil | Tag |
|---|---|---|---|---|
| All | Galilean field glasses (low power, small field) the norm before Zeiss's prism binocular | to ~1894-1905 | — | [SRC] [C1-2] |
| Germany | Hensoldt 12×50 "Marine" glasses | 1902-05 | 4.2 mm | [SRC] [C1-1] |
| Germany | Zeiss D.F. **7×50** ("the most successful Navy glass") and 6×42; Voigtländer 7×50 | start of WWI | 7 mm | [SRC] [C1-1] |
| RN | Busch Terlux **10×45**, "popular in the British Royal Navy" | WWI | 4.5 mm | [SRC] [C1-1] |
| RN | Barr & Stroud 7×50 (CF30) | 1932 | 7.1 mm | [SRC] [C1-4] |
| USN | Mark X telescope 8× (2.5 mm exit pupil: "would cut off 75% of the light that the eye could use at night"); the designer took a 5 mm dark pupil and called for ≤8-9× at night | ≤1914 | 2.5 mm | [SRC] [C1-3] |
| IJN | 12 cm Type 88 binoculars; lookouts selected for night vision | 1932 (out of era) | — | [SRC] [C1-6] |

An uncoated 7×50 transmits 57.8% (coated: 75%) **[SRC]** [C1-8]. "Inferior British night glasses at Jutland" is not
documented; Jellicoe's list of German advantages names lights, star shell, recognition and fire methods, not glasses
**[UNCERTAIN]**.

### 3.2 Lookout practice (mostly WWII manuals, the principles older)

Dark adaptation 30 min (red goggles), never a white light; look 10° off the object; sweep in 10-15° jumps with
pauses; "a ship which happens to cross between you and the moon will be silhouetted"; "a ship crossing the path of
reflected light will appear as a dark object" **[SRC]** [C1-11][C1-12]. Unprotected sun in the day doubled
dark-adaptation time and "reduced effective range by one half" **[SRC]** [C1-13]. In 1914 RN gunlayers were "blinded
by the flash of firing" and often lost the target **[SRC]** [C1-18].

### 3.3 First sighting and engagement ranges by condition

| Condition | Case | Range | Tag |
|---|---|---|---|
| Black overcast or "pitch dark", moonless | Dover 26-27 Oct 1916; Downs 23 Nov 1916 (6 boats passed "at less than a cable", known by paint and forecastles); 25 Feb 1917 (overcast, moon hidden); Dover 20-21 Apr 1917 (fire before sighting; funnel flares seen 10 s later; last torpedo at 200 yd) | under 1 cable to ~600 yd | [SRC] [E3-4][E3-5][E3-6] |
| "Dark night", column | the ship ahead seen to keep station only to ~700 yd | 640 m | [SRC] [C1-27] |
| Clear, moonless | Port Arthur 8 Feb 1904: attackers unknown until torpedoes exploded, launch ~650 yd; Tsushima: ships "passed within 2-3 cables unseen", attacks at ≤600 yd; 23 Jan 1917 "fine and clear": both sides fought by funnel flare, "very close" | 200-650 yd | [SRC] [E1-9][E1-20][E3-5] |
| Clouded full moon | Caldera 1891: two torpedo gunboats seen at 2,000 yd from an ironclad at anchor | 1.8 km | [SRC] [E1-1] |
| Bright moon in fog patches | Dogger Bank 1904: trawlers in the searchlights at ~5 cables taken for torpedo boats | 0.9 km | [SRC] [E1-20] |
| Jutland, sun −9° to −10°, mist | Castor: lights at 2,500 yd, fire at 2,000; 2nd LCS: five ships at "scarcely 1,500 yards" / "not more than 2 miles" (Nottingham, silhouetted) | 1.4-3.2 km | [SRC] [E2-4][E2-7] |
| Jutland, sun −11° to −12°, mist | 4th Flotilla, Black Prince, Petard: 150-2,000 yd, typically 600-1,500; "her hull could not be seen more than three-quarters of a mile away" | 0.14-1.8 km | [SRC] [E2] §2 |
| Jutland, sun −8° to −5°, mist | 12th Flotilla: battleships sighted, torpedoes at 1,700-3,500 yd; "too light for searchlights"; Maenad 4,000-5,000 | 1.6-4.6 km | [SRC] [E2-1] |
| Jutland, sun −3° | Moresby: four battleships at 4,000 yd | 3.7 km | [SRC] [E2-5] |
| Pre-dawn twilight, sun −15° | Premuda 1918: a battleship squadron's smoke at 6.5 mi; the MAS unseen to 300 m | 12 km / 0.3 km | [SRC] [E3-17][E3-18] |
| WWII, dark, elite lookouts | Solomons 1942: "almost always within ten thousand yards; often within five thousand"; Cape Esperance 5,000; Guadalcanal I 3,000 | 2.7-4.6 km | [SRC] [C1-30] |

### 3.4 What showed first

- **A hull against a brighter sky** (twilight, moon, a fire, a flare): the commonest first sight at Jutland
  ("silhouettes"; Petard saw "crane derricks … against the sky" at ~600 yd and knew her for German) **[SRC]** [E2-7].
- **Funnel flare:** Swift identified the Germans by "flares from the funnels of five or six boats" 10 s after they
  fired (1917); on 23 Jan 1917 "neither side used searchlights, since each was visible from the other's funnel flare";
  German boats at Jutland were told to keep sparks down **[SRC]** [E3-5][E3-6][C1-10]. These were oil-fired destroyers
  at high power, so flare is not only a coal-forcing effect **[INFERRED]**.
- **Phosphorescence** in the bow wave, the wash and the wake: "it is still possible to detect ships by the
  phosphorescence they stir up" **[SRC]** [C1-11]. Agincourt saw Seydlitz's "track very visible" **[SRC]** [E2-2].
- **Lights and signals:** a masthead light shown by mistake (Shannon, seen by Hannover); challenges and recognition
  lights; searchlights; gun flashes ("Nothing could be seen of the enemy beyond the flash of the guns", Scheer; at
  Coronel Nürnberg found Monmouth by her flashes) **[SRC]** [D8][C1-25][E3-22].
- **Smoke** betrays a big ship in twilight long before a small one (Premuda) **[SRC]** [E3-17].

### 3.5 Torpedo tracks at night

On the Yellow Sea night of 1904 Tsesarevich's crew saw nine torpedoes, none hitting: "their trajectories could be
clearly seen on account of their luminous tracks on the phosphorescent sea" **[SRC]** [C1-35]. A torpedo may run "more
than a hundred yards ahead of the wake which appears on the surface" **[SRC]** [C1-11]. German battleships at Jutland
"swerved madly" from Spitfire's torpedoes at close range (how they saw them is not stated) **[UNCERTAIN]**.

### 3.6 Gun flash, seen and suffered

Flashes were seen where no ships could be ("in the mist no ships could be distinguished", Jellicoe), but no distance
was found **[SRC]** [C1-9]. Own flash: Swift's forecastle gun blinded Peck and she lost the enemy for "several seconds";
Laverock's bridge officers were "blinded" by German time-fuzed HE bursting near **[SRC]** [E3-4][E3-5]. Laboratory
flash blindness: recovery to a dim target 14-110 s by flash energy, 2-7 s to read a ~1 cd/m² target after weak flashes,
longer for darker targets and bigger flash fields **[SRC]** [C1-37][C2-19][C2-20].

## 4. Identification

### 4.1 The systems

| Navy | System | Tag |
|---|---|---|
| RN 1914-16 | two-letter lamp challenge, two-letter reply, changed by period ("challenge and reply now in force"); challenge then hold fire for the reply. Jellicoe, June 1916: "Their system of recognition signals is excellent. Ours is practically nil." | [SRC] [D1][D14] |
| Germany 1914-16 | coloured lamp groups (red, green, white; horizontal rows or vertical strings) switched on and off for "a few seconds", up to three times; British reports describe at least six patterns in one night | [SRC] [D1] |
| Russia 1904 | no challenge at Dogger Bank; Tabulevich lamps flashed above the searchlights; the admiral marked targets with his light; cease-fire by all lights out but one pointed at the sky | [SRC] [D5][E1-18] |
| RN after Jutland | screens to fire "on every ship that approached, without challenging when in waters where the enemy was expected"; heavy ships not to rejoin at night | [SRC] [D14] |

### 4.2 Cases

| When | Who | What happened | Range | Tag |
|---|---|---|---|---|
| 21-22 Oct 1904 | Russian battleships, Dogger Bank | no challenge; 10-12 min of fire on trawlers and on their own cruisers Aurora (hit, chaplain killed) and Donskoi, whose searchlights were taken for the enemy's; Oryol >500 shells, no hits | ~5 cables | [SRC] [D5][E1-18] |
| 8-9 Feb 1904 | Port Arthur | boats showing lights taken for Russian destroyers | — | [SRC] [E1-18] |
| 12-13 Apr 1904 | Strashny | cruised all night with a Japanese flotilla unrecognised | — | [SRC] [E1-18] |
| 16 Dec 1914 | Lynx | passed three German cruisers by answering with a signal she had seen a German destroyer make | — | [SRC] [E3-20] |
| 24 Jan 1915 | Kolberg | "The enemy's signal of recognition was answered by the Kolberg turning on the searchlight" | — | [SRC] [D4] |
| 31 May 1916, 22:05 | Castor | strangers made "the first two signals … correct for the British challenge of the day", then wrong ones, then lights and fire; other destroyers held fire, unsure | 2,000-2,500 yd | [SRC] [D2] |
| 31 May, 22:15 | 2nd LCS | British began to challenge; the Germans showed coloured lights, then a dozen searchlights and fire; Nottingham and Birmingham, dark, not fired on | 800-2,000 yd | [SRC] [E2-7] |
| 31 May, 23:15-23:40 | Tipperary, Broke | Tipperary's challenge answered by three ships' searchlights at once; Westfalen lit and fired before Broke's challenge was complete | 150-1,000 yd | [SRC] [E2-7][D1] |
| ~00:00 | Ardent, Fortune | "Leading Battleship switched on recognition lights and then searchlights and opened fire on us" | ~1,000 yd | [SRC] [D1] |
| ~00:00 | Agincourt | "I did not challenge her, so as not to give our Division's position away" | close | [SRC] [D1] |
| ~00:00 | Revenge | challenged, "answered by 2 letters, though they were not the correct ones"; no fire | — | [SRC] [D1] |
| ~22:30 | Thunderer | Moltke unchallenged, no fire: "inadvisable to show up the battle fleet unless obvious attack was intended" | close | [SRC, secondary] [D10] |
| 1 Jun, 02:20 | Iron Duke | challenged a cruiser, no answer, "fired a gun" | — | [SRC] [D1] |
| 26-27 Oct 1916 | Flirt, Nubian, Amazon, Viking | Flirt's challenge echoed back and taken for friends; the others challenged and were fired on at once | point blank | [SRC] [E3-5] |
| 17 Mar 1917 | Paragon | torpedoed "while challenging" | close | [SRC] [E3-4][E3-5] |
| 14-15 May 1917 | Borea | recognition signal at ~1,000 m, then shot up | 1,000 m | [SRC] [E3-9] |
| 14-15 Feb 1918 | Amazon | the Germans "repeatedly failed to respond"; taken for British | — | [SRC] [E3-13] |

### 4.3 Rates (from the cases; no source gives one)

- Jutland, British battle fleet and its rear after midnight: every unknown large ship was German, and fire was held in
  6 of 7 cases **[INFERRED]** [D].
- Jutland, British light forces: at least 5 cases of taking Germans for friends; no confirmed British-on-British firing;
  the night's friend-on-friend damage came from collisions **[INFERRED]** [D].
- Dover 1916-18: every British challenge at a raider drew fire or was echoed; no raider challenged first **[INFERRED]**
  [E3].
- Dogger Bank 1904: 100% of the "torpedo boats" were trawlers or own cruisers **[SRC]** [D5].
- Identification was settled at 150-2,000 yd at Jutland, median about 600-800 yd **[INFERRED]** [D].
- Answers came within seconds when they came as fire; no timed interval for a friendly reply was found **[SRC]**.

## 5. The cases

### 5.1 1891-1905 (appendix E1)

| Case | Date | Light (computed) | Attack | First detection | Defender's lights | Launch range | Torpedoes / hits | Attackers lost |
|---|---|---|---|---|---|---|---|---|
| Caldera | 23 Apr 1891 | full moon through cloud, 21-27° up | 2 torpedo gunboats on an ironclad at anchor | 2,000 yd | none (no searchlight, no nets) | 50-100 yd | 5 / 2 (1 dud) | 0 |
| Santa Catarina | 16 Apr 1894 | moon just set, cloudy, "very dark" | 4 boats after a decoy | after the hit | searchlight only after the hit | ~130 yd | ≥4 / 1-2 | 0 |
| Weihaiwei I | 5 Feb 1895 | moon just set, −10 °C | 10 boats (8 in) | as they closed | none reported | ~300 yd | ≥7 / 1 | 2 (rocks, gunfire) |
| Santiago | Jun 1898 | — | blockade | Merrimac at ~a ship's length by a dark picket boat | two US ships 1,000 m off, one holding its lights on the exit | — | — | — |
| Port Arthur | 8-9 Feb 1904 | moonless until ~01:56 JST | 10 destroyers | not before the torpedoes | anchored, lit as usual; Retvizan on searchlight duty "attracted many torpedoes" | ~650 yd (first wave) | 16 / 3 | 0 |
| Port Arthur roads | 23-24 Jun 1904 | gibbous, clear | ~30 boats | fire opened at 5-6 mi | fortress "wall of light" | ≥3 mi (rangefinder) | ≥5-6 found / 0 | — |
| Yellow Sea | 10-11 Aug 1904 | new moon, swell | 18 DD, 29 TB available | — | many dark (Diana never lit) | — | ? / 0 | ? |
| Dogger Bank | 22 Oct 1904 | 95%, 23° up, fog patches | none present | trawlers at ~5 cables | searchlights on trawlers and own cruisers | — | — | — |
| Tsushima | 27-28 May 1905 | moonless until ~01:51 JST | 21 DD, 37-45 TB | "slightly visible" boats | rear ships lit (Nakhimov "like moths"), Nebogatov's division dark | ≤600 yd; to ~100 yd | 54 + 32 linked mines / ~4-6 | 3 TB (2 gunfire, 1 collision) |

**Tsushima, the bench's line** ("Russian ships that lit their searchlights were found and torpedoed"): supported for
Nakhimov and the rear ships (Klado: the rear battleships drew fire when they lit up, the forward ships did not);
Nebogatov's division showed "only a masked stern lantern" and was not torpedoed; Navarin was found because she stopped
out of line, and her attackers lit her; Monomakh and Sissoi unknown **[SRC]** [E1-20][E1-21]. Russian black-powder
smoke hid the destroyers from Navarin's gunners; bursting shells were "brighter than the searchlights" **[SRC]**
[E1-18][E1-20]. Conflicts: Navarin by mines or torpedoes; Monomakh torpedoed or rammed; TB 69 lost by collision (ja.WP)
or gunfire (Tōgō) **[UNCERTAIN]**.

Peacetime checks: in the RN's 1900 manoeuvres the umpires put 6 destroyers out of action, disallowed 26 claims and
adjudged no ship torpedoed; a destroyer took six dark friendly battleships for torpedo boats; Minerva took fishing boats
for torpedo boats **[SRC]** [E1-37]. The 2nd Pacific Squadron's drill alarm at Reval (1904): 8 minutes with no
searchlight ready **[SRC]** [E1-20].

### 5.2 1911-1918, Jutland aside (appendix E3)

| Case | Date | Light (computed) | First sighting | Lights | Ranges | Losses |
|---|---|---|---|---|---|---|
| Feth-i Bülend | 31 Oct 1912 | 64%, 18° up | Greek TB 11 passed the fortress lights | evaded | 150 m, 3 fired, 2 hits | Feth-i Bülend sunk |
| Hamidiye | 21 Nov 1912 | 85%, 26° up | — | — | three boats missed from farther; Drazki hit at ~100 m | Hamidiye damaged |
| Coronel, after dark | 1 Nov 1914 | full moon rising behind cloud | Nürnberg steered for gun flashes | a light on Monmouth's ensign to invite surrender | 550-900 m | Monmouth, all hands |
| Hamburg vs destroyers | 16 Dec 1914 | moonless | Lynx saw V155 at 05:15 | the cruiser's lights at 600-700 yd won the first exchange, then drew Hardy's torpedo; "her lights went out" and she withdrew | <500-700 yd | Hardy damaged |
| Dardanelles sweeping | 10-14 Mar 1915 | moonless | fortress lights caught the sweepers | trawlers turned back when caught; ships' fire made the lights flicker, never hit them | batteries | trawlers, Amethyst 60 casualties from 2 shells |
| Dover raid | 26-27 Oct 1916 | new moon | Germans saw first (21:20), unseen | German lights on the drifters; Flirt's rescue light drew attack; burning Nubian a beacon | point blank | Flirt, Queen, 6 drifters; Nubian wrecked |
| Schouwen | 23 Jan 1917 | new moon, clear, frost | Conquest saw boats crossing astern | none: funnel flare enough; later three cruisers lit a cripple | very close | Simoom (torpedoed by a lone S50 attacking the head of a line) |
| Laverock | 25 Feb 1917 | overcast, moon hidden | Germans saw first | German red flare; HE bursts blinded her bridge | close | none; one ship taken for three, the raid given up |
| Paragon | 17 Mar 1917 | moon below | crossing her bows | rescue lights drew the raiders back 10 min later | close | Paragon; Llewellyn torpedoed |
| Swift and Broke | 20-21 Apr 1917 | moonless (1%), "so dark" | Germans fired first; funnel flares 10 s later | none at contact | point blank; 200 yd last torpedo; Broke rammed G42 at ~27-30 kn | G42, G85; Broke 21 killed, 36 wounded |
| Otranto | 15 May 1917 | 39%, rising in the east | Borea saw the destroyers ~03:30 | Borea's recognition signal at ~1,000 m | 1,000 m; cruisers on drifters at 100 yd | Borea, Carroccio, 14 drifters |
| Wien | 10 Dec 1917 | moon below | unseen | none | — | Wien (2 hits) |
| Barrage raid | 14-15 Feb 1918 | moon set, "very dark and hazy" | the raiders on drifters lit by the barrage's own flares and a Folkestone light | barrage flares | ~50 yd | 7 drifters, a trawler |
| Premuda | 10 Jun 1918 | moonless, sun −15° | smoke at 6.5 mi; MAS unseen to 300 m | none | ≤300 m, 2 hits | Szent István |

Dover Strait, 20-21 April 1917 (the bench's case): "moonless, 2 against 6, 2 German boats sunk" is supported; the
German division was "five or six" boats [UNCERTAIN]; **"about 600 yd" is not in Evans, Bacon or Newbolt**: the sources
give point blank and "so short", a 200 yd last torpedo, and the Germans opening fire before they were seen **[SRC]**
[E3-4][E3-5][E3-6].

### 5.3 Jutland's night (appendix E2)

**The light.** Sunset 20:12 GMT, the sun lowest at −11.5° due north at 23:33, −6° again at 01:56, sunrise 02:55: no
astronomical or even nautical night at 56.5°N (55.5°N dips past −12° for 1.5 h). New moon at about 19:37 GMT on 31 May:
no moonlight at all **[INFERRED]** (PyEphem; two strands agree). Visibility: "the mist was increasing" (Jellicoe ~21:00),
"quite dark and rather misty" (Dublin 22:40), "the night was dark and Sea fairly calm" (Tipperary), "her hull could not be
seen more than three-quarters of a mile away" (~23:20), at daylight "three to four miles" (Jellicoe), "no more than two
miles" (Malaya) **[SRC]** [E2-2][E2-7][E2-14]. No night cloud statement was found.

| GMT | 21:00 | 22:00 | 22:30 | 23:00 | 23:30 | 00:00 | 00:30 | 01:00 | 01:30 | 02:00 | 02:30 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Sun (56.3°N 6.2°E) | −5.4° | −9.3° | −10.6° | −11.4° | −11.7° | −11.5° | −10.8° | −9.6° | −7.9° | −5.8° | −3.2° |
| Its bearing | 325° | 338° | 345° | 352° | 359° | 6° | 14° | 20° | 27° | 34° | 40° |

**Who had the light.** The High Seas Fleet came from the W to NW of the British rear, steering SE to SSE; Castor had them
on the starboard bow, Southampton and Tipperary on the starboard beam. British eyes looked W to N, toward the glow;
German eyes looked E to SE, into the dark **[INFERRED]**. Nottingham: a Stettin "not more than 2 miles away, silhouetted
against the evening sky, and so was lit up to some extent while we remained in darkness"; "burning no searchlights saved
us" **[SRC]** [E2-7]. The Germans answered with lights: once a searchlight came on, the darker side bought nothing
**[INFERRED]**.

**The encounters** (full table in E2 §2):

| GMT | Encounter | Ranges | Lights, challenge | Torpedoes (hits) | Result |
|---|---|---|---|---|---|
| 21:40-22:00 | Frankfurt, Pillau v. Castor's flotilla | ~1,100 m | Germans kept dark, fired torpedoes without lights or guns | 2 (0) | — |
| 22:05-22:15 | IV SG v. Castor, 11th Flotilla | lights at 2,500 yd, fire 2,000 | partial British challenge, then lights | 4 (0) | Castor 12 killed, 23 wounded; ~5 min |
| 22:15-22:35 | IV SG (five ships) v. 2nd LCS | 800-2,000, ~1,500 yd | German coloured lights then a dozen lights on Southampton and Dublin; Nottingham and Birmingham dark, untouched | Southampton 1 (1: Frauenlob) | hot exchange "no more than four minutes", whole action ~15 min; Southampton 90 casualties |
| 23:15-23:35 | 4th Flotilla v. IV SG and Westfalen, Nassau, Rheinland | Tipperary lit at 1,500-2,000; torpedoes 800-1,000 | Tipperary's challenge answered by three ships' lights at once | ~14 in the flotilla's attacks (1: Rostock) | Westfalen fired 92 × 15 cm and 45 × 8.8 cm at Tipperary in 5 min; Spitfire rammed Nassau; Posen rammed Elbing |
| ~23:40 | Broke's group v. Westfalen | ~150 yd (Broke) to ~½ mile | German recognition lights, then lights and fire before Broke's challenge was done | — | Broke 82 casualties (13 × 15 cm and 13 × 8.8 cm in 45 s); Broke rammed Sparrowhawk, Contest cut her stern |
| 23:30-00:30 | Fortune, Ardent and others v. Rheinland, Posen, Oldenburg, Helgoland, Westfalen | 700-1,700 yd | recognition lights, lights, fire; star shell made "a surprise torpedo attack almost impossible" | several (0) | Fortune, Ardent sunk |
| ~00:10 | Black Prince v. Thüringen and others | 750-1,500 yd | Thüringen's lights | — | sunk within ~15 min, all hands; Campbell credits her with two 6-in hits on Rheinland at ~23:35, at 2,200-2,600 m [UNCERTAIN, via secondaries] |
| 00:25-00:45 | 13th Flotilla remnants | 200-1,000 yd | two red over one white or green, then lights | — | Turbulent sunk |
| 01:43-02:10 | 12th Flotilla v. II Squadron | 1,700-3,500 yd; Maenad 4,000-5,000 | "too light for searchlights" | 17 (1: Pommern) | Onslaught hit |
| 02:35 | Moresby | 4,000 yd | — | 1 (0) | V4 lost, cause unknown |

Totals: British lost Black Prince and five destroyers (Tipperary, Fortune, Ardent, Turbulent, Sparrowhawk), none to a
torpedo; Germany lost Frauenlob, Pommern and Rostock to torpedoes, Elbing to a collision, and V4 **[SRC]**. British
torpedoes at night about 37 for 3 hits; German torpedoes at night none known to hit **[INFERRED]** [E2-1]. Sightings
with no fire or report: Thunderer, Agincourt, Revenge, Marlborough, Malaya and Valiant, Fearless (a German battleship
passed down her side, "No fire was opened"); Jellicoe's "No enemy ship was seen by the battlefleet during the night" was
untrue because sightings went unreported **[SRC]/[INFERRED]** [E2] §3.

### 5.4 Patterns across the cases

1. **The side that saw first fired first, and on raid nights that was the raider** **[SRC]** [E3]. At Jutland the
   British usually saw first but hesitated over identity, and the Germans landed the first blow **[SRC]** [E2-13].
2. **Fights were short and decisive:** seconds to 5 minutes a contact, 15 at most **[SRC]** [E2][E3].
3. **Lights drew fire and torpedoes; dark ships were spared** (Summary 5) **[SRC]**.
4. **Searchlights on a lone cripple or a surrender:** the British cruisers on V69 (1917), Nürnberg on Monmouth (1914)
   **[SRC]** [E3].
5. **Lit small craft turned away; lit big ships seldom did:** the Dardanelles sweepers turned about when caught; Hardy
   hauled out and came back; a cruiser whose lights drew a torpedo shut them and withdrew; British destroyers fired and
   turned away **[SRC]** [E3][D1].
6. **Rams and collisions:** Broke rammed G42 at speed (1917); Spitfire-Nassau, Broke-Sparrowhawk, Contest-Sparrowhawk,
   Posen-Elbing in one Jutland action; Port Arthur and Tsushima collisions **[SRC]**.
7. **Twilight at the target's back:** Seitz's Austrians "stood out against the last glow of twilight" at Durazzo
   (1915), and within minutes of dark "gunlaying and range-finding became guesswork" **[SRC]** [E3-5].
8. **A lone, aggressive ship is over-counted at night** (Laverock taken for three) **[SRC]** [E3-5].

## 6. Doctrine by navy, written and done

| Navy, period | Written | Done | Tag |
|---|---|---|---|
| RN 1914-16 | No fleet night action: a night action "must be fought at very close range", "very largely a matter of chance" (Jellicoe). Columns one mile apart so they "would not mistake our own ships for those of the enemy"; destroyers astern, clear of the battleships so neither would fire on the other. Challenge, then hold fire for the reply | Fleet steamed south; battleships held fire on German ships crossing astern; destroyers met the High Seas Fleet piecemeal; lights little used | [SRC] [D3][D14] |
| RN 1917-18 | Director control for all guns; lights from control positions; star shell; screens to fire without challenging where the enemy is expected; Bacon: fire "on all vessels without challenge" in raid areas | Dover patrols still took raiders for friends in Feb 1918 (Amazon court-martialled) | [SRC] [D14][E3-4][E3-8] |
| Germany 1914-16 | Night fleet in close formation; flotillas to make night attacks; (written orders on lights not reached) | Lights trained dark, then light and fire at once; star shell to search and to fire; coloured-lamp recognition; cruisers sometimes fired torpedoes dark; battleships turned away from attacks. Jellicoe: "remarkably high standard" | [SRC] [D3][D4][D1] |
| Russia 1904-05 | Rozhestvensky: no vessel to get among the fleet; the watch may fire on an "evident and imminent" torpedo attack; the admiral marks targets by searchlight | Dogger Bank; at Tsushima searching with lights got ships torpedoed; Nebogatov trained his ships to keep dark | [SRC] [D5][E1-20] |
| Russia, Semenov 1904 | (practice) lighting or firing "would bring the whole flotilla down on the ship" | Diana kept dark through the Yellow Sea night, untouched | [SRC] [E1-18] |
| Japan 1894-1905 | (written doctrine not reached) | attacks after moonset or before moonrise; close to ≤600 yd; boats lit the target ship from two sides while a third came in astern (Navarin) | [SRC] [E1] |
| Makarov 1897, French, A-H, Italian, USN | not reached | — | — |

## 7. The night's light, the eye, and the sim's law

### 7.1 Illuminance and the cases' light

| Sun | Schlyter (lux) | USNO Circ. 171, sun only | Sim |
|---|---|---|---|
| +10° | 11,100 | 11,100 | 15,000 |
| +5° | 4,540 | 4,090 | 6,000 |
| 0° | 759 | 757 | 500 |
| −3° | ~75 | 66 | 60 |
| −6° | 3.41 | 2.99 | 3 |
| −9° | ~0.12 | 0.116 | 0.1 |
| −12° | 0.00806 | 0.0044 | 0.008 |
| −15° | ~0.002 | 1.7·10⁻⁴ | — |
| −18° | 6.45·10⁻⁴ (includes the night sky) | 6.6·10⁻⁶ | 7·10⁻⁴ |

[SRC] [C2-1][C2-2]. Night: starlight and airglow 0.9-1.1 mlx measured (models 0.5-2 mlx), zenith 0.24-0.30 mcd/m², the
horizon 2-3× the zenith; an overcast moonless night 1-3·10⁻⁴ lux (1/10 to 1/3 of clear), near the horizon up to 5-11×
darker; full moon at the zenith 0.27 lux (0.32-0.38 within a day of full), a quarter moon 0.09 of full by
Krisciunas and Schaefer's phase law; cloud on moonlight: thin ÷2, average ÷3, dark stratus ÷10 **[SRC]** [C2-1][C2-2]
[C2-4][C2-6][C2-9].

The cases' sun and moon (all [INFERRED], PyEphem; local times in the appendices):

| Case | Sun at the action | Moon |
|---|---|---|
| Weihaiwei, 5 Feb 1895 | far below | 69%, setting during the attack |
| Port Arthur, 8-9 Feb 1904 | far below | 48%, below until ~01:56 JST |
| Yellow Sea, 10-11 Aug 1904 | −3° at the start, then dark | new |
| Dogger Bank, 22 Oct 1904 | far below | 95%, 23-33° up |
| Tsushima, 27-28 May 1905 | −7° at 20:00 JST, −17° by 21:00 | 37%, below until ~01:51 |
| Coronel, 1 Nov 1914 | −12° at ~19:50 ship's time | 99%, 19-27° up |
| Gulf of Riga, 16-17 Aug 1915 | −12° to −18° | 37%, below |
| Jutland, 31 May-1 Jun 1916 | −5° (21:00) to −11.5° (23:33) to −6° (01:56) | new (0%) |
| Dover Strait, 26-27 Oct 1916 | far below | new (0%) |
| Dover Strait, 20-21 Apr 1917 | −26° | 1%, below |
| Otranto, 15 May 1917 | −23° to −1° | 39%, 10-34° up in the east |
| Dover Strait, 14-15 Feb 1918 | far below | 19%, set |

### 7.2 The twilight sky by bearing (Koomen et al. 1952)

Horizon luminance at Sacramento Peak, cd/ft² (×10.76 for cd/m²), Z the azimuth from the sun **[SRC]** for +5° to −6°,
**[INFERRED]** for −9° and below (the scan's "0.0₂36" notation decoded; see C2 §2) [C2-3]:

| Sun | Z 0° | 22.5° | 45° | 90° | 135° | 180° | toward / away |
|---|---|---|---|---|---|---|---|
| +5° | 1000 | 600 | 200 | 130 | 160 | 190 | 5.3 |
| 0° | 150 | 102 | 62 | 37 | 32 | 38 | 3.9 |
| −3° | 20 | 15 | 11 | 4.2 | 2.4 | 2.4 | 8.3 |
| −6° | 0.9 | 0.68 | 0.4 | 0.11 | 0.078 | 0.078 | 11.5 |
| −9° | 0.04 | 0.028 | 0.017 | 0.005 | 0.0036 | 0.0036 | 11.1 |
| −12° | 0.0018 | 0.0014 | 0.00076 | 0.00036 | 0.00026 | 0.00028 | 6.4 |
| −15° | 0.00018 | 0.00013 | 0.00008 | 0.00005 | 0.00005 | 0.000058 | 3.1 |

The profile is a glow toward the sun (×8 from Z 0° to 90°) over a nearly flat floor (90-180°). Fits: L = F + G·e^(k(cos
Z − 1)) with k ≈ 3.2 and F/G ≈ 0.1 for −6° to −12° (2-6% rms); a single e^(k cos Z) fits worse, k ≈ 1.2 (−3° to −9°),
0.9 (−12°), 0.5 (−15°). Against the level illuminance, the horizon's azimuth mean is 4-7× E/π in twilight (the side away
from the sun alone 2-3×), about 2.5× at night; the glow is a low arch, down ×5 by 30° altitude **[INFERRED]** [C2] §2.
The asymmetry is already there before sunset (5:1 at +5°) and peaks at −6° to −9° **[INFERRED]**.

### 7.3 The eye

- **Large-target threshold against the adapting luminance** (Crumey 2014's fit to Blackwell 1946, 50% detection,
  checked against Taylor 1960's 6° disc): C∞ = 2.72·10⁻³ above 0.354 cd/m², else 7.633·10⁻³ L^(−1/4) − 7.174·10⁻³;
  the rise over 30 cd/m² is ×6 at 10⁻², ×13 at 10⁻³, ×25 at 10⁻⁴, ×47 at 10⁻⁵. Riccò's summation area grows ×3 at
  0.1-1, ×11 at 10⁻², ×28 at 10⁻³, ×58 at 10⁻⁴ **[SRC]** (formulas checked against the paper) [C2-10][C2-11].
- **Point lights** (Knoll, Tousey and Hulburt 1946): threshold 10⁻¹⁰ (1 + b)^½ fc, b in nL; ten times that matches
  IALA's practical 2·10⁻⁷ lux at night and its day values within ×2 **[SRC]/[INFERRED]** [C2-14][C2-15].
- **Searching:** unalerted observers may need 5× or more; Blackwell's 90% level is ×1.6 over 50% **[SRC]** [C2-10]
  [C2-18].
- **Glare:** Stiles-Holladay 10E/θ² holds for 1-30°; below 1° the CIE general equation gives ×1.5 at 1° and ×4 at
  0.3° **[SRC]** [C2-21][C2-23].
- **Flash recovery:** 2-7 s to a ~1 cd/m² target after weak flashes, 5-20 s to a 0.02 cd/m² target, longer with
  flash-field size and for darker targets **[SRC]** [C2-19][C2-20].

### 7.4 The sim's law against the record

[night_ref.py](night_ref.py) runs the sim's sight model (sighting_ref.py: Koschmieder haze from the visibility, the
silhouette's solid angle through a 7×50 at 0.6, the threshold with its summation term) with the sim's dark law and with
Blackwell's, on five anchors from §3.3 and §5.3. Skies are horizon luminances toward the target (§7.2). **[INFERRED]**
throughout.

The dark's rise over 30 cd/m² through a 7×50 (sky cd/m² → f_a on the large-target threshold, f_b on √ω_c):

| Sky | Sim f_a | Sim f_b | Blackwell f_a | Blackwell f_b |
|---|---|---|---|---|
| 10⁻⁴ | 2.3 | 54 | 29 | 8.2 |
| 10⁻³ | 1.4 | 18 | 15 | 5.7 |
| 5·10⁻³ | 1.2 | 8.5 | 9.4 | 4.3 |
| 2·10⁻² | 1.1 | 4.7 | 5.8 | 3.1 |
| 0.1 | 1.0 | 2.6 | 3.0 | 1.8 |

The sim's large-target threshold barely rises, and its summation area (f_b²) grows to 300-3,000× against Blackwell's
33-67×. For a ship (a large target at night ranges) the two errors partly cancel, which is why Q1's law looked right
at the black overcast and drifted long in twilight.

What scale on each law's threshold the record implies (k on the sim's whole threshold; F the field factor on Blackwell's
laboratory C∞, the sim's day 0.05 being F = 18.4):

| Anchor (sky, visibility; record) | k, night contrast 0.6 | F, 0.6 | k, 0.9 | F, 0.9 |
|---|---|---|---|---|
| Black overcast (10⁻⁴, 10 km; destroyer 0.2-0.6 km) | 1.5 | 5.3 | 2.3 | 7.9 |
| Clear moonless (10⁻³, 25 km; destroyer 0.5-2 km) | 2.9 | 8.7 | 4.4 | 13 |
| Jutland −11.5°, away from the glow (3·10⁻³, 8 km; battleship 0.55-1.8 km) | 4.7 | 12 | 7.1 | 18.5 |
| Jutland −11.5°, toward the glow (1.9·10⁻², 8 km; 2-3.2 km, a light cruiser in the record) | 3.5 | 14 | 5.3 | 20.5 |
| Jutland −9°, away (3.9·10⁻², 8 km; ≥2.3 km, a lower bound) | ≤5.0 | ≤23 | ≤7.5 | ≤35 |

No constant scale fits the sim's law (k from 1.5 to 5): it is too generous by a factor that grows from the black night
into twilight. Blackwell's shape halves that drift; with a ship's night contrast of 0.9 against the sky (C2's estimate:
a hull of reflectance 0.1-0.2 lit only by the sky) the implied F is about the day's 18 from Jutland's twilight up, and
falls to about 8-13 in the moonless dark. A field factor easing as the fourth root below a 3·10⁻³ cd/m² sky,
F = 18.4·min(1, (L_eye / 1.8·10⁻³)^¼), puts every anchor in its band and leaves the day unchanged:

| Anchor | Sim today | Proposal | Record |
|---|---|---|---|
| Black overcast, destroyer | 0.7 km | 0.41 | 0.2-0.6 |
| Clear moonless, destroyer | 3.5 | 0.84 | 0.5-2.0 |
| Jutland −11.5°, away, battleship | 5.0 | 1.22 | 0.55-1.8 |
| Jutland −11.5°, toward, battleship | 7.0 | 3.35 | 2.0-3.2 |
| Jutland −9°, away, battleship | 7.6 | 4.49 | ≥2.3 |
| Full moon 30° up (sky 0.01), battleship / destroyer | 14.2 / 9.1 | 5.0 / 2.9 | (Caldera: a gunboat at 1.8 km) |
| Day, 25 km visibility, battleship | 23.1 | 23.1 | unchanged |

By glass under the proposal (a destroyer seen from a destroyer, clear moonless): naked eye 0.15 km, 7×50 0.84, 10×45
0.95, 20×120 1.9 (the IJN's big glasses ×2) **[INFERRED]**, [EST] light shares. The fit rests on five fuzzy anchors;
it is a shape and a knob, not a measurement.

## Against the plan's estimates

| Plan's [EST] (night.md, settings.json, templates) | Verdict | Suggested |
|---|---|---|
| NightLight Daylight table | −3° to −12° confirmed; 0°, +5°, +10° high; −18° double-counts the night sky | 0° 750, +5° 4,500, +10° 11,000; sun-only −15° 2·10⁻⁴, −18° 1·10⁻⁵ [SRC] |
| StarLux 10⁻³, FullMoonLux 0.25 | confirmed | 0.27 and ×1.35 within a day of full; phase by K&S (quarter 0.09) [SRC] |
| NightOvercastShare 0.15 | roughly right | thin 0.5, average 0.33, stratus 0.1 [SRC] |
| Horizon sky = E/π | low | azimuth mean ≈ 5·E/π in twilight, ≈ 2.5·E/π at night [INFERRED] |
| NightTwilightArch e^(1.5 cos Δ) (away = 1/20), forming 0-6° down | too strong a ratio, wrong shape, wrong start | glow + floor: (E/π)·[2 + 20·e^(3.2(cos Δ − 1))] for −3° to −12°; ratio 11 at −6°/−9°, 6 at −12°, 3 at −15°; present before sunset [INFERRED] |
| DarkOf: (1 + √(L_k/L)), knees 10⁻⁴ and 0.2 | large-target rise far too small; summation far too large | §7.4: Blackwell's C∞ and Riccò area, F easing below 3·10⁻³ [INFERRED] |
| SightContrastNight 0.6 | no evidence | ~0.9 against the sky, ~0.5-0.7 against the sea [EST] |
| Lookout's 7×50 at 0.6 | transmission confirmed (0.58 uncoated) | by navy: KM 7×50 / 6×42 from 1914; RN 10×45 (4.5 mm) to the 1930s; USN pre-1918 8× (2.5 mm); Galilean before ~1905 [SRC] |
| Rangefinder 2.5 mm at 0.35 | no evidence | plausible (the USN's 1914 8× telescope) [INFERRED] |
| Destroyer made out 0.4 / 2.0 / 6.4 / 9.8 km (overcast, moonless, −12°, full moon) | overcast right; the rest too long | §7.4's proposal: ~0.4 / ~0.8 / ~0.8 (−11.5°, mist, away from the glow; 2.5 toward it) / ~2.9 km [INFERRED] |
| Bow wave at contrast 1.0 | cue confirmed, no number | add phosphorescence (a self-luminous bow wave and wake when the sea glows) [SRC]/[EST] |
| SightFunnelGlowCd 10 (coal forcing) | cue confirmed, no number | flare also from oil-fired destroyers at high power (Dover 1917, Schouwen 1917): enough to fight by at close range [SRC]/[INFERRED] |
| Broadside flash 15-30 km; a destroyer's gun 29 km | no distance found | a 4-inch should show well short of a 12-inch broadside [INFERRED] |
| SightDazzleS 1 | too short | 2-7 s for lit targets, 15-30 s for silhouettes near threshold [SRC]/[EST] |
| TrackSeenM ~170 m moonless | too short in a phosphorescent sea | a bioluminescence factor (tracks "clearly seen" 1904); the torpedo >100 yd ahead of its wake [SRC] |
| Searchlight templates | partly | see Recommendations 4 |
| SearchlightSweepDegS 1.5 | confirmed (1894) | — |
| SearchlightStrikeS 10, ExposeS 4 | strike only from cold: arcs burned behind shutters (USN 20 min spec); exposure "rapid" by iris | ready = arc struck, trained dark on the target; expose ~1 s [SRC]/[EST] |
| Pointing error, stray 10⁻⁵, backscatter 0.3, glare K 10 | no numbers; the operator's worse view confirmed (observer 25-60° off the axis, 1894) | — |
| Reach (Lion's 36-in): 5.2 / 2.8 / 1.7 / 0.8 km | fits a high-intensity light | plain arc ~2.7 km good, 3.5-3.7 extreme (clear); re-check after the eye change [SRC] |
| Draw 6.5 kW (plain 36-in) | confirmed at the arc | HI 11 kW at the arc, 18-19 kW from the bus [SRC] |
| Star shell DE 1911: 25 s at 6 m/s | burn likely 10-15 s unless parachuted | 15 s (10-25), fall 6 m/s only with a parachute [SRC]/[EST] |
| Star shell: 15% candle, 3·10⁴ cd/kg, 2·10⁵ cd a 15 cm, fuze 1.5%, 15% duds | no evidence; 2·10⁵ plausible against "an area of about 1,000 yards" | keep [EST]; fire it at reduced velocity with its own range table [SRC] |
| Max fuze 25 s | close (army 7.7 cm 28.8 s) | 25-30 s [UNCERTAIN] |
| RN star shell with the 1927 family | late | RN from 1917 [SRC] |
| USN, Japan with the 1920s families | USN by 1922 confirmed | Japan: no evidence |
| 5% of light guns' outfit | WWII big-ship secondaries 2-6% | destroyers and light cruisers 10-20% [SRC, WWII] |
| Aim 600 m beyond, 300 m up | height confirmed (300-370 m); "beyond" unsourced before WWII | keep [EST] |
| One mount keeps one flare up | no WWI evidence: the Germans used it as a precursor | WWII: three guns, ~4 rounds a gun a minute [SRC] |
| Challenge lamp 3·10⁴ cd, 10° | the German challenge was coloured lamp groups, shown all round | several coloured lamps, all round, a few seconds, up to three times [SRC] |
| ChallengeReplyS 20; unanswered → foe | answers as fire came within seconds; unanswered or wrong replies did not make the British line fire | "foe" informs; firing stays the doctrine's hold-fire rule [SRC] |
| ChallengeTrustS 600 | no evidence | a partly right reply caused doubt rather than trust (Castor) [SRC] |
| ChallengeM 2,500 | observed 150-2,000 yd, median 600-800 | 2,000 m for light forces; a hiding battle line doesn't challenge [SRC]/[EST] |
| AnswerChallenge 0 | the Germans had part of the British challenge at Jutland | a partial-reply state for the IV SG and battlecruisers from ~22:00 [SRC]/[EST] |
| WhenLit turn away, LitTurnAwayS 120 | direction confirmed for small craft and lit cruisers drawing torpedoes; no time | contacts lasted seconds to 5 min [SRC] |
| NightSpacingShare 0.5 | RN closed columns to one mile at night; the day spacing not reached | — |
| UnknownFriendM 5 km × (1 − a) | in kind (Russian 1904 standing orders; RN 1917 orders) | the cases were all within ~2 km [SRC] |

## Recommendations for this codebase

1. **The eye (Q1, `SightModel.DarkOf`, `sighting_ref.py`).** Replace the two knees with Blackwell's shape (Crumey's C∞ and
   Riccò fits as ratios over 30 cd/m²) and a field factor F = 18.4·min(1, (L/1.8·10⁻³)^¼) on the eye's luminance,
   with `SightContrastNight` 0.9 against the sky (0.6 against the sea, where the sea is the background). `night_ref.py`
   has the law and its anchors; the C# test's reference rows should be regenerated from it. Expect the night bench's
   first sightings to fall by ×2-4 and the searchlight reach (`SearchlightModel.ReachM`) and star shell reach to change
   with it: re-check them against §1.5 rather than tune the beams. **[INFERRED]**
2. **The sky (`NightLight`).** Fix the Daylight table's ends (0°, +5°, +10°; −15°, −18° sun-only). Raise the horizon to
   about 5·E/π in twilight and 2.5·E/π at night (a setting, so the moon's and stars' shares follow). Replace the von
   Mises arch with glow + floor (k ≈ 3.2, floor ≈ 0.1 of the glow's peak), its strength peaking at −6° to −9° and present
   from before sunset; or keep the single form with k ≈ 1.2. Moon by K&S's phase law, 0.27 lux full. **[SRC]/[INFERRED]**
3. **The night bench (`NightHistory.cs`, the night-* scenarios).**
   - Southampton: five German ships (four on her), 90 casualties, the hot exchange ~4 min within ~15. Campbell credits
     Black Prince with two 6-inch hits on Rheinland at ~23:35 GMT, about 35 minutes before she was sunk [UNCERTAIN];
     "took them for friends" is a hypothesis.
     The 4th Flotilla fired about 14 torpedoes for 1 hit; the British night total about 37 for 3. Dover 20-21 April
     1917: replace "about 600 yd" with point blank (the Germans fired first; funnel flares seen 10 s later; the last
     torpedo at 200 yd). Tsushima: 54 torpedoes plus 32 linked mines for 4-6 hits; 3 boats lost (2 gunfire, 1
     collision). **[SRC]**
   - **Check the Jutland cases' geometry:** the record has the Germans W to NW of the British rear and the British
     looking toward the northern glow (they saw first). The bench has the Germans seeing first by looking north. If the
     scenarios' relative positions are right, the arch's bearing or the silhouette's side is wrong; if not, the
     scenarios are. **[INFERRED]**
   - Add the light by time: the record's range grows from ~0.6-1.5 km at −11.5° to 1.7-3.5 km at −6° and 3.7 km at −3°
     (12th Flotilla, Moresby) in mist of about 3-4 miles at dawn; a 01:45 case would test the twilight's other side.
4. **Searchlight templates (`SearchlightTemplates.cs`).** Victorian: count 6; continental 1890s battleships 90 cm,
   RN 24-in. Pre-dreadnought 1904 (Russian): 75 cm × 6 with remote (electric) control; small light 40 cm. RN 1908-15:
   twin 24-in mountings (the 1914 template's 36-in starts mid-1915); RN remote control from 1915-17, general by 1918.
   Plain-arc spread 2.5-3° (not 2°); the German navy kept the plain arc (no high-intensity); the German 110 cm × 8 is
   unconfirmed. USN high-intensity from 1915-18 is right; its draw 18-19 kW from the bus. Ready means the arc burning
   behind a closed iris and the light trained on the contact (the German method); exposure takes about a second.
   **[SRC]**
5. **Star shell (`ShellTemplates`, `StarShellSystem`).** Fire star rounds at their own reduced velocity (15 cm Lg 500 m/s,
   10.5 cm 450 m/s, 8.8 cm 650 m/s) with their own range table and time of flight. German 1916 burn ~15 s (10-25)
   unless parachuted. An RN family with star shell from 1917; the USN by 1922. Doctrine: the German 1916 preset is
   "to find" (search) then lights. **[SRC]**
6. **The challenge and friend or foe (Q6).** The German challenge is coloured lamps shown all round; the British a
   lettered lamp. "Unanswered → foe" informs the picture but the decision to fire stays the doctrine's (the British
   line held fire on wrong replies). Model a partial or spoofed reply that causes doubt (Castor's two correct signs,
   Flirt's echoed challenge, Lynx's copied reply). A raider's doctrine never challenges. **[SRC]**
7. **Doctrine presets (Q7, one a period).** RN 1914-16: searchlights never or when fired on (poor lights), no star shell,
   challenge then hold fire, the battle line keeps dark and doesn't challenge, no fleet night action, destroyers astern.
   RN 1917-18: lights to fire, star shell, screens fire without challenge where the enemy is expected. Germany 1914-16:
   lights to fire (trained dark, then light and fire together), star shell to find, recognition lights, cruisers may
   fire torpedoes dark. Russia 1904-05: lights to search (the record's mistake; Nebogatov's ships kept dark). Japan
   1904-05: torpedo craft attack after moonset or before moonrise, from two sides, to ≤600 yd. **[SRC]**
8. **The AI.** Raiders and flotillas pick the moonless hours (every Dover raid, every 1891-1905 attack). A ship lit at
   night is fired on within seconds and torpedoed within minutes; a ship keeping dark in the same line is left alone:
   the bench should show both. Fixed shore lights with guns hold boats beyond 3 miles (a coastal-defence item for later).
   **[SRC]/[INFERRED]**
9. **Collisions (4.11).** Two of Jutland's eleven night losses were collisions and one flotilla action had four; Port
   Arthur and Tsushima had more. Night close order makes 4.11 more pressing, as night.md says. **[SRC]**
10. **Funnel flare and phosphorescence** as lights a ship makes at speed: funnel flare from forced coal or oil
    burning (enough to fight by at close range on a clear moonless night), and a phosphorescent bow wave, wake and
    torpedo track on nights when the sea glows (a scenario flag). **[SRC]/[EST]**

## Open questions

- German WWI searchlight sizes and counts by class (Nassau to Derfflinger), the Scheinwerferleitstand and its drill; the
  German star shell's candle, burn time, parachute and outfit; the Schießvorschrift. Leads: O'Hara and Hone (eds.),
  *Fighting in the Dark: Naval Combat at Night 1904-1945* (2023), chapter 2; Groos, *Der Krieg in der Nordsee* vol. V;
  Campbell, *Jutland* (night round counts).
- The RN's post-Jutland reforms in detail (Grand Fleet night-fighting committee, the Searchlight Control Sight, 1917
  star shell calibres, the new recognition system): Marder III-IV; Brooks 2016; the Naval Review archive.
- Makarov's *Discussion of Questions in Naval Tactics* (1897) on lights and darkening; Japanese written night doctrine
  1894-1905; French, Austro-Hungarian, Italian and USN doctrine and exercises.
- Measured beam candlepower and divergence of any 1890-1918 naval projector (Rey, *The Range of Electric Searchlight
  Projectors*; Blondel).
- The Yellow Sea night's torpedo count; the Baltic raid of 10-11 Nov 1916 and Moon Sound 1917; the Black Sea; the
  Italian Dardanelles raid of 1912; 21 Mar 1918 (Botha).
- Brown's 1952 BuShips illumination curves and Rozenberg's twilight azimuth data, to check the Koomen decoding.
- A gun flash's retinal energy at a bridge, to place own-flash dazzle on the flash-blindness curves; the sea's
  luminance below the horizon at night (the hull's contrast against the sea).
- Night cloud at Jutland; the moon in "Fighting in the Dark"'s other cases.

## Sources

The full numbered lists are in each appendix; these are the ones the note leans on most.

- U.S. Navy, Bureau of Steam Engineering, *Searchlights and Signal Lights* (1918) [A1]: https://distantreader.org/stacks/trust/uc2/uc2_ark+=13960=t8cg00d51.txt
- J. Heinz, "Electric Search Lights at Sea", *Proceedings USNI*, Oct 1894 [A2]: https://www.usni.org/magazines/proceedings/1894/october/electric-search-lights-sea
- A. K. Blackley, "Lighting the Night", *Naval History*, June 2026 [A3]: https://www.usni.org/magazines/naval-history/2026/june/lighting-night
- W. Fawcett, navy searchlights, *Popular Electricity*, Feb 1914 [A4]: https://worldradiohistory.com/Archive-Popular-Electricity/PopElec-1914-02.pdf
- The Dreadnought Project, St Vincent, Colossus, Dreadnought pages [A13][A14][A16]: https://www.dreadnoughtproject.org/tfs/index.php/H.M.S._Dreadnought_(1906)
- *Battle of Jutland: Official Despatches with Appendices* (1920) [B1][D1][E2-2]: https://archive.org/details/battleofjutland300grearich
- H. W. Fawcett and G. W. W. Hooper, *The Fighting at Jutland* (1921) [E2-7]: https://archive.org/details/fightingatjutlan00fawcuoft
- J. Brooks, "British Destroyers at Jutland: Torpedo Tactics in Theory and Action", *BJMH* 3(3), 2017 [E2-1]: https://bjmh.gold.ac.uk/index.php/bjmh/article/view/757
- J. R. Jellicoe, *The Grand Fleet 1914-1916* (1919) [D3][E2-8]: https://archive.org/details/grandfleet19141900jell ; *The Crisis of the Naval War* (1920) [B2]: https://archive.org/details/crisisofnavalwar00jelluoft
- R. Scheer, *Germany's High Sea Fleet in the World War* (1920) [D4]: https://archive.org/details/germanyshighseaf00sche
- J. S. Corbett, *Naval Operations* vols. I-III; H. Newbolt, vols. IV-V [D2][E3-5][B4]: https://archive.org/details/navaloperations03corb ; https://archive.org/details/navaloperations04corb ; https://www.naval-history.net/WW1Book-RN5b.htm
- R. H. Bacon, *The Dover Patrol 1915-1917* [E3-4]: https://archive.org/details/doverpatrol1915102baco ; E. R. G. R. Evans, *Keeping the Seas* (1920) [E3-6]: https://archive.org/details/keepingseas00moun
- W. James, "Night Fighting at Sea by the British Navy", *Proceedings USNI*, June 1944 [C1-18][D14]: https://www.usni.org/magazines/proceedings/1944/june/night-fighting-sea-british-navy
- Report of the International Commission of Inquiry, North Sea incident, *FRUS* 1905 [D5]: https://history.state.gov/historicaldocuments/frus1905/d400
- V. Semenoff, *Rasplata* (1910) [E1-18]: https://archive.org/details/cu31924023164985 ; A. Novikoff-Priboy, *Tsushima* [E1-20]: https://archive.org/details/tsushimaanovikof008255mbp ; N. Klado et al., *The Battle of the Sea of Japan* (1906) [E1-21]: https://archive.org/details/battleofseaofjap00klad
- H. W. Wilson, *Ironclads in Action* vol. 2 (1897) [E1-1]: https://archive.org/details/ironcladsinactio02wilsuoft
- "Torpedo and Mine Effects in the Russo-Japanese War", *Proceedings USNI*, Oct 1907 [A29][C1-35]: https://www.usni.org/magazines/proceedings/1907/october/torpedo-and-mine-effects-russo-japanese-war
- F. J. Cleary, "Optical Principles and the Design of Telescopes for Night Use", *Proceedings USNI*, Sept 1914 [C1-3]: https://www.usni.org/magazines/proceedings/1914/september/optical-principles-and-design-telescopes-night-use
- P. Abrahams, notes on binocular history [C1-1]: https://www.dehilster.info/docs/PeterAbrahams/trsg43.txt
- USN *Lookout Manual* (1943) [C1-11]: https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/l/lookout-manual-1943.html
- M. J. Koomen et al., "Measurements of the Brightness of the Twilight Sky", *JOSA* 42, 353 (1952) [C2-3]: https://static1.squarespace.com/static/54694fa6e4b0eaec4530f99d/t/5e876886cdfbaa441f538327/1585932427823/Measurements+of+the+Brightness+of+the+Twilight+Sky+1952.pdf
- P. Schlyter, "Radiometry and photometry in astronomy" [C2-1]: https://stjarnhimlen.se/comp/radfaq.html ; Janiczek and DeYoung, USNO Circular 171 (1987) [C2-2]: https://apps.dtic.mil/sti/pdfs/AD1112511.pdf
- A. Jechow, F. Hölker, C. Kyba, "Using all-sky differential photometry to investigate how nocturnal clouds darken the night sky", *Sci. Rep.* 9, 1391 (2019) [C2-6]: https://pmc.ncbi.nlm.nih.gov/articles/PMC6361923
- K. Krisciunas and B. Schaefer, "A model of the brightness of moonlight", *PASP* 103, 1033 (1991) [C2-9]
- A. Crumey, "Human contrast threshold and astronomical visibility", *MNRAS* 2014 [C2-10]: https://arxiv.org/pdf/1405.4209 ; J. H. Taylor, SIO Ref. 60-25 (1960) [C2-11]: https://misclab.umeoce.maine.edu/education/VisibilityLab/reports/SIO_60-25.pdf
- H. A. Knoll, R. Tousey, E. O. Hulburt, "Visual Thresholds of Steady Point Sources of Light …", *JOSA* 36, 480 (1946) [C2-14]; IALA Recommendation E-200-2 [C2-15]
- R. S. Czeh et al., *A Mathematical Model of Flashblindness* (1965) [C2-19]: https://apps.dtic.mil/sti/pdfs/AD0627332.pdf ; J. D. Teresi, *A Review of Research on Flash Blindness* (1968) [C1-37]: https://apps.dtic.mil/sti/pdfs/AD0840277.pdf
- navweaps.com, German 15 cm, 10.5 cm and 8.8 cm guns (star shell rounds) [B5-B9]: https://navweaps.com/Weapons/WNGER_59-45_skc16.php
- "Star Shell Replaces Navy Searchlight" (March 1922) [B12]: https://mobicore.dissgea.unipd.it/s/WarWon/item/7335
- USN *War Instructions* (1944), ch. 8 "Night Actions" [B26]: https://ibiblio.org/hyperwar/USN/ref/WarInst/WarInst-8.html
- Sun and moon: PyEphem 4.2.1, [night-fighting/case_light.py](night-fighting/case_light.py) and the appendices' scripts
