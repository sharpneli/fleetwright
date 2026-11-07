"""charge_bed_ref.py - a magazine's charges as a packed bed (research note 12-charge-bed.md).

Pure Python, a few seconds. Every derived number in the note comes out of this file.
  1. the stack: kg of propellant a m3 of the stack, by package, from the packages' sizes
  2. the drag: Ergun's terms at the two scales (between charges, among the cords), Reynolds and Mach
  3. a bed held still: the exact steady 1-D profile against the code's lumped form
  4. a bed free to move: how long it holds its pressure before it blows itself into the aisle
  5. a lit package: the case's, tank's or brass case's own mouth
Tags as in the note: [SRC] sourced, [INFERRED] reasoning or a fit, [EST] an estimate.
"""
import math

ATM = 0.101e6
GAMMA = 1.25
CHOKE = math.sqrt(GAMMA) * (2 / (GAMMA + 1)) ** ((GAMMA + 1) / (2 * (GAMMA - 1)))   # 0.658
CRIT = (2 / (GAMMA + 1)) ** (GAMMA / (GAMMA - 1))                                       # 0.555
CD = 0.7          # a ragged opening's discharge coefficient (the code's GasVentCd)
KEEP = 0.5        # the code's MagazineHeatKeep: the share of the impetus left as pressure
MU = 6e-5         # hot propellant gas viscosity, Pa s [EST: 1,500-2,500 K]


class Prop:
    def __init__(s, name, beta, ahi, alo, knee, f, rho, skg, web, form):
        s.name, s.beta, s.ahi, s.alo, s.knee, s.f, s.rho, s.skg, s.web, s.form = name, beta, ahi, alo, knee, f, rho, skg, web, form

    def r(s, p):
        pm = max(p, 0.02e6) / 1e6
        if pm >= s.knee:
            return s.beta * pm ** s.ahi / 1000
        return s.beta * s.knee ** s.ahi * (pm / s.knee) ** s.alo / 1000

    def alpha(s, p):
        return s.ahi if p / 1e6 >= s.knee else s.alo


# As src/Fleetwright.Sim.Ships/Propellants.cs (from note 10 §1.1)
MD = Prop("cordite MD45", 1.8, 0.78, 0.25, 0.15, 1.06e6, 1580, 4 / (1580 * 0.0114), 0.0057, "cord")
RP = Prop("RP C/12 tube 18/8", 1.3, 0.75, 0.25, 0.15, 0.95e6, 1560, 0.25, 0.0025, "tube")
SB = Prop("USN single-base 7-perf", 1.0, 0.88, 0.40, 0.15, 0.95e6, 1600, 0.30, 0.0025, "grain")


def cyl_dp(d, l):
    """Ergun's particle size, 6V/S, for a cylinder."""
    v = math.pi * d * d / 4 * l
    s = math.pi * d * l + math.pi * d * d / 2
    return 6 * v / s


# ------------------------------------------------------------------ 1. the stack
# (name, propellant kg a package, package OD m, package length m, rack pitch over OD m, tag)
PACKAGES = [
    # RN: two quarter charges to a metal case, lids both ends, racked lid-end to the passage [SRC: Harley, from the
    # 1914-16 drill books; navweaps tech-122, Jutland's KGV-era counts]. Sizes [EST]: a 13.5" cartridge 0.33 m x 0.35 m,
    # a 15" 0.37 x 0.41 (bag density 1.1 t/m3), the case 2 cm over and 5 cm longer than its pair.
    ("RN 13.5in, 2 x 33 kg in a case", 66.5, 0.37, 0.77, 0.03, "[EST] sizes; 133 kg charge [SRC navweaps]"),
    ("RN 15in, 2 x 48.5 kg in a case", 97.0, 0.41, 0.89, 0.03, "[EST] sizes; 194 kg charge [SRC navweaps]"),
    # German 30.5 cm: main charge 91 kg in a brass case 305 x 1,271 mm, rim 350 [SRC navweaps]; fore 34.5 kg in silk in a tin
    ("German 30.5 cm main, brass case", 91.0, 0.35, 1.27, 0.02, "[SRC] case; pitch [EST]"),
    ("German 30.5 cm fore charge in its tin", 34.5, 0.34, 0.50, 0.02, "[EST] tin size"),
    # USN: tanks stacked horizontally in tiers, interlocked by rings, no racks [SRC OP 1937 ch. XIV; Slover NC]; 16"/50 a tank of
    # 3 x 110 lb bags [SRC navweaps]; tank size [EST] 0.46 x 1.95 m over the rings
    ("USN 16in/50, 3 x 50 kg bags in a tank", 150.0, 0.46, 1.95, 0.01, "[SRC] contents; size [EST]"),
    ("USN 14in, 2 x 48 kg bags in a tank", 96.0, 0.40, 1.30, 0.01, "[SRC] 2 tanks a charge; size [EST]"),
]


def stack_bulk(m, od, l, gap, hex_pack=False):
    """kg of propellant a m3 of a rack of horizontal cylinders: square (racks) or hexagonal (tiers) pitch."""
    p = od + gap
    area = p * p * (math.sqrt(3) / 2 if hex_pack else 1)
    return m / (area * (l + 0.03))


def section_stack():
    print("\n1. THE STACK: propellant a m3 of the stack (aisles and shells out)")
    print(f"{'package':42s} {'square':>7s} {'hex':>7s}  tag")
    for name, m, od, l, gap, tag in PACKAGES:
        print(f"{name:42s} {stack_bulk(m, od, l, gap):7.0f} {stack_bulk(m, od, l, gap, True):7.0f}  {tag}")
    # bags piled loose (charges out of their cases, the 1916 'rate first' stacks): a random pile of short cylinders
    for rho_bag in (1000, 1100, 1200):
        for eps in (0.36, 0.42):
            print(f"  bags piled, bag {rho_bag} kg/m3, voids {eps}: {rho_bag * (1 - eps):5.0f} kg/m3")
    print("  a whole magazine room over its volume: 29 t in Lion's Q (340 m3 with shells) = "
          f"{29000 / 340:.0f} kg/m3 (the code's note)")


# ------------------------------------------------------------------ 2. the drag
def ergun_k(eps, dp):
    """Ergun's inertial coefficient over a length: dP/dx = k G^2 / rho, k = 1.75 (1-eps)/eps^3 / dp."""
    return 1.75 * (1 - eps) / eps ** 3 / dp


def section_drag():
    print("\n2. THE DRAG: Ergun's inertial term at two scales")
    code_eps = 1 - 600 / MD.rho
    code_dp = (133 / MD.rho) ** (1 / 3)
    rows = [
        ("code: 133 kg charge, voids from cordite's density", code_eps, code_dp),
        ("bags piled: a quarter charge 0.33x0.35, voids 0.40", 0.40, cyl_dp(0.33, 0.35)),
        ("bags piled, voids 0.36 (dense random)", 0.36, cyl_dp(0.33, 0.35)),
        ("among the cords in a bag (MD45, bag 1.1 t/m3), across", 1 - 1100 / MD.rho, 1.5 * 0.0114),
    ]
    for name, eps, dp in rows:
        print(f"  {name:55s} eps {eps:.2f} dp {dp * 1000:6.1f} mm  k {ergun_k(eps, dp):9.1f} /m")
    print("  ratio, piled bags at 0.40 against the code's:", round(ergun_k(0.40, cyl_dp(0.33, 0.35)) / ergun_k(code_eps, code_dp), 1))


# ------------------------------------------------------------------ 3. a bed held still
def code_lumped(pr, room, bulk, depth, dobs, keep=KEEP):
    """The code's GasNetwork.BedPressure for a whole bed alight: P (P - room) = K L RT (G/r)^2 r(P)^2."""
    solid = min(bulk / pr.rho, 0.74)
    k = 1.75 * solid / (1 - solid) ** 3 / dobs
    per = pr.rho * pr.skg * bulk * depth          # G over r
    c = k * depth * keep * pr.f * per * per
    p = max(room, 1.0)
    for _ in range(60):   # Newton's on f(p) = p (p - room) - c r^2
        rr = pr.r(p)
        fval = p * (p - room) - c * rr * rr
        dfd = 2 * p - room - 2 * c * rr * rr * pr.alpha(p) / p
        p = max(p - fval / max(dfd, 1e-9), room)
    return p


def steady_profile(pr, room, bulk, depth, k, keep=KEEP, face_eps=None, n=400):
    """The exact steady bed, closed at its back, open at its face: dG/dx = q(P), P dP/dx = -k RT G^2.
    The face chokes if its gas can't leave through the gaps at the room's pressure. Returns (P back, mass-mean r / r(room), G face)."""
    rt = keep * pr.f
    q0 = bulk * pr.skg * pr.rho          # kg/m3 s per (m/s of burning)
    face_eps = face_eps if face_eps is not None else 0.4

    def shoot(pb):
        p, g, dx, rsum = pb, 0.0, depth / n, 0.0
        for _ in range(n):
            rr = pr.r(p)
            rsum += rr
            g += q0 * rr * dx
            p2 = p * p - 2 * k * rt * g * g * dx
            if p2 <= 0:
                return -1.0, g, rsum / n
            p = math.sqrt(p2)
        return p, g, rsum / n

    lo, hi = room, room * 2
    while True:
        pf, g, _ = shoot(hi)
        pexit = max(room, g * math.sqrt(rt) / (face_eps * CD * CHOKE))
        if pf >= pexit:
            break
        hi *= 2
        if hi > 1e10:
            return float('inf'), float('inf'), float('inf')
    for _ in range(80):
        mid = (lo + hi) / 2
        pf, g, _ = shoot(mid)
        pexit = max(room, g * math.sqrt(rt) / (face_eps * CD * CHOKE))
        if pf >= pexit:
            hi = mid
        else:
            lo = mid
    pf, g, rmean = shoot(hi)
    return hi, rmean / pr.r(room), g


def section_steady():
    print("\n3. A BED HELD STILL (MD45, all alight, RT = 0.5 f): pressure at the back, MPa abs")
    code_dp = (133 / MD.rho) ** (1 / 3)
    code_k = ergun_k(1 - 600 / MD.rho, code_dp)
    pile_k = ergun_k(0.40, cyl_dp(0.33, 0.35))
    print(f"{'depth m':>8s} {'room':>5s} | {'code lumped':>11s} {'exact, code k':>14s} {'exact, piled bags':>18s} {'r gain':>7s}")
    for depth in (0.5, 1.0, 1.5, 2.0, 2.5):
        for room in (0.1e6, 0.3e6, 1.0e6):
            cl = code_lumped(MD, room, 600, depth, code_dp)
            ex, _, _ = steady_profile(MD, room, 600, depth, code_k, face_eps=1 - 600 / MD.rho)
            pb, gain, _ = steady_profile(MD, room, 660, depth, pile_k, face_eps=0.40)
            print(f"{depth:8.1f} {room / 1e6:5.1f} | {cl / 1e6:11.2f} {ex / 1e6:14.2f} {pb / 1e6:18.2f} {gain:7.2f}")
    # where the exponent comes from
    print("  the law: P ~ (K L^3)^(1/(2-2 alpha)); alpha 0.78 -> L^%.1f; single-base 0.88 -> L^%.1f; alpha 1 -> no steady bed"
          % (3 / (2 - 2 * 0.78), 3 / (2 - 2 * 0.88)))
    # Reynolds and Mach at the face of the piled bed, 1.5 m, room 0.3 MPa
    pb, _, g = steady_profile(MD, 0.3e6, 660, 1.5, pile_k, face_eps=0.40)
    rho = 0.3e6 / (KEEP * MD.f)
    dp = cyl_dp(0.33, 0.35)
    re = g * dp / (MU * (1 - 0.40))
    mach = g / (0.40 * rho) / math.sqrt(GAMMA * KEEP * MD.f)
    print(f"  piled bags 1.5 m, room 0.3: G face {g:.0f} kg/m2 s, Re_p {re:.1e}, Mach in the gaps {mach:.2f}; viscous term / inertial "
          f"{150 * MU * (1 - 0.4) / (1.75 * g * dp):.1e}")


# ------------------------------------------------------------------ 4. a bed free to move
def bed_moves(pr, room, bulk, depth, eps0, dp, spread_s=0.0, held=0.0, keep=KEEP, tmax=1.0, dt=2e-6, frozen=False):
    """A bed against a bulkhead, its face to the aisle, alight over spread_s (0: all at once). Lumped, a m2 of face:
    the gas in its gaps (P = m RT / V, the gaps' volume eps L), made at r(P) by what's alight, leaving through the bed
    (Ergun, P^2 - Pf^2 = 2/3 k RT G^2 L) and its face (choked through its gaps if need be); the bed driven by P - room,
    stretching uniformly (a third of the column's mass), the racks holding `held` Pa. frozen: the bed can't move.
    Returns (peak P - room Pa, time of the peak s, time the bed has stretched by half s, burned share by then)."""
    rt = keep * pr.f
    sigma = bulk * depth
    solid0 = 1 - eps0
    s = v = z = t = 0.0
    mg = room * eps0 * depth / rt
    peak, tpeak, choked = 0.0, 0.0, False
    while t < tmax and z < 1:
        L = depth + s
        eps = 1 - solid0 * depth / L
        vv = eps * L
        p = mg * rt / vv
        lit = 1.0 if spread_s <= 0 else min(1.0, t / spread_s)
        shape = math.sqrt(max(1 - z, 0)) if pr.form == "cord" else 1.0
        made = bulk * depth * pr.skg * pr.rho * pr.r(p) * shape * lit
        k = ergun_k(eps, dp)
        # flux out: solve for G with the face either at the room's pressure or choked
        gmax_choke = lambda pf: eps * CD * CHOKE * pf / math.sqrt(rt)
        if p <= room:
            g = 0.0
        else:
            g_bed = math.sqrt(max(p * p - room * room, 0) / (2 / 3 * k * rt * L))
            if g_bed <= gmax_choke(room / CRIT):
                g = g_bed
            else:   # choked at the face: P^2 - Pf^2 = c G^2, G = a Pf
                a, c = eps * CD * CHOKE / math.sqrt(rt), 2 / 3 * k * rt * L
                g = p / math.sqrt(c + 1 / (a * a))
        dpr = p - room
        if dpr > peak:
            peak, tpeak, choked = dpr, t, g > gmax_choke(room / CRIT) * 0.999
        if not frozen:
            acc = max(dpr - held, 0) / (sigma / 3)
            v += acc * dt
            s += v * dt
        mg += (made - g) * dt
        z += made / (bulk * depth) * dt
        t += dt
        if s > depth / 2:
            return peak, tpeak, t, z, v, choked
    return peak, tpeak, float('nan'), z, v, choked


def section_moves():
    print("\n4. A BED THAT HOLDS ITS GAS AND MOVES (MD45 piled bags 0.33 x 0.35 m, voids 0.40, 660 kg/m3)")
    dp = cyl_dp(0.33, 0.35)
    print(f"{'depth':>6s} {'room':>5s} {'lit over':>8s} | {'peak dP kPa':>11s} {'at ms':>6s} {'face':>6s} {'open ms':>8s} {'burned':>7s} {'thrown m/s':>10s} | {'held still, 50 ms on':>20s}")
    for depth in (1.0, 1.5, 2.5):
        for room in (0.1e6, 0.3e6, 1.0e6):
            for spread in (0.0, 0.01, 0.2):
                pk, tp, to, z, v, ch = bed_moves(MD, room, 660, depth, 0.40, dp, spread_s=spread, tmax=0.6)
                pf = bed_moves(MD, room, 660, depth, 0.40, dp, spread_s=spread, frozen=True, tmax=spread + 0.05, dt=1e-5)[0]
                print(f"{depth:6.1f} {room / 1e6:5.1f} {spread:8.2f} | {pk / 1e3:11.0f} {tp * 1000:6.2f} {'choked' if ch else 'Ergun':>6s} {to * 1000:8.1f} {z * 100:6.2f}% {v:10.1f} | {pf / 1e3:20.0f}")
    print("  sensitivity, 1.5 m, room 0.3, all at once: (voids, Ergun's B) -> peak kPa, burned")
    for eps in (0.30, 0.40, 0.50):
        for bmul in (0.5, 1.0, 2.3):
            k_dp = dp / bmul
            pk, tp, to, z, v, ch = bed_moves(MD, 0.3e6, 1100 * (1 - eps), 1.5, eps, k_dp, tmax=0.6)
            print(f"    voids {eps:.2f} B {1.75 * bmul:.2f}: {pk / 1e3:7.0f} kPa at {tp * 1000:5.1f} ms, burned {z * 100:5.2f}%")
    print("  the code's lumped bed for the same, steady (kPa over the room):")
    for depth in (1.0, 1.5, 2.5):
        print("   ", depth, [round((code_lumped(MD, room, 600, depth, (133 / MD.rho) ** (1 / 3)) - room) / 1e3) for room in (0.1e6, 0.3e6, 1e6)])
    print("  what nothing but the racks can hold over a burn: dP to move the column 0.15 m in its burn time")
    for room in (0.1e6, 1e6, 5e6):
        tb = MD.web / MD.r(room)
        print(f"    {room / 1e6:.1f} MPa: burn {tb:.1f} s, 1.5 m deep: {2 * 0.15 * 660 * 1.5 / 3 / tb ** 2:.1f} Pa")


# ------------------------------------------------------------------ 5. a lit package
def vent_out(p, room, area, rt):
    """kg/s through an opening from p into room: choked, or subsonic below the critical ratio."""
    if p <= room:
        return 0.0
    if room / p < CRIT:
        return CD * area * CHOKE * p / math.sqrt(rt)
    x = room / p
    return CD * area * p / math.sqrt(rt) * math.sqrt(2 * GAMMA / (GAMMA - 1) * (x ** (2 / GAMMA) - x ** ((GAMMA + 1) / GAMMA)))


def balance(made_fn, out_fn, room):
    """The pressure where gas made = gas out (made grows as P^alpha < 1, so one root above the room)."""
    lo, hi = room, 1e9
    for _ in range(200):
        p = math.sqrt(lo * hi)
        lo, hi = (p, hi) if made_fn(p) > out_fn(p) else (lo, p)
    return hi


def mouth_pressure(pr, kg, mouth_m2, room, keep):
    """A package alight inside with an open mouth: gas made = what leaves the mouth."""
    rt = keep * pr.f
    return balance(lambda p: pr.rho * kg * pr.skg * pr.r(p), lambda p: vent_out(p, room, mouth_m2, rt), room)


def hoop_burst(od, wall, sigma_u):
    return 2 * sigma_u * wall / od


def section_packages():
    print("\n5. A LIT PACKAGE: its own mouth (a lid off or blown), MPa abs; burst of its wall [EST]")
    cases = [
        ("RN 13.5in case, lid off, 2 cartridges lit", MD, 66.5, math.pi / 4 * 0.35 ** 2, hoop_burst(0.37, 0.0012, 300e6)),
        ("RN 13.5in case, lid off, the near one ejected", MD, 33.2, math.pi / 4 * 0.35 ** 2, hoop_burst(0.37, 0.0012, 300e6)),
        ("RN 15in case, lid off, 2 cartridges lit", MD, 97.0, math.pi / 4 * 0.39 ** 2, hoop_burst(0.41, 0.0012, 300e6)),
        ("German 30.5 cm brass case, lit inside", RP, 91.0, math.pi / 4 * 0.29 ** 2, hoop_burst(0.305, 0.0025, 350e6)),
        ("German fore charge, tin lid off", RP, 34.5, math.pi / 4 * 0.32 ** 2, hoop_burst(0.34, 0.0008, 250e6)),
        ("USN 16in/50 tank, lid off, 3 bags", SB, 150.0, math.pi / 4 * 0.44 ** 2, hoop_burst(0.46, 0.0016, 250e6)),
        ("a quarter charge alone in the open (its cords)", MD, 33.2, 0.0, 0),
    ]
    print(f"{'package':48s} {'room 0.1':>8s} {'0.3':>6s} {'1.0':>6s} {'hot gas':>8s} {'burst':>6s} {'rate gain':>9s}")
    for name, pr, kg, a, burst in cases:
        if a <= 0:
            continue
        row = [mouth_pressure(pr, kg, a, room, KEEP) / 1e6 for room in (0.1e6, 0.3e6, 1e6)]
        hot = mouth_pressure(pr, kg, a, 0.1e6, 1.0) / 1e6
        gain = pr.r(row[0] * 1e6) / pr.r(0.1e6)
        print(f"{name:48s} {row[0]:8.2f} {row[1]:6.2f} {row[2]:6.2f} {hot:8.2f} {burst / 1e6:6.2f} {gain:9.1f}")
    # the near cartridge as a piston in its case: how long it stays
    p = mouth_pressure(MD, 66.5, math.pi / 4 * 0.35 ** 2, 0.1e6, KEEP)
    a = (p - 0.1e6) * math.pi / 4 * 0.33 ** 2 / 33.2
    print(f"  the near 13.5in cartridge thrown out of its case: {a:.0f} m/s2, out in {1000 * math.sqrt(2 * 0.38 / a):.0f} ms")
    p = mouth_pressure(RP, 91.0, math.pi / 4 * 0.29 ** 2, 0.1e6, KEEP)
    a = (min(p, hoop_burst(0.305, 0.0025, 350e6)) - 0.1e6) * math.pi / 4 * 0.29 ** 2 / 91.0
    print(f"  a German main charge's bundle thrown out of its brass case: {a:.0f} m/s2, out in {1000 * math.sqrt(2 * 1.2 / a):.0f} ms")
    # an RP C/12 tube's own bore: 8 mm, the gas of the inner face out of its two ends (1,230 mm), choked or not at the ends
    for room in (0.1e6, 0.3e6, 1e6):
        for keep in (KEEP, 1.0):
            p = balance(lambda p: RP.rho * RP.r(p) * math.pi * 0.008 * 1.23,
                        lambda p: 2 * vent_out(p, room, math.pi / 4 * 0.008 ** 2, keep * RP.f), room)
            print(f"  RP C/12 1,230 x 18/8 mm, inside its bore, room {room / 1e6:.1f}, RT {keep:.1f} f: {p / 1e6:.2f} MPa abs, inner face x{RP.r(p) / RP.r(room):.2f}")
    # one bag alone: the intra-bag bed, radial, slab of half the radius
    k = ergun_k(1 - 1100 / MD.rho, 1.5 * 0.0114)
    pb, gain, _ = steady_profile(MD, 0.1e6, 1100, 0.33 / 4, k, face_eps=1 - 1100 / MD.rho)
    print(f"  a quarter charge alone, among its cords at 1 atm: {pb / 1e6:.3f} MPa abs, rate x{gain:.2f}")
    pb, gain, _ = steady_profile(MD, 1.0e6, 1100, 0.33 / 4, k, face_eps=1 - 1100 / MD.rho)
    print(f"  the same in a room at 1 MPa: {pb / 1e6:.3f} MPa abs, rate x{gain:.2f}")


if __name__ == "__main__":
    section_stack()
    section_drag()
    section_steady()
    section_moves()
    section_packages()
