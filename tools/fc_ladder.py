"""The fire-control error ladder (docs/battle/balance.md BL1w): one duel case run seed by seed with each rung's
`--errors` (each error source the chain draws scaled: all, readings, prior, bias, laying, salvo, dispersion, spotting),
and per window of minutes since the first salvo: the computer's own error, where the guns were laid (with its wander
within a run), the salvos' mean fall against her, the hits and the share of ranging salvos. All in range, metres (RMS,
mean in brackets).

    python tools/fc_ladder.py "all=0" "all=0,bias=1" "all=1" [--seeds 16] [--settings FILE] [--exe PATH]
        [-- lion derfflinger --range 16000 --minutes 15 --speed 22 --target-speed 22 --course 0 --wind 0,0 --swell 0]

What each rung should show, from first principles: everything off, every salvo on her once ranged; one source on, the
fall's scatter that source's own and its mean on her (the spots take off what persists).
"""
import math
import os
import re
import subprocess
import sys
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXE = os.path.join(ROOT, 'src', 'Fleetwright.Shipgen.Cli', 'bin', 'DevRelease', 'net10.0', 'shipgen.exe')
CASE = 'lion derfflinger --range 16000 --minutes 15 --speed 22 --target-speed 22 --course 0 --wind 0,0 --swell 0'.split()
WINDOWS = [(0, 3), (3, 6), (6, 10), (10, 15)]
LINE = re.compile(r'^ +([0-9]+) s  ([ER]).*laid +([-+]+\d+) +n +(\d+) +fall +([-+]+\d+) m.*aim +([-+]+\d+).*hits (\d+)')


def num(s):
    return float(s.replace('-+', '-'))


def run(rung, seed, exe, settings, case):
    args = [exe, 'duel', *case, '--runs', '1', '--trace', '--seed', str(seed), '--errors', rung]
    if settings:
        args += ['--settings', settings]
    out = subprocess.run(args, capture_output=True, text=True, cwd=ROOT).stdout
    salvos = []
    for line in out.splitlines():
        m = LINE.search(line)
        if m:
            salvos.append(dict(t=float(m.group(1)), phase=m.group(2), laid=num(m.group(3)), shells=int(m.group(4)),
                               fall=num(m.group(5)), aim=num(m.group(6)), hits=int(m.group(7))))
    return salvos


def rms(v):
    return math.sqrt(sum(x * x for x in v) / len(v)) if v else float('nan')


def mean(v):
    return sum(v) / len(v) if v else float('nan')


def main():
    argv = sys.argv[1:]
    case = CASE
    if '--' in argv:
        i = argv.index('--')
        argv, case = argv[:i], argv[i + 1:]
    seeds, settings, exe, rungs = 16, None, EXE, []
    it = iter(argv)
    for a in it:
        if a == '--seeds':
            seeds = int(next(it))
        elif a == '--settings':
            settings = next(it)
        elif a == '--exe':
            exe = next(it)
        else:
            rungs.append(a)
    print(' '.join(case) + f', {seeds} seeds')
    for rung in rungs or ['all=0']:
        g = defaultdict(lambda: defaultdict(list))
        for seed in range(1, seeds + 1):
            salvos = run(rung, seed, exe, settings, case)
            if not salvos:
                continue
            t0 = salvos[0]['t']
            for x in salvos:
                minutes = (x['t'] - t0) / 60
                for i, (a, b) in enumerate(WINDOWS):
                    if a <= minutes < b:
                        w = g[i]
                        for k in ('aim', 'laid', 'fall'):
                            w[k].append(x[k])
                        w['ranging'].append(1 if x['phase'] == 'R' else 0)
                        w['hits'].append(x['hits'])
                        w['shells'].append(x['shells'])
                        w['by_run'].append((seed, x['laid']))
        for i, (a, b) in enumerate(WINDOWS):
            w = g[i]
            runs = defaultdict(list)
            for seed, v in w['by_run']:
                runs[seed].append(v)
            wander = math.sqrt(mean([mean([(v - mean(vs)) ** 2 for v in vs]) for vs in runs.values()])) if runs else float('nan')
            head = f'{rung:34}' if i == 0 else ' ' * 34
            print(f"{head}{a:2}-{b:<2} min: computer {rms(w['aim']):4.0f} ({mean(w['aim']):+4.0f})  laid {rms(w['laid']):4.0f}"
                  f" (wander {wander:4.0f})  fall {rms(w['fall']):4.0f} ({mean(w['fall']):+4.0f})"
                  f"  hits {100 * sum(w['hits']) / max(sum(w['shells']), 1):5.1f}%  ranging {100 * mean(w['ranging']):3.0f}%")


if __name__ == '__main__':
    main()
