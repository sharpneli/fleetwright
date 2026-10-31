using System.Numerics;
using System.Text;
using Fleetwright.Sim.Missions;
using ImGuiNET;
using static Fleetwright.Ui.Pieces;
using T = Fleetwright.Ui.Theme;

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
        // a box over the screen (the name must be typed before it can be saved): a title strip, the name, the buttons
        var vp = ImGui.GetMainViewport();
        float w = P(480);
        ImGui.SetNextWindowPos(vp.GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(w, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(P(8), TitleH + P(8)));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 0);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, T.Panel);
        bool open = ImGui.BeginPopupModal(Id, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings);
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);
        if (!open)
            return;
        var keep = dl;
        dl = ImGui.GetWindowDrawList();
        var o = ImGui.GetWindowPos();
        dl.AddRectFilledMultiColor(o, o + new Vector2(w, TitleH), U(T.TitleTop), U(T.TitleTop), U(T.TitleBottom), U(T.TitleBottom));
        dl.HLine(o.X, o.X + w, o.Y + TitleH, T.Edge);
        dl.Text(UiFonts.SansBold, new Vector2(o.X + P(7), Mid(UiFonts.SansBold, o.Y, TitleH)), T.Brass, "SAVE SETUP");
        dl.Text(UiFonts.SansSmall, new Vector2(o.X + P(15) + Width(UiFonts.SansBold, "SAVE SETUP"), Mid(UiFonts.SansSmall, o.Y, TitleH)), T.Muted, "My Games/Fleetwright/Setups");
        ImGui.PushFont(UiFonts.SansSmall);
        ImGui.PushTextWrapPos(w - P(8));
        ImGui.TextDisabled($"{written!.Name} and its {setup.Changes.Count} change{(setup.Changes.Count == 1 ? "" : "s")}, as a scenario file: listed under Your setups, shared by passing the file round.");
        if (UnsavedIn(current!) is int unsaved and > 0)
            ImGui.TextDisabled($"The {(unsaved == 1 ? "unsaved design it uses is" : $"{unsaved} unsaved designs it uses are")} saved to your designs too.");
        ImGui.PopTextWrapPos();
        ImGui.PopFont();
        ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);
        if (saveFocus)
        {
            ImGui.SetKeyboardFocusHere();
            saveFocus = false;
        }
        bool save = ImGui.InputText("##setupname", ref saveName, 96, ImGuiInputTextFlags.EnterReturnsTrue);
        if (saveError != null)
            ImGui.TextColored(T.Amber, saveError);
        ImGui.Spacing();
        var by = ImGui.GetCursorScreenPos();
        float sw = ButtonWidth("Save", "Enter", primary: true), cw = ButtonWidth("Cancel", "Esc");
        bool cancel = dl.Button("##savecancel", new Vector2(o.X + w - P(8) - sw - P(4) - cw, by.Y), "Cancel", "Esc") || ImGui.IsKeyPressed(ImGuiKey.Escape);
        save |= dl.Button("##savego", new Vector2(o.X + w - P(8) - sw, by.Y), "Save", "Enter", primary: true, enabled: saveName.Trim().Length > 0);
        ImGui.SetCursorScreenPos(by + new Vector2(0, P(20)));
        ImGui.Dummy(new Vector2(1, P(2)));
        dl = keep;
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
    }

    void Save(string name)
    {
        SaveUnsaved(current!);   // a setup saved finds its designs at the next start
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
