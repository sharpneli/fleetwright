"""
dc_holistic_ref.py -- holistic damage-control reference model (Naval project)

Companion to claude/damage-research/10_damage_control_holistic.md.
Builds on damage_control_ref.py (flooding engine, parties, counterflooding)
and adds the other threats and, more importantly, the COUPLINGS between them:

  fire  --(heat soak through steel, open doors)-->  more fire
  fire  --(smoke via doors, ventilation intakes)--> spaces untenable, machinery abandoned
  machinery abandoned --> no steam/power --> no fire-main pressure, no pumps  (death spiral)
  hit   --> fire-main rupture --> no water for hoses AND water pouring into the ship
  hoses --> water in the ship, high up --> GM loss + free surface --> list   (Ralph Talbot, Illustrious)
  fire next to ready ammo / magazine --> cook-off --> casualties, more fire, or loss
  explosions --> repair-party casualties --> slower everything
  one crew pool --> fire, flooding, magazines and power all compete for the same men

Not game code: a readable spec you can run.  python dc_holistic_ref.py
All [EST] numbers are designer estimates; anchors are in the doc.
"""
from __future__ import annotations
import math, random
import damage_control_ref as F
from damage_control_ref import Comp, Hole, Link, Party, Ship, RHO, G

MW = 1.0
WOOD_MJ_KG = 17.0

# fuel load, kg wood-equivalent per m^2 of deck  [EST; ship fit-out 1940s]
FUEL_KG_M2 = {
    "accom": 45, "wardroom": 55, "stores": 70, "office": 60, "galley": 30,
    "machinery": 12, "steering": 8, "magazine": 0, "void": 0, "tank": 0,
    "bridge": 30, "hangar": 25, "deckhouse": 20, "gunmount": 10, "torpedo": 5,
    "handling": 5,
}
STRIPPED = 0.45      # fraction left after stripping for war (USN post-Savo) [EST]
HRR_PER_M2 = 0.06    # MW/m^2 deck, fully developed, ventilation-limited [EST; Shadwell ~9 MW / 150 m^2]
HRR_CAP = 20.0       # MW per compartment
READY_COOK_MIN = 15.0  # minutes of fully developed fire before ready ammo starts to cook off [EST]
HOSE_TPH = 17.0      # one 1.5-in fog nozzle ~75 gpm ~ 17 t/h  [EST, 60-95 gpm]
EVAP = 0.25          # share of hose water turned to steam on a working fire [EST]

# ----------------------------------------------------------------------------
# Hull builder for cruiser / destroyer (battleship comes from F.build_battleship)
# ----------------------------------------------------------------------------
LAYOUTS = {
    # (from, to, lower use, upper use) as fractions of L     [after damage-model-research §2.2]
    "CA": [(0.00, 0.08, "stores", "stores"), (0.08, 0.30, "magazine", "accom"),
           (0.30, 0.65, "machinery", "accom"), (0.65, 0.85, "magazine", "wardroom"),
           (0.85, 1.00, "steering", "stores")],
    "DD": [(0.00, 0.06, "stores", "stores"), (0.06, 0.25, "magazine", "accom"),
           (0.25, 0.33, "tank", "accom"), (0.33, 0.66, "machinery", "machinery"),
           (0.66, 0.85, "magazine", "accom"), (0.85, 1.00, "steering", "stores")],
}
SUPER = {   # superstructure blocks: (from, to, use)
    "CA": [(0.18, 0.32, "bridge"), (0.42, 0.62, "hangar"), (0.62, 0.70, "deckhouse")],
    "DD": [(0.20, 0.30, "bridge"), (0.40, 0.48, "torpedo"), (0.52, 0.60, "deckhouse"),
           (0.60, 0.66, "torpedo")],
}
DIMS = {   # L, B, T, D(main deck), 2nd deck height, disp t, GM m, KG m   [EST, Astoria / Fletcher-like]
    "CA": (179.0, 18.8, 6.6, 12.0, 8.2, 12500, 1.3, 6.8),
    "DD": (114.0, 12.0, 4.0, 7.0, 4.9, 2900, 0.9, 4.4),
}


def build_small(kind, nsec=None, zebra=True, stripped=False, tightness=1.0):
    L, B, T, D, d2, disp, GM, KG = DIMS[kind]
    nsec = nsec or (16 if kind == "CA" else 12)
    s = Ship(f"{kind}-like", L, B, T, D, disp=disp, GM=GM, Cw=0.70)
    s.kg = KG
    seclen = L / nsec
    for i in range(nsec):
        xc = (i + 0.5) * seclen; f = xc / L
        width = B * min(1.0, 0.35 + 0.65 * min(f, 1 - f) / 0.2)
        lower_use = upper_use = "stores"
        for a, b, lu, uu in LAYOUTS[kind]:
            if a <= f < b:
                lower_use, upper_use = lu, uu
        lo = Comp(f"lower{i}", i, "inner", xc, 0.0, seclen, width, 1.0, d2,
                  0.85 if lower_use != "magazine" else 0.6)
        up = Comp(f"upper{i}", i, "upper", xc, 0.0, seclen, width, d2, D, 0.9)
        lo.use, up.use = lower_use, upper_use
        s.comps += [lo, up]
        for a, b, use in SUPER[kind]:
            if a <= f < b:
                sp = Comp(f"super{i}", i, "super", xc, 0.0, seclen, width * 0.7, D, D + 2.6, 0.9)
                sp.use = use
                s.comps.append(sp)
    by = {c.name: c for c in s.comps}
    base = 0.002 * tightness
    of = 0.0 if zebra else 0.06
    for i in range(nsec):
        for k in ("lower", "upper", "super"):
            a, b = by.get(f"{k}{i}"), by.get(f"{k}{i+1}")
            if a and b:
                z = a.z0 + 0.3
                s.links.append(Link(a, b, z, base, of if k != "lower" else of / 2,
                                    rating=1.3 * (a.z1 - z)))
        lo, up, sp = by[f"lower{i}"], by[f"upper{i}"], by.get(f"super{i}")
        s.links.append(Link(lo, up, lo.z1, 0.0015 * tightness, of / 2, rating=4.0))
        if sp:
            s.links.append(Link(up, sp, up.z1, 0.0015 * tightness, of / 2, rating=3.0))
    s.gm_eff = s.GM
    s.portable_pumps = 6 if kind == "CA" else 3
    _fire_defaults(s, stripped)
    return s


def build_bb(stripped=True, **kw):
    """Battleship from the flooding model plus a superstructure and uses."""
    s = F.build_battleship(**kw)
    s.kg = 9.6
    n = max(c.sec for c in s.comps) + 1
    for c in s.comps:
        f = c.x / s.L
        if c.kind in ("wingP", "wingS"):
            c.use = "tank"
        elif c.kind in ("innerP", "innerS", "inner"):
            c.use = ("magazine" if (0.15 <= f < 0.35 or 0.65 <= f < 0.80) else
                     "machinery" if 0.35 <= f < 0.65 else
                     "steering" if f >= 0.88 else "stores")
        else:
            c.use = "wardroom" if f > 0.75 else "accom"
    for i in range(n):
        f = (i + 0.5) / n
        if 0.25 <= f < 0.70:
            up = F.comp(s, f"upper{i}")
            sp = Comp(f"super{i}", i, "super", up.x, 0.0, up.length, s.B * 0.6,
                      up.z1, up.z1 + 2.6, 0.9)
            sp.use = "bridge" if f < 0.35 else "deckhouse"
            s.comps.append(sp)
            s.links.append(Link(up, sp, up.z1, 0.0015, 0.0, rating=3.0))
            if i > 0 and any(c.name == f"super{i-1}" for c in s.comps):
                s.links.append(Link(F.comp(s, f"super{i-1}"), sp, sp.z0 + 0.3, 0.002, 0.0, rating=3.0))
    _fire_defaults(s, stripped)
    return s


def _fire_defaults(s, stripped):
    s.stripped = stripped
    for c in s.comps:
        c.use = getattr(c, "use", "stores")
        floor = c.length * c.width
        fl = FUEL_KG_M2.get(c.use, 20) * (STRIPPED if stripped and c.use in
                                         ("accom", "wardroom", "stores", "office", "bridge", "deckhouse") else 1)
        c.fuel = c.fuel0 = fl * floor * WOOD_MJ_KG          # MJ
        c.I = 0.0; c.soak = 0.0; c.smoke = 0.0; c.cooled = 0.0
        c.gas = 0.0                       # tonnes gasoline/avgas present
        c.ready = 0.0                     # tonnes ready-service ammunition / deck torpedoes
        c.mag = None                      # magazine state dict
        c.abandoned = False; c.abandon_t = None; c.steam_until = -1
        c.local_hose_until = -1           # first-response hose (fire watch) active until
        c.ignited_at = None
        c.manned = c.use in ("machinery", "magazine", "handling", "steering", "bridge",
                             "gunmount", "torpedo")
        if c.use == "magazine":
            # the compartment stands for a magazine group plus shell rooms, passages
            # and stores; the magazines proper are ~35 % of its volume [EST]
            c.mag = dict(heat=0.0, sprink=False, flooded=False, flooding=False, frac=0.35,
                         sens=1.0, ctrl=None, events=0)
    # services
    s.fm_looped = False            # fire main loop (post-Savo cruisers; DDs got emergency pumps instead)
    s.fm_rupt = []                 # list of dicts {comp, found_at, isolated, rate}
    s.diesel_fire_pump = None      # comp holding an independent fire pump, or None
    s.emerg_gen = None             # comp holding emergency diesel generator, or None
    s.fans_on = True               # supply ventilation running (spreads smoke into intakes)
    s.secure_supply_vent = True    # doctrine: secure supply fans on fire (Franklin did)
    s.scuppers_blocked = set()
    s.first_response = 1.0         # minutes to the first hose on a new fire in a manned/near space
    s.muster = 2.0                 # minutes for a repair party to lead out hoses at a new task [EST]
    s.turn_to = 15.0               # minutes after the last hit when gun crews etc. turn to firefighting
    s.turn_to_men = 0              # how many extra men that frees (set per ship) [EST]
    s.ba_sets = 2                  # breathing apparatus per party (1942 ~2, 1945 ~6) [EST]
    s.stay_in_smoke = False        # Bunker Hill: engineers ordered to stay at posts
    s.doctrine = "usn43"           # task priority order
    s.cook_events = []
    s.crew_lost = 0
    s.lost = ""


# ----------------------------------------------------------------------------
# Hydrostatics with height of added water (high water costs GM)
# ----------------------------------------------------------------------------
def hydro_kg(s):
    """Like F.hydro, plus (1) the height of added water: water above KG lowers
    GM, water below raises it; (2) wall-sided righting arm
    GZ = sin(phi) * (GM + BM/2 * tan^2 phi), so a ship with negative GM lolls
    to a steady angle instead of capsizing outright (Ralph Talbot: GM -0.75 ft,
    ~20 deg list, survived)."""
    if not hasattr(s, "kg"):
        return _orig_hydro(s)
    w = m_trim = m_heel = m_vert = 0.0
    slack = []
    for c in s.comps:
        t = c.water * RHO
        if t <= 0:
            continue
        d = c.water / c.area
        w += t
        m_trim += t * (s.L / 2 - c.x)
        m_heel += t * c.y
        m_vert += t * (s.kg - (c.z0 + d / 2))
        if c.water < c.cap * 0.98:
            slack.append((t, c.width, max(d, 0.02)))
    disp = s.disp + w
    s.sink = w / (RHO * s.Cw * s.L * s.B)
    s.trim = m_trim / (disp * s.L)
    gm0 = (s.disp * s.GM + m_vert) / disp
    bm = 0.85 * s.B ** 2 / (12 * s.T)

    def f(x):          # x = tan(phi); residual of moment balance (per tonne displacement)
        m_fs = sum(t * math.copysign(min(b * b * abs(x) / (12 * d), b / 3), x) for t, b, d in slack)
        return x * (gm0 + 0.5 * bm * x * x) - (m_heel + m_fs) / disp

    x = math.tan(s.heel)
    if abs(x) < 0.005:
        x = 0.005 * (1 if m_heel >= 0 else -1)
    # relax toward a STABLE equilibrium (one where more heel brings more righting
    # moment); Newton would happily sit on the unstable upright root of a lolling ship
    for _ in range(16):
        lam = 0.6 / (abs(gm0) + 1.5 * bm * x * x + 0.05)
        x -= max(-0.04, min(0.04, lam * f(x)))
    s.heel = math.atan(x)
    s.gm_static = gm0
    s.gm_eff = gm0 - sum(t * b * b / (12 * d) for t, b, d in slack) / disp
    return w


_orig_hydro = F.hydro
F.hydro = hydro_kg


# ----------------------------------------------------------------------------
# Services: power, fire-main pressure
# ----------------------------------------------------------------------------
def depth(c):
    return c.water / c.area


def space_ok(c):
    return c is not None and not c.abandoned and depth(c) < 1.5 and c.I < 0.5


def power_on(s):
    gens = [c for c in s.comps if c.use == "machinery" and c.kind != "upper"]
    if any(space_ok(c) for c in gens):
        return True
    return space_ok(s.emerg_gen)


def fm_pressure(s, c):
    """0..1 water pressure at a fire plug in compartment c."""
    src = (1.0 if power_on(s) else 0.0)
    if s.diesel_fire_pump is not None and space_ok(s.diesel_fire_pump):
        src = max(src, 0.6)                       # e.g. 1,000 gpm diesel pump [WDR 29]
    if src == 0:
        return 0.0
    open_r = [r for r in s.fm_rupt if not r["isolated"]]
    p = src
    jumpered = getattr(c, "jumper", False)
    for r in open_r:
        if r["comp"].sec == c.sec and not jumpered:
            return 0.0                            # riser cut right here
        if s.fm_looped:
            p *= 0.75                             # loop keeps feeding round the break
        else:
            # a single main: everything beyond the break from midships loses pressure
            mid = s.L / 2
            if abs(c.x - mid) > abs(r["comp"].x - mid) and (c.x - mid) * (r["comp"].x - mid) > 0 \
                    and not jumpered:
                return 0.0
            p *= 0.4                              # break bleeds the main
    return p * (0.5 if jumpered and any(r["comp"].sec == c.sec for r in open_r) else 1.0)


# ----------------------------------------------------------------------------
# Damage events
# ----------------------------------------------------------------------------
def neighbours(s, c):
    for l in s.links:
        if l.a is c:
            yield l.b, l
        elif l.b is c:
            yield l.a, l


def ignite(s, c, t, I0=0.08, why=""):
    if c.fuel <= 0 and c.gas <= 0:
        return
    if c.I == 0:
        c.ignited_at = t
        s.log.append((t, f"FIRE in {c.name} ({c.use}) {why}"))
        if c.manned or any(n.manned for n, _ in neighbours(s, c)):
            c.local_hose_until = t + (s.first_response + 20) * 60
    c.I = max(c.I, I0)


def hit(s, c, kind, t, rng):
    """Apply one weapon hit to compartment c."""
    if kind in ("he", "ap", "bomb"):
        pf = {"he": 0.55, "ap": 0.3, "bomb": 0.7}[kind]
        if c.fuel > 0 and rng.random() < pf:
            ignite(s, c, t, rng.uniform(0.05, 0.3), f"({kind} hit)")
        if c.ready > 0 and rng.random() < 0.4:
            ignite(s, c, t, 0.6, "(ready ammo hit)")
        if rng.random() < {"he": 0.2, "ap": 0.12, "bomb": 0.35}[kind] and c.kind != "super":
            rupture_firemain(s, c, t, rng)
        for n, l in neighbours(s, c):
            if rng.random() < 0.5:
                l.sprung += rng.uniform(0.002, 0.03)
        if c.kind in ("super", "upper", "inner") and c.use == "machinery" and rng.random() < 0.5:
            c.steam_until = t + rng.uniform(10, 30) * 60
            s.log.append((t, f"steam leak in {c.name}"))
        if c.manned:
            s.crew_lost += rng.randint(2, 12)
    elif kind == "kamikaze":
        c.gas += rng.uniform(0.3, 0.8)
        ignite(s, c, t, 1.0, "(kamikaze, gasoline)")
        for n, l in neighbours(s, c):
            l.sprung += 0.05
        if rng.random() < 0.5:
            rupture_firemain(s, c, t, rng)
        s.crew_lost += rng.randint(5, 30)


def rupture_firemain(s, c, t, rng):
    s.fm_rupt.append(dict(comp=c, found_at=t + rng.uniform(1.5, 8) * 60, isolated=False,
                          rate=rng.uniform(80, 200)))   # t/h at full pressure (Ralph Talbot >200 t)
    s.log.append((t, f"fire main ruptured in {c.name}"))


# ----------------------------------------------------------------------------
# Fire / smoke / cook-off step
# ----------------------------------------------------------------------------
def above(a, b):
    return b.z0 >= a.z1 - 0.1


def fire_step(s, t, dt, rng):
    # -- fire main ruptures pour water in while pumps run
    for r in s.fm_rupt:
        if not r["isolated"]:
            p = 1.0 if power_on(s) else (0.6 if s.diesel_fire_pump and space_ok(s.diesel_fire_pump) else 0)
            r["comp"].water = min(r["comp"].cap, r["comp"].water + r["rate"] * p / RHO / 3600 * dt)
    # -- scuppers: water on superstructure decks drains overboard unless blocked
    for c in s.comps:
        if c.kind == "super" and c.water > 0 and c.name not in s.scuppers_blocked:
            q = F.CD * 0.08 * math.sqrt(2 * G * depth(c)) * dt     # ~0.08 m^2 of scuppers [EST]
            c.water = max(0.0, c.water - q)
    pw = power_on(s)
    fans = s.fans_on and pw and not (s.secure_supply_vent and any(c.I > 0.2 for c in s.comps))
    burning = [c for c in s.comps if c.I > 0]
    for c in burning:
        # growth (oxygen-limited if the space is closed up)
        open_ = any(l.open_fit > 0 or l.failed or l.sprung > 0.02 for _, l in neighbours(s, c)) \
            or c.kind == "super" or any(h.comp is c for h in s.holes)
        imax = 1.0 if open_ else 0.65
        rg = (1 / 3.0 if s.stripped else 1 / 1.5) / 60           # per s  [EST]
        hoses = c._hoses if hasattr(c, "_hoses") else 0.0
        if c.gas > 0:
            imax, rg = 1.0, 2.0 / 60
        # a fire with water on it stops growing; gasoline keeps re-flashing [EST]
        grow = rg * c.I * max(0.0, 1 - c.I / imax) * max(0.0, 1 - hoses * (0.5 if c.gas > 0 else 1.0))
        # suppression: one fog hose puts out a 150 m^2 fully developed fire in ~9 min
        # (Shadwell 1994: fog controlled in 2 min, all flames out in ~9-10 min)
        floor = c.length * c.width
        k = hoses * (1 / 5.0) / 60 * math.sqrt(150 / max(floor, 30))
        if c.gas > 0:
            k *= 0.6                                              # gasoline: fog/foam needed
        c.I = max(0.0, min(imax, c.I + (grow - k * (0.3 + 0.7 * c.I)) * dt))
        # burn fuel
        hrr = c.I * min(HRR_PER_M2 * floor, HRR_CAP)
        if c.gas > 0:
            hrr += c.I * 40.0                                     # gasoline pool fire [EST]
            c.gas = max(0.0, c.gas - c.I * 40.0 / 43000.0 * dt)   # 43 GJ/t
        c.fuel = max(0.0, c.fuel - c.I * min(HRR_PER_M2 * floor, HRR_CAP) * dt)
        if c.fuel <= 0 and c.gas <= 0:
            c.I = max(0.0, c.I - dt / 120)
        if c.I < 0.01:
            c.I = 0.0
            s.log.append((t, f"fire out in {c.name}"))
        # hose water stays in the ship
        if hoses > 0:
            c.water = min(c.cap, c.water + hoses * HOSE_TPH * (1 - EVAP * (c.I > 0.3)) / RHO / 3600 * dt)
        # heat soak into neighbours
        if c.I > 0.8:
            for n, l in neighbours(s, c):
                if n.I > 0 or (n.fuel <= 0 and n.mag is None and n.ready <= 0):
                    continue
                if l.failed or l.open_fit > 0 or l.sprung > 0.05:
                    ts = 1.5
                elif above(c, n):
                    ts = 12.0                                     # deck above heats fastest
                elif above(n, c):
                    ts = 40.0
                else:
                    ts = 25.0   # [EST; Shadwell: a severe 9 MW fire took ~20 min to bring the next
                                #  compartment's deck to ignition temperatures]
                cool = 0.15 if n.cooled > t else 1.0
                gain = dt / (ts * 60) * cool
                n.soak += gain
                if n.mag is not None:
                    # magazine heating [EST]: an open flash path (door, scuttle, hoist, failed
                    # bulkhead) ~20 min of fully developed fire; through steel ~3 h
                    # (Astoria's 5-in magazine went hours after the fire reached the hoists)
                    flash = l.failed or l.open_fit > 0
                    n.mag["heat"] += dt / ((20 if flash else 180) * 60) * cool
                if n.ready > 0:
                    n.rheat = getattr(n, "rheat", 0.0) + gain * 0.5
        # smoke production
        c.smoke = min(1.0, c.smoke + c.I * dt / 60)
        # ready ammunition / warheads / depth charges heat up in the fire:
        # DD war damage report -- depth-charge fires blew up after 23 and 26 min;
        # Forrestal's bombs in a fuel fire went in 90 s. Cooling (hoses) resets it.
        if c.ready > 0:
            c.rheat = getattr(c, "rheat", 0.0)
            if c.I > 0.3:
                c.rheat += c.I * dt / (READY_COOK_MIN * 60) * (0.3 if hoses > 0 else 1.0)
            else:
                c.rheat = max(0.0, c.rheat - dt / 600)
            if c.rheat > 1.0 and rng.random() < 0.3 * dt / 60:
                cookoff_ready(s, c, t, rng)
    for c in s.comps:
        if c.I == 0 and c.soak >= 1.0 and c.fuel > 0:
            if rng.random() < 0.6:      # something burnable against the hot steel? [EST]
                ignite(s, c, t, 0.1, "(spread through boundary)")
                c.soak = 0
            else:
                c.soak = 0.6
        if c.cooled < t:
            c.soak = max(0.0, c.soak - dt / 1800 * (1 if not any(n.I > 0.7 for n, _ in neighbours(s, c)) else 0))
    # smoke transport + venting
    for l in s.links:
        a, b = l.a, l.b
        k = 0.002 + 3.0 * (l.open_fit + l.sprung + (1.0 if l.failed else 0.0))
        k = min(k, 0.5)
        flow = k * (a.smoke - b.smoke) * dt / 60
        a.smoke -= flow; b.smoke += flow
        # ventilation intakes: superstructure smoke pulled into machinery below when supply fans run
        if fans and a.kind == "upper" and b.kind == "super" and b.smoke > 0.2:
            for m in s.comps:
                if m.sec == a.sec and m.use == "machinery":
                    m.smoke = min(1.0, m.smoke + 0.4 * b.smoke * dt / 60)
    for c in s.comps:
        vent = 0.6 if c.kind == "super" else (0.15 if (fans or not s.secure_supply_vent) else 0.05)
        c.smoke = max(0.0, min(1.0, c.smoke - vent * c.smoke * dt / 60 * (0.3 if c.I > 0 else 1)))
    # machinery abandonment and re-manning
    for c in s.comps:
        if c.use != "machinery" or c.kind == "super":
            continue
        bad = c.I > 0.3 or depth(c) > 2.0 or t < c.steam_until or \
            (c.smoke > (0.9 if s.stay_in_smoke else 0.6))
        if bad and not c.abandoned:
            if c.abandon_t is None:
                c.abandon_t = t + (0 if (c.I > 0.3 or t < c.steam_until) else 300)
            elif t >= c.abandon_t:
                c.abandoned = True
                why = ("steam leak" if t < c.steam_until else "fire" if c.I > 0.3 else
                       "flooding" if depth(c) > 2.0 else "smoke")
                s.log.append((t, f"{c.name} ABANDONED ({why})"))
        elif not bad:
            c.abandon_t = None
            if c.abandoned and c.smoke < 0.3 and c.I == 0 and depth(c) < 1.0:
                c.abandoned = False
                s.log.append((t, f"{c.name} re-manned"))
        if s.stay_in_smoke and c.smoke > 0.6 and rng.random() < 0.02 * dt / 60:
            s.crew_lost += 1
    # magazines
    for c in s.comps:
        m = c.mag
        if m is None or m["flooded"]:
            continue
        if c.I > 0:
            m["heat"] += c.I * dt / 120                           # fire inside: minutes, not hours
        if m["sprink"]:
            m["heat"] = max(0.0, m["heat"] - dt / 300)
            p = fm_pressure(s, c)
            if p < 0.3:
                m["sprink"] = False
                s.log.append((t, f"sprinklers in {c.name} lost pressure"))
            else:   # 32 l/m^2/min (USN 0.8 gpm/ft^2) -- a LOT of water
                c.water = min(c.cap, c.water + 0.032 * c.length * c.width * p * dt / 60 * 0.5)
        if m["flooding"]:      # sea flood valves: ~0.06 m^2 at the bottom, ~200-300 t/h [EST]
            head = max(0.0, s.wl(c.x, c.y) - c.level)
            c.water = min(c.cap, c.water + F.CD * 0.06 * math.sqrt(2 * G * head) * dt)
        if c.water >= 0.95 * m["frac"] * c.cap:
            m["flooded"] = True; m["flooding"] = False; m["sprink"] = False
            s.log.append((t, f"magazine {c.name} flooded -- turret out of action, no cook-off risk"))
            continue
        if m["heat"] > 1.0 and not m["sprink"]:
            if rng.random() < (0.05 + 0.2 * (m["heat"] - 1)) * dt / 60:
                s.lost = s.lost or f"MAGAZINE EXPLOSION {c.name} at {t/60:.0f} min"
                s.log.append((t, s.lost))


def cookoff_ready(s, c, t, rng):
    tons = min(c.ready, rng.uniform(0.05, 0.4))
    c.ready -= tons
    c.rheat = 0.7
    s.cook_events.append(t)
    s.log.append((t, f"ready ammunition cooked off in {c.name}"))
    for n, l in neighbours(s, c):
        l.sprung += 0.02
        n.soak += 0.4
    for p in s.parties:
        if getattr(p, "_at", None) is c and p.men > 0:
            k = int(p.men * rng.uniform(0.2, 0.6))
            p.men -= k
            s.crew_lost += k
    if rng.random() < 0.3:
        rupture_firemain(s, c, t, rng)
    if c.kind != "super" and rng.random() < 0.35:    # explosion inside the hull opens the side [EST]
        a = rng.uniform(0.1, 0.6)
        z = c.z0 + 0.3
        s.holes.append(Hole(c, a, z))
        s.log.append((t, f"internal explosion in {c.name} holed the side ({a:.2f} m^2, {z - s.T:+.1f} m from WL)"))


# ----------------------------------------------------------------------------
# Damage control: DC Central actions and party tasks
# ----------------------------------------------------------------------------
FIRE_TASKS = {        # base minutes, men  [EST]
    "fight": (5.0, 4),      # hose attack, re-evaluated every 5 min; men per hose
    "cool": (5.0, 2),       # boundary cooling of a neighbour (stops spread through steel)
    "isolate": (3.0, 2),    # find the right valves and isolate a fire-main break
    "jumper": (6.0, 3),     # jumper hose from a pressurised plug in the next section
    "jettison": (8.0, 6),   # ready ammo / deck torpedoes / depth charges over the side
    "magvalve": (4.0, 2),   # reach magazine flood/sprinkle valves by hand
}


def dc_central(s, t):
    """Orders that DC Central gives without a party: sprinkle and flood magazines,
    secure supply ventilation (handled in fire_step), counterflood (flood model)."""
    for c in s.comps:
        m = c.mag
        if m is None or m["flooded"]:
            continue
        threat = c.I > 0 or m["heat"] > 0.25 or any(n.I > 0.5 for n, _ in neighbours(s, c))
        if not threat:
            if m["sprink"] and m["heat"] < 0.1:
                m["sprink"] = False
                s.log.append((t, f"DC Central: sprinklers in {c.name} secured"))
            m["heat"] = max(0.0, m["heat"] - 0.002)
            continue
        ctrl_ok = (m["ctrl"] is None or (m["ctrl"].I < 0.3 and m["ctrl"].smoke < 0.8))
        if not m["sprink"] and fm_pressure(s, c) >= 0.3 and ctrl_ok:
            m["sprink"] = True
            s.log.append((t, f"DC Central: sprinkling {c.name}"))
        if m["heat"] > 0.6 and ctrl_ok and not m["flooding"]:
            m["flooding"] = True                                  # open sea flood valves
            s.log.append((t, f"DC Central: flooding magazine {c.name}"))


def fire_tasks(s, t):
    out = []
    for c in s.comps:
        if c.I > 0:
            near_mag = c.mag is not None or c.ready > 0 or c.gas > 0 or \
                any(n.mag is not None and not n.mag["flooded"] for n, _ in neighbours(s, c))
            out.append((1 if near_mag else 3, "fight", c, c.sec))
            for n, _ in neighbours(s, c):
                if n.I == 0 and n.soak > 0.25 and n.cooled < t:
                    out.append((2 if n.mag is not None else 4, "cool", n, n.sec))
        if c.ready > 0 and (c.I > 0 or c.soak > 0.3):
            out.append((1, "jettison", c, c.sec))
        m = c.mag
        if m and not m["flooded"] and m["heat"] > 0.4 and not m["sprink"] and \
                not m["flooding"] and \
                m["ctrl"] is not None and (m["ctrl"].I >= 0.3 or m["ctrl"].smoke >= 0.8):
            out.append((0, "magvalve", c, c.sec))
    for r in s.fm_rupt:
        if not r["isolated"] and t >= r["found_at"] and not r.get("claimed"):
            out.append((0, "isolate", r, r["comp"].sec))   # no water anywhere until this is done
    if power_on(s) or s.diesel_fire_pump is not None:
        for c in s.comps:
            if c.I > 0.2 and fm_pressure(s, c) == 0 and not getattr(c, "jumper", False):
                out.append((1, "jumper", c, c.sec))
    return out


def can_enter(s, c, party):
    return c.kind == "super" or c.smoke < 0.5 or s.ba_sets > 0


def p_task_time(s, kind, party, obj):
    if kind in FIRE_TASKS:
        base, men = FIRE_TASKS[kind]
    else:
        base, men = F.TASKS[kind]
    tt = base / s.dc_skill * max(1.0, men / max(party.men / 3, 1))
    if not power_on(s):
        tt *= 1.6
    tt *= 1 + abs(math.degrees(s.heel)) / 10
    return tt


def run_parties(s, t, rng):
    # finish tasks
    for p in s.parties:
        if p.task and t >= p.busy_until:
            kind, obj = p.task
            if kind == "isolate":
                obj["isolated"] = True
                s.log.append((t, f"{p.name}: fire-main break in {obj['comp'].name} isolated"))
            elif kind == "jettison":
                obj.ready = 0.0
                s.log.append((t, f"{p.name}: ready ammunition in {obj.name} jettisoned"))
            elif kind == "magvalve":
                obj.mag["sprink"] = fm_pressure(s, obj) >= 0.3
                obj.mag["flooding"] = True
                s.log.append((t, f"{p.name}: magazine {obj.name} flood valves opened by hand"))
            elif kind == "jumper":
                obj.jumper = True
                s.log.append((t, f"{p.name}: jumper hose rigged to {obj.name}"))
            elif kind in ("fight", "cool"):
                pass
            else:
                F.finish(s, p, t)
            p.task, p._at = None, None
    for c in s.comps:
        c._hoses = 0.0
    # first-response hoses (fire watch at the scene)
    for c in s.comps:
        if c.I > 0 and c.ignited_at is not None and \
                c.ignited_at + s.first_response * 60 <= t <= c.local_hose_until:
            c._hoses += 0.7 * fm_pressure(s, c)
    # ongoing fire tasks produce hoses
    for p in s.parties:
        if p.task and p.task[0] == "fight" and t >= getattr(p, "start_at", 0):
            c = p.task[1]
            press = fm_pressure(s, c)
            nh = min(3, p.men // 4)
            if c.smoke >= 0.5 and c.kind != "super":           # topside fires can be fought from the weather deck
                nh = min(nh, (s.ba_sets if getattr(p, "skill", 1.0) >= 1 else 1) // 2)
            eff = nh * press if press > 0 else 0.15 * (p.men > 0)    # CO2, buckets
            eff *= 0.8                                           # rotation: stay time ~20 min [Shadwell]
            c._hoses += eff * min(1.0, s.dc_skill * 1.1) * getattr(p, "skill", 1.0)
        elif p.task and p.task[0] == "cool":
            p.task[1].cooled = t + 60
    free = [p for p in s.parties if p.task is None and p.men >= 2]
    if not free:
        return
    ft = fire_tasks(s, t)
    if s.doctrine == "usn43":
        tasks = ft + [(5 + pri, k, o, sec) for pri, k, o, sec in F.gen_tasks(s)]
    else:   # flooding first -- but a fire-main break is a flooding source too, so it stays on top
        tasks = [(pri, k, o, sec) for pri, k, o, sec in F.gen_tasks(s)] + \
                [(0 if k == "isolate" else 5 + pr, k, o, sec) for pr, k, o, sec in ft]
    tasks.sort(key=lambda x: x[0])
    taken = {}
    for pri, kind, obj, sec in tasks:
        if not free:
            break
        key = (kind, id(obj))
        limit = 2 if kind == "fight" else 1
        busy = sum(1 for p in s.parties if p.task and p.task[0] == kind and p.task[1] is obj)
        if kind == "jumper" and busy:
            continue
        if busy + taken.get(key, 0) >= limit:
            continue
        if kind == "fight" and not can_enter(s, obj, None):
            continue
        p = min(free, key=lambda q: abs(q.sec - sec))
        travel = 0.6 * abs(p.sec - sec) * (1.5 if obj is not None and getattr(obj, "smoke", 0) > 0.5 else 1)
        dur = travel + p_task_time(s, kind, p, obj)
        same = p.task is None and getattr(p, "_last", None) is obj
        p.start_at = t + (0 if same else (travel + s.muster) * 60)   # lead out hoses, get water on
        dur += 0 if same else s.muster
        p.task, p.busy_until, p.sec = (kind, obj), t + dur * 60, sec
        p._last = obj
        p._at = obj if isinstance(obj, Comp) else None
        if kind == "isolate":
            obj["claimed"] = True
        elif kind == "jumper":
            obj.jumper = "pending" if False else getattr(obj, "jumper", False)
        elif kind in ("plug", "patch"):
            obj.claimed = True
        elif kind == "pump":
            obj._cl = {"pump"}
        elif kind in ("close", "shore", "caulk"):
            obj.claimed.add(kind)
        taken[key] = taken.get(key, 0) + 1
        free.remove(p)


# ----------------------------------------------------------------------------
# Driver
# ----------------------------------------------------------------------------
def simulate(s, hours=4.0, dt=1.0, seed=1, events=(), verbose=False, max_heel=25):
    rng = random.Random(seed)
    random.seed(seed)
    t, state, trace = 0.0, {}, []
    ev = sorted(events, key=lambda e: e[0])
    last_hit = max((e[0] for e in ev), default=0)
    turned_to = False
    last_party = -1
    while t < hours * 3600:
        while ev and ev[0][0] * 60 <= t:
            _, fn = ev.pop(0)
            fn(s, t, rng)
        if not turned_to and s.turn_to_men > 0 and t >= (last_hit + s.turn_to) * 60:
            turned_to = True
            nmax = max(c.sec for c in s.comps)
            k = s.turn_to_men // 12
            for i in range(k):
                q = Party(f"Turn-to {i+1}", 12, int(nmax * (i + 0.5) / k)); q._at = None; q.skill = 0.7
                s.parties.append(q)
            s.log.append((t, f"action over: {k} extra hose parties from gun crews turn to"))
        s.power = power_on(s)
        w, h = F.step(s, t, dt)
        F.counterflood(s, t, h, state)
        fire_step(s, t, h, rng)
        if int(t // 30) != last_party:
            last_party = int(t // 30)
            dc_central(s, t)
            run_parties(s, t, rng)
        deg = math.degrees(s.heel)
        hull_up = [c for c in s.comps if c.kind == "upper"]
        immersed = sum(1 for c in hull_up if s.wl(c.x, 0) > c.z1) / max(1, len(hull_up))
        if abs(deg) > max_heel:
            s.lost = s.lost or f"capsized at {t/60:.0f} min"
        elif immersed > 0.3:
            s.lost = s.lost or f"foundered at {t/60:.0f} min"
        if not trace or t / 60 - trace[-1]["min"] >= 1.0:
            trace.append(dict(min=t / 60, water=w, heel=deg, gm=s.gm_eff,
                              fires=sum(1 for c in s.comps if c.I > 0.05),
                              burning=sum(c.I for c in s.comps), power=s.power,
                              ab=sum(1 for c in s.comps if c.abandoned)))
        if s.lost:
            break
        t += h
    return trace


def first_t(trace, pred):
    r = next((r for r in trace if pred(r)), None)
    return r["min"] if r else None


def report(s, tr, label):
    peak_f = max(r["fires"] for r in tr)
    out_t = None
    started = first_t(tr, lambda r: r["fires"] > 0)
    if started is not None:
        after = [r for r in tr if r["min"] > started + 1]
        out_t = next((r["min"] for r in after if r["fires"] == 0), None)
    pw_lost = first_t(tr, lambda r: not r["power"])
    last = tr[-1]
    line = (f"{label:<50} fires peak {peak_f:2d}, out at {('%.0f min' % out_t) if out_t else 'never':>9} | "
            f"power lost {('%.0f min' % pw_lost) if pw_lost is not None else '-':>7} | "
            f"water {last['water']:5.0f} t heel {last['heel']:+5.1f}° GM {getattr(s, 'gm_static', s.GM):+.2f} | "
            f"crew lost {s.crew_lost:3d} | cook-offs {len(s.cook_events)}")
    if s.lost:
        line += f"\n{'':<50} ** LOST: {s.lost}"
    return line


def std_parties_small(s, n, men):
    secs = [int(round((max(c.sec for c in s.comps)) * (k + 0.5) / n)) for k in range(n)]
    s.parties = [Party(f"Repair {i+1}", men, sc) for i, sc in enumerate(secs)]
    for p in s.parties:
        p._at = None


# ----------------------------------------------------------------------------
# Scenarios
# ----------------------------------------------------------------------------
def scen_savo(fixed: bool):
    """Heavy cruiser surprised at night (Astoria, Savo Island, 9 Aug 1942):
    ~25 8-in hits in ~15 min, hangar aircraft burning, wardroom country,
    fire main cut forward, smoke drawn into fire rooms. 'fixed' = post-Savo
    changes: stripped flammables, looped main, 1,000 gpm diesel fire pump,
    emergency diesel generator, aircraft jettisoned, supply fans secured,
    more breathing sets, magazine controls outside the fire zone."""
    s = build_small("CA", stripped=fixed)
    s.speed_kn = 15
    s.turn_to_men = 120
    s.dc_skill = 0.8 if not fixed else 1.0
    s.fixed_drain = 4
    s.counterflood_rate = 0
    std_parties_small(s, 4, 22)
    by = {c.name: c for c in s.comps}
    hangar = [c for c in s.comps if c.use == "hangar"]
    for c in hangar:
        c.gas = 0.0 if fixed else 0.35                 # fuelled floatplanes
    for c in s.comps:
        if c.kind == "super" and c.use in ("deckhouse", "bridge"):
            c.ready = 1.5                               # 5-in ready-service ammo
        if c.kind == "upper" and 4 <= c.sec <= 12:
            c.ready = 0.3                               # 5-in rounds in hoists / handling rooms
    mags = [c for c in s.comps if c.mag]
    for m in mags:
        up = by[f"upper{m.sec}"]
        m.mag["ctrl"] = up if not fixed else None      # flood controls in the wardroom country
    if fixed:
        s.fm_looped = True
        s.diesel_fire_pump = by["lower3"]
        s.emerg_gen = by["lower14"]
        s.ba_sets = 6
        s.secure_supply_vent = True
    else:
        s.secure_supply_vent = False
        s.ba_sets = 1
    tgts = (["super4", "super8", "super9", "upper12", "upper13", "super10", "upper4", "upper5",
             "super3", "upper12", "lower7", "upper6", "super9", "upper11", "upper13", "super10",
             "upper3", "upper12", "upper9", "super8", "upper2", "upper14", "super4", "upper10", "upper12"])
    ev = []
    for k, name in enumerate(tgts):
        ev.append((0.5 + k * 0.6, (lambda nm: lambda s_, t, r: hit(s_, by[nm], "he" if r.random() < .6 else "ap", t, r))(name)))
    # two shell holes near the waterline aft
    ev.append((6, lambda s_, t, r: s_.holes.append(Hole(by["lower12"], 0.15, s_.T + 0.4))))
    ev.append((9, lambda s_, t, r: s_.holes.append(Hole(by["lower11"], 0.05, s_.T - 0.6))))
    return s, ev


def scen_dd_fire(first_hose, kind, seed):
    """Destroyer, one hit amidships next to the torpedo mount and ready ammunition.
    kind='kamikaze' (gasoline, fully developed at once) or 'shell' (HE, a fire that
    still has to grow). first_hose = minutes until the first hose is on it: a fire
    watch / repair locker on the spot (1 min) or a party mustering and leading out
    hoses from elsewhere (6 min). DD war damage report: 'one hose stream reaching the
    fire within a minute was often worth more than several arriving a few minutes later'."""
    s = build_small("DD", stripped=True)
    s.speed_kn = 25; s.fixed_drain = 2; s.counterflood_rate = 0
    std_parties_small(s, 3, 10)
    s.ba_sets = 4
    s.secure_supply_vent = True
    s.diesel_fire_pump = next(c for c in s.comps if c.name == "lower2")
    if first_hose <= 1.5:
        s.first_response, s.muster = first_hose, 2.0
    else:
        s.first_response, s.muster = 99.0, first_hose
    by = {c.name: c for c in s.comps}
    for c in s.comps:
        if c.use == "torpedo":
            c.ready = 3.0             # torpedo warheads; depth charges / ready 5-in similar
        if c.use == "deckhouse":
            c.ready = 1.0
    if kind == "kamikaze":
        ev = [(0.0, lambda s_, t, r: hit(s_, by["super6"], "kamikaze", t, r)),
              (0.0, lambda s_, t, r: hit(s_, by["upper7"], "bomb", t, r))]
    else:
        def sh(s_, t, r):
            hit(s_, by["super6"], "he", t, r)
            ignite(s_, by["super6"], t, 0.08, "(HE shell)")
        ev = [(0.0, sh)]
    return s, ev


def scen_talbot(isolate_fast: bool):
    """Ralph Talbot-type (DD war damage report): forward superstructure fire, and a
    fire-main break between decks forward. The fire pumps keep pouring water in
    (>200 t) and it is trapped above the waterline; result ~20 deg list, GM about
    -0.75 ft. Isolating the break early is the whole difference."""
    s = build_small("DD", stripped=False)
    s.speed_kn = 15; s.fixed_drain = 2
    std_parties_small(s, 3, 10)
    s.ba_sets = 2
    s.portable_pumps, s.portable_rate = 1, 0.5     # one electric submersible; the P-500s 'repeatedly failed'
    by = {c.name: c for c in s.comps}
    def ev0(s_, t, r):
        ignite(s_, by["super3"], t, 0.6, "(shell hit, bridge)")
        for a_, b_ in (("upper1", "upper2"), ("upper2", "upper3"), ("upper3", "upper4")):
            F.link(s_, a_, b_).sprung = 0.3          # doors and joiner bulkheads blown open
        s_.fm_rupt.append(dict(comp=by["upper2"], found_at=t + (3 if isolate_fast else 150) * 60,
                               isolated=False, rate=100.0))
        s_.log.append((t, "fire main ruptured in upper2 (between decks, forward)"))
    return s, [(0.0, ev0)]


def scen_bb_combined(doctrine, seed=3):
    """Battleship, 1942 fit (not stripped), three repair parties: one torpedo
    (NC-like, aft of amidships) plus three bombs that start fires in the
    between-decks over the after magazine group and in the superstructure, and
    cut the fire main. The same parties must split between fire and flooding.
    Compare doctrines: 'usn43' (fires near magazines and fire-main breaks first)
    and 'flood_first'."""
    s = build_bb(stripped=False)
    s.speed_kn = 20; s.counterflood_rate = 80; s.fixed_drain = 10
    s.turn_to_men = 0              # still in action: everyone stays at their guns
    s.parties = [Party(f"Repair {i+1}", 30, sec) for i, sec in enumerate((3, 8, 12, 17))]
    for p in s.parties:
        p._at = None
    s.doctrine = doctrine
    s.ba_sets = 4
    s.emerg_gen = F.comp(s, "inner18")
    by = {c.name: c for c in s.comps}
    for c in s.comps:
        if c.kind == "super" and c.use == "deckhouse":
            c.ready = 1.0                              # 5-in ready-service ammunition
    def torp(s_, t, r):
        s_.holes.append(Hole(by["wingP12"], 45.0, 5.0))
        hb = F.link(s_, "wingP12", "innerP12"); hb.sprung = 0.15
        F.link(s_, "innerP12", "innerP13").sprung = 0.02
        F.link(s_, "innerP12", "upper12").sprung = 0.01
    def bomb(name, cut):
        def f(s_, t, r):
            hit(s_, by[name], "bomb", t, r)
            ignite(s_, by[name], t, 0.4, "(bomb)")
            if cut:
                rupture_firemain(s_, by[name], t, r)
        return f
    ev = [(0.0, torp), (1.0, bomb("upper14", True)), (1.3, bomb("super12", False)),
          (1.6, bomb("upper13", True))]
    return s, ev


if __name__ == "__main__":
    import sys
    quick = "--quick" in sys.argv

    print("\n=== 1. Night surprise, heavy cruiser (Savo/Astoria type), 12 h, several seeds ===")
    n1 = 4 if quick else 12
    for fixed, lab in ((False, "1942 Astoria fit: unstripped, single main, planes fuelled"),
                       (True, "post-Savo fit: stripped, looped main, diesel pump+gen")):
        lost_t, causes, outs, pw, cooks = [], {}, [], [], 0
        for sd in range(n1):
            s, ev = scen_savo(fixed)
            tr = simulate(s, hours=12, dt=2.0, seed=11 + sd, events=ev)
            cooks += len(s.cook_events)
            if s.lost:
                lost_t.append(tr[-1]["min"]); k = s.lost.split(" ")[0]
                causes[k] = causes.get(k, 0) + 1
            o = next((r["min"] for r in tr if r["min"] > 16 and r["fires"] == 0), None)
            outs.append(o if (o and not s.lost) else 9999)
            p_ = first_t(tr, lambda r: not r["power"])
            if p_ is not None: pw.append(p_)
            if sd == 0:
                ex = s
        outs.sort()
        med = outs[len(outs) // 2]
        print(f"{lab}\n    lost {len(lost_t)}/{n1} {causes if causes else ''}"
              f"{' (median %.1f h after the first hit)' % (sorted(lost_t)[len(lost_t)//2] / 60) if lost_t else ''}; "
              f"all fires out (median run) {('%.0f min' % med) if med < 9999 else 'never'}; "
              f"power lost in {len(pw)}/{n1} runs; cook-offs/run {cooks/n1:.1f}")
        shown = 0
        for e in ex.log:
            if any(k in e[1] for k in ("internal explosion", "ABANDONED", "MAGAZINE", "flooding magazine",
                                         "capsized", "foundered", "turn to", "power")) and shown < 10:
                print(f"{'':<8}seed 0 {e[0]/60:5.0f} min  {e[1]}"); shown += 1

    print("\n=== 2. Destroyer: minutes to the first hose (Monte Carlo) ===")
    n = 6 if quick else 20
    for kind in ("shell", "kamikaze"):
        for fh in (1.0, 6.0):
            losses = cooks = 0; outs = []; spread = 0
            for sd in range(n):
                s, ev = scen_dd_fire(fh, kind, sd)
                tr = simulate(s, hours=1.5, dt=2.0, seed=100 + sd, events=ev)
                losses += bool(s.lost); cooks += len(s.cook_events)
                spread += max(r["fires"] for r in tr)
                o = next((r["min"] for r in tr if r["min"] > 1 and r["fires"] == 0), None)
                outs.append(o if o else 999)
            outs.sort()
            med = outs[len(outs) // 2]
            print(f"{kind:<9} first hose {fh:3.0f} min: fires out median {('%.0f min' % med) if med < 999 else '>90 min':>8}, "
                  f"80th pct {('%.0f min' % outs[int(.8*n)]) if outs[int(.8*n)] < 999 else '>90 min':>8}, "
                  f"peak burning spaces {spread/n:.1f}, cook-offs/run {cooks/n:.2f}, lost {losses}/{n}")

    print("\n=== 3. Fire-main break above the main deck (Ralph Talbot type) ===")
    for fast, lab in ((True, "break isolated after ~3 min"), (False, "break not found for 2.5 h")):
        s, ev = scen_talbot(fast)
        tr = simulate(s, hours=4, dt=2.0, seed=5, events=ev, max_heel=40)
        high = sum(c.water * RHO for c in s.comps if c.kind in ("upper", "super"))
        print(report(s, tr, lab) + f"\n{'':<50} water above the waterline {high:.0f} t, "
              f"max heel {max(abs(r['heel']) for r in tr):.1f}°, solid GM now {s.gm_static:+.2f} m")

    print("\n=== 4. Battleship in action, torpedo + three bombs: who gets the repair parties? ===")
    n4 = 3 if quick else 8
    for doc, lab in (("usn43", "doctrine: fire-main breaks, fires near magazines first"),
                     ("flood_first", "doctrine: flooding boundaries first")):
        outs, cooks, mh, water, lost, pk = [], 0, [], [], 0, []
        for sd in range(n4):
            s, ev = scen_bb_combined(doc)
            tr = simulate(s, hours=4, dt=2.0, seed=3 + sd, events=ev)
            o = next((r["min"] for r in tr if r["min"] > 3 and r["fires"] == 0), None)
            outs.append(o if o else 9999); cooks += len(s.cook_events); lost += bool(s.lost)
            mh.append(max((c.mag["heat"] for c in s.comps if c.mag), default=0))
            water.append(tr[-1]["water"]); pk.append(max(abs(r["heel"]) for r in tr))
        outs.sort()
        print(f"{lab}\n    all fires out within 4 h in {sum(o < 9999 for o in outs)}/{n4} runs "
              f"(median {('%.0f min' % outs[n4 // 2]) if outs[n4 // 2] < 9999 else '>4 h'}); "
              f"cook-offs/run {cooks/n4:.1f}; hottest magazine (mean) {sum(mh)/n4:.2f}, max {max(mh):.2f}; "
              f"water {sum(water)/n4:.0f} t; peak list {max(pk):.1f}°; lost {lost}/{n4}")
