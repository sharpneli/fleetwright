using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Fleetwright.Sim.Tests;

/// <summary>Friend or foe (docs/battle/night.md Q6): a ship seen in the dark but not made out is an unknown; a challenge
/// unanswered makes her a foe and shows the challenger to her; a foe who answers it is taken for a friend until she fires;
/// the captains fire on an unknown, challenge her or hold by doctrine, a bold one firing where no friend is about her.</summary>
public class FriendOrFoeTests(ITestOutputHelper output)
{
    static readonly SimSettings S = SimSettings.Load(Path.Combine(Fleet.Repo, "Content", "Sim", "settings.json")) with { Smoke = false };
    const double Lat = 56, Lon = 4;

    /// <summary>A light cruiser (side 1, entity 1) and a destroyer (side 2, entity 2) on a moonless spring night (the Dover Strait's), stopped,
    /// beam on to each other, 0.03° of longitude apart (1.9 km); with the AI if asked, and the destroyers' class.</summary>
    static World Night(out AiSystem? ai, out int destroyers, bool withAi)
    {
        var w = new World(WorldTime.FromUtc(1917, 4, 21, 0), 7, S);
        ai = withAi ? new AiSystem(w) : null;
        int cl = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        destroyers = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(Command.SetWeather(20_000));
        w.Apply(ShipSystem.Spawn(cl, Lat, Lon, 0, 0, 1));
        w.Apply(ShipSystem.Spawn(destroyers, Lat, Lon + 0.03, 180, 0, 2));
        return w;
    }

    static World Night() => Night(out _, out _, withAi: false);

    static ref readonly Contact Of(World w, int side, int entity)
    {
        var pic = w.Sightings.Of(side)!;
        return ref pic[pic.IndexOf(entity)];
    }

    static bool Holds(World w, int side, int entity) => w.Sightings.Of(side)?.IndexOf(entity) >= 0;

    /// <summary>Seen but not made out, she's an unknown; challenged and silent, she's a foe once the reply's time is up,
    /// and the challenge's lamp showed the challenger to her navy, which knows her for a foe by it at once.</summary>
    [Fact]
    public void A_challenge_unanswered_makes_her_a_foe_and_shows_the_challenger()
    {
        var w = Night();
        w.AdvanceTo(w.Time + 60_000);
        Assert.True(Holds(w, 1, 2), "the cruiser holds the destroyer");
        var c = Of(w, 1, 2);
        output.WriteLine($"the cruiser holds her at {c.Level}, foe {c.Foe}; the destroyer holds the cruiser: {Holds(w, 2, 1)}");
        Assert.True(c.Level < ContactLevel.Type && !c.Foe, "not made out: an unknown");

        w.Apply(ChallengeSystem.Challenge(1, 2));
        w.AdvanceTo(w.Time + 6_000);
        Assert.True(w.Lights.HasBeam(w.Ships.Store.SlotOf(1)), "her lamp flashing");
        Assert.True(Holds(w, 2, 1) && Of(w, 2, 1).Foe, "the destroyer saw a challenge not her own");
        Assert.False(Of(w, 1, 2).Foe, "the reply not yet due");
        w.AdvanceTo(w.Time + (long)((S.ChallengeSignalS + S.ChallengeReplyS) * 1000));
        output.WriteLine($"after the reply's time: the cruiser holds her as foe {Of(w, 1, 2).Foe}");
        Assert.True(Of(w, 1, 2).Foe);
        Assert.Equal(0, w.Challenges.ChallengeOf(1).Target);
    }

    /// <summary>A foe who gives the reply is taken for a friend, until she shows a light on the challenger.</summary>
    [Fact]
    public void A_foe_who_answers_is_taken_for_a_friend_until_she_lights_the_challenger()
    {
        var w = Night();
        w.AdvanceTo(w.Time + 60_000);
        w.Apply(ChallengeSystem.Challenge(1, 2));
        w.AdvanceTo(w.Time + 5_000);
        w.Apply(ChallengeSystem.Answer(2, 1));
        Assert.True(w.Challenges.ChallengeOf(1).Answered);
        w.AdvanceTo(w.Time + (long)((S.ChallengeSignalS + S.ChallengeReplyS) * 1000));
        var c = Of(w, 1, 2);
        Assert.False(c.Foe);
        Assert.True(c.TakenForFriend(w.Time.Ms, S), "answered: taken for a friend");

        w.Apply(SearchlightSystem.Order(2, SearchlightOrder.Illuminate, 1));
        long ordered = w.Time.Ms;
        while (w.Time.Ms < ordered + 60_000 && !Of(w, 1, 2).Foe)
            w.AdvanceTo(w.Time + 1_000);
        output.WriteLine($"her beam on the cruiser {(w.Time.Ms - ordered) / 1000.0:F0} s after the order; foe {Of(w, 1, 2).Foe} at {Of(w, 1, 2).Level}");
        Assert.True(Of(w, 1, 2).Foe, "she gave herself away by lighting the cruiser");
        Assert.False(Of(w, 1, 2).TakenForFriend(w.Time.Ms, S));
    }

    /// <summary>By day, beyond where she's made out: a ship seen firing on one of her navy's is a foe at once, her type
    /// still unknown.</summary>
    [Fact]
    public void A_ship_seen_firing_on_ones_own_is_a_foe()
    {
        var w = new World(WorldTime.FromUtc(1916, 5, 31, 14), 7, S);
        int cl = w.Ships.AddClass(Fleet.Get("light_cruiser").Class!);
        int dd = w.Ships.AddClass(Fleet.Get("destroyer").Class!);
        w.Apply(Command.SetWeather(12_000));
        w.Apply(ShipSystem.Spawn(cl, Lat, Lon, 0, 0, 1));
        w.Apply(ShipSystem.Spawn(dd, Lat, Lon + 0.14, 180, 0, 2));   // 8.7 km
        w.AdvanceTo(w.Time + 120_000);
        Assert.True(Holds(w, 2, 1));
        Assert.True(Of(w, 2, 1).Level < ContactLevel.Type && !Of(w, 2, 1).Foe, "not made out: an unknown");
        w.Apply(GunnerySystem.Engage(1, 0, 2));
        var store = w.Ships.Store;
        long ordered = w.Time.Ms;
        while (w.Time.Ms < ordered + 60_000 && store.Combat.LastFired(store.SlotOf(1)) == 0)
            w.AdvanceTo(w.Time + 1_000);
        Assert.True(store.Combat.LastFired(store.SlotOf(1)) > 0, "the cruiser fired");
        w.AdvanceTo(w.Time + 1_000);
        output.WriteLine($"she fired {(store.Combat.LastFired(store.SlotOf(1)) - ordered) / 1000.0:F0} s after the order; the destroyer's navy: foe {Of(w, 2, 1).Foe} at {Of(w, 2, 1).Level}");
        Assert.True(Of(w, 2, 1).Foe);
        Assert.True(Of(w, 2, 1).Level < ContactLevel.Type, "her type still not made out");
    }

    /// <summary>The captains by doctrine: firing on an unknown at once, challenging her and firing when she's silent, or
    /// holding; a bold one holding by doctrine fires anyway with no friend about her, and holds with one near her.</summary>
    [Fact]
    public void Captains_fire_challenge_or_hold_on_a_ship_they_cannot_make_out()
    {
        (double FiredS, bool Challenged) Play(Unknowns rule, double temper = 0, bool friendNear = false)
        {
            var w = Night(out var ai, out int destroyers, withAi: true);
            w.Apply(ai!.Orders.SideDoctrine(1, DoctrineField.Unknowns, (double)rule));
            w.Apply(ai.Orders.SideDoctrine(2, DoctrineField.Unknowns, (double)Unknowns.Hold));   // she holds, so she doesn't give herself away
            if (temper != 0)
                w.Apply(ai.Orders.Doctrine(Unit.Ship(1), DoctrineField.Temper, temper));
            if (friendNear)
                w.Apply(ShipSystem.Spawn(destroyers, Lat + 0.01, Lon + 0.03, 0, 0, 1));   // one of the cruiser's, 1.1 km off her
            var store = w.Ships.Store;
            bool challenged = false;
            for (int s = 1; s <= 120; s++)
            {
                w.AdvanceTo(w.Time + 1_000);
                challenged |= w.Challenges.ChallengeOf(1).Target == 2;
                if (store.Combat.Target(store.SlotOf(1), 0) == 2)
                    return (s, challenged);
            }
            return (double.NaN, challenged);
        }
        var fire = Play(Unknowns.Fire);
        var challenge = Play(Unknowns.Challenge);
        var hold = Play(Unknowns.Hold);
        var bold = Play(Unknowns.Hold, temper: 0.5);
        var boldFriend = Play(Unknowns.Hold, temper: 0.5, friendNear: true);
        output.WriteLine($"fire: on her at {fire.FiredS} s; challenge: {challenge.FiredS} s (challenged {challenge.Challenged}); hold: {hold.FiredS}; " +
                         $"bold: {bold.FiredS}; bold with a friend near her: {boldFriend.FiredS}");
        Assert.False(double.IsNaN(fire.FiredS));
        Assert.True(challenge.Challenged);
        Assert.True(challenge.FiredS >= fire.FiredS + S.ChallengeSignalS + S.ChallengeReplyS - 2, "the reply awaited first");
        Assert.True(double.IsNaN(hold.FiredS), "held");
        Assert.False(double.IsNaN(bold.FiredS), "bold, no friend about her");
        Assert.True(double.IsNaN(boldFriend.FiredS), "a friend near her: held");
    }


}
