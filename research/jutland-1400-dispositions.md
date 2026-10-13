# Jutland at 14:00 GMT: positions, courses, dispositions
Status: final    Updated: 2026-10-13    Request: [REQUESTS.md](REQUESTS.md) "Jutland at 14:00 GMT" (Done)

## Summary

The British 2 p.m. dispositions are logged in the despatches and the Admiralty's Narrative of the Battle, so they're
recorded positions. The German ones aren't in any source found online; they're reckoned from the first contact. Put
into `Content/Scenarios/jutland.json` on 2026-10-13 (commit `8f1d289`). Times are GMT; German sources use CET, one
hour ahead.

| Formation | Lead ship at 14:00 | Course, speed | Disposition |
|---|---|---|---|
| 1st BCS (Beatty) | Lion, 56°46'N 4°40'E | N by E (011°), 19.5 kn: just turned north to meet Jellicoe | single line ahead, screened by Champion and the 13th Flotilla (with Turbulent and Termagant) |
| 2nd BCS | New Zealand, 3 miles ENE of Lion | as Lion | single line ahead, screened by six Harwich destroyers (Lydiard, Liberty, Landrail, Laurel, Moorsom, Morris) |
| 5th Battle Squadron | Barham, 5 miles NNW of Lion | as Lion | single line ahead, screened by Fearless and nine of the 1st Flotilla |
| BCF light cruisers | screen 8 miles SSE of Lion (astern after the turn) | as Lion | line ENE-WSW, ships 5 miles apart; Engadine between Gloucester and Cordelia; Yarmouth linking ship to Lion |
| Battle fleet (Jellicoe) | Iron Duke (3rd column), 57°57'N 3°45'E | S 50 E (130°), zigzagging, 14 kn speed of advance | organisation No. 5: six divisions in line ahead, abeam to starboard, 1st to 6th port to starboard |
| 4th LCS | 3 miles ahead of the battle fleet | as the fleet | spread |
| Cruiser screen | 16 miles ahead of the battle fleet | as the fleet | 8 miles apart (one reading says 6) on a line N 40 E - S 40 W, east to west: Cochrane, Shannon, Minotaur, Defence, Duke of Edinburgh, Black Prince, Warrior; Hampshire linking 6 miles astern of Minotaur; one destroyer per cruiser |
| Attached cruisers | flanks of the battle fleet | as the fleet | Active, Bellona, Blanche, Boadicea (which flank each was on: not found) |
| 3rd BCS (Hood), Chester, Canterbury | about 20 miles ahead of the battle fleet | as the fleet | Chester linking with the cruiser line, Canterbury ahead |
| I Scouting Group (Hipper) | Lützow, reckoned ~56°44'N 6°14'E, about 50 miles east of Beatty | N, 16 kn | line ahead |
| II Scouting Group, torpedo boats | screen in an arc ~8 miles ahead and to either side | N, 16 kn | Elbing on the western wing, about 8 miles ahead of the battlecruisers; B109 and B110 sent west to examine the Danish steamer N. J. Fjord |
| High Seas Fleet (Scheer) | König, about 50 miles astern of Hipper (some accounts say 60) | N, 14 kn | one line: III Squadron, Friedrich der Grosse, I Squadron, II Squadron; IV Scouting Group and torpedo boats screening |

**Battle fleet order (Jellicoe's despatch):** 1st Div. King George V, Ajax, Centurion, Erin; 2nd Orion, Monarch,
Conqueror, Thunderer; 3rd Iron Duke, Royal Oak, Superb, Canada; 4th Benbow, Bellerophon, Temeraire, Vanguard; 5th
Colossus, Collingwood, Neptune, St Vincent; 6th Marlborough, Revenge, Hercules, Agincourt.

## Details

- **The contact.** Elbing sighted the N. J. Fjord about 14:00 and sent B109 and B110 to examine her. Galatea saw the
  steamer about 17 miles to the east, and at 14:20 reported enemy vessels ESE steering north. Galatea and Phaeton
  opened fire at 14:28; Elbing hit Galatea at 14:36. Galatea's logged position at 14:25 was 56°48'N 5°21'E.
- **The German positions** come from that geometry. Elbing is near the steamer, about 17 miles east of Galatea, and
  Hipper about 8 miles south-east of Elbing, which puts him about 50 miles east of Beatty. That agrees with the
  accounts that have him "on the same latitude, fifty miles east". The German official history (Groos, *Der Krieg in
  der Nordsee* vol. 5) has the logged positions. It wasn't available online, so these are the first thing to correct
  from it.
- **The light-cruiser order is uncertain.** The Narrative lists the screen west to east as Southampton, Nottingham,
  Falmouth, Birkenhead, Inconstant, Galatea, Birmingham, Dublin, Gloucester, Cordelia, Phaeton. Taken literally that
  puts Galatea mid-line, 25 miles from Phaeton, yet the two fought together within eight minutes of the first report.
  Galatea's logged 14:25 position also puts her well east. The list is probably mangled (a table read across). The
  scenario groups the screen by squadron instead, east to west: the 1st LCS (Galatea, Phaeton, Inconstant, Cordelia),
  Engadine, the 3rd LCS (Gloucester, Birkenhead, Falmouth), the 2nd LCS (Dublin, Nottingham, Birmingham, Southampton).
- **The turn north.** Jellicoe's despatch and the Narrative have Beatty already on N by E at 2 p.m. Some secondary
  accounts put the turn at 14:15. Either way the light-cruiser screen, laid out for the earlier south-easterly
  course, was left astern of Lion.

## Recommendations for this codebase

- Done in `Content/Scenarios/jutland.json`. Headless, Galatea sights B109 at 14:17 (historically 14:20).
- The formations can't place everything exactly. `abreast` lays a line square to the course, so the BCF light
  cruisers use `echelon-p`, which runs their line about 11° off ENE-WSW. Offsets ahead of the guide need their own top
  units (the 5th BS, Yarmouth, Hampshire, B109 and B110, the attached cruisers).
- Still open: the classes as built (see REQUESTS.md), the German logged positions (Groos), and which flank each
  attached cruiser was on.

## Sources

- [Narrative of the Action (Admiralty, via the Jutland crew lists wiki)](https://battleofjutlandcrewlists.miraheze.org/wiki/Narrative_of_the_Action)
- [Jellicoe's despatch, 2 P.M. dispositions (wtj.com)](https://www.wtj.com/archives/jellicoe/jellicoe12b.htm)
- [Official Despatches (naval-history.net)](https://www.naval-history.net/WW1Battle-Battle_of_Jutland_1916_Official_Despatches1.htm)
- [HMS Galatea at Jutland, her 14:25 position (IWM Lives of the First World War)](https://livesofthefirstworldwar.iwm.org.uk/community/6975/?page=6)
- [The N. J. Fjord and Elbing (USNI Proceedings, January 1935)](https://www.usni.org/magazines/proceedings/1935/january/log-war-time-tramp)
- [Battle of Jutland (Wikipedia)](https://en.wikipedia.org/wiki/Battle_of_Jutland)
- [Naval Gazing, Jutland Part 2](https://navalgazing.net/Jutland-Part-2)
