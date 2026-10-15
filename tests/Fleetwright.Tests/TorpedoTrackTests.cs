using Fleetwright.Map.Torpedoes;

namespace Fleetwright.Tests;

/// <summary>The torpedo's track as it surfaces (docs/visuals/torpedoes.md R1): its head a little behind the torpedo (drawn closer than physics), the
/// strength rising and fading and never negative, the tail surfacing on after the run has ended, the dive after a
/// mount's launch, the width spreading.</summary>
public class TorpedoTrackTests
{
    [Fact]
    public void At_four_metres_the_bubbles_take_five_seconds_and_the_track_is_drawn_closer()
    {
        // physically the head is 5 s behind at 4 m (105 m at 41 kn) and full by 16 s
        Assert.Equal(5, 4 / TorpedoTrack.RiseFastMs, 9);
        Assert.Equal(16, 4 / TorpedoTrack.RiseSlowMs, 9);
        // drawn at LagShare of that (the user's departure): a small gap behind the torpedo
        double lag = TorpedoTrack.HeadLagS(4);
        Assert.Equal(5 * TorpedoTrack.LagShare, lag, 9);
        Assert.Equal(0, TorpedoTrack.Surfaced(lag * 0.99, 4));
        Assert.True(TorpedoTrack.Surfaced(lag * 1.1, 4) > 0);
        Assert.Equal(1, TorpedoTrack.Surfaced(TorpedoTrack.FullS(4), 4));
        Assert.InRange(lag * 41 * 0.5144, 5, 40);
    }

    [Fact]
    public void The_strength_rises_then_fades_to_nothing_by_its_life()
    {
        double last = 0, peak = 0;
        bool falling = false;
        for (double t = 0; t <= TorpedoTrack.LifeS + 5; t += 0.5)
        {
            double s = TorpedoTrack.Strength(t, 4);
            Assert.InRange(s, 0, 1);
            if (s < last - 1e-12)
                falling = true;
            else
                Assert.False(falling && s > last + 1e-12, $"rose again at {t} s");
            (last, peak) = (s, Math.Max(peak, s));
        }
        Assert.True(peak > 0.5);
        Assert.Equal(0, TorpedoTrack.Strength(TorpedoTrack.LifeS, 4));
    }

    [Fact]
    public void A_run_that_has_ended_goes_on_surfacing()
    {
        // the last of the run, passed at its end: before its lag, nothing yet; after it, some; then fading
        double end = 60, lag = TorpedoTrack.HeadLagS(4);
        Assert.Equal(0, TorpedoTrack.Strength(lag * 0.9, 4));
        Assert.True(TorpedoTrack.Strength(TorpedoTrack.FullS(4), 4) > 0);
        Assert.True(TorpedoTrack.Strength(end + TorpedoTrack.KeepS - end + 0.1, 4) == 0);
    }

    [Fact]
    public void A_mounts_torpedo_dives_and_recovers_and_the_track_spreads()
    {
        Assert.Equal(4, TorpedoTrack.Depth(0, 4, dives: true));
        Assert.Equal(8, TorpedoTrack.Depth(TorpedoTrack.DiveRunM / 3, 4, dives: true), 6);
        Assert.Equal(4, TorpedoTrack.Depth(TorpedoTrack.DiveRunM, 4, dives: true));
        Assert.Equal(4, TorpedoTrack.Depth(50, 4, dives: false));
        Assert.Equal(2, TorpedoTrack.WidthM(TorpedoTrack.HeadLagS(4), 4), 6);
        Assert.InRange(TorpedoTrack.WidthM(90, 4), 6, 8);
    }
}
