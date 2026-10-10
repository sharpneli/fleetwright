using Fleetwright.Shipgen;

namespace Fleetwright.Tests;

/// <summary>The design worker: the newest request wins, failures say why, and a design built before comes back from
/// the cache (a look change keeps the built ship and only draws again). It bakes, so it needs a GPU.</summary>
[Trait("Category", "Gpu")]
[Collection("Gpu")]   // one GPU device at a time
public class DesignWorkerTests
{
    static Design Load(string name) => Design.Load(Paths.Shipgen("designs", name + ".json"));

    static DesignWorker Worker() => new(scale: 4, mipLevels: 2, limits: true);

    [Fact]
    public void The_newest_request_wins()
    {
        using var w = Worker();
        w.Submit(Load("destroyer"));
        w.Submit(Load("dreadnought"));
        int last = w.Submit(Load("yamato"));
        Assert.True(w.WaitIdle(TimeSpan.FromMinutes(1)));
        var r = w.Latest!;
        Assert.Equal(last, r.Id);
        Assert.Equal("yamato", r.Design.Id);
        Assert.NotNull(r.Ship);
        Assert.NotNull(r.Mesh);
        Assert.NotNull(r.Images);
        Assert.Empty(r.Errors);
        Assert.False(w.Busy);
    }

    [Fact]
    public void An_invalid_design_says_why_and_the_next_still_builds()
    {
        using var w = Worker();
        w.Submit(new Design { Id = "nothing" });
        Assert.True(w.WaitIdle(TimeSpan.FromMinutes(1)));
        Assert.Null(w.Latest!.Ship);
        Assert.Contains(w.Latest.Errors, e => e.Contains("speed_kn"));

        w.Submit(new Design { Id = "slow", SpeedKn = 7 });   // under the 8 kn floor
        Assert.True(w.WaitIdle(TimeSpan.FromMinutes(1)));
        Assert.Null(w.Latest!.Ship);

        w.Submit(new Design { Id = "new", SpeedKn = 8 });   // the designer's empty ship
        Assert.True(w.WaitIdle(TimeSpan.FromMinutes(1)));
        Assert.NotNull(w.Latest!.Ship);
    }

    [Fact]
    public void A_design_built_before_comes_from_the_cache()
    {
        using var w = Worker();
        var a = Load("destroyer");
        w.Submit(a);
        w.WaitIdle(TimeSpan.FromMinutes(1));
        var first = w.Latest!;
        w.Submit(Load("dreadnought"));
        w.WaitIdle(TimeSpan.FromMinutes(1));

        w.Submit(a with { });   // an equal design, not the same object
        w.WaitIdle(TimeSpan.FromMinutes(1));
        Assert.Same(first.Ship, w.Latest!.Ship);
        Assert.Same(first.Images, w.Latest.Images);

        w.Submit(a, new LookInput { Navy = "kure" });   // another look: the same ship, drawn again
        w.WaitIdle(TimeSpan.FromMinutes(1));
        Assert.Same(first.Ship, w.Latest!.Ship);
        Assert.NotSame(first.Images, w.Latest.Images);
        Assert.Equal(0, w.Latest.BuildS);
    }
}

/// <summary>The session is the one design every scene shows: an edit becomes its design and its result, and picking
/// the file again drops the edit.</summary>
[Trait("Category", "Gpu")]
[Collection("Gpu")]
public class DesignSessionTests
{
    [Fact]
    public void An_edit_is_what_every_scene_sees()
    {
        using var s = new DesignSession(Paths.Shipgen("designs", "destroyer.json"), 4, 2);
        int v0 = s.DesignVersion;
        var edited = s.Design! with { SpeedKn = 30 };
        s.Edit(edited);
        Assert.True(s.WaitIdle());
        Assert.True(s.Edited);
        Assert.True(s.DesignVersion > v0);
        Assert.Equal(30, s.Result!.Design.SpeedKn);
        Assert.NotNull(s.Ship);
        s.Rebuild();   // the file again
        Assert.True(s.WaitIdle());
        Assert.False(s.Edited);
        Assert.NotEqual(30, s.Result!.Design.SpeedKn);
    }
}
