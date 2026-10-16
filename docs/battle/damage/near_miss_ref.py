"""near_miss_ref.py -- reference for 15_near_misses.md (Fleetwright damage research).

A shell that misses but bursts close aboard. Three ways it hurts, in the order the note takes them:

  1. Splinters: a burst on the water's surface throws its fragments into the side and upper works.
     The fragment field is 13_fragmentation.md's, imported from fragmentation_ref.py (Gurney, Mott,
     drag, THOR, the side band, the nose spray, the lean). The near miss adds the water: everything that
     leaves the burst downward is lost, and a burst a few cm down chokes what goes up; and the ship's
     geometry (side, upper works, funnels, exposed crew) with the forward-leaning spray.
  2. Shock: a burst under the water loads the hull like a small mine. Indexed by a shock factor
     (sqrt(W)/R, lb and ft) with the surface cut-off that makes surface bursts weak, banded against
     the British 1939-41 bomb near-miss record.
  3. Short shells that reach the hull under water: the existing P6 water run (WaterRun.cs). Here only
     the branch point: does the fuze fire before the shell reaches the hull, and where.

Pure Python; needs fragmentation_ref.py beside it. `python near_miss_ref.py` prints every table quoted
in the note (~2 min).
Units SI unless named. Tags: [SRC] sourced, [FIT] fitted here, [EST] judgement, [UNCERTAIN] thin sources.
"""
import math
import random
from dataclasses import dataclass, field

# ------------------------------------------------------------------------------------------------
# 1. The fragment field is 13's: imported, not rebuilt (one implementation)
import fragmentation_ref as fr   # same folder: docs/battle/damage

RHO_AIR, RHO_W = 1.225, 1025.0
C_WATER = 1500.0      # m/s
E_CASUALTY = 80.0     # J: the classic incapacitation criterion for a fragment (58 ft-lb; 14 uses 79 J) [SRC]
A_PERSON = 0.5        # m^2, presented area of a standing man [EST]
MIN_FRAG = 1e-3       # kg: 13 counts fragments of 1 g and up
RE = {"Explosive D": 0.95, "TNA": 1.0, "TNT": 1.0, "Lyddite": 1.17, "Shimose": 1.17, "Shellite": 1.0,
      "Black powder": 0.55, "Amatol 60/40": 1.0}          # 12's filler.re [SRC/EST there]


# ------------------------------------------------------------------------------------------------
# 2. Shells: 13's shell rows plus what a miss needs (fuze, striking speed, angle of fall)
@dataclass
class Shell:
    name: str
    body: object        # fragmentation_ref.Shell
    fuze: str           # 'nose' (bursts on the water) or 'base' (delay runs from the water)
    delay: float        # s: base fuzes' delay; nose fuzes' function time on water (fuze.water_ms / 1000)
    v: float            # striking speed at the scenario's range, m/s [EST from the range tables]
    fall: float         # angle of fall at that range, deg
    diving: bool = False

    @property
    def d(self):
        return self.body.calibre_mm / 1000

    @property
    def w_tnt(self):
        return self.body.filler_kg * RE.get(self.body.explosive, 1.0)


def _fr(name):
    return fr.SHELL[name]


F28 = fr.Shell("28cm HE L/4.4 (Graf Spee)", 283, 300.0, 23.3, "TNT", "HE", "forged carbon")   # [UNCERTAIN filler]
T91 = fr.Shell("46cm T91 AP", 460, 1460.0, 33.85, "TNA", "AP", "Ni-Cr AP body")

SHELLS = {
    "5in38 AAC 9km":    Shell("5in38 AAC 9km",    _fr("5in/38 AAC Mk 35"), "nose", 0.0005, 380, 18),
    "6in HC 13km":      Shell("6in HC 13km",      _fr("6in/47 HC Mk 34"),  "nose", 0.0005, 420, 22),
    "20cm common 15km": Shell("20cm common 15km", _fr("IJN 20cm common"),  "nose", 0.0005, 450, 22),
    "28cm HE 16km":     Shell("28cm HE 16km",     F28,                     "nose", 0.0005, 480, 22),
    "16in HC 15km":     Shell("16in HC 15km",     _fr("16in HC Mk 13"),    "nose", 0.0005, 560, 14),
    "46cm T0 HE 15km":  Shell("46cm T0 HE 15km",  _fr("46cm T0 HE"),       "nose", 0.0005, 560, 15),
    "16in AP 15km":     Shell("16in AP 15km",     _fr("16in AP Mk 8"),     "base", 0.033, 600, 14),
    "46cm T91 AP 15km": Shell("46cm T91 AP 15km", T91,                     "base", 0.4, 560, 15, True),
}


# ------------------------------------------------------------------------------------------------
# 3. The spray's groups (13 §2.5): side band, nose (ogive) spray, tail spray
@dataclass
class Group:
    name: str
    v0: float
    bins: list          # [(m kg, count)]
    pol_lo: float       # polar band from the nose, deg
    pol_hi: float


def groups(s: Shell):
    b = fr.burst(s.body)
    bins = [(m, n) for m, n in fr.mass_bins(b, nb=30) if m >= MIN_FRAG]
    h = fr.BAND_HALF_DEG
    out = [Group("side", b.v_body, bins, 90 - h, 90 + h)]
    if b.ogive_m:
        k = b.ogive_m / b.body_m
        out.append(Group("nose", b.v_ogive, [(m, n * k) for m, n in bins], 30.0, 80.0))   # 13 §2.5 [EST]
    if b.tail_m:
        k = b.tail_m / b.body_m
        out.append(Group("tail", b.v_tail, [(m, n * k) for m, n in bins], 140.0, 180.0))  # [EST]
    return out


def group_count(g):
    return sum(n for _, n in g.bins)


def presented_area(m):
    """13's presented area, A = (m/k)^(2/3) with k = 660 grains/in^3, in m^2."""
    return fr._area_in2(m * 15432.358) * fr.IN ** 2


def static_rays(s: Shell, n_per=3000, seed=1):
    """[(unit vector in the shell's frame (z = nose), group, weight)]: each group sampled uniformly in solid
    angle over its polar band; weight = the group's count share per ray."""
    rnd = random.Random(seed)
    out = []
    for g in groups(s):
        c1, c2 = math.cos(math.radians(g.pol_lo)), math.cos(math.radians(g.pol_hi))
        for _ in range(n_per):
            cp = rnd.uniform(c2, c1)
            sp = math.sqrt(max(0.0, 1 - cp * cp))
            ph = rnd.uniform(0, 2 * math.pi)
            out.append(((sp * math.cos(ph), sp * math.sin(ph), cp), g, 1.0 / n_per))
    return out


def axis_frame(s: Shell, azimuth_deg):
    th = math.radians(s.fall)
    az = math.radians(azimuth_deg)
    ax = (math.cos(az) * math.cos(th), math.sin(az) * math.cos(th), -math.sin(th))   # the nose
    e1 = (-math.sin(az), math.cos(az), 0.0)
    e2 = (ax[1] * e1[2] - ax[2] * e1[1], ax[2] * e1[0] - ax[0] * e1[2], ax[0] * e1[1] - ax[1] * e1[0])
    return ax, e1, e2


def to_lab(rays, s: Shell, azimuth_deg, v_shell):
    """Lab-frame directions and speeds: the shell's velocity plus each group's Gurney kick (13 §2.5's lean)."""
    ax, e1, e2 = axis_frame(s, azimuth_deg)
    out = []
    for (a, b, c), g, w in rays:
        v = [v_shell * ax[k] + g.v0 * (a * e1[k] + b * e2[k] + c * ax[k]) for k in range(3)]
        sp = math.sqrt(sum(x * x for x in v))
        out.append(((v[0] / sp, v[1] / sp, v[2] / sp), sp, g, w))
    return out


def lab_density(s: Shell, v_shell, axis, sdir):
    """What the game evaluates per part, no rays. For each group: lambda = V u.s + sqrt(v0^2 - V^2 (1-(u.s)^2)),
    n = (lambda s - V u)/v0, dOmega_static/dOmega_lab = lambda^2 / (v0 (lambda - V u.s)).
    Returns [(group, fragments per steradian of lab direction, lab speed)]."""
    us = sum(a * b for a, b in zip(axis, sdir))
    out = []
    for g in groups(s):
        disc = g.v0 ** 2 - v_shell ** 2 * (1 - us * us)
        if disc <= 0:
            continue
        lam = v_shell * us + math.sqrt(disc)
        if lam <= 0:
            continue
        n = [(lam * sc - v_shell * ac) / g.v0 for sc, ac in zip(sdir, axis)]
        psi = math.degrees(math.acos(max(-1.0, min(1.0, sum(a * b for a, b in zip(n, axis))))))
        if not (g.pol_lo <= psi <= g.pol_hi):
            continue
        band_sr = 2 * math.pi * (math.cos(math.radians(g.pol_lo)) - math.cos(math.radians(g.pol_hi)))
        jac = lam * lam / (g.v0 * (lam - v_shell * us))
        out.append((g.name, group_count(g) / band_sr * jac, lam))
    return out


# ------------------------------------------------------------------------------------------------
# 5. Targets: axis-aligned panels. x along the keel (bow +), y abeam, z up from the waterline.
@dataclass
class Panel:
    name: str
    axis: str          # 'x', 'y' or 'z': the panel lies in the plane axis = c
    c: float
    lo: tuple          # bounds of the other two coordinates, in order (x,y,z minus axis)
    hi: tuple
    t_mm: float        # plate; 0 = not a plate (a crew zone: counted, transparent)
    group: str


@dataclass
class Ship:
    name: str
    L: float
    B: float
    T: float
    F: float
    panels: list = field(default_factory=list)
    crew: dict = field(default_factory=dict)     # crew-zone group -> people per m^2 of its panel
    profile: list = field(default_factory=list)  # [(x0, x1, top z)] for the hit test


def box_sides(name, x0, x1, y0, y1, z0, z1, t, group):
    return [Panel(name + "+y", "y", y1, (x0, z0), (x1, z1), t, group),
            Panel(name + "-y", "y", y0, (x0, z0), (x1, z1), t, group),
            Panel(name + "+x", "x", x1, (y0, z0), (y1, z1), t, group),
            Panel(name + "-x", "x", x0, (y0, z0), (y1, z1), t, group),
            Panel(name + "top", "z", z1, (x0, y0), (x1, y1), t, group)]


def fletcher():
    """A Fletcher-like destroyer, 114 x 12 x 4 m, freeboard 5 m amidships. Side 8 mm (13's T8), deckhouses 6.4 mm,
    funnels 4.8 mm. [EST]"""
    L, B, T, F = 114.0, 12.0, 4.0, 5.0
    s = Ship("Fletcher-like DD", L, B, T, F)
    hb = B / 2
    for sgn, tag in ((1, "+"), (-1, "-")):
        s.panels.append(Panel("side_wl" + tag, "y", sgn * hb, (-L / 2, 0.0), (L / 2, 1.0), 8.0, "waterline"))
        s.panels.append(Panel("side_up" + tag, "y", sgn * hb, (-L / 2, 1.0), (L / 2, F), 8.0, "hull side"))
        s.panels.append(Panel("crew_deck" + tag, "y", sgn * (hb - 0.5), (-L / 2 + 6, F), (L / 2 - 6, F + 1.8),
                              0.0, "exposed crew"))
    s.panels.append(Panel("deck", "z", F, (-L / 2, -hb), (L / 2, hb), 6.4, "deck"))
    s.panels += box_sides("fwd_house", 18, 38, -hb + 1.5, hb - 1.5, F, 12.0, 6.4, "upper works")
    s.panels += box_sides("aft_house", -30, -12, -hb + 2.0, hb - 2.0, F, 8.0, 6.4, "upper works")
    s.panels += box_sides("fwd_funnel", 4, 8, -1.3, 1.3, F, 14.0, 4.8, "funnels")
    s.panels += box_sides("aft_funnel", -9, -5, -1.3, 1.3, F, 13.0, 4.8, "funnels")
    s.panels.append(Panel("bridge_crew+", "y", 4.0, (24, 12.0), (34, 14.0), 0.0, "bridge crew"))
    s.panels.append(Panel("bridge_crew-", "y", -4.0, (24, 12.0), (34, 14.0), 0.0, "bridge crew"))
    s.crew = {"exposed crew": 0.28, "bridge crew": 0.6}   # ~100 exposed at GQ along both deck edges [EST]
    s.profile = [(-L / 2, -30, F), (-30, -12, 8.0), (-12, 18, F), (18, 38, 12.0), (38, L / 2, F)]
    return s


def flat_hull():
    """13's T8 target: an 8 mm mild-steel side 7 m high and 110 m long, nothing else."""
    s = Ship("13 T8 flat side", 110.0, 12.0, 4.0, 7.0)
    for sgn, tag in ((1, "+"), (-1, "-")):
        s.panels.append(Panel("side" + tag, "y", sgn * 6.0, (-55.0, 0.0), (55.0, 7.0), 8.0, "hull side"))
    s.profile = [(-55.0, 55.0, 7.0)]
    return s


def leander():
    """A Leander-like light cruiser, 160 x 17 x 5.5 m, freeboard 7 m. DCT at 22-25 m. [EST]"""
    L, B, T, F = 160.0, 17.0, 5.5, 7.0
    s = Ship("Leander-like CL", L, B, T, F)
    hb = B / 2
    for sgn, tag in ((1, "+"), (-1, "-")):
        s.panels.append(Panel("side_wl" + tag, "y", sgn * hb, (-L / 2, 0.0), (L / 2, 1.0), 12.7, "waterline"))
        s.panels.append(Panel("side_up" + tag, "y", sgn * hb, (-L / 2, 1.0), (L / 2, F), 12.7, "hull side"))
        s.panels.append(Panel("crew_deck" + tag, "y", sgn * (hb - 0.5), (-L / 2 + 8, F), (L / 2 - 8, F + 1.8),
                              0.0, "exposed crew"))
    s.panels.append(Panel("deck", "z", F, (-L / 2, -hb), (L / 2, hb), 9.5, "deck"))
    s.panels += box_sides("fwd_house", 22, 50, -hb + 2.5, hb - 2.5, F, 18.0, 9.5, "upper works")
    s.panels += box_sides("dct", 34, 37.5, -1.75, 1.75, 21.0, 24.0, 6.4, "DCT")
    s.crew = {"exposed crew": 0.15}
    s.profile = [(-L / 2, 22, F), (22, 50, 18.0), (50, L / 2, F)]
    return s


def ray_panel(p0, d, pan: Panel):
    i = "xyz".index(pan.axis)
    if abs(d[i]) < 1e-12:
        return None
    s = (pan.c - p0[i]) / d[i]
    if s <= 1e-6:
        return None
    others = [k for k in range(3) if k != i]
    for k, lo, hi in zip(others, pan.lo, pan.hi):
        q = p0[k] + s * d[k]
        if q < lo or q > hi:
            return None
    return s, abs(d[i])        # distance, cosine of obliquity


# ------------------------------------------------------------------------------------------------
# 6. One surface burst's splinters on one ship
_RAYS = {}


def rays_for(s: Shell, n_per=2500):
    key = (s.body.name, n_per)
    if key not in _RAYS:
        _RAYS[key] = static_rays(s, n_per)
    return _RAYS[key]


def splinters(s: Shell, ship: Ship, burst, azimuth_deg, v_keep=0.95, mat="mild", n_per=2500):
    """Expected perforations per panel group, crew casualties and waterline holes from a burst at `burst`
    (x, y, z; z < 0 is below the surface). v_keep: share of the striking speed the shell still has when the
    fuze fires [EST 0.95]."""
    p0 = burst
    z_b = burst[2]
    res, crew_hits = {}, {}
    holes_wl = hole_area = 0.0
    for d, sp, g, w in to_lab(rays_for(s, n_per), s, azimuth_deg, s.v * v_keep):
        if d[2] <= 0:
            continue                                    # into the sea: stopped within a metre (13 §2.3)
        s_w = -z_b / d[2] if z_b < 0 else 0.0          # path through the water above the burst
        best, crossings = None, []
        for pan in ship.panels:
            hit = ray_panel(p0, d, pan)
            if hit is None:
                continue
            if pan.t_mm == 0:
                crossings.append((hit[0], hit[1], pan))
            elif best is None or hit[0] < best[0]:
                best = (hit[0], hit[1], pan)
        speeds = None
        for dist, cosob, pan in crossings:
            if best is not None and dist > best[0]:
                continue
            i_ax = "xyz".index(pan.axis)
            first = [k for k in range(3) if k != i_ax][0]
            cell = int((p0[first] + dist * d[first] - pan.lo[0]) // 2.0)
            n80 = 0.0
            for m, n in g.bins:
                v = sp * math.exp(-s_w / (fr.L1_WATER * m ** (1 / 3)) - max(dist - s_w, 0) / (fr.L1_AIR * m ** (1 / 3)))
                if 0.5 * m * v * v >= E_CASUALTY:
                    n80 += n
            key = (pan.name, cell)
            crew_hits[key] = crew_hits.get(key, 0.0) + n80 * w
        if best is None:
            continue
        dist, cosob, pan = best
        obl = math.degrees(math.acos(min(1.0, cosob)))
        perf = 0.0
        for m, n in g.bins:
            v = sp * math.exp(-s_w / (fr.L1_WATER * m ** (1 / 3)) - max(dist - s_w, 0) / (fr.L1_AIR * m ** (1 / 3)))
            if v > fr.thor_v50(m, pan.t_mm, mat, min(obl, 80.0)):
                perf += n
                if pan.group == "waterline":
                    hole_area += n * w * presented_area(m) * 1.3      # ragged hole ~1.3x presented [EST]
        res[pan.group] = res.get(pan.group, 0.0) + perf * w
        if pan.group == "waterline":
            holes_wl += perf * w
    # casualties per 2 m cell of each crew zone: people there x P(at least one fragment >= 80 J) [EST]
    cas = 0.0
    pans = {p.name: p for p in ship.panels}
    for (pname, cell), nfr in crew_hits.items():
        p = pans[pname]
        area = 2.0 * (p.hi[1] - p.lo[1])
        cas += ship.crew.get(p.group, 0.0) * area * (1 - math.exp(-nfr / area * A_PERSON))
    res["casualties"] = cas
    res["wl_holes"] = holes_wl
    res["wl_hole_cm2"] = hole_area * 1e4
    return res


def surface_depth(s: Shell):
    return -s.v * s.delay * math.sin(math.radians(s.fall))


# ------------------------------------------------------------------------------------------------
# 7. Underwater shock (Cole; the shock factor; the surface cut-off)
def cole(w_kg, r_m):
    """Peak pressure (MPa) and decay constant (s) for TNT at r, Cole's similitude (lb/ft fits)."""
    w_lb, r_ft = w_kg / 0.45359, r_m / 0.3048
    lam = r_ft / w_lb ** (1 / 3)
    p_psi = 21600 * lam ** -1.13
    theta = 6.0e-5 * w_lb ** (1 / 3) * lam ** 0.18
    return p_psi * 6.895e-3, theta


def shock_factor(w_kg, burst, ship: Ship):
    """Near-miss shock factor at the nearest point of the hull below the waterline:
    NSF = sqrt(W_lb)/R_ft * (1 + sin a)/2 * sqrt(1 - exp(-2 dt/theta)). a: the angle of the line from the
    burst up to the hull above the horizontal (0 alongside, 90 under the keel). dt: the time until the
    reflection from the surface cuts the pulse off (its energy flux goes as 1 - exp(-2t/theta))."""
    x, y, z = burst
    hx, hy = ship.L / 2, ship.B / 2
    px = min(max(x, -hx), hx)
    py = min(max(y, -hy), hy)
    pz = min(max(z, -ship.T), -0.5)               # nearest wetted point (at least 0.5 m down) [EST]
    r = math.dist((x, y, z), (px, py, pz))
    r = max(r, 0.5)
    sin_a = max(0.0, (pz - z) / r)
    r_img = math.dist((x, y, -z), (px, py, pz))
    dt = (r_img - r) / C_WATER
    _, theta = cole(w_kg, r)
    cut = math.sqrt(1 - math.exp(-2 * dt / theta))
    sf = math.sqrt(w_kg / 0.45359) / (r / 0.3048)
    return sf * (1 + sin_a) / 2 * cut, dict(r=r, sin_a=sin_a, cut=cut, raw=sf)


# Bands, calibrated on the bomb record (table in main): (upper NSF, what happens)
SHOCK_BANDS = [
    (0.03, "nothing"),
    (0.08, "fragile gear: lights, breakers trip, asdic/radar/gyro; cast-iron fittings crack"),
    (0.20, "machinery mounts and castings: turbine and dynamo feet, blades, pipes; seams weep"),
    (0.45, "hull indented or corrugated, seams open, slow flooding; shafts may be strained"),
    (1.00, "plating and frames badly buckled, moderate flooding, shafts/thrust out of line"),
    (99.0, "plating split or holed alongside: a hole, flooding as from a hit"),
]


def band(nsf):
    for hi, txt in SHOCK_BANDS:
        if nsf < hi:
            return txt
    return SHOCK_BANDS[-1][1]


# British near-miss bombs 1939-41 (US summary of British war damage). Charges are the bombs' usual filling,
# TNT-equivalent: SC50 25 kg, SC250 130, SC500 220 [SRC typical fillings; which bomb fell is the report's estimate].
BOMB_CASES = [
    # ship, charge kg, distance ft (as reported), damage (abridged from the report)
    ("Fame (DD), 200 lb inst., 5 ft",          45, 5,   "severe structural and splinter damage, flooding aft"),
    ("Kelvin (DD), 250 kg, 10 ft",            130, 10,  "outer bottom badly buckled, shaft distorted, turbine foot broke"),
    ("Napier (DD), 250 kg, 10 ft",            130, 10,  "turbine feet fractured, fuel pumps broken, 24 kn"),
    ("Nizam (DD), 250 kg, 15 ft",             130, 15,  "thrust block out, bulkheads buckled, turbine feet, 26 kn"),
    ("Somali (DD), 1000 lb, 30 ft",           220, 30,  "plating fractured, hull heavily indented, flooding, 11.5 kn"),
    ("Montrose (DD), 500 lb, 30 ft",          110, 30,  "hull corrugated below WL, castings cracked, steering out"),
    ("Quorn (DD), ~250 kg, 60 ft",            130, 60,  "four turbine feet broke"),
    ("Furious (CV), 250 lb, 45 ft",            50, 45,  "turbine blades cracked, one shaft out"),
    ("Sheffield (CL), 50-250 lb, 75 ft",       40, 75,  "an asdic, some machinery and electrical gear"),
    ("Winchester (DD), ? , 150-300 ft",       130, 200, "severe machinery damage from cast-iron failures"),
]


def shock_reach(w_kg, depth_m, nsf_target, ship):
    """Horizontal distance from the side at which a burst at depth_m gives nsf_target."""
    lo, hi = 0.0, 300.0
    for _ in range(50):
        mid = (lo + hi) / 2
        nsf, _ = shock_factor(w_kg, (0.0, ship.B / 2 + mid, -depth_m), ship)
        if nsf > nsf_target:
            lo = mid
        else:
            hi = mid
    return lo


# ------------------------------------------------------------------------------------------------
# 8. Where a missing shell bursts
def water_burst(s: Shell, impact_xy, azimuth_deg, ship: Ship, fuze_on_water=True):
    """('surface', point) for a nose fuze; for a base fuze the delay runs from the water (P6's switch on):
    ('underwater_hit', point) if the run reaches the hull first, ('underwater', point) if it fires short,
    ('none', None) if the fuze never starts."""
    if s.fuze == "nose":
        return "surface", (impact_xy[0], impact_xy[1], surface_depth(s))
    if not fuze_on_water:
        return "none", None
    L = (50 if s.diving else 25) * s.d
    x_f = L * math.log(1 + s.v * s.delay / L)
    th, az = math.radians(s.fall), math.radians(azimuth_deg)
    d = (math.cos(az) * math.cos(th), math.sin(az) * math.cos(th), -math.sin(th))
    hx, hy = ship.L / 2, ship.B / 2
    steps = 200
    for i in range(1, steps + 1):
        q = x_f * i / steps
        p = (impact_xy[0] + d[0] * q, impact_xy[1] + d[1] * q, d[2] * q)
        if abs(p[0]) <= hx and abs(p[1]) <= hy and p[2] >= -ship.T:
            return "underwater_hit", p
    return "underwater", (impact_xy[0] + d[0] * x_f, impact_xy[1] + d[1] * x_f, d[2] * x_f)


# ------------------------------------------------------------------------------------------------
# 9. A salvo on a target: hits, near misses, and what the misses do
def top_at(ship, x):
    for x0, x1, z in ship.profile:
        if x0 <= x <= x1:
            return z
    return ship.F


def classify(ship, s, xi, yi):
    """Broadside target, fire from +y (shell flies toward -y). Returns ('hit'|'short'|'over'|'end', d)."""
    hb = ship.B / 2
    if abs(xi) > ship.L / 2:
        return "end", abs(xi) - ship.L / 2
    cot = 1 / math.tan(math.radians(s.fall))
    if yi > hb:
        return "short", yi - hb
    if yi >= -hb - top_at(ship, xi) * cot:
        return "hit", 0.0
    return "over", -hb - yi


DISTS = [0.5, 2, 4, 7, 10, 15, 20, 30, 45, 60, 80, 110]
XS_ALONG = (-40.0, -21.0, 0.0, 6.0, 28.0, 45.0)     # burst positions along the ship, averaged


def effect_tables(s, ship, dists):
    tab = {}
    for kind in ("short", "over", "end"):
        rows = []
        xs = XS_ALONG if kind != "end" else (0.0,)
        for dd in dists:
            row = dict(perf=0.0, upper=0.0, light=0.0, wl=0.0, cas=0.0, nsf=0.0, uwhit=0.0)
            for x in xs:
                if kind == "short":
                    pt, az = (x, ship.B / 2 + dd), -90.0
                elif kind == "over":
                    pt, az = (x, -ship.B / 2 - dd), -90.0
                else:
                    pt, az = (ship.L / 2 + dd, 0.0), -90.0
                kind_b, burst = water_burst(s, pt, az, ship)
                f = 1.0 / len(xs)
                if kind_b == "surface":
                    r = splinters(s, ship, burst, az)
                    row["perf"] += f * (r.get("hull side", 0) + r.get("waterline", 0) + r.get("deck", 0))
                    row["upper"] += f * (r.get("upper works", 0) + r.get("DCT", 0))
                    row["light"] += f * r.get("funnels", 0)
                    row["wl"] += f * r["wl_hole_cm2"]
                    row["cas"] += f * r["casualties"]
                    row["nsf"] += f * shock_factor(s.w_tnt, burst, ship)[0]
                elif kind_b == "underwater":
                    row["nsf"] += f * shock_factor(s.w_tnt, burst, ship)[0]
                elif kind_b == "underwater_hit":
                    row["uwhit"] += f
            rows.append(row)
        tab[kind] = rows
    return tab


def interp(dists, rows, d, key):
    if d >= dists[-1]:
        return 0.0
    for i in range(len(dists) - 1):
        if dists[i] <= d <= dists[i + 1]:
            f = (d - dists[i]) / (dists[i + 1] - dists[i])
            return rows[i][key] * (1 - f) + rows[i + 1][key] * f
    return rows[0][key]


def salvo_mc(s, ship, n_guns, sigma_r, sigma_d, n_salvos=4000, seed=3, fc=(0.0, 0.0), tab=None):
    dists = DISTS
    tab = tab or effect_tables(s, ship, dists)
    rnd = random.Random(seed)
    acc = dict(hits=0.0, nm10=0.0, nm30=0.0, perf=0.0, upper=0.0, light=0.0, wl=0.0, cas=0.0, uwhit=0.0,
               shock_minor=0.0, shock_struct=0.0)
    for _ in range(n_salvos):
        mx, my = rnd.gauss(0, fc[1]), rnd.gauss(0, fc[0])     # the salvo's fire-control error
        for _ in range(n_guns):
            xi = mx + rnd.gauss(0, sigma_d)
            yi = my + rnd.gauss(0, sigma_r)
            kind, d = classify(ship, s, xi, yi)
            if kind == "hit":
                acc["hits"] += 1
                continue
            if d < 10:
                acc["nm10"] += 1
            if d < 30:
                acc["nm30"] += 1
            rows = tab[kind]
            for k in ("perf", "upper", "light", "wl", "cas", "uwhit"):
                acc[k] += interp(dists, rows, d, k)
            nsf = interp(dists, rows, d, "nsf")
            if 0.08 <= nsf < 0.2:
                acc["shock_minor"] += 1
            elif nsf >= 0.2:
                acc["shock_struct"] += 1
    return {k: v / n_salvos for k, v in acc.items()}


# ------------------------------------------------------------------------------------------------
def main():
    dd = fletcher()

    print("=== 1. The fragment field per shell (13's burst(): side spray, nose spray; >= 1 g) ===")
    print(f"{'shell':18}{'v0 side':>8}{'v0 nose':>8}{'side >=1g':>10}{'nose >=1g':>10}{'mean g':>8}{'W TNT kg':>9}")
    for s in SHELLS.values():
        gs = {g.name: g for g in groups(s)}
        b = fr.burst(s.body)
        print(f"{s.name:18}{gs['side'].v0:8.0f}{gs['nose'].v0 if 'nose' in gs else 0:8.0f}"
              f"{group_count(gs['side']):10.0f}{group_count(gs['nose']) if 'nose' in gs else 0:10.0f}"
              f"{b.mean_g:8.1f}{s.w_tnt:9.1f}")
    print("in water, a fragment's speed falls 1/e every " + ", ".join(
        f"{m * 1000:.0f} g {fr.L1_WATER * m ** (1 / 3) * 100:.1f} cm" for m in (0.001, 0.01, 0.1, 1.0)))

    print("\n=== 2. Reconciliation with 13's T8 (8 mm side 7 m high, 110 m; burst AT the surface; shell keeps"
          " its striking speed) ===")
    flat = flat_hull()
    print(f"{'shell (v, fall)':30}{'beam 10':>12}{'25':>12}{'50':>12}{'short 10':>12}{'25':>12}{'50':>12}"
          "   (holes through 8 mm)")
    for name, v, fall in (("5in38 AAC 9km", 300, 20), ("6in HC 13km", 400, 15), ("20cm common 15km", 450, 15),
                          ("46cm T0 HE 15km", 480, 15), ("46cm T0 HE 15km", 480, 35)):
        s0 = SHELLS[name]
        s = Shell(s0.name, s0.body, s0.fuze, 0.0, v, fall)
        row = f"{name + f' ({v}, {fall})':30}"
        for case in ("beam", "short"):
            for d in (10, 25, 50):
                if case == "beam":
                    r = splinters(s, flat, (0.0, 6.0 + d, 0.0), 0.0, v_keep=1.0)
                else:
                    r = splinters(s, flat, (0.0, 6.0 + d, 0.0), -90.0, v_keep=1.0)
                row += f"{r.get('hull side', 0):12.0f}"
        print(row)
    print("  13's T8 holes, same order: 5in 10/4/1 13/5/1; 6in 18/7/2 28/12/3; 20cm 18/7/2 65/25/8;"
          " 46cm(15) 54/21/10 301/94/39; 46cm(35) 24/14/0 205/172/53")

    print("\n=== 3. The same burst a few cm down: the fuze's function time on water (46 cm HE short of the flat"
          " side) ===")
    s0 = SHELLS["46cm T0 HE 15km"]
    print(f"{'t_f':>8}{'depth':>8}{'short 10':>10}{'25':>8}{'50':>8}")
    for tf in (0.0, 0.0001, 0.0005, 0.001, 0.002, 0.005):
        s = Shell(s0.name, s0.body, "nose", tf, 480, 15)
        z = surface_depth(s)
        row = f"{tf * 1000:6.1f}ms{-z * 100:6.1f}cm"
        for d in (10, 25, 50):
            r = splinters(s, flat, (0.0, 6.0 + d, z), -90.0, v_keep=1.0)
            row += f"{r.get('hull side', 0):9.0f}"
        print(row)

    print("\n=== 4. Splinters from a surface burst abreast a Fletcher, broadside, fire from abeam (t_f 0.5 ms) ===")
    print("   perforations of hull (8 mm) / upper works (6.4 mm) / funnels (4.8 mm), waterline holes, exposed crew"
          " hit; averaged along her length")
    for name in ("5in38 AAC 9km", "20cm common 15km", "16in HC 15km", "46cm T0 HE 15km"):
        s = SHELLS[name]
        print(f"\n{name}")
        print(f"{'d m':>6}{'short':>32}{'over':>32}{'off the bow':>32}")
        for d in (1, 3, 7, 15, 30, 60):
            line = f"{d:6}"
            for kind in ("short", "over", "end"):
                xs = XS_ALONG if kind != "end" else (0.0,)
                acc = dict(h=0.0, u=0.0, l=0.0, wl=0.0, c=0.0)
                for x in xs:
                    if kind == "short":
                        p, az = (x, dd.B / 2 + d, surface_depth(s)), -90.0
                    elif kind == "over":
                        p, az = (x, -dd.B / 2 - d, surface_depth(s)), -90.0
                    else:
                        p, az = (dd.L / 2 + d, 0.0, surface_depth(s)), -90.0
                    r = splinters(s, dd, p, az)
                    acc["h"] += (r.get("hull side", 0) + r.get("waterline", 0) + r.get("deck", 0)) / len(xs)
                    acc["u"] += r.get("upper works", 0) / len(xs)
                    acc["l"] += r.get("funnels", 0) / len(xs)
                    acc["wl"] += r["wl_hole_cm2"] / len(xs)
                    acc["c"] += r["casualties"] / len(xs)
                line += f"{acc['h']:6.0f}/{acc['u']:3.0f}/{acc['l']:3.0f} {acc['wl']:6.0f}cm2 {acc['c']:4.1f}  "
            print(line)

    print("\n=== 5. Aspect: a 46 cm HE burst 10 m off a Fletcher's side ===")
    s = SHELLS["46cm T0 HE 15km"]
    z = surface_depth(s)
    for label, pt, az in (("beside, bow-on target, fire from ahead", (0.0, dd.B / 2 + 10), 180.0),
                          ("short, broadside target", (0.0, dd.B / 2 + 10), -90.0),
                          ("over, broadside target", (0.0, -dd.B / 2 - 10), -90.0)):
        r = splinters(s, dd, (pt[0], pt[1], z), az)
        print(f"  {label:40} hull {r.get('hull side', 0) + r.get('waterline', 0):6.0f}  upper "
              f"{r.get('upper works', 0):5.0f}  crew {r['casualties']:5.1f}")

    print("\n=== 6. Shock: the bomb record, as the near-miss shock factor (alongside, burst ~2 m deep) ===")
    for ship_txt, w, dft, dmg in BOMB_CASES:
        nsf, info = shock_factor(w, (0.0, dd.B / 2 + dft * 0.3048, -2.0), dd)
        print(f"  {ship_txt:38} NSF {nsf:5.2f}  -> {dmg}")
    print("  Bands used:")
    lo = 0.0
    for hi, txt in SHOCK_BANDS:
        print(f"    {lo:4.2f}-{hi if hi < 50 else 'up':>4}: {txt}")
        lo = hi

    print("\n=== 7. White Plains, 25 Oct 1944: a 46 cm shell bursting under the turn of the bilge ===")
    cve = Ship("Casablanca CVE", 156.0, 20.0, 6.3, 8.0)
    for w, depth, y, txt in ((33.85, 9.3, 8.0, "T91 AP (33.85 kg), 3 m under the bilge"),
                             (33.85, 11.3, 8.0, "T91 AP (33.85 kg), 5 m under the bilge"),
                             (33.85, 14.3, 8.0, "T91 AP (33.85 kg), 8 m under the bilge"),
                             (23.9, 11.3, 8.0, "T91 AP (08's 23.9 kg TNT-eq), 5 m under"),
                             (61.7, 0.07, 13.0, "T0 HE, surface burst 3 m off the side")):
        nsf, info = shock_factor(w, (0.0, y, -depth), cve)
        print(f"  {txt:44} NSF {nsf:5.2f} (R {info['r']:4.1f} m, cut {info['cut']:.2f}) -> {band(nsf)}")

    print("\n=== 8. Shock reach (horizontal distance from the side, m) for NSF 0.03 / 0.08 / 0.2 / 0.45 ===")
    print(f"{'shell':18}{'burst depth':>12}{'0.03':>7}{'0.08':>7}{'0.2':>7}{'0.45':>7}")
    for s in SHELLS.values():
        depth = -surface_depth(s) if s.fuze == "nose" else 3.0
        r = [shock_reach(s.w_tnt, depth, t, dd) for t in (0.03, 0.08, 0.2, 0.45)]
        print(f"{s.name:18}{depth:11.2f}m" + "".join(f"{x:7.1f}" for x in r))

    print("\n=== 9. Base fuzes from the water (P6's switch on): where a short AP shell bursts, Fletcher ===")
    for name in ("16in AP 15km", "46cm T91 AP 15km"):
        s = SHELLS[name]
        L = (50 if s.diving else 25) * s.d
        xf = L * math.log(1 + s.v * s.delay / L)
        print(f"  {name}: fuze run {xf:.1f} m under water")
        for d in (2, 5, 10, 20, 40, 60, 80):
            kind, p = water_burst(s, (0.0, dd.B / 2 + d), -90.0, dd)
            extra = ""
            if kind == "underwater":
                nsf, _ = shock_factor(s.w_tnt, p, dd)
                extra = f" at y {p[1]:6.1f} m (side at +-6), depth {-p[2]:4.1f} m, NSF {nsf:4.2f}: {band(nsf)}"
            elif kind == "underwater_hit":
                extra = f" at depth {-p[2]:.1f} m"
            print(f"    short {d:3} m -> {kind}{extra}")

    print("\n=== 10. River Plate checks: 28 cm HE short of a Leander-like cruiser abreast her DCT ===")
    cl = leander()
    s = SHELLS["28cm HE 16km"]
    for d in (3, 10, 20, 30, 40, 60):
        r = splinters(s, cl, (36.0, cl.B / 2 + d, surface_depth(s)), -90.0)
        print(f"  short {d:3} m: hull {r.get('hull side', 0) + r.get('waterline', 0):5.0f}, upper works "
              f"{r.get('upper works', 0):5.0f}, DCT {r.get('DCT', 0):4.1f}, exposed crew {r['casualties']:4.1f}")

    print("\n=== 12. Check: the analytic lab-frame spray (what the game uses) against the rays ===")
    s = SHELLS["46cm T0 HE 15km"]
    ax, e1, e2 = axis_frame(s, -90.0)
    gs = {g.name: g for g in groups(s)}
    lab = to_lab(static_rays(s, 60000, seed=7), s, -90.0, s.v * 0.95)
    for gname, psi, phi in (("side", 90, 90), ("side", 86, 60), ("side", 93, 20), ("nose", 55, 90),
                            ("nose", 70, 40)):
        p, f = math.radians(psi), math.radians(phi)
        n = (math.sin(p) * math.cos(f), math.sin(p) * math.sin(f), math.cos(p))
        (sd, sp, _, _), = to_lab([(n, gs[gname], 1.0)], s, -90.0, s.v * 0.95)
        half = math.radians(1.5)
        cnt = sum(w * group_count(g) for d, _, g, w in lab if sum(x * y for x, y in zip(d, sd)) >= math.cos(half))
        ray_d = cnt / (2 * math.pi * (1 - math.cos(half)))
        an = lab_density(s, s.v * 0.95, ax, sd)
        el = math.degrees(math.asin(sd[2]))
        print(f"  {gname} spray, static {psi} deg from the nose (lab elevation {el:4.0f} deg): rays {ray_d:7.1f}/sr,"
              f" analytic {sum(x[1] for x in an):7.1f}/sr at {sp:.0f} m/s")

    print("\n=== 11. Per salvo on a Fletcher, broadside ===")
    print("   hits | misses within 10 / 30 m | perforations hull / upper / funnels | waterline cm2 |"
          " exposed crew hit | shock 0.08-0.2 / >=0.2 | underwater hits")
    cases = [
        ("46cm T0 HE 15km", 9, 36, 15, "Yamato 9 guns, pattern 1.2%"),
        ("46cm T91 AP 15km", 9, 36, 15, "same, AP"),
        ("16in AP 15km", 9, 57, 23, "Iowa 9 guns, AP"),
        ("16in HC 15km", 9, 57, 23, "Iowa 9 guns, 1.9%"),
        ("20cm common 15km", 10, 60, 25, "IJN cruiser 10 guns, 2%"),
        ("5in38 AAC 9km", 5, 40, 16, "destroyer 5 guns, ~2.2%"),
    ]
    for name, n, sr, sd, txt in cases:
        s = SHELLS[name]
        tab = effect_tables(s, dd, DISTS)
        for fc, fctxt in (((0.0, 0.0), "straddle"), ((150.0, 40.0), "FC err 150/40 m")):
            r = salvo_mc(s, dd, n, sr, sd, n_salvos=4000, tab=tab, fc=fc)
            print(f"  {name:17} {txt:27} {fctxt:16} {r['hits']:5.2f} | {r['nm10']:4.2f} / {r['nm30']:4.2f} | "
                  f"{r['perf']:5.0f} / {r['upper']:4.0f} / {r['light']:4.0f} | {r['wl']:6.0f} | {r['cas']:5.2f} | "
                  f"{r['shock_minor']:4.2f} / {r['shock_struct']:4.2f} | {r['uwhit']:4.2f}")

if __name__ == "__main__":
    main()
