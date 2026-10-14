using System.Collections.Frozen;
using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>Compiles a built Ship into a ShipClass: every kind read into an enum, every string link into an index, every
/// list into a slice of a pool. Strict: an unknown kind, a link that doesn't resolve or a duplicate id is a
/// ShipCompileException, never a guess. Deterministic: the same build gives the same class, index for index.</summary>
public static class ShipClassCompiler
{
    public static ShipClass Compile(Ship ship) => new Compilation(ship).Run();

    sealed class Compilation(Ship ship)
    {
        readonly Hitboxes hb = ship.Hitboxes;
        readonly List<int> links = [];
        readonly List<Pt> points = [];
        readonly List<double> numbers = [];
        readonly Dictionary<string, int> materials = new(StringComparer.Ordinal);
        readonly List<string> materialNames = [];
        Dictionary<string, int> comp = null!, cell = null!, room = null!, bulkhead = null!, deck = null!, tier = null!, section = null!,
            turretType = null!;
        string where = "";

        public ShipClass Run()
        {
            comp = Index(hb.Components.Select(c => c.Id), "component");
            cell = Index(hb.Cells.Select(c => c.Id), "cell");
            room = Index(hb.Rooms.Select(r => r.Id), "room");
            bulkhead = Index(hb.Bulkheads.Select(b => b.Id), "bulkhead");
            deck = Index(hb.Decks.Select(d => d.Id), "deck");
            tier = Index(hb.Tiers.Select(t => t.Id), "tier");
            section = Index(hb.Sections.Select(s => s.Id), "section");
            turretType = Index(hb.TurretTypes.Keys, "turret type");
            foreach (var d in hb.Decks)
                if (bulkhead.ContainsKey(d.Id))
                    throw new ShipCompileException($"\"{d.Id}\" is both a deck and a bulkhead");

            var v = hb.Vertical;
            var r = ship.Report;
            var c = new ShipClass
            {
                Name = ship.Design.Name ?? r.Name ?? "",
                DesignId = ship.Design.Id,
                DesignHash = ContentHash.OfDesign(ship.Design),
                ShipgenVersion = ShipDesign.Version,
                Hull = new HullField(hb.HullForm, v),
                Length = hb.Length,
                Beam = hb.Beam,
                Vertical = new VerticalRow(v.Keel, v.Waterline, v.ArmourDeck ?? double.NaN, v.Draught, v.Depth, v.Freeboard, v.Knuckle ?? double.NaN),
                DeckOutline = AddPoints(hb.Hull),
                Hydrostatics = HydrostaticsOf(hb.Hydrostatics),
                Motion = Motion(),
            };
            c.turretTypes = hb.TurretTypes.Values.Select(TurretTypeOf).ToArray();
            c.turretTypeIds = [.. hb.TurretTypes.Keys];
            Components(c);
            Cells(c);
            c.rooms = hb.Rooms.Select(RoomOf).ToArray();
            c.bulkheads = hb.Bulkheads.Select(BulkheadOf).ToArray();
            Decks(c);
            c.tiers = hb.Tiers.Select(TierOf).ToArray();
            c.sections = hb.Sections.Select(s => new SectionRow(s.X0, s.X1)).ToArray();
            Armour(c);
            c.Plating = PlatingOf(hb.Plating);
            Weapons(c);
            (c.powerKn, c.powerShp) = PowerCurve(c.Motion);
            c.eyes = Sight.Eyes(c);
            c.Silhouette = Sight.Silhouette(c, numbers);

            c.links = links.ToArray();
            c.points = points.ToArray();
            c.numbers = numbers.ToArray();
            c.materials = materialNames.ToArray();
            c.componentIds = [.. hb.Components.Select(x => x.Id)];
            c.cellIds = [.. hb.Cells.Select(x => x.Id)];
            c.roomIds = [.. hb.Rooms.Select(x => x.Id)];
            c.bulkheadIds = [.. hb.Bulkheads.Select(x => x.Id)];
            c.deckIds = [.. hb.Decks.Select(x => x.Id)];
            c.tierIds = [.. hb.Tiers.Select(x => x.Id)];
            c.sectionIds = [.. hb.Sections.Select(x => x.Id)];
            (c.componentIndex, c.cellIndex, c.roomIndex, c.bulkheadIndex, c.deckIndex, c.tierIndex, c.sectionIndex) = (comp.ToFrozenDictionary(StringComparer.Ordinal),
                cell.ToFrozenDictionary(StringComparer.Ordinal), room.ToFrozenDictionary(StringComparer.Ordinal), bulkhead.ToFrozenDictionary(StringComparer.Ordinal),
                deck.ToFrozenDictionary(StringComparer.Ordinal), tier.ToFrozenDictionary(StringComparer.Ordinal), section.ToFrozenDictionary(StringComparer.Ordinal));
            c.Solids = new ShipSolids(c);
            FunctionTable.Build(c);
            c.LayoutHash = LayoutHash(c);
            return c;
        }

        // ------------------------------------------------------------------ resolving

        static Dictionary<string, int> Index(IEnumerable<string> ids, string what)
        {
            var d = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var id in ids)
                if (!d.TryAdd(id, d.Count))
                    throw new ShipCompileException($"two {what}s are called \"{id}\"");
            return d;
        }

        int Resolve(Dictionary<string, int> map, string? id, string what)
        {
            if (id is null)
                return -1;
            return map.TryGetValue(id, out int i) ? i : throw new ShipCompileException($"{where}: no {what} \"{id}\"");
        }

        int Comp(string? id) => Resolve(comp, id, "component");
        int Room(string? id) => Resolve(room, id, "room");
        int Deck(string? id) => Resolve(deck, id, "deck");
        int Section(string? id) => Resolve(section, id, "section");

        /// <summary>A component link that must be of a kind.</summary>
        int Comp(string? id, string kind)
        {
            int i = Comp(id);
            if (i >= 0 && hb.Components[i].Kind != kind)
                throw new ShipCompileException($"{where}: \"{id}\" is a {hb.Components[i].Kind}, not a {kind}");
            return i;
        }

        Slice AddLinks(IEnumerable<string>? ids, Dictionary<string, int> map, string what)
        {
            int start = links.Count;
            foreach (var id in ids ?? [])
                links.Add(Resolve(map, id, what));
            return new Slice(start, links.Count - start);
        }

        Slice AddPoints(IEnumerable<Pt>? pts)
        {
            int start = points.Count;
            points.AddRange(pts ?? []);
            return new Slice(start, points.Count - start);
        }

        Slice AddPairs(IEnumerable<double[]>? pairs)
        {
            int start = numbers.Count;
            foreach (var p in pairs ?? [])
            {
                if (p.Length != 2)
                    throw new ShipCompileException($"{where}: a pair of {p.Length} numbers");
                numbers.Add(p[0]);
                numbers.Add(p[1]);
            }
            return new Slice(start, numbers.Count - start);
        }

        int Material(string? name)
        {
            if (name is null)
                return -1;
            if (!materials.TryGetValue(name, out int i))
            {
                materials[name] = i = materialNames.Count;
                materialNames.Add(name);
            }
            return i;
        }

        P3 Point3(double[]? p) => p switch
        {
            null => new P3(double.NaN, double.NaN, double.NaN),
            [var x, var y, var z] => new P3(x, y, z),
            _ => throw new ShipCompileException($"{where}: a point of {p.Length} numbers"),
        };

        static double Or(double? v) => v ?? double.NaN;

        static int Int(long? v) => checked((int)(v ?? 0));

        // ------------------------------------------------------------------ the components

        void Components(ShipClass c)
        {
            int n = hb.Components.Count;
            var rows = new ComponentRow[n];
            var mounts = new List<MountRow>();
            var barbettes = new List<BarbetteRow>();
            var funnels = new List<FunnelRow>();
            var shafts = new List<ShaftLineRow>();
            var rudders = new List<RudderRow>();
            var directors = new List<DirectorRow>();
            var tubes = new List<TubeRow>();
            for (int i = 0; i < n; i++)
            {
                var x = hb.Components[i];
                where = $"component \"{x.Id}\"";
                var kind = Kinds.Component(x.Kind);
                var shape = Kinds.Shape(x.Shape);
                bool mount = kind is ComponentKind.Main or ComponentKind.Secondary or ComponentKind.Torpedo;
                if (mount != (x.Local != null))
                    throw new ShipCompileException(mount ? $"{where}: a mount with no turret shape" : $"{where}: a turret shape on a {x.Kind}");
                if (x.Mount != null && kind != ComponentKind.Barbette)
                    throw new ShipCompileException($"{where}: a mount link on a {x.Kind}");
                int detail = -1;
                var outline = AddPoints(x.Points);
                if (mount)
                {
                    detail = mounts.Count;
                    var t = hb.TurretTypes.GetValueOrDefault(x.Type ?? "") ?? throw new ShipCompileException($"{where}: no turret type \"{x.Type}\"");
                    var traverse = x.TraverseDeg;
                    if (traverse is { Length: not 2 })
                        throw new ShipCompileException($"{where}: a traverse of {traverse.Length} numbers");
                    mounts.Add(new MountRow
                    {
                        Component = i, Kind = kind, Type = turretType[x.Type!], CalibreMm = Or(x.CalibreMm ?? t.CalibreMm),
                        CalibreLength = Or(x.CalibreLength ?? t.CalibreLength), X = x.X ?? 0, Y = x.Y ?? 0, RestDeg = x.RestDeg ?? 0,
                        Rotating = x.Rotating ?? false, Casemate = x.Casemate ?? false, BroadphaseR = Or(x.BroadphaseR), Arcs = AddPairs(x.ArcsDeg),
                        TraverseFromDeg = traverse?[0] ?? double.NaN, TraverseToDeg = traverse?[1] ?? double.NaN, Body = AddPoints(x.Local!.Body),
                        Turret = x.Armour != null, FaceMm = x.Armour?.Face ?? 0, SideMm = x.Armour?.Side ?? 0, RearMm = x.Armour?.Rear ?? 0,
                        RoofMm = x.Armour?.Roof ?? 0, Magazine = Room(x.Magazine), Barbette = Comp(x.Barbette, "barbette"), Rounds = Int(x.Rounds),
                        ReadyRounds = Int(x.ReadyRounds), ReadyT = Or(x.ReadyT), Torpedoes = Int(x.Torpedoes), WarheadKg = Or(x.WarheadKg),
                    });
                }
                else
                    switch (kind)
                    {
                        case ComponentKind.Barbette:
                            detail = barbettes.Count;
                            barbettes.Add(new BarbetteRow(i, Comp(x.Mount), x.X ?? 0, x.Y ?? 0, Or(x.R)));
                            break;
                        case ComponentKind.Funnel:
                            detail = funnels.Count;
                            funnels.Add(new FunnelRow(i, -1, Int(x.Pipes), AddLinks(x.BoilerRooms, room, "room")));
                            break;
                        case ComponentKind.Uptake:
                            AddLinks(x.BoilerRooms, room, "room");   // resolved, not kept: the funnel's
                            break;
                        case ComponentKind.Shaft:
                            detail = shafts.Count;
                            int prop = Comp(x.Propeller, "propeller");
                            shafts.Add(new ShaftLineRow
                            {
                                Shaft = i, Propeller = prop, Alley = Comp(x.Alley, "shaft_alley"), EngineRoom = Room(x.EngineRoom),
                                Position = Kinds.PositionOf(x.Position), LeavesHullX = Or(x.LeavesHullX), P0 = Point3(x.P0), P1 = Point3(x.P1), R = Or(x.R),
                                PropellerDiameterM = prop >= 0 ? Or(hb.Components[prop].DiameterM) : double.NaN,
                            });
                            break;
                        case ComponentKind.ShaftAlley or ComponentKind.Propeller:
                            Comp(x.Shaft, "shaft");
                            break;
                        case ComponentKind.Rudder:
                            detail = rudders.Count;
                            rudders.Add(new RudderRow(i, x.X ?? 0, x.Y ?? 0, Or(x.AreaM2), Room(x.Steering)));
                            break;
                        case ComponentKind.TorpedoTube:
                            detail = tubes.Count;
                            tubes.Add(new TubeRow
                            {
                                Component = i, Room = Room(x.Room), Position = Kinds.PositionOf(x.Position), CalibreMm = Or(x.CalibreMm),
                                Torpedoes = Int(x.Torpedoes), WarheadKg = Or(x.WarheadKg), RestDeg = x.RestDeg ?? 0, Rotating = x.Rotating ?? false,
                                Arcs = AddPairs(x.ArcsDeg), P0 = Point3(x.P0), P1 = Point3(x.P1), R = Or(x.R),
                            });
                            break;
                    }
                var role = Kinds.Role(x.Role);
                if (role == ComponentRole.Director)
                {
                    if (kind != ComponentKind.Superstructure)
                        throw new ShipCompileException($"{where}: a director that is a {x.Kind}");
                    detail = directors.Count;
                    directors.Add(new DirectorRow(i, Kinds.BatteryOf(x.Battery), Or(x.RangefinderM), x.Radar ?? false));
                }
                else if (x.Battery != null)
                    throw new ShipCompileException($"{where}: a battery on a {x.Kind} that isn't a director");
                int smoke = links.Count;
                foreach (var f in x.Smoke ?? [])
                    links.Add(Comp(f, "funnel"));
                rows[i] = new ComponentRow
                {
                    Kind = kind, Role = role, Shape = shape, Level = Int(x.Level), Bounds = BoundsOf(x, outline, mount), X = x.X ?? double.NaN,
                    Y = x.Y ?? double.NaN, Z = x.Z ?? double.NaN, R = Or(x.R), Outline = outline, ArmourMm = x.ArmourMm ?? 0, Material = Material(x.Material),
                    PlateMm = x.PlateMm ?? 0, WoodMm = x.WoodMm ?? 0, Crew = Int(x.Crew), BattleCrew = Int(x.BattleCrew),
                    Smoke = new Slice(smoke, links.Count - smoke), Detail = detail,
                };
            }

            // the links both ways round: an uptake's funnel, a barbette's gun, a propeller's and alley's shaft
            for (int i = 0; i < n; i++)
            {
                var x = hb.Components[i];
                where = $"component \"{x.Id}\"";
                switch (rows[i].Kind)
                {
                    case ComponentKind.Uptake:
                        int f = Comp(x.Funnel, "funnel");
                        if (f < 0)
                            throw new ShipCompileException($"{where}: an uptake with no funnel");
                        var fr = funnels[rows[f].Detail];
                        if (fr.Uptake >= 0)
                            throw new ShipCompileException($"{where}: funnel \"{x.Funnel}\" has two uptakes");
                        funnels[rows[f].Detail] = fr with { Uptake = i };
                        rows[i] = rows[i] with { Detail = rows[f].Detail };
                        break;
                    case ComponentKind.Barbette:
                        var b = barbettes[rows[i].Detail];
                        if (b.Mount < 0 || hb.Components[b.Mount].Barbette != x.Id)
                            throw new ShipCompileException($"{where}: its mount \"{x.Mount}\" doesn't stand on it");
                        break;
                    case ComponentKind.ShaftAlley or ComponentKind.Propeller:
                        int s = Comp(x.Shaft, "shaft");
                        if (s < 0)
                            throw new ShipCompileException($"{where}: no shaft");
                        var sr = shafts[rows[s].Detail];
                        if ((rows[i].Kind == ComponentKind.Propeller ? sr.Propeller : sr.Alley) != i)
                            throw new ShipCompileException($"{where}: shaft \"{x.Shaft}\" doesn't lead to it");
                        rows[i] = rows[i] with { Detail = rows[s].Detail };
                        break;
                }
            }
            foreach (var m in mounts)
                if (m.Barbette >= 0 && barbettes[rows[m.Barbette].Detail].Mount != m.Component)
                    throw new ShipCompileException($"mount \"{hb.Components[m.Component].Id}\": its barbette carries another mount");
            c.components = rows;
            (c.mounts, c.barbettes, c.funnels, c.shaftLines, c.rudders, c.directors, c.tubes) =
                (mounts.ToArray(), barbettes.ToArray(), funnels.ToArray(), shafts.ToArray(), rudders.ToArray(), directors.ToArray(), tubes.ToArray());
        }

        /// <summary>The plan's extent: the outline's, a circle's or disc's square, a mount's turret body at rest, a
        /// segment's ends widened by its radius.</summary>
        Box BoundsOf(Component x, Slice outline, bool mount)
        {
            double x0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y0 = double.PositiveInfinity, y1 = double.NegativeInfinity;
            void Add(double px, double py) => (x0, x1, y0, y1) = (Math.Min(x0, px), Math.Max(x1, px), Math.Min(y0, py), Math.Max(y1, py));
            for (int k = outline.Start; k < outline.End; k++)
                Add(points[k].X, points[k].Y);
            if (mount)
            {
                double cs = Math.Cos(double.DegreesToRadians(x.RestDeg ?? 0)), sn = Math.Sin(double.DegreesToRadians(x.RestDeg ?? 0));
                foreach (var p in x.Local!.Body)
                    Add((x.X ?? 0) + p.X * cs - p.Y * sn, (x.Y ?? 0) + p.X * sn + p.Y * cs);
            }
            if (x.R is double r)
            {
                if (x.X is double cx && x.Y is double cy)
                {
                    Add(cx - r, cy - r);
                    Add(cx + r, cy + r);
                }
                foreach (var p in new[] { x.P0, x.P1 })
                    if (p is [var px, var py, _])
                    {
                        Add(px - r, py - r);
                        Add(px + r, py + r);
                    }
            }
            if (x.DiameterM is double dm && x.X is double dx && x.Y is double dy)
            {
                Add(dx - dm / 2, dy - dm / 2);
                Add(dx + dm / 2, dy + dm / 2);
            }
            if (x0 > x1)
                throw new ShipCompileException($"{where}: no plan (no points, centre or turret)");
            return new Box(x0, x1, y0, y1, x.Base, x.Top);
        }

        // ------------------------------------------------------------------ the subdivision

        void Cells(ShipClass c)
        {
            var rows = new CellRow[hb.Cells.Count];
            var nb = new List<Neighbour>();
            var above = new List<ArmourLayer>();
            for (int i = 0; i < rows.Length; i++)
            {
                var x = hb.Cells[i];
                where = $"cell \"{x.Id}\"";
                int n0 = nb.Count;
                foreach (var e in x.Neighbours)
                {
                    if (e.Length != 2)
                        throw new ShipCompileException($"{where}: a neighbour entry of {e.Length} strings");
                    int other = Resolve(cell, e[0], "cell");
                    nb.Add(e[1] == "open" ? new Neighbour(other, JoinKind.Open, -1)
                        : deck.TryGetValue(e[1], out int d) ? new Neighbour(other, JoinKind.Deck, d)
                        : new Neighbour(other, JoinKind.Bulkhead, Resolve(bulkhead, e[1], "deck or bulkhead")));
                }
                int a0 = above.Count;
                var mm = x.ArmourAboveMm ?? [];
                var mat = x.ArmourAboveMaterial ?? [];
                if (mat.Count != 0 && mat.Count != mm.Count)
                    throw new ShipCompileException($"{where}: {mm.Count} armour layers above, {mat.Count} materials");
                for (int k = 0; k < mm.Count; k++)
                    above.Add(new ArmourLayer(mm[k], Material(mat.Count != 0 ? mat[k] : null)));
                if (x.Room.Length == 0)
                    throw new ShipCompileException($"{where}: no room");
                rows[i] = new CellRow
                {
                    Section = Section(x.Section), Tier = Resolve(tier, x.Tier, "tier"), Band = Kinds.BandOf(x.Band), Box = new Box(x.X0, x.X1, x.Y0, x.Y1, x.Base, x.Top),
                    BelowWaterline = x.BelowWaterline, VolumeM3 = x.VolumeM3, Permeability = x.Permeability, Citadel = x.Citadel ?? false,
                    BeltMm = x.BeltMm ?? 0, BeltMaterial = Material(x.BeltMaterial), TdsM = Or(x.TdsM), Crew = Int(x.Crew), BattleCrew = Int(x.BattleCrew),
                    Room = Room(x.Room), Also = AddLinks(x.Also, room, "room"), Neighbours = new Slice(n0, nb.Count - n0),
                    ArmourAbove = new Slice(a0, above.Count - a0), Through = AddLinks(x.Through, comp, "component"),
                };
            }
            c.cells = rows;
            c.neighbours = nb.ToArray();
            c.armourAbove = above.ToArray();
        }

        RoomRow RoomOf(Shipgen.Room x)
        {
            where = $"room \"{x.Id}\"";
            return new RoomRow
            {
                Kind = Kinds.Room(x.Kind), Fuel = Kinds.FuelOf(x.Fuel), Tonnes = Or(x.Tonnes), Mount = Comp(x.Mount), Shared = x.Shared ?? false,
                Crew = Int(x.Crew), BattleCrew = Int(x.BattleCrew), VolumeM3 = x.VolumeM3, X0 = x.X0, X1 = x.X1, Base = x.Base, Top = x.Top,
                Cells = AddLinks(x.Cells, cell, "cell"), Mounts = AddLinks(x.Mounts, comp, "component"), Tubes = AddLinks(x.Tubes, comp, "component"),
                Shafts = AddLinks(x.Shafts, comp, "component"), Rudders = AddLinks(x.Rudders, comp, "component"),
            };
        }

        BulkheadRow BulkheadOf(Shipgen.Bulkhead x)
        {
            where = $"bulkhead \"{x.Id}\"";
            return new BulkheadRow
            {
                Kind = Kinds.Bulkhead(x.Kind), X = Or(x.X), Section = Section(x.Section), Side = Kinds.SideOf(x.Side), Y = Or(x.Y), X0 = Or(x.X0),
                X1 = Or(x.X1), Base = x.Base, Top = x.Top, PlateMm = x.PlateMm ?? 0, ArmourMm = x.ArmourMm ?? 0, ArmourBottom = Or(x.ArmourBottom),
                ArmourTop = Or(x.ArmourTop), ArmourLowerMm = Or(x.ArmourLowerMm), ArmourLowerTop = Or(x.ArmourLowerTop),
                ArmourMaterial = Material(x.ArmourMaterial),
            };
        }

        void Decks(ShipClass c)
        {
            var plates = new List<DeckPlate>();
            c.decks = hb.Decks.Select(x =>
            {
                where = $"deck \"{x.Id}\"";
                int p0 = plates.Count;
                foreach (var p in x.Plates ?? [])
                    plates.Add(new DeckPlate(p.ArmourMm, p.X0, p.X1, Material(p.Material)));
                return new DeckRow
                {
                    Kind = Kinds.Deck(x.Kind), Number = Int(x.Deck), Z = x.Z, Spans = AddPairs(x.Spans), PlateMm = x.PlateMm ?? 0,
                    PlateEndMm = Or(x.PlateEndMm), WoodMm = x.WoodMm ?? 0, ArmourMm = x.ArmourMm ?? 0, X0 = Or(x.X0), X1 = Or(x.X1),
                    Material = Material(x.Material), Plates = new Slice(p0, plates.Count - p0),
                };
            }).ToArray();
            c.deckPlates = plates.ToArray();
        }

        TierRow TierOf(Shipgen.Tier x)
        {
            where = $"tier \"{x.Id}\"";
            // the bottom tier's floor is the keel, not a deck
            int floor = x.Floor == "Keel" ? -1 : Deck(x.Floor);
            return new TierRow(x.Base, x.Top, x.BelowWaterline, x.Submerged, floor, Deck(x.Ceiling), AddPairs(x.Spans));
        }

        // ------------------------------------------------------------------ the rest

        void Armour(ShipClass c)
        {
            var a = hb.Armour;
            where = "armour";
            var sides = new List<SidePlateRow>();
            var ids = new List<string>();
            if (a.Belt is { } b)
            {
                sides.Add(new SidePlateRow
                {
                    Kind = StrakeKind.Belt, Extent = Extent.Citadel, ThicknessMm = b.ThicknessMm, X0 = b.X0, X1 = b.X1, Bottom = b.Bottom, Top = b.Top,
                    Material = Material(b.Material), BottomMm = Or(b.BottomMm), TaperFrom = Or(b.TaperFrom), TipMm = double.NaN,
                });
                ids.Add("Belt");
            }
            foreach (var s in a.Strakes ?? [])
            {
                sides.Add(new SidePlateRow
                {
                    Kind = s.Kind switch
                    {
                        "end" => StrakeKind.End,
                        "upper" => StrakeKind.Upper,
                        "box" => StrakeKind.Box,
                        _ => throw new ShipCompileException($"unknown strake kind \"{s.Kind}\""),
                    },
                    Extent = ExtentOf(s.Extent), ThicknessMm = s.ThicknessMm, X0 = s.X0, X1 = s.X1, Bottom = s.Bottom, Top = s.Top,
                    Material = Material(s.Material), BottomMm = double.NaN, TaperFrom = double.NaN, TipMm = Or(s.TipMm),
                });
                ids.Add(s.Id);
            }
            Index(ids, "side plate");
            c.sidePlates = sides.ToArray();
            c.sidePlateIds = ids.ToArray();
            var bh = a.Bulkheads ?? [];
            c.armourBulkheads = bh.Select(x => new ArmourBulkheadRow(x.X, x.ThicknessMm, x.Bottom, x.Top, Material(x.Material), Or(x.LowerMm), Or(x.LowerTop)))
                .ToArray();
            c.armourBulkheadIds = [.. bh.Select(x => x.Id)];
            Index(c.armourBulkheadIds, "armour bulkhead");
            c.armourDecks = (a.Decks ?? []).Select(x =>
                new ArmourDeckRow(Deck(x.Deck), x.ThicknessMm, ExtentOf(x.Extent), x.X0, x.X1, x.Z, x.Main, x.Roof, Material(x.Material))).ToArray();
        }

        PlatingRow PlatingOf(HullPlates p) => new()
        {
            Material = Material(p.Material), ShellMm = p.ShellMm, ShellEndMm = p.ShellEndMm, StrengthDeckMm = p.StrengthDeckMm,
            StrengthDeckEndMm = p.StrengthDeckEndMm, MidX0 = p.MidX0, MidX1 = p.MidX1, DeckMm = p.DeckMm, BulkheadMm = p.BulkheadMm,
            InnerBottomMm = p.InnerBottomMm, SuperstructureMm = p.SuperstructureMm, ControlMm = p.ControlMm, DeckWoodMm = p.DeckWoodMm,
        };

        // ------------------------------------------------------------------ the weapons

        /// <summary>The guns' working numbers on each mount, the batteries (a kind of mount and a gun: one range table,
        /// one set of shells), their directors and fire-control level, and the shells they carry. What the designer
        /// doesn't give comes from DesignerPlaceholders, by the look's era and navy.</summary>
        void Weapons(ShipClass c)
        {
            var era = Kinds.EraOf(ship.Design.Look?.Era);
            c.Era = era;
            string navy = ship.Design.Look?.Navy ?? "generic";
            var mounts = c.mounts;
            var batteries = new List<BatteryRow>();
            var members = new List<List<int>>();
            var byGun = new Dictionary<(ComponentKind, double, double, Side), int>();
            for (int i = 0; i < mounts.Length; i++)
            {
                var m = mounts[i];
                where = $"mount \"{hb.Components[m.Component].Id}\"";
                if (m.Kind == ComponentKind.Torpedo)
                {
                    mounts[i] = m with
                    {
                        Battery = -1, ReloadS = DesignerPlaceholders.TorpedoReloadS(LauncherKind.Mount, era), TrainDegS = DesignerPlaceholders.TorpedoTrainDegS(era),
                        ElevateDegS = double.NaN, MaxElevationDeg = double.NaN,
                    };
                    continue;
                }
                if (!(m.CalibreMm > 0) || !(m.CalibreLength > 0))
                    throw new ShipCompileException($"{where}: a gun with no calibre or length");
                // a secondary gun's sides fight apart, each its own battery; the main battery is one
                var side = m.Kind == ComponentKind.Main ? Side.None : SideOf(m.Y);
                if (!byGun.TryGetValue((m.Kind, m.CalibreMm, m.CalibreLength, side), out int b))
                {
                    byGun[(m.Kind, m.CalibreMm, m.CalibreLength, side)] = b = batteries.Count;
                    batteries.Add(new BatteryRow
                    {
                        Kind = m.Kind == ComponentKind.Main ? Battery.Main : Battery.Secondary, Side = side, CalibreMm = m.CalibreMm,
                        CalibreLength = m.CalibreLength,
                    });
                    members.Add([]);
                }
                members[b].Add(i);
                var (reload, train, elevate, maxElev) = DesignerPlaceholders.Gun(m.CalibreMm, m.CalibreLength, c.turretTypes[m.Type].Shape, era);
                mounts[i] = m with { Battery = b, ReloadS = reload, TrainDegS = train, ElevateDegS = elevate, MaxElevationDeg = maxElev };
            }

            var types = new List<ShellTypeRow>();
            var typeIds = new List<string>();
            var typeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            var loads = new List<ShellLoad>();
            int TypeOf(ShellKind kind, BatteryRow b)
            {
                var t = DesignerPlaceholders.Shell(kind, b.CalibreMm, b.CalibreLength, era, navy);
                string id = FormattableString.Invariant($"{b.CalibreMm:0.#}/{b.CalibreLength:0.#} {KindName(kind)} {EraName(era)} {QualityName(t.Quality)}");
                if (!typeIndex.TryGetValue(id, out int k))
                {
                    typeIndex[id] = k = types.Count;
                    types.Add(t);
                    typeIds.Add(id);
                }
                return k;
            }

            for (int b = 0; b < batteries.Count; b++)
            {
                var row = batteries[b];
                int m0 = links.Count, guns = 0;
                foreach (int i in members[b])
                {
                    links.Add(i);
                    guns += c.turretTypes[mounts[i].Type].Barrels;
                }
                // its kind's directors, the longest rangefinder first: a side's, its own side's and the centreline's (all
                // of its kind if that leaves none); the centreline's, all of them
                int d0 = links.Count;
                double rf = 0;
                bool radar = false;
                bool Serves(int i) => row.Side is Side.None or Side.Centre || SideOf(Mid(c.components[c.directors[i].Component].Bounds)) is var s && (s == row.Side || s == Side.Centre);
                static double Mid(Box b) => (b.Y0 + b.Y1) / 2;
                var mine = Enumerable.Range(0, c.directors.Length).Where(i => c.directors[i].Battery == row.Kind).ToList();
                if (mine.Any(Serves))
                    mine.RemoveAll(i => !Serves(i));
                foreach (int d in mine.OrderByDescending(i => c.directors[i].RangefinderM > 0 ? c.directors[i].RangefinderM : 0).ThenBy(i => i))
                {
                    links.Add(d);
                    if (c.directors[d].RangefinderM > rf)
                        rf = c.directors[d].RangefinderM;
                    radar |= c.directors[d].Radar;
                }
                int dirs = links.Count - d0;
                var spec = FireControl.Spec(ship.Design, row.Kind == Battery.Main ? "main" : "secondary");
                int l0 = loads.Count;
                foreach (var (kind, share) in DesignerPlaceholders.ShellMix(row.CalibreMm, era))
                    loads.Add(new ShellLoad(TypeOf(kind, row), share));
                batteries[b] = row with
                {
                    Mounts = new Slice(m0, d0 - m0), Guns = guns, Directors = new Slice(d0, dirs),
                    Level = DesignerPlaceholders.FireControl(dirs, rf, spec.ComputerT, radar || spec.RadarT > 0, era),
                    Shells = new Slice(l0, loads.Count - l0), Fires = TypeOf(DesignerPlaceholders.Fires(row.CalibreMm, era), row),
                    PatternPct = DesignerPlaceholders.Pattern(row.CalibreMm, era),
                };
            }
            c.batteries = batteries.ToArray();
            c.shellTypes = types.ToArray();
            c.shellTypeIds = typeIds.ToArray();
            c.shellLoads = loads.ToArray();
            Launchers(c, era);
        }

        /// <summary>The torpedo mounts and the submerged tubes as one table, and the torpedoes they fire (docs/battle/
        /// fighting.md 4.6): a mount's torpedoes go in at its pivot on the waterline, a tube's at its muzzle.</summary>
        void Launchers(ShipClass c, Era era)
        {
            var launchers = new List<LauncherRow>();
            var torpedoes = new List<TorpedoRow>();
            int TorpedoOf(double calibreMm, double warheadKg)
            {
                var t = DesignerPlaceholders.Torpedo(calibreMm, warheadKg, era);
                int k = torpedoes.IndexOf(t);
                if (k < 0)
                {
                    k = torpedoes.Count;
                    torpedoes.Add(t);
                }
                return k;
            }
            double wl = c.Vertical.Waterline;
            for (int i = 0; i < c.mounts.Length; i++)
            {
                ref readonly var m = ref c.mounts[i];
                if (m.Kind != ComponentKind.Torpedo || m.Torpedoes <= 0)
                    continue;
                launchers.Add(new LauncherRow
                {
                    Kind = LauncherKind.Mount, Index = i, Component = m.Component, X = m.X, Y = m.Y, Z = wl, Arcs = m.Arcs, Trains = m.Rotating,
                    RestDeg = m.RestDeg, Tubes = Math.Max(c.turretTypes[m.Type].Barrels, 1), Torpedoes = m.Torpedoes, ReloadS = m.ReloadS,
                    Torpedo = TorpedoOf(m.CalibreMm, m.WarheadKg),
                });
            }
            for (int i = 0; i < c.tubes.Length; i++)
            {
                ref readonly var t = ref c.tubes[i];
                if (t.Torpedoes <= 0)
                    continue;
                launchers.Add(new LauncherRow
                {
                    Kind = LauncherKind.Tube, Index = i, Component = t.Component, X = t.P1.X, Y = t.P1.Y, Z = t.P1.Z, Arcs = t.Arcs, Trains = t.Rotating,
                    RestDeg = t.RestDeg, Tubes = 1, Torpedoes = t.Torpedoes, ReloadS = DesignerPlaceholders.TorpedoReloadS(LauncherKind.Tube, era),
                    Torpedo = TorpedoOf(t.CalibreMm, t.WarheadKg),
                });
            }
            c.launchers = launchers.ToArray();
            c.torpedoes = torpedoes.ToArray();
        }

        /// <summary>Within this of the centreline, a mount or director stands on it, m.</summary>
        const double CentrelineM = 0.5;

        /// <summary>The side a point athwartships stands on (+y starboard); none given is the centreline.</summary>
        static Side SideOf(double y) => !(Math.Abs(y) >= CentrelineM) ? Side.Centre : y > 0 ? Side.Starboard : Side.Port;

        static string KindName(ShellKind k) => k switch { ShellKind.Ap => "AP", ShellKind.Common => "common", _ => "HE" };

        static string EraName(Era e) => e switch
        {
            Era.Victorian => "victorian",
            Era.GreatWar => "great_war",
            Era.Treaty => "treaty",
            Era.Wwii => "wwii",
            _ => "cold_war",
        };

        static string QualityName(ShellQuality q) => q switch
        {
            ShellQuality.Rn1916 => "rn1916",
            ShellQuality.Km1916 => "km1916",
            ShellQuality.Rn1918 => "rn1918",
            ShellQuality.Ww2 => "ww2",
            _ => "ijn_t91",
        };

        static Extent ExtentOf(string s) => s switch
        {
            "citadel" => Extent.Citadel,
            "fore" => Extent.Fore,
            "aft" => Extent.Aft,
            "full" => Extent.Full,
            "steering" => Extent.Steering,
            _ => throw new ShipCompileException($"unknown armour extent \"{s}\""),
        };

        TurretTypeRow TurretTypeOf(Shipgen.TurretType t) => new()
        {
            Shape = t.Shape switch
            {
                "bb" => TurretShape.Bb,
                "dp" => TurretShape.Dp,
                "open" => TurretShape.Open,
                "casemate" => TurretShape.Casemate,
                "torp" => TurretShape.Torp,
                "tube" => TurretShape.Tube,
                _ => throw new ShipCompileException($"unknown turret shape \"{t.Shape}\""),
            },
            R = t.R, Barrels = checked((int)t.Barrels), BarrelLen = t.BarrelLen, BarrelW = t.BarrelW, Spacing = t.Spacing, CalibreMm = t.CalibreMm,
            CalibreLength = Or(t.CalibreLength),
        };

        static HydrostaticsRow HydrostaticsOf(Shipgen.Hydrostatics h) => new()
        {
            DisplacementT = h.DisplacementT, VolumeM3 = h.VolumeM3, WaterplaneM2 = h.WaterplaneM2, Lcf = h.Lcf, Lcg = h.Lcg, Lcb = h.Lcb, Kg = h.Kg,
            Kb = h.Kb, GmT = h.GmT, GmL = h.GmL, ITM4 = h.ITM4, ILM4 = h.ILM4, TpcT = h.TpcT, MctTm = h.MctTm,
        };

        MotionFacts Motion()
        {
            var r = ship.Report;
            var res = r.Results;
            double area = 0;
            int n = 0;
            foreach (var x in hb.Components)
                if (x.Kind == "rudder")
                {
                    n++;
                    area += x.AreaM2 ?? 0;
                }
            var facts = new MotionFacts
            {
                LengthM = res.LengthM, BeamM = res.BeamM, DraughtM = res.DraughtM, BlockCoefficient = res.BlockCoefficient,
                FullDisplacementT = res.FullDisplacementT, StandardDisplacementT = res.StandardDisplacementT,
                MaxSpeedKn = ship.Design.SpeedKn ?? throw new ShipCompileException("the design has no speed"), PowerShp = res.PowerShp,
                RatedShp = r.Plant.RatedShp, ContinuousShp = r.Plant.ContinuousKw / 0.7457, OverloadMax = r.Plant.OverloadMax,
                Shafts = checked((int)r.Plant.Shafts), Rudders = n, RudderAreaM2 = area, GmFullM = res.GmFullM, RollPeriodS = res.RollPeriodS,
            };
            return DesignerPlaceholders.Fill(facts);
        }

        /// <summary>The power the hull needs, from shipgen's own resistance model at full load (the report's PowerShp
        /// is the same at the designed speed, rounded to 100 shp); to a quarter past the designed speed, which covers
        /// the plant's overload.</summary>
        (double[], double[]) PowerCurve(MotionFacts m)
        {
            var tun = Styles.Get(ship.Design).Tuning(ship.Design);
            double P(double kn) => Navarch.PowerRequired(m.FullDisplacementT, kn, m.LengthM, m.BeamM, m.BlockCoefficient, tun);
            int n = (int)Math.Ceiling(1.25 * m.MaxSpeedKn / ShipClass.PowerStepKn) + 1;
            var kn = new double[n];
            var shp = new double[n];
            for (int i = 0; i < n; i++)
            {
                kn[i] = i * ShipClass.PowerStepKn;
                shp[i] = i == 0 ? 0 : P(kn[i]);
                if (i > 0 && !(shp[i] > shp[i - 1]))
                    throw new ShipCompileException($"the power curve doesn't rise at {kn[i]} kn");
            }
            return (kn, shp);
        }

        // ------------------------------------------------------------------ the layout hash

        static ContentHash LayoutHash(ShipClass c)
        {
            using var h = new ContentHash.Builder();
            h.Add(c.ShipgenVersion);
            h.Add(c.components.Length);
            for (int i = 0; i < c.components.Length; i++)
            {
                ref readonly var r = ref c.components[i];
                h.Add(c.componentIds[i]);
                h.Add((int)r.Kind);
                h.Add((int)r.Role);
                h.Add(r.Detail);
                h.Add(c.Links(r.Smoke));
            }
            h.Add(c.turretTypeIds.Length);
            foreach (var id in c.turretTypeIds)
                h.Add(id);
            h.Add(c.mounts.Length);
            foreach (var m in c.mounts)
            {
                h.Add(m.Component);
                h.Add(m.Type);
                h.Add(m.Magazine);
                h.Add(m.Barbette);
            }
            foreach (var b in c.barbettes)
                h.Add(b.Mount);
            h.Add(c.funnels.Length);
            foreach (var f in c.funnels)
            {
                h.Add(f.Component);
                h.Add(f.Uptake);
                h.Add(c.Links(f.BoilerRooms));
            }
            h.Add(c.shaftLines.Length);
            foreach (var s in c.shaftLines)
            {
                h.Add(s.Shaft);
                h.Add(s.Propeller);
                h.Add(s.Alley);
                h.Add(s.EngineRoom);
            }
            h.Add(c.rudders.Length);
            foreach (var r in c.rudders)
                h.Add(r.Steering);
            h.Add(c.directors.Length);
            foreach (var d in c.directors)
                h.Add(d.Component);
            h.Add(c.batteries.Length);
            foreach (var b in c.batteries)
            {
                h.Add((int)b.Kind);
                h.Add((int)b.Side);
                h.Add(c.Links(b.Mounts));
                h.Add(c.Links(b.Directors));
                h.Add(b.Shells.Count);
                foreach (var l in c.ShellLoads(b.Shells))
                    h.Add(l.ShellType);
                h.Add(b.Fires);
            }
            h.Add(c.shellTypeIds.Length);
            foreach (var id in c.shellTypeIds)
                h.Add(id);
            h.Add(c.tubes.Length);
            foreach (var t in c.tubes)
                h.Add(t.Room);
            h.Add(c.cells.Length);
            for (int i = 0; i < c.cells.Length; i++)
            {
                ref readonly var r = ref c.cells[i];
                h.Add(c.cellIds[i]);
                h.Add(r.Section);
                h.Add(r.Tier);
                h.Add((int)r.Band);
                h.Add(r.Room);
                h.Add(c.Links(r.Also));
                h.Add(c.Links(r.Through));
                h.Add(r.Neighbours.Count);
                foreach (var e in c.Neighbours(r.Neighbours))
                {
                    h.Add(e.Cell);
                    h.Add((int)e.Join);
                    h.Add(e.Through);
                }
            }
            h.Add(c.rooms.Length);
            for (int i = 0; i < c.rooms.Length; i++)
            {
                ref readonly var r = ref c.rooms[i];
                h.Add(c.roomIds[i]);
                h.Add((int)r.Kind);
                h.Add((int)r.Fuel);
                h.Add(r.Mount);
                h.Add(c.Links(r.Cells));
                h.Add(c.Links(r.Mounts));
                h.Add(c.Links(r.Tubes));
                h.Add(c.Links(r.Shafts));
                h.Add(c.Links(r.Rudders));
            }
            h.Add(c.bulkheads.Length);
            for (int i = 0; i < c.bulkheads.Length; i++)
            {
                h.Add(c.bulkheadIds[i]);
                h.Add((int)c.bulkheads[i].Kind);
                h.Add(c.bulkheads[i].Section);
                h.Add((int)c.bulkheads[i].Side);
            }
            h.Add(c.decks.Length);
            for (int i = 0; i < c.decks.Length; i++)
            {
                h.Add(c.deckIds[i]);
                h.Add((int)c.decks[i].Kind);
                h.Add(c.decks[i].Number);
            }
            h.Add(c.tiers.Length);
            for (int i = 0; i < c.tiers.Length; i++)
            {
                h.Add(c.tierIds[i]);
                h.Add(c.tiers[i].Floor);
                h.Add(c.tiers[i].Ceiling);
            }
            h.Add(c.sectionIds.Length);
            foreach (var id in c.sectionIds)
                h.Add(id);
            h.Add(c.sidePlateIds.Length);
            for (int i = 0; i < c.sidePlates.Length; i++)
            {
                h.Add(c.sidePlateIds[i]);
                h.Add((int)c.sidePlates[i].Kind);
            }
            h.Add(c.armourBulkheadIds.Length);
            foreach (var id in c.armourBulkheadIds)
                h.Add(id);
            h.Add(c.armourDecks.Length);
            foreach (var d in c.armourDecks)
                h.Add(d.Deck);
            h.Add(c.materials.Length);
            foreach (var m in c.materials)
                h.Add(m);
            return h.Finish();
        }
    }
}
