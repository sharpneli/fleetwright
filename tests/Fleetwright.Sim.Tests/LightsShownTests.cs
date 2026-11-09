using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>A ship's lights shown (docs/battle/night.md Q13): lit at anchor on a moonless night she's made out far beyond
/// the kilometre a darkened ship is; once she's fired upon her captain puts them out.</summary>
public class LightsShownTests(ITestOutputHelper output)
{
    static readonly SimSettings S = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };

    // the outer roads of Port Arthur, 8 February 1904, 23:30 local: the moon not yet up
    const double Lat = 38.75, Lon = 121.30;

    /// <summary>A light cruiser (side 1, entity 1) stopped, and a destroyer (side 2, entity 2) stopped 6 km east of her,
    /// her lights shown or not; both sides' searchlights and star shell off.</summary>
    static (World W, AiSystem Ai) Roads(bool lit)
    {
        var w = new World(WorldTime.FromUtc(1904, 2, 8, 15, 30), 7, S);
        var ai = new AiSystem(w);
        int cl = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        int dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(Command.SetWeather(15_000));
        w.Apply(ShipSystem.Spawn(cl, Lat, Lon, 0, 0, 1));
        w.Apply(ShipSystem.Spawn(dd, Lat, Lon + 0.069, 90, 0, 2));
        if (lit)
            w.Apply(ShipSystem.ShowLights(2, true));
        foreach (int side in new[] { 1, 2 })
        {
            w.Apply(ai.Orders.SideDoctrine(side, DoctrineField.StarShell, (double)StarShellUse.Never));
            w.Apply(ai.Orders.SideDoctrine(side, DoctrineField.Searchlights, (double)SearchlightUse.Never));
        }
        return (w, ai);
    }

    static bool InSight(World w, byte by, int entity) =>
        w.Sightings.Of(by) is { } pic && pic.IndexOf(entity) is int i and >= 0 && pic[i].InSight;

    [Fact]
    public void A_lit_ship_is_made_out_far_off_and_darkens_once_fired_upon()
    {
        bool Seen(bool lit)
        {
            var (w, _) = Roads(lit);
            for (int s = 0; s < 120; s++)
            {
                w.AdvanceTo(w.Time + 1_000);
                if (InSight(w, 1, 2))
                    return true;
            }
            return false;
        }
        bool dark = Seen(false), lit = Seen(true);

        // fired upon, her lights go out
        var (f, ai) = Roads(true);
        f.Apply(ai.Orders.Engage(Unit.Ship(1), 2));
        bool shown = true;
        for (int s = 0; s < 180 && shown; s++)
        {
            f.AdvanceTo(f.Time + 1_000);
            shown = f.Ships.Store[f.Ships.Store.SlotOf(2)].LightsShown;
        }
        output.WriteLine($"at 6 km on a moonless night: darkened seen {dark}, lit seen {lit}; fired upon, lights still shown {shown}");
        Assert.False(dark);
        Assert.True(lit);
        Assert.False(shown);
    }
}
