# Plan: the scenarios

Agreed 2026-10-25: wave 1 built the same day for the playtest alpha (below). Before it the launch screen shipped two
battles, Tsushima (27 ships) and the Jutland (250); the benches (Squadrons, Flotilla) and the scale test's rungs are
under Tests (a file's `"test": true`). This plan picks a set of engagements of every size, real and hypothetical, across the first cut's
years (1890-1925, docs/battle/fire-control.md), and says what each needs that the game doesn't have yet.

## What a scenario can ask for today

Can: surface ships of any design; guns, torpedoes, fire, flooding, damage control; night, haze and fog (visibility),
wind; smoke; coasts and islands (Natural Earth, the slide); merchants on routes (tramp_1914) and neutral sides; a side's
crews, readiness, fire discipline, doctrine, tempers; homes to fall back to; an end time.

Can't (yet), so the plan steers round them:

- **Aims other than destroy everyone** (`Sim.Ai/Commanders/Aims.cs`: Reach, Protect, Hold, Avoid are named and wait for
  a scenario). Many of history's best fights are a breakout, an escape or a convoy. Those are wave 2, and they need the
  side's commander to act on the aim (steer for it, avoid a fight), not only the judge to read it.
- **Arrivals:** every unit is on the map at the start. A force coming later is placed far off on a route; good enough
  for most, and "enters at 09:30" (N2) can come if the far placing proves clumsy.
- **Mines, submarines, aircraft, shore batteries:** none. Battles that turned on them (Irben 1915, Imbros 1918, Second
  Heligoland 1917, Manila Bay) are left out, or set at a moment and place where they don't decide it.
- **Ensigns:** the screen draws britain, germany, russia, japan, france, italy, ottoman, finland, sweden. Each new navy is a small drawing.

## Sizes

The launch screen's filter splits at 30 ships (SMALL / FLEET). Here: **small** under 15 (a fight you see whole at 1x),
**medium** 15-50, **fleet** 50 and over.

## The candidates

Each: when and where, real or hypothetical, the size, the sides, the hook (the asymmetry that makes it a game, per
the scenario-designs rule: balance first, not tonnage), the conditions, the aims, and what's missing. "Reuse" names
designs we have (Content/Scenarios/Designs, shipgen/designs); "new" the designs to make, by one design a class or one
stand-in for several.

### Early: 1890-1905

**1. The Yalu** (real, 17 September 1894, off the Yalu mouth, Korea Bay). Medium, about 12 a side.
China's Beiyang Fleet in line abreast, two German-built ironclads (Dingyuan, Zhenyuan: 12 in in barbettes, 14 in belts)
and a ragged set of cruisers, against Itō's fast cruisers in two columns, all quick-firers. Armour and a few big slow
guns against speed and a rain of 4.7 and 6 in. Midday, clear. Destroy everyone.
New: Dingyuan, a Chinese armoured and a protected cruiser, Matsushima (one 12.6 in Canet), Yoshino, Naniwa. Ensign:
Qing (the dragon, simplified). The earliest fight on the list: tests the Victorian templates hardest.

**2. Santiago de Cuba** (real, 3 July 1898, the Cuban south coast). Small, 6 against 7.
Cervera's four armoured cruisers and two destroyers come out of the harbour one by one and run west along the coast;
Sampson's battleships and Brooklyn close in. A chase: the Spanish need to get by, not to win. Morning, clear.
Aims: Spain reach (west, off Cape Cruz), the US destroy. New: Infanta María Teresa, Cristóbal Colón, Furor, Iowa (also
Indiana, Oregon), Texas, Brooklyn, Gloucester. Ensigns: Spain, the US. Needs N1 (Reach).

**3. Chemulpo** (real, 9 February 1904, off Chemulpo, Korea). Small, 2 against 6 (and torpedo boats).
Varyag and the gunboat Korietz steam out of the roads to break past Uryu's squadron. A forlorn hope: hold out, hurt,
get through if they can. Noon, clear, narrow water among islands. Aims: Russia reach (open sea), Japan destroy.
Reuse: aurora (Varyag), izumo (Asama). New: Korietz, a Japanese protected cruiser (Naniwa, Niitaka). Needs N1.

**4. The Yellow Sea** (real, 10 August 1904, off Port Arthur). Medium, about 40.
Vitgeft's Port Arthur squadron breaks out for Vladivostok; Togo's fleet bars the way. Six battleships a side, the
Russians slower, the Japanese better shot; a lucky 12 in hit on the flagship broke the Russian line in history.
Afternoon into night, clear. Aims: Russia reach (south-east, round Korea), Japan destroy. Reuse: borodino (Tsesarevich),
oslyabya (Peresvet), mikasa, izumo, aurora, buyny, ikazuchi. New: Retvizan, Poltava. Needs N1. The cheapest early
breakout: Tsushima's designs nearly cover it.

**5. Ulsan** (real, 14 August 1904, Korea Strait). Small, 3 against 4.
The Vladivostok cruisers (Rossia, Gromoboi, Rurik: big, long-legged commerce raiders) caught at dawn by Kamimura's four
armoured cruisers. Rurik falls behind, damaged; the others must choose to stay or go. Dawn, clear. Destroy everyone (a
Reach for Russia later). Reuse: izumo. New: Rossia (also Gromoboi), Rurik.

### Hypothetical, early

**6. Fashoda, 1898** (hypothetical, autumn 1898, off Toulon or Malta). Fleet, about 50.
Britain and France went to the brink over the Sudan. The Mediterranean Fleet's uniform Majestics and Royal Sovereigns
against the French "fleet of samples" (Brennus, Charlemagne, Bouvet, Carnot, Masséna, each different) with many
torpedo boats. Uniformity and numbers against variety and the torpedo. Reuse: brennus, bouvet. New: Majestic, Royal
Sovereign, a French pre-dreadnought or two, French torpedo boats, British cruisers. Ensign: France.

**7. Off Vigo, 1904** (hypothetical, the Dogger Bank incident escalates, late October 1904, off north-west Spain).
Fleet, about 45. Rozhestvensky's squadron, the Tsushima one fresh from Russia, against the Channel Fleet that shadowed it
south. Reuses all of Tsushima's Russians; new: Majestic, King Edward VII, British cruisers and destroyers. Shares
designs with Fashoda.

### The Balkan war

**24. Elli** (real, 16 December 1912, off the Dardanelles' mouth). Small, about 8 against 8.
Greece's Averof, a new fast armoured cruiser, leaves the three old Hydras behind and crosses the T of the Ottoman line
(Barbaros Hayreddin and Turgut Reis, ex-German Brandenburgs, and the old Mesudiye) alone, with destroyers on both
sides. One fast modern ship against a slow, poorly-kept line close to its home straits. Morning, clear. Aims: the
Ottomans reach (back into the straits), Greece destroy. New: Averof (an Italian Pisa), Hydra, Brandenburg, Mesudiye,
destroyers. Ensigns: Greece, the Ottoman Empire. (The numbers name the candidates; they aren't an order.)

### The Great War

**8. Heligoland Bight** (real, 28 August 1914). Fleet, about 80.
Tyrwhitt's destroyers sweep the Bight in mist; German light cruisers come out piecemeal to the guns; Beatty's
battlecruisers come down out of the haze and settle it. A brawl of light forces, then a hammer. Morning, mist (4-7 km).
Destroy everyone. Reuse: arethusa, town_*, l_laforey, m_class (stand-ins for the I and K classes too), lion, queen_mary,
indefatigable, stettin, gazelle, graudenz, v1. New: none needed (a Cöln stand-in from graudenz). The arrivals by
placing; N2 if that's clumsy.

**9. Coronel** (real, 1 November 1914, off central Chile). Small, 5 against 4.
Spee's two armoured cruisers, the Navy's best gunnery, against Cradock's old Good Hope and Monmouth, a light cruiser
and an armed liner. Evening, heavy sea (the British casemates awash), the sun setting behind the British. Destroy
everyone. Reuse: devonshire (Monmouth), duke_of_edinburgh (Good Hope), town_*, stettin and gazelle (Leipzig, Dresden).
New: Scharnhorst (or armoured_cruiser), Otranto (an armed liner). Ensigns exist. Note: the sun behind one side matters
here only if sightings know where it sets; flavour if not.

**10. The Falklands** (real, 8 December 1914, off Port Stanley). Medium, 7 against 5 (and colliers).
Coronel's revenge: Sturdee's battlecruisers coaling at Stanley when Spee arrives; he turns and runs, and they chase him
all day in clear weather. Speed and 12 in guns against the best-shot cruisers afloat. Aims: Germany reach (escape south
or east, out of the area), Britain destroy. Reuse: invincible, Coronel's Germans, devonshire (Kent, Cornwall), town_*.
New: none beyond Coronel's. Needs N1. Plays well as Coronel's sequel.

**11. Cape Sarych** (real, 18 November 1914, off the Crimea). Small, 2 against about 10.
Goeben and Breslau meet the Black Sea Fleet's five old battleships in fog; ten minutes of fire at close range. One
modern battlecruiser against a line of slow pre-dreadnoughts that concentrate on her. Noon, fog (visibility 3-6 km).
Destroy everyone. Reuse: moltke (Goeben), stettin (Breslau), borodino and oslyabya (Evstafi and the rest), aurora.
New: none. Ensign: the Ottoman (the crew stayed German; the flag was Ottoman).

**12. Goeben at bay** (hypothetical, 7 August 1914, the Ionian Sea). Small, 2 against about 8.
Troubridge chose not to fight; here he does. Goeben and Breslau run for the Dardanelles; four armoured cruisers and
eight destroyers come at them at dawn. History's own argument: could old armoured cruisers and torpedoes take a
battlecruiser in clear weather? Aims: Germany reach (east), Britain destroy. Reuse: moltke, stettin, minotaur (Defence),
warrior, duke_of_edinburgh, a destroyer of ours (b97 or k_acasta for the Beagles). New: none. Needs N1 (works with
destroy everyone meanwhile).

**13. Dogger Bank** (real, 24 January 1915). Fleet, about 70.
Hipper's raid caught at dawn; he runs home, Blücher lagging, Beatty's five battlecruisers chasing with the Harwich
Force. A stern chase, then the signal muddle. Morning, clear. Aims: Germany reach (home), Britain destroy. Reuse:
lion, queen_mary (Princess Royal), indefatigable (New Zealand, Indomitable), seydlitz, moltke, derfflinger, graudenz,
stettin, arethusa, m_class, v25. New: Blücher (or armoured_cruiser), Tiger (shipgen has tiger_1914). Needs N1 for the
run; playable as a fight first.

**14. Night in the Dover Strait** (real, 20-21 April 1917). Small, 2 against 6.
Broke and Swift, two big flotilla leaders, meet six German destroyers coming back from shelling Dover; guns at point
blank, torpedoes, Broke rams. Night, calm, no moon. Destroy everyone. Reuse: flotilla_leader, g101 and v25. New: none.
(Ramming waits for collisions, fighting.md 4.11.) The smallest on the list: a fight you can learn the game on.

**15. The Lerwick convoy** (real, 17 October 1917, the North Sea, Shetland to Bergen). Small, 2 against 2 and 12
merchants. Brummer and Bremse, fast minelaying cruisers in disguise, fall on a Scandinavian convoy at dawn; its two
destroyers, Mary Rose and Strongbow, fight to the death. Aims: Germany destroy (the convoy), Britain protect (the
convoy). Reuse: wiesbaden (stand-in for Brummer), m_class, tramp_1914. New: none (a Brummer design is nicer). Needs N1
(Protect). The first escort scenario.

**16. Otranto Straits** (real, 15 May 1917, the Adriatic's mouth). Medium, about 30 with the drifters.
Horthy's three scout cruisers smash the drifters of the Otranto barrage, then run north for Cattaro with British,
Italian and French cruisers and destroyers chasing; Sankt Georg comes out to meet them. Three Allied navies on one
side. Morning, clear. Aims: Austria-Hungary reach (home), the Allies destroy. Reuse: town_* (Dartmouth, Bristol). New:
Novara (also Saida, Helgoland), a Tátra destroyer, Aquila (an Italian scout), a French destroyer, a drifter, Sankt Georg.
Ensigns: Austria-Hungary, Italy, France. Needs N1; the allies as sides at peace (as the Neutral pair today).

**17. Premuda** (real, 10 June 1918, among the Dalmatian islands). Small, 2 battleships and their escort against 2
motor torpedo boats. Before dawn, Szent István and Tegetthoff steam south for the Otranto raid; Rizzo's two MAS boats,
on a routine patrol, slip in through the screen. David and Goliath at night among islands. Aims: Italy destroy, Austria
avoid (get through to the south). Reuse: tegetthoff. New: an Austrian destroyer and torpedo boat, a MAS boat. Needs:
craft of 20 t (do they sail, sight and get sighted right?), the archipelago slide (the islands, docs/battle/bugs.md).

**18. Novik and the V-boats** (real, 17 August 1915, the Gulf of Riga). Small, 1 against 2.
Germany's V99 and V100 slip into the gulf by night; Russia's Novik, the fastest destroyer afloat with the heaviest
guns, catches them at dawn. A clean duel. Reuse: v25 (the V99 stand-in), a Novik is new (or a stand-in from
flotilla_leader). Ensign exists (Russia).

### Hypothetical, the Great War's edges and after

**19. The prizes' war, 1920** (hypothetical, spring 1920, the Strait of Bonifacio). Small, about 6 a side. The user's
example. France and Italy fall out over the Adriatic settlement; their light forces meet off Corsica and Sardinia.
Both navies' newest cruisers are German and Austrian war prizes: France's Metz, Mulhouse and Thionville (ex-Königsberg,
Stralsund, Novara), Italy's Ancona, Bari and Brindisi (ex-Graudenz, Pillau, Helgoland), each with ex-German destroyers.
Near mirror images, crewed by strangers to them (green crews both sides?). Day, clear, a mistral. Destroy everyone.
Reuse: graudenz, pillau, stettin, v25, g101, b97. New: Novara (shared with Otranto). Ensigns: France, Italy.

**20. The Tyrrhenian, 1920** (hypothetical, the same quarrel, the fleets). Fleet, about 60.
Italy's five dreadnoughts (Dante, the Cavours, the Duilios: thirteen 12 in each, fast, thin) against France's seven
(Courbet, Jean Bart, Paris, the Bretagnes: 13.4 in, slower, tougher), with scouts and destroyers. Speed and
numbers of guns against weight of shell. Reuse: dante, courbet. New: Conte di Cavour (also Duilio), Bretagne, an
Italian scout, Italian and French destroyers.

**21. Off the River Plate, 1915** (hypothetical, the South American dreadnought race comes to blows). Medium, about 20.
Brazil's Minas Geraes and São Paulo (twelve 12 in, British-built) against Argentina's Rivadavia and Moreno (twelve 12 in,
American-built, better armoured), each with scouts and destroyers. Mirror fleets from rival yards. Day, the Plate's
haze. New: Minas Geraes, Rivadavia, Bahia (scout), an Argentine armoured cruiser (Garibaldi), destroyers. Ensigns:
Brazil, Argentina.

**22. The Gulf of Finland, 1919** (hypothetical, summer 1919, off Koivisto). Medium, about 25.
Mannerheim's plan to march on Petrograd goes ahead, and the Finnish navy joins Cowan's squadron, whose base at Koivisto
was Finnish water anyway. Petropavlovsk and Andrei Pervozvanny come out of Kronstadt with Novik destroyers; against them
the British light cruisers and destroyers, and the Finns' few ex-Russian gunboats and torpedo boats, slow and lightly
armed, with crews who know every skerry. A dreadnought with a shaky crew against fast, well-handled light forces and
local pilots among the islands. Summer night (it never quite gets dark). Three sides: Britain and Finland at peace with
each other, both at war with the Reds. Reuse: caroline and cambrian (C-class), r_class (stand-in for the V and W).
New: Gangut in 1919 trim (shipgen's gangut is a WWII version), Andrei Pervozvanny, Novik, a Finnish gunboat (the
ex-Russian Uusimaa or Karjala type) and torpedo boat (names and figures to check with research). Ensigns: Finland
(the swallow-tailed state flag), the red fleet. The archipelago again.
Later cuts carry the Finns further: Väinämöinen and Ilmarinen (1932) against the Soviet Baltic Fleet, and, with
silliness allowed, the dreadnought Finland's 1920s naval debates never bought.

**25. The battle for Åland, 1921** (hypothetical, 28 June 1921, the Åland Sea south of Mariehamn). Small, 9 against 7.
Four days after the League of Nations gave Åland to Finland, Stockholm refuses the ruling (in 1918 Swedish warships,
Thor first, had already anchored at Åland) and sends the coastal fleet to Mariehamn. The Sverige class (Sverige,
Drottning Victoria, Gustaf V: 4 x 283 mm, 200 mm belt, 22.5 kn) against the Väinämöinen class built a decade early
(the real pair came in 1932; here Väinämöinen, Ilmarinen and an invented Lemminkäinen: 4 x 254 mm on a 55 mm belt,
diesel-electric, 15 kn), Wrangel-like destroyers against invented Finnish torpedo boats. The stronger ship that picks
the range against numbers, the knife and the haze. **Built 2026-10-25** (`aland.json`; designs sverige, vainamoinen,
wrangel, finnish_tb, all in 1921 tech so nothing is past its period). See "Åland, as built".

**23. Plan Orange, 1923** (hypothetical, the western Pacific). Fleet, about 80.
The US battle line (the standard types: slow, tough, all-or-nothing) against Japan's (Nagato, Ise, Fuso, the Kongo
battlecruisers: faster, longer torpedoes). The first cut's last year. Reuse in part: colorado, pennsylvania, nagato,
kongo (as shipgen has them, some in a later era's trim). New: Omaha, Clemson, Kuma, Minekaze, Ise/Fuso. Ensign: the US.
The biggest build; a later wave.

## What the game needs

- **N1 aims: Reach, Protect, Avoid** (Aims.cs, the judge) and the side's commander acting on them (steer for a place,
  keep a convoy covered, refuse a fight he needn't take). The heart of wave 2; its own plan when it starts.
- **N2 arrivals** ("enters at" on a unit): only if placing far off proves clumsy.
- **N3 ensigns:** France, Italy, Austria-Hungary, the US, Spain, the Ottoman Empire, Greece, Qing China, Brazil,
  Argentina, the red fleet (by the waves that need them).
- **N4 small craft:** MAS boats and drifters (20-100 t): checked that they sail, sight and die right.
- **N5 the archipelago slide:** Premuda, the Gulf of Finland, Chemulpo want ships among islands (the slide's tuning,
  docs/battle/bugs.md).
- **Designs:** about 40 new across all waves; balance-first, as Tsushima's (scenario designs).

## Waves

1. **Designs we have, destroy everyone** (no new sim): Night in the Dover Strait (14), Coronel (9), Cape Sarych (11),
   the prizes' war 1920 (19), Heligoland Bight (8), Dogger Bank as a fight (13). Two small, one small-medium, two fleets;
   1914-1920. Needs a Scharnhorst, a Novara, three ensigns (Ottoman, France, Italy). **Built 2026-10-25** (see "Wave 1,
   as built").
2. **The aims (N1):** Falklands (10), Goeben at bay (12), the Lerwick convoy (15), the Yellow Sea (4), Dogger Bank's run.
3. **New navies:** Elli (24), Otranto (16), Premuda (17), Santiago (2), the Yalu
   (1), Ulsan (5), Chemulpo (3), Novik (18), the Tyrrhenian 1920 (20), River Plate (21), the Gulf of Finland (22).
4. **The big hypotheticals:** Fashoda (6), off Vigo (7), Plan Orange (23).

## Decisions (the user's, 2026-10-25)

1. Wave 1 now, for a playtest alpha: enough fun for the first round of major feedback.
2. Finland is in the first game, with an unrealistic Finnish navy authorised (the user's ruling, as a Finn); more
   Finnish scenarios later, and a style session for its looks and ensign. The Gulf of Finland, 1919 (22) is the seed.

## Wave 1, as built (2026-10-25)

Each played headless by the AI on both sides (`shipgen scenario FILE --play --seeds N`), then set for balance with the
scenario's own knobs (crews, tempers, the weather, stand-ins); the numbers are the AI's, a player does differently.

| Scenario | File | Ships | Balance as built (ships lost a run, AI v AI) |
|---|---|---|---|
| Night in the Dover Strait, 1917 | dover-strait.json | 2 v 6 | both sides accept action by night; leaders' crews elite: about 0.9 v 0.4 (the boats favoured; combing the tracks helps) |
| Coronel, 1914 | coronel.json | 3 v 5 | devonshire for both armoured cruisers, a bold Spee: 0.9 v 0.9 |
| Cape Sarych, 1914 | cape-sarych.json | 7 v 2 | 8 km of fog lets Goeben pick her range: 1.2 v 0.5 (Breslau half the time) |
| The prizes' war, 1920 | prizes-war.json | 6 v 6 | mirror, average crews both: 1.2 v 1.4 |
| Heligoland Bight, 1914 | heligoland-bight.json | 44 v 24 | Beatty covers the light cruisers from 45 km: 10 v 22 (British, as history; the challenge is the cost) |
| Dogger Bank, 1915 | dogger-bank.json | 33 v 26 | the stern chase from 07:50, a cautious Hipper: 15 v 14, mostly light ships; Blücher's tonnage about once a run |

New for it: the novara design; the France, Italy and Ottoman ensigns; the launch screen's second era filter widened to
1906-1925; a test that every shipped scenario reads with its designs and ensigns. Found on the way (docs/battle/bugs.md,
"Balance and calibration"): light ships die too readily (the two fleet battles are far bloodier than history), and the
German armoured cruisers at Coronel capsize from 6 in hits and shoot worse than the British.

## Åland, as built (2026-10-25)

The user's ask: Sverige class against Väinämöinen class, the class a design so more ships can join it. Choices made:

- **1921, not the 1930s.** The real Väinämöinen is 1932, past the first cut (1890-1925). The Åland crisis (the
  League's ruling of 24 June 1921) is the natural flashpoint, Sweden's three ships were all afloat by then, and a 1921
  Väinämöinen built on 1921 tech (Early marine diesels with electric drive, the 1916 director, Standard 1912 shells)
  stays inside the templates' years, so it carries no Imaginary tag. Both navies fire the same shells and fire control:
  the fight is the ships.
- **Looks:** both in their great_war era (Sweden's neutrality chevrons, Finland's ice-field dazzle).
- **Open water, not the skerries.** The fight opens south of the islands. Land blocks neither sight nor shells in the
  sim (`Sightings` never asks `Land`), and there's no depth, so Väinämöinen's real trade (shallow draught, hiding
  behind islands) has nothing to work on yet, and the archipelago slide has open bugs (docs/battle/bugs.md).
- **The haze is the balance knob.** In 12 km of visibility Sweden keeps the range: Finland loses 3.2 of 9 ships to
  Sweden's 1.0, and every big ship's magazines run dry by about 08:00 at long-range hit rates of 2-4%. In 8 km it's a
  fight: Finland 4.1 of 9, Sweden 2.4 of 7 (about a Sverige a run, 13,600 t), from 0 to 8 lost on each side across
  the seeds (one seed sank all three Sverige).
- **Finland's doctrine is bold** (Aggression +1; the user, 2026-10-31: "it doesn't make sense for us to have a
  scenario which is so unbalanced that even the AI decides to bail out"). After balance.md BL5's escape and search
  rules the Kommodori, outmatched at the default bar, declined and got away in most seeds (no fight at all in the
  panel, 0 shells). Bold, he fights down to a third of the odds: Finland 4.2 of 9, Sweden 2.0 of 7 (8 seeds), 7 of 8
  seeds a fight. A fourth Väinämöinen with it was worse for Finland (6.4 v 1.6: a bigger bold force presses in harder).
- **The haze down to 6 km** (2026-10-31, after the admirals' search lines): Sweden's admiral spreads his force to search
  once he knows each squadron can outrun the Finns, so in 8 km he found them again and hunted them (Finland 5.9 v 1.8).
  In 6 km, bold: Finland 4.8 of 9, Sweden 2.9 of 7 (15,600 t, more than a Sverige), every seed a fight.
- **Sizes:** the generator makes Sverige 10,600 t (history 7,100) and Väinämöinen 2,600 t (3,900); both read as
  cruisers to the sim (`Sightings.TypeOf`: under 14,000 t full). The destroyers are about 3x history (Wrangel 1,470 t at
  32 kn, the boats 890 t at 28 kn), as in the destroyer crunch; 34 kn made Wrangel 1,850 t.

Open:

1. **Land that hides.** Islands masking sight (and shells) would give the Finns their real weapon: a skerries
   variant that opens among the islands. It needs land heights (Natural Earth has none) or a nominal height, and the
   archipelago slide's bugs fixed first.
2. **The wave 2 version:** a Swedish landing convoy for Eckerö (Protect against destroy), once N1 lands.
3. A fourth Väinämöinen (Joukahainen) or crews are the next knobs if playtests find Finland too weak.

## Open questions for the user

1. Which of these, and how many for the first release? (A dozen seems about right: three or four a size.)
2. *Answered 2026-10-30 (docs/battle/balance.md decisions 3 and 5): nudged. History inspires the forces and places,
   the physics is the only yardstick, losses within about 2x of the record; no overload for this release.* Real
   battles: as history set them (forces, places, weather), or nudged for balance where history was a massacre
   (Coronel, Santiago, Premuda's lone boats)? Tsushima keeps history's forces with designs set for balance; the same?
3. Hypotheticals: plausible (1920 France-Italy, 1898 Fashoda) only, or wilder too (allow silliness: La Baguette II)?
4. *Answered 2026-10-30 (balance.md P6): aims after the balance round.* Aims (N1) before the wave 2 scenarios, or the breakouts first as straight fights and the aims after?
5. The prizes' war: the ex-German designs as they are (looks of Kiel), or in French and Italian trim?
