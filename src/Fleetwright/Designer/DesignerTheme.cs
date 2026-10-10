using System.Numerics;
using ImGuiNET;

namespace Fleetwright.Designer;

/// <summary>The mockup's design tokens (docs/designer-ui/README.md) as ImGui colours, pushed around the designer's
/// windows only, so the tool scenes keep ImGui's defaults.</summary>
public static class DesignerTheme
{
    static Vector4 Hex(uint rgb, float a = 1) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

    public static readonly Vector4 Ground = Hex(0x141D24);
    public static readonly Vector4 Panel = Hex(0x1B2730);
    public static readonly Vector4 Panel2 = Hex(0x22313B);
    public static readonly Vector4 Inset = Hex(0x15242E);
    public static readonly Vector4 Line = Hex(0x304350);
    public static readonly Vector4 Text = Hex(0xE3E9EC);
    public static readonly Vector4 Muted = Hex(0x93A6B0);
    public static readonly Vector4 Brass = Hex(0xE0AE4F);
    public static readonly Vector4 Blue = Hex(0x5B9BE0);
    public static readonly Vector4 Ok = Hex(0x9FDDB7);
    public static readonly Vector4 OkBg = Hex(0x1C3329);
    public static readonly Vector4 Amber = Hex(0xF1D79C);
    public static readonly Vector4 AmberBg = Hex(0x2E2717);
    public static readonly Vector4 Paper = Hex(0xEFE7D6);
    public static readonly Vector4 Ink = Hex(0x26221C);
    public static readonly Vector4 InkMuted = Hex(0x6E6555);
    public static readonly Vector4 AmberInk = Hex(0x8A5A10);
    public static readonly Vector4 PaperLine = Hex(0xD6CBB4);

    /// <summary>Armour thin to thick.</summary>
    public static readonly Vector4[] ArmourRamp = [Hex(0xF3E3A1), Hex(0xE9B66B), Hex(0xDE9455), Hex(0xCC6F43), Hex(0xB24E36), Hex(0x8E3328)];

    /// <summary>The weight groups' colours on the Legend's bar.</summary>
    public static Vector4 Group(string g) => g switch
    {
        "hull" => Hex(0x6B5B95),
        "armour" => Hex(0xB24E36),
        "machinery" => Hex(0x8C7A63),
        "fuel" => Hex(0x4A4038),
        "armament" => Hex(0xD49A3A),
        "superstructure" => Hex(0x93A6B0),
        "fire_control" => Hex(0x5B9BE0),
        "misc" => Hex(0x5E8C6A),
        _ => Hex(0xBBB3A3),
    };

    public static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    static int pushedColours, pushedVars;

    /// <summary>Pushes the designer's colours and spacing; <see cref="Pop"/> takes them off.</summary>
    public static void Push()
    {
        void C(ImGuiCol col, Vector4 v)
        {
            ImGui.PushStyleColor(col, v);
            pushedColours++;
        }
        void V(ImGuiStyleVar var, float v)
        {
            ImGui.PushStyleVar(var, v);
            pushedVars++;
        }
        void V2(ImGuiStyleVar var, Vector2 v)
        {
            ImGui.PushStyleVar(var, v);
            pushedVars++;
        }
        C(ImGuiCol.WindowBg, Ground);
        C(ImGuiCol.ChildBg, Ground);
        C(ImGuiCol.PopupBg, Panel2);
        C(ImGuiCol.Border, Line);
        C(ImGuiCol.Text, Text);
        C(ImGuiCol.TextDisabled, Muted);
        C(ImGuiCol.FrameBg, Hex(0x2A3B46));   // lighter than the panels, so checkboxes and radios show
        C(ImGuiCol.FrameBgHovered, Panel2);
        C(ImGuiCol.FrameBgActive, Panel2);
        C(ImGuiCol.Button, Panel2);
        C(ImGuiCol.ButtonHovered, Hex(0x2C3F4B));
        C(ImGuiCol.ButtonActive, Hex(0x36505F));
        C(ImGuiCol.Header, Panel2);
        C(ImGuiCol.HeaderHovered, Hex(0x2C3F4B));
        C(ImGuiCol.HeaderActive, Hex(0x36505F));
        C(ImGuiCol.CheckMark, Brass);
        C(ImGuiCol.SliderGrab, Brass);
        C(ImGuiCol.Separator, Line);
        C(ImGuiCol.ScrollbarBg, Ground);
        C(ImGuiCol.ScrollbarGrab, Line);
        C(ImGuiCol.TextSelectedBg, Hex(0x5B9BE0, 0.35f));
        V(ImGuiStyleVar.FrameRounding, 3 * UiFonts.Scale);
        V(ImGuiStyleVar.ChildRounding, 4 * UiFonts.Scale);
        V(ImGuiStyleVar.PopupRounding, 4 * UiFonts.Scale);
        V(ImGuiStyleVar.WindowBorderSize, 0);
        V2(ImGuiStyleVar.FramePadding, new Vector2(8, 4) * UiFonts.Scale);
        V2(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6) * UiFonts.Scale);
    }

    public static void Pop()
    {
        ImGui.PopStyleColor(pushedColours);
        ImGui.PopStyleVar(pushedVars);
        pushedColours = pushedVars = 0;
    }
}
