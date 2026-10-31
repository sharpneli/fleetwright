using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>The captains' guns and fighting (docs/battle/fighting.md 4.2c). A captain keeps his ship's fire order (at
/// will by default, hold fire, or engage a contact) and is the only one who puts its batteries on a target
/// (<see cref="GunnerySystem.Engage"/>, <see cref="GunnerySystem.Cease"/>), as he's the only one who orders its helm.
/// <list type="bullet">
/// <item><b>Fire at will:</b> at each think, for every ship off rails that's in an encounter or has a battery
/// engaged, each battery picks from its navy's contacts in sight within its reach (<see cref="SimSettings.FireReachShare"/>
/// of the range table's): one it can fire on now first (she bears, within its guns' reach), then one it can train on,
/// then the nearest, by the range she'll be at
/// <see cref="SimSettings.FireLeadS"/> on, longer by <see cref="SimSettings.FireCrowdShare"/> for each other ship's battery
/// of the kind already on her (one closing before one running; the fire spread) and, for a secondary, by
/// <see cref="SimSettings.FireSizeShare"/> for each step of her type's size (a boat before a battleship at much her
/// range, never one alongside). It keeps its target until it's unseen for <see cref="SimSettings.FireLostMs"/> or past
/// <see cref="SimSettings.FireKeepShare"/> of its reach, or another it can fire on where it can't, or is nearer so than
/// <see cref="SimSettings.FireSwitchShare"/> of it (an unknown's size taken as the held one's at most, or each ship coming
/// out of the haze would take the fire off the one held): no flicking between targets, as each shift starts the track again.
/// A secondary takes none it'd hit with fewer than doctrine's <see cref="DoctrineField.SecondaryHitFloor"/> of its shells
/// (the matchup's estimate), and drops one at that much less range: no splashing at the edge of its reach.
/// A ship seen done for (burning, silent and stopped: <see cref="Contact.DoneFor"/>) counts as further off the more done for
/// she looks, so any other in reach is taken first; with doctrine's <see cref="DoctrineField.Wrecks"/> ceasing, one
/// <see cref="SimSettings.DoneForCease"/> done for is no target at all (docs/battle/crew.md N5b).
/// With nothing to shoot at it ceases fire, so the ship can go back on rails.</item>
/// <item><b>Friend or foe</b> (docs/battle/night.md Q6): at will takes only a contact he'd open on (<see cref="Opens"/>):
/// one his navy knows for a foe, or an unknown as doctrine's <see cref="DoctrineField.Unknowns"/> says (fire on her; or
/// challenge her, <see cref="Challenge"/>, or hold, firing anyway when he's bold and no friend is about her); never one
/// taken for a friend. Torpedoes at will alike. A challenge made at him he answers with the enemy's reply by his navy's
/// <see cref="DoctrineField.AnswerChallenge"/> (<see cref="AnswerChallenges"/>).</item>
/// <item><b>Engage:</b> the chief batteries (<see cref="BatteryRow.Chief"/>: the main, or with none the heaviest guns) on
/// the contact while the navy holds it (then at will again); the others pick their own. <b>Hold fire:</b> every battery
/// ceases.</item>
/// <item><b>Who gave it:</b> the player's order to the ship, or an admiral's to his tree (its group kept with it). An
/// admiral's order he hands down again (a redistribution) only reaches the ships still holding his; a fresh one, from a
/// group just given a fire order, reaches all of them.</item>
/// <item><b>Fight:</b> a behaviour, as follow and station are: close to the band (where his unit's matchup is best, doctrine's
/// share of its chief guns' reach where that hardly matters), hold it with
/// them bearing, turn away when outgunned (<see cref="Behaviours.Fight"/>). A torpedo craft's (no gun of 6" or more, torpedoes left)
/// is the attack: close to <see cref="SimSettings.TorpedoAttackShare"/> of its torpedoes' fast setting whatever
/// guns it faces, fire, turn away for <see cref="SimSettings.TorpedoTurnAwayMs"/>, and again (docs/battle/fighting.md
/// 4.6).</item>
/// <item><b>Torpedoes at will</b> (4.6), beside the guns, unless holding fire: at the engaged ship, else the biggest
/// contact in sight, when its run for the fast setting is within <see cref="SimSettings.TorpedoFireShare"/> of its range
/// and the launchers bear; <see cref="SimSettings.TorpedoSpread"/> a firing, then <see cref="SimSettings.TorpedoRefireMs"/>
/// before the next.</item>
/// </list>
/// All of it runs in every clock tier at the captain's think: each second on the fine tick, each minute coarse
/// (docs/battle/coarse.md J2). A coarse captain also combs between thinks: woken when a running torpedo first comes into
/// his lookouts' sight, worked out ahead along both tracks, and when his comb is done (<see cref="NextTrackSeen"/>,
/// <see cref="WatchTracks"/>, J3).</summary>
public sealed partial class CaptainLayer
{
    readonly List<int> fighters = new(64), watched = new(64);
    readonly LineOfFire lines = new();
    readonly TorpedoWatch torpedoWatch = new();
    double[] underFire = [];   // by slot: the range of the nearest enemy battery on her, m (0: none), as of this think
    bool anyUnderFire;
    readonly List<(int Ship, double AsternM, double AbeamM)> unitLine = new(16);
    int fighting;          // ships with a battery engaged after the last pass
    bool fightingFine;     // any of them on the fine tick

    /// <summary>A ship's fire order, its target (Engage) and the group it came from (0: the ship's own).</summary>
    public (FireOrder Order, int Target, int From) FireOf(int entity)
    {
        int i = Find(entity);
        return i >= 0 ? (captains[i].Fire, captains[i].FireTarget, captains[i].FireFrom) : (FireOrder.AtWill, 0, 0);
    }

    /// <summary>Whether he has anything to watch for the guns: an encounter open, or a battery engaged.</summary>
    bool Fighting => encounters.Count > 0 || fighting > 0;

    /// <summary>A fire order for his ship (a command): the player's always; an admiral's when it's fresh (X 1) or he holds
    /// that admiral's order already.</summary>
    void FireOrdered(in AiContext ctx, in Command c, OrderFrom by)
    {
        int e = c.Entity;
        bool superior = by.Source == OrderSource.Superior;
        int from = superior ? (int)c.Y : 0;
        ref var cap = ref Get(e);
        if (superior && c.X == 0 && cap.FireFrom != from)
            return;   // the player's order, or another admiral's, stands
        cap.Fire = (AiCommand)c.Code switch
        {
            AiCommand.HoldFire => FireOrder.Hold,
            AiCommand.Engage => FireOrder.Engage,
            _ => FireOrder.AtWill,
        };
        (cap.FireTarget, cap.FireFrom) = (cap.Fire == FireOrder.Engage ? (int)c.A : 0, from);
        if (cap.Fire == FireOrder.Hold && ctx.Ships.SlotOf(e) is int slot and >= 0 && ctx.Ships.Combat.Engaged(slot) > 0)
            ctx.Issue(GunnerySystem.Cease(e));
    }

    /// <summary>Each fighting ship's batteries put on their targets (and a ship alone's doctrine taken up).</summary>
    void FirePass(in AiContext ctx)
    {
        var store = ctx.Ships;
        foreach (int e in torpedoWaiting)
            if (Find(e) is int wi and >= 0)
                captains[wi].TorpedoWait = false;
        torpedoWaiting.Clear();   // the think finds them again
        fighters.Clear();
        for (int k = 0; k < store.OffRailsCount; k++)
        {
            int slot = store.OffRailsSlot(k);
            if (encounters.Of(slot, store[slot].Entity) != 0 || store.Combat.Engaged(slot) > 0)
                fighters.Add(slot);
        }
        (fighting, fightingFine) = (0, false);
        CountOn(store);
        foreach (int slot in fighters)
        {
            // every tier, at its think: the fine tick's second, the coarse step's minute (docs/battle/coarse.md J2)
            var own = store[slot];
            if (store.Combat.Condition(slot).IsGivenUp)
                continue;   // given up (docs/battle/striking.md): her guns stood down, her captain's call made
            ref var cap = ref Get(own.Entity);
            if (cap.TorpedoHeld && !groups.InLine(own.Entity))
                cap.TorpedoHeld = false;   // out of her flotilla's line: her torpedoes her own again
            FallOut(ctx, ref cap, own, slot);
            OutOfRounds(ctx, ref cap, own);
            Comb(ctx, ref cap, own);   // coarse, also when a track comes into sight between thinks (WatchTracks)
            TurnFromLight(ctx, ref cap, own, slot);   // a light on her in the dark (docs/battle/night.md Q7)
            Darken(ctx, ref cap, own, slot);          // darkened: her guns hold till she's fired upon (Q9)
            TakeUpStanding(ctx, ref cap, own, slot);
            Aim(ctx, ref cap, own, slot);
            AnswerChallenges(ctx, own);
            NightLights(ctx, ref cap, own, slot);   // star shell or searchlights on what the guns can't see (docs/battle/night.md Q4, Q5)
            Crew(ctx, own, slot, fighting: true);   // where her men stand (docs/battle/crew.md N3a)
            if (store.Combat.Engaged(slot) > 0)
            {
                fighting++;
                fightingFine |= own.Tier == ClockTier.Fine;
            }
        }
    }

    /// <summary>The coarse tier's next wake for the torpedo watch before <paramref name="untilMs"/> (the captains' next
    /// think), ms, or long.MaxValue: the first moment a running torpedo comes into sight of a coarse ship that's fighting
    /// (in an encounter, or a battery engaged), or a comb of one of them is done (docs/battle/coarse.md J3).</summary>
    internal long NextTrackSeen(in AiContext ctx, long untilMs)
    {
        CoarseFighters(ctx);
        long next = torpedoWatch.NextSeen(ctx, watched, untilMs);
        foreach (int slot in watched)
            if (Find(ctx.Ships[slot].Entity) is int i and >= 0)
            {
                ref readonly var c = ref captains[i];
                if (c.HeldUntilMs > ctx.NowMs)
                    next = Math.Min(next, c.HeldUntilMs);
                if (Zigzagging(c, ctx.NowMs) && c.ZigNextMs > ctx.NowMs)
                    next = Math.Min(next, c.ZigNextMs);
            }
        return next;
    }

    /// <summary>The coarse watch's wake: each coarse fighting ship's lookouts, by the think's own test (<see cref="Comb"/>),
    /// and her zigzag's next leg where one ends (J4), from the course her last think evaded from.</summary>
    internal void WatchTracks(in AiContext ctx)
    {
        CoarseFighters(ctx);
        foreach (int slot in watched)
        {
            var own = ctx.Ships[slot];
            ref var cap = ref Get(own.Entity);
            Comb(ctx, ref cap, own);
            if (Zigzagging(cap, ctx.NowMs + 1) && cap.ZigNextMs <= ctx.NowMs)
            {
                if (cap.HeldUntilMs <= ctx.NowMs)
                    Give(ctx, own, Leg(ctx, own, cap.ZigBaseDeg), own.SpeedOrderKn);
                cap.ZigNextMs = LegEndMs(ctx, own);   // combing, the leg passes unsteered: never the same moment twice
            }
        }
    }

    long thinkMs;   // the captains' last think

    /// <summary>Whether a coarse captain's zigzag runs on between thinks: set by his last think, with a leg ending.</summary>
    bool Zigzagging(in Captain c, long nowMs) => c.ZigNextMs != 0 && c.ZigThinkMs == thinkMs && c.ZigNextMs >= nowMs - 1;

    void CoarseFighters(in AiContext ctx)
    {
        var store = ctx.Ships;
        watched.Clear();
        for (int k = 0; k < store.CoarseCount; k++)
        {
            int slot = store.CoarseSlot(k);
            if (encounters.Of(slot, store[slot].Entity) != 0 || store.Combat.Engaged(slot) > 0)
                watched.Add(slot);
        }
    }

    bool candDark;   // darkened in the dark, as of this ship's think (docs/battle/night.md Q9)

    /// <summary>Darkened (doctrine's <see cref="DoctrineField.Darkened"/>, docs/battle/night.md Q9) in the dark: his guns
    /// hold until she or a ship of her squadron is fired upon (an enemy battery on her), then fight by his orders for
    /// <see cref="SimSettings.DarkenedFreeS"/> after the last time; never while the player keeps her beams on.</summary>
    void Darken(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        candDark = doctrine.Of(own.Entity, own.Side, DoctrineField.Darkened) != 0 && InDark(ctx, own, out _);
        if (!candDark)
        {
            cap.Holding = false;
            return;
        }
        if (FiredUpon(ctx.Ships, own, slot))
            cap.FiredOnMs = ctx.NowMs;
        cap.Holding = !cap.BeamsHeld && (cap.FiredOnMs == 0 || ctx.NowMs - cap.FiredOnMs > ctx.Settings.DarkenedFreeS * 1000);
    }

    /// <summary>Whether an enemy battery is on her or a ship of her squadron, as of this think.</summary>
    bool FiredUpon(ShipStore store, ShipState own, int slot)
    {
        if (slot < underFire.Length && underFire[slot] > 0)
            return true;
        if (groups.Of(own.Entity) is { IsForce: false } g)
            foreach (int m in g.Members)
                if (store.SlotOf(m) is >= 0 and var ms && ms < underFire.Length && underFire[ms] > 0)
                    return true;
        return false;
    }

    /// <summary>A ship alone whose doctrine is to fight (docs/battle/command.md "Doctrine": Engage) takes up Fight when
    /// it first meets an enemy in sight in an encounter; it's armed again once it's out of encounters. Not while the
    /// player holds her (<see cref="Whose.Player"/>): the player's order is what she does.</summary>
    void TakeUpStanding(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        if (cap.Whose == Whose.Player || cap.Order == CaptainOrder.Home || Tasks?.Avoids(Unit.Ship(own.Entity)) == true || (Engage)doctrine.Of(own.Entity, own.Side, DoctrineField.Engage) != Engage.Fight)
            return;
        if (encounters.Of(slot, own.Entity) == 0)
        {
            cap.Met = false;
            return;
        }
        if (cap.Met || groups.InLine(own.Entity) || seen.Near(ctx.World, own.Side, own.Position, ctx.Settings.FightCompareM).Length == 0)
            return;
        cap.Met = true;
        (cap.Order, cap.Target, cap.DistanceM, cap.AbeamM, cap.FightSide, cap.FightEnemy, cap.FightAway) = (CaptainOrder.Fight, 0, 0, 0, 0, 0, false);
        Recount();
    }

    // a ship's candidates, found once for all its batteries: each contact in sight within its widest reach, its entity
    // and slot, range (m), the range it'll be at FireLeadS on (m), bearing from ahead and the rank of its type for a
    // secondary battery
    int[] candEntity = new int[32], candRank = new int[32], candSlot = new int[32], candPic = new int[32];
    double[] candRange = new double[32], candLead = new double[32], candBearing = new double[32], candDone = new double[32];
    // and how much each still matters as a target (BL2a: quiet and making away, down to doctrine's Finish), and how much
    // his side's aim wants her (docs/battle/aims.md A3: what it wants sunk, or one that endangers him; 1 for neither)
    double[] candMatters = new double[32], candWant = new double[32];
    Vec3d[] candPos = new Vec3d[32];
    sbyte[] candFoul = new sbyte[32];   // its line of fire: -1 not asked yet, 0 clear, 1 foul
    int candCount;
    // the ship's doctrine on unknowns and her aggression, as of this think; the nearest unknown in reach she won't fire on
    // (an entity, 0: none), found with her candidates, for a challenge (docs/battle/night.md Q6)
    Unknowns candUnknowns;
    double candBold, candFinish;
    int candWanted;   // the kinds his side's aim wants sunk first (AimBook.Wanted), as of this think
    int candUnknown;
    double candUnknownM;
    double[] reaches = new double[16];
    // by target slot, the batteries on her (chief, and the rest), kept as this pass's orders go out; and the ship's own
    // batteries' targets as its captain's orders leave them
    int[] onChief = [], onOther = [];
    int[] ownTargets = new int[8];

    /// <summary>His ship's batteries on what his fire order wants.</summary>
    void Aim(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var cls = own.Handling.Class;
        var pic = ctx.World.Sightings.Of(own.Side);
        if (cap.Fire == FireOrder.Engage && (pic == null || pic.IndexOf(cap.FireTarget) < 0 || pic.Out(cap.FireTarget)))
            (cap.Fire, cap.FireTarget) = (FireOrder.AtWill, 0);   // gone from the picture, or seen going down: at will again
        var reach = Reaches(ctx, ctx.Ships.Combat.Loadout(slot));
        candCount = -1;   // found when a battery first needs them
        candUnknowns = (Unknowns)doctrine.Of(own.Entity, own.Side, DoctrineField.Unknowns);
        candBold = doctrine.Of(own.Entity, own.Side, DoctrineField.Aggression);
        candFinish = doctrine.Of(own.Entity, own.Side, DoctrineField.Finish);
        candWanted = Book?.Wanted(own.Side) ?? 0;
        if (ownTargets.Length < cls.Batteries.Length)
            ownTargets = new int[Math.Max(cls.Batteries.Length, ownTargets.Length * 2)];
        for (int b = 0; b < cls.Batteries.Length; b++)
            ownTargets[b] = ctx.Ships.Combat.Target(slot, b);
        for (int b = 0; b < cls.Batteries.Length; b++)
        {
            int current = ownTargets[b];
            // darkened and holding (docs/battle/night.md Q9): only the player's own Engage fires
            int want = cap.Fire switch
            {
                FireOrder.Hold => 0,
                FireOrder.Engage when cls.Batteries[b].Chief => cap.Holding && cap.FireFrom != 0 ? 0 : cap.FireTarget,
                _ => cap.Holding ? 0 : Pick(ctx, pic, own, slot, cls, b, reach[b], reach[^1], current),
            };
            Shells(ctx, ref cap, own, slot, b, want, pic);
            if (want == current)
                continue;
            ctx.Issue(want == 0 ? GunnerySystem.Cease(own.Entity, b) : GunnerySystem.Engage(own.Entity, b, want));
            // the next ships' picks see it at once, so a line doesn't all take the same one in the same second
            var on = cls.Batteries[b].Chief ? onChief : onOther;
            if (ctx.Ships.SlotOf(current) is int cs and >= 0 && cs < on.Length)
                on[cs] = Math.Max(on[cs] - 1, 0);
            if (ctx.Ships.SlotOf(want) is int ws and >= 0 && ws < on.Length)
                on[ws]++;
            ownTargets[b] = want;
        }
        Torpedoes(ctx, ref cap, own, slot, pic);
        if (pic != null)
            Challenge(ctx, cap, own, pic, reach[^1]);
    }

    /// <summary>His challenge (docs/battle/night.md Q6): with doctrine's Unknowns at Challenge, at the nearest ship in his
    /// widest reach he won't fire on for not knowing her, once she's within doctrine's <see cref="DoctrineField.ChallengeM"/>
    /// (Q7: farther off he keeps dark), when he has none open and no ship of his navy's is asking her already. Never while
    /// holding his fire, nor darkened in the dark (Q9: no lamp).</summary>
    void Challenge(in AiContext ctx, in Captain cap, ShipState own, Picture pic, double widest)
    {
        if (cap.Fire == FireOrder.Hold || candDark || ctx.World.Challenges.ChallengeOf(own.Entity).Target != 0)
            return;
        if (candCount < 0)
            Candidates(ctx, pic, own, widest);
        if (candUnknown != 0 && candUnknowns == Unknowns.Challenge && candUnknownM <= doctrine.Of(own.Entity, own.Side, DoctrineField.ChallengeM) &&
            !ctx.World.Challenges.Asking(own.Side, candUnknown))
            ctx.Issue(ChallengeSystem.Challenge(own.Entity, candUnknown));
    }

    /// <summary>His answers to the challenges made at him (docs/battle/night.md Q6), each once his navy has seen its
    /// challenger since it began: he gives the enemy's reply with his navy's <see cref="DoctrineField.AnswerChallenge"/>
    /// share (drawn once a challenge, from the world's seed), so the challenger takes him for a friend.</summary>
    void AnswerChallenges(in AiContext ctx, ShipState own)
    {
        var challenges = ctx.World.Challenges;
        if (!challenges.At(own.Entity, 0, out _, out _, out _))
            return;
        double share = doctrine.Of(own.Entity, own.Side, DoctrineField.AnswerChallenge);
        var pic = ctx.World.Sightings.Of(own.Side);
        if (share <= 0 || pic == null)
            return;
        for (int k = 0; challenges.At(own.Entity, k, out int by, out long start, out bool answered); k++)
        {
            if (answered || pic.IndexOf(by) is not (>= 0 and var ci) || pic[ci].SeenMs < start)
                continue;
            var r = ctx.World.Stream("challenge answer").Derive((ulong)(uint)own.Entity << 32 ^ (ulong)start);
            if (r.NextDouble() < share)
                ctx.Issue(ChallengeSystem.Answer(own.Entity, by));
        }
    }

    /// <summary>Whether he'd open on a contact where his navy has her (docs/battle/night.md Q6): one known for a foe; never
    /// one taken for a friend; an unknown when doctrine's Unknowns is Fire, or when he's bold (his aggression a over 0)
    /// and no friend of his but his own unit is within <see cref="SimSettings.UnknownFriendM"/> × (1 − a) of her.</summary>
    bool Opens(in AiContext ctx, ShipState own, in Contact c, Vec3d at)
    {
        if (c.Foe)
            return true;
        if (c.TakenForFriend(ctx.NowMs, ctx.Settings) || candUnknowns != Unknowns.Fire && candBold <= 0)
            return false;
        return candUnknowns == Unknowns.Fire ||
               !lines.FriendNear(ctx, groups, own, at, Math.Min(ctx.Settings.UnknownFriendM * (1 - candBold), ctx.Settings.LineOfFireCellM));
    }

    readonly MatchupSide shellTarget = new();

    /// <summary>His battery's shell for its target (docs/battle/shell-choice.md A3), by doctrine's Shells: each kind it
    /// has rounds of judged by what a round does to her as his navy has her made out (<see cref="Matchup.PerRound"/>
    /// over the classes she may be, <see cref="MatchupSide.AddContact"/>), at the range; the load ordered changes to the
    /// best only past <see cref="SimSettings.ShellSwitchGain"/> of the one loaded, as each change costs the round in the
    /// gun. Judged again when the target, what's made out of her or the range's step changes, or the kind ordered runs
    /// out.</summary>
    void Shells(in AiContext ctx, ref Captain cap, ShipState own, int slot, int b, int target, Picture? pic)
    {
        var combat = ctx.Ships.Combat;
        var cls = own.Handling.Class;
        var js = cap.Shells ??= new ShellJudged[cls.Batteries.Length];
        // what's left in the magazines and doctrine looked at every ShellCheckMs (a round's count changes only as she
        // fires); the target as it changes
        long now = ctx.World.Time.Ms;
        bool check = now >= js[b].CheckMs;
        if (check)
            js[b].CheckMs = now + ShellCheckMs;
        // the player's kind for the main guns holds while they have rounds of it (A5); spent, the choice is his again
        if (cap.ShellHeld > 0 && cls.Batteries[b].Chief)
        {
            if (!check)
                return;
            var held = (ShellKind)(cap.ShellHeld - 1);
            if (Has(combat, slot, cls, b, held))
            {
                if (combat.Ordered(slot, b) != held)
                    ctx.Issue(GunnerySystem.Load(own.Entity, b, held));
                return;
            }
            (cap.ShellSpent, cap.ShellHeld) = (cap.ShellHeld, 0);
            Array.Clear(js);
            js[b].CheckMs = now + ShellCheckMs;
        }
        if (target == 0 || pic == null || pic.IndexOf(target) is not (int ci and >= 0))
            return;
        ref readonly var c = ref pic[ci];
        int step = (int)(Earth.Distance(own.Position, c.Position) / ShellJudgeStepM);
        ref var j = ref js[b];
        bool same = j.Target == target && j.TakenFor == c.TakenFor && j.Step == step && j.Level == c.Level && j.Type == c.Type;
        if (same && !check)
            return;
        if ((ShellUse)doctrine.Of(own.Entity, own.Side, DoctrineField.Shells) != ShellUse.ByTarget)
        {
            // by the magazine order: nothing ordered (the player's kind, handed back, goes too)
            if (combat.Ordered(slot, b) != null && cap.ShellHeld == 0)
                ctx.Issue(GunnerySystem.Load(own.Entity, b, null));
            return;
        }
        var ordered = combat.Ordered(slot, b);
        bool spent = ordered is { } o && !Has(combat, slot, cls, b, o);
        if (same && !spent)
            return;
        (j.Target, j.TakenFor, j.Step, j.Level, j.Type) = (target, c.TakenFor, step, c.Level, c.Type);
        var lo = combat.Loadout(slot);
        MatchupGun? gun = null;
        foreach (var g in Judge.Matchups.Profile(ctx.World, lo).Guns)
            if (g.Battery == b)
                gun = g;
        if (gun == null || gun.Kinds.Length < 2)
            return;
        shellTarget.Clear();
        shellTarget.AddContact(ctx.World, Judge.Matchups, own.Side, c);
        if (shellTarget.Count == 0)
            return;
        double range = Earth.Distance(own.Position, c.Position);
        var loaded = ordered is { } k0 && !spent ? k0 : Lead(combat, slot, lo, b);
        double at = 0, best = 0;
        ShellKind? pick = null;
        foreach (var k in gun.Kinds)
        {
            if (!Has(combat, slot, cls, b, k.Shell.Kind))
                continue;
            double v = 0;
            foreach (ref readonly var e in shellTarget.Entries)
                v += e.Count * Matchup.PerRound(k, e, range, ctx.World.Settings);
            if (k.Shell.Kind == loaded)
                at = v;
            if (v > best)
                (best, pick) = (v, k.Shell.Kind);
        }
        // a kind ordered and spent is replaced by the best, even the one its guns fall back on
        if (pick is { } p && (spent || p != loaded && best > ctx.World.Settings.ShellSwitchGain * at))
            ctx.Issue(GunnerySystem.Load(own.Entity, b, p));
    }

    /// <summary>How often a captain looks at his batteries' rounds and doctrine for their shells, ms; and the range
    /// step he judges a target's shell again at, m (the kinds' worth moves slowly with range).</summary>
    const long ShellCheckMs = 10_000;
    const double ShellJudgeStepM = 1000;

    /// <summary>Whether any of a battery's mounts has rounds of a kind.</summary>
    static bool Has(CombatStore combat, int slot, ShipClass cls, int b, ShellKind kind)
    {
        foreach (int m in cls.Links(cls.Batteries[b].Mounts))
            if (combat.LoadOf(slot, m, kind) >= 0)
                return true;
        return false;
    }

    /// <summary>The kind a battery fires by the magazine order: its first load it has rounds of.</summary>
    static ShellKind? Lead(CombatStore combat, int slot, Loadout lo, int b)
    {
        foreach (var l in lo.Loads(b))
            if (Has(combat, slot, lo.Class, b, lo.ShellTypes[l.ShellType].Kind))
                return lo.ShellTypes[l.ShellType].Kind;
        return null;
    }

    /// <summary>The batteries on each ship (by her slot), main and the rest, from the targets the last pass set.</summary>
    void CountOn(ShipStore store)
    {
        if (onChief.Length < store.Slots)
            (onChief, onOther) = (new int[Math.Max(store.Slots, onChief.Length * 2)], new int[Math.Max(store.Slots, onChief.Length * 2)]);
        else
        {
            Array.Clear(onChief);
            Array.Clear(onOther);
        }
        for (int k = 0; k < store.OffRailsCount; k++)
        {
            int slot = store.OffRailsSlot(k);
            if (store.Combat.Engaged(slot) == 0)
                continue;
            var cls = store[slot].Handling.Class;
            for (int b = 0; b < cls.Batteries.Length; b++)
                if (store.Combat.Target(slot, b) is int t and > 0 && store.SlotOf(t) is int ts and >= 0)
                    (cls.Batteries[b].Chief ? onChief : onOther)[ts]++;
        }
    }

    /// <summary>How many other ships' batteries of a battery's kind are on a target (her slot).</summary>
    int Crowd(ShipClass cls, int b, int target, int targetSlot)
    {
        bool chief = cls.Batteries[b].Chief;
        var on = chief ? onChief : onOther;
        if (targetSlot < 0 || targetSlot >= on.Length)
            return 0;
        int n = on[targetSlot];
        for (int k = 0; k < cls.Batteries.Length; k++)
            if (ownTargets[k] == target && cls.Batteries[k].Chief == chief)
                n--;   // his own aren't another ship's splashes
        return Math.Max(n, 0);
    }

    /// <summary>His torpedoes at will: at the engaged ship if it's within the fast setting's reach, else the biggest
    /// contact in sight within it, the nearest first, when his launchers bear on it.</summary>
    void Torpedoes(in AiContext ctx, ref Captain cap, ShipState own, int slot, Picture? pic)
    {
        var s = ctx.Settings;
        var cls = own.Handling.Class;
        long now = ctx.NowMs;
        if (pic == null || cls.Launchers.Length == 0 || cap.Fire == FireOrder.Hold || cap.TorpedoHeld || cap.TorpedoMs != 0 && now - cap.TorpedoMs < s.TorpedoRefireMs)
            return;
        bool onOrder = (TorpedoUse)doctrine.Of(own.Entity, own.Side, DoctrineField.Torpedoes) == TorpedoUse.OnOrder;
        if (onOrder && cap.Fire != FireOrder.Engage)
            return;   // only at a ship he's ordered to engage
        var (reach, speed) = AttackSetting(ctx, cls, own.Position);
        reach *= s.TorpedoFireShare;
        int target = 0;
        if (cap.Fire == FireOrder.Engage && pic.IndexOf(cap.FireTarget) is int ei and >= 0 && pic[ei].InSight && !pic[ei].Out && Run(ctx, pic, own, ei, speed) is var er &&
            er <= reach && TorpedoChance(ctx, pic, ei, er, speed) >= s.TorpedoMinChance)
            target = cap.FireTarget;
        if (target == 0 && !onOrder)
        {
            int bestRank = int.MaxValue;
            double bestRun = double.MaxValue;
            foreach (int i in seen.Near(ctx.World, own.Side, own.Position, reach))
            {
                if (!Opens(ctx, own, pic[i], seen.Position(own.Side, i)))
                    continue;   // not known for a foe (docs/battle/night.md Q6)
                double run = Run(ctx, pic, own, i, speed);
                int rank = TorpedoRank(pic[i].Type);
                if (run <= reach && TorpedoChance(ctx, pic, i, run, speed) >= s.TorpedoMinChance && (rank < bestRank || rank == bestRank && run < bestRun))
                    (target, bestRank, bestRun) = (pic[i].Entity, rank, run);
            }
        }
        if (target == 0)
            return;
        if (ctx.World.Torpedoes.Ready(ctx.World, own.Entity, target) == 0)
        {
            if (TorpedoesLeft(ctx, own, slot))
                Wait(ref cap);   // her tubes don't bear yet (or reload): tried again as they come round
            return;
        }
        int ti = pic.IndexOf(target);
        var (tq, tv, _) = pic.EstimateAt(ti, now, s);
        if (!TorpedoAim.Intercept(own.Position, tq, tv, speed, out double toward, out _))
            return;
        var aim = own.Position;
        var fwd = Earth.Forward(aim, toward);
        Earth.Advance(ref aim, ref fwd, 1000);
        if (lines.TorpedoFoul(ctx, own, aim, reach / s.TorpedoFireShare, speed))
        {
            Wait(ref cap);   // a friend on the track: not now
            return;
        }
        ctx.Issue(TorpedoSystem.Launch(own.Entity, target, s.TorpedoSpread));
        cap.TorpedoMs = now;
    }

    // the coarse fire watch's ships (docs/battle/night.md Q11): those whose think found a target for their torpedoes but
    // couldn't fire yet, by entity, each once (Captain.TorpedoWait); found afresh at each think
    readonly List<int> torpedoWaiting = new(64);   // at its size, as the others: its first Add mid-fight was garbage (balance.md BL1b)

    void Wait(ref Captain cap)
    {
        if (cap.TorpedoWait)
            return;
        cap.TorpedoWait = true;
        torpedoWaiting.Add(cap.Entity);
    }

    /// <summary>Whether a captain's think left a ship waiting to fire her torpedoes.</summary>
    internal bool TorpedoesWaiting => torpedoWaiting.Count > 0;

    /// <summary>The coarse fire watch's look (docs/battle/night.md Q11): each ship whose captain's last think found a target
    /// for her torpedoes but couldn't fire (her tubes not bearing, a friend on the track) tries again by the think's own
    /// test (<see cref="Torpedoes"/>), as a fine captain does each second; those still waiting stay on the watch.</summary>
    internal void WatchTorpedoes(in AiContext ctx)
    {
        int n = torpedoWaiting.Count;
        for (int k = 0; k < n; k++)
        {
            int e = torpedoWaiting[k];
            if (Find(e) is not (int i and >= 0))
                continue;
            captains[i].TorpedoWait = false;
            if (ctx.Ships.SlotOf(e) is not (int slot and >= 0) || ctx.Ships.Combat.Condition(slot).Status != ShipStatus.Afloat)
                continue;
            var own = ctx.Ships[slot];
            candUnknowns = (Unknowns)doctrine.Of(own.Entity, own.Side, DoctrineField.Unknowns);
            candBold = doctrine.Of(own.Entity, own.Side, DoctrineField.Aggression);
            Torpedoes(ctx, ref captains[i], own, slot, ctx.World.Sightings.Of(own.Side));
        }
        torpedoWaiting.RemoveRange(0, n);
    }

    /// <summary>His flotilla's admiral holds his torpedoes for its signal, or lets them go (docs/battle/command.md 5.1b).</summary>
    internal void HoldTorpedoes(int entity, bool held)
    {
        if (Find(entity) is int i and >= 0 ? captains[i].TorpedoHeld != held : held)
            Get(entity).TorpedoHeld = held;
    }

    /// <summary>Whether her torpedoes wait for her flotilla's signal.</summary>
    internal bool TorpedoesHeld(int entity) => Find(entity) is int i and >= 0 && captains[i].TorpedoHeld;

    /// <summary>His flotilla's signal to fire (docs/battle/command.md 5.1b): a spread at the target from his launchers that
    /// are ready, aimed where his navy's picture says it'll be, unless a friend is on the track; once for a signal made at
    /// <paramref name="signalMs"/>: once torpedoes have gone since, or while a launch waits for its tubes to train (one
    /// that no longer bears by then holds its fire, and he tries again). With his tubes trained out on a beam
    /// (<paramref name="tubesDeg"/>, from ahead; 0 none) he waits for the turn to bring the aim on that side of him,
    /// rather than swing them round. Whether he fired.</summary>
    internal bool FireOnSignal(in AiContext ctx, int entity, int target, long signalMs, double tubesDeg)
    {
        var s = ctx.Settings;
        int slot = ctx.Ships.SlotOf(entity);
        var pic = slot >= 0 ? ctx.World.Sightings.Of(ctx.Ships[slot].Side) : null;
        if (pic == null || pic.IndexOf(target) is not (int ti and >= 0) || pic[ti].Out || FiredSince(ctx, slot, signalMs) || ctx.World.Torpedoes.Launching(entity) ||
            ctx.World.Torpedoes.Ready(ctx.World, entity, target) == 0)
            return false;
        var own = ctx.Ships[slot];
        var (range, speed) = AttackSetting(ctx, own.Handling.Class, own.Position);   // by day the long setting (BL5)
        var (tq, tv, _) = pic.EstimateAt(ti, ctx.NowMs, s);
        if (!TorpedoAim.Intercept(own.Position, tq, tv, speed, out double toward, out _))
            return false;
        if (tubesDeg != 0 && Math.Abs(Wrap180(Wrap180(toward - own.HeadingDeg) - tubesDeg)) > 90)
            return false;   // the turn not yet round far enough
        var aim = own.Position;
        var fwd = Earth.Forward(aim, toward);
        Earth.Advance(ref aim, ref fwd, 1000);
        if (lines.TorpedoFoul(ctx, own, aim, range, speed))
            return false;
        ctx.Issue(TorpedoSystem.Launch(entity, target, s.TorpedoSpread));
        Get(entity).TorpedoMs = ctx.NowMs;
        return true;
    }

    static double Wrap180(double d) => d - 360 * Math.Round(d / 360);

    /// <summary>Whether a ship's launchers have fired since a moment.</summary>
    static bool FiredSince(in AiContext ctx, int slot, long ms)
    {
        var cls = ctx.Ships[slot].Handling.Class;
        for (int l = 0; l < cls.Launchers.Length; l++)
            if (ctx.Ships.Combat.Launcher(slot, l, cls.Launchers[l], ctx.NowMs).FiredMs >= ms)
                return true;
        return false;
    }

    /// <summary>The run a torpedo at a speed needs to meet a contact going on as its navy holds it, m (infinite when it
    /// can't).</summary>
    /// <summary>The chance a torpedo fired now at a contact finds a ship (docs/battle/balance.md BL5; the user: no long
    /// shots at "fast dodging targets", a line of battleships yes): what she presents, her length and her line's within
    /// <see cref="SimSettings.TorpedoLineM"/> of her (a track that misses one finds the next), against how far she can be
    /// from where it's aimed by the time it gets there, <see cref="SimSettings.TorpedoDodgeShare"/> of her speed as seen
    /// times the run's time: L / (L + k V T). A long run at a fast ship, near nothing; at a line, a fair one.</summary>
    public double TorpedoChance(in AiContext ctx, Picture pic, int i, double runM, double speedMs)
    {
        var s = ctx.Settings;
        var (q, v, _) = pic.EstimateAt(i, ctx.NowMs, s);
        int e = pic[i].Entity, slot = ctx.Ships.SlotOf(e);
        if (slot < 0 || !(runM < double.PositiveInfinity))
            return 0;
        var t = ctx.Ships[slot];
        double presented = t.Handling.Class.Motion.LengthM;
        if (groups.Of(e) is { } g)
        {
            // her line's ships near her, as far along it as they reach either way (what a lookout sees of a line)
            double lo = 0, hi = 0;
            var line = g.Line;
            for (int k = 0; k < line.Count; k++)   // by index: a foreach over the interface would allocate its enumerator
                if (line[k] is int m && m != e && ctx.Ships.SlotOf(m) is int ms and >= 0)
                {
                    var d = ctx.Ships[ms].Position - t.Position;
                    if (d.Length > s.TorpedoLineM)
                        continue;
                    double along = Vec3d.Dot(d, t.Forward);
                    (lo, hi) = (Math.Min(lo, along), Math.Max(hi, along));
                }
            presented += hi - lo;
        }
        double away = s.TorpedoDodgeShare * v.Length * runM / Math.Max(speedMs, 0.1);
        return presented / (presented + away);
    }

    double Run(in AiContext ctx, Picture pic, ShipState own, int i, double speedMs)
    {
        var (q, v, _) = pic.EstimateAt(i, ctx.NowMs, ctx.Settings);
        return TorpedoAim.Intercept(own.Position, q, v, speedMs, out _, out double run) ? run : double.PositiveInfinity;
    }

    /// <summary>The torpedoes' reach for an attack at a place now (docs/battle/balance.md BL5): in the dark the fast
    /// setting's; by day <see cref="SimSettings.TorpedoDayShare"/> of the slow setting's at its speed, where that's further
    /// (a day attack fires at a line from its torpedoes' reach, not from point blank).</summary>
    internal static (double RangeM, double SpeedMs) AttackSetting(in AiContext ctx, ShipClass cls, Vec3d at)
    {
        var fast = FastSetting(cls);
        if (AiNight.At(ctx, at, ctx.World.Time))
            return fast;
        double range = 0, speed = 0;
        foreach (ref readonly var l in cls.Launchers)
        {
            ref readonly var t = ref cls.Torpedoes[l.Torpedo];
            if (ctx.Settings.TorpedoDayShare * t.SlowRangeM > range)
                (range, speed) = (ctx.Settings.TorpedoDayShare * t.SlowRangeM, t.SlowKn * Handling.MsPerKnot);
        }
        return range > fast.RangeM ? (range, speed) : fast;
    }

    /// <summary>A class's torpedoes' fast setting: the longest range of its launchers' (m), and its speed (m/s).</summary>
    internal static (double RangeM, double SpeedMs) FastSetting(ShipClass cls)
    {
        double range = 0, speed = 0;
        foreach (ref readonly var l in cls.Launchers)
        {
            ref readonly var t = ref cls.Torpedoes[l.Torpedo];
            if (t.FastRangeM > range)
                (range, speed) = (t.FastRangeM, t.FastKn * Handling.MsPerKnot);
        }
        return (range, speed);
    }

    /// <summary>Whom torpedoes are for: the biggest first.</summary>
    internal static int TorpedoRank(ShipType t) => t switch
    {
        ShipType.Capital => 0,
        ShipType.Cruiser => 1,
        ShipType.Merchant => 2,
        ShipType.Unknown => 3,
        ShipType.Destroyer => 4,
        _ => 5,
    };

    /// <summary>Whether a ship has torpedoes left in launchers that work. Whether she attacks with them is the matchup's
    /// (<see cref="Knowledge.MatchupJudge.Attacks"/>): no ship is torpedo craft by her design.</summary>
    internal static bool TorpedoesLeft(in AiContext ctx, ShipState own, int slot)
    {
        var cls = own.Handling.Class;
        var k = ctx.Ships.Combat.Condition(slot);
        for (int l = 0; l < cls.Launchers.Length; l++)
        {
            var st = ctx.Ships.Combat.Launcher(slot, l, cls.Launchers[l], ctx.NowMs);
            if (k.LauncherWorks(l) && st.Loaded + st.Spare > 0)
                return true;
        }
        return false;
    }

    /// <summary>Each battery's reach for fire at will (<see cref="SimSettings.FireReachShare"/> of the range table's), and
    /// the widest last, into a buffer the layer keeps (the range table's reach is the gunnery's, kept by table): a cache by
    /// loadout grew in the middle of a fight the first time a ship of a new loadout aimed (found by docs/battle/balance.md
    /// BL1b's flotilla test). Read before the next call.</summary>
    ReadOnlySpan<double> Reaches(in AiContext ctx, Loadout lo)
    {
        int n = lo.Class.Batteries.Length + 1;
        if (reaches.Length < n)
            reaches = new double[Math.Max(n, reaches.Length * 2)];
        var r = reaches.AsSpan(0, n);
        r[^1] = 0;
        for (int b = 0; b < n - 1; b++)
        {
            r[b] = ctx.World.Gunnery.ReachM(lo, b) * ctx.Settings.FireReachShare;
            r[^1] = Math.Max(r[^1], r[b]);
        }
        return r;
    }

    /// <summary>A battery's target at will: the current one while it's held and in reach, unless another is better by
    /// what the battery can do about her (fire on her now, bearing and within its guns' reach, before one it can only
    /// track: the user, 2026-10-27, a casemate side kept on a ship it couldn't reach) or by much in range to come, her
    /// size and the fire on her; else the best in sight; 0 for none. A secondary takes none it'd hit with fewer than
    /// doctrine's <see cref="DoctrineField.SecondaryHitFloor"/> of its shells.</summary>
    int Pick(in AiContext ctx, Picture? pic, ShipState own, int slot, ShipClass cls, int b, double reach, double widest, int current)
    {
        if (pic == null)
            return 0;
        var s = ctx.Settings;
        long now = ctx.NowMs;
        bool secondary = !cls.Batteries[b].Chief;
        var p = own.Position;
        if (candCount < 0)
            Candidates(ctx, pic, own, widest);
        double guns = reach / s.FireReachShare;   // the guns' own reach: past it she's tracked, not fired on
        double floor = secondary ? doctrine.Of(own.Entity, own.Side, DoctrineField.SecondaryHitFloor) : 0;
        var gun = floor > 0 ? GunOf(ctx, slot, b) : null;
        // a ship seen done for: any other target first; with a side ceasing on wrecks, none at all (crew.md N5b)
        double cease = (WreckFire)(int)doctrine.Of(own.Entity, own.Side, DoctrineField.Wrecks) == WreckFire.Cease ? s.DoneForCease : double.PositiveInfinity;

        int ci = current != 0 ? pic.IndexOf(current) : -1;
        bool keep = ci >= 0 && !pic[ci].Out && now - pic[ci].SeenMs <= s.FireLostMs && Opens(ctx, own, pic[ci], pic[ci].Position);
        if (keep && lines.GunsFoul(ctx, own, pic.EstimateAt(ci, now, s).Position))
            keep = false;   // a friend in the way: not while he's there
        double curBy = 0;
        int curRank = 0, curCan = 0;
        double curDone = keep ? pic[ci].DoneFor(now, s) : 0;
        if (keep && curDone >= cease)
            keep = false;
        if (keep)
        {
            var (q, v, _) = pic.EstimateAt(ci, now, s);
            double curRange = (q - p).Length;
            // held a little further than taken (by range, and by the hit floor at that much less range): no flicking at the edge
            keep = curRange <= reach * s.FireKeepShare / s.FireReachShare &&
                   (gun == null || HitOn(ctx, gun, own.Side, pic[ci], curRange * s.FireReachShare / s.FireKeepShare) >= floor);
            curCan = Can(Bears(cls, b, Earth.Bearing(p, q) - own.HeadingDeg), curRange <= guns);
            curRank = secondary ? Rank(pic[ci].Type) : 0;
            double curLead = Lead(own, q, v, s);
            curBy = curLead * (1 + s.FireCrowdShare * Crowd(cls, b, current, ctx.Ships.SlotOf(current))) * Sized(s, curRank) /
                    (Worth(curDone) * Matters(ctx, pic[ci], p, q, v, candFinish) * Want(ctx, own, slot, pic[ci], curLead));
        }

        int best = 0, bestRank = 0, bestCan = -1;
        double bestBy = double.MaxValue;
        for (int k = 0; k < candCount; k++)
        {
            if (candRange[k] > reach || candDone[k] >= cease)
                continue;
            int can = Can(Bears(cls, b, candBearing[k]), candRange[k] <= guns);
            if (can < bestCan)
                continue;
            int rank = secondary ? candRank[k] : 0;
            double by = candLead[k] * (1 + s.FireCrowdShare * Crowd(cls, b, candEntity[k], candSlot[k])) * Sized(s, rank) /
                        (Worth(candDone[k]) * candMatters[k] * candWant[k]);
            // a foul line and the hit floor only rule one out: asked only of one that would be the best yet (they're the cost)
            if ((can > bestCan || by < bestBy) && (gun == null || HitOn(ctx, gun, own.Side, pic[candPic[k]], candRange[k]) >= floor) && !Foul(ctx, own, k))
                (best, bestCan, bestRank, bestBy) = (candEntity[k], can, rank, by);
        }
        if (!keep)
            return best;
        if (best == 0 || best == current)
            return current;
        if (bestCan != curCan)
            return bestCan > curCan ? best : current;
        // an unknown's smaller size is a guess: against the one held she counts as its size
        if (bestRank < curRank && bestRank == Rank(ShipType.Unknown))
            bestBy *= Sized(s, curRank) / Sized(s, bestRank);
        return bestBy < s.FireSwitchShare * curBy ? best : current;
    }

    /// <summary>How much his side's aim wants a contact as a target (docs/battle/aims.md A3; the user: reasonable about
    /// it): one of the kinds it wants sunk first (<see cref="Commanders.AimBook.Wanted"/>, by what his navy takes her for)
    /// counts as <see cref="SimSettings.WantedShare"/> times nearer; any other as much nearer by how much she endangers
    /// him (<see cref="Threat"/>), so an escort that can put him out is fought on even terms with the transports (the
    /// nearer first), and one that can't is passed over for them. 1 with nothing wanted.</summary>
    double Want(in AiContext ctx, ShipState own, int slot, in Contact c, double rangeM)
    {
        if (candWanted == 0)
            return 1;
        double w = ctx.Settings.WantedShare;
        return (candWanted & 1 << (int)c.Type) != 0 ? w : 1 + (w - 1) * Threat(ctx, own, slot, c, rangeM);
    }

    /// <summary>How much a contact endangers his ship at a range, 0 to 1: what she's expected to put out of him
    /// (<see cref="Matchup.Threat"/>: her guns on him over an action of <see cref="SimSettings.WantedThreatMin"/>, and a
    /// salvo of her torpedoes in their reach, over the classes his navy may take her for) against
    /// <see cref="SimSettings.WantedThreatOuts"/>, smoothly.</summary>
    double Threat(in AiContext ctx, ShipState own, int slot, in Contact c, double rangeM)
    {
        if (threatOwn != own.Entity || threatMs != ctx.NowMs)
        {
            threatTo.Clear();
            threatTo.AddOwn(ctx.World, Judge.Matchups, slot);
            (threatOwn, threatMs) = (own.Entity, ctx.NowMs);
        }
        threatFrom.Clear();
        threatFrom.AddContact(ctx.World, Judge.Matchups, own.Side, c);
        var s = ctx.Settings;
        return 1 - Math.Exp(-Matchup.Threat(threatFrom.Entries, threatTo.Entries, rangeM, s.WantedThreatMin, 1, s) / s.WantedThreatOuts);
    }

    readonly MatchupSide threatFrom = new(), threatTo = new();
    int threatOwn;
    long threatMs = -1;

    /// <summary>His fight course on a contact his side's aim wants (docs/battle/aims.md A3: round the escort if he can),
    /// bent away from every other enemy held near that endangers him: each pushes by her <see cref="Threat"/> over her
    /// range squared (at <see cref="SimSettings.WantedAvoidM"/> a full threat pushes as hard as the course pulls), at most
    /// <see cref="SimSettings.WantedBendDeg"/> off it. Closer in, the threat makes her a target as good as the transports
    /// (<see cref="Want"/>), and he fights her.</summary>
    double Around(in AiContext ctx, ShipState own, int slot, double course, int enemy)
    {
        var s = ctx.Settings;
        var pic = ctx.World.Sightings.Of(own.Side);
        if (pic == null)
            return course;
        var p = own.Position;
        var push = new Vec3d();
        foreach (int i in seen.Near(ctx.World, own.Side, p, s.FightCompareM))
        {
            if (pic[i].Entity == enemy || (candWanted & 1 << (int)pic[i].Type) != 0)
                continue;
            var q = seen.Position(own.Side, i);
            if (!Opens(ctx, own, pic[i], q))
                continue;
            var d = p - q;
            double r = Math.Max(d.Length, 100), th = Threat(ctx, own, slot, pic[i], r);
            push += d * (th * s.WantedAvoidM * s.WantedAvoidM / (r * r * r));
        }
        var up = p.Normalized();
        push -= up * Vec3d.Dot(push, up);
        if (push.LengthSquared == 0)
            return course;
        var dir = Earth.Forward(p, course) + push;
        double off = Earth.Bearing(p, p + dir * 1000) - course;
        off -= 360 * Math.Round(off / 360);
        double bent = course + Math.Clamp(off, -s.WantedBendDeg, s.WantedBendDeg);
        return (bent % 360 + 360) % 360;
    }

    /// <summary>A contact's worth as a target by how done for she's seen (crew.md N5b): her range counts as much as twenty
    /// times as far, so any other in reach comes first; smoothly, no switch.</summary>
    static double Worth(double doneFor) => 1 - 0.95 * doneFor;

    /// <summary>What a battery can do about a contact: fire on her now (2: she bears, within its guns' reach), train on her
    /// (1: she bears, beyond it), or only track her (0).</summary>
    static int Can(bool bears, bool inReach) => bears ? inReach ? 2 : 1 : 0;

    /// <summary>A battery as the matchup sees it: the matchup counts the guns bearing on one beam, so a side battery on the
    /// other is its twin there (the same kind and calibre); null if neither.</summary>
    MatchupGun? GunOf(in AiContext ctx, int slot, int b)
    {
        var lo = ctx.Ships.Combat.Loadout(slot);
        ref readonly var row = ref lo.Class.Batteries[b];
        MatchupGun? twin = null;
        foreach (var g in Judge.Matchups.Profile(ctx.World, lo).Guns)
            if (g.Battery == b)
                return g;
            else if (g.Kind == row.Kind && g.CalibreMm == row.CalibreMm)
                twin ??= g;
        return twin;
    }

    /// <summary>A battery's hit chance a shell on a contact at a range, as the matchup reckons it: the kind its captain
    /// would load there, over the classes his navy may take her for; 0 past its reach.</summary>
    double HitOn(in AiContext ctx, MatchupGun gun, byte side, in Contact c, double rangeM)
    {
        if (rangeM > gun.KindsReachM)
            return 0;
        shellTarget.Clear();
        shellTarget.AddContact(ctx.World, Judge.Matchups, side, c);
        double hit = 0, n = 0;
        foreach (ref readonly var e in shellTarget.Entries)
            (hit, n) = (hit + e.Count * gun.Chosen(e.Profile, rangeM, ctx.Settings).Hit, n + e.Count);
        return n > 0 ? hit / n : 0;
    }

    /// <summary>A secondary battery's range to a contact counts longer by <see cref="SimSettings.FireSizeShare"/> for each
    /// step of her type's rank (<see cref="Rank"/>), so a boat comes before a battleship at much the same range, never one
    /// alongside.</summary>
    static double Sized(SimSettings s, int rank) => 1 + s.FireSizeShare * rank;

    /// <summary>The range a contact (where and how his navy has her going) will be at <see cref="SimSettings.FireLeadS"/>
    /// on at the rate it's opening or closing now, m: never under a tenth of the range now.</summary>
    static double Lead(ShipState own, Vec3d q, Vec3d v, SimSettings s)
    {
        var d = q - own.Position;
        double range = d.Length;
        if (range < 1)
            return range;
        double rate = Vec3d.Dot(v - own.Forward * own.Speed, d) / range;
        return Math.Max(range + rate * s.FireLeadS, 0.1 * range);
    }

    /// <summary>The contacts his navy holds in sight within his widest reach: range now and to come, bearing from ahead,
    /// type's rank.</summary>
    void Candidates(in AiContext ctx, Picture pic, ShipState own, double widest)
    {
        var p = own.Position;
        double heading = own.HeadingDeg;
        var near = seen.Near(ctx.World, own.Side, p, widest);
        if (candEntity.Length < near.Length)
        {
            int n = Math.Max(near.Length, candEntity.Length * 2);
            (candEntity, candRank, candSlot, candRange, candLead, candBearing) = (new int[n], new int[n], new int[n], new double[n], new double[n], new double[n]);
            (candPic, candDone, candMatters, candWant) = (new int[n], new double[n], new double[n], new double[n]);
            (candPos, candFoul) = (new Vec3d[n], new sbyte[n]);
        }
        candCount = 0;
        candUnknown = 0;
        double unknownD = double.MaxValue;
        foreach (int i in near)
        {
            var q = seen.Position(own.Side, i);
            int e = pic[i].Entity;
            if (!Opens(ctx, own, pic[i], q))
            {
                double d = (q - p).LengthSquared;
                if (!pic[i].TakenForFriend(ctx.NowMs, ctx.Settings) && d < unknownD)
                    (candUnknown, unknownD, candUnknownM) = (e, d, Math.Sqrt(d));
                continue;   // not known for a foe: held (docs/battle/night.md Q6)
            }
            (candEntity[candCount], candSlot[candCount], candRange[candCount]) = (e, ctx.Ships.SlotOf(e), (q - p).Length);
            candLead[candCount] = Lead(own, q, seen.Velocity(own.Side, i), ctx.Settings);
            (candBearing[candCount], candRank[candCount]) = (Earth.Bearing(p, q) - heading, Rank(pic[i].Type));
            (candPos[candCount], candFoul[candCount], candPic[candCount]) = (q, -1, i);
            candDone[candCount] = pic[i].DoneFor(ctx.NowMs, ctx.Settings);
            candMatters[candCount] = Matters(ctx, pic[i], p, q, seen.Velocity(own.Side, i), candFinish);
            candWant[candCount] = Want(ctx, own, ctx.Ships.SlotOf(own.Entity), pic[i], candLead[candCount]);
            candCount++;
        }
    }

    /// <summary>How much an enemy still matters as a target (docs/battle/balance.md BL2a: "fire at what's firing at you"):
    /// 1 unless she's both quiet (her guns not seen firing a while) and making away (her own course opening the range),
    /// down to doctrine's <see cref="DoctrineField.Finish"/> when she's both; from what his navy sees of her.</summary>
    internal static double Matters(in AiContext ctx, in Contact c, Vec3d p, Vec3d q, Vec3d v, double finish)
    {
        if (finish >= 1)
            return 1;
        var s = ctx.Settings;
        double quiet = Math.Clamp((ctx.NowMs - c.QuietMs) / (1000 * s.CrippleQuietS), 0, 1);
        var u = q - p;
        double d = u.Length, away = d > 0 ? Math.Clamp(Vec3d.Dot(v, u) / d / s.CrippleOpenMs, 0, 1) : 0;
        return 1 - (1 - finish) * quiet * away;
    }

    /// <summary>Whether a candidate's line of fire is foul (a friend near the fall), asked once a think.</summary>
    bool Foul(in AiContext ctx, ShipState own, int k)
    {
        if (candFoul[k] < 0)
            candFoul[k] = (sbyte)(lines.GunsFoul(ctx, own, candPos[k]) ? 1 : 0);
        return candFoul[k] == 1;
    }

    /// <summary>Whether any mount of a battery has a relative bearing in its arcs.</summary>
    static bool Bears(ShipClass cls, int b, double relativeDeg)
    {
        foreach (int m in cls.Links(cls.Batteries[b].Mounts))
            if (CombatStore.InArcs(cls, cls.Mounts[m], relativeDeg))
                return true;
        return false;
    }

    /// <summary>A secondary battery's preference by what the contact is taken for: the smallest first (a merchant last).</summary>
    static int Rank(ShipType t) => t switch
    {
        ShipType.Small => 0,
        ShipType.Destroyer => 1,
        ShipType.Unknown => 2,
        ShipType.Cruiser => 3,
        ShipType.Capital => 4,
        _ => 5,
    };

    /// <summary>His Fight, each second (docs/battle/command.md 5.1g): the enemy (the one ordered, else the main battery's
    /// target, else the nearest in sight), and his unit's matchup against what his navy holds in sight round him. Under
    /// doctrine's bar (1 / OutgunnedRatio) he turns away, and comes back once he's well over it; but one that can't get
    /// away (the enemy as fast) runs only to a stronger force of his own, else he fights. A torpedo craft attacks when
    /// the judgement has its side attacking, or when it's ordered to (whoever ordered it judged): in to its torpedoes'
    /// range, fire, away, and in again. One that can hardly be
    /// hurt (<see cref="SimSettings.FightFinishConfidence"/>) closes in to finish the enemy. Nothing while there's nobody
    /// (he keeps what's ordered).</summary>
    void FightThink(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var s = ctx.Settings;
        var cls = own.Handling.Class;
        int main = ChiefBattery(cls);
        double reach = main >= 0 ? ctx.World.Gunnery.ReachM(ctx.Ships.Combat.Loadout(slot), main) : s.ShadowMinM;
        (cap.Attacking, cap.Finishing) = (false, false);
        double band = cap.DistanceM > 0 ? cap.DistanceM : doctrine.Of(own.Entity, own.Side, DoctrineField.RangeShare) * reach;

        int enemy = new Unit(cap.Target).ShipOf(groups);
        if (enemy == 0 || !ctx.Picture.TryGet(own.Side, enemy, out var t) || ctx.World.Sightings.Of(own.Side)?.Out(enemy) == true)
        {
            enemy = main >= 0 ? ctx.Ships.Combat.Target(slot, main) : 0;
            if (enemy == 0 || !ctx.Picture.TryGet(own.Side, enemy, out t))
                enemy = Nearest(ctx, own, s.FightCompareM, out t);
        }
        if (enemy == 0)
            return;
        if (enemy != cap.FightEnemy)
            (cap.FightEnemy, cap.FightSide) = (enemy, 0);

        bool judged = Judge.Near(ctx, own, out var m);
        if (cap.DistanceM <= 0 && judged && m.BandA > 0)
            band = m.BandA;   // where his unit does best against them (Matchup.Judge), doctrine's where it hardly matters
        cap.FightBandM = band;
        double accept = Judge.Bars(own.Entity, own.Side).Accept, c = judged ? m.Confidence : double.PositiveInfinity;
        bool away = cap.FightAway ? c < accept / s.FightRallyShare : c < accept;
        double fallBack = double.NaN;
        if (away && !m.ACanRefuse(s) && double.IsNaN(fallBack = FallBack(ctx, own)))
            away = false;   // no escape: he fights

        // ordered to attack, or in his flotilla's attack (his torpedoes held for its signal): whoever ordered it judged;
        // else his own unit's attack, if his torpedoes are his better weapon here (O7c)
        if (TorpedoesLeft(ctx, own, slot) && (cap.AttackOrdered || cap.TorpedoHeld ||
                                             !away && (m.Terms.Style & MatchupStyle.AttackByA) != 0 && Judge.Attacks(ctx, own)))
        {
            // the attack: in to the torpedoes' range, fire, away, and in again
            cap.Attacking = true;
            bool off = cap.TorpedoMs != 0 && ctx.NowMs - cap.TorpedoMs < s.TorpedoTurnAwayMs;
            double attack = s.TorpedoAttackShare * AttackSetting(ctx, cls, own.Position).RangeM;
            var (course0, kn0) = off ? Behaviours.Run(own, t) : Behaviours.Fight(own, t, attack, s.FightBandShare, s.FightCloseDeg, s.FightMinCloseKn, ref cap.FightSide);
            // under fire on the run-in, and away after: evading, but steady for the last of it (the research: hold for the launch)
            if (off || Earth.Distance(own.Position, t.Position) > s.EvadeSteadyShare * attack)
                course0 = Evade(ctx, own, slot, course0, attackRun: true);
            cap.FightAway = false;
            Give(ctx, own, course0, cap.LimitKn > 0 ? Math.Min(kn0, cap.LimitKn) : kn0);
            return;
        }

        cap.FightAway = away;
        if (!away && c >= s.FightFinishConfidence && m.Terms.LostA < s.FightFinishLostShare)
            (band, cap.Finishing) = (band * s.FightFinishShare, true);   // he can hardly be hurt (a cripple, not a torpedo boat): in to finish her
        var (course, kn) = away ? Behaviours.Run(own, t) : Behaviours.Fight(own, t, band, s.FightBandShare, s.FightCloseDeg, s.FightMinCloseKn, ref cap.FightSide);
        candWanted = Book?.Wanted(own.Side) ?? 0;
        if (!away && candWanted != 0 && ctx.World.Sightings.Of(own.Side) is { } fp && fp.IndexOf(enemy) is int fi and >= 0 &&
            (candWanted & 1 << (int)fp[fi].Type) != 0)
            course = Around(ctx, own, slot, course, enemy);   // on what his aim wants: round what endangers him, if he can
        if (away)
        {
            if (double.IsNaN(fallBack))
                fallBack = FallBack(ctx, own);
            course = Evade(ctx, own, slot, FallBackCourse(course, fallBack, s), attackRun: false);
        }
        if (cap.LimitKn > 0)
            kn = Math.Min(kn, cap.LimitKn);
        Give(ctx, own, course, kn);
    }

    /// <summary>Falling back on the main body (docs/battle/command.md 5.1c): the bearing of the nearest own force (another
    /// top unit of his side) within <see cref="SimSettings.FallBackM"/> that's a match for the enemy in sight round him
    /// (its matchup against them at doctrine's bar or better); NaN if none. Judged once a moment for his unit.</summary>
    double FallBack(in AiContext ctx, ShipState own)
    {
        var s = ctx.Settings;
        var mine = groups.UnitOf(own.Entity);
        int key = mine != null ? Unit.Group(mine.GroupId).Id : Unit.Ship(own.Entity).Id;
        if (fallBacksMs != ctx.NowMs)
        {
            fallBacks.Clear();   // a moment's judgements only: at most one a captain
            fallBacksMs = ctx.NowMs;
        }
        if (fallBacks.TryGetValue(key, out double fb))
            return fb;
        double accept = Judge.Bars(own.Entity, own.Side).Accept, deg = double.NaN;
        // the forces in reach nearest first (ties by index), so the first that's a match is the one
        fallCands.Clear();
        for (int k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            if (g.Parent != 0 || mine != null && g.GroupId == mine.GroupId || ctx.Ships.SlotOf(groups.GuideShip(g)) is not (int gs and >= 0))
                continue;
            var guide = ctx.Ships[gs];
            double d = Earth.Distance(own.Position, guide.Position);
            if (guide.Side == own.Side && d < s.FallBackM)
                fallCands.Add((d, k, gs));
        }
        fallCands.Sort();
        MatchupSide? near = null;   // the enemy round him, once
        foreach (var (_, k, gs) in fallCands)
            if (!(Judge.GroupAgainst(ctx, groups[k], own, near ??= Judge.EnemyNear(ctx, own)) < accept))
            {
                deg = Earth.Bearing(own.Position, ctx.Ships[gs].Position);
                break;
            }
        fallBacks[key] = deg;
        return deg;
    }

    readonly List<(double D, int K, int Slot)> fallCands = [];

    /// <summary>This moment's fall-back bearings by unit, sized with the captains (<see cref="Insert"/>), so the first
    /// captain to fall back in a fight allocates nothing.</summary>
    readonly Dictionary<int, double> fallBacks = [];
    long fallBacksMs = -1;

    /// <summary>The course away: toward the force he falls back on if that's within <see cref="SimSettings.FallBackDeg"/>
    /// of straight away, else straight away.</summary>
    static double FallBackCourse(double away, double toward, SimSettings s)
    {
        if (double.IsNaN(toward))
            return away;
        double off = toward - away;
        return Math.Abs(off - 360 * Math.Round(off / 360)) <= s.FallBackDeg ? toward : away;
    }

    /// <summary>Whether his ship is past doctrine's fall-out (docs/battle/command.md 5.1a), judged when its condition
    /// changes: too slow, too few main guns, listing too far, too much water, or (when doctrine falls out at all) unable to
    /// steer. Then she leaves her group (the line closes up behind her: going-down ships leave the same way) and goes
    /// home, his own order, firing at will as she goes. Not once the player has ordered her on after it
    /// (<see cref="Captain.Overruled"/>), nor while the player holds her (<see cref="Whose.Player"/>) or has the conn of
    /// her line (judged again once she's let go).</summary>
    void FallOut(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var k = ctx.Ships.Combat.Condition(slot);
        if (cap.Overruled || cap.Whose == Whose.Player || cap.Order == CaptainOrder.Home || !k.Damaged || k.Version == cap.FitVersion || k.Status != ShipStatus.Afloat
            || Admirals?.LineConned(own.Entity) == true)   // her line's conn: she stays in it
            return;
        cap.FitVersion = k.Version;
        var cls = own.Handling.Class;
        var fit = Fitness.Of(cls, k, ctx.World.Damage.Flood(cls).ReserveT);
        double D(DoctrineField f) => doctrine.Of(own.Entity, own.Side, f);
        var rule = new FallOutRule(D(DoctrineField.FallOutSpeedShare), D(DoctrineField.FallOutGunsShare), D(DoctrineField.FallOutListDeg),
            D(DoctrineField.FallOutWaterShare));
        var why = rule.Of(fit) switch
        {
            FallOutCause.Steering => CaptainReason.OutSteering,
            FallOutCause.Speed => CaptainReason.OutSpeed,
            FallOutCause.Guns => CaptainReason.OutGuns,
            FallOutCause.List => CaptainReason.OutList,
            FallOutCause.Water => CaptainReason.OutWater,
            _ => CaptainReason.None,
        };
        if (why != CaptainReason.None)
            GoHome(ctx, ref cap, own, why);
    }

    /// <summary>Out of the fight by his own judgement: out of his line (it closes up behind her), his superior's limits
    /// gone, and home.</summary>
    void GoHome(in AiContext ctx, ref Captain cap, ShipState own, CaptainReason why)
    {
        if (groups.Of(own.Entity) != null)
            ctx.Issue(GroupSystem.Remove(own.Entity));
        SetLimit(ctx, ref cap, own, 0);
        SetClearance(ctx, ref cap, own.Entity, 0);
        (cap.Order, cap.Target, cap.Whose, cap.Watch, cap.Asked, cap.Reason) = (CaptainOrder.Home, 0, Whose.Own, false, false, why);
        Recount();
    }

    /// <summary>Whether his ship's magazines are empty (docs/battle/command.md 5.1g): every chief mount out of the rounds
    /// it carries (a mount with none given never runs out) and no torpedo left. Then she leaves her group and goes home,
    /// as one fallen out does, her secondaries firing at will as she goes. Not while the player holds her.</summary>
    void OutOfRounds(in AiContext ctx, ref Captain cap, ShipState own)
    {
        if (cap.Whose == Whose.Player || cap.Order == CaptainOrder.Home || Admirals?.LineConned(own.Entity) == true)
            return;
        var cls = own.Handling.Class;
        var combat = ctx.Ships.Combat;
        int slot = ctx.Ships.SlotOf(own.Entity);
        bool any = false;
        for (int m = 0; m < cls.Mounts.Length; m++)
        {
            ref readonly var mr = ref cls.Mounts[m];
            if (mr.Battery < 0 || !cls.Batteries[mr.Battery].Chief)
                continue;
            if (mr.Rounds <= 0 || combat.Mount(slot, m).Rounds > 0)
                return;   // rounds left, or no limit given
            any = true;
        }
        if (!any)
            return;
        for (int l = 0; l < cls.Launchers.Length; l++)
        {
            var st = combat.Launcher(slot, l, cls.Launchers[l], ctx.NowMs);
            if (st.Loaded + st.Spare > 0)
                return;
        }
        GoHome(ctx, ref cap, own, CaptainReason.OutAmmunition);
    }

    /// <summary>Out of the fight, each second: away from the nearest enemy his navy holds in sight within
    /// <see cref="SimSettings.FightCompareM"/>, else for her home (great circle; <see cref="Homes"/>: her side's, or an
    /// aim's zone), at all she can make; stopped within <see cref="SimSettings.HomeRadiusM"/> of it (a zone's own). With no home and nobody near he keeps his course.</summary>
    void HomeThink(in AiContext ctx, ref Captain cap, ShipState own, int slot)
    {
        var s = ctx.Settings;
        double flank = own.Handling.Class.Motion.MaxSpeedKn;
        if (Nearest(ctx, own, s.FightCompareM, out var t) != 0)
        {
            var (course, kn) = (Escape(ctx, own, t), Behaviours.Run(own, t).SpeedKn);
            Give(ctx, own, Evade(ctx, own, slot, course, attackRun: false), kn);
            CoverRetreat(ctx, ref cap, own, slot, near: true);
            return;
        }
        CoverRetreat(ctx, ref cap, own, slot, near: false);   // out of touch: no more smoke
        if (homes.Of(own.Side, own.Entity, s.HomeRadiusM) is not var (home, there))
            return;
        if (Earth.Distance(own.Position, home) <= there)
        {
            if (own.SpeedOrderKn != 0)
                Give(ctx, own, own.CourseOrderDeg, 0);
            cap.Reason = CaptainReason.Home;
            return;
        }
        Give(ctx, own, Earth.Bearing(own.Position, home), flank);
    }

    /// <summary>A cripple's way out (docs/battle/balance.md BL5: "a bit more sneaky"): away from every enemy her navy holds
    /// about her, each by its bearing weighted by the inverse square of its range (the nearest most, but not one alone:
    /// running from him straight at another is what she mustn't do), bent toward home as the nearest falls back past
    /// <see cref="SimSettings.CrippleHomeFreeM"/>, fully home at twice it; her own speed and her smoke do the rest.</summary>
    double Escape(in AiContext ctx, ShipState own, in Sighting nearest) => Escape(ctx, own, nearest.Position);

    /// <summary>The way out from the nearest enemy at <paramref name="nearestAt"/> (<see cref="Escape(in AiContext, ShipState, in Sighting)"/>):
    /// a withdrawing unit's too (docs/battle/aims.md A2), whose guide it's asked of.</summary>
    internal double Escape(in AiContext ctx, ShipState own, Vec3d nearestAt)
    {
        var s = ctx.Settings;
        var p = own.Position;
        var away = new Vec3d();
        var pic = ctx.World.Sightings.Of(own.Side);
        if (pic != null)
            foreach (int i in seen.Near(ctx.World, own.Side, p, s.FightCompareM))
            {
                var q = seen.Position(own.Side, i);
                if (!Opens(ctx, own, pic[i], q))
                    continue;
                var d = p - q;
                double r2 = Math.Max(d.LengthSquared, 1e4);
                away += d * (1 / (r2 * Math.Sqrt(r2)));   // its unit bearing over its range squared
            }
        if (away.LengthSquared == 0)
            away = p - nearestAt;
        var up = p.Normalized();
        away -= up * Vec3d.Dot(away, up);
        away = away.Normalized();
        if (homes.Of(own.Side, own.Entity, s.HomeRadiusM)?.At is { } home)
        {
            double free = Math.Clamp((Earth.Distance(p, nearestAt) - s.CrippleHomeFreeM) / s.CrippleHomeFreeM, 0, 1);
            var toward = Earth.Forward(p, Earth.Bearing(p, home));
            // toward home only as far as it doesn't take her back at them
            if (Vec3d.Dot(toward, away) > 0 || free >= 1)
                away = (away * (1 - free) + toward * free).Normalized();
        }
        return Earth.Bearing(p, p + away * 1000);
    }

    /// <summary>A ship going home covers her retreat (docs/battle/balance.md BL2b; the user: any ship may): under fire, with an
    /// enemy near, she makes funnel smoke (the smoke field: as much as her plant gives), and keeps it while one is near;
    /// out of touch, home or given another order she stops what she started. Smoke made by another's order isn't his to
    /// stop, nor does he start it then.</summary>
    void CoverRetreat(in AiContext ctx, ref Captain cap, ShipState own, int slot, bool near)
    {
        bool want = near && (cap.SmokeMade || FiredUpon(ctx.Ships, own, slot));
        if (want == cap.SmokeMade || want && ctx.World.Smoke.Screening(own.Entity))
            return;
        cap.SmokeMade = want;
        ctx.Issue(SmokeSystem.MakeSmoke(own.Entity, want));
    }

    /// <summary>A torpedo seen coming at her (doctrine's TorpedoEvasion: comb): she turns parallel to its track, at the
    /// speed rung, until <see cref="SimSettings.TorpedoCombAfterS"/> after it's past (her behaviour waits); then a plain
    /// course order is given back, and a behaviour steers on. Under the conn (hers, or her line's: docs/battle/command.md
    /// "The conn") she doesn't: her lookouts report the track's side (<see cref="Captain.Tracks"/>, the map's ticker),
    /// whatever doctrine says, and the helm is the player's.</summary>
    void Comb(in AiContext ctx, ref Captain cap, ShipState own)
    {
        long now = ctx.NowMs;
        bool conned = Conned(cap, own.Entity);
        if (conned && cap.HeldUntilMs != 0)
            (cap.HeldUntilMs, cap.Reason) = (0, cap.Reason == cap.HeldFor ? CaptainReason.None : cap.Reason);   // her line's conn taken: her station again
        if (cap.HeldUntilMs != 0 && now >= cap.HeldUntilMs)
        {
            cap.HeldUntilMs = 0;
            if (cap.Reason == cap.HeldFor)
                cap.Reason = CaptainReason.None;
            if (cap.Order is CaptainOrder.Course or CaptainOrder.None)
                ctx.Helm.Course(own.Entity, cap.HeldBackDeg);
            else if (cap.Order == CaptainOrder.Route)
                ctx.Helm.ResumeRoute(own.Entity);   // the comb's course took the helm off it for the moment
        }
        bool combs = (TorpedoEvasion)doctrine.Of(own.Entity, own.Side, DoctrineField.TorpedoEvasion) == TorpedoEvasion.Comb;
        if (!conned && !combs || !torpedoWatch.Threat(ctx, own, out double comb, out double past, out double fromDeg))
        {
            cap.Tracks = 0;
            return;
        }
        if (conned)
        {
            double off = fromDeg - own.HeadingDeg;
            cap.Tracks = off - 360 * Math.Round(off / 360) >= 0 ? (sbyte)1 : (sbyte)-1;
            return;
        }
        cap.Tracks = 0;
        if (cap.HeldUntilMs == 0)
            cap.HeldBackDeg = own.CourseOrderDeg;
        if (cap.Reason == CaptainReason.Lit)
            cap.Reason = CaptainReason.None;   // a torpedo before a light
        (cap.HeldDeg, cap.HeldUntilMs, cap.HeldFor) = (comb, now + (long)((past + ctx.Settings.TorpedoCombAfterS) * 1000), CaptainReason.Combing);
        if (cap.Reason == CaptainReason.None)
            cap.Reason = CaptainReason.Combing;
        Give(ctx, own, comb, own.SpeedOrderKn);
    }

    /// <summary>Which ships are under fire, and from how near: every fighting ship's batteries' targets, once a think (a
    /// ship knows when she's shot at: the flashes, the splashes). Before the behaviours, from the targets the last pass
    /// set.</summary>
    void MarkUnderFire(in AiContext ctx)
    {
        var store = ctx.Ships;
        if (underFire.Length < store.Slots)
            underFire = new double[Math.Max(store.Slots, underFire.Length * 2)];
        if (anyUnderFire)
            Array.Clear(underFire);
        anyUnderFire = false;
        if (fighting == 0)
            return;
        for (int k = 0; k < store.OffRailsCount; k++)
        {
            int slot = store.OffRailsSlot(k);
            if (store.Combat.Engaged(slot) == 0)
                continue;
            var shooter = store[slot];
            for (int b = 0; b < shooter.Handling.Class.Batteries.Length; b++)
                if (store.Combat.Target(slot, b) is int t and > 0 && store.SlotOf(t) is int ts and >= 0)
                {
                    double r = Earth.Distance(shooter.Position, store[ts].Position);
                    if (underFire[ts] == 0 || r < underFire[ts])
                        underFire[ts] = r;
                    anyUnderFire = true;
                }
        }
    }

    /// <summary>The course a ship under fire steers for one she wants (docs/battle/command.md 5.1a; the evasion research's
    /// styles): doctrine's zigzag, legs of <see cref="DoctrineField.EvadeLegS"/> either side by
    /// <see cref="DoctrineField.EvadeDeg"/>, each ship on her own phase. A running ship evades fire from beyond
    /// <see cref="DoctrineField.EvadeBeyondM"/> only; a torpedo craft on her attack whatever the range.</summary>
    double Evade(in AiContext ctx, ShipState own, int slot, double course, bool attackRun)
    {
        double near = slot < underFire.Length ? underFire[slot] : 0;
        if (near == 0 || (Evasion)doctrine.Of(own.Entity, own.Side, DoctrineField.Evasion) != Evasion.Zigzag)
            return course;
        if (!attackRun && near < doctrine.Of(own.Entity, own.Side, DoctrineField.EvadeBeyondM))
            return course;
        if (own.Tier == ClockTier.Coarse)
        {
            // his think comes once a minute: his legs at their own ends, steered from this base until it comes (WatchTracks)
            ref var cap = ref Get(own.Entity);
            (cap.ZigBaseDeg, cap.ZigNextMs, cap.ZigThinkMs) = (course, LegEndMs(ctx, own), ctx.NowMs);
        }
        return Leg(ctx, own, course);
    }

    /// <summary>The zigzag's course on the leg the moment falls in: doctrine's legs either side of the course, each ship on
    /// her own phase.</summary>
    double Leg(in AiContext ctx, ShipState own, double course)
    {
        long leg = LegMs(own);
        long phase = own.Entity * 7_919L % leg;   // not every ship on the same leg
        double swing = doctrine.Of(own.Entity, own.Side, DoctrineField.EvadeDeg);
        return ((ctx.NowMs + phase) / leg % 2 == 0 ? course + swing : course - swing + 360) % 360;
    }

    long LegMs(ShipState own) => Math.Max(1, (long)(doctrine.Of(own.Entity, own.Side, DoctrineField.EvadeLegS) * 1000));

    /// <summary>When the zigzag's leg she's on ends, ms.</summary>
    long LegEndMs(in AiContext ctx, ShipState own)
    {
        long leg = LegMs(own), phase = own.Entity * 7_919L % leg;
        return ((ctx.NowMs + phase) / leg + 1) * leg - phase;
    }

    /// <summary>The nearest contact his navy holds in sight within a radius, as a sighting; 0 if none.</summary>
    int Nearest(in AiContext ctx, ShipState own, double radiusM, out Sighting t)
    {
        var p = own.Position;
        int best = -1;
        double bestD = double.MaxValue;
        foreach (int i in seen.Near(ctx.World, own.Side, p, radiusM))
        {
            double d = (seen.Position(own.Side, i) - p).LengthSquared;
            if (d < bestD)
                (best, bestD) = (i, d);
        }
        if (best < 0)
        {
            t = default;
            return 0;
        }
        var q = seen.Position(own.Side, best);
        var v = seen.Velocity(own.Side, best);
        double speed = v.Length;
        int entity = ctx.World.Sightings.Of(own.Side)![best].Entity;
        t = new Sighting(entity, q, speed > 1e-6 ? v * (1 / speed) : Earth.Forward(q, 0), speed, ctx.NowMs);
        return entity;
    }

    /// <summary>A class's heaviest chief battery (<see cref="BatteryRow.Chief"/>: its main, else its heaviest guns), -1 for none.</summary>
    static int ChiefBattery(ShipClass cls)
    {
        int best = -1;
        for (int b = 0; b < cls.Batteries.Length; b++)
            if (cls.Batteries[b].Chief && (best < 0 || cls.Batteries[b].CalibreMm > cls.Batteries[best].CalibreMm))
                best = b;
        return best;
    }
}
