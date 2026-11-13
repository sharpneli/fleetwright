"""water_ricochet_ref.py -- reference for 16-water-ricochet.md (Fleetwright damage research).

Shells skipping off the water: what a ricochet kept, where it went, and whether it hit.

  W0 bulk density   a shell's mass over its outside volume (cylinder + tangent ogive), and the
                    sphere law 18/sqrt(SG) and Hutchings' cylinder 18.7/sqrt(SG) at that density   [SRC form]
  W1 the cases      fall angle and striking speed of the guns in the record's cases, by a point-mass
                    flight fitted to each gun's maximum range (fire_control_ref.py's method)          [INFERRED]
  W2 speed kept     the code's exp(-2 theta k) against the measured fits: .50 cal off water
                    (Baillargeon & Bergeron 2012 via Mirshak & Beech 2018), 20 mm / .50 AP (BRL,
                    AD0479824), L/D 5 bodies (Chen 2020), steel spheres (Soliman 1976)             [SRC fits]
  W3 the skip's flight  where a skipped shell is 50-1,000 m on, and how far it flies, by exit angle,
                    speed kept and yaw drag                                                          [INFERRED]
  W4 hits after a skip  Monte Carlo: a shell skipping s short of a destroyer or a battleship, the
                    code's mirror-and-scatter exit against the proposed exit law                     [EST]
  W5 the graze      axial deceleration of a shell planing on the water, by calibre and speed, and
                    the RN's 1921 nose-fuze minima on water (No. 44 fuze)                            [SRC / INFERRED]
  W6 yaw            drag of a yawed shell, and what yaw does at a plate                              [SRC form / EST]

Pure Python, no dependencies. `python water_ricochet_ref.py` prints every table quoted in the note.
Units: SI inside; yards and feet only where a source gives them.
Tags: [SRC] sourced, [INFERRED] reasoning or a fit, [EST] judgement, [UNCERTAIN] thin sources.
"""
from dataclasses import dataclass
from math import sqrt, exp, log, pi, radians, degrees, sin, cos, atan2, hypot, asin, tan
import random

G = 9.80665
YD = 0.9144
RHO_W = 1025.0          # sea water, kg/m3

# ------------------------------------------------------------------------------------------------
# W0. Bulk density: what a shell weighs against the water it displaces
# ------------------------------------------------------------------------------------------------


def ogive_volume_cal(crh, n=400):
    """Volume of a tangent ogive of crh calibres, in calibre^3 (D = 1). Its length is sqrt(crh - 0.25)."""
    R = crh
    L = sqrt(R - 0.25)
    v = 0.0
    for i in range(n):
        x = (i + 0.5) / n * L                      # from the base of the ogive forward
        r = sqrt(R * R - x * x) - (R - 0.5)        # radius at x
        v += pi * r * r * (L / n)
    return v, L


@dataclass
class ShellForm:
    name: str
    d: float        # m
    m: float        # kg
    length_cal: float   # overall length, calibres [S where known, else EST]
    crh: float      # ogive radius, calibres (the head's outline, cap and windscreen included)

    def bulk_sg(self):
        vo, lo = ogive_volume_cal(self.crh)
        cyl = max(self.length_cal - lo, 0.5)
        vol = (pi / 4 * cyl + vo) * self.d ** 3
        return self.m / vol / RHO_W


SHELLS = [
    # name, bore, mass, length (cal), crh. Lengths are typical of the type [EST from drawings: 3.5-4.5 cal]
    ShellForm("4in 31 lb HE (RN WWI)", 0.1016, 14.1, 3.6, 2.0),
    ShellForm("6in 100 lb CPC (RN)", 0.1524, 45.4, 3.8, 2.0),
    ShellForm("12in 850 lb AP 2crh (RN 1900)", 0.305, 386, 3.4, 2.0),
    ShellForm("12in 850 lb APC 4crh (RN 1910)", 0.305, 386, 4.0, 4.0),
    ShellForm("13.5in 1,400 lb APC (RN 1912)", 0.343, 635, 4.0, 4.0),
    ShellForm("15in 1,920 lb Greenboy (RN 1918)", 0.381, 871, 4.2, 6.0),
    ShellForm("30.5cm 405 kg APC (KM 1910)", 0.305, 405, 4.0, 4.0),
    ShellForm("16in 2,700 lb Mk 8 (USN 1939)", 0.406, 1225, 4.6, 7.4),
    ShellForm("Mk 82 bomb-like body (check: SG ~ 2)", 0.273, 241, 6.0, 3.0),
]


def sphere_limit(sg):
    """Johnson & Reid's 18/sqrt(SG) degrees for spheres [SRC]."""
    return 18.0 / sqrt(sg)


def cylinder_limit(sg):
    """Hutchings 1976 eq. 25 without spin: 18.7/sqrt(SG) degrees [SRC]."""
    return 18.7 / sqrt(sg)


def table_w0():
    print("W0  Bulk density and the sphere-law limits")
    print(f"{'shell':40s} {'SG':>5s} {'18/sqrtSG':>10s} {'18.7/sqrtSG':>12s}")
    for s in SHELLS:
        sg = s.bulk_sg()
        print(f"{s.name:40s} {sg:5.2f} {sphere_limit(sg):9.1f}° {cylinder_limit(sg):11.1f}°")
    print(f"{'(the code: steel 7.9)':40s} {7.9:5.2f} {sphere_limit(7.9):9.1f}°")
    print()


# ------------------------------------------------------------------------------------------------
# W1. Point-mass flight (fire_control_ref.py's drag curve and fit), for falls and striking speeds
# ------------------------------------------------------------------------------------------------
_CD_M = [0.0, 0.70, 0.80, 0.90, 0.95, 1.00, 1.05, 1.10, 1.20, 1.50, 2.00, 2.50, 3.00, 4.00]
_CD_V = [0.120, 0.120, 0.125, 0.160, 0.220, 0.330, 0.380, 0.385, 0.375, 0.340, 0.295, 0.265, 0.245, 0.225]


def _interp(x, xs, ys):
    if x <= xs[0]:
        return ys[0]
    for i in range(1, len(xs)):
        if x <= xs[i]:
            f = (x - xs[i - 1]) / (xs[i] - xs[i - 1])
            return ys[i - 1] + f * (ys[i] - ys[i - 1])
    return ys[-1]


def _atm(z):
    z = max(z, 0.0)
    return 1.225 * exp(-z / 9000.0), 340.3 - 0.0041 * min(z, 11000.0)


def fly(d, m, v0, elev_rad, i, z0=0.0, drag_mult=1.0, dt=0.02, stop_x=None, want=None):
    """Integrate from height z0 to the sea (z = 0). Returns (range, tof, fall rad, speed).
    want: list of x at which to record height (returns dict x -> z)."""
    A = pi * d * d / 4
    vx, vz = v0 * cos(elev_rad), v0 * sin(elev_rad)
    x, z, t = 0.0, z0, 0.0
    k0 = 0.5 * A * i * drag_mult / m
    rec = {}
    wl = sorted(want) if want else []

    def acc(vx, vz, z):
        v = hypot(vx, vz)
        rho, a = _atm(z)
        k = k0 * rho * _interp(v / a, _CD_M, _CD_V) * v
        return -k * vx, -k * vz - G

    while True:
        ax1, az1 = acc(vx, vz, z)
        vx2, vz2 = vx + ax1 * dt, vz + az1 * dt
        ax2, az2 = acc(vx2, vz2, z + vz * dt)
        nx = x + 0.5 * (vx + vx2) * dt
        nz = z + 0.5 * (vz + vz2) * dt
        nvx = vx + 0.5 * (ax1 + ax2) * dt
        nvz = vz + 0.5 * (az1 + az2) * dt
        while wl and nx >= wl[0]:
            f = (wl[0] - x) / (nx - x)
            rec[wl[0]] = z + f * (nz - z)
            wl.pop(0)
        if nz < 0 and t > 0:
            f = z / (z - nz)
            xr = x + f * (nx - x)
            vxr = vx + f * (nvx - vx)
            vzr = vz + f * (nvz - vz)
            if want is not None:
                for w in wl:
                    rec[w] = None   # down before it got there
                return xr, t + f * dt, atan2(-vzr, vxr), hypot(vxr, vzr), rec
            return xr, t + f * dt, atan2(-vzr, vxr), hypot(vxr, vzr)
        x, z, vx, vz, t = nx, nz, nvx, nvz, t + dt


@dataclass
class Gun:
    name: str
    d: float
    m: float
    v0: float
    max_elev: float
    max_range: float
    i: float = 1.0

    def fit(self):
        lo, hi = 0.3, 3.0
        e = radians(self.max_elev)
        for _ in range(26):
            mid = 0.5 * (lo + hi)
            if fly(self.d, self.m, self.v0, e, mid, dt=0.05)[0] > self.max_range:
                lo = mid
            else:
                hi = mid
        self.i = 0.5 * (lo + hi)
        return self

    def at_range(self, R):
        """Elevation, fall (deg) and striking speed at range R (m), by bisection on elevation."""
        lo, hi = 0.0, radians(self.max_elev)
        for _ in range(40):
            mid = 0.5 * (lo + hi)
            if fly(self.d, self.m, self.v0, mid, self.i, dt=0.02)[0] < R:
                lo = mid
            else:
                hi = mid
        r, t, fall, v = fly(self.d, self.m, self.v0, lo, self.i, dt=0.02)
        return degrees(lo), degrees(fall), v, t


# The guns of the cases. [S navweaps] unless tagged.
GUNS = {
    "12in/35 Mk VIII (Canopus)": Gun("12in/35 Mk VIII", 0.305, 386, 724, 13.5, 13900 * YD),
    "12in/40 (Mikasa)": Gun("12in/40", 0.305, 386, 732, 15, 13700),            # as fire_control_ref [UNCERTAIN range]
    "12in/45 Mk X": Gun("12in/45 Mk X", 0.305, 386, 831, 13.5, 16450),
    "13.5in/45 Mk V(H)": Gun("13.5in/45", 0.343, 635, 757, 20, 21700),
    "8in/45 (IJN Armstrong, 1905)": Gun("8in/45", 0.203, 113.4, 760, 30, 18000),   # [EST: 760 m/s, 18 km at 30°]
    "6in/47 Mk 16 (USN 1944)": Gun("6in/47", 0.152, 59, 812, 47.5, 23880),
    "5in/50 (USN 1911)": Gun("5in/50", 0.127, 22.7, 914, 15, 12500),             # [EST: 3,000 fps per SA 1911; 50 lb; range EST]
    "4in QF Mk IV (WWI)": Gun("4in QF Mk IV", 0.1016, 14.1, 722, 20, 9600 * YD),
    "4in BL Mk IX": Gun("4in BL Mk IX", 0.1016, 14.1, 800, 30, 13500 * YD),
    "4.7in BL Mk I": Gun("4.7in BL Mk I", 0.120, 22.68, 814, 30, 15800 * YD),
}
for _g in GUNS.values():
    _g.fit()


def table_w1():
    print("W1  Falls and striking speeds at the cases' ranges (point mass, fitted to max range)")
    cases = [
        ("12in/35 Mk VIII (Canopus)", 11000 * YD, "Falklands 1914, Sturdee's 11,000 yd"),
        ("12in/35 Mk VIII (Canopus)", 12000 * YD, "  ... 12,000 yd as often quoted"),
        ("12in/40 (Mikasa)", 6000, "Yellow Sea 1904, a short ricochet (range not known)"),
        ("12in/40 (Mikasa)", 8000, ""),
        ("8in/45 (IJN Armstrong, 1905)", 5000, "Tsushima, Orel's conning-tower ricochet (range not known)"),
        ("8in/45 (IJN Armstrong, 1905)", 7000, ""),
        ("5in/50 (USN 1911)", 2500 * YD, "SA 1911: 'almost point blank' at 2,500 yd"),
        ("6in/47 Mk 16 (USN 1944)", 4000, "Albert W. Grant 1944 (range EST 3-6 km)"),
        ("4in QF Mk IV (WWI)", 2100 * YD, "RN 1921: No. 44 fuze 'just under 2°' at 2,100 yd"),
        ("4in BL Mk IX", 2400 * YD, "RN 1921: 1°49' at 2,400 yd (4in Mk V and Mk IX)"),
        ("4.7in BL Mk I", 4100 * YD, "RN 1921: 5°20' at 4,100 yd"),
        ("12in/45 Mk X", 4000, "the sim's duel: dreadnought on destroyer, 4 km"),
        ("13.5in/45 Mk V(H)", 4000, "  super-dreadnought, 4 km"),
        ("13.5in/45 Mk V(H)", 8000, "  8 km"),
    ]
    print(f"{'gun':30s} {'range':>8s} {'elev':>6s} {'fall':>6s} {'v_s':>6s}  note")
    for g, R, note in cases:
        gun = GUNS[g]
        el, fall, v, t = gun.at_range(R)
        print(f"{g:30s} {R:7.0f}m {el:5.2f}° {fall:5.2f}° {v:5.0f}  {note}")
    print()


# ------------------------------------------------------------------------------------------------
# W2. Speed kept by a skip
# ------------------------------------------------------------------------------------------------


def kept_code(theta_deg, k=1.0):
    """The code: exp(-2 theta k) (ShellSystem.Skip, WaterRicochetLossK)."""
    return exp(-2 * radians(theta_deg) * k)


def kept_50cal(theta_deg, gamma=7.37, kk=2.258):
    """.50 Ball M2 off water: speed lost = Weibull CDF, scale 7.37°, shape 2.258 [SRC Mirshak & Beech 2018 eq. 3,
    fitted to Baillargeon & Bergeron 2012]. Kept = exp(-(theta/gamma)^k)."""
    return exp(-(theta_deg / gamma) ** kk)


def kept_chen(theta_deg):
    """L/D 5 cone/ogive/flat bodies at 95-172 m/s: Vo/Vi = 0.802 - 0.0164 theta [SRC Chen et al. 2020 eq. 3]."""
    return 0.802 - 0.0164 * theta_deg


def table_w2():
    print("W2  Speed kept by one skip, by the fall it met the water at")
    print(f"{'theta':>6s} {'code k=1':>9s} {'code k=2':>9s} {'.50 M2':>8s} {'Chen L/D5':>10s}")
    for th in (1, 2, 3, 4, 5, 6, 7, 8, 10, 12):
        print(f"{th:5d}° {kept_code(th):9.2f} {kept_code(th, 2):9.2f} {kept_50cal(th):8.2f} {kept_chen(th):10.2f}")
    print("  points: .50 AP M2 / 20 mm at 5° depression (BRL AD0479824): 1,809 fps after against 2,515 in air = "
          f"{1809 / 2515:.2f}; steel spheres (Soliman 1976): 0.81; brass spheres (Ramsauer) exit 0.9 x entry angle")
    print()


# ------------------------------------------------------------------------------------------------
# W3. The skip's flight: where it goes
# ------------------------------------------------------------------------------------------------


def table_w3():
    print("W3  A 12in/45 shell skipping 4 km out (fall and speed from W1): its height 50-1,000 m on and its next graze")
    gun = GUNS["12in/45 Mk X"]
    el, fall, v, t = gun.at_range(4000)
    print(f"    met the water at {fall:.2f}°, {v:.0f} m/s")
    xs = [50, 100, 200, 300, 500, 1000]
    print(f"{'exit':>5s} {'kept':>5s} {'yaw drag':>8s} " + " ".join(f"{x:>6d}m" for x in xs) + "   next graze")
    for exit_deg in (1, 2, 3, 5, 8, 12, 20):
        for kept, dm in ((kept_50cal(fall), 1.0), (kept_50cal(fall), 3.0)):
            r, tt, f2, v2, rec = fly(gun.d, gun.m, v * kept, radians(exit_deg), gun.i, drag_mult=dm, dt=0.01, want=xs)
            hs = " ".join(f"{rec[x]:6.1f}" if rec.get(x) is not None else "   ---" for x in xs)
            print(f"{exit_deg:4d}° {kept:5.2f} {dm:7.1f}x {hs}   {r:6.0f} m at {degrees(f2):.1f}°, {v2:.0f} m/s")
    print("  (yaw drag 3x is a shell leaving at ~20° of yaw, W6; it falls back to 1x as the yaw damps)")
    print()


# ------------------------------------------------------------------------------------------------
# W4. Hits after a skip
# ------------------------------------------------------------------------------------------------


@dataclass
class Target:
    name: str
    h_hull: float     # freeboard over the waterline, m (hull side)
    h_upper: float    # top of the upper works over most of the length a shell meets, m
    upper_share: float  # share of the beam-on profile's length that has upper works up to h_upper


DESTROYER = Target("destroyer (1914)", 3.5, 8.0, 0.35)
BATTLESHIP = Target("battleship (1914)", 7.5, 20.0, 0.45)


def hit_after_skip(target, s, fall, v, d, m, i, exit_law, rng, n=4000, kept=kept_50cal):
    """Share of shells skipping s metres short of a beam-on target that strike it on the way past.
    exit_law(fall, rng) -> exit angle (deg) and yaw-drag multiplier."""
    hits = 0
    cache = {}
    for _ in range(n):
        ex, dm = exit_law(fall, rng)
        key = (round(ex, 1), dm)
        if key not in cache:
            r, tt, f2, v2, rec = fly(d, m, v * kept(fall), radians(ex), i, drag_mult=dm, dt=0.02, want=[s])
            cache[key] = rec.get(s)
        z = cache[key]
        if z is None or z < 0:
            continue
        if z <= target.h_hull:
            hits += 1
        elif z <= target.h_upper and rng.random() < target.upper_share:
            hits += 1
    return hits / n


def exit_code(fall, rng, scatter=3.0):
    """The code: the mirror, scattered 3° (1 sigma), no flatter than 0.2°, no yaw."""
    return max(fall + scatter * rng.gauss(0, 1), 0.2), 1.0


def exit_mirror(fall, rng):
    return fall, 1.0


def exit_law(base, sig=0.6):
    """Proposed family [EST]: median base + 0.5 x fall, lognormal sigma `sig` (0.6: x0.37-x2.7 for 5-95%),
    capped at 35°; yaw drag 1 + 2 x (exit/20°)^2 (a steep exit is a pitched-up, yawed shell, W6)."""
    def law(fall, rng):
        med = base + 0.5 * fall
        ex = min(med * exp(sig * rng.gauss(0, 1)), 35.0)
        return ex, 1.0 + 2.0 * (ex / 20.0) ** 2
    return law


exit_proposed = exit_law(5.0)      # the central proposal
exit_steep = exit_law(10.0)        # bullet-like: pistol and rifle bullets off water leave at 10-26° from 5°


def table_w4():
    print("W4  Hits after a skip: share of shells skipping s short of a beam-on target that strike it")
    rng = random.Random(1)
    for gname, R, tgt in (("12in/45 Mk X", 4000, DESTROYER), ("13.5in/45 Mk V(H)", 4000, DESTROYER),
                          ("13.5in/45 Mk V(H)", 4000, BATTLESHIP), ("13.5in/45 Mk V(H)", 8000, BATTLESHIP)):
        gun = GUNS[gname]
        el, fall, v, t = gun.at_range(R)
        print(f"  {gname} at {R/1000:.0f} km on a {tgt.name} (fall {fall:.2f}°, {v:.0f} m/s, kept {kept_50cal(fall):.2f})")
        print(f"  {'s short':>8s} {'mirror':>7s} {'code ±3°':>9s} {'5+½fall':>8s} {'10+½fall':>9s}")
        for s in (25, 50, 100, 200, 300, 500, 800):
            a = hit_after_skip(tgt, s, fall, v, gun.d, gun.m, gun.i, exit_mirror, rng, n=50)
            b = hit_after_skip(tgt, s, fall, v, gun.d, gun.m, gun.i, exit_code, rng, n=1000)
            c = hit_after_skip(tgt, s, fall, v, gun.d, gun.m, gun.i, exit_proposed, rng, n=1000)
            e = hit_after_skip(tgt, s, fall, v, gun.d, gun.m, gun.i, exit_steep, rng, n=1000)
            print(f"  {s:7d}m {a:7.2f} {b:9.2f} {c:8.2f} {e:9.2f}")
        print()


# ------------------------------------------------------------------------------------------------
# W5. The graze: what a base fuze feels
# ------------------------------------------------------------------------------------------------


def graze_decel_g(d, m, v, cd_w=0.3, wet=0.5):
    """Axial deceleration (g) of a shell planing on the water with a share `wet` of its section in the water,
    drag coefficient cd_w on the wetted section [EST: a cavity-running ogive is 0.2-0.35 on its whole section].
    Equal to the steep-entry value times `wet`."""
    A = pi * d * d / 4
    return 0.5 * RHO_W * v * v * cd_w * A * wet / m / G


def table_w5():
    print("W5  Axial deceleration on the graze (half the section wetted, Cd 0.3) against a full entry")
    print(f"{'shell':28s} {'v':>5s} {'graze g':>8s} {'entry g':>8s}")
    for name, d, m in (("4in 31 lb", 0.1016, 14.1), ("6in 100 lb", 0.1524, 45.4), ("8in 250 lb", 0.203, 113),
                       ("12in 850 lb", 0.305, 386), ("15in 1,920 lb", 0.381, 871)):
        for v in (300, 500, 700):
            print(f"{name:28s} {v:5d} {graze_decel_g(d, m, v):8.0f} {graze_decel_g(d, m, v, wet=1.0):8.0f}")
    print("  RN 1921 No. 44 nose fuze, minimum fall for reliable functioning on water [SRC ADM 186/251 para 59]:")
    for g, yd, ang in (("4in QF Mk IV (WWI)", 2100, 1.95), ("4in BL Mk IX", 2400, 1 + 49 / 60), ("4.7in BL Mk I", 4100, 5 + 20 / 60)):
        gun = GUNS[g]
        el, fall, v, t = gun.at_range(yd * YD)
        vn = v * sin(radians(ang))
        print(f"    {g:22s} {yd:5d} yd: stated {ang:4.2f}° (this flight's fall {fall:4.2f}°), v_s {v:4.0f} m/s,"
              f" normal {vn:4.0f} m/s, graze g {graze_decel_g(gun.d, gun.m, v):5.0f}")
    print()


# ------------------------------------------------------------------------------------------------
# W6. Yaw
# ------------------------------------------------------------------------------------------------


def yaw_drag(yaw_deg, k=12.0):
    """Cd(yaw)/Cd(0) = 1 + k yaw^2 (rad). k = CD_delta2 / CD0: about 3-4 / 0.3 = 10-13 for a supersonic shell
    [EST from McCoy-type coefficients]."""
    return 1 + k * radians(yaw_deg) ** 2


def table_w6():
    print("W6  Yaw: drag, and the plate (armor/00 §5: +½ yaw to obliquity under 10°)")
    print(f"{'yaw':>5s} {'drag x (k=10)':>14s} {'(k=20)':>7s} {'Ob +':>6s} {'.50 AP limit v x':>18s}")
    for y in (0, 5, 10, 15, 20, 30, 45):
        lv = 1 + 0.006 * min(y, 45)       # NPG 1944: +0.5-0.7 %/deg to 30°, a maximum at 40-50° [SRC ADA955279]
        ob = f"{y/2:5.1f}°" if y <= 10 else "  tumb"
        print(f"{y:4d}° {yaw_drag(y, 10):14.2f} {yaw_drag(y, 20):7.2f} {ob:>6s} {lv:18.2f}")
    print()


if __name__ == "__main__":
    table_w0()
    table_w1()
    table_w2()
    table_w3()
    table_w4()
    table_w5()
    table_w6()
