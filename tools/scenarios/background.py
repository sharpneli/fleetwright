"""The scale test's background (docs/battle/plan.md step 2.7c): tramp steamers on looping routes across the open North
Atlantic, on rails while the Jutland is fought, as a ladder rung from the Jutland plus N of them. Deterministic (a fixed
seed), so the file it writes is reproducible:

    python -I tools/scenarios/background.py 400 Content/Scenarios/ladder-4-world.json "Ladder 4: the Jutland and a world"
"""
import json, math, random, sys

n, out, name = int(sys.argv[1]), sys.argv[2], sys.argv[3]
rng = random.Random(1916 + n)

def sea():
    # open water: the North Atlantic between Newfoundland's banks and the approaches to Europe, clear of land
    return [round(rng.uniform(42, 58), 3), round(rng.uniform(-45, -18), 3)]

def bearing(a, b):
    # the initial great-circle bearing from a to b, degrees: a tramp starts steady on its first leg, so it goes onto
    # rails at once instead of swinging round first (half an hour of 1,227 tramps on the fine tick)
    la1, lo1, la2, lo2 = map(math.radians, (a[0], a[1], b[0], b[1]))
    y = math.sin(lo2 - lo1) * math.cos(la2)
    x = math.cos(la1) * math.sin(la2) - math.sin(la1) * math.cos(la2) * math.cos(lo2 - lo1)
    return round(math.degrees(math.atan2(y, x)) % 360, 1)

units = []
for i in range(n):
    route = [sea() for _ in range(rng.choice([2, 3, 4]))]
    start = sea()
    units.append({"name": f"Tramp {i + 1}", "side": 2, "at": start, "course": bearing(start, route[0]), "kn": rng.choice([6, 7, 8]),   # under the tramp design's 9 kn: a ship rung past its top speed only creeps up to it, never steady
                  "route": route, "loop": True, "ships": [{"design": "tramp_1914"}]})

scenario = {
    "scenario": 1,
    "test": True,
    "name": name,
    "brief": f"The scale test's fourth rung: the Jutland, and {n} tramp steamers on rails across the North Atlantic (a neutral side).",
    "from": "jutland",
    "sides": ["Britain", "Germany", "Neutral"],
    "peace": [["Britain", "Neutral"], ["Germany", "Neutral"]],
    "units": units,
}
with open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write("// Written by tools/scenarios/background.py: the Jutland (\"from\") and tramp steamers on looping routes across the\n")
    f.write("// open North Atlantic, a neutral side, on rails while the battle is fought (docs/battle/plan.md step 2.7c).\n")
    f.write("{\n")
    for k in ["scenario", "test", "name", "brief", "from", "sides", "peace"]:
        f.write(f"  {json.dumps(k)}: {json.dumps(scenario[k], ensure_ascii=False)},\n")
    f.write('  "units": [\n')
    f.write(",\n".join("    " + json.dumps(u, ensure_ascii=False) for u in units))
    f.write("\n  ]\n}\n")
print(f"{out}: {n} tramps")
