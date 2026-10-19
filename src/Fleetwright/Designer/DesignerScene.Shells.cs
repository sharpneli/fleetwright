using System.Globalization;
using System.Numerics;
using Fleetwright.Shipgen;
using Fleetwright.Sim.Ships;
using ImGuiNET;
using T = Fleetwright.Designer.DesignerTheme;

namespace Fleetwright.Designer;

/// <summary>
/// The shell picker (docs/designer-ui/shells.md; docs/battle/shells-armour-damage.md S4), in both battery tables where
/// the material column was: a cell of the loadout's kinds and shares and its family, its hover the shells as fired, and
/// a click the picker: the family (a tree by navy, the design's first), a row per load in the magazine order (block,
/// share, rounds, mass, velocity, filler, fuze), the kinds to add and the default loadout. The edits are
/// <see cref="ShellEdits"/>'s; the read-outs are shipgen's own resolution (Ammunition.ResolveBattery), the rounds the
/// class's split (Loadout.Split).
/// </summary>
public sealed unsafe partial class DesignerScene
{
    /// <summary>One load as the picker shows it: its entry in the battery's shells, block, kind, share (whole percent),
    /// rounds a gun, mass, velocity, filler share and explosive, fuze.</summary>
    sealed record ShellLine(int Entry, string Ammo, string Kind, int Pct, int Rounds, double Kg, double Ms, double Filler, string Explosive, string Fuze);

    /// <summary>A battery's shells cell: which battery (main or secondary, its index, its name and gun), its family, the
    /// loads in the magazine order, its rounds a gun and the gun's velocity with its heaviest shell; Problem when the
    /// loadout doesn't resolve.</summary>
    sealed record ShellCell(bool Main, int K, string Name, string Gun, double CalibreMm, string Family, bool Default, int Rounds, double GunMs,
        ShellLine[] Lines, string? Problem = null);

    static readonly Vector4 ApColour = new(0.561f, 0.702f, 0.851f, 1), HeColour = new(0.878f, 0.478f, 0.373f, 1);
    static Vector4 KindColour(string kind) => kind switch { "ap" => ApColour, "common" => T.Brass, _ => HeColour };

    string? shellOpen;   // the dev hook's picker to open: "main:0", "sec:0", and ":fam" or ":blk:E" for its lists

    static ShellCell? ShellCellOf(Design d, bool main, int k, string name, double? roundsPerGun)
    {
        var b = (main ? d.Main : d.Secondary)![k];
        if (b.CalibreMm is not { } cal || b.CalibreLength is not { } len)
            return null;
        string gun = FormattableString.Invariant($"{cal:0.#}/{len:0.#}");
        int rounds = (int)Math.Round(roundsPerGun ?? b.RoundsPerGun ?? 0);
        double gunMs = b.MuzzleVelocityMs ?? Ammunition.DefaultMuzzleVelocity(len);
        bool dflt = b.Shells is not { Count: > 0 };
        try
        {
            var res = Ammunition.ResolveBattery(d, main ? "main" : "secondary", k);
            var battery = main ? Battery.Main : Battery.Secondary;
            var order = Enumerable.Range(0, res.Count).OrderBy(i => MagazineOrder.Rank(battery, res[i].Kind switch { "ap" => ShellKind.Ap, "common" => ShellKind.Common, _ => ShellKind.He })).ThenBy(i => i).ToArray();
            var split = Loadout.Split(rounds, order.Select(i => res[i].Share).ToArray());
            var lines = order.Select((i, o) =>
            {
                var r = res[i];
                string fuze = r.Fuze == "nose" ? "nose" : FormattableString.Invariant($"base {r.FuzeDelayS:0.000} s");
                return new ShellLine(i, r.Ammo, r.Kind, (int)Math.Round(r.Share * 100), split[o], r.MassKg, r.MuzzleVelocityMs, r.FillerKg / r.MassKg,
                    r.Explosive, fuze);
            }).ToArray();
            return new ShellCell(main, k, name, gun, cal, ShellEdits.FamilyOf(d, main, k), dflt, rounds, gunMs, lines);
        }
        catch (Exception x) when (x is KeyNotFoundException or NullReferenceException or InvalidOperationException)
        {
            return new ShellCell(main, k, name, gun, cal, "?", dflt, rounds, gunMs, [], "the loadout names a block the design doesn't have");
        }
    }

    static string Kg(double kg) => kg < 100 ? kg.ToString("0.0", CultureInfo.InvariantCulture) : N0(kg);

    /// <summary>The cell, a combo's size at the cursor: the loads as kind chips with their shares, the family at the
    /// right. Hover: the shells as fired; click: the picker.</summary>
    void ShellCellUi(ShellCell? c, float w)
    {
        var dl = ImGui.GetWindowDrawList();
        float h = ImGui.GetFrameHeight();
        var p = ImGui.GetCursorScreenPos();
        if (c is null)
        {
            Text(dl, UiFonts.Sans, p + new Vector2(P(4), (h - UiFonts.Sans.FontSize) / 2), T.Muted, "–");
            return;
        }
        string key = $"{(c.Main ? "main" : "sec")}:{c.K}";
        bool clicked = ImGui.InvisibleButton("##shells", new Vector2(w, h));
        bool hover = ImGui.IsItemHovered();
        bool open = ImGui.IsPopupOpen("##shellpick");
        dl.AddRectFilled(p, p + new Vector2(w, h), T.U32(T.Inset));
        dl.AddRect(p, p + new Vector2(w, h), T.U32(open ? T.Brass : hover ? T.Bevel : T.Edge));
        float x = p.X + P(4), cy = p.Y + h / 2, ch = UiFonts.SansSmall.FontSize + P(1);
        foreach (var l in c.Lines)
        {
            string t = $"{ShellEdits.KindLabel(l.Kind)} {l.Pct}";
            float tw = Width(UiFonts.SansSmall, t) + P(6);
            dl.AddRectFilled(new Vector2(x, cy - ch / 2), new Vector2(x + tw, cy + ch / 2), T.U32(KindColour(l.Kind)));
            Text(dl, UiFonts.SansSmall, new Vector2(x + P(3), cy - UiFonts.SansSmall.FontSize / 2), T.Ground, t);
            x += tw + P(3);
        }
        string fam = c.Problem != null ? "invalid" : c.Default ? "default" : c.Family;
        float fw = Width(UiFonts.SansSmall, fam);
        float fx = Math.Max(x + P(4), p.X + w - P(16) - fw);
        dl.PushClipRect(new Vector2(x, p.Y), new Vector2(p.X + w - P(14), p.Y + h), true);
        Text(dl, UiFonts.SansSmall, new Vector2(fx, cy - UiFonts.SansSmall.FontSize / 2), c.Problem != null ? T.Amber : T.Muted, fam);
        dl.PopClipRect();
        Twisty(dl, new Vector2(p.X + w - P(7), cy), true, T.Muted);
        if (hover && !open)
            ShellsTip(c);
        if (clicked || shellOpen?.StartsWith(key, StringComparison.Ordinal) == true)
            ImGui.OpenPopup("##shellpick");
        if (ImGui.IsPopupOpen("##shellpick"))
            ImGui.SetNextWindowPos(new Vector2(p.X + w, p.Y + h + P(2)), ImGuiCond.Appearing, new Vector2(1, 0));   // under the cell, its right edge
        if (ImGui.BeginPopup("##shellpick"))
        {
            ShellPicker(c, key);
            ImGui.EndPopup();
        }
    }

    static void ShellsTip(ShellCell c)
    {
        ImGui.BeginTooltip();
        if (c.Problem != null)
            ImGui.TextColored(T.Amber, c.Problem);
        else
        {
            ImGui.TextColored(T.Brass, $"{c.Gun}: the shells as fired, in the magazine order{(c.Default ? " (the default loadout)" : "")}");
            ImGui.PushFont(UiFonts.Mono);
            foreach (var l in c.Lines)
            {
                ImGui.TextColored(KindColour(l.Kind), $"{ShellEdits.KindLabel(l.Kind),-6}");
                ImGui.SameLine();
                ImGui.TextUnformatted(FormattableString.Invariant($"{l.Ammo,-34} {Kg(l.Kg),7} kg {l.Ms,5:0} m/s  {l.Fuze,-13} {l.Rounds} a gun"));
            }
            ImGui.PopFont();
        }
        ImGui.EndTooltip();
    }

    /// <summary>The picker's body (in its popup).</summary>
    void ShellPicker(ShellCell c, string key)
    {
        var d = doc.Current;
        bool main = c.Main;
        int k = c.K;
        string Label(string what) => $"Battery {c.Name} {what}";
        ImGui.PushFont(UiFonts.SansBold);
        ImGui.TextUnformatted($"{c.Name} · {c.Gun} · shells");
        ImGui.PopFont();
        ImGui.SameLine(0, P(10));
        ImGui.TextColored(T.Muted, "the design's own loadout: what the magazines are weighed with, and what a ship gets unless her admiral loads another");
        ImGui.SameLine(0, P(16));
        ImGui.PushFont(UiFonts.Mono);
        ImGui.TextUnformatted($"{c.Rounds} rounds a gun");
        ImGui.PopFont();
        if (c.Problem != null)
            ImGui.TextColored(T.Amber, c.Problem);

        // the family
        ImGui.Dummy(new Vector2(0, P(3)));
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(T.Muted, "Family");
        ImGui.SameLine(0, P(8));
        if (ComboButton("##fam", c.Default ? $"default: {ShellTemplates.DefaultFamily}" : c.Family, P(220)) || shellOpen == key + ":fam")
            ImGui.OpenPopup("##famtree");
        UnderLastItem("##famtree");
        if (ImGui.BeginPopup("##famtree"))
        {
            FamilyTree(d, c.Family, f => Edit(ShellEdits.SetFamily(d, main, k, f), Label($"shells: {f}")));
            ImGui.EndPopup();
        }
        ImGui.SameLine(0, P(8));
        ImGui.TextColored(T.Muted, $"sets each kind below to the family's block{(c.CalibreMm < 200 ? " (its light guns' where it has them)" : "")}");
        ImGui.SameLine(0, P(16));
        ImGui.BeginDisabled(c.Default);
        if (ImGui.Button("Default loadout"))
            Edit(ShellEdits.Default(d, main, k), Label("shells: the default loadout"));
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            Tip($"The default: the {ShellTemplates.DefaultFamily} family, {(main ? "70% AP and 30% HE" : "30% AP and 70% HE")}");

        // a row per load, in the magazine order
        ImGui.Dummy(new Vector2(0, P(3)));
        var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingFixedFit;
        if (ImGui.BeginTable("##loads", 10, flags))
        {
            foreach (var (head, wd) in new[] { ("", 20f), ("KIND", 46f), ("SHELL", 236f), ("SHARE", 88f), ("ROUNDS", 52f), ("MASS", 70f), ("M/S", 46f),
                         ("FILLER", 130f), ("FUZE", 100f), ("", 20f) })
                ImGui.TableSetupColumn(head, ImGuiTableColumnFlags.WidthFixed, P(wd));
            ImGui.PushFont(UiFonts.SansSmall);
            ImGui.TableHeadersRow();
            ImGui.PopFont();
            for (int o = 0; o < c.Lines.Length; o++)
            {
                var l = c.Lines[o];
                ImGui.PushID(l.Entry);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(o == 0 ? T.Brass : T.Muted, (o + 1).ToString(CultureInfo.InvariantCulture));
                if (ImGui.IsItemHovered())
                    Tip(o == 0 ? "Fired first: the lead (the magazine order)" : "Fired once the ones above are spent");
                ImGui.TableNextColumn();
                ImGui.TextColored(KindColour(l.Kind), ShellEdits.KindLabel(l.Kind));
                ImGui.TableNextColumn();
                if (ComboButton("##blk", l.Ammo, P(228)) || shellOpen == $"{key}:blk:{l.Entry}")
                    ImGui.OpenPopup("##blktree");
                UnderLastItem("##blktree");
                if (ImGui.BeginPopup("##blktree"))
                {
                    BlockTree(d, l.Kind, c.CalibreMm, l.Ammo, t => Edit(ShellEdits.SetBlock(d, main, k, l.Entry, t), Label($"{ShellEdits.KindLabel(l.Kind)}: {t}")));
                    ImGui.EndPopup();
                }
                ImGui.TableNextColumn();
                ImGui.BeginDisabled(c.Lines.Length < 2);
                int step = ShareSpinner(l.Pct);
                ImGui.EndDisabled();
                if (step != 0)
                    Edit(ShellEdits.SetShare(d, main, k, l.Entry, l.Pct + 5 * step), Label($"{ShellEdits.KindLabel(l.Kind)} share {l.Pct + 5 * step}%"),
                        key: $"shells:{key}:{l.Entry}");
                ImGui.PushFont(UiFonts.Mono);
                ImGui.TableNextColumn();
                RightText(l.Rounds.ToString(CultureInfo.InvariantCulture));
                ImGui.TableNextColumn();
                RightText($"{Kg(l.Kg)} kg");
                ImGui.TableNextColumn();
                RightText(l.Ms.ToString("0", CultureInfo.InvariantCulture));
                ImGui.PopFont();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(FormattableString.Invariant($"{l.Filler * 100:0.0}% {l.Explosive}"));
                ImGui.TableNextColumn();
                ImGui.PushFont(UiFonts.Mono);
                ImGui.TextUnformatted(l.Fuze);
                ImGui.PopFont();
                ImGui.TableNextColumn();
                ImGui.BeginDisabled(c.Lines.Length < 2);
                if (ImGui.SmallButton("x"))
                    Edit(ShellEdits.Remove(d, main, k, l.Entry), Label($"{ShellEdits.KindLabel(l.Kind)} removed"));
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    Tip("Remove: its share goes to the largest left");
                ImGui.PopID();
            }
            ImGui.EndTable();
        }

        // the kinds to add, and the order they're fired in
        ImGui.Dummy(new Vector2(0, P(3)));
        foreach (var kind in ShellEdits.Kinds)
        {
            ImGui.BeginDisabled(c.Lines.Any(l => l.Kind == kind));
            if (ImGui.Button($"+ {ShellEdits.KindLabel(kind)}"))
                Edit(ShellEdits.AddKind(d, main, k, kind), Label($"{ShellEdits.KindLabel(kind)} added"));
            ImGui.EndDisabled();
            ImGui.SameLine(0, P(4));
        }
        ImGui.SameLine(0, P(10));
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(T.Muted, $"fires {string.Join(", then ", c.Lines.Select(l => ShellEdits.KindLabel(l.Kind)))} (the magazine order: " +
                                   $"{(main ? "a main battery AP first" : "a secondary HE first")})");
        ImGui.Separator();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + P(780));
        ImGui.TextColored(T.Muted, FormattableString.Invariant($"The gun leaves at {c.GunMs:0} m/s with its heaviest shell; a lighter one leaves faster by the ¼ power ") +
                                   $"of the masses. Mass is the block's k·D³ at {c.CalibreMm:0.#} mm, so a calibre change keeps the loadout. Picking a template " +
                                   "copies its block into the design's ammunition; blocks no battery names are dropped.");
        ImGui.PopTextWrapPos();
        if (shellOpen?.StartsWith(key, StringComparison.Ordinal) == true)
            shellOpen = null;
    }

    /// <summary>An open popup's place: under the item just drawn (its button), when it opens.</summary>
    static void UnderLastItem(string popup)
    {
        if (ImGui.IsPopupOpen(popup))
            ImGui.SetNextWindowPos(new Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMax().Y + P(2)), ImGuiCond.Appearing);
    }

    static void RightText(string s)
    {
        float w = ImGui.CalcTextSize(s).X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - w - P(4)));
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(s);
    }

    /// <summary>A combo-looking button: its label and an arrow; true when clicked.</summary>
    static bool ComboButton(string id, string label, float w)
    {
        var p = ImGui.GetCursorScreenPos();
        float h = ImGui.GetFrameHeight();
        bool clicked = ImGui.InvisibleButton(id, new Vector2(w, h));
        bool hover = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + new Vector2(w, h), T.U32(T.Inset));
        dl.AddRect(p, p + new Vector2(w, h), T.U32(hover ? T.Bevel : T.Edge));
        dl.PushClipRect(p, p + new Vector2(w - P(16), h), true);
        Text(dl, UiFonts.Sans, p + new Vector2(P(5), (h - UiFonts.Sans.FontSize) / 2), hover ? T.Bright : T.Text, label);
        dl.PopClipRect();
        Twisty(dl, new Vector2(p.X + w - P(8), p.Y + h / 2), true, T.Muted);
        return clicked;
    }

    /// <summary>◀ n% ▶, the wheel stepping it too: −1, 0 or +1 for a step of 5%.</summary>
    static int ShareSpinner(int pct)
    {
        int step = 0;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        if (ImGui.ArrowButton("##less", ImGuiDir.Left))
            step = -1;
        bool hover = ImGui.IsItemHovered();
        ImGui.SameLine();
        string t = $"{pct}%";
        var p = ImGui.GetCursorScreenPos();
        float w = P(44), h = ImGui.GetFrameHeight();
        ImGui.InvisibleButton("##pct", new Vector2(w, h));
        hover |= ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(p, p + new Vector2(w, h), T.U32(T.Inset));
        Text(dl, UiFonts.Mono, new Vector2(p.X + w - P(4) - Width(UiFonts.Mono, t), p.Y + (h - UiFonts.Mono.FontSize) / 2), T.Bright, t);
        ImGui.SameLine();
        if (ImGui.ArrowButton("##more", ImGuiDir.Right))
            step = 1;
        hover |= ImGui.IsItemHovered();
        ImGui.PopStyleVar();
        float wheel = ImGui.GetIO().MouseWheel;
        if (hover && wheel != 0)
            step = Math.Sign(wheel);
        return step;
    }

    /// <summary>The families as a tree by navy: the design's navy first and open, any navy's next, the rest after; in
    /// each, oldest first, the one nearest the design's era marked.</summary>
    static void FamilyTree(Design d, string current, Action<string> pick)
    {
        string? own = ShellEdits.NavyOf(d);
        foreach (var (navy, families, nearest) in ShellEdits.Tree(d))
        {
            var flags = navy == own || families.Contains(current) ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            if (!ImGui.TreeNodeEx(ShellEdits.NavyName(navy), flags | ImGuiTreeNodeFlags.SpanAvailWidth))
                continue;
            foreach (var f in families)
            {
                string kinds = string.Join(" · ", ShellEdits.Kinds.Where(kd => ShellTemplates.All.Any(t => t.Group == f && t.Value.Kind == kd))
                    .Select(ShellEdits.KindLabel));
                if (ImGui.Selectable($"{f}##{navy}", f == current, ImGuiSelectableFlags.None, new Vector2(P(170), 0)) && f != current)
                    pick(f);
                ImGui.SameLine(0, P(8));
                ImGui.TextColored(T.Muted, kinds + (f == nearest && navy == own ? "   · her era" : ""));
            }
            ImGui.TreePop();
        }
    }

    /// <summary>A kind's blocks as a tree, navy and family: the design's navy first; each block with its mass at this
    /// calibre and its fuze, a light gun's block dimmed on a heavy gun (still pickable), its note on hover.</summary>
    static void BlockTree(Design d, string kind, double calibreMm, string current, Action<string> pick)
    {
        string? own = ShellEdits.NavyOf(d);
        string? curFamily = ShellEdits.TemplateOf(current)?.Group;
        foreach (var (navy, families, nearest) in ShellEdits.Tree(d))
        {
            var fams = families.Where(f => ShellTemplates.All.Any(t => t.Group == f && t.Value.Kind == kind)).ToList();
            if (fams.Count == 0)
                continue;
            var nflags = navy == own || fams.Contains(curFamily ?? "") ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            if (!ImGui.TreeNodeEx(ShellEdits.NavyName(navy), nflags | ImGuiTreeNodeFlags.SpanAvailWidth))
                continue;
            foreach (var f in fams)
            {
                var fflags = f == curFamily || navy == own && f == nearest ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
                if (!ImGui.TreeNodeEx(f, fflags | ImGuiTreeNodeFlags.SpanAvailWidth))
                    continue;
                foreach (var t in ShellTemplates.All.Where(t => t.Group == f && t.Value.Kind == kind))
                {
                    bool dim = t.Light && calibreMm >= 200;
                    if (dim)
                        ImGui.PushStyleColor(ImGuiCol.Text, T.Muted);
                    double kg = (t.Value.MassKKgM3 ?? 0) * Math.Pow(calibreMm / 1000, 3);
                    if (ImGui.Selectable(t.Name, t.Name == current, ImGuiSelectableFlags.None, new Vector2(P(250), 0)) && t.Name != current)
                        pick(t.Name);
                    if (dim)
                        ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered())
                        Tip($"{t.Name}{(t.Light ? " (a light guns' block)" : "")}\n{t.Note}");
                    ImGui.SameLine(0, P(8));
                    ImGui.TextColored(T.Muted, $"{Kg(kg)} kg · {(t.Value.Fuze?.Position == "nose" ? "nose" : "base")}");
                }
                ImGui.TreePop();
            }
            ImGui.TreePop();
        }
    }
}
