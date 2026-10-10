using System.Diagnostics;
using Fleetwright.HitView;
using Fleetwright.Shipgen;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;

namespace Fleetwright;

/// <summary>A design built on the <see cref="DesignWorker"/>: the ship and everything the views draw from it, made off
/// the render thread. Immutable once published; the render thread only uploads it.</summary>
public sealed class DesignResult
{
    /// <summary>The request this answers (<see cref="DesignWorker.Submit"/>'s id): later requests have larger ids.</summary>
    public required int Id { get; init; }
    public required Design Design { get; init; }
    public LookInput? Look { get; init; }

    /// <summary>The built ship, or null when the design is invalid or failed to build (<see cref="Errors"/>).</summary>
    public Ship? Ship { get; init; }

    /// <summary>Why there is no ship: validation errors or the exception's message. Empty when there is one.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>The ship as hitbox triangles (the hitbox views).</summary>
    public HitboxMesh? Mesh { get; init; }

    /// <summary>The sprites' layout (sprite.json) and their baked images, mip chains ready to upload.</summary>
    public ShipSprites? Sprites { get; init; }
    public SpriteImages? Images { get; init; }

    /// <summary>Seconds spent per stage (zero for a stage reused from the cache).</summary>
    public double BuildS { get; init; }
    public double MeshS { get; init; }
    public double DrawS { get; init; }
    public double BakeS { get; init; }

    /// <summary>A line on the build: its times, or the first errors.</summary>
    public string Status => Ship != null
        ? $"build {BuildS * 1000:F0} ms, mesh {MeshS * 1000:F0} ms, draw {DrawS * 1000:F0} ms, bake {BakeS * 1000:F0} ms"
        : string.Join("\n", Errors.Take(7));
}

/// <summary>A sprite's baked layers as mip chains: colour premultiplied (so filtering doesn't bleed the transparent
/// border's black in), the height map reduced by max.</summary>
public sealed class SpriteImages
{
    public required Image8[] Hull { get; init; }
    public required Image8[] Height { get; init; }
    public required Dictionary<string, Image8[]> Turrets { get; init; }

    /// <summary>Level 0 and up to <paramref name="levels"/> halvings, while the size stays even.</summary>
    public static Image8[] Chain(Image8 level0, bool height, int levels)
    {
        var chain = new List<Image8> { level0 };
        while (chain.Count <= levels && chain[^1].Width % 2 == 0 && chain[^1].Height % 2 == 0)
            chain.Add(Mips.Half(chain[^1], max: height));
        if (!height)
            foreach (var l in chain)
            {
                var d = l.Data;
                for (int i = 0; i < d.Length; i += 4)
                {
                    int a = d[i + 3];
                    for (int k = 0; k < 3; k++)
                        d[i + k] = (byte)((d[i + k] * a + 127) / 255);
                }
            }
        return chain.ToArray();
    }
}

/// <summary>
/// Builds designs on a thread of its own so no edit stalls the frame: validate, build, the hitbox mesh, the sprites
/// drawn and baked (with command buffers of its own) and their mips. One request waits at a time: <see cref="Submit"/>
/// replaces whatever hasn't started, and a running build finishes first. So the newest request always wins, and a
/// slider dragged across twenty values builds only the few the worker gets to. <see cref="Latest"/> is the newest
/// finished result; callers poll it each frame (one volatile read) and compare ids.
/// </summary>
public sealed class DesignWorker : IDisposable
{
    readonly double scale;
    readonly int mipLevels;
    readonly bool limits;
    readonly Thread thread;
    readonly AutoResetEvent wake = new(false);
    readonly object gate = new();
    (int Id, Design Design, LookInput? Look)? pending;
    int nextId;
    volatile DesignResult? latest;
    volatile bool stopping;
    readonly nint device;
    GpuBaker? baker;   // made on the worker thread, used only there

    // recent results by design and look (undo, redo and reset come back instantly), and built ships by design (a look
    // change only draws again)
    const int CacheSize = 8;
    readonly LinkedList<(string Key, DesignResult Result)> cache = new();

    /// <param name="scale">Sprite pixels per metre at mip level 0.</param>
    /// <param name="mipLevels">Mip levels below level 0.</param>
    /// <param name="limits">Hold designs to the styles' input limits (the designer), or only to what the physics needs
    /// (the viewers, which also show fuzz designs).</param>
    /// <param name="device">The GPU device to bake on (an <c>SDL_GPUDevice*</c>): the game's own, shared across threads
    /// (SDL GPU allows it; only a command buffer must stay on its thread, and the baker's do). Zero: a device of the
    /// worker's own (tests and tools).</param>
    public DesignWorker(double scale, int mipLevels, bool limits, nint device = 0)
    {
        this.device = device;
        this.scale = scale;
        this.mipLevels = mipLevels;
        this.limits = limits;
        thread = new Thread(Run) { Name = "Design worker", IsBackground = true, Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }

    /// <summary>The newest finished result, or null before the first.</summary>
    public DesignResult? Latest => latest;

    /// <summary>The id of the newest request (0 before the first).</summary>
    public int Requested => Volatile.Read(ref nextId);

    /// <summary>A request newer than <see cref="Latest"/> is waiting or building.</summary>
    public bool Busy => Requested > (latest?.Id ?? 0);

    /// <summary>Asks for a design to be built (in a look other than its own, if given); returns the request's id.
    /// Replaces the waiting request, if any.</summary>
    public int Submit(Design design, LookInput? look = null)
    {
        lock (gate)
        {
            int id = ++nextId;
            pending = (id, design, look);
            wake.Set();
            return id;
        }
    }

    /// <summary>Blocks until every request so far has a result (startup and tests), or the timeout passes.</summary>
    public bool WaitIdle(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (Busy)
        {
            if (sw.Elapsed > timeout)
                return false;
            Thread.Sleep(5);
        }
        return true;
    }

    void Run()
    {
        while (true)
        {
            wake.WaitOne();
            while (true)
            {
                if (stopping)
                {
                    baker?.Dispose();
                    return;
                }
                (int Id, Design Design, LookInput? Look) job;
                lock (gate)
                {
                    if (pending is not { } p)
                        break;
                    job = p;
                    pending = null;
                }
                latest = Make(job.Id, job.Design, job.Look);
            }
        }
    }

    DesignResult Make(int id, Design design, LookInput? look)
    {
        string shipKey = design.ToJson();
        string key = shipKey + "\n" + look?.Navy + "/" + look?.Era;
        foreach (var (k, r) in cache)
            if (k == key)
                return Remember(key, Copy(r, id, design, look));

        // a look change of a design built before: reuse its ship and mesh, draw again
        DesignResult? built = null;
        foreach (var (k, r) in cache)
            if (r.Ship != null && k.StartsWith(shipKey + "\n", StringComparison.Ordinal))
                built = r;
        var sw = Stopwatch.StartNew();
        Ship? ship = built?.Ship;
        HitboxMesh? mesh = built?.Mesh;
        double tBuild = 0, tMesh = 0;
        try
        {
            if (ship == null)
            {
                var errs = ShipDesign.Validate(design, limits).Concat(Shipgen.Looks.Validate(design)).ToList();
                if (errs.Count > 0)
                    return Remember(key, new DesignResult { Id = id, Design = design, Look = look, Errors = ["invalid design:", .. errs] });
                ship = ShipDesign.Build(design);
                tBuild = sw.Elapsed.TotalSeconds;
                mesh = HitboxMesh.Build(ship.Hitboxes);
                tMesh = sw.Elapsed.TotalSeconds - tBuild;
            }
            double t0 = sw.Elapsed.TotalSeconds;
            var sprites = ShipSprites.Build(ship, scale, mipLevels, look);
            double tDraw = sw.Elapsed.TotalSeconds - t0;
            baker ??= MakeBaker();
            var baked = ShipBake.Bake(sprites, baker);
            var images = new SpriteImages
            {
                Hull = SpriteImages.Chain(baked.Hull, false, mipLevels),
                Height = SpriteImages.Chain(baked.Height, true, mipLevels),
                Turrets = baked.Turrets.ToDictionary(t => t.Key, t => SpriteImages.Chain(t.Value, false, mipLevels), StringComparer.Ordinal),
            };
            double tBake = sw.Elapsed.TotalSeconds - t0 - tDraw;
            return Remember(key, new DesignResult
            {
                Id = id, Design = design, Look = look, Ship = ship, Mesh = mesh, Sprites = sprites, Images = images,
                BuildS = tBuild, MeshS = tMesh, DrawS = tDraw, BakeS = tBake,
            });
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"design worker: {design.Id}: {e}");
            return Remember(key, new DesignResult { Id = id, Design = design, Look = look, Errors = [$"{e.GetType().Name}: {e.Message}"] });
        }
    }

    unsafe GpuBaker MakeBaker() => device != 0 ? new GpuBaker((SDL.SDL_GPUDevice*)device) : new GpuBaker();

    static DesignResult Copy(DesignResult r, int id, Design design, LookInput? look) => new()
    {
        Id = id, Design = design, Look = look, Ship = r.Ship, Errors = r.Errors, Mesh = r.Mesh, Sprites = r.Sprites,
        Images = r.Images,
    };

    DesignResult Remember(string key, DesignResult r)
    {
        for (var n = cache.First; n != null; n = n.Next)
            if (n.Value.Key == key)
            {
                cache.Remove(n);
                break;
            }
        cache.AddFirst((key, r));
        while (cache.Count > CacheSize)
            cache.RemoveLast();
        return r;
    }

    public void Dispose()
    {
        stopping = true;
        wake.Set();
        thread.Join();
        wake.Dispose();
    }
}
