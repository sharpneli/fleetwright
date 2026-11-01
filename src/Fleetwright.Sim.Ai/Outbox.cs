using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>A think's orders (docs/battle/workers.md W9): a think takes the world in and gives commands out, and they
/// go here, to be applied on the sim thread in the order given. While it isn't <see cref="Deferred"/> each is applied as
/// it's given (today's order of effects); the world refuses a command issued straight to it while the AI thinks
/// (<see cref="World.Thinking"/>), so every order of a think comes this way. Nothing allocated once grown.</summary>
public sealed class Outbox
{
    Command[] items = new Command[64];
    int count;

    /// <summary>Whether the orders wait for <see cref="Apply"/>; else each is applied as it's given.</summary>
    public bool Deferred { get; set; }

    public int Count => count;

    public void Add(World world, in Command c)
    {
        if (!Deferred)
        {
            Run(world, c);
            return;
        }
        if (count == items.Length)
            Array.Resize(ref items, items.Length * 2);
        items[count++] = c;
    }

    /// <summary>Applies the orders waiting, in the order given, and empties it.</summary>
    public void Apply(World world)
    {
        for (int i = 0; i < count; i++)
            Run(world, items[i]);
        count = 0;
    }

    static void Run(World world, in Command c)
    {
        bool thinking = world.Thinking;
        world.Thinking = false;
        try
        {
            world.Issue(c);
        }
        finally
        {
            world.Thinking = thinking;
        }
    }
}
