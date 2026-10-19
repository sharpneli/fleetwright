"""Contents in a shell's path: reference numbers for 09-contents-in-the-path.md.

Pure Python, no dependencies; runs in well under a second:  python3 contents_ref.py

Two kinds of content (the note's §1):
  * MEDIA  - liquids, coal, tube nests, charges: slow a shell as v·e^(-x/L) with L in calibres = C / rho.
  * SOLIDS - drums, rotors, crankshafts, guns, stowed shells, shafts: met with a mean free path
             lambda = chord / f inside a height band, then put through the plate model as one plate
             (thickness t ± spread, a material, an obliquity drawn for its shape).

Tags: [SRC] sourced, [INF] inferred / fitted here, [EST] engineering estimate, no source.
"""
import math
import random

# ---------------------------------------------------------------- media: decay lengths ------------------------------
RHO_SEA = 1025.0
SEA_TUMBLE_CAL = 25.0        # SimSettings.UnderwaterDecayCalibres: pointed shell, tumbling folded in (repo value)
SEA_DIVING_CAL = 50.0        # SimSettings.UnderwaterDecayCalibresDiving
# USNI 1921 (armor/05 §6): 6" shell, 3,000 -> 148 ft/s over 255 ft: a stable shell's e-fold in water [SRC]
SEA_STABLE_CAL = 255 * 0.3048 / math.log(3000 / 148) / 0.1524          # ≈ 169 calibres

C_LIQUID = SEA_TUMBLE_CAL * RHO_SEA          # ≈ 25,600 cal·kg/m³ : liquids, as the sea (cavity, tumbling)
C_DIVING = SEA_DIVING_CAL * RHO_SEA
C_GRANULAR = 120.0 * 800.0                   # ≈ 96,000 cal·kg/m³ : coal, tube nests, charges [INF] (fits "2 ft ≈ 1 in")


def decay_cal(rho, kind="liquid", diving=False):
    """e-fold length in calibres of the shell for a medium of effective density rho (kg/m³)."""
    if kind == "liquid":
        return (C_DIVING if diving else C_LIQUID) / rho
    return C_GRANULAR / rho


MEDIA = [  # name, effective density kg/m³ (range), kind, note
    ("sea water (outside, flooded cell)", 1025, (1020, 1030), "liquid", "the repo's sea"),
    ("fresh / feed / reserve feed water", 1000, (995, 1000), "liquid", "double bottom, feed tanks"),
    ("fuel oil (1910-25 RN/USN)", 950, (920, 990), "liquid", "Admiralty FFO 0.92-0.97; Bunker C to 0.99"),
    ("diesel / light oil", 850, (830, 870), "liquid", "submarines, motor craft"),
    ("coal, bunkered (bulk)", 800, (750, 900), "granular", "40-48 ft³/ton stowed"),
    ("water-tube bank, small tube, full", 1700, (1500, 1900), "granular", "Yarrow/Thornycroft 1-1.75 in tubes"),
    ("water-tube elements, large tube, full", 1100, (900, 1300), "granular", "Belleville/Niclausse 4-5 in tubes"),
    ("cylindrical boiler interior (water+fire tubes)", 1250, (1100, 1450), "granular", "Scotch boiler below the steam space"),
    ("condenser tube nest (brass tubes, water)", 1100, (950, 1300), "granular", "3/4 in tubes on ~1 in pitch"),
    ("turbine blading zone", 2000, (1500, 2500), "granular", "between rotor drum and casing"),
    ("cordite charges in magazine (bulk)", 550, (400, 750), "granular", "bags in cases or racks"),
]

# ---------------------------------------------------------------- solids: geometry helpers --------------------------


def mean_chord_box(a, b, c):
    """Cauchy: mean chord of a convex body = 4V/S."""
    return 4 * a * b * c / (2 * (a * b + b * c + a * c))


def mean_chord_cyl(D, L):
    V = math.pi * D * D / 4 * L
    S = 2 * math.pi * D * D / 4 + math.pi * D * L
    return 4 * V / S


def tube_mean_steel(OD, ID):
    """Mean steel crossed by a line crossing a thick tube perpendicular to its axis (uniform impact parameter)."""
    return math.pi / 4 * (OD * OD - ID * ID) / OD


def lam(chord, f):
    """Mean free path to the next solid: chord / f (f the share of the band's volume the solids fill)."""
    return chord / f


def p_meet(L, lam_):
    return 1 - math.exp(-L / lam_)


def draw_obliquity(rng, shape):
    """Obliquity of the struck surface for a random line: cylinder sinθ~U, sphere sin²θ~U, flat: none (0)."""
    u = rng.random()
    if shape == "cyl":
        return math.degrees(math.asin(u))
    if shape == "sphere":
        return math.degrees(math.asin(math.sqrt(u)))
    return 0.0


# ---------------------------------------------------------------- thin plate check (DeMarre) ------------------------
K_MILD = 1800.0  # [INF] DeMarre constant for mild steel plate at normal, d,t in dm, m in kg, v in m/s


def demarre_vl(d_mm, m_kg, t_mm, K=K_MILD):
    return K * (d_mm / 100) ** 0.75 * (t_mm / 100) ** 0.7 / math.sqrt(m_kg)


def demarre_t_for_vl(d_mm, m_kg, vl, K=K_MILD):
    return 100 * (vl * math.sqrt(m_kg) / (K * (d_mm / 100) ** 0.75)) ** (1 / 0.7)


SHELLS = [("4in", 102, 14), ("6in", 152, 45), ("8in", 203, 116), ("12in", 305, 390), ("13.5in", 343, 567),
          ("15in", 381, 871)]


def coal_equivalence(v=500.0, coal_m=0.61):
    rows = []
    for name, d, m in SHELLS:
        L = decay_cal(800, "granular") * d / 1000
        vr = v * math.exp(-coal_m / L)
        vl = math.sqrt(max(v * v - vr * vr, 0))
        t = demarre_t_for_vl(d, m, vl)
        rows.append((name, L, 1 - vr / v, t / 25.4))
    return rows


# ---------------------------------------------------------------- shafts -----------------------------------------------


def shaft_od_mm(p_kw_per_shaft, rpm, k=120.0):
    """[EST] cube-root rule: OD ≈ k·(P/N)^(1/3) mm, P kW per shaft; k≈100 merchant solid, 115-130 warship hollow."""
    return k * (p_kw_per_shaft / rpm) ** (1 / 3)


SHAFT_CASES = [  # name, total power kW (from shp/ihp ×0.7457), shafts, rpm at full power
    ("pre-dreadnought VTE (Majestic-type)", 12000 * 0.7457, 2, 100),
    ("armoured cruiser VTE (1905)", 23000 * 0.7457, 2, 125),
    ("Dreadnought, direct turbines", 23000 * 0.7457, 4, 320),
    ("battlecruiser, direct turbines (Lion)", 70000 * 0.7457, 4, 275),
    ("light cruiser, direct turbines (1914)", 30000 * 0.7457, 4, 450),
    ("destroyer VTE (River, 1904)", 7000 * 0.7457, 2, 380),
    ("destroyer direct turbines (M class)", 25000 * 0.7457, 3, 650),
    ("Hood, geared turbines", 144000 * 0.7457, 4, 210),
]

# ---------------------------------------------------------------- examples -------------------------------------------


def run():
    rng = random.Random(1)
    print(f"stable water e-fold (USNI 1921): {SEA_STABLE_CAL:.0f} cal;  liquid C = {C_LIQUID:.0f}, granular C = {C_GRANULAR:.0f}")
    print("\nMEDIA: decay length in calibres (pointed shell)")
    for name, rho, (lo, hi), kind, note in MEDIA:
        print(f"  {name:48s} rho {rho:5.0f} ({lo}-{hi})  L = {decay_cal(rho, kind):5.1f} cal"
              f"  [{decay_cal(hi, kind):.0f}-{decay_cal(lo, kind):.0f}]")

    print("\nCOAL: 2 ft of coal at 500 m/s, as mild steel by DeMarre")
    for name, L, loss, t_in in coal_equivalence():
        print(f"  {name:7s} L {L:5.1f} m  speed lost {loss*100:4.1f}%  ≈ {t_in:4.2f} in of steel")

    print("\nSPLINTERS in coal: a 20 mm, 30 g splinter tumbling (L ≈ 15 of its sizes in coal): stops in "
          f"{3 * 15 * 0.020:.2f} m (3 e-folds)")

    print("\nMEAN CHORDS")
    print(f"  Scotch boiler 4.5 m dia × 3.2 m: {mean_chord_cyl(4.5, 3.2):.2f} m")
    print(f"  small-tube boiler 3.0×3.5×3.0 m box: {mean_chord_box(3.0, 3.5, 3.0):.2f} m;  destroyer 2.4×3.0×2.6: "
          f"{mean_chord_box(2.4, 3.0, 2.6):.2f} m")
    print(f"  LP turbine 2.4 m dia × 4 m: {mean_chord_cyl(2.4, 4.0):.2f} m;  destroyer turbine 1.2 × 2.5: "
          f"{mean_chord_cyl(1.2, 2.5):.2f} m")
    for name, od, idd in [("12in gun at the slide (OD 2.4 cal)", 2.4 * 305, 305),
                          ("12in gun at the chase (OD 1.6 cal)", 1.6 * 305, 305),
                          ("hollow shaft 400 mm, bore 0.45", 400, 180),
                          ("solid crankpin 450 mm", 450, 0),
                          ("15in AP shell, cavity 0.3 cal", 381, 0.3 * 381),
                          ("15in HE/common, cavity 0.55 cal", 381, 0.55 * 381)]:
        print(f"  {name:36s} mean steel chord {tube_mean_steel(od, idd):5.0f} mm")

    print("\nOBLIQUITY draws (10^5)")
    for shape in ("cyl", "sphere"):
        xs = [draw_obliquity(rng, shape) for _ in range(100000)]
        print(f"  {shape:6s} mean {sum(xs)/len(xs):4.1f}°,  ≥65° (the repo's glance) {sum(x >= 65 for x in xs)/len(xs)*100:4.1f}%")

    print("\nMEET CHANCE for a full crossing, by room (band × (1 - e^(-L/λ)))")
    cases = [  # name, crossing m, band share, chord, f
        ("DD engine room, direct turbines, beam 8.5 m", 8.5, 0.55, 1.1, 0.45),
        ("DD boiler room, small tube, beam 8.0 m", 8.0, 0.80, 1.6, 0.55),
        ("BB engine room, VTE, 1 of 2 wings, 9 m", 9.0, 0.90, 1.6, 0.35),
        ("BB boiler room, Scotch, 20 m", 20.0, 0.85, 2.6, 0.55),
        ("DD engine room, plunging 4.5 m (Johnston)", 4.5 * 0.55, 1.0, 1.1, 0.45),
    ]
    for name, L, band, c, f in cases:
        lm = lam(c, f)
        print(f"  {name:46s} λ {lm:4.1f} m  P {band * p_meet(L, lm):.2f}")

    print("\nSHAFTS: OD by the cube-root rule (k 115-130, hollow, bore ≈ 0.45 OD)")
    for name, kw, n, rpm in SHAFT_CASES:
        lo, hi = shaft_od_mm(kw / n, rpm, 115), shaft_od_mm(kw / n, rpm, 130)
        print(f"  {name:40s} {kw/n:6.0f} kW/shaft @ {rpm:3d}: OD {lo:3.0f}-{hi:3.0f} mm ({lo/25.4:4.1f}-{hi/25.4:4.1f} in)")

    print("\nSPEED LEFT after media (v·e^(-x/L))")
    for name, d, rho, kind, x in [("6in through a Scotch boiler, 3.4 m of interior", 152, 1250, "g", 3.4),
                                  ("6in through two small-tube banks, 1.2 m", 152, 1700, "g", 1.2),
                                  ("15in through two small-tube banks, 1.2 m", 381, 1700, "g", 1.2),
                                  ("12in through 3 m of coal", 305, 800, "g", 3.0),
                                  ("12in through 3 m of fuel oil", 305, 950, "liquid", 3.0),
                                  ("15in through a 4 m liquid TDS layer", 381, 1000, "liquid", 4.0)]:
        L = decay_cal(rho, "liquid" if kind == "liquid" else "granular") * d / 1000
        print(f"  {name:48s} L {L:5.1f} m  speed kept {math.exp(-x / L)*100:4.0f}%")


if __name__ == "__main__":
    run()
