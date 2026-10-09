"""
manual_gunnery_ref.py — gunnery without computers: the gunlayer, the roll, the smoke and the splash
(Naval project, manual-gunnery-research.md). Extends fire_control_ref.py downward in time
(1860-1910) and sideways into emergencies (director lost, power lost, night melee, MK1 eyeball).

Run:  python3 manual_gunnery_ref.py            (all tables, ~10 min)
      python3 manual_gunnery_ref.py quick      (physics tables only)
      python3 manual_gunnery_ref.py <table>    (one table)

What is modelled per SHOT (each gun fires on its own unless salvo firing is ordered):
  1. the gunlayer's elevation error - set by HOW he lays: firing on the roll (timing error x roll rate),
     at the end of the roll (roll-to-roll amplitude scatter), or continuous aim (tracking error), plus the
     sight's own error (open sights vs telescope), the gun's own error and combat stress;
  2. the range set on the sight - from the eye, stadimeter or rangefinder, passed by voice pipe,
     with no allowance for range rate before the Dumaresq (1902-04);
  3. the deflection - from an estimate of the enemy's speed and course;
  4. smoke - black/brown powder blinds the layer after each shot; he waits for it to clear;
  5. spotting - can anyone tell THIS gun's splash from the others? (independent fire vs salvos);
  6. blunders - wrong range on the sight, wrong ship, fired off the target.
Hits are tested against the real hull box (fire_control_ref.hit_test).

Tags: [S] sourced (note + appendices 07/08/09), [INFERRED] fit/tuning, [UNCERTAIN] thin sources.
"""

import math
import sys
from dataclasses import dataclass, field, replace

import numpy as np

import fire_control_ref as fc
from fire_control_ref import Gun, GUNS, TargetClass, TARGETS, hit_test, KN, YD, MIL, KYD, _wrap

ARCMIN = math.radians(1 / 60)

# =============================================================================
# 1. Guns of the era (point-mass ballistics fitted to max range; [S] navweaps unless marked)
# =============================================================================
OLD = {g.name: g.fit() for g in [
    Gun("12in RML 35-ton (1870s)", 0.305, 320, 424, 10, 5500, 2.0, train_rate=0.5),          # range [INFERRED]
    Gun("13.5in/30 BL (1880s, brown)", 0.343, 567, 614, 13.5, 10930, 2.0, train_rate=1.0),
    Gun("13in/35 USN (1898, brown)", 0.330, 513, 610, 15, 10970, 2.0, train_rate=1.0),
    Gun("12in/25 Krupp (Dingyuan 1894)", 0.305, 330, 500, 12, 7800, 2.0, train_rate=0.5),    # [UNCERTAIN]
    Gun("12in/35 Mk VIII (1895)", 0.305, 386, 716, 13.5, 12710, 2.0, train_rate=1.5),
    Gun("12in/40 (1901-05)", 0.305, 386, 732, 15, 13700, 2.0, train_rate=2.0),
    Gun("8in/35 USN (1898)", 0.203, 113, 640, 13, 9140, 2.0, train_rate=2.0),                # range [INFERRED]
    Gun("6in/40 QF (1890s)", 0.152, 45.4, 670, 15, 9140, 2.0, train_rate=4.0),
    Gun("4.7in/40 QF (1890s)", 0.120, 20.4, 655, 20, 9000, 2.0, train_rate=6.0),             # [INFERRED]
    Gun("12-pdr 12 cwt QF", 0.076, 5.67, 792, 20, 8500, 2.0, train_rate=10.0),
]}
O = OLD

# targets
PRE = TargetClass("pre-dreadnought", 120, 23, 8.0, 30, 0.6, 16, 1.0)
CRU = TargetClass("armoured cruiser", 110, 19, 6.5, 28, 0.5, 19, 1.2)
SCREEN = TargetClass("practice screen 20 x 17 ft", 6.1, 0.3, 5.2, 8, 0.01, 0, 0.0)   # [S] RN/USN screen
HULK = TargetClass("hulk / anchored ship", 90, 15, 6.0, 25, 0.4, 0, 0.0)


# =============================================================================
# 2. The human at the gun
# =============================================================================
@dataclass
class Skill:
    name: str
    timing: float      # s, 1-sigma error in the instant of firing on the roll (reaction + anticipation scatter)
    resid: float       # s, uncompensated firing delay (anticipation not learned) [S: 0.2-0.3 s total delay]
    track: float       # arcmin of tracking error per deg/s of roll rate under continuous aim
    lead: float        # multiplier on deflection (aim-off) error
    blunder: float     # chance per shot of a gross error (wrong range set, wrong ship, fired off target)


SKILLS = {  # [INFERRED: Scott - only 1-2 % reliable roll-firers; prize firing 24-35 % fleet vs 80 % crack]
    "green":   Skill("green", 0.15, 0.15, 3.5, 1.5, 0.05),
    "average": Skill("average", 0.10, 0.08, 2.0, 1.0, 0.03),
    "trained": Skill("trained", 0.06, 0.04, 1.0, 0.8, 0.012),
    "crack":   Skill("crack", 0.035, 0.02, 0.6, 0.6, 0.005),
}

SIGHT_SIG = {"open": 4.0, "tele_early": 2.5, "tele": 1.2, "ring": 6.0}   # arcmin 1 sigma [S Yorktown 1892: open ~3x worse]
SMOKE_BASE = {"black": 30.0, "brown": 25.0, "smokeless": 5.0}           # s blind after a 13in shot, calm [S USNI 1901]


@dataclass
class Group:
    name: str
    gun: Gun
    n: int
    cycle: float               # s per round per gun at drill [S table 3 of appendix 07]
    sight: str = "open"
    method: str = "roll"       # roll | roll_end | continuous | stabilised
    elev_rate: float = 3.0     # deg/s the layer can drive the elevation (continuous aim needs > roll rate)
    powder: str = "smokeless"
    skill: str = "average"
    h_layer: float = 6.0       # m, the layer's eye (spots his own splash from here)
    gun_err: float = 2.0       # arcmin 1 sigma, gun's own scatter [S Sims 1904: +-3 arcmin, read as ~1.5 sigma]
    mv_sig: float = None       # muzzle-velocity scatter, fraction; default by powder: smokeless 0.4 %, brown 0.8 %,
                               # black 1.0 % [S: 15-25 fps spread on 2,400 fps smokeless; INFERRED for the others]

    def __post_init__(self):
        if self.mv_sig is None:
            self.mv_sig = {"smokeless": 0.004, "brown": 0.008, "black": 0.010}[self.powder]


@dataclass
class Era:
    name: str
    range_method: str = "eye"  # eye | stadimeter | FA45 | RF9
    range_period: float = 60.0 # s between fresh ranges passed to the guns
    range_lag: float = 15.0    # s voice pipe / messenger / dial
    rate_aid: str = "none"     # none | dumaresq (rate from estimated courses and speeds) | clock
    speed_err_kn: float = 4.0  # 1-sigma error in the estimate of the enemy's speed across the line of sight
    spotting: str = "own"      # none | own (each layer corrects by his own splash) | control (officer aloft) | salvo
    ladder0: float = 200.0     # m first correction
    h_spot: float = 6.0        # m, eye height of whoever spots


@dataclass
class Sea:
    roll_A: float = 4.0        # deg amplitude
    roll_P: float = 10.0       # s period [INFERRED pre-dreadnought 8-14 s]
    combat: float = 1.0        # stress multiplier on human errors (1.5 battle)
    rate_k: float = 1.0        # drill-to-battle rate (0.5 battle [S 30-60 %])
    wind_smoke: float = 1.0    # 1 calm or smoke blowing down the range; 0.4 brisk favourable wind
    vis: float = 15000.0
    p_obscured: float = 0.0    # chance per minute the target is hidden (funnel smoke, mist, fires)
    night: bool = False        # each shot's flash blinds the layer for a few seconds [INFERRED ~3 s]
    p_layer_blind: float = 0.0 # extra chance per minute that sights at gun level are blinded (spray, gun smoke)
                               # while an observer or director aloft still sees [S: Orion trial 'in smoke']


def roll_rate(sea):
    """Peak roll rate, deg/s."""
    return 2 * math.pi * sea.roll_A / sea.roll_P


def laying_error(g, sea, rng):
    """One shot's elevation and train error (rad) from the human and the sight. Returns (dv, dh, method used)."""
    sk = SKILLS[g.skill]
    c = sea.combat
    w = roll_rate(sea)                                   # deg/s
    sight = SIGHT_SIG[g.sight] * ARCMIN
    method = g.method
    if method == "continuous" and g.elev_rate < 1.1 * w:  # the gear can't follow the roll: back to roll firing
        method = "roll"
    if method == "roll":
        # the roll sweeps the sight across the target at up to w deg/s; firing late or early by dt costs w*dt
        phase = rng.uniform(-0.35, 0.35) * math.pi        # layers fire near mid-roll, not exactly at it
        rate = math.radians(w) * math.cos(phase)
        dt = rng.normal(sk.resid, sk.timing * c) * rng.choice([-1, 1])   # rolling toward / away: sign flips
        dv = rate * dt
    elif method == "roll_end":
        # fire at the end of the roll: rate ~0, but the end point varies roll to roll (+-12 %) [INFERRED]
        dv = math.radians(sea.roll_A) * rng.normal(0, 0.12) * c
    elif method == "continuous":
        dv = rng.normal(0, sk.track * c * w / math.sqrt(2)) * ARCMIN
    else:  # stabilised (director or gyro): only sight and gun remain
        dv = 0.0
    dv += rng.normal(0, sight) + rng.normal(0, g.gun_err * ARCMIN)
    dh = rng.normal(0, sight) + rng.normal(0, g.gun_err * ARCMIN) + rng.normal(0, 1.5 * ARCMIN * c)  # yaw, trainer
    return dv, dh, method


def range_reading(method, R, rng, bias):
    """One fresh range from the control position (m). bias: a per-engagement N(0,1) draw."""
    if method == "eye":
        return R * (1 + 0.15 * rng.normal() + 0.10 * bias)            # [S Sims 1904: 10-15 % broadside]
    if method == "known":                                             # practice: buoyed / measured range
        return R * (1 + 0.005 * rng.normal() + 0.01 * bias)
    if method == "stadimeter":
        if R > 7300:
            return range_reading("eye", R, rng, bias)
        return R * (1 + 0.01 * rng.normal() + 0.05 * bias)            # mast-height guess [S/INFERRED]
    base, mag = {"FA45": (1.37, 24), "RF9": (2.74, 28)}[method]
    unit = 12.0 * math.pi / 648000 * R * R / (base * mag) * 2.5     # x2.5 at sea [S: 2-3x ideal]
    return R + unit * rng.normal() + 1.5 * unit * bias


# =============================================================================
# 3. The engagement
# =============================================================================
@dataclass
class Fight:
    name: str
    groups: list
    era: Era
    tgt: TargetClass
    R0: float
    sea: Sea = field(default_factory=Sea)
    own_kn: float = 10.0
    tgt_kn: float = 10.0
    geometry: str = "parallel"   # parallel | opposite | chase | stationary | flee
    duration: float = 600.0
    salvo: bool = False
    other_ships: int = 0       # other ships firing at the same target (their splashes look like yours) [S: concentration of fire]


def fight(F, seed=0, trace=False):
    rng = np.random.default_rng(seed)
    sea, era = F.sea, F.era
    # geometry: shooter at origin heading +x; target abeam (+y) at R0
    own_v = np.array([F.own_kn * KN, 0.0])
    tgt_p = np.array([0.0, F.R0])
    if F.geometry == "parallel":
        tgt_v = np.array([F.tgt_kn * KN, 0.0])
    elif F.geometry == "opposite":
        tgt_v = np.array([-F.tgt_kn * KN, 0.0])
    elif F.geometry == "flee":          # target running away at an angle (Santiago: Spanish cruisers fleeing west)
        tgt_v = F.tgt_kn * KN * np.array([math.cos(math.radians(60)), math.sin(math.radians(60))])
    elif F.geometry == "chase":
        tgt_v = np.array([F.tgt_kn * KN, 0.0])
        tgt_p = np.array([F.R0 * 0.5, F.R0 * 0.866])
    else:
        tgt_v = np.zeros(2)
    tgt_psi = math.atan2(tgt_v[1], tgt_v[0]) if np.hypot(*tgt_v) > 0.1 else 0.0
    own_p = np.zeros(2)
    bias = rng.normal()
    R = float(np.hypot(*(tgt_p - own_p)))
    Rc, t_range, rc_rate = range_reading(era.range_method, R, rng, bias), 0.0, 0.0
    if era.rate_aid != "none":
        rel = tgt_v - own_v
        u = (tgt_p - own_p) / R
        rc_rate = float(rel @ u) + rng.normal(0, era.speed_err_kn * KN * 0.5)
    # per-gun state
    guns = []
    for g in F.groups:
        for k in range(g.n):
            guns.append(dict(g=g, ready=rng.uniform(0, g.cycle), corr=0.0, corr_d=0.0, step=era.ladder0, last=0,
                             blind=0.0))
    smoke_bank = 0.0
    votes, vote_t = {}, {}
    salvos = []          # in-flight shells: (t_impact, aim world point, gun dict, R_aim)
    landed = []          # recent splashes (t, x) for misidentification
    out = dict(shots=0, hits=0, by={g.name: [0, 0] for g in F.groups}, roll_fired=0, waited=0.0)
    cross_est = None
    last_min, layer_blind_min = -1, False
    t, dt = 0.0, 0.5
    obsc_min = rng.random(int(F.duration / 60) + 2) < sea.p_obscured
    while t <= F.duration:
        own_p = own_p + own_v * dt
        tgt_p = tgt_p + tgt_v * dt
        relp = tgt_p - own_p
        R = float(np.hypot(*relp))
        u = relp / R
        w_ = np.array([-u[1], u[0]])
        smoke_bank *= math.exp(-dt / 30.0)
        hidden = obsc_min[int(t // 60)] or R > sea.vis
        bank_blind_now = rng.random() > 1.0 / (1.0 + smoke_bank / 3.0)
        if int(t // 60) != last_min:
            last_min = int(t // 60)
            layer_blind_min = rng.random() < sea.p_layer_blind
        # control position passes a fresh range
        if t - t_range >= era.range_period and not hidden:
            Rc, t_range = range_reading(era.range_method, R, rng, bias), t
            if era.rate_aid != "none":
                rc_rate = float((tgt_v - own_v) @ u) + rng.normal(0, era.speed_err_kn * KN * 0.5)
        cross_true = float((tgt_v - own_v) @ w_)
        if cross_est is None or int(t) % 120 == 0:
            cross_err = rng.normal(0, era.speed_err_kn * KN) * sea.combat
            cross_est = 0.0
        # salvo firing: the group fires together when every gun of it is ready and can see
        for gd in guns:
            g = gd["g"]
            if t < gd["ready"]:
                continue
            bank_blind = bank_blind_now                                   # the ship's own smoke bank [INFERRED]
            fire_blind = False
            if g.method != "stabilised" and layer_blind_min:
                bank_blind = True
            if hidden or t < gd["blind"] or bank_blind:
                gd["wait"] = gd.get("wait", 0.0) + dt
                out["waited"] += dt
                if gd["wait"] > 20.0 and not hidden:
                    fire_blind = True        # in battle, layers fire into the smoke at where the enemy was
                else:
                    continue
            gd["wait"] = 0.0
            if F.salvo:
                # salvo firing: the group fires when at least half its guns are loaded; the rest join the next one
                mates = [o for o in guns if o["g"] is g]
                if sum(t >= o["ready"] for o in mates) < 0.5 * len(mates):
                    continue
            # ---- what the sight is set to
            age = t - t_range + era.range_lag
            R_set = Rc + (rc_rate * (age + g.gun.tof(min(Rc, g.gun.table["R"][-1] * 0.99))) if era.rate_aid != "none" else 0.0)
            R_set = max(200.0, min(R_set + gd["corr"], g.gun.table["R"][-1] * 0.99))
            T = g.gun.tof(R_set)
            # ---- the layer
            dv, dh, used = laying_error(g, sea, rng)
            if fire_blind:
                dv += rng.normal(0, 15 * ARCMIN)
                dh += rng.normal(0, 15 * ARCMIN)
                out["blind_shots"] = out.get("blind_shots", 0) + 1
            out["roll_fired"] += used == "roll"
            dRde = g.gun.dR_del(R_set)
            # the layer lays his cross-wire on the middle of what he sees: half the target's height above the
            # waterline, i.e. half a danger space beyond the hull's near edge
            fall0 = g.gun.fall(R_set)
            R_imp = R_set + 0.5 * F.tgt.h / math.tan(max(fall0, 0.003)) + dv * dRde + R_set * 1.8 * rng.normal(0, g.mv_sig)
            # aim-off for the enemy's crossing motion: the estimate's error, worse for a poor layer/sight-setter
            lat = dh * R_set + gd["corr_d"] + (cross_true + cross_err * SKILLS[g.skill].lead) * T
            if rng.random() < SKILLS[g.skill].blunder * sea.combat:
                R_imp += rng.choice([-1, 1]) * rng.uniform(400, 1200)
                lat += rng.normal(0, 15 * MIL * R)
            # world impact point: from the gun's position now, along the line of sight, plus own motion
            aim = own_p + own_v * T + u * R_imp + w_ * lat
            salvos.append(dict(t=t + T, p=aim, gd=gd, R=R_set))
            out["shots"] += 1
            out["by"][g.name][0] += 1
            gd["ready"] = t + g.cycle / sea.rate_k * rng.uniform(0.85, 1.25)
            blind = SMOKE_BASE[g.powder] * (g.gun.d / 0.33) * sea.wind_smoke
            if sea.night:
                blind = max(blind, 3.0 * max(g.gun.d / 0.127, 1.0) ** 0.5)
            for o in guns:                      # own smoke blinds this gun's mount-mates and neighbours
                if o["g"] is g:
                    o["blind"] = max(o["blind"], t + blind)
            smoke_bank += {"black": 1.0, "brown": 0.8, "smokeless": 0.05}[g.powder] * (g.gun.d / 0.15)
        # ---- impacts
        for s in [s for s in salvos if s["t"] <= t]:
            salvos.remove(s)
            gd, g = s["gd"], s["gd"]["g"]
            d = s["p"] - tgt_p
            x, y = float(d @ u), float(d @ w_)
            fall = g.gun.fall(min(s["R"], g.gun.table["R"][-1] * 0.99))
            aspect = _wrap(tgt_psi - math.atan2(u[1], u[0]))
            hit = hit_test(x, y, fall, F.tgt, aspect)
            if hit:
                out["hits"] += 1
                out["by"][g.name][1] += 1
            landed.append((t, x, y, g.name))
            landed = [l for l in landed if t - l[0] < 10]
            same = [l for l in landed if l[3] == g.name]      # splashes of the same calibre look alike
            # ---- spotting: can anyone tell which gun this splash belongs to?
            if era.spotting == "none" or hidden:
                continue
            p_see = 1.0 / (1.0 + smoke_bank / 4.0)            # smoke over the target area hides splashes [INFERRED]
            r_vis = 40.0 * g.gun.d * 1000                     # small splashes are lost at range: 12in ~12 km, 6in ~6 km [INFERRED]
            p_see *= 1.0 / (1.0 + (R / r_vis) ** 2)
            if rng.random() > p_see:
                continue
            n_others = max(0, len(same) * (1 + F.other_ships) - 1)
            p_id = 1.0 if (F.salvo or era.spotting == "salvo") else 1.0 / (1.0 + 0.6 * n_others)
            if rng.random() > p_id and n_others:
                if F.other_ships and rng.random() < F.other_ships / (1 + F.other_ships):
                    x, y = rng.normal(0, 0.03 * R), rng.normal(0, 8 * MIL * R)   # another ship's splash: unrelated error
                else:
                    _, x, y, _ = same[rng.integers(0, len(same))]    # another of our own guns
            h = gd["g"].h_layer if era.spotting == "own" else era.h_spot
            so = 4.0e-5 * R * R / h * sea.combat
            targets = [gd] if era.spotting == "own" else [o for o in guns if o["g"] is g]
            if hit:                                   # a hit is seen as a hit: hold the range, tighten the ladder
                for o in targets:
                    o["step"] = max(50.0, o["step"] / 2)
                continue
            seen = x - 0.5 * F.tgt.h / math.tan(max(fall, 0.003)) + rng.normal(0, so)   # over/short of the hull
            dd = -1 if seen > 0 else 1
            if era.spotting == "control" and not F.salvo:
                # the control officer watches a group of splashes and orders one correction per ~15 s
                buf = votes.setdefault(g.name, [])
                buf.append((dd, y))
                if t - vote_t.get(g.name, -1e9) < 15.0:
                    continue
                vote_t[g.name] = t
                sgn = sum(v[0] for v in buf)
                ym = float(np.mean([v[1] for v in buf]))
                votes[g.name] = []
                if abs(sgn) < 0.34 * len(buf):         # mixed overs and shorts: about right, tighten
                    for o in targets:
                        o["step"] = max(50.0, o["step"] / 2)
                        o["corr_d"] -= 0.5 * ym
                    continue
                dd = 1 if sgn > 0 else -1
                y = ym
            for o in targets:
                if o["last"] and dd != o["last"]:
                    o["step"] = max(50.0, o["step"] / 2)
                o["corr"] += dd * o["step"]
                o["last"] = dd
                o["corr_d"] -= 0.5 * (y + rng.normal(0, 2 * MIL * R))
        t += dt
    return out


def run(F, n=40, seed0=1):
    shots = hits = 0
    by = {}
    for k in range(n):
        o = fight(F, seed=seed0 + k)
        shots += o["shots"]; hits += o["hits"]
        for nm, (s, h) in o["by"].items():
            by.setdefault(nm, [0, 0]); by[nm][0] += s; by[nm][1] += h
    return dict(pct=100 * hits / max(shots, 1), shots=shots / n, hits=hits / n,
                by={k: (100 * v[1] / max(v[0], 1), v[0] / n) for k, v in by.items()})


# =============================================================================
# 4. Calibration cases
# =============================================================================
QF6 = lambda **kw: Group("6in QF", O["6in/40 QF (1890s)"], kw.pop("n", 1), kw.pop("cycle", 10.0), **kw)
QF47 = lambda **kw: Group("4.7in QF", O["4.7in/40 QF (1890s)"], kw.pop("n", 1), kw.pop("cycle", 6.0), **kw)
PRACTICE_ERA = Era("practice, range known", "known", 30, 5, "none", 2.0, "own", 100, 6)


def prize(sight, method, skill, gun="6in", sea=None, name=""):
    g = (QF6 if gun == "6in" else QF47)(sight=sight, method=method, skill=skill, elev_rate=4.0)
    return Fight(name, [g], PRACTICE_ERA, SCREEN, 1500 * YD, sea or Sea(roll_A=3.0, roll_P=9.0), own_kn=8, tgt_kn=0,
                 geometry="stationary", duration=180)

OLD.update({g.name: g.fit() for g in [
    Gun("9in RML 12-ton (1870s)", 0.229, 113, 430, 10, 5000, 2.0, train_rate=0.5),           # [INFERRED]
    Gun("5in/40 USN (1898)", 0.127, 22.7, 700, 15, 9000, 2.0, train_rate=6.0),               # [INFERRED]
    Gun("6-pdr 57 mm", 0.057, 2.7, 570, 20, 6500, 2.0, train_rate=15.0),                     # [INFERRED]
]})
HUASCAR = TargetClass("Huascar (low-freeboard turret ship)", 60, 11, 2.5, 20, 0.3, 11, 2.0)
CHINESE_IC = TargetClass("Dingyuan-class ironclad", 91, 18, 6.0, 25, 0.5, 8, 1.0)
JAP_CRUISER = TargetClass("Japanese protected cruiser", 95, 14, 5.0, 25, 0.4, 16, 1.5)
BATTLE = dict(combat=1.5, rate_k=0.5)
BIG_TARGET = TargetClass("1912 battle-practice target (assumed 120 x 9 m) [UNCERTAIN]", 120, 23, 9.0, 30, 0.6, 0, 0)

ERA_1866 = Era("1866: eye, no spotting", "eye", 120, 20, "none", 5.0, "none", 300, 6)
ERA_1894 = Era("1894: eye, layers spot own splash", "eye", 60, 15, "none", 4.0, "own", 200, 6)
ERA_1898 = Era("1898: stadimeter aloft, layers spot own", "stadimeter", 60, 20, "none", 4.0, "own", 200, 6)
ERA_1898B = Era("1898 in battle: eye, no spotting (pointers blinded by smoke)", "eye", 90, 20, "none", 5.0, "none", 200, 6)
FIRST_BATTLE = dict(combat=2.0, rate_k=0.5)   # crews that had barely practised, first time under fire [INFERRED]
ERA_1904 = Era("1904: FA rangefinder, officer spots from the top", "FA45", 30, 10, "none", 3.0, "control", 200, 25)
ERA_1908 = Era("1908: 9 ft RF, Dumaresq + clock, salvo spotting", "RF9", 20, 8, "clock", 2.0, "salvo", 400, 30)

HISTORY = [
    ("Angamos 1879: Chilean 9in RML on Huascar, closing to ~600 m", "~1/3 (27 heavy hits) [UNCERTAIN]",
     Fight("angamos", [Group("9in RML", O["9in RML 12-ton (1870s)"], 4, 180, "open", "roll", 0.5, "black", "trained")],
           ERA_1866, HUASCAR, 600, Sea(roll_A=3, roll_P=8, **BATTLE), own_kn=9, tgt_kn=9, duration=1200)),
    ("Yalu 1894: Japanese QF 4.7/6in on Chinese ironclads, 2.5 km", "~10-15 % [UNCERTAIN]",
     Fight("yalu_j", [Group("6in QF", O["6in/40 QF (1890s)"], 4, 10, "open", "roll", 4.0, "smokeless", "trained"),
                      Group("4.7in QF", O["4.7in/40 QF (1890s)"], 6, 6, "open", "roll", 4.0, "smokeless", "trained")],
           ERA_1894, CHINESE_IC, 2500, Sea(roll_A=2, roll_P=9, **BATTLE), own_kn=10, tgt_kn=7, geometry="opposite", duration=900)),
    ("Yalu 1894: Chinese 12in Krupp on Japanese cruisers, 2.5 km", "~4-6 % [UNCERTAIN]",
     Fight("yalu_c", [Group("12in Krupp", O["12in/25 Krupp (Dingyuan 1894)"], 4, 240, "open", "roll", 0.5, "black", "average")],
           ERA_1894, JAP_CRUISER, 2500, Sea(roll_A=2, roll_P=9, **BATTLE), own_kn=7, tgt_kn=10, geometry="opposite", duration=1800)),
    ("Manila Bay 1898: US cruisers on anchored ships, 2-5 kyd", "2.4 % (perhaps ~4 %)",
     Fight("manila", [Group("8in/35", O["8in/35 USN (1898)"], 4, 60, "tele_early", "roll", 1.5, "brown", "green"),
                      Group("5in/40", O["5in/40 USN (1898)"], 10, 12, "tele_early", "roll", 3.0, "brown", "green"),
                      Group("6-pdr", O["6-pdr 57 mm"], 6, 5, "open", "roll", 6.0, "brown", "green")],
           ERA_1898B, HULK, 3500 * YD, Sea(roll_A=0.7, roll_P=8, **FIRST_BATTLE), own_kn=8, tgt_kn=0, geometry="stationary", duration=1200)),
    ("Santiago 1898: US battleship on fleeing cruiser, ~3 kyd", "1.3 % all; 3.5 % major calibre",
     Fight("santiago", [Group("13in/35", O["13in/35 USN (1898, brown)"], 4, 300, "tele_early", "roll", 0.8, "brown", "green"),
                        Group("8in/35", O["8in/35 USN (1898)"], 4, 60, "tele_early", "roll", 1.5, "brown", "green"),
                        Group("6-pdr", O["6-pdr 57 mm"], 10, 5, "open", "roll", 6.0, "brown", "green")],
           ERA_1898B, CRU, 3000 * YD, Sea(roll_A=2, roll_P=9, **FIRST_BATTLE), own_kn=13, tgt_kn=15, geometry="parallel", duration=1800)),
    ("Yellow Sea 1904: Japanese 12in on Russian battleship, ~8 km (long phase)", "12in 4.7 %; all ~1.7 %",
     Fight("yellow", [Group("12in/40", O["12in/40 (1901-05)"], 4, 60, "tele", "roll", 1.5, "smokeless", "trained"),
                      Group("6in QF", O["6in/40 QF (1890s)"], 14, 10, "tele", "continuous", 4.0, "smokeless", "trained")],
           ERA_1904, PRE, 8000, Sea(roll_A=3, roll_P=10, **BATTLE), own_kn=14, tgt_kn=12, duration=1800, other_ships=3)),
    ("Tsushima 1905: Japanese 12in on Russian battleship, ~5.5 km", "12in ~9 %; 6/8in ~2 %",
     Fight("tsushima", [Group("12in/40", O["12in/40 (1901-05)"], 4, 60, "tele", "roll", 1.5, "smokeless", "trained"),
                        Group("6in QF", O["6in/40 QF (1890s)"], 14, 10, "tele", "continuous", 4.0, "smokeless", "trained")],
           ERA_1904, PRE, 5500, Sea(roll_A=5.5, roll_P=10, p_obscured=0.15, **BATTLE), own_kn=15, tgt_kn=9, duration=1800,
           other_ships=4)),
    ("Orion trial 1912: gunlayers' firing, 13.5in, 9 kyd, heavy sea", "4/27 = 15 %",
     Fight("orion", [Group("13.5in", GUNS["13.5in/45 Mk V(H)"], 10, 60, "tele", "roll", 2.0, "smokeless", "trained")],
           replace(ERA_1908, range_method="known"), BIG_TARGET, 9000 * YD,
           Sea(roll_A=6, roll_P=12, combat=1.0, rate_k=1.0, p_layer_blind=0.5), own_kn=12, tgt_kn=8,
           duration=210, salvo=True)),
    ("Thunderer, same trial, director firing", "26/39 = 67 %",
     Fight("thunderer", [Group("13.5in", GUNS["13.5in/45 Mk V(H)"], 10, 60, "tele", "stabilised", 2.0, "smokeless", "trained")],
           replace(ERA_1908, range_method="known"), BIG_TARGET, 9000 * YD,
           Sea(roll_A=6, roll_P=12, combat=1.0, rate_k=1.0, p_layer_blind=0.5), own_kn=12, tgt_kn=8,
           duration=210, salvo=True)),
]


def history_table(n=30):
    print("\n=== 6. Calibration against the record, 1879-1912. Hit % per round ===")
    print(f"{'case':66s}{'model':>7}  {'record':s}")
    for name, rec, F in HISTORY:
        r = run(F, n=n)
        extra = "  (" + ", ".join(f"{k} {v[0]:.1f}%" for k, v in r["by"].items()) + ")" if len(r["by"]) > 1 else ""
        print(f"{name:66s}{r['pct']:7.1f}  {rec}{extra}")


# =============================================================================
# 5. Tables
# =============================================================================
def laying_table(n=4000):
    print("\n=== 1. The gunlayer's elevation error, 1 sigma, arcmin (sight + method + gun), practice conditions ===")
    print("Roll firing: timing error x roll rate. End of roll: roll-to-roll scatter. Continuous aim: tracking error.")
    seas = [("calm 1 deg/8 s", Sea(roll_A=1, roll_P=8)), ("moderate 3/10", Sea(roll_A=3, roll_P=10)),
            ("rough 6/12", Sea(roll_A=6, roll_P=12))]
    print(f"{'method / sight / skill':40s}" + "".join(f"{nm:>16s}" for nm, _ in seas))
    rng = np.random.default_rng(5)
    for meth, sight, sk in (("roll", "open", "green"), ("roll", "open", "average"), ("roll", "open", "crack"),
                            ("roll_end", "open", "average"), ("roll", "tele", "trained"),
                            ("continuous", "tele_early", "average"), ("continuous", "tele", "trained"),
                            ("continuous", "tele", "crack"), ("stabilised", "tele", "trained")):
        g = Group("x", O["6in/40 QF (1890s)"], 1, 10, sight, meth, 6.0, "smokeless", sk)
        cells = ""
        for _, sea in seas:
            v = [laying_error(g, sea, rng)[0] for _ in range(n)]
            cells += f"{math.degrees(np.std(v)) * 60:16.1f}"
        print(f"{meth + ' / ' + sight + ' / ' + sk:40s}{cells}")
    print("Peak roll rate: calm 0.8, moderate 1.9, rough 3.1 deg/s. Continuous aim needs gearing faster than that;")
    print("1890s heavy mounts (~1-2 deg/s) could not, so heavy guns fired on the roll until directors (1912+).")
    print("\nWhat 10 arcmin of elevation error means (range error at the target, m):")
    for gn in ("6in/40 QF (1890s)", "12in/40 (1901-05)"):
        g = O[gn]
        print(f"  {gn:22s}" + "".join(f"{k:>5} m:{10 * ARCMIN * g.dR_del(k):5.0f}" for k in (1000, 2000, 4000, 6000, 8000)))


def range_table():
    print("\n=== 2. Knowing the range: 1-sigma error of one reading incl. typical bias, m ===")
    rng = np.random.default_rng(2)
    print(f"{'method':22s}" + "".join(f"{k:>8} m" for k in (1000, 2000, 4000, 6000, 8000)))
    for meth, lab in (("eye", "eye estimate"), ("stadimeter", "stadimeter (1895)"), ("FA45", "B&S 4.5 ft FA (1893)"),
                      ("RF9", "B&S 9 ft (1906)")):
        cells = ""
        for R in (1000, 2000, 4000, 6000, 8000):
            v = [range_reading(meth, R, rng, rng.normal()) - R for _ in range(4000)]
            cells += f"{np.std(v):10.0f}"
        print(f"{lab:22s}{cells}")


def eyeball_range_table():
    print("\n=== 3. 'Point blank' - how far can you fight on an eye-estimated range? ===")
    print("Range window = danger space of an 8 m high hull + its 20 m beam (broadside). An eye estimate is ~15 %")
    print("of range (plus personal bias). Listed: window (m) and the chance the eye range alone lands in it.")
    guns = [("12-pdr", O["12-pdr 12 cwt QF"]), ("6in/40 QF", O["6in/40 QF (1890s)"]), ("12in/40", O["12in/40 (1901-05)"]),
            ("5in/38 (WWII)", GUNS["5in/38 Mk 12"]), ("16in/50 (WWII)", GUNS["16in/50 Mk 7"])]
    ks = (500, 1000, 2000, 3000, 4000, 6000)
    print(f"{'gun':16s}" + "".join(f"{k:>14} m" for k in ks))
    for nm, g in guns:
        cells = ""
        for R in ks:
            if R >= g.table["R"][-1]:
                cells += f"{'-':>16}"
                continue
            win = 8.0 / math.tan(g.fall(R)) + 20.0
            sig = 0.18 * R
            p = math.erf(win / 2 / (sig * math.sqrt(2)))
            cells += f"{win:9.0f} {100 * p:4.0f}%"
        print(f"{nm:16s}{cells}")


def prize_table(n=60):
    print("\n=== 4. Calibration: Royal Navy prize firing (screen 20 x 17 ft at 1,500 yd, firing ship under way) ===")
    for lab, args, rec in (("1897 fleet, 6in: open sights, on the roll, average", ("open", "roll", "average", "6in"), "24 %"),
                           ("1897 fleet, 4.7in: same", ("open", "roll", "average", "4.7in"), "35 %"),
                           ("Scylla 1897, 6in: green", ("open", "roll", "green", "6in"), "8 %"),
                           ("Scylla 1899, 4.7in: telescope + continuous aim, crack", ("tele", "continuous", "crack", "4.7in"), "80 %"),
                           ("Terrible 1901, 6in: same", ("tele", "continuous", "crack", "6in"), "80 % (88 % 1902)"),
                           ("Fleet 1905-07, 6in: telescope + continuous aim, trained", ("tele", "continuous", "trained", "6in"), "79 %")):
        r = run(prize(*args), n=n)
        print(f"  {lab:58s}{r['pct']:6.1f} %   record {rec}")


GAMUT = [  # one 6in-class gun battery with each era's human and technical means [INFERRED era bundles]
    ("1866  muzzle-loader, open sights, black powder, eye",
     dict(cycle=120, sight="open", method="roll", powder="black", skill="average"), ERA_1866, False),
    ("1885  breech-loader, open sights, brown powder, eye",
     dict(cycle=60, sight="open", method="roll", powder="brown", skill="average"), ERA_1894, False),
    ("1894  QF, smokeless, open sights, on the roll",
     dict(cycle=10, sight="open", method="roll", powder="smokeless", skill="average"), ERA_1894, False),
    ("1898  + early telescope, stadimeter aloft",
     dict(cycle=10, sight="tele_early", method="roll", powder="smokeless", skill="average"), ERA_1898, False),
    ("1901  + good telescope, continuous aim (Scott)",
     dict(cycle=10, sight="tele", method="continuous", powder="smokeless", skill="trained"), ERA_1898, False),
    ("1905  + FA rangefinder, officer spots from the top",
     dict(cycle=10, sight="tele", method="continuous", powder="smokeless", skill="trained"), ERA_1904, False),
    ("1908  + 9 ft RF, Dumaresq/clock, salvo spotting",
     dict(cycle=10, sight="tele", method="continuous", powder="smokeless", skill="trained"), ERA_1908, True),
    ("1912  + director firing (gyro-free, but one layer)",
     dict(cycle=10, sight="tele", method="stabilised", powder="smokeless", skill="trained"), ERA_1908, True),
]


def gamut_table(n=30):
    print("\n=== 5. The full gamut before computers: one 6in battery (6 guns) through the eras, in battle")
    print("     (combat 1.5, rates halved), moderate sea, enemy pre-dreadnought on a parallel course at 10 kn.")
    print("     cell = hit % / hits per gun per minute ===")
    ks = (1000, 2000, 3000, 5000, 7000)
    print(f"{'era':52s}" + "".join(f"{k:>13} m" for k in ks))
    for lab, kw, era, salvo in GAMUT:
        cells = ""
        for R in ks:
            g = Group("6in", O["6in/40 QF (1890s)"], 6, kw["cycle"], kw["sight"], kw["method"], 4.0, kw["powder"], kw["skill"])
            F = Fight(lab, [g], era, PRE, R, Sea(roll_A=3, roll_P=10, **BATTLE), own_kn=10, tgt_kn=10, duration=600,
                      salvo=salvo)
            r = run(F, n=n)
            cells += f"{r['pct']:8.1f} /{r['hits'] / 6 / 10:5.2f}"
        print(f"{lab:52s}{cells}")


def confusion_table(n=30):
    print("\n=== 7. Whose splash is it? Independent fire vs salvos, 6in battery, trained, 5 km, battle ===")
    print(f"{'guns / ships on the target':34s}{'independent':>14}{'salvos':>10}")
    for ng, others in ((2, 0), (6, 0), (12, 0), (6, 2), (12, 4)):
        cells = ""
        for salvo in (False, True):
            g = Group("6in", O["6in/40 QF (1890s)"], ng, 10, "tele", "continuous", 4.0, "smokeless", "trained")
            F = Fight("c", [g], ERA_1904, PRE, 5000, Sea(roll_A=3, roll_P=10, **BATTLE), own_kn=10, tgt_kn=10,
                      duration=900, salvo=salvo, other_ships=others)
            cells += f"{run(F, n=n)['pct']:12.1f}"
        print(f"{f'{ng} guns, {others} other ships':34s}{cells}")


def smoke_table(n=30):
    print("\n=== 8. Smoke: aimed rounds per gun per minute and hit %, 6in-class battery of 6, 3 km, battle ===")
    print(f"{'powder / wind':34s}{'rounds/gun/min':>16}{'hit %':>8}{'fired blind %':>15}")
    for powder in ("black", "brown", "smokeless"):
        for wl, wk in (("calm or wind down the range", 1.0), ("brisk favourable wind", 0.4)):
            g = Group("6in", O["6in/40 QF (1890s)"], 6, 10, "open", "roll", 4.0, powder, "average")
            F = Fight("s", [g], ERA_1894, PRE, 3000, Sea(roll_A=3, roll_P=10, wind_smoke=wk, **BATTLE), own_kn=10,
                      tgt_kn=10, duration=600)
            shots = hits = blind = 0
            for k in range(n):
                o = fight(F, seed=k)
                shots += o["shots"]; hits += o["hits"]; blind += o.get("blind_shots", 0)
            print(f"{powder + ' / ' + wl:34s}{shots / n / 6 / 10:16.2f}{100 * hits / max(shots, 1):8.1f}"
                  f"{100 * blind / max(shots, 1):15.0f}")


def emergency_table(n=30):
    print("\n=== 9. MK1 eyeball in an emergency: WWII guns when the director is gone, combat ===")
    print("Reference rows use fire_control_ref (radar director, Level 6). cell = hit % per round")
    G538 = GUNS["5in/38 Mk 12"]
    G16 = GUNS["16in/50 Mk 7"]
    LOCAL_EYE = Era("local: eye range, mount spots own fall", "eye", 30, 3, "none", 4.0, "own", 200, 8)
    LOCAL_RF = Era("local: turret rangefinder, turret officer spots", "RF9", 20, 5, "dumaresq", 3.0, "control", 200, 10)
    print("  Destroyer battery, 5 x 5in/38, at a destroyer on a parallel course (both 30 kn), moderate sea")
    ks = (2000 * YD, 3000 * YD, 5000 * YD, 8000 * YD)
    print(f"  {'case':52s}" + "".join(f"{k / YD:>8.0f} yd" for k in ks))
    sh = fc.Shooter(G538, ((5, 0, 150),), interval=4, speed=30)
    cells = ""
    for R in ks:
        scn = fc.Scenario("ref", sh, TARGETS["DD"], R, tgt_brg=90, tgt_course=0, tgt_speed=30, duration=300,
                          t_open=30, cond=fc.Conditions(combat=1.5))
        cells += f"{fc.run(scn, LV6, n)['pct']:11.1f}"
    print(f"  {'director + radar (Mk 37 + Mk 12), reference':52s}{cells}")
    cases = [
        ("local control, power on, telescopes, eye range", dict(sight="tele", method="continuous", elev=15.0, cycle=4), Sea(roll_A=3, roll_P=8, **BATTLE)),
        ("  same, open sights only", dict(sight="open", method="continuous", elev=15.0, cycle=4), Sea(roll_A=3, roll_P=8, **BATTLE)),
        ("  same, power lost: hand drive, firing on the roll", dict(sight="tele", method="roll", elev=1.0, cycle=8), Sea(roll_A=3, roll_P=8, **BATTLE)),
        ("  local control at night, starshell, flash-blinded", dict(sight="tele", method="continuous", elev=15.0, cycle=4),
         Sea(roll_A=3, roll_P=8, night=True, vis=3000, **BATTLE)),
    ]
    for lab, kw, sea in cases:
        cells = ""
        for R in ks:
            g = Group("5in38", G538, 5, kw["cycle"], kw["sight"], kw["method"], kw["elev"], "smokeless", "trained")
            F = Fight(lab, [g], LOCAL_EYE, TARGETS["DD"], R, sea, own_kn=30, tgt_kn=30, geometry="parallel", duration=300)
            cells += f"{run(F, n=n)['pct']:11.1f}"
        print(f"  {lab:52s}{cells}")
    print("  Battleship, 9 x 16in/50, at a battleship (parallel, 20 kn), moderate sea")
    ks = (8000 * YD, 12000 * YD, 16000 * YD)
    print(f"  {'case':52s}" + "".join(f"{k / YD:>8.0f} yd" for k in ks))
    sh = fc.Shooter(G16, interval=30)
    for lab, lv in (("director + radar (Level 6), reference", "6"), ("turret local control (Level L), reference", "L")):
        cells = ""
        for R in ks:
            scn = fc.Scenario("ref", sh, TARGETS["BB"], R, tgt_brg=90, tgt_course=0, tgt_speed=20, duration=600,
                              cond=fc.Conditions(combat=1.5))
            cells += f"{fc.run(scn, LV[lv], n)['pct']:11.1f}"
        print(f"  {lab:52s}{cells}")
    for lab, kw, era in (
            ("turret RF, power on, continuous aim", dict(method="continuous", elev=12.0, sight="tele"), LOCAL_RF),
            ("turret RF, power lost: hand elevation, on the roll", dict(method="roll", elev=0.2, sight="tele"), LOCAL_RF),
            ("no rangefinder: eye range, power on", dict(method="continuous", elev=12.0, sight="tele"), LOCAL_EYE)):
        cells = ""
        for R in ks:
            g = Group("16in", G16, 9, 40 if kw["elev"] > 1 else 120, kw["sight"], kw["method"], kw["elev"], "smokeless", "trained", h_layer=10)
            F = Fight(lab, [g], era, TARGETS["BB"], R, Sea(roll_A=3, roll_P=12, **BATTLE), own_kn=20, tgt_kn=20, duration=600)
            cells += f"{run(F, n=n)['pct']:11.1f}"
        print(f"  {lab:52s}{cells}")


LV = fc.LV
LV6 = fc.LV["6"]


if __name__ == "__main__":
    only = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] != "quick" else None
    if only:
        globals()[only]()
        sys.exit()
    laying_table()
    range_table()
    eyeball_range_table()
    prize_table()
    if len(sys.argv) > 1 and sys.argv[1] == "quick":
        sys.exit()
    gamut_table()
    history_table()
    confusion_table()
    smoke_table()
    emergency_table()
