namespace Fleetwright.Shipgen;

/// <summary>hitbox: hitbox export. Components with exact shapes in ship-local metres and heights above the main deck.</summary>
public static class Hitbox
{
    const double TurretSide = 0.55, TurretRear = 0.5, TurretRoof = 0.4, Barbette = 0.8;

    static List<Pt> R3(IEnumerable<Pt> pts) => pts.Select(p => new Pt(Math.Round(p.X, 3), Math.Round(p.Y, 3))).ToList();

    static string? NonEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    /// <summary>The propulsion train as components.</summary>
    static IEnumerable<Component> PropulsionComponents(Train tr, double D)
    {
        double Z(double v) => Math.Round(v - D, 2);
        List<Pt> Rect(double x0, double x1, double y, double hw) => R3([new(x0, y - hw), new(x1, y - hw), new(x1, y + hw), new(x0, y + hw)]);
        foreach (var sh in tr.Shafts)
        {
            var (p0, p1) = (sh.P0, sh.P1);
            yield return new Component
            {
                Id = sh.Id, Kind = "shaft", Shape = "segment", Position = sh.Position,
                P0 = [Math.Round(p0.X, 3), Math.Round(p0.Y, 3), Z(p0.Z)], P1 = [Math.Round(p1.X, 3), Math.Round(p1.Y, 3), Z(p1.Z)],
                R = Propulsion.ShaftR, Points = Rect(p1.X, p0.X, p0.Y, Propulsion.ShaftR), Base = Z(Math.Min(p0.Z, p1.Z) - Propulsion.ShaftR),
                Top = Z(Math.Max(p0.Z, p1.Z) + Propulsion.ShaftR), LeavesHullX = Math.Round(sh.ExitX, 3), Propeller = sh.Propeller,
                EngineRoom = NonEmpty(sh.EngineRoom), Alley = NonEmpty(sh.Alley),
            };
        }
        foreach (var a in tr.Alleys)
            yield return new Component
            {
                Id = a.Id, Kind = "shaft_alley", Shape = "polygon", Shaft = a.Shaft, Points = Rect(a.X0, a.X1, a.Y, Propulsion.AlleyW / 2),
                Base = Z(a.Base), Top = Z(a.Top),
            };
        foreach (var p in tr.Propellers)
        {
            double r = p.Diameter / 2;
            yield return new Component
            {
                Id = p.Id, Kind = "propeller", Shape = "disc", X = Math.Round(p.X, 3), Y = Math.Round(p.Y, 3), Z = Z(p.Z),
                DiameterM = Math.Round(p.Diameter, 2), Position = p.Position, Shaft = p.Shaft, Points = Rect(p.X - 0.25 * r, p.X + 0.25 * r, p.Y, r),
                Base = Z(p.Z - r), Top = Z(p.Z + r),
            };
        }
        foreach (var rd in tr.Rudders)
            yield return new Component
            {
                Id = rd.Id, Kind = "rudder", Shape = "polygon", X = Math.Round(rd.X, 3), Y = Math.Round(rd.Y, 3), AreaM2 = Math.Round(rd.AreaM2, 1),
                Points = Rect(rd.X0, rd.X1, rd.Y, rd.Thick / 2), Base = Z(rd.Base), Top = Z(rd.Top), Steering = NonEmpty(tr.Steering),
            };
    }

    /// <summary>The mounts, and the barbettes under those that have one.</summary>
    static IEnumerable<Component> MountComponents(Layout lay, Design design, ArmourLayout ag, double D, double T)
    {
        double barbetteZ = ag.MainZ ?? (ag.BeltMm > 0 ? ag.BeltTop : Math.Max(T, D - Geometry.DeckPitch));
        double fdBase = lay.Decks.Where(dk => dk.Kind == "flight_deck").Select(dk => dk.Base).Append(1e9).Min();
        foreach (var m in lay.Mounts)
        {
            var t = m.T;
            var sh = Geometry.TurretShapesOf(t);
            double arm = m.ArmourMm;
            string? Own(string part) => NonEmpty(m.Material) ?? Armour.ArmourMaterial(design, part);
            string? mat = m.Kind == "main" ? Own("turrets") : m.Kind == "secondary" ? Own("secondary") : null;
            var c = new Component
            {
                Id = m.Id, Kind = m.Kind, Type = m.Type, CalibreMm = t.CalibreMm, CalibreLength = t.CalibreLength, X = Math.Round(m.X, 3),
                Y = Math.Round(m.Y, 3), Base = Math.Round(m.Base, 2), Top = Math.Round(m.Top, 2), ArmourMm = arm,
                BroadphaseR = Math.Round(Math.Max(t.R, Geometry.TurretReach(t, 0.0)), 3), Rotating = m.Fixed is null, RestDeg = m.Rest,
                ArcsDeg = m.Arcs, TraverseDeg = m.Traverse,
                Local = new TurretLocal(R3(sh.Body), sh.Parts.Select(R3).ToList(), sh.Barrels.Select(R3).ToList()),
                Magazine = NonEmpty(m.Magazine), Mount = m.Casemate ? "casemate" : null,
            };
            if (m.Kind is "main" or "secondary")
            {
                c.Armour = new TurretArmour(arm, (long)Math.Round(TurretSide * arm), (long)Math.Round(TurretRear * arm), (long)Math.Round(TurretRoof * arm));
                c.Material = NonEmpty(mat);
            }
            if (m.Kind == "torpedo")
                (c.Torpedoes, c.WarheadKg) = (t.Barrels, (long)Math.Round(Ordnance.WarheadKg()));
            else
            {
                long rounds = (long)Math.Round(Batteries.GunRounds(t) * t.Barrels);
                var (n, w) = Ordnance.ReadyUse(t.CalibreMm, t.Barrels, rounds);
                (c.Rounds, c.ReadyRounds, c.ReadyT) = (rounds, n, Math.Round(w, 2));
            }
            yield return c;
            if (!t.HasBarbette)
                continue;
            double br = Geometry.BarbetteR(t);
            bool inHull = Math.Abs(m.Y) + br <= lay.MountHalfWidth(m.X) && m.Base < fdBase;
            c.Barbette = $"{m.Id} barbette";
            yield return new Component
            {
                Id = c.Barbette, Kind = "barbette", Mount = m.Id, Shape = "circle", X = Math.Round(m.X, 3), Y = Math.Round(m.Y, 3),
                R = Math.Round(br, 3), Base = inHull ? Math.Min(Math.Round(barbetteZ - D, 2), Math.Round(m.Base, 2)) : Math.Round(m.Base - 1.0, 2),
                Top = Math.Round(m.Base, 2), ArmourMm = (long)Math.Round(Barbette * arm), Material = NonEmpty(m.Kind == "main" ? Own("barbettes") : mat),
            };
        }
    }

    /// <summary>The superstructure blocks (directors among them), the conning tower, funnels, uptakes and casings.</summary>
    static IEnumerable<Component> UpperworkComponents(Layout lay, Design design, ArmourLayout ag, double D)
    {
        var directors = lay.Directors.ToDictionary(d => d.Id, StringComparer.Ordinal);
        var quarters = lay.Crew?.SuperstructureQuarters ?? new OrderedDictionary<string, long>();
        foreach (var b in lay.Blocks)
        {
            directors.TryGetValue(b.Id, out var d);
            var smoke = lay.Smoke.GetValueOrDefault(b.Id);
            yield return new Component
            {
                Id = b.Id, Kind = "superstructure", Role = b.Role, Level = b.Level, Shape = "polygon", Points = R3(Geometry.BlockOutline(b)),
                Rrect = b.Points is { Count: > 0 } ? null
                    : new Rrect(Math.Round(b.X0, 3), Math.Round(b.X1, 3), Math.Round(b.Y - b.W / 2, 3), Math.Round(b.Y + b.W / 2, 3),
                        Math.Round(b.Rf, 3), Math.Round(b.Rb, 3)),
                Base = Math.Round(b.Base, 2), Top = Math.Round(b.TopZ, 2),
                Battery = d?.Battery, RangefinderM = d?.Spec.RangefinderM, ArmourMm = d?.Spec.ArmourMm, Radar = d is null ? null : d.Spec.RadarT > 0,
                Material = d is null ? NonEmpty(design.Superstructure?.Material) : null,
                PlateMm = b.PlateMm, Crew = quarters.GetValueOrDefault(b.Id) is var men and not 0 ? men : null,
                Smoke = smoke is { Count: > 0 } ? [.. smoke] : null,
            };
        }
        if (lay.ConningTower is { } ct)
            yield return new Component
            {
                Id = "Conning tower", Kind = "conning_tower", Shape = "circle", X = Math.Round(ct.X, 3), Y = Math.Round(ct.Y, 3), R = Math.Round(ct.R, 3),
                Base = 0.0, Top = Math.Round(ct.Top, 2), ArmourMm = ag.BeltMm, Material = NonEmpty(Armour.ArmourMaterial(design, "conning_tower")),
            };
        var plan = lay.Geo.Plant;
        foreach (var f in lay.Funnels)
        {
            var pts = R3(Geometry.RrectPolygon(f.X - f.L / 2, f.Y - f.W / 2, f.X + f.L / 2, f.Y + f.W / 2, f.W / 2, f.W / 2));
            yield return new Component
            {
                Id = f.Id, Kind = "funnel", Shape = "polygon", Points = pts, Base = f.Z0 ?? 0.0, Top = Math.Round(lay.FunTop, 2), BoilerRooms = f.Serves ?? [],
                Pipes = f.Pipes,
            };
            if (plan != null && f.Serves != null)
                yield return new Component
                {
                    Id = $"{f.Id} uptakes", Kind = "uptake", Funnel = f.Id, Shape = "polygon", Points = pts,
                    Base = Math.Round(plan.InnerBottom + plan.Space.Unit.H - D, 2), Top = f.Z0 ?? 0.0, BoilerRooms = f.Serves,
                };
        }
        foreach (var c in lay.Casings)
            yield return new Component
            {
                Id = c.Id, Kind = "casing", Shape = "polygon", Points = R3(Geometry.RrectPolygon(c.X0, -c.W / 2, c.X1, c.W / 2, 0.5, 0.5)),
                Base = Math.Round(c.Base, 2), Top = Math.Round(c.Top, 2), ArmourMm = c.ArmourMm, Material = c.ArmourMm != 0 ? NonEmpty(ag.RoofMaterial) : null,
            };
    }

    /// <summary>hitboxes.json. inner: the ship's interior (ShipDesign.InteriorOf).</summary>
    public static Hitboxes ExportHitboxes(Layout lay, Design design, Navarch.Result res, ShipDesign.Interior inner)
    {
        double D = res.Depth, T = res.Draught;
        double Rz(double z) => Math.Round(z - D, 2);
        var ag = inner.Armour;
        var comps = MountComponents(lay, design, ag, D, T).Concat(UpperworkComponents(lay, design, ag, D)).ToList();
        double fdMm = design.Armour?.FlightDeckMm ?? 0;
        foreach (var dk in lay.Decks.Where(d => d.Kind != "deck").Concat(lay.Sponsons))
        {
            bool armoured = dk.Kind == "flight_deck" && fdMm != 0;
            comps.Add(new Component
            {
                Id = dk.Id, Kind = dk.Kind, Role = dk.Role, Shape = "polygon", Points = R3(dk.Points), Base = Math.Round(dk.Base, 2), Top = Math.Round(dk.Top, 2),
                ArmourMm = armoured ? fdMm : null, Material = armoured ? NonEmpty(Armour.ArmourMaterial(design, "flight_deck")) : null,
                WoodMm = inner.Planked.Contains(dk.Id) ? inner.Plating.DeckWoodMm : null,
            });
        }
        foreach (var a in lay.Aa)
        {
            var cfg = Geometry.AaCfg[a.Type];
            var (n, w) = Ordnance.ReadyUse(cfg.CalibreMm, cfg.Barrels);
            comps.Add(new Component
            {
                Id = a.Id, Kind = "aa", Type = a.Type, CalibreMm = cfg.CalibreMm, CalibreLength = cfg.CalibreLength, Shape = "circle",
                X = Math.Round(a.X, 3), Y = Math.Round(a.Y, 3), R = cfg.R, Base = a.Base, Top = a.Base + 2.0, RestDeg = a.Dir, ReadyRounds = n,
                ReadyT = Math.Round(w, 2),
            });
        }
        foreach (var m in lay.Masts)   // a pole from the deck it is stepped on, through any deckhouse round it
        {
            double x = m.X, y = m.Y ?? 0.0, bse = 0.0;
            foreach (var dk in lay.Decks)
                if (dk.Top > bse && Geometry.PointInPolygon(x, y, dk.Points))
                    bse = dk.Top;
            comps.Add(new Component
            {
                Id = m.Id!, Kind = "mast", Shape = "circle", X = Math.Round(x, 3), Y = Math.Round(y, 3), R = Mast.PoleR, Base = Math.Round(bse, 2),
                Top = Math.Round(m.Top!.Value, 2),
            });
        }
        foreach (var c in lay.Compartments.Where(c => c.Kind == "hangar"))
            comps.Add(new Component
            {
                Id = c.Id, Kind = "hangar_bay", Shape = "polygon", Points = R3(Geometry.RrectPolygon(c.X0, -c.HalfWidth, c.X1, c.HalfWidth, 0.0, 0.0)),
                Base = Math.Round(c.Base!.Value, 2), Top = Math.Round(c.Top!.Value, 2),
            });
        comps.AddRange(PropulsionComponents(inner.Propulsion, D));
        foreach (var x in comps)
            if (inner.BattleCrew.TryGetValue((x.Kind, x.Id), out var bc) && bc != 0)
                x.BattleCrew = bc;

        var armour = new ArmourReport();
        if (ag.BeltMm > 0)
            armour.Belt = new BeltReport(ag.BeltMm, Math.Round(ag.X0, 3), Math.Round(ag.X1, 3), Rz(ag.BeltBottom), Rz(ag.BeltTop))
            {
                Material = NonEmpty(ag.BeltMaterial),
                BottomMm = ag.BeltBottomMm != ag.BeltMm ? ag.BeltBottomMm : null,
                TaperFrom = ag.BeltBottomMm != ag.BeltMm ? Rz(Math.Min(ag.BeltTop, ag.Waterline)) : null,
            };
        if (ag.Strakes.Count > 0)
            armour.Strakes = ag.Strakes.Select(st => new StrakeReport(st.Id, st.Kind, st.Extent, st.Mm, Math.Round(st.X0, 3), Math.Round(st.X1, 3),
                Rz(st.Bottom), Rz(st.Top)) { TipMm = st.TipMm != st.Mm ? st.TipMm : null, Material = NonEmpty(st.Material) }).ToList();
        var bulkheads = new List<ArmourBulkheadReport>();
        if (ag.Armoured && ag.BulkheadMm > 0)
            bulkheads.AddRange(new[] { ("Forward", ag.X1), ("Aft", ag.X0) }.Select(t => new ArmourBulkheadReport($"{t.Item1} bulkhead",
                Math.Round(t.Item2, 3), Math.Round(ag.BulkheadMm), Rz(ag.BulkheadBottom), Rz(ag.BulkheadTop), NonEmpty(ag.BulkheadMaterial))));
        bulkheads.AddRange(ag.EndBulkheads.Concat(ag.EndPlates).Select(b => new ArmourBulkheadReport(b.Id, Math.Round(b.X, 3), Math.Round(b.Mm), Rz(b.Bottom), Rz(b.Top),
            NonEmpty(b.Material))));
        if (bulkheads.Count > 0)
            armour.Bulkheads = bulkheads;
        if (ag.Decks.Count > 0)
            armour.Decks = ag.Decks.Select(d => new ArmourDeckReport(Decks.DeckName(d.Deck), d.Mm, d.Extent, Math.Round(d.X0, 3), Math.Round(d.X1, 3),
                Rz(d.Z), d.Z == ag.MainZ, d.Z == ag.RoofZ, NonEmpty(d.Material))).ToList();

        var form = inner.Form;
        var sub = inner.Subdivision;
        return new Hitboxes
        {
            Length = lay.Hull.L, Beam = lay.Hull.B,
            Vertical = new Vertical
            {
                Keel = -Math.Round(D, 2), Waterline = -Math.Round(D - T, 2), ArmourDeck = ag.MainZ is double mz ? Rz(mz) : null,
                Draught = Math.Round(T, 2), Depth = Math.Round(D, 2), Freeboard = Math.Round(D - T, 2),
                Raised = lay.Raised.Count > 0
                    ? lay.Raised.Select(st => new RaisedReport(st.Id, Math.Round(st.X0, 3), Math.Round(st.X1, 3), Math.Round(st.Levels * Geometry.DeckPitch, 2))).ToList()
                    : null,
            },
            Hull = R3(lay.Side(0.0).Points()), Bow = lay.Hull.Bow, Stern = lay.Hull.Stern, Hydrostatics = inner.Hydrostatics,
            HullForm = new HullFormReport(Math.Round(form.Cm, 3), Math.Round(form.Cwp, 3), form.Table(tops: lay.Raised.Select(st => st.Levels * Geometry.DeckPitch)).Select(s => new StationReport(Math.Round(s.X, 3),
                s.Z.Select(z => Math.Round(z - D, 2)).ToArray(), s.Y.Select(y => Math.Round(y, 3)).ToArray())).ToList()),
            Armour = armour, Plating = inner.Plating, TurretTypes = lay.TurretTypes, Components = comps,
            Decks = sub.Decks, Tiers = sub.Tiers, Sections = sub.Sections, Bulkheads = sub.Bulkheads, Cells = sub.Cells, Rooms = sub.Rooms,
        };
    }
}
