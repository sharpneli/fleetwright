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

    /// <summary>The dark (docs/battle/night.md Q1): the eye's threshold rises as the light it sees falls (L, the horizon
    /// sky's luminance through the eye's glass, cd/m²), each of two ways by (1 + √(L_k / L)) over its value at
    /// <see cref="SightWeberCdM2"/> (Blackwell 1946: flat in the bright, the de Vries-Rose square root in the dark; never
    /// below the day's): the large object's threshold contrast C∞ with its knee at <see cref="SightDarkCdM2"/>, and the side
    /// of the patch the eye sums its light over (√Ω꜀) with its knee at <see cref="SightDarkSummationCdM2"/> (Riccò's area
    /// grows a hundredfold from day to night: small and distant things fade first, large ones close by stay). Fitted to
    /// the record's night ranges [EST]: a destroyer made out at about half a kilometre on an overcast black night (the
    /// Dover Strait), two moonless and clear, three across the sky from Jutland's midnight twilight.</summary>
    public double SightDarkCdM2 { get; init; } = 1e-4;
    public double SightDarkSummationCdM2 { get; init; } = 0.2;
    public double SightWeberCdM2 { get; init; } = 30;

    /// <summary>A ship's contrast against the horizon sky when no sun lights her sides (after sunset, in twilight, under an
    /// overcast, at night: lit by the sky alone, her upper works stand dark against it) [EST], under a sky of
    /// <see cref="SightContrastNightCdM2"/> or less; the day's <see cref="SightContrastDay"/> under
    /// <see cref="SightContrastDayCdM2"/> or more, between on the luminance's log (a clear sky's at sunset and with the sun
    /// 5° up).</summary>
    public double SightContrastNight { get; init; } = 0.6;
    public double SightContrastNightCdM2 { get; init; } = 150;
    public double SightContrastDayCdM2 { get; init; } = 2000;

    /// <summary>A bow wave shows (docs/battle/night.md Q1): white water at her stem, its height
    /// <see cref="SightBowWaveHeight"/> of her speed's head (v² / 2g) and its length <see cref="SightBowWaveLength"/> of
    /// hers along her side, at a contrast of <see cref="SightFoamContrast"/> against her dark hull and the sea [EST: the
    /// research's numbers to come]. Seen side on, and both sheets bow on; not from astern.</summary>
    public double SightBowWaveHeight { get; init; } = 0.3;
    public double SightBowWaveLength { get; init; } = 0.15;
    public double SightFoamContrast { get; init; } = 1.0;

    /// <summary>Lights (docs/battle/night.md Q2). A ship lit by a light near her shines back at this albedo (grey paint)
    /// [EST]. A light seen alone (a salvo's flash, a funnel's glow) gives a contact on its bearing, its range's 1-sigma
    /// this share of the range (no rangefinder takes a flash; the flash to the report's sound is the best of it) [EST],
    /// its speed unknown (1-sigma, m/s). Her own salvo leaves her eyes adapted as to <see cref="SightDazzleCdM2PerCd"/>
    /// cd/m² a candela of its light, fading over <see cref="SightDazzleS"/> s [EST: a 15" turret's ~2.5·10⁷ cd as deep
    /// twilight's sky, gone in some seven seconds]. A coal-fired ship forcing her fires shows a glow and sparks at her funnel
    /// tops, <see cref="SightFunnelGlowCd"/> a funnel at her rated power, growing as the smoke's forcing does [EST].</summary>
    public double SightHullAlbedo { get; init; } = 0.3;
    public double SightLightRangeShare { get; init; } = 0.3;
    public double SightLightSpeedSigmaMs { get; init; } = 8;
    public double SightDazzleCdM2PerCd { get; init; } = 1.2e-9;
    public double SightDazzleS { get; init; } = 1;
    public double SightFunnelGlowCd { get; init; } = 10;

    /// <summary>Searchlights in the fight (docs/battle/night.md Q4) [EST, every number until the research]. Ordered to burn,
    /// the arc is struck and steady behind its shutter in <see cref="SearchlightStrikeS"/> s; ordered on a bearing from
    /// there, it's trained and exposed in <see cref="SearchlightExposeS"/> s. Held on a ship, its beam wanders off her by
    /// <see cref="SearchlightPointingDeg"/> (1-sigma; by its control: local, remote, director); searching, it sweeps its
    /// arc at <see cref="SearchlightSweepDegS"/>, about a beam's width a look. The lamp shows outside its beam by
    /// <see cref="SearchlightStrayShare"/> of its peak (the front glass, forward of it only). Its beam is seen from the side
    /// as the light its first <see cref="SearchlightColumnM"/> scatter (through the haze, and at least
    /// <see cref="SearchlightClearAirPerM"/>: clear air's own, Rayleigh's at 550 nm), and its own light scattered back
    /// (<see cref="SearchlightBackscatter"/>: the haze's phase function at 180° over an even one's) veils the beam to the
    /// eyes beside it. A beam in an eye veils what's near it by Stiles and Holladay, <see cref="SearchlightGlareK"/> E/θ²
    /// cd/m² (E lux at the eye, θ degrees off the lamp, at least <see cref="SearchlightGlareMinDeg"/>). A ship lights a
    /// target with <see cref="SearchlightsOnOne"/> of her lights that bear.</summary>
    public double SearchlightStrikeS { get; init; } = 10;
    public double SearchlightExposeS { get; init; } = 4;
    public double[] SearchlightPointingDeg { get; init; } = [0.5, 0.35, 0.15];
    public double SearchlightSweepDegS { get; init; } = 1.5;
    public double SearchlightStrayShare { get; init; } = 1e-5;
    public double SearchlightColumnM { get; init; } = 2000;
    public double SearchlightClearAirPerM { get; init; } = 1.2e-5;
    public double SearchlightBackscatter { get; init; } = 0.3;
    public double SearchlightGlareK { get; init; } = 10;
    public double SearchlightGlareMinDeg { get; init; } = 1;
    public int SearchlightsOnOne { get; init; } = 2;

    /// <summary>A captain lights a contact (docs/battle/night.md Q4, to fire) within this share of his lights' reach
    /// (<c>SearchlightModel.ReachM</c>: where the operator makes out the reference ship by them), and douses past
    /// <see cref="SearchlightDouseShare"/> of it.</summary>
    public double SearchlightOpenShare { get; init; } = 0.9;
    public double SearchlightDouseShare { get; init; } = 1.2;

    /// <summary>Star shell (docs/battle/night.md Q5) [EST, every number until the research]. An illuminating mount bursts
    /// its shells <see cref="StarBurstM"/> over a point <see cref="StarBeyondM"/> beyond its target (so she stands dark
    /// against the lit sea for the firer), laid by sight with <see cref="StarDispersionMil"/> of error each way on top of
    /// its fuze's own; it fires the next to burst as the last has <see cref="StarOverlapS"/> left to burn, looking every
    /// <see cref="StarCheckMs"/>. The sea under a flare shines back at <see cref="StarSeaReflectance"/> of its light (the
    /// diffuse and the flare's glitter path together) behind a ship between it and the eye.</summary>
    public double StarBurstM { get; init; } = 300;
    public double StarBeyondM { get; init; } = 600;
    public double StarDispersionMil { get; init; } = 2;
    public double StarOverlapS { get; init; } = 3;
    public int StarCheckMs { get; init; } = 1000;
    public double StarSeaReflectance { get; init; } = 0.1;

    /// <summary>A time fuze's longest setting, s [EST: the Great War's mechanical and powder-train fuzes]: no star shell
    /// bursts later, which bounds its reach.</summary>
    public double StarFuzeMaxS { get; init; } = 25;

    /// <summary>The night's challenge (docs/battle/night.md Q6) [EST, every number until the research]: a signal lamp on
    /// the bridge flashes the challenge at a contact for <see cref="ChallengeSignalS"/> s (a two-letter group), a beam of
    /// <see cref="ChallengeLampCd"/> (its flashing's effective light), <see cref="ChallengeLampDeg"/> wide from a
    /// <see cref="ChallengeLampMirrorM"/> mirror; the reply is awaited <see cref="ChallengeReplyS"/> s after it, and given
    /// by the same lamp. A ship that answers it rightly is taken for a friend for <see cref="ChallengeTrustS"/> s, unless
    /// she gives herself away first.</summary>
    public double ChallengeSignalS { get; init; } = 8;
    public double ChallengeReplyS { get; init; } = 20;
    public double ChallengeLampCd { get; init; } = 3e4;
    public double ChallengeLampDeg { get; init; } = 10;
    public double ChallengeLampMirrorM { get; init; } = 0.25;
    public double ChallengeTrustS { get; init; } = 600;

    /// <summary>A bold captain (docs/battle/night.md Q6: his aggression a over 0) opens on a ship he can't make out, under a
    /// doctrine that would challenge her or hold, where no friend of his (but his own unit's, whom he sees in station) is
    /// within this many metres × (1 − a) of her: at a = 1 on any shape [EST]. At most <see cref="LineOfFireCellM"/>.</summary>
    public double UnknownFriendM { get; init; } = 5_000;

    /// <summary>The captains at night (docs/battle/night.md Q7) [EST]. A ship counts herself lit when the light on her from
    /// others' beams and flares is <see cref="LitShare"/> times the night sky's or more; doctrine turning her away, she holds
    /// away from it until <see cref="LitTurnAwayS"/> s after she was last lit. Searching, her lights sweep
    /// <see cref="SearchHalfDeg"/> either side of the nearest contact her navy holds.</summary>
    public double LitShare { get; init; } = 10;
    public double LitTurnAwayS { get; init; } = 120;
    public double SearchHalfDeg { get; init; } = 45;

    /// <summary>The strain of being found and fired on (docs/battle/night.md Q8; crew.md N3b; CUF R2-R3): a salvo that
    /// straddles a ship, or falls while she's lit, adds <see cref="DuressStraddle"/> at every station of a civilian crew
    /// (CUF R2's full weight), <see cref="DuressStraddleGreen"/> of it for green men, fading out by average (her greenness,
    /// 1 less her men's experience), on the burst's 30 s channel [EST].</summary>
    public double DuressStraddle { get; init; } = 0.04;
    public double DuressStraddleGreen { get; init; } = 0.5;

    /// <summary>A moon low enough silhouettes a ship toward it as the low sun does, as strongly as its path on the sea
    /// outshines the sky (<see cref="NightMoonPathShare"/>), fading as it climbs to this elevation, degrees [EST].</summary>
    public double SightSilhouetteMoonHighDeg { get; init; } = 40;

    // ------------------------------------------------------------------ the night's light (docs/battle/night.md Q1)

    /// <summary>The share of the sky's light an overcast passes [EST: overcast moonless nights ~10⁻⁴ lux against a clear
    /// one's 10⁻³].</summary>
    public double NightOvercastShare { get; init; } = 0.15;

    /// <summary>The share of the moon's light clear air scatters into the horizon sky: its luminance is this of the
    /// moon's level illuminance over π [EST: Krisciunas and Schaefer's moonlit sky near the horizon].</summary>
    public double NightMoonSkyShare { get; init; } = 0.25;

    /// <summary>The moon's path on the sea, as a luminance: this of the moon's illuminance over π [EST].</summary>
    public double NightMoonPathShare { get; init; } = 0.1;

    /// <summary>The twilight's arch: its light gathers toward the sun's bearing as e^(k cos Δ), k this under a clear sky
    /// [EST: the horizon toward the sun some twenty times as bright as across from it, e^(2k)], growing as the sun goes
    /// down to <see cref="NightTwilightArchDeg"/> (degrees below the horizon) from none at sunset.</summary>
    public double NightTwilightArch { get; init; } = 1.5;
    public double NightTwilightArchDeg { get; init; } = 6;

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

    /// <summary>The coarse tier's watch (docs/battle/coarse.md J5): every <see cref="SightWatchMs"/> between its looks,
    /// the engaged batteries' targets are looked for again (fire control reads her being in sight every second; 5 s kept
    /// the haze duel's parity at a fifth of the watch's cost at 1 s).</summary>
    public int SightWatchMs { get; init; } = 5000;

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

    /// <summary>Smoke on the horizon (docs/battle/smoke.md Z6): smoke shows as a mass where the eye looks through more than
    /// <see cref="SightSmokeTau"/> of it (the plan's ~0.5); its contrast against the horizon sky by tint (coal, oil,
    /// powder, fire, chemical) [EST: coal and fire black, oil brown, powder and chemical white against a bright sky].</summary>
    public double SightSmokeTau { get; init; } = 0.5;
    public double[] SightSmokeContrast { get; init; } = [0.8, 0.5, 0.3, 0.8, 0.3];

    /// <summary>A sighting of smoke alone: its range's 1-sigma as a share of the range (no rangefinder takes a cloud on the
    /// horizon) [EST]; its bearing's from the eye and this share of the plume's spread across the line (the ships are
    /// somewhere at its head) [EST]; a first report's speed unknown, its 1-sigma, m/s. The errors of one sighting after
    /// another of the same smoke go together: over <see cref="SightSmokeRangeS"/> they count for one [EST].</summary>
    public double SightSmokeRangeS { get; init; } = 300;
    public double SightSmokeRangeShare { get; init; } = 0.25;
    public double SightSmokeBearingShare { get; init; } = 0.5;
    public double SightSmokeSpeedSigmaMs { get; init; } = 8;

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

    /// <summary>A ship keeping station more than StationFarM astern of it (a scout sent 20 km ahead, a screen taking its
    /// place) rings all she makes until she's within it, not the leader's speed and <see cref="FollowMaxSpeedDiffKn"/>
    /// (docs/battle/admirals-orders.md O7). m.</summary>
    public double StationFarM { get; init; } = 2_000;

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

    /// <summary>The coarse tier's fire-control tick, ms (docs/battle/coarse.md J7, rung 2): the same chain every few
    /// seconds. Readings are taken as they fall due on it, and salvos still fire at their own moments. The parity
    /// duels held at 2 s and 3 s, at half the gunnery's cost at 3 s.</summary>
    public int FireControlCoarseMs { get; init; } = 3000;

    /// <summary>Battle against practice: the multiplier on the salvo-to-salvo, laying, rangetaking and spotting errors
    /// (fire_control_ref.py Conditions.combat, calibrated there at 1.5 against battles' records). 1 (the user,
    /// 2026-10-18): the sim is the ideal case, and what the 1.5 lumped together comes as its own systems (the weather,
    /// smoke and sea; crew fatigue), which will take this knob's place.</summary>
    public double GunneryCombat { get; init; } = 1;

    /// <summary>How long after a battery's track starts (its first reading in the computer) it opens fire at the latest,
    /// s (fire_control_ref.py Scenario.t_open). It opens sooner once its computer's doubt of where the shells must go is
    /// within <see cref="OpenDoubtSteps"/> of its first step (twice the sigma) [EST: about two ranging salvos' worth, so
    /// waiting for more readings would buy no more than ranging does], and at once point blank (the layers' own sight).
    /// A fixed two minutes made every shift of target, a destroyer at 3 km as much as a battleship at 15, cost two
    /// minutes of silence (Tsushima, 2026-10-18).</summary>
    public double GunneryOpenS { get; init; } = 120;
    public double OpenDoubtSteps { get; init; } = 2;

    /// <summary>A mount bears when trained and elevated within this of its order, degrees.</summary>
    public double LayToleranceDeg { get; init; } = 1;

    /// <summary>Spotting: the judgement comes this long after a salvo's last shell lands, ms; a splash is judged over or
    /// short with an error of k·R²/h rad (h the spotter's height; fire-control-research.md §9), and the deflection seen
    /// to this many mil, a share of it corrected (half: one salvo's fall carries its random error, so full corrections
    /// chase noise) once it's over a quarter of the pattern across (its own, not the range's: 2.5 times wider, it
    /// left 40 m off the line at 15 km uncorrected, a whole end-on target's beam).</summary>
    public int SpotDelayMs { get; init; } = 6000;
    public double SpotOpticK { get; init; } = 4e-5;
    public double SpotDeflectionMil { get; init; } = 1;
    public double SpotDeflectionGain { get; init; } = 0.5;

    /// <summary>Line first (the user, 2026-10-18): a splash is judged over or short only in line with the target, against
    /// her hull (hidden behind it, or in front of it), within her width across the line of sight (her length and beam by
    /// her aspect) and this margin, m [EST: a heavy splash's column]; a salvo with none in line is doubtful, and only its
    /// line is corrected. The line's correction is an angle, so it holds as the range changes.</summary>
    public double SpotLineMarginM { get; init; } = 25;

    /// <summary>Fire discipline (docs/battle/fire-control.md F6): the spots' bracket is never narrower than this many of
    /// the salvo's patterns (the USN's "never below the pattern"), nor than <see cref="BracketFloorM"/>, m. A ranging
    /// salvo waits for the spots it's due, or this long past its time of flight and the spot's delay, ms (a spot that
    /// never came). Firing for effect, own ship's turn past this yaw rate, °/s, sends a battery back to ranging when its
    /// computer takes own ship's motion in (any turn past <see cref="OwnTurnYawDegS"/> when it doesn't). The rest is the
    /// side's doctrine (<see cref="FireDisciplines"/>).</summary>
    public double BracketFloorPatterns { get; init; } = 1;
    public double BracketFloorM { get; init; } = 92;
    public int RangingWaitMarginMs { get; init; } = 15_000;

    /// <summary>After a straddle or a hit the spots hold her for this long, s [EST]: the guns lay by the bracket, and the
    /// computer's new ranges don't move them (the short-range bug hunt, 2026-10-18).</summary>
    public double SpotsHoldS { get; init; } = 120;

    /// <summary>A reading more than this many sigma off the computer's own prediction is set aside, unless the next is too.</summary>
    public double ReadingGateSigmas { get; init; } = 3;
    public double HardTurnYawDegS { get; init; } = 1;

    /// <summary>After a spot's correction, the next salvo waits this long for the layers to lay on again, s (fire-control.md
    /// F6d: New York's main battery spent 10 s of its 66 at the director, the USN's secondaries of 1929 fired every 7-10 s,
    /// computing.md §2.5; salvos at unchanged settings go as the guns load, Bismarck's ladder 2 s apart). A battery whose
    /// reload is this short or less fires independently inside its discipline's range for it (a quick-firer), s.</summary>
    public double LayerSettleS { get; init; } = 7;
    public double QuickFirerReloadS { get; init; } = 16;

    /// <summary>The fire disciplines a side may follow (fire-control.md F6, decision 3: the side's doctrine, as its crews
    /// are; a scenario's <c>"fire_discipline"</c>): the first is every side's until it's told otherwise. The research's
    /// (computing.md §2.5, fire-control-research.md §6, §9): the RN of 1914 ranging by single half-salvos, each after the
    /// last one's fall, and alternating halves in fire for effect; the RN after Jutland with two in flight; the German
    /// ladder, successive turrets ~400 yd apart (a turret a rung), three salvos before the first falls; the USN's ladder spotted salvo by
    /// salvo, full salvos in fire for effect; before salvo firing (1905-08, manual-gunnery-research.md §5), quick-firers
/// independent within 5.5 km (the pre-dreadnoughts' shift at 5-6 km), the heavy guns by single salvos.</summary>
    public FireDiscipline[] FireDisciplines { get; init; } =
    [
        new() { Name = "rn-1914", InFlight = 1, GunsShare = 0.5, GunsMin = 4, EffectHalves = true, MissesToRange = 3, RateSpotShare = 0.5 },
        new() { Name = "rn-1917", InFlight = 2, GunsShare = 0.5, GunsMin = 4, EffectHalves = true, MissesToRange = 3, RateSpotShare = 0.5 },
        new() { Name = "german", InFlight = 3, LadderStepM = 366, GunsShare = 0.2, GunsMin = 2, EffectHalves = true, MissesToRange = 3 },
        new() { Name = "usn", InFlight = 1, GunsShare = 0.5, GunsMin = 4, MissesToRange = 3 },
        new() { Name = "independent", InFlight = 1, GunsShare = 0.5, GunsMin = 4, MissesToRange = 3, IndependentWithinM = 5_500 },
    ];

    /// <summary>A shell's dispersion across the line of fire as a share of its dispersion along it (Gun.defl_ratio).</summary>
    public double DispersionDeflectionRatio { get; init; } = 0.4;

    /// <summary>Rangefinding (fire-control-research.md §3.2): a reading's unit of error is the optics' (their acuity,
    /// rigid base and flex: the fit's tech block, F3); an instrument's own bias after calibration, in units of error. Its
    /// bearing's error is its post's (SightModel.BearingSigmaRad, fire-control.md F5). The rangetaker's own error is no
    /// fresh draw each reading: the same man reads all through a fight, so his error wanders about the bias as the eye's
    /// does (his eyes tiring, the image's edges judged a little differently), with this time constant, s [EST] (the user,
    /// 2026-10-18: a fresh error each reading would be a different man at each). A coincidence set's two half-images are
    /// brought into line afresh each reading, a vernier setting the eye scatters on and the image shaking as it's caught,
    /// so this share of its man's error (of the sigma) is fresh and the wander √(1 - share²); a stereo set's fused
    /// picture holds his error, all wander [EST] (the user, 2026-10-18: the kinds differing makes the choice matter).</summary>
    public double RangefinderBiasUnits { get; init; } = 1.5;
    public double RangefinderWanderTauS { get; init; } = 300;
    public double RangefinderFreshCoincidence { get; init; } = 0.5;
    public double RangefinderFreshStereo { get; init; } = 0;

    /// <summary>The air on a rangefinder (fire-control.md F5c): a rangetaker reads at his trained acuity while the target's
    /// contrast through the instrument is this many times the eye's threshold (the sighting model's margin) or more; under
    /// it his error, his instrument's bias and his bearing's (by the square root) grow as (this / margin) to a power, a
    /// coincidence set's (two half-images' edges brought into line) more than a stereo set's (two whole images fused).
    /// Coated glass takes this share of the excess. [EST]: the margin is about 4 on a capital ship at 15 km in clear air,
    /// and the powers give the reference's Jutland haze, a stereo set's k 1.4 and a coincidence set's 2.2 on a low-contrast
    /// target (fire_control_ref.py's Conditions, designer §3.1: stereo ×0.8 on the excess, coincidence ×1.5 at low
    /// contrast).</summary>
    public double RangingClearMargin { get; init; } = 4;
    public double RangingPowerCoincidence { get; init; } = 0.8;
    public double RangingPowerStereo { get; init; } = 0.4;
    public double RangingCoatedShare { get; init; } = 0.8;

    /// <summary>A range by eye (fire-control-research.md §3.1: "σ ≈ 20% of range untrained, 10-15% practised", the whole
    /// error of one estimate, a trained crew's 12.5%): a man's own bias, the same in every estimate he makes, and a wander
    /// about it that changes slowly, with this time constant, s [EST], not a fresh draw each reading (the user,
    /// 2026-10-18: spotting walks out a steady error, no walking follows a fresh 19% a minute). Together √(0.10² + 0.075²)
    /// = 12.5%; battle (the combat factor) and the crew (its Operator: green ×2) multiply the wander. Its bearing's error,
    /// mil.</summary>
    public double EyeRangeShare { get; init; } = 0.075;
    public double EyeRangeBiasShare { get; init; } = 0.10;
    public double EyeWanderTauS { get; init; } = 300;
    public double EyeBearingMil { get; init; } = 5;

    /// <summary>Own ship counts as turning (its salvos take its computer's own-turn kick) past this rate of yaw, °/s.</summary>
    public double OwnTurnYawDegS { get; init; } = 0.2;

    /// <summary>A side's gun crews, anchors at the whole numbers of the gunnery level (docs/battle/crew.md decision 6: a row
    /// for each of <see cref="Experience.Names"/> in order), read between them linearly (<see cref="GunneryCrewAt"/>): the
    /// rangetakers' error (Conditions.
    /// operator: 2 green, 1.5 average, 1 trained, 0.85 elite), the layers' (a guess, the same way), and the chance a spot is
    /// applied to the correction in force rather than the one its salvo was fired with (pyramiding, fire-control.md F6:
    /// "a common error", NO&amp;G ch. 18C; the numbers [EST]), and the battle's factor on the drill reload (fire-control.md
    /// F6d: the loaders' fatigue, heat and fumes, the handling in a seaway; [EST]: with the hoists, the director's cycle and
    /// the ranging waits, battle rates come to the research's 30-60% of drill, predreadnought-tech.md §3).</summary>
    public GunneryCrew[] GunneryCrews { get; init; } =
    [
        new() { Name = "green", Operator = 2, Lay = 1.3, Pyramid = 0.25, Load = 1.5 },
        new() { Name = "average", Operator = 1.5, Lay = 1.15, Pyramid = 0.1, Load = 1.35 },
        new() { Name = "trained", Operator = 1, Lay = 1, Pyramid = 0.03, Load = 1.2 },
        new() { Name = "elite", Operator = 0.85, Lay = 0.9, Pyramid = 0, Load = 1.1 },
    ];

    /// <summary>The share of her men's experience a crew new to the ship shoots at (docs/battle/crew.md decision 6:
    /// gunnery level = experience × (0.3 + 0.7 × work-up) [EST: Chester, long-service men some weeks into a new ship,
    /// shot green to average; CUF §9's "new to the ship: down one for gunnery"]).</summary>
    public double WorkUpGunneryShare { get; init; } = 0.3;

    /// <summary>A crew's gunnery level from her side's experience (0-3) and her work-up (0-1): the experience itself when
    /// worked up (written so that's exact, the rows' anchors read as they are).</summary>
    public double GunneryLevel(double experience, double workUp) => experience * (1 - (1 - WorkUpGunneryShare) * (1 - workUp));

    /// <summary>The gun crews at a gunnery level, read linearly between the <see cref="GunneryCrews"/> at the whole
    /// numbers (an anchor exactly on one), held at the ends.</summary>
    public GunneryCrew GunneryCrewAt(double level)
    {
        var rows = GunneryCrews;
        int i = Math.Clamp((int)Math.Floor(level), 0, rows.Length - 1);
        double t = Math.Clamp(level - i, 0, 1);
        if (t == 0 || i == rows.Length - 1)
            return rows[i];
        ref readonly var a = ref rows[i];
        ref readonly var b = ref rows[i + 1];
        return new()
        {
            Name = "", Operator = a.Operator * (1 - t) + b.Operator * t, Lay = a.Lay * (1 - t) + b.Lay * t,
            Pyramid = a.Pyramid * (1 - t) + b.Pyramid * t, Load = a.Load * (1 - t) + b.Load * t,
        };
    }

    // ------------------------------------------------------------------ fire orders (docs/battle/fighting.md 4.2c)

    /// <summary>Fire at will: a battery takes contacts in sight within its reach (the range table at its highest
    /// elevation) times <see cref="FireReachShare"/>, so it tracks one a little before it can fire, and keeps one to
    /// <see cref="FireKeepShare"/> of it (no flicking at the edge); it keeps its target until the contact is unseen for
    /// <see cref="FireLostMs"/>, or another is nearer than <see cref="FireSwitchShare"/> of its range. Nearer is the range
    /// she'll be at <see cref="FireLeadS"/> on, as the picture has her moving [EST: a destroyer's run of 1.5-2 km], so
    /// one closing comes before one turning away (Tsushima, 2026-10-18: the line kept hammering a destroyer already
    /// running while the others closed); and each other ship's battery of the same kind already on her counts as
    /// <see cref="FireCrowdShare"/> more of it [EST], so a line spreads its fire rather than all taking the nearest (a
    /// second ship's splashes confuse the first's spotting, fire-control-research.md §9).</summary>
    public double FireReachShare { get; init; } = 1.1;
    public double FireKeepShare { get; init; } = 1.25;
    public long FireLostMs { get; init; } = 30_000;
    public double FireSwitchShare { get; init; } = 0.7;
    public double FireLeadS { get; init; } = 120;
    public double FireCrowdShare { get; init; } = 0.25;

    /// <summary>A secondary battery's range to a contact counts longer by this share for each step of her type's rank,
    /// small craft 0 to capital ships 4 [EST] (the user, 2026-10-27: the rank once came before the range, and a battleship's
    /// 6" left one alongside for a destroyer 8 km off). A capital ship counts as at three times her range, a destroyer one
    /// and a half: a boat 8 km off before a battleship beyond 4, a battleship at 1.5 km before a boat beyond 4.5.</summary>
    public double FireSizeShare { get; init; } = 0.5;

    /// <summary>The shell by target (docs/battle/shell-choice.md A3): a captain changes a battery's load only for a kind
    /// whose rounds do this many times the one loaded [EST], since each change costs the round in the gun.</summary>
    public double ShellSwitchGain { get; init; } = 1.25;

    /// <summary>An admiral distributing fire: the enemy's line is the contacts in sight chained to the target, each
    /// within this of the next, m.</summary>
    public double FireLineGapM { get; init; } = 3_000;

    /// <summary>Fight (a captain's minimal fighting manoeuvre): how far either side of the range he keeps is near enough
    /// (a share of it); the angle off the line of sight he closes or opens at, degrees; the radius the enemy round him is
    /// judged in (the matchup, docs/battle/command.md 5.1g), m; and turned away, the share of doctrine's bar he must be
    /// over again to turn back (the bar over it). The range and the bar are doctrine's (RangeShare, OutgunnedRatio).
    /// Closing, he turns in further when that angle wouldn't close on an enemy running away by this many knots [EST]
    /// (40° in from the beam at 22 kn closed 0.3 kn on a 12 kn runner and never reached the band).</summary>
    public double FightBandShare { get; init; } = 0.15;
    public double FightCloseDeg { get; init; } = 40;
    public double FightMinCloseKn { get; init; } = 3;
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

    /// <summary>Torpedoes seen (docs/battle/command.md 5.1a): a wake is seen within TorpedoSeenM of a ship by day, less in
    /// the dark as the eye's law has it (<c>SightModel.TrackSeenM</c>, docs/battle/night.md Q1); one passing within her
    /// half-length and TorpedoCombMarginM is coming at her; she holds the comb TorpedoCombAfterS after it's past. m, s.</summary>
    public double TorpedoSeenM { get; init; } = 2_000;

    /// <summary>A track's solid angle to a lookout's naked eye at its day's reach, sr [EST: some 200 m of foam a few metres
    /// wide, foreshortened from 15 m up at 2 km, ~4 m² showing]: how small it is sets how the dark shortens its reach.</summary>
    public double TorpedoTrackSolidAngle { get; init; } = 1e-6;
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
    /// TorpedoThreatM of a ship of it and running in at her (their own speed toward her) faster than TorpedoThreatRunInMs,
    /// or a ship of it combing a track; a line turned away (doctrine's TurnAwayDeg) turns back once it has seen none for
    /// TurnBackS. m, m/s, s. The run in is a boat's own: 6 m/s is a 25-knot boat heading within some 60° of her, an
    /// attack's, not one crossing or lying ahead of a line that steams at it (2026-10-25, the user's playtest).</summary>
    public double TorpedoThreatM { get; init; } = 8_000;
    public double TorpedoThreatRunInMs { get; init; } = 6;
    public double TurnBackS { get; init; } = 180;

    /// <summary>A flotilla's attack (docs/battle/command.md 5.1b): it first gains a point AttackGainDeg off the target's bow,
    /// AttackGainShare of its torpedoes' fast range from it, on the side it's on; it runs in from there (or once it's that far
    /// round the target's bow, or after AttackGainMaxMs). Degrees, a share, ms.</summary>
    public double AttackGainDeg { get; init; } = 50;
    public double AttackGainShare { get; init; } = 1.5;

    /// <summary>In the dark a flotilla attacks from the side of the target's bow that has him against a twilight sky this
    /// many times as bright as the other side's, or against the moon's path (docs/battle/night.md Q7) [EST].</summary>
    public double AttackSkyRatio { get; init; } = 1.5;
    public long AttackGainMaxMs { get; init; } = 900_000;

    /// <summary>A flotilla's signal to fire holds this long: each boat fires once as her tubes bear, s.</summary>
    public double AttackFireS { get; init; } = 60;

    /// <summary>A screen (docs/battle/command.md 5.1b) keeps ScreenM off the unit it screens unless told otherwise, and is
    /// given a new station only when the bearing to the enemy moves it more than ScreenMoveM. m.</summary>
    public double ScreenM { get; init; } = 5_000;
    public double ScreenMoveM { get; init; } = 1_000;

    /// <summary>A unit's screens share its arc (docs/battle/admirals-orders.md O1c): one sits on the bearing screened on,
    /// two or more spread evenly to ScreenArcDeg either side of it (two: four points on either bow). Degrees.</summary>
    public double ScreenArcDeg { get; init; } = 45;

    /// <summary>A squadron alone on the arc it screens on, with this many ships or more in its line, is divided into two
    /// divisions by its admiral to cover both bows (unless its order is the player's by hand; 0: never). Ships.</summary>
    public int ScreenDivideShips { get; init; } = 4;

    /// <summary>Where a screen sits with the enemy in sight (docs/battle/admirals-orders.md O5): its admiral judges his
    /// screen alone against the enemy in sight of the screened unit, and under doctrine's bar by ScreenMarginShare takes it
    /// to the disengaged side; back toward the enemy once over the bar by as much. A share.</summary>
    public double ScreenMarginShare { get; init; } = 0.25;

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

    /// <summary>A side declining battle by day keeps its flotillas in touch with the enemy, for their night attack, while
    /// the night falls within this many seconds (docs/battle/night.md Q7) [EST].</summary>
    public double CommanderNightWaitS { get; init; } = 5400;

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



    /// <summary>A shell under the water (water is a crossing, not a stop; WaterRun) slows as e^(−x/L), L this many
    /// calibres: a pointed shell, its tumbling folded in (a stable ogive ~150, armor/05 §6; 25 fits 08 §3.8's 30-50% of
    /// its velocity a few metres down and Tosa's 76 mm), and a diving one's flat nose (~45-60).</summary>
    public double UnderwaterDecayCalibres { get; init; } = 25;
    public double UnderwaterDecayCalibresDiving { get; init; } = 50;

    /// <summary>A shell's decay length through a granular or distributed solid (coal; a boiler's tube banks, a turbine's
    /// blading, stacked charges: their metal and water per m³) is this over the medium's density, in calibres
    /// (docs/battle/shell-contents.md; damage/09 §1.1): 96,000 is 120 calibres in coal at 800 kg/m³, fitted to "2 ft of
    /// coal ≈ 1 in of armour" at 6-12" ([INFERRED]; a stable shell's in water, scaled by density, would be ~215). A liquid
    /// inside the hull is the sea's physics: <see cref="UnderwaterDecayCalibres"/> × 1,025 over its density.</summary>
    public double GranularDecayC { get; init; } = 96_000;

    /// <summary>The share of a full load of fuel a ship goes into battle with, where the scenario gives her side none
    /// (docs/battle/shell-contents.md C2): 0.8, ships coaled or oiled before a sortie (damage/09 §4: about 0.9 just after
    /// coaling, 0.3-0.5 after a week at sea). What's left of it stands in her tanks and bunkers, and a shell meets it.</summary>
    public double FuelShareDefault { get; init; } = 0.8;

    /// <summary>The protective side spaces (wing bunkers, TDS layers) kept full and the others burnt first, the period's
    /// practice (damage/09 §4, a doctrine: [UNCERTAIN] as a found rule); off, every fuel space falls alike.</summary>
    public bool ProtectiveBunkersLast { get; init; } = true;

    /// <summary>A burst's fragments through coal slow 1/e every this·m^⅓ m (docs/battle/shell-contents.md C3b; damage/09
    /// §4: a tumbling splinter's e-fold about 15 of its own sizes [EST], a steel cube's side 0.050·m^⅓): coal is
    /// "quite effective at catching fragments". Through a liquid the sea's <see cref="WaterDragL1"/>, by density.</summary>
    public double CoalFragmentDragL1 { get; init; } = 0.75;

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
    public double[] PartVentShare { get; init; } = [0.05, 0.1, 1, 1, 0.02, 0.15, 0.05, 1, 1, 0.3, 0.2, 1, 0, 0.01, 1, 1, 1, 1, 0.1, 0.1, 1];
    public double PartAirShare { get; init; } = 0.7;

    /// <summary>What a space's or a part's gear takes from the gas, MPa (11 §4.3 [EST]: fittings 0.03, electrical 0.10,
    /// auxiliary 0.25, machinery 0.6), by room kind and component kind: out of action from it, damaged from
    /// <see cref="BlastDamagedShare"/> of it.</summary>
    public double[] RoomBlastMPa { get; init; } = [0.03, 0.03, 0.25, 0.25, 0.6, 0.25, 0.25, 0.25, 0.6, 0.25, 0.6, 0.6, 0.03, 0.25, 0.03];
    public double[] PartBlastMPa { get; init; } = [0.10, 0.10, 0.25, 0.10, 0.25, 0.03, 0.10, 0.25, 0.25, 0.25, 0.10, 0.25, 0.6, 0.6, 0.6, 0.6, 0.25, 0.03, 0.03, 0.03, 0.10];
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
    /// torpedo tube, flight deck, hangar bay, a mast's top, searchlight): a burst's fragments that get into it damage it
    /// at this share of their count (docs/battle/shells-armour-damage.md E1, the user's choice: like RoomVital). A funnel
    /// or uptake holed by splinters keeps its draught; an open mount's crew and sights, a conning tower's instruments, a
    /// tube's torpedoes and a searchlight's mirror don't [EST].</summary>
    public double[] PartVital { get; init; } = [0.3, 0.3, 0.5, 0.5, 0.2, 0.2, 0.5, 0.05, 0.05, 0.1, 0.1, 0.1, 0.05, 0.2, 0.05, 0.05, 0.5, 0.1, 0.2, 0.2, 0.5];

    /// <summary>A boiler room keeps this share of its steam with every funnel it draws through wrecked (the draught
    /// lost; 08-gunfire-effects.md §8: funnel hits cost draught, not the boilers).</summary>
    public double DraughtFloor { get; init; } = 0.7;

    /// <summary>The fewest men a gun is fought with (docs/battle/crew.md N2): one a barrel per 1/GunMinMenPerMm mm of
    /// calibre, 1 to <see cref="GunMinMenMax"/> [EST: a heavy gun's layer, loader and rammer, 3; a 3-pdr's layer and
    /// loader, 1-2]; a mount with fewer is silent, and with more fires at its men over its crew of the reload.</summary>
    public double GunMinMenPerMm { get; init; } = 0.02;
    public int GunMinMenMax { get; init; } = 3;

    /// <summary>The control shifting aft when the conn forward is lost, s (docs/battle/crew.md N2) [EST: CUF §4's 5-10 min;
    /// Marlborough 9 min]: she isn't conned meanwhile.</summary>
    public double ConnShiftS { get; init; } = 420;

    /// <summary>A boiler room's steam by its stokers (docs/battle/crew.md N2): this share kept with nobody there, the rest
    /// by its men over its crew [EST: the fires burn on, the water and the draught still go]; and a drop comes only after
    /// what's already in the fires has burned, s [EST: a fire's charge lasts minutes].</summary>
    public double StokeholdFloor { get; init; } = 0.3;
    public double StokeholdHoldS { get; init; } = 600;

    /// <summary>A relief's walk to a station (docs/battle/crew.md N3a) [EST: CUF R10, 20-60 s between adjacent guns, 1-2
    /// min within a section, 3-6 min end to end below the armour deck]: this many s, and this many a section between.</summary>
    public double ReliefWalkS { get; init; } = 45;
    public double ReliefWalkSectionS { get; init; } = 12;

    /// <summary>A station's duress (docs/battle/crew.md N3b; all [EST] from CUF R1-R4, in station units: 1 = its whole
    /// crew hit at once), what has landed at and around it, held in channels each fading by its half-life, s: a near
    /// burst's, a hit inside it, flash or fire there, its casualties (and its officer hit, its neighbours' casualties), and
    /// the share of its casualties held longer.</summary>
    public double[] DuressHalfLifeS { get; init; } = [30, 90, 120, 240, 900];
    public double DuressHeldShare { get; init; } = 0.3;

    /// <summary>What adds to a station's duress (N3b): its officer hit; the casualties at a neighbouring open station (as a
    /// share of theirs, open stations within <see cref="DuressNeighbourM"/> of each other); a shell through it; flash in it,
    /// or a fire while it burns there; a burst within <see cref="DuressNearM"/> of it, by how its men are covered (open,
    /// shielded, a casemate, a turret or below the armour); a relief officer come (taken off).</summary>
    public double DuressOfficerHit { get; init; } = 0.15;
    public double DuressNeighbourShare { get; init; } = 0.3;
    public double DuressNeighbourM { get; init; } = 15;
    public double DuressInside { get; init; } = 0.3;
    public double DuressFire { get; init; } = 0.4;
    public double DuressNearBurst { get; init; } = 0.08;
    public double DuressNearM { get; init; } = 15;
    public double[] DuressNearCover { get; init; } = [1, 0.6, 0.4, 0.3];
    public double DuressOfficerCome { get; init; } = 0.15;

    /// <summary>The men's nerve by their experience (N3b, decision 6: a row for each of <see cref="Experience.Names"/>, read
    /// between them; work-up doesn't enter) [EST: CUF R1-R4]: the threshold in duress; the most of an open station's and an
    /// enclosed one's men (a turret, a casemate, below the armour) who go on their own; how long after the duress falls
    /// below <see cref="NerveBackShare"/> of the nerve they come back, s; and how fast duress fades for them.</summary>
    public NerveRow[] Nerves { get; init; } =
    [
        new() { Name = "green", Nerve = 0.30, GoingOpen = 0.35, GoingEnclosed = 0.15, BackS = 360, Fade = 0.67 },
        new() { Name = "average", Nerve = 0.45, GoingOpen = 0.20, GoingEnclosed = 0.08, BackS = 180, Fade = 1 },
        new() { Name = "trained", Nerve = 0.60, GoingOpen = 0.10, GoingEnclosed = 0.04, BackS = 90, Fade = 1.2 },
        new() { Name = "elite", Nerve = 0.75, GoingOpen = 0.05, GoingEnclosed = 0.02, BackS = 45, Fade = 1.5 },
    ];

    /// <summary>The men's nerve at an experience, the rows read between the whole numbers.</summary>
    public NerveRow NerveAt(double experience)
    {
        var rows = Nerves;
        int i = Math.Clamp((int)Math.Floor(experience), 0, rows.Length - 1);
        double t = Math.Clamp(experience - i, 0, 1);
        if (t == 0 || i == rows.Length - 1)
            return rows[i];
        ref readonly var a = ref rows[i];
        ref readonly var b = ref rows[i + 1];
        return new()
        {
            Name = "", Nerve = a.Nerve * (1 - t) + b.Nerve * t, GoingOpen = a.GoingOpen * (1 - t) + b.GoingOpen * t,
            GoingEnclosed = a.GoingEnclosed * (1 - t) + b.GoingEnclosed * t, BackS = a.BackS * (1 - t) + b.BackS * t, Fade = a.Fade * (1 - t) + b.Fade * t,
        };
    }

    /// <summary>How the men go (N3b) [EST: CUF R1]: the share going rises as a logistic in duress about the nerve, this
    /// share of the nerve wide; and they come back once the duress is under this share of it.</summary>
    public double NerveWidth { get; init; } = 0.15;
    public double NerveBackShare { get; init; } = 0.6;

    /// <summary>A station's officer (N3b, decision 7) [EST: CUF R4]: there and unhurt, the nerve and the share going by
    /// these; hit, the nerve and the time to come back by these until a relief officer walks in. The ship's spare officers,
    /// a share of her battle crew (at least one).</summary>
    public double OfficerNerve { get; init; } = 1.25;
    public double OfficerGoing { get; init; } = 0.5;
    public double OfficerHitNerve { get; init; } = 0.8;
    public double OfficerHitBack { get; init; } = 1.75;
    public double OfficersSpare { get; init; } = 0.01;

    /// <summary>The frozen men (N3b) [EST: CUF §1.6, Semenov]: at a green station (by how far its experience is under 1)
    /// with its officer hit, after a shell through it or a fire in it, this share of its men present and not working until
    /// an officer comes.</summary>
    public double FrozenShare { get; init; } = 0.3;

    /// <summary>A station sensitised (N3b) [EST: CUF R2]: each time it goes over its nerve, the nerve falls by this share,
    /// this much of it back with this half-life, s.</summary>
    public double SensitiseLoss { get; init; } = 0.13;
    public double SensitiseBackShare { get; init; } = 0.5;
    public double SensitiseBackS { get; init; } = 600;

    /// <summary>How often the stations whose men are shaken, frozen or over their nerve are looked at again, s (N3b).</summary>
    public double NerveCheckS { get; init; } = 10;

    /// <summary>How often a captain looks over where his ship's men stand (docs/battle/crew.md N3a: her posture, her
    /// reliefs), s [EST: a crew walks to its gun in 45 s or more anyway].</summary>
    public double CrewPlanS { get; init; } = 5;

    /// <summary>The outages a captain waits out in the line, minutes (docs/battle/command.md 5.1a item 2; the user,
    /// 2026-10-20): his fall-out reads his ship as she'll be once what ends within this is back (a blackout, a tripped
    /// breaker, a stunned crew, a short jam), not what's down for a while. 13 §1.3's short jams are under 15 min, the
    /// trips 1-5 [EST].</summary>
    public double FallOutWaitMin { get; init; } = 15;
    public double NeighbourShare { get; init; } = 0.5;

    /// <summary>Holes: a shell that passes through plating leaves a hole this many calibres across.</summary>
    public double ShellHoleCalibres { get; init; } = 1.5;

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
    /// are (MagazineFlashChance in a magazine, StationLightChance at a station of a train); a boundary of SpallStopMm or more stops
    /// the flakes unless THOR lets them through (14 §4.3). What a flake or the big piece must carry to harm a part, J, by
    /// its kind (as PartVital's list), and a space's vitals (pipes, cables, gauges) [EST, 14 §5.2: instrument or sight
    /// ~100 J, cable or pipe ~500 J, a machine's casing ~5 kJ].</summary>
    public double SpallRoofTD { get; init; } = 0.4;
    public double SpallRoofChance { get; init; } = 0.3;
    public double SpallFlashChance { get; init; } = 0.2;
    public double SpallStopMm { get; init; } = 5;
    public double[] PartFragilityJ { get; init; } = [100, 100, 500, 100, 5000, 100, 100, 5000, 5000, 5000, 5000, 500, 5000, 500, 5000, 5000, 500, 500, 500, 500, 100];
    public double RoomFragilityJ { get; init; } = 500;

    /// <summary>Fire: a burst's chance by the shell's kind (AP, common, HE; 08 §3.9's WWII bursts), as a rate times its
    /// filler's fire over <see cref="FireFillerRef"/> (damage/12 §4.1: TNT's, the WWII shells' the chances were read from;
    /// black powder 4 times it), times the fire load where it bursts over
    /// <see cref="FireStartLoadMJm2"/> (the space's, from the design's outfit: docs/battle/fire.md B0; at most
    /// <see cref="FireStartLoadCap"/>; 560 MJ/m² reads a Great War messdeck as Fire v0's hull did, 0.8), times the burst's
    /// size (0.5 + 0.5 times the cube root of W over 10 kg, at most 1.5), and this share when it bursts outside; the seat
    /// it lights, MW by kind (damage/17 §1: HE 0.1-0.5 MW [EST]; fire_ref.py's SHELL_Q0); fragments reaching a mount's
    /// ready ammunition light it with this chance, its seat that.</summary>
    public double[] FireChance { get; init; } = [0.15, 0.30, 0.45];
    public double FireFillerRef { get; init; } = 0.25;
    public double FireStartLoadMJm2 { get; init; } = 560;
    public double FireStartLoadCap { get; init; } = 2;
    public double[] FireSeatMW { get; init; } = [0.1, 0.4, 0.3];
    public double FireOutsideShare { get; init; } = 0.4;
    /// <summary>A burst outside lights the weather deck only this close under its top, m, or above it (one on the side
    /// lower down lights nothing).</summary>
    public double FireDeckBurstM { get; init; } = 0.5;
    /// <summary>A burst outside the hull up to this high over its section's weather deck showers it with its burning pieces
    /// and products (as one outside), m [EST: a deckhouse's tier or two] (fire.md B5).</summary>
    public double FireDeckReachM { get; init; } = 5;
    public double ReadyAmmoFireChance { get; init; } = 0.3;
    public double ReadyAmmoSeatMW { get; init; } = 1;

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

    /// <summary>A burst among a magazine's charges (in it, its pieces or gas into it) sets it off with
    /// MagazineFlashChance. v0: the ship is lost at once and goes under <see cref="MagazineLossMs"/> later (the venting
    /// race is docs/battle/magazines.md V3's).</summary>
    public double MagazineFlashChance { get; init; } = 0.25;

    /// <summary>The ammunition trains (docs/battle/magazines.md V2; research/simulation/damage-research/10 §3), by a
    /// station's kind [gunhouse, working chamber, casemate, handling room]:
    /// <list type="bullet">
    /// <item>StationLightChance: a burst, its spall or its gas at a station lights each charge lying there with this
    /// chance, the station if any lights (<c>Magazines.LightChance</c>): fitted at V8 to the record's turret and barbette
    /// bursts, 1914-16, about 9 lit in 12 (Seydlitz, Derfflinger C and D, Lion Q, the three battlecruisers, Blücher,
    /// Malaya's battery; not Princess Royal's or Tiger's X) with ~2 charges a station [EST, 10 §3.2];</item>
    /// <item>the doors and the charges lying at the stations are the side's flash discipline's
    /// (<see cref="FlashDisciplines"/>, V5).</item>
    /// </list></summary>
    public double[] StationLightChance { get; init; } = [0.5, 0.5, 0.5, 0.5];

    /// <summary>The venting race (docs/battle/magazines.md V3; research/simulation/damage-research/10 §6,
    /// magazine_race_ref.py): a magazine alight is one gas volume, its spaces' gas, made by the lit charges' surface and
    /// let out through the vents.
    /// <list type="bullet">
    /// <item>MagazineHeatKeep: the share of the impetus left as pressure in a cold steel room [EST: losses, mixing];
    /// MagazineVentCd a ragged opening's discharge coefficient [EST]; MagazineGasGamma the hot gas's [EST: 1.2-1.25].</item>
    /// <item>MagazinePackageR [bag, Clarkson case, brass case, tank]: how many neighbours one burning charge lights in its
    /// package before it burns out, a branching process (10 §1.3 [EST, fitted]: Clarkson ~4, brass ~1.4, tank ~1.2); bags
    /// alone light together, within MagazineBagSpreadS of the flash [EST: 0.2-1 s].</item>
    /// <item>The way out: the magazine's door or scuttle to its handling room gives at MagazineScuttleKPa [EST, 10 §6:
    /// 40 kPa]; a gunhouse's roof at MagazineRoofShare of its lift [EST, 10 §2.3: 0.3-0.5], the gunhouse itself at its
    /// lift (<see cref="Fleetwright.Sim.Ships.ShipClass.LiftKPa"/>); either opens its trunk, MagazineTrunkShare of the
    /// barbette's bore [EST, 10 §2.2: 2-6 m² for a heavy turret]; a mount without a barbette has its hoist,
    /// MagazineHoistM2 [EST], at the scuttle's pressure. The magazine's own boundaries give by the research's table
    /// (10 §6 [EST]), MagazineGiveKPa and the vent each opens, MagazineGiveM2: [the bulkhead to the next space, 250 kPa,
    /// a 4 m² tear; the armoured deck over it, 700, 10 m²; its sides, 1,000, 60 m²; its bottom, 2,000, 60 m²]. An
    /// approved exception to the blast's own rules (the user, 2026-10-20): on a ship's own plating they spread the gas
    /// through her thin bulkheads and never load the girder.</item>
    /// <item>The hull girder at the magazine (V4; 07 §3.1): the share of its section lost there, d, by MagazineD [the deck
    /// over it, its sides, its bottom] [EST, 10 §6]; it breaks when the moment on it passes what's left of its capacity,
    /// K·(1 − d) ≥ sin(π·x/L)·(still + wave·sea + whip), MagazineGirder [K, still water, the waves in a seaway, the
    /// explosion's whipping] as shares of the design moment [EST, fitted to 07 §3.1: amidships in calm water past d ≈
    /// 0.75, in a seaway past ≈ 0.5, near the ends hardly ever]; the sea is the wind's share of MagazineSeawayMs (a
    /// moderate gale) [EST]. A magazine within MagazineEndShare of the length from an end that loses MagazineEndD of
    /// the section without breaking her takes that end off instead (New Orleans: the bow lost, the ship afloat).
    /// Less, it's a local breach; a gunhouse vented and nothing more, burned out; else contained.</item>
    /// <item>The steps: MagazineRaceMs [the first 5 s, after], up to MagazineRaceS.</item>
    /// </list></summary>
    /// <summary>The magazines' flooding (docs/battle/magazines.md V6; research 10 §4): a flash in a train orders its
    /// magazine flooded MagazineOrderS later (the order and the walk to the valves, 1-5 min [INFERRED]); its valves fill it
    /// in FireMagazineFloodS (10-30 min by their bore); sprinklers, where fitted, wet MagazineSprinkleShare of the stow in
    /// MagazineSprinkleS [EST].</summary>
    public double MagazineOrderS { get; init; } = 120;
    public double MagazineSprinkleS { get; init; } = 60;
    public double MagazineSprinkleShare { get; init; } = 0.95;
    public double MagazineHeatKeep { get; init; } = 0.5;
    public double MagazineVentCd { get; init; } = 0.7;
    public double MagazineGasGamma { get; init; } = 1.25;
    public double[] MagazinePackageR { get; init; } = [0, 4, 1.4, 1.2];
    public double MagazineBagSpreadS { get; init; } = 0.5;
    public double MagazineScuttleKPa { get; init; } = 40;
    public double MagazineRoofShare { get; init; } = 0.4;
    public double MagazineTrunkShare { get; init; } = 0.05;
    public double MagazineHoistM2 { get; init; } = 0.25;
    public double[] MagazineGiveKPa { get; init; } = [250, 700, 1000, 2000];
    public double[] MagazineGiveM2 { get; init; } = [4, 10, 60, 60];
    public double[] MagazineD { get; init; } = [0.35, 0.45, 0.2];
    public double[] MagazineGirder { get; init; } = [2.5, 0.35, 0.6, 0.35];
    public double MagazineSeawayMs { get; init; } = 15;
    public double MagazineEndShare { get; init; } = 0.12;
    public double MagazineEndD { get; init; } = 0.8;
    public double[] MagazineRaceMs { get; init; } = [2, 20];
    public double MagazineRaceS { get; init; } = 180;
    /// <summary>How a side's crews handle charges (docs/battle/magazines.md V5, decision 2; research/simulation/
    /// damage-research/10 §3): a doctrine row names one (<see cref="DamageControlDoctrine.Flash"/>).
    /// <list type="bullet">
    /// <item><b>safe:</b> interlocked doors and revolving scuttles, never open through (the RN from 1917, KGV's, the USN's
    /// later); half a charge a gun at a station; the hoists slower for their interlocks (KGV, Prince of Wales).</item>
    /// <item><b>drill:</b> each door opens as the cage passes, 5-15% of the time (a casemate's hoist and its passage's
    /// magazine door in series, well under 5%: Kent, §5); about a charge a gun at each station [SRC: Brown, 8 in a twin
    /// turret's train in action].</item>
    /// <item><b>rate first:</b> doors propped or removed (the BCF, Seydlitz's connecting door), charges stacked at the
    /// stations, the Clarkson cases' lids off in the magazine (10 §6.2's BCF anchor; whether the lids came off in the
    /// magazine or only in the handing room is 10 §8's open question, Friedman against Brown) [EST]; the hoists hardly
    /// wait.</item>
    /// </list>
    /// DoorOpen by the station above the door [gunhouse, working chamber, casemate, handling room]; StationCharges a gun;
    /// LidsOff the share of a magazine's stow lying open; HoistShare the hoists' sustained reload over the drill's
    /// (fire-control F6d's <c>DesignerPlaceholders.HoistShare</c>, 1.5, is drill's).</summary>
    public FlashDiscipline[] FlashDisciplines { get; init; } =
    [
        new() { Name = "safe", DoorOpen = [0, 0, 0, 0], StationCharges = 0.5, LidsOff = 0, HoistShare = 1.7 },
        new() { Name = "drill", DoorOpen = [0.1, 0.1, 0.02, 0.1], StationCharges = 1, LidsOff = 0, HoistShare = 1.5 },
        new() { Name = "rate first", DoorOpen = [1, 1, 0.5, 1], StationCharges = 2, LidsOff = 1, HoistShare = 1.0 },
    ];
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
    /// <summary>... by component kind (Main ... Searchlight; a mount's own by <see cref="CoverMount"/>): a station (a bridge,
    /// a director, the conning tower) 0.7, a barbette's handling crew 0.45, the open (AA, tubes, masts, flight deck,
    /// searchlights) 0.8.</summary>
    public double[] CoverPart { get; init; } = [0.45, 0.45, 0.8, 0.8, 0.45, 0.7, 0.7, 0.8, 0.8, 0.6, 0.6, 0.8, 0.3, 0.3, 0.3, 0.3, 0.8, 0.8, 0.6, 0.6, 0.8];
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
    /// near misses of 1939-41 [FIT]): 0.03 fragile gear (breakers trip, each mount's and director's by its own NSF, and
    /// the directors' gyros, for <see cref="NearTripMin"/>), 0.08 machinery mounts and castings (a machinery space's gear
    /// damaged), 0.2 the hull's seams (shafts strained; the whole ship's breakers by her machinery spaces' NSF, White
    /// Plains' 0.3-0.6: E7's fix, 2026-10-20), 0.45 plating buckled (shafts and thrusts out of line, the machinery
    /// again), 1.0 plating split. Each happens with a chance rising across its band from 0 to 1 [EST]. The
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
    /// <summary>The share of her displacement her water may move in one step while it isn't settled: the step is the
    /// longest (up to <see cref="FloodIdleMs"/>) that keeps to it at the last step's rate (docs/battle/coarse.md J6); 0
    /// steps it every <see cref="FloodMs"/> as before.</summary>
    public double FloodSteadyShare { get; init; } = 1e-4;

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

    /// <summary>A cell's free surface counts in full once its water is this share from dry and from full, less nearer
    /// either (the water can't run across as the ship heels).</summary>
    public double FreeSurfacePocket { get; init; } = 0.1;

    /// <summary>Shallow water pools at the low side (09 §2.5): its free surface no more than its volume × a third of the
    /// breadth over tan of the heel, the heel taken as at least this, degrees (upright, the pool's side isn't yet chosen)
    /// (docs/battle/damage-control.md DC9) [EST].</summary>
    public double FreeSurfacePoolMinDeg { get; init; } = 2;

    /// <summary>The heel comes on toward what the water gives with this time constant, s (a ship lolls over its rolls,
    /// not at once).</summary>
    public double HeelLagS { get; init; } = 30;

    /// <summary>A ship capsizes past this heel, degrees (damage-model-research.md §8.4: 20-30°).</summary>
    public double CapsizeHeelDeg { get; init; } = 25;

    /// <summary>How long a ship takes to go under once it founders or capsizes, ms (a magazine:
    /// <see cref="MagazineLossMs"/>).</summary>
    public int FounderMs { get; init; } = 600_000;
    public int CapsizeMs { get; init; } = 180_000;

    // ------------------------------------------------------------------ leaks with structure (docs/battle/damage-control.md DC1)
    // damage/17 §4-§7, 09 §2.2-§2.4

    /// <summary>A boundary's rating, the head across it it takes before it gives way, as a multiple of the head of water
    /// from its lowest point up to its top (a bulkhead's own, a deck's section's top), its top at least the waterline: built
    /// for the sea at its top with a margin (09 §2.4: ×1.3) [EST].</summary>
    public double FloodRatingK { get; init; } = 1.3;

    /// <summary>A sprung boundary's rating, × (1 − this × D), at least <see cref="FloodRatingSprungMin"/> (09 §2.4: 20-40%
    /// down) [EST].</summary>
    public double FloodRatingPerD { get; init; } = 0.1;
    public double FloodRatingSprungMin { get; init; } = 0.6;

    /// <summary>A sprung seam's area opens with the head: × (1 + β·Δh/rating) (17 §5.1) [EST].</summary>
    public double FloodSeamBeta { get; init; } = 1;

    /// <summary>The working of a sprung seam (17 §5.2) [EST]: a step × <see cref="FloodWorkStep"/> up to
    /// <see cref="FloodWorkMax"/>, at a hazard of <see cref="FloodWorkPerHour"/> × Δh/rating × (1 + v/<see
    /// cref="FloodWorkKn"/>)² × (1 + <see cref="FloodWorkSea"/>) an hour (the sea: ~1 moderate, 3 a gale; the weather has no
    /// sea state yet, so a moderate one).</summary>
    public double FloodWorkPerHour { get; init; } = 0.1;
    public double FloodWorkStep { get; init; } = 1.6;
    public double FloodWorkMax { get; init; } = 4;
    public double FloodWorkKn { get; init; } = 15;
    public double FloodWorkSea { get; init; } = 1;

    /// <summary>The ram head at a hole forward under way: k·v²/2g, k this at the stem falling to 0 at
    /// <see cref="FloodRamReach"/> of her length aft of it (09 §2.2: k ≈ 0.5, the most sensitive guess) [EST]; a full space
    /// holed there presses on its boundaries with <see cref="FloodRamLoad"/> of it.</summary>
    public double FloodRamK { get; init; } = 0.5;
    public double FloodRamReach { get; init; } = 0.25;
    public double FloodRamLoad { get; init; } = 0.5;

    /// <summary>Her weather deck under the sea under way (<c>Ploughing</c>, the Åland card 2026-10-26): the share of a deep
    /// plate's normal force it makes this near the free surface (a lifting surface near it loses about half) [EST], and
    /// the drag coefficient of her upper works' faces under the sea (Hoerner: a block face on ~1) [EST].</summary>
    public double PloughLiftShare { get; init; } = 0.5;
    public double UpperWorksCd { get; init; } = 1;

    /// <summary>The closures in a boundary a load loosens (damage/17 §4's fittings; door dogs, hatch dogs, cable stuffing tubes, vent
    /// closures, reach rods, shaft glands, pipe flanges, by <c>Closure</c>): the D each starts at, its chance per unit of
    /// D over that (p = min(<see cref="ClosureCap"/>, per·(D − start + ½))) and its area, log-uniform between the two, cm²
    /// [EST, checked against Houston, Chester, Helena and North Carolina's vent].</summary>
    public double[] ClosureStartD { get; init; } = [0.5, 0.5, 0.3, 0.5, 0.5, 0.3, 0.5];
    public double[] ClosurePerD { get; init; } = [0.35, 0.30, 0.12, 0.25, 0.20, 0.50, 0.25];
    public double[] ClosureMinCm2 { get; init; } = [10, 10, 0.5, 5, 1, 10, 5];
    public double[] ClosureMaxCm2 { get; init; } = [60, 50, 3, 50, 5, 100, 130];
    public double ClosureCap { get; init; } = 0.9;

    /// <summary>The repair parties' water jobs (docs/battle/damage-control.md DC3; 09 §4, by <c>RepairJob</c>: none, door,
    /// re-dog, caulk, shore, plug, patch, plating, rig a pump, mat, steering by hand, a part, a room pumped out: the last two
    /// drawn per part, <see cref="RepairPartMin"/>; a sunk deck battened): each job's man-minutes for a drilled crew (a plug's and a patch's a
    /// hole) and its crew; a party puts at most <see cref="RepairCrewsAJob"/> crews on one [EST: 09 §4's times × men].</summary>
    public double[] RepairJobManMin { get; init; } = [0, 2, 6, 60, 80, 6, 80, 60, 40, 1800, 60, 0, 0, 4];
    public double[] RepairJobMen { get; init; } = [1, 2, 2, 3, 4, 2, 4, 3, 4, 20, 4, 4, 4, 2];

    /// <summary>A deck gone under the sea, its hatches battened down (DC9), leaks this share of the Lützow rule's opening
    /// (<see cref="FloodSunkDeckM2"/>) [EST].</summary>
    public double BattenShare { get; init; } = 0.1;

    /// <summary>The safe speed (DC6; 09 §2.2): a holed space's boundaries kept under this share of their rating with the ram
    /// head on them, at least <see cref="SafeMinKn"/> (steerage); no faster than <see cref="HullWorkKn"/> while there's work
    /// on her hull's holes, which above it works loose by <see cref="HullWorkLoosen"/> every 10 min (17 §5.2, 13); no
    /// faster than <see cref="MatKn"/> while a collision mat goes over (09 §4: the ship slowed to a crawl), which reaches
    /// holes within <see cref="MatReachM"/> of the sea and leaves <see cref="MatLeft"/> of them [EST].</summary>
    public double SafeLoadShare { get; init; } = 0.8;

    /// <summary>Steering and parts (DC7; 10 §3, 06 §5.1) [EST]: by hand or steam a rudder answers at
    /// <see cref="HandRudderShare"/> of its rate, by engines within <see cref="EnginesRudderShare"/> of hard over about its
    /// jam, taken up <see cref="EnginesSteerS"/> after the steering goes when two shafts turn. A disabled part can be
    /// fixed at sea with a chance by kind (<see cref="RepairPartShare"/>: mounts, torpedo tubes, tops with their
    /// directors and rangefinders, boiler and engine rooms) in a time drawn between two (<see cref="RepairPartMin"/>, pairs,
    /// minutes, × <see cref="RepairPartMen"/> men); a room flooded out of action comes back once pumped under
    /// <see cref="RestoreShare"/> of its capacity in <see cref="RestoreMin"/> (machinery, then the rest: pairs, minutes).</summary>
    public double HandRudderShare { get; init; } = 1.0 / 3;
    public double EnginesRudderShare { get; init; } = 0.3;
    public double EnginesSteerS { get; init; } = 60;
    public double[] RepairPartShare { get; init; } = [0.3, 0.4, 0.5, 0.3];
    public double[] RepairPartMin { get; init; } = [20, 60, 20, 40, 15, 30, 30, 60];
    public double RepairPartMen { get; init; } = 4;
    public double RestoreShare { get; init; } = 0.25;
    public double[] RestoreMin { get; init; } = [30, 60, 10, 20];
    public double SafeMinKn { get; init; } = 3;
    public double HullWorkKn { get; init; } = 15;
    public double HullWorkLoosen { get; init; } = 0.15;
    public double MatKn { get; init; } = 5;
    public double MatReachM { get; init; } = 2;
    public double MatLeft { get; init; } = 0.3;

    /// <summary>DC Central's counterflooding without the conning position (DC5): by hand at the valves at this share of
    /// their rate, after this many seconds to get men there [EST].</summary>
    public double CounterfloodHandShare { get; init; } = 1.0 / 3;
    public double CounterfloodHandS { get; init; } = 180;

    /// <summary>DC Central opens no new tank while her GM is under this, m: the list is a loll, and flooding the high side
    /// would throw her over to the other (10 §2.7, Ralph Talbot) [EST].</summary>
    public double CounterfloodMinGmM { get; init; } = 0.3;

    /// <summary>A portable pump is rigged (DC4; 09 §4: 5-15 min, 3-4 men, the job's man-minutes above) where this much water
    /// stands, m³.</summary>
    public double RepairPumpMinM3 { get; init; } = 5;
    public double RepairCrewsAJob { get; init; } = 2;

    /// <summary>What a plug leaves of its holes, a patch put on with no flow and against the water coming in (09 §4; NRL's
    /// ex-Shadwell: no method reliably sealed an actively flowing rupture) [EST].</summary>
    public double RepairPlugLeft { get; init; } = 0.1;
    public double RepairPatchLeft { get; init; } = 0.08;
    public double RepairPatchFlowLeft { get; init; } = 0.3;

    /// <summary>A party works in water up to this deep (09 §4: about waist deep), m; holes this near the sea outside are
    /// worked on before they leak (the list and the roll bring them under), m [EST].</summary>
    public double RepairReachM { get; init; } = 1.2;
    public double RepairNearM { get; init; } = 1;

    /// <summary>The work's pace (09 §4): × 1 / <see cref="RepairDarkK"/> in the dark (her power under
    /// <see cref="RepairDarkPower"/>), × 1 / (1 + <see cref="RepairListPerDeg"/> × the list in degrees).</summary>
    public double RepairDarkK { get; init; } = 1.6;
    public double RepairDarkPower { get; init; } = 0.5;
    public double RepairListPerDeg { get; init; } = 0.1;

    /// <summary>A boundary is shored once its load passes this share of its rating, its job weighed as this many t/h of
    /// water × what's behind × the load [EST].</summary>
    public double RepairShoreAt { get; init; } = 0.5;
    public double RepairShoreWeightM3h { get; init; } = 500;

    /// <summary>A torpedo's shock on the boundaries of the cells it floods: D this at its reach, more nearer (as reach over
    /// the distance, at most 4.9) (docs/battle/damage-control.md DC1) [EST: damage/17 §6.1's holding bulkhead that stood,
    /// D 1-2; North Carolina's at its rivets].</summary>
    public double TorpedoSprungD { get; init; } = 2;

    /// <summary>Damage control's work on a boundary (damage/17 §5.2, 09 §4): a caulked seam keeps this share of its leak,
    /// re-dogged closures this; a set of shores raises its rating this many times (at most two sets) and halves its
    /// working [SRC 13 / EST].</summary>
    public double CaulkSeamShare { get; init; } = 0.15;
    public double RedogShare { get; init; } = 0.2;
    public double ShoreRatingK { get; init; } = 1.6;

    /// <summary>Cable stuffing tubes, one per this many m² of a boundary of a machinery, magazine, handling, steering or
    /// transmitting space (17 §4: Houston's ~5), and of any other lived-in space's (fewer cables) [EST].</summary>
    public double ClosureTubeM2 { get; init; } = 5;
    public double ClosureTubeOtherM2 { get; init; } = 20;

    /// <summary>A space this share flooded doesn't burn (nor its magazine go up).</summary>
    public double FireDrownShare { get; init; } = 0.5;

    /// <summary>What a fire does (docs/battle/fire.md B2). Heat: a space's rooms (a part itself) are harmed once more each
    /// time its fire's dose of heat passes the next of <see cref="FireHarmS"/>, seconds of developed fire over the whole
    /// space [EST: cables, phones and lighting fail within minutes over 232 °C (17 §5.2), gear and machinery is gone after a
    /// quarter of an hour at 500-800 °C overhead (Shadwell Phase VI)]; a developed fire doses by the share of the space
    /// involved, a growing, local or starved one by <see cref="FireLocalHarmShare"/> [EST]. Men: at flashover the room's
    /// share of the space's men are killed with the first of <see cref="FireFlashCaught"/> and casualties with the second
    /// [EST: a flashed-over room is untenable in seconds; a propellant seat's men are the flash's (16 §4.6)]; a party working
    /// a developed fire below decks loses its men at <see cref="RepairPartyHurtPerMin"/> a minute, without breathing sets and
    /// with them [EST: smoke and burns], <see cref="RepairPartyKilledShare"/> of them killed.</summary>
    public double[] FireHarmS { get; init; } = [180, 900];
    public double FireLocalHarmShare { get; init; } = 0.1;
    public double[] FireFlashCaught { get; init; } = [0.5, 0.8];
    public double[] RepairPartyHurtPerMin { get; init; } = [0.006, 0.002];
    public double RepairPartyKilledShare { get; init; } = 0.3;

    /// <summary>Smoke into the machinery (B2; 17 §5.4, fire_ref.py's smoke): a boiler or engine room burning, or under a
    /// space whose fire is developed or starved, is abandoned with a chance a minute of <see cref="FireSmokeFansPerMin"/>
    /// with her supply fans running and <see cref="FireSmokeSecuredPerMin"/> with them secured [EST, set to Savo], and stays
    /// out (E4's outage clock) while the smoke goes on and <see cref="FireSmokeClearS"/> after [EST].</summary>
    public double FireSmokeFansPerMin { get; init; } = 1.0 / 8;
    public double FireSmokeSecuredPerMin { get; init; } = 1.0 / 60;
    public double FireSmokeClearS { get; init; } = 600;

    /// <summary>A magazine's heat (B2; doc 10 §2.3 and §7.2 rule 12): each developed fire (or burning charges) on its
    /// boundary adds a minute's dose over the time it takes to cook off its propellant (~160-170 °C), s, by the way it
    /// comes: <see cref="FireMagazineS"/> = [a fire in it, a handling room's or burning charges beside it; an open path (an
    /// opening a burst left, its turret's trunk); a fire under it; one beside it through a bulkhead; one over it, its floor
    /// the fire's cool layer] [EST: minutes (Abner Read), 20 min, 20 min, "tens of minutes", 3 h (Astoria)], times the
    /// side's propellant. It goes up when the dose passes a limit drawn once, lognormal of σ
    /// <see cref="FireMagazineSigma"/> about 1; the dose cools away over <see cref="FireMagazineCoolS"/> without a fire. The
    /// doctrine floods one threatened for its time if the controls over it aren't in a burning or smoky space (Teresa,
    /// Astoria) and there's water (the sea's under the waterline, else the section's main), filling it in
    /// <see cref="FireMagazineFloodS"/> [EST].</summary>
    public double[] FireMagazineS { get; init; } = [300, 1200, 1200, 2400, 10800];
    public double FireMagazineSigma { get; init; } = 0.5;
    public double FireMagazineCoolS { get; init; } = 3600;
    public double FireMagazineFloodS { get; init; } = 600;

    /// <summary>A fire seen (B2; 17 §8.5): a developed or open-deck fire (or burning charges) shows, a starved one inside
    /// doesn't. At night its light is a lamp of <see cref="FireGlowCdPerMW"/> candela a MW of what shows [EST: a candle's
    /// ~1 cd from ~80 W], halfway up the ship (<see cref="FireGlowHeightShare"/> of her silhouette's top), made out at
    /// <see cref="FireGlowLux"/> on the eye (Allard's law, IALA's night threshold [SRC]) through the haze (3.912 / the
    /// visibility), the threshold ×100 at sunset as the light's reach has it.</summary>
    public double FireGlowCdPerMW { get; init; } = 1e4;
    public double FireGlowLux { get; init; } = 2e-7;

    /// <summary>The sky the night threshold <see cref="FireGlowLux"/> is for, cd/m² (a clear moonless night's): against a
    /// brighter sky a light must be brighter, as √(1 + L / this) (the eye's square root law; docs/battle/night.md Q1).</summary>
    public double FireGlowSkyCdM2 { get; init; } = 3e-4;
    public double FireGlowHeightShare { get; init; } = 0.5;

    /// <summary>One space's fire (docs/battle/fire.md B0; damage/17 §3-4, fire_ref.py). Growth: Q = seat + α·t², α the
    /// "medium" t² fire (1 MW at 300 s: Eurocode EN 1991-1-2 E.5 [SRC]) times the outfit's growth and the use's (by
    /// FireUse: none, quarters, cabins, offices, stores ×2 "fast", machinery, deck). The room it flashes over in, m² by
    /// use (0: the whole space) [EST: a cabin ~12, an office ~20, a messdeck up to ~90]; developed, the next room joins
    /// every <see cref="FireRoomToRoomS"/> (an open path, 1-3 min: 17 §5.1). Openings: leakage, a door's A√H (0.66 × 1.68
    /// m: 1.44 m^2.5) and the doors a part has (a cell's are its readiness's, B1). Air: 1.5 MW per m^2.5
    /// (Kawagoe [SRC]); fuel: 0.25 MW/m² (Eurocode [SRC]) ramped from 230 MJ/m² over 420 (DTIC bands [FIT]); flashover:
    /// Thomas × 0.65 (NBS berthing 0.67 MW [FIT]). A load under the minimum doesn't light; local fires die after
    /// <see cref="FireLocalS"/> or a share of their room's load; starved ones smoulder out after <see cref="FireStarvedS"/>;
    /// decay on the last share of the fuel, out under the smoulder's heat; an open deck sustains a fire over its load
    /// [EST], its burning area ~0.1 MW a m² [EST]; a space burnt to the share left doesn't light again; one that burned
    /// developed longer than <see cref="FireHotS"/> goes out hot.</summary>
    public double FireAlphaMWs2 { get; init; } = 0.01172e-3;
    public double[] FireUseGrowth { get; init; } = [1, 1, 1, 1, 2, 1, 1];
    public double[] FireRoomM2 { get; init; } = [0, 90, 12, 20, 60, 0, 0];
    public double FireRoomToRoomS { get; init; } = 120;
    public double FireLeakAoh { get; init; } = 0.05;
    public double FireDoorAoh { get; init; } = 1.44;
    public double FirePartDoors { get; init; } = 2;
    public double FireVentMW { get; init; } = 1.5;
    public double FireFuelMWm2 { get; init; } = 0.25;
    public double FireFuelFloorMJm2 { get; init; } = 230;
    public double FireFuelSpanMJm2 { get; init; } = 420;
    public double FireThomasShare { get; init; } = 0.65;
    public double FireMinLoadMJm2 { get; init; } = 60;
    public double FireLocalS { get; init; } = 1200;
    public double FireLocalShare { get; init; } = 0.3;
    public double FireStarvedS { get; init; } = 1800;
    public double FireSmoulderMW { get; init; } = 0.02;
    public double FireDecayShare { get; init; } = 0.2;
    public double FireOpenSustainMJm2 { get; init; } = 300;
    public double FireOpenMWm2 { get; init; } = 0.1;
    public double FireBurntShare { get; init; } = 0.05;
    public double FireHotS { get; init; } = 600;

    /// <summary>Spread (docs/battle/fire.md B1; damage/17 §5, fire_ref.py's spread), from a developed fire over
    /// <see cref="FireSpreadMinMW"/>: through an open path (the same room, an opening a burst left of at least
    /// <see cref="FireOpenPathM2"/>, open doors through a bulkhead over the waterline, a part's openings onto its deck) a
    /// chance a minute times Q over <see cref="FireOpenPathMW"/> (at most 1), doubled when the fuel could feed half as much
    /// again as the air lets burn (flames out of the openings: Shadwell [EST]); through steel after a delay from flashover
    /// (s), then a chance a minute: up 8 min then 1/15, sideways 12 then 1/40, down 40 then 1/180 [FIT to Shadwell Ph V/VI
    /// and USCG A-0], times the neighbour's load over 400 MJ/m² (0.3 to 1.5) and ×1.5 under heavy paint (60 MJ/m² or more);
    /// along the weather deck to the next section 1/6 a minute times its load over 300 (at most 1.5) [EST], ×0.3 wetted. A
    /// weather deck sustains a fire over <see cref="FireOpenSustainMJm2"/> of its load, ×0.6 wetted. Each lights its
    /// neighbour with a seat of <see cref="FireSpreadSeatMW"/>. A burst's torn bulkheads let a fire breathe as an opening of
    /// <see cref="FireTornM2"/>.</summary>
    public double FireSpreadMinMW { get; init; } = 0.3;
    public double FireSpreadSeatMW { get; init; } = 0.3;
    public double FireOpenPathM2 { get; init; } = 0.5;
    public double FireOpenPathPerMin { get; init; } = 0.5;
    public double FireOpenPathMW { get; init; } = 2;
    public double FireFlamesOutRatio { get; init; } = 1.5;
    public double FireFlamesOutK { get; init; } = 2;
    /// <summary>Through steel, by way (up, sideways, down): the delay from flashover, s, and the chance a minute after.</summary>
    public double[] FireSteelDelayS { get; init; } = [480, 720, 2400];
    public double[] FireSteelPerMin { get; init; } = [1.0 / 15, 1.0 / 40, 1.0 / 180];
    public double FireSteelLoadMJm2 { get; init; } = 400;
    public double FireSteelLoadMin { get; init; } = 0.3;
    public double FireSteelLoadMax { get; init; } = 1.5;
    public double FireHeavyPaintMJm2 { get; init; } = 60;
    public double FireHeavyPaintK { get; init; } = 1.5;
    public double FireDeckPerMin { get; init; } = 1.0 / 6;
    public double FireDeckLoadMJm2 { get; init; } = 300;
    public double FireDeckLoadMax { get; init; } = 1.5;
    public double FireWetSustain { get; init; } = 0.6;
    public double FireWetSpread { get; init; } = 0.3;
    public double FireTornM2 { get; init; } = 0.2;

    /// <summary>Reflash (17 §4: Lion's Q turret flashed 28 min after it was thought out; Samuel B. Roberts): a space that
    /// burned developed longer than <see cref="FireHotS"/> and went out holds its heat for <see cref="FireReflashS"/>, with
    /// a chance a minute of lighting again from a seat of <see cref="FireReflashSeatMW"/> [EST: 1/20 min, ×0.1 with a
    /// reflash watch, B4].</summary>
    public double FireReflashPerMin { get; init; } = 0.05;
    public double FireReflashS { get; init; } = 1800;
    public double FireReflashSeatMW { get; init; } = 0.2;

    /// <summary>A propellant seat (ready charges, charges in a gunhouse): its charges' heat, MJ/kg (cordite and nitrocellulose
    /// ~4-5 MJ/kg [SRC: heats of explosion]), released over <see cref="FirePropellantBurnS"/> [EST: charges burn in seconds,
    /// a battery's in a few minutes, Malaya's ~5 min with what they lit], carrying its own oxygen: no limit of air or the
    /// space's fuel.</summary>
    public double FirePropellantMJkg { get; init; } = 4.5;
    public double FirePropellantBurnS { get; init; } = 30;

    /// <summary>Water on a fire (docs/battle/fire.md B3; damage/17 §6.1, fire_ref.py's attack): a fire needs
    /// <see cref="FireWaterLm2"/> L/min of fog-equivalent flow per m² involved (Grimwood, the Iowa formula [SRC]), at least
    /// <see cref="FireWaterMinLpm"/>. Given that, it's knocked down with τ = <see cref="FireKnockS"/> × need / flow [FIT to
    /// Shadwell's fog attack, 2 min], under control once its heat is under 0.05 MW plus 0.002 MW a m² involved, then dies
    /// down with τ <see cref="FireControlS"/> while it's mopped up, out after <see cref="FireMopLm2"/> L a m² of the most it
    /// involved (Särdqvist [SRC]); water stopped before then rekindles it with this share. Short of the need it's held: a
    /// growing fire at (1 − held)² of its growth, a developed one at 1 − 0.7·held of its heat, a local or starved one at
    /// 1 − held. Water boils at <see cref="FireWaterMJkg"/> MJ/kg from the sea's temperature to steam (17 §6.5): at most
    /// the fire's heat over that, and at most the nozzle's share of what it throws; the rest stays aboard.</summary>
    public double FireWaterLm2 { get; init; } = 4;
    public double FireWaterMinLpm { get; init; } = 40;
    public double FireKnockS { get; init; } = 90;
    public double FireControlMW { get; init; } = 0.05;
    public double FireControlMWm2 { get; init; } = 0.002;
    public double FireControlS { get; init; } = 60;
    public double FireMopLm2 { get; init; } = 60;
    public double FireRekindleShare { get; init; } = 0.3;
    public double FireHeldDeveloped { get; init; } = 0.7;
    public double FireWaterMJkg { get; init; } = 2.6;

    /// <summary>Nozzles (17 §6.1): the share of a nozzle's flow that does the work of fog (a straight jet ~10% of its water
    /// cooling against fog's 30-60% [SRC Gałaj; fog UNCERTAIN]: a third; the USN's 1942 all-purpose nozzle half [EST]),
    /// and the most of its water a fire can boil (jet 0.15, all-purpose 0.4, fog 0.8 [EST]).</summary>
    public FireNozzle[] FireNozzles { get; init; } =
    [
        new() { Name = "jet", FogShare = 1.0 / 3, BoilShare = 0.15 },
        new() { Name = "all-purpose", FogShare = 0.5, BoilShare = 0.4 },
        new() { Name = "fog", FogShare = 1, BoilShare = 0.8 },
    ];

    /// <summary>The orders a ship goes into action with (docs/battle/fire.md B1): stripped of what burns or as fitted
    /// (the outfit's two loads), the open doors a cell has (the material condition: 0 closed up), and the weather decks
    /// wetted. A side takes one by name (the scenario's "readiness"; <see cref="ReadinessDefault"/> else): unprepared
    /// (the Chinese at the Yalu, the Spanish at Santiago), prepared (stripped, hoses out, decks wetted, doors open: the
    /// Japanese in 1894), closed up (the RN and the Germans by 1916, the USN in WWII: 17 §8.3); prepared, cluttered (the
    /// Russians at Tsushima after the voyage round Africa: stripped below, but coal, stores, hammocks and timber on the upper
    /// decks, 17 §2.1; fire.md B5), its deck load on the weather decks over the outfit's [EST: enough to sustain a wetted
    /// deck fire, 17 §5.3].</summary>
    public Readiness[] Readiness { get; init; } =
    [
        new() { Name = "unprepared", Stripped = false, CellDoors = 1, WetDecks = false, WtDoorsOpen = 0.5 },
        new() { Name = "prepared", Stripped = true, CellDoors = 1, WetDecks = true, WtDoorsOpen = 0.1 },
        new() { Name = "closed up", Stripped = true, CellDoors = 0, WetDecks = true, WtDoorsOpen = 0.02 },
        new() { Name = "prepared, cluttered", Stripped = true, CellDoors = 1, WetDecks = true, DeckLoadMJm2 = 300, WtDoorsOpen = 0.1 },
    ];
    public string ReadinessDefault { get; init; } = "prepared";

    /// <summary>Firefighting (docs/battle/fire.md B4; damage/17 §6, §8.3), rough on purpose (decision 3): a side's doctrine
    /// by name (the scenario's "damage_control", else <see cref="DamageControlDefault"/>; 17 §8.3's table, fire_ref.py's
    /// DOCTRINES), the parties re-planned every <see cref="FirePlanS"/>, each going <see cref="FireTravelS"/> a section to
    /// the worst fires (heat, ×<see cref="FireMagazineK"/> next to a magazine, at most <see cref="FirePartiesAFire"/> on one),
    /// its men's flow at <see cref="FireRotation"/> for the reliefs and ×<see cref="FireSmokeShare"/> from the doorway of a
    /// smoke-filled space below without breathing sets; the ship has between <see cref="FirePartiesMin"/> and
    /// <see cref="FirePartiesMax"/> of them. A section without its main fights with buckets and extinguishers at
    /// <see cref="FireDryShare"/> of the flow, one cut and rigged again at <see cref="FireJumperShare"/>; a hit in the
    /// section breaks the main with the doctrine's chance × (<see cref="FireMainRefT"/> t / displacement)^
    /// <see cref="FireMainExp"/> [FIT to 17 §6.4's ×1 destroyer, ×0.5 cruiser, ×0.35 battleship], stops a party working
    /// there for <see cref="FireHitStopS"/> with the doctrine's hose-cut chance (its men lost are E6's, where it stands:
    /// docs/battle/crew.md N1). <see cref="FireTurnToS"/> after the last hit the gun crews turn to as the doctrine's extra
    /// parties at <see cref="FireTurnToSkill"/> (doc 10 §5), from the mounts' men.</summary>
    public DamageControlDoctrine[] DamageControl { get; init; } =
    [
        new() { Name = "1890", HoldSpeedS = 3600, CounterfloodDeg = -1, CounterfloodTrimDeg = -1, Skill = 0.4, WatchS = -1, HoseLpm = 600, Nozzle = "jet", MusterS = 360, ManLpm = 100, PartyShare = 0.85, PartyMen = 16,
            MainBreak = 0.25, HoseCut = 0.30, Looped = false, RestoreS = -1, PumpFloor = 0, BreathingSets = false, TurnTo = 1, FloodMagazineS = 600 },
        new() { Name = "1905", HoldSpeedS = 1800, CounterfloodDeg = 4, CounterfloodTrimDeg = -1, Skill = 0.6, WatchS = 120, HoseLpm = 600, Nozzle = "jet", MusterS = 180, ManLpm = 100, PartyShare = 1.0, PartyMen = 16,
            MainBreak = 0.20, HoseCut = 0.35, Looped = false, RestoreS = 900, PumpFloor = 0, BreathingSets = false, TurnTo = 2, FloodMagazineS = 300 },
        new() { Name = "1916", HoldSpeedS = 900, CounterfloodDeg = 2, CounterfloodTrimDeg = 1, Skill = 0.85, WatchS = 180, HoseLpm = 420, Nozzle = "jet", MusterS = 240, ManLpm = 84, PartyShare = 1.0, PartyMen = 16,
            MainBreak = 0.10, HoseCut = 0.15, Looped = false, RestoreS = 900, PumpFloor = 0, BreathingSets = false, TurnTo = 2, FloodMagazineS = 180 },
        new() { Name = "1916 BCF", HoldSpeedS = 900, CounterfloodDeg = 2, CounterfloodTrimDeg = 1, Skill = 0.85, WatchS = 180, HoseLpm = 420, Nozzle = "jet", MusterS = 240, ManLpm = 84,
            PartyShare = 1.0, PartyMen = 16, MainBreak = 0.10, HoseCut = 0.15, Looped = false, RestoreS = 900, PumpFloor = 0, BreathingSets = false, TurnTo = 2,
            FloodMagazineS = 180, Flash = "rate first" },
        new() { Name = "1942", HoldSpeedS = 600, CounterfloodDeg = 1, CounterfloodTrimDeg = 1, Skill = 0.9, WatchS = 120, HoseLpm = 250, Nozzle = "all-purpose", MusterS = 180, ManLpm = 62.5, PartyShare = 1.0, PartyMen = 16,
            MainBreak = 0.25, HoseCut = 0.20, Looped = false, RestoreS = 480, PumpFloor = 0, BreathingSets = false, TurnTo = 2, FloodMagazineS = 180 },
        new() { Name = "1944", HoldSpeedS = 600, CounterfloodDeg = 1, CounterfloodTrimDeg = 1, Skill = 1.0, WatchS = 60, HoseLpm = 360, Nozzle = "fog", MusterS = 120, ManLpm = 90, PartyShare = 1.0, PartyMen = 16,
            MainBreak = 0.15, HoseCut = 0.20, Looped = true, RestoreS = 240, PumpFloor = 0.6, BreathingSets = true, TurnTo = 3,
            CoolBoundaries = true, ReflashWatch = true, FloodMagazineS = 120 },
    ];
    public string DamageControlDefault { get; init; } = "1916";
    public double FirePlanS { get; init; } = 30;
    public double FireTravelS { get; init; } = 30;
    public double FireMagazineK { get; init; } = 3;
    public int FirePartiesAFire { get; init; } = 2;
    public int FirePartiesMin { get; init; } = 2;
    public int FirePartiesMax { get; init; } = 8;
    public double FireRotation { get; init; } = 0.8;
    public double FireSmokeShare { get; init; } = 0.5;
    public double FireDryShare { get; init; } = 0.15;
    public double FireJumperShare { get; init; } = 0.5;
    public double FireMainRefT { get; init; } = 2000;
    public double FireMainExp { get; init; } = 0.4;
    public double FireHitStopS { get; init; } = 120;
    public double FireTurnToS { get; init; } = 900;
    public double FireTurnToSkill { get; init; } = 0.7;
    /// <summary>Boundary cooling cuts the spread through steel from a fire its parties have the water for to this share
    /// (17 §5.2: NRL Phase VI); a reflash watch the chance of lighting again to this share (17 §4).</summary>
    public double FireCoolShare { get; init; } = 0.3;
    public double FireReflashWatchShare { get; init; } = 0.1;

    /// <summary>What an observer makes out of a ship (a contact's <c>Seen</c>): low in the water (settled by this share of
    /// its freeboard), a list, a trim (degrees), stopped (knots).</summary>
    public double SeenLowShare { get; init; } = 0.3;
    public double SeenListDeg { get; init; } = 3;
    public double SeenTrimDeg { get; init; } = 1;
    public double SeenStoppedKn { get; init; } = 1;

    /// <summary>Silent as seen (docs/battle/crew.md N5b): a ship showing damage whose guns haven't been seen firing for this
    /// long, s [EST: past a heavy battery's slowest salvos and a ranging wait]. Seen done for (<c>Contact.DoneFor</c>):
    /// burning, silent a while longer, and as near stopped as her speed is under this, kn [EST]; at will a side ceasing on
    /// wrecks takes none this far done for.</summary>
    public double SeenSilentS { get; init; } = 180;
    public double DoneForKn { get; init; } = 4;
    public double DoneForCease { get; init; } = 0.5;

    // ------------------------------------------------------------------ torpedoes (docs/battle/fighting.md 4.6)

    /// <summary>Torpedoes running at once (the pool grows past it), and the ring of their ends the screens read.</summary>
    public int TorpedoCapacity { get; init; } = 256;
    public int TorpedoEndCapacity { get; init; } = 256;

    /// <summary>A torpedo's run is checked against the ships near it in pieces this long, ms (each a swept segment, so a
    /// hit doesn't depend on it); it can't hit in its first metres (arming).</summary>
    public int TorpedoCheckMs { get; init; } = 2000;
    public double TorpedoArmM { get; init; } = 200;

    /// <summary>Aiming a spread (a fixed launcher fires within its torpedo's gyro angle of its own bearing, the design's
    /// torpedo block's): each torpedo's own course error (1 sigma, degrees), the spacing between torpedoes as a share of
    /// the target's length at the meeting point, and the depth set as a share of the target's draught as it's taken
    /// (within the two depths, m).</summary>
    public double TorpedoCourseSigmaDeg { get; init; } = 1;
    public double TorpedoSpacingShare { get; init; } = 0.6;
    public double TorpedoDepthShare { get; init; } = 0.6;
    public double TorpedoDepthMinM { get; init; } = 1.5;
    public double TorpedoDepthMaxM { get; init; } = 4.5;

    /// <summary>The explosion at the hull (damage-model-research.md §3.4, A6): how far inboard it wrecks, m per kg^⅓ of its
    /// charge (TNT's worth; a 300 kg charge 4.7 m, the depth of a TDS rated for it), and the hole it tears in the side,
    /// m² per kg^⅔ (Seydlitz's 12 × 4 m from a ~200 kg charge). Each other cell within the reach takes a hole of its own
    /// of this share of it, less by its distance; machinery spaces within this many reaches may trip from the shock (the
    /// chance), the nearest magazine within the reach is lit as by a burst among its charges (MagazineFlashChance, fading
    /// to none over the reach's outer half; docs/battle/magazines.md V7), and this share of the battle crew in the spaces it floods is lost.</summary>
    public double TorpedoReachK { get; init; } = 0.7;
    public double TorpedoHoleK { get; init; } = 1.4;
    public double TorpedoInnerHoleShare { get; init; } = 0.5;
    public double TorpedoShockShare { get; init; } = 2;
    public double TorpedoShockChance { get; init; } = 0.5;
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
    /// <summary>A unit's worked-out role (docs/battle/admirals-orders.md O7c): a ship whose torpedoes are her better weapon
    /// against her side's heaviest class is a boat (toward a flotilla) unless she's this many times the typical boat's
    /// displacement or more (the most numerous such class at sea): then she scouts. At the Jutland the boats run to 2,490 t
    /// (B97) against an M-class, the cruisers from 3,050 t; relative, so it holds in any era.</summary>
    public double RoleScoutSizeRatio { get; init; } = 1.5;
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

    // ------------------------------------------------------------------ the smoke field (docs/battle/smoke.md Z1)

    /// <summary>The smoke field (docs/battle/smoke.md): on since the user took it in (2026-10-21, decision 12).</summary>
    public bool Smoke { get; init; } = true;

    /// <summary>The field's tick in the fine tier and in the coarse, ms: puffs laid, the air carried on, trails merged, on a
    /// lattice of world time (the same at any speed within a tier; the coarse is cheaper, docs/battle/smoke.md principles).</summary>
    public int SmokeTickMs { get; init; } = 1000;
    public int SmokeCoarseTickMs { get; init; } = 60_000;

    /// <summary>The coarse tier's young steps (docs/battle/coarse.md J5): every <see cref="SmokeYoungMs"/> between its
    /// ticks, each ship's smoke since her last laid and the index's young part filed again, so her newest smoke stands on
    /// the lines as in fine (6 s let the haze duel's rounds drift out of parity).</summary>
    public int SmokeYoungMs { get; init; } = 3_000;

    /// <summary>A puff's spread at birth, m (the plume after the funnels' lee) [EST]; the most of a ship's run through the
    /// air one puff stands for, m: a tick's run is cut into puffs this long or shorter, and a longer one (the coarse tier's)
    /// widens them along it; the most puffs a ship lays a tick.</summary>
    public double SmokeSigma0M { get; init; } = 15;
    public double SmokeSpacingM { get; init; } = 40;
    public int SmokePuffsPerTick { get; init; } = 8;
    /// <summary>The longest a ship's funnels' smoke waits to be laid, ms: a piece is laid once she's run
    /// <see cref="SmokeSpacingM"/> through the air, or this long has passed (docs/battle/smoke.md Z5b).</summary>
    public int SmokeLayMaxMs { get; init; } = 4000;

    /// <summary>How often a ship's plume (output, rise, downwash) is worked out again, ms of world time: a boiler's output
    /// follows the power over tens of seconds.</summary>
    public int SmokePlumeMs { get; init; } = 10_000;

    /// <summary>A ship coming off rails lays her trail back this far, s, as if she'd smoked all along (sight only reads
    /// smoke off rails; her smoke from before is in the air all the same) [EST: a plume's useful life].</summary>
    public double SmokeBackfillS { get; init; } = 1800;

    /// <summary>Every puff's smoke fades as e^(−age / this), s (0: it lasts until it's spread too thin): shorter than the
    /// physics, for the cost (the user, 2026-10-21: drifting old smoke serves little tactical purpose; docs/battle/smoke.md
    /// "Shorter lived").</summary>
    public double SmokeLifeS { get; init; } = 600;

    /// <summary>Merging (research/vfx/unified-smoke-system.md "Lifecycle"; docs/battle/smoke.md Z5b): a trail's puffs
    /// merge once both are older than <see cref="SmokeSettleS"/>, s, and their centres are within
    /// <see cref="SmokeMergeSigma"/> of the larger σ along the line between them (Gaussians 2σ apart along a line ripple
    /// ~1.4%, 3σ apart ~22%); each trail is walked every <see cref="SmokeMergeEveryMs"/> of world time (a slice of the
    /// trails each tick). The merged ellipse's long σ at most <see cref="SmokeMergeStretch"/> times its short, and at most
    /// <see cref="SmokeMergeLongM"/>, m, unless it's at most twice its short (the large eddies bend a long one, Z5c).
    /// Across trails: every <see cref="SmokeCrossEveryMs"/>, the puffs over <see cref="SmokeCrossAgeS"/>, s.</summary>
    public double SmokeSettleS { get; init; } = 20;
    public double SmokeMergeSigma { get; init; } = 2.0;
    public int SmokeMergeEveryMs { get; init; } = 30_000;
    public double SmokeMergeStretch { get; init; } = 4;
    public double SmokeMergeLongM { get; init; } = 300;
    public int SmokeCrossEveryMs { get; init; } = 60_000;
    public double SmokeCrossAgeS { get; init; } = 120;

    /// <summary>The large eddies (docs/battle/smoke.md Z5c, <c>SmokeEddies</c>): wavelengths from
    /// <see cref="SmokeEddyShortM"/> to <see cref="SmokeEddyLongM"/>, m, each mode's strain (its displacement's slope)
    /// in neutral 8 m/s air <see cref="SmokeEddyStrain"/> (0: none), each turning over in its wavelength at
    /// <see cref="SmokeEddySpeedMs"/>, m/s [EST: ~300 m of meander at saturation, ~0.4 m/s of it; research requested].</summary>
    public double SmokeEddyShortM { get; init; } = 1500;
    public double SmokeEddyLongM { get; init; } = 12_000;
    public double SmokeEddyStrain { get; init; } = 0.1;
    public double SmokeEddySpeedMs { get; init; } = 2;

    /// <summary>The most puffs the field holds: over it, trails merge twice as far apart (the cost degrades in accuracy,
    /// never in time).</summary>
    public int SmokeCap { get; init; } = 262_144;

    /// <summary>The sight lines' index (docs/battle/smoke.md Z2): rebuilt every <see cref="SmokeIndexMs"/> of world time
    /// (each puff's state then), in chunks of <see cref="SmokeChunk"/> puffs along a trail.</summary>
    public int SmokeIndexMs { get; init; } = 15_000;

    /// <summary>The index's young part (docs/battle/smoke.md T2): every <see cref="SmokeFreshMs"/> of world time the puffs
    /// laid since the last rebuild, and those under <see cref="SmokeFreshS"/> old at it, are filed again as they are then
    /// (the full rebuild leaves them out), so new smoke reaches the lines sooner.</summary>
    public int SmokeFreshMs { get; init; } = 3_000;
    public double SmokeFreshS { get; init; } = 30;
    public int SmokeChunk { get; init; } = 4;

    /// <summary>The index's grid: cubes this big, m, in the air's frame (Earth-centred).</summary>
    public double SmokeGridM { get; init; } = 2_000;

    /// <summary>A pair's optical depths hold for <see cref="SmokeRefreshMs"/> of world time; a pair no one has asked for in
    /// <see cref="SmokePairKeepMs"/> is let go.</summary>
    public int SmokeRefreshMs { get; init; } = 15_000;

    /// <summary>A pair's lines (from each eye's place, to the middles of the target's thirds) are worked out one by one for
    /// a puff where they've spread apart at its place by more than this share of its width; else the one line between
    /// the ships' middles (docs/battle/smoke.md, the tuning pass).</summary>
    public double SmokeLineSplit { get; init; } = 0.25;
    public int SmokePairKeepMs { get; init; } = 60_000;

    /// <summary>A target's bands, as shares of her silhouette's top: the heights lines from each eye end at (hull, upper
    /// works, tops) [placeholder: Z3 sets them from the silhouette's area table].</summary>
    public double[] SmokeBands { get; init; } = [0.2, 0.5, 0.85];

    /// <summary>A ship making smoke (docs/battle/smoke.md Z4, <c>SmokeSystem.MakeSmoke</c>) burns her fuel rich: this much
    /// smoke a kg of it, m², in place of her plant's own when it's less (some 8% of the fuel as soot at 7.5 m²/g) [EST:
    /// research request 3, how fast destroyers laid funnel smoke].</summary>
    public double SmokeScreenM2PerKg { get; init; } = 600;

    /// <summary>The firing state (docs/battle/smoke.md T3, <c>SmokeSources.Forcing</c>): a fire pushed past what it burns
    /// cleanly makes black smoke, from <see cref="SmokeForcingFrom"/> of the rated power, as the square of the load over
    /// it, to <see cref="SmokeForcedFactor"/> times well fired's at the rated power [EST: the research's inferred 1,500
    /// m²/s for a coal battleship at full power asks some 4-7 times well fired's; no record yet, request 1. Set to 2 by
    /// the user's eye for gameplay (2026-10-22, "prune it back a bit": 4 blinded too much), docs/battle/smoke.md T4].</summary>
    public double SmokeForcingFrom { get; init; } = 0.5;
    public double SmokeForcedFactor { get; init; } = 2;

    /// <summary>The smoke's wind wanders and gusts about the weather's (docs/battle/smoke.md): its way by up to so many
    /// degrees over some so many seconds, its speed by a share as slowly and by another in gusts of some so many seconds
    /// (smooth signals of the time, drawn from the world's seed) [EST: mesoscale meander and gusts over the sea].</summary>
    public double SmokeWindWanderDeg { get; init; } = 12;
    public double SmokeWindWanderS { get; init; } = 1200;
    public double SmokeWindSpeedShare { get; init; } = 0.1;
    public double SmokeGustShare { get; init; } = 0.15;
    public double SmokeGustS { get; init; } = 90;

    /// <summary>How often the field is copied for the drawing, ms of real time (docs/battle/smoke.md Z5: the drawing's
    /// cadence, never world state; between copies it works each puff out at its own frame's time).</summary>
    public int SmokeCopyMs { get; init; } = 250;

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

/// <summary>A side's men's experience (docs/battle/crew.md decision 6), 0 to 3: their years at sea and their battles, a
/// scenario's to give (trained by default). It goes with them to another ship; a ship's work-up (0-1) is hers. The names
/// are presets on the whole numbers.</summary>
public static class Experience
{
    public const double Green = 0, Average = 1, Trained = 2, Elite = 3, Max = 3;

    public static readonly string[] Names = ["green", "average", "trained", "elite"];

    /// <summary>A name ("trained", any case) or a number from 0 to 3.</summary>
    public static bool TryParse(string? text, out double experience)
    {
        int k = Array.FindIndex(Names, n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
        if (k >= 0)
        {
            experience = k;
            return true;
        }
        return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out experience)
            && experience is >= 0 and <= Max;
    }

    /// <summary>Its name on a whole number, else the number ("1.5").</summary>
    public static string Name(double experience) =>
        experience == Math.Floor(experience) && experience is >= 0 and <= Max ? Names[(int)experience]
            : experience.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>A fire discipline (docs/battle/fire-control.md F6), the side's doctrine for finding the bracket and hitting:
/// <list type="bullet">
/// <item>ranging, how many salvos may be in the air, unspotted, at once (1: each waits for the last one's fall); with a
/// ladder step (m), each group of that many goes at once, the salvos stepped about the bracket's aim so their falls
/// bracket the target in one flight; without, they roll (the next goes when one is spotted);</item>
/// <item>a partial salvo's share of the guns that bear, at least a number of them (enough to spot a wild shot by:
/// navalgazing Spotting, 3 and better 4; all of them if fewer);</item>
/// <item>fire for effect in alternating halves (each half as it loads, so the spotter has falls twice as often) or full
/// salvos; and how many spots in a row without a straddle send it back to ranging;</item>
/// <item>the rate spot: when two spots in a row move the aim the same way, the range rate is corrected too, by this share
/// of the move a minute (the RN's "down 200, rate down 100", computing.md §2.5; 0 for none: the USN's manual held
/// spotting during rate corrections the same as pyramiding);</item>
/// <item>independent fire: inside this range (m; 0 never) a quick-firing battery (<see cref="SimSettings.QuickFirerReloadS"/>)
/// fires each mount as it loads and lays, each fall spotted on its own (whose splash it is: F7), salvo fire beyond.</item>
/// </list></summary>
public sealed record FireDiscipline
{
    public string Name { get; init; } = "";
    public int InFlight { get; init; } = 1;
    public double LadderStepM { get; init; }
    public double GunsShare { get; init; } = 0.5;
    public int GunsMin { get; init; } = 4;
    public bool EffectHalves { get; init; }
    public int MissesToRange { get; init; } = 3;
    public double RateSpotShare { get; init; }
    public double IndependentWithinM { get; init; }
}

/// <summary>A nozzle (docs/battle/fire.md B3; <see cref="SimSettings.FireNozzles"/>).</summary>
public readonly record struct FireNozzle
{
    public string Name { get; init; }
    public double FogShare { get; init; }
    public double BoilShare { get; init; }
}

/// <summary>A side's firefighting doctrine (docs/battle/fire.md B4; <see cref="SimSettings.DamageControl"/>): the fire watch's
/// delay to first water on a new fire (s; under 0: none) and its hose's flow (L/min); the nozzle (a name of FireNozzles); the
/// parties' muster (s), each man's flow (L/min thrown, men a hose and the hose's flow folded in), the share of her repair
/// men (the battle station's, in her quarters and stores) who muster into them (docs/battle/crew.md N1) and a party's men; the main's break chance a hit, looped or not, the time to isolate a break and rig past it (s;
/// under 0: never), the pumps' floor with the steam gone (a diesel pump's share); breathing sets; the extra parties that
/// turn to after the action; and the habits: cooling the boundaries, a reflash watch.</summary>
public readonly record struct DamageControlDoctrine
{
    public string Name { get; init; }
    public double WatchS { get; init; }
    public double HoseLpm { get; init; }
    public string Nozzle { get; init; }
    public double MusterS { get; init; }
    public double ManLpm { get; init; }
    public double PartyShare { get; init; }
    public int PartyMen { get; init; }
    public double MainBreak { get; init; }
    public double HoseCut { get; init; }
    public bool Looped { get; init; }
    public double RestoreS { get; init; }
    public double PumpFloor { get; init; }
    public bool BreathingSets { get; init; }
    public int TurnTo { get; init; }
    public bool CoolBoundaries { get; init; }
    public bool ReflashWatch { get; init; }
    /// <summary>How long a magazine is threatened before it's flooded, s (B2; -1: never).</summary>
    public double FloodMagazineS { get; init; }
    /// <summary>Her parties' pace at water jobs (docs/battle/damage-control.md DC3; 09 §5.3's dc_skill, 0.4-1), and whether
    /// the doctrine puts the water before fires next to magazines (10 §5.1: fire first).</summary>
    public double Skill { get; init; }
    public bool FloodFirst { get; init; }
    /// <summary>DC Central counterfloods past this list, degrees (-1: never; DC5), and past this trim (-1: never)
    /// [EST: NC took 5.5° off at once; the German navy trimmed, the RN rarely].</summary>
    public double CounterfloodDeg { get; init; }
    public double CounterfloodTrimDeg { get; init; }
    /// <summary>How long her captain holds his speed once damage control asks for less, s (DC6, decision 2; -1: he never
    /// slows for it) [EST: the German navy slowed at once (Seydlitz, Lützow), the RN held the line longer].</summary>
    public double HoldSpeedS { get; init; }
    /// <summary>Its flash discipline, a name of <see cref="SimSettings.FlashDisciplines"/> (docs/battle/magazines.md V5;
    /// none: drill).</summary>
    public string? Flash { get; init; }
}

/// <summary>A ship's orders before action for fire (docs/battle/fire.md B1; <see cref="SimSettings.Readiness"/>).</summary>
public readonly record struct Readiness
{
    public string Name { get; init; }
    public bool Stripped { get; init; }
    public double CellDoors { get; init; }
    public bool WetDecks { get; init; }
    /// <summary>The supply fans secured (B2): smoke kept out of the machinery.</summary>
    public bool FansSecured { get; init; }
    /// <summary>What's piled on her weather decks over the outfit's load, MJ/m² (B5's deck clutter).</summary>
    public double DeckLoadMJm2 { get; init; }
    /// <summary>The share of her watertight doors left open in action (docs/battle/damage-control.md DC1, decision 4: in
    /// use, or by poor discipline; fire's <see cref="CellDoors"/> are the upper hull's other doors).</summary>
    public double WtDoorsOpen { get; init; }
}

/// <summary>A gunnery level's multipliers on the rangetakers' and the layers' errors (a struct: read between the rows on
/// every salvo without garbage).</summary>
/// <summary>The men's nerve at an experience (<see cref="SimSettings.Nerves"/>; docs/battle/crew.md N3b).</summary>
public readonly record struct NerveRow
{
    public NerveRow() { }

    public string Name { get; init; } = "";
    public double Nerve { get; init; }
    public double GoingOpen { get; init; }
    public double GoingEnclosed { get; init; }
    public double BackS { get; init; }
    public double Fade { get; init; } = 1;
}

public readonly record struct GunneryCrew
{
    public GunneryCrew() { }

    public string Name { get; init; } = "";
    public double Operator { get; init; }
    public double Lay { get; init; }
    public double Pyramid { get; init; }
    public double Load { get; init; } = 1;
}

[JsonSourceGenerationOptions(WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(SimSettings))]
internal partial class SimSettingsJson : JsonSerializerContext;

/// <summary>A flash discipline (<see cref="SimSettings.FlashDisciplines"/>; docs/battle/magazines.md V5): the share of the
/// time each door of an ammunition train stands open, by the station above it; the charges lying at a station, a gun; the
/// share of a magazine's stow with its lids off; the hoists' sustained reload over the drill reload.</summary>
public readonly record struct FlashDiscipline
{
    public string Name { get; init; }
    public double[] DoorOpen { get; init; }
    public double StationCharges { get; init; }
    public double LidsOff { get; init; }
    public double HoistShare { get; init; }
}
