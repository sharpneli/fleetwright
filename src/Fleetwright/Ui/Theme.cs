using System.Numerics;
using ImGuiNET;

namespace Fleetwright.Ui;

/// <summary>The UI language's colour tokens and ImGui style (docs/ui-language.md "Colour", the mockups'
/// <c>:root</c>), pushed around a game screen's windows (the designer, the launch screen), so the tool scenes keep
/// ImGui's defaults. Every chrome colour comes from here: a reskin is a change to this file. The pieces drawn
/// in it are <see cref="Pieces"/>.</summary>
public static class Theme
{
    static Vector4 Hex(uint rgb, float a = 1) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

    // the Dunkelgrau skin (docs/ui-themes/navy-trials.html, key 6; 2026-11-08): flat naval-paint grey with a faint cool
    // cast, bodies flat and strips, tabs, buttons and bars raised by a very mild sheen (Sheen; 2026-11-09), the sea the only blue
    public static readonly Vector4 Ground = Hex(0x181B1C);
    public static readonly Vector4 Panel = Hex(0x242728);
    /// <summary>A section's own row in the outliner, a tab at rest.</summary>
    public static readonly Vector4 Panel2 = Hex(0x2B2F31);
    /// <summary>Wells: value fields, bars' empty part.</summary>
    public static readonly Vector4 Inset = Hex(0x141616);
    /// <summary>The line between table rows.</summary>
    public static readonly Vector4 Line = Hex(0x303536);
    /// <summary>The near-black edge around panels and bars.</summary>
    public static readonly Vector4 Edge = Hex(0x0A0B0C);
    /// <summary>The bevel's light line inside a panel's edge (panels have a bevel, never a shadow).</summary>
    public static readonly Vector4 Bevel = Hex(0x33383A);
    /// <summary>A pane's title strip (flat), a step lighter than its body; the chosen numbered tab's too.</summary>
    public static readonly Vector4 TitleStrip = Hex(0x2D3133);
    /// <summary>A title strip's rivet before the name (the one ornament).</summary>
    public static readonly Vector4 Rivet = Hex(0x586164);
    /// <summary>A table's column header row.</summary>
    public static readonly Vector4 HeaderBg = Hex(0x1E2122);
    public static readonly Vector4 Text = Hex(0xD5D6D7);
    public static readonly Vector4 Bright = Hex(0xF5F5F5);
    public static readonly Vector4 Muted = Hex(0x919597);
    /// <summary>Disabled, and the faintest marks.</summary>
    public static readonly Vector4 Faint = Hex(0x586164);
    /// <summary>The one accent (cadmium orange; the name kept from the bureau skin's brass): the selection, the active, the
    /// primary action, pane titles. Never decoration.</summary>
    public static readonly Vector4 Brass = Hex(0xE8772E);
    /// <summary>The accent's dark border (a primary button's kbd chip, a toggle's top when on).</summary>
    public static readonly Vector4 BrassDark = Hex(0x5C3416);
    /// <summary>The chosen segment's ground: the accent at 18 % on a panel.</summary>
    public static readonly Vector4 BrassDarker = Hex(0x473529);
    public static readonly Vector4 Ok = Hex(0x8DBE6B);
    /// <summary>A consequence, never a refusal: amber, not red (a hue apart from the accent).</summary>
    public static readonly Vector4 Amber = Hex(0xE8B64A);
    /// <summary>Friend: cream, everywhere (<c>MapPalette.Friend</c>); blue belongs to the sea only.</summary>
    public static readonly Vector4 Friend = Hex(0xF1ECDD);
    /// <summary>Water on a drawing: the waterline, liquid layers.</summary>
    public static readonly Vector4 Water = Hex(0x7FB0CF);
    /// <summary>Arcs of fire and guns bearing on a drawing: the accent, at low alpha for fills (the spec's 9 %).</summary>
    public static readonly Vector4 Arc = Brass;
    /// <summary>The fire-control weight group's colour, and a battery's first series colour.</summary>
    public static readonly Vector4 FireControl = Hex(0x8EC1DE);
    /// <summary>The plan and profile's ground (a grey plate) and its lines.</summary>
    public static readonly Vector4 Plan = Hex(0x282C2E);
    public static readonly Vector4 PlanLine = Hex(0xB0B3B4);
    /// <summary>Small drawings in the chrome (the ship card's plan and profile): the hull's fill, the superstructure, the
    /// funnels and the darkest parts.</summary>
    public static readonly Vector4 DrawingFill = Hex(0x343839);
    public static readonly Vector4 DrawingMid = Hex(0x4A4F51);
    public static readonly Vector4 DrawingDark = Hex(0x1E2122);
    /// <summary>A fire marked on a drawing (the ship card's plate): the flame and its hot core.</summary>
    public static readonly Vector4 Fire = Hex(0xFF9A3D);
    public static readonly Vector4 FireCore = Hex(0xFFE08A);
    /// <summary>A floating HUD panel's glass over the map (the battle HUD's bars and the ship card).</summary>
    public static readonly Vector4 Glass = Hex(0x242728, 0.92f);
    /// <summary>The edge of an inset well (a stepper's value, a drop-down's field).</summary>
    public static readonly Vector4 InsetEdge = Hex(0x0A0B0C);
    /// <summary>The primary button's fill (flat, the accent), under the pointer, and its dark ink.</summary>
    public static readonly Vector4 Primary = Hex(0xE8772E);
    public static readonly Vector4 PrimaryHover = Hex(0xF28A45);
    public static readonly Vector4 PrimaryInk = Hex(0x140800);
    /// <summary>A chosen segment under the pointer.</summary>
    public static readonly Vector4 BrassHover = Hex(0x563D2C);
    /// <summary>Changed values; an overlay toggle's letter when on.</summary>
    public static readonly Vector4 BrassLight = Hex(0xFFC79A);
    /// <summary>An overlay toggle's fill when off (on: <see cref="BrassDarker"/>).</summary>
    public static readonly Vector4 Toggle = Hex(0x333839);
    /// <summary>A numbered tab at rest and under the pointer; the chosen one is the title strip's.</summary>
    public static readonly Vector4 Tab = Hex(0x2B2F31);
    public static readonly Vector4 TabHover = Hex(0x3D4345);
    /// <summary>The resource bar's ground.</summary>
    public static readonly Vector4 ResourceBar = Hex(0x242728);
    /// <summary>The menu bar and the status bar.</summary>
    public static readonly Vector4 MenuBar = Hex(0x1E2122);
    /// <summary>Buttons and segmented controls at rest, hovered, and their border.</summary>
    public static readonly Vector4 Button = Hex(0x333839);
    public static readonly Vector4 ButtonHover = Hex(0x3D4345);
    public static readonly Vector4 ButtonEdge = Hex(0x444A4C);
    /// <summary>A drop-down's square box.</summary>
    public static readonly Vector4 DropBox = Hex(0x525A5C);
    /// <summary>A stepper's arrow boxes and their edge.</summary>
    public static readonly Vector4 StepBox = Hex(0x141616);
    public static readonly Vector4 StepEdge = Hex(0x3D4345);
    /// <summary>The selected row's tint, the accent at 11 % (a 3 px accent bar on its left edge with it).</summary>
    public static readonly Vector4 Selected = Hex(0x3A3029);
    /// <summary>Every other table row's lift over the panel.</summary>
    public static readonly Vector4 RowAlt = new(1, 1, 1, 0.018f);
    /// <summary>A row under the pointer.</summary>
    public static readonly Vector4 Hover = Hex(0x323738);
    /// <summary>A work surface's tags: their ground, border and text; the border is a kbd chip's too, its text the chip's.</summary>
    public static readonly Vector4 TagBg = Hex(0x111313, 0.84f);
    public static readonly Vector4 TagEdge = Hex(0x525A5C);
    public static readonly Vector4 TagText = Hex(0xDDDFE0);
    public static readonly Vector4 KeyText = Hex(0xA3A7A9);
    /// <summary>The enemy, in figures and marks only (<c>MapPalette.Hostile</c>); never a refusal.</summary>
    public static readonly Vector4 Hostile = Hex(0xE04A3C);
    /// <summary>Status chips' and the margin box's grounds and borders: ready, a consequence.</summary>
    public static readonly Vector4 OkTint = Hex(0x292F23);
    public static readonly Vector4 OkEdge = Hex(0x55753F);
    public static readonly Vector4 AmberTint = Hex(0x342E1F);
    /// <summary>An over-the-limit verdict's ground (its border <see cref="BrassDark"/>).</summary>
    public static readonly Vector4 OverTint = Hex(0x2E1C10);
    public static readonly Vector4 AmberEdge = Hex(0x7A6430);
    /// <summary>A plain chip's ground and border (a state with no colour of its own: yours, the AI's, timid).</summary>
    public static readonly Vector4 PlainTint = Hex(0x2B2F31);

    public static readonly Vector4 PlainEdge = Hex(0x6E7577);
    /// <summary>A hostile chip's ground and border: the enemy, a ship going down.</summary>
    public static readonly Vector4 HostileTint = Hex(0x2E1714);
    public static readonly Vector4 HostileEdge = Hex(0x7A3029);
    /// <summary>A light at night (docs/battle/night.md Q9: a beam, a flare, a ship showing one; <c>MapPalette.Light</c>'s
    /// hue): its chip's ink, ground and border.</summary>
    public static readonly Vector4 Lamp = Hex(0xFFD173);
    /// <summary>A ship not known for friend or foe (<c>MapPalette.Unknown</c>'s cream; docs/battle/night.md Q9).</summary>
    public static readonly Vector4 Unknown = Hex(0xF2E6B3);
    public static readonly Vector4 LampTint = Hex(0x2A2412);
    public static readonly Vector4 LampEdge = Hex(0x7A6230);
    /// <summary>A sound ship's bar (her flotation, her segment in a unit's): neutral, colour only where something's wrong.</summary>
    public static readonly Vector4 Sound = Hex(0x979B9D);
    /// <summary>The sides' shares in a ledger bar: friend, enemy.</summary>
    public static readonly Vector4 FriendBar = Hex(0xBDB7A4);
    public static readonly Vector4 HostileBar = Hex(0x9C3A30);

    /// <summary>Armour thin to thick.</summary>
    public static readonly Vector4[] ArmourRamp = [Hex(0xF3E3A1), Hex(0xE9B66B), Hex(0xDE9455), Hex(0xCC6F43), Hex(0xB24E36), Hex(0x8E3328)];

    /// <summary>The ramp's colour for a plate <paramref name="mm"/> thick (the mockup's steps).</summary>
    public static Vector4 Armour(double mm) => ArmourRamp[mm >= 260 ? 5 : mm >= 190 ? 4 : mm >= 140 ? 3 : mm >= 95 ? 2 : mm >= 60 ? 1 : 0];

    /// <summary>The weight groups' colours (ledger swatches and bars, the outliner's sections).</summary>
    public static Vector4 Group(string g) => g switch
    {
        "hull" => Hex(0xB7A27A),
        "armour" => Hex(0x9A86C9),
        "machinery" => Hex(0xE8772E),
        "fuel" => Hex(0x8A6A48),
        "armament" => Hex(0xD0503F),
        "superstructure" => Hex(0xA0A4A6),
        "fire_control" => FireControl,
        "misc" => Hex(0x7FA86A),
        _ => Faint,
    };

    public static uint U32(Vector4 c) => ImGui.ColorConvertFloat4ToU32(c);

    /// <summary>How far a raised fill (title strips, tabs, buttons, bars: <see cref="Pieces.Raised"/>) runs from its top
    /// to its bottom: the top this much lighter than its token, the bottom this much darker (a share of the colour).
    /// Very mild by design: about half the old Gunmetal gradients; 0 is the flat skin.</summary>
    public const float Sheen = 0.07f;

    /// <summary><paramref name="c"/> made lighter (<paramref name="k"/> &gt; 0) or darker by a share of itself, alpha kept.</summary>
    public static Vector4 Lift(Vector4 c, float k) =>
        new(Math.Clamp(c.X * (1 + k), 0, 1), Math.Clamp(c.Y * (1 + k), 0, 1), Math.Clamp(c.Z * (1 + k), 0, 1), c.W);

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
        C(ImGuiCol.PopupBg, Panel2);
        C(ImGuiCol.Border, ButtonEdge);
        C(ImGuiCol.Text, Text);
        C(ImGuiCol.TextDisabled, Muted);
        C(ImGuiCol.FrameBg, Inset);
        C(ImGuiCol.FrameBgHovered, Hex(0x1A1D1E));
        C(ImGuiCol.FrameBgActive, Hex(0x1A1D1E));
        C(ImGuiCol.Button, Button);
        C(ImGuiCol.ButtonHovered, ButtonHover);
        C(ImGuiCol.ButtonActive, DropBox);
        C(ImGuiCol.Header, Button);
        C(ImGuiCol.HeaderHovered, ButtonHover);
        C(ImGuiCol.HeaderActive, ButtonEdge);
        C(ImGuiCol.CheckMark, Brass);
        C(ImGuiCol.SliderGrab, Brass);
        C(ImGuiCol.Separator, Line);
        C(ImGuiCol.ScrollbarBg, Vector4.Zero);
        C(ImGuiCol.ScrollbarGrab, ButtonHover);
        C(ImGuiCol.ScrollbarGrabHovered, ButtonEdge);
        C(ImGuiCol.TableRowBg, Vector4.Zero);
        C(ImGuiCol.TableRowBgAlt, RowAlt);
        C(ImGuiCol.TableBorderLight, Line);
        C(ImGuiCol.TableBorderStrong, Edge);
        C(ImGuiCol.TableHeaderBg, HeaderBg);
        C(ImGuiCol.TextSelectedBg, Brass with { W = 0.35f });
        C(ImGuiCol.MenuBarBg, MenuBar);
        C(ImGuiCol.ModalWindowDimBg, Hex(0x0A0B0C, 0.55f));
        V(ImGuiStyleVar.FrameRounding, 0);
        V(ImGuiStyleVar.ChildRounding, 0);
        V(ImGuiStyleVar.PopupRounding, 1 * s);
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
