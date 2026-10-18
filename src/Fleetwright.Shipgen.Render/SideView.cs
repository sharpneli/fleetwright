namespace Fleetwright.Shipgen.Render;

/// <summary>The ship from the starboard side, as the profile draws it: TopView's twin. Every part with a physical effect
/// comes from the hitbox model (Ship.Hitboxes) as its extent along the ship (x) over its heights; the rest is dressing
/// (Ship.Dressing). Heights are above the main deck, as in the hitboxes. Each part keeps its extent across the ship (Y0
/// .. Y1, +y starboard) for depth: the profile is seen from +y, so the larger Y1, the nearer.</summary>
public sealed class SideView
{
    /// <summary>A part's box: X0..X1 along the ship, Y0..Y1 across it, Base..Top up.</summary>
    public readonly record struct Box(double X0, double X1, double Y0, double Y1, double Base, double Top)
    {
        public double Xc => (X0 + X1) / 2;
        public double L => X1 - X0;
        public double H => Top - Base;

        public static Box Of(IReadOnlyList<Pt> pts, double bse, double top) =>
            new(pts.Min(p => p.X), pts.Max(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.Y), bse, top);

        public static Box Round(double x, double y, double r, double bse, double top) => new(x - r, x + r, y - r, y + r, bse, top);
    }

    /// <summary>A superstructure block (or a director, whose rangefinder base and radar it carries), Level counting from
    /// what it stands on, as in the plan; Role: "bridge", "aft_control", "island", "deckhouse", "director", ...</summary>
    public sealed record Block(string Id, Box B, long Level, string? Role, TopView.Director? Director);

    /// <summary>A gun or torpedo mount at its rest bearing: the body's box, the barrels' extent along the ship
    /// (BarrelX0..BarrelX1; none for a type without barrels) and its type.</summary>
    public sealed record Mount(string Id, ComponentKind Kind, string Type, double X, double Y, double Rest, Box B, double BarrelX0, double BarrelX1,
        bool Casemate);

    public sealed record Funnel(string Id, Box B, long Pipes);

    /// <summary>A mast's pole from its deck to its top, its kind, its top's tiers, and the dressing on it.</summary>
    public sealed record Mast(string Id, double X, double Y, double R, double Base, double Top, double Yard, string Kind, List<double[]>? Booms,
        List<TopView.MastTier> Tops);

    public sealed record Aa(string Id, string Type, double X, double Y, double R, double Base, double Top, double Dir);

    /// <summary>A platform outside the hull's deck: the flight deck or a sponson (Elevator: a deck-edge elevator's).</summary>
    public sealed record Platform(string Id, ComponentKind Kind, Box B, bool Elevator);

    /// <summary>A shaft outside the hull, from where it leaves the hull to its propeller: (x, y, z) at either end.</summary>
    public sealed record Shaft(string Id, double X0, double Z0, double X1, double Z1, double Y, double R);

    public sealed record Propeller(string Id, double X, double Y, double Z, double R);

    /// <summary>A submerged torpedo tube's muzzle door in the shell, seen from starboard: Beam (a round door in the
    /// side) or a bow or stern door seen edge-on.</summary>
    public sealed record TubePort(double X, double Z, double R, bool Beam);

    public required string Id;
    public required double Length;
    public required double Beam;
    /// <summary>The hull's side seen from starboard as (x, z): the deck line (stepping up over the raised decks), the stem,
    /// the keel from the hull-form stations (forefoot and counter as built) and the stern, closed.</summary>
    public required List<Pt> Hull;
    /// <summary>The keel line from the hull-form stations, aft to fore: (x, height).</summary>
    public required List<(double X, double Z)> KeelLine;
    /// <summary>The hull's ends along the ship.</summary>
    public required double HullX0, HullX1;
    public required double Keel;
    /// <summary>The hull from above at its widest (the plan's outline).</summary>
    public required Hull Planform;
    public required double Waterline;
    /// <summary>Where the sides start to lean in (tumblehome), above the main deck; null on a wall-sided hull.</summary>
    public double? Knuckle;
    /// <summary>The raised decks (forecastle, bridge deck, poop): their stretch and height above the main deck.</summary>
    public required List<(double X0, double X1, double Top)> Raised;
    public required OrderedDictionary<string, TurretType> TurretTypes;
    public required List<Mount> Mounts;
    public required List<(Box B, string Mount)> Barbettes;
    public required List<Block> Blocks;
    public required List<Funnel> Funnels;
    public required List<Mast> Masts;
    public required List<Aa> AaMounts;
    public Box? ConningTower;
    public required List<Box> Casings;
    public required List<Box> Hangars;
    public required List<Platform> Platforms;
    public required List<Shaft> Shafts;
    public required List<Propeller> Propellers;
    public required List<Box> Rudders;
    public List<TubePort> TubePorts = [];
    /// <summary>The side armour (the belt and the strakes above or beyond it): its stretch and heights.</summary>
    public required List<(double X0, double X1, double Bottom, double Top)> SideArmour;
    /// <summary>What is drawn without a hitbox: boats, cranes, hatches.</summary>
    public required Dressing Dressing;

    /// <summary>The deck line's height at x: the main deck (0), or a raised deck over it.</summary>
    public double DeckAt(double x) => Raised.Where(r => r.X0 <= x && x <= r.X1).Select(r => r.Top).DefaultIfEmpty(0.0).Max();

    /// <summary>The hull's bottom at x on the centreline (the keel line between stations), or null off its ends.</summary>
    public double? KeelAt(double x)
    {
        var k = KeelLine;
        if (k.Count == 0 || x < k[0].X || x > k[^1].X)
            return null;
        int i = 1;
        while (i < k.Count - 1 && k[i].X < x)
            i++;
        var (a, b) = (k[i - 1], k[i]);
        return b.X - a.X < 1e-9 ? a.Z : a.Z + (b.Z - a.Z) * (x - a.X) / (b.X - a.X);
    }

    /// <summary>What a part at (x, y) whose foot is at height z stands on: the highest deck, roof, platform or barbette
    /// top under it, no higher than z (a little over, for rounding); the deck line if nothing else.</summary>
    public double SupportAt(double x, double y, double z)
    {
        double best = x >= HullX0 && x <= HullX1 ? DeckAt(x) : double.NegativeInfinity;
        bool Under(Box b) => b.X0 <= x && x <= b.X1 && b.Y0 <= y && y <= b.Y1 && b.Top <= z + 0.05;
        foreach (var b in Blocks.Select(b => b.B).Concat(Platforms.Select(p => p.B)).Concat(Casings).Concat(Hangars).Concat(Barbettes.Select(b => b.B)))
            if (Under(b) && b.Top > best)
                best = b.Top;
        return double.IsNegativeInfinity(best) ? z : Math.Min(best, z);
    }

    /// <summary>The body and barrels of a mount at its rest bearing, rotated into the ship's frame: (x0, x1, y0, y1)
    /// of each.</summary>
    static ((double, double, double, double) Body, (double, double)? Barrels) Rotated(TurretLocal loc, double x, double y, double restDeg)
    {
        double a = double.DegreesToRadians(restDeg), c = Math.Cos(a), s = Math.Sin(a);
        Pt R(Pt p) => new(x + p.X * c - p.Y * s, y + p.X * s + p.Y * c);
        var body = loc.Body.Concat(loc.Parts.SelectMany(p => p)).Select(R).ToList();
        var barrels = loc.Barrels.SelectMany(b => b).Select(R).ToList();
        return ((body.Min(p => p.X), body.Max(p => p.X), body.Min(p => p.Y), body.Max(p => p.Y)),
            barrels.Count > 0 ? (barrels.Min(p => p.X), barrels.Max(p => p.X)) : null);
    }

    public static SideView Of(Ship ship)
    {
        var hb = ship.Hitboxes;
        var comps = hb.Components;
        var vert = hb.Vertical;
        var raised = (vert.Raised ?? []).Select(r => (r.X0, r.X1, r.Top)).ToList();

        // the hull: the deck line from the stern to the stem, then the keel back aft; the ends are where the stations
        // end (a station's lowest height is its keel)
        var st = hb.HullForm.Stations;
        double hx0, hx1;
        List<(double X, double Z)> keel;
        if (st.Count >= 2)
        {
            keel = st.Select(s => (s.X, s.Z[0])).ToList();
            (hx0, hx1) = (st[0].X, st[^1].X);
        }
        else   // no stations: a box from the keel to the deck over the planform's length
        {
            (hx0, hx1) = (hb.Hull.Min(p => p.X), hb.Hull.Max(p => p.X));
            keel = [(hx0, vert.Keel), (hx1, vert.Keel)];
        }
        double Deck(double x) => raised.Where(r => r.X0 <= x && x <= r.X1).Select(r => r.Top).DefaultIfEmpty(0.0).Max();
        var breaks = raised.SelectMany(r => new[] { r.X0, r.X1 }).Where(x => hx0 < x && x < hx1).Append(hx0).Append(hx1).Distinct().Order().ToList();
        var outline = new List<Pt>();
        for (int i = 0; i + 1 < breaks.Count; i++)
        {
            double h = Deck((breaks[i] + breaks[i + 1]) / 2);
            if (outline.Count == 0 || Math.Abs(outline[^1].Y - h) > 1e-9)
                outline.Add(new(breaks[i], h));
            outline.Add(new(breaks[i + 1], h));
        }
        for (int i = keel.Count - 1; i >= 0; i--)
            outline.Add(new(keel[i].X, keel[i].Z));

        var mounts = new List<Mount>();
        foreach (var c in comps.Where(c => c.Local != null))
        {
            var (body, barrels) = Rotated(c.Local!, c.X!.Value, c.Y!.Value, c.RestDeg ?? 0.0);
            var (bx0, bx1) = barrels ?? (c.X!.Value, c.X!.Value);
            mounts.Add(new Mount(c.Id, c.Kind, c.Type!, c.X!.Value, c.Y!.Value, c.RestDeg ?? 0.0,
                new Box(body.Item1, body.Item2, body.Item3, body.Item4, c.Base, c.Top), bx0, bx1, c.Casemate == true));
        }

        var blocks = comps.Where(c => c.Kind == ComponentKind.Superstructure).Select(c => new Block(c.Id, Box.Of(c.Points!, c.Base, c.Top), c.Level ?? 1, c.Role,
            c.Role is "director" or "rangefinder" ? new TopView.Director(c.RangefinderM ?? 0.0, c.Radar ?? false, (long)Math.Round(c.Base / Layout.LevelH)) : null)).ToList();

        var dressed = ship.Dressing.Masts.ToDictionary(m => m.Id, StringComparer.Ordinal);
        var masts = comps.Where(c => c.Kind == ComponentKind.Mast).Select(c =>
        {
            var d = dressed[c.Id];
            return new Mast(c.Id, c.X!.Value, c.Y!.Value, c.R!.Value, c.Base, c.Top, d.Yard, c.Type ?? "pole", d.Booms, TopView.TiersOf(comps, c.Id));
        }).ToList();

        var props = comps.Where(c => c.Kind == ComponentKind.Propeller).Select(c => new Propeller(c.Id, c.X!.Value, c.Y!.Value, c.Z!.Value, c.DiameterM!.Value / 2))
            .ToList();
        var shafts = new List<Shaft>();
        foreach (var c in comps.Where(c => c.Kind == ComponentKind.Shaft && c.P0 != null && c.P1 != null))
        {
            // the shaft runs from p0 (forward, in the engine room) to p1 (the propeller): the part outside the hull
            double[] p0 = c.P0!, p1 = c.P1!;
            double xOut = c.LeavesHullX ?? p0[0];
            double t = Math.Abs(p1[0] - p0[0]) < 1e-9 ? 0.0 : Math.Clamp((xOut - p0[0]) / (p1[0] - p0[0]), 0.0, 1.0);
            double zOut = p0[2] + (p1[2] - p0[2]) * t;
            shafts.Add(new Shaft(c.Id, p1[0], p1[2], xOut, zOut, p0[1], c.R ?? 0.25));
        }

        var armour = new List<(double, double, double, double)>();
        if (hb.Armour.Belt is { } belt)
            armour.Add((belt.X0, belt.X1, belt.Bottom, belt.Top));
        foreach (var sk in hb.Armour.Strakes ?? [])
            armour.Add((sk.X0, sk.X1, sk.Bottom, sk.Top));

        return new SideView
        {
            Id = ship.Design.Id ?? "", Length = hb.Length, Beam = hb.Beam, Planform = new Hull(new HullSpec(hb.Length, hb.Beam, hb.Bow, hb.Stern)), Hull = outline, KeelLine = keel, HullX0 = hx0, HullX1 = hx1, Keel = vert.Keel,
            Waterline = vert.Waterline, Knuckle = vert.Knuckle, Raised = raised, TurretTypes = hb.TurretTypes, Mounts = mounts,
            Barbettes = comps.Where(c => c.Kind == ComponentKind.Barbette).Select(c => (Box.Round(c.X!.Value, c.Y!.Value, c.R!.Value, c.Base, c.Top), c.Mount ?? ""))
                .ToList(),
            Blocks = blocks,
            Funnels = comps.Where(c => c.Kind == ComponentKind.Funnel).Select(c => new Funnel(c.Id, Box.Of(c.Points!, c.Base, c.Top), c.Pipes ?? 1)).ToList(),
            Masts = masts,
            AaMounts = comps.Where(c => c.Kind == ComponentKind.Aa).Select(c => new Aa(c.Id, c.Type!, c.X!.Value, c.Y!.Value, c.R!.Value, c.Base, c.Top,
                c.RestDeg ?? 0.0)).ToList(),
            ConningTower = comps.Where(c => c.Kind == ComponentKind.ConningTower).Select(c => (Box?)Box.Round(c.X!.Value, c.Y!.Value, c.R!.Value, c.Base, c.Top))
                .FirstOrDefault(),
            Casings = comps.Where(c => c.Kind == ComponentKind.Casing && c.Top > 0).Select(c => Box.Of(c.Points!, c.Base, c.Top)).ToList(),
            Hangars = comps.Where(c => c.Kind == ComponentKind.HangarBay).Select(c => Box.Of(c.Points!, c.Base, c.Top)).ToList(),
            Platforms = comps.Where(c => c.Kind is ComponentKind.FlightDeck or ComponentKind.Sponson)
                .Select(c => new Platform(c.Id, c.Kind, Box.Of(c.Points!, c.Base, c.Top), c.Role == "elevator")).ToList(),
            Shafts = shafts, Propellers = props,
            Rudders = comps.Where(c => c.Kind == ComponentKind.Rudder).Select(c => Box.Of(c.Points!, c.Base, c.Top)).ToList(),
            // the starboard beam doors, and one bow or stern door where a pair stands side by side
            TubePorts = comps.Where(c => c.Kind == ComponentKind.TorpedoTube && c.P1 is { } p && p[1] >= -0.01)
                .Select(c => new TubePort(c.P1![0], c.P1[2], c.R ?? 0.35, c.Position == "beam")).ToList(),
            SideArmour = armour, Dressing = ship.Dressing,
        };
    }

    /// <summary>Everything drawn, along the ship and up: (x0, x1, z0, z1), metres, heights above the main deck.</summary>
    public (double X0, double X1, double Z0, double Z1) Bounds()
    {
        double x0 = HullX0, x1 = HullX1, z0 = Keel, z1 = Hull.Max(p => p.Y);
        void Add(double xa, double xb, double za, double zb)
        {
            (x0, x1, z0, z1) = (Math.Min(x0, xa), Math.Max(x1, xb), Math.Min(z0, za), Math.Max(z1, zb));
        }
        foreach (var m in Mounts)
            Add(Math.Min(m.B.X0, m.BarrelX0), Math.Max(m.B.X1, m.BarrelX1), m.B.Base, m.B.Top);
        foreach (var b in Blocks.Select(b => b.B).Concat(Funnels.Select(f => f.B)).Concat(Platforms.Select(p => p.B)).Concat(Rudders)
                     .Concat(Barbettes.Select(b => b.B)).Concat(Casings).Concat(Hangars))
            Add(b.X0, b.X1, b.Base, b.Top);
        foreach (var m in Masts)
        {
            Add(m.X - m.R, m.X + m.R, m.Base, m.Top);
            foreach (var t in m.Tops)
                Add(m.X - t.R, m.X + t.R, t.Base, t.Top);
        }
        foreach (var a in AaMounts)
            Add(a.X - a.R, a.X + a.R, a.Base, a.Top);
        foreach (var p in Propellers)
            Add(p.X - p.R, p.X + p.R, p.Z - p.R, p.Z + p.R);
        if (ConningTower is { } ct)
            Add(ct.X0, ct.X1, ct.Base, ct.Top);
        foreach (var bt in Dressing.Boats ?? [])
            Add(bt.X - bt.L / 2, bt.X + bt.L / 2, 0, bt.Top ?? Layout.LevelH + 1.5);
        foreach (var cr in Dressing.Cranes ?? [])
            Add(cr.X - cr.Jib, cr.X + cr.Jib, 0, cr.Top);
        return (x0, x1, z0, z1);
    }
}
