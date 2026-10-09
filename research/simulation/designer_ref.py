"""
designer_ref.py  -  From technology to ship-designer choices to combat-model inputs, for fire control.

Pipeline (no years anywhere in the derivations; years only appear in the comments that name the
historical analogue of a tech tier):

    TECH  (raw physical values the tech tree outputs)
      |   OpticsTech, RadarTech, ComputerTech, MountTech, PropellantTech, CrewTech
      v
    DESIGN (what the player / AI picks in the ship designer)
      |   RFFit, DirectorFit, RadarFit, FCSystemFit
      v
    DERIVED  -> combat model:   Sensor objects + an FCLevel  (consumed by fire_control_ref.engage)
             -> ship designer:  mass, height of centre of mass, rotating mass, crew, power, volume

Then a trade study: how big a rangefinder should a cruiser carry?

Tags: [S] sourced (see note §8 / fire-control-research/component-costs.md), [INFERRED] my estimate or fit,
[CAL] calibrated so that the legacy levels in fire_control_ref.py are reproduced.

Run:  python3 designer_ref.py            (all tables, ~10 min on 2 cores)
      python3 designer_ref.py physics    (fast analytic tables only)
"""
import math
import sys
from dataclasses import dataclass, field, replace
from multiprocessing import Pool

import numpy as np

import fire_control_ref as fc
from fire_control_ref import (YD, KN, MIL, ARCSEC, KYD, GUNS, TARGETS, Sensor, Conditions, FCLevel,
                              Shooter, Scenario, engage, horizon, radar_horizon, kyd)

C_LIGHT = 2.998e8
RHO_STEEL = 7.85      # t/m3


# =============================================================================
# 1. TECH: raw physical values the tech tree produces
# =============================================================================
@dataclass
class OpticsTech:
    """What the optical industry can make. Consumed by every optical sensor derivation."""
    name: str
    delta_arcsec: float = 12.0  # stereo/coincidence acuity of a trained rangetaker through these optics [S: USN 12"]
    rigid_base: float = 5.0     # m: longest base whose tube stays aligned (temperature, flexure) [S qual. NDRC 1941]
    flex_exp: float = 0.75      # accuracy exponent of base beyond rigid_base (1 = perfect) [INFERRED from NDRC]
    M_max: float = 30.0         # highest magnification the glass delivers at full image quality
    stereo: bool = True         # stereo instruments available (needs selected operators)
    coated: bool = False        # anti-reflection coatings: better in haze and dusk
    mass_k: float = 27.0        # kg per m^2 of base, bare naval instrument [INFERRED: 73 kg NMM 1.5 m, B^2]
    gyro_stab: bool = False     # gyro-stabilised mountings available (more readings per minute)


@dataclass
class RadarTech:
    """What the electronics industry can make."""
    name: str
    lam_min: float = 0.10       # shortest wavelength with useful power, m
    P_max_kW: float = 50.0      # peak power available at that wavelength
    tau_us: float = 0.5         # shortest usable pulse, microseconds
    NF_dB: float = 13.0         # receiver noise figure [S typ.: 10-15 dB WWII, 3-6 dB 1970s]
    bearing: str = "lobing"     # none | lobing | conical | monopulse : bearing-measurement method
    range_unit: str = "precision"  # scope | precision | digital : range measurement circuit
    auto_track: bool = False    # automatic range/bearing tracking
    splash: str = "range"       # none | range | both : what the display can show of shell splashes
    set_mass_t: float = 1.8     # transmitter/receiver/console mass, t (excluding antenna) [S: Mk 3 1.7 t packed]
    ant_kg_m2: float = 40.0     # antenna mass per m^2 aperture [S: SK 2,400 lb / 27 m^2; SPG-53 163 lb / 1.8 m^2]


@dataclass
class ComputerTech:
    """What the fire-control computer can do. Maps to the tracker kinds in fire_control_ref §4."""
    name: str
    tracker: str = "cart"       # polar (rate-keeping) | cart (true course) | ca (acceleration) | ct (turn)
    helm_free: bool = True      # own ship may manoeuvre without losing the solution
    latency: float = 8.0        # s, reading -> computer
    q: float = 0.03             # target manoeuvre allowance, m^2/s^3
    max_rdot: float = 1e9       # m/s, clock limit
    max_bdot: float = 1e9       # rad/s
    own_turn_kick: float = 250.0  # m, solution error while own ship turns
    wander_pct: float = 0.9     # salvo-to-salvo generated-range wander + transmission, % of range [CAL]
    resid: float = 0.006        # residual ballistic bias before spotting (wind, density, MV, drift corrections)
    defl_bias_mil: float = 1.5
    detect: bool = False
    sig_a: float = 0.25
    q_turn: float = 1e-5
    tau: float = 20.0
    mass_t: float = 3.0         # computer/table mass, t [S: Ford Mk 1A 1.4 t; Dreyer [INFERRED] 3-5 t]
    crew: int = 6               # table crew [S: Dreyer 7-8 + helpers, Ford 1-3]
    power_kW: float = 5.0


@dataclass
class MountTech:
    """Gun-laying chain: director, stable element, power training/elevation."""
    name: str
    director: bool = True       # guns laid from one director (else each gun lays itself)
    stab_vertical: bool = False # stable vertical / gyro firing: no sea-state penalty
    lay_mil: float = 0.8        # common laying error per salvo, 1 sigma, mil [CAL]
    defl_spot_mil: float = 1.5  # salvo deflection wander, mil [CAL]


@dataclass
class PropellantTech:
    name: str
    mv_gun_pct: float = 0.35    # round-to-round muzzle velocity sigma, % [S typ.: smokeless 0.3-0.5, brown 0.8, black 1.0]
    smoke: str = "smokeless"    # black | brown | smokeless (manual-gunnery smoke model)


@dataclass
class CrewTech:
    name: str
    operator: float = 1.0       # rangetaker multiplier: 0.85 elite, 1 trained, 1.5 average, 2 green
    stereo_pool: float = 0.05   # fraction of recruits who pass the stereo test [S: under 5 %]
    spot_k: float = 1.0


# Example tiers. Names give the historical analogue only; the derivations never use the year.
OPTICS = {
    "O1": OpticsTech("O1 early coincidence (B&S FA, c.1895)", delta_arcsec=14, rigid_base=2.0, M_max=20,
                     stereo=False, mass_k=30),
    "O2": OpticsTech("O2 mature coincidence (FQ2/FT24, c.1912)", delta_arcsec=12, rigid_base=4.0, M_max=28,
                     stereo=False, mass_k=28),
    "O3": OpticsTech("O3 stereo + long base (Zeiss, B&S, c.1925)", delta_arcsec=12, rigid_base=5.0, M_max=30,
                     stereo=True, mass_k=27),
    "O4": OpticsTech("O4 coated, stabilised (US 1943)", delta_arcsec=12, rigid_base=6.0, flex_exp=0.8,
                     M_max=30, stereo=True, coated=True, mass_k=25, gyro_stab=True),
}
RADAR = {
    "R1": RadarTech("R1 metric (Seetakt, 284, Mk 3)", lam_min=0.40, P_max_kW=20, tau_us=2.0, NF_dB=10,
                    bearing="none", range_unit="scope", splash="range", set_mass_t=1.7),
    "R2": RadarTech("R2 early centimetric (Mk 8)", lam_min=0.10, P_max_kW=25, tau_us=1.0, NF_dB=12,
                    bearing="lobing", range_unit="precision", splash="range", set_mass_t=2.2),
    "R3": RadarTech("R3 X-band (Mk 13, 1945)", lam_min=0.03, P_max_kW=50, tau_us=0.3, NF_dB=15,
                    bearing="lobing", range_unit="precision", splash="both", set_mass_t=2.2),
    "R4": RadarTech("R4 auto-track analog (Mk 56/35)", lam_min=0.03, P_max_kW=250, tau_us=0.25, NF_dB=12,
                    bearing="conical", range_unit="precision", auto_track=True, splash="both", set_mass_t=2.3),
    "R5": RadarTech("R5 solid-state digital (SPG-60, WM-25)", lam_min=0.03, P_max_kW=200, tau_us=0.2, NF_dB=5,
                    bearing="monopulse", range_unit="digital", auto_track=True, splash="both", set_mass_t=1.5),
}
COMPUTERS = {
    "C0": ComputerTech("C0 none: plot by hand", "polar", False, latency=20, q=0.02, own_turn_kick=500,
                       wander_pct=2.45, resid=0.02, defl_bias_mil=3.0, mass_t=0.2, crew=3, power_kW=0),
    "C1": ComputerTech("C1 rate clock + Dumaresq", "polar", False, latency=15, q=0.03, max_rdot=18.3,
                       own_turn_kick=400, wander_pct=1.78, resid=0.015, defl_bias_mil=2.5, mass_t=0.8, crew=4, power_kW=0),
    "C2": ComputerTech("C2 plotting table (Dreyer)", "polar", True, latency=15, q=0.03, max_rdot=18.3,
                       max_bdot=math.radians(0.25), own_turn_kick=300, wander_pct=1.48, resid=0.012,
                       defl_bias_mil=2.0, mass_t=4.0, crew=10, power_kW=1),
    "C3": ComputerTech("C3 true-course table (AFCT)", "cart", True, latency=8, q=0.03, own_turn_kick=250,
                       wander_pct=1.19, resid=0.006, defl_bias_mil=1.5, mass_t=5.0, crew=8, power_kW=5),
    "C4": ComputerTech("C4 rangekeeper + stable vertical (Ford Mk 8)", "cart", True, latency=3, q=0.05,
                       own_turn_kick=150, wander_pct=0.99, resid=0.004, defl_bias_mil=1.0, mass_t=3.0, crew=4, power_kW=10),
    "C5": ComputerTech("C5 analog with acceleration (Mk 1A/Mk 47 class)", "ca", True, latency=1, q=0.02,
                       own_turn_kick=50, wander_pct=0.68, resid=0.004, defl_bias_mil=0.8, detect=True, sig_a=0.1,
                       mass_t=2.5, crew=3, power_kW=10),
    "C6": ComputerTech("C6 digital turn filter (Mk 86/WM-25)", "ct", True, latency=0.5, q=0.01, q_turn=5e-7,
                       own_turn_kick=20, wander_pct=0.36, resid=0.003, defl_bias_mil=0.5, detect=True,
                       mass_t=0.8, crew=2, power_kW=8),
}
MOUNTS = {
    "M0": MountTech("M0 local laying, no director", director=False, lay_mil=2.5, defl_spot_mil=2.5),
    "M1": MountTech("M1 director, follow-the-pointer", lay_mil=1.0, defl_spot_mil=2.0),
    "M2": MountTech("M2 director, power drive", lay_mil=0.8, defl_spot_mil=1.5),
    "M3": MountTech("M3 stable vertical + RPC", stab_vertical=True, lay_mil=0.5, defl_spot_mil=1.2),
    "M4": MountTech("M4 digital servo", stab_vertical=True, lay_mil=0.3, defl_spot_mil=0.5),
}
PROPELLANT = {
    "black": PropellantTech("black powder", 1.0, "black"),
    "brown": PropellantTech("brown powder", 0.8, "brown"),
    "cordite": PropellantTech("early smokeless", 0.5),
    "nc": PropellantTech("mature smokeless, flashless", 0.35),
}


# =============================================================================
# 2. Optical rangefinder: design choice -> Sensor + cost
# =============================================================================
MAST_STIFF = {"turret": 70.0, "tower": 60.0, "tripod": 50.0, "pole": 32.0, "deck": 55.0}  # M_vib0 [INFERRED]
M_ATM_CLEAR = 60.0     # magnification beyond which clear-air shimmer starts to cost [INFERRED]
_REF_MLIM = 1.0 / math.hypot(1 / M_ATM_CLEAR, 1 / 60.0)
_REF_NORM = (28.0 / math.sqrt(1 + (28.0 / _REF_MLIM) ** 2)) / 28.0   # legacy levels assume M=28 is nominal [CAL]


@dataclass
class RFFit:
    base: float                 # m
    mag: float                  # x
    kind: str = "stereo"        # stereo | coinc
    mount: str = "director"     # open | hood | armoured | director | turret
    support: str = "tower"      # what it stands on: turret | tower | tripod | pole | deck
    height: float = 25.0        # m above the waterline
    armour_mm: float = 0.0
    count: int = 1              # identical instruments
    label: str = ""


class RFSensor(Sensor):
    """Optical rangefinder whose error comes from tech + fit instead of fixed constants."""

    def __init__(self, fit: RFFit, tech: OpticsTech, name=None):
        per_min = (3.0 if fit.kind == "coinc" else 4.0) * (1.5 if tech.gyro_stab else 1.0)
        per_min *= min(1.0, 30.0 / fit.mag) ** 0.5           # narrow field: slower to pick up [INFERRED]
        brg = {"director": 0.7, "turret": 1.5}.get(fit.mount, 1.0)
        super().__init__(name or f"{fit.base:.1f} m x{fit.mag:.0f} {fit.kind}", fit.kind, fit.height,
                         per_min=per_min, base=fit.base, mag=fit.mag, sig_brg_mil=brg)
        self.fit, self.tech = fit, tech

    def M_eff(self, cond):
        """Seeing and vibration cap useful magnification: M_eff = M / sqrt(1 + (M/M_lim)^2),
        M_lim combines atmosphere (worse in haze) and the support's vibration (worse when tall)."""
        f = self.fit
        M = min(f.mag, self.tech.M_max * 1.3)
        m_atm = M_ATM_CLEAR / max(cond.k_optic, 1.0)
        m_vib = MAST_STIFF[f.support] * min(1.0, (25.0 / max(f.height, 1.0)) ** 0.5)
        if self.tech.gyro_stab:
            m_vib *= 1.5
        m_lim = 1.0 / math.hypot(1 / m_atm, 1 / m_vib)
        Me = M / math.sqrt(1 + (M / m_lim) ** 2) / _REF_NORM
        if M > self.tech.M_max:                                   # dimmer image beyond what the glass supports
            Me *= (self.tech.M_max / M) ** 0.5
        return Me

    def B_eff(self):
        B, t = self.fit.base, self.tech
        return B if B <= t.rigid_base else t.rigid_base * (B / t.rigid_base) ** t.flex_exp

    def unit_error(self, R, cond=None):
        cond = cond or Conditions()
        return self.tech.delta_arcsec * ARCSEC * R * R / (self.B_eff() * self.M_eff(cond))

    def sigma_range(self, R, cond, tc, operator=1.0):
        if self.fit.kind == "stereo" and not self.tech.stereo:
            return None
        kc = cond.k_optic
        if self.kind == "stereo" and kc > 1.0:
            kc = 1.0 + 0.8 * (kc - 1.0)
        if self.tech.coated and kc > 1.0:
            kc = 1.0 + 0.8 * (kc - 1.0)                              # coatings: less veiling glare [INFERRED]
        if self.kind == "coinc" and cond.low_contrast:
            kc *= 1.5
        spray = 1.0 + max(0, cond.sea - 2) * 0.15 * max(0.0, (14.0 - self.height) / 8.0)  # low mounts [INFERRED]
        return operator * kc * spray * self.unit_error(R, cond)

    def sigma_bias(self, R):
        return 1.5 * self.unit_error(R)


def rf_sensors(fit: RFFit, tech: OpticsTech):
    return [RFSensor(fit, tech, f"{fit.label or 'RF'} #{i + 1}") for i in range(fit.count)]


def box_shell_t(W, D, H, t_mm, frac=1.0):
    """Mass (t) of a closed box (no floor) W x D x H plated t_mm, on a fraction of its area."""
    A = 2 * W * H + 2 * D * H + W * D
    return A * frac * t_mm / 1000.0 * RHO_STEEL


def rf_cost(fit: RFFit, tech: OpticsTech):
    """Designer cost of one rangefinder fit (all instruments). Director-mounted RFs are costed by the director."""
    m_inst = tech.mass_k * fit.base ** 2 / 1000.0                     # t
    W, D, H = fit.base + 0.6, 1.6, 1.9                                # hood box around the tube [INFERRED]
    if fit.mount == "open":
        m = 3.0 * m_inst                                              # pedestal, training gear [INFERRED x3]
        crew = 2
    elif fit.mount in ("hood", "turret"):
        m = 3.0 * m_inst + box_shell_t(W, D, H, max(fit.armour_mm, 6.0))
        crew = 2
    elif fit.mount == "armoured":
        m = 3.0 * m_inst + box_shell_t(W, D, H, fit.armour_mm)
        crew = 3
    else:   # director: the instrument only; the director body is in director_cost
        m = 2.0 * m_inst
        crew = 1
    if fit.kind == "stereo":
        crew += 0                                                     # one selected operator either way
    return dict(mass=m * fit.count, z=fit.height, crew=crew * fit.count, m_inst=m_inst,
                rot_diam=W, power_kW=0.5 * fit.count)


@dataclass
class DirectorFit:
    rf: RFFit                   # rangefinder inside (mount='director'); base sets the body width
    armour_mm: float = 12.7
    armour_frac: float = 0.6    # share of the plating at full thickness (front, sides, roof)
    radar: "RadarFit" = None
    crew: int = 6
    equip_t: float = 8.0        # sights, training/elevation gear, transmitters, seats [CAL: Mk 37]


def director_cost(d: DirectorFit, tech: OpticsTech):
    W = max(3.0, d.rf.base + 0.6)
    D, H = 3.5, 2.6
    shell = box_shell_t(W, D, H, d.armour_mm, d.armour_frac) + box_shell_t(W, D, H, 6.0, 1 - d.armour_frac)
    m = shell + d.equip_t + rf_cost(d.rf, tech)["mass"]
    if d.radar is not None:
        m += d.radar.antenna_mass()
    return dict(mass=m, z=d.rf.height, crew=d.crew, rot_diam=W, shell=shell)


# =============================================================================
# 3. Radar: design choice -> Sensor + cost
# =============================================================================
REF_RADAR = dict(lam=0.03, P=50.0, tau=0.3, A=2.44 * 0.61, NF=15.0, R=36600.0)   # Mk 13 on a battleship [S/CAL]
K_TAU = 0.07                                      # fraction of pulse length that survives as range noise [CAL]
UNIT_ERR = {"scope": 30.0, "precision": 13.0, "digital": 8.0}       # m [CAL to Mk 3 / Mk 13 / SPG-60]
K_BRG = {"none": 4.0, "lobing": 10.0, "conical": 15.0, "monopulse": 25.0}  # beamwidth / bearing sigma [INFERRED]


@dataclass
class RadarFit:
    tech: RadarTech
    ant_w: float = 2.4          # m antenna width (sets bearing beam)
    ant_h: float = 0.6
    height: float = 30.0
    lam: float = None           # m, default the shortest the tech allows
    P_kW: float = None
    tau_us: float = None

    def __post_init__(self):
        self.lam = self.lam or self.tech.lam_min
        self.P_kW = self.P_kW or self.tech.P_max_kW
        self.tau_us = self.tau_us or self.tech.tau_us

    def beam_deg(self):
        return 70.0 * self.lam / self.ant_w, 70.0 * self.lam / self.ant_h

    def R_max_bb(self):
        """Radar equation relative to the reference set: R^4 ~ P tau A^2 / (lam^2 NF)."""
        A = self.ant_w * self.ant_h * 0.6 / 0.6
        num = self.P_kW * self.tau_us * A * A / self.lam ** 2 / 10 ** (self.tech.NF_dB / 10)
        ref = REF_RADAR["P"] * REF_RADAR["tau"] * REF_RADAR["A"] ** 2 / REF_RADAR["lam"] ** 2 / 10 ** (REF_RADAR["NF"] / 10)
        return REF_RADAR["R"] * (num / ref) ** 0.25

    def rad_a(self):
        return math.hypot(K_TAU * C_LIGHT * self.tau_us * 1e-6 / 2, UNIT_ERR[self.tech.range_unit])

    def sig_brg_mil(self):
        bw = math.radians(self.beam_deg()[0]) * 1000
        return bw / K_BRG[self.tech.bearing]

    def antenna_mass(self):
        return self.tech.ant_kg_m2 * self.ant_w * self.ant_h / 1000.0

    def sensor(self):
        t = self.tech
        per_min = 60.0 if t.auto_track else (20.0 if t.splash == "both" else (10.0 if t.bearing != "none" else 6.0))
        Rm = self.R_max_bb()
        return Sensor(f"radar {self.lam * 100:.0f} cm {self.ant_w:.1f} m", "radar", self.height, per_min,
                      sig_brg_mil=self.sig_brg_mil(), rad_a=self.rad_a(), rad_b=0.0 if t.range_unit == "digital" else 0.001,
                      rad_max_bb=Rm, rad_min=max(200.0, C_LIGHT * self.tau_us * 1e-6 * 1.5),
                      splash=t.splash, splash_max=0.85 * Rm if t.splash != "none" else 0.0,
                      bearing_from_optics=(t.bearing == "none"))

    def cost(self):
        return dict(mass=self.antenna_mass() + self.tech.set_mass_t, z_antenna=self.height,
                    mass_aloft=self.antenna_mass(), crew=3 if not self.tech.auto_track else 2,
                    power_kW=3.0 + 0.04 * self.P_kW)


# =============================================================================
# 4. The whole fire-control fit -> FCLevel for the combat model
# =============================================================================
def range_sensitivity(gun, R, dv=0.01):
    """dR/R per dv/v at range R, from the gun's own ballistics (vacuum would give 2)."""
    el = gun.elev(R)
    r1 = fc._fly(gun.d, gun.m, gun.v0 * (1 + dv), el, gun.i, dt=0.1)[0]
    r0 = fc._fly(gun.d, gun.m, gun.v0, el, gun.i, dt=0.1)[0]
    return (r1 - r0) / r0 / dv


@dataclass
class FCSystemFit:
    computer: ComputerTech
    mount: MountTech
    optics: OpticsTech
    propellant: PropellantTech
    crew: CrewTech
    rfs: list                   # list[RFFit]
    radars: list = field(default_factory=list)   # list[RadarFit]
    spot_height: float = None   # m, highest spotting position (defaults to the highest RF)
    aircraft: bool = False
    name: str = "fit"


def build_level(fit: FCSystemFit, gun, R_ref=15 * KYD, n_guns=9):
    """FCLevel the combat model consumes. salvo_pct is assembled from physical parts:
    salvo-mean muzzle velocity scatter (propellant, n guns, this gun's range sensitivity) and the
    computer/transmission wander (computer tech)."""
    c, m, p = fit.computer, fit.mount, fit.propellant
    S = range_sensitivity(gun, R_ref)
    mv_term = S * p.mv_gun_pct / math.sqrt(max(n_guns, 1))
    salvo_pct = math.hypot(mv_term, c.wander_pct)
    h_spot = fit.spot_height or max([r.height for r in fit.rfs] + [10.0])

    def sensors():
        out = []
        for r in fit.rfs:
            out += rf_sensors(r, fit.optics)
        for rd in fit.radars:
            out.append(rd.sensor())
        if not fit.rfs:
            out.append(fc.S_stadi(h_spot))
        out.append(fc.S_eye(h_spot))          # the officer's estimate is always there as a backstop
        return out

    lvl = FCLevel(fit.name, 0, sensors, c.tracker, c.helm_free and m.director, q=c.q, latency=c.latency,
                  max_rdot=c.max_rdot, max_bdot=c.max_bdot, resid=c.resid, defl_bias_mil=c.defl_bias_mil,
                  lay_mil=m.lay_mil, director=m.director, stabilised=m.stab_vertical,
                  own_turn_kick=c.own_turn_kick, h_spot=h_spot, aircraft=fit.aircraft, salvo_pct=salvo_pct,
                  salvo_defl_mil=m.defl_spot_mil, tau=c.tau, sig_a=c.sig_a, q_turn=c.q_turn, detect=c.detect)
    return lvl


def fit_cost(fit: FCSystemFit, director: DirectorFit = None):
    rows = []
    for r in fit.rfs:
        if r.mount == "director" and director is not None and director.rf is r:
            continue
        rows.append(("RF " + (r.label or f"{r.base:.1f} m"), rf_cost(r, fit.optics)))
    if director is not None:
        rows.append(("director", director_cost(director, fit.optics)))
    for rd in fit.radars:
        cc = rd.cost()
        rows.append(("radar aloft", dict(mass=cc["mass_aloft"], z=rd.height, crew=0)))
        rows.append(("radar office", dict(mass=cc["mass"] - cc["mass_aloft"], z=6.0, crew=cc["crew"])))
    rows.append(("computer", dict(mass=fit.computer.mass_t, z=-2.0, crew=fit.computer.crew)))
    return rows


# =============================================================================
# 5. Calibration check: tech-built levels against the legacy levels
# =============================================================================
def calibration_table():
    print("\n=== D1. Tech-built fits vs legacy levels (same scenario, 8in cruiser, 15 kyd, CA target, n=60) ===")
    g = GUNS["8in/55 Mk 9"]
    print(f"   8in/55 range sensitivity dR/R per dv/v: "
          + "  ".join(f"{k} kyd {range_sensitivity(g, k * KYD):.2f}" for k in (5, 10, 15, 20, 25)))
    sh = Shooter(g, interval=20, speed=30)
    pairs = [
        ("3", FCSystemFit(COMPUTERS["C2"], MOUNTS["M1"], OPTICS["O2"], PROPELLANT["cordite"], CrewTech("t"),
                          [RFFit(2.74, 28, "coinc", "hood", "tripod", 30), RFFit(4.57, 28, "coinc", "hood", "tripod", 25),
                           RFFit(2.74, 28, "coinc", "hood", "turret", 12)], name="tech 3")),
        ("4", FCSystemFit(COMPUTERS["C3"], MOUNTS["M2"], OPTICS["O3"], PROPELLANT["nc"], CrewTech("t"),
                          [RFFit(4.57, 28, "coinc", "director", "tower", 35), RFFit(9.1, 28, "coinc", "turret", "turret", 10)],
                          name="tech 4")),
        ("5", FCSystemFit(COMPUTERS["C4"], MOUNTS["M3"], OPTICS["O4"], PROPELLANT["nc"], CrewTech("t"),
                          [RFFit(8.1, 25, "stereo", "director", "tower", 40)], name="tech 5")),
        ("6", FCSystemFit(COMPUTERS["C4"], MOUNTS["M3"], OPTICS["O4"], PROPELLANT["nc"], CrewTech("t"),
                          [RFFit(8.1, 25, "stereo", "director", "tower", 40)],
                          [RadarFit(RADAR["R3"], 2.44, 0.61, 37)], name="tech 6")),
    ]
    print(f"   {'legacy':>6} {'hit%':>6} {'1st hit s':>9} | {'tech fit':>8} {'hit%':>6} {'1st hit s':>9}  salvo% legacy/tech")
    for key, f in pairs:
        scn = Scenario("cal", sh, TARGETS["CA"], 15 * KYD, tgt_speed=30)
        a = fc.run(scn, fc.LV[key], n=60)
        lv = build_level(f, g)
        b = fc.run(scn, lv, n=60)
        print(f"   {key:>6} {a['pct']:6.2f} {a['t_hit'] or -1:9.0f} | {f.name:>8} {b['pct']:6.2f} {b['t_hit'] or -1:9.0f}"
              f"   {fc.LV[key].salvo_pct:.2f}/{lv.salvo_pct:.2f}")
    print("\n   Radar derivation check (model vs legacy constants):")
    print(f"   {'set':38s} {'beam':>9} {'Rmax BB':>8} {'rad_a':>6} {'brg mil':>7}   legacy Rmax/rad_a/brg")
    checks = [(RadarFit(RADAR["R1"], 3.66, 0.91, 37, lam=0.40, P_kW=15), fc.R_Mk3()),
              (RadarFit(RADAR["R2"], 3.1, 1.0, 37, P_kW=20), fc.R_Mk8()),
              (RadarFit(RADAR["R3"], 2.44, 0.61, 37), fc.R_Mk13()),
              (RadarFit(RADAR["R4"], 1.8, 1.8, 25), fc.R_Mk56()),
              (RadarFit(RADAR["R5"], 2.4, 1.2, 25), fc.R_digital())]
    for rf_, leg in checks:
        s = rf_.sensor()
        bw = rf_.beam_deg()
        print(f"   {rf_.tech.name[:38]:38s} {bw[0]:4.1f}x{bw[1]:3.1f}d {s.rad_max_bb / 1000:7.1f}k {s.rad_a:6.1f} {s.sig_brg_mil:7.1f}"
              f"   {leg.rad_max_bb / 1000:5.1f}k/{leg.rad_a:4.0f}/{leg.sig_brg_mil:4.1f}")


# =============================================================================
# 6. Rangefinder physics tables (fast, analytic)
# =============================================================================
def physics_table():
    print("\n=== D2. One-reading range error (m, 1 sigma, trained operator) by base, optics tier O3, M=25, tower 30 m ===")
    g = GUNS["8in/55 Mk 9"]
    ranges = (8, 10, 12, 15, 18, 20, 25)
    print(f"   {'base':>6} " + " ".join(f"{k:>6}k" for k in ranges) + "    clear / haze(k=1.5)")
    for B in (1.5, 2.74, 3.66, 4.57, 5.5, 6.1, 8.1, 10.0, 12.0, 15.0):
        s = RFSensor(RFFit(B, 25, "stereo", "director", "tower", 30), OPTICS["O3"])
        cl = [s.sigma_range(k * KYD, Conditions(), TARGETS["CA"]) for k in ranges]
        hz = [s.sigma_range(k * KYD, Conditions(k_optic=1.5), TARGETS["CA"]) for k in ranges]
        print(f"   {B:5.2f}m " + " ".join(f"{a:4.0f}/{b:<3.0f}" for a, b in zip(cl, hz)))
    print("\n   For comparison: the things a range error must be small against")
    print(f"   {'range':>6} {'pattern sig (9)':>15} {'hitting space CA':>17} {'danger space':>12} {'salvo wander 1%':>15}")
    for k in ranges:
        R = k * KYD
        sr, _ = g.sigma_D(R, 9)
        dng, dep, wid = fc.hitting_space(TARGETS["CA"], R, g, 60)
        print(f"   {k:5d}k {sr:15.0f} {dng + dep:17.0f} {dng:12.0f} {0.01 * R:15.0f}")
    print("\n=== D3. Magnification: effective M (what the error formula sees) by support and weather, 30 m high ===")
    print(f"   {'nominal M':>9} " + " ".join(f"{s:>14}" for s in ("tower clear", "tower haze", "pole clear", "pole haze", "turret 10m")))
    for M in (12, 15, 20, 25, 28, 32, 40, 50):
        cells = []
        for sup, k, h in (("tower", 1.0, 30), ("tower", 1.5, 30), ("pole", 1.0, 30), ("pole", 1.5, 30), ("turret", 1.0, 10)):
            s = RFSensor(RFFit(5.0, M, "stereo", "director", sup, h), OPTICS["O3"])
            cells.append(f"{s.M_eff(Conditions(k_optic=k)):14.1f}")
        print(f"   {M:9d} " + " ".join(cells))
    print("\n=== D4. Base length the optics can use: effective base (m) by optics tier ===")
    print(f"   {'base':>6} " + " ".join(f"{k:>6}" for k in OPTICS))
    for B in (3, 4.57, 6, 8.1, 10, 12, 15):
        print(f"   {B:5.1f}m " + " ".join(f"{RFSensor(RFFit(B, 25), t).B_eff():6.2f}" for t in OPTICS.values()))


def cost_table():
    print("\n=== D5. Physical cost of rangefinder fits (optics O3) ===")
    print(f"   {'fit':34s} {'inst t':>6} {'fit t':>6} {'z m':>5} {'t.m':>6} {'width':>6} {'crew':>4}")
    rows = []
    for B in (2.74, 4.57, 6.1, 8.1, 10.0, 15.0):
        for mount, arm, sup, h in (("open", 0, "deck", 15), ("hood", 6, "tripod", 25), ("armoured", 25, "tower", 25),
                                   ("turret", 50, "turret", 10)):
            f = RFFit(B, 25, "stereo", mount, sup, h, arm)
            c = rf_cost(f, OPTICS["O3"])
            rows.append((f"{B:5.2f} m {mount:9s} {arm:3.0f} mm", c))
    for name, c in rows:
        print(f"   {name:34s} {c['m_inst']:6.2f} {c['mass']:6.1f} {c['z']:5.0f} {c['mass'] * c['z']:6.0f} {c['rot_diam']:6.1f} {c['crew']:4d}")
    print("\n   Directors carrying the RF (6 mm unarmoured parts, 60 % of plating at the stated armour)")
    print(f"   {'director':40s} {'mass t':>6} {'z':>4} {'t.m':>6} {'width':>6}")
    for B, arm, h, rad in ((4.57, 12.7, 30, None), (4.57, 38, 35, None),
                           (4.57, 12.7, 30, RadarFit(RADAR["R3"], 2.44, 0.61, 31)),
                           (5.5, 12.7, 30, None), (6.1, 25, 30, None), (8.1, 38, 35, None), (10.0, 25, 35, None),
                           (15.0, 50, 40, None)):
        d = DirectorFit(RFFit(B, 25, "stereo", "director", "tower", h), arm, radar=rad)
        c = director_cost(d, OPTICS["O3"])
        tag = f"{B:5.2f} m RF, {arm:4.1f} mm" + (" + radar" if rad else "")
        print(f"   {tag:40s} {c['mass']:6.1f} {c['z']:4.0f} {c['mass'] * c['z']:6.0f} {c['rot_diam']:6.1f}")
    print("   Check: Mk 37 sourced 16 t (0.5 in, DD) / 21 t (1.5 in, BB).")


# =============================================================================
# 7. Monte-Carlo trade study: how big a rangefinder for a cruiser
# =============================================================================
CRUISER_GUN = "8in/55 Mk 9"
SHOOTER = Shooter(GUNS[CRUISER_GUN], turrets=((3, 0, 150), (3, 0, 150), (3, 180, 150)), interval=20.0, speed=30.0)
CONDS = {"clear": Conditions(vis=35000),
         "haze": Conditions(vis=28000, k_optic=1.5),
         "poor": Conditions(vis=20000, k_optic=2.0, low_contrast=True, spot_k=1.3)}
TECH_SETS = {   # computer, mount, optics, propellant
    "plot (C2/M1/O2)": ("C2", "M1", "O2", "cordite"),
    "table (C3/M2/O3)": ("C3", "M2", "O3", "nc"),
    "keeper (C4/M3/O4)": ("C4", "M3", "O4", "nc"),
}


def make_fit(tech, rfs, radars=(), spot_height=None, name="fit"):
    c, m, o, p = TECH_SETS[tech]
    if not OPTICS[o].stereo:      # the designer only offers what the optics tech can make
        rfs = [replace(r, kind="coinc") for r in rfs]
    return FCSystemFit(COMPUTERS[c], MOUNTS[m], OPTICS[o], PROPELLANT[p], CrewTech("trained"), list(rfs), list(radars),
                       spot_height=spot_height, name=name)


def _one(args):
    """Run n engagements; return per-engagement (hits, hits in first 300 s, t first hit, t 3rd hit, shells)."""
    tech, rfs, radars, spot_h, R0, cond, policy, n, seed0 = args
    fit = make_fit(tech, rfs, radars, spot_h)
    lvl = build_level(fit, GUNS[CRUISER_GUN])
    scn = Scenario("rf", SHOOTER, TARGETS["CA"], R0, tgt_speed=30.0, tgt_policy=policy, tgt_amp=25.0,
                   tgt_period=150.0, cond=CONDS[cond], duration=900.0)
    res = []
    for k in range(n):
        o = engage(scn, lvl, seed=seed0 + k)
        t0 = o["t_track"] or 0.0
        ht = sorted(t - t0 for t in o["hit_times"])
        res.append((o["hits"], sum(1 for t in ht if t <= 300), ht[0] if ht else 1e9,
                    ht[2] if len(ht) > 2 else 1e9, o["shells"]))
    return res


def summarise(res, base=None):
    a = np.array(res, dtype=float)
    hits, h5, t1, t3, sh = a.T
    out = dict(hits=hits.mean(), h5=h5.mean(), pct=100 * hits.sum() / max(sh.sum(), 1),
               t1=float(np.median(t1)), t3=float(np.median(t3)))
    if base is not None:
        b = np.array(base, dtype=float)
        # duel: fraction of (A, B) pairings in which A scores its 3rd hit first (ties split)
        ta, tb = a[:, 3][:, None], b[:, 3][None, :]
        out["win"] = float(((ta < tb) + 0.5 * (ta == tb)).mean())
    return out


def rf_for(B, M=25, kind="stereo", h=30, support="tower", count=1):
    return [RFFit(B, M, kind, "director", support, h, label=f"{B:.1f}m", count=count)] if B > 0 else []


BASES = (0.0, 2.74, 3.66, 4.57, 5.5, 6.1, 8.1, 10.0, 15.0)


def size_study(pool, n=100):
    print("\n=== D6. Cruiser rangefinder size: 9 x 8in, 20 s salvos, CA target at 30 kn, optics only ===")
    print("   One RF in the director at 30 m, x25. 0 m = stadimeter + eye only. 15 min engagement.")
    print("   cell = hits in 15 min / hits in first 5 min / median s to first hit / P(win race to 3 hits vs 4.57 m)")
    jobs, keys = [], []
    for tech in TECH_SETS:
        for cond in ("clear", "haze"):
            for pol in ("steady", "zigzag"):
                for k in (10, 15, 20, 25):
                    for B in BASES:
                        keys.append((tech, cond, pol, k, B))
                        jobs.append((tech, rf_for(B), (), 30.0, k * KYD, cond, pol, n, 1000))
    res = dict(zip(keys, pool.map(_one, jobs)))
    for tech in TECH_SETS:
        for cond in ("clear", "haze"):
            for pol in ("steady", "zigzag"):
                print(f"\n   [{tech}] {cond}, target {pol}")
                print(f"   {'base':>6} " + " ".join(f"{k:>22}k" for k in (10, 15, 20, 25)))
                for B in BASES:
                    cells = []
                    for k in (10, 15, 20, 25):
                        s = summarise(res[(tech, cond, pol, k, B)], res[(tech, cond, pol, k, 4.57)])
                        t1 = f"{s['t1']:.0f}" if s["t1"] < 1e8 else "-"
                        cells.append(f"{s['hits']:5.1f}/{s['h5']:4.1f}/{t1:>4}/{s['win']:.2f}")
                    print(f"   {B:5.2f}m " + " ".join(f"{c:>23}" for c in cells))
    return res


def knee(res, tech, cond, k, frac=0.9):
    v = {B: np.mean([summarise(res[(tech, cond, p, k, B)])["hits"] for p in ("steady", "zigzag")]) for B in BASES}
    best = max(v.values())
    return min(B for B in BASES if v[B] >= frac * best)


def knee_table(res):
    """Smallest base that gets within X % of the best base's 15-min hits, per tech, weather and range."""
    print("\n=== D7. Smallest base within 90 % of the best base (15-min hits, mean of steady and zigzag) ===")
    print(f"   {'tech':20s} {'weather':7s} " + " ".join(f"{k:>7}k" for k in (10, 15, 20, 25)))
    for tech in TECH_SETS:
        for cond in ("clear", "haze"):
            cells = []
            for k in (10, 15, 20, 25):
                v = {B: np.mean([summarise(res[(tech, cond, p, k, B)])["hits"] for p in ("steady", "zigzag")]) for B in BASES}
                best = max(v.values())
                knee = min(B for B in BASES if v[B] >= 0.9 * best)
                cells.append(f"{knee:7.2f}m")
            print(f"   {tech:20s} {cond:7s} " + " ".join(cells))


def height_count_mag_study(pool, n=150):
    print("\n=== D8. Same money, different placement: height, count, magnification, kind (table tech, 18 kyd, zigzag) ===")
    print("   cell = hits in 15 min / first 5 min / median s to first hit")
    variants = [
        ("4.57 m x25 director 30 m (reference)", rf_for(4.57), 30),
        ("4.57 m x25 director 20 m", rf_for(4.57, h=20), 20),
        ("4.57 m x25 director 40 m, tower", rf_for(4.57, h=40), 40),
        ("4.57 m x25 director 40 m, pole mast", rf_for(4.57, h=40, support="pole"), 40),
        ("4.57 m x15", rf_for(4.57, M=15), 30),
        ("4.57 m x35", rf_for(4.57, M=35), 30),
        ("4.57 m x50", rf_for(4.57, M=50), 30),
        ("4.57 m coincidence", rf_for(4.57, kind="coinc"), 30),
        ("2 x 4.57 m (director + after control)", rf_for(4.57) + [RFFit(4.57, 25, "stereo", "hood", "deck", 15)], 30),
        ("3 x 4.57 m (+ turret B)", rf_for(4.57) + [RFFit(4.57, 25, "stereo", "hood", "deck", 15),
                                                     RFFit(4.57, 25, "stereo", "turret", "turret", 11)], 30),
        ("8.1 m director 30 m", rf_for(8.1), 30),
        ("4.57 m director + 8.1 m turret RF", rf_for(4.57) + [RFFit(8.1, 25, "stereo", "turret", "turret", 11)], 30),
        ("2.74 m director + 8.1 m turret RF", rf_for(2.74) + [RFFit(8.1, 25, "stereo", "turret", "turret", 11)], 30),
    ]
    jobs, keys = [], []
    for cond in ("clear", "haze"):
        for name, rfs, h in variants:
            keys.append((cond, name))
            jobs.append(("table (C3/M2/O3)", rfs, (), h, 18 * KYD, cond, "zigzag", n, 2000))
    res = dict(zip(keys, pool.map(_one, jobs)))
    print(f"   {'fit':44s} {'clear':>18} {'haze':>18}")
    for name, _, _ in variants:
        cells = []
        for cond in ("clear", "haze"):
            s = summarise(res[(cond, name)])
            t1 = f"{s['t1']:.0f}" if s["t1"] < 1e8 else "-"
            cells.append(f"{s['hits']:5.1f}/{s['h5']:4.1f}/{t1:>4}")
        print(f"   {name:44s} {cells[0]:>18} {cells[1]:>18}")


def radar_study(pool, n=100):
    print("\n=== D9. Does a fire-control radar make the big rangefinder redundant? (keeper tech, zigzag, haze) ===")
    print("   cell = hits in 15 min / first 5 min / median s to first hit")
    r1 = RadarFit(RADAR["R1"], 3.66, 0.91, 30, lam=0.40, P_kW=15)
    r3 = RadarFit(RADAR["R3"], 2.44, 0.61, 30)
    rows = []
    for rname, rad in (("no radar", ()), ("metric R1 (Mk 3 class)", (r1,)), ("X-band R3 (Mk 13 class)", (r3,))):
        for B in (0.0, 2.74, 4.57, 8.1):
            rows.append((rname, B, rad))
    jobs = [("keeper (C4/M3/O4)", rf_for(B), rad, 30.0, k * KYD, "haze", "zigzag", n, 3000)
            for rname, B, rad in rows for k in (12, 18, 24)]
    out = pool.map(_one, jobs)
    i = 0
    print(f"   {'radar':26s} {'base':>6} " + " ".join(f"{k:>18}k" for k in (12, 18, 24)))
    for rname, B, rad in rows:
        cells = []
        for k in (12, 18, 24):
            s = summarise(out[i]); i += 1
            t1 = f"{s['t1']:.0f}" if s["t1"] < 1e8 else "-"
            cells.append(f"{s['hits']:5.1f}/{s['h5']:4.1f}/{t1:>4}")
        print(f"   {rname:26s} {B:5.2f}m " + " ".join(f"{c:>19}" for c in cells))


def value_table(res):
    """Marginal hits per tonne-metre of topweight: what the designer AI should optimise."""
    print("\n=== D10. What a bigger RF buys per unit of topweight (table tech, director 30 m, 12.7 mm director) ===")
    print("   Δhits = extra hits in 15 min over the next-smaller base, averaged over steady/zigzag and clear/haze")
    print(f"   {'base':>6} {'dir t':>6} {'t.m':>6} {'GM loss mm (10 kt CA, KG 6.5)':>30} "
          + " ".join(f"{'Δhits ' + str(k) + 'k':>10}" for k in (10, 15, 20, 25)))
    prev = None
    for B in BASES[1:]:
        d = director_cost(DirectorFit(RFFit(B, 25, "stereo", "director", "tower", 30)), OPTICS["O3"])
        tm = d["mass"] * (30 - 6.5)
        gm = 1000 * d["mass"] * (30 - 6.5) / 10000
        cells = []
        for k in (10, 15, 20, 25):
            v = np.mean([summarise(res[("table (C3/M2/O3)", c, p, k, B)])["hits"]
                         for c in ("clear", "haze") for p in ("steady", "zigzag")])
            pv = np.mean([summarise(res[("table (C3/M2/O3)", c, p, k, prev if prev is not None else 0.0)])["hits"]
                          for c in ("clear", "haze") for p in ("steady", "zigzag")])
            cells.append(f"{v - pv:10.1f}")
        print(f"   {B:5.2f}m {d['mass']:6.1f} {tm:6.0f} {gm:30.0f} " + " ".join(cells))
        prev = B


def rule_b(R, M=25, tech=None, k=1.0):
    """Base whose clear-weather unit of error at range R equals k x the 9-gun salvo's range sigma."""
    tech = tech or OPTICS["O3"]
    sr, _ = GUNS[CRUISER_GUN].sigma_D(R, 9)
    for b in np.arange(0.5, 40.0, 0.05):
        if RFSensor(RFFit(b, M, "stereo", "director", "tower", 30), tech).unit_error(R) <= k * sr:
            return float(b)
    return float("nan")


def rule_of_thumb(res=None):
    """Analytic sizing rule checked against the Monte-Carlo knee (D7)."""
    print("\n=== D11. Sizing rule: one unit of error at the design range = one salvo range-sigma (k=1) ===")
    print("   (optics O3, x25, clear air, 8in 9-gun salvo; knee = D7 smallest base within 90 % of the best)")
    print(f"   {'range':>6} {'pattern sig':>11} {'k=0.7':>7} {'k=1':>7} {'k=1.4':>7}   knee table / keeper (clear, haze)")
    for kk in (10, 12, 15, 18, 20, 25):
        R = kk * KYD
        sr, _ = GUNS[CRUISER_GUN].sigma_D(R, 9)
        line = f"   {kk:5d}k {sr:10.0f}m " + " ".join(f"{rule_b(R, k=k):6.1f}m" for k in (0.7, 1.0, 1.4))
        if res is not None and kk in (10, 15, 20, 25):
            line += "   " + " ".join(f"{knee(res, t, c, kk):5.2f}" for t in ("table (C3/M2/O3)", "keeper (C4/M3/O4)")
                                     for c in ("clear", "haze"))
        print(line)


# =============================================================================
# 8. Schema print (what tech outputs, what the designer picks, what combat consumes)
# =============================================================================
SCHEMA = [
    # (group, field, unit, typical range, consumed by, used as)
    ("OpticsTech", "delta_arcsec", "arcsec", "10-16", "combat", "RF error: delta R^2 / (B_eff M_eff)"),
    ("OpticsTech", "rigid_base", "m", "2-6", "combat", "B_eff = rigid (B/rigid)^flex_exp above it"),
    ("OpticsTech", "flex_exp", "-", "0.6-0.85", "combat", "as above"),
    ("OpticsTech", "M_max", "x", "15-30", "designer+combat", "max sensible M; image dims beyond"),
    ("OpticsTech", "stereo", "bool", "", "designer", "stereo instruments selectable (needs stereo_pool crews)"),
    ("OpticsTech", "coated", "bool", "", "combat", "haze/dusk multiplier 0.8 on excess"),
    ("OpticsTech", "mass_k", "kg/m^2", "25-30", "designer", "instrument mass = mass_k B^2"),
    ("OpticsTech", "gyro_stab", "bool", "", "combat", "readings/min x1.5, vibration limit x1.5"),
    ("RFFit", "base, mag, kind, mount, support, height, armour_mm, count", "", "", "designer", "player choice"),
    ("RadarTech", "lam_min, P_max_kW, tau_us, NF_dB", "m, kW, us, dB", "", "combat", "R_max, range sigma, rad_min"),
    ("RadarTech", "bearing", "enum", "none/lobing/conical/monopulse", "combat", "sig_brg = beamwidth / K_BRG"),
    ("RadarTech", "range_unit", "enum", "scope/precision/digital", "combat", "range sigma floor 30/13/8 m"),
    ("RadarTech", "splash, auto_track", "", "", "combat", "radar spotting; 60 readings/min"),
    ("RadarTech", "set_mass_t, ant_kg_m2", "t, kg/m^2", "1.5-2.5, 40", "designer", "office + aloft mass"),
    ("RadarFit", "ant_w, ant_h, height (lam, P, tau)", "m", "", "designer", "beam 70 lam/D; R^4 ~ P tau A^2/(lam^2 NF)"),
    ("ComputerTech", "tracker", "enum", "polar/cart/ca/ct", "combat", "what target motion the computer can follow"),
    ("ComputerTech", "latency, q, own_turn_kick, max_rdot/bdot", "s, m2/s3, m", "", "combat", "tracker"),
    ("ComputerTech", "wander_pct, resid, defl_bias_mil", "%, -, mil", "", "combat", "salvo wander; pre-spot bias"),
    ("ComputerTech", "mass_t, crew, power_kW", "", "", "designer", "below armour (plotting room)"),
    ("MountTech", "director, stab_vertical, lay_mil, defl_spot_mil", "", "", "combat", "laying chain"),
    ("PropellantTech", "mv_gun_pct, smoke", "%", "0.3-1.0", "combat", "salvo_pct via range_sensitivity; smoke"),
    ("CrewTech", "operator, stereo_pool, spot_k", "", "", "combat+designer", "rangetaker/spotter multipliers"),
    ("derived", "range_sensitivity(gun, R)", "-", "1.3-2", "combat", "dR/R per dv/v from the gun's ballistics"),
]


def schema_table():
    print("\n=== D0. Parameter schema ===")
    for row in SCHEMA:
        print("   " + " | ".join(row))


if __name__ == "__main__":
    arg = sys.argv[1] if len(sys.argv) > 1 else "all"
    schema_table()
    physics_table()
    cost_table()
    if arg == "physics":
        sys.exit()
    calibration_table()
    with Pool(2) as pool:
        res = size_study(pool)
        knee_table(res)
        rule_of_thumb(res)
        value_table(res)
        height_count_mag_study(pool)
        radar_study(pool)
