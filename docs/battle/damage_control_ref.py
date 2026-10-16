"""
damage_control_ref.py -- reference model for flooding + damage control (Naval project)

Companion to claude/damage-research/09_damage_control.md.
Not game code: a readable, runnable spec that shows the mechanics and lets us
calibrate numbers against historical cases.

Model in one paragraph
----------------------
The hull is cut into longitudinal sections. Each section has up to four
compartments: port wing (TDS), starboard wing, inner (below the armour deck),
upper (armour deck to main deck). Water enters through HOLES (orifice flow,
Q = Cd*A*sqrt(2 g dh), plus a ram head at the bow when under way) and moves
between compartments through BULKHEAD LEAKS (same law, small areas: closures,
glands, sprung seams). Bulkheads fail when the head difference exceeds their
rating (shoring raises it, speed loads forward bulkheads). Hydrostatics are
closed-form: sinkage from added weight, trim and heel from weight moments, GM
cut by free surface. Damage-control PARTIES pick tasks from a priority list
(close fittings, shore, plug, patch, caulk, rig pumps), travel, work for a
time scaled by skill/darkness/list, and change the leak areas. DC Central
counterfloods wing voids to remove list. Pumps remove water but are tiny next
to a real hole: the core lesson is "accept the holed space, hold the boundary,
pump the seepage".

All [EST] numbers are designer estimates, tuned to the calibration cases at
the bottom. Run:  python damage_control_ref.py
"""
from __future__ import annotations
import math, random
from dataclasses import dataclass, field

G = 9.81
RHO = 1.025          # t/m^3 sea water
CD = 0.6             # discharge coefficient, sharp-edged hole (literature uses ~0.6)
KN = 0.5144

# ----------------------------------------------------------------------------
# Data structures
# ----------------------------------------------------------------------------
@dataclass
class Comp:
    name: str
    sec: int
    kind: str            # 'wingP','wingS','inner','upper'
    x: float             # centre, m from bow
    y: float             # transverse centroid, m (+stbd)
    length: float
    width: float
    z0: float            # floor above keel, m
    z1: float            # ceiling
    perm: float
    water: float = 0.0   # m^3 of water
    counterflood_ok: bool = False
    written_off: bool = False
    pump: float = 0.0    # m^3/s portable pumping rigged here

    @property
    def area(self):      # effective free-surface area (permeability folded in)
        return self.length * self.width * self.perm

    @property
    def cap(self):
        return self.area * (self.z1 - self.z0)

    @property
    def level(self):
        return self.z0 + self.water / self.area

    @property
    def full(self):
        return self.water >= self.cap - 1e-6


@dataclass
class Hole:
    comp: Comp
    area: float          # m^2
    z: float             # centre height above keel
    patched: float = 1.0 # multiplier on area (1 = open)
    claimed: bool = False
    @property
    def eff(self):
        return self.area * self.patched


@dataclass
class Link:              # bulkhead / deck between two compartments
    a: Comp
    b: Comp
    z: float             # height of the leak paths (low on the bulkhead)
    base: float          # m^2 leak with fittings closed (glands, seams)
    open_fit: float = 0.0  # m^2 extra from doors/vents left open
    sprung: float = 0.0  # m^2 extra from blast (seams, rivets)
    rating: float = 8.0  # head difference (m) before collapse
    shores: int = 0
    failed: bool = False
    claimed: set = field(default_factory=set)
    @property
    def eff(self):
        if self.failed:
            return 1.5
        return self.base + self.open_fit + self.sprung
    @property
    def strength(self):
        return self.rating * (1 + 0.6 * min(self.shores, 2))


@dataclass
class Party:
    name: str
    men: int
    sec: int
    busy_until: float = 0.0
    task: object = None


@dataclass
class Ship:
    name: str
    L: float; B: float; T: float; D: float
    disp: float; GM: float; Cw: float = 0.72
    speed_kn: float = 0.0
    comps: list = field(default_factory=list)
    holes: list = field(default_factory=list)
    links: list = field(default_factory=list)
    parties: list = field(default_factory=list)
    dc_skill: float = 1.0            # task speed multiplier (USN 1943+ ~1.0, IJN 1944 ~0.6) [EST]
    counterflood_rate: float = 0.0   # t/min, DC Central (NC: ~80 t/min)
    fixed_drain: float = 0.0         # t/min total fixed drainage (needs power) [EST]
    power: bool = True
    portable_pumps: int = 4
    portable_rate: float = 1.95      # t/min per pump (P-500 class, ~500 gal/min)
    log: list = field(default_factory=list)
    # state
    sink: float = 0.0; trim: float = 0.0; heel: float = 0.0; gm_eff: float = 0.0
    lost: str = ""

    # local outside waterline height above keel at compartment (x,y)
    def wl(self, x, y):
        return (self.T + self.sink + self.trim * (self.L / 2 - x)
                + y * math.tan(self.heel))

    def ram(self, x):
        """Extra head at a hull opening from forward speed [EST shape].
        Near the stem k~0.5 of full stagnation; it fades out by 0.25 L."""
        k = 0.5 * max(0.0, 1 - x / (0.25 * self.L))   # [EST]
        v = self.speed_kn * KN
        return k * v * v / (2 * G)


# ----------------------------------------------------------------------------
# Ship builder: battleship-like hull with TDS wings inside the citadel
# ----------------------------------------------------------------------------
def build_battleship(name="NC-like", nsec=20, citadel=(0.20, 0.80), zebra=True,
                     riveted=True, tightness=1.0):
    """tightness: multiplier on every bulkhead's base leak. 1 = air-tested,
    maintained ship. ~10 = untested/unsealed penetrations (Shinano) [EST]."""
    """~45,000 t fast battleship. Inside the citadel each section is
    wingP | innerP | innerS | wingS below the armour deck (the inner split
    stands in for the fine subdivision of magazines, handling rooms, stores),
    plus one 'upper' space between armour deck and main deck."""
    L, B, T, D = 217.0, 33.0, 10.0, 15.5
    s = Ship(name, L, B, T, D, disp=45000, GM=2.5)
    dz_armour = 11.5          # armour (third) deck, ~1.5 m above design WL
    tds = 5.5                 # TDS depth per side (NC 18.5 ft)
    seclen = L / nsec
    inners = {}
    for i in range(nsec):
        xc = (i + 0.5) * seclen
        f = xc / L
        wfac = min(1.0, 0.35 + 0.65 * min(f, 1 - f) / 0.2)    # fine ends [EST]
        width = B * wfac
        deck = D + 3.0 * max(0, 0.25 - f) / 0.25               # sheer at bow
        z0 = 1.6                                               # top of double bottom
        if citadel[0] <= f <= citadel[1]:
            for kind, sgn in (("wingP", -1), ("wingS", 1)):
                s.comps.append(Comp(f"{kind}{i}", i, kind, xc, sgn * (width / 2 - tds / 2),
                                    seclen, tds, z0, dz_armour, 0.50, counterflood_ok=True))
            # perm 0.5: about half the TDS volume already holds oil/water (liquid layers),
            # so flooding it adds little weight (NC WDR 61: most flooded tanks were full)
            wi = (width - 2 * tds) / 2
            inners[i] = []
            for kind, sgn in (("innerP", -1), ("innerS", 1)):
                c = Comp(f"{kind}{i}", i, kind, xc, sgn * wi / 2, seclen, wi, z0, dz_armour, 0.80)
                s.comps.append(c); inners[i].append(c)
        else:
            c = Comp(f"inner{i}", i, "inner", xc, 0.0, seclen, width, z0, dz_armour, 0.75)
            s.comps.append(c); inners[i] = [c]
        s.comps.append(Comp(f"upper{i}", i, "upper", xc, 0.0, seclen, width,
                            dz_armour, deck, 0.90))
    s.gm_eff = s.GM
    by = {c.name: c for c in s.comps}
    base_bh = (0.0020 if riveted else 0.0008) * tightness   # m^2 per bulkhead (~20 cm^2) [EST]
    base_deck = 0.0015 * tightness
    open_fit = 0.0 if zebra else 0.06           # doors/hatches/vents not closed [EST]
    rate = lambda zt, zl, k=1.3: k * (zt - zl)  # tested to bulkhead-deck head x margin [EST]
    for i in range(nsec):
        # longitudinal: transverse bulkheads
        for k in ("wingP", "wingS", "upper"):
            a, b2 = by.get(f"{k}{i}"), by.get(f"{k}{i+1}")
            if a and b2:
                z = a.z0 + 0.3
                s.links.append(Link(a, b2, z, base_bh, open_fit if k == "upper" else 0.0,
                                    rating=rate(min(a.z1, b2.z1), z)))
        if i + 1 < nsec:
            for a in inners[i]:
                for b2 in inners[i + 1]:
                    if len(inners[i]) == 2 and len(inners[i + 1]) == 2 and a.kind != b2.kind:
                        continue
                    z = a.z0 + 0.3
                    s.links.append(Link(a, b2, z, base_bh, open_fit, rating=rate(a.z1, z)))
        up = by[f"upper{i}"]
        for inn in inners[i]:
            s.links.append(Link(inn, up, inn.z1, base_deck, open_fit / 2, rating=6.0))
        if len(inners[i]) == 2:     # inner P/S boundary: magazine & store boxes, passages
            p_, s_ = inners[i]
            s.links.append(Link(p_, s_, p_.z0 + 0.3, base_bh * 1.5, open_fit, rating=rate(p_.z1, p_.z0)))
            for k, inn in (("wingP", p_), ("wingS", s_)):     # holding bulkhead
                w = by[f"{k}{i}"]
                s.links.append(Link(w, inn, w.z0 + 0.3, base_bh * 0.5, 0.0, rating=rate(w.z1, w.z0, 1.6)))
    return s


def comp(s, name):
    return next(c for c in s.comps if c.name == name)


def link(s, a, b):
    return next(l for l in s.links if {l.a.name, l.b.name} == {a, b})


# ----------------------------------------------------------------------------
# Physics step
# ----------------------------------------------------------------------------
def orifice(area, h_up, h_dn):
    """Signed flow m^3/s from side 'up' to side 'dn'. h_* = head above opening."""
    h_up, h_dn = max(0.0, h_up), max(0.0, h_dn)
    dh = h_up - h_dn
    if area <= 0 or (h_up == 0 and h_dn == 0):
        return 0.0
    return math.copysign(CD * area * math.sqrt(2 * G * abs(dh)), dh)


def pressure_level(s, c, sea_src):
    """Effective water pressure level in c. A full compartment is pressurised
    up to the highest source feeding it (sea or neighbour)."""
    if not c.full:
        return c.level
    return max(c.z1, sea_src.get(c.name, c.z1))


def hydro(s):
    """Sinkage, trim and heel. Free surface is handled as water shifting
    downhill with a lever capped at b/3 (shallow water pools at the side
    instead of acting like a full-depth free surface)."""
    w = m_trim = m_heel = 0.0
    slack = []
    for c in s.comps:
        t = c.water * RHO
        w += t
        m_trim += t * (s.L / 2 - c.x)
        m_heel += t * c.y
        if 0 < c.water < c.cap * 0.98:
            d = max(c.water / c.area, 0.02)
            slack.append((t, c.width, d))
    disp = s.disp + w
    awp = s.Cw * s.L * s.B
    s.sink = w / (RHO * awp)
    GML = 1.0 * s.L                          # [EST] longitudinal GM ~ L
    s.trim = m_trim / (disp * GML)           # rad, + = bow down
    phi = s.heel
    for _ in range(12):                      # fixed point on heel
        tp = math.tan(phi)
        m_fs = sum(t * math.copysign(min(b * b * abs(tp) / (12 * d), b / 3), tp)
                   for t, b, d in slack)
        tgt = math.atan((m_heel + m_fs) / (disp * s.GM))
        phi = 0.5 * phi + 0.5 * tgt
    s.heel = phi
    # report-only: small-angle GM with full free-surface correction
    s.gm_eff = s.GM - sum(t * b * b / (12 * d) for t, b, d in slack) / disp
    return w


def step(s, t, dt):
    # sea feed levels per compartment (for pressurised full spaces)
    sea_src = {}
    for h in s.holes:
        c = h.comp
        sea = s.wl(c.x, c.y) + s.ram(c.x)
        sea_src[c.name] = max(sea_src.get(c.name, 0), sea)
    dvol = {}
    def add(name, v):
        dvol[name] = dvol.get(name, 0.0) + v
    def eff_area(c):            # a full space cannot take more without rising pressure
        return 1e-3 if c.full else c.area
    # holes: clamp so one step can close at most half the head gap (stable at any dt)
    for h in s.holes:
        c = h.comp
        sea = s.wl(c.x, c.y) + s.ram(c.x)
        pin = pressure_level(s, c, sea_src)
        v = orifice(h.eff, sea - h.z, pin - h.z) * dt
        lim = 0.5 * abs(max(sea, h.z) - max(pin, h.z)) * eff_area(c)
        add(c.name, math.copysign(min(abs(v), lim), v))
    # bulkhead links
    for l in s.links:
        if l.eff <= 0:
            continue
        pa, pb = pressure_level(s, l.a, sea_src), pressure_level(s, l.b, sea_src)
        ha, hb = max(0.0, pa - l.z), max(0.0, pb - l.z)
        dh = abs(ha - hb)
        if dh > 0:
            dh += 0.5 * s.ram(min(l.a.x, l.b.x))   # speed loads the bow bulkheads
        if not l.failed and dh > l.strength:
            l.failed = True
            s.log.append((t, f"bulkhead {l.a.name}/{l.b.name} COLLAPSED (dh {dh:.1f} m)"))
        v = orifice(l.eff, ha, hb) * dt
        aa, ab = eff_area(l.a), eff_area(l.b)
        lim = 0.5 * abs(ha - hb) * aa * ab / (aa + ab)
        v = math.copysign(min(abs(v), lim), v)
        add(l.a.name, -v); add(l.b.name, v)
    # pumps: portable in place, fixed drainage spread over dry-side spaces
    for c in s.comps:
        if c.pump and c.water > 0:
            add(c.name, -c.pump / RHO / 60 * dt)
    if s.power and s.fixed_drain > 0:
        tgt = [c for c in s.comps if c.water > 0 and not c.written_off
               and c.kind != "upper" and not c.counterflood_ok]
        for c in tgt:
            add(c.name, -s.fixed_drain / RHO / 60 * dt / len(tgt))
    for c in s.comps:
        c.water = min(c.cap, max(0.0, c.water + dvol.get(c.name, 0.0)))
    return hydro(s), dt


# ----------------------------------------------------------------------------
# Counterflooding (DC Central)
# ----------------------------------------------------------------------------
def counterflood(s, t, dt, state):
    """DC Central floods designated void layers on the high side, one tank at a
    time, each to full (a full tank has no free surface). Capacity runs out when
    every intact wing on that side is full -- then list can only grow."""
    if s.counterflood_rate <= 0:
        return
    deg = math.degrees(s.heel)
    if abs(deg) < 1.0:
        return
    side = "wingP" if deg > 0 else "wingS"      # list to stbd -> flood port
    mid = s.L / 2
    cands = [c for c in s.comps if c.kind == side and c.counterflood_ok
             and c.name not in state.setdefault("cf_done", set())
             and not any(h.comp is c for h in s.holes)]
    if not cands:
        if not state.get("cf_out"):
            state["cf_out"] = True
            s.log.append((t, f"counterflood capacity on {side} exhausted"))
        return
    c = min(cands, key=lambda k: (-k.water, abs(k.x - mid)))   # finish a tank first
    v = s.counterflood_rate / RHO / 60 * dt
    v *= random.uniform(1.0, 1.0 + 1.5 * (1 - min(s.dc_skill, 1)))   # poor crews overshoot [EST]
    v = min(v, CF_FRACTION * c.cap - c.water)
    c.water += v
    if c.water >= CF_FRACTION * c.cap - 1e-6:
        state["cf_done"].add(c.name)       # valves shut; tank done
    state["cf"] = state.get("cf", 0) + v * RHO


CF_FRACTION = 1.0


# ----------------------------------------------------------------------------
# Damage-control parties: task generation and execution
# ----------------------------------------------------------------------------
# base minutes for a trained crew, men needed  [EST, anchored in doc §4]
TASKS = {
    "close":  (1.5, 2),    # shut/dog a door, hatch or vent closure
    "plug":   (3.0, 2),    # wooden plugs + wedges, hole <= ~150 cm^2
    "patch":  (18.0, 5),   # box/hinged patch + strongback + shore, <= ~0.3 m^2
    "caulk":  (10.0, 3),   # wedges/oakum/rags in sprung seams, glands, vents
    "shore":  (15.0, 4),   # one K/H shore set on a loaded bulkhead
    "pump":   (8.0, 3),    # rig portable pump, suction, discharge hose
}


def can_work_in(c):
    return (not c.full) and c.water / c.area < 1.2      # <1.2 m of water: workable


def gen_tasks(s):
    tasks = []
    for h in s.holes:
        c = h.comp
        if h.claimed or h.patched < 1.0 or c.written_off:
            continue
        reach = can_work_in(c) and c.level < h.z + 0.6   # hole reachable
        if not reach:
            continue
        if h.area <= 0.015:
            tasks.append((2, "plug", h, c.sec))
        elif h.area <= 0.3:
            tasks.append((2, "patch", h, c.sec))
    for l in s.links:
        if l.failed:
            continue
        wet, dry = (l.a, l.b) if l.a.level >= l.b.level else (l.b, l.a)
        if wet.water <= 0 or not can_work_in(dry):
            continue
        if l.open_fit > 0 and "close" not in l.claimed:
            tasks.append((0, "close", l, dry.sec))
        head = wet.level - dry.level
        if head > 0.45 * l.strength and l.shores < 2 and "shore" not in l.claimed:
            tasks.append((1, "shore", l, dry.sec))
        if l.sprung > 0 and "caulk" not in l.claimed:
            tasks.append((3, "caulk", l, dry.sec))
    used = sum(1 for c in s.comps if c.pump)
    if used < s.portable_pumps:
        for c in s.comps:
            if c.water > 5 and not c.pump and not c.written_off and can_work_in(c) \
                    and not c.counterflood_ok and "pump" not in getattr(c, "_cl", set()):
                tasks.append((4, "pump", c, c.sec))
    tasks.sort(key=lambda x: x[0])
    return tasks


def task_time(s, kind, party):
    base, men = TASKS[kind]
    t = base / s.dc_skill
    t *= max(1.0, men / max(party.men / 3, 1))        # small party: slower
    if not s.power:
        t *= 1.6                                       # darkness, no ventilation
    t *= 1 + abs(math.degrees(s.heel)) / 10            # list makes everything slower
    return t


def finish(s, party, t):
    kind, obj = party.task
    if kind == "plug":
        obj.patched = 0.10
    elif kind == "patch":
        obj.patched = 0.08
    elif kind == "close":
        obj.open_fit = 0.0
    elif kind == "caulk":
        obj.sprung *= 0.2
    elif kind == "shore":
        obj.shores += 1
        obj.claimed.discard("shore")
    elif kind == "pump":
        obj.pump = s.portable_rate
    party.task = None


def run_dc(s, t, state):
    for p in s.parties:
        if p.task and t >= p.busy_until:
            finish(s, p, t)
    free = [p for p in s.parties if p.task is None and p.men > 0]
    if not free:
        return
    for pri, kind, obj, sec in gen_tasks(s):
        if not free:
            break
        p = min(free, key=lambda q: abs(q.sec - sec))
        travel = 0.6 * abs(p.sec - sec)                 # min per section [EST]
        dur = travel + task_time(s, kind, p)
        p.task, p.busy_until, p.sec = (kind, obj), t + dur * 60, sec
        if kind in ("plug", "patch"):
            obj.claimed = True
        elif kind == "pump":
            obj._cl = {"pump"}
        else:
            obj.claimed.add(kind)
        free.remove(p)


# ----------------------------------------------------------------------------
# Simulation driver
# ----------------------------------------------------------------------------
def simulate(s, hours=6.0, dt=1.0, slow_at=None, slow_to=None, verbose=False):
    t, state, trace = 0.0, {}, []
    while t < hours * 3600:
        if slow_at is not None and t >= slow_at * 60:
            s.speed_kn = slow_to
        w, h = step(s, t, dt)
        counterflood(s, t, h, state)
        if int(t // 30) != int((t - h) // 30) or t == 0:
            run_dc(s, t, state)
        # loss checks
        deg = math.degrees(s.heel)
        immersed = sum(1 for c in s.comps if c.kind == "upper"
                       and s.wl(c.x, 0) > c.z1) / max(1, sum(1 for c in s.comps if c.kind == "upper"))
        if abs(deg) > 22:
            s.lost = f"capsized at {t/60:.0f} min"
        elif immersed > 0.3:
            s.lost = f"foundered at {t/60:.0f} min"
        if not trace or t / 60 - trace[-1][0] >= 1.0:
            trace.append((t / 60, w, deg, math.degrees(s.trim) , s.gm_eff, state.get("cf", 0)))
        if s.lost:
            break
        t += h
    return trace


def summary(s, trace, label):
    def at(m):
        r = min(trace, key=lambda r: abs(r[0] - m))
        return f"{r[1]:6.0f} t {r[2]:+5.1f}°"
    last = trace[-1]
    out = f"{label:<46} 10min {at(10)} | 1h {at(60)} | end({last[0]:4.0f}min) {last[1]:6.0f} t heel {last[2]:+5.1f}° cf {last[5]:5.0f} t"
    if s.lost:
        out += f"  ** LOST: {s.lost}"
    return out


def std_parties(s, n=5, men=25):
    secs = [int(len(s.comps) / 4 * k / (n - 1)) if n > 1 else 10 for k in range(n)]
    s.parties = [Party(f"Repair {i+1}", men, min(19, max(0, sc))) for i, sc in enumerate(secs)]


# ----------------------------------------------------------------------------
# Scenarios / calibration
# ----------------------------------------------------------------------------
def scen_torpedo_nc(dc=True):
    """North Carolina 15 Sep 1942: torpedo abreast turret I, port.
    Target: ~970 t flooding (+ ~480 t counterflood), 5.5° list removed in ~6 min,
    progressive flooding minor."""
    s = build_battleship()
    s.speed_kn = 19
    s.counterflood_rate = 80 if dc else 0
    s.fixed_drain = 10
    if dc: std_parties(s)
    sec = 4                                    # x ~0.22 L, end of TDS taper
    w = comp(s, f"wingP{sec}")
    s.holes.append(Hole(w, 50.0, 5.0))         # 32x18 ft hole -> ~50 m2
    s.holes.append(Hole(comp(s, f"wingP{sec+1}"), 6.0, 5.0))   # damage ran ~frames 37-58
    # holding bulkhead seams opened (NC: rivets failed) -> inner flooding
    hb = link(s, f"wingP{sec}", f"innerP{sec}")       # holding bulkhead seams opened
    hb.sprung, hb.rating = 0.25, hb.rating * 0.8
    link(s, f"innerP{sec}", f"innerP{sec+1}").sprung = 0.002
    link(s, f"innerP{sec}", f"upper{sec}").sprung = 0.002
    return s


def scen_bow_shell(speed, slow_after=None, slow_to=None):
    """Bismarck/Seydlitz-type: heavy shell through unarmoured bow near WL.
    Speed adds ram head; slowing cuts inflow and bulkhead load."""
    s = build_battleship()
    s.speed_kn = speed
    s.counterflood_rate = 40
    std_parties(s)
    c = comp(s, "upper1")
    s.holes.append(Hole(c, 1.2, c.z0 + 0.2))           # ~1.2 m^2 tear (in and out)
    s.holes.append(Hole(comp(s, "inner1"), 0.4, 9.6))  # below-WL exit damage
    link(s, "inner1", "inner2").sprung = 0.01
    return s, slow_after, slow_to


def scen_splinters(dc=True, zebra=True, power=True):
    """'Small leaks': 30 splinter/near-miss holes 3-12 cm along the starboard
    side at and just below the WL, plus sprung seams. No single big hole."""
    random.seed(4)
    s = build_battleship(zebra=zebra)
    s.speed_kn = 18
    s.power = power
    s.fixed_drain = 10 if power else 0
    s.counterflood_rate = 60 if dc else 0
    if dc: std_parties(s)
    else: s.portable_pumps = 0
    for _ in range(30):
        sec = random.randint(2, 17)
        cands = [c for c in s.comps if c.sec == sec and c.kind in ("wingS", "inner", "upper")]
        d = random.uniform(0.03, 0.12)
        z = random.uniform(s.T - 1.0, s.T + 2.5)
        tgt = next((k for k in cands if k.z0 <= z <= k.z1), cands[0])
        s.holes.append(Hole(tgt, math.pi * d * d / 4, z))
    for l in s.links:
        if l.a.kind in ("wingS", "inner", "innerS", "upper") and random.random() < 0.25:
            l.sprung = random.uniform(0.001, 0.006)
    return s


def scen_shinano_like(dc_skill, zebra, parties, cf, tightness):
    """Four torpedoes one side (stbd), mid/aft. Shinano: doors open, no air test,
    weak DC. Compare with a drilled crew in the same hull."""
    random.seed(7)
    s = build_battleship(zebra=zebra, tightness=tightness)
    s.speed_kn = 18
    s.dc_skill = dc_skill
    s.counterflood_rate = cf
    s.fixed_drain = 12
    if parties: std_parties(s)
    for sec in (8, 11, 13, 15):
        w = comp(s, f"wingS{sec}")
        s.holes.append(Hole(w, 40.0, 5.0))
        hb = link(s, f"wingS{sec}", f"innerS{sec}")
        hb.sprung, hb.rating = (0.03 if zebra else 0.06), hb.rating * 0.8
    return s


if __name__ == "__main__":
    print("\n=== Hole inflow (t/h), Cd 0.6, head below outside WL ===")
    print(f"{'hole':<22}" + "".join(f"{h:>9} m" for h in (0.5, 2, 5, 8)))
    for name, a in [("splinter 5 cm", math.pi * .05**2 / 4), ("splinter 10 cm", math.pi * .1**2 / 4),
                    ("shell 30 cm", math.pi * .3**2 / 4), ("shell 50 cm", math.pi * .5**2 / 4),
                    ("tear 1 m2", 1.0), ("torpedo 50 m2", 50.0)]:
        print(f"{name:<22}" + "".join(f"{CD*a*math.sqrt(2*G*h)*3600*RHO:>11,.0f}" for h in (0.5, 2, 5, 8)))
    print("ram head at stem (k=0.5): " + ", ".join(
        f"{v} kn {0.5*(v*KN)**2/(2*G):.1f} m" for v in (12, 20, 28)))
    print(f"pumps: P-500 ~{500*3.785/1000*60*RHO:.0f} t/h, handy-billy P-60 ~{60*3.785/1000*60*RHO:.0f} t/h")

    print("\n=== Calibration runs ===")
    for dc in (True, False):
        s = scen_torpedo_nc(dc); tr = simulate(s, hours=3)
        print(summary(s, tr, f"NC torpedo, DC={'drilled' if dc else 'none'}"))
        if dc:
            t6 = next((r for r in tr if abs(r[2]) < 1.0 and r[0] > 1), None)
            print(f"   list < 1° at {t6[0] if t6 else '-'} min; peak list {max(abs(r[2]) for r in tr):.1f}°")
    for sp, sa, st, lab in [(28, None, None, "bow shell, holds 28 kn"),
                            (28, 4, 12, "bow shell, slows to 12 kn at 4 min"),
                            (12, None, None, "bow shell, 12 kn throughout")]:
        s, a, b = scen_bow_shell(sp, sa, st)
        tr = simulate(s, hours=6, slow_at=a, slow_to=b)
        print(summary(s, tr, lab))
        for e in s.log[:3]:
            print(f"   {e[0]/60:5.0f} min  {e[1]}")
    for dc, zb, pw, lab in [(True, True, True, "splinters: drilled DC, Zebra, power"),
                            (False, True, True, "splinters: no DC, Zebra"),
                            (True, True, False, "splinters: drilled DC, NO power")]:
        s = scen_splinters(dc, zb, pw); tr = simulate(s, hours=8)
        print(summary(s, tr, lab))
        by = {}
        for c in s.comps:
            by[c.kind] = by.get(c.kind, 0) + c.water * RHO
        print("   water by space: " + ", ".join(f"{k} {v:.0f} t" for k, v in by.items() if v > 1))
    for sk, zb, pa, cf, ti, lab in [(0.5, False, True, 20, 25, "4 torps: Shinano-like (untested, open, weak DC)"),
                                    (0.5, True, True, 20, 1, "4 torps: tight hull + Zebra, weak DC"),
                                    (1.0, True, True, 80, 1, "4 torps: tight hull + Zebra, drilled DC")]:
        s = scen_shinano_like(sk, zb, pa, cf, ti); tr = simulate(s, hours=10)
        print(summary(s, tr, lab))
        for e in s.log[:4]:
            print(f"   {e[0]/60:5.0f} min  {e[1]}")
