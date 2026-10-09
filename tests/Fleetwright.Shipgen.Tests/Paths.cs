namespace Fleetwright.Shipgen.Tests;

/// <summary>Where the repository's files are, found from the test assembly up to Fleetwright.slnx.</summary>
static class Paths
{
    public static readonly string Root = FindRoot();

    static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Fleetwright.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Fleetwright.slnx not found above the tests");
    }

    public static string TestData(string name) => Path.Combine(Root, "tests", "Fleetwright.Shipgen.Tests", "Data", name);

    public static string Shipgen(params string[] parts) => Path.Combine([Root, "shipgen", .. parts]);
}
