using Fleetwright.Shipgen;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A burst outside her (docs/battle/explosions.md "One outside"): wherever it is that isn't one of her spaces or a part that
/// confines it, whatever set it off. A shell bursting on a plate it didn't get through, or in the open over her deck or beyond her; a
/// store going off on deck; a shell that missed bursting in the sea or over it beside her (a near miss, E7); a torpedo at her plate
/// (4.6); a mine's or a bomb's to come. The geometry decides what follows, never the source:
/// <list type="bullet">
/// <item><b>the near field</b> from outside in (<see cref="GasNetwork.AddOutside"/>): her outside faces that face it holed by the one
/// rule, its plate, its standoff and how far water backs it; into her only through what it opened, with its products; the rooms in the
/// spaces it got into by the share of its reach there (<see cref="NearRooms"/>, the rule a burst within has);</item>
/// <item><b>its pieces</b> on her outside faces toward it (the side, and the weather deck under a burst over it), patch by patch, each
/// through its plate (<see cref="HullPieces"/>): the waterline band holed where the near field didn't take the plate already, and what
/// gets through into the space behind (<see cref="Behind"/>); and on the parts it sees over her hull (<see cref="PartsSeen"/>), wrecked
/// within the near field's reach as any burst's are, and their men, with its blast in the open as much as the sea doesn't back it;</item>
/// <item><b>the water</b>, where it's in the sea: its shock factor at her nearest wetted point and its bands (<see cref="NearMiss.Shock"/>),
/// and its bubble's whip (<see cref="HullGirder.Bubble"/>). In the air the sea's surface takes both to nothing;</item>
/// <item>fire, a magazine and the stores it reaches, as any burst's.</item>
/// </list></summary>
public static partial class Effects
{
    /// <summary>A charge going off outside her with no path of its own (a near miss's shell, a torpedo's warhead, a mine's or a bomb's
    /// to come): at a point in her frame, its pieces flying along dir at v, force the share of its filler that goes off (under 1 a
    /// low-order burst's, with its pieces). A tool may pass a span by component to have the pieces expected into each added to it.</summary>
    public static EffectsResult Outside(ShipClass c, ShipCondition k, in ShellTypeRow charge, P3 at, P3 dir, double v, double force, long ms,
        SimSettings s, ref RandomStream r, Span<double> intoParts = default)
    {
        var res = new EffectsResult { Part = -1, MagazineRoom = -1 };
        Casualties.Begin(k, ms);
        double w = Math.Max(charge.FillerKg * charge.FillerRe, 0);
        var b = new BurstSite
        {
            C = c, K = k, S = s, At = at, Dir = dir, V = v, Force = force, Share = 1,
            Charge = ChargeAt(c, k, -1, at, Math.Cbrt(w), force, s),
            Cell = -1, Part = -1, Frag = charge.Fragments?.Of(force < 1), Inside = false,
            Magazine = -1, Turret = -1, TurretSt = -1, ReadyMount = -1, ReadySt = -1, Via = MagazineVia.None,
        };
        Site(ref b, charge, ms, w, 0, new CrewSight(v, 1), -1, ref res, ref r, intoParts);
        Casualties.Draw(c, k, ms, s, ref res, ref r);
        return res;
    }

    /// <summary>A charge's near field where it is: in a space, backed by the liquid there; outside her, by the sea over it.</summary>
    static NearCharge ChargeAt(ShipClass c, ShipCondition k, int cell, P3 at, double size, double force, SimSettings s) =>
        NearField.Charge(size, force, cell >= 0 ? NearField.TampIn(c, k, cell, at, size, s)
            : NearField.Tamp(HitQuery.WaterOf(c, k.Attitude).At(at.X, at.Y) - at.Z, size, s), s);

    /// <summary>Whether a part lets a burst in it out as fast as it forms (an open mount, a funnel, a mast: its vents as big as its
    /// faces, <see cref="Blast.Open"/>): a burst there is outside her.</summary>
    static bool OpenPart(ShipClass c, int part, SimSettings s)
    {
        int m = c.MountOf(part);
        double share = m >= 0 && c.Mounts[m].Component == part && !c.Enclosed(m) ? 1
            : s.PartVentShare[Math.Min((int)c.Components[part].Kind, s.PartVentShare.Length - 1)];
        return s.VentAlpha * share >= s.OpenVentRatio;
    }

    /// <summary>The steps of a burst outside her (the class's summary).</summary>
    static void OutsideSite(ref BurstSite b, in ShellTypeRow shell, long ms, double w, int self, ref EffectsResult res, ref RandomStream r,
        Span<double> intoParts)
    {
        var (c, k, s) = (b.C, b.K, b.S);
        var water = HitQuery.WaterOf(c, k.Attitude);
        var p = b.At;
        double surface = water.At(p.X, p.Y), depth = Math.Max(surface - p.Z, 0), wf = b.Broken ? 0 : Math.Max(w * b.Force, 0);
        if (b.Part >= 0)
            PartBurstIn(ref b, ref res, ref r);

        // the near field from outside in, its products through what it opened, the rooms in the spaces it got into
        GasNetwork? net = null;
        if (!b.Broken && shell.FillerKg > 0 && b.Charge.ReachM > 0)
        {
            net = GasNetwork.Of(k, s, ms);
            res.Blast = net.AddOutside(p, b.Charge, depth > 0, shell.FillerKg * b.Force * s.ContactGasShare, shell);
            res.BreachM2 += res.Blast.HoleM2;
            NearRooms(ref b, net, -1, depth > 0 ? MagazineVia.Underwater : MagazineVia.Beside, ref res, ref r);
        }

        // its pieces: on her faces toward it and through them, and on what stands over her hull that it sees, each through the water on
        // its way (WaterDragM)
        double reach = NearMiss.ReachM(shell, s), openW = wf * (1 - b.Charge.Tamp);
        var frag = b.Frag;
        if (frag is not null)
            HullPieces(ref b, frag, water, surface, reach, ref res, ref r);
        PartsSeen(ref b, frag, net, water, surface, reach, openW, ref res, ref r, intoParts);
        if (b.Part >= 0 && frag is not null)   // the men in the open part it burst in
            Casualties.Pieces(c, k, Casualties.Part(b.Part), frag, p, b.Dir, b.V, b.Share, 0, default, openW, s);

        // the water: its shock and its bubble, where it's in the sea (in the air its surface takes both to nothing)
        if (wf > 0 && depth > 0)
        {
            double nsf = NearMiss.Nearest(c, k, p, surface, wf, water, s, out int nearCell, out var nearAt, out double nearMm);
            NearMiss.Shock(c, k, p, ms, wf, surface, water, nsf, nearAt, nearMm, s, ref res, ref r);
            if (nearCell >= 0)
                res.BubbleWhip = HullGirder.Of(c).Bubble(wf, p, Math.Max(Math.Sqrt((p.X - nearAt.X) * (p.X - nearAt.X) + (p.Y - nearAt.Y) * (p.Y - nearAt.Y) +
                    (p.Z - nearAt.Z) * (p.Z - nearAt.Z)), 0.5), water, s);
        }

        if (!b.Broken)
            StartFires(ref b, shell, w, ref res, ref r);
        MagazineLit(ref b, ms, ref res, ref r);
        // the high-explosive stores it reaches (EX6c), each its own explosion after this one
        res.Stores += OutsideStores(ref b, frag, net, water, surface, wf, self, ref r);
    }

    // ------------------------------------------------------------------ the near field in her spaces

    /// <summary>The rooms in the spaces a burst's near field got into, past its own (none outside her): each by the share of its floor
    /// the near field's sphere takes there (its reach as the plates on the way let it past, <see cref="GasNetwork.NearShareIn"/>, cut at
    /// the space's nearest point), made worse; a magazine among them lit by it among its charges (15 §2.3). What its pieces do in there
    /// is theirs (<see cref="SpacesNextDoor"/>, <see cref="Behind"/>): not counted twice.</summary>
    static void NearRooms(ref BurstSite b, GasNetwork net, int own, MagazineVia via, ref EffectsResult res, ref RandomStream r)
    {
        var (c, k, s) = (b.C, b.K, b.S);
        double reach = b.WreckM;
        if (!(reach > 0))
            return;
        for (int i = 0; i < c.Cells.Length; i++)
        {
            if (i == own || !net.NearReached(i))
                continue;
            double d = BoxDistance(c.Cells[i].Box, b.At), there = reach * net.NearShareIn(i);
            if (d >= there)
                continue;
            double rw = Math.Sqrt(there * there - d * d);
            foreach (int room in c.RoomsIn(i))
            {
                int node = c.RoomNode(room);
                if (res.Part < 0)
                    res.Part = node;
                if (r.NextDouble() >= NearChance(c, room, rw, b.Force))
                    continue;
                Count(ref res, k, node, k.Harm(node) ? k.State[node] : PartState.Ok);
                if (b.Magazine < 0 && c.Rooms[room].Kind == RoomKind.Magazine)
                    (b.Magazine, b.Via) = (room, via);
            }
        }
    }

    /// <summary>The share of a room's floor a near field's sphere of section rw takes, times the burst's force, at most 1.</summary>
    static double NearChance(ShipClass c, int room, double rw, double force)
    {
        double area = 0;
        foreach (int cell in c.Links(c.Rooms[room].Cells))
        {
            var b = c.Cells[cell].Box;
            area += (b.X1 - b.X0) * (b.Y1 - b.Y0);
        }
        return Math.Min(1, force * Math.PI * rw * rw / Math.Max(area, 1));
    }

    // ------------------------------------------------------------------ pieces on her outside

    /// <summary>A burst's pieces on her outside faces toward it (15 §6.3): the side toward it in patches (the waterline band apart, a
    /// metre below it, then up to the deck in steps a third of the distance, at most 3 m high and 6 m long), each a slab of its plating
    /// and any belt over it; and the weather deck under a burst over it, in patches the same. What gets through each face strikes the
    /// space behind it (<see cref="Behind"/>); the side's waterline band is holed by what gets through it, but where the near field took
    /// that plate already (the casing's pieces are most of a contact hole: damage/11 §3.1), not twice.</summary>
    static void HullPieces(ref BurstSite b, FragmentBurst frag, in Waterplane water, double surface, double reach, ref EffectsResult res, ref RandomStream r)
    {
        var (c, k, s) = (b.C, b.K, b.S);
        var p = b.At;
        var d = b.Dir;
        double v = b.V, share = b.Share;
        var cells = c.Cells;
        var pl = c.Plating;
        var plating = c.MaterialOf(pl.Material);
        int face = p.Y < 0 ? 0 : 1;
        double sg = face == 0 ? -1 : 1;
        for (int ci = 0; ci < cells.Length; ci++)
        {
            var bx = cells[ci].Box;
            // the side toward it, the burst outboard of it
            if (Blast.Outside(k, ci, face, out double sideMm, out _) > 0)
            {
                double xm = Math.Clamp(p.X, bx.X0, bx.X1);
                double wl = water.At(xm, sg * c.Hull.HalfWidth(xm, bx.Top));
                double z0 = Math.Max(bx.Base, wl - s.NearMissBandM), z1 = bx.Top;
                double hwNear = z1 > z0 ? c.Hull.HalfWidth(xm, Math.Clamp(p.Z, z0, z1)) : 0;
                if (z1 > z0 && sg * p.Y > hwNear && hwNear > 0.5)
                {
                    double zn = Math.Clamp(p.Z, z0, z1), dxn = p.X - xm, dyn = p.Y - sg * hwNear, dzn = p.Z - zn;
                    double near = Math.Sqrt(dxn * dxn + dyn * dyn + dzn * dzn);
                    if (near <= reach)
                    {
                        double step = Math.Max(near / 3, 1), dxp = Math.Min(step, 6), dzp = Math.Min(step, 3), lambda = 0;
                        int nx = Math.Max(1, (int)Math.Ceiling((bx.X1 - bx.X0) / dxp));
                        for (int ix = 0; ix < nx; ix++)
                        {
                            double x0 = bx.X0 + (bx.X1 - bx.X0) * ix / nx, x1 = bx.X0 + (bx.X1 - bx.X0) * (ix + 1) / nx, xc = (x0 + x1) / 2;
                            double side = Mild(plating, pl, xc);
                            for (double za = z0; za < z1 - 1e-6;)
                            {
                                // the band's edges, then steps
                                double zb = Math.Min(z1, za + dzp);
                                if (za < wl && zb > wl)
                                    zb = wl;
                                else if (za < wl + s.NearMissBandM && zb > wl + s.NearMissBandM)
                                    zb = wl + s.NearMissBandM;
                                double zc = (za + zb) / 2, hw = c.Hull.HalfWidth(xc, zc);
                                if (hw > 0.5)
                                {
                                    var slab = new Box(x0, x1, sg * hw - 0.05, sg * hw + 0.05, za, zb);
                                    var at = new P3(xc, sg * hw, zc);
                                    double mild = side + Belt(c, xc, zc);
                                    double drag = WaterDragM(p, surface, at, s);
                                    double l = share * frag.Hits(p, d, v, slab, mild, new P3(0, sg, 0), drag);
                                    if (l > 1e-4)
                                    {
                                        lambda += l;
                                        // the waterline band: holes at their height, each a ragged splinter's, where the near field didn't take it
                                        double dx = at.X - p.X, dy = at.Y - p.Y, dz = at.Z - p.Z, rr = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                                        if (zb <= wl + s.NearMissBandM + 1e-6 && !(NearField.HoleM(b.Charge, sideMm, rr, s) > 0) && Poisson(l, ref r) is int n and > 0)
                                        {
                                            double ob = double.RadiansToDegrees(Math.Acos(Math.Clamp(Math.Abs(dy) / Math.Max(rr, 1e-6), 0, 1)));
                                            double a = n * s.SplinterHoleK * frag.ThroughAreaM2(rr + drag, v, mild, ob);
                                            k.Hole(ci, a, at, n);
                                            res.BreachM2 += a;
                                        }
                                    }
                                }
                                za = zb;
                            }
                        }
                        res.SideThrough += lambda;
                        if (lambda > 1e-4)
                            Behind(ref b, frag, ci, new P3(0, sg, 0), lambda, 0, water, surface, ref res, ref r);
                    }
                }
            }
            // the weather deck over it, the burst over the deck
            if (p.Z > bx.Top && Blast.Outside(k, ci, 2, out double deckMm, out _) > 0)
            {
                double xm = Math.Clamp(p.X, bx.X0, bx.X1), hwd = c.Hull.HalfWidth(xm, bx.Top);
                double y0 = Math.Max(bx.Y0, -hwd), y1 = Math.Min(bx.Y1, hwd);
                if (y1 > y0)
                {
                    double dxn = p.X - xm, dyn = p.Y - Math.Clamp(p.Y, y0, y1), dzn = p.Z - bx.Top;
                    double near = Math.Sqrt(dxn * dxn + dyn * dyn + dzn * dzn);
                    if (near <= reach)
                    {
                        double step = Math.Min(Math.Max(near / 3, 1), 6), mild = plating.FragmentMildMm(deckMm), lambda = 0;
                        int nx = Math.Max(1, (int)Math.Ceiling((bx.X1 - bx.X0) / step)), ny = Math.Max(1, (int)Math.Ceiling((y1 - y0) / step));
                        var up = new P3(0, 0, 1);
                        for (int ix = 0; ix < nx; ix++)
                            for (int iy = 0; iy < ny; iy++)
                            {
                                double x0 = bx.X0 + (bx.X1 - bx.X0) * ix / nx, x1 = bx.X0 + (bx.X1 - bx.X0) * (ix + 1) / nx;
                                double ya = y0 + (y1 - y0) * iy / ny, yb = y0 + (y1 - y0) * (iy + 1) / ny;
                                lambda += share * frag.Hits(p, d, v, new Box(x0, x1, ya, yb, bx.Top - 0.05, bx.Top + 0.05), mild, up, 0);
                            }
                        res.SideThrough += lambda;
                        if (lambda > 1e-4)
                            Behind(ref b, frag, ci, up, lambda, mild, water, surface, ref res, ref r);
                    }
                }
            }
        }
    }

    /// <summary>What got through one of a space's outside faces (λ pieces): its vitals hit at their share; the parts in it the burst doesn't
    /// see over her hull, struck through the face and their own plates; and its men. Each through the face where the way to it crosses it
    /// (<see cref="FaceMild"/>: a side's plating and any belt there; the weather deck's, deckMild).</summary>
    static void Behind(ref BurstSite b, FragmentBurst frag, int cell, P3 normal, double lambda, double deckMild, in Waterplane water,
        double surface, ref EffectsResult res, ref RandomStream r)
    {
        var (c, k, s) = (b.C, b.K, b.S);
        var p = b.At;
        var box = c.Cells[cell].Box;
        foreach (int room in c.RoomsIn(cell))
        {
            int node = c.RoomNode(room);
            if (r.NextDouble() < 1 - Math.Exp(-lambda * Vital(c, room, s)))
            {
                Harm(ref res, k, node);
                if (res.Part < 0)
                    res.Part = node;
            }
        }
        var comps = c.Components;
        for (int j = 0; j < comps.Length; j++)
        {
            ref readonly var comp = ref comps[j];
            if (j == b.Part || !Overlaps(comp.Bounds, box) || Seen(c, comp.Bounds, p, water, out _))
                continue;
            var to = Nearest(comp.Bounds, p);
            double into = b.Share * frag.Hits(p, b.Dir, b.V, comp.Bounds, FaceMild(c, normal, deckMild, p, to) + PartMild(c, j, Armour(c, j)), normal,
                WaterDragM(p, surface, to, s));
            if (into > 1e-4 && r.NextDouble() < 1 - Math.Exp(-into * PartVital(c, j, s)))
                Harm(ref res, k, j);
        }
        int g = Casualties.Cell(c, cell);
        if (Casualties.Here(c, k, g) > 0)
        {
            var mid = new P3((box.X0 + box.X1) / 2, (box.Y0 + box.Y1) / 2, (box.Base + box.Top) / 2);
            var (pk, pc) = Casualties.PiecesRisk(box, Casualties.Cover(c, g, s), frag, p, b.Dir, b.V, b.Share, FaceMild(c, normal, deckMild, p, mid),
                normal, 0, s, WaterDragM(p, surface, mid, s));
            Casualties.Add(k, g, pk, pc);
            Steadiness.NearBurst(c, k, g, p, s);
        }
    }

    /// <summary>The parts a burst outside her sees over her hull (15 §2.4), and those in the spaces its near field got into: each within
    /// the reach wrecked by distance as any burst's parts are (<see cref="PartsAround"/>: destroyed within <see cref="SimSettings.DestroyedShare"/>
    /// of it, out of action within it), unless armour its pieces don't get through boxes it; beyond, damaged by the pieces that get into
    /// it. The men of those it sees: its pieces, through the water over it, and in the open its blast (openW, TNT kg: as much as the sea
    /// doesn't back it). A ready mount's charges lit as any burst's.</summary>
    static void PartsSeen(ref BurstSite b, FragmentBurst? frag, GasNetwork? net, in Waterplane water, double surface, double reach, double openW,
        ref EffectsResult res, ref RandomStream r, Span<double> intoParts)
    {
        var (c, k, s) = (b.C, b.K, b.S);
        var p = b.At;
        var comps = c.Components;
        double wreck = b.WreckM;
        for (int i = 0; i < comps.Length; i++)
        {
            if (i == b.Part)
                continue;
            ref readonly var comp = ref comps[i];
            var box = comp.Bounds;
            bool seen = comp.Bounds.Top > 0.05 && Seen(c, comp.Bounds, p, water, out box);
            if (!seen)
                box = comp.Bounds;
            double share = seen ? 1 : net is not null && wreck > 0 ? ReachedShare(c, net, comp.Bounds) : 0;
            if (!(share > 0))
                continue;
            double d = seen ? Math.Max(BoxDistance(box, p), Distance(c, i, p)) : Distance(c, i, p), wreckHere = wreck * share;
            if (d > Math.Max(seen ? reach : 0, wreckHere))
                continue;
            double raw = Armour(c, i), mild = PartMild(c, i, raw);
            double drag = WaterDragM(p, surface, Nearest(box, p), s);
            double into = seen && frag is not null ? b.Share * frag.Hits(p, b.Dir, b.V, box, mild, default, drag) : 0;
            if (seen)
            {
                res.UpperThrough += into;
                if (intoParts.Length > i)
                    intoParts[i] += into;
            }
            double u = into > 1e-4 ? r.NextDouble() : 1;
            bool pierced = u < 1 - Math.Exp(-into), struck = u < 1 - Math.Exp(-into * PartVital(c, i, s));
            if (!(raw > 0 && !pierced))
            {
                var state = d <= s.DestroyedShare * wreckHere ? PartState.Destroyed : d <= wreckHere ? PartState.Disabled : struck ? PartState.Damaged : PartState.Ok;
                if (state != PartState.Ok)
                {
                    bool worse = state == PartState.Damaged ? k.Harm(i) : k.Worsen(i, state);
                    Count(ref res, k, i, worse ? k.State[i] : PartState.Ok);
                    if (res.Part < 0)
                        res.Part = i;
                    int mm = c.MountOf(i);
                    if (worse && mm >= 0 && c.Mounts[mm].Component == i && c.Mounts[mm].ReadyRounds > 0 && state >= PartState.Disabled &&
                        r.NextDouble() < s.ReadyAmmoFireChance * b.Force)
                        ReadyChargesLit(ref b, i, mm, ref res);
                }
            }
            // its men: an open or shielded mount's stand open; the floor hidden from it, none. People take 16 §4.2's flux scale wherever they
            // are, as it was fitted (E7's free field for men in the open, 1/S, went with the one path), and in the open its blast
            if (!seen || frag is null || box.Base > comp.Bounds.Base + StandM || Casualties.Here(c, k, Casualties.Part(i)) <= 0)
                continue;
            int m = c.MountOf(i);
            double crewMild = PartMild(c, i, m >= 0 && c.Mounts[m].Component == i && !Casualties.Enclosed(c, m) ? 0 : raw);
            var mid = new P3((box.X0 + box.X1) / 2, (box.Y0 + box.Y1) / 2, (box.Base + box.Top) / 2);
            var (pk, pc) = Casualties.PiecesRisk(comp.Bounds, Casualties.Cover(c, Casualties.Part(i), s), frag, p, b.Dir, b.V,
                b.Share, crewMild, default, crewMild > 0 ? 0 : openW, s, WaterDragM(p, surface, mid, s));
            Casualties.Add(k, Casualties.Part(i), pk, pc);
            Steadiness.NearBurst(c, k, Casualties.Part(i), p, s);
        }
    }

    /// <summary>A man's height over a floor: a part whose floor is hidden from a burst by more has no men in its sight.</summary>
    const double StandM = 1.8;

    /// <summary>The share of a burst's near-field reach that got into the spaces a part stands in (the most of them; 0: none).</summary>
    static double ReachedShare(ShipClass c, GasNetwork net, in Box box)
    {
        double share = 0;
        for (int i = 0; i < c.Cells.Length; i++)
            if (Overlaps(c.Cells[i].Box, box))
                share = Math.Max(share, net.NearShareIn(i));
        return share;
    }

    /// <summary>The stores a burst outside her reaches (EX6c), each by the straight way to it: one on deck it sees over her side, by
    /// contact or its pieces through the water on their way; else one in a space its near field got into, or on the deck over one, by its
    /// pieces in through what it opened and her plating (and that deck).</summary>
    static int OutsideStores(ref BurstSite b, FragmentBurst? frag, GasNetwork? net, in Waterplane water, double surface, double w, int self,
        ref RandomStream r)
    {
        var (c, k, s) = (b.C, b.K, b.S);
        var p = b.At;
        int lit = 0;
        for (int l = 0; l < c.Launchers.Length; l++)
        {
            ref readonly var row = ref c.Launchers[l];
            if (l == self || !Stores.Live(c, k, l))
                continue;
            if (row.StoreCell < 0 && Seen(c, row.StoreBox, p, water, out var box))
                lit += Stores.Try(c, k, l, p, b.Dir, b.V, frag, b.Share, w, BoxDistance(box, p), 0, s, ref r, WaterDragM(p, surface, Nearest(box, p), s));
            else if (net is not null && (row.StoreCell >= 0 ? net.NearReached(row.StoreCell) : row.StoreDeck >= 0 && net.NearReached(row.StoreDeck)))
                lit += Stores.Try(c, k, l, p, b.Dir, b.V, frag, b.Share, w, BoxDistance(row.StoreBox, p),
                    c.Plating.ShellMm + (row.StoreCell < 0 ? c.Plating.StrengthDeckMm : 0), s, ref r);
        }
        return lit;
    }

    // ------------------------------------------------------------------ what a burst outside her sees

    /// <summary>The part of a box a burst outside her sees over her hull (15 §2.4: the leaning band clears a low side and hits what
    /// stands above it): above the sea; beside her, above the line from the burst over the deck edge on its side at the box's x (outboard
    /// of the edge, all of it); against her side below the deck, nothing inboard of the edge (her side and deck stand between); over her
    /// deck, above that deck. False when none of it shows.</summary>
    internal static bool Seen(ShipClass c, in Box b, P3 p, in Waterplane water, out Box seen)
    {
        double xm = Math.Clamp(p.X, b.X0, b.X1), sg = p.Y < 0 ? -1 : 1;
        double deck = c.Hull.DeckTop(xm), edge = c.Hull.HalfWidth(xm, deck);
        double inboard = edge - (sg > 0 ? b.Y1 : -b.Y0);   // how far inboard of the edge the box's near side stands
        double outboard = sg * p.Y - edge;                  // how far outboard of it the burst is
        double zmin = water.At(xm, sg * edge);
        if (inboard > 0 && outboard > 0.05)
            zmin = Math.Max(zmin, deck + (deck - p.Z) * inboard / outboard);
        else if (inboard > 0 && p.Z >= deck)
            zmin = Math.Max(zmin, deck);   // over her deck: what's under it is hidden
        else if (inboard > 0)
            zmin = double.PositiveInfinity;   // against her side, under her deck: her side and deck hide all inboard
        seen = b with { Base = Math.Max(b.Base, zmin) };
        return seen.Top > seen.Base + 0.05;
    }

    /// <summary>The water a piece goes through from a burst at p to a point (the sea's surface at seaZ over the burst), as the air that
    /// slows it as much, over the air it's counted in already: the share of the straight way under the surface times L₁ over the water's
    /// L₁, less one.</summary>
    internal static double WaterDragM(P3 p, double seaZ, P3 to, SimSettings s)
    {
        double za = seaZ - p.Z, zb = seaZ - to.Z;   // how deep each end is
        if (!(za > 0) && !(zb > 0))
            return 0;
        double dx = to.X - p.X, dy = to.Y - p.Y, dz = to.Z - p.Z, rr = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        double wet = za > 0 && zb > 0 ? 1 : za > 0 ? za / (za - zb) : zb / (zb - za);
        return wet * rr * (Fragments.DragL1 / s.WaterDragL1 - 1);
    }

    /// <summary>What a face stops of the pieces on their way from a burst at p to a point behind it, as mild steel: a side's plating and
    /// any belt where the way crosses it (normal's y the side), the weather deck's deckMild.</summary>
    static double FaceMild(ShipClass c, P3 normal, double deckMild, P3 p, P3 to)
    {
        if (normal.Z != 0)
            return deckMild;
        // where the way crosses her side: its breadth at the point's own station and height, near enough for which plate it is
        double sg = normal.Y, hw = c.Hull.HalfWidth(to.X, to.Z);
        double u = Math.Abs(to.Y - p.Y) > 1e-6 ? Math.Clamp((sg * hw - p.Y) / (to.Y - p.Y), 0, 1) : 0;
        double x = p.X + u * (to.X - p.X), z = p.Z + u * (to.Z - p.Z);
        return Mild(c.MaterialOf(c.Plating.Material), c.Plating, x) + Belt(c, x, z);
    }

    /// <summary>The hull's own side plating at x as mild steel against fragments: amidships or at the ends.</summary>
    static double Mild(in MaterialRow plating, in PlatingRow pl, double x) =>
        plating.FragmentMildMm(x >= pl.MidX0 && x <= pl.MidX1 ? pl.ShellMm : pl.ShellEndMm);

    /// <summary>The side armour over the hull's side at (x, z), as mild steel against fragments (0: none there).</summary>
    static double Belt(ShipClass c, double x, double z)
    {
        double mm = 0;
        foreach (ref readonly var sp in c.SidePlates)
        {
            if (x < sp.X0 || x > sp.X1 || z < sp.Bottom || z > sp.Top)
                continue;
            double t = !double.IsNaN(sp.BottomMm) && !double.IsNaN(sp.TaperFrom) && z < sp.TaperFrom && sp.TaperFrom > sp.Bottom
                ? sp.BottomMm + (sp.ThicknessMm - sp.BottomMm) * Math.Clamp((z - sp.Bottom) / (sp.TaperFrom - sp.Bottom), 0, 1)
                : sp.ThicknessMm;
            mm = Math.Max(mm, c.MaterialOf(sp.Material).FragmentMildMm(t));
        }
        return mm;
    }

    /// <summary>A Poisson count of mean λ (Knuth's product under 30; a rounded normal over it).</summary>
    static int Poisson(double lambda, ref RandomStream r)
    {
        if (lambda >= 30)
            return Math.Max(0, (int)Math.Round(lambda + Math.Sqrt(lambda) * r.NextGaussian()));
        double l = Math.Exp(-lambda), prod = r.NextDouble();
        int n = 0;
        while (prod > l)
        {
            n++;
            prod *= r.NextDouble();
        }
        return n;
    }
}
