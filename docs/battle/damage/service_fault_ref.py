"""service_fault_ref.py -- reference for 17_service_network_faults.md (Fleetwright damage research).

Two ways a ship's power network loses circuits in battle, and what the loss does next:

  1. SHOCK TRIPS   breakers, contactors, ABTs, fuses, lamps and generators opening (or closing) under
                   underwater shock, with no electrical fault.  A lognormal fragility in the shock factor
                   at the device, by the ship's "fitting grade" (unsecured / secured / shock-tested).
  2. CABLE CUTS    fragments, blast, fire and water on cable runs (wireways).  The fragment field is 13's
                   (fragmentation_ref.py, imported); this file adds wireways, cables cut per fragment, and
                   what a cut cable does electrically (open, ground, short).

and one network model both feed: a radial tree of generators -> switchboards -> feeders -> panels ->
branches -> consumers, with protective devices (generator ACB, feeder breaker, panel breaker / fuse),
lock-in, selectivity, ABTs, LVR/LVP motor controllers, grounds on an insulated system, and restore times.
`python service_fault_ref.py` prints every table quoted in the note (about 10 s).

Tags: [SRC] sourced, [FIT] fitted here to the record, [EST] judgement, [UNCERTAIN] thin sources.
Units: kg, m, s; times in minutes; shock factor NSF = sqrt(W lb)/R ft (as 15).
"""
import random
from dataclasses import dataclass, field
from math import sqrt, exp, log, erf, ceil, sin, radians

import fragmentation_ref as fr

# =============================================================================================
# 1. Shock fragility
# =============================================================================================
GRADES = ["G0 unsecured", "G1 secured", "G2 shock-tested", "G3 post-war"]

# Median shock factor at the device (NSF units, after the mount/level/size corrections of
# undex_S) at which it fails.  [FIT] G0 to the 1939-41 British bomb near misses (15 §3.2: breakers
# and lights in the 0.03-0.08 band), G1 to North Carolina (0 trips) and O'Brien (1 main breaker),
# G2 [EST] from the 1943+ HI-test standard, G3 [EST] (MIL-S-901 grade A, equipment ~0.2 KSF design
# point x margin).
S50 = {
    #               G0     G1     G2     G3
    "breaker":    (0.07,  0.22,  0.55,  1.10),   # ACB/AQB trip latch, armature inertia [SRC mechanism]
    "contactor":  (0.06,  0.18,  0.45,  0.90),   # motor controller drops out (LVP) [SRC WDR 58]
    "abt":        (0.10,  0.30,  0.80,  None),   # ABT transfers/closes spuriously (AC, where fitted)
    "fuse":       (0.10,  0.60,  1.20,  2.00),   # fuse jumps its clips (Crossroads, RN "wire fuses")
    "lamp":       (0.05,  0.10,  0.18,  0.40),   # filament; pendant holders, rough-service, fluorescent
    "generator":  (0.12,  0.30,  0.70,  1.40),   # turbo-gen trips (governor, oil pressure, its ACB)
    "bus_short":  (0.90,  1.60,  3.00,  5.00),   # busbars/battery bars jarred together (Tunny, Cobia)
}
BETA_COMMON = 0.35   # shared by everything in one switchboard/space for one event   [EST]
BETA_ITEM = 0.35     # each device's own scatter                                       [EST]
BETA = sqrt(BETA_COMMON ** 2 + BETA_ITEM ** 2)

MOUNT = {"hold": 1.00, "platform": 0.85, "deck": 0.65, "super": 0.45}   # up the structure [EST]


def phi(x):
    return 0.5 * (1 + erf(x / sqrt(2)))


def p_fail(item, grade, S):
    s50 = S50[item][grade]
    if s50 is None or S <= 0:
        return 0.0
    return phi(log(S / s50) / BETA)


def nsf(w_kg, r_m, angle_deg=90.0):
    """Shock factor sqrt(W lb)/R ft with the keel-shock angle form (1+sin a)/2 (15 §3.1)."""
    return sqrt(w_kg / 0.45359) / (r_m / 0.3048) * (1 + sin(radians(angle_deg))) / 2


def size_factor(disp_t):
    """Whole-hull response falls with mass: (disp/2000 t)^-1/6, capped at 1 [EST]."""
    return min(1.0, (disp_t / 2000.0) ** (-1 / 6))


def undex_S(nsf_hull, level, disp_t, resilient=False):
    """Severity at a device: hull NSF at its space x level x size (x 0.5 on resilient mounts)."""
    return nsf_hull * MOUNT[level] * size_factor(disp_t) * (0.5 if resilient else 1.0)


# =============================================================================================
# 2. Cable cuts
# =============================================================================================
CABLE_D = 0.025        # m, a typical armoured power/IC cable (USN HFA 10-40 mm) [EST]
E_CABLE = 500.0        # J to sever one cable (14 §5.2's cable/pipe class) [EST]
LATERAL = 1.3          # cables cut across the path per layer (neighbour nicked 30%) [EST]
MIN_G, V_MIN = 5.0, 150.0   # 13 §4.4's T5 criterion for a cutting fragment [EST]

_cut_cache = {}


def wireway_hits(shell_name, R, w, behind_mm=0.0):
    """(expected fragment hits on a wireway of width w crossing the side band at R, mean KE J)."""
    key = (shell_name, round(R, 2), round(w, 3), behind_mm)
    if key in _cut_cache:
        return _cut_cache[key]
    b = fr.burst(fr.SHELL[shell_name])
    hits = ke = 0.0
    for m, n in fr.mass_bins(b, nb=120):
        if m * 1000 < MIN_G:
            continue
        v = fr.v_at(b.v_body, m, R)
        if v < V_MIN:
            continue
        if behind_mm > 0:
            v50 = fr.thor_v50(m, behind_mm, "mild")
            if v <= v50:
                continue
            v = sqrt(v * v - v50 * v50)          # residual speed, Lambert-Jonas form [EST]
        h = fr.line_hits(n, w, R)
        hits += h
        ke += h * 0.5 * m * v * v
    out = (hits, ke / hits if hits else 0.0)
    _cut_cache[key] = out
    return out


def tray(n_cables, per_row=8):
    """Width (m) and layers of a wireway of n cables laid per_row across."""
    across = min(n_cables, per_row)
    return across * CABLE_D, ceil(n_cables / across)


def cut_stats(shell_name, R, n_cables, behind_mm=0.0, per_row=8):
    """P(wireway touched), P(a given cable cut), expected cables cut."""
    w, layers = tray(n_cables, per_row)
    hits, ke = wireway_hits(shell_name, R, w, behind_mm)
    depth = min(layers, max(1.0, ke / E_CABLE))
    c = min(n_cables, LATERAL * depth)            # cables cut per fragment that hits
    p_one = 1 - exp(-hits * c / n_cables)
    return 1 - exp(-hits), p_one, n_cables * p_one


def wreck_radius(shell_name):
    """Inside 1.0 W^1/3 m of the burst everything is wrecked (11 §4.3), W = filler kg."""
    return 1.0 * fr.SHELL[shell_name].filler_kg ** (1 / 3)


# What a cut does to an energised cable [EST; ordering from the WDRs: shorts and fires on cut,
# energised cables (Sterett, Johnston, Savannah), grounds cleared later (O'Brien), open cuts silent].
FAULT_MIX = {           # (short, ground, open)
    "fragment": (0.30, 0.40, 0.30),
    "blast":    (0.25, 0.30, 0.45),
    "fire":     (0.60, 0.35, 0.05),
}

# Fire, water and construction: rules the note states, kept here as the one place for numbers [EST]
CABLE_FIRE_MIN = {        # median minutes of fire in a space before a run through it fails (lognormal s 0.5)
    "rubber, lead-sheathed (RN; USN before 1942)": 6.0,
    "heat-and-flame-resistant, asbestos (USN HFA, 1942+)": 15.0,
}
WICK_M = 50.0             # water travels this far along a cut cable's core (Savannah: >160 ft) [SRC order]
P_WICK_FAULT = 0.15       # a dry board or box fed by a cable cut in a flooded space faults within 24 h
BOX_FLOOD_MIN = (20.0, 0.8)   # junction / connection boxes under water fail (San Francisco turret I)
MULTICORE = {"circuits_per_cable": 4, "splice_x": 2.5}   # IJN: lighter, cut together, slow to splice
STUFFING_TUBE_CM2 = 2.0   # leak area per unpacked cable penetration of a holed bulkhead (Boise) [EST]

# =============================================================================================
# 3. The network
# =============================================================================================
SEL = {   # P(a device clears a fault it sees, before the next one up does)            [FIT/EST]
    "basic":     {"fuse": 0.90, "branch": 0.75, "feeder": 0.90, "gen": 1.0},   # 1939-42 dashpot ACBs
    "selective": {"fuse": 0.95, "branch": 0.92, "feeder": 0.98, "gen": 1.0},   # PQ relays, 1943+
}
P_SECOND_GROUND = 0.25   # a new ground on a system already grounded is on another conductor [EST]
P_BLIND_RESET = 0.5      # crew resets a tripped device before the fault is found          [EST]
P_FIRE = {"near": 0.03, "up": 0.10, "reset": 0.05, "locked": 0.20}   # electrical fire per short [EST]


def lognorm(rng, med, s):
    return med * exp(rng.gauss(0, s))


T_RESET_ATT = (0.5, 0.6)      # min: first device at a manned board                       [FIT]
T_RESET_NEXT = 0.15           # min per further device at the same board (one operator)    [FIT]
T_RESET_UNATT = (4.0, 0.6)    # min: someone must go to an unmanned panel                  [EST]
T_GEN = (1.5, 0.5)            # min: restart / resynchronise a tripped turbo-generator      [FIT SD]
T_DIESEL = 0.4                # min: emergency diesel auto-start                           [SRC DD rpt]
T_FIND = (3.0, 0.7)           # min: find and isolate a fault in a damaged space           [FIT SD 3 min]
T_FIND_BOARD = (10.0, 0.5)    # min: a fault on a switchboard itself                        [FIT Savannah]
T_LVP_ATT, T_LVP_UNATT = 0.3, (3.0, 0.6)   # restart an LVP controller                     [EST]
T_SPLICE = (30.0, 0.6)        # min: splice / jumper one cut cable (multicore x2.5)         [EST]
HORIZON = 60.0                # min: "the rest of the action"


@dataclass
class Node:
    """A board (root), feeder/panel (inner) or branch/consumer circuit (leaf)."""
    name: str
    kind: str                  # board | feeder | branch
    comp: str                  # where its protective device sits
    parent: "Node" = None
    alt: "Node" = None         # ABT alternate parent (or manual transfer if abt False)
    abt: bool = False
    locked: bool = False       # breaker locked in: never trips on shock, never clears faults
    fuse: bool = False
    attended: bool = False     # a man at the device
    route: list = field(default_factory=list)   # compartments the cable passes through
    func: str = ""             # function it serves (leaf)
    vital: bool = False
    lvr: bool = True           # restarts by itself when power returns (LVR) vs needs a hand (LVP)
    gens: int = 0              # boards: generators on it
    emergency: bool = False    # emergency diesel board (starts on loss)
    children: list = field(default_factory=list)


class Ship:
    def __init__(self, name, disp_t, ac=True, grade=1, sel="basic", comps=None):
        self.name, self.disp, self.ac, self.grade, self.sel = name, disp_t, ac, grade, sel
        self.comps = comps or {}       # comp -> (x m from bow, level)
        self.nodes = {}
        self.filler = {}               # comp -> number of other cables in its wireway

    def add(self, n: Node):
        self.nodes[n.name] = n
        if n.parent:
            n.parent.children.append(n)
        return n

    def leaves(self):
        return [n for n in self.nodes.values() if n.kind == "branch"]

    def path(self, n, use_alt=None):
        """Devices from n up to its board (normal feed unless use_alt names the ABT node)."""
        out, cur = [], n
        while cur:
            out.append(cur)
            cur = cur.alt if (use_alt is cur and cur.alt) else cur.parent
        return out

    def wireways(self):
        """comp -> list of leaf/feeder nodes whose cable passes through it."""
        w = {}
        for n in self.nodes.values():
            for c in n.route:
                w.setdefault(c, []).append(n)
        return w


# ---------------------------------------------------------------------------------------------
# 3.1 One event: shock trips and faults -> who is dark for how long
# ---------------------------------------------------------------------------------------------

@dataclass
class Outcome:
    trips: int = 0
    devices: int = 0
    shorts: int = 0
    grounds: int = 0
    fires: int = 0
    cascades: int = 0          # faults cleared above the nearest device
    gen_clears: int = 0        # ... of them, at a generator breaker (a whole board dark)
    abt_events: int = 0
    out: dict = field(default_factory=dict)    # leaf name -> minutes dark
    cut: set = field(default_factory=set)      # leaf/feeder names cut


def evaluate(ship: Ship, rng, shock=None, cuts=None, repair_vital=True):
    """shock: comp -> S at the devices there (UNDEX); cuts: list of (node, cause, comp).
    Returns Outcome with each leaf's dark time (min, capped at HORIZON)."""
    o = Outcome()
    g = ship.grade
    sel = SEL[ship.sel]
    opened = {}                 # node name -> restore time (min) of its device
    board_queue = {}            # board/comp -> devices reset there so far (for sequential resets)
    dead_until = {}             # node name -> time its own circuit returns (cut cables)
    blocked = {}                # node name -> a fault below it is not yet isolated

    def reset_time(n, t0=0.0):
        if n.kind == "board":
            return t0 + (T_DIESEL if n.emergency else lognorm(rng, *T_GEN))
        if n.attended:
            k = board_queue.get(n.comp, 0)
            board_queue[n.comp] = k + 1
            return t0 + lognorm(rng, *T_RESET_ATT) + k * T_RESET_NEXT
        return t0 + lognorm(rng, *T_RESET_UNATT)

    # --- shock: trips with no fault
    if shock:
        common = {}
        for n in ship.nodes.values():
            S = shock.get(n.comp, 0.0)
            if S <= 0:
                continue
            zc = common.setdefault(n.comp, rng.gauss(0, BETA_COMMON))
            o.devices += 1

            def fails(item):
                s50 = S50[item][g]
                return s50 is not None and log(S) + zc + rng.gauss(0, BETA_ITEM) > log(s50)

            if n.kind == "board":
                if not n.emergency and fails("generator"):
                    opened[n.name] = reset_time(n)
                    o.trips += 1
                if fails("bus_short"):
                    o.shorts += 1
                    t = lognorm(rng, *T_FIND_BOARD) + lognorm(rng, *T_GEN)
                    opened[n.name] = max(opened.get(n.name, 0), t)
                    if rng.random() < P_FIRE["near"]:
                        o.fires += 1
                continue
            item = "fuse" if n.fuse else "breaker"
            if not n.locked and fails(item):
                opened[n.name] = max(opened.get(n.name, 0), reset_time(n))
                o.trips += 1
            if n.abt and ship.ac and fails("abt"):
                # out-of-phase paralleling: contacts weld, a feeder ruptures, and the fault clears at
                # the generator breaker (South Dakota: the after part dark ~1 min). The welded ABT
                # leaves the load on its alternate; the board is back when its generator is.
                o.abt_events += 1
                o.shorts += 1
                bd = ship.path(n)[-1]
                opened[bd.name] = max(opened.get(bd.name, 0), lognorm(rng, 1.0, 0.4))

    # --- faults from cuts
    grounds = []
    seen = set()
    for n, cause, comp in (cuts or []):
        if True:
            if n.name in seen:      # a circuit already cut is already faulted (or silent)
                continue
            seen.add(n.name)
            o.cut.add(n.name)
            rep = lognorm(rng, *T_SPLICE) if (repair_vital and n.vital) else HORIZON
            dead_until[n.name] = max(dead_until.get(n.name, 0), rep)
            ps, pg, _ = FAULT_MIX[cause]
            r = rng.random()
            kind = "short" if r < ps else ("ground" if r < ps + pg else "open")
            if kind == "ground":    # USN AC and DC alike were insulated: one ground is silent
                root = ship.path(n)[-1].name
                if root in grounds and rng.random() < P_SECOND_GROUND:
                    kind = "short"  # a second ground on another conductor of the same system
                grounds.append(root)
        if kind == "ground":
            o.grounds += 1
            continue
        if kind == "open":
            continue
        o.shorts += 1
        t_find = lognorm(rng, *T_FIND)
        # walk up: the first unlocked device that clears; the generator always clears
        chain = ship.path(n)
        for i, d in enumerate(chain):
            if d.kind == "board":
                p = 1.0
            elif d.locked:
                continue
            else:
                p = sel["fuse"] if d.fuse else sel["branch" if d.kind == "branch" else "feeder"]
            if rng.random() < p:
                break
        if i > 0:
            o.cascades += 1
        if d.kind == "board":
            o.gen_clears += 1
        # the device that cleared is out until the fault is found and isolated, then reset
        tr = reset_time(d, t_find)
        blind = rng.random() < P_BLIND_RESET
        pf = P_FIRE["near" if i == 0 else "up"] + (P_FIRE["reset"] if blind else 0)
        if any(x.locked for x in chain[:i]):
            pf = max(pf, P_FIRE["locked"])
        if rng.random() < pf:
            o.fires += 1
        opened[d.name] = max(opened.get(d.name, 0), tr)
        for x in chain[:i]:          # nothing below the clearing device can be fed from elsewhere
            blocked[x.name] = max(blocked.get(x.name, 0), t_find)   # until the fault is isolated
        # an ABT between the fault and the device: transfers, and the alternate sees the fault
        for x in chain[:i]:
            if x.abt and x.alt:
                o.abt_events += 1
                alt_chain = ship.path(x, use_alt=x)
                k = alt_chain.index(x)
                for d2 in alt_chain[k + 1:]:
                    if d2.kind == "board" or not d2.locked:
                        opened[d2.name] = max(opened.get(d2.name, 0), reset_time(d2, t_find))
                        break
                break

    # --- each leaf: dark until its whole normal path is closed, or its ABT path is
    def path_ok_time(n, use_alt=None):
        t = 0.0
        below = True                 # nodes up to the transfer point carry the fault's block
        for d in ship.path(n, use_alt):
            t = max(t, opened.get(d.name, 0.0), dead_until.get(d.name, 0.0))
            if below and use_alt is not None:
                t = max(t, blocked.get(d.name, 0.0))
            if d is use_alt:
                below = False
            if d.kind == "board" and d.emergency:
                t = max(t, T_DIESEL)
        return t

    for leaf in ship.leaves():
        t = path_ok_time(leaf)
        for x in ship.path(leaf):
            if x.alt is not None:
                ta = path_ok_time(leaf, use_alt=x) + (0.0 if x.abt else 1.5)   # manual transfer
                t = min(t, ta)
        if t > 0 and not leaf.lvr:
            t += T_LVP_ATT if leaf.attended else lognorm(rng, *T_LVP_UNATT)
        o.out[leaf.name] = min(t, HORIZON)
    return o


# =============================================================================================
# 4. Two test ships
# =============================================================================================

def destroyer(grade=1, sel="basic", emergency=True, locked=True, lvp_pumps=False, ac=True, abt=True):
    """A 2,100-ton destroyer, split plant, emergency diesels forward and aft outside machinery
    (USN 2,100-ton practice [SRC DD report]); generator ratings and panels [EST]."""
    C = {"FWD_PLAT": (20, "platform"), "PLOT": (28, "platform"), "BRIDGE": (25, "super"),
         "CHART": (27, "super"), "DIR": (24, "super"), "TRUNK": (26, "super"), "FR1": (40, "hold"),
         "ER1": (50, "hold"), "FR2": (60, "hold"), "ER2": (70, "hold"), "AFT_PLAT": (88, "platform"),
         "STEER": (105, "platform"), "PASS_P": (60, "deck"), "PASS_S": (60, "deck"),
         "M12": (18, "deck"), "M345": (90, "deck"), "TT": (65, "deck")}
    s = Ship("DD 2,100 t", 2500, ac=ac, grade=grade, sel=sel, comps=C)
    B1 = s.add(Node("SWBD 1", "board", "ER1", attended=True, gens=1))
    B2 = s.add(Node("SWBD 2", "board", "ER2", attended=True, gens=1))
    EF = s.add(Node("EMERG F", "board", "FWD_PLAT", emergency=True)) if emergency else None
    EA = s.add(Node("EMERG A", "board", "AFT_PLAT", emergency=True)) if emergency else None
    lk = locked
    PF = s.add(Node("PNL fwd", "feeder", "ER1", B1, attended=True, route=["ER1", "FR1", "FWD_PLAT"]))
    PA = s.add(Node("PNL aft", "feeder", "ER2", B2, attended=True, route=["ER2", "AFT_PLAT"]))
    IC = s.add(Node("IC/FC board", "feeder", "PLOT", B1, alt=EF, abt=abt and bool(EF), attended=True,
                    route=["ER1", "FR1", "PASS_S", "PLOT"]))
    s.add(Node("director", "branch", "PLOT", IC, attended=True, route=["PLOT", "TRUNK", "DIR"], func="FC", vital=True))
    s.add(Node("FC radar", "branch", "PLOT", IC, attended=True, route=["PLOT", "TRUNK", "DIR"], func="FC", vital=True))
    s.add(Node("plot", "branch", "PLOT", IC, attended=True, route=["PLOT"], func="FC", vital=True))
    s.add(Node("IC bridge", "branch", "PLOT", IC, attended=True, route=["PLOT", "TRUNK", "CHART", "BRIDGE"],
               func="IC", vital=True))
    s.add(Node("search radar", "branch", "PLOT", IC, attended=True, route=["PLOT", "TRUNK", "CHART"], func="radar"))
    s.add(Node("gyro", "branch", "PLOT", IC, attended=True, route=["PLOT"], func="IC", vital=True))
    s.add(Node("mounts 1-2", "branch", "FWD_PLAT", PF, locked=lk, route=["FWD_PLAT", "M12"], func="guns"))
    s.add(Node("mounts 3-5", "branch", "AFT_PLAT", PA, locked=lk, route=["AFT_PLAT", "M345"], func="guns"))
    s.add(Node("torpedo dir", "branch", "FWD_PLAT", PF, locked=lk,
               route=["FWD_PLAT", "TRUNK", "CHART"], func="torpedo"))
    s.add(Node("light fwd", "branch", "FWD_PLAT", PF, locked=lk, route=["FWD_PLAT", "BRIDGE", "CHART"],
               func="light"))
    s.add(Node("light aft", "branch", "AFT_PLAT", PA, locked=lk, route=["AFT_PLAT", "M345"], func="light"))
    ST = s.add(Node("steering", "branch", "STEER", PA, alt=EA, abt=abt and bool(EA), locked=lk,
                    route=["ER2", "AFT_PLAT", "STEER"], func="steer", vital=True))
    s.add(Node("fire pump 1", "branch", "ER1", B1, alt=EF, abt=abt and bool(EF), attended=True,
               route=["ER1"], func="pumps", vital=True, lvr=not lvp_pumps))
    s.add(Node("fire pump 2", "branch", "ER2", B2, alt=EA, abt=abt and bool(EA), attended=True,
               route=["ER2"], func="pumps", vital=True, lvr=not lvp_pumps))
    s.add(Node("drain pump F", "branch", "FR1", PF, locked=lk, route=["FR1"], func="pumps",
               lvr=not lvp_pumps))
    s.filler = {"TRUNK": 30, "CHART": 20, "BRIDGE": 25, "PLOT": 30, "PASS_P": 12, "PASS_S": 12,
                "FWD_PLAT": 20, "AFT_PLAT": 20, "M12": 8, "M345": 8, "ER1": 25, "ER2": 25, "FR1": 10,
                "FR2": 10, "STEER": 6, "DIR": 10, "TT": 6}
    return s


def battleship(grade=1, sel="basic", locked=True, abt=True, emerg=True):
    """A 1942 fast battleship after South Dakota (WDR 57): four main boards in the machinery, two
    emergency diesel boards, an IC/FC switchboard with an ABT (normal: SWBD 1, alternate: emergency
    forward), superstructure FC and IC branches in unprotected wireways [layout EST]."""
    C = {"ER1": (95, "hold"), "ER2": (110, "hold"), "ER3": (125, "hold"), "ER4": (140, "hold"),
         "EDF": (60, "platform"), "EDA": (170, "platform"), "ICR": (80, "platform"),
         "PLOT": (82, "platform"), "STEER": (195, "platform"), "CT": (75, "super"),
         "CT_TUBE": (75, "deck"), "BRIDGE": (78, "super"), "PILOT": (76, "super"),
         "SKY": (80, "super"), "MDIR": (78, "super"), "SUP01": (85, "super"),
         "SUP02": (90, "super"), "SUP03": (95, "super"), "SUP04": (105, "super"),
         "ADIR": (130, "super"), "SUPA": (125, "super"), "SEC_P": (100, "deck"),
         "SEC_S": (100, "deck"), "MAIN_P": (110, "deck"), "MAIN_S": (110, "deck")}
    s = Ship("BB 35,000 t", 44000, ac=True, grade=grade, sel=sel, comps=C)
    B = [s.add(Node(f"SWBD {i}", "board", f"ER{i}", attended=True, gens=2)) for i in (1, 2, 3, 4)]
    EF = s.add(Node("EMERG F", "board", "EDF", emergency=True)) if emerg else None
    EA = s.add(Node("EMERG A", "board", "EDA", emergency=True)) if emerg else None
    lk = locked
    IC = s.add(Node("IC/FC bus", "feeder", "ICR", B[0], alt=EF, abt=abt and bool(EF), attended=True,
                    route=["ER1", "ICR"]))
    up = ["ICR", "CT_TUBE"]
    for nm, r, f, v in [("main director", up + ["SUP01", "SUP02", "SKY", "MDIR"], "FC", True),
                        ("sky control", up + ["SUP01", "SUP02", "SKY"], "FC", True),
                        ("sec dir fwd", up + ["SUP01", "SUP02", "SUP03"], "FC5", True),
                        ("sec dir aft", ["ICR", "SEC_S", "SUP04", "SUPA", "ADIR"], "FC5", True),
                        ("FC radars", up + ["SUP01", "SUP02", "SKY", "MDIR"], "FC", True),
                        ("IC bridge", up + ["CT", "PILOT", "BRIDGE"], "IC", True),
                        ("IC aft conn", ["ICR", "SEC_P", "SUPA"], "IC", False),
                        ("search radar", up + ["SUP01", "SUP02", "SUP03", "SKY"], "radar", False),
                        ("plot", ["ICR", "PLOT"], "FC", True),
                        ("gyro", ["ICR"], "IC", True)]:
        s.add(Node(nm, "branch", "ICR", IC, attended=True, route=r, func=f, vital=v))
    for i, side in ((0, "S"), (1, "P")):
        pnl = s.add(Node(f"5in pnl {side}", "feeder", f"ER{i+1}", B[i], attended=True,
                         route=[f"ER{i+1}", f"MAIN_{side}", f"SEC_{side}"]))
        for k in range(4):
            s.add(Node(f"5in mount {side}{k+1}", "branch", f"SEC_{side}", pnl, locked=lk,
                       route=[f"SEC_{side}", f"SUP0{1 + k % 3}"], func="guns5"))
    L = s.add(Node("light pnl sup", "feeder", "ER2", B[1], attended=True, route=["ER2", "MAIN_P", "SUP01"]))
    for c in ["CT", "BRIDGE", "PILOT", "SKY", "MDIR", "SUP01", "SUP02", "SUP03", "SUP04", "SUPA",
              "ADIR", "SEC_P", "SEC_S", "MAIN_P", "MAIN_S"]:
        s.add(Node(f"light {c}", "branch", "SUP01", L, locked=lk, route=["SUP01", c], func="light"))
    s.add(Node("steering", "branch", "STEER", B[3], alt=B[2], abt=False, attended=True,
               route=["ER4", "STEER"], func="steer", vital=True))
    for i in range(4):
        s.add(Node(f"fire pump {i+1}", "branch", f"ER{i+1}", B[i], attended=True,
                   route=[f"ER{i+1}"], func="pumps", vital=True))
    s.filler = {c: 40 for c in C}
    s.filler.update({"CT_TUBE": 80, "SUP01": 60, "SUP02": 50, "SKY": 30, "MDIR": 15, "CT": 40,
                     "BRIDGE": 35, "PILOT": 35, "SUP03": 30, "SUP04": 30, "SUPA": 30, "ADIR": 15})
    return s


# =============================================================================================
# 5. Gunfire: hits -> cuts
# =============================================================================================
PROTECTED = {"CT_TUBE": 406.0, "ICR": 999, "PLOT": 999, "ER1": 999, "ER2": 999, "ER3": 999,
             "ER4": 999}      # mm: cables inside armour are not reached by these hits [SRC layout]


def hit_cuts(ship: Ship, rng, comp, shell, bursts=True, bulkhead_mm=6.0, adjacent=None):
    """Cable cuts from one hit in compartment comp.  Returns [(node, cause, comp)]."""
    ww = ship.wireways()
    out = []
    if not bursts:     # through-shot: the line of flight only
        for n in ww.get(comp, []):
            if rng.random() < 0.10:
                out.append((n, "fragment", comp))
        return out
    rw = wreck_radius(shell)
    places = [(comp, 0.0)] + [(c, bulkhead_mm) for c in (adjacent or [])]
    for c, behind in places:
        if c in PROTECTED or c not in ww:
            continue
        R = rng.uniform(1.0, 4.0) if behind == 0 else rng.uniform(3.0, 7.0)
        if behind == 0 and R < rw:
            out += [(n, "blast", c) for n in ww[c]]
            continue
        ncab = len(ww[c]) + ship.filler.get(c, 20)
        _, p_one, _ = cut_stats(shell, R, ncab, behind)
        for n in ww[c]:
            if rng.random() < p_one:
                out.append((n, "fragment", c))
    return out


def neighbours(ship, comp, span=6.0):
    x, lvl = ship.comps[comp]
    return [c for c, (x2, l2) in ship.comps.items()
            if c != comp and abs(x2 - x) <= span and l2 == lvl]


# =============================================================================================
# 6. Tables
# =============================================================================================

def table_fragility():
    print("\nT1  P(fail) for one event, by severity at the device (NSF units) and grade")
    Ss = [0.03, 0.05, 0.08, 0.12, 0.2, 0.3, 0.5, 0.8]
    print(f"{'item':11}{'grade':17}" + "".join(f"{S:7.2f}" for S in Ss))
    for item in ["breaker", "contactor", "abt", "fuse", "lamp", "generator", "bus_short"]:
        for gi in range(3):
            print(f"{item if gi == 0 else '':11}{GRADES[gi]:17}" +
                  "".join(f"{p_fail(item, gi, S):7.2f}" for S in Ss))


UNDEX_CASES = [
    # name, ship disp t, hull NSF at the boards' space, level, grade, record   [NSF: 15 §3.2-3.3 / EST]
    ("Sheffield, bomb 75 ft (1940)", 9100, 0.05, "hold", 0, "some electrical equipment"),
    ("Quorn, 250 kg at 60 ft", 1050, 0.11, "hold", 0, "four turbine feet broke"),
    ("Montrose, 500 lb at 30 ft", 1500, 0.25, "hold", 0, "electric power lost"),
    ("Fame, 200 lb at 5 ft", 1350, 1.00, "hold", 0, "severe, flooding aft"),
    ("O'Brien, torpedo fwd (1942)", 2300, 0.14, "hold", 1, "one main breaker; reset at once"),
    ("North Carolina, torpedo (1942)", 44000, 0.12, "hold", 1, "no breakers tripped"),
    ("White Plains, 46 cm under bilge", 10900, 0.40, "hold", 0, "most breakers; 3 min"),
    ("White Plains, if G1 secured", 10900, 0.40, "hold", 1, "-"),
    ("White Plains, if G2 tested", 10900, 0.40, "hold", 2, "-"),
]


def table_undex(n=2000):
    print("\nT2  Underwater shock: severity at the machinery boards and what trips (destroyer network,"
          " hull NSF and disp as given)")
    print(f"{'case':34}{'S dev':>6}{'P(br)':>7}{'share':>7}{'P(any)':>7}{'P(vit)':>8}"
          f"{'vital min':>11}{'P(gen)':>7}  record")
    for name, disp, h, lvl, grade, rec in UNDEX_CASES:
        rng = random.Random(7)
        S_dev = undex_S(h, lvl, disp)
        tr = an = vit = gen = 0
        vt = []
        for _ in range(n):
            s = destroyer(grade=grade)
            s.disp = disp
            shock = {c: undex_S(h, lv, disp) for c, (x, lv) in s.comps.items()}
            o = evaluate(s, rng, shock=shock)
            tr += o.trips / max(1, o.devices)
            an += o.trips > 0
            dark = [o.out[l.name] for l in s.leaves() if l.vital and o.out[l.name] > 0]
            vit += bool(dark)
            if dark:
                vt.append(max(dark))
        vt.sort()
        med = vt[len(vt) // 2] if vt else 0.0
        p90 = vt[int(len(vt) * 0.9)] if vt else 0.0
        print(f"{name:34}{S_dev:6.3f}{p_fail('breaker', grade, S_dev):7.2f}{tr/n:7.2f}{an/n:7.2f}"
              f"{vit/n:8.2f}{med:6.1f}/{p90:<4.1f}{p_fail('generator', grade, S_dev):7.2f}  {rec}")


def table_torpedo(n=2000):
    print("\nT2b A torpedo on the battleship network: 400 kg TNT at 4 m depth against the side, by where it"
          " strikes (S at each space from its own distance)")
    print(f"{'hit at x (m from bow)':24}{'grade':17}{'S @SWBD1':>9}{'P(any)':>7}{'share':>7}{'P(vit)':>7}"
          f"{'vital min':>11}")
    for x_hit in (45, 100):
        for g in range(3):
            rng = random.Random(9)
            an = sh = vit = 0
            vt = []
            for _ in range(n):
                s = battleship(grade=g)
                shock = {}
                for c, (x, lv) in s.comps.items():
                    R = sqrt((x - x_hit) ** 2 + 4.0 ** 2 + 8.0 ** 2)      # 8 m in from the side [EST]
                    shock[c] = undex_S(nsf(400, R, angle_deg=20), lv, s.disp)
                o = evaluate(s, rng, shock=shock)
                an += o.trips > 0
                sh += o.trips / max(1, o.devices)
                dark = [o.out[l.name] for l in s.leaves() if l.vital and o.out[l.name] > 0]
                if dark:
                    vit += 1
                    vt.append(max(dark))
            vt.sort()
            med = vt[len(vt) // 2] if vt else 0
            p90 = vt[int(len(vt) * .9)] if vt else 0
            R1 = sqrt((95 - x_hit) ** 2 + 16 + 64)
            S1 = undex_S(nsf(400, R1, 20), "hold", 44000)
            print(f"{x_hit:<24}{GRADES[g]:17}{S1:9.3f}{an/n:7.2f}{sh/n:7.2f}{vit/n:7.2f}{med:6.1f}/{p90:<4.1f}")
    print("  record: North Carolina (G1, hit forward of turret I): no breaker tripped, no motor started or"
          " stopped (WDR 61)")


def table_cuts():
    print("\nT3  Cable cuts from one burst: P(a given cable cut) in a single run / a 24-cable wireway,"
          " open and behind 6 mm")
    shells = ["5in/38 AAC Mk 35", "6in/47 HC Mk 34", "IJN 20cm common", "IJN 20cm T91 AP",
              "14in HC", "16in AP Mk 8"]
    print(f"{'shell':20}{'wreck r':>8}" + "".join(f"{'R='+str(R):>16}" for R in (2, 4, 8, 15)))
    print(f"{'':28}" + "".join(f"{'1 / 24 / 24@6mm':>16}" for _ in range(4)))
    for sh in shells:
        row = ""
        for R in (2, 4, 8, 15):
            p1 = cut_stats(sh, R, 1)[1]
            p24 = cut_stats(sh, R, 24)[1]
            p24b = cut_stats(sh, R, 24, 6.0)[1]
            row += f"{p1:6.2f}{p24:5.2f}{p24b:5.2f}"
        print(f"{sh:20}{wreck_radius(sh):7.1f}m{row}")
    print("    cables cut per fragment that hits a 24-cable tray (KE / 500 J, capped by layers):")
    for sh in shells:
        w, layers = tray(24)
        h, ke = wireway_hits(sh, 4, w)
        print(f"    {sh:20} mean KE {ke:7.0f} J -> {min(24, LATERAL * min(layers, max(1, ke / E_CABLE))):.1f}"
              f" cables;  hits on the tray at 4 m {h:5.1f}")


# Hit distribution over the battleship: (comp, share, bursts) [EST, after SD's 26 hits:
# superstructure-heavy, many through-shots]
BB_HITS = [("SUP01", .10, .6), ("SUP02", .10, .6), ("SUP03", .08, .6), ("SUP04", .07, .6),
           ("SUPA", .06, .6), ("SKY", .05, .5), ("MDIR", .03, .4), ("BRIDGE", .05, .6),
           ("PILOT", .03, .6), ("CT", .03, .0), ("ADIR", .03, .4), ("SEC_P", .04, .7),
           ("SEC_S", .04, .7), ("MAIN_P", .03, .7), ("MAIN_S", .03, .7),
           (None, .23, 0)]                # turrets, barbettes, belt, deck: no cable effect here
SD_MIX = [("14in HC", 1), ("IJN 20cm common", 6), ("6in/47 HC Mk 34", 8), ("5in/38 AAC Mk 35", 11)]


def pick(rng, table):
    r, acc = rng.random() * sum(t[1] for t in table), 0.0
    for t in table:
        acc += t[1]
        if r <= acc:
            return t
    return table[-1]


def gunfire(ship_fn, n_hits_mix, n=600, seed=11, **kw):
    """Hits spread over the battleship per BB_HITS; returns aggregate stats."""
    rng = random.Random(seed)
    res = dict(cuts=0.0, shorts=0.0, cascades=0.0, gen=0.0, fires=0.0, p_bus=0.0, bus_min=[], fc_lost=0.0,
               light_hits=0.0, hits=0.0, fc_dark=[], kinds=0.0)
    for _ in range(n):
        s = ship_fn(**kw)
        cuts = []
        light_hit = 0
        for shell, k in n_hits_mix:
            for _ in range(k):
                comp, _, pb = pick(rng, BB_HITS)
                res["hits"] += 1
                if comp is None:
                    continue
                c = hit_cuts(s, rng, comp, shell, bursts=rng.random() < pb,
                             adjacent=neighbours(s, comp))
                if any(nd.func == "light" for nd, _, _ in c):
                    light_hit += 1
                cuts += c
        o = evaluate(s, rng, cuts=cuts)
        res["cuts"] += len(o.cut)
        res["kinds"] += len({x.split()[0] for x in o.cut})
        res["shorts"] += o.shorts
        res["cascades"] += o.cascades
        res["gen"] += o.gen_clears
        res["fires"] += o.fires
        res["light_hits"] += light_hit
        bus_leaves = [l for l in s.leaves() if l.parent and l.parent.name == "IC/FC bus"]
        uncut = [l for l in bus_leaves if l.name not in o.cut]
        bus = min((o.out[l.name] for l in uncut), default=0.0)
        if bus > 0.2:
            res["p_bus"] += 1
            res["bus_min"].append(bus)
        if {"main director", "FC radars"} & o.cut:
            res["fc_lost"] += 1
    for k in ("cuts", "shorts", "cascades", "gen", "fires", "p_bus", "fc_lost", "light_hits", "kinds"):
        res[k] /= n
    res["hits"] /= n
    return res


def table_gunfire():
    print("\nT4  South Dakota's night: 26 medium/heavy HE hits, mostly superstructure (battleship network)")
    print(f"{'variant':40}{'cut':>5}{'short':>6}{'casc':>6}{'gen':>5}{'fires':>6}{'P(bus)':>7}{'bus min':>9}"
          f"{'P(MD cut)':>11}")
    for label, kw in [("1942: basic selectivity, panels locked in", dict(sel="basic", locked=True)),
                      ("1942, panels not locked", dict(sel="basic", locked=False)),
                      ("1943+: PQ relays (selective)", dict(sel="selective", locked=True)),
                      ("selective, not locked", dict(sel="selective", locked=False))]:
        r = gunfire(battleship, SD_MIX, **kw)
        bm = sorted(r["bus_min"])
        med = bm[len(bm) // 2] if bm else 0
        print(f"{label:40}{r['cuts']:5.1f}{r['shorts']:6.1f}{r['cascades']:6.2f}{r['gen']:5.2f}{r['fires']:6.2f}"
              f"{r['p_bus']:7.2f}{med:9.1f}{r['fc_lost']:11.2f}")
    print("  record (WDR 57): ~35 kinds of circuit repaired; one FC/IC-wide loss of ~3 min; FC fought on")


def table_sf():
    print("\nT5  San Francisco: share of hits that cut some lighting circuit (record 19 of ~45 = 0.42)")
    mix = [("IJN 20cm common", 10), ("6in/47 HC Mk 34", 15), ("5in/38 AAC Mk 35", 18), ("14in HC", 2)]
    r = gunfire(battleship, mix, n=300, seed=5)
    print(f"  model: {r['light_hits'] / r['hits']:.2f} of {r['hits']:.0f} hits   (23% of hits land on "
          f"armour/turrets/hull with no cable effect)")


def table_talbot(n=3000):
    print("\nT6  Ralph Talbot: one hit bursting in the chart house / director trunk of a destroyer")
    print(f"{'shell':20}{'comp':8}{'P(FC lost)':>11}{'P(IC br.)':>10}{'P(radar)':>9}{'P(torp)':>8}"
          f"{'E cuts':>7}")
    for sh in ["5in/38 AAC Mk 35", "IJN 20cm common", "IJN 20cm T91 AP"]:
        for comp in ["CHART", "TRUNK"]:
            rng = random.Random(3)
            fc = ic = rd = tp = cuts = 0
            for _ in range(n):
                s = destroyer()
                c = hit_cuts(s, rng, comp, sh, adjacent=neighbours(s, comp, 4))
                names = {nd.name for nd, _, _ in c}
                fc += bool({"director", "FC radar"} & names)
                ic += "IC bridge" in names
                rd += "search radar" in names
                tp += "torpedo dir" in names
                cuts += len(c)
            print(f"{sh:20}{comp:8}{fc/n:11.2f}{ic/n:10.2f}{rd/n:9.2f}{tp/n:8.2f}{cuts/n:7.1f}")
    print("  record: IC to bridge and directors, director-trunk power, torpedo director, SC/FD radar"
          " all cut (WDR 51)")


def table_knobs(n=2000):
    print("\nT7  Design knobs, destroyer under a near miss at hull NSF 0.20 (G0 and G1 fittings)")
    print(f"{'variant':44}{'P(vital dark)':>14}{'median':>8}{'p90 min':>8}{'pumps >3 min':>14}")
    cases = [("G0, emergency diesels, LVR pumps", dict(grade=0)),
             ("G0, no emergency diesels", dict(grade=0, emergency=False)),
             ("G0, LVP pumps (manual restart)", dict(grade=0, lvp_pumps=True)),
             ("G1, emergency diesels, LVR pumps", dict(grade=1)),
             ("G1, panels not locked in", dict(grade=1, locked=False)),
             ("G1, manual transfer instead of ABTs", dict(grade=1, abt=False)),
             ("G0, manual transfer instead of ABTs", dict(grade=0, abt=False)),
             ("G2 shock-tested", dict(grade=2))]
    for label, kw in cases:
        rng = random.Random(21)
        vit = pumps = 0
        vt = []
        for _ in range(n):
            s = destroyer(**kw)
            shock = {c: undex_S(0.20, lv, s.disp) for c, (x, lv) in s.comps.items()}
            o = evaluate(s, rng, shock=shock)
            dark = [o.out[l.name] for l in s.leaves() if l.vital and o.out[l.name] > 0]
            if dark:
                vit += 1
                vt.append(max(dark))
            pl = [l for l in s.leaves() if l.func == "pumps"]
            pumps += sum(o.out[l.name] > 3 for l in pl) / len(pl)
        vt.sort()
        med = vt[len(vt) // 2] if vt else 0
        p90 = vt[int(len(vt) * .9)] if vt else 0
        print(f"{label:44}{vit/n:14.2f}{med:8.1f}{p90:8.1f}{pumps/n:14.2f}")


def table_own_guns():
    print("\nT8  Own guns (13 §3.4, kept): per 20 min of sustained main-battery fire, P(breaker/ABT event)")
    print("    G0 0.05 (South Dakota's ABT, 1942), G2 0.02.  Not a shock-factor event: no hull NSF.")


if __name__ == "__main__":
    table_fragility()
    table_undex()
    table_torpedo()
    table_cuts()
    table_talbot()
    table_gunfire()
    table_sf()
    table_knobs()
    table_own_guns()
