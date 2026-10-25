using System.Globalization;
using System.Numerics;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The launch screen's conditions (docs/map-ui/launch.md L5, the mockup's third column): both sides side by side
/// (yours first) in what decides how well they fight, not what they are: gun crews, readiness, damage control, fire
/// discipline, fuel; four presets over them (as history, even crews, easier, harder: first guesses, decision 5); the day
/// (visibility, wind, the end, the seed, and the aim it's fought for); then the changes from the scenario. Each is a
/// change of the setup (<see cref="SideSetting"/>, <see cref="SetWeather"/>, <see cref="SetEnd"/>, <see cref="SetSeed"/>),
/// gone when it's put back as written. Its lines are made when the pick changes.</summary>
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
        public string Vis = "", WindKn = "", Ends = "", SeedFile = "", SeedNow = "";
        public bool WeatherChanged, EndChanged, NewSeed;
        public int WindPoint;
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
        v.OwnHead = s.Sides[v.Own] + (side >= 0 ? " · you" : "");
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
        v.Weather = WeatherOf(s);
        v.WrittenWeather = WeatherOf(written!);
        v.WeatherChanged = setup.Changes.Any(c => c is SetWeather);
        v.Vis = $"{v.Weather.VisibilityM / 1000:0} km";
        v.WindPoint = (int)Math.Round(((v.Weather.WindFromDeg % 360) + 360) % 360 / 22.5) % 16;
        v.WindKn = $"{v.Weather.WindMs / 0.514444:0} kn";
        var (_, lon) = Centre(s);
        v.Ends = EndOf(s).ToUtc().AddHours(lon / 15).ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
        v.EndChanged = setup.Changes.Any(c => c is SetEnd);
        v.NewSeed = setup.Changes.Any(c => c is SetSeed { Seed: null });
        v.SeedFile = $"{written!.Seed}, the file's";
        v.SeedNow = v.NewSeed ? $"this time {s.Seed}" : setup.Changes.OfType<SetSeed>().FirstOrDefault() is { Seed: { } n } ? $"{n}" : "";
        return v;
    }

    /// <summary>The third column: the conditions side by side, the presets, the day, then the changes; scrolling.</summary>
    void ConditionsColumn(Vector2 at, float w, float bottom)
    {
        if (current is not { } s)
            return;
        var v = Conditions(s);
        ImGui.SetCursorScreenPos(at);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        if (ImGui.BeginChild("##conditions", new Vector2(w + 10 * sc, bottom - at.Y - 8 * sc), ImGuiChildFlags.None, ImGuiWindowFlags.None))
        {
            var keep = dl;
            dl = ImGui.GetWindowDrawList();
            var o = ImGui.GetCursorScreenPos();
            float x = o.X, y = o.Y;
            w -= 6 * sc;
            Tracked(UiFonts.Sans, 10.5f, new Vector2(x, y), Muted, "CONDITIONS", 0.9f);
            const string Sub = "the difficulty, side by side";
            Txt(UiFonts.SansSmall, 11, new Vector2(x + w - Width(UiFonts.SansSmall, 11, Sub), y), Muted, Sub);
            y += 22 * sc;
            // the presets
            float px = x;
            for (int k = 0; k < PresetNames.Length; k++)
            {
                float pw = Width(UiFonts.SansSmall, 11.5f, PresetNames[k]) + 18 * sc;
                if (Button($"##preset{k}", PresetNames[k], new Vector2(px, y), new Vector2(pw, 22 * sc), enabled: !begin, px: 11.5f))
                    pendingEdit = Preset(k, v.Own, v.Other);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(PresetTips[k]);
                px += pw + 5 * sc;
            }
            y += 34 * sc;
            // the sides side by side
            float k0 = 118 * sc, cw = (w - k0 - 8 * sc) / 2;
            for (int c = 0; c < 2; c++)
            {
                float hx = x + k0 + c * (cw + 8 * sc);
                Ensign(c == 0 ? v.OwnEnsign : v.OtherEnsign, new Vector2(hx, y + 2 * sc), new Vector2(16 * sc, 10 * sc));
                Txt(UiFonts.SansSmall, 11.5f, new Vector2(hx + 22 * sc, y - 1 * sc), Bright, c == 0 ? v.OwnHead : v.OtherHead);
            }
            y += 20 * sc;
            for (int k = 0; k < Kinds.Length; k++)
            {
                Txt(UiFonts.SansSmall, 12, new Vector2(x, y + 3 * sc), Soft, KindLabels[k]);
                for (int c = 0; c < 2; c++)
                {
                    var opts = OptionsOf(Kinds[k]);
                    int picked = Choice($"##cond{k}{c}", new Vector2(x + k0 + c * (cw + 8 * sc), y), cw, opts, v.Now[k, c], v.Changed[k, c]);
                    if (picked >= 0 && opts[picked] != v.Now[k, c] && !begin)
                        pendingEdit = Setting(setup, Kinds[k], c == 0 ? v.Own : v.Other, opts[picked]);
                }
                y += 27 * sc;
            }

            // the day
            y += 12 * sc;
            Tracked(UiFonts.Sans, 10.5f, new Vector2(x, y), Muted, "THE DAY", 0.9f);
            y += 24 * sc;
            Txt(UiFonts.SansSmall, 12, new Vector2(x, y + 3 * sc), Soft, "Visibility");
            if (Slider("vis", new Vector2(x + k0, y + 1 * sc), w - k0 - 60 * sc, Math.Round(v.Weather.VisibilityM / 1000), Math.Round(v.WrittenWeather.VisibilityM / 1000),
                    big: false, out double km, min: 1, max: 30, step: 1) && !begin)
                pendingEdit = Weather(v.Weather with { VisibilityM = km * 1000 });
            string vis = dragId == "vis" ? $"{dragValue:0} km" : v.Vis;
            Txt(UiFonts.Mono, 12, new Vector2(x + w - Width(UiFonts.Mono, 12, vis), y + 3 * sc), v.WeatherChanged || dragId == "vis" ? Brass : Bright, vis);
            y += 28 * sc;
            Txt(UiFonts.SansSmall, 12, new Vector2(x, y + 3 * sc), Soft, "Wind");
            int point = Choice("##windfrom", new Vector2(x + k0, y), 74 * sc, SetWeather.Points, SetWeather.Points[v.WindPoint], v.WeatherChanged);
            if (point >= 0 && point != v.WindPoint && !begin)
                pendingEdit = Weather(v.Weather with { WindFromDeg = point * 22.5 });
            ImGui.PushID("wind");
            float sw = Stepper(new Vector2(x + k0 + 84 * sc, y + 2 * sc), v.WindKn, v.Weather.WindMs > 0.01, v.Weather.WindMs / 0.514444 < 60, out int by);
            ImGui.PopID();
            if (by != 0 && !begin)
                pendingEdit = Weather(v.Weather with { WindMs = Math.Max(0, Math.Round(v.Weather.WindMs / 0.514444) + 2 * by) * 0.514444 });
            Txt(UiFonts.SansSmall, 11.5f, new Vector2(x + k0 + 94 * sc + sw, y + 3 * sc), Muted, "from, and how hard");
            y += 28 * sc;
            Txt(UiFonts.SansSmall, 12, new Vector2(x, y + 3 * sc), Soft, "Ends");
            var end = EndOf(s);
            ImGui.PushID("ends");
            Stepper(new Vector2(x + k0, y + 2 * sc), v.Ends, end.Ms - 3_600_000L > s.Start.Ms, end.Ms < s.Start.Ms + 72 * 3_600_000L, out int hours);
            ImGui.PopID();
            if (hours != 0 && !begin)
            {
                var to = end + hours * 3_600_000L;
                pendingEdit = written!.Until is { } wu && wu.Ms == to.Ms || written.Until == null && to.Ms == EndOf(written).Ms ? setup.Without("until") : setup.With(new SetEnd(to));
            }
            if (v.EndChanged)
                Txt(UiFonts.SansSmall, 11.5f, new Vector2(x + k0 + 150 * sc, y + 3 * sc), Brass, "changed");
            Txt(UiFonts.SansSmall, 11.5f, new Vector2(x + k0 + (v.EndChanged ? 206 : 150) * sc, y + 3 * sc), Muted, "local, at the latest");
            y += 28 * sc;
            Txt(UiFonts.SansSmall, 12, new Vector2(x, y + 3 * sc), Soft, "Seed");
            float bx = x + k0;
            for (int k = 0; k < 2; k++)
            {
                string word = k == 0 ? v.SeedFile : "new each time";
                float bw = Width(UiFonts.SansSmall, 11.5f, word) + 16 * sc;
                var a0 = new Vector2(bx, y);
                bool on = k == 1 == v.NewSeed;
                ImGui.SetCursorScreenPos(a0);
                if (ImGui.InvisibleButton($"##seed{k}", new Vector2(bw, 21 * sc)) && !on && !begin)
                    pendingEdit = k == 1 ? setup.With(new SetSeed(null)) : setup.Without("seed");
                bool hover = ImGui.IsItemHovered();
                if (on)
                    dl.AddRectFilled(a0, a0 + new Vector2(bw, 21 * sc), U(Hex(0x2A2412)));
                else if (hover)
                    dl.AddRectFilled(a0, a0 + new Vector2(bw, 21 * sc), U(new Vector4(1, 1, 1, 0.05f)));
                Txt(UiFonts.SansSmall, 11.5f, a0 + new Vector2(8 * sc, 3 * sc), on ? (k == 1 ? Brass : Bright) : Muted, word);
                if (k > 0)
                    VLine(bx, a0.Y, a0.Y + 21 * sc);
                bx += bw;
            }
            dl.AddRect(new Vector2(x + k0, y), new Vector2(bx, y + 21 * sc), U(EdgeC), 2 * sc);
            if (v.SeedNow.Length > 0)
                Txt(UiFonts.SansSmall, 11.5f, new Vector2(bx + 10 * sc, y + 3 * sc), Muted, v.SeedNow);
            y += 30 * sc;
            foreach (var (label, value) in aims)
            {
                Txt(UiFonts.SansSmall, 12, new Vector2(x, y), Soft, label);
                y += Wrapped(UiFonts.SansSmall, 12, new Vector2(x + k0, y), Bright, value, w - k0) + 6 * sc;
            }
            y += 16 * sc;
            y = Changes(new Vector2(x, y), w);
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            ImGui.Dummy(new Vector2(1, 4 * sc));
            dl = keep;
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    /// <summary>A drop-down of the mockup's (the value in brass when it isn't as written): the option picked, else -1.</summary>
    int Choice(string id, Vector2 at, float w, string[] options, string now, bool changed)
    {
        int picked = -1;
        PushFields();
        ImGui.SetCursorScreenPos(at);
        ImGui.SetNextItemWidth(w);
        if (changed)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Brass);
            ImGui.PushStyleColor(ImGuiCol.Border, BrassDark);
        }
        bool open = ImGui.BeginCombo(id, now, ImGuiComboFlags.HeightLarge);
        if (changed)
            ImGui.PopStyleColor(2);
        if (open)
        {
            for (int i = 0; i < options.Length; i++)
                if (ImGui.Selectable(options[i], options[i] == now))
                    picked = i;
            ImGui.EndCombo();
        }
        PopFields();
        return picked;
    }
}
