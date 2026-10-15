namespace Fleetwright.Map;

/// <summary>What the world view's effects share (docs/visuals/splashes.md decisions 1, 2 and 4; docs/visuals/muzzle-flashes.md
/// "Time"), one place, so every effect is learned once:
/// <list type="bullet">
/// <item><b>The effects camera:</b> a tall effect is drawn as from a 3/4 view looking north, a height h above its anchor
/// going h·tan(pitch) up the screen (a splash's anchor is the sea, a flash's the barrel tips). One pitch for every
/// zoom.</item>
/// <item><b>The effects' clock</b> (splash columns, flashes): the world's speed up to <see cref="ClockKnee"/>, then
/// knee·(speed/knee)^<see cref="ClockPower"/> (60x: 11x), so a short event still plays out on the screen. Above the
/// knee a salvo keeps its event with share <see cref="KeepK"/>·rate/speed, so about that many times the live events of
/// 1x at most.</item>
/// <item><b>The sea's clock</b> (foam, frost, smoke): the world's speed capped (<see cref="SeaView.SpeedCap"/>). Above
/// the cap what lies on the water outlives the world's time, so many more lie there at once: each is fainter by
/// (cap/speed)^<see cref="FaintPower"/>, never under <see cref="FaintMin"/>.</item>
/// </list>
/// Display only. The knobs are set through <c>-splash=</c> (pitch, knee, power, keep, faint).</summary>
public static class Effects
{
    /// <summary>The effects camera's pitch, degrees from straight down (approved, splashes.md decision 1).</summary>
    public static double PitchDeg = 45;

    /// <summary>tan of <see cref="PitchDeg"/>: screen metres north per metre up.</summary>
    public static double K => Math.Tan(PitchDeg * Math.PI / 180);

    public static double ClockKnee = 2, ClockPower = 0.5, KeepK = 2;

    public static double FaintPower = 0.5, FaintMin = 0.3;

    /// <summary>Very tall effects (docs/visuals/magazine-explosions.md decision 3): heights up to the knee are drawn
    /// true, above it compressed as knee + H_c(1 − e^(−(z − knee)/H_c)), so a 1 km smoke cap stays on the screen with
    /// its ship. 300 m to start (the user: to iterate on); splashes and flashes stay under it.</summary>
    public static double KneeM = 300, KneeHcM = 450;

    /// <summary>A height over an effect's anchor as drawn, m (before the pitch's k).</summary>
    public static double Drawn(double z) => z <= KneeM ? z : KneeM + KneeHcM * (1 - Math.Exp(-(z - KneeM) / KneeHcM));

    /// <summary>The effects' clock's rate at a clock speed.</summary>
    public static double Rate(double speed) =>
        speed <= ClockKnee ? speed : ClockKnee * Math.Pow(speed / ClockKnee, ClockPower);

    /// <summary>The sea's clock's rate at a clock speed.</summary>
    public static double SeaRate(double speed) => Math.Min(speed, SeaView.SpeedCap);

    /// <summary>The share of salvos that keep their event at a clock speed.</summary>
    public static double KeepShare(double speed) => speed <= ClockKnee ? 1 : Math.Min(1, KeepK * Rate(speed) / speed);

    /// <summary>The strength of what lies on the water, made at a clock speed.</summary>
    public static double SeaStrength(double speed) =>
        speed <= SeaView.SpeedCap ? 1 : Math.Max(FaintMin, Math.Pow(SeaView.SpeedCap / speed, FaintPower));
}
