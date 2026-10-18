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
     lookout's 7x binoculars) is just made out at V. The dark cuts the reach to the night's (~5 km, starlight).
     (1,000 m^2 until fire-control.md F4d: on the ships' real tops and their directors' optics, Lion first sighted
     Derfflinger at a median 30 km in Jutland's 25 km against the record's ~24; at 3,000 m^2, 25 km.)
  4. The eye needs a threshold contrast C_t = C_inf (1 + sqrt(omega_c / omega)): small things need more. The margin
     m = C / C_t: visible at m >= 1.
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
NIGHT_M, NIGHT_SUN = 5000.0, -12.0


def horizon(h):
    return math.sqrt(2 * K * R * max(h, 0.0))


def hidden(h, d):
    beyond = d - horizon(h)
    return 0.0 if beyond <= 0 else beyond * beyond / (2 * K * R)


def threshold(omega):
    return C_INF * (1 + math.sqrt(OMEGA_C / max(omega, 1e-30)))


def extinction(reach):
    if math.isinf(reach):
        return 0.0
    omega = A_REF * M_LOOKOUT ** 2 / reach ** 2
    return max(math.log(C0_DAY / threshold(omega)), 0.05) / reach


def light_reach(sun_deg):
    if sun_deg >= 0:
        return math.inf
    f = min(max(sun_deg / NIGHT_SUN, 0.0), 1.0)
    return NIGHT_M * 100 ** (1 - f)


def margin_of_area(area, d, mag, vis, sun_deg, silhouetted):
    reach = vis if silhouetted else min(vis, light_reach(sun_deg))
    c0 = C0_SIL if silhouetted else C0_DAY
    contrast = c0 * math.exp(-extinction(reach) * d)
    return contrast / threshold(area * mag * mag / (d * d))


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
    # eye m, distance m, magnification, visibility m, sun deg, silhouetted
    (24, 20_000, 7, 25_000, 30, False),
    (24, 30_000, 7, 25_000, 30, False),
    (20, 30_000, 20, 40_000, 30, False),
    (15, 34_000, 7, math.inf, 30, False),
    (24, 4_000, 7, 25_000, -15, False),
    (24, 25_000, 7, 25_000, -4, True),
]

if __name__ == "__main__":
    print("the C# test's table: eye, distance, magnification, visibility, sun, silhouetted -> hidden, area, margin")
    for (h, d, mag, vis, sun, sil) in CASES:
        z = hidden(h, d)
        a = side_area_above(z)
        m = margin_of_area(a, d, mag, vis, sun, sil)
        print(f"  ({h}, {d}, {mag}, {vis}, {sun}, {sil}) -> {z:.12g}, {a:.12g}, {m:.12g}")

    print()
    print("calibration (the schematic battleship side on, a lookout at 24 m with 7x): made out at / found within a minute")
    for vis in (10_000, 25_000, 40_000, math.inf):
        seen = minute = 0
        for d in range(80_000, 100, -100):
            m = margin_of_area(side_area_above(hidden(24, d)), d, 7, vis, 30, False)
            if not seen and m >= 1:
                seen = d
            if not minute and rate(m) >= 1 / 60:
                minute = d
        print(f"  visibility {vis / 1000 if vis != math.inf else 'inf':>5} km: {seen / 1000:5.1f} km / {minute / 1000:5.1f} km")
    print("the record (fire-control-research.md section 2): Jutland's battlecruisers ~24-26 km (visibility ~25 km),")
    print("Denmark Strait ~35 km (clear Arctic air), Savo at night ~5 statute miles")
