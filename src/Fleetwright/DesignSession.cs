using System.Diagnostics;
using Fleetwright.Shipgen;

namespace Fleetwright;

/// <summary>
/// The design being looked at, shared by the scenes that show it: the designs in a folder, which one is picked, and
/// the built <see cref="Ship"/>. Scenes watch <see cref="Version"/> and rebuild their own data when it moves, so
/// switching scenes keeps the same ship and no scene knows another. Building is synchronous (well under a second).
/// </summary>
public sealed class DesignSession
{
    readonly List<string> designs;

    /// <summary>The designs' file names without extension, in the order of <see cref="Index"/>.</summary>
    public string[] Names { get; }

    /// <summary>The picked design.</summary>
    public int Index { get; private set; }

    public string Path => designs[Index];

    /// <summary>The built ship, or null when the design is invalid or failed to build (see <see cref="Status"/>).</summary>
    public Ship? Ship { get; private set; }

    /// <summary>A line or two on the last build: its time, or why there is no ship.</summary>
    public string Status { get; private set; } = "";

    /// <summary>Moves on every build, whether it gave a ship or not.</summary>
    public int Version { get; private set; }

    /// <summary>The designs next to <paramref name="designPath"/>, with it picked and built.</summary>
    public DesignSession(string designPath)
    {
        var full = System.IO.Path.GetFullPath(designPath);
        designs = Directory.GetFiles(System.IO.Path.GetDirectoryName(full)!, "*.json").Order(StringComparer.Ordinal).ToList();
        if (designs.Count == 0)
            designs.Add(designPath);
        Names = designs.Select(p => System.IO.Path.GetFileNameWithoutExtension(p)).ToArray();
        Index = Math.Max(0, designs.FindIndex(p => System.IO.Path.GetFullPath(p) == full));
        Rebuild();
    }

    /// <summary>Picks a design and builds it (nothing happens if it is already picked).</summary>
    public void Select(int index)
    {
        if (index == Index || index < 0 || index >= designs.Count)
            return;
        Index = index;
        Rebuild();
    }

    /// <summary>Loads, validates and builds the picked design again (it may have changed on disk).</summary>
    public void Rebuild()
    {
        var sw = Stopwatch.StartNew();
        Ship = null;
        try
        {
            var design = Design.Load(designs[Index]);
            var errs = ShipDesign.Validate(design, limits: false).Concat(Shipgen.Looks.Validate(design)).ToList();
            if (errs.Count > 0)
                Status = "invalid design:\n" + string.Join("\n", errs.Take(6));
            else
            {
                Ship = ShipDesign.Build(design);
                Status = $"build {sw.Elapsed.TotalSeconds:F2} s";
            }
        }
        catch (Exception e)
        {
            Status = $"{e.GetType().Name}: {e.Message}";
            Console.Error.WriteLine($"design {Names[Index]}: {Status}\n{e.StackTrace}");
        }
        Version++;
    }
}
