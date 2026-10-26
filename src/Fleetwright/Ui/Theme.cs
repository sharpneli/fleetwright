using System.Numerics;
using ImGuiNET;

namespace Fleetwright.Ui;

/// <summary>The UI language's colour tokens and ImGui style (docs/ui-language.md "Colour", the mockups'
/// <c>:root</c>), pushed around a game screen's windows (the designer, the launch screen), so the tool scenes keep
/// ImGui's defaults. Every chrome colour comes from here: the gunmetal reskin is a change to this file. The pieces drawn
/// in it are <see cref="Pieces"/>.</summary>
public static class Theme
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
    /// <summary>The edge of an inset well (a stepper's value, a drop-down's field).</summary>
    public static readonly Vector4 InsetEdge = Hex(0x1A2026);
    /// <summary>The primary button and a chosen segment under the pointer; an overlay toggle's letter when on.</summary>
    public static readonly Vector4 BrassHover = Hex(0x4E3F1E);
    public static readonly Vector4 BrassLight = Hex(0xF0D58A);
    /// <summary>An overlay toggle's gradient when off.</summary>
    public static readonly Vector4 ToggleTop = Hex(0x2E3842);
    public static readonly Vector4 ToggleBottom = Hex(0x232B33);
    /// <summary>A numbered tab at rest (its gradient) and under the pointer (its top); the chosen one is the title strip's.</summary>
    public static readonly Vector4 TabTop = Hex(0x2A333C);
    public static readonly Vector4 TabHover = Hex(0x333D47);
    public static readonly Vector4 TabBottom = Hex(0x20282F);
    /// <summary>The resource bar's gradient.</summary>
    public static readonly Vector4 ResourceTop = Hex(0x26303A);
    public static readonly Vector4 ResourceBottom = Hex(0x1B2228);
    /// <summary>The menu bar and the status bar.</summary>
    public static readonly Vector4 MenuBar = Hex(0x161B20);
    /// <summary>Buttons and segmented controls at rest, hovered, and their border.</summary>
    public static readonly Vector4 Button = Hex(0x2E3842);
    public static readonly Vector4 ButtonHover = Hex(0x45525E);
    public static readonly Vector4 ButtonEdge = Hex(0x3A4651);
    /// <summary>A drop-down's square ▼ box.</summary>
    public static readonly Vector4 DropBox = Hex(0x56636F);
    /// <summary>A stepper's arrow boxes and their edge.</summary>
    public static readonly Vector4 StepBox = Hex(0x0B0F12);
    public static readonly Vector4 StepEdge = Hex(0x2A333C);
    /// <summary>The selected row's warm tint (a 3 px brass bar on its left edge with it).</summary>
    public static readonly Vector4 Selected = Hex(0x2A2A1E);
    /// <summary>A row under the pointer.</summary>
    public static readonly Vector4 Hover = Hex(0x252E36);
    /// <summary>A work surface's tags: their ground, border and text; the border is a kbd chip's too, its text the chip's.</summary>
    public static readonly Vector4 TagBg = Hex(0x0A1016, 0.82f);
    public static readonly Vector4 TagEdge = Hex(0x4A5764);
    public static readonly Vector4 TagText = Hex(0xCFE3F3);
    public static readonly Vector4 KeyText = Hex(0xAEB9C3);
    /// <summary>The enemy, in figures and marks only (<c>MapPalette.Hostile</c>); never a refusal.</summary>
    public static readonly Vector4 Hostile = Hex(0xE2685C);
    /// <summary>Status chips' and the margin box's grounds and borders: ready, a consequence, friend.</summary>
    public static readonly Vector4 OkTint = Hex(0x17261A);
    public static readonly Vector4 OkEdge = Hex(0x2F5A31);
    public static readonly Vector4 AmberTint = Hex(0x33261A);
    public static readonly Vector4 AmberEdge = Hex(0x7A5A2C);
    public static readonly Vector4 BlueTint = Hex(0x16263A);
    /// <summary>A friend chip's border; the blue-filled screen tab.</summary>
    public static readonly Vector4 BlueEdge = Hex(0x2F5E8E);
    /// <summary>The sides' shares in a ledger bar: friend, enemy.</summary>
    public static readonly Vector4 FriendBar = Hex(0x3F6D9E);
    public static readonly Vector4 HostileBar = Hex(0x94473E);

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
        C(ImGuiCol.Border, ButtonEdge);
        C(ImGuiCol.Text, Text);
        C(ImGuiCol.TextDisabled, Muted);
        C(ImGuiCol.FrameBg, Inset);
        C(ImGuiCol.FrameBgHovered, Hex(0x1A2229));
        C(ImGuiCol.FrameBgActive, Hex(0x1A2229));
        C(ImGuiCol.Button, Button);
        C(ImGuiCol.ButtonHovered, ButtonHover);
        C(ImGuiCol.ButtonActive, DropBox);
        C(ImGuiCol.Header, Button);
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
        C(ImGuiCol.MenuBarBg, MenuBar);
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
