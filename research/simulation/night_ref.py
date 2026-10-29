"""Night sighting against the record (research/simulation/night-fighting.md section 7). The eye's dark law as the sim
fits it today (sighting_ref.py's two knees, docs/battle/night.md Q1) beside Blackwell's measured one (Crumey 2014's
fit to Blackwell 1946, checked against Taylor 1960), each run through the same sight model on the same targets, skies
and haze, and asked what scale on its threshold the record's night ranges imply.

The sim's law: the large-object threshold C_inf (0.05 by day) grows by f_a and the summation's sqrt(omega_c) by f_b,
each f = (1 + sqrt(L_k / L)) / (1 + sqrt(L_k / 30)), knees 1e-4 and 0.2 cd/m^2, L the sky's luminance through the glass.

Blackwell's (Crumey 2014, eqs. 23-38; L in cd/m^2, 50% detection, laboratory):
  C_inf(L) = 2.720e-3 for L >= 0.354, else 7.633e-3 L^-1/4 - 7.174e-3
  R(L) = (6.505e-4 L^-1/4 - 8.461e-4)^2 for L < 0.0708, else (1.772e-4 L^-1/4 + 7.167e-5)^2;  Ricco area A_R = R / C_inf
A field law built on it: C_t = F C_inf(L) (1 + sqrt(A_R(L) / A_R(30)) sqrt(omega_c / omega)), one field factor F for
day and night. The sim's day threshold 0.05 is F = 18.4 on Crumey's 2.72e-3; its omega_c (2e-4 sr) is kept and scaled
by the measured growth of the Ricco area.

Anchors (the note's section 7 has the cases): side-on targets seen by a lookout 24 m up through a 7x50 (0.6), skies
as horizon luminances toward the target (Koomen et al. 1952 for Jutland's twilight by bearing; Schlyter and Jechow
et al. 2019 for the night), visibility 10 km under overcast, 25 km clear, 8 km in Jutland's mist.

    python -I research/simulation/night_ref.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))   # its sibling, under python -I too
import sighting_ref as S  # noqa: E402

c_inf, ricco = S.c_inf, S.ricco

# the sim's law as Q1 fitted it, which night.md Q1b replaced with this note's proposal (sighting_ref.py has Q1b's now):
# two knees on (1 + sqrt(L_k / L)), a ship's night contrast 0.6
L_A, L_B, C0_Q1 = 1e-4, 0.2, 0.6


def knee(lk, lum):
    return 1.0 if lum >= S.L_WEBER else (1 + math.sqrt(lk / lum)) / (1 + math.sqrt(lk / S.L_WEBER))


def blackwell_factors(lum):
    """Blackwell's rise over 30 cd/m^2, as the sim's (f_a, f_b) are."""
    if lum >= S.L_WEBER:
        return 1.0, 1.0
    return c_inf(lum) / c_inf(S.L_WEBER), math.sqrt(ricco(lum) / ricco(S.L_WEBER))


def sim_law(k=1.0):
    """The sim's threshold today, scaled by k."""
    return lambda omega, lum: k * S.threshold(omega, knee(L_A, lum), knee(L_B, lum))


def field_law(f):
    """F times Blackwell's C_inf, the sim's omega_c grown as the Ricco area grows."""
    def th(omega, lum):
        return f * c_inf(lum) * (1 + blackwell_factors(lum)[1] * math.sqrt(S.OMEGA_C / max(omega, 1e-30)))
    return th


# The proposal (section 7 of the note): Blackwell's shape, a ship's night contrast of 0.9 against the sky, and the field
# factor F falling from the day's 18.4 below a knee of 1.8e-3 cd/m^2 at the eye (a horizon sky of 3e-3 through a 7x50)
# as the fourth root: F = 18.4 min(1, (L / 1.8e-3)^(1/4)). A fit to five fuzzy anchors [INFERRED], not a measurement.
F_DAY, F_KNEE, F_POWER, C0_PROPOSED = 18.4, 1.8e-3, 0.25, 0.9


def proposed_law():
    def th(omega, lum):
        return field_law(F_DAY * min(1.0, (lum / F_KNEE) ** F_POWER))(omega, lum)
    return th


def battleship(z):
    return S.side_area_above(z)


def destroyer(z):
    # a schematic 1916 destroyer side on: hull 85 m x 3.5 m, bridge and funnels 30 m to 7 m, masts to 15 m (~410 m^2)
    return 85 * max(3.5 - z, 0) + 30 * max(7 - max(z, 3.5), 0) + 2 * 0.5 * max(15 - max(z, 7), 0)


def made_out(law, target, vis, sky, eye=24.0, mag=7.0, glass=0.6, need=1.0):
    """The farthest range (to 10 m) at which the margin reaches `need` (1: made out at all)."""
    sigma = S.extinction(vis)
    for d in range(40_000, 10, -10):
        contrast = S.own_contrast(sky, 0.0) * math.exp(-sigma * d)
        omega = target(S.hidden(eye, d)) * mag * mag / (d * d)
        if contrast / law(omega, sky * glass) >= need:
            return d
    return 0


def implied(make_law, target, want, vis, sky):
    """The scale (k or F) on a law at which the target is made out at `want` m."""
    lo, hi = 0.01, 1000.0
    for _ in range(60):
        mid = math.sqrt(lo * hi)
        if made_out(make_law(mid), target, vis, sky) > want:
            lo = mid
        else:
            hi = mid
    return mid


# name, target, the record's range (m: low, mid, high), visibility m, sky cd/m^2, what the record is
ANCHORS = [
    ("overcast, moonless", destroyer, (200, 400, 600), 10_000, 1e-4,
     "Dover Strait 1916-18: destroyers met at point blank, under a cable to 600 yd"),
    ("clear, moonless", destroyer, (500, 1000, 2000), 25_000, 1e-3,
     "boats unseen to ~600 yd at Port Arthur and Tsushima; 'passed within 2-3 cables unseen'"),
    ("Jutland, sun -11.5, mist, away from the arch", battleship, (550, 1200, 1800), 8_000, 3e-3,
     "Jutland 23:15-00:45: 600-2,000 yd, hulls lost past ~3/4 mile"),
    ("Jutland, sun -11.5, mist, toward the arch", battleship, (2000, 3000, 3200), 8_000, 1.9e-2,
     "Nottingham: a Stettin 'not more than 2 miles', silhouetted against the sky"),
    ("Jutland, sun -9, mist, away (a lower bound)", battleship, (2300, 2300, 3000), 8_000, 3.9e-2,
     "Castor 22:05: the Germans' lights on at 2,500 yd"),
]

if __name__ == "__main__":
    print("the dark's rise over 30 cd/m^2, through a 7x50 (0.6): sky cd/m^2 -> sim f_a, f_b | Blackwell f_a, f_b")
    for sky in (1e-5, 1e-4, 3.2e-4, 1e-3, 5e-3, 2e-2, 0.1, 1.0, 30.0):
        fa, fb = knee(L_A, sky * 0.6), knee(L_B, sky * 0.6)
        ba, bb = blackwell_factors(sky * 0.6)
        print(f"  {sky:8.1e}: {fa:6.2f} {fb:7.2f} | {ba:6.2f} {bb:6.2f}")

    for c0 in (C0_Q1, 0.9):
        S.C0_NIGHT = c0
        print()
        print(f"the scale on each law the record implies (a ship's night contrast {c0}) (one scale fitting every anchor would mean the law's shape is right):")
        print("  anchor: sim's threshold x k (the record's band) | Blackwell's C_inf x F (the day's F = 18.4)")
        for name, target, (lo, mid, hi), vis, sky, _ in ANCHORS:
            k = [implied(sim_law, target, w, vis, sky) for w in (hi, mid, lo)]
            f = [implied(field_law, target, w, vis, sky) for w in (hi, mid, lo)]
            print(f"  {name}: k {k[1]:.1f} ({k[0]:.1f}-{k[2]:.1f}) | F {f[1]:.1f} ({f[0]:.1f}-{f[2]:.1f})")

    print()
    print("made out at (km): the sim today | the proposal (Blackwell, F falling below 3e-3, contrast 0.9) | the record")
    for name, target, (lo, mid, hi), vis, sky, rec in ANCHORS:
        S.C0_NIGHT = C0_Q1
        a = made_out(sim_law(), target, vis, sky) / 1000
        S.C0_NIGHT = C0_PROPOSED
        b = made_out(proposed_law(), target, vis, sky) / 1000
        print(f"  {name}: {a:4.1f} | {b:4.2f} | {lo / 1000:.2f}-{hi / 1000:.2f} ({rec})")
    S.C0_NIGHT = C0_Q1
    a = [made_out(sim_law(), t, 25_000, 0.01) / 1000 for t in (battleship, destroyer)]
    S.C0_NIGHT = C0_PROPOSED
    b = [made_out(proposed_law(), t, 25_000, 0.01) / 1000 for t in (battleship, destroyer)]
    print(f"  full moon 30 deg up (sky 0.01), battleship / destroyer: {a[0]:.1f} / {a[1]:.1f} | {b[0]:.1f} / {b[1]:.1f}"
          " | Caldera 1891: a torpedo gunboat at 2,000 yd under a clouded full moon")
    day = [made_out(law, battleship, 25_000, 19_099, glass=1) / 1000 for law in (sim_law(), proposed_law())]
    print(f"  day, 25 km visibility, battleship: {day[0]:.1f} | {day[1]:.1f} (unchanged)")

    print()
    print("a destroyer made out from a destroyer (eye 8 m), the proposal, by glass [EST light share] (km):")
    glasses = (("eye", 1, 1.0), ("7x50", 7, 0.6), ("10x45", 10, 0.29), ("20x120", 20, 0.5))
    for name, _, _, vis, sky, _ in ANCHORS[:3]:
        out = ", ".join(f"{g} {made_out(proposed_law(), destroyer, vis, sky, eye=8, mag=m, glass=t) / 1000:.2f}"
                        for g, m, t in glasses)
        print(f"  {name}: {out}")
