"""Own ship's motion into the fire-control table: the sizes of the errors her turns put in.

Companion to own-motion-inputs.md (research/simulation/ship-motion/). Plain Python, no dependencies:

    python3 own_motion_ref.py

What it computes, each against FL1 (docs/battle/fc-ladder-check.md) as built:
  1. the gyrocompass's speed (steaming) error and how much it changes in a zigzag and a 90-degree turn;
  2. the ballistic deflection's residual after the change (a compass tuned to Schuler's period at one latitude, used
     at another), and how it decays;
  3. how much of a gyro error reaches a helm-free table: a frame rotation common to her course and every bearing, so
     only its rate of change over the table's prediction horizon counts;
  4. the speed lag: a hard turn's speed loss through a first-order lag (hand-fed log, pitometer), as the rate error the
     table carries and the aim error over the horizon;
  5. what a course lag, a step-by-step transmission's step and a ship's sideslip in a turn would do, for scale.
"""
import math

KN = 1852.0 / 3600.0            # m/s per knot
OMEGA = 7.2921e-5               # earth's rotation, rad/s
R_E = 6.371e6                   # m
G = 9.81
T_SCHULER_MIN = 2 * math.pi * math.sqrt(R_E / G) / 60.0   # 84.4 min
EQ_KN = OMEGA * R_E / KN        # the surface's speed at the equator, ~903 kn


def speed_error_deg(v_kn, course_deg, lat_deg):
    """The gyrocompass's settled steaming error: its north tilted toward the meridian of the ship's velocity
    relative to the earth's surface, tan d = V cos C / (Omega R cos lat + V sin C). Positive: the compass reads
    high (westerly error) on northerly courses."""
    c = math.radians(course_deg)
    return math.degrees(math.atan2(v_kn * math.cos(c), EQ_KN * math.cos(math.radians(lat_deg)) + v_kn * math.sin(c)))


def ballistic_residual_fraction(lat_deg, design_lat_deg):
    """The ballistic deflection after a change dV_N of north-south speed is M dV_N / H, the same at every latitude
    (Chalmers 1920: 'the same at the equator as in 60 deg.'), while the change in the settled speed error it should
    match is dV_N / (Omega R cos lat). Matched at design_lat (period 84.4 min there: dead-beat), the part of the change
    left as error at lat is 1 - cos(lat)/cos(design) (positive: under-deflected, poleward of the design latitude)."""
    return 1 - math.cos(math.radians(lat_deg)) / math.cos(math.radians(design_lat_deg))


def decayed(amplitude_deg, t_min, period_min=85.0, swing_ratio=1 / 3.0):
    """A damped compass's error t minutes after a kick of amplitude_deg: each half swing swing_ratio of the last
    (Sperry's 'two-thirds damping' as one example), period period_min."""
    zeta_w = -math.log(swing_ratio) / (period_min / 2)          # per minute
    wd = 2 * math.pi / period_min
    return amplitude_deg * math.exp(-zeta_w * t_min) * math.cos(wd * t_min)


def envelope(amplitude_deg, t_min, period_min=85.0, swing_ratio=1 / 3.0):
    """The damped error's envelope t minutes after the kick (as decayed, without the swing)."""
    return amplitude_deg * math.exp(math.log(swing_ratio) / (period_min / 2) * t_min)


def max_rate_deg_per_s(amplitude_deg, period_min=85.0):
    """The fastest a compass's oscillating error changes: amplitude x 2 pi / T."""
    return amplitude_deg * 2 * math.pi / (period_min * 60)


def aim_error_from_frame_rate(rate_deg_s, range_m, horizon_s):
    """A gyro error rotates the whole frame (her course and every bearing alike), so a constant one cancels in a
    helm-free table; its rate reads as a bearing rate the table extrapolates over its horizon (latency plus flight)."""
    return math.radians(rate_deg_s) * horizon_s * range_m


def turn_speed_history(v0_ms, loss, tau_loss_s, lag_s, t_end_s=240.0, dt=0.1):
    """Her speed shedding `loss` of v0 with time constant tau_loss_s (a hard turn), and a machine's speed following
    it through a first-order lag lag_s. Returns (peak speed error m/s, own reckoning error after t_end m,
    time of the peak s)."""
    v_hat, dr, peak, t_peak, t = v0_ms, 0.0, 0.0, 0.0, 0.0
    while t < t_end_s:
        v = v0_ms * (1 - loss * (1 - math.exp(-t / tau_loss_s)))
        v_hat += (v - v_hat) * (1 - math.exp(-dt / lag_s)) if lag_s > 0 else (v - v_hat)
        err = v_hat - v
        dr += err * dt
        if abs(err) > abs(peak):
            peak, t_peak = err, t
        t += dt
    return peak, dr, t_peak


def table():
    print(f"Schuler period {T_SCHULER_MIN:.1f} min; the earth's surface at the equator {EQ_KN:.0f} kn\n")

    print("1. Speed error (deg), 20 kn, by course and latitude")
    lats = [0, 30, 45, 56.5, 60, 70]
    print("   course " + "".join(f"{l:>8}" for l in lats))
    for c in (0, 30, 45, 60, 90, 180):
        print(f"   {c:>5}  " + "".join(f"{speed_error_deg(20, c, l):8.2f}" for l in lats))

    lat = 56.5   # the North Sea, Jutland
    print(f"\n   its change in a manoeuvre at {lat} N, 20 kn:")
    cases = [("zigzag 10 deg either side of N", 20, 350, 10),
             ("zigzag 10 deg either side of E", 20, 80, 100),
             ("zigzag 10 deg either side of NE", 20, 35, 55),
             ("90 deg turn N to E", 20, 0, 90),
             ("16-point turn N to S", 20, 0, 180),
             ("speed 20 -> 15 kn heading N", None, 0, 0)]
    for name, v, c0, c1 in cases:
        if v is None:
            d = speed_error_deg(15, 0, lat) - speed_error_deg(20, 0, lat)
        else:
            d = speed_error_deg(v, c1, lat) - speed_error_deg(v, c0, lat)
        print(f"   {name:<34} change {d:+6.2f} deg")

    print("\n2. Ballistic residual: the part of the change left as error (+ under-, - over-deflected); "
          "Chalmers: 'in practice the latitude selected is 40 deg.'")
    for design in (40, 45):
        print(f"   tuned at {design:>4} deg: " + ", ".join(
            f"{l} N {ballistic_residual_fraction(l, design):+.2f}" for l in (0, 30, 45, 56.5, 60, 70)))
    for name, c0, c1 in (("90 deg turn N to E", 0, 90), ("16-point turn N to S", 0, 180),
                         ("16 deg turn 045 -> 061", 45, 61), ("zigzag 10 deg either side of E (a leg)", 80, 100)):
        a = abs((speed_error_deg(20, c1, lat) - speed_error_deg(20, c0, lat)) * ballistic_residual_fraction(lat, 40))
        print(f"   {name:<38} at {lat} N, tuned at 40 N: kick {a:.2f} deg; envelope after 30 min {envelope(a, 30):.2f}, "
              f"90 min {envelope(a, 90):.2f}; fastest change {max_rate_deg_per_s(a)*60:.3f} deg/min")
    a74 = abs((speed_error_deg(13.5, 180, 74) - speed_error_deg(13.5, 0, 74)) * ballistic_residual_fraction(74, 40))
    print(f"   check: 16-point turn at 13.5 kn, 74 N (Lancaster Sound 1972 measured ~6 deg): ballistic alone {a74:.1f} deg")

    print("\n3. What reaches a helm-free table: a frame rotation, so only its rate x the horizon (range 14.6 km, "
          "horizon 15 s latency + 30 s flight)")
    for a in (0.1, 0.5, 1.0, 2.0):
        r = max_rate_deg_per_s(a)
        print(f"   kick {a:>4} deg -> at most {r*60:.3f} deg/min -> aim error "
              f"{aim_error_from_frame_rate(r, 14600, 45):5.1f} m")
    print("   for scale: a 2 deg error in her course alone (the readings' bearings left true): "
          f"{math.radians(2)*14600:.0f} m across at 14.6 km")

    print("\n4. Speed lag through a hard turn: 20 kn shedding 25% with a 30 s time constant")
    v0 = 20 * KN
    for lag, who in ((30, "hand set from a Forbes repeater [FL1 EST]"), (10, "followed by hand, pointer to pointer"),
                     (3, "fed automatically [FL1 EST]"), (0, "exact")):
        peak, dr, tp = turn_speed_history(v0, 0.25, 30, lag)
        print(f"   lag {lag:>3} s ({who:<40}) peak speed error {peak/KN:4.2f} kn at {tp:4.0f} s; "
              f"reckoning off {dr:5.0f} m; over 45 s horizon {abs(peak)*45:4.0f} m")

    print("\n5. For scale, other own-course errors in a turn at 0.47 deg/s (FL1's zigzag) and 1.0 deg/s (hard over), 14.6 km")
    for w in (0.47, 1.0):
        print(f"   {w} deg/s: a course lag of 0.5 s -> {math.radians(w*0.5)*14600:4.0f} m; "
              f"of 2 s -> {math.radians(w*2)*14600:4.0f} m")
    for step in (1 / 6, 1 / 4, 1.0 / 60 * 4):
        print(f"   a transmission step of {step*60:4.1f}' -> +-{math.radians(step/2)*14600:4.0f} m across (truncation)")
    for beta in (3, 6, 10):
        print(f"   a sideslip (drift angle) of {beta:>2} deg at 20 kn -> her velocity {v0*math.sin(math.radians(beta))/KN:4.1f} kn "
              f"off her heading; over 45 s {v0*math.sin(math.radians(beta))*45:4.0f} m")

    print("\n6. The stable vertical in a turn (FL7's machines): the mercury control can't tell the turn's acceleration "
          "from gravity, so it is cut out and the gyro drifts free")
    for v_kn, r_m in ((25, 700), (20, 900)):
        a = (v_kn * KN) ** 2 / r_m
        print(f"   {v_kn} kn on a {r_m} m radius: {a:.2f} m/s2, the apparent vertical {math.degrees(math.atan(a / G))*60:.0f}' off")
    for drift, minutes in ((3, 2), (3, 5), (3, 10)):
        tilt = drift * minutes
        print(f"   free drift {drift}'/min for {minutes:>2} min (a 450 deg turn at 25 kn: ~4 min): {tilt:>2}' of level -> "
              f"~{tilt*15:.0f} yd at 20-25 kyd (16-in/45: ~940 yd a degree of elevation, ~15 yd a minute)")
    print("   (3'/min is the RN's 1921 figure for the Sperry/Henderson free gyro's wander, a stand-in [INF])")


if __name__ == "__main__":
    table()
