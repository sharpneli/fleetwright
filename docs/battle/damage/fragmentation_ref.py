"""fragmentation_ref.py -- reference for 13_fragmentation.md (Fleetwright damage research).

What a shell burst throws: how many fragments, what masses and speeds, in what pattern
(the side spray, the nose spray, the AP nose slug, the base plug), how far they keep their
punch, what plate stops them, and what that means for splinter protection, near misses
and service-network cuts.

It replaces the single Okun table (08 §3.6) with a generator built from the shell's own
numbers: calibre, mass, filler mass, explosive (12's catalogue) and the steel of its wall.

  Gurney           -> initial fragment speed from charge / metal ratio          [SRC form + constants]
  Mott             -> fragment count and mass distribution from wall + explosive [SRC form + constants]
  drag             -> V(R) = V0 exp(-R / (L1 m^1/3)),  L1 = 247 m/kg^1/3         [SRC, DDESB TP-12]
  THOR             -> limit velocity of a compact steel fragment against plate    [SRC, armor/01 §3.10]
  vector sum       -> spray direction = shell's own velocity + Gurney kick        [SRC idea: Okun; physics]

Pure Python, no dependencies. `python fragmentation_ref.py` prints every table quoted in the note.
Units: SI inside (kg, m, m/s, mm); THOR is evaluated in its native fps / in / grains.
Tags: [SRC] sourced, [FIT] fitted here to the anchors, [EST] judgement, [UNCERTAIN] thin sources.
"""
from dataclasses import dataclass
from math import sqrt, exp, log, log10, pi, sin, cos, radians, degrees, atan2

# ----------------------------------------------------------------------------------------------
# 1. Explosives: Gurney constant and brisance (Mott B = B_TNT / brisance)
# ----------------------------------------------------------------------------------------------
B_TNT = 0.0779          # Mott constant, TNT in mild/medium steel, lb^1/2 in^-7/6       [SRC IATG 01.80 T23]
# B_TNT reproduces NOL's cast-TNT AISI-1045 cylinders (2.0" ID, 0.25" wall): 17 gr vs 17.5 gr measured.


@dataclass(frozen=True)
class Explosive:
    name: str
    gurney: float      # sqrt(2E), m/s
    brisance: float    # = B_TNT / B_mott; 12's catalogue column, now with a definition


EXPLOSIVES = {e.name: e for e in [
    # gurney: TNT 2438, Comp B 2774, tetryl 2499 [SRC IATG T21]; black powder 640 (90th pct
    # of fragments; 816 for the fastest) [SRC MSIAC L-295]; the rest scaled by detonation
    # velocity (sqrt(2E) ~ D/3) [EST].
    # brisance: 12's column [EST], cross-checked: amatol 80/20 0.62 from NOL's 45 vs 17.5 gr
    # [SRC->FIT]; black powder 0.16 from L-295's Mott constant (units UNCERTAIN) [SRC->FIT];
    # Comp B 1.41 from IATG's B [SRC].
    Explosive("Black powder", 640, 0.15),
    Explosive("Wet guncotton", 2100, 0.90),
    Explosive("Lyddite", 2550, 1.15),
    Explosive("Shimose", 2600, 1.20),
    Explosive("TNT", 2438, 1.00),
    Explosive("TNT/wax 90/10", 2300, 0.90),
    Explosive("Explosive D", 2350, 0.90),
    Explosive("Shellite", 2450, 1.05),
    Explosive("TNA", 2440, 1.00),
    Explosive("Amatol 60/40", 2150, 0.85),
    Explosive("Amatol 80/20", 1900, 0.65),
    Explosive("Comp B", 2774, 1.41),
]}

# ----------------------------------------------------------------------------------------------
# 2. Shell steels: the other half of fragmentation
# ----------------------------------------------------------------------------------------------


@dataclass(frozen=True)
class Steel:
    name: str
    b_factor: float    # multiplies Mott B (mean fragment mass goes as B^2)
    v_factor: float    # multiplies the Gurney speed


STEELS = {s.name: s for s in [
    Steel("cast iron", 0.60, 0.80),        # brittle: fine, slow; v 0.8 [SRC Gurney/IATG]; size [EST]
    Steel("cast steel", 0.85, 0.95),       # wartime substitute bodies [EST]
    Steel("forged carbon", 1.00, 1.00),    # ~AISI 1045, the Mott constants' own basis [SRC]
    Steel("Ni-Cr AP body", 1.25, 1.00),    # tough heat-treated AP/SAP body: larger pieces [EST]
    Steel("HF-1 (postwar)", 0.65, 1.00),   # high-fragmentation steel, 1950s+ [EST]
]}

# ----------------------------------------------------------------------------------------------
# 3. Shell kinds: where the metal and the filler sit
# ----------------------------------------------------------------------------------------------


@dataclass(frozen=True)
class Kind:
    name: str
    nose_whole: float  # share of casing mass leaving as one nose piece (AP slug / HE fuze) [SRC Okun]
    ogive: float       # share of casing breaking up in the ogive -> nose spray            [EST]
    body: float        # share of casing in the cylindrical body -> side spray             [EST]
    base_plug: float   # share leaving as one base plug                                     [EST]
    base_frag: float   # share of the base breaking up -> tail spray                         [EST]
    fill_body: float   # share of the filler alongside the cylindrical body                  [EST]


KINDS = {k.name: k for k in [
    #        name      nose  ogive body  plug  tail  fill_body
    Kind("AP",        0.33, 0.00, 0.55, 0.08, 0.04, 0.90),
    Kind("Common",    0.25, 0.00, 0.63, 0.08, 0.04, 0.85),   # SAP / CPC / base-fuzed common
    Kind("HE",        0.02, 0.20, 0.63, 0.07, 0.08, 0.75),   # nose-fuzed HE / HC / AAC
]}


@dataclass(frozen=True)
class Shell:
    name: str
    calibre_mm: float
    mass_kg: float
    filler_kg: float
    explosive: str
    kind: str
    steel: str


SHELLS = [  # masses and fillers from 08 §2.1 (navweaps) [SRC]; steel by kind [EST]
    Shell("5in/38 AAC Mk 35",   127, 25.0, 3.3, "Explosive D", "HE", "forged carbon"),
    Shell("6in/47 HC Mk 34",    152, 47.6, 6.0, "Explosive D", "HE", "forged carbon"),
    Shell("8in Army HE (Okun)", 203, 109., 7.3, "TNT", "HE", "forged carbon"),
    Shell("IJN 20cm common",    203, 125.9, 8.2, "TNA", "HE", "forged carbon"),
    Shell("IJN 20cm T91 AP",    203, 125.9, 3.11, "TNA", "AP", "Ni-Cr AP body"),
    Shell("8in/55 AP Mk 21",    203, 152., 2.3, "Explosive D", "AP", "Ni-Cr AP body"),
    Shell("14in HC",            356, 578., 47.3, "Explosive D", "HE", "forged carbon"),
    Shell("15in APC Mk Ia",     381, 871., 27.4, "Lyddite", "AP", "Ni-Cr AP body"),
    Shell("16in AP Mk 8",       406, 1225., 18.55, "Explosive D", "AP", "Ni-Cr AP body"),
    Shell("16in HC Mk 13",      406, 862., 69.7, "Explosive D", "HE", "forged carbon"),
    Shell("46cm T0 HE",         460, 1360., 61.7, "TNA", "HE", "forged carbon"),
    Shell("18in CPC (powder)",  457, 1506., 110.2, "Black powder", "Common", "forged carbon"),
    Shell("6in common, cast iron, powder (1880s)", 152, 45.4, 2.7, "Black powder", "Common", "cast iron"),
]
SHELL = {s.name: s for s in SHELLS}

RHO_STEEL, RHO_FILL = 7850.0, 1600.0
LB, IN = 0.45359237, 0.0254
L1_AIR = 247.0            # m / kg^1/3, Cd 1.28, k = 2.6 g/cm3 (forged-steel shell fragments) [SRC TP-12]
L1_WATER = L1_AIR * 1.225 / 1025.0   # same drag law in water: ~0.3 m / kg^1/3                [EST]
K_SHAPE_GR_IN3 = 660.0    # presented area A = (m/k)^(2/3), k in grains/in^3                     [SRC TP-12]
BAND_HALF_DEG = 7.5       # side spray: "nearly all... within an arc of 15 degrees"             [SRC WDR 51]


def gurney_cyl(c, m, g):
    """Cylinder Gurney speed, m/s."""
    if m <= 0 or c <= 0:
        return 0.0
    r = c / m
    return g * sqrt(r / (1 + 0.5 * r))


def gurney_end(c, m, g):
    """Open-faced sandwich (one heavy end plate pushed by c): the kick on a nose slug or base plug."""
    if m <= 0 or c <= 0:
        return 0.0
    mu = m / c
    return g / sqrt(((1 + 2 * mu) ** 3 + 1) / (6 * (1 + mu)) + mu)


@dataclass
class Burst:
    shell: Shell
    casing: float
    body_m: float; body_c: float; v_body: float
    d_in: float; t_in: float
    mk: float           # Mott constant of the body, kg^1/2
    n0: float           # Mott total count of body fragments
    mean_g: float
    ogive_m: float; v_ogive: float
    tail_m: float; v_tail: float
    nose_m: float; plug_m: float; dv_nose: float; dv_plug: float


def burst(s: Shell, low_order=False) -> Burst:
    e, st, k = EXPLOSIVES[s.explosive], STEELS[s.steel], KINDS[s.kind]
    casing = s.mass_kg - s.filler_kg
    body_m, body_c = casing * k.body, s.filler_kg * k.fill_body
    g = e.gurney * st.v_factor * (0.4 if low_order else 1.0)            # low-order 0.4 [EST]
    v_body = gurney_cyl(body_c, body_m, g)
    # wall geometry from the body's charge/metal ratio: x = (d/D)^2
    r = body_c / body_m
    x = r * RHO_STEEL / (RHO_FILL + r * RHO_STEEL)
    D_in = s.calibre_mm / 25.4
    d_in = D_in * sqrt(x)
    t_in = (D_in - d_in) / 2
    B = B_TNT / e.brisance * st.b_factor * (3.0 if low_order else 1.0)  # low-order coarse x3 [EST]
    mk_lb = B * t_in ** (5 / 6) * d_in ** (1 / 3) * (1 + t_in / d_in)   # lb^1/2              [SRC Mott]
    mk = mk_lb * sqrt(LB)
    n0 = body_m / (2 * mk * mk)
    ogive_m = casing * k.ogive
    v_og = gurney_cyl(s.filler_kg * (1 - k.fill_body) * 0.8, ogive_m, g) if ogive_m else 0.0
    tail_m = casing * k.base_frag
    v_tail = gurney_cyl(s.filler_kg * 0.05, tail_m, g)
    nose_m, plug_m = casing * k.nose_whole, casing * k.base_plug
    c_end = s.filler_kg * 0.10                                          # cavity end share [EST]
    return Burst(s, casing, body_m, body_c, v_body, d_in, t_in, mk, n0, 2 * mk * mk * 1000,
                 ogive_m, v_og, tail_m, v_tail, nose_m, plug_m,
                 gurney_end(c_end, nose_m, g), gurney_end(c_end, plug_m, g))


def mass_bins(b: Burst, nb=400, umin=1e-6):
    """Mott N(>m) = N0 exp(-sqrt(m)/Mk) split into log-spaced bins of the count fraction u.
    Returns [(mass kg, count)]. Small u = the largest fragments."""
    out, edges = [], [umin ** (1 - i / nb) for i in range(nb + 1)]
    for lo, hi in zip(edges[:-1], edges[1:]):
        um = sqrt(lo * hi)
        out.append(((b.mk * log(um)) ** 2, b.n0 * (hi - lo)))
    return out


def n_above(b: Burst, m_kg):
    return b.n0 * exp(-sqrt(m_kg) / b.mk)


def v_at(v0, m, s, water=False):
    return v0 * exp(-s / ((L1_WATER if water else L1_AIR) * m ** (1 / 3)))


# ----------------------------------------------------------------------------------------------
# 4. THOR against plate
# ----------------------------------------------------------------------------------------------
THOR = {  # limit-velocity constants c1, a1, b1, g1 (fps, in, in^2, grains)  [SRC armor/01 §3.10]
    "mild": (6.523, 0.906, -0.963, 1.286),
    # STS / hard homogeneous: mild's limit raised by THOR's hard-vs-mild residual offset (10^0.076) [EST]
    "STS": (6.523 + 0.076, 0.906, -0.963, 1.286),
}


def _area_in2(m_gr):
    return (m_gr / K_SHAPE_GR_IN3) ** (2 / 3)


def thor_v50(m_kg, t_mm, mat="mild", obl_deg=0.0):
    c, a, bb, gg = THOR[mat]
    m_gr = m_kg * 15432.358
    e = t_mm / 25.4
    v = 10 ** c * (e * _area_in2(m_gr)) ** a * m_gr ** bb * (1 / cos(radians(obl_deg))) ** gg
    return v * 0.3048


def thor_tmax(m_kg, v, mat="mild", obl_deg=0.0):
    """Thickest plate (mm) a fragment of m_kg at v m/s just defeats."""
    if v <= 0:
        return 0.0
    c, a, bb, gg = THOR[mat]
    m_gr = m_kg * 15432.358
    v_fps = v / 0.3048
    ea = (v_fps / (10 ** c * m_gr ** bb * (1 / cos(radians(obl_deg))) ** gg)) ** (1 / a)
    return ea / _area_in2(m_gr) * 25.4


def side_speed(b: Burst, v_shell):
    """Speed and lean of the side spray for a shell moving at v_shell when it bursts."""
    return sqrt(b.v_body ** 2 + v_shell ** 2), degrees(atan2(v_shell, b.v_body))


def defeat_quantiles(b: Burst, R, v_shell=0.0, mat="STS", qs=(0.5, 0.1, 0.01)):
    """Thickness (mm) defeated by the best q share (by count) of side-spray fragments at R."""
    v0, _ = side_speed(b, v_shell)
    caps = sorted(((thor_tmax(m, v_at(v0, m, R), mat), n) for m, n in mass_bins(b)), reverse=True)
    tot, out = sum(n for _, n in caps), []
    for q in qs:
        acc = 0.0
        for t, n in caps:
            acc += n
            if acc >= q * tot:
                out.append(t)
                break
    return out


def share_through(b: Burst, R, t_mm, mat="STS", obl=0.0, v_shell=0.0, min_g=1.0):
    """(count through, share by count of fragments >= min_g) of the side spray at R through t_mm."""
    v0, _ = side_speed(b, v_shell)
    tot = thru = 0.0
    for m, n in mass_bins(b):
        if m * 1000 < min_g:
            continue
        tot += n
        if v_at(v0, m, R) > thor_v50(m, t_mm, mat, obl):
            thru += n
    return thru, (thru / tot if tot else 0.0)


def band_density(n, R, half_deg=BAND_HALF_DEG):
    """Fragments per m2 in the side band at R (ring perpendicular to the flight path)."""
    return n / (4 * pi * R * R * sin(radians(half_deg)))


def line_hits(n, w_m, R):
    """Expected hits on a line target (cable run, pipe) of width w crossing the band at R.
    Independent of the band's width: n * w / (2 pi R)."""
    return n * w_m / (2 * pi * R)


# ----------------------------------------------------------------------------------------------
# 5. Near misses: a burst on the water beside a ship
# ----------------------------------------------------------------------------------------------

def near_miss(b: Burst, v_strike, fall_deg, case, dist, side_mm=8.0, mat="mild",
              ship_len=110.0, ship_h=7.0, nphi=360, nband=5):
    """Fragments thrown above the water from a contact burst at the surface.
    case 'beam'  : ship parallel to the line of fire, its side `dist` m abeam of the burst (a line error)
    case 'short' : ship across the line of fire, `dist` m ahead of the burst (a short)
    Returns (hits, holes through side_mm) on the side, counting fragments >= 1 g."""
    w = radians(fall_deg)
    vx, vz = cos(w), -sin(w)                      # flight direction, y = 0
    e1 = (sin(w), 0.0, cos(w))                    # unit vectors normal to the flight path
    e2 = (0.0, 1.0, 0.0)
    bins = [(m, n) for m, n in mass_bins(b, nb=30) if m >= 1e-3]
    groups = [(bins, b.v_body, 90.0, BAND_HALF_DEG, 1.0)]
    if b.ogive_m:   # nose spray: ogive pieces, polar 30-80 deg forward [EST]; Mott as the body
        og_scale = b.ogive_m / b.body_m
        groups.append(([(m, n * og_scale) for m, n in bins], b.v_ogive, 55.0, 25.0, 1.0))
    hits = holes = 0.0
    for gb, vg, polar, half, _ in groups:
        for i in range(nphi):
            ph = 2 * pi * (i + 0.5) / nphi
            for j in range(nband):
                pol = radians(polar + half * (2 * (j + 0.5) / nband - 1))
                cr, sr = cos(pol), sin(pol)
                rx = cr * vx + sr * (cos(ph) * e1[0])
                ry = sr * sin(ph)
                rz = cr * vz + sr * (cos(ph) * e1[2])
                ux, uy, uz = v_strike * vx + vg * rx, vg * ry, v_strike * vz + vg * rz
                if uz <= 0:
                    continue                       # into the water: gone within ~1 m
                sp = sqrt(ux * ux + uy * uy + uz * uz)
                if case == "beam":
                    if uy <= 0:
                        continue
                    tt = dist / uy
                    px, pz = ux * tt, uz * tt
                    if abs(px) > ship_len / 2 or pz > ship_h:
                        continue
                    cosn = uy / sp
                else:
                    if ux <= 0:
                        continue
                    tt = dist / ux
                    py, pz = uy * tt, uz * tt
                    if abs(py) > ship_len / 2 or pz > ship_h:
                        continue
                    cosn = ux / sp
                s = sp * tt
                obl = min(80.0, degrees(acos_safe(cosn)))
                wgt = 1.0 / (nphi * nband)
                for m, n in gb:
                    hits += n * wgt
                    if v_at(sp, m, s) > thor_v50(m, side_mm, mat, obl):
                        holes += n * wgt
    return hits, holes


def acos_safe(x):
    from math import acos
    return acos(max(-1.0, min(1.0, x)))


# ----------------------------------------------------------------------------------------------
# 6. Tables
# ----------------------------------------------------------------------------------------------

def table_bursts():
    print("T1  What each burst throws (side spray = cylindrical body; nose/base = single pieces)\n")
    print(f"{'Shell':40}{'wall mm':>8}{'C/M':>6}{'V0 m/s':>8}{'N0':>7}{'N>1g':>7}{'N>50g':>7}"
          f"{'mean g':>8}{'top1% kg':>9}{'nose kg':>8}{'plug kg':>8}")
    for s in SHELLS:
        b = burst(s)
        top = b.mk * b.mk * log(100) ** 2      # mass exceeded by the largest 1 %
        print(f"{s.name:40}{b.t_in*25.4:8.0f}{b.body_c/b.body_m:6.3f}{b.v_body:8.0f}{b.n0:7.0f}"
              f"{n_above(b, 0.001):7.0f}{n_above(b, 0.05):7.0f}{b.mean_g:8.1f}{top:9.2f}"
              f"{b.nose_m:8.0f}{b.plug_m:8.0f}")


def table_anchors():
    print("\nT2  Anchors")
    b = burst(SHELL["6in/47 HC Mk 34"])
    print(f"  6in HC side-spray speed: model {b.v_body/0.3048:.0f} ft/s; WDR 51 measured ~3200 ft/s;"
          f" WDR 51 rule 10,000*sqrt(chg/wt) = {10000*sqrt(6.0/47.6):.0f} ft/s")
    b = burst(SHELL["IJN 20cm T91 AP"])
    big = b.mk * b.mk * log(b.n0) ** 2
    print(f"  IJN 8in AP (Boise hit 5): largest body piece ~{big:.1f} kg; a 2x4 in piece of its"
          f" {b.t_in:.1f} in wall = {2*4*b.t_in*IN**3*RHO_STEEL:.1f} kg")
    print("  Black powder 6in cast-iron common: side spray"
          f" {burst(SHELL['6in common, cast iron, powder (1880s)']).v_body:.0f} m/s"
          " (Okun: ~600 ft/s = 183 m/s for powder CPC middle-body pieces)")


def table_okun():
    print("\nT3  8in HE: STS defeated (calibres) by the best 50% / 10% / 1% of side-spray fragments")
    print("    model (static burst)              vs  Okun's table (via his 0.15x homogeneous method)")
    okun = {5: (0.110, 0.110, 0.110), 20: (0.0213, 0.093, 0.106), 50: (0.0158, 0.075, 0.100),
            100: (0.0113, 0.057, 0.091), 500: (0.0011, 0.0186, 0.0464), 1000: (0.0006, 0.012, 0.023)}
    b = burst(SHELL["8in Army HE (Okun)"])
    D = 203.0
    for cal, ok in okun.items():
        q = defeat_quantiles(b, cal * D / 1000)
        print(f"  {cal:5d} cal ({cal*D/1000:6.1f} m): " + " ".join(f"{x/D:7.4f}" for x in q)
              + "    | " + " ".join(f"{x:7.4f}" for x in ok))


def table_protection():
    print("\nT4  Splinter plating: share of side-spray fragments (>=1 g) through STS at normal, static burst")
    plates = [(6.1, "10 lb"), (12.2, "20 lb"), (15.6, "25 lb"), (18.4, "30 lb"), (24.5, "40 lb"), (38.1, "1.5in")]
    print(f"{'Shell':24}{'R m':>5}" + "".join(f"{p[1]:>8}" for p in plates))
    for name in ["5in/38 AAC Mk 35", "6in/47 HC Mk 34", "IJN 20cm common", "14in HC", "16in HC Mk 13",
                 "16in AP Mk 8", "18in CPC (powder)"]:
        b = burst(SHELL[name])
        for R in (3, 5, 10, 15, 30):
            row = "".join(f"{share_through(b, R, t)[1]*100:7.0f}%" for t, _ in plates)
            print(f"{name[:24]:24}{R:5d}{row}")


def table_density():
    print("\nT5  Side-band density and cable cuts (fragments >= 5 g still faster than 150 m/s)")
    print(f"{'Shell':24}" + "".join(f"{'R='+str(R)+'m':>17}" for R in (2, 5, 10, 20)))
    print(f"{'':24}" + "".join(f"{'per m2 / P(cut)':>17}" for _ in range(4)))
    for name in ["5in/38 AAC Mk 35", "6in/47 HC Mk 34", "IJN 20cm common", "16in AP Mk 8", "16in HC Mk 13"]:
        b = burst(SHELL[name])
        row = ""
        for R in (2, 5, 10, 20):
            n = sum(c for m, c in mass_bins(b) if m >= 0.005 and v_at(b.v_body, m, R) > 150)
            h = line_hits(n, 0.05, R)
            row += f"{band_density(n, R):9.1f} / {1-exp(-h):4.2f} "
        print(f"{name[:24]:24}{row}")


def table_moving():
    print("\nT6  The spray leans forward with the shell's own speed at the burst")
    print(f"{'Shell':24}{'v shell':>8}{'frag speed':>11}{'lean fwd':>9}{'50%/10% STS mm at 5 m':>24}")
    for name, vs in [("6in/47 HC Mk 34", 0), ("6in/47 HC Mk 34", 500), ("16in HC Mk 13", 450),
                     ("16in AP Mk 8", 0), ("16in AP Mk 8", 150), ("16in AP Mk 8", 350),
                     ("IJN 20cm T91 AP", 300)]:
        b = burst(SHELL[name])
        sp, lean = side_speed(b, vs)
        q = defeat_quantiles(b, 5, vs, qs=(0.5, 0.1))
        print(f"{name[:24]:24}{vs:8.0f}{sp:11.0f}{lean:9.0f}{q[0]:12.1f}{q[1]:12.1f}")


def table_pieces():
    print("\nT7  The single pieces: AP nose slug and base plug (shell at v_r when it bursts)")
    print(f"{'Shell':24}{'nose kg':>8}{'kick':>6}{'plug kg':>8}{'kick':>6}"
          f"{'v_r=100: nose/plug':>20}{'v_r=300: nose/plug':>20}")
    for name in ["8in/55 AP Mk 21", "IJN 20cm T91 AP", "15in APC Mk Ia", "16in AP Mk 8", "16in HC Mk 13"]:
        b = burst(SHELL[name])
        r = ""
        for vr in (100, 300):
            r += f"{vr+b.dv_nose:10.0f}/{max(0, vr-b.dv_plug):<9.0f}"
        print(f"{name[:24]:24}{b.nose_m:8.0f}{b.dv_nose:6.0f}{b.plug_m:8.0f}{b.dv_plug:6.0f}  {r}")


def table_near_miss():
    print("\nT8  Near misses on a destroyer (8 mm mild-steel side, 7 m high, 110 m long): hits / holes")
    print("    contact burst at the surface; 'beam' = line error abeam, 'short' = burst short, ship across the line")
    for name, vs, fall in [("5in/38 AAC Mk 35", 300, 20), ("6in/47 HC Mk 34", 400, 15),
                           ("IJN 20cm common", 450, 15), ("14in HC", 480, 15),
                           ("46cm T0 HE", 480, 15), ("46cm T0 HE", 480, 35)]:
        b = burst(SHELL[name])
        row = f"{name[:20]:20} v{vs} fall {fall:2d}: "
        for case in ("beam", "short"):
            for d in (10, 25, 50):
                h, x = near_miss(b, vs, fall, case, d)
                row += f"{case[0]}{d}:{h:5.0f}/{x:<4.0f} "
        print(row)


def table_explosives():
    print("\nT9  Same 6in HC body, other fillers (and a cast-iron body)")
    print(f"{'Filler':16}{'V0':>6}{'N>1g':>7}{'mean g':>8}{'through 10lb STS @5m':>22}{'@15m':>8}")
    base = SHELL["6in/47 HC Mk 34"]
    for ex in ["Black powder", "Amatol 80/20", "Explosive D", "TNT", "Lyddite", "Shimose", "Comp B"]:
        b = burst(Shell("x", base.calibre_mm, base.mass_kg, base.filler_kg, ex, "HE", "forged carbon"))
        print(f"{ex:16}{b.v_body:6.0f}{n_above(b,0.001):7.0f}{b.mean_g:8.1f}"
              f"{share_through(b,5,6.1)[0]:22.0f}{share_through(b,15,6.1)[0]:8.0f}")
    b = burst(Shell("x", base.calibre_mm, base.mass_kg, base.filler_kg, "Lyddite", "HE", "cast iron"))
    print(f"{'Lyddite, cast iron':16}{b.v_body:6.0f}{n_above(b,0.001):7.0f}{b.mean_g:8.1f}"
          f"{share_through(b,5,6.1)[0]:22.0f}{share_through(b,15,6.1)[0]:8.0f}")
    b = burst(base, low_order=True)
    print(f"{'Exp D low-order':16}{b.v_body:6.0f}{n_above(b,0.001):7.0f}{b.mean_g:8.1f}"
          f"{share_through(b,5,6.1)[0]:22.0f}{share_through(b,15,6.1)[0]:8.0f}")


if __name__ == "__main__":
    table_bursts()
    table_anchors()
    table_okun()
    table_protection()
    table_density()
    table_moving()
    table_pieces()
    table_explosives()
    table_near_miss()
