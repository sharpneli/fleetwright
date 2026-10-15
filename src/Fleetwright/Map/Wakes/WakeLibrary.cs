using System.Collections.Concurrent;
using Fleetwright.Sim.Ships;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map.Wakes;

/// <summary>One bake as a stamp reads it: its texture, its crop, the stem it was baked for, its speed and bow wave.</summary>
public readonly record struct WakeBakeRef(nint Texture, double X0, double X1, double HalfY, double StemX, double SpeedMs, double Zb);

/// <summary>The wake bakes on the GPU (docs/visuals/wakes.md K2): shared by hull size and speed, not by class. Speeds go
/// in buckets <see cref="BucketRatio"/> apart; a stamp draws any speed from the two nearest (K3 lines their crests up).
/// A request is met by any bake whose hull is within <see cref="MatchLengthM"/>, <see cref="MatchBeamM"/> and
/// <see cref="MatchDraughtM"/> of the ship's, at that bucket; a bucket not baked yet is stood in for by the nearest one
/// that is, and baked on one of <see cref="WorkerCount"/> threads (one bake each, nearest ships first). Finished bakes
/// go up a few a frame into a pool of <see cref="PoolSize"/> textures (RG16F, the starboard half, mipped on the
/// worker), the least recently used out. Nothing here allocates per frame once the request list has grown.</summary>
public sealed unsafe class WakeLibrary : IDisposable
{
    public const double KnotMs = 0.514444;

    /// <summary>The first bucket's speed, knots, and the ratio between buckets.</summary>
    public const double FirstBucketKn = 3, BucketRatio = 1.08;

    /// <summary>How close a bake's hull must be to a ship's to stand for it, metres (knobs, set by eye).</summary>
    public static double MatchLengthM = 3, MatchBeamM = 0.6, MatchDraughtM = 0.3;

    public const int PoolSize = 32, WorkerCount = 2;
    const int MaxInFlight = 2 * WorkerCount, UploadsPerFrame = 2;

    public static double BucketSpeedMs(int bucket) => FirstBucketKn * KnotMs * Math.Pow(BucketRatio, bucket);

    /// <summary>The bucket at or under a speed, and how far toward the next it is (0 under the first).</summary>
    public static (int Bucket, double Toward) BucketOf(double speedMs)
    {
        double f = Math.Log(Math.Max(speedMs, 1e-3) / (FirstBucketKn * KnotMs)) / Math.Log(BucketRatio);
        if (f <= 0)
            return (0, 0);
        int b = (int)f;
        return (b, f - b);
    }

    enum State : byte { Free, Baking, Baked, Ready }

    sealed class Slot
    {
        public State State;
        public double L, B, T;
        public int Bucket;
        public long Used;
        public SDL_GPUTexture* Texture;
        public readonly byte[] Packed = new byte[WakeField.PackedBytes];
        // written by the worker before it hands the slot back
        public double X0, X1, HalfY, StemX, SpeedMs, Zb;
    }

    readonly record struct Job(int Slot, WakeHull Hull, double SpeedMs);

    struct Asked
    {
        public WakeHull Hull;
        public int Bucket;
        public double Priority;
    }

    readonly SDL_GPUDevice* device;
    readonly Slot[] slots = new Slot[PoolSize];
    readonly Dictionary<int, WakeHull?> hulls = [];
    readonly BlockingCollection<Job> jobs = new(new ConcurrentQueue<Job>());
    readonly ConcurrentQueue<int> done = new();
    readonly Thread[] workers = new Thread[WorkerCount];
    Asked[] requests = new Asked[64];
    int requestCount, inFlight;
    long frame;
    SDL_GPUTransferBuffer* transfer;

    /// <summary>How many bakes have been made, and the last one's time (the stats).</summary>
    public int Baked { get; private set; }
    public double LastBakeMs { get; private set; }

    public WakeLibrary(SDL_GPUDevice* device)
    {
        this.device = device;
        for (int i = 0; i < PoolSize; i++)
            slots[i] = new Slot();
        for (int i = 0; i < WorkerCount; i++)
        {
            workers[i] = new Thread(Work) { IsBackground = true, Name = $"Wake bake {i}", Priority = ThreadPriority.BelowNormal };
            workers[i].Start();
        }
    }

    void Work()
    {
        var scratch = new WakeBake.Scratch();
        var field = new WakeField();
        var pack = new float[2 * WakeField.Width * WakeField.Height];
        foreach (var job in jobs.GetConsumingEnumerable())
        {
            var s = slots[job.Slot];
            WakeBake.Bake(job.Hull, job.SpeedMs, scratch, field);
            field.Pack(s.Packed, pack);
            (s.X0, s.X1, s.HalfY, s.StemX, s.SpeedMs, s.Zb) = (field.X0, field.X1, field.HalfY, field.StemX, field.SpeedMs, field.Zb);
            LastBakeMs = field.BakeMs;
            done.Enqueue(job.Slot);
        }
    }

    /// <summary>A class's hull for the bakes, made the first time it's asked for (null without a class).</summary>
    public WakeHull? HullOf(int classId, ShipClass? c)
    {
        if (hulls.TryGetValue(classId, out var h))
            return h;
        if (c == null)
            return null;
        return hulls[classId] = WakeHull.Of(c);
    }

    bool Matches(Slot s, WakeHull h) =>
        Math.Abs(s.L - h.LengthM) <= MatchLengthM && Math.Abs(s.B - h.BeamM) <= MatchBeamM && Math.Abs(s.T - h.DraughtM) <= MatchDraughtM;

    /// <summary>The bakes a ship at <paramref name="speedMs"/> draws from: the two buckets round its speed, each the exact
    /// bake if it's up or the nearest one up for that hull; the weight of the first (the second's is 1 minus it).
    /// Buckets not baked are asked for at <paramref name="priority"/> (lower first). False while none is up.</summary>
    public bool Resolve(WakeHull h, double speedMs, double priority, out WakeBakeRef a, out WakeBakeRef b, out double weightA)
    {
        var (b0, toward) = BucketOf(speedMs);
        int sa = -1, sb = -1, da = int.MaxValue, db = int.MaxValue;
        bool haveA = false, haveB = false;
        for (int i = 0; i < PoolSize; i++)
        {
            var s = slots[i];
            if (s.State == State.Free || !Matches(s, h))
                continue;
            haveA |= s.Bucket == b0;
            haveB |= s.Bucket == b0 + 1;
            if (s.State != State.Ready)
                continue;
            int ea = Math.Abs(s.Bucket - b0), eb = Math.Abs(s.Bucket - (b0 + 1));
            if (ea < da)
                (sa, da) = (i, ea);
            if (eb < db)
                (sb, db) = (i, eb);
        }
        if (!haveA)
            Request(h, b0, priority);
        if (!haveB && toward > 0)
            Request(h, b0 + 1, priority + 0.5);
        a = b = default;
        weightA = 1;
        if (sa < 0)
            return false;
        slots[sa].Used = slots[sb].Used = frame;
        a = Ref(slots[sa]);
        b = Ref(slots[sb]);
        weightA = sa == sb ? 1 : 1 - toward;
        return true;
    }

    static WakeBakeRef Ref(Slot s) => new((nint)s.Texture, s.X0, s.X1, s.HalfY, s.StemX, s.SpeedMs, s.Zb);

    /// <summary>Asks for a bucket ahead of need (the ordered speed's, while a ship works up to it), after the ones drawn.</summary>
    public void Prefetch(WakeHull h, double speedMs, double priority)
    {
        var (b0, _) = BucketOf(speedMs);
        for (int i = 0; i < PoolSize; i++)
            if (slots[i].State != State.Free && slots[i].Bucket == b0 && Matches(slots[i], h))
                return;
        Request(h, b0, priority);
    }

    void Request(WakeHull h, int bucket, double priority)
    {
        if (requestCount == requests.Length)
            Array.Resize(ref requests, requests.Length * 2);
        requests[requestCount++] = new Asked { Hull = h, Bucket = bucket, Priority = priority };
    }

    /// <summary>Once a frame, before the passes: finished bakes taken back, a few uploaded into <paramref name="cmd"/>,
    /// this frame's requests handed to the workers, nearest first.</summary>
    public void Update(SDL_GPUCommandBuffer* cmd)
    {
        while (done.TryDequeue(out int i))
        {
            slots[i].State = State.Baked;
            inFlight--;
            Baked++;
        }
        Upload(cmd);
        // the nearest first: as many picks as there's room for, each a scan for the lowest priority left (a few scans
        // over the requests, no sort: Array.Sort with a comparer makes a delegate every call)
        while (inFlight < MaxInFlight)
        {
            int r = -1;
            for (int i = 0; i < requestCount; i++)
                if (requests[i].Hull != null && (r < 0 || requests[i].Priority < requests[r].Priority))
                    r = i;
            if (r < 0)
                break;
            ref var q = ref requests[r];
            var hull = q.Hull;
            q.Hull = null!;   // taken
            bool have = false;
            for (int i = 0; i < PoolSize && !have; i++)
                have = slots[i].State != State.Free && slots[i].Bucket == q.Bucket && Matches(slots[i], hull);
            if (have)
                continue;
            int free = Allocate();
            if (free < 0)
                break;
            var s = slots[free];
            (s.State, s.L, s.B, s.T, s.Bucket) = (State.Baking, hull.LengthM, hull.BeamM, hull.DraughtM, q.Bucket);
            inFlight++;
            jobs.Add(new Job(free, hull, BucketSpeedMs(q.Bucket)));
        }
        for (int r = 0; r < requestCount; r++)
            requests[r].Hull = null!;   // let go of the hulls
        requestCount = 0;
        frame++;
    }

    /// <summary>A free slot, else the least recently used ready one not drawn this frame or the last; -1 for none.</summary>
    int Allocate()
    {
        int best = -1;
        for (int i = 0; i < PoolSize; i++)
        {
            var s = slots[i];
            if (s.State == State.Free)
                return i;
            if (s.State == State.Ready && s.Used < frame - 1 && (best < 0 || s.Used < slots[best].Used))
                best = i;
        }
        return best;
    }

    readonly int[] uploading = new int[UploadsPerFrame];

    /// <summary>Up to <see cref="UploadsPerFrame"/> finished bakes into their textures: copied into the transfer buffer
    /// (mapped with cycling, so a buffer still in flight isn't written), then one copy pass, every mip level.</summary>
    void Upload(SDL_GPUCommandBuffer* cmd)
    {
        int n = 0;
        for (int i = 0; i < PoolSize && n < UploadsPerFrame; i++)
            if (slots[i].State == State.Baked)
                uploading[n++] = i;
        if (n == 0)
            return;
        uint bytes = (uint)WakeField.PackedBytes;
        if (transfer == null)
        {
            var ti = new SDL_GPUTransferBufferCreateInfo { usage = SDL_GPUTransferBufferUsage.SDL_GPU_TRANSFERBUFFERUSAGE_UPLOAD, size = bytes * UploadsPerFrame };
            transfer = SDL_CreateGPUTransferBuffer(device, &ti);
        }
        var dst = (byte*)SDL_MapGPUTransferBuffer(device, transfer, true);
        for (int k = 0; k < n; k++)
            slots[uploading[k]].Packed.CopyTo(new Span<byte>(dst + k * bytes, (int)bytes));
        SDL_UnmapGPUTransferBuffer(device, transfer);
        var cp = SDL_BeginGPUCopyPass(cmd);
        for (int k = 0; k < n; k++)
        {
            var s = slots[uploading[k]];
            s.Texture = s.Texture == null ? CreateTexture() : s.Texture;
            for (int level = 0; level < WakeField.Levels; level++)
            {
                var (w, h) = WakeField.LevelSize(level);
                var src = new SDL_GPUTextureTransferInfo
                {
                    transfer_buffer = transfer, offset = (uint)k * bytes + (uint)WakeField.LevelOffset(level),
                    pixels_per_row = (uint)w, rows_per_layer = (uint)h,
                };
                var region = new SDL_GPUTextureRegion { texture = s.Texture, mip_level = (uint)level, w = (uint)w, h = (uint)h, d = 1 };
                // cycled on the first level only: a fresh backing if a frame in flight still reads the old bake, then
                // every level into that one (cycling each would leave only the last level's data)
                SDL_UploadToGPUTexture(cp, &src, &region, level == 0);
            }
            s.State = State.Ready;
            s.Used = frame;
        }
        SDL_EndGPUCopyPass(cp);
    }

    SDL_GPUTexture* CreateTexture()
    {
        var info = new SDL_GPUTextureCreateInfo
        {
            type = SDL_GPUTextureType.SDL_GPU_TEXTURETYPE_2D,
            format = SDL_GPUTextureFormat.SDL_GPU_TEXTUREFORMAT_R16G16_FLOAT,
            usage = SDL_GPUTextureUsageFlags.SDL_GPU_TEXTUREUSAGE_SAMPLER,
            width = WakeField.Width,
            height = WakeField.Height,
            layer_count_or_depth = 1,
            num_levels = WakeField.Levels,
            sample_count = SDL_GPUSampleCount.SDL_GPU_SAMPLECOUNT_1,
        };
        var tex = SDL_CreateGPUTexture(device, &info);
        if (tex == null)
            throw new InvalidOperationException($"wake texture: {SDL_GetError()}");
        return tex;
    }

    public void Dispose()
    {
        jobs.CompleteAdding();
        foreach (var w in workers)
            w.Join();
        jobs.Dispose();
        SDL_WaitForGPUIdle(device);
        foreach (var s in slots)
            if (s.Texture != null)
                SDL_ReleaseGPUTexture(device, s.Texture);
        if (transfer != null)
            SDL_ReleaseGPUTransferBuffer(device, transfer);
    }
}
