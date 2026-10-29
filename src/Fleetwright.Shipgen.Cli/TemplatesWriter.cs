using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fleetwright.Shipgen.Cli;

/// <summary>`shipgen templates`: the shell, explosive, material, fittings, outfit, fire-control, gun and torpedo catalogues
/// (ShellTemplates, Explosives, MaterialTemplates, Fittings, Outfits, FireControlTemplates, GunTemplates, TorpedoTemplates:
/// their one source) written out as the game's template assets, `Content/Templates/shells.json`, `explosives.json`,
/// `materials.json`, `fittings.json`, `guns.json`, `torpedoes.json` (entries {name, group, note, value}, as
/// tools/designer_templates.py writes the others) and `fire-control.json` (its blocks so, and its presets), and as the
/// docs' pages, docs/shipgen/shell-templates.md, explosive-templates.md, material-templates.md, fittings-templates.md,
/// fire-control-templates.md and gun-templates.md. Never edit those by hand.</summary>
static class TemplatesWriter
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Run(string repo)
    {
        var json = new JsonSerializerOptions
        {
            WriteIndented = true, IndentSize = 1, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        JsonNode Value<T>(T v) => JsonNode.Parse(JsonSerializer.Serialize(v, ShipgenJson.Default.Options.GetTypeInfo(typeof(T))))!;
        var shells = new JsonArray(ShellTemplates.All.Select(t => (JsonNode)new JsonObject
        {
            ["name"] = t.Name, ["group"] = t.Group, ["note"] = t.Note, ["value"] = Value(t.Value),
        }).ToArray());
        var explosives = new JsonArray(Explosives.All.Select(e => (JsonNode)new JsonObject
        {
            ["name"] = e.Value.Name, ["group"] = e.Value.From!.Value.ToString(Inv), ["note"] = e.Note, ["value"] = Value(e.Value),
        }).ToArray());
        var materials = new JsonArray(MaterialTemplates.All.Select(m => (JsonNode)new JsonObject
        {
            ["name"] = m.Name, ["group"] = m.Group, ["note"] = m.Note, ["value"] = Value(m.Value),
        }).ToArray());
        Write(Path.Combine(repo, "Content", "Templates", "shells.json"), shells.ToJsonString(json) + "\n");
        Write(Path.Combine(repo, "Content", "Templates", "explosives.json"), explosives.ToJsonString(json) + "\n");
        var fittings = new JsonArray(Fittings.All.Select(f => (JsonNode)new JsonObject
        {
            ["name"] = f.Name, ["group"] = "", ["note"] = f.Note, ["value"] = Value(f.Value),
        }).ToArray());
        Write(Path.Combine(repo, "Content", "Templates", "materials.json"), materials.ToJsonString(json) + "\n");
        Write(Path.Combine(repo, "Content", "Templates", "fittings.json"), fittings.ToJsonString(json) + "\n");
        var outfits = new JsonArray(Outfits.All.Select(o => (JsonNode)new JsonObject
        {
            ["name"] = o.Name, ["group"] = "", ["note"] = o.Note, ["value"] = Value(o.Value),
        }).ToArray());
        Write(Path.Combine(repo, "Content", "Templates", "outfits.json"), outfits.ToJsonString(json) + "\n");
        Write(Path.Combine(repo, "docs", "shipgen", "outfit-templates.md"), OutfitPage());
        var drainage = new JsonArray(DrainageTemplates.All.Select(o => (JsonNode)new JsonObject
        {
            ["name"] = o.Name, ["group"] = "", ["note"] = o.Note, ["value"] = Value(o.Value),
        }).ToArray());
        Write(Path.Combine(repo, "Content", "Templates", "drainage.json"), drainage.ToJsonString(json) + "\n");
        Write(Path.Combine(repo, "docs", "shipgen", "drainage-templates.md"), DrainagePage());
        var searchlights = new JsonArray(SearchlightTemplates.All.Select(o => (JsonNode)new JsonObject
        {
            ["name"] = o.Name, ["group"] = "", ["note"] = o.Note, ["value"] = Value(o.Value),
        }).ToArray());
        Write(Path.Combine(repo, "Content", "Templates", "searchlights.json"), searchlights.ToJsonString(json) + "\n");
        Write(Path.Combine(repo, "docs", "shipgen", "searchlight-templates.md"), SearchlightPage());
        Write(Path.Combine(repo, "docs", "shipgen", "shell-templates.md"), ShellPage());
        Write(Path.Combine(repo, "docs", "shipgen", "explosive-templates.md"), ExplosivePage());
        Write(Path.Combine(repo, "docs", "shipgen", "material-templates.md"), MaterialPage());
        Write(Path.Combine(repo, "docs", "shipgen", "fittings-templates.md"), FittingsPage());
        var fc = new JsonObject
        {
            ["blocks"] = new JsonArray(FireControlTemplates.All.Select(t => (JsonNode)new JsonObject
            {
                ["name"] = t.Name, ["group"] = t.Kind, ["note"] = t.Note, ["value"] = Value(t.Value),
            }).ToArray()),
            ["presets"] = new JsonArray(FireControlTemplates.Presets.Select(p => (JsonNode)new JsonObject
            {
                ["name"] = p.Name, ["note"] = p.Note, ["fit"] = new JsonArray(p.Fit.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
            }).ToArray()),
        };
        Write(Path.Combine(repo, "Content", "Templates", "fire-control.json"), fc.ToJsonString(json) + "\n");
        Write(Path.Combine(repo, "docs", "shipgen", "fire-control-templates.md"), FireControlPage());
        var guns = new JsonArray(GunTemplates.All.Select(g => (JsonNode)new JsonObject
        {
            ["name"] = g.Name, ["group"] = g.Group, ["note"] = g.Note, ["value"] = Value(g.Value),
        }).ToArray());
        Write(Path.Combine(repo, "Content", "Templates", "guns.json"), guns.ToJsonString(json) + "\n");
        var torpedoes = new JsonArray(TorpedoTemplates.All.Select(t => (JsonNode)new JsonObject
        {
            ["name"] = t.Name, ["group"] = "", ["note"] = t.Note, ["value"] = Value(t.Value),
        }).ToArray());
        Write(Path.Combine(repo, "Content", "Templates", "torpedoes.json"), torpedoes.ToJsonString(json) + "\n");
        Write(Path.Combine(repo, "docs", "shipgen", "gun-templates.md"), GunPage());
        return 0;
    }

    static string GunPage()
    {
        var sb = new StringBuilder();
        sb.Append("""
            # Gun and torpedo templates

            Written by `shipgen templates` from `src/Fleetwright.Shipgen/GunTemplates.cs`; don't edit by hand. The designer's
            templates are `Content/Templates/guns.json` and `torpedoes.json`, from the same tables.

            A battery's `gun` names its gun block, and `torpedoes.torpedo` its torpedo block: the design's own (`gun_tech`,
            `torpedo_tech`), else the catalogue's below; a battery that names none fires with the Second War's double-base
            block, and torpedoes that name none are the Second War's. The blocks are calibre-free, shares of the sim's
            placeholder rules (`DesignerPlaceholders.Gun`, `Charge`, `Pattern`, `Torpedo`: the sources are there), so one
            block serves a period's family of guns. The look never sets them: it's the drawing's only. Designs from before
            format 9 got the blocks their look's era and navy stood for, so they fight as before.

            ```json
            "secondary": [{"calibre_mm": 152, "calibre_length": 50, "per_side": 6, "mount": "casemate", "gun": "Second War, double-base"}],
            "torpedoes": {"tubes": 4, "torpedo": "Great War torpedo"}
            ```

            | Gun block | Reload | Slower at 300 mm (≥40 / <40 / <30 cal) | Training | DP | Elevation | DP | Pattern % (305 mm, power, light) | Propellant | Packed |
            |---|---|---|---|---|---|---|---|---|---|

            """.Replace("\r\n", "\n"));
        foreach (var g in GunTemplates.All)
        {
            var v = g.Value;
            string propellant = v.BlackPowderUnderCal > 0 ? $"{v.Propellant}, black powder under {v.BlackPowderUnderCal:0} cal" : v.Propellant!;
            sb.Append(Inv, $"| {g.Name} | ×{v.ReloadShare:0.0#} | ×{v.HeavySlow:0.0#} / ×{v.HeavySlowUnder40Cal:0.0#} / ×{v.HeavySlowUnder30Cal:0.0#} | " +
                           $"×{v.TrainShare:0.0#} | ×{v.DpTrainShare:0.0#} | {v.MaxElevationDeg:0}° | {v.DpMaxElevationDeg:0}° | " +
                           $"{v.PatternPct:0.0#}, {v.PatternExp:0}, {v.PatternLightPct:0.0#} | {propellant} | {v.Packaging} |\n");
        }
        sb.Append("\nShares of the rules' reload, 7·(d/102 mm)^1.5 s (at most 36), and training, 2·(381 mm/d)^1.5 °/s.\n\n");
        foreach (var g in GunTemplates.All)
            sb.Append("- **").Append(g.Name).Append(":** ").Append(g.Note).Append('\n');
        sb.Append("""

            | Torpedo block | Fast (21") | Slow (21") | Warhead TNT share | Mount training | Reload (mount / tube) | Gyro | Fail | Dud |
            |---|---|---|---|---|---|---|---|---|

            """.Replace("\r\n", "\n"));
        foreach (var t in TorpedoTemplates.All)
        {
            var v = t.Value;
            sb.Append(Inv, $"| {t.Name} | {v.FastKn:0} kn, {v.FastRangeM:#,0} m | {v.SlowKn:0} kn, {v.SlowRangeM:#,0} m | {v.ChargeShare:0.0#} | " +
                           $"{v.MountTrainDegS:0} °/s | {v.MountReloadS:0} / {v.TubeReloadS:0} s | {v.GyroDeg:0}° | {v.Fail:0.0#} | {v.Dud:0.0#} |\n");
        }
        sb.Append('\n');
        foreach (var t in TorpedoTemplates.All)
            sb.Append("- **").Append(t.Name).Append(":** ").Append(t.Note).Append('\n');
        return sb.ToString();
    }

    static string FireControlPage()
    {
        var sb = new StringBuilder();
        sb.Append("""
            # Fire-control templates

            Written by `shipgen templates` from `src/Fleetwright.Shipgen/FireControlTemplates.cs`; don't edit by hand. The
            designer's templates are `Content/Templates/fire-control.json`, from the same table.

            A battery's fire control is a fit of tech blocks (`docs/battle/fire-control.md` F3): one optics block (what its
            rangefinders are and how they read), one computer (how it predicts) and one mount block (how its guns are
            laid). The fit names them, the design copies them into `fire_control_tech`, and a name the design doesn't carry
            takes the catalogue's:

            ```json
            "fire_control": {"main": {"directors": 1, "rangefinder_m": 4.6, "fit": ["Barr & Stroud FQ/FT (O2)",
              "Dreyer table Mk III-V", "Director, follow the pointer"]}}
            ```

            A block is a machine or a make, never a year (the tech tree decides who has it); the combat model sees only its
            numbers. The presets are named fits standing for the reference's levels (`research/simulation/fire_control_ref.py`
            LEVELS 0-8, then its rows beside them): a level is a preset, not a type. Blocks named as a radar's stand-in carry
            a radar's numbers until the radar pass. Sources: `research/simulation/fire-control-designer.md` §2 (the tech
            values, tiers O1-O4, C0-C6, M0-M4), `fire-control-research.md` §3.3-3.5 and §5.3, and
            `fire-control-research/computing.md` §3; [EST] marks numbers no source gives.

            Read by the sim so far: the optics' magnification and readings a minute; every computer number above the mass;
            the mounts' director firing, laying error and deflection wander. Shipgen reads the computer's mass and crew: its
            transmitting station's weight and men (F4c). The rest (acuity, rigid base and flex, the top magnification,
            coatings, the gyro mounting, the optics' and mounts' masses and crews, power, the stable vertical) is carried for
            the steps that read it (F5: the sensors proper; ship motion; power with the power networks).

            ## Presets

            | Preset | Optics | Computer | Mount | |
            |---|---|---|---|---|

            """.Replace("\r\n", "\n"));
        foreach (var p in FireControlTemplates.Presets)
            sb.Append(Inv, $"| {p.Name} | {p.Optics} | {p.Computer} | {p.Mount} | {p.Note} |\n");
        sb.Append("""

            ## Optics

            Kind; acuity ″; rigid base m and flex exponent; magnification (top at a full image); readings a minute; coated,
            gyro-stabilised; mass kg per m² of base.

            | Block | Kind | δ ″ | Rigid m / flex | × (max) | A minute | Coated, gyro | kg/m² |
            |---|---|---|---|---|---|---|---|

            """.Replace("\r\n", "\n"));
        foreach (var t in FireControlTemplates.All.Where(t => t.Kind == "optics"))
        {
            var o = t.Value.Optics!;
            sb.Append(Inv, $"| {t.Name} | {o.Kind} | {N(o.DeltaArcsec)} | {N(o.RigidBaseM)} / {N(o.FlexExp)} | {N(o.Magnification)} ({N(o.MMax)}) | " +
                $"{N(o.ReadingsPerMin)} | {(o.Coated == true ? "coated" : "-")}, {(o.GyroStabilised == true ? "gyro" : "-")} | {N(o.MassKgM2)} |\n");
        }
        sb.Append("""

            ## Computers

            Tracker, helm-free; latency s; q m²/s³; the clock's limits (m/s, °/s); the eye's first guess (kn, °); the residual
            bias (% of range, mil); own turn's kick m; first ladder step m; salvo-to-salvo error % of range; and the filters'
            own numbers. Mass t, crew, power kW.

            | Block | Tracker | Latency | q | Limits | Prior | Bias | Kick | Step | Salvo | Filter | t, crew, kW |
            |---|---|---|---|---|---|---|---|---|---|---|---|

            """.Replace("\r\n", "\n"));
        foreach (var t in FireControlTemplates.All.Where(t => t.Kind == "computer"))
        {
            var c = t.Value.Computer!;
            string limits = c.MaxRangeRateMs > 0 || c.MaxBearingRateDegS > 0 ? $"{N(c.MaxRangeRateMs)}, {N(c.MaxBearingRateDegS)}" : "-";
            string filter = c.Tracker switch
            {
                "acceleration" => $"τ {N(c.AccelTauS)} s, σ {N(c.AccelSigmaMs2)} m/s²",
                "turn_rate" => $"turn {N(c.TurnQ)}, speed {N(c.SpeedQ)}",
                "judgement" => $"looks again {N(c.RejudgeS)} s",
                _ => "-",
            } + (c.Detect == true ? "; detects turns" : "");
            sb.Append(Inv, $"| {t.Name} | {c.Tracker}{(c.HelmFree == true ? ", helm-free" : "")} | {N(c.LatencyS)} | {N(c.Q)} | {limits} | " +
                $"{N(c.PriorKn)}, {N(c.PriorDeg)} | {N(c.ResidPct)}, {N(c.DeflBiasMil)} | {N(c.OwnTurnKickM)} | {N(c.FirstStepM)} | {N(c.SalvoPct)} | " +
                $"{filter} | {N(c.MassT)}, {N(c.Crew)}, {N(c.PowerKw)} |\n");
        }
        sb.Append("""

            ## Mounts

            | Block | Director firing | Stable vertical | Laying mil | Deflection wander mil |
            |---|---|---|---|---|

            """.Replace("\r\n", "\n"));
        foreach (var t in FireControlTemplates.All.Where(t => t.Kind == "mount"))
        {
            var m = t.Value.Mount!;
            sb.Append(Inv, $"| {t.Name} | {(m.DirectorFiring == true ? "yes" : "no")} | {(m.StableVertical == true ? "yes" : "no")} | {N(m.LayMil)} | {N(m.SalvoDeflMil)} |\n");
        }
        sb.Append('\n');
        foreach (var t in FireControlTemplates.All)
            sb.Append("- **").Append(t.Name).Append(":** ").Append(t.Note).Append('\n');
        return sb.ToString();
    }

    static string FittingsPage()
    {
        var sb = new StringBuilder();
        sb.Append("""
            # Fittings templates

            Written by `shipgen templates` from `src/Fleetwright.Shipgen/Fittings.cs`; don't edit by hand. The designer's
            templates are `Content/Templates/fittings.json`, from the same table.

            A design's `fittings` block says how its gear is mounted. Its one number so far is `shock_grade`, G0 to G3
            (`docs/battle/damage/17_service_network_faults.md` §2.2; decision 10 of docs/battle/shells-armour-damage.md):
            how hard a shock trips its breakers, lamps and generators, from the hull's shock factor at the device. The
            armour shock of a hit that didn't get through (E4), the near misses (E7) and later the service networks read it.
            A device the ship doesn't carry can't fail, so an early ship with few breakers trips little whatever its grade.
            A design that gives none is G0. The notes say who fitted to each grade; the design carries the grade, not the
            year (old designs got theirs from their navy and their plant's year when the format moved to 4).

            ```json
            "fittings": {"shock_grade": "G1"}
            ```

            | Template | Grade |
            |---|---|

            """.Replace("\r\n", "\n"));
        foreach (var f in Fittings.All)
            sb.Append(Inv, $"| {f.Name} | {f.Grade} |\n");
        sb.Append('\n');
        foreach (var f in Fittings.All)
            sb.Append("- **").Append(f.Name).Append(":** ").Append(f.Note).Append('\n');
        return sb.ToString();
    }

    static string OutfitPage()
    {
        var sb = new StringBuilder();
        sb.Append("""
            # Outfit templates

            Written by `shipgen templates` from `src/Fleetwright.Shipgen/Outfits.cs`; don't edit by hand. The designer's
            templates are `Content/Templates/outfits.json`, from the same table.

            A design's `outfit` block says what burns aboard (`docs/battle/fire.md`; `docs/battle/damage/17_fire.md` §2):
            each kind of space's fire load, MJ per m² of deck, as fitted and as stripped for war (the ship's orders before
            action pick one), the paint's load on every space but the weather deck, and the growth factor over a furnished
            room's "medium" t² fire (1 MW at 300 s). The sim reads the numbers: a space flashes over only on a load that can
            feed it (about 230 MJ/m² and up), burns as long as its load lasts at the heat its openings let through, and a
            wooden weather deck burns at the deck load, a steel one at half. A key not given takes its navy's template in
            its plant's year (old designs got the whole block so when the format moved to 8).

            ```json
            "outfit": {"name": "Great War (1914)", "fitted": {"quarters": 420, "cabins": 750, "offices": 420, "stores": 1500,
              "machinery": 150, "deck": 300}, "stripped": {"quarters": 400, "cabins": 450, "offices": 420, "stores": 1500,
              "machinery": 150, "deck": 150}, "paint_mj_m2": 50, "growth": 1}
            ```

            | Template | Quarters | Cabins | Offices | Stores | Machinery | Deck | Paint | Growth |
            |---|---|---|---|---|---|---|---|---|

            """.Replace("\r\n", "\n"));
        foreach (var o in Outfits.All)
        {
            var v = o.Value;
            string L(double? fitted, double? stripped) => fitted == stripped ? $"{fitted:0}" : $"{fitted:0} / {stripped:0}";
            sb.Append(Inv, $"| {o.Name} | {L(v.Fitted!.Quarters, v.Stripped!.Quarters)} | {L(v.Fitted.Cabins, v.Stripped.Cabins)} | " +
                           $"{L(v.Fitted.Offices, v.Stripped.Offices)} | {L(v.Fitted.Stores, v.Stripped.Stores)} | {L(v.Fitted.Machinery, v.Stripped.Machinery)} | " +
                           $"{L(v.Fitted.Deck, v.Stripped.Deck)} | {v.PaintMjM2:0} | {v.Growth:0.0#} |\n");
        }
        sb.Append("\nAs fitted / stripped where they differ, MJ/m².\n\n");
        foreach (var o in Outfits.All)
            sb.Append("- **").Append(o.Name).Append(":** ").Append(o.Note).Append('\n');
        return sb.ToString();
    }

    static string DrainagePage()
    {
        var sb = new StringBuilder();
        sb.Append("""
            # Drainage templates

            Written by `shipgen templates` from `src/Fleetwright.Shipgen/DrainageTemplates.cs`; don't edit by hand. The
            designer's templates are `Content/Templates/drainage.json`, from the same table.

            A design's `drainage` block says what's built in against water (`docs/battle/damage-control.md` DC4;
            `docs/battle/09_damage_control.md` §5): the fixed drainage pumps (t/h for each 1,000 t of displacement, on the
            ship's power), the portable pumps her repair parties rig (each one's t/h, no power needed), how fast her
            counterflooding valves fill the tanks on the high side (t/min for each 10,000 t; 0: none), and the tightness of
            her intact boundaries (× the base leak: 1 air-tested, more where glands and penetrations were never tested). A
            key not given takes its navy's template in its plant's year (old designs got the whole block so when the format
            moved to 11). The numbers are estimates by period [EST]; no weight is booked for them yet.

            ```json
            "drainage": {"name": "RN Great War (1914)", "pumps_tph_per_1000t": 50, "portable_pumps": 4, "portable_tph": 15,
              "counterflood_tpm_per_10000t": 10, "tightness": 1.5}
            ```

            | Template | Pumps, t/h per 1,000 t | Portable pumps | Each, t/h | Counterflooding, t/min per 10,000 t | Tightness |
            |---|---|---|---|---|---|

            """.Replace("\r\n", "\n"));
        foreach (var o in DrainageTemplates.All)
        {
            var v = o.Value;
            sb.Append(Inv, $"| {o.Name} | {v.PumpsTphPer1000T:0} | {v.PortablePumps} | {v.PortableTph:0} | {v.CounterfloodTpmPer10000T:0} | {v.Tightness:0.#} |\n");
        }
        sb.Append('\n');
        foreach (var o in DrainageTemplates.All)
            sb.Append("- **").Append(o.Name).Append(":** ").Append(o.Note).Append('\n');
        return sb.ToString();
    }

    static string SearchlightPage()
    {
        var sb = new StringBuilder();
        sb.Append("""
            # Searchlight templates

            Written by `shipgen templates` from `src/Fleetwright.Shipgen/SearchlightTemplates.cs`; don't edit by hand. The
            designer's templates are `Content/Templates/searchlights.json`, from the same table.

            A design's `searchlights` block says what lights she carries (`docs/battle/night.md` Q3), and shipgen sizes and
            sites them itself: a capital ship's count (180 × 27 m of waterplane; a smaller ship in proportion to her waterplane,
            a bigger one by its 0.25 power, at least one) unless `count` gives hers, and her mirror between a torpedo craft's (600
            m² and under) and a capital ship's by the logarithm of her waterplane. They stand on the bridge's wings, on
            platforms on the funnels' sides, on after control, on the masts' tops and the other roofs, the groups taking turns
            so they spread fore and aft, pairs abreast; the deck only where nothing else has room. The arc's crater (cd/cm²)
            and the mirror give the beam's peak (crater × mirror's face × 0.9 clear × 0.85 silvered glass), the spread its
            width; control is local (each light by its crew), remote (from a searchlight control position) or director.
            A light's weight (1.2 t a 36-inch, by its mirror's area), draw (6.5 kW a 36-inch plain arc) and crew (three at a
            36-inch) are booked from its mirror. A key not given takes its template in the plant's year (old designs got the
            whole block by their navy so when the format moved to 14). The numbers are estimates by period [EST] until the
            research request "Night fighting, 1890-1925" comes back.

            ```json
            "searchlights": {"name": "Great War (1914)", "capital_count": 8, "mirror_cm": 91, "small_mirror_cm": 61,
              "lamp_cd_cm2": 16000, "spread_deg": 2, "control": "local"}
            ```

            | Template | Capital ship | Mirror, cm | Small, cm | Crater, cd/cm² | Beam (capital), cd | Spread | Control |
            |---|---|---|---|---|---|---|---|

            """.Replace("\r\n", "\n"));
        foreach (var o in SearchlightTemplates.All)
        {
            var v = o.Value;
            double cd = SearchlightTemplates.BeamCd(v.MirrorCm!.Value / 100, v.LampCdCm2!.Value);
            sb.Append(Inv, $"| {o.Name} | {v.CapitalCount} | {v.MirrorCm:0} | {v.SmallMirrorCm:0} | {v.LampCdCm2:#,0} | {cd:0.0e0} | {v.SpreadDeg:0.0#}° | {v.Control} |\n");
        }
        sb.Append('\n');
        foreach (var o in SearchlightTemplates.All)
            sb.Append("- **").Append(o.Name).Append(":** ").Append(o.Note).Append('\n');
        return sb.ToString();
    }

    static string MaterialPage()
    {
        var sb = new StringBuilder();
        sb.Append("""
            # Material templates

            Written by `shipgen templates` from `src/Fleetwright.Shipgen/MaterialTemplates.cs`; don't edit by hand. The
            designer's pickers and the game's catalogue are `Content/Templates/materials.json`, from the same table.

            A part names its material (`armour.materials`, a deck's or battery's `material`, `hull.plating.material`,
            `superstructure.material`); the name picks the design's block in `materials`, else the catalogue's, else
            steel's (with a warning). A design copies the blocks its parts name (`shipgen upgrade --materials` refreshes
            them from here). `q` is the plate's resistance as a share of the same thickness of the penetration formula's
            plate (Thompson's scale, WWII STS 1) against an 8" capped shell: `research/simulation/armor/00-overview.md`
            §4.2's reconciled table (P1b of docs/battle/shells-armour-damage.md; the research's id ends each note).
            Face-hardened plate is × (D/8")^−k past 8" (held below), with no face-hardened advantage (×0.9) under max(the
            thinnest made, 0.3 calibres); its face shatters a bare-nosed shell (P2) unless the column says it doesn't
            (compound's steel face breaks only chilled shot, by that shot's own limits). Homogeneous plate under 25%
            elongation loses to shells over 8" by Okun's M79 penalty, 1 − (1 − √(El/25))·min(D − 8", 8")/8". Both rules
            are the sim's (`MaterialRow.Against`, `Terminal.FaceHardMm`). The density
            is armor/03 §7's: every plate is weighed with its own (the hull's structure with its plating's). The
            structure group is the plating's and superstructure's picker; the armour picker offers every material.
            The back (P4; damage/14 §2.1 [EST]): a stopped shell throws spall from u = V/V_L of spall_u, and from
            holed_u it holes the plate, breaking in it (face-hardened 0.85/0.94, homogeneous 0.90/1.00, brittle
            0.75/0.95); big is the share of its spall that leaves as one piece (E3: 0.5, brittle 0.3). Fragments is
            how a burst's fragments see it (E1: THOR's class).

            | Material | Group | q | q at 16" | k | Face (share, from mm) | Elongation % | Back (spall, holed, big) | Density t/m³ |
            |---|---|---|---|---|---|---|---|---|

            """.Replace("\r\n", "\n"));
        foreach (var m in MaterialTemplates.All)
        {
            var v = m.Value;
            double q16 = Fleetwright.Sim.Ships.MaterialRow.Of(v).Against(1000, 16 * 25.4);
            string face = v.FaceHardened is { } f ? $"{N(f.Share, "0.00")}, {N(f.MinMm, "0")}{(f.Shatters == false ? "; shatters no steel shell" : "")}" : "-";
            sb.Append(CultureInfo.InvariantCulture,
                $"| {m.Name} | {m.Group} | {N(v.Q, "0.00")} | {N(q16, "0.00")} | {N(v.QScale, "0.00")} | {face} | {N(v.ElongationPct, "0")} | {N(v.Back?.SpallU, "0.00")}, {N(v.Back?.HoledU, "0.00")}, {N(v.Back?.Big, "0.0")} | {N(v.DensityTPerM3, "0.00")} |\n");
        }
        sb.Append('\n');
        foreach (var m in MaterialTemplates.All)
            sb.Append("- **").Append(m.Name).Append(":** ").Append(m.Note).Append('\n');
        return sb.ToString();
    }

    static void Write(string path, string text)
    {
        File.WriteAllText(path, text);
        Console.WriteLine($"wrote {path}");
    }

    static string N(double? v, string f = "0.###") => v is { } x ? x.ToString(f, Inv) : "-";

    static string ShellPage()
    {
        var sb = new StringBuilder();
        sb.Append("""
            # Shell templates

            Written by `shipgen templates` from `src/Fleetwright.Shipgen/ShellTemplates.cs`; don't edit by hand. The game's
            loadout catalogue and the designer's templates are `Content/Templates/shells.json`, from the same table.

            A template is an `ammunition` block (docs/shipgen/README.md): calibre-free, so a navy's family of guns shares it.
            Mass is k·D³ (D in m); k is shown here also as W/D³ in lb/in³, the literature's unit (1 lb/in³ = 27,680 kg/m³).
            Shares are of the shell's mass, thresholds in calibres. A design copies the blocks it uses; a battery that gives
            no shells carries the default loadout of the **1912 standard** family (main 70% AP / 30% HE, secondary 30% / 70%).
            Light guns (under 200 mm) of a family take its light blocks where it has them. Notes cite the research
            (`research/simulation/armor/04-shells-fuzes.md` §1.7 and §3.5, `armor/00` §5.4, `damage/13` §2.5 and §3.2); [EST]
            marks numbers no source gives.

            Columns: kind; W/D³; nose crh and drag form factor; cap (share); body break-up (obliquity °, T/D) and quality;
            filler (share, explosive, booster); fuze (position, delay s, start calibres at normal and their share from 61°,
            plates, delay scatter, blinds); steel (fragment size, speed). What the explosive and booster do (bursting in the
            plate, full detonation) is the explosive's (`explosive-templates.md`).

            """.Replace("\r\n", "\n"));
        foreach (var family in ShellTemplates.All.GroupBy(t => t.Group))
        {
            sb.Append("\n## ").Append(family.Key).Append("\n\n");
            sb.Append("| Template | Kind | W/D³ | crh / i | Cap | Body | Filler | Fuze | Steel |\n");
            sb.Append("|---|---|---|---|---|---|---|---|---|\n");
            foreach (var t in family)
            {
                var a = t.Value;
                string cap = a.Cap?.Kind is null or "none" ? "none" : $"{a.Cap.Kind} {N(a.Cap.Share * 100, "0.#")}%";
                var z = a.Fuze!;
                string fuze = z.Position == "nose" ? "nose" : $"base {N(z.DelayS)} s, {N(z.StartCal)} cal (×{N(z.Oblique)}), {z.Plates}, ±{N(z.Scatter * 100, "0")}%, {N(z.Blind * 100, "0")}% blind";
                sb.Append(CultureInfo.InvariantCulture, $"| {t.Name}{(t.Light ? " (light)" : "")} | {a.Kind} | {N(a.MassKKgM3 / 27_679.9, "0.00")} | {N(a.NoseCrh, "0.#")} / {N(a.FormFactor, "0.00")} | {cap} | " +
                    $"{N(a.Body?.BreakObDeg, "0")}°, {N(a.Body?.BreakTD, "0.##")}; {N(a.Body?.Quality, "0.00")} | {N(a.Filler!.Share * 100, "0.##")}% {a.Filler.Explosive?.Name}, {a.Filler.Booster} | " +
                    $"{fuze} | {N(a.Steel?.Size, "0.##")}/{N(a.Steel?.Speed, "0.##")} |\n");
            }
            sb.Append('\n');
            foreach (var t in family)
                sb.Append("- **").Append(t.Name).Append(":** ").Append(t.Note).Append('\n');
        }
        return sb.ToString();
    }

    static string ExplosivePage()
    {
        var sb = new StringBuilder();
        sb.Append("""
            # Explosive templates

            Written by `shipgen templates` from `src/Fleetwright.Shipgen/Explosives.cs`; don't edit by hand. The game's catalogue
            is `Content/Templates/explosives.json`, from the same table.

            A shell's `filler.explosive` names one; a design copies the ones it names into its `explosives`, and a filler can
            override any number inline. Sources: `docs/battle/damage/12_explosive_fillers.md` §4.1 (re, in-plate, full
            detonation, fire), `11_blast_confinement.md` §2.4 (heats, air, eta), `13_fragmentation.md` §2.1 and §3.1 (Gurney,
            brisance). The sim reads `re` (the blast: W = filler kg × re) from S1 on; the rest ride along for P3 and phase E.

            | Explosive | From | re | Q det / comb MJ/kg | Air m³/kg | eta | Gurney m/s | Brisance | In plate | Full basic / CE | Fire |
            |---|---|---|---|---|---|---|---|---|---|---|

            """.Replace("\r\n", "\n"));
        foreach (var e in Explosives.All)
        {
            var x = e.Value;
            sb.Append(CultureInfo.InvariantCulture, $"| {x.Name} | {x.From} | {N(x.Re, "0.00")} | {N(x.QDet, "0.0")} / {N(x.QComb, "0.0")} | {N(x.AirM3Kg, "0.00")} | " +
                $"{N(x.Eta, "0.00")} | {N(x.GurneyMs, "0")} | {N(x.Brisance, "0.00")} | {N(x.InPlate, "0.00")} | {N(x.Full?.Basic, "0.00")} / {N(x.Full?.Ce, "0.00")} | {N(x.Fire, "0.00")} |\n");
        }
        sb.Append('\n');
        foreach (var e in Explosives.All)
            sb.Append("- **").Append(e.Value.Name).Append(":** ").Append(e.Note).Append('\n');
        return sb.ToString();
    }
}
