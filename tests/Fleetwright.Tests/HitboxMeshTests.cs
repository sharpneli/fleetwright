using System.Collections.Concurrent;
using System.Numerics;
using Fleetwright.HitView;
using Fleetwright.Shipgen;

namespace Fleetwright.Tests;

public class HitboxMeshTests
{
    static double TriArea(IReadOnlyList<Pt> p, List<int> tris)
    {
        double a = 0;
        for (int i = 0; i < tris.Count; i += 3)
        {
            var (u, v, w) = (p[tris[i]], p[tris[i + 1]], p[tris[i + 2]]);
            a += Math.Abs((v.X - u.X) * (w.Y - u.Y) - (v.Y - u.Y) * (w.X - u.X)) / 2;
        }
        return a;
    }

    [Fact]
    public void Ear_clipping_covers_a_concave_polygon_exactly()
    {
        // an L, a U (two reflex corners) and a square with a collinear point on one side
        List<Pt>[] polys =
        [
            [new(0, 0), new(4, 0), new(4, 1), new(1, 1), new(1, 3), new(0, 3)],
            [new(0, 0), new(3, 0), new(3, 3), new(2, 3), new(2, 1), new(1, 1), new(1, 3), new(0, 3)],
            [new(0, 0), new(1, 0), new(2, 0), new(2, 2), new(0, 2)],
        ];
        foreach (var p in polys)
        {
            var tris = HitboxMesh.Triangulate(p);
            Assert.Equal(HitboxMesh.SignedArea(p), TriArea(p, tris), 9);
        }
    }

    /// <summary>Every design's mesh: well-formed indices, prisms with height, caps that cover their footprint (the
    /// triangulation held), and nothing far outside the hull.</summary>
    [Fact]
    public void Every_design_meshes_cleanly()
    {
        var paths = Directory.GetFiles(Paths.Shipgen("designs"), "*.json");
        Assert.True(paths.Length > 50);
        var failures = new ConcurrentBag<string>();
        Parallel.ForEach(paths, path =>
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var design = Design.Load(path);
            if (ShipDesign.Validate(design, limits: false).Count > 0)
                return;
            var hb = ShipDesign.Build(design).Hitboxes;
            var mesh = HitboxMesh.Build(hb);
            var v = mesh.Vertices;
            if (mesh.Prisms.Length == 0 || mesh.Indices.Length % 3 != 0 || mesh.EdgeIndices.Length % 2 != 0)
                failures.Add($"{name}: malformed ({mesh.Prisms.Length} prisms)");
            if (mesh.Indices.Any(i => i >= v.Length) || mesh.EdgeIndices.Any(i => i >= v.Length))
                failures.Add($"{name}: index out of range");
            // a sanity bound, not the hull: flight decks, sponsons and deck-edge lifts overhang the beam (a
            // supercarrier's lift reaches 1.4 beams out)
            float reach = (float)hb.Length / 2 + 20, beam = 2 * (float)hb.Beam;
            if (v.Any(x => !float.IsFinite(x.Position.X) || !float.IsFinite(x.Position.Y) || !float.IsFinite(x.Position.Z)))
                failures.Add($"{name}: a vertex isn't finite");
            foreach (var p in mesh.Prisms)
            {
                if (!(p.Top > p.Base))
                    failures.Add($"{name}: {p.Id} has no height");
                if (p.Min.X < -reach || p.Max.X > reach || Math.Abs(p.Min.Y) > beam || Math.Abs(p.Max.Y) > beam)
                    failures.Add($"{name}: {p.Id} lies outside the ship ({p.Min} to {p.Max})");
                // the top cap (the triangles facing up) must cover the footprint, no more, no less
                var fp = p.Footprint.Select(q => new Pt(q.X, q.Y)).ToList();
                double want = HitboxMesh.SignedArea(fp), got = 0;
                var idx = mesh.Indices.AsSpan(p.FirstIndex, p.IndexCount);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    if (v[idx[i]].Normal != Vector3.UnitZ)
                        continue;
                    Vector3 a = v[idx[i]].Position, b = v[idx[i + 1]].Position, c = v[idx[i + 2]].Position;
                    got += Math.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) / 2;
                }
                if (Math.Abs(got - want) > 0.01 * want + 0.01)
                    failures.Add($"{name}: {p.Id} cap covers {got:F2} m2 of {want:F2}");
            }
        });
        Assert.True(failures.IsEmpty, string.Join("\n", failures.Take(30)));
    }
}
