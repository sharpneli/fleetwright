"""The flash chain and the venting race: reference numbers for 10-magazine-flash-chain.md.

Pure Python, no dependencies; runs in about a second:  python3 magazine_race_ref.py

What it does (the note's §6):
  * the race as a mass balance on one gas volume (a magazine and the spaces open to it):
        dm/dt = rho_p * S(t) * r(P)                    gas made by the lit charges' surface
              - mdot_vent(A_vent(t), P)                outflow through the open vents (choked above ~1.8 bar abs)
        P     = P_atm + m * f_eff / V                  (f = the propellant's impetus R*T, J/kg; f_eff after heat loss)
        r(P)  = beta * P^alpha, two branches meeting at a knee (alpha ~0.2 below ~0.15 MPa, 0.7-0.9 above)
  * S(t) grows as the flame goes through the stow: charges lying open (bags, charges out of their cases) light within
    ~0.5 s; charges still in their packages light as a branching process, R neighbours per burning charge
    (Clarkson ~4, brass ~1.4, USN tank ~1.2 [EST, fitted]); each lit charge's surface shrinks as it burns;
  * each boundary fails at its pressure and adds its area (doors, the gunhouse lift, bulkheads, decks, sides);
  * the peak pressure and what failed give the tier (07 §6.3);
  * the knife edge as one number: how many tonnes of propellant may burn at once before a given vent loses the race.

Tags: [SRC] sourced (see the note), [INF] inferred / fitted here, [EST] engineering estimate, no source.
"""
import math

P_ATM = 101_325.0          # Pa
G = 9.81
GAMMA_GAS = 1.25           # hot propellant gas [EST, interior-ballistics usual 1.2-1.25]
CHOKE = math.sqrt(GAMMA_GAS) * (2 / (GAMMA_GAS + 1)) ** ((GAMMA_GAS + 1) / (2 * (GAMMA_GAS - 1)))   # ≈ 0.66
HEAT_KEEP = 0.5            # share of the impetus left as pressure in a cold steel room (losses, mixing with air) [EST]
VENT_CD = 0.7              # discharge coefficient of a ragged opening [EST]

# ------------------------------------------------------------------ propellants ----------------------------------------
# beta: mm/s at 1 MPa on the high branch; alpha_hi above the knee, alpha_lo below; knee MPa; f J/kg; rho kg/m³;
# sigma: burning surface per kg (m²/kg) of the period's grain; web: the distance a face burns to finish, m;
# form: "cord" (solid, S ~ sqrt(1-z)) or "tube" (S ~ constant) or "grain".
PROPELLANTS = {
    # Cordite MD, 1901-1927 (RN; Japanese C2 1917-): DSJ strand 4.6/6.1/7.9 mm/s at 3.4/4.9/6.9 MPa -> beta 1.8,
    # alpha 0.78 [SRC]; Kubota: alpha ~0.2 below ~0.15 MPa [SRC]. MD45 cord 0.45 in [SRC: designation].
    "cordite_md": dict(beta=1.8, a_hi=0.78, a_lo=0.25, knee=0.15, f=1.06e6, rho=1580, sigma=4 / (1580 * 0.0114),
                       web=0.0057, form="cord", ignit=20),
    # Cordite Mk I, 1893-1901: hotter (58% NG); beta scaled by flame temperature [INF]. Same cord sizes.
    "cordite_mk1": dict(beta=3.0, a_hi=0.75, a_lo=0.25, knee=0.15, f=1.17e6, rho=1600, sigma=4 / (1600 * 0.0114),
                        web=0.0057, form="cord", ignit=40),
    # RP C/12 (German, 1912-): low-N NC + centralite, cooler than MD [INF]; tube 18/8 mm (web 5 mm) [SRC: navweaps 28 cm
    # "820/410 x 18/8"]; S/m of the tube ≈ 0.25 m²/kg.
    "rp_c12": dict(beta=1.6, a_hi=0.75, a_lo=0.25, knee=0.15, f=1.0e6, rho=1600, sigma=0.25, web=0.0025, form="tube",
                   ignit=10),
    # Single-base NC (USN SPD, poudre B, Russian pyroxylin): strand 3.12 mm/s at 3.45 MPa, 11.05 at 13.8 (12.65 %N) ->
    # beta ~1.0, alpha ~0.9 [SRC: DTIC ADA261009]; multi-perforated grains ~0.3 m²/kg [EST].
    "single_base": dict(beta=1.0, a_hi=0.88, a_lo=0.4, knee=0.15, f=0.95e6, rho=1600, sigma=0.3, web=0.002,
                        form="tube", ignit=1),
    # Black powder: r = 12 mm/s (P/0.1 MPa)^0.21 [SRC: Trebinski 2022]; ~1 cm prismatic/pebble grains [EST].
    "black_powder": dict(beta=19.5, a_hi=0.21, a_lo=0.21, knee=0.15, f=0.29e6, rho=1750, sigma=6 / (1750 * 0.01),
                         web=0.005, form="grain", ignit=200),
}


def burn_rate(p, k):
    """Linear burning rate, m/s, at absolute pressure p (Pa), two branches meeting at the knee."""
    pm = max(p, 0.02e6) / 1e6
    if pm >= k["knee"]:
        return k["beta"] * pm ** k["a_hi"] / 1000
    r_knee = k["beta"] * k["knee"] ** k["a_hi"]
    return r_knee * (pm / k["knee"]) ** k["a_lo"] / 1000


def vent_flow(area, p, f):
    """Mass flow, kg/s, through an opening of area m² from absolute p into the air; gas RT ~ HEAT_KEEP*f."""
    if area <= 0 or p <= P_ATM:
        return 0.0
    rt = HEAT_KEEP * f
    rho = p / rt
    choked = VENT_CD * CHOKE * area * p / math.sqrt(rt)
    sub = VENT_CD * area * math.sqrt(2 * rho * (p - P_ATM))
    return min(choked, sub)


# ------------------------------------------------------------------ the knife edge -------------------------------------
def tonnes_at_once(prop, vent_m2, p_fail_kpa):
    """Tonnes of fresh propellant that may burn together and keep the plateau at or under p_fail (gauge).
    Steady state: rho_p*sigma*M*r(P) = vent(P)."""
    k = PROPELLANTS[prop]
    p = P_ATM + p_fail_kpa * 1e3
    return vent_flow(vent_m2, p, k["f"]) / (k["rho"] * k["sigma"] * burn_rate(p, k)) / 1000


def lift_kpa(w_t, d_m):
    """Pressure under a gunhouse that lifts it (gauge kPa): revolving weight over the barbette's area."""
    return w_t * 1000 * G / (math.pi * d_m ** 2 / 4) / 1000


def revolving_t(cal_mm, guns):
    """Fit, 1906-1941 (N=25): W = 229 t n^0.96 (cal/305)^2.70, scatter x/÷1.26 [INF from the sourced weights]."""
    return 229 * guns ** 0.96 * (cal_mm / 305) ** 2.70


def barbette_m(w_t):
    """Fit (N=11 sourced inner diameters): D = 1.28 W^0.30 m, scatter x/÷1.05 [INF]."""
    return 1.28 * w_t ** 0.30


# ------------------------------------------------------------------ the race ------------------------------------------
class Boundary:
    def __init__(self, name, fail_kpa, area, d=0.0, tier=None):
        self.name, self.fail, self.area, self.d, self.tier = name, fail_kpa * 1e3, area, d, tier
        self.t_fail = None


# How readily the flame goes from a burning charge to the next one in its package: R = how many neighbours one burning
# charge lights before it burns out (a branching process). Over 1 the fire grows through the stow, under 1 it dies out.
# Bags out of their cases light together (handled apart: BAG_SPREAD_S). [EST: fitted so the anchors come out]
PACKAGE_R = {
    "clarkson": 4.0,       # RN leather/metal cases, lids on: thin, loose-fitting; cordite lights easily
    "brass": 1.4,          # German main charge in its brass case; the fore charge in its tin
    "tank": 1.2,           # USN bags in airtight metal tanks (and single-base hard to light)
}
BAG_SPREAD_S = 0.5         # time for a flash to light the exposed bags in a space [EST; cordite ~0.2-1 s]


def race(prop, n_open, n_cased, charge_kg, volume, boundaries, vent0=0.0, package="brass", flooded=0.0,
         lit0=1, dt0=0.002, t_end=180.0):
    """One magazine (or station) alight. Returns (peak gauge kPa, burned t, failures, trace).
    n_open:  charges lying open (bags, charges out of their cases): they light within BAG_SPREAD_S of the flash;
    n_cased: charges still in their packages: each burning charge lights the next as a branching process with
             PACKAGE_R[package] over its burn-out time, the unlit pool running down (SIR);
    flooded: share of the charges under water (they don't light)."""
    dt = dt0
    k = PROPELLANTS[prop]
    t_burn = k["web"] / burn_rate(P_ATM, k)
    n_open_dry, n_cased_dry = n_open * (1 - flooded), n_cased * (1 - flooded)
    n_dry = n_open_dry + n_cased_dry
    lit_open = 0.0
    lit_cased = float(min(lit0, n_cased_dry)) if n_open_dry < 1 else 0.0
    lit = lit_open + lit_cased
    cohorts = [[max(lit, 1e-9), 0.0]]  # charges lit together: [count, burned share]
    cohort_t0 = 0.0
    m_gas, t, peak, burned = 0.0, 0.0, 0.0, 0.0
    vent = vent0
    fails = []
    trace = []
    next_print = 0.0
    while t < t_end:
        p = P_ATM + m_gas * HEAT_KEEP * k["f"] / volume
        r = burn_rate(p, k)
        gen = 0.0
        burning = 0.0
        for c in cohorts:
            cnt, zc = c
            if zc >= 1.0:
                continue
            # surface left: a solid cord ~ sqrt(1-z), a tube ~ constant, a grain ~ (1-z)^(2/3)
            shape = math.sqrt(1 - zc) if k["form"] == "cord" else (1 - zc) ** (2 / 3) if k["form"] == "grain" else 1.0
            g = min(k["rho"] * cnt * charge_kg * k["sigma"] * shape * r, cnt * charge_kg * (1 - zc) / dt)
            gen += g
            c[1] = min(1.0, zc + g * dt / (cnt * charge_kg))
            burning += cnt
        out = vent_flow(vent, p, k["f"])
        m_gas = max(0.0, m_gas + (gen - out) * dt)
        burned += gen * dt
        # the flame goes on through the stow: the open charges all but at once, the cased ones as a branching process
        d_lit = 0.0
        if n_open_dry - lit_open > 0:
            d = (n_open_dry - lit_open) * (1 - math.exp(-dt / BAG_SPREAD_S))
            lit_open += d
            d_lit += d
        if n_cased_dry - lit_cased > 0 and burning > 0:
            rate = PACKAGE_R[package] / t_burn                     # neighbours lit a second per burning charge
            d = min(n_cased_dry - lit_cased, burning * rate * dt * (n_cased_dry - lit_cased) / max(n_cased_dry, 1))
            lit_cased += d
            d_lit += d
        if d_lit > 0:
            lit += d_lit
            if t - cohort_t0 < 0.1:          # lit within the same short window: one cohort
                a = cohorts[-1]
                a[1] = a[0] * a[1] / (a[0] + d_lit)
                a[0] += d_lit
            else:
                cohorts.append([d_lit, 0.0])
                cohort_t0 = t
        pg = p - P_ATM
        peak = max(peak, pg)
        for b in boundaries:
            if b.t_fail is None and pg > b.fail:
                b.t_fail = t
                vent += b.area
                fails.append(b)
        dt = dt0 if t < 5 else 10 * dt0
        if t >= next_print:
            trace.append((t, pg / 1e3, lit, burned / 1000, vent))
            next_print += 0.25 if t < 5 else 2.0
        if burning < 1e-6 and t > 1 and pg < 1e3:
            break
        t += dt
    return peak / 1e3, burned / 1000, fails, trace


def tier(peak_kpa, fails):
    d = sum(b.d for b in fails)
    names = {b.name for b in fails}
    if any(b.tier == 4 for b in fails):
        return 4, d
    if d >= 0.8:
        return 3, d
    if d > 0:
        return 2, d
    if "gunhouse lifts" in names or "turret roof" in names:
        return 1, d
    return 0, d


def magazine_boundaries(lift, trunk_m2, deck_kpa=700, side_kpa=1000, nb_kpa=250, door_kpa=40, door_area=1.0,
                        neighbour_magazine=True):
    """The boundaries of a main-battery magazine in the order they usually fail (07 §2.4) [EST].
    Once the gunhouse lifts, the gas's way out is the handing room and the hoist trunk (trunk_m2), not the whole
    barbette: the turntable, the shell rooms' decks and the trunk's own ring stand between."""
    return [
        Boundary("magazine doors / scuttles", door_kpa, door_area),
        Boundary("gunhouse lifts", lift, trunk_m2),
        Boundary("bulkhead to the next magazine" if neighbour_magazine else "bulkhead to the next space", nb_kpa, 4.0,
                 tier=4 if neighbour_magazine else None),
        Boundary("armoured deck over it", deck_kpa, 10.0, d=0.35),     # opens into the closed 'tween deck above [EST]
        Boundary("sides", side_kpa, 60.0, d=0.45),
        Boundary("bottom", 2.0 * side_kpa, 60.0, d=0.2),
    ]


# ------------------------------------------------------------------ cases ---------------------------------------------
def show_case(title, prop, n_open, n_cased, charge_kg, volume, bounds, **kw):
    peak, burned, fails, trace = race(prop, n_open, n_cased, charge_kg, volume, bounds, **kw)
    tr, d = tier(peak, fails)
    failed = ", ".join(f"{b.name} {b.t_fail:.2f}s" for b in fails) or "nothing"
    print(f"  {title:<58} peak {peak:8.0f} kPa  burned {burned:6.1f} t  tier {tr}  (d {d:.2f})")
    print(f"      failed: {failed}")
    return peak, tr


def main():
    print("== Burning rate at magazine pressures (mm/s) ==")
    print("  propellant      0.1 MPa  0.2  0.5   1     5")
    for name, k in PROPELLANTS.items():
        row = "  ".join(f"{burn_rate(p * 1e6, k) * 1000:5.2f}" for p in (0.1, 0.2, 0.5, 1.0, 5.0))
        print(f"  {name:<14} {row}")

    print("\n== Burn-out time of one charge in the open (s), the web at 1 atm ==")
    for name, k in PROPELLANTS.items():
        print(f"  {name:<14} {k['web'] / burn_rate(P_ATM, k):6.1f} s")

    print("\n== Gunhouse lift pressure from the fits (gauge kPa) ==")
    for cal, n in [(152, 2), (190, 1), (234, 1), (280, 2), (305, 2), (343, 2), (356, 3), (381, 2), (406, 3)]:
        w = revolving_t(cal, n)
        dm = barbette_m(w)
        print(f"  {n} x {cal} mm: W {w:5.0f} t  D {dm:4.1f} m  lift {lift_kpa(w, dm):4.0f} kPa")

    print("\n== The knife edge: tonnes that may burn at once and hold the gas under p (fresh charges) ==")
    print("  vent                         p kPa   cordite_md  rp_c12  single_base  black_powder")
    for label, a in [("open flash door 0.5 m²", 0.5), ("hoist trunk 2 m²", 2.0), ("holed gunhouse 5 m²", 5.0),
                     ("lifted 13.5\" barbette 28 m²", 28.0), ("lifted 15\" barbette 34 m²", 34.0)]:
        for p in (110, 700):
            vals = "  ".join(f"{tonnes_at_once(pp, a, p):9.2f}" for pp in ("cordite_md", "rp_c12", "single_base", "black_powder"))
            print(f"  {label:<28} {p:5d}   {vals}")

    # A 13.5" twin (Lion's Q): 2 guns x 110 rounds of 133 kg MD (297 lb) [SRC: navweaps]; magazine and handing room
    # ~ 400 m³ of gas space [EST]; hoist trunk ~ 3 m² [EST].
    w135 = 610.0
    d135 = barbette_m(w135)
    lift135 = lift_kpa(w135, d135)
    mb = lambda **kw: magazine_boundaries(lift135, 3.0, neighbour_magazine=False, **kw)
    print(f"\n== Cases (13.5\" twin: lift {lift135:.0f} kPa, barbette {d135:.1f} m, trunk 3 m²) ==")
    show_case("BCF 1916: flash in the magazine, lids off (all open)", "cordite_md", 220, 0, 133, 400, mb(), vent0=1.0)
    show_case("the same, Clarkson lids on (R 4)", "cordite_md", 0, 220, 133, 400, mb(), vent0=1.0, package="clarkson")
    show_case("the same, 20 open in the handing room, the rest lids on", "cordite_md", 20, 200, 133, 400, mb(), vent0=1.0,
              package="clarkson")
    show_case("a two-turret group (A+B), lids off: 440 open", "cordite_md", 440, 0, 133, 800,
              magazine_boundaries(lift135, 6.0, neighbour_magazine=False), vent0=2.0)
    show_case("the group, lids on (R 4)", "cordite_md", 0, 440, 133, 800,
              magazine_boundaries(lift135, 6.0, neighbour_magazine=False), vent0=2.0, package="clarkson")
    show_case("Lion Q: 8 charges in the chamber and cages, gunhouse holed", "cordite_md", 8, 0, 133, 300,
              [Boundary("turret roof", 60, 10.0)], vent0=5.0)
    show_case("Lion Q with the magazine dry: 8 open + 220 lids on behind a buckled door", "cordite_md", 8, 220, 133, 400,
              mb(door_kpa=60, door_area=1.0), vent0=0.0, package="clarkson")
    # Seydlitz C and D, Dogger Bank: 62 complete charges (36 kg fore in silk + 70 kg main in brass) [SRC] burned in the
    # two turrets' trains, out of their tins; 28 cm twin ~430 t [SRC]; gunhouse openings (hole, hatches) ~6 m² [EST].
    w28 = 430.0
    d28 = barbette_m(w28)
    l28 = lift_kpa(w28, d28)
    print(f"\n== Cases (28 cm twin: lift {l28:.0f} kPa) ==")
    show_case("Seydlitz 1915: one turret's train, 31 charges out of their tins", "rp_c12", 31, 0, 106, 300,
              [Boundary("gunhouse lifts", l28, 20.0)], vent0=6.0)
    show_case("both turrets' 62 in one train (worse than the record)", "rp_c12", 62, 0, 106, 300,
              [Boundary("gunhouse lifts", l28, 20.0)], vent0=6.0)
    show_case("the flash in a dry 28 cm magazine, brass cases (R 1.4)", "rp_c12", 4, 180, 106, 400,
              magazine_boundaries(l28, 2.5), vent0=1.0, package="brass")
    show_case("Gneisenau 1942: 23 t, cased, doors shut", "rp_c12", 2, 207, 110, 700,
              magazine_boundaries(lift_kpa(1050, 10.2), 4.0), vent0=0.0, package="brass")
    print("\n== Single-base and black powder ==")
    show_case("USN single-base bags in tanks, 8\" group (Boise 1942)", "single_base", 4, 330, 45, 300,
              magazine_boundaries(lift_kpa(250, 6.0), 1.5), vent0=0.0, package="tank")
    show_case("pre-1890 black powder magazine, 12\" group", "black_powder", 300, 0, 100, 400,
              magazine_boundaries(lift_kpa(250, 6.5), 2.0), vent0=0.0)
    print("\n== Flooding: the BCF case again, flooded share ==")
    for fl in (0.0, 0.5, 0.8, 0.9, 0.95, 0.99):
        show_case(f"flooded {fl:.0%}", "cordite_md", 220, 0, 133, 400, mb(), vent0=1.0, flooded=fl)
    print("\n== Packaging R swept: one 15\" magazine (2 x 100 rounds, 194 kg MD), lids on ==")
    w15 = 782.0
    for rr in (0.8, 1.2, 1.4, 2.0, 3.0, 4.0, 6.0, 8.0):
        PACKAGE_R["sweep"] = rr
        show_case(f"R {rr}", "cordite_md", 0, 200, 194, 600, magazine_boundaries(lift_kpa(w15, 9.3), 3.5,
                  neighbour_magazine=False), vent0=1.0, package="sweep")

if __name__ == "__main__":
    main()
