using Fleetwright.Gpu;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright;

/// <summary>
/// Several scenes behind one: a menu bar names them, F3 steps to the next. Each scene is made on first use and kept,
/// so it comes back as it was left. The engine sees one <see cref="IScene"/>, and the scenes don't know each other
/// (what they share, such as a <see cref="DesignSession"/>, is handed to them when they are made).
/// </summary>
public sealed unsafe class SceneSwitcher : IScene
{
    readonly (string Name, Func<IScene> Make)[] entries;
    readonly IScene?[] made;
    int current;

    public SceneSwitcher(int start, params (string Name, Func<IScene> Make)[] scenes)
    {
        entries = scenes;
        made = new IScene?[scenes.Length];
        current = Math.Clamp(start, 0, scenes.Length - 1);
    }

    IScene Current => made[current] ??= entries[current].Make();

    public void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt) => Current.Draw(cmd, target, dt);

    public void BuildUi()
    {
        if (ImGui.BeginMainMenuBar())
        {
            for (int i = 0; i < entries.Length; i++)
                if (ImGui.MenuItem(entries[i].Name, i == 0 ? "F3" : null, i == current))
                    current = i;
            ImGui.EndMainMenuBar();
        }
        Current.BuildUi();
    }

    public bool ProcessEvent(SDL_Event* e, uint width, uint height)
    {
        if ((SDL_EventType)e->type == SDL_EventType.SDL_EVENT_KEY_DOWN && (uint)e->key.key == SDLK_F3)
        {
            current = (current + 1) % entries.Length;
            return true;
        }
        return Current.ProcessEvent(e, width, height);
    }

    public void Dispose()
    {
        foreach (var s in made)
            s?.Dispose();
    }
}
