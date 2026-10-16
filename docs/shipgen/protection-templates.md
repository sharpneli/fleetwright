# Protection scheme templates

The layouts families of ships shared (`docs/shipgen/protection-plan.md` H8, research `09_protection_schemes.md` §6.8),
as blocks a design copies. A scheme names no year and sets only how the protection is laid out: the inputs of the
protection plan's steps H2-H6 (slopes, inclined and internal belts, the inner bulkhead, magazine boxes, zones). It
never adds or removes a belt or a deck the design doesn't have, except a scheme's light lower deck. Values are typical,
not one ship's: a design moved onto its family keeps its own where its history differs (the table at the end).

`tools/designer_templates.py` writes them to `Content/Templates/protection.json`, the designer's catalogue;
`ProtectionSchemes.Apply` puts one on a design (the designer's template button, and `shipgen scheme`).

| field | effect |
|---|---|
| `slope_deg`, `flat_share` | Slopes on the citadel's main armour deck (its thickest over the citadel, or the whole length): the deck's thickness goes to the slopes, the flat takes `flat_share` of it, and the slopes lean at `slope_deg` off the horizontal down to the belt's lower edge. Applied again, it gives the same deck. |
| `lower_deck` | `{mm, slope_mm, slope_deg}`: a light armour deck one deck under the main armour deck, with slopes. |
| `belt_position`, `belt_incline_deg`, `belt_extent` | As the design's own keys: the belt inside the shell, leaning (top outboard), over the machinery only. |
| `belt_bottom_share` | The belt's lower edge as a share of its thickness (`belt_bottom_mm`): an internal belt tapers, or its lower belt is heavy. |
| `inner_bulkhead`, `magazine_box` | As the design's own blocks, replacing them. |

What isn't here yet: TDS layers. The all-or-nothing and inclined-internal families had four or five (liquid and void),
but the torpedo model still holes every layer within reach at once (protection-plan.md H3, open), so no scheme gives a
design layers until that is settled. The magazine-first schemes need no inputs: every design already has its magazines
low and inboard (H1-H2), and an all-or-nothing citadel or a structural cruiser belt is the plain inputs.

## Capital ships

**Belt and slope** (`belt_and_slope`)

The pre-1915 two-line scheme (§2.1): a turtleback behind the belt, coal over the slopes, a screen bulkhead beside the
magazines. Majestic, Mikasa, Dreadnought, the battlecruisers, Cressy as an armoured cruiser.

```json
"scheme": {"slope_deg": 30, "flat_share": 0.65, "inner_bulkhead": {"mm": 0, "magazines_mm": 38}}
```

**German bulkhead** (`german_bulkhead`)

The same turtleback with a torpedo bulkhead the length of the citadel behind the coal (§2.3): Nassau, Kaiser,
Derfflinger.

```json
"scheme": {"slope_deg": 30, "flat_share": 0.65, "inner_bulkhead": {"mm": 30, "magazines_mm": 40}}
```

**Turtleback, modern** (`turtleback_modern`)

A low armour deck with shallow slopes behind the main belt, an upper belt and deck over it: Bismarck, Scharnhorst,
Hipper as a cruiser, Deutschland's.

```json
"scheme": {"slope_deg": 22, "flat_share": 0.73}
```

**Inclined, internal** (`inclined_internal`)

The belt inside the shell, leaning out at its top, carried on down as a lower belt tapering to the bottom: Nelson,
South Dakota, Iowa.

```json
"scheme": {"belt_position": "internal", "belt_incline_deg": 19, "belt_bottom_share": 0.3}
```

**Inclined, external** (`inclined_external`)

An all-or-nothing belt on the side, leaning out at its top so plunging fire meets it more obliquely: Hood, Yamato,
Littorio.

```json
"scheme": {"belt_incline_deg": 15}
```

**French modern** (`french_modern`)

An external inclined belt under a heavy upper deck, and a light lower deck whose slopes meet the belt's lower edge;
the holding bulkhead thickened beside the magazines and a plate under them against mines: Dunkerque, Richelieu.

```json
"scheme": {"belt_incline_deg": 11, "lower_deck": {"mm": 40, "slope_mm": 40, "slope_deg": 30}, "inner_bulkhead": {"mm": 0, "magazines_mm": 20}, "magazine_box": {"floor_mm": 30}}
```

## Cruisers

**Protected deck** (`protected_deck`)

The deck alone, flat over the middle and sloping below the waterline at the sides, the slopes much thicker (§4.1):
Olympia, Esmeralda. On a belted ship, its deck: the scout cruisers.

```json
"scheme": {"slope_deg": 30, "flat_share": 0.45}
```

**Machinery belt and magazine boxes** (`cruiser_box`)

The treaty cruiser's (§4.4): a belt over the machinery only, the magazines in armoured boxes low in the hull. Kent
(the boxes as designed), Leander, New Orleans.

```json
"scheme": {"belt_extent": "machinery", "magazine_box": {"side_mm": 102, "crown_mm": 64, "end_mm": 64}}
```

## The shipped designs on their schemes

Moved 2026-10-16 with `shipgen scheme` (their own values where their history differs, in brackets). Standard
displacement before and after, and the compartment standard (protection-plan.md H7) where it moved. Designs not listed
keep the inputs they had: destroyers, escorts, merchants and carriers (magazines low and inboard already), the
all-or-nothing US ships and the generic battleship and cruisers (their plain inputs are their scheme), the citadel and
breastwork ships, the monitors and the fantasies. The scenarios' designs (Content/Scenarios/Designs) are tuned for
balance and stay as they are.

| Design | Scheme | Standard, t |
|---|---|---|
| dreadnought | belt_and_slope | 24,388 → 25,354 (+4.0 %) |
| lord_nelson | belt_and_slope | 19,968 → 20,890 (+4.6 %) |
| mikasa | belt_and_slope | 11,557 → 11,828 (+2.3 %) |
| connecticut | belt_and_slope | 16,369 → 16,527 (+1.0 %) |
| invincible | belt_and_slope | 18,487 → 20,020 (+8.3 %) |
| lion | belt_and_slope | 36,836 → 38,565 (+4.7 %) |
| tiger_1914 | belt_and_slope | 34,204 → 35,570 (+4.0 %) |
| kongo | belt_and_slope | 35,868 → 37,724 (+5.2 %); standard 3 → 2 |
| super_dreadnought | belt_and_slope | 27,601 → 28,450 (+3.1 %); standard 1 → 2 |
| battlecruiser | belt_and_slope | 29,382 → 31,770 (+8.1 %) |
| armoured_cruiser | belt_and_slope | 13,522 → 13,601 (+0.6 %) |
| dante | belt_and_slope | 25,600 → 27,007 (+5.5 %) |
| courbet | belt_and_slope | 23,297 → 24,124 (+3.5 %) |
| danton | belt_and_slope | 21,905 → 22,938 (+4.7 %) |
| nassau | german_bulkhead | 18,515 → 19,352 (+4.5 %) |
| nassau_casemates | german_bulkhead | 19,645 → 20,496 (+4.3 %) |
| seydlitz | german_bulkhead (bulkhead 50/50 mm) | 31,450 → 34,422 (+9.4 %) |
| tegetthoff | german_bulkhead | 20,016 → 21,398 (+6.9 %) |
| borodino | german_bulkhead | 12,596 → 13,412 (+6.5 %) |
| bismarck | turtleback_modern (deck 80 mm flat, 110 slopes) | 39,600 → 39,313 (−0.7 %) |
| deutschland | turtleback_modern | 9,488 → 9,615 (+1.3 %) |
| nelson | inclined_internal (18°) | 29,034 → 29,853 (+2.8 %) |
| battleship_layered | inclined_internal | 54,231 → 55,045 (+1.5 %) |
| yamato | inclined_external (20°) | 54,520 → 55,132 (+1.1 %) |
| littorio | inclined_external (11°) | 40,944 → 41,078 (+0.3 %); standard 1 → 2 |
| dunkerque | french_modern | 20,536 → 22,292 (+8.6 %) |
| richelieu | french_modern (15°) | 34,685 → 37,929 (+9.4 %) |
| karlsruhe | protected_deck | 6,304 → 6,457 (+2.4 %) |
| kent | cruiser_box | 9,283 → 10,044 (+8.2 %) |
| kgv | (zones: belt 348 mm, 374 at the magazines; deck 127, 152 at the magazines) | 33,859 → 34,026 (+0.5 %) |
