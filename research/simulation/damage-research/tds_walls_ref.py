"""tds_walls_ref.py - torpedo protection walls: what they held and how far they bent (research note 14-torpedo-protection-walls.md).

Pure Python, under a second. Every derived number in the note comes out of this file.
  1. the walls' steels: yield, tensile, elongation by grade and navy, and a flow stress
  2. a wall as a membrane: p = sigma_f * t * sqrt(24 eps) / a, by system, against the code's TdsMembraneK
  3. the load the walls actually took: the static pressure that leaves each measured permanent set
  4. bend at tearing: the record's sets as w/a and as strain, leak -> seams open -> torn
  5. charge-held laws: the Japanese formula (USNTMJ S-01-9) and the amateur depth regression, against the trials
  6. how long a hit is and what it floods: Job 74's destroyed length against W^1/3, North Carolina's cells, the tonnes per hit
  7. liquid against the holding bulkhead: the water-hammer pulse a moving bulkhead puts through a full tank
Tags as in the note: [SRC] sourced, [INFERRED] reasoning or a fit, [EST] an estimate.
"""
import math
import statistics as st

KSI = 6.895          # MPa per ksi
LB_IN = 0.0245       # inches of steel per lb/ft2 (40.8 lb/ft2 per inch)
FT = 0.3048
RUPTURE_MPA_MM = 0.06    # the code's plain boundary, joints that fail (SimSettings.RuptureMPaMm)
TDS_K = 2.2              # the code's TdsMembraneK


def section(title):
    print()
    print("=" * 100)
    print(title)
    print("=" * 100)


def lb_mm(lb):
    """US/RN plate weight in lb/ft2 to mm."""
    return lb * LB_IN * 25.4


# ------------------------------------------------------------------ 1. steels
# yield, tensile in ksi, elongation % [SRC Okun, "Metallurgy of armour and construction steels", NavWeaps]; Ducol from
# Wikipedia (63-71 kg/mm2 UTS, 42 kg/mm2 elastic limit, 20-23 % on 200 mm).
STEELS = {
    # name: (yield ksi, tensile ksi, elong %, where used as a TDS wall)
    "MS (mild/medium, all navies)": (40, 60, 20, "US Standard-type and older holding bulkheads (Nevada, BB55's per Robertson)"),
    "HTS (high-tensile)": (47, 78, 22, "US TDS layer walls (BB57's two outer 25 lb walls)"),
    "STS (special treatment)": (80, 117, 25, "US holding bulkheads from BB55 abreast turrets, BB57, BB61"),
    "German WWI nickel-steel TB": (50, 78, 21, "German torpedo bulkheads 1908-18 (30-50 mm)"),
    "RN D / D.1 (Si-Mn HT)": (55, 89, 22, "RN protective bulkheads 1920s-40s (Nelson, KGV); Job 74"),
    "Ducol (as rolled, Japan)": (42 * 9.81 / KSI, 67 * 9.81 / KSI, 21, "IJN DS: Nagato, Yamato bulkheads; Pugliese's ER similar"),
    "German Ww (Wotan weich)": (68, 104, 22, "KM torpedo bulkheads 1930s-40s (Bismarck 45 mm)"),
    "German St 52": (51, 74, 18, "KM construction steel, layer walls"),
}


def flow(y, u):
    """A flow stress for a membrane stretched a few percent: the mean of yield and tensile [INFERRED]."""
    return 0.5 * (y + u)


section("1. The walls' steels [SRC Okun; Ducol]: MPa")
print(f"{'grade':34s} {'yield':>6s} {'UTS':>6s} {'flow':>6s} {'elong':>6s}   used as")
for k, (y, u, e, use) in STEELS.items():
    print(f"{k:34s} {y*KSI:6.0f} {u*KSI:6.0f} {flow(y,u)*KSI:6.0f} {e:5.0f}%   {use}")
print("The code's EST: sigma 280 MPa (below every grade's yield), eps 0.15, a 4 m.")


# ------------------------------------------------------------------ 2. membrane
def membrane_mpa(sig_mpa, t_mm, eps, a_m):
    """A clamped strip pulled into a parabola: strain eps = (8/3)(w/a)^2, so w = a sqrt(3 eps/8), and p = 8 T w / a^2."""
    return sig_mpa * (t_mm / 1000) * math.sqrt(24 * eps) / a_m


def eps_from_wa(wa):
    return 8.0 / 3.0 * wa * wa


def wa_from_eps(eps):
    return math.sqrt(3 * eps / 8)


section("2. A wall as a membrane: p = sigma_f t sqrt(24 eps)/a, MPa a mm and as the code's K (x 0.06)")
print("code's EST:", f"{membrane_mpa(280, 1, 0.15, 4):.3f} MPa/mm, K {membrane_mpa(280, 1, 0.15, 4)/RUPTURE_MPA_MM:.2f}"
      f"  (w at tearing {4*wa_from_eps(0.15):.2f} m, w/a {wa_from_eps(0.15):.2f})")
# eps at tearing: riveted seams open at w/a ~0.10 (Nevada, NC); continuous welded plate held tight to w/a 0.075 (Caisson H)
# and torn plate came at ~1.5 m (Saratoga TB1, 5 ft): eps 0.06 a fair welded/continuous value, 0.15 the plate's own limit
EPS = {"riveted seams (w/a 0.10)": eps_from_wa(0.10), "continuous plate (w/a 0.15)": eps_from_wa(0.15),
       "plate's own (eps 0.15)": 0.15}
SYSTEMS = [
    # name, steel, t mm, span a m (lo, hi), source of span
    ("German WWI TB (Kaiser/Koenig)", "German WWI nickel-steel TB", 40, (4.5, 6.0), "DB to slope, EST"),
    ("Nelson (2 x 0.75 in D)", "RN D / D.1 (Si-Mn HT)", 38, (6.0, 8.0), "EST"),
    ("Job 74 #4 (2 x 35 lb D.1)", "RN D / D.1 (Si-Mn HT)", 2 * lb_mm(35), (7.5, 9.0), ">= 28 ft rupture depth in #5, SRC/INFERRED"),
    ("KGV / Job 74 #5 (60 lb D.1)", "RN D / D.1 (Si-Mn HT)", lb_mm(60), (7.5, 9.0), "as above"),
    ("Nevada (1.5 in MS)", "MS (mild/medium, all navies)", 38, (5.0, 6.5), "EST"),
    ("BB55 abreast turret I (2 in STS)", "STS (special treatment)", 51, (3.5, 5.0), "split by the first platform, EST"),
    ("BB57 (25 lb STS)", "STS (special treatment)", lb_mm(25), (6.5, 8.0), "third deck to triple bottom, EST"),
    ("Bismarck (45 mm Ww)", "German Ww (Wotan weich)", 45, (6.0, 8.0), "EST"),
    ("Littorio (40 mm ER, as D)", "RN D / D.1 (Si-Mn HT)", 40, (5.0, 7.0), "EST"),
]
print(f"{'system':34s} {'t mm':>5s} {'a m':>9s} " + " ".join(f"{k[:22]:>24s}" for k in EPS))
for name, steel, t, (alo, ahi), _ in SYSTEMS:
    y, u, _, _ = STEELS[steel]
    sf = flow(y, u) * KSI
    cells = []
    for eps in EPS.values():
        plo = membrane_mpa(sf, t, eps, ahi)
        phi = membrane_mpa(sf, t, eps, alo)
        cells.append(f"{plo:4.1f}-{phi:4.1f} MPa K{plo/(RUPTURE_MPA_MM*t):3.1f}-{phi/(RUPTURE_MPA_MM*t):3.1f}")
    print(f"{name:34s} {t:5.1f} {alo:4.1f}-{ahi:3.1f} " + " ".join(f"{c:>24s}" for c in cells))


# ------------------------------------------------------------------ 3. equivalent static loads from the sets
def p_eq(sig_y_mpa, t_mm, w_m, a_m):
    """The static pressure that leaves a strip of span a set by w (rigid-plastic, tension at yield): 8 sigma t w / a^2."""
    return 8 * sig_y_mpa * (t_mm / 1000) * w_m / a_m ** 2


SETS = [
    # case, steel, t mm, w m, a m (mid), outcome, charge kg TNT, system depth m [SRC as in the note]
    ("Job 74 #4 1936, protective bhd", "RN D / D.1 (Si-Mn HT)", 2 * lb_mm(35), 18 * 0.0254, 8.2, "held, rivet weeps", 454, 3.79),
    ("Caisson H 1939 (BB57), x2 scale", "STS (special treatment)", lb_mm(25), 22 * 0.0254, 7.2, "held tight 2 h", 318, 5.46),
    ("Caisson F (BB55), x2 scale", "MS (mild/medium, all navies)", 19, 6 * 0.0254, 7.2, "held", 318, 5.64),
    ("California 1941, holding bhd", "MS (mild/medium, all navies)", 19, 1 * 0.0254, 6.0, "held", 200, 5.2),
    ("California 1941, TB 4 (0.75 in)", "MS (mild/medium, all navies)", 19, 24 * 0.0254, 6.0, "intact", 200, 4.0),
    ("Nevada 1941, holding bhd 1.5 in", "MS (mild/medium, all navies)", 38, 24 * 0.0254, 5.8, "seams open, heavy leak", 200, 4.3),
    ("Saratoga 1942, TB 4", "MS (mild/medium, all navies)", 16, 12 * 0.0254, 6.0, "tight bar bottom rivets", 227, 4.5),
    ("Saratoga 1942, TB 3", "MS (mild/medium, all navies)", 16, 18 * 0.0254, 6.0, "bottom pulled loose", 227, 3.5),
    ("Saratoga 1942, TB 2", "MS (mild/medium, all navies)", 16, 36 * 0.0254, 6.0, "bottom pulled loose", 227, 2.5),
    ("Saratoga 1942, TB 1", "MS (mild/medium, all navies)", 16, 60 * 0.0254, 6.0, "torn loose, holed", 227, 1.5),
    ("Tirpitz 1944, 45 mm TB (16 ft dish)", "German Ww (Wotan weich)", 45, 6 * 0.0254, 16 * FT, "riveted seams weep", 109, 2.0),
]
section("3. The load the walls took: static pressure that leaves each set, 8 sigma_y t w/a^2 [INFERRED; sets SRC, t/a partly EST]")
print(f"{'case':38s} {'t':>5s} {'w m':>5s} {'a':>4s} {'w/a':>5s} {'eps %':>6s} {'p_eq MPa':>9s}  outcome")
for case, steel, t, w, a, out, W, D in SETS:
    sy = STEELS[steel][0] * KSI
    print(f"{case:38s} {t:5.1f} {w:5.2f} {a:4.1f} {w/a:5.3f} {100*eps_from_wa(w/a):6.2f} {p_eq(sy, t, w, a):9.2f}  {out}")
print("(t for California/Saratoga/Caisson F is an EST where the record gives none; a is the wall's height between its supports, EST)")


# ------------------------------------------------------------------ 4. bend at tearing
section("4. Bend at tearing: the record's thresholds [SRC sets; INFERRED as strain]")
TH = [
    ("riveted connections weep", 0.03, "Tirpitz 6 in over 16 ft; Job 74 #4 rivets at wide pitch"),
    ("held tight, with margin", 0.06, "Job 74 #4 18 in; Caisson H 22 in (11 in at half scale)"),
    ("riveted seams open (fails as a boundary)", 0.10, "Nevada ~2 ft; BB55 1942 seams open 6-12 in"),
    ("edge connections pulled loose", 0.10, "Saratoga TB2/TB3 at 18-36 in"),
    ("plate torn and holed", 0.25, "Saratoga TB1 at ~5 ft; Job 74 #5 at welded stiffeners, earlier"),
]
for what, wa, ev in TH:
    print(f"{what:42s} w/a {wa:4.2f}  eps {100*eps_from_wa(wa):5.2f} %  w at a=6 m {6*wa:4.2f} m, a=8 m {8*wa:4.2f} m   ({ev})")


# ------------------------------------------------------------------ 5. charge-held laws
def jp_charge_kg(T_mm, D_mm):
    """Japanese formula for void (non-liquid) systems, 200-400 kg: T = 95.62 C^0.469 D^-1/3, inverted [SRC USNTMJ S-01-9 via Robertson]."""
    return (T_mm * D_mm ** (1 / 3) / 95.62) ** (1 / 0.469)


def amateur_lb(depth_ft):
    return 37.6 * depth_ft - 132


section("5. Charge-held laws against the trials")
print("Japanese check (Robertson): C 317.5 kg at D 5,486 mm needs T =", f"{95.62*317.5**0.469*5486**(-1/3):.0f} mm (he gives 81)")
CASES5 = [
    # name, T total plating mm (EST from the layers), D mm, charge kg, outcome
    ("Job 74 #4 (shell 16 + sandwich 2x9 + 44)", 16 + 18 + 2 * lb_mm(35), 3790, 454, "held"),
    ("Job 74 #5 (shell 16 + 18 + 37)", 16 + 18 + lb_mm(60), 3790, 454, "torn where welded"),
    ("Yamato (armour counted: off the law)", 16 + 150, 5100, 400, "Skate 1943 failed at the joint"),
    ("Tosa 1924 frame 192 (void, ~16+25+25 EST)", 66, 3500, 300, "breached, 1,203 lt"),
    ("Kronprinz 1916 (16 + coal-bunker 10 + 40 TB)", 66, 4000, 150, "held, 250 t"),
]
for name, T, D, C, out in CASES5:
    print(f"{name:44s} T {T:5.0f} mm D {D/1000:4.2f} m -> holds {jp_charge_kg(T, D):6.0f} kg  vs {C} kg: {out}")
for name, ft, lb in [("Hood 7.5-10 ft", 10, 450), ("Nelson 12 ft", 12, 750), ("KGV 13 ft", 13, 1000),
                     ("NC 18.5 ft", 18.5, 700), ("Yamato 16.7 ft", 16.7, 880), ("Littorio ~23.7 ft", 23.7, 772)]:
    print(f"amateur rating {name:18s} {amateur_lb(ft):5.0f} lb   (rated {lb} lb)")


# ------------------------------------------------------------------ 6. length and tonnes
section("6. How long a hit is, and what it floods")
k_destroy = 36 * FT / 454 ** (1 / 3)
print(f"Job 74 #4/#5: layers destroyed over 36 ft at 454 kg contact -> {k_destroy:.2f} W^1/3 m [SRC length; INFERRED scaling]")
print(f"  partly flooded 15 ft beyond each end -> {(36+30)*FT/454**(1/3):.2f} W^1/3 m flooded length")
for W in (150, 200, 300, 454):
    print(f"  {W:4d} kg: destroyed {k_destroy*W**(1/3):5.1f} m, flooded ~{(36+30)*FT/454**(1/3)*W**(1/3):5.1f} m;"
          f" the code's near field 2 x 0.7 W^1/3 = {1.4*W**(1/3):5.1f} m")
# North Carolina: 970 t added, 597 t liquid already in the damaged area [SRC WDR 61]; ~15 voids + 14 tanks + 2 magazines
# + trunks, storerooms, 2 living spaces flooded immediately; one TDS layer ~1.1 m x ~8 m section abreast turret I [EST]
cells = 15 + 14
total = 970 + 597
print(f"BB55 1942: {cells} TDS cells + 2 magazines etc. for {total} t of liquid in the flooded spaces;"
      f" if TDS ~{0.75*total:.0f} t: {0.75*total/cells:.0f} t a cell [INFERRED]")
for wl, hl in ((1.1, 8.0), (1.4, 6.0)):
    vol = 0.75 * total / cells / 1.0
    print(f"  a cell of {vol:.0f} m3 at {wl} m x {hl} m section is {vol/(wl*hl):.1f} m long ({vol/(wl*hl)/1.22:.1f} frames of 4 ft)")
print(f"Tosa 1924 test 1: 995 lt in 23 compartments = {995*1.016/23:.0f} t a compartment [SRC]")
# a section-long, full-height TDS cell as the code builds it, one side, KGV-like (depth 4 m x height 9 m, permeability 0.95)
for depth, h in ((4.0, 9.0), (5.5, 9.0)):
    per_m = depth * h * 0.95 * 1.025
    print(f"code-like TDS, {depth} m deep x {h} m high: {per_m:.0f} t a metre one side -> one 17 m section {17*per_m:,.0f} t, two {34*per_m:,.0f} t")

HITS = [  # ship, year, charge kg (TNT eq, EST where no source), class, tonnes, notes [SRC as in the note]
    ("Kronprinz", 1916, 150, "held", 250), ("Westfalen", 1916, 150, "held", 800),
    ("Ostfriesland (mine)", 1916, 150, "held, then tore on turning", 500), ("Grosser Kurfuerst (mine)", 1917, 150, "held?", 280),
    ("Moltke E42 (cond. flooding)", 1918, 150, "held (interior via condensers)", 1800),
    ("Seydlitz (mine, fwd)", 1916, 150, "breached", 1270),
    ("Tosa test 1", 1924, 100, "breached", 1011), ("Tosa test 2", 1924, 200, "breached", 1024),
    ("Tosa test 3", 1924, 300, "breached", 1222), ("Tosa test 4", 1924, 350, "breached", 1179),
    ("Tosa test 5", 1924, 150, "breached", 738),
    ("Saratoga", 1942, 227, "piping at HB; 3 firerooms", 1100), ("North Carolina", 1942, 300, "seams open", 970),
    ("Littorio (bomb)", 1943, 320, "held (TDS only)", 1066), ("Vittorio Veneto (Urge)", 1941, 340, "breached", 2700),
    ("Yamato (Skate)", 1943, 300, "joint failed", 3000), ("Monarch trial (2,084 lb at 8 ft)", 1923, 945, "held; condenser leak", 2743),
]
print()
print(f"{'case':34s} {'year':>4s} {'kg':>4s} {'t':>6s}  outcome")
for s, y, w, c, t in HITS:
    print(f"{s:34s} {y:4d} {w:4d} {t:6,d}  {c}")
groups = {"held": [], "leaked or partly": [], "breached": []}
for s, y, w, c, t in HITS:
    if "Monarch" in s or "Moltke" in s:
        continue
    g = "breached" if ("breached" in c or "joint" in c) else ("leaked or partly" if ("seams" in c or "piping" in c or "tore" in c) else "held")
    groups[g].append(t)
for g, v in groups.items():
    print(f"  {g:18s} n {len(v):2d}  median {st.median(v):6,.0f} t  range {min(v):,}-{max(v):,} t")


# ------------------------------------------------------------------ 7. liquid against the holding bulkhead
section("7. Liquid against the holding bulkhead: a moving wall drives a pulse through a full tank [INFERRED physics, EST speeds]")
for liq, rho, c in (("sea water", 1025, 1500), ("fuel oil", 920, 1350)):
    for v in (5, 10, 20):
        print(f"  {liq:9s} wall at {v:2d} m/s: water-hammer pulse rho c v = {rho*c*v/1e6:5.1f} MPa,"
              f" over 2d/c = {2*1.5/c*1000:.1f} ms for a 1.5 m tank")
w, tdyn = 18 * 0.0254, (0.02, 0.04)
print(f"Job 74 #4's 18 in set over {tdyn[0]*1000:.0f}-{tdyn[1]*1000:.0f} ms [EST]: mean speed {w/tdyn[1]:.0f}-{w/tdyn[0]:.0f} m/s")
back = [membrane_mpa(flow(*STEELS["HTS (high-tensile)"][:2]) * KSI, lb_mm(25), eps_from_wa(0.10), a) for a in (7.0, 5.0)]
print("A void behind the wall takes no pulse (air's rho c ~ 400 Pa s/m: 0.004 MPa at 10 m/s); a 25 lb HTS wall behind a full")
print(f"tank opens its seams at {back[0]:.1f}-{back[1]:.1f} MPa static (a 5-7 m), so a 6-30 MPa pulse of 2 ms (impulse {6e6*0.002/1e3:.0f}-{30e6*0.002/1e3:.0f} kPa s)")
print("against its ~120 kg/m2 (an impulse of that size moves it at 100-500 m/s if nothing else held it) breaks it:")
print("Saratoga's boiler-room bulkhead, Job 74's inner tank wall [INFERRED].")
