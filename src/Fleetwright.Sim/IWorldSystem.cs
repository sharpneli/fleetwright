using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>A part of the world with events and commands of its own (the ships in the fine clock tier, the AI's layers; later: rails, encounters).
/// Registered once with <see cref="World.Register"/>, which gives it the id its events and commands carry. It's
/// called only from the thread advancing the world, and allocates nothing in either call.</summary>
public interface IWorldSystem
{
    /// <summary>Runs one of its events; the world's time is the event's.</summary>
    void Handle(World world, in SimEvent e);

    /// <summary>Applies one of its commands at the world's current time.</summary>
    void Apply(World world, in Command c);

    /// <summary>Mixes all of its state into the world's hash.</summary>
    void Hash(ref StateHash h);

    /// <summary>Told after any command the world applied, whoever's it was (a system that depends on another's state
    /// wakes on it here).</summary>
    void Applied(World world, in Command c)
    {
    }

    /// <summary>Told when ships came off rails other than by a command (a contact), or an encounter opened: a system
    /// that watches ships on the fine tick more often than on rails wakes on it here, as on <see cref="Applied"/>.</summary>
    void OffRails(World world)
    {
    }

    /// <summary>Copies its part of what the screens need into a snapshot, after the world's; allocates nothing.</summary>
    void Fill(Snapshot s)
    {
    }
}
