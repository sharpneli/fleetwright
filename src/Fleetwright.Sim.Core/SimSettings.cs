using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Fleetwright.Sim.Core;

/// <summary>Every tuning number of the sim, in one place (docs/world/architecture.md, "No magic values"). Immutable:
/// a world is made with one and keeps it, and a replay records it, since settings change results. The game reads them
/// from data (<c>Content/Sim/settings.json</c>, <see cref="Load"/>); the defaults here fill any key the file leaves
/// out, and are what a world gets when it's given none (low-level tests).</summary>
public sealed record SimSettings
{
    public static readonly SimSettings Default = new();

    /// <summary>The fastest the clock may be asked to run, game seconds per real second.</summary>
    public double MaxSpeed { get; init; } = 1_000_000;

    /// <summary>The slowest, above zero (zero is a pause).</summary>
    public double MinSpeed { get; init; } = 0.1;

    /// <summary>How far ahead of the clock the target may run, in real seconds at the asked speed. A sim slower than
    /// asked drops its achieved speed instead of building a backlog to catch up on later.</summary>
    public double MaxLagRealSeconds { get; init; } = 0.25;

    /// <summary>How often the sim thread publishes a snapshot while it's behind, wall milliseconds.</summary>
    public double PublishIntervalMs { get; init; } = 8;

    /// <summary>Events the sim thread runs between looks at the wall clock and the stop flag.</summary>
    public int EventsPerSlice { get; init; } = 4096;

    /// <summary>Room made up front, so play doesn't grow it: the event queue, the command queue and the command log.</summary>
    public int EventCapacity { get; init; } = 16_384;
    public int CommandCapacity { get; init; } = 256;
    public int CommandLogCapacity { get; init; } = 16_384;

    // ------------------------------------------------------------------ ships at sea (docs/battle/plan.md step 2.2)

    /// <summary>The ships' fixed tick, ms: every ship's pose moves on it, on a grid of world time.</summary>
    public int ShipTickMs { get; init; } = 50;

    /// <summary>Room made up front: ships, and waypoints per ship's route.</summary>
    public int ShipCapacity { get; init; } = 64;
    public int RouteCapacity { get; init; } = 32;

    /// <summary>Of the shaft power, what pushes the ship (the quasi-propulsive coefficient): thrust = η·P/U.</summary>
    public double PropulsiveEfficiency { get; init; } = 0.6;

    /// <summary>Thrust per watt at a standstill, N/W, which caps η·P/U at low speed (0.1 N/W is ~7.6 kgf per shp).</summary>
    public double BollardThrustPerWatt { get; init; } = 0.1;

    /// <summary>The water moved with the hull going ahead, as a fraction of its displacement.</summary>
    public double AddedMassFraction { get; init; } = 0.07;

    /// <summary>The power a plant makes astern, as a fraction of its ahead power.</summary>
    public double AsternPowerFraction { get; init; } = 0.5;

    /// <summary>The steady turning radius at hard over, as a fraction of the tactical diameter
    /// (research/simulation/fire-control-research/manoeuvring.md §3: 0.43-0.47).</summary>
    public double SteadyRadiusOverTd { get; init; } = 0.45;

    /// <summary>Nomoto's dimensionless time constant T' = T·U/L (manoeuvring.md §3: 1.0-1.5 for warships), and the
    /// factor a single rudder puts on it.</summary>
    public double NomotoTPrime { get; init; } = 1.25;
    public double SingleRudderTFactor { get; init; } = 1.1;

    /// <summary>The speed below which T stops growing, m/s: a ship barely moving still answers its helm slowly, not
    /// never.</summary>
    public double NomotoMinSpeedMs { get; init; } = 1;

    /// <summary>The fraction of speed lost in a steady hard-over turn at the same shaft power (manoeuvring.md §3: 0.25
    /// liners and old battleships, 0.35 battleships, 0.40 destroyers), by length over beam: the first at L/B 7 or
    /// less, the second at 10 or more, linear between.</summary>
    public double TurnSpeedLossBeamy { get; init; } = 0.3;
    public double TurnSpeedLossSlender { get; init; } = 0.4;

    /// <summary>The rudder a helmsman uses for an ordinary change of course, degrees (manoeuvring.md §4: 10-15°).</summary>
    public double StandardRudderDeg { get; init; } = 15;

    /// <summary>The helmsman: rudder degrees per degree of heading error ahead, and how far ahead he looks, as a
    /// multiple of the ship's Nomoto T (he meets the swing before it carries him past).</summary>
    public double HelmGain { get; init; } = 1.5;
    public double HelmLeadOverT { get; init; } = 1.2;

    /// <summary>Within this latitude of either pole a held course means steering straight: a compass course spirals
    /// into the pole and can't be held there, so the helmsman takes his course from the heading, and rails run on the
    /// great circle. Leaving, the ship holds the heading it leaves with.</summary>
    public double PolarCapDeg { get; init; } = 89;

    /// <summary>A route's last waypoint counts as reached inside this, metres.</summary>
    public double ArrivalRadiusM { get; init; } = 300;

    // ------------------------------------------------------------------ the rails clock tier (docs/battle/plan.md 2.5)

    /// <summary>Ships steaming steady go on rails (false: everything stays on the fine tick, for comparing).</summary>
    public bool RailsEnabled { get; init; } = true;

    /// <summary>How often the fine tick looks for ships to put on rails, ms (a multiple of the tick).</summary>
    public int RailsCheckMs { get; init; } = 5000;

    /// <summary>Steady enough for rails: the swing, the rudder, the heading off the course (or a group's ships off their
    /// guide's), the speed off what the engines give (or off the guide's).</summary>
    public double RailsMaxYawRateDegS { get; init; } = 0.02;
    public double RailsMaxRudderDeg { get; init; } = 0.5;
    public double RailsMaxCourseErrDeg { get; init; } = 0.5;
    public double RailsMaxSpeedErrKn { get; init; } = 0.1;

    /// <summary>A group's other ships go on rails with their guide within this of its speed (their captains keep station
    /// with a deadband; a ship still closing up runs faster).</summary>
    public double RailsGroupSpeedErrKn { get; init; } = 0.5;

    /// <summary>A group's ships go on rails with their guide within this of its heading, degrees: more than the captains'
    /// course deadband (<see cref="CaptainCourseDeadbandDeg"/>), which a ship in station may sit off by. On rails they
    /// take the guide's heading.</summary>
    public double RailsGroupHeadingDeg { get; init; } = 2;

    /// <summary>A group's ships go on rails only within this of their stations, m: rails hold their offsets as they are.</summary>
    public double RailsGroupStationM { get; init; } = 50;

    // ------------------------------------------------------------------ the AI (docs/battle/plan.md step 2.3b)

    /// <summary>How often an AI layer thinks while every ship it commands is on rails, ms: nothing about them changes
    /// but by events then, and thinking each second would hold the clock to about a hundred thousand times.</summary>
    public int AiRailsPeriodMs { get; init; } = 60_000;

    /// <summary>How often captains think, ms of world time (on a grid of it).</summary>
    public int CaptainPeriodMs { get; init; } = 1000;

    /// <summary>A captain re-gives an order only when what he wants differs from what's ordered by more than this.</summary>
    public double CaptainCourseDeadbandDeg { get; init; } = 1;
    public double CaptainSpeedDeadbandKn { get; init; } = 0.3;

    /// <summary>Following: the station astern of the leader when the order gives none, in the follower's lengths; how far
    /// past the station he steers for, in the same lengths (pursuing a point ahead keeps him from snaking); how fast he
    /// closes a gap along the line, seconds to make it good; and the most he goes over or under the leader's speed.</summary>
    public double FollowDistanceLengths { get; init; } = 3;
    public double FollowLookaheadLengths { get; init; } = 4;
    public double FollowCloseS { get; init; } = 90;
    public double FollowMaxSpeedDiffKn { get; init; } = 6;

    /// <summary>How often admirals think, ms of world time; and how far under the slowest member's designed speed they
    /// hold their guide, so the others have speed in hand to keep station.</summary>
    public int AdmiralPeriodMs { get; init; } = 1000;
    public double GroupSpeedMarginKn { get; init; } = 2;

    // ------------------------------------------------------------------ from data

    /// <summary>Reads settings from JSON. Strict: an unknown key is an error (a misspelt key would silently keep its
    /// default). Keys left out keep the defaults above.</summary>
    public static SimSettings Load(string path) => Parse(File.ReadAllText(path));

    public static SimSettings Parse(string json)
    {
        // the generated reader sets every init property, a missing key to zero, so the file goes over the defaults first
        var given = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
                        { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
                    ?? throw new JsonException("the settings aren't a JSON object");
        var merged = JsonNode.Parse(Default.ToJson())!.AsObject();
        foreach (var (key, value) in given)
            merged[key] = value?.DeepClone();
        return merged.Deserialize(SimSettingsJson.Default.SimSettings) ?? throw new JsonException("the settings are null");
    }

    /// <summary>These settings as JSON, every key: what the shipped file is made from.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, SimSettingsJson.Default.SimSettings);
}

[JsonSourceGenerationOptions(WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(SimSettings))]
internal partial class SimSettingsJson : JsonSerializerContext;
