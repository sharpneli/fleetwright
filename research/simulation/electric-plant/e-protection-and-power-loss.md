# E. Warship electric systems 1890-1925: circuit protection, dynamo connection, and recorded losses of electric power in action
Status: draft    Updated: 2026-11-04    Request: [REQUESTS.md, 2026-11-04](../../REQUESTS.md); an appendix of [electric-plant.md](../electric-plant.md)

Scope note: about 22 searches and fetches. Read in full: two Institute of Marine Engineers histories of RN electrics (MacLaren 1961; Scott 1983), two USNI Proceedings articles (1907, 1919), and ship and battle pages for Dogger Bank, Szent Istvan, Marlborough, Lutzow, Seydlitz, Emden, Tsesarevich and Oryol. Campbell's *Jutland: an Analysis of the Fighting*, the Admiralty's 1916 damage reports, Doerry & Amy 2019, von Hase, Kostenko and the German official history were NOT reached online in this pass. Anything that rests only on them is in Gaps, not in Findings.

## 1. Protection and distribution practice by navy (fuses, breakers, parallel running, ring main vs radial, voltages, switchboards)

### Takeaway
The RN is well documented. It ran on 80 V DC from 1885 and 100 V from about 1900. HMS *Defence* (1908) was its first 220 V ship and introduced a watertight ring main in place of a single central switchboard. Automatic ring-main breakers to isolate damaged sections were part of that scheme, but discrimination between breakers was still poor in 1938 trials. The USN ran 125 V DC compound-wound generators by 1907, with equalizers for parallel running. By 1919 its ships had forward and after dynamo rooms, distribution rooms with switchboards, and "interlocking circuit-breaker relays", while lighting circuits were still fused. For German, Japanese, Russian and French practice, no primary source was reached.

### Cited Findings
**Royal Navy**
- First RN electric ship HMS *Inflexible* (1881): 800 V DC. Cut to 80 V DC in 1885, raised to 100 V DC in 1900. "Some larger ships went to 220 V d.c.", which stayed the standard for about 50 years. 440 V 60 Hz AC came in four Daring-class destroyers in 1946, following USN practice. — [Scott, "Electrical System Design for Warships", J. Naval Engineering 28(1), 1983](https://library.imarest.org/record/8911/files/v28b1p10a.pdf)
- Early ships put series lamps on an 800 V generator. After the first fatal accident within a year, 80 V became standard. In 1899 an electrician at Portsmouth recommended 100 V. By 1905 tests had shown "220-volt shocks were unpleasant but not dangerous", and HMS *Defence* (1908) became the first 220 V ship. — [MacLaren, "Electrical Engineering in the Royal Navy", IEE presidential address 1960, J. Naval Eng. 13(1), 1961](https://library.imarest.org/record/9717/files/v13b1p07a.pdf)
- *Defence* (1908) introduced a watertight ring main, "a fundamental departure from the simple central switchboard hitherto used". The ring-main gear had to be watertight so flooding would not short it. Ring-main breakers isolated damaged sections of the ring automatically. Wartime experience (WWII) showed the watertight seals rarely held in service. — [MacLaren 1961](https://library.imarest.org/record/9717/files/v13b1p07a.pdf)
- Plant scale: an 1880s first-class ship had three 200 A, 80 V dynamos; by 1900, 600 A dynamos were needed. — [MacLaren 1961](https://library.imarest.org/record/9717/files/v13b1p07a.pdf)
- Breakers' weakness, as a check: the 1938 *London* trials ran up to four generators in parallel (33,000 A fault current) and found "poor discrimination between breakers". New overcurrent relays followed, and high-breaking-capacity fuses were fitted as back-up to the breakers. — [MacLaren 1961](https://library.imarest.org/record/9717/files/v13b1p07a.pdf)
- Scott dates the improvement in supply integrity to the "d.c. ring main system" around 1900, and calls the early RN system similar to that of some transatlantic liners. — [Scott 1983](https://library.imarest.org/record/8911/files/v28b1p10a.pdf). This conflicts with MacLaren, who puts the ring main at *Defence* in 1908. **[UNCERTAIN]**: MacLaren is the more specific of the two.

**United States Navy**
- 1907: the standard generator "develops 800 amperes at 125 volts normal rating", with an 80 V tandem-compound design also described. The larger DC generators used equalizing rings "to maintain the brushes of the same polarity at the same potential", i.e. equalizers for parallel running. — [Walling & Martin, "Electrical Installations of the United States Navy", USNI Proceedings, Jan 1907](https://www.usni.org/magazines/proceedings/1907/january/electrical-installations-united-states-navy) (the page's last fifth was not read)
- 1919 battleship routine: the forward and after dynamo rooms' machinist's mates "stand by to equalize, divide or shift load as may be required". This shows the plant could be run paralleled or divided. On *Texas* the two machines in one room took the load on alternate days, with the idle machine kept ready. The forward distribution room had a chief electrician "in charge, distribution switchboard", and the plotting room had its own fire-control switchboards. — [USNI Proceedings, June 1919, "Electrical Division Aboard Ship"](https://www.usni.org/magazines/proceedings/1919/june/electrical-division-aboard-ship)
- Protection in 1919: daily tests of "the interlocking circuit-breaker relay", weekly checks of circuit-breaker settings, monthly breaker inspections. Lighting rounds looked for "blown fuses", and fire parties were sent to "stop blowers, pull fuses". So breakers sat at the switchboard and fuses on the branch circuits. — [USNI 1919](https://www.usni.org/magazines/proceedings/1919/june/electrical-division-aboard-ship)
- Earlier USN practice: series-wound searchlight machines gave about 50 V, shunt-wound lamp machines 70-110 V, and compound winding let one machine run both. *Kentucky* and *Kearsarge* (electric turrets) had 350 kW of generators against 661 kW connected, on a three-wire system. — [USNI 1919](https://www.usni.org/magazines/proceedings/1919/june/electrical-division-aboard-ship)
- Battle routine 1919: at general quarters electricians manned the dynamo rooms and repair parties, and the idle plant was readied. After a collision the dynamo repair sections stood by to start the idle plant. — [USNI 1919](https://www.usni.org/magazines/proceedings/1919/june/electrical-division-aboard-ship)

**Russia (plant size only)**
- *Tsesarevich* (French-built, 1903): six steam-driven generators, 550 kW in all. Her turrets were electrically powered. — [Wikipedia, Tsesarevich](https://en.wikipedia.org/wiki/Russian_battleship_Tsesarevich)

**Modern doctrine, as a contrast**
- Modern RN frigates divide their switchboards (bus-couplers, at least two watertight bulkheads apart, above flood level) so a damaged section can be isolated. They run either split (Type 21/22/42) or paralleled (Type 23). — [Scott 1983](https://library.imarest.org/record/8911/files/v28b1p10a.pdf)

### Inferences
- **[EST]** Before 1908 an RN ship had one central switchboard. A hit or flood there could take out everything at once. A fuse blowing took out one circuit.
- **[EST]** After *Defence*, a ring main with section breakers should in principle lose only a section. That needs the breakers to trip in the right order, and 1938 trials showed they often did not. For a game, pre-1925 RN breakers can be modelled as tripping wide, often a whole board, rather than cleanly isolating a section.
- **[EST]** USN plants of the 1900s-1910s were 125 V DC, compound-wound and equalized for parallel running, with two or more dynamo rooms that could be divided. Splitting the plant in action is suggested by "divide or shift load" but is not stated as a rule.
- **[EST]** Shock opens switches more often than it damages machines. Marlborough (below) is the period evidence: switches shaken off and fuses blown, both put right quickly.

### Gaps
- German (Kaiserliche Marine), Japanese, French and Russian voltages, fuse and breaker practice: no primary or secondary source reached. Recollection only, not verified: German capital ships ran 110 V DC early and 220-225 V later. Treat as unsourced.
- The date the USN first used automatic circuit breakers was not found. They were in routine use by 1919 (above). The 1898 USNI article ["Diseases of Electrical Installations in the Navy"](https://www.usni.org/magazines/proceedings/1898/april/diseases-electrical-installations-navy-their-causes-and-remedies) and the 1911 article ["Alternating Current versus Direct Current"](https://www.usni.org/magazines/proceedings/1911/june/alternating-current-versus-direct-current-use-board-ship) were found but not read; they are the next things to read. The last ~21k characters of the 1907 article were also not read.
- No documented rule was found on whether the RN split its dynamos onto separate switchboards in "action state" before 1925, and no RN "action state" electrical order.
- The problems of running DC compound dynamos in parallel (load hunting, reverse current without equalizers) are documented only by the existence of the USN equalizers. No period report of a ship losing power through a parallel-running fault was found.
- Doerry & Amy 2019 (doerry.org) was not reached.

## 2. Recorded cases 1894-1925 of warships losing electric power, lighting or power-driven services in action or after damage

### Takeaway
Few cases are well documented, but they share a pattern. Total blackouts came from water (Lion's flooding at Dogger Bank, Szent Istvan's boiler rooms). Shock and splinters caused partial, quickly repaired outages: Marlborough's switches and fuses, Lutzow's lighting out for 45 minutes after a salvo, Seydlitz's switch room. No clean pre-1925 case was found of one shell hitting a dynamo room directly and blacking out the ship.

### Cited Findings (case table)

| Date / action | Ship | Cause | What failed | Duration / outcome | Source |
|---|---|---|---|---|---|
| 24 Jan 1915, Dogger Bank | HMS *Lion* | 12-in hits from *Derfflinger* below the waterline; flooding, list to port | "Lion's electric generators out of action": Beatty "could only signal using flag hoists". Port engine stopped (feed-water contamination), speed down to 15 kn, then 7 kn | Out of action and towed home by *Indomitable*. Feed water contaminated by salt on the return. The generators' exact cause (flooded switchboard or dynamo room) is not given by this source **[UNCERTAIN]** | [Wikipedia, Dogger Bank, citing Massie 2003 pp. 396-401, 410-411](https://en.wikipedia.org/wiki/Battle_of_Dogger_Bank_(1915)) |
| 31 May 1916, Jutland, 18:54 | HMS *Marlborough* | Torpedo in the diesel (dynamo) engine room; shock | The shock "shook off switches on lower power board" and blew "some fuzes in telephone circuits". The diesel room flooded, two killed. A serious list, but she kept firing | Switches and fuses "very quickly replaced and all control instruments were found to be in step". Crew partly taken off overnight for fear of capsize | [jutland1916.com, quoting the after-action report](https://www.jutland1916.com/?p=34113) |
| 31 May 1916, Jutland | SMS *Lutzow* | Salvoes; flooding of the whole fore part | First hit destroyed the wireless office (12 men). After one salvo "It was three-quarters of an hour before the lighting installation was restored". 27 men in the diesel dynamo room, cut off by flooding, "continued to carry on their work in order to provide the ship with light" | Lighting out about 45 min, then restored. Ship scuttled that night after the bulkheads gave way around 03:00 | [jutland1916.com, SMS Lutzow (survivor's account)](https://www.jutland1916.com/ships-stories/sms-lutzow) |
| 31 May 1916, Jutland, 16:55 | SMS *Seydlitz* | First hit | "The electrical switch room fell out". At 19:44 the shock of a hit threw off the upper rudder engine's coupling (mechanical) | Duration not given. On the return the portable "leak" pumps could not cope or failed | [warhistory.org, Seydlitz part III](https://warhistory.org/article/sms-seydlitz-operational-history-iii) |
| 10 Jun 1918, off Premuda | SMS *Szent Istvan* | Two torpedoes abreast the boiler rooms; the after, then the forward boiler room flooded | Flooding "killed the power for the pumps and only left enough electricity to run the lights". 10° list, cut to 7° by counterflooding | Capsized at 06:05, under 3 h after the hits | [Military Wiki, Szent Istvan](https://military-history.fandom.com/wiki/SMS_Szent_Istv%C3%A1n) |
| 9 Nov 1914, Cocos | SMS *Emden* | Shellfire from *Sydney* | Wireless office destroyed early. Steering gear disabled and hand steering jammed by splinters, so she steered by her screws. Rangefinders destroyed | Beached. The source does not say the electrics failed | [Wikipedia, SMS Emden](https://en.wikipedia.org/wiki/SMS_Emden) |
| 10 Aug 1904, Yellow Sea | *Tsesarevich* | Two 12-in hits by the conning tower | Steering wheel jammed and the ship circled. No electrical failure reported | Repaired. 6 kn afterwards because of funnel damage | [Wikipedia, Tsesarevich](https://en.wikipedia.org/wiki/Russian_battleship_Tsesarevich) |
| 27-28 May 1905, Tsushima | *Oryol* | Shellfire and fires | 6-in turrets jammed by 8-in hits, one burnt out. The source names no electrical failure | Captured | [warhistory.org, Oryol](https://warhistory.org/2021/02/02/the-sad-eagle-oryol/) |

**WWII checks (not re-researched):** the *Ark Royal* (1941) flooding of the main switchboard control room, and *Belfast*'s 1939 mine shock disabling her electrical equipment, are both given by [MacLaren 1961](https://library.imarest.org/record/9717/files/v13b1p07a.pdf) as the triggers for relay control, diesel emergency generators, automatic changeover switches and shock testing.

### Inferences
- **[EST]** Pre-1925 evidence suggests three tiers:
  - **Water** in dynamo, switchboard or boiler spaces gives a full or near-full loss (Lion; Szent Istvan's pumps).
  - **Shock** from torpedoes or heavy hits opens switches and blows fuses. These are put back in minutes (Marlborough).
  - **Splinter or shell damage** to the switchboard or lighting gear gives a partial outage of tens of minutes (Lutzow's 45 min; Seydlitz's switch room).
- **[EST]** Counted over hits, a total blackout is rare. In the cases found, ships took many heavy hits and stayed lit (Lutzow took more than 20 hits and lost her lights once, for 45 min). Lion lost her generators only after flooding and listing. This argues for blackout chance tied to water reaching the dynamo or switchboard spaces, not a per-hit roll.
- **[EST]** Szent Istvan shows that a ship can keep lighting while losing her heavy loads (pumps): partial capacity, with lighting kept going ahead of pumps.

### Gaps (cases not confirmed online; candidates for Campbell or the Admiralty reports)
- Lion, Dogger Bank: which space flooded (switchboard room, the A or B dynamo rooms, or the port feed tank) is not confirmed. Recollection, unverified: Campbell and Roberts put it down to flooding that short-circuited the switchboard and dynamos.
- Not checked online, with no case found: Yalu 1894; Santiago 1898 (the spanamwar hit chart is an image only); Mikasa, Retvizan and Suvorov electrics (Novikov-Priboy and Kostenko not reached); Coronel and the Falklands (*Kent*'s wireless); Dardanelles 1915 (*Inflexible*'s mine, *Irresistible*); *Audacious* 1914; Goeben; Konigsberg. The Jutland cases of Lion, Warspite (her steering jam is usually given as mechanical/hydraulic, not electrical **[UNCERTAIN]**), Malaya, Derfflinger, Von der Tann (condenser), Wiesbaden, Elbing and the British destroyers were also not checked. Not having found them online does not mean the events did not happen.

## 3. Period sources on electric fragility, and the lessons drawn after Jutland and in the 1920s

### Takeaway
The online sources reached record that the RN moved to 220 V and a watertight ring main in 1908, and that the decisive reforms came in WWII: diesel emergency generators, automatic changeover switches, relay control and shock testing. No post-Jutland (1916-1925) committee finding on electrics was found.

### Cited Findings
- After *Ark Royal*, the RN adopted relay control with normally dead control cables, secondary control positions, diesel generators where ships lacked them, and automatic changeover switches for AA mountings and other vital services. It also added automatic emergency lanterns and a damage-control communication system. — [MacLaren 1961](https://library.imarest.org/record/9717/files/v13b1p07a.pdf)
- The ring main's watertightness "rarely held in service", and doubts after WWII about how well it worked led to the review of AC. — [MacLaren 1961](https://library.imarest.org/record/9717/files/v13b1p07a.pdf)
- *Lutzow* had a diesel dynamo room in 1916, a separate source that kept the lights on while the fore part flooded. *Marlborough* also had a diesel dynamo room. — [jutland1916 Lutzow](https://www.jutland1916.com/ships-stories/sms-lutzow); [jutland1916 Marlborough](https://www.jutland1916.com/?p=34113)

### Inferences
- **[EST]** By 1916 both the RN and the German navy fitted diesel dynamos beside the steam dynamos. That gives a game grounds for a second, independent source in dreadnoughts of about 1912 onward.

### Gaps
- Not reached: Grand Fleet or Admiralty committees on electrical lessons after Jutland, USN BuEng reports, von Hase, Chatfield, Dreyer, *Der Krieg zur See*, and the Admiralty's 1916 Reports on Damage. No source found for the dates battery emergency lighting came in before WWII.
