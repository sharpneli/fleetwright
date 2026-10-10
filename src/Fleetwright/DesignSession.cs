using Fleetwright.Shipgen;

namespace Fleetwright;

/// <summary>
/// The one design every scene looks at: the designs in a folder, which one is picked, the design as it is now (picked
/// from a file, or edited in the designer), the look it's drawn in, and its newest <see cref="DesignResult"/> from the
/// shared <see cref="DesignWorker"/>. Scenes watch <see cref="Version"/> for new results and the designer watches
/// <see cref="DesignVersion"/> for designs changed elsewhere, so the ship viewer, the hitbox viewer and the designer
/// always show the same ship and no scene knows another. Changes return at once; the result lands a few frames later,
/// and the last one stays until then.
/// </summary>
public sealed class DesignSession : IDisposable
{
    readonly List<string> designs;
    string? loadError;
    bool limits;   // the current design's requests keep to the style limits (an edit) or not (a file, maybe fuzz)

    /// <summary>The designs' file names without extension, in the order of <see cref="Index"/>.</summary>
    public string[] Names { get; }

    /// <summary>The picked file (the last one, after an edit or a design from elsewhere).</summary>
    public int Index { get; private set; }

    public string Path => designs[Index];

    /// <summary>The design now, and the file it came from (null: new or from outside the folder).</summary>
    public Design? Design { get; private set; }
    public string? DesignPath { get; private set; }

    /// <summary>The design differs from its file: edited in the designer.</summary>
    public bool Edited { get; private set; }

    /// <summary>Moves whenever <see cref="Design"/> changes.</summary>
    public int DesignVersion { get; private set; }

    /// <summary>The look the sprites are drawn in, other than the design's own (null: its own).</summary>
    public LookInput? Look { get; private set; }

    /// <summary>The builder every scene shares.</summary>
    public DesignWorker Worker { get; }

    /// <summary>The newest finished build (null before the first).</summary>
    public DesignResult? Result => Worker.Latest;

    /// <summary>The built ship, or null when the design is invalid or failed to build (see <see cref="Status"/>).</summary>
    public Ship? Ship => Result?.Ship;

    /// <summary>Moves whenever a new result lands, whether it gave a ship or not.</summary>
    public int Version => Result?.Id ?? 0;

    /// <summary>A build is waiting or running.</summary>
    public bool Busy => Worker.Busy;

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
        Worker = new DesignWorker(spriteScale, mipLevels, limits: false, device);
        Rebuild();
        WaitIdle();
    }

    /// <summary>Picks a design and builds it (nothing happens if it is already picked and unedited).</summary>
    public void Select(int index)
    {
        if ((index == Index && !Edited) || index < 0 || index >= designs.Count)
            return;
        Index = index;
        Rebuild();
    }

    /// <summary>Draws the sprites in another look (null: the design's own).</summary>
    public void SetLook(LookInput? look)
    {
        Look = look;
        if (Design != null)
            Worker.Submit(Design, Look, limits);
    }

    /// <summary>Loads the picked file again (it may have changed on disk; an edit is dropped) and builds it.</summary>
    public void Rebuild()
    {
        try
        {
            Set(Design.Load(designs[Index]), designs[Index], edited: false, limits: false);
            loadError = null;
        }
        catch (Exception e)
        {
            loadError = $"{Names[Index]}: {e.GetType().Name}: {e.Message}";
            Console.Error.WriteLine($"design {loadError}");
        }
    }

    /// <summary>An edit (the designer): the design now, built against the style limits.</summary>
    public void Edit(Design design) => Set(design, DesignPath, edited: true, limits: true);

    /// <summary>A design from elsewhere (the designer's New or Open): picked as the file it came from, if it's in the
    /// folder.</summary>
    public void Load(Design design, string? path)
    {
        if (path != null)
        {
            int i = designs.FindIndex(p => System.IO.Path.GetFullPath(p) == System.IO.Path.GetFullPath(path));
            if (i >= 0)
                Index = i;
        }
        Set(design, path, edited: path == null, limits: true);
    }

    void Set(Design design, string? path, bool edited, bool limits)
    {
        Design = design;
        DesignPath = path;
        Edited = edited;
        this.limits = limits;
        DesignVersion++;
        Worker.Submit(design, Look, limits);
    }

    /// <summary>Blocks until the newest request has its result (startup, captures).</summary>
    public bool WaitIdle() => Worker.WaitIdle(TimeSpan.FromMinutes(1));

    public void Dispose() => Worker.Dispose();
}
