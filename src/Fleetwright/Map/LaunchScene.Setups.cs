using System.Numerics;
using System.Text;
using Fleetwright.Sim.Missions;
using ImGuiNET;

namespace Fleetwright.Map;

/// <summary>The launch screen's Save setup… (docs/map-ui/launch.md L2): the setup being edited written as a scenario
/// file of the scenario it's of, in <see cref="SetupsFolder"/>, then read back as its entry under Your setups and picked
/// (what's listed is what's on disk). A setup picked and saved again keeps its file; a new name makes a new one.</summary>
public sealed unsafe partial class LaunchScene
{
    bool saveOpen, saveFocus;
    string saveName = "";
    string? saveError;

    void OpenSave()
    {
        saveOpen = saveFocus = true;
        saveError = null;
        saveName = setup.Name.Length > 0 ? setup.Name : $"{written!.Name} · {setup.Changes[0] switch
        {
            PlayAs { Side: null } => "watched",
            PlayAs p => "as " + p.Side,
            SwapDesign d => d.To,
            AddUnit a => "with " + a.Unit.Name,
            RemoveUnit r => "without " + r.Name,
            _ => "my setup",
        }}";
    }

    void SaveDialog()
    {
        const string Id = "##savesetup";
        if (!saveOpen)
            return;
        if (!ImGui.IsPopupOpen(Id))
            ImGui.OpenPopup(Id);
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(480 * sc, 0));
        Designer.DesignerTheme.Push();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(18, 14) * sc);
        bool open = ImGui.BeginPopupModal(Id, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings);
        ImGui.PopStyleVar();
        if (!open)
        {
            Designer.DesignerTheme.Pop();
            return;
        }
        ImGui.TextColored(Brass, "SAVE SETUP");
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 444 * sc);
        ImGui.TextDisabled($"{written!.Name} and its {setup.Changes.Count} change{(setup.Changes.Count == 1 ? "" : "s")}, as a scenario file in My Games/Fleetwright/Setups: listed under Your setups, and shared by passing the file round.");
        ImGui.PopTextWrapPos();
        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);
        if (saveFocus)
        {
            ImGui.SetKeyboardFocusHere();
            saveFocus = false;
        }
        bool save = ImGui.InputText("##setupname", ref saveName, 96, ImGuiInputTextFlags.EnterReturnsTrue);
        if (saveError != null)
            ImGui.TextColored(Hostile, saveError);
        ImGui.Spacing();
        float bw = 110 * sc;
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 18 * sc - 2 * bw - ImGui.GetStyle().ItemSpacing.X);
        bool cancel = ImGui.Button("Cancel", new Vector2(bw, 26 * sc)) || ImGui.IsKeyPressed(ImGuiKey.Escape);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Brass);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Hex(0xE6C77A));
        ImGui.PushStyleColor(ImGuiCol.Text, Hex(0x111111));
        save |= ImGui.Button("Save", new Vector2(bw, 26 * sc));
        ImGui.PopStyleColor(3);
        if (save && saveName.Trim().Length > 0)
        {
            try
            {
                Save(saveName.Trim());
                cancel = true;
            }
            catch (Exception x) when (x is IOException or UnauthorizedAccessException or ScenarioException)
            {
                saveError = x.Message;
            }
        }
        if (cancel)
        {
            saveOpen = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
        Designer.DesignerTheme.Pop();
    }

    void Save(string name)
    {
        Directory.CreateDirectory(SetupsFolder);
        string slug = Slug(name), path = Path.Combine(SetupsFolder, slug + ".json");
        string? mine = entries[pick].Saved ? Path.GetFullPath(entries[pick].Path) : null;
        for (int n = 2; File.Exists(path) && !Path.GetFullPath(path).Equals(mine, StringComparison.OrdinalIgnoreCase); n++)
            path = Path.Combine(SetupsFolder, $"{slug}-{n}.json");   // a name taken by another setup: a number after it
        File.WriteAllText(path, (setup with { Name = name }).ToJson(written!));
        if (mine != null && !Path.GetFullPath(path).Equals(mine, StringComparison.OrdinalIgnoreCase))
            mine = null;   // renamed: the old file stays, its own setup
        entries.RemoveAll(e => Path.GetFullPath(e.Path).Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        var saved = Read(path, opened: false);
        if (saved.Error != null)
            throw new ScenarioException(saved.Error);
        entries.Add(saved);
        Sort();
        Pick(entries.IndexOf(saved));
    }

    /// <summary>A file name from a setup's name: letters and digits, the rest dashes.</summary>
    static string Slug(string name)
    {
        var b = new StringBuilder();
        foreach (char c in name.ToLowerInvariant())
            if (char.IsLetterOrDigit(c) && c < 128)
                b.Append(c);
            else if (b.Length > 0 && b[^1] != '-')
                b.Append('-');
        string s = b.ToString().Trim('-');
        return s.Length == 0 ? "setup" : s.Length > 60 ? s[..60].TrimEnd('-') : s;
    }
}
