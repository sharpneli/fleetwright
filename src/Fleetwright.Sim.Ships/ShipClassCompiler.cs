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
                ShellJoint = r.Hull.Shell,
                InternalsJoint = r.Hull.Internals,
                ShockGrade = r.Fittings.ShockGrade,
                Drainage = DrainageOf(DrainageTemplates.Of(ship.Design), hb.Hydrostatics.DisplacementT),
                Boilers = Contents.BoilersOf(r.Plant.Boilers),
                Engines = Contents.EnginesOf(r.Plant.Engines),
            };
            c.componentIds = [.. hb.Components.Select(x => x.Id)];
            c.turretTypes = hb.TurretTypes.Values.Select(TurretTypeOf).ToArray();
            c.turretTypeIds = [.. hb.TurretTypes.Keys];
            Components(c);
            Cells(c);
            c.rooms = hb.Rooms.Select(RoomOf).ToArray();
            c.bulkheads = hb.Bulkheads.Select(BulkheadOf).ToArray();
            Decks(c);
            c.tiers = hb.Tiers.Select(TierOf).ToArray();
            c.sections = hb.Sections.Select(s => new SectionRow(s.X0, s.X1)).ToArray();
            FireSpaces(c);
            Armour(c);
            c.Plating = PlatingOf(hb.Plating);
            Weapons(c);
            (c.powerKn, c.powerShp) = PowerCurve(c.Motion);
            c.eyes = Sight.Eyes(c);
            TurretRangefinders(c);
            FireControlUnits(c);
            c.Silhouette = Sight.Silhouette(c, numbers);
            RoomContents(c);
            RoomStowages(c);
            PartContents(c, objects);

            c.links = links.ToArray();
            c.points = points.ToArray();
            c.numbers = numbers.ToArray();
            c.materials = materialNames.ToArray();
            c.materialRows = [.. materialNames.Select(MaterialOf)];
            c.cellIds = [.. hb.Cells.Select(x => x.Id)];
            c.roomIds = [.. hb.Rooms.Select(x => x.Id)];
            c.bulkheadIds = [.. hb.Bulkheads.Select(x => x.Id)];
            c.deckIds = [.. hb.Decks.Select(x => x.Id)];
            c.tierIds = [.. hb.Tiers.Select(x => x.Id)];
            c.sectionIds = [.. hb.Sections.Select(x => x.Id)];
            (c.componentIndex, c.cellIndex, c.roomIndex, c.bulkheadIndex, c.deckIndex, c.tierIndex, c.sectionIndex) = (comp.ToFrozenDictionary(StringComparer.Ordinal),
                cell.ToFrozenDictionary(StringComparer.Ordinal), room.ToFrozenDictionary(StringComparer.Ordinal), bulkhead.ToFrozenDictionary(StringComparer.Ordinal),
                deck.ToFrozenDictionary(StringComparer.Ordinal), tier.ToFrozenDictionary(StringComparer.Ordinal), section.ToFrozenDictionary(StringComparer.Ordinal));
            c.DoorsBelow = hb.DoorsBelow;
            c.Solids = new ShipSolids(c);
            FunctionTable.Build(c);
            Trains.Build(c);
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
        int Comp(string? id, ComponentKind kind)
        {
            int i = Comp(id);
            if (i >= 0 && hb.Components[i].Kind != kind)
                throw new ShipCompileException($"{where}: \"{id}\" is a {hb.Components[i].Kind.Name()}, not a {kind.Name()}");
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

        /// <summary>A material's row from the report's table (every name the parts give, resolved by shipgen).</summary>
        MaterialRow MaterialOf(string name) =>
            ship.Report.Materials?.GetValueOrDefault(name) is { Q: > 0 } m ? MaterialRow.Of(m)
                : MaterialTemplates.Resolve(ship.Design, name) is { Q: > 0 } k ? MaterialRow.Of(k)
                : throw new ShipCompileException($"material {name}: not in report.materials nor the catalogue, or no q");

        /// <summary>What stands in each room in a shell's path (docs/battle/shell-contents.md C1b): its kind's rows for
        /// this plant, between the small and the large by her displacement; the band as heights over the room's cells.
        /// The objects' materials are the catalogue's names (the design may give its own of the same name).</summary>
        readonly List<ContentObject> objects = [];

        void RoomContents(ShipClass c)
        {
            double size = Contents.SizeOf(c.Hydrostatics.DisplacementT);
            c.ForgedMaterial = Material("high-tensile steel");
            c.roomContents = new RoomContents[c.rooms.Length];
            for (int i = 0; i < c.rooms.Length; i++)
            {
                // the largest calibre a room serves (a magazine's shells), its mounts by their components
                double cal = 0;
                var ms = c.rooms[i].Mounts;
                foreach (var mount in c.mounts)
                {
                    bool serves = mount.Component == c.rooms[i].Mount;
                    for (int j = ms.Start; j < ms.End && !serves; j++)
                        serves = links[j] == mount.Component;
                    if (serves)
                        cal = Math.Max(cal, mount.CalibreMm);
                }
                if (Contents.Of(c.rooms[i].Kind, c.Boilers, c.Engines, cal) is not var (small, large))
                    continue;
                var row = Contents.Between(small, large, size);
                double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                var cs = c.rooms[i].Cells;
                for (int j = cs.Start; j < cs.End; j++)
                    (lo, hi) = (Math.Min(lo, c.cells[links[j]].Box.Base), Math.Max(hi, c.cells[links[j]].Box.Top));
                if (!(hi > lo) || !(row.Packing > 0))
                    continue;
                c.roomContents[i] = new RoomContents(lo + row.BandLo * (hi - lo), lo + row.BandHi * (hi - lo), row.ChordM / row.Packing, row.ChordM,
                    Objects(row), row.Name);
            }
            c.contentObjects = [.. objects];
        }

        SolidAt Solid(SolidRow s) => s.Some ? new SolidAt(s.Name, s.LoMm, s.HiMm, Material(s.Material), s.Shape, s.Bore) : default;

        /// <summary>A row's objects into the pool, cumulated for the draw: their slice.</summary>
        Slice Objects(ContentsRow row)
        {
            int start = objects.Count;
            double total = row.Mix.Sum(o => o.Share), upTo = 0;
            foreach (var o in row.Mix)
            {
                upTo += o.Share / total;
                objects.Add(new ContentObject(upTo, o.Skin.Name, Solid(o.Skin), o.InteriorRho, o.InteriorShare, o.Interior, o.CoreChance, Solid(o.Core)));
            }
            return new Slice(start, row.Mix.Length);
        }

        /// <summary>What stands in the parts (docs/battle/shell-contents.md C4b): an enclosed turret's gun house its guns and
        /// gear by its calibre, a barbette its trunk and turntable; bands as heights over the part's bounds.</summary>
        void PartContents(ShipClass c, List<ContentObject> pool)
        {
            var rows = new List<RoomContents>();
            c.partContents = new Slice[c.components.Length];
            for (int i = 0; i < c.components.Length; i++)
            {
                var b = c.components[i].Bounds;
                int start = rows.Count;
                void Add(ContentsRow row) => rows.Add(new RoomContents(b.Base + row.BandLo * (b.Top - b.Base), b.Base + row.BandHi * (b.Top - b.Base),
                    row.ChordM / row.Packing, row.ChordM, Objects(row), row.Name));
                if (c.components[i].Kind == ComponentKind.Barbette)
                {
                    Add(Contents.BarbetteTrunk);
                    Add(Contents.BarbetteTop);
                }
                else
                    foreach (var m in c.mounts)
                        if (m.Component == i && m.Turret && c.turretTypes[m.Type].Shape == TurretShape.Bb)
                        {
                            Add(Contents.GunHouse(m.CalibreMm));
                            break;
                        }
                if (!(b.Top > b.Base))
                    rows.RemoveRange(start, rows.Count - start);
                c.partContents[i] = new Slice(start, rows.Count - start);
            }
            c.partRows = [.. rows];
            c.contentObjects = [.. pool];
        }

        /// <summary>What each room stows to a level (docs/battle/shell-contents.md C2-C3), as Shipgen stowed the fuel
        /// (Powerplant.Bunkers): a coal plant's in its wing bunkers (their tonnes) and then its end bunkers (0.9 of them);
        /// an oil plant's in the double bottom (0.6 of it), then the TDS layers (0.3 of them: half the protection's width at
        /// 0.6), then end bunkers (0.9), each only as far as the fuel goes, in that order. A coal plant's double bottom
        /// holds the crew's feed and fresh water (Crew.WaterInDoubleBottomM3). Fuel tanks hold their tonnes. A space off
        /// the centreline (a wing bunker, a TDS layer) is protective.</summary>
        void RoomStowages(ShipClass c)
        {
            var r = ship.Report;
            string plantFuel = r.Plant.Fuel;
            bool oil = plantFuel is "oil" or "diesel";
            double stow = Powerplant.Fuels.TryGetValue(plantFuel, out var f) ? f.Stowage : 1.07;
            double fuelLeft = (r.Results?.FuelT ?? 0) * stow;   // m³ of fuel at full load, placed in Shipgen's order
            double fuelRho = plantFuel switch { "coal" => Stowage.CoalRho, "diesel" => Stowage.DieselRho, "petrol" => Stowage.PetrolRho, _ => Stowage.OilRho };
            var kind = plantFuel == "coal" ? MediumKind.Granular : MediumKind.Liquid;
            c.stowages = new RoomStowage[c.rooms.Length];
            var spans = new (double Lo, double Hi, bool Side)[c.rooms.Length];
            double dbVolume = 0;
            for (int i = 0; i < c.rooms.Length; i++)
            {
                double lo = double.PositiveInfinity, hi = double.NegativeInfinity, y0 = double.PositiveInfinity, y1 = double.NegativeInfinity;
                var cs = c.rooms[i].Cells;
                for (int j = cs.Start; j < cs.End; j++)
                {
                    var b = c.cells[links[j]].Box;
                    (lo, hi, y0, y1) = (Math.Min(lo, b.Base), Math.Max(hi, b.Top), Math.Min(y0, b.Y0), Math.Max(y1, b.Y1));
                }
                spans[i] = (lo, hi, y0 > 0.05 || y1 < -0.05);
                if (c.rooms[i].Kind == RoomKind.DoubleBottom)
                    dbVolume += c.rooms[i].VolumeM3;
            }
            // Shipgen's order: wing bunkers (given), the double bottom and the TDS layers for oil, then the end bunkers
            void Place(Func<int, bool> which, double share, MediumKind k, double rho, bool fuel)
            {
                // a group the fuel doesn't fill is filled evenly, to the share the fuel left makes
                bool Takes(int i) => which(i) && spans[i].Hi > spans[i].Lo && c.rooms[i].VolumeM3 > 0;
                double room3 = 0;
                for (int i = 0; i < c.rooms.Length; i++)
                    if (Takes(i))
                        room3 += share * c.rooms[i].VolumeM3;
                double scale = fuel ? (room3 > 0 ? Math.Clamp(fuelLeft / room3, 0, 1) : 0) : 1;
                if (fuel)
                    fuelLeft -= scale * room3;
                for (int i = 0; i < c.rooms.Length; i++)
                {
                    var room = c.rooms[i];
                    if (!Takes(i))
                        continue;
                    double want = scale * share * room.VolumeM3;
                    if (want > 0)
                        c.stowages[i] = new RoomStowage(k, rho, want / room.VolumeM3, fuel, spans[i].Side, spans[i].Lo, spans[i].Hi, room.VolumeM3,
                            k == MediumKind.Granular ? "coal" : fuel ? plantFuel : "water");
                }
            }
            bool Is(int i, RoomKind k) => c.rooms[i].Kind == k;
            Place(i => Is(i, RoomKind.Bunker) && c.rooms[i].Tonnes > 0, 0.9, kind, fuelRho, true);   // wing bunkers: their tonnes fit 0.9
            if (oil)
            {
                Place(i => Is(i, RoomKind.DoubleBottom), 0.6, MediumKind.Liquid, fuelRho, true);
                Place(i => Is(i, RoomKind.Tds), 0.3, MediumKind.Liquid, fuelRho, true);
            }
            Place(i => Is(i, RoomKind.Bunker) && !(c.rooms[i].Tonnes > 0), 0.9, kind, fuelRho, true);
            if (!oil && r.Crew?.WaterInDoubleBottomM3 is > 0 and var water && dbVolume > 0)
                Place(i => Is(i, RoomKind.DoubleBottom), Math.Min(water / dbVolume, 0.95), MediumKind.Liquid, Stowage.WaterRho, false);
            for (int i = 0; i < c.rooms.Length; i++)
                if (Is(i, RoomKind.FuelTank) && c.rooms[i].VolumeM3 > 0 && spans[i].Hi > spans[i].Lo)
                {
                    bool petrol = c.rooms[i].Fuel == Fuel.Petrol;
                    double rho = petrol ? Stowage.PetrolRho : Stowage.OilRho;
                    double share = c.rooms[i].Tonnes > 0 ? Math.Min(c.rooms[i].Tonnes * 1000 / rho / c.rooms[i].VolumeM3, 0.95) : 0.9;
                    c.stowages[i] = new RoomStowage(MediumKind.Liquid, rho, share, true, spans[i].Side, spans[i].Lo, spans[i].Hi, c.rooms[i].VolumeM3,
                        petrol ? "petrol" : "oil");
                }
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
                var kind = x.Kind;
                var shape = Kinds.Shape(x.Shape);
                bool mount = kind is ComponentKind.Main or ComponentKind.Secondary or ComponentKind.Torpedo;
                if (mount != (x.Local != null))
                    throw new ShipCompileException(mount ? $"{where}: a mount with no turret shape" : $"{where}: a turret shape on a {x.Kind.Name()}");
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
                        RoofMm = x.Armour?.Roof ?? 0, Magazine = Room(x.Magazine), Barbette = Comp(x.Barbette, ComponentKind.Barbette), Rounds = Int(x.Rounds),
                        ReadyRounds = Int(x.ReadyRounds), ReadyT = Or(x.ReadyT), Torpedoes = Int(x.Torpedoes), WarheadKg = Or(x.WarheadKg),
                        RangefinderM = x.RangefinderM is > 0 ? x.RangefinderM.Value : 0, RangefinderEye = -1,
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
                            int prop = Comp(x.Propeller, ComponentKind.Propeller);
                            shafts.Add(new ShaftLineRow
                            {
                                Shaft = i, Propeller = prop, Alley = Comp(x.Alley, ComponentKind.ShaftAlley), EngineRoom = Room(x.EngineRoom),
                                Position = Kinds.PositionOf(x.Position), LeavesHullX = Or(x.LeavesHullX), P0 = Point3(x.P0), P1 = Point3(x.P1), R = Or(x.R),
                                PropellerDiameterM = prop >= 0 ? Or(hb.Components[prop].DiameterM) : double.NaN,
                            });
                            break;
                        case ComponentKind.ShaftAlley or ComponentKind.Propeller:
                            Comp(x.Shaft, ComponentKind.Shaft);
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
                    links.Add(Comp(f, ComponentKind.Funnel));
                rows[i] = new ComponentRow
                {
                    Kind = kind, Role = role, Shape = shape, Level = Int(x.Level), Bounds = BoundsOf(x, outline, mount), X = x.X ?? double.NaN,
                    Y = x.Y ?? double.NaN, Z = x.Z ?? double.NaN, R = Or(x.R), Outline = outline, ArmourMm = x.ArmourMm ?? 0, Material = Material(x.Material),
                    PlateMm = x.PlateMm ?? 0, WoodMm = x.WoodMm ?? 0, Crew = Int(x.Crew), BattleCrew = Int(x.BattleCrew),
                    Smoke = new Slice(smoke, links.Count - smoke), Detail = detail, RangefinderM = x.RangefinderM is > 0 ? x.RangefinderM.Value : 0,
                    Mast = kind == ComponentKind.Mast ? Kinds.Mast(x.Type) : MastKind.Pole,
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
                        int f = Comp(x.Funnel, ComponentKind.Funnel);
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
                        int s = Comp(x.Shaft, ComponentKind.Shaft);
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

        /// <summary>The design's drainage scaled to her displacement (DC4).</summary>
        static DrainageRow DrainageOf(DrainageInput d, double displacementT) => new(
            (d.PumpsTphPer1000T ?? 0) * displacementT / 1000, d.PortablePumps ?? 0, d.PortableTph ?? 0,
            (d.CounterfloodTpmPer10000T ?? 0) * displacementT / 10_000, Math.Max(d.Tightness ?? 1, 1));

        /// <summary>The places a fire burns (docs/battle/fire.md B0), with the design's outfit's loads (Outfits): every cell
        /// at its own index, by its room's use; the parts with a space inside (superstructure blocks as cabins and offices,
        /// the conning tower and tops as offices, gunhouses and casemates as machinery until their charges are a seat of
        /// their own); a strip of weather deck a section, over its top cells. Paint goes on every space but the deck; a
        /// wooden deck burns at the outfit's deck load, a steel one at half [EST].</summary>
        void FireSpaces(ShipClass c)
        {
            var o = Outfits.Of(ship.Design);
            double paint = o.PaintMjM2 ?? 0, growth = o.Growth ?? 1;
            bool wood = hb.Plating is { } plating && plating.DeckWoodMm > 0;
            double Load(FireLoadsInput l, FireUse use) => use switch
            {
                FireUse.Quarters => (l.Quarters ?? 0) + paint,
                FireUse.Cabins => ((l.Cabins ?? 0) + (l.Offices ?? 0)) / 2 + paint,
                FireUse.Offices => (l.Offices ?? 0) + paint,
                FireUse.Stores => (l.Stores ?? 0) + paint,
                FireUse.Machinery => (l.Machinery ?? 0) + paint,
                FireUse.Deck => (l.Deck ?? 0) * (wood ? 1 : 0.5),
                _ => 0,
            };
            FireSpaceRow Row(FireSpaceKind kind, int index, int sec, FireUse use, double area, double height, int cell) => new()
            {
                Kind = kind, Index = index, Section = sec, Use = use, AreaM2 = area, HeightM = height, LoadMJm2 = Load(o.Fitted!, use),
                StrippedMJm2 = Load(o.Stripped!, use), Growth = growth, PaintMJm2 = use is FireUse.None or FireUse.Deck ? 0 : paint, Cell = cell,
            };

            var rows = new List<FireSpaceRow>();
            for (int i = 0; i < c.cells.Length; i++)
            {
                var x = c.cells[i];
                var use = c.rooms[x.Room].Kind switch
                {
                    RoomKind.Accommodation => FireUse.Quarters,
                    RoomKind.Stores or RoomKind.Hold => FireUse.Stores,
                    RoomKind.BoilerRoom or RoomKind.EngineRoom or RoomKind.Steering or RoomKind.TorpedoRoom => FireUse.Machinery,
                    RoomKind.TransmittingStation => FireUse.Offices,
                    _ => FireUse.None,
                };
                var b = x.Box;
                rows.Add(Row(FireSpaceKind.Cell, i, x.Section, use, (b.X1 - b.X0) * (b.Y1 - b.Y0), b.Top - b.Base, i));
            }
            c.fireOfPart = new int[c.components.Length];
            for (int i = 0; i < c.components.Length; i++)
            {
                var x = c.components[i];
                var use = x.Kind switch
                {
                    ComponentKind.Superstructure => FireUse.Cabins,
                    ComponentKind.ConningTower or ComponentKind.Top => FireUse.Offices,
                    ComponentKind.Main or ComponentKind.Secondary or ComponentKind.Torpedo or ComponentKind.Aa or ComponentKind.Sponson
                        or ComponentKind.TorpedoTube => FireUse.Machinery,
                    ComponentKind.HangarBay => FireUse.Stores,
                    ComponentKind.FlightDeck => FireUse.Deck,
                    _ => FireUse.None,
                };
                c.fireOfPart[i] = -1;
                var b = x.Bounds;
                double area = (b.X1 - b.X0) * (b.Y1 - b.Y0);
                if (use == FireUse.None || !(area > 0))
                    continue;
                c.fireOfPart[i] = rows.Count;
                int sec = c.SectionAt((b.X0 + b.X1) / 2);
                rows.Add(Row(FireSpaceKind.Part, i, sec, use, area, Math.Clamp(b.Top - b.Base, 2, 3.5), TopCell(sec)));
            }
            c.fireOfDeck = new int[c.sections.Length];
            for (int s = 0; s < c.sections.Length; s++)
            {
                double top = double.MinValue, area = 0;
                foreach (var x in c.cells)
                    if (x.Section == s)
                        top = Math.Max(top, x.Box.Top);
                foreach (var x in c.cells)
                    if (x.Section == s && x.Box.Top >= top - 0.05)
                        area += (x.Box.X1 - x.Box.X0) * (x.Box.Y1 - x.Box.Y0);
                c.fireOfDeck[s] = rows.Count;
                rows.Add(Row(FireSpaceKind.Deck, s, s, FireUse.Deck, area, 0, TopCell(s)));
            }
            FireLinks(c, rows);
            c.fireSpaces = rows.ToArray();
            double mx = 0, mv = 0;
            foreach (var r in c.rooms)
                if (r.Kind is RoomKind.BoilerRoom or RoomKind.EngineRoom)
                    (mx, mv) = (mx + (r.X0 + r.X1) / 2 * r.VolumeM3, mv + r.VolumeM3);
            c.PumpSection = mv > 0 ? c.SectionAt(mx / mv) : c.sections.Length / 2;

            // the section's top cell nearest the centreline: where a fire on its deck or in its superstructure is shown
            int TopCell(int s)
            {
                int best = -1;
                for (int i = 0; i < c.cells.Length; i++)
                {
                    var b = c.cells[i].Box;
                    if (c.cells[i].Section != s)
                        continue;
                    if (best < 0)
                    {
                        best = i;
                        continue;
                    }
                    var bb = c.cells[best].Box;
                    if (b.Top > bb.Top + 0.05 || b.Top > bb.Top - 0.05 && Math.Abs(b.Y0 + b.Y1) < Math.Abs(bb.Y0 + bb.Y1))
                        best = i;
                }
                return Math.Max(best, 0);
            }
        }

        /// <summary>The fire spaces' graph (docs/battle/fire.md B1), each edge from both ends: a cell to its neighbours (the
        /// same room open, a deck up or down, a bulkhead sideways, with the pool entry a burst's openings between them are
        /// kept on); a cell with nothing over it up to its section's weather deck; a section's deck along to the next ones;
        /// a part with a space inside out onto its section's deck, and open to the parts it stands on or against (the
        /// superstructure's own ladders and doors). Parts are paired within a section and its neighbours only.</summary>
        void FireLinks(ShipClass c, List<FireSpaceRow> rows)
        {
            var links = new List<FireLink>[rows.Count];
            for (int i = 0; i < links.Length; i++)
                links[i] = [];
            void Both(int a, int b, FireWay ab, FireWay ba, int pool = -1)
            {
                links[a].Add(new FireLink(b, ab, pool));
                links[b].Add(new FireLink(a, ba, pool));
            }
            var cells = c.cells;
            for (int i = 0; i < cells.Length; i++)
            {
                var b = cells[i].Box;
                bool covered = false;
                var nb = cells[i].Neighbours;
                for (int j = nb.Start; j < nb.End; j++)
                {
                    var n = c.neighbours[j];
                    var o = cells[n.Cell].Box;
                    if (o.Base >= b.Top - 0.05)
                        covered = true;
                    if (n.Cell <= i)
                        continue;   // each edge once, from its lower cell: its pool entry is the one the openings are kept on
                    var (ab, ba) = n.Join switch
                    {
                        JoinKind.Open => (FireWay.Open, FireWay.Open),
                        JoinKind.Deck when o.Base >= b.Top - 0.05 => (FireWay.Up, FireWay.Down),
                        JoinKind.Deck => (FireWay.Down, FireWay.Up),
                        _ => (FireWay.Side, FireWay.Side),
                    };
                    Both(i, n.Cell, ab, ba, j);
                }
                if (!covered)
                    Both(i, c.fireOfDeck[cells[i].Section], FireWay.Up, FireWay.Down);
            }
            for (int s = 0; s + 1 < c.sections.Length; s++)
                Both(c.fireOfDeck[s], c.fireOfDeck[s + 1], FireWay.Deck, FireWay.Deck);
            var parts = new List<int>[c.sections.Length];
            for (int s = 0; s < parts.Length; s++)
                parts[s] = [];
            for (int p = 0; p < c.components.Length; p++)
                if (c.fireOfPart[p] >= 0)
                {
                    int f = c.fireOfPart[p];
                    parts[rows[f].Section].Add(p);
                    Both(f, c.fireOfDeck[rows[f].Section], FireWay.Out, FireWay.Out);
                }
            static bool Over(double a0, double a1, double b0, double b1) => Math.Min(a1, b1) - Math.Max(a0, b0) > 0.05;
            static bool Meet(double a0, double a1, double b0, double b1) => Math.Abs(a1 - b0) < 0.15 || Math.Abs(b1 - a0) < 0.15;
            for (int s = 0; s < parts.Length; s++)
                foreach (int p in parts[s])
                    for (int t = s; t <= Math.Min(s + 1, parts.Length - 1); t++)
                        foreach (int q in parts[t])
                        {
                            if (t == s && q <= p)
                                continue;
                            Box a = c.components[p].Bounds, d = c.components[q].Bounds;
                            bool x = Over(a.X0, a.X1, d.X0, d.X1), y = Over(a.Y0, a.Y1, d.Y0, d.Y1), z = Over(a.Base, a.Top, d.Base, d.Top);
                            bool stacked = x && y && Meet(a.Base, a.Top, d.Base, d.Top);
                            bool beside = z && (x && Meet(a.Y0, a.Y1, d.Y0, d.Y1) || y && Meet(a.X0, a.X1, d.X0, d.X1));
                            if (stacked || beside || x && y && z)
                                Both(c.fireOfPart[p], c.fireOfPart[q], FireWay.Open, FireWay.Open);
                        }
            var pool = new List<FireLink>();
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i] = rows[i] with { Links = new Slice(pool.Count, links[i].Count) };
                pool.AddRange(links[i]);
            }
            c.fireLinks = pool.ToArray();
        }

        RoomRow RoomOf(Shipgen.Room x)
        {
            where = $"room \"{x.Id}\"";
            return new RoomRow
            {
                Kind = x.Kind, Fuel = Kinds.FuelOf(x.Fuel), Tonnes = Or(x.Tonnes), Mount = Comp(x.Mount), Shared = x.Shared ?? false,
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
                ArmourMaterial = Material(x.ArmourMaterial), PlateLowMm = Or(x.PlateLowMm), PlateLowTop = Or(x.PlateLowTop),
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
            if (a.Belt is { Position: not "internal" } b)   // (an internal one is inclined plates)
            {
                sides.Add(new SidePlateRow
                {
                    Kind = StrakeKind.Belt, Extent = Extent.Citadel, ThicknessMm = b.ThicknessMm, X0 = b.X0, X1 = b.X1, Bottom = b.Bottom, Top = b.Top,
                    Material = Material(b.Material), BottomMm = Or(b.BottomMm), TaperFrom = Or(b.TaperFrom), TipMm = double.NaN,
                    InclineDeg = b.InclineDeg ?? 0, BackingMm = b.Backing?.Mm ?? 0, BackingMaterial = Material(b.Backing?.Material),
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
                        "zone" => StrakeKind.Zone,
                        _ => throw new ShipCompileException($"unknown strake kind \"{s.Kind}\""),
                    },
                    Extent = s.Extent, ThicknessMm = s.ThicknessMm, X0 = s.X0, X1 = s.X1, Bottom = s.Bottom, Top = s.Top,
                    Material = Material(s.Material), BottomMm = Or(s.BottomMm), TaperFrom = Or(s.TaperFrom), TipMm = Or(s.TipMm),
                    InclineDeg = s.InclineDeg ?? 0, BackingMm = s.Backing?.Mm ?? 0, BackingMaterial = Material(s.Backing?.Material),
                });
                ids.Add(s.Id);
            }
            Index(ids, "side plate");
            c.sidePlates = sides.ToArray();
            c.GratingShare = a.Gratings ?? Shipgen.Armour.GratingShareDefault;
            c.sidePlateIds = ids.ToArray();
            var bh = a.Bulkheads ?? [];
            c.armourBulkheads = bh.Select(x => new ArmourBulkheadRow(x.X, x.ThicknessMm, x.Bottom, x.Top, Material(x.Material), Or(x.LowerMm), Or(x.LowerTop),
                Or(x.W))).ToArray();
            c.armourBulkheadIds = [.. bh.Select(x => x.Id)];
            Index(c.armourBulkheadIds, "armour bulkhead");
            c.armourDecks = (a.Decks ?? []).Select(x =>
                new ArmourDeckRow(Deck(x.Deck), x.ThicknessMm, x.Extent, x.X0, x.X1, x.Z, x.Main, x.Roof, Material(x.Material), Or(x.W))
                    { SlopeReach = Or(x.SlopeReach), SlopeFoot = Or(x.SlopeFoot) }).ToArray();
            var inc = a.Inclined ?? [];
            c.inclined = [.. inc.Select(x => new InclinedRow
            {
                Kind = x.Kind switch
                {
                    "slope" => InclinedKind.Slope,
                    "belt" => InclinedKind.Belt,
                    "zone" => InclinedKind.Zone,
                    _ => throw new ShipCompileException($"unknown inclined plate kind \"{x.Kind}\""),
                },
                ThicknessMm = x.ThicknessMm, X0 = x.X0, X1 = x.X1, Z0 = x.Z0, Z1 = x.Z1, U0 = x.U0, U1 = x.U1, SideZ = x.SideZ,
                Material = Material(x.Material), BottomMm = Or(x.BottomMm), TaperFrom = Or(x.TaperFrom),
            })];
            c.inclinedIds = [.. inc.Select(x => x.Id)];
            Index(c.inclinedIds, "inclined plate");
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
        /// doesn't give comes from DesignerPlaceholders, by the design's gun and torpedo blocks (never the look: it's the
        /// drawing's only).</summary>
        void Weapons(ShipClass c)
        {
            var design = ship.Design;
            var torpedo = WeaponBlocks.Torpedo(design);
            var blocks = new Dictionary<string, GunTechRow>();
            // a mount's gun block: its design battery's (of its kind and calibre, and length where the battery gives it)
            GunTechRow GunOf(ComponentKind kind, double calibreMm, double calibreLength)
            {
                var list = kind == ComponentKind.Main ? design.Main : design.Secondary;
                string? name = list?.FirstOrDefault(x => x.CalibreMm == calibreMm && (x.CalibreLength is null || x.CalibreLength == calibreLength))?.Gun;
                string key = name ?? GunTemplates.Default;
                if (!blocks.TryGetValue(key, out var g))
                    blocks[key] = g = WeaponBlocks.Gun(design, name);
                return g;
            }
            var mounts = c.mounts;
            var gunOfType = new GunTechRow?[c.turretTypes.Length];
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
                        Battery = -1, ReloadS = DesignerPlaceholders.TorpedoReloadS(LauncherKind.Mount, torpedo), TrainDegS = DesignerPlaceholders.TorpedoTrainDegS(torpedo),
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
                var gun = GunOf(m.Kind, m.CalibreMm, m.CalibreLength);
                gunOfType[m.Type] ??= gun;
                var (reload, train, elevate, maxElev) = DesignerPlaceholders.Gun(m.CalibreMm, m.CalibreLength, c.turretTypes[m.Type].Shape, gun);
                mounts[i] = m with
                {
                    Battery = b, ReloadS = reload, TrainDegS = train, ElevateDegS = elevate, MaxElevationDeg = maxElev, SupplyS = reload * DesignerPlaceholders.HoistShare,
                };
            }
            // each turret type's charge, by the block of the first mount of it (a type no mount uses: the default's)
            c.charges = c.turretTypes.Select((t, k) => t.Shape is TurretShape.Torp or TurretShape.Tube || !(t.CalibreLength > 0) ? default
                : DesignerPlaceholders.Charge(t.CalibreMm, t.CalibreLength, gunOfType[k] ?? WeaponBlocks.Gun(design, null))).ToArray();

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
                bool Serves(int i) => row.Side is Side.None or Side.Centre || SideOf(Mid(c.components[c.directors[i].Component].Bounds)) is var s && (s == row.Side || s == Side.Centre);
                static double Mid(Box b) => (b.Y0 + b.Y1) / 2;
                var mine = Enumerable.Range(0, c.directors.Length).Where(i => c.directors[i].Battery == row.Kind).ToList();
                if (mine.Any(Serves))
                    mine.RemoveAll(i => !Serves(i));
                foreach (int d in mine.OrderByDescending(i => c.directors[i].RangefinderM > 0 ? c.directors[i].RangefinderM : 0).ThenBy(i => i))
                    links.Add(d);
                int dirs = links.Count - d0;
                // its fit, the tech blocks the design names (fire-control.md F3); a battery without directors fires under local control
                var spec = ship.Design.FireControl?.Of(row.Kind == Battery.Main ? "main" : "secondary");
                var own = FireControlBlocks.Fit(ship.Design, FireControlTemplates.FitOf(spec));
                // its guns' own sights, the design's (fire_control.BATTERY.sights): what they lay by when no director fires them
                string sightsName = FireControlTemplates.SightsOf(spec);
                var sights = FireControlBlocks.Mount(FireControlBlocks.Block(ship.Design, sightsName)?.Mount
                    ?? throw new ShipCompileException($"fire control: no sights block \"{sightsName}\""));
                var fit = dirs == 0 ? FireControlPresets.Local with { Mount = sights } : own;
                batteries[b] = row with
                {
                    Mounts = new Slice(m0, d0 - m0), Guns = guns, Directors = new Slice(d0, dirs), Level = dirs == 0 ? FireControlLevel.Eyeball : FireControlPresets.LevelOf(fit),
                    Fit = fit, TurretOptics = own.Optics, Sights = sights,
                    PatternPct = DesignerPlaceholders.Pattern(row.CalibreMm, GunOf(row.Kind == Battery.Main ? ComponentKind.Main : ComponentKind.Secondary, row.CalibreMm, row.CalibreLength)),
                };
            }
            // the guns she fights with: her main battery, or with none her heaviest
            bool anyMain = batteries.Any(r => r.Kind == Battery.Main);
            double heaviest = batteries.Count > 0 ? batteries.Max(r => r.CalibreMm) : 0;
            for (int b = 0; b < batteries.Count; b++)
                batteries[b] = batteries[b] with { Chief = anyMain ? batteries[b].Kind == Battery.Main : batteries[b].CalibreMm == heaviest };
            c.batteries = batteries.ToArray();
            // the gun's shells, as shipgen resolved them (the design's, or the default loadout): the class's own loadout
            c.Loadout = Loadout.Compile(c, ship.Report.Shells, "", c.DesignHash);
            c.design = ship.Design;
            Launchers(c, torpedo);
        }

        /// <summary>The gunhouses' rangefinders as eyes (docs/battle/fire-control.md F4b): each a hood 0.3 m over its turret's
        /// roof, seeing only through its turret's arcs, its make its battery's; the eyes sorted by height again, and each
        /// mount told its eye.</summary>
        void TurretRangefinders(ShipClass c)
        {
            var add = new List<EyeRow>();
            for (int m = 0; m < c.mounts.Length; m++)
            {
                ref readonly var mt = ref c.mounts[m];
                if (!(mt.RangefinderM > 0) || mt.Battery < 0)
                    continue;
                var arcs = numbers.GetRange(mt.Arcs.Start, mt.Arcs.Count);
                ulong blind = 0;
                for (int s = 0; s < Sight.Sectors; s++)
                {
                    double a = (s + 0.5) * Sight.SectorDeg;
                    bool inside = arcs.Count == 0;
                    for (int i = 0; i + 1 < arcs.Count && !inside; i += 2)
                    {
                        double span = arcs[i + 1] - arcs[i];
                        inside = span >= 360 || ((a - arcs[i]) % 360 + 360) % 360 <= span;
                    }
                    if (!inside)
                        blind |= 1UL << s;
                }
                var b = c.components[mt.Component].Bounds;
                add.Add(new EyeRow(EyeKind.Turret, mt.Component, mt.X, mt.Y, b.Top + 0.3 - c.Vertical.Waterline, mt.RangefinderM, blind,
                    c.batteries[mt.Battery].TurretOptics, InstrumentSupport.Turret));
            }
            if (add.Count == 0)
                return;
            c.eyes = [.. c.eyes.Concat(add).OrderByDescending(e => e.HeightM).ThenBy(e => e.Component)];
            for (int e = 0; e < c.eyes.Length; e++)
                if (c.eyes[e].Kind == EyeKind.Turret)
                    for (int m = 0; m < c.mounts.Length; m++)
                        if (c.mounts[m].Component == c.eyes[e].Component)
                            c.mounts[m] = c.mounts[m] with { RangefinderEye = e };
        }

        /// <summary>The fire-control units (docs/battle/fire-control.md F0): one a director, its instruments the eyes
        /// that stand in it (highest first, as the eyes are), and each battery's units in its directors' order.</summary>
        void FireControlUnits(ShipClass c)
        {
            int positions = 0;
            foreach (var eye in c.eyes)
                positions += eye.Kind == EyeKind.Rangefinder ? 1 : 0;
            if (c.directors.Length + positions > 64)
                throw new ShipCompileException($"{c.directors.Length + positions} control positions: fire control holds its units as 64 bits");
            var units = new FcUnitRow[c.directors.Length + positions];
            var instruments = new List<InstrumentRow>();
            for (int d = 0; d < c.directors.Length; d++)
            {
                int i0 = links.Count;
                for (int e = 0; e < c.eyes.Length; e++)
                {
                    ref readonly var eye = ref c.eyes[e];
                    if (eye.Kind != EyeKind.Director || eye.Component != c.directors[d].Component)
                        continue;
                    links.Add(instruments.Count);
                    instruments.Add(new InstrumentRow(d, e, eye.Component, eye.X, eye.Y, eye.HeightM, eye.Ranges ? eye.Optics.Kind : InstrumentKind.Eye,
                        eye.RangefinderM));
                }
                units[d] = new FcUnitRow(d, c.directors[d].Battery, new Slice(i0, links.Count - i0));
            }
            // the control positions' rangefinders, after the directors: the main battery's
            int u = c.directors.Length;
            for (int e = 0; e < c.eyes.Length; e++)
            {
                ref readonly var eye = ref c.eyes[e];
                if (eye.Kind != EyeKind.Rangefinder)
                    continue;
                int i0 = links.Count;
                links.Add(instruments.Count);
                instruments.Add(new InstrumentRow(u, e, eye.Component, eye.X, eye.Y, eye.HeightM, eye.Ranges ? eye.Optics.Kind : InstrumentKind.Eye,
                    eye.RangefinderM));
                units[u++] = new FcUnitRow(-1, Battery.Main, new Slice(i0, 1));
            }
            for (int b = 0; b < c.batteries.Length; b++)
            {
                int u0 = links.Count;
                foreach (int d in links.GetRange(c.batteries[b].Directors.Start, c.batteries[b].Directors.Count))
                    links.Add(d);   // a unit's index is its director's
                if (c.batteries[b].Kind == Battery.Main)
                    for (int p = c.directors.Length; p < units.Length; p++)
                        links.Add(p);
                c.batteries[b] = c.batteries[b] with { Units = new Slice(u0, links.Count - u0) };
            }
            (c.fcUnits, c.instruments) = (units, instruments.ToArray());
        }

        /// <summary>The torpedo mounts and the submerged tubes as one table, and the torpedoes they fire (docs/battle/
        /// fighting.md 4.6): a mount's torpedoes go in at its pivot on the waterline, a tube's at its muzzle.</summary>
        void Launchers(ShipClass c, TorpedoTechRow tech)
        {
            var launchers = new List<LauncherRow>();
            var torpedoes = new List<TorpedoRow>();
            int TorpedoOf(double calibreMm, double warheadKg)
            {
                var t = DesignerPlaceholders.Torpedo(calibreMm, warheadKg, tech);
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
                    RestDeg = t.RestDeg, Tubes = 1, Torpedoes = t.Torpedoes, ReloadS = DesignerPlaceholders.TorpedoReloadS(LauncherKind.Tube, tech),
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
            CalibreLength = Or(t.CalibreLength), TipM = Geometry.BarrelTip(t),
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
                if (x.Kind == ComponentKind.Rudder)
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
            for (int b = 0; b < c.batteries.Length; b++)
            {
                ref readonly var row = ref c.batteries[b];
                h.Add((int)row.Kind);
                h.Add((int)row.Side);
                h.Add(c.Links(row.Mounts));
                h.Add(c.Links(row.Directors));
                // the design's own loadout, as the batteries carried it before loadouts (so the hash didn't move)
                h.Add(c.Loadout.Shells(b).Count);
                foreach (var l in c.Loadout.Loads(b))
                    h.Add(l.ShellType);
                h.Add(c.Loadout.Fires(b));
            }
            h.Add(c.Loadout.ShellTypes.Length);
            for (int i = 0; i < c.Loadout.ShellTypes.Length; i++)
                h.Add(c.Loadout.ShellTypeId(i));
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
            if (c.inclinedIds.Length > 0)   // (so a class without them keeps the hash it had before them)
            {
                h.Add(c.inclinedIds.Length);
                foreach (var id in c.inclinedIds)
                    h.Add(id);
            }
            h.Add(c.materials.Length);
            foreach (var m in c.materials)
                h.Add(m);
            return h.Finish();
        }
    }
}
