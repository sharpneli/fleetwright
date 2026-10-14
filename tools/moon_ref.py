"""Reference moon positions and phases for the sim's moon test (tests/Fleetwright.Sim.Tests/MoonTests.cs).

    python -I tools/moon_ref.py

An independent, more precise algorithm than the sim's (Fleetwright.Sim.Core/Moon.cs uses the Astronomical Almanac's
low-precision formulas, ~0.3°): Meeus, "Astronomical Algorithms" ch. 47 (the ELP-2000/82 series truncated to its
main terms, ~10" in longitude, 4" in latitude), with nutation in longitude and the obliquity's nutation, apparent
sidereal time, and the phase angle from ch. 48 with the sun from tools/sun_ref.py's NOAA algorithm. Checked against
Meeus's worked example 47.a on every run. Prints the sublunar latitude and longitude (degrees), the distance (km)
and the phase angle (degrees, 0 full) at each date, as C# test rows. Times are UT, which the sim's world time stands
for (no leap seconds, no delta T: the moon moves 0.5" a second, so delta T's minute is ~30", far inside the test).
"""
import math
from datetime import datetime, timezone

DATES = [
    "1850-01-01 00:00",
    "1890-07-01 12:00",
    "1905-05-27 12:00",   # Tsushima, the night's torpedo attacks
    "1916-05-31 23:00",   # Jutland, the night
    "1942-08-08 14:40",   # Savo Island (01:40 local)
    "1942-11-14 12:00",   # Guadalcanal, the second night (23:00 local), a setting half moon
    "1944-10-24 19:00",   # Surigao Strait
    "1950-09-23 00:00",
    "1992-04-12 00:00",   # Meeus's example 47.a
    "2000-01-21 04:44",   # total lunar eclipse, greatest
    "2050-12-21 06:00",
]

# Table 47.A: D, M, M', F, Σl (1e-6°), Σr (1e-3 km)
LR = [
    (0, 0, 1, 0, 6288774, -20905355), (2, 0, -1, 0, 1274027, -3699111), (2, 0, 0, 0, 658314, -2955968),
    (0, 0, 2, 0, 213618, -569925), (0, 1, 0, 0, -185116, 48888), (0, 0, 0, 2, -114332, -3149),
    (2, 0, -2, 0, 58793, 246158), (2, -1, -1, 0, 57066, -152138), (2, 0, 1, 0, 53322, -170733),
    (2, -1, 0, 0, 45758, -204586), (0, 1, -1, 0, -40923, -129620), (1, 0, 0, 0, -34720, 108743),
    (0, 1, 1, 0, -30383, 104755), (2, 0, 0, -2, 15327, 10321), (0, 0, 1, 2, -12528, 0),
    (0, 0, 1, -2, 10980, 79661), (4, 0, -1, 0, 10675, -34782), (0, 0, 3, 0, 10034, -23210),
    (4, 0, -2, 0, 8548, -21636), (2, 1, -1, 0, -7888, 24208), (2, 1, 0, 0, -6766, 30824),
    (1, 0, -1, 0, -5163, -8379), (1, 1, 0, 0, 4987, -16675), (2, -1, 1, 0, 4036, -12831),
    (2, 0, 2, 0, 3994, -10445), (4, 0, 0, 0, 3861, -11650), (2, 0, -3, 0, 3665, 14403),
    (0, 1, -2, 0, -2689, -7003), (2, 0, -1, 2, -2602, 0), (2, -1, -2, 0, 2390, 10056),
    (1, 0, 1, 0, -2348, 6322), (2, -2, 0, 0, 2236, -9884), (0, 1, 2, 0, -2120, 5751),
    (0, 2, 0, 0, -2069, 0), (2, -2, -1, 0, 2048, -4950), (2, 0, 1, -2, -1773, 4130),
    (2, 0, 0, 2, -1595, 0), (4, -1, -1, 0, 1215, -3958), (0, 0, 2, 2, -1110, 0),
    (3, 0, -1, 0, -892, 3258), (2, 1, 1, 0, -810, 2616), (4, -1, -2, 0, 759, -1897),
    (0, 2, -1, 0, -713, -2117), (2, 2, -1, 0, -700, 2354), (2, 1, -2, 0, 691, 0),
    (2, -1, 0, -2, 596, 0), (4, 0, 1, 0, 549, -1423), (0, 0, 4, 0, 537, -1117),
    (4, -1, 0, 0, 520, -1571), (1, 0, -2, 0, -487, -1739), (2, 1, 0, -2, -399, 0),
    (0, 0, 2, -2, -381, -4421), (1, 1, 1, 0, 351, 0), (3, 0, -2, 0, -340, 0),
    (4, 0, -3, 0, 330, 0), (2, -1, 2, 0, 327, 0), (0, 2, 1, 0, -323, 1165),
    (1, 1, -1, 0, 299, 0), (2, 0, 3, 0, 294, 0), (2, 0, -1, -2, 0, 8752),
]

# Table 47.B: D, M, M', F, Σb (1e-6°)
B = [
    (0, 0, 0, 1, 5128122), (0, 0, 1, 1, 280602), (0, 0, 1, -1, 277693), (2, 0, 0, -1, 173237),
    (2, 0, -1, 1, 55413), (2, 0, -1, -1, 46271), (2, 0, 0, 1, 32573), (0, 0, 2, 1, 17198),
    (2, 0, 1, -1, 9266), (0, 0, 2, -1, 8822), (2, -1, 0, -1, 8216), (2, 0, -2, -1, 4324),
    (2, 0, 1, 1, 4200), (2, 1, 0, -1, -3359), (2, -1, -1, 1, 2463), (2, -1, 0, 1, 2211),
    (2, -1, -1, -1, 2065), (0, 1, -1, -1, -1870), (4, 0, -1, -1, 1828), (0, 1, 0, 1, -1794),
    (0, 0, 0, 3, -1749), (0, 1, -1, 1, -1565), (1, 0, 0, 1, -1491), (0, 1, 1, 1, -1475),
    (0, 1, 1, -1, -1410), (0, 1, 0, -1, -1344), (1, 0, 0, -1, -1335), (0, 0, 3, 1, 1107),
    (4, 0, 0, -1, 1021), (4, 0, -1, 1, 833), (0, 0, 1, -3, 777), (4, 0, -2, 1, 671),
    (2, 0, 0, -3, 607), (2, 0, 2, -1, 596), (2, -1, 1, -1, 491), (2, 0, -2, 1, -451),
    (0, 0, 3, -1, 439), (2, 0, 2, 1, 422), (2, 0, -3, -1, 421), (2, 1, -1, 1, -366),
    (2, 1, 0, 1, -351), (4, 0, 0, 1, 331), (2, -1, 1, 1, 315), (2, -2, 0, -1, 302),
    (0, 0, 1, 3, -283), (2, 1, 1, -1, -229), (1, 1, 0, -1, 223), (1, 1, 0, 1, 223),
    (0, 1, -2, -1, -220), (2, 1, -1, -1, -220), (1, 0, 1, 1, -185), (2, -1, -2, -1, 181),
    (0, 1, 2, 1, -177), (4, 0, -2, -1, 176), (4, -1, -1, -1, 166), (1, 0, 1, -1, -164),
    (4, 0, 1, -1, 132), (1, 0, -1, -1, -119), (4, -1, 0, -1, 115), (2, -2, 0, 1, 107),
]

r = math.radians


def julian(s):
    t = datetime.strptime(s, "%Y-%m-%d %H:%M").replace(tzinfo=timezone.utc)
    j2000 = datetime(2000, 1, 1, 12, tzinfo=timezone.utc)
    return 2451545.0 + (t - j2000).total_seconds() / 86400


def moon_ecliptic(jd):
    """Geocentric ecliptic longitude and latitude (of date, without nutation), degrees, and distance, km; and the
    terms for the example's check."""
    T = (jd - 2451545) / 36525
    Lp = 218.3164477 + 481267.88123421 * T - 0.0015786 * T**2 + T**3 / 538841 - T**4 / 65194000
    D = 297.8501921 + 445267.1114034 * T - 0.0018819 * T**2 + T**3 / 545868 - T**4 / 113065000
    M = 357.5291092 + 35999.0502909 * T - 0.0001536 * T**2 + T**3 / 24490000
    Mp = 134.9633964 + 477198.8675055 * T + 0.0087414 * T**2 + T**3 / 69699 - T**4 / 14712000
    F = 93.2720950 + 483202.0175233 * T - 0.0036539 * T**2 - T**3 / 3526000 + T**4 / 863310000
    A1, A2, A3 = 119.75 + 131.849 * T, 53.09 + 479264.290 * T, 313.45 + 481266.484 * T
    E = 1 - 0.002516 * T - 0.0000074 * T**2
    sl = sr = sb = 0.0
    for d, m, mp, f, cl, cr in LR:
        e = E ** abs(m)
        a = r(d * D + m * M + mp * Mp + f * F)
        sl += cl * e * math.sin(a)
        sr += cr * e * math.cos(a)
    for d, m, mp, f, cb in B:
        sb += cb * E ** abs(m) * math.sin(r(d * D + m * M + mp * Mp + f * F))
    sl += 3958 * math.sin(r(A1)) + 1962 * math.sin(r(Lp - F)) + 318 * math.sin(r(A2))
    sb += (-2235 * math.sin(r(Lp)) + 382 * math.sin(r(A3)) + 175 * math.sin(r(A1 - F)) + 175 * math.sin(r(A1 + F))
           + 127 * math.sin(r(Lp - Mp)) - 115 * math.sin(r(Lp + Mp)))
    lam = (Lp + sl / 1e6) % 360
    return lam, sb / 1e6, 385000.56 + sr / 1000, (Lp % 360, D % 360, M % 360, Mp % 360, F % 360, sl, sb, sr)


def sun_longitude(jd):
    """The sun's apparent longitude and distance (AU), tools/sun_ref.py's NOAA algorithm."""
    jc = (jd - 2451545) / 36525
    l0 = (280.46646 + jc * (36000.76983 + jc * 0.0003032)) % 360
    m = 357.52911 + jc * (35999.05029 - 0.0001537 * jc)
    e = 0.016708634 - jc * (0.000042037 + 0.0000001267 * jc)
    c = (math.sin(r(m)) * (1.914602 - jc * (0.004817 + 0.000014 * jc))
         + math.sin(r(2 * m)) * (0.019993 - 0.000101 * jc) + math.sin(r(3 * m)) * 0.000289)
    omega = 125.04 - 1934.136 * jc
    v = m + c
    rad = 1.000001018 * (1 - e * e) / (1 + e * math.cos(r(v)))
    return l0 + c - 0.00569 - 0.00478 * math.sin(r(omega)), rad


def moon(jd):
    """Sublunar latitude and longitude (degrees), distance (km), phase angle (degrees, 0 full)."""
    lam, beta, dist, _ = moon_ecliptic(jd)
    jc = (jd - 2451545) / 36525
    omega = 125.04452 - 1934.136261 * jc
    Ls, Lm = 280.4665 + 36000.7698 * jc, 218.3165 + 481267.8813 * jc
    dpsi = (-17.20 * math.sin(r(omega)) - 1.32 * math.sin(r(2 * Ls)) - 0.23 * math.sin(r(2 * Lm))
            + 0.21 * math.sin(r(2 * omega))) / 3600
    deps = (9.20 * math.cos(r(omega)) + 0.57 * math.cos(r(2 * Ls)) + 0.10 * math.cos(r(2 * Lm))
            - 0.09 * math.cos(r(2 * omega))) / 3600
    eps0 = 23 + (26 + (21.448 - jc * (46.815 + jc * (0.00059 - jc * 0.001813))) / 60) / 60
    eps = eps0 + deps
    lam_app = lam + dpsi
    ra = math.degrees(math.atan2(math.sin(r(lam_app)) * math.cos(r(eps)) - math.tan(r(beta)) * math.sin(r(eps)),
                                 math.cos(r(lam_app))))
    dec = math.degrees(math.asin(math.sin(r(beta)) * math.cos(r(eps))
                                 + math.cos(r(beta)) * math.sin(r(eps)) * math.sin(r(lam_app))))
    gmst = 280.46061837 + 360.98564736629 * (jd - 2451545) + 0.000387933 * jc * jc - jc ** 3 / 38710000
    gast = gmst + dpsi * math.cos(r(eps))
    lon = (ra - gast) % 360
    if lon > 180:
        lon -= 360
    # phase angle (ch. 48): the elongation psi from the sun, then the angle at the moon
    ls, rs = sun_longitude(jd)
    psi = math.acos(math.cos(r(beta)) * math.cos(r(lam_app - ls)))
    i = math.degrees(math.atan2(rs * 149597870.7 * math.sin(psi), dist - rs * 149597870.7 * math.cos(psi)))
    return dec, lon, dist, i


def check_example():
    """Meeus's example 47.a, 1992 April 12 0h TD: every intermediate the book prints."""
    lam, beta, dist, (Lp, D, M, Mp, F, sl, sb, sr) = moon_ecliptic(2448724.5)
    for name, got, want, tol in [("L'", Lp, 134.290182, 1e-5), ("D", D, 113.842304, 1e-5), ("M", M, 97.643514, 1e-5),
                                 ("M'", Mp, 5.150833, 1e-5), ("F", F, 219.889721, 1e-5), ("Σl", sl, -1127527, 2),
                                 ("Σb", sb, -3229126, 2), ("Σr", sr, -16590875, 2), ("λ", lam, 133.162655, 1e-5),
                                 ("β", beta, -3.229126, 1e-5), ("Δ", dist, 368409.7, 0.1)]:
        assert abs(got - want) <= tol, f"example 47.a: {name} = {got}, the book has {want}"


check_example()
for s in DATES:
    lat, lon, dist, i = moon(julian(s))
    y, mo, d = (int(v) for v in s[:10].split("-"))
    h, mi = (int(v) for v in s[11:].split(":"))
    print(f"        {{ {y}, {mo}, {d}, {h}, {mi}, {lat:.4f}, {lon:.4f}, {dist:.0f}, {i:.3f} }},")
