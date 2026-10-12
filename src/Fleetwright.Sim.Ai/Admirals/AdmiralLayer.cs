using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>The admirals (docs/battle/plan.md step 2.4): one per group, the layer above the captains. Each second,
/// before his captains think, an admiral works out every member's station from the group's formation and gives it to
/// the member's captain as a <see cref="CaptainOrder.Station"/>; the guide keeps whatever it's ordered, so one order to
/// the guide moves the group. He holds the guide's speed under what the slowest member makes, less a margin to close
/// up with. Ships that left a group get their captains back (if the admiral had them), and ships that are gone leave
/// their group. He commands his own ships, so he reads them as they are; other ships he'd see through the picture.
/// <para>The formations' geometry is the world's (<see cref="GroupSystem.StationOf"/>).</para></summary>
public sealed class AdmiralLayer(GroupSystem groups, CaptainLayer captains) : IAiLayer
{
    readonly List<int> gone = [];

    public bool Active => groups.Count > 0 || AnyAdmirals();
    public int PeriodMs(SimSettings s) => s.AdmiralPeriodMs;

    public void Order(in AiContext ctx, in Command c) => throw new ArgumentException("admirals take no orders yet: groups are the world's");

    public void Think(in AiContext ctx)
    {
        var ships = ctx.Ships;

        // ships no longer at sea leave their groups
        gone.Clear();
        for (int k = 0; k < groups.Count; k++)
        {
            var members = groups[k].Members;
            for (int i = 0; i < members.Count; i++)
                if (ships.SlotOf(members[i]) < 0)
                    gone.Add(members[i]);
        }
        foreach (int m in gone)
            ctx.World.Issue(GroupSystem.Remove(m));

        for (int k = 0; k < groups.Count; k++)   // indexed: a foreach over the interfaces would allocate
        {
            var g = groups[k];
            var members = g.Members;
            int guide = g.Guide;
            if (captains.IsAdmirals(guide))
                captains.Set(ctx, guide, CaptainOrder.None, 0, 0, 0, byAdmiral: true);   // the guide is the player's to steer

            // the guide's speed, within what the slowest member can make with some to spare
            double slowest = double.MaxValue;
            for (int i = 0; i < members.Count; i++)
                slowest = Math.Min(slowest, ships[ships.SlotOf(members[i])].Handling.Class.Motion.MaxSpeedKn);
            var lead = ships[ships.SlotOf(guide)];
            double cap = Math.Max(slowest - ctx.Settings.GroupSpeedMarginKn, 0);
            if (members.Count > 1 && lead.SpeedOrderKn > cap + 0.05)
                ctx.Orders.Speed(guide, cap);

            for (int i = 1; i < members.Count; i++)
            {
                var (target, astern, abeam) = GroupSystem.StationOf(g.Formation, g.SpacingM, members, i);
                captains.Set(ctx, members[i], CaptainOrder.Station, target, astern, abeam, byAdmiral: true);
            }
        }

        // captains still holding an admiral's order for a ship no longer in a group, or now its guide: theirs no more
        for (int i = captains.Count - 1; i >= 0; i--)
        {
            int e = captains.AdmiralsAt(i);
            if (e != 0 && (groups.Of(e) is not { } g || g.Guide == e))
                captains.Set(ctx, e, CaptainOrder.None, 0, 0, 0, byAdmiral: true);
        }
    }

    bool AnyAdmirals()
    {
        for (int i = 0; i < captains.Count; i++)
            if (captains.AdmiralsAt(i) != 0)
                return true;
        return false;
    }

    public void Hash(ref StateHash h)
    {
        // nothing of his own: the stations he gives are in the captains', the groups in the world's
    }

    public void Fill(ShipsView v)
    {
    }
}
