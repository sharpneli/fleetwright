using Fleetwright.Gpu;
using ImGuiNET;
using SDL;
using static SDL.SDL3;

namespace Fleetwright;

/// <summary>
/// Several scenes behind one: the map (the first), and the designer with the viewers as its tabs (the rest:
/// docs/map-ui/battle-hud.md decision 3). The map has the whole window, its own top bar opening the designer; the
/// designer's tabs are at the right end of the main menu bar, with the way back to the map, and F3 steps through them.
/// Each scene is made on first use and kept, so it comes back as it was left. The engine sees one
/// <see cref="IScene"/>, and the scenes don't know each other (what they share, such as a <see cref="DesignSession"/>,
/// is handed to them when they are made). Esc never quits (decision 4): the scene shuts what it has open, else the
/// app's menu opens, with the scene's own items (<see cref="ISceneMenu"/>) and Quit.
/// <para>The launch screen (docs/map-ui/launch.md L1), if given, is a scene beside them: the app's first until a
/// scenario begins (<see cref="Home"/>: where the designer goes back to), and opened again from the map's menu.</para>
/// </summary>
public sealed unsafe class SceneSwitcher : IScene
{
    readonly (string Name, Func<IScene> Make)[] entries;
    readonly IScene?[] made;
    readonly Action quit;
    readonly IScene? launch;
    int current, lastTab = 1;   // current -1: the launch screen
    bool menuOpen, menuShut;

    public SceneSwitcher(int start, Action quit, IScene? launch, params (string Name, Func<IScene> Make)[] scenes)
    {
        entries = scenes;
        made = new IScene?[scenes.Length];
        this.quit = quit;
        this.launch = launch;
        Home = start < 0 && launch != null ? -1 : 0;
        Show(start);
    }

    IScene Current => current < 0 ? launch! : made[current] ??= entries[current].Make();

    /// <summary>Where the designer goes back to: 0 the map, -1 the launch screen (until a scenario begins).</summary>
    public int Home { get; set; }

    /// <summary>The launch screen is showing.</summary>
    public bool ShowingLaunch => current < 0;

    /// <summary>Called when the launch screen gives way to another scene (it lets a battle it paused run again).</summary>
    public Action? LaunchLeft { get; init; }

    /// <summary>Called when the launch screen is shown again after another scene (back from the designer: the player's
    /// designs read again).</summary>
    public Action? LaunchEntered { get; init; }

    /// <summary>Opens a scene: -1 the launch screen, 0 the map, the others the designer's tabs.</summary>
    public void Show(int scene)
    {
        bool fromLaunch = current < 0;
        current = scene < 0 && launch != null ? -1 : Math.Clamp(scene, 0, entries.Length - 1);
        if (current > 0)
            lastTab = current;
        if (fromLaunch && current >= 0)
            LaunchLeft?.Invoke();
        else if (!fromLaunch && current < 0)
            LaunchEntered?.Invoke();
    }

    /// <summary>The designer, at the tab it was left on.</summary>
    public void ShowDesigner() => Show(lastTab);

    /// <summary>Opens the app's menu (Esc, or a scene's own button for it).</summary>
    public void ShowMenu()
    {
        if (menuOpen)
            return;
        menuOpen = true;
        (Current as ISceneMenu)?.MenuShown(true);
    }

    public void Draw(SDL_GPUCommandBuffer* cmd, RenderTarget target, float dt) => Current.Draw(cmd, target, dt);

    /// <summary>The scene's UI; on the designer's tabs, the tabs at the right end of the main menu bar (the designer
    /// puts its own menus at the left: a second BeginMainMenuBar appends to the same bar); then the menu, if open.</summary>
    public void BuildUi()
    {
        Current.BuildUi();
        if (current > 0 && ImGui.BeginMainMenuBar())
        {
            Ui.Theme.Push();
            float w = ImGui.CalcTextSize(Home < 0 ? "Scenarios |" : "Map |").X + ImGui.CalcTextSize("F3").X + ImGui.GetStyle().ItemSpacing.X * 6 + ImGui.GetStyle().WindowPadding.X;
            for (int i = 1; i < entries.Length; i++)
                w += ImGui.CalcTextSize(entries[i].Name).X + ImGui.GetStyle().ItemSpacing.X * 2;
            ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - w));
            if (ImGui.MenuItem(Home < 0 ? "Scenarios" : "Map"))
                Show(Home);
            ImGui.TextDisabled("|");
            for (int i = 1; i < entries.Length; i++)
                if (Ui.Pieces.ScreenTab(entries[i].Name, i == current))
                    Show(i);
            ImGui.TextDisabled("F3");
            Ui.Theme.Pop();
            ImGui.EndMainMenuBar();
        }
        Menu();
    }

    /// <summary>The app's menu: a modal over the scene with the scene's items, the way to the other half (the
    /// designer or the map), and Quit.</summary>
    void Menu()
    {
        const string Id = "##appmenu";
        if (menuOpen && !ImGui.IsPopupOpen(Id))
            ImGui.OpenPopup(Id);
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.GetCenter(), ImGuiCond.Always, new System.Numerics.Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(320 * UiFonts.Scale, 0));
        Ui.Theme.Push();
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(8, 5) * UiFonts.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2(10, 10) * UiFonts.Scale);
        bool open = ImGui.BeginPopupModal(Id, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings);
        ImGui.PopStyleVar();
        if (!open)
        {
            ImGui.PopStyleVar();
            Ui.Theme.Pop();
            return;
        }
        var full = new System.Numerics.Vector2(-1, 0);
        bool shut = menuShut;
        menuShut = false;
        ImGui.TextDisabled("FLEETWRIGHT");
        ImGui.Separator();
        if (ImGui.Button("Resume  (Esc)", full))
            shut = true;
        if (Current is ISceneMenu items)
        {
            ImGui.Spacing();
            shut |= items.MenuItems();
        }
        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.Button(current <= 0 ? "Ship designer  (F3)" : Home < 0 ? "Back to the scenarios" : "Back to the map", full))
        {
            shut = true;
            if (current <= 0)
                ShowDesigner();
            else
                Show(Home);
        }
        if (current < 0 && Home == 0 && ImGui.Button("Back to the battle", full))
        {
            shut = true;
            Show(0);
        }
        if (ImGui.Button("Quit to desktop", full))
            quit();
        if (shut)
        {
            ImGui.CloseCurrentPopup();
            menuOpen = false;
            (Current as ISceneMenu)?.MenuShown(false);
        }
        ImGui.EndPopup();
        ImGui.PopStyleVar();
        Ui.Theme.Pop();
    }

    public bool ProcessEvent(SDL_Event* e, uint width, uint height)
    {
        if ((SDL_EventType)e->type == SDL_EventType.SDL_EVENT_KEY_DOWN)
        {
            uint key = (uint)e->key.key;
            if (key == SDLK_F3)
            {
                Show(current <= 0 ? lastTab : current % (entries.Length - 1) + 1);
                return true;
            }
            if (key == SDLK_ESCAPE)
            {
                // the topmost thing shuts: the menu, else what the scene has open, else a popup of ImGui's (it shuts
                // its own); with nothing open, the menu
                if (menuOpen)
                    menuShut = true;
                else if (!Current.ProcessEvent(e, width, height) && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopup))
                    ShowMenu();
                return true;
            }
            if (menuOpen)
                return true;   // under the menu, the scene's keys wait (space would run the clock)
        }
        return Current.ProcessEvent(e, width, height);
    }

    public void Dispose()
    {
        foreach (var s in made)
            s?.Dispose();
        launch?.Dispose();
    }
}
