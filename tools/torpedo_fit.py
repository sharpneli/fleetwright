"""The matchup's torpedo hit chance fitted to play (docs/battle/balance.md P11).

Input: the CSVs `shipgen scenario X --play --fire-table` writes with P11_SPREADS=FILE set (every spread fired: its
scenario, by day or night, torpedoes, hits on its target and on other ships, range, the run on the setting it was fired
on, the setting's and the target's speeds, her length). The matchup can't know the geometry, so it times a torpedo's run
as she turns away from it: the range over the torpedo's speed less hers (and, to compare, as a shot on her beam: over
its speed across her track). The law fitted, by maximum likelihood on a grid:

    hit = (min(cap, (T / t)^p) + floor) * sqrt(length / 150)

Hits are on her or another ship (a spread into a line finds the next astern). Prints both geometries' best fits, each
scenario's hits against the fit, and the hit rate by run time.

    python -I tools/torpedo_fit.py docs/battle/p11/fitdata
"""
import csv
import glob
import math
import sys

KN = 0.514444


def load(folder):
    rows = []
    for path in glob.glob(folder + "/*.csv"):
        with open(path) as f:
            for r in csv.DictReader(f):
                def num(k):
                    return float(r[k]) if r[k] not in ("", None) else float("nan")
                rows.append(dict(sc=r["scenario"], night=int(r["night"]), n=int(r["count"]), H=int(r["hits"]) + int(r["others"]),
                                 R=num("range_m"), kn=num("setting_kn"), slow=num("slow_kn"), tkn=num("target_kn"), L=num("length_m")))
    return rows


def speed(r):
    # the setting it ran on; a shot out of reach on the truth as the slow one
    return (r["kn"] if r["kn"] == r["kn"] else r["slow"]) * KN


def stern(r):
    a = speed(r) - r["tkn"] * KN
    return r["R"] / a if a > 0.5 else float("inf")


def beam(r):
    a = math.sqrt(max(speed(r) ** 2 - (r["tkn"] * KN) ** 2, 0))
    return r["R"] / a if a > 0.5 else float("inf")


def chance(T, p, cap, floor, r, key):
    t = key(r)
    return (min(cap, (T / t) ** p if t > 0 else cap) + floor) * math.sqrt(r["L"] / 150)


def loglik(rows, T, p, cap, floor, key):
    s = 0
    for r in rows:
        x = min(max(chance(T, p, cap, floor, r, key), 1e-6), 0.999)
        s += r["H"] * math.log(x) + (r["n"] - r["H"]) * math.log(1 - x)
    return s


def main():
    rows = load(sys.argv[1] if len(sys.argv) > 1 else "docs/battle/p11/fitdata")
    print(f"{sum(r['n'] for r in rows)} torpedoes, {sum(r['H'] for r in rows)} hits, {len(set(r['sc'] for r in rows))} scenarios")
    for name, key in (("as she turns away", stern), ("on her beam", beam)):
        best = max((loglik(rows, T, p, c, F, key), T, p, c, F) for T in range(8, 41, 2) for p in [1.0, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6]
                   for c in [0.3, 0.4, 0.5] for F in [0, 0.0025, 0.005, 0.01])
        ll, T, p, c, F = best
        print(f"{name}: T {T} s, power {p}, cap {c}, floor {F} (log-likelihood {ll:.1f})")
        for sc in sorted(set(r["sc"] for r in rows)):
            for night in (0, 1):
                b = [r for r in rows if r["sc"] == sc and r["night"] == night]
                n = sum(r["n"] for r in b)
                if n:
                    print(f"   {sc:22s} {'night' if night else 'day  '} {n:5d} torpedoes: {sum(r['H'] for r in b):4d} hits, the fit {sum(r['n'] * chance(T, p, c, F, r, key) for r in b):6.1f}")
        for lo, hi in ((0, 45), (45, 90), (90, 180), (180, 360), (360, 720), (720, float("inf"))):
            b = [r for r in rows if lo <= key(r) < hi]
            n = sum(r["n"] for r in b)
            if n:
                h = sum(r["H"] for r in b)
                print(f"   run {lo:4.0f}-{hi:4.0f} s: {n:5d} torpedoes, {h:4d} hits ({100 * h / n:4.1f}%), the fit {100 * sum(r['n'] * chance(T, p, c, F, r, key) for r in b) / n:4.1f}%")


if __name__ == "__main__":
    main()
