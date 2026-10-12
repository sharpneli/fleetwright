"""Bake Natural Earth land (world-atlas's TopoJSON) into the game's land files (Content/World/land-*.bin).

    python -I tools/land_bake.py DIR

DIR holds world-atlas 2.0.2's land-110m.json, land-50m.json and land-10m.json (from
https://cdn.jsdelivr.net/npm/world-atlas@2.0.2/). Each becomes Content/World/land-<res>.bin: the land as rings of
longitude and latitude, outer coasts and holes (lakes, the Caspian) alike, read by Fleetwright.Sim.Core's Land.

The format, little-endian: int32 magic 0x444C5746 ("FWLD"), int32 version 1, int32 ring count, then each ring's
point count (int32), then every point as int32 longitude and latitude in microdegrees, ring after ring. A ring isn't
closed by repeating its first point, and has no two equal points in a row.

The data is Natural Earth (public domain), via world-atlas (ISC, Mike Bostock). A rough first Earth: a better model
replaces it later (docs/battle/plan.md).
"""
import json, os, struct, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Content", "World")


def rings(path):
    topo = json.load(open(path, encoding="utf-8"))
    sx, sy = topo["transform"]["scale"]
    tx, ty = topo["transform"]["translate"]
    arcs = []
    for arc in topo["arcs"]:
        x = y = 0
        pts = []
        for dx, dy in arc:
            x += dx
            y += dy
            pts.append((x * sx + tx, y * sy + ty))
        arcs.append(pts)

    def ring(indices):
        out = []
        for i in indices:
            pts = arcs[i] if i >= 0 else arcs[~i][::-1]
            out.extend(pts if not out else pts[1:])
        return out

    def polygons(g):
        if g["type"] == "Polygon":
            yield g["arcs"]
        elif g["type"] == "MultiPolygon":
            yield from g["arcs"]
        elif g["type"] == "GeometryCollection":
            for c in g["geometries"]:
                yield from polygons(c)

    for poly in polygons(topo["objects"]["land"]):
        for r in poly:
            pts = []
            for lon, lat in ring(r):
                q = (round(lon * 1e6), round(lat * 1e6))
                if not pts or pts[-1] != q:
                    pts.append(q)
            if len(pts) > 1 and pts[0] == pts[-1]:
                pts.pop()
            if len(pts) >= 3:
                yield pts


def bake(src, res):
    rs = list(rings(os.path.join(src, f"land-{res}.json")))
    path = os.path.join(OUT, f"land-{res}.bin")
    with open(path, "wb") as f:
        f.write(struct.pack("<iii", 0x444C5746, 1, len(rs)))
        f.write(struct.pack(f"<{len(rs)}i", *(len(r) for r in rs)))
        for r in rs:
            f.write(struct.pack(f"<{2 * len(r)}i", *(v for p in r for v in p)))
    n = sum(len(r) for r in rs)
    print(f"land-{res}: {len(rs)} rings, {n} points, largest {max(len(r) for r in rs)}, {os.path.getsize(path)} bytes")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for res in ("110m", "50m", "10m"):
        bake(sys.argv[1], res)
