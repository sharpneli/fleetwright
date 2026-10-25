using System.Globalization;
using System.Numerics;
using Fleetwright.Designer;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The scenario's result (docs/map-ui/battle-hud.md U7): when the snapshot first carries the end, a modal over
/// the map with the verdict, each side's count and each ship's fate: the truth on both sides (decision 5: the battler's
/// result is its score). The clock stops under it; carrying on runs it again (a fight in progress is never cut off: the
/// end is a score, not a stop). The menu opens it again. Its text is made once, when it opens. A fight of a setup
/// says so, with each change from the scenario as written (docs/map-ui/launch.md: the hard thing done is seen as
/// done).</summary>
public sealed partial class MapScene
{
    EndView? endShown;
    bool resultOpen, pausedForResult;
    string resultKick = "", resultTitle = "", resultMeta = "";
    readonly List<(string Name, string[] Cells)> resultSides = [];
    readonly List<(string Name, string Fate, bool Lost, int Side)> resultShips = [];
    string resultSetup = "";
    readonly List<string> resultChanges = [];

    /// <summary>Opens the result when the end first arrives (once a world).</summary>
    void WatchEnd(Snapshot s)
    {
        if (s.End == null || ReferenceEquals(s.End, endShown))
            return;
        endShown = s.End;
        OpenResult(s);
    }

    void OpenResult(Snapshot s)
    {
        var end = s.End;
        if (end == null)
            return;
        int me = s.PlayerSide;
        var sides = end.Sides;
        string at = new WorldTime(end.EndMs).ToUtc().ToString("HH:mm", CultureInfo.InvariantCulture);
        resultKick = $"SCENARIO OVER · {end.Reason.ToUpperInvariant()} AT {at}";
        // the verdict: the side whose aims are met; else the one that lost much less; else a draw
        int met = -1, metCount = 0;
        for (int i = 0; i < sides.Length; i++)
            if (sides[i].AimsMet)
                (met, metCount) = (i, metCount + 1);
        int better = -1;
        if (metCount != 1 && sides.Length == 2)
        {
            double a = sides[0].LostTonnes / Math.Max(sides[0].Tonnes, 1), b = sides[1].LostTonnes / Math.Max(sides[1].Tonnes, 1);
            better = a < b * 0.5 ? 0 : b < a * 0.5 ? 1 : -1;
        }
        resultTitle = metCount == 1 ? (met == me ? $"A {sides[met].Name} victory" : me >= 0 ? $"Defeat: a {sides[met].Name} victory" : $"A {sides[met].Name} victory")
            : better >= 0 ? $"{sides[better].Name} had the better of it" : "A draw";
        resultMeta = end.Reason switch
        {
            "decided" => "One side is gone or out of the fight.",
            "time" => "Time is up. If you carry on, the fight goes on: the count is as it stood.",
            "quiet" => "No fight for a while: the sides have parted.",
            "done" => "Every side has done what it came for, or given it up.",
            _ => "",
        };
        resultChanges.Clear();
        resultSetup = "";
        if (world.Scenario is { Setup: { IsEmpty: false } setup, Written: { } written })
        {
            resultSetup = $"{written.Name}, as you set it:";
            foreach (var c in setup.Changes)
                resultChanges.Add(c.Describe(written));
        }
        resultSides.Clear();
        foreach (var x in sides)
            resultSides.Add((x.Name, [x.Ships.ToString(CultureInfo.InvariantCulture), x.Lost.ToString(CultureInfo.InvariantCulture),
                x.LostTonnes.ToString("#,0", CultureInfo.InvariantCulture), x.Damaged.ToString(CultureInfo.InvariantCulture),
                x.Hits.ToString(CultureInfo.InvariantCulture), x.Torpedoes.ToString(CultureInfo.InvariantCulture), x.AimsMet ? "met" : "not met"]));
        resultShips.Clear();
        foreach (var x in end.Ships)
        {
            string fate = x.Lost
                ? $"sunk {new WorldTime(x.LossMs).ToUtc():HH:mm} · {x.Loss switch { LossKind.Magazine => "her magazine", LossKind.Capsized => "capsized", LossKind.BrokeInTwo => "broke in two", _ => "foundered" }}"
                : x.Damaged ? $"damaged · {x.Hits} hits" : x.Hits > 0 ? $"{x.Hits} hits" : "unharmed";
            resultShips.Add((x.Name, fate, x.Lost, x.Side));
        }
        resultOpen = true;
        if (!s.Paused)
        {
            world.Send(Command.Pause());
            pausedForResult = true;
        }
    }

    void Result(Snapshot s)
    {
        const string Id = "##result";
        if (!resultOpen)
            return;
        if (!ImGui.IsPopupOpen(Id))
            ImGui.OpenPopup(Id);
        float sc = UiFonts.Scale, w = 940 * sc;
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(w, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(22, 16) * sc);
        bool open = ImGui.BeginPopupModal(Id, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings);
        ImGui.PopStyleVar();
        if (!open)
            return;
        ImGui.TextColored(DesignerTheme.Brass, resultKick);
        ImGui.PushFont(UiFonts.PosterFigure);
        ImGui.TextColored(DesignerTheme.Bright, resultTitle);
        ImGui.PopFont();
        ImGui.TextDisabled(resultMeta);
        if (resultSetup.Length > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(DesignerTheme.Brass, resultSetup);
            foreach (var c in resultChanges)
                ImGui.TextDisabled("    " + c);
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        // the count: a row a side
        if (ImGui.BeginTable("##count", 8, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg, new Vector2(w - 44 * sc, 0)))
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 2);
            foreach (var h in CountHeads)
                ImGui.TableSetupColumn(h, ImGuiTableColumnFlags.WidthStretch, 1);
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.TableHeadersRow();
            ImGui.PopFont();
            for (int i = 0; i < resultSides.Count; i++)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextColored(i == s.PlayerSide ? Friend : Bad, resultSides[i].Name);
                ImGui.PushFont(UiFonts.Mono);
                foreach (var cell in resultSides[i].Cells)
                {
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(cell);
                }
                ImGui.PopFont();
            }
            ImGui.EndTable();
        }
        ImGui.Spacing();
        // each ship's fate, by side
        float colW = (w - 44 * sc) / Math.Max(resultSides.Count, 1);
        if (ImGui.BeginTable("##fates", Math.Max(resultSides.Count, 1), ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame, new Vector2(w - 44 * sc, 0)))
        {
            for (int side = 0; side < resultSides.Count; side++)
            {
                ImGui.TableNextColumn();
                ImGui.PushFont(UiFonts.SansSmall);
                ImGui.TextDisabled(resultSides[side].Name);
                ImGui.PopFont();
                ImGui.PushID(side);
                if (ImGui.BeginTable("##side", 2, ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableSetupColumn("##n", ImGuiTableColumnFlags.WidthFixed, Math.Min(150 * sc, colW * 0.42f));
                    ImGui.TableSetupColumn("##f", ImGuiTableColumnFlags.WidthStretch);
                    foreach (var x in resultShips)
                        if (x.Side == side)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.TextColored(side == s.PlayerSide ? Friend : Bad, x.Name);
                            ImGui.TableNextColumn();
                            ImGui.TextColored(x.Lost ? Bad : DesignerTheme.Text, x.Fate);
                        }
                    ImGui.EndTable();
                }
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        float bw = 150 * sc;
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 22 * sc - 3 * bw - 2 * ImGui.GetStyle().ItemSpacing.X);
        bool shut = false;
        if (ImGui.Button("Carry on", new Vector2(bw, 26 * sc)))
        {
            shut = true;
            if (pausedForResult)
                world.Send(Command.Resume());
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("the map again, its clock running: look round, or fight on");
        ImGui.SameLine();
        if (ImGui.Button("Play again", new Vector2(bw, 26 * sc)) && world.Scenario is { } sc2)
        {
            shut = true;
            world.Load(sc2);
        }
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, DesignerTheme.Brass);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, DesignerTheme.Brass with { W = 0.85f });
        ImGui.PushStyleColor(ImGuiCol.Text, DesignerTheme.Ground);
        if (ImGui.Button("Scenarios", new Vector2(bw, 26 * sc)))
        {
            shut = true;
            if (OpenLaunch != null)
                OpenLaunch();
            else
                OpenMenu?.Invoke();
        }
        ImGui.PopStyleColor(3);
        if (shut || resultShut)
        {
            (resultOpen, resultShut, pausedForResult) = (false, false, false);
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    bool resultShut;
    static readonly string[] CountHeads = ["SHIPS", "LOST", "TONNES LOST", "DAMAGED", "HITS", "TORPEDOES", "AIMS"];
}
