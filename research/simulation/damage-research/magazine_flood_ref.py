"""Magazine flooding: how long the sea takes to fill a magazine through a flood valve, and how
long the order takes to reach the valve. Reference for research note
11-magazine-flooding.md (simulation/damage-research/).

Run: python3 magazine_flood_ref.py

Physics [INFERRED]: a sea valve and its pipe pass Q = c * A * sqrt(2 g h), with h the depth of the
water outside over the water inside (the head falls as the magazine fills: Torricelli). c lumps the
valve, bends and the strainer; 0.4-0.6 for a short pipe with a globe or sluice valve. The air escape
is assumed big enough (Attwood: lift valve and a pipe led up to the upper deck), which a choked
escape would not be. A magazine wholly below the waterline fills to its crown; one that straddles it
fills from the sea only to the outside water level, the rest needs the fire main (USN 1917).
"""
import math

G = 9.81


def fill_time_s(volume_m3, height_m, floor_depth_m, bore_m, c=0.5, to_share=1.0, steps=4000):
    """Seconds for a sea valve of `bore_m` to fill a box magazine (`volume_m3`, `height_m` high, its
    floor `floor_depth_m` below the waterline) to `to_share` of its height. None if the sea can't
    reach that level (the share above the waterline)."""
    area_floor = volume_m3 / height_m
    a_valve = math.pi * bore_m ** 2 / 4
    target = to_share * height_m
    if target >= floor_depth_m:
        return None
    t, y = 0.0, 0.0
    dy = target / steps
    for _ in range(steps):
        head = floor_depth_m - (y + dy / 2)
        q = c * a_valve * math.sqrt(2 * G * head)
        t += area_floor * dy / q
        y += dy
    return t


def bore_for(volume_m3, height_m, floor_depth_m, minutes, c=0.5):
    """The bore that fills the magazine in `minutes` (bisection)."""
    lo, hi = 0.02, 1.0
    for _ in range(60):
        mid = (lo + hi) / 2
        t = fill_time_s(volume_m3, height_m, floor_depth_m, mid, c)
        if t is None or t > minutes * 60:
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2


def main():
    print("1. Fill times, min (floor 7 m below the waterline, magazine 3 m high, c = 0.5)")
    print("   rows: volume m3; columns: bore mm; each cell: to 1/3 height (bottom tier) / half / full")
    bores = [0.10, 0.15, 0.20, 0.25, 0.30]
    print("   V\\D  " + "".join(f"{int(b*1000):>18}" for b in bores))
    for v in [50, 100, 200, 300, 500]:
        cells = []
        for b in bores:
            third = fill_time_s(v, 3, 7, b, to_share=1 / 3) / 60
            half = fill_time_s(v, 3, 7, b, to_share=0.5) / 60
            full = fill_time_s(v, 3, 7, b) / 60
            cells.append(f"{third:5.1f}/{half:5.1f}/{full:5.1f}")
        print(f"   {v:>4} " + "".join(f"{c:>18}" for c in cells))

    print("\n2. The second half is slower than the first (Torricelli): full / half")
    for floor in [4.0, 5.0, 7.0, 10.0]:
        half = fill_time_s(200, 3, floor, 0.2, to_share=0.5)
        full = fill_time_s(200, 3, floor, 0.2) if floor > 3 else None
        print(f"   floor {floor:4.1f} m below the waterline: full/half = {full/half:4.2f}")

    print("\n3. The bore that meets the USN 1917 rule (full in 20 min), floor 7 m, 3 m high")
    for c in [0.4, 0.5, 0.6]:
        row = ", ".join(f"{v} m3: {bore_for(v, 3, 7, 20, c)*1000:4.0f} mm" for v in [50, 100, 200, 300, 500])
        print(f"   c {c}: {row}")

    print("\n4. A fire-main 'sprinkler' at the rule's 20 min needs, m3/min (and US gal/min)")
    for v in [20, 50, 100, 200]:
        print(f"   {v:>4} m3: {v/20:5.1f} m3/min = {v/20*264.2:6.0f} gpm")

    print("\n5. Order to water, minutes [EST], by who decides and where the valve is")
    decide = {"in the train (turret officer, handing room)": (0.0, 1.0),
              "the gunner or a DC officer who sees smoke": (1.0, 4.0),
              "the bridge, on a messenger's report": (2.0, 6.0)}
    reach = {"valve station clear, key to hand": (0.5, 2.0),
             "key in a locked rack / captain's cabin": (2.0, 5.0),
             "station in smoke (got through)": (3.0, 15.0)}
    for d, (d0, d1) in decide.items():
        for r, (r0, r1) in reach.items():
            print(f"   {d:<46} + {r:<38}: {d0+r0:4.1f}-{d1+r1:4.1f}")


if __name__ == "__main__":
    main()
