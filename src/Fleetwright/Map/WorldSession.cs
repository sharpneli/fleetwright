using Fleetwright.Sim;
using Fleetwright.Sim.Ai;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

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

    /// <summary>The classes in play, shared with the art (<see cref="ShipArtLibrary"/>): the app's, made before the
    /// world. Thread-safe; the map requests designs here and spawns ships by the ids it gives.</summary>
    public ShipClassRegistry Classes { get; }

    /// <summary>The world's AI (admirals and captains). Owned by the sim thread: the UI only builds orders with it
    /// (<see cref="AiSystem.Orders"/>, which reads nothing but its id) and sends them.</summary>
    public AiSystem Ai { get; }

    /// <summary>The world's settings: immutable, so the UI may read them (a route's capacity, the snap speed).</summary>
    public SimSettings Settings { get; }

    /// <summary>The world's land: immutable, so the map draws its finest level from the same instance.</summary>
    public Shore Shore { get; }

    /// <summary>The shipped 1:10M land and its index, built on a thread (~0.2-0.4 s): the app starts it before the
    /// window opens and hands the result to the world.</summary>
    public static Task<Shore> LoadShore() =>
        Task.Run(() => Shore.Build(Land.Load(Path.Combine(AppContext.BaseDirectory, "Content", "World", "land-10m.bin"))));

    /// <summary>A world with the shipped settings (Content/Sim/settings.json), the app's class registry and the land.
    /// <paramref name="setup"/> runs on it before its thread starts: the dev ships (-ships=) until missions place
    /// forces.</summary>
    public WorldSession(WorldTime start, ulong seed, ShipClassRegistry classes, Shore shore, Action<World>? setup = null)
    {
        Classes = classes;
        Shore = shore;
        var settings = Settings = SimSettings.Load(Path.Combine(AppContext.BaseDirectory, "Content", "Sim", "settings.json"));
        var w = new World(start, seed, settings, classes, shore);
        Ai = new AiSystem(w);
        setup?.Invoke(w);
        host = new SimHost(w);
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
