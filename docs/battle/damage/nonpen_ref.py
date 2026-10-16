"""nonpen_ref.py -- reference for 13_nonpenetrating_hits.md (Fleetwright damage research).

What a heavy hit that does NOT get through the armour still does:
  1. turrets and barbettes: jammed in train, slowed, one gun out, sights out, crew stunned (sec. 1 of the note);
  2. the belt at the waterline: plates pushed in, seams and rivets opened, the cell behind flooding (sec. 2);
  3. shock: breakers, lights, gyros, rangefinders, radar, cast iron, crew (sec. 3);
and, at the end, what those add up to over a battle, against what penetrations do (sec. 4).

Pure Python, no dependencies. `python nonpen_ref.py` prints every table quoted in the note (seeded, ~1 s).
Conventions as filler_ref.py: r = plate thickness / the shell's penetration there, so r > 1 is a non-penetration
and r close to 1 is a near thing. Impulse I = shell mass x striking speed (N s).
Tags in comments: [SRC] sourced, [FIT] fitted here to the cases, [EST] judgement, [UNCERTAIN] thin sources.
"""
import math
import random
from dataclasses import dataclass

I_REF = 4.0e5  # N s: a 15" APC (871 kg) at ~460 m/s, the "heavy hit" the case rates are mostly from     [SRC-ish]


def size_factor(impulse: float) -> float:
    """How much of the heavy-hit rate a smaller or bigger shell gets. 8" (~0.6e5) -> ~0.4; 16" Mk 8 -> 1.0."""
    return min(1.15, (impulse / I_REF) ** 0.5)                                                         # [FIT]


def margin_factor(r: float) -> float:
    """A shell that nearly got through (r ~ 1) dislodges plate and throws spall; one far short (r ~ 3) only
    dents. 1.0 at r <= 1, 0.7 at r = 2, floor 0.4 from r = 3."""
    return max(0.4, min(1.0, 1.3 - 0.3 * r))                                                           # [FIT]


# ----------------------------------------------------------------------------------------------------------
# 1. Turrets and barbettes
# ----------------------------------------------------------------------------------------------------------
# Per location, for a heavy (I ~ I_REF) AP hit with r ~ 1.3, the shares of outcomes. Fitted to ~35 non-pen /
# partial cases (13 sec. 1.1); the War Damage Reports and Luetzow's hit list (nearly complete samples) give
# ~27% train jams per recorded non-pen turret/barbette hit; narratives ~50%; reporting bias ~1.5x.          [FIT]
#   jam    = jammed in train (holds its bearing)
#   stiff  = trains, but slowly (South Dakota III: "some difficulty training")
#   gun    = one gun out (cradle, slide, breech, gun-port splinters) or elevation limited (roof driven in)
#   sights = turret sights / rangefinder / hood / periscope out (turret goes to director or degraded)
#   stun   = crew stunned: the turret misses 1-3 salvoes (10 s - 2 min)
TURRET_LOC = {
    #                   jam   stiff  gun   sights stun   (remainder: no effect beyond the gouge)
    "barbette_exposed": (0.38, 0.06, 0.05, 0.03, 0.10),  # VdT A, NZ X, PR X, Boise I, Vincennes I, Peresvet
    "barbette_deck":    (0.22, 0.10, 0.03, 0.00, 0.05),  # SD III (stiff), Jean Bart (glacis), VdT C (deck)
    "face":             (0.14, 0.04, 0.33, 0.08, 0.15),  # Quincy III, Bismarck C; Luetzow A/B, Seydlitz wing
    "side":             (0.12, 0.04, 0.12, 0.12, 0.08),  # few cases; glancing hits do little
    "roof":             (0.08, 0.02, 0.22, 0.30, 0.15),  # Tiger Q x2, Lion A, Oryol (elevation), Gneisenau
}
# Where on a turret/barbette a heavy hit lands (projected-area guess for a side-on 1915-45 capital ship)     [EST]
TURRET_HIT_SHARE = {"barbette_exposed": 0.25, "barbette_deck": 0.15, "face": 0.25, "side": 0.20, "roof": 0.15}

# Mechanism -> outage. A jam's cause decides its length (13 sec. 1.3).                                     [FIT]
#   debris : spall / a loose piece / splinters in the gap, roller path or rack: crew clears it
#   deck   : deck, glacis or wreckage pushed into the turret: cut free with oxy-acetylene
#   ring   : barbette ring, roller path or training gear distorted/broken: dockyard
JAM_CAUSE = {  # share by location
    "barbette_exposed": {"debris": 0.45, "deck": 0.10, "ring": 0.45},
    "barbette_deck":    {"debris": 0.20, "deck": 0.55, "ring": 0.25},
    "face":             {"debris": 0.35, "deck": 0.05, "ring": 0.60},  # plate pushed back, shock to gear
    "side":             {"debris": 0.50, "deck": 0.00, "ring": 0.50},
    "roof":             {"debris": 0.60, "deck": 0.00, "ring": 0.40},  # Tiger Q at Dogger Bank
}
JAM_MINUTES = {  # (low, high) uniform in log space; None = not at sea
    "debris": (3, 40),     # NZ "brief", Tiger X 7 min, Seydlitz 3 min, Tiger Q right cage "a few minutes"
    "deck":   (60, 600),   # VdT C ~3.5 h, Jean Bart 9 h 18 min
    "ring":   None,        # VdT A, PR X, Boise I, Scharnhorst A, Bismarck C, Ajax Y, Peresvet: rest of battle
}
GUN_OUT_MINUTES = (30, 240)    # Lion A left gun 2 h; often rest of battle (counted as a share below)       [EST]
GUN_OUT_PERMANENT = 0.5        # Luetzow B right gun, Lion A? -- half of one-gun losses last the battle     [FIT]
STIFF_TRAIN_RATE = 0.4         # share of normal training speed while stiff (hand / part power)             [EST]
STUN_SECONDS = (10, 120)       # New Zealand X officer, Achilles DCT, Luetzow A "rocked"                    [SRC-ish]

HE_ON_TURRET = {"jam": 0.6, "stiff": 0.8, "gun": 0.8, "sights": 1.5, "stun": 1.2}  # burst on the plate: less
# push, more splinters into hoods and ports (San Francisco II: two 14" HE, no jam)                         [EST]


@dataclass
class TurretEffect:
    kind: str           # 'jam' | 'stiff' | 'gun' | 'sights' | 'stun' | 'none'
    minutes: float      # outage; math.inf = rest of the battle / dockyard
    cause: str = ""


def log_uniform(rng, lo, hi):
    return math.exp(rng.uniform(math.log(lo), math.log(hi)))


def turret_nonpen(rng, loc: str, impulse: float, r: float, he: bool = False) -> TurretEffect:
    k = size_factor(impulse) * margin_factor(r)
    p = dict(zip(("jam", "stiff", "gun", "sights", "stun"), TURRET_LOC[loc]))
    for key in p:
        # stun scales gently with size (the whole turret rings whatever the shell); the others with k
        p[key] *= (0.5 + 0.5 * k) if key == "stun" else k
        if he:
            p[key] *= HE_ON_TURRET[key]
    x = rng.random()
    for kind, pk in p.items():
        if x < pk:
            break
        x -= pk
    else:
        return TurretEffect("none", 0.0)
    if kind == "jam":
        causes = JAM_CAUSE[loc]
        cause = rng.choices(list(causes), weights=list(causes.values()))[0]
        span = JAM_MINUTES[cause]
        return TurretEffect("jam", math.inf if span is None else log_uniform(rng, *span), cause)
    if kind == "stiff":
        return TurretEffect("stiff", math.inf if rng.random() < 0.5 else log_uniform(rng, 20, 180))
    if kind == "gun":
        return TurretEffect("gun", math.inf if rng.random() < GUN_OUT_PERMANENT else log_uniform(rng, *GUN_OUT_MINUTES))
    if kind == "sights":
        return TurretEffect("sights", math.inf)
    return TurretEffect("stun", rng.uniform(*STUN_SECONDS) / 60.0)


def turret_table(rng, n=20000):
    rows = []
    cases = [("8in AP (125 kg, 500 m/s) on 12in", 125 * 500, 1.8),
             ("12in AP (390 kg, 480 m/s), r=1.3", 390 * 480, 1.3),
             ("15in APC (871 kg, 460 m/s), r=1.3", 871 * 460, 1.3),
             ("15in APC, near thing r=1.05", 871 * 460, 1.05),
             ("16in AP (1225 kg, 480 m/s) on 17in, r=1.6", 1225 * 480, 1.6),
             ("14in HE (bombardment) on 5in barbette", 680 * 500, 1.0)]
    for name, imp, r in cases:
        he = "HE" in name
        tally = {"jam": 0, "stiff": 0, "gun": 0, "sights": 0, "stun": 0, "none": 0}
        long_jam = 0
        for _ in range(n):
            loc = rng.choices(list(TURRET_HIT_SHARE), weights=list(TURRET_HIT_SHARE.values()))[0]
            e = turret_nonpen(rng, loc, imp, r, he)
            tally[e.kind] += 1
            long_jam += e.kind == "jam" and e.minutes > 240
        rows.append((name, {k: v / n for k, v in tally.items()}, long_jam / n))
    return rows


def jam_duration_table(rng, n=40000):
    """Given a jam, how long (all turret locations, heavy AP)."""
    bins = {"< 15 min": 0, "15 min - 4 h": 0, "4 - 12 h": 0, "rest of battle / dockyard": 0}
    jams = 0
    while jams < n:
        loc = rng.choices(list(TURRET_HIT_SHARE), weights=list(TURRET_HIT_SHARE.values()))[0]
        e = turret_nonpen(rng, loc, I_REF, 1.3)
        if e.kind != "jam":
            continue
        jams += 1
        m = e.minutes
        key = ("< 15 min" if m < 15 else "15 min - 4 h" if m < 240 else "4 - 12 h" if m < 720
               else "rest of battle / dockyard")
        bins[key] += 1
    return {k: v / n for k, v in bins.items()}


# ----------------------------------------------------------------------------------------------------------
# 2. The belt at the waterline: seams, rivets, plates pushed in
# ----------------------------------------------------------------------------------------------------------
G = 9.81
CD = 0.6                       # ragged orifice                                                          [SRC]


def orifice_tph(area_m2: float, head_m: float) -> float:
    """Inflow, t/h, through `area_m2` with `head_m` of water above it (sea water ~1.025 t/m3)."""
    return CD * area_m2 * math.sqrt(2 * G * max(0.0, head_m)) * 3600 * 1.025


# P(any leak) for a heavy non-pen hit, by where on the belt it strikes (13 sec. 2.2).                     [FIT/EST]
BELT_ZONE_PLEAK = {"wl": 0.50, "upper": 0.25, "lower_edge": 0.80}
JOINT_SHARE = 0.25             # hits within ~0.3 m of a plate butt or seam                                [EST]
JOINT_MULT = 1.5               # Markgraf 18:10 (400 t), Yamato's belt joint                              [FIT]
REPEAT_MULT = 2.0              # a second hit within ~3 m of a loosened plate (Oslyabya, Sevastopol)       [FIT]
LOOSENED_T = 0.85              # a loosened plate defends like 85% of itself afterwards                   [EST]

# Regime given a leak: (share, area low, area high in m^2, DC: (factor after work, minutes of work))       [FIT]
#   areas are the opening into the cell behind the belt (bunker, wing void, tank), uniform in log space.
LEAK_REGIME = {
    #           share  A_lo     A_hi     DC factor, minutes
    "seep":     (0.55, 2.0e-4, 2.0e-3, (0.15, (10, 40))),   # rivets, bolts, hairline seam: Moltke's neighbour,
                                                            # Sevastopol, NC "50 gal/h", SoDak hit 11 rivets
    "moderate": (0.30, 3.0e-3, 2.5e-2, (0.30, (20, 60))),   # plate dished 5-20 cm: Moltke fwd, Markgraf 14in
    "major":    (0.12, 5.0e-2, 2.0e-1, (0.50, (30, 90))),   # pushed in >= 30 cm / joint: Grosser Kurfuerst, Lion
    "lost":     (0.03, 0.5,    2.0,    (0.70, (60, 240))),  # plate fell off: Seydlitz, Derfflinger x2, Moltke aft
}
# A leak reopens under way: wedges and mats work loose (Grosser Kurfuerst overnight, Bismarck's mat, Seydlitz).
REOPEN_KN = 15.0               # above this, a worked leak goes back toward its full area                 [EST]
REOPEN_PER_10MIN = 0.15        # chance per 10 min above REOPEN_KN                                        [EST]
CONTAMINATION_P = 0.15         # cell behind is a feed or oil tank: a boiler/engine falters (Lion, Malaya) [FIT]


def belt_leak(rng, zone: str, impulse: float, r: float, joint: bool, repeat: bool, welded: bool = False):
    """Returns (regime, area_m2) or (None, 0.0)."""
    k = size_factor(impulse) * margin_factor(r)
    p = BELT_ZONE_PLEAK[zone] / (1 + JOINT_SHARE * (JOINT_MULT - 1)) * k
    if joint:
        p *= JOINT_MULT
    if repeat:
        p *= REPEAT_MULT
    if welded:
        p *= 0.6              # late-war welded skin: fewer sheared rivets (NC: "relative weakness of riveted seams")
    if rng.random() >= min(0.95, p):
        return None, 0.0
    # a near thing or a joint shifts the regime toward the bad end
    weights = []
    for name, (share, *_rest) in LEAK_REGIME.items():
        w = share
        if name in ("major", "lost"):
            w *= (0.5 + k) * (1.6 if joint else 1.0) * (1.5 if repeat else 1.0)
        weights.append(w)
    name = rng.choices(list(LEAK_REGIME), weights=weights)[0]
    _, lo, hi, _dc = LEAK_REGIME[name]
    return name, log_uniform(rng, lo, hi)


def leak_flow_table():
    rows = []
    for name, (share, lo, hi, (dcf, mins)) in LEAK_REGIME.items():
        mid = math.sqrt(lo * hi)
        rows.append((name, share, lo, hi,
                     [orifice_tph(a, h) for a in (lo, mid, hi) for h in (2.0,)],
                     orifice_tph(mid, 1.0), orifice_tph(mid, 3.0), dcf, mins))
    return rows


def belt_leak_mc(rng, n=20000, impulse=I_REF, r=1.3):
    """Per heavy non-pen hit within +-1 m of the waterline: leak chance, regime shares, and the water a leak
    puts in before the cell behind fills or DC wins (cell 150-600 t, head 1.5-3 m, ship at 20 kn then 12 kn)."""
    out = {"none": 0, "seep": 0, "moderate": 0, "major": 0, "lost": 0}
    water = []
    for _ in range(n):
        joint = rng.random() < JOINT_SHARE
        reg, area = belt_leak(rng, "wl", impulse, r, joint, repeat=False)
        out[reg or "none"] += 1
        if reg is None:
            continue
        cell_t = rng.uniform(150, 600)
        head = rng.uniform(1.5, 3.0)
        dcf, (m_lo, m_hi) = LEAK_REGIME[reg][3]
        t_dc = rng.uniform(m_lo, m_hi)
        w = 0.0
        a_now = area
        worked = False
        for minute in range(240):
            # inflow falls as the cell fills (head inside rises): crude sqrt of the remaining fraction
            frac_left = max(0.0, 1 - w / cell_t)
            w += orifice_tph(a_now, head * frac_left) / 60.0
            if w >= cell_t:
                w = cell_t
                break
            if not worked and minute >= t_dc:
                a_now, worked = area * dcf, True
            speed = 20.0 if minute < 60 else 12.0
            if worked and speed > REOPEN_KN and minute % 10 == 0 and rng.random() < REOPEN_PER_10MIN:
                a_now, worked = area, False
                t_dc = minute + rng.uniform(m_lo, m_hi)
        water.append(w)
    shares = {k: v / n for k, v in out.items()}
    water.sort()
    mean = sum(water) / len(water) if water else 0.0
    return shares, mean, water[len(water) // 2] if water else 0.0, water[int(0.9 * len(water))] if water else 0.0


# ----------------------------------------------------------------------------------------------------------
# 3. Shock
# ----------------------------------------------------------------------------------------------------------
# Delta-v = I / participating mass: whole 45 kt ship ~0.01 m/s; a 1,500 t turret ~0.3 m/s; a 300 t conning
# tower ~1.5 m/s; struck plate and its bay (50-150 t) 3-10 m/s. So the shock is severe in the struck structure
# (zone A), much weaker one bulkhead or deck away (B), and only reaches the rest of the ship (C) through
# electrical cascades and ship-wide instruments (gyro).                                                    [EST]
PARTICIPATING_T = {"plate bay": 100, "conning tower": 300, "turret": 1500, "section (20 m)": 4000,
                   "whole ship (45 kt)": 45000}

ERAS = ("pre1914", "ww1", "ww2_early", "ww2_hardened")
# Per heavy non-pen hit (I = I_REF) on belt, barbette, turret or CT; scaled by (I/I_REF)^0.7.             [EST]
#   zone: 'A' struck structure / compartment group; 'B' adjacent (15-40 m, next deck); 'C' ship-wide
SHOCK = {
    #  effect                    zone  pre1914 ww1   ww2e  ww2h   outage (see SHOCK_OUTAGE)
    "local trip / lamps":       ("A", (0.30, 0.40, 0.50, 0.25)),
    "section power or lights":  ("B", (0.03, 0.08, 0.15, 0.06)),
    "ship-wide power cascade":  ("C", (0.00, 0.005, 0.02, 0.008)),  # SD: 1 from hits, 1 from own blast
    "gyrocompass upset":        ("C", (0.00, 0.03, 0.05, 0.03)),
    "director/stable vertical": ("B", (0.00, 0.08, 0.12, 0.06)),
    "rangefinder misaligned":   ("A", (0.15, 0.20, 0.20, 0.12)),
    "radar set fails":          ("B", (0.00, 0.00, 0.25, 0.12)),
    "phones / FC circuits":     ("A", (0.25, 0.30, 0.25, 0.15)),
    "cast iron / gauges / pipe": ("A", (0.20, 0.20, 0.20, 0.06)),
}
# minutes: list of (share, lo, hi); hi = inf for "until port"
SHOCK_OUTAGE = {
    "local trip / lamps":       [(0.9, 0.2, 3), (0.1, 3, 15)],         # Sculpin 10 s; battle lanterns at once
    "section power or lights":  [(0.8, 1, 5), (0.2, 5, 20)],
    "ship-wide power cascade":  [(0.9, 1, 10), (0.1, 30, 90)],         # SD 1 min / 3 min; welded contacts
    "gyrocompass upset":        [(0.8, 1, 2), (0.2, 60, 240)],         # standby / magnetic at once; master 1-4 h
    "director/stable vertical": [(0.7, 5, 30), (0.3, 30, 60)],         # re-erect, spin-up (tech-074)
    "rangefinder misaligned":   [(0.5, 5, 20), (0.3, 120, 240), (0.2, math.inf, math.inf)],  # re-collimate / degraded / prism
    "radar set fails":          [(0.6, 5, 20), (0.25, 60, 240), (0.15, math.inf, math.inf)],  # tubes; aerial
    "phones / FC circuits":     [(1.0, math.inf, math.inf)],           # rest of action: messengers, sound-powered
    "cast iron / gauges / pipe": [(0.8, 1, 10), (0.2, math.inf, math.inf)],  # isolate; a cracked foot = days
}
CREW_STUN_P = {"turret": 0.6, "conning tower": 0.7}   # 10 s - 2 min; 10% a key man out 5+ min             [FIT]
# Own guns, per 20 min of sustained main-battery fire (Bismarck FuMO 23, KGV 284?, South Dakota's ABT)
OWN_GUN = {"radar fails": (0.0, 0.0, 0.20, 0.07), "breaker/ABT event": (0.0, 0.02, 0.05, 0.02)}     # [EST]


def shock_delta_v(impulse: float):
    return {k: impulse / (t * 1000.0) for k, t in PARTICIPATING_T.items()}


def shock_events(rng, era: str, impulse: float):
    ei = ERAS.index(era)
    s = min(1.3, (impulse / I_REF) ** 0.7)
    ev = []
    for name, (zone, ps) in SHOCK.items():
        if rng.random() < ps[ei] * s:
            share = [x[0] for x in SHOCK_OUTAGE[name]]
            lo, hi = rng.choices([(x[1], x[2]) for x in SHOCK_OUTAGE[name]], weights=share)[0]
            ev.append((name, zone, math.inf if math.isinf(hi) else log_uniform(rng, lo, hi)))
    return ev


# ----------------------------------------------------------------------------------------------------------
# 4. A battle: 100 heavy hits on a capital ship, what non-penetrations add up to
# ----------------------------------------------------------------------------------------------------------
# Where 100 heavy hits land on a 1915-45 capital ship at 12-18 km, and how many get through there.         [EST]
# (08 sec. 6.3 shares; Bismarck ~3 belt holes in ~80 heavy hits; Jutland 1 in 17; forum 22% on turrets.)
HIT_SHARE = {          # share of hits, P(penetrates)
    "turret":       (0.10, 0.15),
    "barbette":     (0.06, 0.20),
    "conning tower": (0.01, 0.15),
    "belt_wl":      (0.08, 0.08),
    "belt_upper":   (0.07, 0.12),
    "belt_lower":   (0.03, 0.10),
    "deck_armour":  (0.10, 0.20),
    "unarmoured":   (0.55, 1.00),   # superstructure, ends, funnels: not this note's business
}
BATTLE_MIN = 120.0
N_TURRETS = 4


def battle_mc(rng, era="ww1", n_battles=4000, n_hits=100):
    tot = {"turret-min lost (non-pen)": 0.0, "turret-min lost (pen)": 0.0, "train jams": 0.0,
           "turret hits non-pen": 0.0, "turret hits pen": 0.0,
           "belt leaks": 0.0, "major/lost leaks": 0.0, "shock power events": 0.0,
           "cascade (ship-wide)": 0.0, "rangefinder/director/radar out": 0.0, "crew stuns": 0.0}
    any_cascade = 0
    for _ in range(n_battles):
        turret_out = [0.0] * N_TURRETS      # minutes lost per turret, capped at the battle
        cascade = False
        for _h in range(n_hits):
            t_hit = rng.uniform(0, BATTLE_MIN)
            left = BATTLE_MIN - t_hit
            loc = rng.choices(list(HIT_SHARE), weights=[v[0] for v in HIT_SHARE.values()])[0]
            pen = rng.random() < HIT_SHARE[loc][1]
            if loc in ("turret", "barbette"):
                ti = rng.randrange(N_TURRETS)
                if pen:
                    tot["turret hits pen"] += 1
                    loss = left if rng.random() < 0.9 else rng.uniform(3, 30)    # 90% out for good (13 sec 1.1)
                    turret_out[ti] += loss
                    tot["turret-min lost (pen)"] += min(loss, left)
                else:
                    tot["turret hits non-pen"] += 1
                    sub = (rng.choices(["face", "side", "roof"], weights=[0.25, 0.2, 0.15])[0] if loc == "turret"
                           else rng.choices(["barbette_exposed", "barbette_deck"], weights=[0.25, 0.15])[0])
                    e = turret_nonpen(rng, sub, I_REF, rng.uniform(1.0, 2.0))
                    weight = {"jam": 1.0, "stiff": 0.3, "gun": 0.5, "sights": 0.25, "stun": 1.0}.get(e.kind, 0.0)
                    loss = min(e.minutes, left) * weight
                    tot["turret-min lost (non-pen)"] += loss
                    tot["train jams"] += e.kind == "jam"
                    tot["crew stuns"] += e.kind == "stun"
            if loc.startswith("belt") and not pen:
                zone = {"belt_wl": "wl", "belt_upper": "upper", "belt_lower": "lower_edge"}[loc]
                reg, _a = belt_leak(rng, zone, I_REF, rng.uniform(1.0, 2.0), rng.random() < JOINT_SHARE,
                                    rng.random() < 0.1)
                if reg:
                    tot["belt leaks"] += 1
                    tot["major/lost leaks"] += reg in ("major", "lost")
            if loc in ("turret", "barbette", "conning tower") or loc.startswith("belt"):
                if not pen:
                    for name, _z, _m in shock_events(rng, era, I_REF):
                        if "power" in name or "trip" in name:
                            tot["shock power events"] += 1
                        if name == "ship-wide power cascade":
                            tot["cascade (ship-wide)"] += 1
                            cascade = True
                        if name in ("rangefinder misaligned", "director/stable vertical", "radar set fails"):
                            tot["rangefinder/director/radar out"] += 1
        any_cascade += cascade
    res = {k: v / n_battles for k, v in tot.items()}
    res["P(>=1 ship-wide cascade)"] = any_cascade / n_battles
    return res


# ----------------------------------------------------------------------------------------------------------
def main():
    rng = random.Random(13)
    print("== 1. Turret / barbette non-pen hits: outcome shares (all turret locations mixed) ==")
    print(f"{'case':44s} {'jam':>5s} {'stiff':>5s} {'gun':>5s} {'sight':>5s} {'stun':>5s} {'none':>5s}  jam>4h")
    for name, t, lj in turret_table(rng):
        print(f"{name:44s} {t['jam']:5.2f} {t['stiff']:5.2f} {t['gun']:5.2f} {t['sights']:5.2f} {t['stun']:5.2f}"
              f" {t['none']:5.2f}  {lj:5.2f}")
    print("\n   by location, heavy AP r=1.3 (jam / any loss):")
    for loc in TURRET_LOC:
        k = size_factor(I_REF) * margin_factor(1.3)
        jam = TURRET_LOC[loc][0] * k
        anyl = sum(TURRET_LOC[loc][:4]) * k
        print(f"   {loc:18s} jam {jam:.2f}   any lasting loss {anyl:.2f}")
    print("\n   given a jam, how long:", {k: round(v, 2) for k, v in jam_duration_table(rng).items()})

    print("\n== 2. Belt leaks: regimes, opening, inflow at 2 m (lo / mid / hi), mid at 1 m and 3 m ==")
    for name, share, lo, hi, f2, f1, f3, dcf, mins in leak_flow_table():
        print(f"   {name:9s} {share:4.2f}  A {lo:.4f}-{hi:.3f} m2   2 m: {f2[0]:6.0f} {f2[1]:6.0f} {f2[2]:7.0f} t/h"
              f"   mid@1m {f1:6.0f}  mid@3m {f3:6.0f}   DC x{dcf} in {mins[0]}-{mins[1]} min")
    print(f"   one 22 mm rivet hole at 3 m: {orifice_tph(math.pi * 0.011 ** 2, 3):.1f} t/h;"
          f"  seam 1 mm x 5 m at 2 m: {orifice_tph(0.001 * 5, 2):.0f} t/h")
    for label, imp, r in (("15in APC r=1.3", I_REF, 1.3), ("15in APC near thing r=1.05", I_REF, 1.05),
                          ("12in AP r=1.3", 390 * 480, 1.3), ("8in AP r=1.8", 125 * 500, 1.8)):
        shares, mean, med, p90 = belt_leak_mc(rng, 6000, imp, r)
        print(f"   {label:28s} P(leak) {1 - shares['none']:.2f}  seep {shares['seep']:.2f} mod {shares['moderate']:.2f}"
              f" major {shares['major']:.2f} lost {shares['lost']:.2f} | water per leak mean {mean:4.0f} t"
              f" median {med:4.0f} p90 {p90:4.0f}")

    print("\n== 3. Shock: delta-v of a 15in APC impulse (4e5 N s) by participating mass ==")
    for k, v in shock_delta_v(I_REF).items():
        print(f"   {k:20s} {v:6.2f} m/s")
    print("   own 16in/50 recoil impulse per gun ~1.0-1.3e6 N s over ~0.15 s (vs a 2-4 ms impact)")
    print("   per heavy non-pen hit, P(effect) by era:", ERAS)
    for name, (zone, ps) in SHOCK.items():
        print(f"   {name:26s} {zone}  " + "  ".join(f"{p:.3f}" for p in ps))

    print("\n== 4. 100 heavy hits on a capital ship in a 2 h battle (4 turrets) ==")
    for era in ("ww1", "ww2_early", "ww2_hardened"):
        res = battle_mc(random.Random(7), era, 3000)
        print(f"   [{era}]")
        for k, v in res.items():
            print(f"      {k:34s} {v:7.2f}")


if __name__ == "__main__":
    main()
