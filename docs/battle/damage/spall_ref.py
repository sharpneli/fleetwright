"""spall_ref.py -- reference for 14_spall_behind_armour.md (Fleetwright damage research).

What comes off the back of a plate that held: back-face spall, the scab, and the half-pushed plug, and what that
cloud does to the people and parts behind it. Plan step E2 (shells-armour-damage.md).

Pure Python, no dependencies. `python spall_ref.py` prints every table quoted in the note.
Units: m, kg, m/s, degrees, J. Tags in comments: [SRC] sourced, [FIT] fitted here to the cases, [EST] judgement.

The model in one paragraph:
  u = V / V_L (the plate's Navy limit for this shell at this obliquity, Thompson's V_L as the plan's P1a has it).
  Below u_s nothing comes off. Between u_s and u_H (the holing limit) the back face throws a cloud whose size grows
  with s = (u - u_s) / (u_H - u_s). Above u_H the plate is holed and E1/E3 take over. A burst on the face of a plate
  it can't hole (contact burst) throws a scab by the same machinery with its own s.
  The cloud: a disc of the back face, diameter d_s, depth t_s, of which a share leaves as one big piece (the plug or
  scab) and the rest as flakes ~t_s across. It flies in a cone of half-angle ~45 deg about an axis halfway between
  the plate's normal and the shot line, fastest on the axis (v = v_ax * cos(1.92 theta)), densest near 0.4 of the
  edge angle. Drag doesn't matter inside a ship; the first bulkhead does.
"""
import math

RHO_STEEL = 7850.0

# ---------------------------------------------------------------------------------------------------------
# 1. The plate's limit (Thompson, as P1a will have it; STS basis, q the material's thickness multiplier)
def thompson_vl(d_m, w_kg, t_m, ob_deg, q=1.0):
    """Navy ballistic limit (m/s) of plate t_m (STS-equivalent q*t) against a shell of calibre d_m, mass w_kg."""
    d_in, t_in, w_lb = d_m / 0.0254, q * t_m / 0.0254, w_kg / 0.45359
    ob = min(ob_deg, 75.0)
    f = 6.0 * (t_in / d_in - 0.45) * (ob ** 2 + 2000.0) + 40000.0
    vl_fps = 0.024056 * d_in * math.sqrt(t_in / w_lb) * f / math.cos(math.radians(ob))   # [SRC plan P1a, armor_pen_ref]
    if ob_deg > 75:
        vl_fps *= math.cos(math.radians(75)) / math.cos(math.radians(ob_deg))
    return vl_fps * 0.3048


# ---------------------------------------------------------------------------------------------------------
# 2. When the back face lets go.  Plate classes (the material block's `fragments` field already names three).
#   u_s  spall onset as a share of the Navy limit V_L                               [EST, see the note's section 2]
#   u_H  the holing limit as a share of V_L: above it the plate is holed (E1/E4)     [SRC armor/00: FH 2-12% below]
PLATE_CLASS = {
    #              u_s    u_H   big_share  note
    "ductile":   (0.90, 1.00, 0.5,  "STS, Class B, NCA, Wh: bulges and tears, throws little (O.S. 751 criterion)"),
    "fh":        (0.85, 0.94, 0.5,  "KC, Class A, Harvey: brittle face cracks, soft back plugs"),
    "brittle":   (0.75, 0.95, 0.3,  "compound, cast, high-hardness, poor lots: back flakes freely"),
    "mild":      (0.90, 1.00, 0.5,  "structural mild/HT steel: dishes, petals"),
}

T_S_SHARE = 0.20     # scab depth / plate thickness: HESH scabs 20-30 mm of 140-150, 1.5 in of 150 mm [SRC tankarchives,
                     # tankhistoria]
FLAKE_ASPECT = 1.5   # flakes ~ t_s x 1.5t_s x 1.5t_s [EST]
THETA_MAX = 45.0     # edge of the cloud, deg: TNO 30-50 for KE perforation; cos(1.92 th) -> 0 at 47 [SRC TNO PML 1997-A65]
PEAK_SHARE = 0.4     # number density peaks at ~0.4 theta_max [SRC TNO]
KV_KE = 0.25         # axial speed / normal striking speed at s=1 for a stopped shell [EST, bracketed below]
V_BIG_SHARE = 0.6    # the plug or scab, heavier, leaves slower than the flakes [EST]


def spall_severity(u, plate):
    u_s, u_h, _, _ = PLATE_CLASS[plate]
    if u < u_s:
        return 0.0
    if u >= u_h:
        return None   # holed: not E2's
    return (u - u_s) / (u_h - u_s)


def cloud_ke(d_m, t_m, v_ms, ob_deg, s, plate):
    """Spall cloud from a stopped shell. Returns dict or None."""
    if not s:
        return None
    big_share = PLATE_CLASS[plate][2] if s >= 0.5 else 0.0
    d_s = d_m + t_m                                   # conical plug footprint: D at the face, D+T at the back [EST,
                                                      # Okun: 'broken plug 17 in wide' off an 8 in plate]
    t_s = T_S_SHARE * t_m
    mass = s * RHO_STEEL * math.pi / 4 * d_s ** 2 * t_s
    v_n = v_ms * math.cos(math.radians(ob_deg))
    v_ax = KV_KE * v_n * (0.4 + 0.6 * s)
    return _pieces(mass, big_share, t_s, v_ax, ob_deg)


# Contact burst on the face (HE, or AP whose fuze fires in the plate) that doesn't hole it.
#   t_h: the thickness the burst just holes. Okun: 0.156 cal of STS for a contact detonation [SRC navweaps Misc];
#   scab up to SCAB_TOP t_h.
SCAB_TOP = 1.5      # scab window: t_h .. 1.5 t_h (Okun: 'dented and cracked' to 1.2 t_h) [EST]


def contact_th(d_m, kind):
    return (0.156 if kind == "he" else 0.10) * d_m      # AP's small filler: 0.10 cal [EST]


def r_hole(w_kg, t_m):
    """The game's contact-hole radius (08 3.6, kept by 11 3.1): 0.45 W^(1/3) sqrt(6/t_mm)."""
    return 0.45 * w_kg ** (1 / 3) * math.sqrt(6.0 / (t_m * 1000))


INBOARD_SHARE = 0.5   # share of a punched hole's plate that goes inboard; the rest petals or goes out [EST]
V_PUNCH = 150.0       # m/s, plate debris from a contact burst that holes it: HESH-perforation-like [EST]


def cloud_punched(w_kg, t_m, ob_deg):
    """A contact burst that holes the plate from outside (the hole's own plate blown in: Tiger's Q roof)."""
    r = r_hole(w_kg, t_m)
    mass = INBOARD_SHARE * RHO_STEEL * math.pi * r ** 2 * t_m
    return _pieces(mass, 0.0, 0.5 * t_m, V_PUNCH, ob_deg)


def cloud_contact(d_m, t_m, ob_deg, kind):
    t_h = contact_th(d_m, kind)
    if t_m <= t_h or t_m >= SCAB_TOP * t_h:
        return None
    s = (SCAB_TOP * t_h - t_m) / ((SCAB_TOP - 1) * t_h)
    d_s = max(2.0 * t_m, d_m)                         # HESH scabs 1.3-2.4 T across [SRC]
    t_s = T_S_SHARE * t_m
    mass = s * RHO_STEEL * math.pi / 4 * d_s ** 2 * t_s
    v_ax = 40.0 + 60.0 * s                            # HESH 36-75 m/s [SRC tankarchives]; naval casings weaker [EST]
    return _pieces(mass, 0.5 if s >= 0.5 else 0.0, t_s, v_ax, ob_deg)


def _pieces(mass, big_share, t_s, v_ax, ob_deg):
    m_big = big_share * mass
    m_flake = RHO_STEEL * t_s * (FLAKE_ASPECT * t_s) ** 2
    n = (mass - m_big) / m_flake
    return {"mass": mass, "m_big": m_big, "m_flake": m_flake, "n": n, "v_ax": v_ax,
            "v_big": V_BIG_SHARE * v_ax,
            "axis_deg": ob_deg / 2.0,                 # halfway between normal and shot line [SRC TNO; HESH 30 deg at 60]
            "t_s": t_s}


# ---------------------------------------------------------------------------------------------------------
# 3. Where the pieces go: angular distribution and speed by angle.
def _density(th):
    """Relative number per steradian at angle th (deg) from the axis: rises to a peak at 0.4 theta_max, gone at the
    edge. A smooth stand-in for TNO's log-normal fit [FIT]."""
    x = th / THETA_MAX
    if x >= 1:
        return 0.0
    p = PEAK_SHARE
    return (0.35 + x / p if x < p else 1.35 * (1 - x) / (1 - p))


def _norm():
    n, k = 0.0, 400
    for i in range(k):
        th = (i + 0.5) / k * THETA_MAX
        n += _density(th) * 2 * math.pi * math.sin(math.radians(th)) * math.radians(THETA_MAX / k)
    return n


NORM = _norm()


def speed_at(c, th):
    return max(0.0, c["v_ax"] * math.cos(math.radians(1.92 * th)))   # Verolme [SRC TNO]


# ---------------------------------------------------------------------------------------------------------
# 4. What it does.
KE_HAZ = 79.0        # J: AASTP-1 / DDESB hazardous fragment [SRC]
A_PERSON = 0.56      # m^2: AASTP body area [SRC]


def p_kill_given_hit(ke):
    """Share of hits that kill or incapacitate: 79 J -> 3%, 300 J -> 37% (Swiss blunt model, weighted) [SRC];
    a heavy chunk of kJ almost always incapacitates [EST]."""
    if ke < KE_HAZ:
        return 0.0
    if ke < 300:
        return 0.03 + 0.34 * (ke - 79) / 221
    return min(0.95, 0.37 + 0.58 * math.log10(ke / 300) / 1.0)


def hits_on(c, r, area, th):
    """Expected flake hits on a target of presented area at distance r and angle th off the axis."""
    return c["n"] * _density(th) / NORM * area / r ** 2


def casualty_p(c, r, th, area=A_PERSON):
    """Probability a person at (r, th) is a casualty (flakes plus the big piece's share)."""
    ke = 0.5 * c["m_flake"] * speed_at(c, th) ** 2
    lam = hits_on(c, r, area, th) * p_kill_given_hit(ke)
    # the big piece: one lump, near the axis (within 15 deg), kills what it hits
    if c["m_big"] > 0 and th < 15:
        lam += area / (2 * math.pi * (1 - math.cos(math.radians(15))) * r ** 2)
    return 1 - math.exp(-lam)


def reach(c, p=0.5, th=None):
    """Distance (m) out to which a person in the densest direction is a casualty with probability >= p."""
    th = PEAK_SHARE * THETA_MAX if th is None else th
    lo, hi = 0.2, 60.0
    if casualty_p(c, lo, th) < p:
        return 0.0
    for _ in range(60):
        mid = (lo + hi) / 2
        lo, hi = (mid, hi) if casualty_p(c, mid, th) >= p else (lo, mid)
    return lo


def casualties_in_cone(c, r_wall, crew_per_m2):
    """Expected casualties in a space whose far boundary is r_wall away along the cloud, crew spread evenly on the floor
    area the cone sweeps (a disc of radius r*tan(th) at each r, 1.8 m high: people stand in a slab) [EST geometry]."""
    tot, k = 0.0, 60
    for i in range(k):
        r = (i + 0.5) / k * r_wall
        dr = r_wall / k
        for j in range(18):
            th = (j + 0.5) / 18 * THETA_MAX
            dth = math.radians(THETA_MAX / 18)
            # floor area element of the cone's footprint: people are a slab, so count 2*pi*r*sin(th)*r*dth*dr/h
            vol = 2 * math.pi * (r * math.sin(math.radians(th))) * r * dth * dr
            people = crew_per_m2 * vol / 1.8
            tot += people * casualty_p(c, r, th)
    return tot


# THOR, mild homogeneous steel, limit velocity (fps, in, in^2, grains) [SRC armor/01 3.10]. Spall flakes are heavier
# and slower than THOR's data (5-825 grains, >600 fps): an extrapolation, for order of magnitude only.
def thor_mild_defeats_mm(m_kg, v_ms, area_m2):
    m_gr = m_kg / 6.47989e-5
    a_in2 = area_m2 / 0.0254 ** 2
    v_fps = v_ms / 0.3048
    # V0 = 10^6.523 (e A)^0.906 m^-0.963  ->  e = (V0 / (10^6.523 m^-0.963))^(1/0.906) / A
    ea = (v_fps / (10 ** 6.523 * m_gr ** -0.963)) ** (1 / 0.906)
    return ea / a_in2 * 25.4


# ---------------------------------------------------------------------------------------------------------
# 5. Cases.
SHELLS = {   # calibre m, mass kg
    "6in 130lb":   (0.152, 59.0),
    "8in 335lb":   (0.203, 152.0),
    "11in Ger":    (0.283, 302.0),
    "12in 850lb":  (0.305, 386.0),
    "14in 1500lb": (0.356, 680.0),
    "15in Greenboy": (0.381, 871.0),
    "16in Mk8":    (0.406, 1225.0),
}


def show_cloud(label, c):
    if c is None:
        print(f"  {label:44s} -- nothing behind")
        return
    ke_f = 0.5 * c["m_flake"] * c["v_ax"] ** 2
    ke_b = 0.5 * c["m_big"] * c["v_big"] ** 2
    print(f"  {label:44s} mass {c['mass']:6.1f} kg  big {c['m_big']:5.1f} kg ({ke_b/1000:5.1f} kJ)  flakes {c['n']:5.0f} x "
          f"{c['m_flake']*1000:6.0f} g  v_ax {c['v_ax']:4.0f} m/s  KE/flake {ke_f/1000:5.2f} kJ  "
          f"r50 {reach(c, 0.5):4.1f} m  r10 {reach(c, 0.1):4.1f} m")


# Iowa-type belt vs 16in Mk 8: where on the range scale the belt is in the spall band.
RANGE_TABLE = [(10000, 2074, 5.7), (15000, 1893, 9.8), (20000, 1740, 14.9), (25000, 1632, 21.1),
               (30000, 1567, 28.25), (35000, 1556, 36.27)]   # yd, fps, fall deg (navweaps 16in/50, armor_pen_ref P)


def spall_band_by_range(t_m=0.307, incline=19.0, q=0.92, plate="fh"):
    out = []
    for (y0, v0, f0), (y1, v1, f1) in zip(RANGE_TABLE, RANGE_TABLE[1:]):
        for k in range(5):
            y = y0 + k * (y1 - y0) / 5
            v = (v0 + (v1 - v0) * k / 5) * 0.3048
            fall = f0 + (f1 - f0) * k / 5
            vl = thompson_vl(0.406, 1225, t_m, fall + incline, q)
            out.append((y * 0.9144 / 1000, v / vl))
    return out


if __name__ == "__main__":
    print("=== 1. Onset and holing bands (u = V / V_L) ===")
    for k, (us, uh, bs, note) in PLATE_CLASS.items():
        print(f"  {k:8s} spall from u={us:.2f}, holed from u={uh:.2f}, big piece {bs:.0%}  ({note})")

    print("\n=== 2. The cloud by severity: 15in Greenboy (871 kg) on 330 mm KC at 20 deg ===")
    d, w = SHELLS["15in Greenboy"]
    vl = thompson_vl(d, w, 0.330, 20, q=0.9)
    print(f"  Thompson V_L (q 0.9) = {vl:.0f} m/s")
    for u in (0.84, 0.87, 0.90, 0.93):
        s = spall_severity(u, "fh")
        show_cloud(f"u={u:.2f} s={s if s is None else round(s,2)}", cloud_ke(d, 0.330, u * vl, 20, s, "fh"))

    print("\n=== 3. Near-limit (s = 0.75) clouds across shells and plates ===")
    for name, t, ob, plate in [("6in 130lb", 0.076, 10, "ductile"), ("8in 335lb", 0.127, 20, "fh"),
                               ("12in 850lb", 0.229, 10, "fh"), ("14in 1500lb", 0.152, 65, "ductile"),
                               ("16in Mk8", 0.152, 70, "ductile"), ("16in Mk8", 0.305, 15, "fh"),
                               ("15in Greenboy", 0.150, 70, "ductile")]:
        d, w = SHELLS[name]
        vl = thompson_vl(d, w, t, ob)
        u_s, u_h = PLATE_CLASS[plate][:2]
        c = cloud_ke(d, t, vl * (u_s + 0.75 * (u_h - u_s)), ob, 0.75, plate)
        show_cloud(f"{name} on {t*1000:.0f} mm {plate} at {ob} deg (V_L {vl:.0f})", c)

    print("\n=== 4. Contact bursts on a plate they can't hole ===")
    for name, t, kind in [("11in Ger", 0.064, "he"), ("11in Ger", 0.083, "he"), ("11in Ger", 0.035, "ap"),
                          ("11in Ger", 0.064, "ap"), ("6in 130lb", 0.025, "he"), ("6in 130lb", 0.040, "he"),
                          ("8in 335lb", 0.051, "he"), ("14in 1500lb", 0.076, "he")]:
        d, _ = SHELLS[name]
        show_cloud(f"{name} {kind} on {t*1000:.0f} mm (t_h {contact_th(d, kind)*1000:.0f} mm)",
                   cloud_contact(d, t, 0, kind))

    print("\n=== 4b. Contact bursts that hole the plate from outside: the hole's plate blown inboard ===")
    print("  (only at openings, joints and edges: an intact face >= 0.2 cal isn't holed by a contact burst)")
    for label, w, t in [("11in APC (~8 kg filler) on 51 mm roof (Tiger Q, at the hood)", 8.0, 0.051),
                        ("11in APC on 83 mm roof at a port", 8.0, 0.083),
                        ("14in HC (45 kg) on 76 mm roof at a hood", 45.0, 0.076)]:
        print(f"    hole radius {r_hole(w, t):.2f} m")
        show_cloud(label, cloud_punched(w, t, 0))

    print("\n=== 5. Casualties in a gunhouse: 1 crew per 2 m^2, far wall at 4 m ===")
    for label, c in [("Tiger Q: 11in APC burst holes a 51 mm roof (punched)", cloud_punched(8.0, 0.051, 0)),
                     ("14in HE contact on 76 mm roof (scab only)", cloud_contact(0.356, 0.076, 0, "he")),
                     ("15in on 330 mm KC face, u=0.90", cloud_ke(0.381, 0.330, 0.90 * thompson_vl(0.381, 871, 0.33, 20, 0.9),
                                                             20, spall_severity(0.90, "fh"), "fh")),
                     ("16in on 150 mm STS roof at 70, u=0.97", cloud_ke(0.406, 0.150, 0.97 * thompson_vl(0.406, 1225, 0.15, 70),
                                                                     70, spall_severity(0.97, "ductile"), "ductile"))]:
        if c:
            print(f"  {label:58s} {casualties_in_cone(c, 4.0, 0.5):4.1f} casualties")

    print("\n=== 6. What a flake defeats (THOR mild steel, extrapolated) ===")
    for m, v, a in [(0.05, 60, 4e-4), (0.42, 100, 1.8e-3), (2.0, 120, 5e-3), (5.0, 110, 1e-2)]:
        print(f"  {m*1000:6.0f} g at {v:3d} m/s, {a*1e4:4.0f} cm^2: defeats ~{thor_mild_defeats_mm(m, v, a):4.1f} mm mild steel")

    print("\n=== 7. Hazard by distance on the densest ray (s = 0.75, 15in on 330 mm KC) ===")
    d, w = SHELLS["15in Greenboy"]
    vl = thompson_vl(d, w, 0.33, 20, 0.9)
    c = cloud_ke(d, 0.33, 0.9125 * vl, 20, 0.75, "fh")
    for r in (1, 2, 3, 4, 5, 7, 10):
        print(f"  r {r:2d} m: P(casualty) {casualty_p(c, r, 18):.2f} on 18 deg, {casualty_p(c, r, 40):.2f} on 40 deg, "
              f"{casualty_p(c, r, 5):.2f} on 5 deg")

    print("\n=== 8. Iowa-type belt (307 mm Class A, 19 deg incline, q 0.92) vs 16in Mk 8, beam-on: u by range ===")
    for km, u in spall_band_by_range():
        s_ = spall_severity(u, "fh")
        tag = "holed/perforated" if s_ is None else ("nothing behind" if s_ == 0 else f"spall s={s_:.2f}")
        print(f"  {km:5.1f} km  u={u:.2f}  {tag}")
