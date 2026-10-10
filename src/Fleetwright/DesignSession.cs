using Fleetwright.Shipgen;

namespace Fleetwright;

/// <summary>
/// The design being looked at, shared by the viewer scenes: the designs in a folder, which one is picked, and its
/// newest <see cref="DesignResult"/> from a <see cref="DesignWorker"/>. Scenes watch <see cref="Version"/> and take
/// the new result when it moves, so switching scenes keeps the same ship and no scene knows another. Picking a design
/// returns at once; the result lands a few frames later, and the last one stays until then.
/// </summary>
public sealed class DesignSession : IDisposable
{
    readonly List<string> designs;
    readonly DesignWorker worker;
    Design? design;
    string? loadError;

    /// <summary>The designs' file names without extension, in the order of <see cref="Index"/>.</summary>
    public string[] Names { get; }

    /// <summary>The picked design.</summary>
    public int Index { get; private set; }

    public string Path => designs[Index];

    /// <summary>The look the sprites are drawn in, other than the design's own (null: its own).</summary>
    public LookInput? Look { get; private set; }

    /// <summary>The newest finished build (null before the first).</summary>
    public DesignResult? Result => worker.Latest;

    /// <summary>The built ship, or null when the design is invalid or failed to build (see <see cref="Status"/>).</summary>
    public Ship? Ship => Result?.Ship;

    /// <summary>Moves whenever a new result lands, whether it gave a ship or not.</summary>
    public int Version => Result?.Id ?? 0;

    /// <summary>A build is waiting or running.</summary>
    public bool Busy => worker.Busy;

    /// <summary>A line or two on the last build: its times, or why there is no ship.</summary>
    public string Status => loadError ?? (Busy ? "building..." : Result?.Status ?? "");

    /// <summary>The designs next to <paramref name="designPath"/>, with it picked and built (this one waits for the
    /// build, so the first frame has the ship).</summary>
    public DesignSession(string designPath, double spriteScale, int mipLevels, nint device = 0)
    {
        var full = System.IO.Path.GetFullPath(designPath);
        designs = Directory.GetFiles(System.IO.Path.GetDirectoryName(full)!, "*.json").Order(StringComparer.Ordinal).ToList();
        if (designs.Count == 0)
            designs.Add(designPath);
        Names = designs.Select(p => System.IO.Path.GetFileNameWithoutExtension(p)).ToArray();
        Index = Math.Max(0, designs.FindIndex(p => System.IO.Path.GetFullPath(p) == full));
        worker = new DesignWorker(spriteScale, mipLevels, limits: false, device);
        Rebuild();
        WaitIdle();
    }

    /// <summary>Picks a design and builds it (nothing happens if it is already picked).</summary>
    public void Select(int index)
    {
        if (index == Index || index < 0 || index >= designs.Count)
            return;
        Index = index;
        Rebuild();
    }

    /// <summary>Draws the sprites in another look (null: the design's own).</summary>
    public void SetLook(LookInput? look)
    {
        Look = look;
        if (design != null)
            worker.Submit(design, Look);
    }

    /// <summary>Loads the picked design again (it may have changed on disk) and builds it.</summary>
    public void Rebuild()
    {
        try
        {
            design = Design.Load(designs[Index]);
            loadError = null;
            worker.Submit(design, Look);
        }
        catch (Exception e)
        {
            design = null;
            loadError = $"{Names[Index]}: {e.GetType().Name}: {e.Message}";
            Console.Error.WriteLine($"design {loadError}");
        }
    }

    /// <summary>Blocks until the newest request has its result (startup, captures).</summary>
    public bool WaitIdle() => worker.WaitIdle(TimeSpan.FromMinutes(1));

    public void Dispose() => worker.Dispose();
}
