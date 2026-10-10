using System.Numerics;
using ImGuiNET;

namespace Fleetwright;

/// <summary>A scene's control window pinned to the left edge, below the menu bar and down to the bottom of the
/// window. Only its width is the user's: the right edge drags, the height follows the window.</summary>
public static class SidePanel
{
    const float StartWidth = 330, MinWidth = 200;

    /// <summary>Begins the panel; end it with <see cref="ImGui.End"/> as any window.</summary>
    public static void Begin(string name)
    {
        var vp = ImGui.GetMainViewport();
        float h = vp.WorkSize.Y;
        ImGui.SetNextWindowPos(vp.WorkPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(StartWidth, h), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(MinWidth, h), new Vector2(MathF.Max(MinWidth, vp.WorkSize.X * 0.8f), h));
        ImGui.Begin(name, ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBringToFrontOnFocus);
    }
}
