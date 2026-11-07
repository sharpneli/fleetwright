"""store_initiation_ref.py -- reference for 15-what-sets-a-store-off.md (Fleetwright damage research).

What sets an explosive store off, by store, 1890-1945: shock (blast in air, shock in water),
fragments, and heat (propellant lit by hot gas; cased high explosive cooked off by fire).

  S1 shock in air     Kinney-Graham incident overpressure, reflected by Rankine-Hugoniot   [SRC form]
                      against the gap tests' initiation pressures                           [SRC / INFERRED]
  S2 shock in water   Cole's P = 52.4 (W^1/3/R)^1.13 MPa, the distance a pistol or a filler fails
  F1 fragment speed   Gurney, cylinder: v = G sqrt(C/M / (1 + C/2M))                        [SRC form]
  F2 perforation      THOR V50 for a compact steel fragment on mild steel (13's law)        [SRC]
  F3 initiation       Held: v^2 d >= K for the filler, applied to the speed left after the case [SRC form, INFERRED use]
  H1 propellant       surface ignition of a semi-infinite stick by hot gas (convection + gas radiation)
  H2 cook-off         a cased charge's wall heated by a fire until the filler at the wall reaches
                      its critical temperature                                               [EST model]

Pure Python, no dependencies. `python store_initiation_ref.py` prints every table quoted in the note.
Units: SI inside; THOR in its native fps / in / grains.
Tags: [SRC] sourced, [INFERRED] reasoning or a fit, [EST] judgement, [UNCERTAIN] thin sources.
"""
from dataclasses import dataclass
from math import sqrt, exp, log, log10, pi, erfc

P0 = 101_325.0
SIGMA = 5.670e-8

# ------------------------------------------------------------------------------------------------
# 0. Fillers: what each needs
# ------------------------------------------------------------------------------------------------


@dataclass(frozen=True)
class Filler:
    name: str
    gap_gpa: float     # shock pressure for 50% initiation in a lab gap test (NOL LSGT class), GPa
    held: float        # Held v^2 d for a bare charge, mm^3/us^2 (= km^2/s^2 * mm)
    t_crit_k: float    # temperature at the wall that runs away in a cook-off (minutes), K
    gurney: float      # sqrt(2E), m/s, for its own case's fragments
    note: str


FILLERS = {f.name: f for f in [
    # gap: cast TNT 3.7 GPa (NOLTR 62-41 37.3 kbar; NOL LSGT 129 cards); Comp B 2.0-2.1 (201-213 cards,
    # 19.7-21.2 kbar); tritonal LSGT 5.5, super gap 1.5, full-scale Mk 82 1.1-1.2 GPa [SRC].
    # held: Comp B 16, H-6 16.5 [SRC ADA554529 T2]. Others scaled from Comp B by (gap/2.05)^2,
    # since v^2 d ~ P^2 tau ~ the critical energy [INFERRED], or [EST] where no gap figure was found.
    # t_crit: TNT 561 K, Comp B 487 K, tetryl 460 K (Rogers-type critical temperatures, recalled) [SRC recalled];
    # picric, Explosive D, amatol from their 5 s explosion temperatures less ~40 K [EST].
    Filler("black powder", 0.0, 0.0, 600, 640,
           "deflagrates: lit by a spark, flame or friction; explodes in a closed case [SRC AMCP 706-177 BM 32 cm]"),
    Filler("wet guncotton", 99.0, 1e9, 9999, 2400,
           "cannot be lit or shot off while wet; only its dry primer can go [SRC USNI 1900]"),
    Filler("picric (lyddite, Shimose, melinite)", 2.6, 26, 555, 2500,
           "[EST gap] more sensitive than cast TNT; metal picrates the hazard [SRC]"),
    Filler("TNT cast", 3.7, 52, 561, 2438, "[SRC gap; held INFERRED]"),
    Filler("TNT pressed", 2.1, 17, 561, 2438, "[SRC gap 193-198 cards; held INFERRED]"),
    Filler("amatol 50/50-80/20", 5.0, 95, 500, 2100,
           "[EST gap from booster sensitivity 0.60-0.83 in wax vs Comp B 1.40 SRC]; rifle bullet 0/5 [SRC]"),
    Filler("Explosive D (ammonium picrate)", 5.5, 115, 550, 2200, "[EST] the AP filler: chosen for its insensitivity [SRC]"),
    Filler("TNT/HND (Type 97, hexanite)", 3.0, 34, 520, 2500, "[EST] between TNT and tetryl"),
    Filler("Comp B / Torpex / H-6", 2.05, 16, 487, 2774, "[SRC gap, held]"),
    Filler("tetryl (boosters)", 1.0, 4, 460, 2500, "[EST gap; held 44 listed in ADA554529 reads wrong-way, not used]"),
]}


# ------------------------------------------------------------------------------------------------
# S1. Shock in air: what a detonation's blast puts on a store's face
# ------------------------------------------------------------------------------------------------

def kinney_ps(z):
    """Incident overpressure, Pa, at scaled distance z m/kg^1/3 TNT (Kinney & Graham)."""
    r = 808 * (1 + (z / 4.5) ** 2) / sqrt((1 + (z / 0.048) ** 2) * (1 + (z / 0.32) ** 2) * (1 + (z / 1.35) ** 2))
    return r * P0


def reflected_rh(ps, g=1.4):
    return 2 * ps + (g + 1) * ps * ps / ((g - 1) * ps + 2 * g * P0)


def table_s1():
    print("\nS1. Air blast on a store's face vs what a filler needs")
    print("  Z m/kg^1/3 | incident MPa | reflected MPa (ideal x8 cap) | x20 real-gas bound | share of cast TNT's 3.7 GPa")
    for z in (0.06, 0.1, 0.2, 0.3, 0.5, 0.8, 1.2, 2.0):
        ps = kinney_ps(z)
        pr = reflected_rh(ps)
        print(f"  {z:5.2f} | {ps/1e6:8.2f} | {pr/1e6:8.1f} | {20*ps/1e6:8.0f} | {20*ps/3.7e9:6.1%}")
    print("  -> beyond about one charge radius (0.053 W^1/3 for a TNT sphere) no air blast reaches GPa:")
    print("     a filler is shocked off only by contact, the donor's case or fragments [INFERRED].")
    # The Mk 82 test, as scaled distance
    w_tnt = 87 * 1.07   # 87 kg tritonal, ~1.07 TNT [EST]
    for r in (0.20, 0.30):
        print(f"  Mk 82 tritonal pair at {r:.2f} m: Z = {r / w_tnt ** (1/3):.3f} m/kg^1/3 "
              f"({'detonated' if r < 0.25 else 'no reaction'}, ADP004860) [SRC]")
    print("  Q-D, the acceptors' stacks unhurt at 3-7 bar (K1.25-K2 = 0.50-0.79 m/kg^1/3) [SRC]:")
    for k in (1.25, 2.0, 6.0, 11.0):
        z = k * 0.3967
        print(f"     K{k:<5} = {z:.2f} m/kg^1/3, open-air incident there {kinney_ps(z)/1e3:7.0f} kPa "
              "(the igloo design loads, 3-7 bar, are behind earth cover)")


# ------------------------------------------------------------------------------------------------
# S2. Shock in water
# ------------------------------------------------------------------------------------------------

def cole_p(w, r):
    """Peak shock pressure, MPa, W kg TNT at R m (Cole; valid R > ~10 charge radii)."""
    return 52.4 * (w ** (1 / 3) / r) ** 1.13


def cole_r(w, p_mpa):
    return w ** (1 / 3) / (p_mpa / 52.4) ** (1 / 1.13)


def table_s2():
    print("\nS2. Shock in water: the distance at which a pressure is reached")
    print("  W kg TNT | 7 MPa (DC pistols fail, Kirkby) | 20 MPa | 100 MPa | 1 GPa (formula past validity)")
    for w in (30, 136, 300, 500):
        print(f"  {w:5d} | {cole_r(w, 7):6.1f} m | {cole_r(w, 20):5.1f} m | {cole_r(w, 100):5.1f} m | {cole_r(w, 1000):5.2f} m")
    w = 136
    for r, ft in ((24.4, 80), (33.5, 110)):
        print(f"  Kirkby: 300 lb charges, pistol-to-charge joints failed at {ft} ft ({r:.0f} m): "
              f"{cole_p(w, r):.1f} MPa, Z = {r / w ** (1/3):.1f} m/kg^1/3 [SRC distance, INFERRED pressure]")


# ------------------------------------------------------------------------------------------------
# F. Fragments
# ------------------------------------------------------------------------------------------------

def gurney_cyl(g, cm):
    return g * sqrt(cm / (1 + cm / 2))


def thor_v50(m_g, e_mm, k_grain_in3=660.0):
    """THOR limit velocity, m/s, compact steel fragment m grams on mild steel e mm, normal."""
    m_gr = m_g * 15.432
    a_in2 = (m_gr / k_grain_in3) ** (2 / 3)
    e_in = e_mm / 25.4
    v_fps = 10 ** 6.523 * (e_in * a_in2) ** 0.906 * m_gr ** -0.963
    return v_fps * 0.3048


def frag_d_mm(m_g):
    """Presented size of a compact steel fragment: the cube root of its volume, mm."""
    return (m_g / 7.85) ** (1 / 3) * 10


def residual(v, v50):
    return sqrt(v * v - v50 * v50) if v > v50 else 0.0


def initiates(filler, m_g, v, case_mm):
    """True if a fragment m g at v m/s through case_mm of mild steel leaves v^2 d over the filler's Held K."""
    if filler.held >= 1e8:
        return False, 0.0
    vr = residual(v, thor_v50(m_g, case_mm)) if case_mm > 0 else v
    s = (vr / 1000) ** 2 * frag_d_mm(m_g)
    return s >= filler.held, s


@dataclass(frozen=True)
class Store:
    name: str
    filler: str
    case_mm: float        # wall the fragment must pass to reach the filler [EST unless noted]
    cm: float             # its own charge/metal, for what it throws when it goes
    note: str


STORES = [
    Store("AP shell, 12-16 in", "Explosive D (ammonium picrate)", 60, 0.03, "wall 0.15-0.3 cal"),
    Store("AP shell, RN shellite / TNT", "picric (lyddite, Shimose, melinite)", 60, 0.03, "shellite less sensitive than lyddite"),
    Store("Common/HE shell 12 in (lyddite)", "picric (lyddite, Shimose, melinite)", 30, 0.12, "wall ~0.1 cal"),
    Store("HE shell 6 in (TNT)", "TNT cast", 15, 0.12, ""),
    Store("5 in AA common (USN)", "Explosive D (ammonium picrate)", 10, 0.10, "thin wall; 'most susceptible to mass detonation' [SRC]"),
    Store("Torpedo warhead, TNT", "TNT cast", 6, 3.0, "warhead skin [EST]"),
    Store("Torpedo warhead, Type 97/hexanite", "TNT/HND (Type 97, hexanite)", 6, 3.5, "[EST]"),
    Store("Torpedo warhead, Torpex", "Comp B / Torpex / H-6", 6, 3.0, "[EST]"),
    Store("Torpedo warhead, wet guncotton", "wet guncotton", 6, 2.0, "only the dry primer"),
    Store("Depth charge, TNT/amatol 300 lb", "amatol 50/50-80/20", 5, 2.7, "Kirkby: ~18 in case, 300 lb [SRC]"),
    Store("Depth charge, TNT", "TNT cast", 5, 2.7, ""),
    Store("Mine, TNT 150-300 kg", "TNT cast", 5, 3.0, "[EST case]"),
    Store("GP bomb 250-500 kg, TNT/amatol", "TNT cast", 10, 1.0, "C/M ~1, filler 50% [SRC 13]"),
    Store("GP bomb, Comp B", "Comp B / Torpex / H-6", 10, 1.0, ""),
    Store("SAP/AP bomb", "Explosive D (ammonium picrate)", 25, 0.3, ""),
]

ATTACKS = [  # name, fragment g, m/s  [from 13: HE 750-950 m/s ~25 g mean; AP chunks 370-580 m/s, kg]
    ("6 in HE fragment", 25, 900),
    ("6 in HE big piece", 200, 800),
    ("15 in AP chunk", 2000, 500),
    ("GP bomb fragment", 20, 1900),
    ("GP bomb big piece", 150, 1700),
    ("warhead/DC fragment", 30, 2800),
]


def table_f():
    print("\nF1. Fragment speeds by Gurney (cylinder)")
    for name, g, cm in (("AP shell, TNT-class, C/M 0.03", 2438, 0.03), ("HE shell C/M 0.12", 2438, 0.12),
                        ("GP bomb C/M 1.0", 2438, 1.0), ("depth charge C/M 2.7", 2438, 2.7),
                        ("torpedo warhead C/M 3.5", 2500, 3.5)):
        print(f"  {name:32s} {gurney_cyl(g, cm):6.0f} m/s")

    print("\nF2. Bare filler: fragment speed that sets it off, by fragment mass (Held)")
    masses = (1, 10, 100, 1000)
    print("  filler | K | " + " | ".join(f"{m} g (d {frag_d_mm(m):.0f} mm)" for m in masses))
    for f in FILLERS.values():
        if f.held <= 0 or f.held >= 1e8:
            continue
        vs = [1000 * sqrt(f.held / frag_d_mm(m)) for m in masses]
        print(f"  {f.name:36s} | {f.held:5.0f} | " + " | ".join(f"{v:5.0f}" for v in vs))

    print("\nF3. Through its case: does each attack set the store off? (v^2 d left / K)")
    print("  store | case mm | " + " | ".join(a[0] for a in ATTACKS))
    for st in STORES:
        f = FILLERS[st.filler]
        cells = []
        for _, m, v in ATTACKS:
            ok, s = initiates(f, m, v, st.case_mm)
            cells.append(("GO " if ok else "-- ") + (f"{s / f.held:4.2f}" if f.held < 1e8 else "  wet"))
        print(f"  {st.name:34s} | {st.case_mm:3.0f} | " + " | ".join(cells))

    print("\nF4. Case perforation alone (THOR V50, m/s)")
    for e in (1, 5, 10, 25, 60):
        print(f"  {e:3d} mm: " + "  ".join(f"{m} g {thor_v50(m, e):5.0f}" for m in (1, 10, 100, 1000)))


# ------------------------------------------------------------------------------------------------
# H1. Propellant lit by hot gas
# ------------------------------------------------------------------------------------------------

CORDITE = dict(k=0.23, rho=1580.0, c=1500.0)   # double-base stick [EST: 0.2-0.25 W/m K]
T_IGN = 480.0     # surface ignition, 190-227 C measured, heating-rate independent [SRC AD0472135]


def ignite_time_cf(t_gas, h_conv, eps_gas=0.3, t0=300.0, t_ign=T_IGN, prop=CORDITE):
    """Closed form: semi-infinite solid, convection with an effective h (radiation linearised at the
    mean surface temperature). theta_s = 1 - exp(b^2) erfc(b), b = h sqrt(t)/sqrt(k rho c)."""
    k, rho, c = prop["k"], prop["rho"], prop["c"]
    tm = 0.5 * (t0 + t_ign)
    h_r = eps_gas * SIGMA * (t_gas ** 2 + tm ** 2) * (t_gas + tm)
    h = h_conv + h_r
    need = (t_ign - t0) / (t_gas - t0) if t_gas > t_ign else 2.0
    if need >= 1:
        return None
    lo, hi = 0.0, 50.0
    for _ in range(200):
        b = 0.5 * (lo + hi)
        th = 1 - exp(b * b) * erfc(b) if b < 25 else 1 - 1 / (b * sqrt(pi))
        if th < need:
            lo = b
        else:
            hi = b
    b = 0.5 * (lo + hi)
    return (b * sqrt(k * rho * c) / h) ** 2


def table_h1():
    print("\nH1. Time for a cordite surface to reach 480 K in hot gas (closed form; s)")
    hs = (20, 60, 200)
    print("  gas K | " + " | ".join(f"h {h} W/m2K" for h in hs))
    for tg in (500, 550, 600, 700, 900, 1200, 1600, 2000, 2500):
        cells = []
        for h in hs:
            t = ignite_time_cf(tg, h)
            cells.append("never" if t is None else (f"{t:8.3f}" if t < 1 else f"{t:8.1f}"))
        print(f"  {tg:5d} | " + " | ".join(cells))
    print("  (h 20 still air, 60 a moving smoke layer, 200 a jet through a door; gas emissivity 0.3)")
    k, rho, c = CORDITE["k"], CORDITE["rho"], CORDITE["c"]
    d_crit = (pi / 4) * k * rho * c * (T_IGN - 300) ** 2
    print(f"\n  The dose rule for the code: lit when sum(q^2 dt) >= D = (pi/4) k rho c dT^2 = {d_crit/1e6:.0f} (kW/m2)^2 s,")
    print("  q = h (Tg - T_ign) + eps sigma (Tg^4 - T_ign^4), the flux onto a surface already at 480 K (none below it):")
    print("  gas K | h | dose rule s | closed form s | ratio")
    for tg, h in ((550, 60), (600, 200), (700, 60), (900, 20), (900, 60), (1200, 60), (1600, 60), (2000, 200), (2500, 200)):
        q = h * (tg - T_IGN) + 0.3 * SIGMA * (tg ** 4 - T_IGN ** 4)
        td = d_crit / q ** 2
        tc = ignite_time_cf(tg, h)
        print(f"  {tg:5d} | {h:3d} | {td:9.3f} | {tc:9.3f} | {td/tc:5.2f}")


# ------------------------------------------------------------------------------------------------
# H2. Cook-off of a cased charge in a fire
# ------------------------------------------------------------------------------------------------

STEEL = dict(rho=7850.0, c=520.0)
TNT_PROP = dict(k=0.26, rho=1650.0, c=1370.0, t_melt=354.0, l_melt=98_000.0)


def cookoff_time(case_mm, t_crit, t_fire=1100.0, h_fire=25.0, eps=0.9, t0=300.0, filler=TNT_PROP, t_end=7200.0):
    """Seconds until the filler at the wall reaches t_crit: a lumped steel wall heated by a fire
    (convection + radiation), losing into a semi-infinite filler slab (explicit grid; melting by an
    apparent heat capacity). [EST model]"""
    k, rho, c = filler["k"], filler["rho"], filler["c"]
    alpha = k / (rho * c)
    n = 80
    depth = 0.12
    dx = depth / n
    dt = min(0.4 * dx * dx / alpha, 1.0)
    tw = t0
    temps = [t0] * (n + 1)
    m_wall = STEEL["rho"] * STEEL["c"] * case_mm / 1000
    t = 0.0

    def capp(tk):
        if filler.get("t_melt") and abs(tk - filler["t_melt"]) < 5:
            return c + filler["l_melt"] / 10
        return c

    while t < t_end:
        q_in = h_fire * (t_fire - tw) + eps * SIGMA * (t_fire ** 4 - tw ** 4)
        q_out = k * (tw - temps[1]) / dx
        tw += dt * (q_in - q_out) / (m_wall + 0.5 * rho * c * dx)
        temps[0] = tw
        new = temps[:]
        for i in range(1, n):
            new[i] = temps[i] + k * dt / (rho * capp(temps[i]) * dx * dx) * (temps[i + 1] - 2 * temps[i] + temps[i - 1])
        temps = new
        t += dt
        if tw >= t_crit:
            return t
    return None


def table_h2():
    print("\nH2. Cook-off: minutes until the filler at the wall reaches its critical temperature")
    fires = (("engulfing fuel fire 1,100 K", 1100, 25), ("compartment fire 900 K", 900, 15), ("hot smoke 700 K", 700, 10))
    print("  wall mm | T_crit K | " + " | ".join(f[0] for f in fires))
    for case_mm, tc, label in ((5, 561, "DC/warhead, TNT"), (6, 487, "warhead, Torpex"), (10, 561, "GP bomb, TNT"),
                               (10, 487, "GP bomb, Comp B"), (15, 561, "HE shell 6 in"), (30, 555, "HE shell 12 in"),
                               (60, 550, "AP shell")):
        cells = []
        for _, tf, h in fires:
            s = cookoff_time(case_mm, tc, t_fire=tf, h_fire=h)
            cells.append("  none" if s is None else f"{s / 60:6.1f}")
        print(f"  {case_mm:3d} {label:16s} | {tc} | " + " | ".join(cells))
    print("  checks: Forrestal's M117 (Comp B, ~10 mm) went at ~1.5 min in a JP-5 fire [SRC];")
    print("          Franklin's GP bombs 'as little as 3-4 minutes, or not at all' [SRC WDR 56];")
    print("          Isherwood's and Mullany's depth charges 26 and 23 min in deck fires [SRC];")
    print("          no TNT warhead known to have gone by roasting [SRC destroyer report].")


if __name__ == "__main__":
    table_s1()
    table_s2()
    table_f()
    table_h1()
    table_h2()
