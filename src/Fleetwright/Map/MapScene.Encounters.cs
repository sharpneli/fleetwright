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

    /// <summary>Where an encounter's ring goes, if it's drawn: round all its ships with the truth; in the player's view
    /// only the player's own, round their own ships in it (the rest of it is what the picture holds).</summary>
    static bool Ring(Snapshot s, int i, out Vec3d centre, out double radiusM)
    {
        var v = s.Encounters;
        (centre, radiusM) = (v.Centre[i], v.RadiusM[i]);
        if (!s.Truth && s.PlayerSide >= 0)
        {
            if (!v.Own[i])
                return false;
            var ships = s.Ships;
            var sum = default(Vec3d);
            for (int k = 0; k < ships.Count; k++)
                if (ships.Encounter[k] == v.Id[i] && ships.Side[k] == s.PlayerSide)
                    sum += ships.Position[k];
            if (sum.LengthSquared == 0)
                return false;
            centre = sum.Normalized() * Earth.Radius;
            radiusM = 0;
            for (int k = 0; k < ships.Count; k++)
                if (ships.Encounter[k] == v.Id[i] && ships.Side[k] == s.PlayerSide)
                    radiusM = Math.Max(radiusM, Earth.Distance(centre, ships.Position[k]));
        }
        radiusM = Math.Max(radiusM, EncounterRingPadM) + EncounterRingPadM;
        return true;
    }

    void DrawEncounters(in MapView view, Snapshot s)
    {
        var v = s.Encounters;
        for (int i = 0; i < v.Count; i++)
            if (Ring(s, i, out var centre, out double r))
                RingM(view, centre, r, v.Own[i] ? OwnEncounter : OthersEncounter, v.Own[i] ? 2f : 1.5f, Dash.Solid, true);
    }

    // the encounters' labels, made when what they say changes
    readonly Dictionary<int, ((int, int, long, int, int) Key, string Text)> encounterTexts = [];

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
            if (!Ring(s, i, out var top, out double r))
                continue;
            var north = Earth.Forward(top, 0);
            Earth.Advance(ref top, ref north, r);
            if (!view.Project(top, out double x, out double y))
                continue;
            bool count = s.Truth || s.PlayerSide < 0;   // the player knows their own, not the count
            var (ours, theirs) = Losses(v.Id[i]);
            var key = (v.Id[i], count ? v.Ships[i] : -1, v.OpenedMs[i], ours, theirs);
            if (!encounterTexts.TryGetValue(v.Id[i], out var cached) || cached.Key != key)
            {
                string ships = count ? $" · {v.Ships[i]} ships" : "";
                encounterTexts[v.Id[i]] = cached = (key, $"Encounter {v.Id[i]}{ships} · since {new WorldTime(v.OpenedMs[i]).ToUtc():HH:mm}{LossesText(ours, theirs)}");
            }
            string text = cached.Text;
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
