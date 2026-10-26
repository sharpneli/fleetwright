using System.Globalization;
using System.Numerics;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using ImGuiNET;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

namespace Fleetwright.Map;

/// <summary>The launch screen's conditions (docs/map-ui/launch.md L5, tab 3 of the mockup launch-bureau.html), in three
/// columns of tables: both sides side by side (yours first) in what decides how well they fight, not what they are: gun
/// crews, readiness, damage control, fire discipline, fuel, with four presets over them as a segmented control (as
/// history, even crews, easier, harder: first guesses, decision 5; the one the settings match is lit); the day
/// (visibility, wind, the end, the seed, and the aim it's fought for); and the changes from the scenario, a line each
/// with its ✕. Each is a change of the setup (<see cref="SideSetting"/>, <see cref="SetWeather"/>, <see cref="SetEnd"/>,
/// <see cref="SetSeed"/>), gone when it's put back as written. Its lines are made when the pick changes.</summary>
public sealed unsafe partial class LaunchScene
{
    static readonly string[] CrewNames = ["green", "average", "trained", "elite"];
    static readonly (double Share, string Word)[] FuelSteps = [(1, "full"), (0.75, "75%"), (0.5, "50%"), (0.25, "25%")];
    static readonly SideKind[] Kinds = [SideKind.Crews, SideKind.Readiness, SideKind.DamageControl, SideKind.FireDiscipline, SideKind.Fuel];
    static readonly string[] KindLabels = ["Gun crews", "Readiness", "Damage control", "Fire discipline", "Fuel"];
    static readonly string[] PresetNames = ["As history", "Even crews", "Easier", "Harder"];
    static readonly string[] PresetTips =
    [
        "every side's crews, readiness, damage control, fire discipline and fuel as the scenario has them",
        "both sides' gun crews trained",
        "yours trained; the enemy's average, his decks cluttered",
        "yours green, your decks cluttered",
    ];
    static readonly string[] SeedWordsNew = ["the file's", "new each time"];

    string[][]? kindOptions;
    string[] FuelWords => [.. FuelSteps.Select(f => f.Word)];

    sealed class ConditionsView
    {
        public Scenario? Of;
        public int Own, Other;
        public string OwnHead = "", OtherHead = "", OwnEnsign = "", OtherEnsign = "";
        public readonly string[,] Now = new string[5, 2];
        public readonly bool[,] Changed = new bool[5, 2];
        public WeatherHere Weather, WrittenWeather;
        public string Vis = "", VisWord = "", WindKn = "", Ends = "", SeedNote = "";
        public bool WeatherChanged, EndChanged, NewSeed;
        public int WindPoint, Preset = -1;
        public string[] SeedWords = [];
    }

    ConditionsView? conditionsView;
    ulong drawnSeed;   // the seed drawn for "new each time", for this pick

    string[] OptionsOf(SideKind k)
    {
        kindOptions ??=
        [
            CrewNames,
            [.. world.Settings.Readiness.Select(r => r.Name)],
            [.. world.Settings.DamageControl.Select(d => d.Name)],
            [.. world.Settings.FireDisciplines.Select(d => d.Name)],
            FuelWords,
        ];
        return kindOptions[Array.IndexOf(Kinds, k)];
    }

    /// <summary>A side's setting in a scenario, as its option is named (a side not given takes the settings' default).</summary>
    string SettingOf(Scenario s, SideKind k, int sd) => k switch
    {
        SideKind.Crews => s.Crews.Where(x => x.Side == sd).Select(x => x.Quality.ToString().ToLowerInvariant()).FirstOrDefault() ?? "trained",
        SideKind.Readiness => s.Readiness.Where(x => x.Side == sd).Select(x => x.Name).FirstOrDefault() ?? world.Settings.ReadinessDefault,
        SideKind.DamageControl => s.DamageControl.Where(x => x.Side == sd).Select(x => x.Name).FirstOrDefault() ?? world.Settings.DamageControlDefault,
        SideKind.FireDiscipline => s.FireDisciplines.Where(x => x.Side == sd).Select(x => x.Name).FirstOrDefault() ?? world.Settings.FireDisciplines[0].Name,
        _ => FuelWord(s.Fuel.Where(x => x.Side == sd).Select(x => x.Share).DefaultIfEmpty(1).First()),
    };

    static string FuelWord(double share)
    {
        foreach (var (v, word) in FuelSteps)
            if (Math.Abs(v - share) < 1e-6)
                return word;
        return $"{share * 100:0}%";
    }

    /// <summary>The setup with a side's setting at an option, or without the change when that's as written.</summary>
    ScenarioSetup Setting(ScenarioSetup st, SideKind k, int sd, string option)
    {
        var c = new SideSetting(k, sd, k == SideKind.Fuel
            ? FuelSteps.First(f => f.Word == option).Share.ToString("0.###", CultureInfo.InvariantCulture) : option);
        return option == SettingOf(written!, k, sd) ? st.Without(c.Key) : st.With(c);
    }

    /// <summary>The setup with the conditions of a preset (decision 5: first guesses, to be settled by playtesting).</summary>
    ScenarioSetup Preset(int k, int own, int other)
    {
        var st = setup with { Changes = [.. setup.Changes.Where(c => c is not SideSetting)] };
        const string Cluttered = "prepared, cluttered";
        bool cluttered = Array.IndexOf(OptionsOf(SideKind.Readiness), Cluttered) >= 0;
        switch (k)
        {
            case 1:
                st = Setting(Setting(st, SideKind.Crews, own, "trained"), SideKind.Crews, other, "trained");
                break;
            case 2:
                st = Setting(Setting(st, SideKind.Crews, own, "trained"), SideKind.Crews, other, "average");
                if (cluttered)
                    st = Setting(st, SideKind.Readiness, other, Cluttered);
                break;
            case 3:
                st = Setting(st, SideKind.Crews, own, "green");
                if (cluttered)
                    st = Setting(st, SideKind.Readiness, own, Cluttered);
                break;
        }
        return st;
    }

    WeatherHere WeatherOf(Scenario s) =>
        s.Weather ?? new WeatherHere(world.Settings.DefaultVisibilityM, world.Settings.DefaultWindFromDeg, world.Settings.DefaultWindMs);

    /// <summary>The setup with the weather, or without the change when it's as written.</summary>
    ScenarioSetup Weather(WeatherHere w)
    {
        var was = WeatherOf(written!);
        bool same = Math.Abs(w.VisibilityM - was.VisibilityM) < 1 && Math.Abs(w.WindFromDeg - was.WindFromDeg) < 0.5 && Math.Abs(w.WindMs - was.WindMs) < 0.01;
        return same ? setup.Without("weather") : setup.With(new SetWeather(w with { AirOverSeaK = was.AirOverSeaK }));
    }

    /// <summary>When a scenario ends: its "until", else the start and the settings' play limit.</summary>
    WorldTime EndOf(Scenario s) => s.Until ?? s.Start + world.Settings.PlayLimitMs;

    ConditionsView Conditions(Scenario s)
    {
        if (conditionsView is { } v && ReferenceEquals(v.Of, s))
            return v;
        v = conditionsView = new ConditionsView { Of = s };
        v.Own = side >= 0 ? side : shipTabs.Count > 0 ? shipTabs[0].Side : 0;
        v.Other = shipTabs.Select(t => t.Side).FirstOrDefault(sd => sd != v.Own, v.Own == 0 && s.Sides.Length > 1 ? 1 : 0);
        v.OwnHead = s.Sides[v.Own] + (side >= 0 ? " · yours" : "");
        v.OtherHead = s.Sides[v.Other];
        v.OwnEnsign = s.AboutOf(v.Own)?.Ensign ?? "";
        v.OtherEnsign = s.AboutOf(v.Other)?.Ensign ?? "";
        for (int k = 0; k < Kinds.Length; k++)
            for (int c = 0; c < 2; c++)
            {
                int sd = c == 0 ? v.Own : v.Other;
                v.Now[k, c] = SettingOf(s, Kinds[k], sd);
                v.Changed[k, c] = v.Now[k, c] != SettingOf(written!, Kinds[k], sd);
            }
        // the preset the side settings match, if one does
        var mine = setup.Changes.OfType<SideSetting>().Select(c => c.Key + "=" + c.Value).Order().ToList();
        for (int k = 0; k < PresetNames.Length && v.Preset < 0; k++)
            if (Preset(k, v.Own, v.Other).Changes.OfType<SideSetting>().Select(c => c.Key + "=" + c.Value).Order().SequenceEqual(mine))
                v.Preset = k;
        v.Weather = WeatherOf(s);
        v.WrittenWeather = WeatherOf(written!);
        v.WeatherChanged = setup.Changes.Any(c => c is SetWeather);
        v.Vis = $"{v.Weather.VisibilityM / 1000:0} km";
        v.VisWord = SightWord(v.Weather.VisibilityM);
        v.WindPoint = (int)Math.Round(((v.Weather.WindFromDeg % 360) + 360) % 360 / 22.5) % 16;
        v.WindKn = $"{v.Weather.WindMs / 0.514444:0} kn";
        var (_, lon) = Centre(s);
        v.Ends = EndOf(s).ToUtc().AddHours(lon / 15).ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
        v.EndChanged = setup.Changes.Any(c => c is SetEnd);
        v.NewSeed = setup.Changes.Any(c => c is SetSeed { Seed: null });
        v.SeedWords = [$"{written!.Seed}, the file's", SeedWordsNew[1]];
        v.SeedNote = v.NewSeed ? $"this time {s.Seed}" : setup.Changes.OfType<SetSeed>().FirstOrDefault() is { Seed: { } n } ? $"{n}" : "";
        return v;
    }

    void ConditionsPage(Vector2 a, Vector2 size)
    {
        if (current is not { } s)
            return;
        var v = Conditions(s);
        float w1 = MathF.Round(size.X * 0.37f), w2 = MathF.Round(size.X * 0.33f), w3 = size.X - w1 - w2;
        CrewsColumn(a, new Vector2(w1, size.Y), v);
        dl.AddLine(new Vector2(a.X + w1, a.Y), new Vector2(a.X + w1, a.Y + size.Y), U(T.Edge));
        DayColumn(new Vector2(a.X + w1 + 1, a.Y), new Vector2(w2 - 1, size.Y), v, s);
        dl.AddLine(new Vector2(a.X + w1 + w2, a.Y), new Vector2(a.X + w1 + w2, a.Y + size.Y), U(T.Edge));
        ChangesColumn(new Vector2(a.X + w1 + w2 + 1, a.Y), new Vector2(w3 - 1, size.Y));
    }

    void CrewsColumn(Vector2 a, Vector2 size, ConditionsView v)
    {
        float w = size.X, y = a.Y;
        float right = dl.SubHeader(a, w, "CREWS AND SHIPS");
        float pw = SegmentedWidth(PresetNames) - 1;
        int hit = dl.Segmented("##preset", new Vector2(right - pw - 1, y + P(2)), PresetNames, v.Preset, out _);
        if (hit >= 0)
            pendingEdit = Preset(hit, v.Own, v.Other);
        if (ImGui.IsMouseHoveringRect(new Vector2(right - pw, y), new Vector2(right, y + RowH)))
        {
            float hx = right - pw - 1;
            for (int k = 0; k < PresetNames.Length; k++)
            {
                float bw = Width(UiFonts.SansSmall, PresetNames[k]) + P(14) - 1;
                if (ImGui.GetMousePos().X >= hx && ImGui.GetMousePos().X < hx + bw)
                    ImGui.SetTooltip(PresetTips[k]);
                hx += bw;
            }
        }
        y += RowH;
        var h = new Vector2(a.X, y);
        float c0 = a.X + P(112), cw = (w - P(118)) / 2;
        dl.Head(h, w);
        dl.HeadCell(h, a.X + P(6), "ROW");
        for (int c = 0; c < 2; c++)
        {
            float hx = c0 + c * cw;
            Ensign(c == 0 ? v.OwnEnsign : v.OtherEnsign, new Vector2(hx, h.Y + P(4)), new Vector2(P(16), P(10)));
            dl.HeadCell(h, hx + P(22), c == 0 ? v.OwnHead : v.OtherHead);
        }
        y += HeadH;
        for (int k = 0; k < Kinds.Length; k++)
        {
            var ra = new Vector2(a.X, y);
            dl.RowBg(ra, w, RowH, k);
            dl.Text(UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), T.Text, KindLabels[k]);
            var opts = OptionsOf(Kinds[k]);
            for (int c = 0; c < 2; c++)
            {
                int picked = dl.DropDown($"##cond{k}{c}", new Vector2(c0 + c * cw, ra.Y + P(2)), cw - P(8), v.Now[k, c], opts, v.Changed[k, c]);
                if (picked >= 0 && opts[picked] != v.Now[k, c])
                    pendingEdit = Setting(setup, Kinds[k], c == 0 ? v.Own : v.Other, opts[picked]);
            }
            y += RowH;
        }
        y += P(6);
        dl.Wrapped(UiFonts.SansSmall, new Vector2(a.X + P(6), y), T.Muted,
            "What decides how well they fight, not what they are. The presets are first guesses, to be settled by playtesting.", w - P(12));
    }

    void DayColumn(Vector2 a, Vector2 size, ConditionsView v, Scenario s)
    {
        float w = size.X, y = a.Y;
        dl.SubHeader(a, w, "THE DAY");
        y += RowH;
        var h = new Vector2(a.X, y);
        float c0 = a.X + P(86);
        dl.Head(h, w);
        dl.HeadCell(h, a.X + P(6), "ROW");
        dl.HeadCell(h, c0, "SETTING");
        y += HeadH;
        int n = 0;
        Vector2 Row(string label)
        {
            var ra = new Vector2(a.X, y);
            dl.RowBg(ra, w, RowH, n++);
            dl.Text(UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), T.Text, label);
            y += RowH;
            return ra;
        }
        void Note(Vector2 ra, float x, string s2, bool brass = false) =>
            dl.Text(UiFonts.SansSmall, new Vector2(x, Mid(UiFonts.SansSmall, ra.Y, RowH)), brass ? T.Brass : T.Muted, s2);

        // visibility, by the kilometre
        var r = Row("Visibility");
        double km = Math.Round(v.Weather.VisibilityM / 1000);
        float sw = dl.Stepper("##vis", new Vector2(c0, r.Y + P(2.5f)), v.Vis, P(52), km > 1, km < 30, out int by);
        if (by != 0)
            pendingEdit = Weather(v.Weather with { VisibilityM = Math.Clamp(km + by, 1, 30) * 1000 });
        Note(r, c0 + sw + P(8), v.WeatherChanged && Math.Abs(v.Weather.VisibilityM - v.WrittenWeather.VisibilityM) > 1 ? "changed" : v.VisWord, v.WeatherChanged && Math.Abs(v.Weather.VisibilityM - v.WrittenWeather.VisibilityM) > 1);
        // the wind: from, and how hard
        r = Row("Wind");
        int point = dl.DropDown("##windfrom", new Vector2(c0, r.Y + P(2)), P(56), SetWeather.Points[v.WindPoint], SetWeather.Points,
            v.WeatherChanged && Math.Abs(v.Weather.WindFromDeg - v.WrittenWeather.WindFromDeg) > 0.5);
        if (point >= 0 && point != v.WindPoint)
            pendingEdit = Weather(v.Weather with { WindFromDeg = point * 22.5 });
        double kn = Math.Round(v.Weather.WindMs / 0.514444);
        sw = dl.Stepper("##wind", new Vector2(c0 + P(62), r.Y + P(2.5f)), v.WindKn, P(46), kn > 0, kn < 60, out by);
        if (by != 0)
            pendingEdit = Weather(v.Weather with { WindMs = Math.Clamp(kn + 2 * by, 0, 60) * 0.514444 });
        Note(r, c0 + P(62) + sw + P(8), "from, and how hard");
        // the end, by the hour
        r = Row("Ends");
        var end = EndOf(s);
        sw = dl.Stepper("##ends", new Vector2(c0, r.Y + P(2.5f)), v.Ends, P(96), end.Ms - 3_600_000L > s.Start.Ms, end.Ms < s.Start.Ms + 72 * 3_600_000L, out int hours);
        if (hours != 0)
        {
            var to = end + Math.Sign(hours) * 3_600_000L;
            pendingEdit = written!.Until is { } wu && wu.Ms == to.Ms || written.Until == null && to.Ms == EndOf(written).Ms ? setup.Without("until") : setup.With(new SetEnd(to));
        }
        Note(r, c0 + sw + P(8), v.EndChanged ? "changed" : "local, at the latest", v.EndChanged);
        // the seed
        r = Row("Seed");
        int seed = dl.Segmented("##seed", new Vector2(c0, r.Y + P(2)), v.SeedWords, v.NewSeed ? 1 : 0, out sw);
        if (seed >= 0)
            pendingEdit = seed == 1 ? setup.With(new SetSeed(null)) : setup.Without("seed");
        if (v.SeedNote.Length > 0)
            Note(r, c0 + sw + P(8), v.SeedNote);
        // the aims and the end
        foreach (var (label, value) in aims)
        {
            float th = WrappedHeight(UiFonts.Sans, value, w - (c0 - a.X) - P(8));
            float rh = Math.Max(RowH, th + P(4));
            var ra = new Vector2(a.X, y);
            if (n++ % 2 == 1)
                dl.AddRectFilled(ra, ra + new Vector2(w, rh), U(new Vector4(1, 1, 1, 0.018f)));
            dl.HLine(ra.X, ra.X + w, ra.Y + rh - 1, T.Line);
            dl.Text(UiFonts.Sans, new Vector2(ra.X + P(6), Mid(UiFonts.Sans, ra.Y, RowH)), T.Text, label);
            dl.Wrapped(UiFonts.Sans, new Vector2(c0, ra.Y + P(2)), T.Bright, value, w - (c0 - a.X) - P(8));
            y += rh;
        }
    }

    readonly List<(string Label, string Value)> aims = [];

    void MadeBody(Scenario s)
    {
        MadeShips(s);
        aims.Clear();
        var (_, lon) = Centre(s);
        string end = s.Until is { } u2 ? $"at {u2.ToUtc().AddHours(lon / 15):HH:mm} local" : "";
        for (int sd = 0; sd < s.Sides.Length; sd++)
            if (side < 0 || sd == side)
                aims.Add((side < 0 ? s.Sides[sd] : "The aim", MapScene.AimsText(s, sd)));
        aims.Add(("The end", $"when one side is gone or out of the fight{(end.Length > 0 ? ", or " + end : "")}"));
    }

    /// <summary>Every change from the scenario as written, a line each with its undo (✕), and Reset all.</summary>
    void ChangesColumn(Vector2 a, Vector2 size)
    {
        float w = size.X;
        float right = dl.SubHeader(a, w, "CHANGES FROM THE SCENARIO");
        const string Reset = "Reset all";
        float bw = ButtonWidth(Reset, null);
        if (dl.Button("##resetall", new Vector2(right - bw, a.Y + P(1)), Reset, enabled: !setup.IsEmpty && !begin))
            pendingEdit = setup with { Changes = [] };
        ImGui.SetCursorScreenPos(a + new Vector2(0, RowH));
        var keep = dl;
        float y = 0;
        if (ImGui.BeginChild("##changes", new Vector2(w, size.Y - RowH), ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            dl = ImGui.GetWindowDrawList();
            var o = ImGui.GetCursorScreenPos();
            float cw = ImGui.GetContentRegionAvail().X;
            dl.Head(o, cw);
            dl.HeadCell(o, o.X + P(6), "CHANGE");
            y = o.Y + HeadH;
            if (setup.IsEmpty)
            {
                dl.Text(UiFonts.SansSmall, new Vector2(o.X + P(6), Mid(UiFonts.SansSmall, y, RowH)), T.Muted, "None: the scenario as written.");
                y += RowH;
            }
            SetupChange? undo = null;
            for (int i = 0; i < setup.Changes.Count; i++)
            {
                var c = setup.Changes[i];
                string text = Described(c);
                float th = WrappedHeight(UiFonts.Sans, text, cw - P(34));
                float rh = Math.Max(RowH, th + P(4));
                var ra = new Vector2(o.X, y);
                dl.RowBg(ra, cw, rh, i);
                dl.Wrapped(UiFonts.Sans, new Vector2(ra.X + P(6), ra.Y + P(2)), T.Text, text, cw - P(34));
                ImGui.PushID(i);
                if (dl.CrossButton("##undo", new Vector2(ra.X + cw - P(12), ra.Y + RowH / 2), "undo: as written"))
                    undo = c;
                ImGui.PopID();
                y += rh;
            }
            if (undo != null && !begin)
                pendingEdit = undo is AddUnit au ? setup.WithoutAdded(au.Unit.Name) : setup.Without(undo.Key);
            y += P(4) - o.Y;
        }
        ImGui.SetCursorPos(new Vector2(0, Math.Max(y, 0)));
        ImGui.Dummy(new Vector2(1, 0));
        ImGui.EndChild();
        dl = keep;
    }

    // each change's line, made once (a frame allocates nothing for a list that stands); by the change itself, as a
    // change is a new one when it's changed
    readonly Dictionary<SetupChange, string> described = new(ReferenceEqualityComparer.Instance);

    string Described(SetupChange c)
    {
        if (!described.TryGetValue(c, out var s))
            described[c] = s = c switch
            {
                SwapDesign sw => sw.Describe(written!, TitleOf),
                AddUnit a => a.Describe(written!, TitleOf),
                ChangeCount n => n.Describe(written!, TitleOf),
                SetDoctrine d => d.Describe(written!, presets),
                _ => c.Describe(written!),
            };
        return s;
    }
}
