using System.Numerics;
using ImGuiNET;

namespace Fleetwright.Designer;

/// <summary>The dense bureau look's tokens (docs/designer-ui/bureau.html) as ImGui colours, pushed around the
/// designer's windows only, so the tool scenes keep ImGui's defaults.</summary>
public static class DesignerTheme
{
    static Vector4 Hex(uint rgb, float a = 1) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

    public static readonly Vector4 Ground = Hex(0x0E1215);
    public static readonly Vector4 Panel = Hex(0x1C2329);
    /// <summary>A section's own row in the outliner, a tab at rest.</summary>
    public static readonly Vector4 Panel2 = Hex(0x20282F);
    /// <summary>Wells: value fields, bars' empty part.</summary>
    public static readonly Vector4 Inset = Hex(0x11161A);
    /// <summary>The line between table rows.</summary>
    public static readonly Vector4 Line = Hex(0x20292F);
    /// <summary>The near-black edge around panels and bars.</summary>
    public static readonly Vector4 Edge = Hex(0x090C0E);
    public static readonly Vector4 Bevel = Hex(0x2C3640);
    public static readonly Vector4 TitleTop = Hex(0x36434F);
    public static readonly Vector4 TitleBottom = Hex(0x27313A);
    /// <summary>A table's column header row.</summary>
    public static readonly Vector4 HeaderBg = Hex(0x161C21);
    public static readonly Vector4 Text = Hex(0xC9D3DB);
    public static readonly Vector4 Bright = Hex(0xEAF0F4);
    public static readonly Vector4 Muted = Hex(0x7F8B95);
    public static readonly Vector4 Brass = Hex(0xD9B868);
    public static readonly Vector4 BrassDark = Hex(0x5A4A24);
    public static readonly Vector4 BrassDarker = Hex(0x3E3218);
    public static readonly Vector4 Ok = Hex(0x86C77E);
    /// <summary>A consequence, never a refusal: amber, not red.</summary>
    public static readonly Vector4 Amber = Hex(0xE3A452);
    public static readonly Vector4 Blue = Hex(0x5B9BE0);
    /// <summary>The plan and profile's blueprint ground and its lines.</summary>
    public static readonly Vector4 Plan = Hex(0x14243A);
    public static readonly Vector4 PlanLine = Hex(0x9CC2E2);

    /// <summary>Armour thin to thick.</summary>
    public static readonly Vector4[] ArmourRamp = [Hex(0xF3E3A1), Hex(0xE9B66B), Hex(0xDE9455), Hex(0xCC6F43), Hex(0xB24E36), Hex(0x8E3328)];

    /// <summary>The ramp's colour for a plate <paramref name="mm"/> thick (the mockup's steps).</summary>
    public static Vector4 Armour(double mm) => ArmourRamp[mm >= 260 ? 5 : mm >= 190 ? 4 : mm >= 140 ? 3 : mm >= 95 ? 2 : mm >= 60 ? 1 : 0];

    /// <summary>The weight groups' colours (ledger swatches and bars, the outliner's sections).</summary>
    public static Vector4 Group(string g) => g switch
    {
        "hull" => Hex(0x8C7B62),
        "armour" => Hex(0x6E5A86),
        "machinery" => Hex(0xC58A2E),
        "fuel" => Hex(0x6A5A48),
        "armament" => Hex(0xA3473A),
        "superstructure" => Hex(0x5E8C6A),
        "fire_control" => Hex(0x5B7FA6),
        "misc" => Hex(0x9A9A8A),
        _ => Hex(0x56636F),
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
        float s = UiFonts.Scale;
        C(ImGuiCol.WindowBg, Ground);
        C(ImGuiCol.ChildBg, Vector4.Zero);
        C(ImGuiCol.PopupBg, Hex(0x1F272E));
        C(ImGuiCol.Border, Hex(0x3A4651));
        C(ImGuiCol.Text, Text);
        C(ImGuiCol.TextDisabled, Muted);
        C(ImGuiCol.FrameBg, Inset);
        C(ImGuiCol.FrameBgHovered, Hex(0x1A2229));
        C(ImGuiCol.FrameBgActive, Hex(0x1A2229));
        C(ImGuiCol.Button, Hex(0x2E3842));
        C(ImGuiCol.ButtonHovered, Hex(0x45525E));
        C(ImGuiCol.ButtonActive, Hex(0x56636F));
        C(ImGuiCol.Header, Hex(0x2E3842));
        C(ImGuiCol.HeaderHovered, Hex(0x38434E));
        C(ImGuiCol.HeaderActive, Hex(0x45525E));
        C(ImGuiCol.CheckMark, Brass);
        C(ImGuiCol.SliderGrab, Brass);
        C(ImGuiCol.Separator, Line);
        C(ImGuiCol.ScrollbarBg, Vector4.Zero);
        C(ImGuiCol.ScrollbarGrab, Hex(0x34404A));
        C(ImGuiCol.ScrollbarGrabHovered, Hex(0x45525E));
        C(ImGuiCol.TableRowBg, Vector4.Zero);
        C(ImGuiCol.TableRowBgAlt, new Vector4(1, 1, 1, 0.018f));
        C(ImGuiCol.TableBorderLight, Line);
        C(ImGuiCol.TableBorderStrong, Edge);
        C(ImGuiCol.TableHeaderBg, HeaderBg);
        C(ImGuiCol.TextSelectedBg, Hex(0x5B9BE0, 0.35f));
        C(ImGuiCol.MenuBarBg, Hex(0x161B20));
        C(ImGuiCol.ModalWindowDimBg, Hex(0x040608, 0.55f));
        V(ImGuiStyleVar.FrameRounding, 0);
        V(ImGuiStyleVar.ChildRounding, 0);
        V(ImGuiStyleVar.PopupRounding, 2 * s);
        V(ImGuiStyleVar.WindowBorderSize, 0);
        V(ImGuiStyleVar.ChildBorderSize, 0);
        V(ImGuiStyleVar.ScrollbarSize, 10 * s);
        V(ImGuiStyleVar.ScrollbarRounding, 0);
        V2(ImGuiStyleVar.FramePadding, new Vector2(5, 1) * s);
        V2(ImGuiStyleVar.ItemSpacing, new Vector2(6, 3) * s);
        V2(ImGuiStyleVar.CellPadding, new Vector2(6, 1) * s);
    }

    public static void Pop()
    {
        ImGui.PopStyleColor(pushedColours);
        ImGui.PopStyleVar(pushedVars);
        pushedColours = pushedVars = 0;
    }
}
