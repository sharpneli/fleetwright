using System.Numerics;
using Fleetwright.Map.Flashes;
using Fleetwright.Sim.Core;

namespace Fleetwright.Tests;

/// <summary>The frame's point lights in two pools (docs/visuals/hits-and-fires.md decision 5): the fires never take the
/// flashes' places, nor the flashes theirs; past a pool's eight the fires lighting least near the view's centre go.</summary>
public class FireLightsTests
{
    [Fact]
    public void Flashes_and_fires_keep_their_own_eight()
    {
        var l = new FlashLights();
        var centre = Earth.Position(56.5, 4);
        l.Centre(centre);
        for (int i = 0; i < 12; i++)
            l.Add(Earth.Position(56.5 + i * 0.01, 4), 1000 + i, Vector3.One, 5);
        for (int i = 0; i < 12; i++)   // the far ones a little brighter, but farther
            l.AddFire(Earth.Position(56.5, 4 + i * 0.02), 100 + i, Vector3.One, 5, 1000);
        Assert.Equal(LightUniforms.MaxFlashes, l.Count - l.Fires);
        Assert.Equal(LightUniforms.MaxFires, l.Fires);
        // the eight fires kept are the nearest
        for (int i = l.Count - l.Fires; i < l.Count; i++)
            Assert.True((l[i].At - centre).Length < 8 * 0.02 * 64_000 + 100, $"fire {i} at {(l[i].At - centre).Length:0} m");
        var view = new Fleetwright.Map.MapView(Fleetwright.Map.MapCamera.At(56.5, 4, 1), 1920, 1080);
        l.Finish(view);
        Assert.Equal(16, (int)l.Block.FlashInfo.X);
    }
}
