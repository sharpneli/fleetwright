"""Reference values from Windows CPython for the C# Py helpers (written to pyref.json)."""
import json, math, random, struct, sys
rng = random.Random(42)
def h(x): return struct.pack('>d', x).hex()
def rnd(): 
    k = rng.random()
    if k < 0.3: return rng.uniform(-10, 10)
    if k < 0.6: return rng.uniform(0, 1)
    if k < 0.8: return math.exp(rng.uniform(-20, 20)) * rng.choice((1, -1))
    return round(rng.uniform(-1000, 1000), rng.randint(0, 4))
out = dict(unary=[], binary=[], fmt=[], round=[], sums=[])
for _ in range(4000):
    x = rnd()
    rec = [h(x)]
    for name, f in (("sin", math.sin), ("cos", math.cos), ("tan", math.tan), ("atan", math.atan),
                    ("exp", lambda v: math.exp(v) if v < 700 else 0.0), ("log", lambda v: math.log(abs(v)) if v else 0.0),
                    ("sqrt", lambda v: math.sqrt(abs(v))), ("gamma", lambda v: math.gamma(abs(v) % 30 + 0.01)),
                    ("cbrt", lambda v: abs(v) ** (1 / 3)), ("pow075", lambda v: abs(v) ** 0.75),
                    ("sq", lambda v: v ** 2), ("radians", math.radians), ("degrees", math.degrees),
                    ("floordiv", lambda v: v // 0.37), ("mod", lambda v: v % 0.37), ("mod360", lambda v: v % 360.0)):
        rec.append(h(f(x)))
    out["unary"].append(rec)
for _ in range(4000):
    x, y = rnd(), rnd()
    out["binary"].append([h(x), h(y), h(math.atan2(x, y)), h(math.hypot(x, y)), h(math.dist((x, y), (y * 0.3, x - 1))),
                          h(abs(x) ** y if abs(y) < 50 and x != 0 else 0.0)])
for _ in range(3000):
    x = rnd()
    out["fmt"].append([h(x), repr(x), f"{x:.0f}", f"{x:.1f}", f"{x:.2f}", f"{x:.3f}", f"{x:g}", f"{x:,.0f}", f"{x:+.1f}", f"{x:,.1f}"])
for v in (0.5, 1.5, 2.5, -0.5, 0.125, 0.375, 1e16, 1e-5, 0.0001, 123456789.0, 1e22, 2.675, -0.0, 5e-324, 1.7976931348623157e308, 999999.5, 9999995.0, 0.00001234):
    out["fmt"].append([h(v), repr(v), f"{v:.0f}", f"{v:.1f}", f"{v:.2f}", f"{v:.3f}", f"{v:g}", f"{v:,.0f}", f"{v:+.1f}", f"{v:,.1f}"])
for _ in range(4000):
    x = rnd()
    n = rng.randint(-2, 6)
    out["round"].append([h(x), n, h(round(x, n)), round(x) if abs(x) < 1e15 else 0])
for _ in range(500):
    xs = [rnd() for _ in range(rng.randint(0, 30))]
    out["sums"].append([[h(x) for x in xs], h(float(sum(xs)))])
json.dump(dict(python=sys.version, **out), open(sys.argv[1], "w"))
