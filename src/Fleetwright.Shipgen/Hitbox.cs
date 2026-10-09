namespace Fleetwright.Shipgen;

/// <summary>hitbox: hitbox export. Components with exact shapes in ship-local metres and heights above the main deck.</summary>
public static class Hitbox
{
    const double TURRET_SIDE = 0.55, TURRET_REAR = 0.5, TURRET_ROOF = 0.4, BARBETTE = 0.8;

    static List<object?> R3(IEnumerable<Pt> pts) => pts.Select(p => (object?)new List<object?> { Math.Round(p.X, 3), Math.Round(p.Y, 3) }).ToList();

    static (double X, double Y, double Z) P3(object? v)
    {
        var a = (object?[])v!;
        return (Py.ToDouble(a[0]), Py.ToDouble(a[1]), Py.ToDouble(a[2]));
    }

    /// <summary>The propulsion train as components.</summary>
    static List<PyDict> PropulsionComponents(PyDict tr, double D)
    {
        double Z(double v) => Math.Round(v - D, 2);
        List<object?> Rect(double x0, double x1, double y, double hw) =>
        [
            new List<object?> { Math.Round(x0, 3), Math.Round(y - hw, 3) }, new List<object?> { Math.Round(x1, 3), Math.Round(y - hw, 3) },
            new List<object?> { Math.Round(x1, 3), Math.Round(y + hw, 3) }, new List<object?> { Math.Round(x0, 3), Math.Round(y + hw, 3) },
        ];
        var out_ = new List<PyDict>();
        var steering = tr.Get("steering");
        foreach (PyDict sh in tr.L("shafts").Cast<PyDict>())
        {
            var (x0, y, z0) = P3(sh["p0"]);
            var (x1, _, z1) = P3(sh["p1"]);
            var c = PyDict.Of(("id", sh["id"]), ("kind", "shaft"), ("shape", "segment"), ("position", sh["position"]),
                ("p0", new List<object?> { Math.Round(x0, 3), Math.Round(y, 3), Z(z0) }),
                ("p1", new List<object?> { Math.Round(x1, 3), Math.Round(y, 3), Z(z1) }), ("r", Propulsion.SHAFT_R),
                ("points", Rect(x1, x0, y, Propulsion.SHAFT_R)), ("base", Z(Math.Min(z0, z1) - Propulsion.SHAFT_R)),
                ("top", Z(Math.Max(z0, z1) + Propulsion.SHAFT_R)), ("leaves_hull_x", Math.Round(sh.F("exit_x"), 3)), ("propeller", sh["propeller"]));
            if (Py.Truthy(sh["engine_room"]))
                c["engine_room"] = sh["engine_room"];
            if (sh.B("alley"))
                c["alley"] = sh["alley"];
            out_.Add(c);
        }
        foreach (PyDict a in tr.L("alleys").Cast<PyDict>())
            out_.Add(PyDict.Of(("id", a["id"]), ("kind", "shaft_alley"), ("shape", "polygon"), ("shaft", a["shaft"]),
                ("points", Rect(a.F("x0"), a.F("x1"), a.F("y"), Propulsion.ALLEY_W / 2)), ("base", Z(a.F("base"))), ("top", Z(a.F("top")))));
        foreach (PyDict p in tr.L("propellers").Cast<PyDict>())
        {
            double r = p.F("diameter") / 2;
            out_.Add(PyDict.Of(("id", p["id"]), ("kind", "propeller"), ("shape", "disc"), ("x", Math.Round(p.F("x"), 3)),
                ("y", Math.Round(p.F("y"), 3)), ("z", Z(p.F("z"))), ("diameter_m", Math.Round(p.F("diameter"), 2)), ("position", p["position"]),
                ("shaft", p["shaft"]), ("points", Rect(p.F("x") - 0.25 * r, p.F("x") + 0.25 * r, p.F("y"), r)), ("base", Z(p.F("z") - r)),
                ("top", Z(p.F("z") + r))));
        }
        foreach (PyDict rd in tr.L("rudders").Cast<PyDict>())
        {
            var c = PyDict.Of(("id", rd["id"]), ("kind", "rudder"), ("shape", "polygon"), ("x", Math.Round(rd.F("x"), 3)),
                ("y", Math.Round(rd.F("y"), 3)), ("area_m2", Math.Round(rd.F("area_m2"), 1)),
                ("points", Rect(rd.F("x0"), rd.F("x1"), rd.F("y"), rd.F("thick") / 2)), ("base", Z(rd.F("base"))), ("top", Z(rd.F("top"))));
            if (Py.Truthy(steering))
                c["steering"] = steering;
            out_.Add(c);
        }
        return out_;
    }

    /// <summary>hitboxes.json. inner: the ship's interior (shipdesign.interior).</summary>
    public static PyDict ExportHitboxes(Layout lay, Design design, Navarch.Result res, ShipDesign.Interior inner)
    {
        double D = res.Depth, T = res.Draught;
        double Rz(double z) => Math.Round(z - D, 2);
        var ag = inner.Armour;
        bool armoured = ag.Armoured;
        double barbetteZ = ag.MainZ ?? (ag.BeltMm > 0 ? ag.BeltTop : Math.Max(T, D - Geometry.DECK_PITCH));
        double fdBase = lay.Decks.Where(dk => dk.Kind == "flight_deck").Select(dk => dk.Base).Append(1e9).Min();
        var comps = new List<PyDict>();

        PyDict WithMaterial(PyDict d, string? m)
        {
            if (!string.IsNullOrEmpty(m))
                d["material"] = m;
            return d;
        }

        foreach (var m in lay.Mounts)
        {
            var t = m.T;
            var sh = Geometry.TurretShapesOf(t);
            double arm = m.ArmourMm;
            var c = PyDict.Of(("id", m.Id), ("kind", m.Kind), ("type", m.Type));
            c.Update(Geometry.GunOf(t));
            c.Update(("x", Math.Round(m.X, 3)), ("y", Math.Round(m.Y, 3)), ("base", Math.Round(m.Base, 2)),
                ("top", Math.Round(m.Top, 2)), ("armour_mm", arm), ("broadphase_r", Math.Round(Math.Max(t.R, Geometry.TurretReach(t, 0.0)), 3)),
                ("rotating", m.Fixed is null), ("rest_deg", m.Rest), ("arcs_deg", m.Arcs), ("traverse_deg", m.Traverse),
                ("local", PyDict.Of(("body", R3(sh.Body)), ("parts", sh.Parts.Select(p => (object?)R3(p)).ToList()),
                    ("barrels", sh.Barrels.Select(p => (object?)R3(p)).ToList()))));
            comps.Add(c);
            string? Own(string part) => !string.IsNullOrEmpty(m.Material) ? m.Material : Armour.ArmourMaterial(design, part);
            string? mat = m.Kind == "main" ? Own("turrets") : m.Kind == "secondary" ? Own("secondary") : null;
            if (m.Kind is "main" or "secondary")
            {
                c["armour"] = PyDict.Of(("face", arm), ("side", (long)Math.Round(TURRET_SIDE * arm)), ("rear", (long)Math.Round(TURRET_REAR * arm)),
                    ("roof", (long)Math.Round(TURRET_ROOF * arm)));
                WithMaterial(c, mat);
            }
            if (m.Kind == "torpedo")
                c.Update(("torpedoes", t.Barrels), ("warhead_kg", (long)Math.Round(Ordnance.WarheadKg())));
            else
            {
                long rounds = (long)Math.Round(Batteries.GunRounds(t) * t.Barrels);
                var (n, w) = Ordnance.ReadyUse(t.CalibreMm, t.Barrels, rounds);
                c.Update(("rounds", rounds), ("ready_rounds", n), ("ready_t", Math.Round(w, 2)));
            }
            if (!string.IsNullOrEmpty(m.Magazine))
                c["magazine"] = m.Magazine;
            if (m.Casemate)
                c["mount"] = "casemate";
            if (t.HasBarbette)
            {
                bool inHull = Math.Abs(m.Y) + 0.95 * t.R <= lay.Hull.HalfWidth(m.X) && m.Base < fdBase;
                c["barbette"] = $"{m.Id} barbette";
                var bb = PyDict.Of(("id", $"{m.Id} barbette"), ("kind", "barbette"), ("mount", m.Id), ("shape", "circle"),
                    ("x", Math.Round(m.X, 3)), ("y", Math.Round(m.Y, 3)), ("r", Math.Round(t.R * 0.95, 3)),
                    ("base", inHull ? Math.Min(Rz(barbetteZ), Math.Round(m.Base, 2)) : Math.Round(m.Base - 1.0, 2)),
                    ("top", Math.Round(m.Base, 2)), ("armour_mm", (long)Math.Round(BARBETTE * arm)));
                comps.Add(bb);
                WithMaterial(bb, m.Kind == "main" ? Own("barbettes") : mat);
            }
        }
        var directors = lay.Directors.ToDictionary(d => d.Id, StringComparer.Ordinal);
        var supMaterial = design.Superstructure?.Material;
        var quarters = lay.Crew?.SuperstructureQuarters ?? new OrderedDictionary<string, long>();
        foreach (var b in lay.Blocks)
        {
            var pts = Geometry.BlockOutline(b);
            lay.Smoke.TryGetValue(b.Id, out var smoke);
            var c = PyDict.Of(("id", b.Id), ("kind", "superstructure"), ("role", b.Role), ("shape", "polygon"), ("points", R3(pts)));
            if (b.Points is not { Count: > 0 })
                c["rrect"] = PyDict.Of(("x0", Math.Round(b.X0, 3)), ("x1", Math.Round(b.X1, 3)), ("y0", Math.Round(b.Y - b.W / 2, 3)),
                    ("y1", Math.Round(b.Y + b.W / 2, 3)), ("rf", Math.Round(b.Rf, 3)), ("rb", Math.Round(b.Rb, 3)));
            c.Update(("base", Math.Round(b.Base, 2)), ("top", Math.Round(b.TopZ, 2)));
            comps.Add(c);
            if (directors.TryGetValue(b.Id, out var d))
                c.Update(("battery", d.Battery), ("rangefinder_m", d.Spec.RangefinderM), ("armour_mm", d.Spec.ArmourMm),
                    ("radar", d.Spec.RadarT > 0));
            else
                WithMaterial(c, supMaterial);
            if (b.PlateMm is double plate)
                c["plate_mm"] = plate;
            if (quarters.TryGetValue(b.Id, out var men) && men != 0)
                c["crew"] = men;
            if (smoke is { Count: > 0 })
                c["smoke"] = smoke.ToList();
        }
        var ct = lay.ConningTower;
        if (ct != null)
        {
            var c = PyDict.Of(("id", "Conning tower"), ("kind", "conning_tower"), ("shape", "circle"), ("x", Math.Round(ct.X, 3)),
                ("y", Math.Round(ct.Y, 3)), ("r", Math.Round(ct.R, 3)), ("base", 0.0), ("top", Math.Round(ct.Top, 2)),
                ("armour_mm", ag.BeltMm));
            comps.Add(c);
            WithMaterial(c, Armour.ArmourMaterial(design, "conning_tower"));
        }
        var plan = lay.Geo.Plant;
        foreach (var f in lay.Funnels)
        {
            var pts = R3(Geometry.RrectPolygon(f.X - f.L / 2, f.Y - f.W / 2, f.X + f.L / 2, f.Y + f.W / 2, f.W / 2, f.W / 2));
            comps.Add(PyDict.Of(("id", f.Id), ("kind", "funnel"), ("shape", "polygon"), ("points", pts), ("base", f.Z0 ?? 0.0),
                ("top", Math.Round(lay.FunTop, 2)), ("boiler_rooms", f.Serves ?? [])));
            if (plan != null && f.Serves != null)
                comps.Add(PyDict.Of(("id", $"{f.Id} uptakes"), ("kind", "uptake"), ("funnel", f.Id), ("shape", "polygon"),
                    ("points", pts), ("base", Math.Round(plan.InnerBottom + plan.Space.Unit.H - D, 2)),
                    ("top", f.Z0 ?? 0.0), ("boiler_rooms", f.Serves)));
        }
        foreach (var c in lay.Casings)
        {
            var pts = Geometry.RrectPolygon(c.X0, -c.W / 2, c.X1, c.W / 2, 0.5, 0.5);
            var cc = PyDict.Of(("id", c.Id), ("kind", "casing"), ("shape", "polygon"), ("points", R3(pts)),
                ("base", Math.Round(c.Base, 2)), ("top", Math.Round(c.Top, 2)), ("armour_mm", c.ArmourMm));
            comps.Add(cc);
            if (c.ArmourMm != 0)
                WithMaterial(cc, ag.RoofMaterial);
        }
        foreach (var dk in lay.Decks.Where(d => d.Kind != "deck").Concat(lay.Sponsons))
        {
            var c = PyDict.Of(("id", dk.Id), ("kind", dk.Kind), ("shape", "polygon"), ("points", R3(dk.Points)),
                ("base", Math.Round(dk.Base, 2)), ("top", Math.Round(dk.Top, 2)));
            comps.Add(c);
            double fdMm = design.Armour?.FlightDeckMm ?? 0;
            if (dk.Kind == "flight_deck" && fdMm != 0)
            {
                c["armour_mm"] = fdMm;
                WithMaterial(c, Armour.ArmourMaterial(design, "flight_deck"));
            }
            if (inner.Planked.Contains(dk.Id))
                c["wood_mm"] = inner.Plating.DeckWoodMm;
        }
        foreach (var a in lay.Aa)
        {
            var cfg = Geometry.AA_CFG[a.Type];
            var (n, w) = Ordnance.ReadyUse(cfg.CalibreMm, cfg.Barrels);
            comps.Add(PyDict.Of(("id", a.Id), ("kind", "aa"), ("type", a.Type), ("calibre_mm", cfg.CalibreMm),
                ("calibre_length", cfg.CalibreLength), ("shape", "circle"), ("x", Math.Round(a.X, 3)), ("y", Math.Round(a.Y, 3)),
                ("r", cfg.R), ("base", a.Base), ("top", a.Base + 2.0), ("ready_rounds", n), ("ready_t", Math.Round(w, 2))));
        }
        foreach (var c in lay.Compartments)
            if (c.Kind == "hangar")
            {
                var pts = Geometry.RrectPolygon(c.X0, -c.HalfWidth, c.X1, c.HalfWidth, 0.0, 0.0);
                comps.Add(PyDict.Of(("id", c.Id), ("kind", "hangar_bay"), ("shape", "polygon"), ("points", R3(pts)),
                    ("base", Math.Round(c.Base!.Value, 2)), ("top", Math.Round(c.Top!.Value, 2))));
            }
        comps.AddRange(PropulsionComponents(inner.Propulsion, D));
        foreach (var x in comps)
            if (inner.BattleCrew.TryGetValue((x.S("kind"), x.S("id")), out var bc) && bc != 0)
                x["battle_crew"] = bc;
        var form = inner.Form;
        var sub = inner.Subdivision;
        var armOut = new PyDict();
        if (ag.BeltMm > 0)
        {
            armOut["belt"] = WithMaterial(PyDict.Of(("thickness_mm", ag.BeltMm), ("x0", Math.Round(ag.X0, 3)), ("x1", Math.Round(ag.X1, 3)),
                ("bottom", Rz(ag.BeltBottom)), ("top", Rz(ag.BeltTop))), ag.BeltMaterial);
            if (ag.BeltBottomMm != ag.BeltMm)
                armOut.D("belt").Update(("bottom_mm", ag.BeltBottomMm), ("taper_from", Rz(Math.Min(ag.BeltTop, ag.Waterline))));
        }
        if (ag.Strakes.Count > 0)
            armOut["strakes"] = ag.Strakes.Select(st =>
            {
                var d = PyDict.Of(("id", st.Id), ("kind", st.Kind), ("extent", st.Extent), ("thickness_mm", st.Mm));
                if (st.TipMm != st.Mm)
                    d["tip_mm"] = st.TipMm;
                d.Update(("x0", Math.Round(st.X0, 3)), ("x1", Math.Round(st.X1, 3)), ("bottom", Rz(st.Bottom)), ("top", Rz(st.Top)));
                return (object?)WithMaterial(d, st.Material);
            }).ToList();
        if (armoured && ag.BulkheadMm > 0)
            armOut["bulkheads"] = new[] { ("Forward", ag.X1), ("Aft", ag.X0) }.Select(t =>
                (object?)WithMaterial(PyDict.Of(("id", $"{t.Item1} bulkhead"), ("x", Math.Round(t.Item2, 3)),
                    ("thickness_mm", Math.Round(ag.BulkheadMm)), ("bottom", Rz(ag.BulkheadBottom)), ("top", Rz(ag.BulkheadTop))),
                    ag.BulkheadMaterial)).ToList();
        if (ag.EndBulkheads.Count > 0)
        {
            var l = armOut.SetDefault("bulkheads", new List<object?>()) as List<object?>;
            l!.AddRange(ag.EndBulkheads.Select(b => (object?)WithMaterial(PyDict.Of(("id", b.Id), ("x", Math.Round(b.X, 3)),
                ("thickness_mm", Math.Round(b.Mm)), ("bottom", Rz(b.Bottom)), ("top", Rz(b.Top))), b.Material)));
        }
        if (ag.Decks.Count > 0)
            armOut["decks"] = ag.Decks.Select(d => (object?)WithMaterial(PyDict.Of(("deck", Decks.DeckName(d.Deck)), ("thickness_mm", d.Mm),
                ("extent", d.Extent), ("x0", Math.Round(d.X0, 3)), ("x1", Math.Round(d.X1, 3)), ("z", Rz(d.Z)),
                ("main", d.Z == ag.MainZ), ("roof", d.Z == ag.RoofZ)), d.Material)).ToList();
        var vertical = PyDict.Of(("keel", -Math.Round(D, 2)), ("waterline", -Math.Round(D - T, 2)),
            ("armour_deck", ag.MainZ is double mz ? Rz(mz) : null), ("draught", Math.Round(T, 2)), ("depth", Math.Round(D, 2)),
            ("freeboard", Math.Round(D - T, 2)));
        if (lay.Raised.Count > 0)
            vertical["raised"] = lay.Raised.Select(st => (object?)PyDict.Of(("id", st.Id), ("x0", Math.Round(st.X0, 3)),
                ("x1", Math.Round(st.X1, 3)), ("top", Math.Round(st.Levels * Geometry.DECK_PITCH, 2)))).ToList();
        var out_ = PyDict.Of(("units", "metres"),
            ("frame", "ship-local: origin = ship centre = sprite centre, +x toward bow, +y toward starboard; " +
                      "angles clockwise from dead ahead"),
            ("heights", "base/top/z are metres above the main deck (negative = below it)"),
            ("turret_local", "turret 'local' polygons are in turret space (pivot at 0,0, barrels along +x); " +
                             "rotate by the current turret angle, then add (x, y)"),
            ("length", lay.Hull.L), ("beam", lay.Hull.B), ("vertical", vertical), ("hull", R3(lay.Hull.Points())),
            ("hydrostatics", inner.Hydrostatics),
            ("hull_form", PyDict.Of(("midship_coefficient", Math.Round(form.Cm, 3)), ("waterplane_coefficient", Math.Round(form.Cwp, 3)),
                ("stations", form.Table().Select(s => (object?)PyDict.Of(("x", Math.Round(s.X, 3)),
                    ("z", s.Z.Select(z => (object?)Math.Round(z - D, 2)).ToList()), ("y", s.Y.Select(y => (object?)Math.Round(y, 3)).ToList()))).ToList()))),
            ("armour", armOut), ("plating", inner.Plating), ("components", comps.Cast<object?>().ToList()));
        out_.Update(sub);
        return out_;
    }
}
