using Fleetwright.Gpu;
using SDL;

namespace Fleetwright;

/// <summary>
/// A full-window view with its own UI: the ship viewer, the hitbox viewer, later the game's screens. The engine owns
/// the window, the frame loop and the main <see cref="RenderTarget"/>, and calls these once per frame; a scene never
/// reaches back into the engine. What a scene draws comes from a renderer that takes any RenderTarget, so the same
/// drawing can go into a small offscreen target inside another scene's UI.
/// </summary>
public unsafe interface IScene : IDisposable
{
    /// <summary>Draws the frame into <paramref name="target"/>: begins and ends its own render passes.</summary>
    void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt);

    /// <summary>Builds the scene's ImGui windows, between the engine's NewFrame and Render.</summary>
    void BuildUi();

    /// <summary>An input event ImGui didn't want. <paramref name="width"/> and <paramref name="height"/> are the
    /// window's. False when the event isn't the scene's.</summary>
    bool ProcessEvent(SDL_Event* e, uint width, uint height);
}
