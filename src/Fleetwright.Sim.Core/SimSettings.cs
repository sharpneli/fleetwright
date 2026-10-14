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

    /// <summary>Opposing ships within this of each other are in the fine tier, metres: beyond the longest sighting
    /// range, so the fine tier owns everything that could be seen. Ships on rails come off at the moment it's crossed.</summary>
    public double EncounterRangeM { get; init; } = 50_000;

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

    // ------------------------------------------------------------------ shells (docs/battle/fighting.md 4.2a)

    /// <summary>The top of a shell's danger window, metres over the sea: nothing at sea is taller, so only the flight
    /// below it can hit a ship (docs/battle/plan.md "Shells").</summary>
    public double ShellWindowTopM { get; init; } = 60;

    /// <summary>How far a shell's track is followed on under the water, metres along it, for the hit query (water is a
    /// crossing, not a stop: the damage model decides what the shell does there).</summary>
    public double ShellUnderwaterM { get; init; } = 10;

    /// <summary>The shells in flight the world starts with room for (it grows past it), and the splashes it keeps for
    /// the screens: how many, and for how long, ms.</summary>
    public int ShellCapacity { get; init; } = 512;
    public int SplashCapacity { get; init; } = 4096;
    public int SplashLifeMs { get; init; } = 20_000;

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

    /// <summary>Rangefinding (fire-control-research.md §3.2): one unit of error δ at the eye, arcseconds (OP 1171), so a
    /// reading's error is δ·R²/(B·M); bases longer than <see cref="RangefinderLongBaseM"/> give less than geometry says
    /// (as the 0.75 power past it, NDRC 1941); an instrument's own bias after calibration, in units of error; its
    /// bearing's error, mil.</summary>
    public double RangefinderUnitArcsec { get; init; } = 12;
    public double RangefinderLongBaseM { get; init; } = 5;
    public double RangefinderBiasUnits { get; init; } = 1.5;
    public double RangefinderBearingMil { get; init; } = 1;

    /// <summary>A range by eye: its error and its bias as shares of the range, its bearing's error, mil.</summary>
    public double EyeRangeShare { get; init; } = 0.20;
    public double EyeRangeBiasShare { get; init; } = 0.10;
    public double EyeBearingMil { get; init; } = 5;

    /// <summary>Own ship counts as turning (its salvos take the level's own-turn kick) past this rate of yaw, °/s.</summary>
    public double OwnTurnYawDegS { get; init; } = 0.2;

    /// <summary>The fire-control levels, a row for each <c>FireControlLevel</c> (0-8) in order: fire_control_ref.py
    /// LEVELS (0, 1, 2, 3, 4, 5, 6, 7, 8 of its rows; their trackers, radars and stabilisation are fighting.md 4.5's).
    /// Local control (a battery without a director) fires at row 0.</summary>
    public GunneryLevel[] GunneryLevels { get; init; } =
    [
        new() { Name = "0 eyeball", ReadingsPerMin = 1, Magnification = 0, LatencyS = 20, Q = 0.02, PriorKn = 5, PriorDeg = 30, ResidPct = 2.5,
                DeflBiasMil = 4, LayMil = 3, Director = false, OwnTurnKickM = 0, Ladder0M = 550, SalvoPct = 2.5, SalvoDeflMil = 3 },
        new() { Name = "1 early rangefinder", ReadingsPerMin = 2, Magnification = 24, LatencyS = 15, Q = 0.02, PriorKn = 4, PriorDeg = 25,
                ResidPct = 2, DeflBiasMil = 3, LayMil = 2.5, Director = false, OwnTurnKickM = 0, Ladder0M = 366, SalvoPct = 2, SalvoDeflMil = 2.5 },
        new() { Name = "2 Dumaresq and clock", ReadingsPerMin = 2, Magnification = 28, LatencyS = 15, Q = 0.03, PriorKn = 3, PriorDeg = 20,
                ResidPct = 1.5, DeflBiasMil = 2.5, LayMil = 2, Director = false, OwnTurnKickM = 0, Ladder0M = 366, SalvoPct = 1.8, SalvoDeflMil = 2.5 },
        new() { Name = "3 Dreyer and director", ReadingsPerMin = 2, Magnification = 28, LatencyS = 15, Q = 0.03, PriorKn = 2.5, PriorDeg = 15,
                ResidPct = 1.2, DeflBiasMil = 2, LayMil = 1, Director = true, OwnTurnKickM = 300, Ladder0M = 366, SalvoPct = 1.5, SalvoDeflMil = 2 },
        new() { Name = "4 interwar table", ReadingsPerMin = 4, Magnification = 28, LatencyS = 8, Q = 0.03, PriorKn = 2, PriorDeg = 12,
                ResidPct = 0.6, DeflBiasMil = 1.5, LayMil = 0.8, Director = true, OwnTurnKickM = 250, Ladder0M = 366, SalvoPct = 1.2, SalvoDeflMil = 1.5 },
        new() { Name = "5 rangekeeper", ReadingsPerMin = 6, Magnification = 25, LatencyS = 3, Q = 0.05, PriorKn = 2, PriorDeg = 12,
                ResidPct = 0.4, DeflBiasMil = 1, LayMil = 0.5, Director = true, OwnTurnKickM = 150, Ladder0M = 366, SalvoPct = 1, SalvoDeflMil = 1.2 },
        new() { Name = "6 radar spotting", ReadingsPerMin = 6, Magnification = 25, LatencyS = 2, Q = 0.05, PriorKn = 2, PriorDeg = 12,
                ResidPct = 0.4, DeflBiasMil = 1, LayMil = 0.5, Director = true, OwnTurnKickM = 150, Ladder0M = 366, SalvoPct = 1, SalvoDeflMil = 1.2 },
        new() { Name = "7 auto-track", ReadingsPerMin = 60, Magnification = 25, LatencyS = 1, Q = 0.02, PriorKn = 2, PriorDeg = 12,
                ResidPct = 0.4, DeflBiasMil = 0.8, LayMil = 0.4, Director = true, OwnTurnKickM = 50, Ladder0M = 366, SalvoPct = 0.7, SalvoDeflMil = 0.8 },
        new() { Name = "8 digital", ReadingsPerMin = 30, Magnification = 25, LatencyS = 0.5, Q = 0.01, PriorKn = 2, PriorDeg = 12,
                ResidPct = 0.3, DeflBiasMil = 0.5, LayMil = 0.3, Director = true, OwnTurnKickM = 20, Ladder0M = 366, SalvoPct = 0.4, SalvoDeflMil = 0.5 },
    ];

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
    /// (a share of it); the angle off the line of sight he closes or opens at, degrees; the radius his navy's guns and the
    /// enemy's are compared in, m; and the share of the outgunned ratio under which he turns back. The range and the ratio
    /// are doctrine's (docs/battle/command.md "Doctrine": RangeShare, OutgunnedRatio).</summary>
    public double FightBandShare { get; init; } = 0.15;
    public double FightCloseDeg { get; init; } = 40;
    public double FightCompareM { get; init; } = 30_000;
    public double FightRallyShare { get; init; } = 0.75;

    /// <summary>A clear line of fire (docs/battle/command.md 5.1a): a gun's is foul while a friend is within FireFoulM of
    /// where its shells come down (from FireFoulBeforeM short of the target to FireFoulM past it), a torpedo's while one
    /// is within TorpedoFoulM of its track; neither counts the first FireFoulNearM off the shooter. m; the grid's cube.</summary>
    public double FireFoulM { get; init; } = 400;

    /// <summary>A torpedo craft holds steady (stops evading) within this share of her attack range, to fire
    /// (docs/battle/command.md 5.1a; the evasion research's 30 s before the launch).</summary>
    public double EvadeSteadyShare { get; init; } = 1.25;
    public double FireFoulBeforeM { get; init; } = 2_000;
    public double FireFoulNearM { get; init; } = 300;
    public double TorpedoFoulM { get; init; } = 400;
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

    /// <summary>A contact not yet made out to its class counts for this many heavy guns (12" guns' worth: guns ×
    /// (calibre / 305 mm)³), by <c>ShipType</c> (unknown, capital, cruiser, destroyer, small, merchant).</summary>
    public double[] FightTypeGuns { get; init; } = [1, 8, 1, 0.1, 0, 0];

    /// <summary>The salvos the world keeps in flight for spotting (it grows past it), and the combat log's ring.</summary>
    public int SalvoCapacity { get; init; } = 256;
    public int CombatLogCapacity { get; init; } = 4096;

    // ------------------------------------------------------------------ damage (docs/battle/fighting.md 4.3a)
    // research/simulation/damage-research/gunfire_ref.py and 08-gunfire-effects.md: the [INFERRED] numbers are its
    // tuning values, fitted to the war-damage cases it names.

    /// <summary>Whether hits do anything (the damage system's). Off, they're counted and logged only, as before 4.3a: a
    /// bench calibrating fire control on its own (the duel, the gunnery tests) turns it off.</summary>
    public bool HitsDoDamage { get; init; } = true;

    /// <summary>What a shell that arms on armour it can defeat does, a row for each <c>ShellQuality</c> in order
    /// (gunfire_ref.py OUTCOME: rn1916, km1916, rn1918, ww2, ijn_t91).</summary>
    public ShellOutcomeRow[] ShellOutcomes { get; init; } =
    [
        new() { Name = "rn1916", Burst = 0.10, InPlate = 0.35, LowOrder = 0.10, Dud = 0.05, Broken = 0.40 },
        new() { Name = "km1916", Burst = 0.40, InPlate = 0.15, LowOrder = 0.10, Dud = 0.15, Broken = 0.20 },
        new() { Name = "rn1918", Burst = 0.55, InPlate = 0.10, LowOrder = 0.20, Dud = 0.10, Broken = 0.05 },
        new() { Name = "ww2", Burst = 0.75, InPlate = 0.05, LowOrder = 0.07, Dud = 0.08, Broken = 0.05 },
        new() { Name = "ijn_t91", Burst = 0.70, InPlate = 0.05, LowOrder = 0.07, Dud = 0.13, Broken = 0.05 },
    ];

    /// <summary>A plate's resistance as a share of the same thickness of Class A or Krupp cemented armour (the
    /// penetration fit's plate), by the material's name; a name not listed is <see cref="ArmourQualityOther"/>.</summary>
    public Dictionary<string, double> ArmourQuality { get; init; } = new()
    {
        ["Krupp cemented"] = 1, ["Krupp cemented (KC n/A)"] = 1, ["cemented armour"] = 1, ["Terni cemented"] = 1, ["Vickers cemented"] = 1,
        ["Witkowitz cemented"] = 1, ["Class A"] = 1, ["Class A armour"] = 1, ["Class B"] = 1, ["STS"] = 1, ["special steel"] = 0.95,
        ["non-cemented armour"] = 0.9, ["Vickers hardened"] = 0.9, ["Harvey nickel steel"] = 0.8, ["nickel steel"] = 0.75,
        ["high-tensile steel"] = 0.7, ["compound"] = 0.6, ["steel"] = 0.55, ["mild steel"] = 0.5, ["wrought iron"] = 0.45,
        ["aluminium alloy"] = 0.25, ["wood"] = 0.05,
    };
    public double ArmourQualityOther { get; init; } = 0.6;

    /// <summary>Penetration: a common shell's capability as a share of AP's; an HE shell's nose fuze punches this many
    /// calibres (times the square root of the obliquity's cosine) and bursts; each plate's resistance is drawn within
    /// plus or minus this share.</summary>
    public double CommonPenShare { get; init; } = 0.6;
    public double HePenCalibres { get; init; } = 0.2;
    public double PenSpread { get; init; } = 0.1;

    /// <summary>A base fuze arms on a plate of this many calibres at normal impact (Okun's Mk 21; less at obliquity).</summary>
    public double FuzeArmCalibres { get; init; } = 0.07;

    /// <summary>A shell that can't get through a plate glances off it from this obliquity, surely by this many degrees
    /// more, degrees.</summary>
    public double RicochetFromDeg { get; init; } = 45;
    public double RicochetSpanDeg { get; init; } = 20;

    /// <summary>The fuze's delay distance is the delay times the residual velocity, times a draw between these.</summary>
    public double DelayLow { get; init; } = 0.7;
    public double DelayHigh { get; init; } = 1.1;

    /// <summary>A shell crossing a machinery space (a boiler or engine room) is armed by what's in it (turbines, gears,
    /// boilers: Johnston) with this chance.</summary>
    public double MachineryArmChance { get; init; } = 0.45;

    /// <summary>A shell that met the water before the hull strikes at this share of its velocity (08 3.8).</summary>
    public double UnderwaterSpeedShare { get; init; } = 0.4;

    /// <summary>A filler's TNT equivalence (PLACEHOLDER until the designer gives fillers: lyddite and TNT are about 1).</summary>
    public double FillerTnt { get; init; } = 1;

    /// <summary>The burst's radii, m, times the cube root of its TNT kg: equipment wrecked and most crew hit; light
    /// structure and people injured; a contact burst's hole in plating, times the square root of 6 mm over the plate.</summary>
    public double WreckRadiusK { get; init; } = 1.6;
    public double BlastRadiusK { get; init; } = 3.5;
    public double HoleRadiusK { get; init; } = 0.45;

    /// <summary>A burst's force as a share of one inside: in the plate, outside on it, and low-order.</summary>
    public double BurstInPlateK { get; init; } = 0.4;
    public double BurstOutsideK { get; init; } = 0.15;
    public double LowOrderK { get; init; } = 0.45;

    /// <summary>A part within this share of the wreck radius is destroyed, within it disabled; a room is disabled by a
    /// burst inside it whose wreck radius is this share of its length (else damaged, and twice damaged is disabled).</summary>
    public double DestroyedShare { get; init; } = 0.5;
    public double RoomDisableShare { get; init; } = 0.4;

    /// <summary>A space's vitals as a share of its floor, by its kind (stores, accommodation, double bottom, magazine,
    /// bunker, boiler room, engine room, steering, TDS, torpedo room, fuel tank, cargo tank, hold): a burst inside it
    /// reaches them with this chance and its wreck area's share of the floor (times its force), a shell passing
    /// through with this chance; a burst's fragments through a bulkhead reach the next space's at
    /// <see cref="NeighbourShare"/> of that. Boilers and engines fill their rooms; propellant lies low in its magazine,
    /// in cases.</summary>
    public double[] RoomVital { get; init; } = [0.3, 0.3, 0.3, 0.15, 0.3, 0.5, 0.5, 0.6, 0.3, 0.3, 0.3, 0.3, 0.3];

    /// <summary>A boiler room keeps this share of its steam with every funnel it draws through wrecked (the draught
    /// lost; 08-gunfire-effects.md §8: funnel hits cost draught, not the boilers).</summary>
    public double DraughtFloor { get; init; } = 0.7;
    public double NeighbourShare { get; init; } = 0.5;

    /// <summary>Holes: a shell that passes through plating leaves a hole this many calibres across; plating holed up to
    /// this far over the waterline lets water in; a heavy shell (this calibre, mm, and up) defeated by a belt at the
    /// waterline opens its seams with this chance, a leak of this many square metres.</summary>
    public double ShellHoleCalibres { get; init; } = 1.5;
    public double HoleOverWaterlineM { get; init; } = 0.5;
    public double SeamLeakMinMm { get; init; } = 270;
    public double SeamLeakChance { get; init; } = 0.4;
    public double SeamLeakM2 { get; init; } = 0.002;

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

    /// <summary>A hit that doesn't get through a turret or barbette jams its mount (a conning tower: blinds it) with
    /// these chances by calibre: from <see cref="JamHeavyMm"/>, from <see cref="JamMediumMm"/>, and smaller (08 3.7, R5).</summary>
    public double JamHeavyMm { get; init; } = 270;
    public double JamMediumMm { get; init; } = 200;
    public double[] JamChance { get; init; } = [0.35, 0.05, 0.01];

    /// <summary>A burst in a magazine, or in a turret or its barbette (the handling path to it), sets off the magazine
    /// with these chances, times the side's propellant (1 by default; <c>DamageSystem.SetPropellant</c>). v0: the ship is
    /// lost at once and goes under <see cref="MagazineLossMs"/> later (the venting race is fighting.md 4.5's).</summary>
    public double MagazineFlashChance { get; init; } = 0.25;
    public double TurretFlashChance { get; init; } = 0.25;
    public int MagazineLossMs { get; init; } = 60_000;

    /// <summary>Casualties: people per square metre on open decks and in parts with no crew of their own; the share hit
    /// within the wreck radius and in the blast's ring; a shell passing without bursting hits a lane this wide, m, at
    /// this share.</summary>
    public double DeckCrewPerM2 { get; init; } = 0.01;
    public double CasualtyWreckShare { get; init; } = 0.8;
    public double CasualtyBlastShare { get; init; } = 0.2;
    public double PassLaneM { get; init; } = 1;
    public double PassCasualtyShare { get; init; } = 0.5;

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

/// <summary>A fire-control level's numbers (research/simulation/fire_control_ref.py FCLevel): rangefinder readings a
/// minute and the instruments' magnification; the computer's latency, s, and the target acceleration it allows (q,
/// m²/s³); the eye's first guess at the target's speed and course (1 sigma); the residual ballistic bias before spotting,
/// % of range, and in deflection, mil; the common laying error a salvo, mil; whether a director fires the guns (else
/// each gun lays itself and its error is dispersion); the extra error while own ship turns, m; the first ladder step,
/// m; the salvo-to-salvo error, % of range and mil, that spotting can't remove.</summary>
public sealed record GunneryLevel
{
    public string Name { get; init; } = "";
    public double ReadingsPerMin { get; init; }
    public double Magnification { get; init; }
    public double LatencyS { get; init; }
    public double Q { get; init; }
    public double PriorKn { get; init; }
    public double PriorDeg { get; init; }
    public double ResidPct { get; init; }
    public double DeflBiasMil { get; init; }
    public double LayMil { get; init; }
    public bool Director { get; init; }
    public double OwnTurnKickM { get; init; }
    public double Ladder0M { get; init; }
    public double SalvoPct { get; init; }
    public double SalvoDeflMil { get; init; }
}

/// <summary>What shells of a quality do when they arm on armour they can defeat (gunfire_ref.py OUTCOME): burst after
/// the fuze's delay, burst in the plate, burst low-order, fail to burst, or break up; shares adding up to 1.</summary>
public sealed record ShellOutcomeRow
{
    public string Name { get; init; } = "";
    public double Burst { get; init; }
    public double InPlate { get; init; }
    public double LowOrder { get; init; }
    public double Dud { get; init; }
    public double Broken { get; init; }
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
