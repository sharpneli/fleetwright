"""Reads `shipgen scenario X --play --salvo-log FILE` CSVs (docs/battle/balance.md BL1v-z) and prints, for the heavy guns
(280 mm and up, not point blank), per log:

- where the guns were laid against her, by phase and range band: the median miss, the share within 200 m and past 1 km;
- the mean fall by salvos since fire for effect began (a bias growing with time is a drift the spots don't follow);
- how long a battery holds a target, how often it shifts and goes back to the one before, and the share of ranging salvos;
- the hit rate by range band.

    python tools/salvo_log.py before.csv after.csv
"""
import csv
import statistics
import sys
from collections import Counter, defaultdict


def heavy(path):
    return [r for r in csv.reader(open(path)) if r[0] == 'F' and r[1] != 'seed' and float(r[6]) >= 280 and r[17] == '0']


def band(r):
    m = float(r[7])
    return '<10 km' if m < 10000 else '10-14 km' if m < 14000 else '14+ km'


def main():
    for path in sys.argv[1:]:
        F = heavy(path)
        print(path)
        g = defaultdict(list)
        hits, shells = Counter(), Counter()
        for r in F:
            g[(r[14], band(r))].append(abs(float(r[13])))
            hits[band(r)] += int(r[16])
            shells[band(r)] += int(r[8])
        for k in sorted(g):
            v = g[k]
            print(f'  laid {k[0]:8} {k[1]:8} n {len(v):5}  median {statistics.median(v):4.0f} m  within 200 m '
                  f'{100 * sum(x < 200 for x in v) / len(v):3.0f}%  past 1 km {100 * sum(x > 1000 for x in v) / len(v):4.1f}%')
        print('  hits ' + ', '.join(f'{b} {100 * hits[b] / shells[b]:.1f}% of {shells[b]}' for b in sorted(shells)))
        eng = defaultdict(list)
        for r in F:
            eng[(r[1], r[3], r[4])].append(r)
        age = defaultdict(list)
        holds, salvos, back, shifts, ranging = [], [], 0, 0, 0
        for rs in eng.values():
            rs.sort(key=lambda r: float(r[2]))
            n, target = -1, None
            spans = []
            for r in rs:
                ranging += r[14] == 'Ranging'
                if r[5] != target or r[14] != 'Effect':
                    n, target = -1, r[5]
                if r[14] == 'Effect':
                    n += 1
                    age[min(n // 3, 5)].append(float(r[9]))
                if spans and spans[-1][0] == r[5]:
                    spans[-1][2], spans[-1][3] = float(r[2]), spans[-1][3] + 1
                else:
                    spans.append([r[5], float(r[2]), float(r[2]), 1])
            for i, (t, a, b, k) in enumerate(spans):
                holds.append(b - a)
                salvos.append(k)
                shifts += i > 0
                back += i >= 2 and spans[i - 2][0] == t
        print('  fire for effect, mean fall by salvos since it began: ' +
              ', '.join(f'{3 * k}-{3 * k + 2} {statistics.mean(v):+.0f}' for k, v in sorted(age.items())))
        print(f'  targets: {shifts} shifts, {back} back to the one before; a hold {statistics.median(holds):.0f} s, '
              f'{statistics.median(salvos):.0f} salvos (medians); ranging {100 * ranging / len(F):.0f}% of salvos')


if __name__ == '__main__':
    main()
