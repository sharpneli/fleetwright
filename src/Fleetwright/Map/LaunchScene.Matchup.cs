using System.Globalization;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Missions;

namespace Fleetwright.Map;

/// <summary>The design picker's matchup (docs/map-ui/launch.md "What the sim needs"): each candidate judged as the
/// commander judges a fight (<see cref="Matchup.Judge"/>, docs/battle/command.md 5.1g), as many of her as the row's unit
/// has against the enemy unit that line would meet: of the enemy's units with ships of their own, the one whose ships
/// are nearest the row's in size (by displacement, as a ratio), so the whole column is judged against one line. Each
/// side's range share is its doctrine's, the light the start's at the scenario's place. 1 is even; more is better for
/// her. Judged once a class's figures are in, kept by class.</summary>
public sealed unsafe partial class LaunchScene
{
    ScenarioUnit? foe;
    bool noFoe;
    MatchupEntry[] foeEntries = [];
    readonly MatchupEntry[] own = new MatchupEntry[1];
    readonly Dictionary<int, (double Value, string Text)> judged = [];
    double shareOwn, shareFoe, sightM;
    string matchupLine = "", matchupLabel = "";

    void ResetMatchup()
    {
        (foe, noFoe, foeEntries, matchupLine, matchupLabel) = (null, false, [], "", "");
        judged.Clear();
    }

    /// <summary>The enemy line found, once the classes it's chosen by are in; false until then (or with no enemy).</summary>
    bool FoeReady(ShipLine row)
    {
        if (foe != null)
            return true;
        if (noFoe || current is not { } s || FiguresOf(ClassOf(row.Design)) is not { } mine)
            return false;
        double size = mine.Profile.DisplacementT, bestD = double.PositiveInfinity;
        ScenarioUnit? best = null;
        bool waiting = false;
        void Walk(ScenarioUnit u)
        {
            foreach (var sub in u.Units)
                Walk(sub);
            if (u.Side == row.Side || u.Ships.Count == 0)
                return;
            double tonnes = 0;
            int n = 0;
            foreach (var sh in u.Ships)
            {
                if (FiguresOf(ClassOf(sh.Design)) is not { } f)
                {
                    waiting = true;
                    return;
                }
                tonnes += f.Profile.DisplacementT * sh.Count;
                n += sh.Count;
            }
            double d = n > 0 ? Math.Abs(Math.Log(tonnes / n / size)) : double.PositiveInfinity;
            if (d < bestD)
                (best, bestD) = (u, d);
        }
        foreach (var u in s.Units)
            Walk(u);
        if (waiting)
            return false;
        if (best == null)
        {
            noFoe = true;
            matchupLine = "Matchup: no enemy to judge her against";
            return false;
        }
        foe = best;
        foeEntries = [.. best.Ships.Select(sh => new MatchupEntry(FiguresOf(ClassOf(sh.Design))!.Profile, sh.Count))];
        shareOwn = ScenarioLoader.SideDoctrine(s, row.Side, DoctrineField.RangeShare);
        shareFoe = ScenarioLoader.SideDoctrine(s, best.Side, DoctrineField.RangeShare);
        var (lat, lon) = Centre(s);
        sightM = SightModel.LightReachM(NightLight.At(Earth.Position(lat, lon), s.Start, WeatherOf(s).Cloud, world.Settings).SkyCdM2, world.Settings);
        string ships = string.Join(", ", best.Ships.Select(sh => $"{sh.Count} {TitleOf(sh.Design)}"));
        matchupLine = $"Matchup: {unitCount} of her v {best.Name} ({ships}); 1 = even";
        matchupLabel = $"Matchup v {best.Name}";
        return true;
    }

    /// <summary>A class's judgement against the enemy line, with its figure; null until both are in.</summary>
    (double Value, string Text)? MatchupOf(ShipLine row, int id)
    {
        if (id < 0 || !FoeReady(row))
            return null;
        if (judged.TryGetValue(id, out var j))
            return j;
        if (FiguresOf(id) is not { } f)
            return null;
        own[0] = new MatchupEntry(f.Profile, Math.Max(unitCount, 1));
        double v = Matchup.Judge(own, foeEntries, shareOwn, shareFoe, world.Settings, sightM).Confidence;
        judged[id] = j = (v, v.ToString(v < 10 ? "0.00" : v < 100 ? "0.0" : "0", CultureInfo.InvariantCulture));
        return j;
    }
}
