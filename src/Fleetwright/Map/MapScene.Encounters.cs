using System.Diagnostics;
using System.Numerics;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>Encounters on the map (docs/battle/plan.md step 3.1c): a ring round each open encounter's ships, the
/// player's own in amber, others' in grey; a label at its top (its number, ships and since when); and a notice in the
/// clock's panel when one is news (the player's ships in it, or one opened between others), with the player's rule
/// for what the clock does then.</summary>
public sealed partial class MapScene
{
    static readonly Vector4 OwnEncounter = new(1f, 0.68f, 0.25f, 0.8f), OthersEncounter = new(0.7f, 0.76f, 0.85f, 0.5f);
    const double EncounterRingPadM = 5_000;   // outside the outermost ship
    const double NewsShownSeconds = 8;

    int newsSeen;          // the news last noticed, and when (wall clock)
    long newsSeenAt;

    static double EncounterRingM(EncountersView v, int i) => Math.Max(v.RadiusM[i], EncounterRingPadM) + EncounterRingPadM;

    void DrawEncounters(in MapView view, Snapshot s)
    {
        var v = s.Encounters;
        for (int i = 0; i < v.Count; i++)
            RingM(view, v.Centre[i], EncounterRingM(v, i), v.Own[i] ? OwnEncounter : OthersEncounter, v.Own[i] ? 2f : 1.5f);
    }

    /// <summary>Each encounter's label at the top of its ring.</summary>
    void EncounterLabels(Snapshot s)
    {
        var v = s.Encounters;
        if (v.Count == 0)
            return;
        var view = View;
        var vp = ImGui.GetMainViewport();
        float sx = vp.Size.X / (float)Math.Max(width, 1), sy = vp.Size.Y / (float)Math.Max(height, 1);
        var dl = ImGui.GetBackgroundDrawList();
        for (int i = 0; i < v.Count; i++)
        {
            var top = v.Centre[i];
            var north = Earth.Forward(top, 0);
            Earth.Advance(ref top, ref north, EncounterRingM(v, i));
            if (!view.Project(top, out double x, out double y))
                continue;
            string text = $"Encounter {v.Id[i]} · {v.Ships[i]} ships · since {new WorldTime(v.OpenedMs[i]).ToUtc():HH:mm}";
            var at = new Vector2(vp.Pos.X + (float)(x * sx) - ImGui.CalcTextSize(text).X / 2, vp.Pos.Y + (float)(y * sy) - ImGui.GetTextLineHeight() - 4);
            dl.AddText(at + new Vector2(1, 1), 0xC0000000, text);
            dl.AddText(at, ImGui.GetColorU32(v.Own[i] ? OwnEncounter with { W = 1 } : OthersEncounter with { W = 0.9f }), text);
        }
    }

    static readonly string[] RuleNames = ["pause", "1×", "10×", "100×", "carry on"];

    static int RuleIndex(InterestRule r) => r.Action switch
    {
        InterestAction.Pause => 0,
        InterestAction.Slow => r.Speed <= 1 ? 1 : r.Speed <= 10 ? 2 : 3,
        _ => 4,
    };

    static Command RuleCommand(InterestKind kind, int i) => i switch
    {
        0 => Command.SetInterest(kind, InterestAction.Pause),
        1 or 2 or 3 => Command.SetInterest(kind, InterestAction.Slow, i == 1 ? 1 : i == 2 ? 10 : 100),
        _ => Command.SetInterest(kind, InterestAction.Nothing),
    };

    /// <summary>In the clock's panel: the news, while it's fresh or the clock is paused at it; and the player's rules.</summary>
    void EncounterNews(Snapshot s)
    {
        var v = s.Encounters;
        if (v.NewsId != newsSeen)
            (newsSeen, newsSeenAt) = (v.NewsId, Stopwatch.GetTimestamp());
        bool pausedAtIt = s.Paused && s.Time.Ms - v.NewsMs <= 1;
        if (v.NewsId != 0 && (pausedAtIt || Stopwatch.GetElapsedTime(newsSeenAt).TotalSeconds < NewsShownSeconds))
        {
            string what = v.NewsKind == InterestKind.Own ? "contact: our ships in encounter" : "encounter between others:";
            ImGui.TextColored(v.NewsKind == InterestKind.Own ? OwnEncounter with { W = 1 } : OthersEncounter with { W = 1 },
                $"{what} {v.NewsId} at {new WorldTime(v.NewsMs).ToUtc():HH:mm}{(pausedAtIt ? " (paused)" : "")}");
        }
        if (s.PlayerSide < 0)
            return;
        ImGui.TextDisabled("on contact:");
        Rule(s, InterestKind.Own, "##own");
        ImGui.SameLine();
        ImGui.TextDisabled("others':");
        Rule(s, InterestKind.Others, "##others");
    }

    void Rule(Snapshot s, InterestKind kind, string id)
    {
        ImGui.SameLine();
        int cur = RuleIndex(kind == InterestKind.Own ? s.InterestOwn : s.InterestOthers);
        ImGui.SetNextItemWidth(90 * UiFonts.Scale);
        if (ImGui.BeginCombo(id, RuleNames[cur]))
        {
            for (int i = 0; i < RuleNames.Length; i++)
                if (ImGui.Selectable(RuleNames[i], i == cur) && i != cur)
                    world.Send(RuleCommand(kind, i));
            ImGui.EndCombo();
        }
    }
}
