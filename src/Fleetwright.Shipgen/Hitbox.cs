namespace Fleetwright.Shipgen;

/// <summary>hitbox: hitbox export. Components with exact shapes in ship-local metres and heights above the main deck.</summary>
public static class Hitbox
{
    const double TURRET_SIDE = 0.55, TURRET_REAR = 0.5, TURRET_ROOF = 0.4, BARBETTE = 0.8;

    static List<object?> R3(IEnumerable<Pt> pts) => pts.Select(p => (object?)new List<object?> { Py.Round(p.X, 3), Py.Round(p.Y, 3) }).ToList();

    static (double X, double Y, double Z) P3(object? v)
    {
        var a = (object?[])v!;
        return (Py.ToDouble(a[0]), Py.ToDouble(a[1]), Py.ToDouble(a[2]));
    }

    /// <summary>The propulsion train as components.</summary>
    static List<PyDict> PropulsionComponents(PyDict tr, double D)
    {
        double Z(double v) => Py.Round(v - D, 2);
        List<object?> Rect(double x0, double x1, double y, double hw) =>
        [
            new List<object?> { Py.Round(x0, 3), Py.Round(y - hw, 3) }, new List<object?> { Py.Round(x1, 3), Py.Round(y - hw, 3) },
            new List<object?> { Py.Round(x1, 3), Py.Round(y + hw, 3) }, new List<object?> { Py.Round(x0, 3), Py.Round(y + hw, 3) },
        ];
        var out_ = new List<PyDict>();
        var steering = tr.Get("steering");
        foreach (PyDict sh in tr.L("shafts").Cast<PyDict>())
        {
            var (x0, y, z0) = P3(sh["p0"]);
            var (x1, _, z1) = P3(sh["p1"]);
            var c = PyDict.Of(("id", sh["id"]), ("kind", "shaft"), ("shape", "segment"), ("position", sh["position"]),
                ("p0", new List<object?> { Py.Round(x0, 3), Py.Round(y, 3), Z(z0) }),
                ("p1", new List<object?> { Py.Round(x1, 3), Py.Round(y, 3), Z(z1) }), ("r", Propulsion.SHAFT_R),
                ("points", Rect(x1, x0, y, Propulsion.SHAFT_R)), ("base", Z(Py.Min(z0, z1) - Propulsion.SHAFT_R)),
                ("top", Z(Py.Max(z0, z1) + Propulsion.SHAFT_R)), ("leaves_hull_x", Py.Round(sh.F("exit_x"), 3)), ("propeller", sh["propeller"]));
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
            out_.Add(PyDict.Of(("id", p["id"]), ("kind", "propeller"), ("shape", "disc"), ("x", Py.Round(p.F("x"), 3)),
                ("y", Py.Round(p.F("y"), 3)), ("z", Z(p.F("z"))), ("diameter_m", Py.Round(p.F("diameter"), 2)), ("position", p["position"]),
                ("shaft", p["shaft"]), ("points", Rect(p.F("x") - 0.25 * r, p.F("x") + 0.25 * r, p.F("y"), r)), ("base", Z(p.F("z") - r)),
                ("top", Z(p.F("z") + r))));
        }
        foreach (PyDict rd in tr.L("rudders").Cast<PyDict>())
        {
            var c = PyDict.Of(("id", rd["id"]), ("kind", "rudder"), ("shape", "polygon"), ("x", Py.Round(rd.F("x"), 3)),
                ("y", Py.Round(rd.F("y"), 3)), ("area_m2", Py.Round(rd.F("area_m2"), 1)),
                ("points", Rect(rd.F("x0"), rd.F("x1"), rd.F("y"), rd.F("thick") / 2)), ("base", Z(rd.F("base"))), ("top", Z(rd.F("top"))));
            if (Py.Truthy(steering))
                c["steering"] = steering;
            out_.Add(c);
        }
        return out_;
    }

    /// <summary>hitboxes.json. inner: the ship's interior (shipdesign.interior).</summary>
    public static PyDict ExportHitboxes(Layout lay, PyDict design, Navarch.Result res, ShipDesign.Interior inner)
    {
        double D = res.Depth, T = res.Draught;
        double Rz(double z) => Py.Round(z - D, 2);
        var ag = inner.Armour;
        bool armoured = Py.Truthy(ag["armoured"]);
        double barbetteZ = ag["main_z"] is not null ? ag.F("main_z") : ag.F("belt_mm") > 0 ? ag.F("belt_top") : Py.Max(T, D - Geometry.DECK_PITCH);
        double fdBase = Py.Min(lay.Decks.Where(dk => Py.Eq(dk["kind"], "flight_deck")).Select(dk => dk.F("base")).Append(1e9));
        var comps = new List<PyDict>();

        PyDict WithMaterial(PyDict d, object? m)
        {
            if (Py.Truthy(m))
                d["material"] = m;
            return d;
        }

        foreach (var m in lay.Mounts)
        {
            var t = m.D("t");
            var sh = Geometry.TurretShapesOf(t);
            var arm = m.Get("armour_mm", 0L);
            var t0 = t.Copy();
            t0["barrel_len"] = 0L;
            var c = PyDict.Of(("id", m["id"]), ("kind", m["kind"]), ("type", m["type"]));
            c.Update(Geometry.GunOf(t));
            c.Update(("x", Py.Round(m.F("x"), 3)), ("y", Py.Round(m.F("y"), 3)), ("base", Py.Round(m.F("base"), 2)),
                ("top", Py.Round(m.F("top"), 2)), ("armour_mm", arm), ("broadphase_r", Py.Round(Py.Max(t.F("r"), Geometry.TurretReach(t0)), 3)),
                ("rotating", m.Get("fixed") is null), ("rest_deg", m["rest"]), ("arcs_deg", m["arcs"]), ("traverse_deg", m["traverse"]),
                ("local", PyDict.Of(("body", R3(sh.Body)), ("parts", sh.Parts.Select(p => (object?)R3(p)).ToList()),
                    ("barrels", sh.Barrels.Select(p => (object?)R3(p)).ToList()))));
            comps.Add(c);
            object? mat = Py.Eq(m["kind"], "main") ? Py.Or(m.Get("material"), Armour.ArmourMaterial(design, "turrets"))
                : Py.Eq(m["kind"], "secondary") ? Py.Or(m.Get("material"), Armour.ArmourMaterial(design, "secondary")) : null;
            if (Py.In(m["kind"], "main", "secondary"))
            {
                double a = Py.ToDouble(arm);
                c["armour"] = PyDict.Of(("face", arm), ("side", Py.Round(TURRET_SIDE * a)), ("rear", Py.Round(TURRET_REAR * a)),
                    ("roof", Py.Round(TURRET_ROOF * a)));
                WithMaterial(c, mat);
            }
            if (Py.Eq(m["kind"], "torpedo"))
                c.Update(("torpedoes", t["barrels"]), ("warhead_kg", Py.Round(Ordnance.WarheadKg())));
            else
            {
                long rounds = Py.Round(Batteries.GunRounds(t) * t.F("barrels"));
                var (n, w) = Ordnance.ReadyUse(t.F("calibre_mm"), t.F("barrels"), rounds);
                c.Update(("rounds", rounds), ("ready_rounds", n), ("ready_t", Py.Round(w, 2)));
            }
            if (m.B("magazine"))
                c["magazine"] = m["magazine"];
            if (m.B("casemate"))
                c["mount"] = "casemate";
            if (Geometry.HasBarbette(t))
            {
                bool inHull = Math.Abs(m.F("y")) + 0.95 * t.F("r") <= lay.Hull.HalfWidth(m.F("x")) && m.F("base") < fdBase;
                c["barbette"] = $"{m.S("id")} barbette";
                var bb = PyDict.Of(("id", $"{m.S("id")} barbette"), ("kind", "barbette"), ("mount", m["id"]), ("shape", "circle"),
                    ("x", Py.Round(m.F("x"), 3)), ("y", Py.Round(m.F("y"), 3)), ("r", Py.Round(t.F("r") * 0.95, 3)),
                    ("base", inHull ? Py.Min(Rz(barbetteZ), Py.Round(m.F("base"), 2)) : Py.Round(m.F("base") - 1.0, 2)),
                    ("top", Py.Round(m.F("base"), 2)), ("armour_mm", Py.Round(BARBETTE * Py.ToDouble(arm))));
                comps.Add(bb);
                WithMaterial(bb, Py.Eq(m["kind"], "main") ? Py.Or(m.Get("material"), Armour.ArmourMaterial(design, "barbettes")) : mat);
            }
        }
        var directors = new Dictionary<string, PyDict>(StringComparer.Ordinal);
        foreach (var d in lay.Directors)
            directors[d.S("id")] = d;
        var supMaterial = design.DOr("superstructure").Get("material");
        var quarters = (lay.Crew ?? new PyDict()).Get("superstructure_quarters", new PyDict()) as PyDict ?? new PyDict();
        foreach (var b in lay.Blocks)
        {
            var pts = Geometry.BlockOutline(b);
            lay.Smoke.TryGetValue(b.S("id"), out var smoke);
            var c = PyDict.Of(("id", b["id"]), ("kind", "superstructure"), ("role", b["role"]), ("shape", "polygon"), ("points", R3(pts)));
            if (!b.B("points"))
                c["rrect"] = PyDict.Of(("x0", Py.Round(b.F("x0"), 3)), ("x1", Py.Round(b.F("x1"), 3)), ("y0", Py.Round(b.F("y") - b.F("w") / 2, 3)),
                    ("y1", Py.Round(b.F("y") + b.F("w") / 2, 3)), ("rf", Py.Round(b.F("rf"), 3)), ("rb", Py.Round(b.F("rb"), 3)));
            c.Update(("base", Py.Round(Layout.BlockBase(b), 2)), ("top", Py.Round(Layout.BlockTop(b), 2)));
            comps.Add(c);
            if (directors.TryGetValue(b.S("id"), out var d))
                c.Update(("battery", d["battery"]), ("rangefinder_m", d["rangefinder_m"]), ("armour_mm", d["armour_mm"]),
                    ("radar", d.F("radar_t") > 0));
            else
                WithMaterial(c, supMaterial);
            if (b.Has("_plate_mm"))
                c["plate_mm"] = b["_plate_mm"];
            if (quarters.B(b.S("id")))
                c["crew"] = quarters[b.S("id")];
            if (smoke is { Count: > 0 })
                c["smoke"] = smoke.ToList();
        }
        var ct = lay.ConningTower;
        if (ct != null)
        {
            var c = PyDict.Of(("id", "Conning tower"), ("kind", "conning_tower"), ("shape", "circle"), ("x", Py.Round(ct.F("x"), 3)),
                ("y", Py.Round(ct.F("y"), 3)), ("r", Py.Round(ct.F("r"), 3)), ("base", 0.0), ("top", Py.Round(ct.F("top"), 2)),
                ("armour_mm", ag["belt_mm"]));
            comps.Add(c);
            WithMaterial(c, Armour.ArmourMaterial(design, "conning_tower"));
        }
        var plan = lay.Geo.Plant;
        foreach (var f in lay.Funnels)
        {
            var pts = R3(Geometry.RrectPolygon(f.F("x") - f.F("l") / 2, f.F("y") - f.F("w") / 2, f.F("x") + f.F("l") / 2,
                f.F("y") + f.F("w") / 2, f.F("w") / 2, f.F("w") / 2));
            comps.Add(PyDict.Of(("id", f["id"]), ("kind", "funnel"), ("shape", "polygon"), ("points", pts), ("base", f.Get("z0", 0.0)),
                ("top", Py.Round(lay.FunTop, 2)), ("boiler_rooms", f.Get("serves", new List<object?>()))));
            if (plan != null && f.Get("serves") is not null)
                comps.Add(PyDict.Of(("id", $"{f.S("id")} uptakes"), ("kind", "uptake"), ("funnel", f["id"]), ("shape", "polygon"),
                    ("points", pts), ("base", Py.Round(plan.F("inner_bottom") + Py.ToDouble(((object?[])plan.D("space")["unit"]!)[2]) - D, 2)),
                    ("top", f.Get("z0", 0.0)), ("boiler_rooms", f.Get("serves", new List<object?>()))));
        }
        foreach (var c in lay.Casings)
        {
            var pts = Geometry.RrectPolygon(c.F("x0"), -c.F("w") / 2, c.F("x1"), c.F("w") / 2, 0.5, 0.5);
            var cc = PyDict.Of(("id", c["id"]), ("kind", "casing"), ("shape", "polygon"), ("points", R3(pts)),
                ("base", Py.Round(c.F("base"), 2)), ("top", Py.Round(c.F("top"), 2)), ("armour_mm", c["armour_mm"]));
            comps.Add(cc);
            if (Py.Truthy(c["armour_mm"]))
                WithMaterial(cc, ag["roof_material"]);
        }
        var dks = lay.Decks.Where(d => !Py.Eq(d["kind"], "deck")).Concat(lay.Sponsons.Select(sp =>
        {
            var s2 = sp.Copy();
            s2["kind"] = "sponson";
            return s2;
        }));
        foreach (var dk in dks)
        {
            var c = PyDict.Of(("id", dk["id"]), ("kind", dk["kind"]), ("shape", "polygon"), ("points", R3(Geometry.Pts(dk["points"]))),
                ("base", Py.Round(dk.F("base"), 2)), ("top", Py.Round(dk.F("top"), 2)));
            comps.Add(c);
            var fdMm = design.DOr("armour").Get("flight_deck_mm", 0L);
            if (Py.Eq(dk["kind"], "flight_deck") && Py.Truthy(fdMm))
            {
                c["armour_mm"] = fdMm;
                WithMaterial(c, Armour.ArmourMaterial(design, "flight_deck"));
            }
            if (inner.Planked.Contains(dk.S("id")))
                c["wood_mm"] = inner.Plating["deck_wood_mm"];
        }
        foreach (var a in lay.Aa)
        {
            var cfg = Geometry.AA_CFG[a.S("type")];
            var (n, w) = Ordnance.ReadyUse(cfg.CalibreMm, cfg.Barrels);
            comps.Add(PyDict.Of(("id", a["id"]), ("kind", "aa"), ("type", a["type"]), ("calibre_mm", cfg.CalibreMm),
                ("calibre_length", cfg.CalibreLength), ("shape", "circle"), ("x", Py.Round(a.F("x"), 3)), ("y", Py.Round(a.F("y"), 3)),
                ("r", cfg.R), ("base", a["base"]), ("top", a.F("base") + 2.0), ("ready_rounds", n), ("ready_t", Py.Round(w, 2))));
        }
        foreach (var c in lay.Compartments)
            if (Py.Eq(c["kind"], "hangar"))
            {
                var pts = Geometry.RrectPolygon(c.F("x0"), -c.F("half_width"), c.F("x1"), c.F("half_width"), 0.0, 0.0);
                comps.Add(PyDict.Of(("id", c["id"]), ("kind", "hangar_bay"), ("shape", "polygon"), ("points", R3(pts)),
                    ("base", Py.Round(c.F("base"), 2)), ("top", Py.Round(c.F("top"), 2))));
            }
        comps.AddRange(PropulsionComponents(inner.Propulsion, D));
        foreach (var x in comps)
            if (inner.BattleCrew.TryGetValue((x.S("kind"), x.S("id")), out var bc) && bc != 0)
                x["battle_crew"] = bc;
        var form = inner.Form;
        var sub = inner.Subdivision;
        var armOut = new PyDict();
        if (ag.F("belt_mm") > 0)
        {
            armOut["belt"] = WithMaterial(PyDict.Of(("thickness_mm", ag["belt_mm"]), ("x0", Py.Round(ag.F("x0"), 3)), ("x1", Py.Round(ag.F("x1"), 3)),
                ("bottom", Rz(ag.F("belt_bottom"))), ("top", Rz(ag.F("belt_top")))), ag["belt_material"]);
            if (!Py.Eq(ag["belt_bottom_mm"], ag["belt_mm"]))
                armOut.D("belt").Update(("bottom_mm", ag["belt_bottom_mm"]), ("taper_from", Rz(Py.Min(ag.F("belt_top"), ag.F("waterline")))));
        }
        var strakes = ag.L("strakes").Cast<PyDict>().ToList();
        if (strakes.Count > 0)
            armOut["strakes"] = strakes.Select(st =>
            {
                var d = PyDict.Of(("id", st["id"]), ("kind", st["kind"]), ("extent", st["extent"]), ("thickness_mm", st["mm"]));
                if (!Py.Eq(st["tip_mm"], st["mm"]))
                    d["tip_mm"] = st["tip_mm"];
                d.Update(("x0", Py.Round(st.F("x0"), 3)), ("x1", Py.Round(st.F("x1"), 3)), ("bottom", Rz(st.F("bottom"))), ("top", Rz(st.F("top"))));
                return (object?)WithMaterial(d, st["material"]);
            }).ToList();
        if (armoured && ag.F("bulkhead_mm") > 0)
            armOut["bulkheads"] = new[] { ("Forward", ag.F("x1")), ("Aft", ag.F("x0")) }.Select(t =>
                (object?)WithMaterial(PyDict.Of(("id", $"{t.Item1} bulkhead"), ("x", Py.Round(t.Item2, 3)),
                    ("thickness_mm", Py.RoundObj(ag["bulkhead_mm"])), ("bottom", Rz(ag.F("bulkhead_bottom"))), ("top", Rz(ag.F("bulkhead_top")))),
                    ag["bulkhead_material"])).ToList();
        var endBhs = ag.L("end_bulkheads").Cast<PyDict>().ToList();
        if (endBhs.Count > 0)
        {
            var l = armOut.SetDefault("bulkheads", new List<object?>()) as List<object?>;
            l!.AddRange(endBhs.Select(b => (object?)WithMaterial(PyDict.Of(("id", b["id"]), ("x", Py.Round(b.F("x"), 3)),
                ("thickness_mm", Py.RoundObj(b["mm"])), ("bottom", Rz(b.F("bottom"))), ("top", Rz(b.F("top")))), b["material"])));
        }
        var adecks = ag.L("decks").Cast<PyDict>().ToList();
        if (adecks.Count > 0)
            armOut["decks"] = adecks.Select(d => (object?)WithMaterial(PyDict.Of(("deck", Decks.DeckName(d.I("deck"))), ("thickness_mm", d["mm"]),
                ("extent", d["extent"]), ("x0", Py.Round(d.F("x0"), 3)), ("x1", Py.Round(d.F("x1"), 3)), ("z", Rz(d.F("z"))),
                ("main", Py.Eq(d["z"], ag["main_z"])), ("roof", Py.Eq(d["z"], ag["roof_z"]))), d["material"])).ToList();
        var vertical = PyDict.Of(("keel", -Py.Round(D, 2)), ("waterline", -Py.Round(D - T, 2)),
            ("armour_deck", ag["main_z"] is not null ? Rz(ag.F("main_z")) : null), ("draught", Py.Round(T, 2)), ("depth", Py.Round(D, 2)),
            ("freeboard", Py.Round(D - T, 2)));
        if (lay.Raised.Count > 0)
            vertical["raised"] = lay.Raised.Select(st => (object?)PyDict.Of(("id", st["id"]), ("x0", Py.Round(st.F("x0"), 3)),
                ("x1", Py.Round(st.F("x1"), 3)), ("top", Py.Round(st.F("levels") * Geometry.DECK_PITCH, 2)))).ToList();
        var out_ = PyDict.Of(("units", "metres"),
            ("frame", "ship-local: origin = ship centre = sprite centre, +x toward bow, +y toward starboard; " +
                      "angles clockwise from dead ahead"),
            ("heights", "base/top/z are metres above the main deck (negative = below it)"),
            ("turret_local", "turret 'local' polygons are in turret space (pivot at 0,0, barrels along +x); " +
                             "rotate by the current turret angle, then add (x, y)"),
            ("length", lay.Hull.L), ("beam", lay.Hull.B), ("vertical", vertical), ("hull", R3(lay.Hull.Points())),
            ("hydrostatics", inner.Hydrostatics),
            ("hull_form", PyDict.Of(("midship_coefficient", Py.Round(form.Cm, 3)), ("waterplane_coefficient", Py.Round(form.Cwp, 3)),
                ("stations", form.Table().Select(s => (object?)PyDict.Of(("x", Py.Round(s.X, 3)),
                    ("z", s.Z.Select(z => (object?)Py.Round(z - D, 2)).ToList()), ("y", s.Y.Select(y => (object?)Py.Round(y, 3)).ToList()))).ToList()))),
            ("armour", armOut), ("plating", inner.Plating), ("components", comps.Cast<object?>().ToList()));
        out_.Update(sub);
        return out_;
    }
}
