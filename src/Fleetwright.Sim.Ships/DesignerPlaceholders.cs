namespace Fleetwright.Sim.Ships;

/// <summary>DESIGNER PLACEHOLDERS: facts a class needs that the designer (shipgen) doesn't give yet. Each is a rough
/// stand-in, here and nowhere else, so they're easy to find and to replace when shipgen derives them (the user,
/// 2026-10-12: "if you need something from the designer and it ain't there, put in a placeholder and flag it"). The
/// list is in docs/battle/plan.md, "Needed from the designer". A replaced placeholder is deleted from here.</summary>
public static class DesignerPlaceholders
{
    /// <summary>Fills what the design didn't give.</summary>
    public static MotionFacts Fill(MotionFacts m) => m with
    {
        TacticalDiameterM = m.TacticalDiameterM > 0 ? m.TacticalDiameterM : TacticalDiameter(m),
        RudderRateDegS = m.RudderRateDegS > 0 ? m.RudderRateDegS : Math.Clamp(2.3 * 100 / m.LengthM, 2.3, 10),
        HardOverDeg = m.HardOverDeg > 0 ? m.HardOverDeg : 35,
        PowerRampS = m.PowerRampS > 0 ? m.PowerRampS : 1.93 * Math.Pow(m.FullDisplacementT, 0.47),
        PowerCutS = m.PowerCutS > 0 ? m.PowerCutS : 15,
    };

    /// <summary>PLACEHOLDER: TD/L = 0.341·(L/B)^1.3, times 0.77 for two rudders or more, kept to 2.5-8 (4-8
    /// under 60 m: planing craft turn in 3-6 lengths). A rough fit to
    /// research/simulation/fire-control-research/manoeuvring.md §1: a single-rudder ship of L/B 6 turns in ~3.5
    /// lengths, Fletcher's L/B 9.6 in 6-8 (Titanic's 4.4); twin rudders ~25% tighter (Sumner against Fletcher). It
    /// overstates fast battleships (Iowa 3.9 against a sourced 2.8). Shipgen's rudders all have 1.7% of L·T, so the
    /// rudder's size can't tell ships apart yet: the designer should derive TD from the rudders, the hull's form and
    /// the skeg. Rudder rate and hard over (2.3°/s to 35°, the SOLAS-type gear of manoeuvring.md §3, faster under
    /// 100 m: a PT boat's ~3 s to hard over) want the steering gear and its era; the power ramp (1.93·Δ^0.47 s from nothing to
    /// full, Δ the full displacement in tonnes: ~340 s for a 60,000 t battleship, ~100 s for a 4,400 t destroyer, ~20 s
    /// for a 115 t PT boat, which with the forces gives the research's times to full speed) and the cut (15 s from
    /// full to nothing) want the plant's kind and its boilers.</summary>
    static double TacticalDiameter(MotionFacts m)
    {
        double tdOverL = 0.341 * Math.Pow(m.LengthM / m.BeamM, 1.3) * (m.Rudders >= 2 ? 0.77 : 1);
        return Math.Clamp(tdOverL, m.LengthM < 60 ? 4 : 2.5, 8) * m.LengthM;
    }
}
