using Fleetwright.Sim;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>The app's world: made once by the app, not by a tab, so switching tabs leaves it running
/// (docs/world/architecture.md, "Screens"). It runs on its own thread (<see cref="SimHost"/>); the frame loop reports
/// real time each frame (<see cref="Frame"/>) and the scenes read <see cref="Current"/> and send commands. Nothing
/// here holds a reference into the world.</summary>
public sealed class WorldSession : IDisposable
{
    readonly SimHost host;

    /// <summary>The newest snapshot, taken at the start of this frame. The UI thread's; valid until the next frame.</summary>
    public Snapshot Current { get; private set; }

    public WorldSession(WorldTime start, ulong seed)
    {
        host = new SimHost(new World(start, seed));
        host.Start();
        // the first snapshot, so no frame shows an empty one
        var s = host.Latest();
        for (int k = 0; k < 1000 && s.Sequence == 0; k++)
        {
            Thread.Sleep(1);
            s = host.Latest();
        }
        Current = s;
    }

    /// <summary>The frame loop's call, every frame: real time passed, then the newest snapshot.</summary>
    public void Frame(float dt)
    {
        host.Elapse(dt);
        Current = host.Latest();
    }

    public void Send(in Command c) => host.Send(c);

    public void Dispose() => host.Dispose();
}
