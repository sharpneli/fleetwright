# Whose Salvo? Telling Your Own Fall of Shot from Another Ship's on the Same Target (1905-1925 first)
Status: draft    Updated: 2026-11-18    Request: [REQUESTS.md "Telling your own salvo from another ship's on the same target"](../../REQUESTS.md) (the code: [docs/battle/shell-consumption.md](../../../docs/battle/shell-consumption.md) SC2, `SimSettings.SplashConfusion`, `SalvoIdentifiedShells`, `SplashWindowMs`, `SplashCalibreBand`, `SplashNearM`, `SplashMisreadShare`, `GunnerySystem.Told`)

*Three questions: (1) how often spotters lost their own salvo or took another ship's for it, and how that went with the number of ships on the target, their calibres, the salvos' timing and the range; (2) what the navies did about it; (3) whether a salvo of three or more shells was ever reliably known for one's own by its pattern or timing alone. No navy's count of misidentified salvos was found. The answers rest on the record's cases, the navies' orders and their own war-game factors, and a small timing model checked against those factors.*

Tags as in the other fire-control and damage notes: **[SRC]** sourced (URL in the Sources), **[INFERRED]** my reasoning or a fit, **[EST]** an estimate with no direct source, **[UNCERTAIN]** thin or conflicting sources.

**Companion code:** [`whose_salvo_ref.py`](../whose_salvo_ref.py) (standard library, under a second). S1: the chance of telling a salvo by ships on the target, timing and confusion constant; S2: sequence firing's rate cost; S3: the told share against the RN's 1922 war-game factors. Every derived number below comes from it.

---

## Summary

1. **A salvo of three or more was not always told, so the code's free pass is wrong.** Spotters took another ship's salvo for their own in every recorded case where two ships of near the same calibre fired on one target with no timing scheme in force:
   - Tiger on Lion's 13.5 in splashes at the Dogger Bank (1915): her fall was about 3,000 yd beyond Seydlitz, and she didn't use her time-of-flight arrangements [SRC].
   - Tiger (13.5 in) and New Zealand (12 in) on Moltke in Jutland's run to the south (1916): Brooks thinks each confused the other's spotting [SRC, his inference]. No 12 in hit on any German ship is recorded for that hour [SRC].
   - Ajax took Achilles' 6 in salvos for her own for 25 minutes at the River Plate (1939) [SRC].
   - King George V ranged by radar on Rodney's splashes for some twenty minutes on 27 May 1941 [SRC].

   Where the ships had a timing scheme, no confusion is reported:
   - The Grand Fleet's pairs: the 1916 Battle Orders call the mutual interference practically negligible [SRC, via a forum transcription].
   - Hood and Prince of Wales firing in time slots at the Denmark Strait [SRC].
   - The German battlecruisers' concentrations: three ships on Lion at the Dogger Bank (16 heavy hits), Seydlitz and Derfflinger on Queen Mary at Jutland [SRC for the hits; that the Germans had no confusion is my reading of the silence, UNCERTAIN].

2. **What decided it was time, then calibre; pattern hardly at all.**
   - **Time:** a heavy column stands about 10-15 s, a medium one 3-5 s [SRC, Soviet 1927 rules].
     - Without a clock a spotter needed the ships' salvos 15-20 s apart (Bernotti, 1914) [SRC].
     - With a time-of-flight clock or buzzer actually used, 5 s was enough (the USN's 1930s rule) [SRC].
   - **Ships:** two ships could be timed apart, three or more could not, unless each fired more slowly. That is why every navy's doctrine stopped at pairs.
   - **Calibre:** a big difference helped. A 12 in against a 13.5 in, a 14 in against a 16 in, didn't.
   - **Range:** it worked through the time of flight. Long range means more shells in the air, and overs were seldom seen at all, so shorts were "the only guide" [SRC]. At short range a hit's flash identified the range for every ship at once.

3. **What the navies did:**
   - **Doctrine:** pairs. Italy (Bernotti 1914): two at most [SRC]. Grand Fleet: pairs on the enemy's van in 1914, pairs "the most satisfactory" in September 1915, threes and fours only in good conditions in 1916 [SRC].
   - **Timing:** time-of-flight watches, clocks and buzzers. The RN called them fundamental by 1915 [SRC]. The German electric fall-of-shot indicators were unreliable, Brooks says [SRC]; Lützow's was in use at Jutland [SRC].
   - **Sequence firing:** the RN's GIC, ships firing in turns in fixed slots. It was standard for capital-ship divisions after Jutland and for light cruisers after November 1917 [SRC]. US battleships with the Grand Fleet fired pairs in half-minute sectors in March 1918 [SRC]. It cost rate, not accuracy [SRC].
   - **Master-ship control:** one ship spotting for all.
     - The Russian Black Sea Fleet used it at Cape Sarych in 1914 [SRC].
     - The USN's massed-firing trials had all but been given up by early 1918 [SRC].
     - The RN made it standard only by 1939, and even then preferred GIC for 16 in ships, because nine simultaneous 16 in splashes were hard to spot [SRC].
   - **Dye:** none before 1930 [SRC]. French post-war work; the USN from 1930; the IJN from 1941; the RN's K shells from late 1942 [SRC].
   - **War-game factors:** the RN's 1922 rules counted three ships on one target as 2.5 and four as 3 [SRC]. The USN's fleet-manoeuvre rules of about 1908 counted three on one as worth less than two [SRC].

4. **Proposed model** [EST, checked in S3]. For a salvo of any size, tell it with chance 1/(1 + c·k):
   - k is the other ships' salvos of its calibre band landing near her within ±W of its own.
   - W is 15 s without a clock in use and 5 s with one.
   - c is 0.6, the code's constant for lone shells.
   - Two ships firing heavy salvos every 40 s at a random phase then tell 0.91 of their salvos with a clock and 0.72 without; three tell 0.83 and 0.55.
   - **Check:** under a clock, the told share times the ships comes to ×1.81, ×2.48 and ×3.02 for two, three and four ships. The RN's 1922 factors are ×2, ×2.5 and ×3. The fit is closer than an estimate deserves, so treat it as a sanity check, not a calibration [INFERRED].
   - **Misreads:** a misread should take the other ship's fall, not a coin flip. That alone gives the record's sticky mistakes: a ship that reads her consort's straddle as her own holds her wrong range (Tiger).

---

## 1. What makes a splash yours

A spotter has four cues: **when** it lands, **where** (its line and range about the target), **how big** (the calibre), and **how many** (the salvo's count of shells).

- **When.** A heavy column rises in 2-3 s and stands about 10-15 s for 305 mm, 3-5 s for medium calibres (Soviet *Rules of fire control for naval targets*, 1927, as quoted by a Russian analyst) [SRC, secondary].
  - The code's `SplashColumn.LifeS` gives a 13.5 in column about 8 s, its dense shaft about 4.8 s [INFERRED from the code].
  - Within one salvo the shells land within a second or so of each other (a director salvo; triple turrets' delay coils) [SRC: shell-splashes.md].
  - Two ships' salvos are told apart by time only if their columns don't stand together, and only if the spotter knows when his own is due. Without a clock he knows that to within several seconds. With a time-of-flight clock started at the gun and a buzzer at the fall, to about a second.
- **Where.** Ships in line fire from different bearings, so their salvos' lines differ a little at the target. Bernotti worked a case: two ships 500 m apart at 8,000 m put their splashes less than 20 m apart [SRC]. Two ships' falls on the same target land on top of each other.
- **Size.** Column height goes about with calibre (the code: 180 calibres), so a 12 in and a 13.5 in differ by 12% and a 14 in and a 16 in by 14% [INFERRED from the code]. One column beside another might show that difference. A column seen at another moment, at 15-20 km in haze and smoke, would not.
  - The all-big-gun ship's case was in part that the splashes of mixed calibres (12 in, 9.2 in, 6 in) were hard to interpret [SRC].
  - 15 in against 8 in is plainly different, yet Prinz Eugen's gunnery officer at the Denmark Strait wasn't sure the first splashes he saw were his, and fired a second full salvo to see [SRC].
- **Count.** A spotter counted the shorts and took the shells he couldn't see as hits or overs. Overs beyond a large target "could seldom be observed", so the spotter had to know how many guns had fired [SRC].
  - Another ship's shorts counted as one's own break this at once.
  - The RN still held in 1939 that shorts are often the only guide [SRC].
  - A 2019 *Naval History* account of the River Plate adds a trap: in a large combined fall the shorts hide the overs, and a straddle reads as all short [SRC].

**So a salvo's identity is mostly its time.** Pattern and count are spoiled by the other ship's salvo landing in the same few seconds. Size helps only across a big calibre gap. Line helps hardly at all [INFERRED].

---

## 2. The cases

| Year, action | On the target | Calibres | Timing aids | Range | What happened | Tag |
|---|---|---|---|---|---|---|
| 1905 Tsushima | Most of the Russian line on Mikasa | 12 in, 10 in, 6 in mixed | Stopwatches at most | 5-7 km | Russian reports say own splashes couldn't be picked out even with stopwatch timing; Orel couldn't hit Mikasa and shifted to the nearest armoured cruiser. All six Russian hits of the first ten minutes were on Mikasa, 13 of 20 in the next ten. | [SRC secondary, Rytik; UNCERTAIN] |
| 1905 Tsushima | The Japanese line, mostly on Suvorov, then Oslyabya, then Alexander III | 12 in, 8 in, 6 in mixed | — | 5-6 km | Each Japanese ship chose her own target and changed it if she couldn't see her own splashes. Their shells' visible bursts let gunners watch for hits rather than splashes. | [UNCERTAIN: the analyst's uncited "information I have"] |
| 1914 Falklands | Invincible and Inflexible, at times on the same ship | 12 in, 12 in | Not recorded | 12-15 km | Smoke down the range made spotting difficult; a large spread, slow and ragged fire. Splash confusion isn't named in what was reached. | [SRC for the smoke; confusion not found] |
| 1914 Cape Sarych | Russian Black Sea pre-dreadnoughts, master-ship control from Ioann Zlatoust | 12 in | Radio from the master ship | ~4-7 km, fog | The concentration was built to avoid the confusion; it failed for want of sight (the master ship couldn't see Goeben), not of splashes. | [SRC] |
| 1915 Dogger Bank | Lion and Tiger on Seydlitz | 13.5 in, 13.5 in | Tiger didn't use hers | ~16-18 km | Tiger took Lion's splashes for her own; her fall about 3,000 yd beyond Seydlitz. Beatty also blamed her want of the short shots that control needs. | [SRC] |
| 1915 Dogger Bank | Seydlitz, Moltke, Derfflinger on Lion | 11 in, 11 in, 12 in | Electric fall-of-shot indicators | ~16-18 km | Lion took 16 heavy hits. Brooks has Seydlitz and Moltke firing about 14 rounds a minute against Lion's 2; the Germans spotted single salvos until straddling, then fired rapid unspotted salvos. | [SRC] |
| 1916 Jutland, run to the south | Lion and Princess Royal on Lützow | 13.5 in, 13.5 in | Grand Fleet procedures | 15-18 km | Four hits on Lützow from their 1,250 lb shells in 66 minutes. No confusion is recorded; smoke is. | [SRC for hits; confusion not found] |
| 1916 Jutland, run to the south | Tiger and New Zealand on Moltke | 13.5 in, 12 in | As above | 15-18 km | Brooks thinks each ship's fall may well have confused the other's spotting. The battlecruisers' 12 in guns made no recorded hit in that hour. | [SRC: Brooks n. 66, an inference; hits SRC] |
| 1916 Jutland, run to the south | Seydlitz and Derfflinger on Queen Mary | 11 in, 12 in | Fall-of-shot indicators | ~13-15 km | Queen Mary destroyed in about ten minutes of their joint fire. No confusion is recorded. | [SRC for the hits; UNCERTAIN on the spotting] |
| 1916 Jutland | II Scouting Group on Chester | 15 cm × 4 ships | — | close (not found) | About 19 hits in five minutes: concentration of one calibre worked close in. | [UNCERTAIN: a forum's summary] |
| 1918 Pentland Firth (practice) | New York + Texas, Florida + Delaware in pairs | 14 in + 14 in, 12 in + 12 in | Half-minute sectors of mean time | 11.8-12.9 kyd | The pairs fired "without clashing" in their sectors. The spotting was poor for low visibility and large patterns, not for each other. | [SRC] |
| 1939 River Plate | Ajax and Achilles in concentration | 6 in, 6 in | Radio-linked concentration | > 15 kyd | Achilles dropped out without Ajax's gunnery officer knowing, and he took her salvos for his own: 25 minutes of misdirected fire. | [SRC] |
| 1941 Denmark Strait | Hood and Prince of Wales; Bismarck and Prinz Eugen on Hood | 15 in, 14 in; 15 in, 8 in | British time slots | ~20-24 km | The British fired in assigned slots; no confusion reported. Prinz Eugen's officer unsure the first splashes he saw were his. | [SRC] |
| 1941 Bismarck's end | KGV and Rodney | 14 in, 16 in | Radar on KGV | 15 → 3 km | KGV's Type 284 ranged on Rodney's splash echoes until 09:10. Rodney fired under optical control throughout. | [SRC] |

**Reading the cases** [INFERRED]:

- **Same or near calibre, no timing scheme, long range: confusion every time it's looked for.** In Tiger's and Ajax's cases it lasted most of the action, or 25 minutes, and it was a misread, not a doubt. The spotter thought he was on.
- **A timing scheme or a drilled routine: no recorded confusion**, even with three ships (the Germans on Lion).
  - The Germans' routine was the one that saves it: single salvos spotted one by one while ranging, rapid unspotted salvos once straddling [SRC].
  - Once straddling, the spot hardly matters, and hits show.
- **Short range: concentration worked** whatever the calibres (Chester, Tsushima's Japanese, Defence and Warrior under several German ships). The range was nearly a rangefinder's, and hits were seen.
- **The German fire on Defence and Warrior** was asked about. In what was reached, there's no report of German spotters losing their fall on them. They came suddenly into view of the I Scouting Group (Paschen), Lützow is sometimes credited with Defence, and Defence blew up within minutes [SRC for the facts]. How many German ships fired on each, and how their spotters fared, is a gap.

---

## 3. What the navies did

### 3.1 Royal Navy

- **1913:** Home Fleet trials of "cruiser arc lamps" to signal range, rate and target between ships [SRC: Dreadnought Project].
- **1914:** the first Grand Fleet Battle Orders (signal of 15 August 1914) had the leading division concentrate its four leading ships in pairs on the enemy's two van ships [SRC, forum transcription].
- **September 1915:** a Grand Fleet Gunnery and Torpedo Order (Nos. 65-66):
  - concentration "in pairs has given the most satisfactory results" and was to be the usual way [SRC];
  - threes and fours were possible if more confusion was accepted [SRC];
  - director firing and time-of-flight watches were the foundation of keeping the spotting straight [SRC].
- **1916 GFBOs:** pairs give the best results, their mutual interference "practically negligible"; three or four ships only in conditions good enough to spot and keep the range [SRC, forum transcription].
- **Before Jutland the Grand Fleet practised both ways,** individually and concentrating in pairs [SRC: Brooks n. 54].
- **Dogger Bank's lesson as Beatty and Chatfield drew it:**
  - Tiger's fire was poor for want of shorts and because she didn't use her time-of-flight arrangements [SRC: Brooks n. 15].
  - "Shorts are the only guide" [SRC: Chatfield, Brooks n. 16].
- **After Jutland:**
  - The September 1916 Spotting Rules [SRC].
  - Sequence firing (GIC, from its signal) standardised for capital-ship divisions [SRC].
  - Extended to light cruisers after the November 1917 action in the Bight [SRC].
  - Brooks quotes the Grand Fleet's judgment that the spotting rules and then concentration of fire dominated its gunnery work in 1916-18 [SRC].
  - The range plot's new use: recording the ranges consorts were using in concentration [SRC: GFG&TOs, Brooks nn. 185-186].
  - August-October 1917: divisional and squadron firings by the 1st BCS and the 1st, 4th and 5th Battle Squadrons tried signalling methods; range dials high forward and low aft did best [SRC: Dreadnought Project].
- **1922 war-game rules:** concentration not additive beyond pairs; three ships about 2.5 times one, four about 3 times [SRC, forum transcription].
- **1929 tactical rules (C.B. 3011):** a 25% accuracy bonus for concentration fire beyond 15,000 yd [SRC]. That is master-ship fire, all shells landing as one group.
- **1939 (Progress in Naval Gunnery):**
  - Master-ship control (GMS) is the standard for like ships, GIC for unlike ones [SRC].
  - Nelson and Rodney's long-range pair firing confirmed GIC for 16 in ships, because accurate spotting of nine simultaneous 16 in splashes was hard [SRC].
  - The C-in-C Mediterranean found that a three-ship GIC concentration doesn't develop full output [SRC].
- **Fall-of-shot hooter:** by WWII a "fall of shot hooter" in the directors [SRC, forum citing Simanton]. Progress in Naval Gunnery 1939 asks for "an accurately worked time of flight hooter" to stop ricochets being taken for the fall [SRC].
- **Dye:** the RN didn't adopt it until after WWII began [SRC]. "K" shells reached the fleet from late 1942 [SRC], on the French "Dispositif K" passed over in 1939 [SRC, secondary].

### 3.2 Germany

- **Hit indicators:** electric devices buzzed when a salvo was due, but Brooks judges them far from reliable [SRC: Brooks n. 41].
  - Lützow's was in use at Jutland: her gunnery officer's account has the indicator screeching as the columns rose [SRC: Paschen].
  - Seydlitz reported after Jutland that regular shooting by electric clock was impossible in frequent manoeuvres [SRC: Brooks n. 46].
- **Fire control:** single salvos to find the target, each spotted before the next; rapid unspotted salvos once consistently straddling [SRC: Brooks n. 42].
- **German concentration doctrine:** in what was reached, nothing comparable to the Grand Fleet's written rules. The I Scouting Group concentrated readily: on Lion in 1915, on Queen Mary in 1916 [SRC for the acts].
- **Dye:** pink dye in WWII shells is mentioned, and doubted, in a camouflage story [UNCERTAIN].

### 3.3 Russia

- **1904-05 debate** [SRC secondary: a Russian analyst summarising the instructions]:
  - Myakishev (Pacific Squadron): the flagship ranges, passes the range down the line, and the ships fire broadside volleys to kill. He thought a ship could tell her own volleys at 30-40 cables.
  - Grevenitz (Vladivostok cruisers) held that with several ships on one target it would be impossible to tell one's own fall, so he wanted the squadron split, each detachment on its own target.
  - Rozhestvensky's Order No. 29 concentrated the whole squadron on a signalled target "if possible".
- **Black Sea, pre-war:** the Black Sea Fleet built master-ship control for its three newest pre-dreadnoughts, with extra radio to pass range and deflection. Ioann Zlatoust controlled Evstafi and Pantelimon at Cape Sarych (1914) and the Bosporus (1915) [SRC]. It is the period's only fleet use of single-ship control of a concentration found.

### 3.4 Italy, Japan, the United States

- **Italy (Bernotti, 1914)** [SRC]:
  - The most efficient concentrating group is two ships.
  - Successive ships' salvos must be 15-20 s apart or they mix within a few rounds, because corrections and each salvo's few seconds of spread make the timing drift.
  - With each ship's salvo interval T and the ships' interval t, at most T/t ships can concentrate: two at the period's rates.
- **Japan:** each ship chose her own target and changed it when she couldn't see her own splashes [UNCERTAIN, uncited]. Dye from 1941 (Kongō red, Hiei black, Kirishima blue, Haruna none) [SRC, secondary].
- **United States:**
  - About 1908 the fleet-manoeuvre rules made three ships on one count less than two, because of spotting interference. The game board let concentration add up [SRC: Niblack and Hoff in discussion of Pye 1908].
  - March 1918, with the Grand Fleet: pair concentration in half-minute sectors (above).
  - Early 1918: Kimmel told the General Board massed firing had been practically given up as a failure, while sequence firing showed promise [SRC].
  - 1930: splash colours first used in Force Battle Practice [SRC].
  - Late 1930s battleship doctrine without dye [SRC, forum transcription]:
    - the ships fired in rotation;
    - no ship to wait much more than half a salvo interval for the one before;
    - none to fire within 5 s of another's salvo.
  - 1944 War Instructions: a concentration's fire is usually less than the sum of the ships firing singly [SRC].

### 3.5 Dye, by navy

| Navy | First use | Note |
|---|---|---|
| France | after 1918 (date not found) | dye bag in the cap with a small burster; "Dispositif K" passed to the RN 1939 [SRC secondary] |
| USN | 1930 (Force Battle Practice) | dry powder in paper bags between cap and windscreen; one colour a ship [SRC] |
| IJN | 1941 | one colour a ship [SRC secondary] |
| RN | late 1942-early 1943 | "K" shells with a small fuze and burster; colours by ship in 1946 [SRC] |

**For 1905-1925 there was no dye anywhere.** The only aids were time and organisation [SRC].

---

## 4. How many on one target, by doctrine

| Source | Year | Ships on one target | Why |
|---|---|---|---|
| USN fleet-manoeuvre rules | c. 1908 | 3 count less than 2 | spotting interference [SRC] |
| Bernotti (Italy) | 1914 | 2 | salvos 15-20 s apart at most T/t ships [SRC] |
| GFBO, Grand Fleet | 1914 | pairs on the van | [SRC, forum] |
| GFG&TO 65-66 | 1915 | pairs usual; 3-4 with more confusion | [SRC] |
| GFBO | 1916 | pairs best; 3-4 in good conditions | interference practically negligible in pairs [SRC, forum] |
| RN after Jutland | 1916-17 | pairs in GIC | [SRC] |
| RN war-game rules | 1922 | 3 ≈ 2.5×, 4 ≈ 3× | [SRC, forum] |
| RN Progress in Naval Gunnery | 1939 | pairs; three-ship GIC loses output | [SRC] |
| USN battleship doctrine | late 1930s | rotation, 5 s apart | [SRC, forum] |

---

## 5. Was a salvo ever known by pattern or timing alone?

**By pattern alone: no case found, and the record has the contrary.**
- Tiger's salvos and Ajax's were taken for another ship's of the same calibre [SRC].
- The RN's own 1939 finding that a nine-shell 16 in fall was hard to spot shows a pattern was a burden on the spotter, not a signature [SRC].
- The code's `SalvoIdentifiedShells` 3 came from the manual-gunnery note's §5, which marked it as part of its model. It was an assumption, not a finding [INFERRED].

**By timing: yes, when the ships made the timing.** Each of these worked or is not reported to have failed:
- the Grand Fleet's pairs with director firing and time-of-flight watches (1915-16);
- sequence firing (1916 on);
- the US pairs' half-minute sectors (1918);
- Hood and Prince of Wales's slots (1941).

The condition is a clock the spotter uses, and other ships' salvos kept out of his window: 15-20 s apart without a buzzer, 5 s with one [SRC].

**By calibre:** a large gap is a cue, but not a sure one at long range (Prinz Eugen). A gap of 12-15% isn't one (Tiger and New Zealand; KGV and Rodney by radar) [INFERRED].

---

## 6. A model for the chance of telling one's own salvo

**The model** [EST unless tagged]:

- **Window.** The spotter looks for his salvo in a window of ±W about when it falls:
  - W ≈ 15 s if he has no time-of-flight clock, or doesn't use it. Bernotti's 15-20 s, and a heavy column's 10-15 s [SRC bounds].
  - W ≈ 5 s with a clock or buzzer in use. The USN's 5 s rule [SRC].
- **Other salvos.** Each other ship of his calibre band firing on her puts a salvo in his window with the share 2W/T, T being her salvo interval, its phase not his to choose.
  - 40 s is taken for deliberate heavy fire. Iron Duke's spotted salvos at Jutland were 35-50 s apart [SRC: Brooks].
  - 15 s for medium guns [EST].
- **Telling his own.** With k other salvos in his window he tells his own with 1/(1 + c·k).
  - c = 1 would be a pure guess among k + 1 groups.
  - c = 0.6, the code's constant for lone shells, leaves him something from line, count and size within the band.
- **Sequence firing.** No other salvo shares the window, and the cost is rate: each ship fires no faster than once in n·s seconds, s the slot. Bernotti's t of 15-20 s; the 1918 US pairs' 30 s.

**S1 (heavy guns, T 40 s): chance a salvo is told** [INFERRED: the model]:

| Others on her (same band) | No clock, c 0.6 | No clock, c 1.0 | Clock, c 0.6 | Clock, c 1.0 |
|---:|---:|---:|---:|---:|
| 0 | 1.00 | 1.00 | 1.00 | 1.00 |
| 1 | 0.72 | 0.62 | 0.91 | 0.88 |
| 2 | 0.55 | 0.44 | 0.83 | 0.77 |
| 3 | 0.45 | 0.33 | 0.76 | 0.68 |
| 4 | 0.37 | 0.27 | 0.69 | 0.61 |
| 5 | 0.32 | 0.22 | 0.64 | 0.55 |

**S1 (medium guns, T 15 s)** [INFERRED: the model]:

| Others on her (same band) | No clock, c 0.6 | Clock, c 0.6 |
|---:|---:|---:|
| 1 | 0.45 | 0.75 |
| 2 | 0.29 | 0.59 |
| 3 | 0.22 | 0.48 |
| 5 | 0.14 | 0.35 |

Medium guns suffer most, because their salvos come four times a minute and their windows fill. That fits Bernotti's and the RN's pairs, and the pre-war argument against mixed batteries.

**Ships of another calibre band don't count** [EST]. A ship's own other guns firing independently are still read as one group by her control officer (the code's rule; manual-gunnery §5).

**S3, against the RN's 1922 factors** [INFERRED]:

| Ships on the target | Told share (clock, W 5 s, c 0.6) | × ships | 1922 rules | Told share without a clock | × ships |
|---:|---:|---:|---:|---:|---:|
| 1 | 1.00 | 1.00 | ×1 | 1.00 | 1.00 |
| 2 | 0.91 | 1.81 | ×2 | 0.72 | 1.44 |
| 3 | 0.83 | 2.48 | ×2.5 | 0.55 | 1.66 |
| 4 | 0.76 | 3.02 | ×3 | 0.45 | 1.78 |

- **The fit.** The told share with a clock, times the ships, lands on the 1922 factors. A told share is not a hit share: a salvo not told is doubtful or misread, and a ship already straddling barely needs the spot. So read the agreement as "c 0.6 and W 5 s are of the right size for a drilled fleet with clocks", not as a calibration.
- **Without a clock** four ships do little better than two. That is the USN's c. 1908 finding [SRC].

**S2, sequence firing's rate cost** (T 40 s; the share of each ship's rate kept) [INFERRED]:

| Ships | slot 15 s | slot 20 s | slot 30 s | 1922 rules (per ship) |
|---:|---:|---:|---:|---:|
| 2 | 1.00 | 1.00 | 0.67 | 1.00 |
| 3 | 0.89 | 0.67 | 0.44 | 0.83 |
| 4 | 0.67 | 0.50 | 0.33 | 0.75 |

Pairs in 20 s slots cost nothing at deliberate rates. A third ship costs a third of everyone's rate, which is what the 1939 report found.

**Persistence** [INFERRED]. The three long misreads (Tiger, Ajax, KGV: 20 minutes to most of an action) were not bad luck salvo by salvo. A spotter who took a consort's straddling salvo for his own saw himself on, so he made no correction and stayed wrong. A model that reads the misread from the other ship's fall gives that without a stickiness constant.

---

## Recommendations for this codebase

All advisory; the numbers' tags as above.

1. **Drop the salvo's free pass** (`SalvoIdentifiedShells`).
   - In `GunnerySystem.Told`, a salvo of three or more counts the other ships' salvos, not shells: distinct shooters with falls of its calibre band near her within its window.
   - It is told with 1/(1 + `SalvoConfusion`·k), `SalvoConfusion` 0.6 [EST; S3's check]. 1.0 is the pure guess, for a harsh setting.
   - A salvo of one or two keeps today's 1/(1 + 0.6N) over the other ships' splashes.
   - Hits stay told, and point blank stays the layers' own.
   - This makes the heavy guns pay for crowding: today two battlecruisers on one ship cost nothing.
2. **The window by timing aid, not one number.** `SplashWindowMs` 3,000 → two settings:
   - about 15,000 ms with no time-of-flight clock in use [SRC bounds: Bernotti 15-20 s, a heavy column's 10-15 s];
   - about 5,000 ms with one [SRC: USN 5 s].

   The window is the spotter's uncertainty about his own salvo's moment, so it belongs to the fit and the crew:
   - a time-of-flight clock or buzzer as a fire-control fit: the RN's by 1915 (Tiger had "time-of-flight arrangements"); the Germans' electric indicators, with a reliability below one [EST];
   - a crew that may not use it: a green crew, as Tiger in 1915 [EST].

   Today's ±3 s is narrower than a heavy column's standing time (`SplashColumn.LifeS` about 8 s for 13.5 in), so even the drilled case is generous.
3. **A misread takes the other ship's fall.** When a salvo isn't told and `SplashMisreadShare` falls, read the sign (over, short, straddle) of the other ship's fall in the window from the ring's `splashAt` against her, not a coin flip.
   - The Tiger case then plays itself out: Lion straddling, Tiger reads a straddle and holds her range.
   - `SplashMisreadShare` 0.3 is untested by the record. The three cases are all misreads, but a doubt is never recorded, so it may stand; 0.5 for a green crew is defensible [EST].
4. **The calibre band.** `SplashCalibreBand` 0.15 covers 12 in against 13.5 in and 14 in against 16 in, which the cases need. Widening to 0.25-0.3 would cover 9.2 in against 12 in, which the all-big-gun argument says confused spotters [EST, UNCERTAIN]. Low priority.
5. **Sequence firing as a doctrine,** RN from 1916 for capital ships and November 1917 for light cruisers; pairs only:
   - the two ships fire in alternate slots of `SequenceSlotMs` about 20,000 [SRC: Bernotti's 15-20 s; the 1918 US pairs' 30 s];
   - their salvos are always told with a clock;
   - each fires no faster than once in two slots.

   Master-ship control is a 1930s fit, outside 1905-1925, except the Russian Black Sea Fleet in 1914.
6. **Dye as a later shell fit:** USN 1930, IJN 1941, RN late 1942. With dye, `SalvoConfusion` about 0.1 [EST], not zero: colours fade at long range and in poor light.
7. **How many on one,** for the allocation (docs/battle/allocation.md): the period's doctrine is pairs.
   - A third ship on one target loses about a fifth of each ship's spots with clocks and nearly half without (S3).
   - The captains could weigh that against leaving a ship unfired on, the Dogger Bank's and Jutland's other cost (Moltke free to fire on Lion, Derfflinger unengaged for ten minutes) [SRC].
8. **fc-history's concentration cases** (balance.md BL1g's to-do: the Yellow Sea, Tsushima, Rodney with KGV): run with their other shooters. Add the Dogger Bank's Tiger on Seydlitz with Lion, which should fall well short of Lion's own rate.

---

## Gaps and uncertainties

- **No counts.** No navy's tally of salvos misidentified under concentration was found. The GFG&TOs (ADM 137), the 1916 Spotting Rules (C.B. 272) and Schleihauf's "A Concentrated Effort" (*Warship International* 1998, No. 2) are where they'd be.
- **The GFBO wording** ("practically negligible") and the 1922 factors come through a games forum's transcription of the primary documents, not the documents themselves.
- **German doctrine** on concentration and the fall-of-shot indicator's working, and German spotters' accounts of the fire on Defence and Warrior, were not reached. Hase's book and the German official history are the places.
- **Tsushima:** the Russian "stopwatch" reports and the Japanese practice come through a Russian analyst's articles. Japanese primary sources not reached.
- **The Grand Fleet at Jutland's deployment** (many ships on Wiesbaden, König, Lützow): no ship report naming splash confusion was found in what was read.
- **Splash duration:** the 10-15 s for 305 mm is a 1927 Soviet manual through a secondary source. The code's 8 s is a scaling estimate.
- **The model:** its c, W and T are estimates. S3's agreement with the 1922 factors is one check against a war-game rule, itself an estimate.

---

## Sources

- Bernotti, R., "Concentration of Fire and the Numerical Strength of a Division", USNI *Proceedings*, March 1914 (trans. Gill and Westcott): https://www.usni.org/magazines/proceedings/1914/march/concentration-fire-and-numerical-strength-division
- Pye, W. S., "A Few Hints to the Study of Naval Tactics", USNI *Proceedings*, January 1908, with discussion by Alger, Hoff, Fiske and Niblack: https://www.usni.org/magazines/proceedings/1908/january/few-hints-study-naval-tactics
- Brooks, J., *Dreadnought Gunnery and the Battle of Jutland* (Routledge, 2005), chapters 2, 7 and 8 and their notes (n. 15-17, 22, 41-42, 46, 54, 66, 149, 173-188): https://vdoc.pub/documents/dreadnought-gunnery-at-the-battle-of-jutland-fire-control-and-the-royal-navy-1892-1919-cass-series-naval-policy-and-history-6aog02915tu0
- The Dreadnought Project, "Concentration" (GFG&TO 1915 Nos. 65-66, 1916 No. 132, 1917 No. 236; GIC, GMS; Kimmel 1918): http://www.dreadnoughtproject.org/tfs/index.php/Concentration
- The Dreadnought Project, "Spotting" (overs seldom seen, counting shorts, time-of-flight clocks): http://www.dreadnoughtproject.org/tfs/index.php/Spotting
- Old Dominion GameWorks forum, "Concentration Fire" (L. Gill, B. Angel; GFBO 1914 and 1916, RN 1922 war-game rules, RN Firing Manual 1939, USN battleship doctrine late 1930s): https://www.odgw.com/forums/topic/1044-concentration-fire/
- Rodman to Daniels, Battleship Division Nine pair concentration exercise, 20 March 1918 (NHHC): https://history.navy.mil/research/publications/documentary-histories/wwi/april-1918/rear-admiral-hugh-ro-0.html
- *Progress in Naval Gunnery 1939* (C.B. 3001/39), ADM 239/137, via the HMS Hood Association: http://www.hmshood.org.uk/reference/official/adm239/adm239-137.htm
- "A Battle Badly Fought" (River Plate), USNI *Naval History*, August 2019: https://www.usni.org/magazines/naval-history-magazine/2019/august/battle-badly-fought
- War Instructions, United States Navy, 1944 (NHHC): https://www.history.navy.mil/research/library/online-reading-room/title-list-alphabetically/w/war-instructions-united-states-navy-1944.html
- Navweaps tech-016 (Bismarck's last battle; KGV on Rodney's splashes): https://www.navweaps.com/index_tech/tech-016.php
- kbismarck forum (Hood and Prince of Wales's time slots, citing Leach, McMullen and PoW's gunnery report): https://kbismarck.org/forum/viewtopic.php?p=34374
- Prinz Eugen at the Denmark Strait (Jasper's second salvo): https://weaponsandwarfare.com/?p=12980
- Battle of Dogger Bank (1915), Wikipedia (Tiger on Lion's splashes, 3,000 yd; gunnery records): https://en.wikipedia.org/wiki/Battle_of_Dogger_Bank_(1915)
- Damage to major ships at the Battle of Jutland, Wikipedia (hits by period and calibre): https://en.wikipedia.org/wiki/Damage_to_major_ships_at_the_Battle_of_Jutland
- Paschen (Lützow) at Jutland, "SMS Lützow: The Skagerrak Battle": https://warhistory.org/2017/07/22/sms-lutzow-the-skagerrak-battle/
- Rytik/Military Review, "Woe from wit: on the methods of concentrating artillery fire on one target in the Russo-Japanese War": https://military-review.com/12480098-woe-from-wit-on-the-methods-of-concentrating-artillery-fire-on-one-target-in-the-russo-japanese-war
- Military Review, "Volley fire know-how of the Japanese fleet in Tsushima" (the 1927 Soviet rules' splash times): https://military-review.com/12479000-volley-fire-know-how-of-the-japanese-fleet-in-tsushima
- Russian battleship Evstafi and the Black Sea master ship, Wikipedia: https://www.wikipedia.org/wiki/Russian_battleship_Evstafi ; kchf.ru, Ioann Zlatoust: https://kchf.ru/eng/ship/predreadnoughts/ioann_zlatoust.htm
- Falklands 1914 (smoke and spotting), 1911 Britannica supplement: https://www.studylight.org/encyclopedias/bri/f/falkland-islands-battle.html
- Splash colours, USN (NavSource Iowa-class notes): https://www.navsource.org/archives/01/pdf/016292s.pdf
- naval gazing, "Spotting" (time-of-flight buzzer, dye, the British only in WWII): https://www.navalgazing.net/Spotting
- RN K shells and the 1946 colours (from navweaps' gun pages, quoted): https://www.devstrike.net/topic/1818-dye-cappedsplash-shells-are-shells-that-also-include-dyes-creating-areas-of-colored-water-if-they-miss/
- French "Dispositif K" and the RN: https://mechtraveller.com/?p=7687
- IJN dyes 1941 (Kongō class): https://groups.google.com/g/sci.military.naval/c/DlE9WHl2fdU
- Dreadnought, Wikipedia (mixed-calibre splashes and the all-big-gun case): https://en.wikipedia.org/wiki/Dreadnought
- In this repo: [manual-gunnery-research.md](../manual-gunnery-research.md) §5, [predreadnought-tech.md](predreadnought-tech.md), [radar.md](radar.md) §4, [../../vfx/shell-splashes.md](../../vfx/shell-splashes.md), `src/Fleetwright.Sim.Combat/SplashColumn.cs`.
