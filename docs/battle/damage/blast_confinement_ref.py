"""blast_confinement_ref.py -- reference for 11_blast_confinement.md (Fleetwright damage research).

What a burst does to structure, crew and equipment: in the open (shock and fragments only) versus inside a
space (shock plus the trapped gas, the quasi-static pressure, cascading through the boundaries that give way).

Pure Python, no dependencies. `python blast_confinement_ref.py` prints every table quoted in the note.
Units: kg, m, m^2, m^3, MPa, ms. W is always the filler's own mass in kg (not TNT-equivalent): the explosive's
block carries the energies. Tags in comments: [SRC] sourced, [FIT] fitted here to the cases, [EST] judgement.
"""
import math

P0 = 0.101325  # MPa, ambient

# ---------------------------------------------------------------------------------------------------------
# 1. Explosives: the numbers a confined burst needs.
#   qd  heat of detonation, MJ/kg         (drives the shock and the oxygen-starved QSP)
#   qc  heat of combustion, MJ/kg         (qd + afterburning of the products in the room's air)
#   air m^3 of air to burn 1 kg's products fully (oxygen balance); 0 = nothing left to burn
#   re  blast TNT equivalence (shock, fragments): what `filler.re` already is
#   eta share of the energy that ends up as gas pressure (black powder: ~half its products are solids)
# TNT measured (Kuhl/LLNL: 4.57 / 14.96); others from oxygen balance and Meyer [EST where marked '?'].
EXPLOSIVES = {
    #  name           qd    qc    air   re    eta
    "TNT":          (4.5, 15.0, 2.66, 1.00, 1.00),   # [SRC]
    "TNA":          (4.4, 13.6, 2.25, 1.00, 1.00),   # trinitroanisole, IJN Type 91/0 ?
    "Lyddite":      (4.4, 11.4, 1.63, 1.12, 1.00),   # picric acid; shimose ?
    "Shellite":     (4.2, 12.3, 2.00, 1.05, 1.00),   # 70/30 picric/dinitrophenol ?
    "ExplosiveD":   (3.2, 11.5, 1.87, 0.90, 1.00),   # ammonium picrate, USN AP ?
    "Amatol80":     (4.0,  4.0, 0.00, 1.00, 1.00),   # oxygen-balanced: nothing to afterburn ?
    "RDX/TNT":      (5.0, 12.0, 1.70, 1.35, 1.00),   # Comp B-like ?
    "BlackPowder":  (2.9,  2.9, 0.00, 0.40, 0.55),   # deflagrates; 56% of products solid [SRC/EST]
}

ETA_GAMMA = 0.28  # eta_heatloss * (gamma-1) = 0.7 * 0.4, fitted to DDESB TNT 0.21 MPa at 0.0497 kg/m^3 [FIT]


def afterburn_share(expl, w, v_air):
    """Share of the afterburning energy released: 1 with ample air, falling as the air runs short.
    Smooth form fitted between DDESB (full at 0.05 kg/m^3) and the cabin and SwRI fits at 0.3-1 kg/m^3 [FIT]."""
    air = EXPLOSIVES[expl][2]
    if air <= 0 or v_air <= 0:
        return 0.0
    return 1.0 / (1.0 + w * air / v_air)


def qsp(expl, w, v, v_air=None):
    """Quasi-static gas pressure (gauge, MPa) of w kg of filler in a closed volume v (m^3).
    v_air: the air left in it (flooded or packed spaces have less); defaults to v."""
    qd, qc, _, _, eta = EXPLOSIVES[expl]
    v_air = v if v_air is None else v_air
    phi = afterburn_share(expl, w, v_air)
    return ETA_GAMMA * eta * (qd + phi * (qc - qd)) * w / v


# The published fits, for the comparison table (W TNT kg, V m^3, MPa).
def qsp_ufc(wv):    return 2.25 * wv ** 0.72        # UFC 3-340-02 fig 2-152, converted [SRC, secondary]
def qsp_swri(wv):   return 2.341 * wv ** 0.8395     # SwRI/Baker 177 tests, W/V <= 0.4 [SRC]
def qsp_cabin(wv):  return 1.45 * wv ** 0.676       # Zhang 2024 steel cabin, 0.015-0.48 [SRC]


# ---------------------------------------------------------------------------------------------------------
# 2. Venting: how long the pressure lasts, and what the structure feels.
VENT_C = 725.0        # m/s: Kinney-Sewell blowdown constant 2.13*a0 [SRC]
VENT_ALPHA = 0.6      # orifice coefficient [SRC range 0.6-1]
BURN_STRETCH = 2.0    # measured blowdowns last ~2x the isentropic estimate (afterburning goes on) [SRC FRANG]
T_PANEL_MS = 15.0     # natural period of a stiffened ship panel, 10-30 ms [EST]
DLF = 1.3             # load factor on a long pulse: 2 for a step, less for a 10-15 ms rise [EST]
OPEN_RATIO = 0.6      # alpha*A / V^(2/3) above which no gas pressure builds at all [SRC Keenan & Tancreto]


def blowdown_ms(p_g, v, a_vent):
    """Duration (ms) of the gas pressure p_g (gauge MPa) in volume v with vent area a_vent (m^2)."""
    if a_vent <= 0:
        return math.inf
    return 1000.0 * BURN_STRETCH * v / (VENT_C * VENT_ALPHA * a_vent) * math.log((p_g + P0) / P0)


def effective_load(p_g, v, a_vent):
    """The static-equivalent pressure a boundary feels: DLF x QSP for a long pulse, the impulse x omega for a short one."""
    if a_vent > 0 and VENT_ALPHA * a_vent >= OPEN_RATIO * v ** (2 / 3):
        return 0.0, 0.0
    tb = blowdown_ms(p_g, v, a_vent)
    return p_g * min(DLF, math.pi * tb / T_PANEL_MS), tb


# ---------------------------------------------------------------------------------------------------------
# 3. Boundaries: what holds, what deforms, what goes.
# Capacity per mm of plate, MPa/mm [FIT to Boise (9 mm magazine bulkhead held 0.41-0.48 MPa, yielded ~0.09),
# the WDR 51 / Decoy radii and the plastic-collapse estimates; joints, not plate, fail first].
C_RUPTURE = {"wt": 0.060, "deck": 0.060, "side": 0.060, "joiner": 0.025, "armour": 0.060}
C_DEFORM = 0.012          # dished, seams sprung, doors jammed: leaks, not openings
DOOR_SHARE = 0.5          # a WT door or hatch goes at half its bulkhead's rupture pressure (min below)
DOOR_MIN = 0.08
DOOR_AREA = 1.2           # m^2 opened when a door or hatch is blown
RIVETED = 0.85            # riveted joints vs welded (Formidable: the riveted connection went) [EST]


class Boundary:
    def __init__(self, kind, t_mm, area, to, door=False, riveted=True):
        self.kind, self.t, self.area, self.to, self.door, self.riveted = kind, t_mm, area, to, door, riveted
        self.state = "ok"     # ok, deformed, door_blown, ruptured

    def rupture(self):
        return C_RUPTURE[self.kind] * self.t * (RIVETED if self.riveted else 1.0)

    def door_cap(self):
        return max(DOOR_MIN, DOOR_SHARE * self.rupture())

    def deform(self):
        return C_DEFORM * self.t


class Space:
    def __init__(self, name, v, a_vent=0.0, air_share=1.0, crew=0):
        self.name, self.v, self.a_vent, self.air, self.crew = name, v, a_vent, air_share, crew
        self.bounds = []


OUTSIDE = ("air", "sea")


def cascade(spaces, burst, expl, w, log=None):
    """The burst's gas spreads through whatever gives way. Each round: the pressure of the region the gas fills,
    its effective load on every boundary of the region, and every boundary over its limit fails at once.
    A failed boundary to another space adds that space to the region; one to the air or sea vents it.
    Ends when nothing more fails. Returns (rounds, region, boundaries touched)."""
    region = {burst}
    a_vent = spaces[burst].a_vent
    rounds = []
    while True:
        v = sum(spaces[s].v for s in region)
        v_air = sum(spaces[s].v * spaces[s].air for s in region)
        p = qsp(expl, w, v, v_air)
        pe, tb = effective_load(p, v, a_vent)
        rounds.append(dict(region=sorted(region), v=v, p=p, pe=pe, tb=tb))
        new = []
        for s in list(region):
            for b in spaces[s].bounds:
                if b.state == "ruptured" or b.to in region:
                    continue
                if pe >= b.rupture():
                    b.state = "ruptured"; new.append((s, b, b.area))
                elif b.door and b.state != "door_blown" and pe >= b.door_cap():
                    b.state = "door_blown"; new.append((s, b, DOOR_AREA))
                elif pe >= b.deform() and b.state == "ok":
                    b.state = "deformed"
        if not new:
            break
        for s, b, a in new:
            if b.to in OUTSIDE:
                a_vent += a
            else:
                region.add(b.to)
                a_vent += spaces[b.to].a_vent
    return rounds, region


# ---------------------------------------------------------------------------------------------------------
# 4. People and things in the gas.
def lethal_share(p):
    """Long-duration pressure on a man in a closed space: Bowen near a reflecting surface, 50% at 0.42 MPa,
    1% at 0.28 (lung); probit in log pressure [SRC Bowen 1968]."""
    if p <= 0:
        return 0.0
    return 0.5 * (1 + math.erf(math.log(p / 0.42) / (0.174 * math.sqrt(2))))


def disabled_share(p):
    """Out of action (eardrums, lungs, concussion): 50% near 0.12 MPa, few below 0.035 [SRC eardrum 0.10, lung 0.085]."""
    if p <= 0:
        return 0.0
    return 0.5 * (1 + math.erf(math.log(p / 0.12) / (0.5 * math.sqrt(2))))


FRAGILITY = {  # MPa of gas pressure that puts a part out of action [EST, from the WDR trips and wrecks]
    "fittings": 0.03,     # joiner work, lockers, light ducts, fire curtains
    "electrical": 0.10,   # switchboards, instruments, fire-control gear (South Dakota's trips)
    "auxiliary": 0.25,    # pumps, light piping, uptakes, hoist trunks (Boise's hoists burst ~0.48)
    "machinery": 0.60,    # boilers, turbines, gearing
}


def fumes_ppm(expl, w, v_region, v_air):
    """CO left after the burst: TNT-type fills make ~0.35 m^3 CO per kg; afterburning burns it [SRC KW rule]."""
    qd, qc, air, _, _ = EXPLOSIVES[expl]
    if air <= 0:
        return 0.0
    co_m3 = 0.345 * w * (air / 2.66) * (1 - afterburn_share(expl, w, v_air))
    return 1e6 * co_m3 / v_region


# ---------------------------------------------------------------------------------------------------------
# 5. Open air: the shock alone (Kingery-Bulmash hemispherical, Swisdak's fits [SRC]).
def _kb(z, bands):
    for lo, hi, c in bands:
        if lo <= z <= hi:
            lz = math.log(z)
            return math.exp(sum(ci * lz ** i for i, ci in enumerate(c)))
    return None


KB_PSO = [(0.2, 2.9, (7.2106, -2.1069, -0.3229, 0.1117, 0.0685)),
          (2.9, 23.8, (7.5938, -3.0523, 0.40977, 0.0261, -0.01267)),
          (23.8, 198.5, (6.0536, -1.4066))]
KB_IR = [(0.06, 40, (6.7853, -1.3466, 0.101, -0.01123))]


def pso_kpa(r, w_tnt):
    return _kb(r / w_tnt ** (1 / 3), KB_PSO)


def ir_kpa_ms(r, w_tnt):
    v = _kb(r / w_tnt ** (1 / 3), KB_IR)
    return None if v is None else v * w_tnt ** (1 / 3)


def radius_where(f, target, w, lo=0.05, hi=200.0):
    """Largest r with f(r, w) >= target (f falls with r), by bisection in log r."""
    lo = max(lo, 0.21 * w ** (1 / 3))     # the fits start at Z ~0.2
    if f(lo, w) is None or f(lo, w) < target:
        return 0.0
    for _ in range(60):
        mid = math.sqrt(lo * hi)
        val = f(mid, w)
        if val is not None and val >= target:
            lo = mid
        else:
            hi = mid
    return lo


def open_radii(w_tnt):
    """Shock-only radii on an open deck (m). Fragments are the fragment model's; these are what the blast adds."""
    tear_6mm = 3.0 * 6 * 1000      # kPa.ms: Nurick tearing impulse ~3 kPa.s per mm of mild steel [SRC derived]
    tear_2mm = 3.0 * 2 * 1000
    return dict(
        tear_6mm=radius_where(ir_kpa_ms, tear_6mm, w_tnt),
        tear_2mm=radius_where(ir_kpa_ms, tear_2mm, w_tnt),
        lethal=radius_where(pso_kpa, 700.0, w_tnt),     # short pulse: 50% lung lethality needs ~0.6-0.8 MPa incident
        disabled=radius_where(pso_kpa, 100.0, w_tnt),   # eardrums, concussion
        fittings=radius_where(pso_kpa, 70.0, w_tnt),
    )


# ---------------------------------------------------------------------------------------------------------
# 6. Contact holes (kept from 08 §3.6, now checked on 13 cases): R = 0.45 W^(1/3) sqrt(6/t).
def hole_radius(w_tnt, t_mm, k=0.45, n=0.5):
    return k * w_tnt ** (1 / 3) * (6.0 / t_mm) ** n


# ---------------------------------------------------------------------------------------------------------
# 7. Ships for the benches: a run of compartments along the ship, with the deck above, the deck below, the side.
def chain(n, v_each, t_bulk, t_deck_above, t_deck_below, t_side, side_below_share=0.5, burst_idx=None,
          door=True, joiner_rooms=0, a_vent=0.0, height=3.0, beam=11.0, crew=10, riveted=False):
    """n compartments in a row (index 0..n-1); the burst one may be split into joiner rooms first."""
    spaces = {}
    length = v_each / (height * beam)
    for i in range(n):
        s = Space(f"c{i}", v_each, a_vent=a_vent if i == burst_idx else 0.0, crew=crew)
        spaces[s.name] = s
    for i in range(n):
        s = spaces[f"c{i}"]
        if i > 0:
            s.bounds.append(Boundary("wt", t_bulk, height * beam, f"c{i-1}", door=door, riveted=riveted))
        if i < n - 1:
            s.bounds.append(Boundary("wt", t_bulk, height * beam, f"c{i+1}", door=door, riveted=riveted))
        s.bounds.append(Boundary("deck", t_deck_above, length * beam, f"above{i}", door=door, riveted=riveted))
        s.bounds.append(Boundary("deck", t_deck_below, length * beam, f"below{i}", door=door, riveted=riveted))
        s.bounds.append(Boundary("side", t_side, length * height * 2 * (1 - side_below_share), "air", riveted=riveted))
        s.bounds.append(Boundary("side", t_side, length * height * 2 * side_below_share, "sea", riveted=riveted))
        spaces[f"above{i}"] = Space(f"above{i}", v_each, crew=crew)
        spaces[f"below{i}"] = Space(f"below{i}", v_each, crew=crew)
    if joiner_rooms and burst_idx is not None:
        # the burst compartment is cut into rooms by light partitions: the burst is in room 0
        host = spaces[f"c{burst_idx}"]
        room = Space(f"c{burst_idx}r", host.v / joiner_rooms, a_vent=host.a_vent, crew=crew)
        host.v -= room.v
        room.bounds = [Boundary("joiner", 2.0, height * beam / 2, host.name, door=False)]
        room.bounds += [Boundary(b.kind, b.t, b.area / joiner_rooms, b.to, door=False) for b in host.bounds]
        spaces[room.name] = room
    return spaces


def describe(spaces, rounds, region, expl, w, burst):
    last = rounds[-1]
    gone = [(s, b) for s in spaces for b in spaces[s].bounds if b.state == "ruptured"]
    doors = [(s, b) for s in spaces for b in spaces[s].bounds if b.state == "door_blown"]
    bent = [(s, b) for s in spaces for b in spaces[s].bounds if b.state == "deformed"]
    first = rounds[0]
    kinds = {}
    for s, b in gone:
        key = "sea" if b.to == "sea" else "air" if b.to == "air" else b.kind
        kinds[key] = kinds.get(key, 0) + 1
    return dict(p0=first["p"], pe0=first["pe"], p_end=last["p"], v_end=last["v"], spaces=len(region),
                ruptured=kinds, doors=len(doors), deformed=len(bent),
                lethal0=lethal_share(first["pe"] / DLF if first["pe"] else 0), disabled0=disabled_share(first["p"]),
                lethal_end=lethal_share(last["p"]), fumes=fumes_ppm(expl, w, last["v"], last["v"]))


# ---------------------------------------------------------------------------------------------------------
# 8. The benches.
def table_qsp():
    print("\n## QSP, TNT, by charge per volume (MPa gauge)\n")
    print("| W/V kg/m3 | this model | UFC 3-340 | SwRI | steel cabin | linear, no afterburn |")
    print("|---|---|---|---|---|---|")
    for wv in (0.003, 0.01, 0.03, 0.05, 0.1, 0.3, 1.0):
        m = qsp("TNT", wv * 100, 100)
        print(f"| {wv} | {m:.3f} | {qsp_ufc(wv):.3f} | {qsp_swri(wv):.3f} | {qsp_cabin(wv):.3f} | {ETA_GAMMA*4.5*wv:.3f} |")
    print("\nDDESB measured (TNT, fully confined): 0.0497 kg/m3 -> 0.210 MPa; 0.0256 -> 0.122; 0.0224 -> 0.104")
    for wv, meas in ((0.0497, 0.210), (0.0256, 0.122), (0.0224, 0.104)):
        print(f"  model {qsp('TNT', wv*100, 100):.3f} vs {meas}")


def table_fillers():
    print("\n## The same 10 kg in 100 m3 and in 20 m3, by filler (MPa)\n")
    print("| filler | 100 m3 | 20 m3 | afterburn share at 20 m3 |")
    print("|---|---|---|---|")
    for e in EXPLOSIVES:
        print(f"| {e} | {qsp(e, 10, 100):.3f} | {qsp(e, 10, 20):.3f} | {afterburn_share(e, 10, 20):.2f} |")


def table_open_vs_closed():
    print("\n## Open deck versus a closed space, per shell (radii m; pressures MPa)\n")
    print("| shell (filler kg) | open: 6 mm torn / 2 mm torn / lethal / disabled | closed 50 m3 | 150 m3 | 600 m3 |")
    print("|---|---|---|---|---|")
    shells = [("4in HE (1.0 TNT)", "TNT", 1.0), ("5in/38 AAC (3.3 D)", "ExplosiveD", 3.3),
              ("6in HC (6.0 D)", "ExplosiveD", 6.0), ("8in HC (9.7 D)", "ExplosiveD", 9.7),
              ("12in Jap. HE 1905 (39 Lydd.)", "Lyddite", 39.0), ("15in APC 1916 (27 Lydd.)", "Lyddite", 27.4),
              ("16in AP Mk8 (18.6 D)", "ExplosiveD", 18.55), ("16in HC Mk13 (69.7 D)", "ExplosiveD", 69.7),
              ("46cm T0 HE (61.7 TNA)", "TNA", 61.7), ("15in CPC (58.6 BP)", "BlackPowder", 58.6)]
    for name, e, w in shells:
        wt = w * EXPLOSIVES[e][3]
        o = open_radii(wt)
        cells = []
        for v in (50, 150, 600):
            p = qsp(e, w, v)
            cells.append(f"{p:.2f} ({lethal_share(p)*100:.0f}% dead, {disabled_share(p)*100:.0f}% out)")
        print(f"| {name} | {o['tear_6mm']:.1f} / {o['tear_2mm']:.1f} / {o['lethal']:.1f} / {o['disabled']:.1f} | "
              + " | ".join(cells) + " |")


CASES = [
    # name, builder kwargs, explosive, w, what history says
    ("5in AAC in a DD compartment", dict(n=5, v_each=150, t_bulk=5, t_deck_above=8, t_deck_below=5, t_side=8,
                                          burst_idx=2), "ExplosiveD", 3.3,
     "WDR 51: shell blast minor; Sterett 11 hits survived"),
    ("8in HC in a DD compartment", dict(n=5, v_each=150, t_bulk=5, t_deck_above=8, t_deck_below=5, t_side=8,
                                         burst_idx=2), "ExplosiveD", 9.7,
     "Ralph Talbot 5x8in: bulkhead holes 3-5 ft, compartments wrecked, survived"),
    ("14in HC in a DD engine room", dict(n=5, v_each=600, t_bulk=6, t_deck_above=9, t_deck_below=6, t_side=9,
                                          burst_idx=2, height=5.5), "ExplosiveD", 47.3,
     "S.B. Roberts (DE): one 14in salvo, 40x10 ft hole in No.2 engine room, lost"),
    ("46cm T0 HE in a DD engine room", dict(n=5, v_each=600, t_bulk=6, t_deck_above=9, t_deck_below=6, t_side=9,
                                             burst_idx=2, height=5.5), "TNA", 61.7,
     "08: wrecks the compartment and often the next"),
    ("250kg SAP (60 kg) in a DD space", dict(n=5, v_each=200, t_bulk=5, t_deck_above=8, t_deck_below=5, t_side=8,
                                              burst_idx=2), "TNT", 60.0,
     "WDR 51: 500 lb SAP carries away several adjacent decks and bulkheads; little beyond ~6 m"),
    ("Decoy: 50kg bomb (25 kg)", dict(n=5, v_each=150, t_bulk=5, t_deck_above=8, t_deck_below=5, t_side=8,
                                       burst_idx=2), "TNT", 25.0, "structural damage within ~3.7 m"),
    ("Boise hit 6: 8in AP in a magazine", dict(n=5, v_each=150, t_bulk=9, t_deck_above=19, t_deck_below=9,
                                                t_side=16, burst_idx=2, riveted=True, side_below_share=1.0), "TNA", 3.11,
     "bulkhead deflected 2 in, doors dished 3-4 in (before the powder burned)"),
    ("Marblehead A: 100kg GP in steering room", dict(n=5, v_each=120, t_bulk=8, t_deck_above=8, t_deck_below=8,
                                                     t_side=10, burst_idx=2, riveted=True), "TNT", 45.0,
     "decks holed 8x10 and 5x6 ft, main deck torn and lifted 6 ft, doors blown 2 bulkheads away"),
    ("Savannah: Fritz X in handling room", dict(n=7, v_each=350, t_bulk=10, t_deck_above=25, t_deck_below=50,
                                                t_side=16, burst_idx=3, side_below_share=1.0, height=4.0,
                                                beam=18, riveted=False), "Amatol80", 270.0,
     "bulkheads demolished within 4.6-6 m, held at 6-8 m, doors failed over 27 m, bottom opened, flooded 46 m"),
    ("Baden: 15in CPC (BP) in the end", dict(n=5, v_each=300, t_bulk=8, t_deck_above=10, t_deck_below=10,
                                              t_side=12, burst_idx=2, riveted=True, side_below_share=0.0), "BlackPowder", 58.6,
     "upper deck lifted 4 ft 6 in, 43 ft torn from the side; main deck holed 16 x 4.5 ft"),
    ("Baden, the same shell TNT-filled", dict(n=5, v_each=300, t_bulk=8, t_deck_above=10, t_deck_below=10,
                                               t_side=12, burst_idx=2, riveted=True, side_below_share=0.0), "TNT", 58.6,
     "(for contrast)"),
    ("Exeter: 28cm HE in the CPO flat", dict(n=5, v_each=150, t_bulk=8, t_deck_above=10, t_deck_below=10,
                                              t_side=12, burst_idx=2, riveted=True, side_below_share=0.0), "TNT", 23.0,
     "nearly all men there killed, mostly by blast; mains fractured"),
    ("16in AP Mk 8 in a BB engine room", dict(n=5, v_each=1500, t_bulk=12, t_deck_above=50, t_deck_below=12,
                                               t_side=25, burst_idx=2, height=7, beam=25), "ExplosiveD", 18.55,
     "Scharnhorst: 14in in No.1 boiler room, speed 31 -> 8-10 kn (steam), recovered"),
    ("Kirishima: 16in AP in a casemate", dict(n=5, v_each=400, t_bulk=8, t_deck_above=8, t_deck_below=19,
                                               t_side=25, burst_idx=2, riveted=True, a_vent=4.0, side_below_share=0.0),
     "ExplosiveD", 18.55, "holes ~10 m across in the decks (survivor's sketch)"),
    ("6in HC in a cabin flat (joiner rooms)", dict(n=5, v_each=150, t_bulk=8, t_deck_above=10, t_deck_below=10,
                                                    t_side=12, burst_idx=2, joiner_rooms=3, side_below_share=0.0),
     "ExplosiveD", 6.0, "Boise hit 2: captain's cabin wrecked, a longitudinal bulkhead 15 ft inboard demolished"),
]


def run_cases():
    print("\n## The cascade against the cases\n")
    print("| case | first P / felt | spaces reached | ruptured | doors | deformed | dead/out in burst space | CO ppm | history |")
    print("|---|---|---|---|---|---|---|---|---|")
    for name, kw, e, w, hist in CASES:
        burst = f"c{kw['burst_idx']}" + ("r" if kw.get("joiner_rooms") else "")
        sp = chain(**kw)
        rounds, region = cascade(sp, burst, e, w)
        d = describe(sp, rounds, region, e, w, burst)
        rup = ", ".join(f"{k} {v}" for k, v in sorted(d["ruptured"].items())) or "none"
        print(f"| {name} | {d['p0']:.2f} / {d['pe0']:.2f} | {d['spaces']} ({d['v_end']:.0f} m3, ends {d['p_end']:.2f}) | {rup} | "
              f"{d['doors']} | {d['deformed']} | {lethal_share(d['p0'])*100:.0f}% / {disabled_share(d['p0'])*100:.0f}% | "
              f"{d['fumes']:.0f} | {hist} |")


def table_holes():
    print("\n## Contact holes: 0.45 W^(1/3) sqrt(6/t) (m radius)\n")
    for w in (1, 3, 10, 30, 60):
        print(w, " ".join(f"{t}mm:{hole_radius(w, t):.2f}" for t in (3, 6, 12, 25, 50)))


if __name__ == "__main__":
    table_qsp()
    table_fillers()
    table_open_vs_closed()
    run_cases()
    table_holes()
