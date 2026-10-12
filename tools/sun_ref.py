"""Reference sun positions for the sim's sun test (tests/Fleetwright.Sim.Tests/SunTests.cs).

    python -I tools/sun_ref.py

An independent, more precise algorithm than the sim's (Fleetwright.Sim.Core/Sun.cs uses the Astronomical Almanac's
low-precision formulas, ~1'): the NOAA solar calculator's, from Meeus, "Astronomical Algorithms" ch. 12 and 25, with
the equation of centre to three terms, nutation and aberration in the apparent longitude, the corrected obliquity,
and apparent sidereal time. Prints the subsolar latitude (the declination) and longitude at each date, in degrees,
as C# test rows. Times are UT, which the sim's world time stands for (no leap seconds, no delta T).
"""
import math
from datetime import datetime, timezone

DATES = [
    "1850-01-01 00:00",
    "1890-07-01 12:00",
    "1905-05-27 05:00",   # Tsushima, near the first shots (14:08 local time, UTC+9)
    "1916-05-31 15:48",   # Jutland, the battlecruisers open fire (GMT, as the British logs keep it)
    "1916-05-31 18:30",
    "1950-09-23 00:00",
    "2000-06-21 12:00",
    "2050-12-21 06:00",
]


def julian(s):
    t = datetime.strptime(s, "%Y-%m-%d %H:%M").replace(tzinfo=timezone.utc)
    j2000 = datetime(2000, 1, 1, 12, tzinfo=timezone.utc)
    return 2451545.0 + (t - j2000).total_seconds() / 86400


def subsolar(jd):
    r = math.radians
    jc = (jd - 2451545) / 36525
    l0 = (280.46646 + jc * (36000.76983 + jc * 0.0003032)) % 360
    m = 357.52911 + jc * (35999.05029 - 0.0001537 * jc)
    c = (math.sin(r(m)) * (1.914602 - jc * (0.004817 + 0.000014 * jc))
         + math.sin(r(2 * m)) * (0.019993 - 0.000101 * jc) + math.sin(r(3 * m)) * 0.000289)
    omega = 125.04 - 1934.136 * jc
    lam = l0 + c - 0.00569 - 0.00478 * math.sin(r(omega))      # apparent: aberration and nutation in longitude
    eps0 = 23 + (26 + (21.448 - jc * (46.815 + jc * (0.00059 - jc * 0.001813))) / 60) / 60
    eps = eps0 + 0.00256 * math.cos(r(omega))
    ra = math.degrees(math.atan2(math.cos(r(eps)) * math.sin(r(lam)), math.cos(r(lam))))
    dec = math.degrees(math.asin(math.sin(r(eps)) * math.sin(r(lam))))
    gmst = 280.46061837 + 360.98564736629 * (jd - 2451545) + 0.000387933 * jc * jc - jc ** 3 / 38710000
    gast = gmst + (-0.00478 * math.sin(r(omega))) * math.cos(r(eps))   # nutation in right ascension
    lon = (ra - gast) % 360
    if lon > 180:
        lon -= 360
    return dec, lon


for s in DATES:
    dec, lon = subsolar(julian(s))
    y, mo, d = (int(v) for v in s[:10].split("-"))
    h, mi = (int(v) for v in s[11:].split(":"))
    print(f"        [{y}, {mo}, {d}, {h}, {mi}, {dec:.4f}, {lon:.4f}],")
