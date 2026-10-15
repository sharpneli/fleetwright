using Fleetwright.Map.Torpedoes;
using Fleetwright.Sim;
using Fleetwright.Sim.Core;

namespace Fleetwright.Tests;

/// <summary>The map's book of torpedoes (docs/visuals/torpedoes.md R0): two of one spread, launched together on the same
/// course, kept apart by their serials; a run takes its end once and is kept while its track lasts, then let go.</summary>
public class TorpedoBookTests
{
    static readonly Vec3d At = Earth.Position(56.5, 4.0);

    static TorpedoesView Running(params long[] serials)
    {
        var v = new TorpedoesView();
        v.EnsureCapacity(serials.Length);
        var f = Earth.Forward(At, 90);
        for (int i = 0; i < serials.Length; i++)
            (v.Origin[i], v.Forward[i], v.LaunchMs[i], v.SpeedMs[i], v.EndS[i], v.Serial[i], v.DepthM[i], v.Shooter[i], v.Class[i]) =
                (At, f, 1_000, 20, 100, serials[i], 4, 2, 0);
        v.Count = serials.Length;
        return v;
    }

    static TorpedoEndsView Ends(long serial, long ms, TorpedoEnd how)
    {
        var e = new TorpedoEndsView();
        e.EnsureCapacity(1);
        (e.Point[0], e.Ms[0], e.Serial[0], e.Ship[0], e.How[0], e.ChargeKg[0]) = (At, ms, serial, how == TorpedoEnd.Hit ? 1 : -1, how, 180);
        e.Count = 1;
        return e;
    }

    [Fact]
    public void Two_of_a_spread_keep_two_runs_and_each_ends_on_its_own()
    {
        var book = new TorpedoBook();
        book.Update(Running(7, 8), new TorpedoEndsView(), 2_000, 90);
        Assert.Equal(2, book.Count);

        // 7 hits at 31 s; 8 runs on
        book.Update(Running(8), Ends(7, 31_000, TorpedoEnd.Hit), 31_000, 90);
        Assert.Equal(2, book.Count);
        int hit = book[0].Serial == 7 ? 0 : 1;
        Assert.True(book[hit].Ended);
        Assert.Equal(TorpedoEnd.Hit, book[hit].How);
        Assert.Equal(30, book[hit].EndS, 6);
        Assert.Equal(180, book[hit].ChargeKg);
        Assert.False(book[1 - hit].Ended);

        // the hit's track lasts its 90 s past the end, then it goes; the other is still kept
        book.Update(Running(8), new TorpedoEndsView(), 120_000, 90);
        Assert.Equal(2, book.Count);
        book.Update(Running(8), new TorpedoEndsView(), 122_000, 90);
        Assert.Equal(1, book.Count);
        Assert.Equal(8, book[0].Serial);
    }
}
