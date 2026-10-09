"""
fire_control_ref.py — reference implementation of the surface fire-control chain
(Naval project, fire-control-research.md).

    detect -> range & bearing -> compute (track + predict) -> gun orders -> laying
           -> dispersion -> hit test against the real hull -> spot -> correct

Run:  python3 fire_control_ref.py            (all tables, ~2-4 min)
      python3 fire_control_ref.py quick      (skip the Monte-Carlo tables)

Units inside the code are SI (m, s, rad, kg). Tables print naval units (yd, kyd, kn, mil)
because the sources do. 1 mil = 1/1000 rad here (USN "mil" ~ 1/1000 rad; the 6400-mil
convention differs by 2 %, irrelevant at this precision).

Tags as in the note: [S] sourced (see note), [INFERRED] my fit or tuning value,
[UNCERTAIN] thin sources. Everything the note's tables quote is produced by this file.
Fixed seeds; numpy only.
"""

import math
import sys
from dataclasses import dataclass, field, replace

import numpy as np

YD = 0.9144
KN = 0.514444
MIL = 1e-3
ARCSEC = math.pi / 648000.0
G = 9.80665


def kyd(m):
    return m / (1000 * YD)


# =============================================================================
# 1. Ballistics: point mass, standard drag curve, form factor fitted to max range
# =============================================================================
# Cd(Mach) for a pointed shell, shape of the 1943 / KD8 family [INFERRED from
# general exterior-ballistics references]; scaled per gun by a form factor i.
_CD_M = [0.0, 0.70, 0.80, 0.90, 0.95, 1.00, 1.05, 1.10, 1.20, 1.50, 2.00, 2.50, 3.00, 4.00]
_CD_V = [0.120, 0.120, 0.125, 0.160, 0.220, 0.330, 0.380, 0.385, 0.375, 0.340, 0.295, 0.265, 0.245, 0.225]


def _cd(mach):
    return float(np.interp(mach, _CD_M, _CD_V))


def _atm(z):
    """Density (kg/m3) and speed of sound (m/s), troposphere/stratosphere fit."""
    z = max(z, 0.0)
    rho = 1.225 * math.exp(-z / 9000.0)
    a = 340.3 - 0.0041 * min(z, 11000.0)
    return rho, a


def _fly(d, m, v0, elev, i, dt=0.05, want_path=False):
    """Integrate to impact (z=0). Returns range, tof, fall angle (rad), impact speed."""
    A = math.pi * d * d / 4
    vx, vz = v0 * math.cos(elev), v0 * math.sin(elev)
    x = z = t = 0.0
    k0 = 0.5 * A * i / m

    def acc(vx, vz, z):
        v = math.hypot(vx, vz)
        rho, a = _atm(z)
        k = k0 * rho * _cd(v / a) * v
        return -k * vx, -k * vz - G

    while True:
        ax1, az1 = acc(vx, vz, z)
        vx2, vz2 = vx + ax1 * dt, vz + az1 * dt
        ax2, az2 = acc(vx2, vz2, z + vz * dt)
        nx = x + 0.5 * (vx + vx2) * dt
        nz = z + 0.5 * (vz + vz2) * dt
        nvx = vx + 0.5 * (ax1 + ax2) * dt
        nvz = vz + 0.5 * (az1 + az2) * dt
        if nz < 0 and t > 0:
            f = z / (z - nz)
            x = x + f * (nx - x)
            t = t + f * dt
            vx = vx + f * (nvx - vx)
            vz = vz + f * (nvz - vz)
            return x, t, math.atan2(-vz, vx), math.hypot(vx, vz)
        x, z, vx, vz, t = nx, nz, nvx, nvz, t + dt


@dataclass
class Gun:
    name: str
    d: float            # bore, m
    m: float            # shell mass, kg
    v0: float           # m/s
    max_elev: float     # deg
    max_range: float    # m at max_elev  [S navweaps]
    pattern_pct: float  # full-salvo 100 % range pattern, % of range  [S/INFERRED, note §5.1]
    defl_ratio: float = 0.4   # deflection sigma / range sigma  [S: "about half"; 4 mil normal]
    train_rate: float = 4.0   # deg/s mount training  [S where known]
    i: float = 1.0      # fitted form factor
    table: dict = field(default_factory=dict, repr=False)

    def fit(self):
        lo, hi = 0.3, 3.0
        e = math.radians(self.max_elev)
        for _ in range(28):
            mid = 0.5 * (lo + hi)
            r = _fly(self.d, self.m, self.v0, e, mid, dt=0.1)[0]
            if r > self.max_range:
                lo = mid
            else:
                hi = mid
        self.i = 0.5 * (lo + hi)
        els = np.radians(np.arange(0.25, self.max_elev + 0.01, 0.25))
        rows = [_fly(self.d, self.m, self.v0, e, self.i, dt=0.1) for e in els]
        R = np.array([r[0] for r in rows])
        keep = np.concatenate([[True], np.diff(R) > 0])  # monotone part only
        self.table = dict(el=els[keep], R=R[keep], tof=np.array([r[1] for r in rows])[keep],
                          fall=np.array([r[2] for r in rows])[keep], vs=np.array([r[3] for r in rows])[keep])
        return self

    def elev(self, R):
        return float(np.interp(R, self.table["R"], self.table["el"]))

    def tof(self, R):
        return float(np.interp(R, self.table["R"], self.table["tof"]))

    def fall(self, R):
        return float(np.interp(R, self.table["R"], self.table["fall"]))

    def dR_del(self, R):
        """metres of range per radian of elevation at range R."""
        el = self.elev(R)
        h = math.radians(0.25)
        R1 = float(np.interp(el + h, self.table["el"], self.table["R"]))
        R0 = float(np.interp(max(el - h, self.table["el"][0]), self.table["el"], self.table["R"]))
        return (R1 - R0) / (el + h - max(el - h, self.table["el"][0]))

    def sigma_D(self, R, n):
        """Range and deflection 1-sigma shell dispersion (m) for an n-gun salvo.
        pattern = ratio(n) * D (USNI 1917 table), D = 0.798 sigma (normal)."""
        ratio = float(np.interp(n, [1, 2, 3, 4, 6, 8, 9, 10, 12], [1.6, 1.9, 2.43, 2.74, 3.47, 3.85, 4.0, 4.13, 4.34]))
        sr = (self.pattern_pct / 100.0) * R / ratio / 0.798 * 0.85  # 0.85: quoted patterns are full salvos with gun-to-gun scatter
        return sr, self.defl_ratio * sr


# name, bore, shell kg, m/s, max elev, max range m, full-salvo pattern % of range, train deg/s
GUNS = {g.name: g for g in [
    Gun("12in/40 (Mikasa)", 0.305, 386, 732, 15, 13700, 3.5, train_rate=2.0),            # range [UNCERTAIN]
    Gun("12in/45 Mk X", 0.305, 386, 831, 13.5, 16450, 3.3, train_rate=2.0),
    Gun("30.5cm SK L/50", 0.305, 405, 855, 13.5, 18100, 2.5, train_rate=3.0),
    Gun("13.5in/45 Mk V(H)", 0.343, 635, 757, 20, 21700, 2.5, train_rate=2.0),
    Gun("15in/42 Mk I", 0.381, 871, 749, 30, 30680, 1.7, train_rate=2.0),
    Gun("14in/45 Mk VII", 0.356, 721, 757, 40, 35260, 2.0, train_rate=2.0),
    Gun("38cm SK C/34", 0.380, 800, 820, 30, 36520, 1.5, train_rate=5.0),               # pattern [INFERRED]
    Gun("16in/45 Mk 6", 0.406, 1225, 701, 45, 33740, 1.9, train_rate=4.0),
    Gun("16in/50 Mk 7", 0.406, 1225, 762, 45, 38720, 1.9, train_rate=4.0),
    Gun("46cm Type 94", 0.460, 1460, 780, 45, 42030, 1.2, train_rate=2.0),
    Gun("8in/55 Mk 9", 0.203, 118, 853, 41, 29130, 2.0, train_rate=6.7),
    Gun("6in/47 Mk 16", 0.152, 59, 812, 47.5, 23880, 2.0, train_rate=10.0),
    Gun("5in/38 Mk 12", 0.127, 25, 792, 45, 15900, 1.7, train_rate=25.0),
    Gun("5in/54 Mk 45", 0.127, 31.75, 808, 45, 23700, 1.5, train_rate=30.0),          # pattern [INFERRED]
    Gun("76mm/62 OTO", 0.076, 6.3, 925, 45, 16000, 1.5, train_rate=60.0),              # pattern [INFERRED]
]}


def ballistics_table():
    print("\n=== 1. Ballistics check: model vs sourced 16in/50 Mk 7 (navweaps) ===")
    g = GUNS["16in/50 Mk 7"]
    src_tof = {10: 13.2, 15: 21.0, 20: 29.6, 25: 39.3, 30: 50.3}
    src_fall = {10: 5.0, 20: 14.9, 30: 28.3, 40: 45.5}
    print(f"{'kyd':>4} {'elev':>6} {'TOF s':>6} {'src':>5} {'fall':>6} {'src':>5} {'yd/mil':>7}")
    for k in (10, 15, 20, 25, 30, 35, 40):
        R = k * 1000 * YD
        print(f"{k:4d} {math.degrees(g.elev(R)):6.1f} {g.tof(R):6.1f} {src_tof.get(k, float('nan')):5.1f} "
              f"{math.degrees(g.fall(R)):6.1f} {src_fall.get(k, float('nan')):5.1f} {g.dR_del(R) * MIL / YD:7.1f}")
    print("\nAll guns: form factor, TOF and fall angle at 10 / 20 kyd, pattern sigma at 15 kyd")
    for g in GUNS.values():
        cells = []
        for k in (10, 20):
            R = k * 1000 * YD
            if R < g.table["R"][-1]:
                cells.append(f"{g.tof(R):5.1f}s {math.degrees(g.fall(R)):4.1f}deg")
            else:
                cells.append(f"{'-':>12}")
        R15 = min(15000 * YD, 0.9 * g.table["R"][-1])
        sr, sd = g.sigma_D(R15, 9)
        print(f"  {g.name:20s} i={g.i:4.2f}  {cells[0]}  {cells[1]}  sigma_D(9 guns)={sr / YD:4.0f} x {sd / YD:3.0f} yd")


for _g in GUNS.values():
    _g.fit()


# =============================================================================
# 2. Targets: size for hitting and for being seen
# =============================================================================
@dataclass
class TargetClass:
    name: str
    L: float        # m, waterline length
    B: float        # m, beam
    h: float        # m, effective hitting height (hull + armoured/vital superstructure) [note §6.4]
    top: float      # m, masthead height for visibility
    rcs_rel: float  # radar size relative to a battleship [INFERRED]
    speed: float    # kn, typical
    turn: float     # deg/s, sustained turn rate [INFERRED]


TARGETS = {t.name: t for t in [
    TargetClass("BB", 250, 33, 10.0, 40, 1.00, 27, 1.0),
    TargetClass("BC-WWI", 210, 29, 9.0, 35, 0.80, 26, 1.2),
    TargetClass("CA", 190, 20, 7.5, 32, 0.50, 32, 1.5),
    TargetClass("CL", 175, 18, 7.0, 30, 0.40, 32, 1.8),
    TargetClass("DD", 110, 11, 4.5, 22, 0.10, 35, 3.0),
    TargetClass("DE/FF", 95, 11, 4.0, 20, 0.08, 24, 3.0),
    TargetClass("PT/FAC", 25, 6, 2.0, 6, 0.01, 40, 6.0),
]}


def horizon(h1, h2):
    """Optical mutual visibility (m) with standard refraction: 3.86(sqrt h1 + sqrt h2) km [S]."""
    return 3860.0 * (math.sqrt(max(h1, 0)) + math.sqrt(max(h2, 0)))


def radar_horizon(h1, h2):
    """4/3-earth radar horizon, 4.12(sqrt h1 + sqrt h2) km [S, std]."""
    return 4120.0 * (math.sqrt(max(h1, 0)) + math.sqrt(max(h2, 0)))


def hit_test(x_land, y, fall, tc, aspect):
    """Exact geometric hit test of one shell against a box hull.
    Frame: x along the line of fire (away from the shooter), y across, origin = target centre.
    aspect = angle between target heading and the line of fire (0 = going away, 90 = broadside).
    Returns None, 'side' or 'deck'. The shell's last part of flight is the segment
    from (x_land - h cot(fall), y) at height h down to (x_land, y)."""
    c, s = math.cos(aspect), math.sin(aspect)
    # rectangle half extents along hull axes; intersect the line y = const with the rotated rectangle
    lo, hi = -1e9, 1e9
    # hull axis u = (c, s), v = (-s, c); point p = (x, y): u.p in [-L/2, L/2], v.p in [-B/2, B/2]
    for (ax, ay, half) in ((c, s, tc.L / 2), (-s, c, tc.B / 2)):
        # constraint: -half <= ax*x + ay*y <= half
        if abs(ax) < 1e-12:
            if abs(ay * y) > half:
                return None
            continue
        a = (-half - ay * y) / ax
        b = (half - ay * y) / ax
        if a > b:
            a, b = b, a
        lo, hi = max(lo, a), min(hi, b)
    if lo > hi:
        return None
    x0 = x_land - tc.h / math.tan(max(fall, 1e-3))
    if x_land < lo or x0 > hi:
        return None
    return "side" if x0 < lo else "deck"


def hitting_space(tc, R, gun, aspect_deg=90.0):
    """Range hitting space (danger space + presented depth) and presented width, m."""
    a = math.radians(aspect_deg)
    depth = tc.L * abs(math.cos(a)) + tc.B * abs(math.sin(a))
    width = tc.L * abs(math.sin(a)) + tc.B * abs(math.cos(a))
    danger = tc.h / math.tan(gun.fall(R))
    return danger, depth, width


# =============================================================================
# 3. Sensors: eye, stadimeter, optical rangefinders, radars, laser
# =============================================================================
@dataclass
class Sensor:
    name: str
    kind: str                 # eye | stadimeter | coinc | stereo | radar | laser
    height: float = 30.0      # m above water
    per_min: float = 2.0      # usable readings per minute
    base: float = 0.0         # m (optical)
    mag: float = 0.0          # magnification (optical)
    sig_brg_mil: float = 1.0  # bearing 1-sigma, mil
    rad_a: float = 0.0        # radar range sigma = rad_a + rad_b * R  (m)
    rad_b: float = 0.0
    rad_max_bb: float = 0.0   # radar max range on a battleship, m
    rad_min: float = 0.0
    splash: str = "none"      # radar splash spotting: none | range | both
    splash_max: float = 0.0   # m
    bearing_from_optics: bool = False   # metric radar: range only, bearing taken optically
    blind: tuple = ()         # relative-bearing blind sectors, deg (lo, hi), 0 = bow

    def unit_error(self, R):
        """Optical: one 'unit of error' (delta = 12 arcsec at the eye) in metres. [S OP 1171]"""
        BM = self.base * self.mag
        if self.base > 5.0:  # NDRC 1941: long bases deliver less than geometry predicts [INFERRED exponent]
            BM = 5.0 * (self.base / 5.0) ** 0.75 * self.mag
        return 12.0 * ARCSEC * R * R / BM

    def sigma_range(self, R, cond, tc, operator=1.0):
        """1-sigma random range error of one reading, m (None if no reading possible)."""
        k = self.kind
        if k == "eye":
            return 0.20 * R * operator
        if k == "stadimeter":
            return 0.01 * R if R < 7300 else None    # practical limit 6-8 kyd [INFERRED]
        if k in ("coinc", "stereo"):
            kc = cond.k_optic
            if k == "stereo" and cond.k_optic > 1.0:
                kc = 1.0 + 0.8 * (cond.k_optic - 1.0)       # stereo better in haze [S qual., INFERRED x0.8]
            if k == "coinc" and cond.low_contrast:
                kc *= 1.5                                    # needs vertical edges [INFERRED]
            return operator * kc * self.unit_error(R)
        if k == "radar":
            return self.rad_a + self.rad_b * R
        if k == "laser":
            return 5.0
        return None

    def sigma_bias(self, R):
        """Per-instrument systematic error, 1 sigma, m. Optical: ~1 unit of error after calibration
        [INFERRED from USNI 1930 calibration practice]. Stadimeter: mast-height guess 5 % [INFERRED]."""
        if self.kind in ("coinc", "stereo"):
            return 1.5 * self.unit_error(R)
        if self.kind == "stadimeter":
            return 0.05 * R
        if self.kind == "eye":
            return 0.10 * R
        if self.kind == "radar":
            return 0.5 * self.rad_a
        return 2.0

    def can_see(self, R, rel_brg_deg, cond, tc, obscured):
        for lo, hi in self.blind:
            if lo <= (rel_brg_deg % 360) <= hi:
                return False
        if self.kind == "radar":
            rmax = self.rad_max_bb * tc.rcs_rel ** 0.25
            return self.rad_min < R < min(rmax, radar_horizon(self.height, tc.top))
        if R > horizon(self.height, tc.top):
            return False
        vis = cond.vis if not cond.night else cond.night_vis
        if R > vis or obscured:
            return False
        if self.kind == "laser":
            return R < min(20000, vis)
        return True


def S_eye(h=20):            return Sensor("eye estimate", "eye", h, per_min=1, sig_brg_mil=5)
def S_stadi(h=20):          return Sensor("stadimeter", "stadimeter", h, per_min=2, sig_brg_mil=3)
def S_RF(name, base, mag, kind="coinc", h=25, per_min=2, brg=1.0, blind=()):
    return Sensor(name, kind, h, per_min=per_min, base=base, mag=mag, sig_brg_mil=brg, blind=blind)


# radar figures [S note §3]; ranges on a battleship-size target
def R_Seetakt():  return Sensor("FuMO 23 Seetakt", "radar", 35, 6, sig_brg_mil=20, rad_a=60, rad_max_bb=25000,
                                 rad_min=1000, bearing_from_optics=True)
def R_284():      return Sensor("Type 284", "radar", 35, 6, sig_brg_mil=15, rad_a=110, rad_max_bb=24000,
                                 rad_min=1000, splash="range", splash_max=20000, bearing_from_optics=True)
def R_284M():     return Sensor("Type 284M/P", "radar", 35, 6, sig_brg_mil=3, rad_a=60, rad_max_bb=27000,
                                 rad_min=1000, splash="range", splash_max=22000)
def R_Mk3():      return Sensor("Mk 3", "radar", 37, 6, sig_brg_mil=4, rad_a=37, rad_max_bb=26000,
                                 rad_min=900, splash="range", splash_max=18300, bearing_from_optics=True)
def R_Mk8():      return Sensor("Mk 8 Mod 3", "radar", 37, 10, sig_brg_mil=2, rad_a=14, rad_b=0.001,
                                 rad_max_bb=36600, rad_min=230, splash="range", splash_max=32000)
def R_Mk13():     return Sensor("Mk 13", "radar", 37, 20, sig_brg_mil=2, rad_a=14, rad_b=0.001,
                                 rad_max_bb=36600, rad_min=230, splash="both", splash_max=38400)
def R_Type22():   return Sensor("Type 22", "radar", 30, 3, sig_brg_mil=50, rad_a=200, rad_max_bb=30000,
                                 rad_min=1500, bearing_from_optics=True)
def R_Mk56():     return Sensor("Mk 56/35 auto-track", "radar", 25, 60, sig_brg_mil=1.0, rad_a=9, rad_max_bb=27000,
                                 rad_min=300, splash="both", splash_max=15000)
def R_digital():  return Sensor("SPQ-9/SPG-60, WM-25", "radar", 25, 60, sig_brg_mil=0.8, rad_a=9,
                                 rad_max_bb=40000, rad_min=300, splash="both", splash_max=20000)
def L_laser():    return Sensor("laser RF", "laser", 25, 30, sig_brg_mil=0.5)


@dataclass
class Conditions:
    vis: float = 25000          # m meteorological visibility
    night: bool = False
    night_vis: float = 5000     # m optical range at night (moon, starshell, searchlights) [S Savo ~5 mi]
    k_optic: float = 1.0        # rangefinder error multiplier: 1 clear, 1.5-2 haze/spray/vibration, 2-3 smoke/dusk
    low_contrast: bool = False  # grey ship on grey sky, hull-down, bow-on
    p_obscured: float = 0.0     # fraction of minutes the target is hidden to optics (smoke, squalls)
    operator: float = 1.0       # rangetaker quality: 0.85 elite, 1 trained, 1.5 average, 2 green/fatigued
    spot_k: float = 1.0         # optical spotting difficulty multiplier
    sea: int = 3                # sea state (laying error without stabilisation)
    combat: float = 1.0         # 1 = practice; ~1.5 = battle (haste, blast, shock, fear): multiplies salvo-to-salvo
                                # error, laying error, rangetaker error and spotting noise [INFERRED, calibrated]
    spot_confusion: float = 0.0 # chance a spot is lost or misread (several ships on one target, smoke, own hits)


# =============================================================================
# 4. Computing: trackers that stand in for the plot, clock or rangekeeper
# =============================================================================
# Historical machines were not Kalman filters. The trackers below are the cheapest model that
# reproduces what each machine could and could not do [INFERRED]:
#   'polar' = rate-keeping in range and bearing with constant rates (Dumaresq + clock, Dreyer,
#             Japanese plot). Constant rates are wrong for a crossing target, so it drifts by geometry.
#   'cart'  = true-course plot / rangekeeper (Argo, AFCT, Ford, Mk 1A, digital): tracks the target's
#             own course and speed, so geometry is exact and only target manoeuvres hurt.
# q = target-acceleration noise the computer "allows": small for men who assume a steady target,
#     larger for automatic trackers (faster re-convergence, noisier).

def _kf2_predict(x, P, dt, q):
    x0 = x[0] + x[1] * dt
    p00 = P[0][0] + 2 * dt * P[0][1] + dt * dt * P[1][1] + q * dt ** 3 / 3
    p01 = P[0][1] + dt * P[1][1] + q * dt * dt / 2
    p11 = P[1][1] + q * dt
    return [x0, x[1]], [[p00, p01], [p01, p11]]


def _kf2_update(x, P, z, r):
    s = P[0][0] + r
    k0, k1 = P[0][0] / s, P[0][1] / s
    y = z - x[0]
    x = [x[0] + k0 * y, x[1] + k1 * y]
    P = [[(1 - k0) * P[0][0], (1 - k0) * P[0][1]],
         [(1 - k0) * P[0][1], P[1][1] - k1 * P[0][1]]]
    return x, P


def _wrap(a):
    return (a + math.pi) % (2 * math.pi) - math.pi


class PolarTracker:
    def __init__(self, lvl, t, R, th, sR, sth, rel_v_est, sv):
        self.l, self.t = lvl, t
        uR = (math.cos(th), math.sin(th))
        up = (-math.sin(th), math.cos(th))
        rd = rel_v_est[0] * uR[0] + rel_v_est[1] * uR[1]
        bd = (rel_v_est[0] * up[0] + rel_v_est[1] * up[1]) / R
        self.r, self.Pr = [R, rd], [[sR * sR, 0], [0, sv * sv]]
        self.b, self.Pb = [th, bd], [[sth * sth, 0], [0, (sv / R) ** 2]]

    def _pred(self, t):
        dt = t - self.t
        if dt > 0:
            self.r, self.Pr = _kf2_predict(self.r, self.Pr, dt, self.l.q)
            self.b, self.Pb = _kf2_predict(self.b, self.Pb, dt, self.l.q / self.r[0] ** 2)
            self.t = t

    def update(self, t, own_pos, R=None, sR=None, th=None, sth=None):
        self._pred(t)
        if R is not None:
            self.r, self.Pr = _kf2_update(self.r, self.Pr, R, sR * sR)
        if th is not None:
            zt = self.b[0] + _wrap(th - self.b[0])
            self.b, self.Pb = _kf2_update(self.b, self.Pb, zt, sth * sth)

    def own_change(self, dv):
        """Own-ship velocity change. A 'helm-free' machine (own course/speed fed from gyro and log)
        corrects its rates at once; a plain clock does not and must re-learn them."""
        if not self.l.helm_free:
            return
        th, R = self.b[0], self.r[0]
        self.r[1] -= dv[0] * math.cos(th) + dv[1] * math.sin(th)
        self.b[1] -= (-dv[0] * math.sin(th) + dv[1] * math.cos(th)) / R

    def rel_at(self, t, T, own_pos, own_v):
        dt = t - self.t + T
        rd = max(-self.l.max_rdot, min(self.l.max_rdot, self.r[1]))
        bd = max(-self.l.max_bdot, min(self.l.max_bdot, self.b[1]))
        return self.r[0] + rd * dt, self.b[0] + bd * dt


class CartTracker:
    def __init__(self, lvl, t, R, th, sR, sth, own_pos, tgt_v_est, sv):
        self.l, self.t = lvl, t
        p = np.array([own_pos[0] + R * math.cos(th), own_pos[1] + R * math.sin(th)])
        self.x = np.array([p[0], p[1], tgt_v_est[0], tgt_v_est[1]])
        c, s = math.cos(th), math.sin(th)
        Rm = np.array([[c, -s], [s, c]])
        Pp = Rm @ np.diag([sR * sR, (R * sth) ** 2]) @ Rm.T
        self.P = np.zeros((4, 4))
        self.P[:2, :2] = Pp
        self.P[2, 2] = self.P[3, 3] = sv * sv

    def _pred(self, t):
        dt = t - self.t
        if dt <= 0:
            return
        F = np.eye(4)
        F[0, 2] = F[1, 3] = dt
        q = self.l.q
        Q = np.zeros((4, 4))
        for i in (0, 1):
            Q[i, i] = q * dt ** 3 / 3
            Q[i, i + 2] = Q[i + 2, i] = q * dt * dt / 2
            Q[i + 2, i + 2] = q * dt
        self.x = F @ self.x
        self.P = F @ self.P @ F.T + Q
        self.t = t

    def update(self, t, own_pos, R=None, sR=None, th=None, sth=None):
        self._pred(t)
        dx, dy = self.x[0] - own_pos[0], self.x[1] - own_pos[1]
        Rp = math.hypot(dx, dy)
        rows, zs, rs = [], [], []
        if R is not None:
            rows.append([dx / Rp, dy / Rp, 0, 0]); zs.append(R - Rp); rs.append(sR * sR)
        if th is not None:
            rows.append([-dy / Rp ** 2, dx / Rp ** 2, 0, 0]); zs.append(_wrap(th - math.atan2(dy, dx))); rs.append(sth * sth)
        if not rows:
            return
        H = np.array(rows)
        S = H @ self.P @ H.T + np.diag(rs)
        K = self.P @ H.T @ np.linalg.inv(S)
        self.x = self.x + K @ np.array(zs)
        self.P = (np.eye(4) - K @ H) @ self.P

    def own_change(self, dv):
        pass  # own motion is an exact input to a true-course machine

    def rel_at(self, t, T, own_pos, own_v):
        dt = t - self.t + T
        px = self.x[0] + self.x[2] * dt - (own_pos[0] + own_v[0] * T)
        py = self.x[1] + self.x[3] * dt - (own_pos[1] + own_v[1] * T)
        return math.hypot(px, py), math.atan2(py, px)


class ManoeuvreTracker:
    """Trackers that model the target's own manoeuvre, for the later machines.
    model 'ca': constant velocity plus a decaying (Singer) acceleration - the curvilinear / parabolic
           predictor of the 1950s analog computers (Sperry 'target course predictor', filed 1954) [S].
    model 'ct': coordinated turn, state (x, y, speed, heading, turn rate) - follows a ship on a steady
           circle exactly; stands in for 1970s digital filters [INFERRED] and, with a slow hand-set turn
           rate, for an AFCT-style enemy-turn setting [UNCERTAIN].
    detect: innovation test that inflates the motion uncertainty when the target clearly manoeuvres -
           the USN Mk 1A had a 'target just turned' button that did this by hand [S, navalgazing]."""

    def __init__(self, lvl, t, R, th, sR, sth, own_pos, tgt_v_est, sv):
        self.l, self.t, self.m = lvl, t, lvl.tracker
        px, py = own_pos[0] + R * math.cos(th), own_pos[1] + R * math.sin(th)
        c, s = math.cos(th), math.sin(th)
        Rm = np.array([[c, -s], [s, c]])
        Pp = Rm @ np.diag([sR * sR, (R * sth) ** 2]) @ Rm.T
        spd = float(np.hypot(*tgt_v_est))
        if self.m == "ca":
            self.x = np.array([px, py, tgt_v_est[0], tgt_v_est[1], 0.0, 0.0])
            self.P = np.zeros((6, 6))
            self.P[2, 2] = self.P[3, 3] = sv * sv
            self.P[4, 4] = self.P[5, 5] = lvl.sig_a ** 2
        else:
            self.x = np.array([px, py, spd, math.atan2(tgt_v_est[1], tgt_v_est[0]), 0.0])
            self.P = np.zeros((5, 5))
            self.P[2, 2] = (lvl.prior_kn * KN) ** 2
            self.P[3, 3] = math.radians(lvl.prior_deg) ** 2
            self.P[4, 4] = math.radians(1.0) ** 2
        self.P[:2, :2] = Pp
        self.strikes = 0

    # ---- motion models
    def _f(self, x, dt):
        x = x.copy()
        if self.m == "ca":
            tau = self.l.tau
            e = math.exp(-dt / tau)
            k1, k2 = tau * tau * (e - 1 + dt / tau), tau * (1 - e)
            x[0] += x[2] * dt + x[4] * k1
            x[1] += x[3] * dt + x[5] * k1
            x[2] += x[4] * k2
            x[3] += x[5] * k2
            x[4] *= e
            x[5] *= e
            return x
        v, psi, w = x[2], x[3], x[4]
        if abs(w) < 1e-6:
            x[0] += v * math.cos(psi) * dt
            x[1] += v * math.sin(psi) * dt
        else:
            x[0] += v / w * (math.sin(psi + w * dt) - math.sin(psi))
            x[1] += v / w * (-math.cos(psi + w * dt) + math.cos(psi))
        x[3] = psi + w * dt
        return x

    def _Q(self, dt):
        if self.m == "ca":
            e2 = 1 - math.exp(-2 * dt / self.l.tau)
            qa = self.l.sig_a ** 2 * e2
            Q = np.diag([qa * dt ** 4 / 20, qa * dt ** 4 / 20, qa * dt * dt / 3, qa * dt * dt / 3, qa, qa])
            Q[:2, :2] += np.eye(2) * self.l.q * dt ** 3 / 3
            return Q
        return np.diag([self.l.q * dt ** 3 / 3, self.l.q * dt ** 3 / 3, 0.05 ** 2 * dt, 1e-6 * dt,
                        self.l.q_turn * dt])

    def _pred(self, t):
        dt = t - self.t
        if dt <= 0:
            return
        n = len(self.x)
        F = np.zeros((n, n))
        fx = self._f(self.x, dt)
        for i in range(n):
            d = np.zeros(n)
            d[i] = 1e-4 * max(1.0, abs(self.x[i]))
            F[:, i] = (self._f(self.x + d, dt) - fx) / d[i]
        self.x = fx
        self.P = F @ self.P @ F.T + self._Q(dt)
        self.t = t

    def update(self, t, own_pos, R=None, sR=None, th=None, sth=None):
        self._pred(t)
        n = len(self.x)
        dx, dy = self.x[0] - own_pos[0], self.x[1] - own_pos[1]
        Rp = math.hypot(dx, dy)
        rows, zs, rs = [], [], []
        if R is not None:
            h = np.zeros(n); h[0], h[1] = dx / Rp, dy / Rp
            rows.append(h); zs.append(R - Rp); rs.append(sR * sR)
        if th is not None:
            h = np.zeros(n); h[0], h[1] = -dy / Rp ** 2, dx / Rp ** 2
            rows.append(h); zs.append(_wrap(th - math.atan2(dy, dx))); rs.append(sth * sth)
        if not rows:
            return
        H = np.array(rows)
        S = H @ self.P @ H.T + np.diag(rs)
        Si = np.linalg.inv(S)
        z = np.array(zs)
        if self.l.detect:   # 'target just turned': two big surprises in a row open the filter up
            nis = float(z @ Si @ z)
            self.strikes = self.strikes + 1 if nis > (10.8 if len(zs) == 1 else 13.8) else 0  # chi2 99.9 %
            if self.strikes >= 2:
                if self.m == "ca":
                    self.P[4, 4] += (0.3) ** 2; self.P[5, 5] += (0.3) ** 2
                    self.P[2, 2] += 2.0 ** 2; self.P[3, 3] += 2.0 ** 2
                else:
                    self.P[4, 4] += math.radians(1.0) ** 2; self.P[3, 3] += math.radians(10) ** 2
                self.strikes = 0
                S = H @ self.P @ H.T + np.diag(rs)
                Si = np.linalg.inv(S)
        K = self.P @ H.T @ Si
        self.x = self.x + K @ z
        self.P = (np.eye(n) - K @ H) @ self.P
        if self.m == "ct":
            self.x[4] = max(-0.05, min(0.05, self.x[4]))   # no ship turns faster than ~3 deg/s

    def own_change(self, dv):
        pass

    def rel_at(self, t, T, own_pos, own_v):
        x = self._f(self.x, max(t - self.t + T, 0.0))
        px = x[0] - (own_pos[0] + own_v[0] * T)
        py = x[1] - (own_pos[1] + own_v[1] * T)
        return math.hypot(px, py), math.atan2(py, px)

    def velocity(self):
        if self.m == "ca":
            return np.array([self.x[2], self.x[3]])
        return self.x[2] * np.array([math.cos(self.x[3]), math.sin(self.x[3])])


def make_tracker(lvl, t, R, th, sR, sth, own_pos, own_v, tgt_v_est, sv):
    if lvl.tracker == "cart":
        return CartTracker(lvl, t, R, th, sR, sth, own_pos, tgt_v_est, sv)
    if lvl.tracker in ("ca", "ct"):
        return ManoeuvreTracker(lvl, t, R, th, sR, sth, own_pos, tgt_v_est, sv)
    return PolarTracker(lvl, t, R, th, sR, sth, tgt_v_est - own_v, sv)


# =============================================================================
# 5. Fire-control levels, MK1 Eyeball to digital radar
# =============================================================================
@dataclass
class FCLevel:
    name: str
    year: int
    sensors: callable            # -> list[Sensor] typical fit for the era
    tracker: str = "polar"
    helm_free: bool = False
    q: float = 0.05              # m2/s3
    latency: float = 15.0        # s from reading to the computer
    prior_kn: float = 3.0        # human estimate of target speed, 1 sigma
    prior_deg: float = 20.0      # ... and course
    max_rdot: float = 1e9        # m/s the clock can hold (Dreyer +-1,200 yd/min = 18.3 m/s) [S]
    max_bdot: float = 1e9        # rad/s (Dreyer +-15 deg/min) [S]
    resid: float = 0.01          # residual ballistic bias before spotting, 1 sigma, fraction of range [S/INFERRED]
    defl_bias_mil: float = 2.0   # wind/drift/parallax residual, 1 sigma
    lay_mil: float = 1.0         # common laying error per salvo, 1 sigma (elev and train)
    director: bool = True        # False = each gun lays itself (errors become extra dispersion)
    stabilised: bool = False     # stable vertical / gyro firing: no sea-state penalty
    own_turn_kick: float = 0.0   # m, extra error while own ship turns (Mk 8 "several hundred yd") [S]
    spot_gain: float = 0.5       # fraction of a measured miss corrected (magnitude spotting). Half, with a dead band
                                 # of half a pattern: one salvo's MPI carries random salvo error, so full
                                 # corrections chase noise ("never spot below the pattern size") [S rule, INFERRED gain]
    ladder0: float = 366.0       # first ladder step, m (400 yd RN) [S]
    h_spot: float = 30.0         # spotting top height, m
    aircraft: bool = False
    salvo_pct: float = 1.0       # salvo-to-salvo random MPI error in range, 1 sigma, % of range: stable-element and
                                 # servo error, salvo-mean muzzle-velocity scatter, turret-to-turret calibration,
                                 # generated-range wander. Spotting cannot remove it. [INFERRED, calibrated to the
                                 # USN 1944 hit study and the 1932-33 LRBP 313 yd mean MPI error]
    salvo_defl_mil: float = 1.5  # same, deflection
    tau: float = 20.0            # 'ca' tracker: acceleration memory, s
    sig_a: float = 0.25          # 'ca' tracker: target acceleration 1 sigma, m/s2 (25 kn ship turning 1 deg/s ~ 0.22)
    q_turn: float = 1e-5         # 'ct' tracker: turn-rate noise, rad2/s3
    detect: bool = False         # manoeuvre detection ('target just turned')


LEVELS = [
    FCLevel("0 MK1 Eyeball (c.1890)", 1890, lambda: [S_eye(18)], "polar", False, q=0.02, latency=20,
            prior_kn=5, prior_deg=30, resid=0.025, defl_bias_mil=4, lay_mil=3.0, director=False, ladder0=550, h_spot=18, salvo_pct=2.5, salvo_defl_mil=3.0),
    FCLevel("1 Early RF + stadimeter (1905)", 1905, lambda: [S_RF("FA3 4.5 ft", 1.37, 24, h=20), S_stadi(20)],
            "polar", False, q=0.02, latency=15, prior_kn=4, prior_deg=25, resid=0.02, defl_bias_mil=3,
            lay_mil=2.5, director=False, h_spot=20, salvo_pct=2.0, salvo_defl_mil=2.5),
    FCLevel("2 Dumaresq + Vickers clock, 9 ft RF (1910)", 1910,
            lambda: [S_RF("FQ2 9 ft", 2.74, 28, h=25), S_RF("FQ2 9 ft", 2.74, 28, h=12)],
            "polar", False, q=0.03, latency=15, prior_kn=3, prior_deg=20, max_rdot=18.3, resid=0.015,
            defl_bias_mil=2.5, lay_mil=2.0, director=False, h_spot=30, salvo_pct=1.8, salvo_defl_mil=2.5),
    FCLevel("3 Dreyer table + director (1916)", 1916,
            lambda: [S_RF("FQ2 9 ft", 2.74, 28, h=30), S_RF("FT24 15 ft", 4.57, 28, h=25), S_RF("FQ2 9 ft", 2.74, 28, h=12)],
            "polar", True, q=0.03, latency=15, prior_kn=2.5, prior_deg=15, max_rdot=18.3, max_bdot=math.radians(0.25),
            resid=0.012, defl_bias_mil=2.0, lay_mil=1.0, own_turn_kick=300, h_spot=35, salvo_pct=1.5, salvo_defl_mil=2.0),
    FCLevel("3b Pollen Argo true-course clock (1913)", 1913,
            lambda: [S_RF("9 ft gyro-stabilised", 2.74, 28, h=30, per_min=8), S_RF("FT24 15 ft", 4.57, 28, h=25)],
            "cart", True, q=0.03, latency=8, prior_kn=2.5, prior_deg=15, resid=0.012, defl_bias_mil=2.0,
            lay_mil=1.0, own_turn_kick=200, h_spot=35, salvo_pct=1.5, salvo_defl_mil=2.0),
    FCLevel("4 Interwar central table, AFCT (1930s)", 1935,
            lambda: [S_RF("15 ft DCT", 4.57, 28, h=35, per_min=4), S_RF("30 ft turret", 9.1, 28, h=10, per_min=4)],
            "cart", True, q=0.03, latency=8, prior_kn=2, prior_deg=12, resid=0.006, defl_bias_mil=1.5,
            lay_mil=0.8, own_turn_kick=250, h_spot=35, salvo_pct=1.2, salvo_defl_mil=1.5),
    FCLevel("4t AFCT with enemy-turn setting [UNCERTAIN]", 1935,
            lambda: [S_RF("15 ft DCT", 4.57, 28, h=35, per_min=4), S_RF("30 ft turret", 9.1, 28, h=10, per_min=4)],
            "ct", True, q=0.03, q_turn=1e-7, latency=8, prior_kn=2, prior_deg=12, resid=0.006, defl_bias_mil=1.5,
            lay_mil=0.8, own_turn_kick=250, h_spot=35, salvo_pct=1.2, salvo_defl_mil=1.5),
    FCLevel("4j Japanese Type 92/98 plot (1930s)", 1935,
            lambda: [S_RF("10 m triplex", 10.0, 25, h=35, per_min=4), S_RF("8 m turret", 8.0, 25, h=10, per_min=4)],
            "polar", True, q=0.03, latency=15, prior_kn=2, prior_deg=12, resid=0.006, defl_bias_mil=1.5,
            lay_mil=1.0, own_turn_kick=400, h_spot=40, salvo_pct=1.3, salvo_defl_mil=1.5),
    FCLevel("5 Ford Mk 8 + stable vertical, optical (1941)", 1941,
            lambda: [S_RF("Mk 48 26.5 ft stereo", 8.1, 25, "stereo", h=40, per_min=6, brg=0.7),
                     S_RF("Mk 52 46 ft turret", 14.0, 25, "stereo", h=12, per_min=4, brg=1.0)],
            "cart", True, q=0.05, latency=3, prior_kn=2, prior_deg=12, resid=0.004, defl_bias_mil=1.0,
            lay_mil=0.5, stabilised=True, own_turn_kick=150, h_spot=40, salvo_pct=1.0, salvo_defl_mil=1.2),
    FCLevel("5r Mk 8 + Mk 3 radar range (1942)", 1942,
            lambda: [S_RF("Mk 48 26.5 ft stereo", 8.1, 25, "stereo", h=40, per_min=6, brg=0.7), R_Mk3()],
            "cart", True, q=0.05, latency=3, prior_kn=2, prior_deg=12, resid=0.004, defl_bias_mil=1.0,
            lay_mil=0.5, stabilised=True, own_turn_kick=150, h_spot=40, salvo_pct=1.0, salvo_defl_mil=1.2),
    FCLevel("6 Mk 8 + Mk 13 radar, radar spotting (1945)", 1945,
            lambda: [S_RF("Mk 48 26.5 ft stereo", 8.1, 25, "stereo", h=40, per_min=6, brg=0.7), R_Mk13()],
            "cart", True, q=0.05, latency=2, prior_kn=2, prior_deg=12, resid=0.004, defl_bias_mil=1.0,
            lay_mil=0.5, stabilised=True, own_turn_kick=150, h_spot=40, salvo_pct=1.0, salvo_defl_mil=1.2),
    FCLevel("7 Auto-track radar, analog (Mk 56/68, 1955)", 1955,
            lambda: [R_Mk56(), S_RF("optical", 4.6, 24, "stereo", h=25, per_min=4)],
            "ca", True, detect=True, sig_a=0.1, q=0.02, latency=1, prior_kn=2, prior_deg=12, resid=0.004, defl_bias_mil=0.8,
            lay_mil=0.4, stabilised=True, own_turn_kick=50, h_spot=25, salvo_pct=0.7, salvo_defl_mil=0.8),
    FCLevel("8 Digital + laser (Mk 86 / WM-25, 1975)", 1975, lambda: [R_digital(), L_laser()],
            "ct", True, detect=True, q=0.01, q_turn=5e-7, latency=0.5, prior_kn=2, prior_deg=12, resid=0.003, defl_bias_mil=0.5,
            lay_mil=0.3, stabilised=True, own_turn_kick=20, h_spot=25, salvo_pct=0.4, salvo_defl_mil=0.5),
    FCLevel("L Local control, WWII turret", 1941,
            lambda: [S_RF("turret 46 ft", 14.0, 25, "stereo", h=10, per_min=3, brg=1.5)],
            "polar", True, q=0.03, latency=10, prior_kn=3, prior_deg=20, resid=0.006, defl_bias_mil=2.0,
            lay_mil=1.5, director=False, own_turn_kick=300, h_spot=10, salvo_pct=1.5, salvo_defl_mil=2.0),
]
LV = {l.name.split()[0]: l for l in LEVELS}


# =============================================================================
# 6. The engagement: ships move, sensors read, the computer tracks, guns fire, spotters correct
# =============================================================================
@dataclass
class Shooter:
    gun: Gun
    turrets: tuple = ((3, 0, 150), (3, 0, 150), (3, 180, 150))   # (guns, centre rel. bearing deg, half-arc deg)
    interval: float = 40.0      # s between salvos [S: USN 30-84 s main battery]
    salvo_frac: float = 1.0     # 0.5 = half-salvos at half the interval
    speed: float = 25.0         # kn
    gun_output: float = 1.0     # fraction of guns that actually fire (defects, PoW 0.74) [S]


@dataclass
class Scenario:
    name: str
    shooter: Shooter
    tgt: TargetClass
    R0: float                   # m
    tgt_brg: float = 90.0       # deg, target bearing relative to own course at start (port = +)
    tgt_course: float = 0.0     # deg, target course relative to own course
    tgt_speed: float = 25.0
    tgt_policy: str = "steady"  # steady | zigzag | chase | crippled
    tgt_amp: float = 30.0       # deg, zigzag / chase course change
    tgt_period: float = 240.0   # s, zigzag period / minimum time between chase turns
    tgt_turn: float = 0.0       # deg/s, 'circle' policy: steady turn (limited by the target's turn rate)
    tgt_turn_start: float = 0.0 # s, when the circle starts
    own_policy: str = "steady"
    own_amp: float = 30.0
    own_period: float = 300.0
    cond: Conditions = field(default_factory=Conditions)
    duration: float = 900.0     # s
    t_open: float = 120.0       # s after the first range is in the computer
    sensors: callable = None    # override the level's sensor fit
    resid_override: float = None


def _guns_bearing(turrets, rel_deg):
    n = 0
    for g, c, half in turrets:
        if abs((rel_deg - c + 180) % 360 - 180) <= half:
            n += g
    return n


def engage(scn, lvl, seed=0, trace=False):
    rng = np.random.default_rng(seed)
    gun, sh, tc, cond = scn.shooter.gun, scn.shooter, scn.tgt, scn.cond
    resid = lvl.resid if scn.resid_override is None else scn.resid_override
    # ---- kinematics (math angles, rad; world frame)
    own_c, own_des = 0.0, 0.0
    own_p = np.zeros(2)
    own_spd = sh.speed * KN
    b0 = math.radians(scn.tgt_brg)
    tgt_p = np.array([scn.R0 * math.cos(b0), scn.R0 * math.sin(b0)])
    tgt_base = math.radians(scn.tgt_course)
    tgt_c, tgt_des = tgt_base, tgt_base
    tgt_spd = scn.tgt_speed * KN
    next_zz = scn.tgt_period * rng.uniform(0.3, 1.0)
    own_next = scn.own_period * rng.uniform(0.5, 1.0)
    last_chase = -1e9

    def vel(c, s):
        return np.array([s * math.cos(c), s * math.sin(c)])

    sensors = (scn.sensors or lvl.sensors)()
    sens_next = [rng.uniform(0, 60 / s.per_min) for s in sensors]
    sens_bias = [rng.normal() for s in sensors]
    obsc = rng.random(int(scn.duration / 60) + 2) < cond.p_obscured
    # unknown ballistic biases (wind, density, MV, drift residuals) for this engagement
    zb_r, zb_d = rng.normal(), rng.normal()
    sea_k = 1.0 if lvl.stabilised else 1.0 + 0.25 * max(cond.sea - 3, 0)
    # estimate of target motion made by eye before the plot says anything [S: 1-2 kn, 10-30 deg]
    est_spd = scn.tgt_speed * KN + rng.normal(0, lvl.prior_kn * KN)
    est_c = tgt_c + math.radians(rng.normal(0, lvl.prior_deg))
    sv = math.hypot(lvl.prior_kn * KN, scn.tgt_speed * KN * math.radians(lvl.prior_deg))

    trk, queue, salvos, spots = None, [], [], []
    c_r = c_d = 0.0                 # spot corrections in force (m)
    ladder = dict(step=lvl.ladder0, last=0)
    out = dict(shells=0, hits=0, side=0, deck=0, salvos=0, straddles=0, t_straddle=None,
               t_hit=None, n_straddle=None, mpi=[], t_track=None, mpi_t=[], hit_times=[])
    next_salvo = None
    prev_own_v = vel(own_c, own_spd)
    dt = 1.0
    t = 0.0
    while t <= scn.duration:
        # ---- manoeuvres
        if scn.tgt_policy == "zigzag" and t >= next_zz:
            tgt_des = tgt_base + math.radians(scn.tgt_amp) * rng.choice([-1, 1])
            next_zz = t + scn.tgt_period * rng.uniform(0.7, 1.3)
        if scn.tgt_policy == "crippled" and t >= next_zz:
            tgt_des = tgt_c + math.radians(rng.normal(0, 25))
            next_zz = t + 60
        if scn.own_policy == "zigzag" and t >= own_next:
            own_des = math.radians(scn.own_amp) * rng.choice([-1, 1])
            own_next = t + scn.own_period * rng.uniform(0.7, 1.3)
        turn_t = math.radians(tc.turn) * dt
        if scn.tgt_policy == "circle" and t >= scn.tgt_turn_start:
            tgt_c += math.radians(max(-tc.turn, min(tc.turn, scn.tgt_turn))) * dt
            tgt_des = tgt_c
        else:
            tgt_c += max(-turn_t, min(turn_t, _wrap(tgt_des - tgt_c)))
        turn_o = math.radians(1.0) * dt
        d_o = max(-turn_o, min(turn_o, _wrap(own_des - own_c)))
        own_turning = abs(d_o) > 1e-4
        own_c += d_o
        own_v, tgt_v = vel(own_c, own_spd), vel(tgt_c, tgt_spd)
        own_p = own_p + own_v * dt
        tgt_p = tgt_p + tgt_v * dt
        rel = tgt_p - own_p
        R = float(np.hypot(*rel))
        th = math.atan2(rel[1], rel[0])
        rel_deg = math.degrees(_wrap(th - own_c)) % 360
        if trk is not None:
            trk.own_change(own_v - prev_own_v)
        prev_own_v = own_v
        minute = int(t // 60)
        # ---- sensor readings
        optical_ok = True
        for i, s in enumerate(sensors):
            if t < sens_next[i]:
                continue
            sens_next[i] = t + 60 / s.per_min * rng.uniform(0.7, 1.3)
            if not s.can_see(R, rel_deg, cond, tc, obsc[minute] and s.kind not in ("radar",)):
                continue
            sR = s.sigma_range(R, cond, tc, cond.operator * cond.combat)
            if sR is None:
                continue
            kb = (cond.k_optic * (2.0 if cond.low_contrast else 1.0)) if s.kind in ("coinc", "stereo") else 1.0
            zR = R + sens_bias[i] * kb * s.sigma_bias(R) + rng.normal(0, sR)
            zth, sth = None, None
            if s.kind != "radar" or not s.bearing_from_optics:
                sth = s.sig_brg_mil * MIL * (1.0 if s.kind == "radar" else cond.k_optic ** 0.5)
                zth = th + rng.normal(0, sth)
            queue.append((t + lvl.latency, t, own_p.copy(), zR, sR, zth, sth))
        # optical bearing for range-only radars: the director layer/trainer keeps on the target
        if any(s.kind == "radar" and s.bearing_from_optics for s in sensors):
            vis = cond.night_vis if cond.night else cond.vis
            if R < vis and not obsc[minute] and int(t) % 10 == 0:
                sth = 1.0 * MIL
                queue.append((t + lvl.latency, t, own_p.copy(), None, None, th + rng.normal(0, sth), sth))
        # ---- computer
        while queue and queue[0][0] <= t:
            _, tm, op, zR, sR, zth, sth = queue.pop(0)
            if trk is None:
                if zR is None or zth is None:
                    continue
                trk = make_tracker(lvl, tm, zR, zth, sR, sth, op, own_v, vel(est_c, est_spd), sv)
                out["t_track"] = t
                next_salvo = t + scn.t_open
            else:
                trk.update(tm, op, zR, sR, zth, sth)
        # ---- fire
        if trk is not None and next_salvo is not None and t >= next_salvo:
            next_salvo = t + sh.interval * sh.salvo_frac
            n = int(round(_guns_bearing(sh.turrets, rel_deg) * sh.gun_output * sh.salvo_frac))
            Rp, thp = trk.rel_at(t, 0.0, own_p, own_v)
            T = gun.tof(min(Rp, gun.table["R"][-1]))
            for _ in range(3):
                Rp, thp = trk.rel_at(t, T, own_p, own_v)
                T = gun.tof(min(Rp, gun.table["R"][-1]))
            R_ord = Rp + c_r
            if n > 0 and 500 < R_ord < gun.table["R"][-1]:
                lay = lvl.lay_mil * MIL * sea_k * cond.combat
                e_el = rng.normal(0, lay) * gun.dR_del(R_ord) if lvl.director else 0.0
                e_tr = rng.normal(0, lay) * R_ord if lvl.director else 0.0
                kick = rng.normal(0, lvl.own_turn_kick, 2) if own_turning else (0.0, 0.0)
                e_sr = rng.normal(0, cond.combat * lvl.salvo_pct / 100 * R_ord)
                e_sd = rng.normal(0, cond.combat * lvl.salvo_defl_mil * MIL * R_ord)
                R_act = R_ord + e_el + e_sr + zb_r * resid * R_ord + kick[0]
                lat = c_d + e_tr + e_sd + zb_d * lvl.defl_bias_mil * MIL * R_ord + kick[1]
                th_act = thp + lat / R_ord
                T_act = gun.tof(min(R_act, gun.table["R"][-1]))
                sr, sd = gun.sigma_D(R_act, n)
                if not lvl.director:  # each gun's own laying error widens the pattern
                    sr = math.hypot(sr, lay * gun.dR_del(R_act))
                    sd = math.hypot(sd, lay * R_act)
                u = np.array([math.cos(th_act), math.sin(th_act)])
                w = np.array([-u[1], u[0]])
                centre = own_p + own_v * T_act + u * R_act
                pts = [centre + u * rng.normal(0, sr) + w * rng.normal(0, sd) for _ in range(n)]
                salvos.append(dict(t_imp=t + T_act, pts=pts, u=u, w=w, R=R_act, c_r=c_r, c_d=c_d,
                                   pattern=sr * 4.0, t_fire=t))
        # ---- impacts
        for s in [s for s in salvos if s["t_imp"] <= t]:
            salvos.remove(s)
            out["salvos"] += 1
            u, w = s["u"], s["w"]
            fall = gun.fall(min(s["R"], gun.table["R"][-1]))
            aspect = _wrap(tgt_c - math.atan2(u[1], u[0]))
            xs, ys, hits = [], [], 0
            for p in s["pts"]:
                d = p - tgt_p
                x, y = float(d @ u), float(d @ w)
                xs.append(x); ys.append(y)
                hres = hit_test(x, y, fall, tc, aspect)
                if hres:
                    hits += 1
                    out[hres] += 1
            out["shells"] += len(xs)
            out["hits"] += hits
            out["hit_times"] += [t] * hits
            mx, my = float(np.mean(xs)), float(np.mean(ys))
            out["mpi"].append((mx, my))
            out["mpi_t"].append(s["t_fire"] - (out["t_track"] or 0))
            strad = (min(xs) < 0 < max(xs) and abs(my) < tc.L / 2 + 50) or hits > 0
            if strad:
                out["straddles"] += 1
                if out["t_straddle"] is None:
                    out["t_straddle"], out["n_straddle"] = s["t_fire"], out["salvos"]
            if hits and out["t_hit"] is None:
                out["t_hit"] = s["t_fire"]
            if trace:
                print(f"  t={s['t_fire']:5.0f} R={kyd(s['R']):5.1f}k n={len(xs)} MPI {mx / YD:+6.0f} yd {my / YD:+5.0f} yd"
                      f" hits {hits} {'STRADDLE' if strad else ''}")
            # ---- spotting observation
            vis = cond.night_vis if cond.night else cond.vis
            sees = R < vis and not obsc[minute]
            obs = dict(c_r=s["c_r"], c_d=s["c_d"], pattern=s["pattern"], t_ready=t + 6)
            radar_spot = [z for z in sensors if z.kind == "radar" and z.splash != "none"
                          and R < z.splash_max and z.can_see(R, rel_deg, cond, tc, False)]
            if radar_spot:
                z = radar_spot[0]
                obs["range"] = mx + rng.normal(0, max(25.0, 2 * z.sigma_range(R, cond, tc)))
                obs["t_ready"] = t + 4
                if z.splash == "both":
                    obs["defl"] = my + rng.normal(0, 2 * MIL * R)
            elif lvl.aircraft:
                obs["range"] = mx + rng.normal(0, 70)
                obs["defl"] = my + rng.normal(0, 70)
                obs["t_ready"] = t + 30
            elif sees:
                # optical: over/short judged from the depression of each splash against the hull
                so = 4.0e-5 * cond.spot_k * cond.combat * R * R / lvl.h_spot      # [INFERRED; calibrated to "500 yd obvious at
                common = rng.normal(0, 0.5 * so)                     #  12 kyd, barely seen at 19 kyd", NavPers]
                seen = [x + common + rng.normal(0, so) for x in xs]
                over, short = sum(v > 0 for v in seen), sum(v < 0 for v in seen)
                obs["sign"] = "straddle" if (hits or (over and short)) else ("over" if over else "short")
            if sees and "defl" not in obs:
                obs["defl"] = my + rng.normal(0, 1.0 * MIL * R)
            if rng.random() < cond.spot_confusion:
                obs = {}
            if "range" in obs or "sign" in obs or "defl" in obs:
                spots.append(obs)
            if scn.tgt_policy == "chase" and t - last_chase > scn.tgt_period and abs(mx) < 600:
                # salvo chasing: steer for the splash just seen (the place the shooter will correct away from),
                # limited to +-amp from the base course; see evasion_ref.py for the full study
                mpi_w = tgt_p + u * mx + w * my
                dv = mpi_w - tgt_p
                rel = _wrap(math.atan2(dv[1], dv[0]) - tgt_base)
                amp = math.radians(scn.tgt_amp)
                if abs(rel) > math.radians(150):
                    rel = math.copysign(amp, rel)
                tgt_des = tgt_base + max(-amp, min(amp, rel))
                last_chase = t
        # ---- apply spots (relative to the correction the spotted salvo was fired with)
        for o in [o for o in spots if o["t_ready"] <= t]:
            spots.remove(o)
            if "range" in o:
                if abs(o["range"]) > 0.5 * o["pattern"]:
                    c_r = o["c_r"] - lvl.spot_gain * o["range"]
            elif "sign" in o:
                st = ladder["step"]
                smin = max(46.0, 0.5 * o["pattern"])
                if o["sign"] == "straddle":
                    ladder["step"] = max(smin, st / 2)
                    c_r = o["c_r"]
                else:
                    d = -1 if o["sign"] == "over" else 1
                    if ladder["last"] and d != ladder["last"]:
                        ladder["step"] = st = max(smin, st / 2)
                    c_r = o["c_r"] + d * st
                    ladder["last"] = d
            if "defl" in o and abs(o["defl"]) > 0.25 * o["pattern"]:
                c_d = o["c_d"] - lvl.spot_gain * o["defl"]
        t += dt
    return out


def run(scn, lvl, n=60, seed0=1):
    """Monte-Carlo summary of n engagements."""
    sh = hits = st = sal = 0
    t_st, t_hit, n_st, side, deck = [], [], [], 0, 0
    for k in range(n):
        o = engage(scn, lvl, seed=seed0 + k)
        sh += o["shells"]; hits += o["hits"]; st += o["straddles"]; sal += o["salvos"]
        side += o["side"]; deck += o["deck"]
        if o["t_straddle"] is not None:
            t_st.append(o["t_straddle"] - o["t_track"]); n_st.append(o["n_straddle"])
        if o["t_hit"] is not None:
            t_hit.append(o["t_hit"] - o["t_track"])
    return dict(pct=100 * hits / max(sh, 1), shells=sh / n, hits=hits / n, straddle_pct=100 * st / max(sal, 1),
                t_straddle=float(np.median(t_st)) if t_st else None, n_straddle=float(np.median(n_st)) if n_st else None,
                t_hit=float(np.median(t_hit)) if t_hit else None, p_any_hit=len(t_hit) / n,
                deck_frac=deck / max(side + deck, 1))


# =============================================================================
# 7. Historical calibration cases (model vs record). Hit counts are often uncertain: see the note.
# =============================================================================
KYD = 1000 * YD
pre = TargetClass("pre-dread", 121, 23, 8, 30, 0.6, 13, 1.0)
rod = Gun("16in/45 Mk I (Rodney)", 0.406, 929, 785, 40, 36300, 2.2, train_rate=2.0).fit()
zeiss = lambda: [S_RF("Zeiss 3 m stereo", 3.0, 23, "stereo", h=30, per_min=3), S_RF("Zeiss 3 m stereo", 3.0, 23, "stereo", h=15, per_min=3)]
bis = lambda: [S_RF("10.5 m stereo", 10.5, 23, "stereo", h=35, per_min=4), S_RF("10.5 m stereo", 10.5, 23, "stereo", h=20, per_min=4), S_RF("7 m stereo", 7.0, 23, "stereo", h=18, per_min=4)]
twin4 = ((4, 0, 150), (4, 180, 150))
HISTORY = [
 ("Tsushima 1905 (Mikasa 12in, ~9%)", Scenario("tsu", Shooter(GUNS["12in/40 (Mikasa)"], ((2,0,135),(2,180,135)), 60, speed=15), pre, 6*KYD, 90, 0, 9, cond=Conditions(vis=10000, combat=1.5), duration=1200), LV["1"], 9),
 ("Jutland RtS, British BCs (1.4%)", Scenario("jut", Shooter(GUNS["13.5in/45 Mk V(H)"], twin4, 60, 0.5), TARGETS["BC-WWI"], 15000, 80, 0, 25, "zigzag", 15, 300, own_policy="zigzag", own_amp=15,
       cond=Conditions(vis=16000, k_optic=1.5, low_contrast=True, p_obscured=0.25, spot_k=1.5, combat=1.5, spot_confusion=0.2), duration=1500), LV["3"], 1.4),
 ("Jutland RtS, German 1SG (4.0%)", Scenario("jutg", Shooter(GUNS["30.5cm SK L/50"], twin4, 60, 0.5), TARGETS["BC-WWI"], 15000, 80, 0, 25, "zigzag", 15, 300, own_policy="zigzag", own_amp=15,
       cond=Conditions(vis=18000, p_obscured=0.1, combat=1.5), duration=1500, sensors=zeiss), LV["3"], 4.0),
 ("Denmark Strait, Bismarck (~5.5%)", Scenario("ds", Shooter(GUNS["38cm SK C/34"], twin4, 50, 0.5), TARGETS["BB"], 20000, 60, -120, 28, cond=Conditions(vis=30000, combat=1.5, spot_confusion=0.2), duration=600, t_open=60, sensors=bis), LV["4"], 5.5),
 ("LRBP 1932, 16in/45 at 30 kyd (4.4%)", Scenario("lrbp", Shooter(GUNS["16in/45 Mk 6"], twin4, 60), TARGETS["BB"], 30*KYD, 90, 0, 15, cond=Conditions(vis=40000), duration=900), LV["4"], 4.4),
 ("USN 1944 study 16in/50, 10 kyd (32.7%)", Scenario("u10", Shooter(GUNS["16in/50 Mk 7"]), TARGETS["BB"], 10*KYD, 90, 0, 15, duration=900), LV["6"], 32.7),
 ("USN 1944 study 16in/50, 20 kyd (10.5%)", Scenario("u20", Shooter(GUNS["16in/50 Mk 7"]), TARGETS["BB"], 20*KYD, 90, 0, 15, duration=900), LV["6"], 10.5),
 ("USN 1944 study 16in/50, 30 kyd (2.7%)", Scenario("u30", Shooter(GUNS["16in/50 Mk 7"]), TARGETS["BB"], 30*KYD, 90, 0, 15, duration=900), LV["6"], 2.7),
 ("North Cape, Duke of York (~3%)", Scenario("nc", Shooter(GUNS["14in/45 Mk VII"], ((4,0,150),(2,0,150),(4,180,150)), 100, speed=27, gun_output=0.8), TARGETS["BB"], 11000, 20, 0, 31, "chase", 30, 120,
       cond=Conditions(night=True, night_vis=2000, sea=6, combat=1.5), duration=5400, sensors=lambda: [R_284M()]), LV["4"], 3.0),
 ("Guadalcanal, Washington (9-20 of 75)", Scenario("wk", Shooter(GUNS["16in/45 Mk 6"], ((6,0,150),(3,180,150)), 30), TARGETS["BB"], 8400*0.9144, 70, 180, 20,
       cond=Conditions(night=True, night_vis=9000, combat=1.5), duration=420, t_open=60, sensors=lambda: [S_RF("Mk 38 26.5 ft", 8.1, 25, "stereo", h=40, per_min=4), replace(R_Mk3(), splash="none")]), LV["5r"], 15),
 ("Komandorski, Salt Lake City (~0.5%)", Scenario("kom", Shooter(GUNS["8in/55 Mk 9"], ((5,0,150),(5,180,150)), 20, speed=30), TARGETS["CA"], 20*KYD, 170, 0, 33, "zigzag", 35, 120, own_policy="zigzag", own_amp=35, own_period=150,
       cond=Conditions(vis=25000, p_obscured=0.3, k_optic=1.5, combat=1.5), duration=2400), LV["5r"], 0.5),
 ("Bismarck's end, Rodney (~11%)", Scenario("rod", Shooter(rod, ((6,0,150),(3,0,150)), 45, speed=20), TARGETS["BB"], 14000, 30, -150, 7, "crippled",
       cond=Conditions(vis=15000, k_optic=1.5, p_obscured=0.3, combat=1.5, spot_confusion=0.3), duration=1500), LV["4"], 11),
]
HISTORY_REF = {   # record, as text [S note §7]
    "tsu": "~40 / 446 12in", "jut": "21 / 1,469", "jutg": "67 / 1,670", "ds": "5-7 / 93", "lrbp": "practice, 4.2-5.4%",
    "u10": "USN 1944 est.", "u20": "USN 1944 est.", "u30": "USN 1944 est.", "nc": ">=13 / 446",
    "wk": "9-20 / 75", "kom": "2-5 / 832", "rod": "~40 / 375 (counted)",
}


# =============================================================================
# 8. Quick analytic layer for the game (no Monte Carlo)
# =============================================================================
def _phi(x):
    return 0.5 * (1 + math.erf(x / math.sqrt(2)))


def quick_hit_probability(R, gun, tc, aspect_deg, sig_fc_r, sig_fc_d, n=9):
    """Expected hits per shell, normal errors, rectangle hitting space.
    sig_fc_* = 1-sigma MPI (fire-control) error in m. Exact in expectation for normal errors:
    per-shell spread is sqrt(sig_fc^2 + sig_D^2) per axis."""
    danger, depth, width = hitting_space(tc, R, gun, aspect_deg)
    sr, sd = gun.sigma_D(R, n)
    s_r, s_d = math.hypot(sig_fc_r, sr), math.hypot(sig_fc_d, sd)
    # range window: shell must land between the near side (minus danger space) and the far side
    pr = _phi((depth / 2) / s_r) - _phi((-depth / 2 - danger) / s_r)
    pd = 2 * _phi(width / 2 / s_d) - 1
    return pr * pd


# =============================================================================
# 9. Tables
# =============================================================================
def sensor_table():
    print("\n=== 2. Range error of ONE reading, 1 sigma random (+ systematic), yd ===")
    print("Optical: delta = 12 arcsec unit of error [S OP 1171], clear day, trained operator.")
    ranges = (2, 5, 10, 15, 20, 25, 30)
    S = [S_eye(), S_stadi(), S_RF("FA3 4.5 ft coinc", 1.37, 24), S_RF("FQ2 9 ft coinc", 2.74, 28),
         S_RF("Zeiss 3 m stereo", 3.0, 23, "stereo"), S_RF("FT24 15 ft coinc", 4.57, 28),
         S_RF("Mk 48 26.5 ft stereo", 8.1, 25, "stereo"), S_RF("10.5 m stereo (Bismarck)", 10.5, 23, "stereo"),
         S_RF("46 ft / 14 m turret", 14.0, 25, "stereo"), S_RF("15 m (Yamato)", 15.0, 30),
         R_Type22(), R_284(), R_Seetakt(), R_Mk3(), R_Mk8(), R_Mk56(), L_laser()]
    c = Conditions()
    print(f"{'sensor':28s}" + "".join(f"{k:>7d}k" for k in ranges) + "   bias@20k")
    for s in S:
        row = ""
        for k in ranges:
            R = k * KYD
            ok = (R < s.rad_max_bb and R > s.rad_min) if s.kind == "radar" else True
            v = s.sigma_range(R, c, TARGETS["BB"]) if ok else None
            row += f"{v / YD:8.0f}" if v is not None else f"{'-':>8}"
        print(f"{s.name:28s}{row}   {s.sigma_bias(20 * KYD) / YD:6.0f}")
    print("\nWhere radar becomes the better rangefinder (random error per reading):")
    for r in (R_Mk8(), R_Mk3(), R_284(), R_Seetakt()):
        cells = []
        for o in (S_RF("15 ft", 4.57, 28), S_RF("26.5 ft", 8.1, 25, "stereo"), S_RF("10.5 m", 10.5, 23, "stereo")):
            R = 1000.0
            while o.sigma_range(R, c, None) < r.sigma_range(R, c, None) and R < 40000:
                R += 100
            cells.append(f"{o.name} {kyd(R):4.1f}k")
        print(f"  {r.name:16s} vs " + ", ".join(cells))


def hitting_space_table():
    print("\n=== 3. Hitting space, yd: range window (danger space + depth) x width, broadside / bow-on ===")
    for gname in ("16in/50 Mk 7", "6in/47 Mk 16", "5in/38 Mk 12"):
        g = GUNS[gname]
        print(f"  {gname}")
        for tn in ("BB", "CA", "DD", "PT/FAC"):
            tc = TARGETS[tn]
            cells = []
            for k in (5, 10, 15, 20, 25, 30):
                R = k * KYD
                if R >= g.table["R"][-1]:
                    cells.append(f"{'-':>13}")
                    continue
                d, dep, w = hitting_space(tc, R, g, 90)
                d0, dep0, w0 = hitting_space(tc, R, g, 0)
                cells.append(f"{(d + dep) / YD:4.0f}x{w / YD:3.0f}/{(d0 + dep0) / YD:3.0f}")
            print(f"    {tn:7s}" + " ".join(cells))
    print("    columns 5, 10, 15, 20, 25, 30 kyd; cell = range-window x width / bow-on range-window")


def quick_table():
    print("\n=== 4. Expected hits per shell (analytic), 16in/50, 9-gun salvo, broadside ===")
    g = GUNS["16in/50 Mk 7"]
    print(f"{'MPI error (1 sigma)':22s}" + "".join(f"{t:>8s}" for t in ("BB10k", "BB20k", "BB30k", "CA20k", "DD10k", "DD20k")))
    for pct in (0.0, 0.5, 1.0, 1.5, 2.0, 3.0):
        row = ""
        for tn, k in (("BB", 10), ("BB", 20), ("BB", 30), ("CA", 20), ("DD", 10), ("DD", 20)):
            R = k * KYD
            p = quick_hit_probability(R, g, TARGETS[tn], 90, pct / 100 * R, 1.5 * MIL * R)
            row += f"{100 * p:7.1f}%"
        print(f"  {pct:4.1f}% of range       {row}")
    print("  (USN July 1944 study for 16in vs BB broadside: 32.7 / 10.5 / 2.7 % at 10 / 20 / 30 kyd [S])")


def convergence(lvl, R0=15 * KYD, tgt_kn=25, turn_deg=45, n=40, tof=25.0, circle=0.0):
    """Prediction error of the computer over one time of flight, steady crossing target that turns at 300 s.
    Returns RMS miss (yd) at chosen times after the first reading."""
    checks = (30, 60, 120, 240, 330, 360, 420, 540)
    errs = {c: [] for c in checks}
    rerr = {c: [] for c in checks}
    cond = Conditions()
    tc = TARGETS["BB"]
    for seed in range(n):
        rng = np.random.default_rng(100 + seed)
        own_p, own_v = np.zeros(2), np.array([20 * KN, 0.0])
        tgt_p = np.array([0.0, R0])
        tgt_c = math.pi                      # crossing right to left: maximum bearing rate
        sv = math.hypot(lvl.prior_kn * KN, tgt_kn * KN * math.radians(lvl.prior_deg))
        est = (tgt_kn * KN + rng.normal(0, lvl.prior_kn * KN), tgt_c + math.radians(rng.normal(0, lvl.prior_deg)))
        sensors = lvl.sensors()
        nxt = [rng.uniform(0, 60 / s.per_min) for s in sensors]
        bias = [rng.normal() for s in sensors]
        trk, q = None, []
        t0 = None
        for t in range(0, 600):
            if t0 is not None and t - t0 == 300 and not circle:
                tgt_c += math.radians(turn_deg)
            if circle and t0 is not None and t - t0 >= 300:
                tgt_c += math.radians(circle)           # steady circle from 300 s, deg/s
            tv = np.array([math.cos(tgt_c), math.sin(tgt_c)]) * tgt_kn * KN
            own_p = own_p + own_v
            tgt_p = tgt_p + tv
            rel = tgt_p - own_p
            R, th = float(np.hypot(*rel)), math.atan2(rel[1], rel[0])
            for i, s in enumerate(sensors):
                if t >= nxt[i]:
                    nxt[i] = t + 60 / s.per_min * rng.uniform(0.7, 1.3)
                    sR = s.sigma_range(R, cond, tc)
                    if sR is None or not s.can_see(R, 90, cond, tc, False):
                        continue
                    zR = R + bias[i] * s.sigma_bias(R) + rng.normal(0, sR)
                    sth = s.sig_brg_mil * MIL if not (s.kind == "radar" and s.bearing_from_optics) else 1.0 * MIL
                    q.append((t + lvl.latency, t, own_p.copy(), zR, sR, th + rng.normal(0, sth), sth))
            while q and q[0][0] <= t:
                _, tm, op, zR, sR, zth, sth = q.pop(0)
                if trk is None:
                    v_est = np.array([math.cos(est[1]), math.sin(est[1])]) * est[0]
                    trk = make_tracker(lvl, tm, zR, zth, sR, sth, op, own_v, v_est, sv)
                    t0 = t
                else:
                    trk.update(tm, op, zR, sR, zth, sth)
            if t0 is not None and (t - t0) in errs:
                Rp, thp = trk.rel_at(t, tof, own_p, own_v)
                if circle and t - t0 >= 300:   # truth follows the arc over the time of flight
                    w, v = math.radians(circle), tgt_kn * KN
                    arc = np.array([v / w * (math.sin(tgt_c + w * tof) - math.sin(tgt_c)),
                                    v / w * (-math.cos(tgt_c + w * tof) + math.cos(tgt_c))])
                    true = tgt_p + arc - (own_p + own_v * tof)
                else:
                    true = tgt_p + tv * tof - (own_p + own_v * tof)
                pred = np.array([Rp * math.cos(thp), Rp * math.sin(thp)])
                errs[t - t0].append(float(np.hypot(*(pred - true))))
                if isinstance(trk, CartTracker):
                    rv = float(np.hypot(trk.x[2] - tv[0], trk.x[3] - tv[1]))
                elif isinstance(trk, ManoeuvreTracker):
                    trk._pred(t)
                    rv = float(np.hypot(*(trk.velocity() - tv)))
                else:
                    trk._pred(t)
                    tr_rd = (tv - own_v) @ np.array([math.cos(th), math.sin(th)])
                    tr_bd = ((tv - own_v) @ np.array([-math.sin(th), math.cos(th)])) / R
                    rv = math.hypot(trk.r[1] - tr_rd, R * (trk.b[1] - tr_bd))
                rerr[t - t0].append(rv / KN)
    rms = lambda d, k: {c: math.sqrt(np.mean(np.square(v))) / k for c, v in d.items() if v}
    return rms(errs, YD), rms(rerr, 1.0)


def convergence_table():
    print("\n=== 5. The computer: predicted-position error over a 25 s time of flight, RMS yd ===")
    print("Steady BB crossing at 25 kn, 15 kyd, own ship 20 kn; target turns 45 deg at 300 s.")
    print("Error excludes ballistics and laying: it is what the rangekeeping alone gets wrong.")
    print("Upper block: miss in yd (includes rangefinder bias, which only spotting removes).")
    print("Lower block: error in the target's motion as the computer holds it, kn (= 'rate' error).")
    hdr = f"{'level':46s}{'30s':>6}{'60s':>6}{'120s':>6}{'240s':>6} |{'+30':>6}{'+60':>6}{'+120':>6}{'+240':>6}"
    res = [(l, convergence(l)) for l in LEVELS]
    for j, fmt in ((0, "{:6.0f}"), (1, "{:6.1f}")):
        print(hdr)
        for l, c in res:
            c = c[j]
            print(f"{l.name:46s}" + "".join(fmt.format(c.get(k, float('nan'))) for k in (30, 60, 120, 240)) + " |"
                  + "".join(fmt.format(c.get(k, float('nan'))) for k in (330, 360, 420, 540)))


LADDER_SCN = [
    ("Day 16 kyd, zigzag", dict(R0=16 * KYD, tgt_policy="zigzag", tgt_amp=20, tgt_period=240, cond=Conditions(combat=1.5))),
    ("Day 25 kyd, steady", dict(R0=25 * KYD, cond=Conditions(vis=35000, combat=1.5))),
    ("Haze+smoke 13 kyd", dict(R0=13 * KYD, tgt_policy="zigzag", tgt_amp=20, tgt_period=240,
                               cond=Conditions(vis=13000, k_optic=1.8, p_obscured=0.3, spot_k=1.5, combat=1.5))),
    ("Night 12 kyd", dict(R0=12 * KYD, tgt_policy="zigzag", tgt_amp=20, tgt_period=240,
                          cond=Conditions(night=True, night_vis=4000, combat=1.5))),
]


def ladder_table(n=40):
    print("\n=== 6. The ladder: same ship, same gun (8 x 15in/42, salvo every 40 s), 15 min, combat conditions ===")
    print("Target BB at 25 kn, bearing 90 deg, parallel course. cell = hit % / straddle % / median s to first hit")
    print(f"{'level':46s}" + "".join(f"{nm:>24s}" for nm, _ in LADDER_SCN))
    sh = Shooter(GUNS["15in/42 Mk I"], ((4, 0, 150), (4, 180, 150)), 40)
    for l in LEVELS:
        cells = ""
        for nm, kw in LADDER_SCN:
            scn = Scenario(nm, sh, TARGETS["BB"], tgt_brg=90, tgt_course=0, tgt_speed=25, duration=900, **kw)
            r = run(scn, l, n)
            th = f"{r['t_hit']:.0f}" if r["t_hit"] is not None else "-"
            cells += f"{r['pct']:8.1f}/{r['straddle_pct']:3.0f}/{th:>5s}  " if r["shells"] else f"{'no solution':>24s}"
        print(f"{l.name:46s}{cells}")


def mpi_table(n=60):
    print("\n=== 7. MPI error by salvo number, RMS % of range (range axis) - drop-in numbers for a simple game model ===")
    print("Scenario: day 16 kyd, zigzag, combat (as table 6, column 1).")
    sh = Shooter(GUNS["15in/42 Mk I"], ((4, 0, 150), (4, 180, 150)), 40)
    nm, kw = LADDER_SCN[0]
    print(f"{'level':46s}{'salvo 1':>9}{'2-3':>7}{'4-8':>7}{'9+':>7}  defl 9+ (mil)")
    for l in LEVELS:
        scn = Scenario(nm, sh, TARGETS["BB"], tgt_brg=90, tgt_course=0, tgt_speed=25, duration=900, **kw)
        b = {0: [], 1: [], 2: [], 3: []}
        bd = []
        for k in range(n):
            o = engage(scn, l, seed=500 + k)
            for i, (mx, my) in enumerate(o["mpi"]):
                key = 0 if i == 0 else 1 if i < 3 else 2 if i < 8 else 3
                b[key].append(mx / (16 * KYD))
                if key == 3:
                    bd.append(my / (16 * KYD) / MIL)
        cells = "".join(f"{100 * math.sqrt(np.mean(np.square(v))):7.2f}" if v else f"{'-':>7}" for v in b.values())
        print(f"{l.name:46s}  {cells}   {math.sqrt(np.mean(np.square(bd))) if bd else float('nan'):5.1f}")


def history_table(n=40):
    print("\n=== 8. Calibration against the record ===")
    print(f"{'case':42s}{'level':8s}{'model %':>8}{'record':>22}{'straddle %':>11}{'1st strad salvo':>16}")
    for name, scn, lvl, ref in HISTORY:
        r = run(scn, lvl, n)
        print(f"{name.split(' (')[0]:42s}{lvl.name.split()[0]:8s}{r['pct']:8.1f}{HISTORY_REF[scn.name]:>22s}"
              f"{r['straddle_pct']:11.0f}{(r['n_straddle'] or float('nan')):16.0f}")


def geometry_table():
    print("\n=== 9. Geometry limits: bearing rate (deg/s) a mount must follow ===")
    print("Target crossing at the given speed, own ship steady; add own turn rate (~1 deg/s BB, 3 deg/s DD) on top.")
    print(f"{'range':>8}" + "".join(f"{v:>8d}kn" for v in (15, 30, 45)))
    for k in (1, 2, 5, 10, 20):
        R = k * KYD
        print(f"{k:6d}k " + "".join(f"{math.degrees(v * KN / R):10.2f}" for v in (15, 30, 45)))
    print("Mount train rates [S]: 46 cm 2, RN 14-16in 2, USN 16in 4, 38 cm 5, 8in 6.7, 6in 10, 5in/38 25, OTO 76 60 deg/s")
    print("\nGuns bearing vs relative bearing (0 = bow):")
    lay = {"BB 3x3 (A,B fwd; X aft), arcs +-150": ((3, 0, 150), (3, 0, 150), (3, 180, 150)),
           "BB 4x2 (A,B / X,Y), arcs +-150": ((4, 0, 150), (4, 180, 150)),
           "Nelson 3x3 all forward, arcs +-150": ((9, 0, 150),),
           "DD 5x1 (2 fwd, 3 aft), arcs +-150": ((2, 0, 150), (3, 180, 150))}
    brgs = (0, 15, 30, 45, 90, 135, 150, 165, 180)
    print(f"{'':40s}" + "".join(f"{b:>5d}" for b in brgs))
    for k, v in lay.items():
        print(f"{k:40s}" + "".join(f"{_guns_bearing(v, b):5d}" for b in brgs))


def spotting_table():
    print("\n=== 10. Optical spotting: 1-sigma error judging a splash over/short, yd ===")
    print("sigma = 4e-5 rad * R^2 / height of eye  [INFERRED; 500 yd obvious at 12 kyd, barely seen at 19 kyd, NavPers]")
    print(f"{'eye height':>12}" + "".join(f"{k:>7d}k" for k in (5, 10, 15, 20, 25, 30)))
    for h in (10, 18, 30, 40, 1500):
        lab = f"{h} m" if h < 1000 else "aircraft*"
        print(f"{lab:>12}" + "".join(f"{4e-5 * (k * KYD) ** 2 / h / YD:8.0f}" for k in (5, 10, 15, 20, 25, 30)))
    print("  *aircraft spotting is modelled as a direct ~70 m reading instead (plan view), 30 s radio delay")
    print("Radar splash spotting [S]: Mk 3 / 284 range only to ~20 kyd; Mk 8 Mod 3 range to 35 kyd; Mk 13 range and")
    print("deflection to 42 kyd; post-war B-scan, spots still entered by hand (closed loop only on CIWS).")


TURN_SCN = [
    ("steady", dict(tgt_policy="steady")),
    ("zigzag +-20/4min", dict(tgt_policy="zigzag", tgt_amp=20, tgt_period=240)),
    ("circle 0.5 deg/s", dict(tgt_policy="circle", tgt_turn=0.5, tgt_turn_start=240)),
    ("circle 1.0 deg/s", dict(tgt_policy="circle", tgt_turn=1.0, tgt_turn_start=240)),
]


def turning_table(n=40, levels=("3", "3b", "4", "4t", "4j", "5", "6", "7", "8")):
    print("\n=== 11. Target motion vs computer: hit % (and RMS MPI error, yd) ===")
    print("8 x 15in/42 every 40 s at a BB, 25 kn, 16 kyd start, 15 min, combat 1.5. Circles start at 240 s,")
    print("after the first solution has settled on a straight course. Cell = hit % / RMS range MPI error yd")
    print(f"{'level':46s}" + "".join(f"{nm:>20s}" for nm, _ in TURN_SCN))
    sh = Shooter(GUNS["15in/42 Mk I"], ((4, 0, 150), (4, 180, 150)), 40)
    for k in levels:
        l = LV[k]
        cells = ""
        for nm, kw in TURN_SCN:
            scn = Scenario(nm, sh, TARGETS["BB"], 16 * KYD, tgt_brg=90, tgt_course=0, tgt_speed=25, duration=900,
                           cond=Conditions(combat=1.5), **kw)
            hits = shells = 0
            mx = []
            for j in range(n):
                o = engage(scn, l, seed=900 + j)
                hits += o["hits"]; shells += o["shells"]
                mx += [m[0] for m, tt in zip(o["mpi"], o["mpi_t"]) if tt > 300]
            rms = math.sqrt(np.mean(np.square(mx))) / YD if mx else float("nan")
            cells += f"{100 * hits / max(shells, 1):12.1f} / {rms:5.0f}"
        print(f"{l.name:46s}{cells}")


def circle_convergence_table(levels=("3", "3b", "4", "4t", "5", "6", "7", "8")):
    print("\n=== 12. Prediction error against a steady circle, RMS yd over a 25 s time of flight ===")
    print("As table 5 but at 300 s the target puts the helm over and circles at 1 deg/s (25 kn: ~800 yd radius).")
    print(f"{'level':46s}{'straight 240s':>14}{'+30':>7}{'+60':>7}{'+120':>7}{'+240':>7}")
    for k in levels:
        e, _ = convergence(LV[k], circle=1.0, n=30)
        print(f"{LV[k].name:46s}{e[240]:14.0f}" + "".join(f"{e[c]:7.0f}" for c in (330, 360, 420, 540)))


if __name__ == "__main__":
    quick = len(sys.argv) > 1 and sys.argv[1] == "quick"
    ballistics_table()
    sensor_table()
    hitting_space_table()
    quick_table()
    geometry_table()
    spotting_table()
    if not quick:
        convergence_table()
        history_table()
        ladder_table()
        mpi_table()
        circle_convergence_table()
        turning_table()
