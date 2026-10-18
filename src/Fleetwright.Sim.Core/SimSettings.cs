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

    /// <summary>A rendered frame, real seconds, for the user's rule above: a unit is placed on rails and its orders
    /// snap only where its own manoeuvre takes about this long or less at the asked speed. A ship's turn is
    /// <see cref="RailsSnapMinSpeed"/>'s; a line ahead's turn in succession (its last ship in the guide's wake reaching
    /// where the guide turned) is longer, so a long column snaps only at higher speeds (ShipSystem.Tiers.cs). A constant,
    /// not the display's: the sim never reads the display.</summary>
    public double RailsFrameS { get; init; } = 1.0 / 60;

    /// <summary>At this asked speed and above, every ship that isn't on rails is in the coarse clock tier: kinematic
    /// motion (constant acceleration, turns as arcs of the turning circle), decided for every
    /// <see cref="CoarseStepMs"/>; below it they're on the fine tick (docs/world/architecture.md "The speed picks the
    /// tier"). Just over 240× (the user, 2026-10-14; 240× 2026-10-13): 240× itself stays fine, the clock's fastest fine
    /// notch; a coarse frame is 4 game seconds or more, so the rudder and the swing left out aren't seen.
    /// The asked speed, never the achieved one: it's world state, so this is deterministic.</summary>
    public double CoarseMinSpeed { get; init; } = 241;

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

    /// <summary>The least encounter range, metres. Opposing ships within the encounter range of each other are in the fine
    /// tier, and ships on rails come off at the moment it's crossed; the range is the sight reach of the ships at sea (the
    /// tallest eye's and silhouette's horizons, docs/battle/fire-control.md F4d: Sim's SightReach), so the fine tier owns
    /// everything that could be seen, and never under this. It was a fixed 50 km.</summary>
    public double EncounterRangeMinM { get; init; } = 0;

    /// <summary>Ships go back on rails only this much further apart than the encounter range, metres, so a pair
    /// steaming along its edge doesn't flicker between the tiers.</summary>
    public double EncounterMarginM { get; init; } = 10_000;

    /// <summary>An encounter closes once its sides have been further apart than the range and margin for this long, ms
    /// (docs/battle/plan.md step 3.1b): its ships are held in the fine tier until then.</summary>
    public int EncounterCloseMs { get; init; } = 600_000;

    /// <summary>A scenario with no <c>"until"</c> of its own ends this long after its start, ms (docs/battle/command.md
    /// "The end").</summary>
    public long PlayLimitMs { get; init; } = 24 * 3_600_000L;

    /// <summary>A scenario whose sides have met ends once no encounter has been open for this long, ms: a stalemate, or
    /// sides that have gone their ways (docs/battle/command.md "The end").</summary>
    public long QuietEndMs { get; init; } = 2 * 3_600_000L;

    /// <summary>How often every pair of opposing rails is solved afresh, ms: the horizon a solve looks ahead to. A
    /// ship's rails changing (a waypoint, an order) solves it again at once.</summary>
    public int ContactScanMs { get; init; } = 3_600_000;

    /// <summary>The most steps one solve of a pair takes before it looks again later from where it got to.</summary>
    public int ContactTraceSteps { get; init; } = 64;

    // ------------------------------------------------------------------ weather (docs/battle/plan.md step 3.2b)

    /// <summary>The visibility of a world whose scenario gives none, metres: a clear day at sea.</summary>
    public double DefaultVisibilityM { get; init; } = 40_000;

    /// <summary>The wind of a world whose scenario gives none: the way it blows from (degrees) and its speed (m/s). A
    /// moderate breeze, Beaufort 4, so the sea (docs/visuals/sea.md) isn't glassy.</summary>
    public double DefaultWindFromDeg { get; init; } = 250;

    public double DefaultWindMs { get; init; } = 7.9;

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
    /// m², is made out by a lookout's binoculars at exactly the visibility; the haze's extinction follows from it. About a
    /// capital ship's side (Lion's 2,965 m²): re-fitted in fire-control.md F4d on the ships' real tops, where 1,000 m² had
    /// Lion first sight Derfflinger at a median 30 km in Jutland's 25 km against the record's ~24.</summary>
    public double SightReferenceAreaM2 { get; init; } = 3000;

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

    /// <summary>A look ordered at once (ShipSystem.LookNow, after a dev spawn) is over this many seconds: as if the ships
    /// had been there a while, so anything plainly visible is found (a 1 s look finds it one time in eight).</summary>
    public double SightLookNowS { get; init; } = 300;

    // ------------------------------------------------------------------ pictures (docs/battle/plan.md step 3.2d)

    /// <summary>How many of a side's ships nearest a target look for it each time.</summary>
    public int SightObservers { get; init; } = 6;

    /// <summary>How many of an observer's eyes (highest first, those bearing on the target) look.</summary>
    public int SightEyes { get; init; } = 4;

    /// <summary>The looks' grid in an encounter's tangent plane, m a cell.</summary>
    public double SightGridM { get; init; } = 8_000;

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

    // ------------------------------------------------------------------ shells (docs/battle/fighting.md 4.2a)

    /// <summary>The top of a shell's danger window, metres over the sea: nothing at sea is taller, so only the flight
    /// below it can hit a ship (docs/battle/plan.md "Shells").</summary>
    public double ShellWindowTopM { get; init; } = 60;

    /// <summary>The shells in flight the world starts with room for (it grows past it), and the splashes it keeps for
    /// the screens: how many, and for how long, ms.</summary>
    public int ShellCapacity { get; init; } = 512;
    public int SplashCapacity { get; init; } = 4096;
    /// <summary>The mounts fired kept for the screens (docs/visuals/muzzle-flashes.md), for <see cref="SplashLifeMs"/>.</summary>
    public int ShotCapacity { get; init; } = 2048;
    public int SplashLifeMs { get; init; } = 20_000;
    /// <summary>The magazines set off kept for the screens (docs/visuals/magazine-explosions.md), how many and for how
    /// long, ms: the pall drifts for minutes.</summary>
    public int BlastCapacity { get; init; } = 64;
    public int BlastLifeMs { get; init; } = 900_000;

    /// <summary>The world's ship index (docs/battle/fighting.md, "One index of where things are"): its cubes' width, m,
    /// and how old it may get before a query rebuilds it, ms (a query's reach grows by the fastest ship's run since).</summary>
    public double ShipIndexCellM { get; init; } = 5000;
    public int ShipIndexMaxAgeMs { get; init; } = 2000;

    /// <summary>The dev's "fire at" (until fire control, 4.2b): each shell's error in range, 1 sigma, as a percentage of
    /// the range, and across it as a share of that.</summary>
    public double DevFirePatternPct { get; init; } = 0.8;
    public double DevFireDeflectionShare { get; init; } = 0.4;

    // ------------------------------------------------------------------ fire control (docs/battle/fighting.md 4.2b)

    /// <summary>The engaged batteries' fire-control tick, ms, on a grid of world time: readings taken and taken in, the
    /// mounts ordered, the next salvo timed.</summary>
    public int FireControlMs { get; init; } = 1000;

    /// <summary>Battle against practice: the multiplier on the salvo-to-salvo, laying, rangetaking and spotting errors
    /// (fire_control_ref.py Conditions.combat, calibrated at 1.5).</summary>
    public double GunneryCombat { get; init; } = 1.5;

    /// <summary>How long after a battery's track starts (its first reading in the computer) it opens fire, s
    /// (fire_control_ref.py Scenario.t_open).</summary>
    public double GunneryOpenS { get; init; } = 120;

    /// <summary>A mount bears when trained and elevated within this of its order, degrees.</summary>
    public double LayToleranceDeg { get; init; } = 1;

    /// <summary>Spotting: the judgement comes this long after a salvo's last shell lands, ms; a splash is judged over or
    /// short with an error of k·R²/h rad (h the spotter's height; fire-control-research.md §9), and the deflection seen
    /// to this many mil, a share of it corrected (half: one salvo's fall carries its random error, so full corrections
    /// chase noise) once it's over a quarter of a pattern; the ladder's smallest step, m (and never under half a
    /// pattern).</summary>
    public int SpotDelayMs { get; init; } = 6000;
    public double SpotOpticK { get; init; } = 4e-5;
    public double SpotDeflectionMil { get; init; } = 1;
    public double SpotDeflectionGain { get; init; } = 0.5;
    public double SpotLadderMinM { get; init; } = 46;

    /// <summary>A shell's dispersion across the line of fire as a share of its dispersion along it (Gun.defl_ratio).</summary>
    public double DispersionDeflectionRatio { get; init; } = 0.4;

    /// <summary>Rangefinding (fire-control-research.md §3.2): a reading's unit of error is the optics' (their acuity,
    /// rigid base and flex: the fit's tech block, F3); an instrument's own bias after calibration, in units of error; its
    /// bearing's error, mil.</summary>
    public double RangefinderBiasUnits { get; init; } = 1.5;
    public double RangefinderBearingMil { get; init; } = 1;

    /// <summary>A range by eye: its error and its bias as shares of the range, its bearing's error, mil.</summary>
    public double EyeRangeShare { get; init; } = 0.20;
    public double EyeRangeBiasShare { get; init; } = 0.10;
    public double EyeBearingMil { get; init; } = 5;

    /// <summary>Own ship counts as turning (its salvos take its computer's own-turn kick) past this rate of yaw, °/s.</summary>
    public double OwnTurnYawDegS { get; init; } = 0.2;

    /// <summary>A side's gun crews, a row for each <see cref="CrewQuality"/> in order: the rangetakers' error (Conditions.
    /// operator: 2 green, 1.5 average, 1 trained, 0.85 elite) and the layers' (a guess, the same way).</summary>
    public GunneryCrew[] GunneryCrews { get; init; } =
    [
        new() { Name = "green", Operator = 2, Lay = 1.3 },
        new() { Name = "average", Operator = 1.5, Lay = 1.15 },
        new() { Name = "trained", Operator = 1, Lay = 1 },
        new() { Name = "elite", Operator = 0.85, Lay = 0.9 },
    ];

    // ------------------------------------------------------------------ fire orders (docs/battle/fighting.md 4.2c)

    /// <summary>Fire at will: a battery takes contacts in sight within its reach (the range table at its highest
    /// elevation) times <see cref="FireReachShare"/>, so it tracks one a little before it can fire, and keeps one to
    /// <see cref="FireKeepShare"/> of it (no flicking at the edge); it keeps its target until the contact is unseen for
    /// <see cref="FireLostMs"/>, or another is nearer than <see cref="FireSwitchShare"/> of its range.</summary>
    public double FireReachShare { get; init; } = 1.1;
    public double FireKeepShare { get; init; } = 1.25;
    public long FireLostMs { get; init; } = 30_000;
    public double FireSwitchShare { get; init; } = 0.7;

    /// <summary>An admiral distributing fire: the enemy's line is the contacts in sight chained to the target, each
    /// within this of the next, m.</summary>
    public double FireLineGapM { get; init; } = 3_000;

    /// <summary>Fight (a captain's minimal fighting manoeuvre): how far either side of the range he keeps is near enough
    /// (a share of it); the angle off the line of sight he closes or opens at, degrees; the radius the enemy round him is
    /// judged in (the matchup, docs/battle/command.md 5.1g), m; and turned away, the share of doctrine's bar he must be
    /// over again to turn back (the bar over it). The range and the bar are doctrine's (RangeShare, OutgunnedRatio).</summary>
    public double FightBandShare { get; init; } = 0.15;
    public double FightCloseDeg { get; init; } = 40;
    public double FightCompareM { get; init; } = 30_000;
    public double FightRallyShare { get; init; } = 0.75;

    /// <summary>A fighting unit whose matchup against the enemy round it is this confident or more, and which expects to
    /// lose under FightFinishLostShare of its tonnage, can hardly be hurt (a cripple, silent or stopped; not torpedo boats,
    /// whose torpedoes can): it closes to FightFinishShare of its band to finish him (docs/battle/command.md 5.1g).</summary>
    public double FightFinishConfidence { get; init; } = 10;
    public double FightFinishLostShare { get; init; } = 0.02;
    public double FightFinishShare { get; init; } = 0.4;

    /// <summary>A clear line of fire (docs/battle/command.md 5.1a): a gun's is foul while a friend is within FireFoulM of
    /// where its shells come down (from FireFoulBeforeM short of the target to FireFoulM past it), a torpedo's while one
    /// will come within TorpedoFoulM of the torpedoes on their run, each going on as she is (and her margin growing with
    /// TorpedoFoulTurnDeg); neither counts the first
    /// FireFoulNearM off the shooter. m; the grid's cube.</summary>
    public double FireFoulM { get; init; } = 400;

    /// <summary>A torpedo craft holds steady (stops evading) within this share of her attack range, to fire
    /// (docs/battle/command.md 5.1a; the evasion research's 30 s before the launch).</summary>
    public double EvadeSteadyShare { get; init; } = 1.25;
    public double FireFoulBeforeM { get; init; } = 2_000;
    public double FireFoulNearM { get; init; } = 300;
    public double TorpedoFoulM { get; init; } = 200;

    /// <summary>A friend's margin from a torpedo grows with the time it takes to reach her: as far as she could have
    /// turned off her course by this much (a ship manoeuvres; a flotilla's boat astern is passed in seconds, a ship in a
    /// mêlée minutes on). Degrees.</summary>
    public double TorpedoFoulTurnDeg { get; init; } = 20;
    public double LineOfFireCellM { get; init; } = 5_000;

    /// <summary>Torpedoes seen (docs/battle/command.md 5.1a): a wake is seen within TorpedoSeenM of a ship while the sun is
    /// over TorpedoDayElevationDeg, within TorpedoSeenNightM otherwise; one passing within her half-length and
    /// TorpedoCombMarginM is coming at her; she holds the comb TorpedoCombAfterS after it's past. m, degrees, s.</summary>
    public double TorpedoSeenM { get; init; } = 2_000;
    public double TorpedoSeenNightM { get; init; } = 400;
    public double TorpedoDayElevationDeg { get; init; } = -6;
    public double TorpedoCombMarginM { get; init; } = 100;
    public double TorpedoCombAfterS { get; init; } = 20;

    /// <summary>A ship out of the fight going home stops this near it, m (docs/battle/command.md 5.1a).</summary>
    public double HomeRadiusM { get; init; } = 10_000;

    /// <summary>Manoeuvres (docs/battle/command.md 5.1b): a turn together is done when every ship of the line is within
    /// ManoeuvreDoneDeg of the new course (the stations are given again then), or after ManoeuvreMaxMs whatever; turning
    /// the rear ship first, each puts her helm over BattleTurnStepS after the one astern of her. Degrees, ms, s.</summary>
    public double ManoeuvreDoneDeg { get; init; } = 5;
    public long ManoeuvreMaxMs { get; init; } = 600_000;
    public double BattleTurnStepS { get; init; } = 10;

    /// <summary>A torpedo attack coming at a line (docs/battle/command.md 5.1b): enemy torpedo craft within
    /// TorpedoThreatM of a ship of it and closing on her faster than TorpedoThreatClosingMs, or a ship of it combing a
    /// track; a line turned away (doctrine's TurnAwayDeg) turns back once it has seen none for TurnBackS. m, m/s, s.</summary>
    public double TorpedoThreatM { get; init; } = 8_000;
    public double TorpedoThreatClosingMs { get; init; } = 3;
    public double TurnBackS { get; init; } = 180;

    /// <summary>A flotilla's attack (docs/battle/command.md 5.1b): it first gains a point AttackGainDeg off the target's bow,
    /// AttackGainShare of its torpedoes' fast range from it, on the side it's on; it runs in from there (or once it's that far
    /// round the target's bow, or after AttackGainMaxMs). Degrees, a share, ms.</summary>
    public double AttackGainDeg { get; init; } = 50;
    public double AttackGainShare { get; init; } = 1.5;
    public long AttackGainMaxMs { get; init; } = 900_000;

    /// <summary>A flotilla's signal to fire holds this long: each boat fires once as her tubes bear, s.</summary>
    public double AttackFireS { get; init; } = 60;

    /// <summary>A screen (docs/battle/command.md 5.1b) keeps ScreenM off the unit it screens unless told otherwise, and is
    /// given a new station only when the bearing to the enemy moves it more than ScreenMoveM. m.</summary>
    public double ScreenM { get; init; } = 5_000;
    public double ScreenMoveM { get; init; } = 1_000;

    /// <summary>Tasks (docs/battle/command.md 5.1c): the task layer thinks every ForcePeriodMs while any unit on a task is
    /// on the fine tick; a GoTo is there within ArrivedM; a Join takes its unit in within JoinM of it; a task a fight
    /// interrupted goes on once its unit has seen no enemy for TaskResumeS. ms, m, m, s.</summary>
    public int ForcePeriodMs { get; init; } = 10_000;
    public double ArrivedM { get; init; } = 2_000;
    public double JoinM { get; init; } = 3_000;
    public double TaskResumeS { get; init; } = 300;

    /// <summary>The scouting tasks (docs/battle/command.md 5.1c): a scout keeps ScoutM ahead of its unit; a search steams
    /// legs SearchLegM long, a group spread abreast SearchSpacingShare of the visibility apart (in sight of each other); a
    /// covering unit keeps CoverM off the one it covers. m, a share.</summary>
    public double ScoutM { get; init; } = 20_000;
    public double SearchLegM { get; init; } = 60_000;
    public double SearchSpacingShare { get; init; } = 0.9;
    public double CoverM { get; init; } = 20_000;

    /// <summary>The tactical picture (docs/battle/command.md 5.1c): enemy contacts within TacticalGapM of each other are one
    /// group. A force deploys (doctrine's Deploy) when the enemy's main body is within DeployM of its guide; it's deployed
    /// when every ship is within DeployFormedM of her station in the one line, or after DeployMaxMs. m, m, m, ms.</summary>
    public double TacticalGapM { get; init; } = 6_000;
    public double DeployM { get; init; } = 30_000;
    public double DeployFormedM { get; init; } = 1_000;
    public long DeployMaxMs { get; init; } = 1_800_000;

    /// <summary>Falling back on the main body (docs/battle/command.md 5.1c): a unit turned away from a stronger enemy runs
    /// for the nearest own force within FallBackM that's a match for him, if that's within FallBackDeg of straight away.
    /// m, degrees.</summary>
    public double FallBackM { get; init; } = 150_000;
    public double FallBackDeg { get; init; } = 100;

    /// <summary>The side's commander (docs/battle/command.md 5.1d) thinks every CommanderPeriodMs. A contact out of sight
    /// longer than ContactFreshMs is lost: an intercept gives it up, and he holds nothing by it. His target (his phases,
    /// his search) is the biggest ship of the enemy group he's least confident against (5.1g), kept until another group
    /// is CommanderRetargetShare times graver; he sends a flotilla in on an enemy group within CommanderAttackM of it that
    /// its matchup has it attacking. ms, ms, a ratio, m.</summary>
    public int CommanderPeriodMs { get; init; } = 60_000;
    public long ContactFreshMs { get; init; } = 1_800_000;
    public double CommanderRetargetShare { get; init; } = 1.5;
    public double CommanderAttackM { get; init; } = 25_000;

    /// <summary>He declines battle once his side's matchup against all he holds is under doctrine's bar by this share
    /// (and he can get away), and accepts it again once it's over the bar by this share: his margin either way.</summary>
    public double CommanderMarginShare { get; init; } = 0.25;

    /// <summary>The enemy lost, his main body searches where he was last held on legs this long (m), and gives up after.</summary>
    public double LostSearchLegM { get; init; } = 20_000;

    /// <summary>It's night to a commander (doctrine's NightAction and NightAttack) with the sun more than this far down at
    /// his main body, degrees (civil twilight's end).</summary>
    public double NightSunElevationDeg { get; init; } = -6;

    /// <summary>What doctrine's Aggression (-1 timid to +1 bold) does: each listed field is read as its value x (1 + shift
    /// x aggression). Bold: worse odds taken (OutgunnedRatio up) and sought (SeekRatio down), losses borne longer
    /// (BreakOffShare, AttackBreakOffShare up), nearer (RangeShare down), in the line longer (the fall-out thresholds), a
    /// smaller turn from torpedoes.</summary>
    public Dictionary<string, double> AggressionShift { get; init; } = new()
    {
        ["OutgunnedRatio"] = 0.5, ["BreakOffShare"] = 0.5, ["AttackBreakOffShare"] = 0.4, ["RangeShare"] = -0.3,
        ["FallOutSpeedShare"] = -0.3, ["FallOutGunsShare"] = -0.3, ["FallOutListDeg"] = 0.3, ["FallOutWaterShare"] = 0.3,
        ["TurnAwayDeg"] = -0.5, ["SeekRatio"] = -0.5,
    };

    /// <summary>The salvos the world keeps in flight for spotting (it grows past it), and the combat log's ring.</summary>
    public int SalvoCapacity { get; init; } = 256;
    public int CombatLogCapacity { get; init; } = 4096;

    // ------------------------------------------------------------------ damage (docs/battle/fighting.md 4.3a)
    // research/simulation/damage-research/gunfire_ref.py and 08-gunfire-effects.md: the [INFERRED] numbers are its
    // tuning values, fitted to the war-damage cases it names.

    /// <summary>Whether hits do anything (the damage system's). Off, they're counted and logged only, as before 4.3a: a
    /// bench calibrating fire control on its own (the duel, the gunnery tests) turns it off.</summary>
    public bool HitsDoDamage { get; init; } = true;

    /// <summary>A filler that doesn't detonate fully (its explosive's rate with the shell's booster) goes low-order with
    /// this chance, else it's a dud (damage/12 §4 stage 2 [EST]); a low-order burst has <see cref="LowOrderK"/> of the
    /// blast. The shell's own numbers decide the rest: its fuze's start, delay and blinds, its explosive's bursts in the
    /// plate (docs/battle/shells-armour-damage.md P3, which replaced the outcome table's rows).</summary>
    public double LowOrderShare { get; init; } = 0.75;

    /// <summary>A two-plate fuze adds up the thin plates it meets within this many calibres of the last (in quick
    /// succession: a superstructure's decks and walls) [EST].</summary>
    public double FuzeSumCalibres { get; init; } = 10;

    /// <summary>The shell's state on the plates (docs/battle/shells-armour-damage.md P2; armor/00 §5.3, 02 §3.1, 05
    /// §4g): a face-hardened plate that shatters the shell holds as if ShatterBonus times thicker at normal (Okun's
    /// +30% on velocity; the early trials fit ×1.4 in thickness), fading to none at ShatterToDeg; neither a bare nose
    /// shatters nor a body breaks near its limit on a plate under BreakFromTD calibres (armor_pen_ref.py's gate). A body
    /// getting through under NearLimitBand times the plate's limit breaks with NearLimitBreakFloor's chance under 30° and
    /// past 60°, NearLimitBreakPeak's at 45° [INFERRED]. A body past its proof's obliquity breaks on face-hardened plate
    /// with a chance rising to sure BreakSpanDeg past it (RN 1914 APC broke at 15-20°, armour-and-damage.md §3.1).</summary>
    public double ShatterBonus { get; init; } = 1.4;
    public double ShatterToDeg { get; init; } = 55;
    public double BreakFromTD { get; init; } = 0.25;
    public double NearLimitBand { get; init; } = 1.1;
    public double NearLimitBreakFloor { get; init; } = 0.1;
    public double NearLimitBreakPeak { get; init; } = 0.5;
    public double BreakSpanDeg { get; init; } = 5;

    /// <summary>Penetration: a common shell's capability as a share of AP's; an HE shell's nose fuze punches this many
    /// calibres (times the square root of the obliquity's cosine) and bursts.</summary>
    public double CommonPenShare { get; init; } = 0.6;
    public double HePenCalibres { get; init; } = 0.2;

    /// <summary>A plate's quality on a ship, drawn once for each plate of hers (its lot: a normal draw of this sigma
    /// about 1, on its limit velocity), and each shot's scatter about it (docs/battle/shells-armour-damage.md P7;
    /// armor/00 §3: 7% through at 0.9 of the limit, 50% at it, 93% at 1.1).</summary>
    public double PlateLotSigma { get; init; } = 0.06;
    public double PlateShotSigma { get; init; } = 0.03;


    /// <summary>A whole shell may glance off a plate from this obliquity, degrees (the biting angle: below it the nose
    /// digs in; docs/battle/shells-armour-damage.md P5): when its motion across the plate is under this share of what
    /// the plate stops square on (Terminal.Glances). It skids off this many degrees off the plate's face.</summary>
    public double RicochetFromDeg { get; init; } = 65;
    public double GlanceShare { get; init; } = 1;
    public double GlanceLiftDeg { get; init; } = 1;

    /// <summary>A plate's turn of a shell through it (Terminal.Turn) bends the hit's path from this many degrees; a
    /// smaller one only costs it speed.</summary>
    public double NormaliseMinDeg { get; init; } = 0.5;


    /// <summary>A shell crossing a machinery space (a boiler or engine room) is armed by what's in it (turbines, gears,
    /// boilers: Johnston) with this chance.</summary>
    public double MachineryArmChance { get; init; } = 0.45;

    /// <summary>A shell under the water (water is a crossing, not a stop; WaterRun) slows as e^(−x/L), L this many
    /// calibres: a pointed shell, its tumbling folded in (a stable ogive ~150, armor/05 §6; 25 fits 08 §3.8's 30-50% of
    /// its velocity a few metres down and Tosa's 76 mm), and a diving one's flat nose (~45-60).</summary>
    public double UnderwaterDecayCalibres { get; init; } = 25;
    public double UnderwaterDecayCalibresDiving { get; init; } = 50;

    /// <summary>The hit query follows a shell this many decay lengths on under the water (3: down to 5% of its speed; a
    /// 15" shell 29 m). Physical, not tuned to historical loss rates: without damage control more ships flood and sink
    /// than history saw, and that's accepted (user, 2026-10-16). The fuze started on the water
    /// (<see cref="FuzeStartsOnWater"/>) is what ends an ordinary shell's run sooner.</summary>
    public double UnderwaterReachDecays { get; init; } = 3;

    /// <summary>A base fuze starts on the water, its delay running from there (armor/04 §3.5; P6's scaffold, on since P3
    /// of docs/battle/shells-armour-damage.md; off, only plates and machinery start it).</summary>
    public bool FuzeStartsOnWater { get; init; } = true;

    /// <summary>The burst's radii, m, times the cube root of its TNT kg: equipment wrecked by its shock and fragments, in
    /// the open and in the near zone of a burst inside (docs/battle/shells-armour-damage.md E5; damage/11 §3.3, §5.1: was
    /// 1.6, an internal number); a contact burst's hole in plating, times the square root of 6 mm over the plate (11 §3.1:
    /// unbiased against 13 holes). People are E6's (<see cref="CasualtyS"/>).</summary>
    public double WreckRadiusK { get; init; } = 1.0;
    public double HoleRadiusK { get; init; } = 0.45;

    /// <summary>A burst in a closed space (E5; damage/11 §1.2-§4): the gas pressure P = <see cref="BlastHeatK"/>·η·[Q_d +
    /// φ·(Q_c − Q_d)]·W/V MPa (W the filler's kg, V the space's gas, φ = 1/(1 + W·air/V) the afterburn its air allows);
    /// it blows down through the vents A as exp(−<see cref="VentC"/>·<see cref="VentAlpha"/>·A·t/V), measured
    /// <see cref="BurnStretch"/>× longer, and a panel feels P·min(<see cref="BlastLoadFactor"/>, π·t/<see cref="PanelMs"/>);
    /// none at all once α·A ≥ <see cref="OpenVentRatio"/>·V^⅔ (open). A boundary deforms from <see cref="DeformMPaMm"/> per
    /// mm of plate and ruptures at <see cref="RuptureMPaMm"/> (riveted × <see cref="RivetedK"/>); a door goes at
    /// <see cref="DoorShare"/> of its bulkhead's, at least <see cref="DoorMinMPa"/>, opening <see cref="DoorM2"/>; a
    /// ruptured boundary opens <see cref="RupturedShare"/> of its area to the flooding (all of it to the gas). In the near
    /// zone (WreckRadiusK·W^⅓) a boundary is holed <see cref="NearHoleK"/>·W^⅓ across.</summary>
    public double BlastHeatK { get; init; } = 0.28;
    public double AtmosphereMPa { get; init; } = 0.101;
    public double VentC { get; init; } = 725;
    public double VentAlpha { get; init; } = 0.6;
    public double BurnStretch { get; init; } = 2;
    public double PanelMs { get; init; } = 15;
    public double BlastLoadFactor { get; init; } = 1.3;
    public double OpenVentRatio { get; init; } = 0.6;
    public double RuptureMPaMm { get; init; } = 0.06;
    public double DeformMPaMm { get; init; } = 0.012;
    public double RivetedK { get; init; } = 0.85;
    public double DoorShare { get; init; } = 0.5;
    public double DoorMinMPa { get; init; } = 0.08;
    public double DoorM2 { get; init; } = 1.2;
    public double RupturedShare { get; init; } = 0.25;
    public double NearHoleK { get; init; } = 0.5;

    /// <summary>The flame a burst's gas carries (E5; the user's choice, 2026-10-17): its own hot products, m³ a kg of
    /// filler at flame temperature [EST: TNT's ~0.75 m³/kg of gas at STP, ×7 at ~2,000 K]. A magazine or handling room
    /// the gas reaches is a flash source only if the burst was in it, or the products fill the spaces reached before it
    /// (a 4" HE's don't get past its own space, a bomb's do).</summary>
    public double HotProductsM3Kg { get; init; } = 5;

    /// <summary>A deformed boundary's seam leak (E5; damage/17 leakage §3.2, <c>leak_ratio</c>): D = felt/deform; none below
    /// <see cref="LeakWeepD"/>, <see cref="LeakR1"/>·D² cm²/m² to D 1, then log-linear to <see cref="LeakR5"/> at D 5;
    /// lognormal about it (σ <see cref="LeakSigma"/>), welded × <see cref="LeakWeldedK"/>.</summary>
    public double LeakWeepD { get; init; } = 0.3;
    public double LeakR1 { get; init; } = 0.3;
    public double LeakR5 { get; init; } = 100;
    public double LeakSigma { get; init; } = 0.8;
    public double LeakWeldedK { get; init; } = 0.4;

    /// <summary>A space's vents to the outside by its room kind (as RoomKind's list: stores, accommodation, double
    /// bottom, magazine, bunker, boiler room, engine room, steering, TDS, torpedo room, fuel tank, cargo tank, hold,
    /// handling, hangar), as a share of V^⅔ [EST until the designer emits them: a boiler room's uptakes, vent trunks];
    /// a part's by its component kind (as PartVital's), 1 and over open (an open mount, a mast, a funnel); the free
    /// share of a part's box [EST].</summary>
    public double[] RoomVentShare { get; init; } = [0.01, 0.02, 0, 0.005, 0.01, 0.15, 0.03, 0.01, 0, 0.02, 0, 0, 0.02, 0.02, 0.1];
    public double[] PartVentShare { get; init; } = [0.05, 0.1, 1, 1, 0.02, 0.15, 0.05, 1, 1, 0.3, 0.2, 1, 0, 0.01, 1, 1, 1, 1, 0.1];
    public double PartAirShare { get; init; } = 0.7;

    /// <summary>What a space's or a part's gear takes from the gas, MPa (11 §4.3 [EST]: fittings 0.03, electrical 0.10,
    /// auxiliary 0.25, machinery 0.6), by room kind and component kind: out of action from it, damaged from
    /// <see cref="BlastDamagedShare"/> of it.</summary>
    public double[] RoomBlastMPa { get; init; } = [0.03, 0.03, 0.25, 0.25, 0.6, 0.25, 0.25, 0.25, 0.6, 0.25, 0.6, 0.6, 0.03, 0.25, 0.03];
    public double[] PartBlastMPa { get; init; } = [0.10, 0.10, 0.25, 0.10, 0.25, 0.03, 0.10, 0.25, 0.25, 0.25, 0.10, 0.25, 0.6, 0.6, 0.6, 0.6, 0.25, 0.03, 0.03];
    public double BlastDamagedShare { get; init; } = 0.5;

    /// <summary>A burst's force as a share of one inside: in the plate, outside on it, and low-order.</summary>
    public double BurstInPlateK { get; init; } = 0.4;
    public double BurstOutsideK { get; init; } = 0.15;
    public double LowOrderK { get; init; } = 0.3;

    /// <summary>A part within this share of the wreck radius is destroyed, within it disabled.</summary>
    public double DestroyedShare { get; init; } = 0.5;

    /// <summary>A space's vitals as a share of its floor, by its kind (stores, accommodation, double bottom, magazine,
    /// bunker, boiler room, engine room, steering, TDS, torpedo room, fuel tank, cargo tank, hold): a burst inside it
    /// reaches them with this chance and its wreck area's share of the floor (times its force), a shell passing
    /// through with this chance; a burst's fragments through a bulkhead reach the next space's at
    /// <see cref="NeighbourShare"/> of that. Boilers and engines fill their rooms; propellant lies low in its magazine,
    /// in cases.</summary>
    public double[] RoomVital { get; init; } = [0.3, 0.3, 0.3, 0.15, 0.3, 0.5, 0.5, 0.6, 0.3, 0.3, 0.3, 0.3, 0.3];

    /// <summary>A part's vitals as a share of it, by its kind (main, secondary and torpedo mounts, AA, barbette,
    /// superstructure, conning tower, funnel, uptake, casing, sponson, mast, shaft, shaft alley, propeller, rudder,
    /// torpedo tube, flight deck, hangar bay): a burst's fragments that get into it damage it at this share of their
    /// count (docs/battle/shells-armour-damage.md E1, the user's choice: like RoomVital). A funnel or uptake holed by
    /// splinters keeps its draught; an open mount's crew and sights, a conning tower's instruments and a tube's
    /// torpedoes don't [EST].</summary>
    public double[] PartVital { get; init; } = [0.3, 0.3, 0.5, 0.5, 0.2, 0.2, 0.5, 0.05, 0.05, 0.1, 0.1, 0.1, 0.05, 0.2, 0.05, 0.05, 0.5, 0.1, 0.2];

    /// <summary>A boiler room keeps this share of its steam with every funnel it draws through wrecked (the draught
    /// lost; 08-gunfire-effects.md §8: funnel hits cost draught, not the boilers).</summary>
    public double DraughtFloor { get; init; } = 0.7;
    public double NeighbourShare { get; init; } = 0.5;

    /// <summary>Holes: a shell that passes through plating leaves a hole this many calibres across; plating holed up to
    /// this far over the waterline lets water in.</summary>
    public double ShellHoleCalibres { get; init; } = 1.5;
    public double HoleOverWaterlineM { get; init; } = 0.5;

    /// <summary>A belt that held still leaks (docs/battle/shells-armour-damage.md E4; damage/13 §2, <c>belt_leak</c>): the
    /// chance by where it struck against the waterline now (within <see cref="BeltWaterlineM"/> of it, higher, or within
    /// <see cref="BeltLowerEdgeM"/> of the plate's lower edge), over 1 + <see cref="BeltJointShare"/>·(joint − 1), times
    /// size(I)·margin(r) as a turret's, on a plate joint (within <see cref="BeltJointM"/> of a butt or seam of plates
    /// <see cref="BeltPlateM"/> long and high from the row's corner [EST]) by <see cref="BeltJointK"/>, near a leak before
    /// (within <see cref="BeltRepeatM"/>: the plate loosened) by <see cref="BeltRepeatK"/>, behind a welded skin by
    /// <see cref="WeldedSkinK"/>, at most <see cref="BeltLeakMax"/>. A loosened plate defends at
    /// <see cref="LoosenedPlate"/> of itself.</summary>
    public double[] BeltLeakChance { get; init; } = [0.50, 0.25, 0.80];
    /// <summary>How far behind the plate the cell it leaks into begins, at most (its backing and the skin between) [EST].</summary>
    public double BeltBehindM { get; init; } = 2;
    public double BeltWaterlineM { get; init; } = 1;
    public double BeltLowerEdgeM { get; init; } = 1;
    public double[] BeltPlateM { get; init; } = [5, 2.5];
    public double BeltJointM { get; init; } = 0.2;
    public double BeltJointShare { get; init; } = 0.25;
    public double BeltJointK { get; init; } = 1.5;
    public double BeltRepeatM { get; init; } = 3;
    public double BeltRepeatK { get; init; } = 2;
    public double WeldedSkinK { get; init; } = 0.6;
    public double BeltLeakMax { get; init; } = 0.95;
    public double LoosenedPlate { get; init; } = 0.85;

    /// <summary>A leak's regime (13 §2.3 [FIT]): seep, moderate, major, lost by these shares, the worse two times (0.5 +
    /// size·margin), by <see cref="LeakJointK"/> on a joint and <see cref="LeakRepeatK"/> near a leak before; its opening
    /// into the cell behind, m², uniform in log space between each regime's pair. Damage control's rows (caulked, wedged,
    /// matted) and the reopening at speed wait for damage control.</summary>
    public double[] LeakRegimeShare { get; init; } = [0.55, 0.30, 0.12, 0.03];
    public double[] LeakRegimeM2 { get; init; } = [2e-4, 2e-3, 3e-3, 2.5e-2, 0.05, 0.2, 0.5, 2];
    public double LeakJointK { get; init; } = 1.6;
    public double LeakRepeatK { get; init; } = 1.5;

    /// <summary>A leak into a fuel tank or the double bottom (feed water, oil) fouls the nearest boiler or engine room's
    /// supply with this chance, for <see cref="FeedCasualtyMin"/> minutes, log-uniform (13 §2.3: Lion's port engine,
    /// Malaya's A boiler room [FIT]; the length [EST]).</summary>
    public double FeedCasualtyChance { get; init; } = 0.15;
    public double[] FeedCasualtyMin { get; init; } = [10, 60];

    /// <summary>Shock through the structure from a heavy hit that held on a belt, a barbette, a turret or a conning tower
    /// (docs/battle/shells-armour-damage.md E4; damage/13 §3.4 [EST]): per hit, times (I / NonPenImpulseRef)^0.7 to at most
    /// ShockSizeMax, the chances by the ship's shock grade: G0 the first (13's 1939-42 column, gear unsecured), G1 the
    /// second (its 1943- column, after the securing alterations), G2 and G3 the second times <see cref="ShockGradeK"/>'s.
    /// Zone A the struck structure and what's within <see cref="ShockZoneAM"/> of the hit, B within
    /// <see cref="ShockZoneBM"/>, C the ship. Rows: local power tripped (A), a section's power (B), the ship's power
    /// cascading out (C), a director's gyro (A and B), a rangefinder knocked out of adjustment (A). Power reaches only a
    /// ship with director fire control (its breakers, FC circuits and gyros; before it, fuses and knife switches): its
    /// mounts and directors are silent while it's off. How long each lasts: (share, low, high minutes) triples,
    /// log-uniform, a high of 0 for the rest of the battle.</summary>
    public double[] ShockLocalTrip { get; init; } = [0.50, 0.25];
    public double[] ShockSectionPower { get; init; } = [0.15, 0.06];
    public double[] ShockCascade { get; init; } = [0.02, 0.008];
    public double[] ShockDirectorGyro { get; init; } = [0.12, 0.06];
    public double[] ShockRangefinder { get; init; } = [0.20, 0.12];
    public double[] ShockGradeK { get; init; } = [1, 1, 0.5, 0.25];
    public double ShockZoneAM { get; init; } = 12;
    public double ShockZoneBM { get; init; } = 40;
    public double[] LocalTripMin { get; init; } = [0.9, 0.17, 3, 0.1, 3, 15];
    public double[] SectionPowerMin { get; init; } = [0.8, 1, 5, 0.2, 5, 20];
    public double[] CascadeMin { get; init; } = [0.9, 1, 10, 0.1, 30, 90];
    public double[] DirectorGyroMin { get; init; } = [0.7, 5, 30, 0.3, 30, 60];
    public double[] RangefinderMin { get; init; } = [0.5, 5, 20, 0.3, 120, 240, 0.2, 0, 0];

    /// <summary>The ship's own main battery shaking its breakers (13 §3.4, South Dakota's ABT): per 20 minutes of fire, by
    /// grade as the shock's columns, a section's power out round the firing mount (<see cref="SectionPowerMin"/>).</summary>
    public double[] OwnGunBreaker { get; init; } = [0.05, 0.02];

    /// <summary>A burst's single pieces (docs/battle/shells-armour-damage.md E1; damage/13 §2.5), walked on through the
    /// ship from the burst like a shell: an AP or common shell's nose slug, a blunt broken penetrator SlugCalibres of the
    /// calibre across, and the base plug, PlugCalibres across (Ralph Talbot's 8" plugs 5.6" across), both of
    /// PieceQuality (a plate is that share as hard to them as to a whole shell) [EST]; slower than PieceMinMs a piece
    /// falls where it is.</summary>
    public double SlugCalibres { get; init; } = 0.75;
    public double PlugCalibres { get; init; } = 0.7;
    public double PieceQuality { get; init; } = 0.6;
    public double PieceMinMs { get; init; } = 30;

    /// <summary>E2: a shell that holed a plate in pieces (holed, shattered through, or broken just over the limit) bursts
    /// weakly at the plate: its fragments (E1's field) at this share of a full burst's, from just behind the plate at its
    /// pieces' speed, no blast and no fire (damage/16 §4.3: "about 0.15×"). Into an enclosed turret or its barbette, a
    /// flash source for the magazine as a burst there is (Seydlitz at Dogger Bank).</summary>
    public double BrokenShare { get; init; } = 0.15;

    /// <summary>E3, spall behind a plate that held (damage/14): a heavy shell (the plate under SpallRoofTD of its
    /// calibre) glancing off a turret roof drives it in with this chance (Dunkerque, one case [EST]); charges in a cloud's
    /// way (a gunhouse, a working chamber, a handling room, a magazine) light with SpallFlashChance at severity 0.5 and up
    /// (14 §6.3 [EST]; in proportion below, the ramp the code's), a flash source for the magazine chain as E2's pieces
    /// are (MagazineFlashChance in a magazine, TurretFlashChance in a turret or handling room); a boundary of SpallStopMm or more stops
    /// the flakes unless THOR lets them through (14 §4.3). What a flake or the big piece must carry to harm a part, J, by
    /// its kind (as PartVital's list), and a space's vitals (pipes, cables, gauges) [EST, 14 §5.2: instrument or sight
    /// ~100 J, cable or pipe ~500 J, a machine's casing ~5 kJ].</summary>
    public double SpallRoofTD { get; init; } = 0.4;
    public double SpallRoofChance { get; init; } = 0.3;
    public double SpallFlashChance { get; init; } = 0.2;
    public double SpallStopMm { get; init; } = 5;
    public double[] PartFragilityJ { get; init; } = [100, 100, 500, 100, 5000, 100, 100, 5000, 5000, 5000, 5000, 500, 5000, 500, 5000, 5000, 500, 500, 500];
    public double RoomFragilityJ { get; init; } = 500;

    /// <summary>Fire: a burst's chance by the shell's kind (AP, common, HE), times the fire load where it bursts (by the
    /// look's era: Victorian, Great War, treaty, WWII, Cold War; and in superstructure, machinery or the rest of the
    /// hull), times the burst's size (0.5 + 0.5 times the cube root of W over 10 kg, at most 1.5), and this share when
    /// it bursts outside; fragments reaching a mount's ready ammunition light it with this chance.</summary>
    public double[] FireChance { get; init; } = [0.15, 0.30, 0.45];
    public double[] FireLoadByEra { get; init; } = [1.6, 1.2, 1, 1, 1];
    public double FireLoadSuperstructure { get; init; } = 1.3;
    public double FireLoadMachinery { get; init; } = 0.5;
    public double FireLoadHull { get; init; } = 0.8;
    public double FireOutsideShare { get; init; } = 0.4;
    public double ReadyAmmoFireChance { get; init; } = 0.3;

    /// <summary>What a turret or barbette that held does (docs/battle/shells-armour-damage.md E4; damage/13 §1.4
    /// <c>turret_nonpen</c>): by where it struck (rows: exposed barbette near the ring, barbette at or under the deck,
    /// face, side and rear, roof), the chances of a jam in train, stiff, one gun out, sights out and a stun with nothing
    /// lasting [FIT], times size(I) = min(<see cref="NonPenSizeMax"/>, (I / <see cref="NonPenImpulseRef"/>)^0.5) and
    /// margin(r) = clamp(1.3 − 0.3·r, 0.4, 1), r the plate over what the shell beats there (the stun only by half of
    /// them); a nose-fuzed shell bursting on it by <see cref="TurretBurstK"/>; a barbette under <see cref="ThinTurretMm"/> by
    /// <see cref="ThinBarbetteK"/> (Savo, Boise).</summary>
    public double NonPenImpulseRef { get; init; } = 4e5;
    public double NonPenSizeMax { get; init; } = 1.15;
    public double[] TurretNonPen { get; init; } =
    [
        0.38, 0.06, 0.05, 0.03, 0.10,
        0.22, 0.10, 0.03, 0.00, 0.05,
        0.14, 0.04, 0.33, 0.08, 0.15,
        0.12, 0.04, 0.12, 0.12, 0.08,
        0.08, 0.02, 0.22, 0.30, 0.15,
    ];
    public double[] TurretBurstK { get; init; } = [0.6, 0.8, 0.8, 1.5, 1.2];
    public double ThinTurretMm { get; init; } = 152;
    public double ThinBarbetteK { get; init; } = 1.5;

    /// <summary>A jam's cause by where it struck (rows as <see cref="TurretNonPen"/>'s: debris, deck, ring shares), and
    /// how long each lasts, minutes, log-uniform: debris cleared by the crew, deck or glacis cut free; the ring
    /// distorted is for the battle (13 §1.3: 44% of jams last it). Stiff trains at <see cref="StiffTrain"/> of its
    /// rate, for the battle with <see cref="StiffBattleShare"/>, else <see cref="StiffMin"/>; a gun out likewise; a stun
    /// <see cref="StunS"/> seconds.</summary>
    public double[] JamCause { get; init; } =
    [
        0.45, 0.10, 0.45,
        0.20, 0.55, 0.25,
        0.35, 0.05, 0.60,
        0.50, 0.00, 0.50,
        0.60, 0.00, 0.40,
    ];
    public double[] JamDebrisMin { get; init; } = [3, 40];
    public double[] JamDeckMin { get; init; } = [60, 600];
    public double StiffTrain { get; init; } = 0.4;
    public double StiffBattleShare { get; init; } = 0.5;
    public double[] StiffMin { get; init; } = [20, 180];
    public double GunOutBattleShare { get; init; } = 0.5;
    public double[] GunOutMin { get; init; } = [30, 240];
    public double[] StunS { get; init; } = [10, 120];

    /// <summary>The crew stunned by a heavy hit that held, with a lasting outcome on a turret (a stun with nothing
    /// lasting is the table's own), or on a conning tower (13 §3.4 [FIT]), times the shock's (I / NonPenImpulseRef)^0.7 to
    /// at most <see cref="ShockSizeMax"/>: out <see cref="StunS"/> seconds, and with <see cref="KeyManShare"/> a key man
    /// out <see cref="KeyManMin"/> minutes [EST: "5+ min"].</summary>
    public double ShockSizeMax { get; init; } = 1.3;
    public double TurretStunChance { get; init; } = 0.6;
    public double TowerStunChance { get; init; } = 0.7;
    public double KeyManShare { get; init; } = 0.1;
    public double[] KeyManMin { get; init; } = [5, 30];

    /// <summary>A burst in a magazine, or in a turret or its barbette (the handling path to it), sets off the magazine
    /// with these chances, times the side's propellant (1 by default; <c>DamageSystem.SetPropellant</c>). v0: the ship is
    /// lost at once and goes under <see cref="MagazineLossMs"/> later (the venting race is fighting.md 4.5's).</summary>
    public double MagazineFlashChance { get; init; } = 0.25;
    public double TurretFlashChance { get; init; } = 0.25;
    public int MagazineLossMs { get; init; } = 60_000;

    /// <summary>Casualties (docs/battle/shells-armour-damage.md E6; damage/16): men, not area. Each group of men in reach
    /// (a cell's or a part's battle crew, at its points) takes the burst's pieces that still carry
    /// <see cref="ManStopJ"/> there (E1's sprays, through the plate between) on a man's <see cref="ManAreaM2"/>, times the
    /// people's flux scale <see cref="CasualtyS"/> (13's free field is 1, and stays 1 for parts: ricochets, debris and
    /// small pieces inside a steel ship [FIT, 16 §4.2]); a burst on a plate throws <see cref="ContactShare"/> of them its
    /// way. A share of the men by where they are (<see cref="CoverRoom"/> by room kind, <see cref="CoverPart"/> by
    /// component kind, <see cref="CoverMount"/> for a gunhouse, a casemate and an open mount) sees the burst; the rest
    /// take <see cref="ShieldedShare"/> of its flux [EST, 16 §4.1]. P(casualty) = 1 − e^(−λ), P(killed) = 1 − e^(−qλ),
    /// q <see cref="CasualtyQ"/> [FIT].</summary>
    public double CasualtyS { get; init; } = 8.7;
    public double CasualtyQ { get; init; } = 0.08;
    public double ManStopJ { get; init; } = 80;
    public double ManAreaM2 { get; init; } = 0.5;
    public double ContactShare { get; init; } = 0.5;
    public double ShieldedShare { get; init; } = 0.1;
    /// <summary>The share of the men who see the burst, by room kind (Stores ... Hangar): flats and stores 0.6, magazines
    /// and handling rooms 0.45, machinery and the steering gear 0.3 (16 §4.1 [EST]).</summary>
    public double[] CoverRoom { get; init; } = [0.6, 0.6, 0.6, 0.45, 0.3, 0.3, 0.3, 0.3, 0.6, 0.6, 0.6, 0.6, 0.6, 0.45, 0.6];
    /// <summary>... by component kind (Main ... HangarBay; a mount's own by <see cref="CoverMount"/>): a station (a bridge,
    /// a director, the conning tower) 0.7, a barbette's handling crew 0.45, the open (AA, tubes, masts, flight deck) 0.8.</summary>
    public double[] CoverPart { get; init; } = [0.45, 0.45, 0.8, 0.8, 0.45, 0.7, 0.7, 0.8, 0.8, 0.6, 0.6, 0.8, 0.3, 0.3, 0.3, 0.3, 0.8, 0.8, 0.6];
    /// <summary>... a mount's: an enclosed gunhouse (breeches, cradles, rammers), a casemate, an open or shielded mount.</summary>
    public double[] CoverMount { get; init; } = [0.45, 0.6, 0.8];
    /// <summary>Blast on people (16 §4.5; 11): in a closed space by the pressure its boundaries felt (E5), killed 50% at
    /// <see cref="BlastKillMPa"/> (Bowen; lognormal σ <see cref="BlastKillSigma"/>), out of action 50% at
    /// <see cref="BlastHurtMPa"/> (σ <see cref="BlastHurtSigma"/>); in the open killed within <see cref="OpenKillK"/>·W^⅓,
    /// a share <see cref="OpenHurtShare"/> hurt out to <see cref="OpenHurtK"/>·W^⅓ (W the filler's TNT kg).</summary>
    public double BlastKillMPa { get; init; } = 0.42;
    public double BlastKillSigma { get; init; } = 0.174;
    public double BlastHurtMPa { get; init; } = 0.12;
    public double BlastHurtSigma { get; init; } = 0.5;
    public double OpenKillK { get; init; } = 1.3;
    public double OpenHurtK { get; init; } = 3.2;
    public double OpenHurtShare { get; init; } = 0.5;
    /// <summary>The shell's own way (16 §4.3): the men in its lane (a calibre across and <see cref="LaneManM"/> either side,
    /// at a man's height) are killed; each plate it gets through throws the debris 14 §3.1 measured (TNO: 0.9 of the hole,
    /// in the spall cloud's geometry and flakes, in place of 16's disc [EST]), striking as spall does. Spall and debris
    /// strike people by 14 §5.1's flux, no flux scale, q <see cref="DebrisQ"/> (the pieces are heavy: 16's q_sec
    /// [EST]).</summary>
    public double LaneManM { get; init; } = 0.25;
    public double DebrisQ { get; init; } = 0.35;
    /// <summary>Propellant flash (16 §4.6 [FIT]): the killed and wounded shares of the men it reaches, a turret's train
    /// (Derfflinger's C, Seydlitz's after turrets) and an open casemate battery (Malaya's).</summary>
    public double[] FlashTurret { get; init; } = [0.92, 0.06];
    public double[] FlashBattery { get; init; } = [0.54, 0.32];
    /// <summary>The wounded (16 §4.8 [EST]): this share out for the action, the rest back at their station after first aid,
    /// in [min, max] minutes.</summary>
    public double WoundedOutShare { get; init; } = 0.45;
    public double[] WoundedBackMin { get; init; } = [15, 45];

    /// <summary>Near misses (docs/battle/shells-armour-damage.md E7; damage/15): a shell that misses bursts in the water,
    /// and the ships whose waterplane lies within max(<see cref="NearMissSplinterK"/>·W^⅓, <see cref="NearMissShockK"/>·√W)
    /// m of it (W the filler's TNT kg) take a near miss: 15·W^⅓ is where its splinters stop hitting exposed men (15 §6.1),
    /// 7.5·√W where its shock falls under 0.03. A shell falling flatter than <see cref="WaterRicochetDeg"/> may skip off
    /// the water (18°/√SG for a steel sphere [SRC], shells alike [EST]): a base fuze then doesn't burst (v0), a nose fuze
    /// bursts on the graze. Fragments in water slow 1/e every <see cref="WaterDragL1"/>·m^⅓ m (13 §2.3). Splinters through
    /// the side within <see cref="NearMissBandM"/> of the waterline hole it, each <see cref="SplinterHoleK"/> times the
    /// fragment's presented area (15 §6.3 [EST]).</summary>
    public double NearMissSplinterK { get; init; } = 15;
    public double NearMissShockK { get; init; } = 7.5;
    public double WaterRicochetDeg { get; init; } = 6.4;
    public double WaterDragL1 { get; init; } = 0.3;
    public double NearMissBandM { get; init; } = 1;
    public double SplinterHoleK { get; init; } = 1.3;
    /// <summary>A near miss's shock (E7; damage/15 §3, §6.4): the near-miss shock factor NSF = √W/R·(1 + sin a)/2·
    /// √(1 − e^(−2Δt/θ)) (lb, ft; Cole's θ, the surface's reflection cutting the pulse off) at the nearest wetted point,
    /// at least <see cref="ShockWetM"/> down [EST]. Its bands (<see cref="NearShockBands"/>, fitted to the British bomb
    /// near misses of 1939-41 [FIT]): 0.03 fragile gear (breakers trip: a section's power and the directors' gyros, for
    /// <see cref="NearTripMin"/>), 0.08 machinery mounts and castings (a machinery space's gear damaged; the whole ship's
    /// breakers), 0.2 the hull's seams (shafts strained), 0.45 plating buckled (shafts and thrusts out of line, the
    /// machinery again), 1.0 plating split. Each happens with a chance rising across its band from 0 to 1 [EST]. The
    /// gear's edges are times the ship's shock grade's <see cref="NearShockGradeK"/> (G0 the record's cast-iron fittings,
    /// G1 shock-hardened: 15 §6.5's ×2 [EST]), the structure's not. The seams leak by r(D) (damage/17 §3.1: D = 5·NSF,
    /// times <see cref="ShockPlateMm"/> over the plate: the bands are a destroyer's bottom's [EST]), over the shell and
    /// the transverse bulkheads where D is past the weep; at D 5 the nearest plate is holed as a contact burst's. A burst
    /// within <see cref="SternShare"/> of the length from the stern shakes the rudders, shafts and propellers there by
    /// their own NSF, as the structure.</summary>
    public double ShockWetM { get; init; } = 0.5;
    public double[] NearShockBands { get; init; } = [0.03, 0.08, 0.2, 0.45, 1.0];
    public double[] NearShockGradeK { get; init; } = [1, 2, 3, 4];
    public double[] NearTripMin { get; init; } = [1, 1, 5];
    public double ShockPlateMm { get; init; } = 9;
    public double SternShare { get; init; } = 0.15;

    /// <summary>Steering lost at the steering gear or the rudder jams the rudder where it is with this chance (a ship
    /// that circles); else it trails amidships, the helm gone.</summary>
    public double RudderJamChance { get; init; } = 0.5;

    // ------------------------------------------------------------------ fire, flooding, going down (fighting.md 4.3b)
    // damage-model-research.md §3.5, §3.6, §8.4, §8.5, §11: v0, deliberately simple (Flooding, Fires)

    /// <summary>A wet or burning ship's flooding and fire step, ms, on a grid of world time; once its water has settled
    /// (it changed by under <see cref="FloodIdleT"/> in a step) and nothing burns, every <see cref="FloodIdleMs"/>.</summary>
    public int FloodMs { get; init; } = 1000;
    public int FloodIdleMs { get; init; } = 30_000;
    public double FloodIdleT { get; init; } = 0.05;

    /// <summary>The discharge coefficient of every opening water comes through (Cd·A·√(2g·head)).</summary>
    public double FloodCd { get; init; } = 0.6;

    /// <summary>The openings between neighbouring cells, m²: within a room (open), through an intact bulkhead or deck
    /// (glands, pipes, doors: the leaks), through one a burst tore, and through a deck under the sea outside (the Lützow
    /// rule: the spaces over it are no longer tight). A cell open to the weather takes the sea through this share of its
    /// deck once the deck is under (hatches, vents, skylights).</summary>
    public double FloodOpenM2 { get; init; } = 1;
    public double FloodLeakBulkheadM2 { get; init; } = 0.0005;
    public double FloodLeakDeckM2 { get; init; } = 0.0005;
    public double FloodTornM2 { get; init; } = 0.05;
    public double FloodSunkDeckM2 { get; init; } = 0.3;
    public double FloodWeatherShare { get; init; } = 0.01;

    /// <summary>A room is out of action once its cells hold this share of their capacity (machinery stops, a magazine
    /// can't serve its guns).</summary>
    public double RoomFloodedShare { get; init; } = 0.5;

    /// <summary>The pumps, tonnes an hour for each 1,000 t of displacement, at full power: they clear the cells with no
    /// hole of their own (the leaks, not the holes). Counter-flooding is fighting.md 4.5's.</summary>
    public double PumpTPerHourPer1000T { get; init; } = 50;

    /// <summary>A cell's free surface counts in full once its water is this share from dry and from full, less nearer
    /// either (the water can't run across as the ship heels).</summary>
    public double FreeSurfacePocket { get; init; } = 0.1;

    /// <summary>The heel comes on toward what the water gives with this time constant, s (a ship lolls over its rolls,
    /// not at once).</summary>
    public double HeelLagS { get; init; } = 30;

    /// <summary>A ship capsizes past this heel, degrees (damage-model-research.md §8.4: 20-30°).</summary>
    public double CapsizeHeelDeg { get; init; } = 25;

    /// <summary>How long a ship takes to go under once it founders or capsizes, ms (a magazine:
    /// <see cref="MagazineLossMs"/>).</summary>
    public int FounderMs { get; init; } = 600_000;
    public int CapsizeMs { get; init; } = 180_000;

    /// <summary>Fire, chances a minute: a burning space or part damaged (each room in it once more), a burning space
    /// spreading to each neighbour, put out by its crew (times the share of the crew left); a magazine in a burning space,
    /// or next to one, set off (times the side's propellant). A fire costs this many crew a minute; a space this share
    /// flooded doesn't burn.</summary>
    public double FireHarmPerMin { get; init; } = 0.2;
    public double FireSpreadPerMin { get; init; } = 0.02;
    public double FireOutPerMin { get; init; } = 0.15;
    public double FireMagazineInPerMin { get; init; } = 0.05;
    public double FireMagazineNextPerMin { get; init; } = 0.01;
    public double FireCrewPerMin { get; init; } = 0.3;
    public double FireDrownShare { get; init; } = 0.5;

    /// <summary>What an observer makes out of a ship (a contact's <c>Seen</c>): low in the water (settled by this share of
    /// its freeboard), a list, a trim (degrees), stopped (knots).</summary>
    public double SeenLowShare { get; init; } = 0.3;
    public double SeenListDeg { get; init; } = 3;
    public double SeenTrimDeg { get; init; } = 1;
    public double SeenStoppedKn { get; init; } = 1;

    // ------------------------------------------------------------------ torpedoes (docs/battle/fighting.md 4.6)

    /// <summary>Torpedoes running at once (the pool grows past it), and the ring of their ends the screens read.</summary>
    public int TorpedoCapacity { get; init; } = 256;
    public int TorpedoEndCapacity { get; init; } = 256;

    /// <summary>A torpedo's run is checked against the ships near it in pieces this long, ms (each a swept segment, so a
    /// hit doesn't depend on it); it can't hit in its first metres (arming).</summary>
    public int TorpedoCheckMs { get; init; } = 2000;
    public double TorpedoArmM { get; init; } = 200;

    /// <summary>Aiming a spread: a fixed launcher fires within this many degrees of its own bearing (its gyro angle; by
    /// era), each torpedo's own course error (1 sigma, degrees), the spacing between torpedoes as a share of the target's
    /// length at the meeting point, and the depth set as a share of the target's draught as it's taken (within the two
    /// depths, m).</summary>
    public double[] TorpedoGyroDeg { get; init; } = [5, 20, 90, 90, 90];
    public double TorpedoCourseSigmaDeg { get; init; } = 1;
    public double TorpedoSpacingShare { get; init; } = 0.6;
    public double TorpedoDepthShare { get; init; } = 0.6;
    public double TorpedoDepthMinM { get; init; } = 1.5;
    public double TorpedoDepthMaxM { get; init; } = 4.5;

    /// <summary>By era: the chance a torpedo fails on its run (it sinks somewhere along it), and that one that strikes
    /// doesn't go off.</summary>
    public double[] TorpedoFail { get; init; } = [0.25, 0.12, 0.1, 0.08, 0.05];
    public double[] TorpedoDud { get; init; } = [0.15, 0.1, 0.08, 0.1, 0.05];

    /// <summary>The explosion at the hull (damage-model-research.md §3.4, A6): how far inboard it wrecks, m per kg^⅓ of its
    /// charge (TNT's worth; a 300 kg charge 4.7 m, the depth of a TDS rated for it), and the hole it tears in the side,
    /// m² per kg^⅔ (Seydlitz's 12 × 4 m from a ~200 kg charge). Each other cell within the reach takes a hole of its own
    /// of this share of it, less by its distance; machinery spaces within this many reaches may trip from the shock (the
    /// chance), a magazine within the reach may go (once a hit, times the side's propellant: torpedoes seldom set one
    /// off), and this share of the battle crew in the spaces it floods is lost.</summary>
    public double TorpedoReachK { get; init; } = 0.7;
    public double TorpedoHoleK { get; init; } = 1.4;
    public double TorpedoInnerHoleShare { get; init; } = 0.5;
    public double TorpedoShockShare { get; init; } = 2;
    public double TorpedoShockChance { get; init; } = 0.5;
    public double TorpedoMagazineChance { get; init; } = 0.05;
    public double TorpedoCasualtyShare { get; init; } = 0.5;

    /// <summary>The hull girder (damage-model-research.md §1.6, 03-small-vessels: small ships die by broken backs): an
    /// underwater explosion breaks a ship whose depth (keel to main deck) is small beside the explosion's height (twice its
    /// reach): never when the height is under the first share of the depth, always over the second, and between them by
    /// the share; times 1 over the middle half of its length, falling to none at the ends. A ship broken in two goes
    /// under in <see cref="BrokenMs"/>.</summary>
    public double[] TorpedoBreakShare { get; init; } = [0.7, 1.0];
    public int BrokenMs { get; init; } = 300_000;

    /// <summary>The captains' torpedoes (CaptainLayer.Fire.cs): at will when the run for the fast setting is within this
    /// share of its range, so many a firing, then this long before the next; a torpedo craft's attack closes to this
    /// share of the fast setting's range, and turns away for this long after firing.</summary>
    public double TorpedoFireShare { get; init; } = 0.9;
    public int TorpedoSpread { get; init; } = 2;
    public int TorpedoRefireMs { get; init; } = 60_000;
    public double TorpedoAttackShare { get; init; } = 0.7;
    public int TorpedoTurnAwayMs { get; init; } = 180_000;

    // ------------------------------------------------------------------ the matchup (docs/battle/command.md 5.1g)
    // Who can fight whom, judged analytically from the classes (Sim.Combat/Matchup.cs): fitted to the fine model's own
    // benches (shipgen duel, hits, torpedo), 2026-10-14.

    /// <summary>A gun's hit chance (fire-control-research.md §8.3's quick formula): the fire control's error by level
    /// (0 eyeball .. 8 digital; local control counts as 0), 1 sigma, in range as % of the range and across in mils (§12.2,
    /// salvos 4-8), with the gun's own dispersion; the target's height over the water for the danger space, as a share of
    /// its freeboard; the muzzle's height, m. Matches shipgen duel's 15-minute hit rates within about 1.5x.</summary>
    public double[] MatchupMpiPct { get; init; } = [10.9, 4.9, 3.7, 2.9, 2.3, 2.0, 1.8, 1.3, 0.9];
    public double[] MatchupDeflectionMil { get; init; } = [9.8, 5.4, 5.4, 4.9, 3.8, 2.8, 3.1, 3.1, 2.0];
    public double MatchupHeightShare { get; init; } = 1.3;
    public double MatchupMuzzleM { get; init; } = 10;

    /// <summary>What a hit does: the chance it puts the ship out of the fight (lost, or past doctrine's fall-out) is
    /// 1 - exp(-K x size x share / displacement), tonnes: MatchupShellK with a shell's size the square root of its mass in
    /// kg (shipgen hits: an 88 mm and a 6" shell on a destroyer, 3.4% and 7.2%, go as that, not as their filler), the share 1 when it
    /// bursts inside, MatchupKeptShare when the armour keeps it out (or a nose fuze bursts it on the plate),
    /// MatchupPassShare when it passes through plating too thin to arm its fuze; MatchupTorpedoK by a torpedo's charge in kg.
    /// Penetration is a probability around the plate, spread by MatchupPenSpread of it (1 sigma). A side hit in the
    /// upperworks reaches the vitals by its splinters only, exp(-deck armour / the shell's splinter plate) of it (the
    /// armour its best 10% beat 5 m off: Fragments.SplinterMm, E1). Fitted to shipgen
    /// hits and shipgen torpedo with 30 minutes of flooding after (a 2,300 t destroyer 7% a 6" HE hit, Iron Duke 0.85% a
    /// 12" AP hit at 14 km, a battleship 18% a torpedo).</summary>
    public double MatchupShellK { get; init; } = 28;
    public double MatchupKeptShare { get; init; } = 0.15;
    public double MatchupPassShare { get; init; } = 0.4;
    public double MatchupTorpedoK { get; init; } = 48;
    public double MatchupPenSpread { get; init; } = 0.1;
    /// <summary>E3: a shell its plate holds within the spall band adds this share of a burst inside to what a kept one
    /// does: the cloud behind a turret face or a roof, rarely a flash [EST; the balance pass fits it].</summary>
    public double MatchupSpallShare { get; init; } = 0.05;
    /// <summary>A shell through armour does what its fuze and filler give it (<see cref="LowOrderShare"/>): a full burst
    /// all of it, a burst in the plate and a low-order burst these shares, a blind fuze or a dud as one kept out (a
    /// broken shell too, the plate's doing).</summary>
    public double MatchupInPlateShare { get; init; } = 0.5;
    public double MatchupLowOrderShare { get; init; } = 0.3;

    /// <summary>A torpedo's hit chance, fired at a range: MatchupTorpedoHitM / range, at most MatchupTorpedoHitMax, by the
    /// square root of the target's length over MatchupTorpedoLengthM, times MatchupTorpedoEvadedShare for her combing and
    /// turning away, and by how much slower than the torpedo she is beside a target at MatchupTorpedoRefKn (a fast ship
    /// turning away outruns its reach): shipgen torpedo against a steady target at 18 kn, 35% at 3 km, 18% at 5 km.</summary>
    public double MatchupTorpedoHitM { get; init; } = 900;
    public double MatchupTorpedoHitMax { get; init; } = 0.4;
    public double MatchupTorpedoLengthM { get; init; } = 150;
    public double MatchupTorpedoEvadedShare { get; init; } = 0.6;
    public double MatchupTorpedoRefKn { get; init; } = 18;

    /// <summary>The engagement judged: a gunfight lasts MatchupFightMin minutes; a torpedo attack the run in from the
    /// defender's reach to the firing range and out again, plus MatchupTorpedoFireMin. A side is faster than the other
    /// (and so picks the range) by more than MatchupSpeedMarginKn. A side's share lost is never taken under MatchupFloor,
    /// so a ratio of two small numbers stays finite. Minutes, knots, a share.</summary>
    public double MatchupFightMin { get; init; } = 30;
    /// <summary>A ship is built to make torpedo attacks with this many tubes bearing a thousand tonnes or more.</summary>
    public double MatchupAttackTubesPerKt { get; init; } = 0.5;
    public double MatchupTorpedoFireMin { get; init; } = 2;
    /// <summary>Attackers running in and out zigzag: the defence's fire on them then hits this share as often
    /// (docs/battle/command.md 5.1a: a light cruiser running zigzag from a battleship at 14 km took no hits in four
    /// seeds, four steady; the fine model's flotilla loses about half its boats over a whole run).</summary>
    public double MatchupRunInHitShare { get; init; } = 0.4;
    public double MatchupSpeedMarginKn { get; init; } = 2;
    public double MatchupFloor { get; init; } = 0.001;

    /// <summary>An enemy's toughness left as it's seen: low in the water, listing, trimmed by the bow or the stern, burning
    /// (each a share, multiplied). Silent, its guns count for nothing; stopped, it can't get away.</summary>
    public double MatchupSeenLowShare { get; init; } = 0.5;
    public double MatchupSeenListingShare { get; init; } = 0.6;
    public double MatchupSeenTrimShare { get; init; } = 0.8;
    public double MatchupSeenBurningShare { get; init; } = 0.9;

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

/// <summary>How good a side's gun crews are (docs/battle/fighting.md 4.2b: one per side, a scenario's to give).</summary>
public enum CrewQuality : byte
{
    Green,
    Average,
    Trained,
    Elite,
}

/// <summary>A crew quality's multipliers on the rangetakers' and the layers' errors.</summary>
public sealed record GunneryCrew
{
    public string Name { get; init; } = "";
    public double Operator { get; init; }
    public double Lay { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(SimSettings))]
internal partial class SimSettingsJson : JsonSerializerContext;
