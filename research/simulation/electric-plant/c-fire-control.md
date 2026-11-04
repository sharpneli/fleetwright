# C. Fire-control and navigation equipment: electric load and behaviour on power loss, 1890-1945
Status: draft    Updated: 2026-11-04    Request: [REQUESTS.md, 2026-11-04](../../REQUESTS.md); an appendix of [electric-plant.md](../electric-plant.md)

Scope note: about 28 searches and fetches. The sources that answered best were the 1918 Dreyer Table handbook summary (dreadnoughtproject), the 1911 USNI Sperry article, USN *Naval Ordnance and Gunnery* (1944/1950s editions on eugeneleeslover.com), the WWII USN radar handbook (ibiblio hyperwar) and a HAIDA (RCN) gyro page. Dreadnoughtproject's director pages had almost no electrical detail. Two sites refused the fetch: commsmuseum.co.uk (RN Types 1-16) and the dreadnoughtproject "British Wireless Systems" page. **Very few watt figures exist in open web sources for 1905-1925 fire-control gear.** Most of the per-consumer power figures the game needs will be [EST]. Each one is marked as such below.

## 1. Directors and transmission to the guns (RN director firing, follow-the-pointer, German Richtungsweiser, USN, synchros)

### Takeaway
From about 1912 to the 1930s, RN director firing, and DC-era systems in general, sent the director's train and elevation to each gun by electric step-by-step / "follow-the-pointer" receivers. The gun's own layer and trainer worked the turret's hydraulics by hand to match pointers. If the transmission failed, the guns still worked: the layers fell back to their own sights (local or turret control). AC synchro transmission came into USN new construction in the 1930s, and it alone allowed fully automatic RPC. Not one wattage for a director or a receiver turned up.

### Cited Findings
- RN turret elevation receiver (Great War, tripod director firing system): "Follow-the-Pointer". The director's ordered elevation shows as two red pointers, and the layer works the gun until the two black pointers match them. The page gives no maker, supply or voltage. — [Dreadnought Project: Turret Type Elevation Receiver](https://dreadnoughtproject.org/tfs/index.php/Turret_Type_Elevation_Receiver)
- The turret director trainer "would follow the pointer with his training receiver to keep his mounting properly trained", and moved the turret with its hydraulic controls. A voicepipe gave him gun range, which he dialled into the receiver to correct for convergence. Source: the 1917 *Director Firing Handbook* (O.U. 6125, ADM 186/227). — [Dreadnought Project: Turret Director Trainer](https://dreadnoughtproject.org/tfs/index.php/Turret_Director_Trainer)
- Dreyer Table outputs to the director and guns (gun range, gun deflection) were "relayed electro-mechanically". Range and bearing reports from rangefinders came in the same way, and bearing pins on the bearing plot were "actuated by a solenoid". — [Handbook of Dreyer Fire Control Tables 1918 (dreadnoughtproject summary)](https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php)
- Okun (NavWeaps):
  - DC systems used "stepped" power circuits and "typically needed a person for final aim adjustment".
  - The British used no AC until they adopted US AA directors in WWII. Their earlier systems used "DC switches for stepped coarse control, with a man doing final fine control".
  - AC power and synchros (RDC) came into newer USN ships in the 1930s, and older ships were retrofitted gradually. "To my knowledge", all US warships went from DC to AC during WWII.
  - HMS Vanguard was the first RN ship with extensive AC power from the start.
  - Germany used AC in post-1930 warships, with magnetic amplifiers.
  - With AC synchros, manual follow-the-pointer was needed only as a backup in casualties. — [NavWeaps tech-013, Okun, "Fire Control and Fire Control on the Prinz Eugen"](https://navweaps.com/index_tech/tech-013.php)
- USN synchros (WWII):
  - Synchro rotors are fed from "115-volt AC".
  - Coarse/fine pairs: in the 5"/38 the fine synchro runs at 36:1, and the coarse one takes over when the error exceeds 3.8°. In amplidyne drives the pair is geared 36:1, and the switch comes at about 3° out of synchronism.
  - Accuracy target: errors "not more than a few minutes of arc". — [Naval Ordnance and Gunnery, ch. 10E (eugeneleeslover)](https://eugeneleeslover.com/USNAVY/CHAPTER-10-E.html)
- Early director/firing context: an 1888 report on a Victorian colonial-navy director says the commander was to fire all guns together by turning a key in "the battery of the electric gear". The gear was still unfinished, so the men fired by hand. — [Cerberus: Director Firing (Argus, 3 Apr 1888)](https://cerberus.com.au/director.html)
- Electric firing of ships' guns from batteries was already a subject in 1894. The article was not read. — [USNI Proceedings Oct 1894, "Electric firing on board ship"](https://www.usni.org/magazines/proceedings/1894/october/electric-firing-board-ship-electric-primers-and-firing)

### Inferences
- [EST] Director receivers in 1912-1925 were dial movements: small step-by-step motors or solenoid ratchets driving pointers. Each probably drew a few watts to some tens of watts on the fire-control DC circuit. A director plus receivers at 8-16 positions is likely well under 1 kW. I found no figure.
- [EST] Power-loss behaviour of a follow-the-pointer gun: the receiver pointers freeze. The gun itself (hydraulic or steam-hydraulic training and elevation, powered by the turret's own pumps) can still lay by its own telescopes in "local" or "turret control". The gun's accuracy falls to local laying, because the director's steady aim and its salvo co-ordination are lost. For a hydraulic turret the hydraulic pumping engine is the power source, not the fire-control circuit. Losing only the fire-control circuit leaves the turret moving.
- [EST] Firing circuit: RN breech guns used electric firing tubes, with percussion as the alternative (lock fired by lanyard or trigger). With the director firing circuit dead, each gun could still be fired by its own layer, electrically or by percussion. Whether the RN director firing circuit ran from batteries or from the ship's mains is unconfirmed (see Gaps). The 1888 "battery of the electric gear" points to primary batteries in the early period.
- [EST] Synchro-era loss: with 115 V AC gone, synchros give no data at all; unlike step-by-step dials, they hold no last reading. The mount must go to local control with its own sights, or to manual.

### Gaps
- No watts, amps or voltage found for the RN director, its transmitters, Evershed bearing indicators, or Barr & Stroud / Vickers receivers.
- The RN director firing circuit's supply (primary batteries, accumulators, or the ship's lighting/power ring mains) is unconfirmed. Likely in O.U. 6125 (1917 *Director Firing Handbook*), which was not found online.
- German Richtungsweiser (director, about 1915-16) and its receivers: no source found on transmission or power.
- No source on USN pre-1930 director transmission (e.g. the Ford/GE systems of the 1918-1925 battleships).

## 2. Fire-control tables and computers (Dreyer, Argo, AFCT, Ford Rangekeeper Mk 1 and Mk 8, Computer Mk 1, foreign tables)

### Takeaway
The Dreyer Tables of 1912-1918 mixed clockwork, hand cranks and small electric motors, with hand-crank backups. The Mk I range clock was clockwork (Vickers), and from the Mark III on the range clock was motor driven. Losing power cost an extra hand and some automatic plotting, but the table could be worked by hand. By the 1930s, US computers such as the Ford Mk 1 were full of motor servos (about 20). Their manuals list Automatic, Semi-automatic, Manual and Local modes, with hand cranks for setting inputs. Some backup computers (the USN Computer Mk 6 in a turret booth) needed no power at all.

### Cited Findings
- Dreyer Table range clock:
  - Mark I and I*: "Vickers, external, clockwork driven". It "did not have sufficient power to drive the plotting pencil worm screw or the gun range counter", so an operator chased a pointer by handwheel.
  - Mark II: Argo clock, power source not stated.
  - Marks III, III*, IV, IV*, V: "Vickers, integral to dumaresq, motor driven". — [Handbook of Dreyer Fire Control Tables 1918](https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php)
- Dreyer range plot drive: Mark I and I* "Hand cranked"; Mark II "Electric motor with handcrank back-up". Later marks have ditto marks, which the fetch tool read as the same; that reading is not certain [UNCERTAIN]. — [same](https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php)
- Dreyer Mark III crew: "7 (+1 if motor breaks)". So a failed motor cost one more man, not the table. — [same](https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php)
- Dreyer Dumaresq own-heading input:
  - Mark I: "handwheel input". Mark I* and III: "automatic input" (from the gyro compass) [EST that it came from the gyro]. Mark IV: "electrical".
  - The electrical Dumaresq used "small gears driving commutators" to pass enemy speed and inclination to repeaters aloft.
  - The handbook lists a Main Drive Motor, a Main Drive Control and Governor, a "Motor, Standard (2-Pole) Pattern" in the bearing plot, and an "Electric Motor Drive for the Turret Table". No ratings are given. — [same](https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php)
- The Dreyer Table, Dumaresq and Argo Clock date from about 1905 on, but "took years to be widely deployed". — [Wikipedia: Rangekeeper](https://en.wikipedia.org/wiki/Rangekeeper)
- Ford Mk 1 / Mk 1A (about 1931):
  - "Reversible two-phase capacitor-run induction motors with tungsten contacts" for the drives needing precise speeds.
  - "Approximately 20 servomechanisms, mostly position servos".
  - Eddy-current slip clutches for speed stabilisation.
  - An integrator drive motor regulated by a clock escapement.
  - Mk 1A weight 3,150 lb (1,430 kg).
  - In WWI-era rangekeepers, sailors followed the pointers by hand. — [Wikipedia: Rangekeeper](https://en.wikipedia.org/wiki/Rangekeeper)
- Computer Mk 1 (Mk 37 system): more than 3,000 lb. It takes line of sight from the director and sends line of fire to the mounts by synchros, and the mounts are laid by hydraulic servos. The page gives no power figures. — [Wikipedia: Mark I Fire Control Computer](https://en.wikipedia.org/wiki/Mark_I_Fire_Control_Computer)
- OP 1064 (Computer Mk 1): operating sections cover "Handcranks and Dials on the Rear Top of the Computer". Its modes are Automatic, Semi-automatic, Manual and Local, plus Secured and Standby. — [OP 1064 (eugeneleeslover)](https://www.eugeneleeslover.com/ENGINEERING/OP1064.php)
- USN battleship Mk 41 director and plotting room (WWII):
  - If the automatic follow-ups fail, "selected level or crosslevel may be maintained by using the hand follow-ups, the galvanometers, and the hand firing key".
  - Target bearing can be followed by an engaged hand crank.
  - The turret booth's Computer Mk 6 is a standby unit that "operates without mechanical or electrical inputs", with inputs set by hand. — [Naval Ordnance and Gunnery ch. 20E](https://eugeneleeslover.com/USNAVY/CHAPTER-20-E.html)
- The Admiralty Fire Control Table has a Wikipedia article, which was not fetched for lack of budget. — [Wikipedia: AFCT](https://en.wikipedia.org/wiki/Admiralty_Fire_Control_Table)

### Inferences
- [EST] Dreyer Table motors were small fractional-horsepower DC motors (on the order of 50-250 W each), with a governor; the whole table perhaps drew a few hundred watts. Without power:
  - The range clock stops (Marks III-V) or must be driven by hand; the Mark I clockwork clock keeps running.
  - The plots stop advancing until cranked.
  - Electro-mechanical input and output transmissions go dead, so ranges and bearings come by voice pipe or telephone.
  - The table is still usable with one more hand.
- [EST] Ford Mk 1 / Mk 1A: the motor servos and the escapement-regulated integrator drive stop with power. Hand cranks can set the inputs and keep the solution turning ("Manual" mode), but the synchro outputs to the guns also die with the 115 V AC supply. In practice the computer goes dead unless the supply comes back. Load is probably a few hundred watts to about 1 kW; not sourced.
- [EST] Ford Rangekeeper Mk 1 (1916-17): mostly mechanical, with a motor-driven time drive. Losing power probably stops the time input (the integrators freeze) unless hand-driven. Not confirmed.

### Gaps
- No wattage for any Dreyer Table, Argo clock, AFCT, Ford Rangekeeper or Computer Mk 1.
- Argo clock (Pollen, 1912): its drive (electric motor or clockwork) was not confirmed in the sources I read.
- AFCT (1920s-30s): motor drives not confirmed.
- German *Rechenstelle* tables and the Japanese Type 92/98 shagekiban: nothing found.
- Ford Rangekeeper Mk 8 (OP 1064 is actually the Computer Mk 1 manual): no power details found.

## 3. Gyrocompasses (Anschütz 1908+, Sperry 1911+, Brown)

### Takeaway
Early gyrocompasses ran on a motor-generator set drawing "a few amperes". They took minutes to settle if started near the meridian and about 2 hours from 90° off (Sperry, 1911). A later RN-fitted Sperry took about 4.5 hours, cut to 1.5 hours by nudging the suspension. Repeaters were electrically driven, and they stop when the master or its supply does.

### Cited Findings
- Sperry gyrocompass, 1911:
  - Rotor "8000 revolutions per minute"; power "only a few amperes"; no voltage given.
  - Fed from a motor-generator set on a common base with a small switchboard.
  - Settles to true north in "a few minutes" if started roughly on the meridian, and in "about two hours" if started 90° from north.
  - Repeaters on a two-wire cable, with no limit on their number.
  - Speed correction about 2° for a 30-knot change. — [USNI Proceedings, Sept 1911, "The Sperry gyroscopic compass for naval vessels"](https://www.usni.org/magazines/proceedings/1911/september/sperry-gyroscopic-compass-naval-vessels)
- Sperry Mk V / Admiralty Mk V (fitted 1943-63, RCN HAIDA):
  - About 4.5 hours to settle on true north after power-up; pressing at points on the suspension cuts it to about 1.5 hours, at 1-2° accuracy.
  - An earlier compass in AP 1005 (about 1935) ran on 85 V AC, 150 Hz, three-phase; newer ships 120 V, 400 cycles.
  - Tape repeaters driven by a 24 V DC stepping motor.
  - About 12 repeater positions, including the Transmitting Station and the forward and after Mk 34 directors. — [HMCS Haida: Gyro Compass System](https://www.jproc.ca/haida/gyrocompass.html)
- Anschütz: first gyrocompass about 1906; sea trials in SMS *Deutschland* in 1908, then adopted widely in the German navy. — [Wikipedia: Gyrocompass](https://en.wikipedia.org/wiki/Gyrocompass); [NSL: Short History of the Arma Gyrocompass](https://archive.navalsubleague.org/1999/short-history-of-arma-gyrocompass-messrs-pekelney-lindell)
- Arma (US) compasses had "lower power consumption" than Sperry and were "easier to start and stop". — [NSL: Arma history](https://archive.navalsubleague.org/1999/short-history-of-arma-gyrocompass-messrs-pekelney-lindell)

### Inferences
- [EST] Power: "a few amperes" at the motor-generator's output (tens of volts AC) suggests about 100-300 W at the rotor. The motor-generator set's input from the ship's DC mains might be 0.5-1 kW including losses. Repeaters were step-by-step on low-voltage DC: a few to ~10 W each [EST].
- [EST] On a power cut:
  - The rotor coasts down over many minutes, as it is a heavy wheel in bearings (an 8000 rpm Sperry of ~25 kg).
  - The compass keeps a usable heading for perhaps 5-15 minutes while the rotor slows, but begins to wander as its speed (and so its north-seeking moment) falls. The repeaters stop at once if their own supply goes, or freeze at the last step.
  - Back on power, the run-up takes minutes. Settling is a fresh start: minutes if the ship's head is set in by hand from the magnetic compass, up to 2 hours (1911 Sperry) or 4.5 hours (WWII Sperry) from far off.
  - The game could model: heading valid for ~10 min after the cut (degrading), then lost; after restoration, 20-60 min to usable within ~1° if started on a known heading, 2-4.5 h from cold.
- [EST] Consumers that take the gyro's heading lose it with the gyro: the Dreyer Dumaresq (Mark I* on, "automatic input"), the plots, and the director's convergence. They fall back to the magnetic compass and handwheel input.

### Gaps
- Anschütz rotor speed, supply (it used three-phase AC, frequency unconfirmed) and the 1912 three-gyro design figures: nothing found. [UNCERTAIN] Recollected figures of about 20,000 rpm were not verified.
- Brown (UK) gyrocompass: nothing found.
- No measured coast-down times found for any early gyro.

## 4. Gyro stable verticals and director gyros

### Takeaway
WWII USN stable verticals and elements (Mk 41/43 in battleship plotting rooms, Stable Element Mk 6 with the Mk 37) were motor-driven gyros on 115 V AC, with automatic follow-ups that have hand fallbacks (hand follow-ups, galvanometers, the hand firing key). They had a mercury cut-out to protect the gyro in hard manoeuvres. I found no erection times.

### Cited Findings
- Mk 41 stable vertical (battleship plotting room):
  - Mercury cut-out valve switches "prevent malfunctioning of the gyro during high-rate maneuvers", and need own speed and course by synchro.
  - On failure of the automatic follow-ups, "selected level or crosslevel may be maintained by using the hand follow-ups, the galvanometers, and the hand firing key".
  - The Mk 43 is noted for the South Dakota class. — [Naval Ordnance and Gunnery ch. 20E](https://eugeneleeslover.com/USNAVY/CHAPTER-20-E.html)
- Stable Element Mk 6 (Mk 37 system): a gyroscopic device reacting to roll and pitch that supplies the Computer Mk 1. No erection time given. — [Wikipedia: Mark I Fire Control Computer](https://en.wikipedia.org/wiki/Mark_I_Fire_Control_Computer)

### Inferences
- [EST] Before stable verticals (RN up to the late 1920s), director firing allowed for roll by hand: the director layer kept the cross-wire on and fired at the top of the roll. A gyro-stabilised firing key, if fitted, closed the firing circuit at the chosen level. With power gone, firing falls back to the layer's judgement of the roll; nothing else is lost.
- [EST] Stable vertical: when power is cut, the gyro coasts and drifts off the vertical within minutes. When power comes back, it must run up and re-erect: perhaps 5-15 minutes with fast erection, 30+ minutes without. Not sourced; flagged for verification in OP 1064 or the Stable Element Mk 6 manual (OP 1065?).

### Gaps
- RN "gyro director firing gear" (1920s-30s) and its power: not found.
- No source on the USN Mk 32 stable vertical.
- No erection or re-erection times found.

## 5. Rangefinders, plotting, range clocks, TS lighting, telephones, voice pipes, salvo bells, firing circuits

### Takeaway
Rangefinders were optical and needed no power for ranging; their readings reached the plot electro-mechanically. Dreyer plots were motor driven with hand-crank backup. Voice pipes need nothing, and these were the universal fallback. Telephones, salvo bells and buzzers, signal lamps on the table and transmitting-station lighting are all small loads, and no source gave watts.

### Cited Findings
- Range reports from rangefinders reached the Dreyer Table "electro-mechanically". The table had "Signal Lamps on the Table" and a Standard Pattern Transmitting Commutator. — [Handbook of Dreyer Fire Control Tables 1918](https://dreadnoughtproject.org/docs/notes/Handbook_of_Dreyer_Fire_Control_Tables_1918.php)
- The director trainer got gun range by voice pipe. — [Dreadnought Project: Turret Director Trainer](https://dreadnoughtproject.org/tfs/index.php/Turret_Director_Trainer)
- WWI-era range keeping: range "phoned from the rangefinder" and spotting by telephone. — [Wikipedia: Rangekeeper](https://en.wikipedia.org/wiki/Rangekeeper)

### Inferences
- [EST] Loads: transmitting-station lighting a few hundred watts (tens of carbon or tungsten lamps at 25-60 W). Telephones (sound-powered or low-voltage battery) a few watts. Salvo bells and fire gongs tens of watts while ringing. The firing circuit draws a pulse only.
- [EST] On a power loss:
  - Voice pipes and sound-powered phones go on working. Battery telephones go on working while their batteries last.
  - TS lighting goes unless there is battery or police lighting, and the table must then be read by hand lamp.
  - Step-by-step range and bearing transmissions freeze, so the plot must take ranges by voice.

### Gaps
- No watt figures for any of these.
- Whether RN fire-control telephones of 1905-1925 were battery (common-battery or local-battery) or sound-powered was not confirmed.

## 6. Wireless sets, 1905-1925

### Takeaway
The RN fitted alternator-fed spark sets from 1904-05. A 1906-era RN table lists a "1.5 kW" set and a "Mark II - 14 kW" set (the large battleship set). The 14 kW in the question matches.

### Cited Findings
- From 1904-05 the RN adopted "transmitters fitted with alternators". — [RNZN comms: History of Naval Ships Wireless Systems 1890s-1920s (PDF)](https://rnzncomms.wordpress.com/wp-content/uploads/2011/01/history-of-naval-ships-wireless-systems.pdf)
- The RN tune table (about 1906) gives "1.5 kW (Q-U)" and "Mark II - 14 kW power" on tunes Q-W. It also names a "Rotary Converter" and an "Alternator & spark" set, and a 1907 "Destroyer Wireless" set. — [same PDF](https://rnzncomms.wordpress.com/wp-content/uploads/2011/01/history-of-naval-ships-wireless-systems.pdf)
- Dreadnoughtproject has a British Wireless Systems page (Types 1-16), but it could not be fetched. — [Dreadnought Project: British Wireless Systems](https://www.dreadnoughtproject.org/tfs/index.php/British_Wireless_Systems)
- Commsmuseum has pages per RN Type (Type 1, 5C, 9, 14-16), but they could not be fetched. — [Commsmuseum: Type 1](https://commsmuseum.co.uk/transmitters/3%20Type%201/type1.htm)

### Inferences
- [EST] A battleship's main spark set (Type 1, Mark II, 14 kW) was driven by a motor-alternator from the ship's DC mains. It is an intermittent load of ~14-20 kW while keying, near nil on watch. Secondary and auxiliary sets were 1-1.5 kW, some on accumulators or a rotary converter. On a power loss the main set goes silent, and an auxiliary or emergency set on batteries may keep a short range.

### Gaps
- The Type 1/2/9/10/16 designations versus kW were not confirmed, because the per-type pages were unreachable.
- Emergency battery sets: no source found.

## 7. Remote power control (RPC) of mounts

### Takeaway
USN WWII mounts were power-driven under synchro orders (RPC). The 5"/38 used electric-hydraulic drives (AUTO/LOCAL/HAND/MANUAL modes) and the 5"/54 used amplidyne drives with a 40 hp train motor. A brake locks the mount if power fails, hydraulic accumulators finish an operating cycle, and every mount has a MANUAL handwheel mode. RN RPC came in late WWII and after: the 4.5" Mk V was designed for it, and Vanguard had AC from the start. This is the last item in time and the largest load.

### Cited Findings
- USN mounts:
  - 5"/54 Train Power Drive Mk 14: "40-hp train motor".
  - 5"/54 brake unit "locks the drive and holds the mount stationary if power supply fails during power operation".
  - Electric-hydraulic systems have accumulators to "complete an operating cycle in case of power failure".
  - Modes: 5"/38 AUTO, LOCAL, HAND and MANUAL (AUTO follows at up to 25°/s); 5"/54 AUTO and LOCAL HIGH/MED/LOW; 40 mm York drive MANUAL, LOCAL and AUTO; 16"/50 HAND and AUTO.
  - The manual train on the 5"/38 is geared directly to the training rack.
  - A 10 kW amplidyne appears only as a teaching example. — [Naval Ordnance and Gunnery ch. 10E](https://eugeneleeslover.com/USNAVY/CHAPTER-10-E.html)
- AC synchros and RDC in USN ships from the 1930s, with all converted during WWII. RN AC only with the US AA directors and Vanguard. — [NavWeaps tech-013 (Okun)](https://navweaps.com/index_tech/tech-013.php)
- RN 4.5-inch Mark V was "designed for anti-aircraft use with remote power control (RPC, where the guns automatically train and elevate the target following the director)". — [Wikipedia: QF 4.5-inch Mk I-V](https://en.wikipedia.org/wiki/QF_4.5-inch_Mk_I_%E2%80%93_V_naval_gun)

### Inferences
- [EST] An RPC 5" mount draws tens of kW at peak, from the train motor plus the elevation drive. Losing power under RPC means: the brake holds the mount where it is, the accumulator finishes the round, and then the crew shifts to MANUAL handwheels at a fraction of the laying speed. A loss of only the synchro data (115 V AC) leaves the mount in LOCAL, laid by its own pointers with power.

### Gaps
- No source on RN metadyne mounts (dates, kW).
- No amplidyne kW ratings for specific mounts.

## 8. Radar (WWII, scale only)

### Takeaway
Early USN fire-control radars drew 2-4 kW.

### Cited Findings
- Mk 3: 2.2 kW, 115 V, 60 c single phase, ~3,700 lb. Mk 4: 2.4 kW, 115 V, 60 c, ~3,800 lb. Mk 8: 4 kW, 440 V, 3-phase, 60 c, 14,485 lb with spares. The Mk 12 is "a higher-powered set". — [USN radar handbook (ibiblio hyperwar), Ship Fire Control Sets](https://ibiblio.org/hyperwar/USN/ref/Radar/Radar-2.html)

### Inferences
- [EST] A WWII battleship's whole radar fit (search plus several fire-control sets) was of the order of 15-30 kW.

### Gaps
- No figures found for search radars (SG, SK) or RN Types (284, 281, 273).

## 9. Searchlight control

### Takeaway
No source found for remote searchlight control gear (1915+) power.

### Cited Findings
- (none found)

### Inferences
- [EST] Remote searchlight control used step-by-step training and elevation transmissions like the gun receivers: small loads of tens of watts per light, plus the training motors. Without the control circuit, each light is trained by hand at the projector.

### Gaps
- Searchlight control gear: no source found in the budget. Searchlights themselves are outside this scope.
