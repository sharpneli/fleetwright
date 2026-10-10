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

    /// <summary>A ray straight down onto a main turret finds it; with main mounts hidden it finds what is under the
    /// turret instead; with the clip box below the turret's base, nothing above the box.</summary>
    [Fact]
    public void Picking_finds_the_first_shown_prism()
    {
        var mesh = HitboxMesh.Build(ShipDesign.Build(Design.Load(Paths.Shipgen("designs", "bismarck.json"))).Hitboxes);
        var turret = mesh.Prisms.First(p => HitKinds.All[p.Kind].Name == "main");
        var c = (turret.Min + turret.Max) / 2;
        var (o, d) = (new Vector3(c.X, c.Y, 200), -Vector3.UnitZ);

        var all = HitboxViewState.All;
        Assert.Equal(turret.Id, mesh.Prisms[mesh.Pick(o, d, all)].Id);

        var noMain = all with { KindMask = all.KindMask & ~(1UL << HitKinds.IndexOf("main")) };
        var under = mesh.Prisms[mesh.Pick(o, d, noMain)];
        Assert.NotEqual("main", HitKinds.All[under.Kind].Name);
        Assert.True(under.Top <= turret.Base + 0.01f, $"{under.Id} tops at {under.Top}, the turret's base is {turret.Base}");

        float cut = turret.Base - 3;
        var clipped = all with { Clip = true, ClipMin = mesh.Min - Vector3.One, ClipMax = mesh.Max with { Z = cut } };
        var below = mesh.Prisms[mesh.Pick(o, d, clipped)];
        Assert.True(below.Base <= cut, $"{below.Id} starts at {below.Base}, above the cut at {cut}");

        Assert.Equal(HitboxMesh.NoPrism, mesh.Pick(new Vector3(0, 500, 200), d, all));   // off the ship
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
                if (p.Kind == HitKinds.IndexOf("hull") || p.Lofted)
                    continue;   // the hull's skin and the lofts aren't extruded footprints (the tests below)
                // the top cap (the triangles facing up) must cover its ring of vertices (the footprint, drawn grown by
                // a hair), no more, no less; and the drawn ring stays within a few centimetres of the footprint
                double got = 0;
                uint top0 = uint.MaxValue;
                var idx = mesh.Indices.AsSpan(p.FirstIndex, p.IndexCount);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    if (v[idx[i]].Normal != Vector3.UnitZ)
                        continue;
                    Vector3 a = v[idx[i]].Position, b = v[idx[i + 1]].Position, c = v[idx[i + 2]].Position;
                    got += Math.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) / 2;
                    top0 = Math.Min(top0, Math.Min(idx[i], Math.Min(idx[i + 1], idx[i + 2])));
                }
                var ring = Enumerable.Range((int)top0, p.Footprint.Length).Select(k => new Pt(v[k].Position.X, v[k].Position.Y)).ToList();
                double want = HitboxMesh.SignedArea(ring);
                if (Math.Abs(got - want) > 0.01 * want + 0.01)
                    failures.Add($"{name}: {p.Id} cap covers {got:F2} m2 of {want:F2}");
                for (int k = 0; k < ring.Count; k++)
                    if (Vector2.Distance(new((float)ring[k].X, (float)ring[k].Y), p.Footprint[k]) > 0.15f)
                    {
                        failures.Add($"{name}: {p.Id} is drawn {Vector2.Distance(new((float)ring[k].X, (float)ring[k].Y), p.Footprint[k]):F2} m off its footprint");
                        break;
                    }
            }
        });
        Assert.True(failures.IsEmpty, string.Join("\n", failures.Take(30)));
    }

    /// <summary>The hull's skin is the hull form: every vertex of its sides lies on |y| = W(x, z), pushed out by the
    /// centimetre that keeps it off the cells; with tumblehome too, and up a raised stretch's sides.</summary>
    [Theory]
    [InlineData("bismarck")]
    [InlineData("bouvet")]
    [InlineData("danton")]
    public void Hull_skin_lies_on_the_hull_form(string name)
    {
        var hb = ShipDesign.Build(Design.Load(Paths.Shipgen("designs", $"{name}.json"))).Hitboxes;
        var field = new HullField(hb.HullForm, hb.Vertical);
        var mesh = HitboxMesh.Build(hb);
        int hull = HitKinds.IndexOf("hull"), sides = 0;
        foreach (var p in mesh.Prisms.Where(p => p.Kind == hull))
        {
            var idx = mesh.Indices.AsSpan(p.FirstIndex, p.IndexCount);
            foreach (var i in idx)
            {
                var v = mesh.Vertices[i];
                if (Math.Abs(v.Normal.Y) < 0.5 || v.Normal.Z == 1 || v.Normal.Z == -1)
                    continue;   // the deck tops, bottoms and end walls
                sides++;
                double x = Math.Clamp(v.Position.X, field.X0 + 1e-4, field.X1 - 1e-4);
                double w = field.HalfWidth(x, v.Position.Z);
                Assert.True(Math.Abs(Math.Abs(v.Position.Y) - (w + 0.01)) < 2e-3 || w == 0,
                    $"{name}: {p.Id} vertex {v.Position} is {Math.Abs(v.Position.Y) - w:F3} m off the form");
            }
        }
        Assert.True(sides > 1000);
    }

    /// <summary>What the hull's side cuts follows the side up its height: a cell cut by it stays inside the hull at every
    /// height, and the belt and strakes hug it (outer face 0.15 m out, inner 0.25 m in), top to bottom.</summary>
    [Theory]
    [InlineData("bismarck")]
    [InlineData("bouvet")]
    [InlineData("danton")]
    public void Lofts_follow_the_hull_side(string name)
    {
        var hb = ShipDesign.Build(Design.Load(Paths.Shipgen("designs", $"{name}.json"))).Hitboxes;
        var field = new HullField(hb.HullForm, hb.Vertical);
        var mesh = HitboxMesh.Build(hb);
        int belt = HitKinds.IndexOf("belt"), strake = HitKinds.IndexOf("strake"), cells = 0, armour = 0;
        foreach (var p in mesh.Prisms.Where(p => p.Lofted))
        {
            bool onSide = p.Kind == belt || p.Kind == strake;
            if (p.Source is Cell)
                cells++;
            else if (onSide)
                armour++;
            foreach (var i in mesh.Indices.AsSpan(p.FirstIndex, p.IndexCount))
            {
                var q = mesh.Vertices[i].Position;
                double w = field.HalfWidth(Math.Clamp(q.X, field.X0 + 1e-4, field.X1 - 1e-4), Math.Clamp(q.Z, p.Base, p.Top));
                                double xc = Math.Clamp(q.X, field.X0 + 1e-4, field.X1 - 1e-4), zc = Math.Clamp(q.Z, p.Base, p.Top);
                // the side's least and greatest half-breadth within 5 cm (a drawn shape is grown by a hair, more at its corners)
                var near = (from dx in new[] { -0.05, 0.0, 0.05 } from dz in new[] { -0.05, 0.0, 0.05 }
                            select field.HalfWidth(Math.Clamp(xc + dx, field.X0 + 1e-4, field.X1 - 1e-4), zc + dz)).ToList();
                double reach = near.Max(), wLo = near.Min();
                if (p.Source is Cell)
                    Assert.True(Math.Abs(q.Y) <= reach + 0.05, $"{name}: {p.Id} reaches {Math.Abs(q.Y) - w:F2} m out of the hull at {q}");
                else if (onSide && w > 0.3 && Math.Abs(q.Y) > 0.5 * w)   // where the side is wider than the belt is thick
                {
                    // its outer face or its inner, give or take 5 cm in height (the side can step there: a cut-up stern)
                    bool On(double off) => Math.Abs(q.Y) - off >= wLo - 0.08 && Math.Abs(q.Y) - off <= reach + 0.08;   // a corner grows up to 4 hairs
                    Assert.True(On(0.15) || On(-0.25), $"{name}: {p.Id} stands {Math.Abs(q.Y) - w:F2} m off the side at {q}");
                }
            }
        }
        Assert.True(cells > 10 && armour >= 2, $"{name}: {cells} lofted cells, {armour} lofted belts and strakes");
    }
}
