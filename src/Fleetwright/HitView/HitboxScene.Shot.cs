using System.Globalization;
using System.Numerics;
using Fleetwright.Shipgen;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using ImGuiNET;
using ShipP3 = Fleetwright.Sim.Ships.P3;

namespace Fleetwright.HitView;

/// <summary>What a click in the hitbox viewer does.</summary>
enum ClickMode
{
    Select,
    Ray,
    Shell,
}

/// <summary>
/// The hitbox viewer's shot: a click fires a shell at what's under the cursor, its line of flight through that point
/// from the range and aspect asked (the aspect the view's bearing, unless fixed), through the world's own damage system
/// on a ship of the design (<see cref="ShotRange"/>). The panel picks the shooter (this ship or another design), its
/// battery and shell, the range, the seed and whether the ship keeps her damage; the shot window lists the terminal walk
/// plate by plate and what the hit did, and plays it back over the view: the shell coming in, slowing at each plate
/// (each flashing with what it made of it), turning and glancing along its bent path, its fuze starting, the burst at
/// its wreck radius, and lines from it to the parts it made worse.
/// </summary>
public sealed unsafe partial class HitboxScene
{
    /// <summary>A dev hook (-hitshot=X,Y,Z,RANGE_KM,ASPECT[,BATTERY[,KIND]][,at=S]): a shell fired at start through the
    /// ship-frame point, from the range and aspect (degrees off the bow it comes from); the battery by its index among
    /// those with shells, the shell by kind (ap, common, he); with at=S the playback held at S seconds.</summary>
    public static string? DevShot { get; set; }

    bool devShotDone;
    SimSettings? simSettings;
    ShotRange? shotRange;
    Shot? shot;
    Playback? playback;
    readonly HitPath eye = new();
    string shotStatus = "";
    int shooter = -1, battery, load, seed = 1;
    float rangeKm = 12, aspectDeg = 90;
    bool aspectFromView = true, keepDamage;
    readonly Dictionary<int, ShipClass?> shooters = [];
    ShipP3 aim;
    bool hasAim;
    float play, playSpeed = 1;
    bool playing, loop;

    void ResetShot()
    {
        (shot, playback, hasAim, shotStatus) = (null, null, false, "");
        shotRange?.Reset();
    }

    // ------------------------------------------------------------------ firing

    /// <summary>A ray from the eye through the view at (x, y), traced through the ship into a path (turrets at rest, as
    /// drawn). False with no ship.</summary>
    bool TraceEye(float x, float y, uint width, uint height, HitPath into)
    {
        if (renderer.Mesh is not { } m || session.Ship is not { } ship)
            return false;
        cls ??= ShipClassCompiler.Compile(ship);
        var ndc = new Vector2(2 * x / Math.Max(1, width) - 1, 1 - 2 * y / Math.Max(1, height));
        float reach = (m.Max - m.Min).Length() + 10;
        var (o, d) = cam.Ray(ndc, (float)width / Math.Max(1, height), reach);
        var end = o + d * (cam.Perspective ? cam.EyeDistance + reach : 8 * reach);
        ShipTracer.Trace(cls, new ShipP3(o.X, o.Y, o.Z), new ShipP3(end.X, end.Y, end.Z), [], Waterplane.Level(cls), into);
        return true;
    }

    /// <summary>A shell at what's under the cursor: the first thing of the ship the eye's ray meets.</summary>
    void Shoot(float x, float y, uint width, uint height)
    {
        if (!TraceEye(x, y, width, height, eye))
            return;
        foreach (ref readonly var c in eye.Crossings)
            if (c.Kind is CrossingKind.HullIn or CrossingKind.PartIn)
            {
                (aim, hasAim) = (c.Point, true);
                if (aspectFromView)
                    aspectDeg = (cam.Bearing % 360 + 360) % 360;
                FireShot();
                return;
            }
        shotStatus = "click on the ship";
    }

    /// <summary>The shooter's class: this ship's, or another design's (compiled the first time it's picked).</summary>
    ShipClass? ShooterClass()
    {
        if (shooter < 0)
            return cls ??= session.Ship is { } ship ? ShipClassCompiler.Compile(ship) : null;
        if (!shooters.TryGetValue(shooter, out var sc))
        {
            try
            {
                sc = ShipClassCompiler.Compile(ShipDesign.Build(Design.Load(session.PathOf(shooter))));
            }
            catch (Exception e)
            {
                shotStatus = $"{session.Names[shooter]}: {e.Message}";
            }
            shooters[shooter] = sc;
        }
        return sc;
    }

    /// <summary>The shooter's batteries that carry shells.</summary>
    static List<int> Batteries(ShipClass sc)
    {
        var list = new List<int>();
        for (int b = 0; b < sc.Batteries.Length; b++)
            if (sc.Loadout.Loads(b).Length > 0)
                list.Add(b);
        return list;
    }

    /// <summary>The shot again at the aim, with the panel's settings.</summary>
    void FireShot()
    {
        if (!hasAim || session.Ship is not { } ship)
            return;
        cls ??= ShipClassCompiler.Compile(ship);
        if (ShooterClass() is not { } sc)
            return;
        var bats = Batteries(sc);
        if (bats.Count == 0)
        {
            shotStatus = "the shooter carries no shells";
            return;
        }
        battery = Math.Clamp(battery, 0, bats.Count - 1);
        int b = bats[battery];
        var loads = sc.Loadout.Loads(b);
        load = Math.Clamp(load, 0, loads.Length - 1);
        int type = loads[load].ShellType;
        double maxElev = sc.Mounts[sc.Links(sc.Batteries[b].Mounts)[0]].MaxElevationDeg;
        simSettings ??= SimSettings.Load(Path.Combine(AppContext.BaseDirectory, "Content", "Sim", "settings.json"));
        shotRange ??= new ShotRange(simSettings);
        try
        {
            shot = shotRange.Fire(cls, sc.Loadout.ShellTypes[type], sc.Loadout.ShellTypeId(type), maxElev, rangeKm * 1000, aspectDeg, aim, (ulong)seed,
                keepDamage);
        }
        catch (ArgumentException e)
        {
            (shot, playback, shotStatus) = (null, null, e.Message);
            return;
        }
        shotStatus = shot.Struck ? "" : shot.Note;
        playback = shot.Struck ? Playback.Of(shot, simSettings) : null;
        (play, playing) = (0, playback != null);
    }

    /// <summary>The playback's clock, and the dev hook's shot once the ship is built.</summary>
    void AdvanceShot(float dt)
    {
        if (DevShot != null && !devShotDone && renderer.Mesh != null && session.Ship != null)
        {
            devShotDone = true;
            DevFire(DevShot);
        }
        if (!playing || playback == null)
            return;
        play += dt * playSpeed;
        if (play < playback.TotalS)
            return;
        if (loop)
            play = 0;
        else
            (play, playing) = ((float)playback.TotalS, false);
    }

    void DevFire(string spec)
    {
        var inv = CultureInfo.InvariantCulture;
        var parts = spec.Split(',');
        double D(int i) => double.Parse(parts[i], inv);
        if (parts.Length < 5)
        {
            shotStatus = "-hitshot=X,Y,Z,RANGE_KM,ASPECT[,BATTERY[,KIND]][,at=S]";
            return;
        }
        (aim, hasAim, rangeKm, aspectDeg, aspectFromView) = (new ShipP3(D(0), D(1), D(2)), true, (float)D(3), (float)D(4), false);
        float? at = null;
        for (int i = 5; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("at="))
                at = float.Parse(parts[i][3..], inv);
            else if (int.TryParse(parts[i], out int b))
                battery = b;
            else if (ShooterClass() is { } sc && Batteries(sc) is { Count: > 0 } bats)
            {
                var want = parts[i].ToLowerInvariant() switch { "he" => ShellKind.He, "common" or "sap" => ShellKind.Common, _ => ShellKind.Ap };
                var loads = sc.Loadout.Loads(bats[Math.Clamp(battery, 0, bats.Count - 1)]);
                for (int l = 0; l < loads.Length; l++)
                    if (sc.Loadout.ShellTypes[loads[l].ShellType].Kind == want)
                        load = l;
            }
        }
        FireShot();
        if (at is { } s && playback != null)
            (play, playing) = (Math.Min(s, (float)playback.TotalS), false);
    }

    // ------------------------------------------------------------------ the panel and the shot window

    /// <summary>The side panel's shot settings.</summary>
    void ShotPanel()
    {
        ImGui.Separator();
        ImGui.TextUnformatted("the shell (click on the ship to fire)");
        string who = shooter < 0 ? "this ship" : session.Names[shooter];
        if (ImGui.BeginCombo("shooter", who))
        {
            if (ImGui.Selectable("this ship", shooter < 0))
                (shooter, battery, load) = (-1, 0, 0);
            for (int i = 0; i < session.Names.Length; i++)
                if (ImGui.Selectable(session.Names[i], shooter == i))
                    (shooter, battery, load) = (i, 0, 0);
            ImGui.EndCombo();
        }
        if (ShooterClass() is { } sc)
        {
            var bats = Batteries(sc);
            if (bats.Count > 0)
            {
                battery = Math.Clamp(battery, 0, bats.Count - 1);
                string Name(int b) => $"{sc.Batteries[b].Kind.ToString().ToLowerInvariant()} {sc.Batteries[b].CalibreMm:0} mm";
                if (ImGui.BeginCombo("battery", Name(bats[battery])))
                {
                    for (int i = 0; i < bats.Count; i++)
                        if (ImGui.Selectable(Name(bats[i]), i == battery))
                            (battery, load) = (i, 0);
                    ImGui.EndCombo();
                }
                var loads = sc.Loadout.Loads(bats[battery]);
                load = Math.Clamp(load, 0, loads.Length - 1);
                if (ImGui.BeginCombo("shell", sc.Loadout.ShellTypeId(loads[load].ShellType)))
                {
                    for (int l = 0; l < loads.Length; l++)
                        if (ImGui.Selectable(sc.Loadout.ShellTypeId(loads[l].ShellType), l == load))
                            load = l;
                    ImGui.EndCombo();
                }
            }
        }
        ImGui.SliderFloat("range", ref rangeKm, 0.5f, 35, "%.1f km");
        ImGui.Checkbox("from the view's bearing", ref aspectFromView);
        if (!aspectFromView)
            ImGui.SliderFloat("from", ref aspectDeg, 0, 360, "%.0f° off the bow");
        ImGui.InputInt("seed", ref seed);
        ImGui.Checkbox("keep her damage", ref keepDamage);
        if (keepDamage)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("fresh ship"))
                shotRange?.Reset();
        }
        if (hasAim)
        {
            if (ImGui.Button("again"))
                FireShot();
            ImGui.SameLine();
            if (ImGui.Button("re-roll"))
            {
                seed++;
                FireShot();
            }
        }
        if (shotStatus.Length > 0)
            ImGui.TextWrapped(shotStatus);
    }

    /// <summary>The shot window: what was fired, the walk plate by plate (the plate the playback is at marked), the fuze
    /// and the burst, what the hit did, and the playback's controls.</summary>
    void ShotWindow()
    {
        if (shot is not { Struck: true } s || cls is not { } c || playback is not { } pb)
            return;
        var inv = CultureInfo.InvariantCulture;
        var vp = ImGui.GetMainViewport();
        float w = 620 * UiFonts.Scale;
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + vp.WorkSize.X - w - 8, vp.WorkPos.Y + 8), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(w, 560 * UiFonts.Scale), ImGuiCond.FirstUseEver);
        ImGui.Begin("Shot");

        // the playback
        if (ImGui.Button(playing ? "pause" : "play"))
        {
            if (!playing && play >= pb.TotalS)
                play = 0;
            playing = !playing;
        }
        ImGui.SameLine();
        if (ImGui.Button("replay"))
            (play, playing) = (0, true);
        ImGui.SameLine();
        ImGui.Checkbox("loop", ref loop);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120 * UiFonts.Scale);
        ImGui.SliderFloat("speed", ref playSpeed, 0.1f, 2, "%.2fx");
        if (ImGui.SliderFloat("time", ref play, 0, (float)pb.TotalS, "%.2f s"))
            playing = false;

        ImGui.Separator();
        var t = s.Terminal;
        ImGui.TextUnformatted(string.Format(inv, "{0}: {1:0.0} km from {2:0}° off the bow, falling {3:0.0}° at {4:0} m/s{5}", s.ShellName, s.RangeM / 1000,
            s.AspectDeg, s.FallDeg, s.AirStrikeMs, s.WaterRunM > 0 ? string.Format(inv, ", {0:0.0} m under the water to {1:0} m/s", s.WaterRunM, s.StrikeMs) : ""));
        ImGui.TextUnformatted(string.Format(inv, "{0}{1}{2}{3}{4}  (seed {5})", Words(t.Outcome), t.Decapped ? ", decapped" : "", t.Shattered ? ", shattered" : "",
            t.BrokeNearLimit ? ", broke near the limit" : "", t.TurnDeg > 0 ? string.Format(inv, ", turned {0:0.0}°", t.TurnDeg) : "", s.Seed));
        if (s.Fuze != FuzeCause.None)
            ImGui.TextUnformatted(string.Format(inv, "fuze {0} at {1:0.00} m{2}", s.Fuze switch
            {
                FuzeCause.Water => "started on the water, running",
                FuzeCause.Contents => "started on what it struck inside",
                FuzeCause.Nose => "fired (nose)",
                _ => "started by a plate",
            }, s.FuzeT - pb.T0, t.Bursts ? string.Format(inv, "; burst {0} at {1:0.00} m ({2:0.0} m on), force {3:0.00}, wreck radius {4:0.0} m",
                t.Burst.ToString().ToLowerInvariant(), t.BurstT - pb.T0, t.BurstT - s.FuzeT, t.Force, pb.WreckM) : ""));
        else if (t.Bursts)
            ImGui.TextUnformatted(string.Format(inv, "burst {0} at {1:0.00} m, force {2:0.00}", t.Burst.ToString().ToLowerInvariant(), t.BurstT - pb.T0, t.Force));

        // the walk, plate by plate
        int at = pb.StepAt(play);
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX;
        if (s.Steps.Length > 0 && ImGui.BeginTable("plates", 11, flags))
        {
            foreach (string h in (ReadOnlySpan<string>)["m", "plate", "mm (as met)", "ob°", "v in", "V_L", "v/V_L", "made of it", "v out", "turn°", "fuze ms"])
                ImGui.TableSetupColumn(h);
            ImGui.TableHeadersRow();
            for (int i = 0; i < s.Steps.Length; i++)
            {
                ref readonly var st = ref s.Steps[i];
                ref readonly var x = ref s.Path.Crossings[st.Crossing];
                ImGui.TableNextRow();
                if (i == at)
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.GetColorU32(new Vector4(0.9f, 0.7f, 0.2f, 0.35f)));
                Cell(string.Format(inv, "{0:0.00}", st.T - pb.T0));
                Cell(st.Call == PlateCall.Medium ? st.Name ?? "medium"
                    : st.Name is { } what ? string.Format(inv, "{0}: {1}", c.RoomId(x.Index), what)
                    : HitPath.PlateName(c, x) is { Length: > 0 } n ? n : x.Kind is CrossingKind.PartIn ? c.ComponentId(x.Index) : x.Kind.ToString());
                Cell(st.Call == PlateCall.Medium ? string.Format(inv, "{0:0.0} m", st.RunM)
                    : string.Format(inv, "{0:0.#}{1} ({2:0})", st.Mm, c.Material(x.Material) is { } m ? " " + m : "", st.EffectiveMm));
                Cell(string.Format(inv, "{0:0.0}", st.ObliquityDeg));
                Cell(string.Format(inv, "{0:0}", st.InMs));
                Cell(st.LimitMs > 0 ? string.Format(inv, "{0:0}", st.LimitMs) : "-");
                Cell(st.LimitMs > 0 ? string.Format(inv, "{0:0.00}", st.InMs / st.LimitMs) : "-");
                ImGui.TableNextColumn();
                ImGui.TextColored(CallColour(st.Call), Words(st.Call) + (st.Decapped ? ", decapped" : ""));
                Cell(string.Format(inv, "{0:0}", st.OutMs));
                Cell(st.TurnDeg > 0 ? string.Format(inv, "{0:0.0}", st.TurnDeg) : "");
                Cell(double.IsNaN(st.FuzeLeftS) ? "" : string.Format(inv, "{0:0.0}", st.FuzeLeftS * 1000));
            }
            ImGui.EndTable();
        }

        // what it did
        ImGui.Separator();
        var e = s.Effects;
        ImGui.TextUnformatted(string.Format(inv, "effects: {0}; killed {1}, wounded {2}{3}", s.Effect == HitEffect.None ? "none" : s.Effect.ToString(), e.Killed,
            e.Wounded, e.BreachM2 > 0 ? string.Format(inv, "; holes {0:0.00} m²", e.BreachM2) : ""));
        foreach (var (node, was, now, wasFlags, nowFlags) in s.Parts)
            ImGui.TextColored(StateColour(now, nowFlags != wasFlags), string.Format(inv, "  {0}: {1} -> {2}{3}", NodeName(c, node), was, now,
                nowFlags != wasFlags ? $" ({nowFlags})" : ""));
        foreach (var (cell, m2) in s.Holes)
            ImGui.TextColored(HoleColour, string.Format(inv, "  hole into {0}: {1:0.00} m²", c.CellId(cell), m2));
        foreach (int cell in s.Fires)
            ImGui.TextColored(FireColour, $"  fire in {c.CellId(cell)}");
        ImGui.End();

        static void Cell(string text)
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(text);
        }
    }

    static string NodeName(ShipClass c, int node) => node < c.Components.Length ? c.ComponentId(node) : c.RoomId(node - c.Components.Length);

    static string Words(Outcome o) => o switch
    {
        Outcome.Defeated => "defeated",
        Outcome.Ricochet => "ricochet",
        Outcome.PassedThrough => "passed through",
        Outcome.BurstInside => "burst inside",
        Outcome.BurstInPlate => "burst in the plate",
        Outcome.LowOrder => "low-order burst",
        Outcome.BurstOutside => "burst outside",
        Outcome.Dud => "dud",
        Outcome.BrokeUp => "broke up",
        Outcome.BurstBeyond => "burst beyond",
        Outcome.Holed => "holed in pieces",
        _ => o.ToString(),
    };

    static string Words(PlateCall call) => call switch
    {
        PlateCall.Through => "through",
        PlateCall.Glanced => "glanced",
        PlateCall.Holed => "holed (pieces)",
        PlateCall.Stopped => "stopped",
        PlateCall.Shattered => "shattered",
        PlateCall.BrokeUp => "broke up",
        PlateCall.BurstInPlate => "burst in it",
        PlateCall.NoseInside => "nose fuze: inside",
        PlateCall.NoseOutside => "nose fuze: outside",
        PlateCall.Medium => "slowed in it",
        _ => call.ToString(),
    };

    static readonly Vector4 HoleColour = new(0.3f, 0.7f, 1, 1), FireColour = new(1, 0.45f, 0.1f, 1);

    static Vector4 CallColour(PlateCall call) => call switch
    {
        PlateCall.Through => new(0.45f, 1, 0.45f, 1),
        PlateCall.Glanced => new(0.3f, 0.9f, 1, 1),
        PlateCall.Stopped => new(1, 0.25f, 0.25f, 1),
        PlateCall.Holed or PlateCall.Shattered or PlateCall.BrokeUp => new(1, 0.4f, 1, 1),
        _ => new(1, 0.65f, 0.2f, 1),
    };

    static Vector4 StateColour(PartState s, bool flagsOnly) => s switch
    {
        PartState.Destroyed => new(1, 0.2f, 0.2f, 1),
        PartState.Disabled => new(1, 0.55f, 0.15f, 1),
        PartState.Damaged => new(1, 0.9f, 0.3f, 1),
        _ => flagsOnly ? new(0.75f, 0.55f, 1, 1) : new(0.8f, 0.8f, 0.8f, 1),
    };

    // ------------------------------------------------------------------ the playback

    /// <summary>The shot in time: the shell comes in along its flight for <see cref="ApproachS"/>, then its head runs
    /// along the path from where it struck (T0) to where its body stopped or left (TEnd), at a speed on screen in
    /// proportion to its own (at least <see cref="MinShare"/> of its striking speed's, so a slow shell still moves),
    /// the whole path taking about <see cref="PathS"/> at full speed; then the burst and what it reached, held
    /// <see cref="AfterS"/>.</summary>
    sealed class Playback
    {
        public const double ApproachM = 30, PathS = 1.6, MinShare = 0.12, AfterS = 2.2;
        public double T0, TEnd, ApproachS, EndS, TotalS, WreckM, StrikeMs;
        public readonly List<(double T, double Time, double Speed, double Ms)> Knots = [];
        public double[] StepTime = [];

        public static Playback Of(Shot s, SimSettings settings)
        {
            var p = new Playback { StrikeMs = Math.Max(s.StrikeMs, 1) };
            var xs = s.Path.Crossings;
            p.T0 = 0;
            foreach (ref readonly var x in xs)
                if (x.Kind is CrossingKind.HullIn or CrossingKind.PartIn)
                {
                    p.T0 = x.T;
                    break;
                }
            double last = xs.Length > 0 ? xs[^1].T : p.T0;
            p.TEnd = Math.Max(p.T0, Math.Min(s.Terminal.StopT, last + 6));
            double k = Math.Max(p.TEnd - p.T0, 8) / PathS;   // m/s on screen at its striking speed
            double Vis(double v) => k * Math.Max(v / p.StrikeMs, MinShare);
            p.ApproachS = Math.Clamp(ApproachM / k, 0.3, 0.8);
            double t = p.T0, time = p.ApproachS, v = s.StrikeMs;
            p.Knots.Add((t, time, Vis(v), v));
            foreach (ref readonly var st in s.Steps.AsSpan())
            {
                if (st.T <= t || st.T >= p.TEnd)
                    continue;
                time += (st.T - t) / p.Knots[^1].Speed;
                (t, v) = (st.T, st.OutMs);
                p.Knots.Add((t, time, Vis(v), v));
            }
            p.EndS = time + (p.TEnd - t) / p.Knots[^1].Speed;
            p.StepTime = new double[s.Steps.Length];
            for (int i = 0; i < s.Steps.Length; i++)
                p.StepTime[i] = p.TimeAt(s.Steps[i].T);
            p.WreckM = s.Terminal.Bursts ? Effects.WreckRadiusM(s.Shell, s.Terminal.Force, settings) : 0;
            p.TotalS = p.EndS + AfterS;
            return p;
        }

        /// <summary>The knot T is in.</summary>
        int KnotOf(double t)
        {
            int i = 0;
            while (i + 1 < Knots.Count && Knots[i + 1].T <= t)
                i++;
            return i;
        }

        /// <summary>When the head reaches T along the path.</summary>
        public double TimeAt(double t)
        {
            var kn = Knots[KnotOf(Math.Clamp(t, T0, TEnd))];
            return kn.Time + (Math.Clamp(t, T0, TEnd) - kn.T) / kn.Speed;
        }

        /// <summary>Where the head is along the path at a time (NaN while it's still coming in).</summary>
        public double TAt(double time)
        {
            if (time < ApproachS)
                return double.NaN;
            int i = 0;
            while (i + 1 < Knots.Count && Knots[i + 1].Time <= time)
                i++;
            return Math.Min(Knots[i].T + (time - Knots[i].Time) * Knots[i].Speed, TEnd);
        }

        /// <summary>The shell's own speed at T, m/s.</summary>
        public double MsAt(double t) => Knots[KnotOf(t)].Ms;

        /// <summary>The last plate the head has reached at a time (-1: none yet).</summary>
        public int StepAt(double time)
        {
            int at = -1;
            for (int i = 0; i < StepTime.Length; i++)
                if (StepTime[i] <= time + 1e-9)
                    at = i;
            return at;
        }
    }

    /// <summary>Ship-frame points onto the screen, as the renderer draws them.</summary>
    readonly struct Projector(Matrix4x4 vp, Vector2 size)
    {
        public bool Screen(ShipP3 p, out Vector2 q)
        {
            var c = Vector4.Transform(new Vector4((float)p.X, (float)p.Y, (float)p.Z, 1), vp);
            q = c.W > 1e-4f ? new Vector2((c.X / c.W + 1) / 2 * size.X, (1 - c.Y / c.W) / 2 * size.Y) : default;
            return c.W > 1e-4f;
        }
    }

    Projector Project(HitboxMesh m)
    {
        var size = ImGui.GetIO().DisplaySize;
        return new(cam.ViewProj(size.X / Math.Max(1, size.Y), (m.Min + m.Max) / 2, Math.Max(1, (m.Max - m.Min).Length() / 2 + 5)), size);
    }

    /// <summary>The shot over the view at the playback's time: its line of flight coming in, the path it has run so far
    /// coloured by its speed (the rest of the line faint), the plates it has reached marked by what it made of them
    /// (flashing as it meets them, the newest named), the fuze, the head, and at the end the burst at its wreck radius,
    /// lines to the parts it made worse and boxes round them, its holes and fires. An x-ray: nothing hides it.</summary>
    void DrawShot()
    {
        if (click != ClickMode.Shell || shot is not { Struck: true } s || playback is not { } pb || renderer.Mesh is not { } m || cls is not { } c)
            return;
        var pr = Project(m);
        var dl = ImGui.GetBackgroundDrawList();
        var path = s.Path;
        double time = play, head = pb.TAt(time);
        var d = s.Direction;
        ShipP3 Back(double metres) => new(s.Point.X - d.X * metres, s.Point.Y - d.Y * metres, s.Point.Z - d.Z * metres);
        uint U(Vector4 v) => ImGui.GetColorU32(v);

        // the line of flight, faint: in, and on along the path past where the head is
        Line(Back(Playback.ApproachM * 1.5), s.Point, new Vector4(1, 1, 1, 0.18f), 1);
        Along(pb.T0, pb.TEnd, false, 1);

        // coming in
        if (double.IsNaN(head))
        {
            double f = Math.Clamp(time / pb.ApproachS, 0, 1);
            var at = Back(Playback.ApproachM * (1 - f));
            Line(Back(Playback.ApproachM * (1 - f) + 6), at, new Vector4(1, 0.95f, 0.6f, 0.7f), 2);
            Head(at);
            return;
        }

        // the path so far, by speed
        Line(Back(6), s.Point, new Vector4(1, 0.95f, 0.6f, 0.5f), 2);
        Along(pb.T0, head, true, 3);

        // the crossings it has passed (spaces and parts, faint) and the plates it has reached
        foreach (ref readonly var x in path.Crossings)
            if (x.T <= head && x.Plate == PlateKind.None && pr.Screen(x.Point, out var q))
                dl.AddCircleFilled(q, 2, U(new Vector4(0.75f, 0.75f, 0.75f, 0.5f)));
        int newest = -1;
        for (int i = 0; i < s.Steps.Length; i++)
        {
            if (pb.StepTime[i] > time)
                continue;
            newest = i;
            ref readonly var st = ref s.Steps[i];
            if (!pr.Screen(st.Point, out var q))
                continue;
            var col = CallColour(st.Call);
            double age = time - pb.StepTime[i];
            if (age < 0.35)
                dl.AddCircle(q, 6 + (float)age * 70, U(col with { W = (float)(1 - age / 0.35) }), 0, 2.5f);
            if (st.Call is PlateCall.Stopped or PlateCall.NoseOutside)
            {
                dl.AddLine(q - new Vector2(6, 6), q + new Vector2(6, 6), U(col), 3);
                dl.AddLine(q - new Vector2(6, -6), q + new Vector2(6, -6), U(col), 3);
            }
            else
                dl.AddCircle(q, 6, U(col), 0, 2.5f);
        }
        if (s.Fuze != FuzeCause.None && pb.TimeAt(s.FuzeT) <= time && pr.Screen(path.At(s.FuzeT), out var fq))
        {
            dl.AddQuadFilled(fq + new Vector2(0, -5), fq + new Vector2(5, 0), fq + new Vector2(0, 5), fq + new Vector2(-5, 0), U(new Vector4(1, 1, 1, 0.95f)));
            dl.AddText(fq + new Vector2(7, -16), U(new Vector4(1, 1, 1, 0.9f)), "fuze");
        }
        if (newest >= 0 && pr.Screen(s.Steps[newest].Point, out var lq))
        {
            ref readonly var st = ref s.Steps[newest];
            string text = string.Format(CultureInfo.InvariantCulture, "{0} {1:0} mm, {2:0}°\n{3:0} -> {4:0} m/s (V_L {5:0}): {6}", HitPath.PlateName(c,
                path.Crossings[st.Crossing]), st.Mm, st.ObliquityDeg, st.InMs, st.OutMs, st.LimitMs, Words(st.Call));
            dl.AddText(lq + new Vector2(10, 6), U(new Vector4(0, 0, 0, 0.8f)), text);
            dl.AddText(lq + new Vector2(9, 5), U(CallColour(st.Call)), text);
        }

        // the head, until it's done
        if (time < pb.EndS)
        {
            Head(path.At(head));
            return;
        }

        // the end: the burst at its wreck radius (or the pieces of a broken shell), then what it reached
        double after = time - pb.EndS;
        var t = s.Terminal;
        var end = path.At(t.Bursts ? t.BurstT : pb.TEnd);
        if (pr.Screen(end, out var eq))
        {
            if (t.Bursts)
            {
                float rpx = Radius(end, Math.Max(pb.WreckM, 0.5));
                double g = Math.Clamp(after / 0.35, 0, 1);
                float r = rpx * (float)(1 - (1 - g) * (1 - g));
                float fade = (float)Math.Clamp(1 - (after - 0.35) / 1.5, 0.25, 1);
                dl.AddCircleFilled(eq, r, U(new Vector4(1, 0.55f, 0.15f, 0.35f * fade)));
                dl.AddCircle(eq, r, U(new Vector4(1, 0.8f, 0.3f, fade)), 0, 2);
                dl.AddCircleFilled(eq, Math.Max(3, r * 0.25f), U(new Vector4(1, 1, 0.8f, fade)));
            }
            else if (t.Broken)
                dl.AddCircle(eq, 9, U(new Vector4(1, 0.4f, 1, 1)), 0, 2);
            else if (t.Outcome is Outcome.Defeated or Outcome.Dud && t.Plate >= 0)
                dl.AddCircle(eq, 10, U(new Vector4(1, 0.25f, 0.25f, 1)), 0, 3);
        }
        if (after < 0.2)
            return;
        double reach = Math.Clamp((after - 0.2) / 0.4, 0, 1);
        foreach (var (node, _, now, wasFlags, nowFlags) in s.Parts)
        {
            var box = NodeBox(c, node);
            var col = StateColour(now, wasFlags != nowFlags);
            var mid = new ShipP3((box.X0 + box.X1) / 2, (box.Y0 + box.Y1) / 2, (box.Base + box.Top) / 2);
            Line(end, new ShipP3(end.X + (mid.X - end.X) * reach, end.Y + (mid.Y - end.Y) * reach, end.Z + (mid.Z - end.Z) * reach),
                col with { W = 0.6f }, 1.5f);
            if (reach >= 1)
                Wire(box, col, 1.5f);
        }
        if (reach >= 1)
        {
            foreach (var (cell, _) in s.Holes)
                Wire(c.Cells[cell].Box, HoleColour, 2);
            foreach (int cell in s.Fires)
                Wire(c.Cells[cell].Box, FireColour with { W = 0.6f + 0.4f * MathF.Sin((float)time * 12) }, 2);
        }

        void Head(ShipP3 at)
        {
            if (!pr.Screen(at, out var q))
                return;
            dl.AddCircleFilled(q, 6, U(new Vector4(1, 0.9f, 0.4f, 0.35f)));
            dl.AddCircleFilled(q, 3.5f, U(new Vector4(1, 1, 0.9f, 1)));
        }

        void Line(ShipP3 a, ShipP3 b, Vector4 col, float width)
        {
            if (pr.Screen(a, out var qa) && pr.Screen(b, out var qb))
                dl.AddLine(qa, qb, U(col), width);
        }

        // the path from T a to T b, a piece per leg and per knot of speed: faint, or by the shell's speed
        void Along(double a, double b, bool bySpeed, float width)
        {
            double t0 = a;
            while (t0 < b - 1e-6)
            {
                double t1 = b;
                for (int k = 1; k < path.Legs; k++)
                    if (path.LegOf(k).T > t0 + 1e-6)
                    {
                        t1 = Math.Min(t1, path.LegOf(k).T);
                        break;
                    }
                foreach (var kn in pb.Knots)
                    if (kn.T > t0 + 1e-6)
                    {
                        t1 = Math.Min(t1, kn.T);
                        break;
                    }
                Line(path.At(t0), path.At(t1), bySpeed ? SpeedColour(pb.MsAt(t0) / pb.StrikeMs) : new Vector4(1, 1, 1, 0.18f), width);
                t0 = t1;
            }
        }

        // a sphere's radius on screen at a point
        float Radius(ShipP3 at, double metres)
        {
            var (right, _, _) = cam.Basis();
            var side = new ShipP3(at.X + right.X * metres, at.Y + right.Y * metres, at.Z + right.Z * metres);
            return pr.Screen(at, out var a) && pr.Screen(side, out var b) ? Vector2.Distance(a, b) : 0;
        }

        void Wire(in Box b, Vector4 col, float width)
        {
            Span<Vector2> q = stackalloc Vector2[8];
            for (int i = 0; i < 8; i++)
                if (!pr.Screen(new ShipP3((i & 1) == 0 ? b.X0 : b.X1, (i & 2) == 0 ? b.Y0 : b.Y1, (i & 4) == 0 ? b.Base : b.Top), out q[i]))
                    return;
            uint u = U(col);
            for (int i = 0; i < 8; i++)
                for (int bit = 1; bit <= 4; bit <<= 1)
                    if ((i & bit) == 0)
                        dl.AddLine(q[i], q[i | bit], u, width);
        }
    }

    /// <summary>The colour of the shell's speed as a share of its striking speed: pale yellow at full, red near none.</summary>
    static Vector4 SpeedColour(double share)
    {
        float f = (float)Math.Clamp(share, 0, 1);
        return new Vector4(1, 0.25f + 0.7f * f, 0.1f + 0.5f * f, 1);
    }

    /// <summary>A node's box: a component's bounds, or the cells of a room together.</summary>
    static Box NodeBox(ShipClass c, int node)
    {
        if (node < c.Components.Length)
            return c.Components[node].Bounds;
        double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue, z0 = double.MaxValue, z1 = double.MinValue;
        foreach (int cell in c.Links(c.Rooms[node - c.Components.Length].Cells))
        {
            var b = c.Cells[cell].Box;
            (x0, x1, y0, y1, z0, z1) = (Math.Min(x0, b.X0), Math.Max(x1, b.X1), Math.Min(y0, b.Y0), Math.Max(y1, b.Y1), Math.Min(z0, b.Base), Math.Max(z1, b.Top));
        }
        return new Box(x0, x1, y0, y1, z0, z1);
    }
}
