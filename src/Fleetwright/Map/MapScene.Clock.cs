using System.Numerics;
using Fleetwright.Sim;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The clock's speed detent: a track of notches from 1x to 1,000,000x, the knob on the speed asked, a mark over
/// the speed achieved, the coarse tier's stretch tinted. Click or drag picks a notch (and runs a paused clock); the wheel
/// over it and + and - step it.</summary>
public sealed partial class MapScene
{
    static readonly double[] Speeds = [1, 3, 10, 30, 60, 300, 1000, 3000, 10_000, 30_000, 100_000, 300_000, 1_000_000];
    static readonly string[] SpeedLabels = [.. Speeds.Select(sp => SpeedLabel(sp).TrimEnd('×'))];
    static readonly string[] SpeedSpans = [.. Speeds.Select(sp => $"{SpeedLabel(sp)}: {PerSecond(sp)} a second")];

    int detentSent = -1;   // the notch a drag last sent, so a held drag sends once per notch

    void SpeedDetent(Snapshot s)
    {
        var io = ImGui.GetIO();
        var dl = ImGui.GetWindowDrawList();
        float sc = UiFonts.Scale, labelW = 0;
        foreach (string l in SpeedLabels)
            labelW = Math.Max(labelW, ImGui.CalcTextSize(l).X);
        float step = Math.Max(labelW + 6 * sc, 24 * sc), lineH = ImGui.GetTextLineHeight();
        var o = ImGui.GetCursorScreenPos();
        float x0 = o.X + step / 2, y = o.Y + ImGui.GetFrameHeight() / 2;
        ImGui.InvisibleButton("##speed", new Vector2(step * Speeds.Length, ImGui.GetFrameHeight() + lineH));
        bool active = ImGui.IsItemActive(), hovered = ImGui.IsItemHovered();
        int under = Math.Clamp((int)MathF.Round((io.MousePos.X - x0) / step), 0, Speeds.Length - 1);

        if (active)
        {
            if (ImGui.IsItemActivated() || under != detentSent)
            {
                world.Send(Command.SetSpeed(Speeds[under]));
                if (s.Paused)
                    world.Send(Command.Resume());
                detentSent = under;
            }
        }
        else
        {
            detentSent = -1;
            if (hovered && io.MouseWheel != 0)
                world.Send(Command.SetSpeed(StepSpeed(s.Speed, Math.Sign(io.MouseWheel))));
        }
        if (hovered && !active)
        {
            ImGui.BeginTooltip();
            ImGui.Text(SpeedSpans[under]);
            if (Speeds[under] >= world.Settings.CoarseMinSpeed)
                ImGui.TextDisabled("coarse tier: ships off rails in 1-minute steps");
            ImGui.EndTooltip();
        }

        // the track, the coarse stretch over it, the notches and their labels
        float x1 = x0 + step * (Speeds.Length - 1), xc = x0 + step * Notch(world.Settings.CoarseMinSpeed);
        float th = 2 * sc;
        dl.AddRectFilled(new Vector2(x0, y - th), new Vector2(x1, y + th), ImGui.GetColorU32(ImGuiCol.FrameBg), th);
        if (xc < x1)
            dl.AddRectFilled(new Vector2(xc, y - th), new Vector2(x1, y + th), ImGui.GetColorU32(new Vector4(1f, 0.75f, 0.3f, 0.45f)), th);
        uint dim = ImGui.GetColorU32(ImGuiCol.TextDisabled), text = ImGui.GetColorU32(ImGuiCol.Text);
        int asked = Array.IndexOf(Speeds, s.Speed);
        for (int i = 0; i < Speeds.Length; i++)
        {
            float x = x0 + step * i;
            dl.AddLine(new Vector2(x, y - 5 * sc), new Vector2(x, y + 5 * sc), dim, sc);
            float w = ImGui.CalcTextSize(SpeedLabels[i]).X;
            dl.AddText(new Vector2(x - w / 2, y + 7 * sc), i == asked && !s.Paused ? text : dim, SpeedLabels[i]);
        }

        // the speed achieved: a mark above the track (it lags the asked when the sim can't keep up)
        if (!s.Paused)
        {
            float xa = x0 + step * Notch(s.AchievedSpeed);
            dl.AddTriangleFilled(new Vector2(xa - 4 * sc, y - 10 * sc), new Vector2(xa + 4 * sc, y - 10 * sc), new Vector2(xa, y - 5 * sc), text);
        }

        // the knob on the speed asked: hollow while paused
        float xk = x0 + step * (detentSent >= 0 ? detentSent : Notch(s.Speed)), r = 6 * sc;
        if (s.Paused)
            dl.AddCircle(new Vector2(xk, y), r, dim, 0, 2 * sc);
        else
            dl.AddCircleFilled(new Vector2(xk, y), r, ImGui.GetColorU32(active || hovered ? ImGuiCol.SliderGrabActive : ImGuiCol.SliderGrab));
    }

    /// <summary>Where a speed falls on the detent, in notches: log-linear between them, clamped to the ends.</summary>
    static float Notch(double speed)
    {
        if (speed <= Speeds[0])
            return 0;
        for (int i = 1; i < Speeds.Length; i++)
            if (speed <= Speeds[i])
                return i - 1 + (float)(Math.Log(speed / Speeds[i - 1]) / Math.Log(Speeds[i] / Speeds[i - 1]));
        return Speeds.Length - 1;
    }

    /// <summary>The next notch up or down from a speed, which needn't be on one (-speed= takes any).</summary>
    static double StepSpeed(double speed, int dir)
    {
        if (dir > 0)
        {
            foreach (double sp in Speeds)
                if (sp > speed * 1.001)
                    return sp;
            return Speeds[^1];
        }
        for (int i = Speeds.Length - 1; i >= 0; i--)
            if (Speeds[i] < speed / 1.001)
                return Speeds[i];
        return Speeds[0];
    }

    static string SpeedLabel(double s) => s >= 1_000_000 && s % 1_000_000 == 0 ? $"{s / 1_000_000:0}M×"
        : s >= 1000 && s % 1000 == 0 ? $"{s / 1000:0}k×" : $"{s:0.#}×";

    /// <summary>World time a real second at a speed: "5 min", "8.3 h".</summary>
    static string PerSecond(double speed) => speed switch
    {
        < 60 => $"{speed:0} s",
        < 3600 => $"{speed / 60:0.#} min",
        < 86_400 => $"{speed / 3600:0.#} h",
        _ => $"{speed / 86_400:0.#} days",
    };
}
