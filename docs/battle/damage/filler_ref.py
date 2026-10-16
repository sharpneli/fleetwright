"""filler_ref.py -- reference for 12_explosive_fillers.md (Fleetwright damage research).

The shell filler as a parameter: the explosives catalogue that shells-armour-damage.md open question 2 asks for,
and the three-stage hit model that uses it (impact -> initiation -> effect).

Pure Python, no dependencies. `python filler_ref.py` prints every table quoted in the note.
Units: kg. All stats relative to TNT = 1.00.
Tags in comments: [SRC] sourced, [FIT] fitted here to the cases, [EST] judgement, [UNCERTAIN] thin sources.
"""
from dataclasses import dataclass


@dataclass(frozen=True)
class Explosive:
    name: str
    re: float           # blast TNT equivalence (the shell block's filler.re)
    brisance: float     # shatter / fragmentation vs TNT                                     [EST]
    in_plate: float     # P(bursts in or on the plate) at r = 1 (armour / penetration); scales with r^2
    full_basic: float   # P(full detonation) with a black-powder booster
    full_ce: float      # P(full detonation) with a CE / tetryl booster
    fire: float         # incendiary factor, black powder = 1.0                               [EST]
    storage: str        # hidden hazard class
    cost: float         # relative cost per ton, TNT = 1.0                                     [EST]
    cast: bool          # cast-loadable (fast filling) vs pressed (slow)
    year: int           # first naval service (approx.)


# re: armor/04 §2.1 where sourced (black powder 0.55, picric 1.17, TNT 1.00) [SRC]; the rest [EST]/[UNCERTAIN].
# in_plate: lyddite 0.35 is the sourced rate (armor/04 §6) [SRC]; Shellite 0.0 as the block's example [SRC];
#   the others placed relative to them by the qualitative record [FIT].
# full_ce: Shellite 0.90 with CE (armor/04, "90% full detonation") [SRC]; others [EST].
CATALOGUE = [
    Explosive("Black powder",   0.55, 0.15, 0.15, 1.00, 1.00, 1.00, "low (damp -> duds)", 0.30, True,  1860),
    Explosive("Wet guncotton",  0.85, 0.90, 0.08, 0.55, 0.85, 0.20, "low",                0.90, False, 1890),
    Explosive("Lyddite",        1.17, 1.15, 0.35, 0.95, 0.97, 0.40, "high",               0.90, True,  1888),
    Explosive("Shimose",        1.17, 1.20, 0.45, 0.95, 0.97, 0.50, "high + in-bore",     1.00, True,  1893),
    Explosive("TNT",            1.00, 1.00, 0.10, 0.60, 0.95, 0.25, "very low",           1.00, True,  1902),
    Explosive("TNT/wax 90/10",  0.90, 0.90, 0.04, 0.55, 0.93, 0.25, "very low",           1.00, False, 1905),
    Explosive("Explosive D",    0.95, 0.90, 0.02, 0.50, 0.92, 0.20, "low",                1.10, False, 1911),
    Explosive("Shellite",       1.00, 1.05, 0.00, 0.60, 0.90, 0.35, "medium (ages)",      0.90, True,  1918),
    Explosive("TNA",            1.00, 1.00, 0.05, 0.60, 0.93, 0.25, "low",                1.20, True,  1931),
    Explosive("Amatol 60/40",   1.00, 0.85, 0.10, 0.60, 0.93, 0.15, "medium (damp)",      0.75, True,  1915),
    Explosive("Amatol 80/20",   0.95, 0.65, 0.12, 0.55, 0.90, 0.10, "medium (damp)",      0.45, False, 1915),
]

LOW_ORDER_SHARE = 0.75   # of the non-full outcomes; the rest are duds                            [EST]
LOW_ORDER_EFFECT = 0.30  # blast of a low-order burst, share of full                               [EST]
IN_PLATE_BEHIND = 0.15   # share of the effect reaching the spaces behind a plate burst (spall)    [EST]


def p_in_plate(e: Explosive, r: float) -> float:
    """r = armour thickness / the shell's penetration there (0 = unarmoured)."""
    return 0.0 if r <= 0 else min(1.0, e.in_plate * r * r)


def expected_inside_tnt(e: Explosive, filler_kg: float, r: float, ce: bool) -> float:
    """Expected TNT-equivalent kg delivered inside the target per hit."""
    full = e.full_ce if ce else e.full_basic
    pp = p_in_plate(e, r)
    q = full + (1 - full) * LOW_ORDER_SHARE * LOW_ORDER_EFFECT
    w = filler_kg * e.re
    return (1 - pp) * q * w + pp * IN_PLATE_BEHIND * w


SCENARIOS = [
    ("A AP 15kg r0.9 CE",    15, 0.9, True),
    ("B AP 15kg r0.9 basic", 15, 0.9, False),
    ("C AP 15kg r0.5 CE",    15, 0.5, True),
    ("D HE 50kg r0 CE",      50, 0.0, True),
    ("E HE 50kg r0 basic",   50, 0.0, False),
]

if __name__ == "__main__":
    print("Expected TNT-equivalent kg inside the target per hit\n")
    print(f"{'Explosive':15}" + "".join(f"{s[0]:>22}" for s in SCENARIOS))
    for e in CATALOGUE:
        row = "".join(f"{expected_inside_tnt(e, m, r, ce):>22.1f}" for _, m, r, ce in SCENARIOS)
        print(f"{e.name:15}{row}")
