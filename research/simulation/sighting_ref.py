"""The sighting model's reference (docs/battle/plan.md step 3.2c), written apart from the sim's
Fleetwright.Sim.Combat/SightModel.cs so each checks the other (tests/Fleetwright.Sim.Tests/SightTests.cs holds this
script's numbers), and its calibration against the record.

The model: an eye h over the sea, a target d away.
  1. The horizon hides the target's lower part: d_h = sqrt(2 k R h) (k = 7/6: 3.86 sqrt(h) km); beyond d_h the target is
     hidden to (d - d_h)^2 / (2 k R) (fire-control-research.md section 2: mutual visibility 3.86 (sqrt h1 + sqrt h2) km).
  2. What shows is its silhouette above that, side on and end on by its aspect, seen as a solid angle
     omega = A M^2 / d^2 through the eye's magnification M.
  3. The air fades its contrast: C = C0 exp(-sigma d) (Koschmieder). A visibility V means what a ship's log means: how
     far a ship is made out. sigma is the one at which a reference ship (3,000 m^2 side on, about a capital ship's; a
     lookout's 7x binoculars) is just made out at V by day. C0 is a grey ship's in the sun (0.35), darker against the
     sky in its light alone (0.6: after sunset, at night), between on the log of the horizon sky's luminance L from
     150 to 2,000 cd/m^2, and toward a silhouette's (0.8) by its share against a bright horizon.
     (1,000 m^2 until fire-control.md F4d: on the ships' real tops and their directors' optics, Lion first sighted
     Derfflinger at a median 30 km in Jutland's 25 km against the record's ~24; at 3,000 m^2, 25 km.)
  4. The eye needs a threshold contrast C_t = C_inf (1 + sqrt(omega_c / omega)): small things need more. In the dark
     (docs/battle/night.md Q1) C_inf grows by f_a and sqrt(omega_c) by f_b, each f = (1 + sqrt(L_k / L)) / (1 +
     sqrt(L_k / 30)) (1 from 30 cd/m^2 up), L the sky's luminance through the eye's glass (the glass's share of it),
     knees L_a = 1e-4 and L_b = 0.2 cd/m^2. The margin m = C / C_t: visible at m >= 1.
  5. Detection is a rate: rate = rate_max * min(1, ln m / ln m_full), so a look of dt finds it with 1 - exp(-rate dt)
     whatever dt is.

Run it for the table the C# test holds and the calibration against the record:

    python -I research/simulation/sighting_ref.py
"""
import math

R = 6_371_000.0
K = 7.0 / 6.0
C0_DAY, C0_SIL = 0.35, 0.8
C_INF = 0.05
OMEGA_C = 2e-4
A_REF = 3000.0
M_LOOKOUT = 7.0
RATE, M_FULL = 1.0 / 30, 3.0
C0_NIGHT, L_NIGHT, L_DAY = 0.6, 150.0, 2000.0
L_A, L_B, L_WEBER = 1e-4, 0.2, 30.0


def horizon(h):
    return math.sqrt(2 * K * R * max(h, 0.0))


def hidden(h, d):
    beyond = d - horizon(h)
    return 0.0 if beyond <= 0 else beyond * beyond / (2 * K * R)


def threshold(omega, fa=1.0, fb=1.0):
    return C_INF * fa * (1 + fb * math.sqrt(OMEGA_C / max(omega, 1e-30)))


def knee(lk, lum):
    return 1.0 if lum >= L_WEBER else (1 + math.sqrt(lk / lum)) / (1 + math.sqrt(lk / L_WEBER))


def own_contrast(sky, sil):
    lit = min(max(math.log(sky / L_NIGHT) / math.log(L_DAY / L_NIGHT), 0.0), 1.0)
    own = C0_NIGHT + (C0_DAY - C0_NIGHT) * lit
    return own + (C0_SIL - own) * sil


def extinction(reach):
    if math.isinf(reach):
        return 0.0
    omega = A_REF * M_LOOKOUT ** 2 / reach ** 2
    return max(math.log(C0_DAY / threshold(omega)), 0.05) / reach


def margin_of_area(area, d, mag, vis, sky, sil, glass=1.0):
    contrast = own_contrast(sky, sil) * math.exp(-extinction(vis) * d)
    lum = sky * glass
    return contrast / threshold(area * mag * mag / (d * d), knee(L_A, lum), knee(L_B, lum))


def rate(m, search=1.0):
    return 0.0 if m <= 1 else RATE * search * min(1.0, math.log(m) / math.log(M_FULL))


# a schematic battleship: side on, the area above each metre over the waterline (a hull 180 m x 7 m, upperworks 60 m
# long to 18 m, masts to 25 m); it stands in for the classes' tables, which the sim compiles from the hitboxes
def side_area_above(z):
    hull = 180 * max(7 - z, 0)
    upper = 60 * max(18 - max(z, 7), 0)
    masts = 2 * 1.5 * max(25 - max(z, 18), 0)
    return hull + upper + masts


CASES = [
    # eye m, distance m, magnification, visibility m, sky cd/m^2 (19,099: the sun 30 deg up), silhouette share, glass
    (24, 20_000, 7, 25_000, 19_099, 0, 1),
    (24, 30_000, 7, 25_000, 19_099, 0, 1),
    (20, 30_000, 20, 40_000, 19_099, 0, 1),
    (15, 34_000, 7, math.inf, 19_099, 0, 1),
    (24, 2_000, 7, 25_000, 3.2e-4, 0, 0.6),   # a moonless night, a lookout's 7x50
    (24, 800, 7, 20_000, 4.8e-5, 0, 0.6),     # overcast and moonless
    (24, 25_000, 7, 25_000, 7.0, 1, 1),       # dusk, silhouetted
    (24, 6_000, 7, 25_000, 0.01, 0.6, 0.6),   # a moon's path beyond her
]

if __name__ == "__main__":
    print("the C# test's table: eye, distance, magnification, visibility, sky, silhouette, glass -> hidden, area, margin")
    for (h, d, mag, vis, sky, sil, glass) in CASES:
        z = hidden(h, d)
        a = side_area_above(z)
        m = margin_of_area(a, d, mag, vis, sky, sil, glass)
        print(f"  ({h}, {d}, {mag}, {vis}, {sky}, {sil}, {glass}) -> {z:.12g}, {a:.12g}, {m:.12g}")

    print()
    print("calibration (the schematic battleship side on, a lookout at 24 m with 7x): made out at / found within a minute")
    for vis in (10_000, 25_000, 40_000, math.inf):
        seen = minute = 0
        for d in range(80_000, 100, -100):
            m = margin_of_area(side_area_above(hidden(24, d)), d, 7, vis, 19_099, 0)
            if not seen and m >= 1:
                seen = d
            if not minute and rate(m) >= 1 / 60:
                minute = d
        print(f"  visibility {vis / 1000 if vis != math.inf else 'inf':>5} km: {seen / 1000:5.1f} km / {minute / 1000:5.1f} km")
    print("the record (fire-control-research.md section 2): Jutland's battlecruisers ~24-26 km (visibility ~25 km),")
    print("Denmark Strait ~35 km (clear Arctic air), Savo at night ~5 statute miles")
