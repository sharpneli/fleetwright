"""
armor_pen_ref.py -- reference inputs + checks for the armour-penetration model
(Naval project, research/simulation/armor/00-overview.md).

This is NOT the game model. It is a test bench:
  * a catalogue of the historical penetration formulas, coded exactly as published
    (01-physics-formulas, 06-early-era),
  * the material / shell / fuze parameter tables proposed in 00-overview §4-§6,
  * a candidate per-plate resolver and plate-sequence stepper (00-overview §8),
  * checks of all of the above against the calibration data in 07_*.csv.

Run:  python3 armor_pen_ref.py [path/to/folder/with/07_csvs]
Pure python, no dependencies. Every [CALC §x] number in 00-overview comes from section x of the printed output.

Units: the historical formulas are in inches / lb / ft/s and are kept that way inside
(constants would otherwise be unrecognisable). Public helpers take SI and convert.
[INFERRED] constants are tuning values; the overview says what each was fitted to.
"""

import csv
import math
import os
import random
import statistics as st
import sys
from dataclasses import dataclass, field, replace

IN, LB, FPS = 0.0254, 0.45359237, 0.3048
rad, deg = math.radians, math.degrees

# =============================================================================
# 1. Formula catalogue (T, D in inches; W in lb; V in ft/s; Ob in degrees)
#    Sources: Okun "Historical penetration formulae" (navweaps Hstfrmla.php),
#    EB1911 "Armour Plates", USN Naval Ordnance 1937, navweaps tech-109.
# =============================================================================
def fairbairn(W, D, V):            # wrought iron, thin plate, c.1865
    return D * 0.0007692 * ((W / D**3) * V**2) ** 0.5


def tresidder(W, D, V):            # wrought iron, RN standard 1870s-1900s (EB1911 log A = 8.8410)
    return D * 0.00003798 * (W / D**3) ** 0.5 * V**1.5


def krupp_wi(W, D, V):             # wrought iron, Krupp early 1870s
    return D * 0.00004643 * ((W / D**3) * V**2) ** 0.75


def demarre_wi(W, D, V):           # wrought iron, De Marre late 1870s
    return D * 0.00002778 * D**0.1542 * ((W / D**3) * V**2) ** 0.7695


def gavre(W, D, V):                # wrought iron, Gavre Commission (USN acceptance basis 1894)
    return D * 0.00002887 * D**0.42857 * ((W / D**3) * V**2) ** 0.71429


def demarre(W, D, V, ob=0.0, C=1.0):   # nickel steel 1890 (C=1); STS ~1.2-1.25; cos^3 inside
    c3 = math.cos(rad(ob)) ** 3
    return D * 0.00005021 * D**0.07144 * ((W / D**3) * (V / C) ** 2 * c3) ** 0.71429


def krupp_kc_uncapped(W, D, V):    # KC vs uncapped AP, c.1895 (EB1911 log A1 = 6.3532)
    return D * 0.0006659 * ((W / D**3) * V**2) ** 0.5


def krupp_allpurpose(W, D, V, C=660.0):  # 1930s, normal impact; C 525 mild steel, 655-694 Wh, <=804 KC n/A
    return D * 0.30386 * D**0.25 * ((W / D**3) * (V / C) ** 2) ** 0.625


def davis_harvey_uncapped(W, D, V):
    return D * 0.00003466 * D**0.3333 * ((W / D**3) * V**2) ** 0.6667


def davis_harvey_capped(W, D, V):
    return D * 0.00008582 * D**0.25 * ((W / D**3) * V**2) ** 0.625


def garzke_dulin(W, D, V):         # 0 deg, muzzle-ish only (tech-109) -- do NOT cos-scale
    return 0.0004689 * W**0.55 * D**-0.65 * V**1.1


def F_std(td, ob):                 # Thompson "standard F" (USN 1930-31, 8in Mk11 vs Class B/STS, 0-75 deg)
    return 6.0 * (td - 0.45) * (ob**2 + 2000.0) + 40000.0


def thompson_vl(T, D, W, ob, q=1.0):
    """USN Navy Ballistic Limit (ft/s) of a plate T (in) -- q = '% of NBL' plate grade (1.0 = standard)."""
    return 0.024056 * D * math.sqrt(T / W) * F_std(T / D, ob) * q / math.cos(rad(ob))


def thompson_T(V, D, W, ob, q=1.0):
    """Thickness (in) a shell just defeats at V (inverse of thompson_vl by bisection)."""
    lo, hi = 1e-4, 6.0 * D
    if thompson_vl(hi, D, W, ob, q) < V:
        return hi
    for _ in range(60):
        mid = 0.5 * (lo + hi)
        if thompson_vl(mid, D, W, ob, q) < V:
            lo = mid
        else:
            hi = mid
    return 0.5 * (lo + hi)


def okun_fh_shape(W, D, V, C):     # FH law shape T = C W^0.2 V^1.21 (tech-033); C must carry calibre
    return C * W**0.2 * V**1.21


def spaced(plates_in):
    """Okun spaced-plate equivalence (Miscarmr): (sum T^1.4)^(1/1.4)."""
    return sum(t**1.4 for t in plates_in) ** (1 / 1.4)


def okun_fuze_min(D, ob, tfz=0.07):  # Okun base-fuze start threshold (in); Ob frozen at 61
    r = rad(min(ob, 61.0))
    return tfz * D * ((1 + math.cos(2 * r)) / 2 + 0.4537 * math.sin(r) ** 5.7019)


# SI convenience wrappers --------------------------------------------------------
def pen_thompson_mm(m_kg, cal_m, v_ms, ob_deg, q=1.0):
    return thompson_T(v_ms / FPS, cal_m / IN, m_kg / LB, ob_deg, q) * 25.4


def vl_thompson_ms(t_mm, m_kg, cal_m, ob_deg, q=1.0):
    return thompson_vl(t_mm / 25.4, cal_m / IN, m_kg / LB, ob_deg, q) * FPS


# =============================================================================
# 2. Proposed parameter tables  (00-overview §4-§6; sources in 03/02/06/04)
# =============================================================================
@dataclass
class Material:
    id: str
    name: str
    rho: float          # kg/m3
    fh: bool            # face-hardened (shatter / cap logic applies)
    Q: float            # thickness multiplier vs WWII US Class A/STS, vs a good CAPPED shell
    years: str
    face: float = 0.0   # face fraction (FH only)
    t_min_in: float = 0.0   # thinnest plate made as this type
    elong: float = 25.0     # % (drives large-calibre scale penalty, homogeneous)
    scale_k: float = 0.0    # FH scale exponent beyond 8in calibre (thick-face plate loses vs big shells)
    note: str = ""
    shatter_vs: str = "steel"   # which shot the face shatters: "steel" (any uncapped) | "chilled" (Palliser only)


# Q is a THICKNESS multiplier in the engine basis: Thompson F_STD (standard US Class B/STS as
# fitted c.1930-31 to the 260-lb 8in Mk 11 Mod 1 AP; the same standard graded WWII plates against
# WWII hard-capped shells at NPG 1946-47 -- section B).  Derived in 00-overview §4.2 from
#   (a) early trials re-run through Thompson (section L below),
#   (b) Okun's homogeneous "backing quality" ladder (WI 0.6, MS 0.7, HT/Ni 0.8, Ducol 0.9, STS 1.0),
#   (c) NPG 1946-47 grades and Okun FACEHARD tables vs Thompson (sections B, D).
# FH plates: Q8 = value vs an 8in shell; Q(D) = Q8 * (D/8)^-scale_k for D > 8in.
# Historical "figure of merit" (inches of wrought iron, Tresidder basis) ~= 1.78 x Q (section L).
MATERIALS = {m.id: m for m in [
    Material("WI",   "Wrought iron (+timber backing)", 7700, False, 0.53, "1859-1880", note="Spezia 1876 ~0.50 if 590 fps spare is right; Tresidder/Thompson 0.56; Okun 0.6"),
    Material("LAMWI","Laminated / sandwich wrought iron", 7700, False, 0.43, "1860-1880", note="~0.8 of solid (Appleton)"),
    Material("MS",   "Mild shipbuilding steel", 7850, False, 0.70, "1880-1945", note="Okun backing ladder 0.7"),
    Material("STL",  "All-steel armour (Schneider)", 7850, False, 0.78, "1876-1890", note="Annapolis: held at Q>=0.76; cracks"),
    Material("CMP",  "Compound (steel-faced iron)", 7750, True, 0.72, "1877-1891", face=0.30, note="Annapolis <0.75; shatters Palliser only", shatter_vs="chilled"),
    Material("NIS",  "Nickel steel (homogeneous)", 7850, False, 0.85, "1889-1896", note="Annapolis >0.77-0.83; Johnson limit 0.92"),
    Material("HT",   "High-tensile structural (UK HT, Krupp low-Ni)", 7850, False, 0.80, "1900-1925", note="Okun ladder 0.8"),
    Material("HRV",  "Harvey (Harveyized Ni-steel)", 7850, True, 0.82, "1891-1899", face=0.08, t_min_in=3.2,
             note="NO1937 0.79; x1.4 shatter bonus vs uncapped gives ~1.15 (trials 1.0-1.18)"),
    Material("KNC",  "Krupp-type homogeneous Ni-Cr (KNC/Krupp soft)", 7850, False, 0.92, "1894-1925", elong=22),
    Material("KCA",  "Krupp cemented a/A (and licensees, CKC)", 7850, True, 0.88, "1894-1918", face=0.34, t_min_in=3.2,
             scale_k=0.05, note="capped trials 0.87-0.97; Okun 0.83 of WWII FH"),
    Material("BKC",  "British/Japanese(VC)/Italian WWI KC", 7850, True, 0.85, "1900-1932", face=0.30, t_min_in=4.0),
    Material("WKC",  "Witkowitz KC (A-H)", 7850, True, 0.97, "1905-1918", face=0.34, t_min_in=3.2),
    Material("MNC",  "Midvale non-cemented (US)", 7850, True, 1.15, "1906-1910", face=0.80, t_min_in=4.0, scale_k=0.45,
             note="1.3x CKC vs 12in soft-capped, ~Harvey vs 16in"),
    Material("CB1",  "US Class B / STS, WWI", 7850, False, 0.95, "1910-1932", elong=23),
    Material("DS",   "Ducol / D-steel", 7850, False, 0.90, "1925-1945", note="Okun ladder 0.9"),
    Material("STS",  "US STS / WWII Class B", 7850, False, 1.00, "1930-1945", elong=25),
    Material("NCA",  "British Non-Cemented Armour", 7850, False, 1.00, "1926-1945", elong=25),
    Material("WH",   "Wotan Hart", 7850, False, 1.00, "1925-1945", elong=18),
    Material("WW",   "Wotan Weich", 7850, False, 0.95, "1925-1945", elong=22),
    Material("PO",   "Italian homogeneous (PO)", 7850, False, 1.00, "1929-1943", elong=17),
    Material("NVNC", "Japanese New Vickers Non-Cemented", 7850, False, 0.97, "1926-1945", elong=23, note="Okun M79 input Q 0.97"),
    Material("CNC",  "Japanese Copper Non-Cemented", 7850, False, 0.97, "1931-1945", elong=23),
    Material("USA",  "US Class A, WWII (thick chill)", 7850, True, 1.03, "1933-1945", face=0.55, t_min_in=5.0, scale_k=0.17,
             note="FACEHARD NL vs Thompson 1.03 at 8in, 0.92 at 16in (section D); OS751 trend agrees (section C)"),
    Material("CA",   "British Cemented Armour", 7850, True, 0.99, "1933-1945", face=0.30, t_min_in=4.0, scale_k=-0.05,
             note="slightly better vs bigger shells (section D, NL)"),
    Material("KCNA", "Krupp KC n/A", 7850, True, 1.01, "1928-1945", face=0.41, t_min_in=3.9, scale_k=0.03,
             note="NPG 1946-47 grades 83-113% of standard"),
    Material("TKC",  "Italian Terni WWII cemented", 7850, True, 1.03, "1929-1943", face=0.45, t_min_in=4.0, scale_k=-0.08,
             note="FACEHARD NL: best of the five at 15-18in; little test data"),
    Material("FRFH", "French 1930s face-hardened", 7850, True, 0.95, "1932-1945", face=0.35, t_min_in=4.0, scale_k=0.05,
             note="Okun assumed = US; German tests found inferior"),
    Material("VH",   "Japanese Vickers Hardened", 7850, True, 0.89, "1937-1945", face=0.35, t_min_in=11.0, scale_k=0.0,
             note="FACEHARD NL 0.89-0.90 at 8-18in; Shinano 26in plate 0.84 of US Class A (Okun)"),
]}

# Shell-side tables (00-overview §5) -------------------------------------------------
CAP_DECAP = {   # T/D needed to strip the cap: (normal impact, 15-45 deg, >45 deg)  tech-085
    "soft":  (0.105, 0.0805, 0.0805),
    "hard1": (0.105, 0.0805, 0.0805),
    "hard2": (0.17, 0.17, 0.14),
}
SHATTER_BONUS_T = 1.40   # FH thickness bonus vs a shattering shell at 0 deg -> 1.0 at 55 deg (Okun +30% V ~ +40% T;
                         # early trials 1.2-1.7, thin plates high -- section L)

SHELL_QUALITY = {        # multiplier on penetration capability (thickness) vs WWII standard capped AP
    "palliser":    0.85,  # chilled iron vs WI/compound; shatters on steel faces
    "steel_uncapped_1890": 0.95,
    "softcap_1900": 0.95,
    "rn_1916": 0.85,      # plus break-up at >=15-20 deg on >=0.4 cal FH
    "km_1916": 0.95,
    "greenboy": 0.97,
    "ijn_t91": 0.95,
    "ww2": 1.00,
    "us_late": 1.08,
}


# =============================================================================
# 3. Geometry helpers
# =============================================================================
def compound_obliquity(fall, target_angle=90.0, incline=0.0, heel=0.0):
    """Obliquity (deg) on a side plate. target_angle 90 = beam-on; incline >0 = top leaning
    outboard (Iowa-style, adds to fall); heel >0 rolls the plate toward the shooter."""
    f, a, i = rad(fall), rad(target_angle), rad(incline + heel)
    u = (math.cos(f) * math.sin(a), math.cos(f) * math.cos(a), -math.sin(f))   # (across, along, up)
    n = (math.cos(i), 0.0, math.sin(i))        # plate normal; top leaning toward the shooter adds to fall
    return deg(math.acos(min(1.0, abs(sum(x * y for x, y in zip(u, n))))))


def okun_deflection(ob, vs_over_vl):
    """Okun M79 turn toward the normal on perforation (deg), Ob <= 45 form; scaled above 45."""
    vr = max(vs_over_vl, 1.0 + 1e-9)
    k1 = vr * vr + vr * math.sqrt(vr * vr - 1)
    o = min(ob, 45.0)
    k2 = math.sin(rad(o)) * math.cos(rad(o)) / k1
    k3 = 1 - 4 * k2 * k2
    df = deg(math.atan((1 - math.sqrt(max(k3, 0))) / (2 * k2))) if k2 > 1e-12 else 0.0
    if ob > 45:
        df *= 1 + (ob - 45) / 45
    return df


def energy_nbl_factor(ob):
    """Okun M79: residual velocity above 45 deg uses Energy NBL = E4 x true NBL."""
    if ob <= 45:
        return 1.0
    e1 = 1 + (ob - 45) / 45
    e3 = e1 / (2 * math.sin(rad(ob)) * math.cos(rad(ob)))
    return math.sqrt(2 * e3 - 1) / e3


# =============================================================================
# 4. Candidate per-plate resolver  (00-overview §8 -- a suggestion, not a decision)
# =============================================================================
@dataclass
class ShellState:
    name: str
    D: float               # in
    W: float               # lb (current, cap included while capped)
    V: float               # ft/s
    cap: str = "hard1"     # none | soft | hard1 | hard2
    cap_frac: float = 0.12
    quality: str = "ww2"
    integrity: str = "intact"   # intact | nose_damaged | broken
    fuze_started_at: float = None   # path length (ft) at fuze start
    path_ft: float = 0.0
    delay_s: float = 0.033
    fuze_tfz: float = 0.07
    cap_virtual: bool = False   # cap stripped, but the gap to the next plate was too short to matter
    last_df: float = 0.0        # deflection toward the normal at the last perforation (deg)


@dataclass
class Plate:
    t_in: float
    mat: str
    ob: float              # obliquity of this plate along the current path (deg)
    gap_after_ft: float = 0.0
    label: str = ""


def plate_effective_T(p: Plate, s: ShellState):
    m = MATERIALS[p.mat]
    q = m.Q
    if m.fh and m.scale_k:                        # FH scaling with calibre beyond 8in
        q *= (max(s.D, 8.0) / 8.0) ** (-m.scale_k)
    if not m.fh and m.elong < 25 and s.D > 8:     # Okun M79 elongation penalty (applied to thickness)
        q *= 1 - (1 - math.sqrt(m.elong / 25)) * min((s.D - 8) / 8, 1.0)
    if m.fh and p.t_in < max(m.t_min_in, 0.3 * s.D):   # thin FH: back too thin, no FH advantage
        q *= 0.9
    te = q * p.t_in
    # shatter bonus: FH plate vs uncapped / decapped / soft cap above 20 deg / RN 1916 at >=15 deg
    uncapped = s.cap == "none" and not s.cap_virtual
    shatters = m.fh and p.t_in / s.D >= 0.25 and (
        uncapped or (s.cap == "soft" and p.ob > 20)
        or (s.quality == "rn_1916" and p.ob >= 15 and p.t_in / s.D >= 0.4))
    if m.shatter_vs == "chilled" and s.quality != "palliser":
        shatters = False
    if shatters and p.ob < 55:
        te *= 1 + (SHATTER_BONUS_T - 1) * (1 - p.ob / 55)
    return te, shatters


def decap_gap_needed_cal(ob, cap):
    """Calibres of gap needed behind a decapping plate for the bare nose to reach the next plate bare (tech-085)."""
    if cap == "soft":
        return 0.5
    dgn = 2.3                                   # 1.6-3 cal at normal (depends on decap plate thickness)
    return max(1.0, dgn - (dgn - 1.0) * min(ob, 30.0) / 30.0)


def resolve_plate(s: ShellState, p: Plate, rnd=None, q_noise=0.0):
    """Mutates s. Returns outcome code. Engine: Thompson F_STD on Q-scaled thickness."""
    # fuze start first: an impact decelerates the shell even if it then glances or stops
    tfz = okun_fuze_min(s.D, p.ob, s.fuze_tfz)
    if s.fuze_started_at is None and (p.t_in >= tfz or MATERIALS[p.mat].fh):
        s.fuze_started_at = s.path_ft
    if p.ob >= 80:
        return "RICOCHET"
    te, shatters = plate_effective_T(p, s)
    te *= (1 + q_noise) / SHELL_QUALITY[s.quality]   # shell quality = thickness the shell can handle
    vl = thompson_vl(max(te, 1e-3), s.D, s.W, min(p.ob, 75.0))
    if p.ob > 75:                          # beyond the F_STD fit, extend with 1/cos only
        vl *= math.cos(rad(75)) / math.cos(rad(p.ob))
    ratio = s.V / vl
    # ricochet band 65-80 deg: need ratio >= 1 + 0.08 (Ob - 65)   [INFERRED, 05 step 1]
    if p.ob >= 65 and ratio < 1 + 0.08 * (p.ob - 65) and s.integrity == "intact":
        return "RICOCHET"
    s.cap_virtual = False
    # decap (cap mass lost; if the gap behind is short, the cap still protects the nose on the next plate)
    if s.cap != "none":
        n, mid, hi = CAP_DECAP.get(s.cap, CAP_DECAP["hard1"])
        thr = n if p.ob < 15 else (mid if p.ob <= 45 else hi)
        if p.t_in / s.D >= thr or MATERIALS[p.mat].fh:
            s.cap_virtual = p.gap_after_ft * 12 / s.D < decap_gap_needed_cal(p.ob, s.cap)
            s.cap = "none"
            s.W *= 1 - s.cap_frac
    if ratio < 1.0:
        s.V = 0.0
        return "SHATTER" if shatters else "STOPPED"
    if shatters:
        s.integrity = "broken"
    df = okun_deflection(p.ob, ratio)
    s.last_df = df
    v_e = vl * energy_nbl_factor(p.ob)                   # Okun "Energy NBL" above 45 deg
    s.V = math.cos(rad(df)) * math.sqrt(max(s.V**2 - v_e**2, 0.0))   # Lambert-Jonas p=2, Okun cos(DF)
    s.path_ft += p.gap_after_ft
    return "PEN_BROKEN" if shatters else "PEN"


def burst_point(s: ShellState):
    if s.fuze_started_at is None:
        return None
    return s.fuze_started_at + s.delay_s * max(s.V, 1.0)


# =============================================================================
# 5. Data loading  (files from research/simulation/armor/: 07-calibration-tables.csv,
#    07-okun-facehard-tables.csv, 07-table-sources.csv, 07-trials.csv)
# =============================================================================
OKUN_PLATES = {"uk": "British CA", "us": "US Class A", "it": "Italian Terni", "de": "German KC n/A", "jp": "Japanese VH"}
OKUN_DECKS = {"uk": "UK NCA (25% el.)", "us": "US Class B/STS (25%)", "it": "Italian hom. (17%)",
              "de": "German Wh (18%)", "jp": "Japanese NVNC/CNC (Q0.97)"}


def load(folder):
    rd = lambda n: list(csv.DictReader(open(os.path.join(folder, n))))
    tabs, okun, trials = rd("07-calibration-tables.csv"), rd("07-okun-facehard-tables.csv"), rd("07-trials.csv")
    src = {r["src"]: r["source_note"] for r in rd("07-table-sources.csv")}
    for r in tabs:
        r["source"] = src.get(r["src"], "")
    return tabs, okun, trials


def f(x):
    try:
        return float(x)
    except (TypeError, ValueError):
        return None


# =============================================================================
# 6. Checks
# =============================================================================
def hdr(t):
    print("\n" + "=" * 100 + "\n" + t + "\n" + "=" * 100)


def check_usn_table():
    hdr("A. Thompson F_STD reproduces the navweaps/USN 16in/50 Mk 8 table (2,700 lb)")
    rows = [(2280, 2.5, 29.39, 0.67), (2074, 5.7, 26.16, 1.71), (1893, 9.8, 23.04, 2.79), (1740, 14.9, 20.04, 3.90),
            (1632, 21.1, 17.36, 5.17), (1567, 28.25, 14.97, 6.65), (1556, 36.27, 12.97, 8.48), (1607, 45.47, 11.02, 11.26)]
    print("| V fps | fall | belt table | belt F_STD | G&D x cos^1 | De Marre C=1.35 | deck table | deck F_STD |")
    print("|---|---|---|---|---|---|---|---|")
    for V, fa, belt, dk in rows:
        b = thompson_T(V, 16, 2700, fa)
        d = thompson_T(V, 16, 2700, 90 - fa)
        print(f"| {V} | {fa} | {belt} | {b:.2f} | {garzke_dulin(2700,16,V)*math.cos(rad(fa)):.2f} | "
              f"{demarre(2700,16,V,fa,1.35):.2f} | {dk} | {d:.2f} |")


def check_npg(trials):
    hdr("B. NPG 1946-47 measured limits: Thompson NBL vs reported 'USN NBL' and plate grade (% of NBL)")
    print("| shell | plate | T in | Ob | reported NBL | F_STD NBL | measured BL | grade % |")
    print("|---|---|---|---|---|---|---|---|")
    grades = {}
    for r in trials:
        if not r["date"].startswith("1946-47"):
            continue
        res = r["result"]
        try:
            rep = float(res.split("USN NBL")[1].split("fps")[0])
            meas = float(res.split("(")[1].split("fps")[0].replace("under", "").strip())
        except Exception:
            continue
        D, W, T, ob = f(r["cal_mm"]) / 25.4, f(r["shell_kg"]) / LB, f(r["plate_mm"]) / 25.4, f(r["obliquity_deg"])
        # NPG graded the as-measured thickness; plate_mm is actual where given
        nbl = thompson_vl(T, D, W, ob)
        g = 100 * meas / nbl
        key = r["plate_type"].split("(")[0].strip()
        grades.setdefault(key, []).append(g)
        print(f"| {r['gun_or_shell'][:26]} | {r['plate_type'][:34]} | {T:.2f} | {ob:.0f} | {rep:.0f} | {nbl:.0f} | "
              f"{'<' if 'under' in res else ''}{meas:.0f} | {g:.1f} |")
    print("\nGrade by plate family (mean % of standard NBL, n):")
    for k, v in grades.items():
        print(f"  {k:40s} {st.mean(v):6.1f} %  (n={len(v)}, range {min(v):.0f}-{max(v):.0f})")


def check_os751(trials):
    hdr("C. O.S. 751 (1947) Class A acceptance velocities as % of F_STD NBL -- calibre/T/D trend")
    print("| shell | Ob | T/D | spec fps | F_STD NBL | % |")
    print("|---|---|---|---|---|---|")
    pts = []
    for r in trials:
        if not r["date"].startswith("1947 spec") or "fn3" in r["result"] or not r["shell_kg"]:
            continue
        if "Class A" not in r["plate_type"]:
            continue
        D, W, T, ob, V = f(r["cal_mm"]) / 25.4, f(r["shell_kg"]) / LB, f(r["plate_mm"]) / 25.4, f(r["obliquity_deg"]), f(r["strike_v_ms"]) / FPS
        nbl = thompson_vl(T, D, W, ob)
        pts.append((D, T / D, ob, 100 * V / nbl, r["gun_or_shell"]))
    for D, td, ob, pc, nm in sorted(pts)[:: max(1, len(pts) // 30)]:
        print(f"| {nm[:34]} | {ob:.0f} | {td:.2f} | — | — | {pc:.1f} |")
    byD = {}
    for D, td, ob, pc, nm in pts:
        byD.setdefault(round(D), []).append(pc)
    print("\nMean acceptance velocity as % of F_STD NBL by calibre (Class A, intact-criterion rows):")
    for D in sorted(byD):
        print(f"  {D:>3}in : {st.mean(byD[D]):6.1f} %  (n={len(byD[D])})")


def okun_rows(okun, max_fall=40, min_cal=0):
    for r in okun:
        V, fa, kg, cal = f(r["strike_v_ms"]), f(r["fall_deg"]), f(r["shell_kg"]), f(r["cal_mm"])
        if V and kg and cal and fa is not None and fa <= max_fall and cal >= min_cal:
            yield r, V / FPS, fa, kg / LB, cal / 25.4


def check_okun_fh(okun):
    hdr("D. Okun FACEHARD tables vs Thompson homogeneous: R = T_FH / T_STS (same V, Ob), one criterion per table")
    print("R > 1: the shell defeats MORE of this face-hardened plate than standard STS, so engine Q = 1/R.")
    print("NL (Navy limit) is the engine's criterion; EFF (fit to burst) shown for comparison. Fall <= 30 deg.")
    for crit in ("NL", "EFF"):
        groups, guns = {}, {}
        for r, V, fa, W, D in okun_rows(okun, 30):
            th = thompson_T(V, D, W, fa)
            for c in OKUN_PLATES:
                t = f(r[f"{c}_{crit}_mm"])
                if t:
                    groups.setdefault(c, {}).setdefault(round(D), []).append(t / 25.4 / th)
                    guns.setdefault(round(D), set()).add(r["gun"].split(" Mark")[0].split(" SK")[0][:10])
        cals = sorted(guns)
        print(f"\nCriterion {crit}:\n| plate | " + " | ".join(f'{c}"' for c in cals) + " |")
        print("|---|" + "---|" * len(cals))
        for c, g in groups.items():
            print(f"| {OKUN_PLATES[c]} | " + " | ".join(f"{st.mean(g[k]):.2f}" if k in g else "–" for k in cals) + " |")
        print("| (tables) | " + " | ".join("/".join(sorted(guns[k])) for k in cals) + " |")


def fit_fh_law(okun):
    hdr("E. Free fit to Okun US Class A tables (EFF): ln(T/D) = lnC + a ln(W/D^3) + b ln V + n ln cos(Ob) + d ln D  (Ob<=30)")
    X, Y, Yt = [], [], []
    for r, V, fa, W, D in okun_rows(okun, 30):
        t = f(r["us_EFF_mm"])
        if not t:
            continue
        X.append((1.0, math.log(W / D**3), math.log(V), math.log(math.cos(rad(fa))), math.log(D)))
        Y.append(math.log(t / 25.4 / D))
        Yt.append(math.log(thompson_T(V, D, W, fa) / D))
    beta, bt = ols(X, Y), ols(X, Yt)
    pred = [sum(b * x for b, x in zip(beta, xi)) for xi in X]
    rms = math.sqrt(sum((p - y) ** 2 for p, y in zip(pred, Y)) / len(Y))
    print(f"Okun US Class A: n={len(Y)}  a={beta[1]:.2f}  b={beta[2]:.2f}  n_cos={beta[3]:.2f}  d={beta[4]:+.2f}  rms={100*rms:.1f}%")
    print(f"Thompson, same points:      a={bt[1]:.2f}  b={bt[2]:.2f}  n_cos={bt[3]:.2f}  d={bt[4]:+.2f}")
    print("(range tables tie V, Ob and D together; the free a/d split is not well determined -- see section O)")


def ols(X, Y):
    k = len(X[0])
    A = [[sum(x[i] * x[j] for x in X) for j in range(k)] for i in range(k)]
    b = [sum(x[i] * y for x, y in zip(X, Y)) for i in range(k)]
    for i in range(k):                               # Gauss-Jordan
        p = max(range(i, k), key=lambda r: abs(A[r][i]))
        A[i], A[p], b[i], b[p] = A[p], A[i], b[p], b[i]
        for r in range(k):
            if r != i:
                m = A[r][i] / A[i][i]
                A[r] = [a - m * c for a, c in zip(A[r], A[i])]
                b[r] -= m * b[i]
    return [b[i] / A[i][i] for i in range(k)]


def check_italian_obliquity(tabs):
    hdr("F. Italian official KC tables (1942): T(Ob)/T(0) vs Thompson and cos^n laws (381/50 APC, 884.8 kg)")
    rows = {}
    for r in tabs:
        if "Italian official" not in r["source"] or "APC" not in r["shell"] or "381" not in r["gun"]:
            continue
        if "obliquity" not in r["note"]:
            continue
        ob = f(r["note"].split("obliquity")[-1].strip().split()[0])
        rows.setdefault(r["range_m"], {})[ob] = f(r["belt_pen_mm"])
    print("| range m | T(0) mm | 10 | 20 | 30 | 40 | Thompson 10/20/30/40 | cos^1.5 | cos^2.14 |")
    print("|---|---|---|---|---|---|---|---|---|")
    W, D = 884.8 / LB, 15.0
    for rg, d in sorted(rows.items(), key=lambda kv: float(kv[0])):
        if not d.get(0):
            continue
        t0 = d[0] / 25.4
        V0 = thompson_vl(t0, D, W, 0)
        th = [thompson_T(V0, D, W, o) / t0 for o in (10, 20, 30, 40)]
        rat = [d.get(o, 0) / d[0] for o in (10, 20, 30, 40)]
        print(f"| {rg} | {d[0]:.0f} | " + " | ".join(f"{x:.3f}" for x in rat) + " | "
              + "/".join(f"{x:.2f}" for x in th) + " | "
              + "/".join(f"{math.cos(rad(o))**1.5:.2f}" for o in (10, 20, 30, 40)) + " | "
              + "/".join(f"{math.cos(rad(o))**2.14:.2f}" for o in (10, 20, 30, 40)) + " |")


def check_early():
    hdr("G. Early era: formula cross-checks and the WI-equivalent route (06 §1, §3)")
    print("Gun-table wrought-iron figures vs Tresidder (striking V back-solved):")
    for nm, W, D, V, quoted in (("13.5in BL Mk I-IV, 1000yd", 1250, 13.5, 1812, 28.2),
                                ("12in BL Mk VIII, 1000yd", 850, 12, 2200, 33.0),
                                ("100-ton RML Spezia 1876", 2000, 17.72, 1446, 22.0)):
        print(f"  {nm:30s} Tresidder {tresidder(W, D, V):5.1f} in  quoted {quoted}")
    print("\nAnnapolis 1890 (6in/100lb at 2,075 fps, ~10.5in plates): needed T_WI vs Tresidder capability")
    P = tresidder(100, 6, 2075)
    for mat, t, outcome in (("CMP", 10.72, "perforated"), ("STL", 10.57, "stopped"), ("NIS", 10.42, "stopped")):
        need = 1.77 * MATERIALS[mat].Q * t
        print(f"  {MATERIALS[mat].name:34s} T_WI-equiv {need:5.1f} in vs shot {P:4.1f} in -> "
              f"{'perforate' if P > need else 'stop'} (actual: {outcome})")
    print("\nSame shot through the Thompson engine with Q (WWII-STS equivalent):")
    for mat, t, outcome in (("CMP", 10.72, "perforated"), ("STL", 10.57, "stopped"), ("NIS", 10.42, "stopped"), ("WI", 10.5, "-")):
        vl = thompson_vl(MATERIALS[mat].Q * t, 6, 100, 0)
        print(f"  {MATERIALS[mat].name:34s} V_L {vl:5.0f} fps vs 2075 -> {'perforate' if 2075 > vl else 'stop'} (actual: {outcome})")
    print("\nIndian Head 1893: 14in Harvey vs 10in/500lb UNCAPPED at 2,059 fps (not perforated):")
    s = ShellState("10in Holtzer", 10, 500, 2059, cap="none", quality="steel_uncapped_1890")
    te, sh = plate_effective_T(Plate(14, "HRV", 0), s)
    print(f"  T_eff {te:.1f} in (shatter={sh}); V_L {thompson_vl(te/0.95, 10, 500, 0):.0f} fps")
    print("Beardmore 1901: 6in KC, 100lb 6in UNCAPPED limit 2,261 fps; CAPPED (105.9lb) perforated at 1,945:")
    for cap, W, V in (("none", 100, 2261), ("soft", 105.9, 1945)):
        s = ShellState("6in", 6, W, V, cap=cap, quality="softcap_1900")
        te, sh = plate_effective_T(Plate(6, "KCA", 0), s)
        print(f"  cap={cap:5s} T_eff {te:.2f} in shatter={sh}  V_L {thompson_vl(te/0.95, 6, W, 0):.0f} fps vs {V}")
    print("\nPeriod formula spread for one shot (12in 850lb at 2,000 fps, normal):")
    for nm, fn in (("Fairbairn WI", fairbairn), ("Tresidder WI", tresidder), ("Krupp WI", krupp_wi),
                   ("De Marre WI", demarre_wi), ("Gavre WI", gavre)):
        print(f"  {nm:14s} {fn(850, 12, 2000):5.1f} in")
    print(f"  De Marre NiS   {demarre(850, 12, 2000):5.1f} in   Krupp KC uncapped {krupp_kc_uncapped(850,12,2000):5.1f}"
          f"   Davis Harvey capped {davis_harvey_capped(850,12,2000):5.1f}   Thompson STS {thompson_T(2000,12,850,0):5.1f}")


def check_multiplate():
    hdr("H. Multi-plate stepping (Thompson + Lambert-Jonas p=2 + Okun DF) vs test cases")
    print("Hood 1919 deck series: 15in Greenboy 1,920 lb (inert) at 1,350 fps, 58 deg; 2in HT upper deck, then HT main deck.")
    print("Okun's reconstruction: 1,236 fps after the upper deck; 1,093 / 714 / 657 after a 3 / 5.5 / 6 in main deck.")
    print("| main deck | model V after upper | model V after main | model main-deck V_L | Okun-implied V_L (p=2) |")
    print("|---|---|---|---|---|")
    for main, okun_after in ((3.0, 1093), (5.5, 714), (6.0, 657)):
        s = ShellState("15in Greenboy", 15, 1920, 1350, cap="hard1")
        resolve_plate(s, Plate(2.0, "HT", 58, gap_after_ft=8))
        v1, ob2 = s.V, 58 - s.last_df
        te, _ = plate_effective_T(Plate(main, "HT", ob2), s)
        vl = thompson_vl(te, 15, s.W, ob2)
        resolve_plate(s, Plate(main, "HT", ob2))
        print(f"| {main} in at {ob2:.1f} deg | {v1:.0f} | {s.V:.0f} | {vl:.0f} | {math.sqrt(1236**2 - okun_after**2):.0f} |")
    print("\nSpaced 2+1+2in STS at 68 deg vs solid 5in, 14in 1,500 lb (USN test via forum: 1,250 vs 1,690 fps -> 0.74):")
    solid = thompson_vl(5, 14, 1500, 68)
    for label, df_cap in (("resolver deflection", None), ("deflection capped at 3 deg (thin plates)", 3.0)):
        lo, hi = 300.0, 3000.0
        for _ in range(40):
            mid = (lo + hi) / 2
            s = ShellState("14in", 14, 1500, mid, cap="none")
            ok, ob = True, 68.0
            for t in (2, 1, 2):
                if not resolve_plate(s, Plate(t, "STS", ob)).startswith("PEN"):
                    ok = False
                    break
                ob = max(ob - (min(s.last_df, df_cap) if df_cap else s.last_df), 0)
            lo, hi = (lo, mid) if ok else (mid, hi)
        print(f"  {label:42s}: stack V_L {hi:.0f} fps vs solid {solid:.0f} -> ratio {hi/solid:.2f}")
    t = [2.0, 1.0, 2.0]
    sp = sum(x**1.4 for x in t) ** (1 / 1.4)
    print(f"  Okun 1.4-rule: equivalent {sp:.2f} in -> ratio {thompson_vl(sp, 14, 1500, 68)/solid:.2f}")
    print("\nOkun's equivalent-thickness rules, 5+2in:")
    t = [5.0, 2.0]
    sp = sum(x**1.4 for x in t) ** (1 / 1.4)
    print(f"  sum {sum(t):.2f}  spaced(1.4) {sp:.2f}  laminated avg {(sum(t)+sp)/2:.2f}  USN 0.7-rule {0.7*5+2:.2f}  M79 5.55-5.98")


def check_fuze_and_burst():
    hdr("I. Fuze start thresholds (Okun formula, Tfz 0.07) and burst distance behind the first armour")
    print("| shell | 0 deg | 30 | 45 | 61+ | US doc (Mk 21) |")
    print("|---|---|---|---|---|---|")
    for nm, D in (("6in", 6), ("8in", 8), ("14in", 14), ("16in", 16), ("18.1in", 18.1)):
        doc = "1.5in / 0.375in at 65" if D == 16 else ""
        print(f"| {nm} | " + " | ".join(f"{okun_fuze_min(D, o):.2f}" for o in (0, 30, 45, 65)) + f" | {doc} |")
    print("\nBurst distance (ft) = delay x residual V:")
    for d in (0.003, 0.015, 0.025, 0.033, 0.035, 0.08, 0.4):
        print(f"  {d:5.3f} s : " + "  ".join(f"{v} fps -> {d*v:5.0f}" for v in (600, 1000, 1500)))


def defeated_thickness(mat, D, W, V, ob, cap, quality):
    """Actual plate thickness (in) of `mat` that the shell just defeats (bisection on the resolver's limit)."""
    lo, hi = 0.05, 6 * D
    for _ in range(50):
        t = 0.5 * (lo + hi)
        s = ShellState("x", D, W, V, cap=cap, quality=quality)
        te, _ = plate_effective_T(Plate(t, mat, ob), s)
        if thompson_vl(te / SHELL_QUALITY[quality], D, W, ob) < V:
            lo = t
        else:
            hi = t
    return 0.5 * (lo + hi)


def check_immune_zone_example():
    hdr("J. Era illustration: plate the same shot defeats, by armour type (12in 850lb, 2,000 fps, Ob 10 deg)")
    print("| armour | years | uncapped steel AP (1890s) in | capped AP (WWII quality) in | weight per protection vs STS |")
    print("|---|---|---|---|---|")
    for mid in ("WI", "LAMWI", "STL", "CMP", "NIS", "HRV", "KCA", "BKC", "WKC", "KNC", "CB1", "USA", "CA", "KCNA", "TKC", "VH", "STS", "WH"):
        m = MATERIALS[mid]
        tu = defeated_thickness(mid, 12, 850, 2000, 10, "none", "steel_uncapped_1890")
        tc = defeated_thickness(mid, 12, 850, 2000, 10, "hard1", "ww2")
        ts = defeated_thickness("STS", 12, 850, 2000, 10, "hard1", "ww2")
        print(f"| {m.name[:44]} | {m.years} | {tu:.1f} | {tc:.1f} | {(m.rho/7850)*(tc/ts):.2f} |")


def check_scatter():
    hdr("K. Scatter: P(perforate) vs V/V_L with plate N(1,0.06) and shot N(0,0.03) on the limit")
    rnd = random.Random(1)
    for ratio in (0.85, 0.9, 0.95, 1.0, 1.05, 1.1, 1.15):
        n = 20000
        k = sum(ratio > (1 + rnd.gauss(0, 0.06)) * (1 + rnd.gauss(0, 0.03)) for _ in range(n))
        print(f"  V/V_L {ratio:.2f}: {100*k/n:5.1f} %")



EARLY_TRIALS = [  # (label, D in, W lb, V fps, plate in, obliquity, material, relation, source)
    ("Spezia 1876: 22in WI perforated, ~590 fps spare (100-ton Palliser)", 17.72, 2000, 1446, 22, 0, "WI", "<", "06 §3.1"),
    ("Annapolis 1890: 6in Holtzer 100lb perforated 10.72in compound", 6, 100, 2075, 10.72, 0, "CMP", "<", "06 §3.4"),
    ("Annapolis 1890: 6in stopped by 10.57in all-steel (pen. 12in)", 6, 100, 2075, 10.57, 0, "STL", ">~", "06 §3.4"),
    ("Annapolis 1890: 6in stopped by 10.42in nickel steel", 6, 100, 2075, 10.42, 0, "NIS", ">", "06 §3.4"),
    ("Annapolis 1890: 8in Firth 210lb perforated compound", 8, 210, 1850, 10.72, 0, "CMP", "<", "06 §3.4"),
    ("Annapolis 1890: 8in stopped (broke) by nickel steel", 8, 210, 1850, 10.42, 0, "NIS", ">", "06 §3.4"),
    ("Johnson 1894: 10in 500lb lodged in 10in NiS at 1490 (limit)", 10, 500, 1490, 10, 0, "NIS", "=", "06 §3.5"),
    ("Indian Head 1893: 14in Harvey NOT perf. by uncapped 10in at 2059", 10, 500, 2059, 14, 0, "HRV", ">", "06 §3.5"),
    ("1897: 12in Holtzer 850lb just perf. 12in Harvey at 1811 (uncapped)", 12, 850, 1811, 12, 0, "HRV", "=", "07 trials"),
    ("NO 1937 prob. VII: 10in 500lb Harvey 13in limit 1620", 10, 500, 1620, 13, 0, "HRV", "=", "01 §1.2"),
    ("Meppen 1895: 11.8in KC not perf. by 12in 716lb at 1993 (uncapped)", 12, 716, 1993, 11.8, 0, "KCA", ">", "06 §3.6"),
    ("Beardmore 1901: 6in KC limit 2261, 100lb UNCAPPED", 6, 100, 2261, 6, 0, "KCA", "=", "06 §3.6"),
    ("Beardmore 1901: 6in KC perforated by CAPPED 105.9lb at 1945", 6, 105.9, 1945, 6, 0, "KCA", "<", "06 §3.6"),
    ("Krupp 250mm KC: capped 150mm limit ~2240", 5.9, 112, 2240, 9.84, 0, "KCA", "=", "06 §3.6"),
    ("NO 1937 prob. V: 12in 870lb vs 12in KC limit 1420", 12, 870, 1420, 12, 0, "KCA", "=", "01 §1.2"),
    ("NO 1937 prob. VII: 10in 500lb vs 12in KC limit 1685", 10, 500, 1685, 12, 0, "KCA", "=", "01 §1.2"),
    ("NO 1937 prob. VI: 12in 870lb vs 14in Class A limit 1570", 12, 870, 1570, 14, 0, "KCA", "=", "01 §1.2"),
    ("Baden 1921: 15in Greenboy perforated 350mm KC a/A at 18.5 deg", 15, 1910, 1550, 13.78, 18.5, "KCA", "<", "07 trials"),
    ("Baden 1921: 15in Greenboy stopped by 350mm KC CT at 30 deg", 15, 1910, 1550, 13.78, 30, "KCA", ">", "07 trials"),
    ("1918-19: Russian 12in 1038lb intact through 8in British KC at 20", 12, 1038, 1615, 8, 20, "BKC", "<", "07 trials"),
    ("1918-19: same shell broke on the 8in plate at 1447", 12, 1038, 1447, 8, 20, "BKC", "~", "07 trials"),
]


def check_early_trials_Q():
    hdr("L. Early trials re-run through Thompson: implied plate x shell quality Q (thickness, STS = 1)")
    print("Q_implied = Thompson-STS thickness the shot defeats / plate thickness.  Relation: '<' plate was beaten")
    print("(so Q is below this), '>' plate held (Q above), '=' limit.  Also the Tresidder WI figure and STS/WI ratio.")
    print("| trial | T/D | Thompson STS in | Q implied | table Q (shatter-adj.) | Tresidder WI in | STS/WI |")
    print("|---|---|---|---|---|---|---|")
    ratios = []
    for nm, D, W, V, T, ob, mat, rel, srcn in EARLY_TRIALS:
        ts = thompson_T(V, D, W, ob)
        tr = tresidder(W, D, V)
        if ob == 0:
            ratios.append(tr / ts)
        uncapped = "UNCAPPED" in nm or "uncapped" in nm or "Holtzer" in nm or "Firth" in nm or "Palliser" in nm
        s = ShellState("x", D, W, V, cap="none" if uncapped else "hard1", quality="ww2")
        te, sh = plate_effective_T(Plate(T, mat, ob), s)
        print(f"| {nm} | {T/D:.2f} | {ts:.1f} | {rel} {ts/T:.2f} | {te/T:.2f}{' (shatter)' if sh else ''} | {tr:.1f} | {tr/ts:.2f} |")
    print(f"\nTresidder-WI / Thompson-STS over the normal-impact shots: mean {st.mean(ratios):.2f}, "
          f"range {min(ratios):.2f}-{max(ratios):.2f} (n={len(ratios)}; Tresidder has no obliquity term)")


def print_materials():
    hdr("M. Material table (engine basis): effective Q vs shell calibre (plate = 1 cal, or the type's minimum)")
    print("| id | material | rho | FH | Q(6in) | Q(8in) | Q(12in) | Q(16in) | years |")
    print("|---|---|---|---|---|---|---|---|---|")
    for m in MATERIALS.values():
        qs = []
        for D in (6, 8, 12, 16):
            s = ShellState("x", D, 0.6 * D**3, 1800)
            t = max(1.0 * D, m.t_min_in)
            te, _ = plate_effective_T(Plate(t, m.id, 0), s)
            qs.append(te / t)
        print(f"| {m.id} | {m.name} | {m.rho} | {'Y' if m.fh else ''} | " + " | ".join(f"{q:.2f}" for q in qs) + f" | {m.years} |")


def check_deck_models(okun):
    hdr("N. Deck plates: Okun M79 deck column / Thompson F_STD at Ob = 90 - fall, by deck obliquity")
    bins = {}
    for r, V, fa, W, D in okun_rows(okun, 90):
        if fa < 5:
            continue
        ob = 90 - fa
        th = thompson_T(V, D, W, min(ob, 75)) * 25.4
        if ob > 75:
            th *= math.cos(rad(ob)) / math.cos(rad(75))
        for c in ("us", "de"):
            dmm = f(r[f"{c}_deck_mm"])
            if dmm:
                bins.setdefault((c, int(ob // 10) * 10), []).append(dmm / th)
    for c in ("us", "de"):
        print(f"  {OKUN_DECKS[c]}:")
        for (cc, b), v in sorted(bins.items()):
            if cc == c:
                print(f"    Ob {b}-{b+10}: {st.mean(v):.2f}  (n={len(v)}, {min(v):.2f}-{max(v):.2f})")


def fit_fh_fixed(okun):
    hdr("O. FH tables (Okun EFF criterion, all 18 tables, Ob<=30, D>=8in): exponents with weight exponent fixed at 0.2")
    for c, name in OKUN_PLATES.items():
        X, Y = [], []
        for r, V, fa, W, D in okun_rows(okun, 30, 200):
            t = f(r[f"{c}_EFF_mm"])
            if not t:
                continue
            X.append((1.0, math.log(V), math.log(math.cos(rad(fa))), math.log(D)))
            Y.append(math.log(t / 25.4 / D) - 0.2 * math.log(W / D**3))
        b = ols(X, Y)
        pred = [sum(cc * x for cc, x in zip(b, xi)) for xi in X]
        rms = math.sqrt(sum((p - y) ** 2 for p, y in zip(pred, Y)) / len(Y))
        print(f"  {name:16s} n={len(Y):3d}  b(V)={b[1]:.2f}  n(cos)={b[2]:.2f}  d(D)={b[3]:+.2f}  rms={100*rms:.1f}%")


def check_iowa_iz():
    hdr("P. Immune zone: Iowa scheme vs 16in/50 Mk 8 (2,700 lb), broadside-on.  USN figure: 23.6-27.4 kyd")
    tab = [(10000, 2074, 5.7), (15000, 1893, 9.8), (20000, 1740, 14.9), (25000, 1632, 21.1), (30000, 1567, 28.25),
           (35000, 1556, 36.27), (40000, 1607, 45.47)]          # navweaps 16in/50 range table (V fps, fall deg)

    def at(yd):                                               # linear interpolation between table rows
        for (y0, v0, f0), (y1, v1, f1) in zip(tab, tab[1:]):
            if y0 <= yd <= y1:
                k = (yd - y0) / (y1 - y0)
                return v0 + k * (v1 - v0), f0 + k * (f1 - f0)
    lam = (4.75 + 1.25 + (4.75**1.4 + 1.25**1.4) ** (1 / 1.4)) / 2   # 4.75 Class B on 1.25 STS, laminated
    out = {"belt_bare": None, "belt_hull": None, "deck_pen": None, "deck_pen_nogate": None}
    for yd in range(10000, 40001, 250):
        V, fall = at(yd)
        ob = compound_obliquity(fall, 90, 19)
        a_ = ShellState("16", 16, 2700, V, cap="hard1", cap_frac=0.06)
        if resolve_plate(a_, Plate(12.1, "USA", ob)) == "PEN":
            out["belt_bare"] = yd
        b_ = ShellState("16", 16, 2700, V, cap="hard1", cap_frac=0.06)
        resolve_plate(b_, Plate(1.5, "STS", ob, gap_after_ft=4.0))
        if b_.V > 0 and resolve_plate(b_, Plate(12.1, "USA", ob)) == "PEN":
            out["belt_hull"] = yd
        d = ShellState("16", 16, 2700, V, cap="hard1", cap_frac=0.06)
        obd = 90 - fall
        if resolve_plate(d, Plate(1.5, "STS", obd, gap_after_ft=8)).startswith("PEN") and \
                resolve_plate(d, Plate(lam, "STS", obd - d.last_df)).startswith("PEN") and out["deck_pen"] is None:
            out["deck_pen"] = yd
        if out["deck_pen_nogate"] is None and thompson_vl(spaced([1.5, lam]), 16, 2700, min(obd, 75)) * (
                math.cos(rad(75)) / math.cos(rad(obd)) if obd > 75 else 1) < V:
            out["deck_pen_nogate"] = yd
    print("Belt 12.1in Class A @19 deg, no outer hull: effective penetration out to", out["belt_bare"], "yd")
    print("Belt behind 1.5in STS outer hull (4 ft gap, decaps -> shell shatters on Class A): intact penetration out to",
          out["belt_hull"], "yd (None = never; plate only holed by a broken shell)")
    print("Deck 1.5in STS + 4.75 Class B on 1.25 STS, stepped, with the 65-80 deg ricochet gate: penetrated from",
          out["deck_pen"], "yd")
    print("Deck as one Okun-spaced plate, Thompson, no ricochet gate: penetrated from", out["deck_pen_nogate"], "yd")


if __name__ == "__main__":
    folder = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.abspath(__file__))
    check_usn_table()
    check_fuze_and_burst()
    check_early()
    check_immune_zone_example()
    check_multiplate()
    check_scatter()
    check_early_trials_Q()
    print_materials()
    check_iowa_iz()
    try:
        tabs, okun, trials = load(folder)
    except FileNotFoundError:
        print("\n(07_*.csv not found next to this file -- data checks skipped)")
        sys.exit(0)
    check_npg(trials)
    check_os751(trials)
    check_okun_fh(okun)
    fit_fh_law(okun)
    fit_fh_fixed(okun)
    check_italian_obliquity(tabs)
    check_deck_models(okun)
