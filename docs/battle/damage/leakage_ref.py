"""leakage_ref.py -- reference for 17_structural_leakage.md (Fleetwright damage research).

Flooding through damaged structure: sprung seams, loosened rivets, dished bulkheads and decks, loosened fittings
(door dogs, stuffing tubes, glands), and how those leaks grow or are fought over time.

It answers one question for the sim: when a burst or shock leaves a boundary standing (not ruptured), what leak
area does it get, how does that depend on how hard it was hit, on the head across it and on time, and what does damage
control do to it? It replaces 11 sec. 5.1's "deformed -> the 4.3b leak rate x10" and 15 sec. 6.4's 5-50 / 50-500 cm2
bands with one curve, and sets the tuning knobs out in one place (KNOBS).

Pure Python, no dependencies. `python leakage_ref.py` prints every table quoted in the note (seeded, ~1-2 min).
Tags in comments: [SRC] sourced, [FIT] fitted here to the cases, [EST] judgement, [UNCERTAIN] thin sources.

Conventions
  D        damage index of a boundary: what loaded it over what makes it deform.
           Blast (11): D = P_felt / P_deform, P_deform = 0.012 MPa/mm (x0.85 riveted). 11's rupture is 0.06 MPa/mm,
           so a boundary still standing has D < 5 and D >= 5 is 11's "ruptured".
           Underwater shock (15): D = 5 * NSF_local / 1.0, so NSF 0.2 (seams open) -> D 1, NSF 1.0 (holed) -> D 5.
  r(D)     leak-area ratio: cm2 of equivalent orifice per m2 of the boundary that took load D.
  head     metres of sea water across the leak; flows by Q = Cd A sqrt(2 g dh), Cd = 0.6 (as 09, 13, FLOODSTAND).
"""
import math
import random

RHO = 1.025   # t/m3 sea water
G = 9.81
CD = 0.6      # [SRC] FLOODSTAND final report: 0.6 "well justified" for all openings but pipes/ducts
MU = 1.08e-3  # Pa s, sea water ~15 C

# ------------------------------------------------------------------------------------------------------------
# The knobs. Everything the note calls a tuning value is here.
# ------------------------------------------------------------------------------------------------------------
KNOBS = dict(
    D_weep=0.3,        # below this a boundary is untouched                                         [EST]
    r1=0.3,            # cm2/m2 at D = 1, "deformed, seams just sprung" (Lexington's weeps, SF 0.2)  [FIT]
    r5=100.0,          # cm2/m2 just short of rupture (D -> 5): ~1% open; Titanic ~180, NC ~250     [FIT]
    r_rupt=2500.0,     # cm2/m2 ruptured = 11's "about 1/4 of its area"                              [SRC 11]
    sigma_ln=0.8,      # lognormal spread of one boundary's area about r(D): x/÷ 2.2 at 1 sigma     [EST]
    welded=0.4,        # seam-leak multiplier for a welded boundary (fittings unchanged)            [EST]
    beta=1.0,          # head opening: A x (1 + beta * dh / h_rating)                              [EST]
    lam0=0.1,          # per hour: chance a sprung seam "works" open a step, at its rating head, calm, stopped [EST]
    work_step=1.6,     # x area per working step                                                    [EST]
    work_max=4.0,      # working saturates: beyond this the seam is "opened" and the rating decides  [EST]
    sea=0.0,           # sea-state term: hazard x (1 + sea); ~1 in a moderate sea, ~3 in a gale     [EST, O'Brien]
    v0_kn=15.0,        # speed scale for working (vibration, pitching, ram)                         [EST]
    caulk=0.15,        # caulked seam keeps 15% (13_nonpen: x0.15 in 10-40 min)                     [SRC-ish]
    shore=0.5,         # a shored panel: its working hazard x0.5 and rating x1.6 (09)                [EST]
)


def q_th(area_m2, dh):
    """Orifice flow, t/h."""
    if dh <= 0 or area_m2 <= 0:
        return 0.0
    return CD * area_m2 * math.sqrt(2 * G * dh) * 3600 * RHO


def area_for(q_tph, dh):
    """Equivalent orifice area (m2) that passes q t/h at head dh."""
    return q_tph / (3600 * RHO * CD * math.sqrt(2 * G * dh))


# ------------------------------------------------------------------------------------------------------------
# 1. Physics: is a seam an orifice?  A slot of gap g and path length L (plate thickness for a butt crack,
#    lap width ~3 rivet diameters for a lap seam) loses head to entry/exit (orifice) and to viscous shear
#    (plane Poiseuille), in series:  rho g dh = 1/2 rho K u^2 + 12 mu L u / g^2,  K = 1/Cd^2.
# ------------------------------------------------------------------------------------------------------------
def slot_cd_eff(gap_m, path_m, dh):
    """The slot's effective discharge coefficient: its mean speed over sqrt(2 g dh)."""
    rho = RHO * 1000
    dp = rho * G * dh
    a = 0.5 * rho / CD ** 2
    b = 12 * MU * path_m / gap_m ** 2
    u = (-b + math.sqrt(b * b + 4 * a * dp)) / (2 * a)
    return u / math.sqrt(2 * G * dh)


def table_slot():
    print("\n## 1. Is a seam an orifice? effective Cd of a slot (orifice = 0.60)")
    print("gap mm | butt crack L=15 mm: 0.5 m / 2 m / 8 m head | lap seam L=75 mm: 0.5 m / 2 m / 8 m head")
    for g_mm in (0.02, 0.05, 0.1, 0.2, 0.5, 1.0, 3.0):
        g = g_mm / 1000
        a = [slot_cd_eff(g, 0.015, h) for h in (0.5, 2, 8)]
        b = [slot_cd_eff(g, 0.075, h) for h in (0.5, 2, 8)]
        print(f"{g_mm:6.2f} | " + " / ".join(f"{x:.2f}" for x in a) + "  | " + " / ".join(f"{x:.2f}" for x in b))
    # per metre of seam at 4 m: t/h
    print("t/h per metre of seam at 4 m head (lap seam):",
          ", ".join(f"{g} mm {slot_cd_eff(g/1000, 0.075, 4)/CD*q_th(g/1000, 4):.2f}" for g in (0.05, 0.1, 0.2, 0.5, 1.0)))


# ------------------------------------------------------------------------------------------------------------
# 2. The record, turned into equivalent areas. Heads and floor areas are [EST] where the reports give none.
# ------------------------------------------------------------------------------------------------------------
GAL = 3.785e-3  # m3 per US gal

ANCHORS = [
    # name, t/h, head m, boundary area m2 (None: a point leak), path, source
    ("NC: Turret I stool, loosened rivets", 50 * GAL * RHO, 6.0, None, "rivets", "WDR 61 V-G-4: ~50 gal/h"),
    ("NC: residual after caulking (15 min pumping/watch, 30 t/h pump)", 30 * 0.25 / 4, 3.0, None, "caulked boundaries",
     "WDR 61 V-C-9; pump rate [EST]"),
    ("Pensacola: riveted tank bulkhead, before Test A", 20 * 0.0254 / (21 * 24) * RHO, 4.0, 40, "tank seam",
     "AD0366710: 1 in / 3 weeks; 20 m2 floor [EST]"),
    ("Pensacola: the same after Test A (ship whipped)", 20 * 0.356 / 96 * RHO, 4.0, 40, "tank seam",
     "AD0366710: 1 ft 2 in in ~4 days"),
    ("Ralph Talbot: rudder port packing after Test A", 10 * 0.0254 / 24 * RHO, 3.0, None, "gland",
     "AD0366723: 1 in/day; 10 m2 [EST]"),
    ("Flying Fish: hull leaks at 300 ft", 8000 * 0.4536 / 1000, 91.0, None, "seams/glands", "WDR 58 App. I"),
    ("Houston CL-81: magazine via loosened cable stuffing tubes (filled ~3 h)", 120 / 3.0, 2.5, 25, "stuffing tubes",
     "WDR 53; 150 m3 magazine [EST]"),
    ("Chester CA-27: after engine room via shaft and bulkhead", 20000 * GAL * RHO, 5.0, None, "gland + seams",
     "WDR CA-27: 20,000 gal/h, 'must have been greater'"),
    ("Helena CL-50: boiler room 4 'within two hours' via a non-WT bulkhead", 500 / 2.0, 4.0, 60, "flanged joint",
     "WDR 43; 500 t room [EST]"),
    ("Grosser Kurfuerst: belt plate forced in 33 cm, overnight net", 180, 2.0, 8 * 2.5, "sprung belt",
     "13_nonpen (A-H report p.26), net of pumps"),
    ("Saratoga: bottom dished 3-6 in, fr 68-137, sank in 7.6 h unattended", 15000 / 7.6, 8.0, 84 * 5, "dished hull seams",
     "AD0366725; water to sink and band height [EST, UNCERTAIN]"),
    ("Titanic: 'equivalent 12 sq ft' along ~200 ft (3/4 in average)", q_th(1.11, 7.6), 7.6, 61 * 1.5,
     "sheared rivets, parted seams", "Wilding, BoT inquiry; band 1.5 m [EST]"),
    ("NC: holding bulkhead seam opened ~1 ft over fr 45-52 (rupture)", q_th(0.3 * 8.5 / 2, 6), 6.0, 8.5 * 6,
     "rivets pulled out", "WDR 61 V-F-3; wedge-shaped opening [EST]"),
]


def table_anchors():
    print("\n## 2. The record as equivalent areas")
    print("case | t/h | head m | area cm2 | per m2 of loaded boundary")
    for name, q, h, ab, path, src in ANCHORS:
        a = area_for(q, h) * 1e4
        per = f"{a/ab:.2g} cm2/m2" if ab else "point"
        print(f"{name} | {q:.3g} | {h:g} | {a:.3g} | {per}")


# ------------------------------------------------------------------------------------------------------------
# 3. The curve: leak ratio by damage index
# ------------------------------------------------------------------------------------------------------------
def leak_ratio(D, k=KNOBS):
    """cm2 of leak per m2 of loaded boundary. 0 below D_weep; ~D^2 up to D = 1 (weeping: rivets, gaskets); then
    log-linear from r1 at D = 1 to r5 at D = 5 (seams sprung, then opened, then torn); r_rupt at D >= 5."""
    if D < k["D_weep"]:
        return 0.0
    if D < 1.0:
        return k["r1"] * D * D
    if D < 5.0:
        return k["r1"] * (k["r5"] / k["r1"]) ** ((D - 1.0) / 4.0)
    return k["r_rupt"]


def state_name(D):
    if D < KNOBS["D_weep"]:
        return "intact"
    if D < 1:
        return "weeping"
    if D < 2.5:
        return "sprung"
    if D < 5:
        return "opened"
    return "ruptured"


def boundary_leak(D, area_m2, riveted=True, rng=None, k=KNOBS):
    """One boundary's seam leak area (m2) from the load it took. Lognormal about the curve when rng is given."""
    r = leak_ratio(D, k)
    if r == 0:
        return 0.0
    if D < 5 and not riveted:
        r *= k["welded"]
    f = math.exp(rng.gauss(0, k["sigma_ln"]) - 0.5 * k["sigma_ln"] ** 2) if rng else 1.0
    return min(r * f, k["r_rupt"]) * area_m2 * 1e-4


def table_curve():
    print("\n## 3. The curve r(D), cm2 per m2, and what it gives a boundary")
    print("D | state | r cm2/m2 | 30 m2 DD bulkhead cm2 | 150 m2 BB bulkhead cm2 | BB bulkhead t/h at 4 m | gap if all in edges (mm)")
    for D in (0.3, 0.5, 0.8, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 4.99, 5.0):
        r = leak_ratio(D)
        a30, a150 = r * 30, r * 150
        perim150 = 2 * (25 + 6)  # a 25 m x 6 m transverse bulkhead's edge angles, m
        gap = a150 * 1e-4 / perim150 * 1000
        print(f"{D:g} | {state_name(D)} | {r:.3g} | {a30:.3g} | {a150:.3g} | {q_th(a150*1e-4, 4):.3g} | {gap:.2g}")
    print("\nAnchors placed on the curve (D that gives the anchor's cm2/m2):")
    for name, q, h, ab, path, src in ANCHORS:
        if not ab:
            continue
        per = area_for(q, h) * 1e4 / ab
        print(f"  {name}: {per:.2g} cm2/m2 -> D = {inv_ratio(per):.2f}")


def inv_ratio(per, k=KNOBS):
    if per >= k["r_rupt"]:
        return 5.0
    if per <= k["r1"]:
        return max(k["D_weep"], math.sqrt(per / k["r1"]))
    return 1 + 4 * math.log(per / k["r1"]) / math.log(k["r5"] / k["r1"])


# Underwater shock: 15's bands against the curve
def table_shock():
    print("\n## 3b. 15's shock bands against the curve (D = 5 x NSF; a 30 m2 patch of side or a bulkhead)")
    print("NSF | D | r cm2/m2 | 30 m2 cm2 | 15 sec. 6.4 said")
    said = {0.1: "seams weep", 0.2: "5-50 cm2", 0.3: "5-50 cm2", 0.45: "5-50 / 50-500", 0.7: "50-500", 0.95: "50-500",
            1.0: "a hole"}
    for nsf in (0.06, 0.1, 0.2, 0.3, 0.45, 0.7, 0.95, 1.0):
        D = 5 * nsf
        r = leak_ratio(D)
        print(f"{nsf:g} | {D:.2g} | {r:.3g} | {r*30:.3g} | {said.get(nsf, '')}")


# ------------------------------------------------------------------------------------------------------------
# 4. Fittings: what shock and blast loosen in a boundary besides its seams.
#    Crossroads B: loose doors 19.9%, shaft glands 15.2%, hatch dogs 13.5%, reach rods 12.5%, stuffing tubes 4.7%
#    of secondary flooding [SRC AD0366699]. The areas are [EST], checked against Houston and Chester.
# ------------------------------------------------------------------------------------------------------------
FITTINGS = {
    #  kind:            (D at which it starts, p per unit D above that (cap 0.9), area cm2 lo, hi)
    "door_dogs":        (0.5, 0.35, 10, 60),     # dogs loosened / door dished 2-4 in (Mayrant, Houston B-312-L)
    "hatch_dogs":       (0.5, 0.30, 10, 50),
    "stuffing_tube":    (0.3, 0.12, 0.5, 3),     # per tube; ~1 per 5 m2 of a machinery/magazine bulkhead (Houston)
    "vent_closure":     (0.5, 0.25, 5, 50),      # NC: 2 ft into turret I LHR through a vent duct
    "reach_rod":        (0.5, 0.20, 1, 5),
    "shaft_gland":      (0.3, 0.50, 10, 100),    # "whenever the water reached the height of the penetration"
    "pipe_flange":      (0.5, 0.25, 5, 130),     # Helena: "squirting with some force from a flanged joint"
}


def fitting_leaks(D, counts, rng, k=KNOBS):
    """Draw loosened fittings on one boundary. counts: {kind: n}. Returns [(kind, area m2)]."""
    out = []
    for kind, n in counts.items():
        d0, p1, lo, hi = FITTINGS[kind]
        if D < d0:
            continue
        p = min(0.9, p1 * (D - d0 + 0.5))
        for _ in range(n):
            if rng.random() < p:
                out.append((kind, math.exp(rng.uniform(math.log(lo), math.log(hi))) * 1e-4))
    return out


def table_fittings():
    print("\n## 4. Fittings loosened on a BB machinery bulkhead (2 doors, 30 tubes, 1 vent, 2 rods, 1 pipe) by D")
    print("D | mean fittings loosened | mean area cm2 | p90 cm2 | seam cm2 (150 m2) | fittings' share")
    counts = {"door_dogs": 2, "stuffing_tube": 30, "vent_closure": 1, "reach_rod": 2, "pipe_flange": 1}
    rng = random.Random(7)
    for D in (0.3, 0.5, 1.0, 1.5, 2.0, 3.0, 4.0):
        tot, ns = [], []
        for _ in range(4000):
            f = fitting_leaks(D, counts, rng)
            ns.append(len(f))
            tot.append(sum(a for _, a in f) * 1e4)
        tot.sort()
        seam = leak_ratio(D) * 150
        m = sum(tot) / len(tot)
        print(f"{D:g} | {sum(ns)/len(ns):.1f} | {m:.3g} | {tot[int(0.9*len(tot))]:.3g} | {seam:.3g} | {m/(m+seam):.0%}")


# ------------------------------------------------------------------------------------------------------------
# 5. Head: a sprung boundary opens further as the head across it grows, and fails at its rating (09 sec. 2.4)
# ------------------------------------------------------------------------------------------------------------
def head_factor(dh, h_rating, k=KNOBS):
    return 1 + k["beta"] * max(0.0, dh) / h_rating


def table_head():
    print("\n## 5. Head dependence: Q relative to a fixed orifice, rating head 8 m")
    print("dh m | A factor | Q (A grows) / Q (fixed A) | Q ~ dh^n, local n")
    prev = None
    for dh in (0.5, 1, 2, 4, 6, 8):
        f = head_factor(dh, 8)
        q = f * math.sqrt(dh)
        n = "" if prev is None else f"{math.log(q/prev[1])/math.log(dh/prev[0]):.2f}"
        print(f"{dh:g} | {f:.2f} | {f:.2f} | {n}")
        prev = (dh, q)


# ------------------------------------------------------------------------------------------------------------
# 6. A small flooding engine for the cases: compartments in a graph, holes to the sea, leaky links.
#    Levels in metres over the outside waterline (negative = below). Same rules as 4.3b in spirit.
# ------------------------------------------------------------------------------------------------------------
class Comp:
    def __init__(s, name, vol, z0, height, perm=0.85, water=0.0):
        s.name, s.cap, s.z0, s.h = name, vol * perm, z0, height
        s.area = s.cap / height
        s.w = min(water, s.cap)
        s.pumped = 0.0

    def level(s):
        return s.z0 + s.w / s.area

    def full(s):
        return s.w >= s.cap - 1e-6


class Link:
    """A boundary between two compartments (b = None: the sea). seam = seam leak area m2 (at m = 1);
    fit = fittings' area m2 (no working, caulked separately); rating = collapse head m."""
    def __init__(s, a, b, seam=0.0, fit=0.0, rating=8.0, hole=0.0, sprung=True):
        s.a, s.b, s.seam, s.fit, s.rating, s.hole = a, b, seam, fit, rating, hole
        s.m = 1.0            # working multiplier on the seam
        s.caulk = 1.0        # 1 = not caulked
        s.fit_caulk = 1.0
        s.shored = False
        s.failed = False
        s.sprung = sprung
        s.events = []

    def area(s, dh):
        if s.failed:
            return s.hole + 0.25 * 40.0  # a collapsed bulkhead: a large opening [EST]
        rat = s.rating * (1.6 if s.shored else 1.0)
        seam = s.seam * s.m * s.caulk * (head_factor(dh, rat) if s.sprung else 1.0)
        return s.hole + seam + s.fit * s.fit_caulk


def pressure_levels(comps, links, sea):
    """Level of each compartment's water as pressure: its surface, or for a full one the highest level it is
    fed from (through any link with area), iterated."""
    L = {c: c.level() for c in comps}
    for _ in range(len(comps) + 1):
        for ln in links:
            for x, y in ((ln.a, ln.b), (ln.b, ln.a)):
                if x is None or not x.full():
                    continue
                src = sea if y is None else L[y]
                if src > L[x]:
                    L[x] = src
    return L


def step(comps, links, dt, sea_at, speed_kn, rng, k=KNOBS, pumps=None):
    L = pressure_levels(comps, links, 0.0)
    flows = []
    for ln in links:
        la = L[ln.a]
        zl_a = ln.a.z0
        if ln.b is None:
            lb, zl = sea_at(ln.a), zl_a
        else:
            lb, zl = L[ln.b], max(zl_a, ln.b.z0)
        ha, hb = max(la, zl), max(lb, zl)
        dh = ha - hb
        if abs(dh) < 1e-6:
            continue
        A = ln.area(abs(dh))
        q = q_th(A, abs(dh)) / 3600 * dt / RHO  # m3 this step
        src, dst = (ln.a, ln.b) if dh > 0 else (ln.b, ln.a)
        # never overshoot equal levels in one step
        areas = [c.area for c in (src, dst) if c is not None]
        a_eq = areas[0] if len(areas) == 1 else areas[0] * areas[1] / (areas[0] + areas[1])
        q = min(q, abs(dh) * a_eq)
        flows.append((src, dst, q, abs(dh)))
        # bulkhead check (09): head over rating -> collapse
        if ln.b is not None and not ln.failed:
            rat = ln.rating * (1.6 if ln.shored else 1.0)
            if abs(dh) > rat:
                ln.failed = True
                ln.events.append("collapsed")
        # working: a sprung seam under load opens in steps, more with speed
        if ln.seam > 0 and ln.sprung and not ln.failed:
            rat = ln.rating * (1.6 if ln.shored else 1.0)
            lam = (k["lam0"] * (abs(dh) / rat) * (1 + speed_kn / k["v0_kn"]) ** 2 * (1 + k["sea"])
                   * (k["shore"] if ln.shored else 1))
            if ln.m < k["work_max"] and rng.random() < lam * dt / 3600:
                ln.m = min(k["work_max"], ln.m * k["work_step"])
                ln.events.append("worked")
            # a caulked hull leak works loose at speed (13_nonpen: 15%/10 min above 15 kn back toward full);
            # an internal bulkhead's caulking only through working steps, which multiply m, not caulk
            if ln.b is None and ln.caulk < 1 and speed_kn > 15:
                ln.caulk = min(1.0, ln.caulk + (1 - ln.caulk) * 0.15 * dt / 600)
    for src, dst, q, dh in flows:
        if src is not None:
            q = min(q, src.w)
        if dst is not None:
            q = min(q, dst.cap - dst.w)
        if src is not None:
            src.w -= q
        if dst is not None:
            dst.w += q
    if pumps:
        for c, rate in pumps.items():
            dq = min(c.w, rate * dt / 3600 / RHO)
            c.w -= dq
            c.pumped += dq


def run(comps, links, hours, dt=5.0, sea_at=None, speed=lambda t: 0.0, actions=(), pumps=lambda t: {}, seed=1,
        report=(0.25, 0.5, 1, 2, 4, 8), k=KNOBS):
    rng = random.Random(seed)
    sea_at = sea_at or (lambda c: 0.0)
    acts = sorted(actions, key=lambda a: a[0])
    out = {}
    t = 0.0
    rep = list(report)
    while t <= hours * 3600:
        while acts and acts[0][0] * 60 <= t:
            acts.pop(0)[1]()
        step(comps, links, dt, sea_at, speed(t), rng, k, pumps(t))
        t += dt
        while rep and t >= rep[0] * 3600:
            out[rep.pop(0)] = {c.name: c.w * RHO for c in comps}
    return out


# ------------------------------------------------------------------------------------------------------------
# 7. The cases
# ------------------------------------------------------------------------------------------------------------
def case_nc_inboard(D, dc, seed=1):
    """NC-type torpedo: the side and TDS layers flooded at once (holed); an inboard magazine/handling space behind a
    holding bulkhead that took load D and stayed standing. 60 m2 bulkhead, 600 m3 space, floor 9 m under the WL."""
    tds = Comp("TDS", 900, -9, 12, perm=0.95, water=900 * 0.95 * 0.75)
    mag = Comp("magazine", 600, -9, 8, perm=0.8)
    rng = random.Random(seed)
    seam = boundary_leak(D, 60, riveted=True, rng=rng)
    fit = sum(a for _, a in fitting_leaks(D, {"stuffing_tube": 10, "vent_closure": 1}, rng))
    hole = Link(tds, None, hole=40.0, sprung=False)
    hb = Link(mag, tds, seam=seam, fit=fit, rating=1.3 * 8)
    acts = []
    pumps = lambda t: {}
    if dc:
        def caulk():
            hb.caulk = KNOBS["caulk"]; hb.fit_caulk = 0.2
        def shore():
            hb.shored = True
        acts = [(20, caulk), (25, shore)]
        pumps = lambda t: ({mag: 30.0} if t > 10 * 60 else {})
    res = run([tds, mag], [hole, hb], 8, actions=acts, pumps=pumps, seed=seed, speed=lambda t: 18.0)
    return (seam + fit) * 1e4, res, hb


def table_nc():
    print("\n## 7.1 NC-type: water (t) in the space behind a holding bulkhead that stood, by D, 18 kn")
    print("D | leak cm2 (seam+fittings, median draw) | no DC: 0.5 h / 2 h / 8 h | drilled DC (pump 30 t/h @10 min, caulk @20, shore @25): 0.5 h / 2 h / 8 h")
    for D in (0.5, 1.0, 1.5, 2.0, 3.0, 4.0):
        rows = []
        for dc in (False, True):
            ws = {h: [] for h in (0.5, 2, 8)}
            areas = []
            for s in range(1, 41):
                a, res, _ = case_nc_inboard(D, dc, seed=s)
                areas.append(a)
                for h in ws:
                    ws[h].append(res[h]["magazine"])
            rows.append(" / ".join(f"{sorted(ws[h])[20]:.0f}" for h in (0.5, 2, 8)))
        print(f"{D:g} | {sorted(areas)[20]:.3g} | {rows[0]} | {rows[1]}")
    print("  (medians of 40 seeded draws; the space holds ~490 t)")


def case_chain(speed_kn, D_first, tight, dc, seed=1):
    """Luetzow-type: a holed bow space (the broadside torpedo flat) and four compartments aft of it in a chain,
    1,500 m3 each, floors 9 m under the WL, transverse bulkheads 150 m2. The first bulkhead took blast/shock D_first;
    the rest are intact but leak at `tight` cm2 base (09's tightness) through glands, joints, speaking tubes.
    Ram head on the forward space 0.5 v^2/2g (09 sec. 2.2)."""
    cs = [Comp(f"C{i}", 1500, -9, 11, perm=0.85) for i in range(5)]
    cs[0].w = cs[0].cap * 0.8
    rng = random.Random(seed)
    links = [Link(cs[0], None, hole=6.0, sprung=False)]
    for i in range(4):
        if i == 0:
            seam = boundary_leak(D_first, 150, True, rng)
            fit = sum(a for _, a in fitting_leaks(D_first, {"door_dogs": 1, "stuffing_tube": 20, "vent_closure": 1}, rng))
        else:
            seam, fit = 0.0, tight * 1e-4
        links.append(Link(cs[i + 1], cs[i], seam=seam, fit=fit, rating=1.3 * 9, sprung=(i == 0)))
    v = speed_kn * 0.5144
    ram = 0.5 * v * v / (2 * G)
    sea_at = lambda c: ram if c is cs[0] else 0.0
    acts, pumps = [], (lambda t: {})
    if dc:
        def caulk(ln=links[1]):
            ln.caulk = KNOBS["caulk"]; ln.fit_caulk = 0.2
        def shore(ln=links[1]):
            ln.shored = True
        acts = [(30, caulk), (40, shore)]
        pumps = lambda t: ({cs[1]: 60.0} if t > 20 * 60 else {})
    res = run(cs, links, 8, sea_at=sea_at, speed=lambda t: speed_kn, actions=acts, pumps=pumps, seed=seed)
    return res, links


def table_chain():
    print("\n## 7.2 Luetzow-type chain: water aft of the holed bow space (t, C1..C4 summed), medians of 30 draws")
    print("first bulkhead D | others' tightness cm2 | speed kn | DC | 1 h | 2 h | 4 h | 8 h | collapsed by 8 h")
    for D, tight, spd, dc in [(1.5, 20, 7, False), (1.5, 20, 15, False), (1.5, 200, 15, False), (2.5, 200, 15, False),
                               (2.5, 200, 7, False), (2.5, 200, 3, False), (2.5, 200, 15, True), (2.5, 200, 7, True),
                               (3.5, 200, 15, False), (3.5, 200, 15, True)]:
        tots = {h: [] for h in (1, 2, 4, 8)}
        coll = 0
        for s in range(1, 31):
            res, links = case_chain(spd, D, tight, dc, seed=s)
            for h in tots:
                tots[h].append(sum(res[h][f"C{i}"] for i in range(1, 5)))
            coll += any(ln.failed for ln in links[1:])
        med = lambda xs: sorted(xs)[len(xs) // 2]
        print(f"{D:g} | {tight:g} | {spd:g} | {'drilled' if dc else 'none'} | " +
              " | ".join(f"{med(tots[h]):.0f}" for h in (1, 2, 4, 8)) + f" | {coll}/30")
    print("  (the four spaces hold ~4,300 t; Luetzow's below-armour water was 4,209 t of 8,351 t at ~02:00, about 6.5 h"
          " after the torpedo-flat hits)")


def case_houston():
    """Houston-type: a magazine (150 m3) beside a flooded space, bulkhead's cable stuffing tubes loosened (no seam
    damage). 25 tubes, median-ish 1.5 cm2 loosened on ~half."""
    out = []
    for n_loose, a_each in [(6, 1.0), (12, 1.5), (20, 2.0)]:
        flooded = Comp("flooded", 800, -8, 9, perm=0.9, water=800 * 0.9)
        mag = Comp("magazine", 150, -8, 5, perm=0.8)
        links = [Link(flooded, None, hole=2.0, sprung=False),
                 Link(mag, flooded, fit=n_loose * a_each * 1e-4, rating=1.3 * 9, sprung=False)]
        res = run([flooded, mag], links, 8, report=(0.5, 1, 2, 3, 4, 8))
        cap = mag.cap * RHO
        t_full = next((h for h in (0.5, 1, 2, 3, 4, 8) if res[h]["magazine"] >= 0.98 * cap), None)
        out.append((n_loose, a_each, n_loose * a_each, [res[h]["magazine"] for h in (0.5, 1, 2, 3)], cap, t_full))
    print("\n## 7.3 Houston-type: a magazine flooding through loosened cable stuffing tubes only")
    print("tubes loose | cm2 each | total cm2 | t at 0.5 / 1 / 2 / 3 h | holds t | full by")
    for n, a, tot, ws, cap, tf in out:
        print(f"{n} | {a:g} | {tot:g} | " + " / ".join(f"{w:.0f}" for w in ws) + f" | {cap:.0f} | " + (f"{tf} h" if tf else "not by 8 h"))
    print("  (Houston: A-421/A-422 found completely flooded ~3 h after the hit, through stuffing tubes in bulkhead 57)")


def case_saratoga():
    """Saratoga-type: a long band of bottom plating dished by underwater shock, no holes, nobody aboard.
    84 m x 5 m of one side, D by the curve; 6 compartments behind it, 2,500 m3 each."""
    print("\n## 7.4 Saratoga-type: a dished bottom, unattended. Water (t) over time by D of the band (420 m2)")
    print("D | leak cm2 | 1 h | 2 h | 4 h | 8 h")
    for D in (1.5, 2.0, 2.5, 3.0):
        comps = [Comp(f"S{i}", 2500, -10, 10, perm=0.9) for i in range(6)]
        links = [Link(c, None, seam=boundary_leak(D, 70, True), rating=1e9) for c in comps]
        res = run(comps, links, 8, report=(1, 2, 4, 8))
        tot = lambda h: sum(res[h].values())
        print(f"{D:g} | {leak_ratio(D)*420:.3g} | " + " | ".join(f"{tot(h):.0f}" for h in (1, 2, 4, 8)))
    print("  (the six spaces hold ~13,800 t; Saratoga listed 3 deg at +2.6 h, 6 deg at +3.4 h, sank at +7.6 h)")


def case_dd():
    """A destroyer compartment next to an 8in HC burst (11 sec. 4.2: doors blown, two bulkheads 'deformed'):
    if the burst space is holed below the waterline, how fast does the deformed neighbour follow?"""
    print("\n## 7.5 Destroyer: the neighbour of a holed burst space, behind a 30 m2 bulkhead, by D (no DC / DC at 5 min)")
    print("D | leak cm2 | no DC: neighbour to 50% / 90% of its waterline level (~78 t) | DC (caulk at 5 min, 1 pump 25 t/h): t at 30 min")
    for D in (1.0, 2.0, 3.0, 4.2):
        res_t = []
        for dc in (False, True):
            burst = Comp("burst", 150, -3, 5, perm=0.85, water=150 * 0.85)
            nb = Comp("neighbour", 150, -3, 5, perm=0.85)
            hb = Link(nb, burst, seam=boundary_leak(D, 30, False), rating=1.3 * 5)
            links = [Link(burst, None, hole=0.5, sprung=False), hb]
            acts, pumps = [], (lambda t: {})
            if dc:
                acts = [(5, lambda hb=hb: setattr(hb, "caulk", KNOBS["caulk"]))]
                pumps = lambda t, nb=nb: {nb: 25.0}
            res = run([burst, nb], links, 2, dt=2.0, actions=acts, pumps=pumps,
                      report=[x / 60 for x in range(1, 121)])
            cap = nb.cap * RHO * 3 / 5   # it can fill only to the waterline: 3 m of its 5 m
            if not dc:
                t50 = next((h * 60 for h in sorted(res) if res[h]["neighbour"] >= 0.5 * cap), None)
                t90 = next((h * 60 for h in sorted(res) if res[h]["neighbour"] >= 0.9 * cap), None)
                f = lambda x: "> 2 h" if x is None else f"{x:.0f} min"
                res_t.append(f"{f(t50)} / {f(t90)}")
            else:
                res_t.append(f"{res[0.5]['neighbour']:.0f} t")
        print(f"{D:g} (welded) | {leak_ratio(D)*30*KNOBS['welded']:.3g} | {res_t[0]} | {res_t[1]}")
    print("  (a 1940s destroyer's own pumps: ~25-100 t/h a space; portable pumps up to ~230 t/h)")


def table_sensitivity():
    print("\n## 8. Sensitivity: 7.1's space behind a holding bulkhead at D 1.5, 18 kn; water (t) as one knob moves")
    print("knob | value | no DC, at 2 h | drilled DC, at 8 h")
    base = dict(KNOBS)
    med = lambda xs: sorted(xs)[len(xs) // 2]
    for knob, vals in [("r1", (0.1, 0.3, 1.0)), ("r5", (30, 100, 300)), ("beta", (0, 1, 2)),
                       ("lam0", (0, 0.1, 0.5)), ("work_max", (2, 4, 8)), ("sea", (0, 1, 3)), ("caulk", (0.05, 0.15, 0.4))]:
        for v in vals:
            KNOBS.clear(); KNOBS.update(base); KNOBS[knob] = v
            a = med([case_nc_inboard(1.5, False, seed=s_)[1][2]["magazine"] for s_ in range(1, 21)])
            b = med([case_nc_inboard(1.5, True, seed=s_)[1][8]["magazine"] for s_ in range(1, 21)])
            print(f"{knob} | {v:g} | {a:.0f} | {b:.0f}")
    KNOBS.clear(); KNOBS.update(base)


def main():
    table_slot()
    table_anchors()
    table_curve()
    table_shock()
    table_fittings()
    table_head()
    table_nc()
    table_chain()
    case_houston()
    case_saratoga()
    case_dd()
    table_sensitivity()


if __name__ == "__main__":
    main()
