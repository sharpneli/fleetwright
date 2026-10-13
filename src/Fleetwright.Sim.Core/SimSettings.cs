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

    // ------------------------------------------------------------------ ships and the land (docs/world/terrain-plan.md)

    /// <summary>The minimum: how close to the coast a ship's side may come, metres, beyond half its beam. The backstop
    /// holds it; nothing comes closer.</summary>
    public double CoastMinClearanceM { get; init; } = 30;

    /// <summary>The standoff, where a sliding ship keeps: the minimum plus this many of its lengths, within the bounds,
    /// metres (close, as in Rule the Waves: ~100 m for a torpedo boat, ~300 m for a battleship).</summary>
    public double CoastStandoffLengths { get; init; } = 1.5;
    public double CoastStandoffMinM { get; init; } = 80;
    public double CoastStandoffMaxM { get; init; } = 400;

    /// <summary>The feeler's look-ahead: the ship's turning radius at its course rudder plus this many seconds of its
    /// speed (its answer to the helm, the Nomoto lag, inside it).</summary>
    public double SlideLookaheadS { get; init; } = 30;

    /// <summary>Released only once the wanted course is clear this many look-aheads on, so it doesn't dither.</summary>
    public double SlideReleaseFactor { get; init; } = 1.5;

    /// <summary>A sliding ship is let go only with the wanted course within this many degrees of its heading (and
    /// clear): a longer turn back to it would sweep through the coast beside it, so it follows the coast until the
    /// coast turns toward its course.</summary>
    public double SlideReleaseDeg { get; init; } = 60;

    /// <summary>A wanted course within this many degrees of square to the coast slides to starboard of it, whichever way
    /// is a little nearer: so a small difference in where a ship meets the coast doesn't send it the other way.</summary>
    public double SlideSquareDeg { get; init; } = 15;

    /// <summary>Along the coast, the most a sliding ship turns off its line toward the sea (or back toward the
    /// standoff), degrees: all of it at the coast itself, none at the standoff.</summary>
    public double SlideCorrectionDeg { get; init; } = 30;

    /// <summary>Sliding with the coast a turning radius ahead nearer than this many standoffs, the helmsman may put
    /// the rudder hard over: the coast closes in tighter than his ordinary rudder turns.</summary>
    public double SlideHardOverStandoffs { get; init; } = 0.75;

    /// <summary>Sliding, a corner ahead (where its heading would come inside its standoff) nearer than this many of
    /// its ordinary turning radii: hard over too.</summary>
    public double SlideHardOverRadii { get; init; } = 1.5;

    /// <summary>Hard over for the land, the helmsman holds it, not easing it as the swing builds, while he's more than
    /// this many degrees off the slide's course.</summary>
    public double SlideHardOverErrDeg { get; init; } = 20;

    /// <summary>The feeler counts land only where the wanted course comes this much closer than the ship already is,
    /// metres, inside its standoff: a ship that isn't heading for the coast is left alone.</summary>
    public double SlideToleranceM { get; init; } = 10;

    /// <summary>A new course this far from the last, degrees, forgets which way along the coast a ship slides.</summary>
    public double SlideResetDeg { get; init; } = 30;

    /// <summary>Slower than this, m/s, the feeler is off (a ship stopped or going astern has only the backstop).</summary>
    public double SlideMinSpeedMs { get; init; } = 0.5;

    /// <summary>How far the backstop, a spawn or a waypoint looks for the sea from a point on land, metres.</summary>
    public double CoastSearchM { get; init; } = 100_000;

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

    /// <summary>Rails and the land (docs/world/terrain-plan.md S2): a unit's track on rails is traced this far ahead
    /// for land when its rails are laid or anchored again (and again when it's steamed that far), metres.</summary>
    public double RailsLandHorizonM { get; init; } = 500_000;

    /// <summary>A unit on rails comes off this far, metres, plus its guide's look-ahead, short of where its track
    /// comes within its standoff (and its widest station abeam) of the coast, so the fine tick's slide takes over.</summary>
    public double RailsLandMarginM { get; init; } = 2_000;

    /// <summary>A unit goes on rails only with its track clear of land for this many of those distances: never for a
    /// few minutes' steaming before it would come off again.</summary>
    public double RailsLandOnFactor { get; init; } = 2;

    /// <summary>Sliding on rails (S3, at <see cref="RailsSnapMinSpeed"/> and up): the slide's course is taken again at
    /// least this often along a coast, metres, and sooner where its leg would come near the minimum (half the way
    /// there, but never less than the least step).</summary>
    public double SlideRailsStepM { get; init; } = 2_000;
    public double SlideRailsMinStepM { get; init; } = 20;

    /// <summary>At this asked speed and above, a unit is placed on rails from its orders and an order to a ship or group
    /// on rails (a course, a speed, a route) is carried out at once on rails: the turn and the change of speed are
    /// instantaneous and a group's ships jump to their stations on the new heading. Below it the order takes them off
    /// rails and the turn plays out (coarse or fine, by the speed), and a unit goes on rails only once it's steady. The
    /// user's rule (2026-10-13): a manoeuvre that takes about a rendered frame or less may be taken as done at once; at
    /// 10,000× a frame is ~2.8 game minutes, about a standard turn. The speed is world state, so this is
    /// deterministic, and nothing here reads the map (the zoom's rule went in docs/battle/plan.md step 2.8e).</summary>
    public double RailsSnapMinSpeed { get; init; } = 10_000;

    /// <summary>At this asked speed and above, every ship that isn't on rails is in the coarse clock tier: kinematic
    /// motion (constant acceleration, turns as arcs of the turning circle), decided for every
    /// <see cref="CoarseStepMs"/>; below it they're on the fine tick (docs/world/architecture.md "The speed picks the
    /// tier"). 240× (the user, 2026-10-13): a frame is 4 game seconds, so the rudder and the swing left out aren't seen.
    /// The asked speed, never the achieved one: it's world state, so this is deterministic.</summary>
    public double CoarseMinSpeed { get; init; } = 240;

    /// <summary>The coarse step, ms (a multiple of <see cref="RailsCheckMs"/>): how often coarse ships' captains and
    /// admirals decide, and contacts, encounters, the rails check and sighting are looked at. Coarse motion is exact
    /// between steps, so the step doesn't change where ships go, only when they decide.</summary>
    public int CoarseStepMs { get; init; } = 60_000;

    /// <summary>How many of a coarse guide's past segments it keeps, so its group's ships ride its track (a column turns
    /// where its guide turned): enough to reach back down a long line through a few turns. Past the oldest the track
    /// runs straight back.</summary>
    public int CoarseTrackSegments { get; init; } = 16;

    /// <summary>A coarse ship's captain keeping station steers for where it will be this many coarse steps on, at the
    /// speed that gets him there: two, so a change of speed taking longer than a step isn't overshot (a column spawned
    /// 400 m out of station closes without passing it).</summary>
    public double CoarseRendezvousSteps { get; init; } = 2;

    /// <summary>A coarse ship within this of its course, degrees, takes it at once rather than turning for it (what's
    /// left at a turn's end: the meridians converging along the arc).</summary>
    public double CoarseSnapDeg { get; init; } = 0.01;

    /// <summary>The fine model's sea trials that give each class its coarse accelerations: their step, s, and the
    /// longest a trial runs, s (Handling).</summary>
    public double CoarseCalibrationStepS { get; init; } = 1;
    public double CoarseCalibrationMaxS { get; init; } = 7200;

    // ------------------------------------------------------------------ contacts (docs/battle/plan.md 2.5, ahead of 3.1)

    /// <summary>Opposing ships within this of each other are in the fine tier, metres: beyond the longest sighting
    /// range, so the fine tier owns everything that could be seen. Ships on rails come off at the moment it's crossed.</summary>
    public double EncounterRangeM { get; init; } = 50_000;

    /// <summary>Ships go back on rails only this much further apart than the encounter range, metres, so a pair
    /// steaming along its edge doesn't flicker between the tiers.</summary>
    public double EncounterMarginM { get; init; } = 10_000;

    /// <summary>An encounter closes once its sides have been further apart than the range and margin for this long, ms
    /// (docs/battle/plan.md step 3.1b): its ships are held in the fine tier until then.</summary>
    public int EncounterCloseMs { get; init; } = 600_000;

    /// <summary>How often every pair of opposing rails is solved afresh, ms: the horizon a solve looks ahead to. A
    /// ship's rails changing (a waypoint, an order) solves it again at once.</summary>
    public int ContactScanMs { get; init; } = 3_600_000;

    /// <summary>The most steps one solve of a pair takes before it looks again later from where it got to.</summary>
    public int ContactTraceSteps { get; init; } = 64;

    // ------------------------------------------------------------------ weather (docs/battle/plan.md step 3.2b)

    /// <summary>The visibility of a world whose scenario gives none, metres: a clear day at sea.</summary>
    public double DefaultVisibilityM { get; init; } = 40_000;

    // ------------------------------------------------------------------ sighting (docs/battle/plan.md step 3.2c)

    /// <summary>The refraction factor on the Earth's radius for sight lines (the standard 7/6: d = 3.86 √h km).</summary>
    public double SightRefraction { get; init; } = 7.0 / 6.0;

    /// <summary>A grey warship's inherent contrast against the horizon sky by day (0 none, 1 black on white).</summary>
    public double SightContrastDay { get; init; } = 0.35;

    /// <summary>Its contrast silhouetted against the bright horizon toward a low sun.</summary>
    public double SightContrastSilhouette { get; init; } = 0.8;

    /// <summary>The contrast a searching eye needs to pick out a large object (Koschmieder's 0.02 is for one looked at
    /// where it's known to be; searching for it asks a few times more).</summary>
    public double SightThresholdContrast { get; init; } = 0.05;

    /// <summary>The threshold contrast rises as an object's solid angle (as magnified) shrinks: Cₜ = C∞ (1 + √(Ω꜀ / Ω))
    /// (between Riccò's law for the smallest and none for the largest), sr. About (14 mrad)².</summary>
    public double SightCriticalSolidAngle { get; init; } = 2e-4;

    /// <summary>What a visibility means (the logs' sense, not the meteorologist's): a ship showing this much side on,
    /// m², is made out by a lookout's binoculars at exactly the visibility; the haze's extinction follows from it.</summary>
    public double SightReferenceAreaM2 { get; init; } = 1000;

    /// <summary>The detection rate, per second, once an object is plainly visible (<see cref="SightFullMargin"/> times its
    /// threshold): a lookout's sweep comes round to it in this.</summary>
    public double SightRatePerS { get; init; } = 1.0 / 30;

    /// <summary>The margin over the threshold contrast at which the rate is full; between 1 and this it rises with the
    /// margin's logarithm.</summary>
    public double SightFullMargin { get; init; } = 3;

    /// <summary>How far an unlit ship shows at night (starlight, no moon), m: sight lines are cut to this in the dark.</summary>
    public double SightNightM { get; init; } = 5_000;

    /// <summary>The sun's elevation where night begins, degrees (nautical twilight's end); from 0° down to it the light
    /// fails, the sight lines shortening geometrically to <see cref="SightNightM"/>.</summary>
    public double SightNightSunDeg { get; init; } = -12;

    /// <summary>A target against the bright horizon (silhouetted): the sun between this and <see cref="SightSilhouetteHighDeg"/>
    /// of elevation, and within <see cref="SightSilhouetteArcDeg"/> of the line of sight's bearing beyond the target.</summary>
    public double SightSilhouetteLowDeg { get; init; } = -10;
    public double SightSilhouetteHighDeg { get; init; } = 6;
    public double SightSilhouetteArcDeg { get; init; } = 50;

    /// <summary>The binoculars' magnification at a masthead and on the bridge, and the conning tower's slits'.</summary>
    public double SightLookoutMagnification { get; init; } = 7;
    public double SightSlitsMagnification { get; init; } = 1;

    /// <summary>A searching eye covers this share of the horizon: a director's or rangefinder's narrow field searches
    /// slower than a lookout's binoculars.</summary>
    public double SightDirectorSearchFactor { get; init; } = 0.3;

    /// <summary>How often each navy's sighting is looked at in an encounter, ms (the plan's ~1 Hz).</summary>
    public int SightingMs { get; init; } = 1000;

    // ------------------------------------------------------------------ pictures (docs/battle/plan.md step 3.2d)

    /// <summary>How many of a side's ships nearest a target look for it each time.</summary>
    public int SightObservers { get; init; } = 6;

    /// <summary>How many of an observer's eyes (highest first, those bearing on the target) look.</summary>
    public int SightEyes { get; init; } = 4;

    /// <summary>The looks' grid in an encounter's tangent plane, m a cell.</summary>
    public double SightGridM { get; init; } = 8_000;

    /// <summary>No eye looks further than this, m (two 60 m mastheads' horizons together).</summary>
    public double SightMaxM { get; init; } = 60_000;

    /// <summary>A contact's dead reckoning allows for manoeuvre: its acceleration's 1-sigma, m/s².</summary>
    public double SightManoeuvreMs2 { get; init; } = 0.05;

    /// <summary>The first report's course and speed, judged by eye: their 1-sigma errors, degrees and m/s.</summary>
    public double SightCourseSigmaDeg { get; init; } = 20;
    public double SightSpeedSigmaMs { get; init; } = 3;

    /// <summary>The margins at which a contact's type and its class are made out, and the range within which its
    /// name is (with the class's margin).</summary>
    public double SightTypeMargin { get; init; } = 2;
    public double SightClassMargin { get; init; } = 4;
    public double SightNameM { get; init; } = 6_000;

    /// <summary>The chance a class first made out is taken for the most similar class in the world (a battlecruiser
    /// for a battleship), if one's like enough (its unlikeness under <see cref="SightSimilarScore"/>: funnels and masts
    /// one each, main mounts a half, length five per ratio).</summary>
    public double SightMisidentify { get; init; } = 0.3;
    public double SightSimilarScore { get; init; } = 1.5;

    /// <summary>A contact out of sight this long is forgotten, ms.</summary>
    public long SightForgetMs { get; init; } = 6 * 3_600_000L;

    // ------------------------------------------------------------------ shadowing (docs/battle/plan.md step 3.2f)

    /// <summary>A shadower given no range keeps this share of how far he can see (the visibility, or the dark's reach),
    /// between the least and the most, m: inside sight, outside the guns' best.</summary>
    public double ShadowVisibilityShare { get; init; } = 0.7;
    public double ShadowMinM { get; init; } = 3_000;
    public double ShadowMaxM { get; init; } = 20_000;

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

    /// <summary>How much of the rate the gap is closing at a follower takes off what he rings, so he eases off before
    /// he's on station rather than overshooting it (his engines answer in minutes): 0 rings on the gap alone.</summary>
    public double FollowSpeedDamping { get; init; } = 2;

    /// <summary>Following in line ahead (a follow, or a station with nothing abeam): the follower keeps the track the ship
    /// ahead steamed, so he turns where it turned. Its points a step apart, m; as many as are kept (enough for the
    /// longest gap astern, at 50 m a step 512 make 25 km); how far along the track ahead of himself he takes the heading
    /// he steers, in his lengths (where his helm will have answered: swept 0.5 to 5 on a battle line's 90° turn, 2 best;
    /// further, he turns before the ship ahead did); and how far off the track he may be before he joins its line the
    /// plain way, in his lengths.</summary>
    public double FollowTrackStepM { get; init; } = 50;
    public int FollowTrackPoints { get; init; } = 512;
    public double FollowTrackLookaheadLengths { get; init; } = 2;
    public double FollowTrackJoinLengths { get; init; } = 4;

    /// <summary>Steering the track: how many degrees off the track's heading he steers per metre he's off it, toward
    /// it, and at most (swept 0.05 to 0.5: 0.3 holds a line within ~130 m through a 90° turn; more rings speed more).</summary>
    public double FollowTrackCrossDegPerM { get; init; } = 0.3;
    public double FollowTrackCrossMaxDeg { get; init; } = 30;

    /// <summary>Steering the track: he corrects for where he'll be off it a while on at the rate he's closing or opening
    /// it, not where he is, looking ahead this multiple of his ship's Nomoto T (damped: his helm answers late, so on the
    /// offset alone he swings across, and down a line each ship passes the swing on a little larger): 0 steers on the
    /// offset alone.</summary>
    public double FollowTrackCrossLeadOverT { get; init; } = 1.25;

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
