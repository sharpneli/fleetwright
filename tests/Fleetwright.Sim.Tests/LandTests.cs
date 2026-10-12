using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>The land files against places whose answer nobody doubts, at every level of detail.</summary>
public class LandTests
{
    static readonly Dictionary<string, Land> Levels = new[] { "110m", "50m", "10m" }.ToDictionary(r => r,
        r => Land.Load(Path.Combine(Repo(), "Content", "World", $"land-{r}.bin")));

    static string Repo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Fleetwright.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("no Fleetwright.slnx above the tests");
    }

    public static TheoryData<string> Resolutions => ["110m", "50m", "10m"];

    // name, latitude, longitude, land?
    static readonly (string Name, double Lat, double Lon, bool Land)[] Places =
    [
        ("Paris", 48.86, 2.35, true),
        ("Moscow", 55.75, 37.62, true),
        ("the Sahara", 23, 13, true),
        ("the Great Plains", 40, -100, true),
        ("Brasília", -15.8, -47.9, true),
        ("by the South Pole", -89.9, 45, true),   // the pole itself is a vertex of the data's ring
        ("Australia's middle", -25, 134, true),
        ("the North Sea, Jutland bank", 56.7, 5.8, false),
        ("the mid-Atlantic", 30, -40, false),
        ("the Pacific", 0, -150, false),
        ("the North Pole", 90, 0, false),
        ("the Indian Ocean", -20, 80, false),
        ("the Caspian", 42, 51, false),
        ("the Mediterranean", 35, 18, false),
    ];

    [Theory]
    [MemberData(nameof(Resolutions))]
    public void Knows_land_from_sea(string res)
    {
        var land = Levels[res];
        foreach (var (name, lat, lon, want) in Places)
            Assert.True(land.IsLand(Earth.Up(lat, lon)) == want, $"{res}: {name} should be {(want ? "land" : "sea")}");
    }

    [Fact]
    public void Finer_levels_have_more_coast()
    {
        Assert.True(Levels["110m"].PointCount < Levels["50m"].PointCount);
        Assert.True(Levels["50m"].PointCount < Levels["10m"].PointCount);
        // Scapa Flow's islands and the Jade only show at the finest level
        Assert.True(Levels["10m"].IsLand(Earth.Up(58.95, -3.10)));   // Hoy, Orkney
    }
}
