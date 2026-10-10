# Hull Form Variations for the Ship Designer

Oct 10, 2026 · @Teemu

## Overview

Recommendation: split the hull into four independent region selectors plus a set of add-on checkboxes, rather than one flat "hull type" list. Historical ships mixed and matched these features freely (a tumblehome section with a ram bow and a counter stern was a normal 1895 French battleship), so per-region choices give far more combinations for the same implementation cost.

| Designer input | Control | Example options |
| --- | --- | --- |
| Cross-section | Dropdown + strength slider | Wall-sided, tumblehome, flared, V-section, hard chine |
| Bow | Dropdown | Plumb, ram, clipper, Atlantic, bulbous, knuckled, wave-piercing |
| Stern | Dropdown | Counter, cruiser, transom, tunnel |
| Deck profile | Dropdown + sheer slider | Flush, raised forecastle, stepped, turtleback, low-freeboard breastwork |
| Add-ons | Checkboxes | Torpedo bulges, bilge keels, double/triple bottom, Pugliese cylinders, ice belt |

Each option should touch four physics channels that your hull generator already models: **stability** (metacentric height GM and the righting-arm curve at large heel), **reserve buoyancy** (volume above the waterline), **resistance** (speed per unit of power), and **seakeeping** (wetness, roll, gun-platform steadiness). The interesting trade-offs come from options that win on one channel and lose on another, which is exactly what tumblehome does.

## Hull cross-section forms

The cross-section is the highest-value variation because it changes the shape of the whole hull above the waterline, so it changes both hitboxes and the righting-arm curve. Make it a dropdown with a 0–100% strength slider so tumblehome and flare can be mild or extreme.

### Tumblehome

Tumblehome is a hull that narrows above the waterline; its opposite is flare. The French Navy championed it in the 1890s (Jauréguiberry, Charles Martel, Masséna, Bouvet, Charlemagne class). France exported it to Russia: [Tsesarevich](https://en.wikipedia.org/wiki/Russian_battleship_Tsesarevich) was built at La Seyne on Jauréguiberry's lines, and the four Russian-built Borodino-class ships copied her.

- **Why designers used it:** contemporary sources credit it with higher freeboard, lighter upper structure, wider firing arcs for guns in side sponsons (wing turrets could fire nearly fore-and-aft), and less roll in heavy weather ([naval-encyclopedia](https://naval-encyclopedia.com/ww1/russia/tsesarevich.php)). Sloped upper sides also made flat-trajectory shells strike at an angle, the same logic as sloped tank armour.
- **The cost:** reduced buoyancy and stability, with excessive heel in hard turns. The Russo-Japanese War showed these ships were good ocean steamers but dangerously unstable once watertight integrity was breached ([Wikipedia: Tumblehome](https://en.wikipedia.org/wiki/Tumblehome)). At Tsushima in 1905, three Borodinos were sunk and Oryol was captured; overloading with coal had already pushed their belts underwater. Bouvet capsized within minutes after a mine at the Dardanelles in 1915.
- **Modern revival:** USS Zumwalt uses a wave-piercing tumblehome for radar stealth, and narrow wave-piercers often add outriggers to recover stability.

**Game effects to model:** less topside weight (more budget for armour or guns); wider arcs for wing mounts; smaller silhouette and slightly angled upper-hull armour; righting arm peaks earlier and drops off faster at large heel; less reserve buoyancy so flooding causes capsize sooner; more heel in turns. Borodino-style detail: tumblehome can be broken by flat-sided casemate sections, so allow it fore and aft only.

### Other cross-sections

| Option | Era | Historical example | Game trade-off |
| --- | --- | --- | --- |
| Wall-sided | All | Most battleships after 1906 | Baseline |
| Flared topsides | 1900s–present | Japanese cruisers and destroyers, late-war US ships | Drier forecastle, steadier gun platform in a seaway; more topside weight; larger silhouette |
| Fine V-section | 1890s–1945 | Destroyers, torpedo boats | Low resistance at high speed; more roll, smaller GM, less internal volume |
| Hard chine / planing | 1910s–present | Coastal motor boats, PT boats, MTBs | Very high speed on light hulls; pounding and poor seakeeping in rough water; only for small craft |
| Box / flat-bottom | 1860s–1945 | Monitors, river gunboats | Shallow draft, big beam, very stable; slow and wet |

## Bow forms

The bow is the most visually distinctive choice and has a clean historical storyline: ram (1860s–1900s) → plumb and clipper (1900s–1920s) → flared, knuckled and bulbous (1930s–1945) → wave-piercing (modern).

| Bow | Era | Historical example | Game effects |
| --- | --- | --- | --- |
| Ram | 1862–c.1910 | Ferdinand Max sank Re d'Italia at Lissa (1866); Camperdown sank HMS Victoria by accident (1893) | Enables ramming attack; heavy, wet forward; bow damage floods fast; easy to wreck your own ship in collisions |
| Plumb / straight stem | 1900s–1920s | HMS Dreadnought, early dreadnoughts | Baseline; cheap; wet at high speed |
| Clipper | 1890s–1930s | Many light cruisers and yachts | Drier than plumb; longer overall for same waterline |
| Flared "Atlantic" bow | 1930s–1945 | Bismarck and Scharnhorst after refit | Much drier in heavy seas; better forward gun use in weather; topside weight |
| Knuckle bow | 1930s–1945 | Iowa class, many Japanese and British destroyers | Deflects spray from the deck at low weight cost; mild seakeeping bonus |
| Bulbous forefoot | 1910–present | USS Delaware (1910), Yamato and Musashi | Lower drag near design speed, penalty off-design; small extra underwater hitbox |
| Hurricane bow | 1940s–1950s | Carriers that enclosed the forecastle up to the flight deck | Protects flight deck and bow AA from green water; carriers only |
| Wave-piercing / reverse | 1990s–present | USS Zumwalt | Low radar signature, slices through waves; wet, reduced reserve buoyancy forward |

**Ram.** After Lissa, most capital ships carried rams for about 40 years, but their victims were mostly friendly ships in accidents. HMS Iron Duke sank HMS Vanguard in 1875, and HMS Dreadnought rammed and sank U-29 in 1915 ([IMarEST](https://www.imarest.org/resource/ram-bow-revival-that-became-the-bulb.html)). A ram option that adds a ramming weapon plus a self-damage risk in fleet manoeuvres captures this history well.

**Bulbous bow.** The bulb descends directly from the ram: towing tests before 1900 showed an underwater ram shape cut resistance, and David W. Taylor used a "bulbous forefoot" on USS Delaware in 1910 ([Wikipedia: Bulbous bow](https://en.wikipedia.org/wiki/Bulbous_bow)). Japan used modest bulbs on Ōyodo, Shōkaku and Taihō and a much larger one on the Yamato class. The bulb works best at one speed band, so model it as a drag reduction centred on a player-chosen design speed.

## Stern forms

The stern decides waterline length, rudder exposure and high-speed resistance. Historically the counter stern gave way to the cruiser stern in the early 20th century, partly because the cruiser stern kept the rudder fully submerged, which mattered in a ship that expected to be shot at ([US Naval Historical Center](https://ibiblio.org/hyperwar/OnlineLibrary/photos/glossary/glos-c/cru-strn.htm)).

| Stern | Era | Example | Game effects |
| --- | --- | --- | --- |
| Counter | 1860s–1910s | Pre-dreadnoughts, early cruisers | Overhang above water; rudder and steering gear partly exposed above the armour deck; shorter waterline, so slightly slower |
| Cruiser | 1905–1945 | Most WW1–WW2 capital ships | Rudder fully submerged; waterline longer than length between perpendiculars, so lower resistance; steering gear lower under armour |
| Transom | 1930s–present | Late-war destroyers, modern frigates | Higher drag at low speed but lower at high speed, so it suits fast ships ([Marine Insight](https://www.marineinsight.com/naval-architecture/different-types-ships-sterns/)); more aft deck area for depth charges, mines, AA |
| Tunnel | 1900s–1940s | River gunboats | Propellers in recesses for very shallow draft; poor at sea |

**Rudder layout as a sub-option.** Single centreline rudder, twin rudders side by side, or twin rudders behind each shaft. This is a good place for gameplay drama: a hit aft that jams the rudder (as with Bismarck in 1941) should be possible, and twin widely spaced rudders should make that less likely at a weight cost.

## Deck and freeboard profiles

Freeboard is where stability, seakeeping and target size trade off most directly, and the low-freeboard ironclads give you some of the best cautionary tales. Pair a profile dropdown with a sheer slider (how much the deck rises toward bow and stern).

| Profile | Era | Example | Game effects |
| --- | --- | --- | --- |
| Low freeboard (monitor) | 1862–1880s | USS Monitor, Union river monitors | Tiny target, very steady; swamps in any sea; coastal only |
| Breastwork | 1868–1880s | HMVS Cerberus, HMS Devastation | Armoured raised box amidships keeps turrets, hatches and funnels dry while hull stays low |
| Flush deck | 1900s–1945 | US "four-piper" destroyers, Furutaka class | Continuous longitudinal strength, lighter hull; less freeboard at the bow unless sheer is added |
| Raised forecastle | 1890s–present | Most destroyers and cruisers | Drier bow and better speed into head seas; a step that weakens the hull girder if placed badly |
| Stepped / quarterdeck drop | 1890s–1945 | Many British and Japanese cruisers | Saves weight aft; low quarterdeck floods in following seas |
| Undulating ("wave") sheer | 1920s–1945 | Yūbari, Japanese heavy cruisers | High bow with weight saved amidships and aft; distinct silhouette |
| Turtleback forecastle | 1870s–1900s | Early torpedo boats and destroyers | Sheds water but leaves no useful deck forward; only light guns |

**Breastwork and Devastation.** Edward Reed's breastwork monitor raised the turrets and openings on an armoured box so the ship could keep a monitor's low hull without flooding through the deck ([Wikipedia: Breastwork monitor](https://en.wikipedia.org/wiki/Breastwork_monitor)). Devastation was designed with only about 4 ft 6 in (1.4 m) of freeboard. After HMS Captain capsized in 1870, her design was changed to carry the breastwork structure out to the sides to improve stability at large heel. She then proved a steady gun platform, though her low forecastle was washed down and limited speed into head seas ([Navypedia](https://www.navypedia.org/ships/uk/brit_bb1_devastation.html)).

**Japanese weight-saving hulls.** Yuzuru Hiraga's Yūbari (1923) built the belt and deck armour into the hull structure and used a high, strongly flared forecastle for seakeeping ([naval-encyclopedia](https://naval-encyclopedia.com/ww2/japan/yubari.php)). The Furutaka class carried this into a flush deck whose continuous longitudinal members saved weight and added strength. Both still came out heavily overweight, roughly 1,000 tons for Furutaka ([Wikipedia: Furutaka](https://en.wikipedia.org/wiki/Japanese_cruiser_Furutaka)). An "integral armour" checkbox that saves hull weight but makes armour damage also weaken hull strength would model this nicely.

## Underwater protection and hull add-ons

These work best as checkboxes because they were often retrofitted. Each changes the underwater hull shape, so it changes the hitbox, the beam, and the drag.

| Add-on | Era | Example | Game effects |
| --- | --- | --- | --- |
| External torpedo bulge | 1914–1945 | Edgar-class cruisers, WW1 monitors, interwar battleship rebuilds | Absorbs torpedo hits; wider beam raises stability; big speed loss |
| Internal bulge / torpedo belt | 1920s–1945 | US and British treaty battleships | Same idea built inside the hull; less drag, but costs internal volume and weight |
| Pugliese cylinders | 1930s–1945 | Littorio class, rebuilt Cavour and Duilio classes | Compact, lighter than layered bulkheads; one hit per section; weaker near the ends where the hull narrows |
| Double / triple bottom | 1860s–present | HMS Warrior onward; triple bottoms on WW2 battleships | Protects against grounding and magnetic mines under the keel; weight and height cost |
| Bilge keels | 1870s–present | Almost all ocean ships | Less roll for a steadier gun platform; small drag cost |
| Ice belt / strengthened bow | 1890s–present | Russian and Scandinavian ships | Operate in ice; heavier bow |

**Torpedo bulge.** British DNC Eustace Tennyson-d'Eyncourt fitted bulges to four old Edgar-class cruisers in 1914. An outer air-filled layer ruptures and an inner water-filled layer spreads the shock, with transverse bulkheads limiting flooding ([Wikipedia: Anti-torpedo bulge](https://en.wikipedia.org/wiki/Anti-torpedo_bulge)). On HMS Grafton the bulges cost 4 knots, but when she was torpedoed off Gallipoli damage stayed limited and she reached Malta under her own power ([Wikipedia: HMS Grafton](<https://en.wikipedia.org/wiki/HMS_Grafton_(1892)>)). That is a perfect game trade-off: a big speed penalty for real survivability.

**Pugliese system.** A hollow air-filled cylinder up to 3.8 m in diameter ran along each side inside a liquid-filled space; a torpedo blast crushed the cylinder, absorbing energy ([Regia Nave Roma](https://regianaveroma.org/en/scheda-incisa-della-rocchetta)). Its reputation is mixed: some histories call it a failure, while others argue most Taranto hits landed outside the system ([Navy General Board](https://www.navygeneralboard.com/?p=1883)). Model it as strong per hit, but single-use per section and tapering in effect toward the bow and stern.

## Oddities and special hull types

These are less about fine-tuning and more about fun: whole hull archetypes that let players build historically real "what were they thinking" ships. Like every hull shape they are buildable in any era; the AI simply keeps them to their historical window.

| Hull type | Era | Example | Game effects |
| --- | --- | --- | --- |
| Circular (popovka) | 1870s | Novgorod, Vitse-admiral Popov | Huge displacement and armour per length, very shallow draft, rock-steady; very slow, loses headway in storms, spins under recoil and current |
| Elongated round hull | 1880 | Imperial yacht Livadia | Popovka fixed with pointed ends; keeps the steadiness, reduces the spinning |
| Monitor / bulged bombardment hull | 1862–1945 | Civil War monitors, RN Erebus class | Massive guns on a cheap, shallow hull; 6–12 knots; coastal |
| Stepped hydroplane | 1910s–1940s | Coastal motor boats | Very fast in calm water; useless in a seaway |
| Catamaran / trimaran | 1990s–present | Type 022 missile boat, Independence-class LCS | Huge deck area and stability with narrow, fast hulls; complex structure |

**Popovkas.** John Elder proposed widening a warship's beam to shrink the area needing armour; Rear-Admiral Popov took it to a full circle with a flat bottom for shallow-water coast defence ([military-history.org](https://military-history.org/articles/the-novgorod.htm)). Novgorod was a steady gun platform, rolling rarely beyond 7–8°, but lost all headway in a force 8 storm in 1877. Reports say river current on the Dnieper spun both ships uncontrollably ([Jalopnik](https://jalopnik.com/russia-once-built-a-very-weird-circular-warship-1847496604)). William Froude's tank tests for a larger circular ship found it would need five times the power of a conventional hull for the same speed, but lengthening it cut that sharply ([Naval Gazing](https://www.navalgazing.net/Exotic-Hulls-Part-7)). Your generator could expose this as a length-to-beam slider that is allowed to go all the way down to 1:1, letting physics punish the player honestly.

**Length-to-beam as a continuous input.** Even outside the oddities, one slider covers a lot of history: about 4:1 for monitors, 6:1 for pre-dreadnoughts, 7–8:1 for fast battleships and battlecruisers, and 10:1 or more for destroyers. Longer and thinner means faster and less stable; shorter and wider means steadier and slower.

## Implementation notes

Most options reduce to a handful of geometric modifiers on the generator's section and profile curves, so the physics comes out of the mesh rather than hand-tuned bonuses. Hand-tuned values are only needed where geometry alone does not capture the effect (ramming, Pugliese single-use, rudder jam).

| Option | Geometry change | Emerges from physics | Needs explicit rule |
| --- | --- | --- | --- |
| Tumblehome | Section half-breadth shrinks above waterline by strength × height | Lower GZ at large heel, less reserve buoyancy, lighter topsides | Wider arcs for wing mounts; angled upper armour |
| Flare | Half-breadth grows above waterline, mostly forward | More reserve buoyancy forward, more topside weight | Wetness / spray reduction |
| Ram, bulb | Extra volume at forefoot below waterline | Underwater hitbox, displacement | Ramming damage; bulb drag curve vs design speed |
| Cruiser vs counter stern | Waterline length and overhang | Speed via waterline length | Rudder exposure above armour |
| Transom | Truncated stern at waterline | Speed-dependent drag from the existing Froude-based resistance | Aft deck area |
| Bulges | Added outboard volume amidships below waterline | Beam, GM, drag | Torpedo damage absorption layers |
| Freeboard / sheer | Deck height curve along length | Reserve buoyancy, silhouette, weight | Deck wetness by sea state |
| Length-to-beam | Overall proportions | Speed, stability, turning | None |

**Suggested build order**, from most gameplay per unit of effort:

1. Tumblehome and flare as a single signed "topside shape" slider (negative = tumblehome, positive = flare).
2. Bow dropdown with ram, plumb, clipper, flared and bulbous.
3. Stern dropdown (counter, cruiser, transom).
4. Torpedo bulge checkbox, as a retrofit option in refits.
5. Freeboard profile and sheer slider.
6. Oddities (popovka, monitor hull) as extra hull archetypes.

**Era as style, not gate.** All hull shapes are available in every era; the Era columns in this doc are style windows for default designs and the AI, not unlocks. Suggested AI preferences: ram bows 1860s–c.1910; tumblehome for French and Russian-style designs 1890–1905; counter sterns before c.1910, cruiser sterns after; bulbous bows from 1910 on fast ships; bulges on refits from 1914; Pugliese-style and internal belts from the 1930s. Era-gated material and engine tech will still limit some shapes naturally without extra rules: a hard-chine planing hull is only fast with light, high-power engines, and a big bulbous bow only pays off at speeds early machinery can't reach.

**Feedback in the designer.** The tumblehome trade-off only reads if players can see it. Showing a live righting-arm (GZ) curve and a reserve-buoyancy figure next to the hull preview would let them watch tumblehome lower the curve at large angles while freeing weight. That makes the Borodino lesson something players learn by playing.

**Using the existing Froude model.** Since resistance already varies with Froude number, bulbous bows and transom sterns can be pure geometry: the bulb's wave-cancelling benefit and the transom's high-speed advantage should emerge from the hull shape feeding that model, with no extra drag term.

## Sources

Search-result excerpts were used for these pages; the Bouvet capsize, Bismarck rudder hit, HMS Captain loss and Zumwalt references are from general knowledge.

- [Wikipedia: Tumblehome](https://en.wikipedia.org/wiki/Tumblehome)
- [Wikipedia: Russian battleship Tsesarevich](https://en.wikipedia.org/wiki/Russian_battleship_Tsesarevich)
- [naval-encyclopedia: Tsesarevich](https://naval-encyclopedia.com/ww1/russia/tsesarevich.php)
- [Wikipedia: Bulbous bow](https://en.wikipedia.org/wiki/Bulbous_bow)
- [IMarEST: Ram bow revival that became the bulb](https://www.imarest.org/resource/ram-bow-revival-that-became-the-bulb.html)
- [CIMSEC: The Ram, a 19th-century naval warfare dead end](https://cimsec.org/?p=10658)
- [US Naval Historical Center: Cruiser sterns](https://ibiblio.org/hyperwar/OnlineLibrary/photos/glossary/glos-c/cru-strn.htm)
- [Marine Insight: Types of ship sterns](https://www.marineinsight.com/naval-architecture/different-types-ships-sterns/)
- [Wikipedia: Breastwork monitor](https://en.wikipedia.org/wiki/Breastwork_monitor)
- [Navypedia: Devastation class](https://www.navypedia.org/ships/uk/brit_bb1_devastation.html)
- [naval-encyclopedia: Yūbari](https://naval-encyclopedia.com/ww2/japan/yubari.php)
- [Wikipedia: Japanese cruiser Furutaka](https://en.wikipedia.org/wiki/Japanese_cruiser_Furutaka)
- [Wikipedia: Anti-torpedo bulge](https://en.wikipedia.org/wiki/Anti-torpedo_bulge)
- [Wikipedia: HMS Grafton (1892)](<https://en.wikipedia.org/wiki/HMS_Grafton_(1892)>)
- [Regia Nave Roma: Littorio and Vittorio Veneto technical sheet](https://regianaveroma.org/en/scheda-incisa-della-rocchetta)
- [Navy General Board: Pugliese system](https://www.navygeneralboard.com/?p=1883)
- [military-history.org: Novgorod](https://military-history.org/articles/the-novgorod.htm)
- [Jalopnik: Russia's circular warship](https://jalopnik.com/russia-once-built-a-very-weird-circular-warship-1847496604)
- [Naval Gazing: Exotic Hulls part 7](https://www.navalgazing.net/Exotic-Hulls-Part-7)
