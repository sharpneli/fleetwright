namespace Fleetwright.Shipgen.Render;

/// <summary>The ship from above, as the sprites draw it. Everything with a physical effect comes from the hitbox model
/// (Ship.Hitboxes), in the shapes the drawing takes; the rest is dressing (Ship.Dressing) that has none. A look may
/// draw a part slightly over or under its footprint, never move or reshape it. Heights are above the main deck, as in
/// the hitboxes; DeckM turns them into the sprites' heights above the waterline.</summary>
public sealed class TopView
{
    /// <summary>A gun or torpedo mount: its type, pivot, draw order Z (by base height), rest bearing, arcs, and the
    /// roof's height.</summary>
    public sealed record Mount(string Id, ComponentKind Kind, string Type, double X, double Y, long Z, double Rest, List<double[]> Arcs,
        double[]? Traverse, double Top, bool Casemate);

    public sealed record Barbette(double X, double Y, double R, double Top);

    /// <summary>A superstructure block (or a director): its box X0..X1, W wide about Y, its corner radii, or its own
    /// outline (Points); Outline is the hitbox polygon either way. Level counts from what it stands on; Upper: drawn
    /// in the high pass (it stands on something, or on a lower level of its own stack).</summary>
    public sealed record Block(string Id, double X0, double X1, double Y, double W, double Rf, double Rb, List<Pt>? Points, List<Pt> Outline,
        long Level, bool Upper, double Top, Director? Director);

    /// <summary>A director's rangefinder base (0: a gyro sight), radar, and the level it stands on.</summary>
    public sealed record Director(double RangefinderM, bool Radar, long On);

    public sealed record Funnel(string Id, double X, double Y, double L, double W, long Pipes, List<Pt> Outline, double Top);

    /// <summary>A mast's pole and its top's tiers (their hitboxes), its kind, and the dressing on it: its yard, the legs or
    /// struts its kind has, and cargo booms.</summary>
    public sealed record Mast(string Id, double X, double Y, double R, double Top, double Yard, string Kind, List<double[]>? Booms, List<MastTier> Tops)
    {
        public bool Tripod => Kind == "tripod";
    }

    /// <summary>A tier of a mast's top: its radius, and its floor and roof above the main deck.</summary>
    public sealed record MastTier(double R, double Base, double Top);

    /// <summary>A mast's top tiers, from the hitboxes' top components it carries (its id's "top" ones), lowest first.</summary>
    public static List<MastTier> TiersOf(IEnumerable<Component> comps, string mastId) =>
        comps.Where(c => c.Kind == ComponentKind.Top && c.Id.StartsWith(mastId + " top", StringComparison.Ordinal))
            .Select(c => new MastTier(c.R!.Value, c.Base, c.Top)).OrderBy(t => t.Base).ToList();

    public sealed record Aa(string Id, string Type, double X, double Y, double Dir, double R, double Top, bool Upper);
    /// <summary>A searchlight on its platform: Upper on a roof drawn in the high pass, Aloft on a funnel's or a mast's
    /// platform (drawn over them).</summary>
    public sealed record Light(string Id, double X, double Y, double R, double Top, bool Upper, bool Aloft);

    /// <summary>A raised stretch of hull, Levels decks up.</summary>
    public sealed record Raised(double X0, double X1, long Levels, List<Pt> Outline, double Top);

    /// <summary>The conning tower: an armoured drum.</summary>
    public sealed record Tower(double X, double Y, double R, double Top);

    /// <summary>A machinery casing standing proud of the main deck.</summary>
    public sealed record Casing(string Id, List<Pt> Outline, double Top);

    /// <summary>A platform outside the hull: the flight deck or a sponson (Elevator: a deck-edge elevator's).</summary>
    public sealed record Platform(string Id, List<Pt> Outline, double Top, bool Elevator)
    {
        public double X0 => Outline.Min(p => p.X);
        public double X1 => Outline.Max(p => p.X);
        public double Y0 => Outline.Min(p => p.Y);
        public double Y1 => Outline.Max(p => p.Y);
    }

    public required string Id;
    /// <summary>The physical hull at its widest: what is seen from above.</summary>
    public required Hull Hull;
    /// <summary>The main deck: the hull where the sides are straight, narrower where they lean in (tumblehome).</summary>
    public required Planform Deck;
    /// <summary>Where the sides lean in: the side's outline at heights between the widest point and the deck, as
    /// (metres above the waterline, outline), lowest first; empty on a wall-sided hull.</summary>
    public required List<(double TopM, List<Pt> Outline)> SideBands;
    /// <summary>The side's outline at finer steps for its shading (TumblehomeShades of the way from the knuckle to the
    /// deck), lowest first; empty on a wall-sided hull. The profile shades its side at the same heights.</summary>
    public required List<List<Pt>> ShadeBands;
    public static readonly double[] TumblehomeShades = [1 / 6.0, 2 / 6.0, 3 / 6.0, 4 / 6.0, 5 / 6.0];
    /// <summary>The widest point above the waterline (the deck on a wall-sided hull).</summary>
    public required double KnuckleM;
    /// <summary>The main deck above the waterline.</summary>
    public required double DeckM;
    public required OrderedDictionary<string, TurretType> TurretTypes;
    public required List<Mount> Mounts;
    public required List<Barbette> Barbettes;
    public required List<Block> Blocks;
    public required List<Funnel> Funnels;
    public required List<Mast> Masts;
    public required List<Aa> AaMounts;
    public required List<Light> Searchlights;
    public required List<Raised> RaisedDecks;
    public Tower? ConningTower;
    public required List<Casing> Casings;
    public Platform? FlightDeck;
    public required List<Platform> Sponsons;
    /// <summary>What is drawn without a hitbox: deck finish, boats, fittings, hatches, cranes, bow details and the flight
    /// deck's markings.</summary>
    public required Dressing Dressing;

    static int DrawKind(ComponentKind k) => k switch { ComponentKind.Main => 2, ComponentKind.Secondary => 1, _ => 0 };

    static (double X0, double X1, double Y0, double Y1) Box(List<Pt> pts) =>
        (pts.Min(p => p.X), pts.Max(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.Y));

    public static TopView Of(Ship ship)
    {
        var hb = ship.Hitboxes;
        var spec = ship.Dressing;
        var comps = hb.Components;

        var guns = comps.Where(c => c.Local != null).ToList();
        (double, int) Key(Component c) => (Math.Round(c.Base, 3), DrawKind(c.Kind));
        var keys = guns.Select(Key).Distinct().Order().ToList();
        var mounts = guns.Select(c => new Mount(c.Id, c.Kind, c.Type!, c.X!.Value, c.Y!.Value, keys.IndexOf(Key(c)), c.RestDeg ?? 0.0,
            c.ArcsDeg ?? [], c.TraverseDeg, c.Top, c.Casemate == true)).ToList();

        var blocks = new List<Block>();
        foreach (var c in comps.Where(c => c.Kind == ComponentKind.Superstructure))
        {
            long level = c.Level ?? 1;
            bool upper = level > 1 || c.Base - Layout.LevelH * (level - 1) > 0.005;
            var dir = c.Role is "director" or "rangefinder" ? new Director(c.RangefinderM ?? 0.0, c.Radar ?? false, (long)Math.Round(c.Base / Layout.LevelH)) : null;
            if (c.Rrect is { } rr)
                blocks.Add(new Block(c.Id, rr.X0, rr.X1, (rr.Y0 + rr.Y1) / 2, rr.Y1 - rr.Y0, rr.Rf, rr.Rb, null, c.Points!, level, upper, c.Top, dir));
            else
            {
                var (x0, x1, y0, y1) = Box(c.Points!);
                blocks.Add(new Block(c.Id, x0, x1, (y0 + y1) / 2, y1 - y0, 0.0, 0.0, c.Points, c.Points!, level, upper, c.Top, dir));
            }
        }

        var funnels = comps.Where(c => c.Kind == ComponentKind.Funnel).Select(c =>
        {
            var (x0, x1, y0, y1) = Box(c.Points!);
            return new Funnel(c.Id, (x0 + x1) / 2, (y0 + y1) / 2, x1 - x0, y1 - y0, c.Pipes ?? 1, c.Points!, c.Top);
        }).ToList();

        var dressed = spec.Masts.ToDictionary(m => m.Id, StringComparer.Ordinal);
        var masts = comps.Where(c => c.Kind == ComponentKind.Mast).Select(c =>
        {
            var d = dressed[c.Id];
            return new Mast(c.Id, c.X!.Value, c.Y!.Value, c.R!.Value, c.Top, d.Yard, c.Type ?? "pole", d.Booms, TiersOf(comps, c.Id));
        }).ToList();

        // an AA mount on a roof drawn in the high pass is drawn there too, over the roof
        bool OnUpper(Component a) => blocks.Any(b => b.Upper && Math.Abs(b.Top - a.Base) < 0.02 && Geometry.PointInPolygon(a.X!.Value, a.Y!.Value, b.Outline));
        var aa = comps.Where(c => c.Kind == ComponentKind.Aa).Select(c => new Aa(c.Id, c.Type!, c.X!.Value, c.Y!.Value, c.RestDeg ?? 0.0, c.R!.Value, c.Top,
            OnUpper(c))).ToList();

        var tall = comps.Where(c => c.Kind is ComponentKind.Funnel or ComponentKind.Mast).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var lights = comps.Where(c => c.Kind == ComponentKind.Searchlight).Select(c => new Light(c.Id, c.X!.Value, c.Y!.Value, c.R!.Value, c.Top,
            OnUpper(c), c.On is { } on && tall.Contains(on))).ToList();

        var hull = new Hull(new HullSpec(hb.Length, hb.Beam, hb.Bow, hb.Stern));
        var vert = hb.Vertical;
        double deckM = Math.Max(vert.Freeboard, 0.1);
        // where the sides lean in, the decks are the form's at their heights; else the hull's own outline
        Planform SideAt(double h) => vert.Knuckle is null ? hull : new FormPlanform(hb.HullForm, hb.Length, hb.Beam, h);
        var raised = (vert.Raised ?? []).Select(r => new Raised(r.X0, r.X1, (long)Math.Round(r.Top / Geometry.DeckPitch),
            SideAt(r.Top).Points(inset: Layout.RaisedInset, xMin: r.X0, xMax: r.X1), r.Top)).ToList();
        var bands = new List<(double, List<Pt>)>();
        var shades = new List<List<Pt>>();
        double knuckleM = deckM;
        if (vert.Knuckle is double k)
        {
            knuckleM = Math.Max(0.0, k - vert.Waterline);
            foreach (var f in new[] { 0.25, 0.5, 0.75 })
                bands.Add((knuckleM + (deckM - knuckleM) * f, SideAt(k * (1 - f)).Points()));
            foreach (var f in TumblehomeShades)
                shades.Add(SideAt(k * (1 - f)).Points());
        }

        var platforms = comps.Where(c => c.Kind is ComponentKind.FlightDeck or ComponentKind.Sponson)
            .Select(c => (c.Kind, P: new Platform(c.Id, c.Points!, c.Top, c.Role == "elevator"))).ToList();

        return new TopView
        {
            Id = ship.Design.Id ?? "", Hull = hull, Deck = SideAt(0.0), SideBands = bands, ShadeBands = shades, KnuckleM = knuckleM, DeckM = deckM, TurretTypes = hb.TurretTypes, Mounts = mounts,
            Barbettes = comps.Where(c => c.Kind == ComponentKind.Barbette).Select(c => new Barbette(c.X!.Value, c.Y!.Value, c.R!.Value, c.Top)).ToList(),
            Blocks = blocks, Funnels = funnels, Masts = masts, AaMounts = aa, Searchlights = lights, RaisedDecks = raised,
            ConningTower = comps.Where(c => c.Kind == ComponentKind.ConningTower).Select(c => new Tower(c.X!.Value, c.Y!.Value, c.R!.Value, c.Top))
                .FirstOrDefault(),
            Casings = comps.Where(c => c.Kind == ComponentKind.Casing && c.Top > 0).Select(c => new Casing(c.Id, c.Points!, c.Top)).ToList(),
            FlightDeck = platforms.Where(p => p.Kind == ComponentKind.FlightDeck).Select(p => p.P).FirstOrDefault(),
            Sponsons = platforms.Where(p => p.Kind == ComponentKind.Sponson).Select(p => p.P).ToList(), Dressing = spec,
        };
    }

    /// <summary>The static height-map columns, lowest first, metres above the waterline: the hull's deck, the raised and
    /// flight decks and sponsons, casings, the barbettes standing proud, the conning tower, the superstructure, AA,
    /// funnels and masts, and the dressing that stands on deck (hatches, cranes, boats).</summary>
    public List<HeightColumn> HeightColumns()
    {
        var d = Dressing;
        // the hull at its widest, then where the sides lean in, the side stepping up to the deck
        var items = new List<HeightColumn> { new(KnuckleM, "hull") };
        foreach (var (top, outline) in SideBands)
            items.Add(new(top, "polygon") { Points = outline });
        if (SideBands.Count > 0)
            items.Add(new(DeckM, "polygon") { Points = Deck.Points() });
        foreach (var r in RaisedDecks)
            items.Add(new(DeckM + r.Top, "polygon") { Points = r.Outline });
        if (FlightDeck is { } fd)
            items.Add(new(DeckM + fd.Top, "polygon") { Points = fd.Outline });
        foreach (var sp in Sponsons)
            items.Add(new(DeckM + sp.Top, "polygon") { Points = sp.Outline });
        foreach (var c in Casings)
            items.Add(new(DeckM + c.Top, "polygon") { Points = c.Outline });
        foreach (var ht in d.Hatches ?? [])
            items.Add(new(DeckM + 1.2, "rect") { X = ht.X - ht.L / 2, Y = ht.Y - ht.W / 2, W = ht.L, H = ht.W });
        foreach (var cr in d.Cranes ?? [])
            items.Add(new(DeckM + cr.Top, "circle") { Cx = cr.X, Cy = cr.Y, R = cr.R });
        foreach (var b in Barbettes.Where(b => b.Top > 0.5))
            items.Add(new(DeckM + b.Top, "circle") { Cx = b.X, Cy = b.Y, R = b.R });
        if (ConningTower is { } ct)
            items.Add(new(DeckM + ct.Top, "circle") { Cx = ct.X, Cy = ct.Y, R = ct.R });
        foreach (var b in Blocks)
            items.Add(new(DeckM + b.Top, "polygon") { Points = b.Outline });
        foreach (var a in AaMounts)
            items.Add(new(DeckM + a.Top, "circle") { Cx = a.X, Cy = a.Y, R = a.R * 0.8 });
        foreach (var l in Searchlights)   // the drum a dome on its platform
            items.Add(new(DeckM + l.Top, "circle") { Cx = l.X, Cy = l.Y, R = l.R, Profile = Relief.Dome(l.X, l.Y, l.R, l.R, DeckM + l.Top, l.R) });
        foreach (var bt in d.Boats ?? [])
        {
            double top = DeckM + (bt.Top ?? Layout.LevelH + 1.5);   // a boat's hull: an ellipsoid along its length
            items.Add(new(top, "ellipse")
            {
                Cx = bt.X, Cy = bt.Y, Rx = bt.L / 2, Ry = bt.W / 2, Profile = Relief.Dome(bt.X, bt.Y, bt.L / 2, bt.W / 2, top, bt.W / 2),
            });
        }
        foreach (var fn in Funnels)
            items.Add(new(DeckM + fn.Top, "polygon") { Points = fn.Outline });
        foreach (var m in Masts)
        {
            items.Add(new(DeckM + m.Top, "circle") { Cx = m.X, Cy = m.Y, R = m.R, Profile = Relief.Dome(m.X, m.Y, m.R, m.R, DeckM + m.Top, m.R) });
            foreach (var t in m.Tops)
                items.Add(new(DeckM + t.Top, "circle") { Cx = m.X, Cy = m.Y, R = t.R });
        }
        return items.OrderBy(it => it.Top).ToList();
    }
}

/// <summary>A height-map column: Top metres above the waterline over a shape: "hull", "polygon" (Points), "rect" (X, Y,
/// W, H), "circle" (Cx, Cy, R) or "ellipse" (Cx, Cy, Rx, Ry). Profile: its relief's shape when it isn't flat at Top
/// (a dome rising to Top), for the lighting only; the height map keeps the column.</summary>
public sealed record HeightColumn(double Top, string Shape)
{
    public Relief? Profile { get; init; }
    public List<Pt>? Points { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? W { get; init; }
    public double? H { get; init; }
    public double? Cx { get; init; }
    public double? Cy { get; init; }
    public double? R { get; init; }
    public double? Rx { get; init; }
    public double? Ry { get; init; }
}
