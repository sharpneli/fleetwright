#if !TRACY
namespace Fleetwright;

// Release builds leave Tracy out (Fleetwright.csproj): these stand in for the TracyWrapper calls the game makes, as
// no-ops. ProfileScope is an empty struct, so `using (new ProfileScope(...))` costs nothing and allocates nothing.

static class Profiler
{
    public static void InitThread(string name) { }
    public static void HeartBeat() { }
}

readonly struct ProfileScope : IDisposable
{
    public ProfileScope(string name, uint color) { }
    public void Dispose() { }
}

static class ZoneC
{
    public const uint BLUE = 0, CYAN = 0, GREEN = 0, RED = 0, PURPLE = 0, ORANGE = 0, YELLOW = 0;
}
#endif
