using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The world's instruments on the UI side (docs/battle/plan.md step 2.7b), fed once a frame by
/// <see cref="WorldSession"/>: how smoothly the clock reached the screen (game time per frame against the speed asked,
/// frames that got no new snapshot, how old the snapshot drawn was), where the sim thread's time went (its probes, as a
/// share of the thread, from the snapshots), the map's own parts (<see cref="Ui"/>, timed by the scene) and garbage
/// collections. Each figure is over the last second (refreshed once a second, so it reads), and a run's whole is kept
/// for <see cref="Report"/> (-frametimes). Allocates nothing per frame.</summary>
public sealed class WorldPerf
{
    /// <summary>The map's probes, on the UI thread: the scene times its parts here.</summary>
    public Probes Ui { get; } = new();

    // the last second's figures, shown
    public readonly double[] SimShare = new double[Probes.Capacity], SimMaxMs = new double[Probes.Capacity];
    public readonly double[] UiMsPerFrame = new double[Probes.Capacity], UiMaxMs = new double[Probes.Capacity];
    public int Frames, Stale, Gen0, Gen1, Gen2;
    public double WorstJump, WorstAgeMs, MeanAgeMs, SmoothPct, Achieved, BehindMs;
    string[] simNames = new string[Probes.Capacity];
    int simCount;

    // the window being measured
    readonly long[] simTicks0 = new long[Probes.Capacity], uiTicks0 = new long[Probes.Capacity];
    readonly double[] simMax = new double[Probes.Capacity];
    long windowStart = Probes.Now;
    int frames, stale, smooth, g0, g1, g2;
    double worstJump, worstAge, sumAge;
    long lastSequence = -1, lastGameMs = long.MinValue;
    int lastGeneration = -1;

    // the whole run, for the report
    readonly List<(float GameMs, float ExpectedMs, float AgeMs, bool Stale)> run = new(100_000);
    readonly long[] runSim0 = new long[Probes.Capacity], runUi0 = new long[Probes.Capacity];
    long runStart;
    int runG0 = -1, runG1, runG2;
    readonly long[] runSimMax = new long[Probes.Capacity];
    readonly long[] runUiMax = new long[Probes.Capacity];

    /// <summary>A frame: the snapshot it will draw, the real time since the last one, and which world it's from (a new
    /// world starts the measures over).</summary>
    public void Frame(Snapshot s, double dt, int generation, long drawMs)
    {
        long now = Probes.Now;
        if (generation != lastGeneration)
        {
            lastGeneration = generation;
            (lastSequence, lastGameMs) = (-1, long.MinValue);
            Start(s, now);
            run.Clear();
            Array.Copy(s.Probes.Ticks, runSim0, Probes.Capacity);
            Array.Copy(Ui.Ticks, runUi0, Probes.Capacity);
            Array.Clear(runSimMax);
            Array.Clear(runUiMax);
            runStart = now;
            (runG0, runG1, runG2) = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
        }

        // the clock as the screen sees it
        bool fresh = s.Sequence != lastSequence;
        double game = lastGameMs == long.MinValue ? 0 : drawMs - lastGameMs;   // the clock the map draws at
        // smooth means each frame's step is the speed the clock is keeping (the achieved, which is the asked unless the
        // sim is behind): steady motion at a lower speed is smooth, jerks are not
        double expected = s.Paused || s.AchievedSpeed < 0.5 ? 0 : s.AchievedSpeed * dt * 1000;   // none yet after a load
        double age = Probes.Ms(now - s.PublishedAt);
        if (lastGameMs != long.MinValue && run.Count < run.Capacity)
            run.Add(((float)game, (float)expected, (float)age, !fresh && expected > 0));
        (lastSequence, lastGameMs) = (s.Sequence, drawMs);
        frames++;
        if (!fresh && expected > 0)
            stale++;
        if (expected > 0)
        {
            double jump = game / expected;
            worstJump = Math.Max(worstJump, jump);
            if (Math.Abs(jump - 1) < 0.25)
                smooth++;
        }
        worstAge = Math.Max(worstAge, age);
        sumAge += age;
        if (fresh)
            for (int i = 0; i < s.Probes.Count; i++)
            {
                simMax[i] = Math.Max(simMax[i], Probes.Ms(s.Probes.Max[i]));
                runSimMax[i] = Math.Max(runSimMax[i], s.Probes.Max[i]);
            }
        for (int i = 0; i < Ui.Count; i++)
            runUiMax[i] = Math.Max(runUiMax[i], Ui.Max[i]);

        double seconds = Probes.Ms(now - windowStart) / 1000;
        if (seconds < 1)
            return;
        // the second's figures
        simCount = s.Probes.Count;
        Array.Copy(s.Probes.Names, simNames, Probes.Capacity);
        for (int i = 0; i < simCount; i++)
        {
            SimShare[i] = Probes.Ms(s.Probes.Ticks[i] - simTicks0[i]) / 1000 / seconds;
            SimMaxMs[i] = simMax[i];
        }
        for (int i = 0; i < Ui.Count; i++)
        {
            UiMsPerFrame[i] = Probes.Ms(Ui.Ticks[i] - uiTicks0[i]) / Math.Max(frames, 1);
            UiMaxMs[i] = Probes.Ms(Ui.Max[i]);
        }
        Frames = frames;
        Stale = stale;
        WorstJump = worstJump;
        WorstAgeMs = worstAge;
        MeanAgeMs = sumAge / Math.Max(frames, 1);
        SmoothPct = 100.0 * smooth / Math.Max(frames, 1);
        (Gen0, Gen1, Gen2) = (GC.CollectionCount(0) - g0, GC.CollectionCount(1) - g1, GC.CollectionCount(2) - g2);
        Achieved = s.AchievedSpeed;
        BehindMs = s.Target.Ms - s.Time.Ms;
        Start(s, now);
    }

    void Start(Snapshot s, long now)
    {
        windowStart = now;
        Array.Copy(s.Probes.Ticks, simTicks0, Probes.Capacity);
        Array.Copy(Ui.Ticks, uiTicks0, Probes.Capacity);
        Array.Clear(simMax);
        Ui.ResetMax();
        frames = stale = smooth = 0;
        worstJump = worstAge = sumAge = 0;
        (g0, g1, g2) = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    }

    /// <summary>The panel (F2, beside the engine's stats): the clock's smoothness, the sim thread, the map's parts.</summary>
    public void Draw(Snapshot s, ShipArtLibrary art, int overlayVertices)
    {
        var vp = ImGui.GetMainViewport();
        float sc = UiFonts.Scale;
        ImGui.SetNextWindowPos(new Vector2(vp.WorkPos.X + vp.WorkSize.X - 10 * sc, vp.WorkPos.Y + 170 * sc), ImGuiCond.Always, new Vector2(1, 0));
        ImGui.SetNextWindowBgAlpha(0.85f);
        if (!ImGui.Begin("World performance", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }
        // TextUnformatted throughout: ImGui.Text reads % as a format
        ImGui.TextUnformatted($"Clock: asked {s.Speed:G4}x, achieved {Achieved:F0}x, behind {BehindMs / 1000.0:F1} s game");
        ImGui.TextUnformatted($"Frames {Frames}/s: {SmoothPct:F0}% smooth, {Stale} stale, worst step {WorstJump:F1}x a frame's");
        ImGui.TextUnformatted($"Snapshot age: mean {MeanAgeMs:F1} ms, worst {WorstAgeMs:F1} ms");
        ImGui.TextUnformatted($"GC this second: gen0 {Gen0}, gen1 {Gen1}, gen2 {Gen2}");
        ImGui.TextUnformatted($"Ship art: {art.Uploaded} classes, {art.Bytes / 1048576.0:F0} MB of textures, longest upload frame {art.MaxUploadMs:F1} ms");
        ImGui.TextUnformatted($"Overlay: {overlayVertices:#,0} vertices of {GlobeRenderer.OverlayCapacity:#,0}{(overlayVertices > GlobeRenderer.OverlayCapacity ? "  (FULL: the last drawn are dropped)" : "")}");
        if (ImGui.BeginTable("##simprobes", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Sim thread");
            ImGui.TableSetupColumn("share");
            ImGui.TableSetupColumn("longest");
            ImGui.TableHeadersRow();
            for (int i = 0; i < simCount; i++)
                if (SimShare[i] >= 0.0005 || SimMaxMs[i] >= 1)
                    Row(simNames[i], $"{SimShare[i] * 100:F1}%", $"{SimMaxMs[i]:F2} ms");
            ImGui.EndTable();
        }
        if (ImGui.BeginTable("##uiprobes", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Map");
            ImGui.TableSetupColumn("a frame");
            ImGui.TableSetupColumn("longest");
            ImGui.TableHeadersRow();
            for (int i = 0; i < Ui.Count; i++)
                Row(Ui.Names[i], $"{UiMsPerFrame[i]:F2} ms", $"{UiMaxMs[i]:F2} ms");
            ImGui.EndTable();
        }
        ImGui.End();

        static void Row(string name, string a, string b)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(a);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(b);
        }
    }

    long effectFrames;
    readonly long[] effectSum = new long[5];
    readonly int[] effectMax = new int[5];
    static readonly string[] EffectNames = ["smoke puffs", "point lights", "of them fires'", "splash book", "smoke sources"];

    /// <summary>The world view's effects this frame (docs/visuals/hits-and-fires.md I8): the smoke's puffs, the point lights
    /// and the fires' share of them, the splash book's entries and the continuous smoke sources, for the run's means and
    /// peaks.</summary>
    public void Effects(int puffs, int lights, int fireLights, int splashes, int sources)
    {
        effectFrames++;
        Span<int> n = [puffs, lights, fireLights, splashes, sources];
        for (int i = 0; i < n.Length; i++)
        {
            effectSum[i] += n[i];
            effectMax[i] = Math.Max(effectMax[i], n[i]);
        }
    }

    /// <summary>The run's whole, for -frametimes at exit.</summary>
    public string Report(Snapshot s, ShipArtLibrary art)
    {
        if (run.Count < 2)
            return "world: no frames measured";
        double seconds = Probes.Ms(Probes.Now - runStart) / 1000;
        var moving = run.Where(r => r.ExpectedMs > 0).ToArray();
        var ratio = moving.Select(r => r.GameMs / r.ExpectedMs).Order().ToArray();
        var age = run.Select(r => r.AgeMs).Order().ToArray();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"world over {run.Count} frames ({seconds:F1} s), asked {s.Speed:G4}x, achieved {s.AchievedSpeed:F0}x at the end:");
        if (ratio.Length > 0)
            sb.AppendLine($"  game time a frame over the achieved speed's: p1 {ratio[ratio.Length / 100]:F2}, p50 {ratio[ratio.Length / 2]:F2}, " +
                          $"p99 {ratio[ratio.Length * 99 / 100]:F2}, max {ratio[^1]:F2}; {moving.Count(r => r.Stale)} stale frames; " +
                          $"{100.0 * ratio.Count(r => Math.Abs(r - 1) < 0.25) / ratio.Length:F0}% within 25%");
        sb.AppendLine($"  snapshot age when drawn: p50 {age[age.Length / 2]:F1}, p99 {age[age.Length * 99 / 100]:F1}, max {age[^1]:F1} ms");
        sb.AppendLine($"  GC: gen0 {GC.CollectionCount(0) - runG0}, gen1 {GC.CollectionCount(1) - runG1}, gen2 {GC.CollectionCount(2) - runG2}");
        sb.AppendLine($"  ship art: {art.Uploaded} classes, {art.Bytes / 1048576.0:F0} MB of textures, longest upload frame {art.MaxUploadMs:F1} ms");
        if (effectFrames > 0)
            sb.AppendLine("  effects, mean (most): " + string.Join(", ",
                EffectNames.Select((name, i) => $"{name} {effectSum[i] / (double)effectFrames:F0} ({effectMax[i]})")));
        sb.AppendLine("  sim thread, share of the thread (longest call):");
        for (int i = 0; i < s.Probes.Count; i++)
        {
            double share = Probes.Ms(s.Probes.Ticks[i] - runSim0[i]) / 1000 / seconds;
            if (share >= 0.0001 || runSimMax[i] > 0)
                sb.AppendLine($"    {s.Probes.Names[i],-26} {share * 100,6:F2}%  ({Probes.Ms(runSimMax[i]):F2} ms)");
        }
        sb.AppendLine("  map, ms a frame (longest):");
        for (int i = 0; i < Ui.Count; i++)
            sb.AppendLine($"    {Ui.Names[i],-26} {Probes.Ms(Ui.Ticks[i] - runUi0[i]) / run.Count,6:F3}  ({Probes.Ms(runUiMax[i]):F2})");
        return sb.ToString();
    }
}
