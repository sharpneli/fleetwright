using System.Numerics;
using Fleetwright.Sim.Missions;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The world tab's scenarios (docs/battle/plan.md step 2.7a): a picker of the shipped ones (Content/Scenarios)
/// that loads one in place of the world, a scenario file dropped on the window does the same (Program), and
/// <c>-scenario=</c> at start. While it loads the old world runs on; when the new one is in, the selection and tracks of
/// the old are dropped and the map goes to the scenario's camera.</summary>
public sealed unsafe partial class MapScene
{
    string[]? scenarioPaths;
    int scenarioPick;
    int seenGeneration;

    /// <summary>The next scenario's camera is skipped: -map= given beside -scenario= at start (screenshots).</summary>
    public bool KeepCameraOnce { get; set; }

    /// <summary>The next scenario's world starts with this ship selected (-select= beside -scenario=), or -1.</summary>
    public int SelectOnce { get; set; } = -1;

    /// <summary>Once a frame, before anything reads the snapshot: a new world drops what the map held of the old.</summary>
    void NewWorld()
    {
        if (world.Generation == seenGeneration)
            return;
        seenGeneration = world.Generation;
        (selected, SelectOnce) = (SelectOnce, -1);
        picked.Clear();
        tracks.Clear();
        going.Clear();
        wrecks.Clear();
        wrecked.Clear();
        shipRenderer.Splashes.Book.Clear();
        shipRenderer.Flashes.Book.Clear();
        shipRenderer.Smoke.Book.Clear();
        (placing, placeDrag) = (false, false);
        // the spawner's side is the player's: the scenario's (any of its sides), else the dev world's 0
        maxSide = Math.Max(3, (world.Scenario?.Sides.Length ?? 0) - 1);
        spawnSide = Math.Clamp(world.Scenario?.Player ?? 0, 0, maxSide);
        if (KeepCameraOnce)
            KeepCameraOnce = false;
        else if (world.Scenario?.Camera is { } c)
        {
            camera = MapCamera.At(c.Lat, c.Lon, c.MetresPerPixel);
            clamped = false;
        }
    }

    void ScenarioRow()
    {
        scenarioPaths ??= WorldSession.ShippedScenarios();
        if (world.Loading is { } l)
            ImGui.TextDisabled($"Loading {l.Name}: classes {l.Ready} of {l.Of}...");
        else
        {
            ImGui.SetNextItemWidth(170 * UiFonts.Scale);
            var names = scenarioPaths.Select(Path.GetFileNameWithoutExtension).ToArray();
            if (scenarioPaths.Length > 0)
            {
                ImGui.Combo("##scenario", ref scenarioPick, names!, names.Length);
                ImGui.SameLine();
                if (ImGui.Button("Load scenario"))
                    Load(scenarioPaths[scenarioPick]);
            }
            else
                ImGui.TextDisabled("no scenarios in Content/Scenarios");
            if (world.Scenario is { } sc)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"{sc.Name}, loaded in {world.LoadSeconds.Total:F1} s");
                if (ImGui.IsItemHovered() && sc.Brief.Length > 0)
                    ImGui.SetTooltip(sc.Brief);
            }
        }
        if (world.LoadError is { } e)
            ImGui.TextColored(new Vector4(1, 0.4f, 0.3f, 1), e);
        if (world.ScenarioStart is { Warnings.Count: > 0 } st)
        {
            ImGui.TextColored(new Vector4(1, 0.75f, 0.3f, 1), $"{st.Warnings.Count} warning(s): {st.Warnings[0]}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(string.Join("\n", st.Warnings));
        }
        ImGui.Separator();
    }

    void Load(string path) => world.Load(path);
}
