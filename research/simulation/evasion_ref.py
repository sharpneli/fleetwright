"""
evasion_ref.py — manoeuvre, evasion and counter-prediction model
(Naval project, evasion-research.md). Builds on fire_control_ref.py.

The problem in one line: the gun aims at where the target WILL be; the target decides where it will be
AFTER the gun has fired. Everything here quantifies that gap and what each side can do with it.

Run:  python3 evasion_ref.py            (all tables, ~10-15 min)
      python3 evasion_ref.py quick      (dynamics + envelopes only, seconds)

Two mechanisms are kept separate throughout:
  (a) DODGE: the target moves off the predicted point during latency + time of flight. Hard kinematic
      limit: no computer predicts a decision not yet made.
  (b) CORRUPTION: every course change leaves the computer's estimate of course and speed wrong until it
      re-converges; meanwhile every salvo is aimed with a wrong rate. WWII computers suffer mostly from
      (b); turn-following digital computers suffer only from (a).

Tags: [S] sourced (see the note), [INFERRED] fit or tuning value, [UNCERTAIN] thin sources.
"""

import math
import sys
from dataclasses import dataclass, field, replace

import numpy as np

import fire_control_ref as fc
from fire_control_ref import (GUNS, Gun, TARGETS, TargetClass, LV, FCLevel, Conditions, hit_test,
                              make_tracker, CartTracker, ManoeuvreTracker, PolarTracker, KN, YD, MIL, KYD, _wrap)


# =============================================================================
# 1. Hull dynamics: first-order (Nomoto) yaw, rudder-rate limit, speed loss in turns, speed lags
# =============================================================================
@dataclass
class Hull:
    name: str
    tc: TargetClass          # hitting box + visibility
    L: float                 # m
    vmax: float              # kn
    TD: float                # m, tactical diameter at vmax, hard rudder [S where a ship is named]
    Tp: float                # Nomoto T' = T U / L  [INFERRED 1.0-1.5, note §1]
    rud_rate: float          # deg/s  [S: ~2.3-2.5 warships; INFERRED small craft]
    rud_max: float = 35.0    # deg
    f_loss: float = 0.35     # speed loss fraction in a steady hard turn [S/INFERRED]
    tau_acc: float = 150.0   # s, first-order speed lag accelerating
    tau_coast: float = 300.0 # s, engines stopped
    tau_back: float = 90.0   # s, backing
    Rss: float = 0.0         # m, steady radius at full rudder (fitted to TD)

    def fit(self):
        lo, hi = 0.2 * self.TD, 0.7 * self.TD
        for _ in range(30):
            self.Rss = 0.5 * (lo + hi)
            td = tactical_diameter(self)
            if td > self.TD:
                hi = self.Rss
            else:
                lo = self.Rss
        return self


class Ship:
    def __init__(self, hull, pos, psi, speed_kn):
        self.h = hull
        self.p = np.array(pos, float)
        self.psi = psi
        self.U = speed_kn * KN
        self.r = 0.0
        self.d = 0.0                 # rudder angle, deg (+ = turn to port / CCW)
        self.cmd_rud = 0.0           # -1..1 of rud_max
        self.cmd_spd = speed_kn      # kn ordered
        self.des_psi = None          # if set, a heading controller drives the rudder

    def copy(self):
        s = Ship.__new__(Ship)
        s.__dict__.update(self.__dict__)
        s.p = self.p.copy()
        return s

    def vel(self):
        return self.U * np.array([math.cos(self.psi), math.sin(self.psi)])

    def step(self, dt):
        h = self.h
        if self.des_psi is not None:   # helmsman: anticipates the swing by one Nomoto time constant
            T = h.Tp * h.L / max(self.U, 1.0)
            err = _wrap(self.des_psi - self.psi) - 0.8 * T * self.r
            self.cmd_rud = max(-1.0, min(1.0, err / math.radians(12)))
        tgt = self.cmd_rud * h.rud_max
        self.d += max(-h.rud_rate * dt, min(h.rud_rate * dt, tgt - self.d))
        U = max(self.U, 0.3)
        x = self.d / h.rud_max
        r_cmd = math.copysign(abs(x) ** 0.55, x) * U / h.Rss       # partial rudder: TD ~ (35/d)^0.55 [S fit]
        T = h.Tp * h.L / U
        self.r += (r_cmd - self.r) * min(1.0, dt / T)
        frac = min(1.0, (self.r * h.Rss / U) ** 2)
        U_tgt = max(self.cmd_spd, 0.0) * KN * (1 - h.f_loss * frac)
        if U_tgt > self.U:
            tau = h.tau_acc
        else:
            tau = h.tau_back if self.cmd_spd < 0 else h.tau_coast
        tau = max(tau, dt)
        if U_tgt < self.U and frac > 0.05:      # turn-induced loss acts on the hull-drag time scale [INFERRED]
            tau = min(tau, 1.5 * T)
        self.U += (U_tgt - self.U) * min(1.0, dt / tau)
        self.U = max(self.U, 0.0)
        self.psi += self.r * dt
        self.p = self.p + self.vel() * dt


def tactical_diameter(hull, v=None, rud=1.0):
    s = Ship(hull, (0, 0), 0.0, v or hull.vmax)
    s.cmd_rud = rud
    t = 0.0
    while s.psi < math.pi and t < 2000:
        s.step(0.25)
        t += 0.25
    return abs(s.p[1])


def time_to_heading(hull, deg, v=None):
    s = Ship(hull, (0, 0), 0.0, v or hull.vmax)
    s.cmd_rud = 1.0
    t = 0.0
    while s.psi < math.radians(deg) and t < 3000:
        s.step(0.25)
        t += 0.25
    return t, s.U / KN


T_ = TARGETS
HULLS = {h.name: h.fit() for h in [
    # name, hitting box, L, vmax, TD, T', rudder deg/s, max, speed-loss, tau_acc, tau_coast, tau_back
    Hull("BB fast (Iowa)", T_["BB"], 262, 31, 744, 1.5, 2.4, 35, 0.35, 200, 350, 100),          # TD [S]
    Hull("BB WWI (QE)", replace(T_["BB"], L=195, B=27), 195, 24, 640, 1.4, 2.0, 35, 0.30, 200, 300, 110),  # TD [UNCERTAIN]
    Hull("CA (Baltimore)", T_["CA"], 205, 33, 800, 1.2, 2.4, 35, 0.38, 120, 250, 70),           # TD [INFERRED]
    Hull("DD (Fletcher)", T_["DD"], 115, 36, 869, 1.2, 2.4, 35, 0.40, 60, 170, 40),             # TD [S] single rudder
    Hull("DD twin rudder (Sumner)", T_["DD"], 115, 34, 650, 1.15, 2.4, 35, 0.40, 60, 170, 40), # TD [S]
    Hull("FF gas turbine (Perry)", replace(T_["DE/FF"], L=135, B=14, h=5.0), 135, 29, 650, 1.1, 2.5, 35, 0.40, 30, 150, 22),
    Hull("FAC (Osa)", replace(T_["PT/FAC"], L=39, B=7.6, h=2.5, top=10), 39, 40, 230, 1.0, 6.0, 35, 0.45, 10, 15, 5),
    Hull("MTB / PT (Elco 80)", T_["PT/FAC"], 24, 40, 150, 1.0, 10.0, 35, 0.50, 10, 12, 5),
]}
H = HULLS


def dynamics_table():
    print("\n=== 1. Hull dynamics, calibrated to tactical diameter [S anchors: Iowa 744 m @30 kn, Fletcher 869 m,")
    print("     Sumner 650 m; Iowa/Ticonderoga 180 deg in ~2 min; PT 180 deg in 9-22 s] ===")
    print(f"{'hull':26s}{'TD m':>7}{'R_ss':>6}{'t90':>6}{'t180':>6}{'t360':>6}{'kn@180':>7}{'max r deg/s':>12}")
    for h in HULLS.values():
        t90, _ = time_to_heading(h, 90)
        t180, v180 = time_to_heading(h, 180)
        t360, _ = time_to_heading(h, 360)
        rmax = math.degrees(h.vmax * KN / h.Rss)
        print(f"{h.name:26s}{tactical_diameter(h):7.0f}{h.Rss:6.0f}{t90:6.0f}{t180:6.0f}{t360:6.0f}{v180:7.1f}{rmax:12.1f}")


# =============================================================================
# 2. The dodge envelope: where can the ship be after T seconds, relative to the straight-line prediction?
# =============================================================================
def run_cmds(hull, T, plan, v=None, dt=0.5):
    """plan: list of (t_start, rudder, speed_kn or None). Returns final position, heading."""
    s = Ship(hull, (0, 0), 0.0, v or hull.vmax)
    t, i = 0.0, 0
    plan = sorted(plan)
    while t < T - 1e-9:
        while i < len(plan) and plan[i][0] <= t:
            s.cmd_rud = plan[i][1]
            if plan[i][2] is not None:
                s.cmd_spd = plan[i][2]
            i += 1
        s.step(min(dt, T - t))
        t += dt
    return s.p, s.psi


def envelope(hull, T, react=0.0, v=None):
    """Displacement from the dead-reckoned point after T s (straight, constant speed predicted).
    Options evaluated: hard over, standard (15 deg) rudder, reversal at T/2, stop engines / back.
    react = delay before the manoeuvre starts (seeing the flash, ordering the helm)."""
    v = v or hull.vmax
    dr = np.array([v * KN * T, 0.0])
    Te = max(T - react, 0.0)
    out = {}
    for name, plan in [("hard", [(0, 1.0, None)]), ("std15", [(0, 15 / 35, None)]),
                       ("reverse", [(0, 1.0, None), (Te / 2, -1.0, None)]),
                       ("stop", [(0, 0.0, 0.0)]), ("back", [(0, 0.0, -0.1)])]:
        if Te <= 0:
            out[name] = (0.0, 0.0)
            continue
        p, _ = run_cmds(hull, Te, plan, v)
        p = p + np.array([v * KN * react, 0.0])      # straight during the reaction time
        d = p - dr
        out[name] = (abs(float(d[1])), float(d[0]))  # (lateral, along-track: negative = short of DR point)
    # reach: the farthest lateral point achievable at T (hard over, then rudder amidships at the best moment)
    best = 0.0
    for k in range(1, 21):
        th = Te * k / 20
        p, _ = run_cmds(hull, Te, [(0, 1.0, None), (th, 0.0, None)], v) if Te > 0 else (np.zeros(2), 0)
        best = max(best, abs(float(p[1])))
    out["reach"] = best
    # sigma of a fair three-way gamble (hard left / hold / hard right) using the reach
    out["sigma3"] = best * math.sqrt(2 / 3)
    return out


def envelope_table():
    print("\n=== 2. Dodge envelope: metres off the straight-line prediction after T s (hard over at t = 0) ===")
    print("cell = lateral / along-track (negative = short). 'gamble' = 1-sigma lateral spread if the target")
    print("picks hard-left / hold / hard-right with equal odds (the best it can do against a mean-aiming gun).")
    Ts = (10, 20, 30, 45, 60, 90)
    print(f"{'hull':26s}" + "".join(f"{t:>12d}s" for t in Ts))
    for h in HULLS.values():
        cells = ""
        for T in Ts:
            e = envelope(h, T)
            cells += f"{e['hard'][0]:7.0f}/{e['hard'][1]:5.0f}"
        print(f"{h.name:26s}{cells}")
    print("\nReach: farthest lateral offset achievable at T (hard over, then rudder amidships at the best moment), m")
    print(f"{'hull':26s}" + "".join(f"{t:>8d}s" for t in Ts))
    for h in HULLS.values():
        print(f"{h.name:26s}" + "".join(f"{envelope(h, T)['reach']:9.0f}" for T in Ts))
    print("\nSame, other options at T = 30 s and 60 s (lateral m; stop/back give along-track shortfall m):")
    print(f"{'hull':26s}{'std15 30':>9}{'rev 30':>8}{'stop 30':>8}{'back 30':>8} |{'std15 60':>9}{'rev 60':>8}{'stop 60':>8}{'back 60':>8}{'gamble60':>9}")
    for h in HULLS.values():
        a, b = envelope(h, 30), envelope(h, 60)
        print(f"{h.name:26s}{a['std15'][0]:9.0f}{a['reverse'][0]:8.0f}{-a['stop'][1]:8.0f}{-a['back'][1]:8.0f} |"
              f"{b['std15'][0]:9.0f}{b['reverse'][0]:8.0f}{-b['stop'][1]:8.0f}{-b['back'][1]:8.0f}{b['sigma3']:9.0f}")




# =============================================================================
# 2b. Light guns, light-gun fire control, stopping power
# =============================================================================
G40 = Gun("40mm/56 Bofors", 0.040, 0.90, 881, 45, 10000, 3.0, defl_ratio=0.6, train_rate=30).fit()
G20 = Gun("20mm/70 Oerlikon", 0.020, 0.123, 844, 45, 4390, 4.0, defl_ratio=0.6, train_rate=40).fit()
G76 = GUNS["76mm/62 OTO"]
G5 = GUNS["5in/38 Mk 12"]

LIGHT = FCLevel("Light gun, lead-computing sight (Mk 51 / Mk 14), tracers", 1943,
                lambda: [fc.Sensor("pointer's eye", "eye", 10, per_min=20, sig_brg_mil=2.0)],
                "cart", True, q=0.5, latency=0.5, prior_kn=5, prior_deg=30, resid=0.02, defl_bias_mil=2.0,
                lay_mil=2.0, director=True, stabilised=False, h_spot=10, salvo_pct=1.0, salvo_defl_mil=2.0,
                spot_gain=0.5)

# Chance that ONE hit stops the target's attack (crippled, fire, steering/torpedo control lost). [INFERRED from
# damage-research 03 and 08: 16in AP passes through a destroyer ~94 %; wooden boats pass shells unfuzed;
# petrol fires; USN destroyers survived 84 % of gun/bomb damage]
P_STOP = {
    ("16in AP", "DD"): 0.10, ("16in HE", "DD"): 0.35, ("8in", "DD"): 0.15, ("6in", "DD"): 0.10,
    ("5in", "DD"): 0.06, ("40mm", "DD"): 0.01,
    ("5in", "MTB"): 0.50, ("76mm", "MTB"): 0.45, ("40mm", "MTB"): 0.12, ("20mm", "MTB"): 0.04,
    ("5in", "FAC"): 0.45, ("76mm", "FAC"): 0.35, ("40mm", "FAC"): 0.15, ("20mm", "FAC"): 0.04,
}

# =============================================================================
# 3. Evader behaviours (what the target's captain does)
# =============================================================================
class Evader:
    """Sets the ship's desired heading (relative to the mission's base course) and speed.
    Sees the enemy's gun flashes (if visible) and the splashes of each salvo, after a reaction delay."""
    react = 4.0

    def __init__(self, rng, **kw):
        self.rng, self.kw = rng, kw
        self.off = 0.0            # heading offset from base, rad
        self.spd = None           # None = mission speed
        self.next_t = 0.0

    def on_flash(self, t, ship, base):
        pass

    def on_splash(self, t, ship, base, mpi):
        pass

    def tick(self, t, ship, base):
        pass


class Steady(Evader):
    name = "steady"


class Zigzag(Evader):
    """Fixed legs +-A every P seconds - the WWII convoy/screen habit; a timer the enemy can learn."""
    name = "zigzag"

    def tick(self, t, ship, base):
        A, P = math.radians(self.kw.get("A", 30)), self.kw.get("P", 120)
        if t >= self.next_t:
            self.off = A if self.off <= 0 else -A
            self.next_t = t + P


class Weave(Evader):
    """Continuous sinusoidal weave ('fishtailing') around the base course."""
    name = "weave"

    def __init__(self, rng, **kw):
        super().__init__(rng, **kw)
        self.ph = rng.uniform(0, 2 * math.pi)

    def tick(self, t, ship, base):
        A, P = math.radians(self.kw.get("A", 30)), self.kw.get("P", 90)
        self.off = A * math.sin(2 * math.pi * t / P + self.ph)


class RandomJink(Evader):
    """Random headings (and speeds for small craft) at random moments, mean dwell D."""
    name = "random"

    def tick(self, t, ship, base):
        A, D = math.radians(self.kw.get("A", 35)), self.kw.get("D", 45)
        if t >= self.next_t:
            self.off = A * self.rng.choice([-1, -0.5, 0, 0.5, 1])
            if self.kw.get("speed"):
                self.spd = ship.h.vmax * self.rng.choice([0.4, 0.7, 1.0, 1.0])
            self.next_t = t + self.rng.exponential(D)


class FlashDodge(Evader):
    """On every enemy flash: gamble on hard left / hold / hard right. Holds steady between salvos,
    which keeps its own guns steadier than constant weaving."""
    name = "flash"

    def on_flash(self, t, ship, base):
        A = math.radians(self.kw.get("A", 35))
        self.off = A * self.rng.choice([-1, 0, 1])
        if self.kw.get("speed"):
            self.spd = ship.h.vmax * self.rng.choice([0.4, 1.0])


class Chase(Evader):
    """Salvo chasing: steer toward the last salvo's fall of shot (the place the enemy has just
    corrected away from). With prob p; otherwise a flash-style gamble."""
    name = "chase"

    def on_splash(self, t, ship, base, mpi):
        A = math.radians(self.kw.get("A", 45))
        d = mpi - ship.p
        if np.hypot(*d) > self.kw.get("seen", 2500):
            return
        if self.rng.random() < self.kw.get("p", 1.0):
            brg = math.atan2(d[1], d[0])
            rel = _wrap(brg - base)
            if abs(rel) > math.radians(150):      # splash astern-ish: turning toward it costs too much
                rel = math.copysign(A, rel)
            self.off = max(-A, min(A, rel))
        else:
            self.off = A * self.rng.choice([-1, 0, 1])


EVADERS = {"steady": Steady, "zigzag": Zigzag, "weave": Weave, "random": RandomJink, "flash": FlashDodge,
           "chase": Chase}


# =============================================================================
# 4. Shooter doctrine (how the gun decides where to aim against a manoeuvring target)
# =============================================================================
@dataclass
class Battery:
    name: str
    gun: Gun
    n: int                     # guns
    interval: float            # s between salvos / bursts
    level: FCLevel
    doctrine: str = "track"    # track | rocking | spread | anticipate | decay | centroid | split | cover
    rmax: float = 1e9          # m
    rmin: float = 300.0
    p_stop: float = 0.0        # chance one hit stops this target [INFERRED, note §7]
    burst: int = 1             # rounds per gun per salvo (automatic weapons)
    spot: str = "auto"         # auto | tracer
    sensors: callable = None
    wait_splash: bool = False  # hold each salvo until the previous one is seen to land ('fire on the splash')
    offset: tuple = (0.0, 0.0) # m, position relative to the main shooter (crossfire from a second ship)


@dataclass
class Duel:
    hull: Hull
    batteries: list
    R0: float
    brg: float = 90.0          # deg, target bearing from shooter's bow at start
    tgt_course: float = 0.0    # deg relative to shooter course (hold mission)
    tgt_kn: float = None
    shooter_kn: float = 20.0
    mission: str = "hold"      # hold | attack
    R_launch: float = 6000.0   # attack: launch range, m
    t_steady: float = 30.0     # attack: s steady before launch (torpedo aim) [INFERRED]
    t_retire: float = 120.0
    evader: str = "steady"
    ev_kw: dict = field(default_factory=dict)
    cond: Conditions = field(default_factory=Conditions)
    duration: float = 900.0
    t_open: float = 60.0       # s of tracking before first salvo
    flash_visible: bool = True # can the target see the enemy fire? (day/night, smoke, calibre)


class Learner:
    """What a fire-control party learns about this target's habits from its own falls of shot.
    After each salvo lands it notes the heading a salvo-chaser would now steer for, then looks at the
    target's heading ~25 s later: moved toward that heading, away from it, or held."""

    def __init__(self):
        self.n = {"toward": 1.0, "away": 1.0, "held": 1.0}   # Laplace prior
        self.pending = []

    def watch(self, t, chase_h, psi_est):
        if abs(_wrap(chase_h - psi_est)) > math.radians(10):   # uninformative if already on that heading
            self.pending.append((t, chase_h, psi_est))

    def update(self, t, psi_est):
        keep = []
        for (t0, ch, psi0) in self.pending:
            if t - t0 >= 25:
                before, after = abs(_wrap(ch - psi0)), abs(_wrap(ch - psi_est))
                if abs(_wrap(psi_est - psi0)) < math.radians(6):
                    self.n["held"] += 1
                elif after < before:
                    self.n["toward"] += 1
                else:
                    self.n["away"] += 1
            else:
                keep.append((t0, ch, psi0))
        self.pending = keep

    def probs(self):
        tot = sum(self.n.values())
        return {k: v / tot for k, v in self.n.items()}


def chase_heading(mean_h, p_tgt, mpi, A=math.radians(45)):
    """The heading a salvo-chaser steers after seeing a splash at mpi."""
    dvec = mpi - p_tgt
    rel = _wrap(math.atan2(dvec[1], dvec[0]) - mean_h)
    if abs(rel) > math.radians(150):
        rel = math.copysign(A, rel)
    return mean_h + max(-A, min(A, rel))


def _mean_heading(hist):
    return math.atan2(np.mean([math.sin(h[1]) for h in hist]), np.mean([math.cos(h[1]) for h in hist]))


def _est_state(trk, t, own_p, own_v, hist):
    """Target state as the computer holds it: position, velocity, turn rate."""
    R0, th0 = trk.rel_at(t, 0.0, own_p, own_v)
    R1, th1 = trk.rel_at(t, 4.0, own_p, own_v)
    p0 = own_p + R0 * np.array([math.cos(th0), math.sin(th0)])
    p1 = own_p + own_v * 4.0 + R1 * np.array([math.cos(th1), math.sin(th1)])
    v = (p1 - p0) / 4.0
    if isinstance(trk, ManoeuvreTracker) and trk.m == "ct":
        w = float(trk.x[4])
    else:
        hist.append((t, math.atan2(v[1], v[0])))
        while hist and t - hist[0][0] > 30:
            hist.pop(0)
        w = 0.0
        if len(hist) >= 2 and hist[-1][0] - hist[0][0] > 8:
            w = _wrap(hist[-1][1] - hist[0][1]) / (hist[-1][0] - hist[0][0])
    return p0, v, w


def _sample_futures(hull, p0, v, w, T, rng, learner, last_mpi, n=24, mean_h=None):
    """Monte-Carlo futures of the target over T s using its class dynamics and the learned habits."""
    U = max(float(np.hypot(*v)), 0.5)
    psi = math.atan2(v[1], v[0])
    pr = learner.probs()
    out = []
    for k in range(n):
        s = Ship(hull, p0, psi, U / KN)
        s.U = U
        s.r = w
        x = max(-1.0, min(1.0, w * hull.Rss / U))
        s.d = math.copysign(abs(x) ** (1 / 0.55), x) * hull.rud_max
        s.cmd_rud = s.d / hull.rud_max
        u = rng.random()
        t_sw = rng.uniform(2, 8)     # evaders turn when they see our flash
        if last_mpi is not None and u < pr["toward"] and mean_h is not None:
            s.des_psi = chase_heading(mean_h, p0, last_mpi)
            plan = []
        elif u < pr["toward"] + pr["away"]:
            plan = [(t_sw, rng.choice([-1.0, 1.0]), None)]
        else:
            plan = [(t_sw, rng.choice([0.0, s.cmd_rud]), None)]
        t, i = 0.0, 0
        while t < T:
            if i < len(plan) and plan[i][0] <= t:
                s.cmd_rud = plan[i][1]
                i += 1
            s.step(1.0)
            t += 1.0
        out.append(s.p.copy())
    return np.array(out)


_REACH = {}


def _reach(hull, T):
    k = (hull.name, int(min(T, 120)))
    if k not in _REACH:
        _REACH[k] = envelope(hull, k[1], react=4.0)["reach"]
    return _REACH[k]


# =============================================================================
# 5. The duel
# =============================================================================
def duel(d, seed=0, trace=False):
    rng = np.random.default_rng(seed)
    cond = d.cond
    dt = 0.5 if (d.hull.L < 60 or min(b.interval for b in d.batteries) < 4) else 1.0
    own_v = np.array([d.shooter_kn * KN, 0.0])
    own_p = np.zeros(2)
    b0 = math.radians(d.brg)
    tp = d.R0 * np.array([math.cos(b0), math.sin(b0)])
    if d.mission == "attack":
        psi0 = math.atan2(-tp[1], -tp[0])
    else:
        psi0 = math.radians(d.tgt_course)
    tgt = Ship(d.hull, tp, psi0, d.tgt_kn or d.hull.vmax)
    ev = EVADERS[d.evader](rng, **d.ev_kw)
    ev.react = d.ev_kw.get("react", 2.5 if d.hull.L < 60 else 4.0)
    phase, t_phase = "approach", 0.0
    st = []
    for b in d.batteries:
        lvl = b.level
        st.append(dict(b=b, sensors=(b.sensors or lvl.sensors)(), nxt=None, trk=None, q=[], next_salvo=None,
                       c_r=0.0, c_d=0.0, ladder=lvl.ladder0, last=0, zb=(rng.normal(), rng.normal()),
                       bias=None, hist=[], learner=Learner(), last_mpi=None, last_seen=-1e9, rock=0))
    for x in st:
        x["nxt"] = [rng.uniform(0, 60 / s.per_min) for s in x["sensors"]]
        x["off"] = np.array(x["b"].offset, float)
        x["bias"] = [rng.normal() for s in x["sensors"]]
    salvos, events, spots = [], [], []
    out = dict(hits=0, shells=0, hits_pre=0, stopped=None, launched=None, salvos=0, by={b.name: [0, 0] for b in d.batteries},
               mpi=[], dodge=[])
    t = 0.0
    while t <= d.duration:
        own_p = own_p + own_v * dt
        rel = tgt.p - own_p
        R = float(np.hypot(*rel))
        th = math.atan2(rel[1], rel[0])
        # ---------------- mission + evasion
        if d.mission == "attack":
            lead = own_p + own_v * (R / max(tgt.U + 1.0, 1.0)) * 0.5
            to = lead - tgt.p
            base = math.atan2(to[1], to[0])
            if phase == "approach" and R <= d.R_launch:
                phase, t_phase = "steady", t
            if phase == "steady" and t - t_phase >= d.t_steady:
                phase, t_phase = "retire", t
                out["launched"] = t if out["stopped"] is None else None
            if phase == "retire":
                base = math.atan2(-rel[1], -rel[0])
                if t - t_phase > d.t_retire:
                    break
        else:
            base = psi0
        if out["stopped"] is not None and d.mission == "attack":
            break
        ev.tick(t, tgt, base)
        while events and events[0][0] <= t:
            _, kind, payload = events.pop(0)
            if kind == "flash":
                ev.on_flash(t, tgt, base)
            else:
                ev.on_splash(t, tgt, base, payload)
        off = 0.0 if phase == "steady" else ev.off
        tgt.des_psi = base + off
        tgt.cmd_spd = ev.spd if (ev.spd is not None and phase != "steady") else (d.tgt_kn or d.hull.vmax)
        tgt.step(dt)
        # ---------------- each battery: sense, track, fire
        R_main = R
        for x in st:
            b, lvl = x["b"], x["b"].level
            own_p_b = own_p + x["off"]
            relb = tgt.p - own_p_b
            R = float(np.hypot(*relb))
            th = math.atan2(relb[1], relb[0])
            for i, s in enumerate(x["sensors"]):
                if t < x["nxt"][i]:
                    continue
                x["nxt"][i] = t + 60 / s.per_min * rng.uniform(0.7, 1.3)
                if not s.can_see(R, 90, cond, d.hull.tc, False):
                    continue
                kn_ = (2.5 if (cond.night and s.kind not in ("radar", "laser")) else 1.0)   # night: eyes and optics [INFERRED]
                sR = s.sigma_range(R, cond, d.hull.tc, cond.operator * cond.combat * kn_)
                if sR is None:
                    continue
                zR = R + x["bias"][i] * s.sigma_bias(R) + rng.normal(0, sR)
                zth = sth = None
                if s.kind != "radar" or not s.bearing_from_optics:
                    sth = s.sig_brg_mil * MIL * (2.0 if (cond.night and s.kind not in ("radar", "laser")) else 1.0)
                    zth = th + rng.normal(0, sth)
                x["q"].append((t + lvl.latency, t, own_p_b.copy(), zR, sR, zth, sth))
                x["last_seen"] = t
            while x["q"] and x["q"][0][0] <= t:
                _, tm, op, zR, sR, zth, sth = x["q"].pop(0)
                if x["trk"] is None:
                    if zth is None:
                        continue
                    est_c = tgt.psi + math.radians(rng.normal(0, lvl.prior_deg))
                    est_s = tgt.U + rng.normal(0, lvl.prior_kn * KN)
                    sv = math.hypot(lvl.prior_kn * KN, tgt.U * math.radians(lvl.prior_deg))
                    x["trk"] = make_tracker(lvl, tm, zR, zth, sR, sth, op, own_v,
                                            est_s * np.array([math.cos(est_c), math.sin(est_c)]), sv)
                    x["next_salvo"] = t + d.t_open
                else:
                    x["trk"].update(tm, op, zR, sR, zth, sth)
            trk = x["trk"]
            if trk is None or x["next_salvo"] is None or t < x["next_salvo"] or out["stopped"] is not None:
                continue
            if not (b.rmin < R < min(b.rmax, b.gun.table["R"][-1])) or t - x["last_seen"] > 40:
                continue
            if (b.wait_splash or b.doctrine == "anticipate") and any(z["x"] is x for z in salvos):
                continue
            if (b.wait_splash or b.doctrine == "anticipate") and t - x.get("t_last_splash", -1e9) < 3.0:
                continue
            x["next_salvo"] = t + b.interval
            # ---- aim
            p_est, v_est, w_est = _est_state(trk, t, own_p_b, own_v, x["hist"])
            vis_now = (cond.night_vis if cond.night else cond.vis) > R
            # a lookout reads the target's heading from its silhouette (target angle), ~10 deg 1 sigma [INFERRED];
            # without a visual the computer's own (lagging) estimate is all there is
            h_obs = tgt.psi + math.radians(rng.normal(0, 10)) if vis_now else math.atan2(v_est[1], v_est[0])
            x["learner"].update(t, h_obs)
            x["hdg_hist"] = [h for h in x.get("hdg_hist", []) if t - h[0] < 240] + [(t, h_obs)]
            Rp, thp = trk.rel_at(t, 0.0, own_p_b, own_v)
            T = b.gun.tof(min(Rp, b.gun.table["R"][-1]))
            for _ in range(3):
                Rp, thp = trk.rel_at(t, T, own_p_b, own_v)
                T = b.gun.tof(min(Rp, b.gun.table["R"][-1]))
            own_f = own_p_b + own_v * T
            aim_pts = None
            ngun = b.n
            doc = b.doctrine
            if doc in ("decay", "centroid", "split", "cover", "anticipate", "aspect", "spread"):
                Tgap = T          # p_est is already the computer's estimate for 'now'
                if doc == "decay":
                    tau = 30.0
                    U = float(np.hypot(*v_est))
                    psi = math.atan2(v_est[1], v_est[0])
                    dpsi = w_est * tau * (1 - math.exp(-Tgap / tau))
                    # mean heading over the arc ~ half the change
                    pos = p_est + U * Tgap * np.array([math.cos(psi + dpsi / 2), math.sin(psi + dpsi / 2)])
                    aim_pts = [pos]
                elif doc in ("centroid", "split", "cover"):
                    smp = _sample_futures(d.hull, p_est, v_est, w_est, Tgap, rng, x["learner"],
                                          x["last_mpi"] if t - x.get("t_last_splash", -1e9) < 20 else None,
                                          mean_h=_mean_heading(x["hdg_hist"]))
                    if doc == "split":
                        u_l = np.array([-math.sin(thp), math.cos(thp)])     # across the line of fire
                        key = smp @ u_l
                        idx = np.argsort(key)
                        groups = np.array_split(idx, 3)
                        aim_pts = [smp[g].mean(axis=0) for g in groups]
                    else:
                        aim_pts = [smp.mean(axis=0)]
                        x["cover_sd"] = smp.std(axis=0)
                elif doc in ("anticipate", "aspect"):
                    rel_aim = Rp * np.array([math.cos(thp), math.sin(thp)])
                    pos = own_f + rel_aim
                    pr = x["learner"].probs()
                    U_est = float(np.hypot(*v_est))
                    if vis_now:
                        # 'aspect': re-aim along the heading the lookouts can SEE (target angle), not the lagging
                        # computed course - the WWII spotter's target-angle input
                        sim = Ship(d.hull, p_est, h_obs, U_est / KN)
                        tt = 0.0
                        while tt < Tgap:
                            sim.step(1.0)
                            tt += 1.0
                        pos = sim.p
                    if doc == "anticipate" and x["last_mpi"] is not None and \
                            t - x.get("t_last_splash", -1e9) < 20 and pr["toward"] > 0.4:
                        # the target will steer for the splash it has just seen: simulate that and lead it there
                        mean_h = _mean_heading(x["hdg_hist"])
                        chase_h = chase_heading(mean_h, p_est, x["last_mpi"])
                        h0 = h_obs
                        if not vis_now and x.get("exp_h") is not None and pr["toward"] > 0.5:
                            h0 = x["exp_h"]
                        x["exp_h"] = chase_h
                        sim = Ship(d.hull, p_est, h0, U_est / KN)
                        delay = max(0.0, x["t_last_splash"] + 4.0 - t)
                        tt = 0.0
                        while tt < Tgap:
                            if tt >= delay:
                                sim.des_psi = chase_h
                            sim.step(1.0)
                            tt += 1.0
                        pt = pr["toward"]
                        pos = pt * sim.p + (1 - pt) * pos
                    aim_pts = [pos]
                elif doc == "spread":
                    rel_aim = Rp * np.array([math.cos(thp), math.sin(thp)])
                    aim_pts = [own_f + rel_aim]
                if False:
                    # convert absolute aim points to (range, bearing) from own future position
                    pass
            if aim_pts is None:
                aim_pts = [own_f + Rp * np.array([math.cos(thp), math.sin(thp)])]
            # ---- lay and fire
            sea_k = 1.0 if lvl.stabilised else 1.0 + 0.25 * max(cond.sea - 3, 0)
            lay = lvl.lay_mil * MIL * sea_k * cond.combat
            per = [ngun // len(aim_pts) + (1 if k < ngun % len(aim_pts) else 0) for k in range(len(aim_pts))]
            shells = []
            rock = 0.0
            if doc == "rocking":
                rock = [0.0, 1.0, -1.0][x["rock"] % 3]
                x["rock"] += 1
            Rcen = None
            for ap, ng in zip(aim_pts, per):
                if ng <= 0:
                    continue
                relv = ap - own_f
                Ra = float(np.hypot(*relv))
                tha = math.atan2(relv[1], relv[0])
                if not (b.rmin < Ra < b.gun.table["R"][-1]):
                    continue
                nshell = ng * b.burst
                sr, sd = b.gun.sigma_D(Ra, max(nshell, 1))
                R_ord = max(0.5 * Ra, min(1.5 * Ra, Ra + x["c_r"] + rock * 2.0 * sr))
                e_r = rng.normal(0, lay) * b.gun.dR_del(R_ord) + rng.normal(0, cond.combat * lvl.salvo_pct / 100 * R_ord) \
                      + x["zb"][0] * lvl.resid * R_ord
                e_d = rng.normal(0, lay) * R_ord + rng.normal(0, cond.combat * lvl.salvo_defl_mil * MIL * R_ord) \
                      + x["zb"][1] * lvl.defl_bias_mil * MIL * R_ord + x["c_d"]
                if not lvl.director:
                    sr = math.hypot(sr, lay * b.gun.dR_del(R_ord))
                    sd = math.hypot(sd, lay * R_ord)
                xs, xd = 0.0, 0.0
                if doc == "cover" and "cover_sd" in x:
                    u = np.array([math.cos(tha), math.sin(tha)])
                    w_ = np.array([-u[1], u[0]])
                    env_r = float(np.sqrt(np.mean(((x["cover_sd"]) * u) ** 2)) * math.sqrt(2))
                    env_d = float(np.sqrt(np.mean(((x["cover_sd"]) * w_) ** 2)) * math.sqrt(2))
                    xs = math.sqrt(max(0.0, (0.8 * env_r) ** 2 - sr ** 2))
                    xd = math.sqrt(max(0.0, (0.8 * env_d) ** 2 - sd ** 2))
                if doc == "spread":
                    reach = _reach(d.hull, T + 4)
                    xs = xd = 0.4 * reach
                R_act = R_ord + e_r
                th_act = tha + e_d / R_ord
                T_act = b.gun.tof(min(R_act, b.gun.table["R"][-1]))
                u = np.array([math.cos(th_act), math.sin(th_act)])
                w_ = np.array([-u[1], u[0]])
                centre = own_p_b + own_v * T_act + u * R_act
                for _ in range(nshell):
                    shells.append(centre + u * (rng.normal(0, sr) + rng.normal(0, xs) if xs else rng.normal(0, sr))
                                  + w_ * (rng.normal(0, sd) + (rng.normal(0, xd) if xd else 0.0)))
                Rcen = R_act if Rcen is None else Rcen
            if not shells:
                continue
            T_imp = b.gun.tof(min(Rcen, b.gun.table["R"][-1]))
            salvos.append(dict(R_at=R, x=x, t_imp=t + T_imp, pts=shells, u=u, R=Rcen, c_r=x["c_r"], c_d=x["c_d"],
                               pattern=4.0 * b.gun.sigma_D(Rcen, max(len(shells), 1))[0], aim=aim_pts))
            if d.flash_visible:
                events.append((t + ev.react, "flash", None))
                events.sort(key=lambda e: e[0])
        # ---------------- impacts
        R = R_main
        for sv_ in [z for z in salvos if z["t_imp"] <= t]:
            salvos.remove(sv_)
            x, b = sv_["x"], sv_["x"]["b"]
            R = float(np.hypot(*(tgt.p - (own_p + x["off"]))))
            u = sv_["u"]
            w_ = np.array([-u[1], u[0]])
            fall = b.gun.fall(min(sv_["R"], b.gun.table["R"][-1]))
            aspect = _wrap(tgt.psi - math.atan2(u[1], u[0]))
            xs, ys, hits = [], [], 0
            for pnt in sv_["pts"]:
                dd = pnt - tgt.p
                xx, yy = float(dd @ u), float(dd @ w_)
                xs.append(xx); ys.append(yy)
                if hit_test(xx, yy, fall, d.hull.tc, aspect):
                    hits += 1
                    if out["stopped"] is None and rng.random() < b.p_stop:
                        out["stopped"] = t
            out["shells"] += len(xs); out["hits"] += hits; out["salvos"] += 1
            out["salvo_hit"] = out.get("salvo_hit", 0) + (hits > 0)
            out["by"][b.name][0] += len(xs); out["by"][b.name][1] += hits
            if out["launched"] is None and d.mission == "attack" and phase != "retire":
                out["hits_pre"] += hits
            mx, my = float(np.mean(xs)), float(np.mean(ys))
            mpi_world = tgt.p + u * mx + w_ * my
            out["mpi"].append((mx, my))
            if sv_["aim"]:
                am = np.mean(sv_["aim"], axis=0) - tgt.p     # where the computer aimed vs where the ship is
                out["dodge"].append((float(am @ u), float(am @ w_)))
            events.append((t + ev.react, "splash", mpi_world))
            events.sort(key=lambda e: e[0])
            # the shooter's own learning: which side of the target did this salvo fall?
            if x["trk"] is not None and x.get("hdg_hist"):
                pe, ve, _ = _est_state(x["trk"], t, own_p + x["off"], own_v, [])
                vis_now = (cond.night_vis if cond.night else cond.vis) > R
                h_now = tgt.psi + math.radians(rng.normal(0, 10)) if vis_now else math.atan2(ve[1], ve[0])
                x["learner"].watch(t, chase_heading(_mean_heading(x["hdg_hist"]), pe, mpi_world), h_now)
            x["last_mpi"] = mpi_world
            x["t_last_splash"] = t
            if trace:
                print(f"  t={t:5.0f} {b.name:8s} R={R:6.0f} MPI {mx:+6.0f} {my:+5.0f} m hits {hits}")
            # spotting
            lvl = b.level
            radar = [z for z in x["sensors"] if z.kind == "radar" and z.splash != "none" and R < z.splash_max]
            vis = cond.night_vis if cond.night else cond.vis
            obs = dict(c_r=sv_["c_r"], c_d=sv_["c_d"], pattern=sv_["pattern"], t_ready=t + 6)
            if b.spot == "tracer" and R < vis:
                kt = 3.0 if cond.night else 1.0     # at night the gunner sees his tracers but not the boat clearly
                obs.update(range=mx + rng.normal(0, kt * 1.5 * MIL * R), defl=my + rng.normal(0, kt * 1.5 * MIL * R), t_ready=t + 1)
            elif radar:
                z = radar[0]
                obs["range"] = mx + rng.normal(0, max(25.0, 2 * z.sigma_range(R, cond, d.hull.tc)))
                obs["t_ready"] = t + 4
                if z.splash == "both":
                    obs["defl"] = my + rng.normal(0, 2 * MIL * R)
            elif R < vis:
                so = 4.0e-5 * cond.spot_k * cond.combat * R * R / lvl.h_spot
                common = rng.normal(0, 0.5 * so)
                seen = [v + common + rng.normal(0, so) for v in xs]
                ov, sh = sum(v > 0 for v in seen), sum(v < 0 for v in seen)
                obs["sign"] = "straddle" if (hits or (ov and sh)) else ("over" if ov else "short")
                obs["defl"] = my + rng.normal(0, 1.0 * MIL * R)
            spots.append((x, obs))
        for (x, o) in [z for z in spots if z[1]["t_ready"] <= t]:
            spots.remove((x, o))
            lvl = x["b"].level
            if "range" in o:
                if abs(o["range"]) > 0.5 * o["pattern"] or x["b"].spot == "tracer":
                    x["c_r"] = o["c_r"] - lvl.spot_gain * o["range"]
            elif "sign" in o:
                stp = x["ladder"]
                smin = max(46.0, 0.5 * o["pattern"])
                if o["sign"] == "straddle":
                    x["ladder"] = max(smin, stp / 2)
                    x["c_r"] = o["c_r"]
                else:
                    dd = -1 if o["sign"] == "over" else 1
                    if x["last"] and dd != x["last"]:
                        x["ladder"] = stp = max(smin, stp / 2)
                    x["c_r"] = o["c_r"] + dd * stp
                    x["last"] = dd
            if "defl" in o and (abs(o["defl"]) > 0.25 * o["pattern"] or x["b"].spot == "tracer"):
                x["c_d"] = o["c_d"] - lvl.spot_gain * o["defl"]
        t += dt
    return out


def run_duel(d, n=30, seed0=1):
    hits = shells = 0
    stops = launched = 0
    salv = shit = 0
    pre = []
    by = {}
    for k in range(n):
        o = duel(d, seed=seed0 + k)
        hits += o["hits"]; shells += o["shells"]
        salv += o["salvos"]; shit += o.get("salvo_hit", 0)
        stops += o["stopped"] is not None and (o["launched"] is None)
        launched += o["launched"] is not None
        pre.append(o["hits_pre"])
        for nm, (s_, h_) in o["by"].items():
            by.setdefault(nm, [0, 0])
            by[nm][0] += s_; by[nm][1] += h_
    return dict(pct=100 * hits / max(shells, 1), p_salvo=100 * shit / max(salv, 1), shells=shells / n,
                hits10=hits / n / (d.duration / 600.0), hits=hits / n, p_stop=stops / n,
                p_launch=launched / n, hits_pre=float(np.mean(pre)),
                by={k: (100 * v[1] / max(v[0], 1), v[0] / n) for k, v in by.items()})



# =============================================================================
# 6. Tables
# =============================================================================
def _phi(x):
    return 0.5 * (1 + math.erf(x / math.sqrt(2)))


def gamble_multiplier(hull, gun, R, fc_pct, react=4.0, latency=2.0):
    """Analytic: hit chance of a target that gambles hard-left / hold / hard-right after the flash,
    relative to a steady target. Turning displaces the ship across its own heading = along its narrow axis,
    so the relevant hitting window is the narrow one (range window if broadside, beam if bow-on)."""
    T = gun.tof(min(R, gun.table["R"][-1])) + latency
    reach = envelope(hull, T, react=react)["reach"]
    danger, depth, width = fc.hitting_space(hull.tc, R, gun, 90)
    h = 0.5 * (danger + depth)            # broadside: the turn moves the ship along the line of fire
    sr, _ = gun.sigma_D(R, 9)
    sig = math.hypot(sr, fc_pct / 100 * R)
    P = lambda x: _phi((x + h) / sig) - _phi((x - h) / sig)
    return T, reach, (P(0) + 2 * P(reach)) / 3 / P(0)


def gamble_table():
    print("\n=== 3. When does dodging pay? Analytic: hits vs a target gambling hard-left/hold/hard-right after each")
    print("     enemy flash, as a fraction of hits on a steady target. Broadside target; 4 s to react; 2 s latency.")
    print("     Two shooter qualities: WWII (MPI error 1.8 % of range) and digital (0.8 %). cell = WWII / digital")
    cases = [("BB fast (Iowa)", "16in/50 Mk 7", (10, 15, 20, 25, 30, 35)),
             ("CA (Baltimore)", "8in/55 Mk 9", (8, 12, 16, 20, 24, 28)),
             ("DD (Fletcher)", "5in/38 Mk 12", (3, 5, 7, 9, 11, 13)),
             ("DD (Fletcher)", "16in/50 Mk 7", (5, 10, 15, 20, 25, 30)),
             ("MTB / PT (Elco 80)", "40mm", (1, 2, 3, 4, 5, 6)),
             ("MTB / PT (Elco 80)", "5in/38 Mk 12", (1, 2, 4, 6, 8, 10))]
    for hn, gn, ks in cases:
        g = G40 if gn == "40mm" else GUNS[gn]
        hull = HULLS[hn]
        unit = 1000 if gn == "40mm" or (hn.startswith("MTB")) else KYD
        lab = "km" if unit == 1000 else "kyd"
        cells = []
        for k in ks:
            R = k * unit
            if R >= g.table["R"][-1] * 0.95:
                cells.append(f"{'-':>18}")
                continue
            T, reach, m1 = gamble_multiplier(hull, g, R, 1.8)
            _, _, m2 = gamble_multiplier(hull, g, R, 0.8)
            cells.append(f"{k:>3}{lab[0]}:{m1:4.2f}/{m2:4.2f} r{reach:4.0f}")
        print(f"  {hn[:18]:18s} vs {gn[:12]:12s} " + " ".join(cells))
    print("  (r = metres the target can get off the predicted point; ranges in kyd except the MTB rows in km)")


SH16 = lambda lvl, doc="track", **kw: Battery("16in", GUNS["16in/50 Mk 7"], 9, 30, lvl, doc, **kw)
SH8 = lambda lvl, doc="track", **kw: Battery("8in", GUNS["8in/55 Mk 9"], 9, 20, lvl, doc, **kw)
SHOOTERS = [("WWII optical (Mk 8), track", "5", "track"), ("WWII optical, aspect", "5", "aspect"),
            ("WWII optical, anticipate", "5", "anticipate"), ("WWII radar (Mk 8+Mk 13), track", "6", "track"),
            ("WWII radar, anticipate", "6", "anticipate"), ("Digital (turn filter), track", "8", "track"),
            ("Digital, decaying turn", "8", "decay"), ("Digital, anticipate", "8", "anticipate")]
STYLES = [("steady", {}), ("zigzag", dict(A=30, P=120)), ("weave", dict(A=30, P=90)), ("random", dict(A=35, D=45)),
          ("flash", dict(A=35)), ("chase", dict(A=45))]


def matrix(hull, mk, ranges, unit=KYD, n=16, duration=900, shooters=SHOOTERS, styles=STYLES, title=""):
    print(title)
    print(f"{'shooter':34s}{'range':>6}" + "".join(f"{nm:>9s}" for nm, _ in styles))
    res = {}
    for lab, lv, doc in shooters:
        for k in ranges:
            row = []
            for nm, kw in styles:
                d = Duel(hull, [mk(LV[lv], doc)], k * unit, brg=90, tgt_course=0, duration=duration,
                         evader=nm, ev_kw=kw, cond=Conditions(combat=1.5, vis=35000))
                r = run_duel(d, n=n)
                row.append(r["pct"])
                res[(lab, k, nm)] = r["pct"]
            print(f"{lab:34s}{k:>6}" + "".join(f"{v:9.1f}" for v in row))
    return res


def bb_matrix(n=16):
    return matrix(HULLS["BB fast (Iowa)"], SH16, (12, 20, 28), n=n,
                  title="\n=== 4. Battleship duel: 9 x 16in/50 every 30 s vs an Iowa-class target, 25 kn, broadside start, "
                        "15 min, combat. Hit % per shell ===")


def ca_matrix(n=16):
    return matrix(HULLS["CA (Baltimore)"], SH8, (10, 16, 22), n=n,
                  title="\n=== 5. Cruiser duel: 9 x 8in/55 every 20 s vs a Baltimore-class target, 32 kn. Hit % per shell ===")


def dd_matrix(n=16):
    shooters = [("WWII 5in/38 Mk 37 + radar, track", "6", "track"), ("WWII 5in/38, anticipate", "6", "anticipate"),
                ("Digital 5in/54, track", "8", "track"), ("Digital 5in/54, decaying turn", "8", "decay"),
                ("Digital 5in/54, anticipate", "8", "anticipate")]
    def mk(lvl, doc):
        if lvl is LV["8"]:
            return Battery("5in54", GUNS["5in/54 Mk 45"], 1, 3, lvl, doc)
        return Battery("5in38", G5, 8, 4, lvl, doc)
    return matrix(HULLS["DD (Fletcher)"], mk, (4, 8, 12), n=n, shooters=shooters, duration=600,
                  title="\n=== 6. Destroyer as target: 8 x 5in/38 every 4 s (WWII) or 1 x 5in/54 every 3 s (digital), "
                        "35 kn target. Hit % per shell ===")


def mtb_matrix(n=16):
    shooters = [("40mm quad, Mk 51 + tracers", "LIGHT", "track"), ("5in/38 x4, Mk 37 + radar", "6", "track"),
                ("76mm OTO, digital", "8", "track"), ("76mm OTO, digital, cover", "8", "cover")]
    styles = [("steady", {}), ("weave", dict(A=40, P=30)), ("random", dict(A=60, D=12, speed=True)),
              ("flash", dict(A=60, speed=True)), ("chase", dict(A=60))]
    def mk(lvl, doc):
        if lvl is LIGHT:
            return Battery("40mm", G40, 4, 1.0, LIGHT, doc, burst=2, spot="tracer", rmax=4500)
        if lvl is LV["8"]:
            return Battery("76mm", G76, 1, 0.7, lvl, doc)
        return Battery("5in38", G5, 4, 4, lvl, doc)
    print("\n=== 7. MTB as target (Elco 80 ft, 40 kn), day. Hit % per round ===")
    print(f"{'shooter':34s}{'km':>6}" + "".join(f"{nm:>9s}" for nm, _ in styles))
    for lab, lv, doc in shooters:
        lvl = LIGHT if lv == "LIGHT" else LV[lv]
        for k in (1, 2, 3, 5):
            row = []
            for nm, kw in styles:
                d = Duel(HULLS["MTB / PT (Elco 80)"], [mk(lvl, doc)], k * 1000, brg=90, tgt_course=0, duration=240,
                         evader=nm, ev_kw=kw, cond=Conditions(combat=1.5), t_open=15)
                row.append(run_duel(d, n=n)["pct"])
            print(f"{lab:34s}{k:>6}" + "".join(f"{v:9.2f}" for v in row))


def attack_runs(n=24):
    print("\n=== 8. Destroyer torpedo attack on a battleship, combat. Start 18 kyd on the bow, attacker 35 kn,")
    print("     30 s steady to fire. cell = P(attacker stopped before launch) / mean hits before launch ===")
    hull = HULLS["DD (Fletcher)"]
    G6 = GUNS["6in/47 Mk 16"]
    defences = {
        "USN 1944 BB, day (16in + 10x5in/38 Mk 37 radar + 40mm)": (lambda: [
            Battery("16in", GUNS["16in/50 Mk 7"], 9, 30, LV["6"], p_stop=P_STOP[("16in AP", "DD")]),
            Battery("5in38", G5, 10, 4, LV["6"], p_stop=P_STOP[("5in", "DD")], rmax=15000),
            Battery("40mm", G40, 16, 1.0, LIGHT, burst=2, spot="tracer", rmax=2500, p_stop=P_STOP[("40mm", "DD")])],
            Conditions(combat=1.5, vis=30000)),
        "same, night (radar 5in; optics 4 km)": (lambda: [
            Battery("16in", GUNS["16in/50 Mk 7"], 9, 30, LV["6"], p_stop=P_STOP[("16in AP", "DD")]),
            Battery("5in38", G5, 10, 4, LV["6"], p_stop=P_STOP[("5in", "DD")], rmax=15000),
            Battery("40mm", G40, 16, 1.0, LIGHT, burst=2, spot="tracer", rmax=2500, p_stop=P_STOP[("40mm", "DD")])],
            Conditions(combat=1.5, night=True, night_vis=4000)),
        "night, no radar (IJN/KM style; searchlights 4 km)": (lambda: [
            Battery("5in38", G5, 8, 5, LV["5"], p_stop=P_STOP[("5in", "DD")], rmax=15000,
                    sensors=lambda: [fc.S_RF("4.5 m stereo", 4.5, 20, "stereo", h=25, per_min=6)])],
            Conditions(combat=1.5, night=True, night_vis=4000)),
        "WWI dreadnought, day (12 x 6in casemates, Dreyer)": (lambda: [
            Battery("6in", G6, 6, 6, LV["3"], p_stop=0.15, rmax=12000)],
            Conditions(combat=1.5, vis=16000)),
    }
    styles = [("steady", {}), ("weave", dict(A=30, P=60)), ("flash", dict(A=40)), ("chase", dict(A=45))]
    for lab, (dfn, cond) in defences.items():
        print(f"  {lab}")
        print(f"  {'launch':10s}" + "".join(f"{nm:>14s}" for nm, _ in styles))
        for lk in (10, 8, 6, 4):
            cells = ""
            for nm, kw in styles:
                d = Duel(hull, dfn(), 18 * KYD, brg=30, mission="attack", R_launch=lk * KYD, evader=nm, ev_kw=kw,
                         cond=cond, duration=1200, t_open=30, shooter_kn=20)
                r = run_duel(d, n=n)
                cells += f"{r['p_stop']:8.2f} /{r['hits_pre']:4.1f}"
            print(f"  {lk:>4} kyd {cells}")


def mtb_runs(n=40):
    print("\n=== 9. MTB attack on a destroyer, combat. 40 kn. 10 s steady to fire. cell = P(stopped before launch) ===")
    print("     Defence: 5 x 5in/38 (Mk 37 + radar, 4 s), 4 x 40mm (Mk 51, tracers, 3.5 km), 6 x 20mm (1.8 km).")
    hull = HULLS["MTB / PT (Elco 80)"]
    def defence():
        return [Battery("5in38", G5, 5, 4, LV["6"], p_stop=P_STOP[("5in", "MTB")], rmax=12000),
                Battery("40mm", G40, 4, 1.0, LIGHT, burst=2, spot="tracer", rmax=3500, p_stop=P_STOP[("40mm", "MTB")]),
                Battery("20mm", G20, 6, 0.5, LIGHT, burst=2, spot="tracer", rmax=1800, p_stop=P_STOP[("20mm", "MTB")])]
    styles = [("steady", {}), ("weave", dict(A=40, P=30)), ("random", dict(A=50, D=10, speed=True)),
              ("flash", dict(A=50, speed=True))]
    for cond_lab, cond, start in (("day, seen at 8 km", Conditions(combat=1.5), 8000),
                                  ("night, radar-directed 5in; boat seen by eye at 2 km", Conditions(combat=1.5, night=True, night_vis=2000), 8000),
                                  ("night, no radar; boat seen at 2 km (starshell, wake)", Conditions(combat=1.5, night=True, night_vis=2000), 8000),
                                  ("night, no radar; quiet approach, seen at 1.2 km", Conditions(combat=1.5, night=True, night_vis=1200), 8000)):
        print(f"  {cond_lab}")
        print(f"  {'launch':10s}" + "".join(f"{nm:>10s}" for nm, _ in styles))
        for lk in (4000, 3000, 2000, 1000, 500):
            cells = ""
            for nm, kw in styles:
                dfn = defence()
                if "no radar" in cond_lab:
                    dfn[1] = replace(dfn[1], level=replace(LIGHT, salvo_pct=2.0))   # [INFERRED] untrained night light AA
                    dfn[0] = replace(dfn[0], level=LV["5"], sensors=lambda: [fc.S_RF("Mk 42 15 ft", 4.57, 24, "stereo", h=25, per_min=6)])
                d = Duel(hull, dfn, start, brg=40, mission="attack", R_launch=lk, t_steady=10, evader=nm, ev_kw=kw,
                         cond=cond, duration=900, t_open=10, shooter_kn=25, ev_react=2.0) if False else \
                    Duel(hull, dfn, start, brg=40, mission="attack", R_launch=lk, t_steady=10, evader=nm, ev_kw=kw,
                         cond=cond, duration=900, t_open=10, shooter_kn=25)
                r = run_duel(d, n=n)
                cells += f"{r['p_stop']:10.2f}"
            print(f"  {lk:>6} m  {cells}")


def chase_game(n=24):
    print("\n=== 10. The salvo-chasing game: cruiser vs cruiser, 16 kyd, 8in, 25 min, combat. Hit % per shell")
    print("     rows: how often the target chases (otherwise it gambles left/hold/right on each splash);")
    print("     columns: shooter's fire control and doctrine ===")
    hull = HULLS["CA (Baltimore)"]
    docs = [("optical track", "5", "track", False), ("optical aspect", "5", "aspect", True),
            ("optical anticip.", "5", "anticipate", True), ("radar track", "6", "track", False),
            ("radar anticip.", "6", "anticipate", True)]
    print(f"{'p(chase)':10s}" + "".join(f"{nm:>17s}" for nm, *_ in docs))
    for pch in (0.0, 0.5, 0.75, 1.0):
        cells = ""
        for nm, lv, doc, ws in docs:
            b = Battery("8in", GUNS["8in/55 Mk 9"], 9, 20, LV[lv], doc, wait_splash=ws)
            d = Duel(hull, [b], 16 * KYD, brg=90, duration=1500, evader="chase", ev_kw=dict(A=45, p=pch),
                     cond=Conditions(combat=1.5, vis=35000))
            cells += f"{run_duel(d, n=n)['pct']:17.1f}"
        print(f"{pch:<10.2f}{cells}")
    print("  reference: steady target, optical track =", end=" ")
    b = Battery("8in", GUNS["8in/55 Mk 9"], 9, 20, LV["5"])
    print(f"{run_duel(Duel(hull, [b], 16 * KYD, brg=90, duration=1500, cond=Conditions(combat=1.5, vis=35000)), n=n)['pct']:.1f}")


def crossfire_table(n=20):
    print("\n=== 11. Crossfire: same total guns, one ship vs two ships 90 deg apart (digital FC, 16in, 25 kyd). Hit % ===")
    hull = HULLS["BB fast (Iowa)"]
    print(f"{'':28s}" + "".join(f"{nm:>9s}" for nm, _ in STYLES))
    for lab, bats in (("one ship, 9 guns", [SH16(LV["8"])]),
                      ("two ships, 2 x 9 guns", [SH16(LV["8"]), replace(SH16(LV["8"]), name="16inB", offset=(25 * KYD, 25 * KYD))])):
        row = []
        for nm, kw in STYLES:
            d = Duel(hull, bats, 25 * KYD, brg=90, duration=900, evader=nm, ev_kw=kw, cond=Conditions(combat=1.5))
            row.append(run_duel(d, n=n)["pct"])
        print(f"{lab:28s}" + "".join(f"{v:9.1f}" for v in row))


def own_cost_table(n=30):
    print("\n=== 12. What evasion costs your OWN gunnery (fire_control_ref engagement: you, zigzagging, shoot at a")
    print("     steady BB at 20 kyd). Hit % of your guns; zigzag = +-A deg legs every P s ===")
    sh = fc.Shooter(GUNS["16in/50 Mk 7"], interval=30)
    print(f"{'level':46s}{'steady':>8}{'30/120':>8}{'30/60':>8}{'45/45':>8}")
    for k in ("3", "5", "6", "8"):
        row = []
        for pol, amp, per in (("steady", 0, 300), ("zigzag", 30, 120), ("zigzag", 30, 60), ("zigzag", 45, 45)):
            scn = fc.Scenario("own", sh, TARGETS["BB"], 20 * KYD, tgt_brg=90, tgt_course=0, tgt_speed=25,
                              own_policy=pol, own_amp=amp, own_period=per, duration=900, cond=Conditions(combat=1.5))
            row.append(fc.run(scn, LV[k], n)["pct"])
        print(f"{LV[k].name:46s}" + "".join(f"{v:8.1f}" for v in row))


def rate_table(n=20):
    print("\n=== 13. Volume matters: 'fire on the splash' costs rate of fire. Hits per 10 min (and % per shell),")
    print("     battleship duel as table 4 ===")
    hull = HULLS["BB fast (Iowa)"]
    styles = [("steady", {}), ("flash", dict(A=35)), ("chase", dict(A=45)), ("weave", dict(A=30, P=90))]
    print(f"{'shooter':30s}{'kyd':>5}" + "".join(f"{nm:>15s}" for nm, _ in styles))
    for lab, lv, doc in (("WWII radar, track", "6", "track"), ("WWII radar, anticipate", "6", "anticipate"),
                         ("Digital, track", "8", "track"), ("Digital, anticipate", "8", "anticipate")):
        for k in (12, 20, 28):
            cells = ""
            for nm, kw in styles:
                d = Duel(hull, [SH16(LV[lv], doc)], k * KYD, brg=90, duration=900, evader=nm, ev_kw=kw,
                         cond=Conditions(combat=1.5, vis=35000))
                r = run_duel(d, n=n)
                cells += f"{r['hits10']:8.1f} ({r['pct']:4.1f})"
            print(f"{lab:30s}{k:>5}{cells}")


def doctrine_table(n=20):
    print("\n=== 14. Doctrines that look clever but barely pay. Battleship duel at 20 kyd, hit % per shell ===")
    hull = HULLS["BB fast (Iowa)"]
    styles = [("steady", {}), ("zigzag", dict(A=30, P=120)), ("weave", dict(A=30, P=90)), ("flash", dict(A=35)),
              ("chase", dict(A=45))]
    print(f"{'shooter':34s}" + "".join(f"{nm:>9s}" for nm, _ in styles))
    for lab, lv, doc in (("WWII radar, track", "6", "track"), ("WWII radar, rocking ladder", "6", "rocking"),
                         ("WWII radar, deliberate spread", "6", "spread"), ("Digital, track", "8", "track"),
                         ("Digital, decaying turn", "8", "decay"), ("Digital, centroid of futures", "8", "centroid"),
                         ("Digital, split salvo (3 aims)", "8", "split"), ("Digital, cover envelope", "8", "cover")):
        row = ""
        for nm, kw in styles:
            d = Duel(hull, [SH16(LV[lv], doc)], 20 * KYD, brg=90, duration=900, evader=nm, ev_kw=kw,
                     cond=Conditions(combat=1.5, vis=35000))
            row += f"{run_duel(d, n=n)['pct']:9.1f}"
        print(f"{lab:34s}{row}")


def sufficiency_table(n=16):
    print("\n=== 15. How much evasion is enough? Hit % per shell vs gentle and hard versions of each style, combat ===")
    cases = [
        ("BB target, 16in, WWII radar track", HULLS["BB fast (Iowa)"], lambda: SH16(LV["6"]), (12, 20, 28), KYD, 900),
        ("BB target, 16in, digital anticipate", HULLS["BB fast (Iowa)"], lambda: SH16(LV["8"], "anticipate"), (12, 20, 28), KYD, 900),
        ("DD target, 8 x 5in/38 WWII radar", HULLS["DD (Fletcher)"], lambda: Battery("5in38", G5, 8, 4, LV["6"]), (4, 8, 12), KYD, 600),
        ("DD target, 5in/54 digital", HULLS["DD (Fletcher)"], lambda: Battery("5in54", GUNS["5in/54 Mk 45"], 1, 3, LV["8"]), (4, 8, 12), KYD, 600),
    ]
    styles = [("steady", "steady", {}), ("zz 15/180", "zigzag", dict(A=15, P=180)), ("zz 30/120", "zigzag", dict(A=30, P=120)),
              ("zz 45/60", "zigzag", dict(A=45, P=60)), ("flash 15", "flash", dict(A=15)), ("flash 35", "flash", dict(A=35)),
              ("weave 15/90", "weave", dict(A=15, P=90)), ("chase 45", "chase", dict(A=45))]
    for lab, hull, mk, ranges, unit, dur in cases:
        print(f"  {lab}")
        print(f"  {'range':8s}" + "".join(f"{nm:>12s}" for nm, *_ in styles))
        for k in ranges:
            row = ""
            for nm, ev, kw in styles:
                d = Duel(hull, [mk()], k * unit, brg=90, duration=dur, evader=ev, ev_kw=kw,
                         cond=Conditions(combat=1.5, vis=35000))
                row += f"{run_duel(d, n=n)['pct']:12.1f}"
            print(f"  {k:>4} kyd{row}")


def short_range_table(n=12):
    print("\n=== 16. Battleship at short range: does evasion still pay? 16in, 10 min, combat. Hit % per shell ===")
    styles = (("steady", {}), ("zigzag", dict(A=30, P=120)), ("chase", dict(A=45)), ("flash", dict(A=35)))
    print(f"{'shooter':24s}{'kyd':>5}" + "".join(f"{nm:>9s}" for nm, _ in styles))
    for lab, lv, doc in (("WWII optical track", "5", "track"), ("WWII radar track", "6", "track"),
                         ("Digital anticipate", "8", "anticipate")):
        for k in (6, 9):
            row = ""
            for ev, kw in styles:
                d = Duel(HULLS["BB fast (Iowa)"], [SH16(LV[lv], doc)], k * KYD, brg=90, duration=600, evader=ev,
                         ev_kw=kw, cond=Conditions(combat=1.5, vis=35000))
                row += f"{run_duel(d, n=n)['pct']:9.1f}"
            print(f"{lab:24s}{k:>5}{row}")


if __name__ == "__main__":
    quick = len(sys.argv) > 1 and sys.argv[1] == "quick"
    only = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] not in ("quick",) else None
    if only:
        globals()[only]()
        sys.exit()
    dynamics_table()
    envelope_table()
    gamble_table()
    if not quick:
        bb_matrix()
        ca_matrix()
        dd_matrix()
        mtb_matrix()
        attack_runs()
        mtb_runs()
        chase_game()
        crossfire_table()
        own_cost_table()
        rate_table()
        doctrine_table()
        sufficiency_table()
        short_range_table()
