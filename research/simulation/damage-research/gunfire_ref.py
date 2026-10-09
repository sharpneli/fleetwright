"""
gunfire_ref.py — reference implementation of the gunfire terminal-effects model
(Naval project, 08-gunfire-effects.md).

Everything the note's tables quote is produced by this file:  python3 gunfire_ref.py
Pure python, no dependencies.  Monte-Carlo sections use a fixed seed.

Units: m, kg, s, mm (plate), t (water).  [INFERRED] constants are tuning values,
not engineering ratings; the note says which historical case each one is fitted to.
Scope: what happens GIVEN a hit. Whether a shell hits (dispersion, fire control)
belongs to the separate ballistics / fire-control model.
"""

import math
import random
from dataclasses import dataclass, field

# =============================================================================
# 1. Shells  (navweaps data; burster in kg of filler; tnt = TNT-equivalence)
# =============================================================================
FILLER_TNT = {"TNT": 1.0, "ExpD": 0.95, "TNA": 1.0, "Shellite": 1.0, "Lyddite": 1.05,
              "BlackPowder": 0.35, "TNT/RDX": 1.15}


@dataclass
class Shell:
    name: str
    cal: float            # m
    mass: float           # kg
    burster: float        # kg filler
    filler: str
    kind: str             # "AP" | "SAP" | "HE"
    mv: float             # m/s
    delay: float = 0.0    # s, base-fuze delay (AP/SAP). HE: nose instantaneous
    quality: str = "ww2"  # row of OUTCOME table
    i_form: float = 1.10  # drag form factor (fitted: 16"/50 Mk 8 and 46 cm T91 tables)

    @property
    def w_tnt(self):
        return self.burster * FILLER_TNT[self.filler]


SHELLS = {s.name: s for s in [
    # destroyer / secondary
    Shell("IJN 12.7cm T0 HE",   0.127, 23.0,   1.88, "TNA",     "HE", 915),
    Shell("USN 5in/38 AAC",     0.127, 25.0,   3.3,  "ExpD",    "HE", 792),
    Shell("USN 5in/38 Common",  0.127, 24.5,   1.2,  "ExpD",    "SAP", 792, 0.01),
    Shell("USN 6in/47 AP SH",   0.152, 59.0,   0.9,  "ExpD",    "AP", 762, 0.01),
    Shell("USN 6in/47 HC",      0.152, 47.6,   6.0,  "ExpD",    "HE", 812),
    # cruiser
    Shell("USN 8in/55 AP SH",   0.203, 152.0,  2.3,  "ExpD",    "AP", 762, 0.02),
    Shell("USN 8in/55 HC",      0.203, 118.0,  9.7,  "ExpD",    "HE", 823),
    Shell("IJN 20cm T91 AP",    0.203, 125.9,  3.11, "TNA",     "AP", 835, 0.40, "ijn_t91"),
    Shell("KM 28cm APC",        0.283, 330.0,  7.03, "TNT",     "AP", 890, 0.035),
    Shell("KM 28cm HE nose",    0.283, 315.0,  23.0, "TNT",     "HE", 890),
    # capital
    Shell("RN 15in APC 1916",   0.381, 871.0,  27.4, "Lyddite", "AP", 752, 0.025, "rn1916"),
    Shell("RN 15in APC Mk XXII",0.381, 879.0,  22.0, "Shellite","AP", 746, 0.025),
    Shell("RN 15in HE Mk VII",  0.381, 879.0,  59.0, "TNT",     "HE", 746),
    Shell("KM 38cm APC",        0.380, 800.0,  21.4, "TNT",     "AP", 820, 0.035),
    Shell("KM 38cm HE nose",    0.380, 800.0,  66.2, "TNT",     "HE", 820),
    Shell("USN 14in/45 AP",     0.356, 680.4,  10.4, "ExpD",    "AP", 792, 0.035),
    Shell("USN 14in/45 HC",     0.356, 578.0,  47.3, "ExpD",    "HE", 834),
    Shell("IJN 36cm T91 AP",    0.356, 673.5,  11.1, "TNA",     "AP", 775, 0.40, "ijn_t91"),
    Shell("IJN 36cm T0 HE",     0.356, 625.0,  29.5, "TNA",     "HE", 805),
    Shell("USN 16in/50 AP Mk8", 0.406, 1225.0, 18.55,"ExpD",    "AP", 762, 0.033),
    Shell("USN 16in/50 HC Mk13",0.406, 862.0,  69.67,"ExpD",    "HE", 820),
    Shell("IJN 46cm T91 AP",    0.460, 1460.0, 23.9, "TNA",     "AP", 780, 0.40, "ijn_t91"),
    Shell("IJN 46cm T0 HE",     0.460, 1360.0, 61.7, "TNA",     "HE", 805),
    Shell("RN 18in/40 APC",     0.457, 1506.0, 54.0, "Lyddite", "AP", 692, 0.025, "rn1916"),
    Shell("RN 18in/40 CPC",     0.457, 1506.0, 110.2,"BlackPowder","SAP", 692, 0.025, "rn1916"),
]}

# =============================================================================
# 2. Exterior ballistics (point mass, exponential atmosphere, generic Cd(M))
#    i_form = 1.10 reproduces the 16"/50 Mk 8 table within ~3 % to 30 kyd and the
#    46 cm T91 points within 1 %.
# =============================================================================
G = 9.81
CD_TABLE = [(0.0, 0.110), (0.7, 0.115), (0.85, 0.125), (0.95, 0.20), (1.0, 0.30),
            (1.1, 0.36), (1.2, 0.365), (1.5, 0.345), (2.0, 0.305), (2.5, 0.275), (3.0, 0.255)]


def _cd(m):
    for (m0, c0), (m1, c1) in zip(CD_TABLE, CD_TABLE[1:]):
        if m <= m1:
            return c0 + (c1 - c0) * (m - m0) / (m1 - m0)
    return CD_TABLE[-1][1]


def _fly(s: Shell, elev, dt=0.02):
    A = math.pi * s.cal ** 2 / 4
    x = y = t = 0.0
    vx, vy = s.mv * math.cos(math.radians(elev)), s.mv * math.sin(math.radians(elev))
    while True:
        v = math.hypot(vx, vy)
        a = math.sqrt(1.4 * 287.05 * max(216.65, 288.15 - 0.0065 * y))
        k = 0.5 * 1.225 * math.exp(-y / 7900) * s.i_form * _cd(v / a) * A / s.mass
        nx, ny = x + vx * dt, y + vy * dt
        vx, vy = vx - k * v * vx * dt, vy - (k * v * vy + G) * dt
        t += dt
        if ny < 0 and t > 0.1:
            return x + (nx - x) * y / (y - ny), t, math.hypot(vx, vy), math.degrees(math.atan2(-vy, vx))
        x, y = nx, ny


_BCACHE = {}


def ballistics(s: Shell, rng):
    """-> (striking velocity m/s, fall angle deg, time of flight s); None if out of range."""
    key = (s.name, round(rng, -2))
    if key in _BCACHE:
        return _BCACHE[key]
    lo, hi = 0.02, 45.0
    if _fly(s, hi)[0] < rng:
        _BCACHE[key] = None
        return None
    for _ in range(32):
        mid = (lo + hi) / 2
        if _fly(s, mid)[0] < rng:
            lo = mid
        else:
            hi = mid
    _, tof, v, fall = _fly(s, (lo + hi) / 2)
    _BCACHE[key] = (v, fall, tof)
    return _BCACHE[key]


# =============================================================================
# 3. Penetration (capability in mm of USN Class A/B-equivalent plate)
#    Garzke & Dulin fit (05_mechanics §A3) for normal impact; obliquity term
#    fitted to the navweaps 16"/50 Mk 8 side/deck tables:
#        vertical plate : × 0.967·cos(ob)^1.59      (±3 % to 30 kyd)
#        horizontal     : × 0.82 ·cos(ob)^1.125     (±5 % to 40 kyd)
#    Small calibres (< 11") are extrapolated — treat ±15 %.
# =============================================================================
def pen_normal_mm(s: Shell, v):
    W, D, V = s.mass / 0.4536, s.cal / 0.0254, v / 0.3048
    return 25.4 * 0.0004689 * W ** 0.55 * D ** -0.65 * V ** 1.1


def pen_mm(s: Shell, v, obliquity_deg, deck=False):
    c = max(1e-3, math.cos(math.radians(obliquity_deg)))
    if s.kind == "HE":                      # nose-fuzed: punches ~0.16–0.25 cal, then bursts
        return 0.20 * s.cal * 1000 * c ** 0.5
    if s.kind == "SAP":
        f = 0.6                             # common/CPC: ~60 % of AP, thin walls
    else:
        f = 1.0
    if deck:
        return f * pen_normal_mm(s, v) * 0.82 * c ** 1.125
    return f * pen_normal_mm(s, v) * 0.967 * c ** 1.59


def spaced(plates_mm):
    """Okun spaced-plate equivalence (05 §A3): (Σ t^1.4)^(1/1.4)."""
    return sum(t ** 1.4 for t in plates_mm) ** (1 / 1.4) if plates_mm else 0.0


def fuze_arm_mm(s: Shell, ob=0.0):
    """Okun Mk 21 base-fuze minimum plate (single plate)."""
    ob = min(ob, 61.0)
    r = math.radians(ob)
    return 0.07 * s.cal * 1000 * ((1 + math.cos(2 * r)) / 2 + 0.4537 * math.sin(r) ** 5.7019)


# =============================================================================
# 4. Outcome table for a hit on armour the shell can defeat (P/T >= 1)
#    Rows from 08 §3.2 — fitted loosely to Jutland, Baden, Bismarck, Jean Bart.
#    PEN_B burst after delay | PIB burst in plate | LO low-order | DUD | BRK holed, shell broken
# =============================================================================
OUTCOME = {
    "rn1916":  dict(PEN_B=0.10, PIB=0.35, LO=0.10, DUD=0.05, BRK=0.40),
    "km1916":  dict(PEN_B=0.40, PIB=0.15, LO=0.10, DUD=0.15, BRK=0.20),
    "rn1918":  dict(PEN_B=0.55, PIB=0.10, LO=0.20, DUD=0.10, BRK=0.05),
    "ww2":     dict(PEN_B=0.75, PIB=0.05, LO=0.07, DUD=0.08, BRK=0.05),
    "ijn_t91": dict(PEN_B=0.70, PIB=0.05, LO=0.07, DUD=0.13, BRK=0.05),
}


def roll(table, rnd):
    x = rnd.random()
    for k, p in table.items():
        x -= p
        if x <= 0:
            return k
    return k


# =============================================================================
# 5. Burst effects  [INFERRED, cube-root scaled, fitted to WDR cases]
#    R_wreck  (internal burst: equipment wrecked, ~80 % crew casualties)
#             = 1.6·W^(1/3) m   → 16" Mk 8: 4.2 m  (Kirishima casemate holes ~10 m dia)
#    R_hole   (contact burst on plate t): 0.45·W^(1/3)·sqrt(6/t) m
#             → IJN 8" common on ¼" STS: 0.9 m (SoDak hit 5: 6 ft hole)
#             → IJN 14" on ¾" plating:  0.78 m (SoDak hit 2: 5×4 ft hole)
#    R_blast  (light structure/injuries) = 3.5·W^(1/3) m
#    Fragments: Okun, in calibres from burst → STS thickness defeated (fraction)
# =============================================================================
FRAG_TABLE = [  # (distance in calibres, t50 in calibres, t10 in calibres)
    (5, 0.080, 0.20), (20, 0.0213, 0.093), (50, 0.0158, 0.05), (100, 0.0113, 0.03), (1000, 0.0006, 0.002)]


def r_wreck(s):
    return 1.6 * s.w_tnt ** (1 / 3)


def r_blast(s):
    return 3.5 * s.w_tnt ** (1 / 3)


def r_hole(s, t_mm):
    return 0.45 * s.w_tnt ** (1 / 3) * math.sqrt(6.0 / max(t_mm, 1.0))


def frag_pen_mm(s, dist_m, pct=50):
    d = max(5.0, dist_m / s.cal)
    col = 1 if pct == 50 else 2
    for a, b in zip(FRAG_TABLE, FRAG_TABLE[1:]):
        if d <= b[0]:
            f = (math.log(d) - math.log(a[0])) / (math.log(b[0]) - math.log(a[0]))
            v = math.exp(math.log(a[col]) + f * (math.log(b[col]) - math.log(a[col])))
            if s.kind == "HE":
                v *= 1.35            # HE fragments: Okun gives 0.11 vs 0.08 cal at contact
            return v * s.cal * 1000
    return 0.0


def frag_reach_m(s, t_mm, pct=50):
    """Distance within which pct % of fragments still defeat t_mm of STS."""
    lo, hi = 0.0, 1000 * s.cal
    for _ in range(40):
        mid = (lo + hi) / 2
        if frag_pen_mm(s, mid, pct) > t_mm:
            lo = mid
        else:
            hi = mid
    return lo


# =============================================================================
# 6. Targets: side-view zones.
#    side/top   projected areas (m²) — side faces catch flat fire, tops plunging fire
#    path       plates met in order [(mm, label, gap m)]; top_path for deck hits
#    depth      m of ship behind the face along the shot (to the far side)
#    heavy      P(the path meets a mass that arms a fuze or stops the shell:
#               turbines/gears (Johnston), slopes, barbettes, far-side TDS)
#    incline    belt incline (deg, top outboard) added to the fall angle
#    waterline  share of side hits that hole at/below the WL;  flood_cap t behind it
#    comp_len   length of the WT compartment behind the face (m) — a burst with
#               R_wreck > 0.4·comp_len also breaches the next bulkhead
#    crew       people per 100 m² of face;  ammo = P(exposed propellant/ready ammo)
#    systems    (name, vulnerable area m², protection mm, below_wl)
# =============================================================================
@dataclass
class Zone:
    name: str
    side: float
    top: float
    path: list
    top_path: list
    depth: float
    heavy: float = 0.0
    incline: float = 0.0
    waterline: float = 0.0
    flood_cap: float = 0.0
    comp_len: float = 10.0
    crew: float = 0.0
    systems: tuple = ()
    fire_load: float = 1.0
    ammo: float = 0.0


@dataclass
class Target:
    name: str
    L: float
    B: float
    h_eff: float                  # mean silhouette height above WL (danger space)
    disp: float
    reserve: float                # t of flooding that sinks it
    zones: list = field(default_factory=list)


S_ = lambda n, a, p=0, wl=False: (n, a, p, wl)

FLETCHER = Target("Fletcher DD", 114.7, 12.0, 7.0, 2900, 800, [
    Zone("hull side fwd", 190, 380, [(9, "side", 11)], [(9, "deck", 4)], 11, heavy=0.05, waterline=0.45,
         flood_cap=250, comp_len=11, crew=3, fire_load=1.0, ammo=0.05,
         systems=(S_("fwd magazine", 25, 0, True), S_("berthing/messing", 60), S_("sonar/IC room", 15, 0, True))),
    Zone("hull side machinery", 180, 140, [(12, "side", 2)], [(9, "deck", 3)], 12, heavy=0.55, waterline=0.5,
         flood_cap=420, comp_len=10, crew=2, fire_load=0.5,
         systems=(S_("fireroom", 60, 0, True), S_("engine room", 60, 0, True), S_("main steam", 30, 0, True),
                  S_("generator/switchboard", 15, 0, True))),
    Zone("hull side aft", 160, 330, [(9, "side", 11)], [(9, "deck", 4)], 11, heavy=0.05, waterline=0.45,
         flood_cap=250, comp_len=11, crew=3, fire_load=1.0, ammo=0.08,
         systems=(S_("aft magazine", 25, 0, True), S_("steering", 15, 0, True), S_("depth charges", 10))),
    Zone("bridge/director", 90, 60, [(6, "bridge", 8)], [(6, "roof", 3)], 8, crew=6, fire_load=1.5, ammo=0.05,
         systems=(S_("bridge crew", 35), S_("main director", 10, 13), S_("search radar", 6), S_("CIC/radio", 20))),
    Zone("deckhouse/tubes/funnels", 160, 260, [(5, "house", 6)], [(5, "deck", 3)], 6, crew=3, fire_load=1.2, ammo=0.2,
         systems=(S_("torpedo mount", 25), S_("uptake", 30), S_("40mm mount", 15))),
    Zone("5in mounts", 60, 60, [(3, "shield", 4)], [(3, "roof", 2)], 4, heavy=0.15, crew=8, fire_load=1.0, ammo=0.4,
         systems=(S_("5in mount", 40),)),
])

# South Dakota-like battleship (311 mm belt @19° behind 32 mm hull, 146+38 mm decks,
# 457/184 mm turrets, 439 mm barbettes, 406 mm CT)
SODAK = Target("SoDak BB", 207.0, 33.0, 13.0, 44500, 14000, [
    Zone("belt (citadel WL)", 420, 0, [(32, "outer", 0.5), (311, "belt", 6)], [], 30, heavy=0.9, incline=19,
         waterline=0.6, flood_cap=600, comp_len=15, crew=0.5, fire_load=0.3,
         systems=(S_("machinery space", 120, 0, True), S_("magazine", 60, 0, True), S_("TDS tank", 200, 0, True))),
    Zone("upper side citadel", 315, 3000, [(38, "side", 3)], [(38, "bomb deck", 2.4), (146, "armour deck", 0)], 30,
         heavy=0.5, crew=2, fire_load=1.0, ammo=0.05,
         systems=(S_("berthing", 150), S_("5in handling", 40), S_("cable runs", 60))),
    Zone("unarmoured ends", 600, 2300, [(16, "side", 15)], [(25, "deck", 2.4), (16, "deck", 2.4)], 25, heavy=0.05,
         waterline=0.4, flood_cap=900, comp_len=15, crew=1, fire_load=0.8,
         systems=(S_("tanks/stores", 300, 0, True), S_("berthing", 150), S_("steering (boxed)", 20, 343, True))),
    Zone("main turrets", 210, 240, [(457, "face", 2)], [(184, "roof", 2)], 10, heavy=0.8, crew=10, fire_load=0.2, ammo=0.3,
         systems=(S_("turret", 210, 0),)),
    Zone("barbettes (exposed)", 60, 0, [(439, "barbette", 1)], [], 8, heavy=0.8, crew=2, fire_load=0.1, ammo=0.4,
         systems=(S_("turret train/hoists", 60, 0),)),
    Zone("conning tower", 20, 15, [(406, "CT", 1)], [(184, "roof", 1)], 4, heavy=0.5, crew=10, fire_load=0.1,
         systems=(S_("CT crew", 20, 0),)),
    Zone("fwd superstructure", 480, 500, [(10, "house", 10)], [(10, "deck", 3)], 14, crew=3, fire_load=1.3, ammo=0.05,
         systems=(S_("bridge/flag", 60), S_("main director", 15, 38), S_("5in director", 10, 25), S_("CIC/radio", 40),
                  S_("cable runs", 60))),
    Zone("aft superstructure/funnel", 300, 450, [(10, "house", 8)], [(10, "deck", 3)], 12, crew=2, fire_load=1.3, ammo=0.1,
         systems=(S_("aft director", 15, 38), S_("uptake", 80), S_("aircraft/catapult", 40))),
    Zone("5in mounts", 80, 100, [(19, "mount", 3)], [(19, "roof", 2)], 4, heavy=0.2, crew=6, fire_load=0.8, ammo=0.35,
         systems=(S_("5in mount", 80, 19),)),
    Zone("open AA positions", 100, 250, [(6, "shield", 3)], [], 3, crew=12, fire_load=1.0, ammo=0.5,
         systems=(S_("AA mount", 60), S_("AA director", 10))),
    Zone("masts/radar", 60, 10, [(5, "mast", 2)], [], 2, crew=0.5, fire_load=0.2,
         systems=(S_("search radar", 15), S_("FC radar", 8), S_("W/T aerials", 20))),
])

TARGETS = {t.name: t for t in (FLETCHER, SODAK)}


# =============================================================================
# 7. Where a hit lands: side or top.  Given that the shell HAS hit (whether it
#    hits is the fire-control model's job), the fall angle decides the face:
#    P(side) = (h_eff / tan fall) / footprint depth  — the danger-space share.
# =============================================================================
def footprint(t: Target, fall_deg, aspect_deg=90.0):
    """Range depth and deflection width of the target as the shell sees it."""
    a = math.radians(aspect_deg)
    depth = abs(t.B * math.sin(a)) + abs(t.L * math.cos(a)) + t.h_eff / math.tan(math.radians(max(fall_deg, 0.5)))
    width = abs(t.L * math.sin(a)) + abs(t.B * math.cos(a))
    return depth, width


def side_share(t: Target, fall_deg, aspect_deg=90.0):
    depth, _ = footprint(t, fall_deg, aspect_deg)
    return (t.h_eff / math.tan(math.radians(max(fall_deg, 0.5)))) / depth


# =============================================================================
# 8. Per-hit resolution
# =============================================================================
FIRE_P = {"HE": 0.45, "SAP": 0.30, "AP": 0.15}       # per burst in zone (WWII), × zone.fire_load


def pick_zone(t: Target, side_share, rnd):
    face = "side" if rnd.random() < side_share else "top"
    areas = [(z, z.side if face == "side" else z.top) for z in t.zones]
    tot = sum(a for _, a in areas)
    x = rnd.random() * tot
    for z, a in areas:
        x -= a
        if x <= 0:
            return z, face
    return areas[-1][0], face


def resolve(s: Shell, t: Target, rng, rnd, zone=None, face=None, target_angle=0.0):
    """One hit. Returns dict: outcome, burst ('outside'|'in plate'|'inside'|None),
    flood_t, fire, casualties, systems_hit[], zone."""
    v, fall, _ = ballistics(s, rng)
    if zone is None:
        zone, face = pick_zone(t, side_share(t, fall), rnd)
    path = zone.path if face == "side" else zone.top_path
    if not path:
        path = zone.path
    ob = (math.degrees(math.acos(math.cos(math.radians(fall + zone.incline)) * math.cos(math.radians(target_angle))))
          if face == "side" else 90 - fall)
    deck = face == "top"
    res = dict(zone=zone.name, face=face, outcome=None, burst=None, flood_t=0.0, fire=False,
               casualties=0.0, systems=[], dist_inside=0.0)
    armour = [mm for mm, *_ in path]
    total = spaced(armour)
    cap = pen_mm(s, v, ob, deck)
    size = (s.w_tnt / 10) ** (1 / 3)                        # 1.0 ≈ 10 kg TNT (14"/15" AP)

    # ---- HE: bursts on first plate; holes it if thin, else burst outside --------
    if s.kind == "HE":
        first = path[0][0]
        if first <= cap:
            res["outcome"], res["burst"] = "HE burst just inside", "inside"
        else:
            res["outcome"], res["burst"] = "HE burst on armour", "outside"
    else:
        # ---- AP/SAP: ricochet check, then fuze arming, then penetration ----------
        ric = 0.0 if ob < 45 else min(1.0, (ob - 45) / 20)
        if total > cap and rnd.random() < ric:
            res["outcome"] = "ricochet"
            res["burst"] = "outside" if rnd.random() < 0.35 else None
        elif total > cap * rnd.uniform(0.9, 1.1):
            res["outcome"] = "defeated (shatter/no pen)"
            res["burst"] = "outside" if rnd.random() < 0.5 else None
        else:
            armed = any(mm >= fuze_arm_mm(s, ob if deck or i == 0 else 0) for i, (mm, *_) in enumerate(path))
            if not armed and rnd.random() < zone.heavy:
                armed = True                                   # met a turbine/gear/boiler
            if not armed:
                res["outcome"] = "over-penetration (no arm)"
                res["burst"] = None
                # still: holes, cables, people along path
                res["dist_inside"] = zone.depth
            else:
                o = roll(OUTCOME[s.quality], rnd)
                if o == "DUD" or o == "BRK":
                    res["outcome"] = "holed, dud/broken"
                    res["burst"] = None
                elif o == "PIB":
                    res["outcome"], res["burst"] = "burst in plate", "in plate"
                else:
                    margin = max(0.0, 1 - total / cap)
                    v_res = v * math.sqrt(margin) if total > 0 else v
                    d = s.delay * v_res * rnd.uniform(0.7, 1.1)
                    if d > zone.depth and not (rnd.random() < zone.heavy):
                        res["outcome"], res["burst"] = "penetrated, burst beyond far side", None
                        res["dist_inside"] = zone.depth
                    else:
                        res["outcome"] = "penetrated, low-order" if o == "LO" else "penetrated, burst inside"
                        res["burst"] = "inside"
                        res["dist_inside"] = min(d, zone.depth)

    # ---- effects -----------------------------------------------------------------
    k = {"inside": 1.0, "in plate": 0.4, "outside": 0.15, None: 0.0}[res["burst"]]
    if res["outcome"] and "low-order" in res["outcome"]:
        k *= 0.45
    passed = res["burst"] is None and res["outcome"] and ("over-pen" in res["outcome"] or "holed" in res["outcome"]
                                                          or "beyond" in res["outcome"])
    face_area = max(zone.side if face == "side" else zone.top, 1.0)
    # waterline holes → flooding (a burst that out-reaches 0.4·comp_len breaches the next bulkhead)
    wl_hit = face == "side" and zone.waterline and rnd.random() < zone.waterline
    if wl_hit and res["outcome"] != "ricochet" and "defeated" not in res["outcome"] \
            and "burst on armour" not in res["outcome"]:
        if res["burst"] in ("inside", "in plate"):
            hole = r_hole(s, path[0][0])
            frac = min(1.0, 0.25 + (hole / 2.0) ** 2)
            if r_wreck(s) * (1 if res["burst"] == "inside" else 0.5) > 0.4 * zone.comp_len:
                frac *= 2.0
                res["systems"].append("WT BULKHEAD BREACHED")
        else:
            frac = 0.2                                        # two shell-sized holes; pumps can hold it
        res["flood_t"] = zone.flood_cap * frac * rnd.uniform(0.6, 1.2)
    # systems: P = (A_sys + π·R²)/A_face, protection cuts fragment reach, below-WL kit
    # is reached mainly by waterline hits
    R = r_wreck(s) * math.sqrt(k) if k else 0.0
    outside = res["burst"] == "outside" or (res["outcome"] and ("defeated" in res["outcome"] or res["outcome"] == "ricochet"))
    for name, a_sys, prot, below in zone.systems:
        if outside:
            continue                                          # zone armour kept it out (see non-pen effects)
        if passed:
            p = a_sys / face_area                             # the shell body itself
        else:
            r_eff = R if prot <= frag_pen_mm(s, 2.0) else 0.0  # own splinter box / armoured box
            p = k * (a_sys + math.pi * r_eff ** 2) / face_area
            if prot > frag_pen_mm(s, 2.0):
                p *= 0.1                                      # only a direct strike on the box
        if below and not wl_hit:
            p *= 0.5
        if rnd.random() < min(0.95, p):
            res["systems"].append(name)
    # non-penetrating heavy hits on turrets/barbettes/CT (08 §5): jam, stun, spall
    if outside and zone.name in ("main turrets", "barbettes (exposed)", "conning tower"):
        heavy_shell = s.cal >= 0.27
        if rnd.random() < (0.35 if heavy_shell else (0.05 if s.cal >= 0.2 else 0.01)):
            res["systems"].append("turret JAMMED (non-pen)" if zone.name != "conning tower" else "CT vision/comms lost")
        if heavy_shell and rnd.random() < 0.5:
            res["systems"].append("crew stunned 2–5 min")
    if outside and wl_hit and s.cal >= 0.27 and rnd.random() < 0.4:
        res["systems"].append("belt seam leak")
        res["flood_t"] += rnd.uniform(5, 50)                  # t over the first hour
    # fire
    if res["burst"]:
        pf = FIRE_P[s.kind] * zone.fire_load * min(1.5, 0.5 + 0.5 * size) * (1.0 if res["burst"] != "outside" else 0.4)
        res["fire"] = rnd.random() < pf
        if zone.ammo and rnd.random() < zone.ammo * k * 0.6:
            res["systems"].append("READY-AMMO FIRE")
            res["fire"] = True
    # casualties: crew density × lethal area (wreck radius 80 %, blast ring 20 %)
    dens = zone.crew / 100 * 2.5
    if res["burst"]:
        rw, rb = r_wreck(s) * math.sqrt(k), r_blast(s) * math.sqrt(k)
        base = dens * (math.pi * rw ** 2 * 0.8 + math.pi * (rb ** 2 - rw ** 2) * 0.2)
    else:
        base = dens * zone.depth * 1.0 * 0.5                  # 1 m-wide path through the space
    res["casualties"] = base * rnd.uniform(0.3, 1.7)
    return res


def mc(s, t, rng, n=6000, seed=7, **kw):
    rnd = random.Random(seed)
    agg = dict(n=n, outcomes={}, flood=0.0, fire=0, cas=0.0, sys={}, zones={})
    for _ in range(n):
        r = resolve(s, t, rng, rnd, **kw)
        agg["outcomes"][r["outcome"]] = agg["outcomes"].get(r["outcome"], 0) + 1
        agg["zones"][r["zone"]] = agg["zones"].get(r["zone"], 0) + 1
        agg["flood"] += r["flood_t"]
        agg["fire"] += r["fire"]
        agg["cas"] += r["casualties"]
        for x in r["systems"]:
            agg["sys"][x] = agg["sys"].get(x, 0) + 1
    return agg


# =============================================================================
# 9. Report
# =============================================================================
def table_shells():
    print("## A. Shells and derived burst radii\n")
    print("| Shell | cal mm | kg | burster kg | % | TNT-eq kg | R_wreck m | R_blast m | hole in 6 mm m | frag ≥6 mm (50 %) m | frag ≥25 mm (10 %) m |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")
    for s in SHELLS.values():
        print(f"| {s.name} | {s.cal*1000:.0f} | {s.mass:.0f} | {s.burster:.1f} | {100*s.burster/s.mass:.1f} | {s.w_tnt:.1f} "
              f"| {r_wreck(s):.1f} | {r_blast(s):.1f} | {r_hole(s, 6):.1f} | {frag_reach_m(s, 6):.0f} | {frag_reach_m(s, 25, 10):.0f} |")


def table_ballistics():
    print("\n## B. Ballistics check (model vs navweaps)\n")
    print("| Shell | range | v model | v table | fall model | fall table |")
    print("|---|---|---|---|---|---|")
    YD = 0.9144
    for ry, sv, fa in ((10000, 2074, 5.01), (20000, 1740, 14.92), (30000, 1567, 28.25), (40000, 1607, 47.73)):
        v, f, _ = ballistics(SHELLS["USN 16in/50 AP Mk8"], ry * YD)
        print(f"| 16in Mk8 | {ry} yd | {v/0.3048:.0f} fps | {sv} fps | {f:.1f}° | {fa}° |")
    for rm, sv, fa in ((5000, 690, 3.3), (10000, 620, 7.2), (15000, 562, 11.5)):
        v, f, _ = ballistics(SHELLS["IJN 46cm T91 AP"], rm)
        print(f"| 46cm T91 | {rm} m | {v:.0f} m/s | {sv} m/s | {f:.1f}° | {fa}° |")


def table_pen():
    print("\n## C. Penetration vs range (mm; belt = vertical, target angle 0; deck = horizontal)\n")
    rngs = [5000, 10000, 15000, 20000, 25000, 30000]
    print("| Shell | " + " | ".join(f"{r//1000} km belt/deck" for r in rngs) + " |")
    print("|---|" + "---|" * len(rngs))
    for nm in ("USN 6in/47 AP SH", "USN 8in/55 AP SH", "KM 28cm APC", "USN 14in/45 AP", "RN 15in APC Mk XXII",
               "KM 38cm APC", "USN 16in/50 AP Mk8", "IJN 46cm T91 AP", "RN 18in/40 APC"):
        s = SHELLS[nm]
        cells = []
        for r in rngs:
            b = ballistics(s, r)
            if not b:
                cells.append("–")
                continue
            v, f, _ = b
            cells.append(f"{pen_mm(s, v, f):.0f} / {pen_mm(s, v, 90 - f, deck=True):.0f}")
        print(f"| {nm} | " + " | ".join(cells) + " |")
    print("\nCalibration: navweaps 16\"/50 Mk 8 side 664/509/380 mm, deck 43/99/169 mm at 10/20/30 kyd.")
    for ry in (10000, 20000, 30000):
        v, f, _ = ballistics(SHELLS["USN 16in/50 AP Mk8"], ry * 0.9144)
        s = SHELLS["USN 16in/50 AP Mk8"]
        print(f"  model {ry} yd: side {pen_mm(s, v, f):.0f} mm, deck {pen_mm(s, v, 90-f, True):.0f} mm")


def table_fuze():
    print("\n## D. Base-fuze arming thresholds (mm, single plate) — what passes straight through\n")
    print("| Shell | 0° | 30° | 45° | 60° | arms on DD side (9–12 mm)? | arms on 10 mm superstructure? |")
    print("|---|---|---|---|---|---|---|")
    for nm in ("USN 6in/47 AP SH", "USN 8in/55 AP SH", "IJN 20cm T91 AP", "USN 14in/45 AP", "USN 16in/50 AP Mk8", "IJN 46cm T91 AP"):
        s = SHELLS[nm]
        a = [fuze_arm_mm(s, o) for o in (0, 30, 45, 60)]
        print(f"| {nm} | " + " | ".join(f"{x:.0f}" for x in a) + f" | {'yes' if a[0] <= 12 else 'no'} | {'yes' if a[0] <= 10 else 'no'} |")


def table_faces():
    print("\n## E. Given a hit: share landing on the side (vs deck/top), broadside-on target\n")
    print("| Shell | range | fall | Fletcher side share | SoDak side share |")
    print("|---|---|---|---|---|")
    for nm in ("USN 5in/38 AAC", "USN 8in/55 AP SH", "USN 16in/50 AP Mk8", "IJN 46cm T0 HE"):
        for r in (5000, 10000, 15000, 20000, 25000):
            b = ballistics(SHELLS[nm], r)
            if not b:
                continue
            f = b[1]
            print(f"| {nm} | {r//1000} km | {f:.1f}° | {100*side_share(FLETCHER, f):.0f} % | {100*side_share(SODAK, f):.0f} % |")


def table_outcomes():
    print("\n## F. Per-hit outcomes (Monte Carlo, random zone by projected area)\n")
    cases = [("USN 5in/38 AAC", "Fletcher DD", 8000), ("IJN 12.7cm T0 HE", "Fletcher DD", 8000),
             ("USN 6in/47 HC", "Fletcher DD", 10000), ("USN 8in/55 AP SH", "Fletcher DD", 12000),
             ("IJN 20cm T91 AP", "Fletcher DD", 12000), ("USN 16in/50 AP Mk8", "Fletcher DD", 15000),
             ("USN 16in/50 HC Mk13", "Fletcher DD", 15000), ("IJN 46cm T91 AP", "Fletcher DD", 15000),
             ("IJN 46cm T0 HE", "Fletcher DD", 15000),
             ("USN 5in/38 AAC", "SoDak BB", 8000), ("USN 8in/55 AP SH", "SoDak BB", 12000),
             ("IJN 36cm T0 HE", "SoDak BB", 8000), ("USN 16in/50 AP Mk8", "SoDak BB", 10000),
             ("USN 16in/50 AP Mk8", "SoDak BB", 20000), ("USN 16in/50 AP Mk8", "SoDak BB", 28000),
             ("IJN 46cm T91 AP", "SoDak BB", 20000), ("IJN 46cm T0 HE", "SoDak BB", 20000)]
    print("| Shell → target @ range | burst inside | over-pen / no burst | defeated / ricochet | E[flood t] | P(fire) | E[cas] | top systems hit (per 100 hits) |")
    print("|---|---|---|---|---|---|---|---|")
    for nm, tn, r in cases:
        a = mc(SHELLS[nm], TARGETS[tn], r)
        o, n = a["outcomes"], a["n"]
        inside = sum(c for k, c in o.items() if k and ("inside" in k or "low-order" in k or "in plate" in k))
        nob = sum(c for k, c in o.items() if k and ("over-pen" in k or "dud" in k or "beyond" in k))
        dfd = sum(c for k, c in o.items() if k and ("defeated" in k or "ricochet" in k or "on armour" in k))
        top = sorted(a["sys"].items(), key=lambda kv: -kv[1])[:4]
        print(f"| {nm} → {tn} @ {r//1000} km | {100*inside/n:.0f} % | {100*nob/n:.0f} % | {100*dfd/n:.0f} % | {a['flood']/n:.0f} "
              f"| {100*a['fire']/n:.0f} % | {a['cas']/n:.1f} | " + ", ".join(f"{k} {100*c/n:.0f}" for k, c in top) + " |")


def scenario_dd_vs_bb():
    print("\n## G. A SoDak takes N hits of 5\"/38 AAC at 7 km (zones by projected area)\n")
    s, t, trials = SHELLS["USN 5in/38 AAC"], SODAK, 400
    Ns = (10, 30, 75)
    cols = {}
    for N in Ns:
        rnd = random.Random(3 + N)
        lost, fires, cas = {}, 0.0, 0.0
        for _ in range(trials):
            got = set()
            for _ in range(N):
                r = resolve(s, t, 7000, rnd)
                got.update(r["systems"])
                fires += r["fire"]
                cas += r["casualties"]
            for x in got:
                lost[x] = lost.get(x, 0) + 1
        cols[N] = (fires / trials, cas / trials, lost)
    print("| | " + " | ".join(f"{N} hits" for N in Ns) + " |\n|---|" + "---|" * len(Ns))
    print("| E[fires started] | " + " | ".join(f"{c[0]:.0f}" for c in cols.values()) + " |")
    print("| E[casualties] | " + " | ".join(f"{c[1]:.0f}" for c in cols.values()) + " |")
    names = sorted(set(k for c in cols.values() for k in c[2]), key=lambda k: -cols[Ns[-1]][2].get(k, 0))
    for k in names:
        print(f"| P({k} damaged ≥1×) | " + " | ".join(f"{100*c[2].get(k, 0)/trials:.0f} %" for c in cols.values()) + " |")


DD_MACH = ("fireroom", "engine room", "main steam")


def dd_status(results, rnd):
    """Crude ship-level outcome for a Fletcher from a list of per-hit results  [INFERRED]
    crippled : any machinery node lost, or > 300 t aboard  (Johnston, Hoel, Roberts)
    lost     : flooding past reserve; or magazine hit → 25 % prompt explosion;
               or ≥ 6 fires → 30 % fire reaches a magazine later (Cushing, Monssen)"""
    flood = sum(r["flood_t"] for r in results)
    sysl = [x for r in results for x in r["systems"]]
    fires = sum(r["fire"] for r in results)
    crippled = any(x in DD_MACH for x in sysl) or flood > 300
    lost = flood > FLETCHER.reserve * rnd.uniform(0.7, 1.2)
    if any("magazine" in x for x in sysl) and rnd.random() < 0.25:
        lost = True
    if fires >= 6 and rnd.random() < 0.3:
        lost = True
    return crippled or lost, lost, flood


def scenario_n_hits_dd():
    print("\n## H. Fletcher state after N hits (Monte Carlo, hits at random zones)\n")
    print("| Shell (range) | N=1 crippled / lost | N=2 | N=3 | N=5 | N=10 | N=20 | N=40 |")
    print("|---|---|---|---|---|---|---|---|")
    for nm, r in (("USN 5in/38 AAC", 8000), ("IJN 12.7cm T0 HE", 8000), ("USN 6in/47 HC", 10000),
                  ("USN 8in/55 AP SH", 12000), ("USN 8in/55 HC", 12000), ("IJN 20cm T91 AP", 12000),
                  ("USN 16in/50 AP Mk8", 15000), ("IJN 46cm T91 AP", 15000), ("IJN 46cm T0 HE", 15000)):
        s = SHELLS[nm]
        cells = []
        for n in (1, 2, 3, 5, 10, 20, 40):
            rnd = random.Random(5 + n)
            T, c, l = 1500, 0, 0
            for _ in range(T):
                cr, lo, _ = dd_status([resolve(s, FLETCHER, r, rnd) for _ in range(n)], rnd)
                c += cr
                l += lo
            cells.append(f"{100*c/T:.0f} / {100*l/T:.0f} %")
        print(f"| {nm} ({r//1000} km) | " + " | ".join(cells) + " |")


def scenario_broadside_hits_dd():
    print("\n## I. One heavy broadside lands k hits on a Fletcher at 15 km\n")
    print("| Shell | k=1 crippled / lost | k=2 | k=3 | k=4 | E[water t] per hit | E[cas] per hit |")
    print("|---|---|---|---|---|---|---|")
    for nm in ("IJN 46cm T0 HE", "IJN 46cm T91 AP", "RN 18in/40 CPC", "RN 18in/40 APC", "USN 16in/50 HC Mk13", "USN 16in/50 AP Mk8"):
        s = SHELLS[nm]
        if not ballistics(s, 15000):
            continue
        cells = []
        for k_ in (1, 2, 3, 4):
            rnd = random.Random(40 + k_)
            T, c, l = 3000, 0, 0
            for _ in range(T):
                cr, lo, _ = dd_status([resolve(s, FLETCHER, 15000, rnd) for _ in range(k_)], rnd)
                c += cr
                l += lo
            cells.append(f"{100*c/T:.0f} / {100*l/T:.0f} %")
        a_ = mc(s, FLETCHER, 15000, n=4000)
        print(f"| {nm} | " + " | ".join(cells) + f" | {a_['flood']/a_['n']:.0f} | {a_['cas']/a_['n']:.1f} |")


def table_bb_vs_bb():
    print("\n## J. Capital-ship AP against the SoDak: per-hit outcomes by range\n")
    print("| Shell | range | P(belt or deck hit) | P(citadel burst) | P(turret/barbette out) | P(fire) | E[flood t] | E[cas] |")
    print("|---|---|---|---|---|---|---|---|")
    for nm in ("USN 14in/45 AP", "RN 15in APC Mk XXII", "RN 15in APC 1916", "KM 38cm APC", "USN 16in/50 AP Mk8", "IJN 46cm T91 AP"):
        s = SHELLS[nm]
        for r in (8000, 15000, 22000, 28000):
            if not ballistics(s, r):
                continue
            rnd = random.Random(21)
            n, arm, cit, tur, fire, fl, cas = 6000, 0, 0, 0, 0, 0.0, 0.0
            for _ in range(n):
                x = resolve(s, SODAK, r, rnd)
                is_arm = x["zone"] == "belt (citadel WL)" or (x["zone"] == "upper side citadel" and x["face"] == "top")
                arm += is_arm
                cit += is_arm and x["burst"] == "inside"
                tur += any(k in ("turret", "turret train/hoists", "turret JAMMED (non-pen)") for k in x["systems"])
                fire += x["fire"]
                fl += x["flood_t"]
                cas += x["casualties"]
            print(f"| {nm} | {r//1000} km | {100*arm/n:.0f} % | {100*cit/n:.1f} % | {100*tur/n:.1f} % | {100*fire/n:.0f} % | {fl/n:.0f} | {cas/n:.1f} |")


def table_immune():
    print("\n## K. Immune zone of the SoDak scheme (311 mm @19° + 32 mm hull; 38+146 mm decks), broadside-on\n")
    belt = spaced([32, 311])
    deck = spaced([38, 146])
    print("| Shell | belt defeated out to | deck defeated from | immune zone |")
    print("|---|---|---|---|")
    for nm in ("USN 14in/45 AP", "RN 15in APC Mk XXII", "KM 38cm APC", "USN 16in/50 AP Mk8", "IJN 46cm T91 AP", "RN 18in/40 APC"):
        s = SHELLS[nm]
        b_out, d_in = 0, None
        for r in range(2000, 40001, 500):
            bb = ballistics(s, r)
            if not bb:
                break
            v, f, _ = bb
            if pen_mm(s, v, f + 19) >= belt:
                b_out = r
            if d_in is None and pen_mm(s, v, 90 - f, True) >= deck:
                d_in = r
        zone = f"{b_out/1000:.1f}–{d_in/1000:.1f} km" if d_in and d_in > b_out else ("none" if d_in else f"> {b_out/1000:.1f} km")
        print(f"| {nm} | {b_out/1000:.1f} km | {'' if d_in is None else f'{d_in/1000:.1f} km'} | {zone} |")


if __name__ == "__main__":
    table_shells()
    table_ballistics()
    table_pen()
    table_fuze()
    table_faces()
    table_outcomes()
    scenario_dd_vs_bb()
    table_immune()
    table_bb_vs_bb()
    scenario_n_hits_dd()
    scenario_broadside_hits_dd()
