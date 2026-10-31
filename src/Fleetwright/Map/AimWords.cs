using System.Globalization;
using Fleetwright.Sim;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Missions;

namespace Fleetwright.Map;

/// <summary>A side's aims in words (docs/battle/aims.md A5): as the scenario gives them (the launch screen, the
/// briefing) and as they stand (the top bar, the result, from the snapshot's <see cref="AimView"/>s). One place, so every
/// screen says an aim the same way.</summary>
static class AimWords
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>An aim as the scenario gives it: "Reach the Dardanelles with 1 of Mittelmeerdivision", "Sink 20,000 t of
    /// merchants in the Bay of Bengal", "Stop Germany".</summary>
    public static string Plan(Scenario sc, ScenarioAim a)
    {
        var aim = a.Aim;
        return aim.Kind switch
        {
            AimKind.Reach => $"Reach {Where(a)} {(aim.Of > 0 ? $"with {aim.Of.ToString(Inv)} of" : "with")} {Units(a)}" +
                             (aim.StayMs > 0 ? $", to stay {aim.StayMs / 60_000.0:0} min" : ""),
            AimKind.Sink => $"Sink {aim.Tonnes.ToString("#,0", Inv)} t of merchants{(a.ZoneName.Length > 0 ? " in " + a.ZoneName : "")}",
            AimKind.Stop => $"Stop {SideName(sc, aim.About)}",
            _ => "Destroy everyone",
        } + Weight(aim);
    }

    /// <summary>An aim as it stands: "Reach the Dardanelles: 0 in, 1 wanted", "Sink merchants: 8,200 of 20,000 t", "Stop Germany:
    /// open", and met or lost once decided.</summary>
    public static string Now(Scenario sc, ScenarioAim a, in AimView v)
    {
        var aim = a.Aim;
        string text = aim.Kind switch
        {
            AimKind.Reach => $"Reach {Where(a)}: {v.Done.ToString("0", Inv)} in, {v.Of.ToString("0", Inv)} wanted",
            AimKind.Sink => $"Sink merchants: {v.Done.ToString("#,0", Inv)} of {v.Of.ToString("#,0", Inv)} t",
            AimKind.Stop => $"Stop {SideName(sc, aim.About)}",
            _ => $"Destroy everyone: {(v.Of > 0 ? v.Done / v.Of : 0).ToString("0%", Inv)} of their tonnage",
        };
        return text + (AimState)v.State switch
        {
            AimState.Met => " · met",
            AimState.Lost => " · lost",
            _ => aim.Kind == AimKind.Stop ? ": open" : "",
        };
    }

    /// <summary>A side's aims as the scenario gives them, joined.</summary>
    public static string Plans(Scenario sc, int side) => string.Join(" · ", sc.AimEntriesOf(side).Select(a => Plan(sc, a)));

    /// <summary>A side's aims as they stand (the snapshot's views, in order), joined; as planned where the snapshot has
    /// none yet.</summary>
    public static string Nows(Scenario sc, Snapshot s, int side)
    {
        var parts = new List<string>();
        int i = 0;
        foreach (var a in sc.AimEntriesOf(side))
            parts.Add(View(s, side, i++) is { } v ? Now(sc, a, v) : Plan(sc, a));
        return string.Join(" · ", parts);
    }

    /// <summary>A side's i-th aim's view in the snapshot, if it has one.</summary>
    public static AimView? View(Snapshot s, int side, int i)
    {
        for (int k = 0; k < s.AimCount; k++)
            if (s.Aims[k].Side == side && i-- == 0)
                return s.Aims[k];
        return null;
    }

    /// <summary>A key that changes when any of a side's aims' figures do (the top bar makes its text again then).</summary>
    public static double Key(Snapshot s, int side)
    {
        double key = 0;
        for (int k = 0; k < s.AimCount; k++)
            if (s.Aims[k].Side == side)
                key = key * 31 + s.Aims[k].State * 1e9 + Math.Round(s.Aims[k].Done) + 1;
        return key;
    }

    /// <summary>Where a zone is, by its name or else the units bound for it.</summary>
    public static string Where(ScenarioAim a) => a.ZoneName.Length > 0 ? a.ZoneName : a.Aim.Kind == AimKind.Reach ? $"{Units(a)}'s zone" : "the hunting ground";

    static string Units(ScenarioAim a) => a.Units.Length == 0 ? "them" : a.Units is ["all"] ? "the side's ships" : string.Join(", ", a.Units);

    static string SideName(Scenario sc, int side) => side >= 0 && side < sc.Sides.Length ? sc.Sides[side] : $"side {side}";

    static string Weight(in Aim aim) => aim.Weight != 1 ? $" (weight {aim.Weight.ToString("0.##", Inv)})" : "";
}
