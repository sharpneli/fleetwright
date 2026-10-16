"""fire_ref.py -- reference for 17_fire.md (Fleetwright damage research).

What happens after a fire starts: how fast it grows, when a space flashes over, how long it burns, how it
spreads (open paths, hot steel, the open deck), how much fuel each era carried, and how fast crews of
each era put it out, with the water that stays aboard.

    python fire_ref.py              # every table the note quotes (about 2 min)
    python fire_ref.py physics      # just the single-compartment tables (instant)

Pure Python, no dependencies. Units: s, m, m^2, MW, MJ, L/min. Tags: [SRC] sourced, [DERIVED] computed
from sourced numbers, [FIT] set here to the record, [EST] judgement.

One burning space:
    growth      Q = Q0 + a*t^2 (t-squared fire; a by era and use)                        [SRC classes]
    caps        Q_fuel = A * r(q)   r = 0.25 MW/m2 * clamp((q-230)/420, 0.01, 1)            [SRC 250 kW/m2; FIT ramp]
                Q_vent = 1.5 * sum(A_o*sqrt(H_o))  (doors, holes); open deck: no cap       [SRC Kawagoe]
                Q_fo   = 0.65 * (0.0078*A_T + 0.378*A_o*sqrt(H_o))                         [SRC Thomas; FIT to NBS 0.67 MW]
    phases      grows -> flashover (Q reaches Q_fo) -> developed at min(Q_vent, Q_fuel) -> decay at 20% fuel -> out
                or 'local' (fuel cap under Q_fo: burns ~30% of the load near the seat, then dies)
                or 'starved' (vent cap under Q_fo: smoulders; a new hole or opened door lets it flash)
    water       control needs F_req = 4 L/min per m2 involved (fog); a jet counts a third        [SRC Grimwood/Iowa; SRC jet ~10%]
                F >= F_req: knock-down, tau = 90 s * F_req/F; then mop-up 60 L/m2 involved       [FIT Shadwell; SRC Sardqvist]
                F <  F_req: the fire is held at (1 - F/F_req) of its strength, not beaten
                evaporated <= min(F*e, Q/2.6 MJ/kg) (e: fog 0.8, jet 0.15); the rest is water aboard
    spread      open path (door, hole, hoist): hazard 1/2 min from a developed fire (x2 flames out of the openings)
                hot steel: up after 8 min then 1/15 min; sideways after 12 min then 1/40 min; down after 40 min
                then 1/180 min; x(neighbour load/400), x0.3 under boundary cooling              [FIT Shadwell Ph V/VI, USCG A-0]
                open deck: 1/6 min x(load/300), x0.3 decks wetted
"""
import math
import random
import statistics as st
import sys
from dataclasses import dataclass, field, replace

# =============================================================================================================
# 1. Fire load by era and space (MJ per m2 of deck)
# =============================================================================================================
ERAS = ["1890", "1905", "1914", "1939", "1943"]
ERA_NAME = {
    "1890": "1880-1905, unstripped (Yalu, Santiago)",
    "1905": "1904-05, stripped pre-dreadnought (Tsushima)",
    "1914": "1914-18, stripped at mobilisation",
    "1939": "1919-42, peacetime outfit (Savo)",
    "1943": "1943-45, stripped (USN after Savo)",
}
FIRE_LOAD = {      # 1890  1905  1914  1939  1943      [EST] unless the note says otherwise; see 17 sec. 2
    "cabins":     (900,  600,  450,  650,  280),   # officers' cabins, wardroom: panelling, furniture, kit
    "berthing":   (450,  380,  400,  380,  240),   # messdecks: hammocks/bunks, kit, tables (1943 = NBS 1979 [SRC])
    "office":     (420,  420,  420,  500,  300),   # offices, plot, radio, chart (Eurocode office 420 [SRC])
    "stores":     (1500, 1500, 1500, 1500, 1200),  # clothing, provisions, bosun's (library 1500 [SRC] analogue)
    "paintstore": (3000, 2500, 1500, 3000, 1000),
    "machinery":  (150,  150,  150,  150,  120),   # lagging, oil film, rags; a fuel spray is another thing
    "passage":    (120,  80,   80,   150,  30),    # varnished wood partitions (1890), linoleum (1939)
    "weather":    (400,  350,  150,  250,  120),   # what burns on an open deck: boats, hammocks, gear, the top of the planking
    "hangar":     (0,    0,    0,    300,  150),   # without the aircraft (they are added as fuel)
    "magazine":   (0,    0,    0,    0,    0),     # magazines are 07's and 10's
}
PAINT_ADD = {"1890": 30, "1905": 60, "1914": 50, "1939": 100, "1943": 10}   # MJ/m2 of deck [DERIVED 17 sec. 2.3]
ALPHA_ERA = {"1890": 2.0, "1905": 1.5, "1914": 1.0, "1939": 1.3, "1943": 0.7}   # x 'medium' [EST, sec. 3.1]
ALPHA_MED = 0.01172e-3          # MW/s2, t-squared 'medium' (1 MW in 300 s) [SRC]
ALPHA_USE = {"stores": 2.0, "paintstore": 4.0, "hangar": 4.0}               # 'fast' and 'ultra-fast' uses
OPEN_SUSTAIN = 300               # MJ/m2: an open deck below this burns its items and goes out [EST]
AVGAS_GJ = 14.4                 # one fuelled floatplane, 120 gal [DERIVED: 454 L x 0.72 x 44 MJ/kg]


def load(use, era):
    q = FIRE_LOAD[use][ERAS.index(era)]
    if use not in ("weather", "magazine"):
        q += PAINT_ADD[era]
    return q


# =============================================================================================================
# 2. One compartment: the physics
# =============================================================================================================
DOOR_AOH = 0.66 * 1.68 * math.sqrt(1.68)     # one open WT door, A*sqrt(H) = 1.44 m^2.5
LEAK_AOH = 0.05                               # closed up: leakage, vents [EST]


def r_fuel(q):
    """Fuel-controlled HRR per m2 of deck (MW/m2): 250 kW/m2 for a furnished room [SRC Eurocode E.5],
    ramped down for thin loads: under ~230 MJ/m2 a space can't flash over, 230-450 is marginal
    (DTIC ADA274791 bands [SRC]; the NBS berthing at 240 MJ/m2 did not flash over without carpet) [FIT]."""
    return 0.25 * min(1.0, max(0.01, (q - 230) / 420))


def hole_aoh(area_m2):
    """A square hole in a bulkhead or the side: A*sqrt(H) with H = sqrt(A)."""
    return area_m2 ** 1.25


def q_vent(aoh):
    return 1.5 * aoh                             # MW [SRC Kawagoe ~1.5 A sqrt(H); Eurocode 1.4]


def q_flashover(area, height, aoh):
    side = math.sqrt(area)
    a_t = 2 * area + 4 * side * height
    return 0.65 * (0.0078 * a_t + 0.378 * aoh)  # MW [SRC Thomas; FIT x0.65 to NBS berthing 0.67 MW]


def f_req(area_involved):
    return max(40.0, 4.0 * area_involved)       # L/min, fog-equivalent [SRC Grimwood 4 L/min/m2; Iowa]


NOZZLE_EFF = {"jet": 1 / 3, "allpurpose": 0.5, "fog": 1.0}      # fog-equivalent share of the flow [SRC jet 10% vs fog; EST AP]
NOZZLE_EVAP = {"jet": 0.15, "allpurpose": 0.4, "fog": 0.8}      # most a nozzle's water can boil off [EST]


# =============================================================================================================
# 3. Spaces, fires, ships
# =============================================================================================================
@dataclass
class Space:
    name: str
    use: str
    sec: int
    z: int                 # 1 lower deck, 2 main/battery deck, 3 open (weather) deck, 4 superstructure
    area: float
    height: float = 2.6
    era: str = "1939"
    open_air: bool = False
    extra_mj: float = 0.0  # aircraft, gasoline
    doors_open: int = 0
    holes: float = 0.0     # m2 of shell holes (vertical)
    q: float = 0.0
    # fire state
    Q: float = 0.0
    E: float = 0.0
    E0: float = 0.0
    phase: str = ""        # '', grow, local, starved, dev, decay, ctrl
    t_ign: float = 0.0
    q0: float = 0.0
    dev_s: float = 0.0
    starved_s: float = 0.0
    local_s: float = 0.0
    ainv_max: float = 0.0
    mop: float = 0.0
    out_at: float = -1.0
    hot_out: bool = False
    flashed: bool = False
    local_hose: float = 0.0
    local_cut: bool = False
    nbrs: list = field(default_factory=list)   # (space, kind) kind: up, down, side, deck

    def setup(self):
        self.q = load(self.use, self.era)
        return self

    @property
    def burning(self):
        return self.phase not in ("", "out")

    def aoh(self):
        if self.open_air:
            return 1e9
        return LEAK_AOH + self.doors_open * DOOR_AOH + hole_aoh(self.holes)

    def qcap_fuel(self):
        return self.area * r_fuel(self.q) + (2.0 if self.extra_mj else 0.0)

    def qfo(self):
        return 1e9 if self.open_air else q_flashover(self.area, self.height, self.aoh())

    def alpha(self):
        a = ALPHA_MED * ALPHA_ERA[self.era] * ALPHA_USE.get(self.use, 1.0)
        return a * (4.0 if self.extra_mj else 1.0)

    def ignite(self, t, q0):
        if self.burning or self.q + self.extra_mj / self.area < 60:
            return False
        if self.E0 <= 0:
            self.E0 = self.q * self.area + self.extra_mj
            self.E = self.E0
        if self.E <= 0.05 * self.E0:
            return False                       # burnt out already
        self.phase, self.t_ign, self.q0, self.Q = "grow", t, q0, q0
        self.dev_s = self.starved_s = self.local_s = self.mop = 0.0
        return True

    def involved(self):
        if self.phase in ("dev", "decay"):
            return self.area
        if self.open_air:
            return min(self.area, self.Q / 0.1)    # burning items on an open deck, ~0.1 MW per m2 [EST]
        return self.area * min(1.0, max(0.1, self.Q / self.qfo()))


@dataclass
class Doctrine:
    name: str
    first_hose: float | None      # min from ignition to a local hose (hoses laid out, fire watch); None: none
    muster: float                 # min for a party to muster and lead out hose the first time
    flow: float                   # L/min per hose at full pressure
    nozzle: str                   # jet | allpurpose | fog
    crew_per_hose: int
    p_main: float                 # chance a hit in a zone cuts the fire main there
    p_cut: float                  # chance a hit cuts each hose working in its section
    spares: int                   # spare hose lengths
    looped: bool
    t_isolate: float | None       # min to isolate a break (restores the zones beyond); None: never
    ba: bool                      # breathing sets enough to attack smoke-filled spaces
    cooling: bool                 # boundary cooling as part of the attack
    reflash_watch: bool
    wet_decks: bool
    doors: int                    # open doors per space in action (0 = closed up)
    turn_to: int                  # extra parties after the action
    handy: bool                   # portable pumps for zones with no main
    jumper: float | None = None   # min to rig a jumper hose into a cut section (half pressure)
    fans_secured: bool = False    # supply ventilation secured: smoke stays out of the machinery


DOCTRINES = {
    # [EST] built on 17 sec. 5; the sources are named there
    "disorganised_1890": Doctrine("Chinese 1894 / Spanish 1898", None, 6, 600, "jet", 6, 0.25, 0.30, 4,
                                  False, None, False, False, False, False, 1, 1, False, None),
    "prepared_1894": Doctrine("Japanese 1894 / Zhenyuan (hoses laid out)", 2.0, 3, 600, "jet", 6, 0.20, 0.30, 8,
                              False, 15, False, False, False, True, 1, 2, False, None),
    "tsushima_1905": Doctrine("Russian 1905", 1.5, 3, 600, "jet", 6, 0.20, 0.45, 10,
                              False, 15, False, False, False, True, 1, 2, False, None),
    "rn_1916": Doctrine("RN 1916", 3.0, 4, 420, "jet", 5, 0.10, 0.15, 10,
                        False, 15, False, False, False, True, 0, 2, True, 15),
    "usn_1942": Doctrine("USN 1942 (Savo fit)", 2.0, 3, 250, "allpurpose", 4, 0.25, 0.20, 10,
                         False, 8, False, False, False, False, 0, 2, True, 10),
    "usn_1944": Doctrine("USN 1944 (post-Savo)", 1.0, 2, 360, "fog", 4, 0.15, 0.20, 20,
                         True, 4, True, True, True, False, 0, 3, True, 6, True),
}

SHIPS = {   # sections, compartment m2, superstructure m2, open deck m2 per section, parties x men, pumps L/min,
            # x on the doctrine's fire-main break chance (more risers and more ship between them on a big one) [EST]
    "destroyer":  dict(n=6, a=45, sup=25, deck=60, parties=(3, 10), pumps=7500, main=1.0),
    "cruiser":    dict(n=10, a=70, sup=40, deck=150, parties=(4, 16), pumps=8000, main=0.5),
    "battleship": dict(n=14, a=90, sup=60, deck=220, parties=(6, 20), pumps=15000, main=0.35),
}


def build_ship(kind, era, doc, aircraft=0):
    s = SHIPS[kind]
    n = s["n"]
    sp = []
    for i in range(n):
        f = i / (n - 1)
        # lower deck
        if f < 0.12:
            use = "stores"
        elif f < 0.25 or 0.72 < f < 0.85:
            use = "magazine"
        elif f < 0.72:
            use = "machinery"
        else:
            use = "stores"
        sp.append(Space(f"L{i}", use, i, 1, s["a"], era=era))
        # main / battery deck
        if i == 0:
            use = "paintstore"
        elif f < 0.55:
            use = "berthing"
        elif f < 0.62:
            use = "office"
        elif f < 0.95:
            use = "cabins"
        else:
            use = "stores"
        sp.append(Space(f"M{i}", use, i, 2, s["a"], era=era, doors_open=doc.doors))
        # open deck
        sp.append(Space(f"W{i}", "weather", i, 3, s["deck"], era=era, open_air=True))
        # superstructure: forward and aft
        if 0.15 <= f <= 0.40:
            sp.append(Space(f"S{i}", "office" if f < 0.3 else "cabins", i, 4, s["sup"], era=era, doors_open=1))
        elif 0.60 <= f <= 0.75 and era in ("1939", "1943") and kind != "destroyer":
            sp.append(Space(f"H{i}", "hangar", i, 4, s["sup"] * 1.5, era=era, doors_open=2,
                            extra_mj=AVGAS_GJ * 1000 * aircraft / 2))
    for x in sp:
        x.setup()
    by = {(x.sec, x.z): x for x in sp}
    for x in sp:
        for dz, kind_ in ((1, "up"), (-1, "down")):
            y = by.get((x.sec, x.z + dz))
            if y:
                x.nbrs.append((y, kind_))
        for ds in (-1, 1):
            y = by.get((x.sec + ds, x.z))
            if y:
                x.nbrs.append((y, "deck" if x.open_air else "side"))
    return sp, s


# =============================================================================================================
# 4. The ship simulation
# =============================================================================================================
@dataclass
class Party:
    sec: float
    men: int
    skill: float = 1.0
    target: Space | None = None
    arrive: float = 0.0
    mustered: bool = False
    hoses_cut: int = 0
    cut_until: float = 0.0


SHELL_S = {"HE": 0.68, "AP": 0.20, "BP": 0.90}   # fire 'potency' [FIT to 08 sec. 3.9: P(fire) = 1 - exp(-s q/400)]
SHELL_Q0 = {"HE": 0.3, "AP": 0.1, "BP": 0.4}    # MW at the seat [EST]
SHELL_HOLE = {"HE": 1.0, "AP": 0.3, "BP": 0.6}  # m2 of vent the burst opens [EST]
Z_WEIGHT_HE = {1: 0.3, 2: 2.0, 3: 3.0, 4: 2.0}  # where hits burst [EST, 08 sec. 6]
Z_WEIGHT_AP = {1: 1.5, 2: 2.0, 3: 1.0, 4: 1.5}


def p_fire(kind, q):
    return 1 - math.exp(-SHELL_S[kind] * q / 400)


class Sim:
    def __init__(self, kind, era, docname, hits, rng, aircraft=0, dt=10.0, horizon_h=4.0):
        self.doc = DOCTRINES[docname] if isinstance(docname, str) else docname
        self.sp, self.ship = build_ship(kind, era, self.doc, aircraft)
        self.n = self.ship["n"]
        self.rng, self.dt, self.T = rng, dt, horizon_h * 3600
        self.hits = sorted(hits)
        np, men = self.ship["parties"]
        self.parties = [Party(sec=(i + 0.5) * self.n / np, men=men) for i in range(np)]
        self.zone_dead = [False] * 4
        self.breaks = []         # (zone, t)
        self.spares = self.doc.spares
        self.fires = 0
        self.flashovers = 0
        self.water_t = 0.0
        self.t_ablaze = None
        self.mag_exp = 0.0       # minutes of developed fire next to a magazine (summed)
        self.peak_dev = 0
        self.last_hit = self.hits[-1][0] * 60 if self.hits else 0
        self.turned_to = False
        self.reflashes = 0
        self.t_now = 0.0
        self.lit = set()
        self.power_lost = False
        self.t_power = None
        self.abandoned = set()

    # --- fire main -------------------------------------------------------------------------------------------
    def zone(self, sec):
        return min(3, int(4 * sec / self.n))

    def pressure(self, sec):
        z = self.zone(sec)
        if self.power_lost:                             # main fire pumps stopped: only a diesel pump remains
            return 0.6 if (self.doc.looped and not self.zone_dead[z]) else 0.0
        if not self.zone_dead[z]:
            return 1.0
        if self.doc.jumper is not None and any(b[0] == z and self.t_now - b[1] >= self.doc.jumper * 60
                                               for b in self.breaks):
            return 0.5                                  # jumper hose from the next section
        return 0.0

    def break_main(self, z, t):
        self.breaks.append([z, t, False])
        self._apply_breaks(t)

    def _apply_breaks(self, t):
        dead = [False] * 4
        for z, tb, iso in self.breaks:
            dead[z] = True
            if not self.doc.looped and not iso:
                if z == 1:
                    dead[0] = True
                elif z == 2:
                    dead[3] = True
        self.zone_dead = dead

    # --- hits ------------------------------------------------------------------------------------------------
    def hit(self, t, kind):
        w = Z_WEIGHT_HE if kind in ("HE", "BP") else Z_WEIGHT_AP
        cand = [x for x in self.sp if x.use != "magazine"]
        x = self.rng.choices(cand, [w[c.z] * (c.area ** 0.5) for c in cand])[0]
        if not x.open_air:
            x.holes += SHELL_HOLE.get(kind, 2.0) * self.rng.uniform(0.5, 1.5)
        # fire
        if kind == "GAS":
            x.extra_mj += 1500 * 45 * self.rng.uniform(0.5, 1.0)   # 0.75-1.5 t of gasoline [EST]
            self.fires += 1
            x.E0 = x.E = 0
            # the aircraft's fuel sprays the space and runs to the ones around it: flashover in a minute or two
            # (Darwin 1988 [SRC]); each gets a share of the gasoline
            lit = [x] + [y for y, k in x.nbrs]
            for y in lit:
                if y is not x:
                    y.extra_mj += x.extra_mj * 0.3
                    y.E0 = y.E = 0
                y.local_cut = True                           # the crash kills or scatters the men on the spot
                if y.ignite(t, 3.0):
                    self.lit.add(y.name)
                    if not y.open_air:
                        y.phase, y.flashed = "dev", True
                        self.flashovers += 1
        elif self.rng.random() < p_fire(kind, x.q):
            if x.ignite(t, SHELL_Q0[kind]):
                self.fires += 1             # fires started by hits
                self.lit.add(x.name)
            elif x.burning:
                x.Q += SHELL_Q0[kind]
        # fire main
        if self.rng.random() < self.doc.p_main * self.ship["main"] * (1.5 if kind == "GAS" else 1.0):
            self.break_main(self.zone(x.sec), t)
        # hoses and men in the section
        for p in self.parties:
            if p.target is not None and abs(p.target.sec - x.sec) <= 0.5 and t >= p.arrive:
                for _ in range(3):
                    if self.rng.random() < self.doc.p_cut:
                        p.hoses_cut += 1
                if self.rng.random() < 0.3:
                    p.men = int(p.men * 0.6)
        if x.local_hose and self.rng.random() < self.doc.p_cut * 2:
            x.local_cut = True

    # --- firefighting ----------------------------------------------------------------------------------------
    def plan(self, t):
        if not self.turned_to and t > self.last_hit + 15 * 60:
            self.turned_to = True
            for i in range(self.doc.turn_to):
                self.parties.append(Party(sec=self.n / 2, men=12, skill=0.7))
        burning = [x for x in self.sp if x.burning and x.phase != "ctrl"]
        for p in self.parties:
            if p.target is not None and not p.target.burning:
                p.target = None
        need = []
        for x in burning:
            have = sum(1 for p in self.parties if p.target is x)
            if have >= 2:
                continue
            mag = any(y.use == "magazine" for y, _ in x.nbrs)
            pri = x.Q * (3 if mag else 1) + (0.5 if x.phase in ("grow",) else 0)
            need.append((pri, x))
        need.sort(key=lambda a: -a[0])
        free = [p for p in self.parties if p.target is None and p.men >= 2]
        for _, x in need:
            if not free:
                break
            p = min(free, key=lambda q: abs(q.sec - x.sec))
            free.remove(p)
            travel = 30 * abs(p.sec - x.sec) + 30 * abs(x.z - 2)
            p.arrive = t + travel + (0 if p.mustered else self.doc.muster * 60)
            p.mustered = True
            p.target, p.sec = x, x.sec

    def flows(self, t):
        """fog-equivalent L/min on each space, and the raw L/min."""
        eff, raw, noz = {}, {}, {}
        demand = []
        for p in self.parties:
            x = p.target
            if x is None or t < p.arrive:
                continue
            if p.hoses_cut and t >= p.cut_until and self.spares > 0:
                self.spares -= 1
                p.hoses_cut -= 1
                p.cut_until = t + 120
            nh = max(0, min(3, p.men // self.doc.crew_per_hose) - p.hoses_cut)
            pr = self.pressure(x.sec)
            if pr > 0:
                f, kind = nh * self.doc.flow * math.sqrt(pr), self.doc.nozzle
            elif self.doc.handy and t > p.arrive + 8 * 60:
                f, kind = min(nh, 1) * 190, "jet"            # handy-billy, 50 gpm
            else:
                f, kind = (60 if p.men >= 4 else 0), "jet"    # buckets, CO2
            acc = 1.0
            if not x.open_air and x.phase in ("dev", "decay") and not self.doc.ba:
                acc = 0.5                                    # from the doorway, in smoke
            demand.append((x, f, kind, acc * 0.8 * p.skill))
        for x in self.sp:
            if x.burning and x.local_hose and not x.local_cut and self.pressure(x.sec) > 0:
                demand.append((x, self.doc.flow, self.doc.nozzle, 1.0))
        tot = sum(f for x, f, k, a in demand if self.pressure(x.sec) > 0 and k == self.doc.nozzle)
        cap = (0 if self.power_lost else self.ship["pumps"]) + (3800 if self.doc.looped else 0)
        scale = min(1.0, cap / tot) if tot > 0 else 1.0
        for x, f, kind, a in demand:
            if kind == self.doc.nozzle and self.pressure(x.sec) > 0:
                f *= scale
            eff[x.name] = eff.get(x.name, 0) + f * NOZZLE_EFF[kind] * a
            raw[x.name] = raw.get(x.name, 0) + f
            noz[x.name] = kind
        return eff, raw, noz

    # --- one step --------------------------------------------------------------------------------------------
    def step(self, t):
        dt, rng = self.dt, self.rng
        eff, raw, noz = self.flows(t)
        for x in self.sp:
            if not x.burning:
                if x.hot_out and x.out_at > 0 and t - x.out_at < 1800:
                    lam = (1 / 1200) * (0.1 if self.doc.reflash_watch else 1.0)
                    if rng.random() < lam * dt and x.ignite(t, 0.2):
                        self.reflashes += 1
                continue
            # local first hose
            if x.local_hose == 0 and self.doc.first_hose is not None and not x.open_air \
                    and t - x.t_ign >= self.doc.first_hose * 60 and t - x.t_ign < self.doc.first_hose * 60 + dt \
                    and t <= self.last_hit + 600:
                x.local_hose = 1
            if x.local_hose and x.open_air is False and self.doc.first_hose is None:
                x.local_hose = 0
            F = eff.get(x.name, 0.0)
            if x.extra_mj and x.E > x.q * x.area and noz.get(x.name) != "foam":
                F *= 0.5                                     # gasoline re-flashes under water; foam is better
            Fraw = raw.get(x.name, 0.0)
            ainv = x.involved()
            x.ainv_max = max(x.ainv_max, ainv)
            Freq = f_req(ainv)
            held = min(1.0, F / Freq)
            qfo, qfu, qv = x.qfo(), x.qcap_fuel(), q_vent(x.aoh())
            # --- the fire itself
            if x.phase == "ctrl":
                if F <= 0:                                   # water stopped before it was out: rekindles
                    if rng.random() < 0.3:
                        x.phase, x.Q, x.t_ign = "grow", 0.2, t - 60
                        self.reflashes += 1
                    else:
                        x.phase = "out"
                        x.out_at, x.hot_out = t, x.dev_s > 600
                        continue
                else:
                    x.Q *= math.exp(-dt / 60)
                    x.mop += Fraw * dt / 60
                    if x.mop >= 60 * x.ainv_max:
                        x.phase, x.Q, x.out_at, x.hot_out = "out", 0.0, t, x.dev_s > 600
                        continue
            elif held >= 1.0 and x.phase in ("grow", "local", "starved", "dev", "decay"):
                tau = 90 * Freq / F
                x.Q *= math.exp(-dt / tau)
                if x.Q < 0.05 + 0.02 * ainv / 10:
                    x.phase, x.mop = "ctrl", 0.0
            else:
                if x.phase == "grow":
                    te = t - x.t_ign
                    x.Q = x.q0 + x.alpha() * te * te * (1 - held) ** 2
                    lim = min(qfu, qv)
                    if not x.open_air and x.Q >= qfo and qfu >= qfo and qv >= qfo:
                        x.phase, x.flashed = "dev", True
                        self.flashovers += 1
                    elif x.Q >= lim:
                        x.Q = lim
                        if x.open_air:
                            wet = 0.6 if self.doc.wet_decks else 1.0
                            x.phase = "dev" if x.q * wet >= OPEN_SUSTAIN else "local"
                        elif qfu < qfo:
                            x.phase = "local"
                        else:
                            x.phase = "starved"
                elif x.phase == "local":
                    x.Q = min(qfu, qv) * (1 - held)
                    x.local_s += dt
                    if x.local_s > 1200 or x.E <= 0.7 * x.E0:     # the items at the seat are burnt: it dies
                        x.phase, x.Q, x.out_at, x.hot_out = "out", 0.0, t, False
                        continue
                elif x.phase == "starved":
                    x.starved_s += dt
                    x.Q = max(0.02, min(qfu, qv)) * (1 - held)
                    if x.starved_s > 1800:                  # oxygen gone: smoulders out, the steel and char stay hot
                        x.phase, x.Q, x.out_at, x.hot_out = "out", 0.0, t, True
                        continue
                    if qv >= qfo and qfu >= qfo:            # a hole or a door opened: backdraft, flashover
                        x.phase, x.flashed = "dev", True
                        self.flashovers += 1
                elif x.phase == "dev":
                    x.dev_s += dt
                    x.Q = min(qv, qfu) * (1 - 0.7 * held)
                    if x.E <= 0.2 * x.E0:
                        x.phase = "decay"
                elif x.phase == "decay":
                    x.Q = min(qv, qfu) * (1 - 0.7 * held) * max(0.25, x.E / (0.2 * x.E0))
                    if x.flashed:
                        x.dev_s += dt
            x.E -= x.Q * dt
            if x.E <= 0 or (x.phase == "decay" and x.Q < 0.02):
                x.phase, x.Q, x.out_at, x.hot_out = "out", 0.0, t, x.dev_s > 600
            # --- water aboard
            if Fraw > 0 and not x.open_air:
                e = NOZZLE_EVAP[noz.get(x.name, "jet")]
                evap = min(Fraw * dt / 60 * e, x.Q * dt / 2.6)
                self.water_t += (Fraw * dt / 60 - evap) / 1000
            # --- spread
            if x.phase in ("dev", "decay") and x.Q > 0.3:
                ext = (not x.open_air) and qfu > 1.5 * qv
                cool = 0.3 if (self.doc.cooling and F > 0) else 1.0
                for y, k in x.nbrs:
                    if y.burning:
                        continue
                    fuel = min(1.5, max(0.3, (y.q + y.extra_mj / y.area) / 400))
                    if x.open_air and y.open_air:
                        lam = (1 / 360) * min(1.5, y.q / 300) * (0.3 if self.doc.wet_decks else 1.0)
                    else:
                        lam = 0.0
                        path_open = (k in ("up", "down") and (x.holes > 0.5 or y.holes > 0.5)) or \
                                    (k == "side" and x.doors_open and y.doors_open) or \
                                    (x.open_air != y.open_air and k in ("up", "down"))
                        if path_open:
                            lam += (1 / 120) * (2 if ext else 1) * min(1.0, x.Q / 2)
                        delay, T = {"up": (480, 900), "side": (720, 2400), "down": (2400, 10800),
                                    "deck": (720, 2400)}[k]
                        if x.dev_s >= delay and not x.open_air:
                            lam += (1 / T) * fuel * (1.5 if PAINT_ADD[x.era] >= 60 else 1.0) * cool
                    if rng.random() < lam * dt:
                        if y.ignite(t, 0.3):
                            self.lit.add(y.name)
            if x.phase in ("dev", "decay") and any(y.use == "magazine" for y, _ in x.nbrs):
                self.mag_exp += dt / 60
        # --- smoke into the machinery (supply fans), and the pumps with it
        mach = [x for x in self.sp if x.use == "machinery"]
        for m in mach:
            if m.name in self.abandoned:
                continue
            smoky = m.phase in ("dev", "decay", "starved") or any(
                y.phase in ("dev", "decay", "starved") and k == "up" for y, k in m.nbrs)
            if smoky and rng.random() < dt / (60 * (60 if self.doc.fans_secured else 8)):
                self.abandoned.add(m.name)
        if not self.power_lost and mach and len(self.abandoned) >= 0.5 * len(mach):
            self.power_lost, self.t_power = True, t
        # --- ship-level measures
        dev = [x for x in self.sp if x.phase in ("dev", "decay") or (x.open_air and x.burning and x.Q > 1)]
        self.peak_dev = max(self.peak_dev, len(dev))
        secs = {x.sec for x in dev}
        if self.t_ablaze is None and len(secs) >= 0.5 * self.n and len(dev) >= 0.25 * len(self.sp):
            self.t_ablaze = t

    def run(self):
        t, hi = 0.0, 0
        t_all_out = None
        while t <= self.T:
            while hi < len(self.hits) and self.hits[hi][0] * 60 <= t:
                self.hit(t, self.hits[hi][1])
                hi += 1
            for z_b in self.breaks:
                if not z_b[2] and self.doc.t_isolate is not None and t - z_b[1] >= self.doc.t_isolate * 60:
                    z_b[2] = True
                    self._apply_breaks(t)
            self.t_now = t
            if int(t) % 30 == 0:
                self.plan(t)
            self.step(t)
            if hi >= len(self.hits):
                if any(x.burning for x in self.sp):
                    t_all_out = None
                elif t_all_out is None:
                    t_all_out = t
                    if t > self.last_hit + 1800:
                        break
            t += self.dt
        burning_end = sum(1 for x in self.sp if x.burning)
        return dict(fires=self.fires, spaces=len(self.lit), flash=self.flashovers, peak=self.peak_dev,
                    out_min=None if burning_end else (t_all_out / 60 if t_all_out is not None else None),
                    burning_end=burning_end, ablaze_min=None if self.t_ablaze is None else self.t_ablaze / 60,
                    water=self.water_t, mag=self.mag_exp, reflash=self.reflashes,
                    burnt=sum(1 for x in self.sp if x.E0 > 0 and x.E < 0.5 * x.E0),
                    power_min=None if self.t_power is None else self.t_power / 60)


# =============================================================================================================
# 5. Single-compartment benches
# =============================================================================================================
def compartment_free_burn(area, q, doors=1, holes=0.0, era="1939", use="berthing", dt=5.0, t_max=6 * 3600):
    """Unfought fire in one space: returns (flashover min, peak MW, duration h, phase seen)."""
    x = Space("X", use, 0, 2, area, era=era, doors_open=doors, holes=holes)
    x.q = q
    x.ignite(0.0, 0.1)
    t, tfo, peak, phases = 0.0, None, 0.0, []
    while t < t_max and x.burning:
        qfo, qfu, qv = x.qfo(), x.qcap_fuel(), q_vent(x.aoh())
        if x.phase == "grow":
            x.Q = x.q0 + x.alpha() * t * t
            if x.Q >= qfo and qfu >= qfo and qv >= qfo:
                x.phase, tfo = "dev", t
            elif x.Q >= min(qfu, qv):
                x.Q = min(qfu, qv)
                x.phase = "local" if qfu < qfo else "starved"
        elif x.phase == "local":
            x.Q = min(qfu, qv)
            if t > 1200 + 300 or x.E <= 0.7 * x.E0:
                phases.append("burnt out locally")
                break
        elif x.phase == "starved":
            x.Q = max(0.02, min(qfu, qv))
            if t > 1800 + 300:
                phases.append("smouldered out")
                break
        elif x.phase == "dev":
            x.Q = min(qv, qfu)
            if x.E <= 0.2 * x.E0:
                x.phase = "decay"
        elif x.phase == "decay":
            base = min(qv, qfu)
            x.Q = base * max(0.25, x.E / (0.2 * x.E0)) if tfo is not None else base * max(0.25, x.E / (0.7 * x.E0))
        if not phases or phases[-1] != x.phase:
            phases.append(x.phase)
        peak = max(peak, x.Q)
        x.E -= x.Q * dt
        if x.E <= 0 or (x.phase == "decay" and x.Q < 0.02):
            break
        t += dt
    return (None if tfo is None else tfo / 60), peak, t / 3600, "/".join(phases)


def compartment_attack(area, q, flow, nozzle, t_water_min, era="1939", doors=1, dt=2.0):
    """One hose arriving at t_water: returns (control min after water, out min after ignition, flashed, water t)."""
    x = Space("X", "berthing", 0, 2, area, era=era, doors_open=doors)
    x.q = q
    x.ignite(0.0, 0.1)
    t, flashed, t_ctrl, water, ainv_max = 0.0, False, None, 0.0, 0.0
    mop = 0.0
    phase = "grow"
    Q = 0.1
    while t < 4 * 3600:
        qfo, qfu, qv = x.qfo(), x.qcap_fuel(), q_vent(x.aoh())
        F = flow * NOZZLE_EFF[nozzle] if t >= t_water_min * 60 else 0.0
        ainv = area if phase in ("dev",) else area * min(1.0, max(0.1, Q / qfo))
        ainv_max = max(ainv_max, ainv)
        Freq = f_req(ainv)
        held = min(1.0, F / Freq) if F else 0.0
        if phase == "ctrl":
            Q *= math.exp(-dt / 60)
            mop += flow * dt / 60
            if mop >= 60 * ainv_max:
                return t_ctrl, t / 60, flashed, water
        elif held >= 1.0:
            Q *= math.exp(-dt / (90 * Freq / F))
            if Q < 0.05 + 0.002 * ainv:
                phase, t_ctrl = "ctrl", t / 60 - t_water_min
        elif phase == "grow":
            Q = x.q0 + x.alpha() * t * t * (1 - held) ** 2
            if Q >= qfo and qfu >= qfo and qv >= qfo:
                phase, flashed = "dev", True
            Q = min(Q, qfu, qv)
        else:
            Q = min(qv, qfu) * (1 - 0.7 * held)
        if F:
            e = NOZZLE_EVAP[nozzle]
            water += (flow * dt / 60 - min(flow * dt / 60 * e, Q * dt / 2.6)) / 1000
        x.E -= Q * dt
        if x.E <= 0:
            return t_ctrl, t / 60, flashed, water
        t += dt
    return t_ctrl, None, flashed, water


# =============================================================================================================
# 6. Scenarios
# =============================================================================================================
def spread_hits(n, t0, t1, mix, rng):
    kinds = [k for k, w in mix for _ in range(int(w * 100))]
    return [(rng.uniform(t0, t1), rng.choice(kinds)) for _ in range(n)]


SCENARIOS = [
    # name, ship, era, doctrine, hits(n, t0, t1, mix), aircraft, the record
    ("Santiago 1898: armoured cruiser, unstripped, no fire organisation", "cruiser", "1890", "disorganised_1890",
     (30, 0, 45, [("BP", 0.7), ("HE", 0.3)]), 0,
     "Teresa beached 40 min, Oquendo 55, Vizcaya 100: 'burned out'"),
    ("Same hits, stripped and prepared (hoses laid out)", "cruiser", "1905", "prepared_1894",
     (30, 0, 45, [("BP", 0.7), ("HE", 0.3)]), 0,
     "Zhenyuan at the Yalu: fires 'kept in hand'"),
    ("Lighter fire (12 hits in 45 min), unstripped, unorganised", "cruiser", "1890", "disorganised_1890",
     (12, 0, 45, [("BP", 0.7), ("HE", 0.3)]), 0, "Chaoyong, Yangwei at the Yalu: burned out"),
    ("Lighter fire (12 hits in 45 min), stripped and prepared", "cruiser", "1905", "prepared_1894",
     (12, 0, 45, [("BP", 0.7), ("HE", 0.3)]), 0, "Matsushima, Hiei: fires put out"),
    ("Tsushima 1905: battleship under concentration (Suvorov)", "battleship", "1905", "tsushima_1905",
     (60, 0, 40, [("HE", 0.85), ("AP", 0.15)]), 0,
     "Suvorov ablaze fore and aft ~30 min after; burned ~5 h"),
    ("Jutland 1916: RN battleship, 15 heavy hits", "battleship", "1914", "rn_1916",
     (15, 0, 30, [("AP", 0.8), ("HE", 0.2)]), 0,
     "fires rare and short (D.K. Brown); propellant fires are 07's"),
    ("Savo 1942: heavy cruiser, 1942 fit, 50 hits in 20 min", "cruiser", "1939", "usn_1942",
     (50, 0, 20, [("HE", 0.5), ("AP", 0.5)]), 2,
     "Astoria: never controlled; sank ~10 h later"),
    ("Same hits, post-Savo fit", "cruiser", "1943", "usn_1944",
     (50, 0, 20, [("HE", 0.5), ("AP", 0.5)]), 0,
     "later USN cruisers controlled their fires"),
    ("San Francisco 1942: ~45 hits over 70 min, inflammables landed", "cruiser", "1943", "usn_1942",
     (45, 0, 70, [("HE", 0.7), ("AP", 0.3)]), 0,
     "~22 fires, all 'brought under control quickly'"),
    ("South Dakota 1942: 26 hits in 17 min", "battleship", "1943", "usn_1942",
     (26, 0, 17, [("HE", 0.6), ("AP", 0.4)]), 0,
     "13 fires, 'quickly extinguished before spreading'"),
    ("Destroyer 1945: kamikaze (gasoline) amidships", "destroyer", "1943", "usn_1944",
     None, 0, "Zellars 15, Walke 15, Stormes 20, Hadley 25 min"),
    ("Same kamikaze, 1942 doctrine and fit", "destroyer", "1939", "usn_1942",
     None, 0, "Abner Read, 1944: lost after control was lost"),
]


def run_scenario(sc, runs, seed=1):
    name, kind, era, doc, hd, air, rec = sc
    rng = random.Random(seed)
    out = []
    for _ in range(runs):
        if hd is None:
            hits = [(0.0, "GAS")]
        else:
            hits = spread_hits(*hd, rng)
        out.append(Sim(kind, era, doc, hits, rng, aircraft=air).run())
    return out


def med(v):
    v = [a for a in v if a is not None]
    return st.median(v) if v else None


def fmt(v, f="{:.0f}"):
    return "-" if v is None else f.format(v)


def main():
    only_physics = len(sys.argv) > 1 and sys.argv[1] == "physics"

    print("== T1. Fire load by era (MJ/m2 of deck, paint included) ==")
    print(f"{'space':12}" + "".join(f"{e:>8}" for e in ERAS))
    for u in FIRE_LOAD:
        if u == "magazine":
            continue
        print(f"{u:12}" + "".join(f"{load(u, e):8.0f}" for e in ERAS))
    print("P(fire) per burst, mean space of each era (08 sec 3.9 check):")
    for e in ERAS:
        q = st.mean(load(u, e) for u in ("berthing", "cabins", "office", "weather", "passage"))
        print(f"  {e}: q~{q:4.0f}  HE {p_fire('HE', q):.2f}  AP {p_fire('AP', q):.2f}  BP common {p_fire('BP', q):.2f}")

    print("\n== T2. One berthing space burning unfought (flashover min, peak MW, hours, phases) ==")
    cases = [
        ("40 m2, 1943 load (240+10), door open", 40, load("berthing", "1943"), 1, 0.0),
        ("40 m2, 1939 load, door open", 40, load("berthing", "1939"), 1, 0.0),
        ("40 m2, 1939 load, closed up", 40, load("berthing", "1939"), 0, 0.0),
        ("40 m2, 1939 load, closed up + 1 m2 shell hole", 40, load("berthing", "1939"), 0, 1.0),
        ("40 m2, 1939 load, door + 3 m2 of holes", 40, load("berthing", "1939"), 1, 3.0),
        ("90 m2 cabins, 1890 load, door open", 90, load("cabins", "1890"), 1, 0.0),
        ("90 m2 cabins, 1890 load, 4 m2 of holes", 90, load("cabins", "1890"), 1, 4.0),
        ("90 m2 cabins, 1943 load, door open", 90, load("cabins", "1943"), 1, 0.0),
        ("30 m2 stores, 1939, door open", 30, load("stores", "1939"), 1, 0.0),
    ]
    for lab, a, q, d, h in cases:
        era = "1890" if "1890" in lab else ("1943" if "1943" in lab else "1939")
        use = "stores" if "stores" in lab else ("cabins" if "cabins" in lab else "berthing")
        tfo, pk, hrs, ph = compartment_free_burn(a, q, d, h, era=era, use=use)
        print(f"  {lab:46} q={q:5.0f}  flashover {fmt(tfo, '{:.1f}'):>5} min  peak {pk:5.2f} MW  "
              f"{hrs:4.1f} h  {ph}")

    print("\n== T3. One hose on a 30 m2 (74 m3, Shadwell-size) fire, 1939 load ==")
    print("   nozzle, flow, water at  ->  control (min after water), out (min after ignition), flashed, water aboard t")
    for noz, fl, tw in (("fog", 360, 4), ("jet", 360, 4), ("fog", 360, 1), ("fog", 360, 8), ("allpurpose", 250, 4),
                        ("jet", 150, 4), ("jet", 750, 4)):
        tc, to, fl_, w = compartment_attack(30, load("berthing", "1939"), fl, noz, tw)
        print(f"  {noz:10} {fl:4.0f} L/min at {tw:2.0f} min: control {fmt(tc, '{:.1f}'):>5}, "
              f"out {fmt(to, '{:.1f}'):>5}, flashed {fl_!s:5}, water {w:4.1f} t")

    print("\n== T4. First-water time vs outcome: 90 m2 1939 messdeck, one 250 L/min all-purpose hose ==")
    for tw in (0.5, 1, 2, 3, 4, 6, 8, 10, 15):
        tc, to, fl_, w = compartment_attack(90, load("berthing", "1939"), 250, "allpurpose", tw)
        tc2, to2, fl2, w2 = compartment_attack(90, load("berthing", "1939"), 750, "allpurpose", tw)
        print(f"  water at {tw:4.1f} min: 1 hose -> out {fmt(to, '{:.0f}'):>4} min (flashed {fl_!s:5}); "
              f"3 hoses -> out {fmt(to2, '{:.0f}'):>4} min (flashed {fl2!s:5}, {w2:4.1f} t aboard)")

    print("\n== T5. Water a hose leaves aboard, per hour on a 2 MW fire (t/h) ==")
    for noz, fl in (("jet", 400), ("allpurpose", 250), ("fog", 360)):
        evap = min(fl * NOZZLE_EVAP[noz], 2.0 * 60 / 2.6)
        print(f"  {noz:10} {fl:4.0f} L/min = {fl * 0.06:4.1f} t/h applied, {fl * 0.06 - evap * 0.06:4.1f} t/h stays")

    if only_physics:
        return
    print("\n== T6. Ships (60 runs each): medians, and the share never out by the end (4 h) ==")
    print(f"  {'scenario':60} {'hit fires':>5} {'flash':>5} {'peak':>4} {'out min':>7} {'never':>5} "
          f"{'ablaze':>6} {'water t':>7} {'mag min':>7} {'burnt':>5}")
    keep = {}
    for sc in SCENARIOS:
        res = run_scenario(sc, 60)
        keep[sc[0]] = res
        never = sum(1 for r in res if r["out_min"] is None) / len(res)
        abl = [r["ablaze_min"] for r in res]
        abl_share = sum(1 for a in abl if a is not None) / len(res)
        print(f"  {sc[0][:60]:60} {med([r['fires'] for r in res]):9.0f} {med([r['flash'] for r in res]):5.0f} "
              f"{med([r['peak'] for r in res]):4.0f} {fmt(med([r['out_min'] for r in res])):>7} {never:5.0%} "
              f"{fmt(med(abl)):>4}{'' if abl_share >= 0.5 else '*':1} {med([r['water'] for r in res]):7.0f} "
              f"{med([r['mag'] for r in res]):7.0f} {med([r['burnt'] for r in res]):5.0f}")
        print(f"      record: {sc[6]}; ablaze (half the sections) in {abl_share:.0%} of runs")
    print("  * ablaze median over the runs that got there (fewer than half did)")

    print("\n== T8. Savo hits (50 in 20 min, cruiser, 1939 load): one post-Savo change at a time (60 runs) ==")
    base = DOCTRINES["usn_1942"]
    good = DOCTRINES["usn_1944"]
    variants = [("1942 fit, as fought", base, "1939", 2),
                ("+ aircraft landed", base, "1939", 0),
                ("+ stripped (1943 load)", base, "1943", 2),
                ("+ supply fans secured", replace(base, fans_secured=True), "1939", 2),
                ("+ looped main, diesel pump, jumpers 6 min", replace(base, looped=True, t_isolate=4, jumper=6), "1939", 2),
                ("+ fog nozzles (360 L/min)", replace(base, nozzle="fog", flow=360), "1939", 2),
                ("+ breathing sets", replace(base, ba=True), "1939", 2),
                ("+ first hose at 1 min", replace(base, first_hose=1.0, muster=2), "1939", 2),
                ("all of it (post-Savo)", good, "1943", 0)]
    print(f"  {'change':44} {'never out':>9} {'out min':>7} {'spaces':>6} {'power lost':>10} {'water t':>7}")
    for lab, doc, era, air in variants:
        rng = random.Random(11)
        res = [Sim("cruiser", era, doc, spread_hits(50, 0, 20, [("HE", 0.5), ("AP", 0.5)], rng), rng,
                   aircraft=air).run() for _ in range(60)]
        never = sum(1 for r in res if r["out_min"] is None) / len(res)
        pl = sum(1 for r in res if r["power_min"] is not None) / len(res)
        print(f"  {lab:44} {never:9.0%} {fmt(med([r['out_min'] for r in res])):>7} "
              f"{med([r['spaces'] for r in res]):6.0f} {pl:10.0%} {med([r['water'] for r in res]):7.0f}")

    print("\n== T7. Bimodality: spaces still burning at the end (4 h), from T6's runs ==")
    for lab, part in (("Santiago, unprepared", keep[SCENARIOS[0][0]]), ("Santiago, prepared", keep[SCENARIOS[1][0]]),
                      ("Yalu weight, unprepared", keep[SCENARIOS[2][0]]), ("Yalu weight, prepared", keep[SCENARIOS[3][0]]),
                      ("Savo, 1942 fit", keep[SCENARIOS[6][0]]), ("San Francisco", keep[SCENARIOS[8][0]])):
        hist = {}
        for r in part:
            b = "out" if r["out_min"] is not None else ("1-3" if r["burning_end"] <= 3 else
                                                         ("4-9" if r["burning_end"] <= 9 else "10+"))
            hist[b] = hist.get(b, 0) + 1
        print(f"  {lab:22}: " + "  ".join(f"{k}: {hist.get(k, 0)}" for k in ("out", "1-3", "4-9", "10+")))


if __name__ == "__main__":
    main()
