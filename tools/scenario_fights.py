"""Each scenario's seeds: losses and hits by side, and the games with no real fight (under 10 hits in all, nobody lost)
(docs/battle/balance.md P11, "The scenarios' fights"). Input: a folder of `shipgen scenario X --play --seeds N` outputs.

    python -I tools/scenario_fights.py docs/battle/p11/fights
"""
import glob
import os
import re
import sys

folder = sys.argv[1]
side_re = re.compile(r"(\S[^,]*?) lost (\d+)/(\d+) \((\d+) t\)[^,]*(?:, aims met)?, score [\d.]+, hits (\d+)")
print(f"{'scenario':22s} {'seeds':>5s} {'no fight':>8s}  sides: lost a game (hits a game)")
for path in sorted(glob.glob(os.path.join(folder, "*.txt"))):
    name = os.path.basename(path)[:-4]
    seeds = []
    for line in open(path, encoding="utf-8", errors="replace"):
        if not line.startswith("  seed"):
            continue
        sides = [(m.group(1).split("  ")[-1].strip(), int(m.group(2)), int(m.group(5))) for m in side_re.finditer(line)]
        if sides:
            seeds.append(sides)
    if not seeds:
        print(f"{name:22s}   (none)")
        continue
    none = sum(1 for sd in seeds if sum(h for _, _, h in sd) < 10 and sum(l for _, l, _ in sd) == 0)
    names = [n for n, _, _ in seeds[0]]
    parts = []
    for i, n in enumerate(names):
        lost = sum(sd[i][1] for sd in seeds) / len(seeds)
        hits = sum(sd[i][2] for sd in seeds) / len(seeds)
        parts.append(f"{n} {lost:.1f} ({hits:.0f})")
    print(f"{name:22s} {len(seeds):5d} {none:8d}  " + "   ".join(parts))
