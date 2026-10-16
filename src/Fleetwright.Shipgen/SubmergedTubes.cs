namespace Fleetwright.Shipgen;

/// <summary>Submerged torpedo tubes (torpedoes.submerged): fixed 533 mm tubes below the waterline, firing on the beam,
/// ahead through the stem or astern, each in a torpedo room that holds its reloads. The layout plans the rooms (Plan);
/// once the hull's form exists, Build runs each tube from its room out through the shell.</summary>
public static class SubmergedTubes
{
    /// <summary>A tube's length (the torpedo's, as the fixed tube's), the radius its hitbox and port take, and the
    /// calibre.</summary>
    public const double TubeLen = 7.2, TubeR = 0.35, CalibreMm = 533;
    /// <summary>A tube's installation (tube, sluice, bar and air), tonnes, and the default torpedoes a tube carries, the
    /// one in it included (Dreadnought: 23 for 5 tubes; Queen Elizabeth: 20 for 4).</summary>
    public const double TubeT = 6.0;
    public const int PerTubeDefault = 4;
    /// <summary>Along the beam room: the pitch of one side's tubes, the room's ends, and the length a rack of reloads
    /// takes (four torpedoes athwartships, two each side, stacked).</summary>
    const double Pitch = 3.0, Ends = 2.0, RackL = 0.7, RackN = 4;
    /// <summary>The axis's depth under the waterline: 0.3 draught, held to 1.5-3 m; at least MinDepth, and AxisUp over
    /// the floor of a deck space at least MinTier tall.</summary>
    const double DepthK = 0.3, DepthMin = 1.5, DepthMax = 3.0, MinDepth = 1.2, AxisUp = 1.0, MinTier = 1.8;
    /// <summary>A beam room's breadth at the tubes: a tube and the run to load it, staggered across the centreline.</summary>
    const double LoadRun = 2.0;
    /// <summary>How far a bow or stern room's end may stand from where its tubes leave the hull: the tube, and a
    /// sleeve through the spaces between.</summary>
    const double MaxRun = 1.5 * TubeLen;
    /// <summary>Crew: per room and per tube (loading numbers).</summary>
    const double RoomCrew = 2, TubeCrew = 3;

    public static int PerSide(Design d) => d.Torpedoes?.Submerged?.PerSide ?? 0;
    public static int Bow(Design d) => d.Torpedoes?.Submerged?.Bow ?? 0;
    public static int Stern(Design d) => d.Torpedoes?.Submerged?.Stern ?? 0;
    public static int Count(Design d) => 2 * PerSide(d) + Bow(d) + Stern(d);
    public static int PerTube(Design d) => d.Torpedoes?.Submerged?.TorpedoesPerTube ?? PerTubeDefault;

    /// <summary>Tubes and torpedoes, tonnes: the first-pass estimate and each room's booking.</summary>
    public static double Weight(int tubes, int perTube) => tubes * (TubeT + perTube * Batteries.TorpedoT);

    /// <summary>A torpedo room's crew for its tubes.</summary>
    public static double Crew(int tubes) => tubes == 0 ? 0 : RoomCrew + TubeCrew * tubes;

    /// <summary>The rooms and their tubes, after the style's compartments are placed (ShipDesign.LayOut): first a bow
    /// room abaft the collision bulkhead and a stern room as far aft as the hull at its axis reaches, each within MaxRun
    /// of where its tubes leave the hull; then the beam rooms, nearest the space forward of the forward turrets' trunks.
    /// Each keeps clear of the rooms and trunks in its band. The axis lies about 0.3 draught under water, on a deck
    /// space's floor when one is near (else the room spans two); a stern tube passes beside a centreline rudder's stock
    /// and rises while gear hangs aft of where it leaves the hull. Widths come from the hull's form. Books the weights;
    /// asks for length when a room has no place.</summary>
    public static void Plan(Layout lay, Design design, Navarch.Result res)
    {
        int nSide = PerSide(design), nBow = Bow(design), nStern = Stern(design);
        if (nSide + nBow + nStern == 0)
            return;
        if (design.StyleName == "planing" || lay.Geo.Plant is not { } plan)
        {
            lay.Warnings.Add("Submerged torpedo tubes need a hull with decks below the waterline; a planing craft has none. Left out.");
            return;
        }
        double L = lay.Hull.L, D = res.Depth, T = res.Draught;
        var gear = Propulsion.Gear(lay, design, res);
        var form = ShipDesign.FormOf(lay, design, res, gear);
        var zs = Levels(plan);
        double axis = Axis(zs, T);
        if (T - axis < MinDepth)
            lay.Warnings.Add($"The hull is too shallow for submerged tubes: their axis is only {T - axis:F1} m under the waterline, " +
                             "and a torpedo leaving the tube may broach.");
        double ib = plan.InnerBottom - D, roof = res.Armour.RoofZ is double rz ? rz - D : 0.0;
        int perTube = PerTube(design);
        double coll = L / 2 - Subdivision.Collision * L;

        int Reloads(int tubes) => tubes * Math.Max(0, perTube - 1);
        double Stow(int tubes) => RackL * Math.Ceiling(Reloads(tubes) / RackN);
        // a room along the axis (bow, stern): the torpedo's loading run behind the breech, the reloads beside it
        double AxialL(int n) => TubeLen + 1.0 + Stow(n);

        // nowhere for a room len long: more length in the middle (the warship's sizer gives it there, where the rooms
        // and machinery crowd), else in all
        void Short(double len, string what)
        {
            string msg = $"No room below the waterline for {what} ({len:F1} m long): a longer hull leaves more between the " +
                         "magazines, the machinery and the ends.";
            if (design.StyleName == "warship")
                lay.FailMid(len + 0.5, msg);
            else
                lay.FailLength(L + len, msg);
        }

        Compartment? Room(string id, double len, double a, double pref, Func<double, double, bool> fits, string what)
        {
            var c = TryRoom(id, len, a, pref, fits);
            if (c is null)
                Short(len, what);
            return c;
        }

        // the free place for a room len long whose band holds the axis a (above the keel), nearest pref, where fits
        // says the hull is wide enough
        Compartment? TryRoom(string id, double len, double a, double pref, Func<double, double, bool> fits)
        {
            var (bse, top) = Band(zs, a);
            var busy = Obstacles(lay, bse - D, top - D, ib, roof);
            double? best = null;
            for (double x0 = -L / 2; x0 + len <= coll + 1e-9; x0 += 0.25)
            {
                double x1 = x0 + len;
                if (best is double bx && Math.Abs(x0 + len / 2 - pref) >= Math.Abs(bx + len / 2 - pref))
                    continue;
                if (busy.Any(b => b.X0 < x1 && x0 < b.X1) || !fits(x0, x1))
                    continue;
                best = x0;
            }
            if (best is not double xa)
                return null;
            var c = new Compartment
            {
                Id = id, Kind = "torpedo_room", X0 = xa, X1 = xa + len, HalfWidth = lay.Hull.HalfWidth(xa + len / 2), Base = bse - D,
                Top = top - D, Tubes = [],
            };
            lay.Compartments.Add(c);
            return c;
        }

        void Book(Compartment c, int tubes, double a)
        {
            c.Tonnes = Math.Round(tubes * perTube * Batteries.TorpedoT, 1);
            lay.Weights.Add(new Weight(c.Id, "armament", Weight(tubes, perTube), (c.X0 + c.X1) / 2, ZRel.Deck(a - D)));
        }

        // the end rooms first: each wants its end, where its tubes leave; the beam rooms take what is left
        if (nBow > 0)
        {
            double off = 0.6 * (nBow - 1);
            double stem = L / 2;
            while (stem > 0 && form.HalfWidth(stem, axis) <= off + TubeR + 0.1)
                stem -= 0.25;
            if (Room("Bow torpedo room", AxialL(nBow), axis, coll,
                    (_, x1) => form.HalfWidth(x1 - 1.0, axis) > off + TubeR + 0.1 && stem - x1 <= MaxRun,
                    $"the bow tube{(nBow > 1 ? "s" : "")}") is { } room)
            {
                var ys = Propulsion.Spread(nBow, off);
                for (int k = 0; k < ys.Count; k++)
                    Add(lay, room, nBow == 1 ? "Bow tube" : $"Bow tube {k + 1}", "bow", room.X1, ys[k].Y, axis - D, 0.0);
                Book(room, nBow, axis);
            }
        }
        if (nStern > 0)
        {
            // beside a centreline rudder's stock (it runs up through the hull to the steering gear), not over it
            double off = 0.6 * (nStern - 1), yc = 0.0;
            if (gear.Rudders.FirstOrDefault(rd => Math.Abs(rd.Y) < off + TubeR + rd.Thick / 2 + 0.2) is { } stock)
            {
                double clear = stock.Thick / 2 + TubeR + 0.25;
                if (nStern == 1)
                    yc = stock.Y + clear;
                else
                    off = Math.Max(off, clear);
            }
            // where the hull at the axis ends aft, the torpedo leaves: nothing hanging under the counter may stand aft
            // of that on its line, else the axis rises until the hull reaches past it (or the line clears it)
            double lo = yc - off - TubeR, hi = yc + off + TubeR, reach = Math.Max(Math.Abs(lo), Math.Abs(hi));
            double ExitX(double z)
            {
                double x = -L / 2;
                while (x < 0 && form.HalfWidth(x, z) <= reach + 0.1)
                    x += 0.25;
                return x;
            }
            var hang = gear.Rudders.Select(rd => (Id: rd.Id, X: rd.X0, Y0: rd.Y - rd.Thick / 2, Y1: rd.Y + rd.Thick / 2, rd.Base, rd.Top))
                .Concat(gear.Propellers.Select(p => (Id: p.Id, X: p.X - 0.25 * p.Diameter, Y0: p.Y - p.Diameter / 2, Y1: p.Y + p.Diameter / 2,
                    Base: p.Z - p.Diameter / 2, Top: p.Z + p.Diameter / 2))).ToList();
            List<string> InTheWay(double z)
            {
                double ex = ExitX(z);
                return hang.Where(h => h.X < ex && h.Base < z + TubeR && z - TubeR < h.Top && h.Y0 < hi && lo < h.Y1)
                    .Select(h => h.Id.ToLowerInvariant()).ToList();
            }
            double a = axis;
            var over = InTheWay(a);
            while (over.Count > 0 && T - a > TubeR + 0.2)
            {
                a += 0.25;
                if (InTheWay(a).Count == 0)
                    break;
            }
            string what = nStern > 1 ? "The stern tubes" : "The stern tube";
            if (T - a < TubeR + 0.2)
                lay.Fail(null, $"{what} would fire into the {string.Join(" and the ", over)} at any depth under water: twin rudders " +
                               "or an even number of shafts leave the line clear.");
            else
            {
                if (over.Count > 0 && T - a < MinDepth)
                    lay.Warnings.Add($"{what} rise{(nStern > 1 ? "" : "s")} to clear the {string.Join(" and the ", over)}, only " +
                                     $"{T - a:F1} m under the waterline: a torpedo leaving may broach.");
                // the axis so far, then each deck space's whose axis is deep enough and clear (a room over the steering
                // gear rather than beside it), nearest first
                var axes = zs.Take(zs.Count - 1).Select((z, i) => (A: z + AxisUp, Tall: zs[i + 1] - z))
                    .Where(c => c.Tall >= MinTier - 1e-6 && T - c.A >= MinDepth && Math.Abs(c.A - a) > 0.05 && InTheWay(c.A).Count == 0)
                    .Select(c => c.A).OrderBy(c => Math.Abs(c - a)).Prepend(a);
                Compartment? room = null;
                double at = a;
                foreach (var c in axes)
                {
                    double end = ExitX(c);
                    room = TryRoom("Stern torpedo room", AxialL(nStern), c, -L / 2,
                        (x0, _) => form.HalfWidth(x0 + 1.0, c) > reach + 0.1 && x0 - end <= MaxRun);
                    if (room != null)
                    {
                        at = c;
                        break;
                    }
                }
                if (room is null)
                    Short(AxialL(nStern), what.ToLowerInvariant());
                else
                {
                    var ys = Propulsion.Spread(nStern, off);
                    for (int k = 0; k < ys.Count; k++)
                        Add(lay, room, nStern == 1 ? "Stern tube" : $"Stern tube {k + 1}", "stern", room.X0, yc + ys[k].Y, at - D, 180.0);
                    Book(room, nStern, at);
                }
            }
        }
        if (nSide > 0)
        {
            double needHw = (TubeLen + LoadRun) / 2;
            double widest = form.HalfWidth(form.Lcb, axis);
            bool Wide(double x0, double x1) => Math.Min(form.HalfWidth(x0 + 0.5, axis), form.HalfWidth(x1 - 0.5, axis)) >= needHw;
            double BeamL(int n) => Ends + Pitch * n + Stow(2 * n);
            // just forward of the foremost trunk (the hull wide there, the room near the fore magazines), then just
            // abaft the aftmost
            var trunks = lay.Mounts.Where(m => m.T.HasBarbette && m.Kind == "main").Select(m => (m.X, R: Geometry.BarbetteR(m.T))).ToList();
            var cit = lay.Geo.CitadelSpan(L);
            double fore = trunks.Count > 0 ? trunks.Max(t => t.X + t.R) : cit.X1, aftmost = trunks.Count > 0 ? trunks.Min(t => t.X - t.R) : cit.X0;
            if (widest < needHw)
            {
                lay.Fail("beam", $"The beam tubes need {2 * needHw:F1} m across at their depth to load; the hull has {2 * widest:F1} m.");
                lay.NeedB = Math.Max(lay.NeedB, lay.Hull.B * needHw / Math.Max(widest, 0.1) + 0.05);
            }
            else
            {
                // one room if it fits, else the pairs shared out over more rooms, fore and aft by turns
                List<(Compartment Room, int N)>? rooms = null;
                for (int parts = 1; parts <= nSide && rooms is null; parts++)
                {
                    var got = new List<(Compartment, int)>();
                    for (int k = 0; k < parts; k++)
                    {
                        int n = nSide / parts + (k < nSide % parts ? 1 : 0);
                        double len = BeamL(n);
                        if (TryRoom("Torpedo room", len, axis, k % 2 == 0 ? fore + len / 2 : aftmost - len / 2, Wide) is not { } room)
                            break;
                        got.Add((room, n));
                    }
                    if (got.Count == parts)
                        rooms = got;
                    else
                        foreach (var (room, _) in got)
                            lay.Compartments.Remove(room);
                }
                if (rooms is null)
                    Short(BeamL(nSide), "the beam tubes");
                else
                {
                    rooms.Sort((p, q) => q.Room.X0.CompareTo(p.Room.X0));
                    int next = 1;
                    for (int r = 0; r < rooms.Count; r++)
                    {
                        var (room, n) = rooms[r];
                        if (rooms.Count > 1)
                            room.Id = $"Torpedo room {r + 1}";
                        double xa = (room.X0 + room.X1) / 2 - Pitch * n / 2;
                        for (int k = 0; k < n; k++, next++)
                            foreach (int side in (int[])[1, -1])
                                Add(lay, room, $"Beam tube {next}{(side > 0 ? "S" : "P")}", "beam", xa + Pitch * (k + (side > 0 ? 0.75 : 0.25)),
                                    side, axis - D, side > 0 ? 90.0 : 270.0);
                        Book(room, 2 * n, axis);
                    }
                }
            }
        }
    }

    static void Add(Layout lay, Compartment room, string id, string kind, double x, double y, double z, double bearing)
    {
        room.Tubes!.Add(id);
        lay.Tubes.Add(new SubmergedTube(id, kind, room.Id, x, y, z, bearing));
    }

    /// <summary>The floors a room may stand on and the decks over it, above the keel: the inner bottom, the decks of
    /// the stack above it, up to the main deck.</summary>
    static List<double> Levels(PlantPlan plan) =>
        plan.Decks.Where(z => z > plan.InnerBottom + 1e-6).Append(plan.InnerBottom).Order().ToList();

    /// <summary>The tubes' axis above the keel: about 0.3 draught under water; AxisUp over the floor of a deck space at
    /// least MinTier tall when that lies within a metre of it and is deep enough, else just there.</summary>
    static double Axis(List<double> zs, double T)
    {
        double want = Math.Max(T - Math.Clamp(DepthK * T, DepthMin, DepthMax), zs[0] + 0.6);
        double? best = null;
        for (int i = 0; i + 1 < zs.Count; i++)
        {
            double a = zs[i] + AxisUp;
            if (zs[i + 1] - zs[i] >= MinTier - 1e-6 && T - a >= MinDepth && Math.Abs(a - want) <= 1.0
                && (best is not double b || Math.Abs(a - want) < Math.Abs(b - want)))
                best = a;
        }
        return best ?? want;
    }

    /// <summary>The room round an axis, above the keel: from the highest floor half a metre under it to the lowest deck
    /// 0.8 m over it.</summary>
    static (double Base, double Top) Band(List<double> zs, double a) =>
        (zs.Where(z => z <= a - 0.5).DefaultIfEmpty(zs[0]).Max(), zs.Where(z => z >= a + 0.8).DefaultIfEmpty(zs[^1]).Min());

    /// <summary>What a room keeps clear of along the length, in the band bse..top above the main deck: the compartments
    /// there (those without a base stand on the inner bottom ib, without a top reach the roof) and the trunks under the
    /// turrets.</summary>
    static List<(double X0, double X1)> Obstacles(Layout lay, double bse, double top, double ib, double roof)
    {
        var busy = new List<(double, double)>();
        foreach (var c in lay.Compartments)
        {
            if (c.Kind is "citadel" or "hangar" or "handling")
                continue;   // a handling room is the deck over its magazine, shared (Ordnance.TPerM3): a torpedo room may stand in it
            double cb = c.Base ?? ib, ct = c.Top ?? roof;
            if (cb < top - 0.05 && bse + 0.05 < ct)
                busy.Add((c.X0, c.X1));
        }
        foreach (var m in lay.Mounts)
            if (m.T.HasBarbette && m.Kind is "main" or "secondary" && !m.Casemate && Math.Abs(m.Y) < lay.Hull.HalfWidth(m.X))
            {
                double r = Geometry.BarbetteR(m.T);
                busy.Add((m.X - r, m.X + r));
            }
        return busy;
    }

    /// <summary>The tubes run out through the hull's form: a beam tube from the shell inboard, its length long (or as far
    /// as the far side allows); a bow or stern tube from its room's end to where the hull at its height ends, its breech
    /// a tube's length back within the room as the subdivision gave it its cells. D: the hull's depth (layout heights
    /// are above the main deck, the form's above the keel).</summary>
    public static List<TubeRun> Build(Layout lay, HullForm form, double D, SubdivisionData sub)
    {
        var runs = new List<TubeRun>(lay.Tubes.Count);
        var rooms = new Dictionary<string, (double X0, double X1)>(StringComparer.Ordinal);
        foreach (var c in lay.Compartments)
            if (c.Kind == "torpedo_room")
                rooms[c.Id] = (c.X0, c.X1);
        foreach (var r in sub.Rooms)
            if (r.Kind == "torpedo_room" && r.Cells.Count > 0)
                rooms[r.Id] = (r.X0, r.X1);
        foreach (var t in lay.Tubes)
        {
            double zk = t.Z + D;
            var room = rooms[t.Room];
            (double X, double Y) p0, p1;
            if (t.Kind == "beam")
            {
                double hw = form.HalfWidth(t.X, zk);
                double inner = Math.Max(hw - TubeLen, -(hw - 1.0));
                p1 = (t.X, t.Y * hw);
                p0 = (t.X, t.Y * inner);
            }
            else
            {
                int dir = t.Kind == "bow" ? 1 : -1;
                double end = dir > 0 ? lay.Hull.L / 2 : -lay.Hull.L / 2, x = t.X;
                double step = 0.1 * dir;
                while (dir * (end - x) > 0 && form.HalfWidth(x + step, zk) > Math.Abs(t.Y) + TubeR + 0.1)
                    x += step;
                double breech = Math.Clamp(x - dir * TubeLen, room.X0 + 0.5, room.X1 - 0.5);
                p1 = (x, t.Y);
                p0 = (breech, t.Y);
            }
            runs.Add(new TubeRun(t, (p0.X, p0.Y, t.Z), (p1.X, p1.Y, t.Z)));
        }
        return runs;
    }

    /// <summary>The tubes as hitbox components: segments from breech (P0) to muzzle (P1), fixed on their bearing.</summary>
    public static IEnumerable<Component> Components(IEnumerable<TubeRun> runs)
    {
        static List<Pt> Plan((double X, double Y, double Z) a, (double X, double Y, double Z) b)
        {
            double x0 = Math.Min(a.X, b.X) - TubeR, x1 = Math.Max(a.X, b.X) + TubeR;
            double y0 = Math.Min(a.Y, b.Y) - TubeR, y1 = Math.Max(a.Y, b.Y) + TubeR;
            return [new(Math.Round(x0, 3), Math.Round(y0, 3)), new(Math.Round(x1, 3), Math.Round(y0, 3)),
                new(Math.Round(x1, 3), Math.Round(y1, 3)), new(Math.Round(x0, 3), Math.Round(y1, 3))];
        }
        foreach (var r in runs)
        {
            var (t, a, b) = (r.Tube, r.P0, r.P1);
            yield return new Component
            {
                Id = t.Id, Kind = "torpedo_tube", Shape = "segment", Position = t.Kind, Room = t.Room, CalibreMm = CalibreMm,
                P0 = [Math.Round(a.X, 3), Math.Round(a.Y, 3), Math.Round(a.Z, 2)], P1 = [Math.Round(b.X, 3), Math.Round(b.Y, 3), Math.Round(b.Z, 2)],
                R = TubeR, Points = Plan(a, b), Base = Math.Round(t.Z - TubeR, 2), Top = Math.Round(t.Z + TubeR, 2),
                Rotating = false, RestDeg = t.Bearing, ArcsDeg = [[t.Bearing - Arcs.ArcFixed, t.Bearing + Arcs.ArcFixed]],
                Torpedoes = 1, WarheadKg = (long)Math.Round(Ordnance.WarheadKg(CalibreMm)),
            };
        }
    }
}

/// <summary>A submerged tube as the layout plans it: Kind "beam", "bow" or "stern"; X its station (a beam tube's) or
/// its room's end (bow, stern); Y the side (beam: +1 starboard, -1 port) or its offset from the centreline; Z its
/// axis above the main deck; Bearing its fixed bearing.</summary>
public sealed record SubmergedTube(string Id, string Kind, string Room, double X, double Y, double Z, double Bearing);

/// <summary>A tube run out through the hull: P0 the breech, P1 the muzzle at the shell, heights above the main deck.</summary>
public sealed record TubeRun(SubmergedTube Tube, (double X, double Y, double Z) P0, (double X, double Y, double Z) P1);
