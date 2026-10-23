using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Commanders;
using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Ai.Tasks;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>When a commander sends a flotilla in to attack (docs/battle/command.md 5.1d and 5.1g; the chain of command,
/// docs/battle/admirals-orders.md O7b): the one rule the side's commander (for his top units) and every force's admiral
/// (for the flotillas of his force) share. A flotilla is sent in by day on an enemy line his own heavy units are engaging,
/// its attack worth making (its matchup has it attacking, at doctrine's bar); by night (doctrine's NightAttack) on any
/// heavy enemy in reach worth it; declining a stronger enemy, by night only; broken off, once, to cover the others
/// breaking off; given up and going home, never. One whose attack was beaten off isn't sent in again. AI state:
/// hashed; nothing allocated once grown.</summary>
public sealed class FlotillaAttacks(MatchupJudge judge, HeldGroups held, Doctrine doctrine)
{
    readonly List<int> beatenOff = new(8), covered = new(8);   // units, sorted
    readonly MatchupSide one = new(), enemy = new();

    internal TaskLayer Tasks { get; set; } = null!;

    /// <summary>The enemy groups a side holds now.</summary>
    public ReadOnlySpan<HeldGroup> Held(in AiContext ctx, byte side) => held.Of(ctx, side);

    /// <summary>Whether a flotilla may be sent in: a squadron, a force of them, or a boat alone, with torpedoes left; not
    /// on an attack already, nor beaten off in one before; screening, on its orders, through with its task, or
    /// (<paramref name="alsoFree"/>) what its commander counts free besides.</summary>
    public bool Free(in AiContext ctx, Unit u, int slot, bool alsoFree = false)
    {
        var (kind, state, why) = Tasks.Of(u);
        if (why == TaskReason.BeatenOff)
            Add(beatenOff, u.Id);
        if (kind == TaskKind.Attack && state is TaskState.OnTask or TaskState.Engaged || beatenOff.BinarySearch(u.Id) >= 0)
            return false;
        if (!(kind is TaskKind.None or TaskKind.Screen || Tasks.Idle(u) || alsoFree))
            return false;
        return u.IsGroup && ctx.World.Groups.Get(u.GroupId) is { } g ? AdmiralLayer.AnyTorpedoes(ctx, g) : CaptainLayer.TorpedoCraft(ctx, ctx.Ships[slot], slot);
    }

    /// <summary>What a flotilla free to go in (<see cref="Free"/>) is sent in for now, as its side's commander sees the
    /// battle (<see cref="CommanderPhase.None"/>: no commander, its own commander's judgement alone), or
    /// <see cref="CommanderWhy.None"/>. <paramref name="engaging"/>: where the heavy units of its commander are (by day
    /// one must be within <see cref="SimSettings.FightCompareM"/> of the enemy line).</summary>
    public CommanderWhy SendIn(in AiContext ctx, byte side, CommanderPhase phase, CommanderReason reason, Unit u, Vec3d at,
        ReadOnlySpan<HeldGroup> enemyGroups, List<Vec3d> engaging)
    {
        switch (phase)
        {
            case CommanderPhase.Home:
                return CommanderWhy.None;
            case CommanderPhase.Retiring when reason == CommanderReason.BrokenOff:
                return covered.BinarySearch(u.Id) < 0 && Worth(ctx, side, u, at, enemyGroups, engaging) ? CommanderWhy.Cover : CommanderWhy.None;
            case CommanderPhase.Retiring:
                return Night(ctx, side, at) && Worth(ctx, side, u, at, enemyGroups, null) ? CommanderWhy.Night : CommanderWhy.None;
            case CommanderPhase.Night:
                return Night(ctx, side, at) && Worth(ctx, side, u, at, enemyGroups, null) ? CommanderWhy.Night : CommanderWhy.None;
            case CommanderPhase.Battle:
                return Worth(ctx, side, u, at, enemyGroups, engaging) ? CommanderWhy.Battle : CommanderWhy.None;
            case CommanderPhase.None:
                // no commander: his own commander's judgement, by night as by day
                return Night(ctx, side, at) && Worth(ctx, side, u, at, enemyGroups, null) ? CommanderWhy.Night
                    : Worth(ctx, side, u, at, enemyGroups, engaging) ? CommanderWhy.Battle : CommanderWhy.None;
            default:
                return CommanderWhy.None;   // no battle yet: the flotillas keep with the heavy units
        }
    }

    /// <summary>A flotilla sent in (its why from <see cref="SendIn"/>): one sent to cover a break off isn't sent again.</summary>
    public void Sent(Unit u, CommanderWhy why)
    {
        if (why == CommanderWhy.Cover)
            Add(covered, u.Id);
    }

    /// <summary>Whether it's night for torpedo craft where a flotilla is (the sun more than
    /// <see cref="SimSettings.NightSunElevationDeg"/> down) and doctrine sends them in by night.</summary>
    public bool Night(in AiContext ctx, byte side, Vec3d at) =>
        doctrine.OfGroup(null, side, DoctrineField.NightAttack) != 0 &&
        Light.SunElevationDeg(at, ctx.World.Time) < ctx.Settings.NightSunElevationDeg;

    /// <summary>Whether a flotilla's attack is worth making (docs/battle/command.md 5.1g): an enemy group within
    /// <see cref="SimSettings.CommanderAttackM"/> of it that its matchup has it attacking, at doctrine's bar or better; and,
    /// unless <paramref name="engaging"/> is null (by night), a heavy unit of its commander's engaging that group (within
    /// <see cref="SimSettings.FightCompareM"/> of it).</summary>
    public bool Worth(in AiContext ctx, byte side, Unit u, Vec3d at, ReadOnlySpan<HeldGroup> enemyGroups, List<Vec3d>? engaging)
    {
        double reach = ctx.Settings.CommanderAttackM, fight = ctx.Settings.FightCompareM, accept = judge.Bars(0, side).Accept;
        var pic = ctx.World.Sightings.Of(side)!;
        foreach (ref readonly var g in enemyGroups)
        {
            if ((g.Centre - at).Length > reach)
                continue;
            if (engaging != null)
            {
                bool near = false;
                foreach (var p in engaging)
                    near |= (p - g.Centre).Length <= fight;
                if (!near)
                    continue;
            }
            one.Clear();
            if (u.IsGroup && ctx.World.Groups.Get(u.GroupId) is { } own)
                judge.AddGroup(ctx.World, own, one);
            else if (ctx.Ships.SlotOf(u.Entity) is int slot and >= 0)
                one.AddOwn(ctx.World, judge.Matchups, slot);
            enemy.Clear();
            foreach (int i in held.Members(side, g))
                enemy.AddContact(ctx.World, judge.Matchups, side, pic[i]);
            if (one.Count == 0 || enemy.Count == 0)
                continue;
            var r = judge.Judge(ctx, 0, side, at, one, enemy);
            if ((r.Terms.Style & MatchupStyle.AttackByA) != 0 && r.Confidence >= accept)
                return true;
        }
        return false;
    }

    static void Add(List<int> set, int unit)
    {
        int i = set.BinarySearch(unit);
        if (i < 0)
            set.Insert(~i, unit);
    }

    public void Hash(ref StateHash h)
    {
        h.Add(beatenOff.Count);
        foreach (int u in beatenOff)
            h.Add(u);
        h.Add(covered.Count);
        foreach (int u in covered)
            h.Add(u);
    }
}
