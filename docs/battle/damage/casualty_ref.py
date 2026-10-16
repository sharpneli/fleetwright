"""casualty_ref.py -- reference for 16_casualties.md (Fleetwright damage research).

Killed and wounded per hit, fitted to the war-damage record instead of crew density x burst area with guessed
80% / 20% shares.

    python casualty_ref.py            # every table the note quotes (about 30 s: the fit is a grid search)
    python casualty_ref.py records    # just the record tables (16_casualty_records.csv beside this file)

Pure Python, no dependencies. Units: m, m^2, m^3, kg, mm, MPa.
Tags: [SRC] sourced, [FIT] fitted here to the record, [EST] judgement.

The model, per hit, per group of men (a cell's battle crew, or part of one):
    fragments   lam = S * N_f(r) * g * A * T(t, r) * band / (4 pi r^2)   man-stopping hits on one exposed man
                P(casualty) = 1 - exp(-lam)     P(killed) = 1 - exp(-q lam)
                N_f(r): 13_fragmentation's burst (Gurney, Mott, drag), the pieces still carrying 80 J at r;
                a share `cover` of the men are exposed, the rest take a tenth of the flux
    secondary   a shell that perforates a plate without bursting drives the plate's disc ahead of it in a cone
                (Saumarez's and Gneisenau's director crews); an armour plate that holds spalls on some hits
    blast       confined: the space's quasi-static pressure (11_blast_confinement) -> killed / out of action
                open:     killed within 1.3 W^1/3, out of action to 3.2 W^1/3 (11 sec. 5.1)
    propellant  a flash in the turret train or a casemate battery: a share of everyone it reaches
Mechanisms combine as independent risks. No random factor afterwards: the scatter comes from where the hit lands,
which way the fragment ring points, whether the plate spalls, and binomial draws on the men.
"""
import csv
import math
import os
import random
import statistics as st
import sys
from dataclasses import dataclass

HERE = os.path.dirname(os.path.abspath(__file__))

# =============================================================================================================
# 1. Shells
# =============================================================================================================
FILLER_RE = {"TNT": 1.0, "TNA": 1.0, "ExpD": 0.95, "Lyddite": 1.12, "Shimose": 1.12, "Shellite": 1.05,
             "Pyroxylin": 0.9, "BP": 0.4, "Amatol": 1.0}


@dataclass
class Shell:
    name: str
    cal: float      # m
    mass: float     # kg
    filler: float   # kg
    expl: str
    kind: str       # AP | SAP | HE | common

    @property
    def w_tnt(self):
        return self.filler * FILLER_RE[self.expl]

    @property
    def q(self):
        return q_kill(self)


SHELLS = {s.name: s for s in [
    Shell("4in HE (WWI 10.5cm)", 0.105, 17.4, 1.5, "TNT", "HE"),
    Shell("5in/38 AAC", 0.127, 25.0, 3.3, "ExpD", "HE"),
    Shell("5.5in HE (Fr 138.6)", 0.139, 40.0, 3.0, "TNT", "HE"),
    Shell("6in HE (WWII)", 0.152, 47.6, 6.0, "ExpD", "HE"),
    Shell("6in common (WWI 15cm)", 0.15, 45.3, 3.0, "TNT", "common"),
    Shell("6in AP (WWII)", 0.152, 59.0, 0.9, "ExpD", "AP"),
    Shell("8in AP (WWII)", 0.203, 125.9, 3.1, "TNA", "AP"),
    Shell("8in HE (WWII)", 0.203, 118.0, 9.7, "ExpD", "HE"),
    Shell("11in APC (KM 28cm)", 0.283, 330.0, 7.0, "TNT", "AP"),
    Shell("11in HE (KM 28cm)", 0.283, 315.0, 23.0, "TNT", "HE"),
    Shell("12in common (Russ. 1904)", 0.305, 331.0, 6.0, "Pyroxylin", "common"),
    Shell("6in common (Russ. 1904)", 0.152, 41.5, 1.5, "Pyroxylin", "common"),
    Shell("6in HE shimose (IJN 1905)", 0.152, 45.4, 4.5, "Shimose", "HE"),
    Shell("12pdr HE (IJN 1905)", 0.076, 5.7, 0.4, "Shimose", "HE"),
    Shell("12in HE shimose (IJN 1905)", 0.305, 386.0, 39.0, "Shimose", "HE"),
    Shell("12in APC (Jutland)", 0.305, 386.0, 11.9, "Lyddite", "AP"),
    Shell("13.5in APC (Jutland)", 0.343, 635.0, 20.0, "Lyddite", "AP"),
    Shell("15in APC (Jutland)", 0.381, 871.0, 27.4, "Lyddite", "AP"),
    Shell("30.5cm APC (KM 1916)", 0.305, 405.0, 11.5, "TNT", "AP"),
    Shell("15in APC (WWII)", 0.381, 879.0, 22.0, "Shellite", "AP"),
    Shell("38cm APC (KM)", 0.380, 800.0, 21.4, "TNT", "AP"),
    Shell("14in AP (IJN T91)", 0.356, 673.5, 11.1, "TNA", "AP"),
    Shell("16in AP Mk8", 0.406, 1225.0, 18.6, "ExpD", "AP"),
    Shell("16in HC Mk13", 0.406, 862.0, 69.7, "ExpD", "HE"),
]}

# =============================================================================================================
# 2. Fragments
# =============================================================================================================
# Fragment numbers come from 13_fragmentation (fragmentation_ref.py, imported: one implementation). Gurney gives
# the launch speed, Mott the count and masses, TP-12's drag law the speed at r. A fragment puts a man out of action
# if it still carries 80 J where it reaches him (58 ft-lb; 14 and 15 use the same) [SRC]. What matters for people:
# a bigger shell breaks into bigger pieces, not more of them. A 16in AP shell throws ~300 man-stopping pieces, a
# 5in AA shell ~800 (sec. 2.3).
import fragmentation_ref as FR

E_CASUALTY = 80.0     # J [SRC]
A_MAN = 0.5           # m^2: a standing man's presented area, as 15 [SRC Zecevic: standing 0.5, crouched 0.37]
# Cover by kind of space: the share of the men there who see the burst; the rest take SHIELDED of its flux [EST]
COVER = {"open": 0.8,        # weather deck, AA platform, open bridge
         "station": 0.7,     # bridge, CIC, director, signal bridge: crowded, light fittings
         "flat": 0.6,        # mess deck, cabins, passages, repair-party stations
         "battery": 0.6,     # casemate battery, gun deck
         "gunhouse": 0.45,   # turret: breeches, cradles, rammers
         "handling": 0.45,
         "machinery": 0.3}   # boilers, turbines, pumps between the burst and most of the watch
BAND_P, BAND_IN, BAND_OUT = 0.26, 2.9, 0.34   # the side-spray ring, coarsened to a room (13 sec. 2.5) [EST]
G_CONTACT = 0.5       # a burst on a plate throws half its fragments into the plate (and through it, if thin)
FRAG_TABLE = [(5, 0.080, 0.20), (20, 0.0213, 0.093), (50, 0.0158, 0.05), (100, 0.0113, 0.03),
              (1000, 0.0006, 0.002)]   # Okun: STS defeated by 50% / 10% of fragments, in calibres [SRC, as gunfire_ref]
S_FIT = 8.66          # multiplier on the free-field flux, for people only [FIT, sec. 4.2]; fit() resets it
# 13's field is calibrated for plates and stays as it is for parts. Inside a steel ship people meet more than the
# free-field count: ricochets off the boundaries, debris from fittings and structure, and Mott's short count of small
# pieces [EST]. S carries all of that, and the stand-in ships' placement besides.
Q_FRAG = 0.04         # the chance one man-stopping fragment kills rather than wounds [FIT, sec. 4.2]; fit() resets it.
# Heavier pieces should kill more often, but the record doesn't want it: a 16in AP hit is no deadlier per man in
# reach than a 5in HE hit (R2), so q is one number.

_EXPL = {"TNT": "TNT", "TNA": "TNA", "ExpD": "Explosive D", "Lyddite": "Lyddite", "Shimose": "Shimose",
         "Shellite": "Shellite", "Pyroxylin": "Wet guncotton", "BP": "Black powder", "Amatol": "Amatol 60/40"}
_KIND = {"AP": "AP", "SAP": "Common", "common": "Common", "HE": "HE"}
_BURSTS, _NEFF = {}, {}


def frag_burst(s: "Shell"):
    if s.name not in _BURSTS:
        steel = "Ni-Cr AP body" if s.kind == "AP" else "forged carbon"
        _BURSTS[s.name] = FR.burst(FR.Shell(s.name, s.cal * 1000, s.mass, s.filler, _EXPL[s.expl], _KIND[s.kind], steel))
    return _BURSTS[s.name]


def _m_min(v0, r):
    """Smallest fragment launched at v0 that still carries E_CASUALTY at r (bisection in log m)."""
    lo, hi = 1e-6, 50.0
    if 0.5 * hi * FR.v_at(v0, hi, r) ** 2 < E_CASUALTY:
        return None
    for _ in range(40):
        mid = math.sqrt(lo * hi)
        if 0.5 * mid * FR.v_at(v0, mid, r) ** 2 >= E_CASUALTY:
            hi = mid
        else:
            lo = mid
    return hi


def n_frag(s: "Shell", r=5.0):
    """Man-stopping fragments of the burst at r m: body, ogive and tail sprays (the nose slug and base plug are
    single pieces and left out)."""
    key = (s.name, round(r * 4) / 4)
    if key not in _NEFF:
        b = frag_burst(s)
        n = 0.0
        for m_c, v0 in ((b.body_m, b.v_body), (b.ogive_m, b.v_ogive), (b.tail_m, b.v_tail)):
            if m_c <= 0 or v0 <= 0:
                continue
            mm = _m_min(v0, key[1])
            if mm is not None:
                n += m_c / (2 * b.mk ** 2) * math.exp(-math.sqrt(mm) / b.mk)
        _NEFF[key] = n
    return _NEFF[key]


def q_kill(s: Shell):
    """The chance one man-stopping fragment kills rather than wounds."""
    return Q_FRAG


def _t50_t10(s, r):
    d = max(5.0, r / s.cal)
    for a, b in zip(FRAG_TABLE, FRAG_TABLE[1:]):
        if d <= b[0]:
            f = (math.log(d) - math.log(a[0])) / (math.log(b[0]) - math.log(a[0]))
            t50 = math.exp(math.log(a[1]) + f * (math.log(b[1]) - math.log(a[1])))
            t10 = math.exp(math.log(a[2]) + f * (math.log(b[2]) - math.log(a[2])))
            k = 1.35 if s.kind == "HE" else 1.0
            return t50 * k * s.cal * 1000, t10 * k * s.cal * 1000
    return 1e-3, 2e-3


def transmit(s, t_mm, r):
    """Share of fragments that get through t_mm of plate at r: logistic in log t through Okun's 50% and 10%."""
    if t_mm <= 0:
        return 1.0
    t50, t10 = _t50_t10(s, r)
    sig = max(0.05, math.log(t10 / t50) / math.log(9))
    x = (math.log(t_mm) - math.log(t50)) / sig
    return 1.0 / (1.0 + math.exp(min(60, x)))


def lam_burst(s, r, t_mm=0.0, g=1.0, band=1.0):
    """Expected man-stopping fragments on one exposed man at r."""
    return S_FIT * n_frag(s, r) * g * A_MAN * transmit(s, t_mm, r) * band / (4 * math.pi * max(r, 0.4) ** 2)


SHIELDED = 0.1        # a man the clutter hides still takes this share of an exposed man's fragments [EST]


def frag_risk(lam, q, cov, g: "Group"):
    """(P killed, P casualty) of one man of the group, averaged over where he stands and whether he's exposed
    (share cov of the men see the burst, the rest take SHIELDED of its flux)."""
    def pk(r):
        x = lam(r)
        return cov * (1 - math.exp(-q * x)) + (1 - cov) * (1 - math.exp(-q * SHIELDED * x))

    def pc(r):
        x = lam(r)
        return cov * (1 - math.exp(-x)) + (1 - cov) * (1 - math.exp(-SHIELDED * x))
    return _avg(pk, g.r0, g.r1, g.dim), _avg(pc, g.r0, g.r1, g.dim)


# Secondary debris from a plate a shell perforates without bursting: the disc of ~1.5 calibres it pushes out,
# broken into ~10 g pieces, thrown ahead in a 35 deg half-angle cone at the shell's speed. [EST, sec. 3.3]
SEC_DISC_CAL, SEC_PIECE_KG, SEC_HALF_ANGLE, SEC_Q = 1.5, 0.010, 35.0, 0.35
SPALL_SHARE = 0.15    # an armour plate that holds still throws this share of the disc off its back [EST, Tiger Q]
P_SPALL = 0.25        # ... on this share of the hits it stops: Tiger's Q roof, Von der Tann's after CT, Markgraf's
                      # casemate and Princess Royal's X turret among ~25 armour hits in R1 [EST]
PORT_LEAK = 0.03      # share of an outside burst's flux that enters an armoured mount through ports and slits [EST]


def n_secondary(s, t_mm):
    m = 7850 * (t_mm / 1000) * math.pi * (SEC_DISC_CAL * s.cal / 2) ** 2
    return m / SEC_PIECE_KG


def omega_cone(half_deg=SEC_HALF_ANGLE):
    return 2 * math.pi * (1 - math.cos(math.radians(half_deg)))


# =============================================================================================================
# 3. Blast (from 11_blast_confinement: QSP, Bowen lethality, eardrum/lung disability)
# =============================================================================================================
def qsp_tnt(w, v):
    """11 sec. 1.2 for a TNT-type filler: 0.28 [Qd + phi (Qc - Qd)] W / V, phi = 1 / (1 + 2.66 W / V)."""
    return 0.28 * (4.5 + 10.5 / (1 + 2.66 * w / v)) * w / v


def lethal_share(p):
    return 0.0 if p <= 0 else 0.5 * (1 + math.erf(math.log(p / 0.42) / (0.174 * math.sqrt(2))))


def disabled_share(p):
    return 0.0 if p <= 0 else 0.5 * (1 + math.erf(math.log(p / 0.12) / (0.5 * math.sqrt(2))))


# =============================================================================================================
# 4. One hit on groups of men
# =============================================================================================================
@dataclass
class Group:
    """Men a hit can reach: n of them between r0 and r1 m from the burst (or the shot line), spread over a
    layer (dim 2: a deck, a platform) or a volume (dim 3: a room)."""
    n: float
    r0: float
    r1: float
    cover: str = "flat"
    dim: int = 3
    t_mm: float = 0.0          # plate between the burst and them (0: same space or open)
    confined: bool = False     # in the burst space's gas (blast)
    cone: float = 0.0          # for a shot that doesn't burst: the share of them in the debris cone
    path: float = 0.0          # ... and the share in the shot's own path
    plate_mm: float = 0.0      # ... the plate it perforated just before reaching them
    label: str = ""


# Propellant flash: (killed, wounded) share of the men it reaches [FIT]. An enclosed turret train: Derfflinger's C
# turret 73 killed and 5 wounded of 78; Seydlitz's two after turrets ~160 of ~165. An open casemate battery, where
# men can get out: Malaya's 121 men, 65 dead (with those who died of burns) and 39 wounded.
PROP_TURRET = (0.92, 0.06)
PROP_BATTERY = (0.54, 0.32)


@dataclass
class Hit:
    shell: str
    mode: str                  # burst_in | burst_open | burst_contact | through | armour | dud | propellant
    groups: list
    v: float = 0.0             # burst space volume for the QSP (burst_in)
    prop_share: tuple = (0.92, 0.06)  # propellant: (killed, wounded) share of the men the flash reaches
    note: str = ""


def _avg(fn, r0, r1, dim, n=24):
    """Mean of fn(r) over men spread evenly between r0 and r1 (weights r^(dim-1))."""
    if r1 <= r0:
        return fn(r0)
    tot = wsum = 0.0
    for i in range(n):
        r = r0 + (i + 0.5) * (r1 - r0) / n
        w = r ** (dim - 1)
        tot += fn(r) * w
        wsum += w
    return tot / wsum


def group_risk(h: Hit, g: Group, band=1.0, spall=True):
    """-> (P killed, P casualty) for one man of the group."""
    s = SHELLS[h.shell]
    cov = COVER[g.cover]
    pk_terms, pc_terms = [], []

    if h.mode in ("burst_in", "burst_open", "burst_contact", "armour"):
        gg = G_CONTACT if h.mode in ("burst_contact", "armour") else 1.0
        if h.mode == "armour" and g.t_mm > 0:                  # men behind the plate that held
            def lam(r):
                return (lam_burst(s, r, 0, gg, band) * PORT_LEAK
                        + (S_FIT * SPALL_SHARE * n_secondary(s, g.t_mm) * A_MAN / (omega_cone() * max(r, 0.4) ** 2)
                           * (g.cone or 0.5) if spall else 0.0))
            qq = SEC_Q
        else:
            def lam(r):
                return lam_burst(s, r, g.t_mm, gg, band)
            qq = s.q
        a, b = frag_risk(lam, qq, cov, g)
        pk_terms.append(a)
        pc_terms.append(b)

        # blast
        w = s.w_tnt
        if h.mode == "burst_in" and g.confined and h.v > 0:
            p = qsp_tnt(w, h.v)
            pk_terms.append(lethal_share(p))
            pc_terms.append(max(lethal_share(p), disabled_share(p)))
        elif g.t_mm == 0 and h.mode != "armour":
            rk, rd = 1.3 * w ** (1 / 3), 3.2 * w ** (1 / 3)
            pk_terms.append(_avg(lambda r: 1.0 if r < rk else 0.0, g.r0, g.r1, g.dim))
            pc_terms.append(_avg(lambda r: 1.0 if r < rk else (0.5 if r < rd else 0.0), g.r0, g.r1, g.dim))

    elif h.mode in ("through", "dud"):
        # the shot itself: whoever is in its path (radius half a calibre plus a man's half-width)
        if g.path:
            pk_terms.append(g.path)
            pc_terms.append(g.path)
        if g.cone and g.plate_mm > 0:
            nsec = n_secondary(s, g.plate_mm)

            def lam(r):
                return S_FIT * nsec * A_MAN / (omega_cone() * max(r, 0.4) ** 2)
            a, b = frag_risk(lam, SEC_Q, cov, g)
            pk_terms.append(g.cone * a)
            pc_terms.append(g.cone * b)

    elif h.mode == "propellant":
        return h.prop_share[0], h.prop_share[0] + h.prop_share[1]

    pk = 1 - math.prod(1 - x for x in pk_terms)
    pc = 1 - math.prod(1 - x for x in pc_terms)
    return pk, max(pk, pc)


def expected(h: Hit):
    """Expected (killed, wounded) of one hit, averaged over the fragment band's direction."""
    K = W = 0.0
    spalls = [(P_SPALL, True), (1 - P_SPALL, False)] if h.mode == "armour" else [(1.0, True)]
    for g in h.groups:
        bands = [(1.0, 1.0)] if h.mode in ("through", "dud", "propellant") else [(BAND_P, BAND_IN), (1 - BAND_P, BAND_OUT)]
        for pb, b in bands:
            for ps, sp in spalls:
                pk, pc = group_risk(h, g, b, sp)
                K += ps * pb * g.n * pk
                W += ps * pb * g.n * (pc - pk)
    return K, W


def sample(h: Hit, rnd):
    """One draw: each group's band drawn once (a group sits in the ring or not), then binomial men."""
    K = W = 0
    sp = rnd.random() < P_SPALL
    for g in h.groups:
        b = 1.0 if h.mode in ("through", "dud", "propellant") else (BAND_IN if rnd.random() < BAND_P else BAND_OUT)
        pk, pc = group_risk(h, g, b, sp)
        n = int(g.n) + (1 if rnd.random() < g.n - int(g.n) else 0)
        for _ in range(n):
            x = rnd.random()
            if x < pk:
                K += 1
            elif x < pc:
                W += 1
    return K, W


# =============================================================================================================
# 5. The current rule (gunfire_ref / Effects.cs): density x (0.8 pi Rw^2 + 0.2 pi (Rb^2 - Rw^2)) x U(0.3, 1.7)
# =============================================================================================================
def current_rule(shell, dens_per_m2, burst=True, depth=10.0, k=1.0):
    s = SHELLS[shell]
    if not burst:
        return dens_per_m2 * depth * 0.5
    rw, rb = 1.6 * s.w_tnt ** (1 / 3) * math.sqrt(k), 3.5 * s.w_tnt ** (1 / 3) * math.sqrt(k)
    return dens_per_m2 * (0.8 * math.pi * rw ** 2 + 0.2 * math.pi * (rb ** 2 - rw ** 2))


# =============================================================================================================
# 6. The record (16_casualty_records.csv): per-hit classes and ship-level groups
# =============================================================================================================
def load_records(path=os.path.join(HERE, "16_casualty_records.csv")):
    if not os.path.exists(path):
        return None
    out = []
    with open(path, newline="", encoding="utf-8") as f:
        for r in csv.DictReader(f):
            for k in ("killed_n", "wounded_n", "hits_n"):
                r[k] = float(r[k]) if r.get(k, "") not in ("", None) else None
            out.append(r)
    return out


CLASS_NAMES = {
    "A": "burst at a manned station (bridge, director, AA platform, gun, CIC)",
    "B": "burst in other spaces (flats, mess decks, machinery, upper deck)",
    "C": "passed through without bursting",
    "D": "dud",
    "E": "on armour (defeated, or burst on the plate outside)",
    "F": "propellant flash or fire (turret train, battery, magazine)",
    "G": "near miss",
}


def table_record_classes(rec):
    print("## R1. Per-hit record by what the hit did (hits with a stated killed count)\n")
    print("| Class | Hits | Killed none | Median killed | Mean killed | Max | With wounded stated | Mean K / W | Killed share of casualties |")
    print("|---|---|---|---|---|---|---|---|---|")
    for c, name in CLASS_NAMES.items():
        h = [r for r in rec if r["level"] == "hit" and r["class"] == c and r["killed_n"] is not None]
        if not h:
            continue
        ks = [r["killed_n"] for r in h]
        kw = [r for r in h if r["wounded_n"] is not None]
        K = sum(r["killed_n"] for r in kw)
        W = sum(r["wounded_n"] for r in kw)
        share = f"{K / (K + W):.2f}" if K + W else "–"
        print(f"| {c}: {name} | {len(h)} | {sum(1 for x in ks if x == 0) / len(h):.0%} | {st.median(ks):.0f} | {st.mean(ks):.1f} | "
              f"{max(ks):.0f} | {len(kw)} | {K / max(1, len(kw)):.1f} / {W / max(1, len(kw)):.1f} | {share} |")


GROUP_NAMES = {
    "G1": "WWI capital ships, heavy APC (Dogger Bank, Jutland; no flash fires, no sinkings)",
    "G2a": "Russian battleships hit by Japanese shimose HE, 1904-05",
    "G2b": "Japanese battleships and cruisers hit by Russian AP and common, 1904-05",
    "G3": "WWII capital ships, heavy AP",
    "G4": "WWII capital ships and cruisers, medium HE at close range (bombardment, crowded AA)",
    "G5": "WWII cruisers, 6-11in AP and HE",
    "G6a": "WWI cruisers fighting from behind armour, 4.1in HE (Falklands, Heligoland, Coronel)",
    "G6b": "WWI light cruisers with open-backed gun shields, 4.1-5.9in at close range (Jutland)",
    "G7": "Destroyers, WWII, 4.7-8in",
}


def table_record_groups(rec):
    print("\n## R2. Ship-level record: killed and wounded per hit\n")
    print("| Group | Ships | Hits | Killed / hit | Wounded / hit | Killed share | Ships' spread (killed / hit) |")
    print("|---|---|---|---|---|---|---|")
    out = {}
    for gk, name in GROUP_NAMES.items():
        sh = [r for r in rec if r["level"] == "ship" and r["group"] == gk and r["hits_n"]]
        if not sh:
            continue
        n = sum(r["hits_n"] for r in sh)
        K = sum(r["killed_n"] for r in sh)
        sw = [r for r in sh if r["wounded_n"] is not None]
        nW = sum(r["hits_n"] for r in sw)
        W = sum(r["wounded_n"] for r in sw)
        Kw = sum(r["killed_n"] for r in sw)
        per = sorted(r["killed_n"] / r["hits_n"] for r in sh)
        out[gk] = (K / n, W / nW if nW else None)
        print(f"| {gk}: {name} | {len(sh)} | {n:.0f} | {K / n:.2f} | {W / nW:.2f} | {Kw / (Kw + W):.2f} | "
              f"{per[0]:.1f}–{per[-1]:.1f} (median {st.median(per):.1f}) |")
    return out


# =============================================================================================================
# 7. Single hits reconstructed: the men present are [EST] from the ship's plan and the report's words
# =============================================================================================================
CASES = [
    ("Colorado 1945, 5in AAC at the air defense station", 20, 48,
     Hit("5in/38 AAC", "burst_contact", [Group(45, 0.8, 5, "open", 2, label="air defense station"),
                                         Group(70, 5, 14, "open", 2, t_mm=3, label="platforms and tubs around")])),
    ("North Carolina 1945, 5in AAC at the base of a 5in director", 3, 44,
     Hit("5in/38 AAC", "burst_contact", [Group(8, 1, 3, "station", 3, t_mm=13, label="director, 1/2in"),
                                         Group(60, 3, 14, "open", 2, t_mm=6, label="20 and 40mm crews, shields")])),
    ("Cleopatra 1942, 6in on the after bridge", 15, 5,
     Hit("6in HE (WWII)", "burst_contact", [Group(28, 0.8, 7, "station", 2, label="bridge and ADP")])),
    ("Nachi 1943, 6in on the signal bridge", 11, 21,
     Hit("6in HE (WWII)", "burst_contact", [Group(25, 0.8, 7, "station", 2, label="signal bridge"),
                                            Group(25, 6, 14, "station", 2, t_mm=6, label="bridge decks around")])),
    ("Tiger 1915, 11in APC in the intelligence office under the CT", 7, 9,
     Hit("30.5cm APC (KM 1916)", "burst_in", [Group(12, 0.8, 3, "station", 3, confined=True, label="office"),
                                              Group(20, 3, 8, "station", 3, t_mm=10, label="passages, CT base")], v=60)),
    ("Mikasa 1904, 12in on the forecastle", 7, 16,
     Hit("12in common (Russ. 1904)", "burst_open", [Group(25, 1, 12, "open", 2, label="12pdr crews, fire party"),
                                                     Group(40, 4, 15, "battery", 2, t_mm=6, label="battery below")])),
    ("Murphy 1942, 5.5in in the after engine room", 3, 25.5,
     Hit("5.5in HE (Fr 138.6)", "burst_in", [Group(28, 0.8, 7, "machinery", 3, confined=True, label="engine room watch")],
         v=700)),
    ("Wichita 1942, 194mm below decks", 0, 14,
     Hit("8in HE (WWII)", "burst_in", [Group(2, 1, 4, "flat", 3, confined=True, label="burst space"),
                                       Group(30, 4, 12, "flat", 3, t_mm=6, label="spaces around")], v=250)),
    ("De Ruyter 1942, 8in in the auxiliary motor room", 1, 6,
     Hit("8in AP (WWII)", "burst_in", [Group(6, 1, 5, "machinery", 3, confined=True, label="motor room"),
                                       Group(10, 5, 10, "flat", 3, t_mm=8, label="next spaces")], v=300)),
    ("Saumarez 1943, 11in through the director control tower", 11, 0,
     Hit("11in APC (KM 28cm)", "through", [Group(11, 0.3, 2.5, "station", 3, cone=0.85, path=0.15, plate_mm=10,
                                                 label="DCT crew")])),
    ("Gneisenau 1940, 15in through the foretop director", 6, 0,
     Hit("15in APC (WWII)", "through", [Group(10, 0.3, 3, "station", 3, cone=0.55, path=0.1, plate_mm=20,
                                             label="director crew")])),
    ("Boise 1942 hit 3, through the side and 3rd deck", 0, 1,
     Hit("6in AP (WWII)", "through", [Group(3, 0.3, 8, "flat", 3, cone=0.15, plate_mm=12, label="few men near")])),
    ("Von der Tann 1916, 15in on the after CT", 3, 0,
     Hit("15in APC (Jutland)", "armour", [Group(6, 0.4, 1.5, "station", 3, t_mm=200, cone=0.6, label="after CT")])),
    ("Derfflinger 1916, C turret flash", 73, 5,
     Hit("15in APC (Jutland)", "propellant", [Group(78, 0, 0, label="C turret, working chamber, handling rooms")],
         prop_share=PROP_TURRET)),
    ("Malaya 1916, 6in battery flash", 65, 39,
     Hit("12in APC (Jutland)", "propellant", [Group(121, 0, 0, label="starboard 6in battery")], prop_share=PROP_BATTERY)),
]


def table_cases():
    print("\n## M1. The model on single hits (men present are estimates, record in brackets)\n")
    print("| Hit | Men | Model killed / wounded (expected) | Record | Current rule (expected casualties) |")
    print("|---|---|---|---|---|")
    for name, k, w, h in CASES:
        K, W = expected(h)
        men = sum(g.n for g in h.groups)
        # the current rule on the same men: spread them over the area they occupy
        area = sum((math.pi * (g.r1 ** 2 - g.r0 ** 2)) if g.dim == 2 else (math.pi * (g.r1 ** 2 - g.r0 ** 2))
                   for g in h.groups) or 1
        cur = current_rule(h.shell, men / area, burst=h.mode.startswith("burst"),
                           depth=max(g.r1 for g in h.groups)) if h.mode != "propellant" else float("nan")
        print(f"| {name} | {men:.0f} | {K:.1f} / {W:.1f} | ({k:.0f} / {w:.0f}) | {cur:.1f} |")


# =============================================================================================================
# 8. Ships: where a hit lands and how, and who is there. A coarse stand-in for shipgen's battle_crew cells, to
#    check the per-hit model against the ship-level record (R2) and to fit its two constants. Each situation is
#    (share of hits, Hit). The men are placed as shipgen places them (README: battle_crew): gun crews at their
#    mounts, 0.4 of a turret's crew in its barbette, engineers in the machinery by volume, the command party on the
#    bridge, the rest as repair, first-aid and ammunition parties in the hull's flats by volume. The shares of hits
#    by outcome follow the record (R1 counts; 08's per-hit tables). [EST throughout: sec. 5 says how the engine
#    replaces this with real ships.]
# =============================================================================================================
def situations_ww1_capital(shell, convert_to_contact=False):
    """A Jutland capital ship, ~1,100 men at action stations; the upper deck is empty, men are behind armour."""
    S = shell
    sits = [
        (0.32, Hit(S, "armour", [Group(0.5, 2, 15, "open", 2, label="men on deck near"),
                                 Group(8, 0.5, 3, "gunhouse", 3, t_mm=230, cone=0.3, label="behind the plate")])),
        (0.12, Hit(S, "through", [Group(3, 0.3, 10, "flat", 3, cone=0.3, path=0.05, plate_mm=12)])),
        (0.06, Hit(S, "dud", [Group(3, 0.3, 10, "flat", 3, cone=0.2, path=0.05, plate_mm=12)])),
        (0.20, Hit(S, "burst_in", [Group(4, 0.8, 7, "flat", 3, confined=True, label="flat, repair party"),
                                   Group(12, 6, 15, "flat", 3, t_mm=8, label="next flats")], v=450)),
        (0.10, Hit(S, "burst_in", [Group(30, 1, 12, "battery", 3, confined=True, label="casemate battery"),
                                   Group(12, 4, 10, "handling", 3, t_mm=10, label="ammunition passage")], v=1500)),
        (0.12, Hit(S, "burst_contact", [Group(1, 1, 15, "open", 2, label="superstructure, funnels, boats"),
                                        Group(4, 3, 12, "station", 3, t_mm=8, label="spaces in the superstructure")])),
        (0.04, Hit(S, "burst_contact", [Group(20, 1, 8, "station", 2, label="bridge, fore top, signal")])),
        (0.04, Hit(S, "burst_in", [Group(40, 1, 12, "machinery", 3, confined=True, label="boiler room")], v=1600)),
        (0.15, Hit(S, "burst_in", [], v=300, note="nobody in reach: bunkers, stores, voids, funnels")),
    ]
    if convert_to_contact:      # a nose-fuzed HE bursts on whatever it meets first (shimose, 1905)
        sits = [(p, Hit(S, "burst_contact" if h.mode in ("through", "dud") else h.mode,
                        [Group(g.n, g.r0, g.r1, g.cover, g.dim, g.t_mm, g.confined, label=g.label) for g in h.groups],
                        h.v)) for p, h in sits]
    return sits


def situations_ww2_capital(shell, crowd=1.0):
    """A WWII battleship, ~2,300 men, several hundred of them in open AA mounts; heavy AP at 15-25 km (SoDak's
    8in AP was mostly through the upper works)."""
    S = shell
    return [
        (0.25, Hit(S, "armour", [Group(6 * crowd, 2, 15, "open", 2, label="AA crews near"),
                                 Group(6, 0.5, 3, "gunhouse", 3, t_mm=300, cone=0.2)])),
        (0.25, Hit(S, "through", [Group(6, 0.3, 10, "station", 3, cone=0.3, path=0.05, plate_mm=10)])),
        (0.06, Hit(S, "dud", [Group(4, 0.3, 10, "flat", 3, cone=0.2, path=0.05, plate_mm=12)])),
        (0.20, Hit(S, "burst_in", [Group(4, 0.8, 7, "flat", 3, confined=True),
                                   Group(15, 6, 15, "flat", 3, t_mm=10)], v=500)),
        (0.16, Hit(S, "burst_contact", [Group(12 * crowd, 1, 12, "open", 2, label="AA mounts, open stations"),
                                        Group(15, 3, 12, "station", 3, t_mm=10, label="superstructure spaces")])),
        (0.08, Hit(S, "burst_contact", [Group(30 * crowd, 1, 10, "station", 2, label="bridge, ADP, directors")])),
        (0.15, Hit(S, "burst_in", [], v=300, note="nobody in reach: tanks, stores, voids, uptakes")),
    ]


def situations_ww2_close_he(shell):
    """The same ship under medium HE close in (bombardment duty, Tinian, Iwo Jima): everything bursts on contact,
    and the AA crews are closed up in strength."""
    return [(p, Hit(shell, "burst_contact" if h.mode in ("through", "dud", "armour") else h.mode,
                    [Group(g.n, g.r0, g.r1, g.cover, g.dim, 0 if h.mode in ("through", "dud") else g.t_mm, g.confined)
                     for g in h.groups], h.v)) for p, h in situations_ww2_capital(shell, crowd=1.5)]


def situations_cruiser(shell, crowd=1.0):
    """A WWII heavy or light cruiser, ~900 men."""
    S = shell
    return [
        (0.12, Hit(S, "armour", [Group(2 * crowd, 2, 12, "open", 2), Group(4, 0.5, 2.5, "gunhouse", 3, t_mm=80, cone=0.3)])),
        (0.18, Hit(S, "through", [Group(4, 0.3, 8, "flat", 3, cone=0.3, path=0.05, plate_mm=10)])),
        (0.05, Hit(S, "dud", [Group(4, 0.3, 8, "flat", 3, cone=0.2, path=0.05, plate_mm=10)])),
        (0.25, Hit(S, "burst_in", [Group(5, 0.8, 6, "flat", 3, confined=True), Group(12, 5, 12, "flat", 3, t_mm=6)], v=300)),
        (0.10, Hit(S, "burst_in", [Group(25, 1, 8, "machinery", 3, confined=True)], v=900)),
        (0.20, Hit(S, "burst_contact", [Group(8 * crowd, 1, 10, "open", 2), Group(10, 3, 10, "station", 3, t_mm=6)])),
        (0.10, Hit(S, "burst_contact", [Group(22 * crowd, 1, 8, "station", 2, label="bridge, director, ADP")])),
        (0.10, Hit(S, "burst_in", [], v=300, note="nobody in reach")),
    ]


def situations_ww1_cruiser(shell, guns_open=False):
    """A WWI cruiser, ~400-650 men. An armoured cruiser (County class) fights from casemates and turrets behind
    4in, with coal bunkers outboard of the machinery; a light cruiser's guns stand on the open deck behind
    open-backed shields (Chester's wounded lost legs)."""
    S = shell
    gun = Group(10, 0.8, 8, "open", 2, label="gun crews, open-backed shields") if guns_open else \
        Group(8, 0.5, 3, "battery", 3, t_mm=100, cone=0.3, label="casemate crew behind 4in")
    return [
        (0.10 if guns_open else 0.35, Hit(S, "armour", [Group(0.5, 2, 12, "open", 2),
                                                        Group(6, 0.5, 3, "battery", 3, t_mm=100, cone=0.3)])),
        (0.15, Hit(S, "burst_contact", [Group(1, 1, 10, "open", 2), Group(5, 3, 10, "flat", 3, t_mm=6, label="spaces behind")])),
        (0.20, Hit(S, "burst_in", [Group(3, 0.8, 5, "flat", 3, confined=True), Group(8, 5, 12, "flat", 3, t_mm=6)], v=200)),
        (0.05, Hit(S, "burst_in", [Group(4, 0.8, 5, "flat", 3, confined=True)], v=300)),   # coal bunker, stokers' flat
        (0.30 if guns_open else 0.10, Hit(S, "burst_contact" if guns_open else "armour", [gun])),
        (0.05, Hit(S, "burst_contact", [Group(12, 0.8, 6, "station", 2, label="bridge, fore top")])),
        (0.15 if guns_open else 0.10, Hit(S, "burst_contact", [Group(2, 1, 10, "open", 2, label="funnels, boats, masts")])),
        (0.15, Hit(S, "burst_in", [], v=300, note="nobody in reach: coal bunkers, stores")),
    ]


def situations_destroyer(shell):
    """A WWII destroyer, ~300 men: everything is thin, every HE hit bursts, men are everywhere."""
    S = shell
    return [
        (0.30, Hit(S, "burst_in", [Group(3, 0.8, 5, "flat", 3, confined=True), Group(8, 4, 10, "flat", 3, t_mm=5)], v=150)),
        (0.25, Hit(S, "burst_in", [Group(18, 1, 6, "machinery", 3, confined=True)], v=500)),
        (0.25, Hit(S, "burst_contact", [Group(8, 1, 8, "open", 2, label="guns, tubes, AA"),
                                        Group(6, 3, 9, "flat", 3, t_mm=5)])),
        (0.12, Hit(S, "burst_contact", [Group(16, 0.8, 6, "station", 2, label="bridge, director")])),
        (0.08, Hit(S, "through", [Group(4, 0.3, 6, "flat", 3, cone=0.4, path=0.05, plate_mm=8)])),
        (0.05, Hit(S, "burst_in", [], v=150, note="nobody in reach")),
    ]


def mix(fn, shells):
    """A ship's situations under a mix of shells [(share, shell)]."""
    return [(w * p, h) for w, s in shells for p, h in fn(s)]


SHIP_CHECKS = [
    ("G1", "Jutland capital ship, 13.5in APC", lambda: situations_ww1_capital("13.5in APC (Jutland)")),
    ("G1", "Jutland capital ship, 30.5cm APC", lambda: situations_ww1_capital("30.5cm APC (KM 1916)")),
    ("G2a", "1905 Russian battleship, Japanese shimose: 12in 0.25, 6in 0.5, 12pdr 0.25",
     lambda: mix(lambda s: situations_ww1_capital(s, True), [(0.25, "12in HE shimose (IJN 1905)"),
                                                            (0.5, "6in HE shimose (IJN 1905)"), (0.25, "12pdr HE (IJN 1905)")])),
    ("G2b", "1904 Japanese battleship, Russian common: 12in 0.4, 6in 0.6",
     lambda: mix(situations_ww1_capital, [(0.4, "12in common (Russ. 1904)"), (0.6, "6in common (Russ. 1904)")])),
    ("G3", "WWII battleship, 8in AP (SoDak's mix)", lambda: situations_ww2_capital("8in AP (WWII)")),
    ("G3", "WWII battleship, 15in AP", lambda: situations_ww2_capital("15in APC (WWII)")),
    ("G4", "WWII battleship, 6in HE close in, AA closed up", lambda: situations_ww2_close_he("6in HE (WWII)")),
    ("G5", "WWII cruiser, 8in AP", lambda: situations_cruiser("8in AP (WWII)")),
    ("G5", "WWII cruiser, 11in APC", lambda: situations_cruiser("11in APC (KM 28cm)")),
    ("G6a", "WWI armoured cruiser, 4.1in HE", lambda: situations_ww1_cruiser("4in HE (WWI 10.5cm)")),
    ("G6b", "WWI light cruiser, 5.9in common, open guns", lambda: situations_ww1_cruiser("6in common (WWI 15cm)", True)),
    ("G7", "Destroyer, 5in AAC", lambda: situations_destroyer("5in/38 AAC")),
    ("G7", "Destroyer, 8in HE", lambda: situations_destroyer("8in HE (WWII)")),
]


def ship_expected(sits):
    tot = sum(p for p, _ in sits)
    K = W = 0.0
    for p, h in sits:
        k, w = expected(h)
        K += p * k / tot
        W += p * w / tot
    return K, W


def fit(targets):
    """S_FIT and Q_FRAG, by least squares in log(x + 0.5) of killed and wounded per hit over the record's groups
    (R2, each the mean of its checks) plus log(x + 1) over the single hits (M1), the hits weighted to count as
    much as the groups in all. The free-field prior is S_FIT = 1."""
    global S_FIT, Q_FRAG
    sits = [(gk, f()) for gk, _, f in SHIP_CHECKS]
    cases = [x for x in CASES if x[3].mode != "propellant"]
    best = None
    for i in range(-16, 33):              # S from 0.1 to 100 (16 steps a decade)
        for j in range(2, 31, 2):         # q from 0.02 to 0.30
            S_FIT, Q_FRAG = 10 ** (i / 16), j / 100
            per = {}
            for gk, s_ in sits:
                per.setdefault(gk, []).append(ship_expected(s_))
            eg = 0.0
            for gk, lst in per.items():
                k = st.mean(x[0] for x in lst)
                w = st.mean(x[1] for x in lst)
                rk, rw = targets[gk]
                eg += (math.log(k + 0.5) - math.log(rk + 0.5)) ** 2 + (math.log(w + 0.5) - math.log(rw + 0.5)) ** 2
            ec = 0.0
            for _, k, w, h in cases:
                K, W = expected(h)
                ec += (math.log(K + 1) - math.log(k + 1)) ** 2 + (math.log(W + 1) - math.log(w + 1)) ** 2
            err = eg + ec * len(per) / len(cases)
            if best is None or err < best[0]:
                best = (err, S_FIT, Q_FRAG, eg, ec)
    S_FIT, Q_FRAG = best[1], best[2]
    return best


def ship_mc(sits, n=3000, seed=11):
    rnd = random.Random(seed)
    tot = sum(p for p, _ in sits)
    ks, ws = [], []
    for _ in range(n):
        x = rnd.random() * tot
        for p, h in sits:
            x -= p
            if x <= 0:
                break
        k, w = sample(h, rnd)
        ks.append(k)
        ws.append(w)
    ks_sorted = sorted(ks)
    return dict(K=st.mean(ks), W=st.mean(ws), zero=sum(1 for k, w in zip(ks, ws) if k + w == 0) / n,
                k0=sum(1 for k in ks if k == 0) / n, p50=ks_sorted[n // 2], p90=ks_sorted[int(0.9 * n)],
                p99=ks_sorted[int(0.99 * n)])


def table_ships(targets):
    print("\n## M2. The model on whole ships against the ship-level record (per hit)\n")
    print("| Group | Ship and shell | Model killed / wounded | Record killed / wounded | No casualties | Killed none | Killed: median / 90th / 99th pct |")
    print("|---|---|---|---|---|---|---|")
    for gk, name, f in SHIP_CHECKS:
        m = ship_mc(f())
        rk, rw = targets.get(gk, (float("nan"), None))
        print(f"| {gk} | {name} | {m['K']:.2f} / {m['W']:.2f} | {rk:.2f} / {'–' if rw is None else f'{rw:.2f}'} | "
              f"{m['zero']:.0%} | {m['k0']:.0%} | {m['p50']} / {m['p90']} / {m['p99']} |")


def table_ships_current(targets):
    """The current rule on the same stand-in ships: density = men / (pi R_blast^2) of the space they're in."""
    print("\n## M5. The current rule on the same ships (expected casualties per hit)\n")
    print("| Group | Ship and shell | Current rule | Model (killed + wounded) | Record (killed + wounded) |")
    print("|---|---|---|---|---|")
    for gk, name, f in SHIP_CHECKS:
        sits = f()
        tot = sum(p for p, _ in sits)
        cur = 0.0
        for p, h in sits:
            if not h.groups:
                continue
            men = sum(g.n for g in h.groups)
            area = sum(math.pi * (g.r1 ** 2 - g.r0 ** 2) for g in h.groups)
            burst = h.mode.startswith("burst") or (h.mode == "armour")
            k = 0.15 if h.mode == "armour" else 1.0
            c = current_rule(h.shell, men / area, burst=burst, depth=max(g.r1 for g in h.groups), k=k)
            cur += p * min(c, men) / tot
        K, W = ship_expected(sits)
        rk, rw = targets.get(gk, (float("nan"), 0))
        print(f"| {gk} | {name} | {cur:.1f} | {K + W:.1f} | {rk + (rw or 0):.1f} |")


def table_per_hit_distribution():
    print("\n## M3. One hit, one space: how the model scales with the shell (men spread evenly, 0.02 / m^3)\n")
    print("| Shell | Man-stopping fragments at 5 m | Flat 300 m^3, 6 men: K / W | Engine room 1,500 m^3, 30 men | Open deck, 20 men within 10 m | Bridge, 25 men within 6 m |")
    print("|---|---|---|---|---|---|")
    for nm in ("4in HE (WWI 10.5cm)", "5in/38 AAC", "6in HE (WWII)", "8in AP (WWII)", "8in HE (WWII)", "12in APC (Jutland)",
               "15in APC (WWII)", "16in AP Mk8", "16in HC Mk13"):
        row = [nm, f"{n_frag(SHELLS[nm], 5.0):,.0f}"]
        for h in (Hit(nm, "burst_in", [Group(6, 0.5, 4.2, "flat", 3, confined=True)], v=300),
                  Hit(nm, "burst_in", [Group(30, 0.5, 7.1, "machinery", 3, confined=True)], v=1500),
                  Hit(nm, "burst_open", [Group(20, 0.5, 10, "open", 2)]),
                  Hit(nm, "burst_contact", [Group(25, 0.5, 6, "station", 2)])):
            K, W = expected(h)
            row.append(f"{K:.1f} / {W:.1f}")
        print("| " + " | ".join(row) + " |")


def table_current_vs_model():
    print("\n## M4. The current rule against the model, same men (flat of 300 m^3 with 6 men = 0.06 / m^2 on 10 m x 10 m)\n")
    print("| Shell | Current rule: expected casualties | Model: killed / wounded | Model / current |")
    print("|---|---|---|---|")
    for nm in ("5in/38 AAC", "6in HE (WWII)", "8in HE (WWII)", "12in APC (Jutland)", "16in AP Mk8", "16in HC Mk13"):
        cur = current_rule(nm, 0.06)
        K, W = expected(Hit(nm, "burst_in", [Group(6, 0.5, 4.2, "flat", 3, confined=True),
                                             Group(18, 4.2, 12, "flat", 3, t_mm=6)], v=300))
        print(f"| {nm} | {cur:.1f} | {K:.1f} / {W:.1f} | {(K + W) / cur:.2f} |")


def main():
    rec = load_records()
    targets = {}
    if rec:
        table_record_classes(rec)
        targets = table_record_groups(rec)
    if len(sys.argv) > 1 and sys.argv[1] == "records":
        return
    if targets:
        err, s, q, eg, ec = fit(targets)
        print(f"\nFitted: S_FIT = {s:.2f} (prior 1.0), Q_FRAG = {q:.2f}; log error {eg:.2f} over {len(targets)} groups, "
              f"{ec:.2f} over {sum(1 for x in CASES if x[3].mode != 'propellant')} single hits")
    table_cases()
    table_ships(targets)
    table_ships_current(targets)
    table_per_hit_distribution()
    table_current_vs_model()


if __name__ == "__main__":
    main()
