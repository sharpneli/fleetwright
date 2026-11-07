"""big_explosions_ref.py - big explosions in and beside ships (research note 13-big-explosions.md).

Pure Python, a few seconds. Every derived number in the note comes out of this file.
  1. gas through a hull: how fast a pressure crosses it against how fast the gas can fill what's beyond
  2. a belt pushed from inside: what holds a side with armour on it, and how long its own mass holds it
  3. the bubble: Cole's period and radius against Cheonan and the DST cylinder
  4. the hull's two-node mode: the free-free beam with Kumai's virtual mass, for the code's ship types
  5. whipping: a bubble under or beside a hull through the two-node mode, in design moments, against the cases
  6. the gas whip of an internal burst, the same mode against the code's E*m/M law
  7. confined gas above 1 kg/m3: the oxygen limit and the co-volume against Weibull's fit
  8. bombs: fall speed by release height and type, against Tallboy's and Grand Slam's recorded speeds
Tags as in the note: [SRC] sourced, [INFERRED] reasoning or a fit, [EST] an estimate.
"""
import math

G = 9.81
RHO_W = 1025.0
P_ATM = 101325.0
E_STEEL = 2.06e11
SIGMA_Y = 280e6          # mild/HT hull steel yield, Pa [EST: 235-350]


def section(title):
    print()
    print("=" * 100)
    print(title)
    print("=" * 100)


# ------------------------------------------------------------------ ships (representative; I as the code's HullGirder reads)
class Ship:
    def __init__(s, name, L, B, T, disp_t, I_m4, cap_dm):
        s.name, s.L, s.B, s.T, s.disp, s.I, s.cap = name, L, B, T, disp_t, I_m4, cap_dm

    @property
    def virtual_t(s):
        """Kumai's virtual displacement: the ship and the water that moves with her in vertical bending [SRC Kumai 1958]."""
        return s.disp * (1.2 + s.B / (3 * s.T))

    @property
    def design_nm(s):
        """shipgen's design moment, displacement x g x L / 40."""
        return s.disp * 1000 * G * s.L / 40


SHIPS = [
    # name, L m, B m, T m, displacement t, I m4 (explosions.md EX3 table; [EST] for the cruiser and Cheonan), capacity in design moments
    Ship("Cheonan (Pohang PCC)", 88.3, 10.0, 2.9, 1220, 1.1, 3.5),       # I [EST] scaled from the destroyer by B*D^2; cap [EST]
    Ship("Destroyer", 110.0, 11.0, 3.5, 2000, 3.3, 4.09),
    Ship("Light cruiser", 170.0, 18.0, 6.0, 10000, 40.0, 2.8),          # [EST]
    Ship("Lion", 213.0, 27.0, 8.4, 26000, 210.0, 2.62),
    Ship("Yamato", 256.0, 38.9, 10.4, 65000, 216.0, 2.56),
]

# ------------------------------------------------------------------ 1. gas through a hull
section("1. Gas through a hull: the pressure crosses at the speed of sound, the gas follows through what's open")
print("""A pressure rise in one space reaches the next as a wave at the speed of sound in the gas it passes, but what it does
there depends on how much gas follows it through the openings. For a space V2 fed through an opening A from a source at
P1, the time to fill it to the source's pressure is about tau = V2 / (Cd*A*c*), with c* the choked mass flux's velocity.""")
c_cold, c_hot = 340.0, 800.0          # air; hot products at ~1,500-2,500 K [INFERRED]
print(f"  wave transit over 200 m: cold air {200 / c_cold:.2f} s, hot products {200 / c_hot:.2f} s")
cd, cstar = 0.7, 0.58 * 600           # choked flow: about 0.58 x the hot gas's sound speed through the throat [INFERRED]
print("  fill time tau = V/(Cd*A*0.58c), hot gas (c 600 m/s), seconds:")
print("      V m3 \\ A m2:   0.1      0.5      2       10")
for v in (100, 500, 2000, 10000):
    row = "  ".join(f"{v / (cd * a * cstar):7.2f}" for a in (0.1, 0.5, 2, 10))
    print(f"   {v:7d}       {row}")


def two_volumes(t_burn, v1, v2, area, e_tot_j, steps=40000):
    """A source V1 releasing e_tot over t_burn into its gas, venting through `area` into a closed V2. Lumped cells,
    P = (gamma-1)*E/V each; the energy crossing is the gas's enthalpy at its velocity through the opening, capped at
    the hot gas's sound speed (choked) [INFERRED]. Returns the source's peak, the far side's pressure at that moment,
    and when the two first come within 20% of each other."""
    gam, rho_hot, c_hot = 1.25, 0.5, 600.0
    e1 = e2 = 0.0
    p1max = p2_at = 0.0
    t_eq = None
    tend = max(5 * t_burn, 10.0)
    dt = tend / steps
    for i in range(steps):
        t = i * dt
        if t < t_burn:
            e1 += e_tot_j / t_burn * dt
        p1, p2 = (gam - 1) * e1 / v1, (gam - 1) * e2 / v2
        dp = p1 - p2
        u = min(math.sqrt(2 * abs(dp) / rho_hot), c_hot)
        flux = cd * area * u * gam / (gam - 1) * max(p1, p2)
        de = math.copysign(min(flux * dt, abs(dp) * v1 * v2 / ((v1 + v2) * (gam - 1))), dp)
        e1 -= de
        e2 += de
        if p1 > p1max:
            p1max, p2_at = p1, p2
        if t_eq is None and p1 > 1e3 and abs(p1 - p2) < 0.2 * p1:
            t_eq = t
    return p1max, p2_at, t_eq


print("""
  Two spaces, 400 m3 lit (a magazine and its handling room), the next 2,000 m3 (an engine room) beyond a torn bulkhead,
  10 t of cordite's worth of energy (impetus 1.0 MJ/kg x 0.5 kept) released over t_burn, no other vent [INFERRED, lumped].
  Source's peak MPa / the far side's at that moment / seconds until they are within 20%:""")
print("     t_burn s \\ opening m2:     0.5                     2                       10")
for tb in (0.01, 0.05, 0.3, 1.0, 3.0):
    cells = []
    for a in (0.5, 2.0, 10.0):
        p1, p2, te = two_volumes(tb, 400.0, 2000.0, a, 10e3 * 1.0e6 * 0.5)
        cells.append(f"{p1 / 1e6:5.2f}/{p2 / 1e6:4.2f}/{te:5.2f} s")
    print(f"     {tb:6.2f}            " + "    ".join(cells))
print("""  The far side's pressure at the source's peak is what a far boundary feels while the near ones are being loaded:
  a burst (ms) or a fast flash through a small opening leaves it at a fraction; a burn of a second or more brings them
  together before the source peaks. What decides is t_burn against V/(Cd*A*c*), not the hull's length.""")

# ------------------------------------------------------------------ 2. a belt pushed from inside
section("2. A belt pushed from inside: a heavy panel held by its edge connections, not a membrane")


def belt_panel(h_m, t_armour_mm, backing_mm, rivet_d_mm, rivets_per_m, rows, tau_rivet=300e6):
    """A strake of belt h_m high on its backing, pushed outward by P inside. The armour bolts tie plate to backing, so they
    move together; what holds the unit is the backing's riveted joints to the decks above and below it (and to the
    plating round it), in shear. Capacity per m of length = 2 edges x rows x rivets/m x rivet shear; over h -> pressure."""
    a_rivet = math.pi * (rivet_d_mm / 1000) ** 2 / 4
    per_edge = rows * rivets_per_m * a_rivet * tau_rivet
    p_hold = 2 * per_edge / h_m
    areal = (t_armour_mm + backing_mm) / 1000 * 7850
    return p_hold, areal


print("  Strake        h m  armour mm  backing mm  rivets       holds MPa   kg/m2   a strake's own plate (0.06 MPa/mm x backing)")
for name, h, ta, tb, rd, rpm, rows in [
    ("Destroyer side, no belt", 3.0, 0, 10, 16, 20, 1),
    ("Light cruiser 76 mm", 3.0, 76, 15, 22, 18, 2),
    ("Battlecruiser 229 mm", 3.5, 229, 25, 25, 16, 2),
    ("Battleship 305 mm", 3.8, 305, 30, 25, 16, 3),
    ("Battleship 343 mm", 4.0, 343, 40, 28, 16, 3)]:
    p, m = belt_panel(h, ta, tb, rd, rpm, rows)
    print(f"  {name:22s}{h:4.1f}  {ta:7d}  {tb:9d}   {rows}x{rpm}/m {rd} mm   {p / 1e6:6.2f}   {m:7.0f}   {0.06 * tb:.2f} MPa")
print("  [EST] joints: rows, pitch and rivet sizes are typical, not any class's.")
print("""
  Its own mass holds it for a while: under a net pressure P it moves s = P/(2 m'') t^2. Time to move 0.3 m (a joint well
  open, gas going out round it):""")
print("     kg/m2 \\ P MPa:    0.3     1      3")
for m in (80, 800, 2000, 2900):
    row = "  ".join(f"{1000 * math.sqrt(2 * 0.3 * m / (p * 1e6)):6.1f}" for p in (0.3, 1, 3))
    print(f"     {m:6d}        {row}   ms")
print("""  So against a shell's burst (a pulse of ms) a belt is a wall; against a magazine's burn (100s of ms) it is a heavy door
  held by its riveted edges: about one to two times what its backing would hold as a membrane, and nowhere near its
  armour's millimetres as one (229 mm x 0.06 MPa/mm = 14 MPa). When it goes, the whole strake opens at once, a plate's
  area (2.5-4.5 m by 4-6 m, 10-25 m2), not a tear that grows.""")

# ------------------------------------------------------------------ 3. the bubble
section("3. The bubble: Cole's similitude [SRC Cole 1948; Reid 1996; Swisdak]")
K_T, J_R = 2.11, 3.50   # first period s kg^-1/3 m^5/6, max radius m kg^-1/3 m^1/3, TNT, Z = depth + 9.8 (Reid; DST K1 2.12, J 3.52)


def bubble_period(w_kg, depth_m, k=K_T):
    return k * w_kg ** (1 / 3) / (depth_m + 9.8) ** (5 / 6)


def bubble_rmax(w_kg, depth_m, j=J_R):
    return j * (w_kg / (depth_m + 9.8)) ** (1 / 3)


print(f"  Cheonan, 136 kg at 8 m (Kim & Gitterman's fit): T = {bubble_period(136, 8):.3f} s against 0.990 s observed [SRC]")
print(f"           250 kg at 6-9 m (the official charge): T = {bubble_period(250, 9):.2f}-{bubble_period(250, 6):.2f} s")
print(f"  Rayleigh's collapse gives the same law: T = 1.83 Rmax sqrt(rho/p_inf) -> {1.83 * J_R / math.sqrt(G):.2f} W^1/3 Z^-5/6")
print("  First period, s, and max radius, m (TNT):")
print("     W kg \\ depth m:     5           10          20          40")
for w in (25, 100, 300, 1000, 2400, 5000):
    row = "  ".join(f"{bubble_period(w, d):4.2f}/{bubble_rmax(w, d):4.1f}" for d in (5, 10, 20, 40))
    print(f"     {w:6d}         {row}")

# ------------------------------------------------------------------ 4. the two-node mode
section("4. The hull's two-node mode: a free-free beam with Kumai's virtual mass")
BL = 4.7300408
SIG = (math.cosh(BL) - math.cos(BL)) / (math.sinh(BL) - math.sin(BL))
NX = 401


def mode(xi):
    a = BL * xi
    return math.cosh(a) + math.cos(a) - SIG * (math.sinh(a) + math.sin(a))


def mode_dd(xi):
    """d2 phi / d xi2 / BL^2"""
    a = BL * xi
    return math.cosh(a) - math.cos(a) - SIG * (math.sinh(a) - math.sin(a))


_phi = [mode(i / (NX - 1)) for i in range(NX)]
_norm = math.sqrt(sum(p * p for p in _phi) / NX)   # RMS over the length -> phi normalised to RMS 1
PHI = [p / _norm for p in _phi]
PHIDD = [mode_dd(i / (NX - 1)) / _norm for i in range(NX)]


def omega(ship):
    m = ship.virtual_t * 1000 / ship.L
    return BL ** 2 * math.sqrt(E_STEEL * ship.I / (m * ship.L ** 4))


print(f"  mode: |phi| at the ends {abs(PHI[0]):.2f}, amidships {abs(PHI[NX // 2]):.2f}; curvature peaks amidships")
print("  Ship                   L m   disp t  virtual t   I m4    f2 Hz   period s   (Kumai 3.07e6*sqrt(I/(Dv L^3)) cpm)")
for s in SHIPS:
    f = omega(s) / (2 * math.pi)
    kumai = 3.07e6 * math.sqrt(s.I / (s.virtual_t * s.L ** 3)) / 60
    print(f"  {s.name:22s}{s.L:5.0f} {s.disp:8.0f} {s.virtual_t:9.0f} {s.I:7.1f}  {f:6.2f}   {1 / f:6.2f}     {kumai:.2f} Hz")
print("  A uniform beam reads high: shear, the ends' small sections and superstructure lower f by 10-30% [INFERRED].")

# ------------------------------------------------------------------ 5. whipping
section("5. Whipping: a pulsating bubble through the two-node mode")


VENT_SHARE = 1.0     # the bubble breaks the surface when Rmax reaches its depth [EST: Cole; Cheonan, Rmax 6.9 m at 8 m, pulsed]


def vents(w_kg, depth_m):
    return bubble_rmax(w_kg, depth_m) >= VENT_SHARE * depth_m


def bubble_history(w_kg, depth_m, cycles=2, r_min_ratio=0.15, gam=1.25, loss=0.55):
    """Rayleigh-Plesset (incompressible, no surface) from a minimum at r_min_ratio*Rmax, adiabatic gas sized so the
    expansion reaches Cole's Rmax; at each later minimum the gas keeps `loss` of its energy (Cole's second period,
    K2 1.73 against K1 2.12, gives (1.73/2.12)^3 = 0.54). A bubble that breaks the surface (vents()) has no collapse:
    its history stops at its first maximum, its gas gone to the air. Returns (t, V, Vddot) lists."""
    p_inf = P_ATM + RHO_W * G * depth_m
    rmax = bubble_rmax(w_kg, depth_m)
    r0 = r_min_ratio * rmax
    v0, vmax = 4 / 3 * math.pi * r0 ** 3, 4 / 3 * math.pi * rmax ** 3
    pg0 = p_inf * (vmax - v0) * (gam - 1) / (v0 * (1 - (v0 / vmax) ** (gam - 1)))
    r, rd, t = r0, 0.0, 0.0
    period = bubble_period(w_kg, depth_m)
    dt = period / 20000
    ts, vs, vdds = [], [], []
    cyc, falling = 0, False
    kgas = pg0 * r0 ** (3 * gam)
    while cyc < cycles and t < 4 * period:
        pg = kgas / r ** (3 * gam)
        rdd = ((pg - p_inf) / RHO_W - 1.5 * rd * rd) / r
        # V'' = 4 pi (2 R Rd^2 + R^2 Rdd)
        vdd = 4 * math.pi * (2 * r * rd * rd + r * r * rdd)
        ts.append(t)
        vs.append(4 / 3 * math.pi * r ** 3)
        vdds.append(vdd)
        # semi-implicit step with a step that shrinks near the minimum
        h = dt * min(1.0, max(r / rmax, 0.02) * 4)
        rd += rdd * h
        r += rd * h
        t += h
        if rd < 0:
            if not falling and vents(w_kg, depth_m):
                break                    # blown out at its first maximum
            falling = True
        if falling and rd >= 0:          # a minimum: next cycle
            cyc += 1
            falling = False
            kgas *= loss
            r = max(r, 1e-3)
    return ts, vs, vdds


def whip(ship, w_kg, depth_m, off_m=0.0, at=0.5, zeta=0.03, wall=1.0):
    """Peak two-node bending moment, in design moments, from a bubble at depth_m, off_m to the side of the keel line,
    at the station `at` (0 bow, 1 stern). The pressure on her bottom is the incompressible source's, rho*Vdd/(4 pi r),
    less the free surface's image; `wall` a factor for the hull's own reflection (1 none, 2 a rigid wide bottom).
    Integrated over her flat of bottom (B wide, 5 strips) and her length; the mode's response by Duhamel's integral."""
    ts, vs, vdds = bubble_history(w_kg, depth_m)
    om = omega(ship)
    m_line = ship.virtual_t * 1000 / ship.L
    mstar = m_line * ship.L          # phi normalised to RMS 1
    xs = [i / (NX - 1) * ship.L for i in range(NX)]
    dx = ship.L / (NX - 1)
    xb = at * ship.L
    ys = [(-0.5 + (k + 0.5) / 5) * ship.B for k in range(5)]
    dz_down, dz_img = depth_m - ship.T, depth_m + ship.T
    # geometric influence: sum over the bottom of phi(x) * (1/r - 1/r') * dA
    infl = 0.0
    for i, x in enumerate(xs):
        for y in ys:
            r1 = math.sqrt((x - xb) ** 2 + (y - off_m) ** 2 + dz_down ** 2)
            r2 = math.sqrt((x - xb) ** 2 + (y - off_m) ** 2 + dz_img ** 2)
            infl += PHI[i] * (1 / max(r1, 0.5) - 1 / r2) * dx * ship.B / 5
    # modal force F*(t) = rho*Vdd(t)/(4pi) * infl * wall; response q by a Duhamel step (exact for piecewise-constant force)
    q, qd, qmax = 0.0, 0.0, 0.0
    wd = om * math.sqrt(1 - zeta ** 2)
    tlast = ts[-1]
    t_free = tlast + 3 / (om / (2 * math.pi))
    i = 0
    t = 0.0
    while t < t_free:
        if i < len(ts) - 1:
            h = ts[i + 1] - ts[i]
            f = wall * RHO_W * vdds[i] / (4 * math.pi) * infl / mstar
            i += 1
        else:
            h = 0.002 / (om / (2 * math.pi))
            f = 0.0
        # exact SDOF step under constant force f
        qs = f / (om * om)
        e = math.exp(-zeta * om * h)
        c, s = math.cos(wd * h), math.sin(wd * h)
        a0, b0 = q - qs, (qd + zeta * om * (q - qs)) / wd
        q = qs + e * (a0 * c + b0 * s)
        qd = e * (-zeta * om * (a0 * c + b0 * s) + (-a0 * wd * s + b0 * wd * c))
        qmax = max(qmax, abs(q))
        t += h
    curv = max(abs(v) for v in PHIDD[NX // 4: 3 * NX // 4]) * (BL / ship.L) ** 2
    m_peak = E_STEEL * ship.I * qmax * curv
    return m_peak / ship.design_nm


print("""  The pressure a bubble puts on her bottom is the incompressible flow's, rho*V''/(4 pi r): it changes sign through
  each cycle and integrates to nearly nothing, so what bends her is how much of it lands at her own frequency. The
  model below is a uniform free-free beam, the bubble Rayleigh-Plesset sized to Cole's Rmax, the surface's image, 3%
  damping, no hull reflection (wall 1; a rigid wide bottom would double the pressure). It gives the moment's order and
  its trends, not a class's number [INFERRED].""")
print("\n  Peak whipping moment amidships, design moments, a charge under the keel amidships, depth of the charge below the surface:")
for s in SHIPS:
    print(f"  {s.name} (f2 {omega(s) / 2 / math.pi:.2f} Hz, capacity ~{s.cap} design moments)")
    print("     W kg TNT \\ depth m:   " + "".join(f"{d:>8d}" for d in (6, 10, 15, 25, 40)))
    for w in (100, 300, 1000, 2400, 5000):
        print(f"     {w:8d}            " + "".join(f"{whip(s, w, d):7.2f}{'v' if vents(w, d) else ' '}" for d in (6, 10, 15, 25, 40)))
    print("     (v: the bubble breaks the surface, one expansion and no collapse)")

print("\n  Off the keel line (Destroyer, 300 kg at 10 m): design moments by lateral offset")
s = SHIPS[1]
print("     offset m:   " + "".join(f"{o:>7d}" for o in (0, 5, 10, 20, 40, 80)))
print("                 " + "".join(f"{whip(s, 300, 10, o):7.2f}" for o in (0, 5, 10, 20, 40, 80)))
print("  ... and along her length (offset 0):")
print("     station:    " + "".join(f"{a:>7.2f}" for a in (0.5, 0.4, 0.3, 0.224, 0.15, 0.05)))
print("                 " + "".join(f"{whip(s, 300, 10, 0, a):7.2f}" for a in (0.5, 0.4, 0.3, 0.224, 0.15, 0.05)))

print("\n  The cases (moment from the model, design moments; capacity is the ship's ~2.5-4):")
CASES = [
    ("Cheonan 2010, 136 kg at 8 m, 5 m off; broke in two [SRC]", SHIPS[0], 136, 8, 5),
    ("Cheonan, the official 250 kg at 7 m, 3 m off", SHIPS[0], 250, 7, 3),
    ("Mk 48 (295 kg PBXN-103 ~ 450 kg TNT bubble [EST]) 10 m under a 2,700 t frigate; broke [SRC]",
     Ship("Frigate (Torrens/Perry)", 110, 12.5, 4.2, 2700, 4.5, 3.5), 450, 12, 0),
    ("Shubrick 1943, 250 kg GP (136 kg) 1.5-2 m under her bottom; survived, half her section gone [SRC]", SHIPS[1], 136, 5.5, 0),
    ("Uganda 1943, Fritz X 320 kg amatol just under the keel; 1,300 t in, not broken [SRC]",
     Ship("Uganda (Fiji)", 169, 18.9, 5.0, 8800, 30, 2.8), 300, 6.5, 0),
    ("A 2,000 lb bomb (~500 kg) bursting 5 m off a battleship, 6 m down (Ostfriesland's) [SRC case]",
     Ship("Ostfriesland", 167, 28.5, 8.2, 22800, 60, 2.5), 500, 6, 5 + 14),
    ("A Tallboy (2,400 kg Torpex ~ 3,400 kg TNT bubble) 18 m off Lützow, 5 m down (in 7-8 m of water) [SRC case]",
     Ship("Lützow (Deutschland)", 186, 21.6, 7.4, 15000, 35, 2.8), 3400, 5, 18 + 10),
]
print("   moment  gamma=R/Rmax  f2 Hz  bubble Hz")
for name, ship, w, d, off in CASES:
    gam_ = math.hypot(d - ship.T, off) / bubble_rmax(w, d)
    print(f"   {whip(ship, w, d, off):6.2f}{'v' if vents(w, d) else ' '}  {gam_:5.2f}       {omega(ship) / 2 / math.pi:.2f}   {1 / bubble_period(w, d):.2f}     {name}")
print("""  gamma = distance from the charge to her keel over the bubble's Rmax. Below about 1 the bubble meets her bottom and
  vents into her (Shubrick, Uganda: the bombs passed through her and burst against or just under the bottom) and its
  pulsation is spent locally: a local wreck, not a whip; the model doesn't apply there (DST: similitude holds above
  gamma 1.67). The ships that broke (Cheonan, the Mk 48 frigates) sat at gamma 0.8-1.1, the influence pistol's design
  point; those far off (Ostfriesland, Lützow) whipped under capacity and went by their holes.""")

# ------------------------------------------------------------------ 5b. the one-cycle law
section("5b. The one-cycle law: what to put in the code")


def g_hat(s_):
    """|integral over one cycle of v(tau) e^{i s tau} d tau| for the RP bubble's normalised volume v = V/Vmax, tau = t/T."""
    ts, vs, _ = bubble_history(100, 10, cycles=1)
    tt = ts[-1]
    vmax = max(vs)
    re = im = 0.0
    for i in range(len(ts) - 1):
        dtau = (ts[i + 1] - ts[i]) / tt
        tau = ts[i] / tt
        re += vs[i] / vmax * math.cos(s_ * tau) * dtau
        im += vs[i] / vmax * math.sin(s_ * tau) * dtau
    return math.hypot(re, im)


print("  g(s) = |int_0^1 v(tau) exp(i s tau) dtau|, s = omega2 * T_bubble = 2 pi f2 T:")
print("     f2*T:  " + "".join(f"{x:7.2f}" for x in (0.0, 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0)))
print("     g:     " + "".join(f"{g_hat(2 * math.pi * x):7.3f}" for x in (0.0, 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0)))
print("     g*s:   " + "".join(f"{g_hat(2 * math.pi * x) * 2 * math.pi * x:7.3f}" for x in (0.0, 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0)))


def influence(ship, depth_m, off_m=0.0, at=0.5):
    """sum over her flat of bottom of phi(x) * (1/r - 1/r') dA, m: how much of a source's pressure lands on her mode."""
    xs = [i / (NX - 1) * ship.L for i in range(NX)]
    dx = ship.L / (NX - 1)
    xb = at * ship.L
    ys = [(-0.5 + (k + 0.5) / 5) * ship.B for k in range(5)]
    tot = 0.0
    for i, x in enumerate(xs):
        for y in ys:
            r1 = math.sqrt((x - xb) ** 2 + (y - off_m) ** 2 + (depth_m - ship.T) ** 2)
            r2 = math.sqrt((x - xb) ** 2 + (y - off_m) ** 2 + (depth_m + ship.T) ** 2)
            tot += PHI[i] * (1 / max(r1, 0.5) - 1 / r2) * dx * ship.B / 5
    return abs(tot)


def whip_law(ship, w_kg, depth_m, off_m=0.0, exact=True):
    """One cycle, closed form [INFERRED]. The bubble's pressure rho*V''/(4 pi r) lands on her two-node mode as
         F*(t) = rho * V''(t) * Infl / (4 pi),   Infl = sum phi (1/r - 1/r') dA   (~ phi_mid * B * 2 ln(h'/h) for h, h' << L/4)
    and over one cycle its Fourier part at her frequency is rho*omega^2*Vmax*T*g(omega T)*Infl/(4 pi) (V'' -> -omega^2 V by
    parts; V's minima are where the cycle starts and ends). So the mode's amplitude is that over (M* omega), M* her
    virtual mass, and the moment amidships EI * kappa_mid * amplitude. h: the charge's distance below her keel (offset
    included), h' = depth + draught (the surface's image). Valid for gamma > ~1."""
    om = omega(ship)
    tb = bubble_period(w_kg, depth_m)
    vmax = 4 / 3 * math.pi * bubble_rmax(w_kg, depth_m) ** 3
    if exact:
        infl = influence(ship, depth_m, off_m)
    else:
        h, hi = math.hypot(depth_m - ship.T, off_m), math.hypot(depth_m + ship.T, off_m)
        infl = abs(PHI[NX // 2]) * ship.B * 2 * math.log(hi / h)
    jstar = RHO_W * om * om * vmax * tb * g_hat(om * tb) * infl / (4 * math.pi)
    mstar = ship.virtual_t * 1000
    curv = abs(PHIDD[NX // 2]) * (BL / ship.L) ** 2
    return E_STEEL * ship.I * curv * jstar / (mstar * om) / ship.design_nm, jstar


print("\n  The law against the full model's first cycle (design moments; full model with one cycle and no damping):")
print("  Ship          W kg  depth m   law (Infl summed)   law (ln form)   full(1 cycle)")
for s_ in (SHIPS[1], SHIPS[3]):
    for w, d in ((100, 10), (300, 10), (300, 25), (1000, 15), (2400, 25), (5000, 40)):
        hist = bubble_history
        globals()["bubble_history"] = lambda w_, d_, cycles=1, **k: hist(w_, d_, cycles=1)
        full = whip(s_, w, d, zeta=0.0)
        globals()["bubble_history"] = hist
        law, _ = whip_law(s_, w, d)
        law2, _ = whip_law(s_, w, d, exact=False)
        print(f"  {s_.name:12s}{w:6d}  {d:5d}   {law:10.2f}       {law2:10.2f}     {full:10.2f}")
print("""  In the code's terms: HullGirder.Read takes the whip already in design moments, so EX5 can add this law's moment as
  it is (a bubble's), beside the gas's from HullGirder.Whip. The second cycle adds up to ~40% where the hull is tuned to
  it (section 5's full model); the law's first cycle is within -33%/+14% of the full model's first cycle, -60% at worst (5 t at 40 m).""")

# ------------------------------------------------------------------ 6. the gas whip
section("6. The gas whip of an internal burst: the same mode against the code's law")
print("""  An internal explosion in a closed box puts no net force on the hull: the gas pushes deck and bottom apart equally.
  Only what vents one way (the jet up through her decks, or down through her bottom into the water) gives her an
  impulse. Taken as a short kick J at amidships: q' = J*phi/M*, the moment EI*q'/omega*phi''. The code's
  HullGirder.Whip: J = sqrt(2*k*E*m), bending energy J^2/(2M) as M^2 L/(2EI) of a uniform moment.""")
for s in SHIPS[1:4]:
    om = omega(s)
    mstar = s.virtual_t * 1000
    mass = s.disp * 1000
    phi_mid = abs(PHI[NX // 2])
    curv = max(abs(v) for v in PHIDD[NX // 4: 3 * NX // 4]) * (BL / s.L) ** 2
    for e_mj, m_kg in ((0.5 * 13, 13), (0.5 * 41000, 41000)):       # a 13 kg burst's energy; Lion's Q's 41 t
        j = math.sqrt(2 * e_mj * 1e6 * m_kg)
        modal = E_STEEL * s.I * (j * phi_mid / mstar) / om * curv / s.design_nm
        code = math.sqrt(2 * 1.0 * e_mj * 1e6 * m_kg / mass * E_STEEL * s.I / s.L) / s.design_nm
        print(f"  {s.name:12s} E {e_mj:9.1f} MJ m {m_kg:7d} kg: modal {modal:7.3f}  the code's (k=1) {code:7.3f}  ratio {modal / code:.2f}")
print("  So the code's form has the mode's scaling (both go as J*sqrt(EI)/sqrt(M)); the ratio is the mode's shape and the")
print("  virtual mass, not a free factor; k 0.2 is then the share of the gas's momentum that leaves vertically in phase.")

# ------------------------------------------------------------------ 7. confined gas above 1 kg/m3
section("7. Confined gas above 1 kg/m3: the oxygen limit and the co-volume")
Q_D, Q_AB, O2_NEED, O2_AIR, H_O2 = 4.6e6, 10.3e6, 0.74, 0.276, 14.0e6   # TNT J/kg; afterburn J/kg; kg O2/kg; kg O2/m3; J/kg O2


def qsp_thermo(wv, gam_m1=0.28, covol=0.0):
    """(gamma-1) x (detonation + the afterburn the room's oxygen allows) / V; covolume m3/kg of products."""
    after = min(Q_AB * wv, H_O2 * O2_AIR)
    p = gam_m1 * (Q_D * wv + after)
    return p / max(1 - covol * wv, 0.05)


def qsp_weibull(wv):
    """Weibull 1968 as quoted in UFC 3-340-02's lineage: psi = 2410 (W/V lb/ft3)^0.72 [SRC via 11's sources]."""
    return 2410 * (wv / 16.018) ** 0.72 * 6894.76


print(f"  full afterburn possible up to W/V = O2 in air / O2 needed = {O2_AIR / O2_NEED:.2f} kg/m3 (TNT)")
print("    W/V kg/m3   thermo, no covol   thermo, covol 1 L/kg   Weibull fit    ratio thermo/Weibull   MPa")
for wv in (0.1, 0.37, 1, 1.6, 3, 10, 30, 100, 300):
    a, b, c = qsp_thermo(wv), qsp_thermo(wv, covol=0.001), qsp_weibull(wv)
    print(f"    {wv:8.2f}      {a / 1e6:8.2f}           {b / 1e6:8.2f}            {c / 1e6:8.2f}        {a / c:6.2f}")
print("""  Weibull's power law runs on the oxygen-limited line's chord: within ~25% from 0.3 to 10 kg/m3, then it falls below.
  The code's 0.28*eta*Q*W/V is the thermo line if eta carries the afterburn's share; above 0.37 kg/m3 the share that
  can afterburn is the room's oxygen, not a fixed eta. Above ~30 kg/m3 the co-volume matters (x1.03 at 30, x1.4 at 300).""")

# ------------------------------------------------------------------ 8. bombs falling
section("8. Bombs: fall speed by release height")


def cd_of_mach(m, base=0.18):
    """A streamlined bomb's drag on its section [EST]: flat subsonic, the transonic rise, past it slowly down."""
    if m < 0.75:
        return base
    if m < 1.05:
        return base + (0.45 - base) * ((m - 0.75) / 0.30) ** 2
    return 0.45 - 0.1 * min((m - 1.05) / 0.5, 1)


def fall(mass_kg, diam_m, h_m, v0=0.0, dive_deg=0.0, base=0.18, dt=0.01):
    """Impact speed and angle from a release at h_m, speed v0 m/s, diving dive_deg below horizontal."""
    a = math.pi * diam_m ** 2 / 4
    vx, vz = v0 * math.cos(math.radians(dive_deg)), -v0 * math.sin(math.radians(dive_deg))
    z, t = h_m, 0.0
    while z > 0:
        rho = 1.225 * math.exp(-z / 8500)
        tk = 288 - 0.0065 * min(z, 11000)
        c = math.sqrt(1.4 * 287 * tk)
        v = math.hypot(vx, vz)
        drag = 0.5 * rho * v * v * cd_of_mach(v / c, base) * a / mass_kg
        vx -= drag * vx / v * dt if v > 0 else 0
        vz += (-G - (drag * vz / v if v > 0 else 0)) * dt
        z += vz * dt
        t += dt
    v = math.hypot(vx, vz)
    return v, math.degrees(math.atan2(-vz, vx)), t


BOMBS = [
    # name, kg, diameter m, drag base, filler kg, source
    ("US 500 lb GP AN-M64", 238, 0.361, 0.20, 121, "[SRC ww2data/OP 1280]"),
    ("US 1,000 lb SAP AN-M59", 451, 0.384, 0.17, 145, "[SRC]"),
    ("US 1,600 lb AP AN-Mk 1", 726, 0.356, 0.15, 98, "[SRC filler; dia EST 14 in]"),
    ("Japanese No. 80 Mk 5 AP", 797, 0.45, 0.15, 23, "[SRC 22.8-30 kg; dia EST]"),
    ("German SC 250", 250, 0.368, 0.20, 130, "[SRC]"),
    ("German PC 1400 / Fritz X body", 1400, 0.559, 0.16, 300, "[SRC]"),
    ("Tallboy", 5400, 0.965, 0.14, 2360, "[SRC]"),
    ("Grand Slam", 10000, 1.17, 0.14, 4300, "[SRC]"),
]
print("  Level release at 75 m/s (Lancaster ~270 km/h), and a dive at 70 deg and 110 m/s released at 600 m:")
print("  Bomb                            W/D^3 kg/m3  filler %   from 3,000 m        from 5,500 m        from 6,700 m      dive, 600 m")
for name, mkg, d, base, fill, src in BOMBS:
    wd3 = mkg / d ** 3
    cells = []
    for h in (3000, 5500, 6700):
        v, ang, t = fall(mkg, d, h, 75, 0, base)
        cells.append(f"{v:4.0f} m/s {ang:3.0f} deg")
    vdv, adv, _ = fall(mkg, d, 600, 110, 70, base)
    print(f"  {name:31s}{wd3:9.0f}   {100 * fill / mkg:6.0f}    " + "   ".join(cells) + f"   {vdv:4.0f} m/s {adv:3.0f}   {src}")
print("  Records: Tallboy 336 m/s (750 mph) from 5,500 m [SRC]; Grand Slam ~320 m/s at the Ashley trial from 6,700 m [SRC].")
print("  The drag curve is [EST]; it is checked against those two only.")
