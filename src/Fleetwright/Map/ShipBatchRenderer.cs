using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Gpu;
using Fleetwright.Map.Flashes;
using Fleetwright.Map.Splashes;
using Fleetwright.Map.Wakes;
using Fleetwright.Shipgen.Render;
using Fleetwright.Shipgen.Render.Bake;
using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;
using SDL;
using static SDL.SDL3;

namespace Fleetwright.Map;

/// <summary>One quad the ship shaders draw (shipmap.vert's Quad, std430: 10 vec4s). Corners are metres from the eye, so
/// the floats are small numbers and nothing jitters at the ship band.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct ShipQuad
{
    public Vector4 O, U, V, Ship, ShipV, Sun, March, Hmap, Tint, Sea;
    public const int SizeInBytes = 160;
}

public enum ShipDrawKind : byte
{
    Sprite,
    Shadow,
    /// <summary>A wake's stamp (docs/visuals/wakes.md K3): drawn into the wake target, its two bakes in Tex0 and Tex1.</summary>
    Wake,
}

/// <summary>One instanced draw: <see cref="Count"/> quads from <see cref="First"/>, with these textures (a sprite: its
/// colour, the height map, its normal + AO, its material; a shadow: the height map).</summary>
public struct ShipDraw
{
    public ShipDrawKind Kind;
    public nint Tex0, Tex1, Tex2, Tex3;
    public int First, Count;
}

/// <summary>The ships of a snapshot as quads and instanced draws, on the CPU, per frame (docs/battle/plan.md step 2.3):
/// in the world view (<see cref="WorldView"/>) a ship whose art is up is its sprite at its true size, however small
/// (the hull, the turrets at rest, their shadows and the sun's shadow from the height map, sampled through the mip
/// chain); on the planning table, or while its art isn't drawn yet, it is a stand-in icon a fixed number of pixels
/// long, along its heading. All ships cross between the two together, with the rest of the map. Draws are one per class and layer (per turret type for the turrets), so a fleet of one class is a
/// handful of draws. Nothing here allocates once the arrays have grown to the fleet.
/// <para>The light (docs/visuals/ship-lighting.md) is one per frame, at the view's centre (sprites show only within a
/// few km, where the sun's elevation varies by hundredths of a degree): the sun, the real moon, the ambient
/// (<see cref="ShipLights"/>); the shadows are cast by the stronger of sun and moon.</para>
/// <para>Damage shows on the sprite (docs/battle/fighting.md 4.4, kept light): the sea's plane in the ship's frame
/// from its sinkage, trim and heel, against the height map, drowns what's under it, so a ship settling, listing or going
/// under draws itself (going under, the plane rises over its last minutes by how it's lost: by the bow or stern
/// foundering, on its side capsizing, straight down for a magazine); turrets damaged, out of action or wrecked are
/// tinted (a jammed one keeps the bearing it stopped at, the sim's) and sooted; and wrecked parts above the deck get a
/// scorch over their hitbox's plan. The light (docs/visuals/ship-lighting.md L5) follows: a tilted hull is lit in its own
/// frame, what's drowned has no glint, the scorches and a damaged turret's soot are matte and dark in their hollows, and
/// the shadows go by the heights over the sea's plane, so what's under it casts none.</para></summary>
public sealed class ShipDrawList
{
    /// <summary>The icon's length and width on screen, pixels.</summary>
    public const double IconLengthPx = 22, IconWidthPx = 8;

    /// <summary>Sprites shorter than this on screen get no shadows: too small to see, and the march costs.</summary>
    public const double ShadowFromPx = 90;

    public static readonly Vector4 IconTint = new(0.95f, 0.92f, 0.82f, 1f), SelectedTint = new(1f, 0.78f, 0.25f, 1f);

    /// <summary>The icons' tint by side (side 0, and any past these, the plain one): a dev aid until missions say who
    /// is whose, like the icons themselves.</summary>
    public static readonly Vector4[] SideTints = [IconTint, new(0.55f, 0.75f, 1f, 1f), new(1f, 0.5f, 0.45f, 1f), new(0.55f, 0.95f, 0.55f, 1f)];

    /// <summary>A turret's tint by its state (damaged, out of action, wrecked; jammed alone), multiplied in by its alpha,
    /// and its soot (rough, no metal, darker hollows); a scorch's colour and opacity over a wrecked part by its state.</summary>
    static readonly Vector4 TurretDamaged = new(0.85f, 0.8f, 0.75f, 0.5f), TurretOut = new(0.55f, 0.5f, 0.47f, 0.75f),
        TurretWrecked = new(0.28f, 0.25f, 0.23f, 0.9f), TurretJammed = new(1f, 0.85f, 0.6f, 0.35f);
    const float SootDamaged = 0.35f, SootOut = 0.7f, SootWrecked = 1f;
    static readonly Vector4 ScorchDamaged = new(0.42f, 0.36f, 0.3f, 0.3f), ScorchOut = new(0.45f, 0.3f, 0.2f, 0.55f),
        ScorchWrecked = new(0.4f, 0.24f, 0.14f, 0.85f);

    ShipQuad[] quads = new ShipQuad[1024];
    ShipDraw[] draws = new ShipDraw[64], wakeDraws = new ShipDraw[64];
    int quadCount, drawCount, wakeDrawCount;

    /// <summary>Wakes from this speed, m/s (2 kn), full from <see cref="WakeFullMs"/> (3 kn); none going astern.</summary>
    public const double WakeFromMs = 2 * WakeLibrary.KnotMs, WakeFullMs = 3 * WakeLibrary.KnotMs;

    /// <summary>How far round the screen a ship is still placed for its wake alone, in its lengths on screen: a stamp
    /// reaches ~2.5 lengths astern of its stem.</summary>
    const double WakeReachLengths = 3;

    /// <summary>The wave field's fade astern of the pivot, a Gaussian over this many lengths, going straight, and in a
    /// hard turn (the user, 2026-10-15: long arms looked silly in a turn; a subtle field near the ship is enough). Not
    /// physical: the field is kept to where it reads as the ship's own. A turn is hard at κ·L = <see cref="HardTurnKL"/>
    /// (a turning circle's radius ~4 lengths).</summary>
    public const double WakeFadeL = 1.0, WakeFadeTurnL = 0.4, HardTurnKL = 0.25;
    /// <summary>This frame's light, at the view's centre; the muzzle flashes' lights are set into it after the build
    /// (<see cref="SetFlashLights"/>).</summary>
    public ShipLights Lights { get; private set; } = ShipLights.Flat;

    /// <summary>The muzzle flashes' lights this frame (docs/visuals/muzzle-flashes.md F4), into the frame's light block
    /// every map shader reads.</summary>
    public void SetFlashLights(in LightUniforms flashes)
    {
        var l = Lights;
        l.Uniforms.FlashInfo = flashes.FlashInfo;
        (l.Uniforms.FlashEast, l.Uniforms.FlashNorth, l.Uniforms.FlashUp) = (flashes.FlashEast, flashes.FlashNorth, flashes.FlashUp);
        l.Uniforms.FlashPos = flashes.FlashPos;
        l.Uniforms.FlashRad = flashes.FlashRad;
        Lights = l;
    }

    // per ship this frame
    struct Placed
    {
        public Vec3d Rel, F, S;
        public double PxPerM, LengthPx, SpriteAlpha, IconAlpha;
        public Vector2 SunDir;
        public double SunTan;
        public bool Shadows;
        /// <summary>Off the screen, placed only for its wake, which reaches into it.</summary>
        public bool WakeOnly;
        public int Class, Entity, Side;
        /// <summary>Its view's mounts' bearings, and where its own start in them (-1: at rest).</summary>
        public double[] Bearings;
        public int MountStart;
        /// <summary>Its row in its view (damage), its class, the sea's plane over its waterline (z, slopes, on), and how
        /// much of it still shows (1 until it goes under).</summary>
        public ShipsView View;
        public int Row;
        public ShipClass? Cls;
        public Vector4 Sea;
        public double Fade;
        /// <summary>How far the sea's plane stands off the waterline anywhere over the ship, at most: the shadows, by the
        /// heights over it, may reach that much further.</summary>
        public double SeaSpan;
    }

    Placed[] placed = new Placed[64];
    int placedCount;
    readonly Dictionary<int, List<int>> byClass = [];
    readonly List<int> classes = [];

    public ReadOnlySpan<ShipQuad> Quads => quads.AsSpan(0, quadCount);
    public ReadOnlySpan<ShipDraw> Draws => draws.AsSpan(0, drawCount);

    /// <summary>The wakes' stamps, drawn into the wake target before the globe's pass.</summary>
    public ReadOnlySpan<ShipDraw> WakeDraws => wakeDraws.AsSpan(0, wakeDrawCount);

    /// <summary>How each of the ships was drawn this frame (not the sighted), by its index in the snapshot: 0 not at all (off screen or the far
    /// side), 1 icon, 2 sprite, 3 both (cross-fading).</summary>
    public byte[] DrawnAs { get; private set; } = new byte[64];

    /// <param name="artOf">A class's art, or null while it isn't up.</param>
    /// <param name="classOf">A class by its registry id, or null (its length for the icon's fade, its frame for damage).</param>
    /// <param name="icon">The icon's texture.</param>
    /// <param name="hidden">Per snapshot ship, true to leave it out (drawn as part of its group's marker); may be
    /// shorter than the ships.</param>
    /// <param name="sunMotion"><see cref="SunLapse.Motion"/>: below 1 the light fades to the day's mean (and the moon's to
    /// the night's) and the shadows out.</param>
    /// <param name="sighted">The enemy ships in sight (<see cref="Snapshot.Sighted"/>), drawn the same after the ships.</param>
    /// <param name="wakes">The wake bakes: in the world view each ship under way gets its wave field's stamp
    /// (docs/visuals/wakes.md K3); null for none.</param>
    public void Build(in MapView view, ShipsView ships, long timeMs, Vec3d sun, Func<int, ShipArt?> artOf, Func<int, ShipClass?> classOf,
        nint icon, int selectedEntity, ReadOnlySpan<bool> hidden = default, double sunMotion = 1, ShipsView? sighted = null,
        WakeLibrary? wakes = null)
    {
        Lights = LightsAt(view, timeMs, sun, sunMotion);
        quadCount = drawCount = wakeDrawCount = placedCount = 0;
        foreach (var l in byClass.Values)
            l.Clear();
        classes.Clear();
        if (DrawnAs.Length < ships.Count)
            DrawnAs = new byte[Math.Max(ships.Count, DrawnAs.Length * 2)];
        int most = ships.Count + (sighted?.Count ?? 0);
        if (placed.Length < most)
            placed = new Placed[Math.Max(most, placed.Length * 2)];
        double world = WorldView.Factor(view);
        bool wakeOn = wakes != null && world > 0 && SeaView.WakeGain > 0;
        Place(view, world, ships, timeMs, sun, artOf, classOf, hidden, DrawnAs, wakeOn);
        if (sighted != null)
            Place(view, world, sighted, timeMs, sun, artOf, classOf, default, default, wakeOn);

        if (wakeOn)
            for (int i = 0; i < placedCount; i++)
                Wake(ref placed[i], wakes!, view);

        // the sprites, layer by layer, a draw per class (and turret type)
        foreach (int cls in classes)
        {
            var art = artOf(cls)!;
            int first = quadCount;
            foreach (int i in byClass[cls])
                Hull(ref placed[i], art);
            AddDraw(ShipDrawKind.Sprite, art.Hull, art.Height, first, art.HullNormal, art.HullMaterial);
        }
        foreach (int cls in classes)   // scorches over wrecked parts, under the turrets
        {
            var art = artOf(cls)!;
            int first = quadCount;
            foreach (int i in byClass[cls])
                Scorches(ref placed[i], art);
            AddDraw(ShipDrawKind.Sprite, art.Hull, art.Height, first, art.HullNormal, art.HullMaterial);
        }
        foreach (int cls in classes)   // the hull's shadow, on the hull and the sea: under the turrets, which take it at their roofs
        {
            var art = artOf(cls)!;
            int first = quadCount;
            foreach (int i in byClass[cls])
                if (placed[i].Shadows)
                    SunShadow(ref placed[i], art);
            AddDraw(ShipDrawKind.Shadow, art.Height, 0, first);
        }
        for (int layer = 0; layer < 2; layer++)   // turret shadows, then turrets
            foreach (int cls in classes)
            {
                var art = artOf(cls)!;
                for (int t = 0; t < art.Turrets.Length; t++)
                {
                    int first = quadCount;
                    foreach (int i in byClass[cls])
                        if (layer == 1 || placed[i].Shadows)
                            Turrets(ref placed[i], art, t, shadow: layer == 0);
                    AddDraw(ShipDrawKind.Sprite, art.Turrets[t], art.Height, first, art.TurretNormals[t], art.TurretMaterials[t]);
                }
            }

        // the icons, all in one draw
        int firstIcon = quadCount;
        for (int i = 0; i < placedCount; i++)
            if (placed[i].IconAlpha > 0)
                Icon(ref placed[i], placed[i].Entity == selectedEntity);
        AddDraw(ShipDrawKind.Sprite, icon, icon, firstIcon, icon, icon);
    }

    /// <summary>The light at the view's centre: the sun, the real moon from there, and in time-lapse the day's means.</summary>
    static ShipLights LightsAt(in MapView view, long timeMs, Vec3d sun, double sunMotion)
    {
        var up = view.Eye.Normalized();
        var t = new WorldTime(timeMs);
        var at = up * Earth.Radius;
        double phaseLaw = sunMotion < 1 ? Moon.PhaseLaw(Moon.Phase(t).AngleDeg) : 0;
        var means = sunMotion < 1 ? SunLapse.DayMeans(up, sun) : default;
        return ShipLights.Compute(LightTable.Current, up, sun, Moon.Direction(at, t), Moon.Light(at, t), phaseLaw, sunMotion, means);
    }

    /// <summary>Places a view's ships: where each is, how big on screen, sprite or icon (by <paramref name="world"/>,
    /// the view's <see cref="WorldView.Factor(in MapView)"/>), the sun in its frame; how each was drawn into
    /// <paramref name="drawnAs"/>, if given.</summary>
    void Place(in MapView view, double world, ShipsView ships, long timeMs, Vec3d sun, Func<int, ShipArt?> artOf, Func<int, ShipClass?> classOf,
        ReadOnlySpan<bool> hidden, Span<byte> drawnAs, bool wakes = false)
    {
        for (int k = 0; k < ships.Count; k++)
        {
            if (k < drawnAs.Length)
                drawnAs[k] = 0;
            if (k < hidden.Length && hidden[k])
                continue;
            var (p, f) = ships.PoseAt(k, timeMs);
            if (!view.Project(p, out double x, out double y))
                continue;
            int cls = ships.Class[k];
            var art = artOf(cls);
            var c = classOf(cls);
            double length = c?.Length ?? 150;
            var rel = p - view.Eye;
            double pxPerM = 1 / view.MetresPerPixel * view.Altitude / Math.Max(Vec3d.Dot(rel, view.Forward), 1e-3);
            double lengthPx = length * pxPerM, margin = Math.Max(lengthPx, IconLengthPx);
            double reach = wakes ? Math.Max(margin, WakeReachLengths * lengthPx) : margin;
            if (x < -reach || y < -reach || x > view.Width + reach || y > view.Height + reach)
                continue;
            bool wakeOnly = x < -margin || y < -margin || x > view.Width + margin || y > view.Height + margin;
            var up = p.Normalized();
            var s = Vec3d.Cross(f, up);
            var light = Lights.ShadowDir;   // the shadows' light, in the ship's frame
            double sx = Vec3d.Dot(light, f), sy = Vec3d.Dot(light, s), sz = Vec3d.Dot(light, up);
            double horiz = Math.Sqrt(sx * sx + sy * sy);
            double sprite = wakeOnly ? 0 : art != null && pxPerM >= art.MinPxPerM ? world : 0;
            var (sea, fade) = c != null && art != null ? SeaOf(c, art, ships, k, timeMs) : (Vector4.Zero, 1.0);
            placed[placedCount] = new Placed
            {
                Rel = rel, F = f, S = s, PxPerM = pxPerM, LengthPx = lengthPx,
                SpriteAlpha = sprite * fade, IconAlpha = wakeOnly ? 0 : (1 - sprite) * fade, WakeOnly = wakeOnly,
                SunDir = horiz > 1e-9 ? new Vector2((float)(sx / horiz), (float)(sy / horiz)) : Vector2.UnitX,
                SunTan = Math.Max(sz / Math.Max(horiz, 1e-9), 0.15),
                Shadows = !wakeOnly && sz > 0.05 && lengthPx >= ShadowFromPx && Lights.ShadowOpacity > 0,   // none in time-lapse: they'd spin
                Class = cls, Entity = ships.Entity[k], Side = ships.Side[k], Bearings = ships.MountBearingDeg, MountStart = ships.MountStart[k],
                View = ships, Row = k, Cls = c, Sea = sea, Fade = fade,
                SeaSpan = c != null ? Math.Abs(sea.X) + Math.Abs(sea.Y) * c.Length / 2 + Math.Abs(sea.Z) * c.Beam / 2 : 0,
            };
            if (k < drawnAs.Length && !wakeOnly)
                drawnAs[k] = (byte)((sprite > 0 ? 2 : 0) | (sprite < 1 ? 1 : 0));
            if (sprite > 0)
            {
                if (!byClass.TryGetValue(cls, out var list))
                    byClass[cls] = list = [];
                if (list.Count == 0)
                    classes.Add(cls);
                list.Add(placedCount);
            }
            placedCount++;
        }

    }

    /// <summary>The sea's plane over a ship's waterline in its own frame (z at the origin, the slopes along and across, and
    /// the tallest column over the waterline when there's anything to drown, where the shaders start looking for the
    /// point of the tilted hull that shows: 0 when there isn't), and how much of it shows: settled by its attitude; going under, rising over it
    /// by how it's lost.</summary>
    static (Vector4 Sea, double Fade) SeaOf(ShipClass c, ShipArt art, ShipsView v, int k, long now)
    {
        var a = v.Attitude[k];
        var w = HitQuery.WaterOf(c, a);
        double z = w.Z - c.Vertical.Waterline, dx = w.DzDx, dy = w.DzDy, fade = 1;
        var loss = v.Loss[k];
        if (loss != LossKind.None)
        {
            double span = Math.Max(v.GoneMs[k] - v.LossMs[k], 1), p = Math.Clamp((now - v.LossMs[k]) / span, 0, 1);
            double top = art.Meta.Shadow.MaxHeightM + 3, ease = Math.Pow(p, 1.5);
            switch (loss)
            {
                case LossKind.Foundered:   // by the end it's down by (the stern, when it's level)
                    double along = a.TrimDeg > 0 ? 1 : -1, slope = 0.3;
                    dx += along * slope * p;
                    z += ease * (top + slope * c.Length / 2);
                    break;
                case LossKind.Capsized:   // over on the side it lists to
                    double side = a.HeelDeg >= 0 ? 1 : -1;
                    dy += side * 0.9 * p;
                    z += ease * (top + 0.9 * c.Beam / 2);
                    break;
                default:   // a magazine: straight down, quickly
                    z += Math.Pow(p, 0.7) * top;
                    break;
            }
            fade = 1 - SmoothStep(0.85, 1, p);
        }
        bool on = z != 0 || dx != 0 || dy != 0;
        double tallest = Math.Max(art.Meta.Shadow.MaxHeightM + art.Meta.Shadow.HeightStepM, 1);
        return (on ? new Vector4((float)z, (float)dx, (float)dy, (float)tallest) : Vector4.Zero, fade);
    }

    /// <summary>A ship's wave field (docs/visuals/wakes.md K3): one quad over the bakes' crop in its frame, which
    /// wakesplat.frag fills from the two bakes round its speed, each scaled about the stem by (its speed / the ship's)²
    /// so their crests line up, the field bent along a turn. Ahead only, from <see cref="WakeFromMs"/>.</summary>
    void Wake(ref Placed p, WakeLibrary wakes, in MapView view)
    {
        if (p.Cls is not { } c)
            return;
        double u = p.View.Speed[p.Row];
        if (u < WakeFromMs || wakes.HullOf(p.Class, c) is not { } h)
            return;
        // nearest the view's centre first; the ordered speed's bucket after every one drawn
        double along = Vec3d.Dot(p.Rel, view.Forward), priority = Math.Sqrt(Math.Max(p.Rel.LengthSquared - along * along, 0));
        if (!wakes.Resolve(h, u, priority, out var a, out var b, out double wa))
            return;
        double ordered = p.View.SpeedOrderKn[p.Row] * WakeLibrary.KnotMs;
        if (ordered >= WakeFromMs && Math.Abs(ordered - u) > 0.05 * u)
            wakes.Prefetch(h, ordered, 1e9 + priority);

        double sa = a.SpeedMs * a.SpeedMs / (u * u), sb = b.SpeedMs * b.SpeedMs / (u * u);
        double zb = WakeBake.BowWaveHeight(u, h.DraughtM, h.EntranceDeg);
        double ga = SeaView.WakeGain * zb / Math.Max(a.Zb, 1e-6) * sa, gb = SeaView.WakeGain * zb / Math.Max(b.Zb, 1e-6) * sb;
        double stem = h.X1, pivot = h.MidX;
        double x0 = Math.Min(stem + (a.X0 - a.StemX) / sa, stem + (b.X0 - b.StemX) / sb);
        double x1 = Math.Max(stem + (a.X1 - a.StemX) / sa, stem + (b.X1 - b.StemX) / sb);
        double y1 = Math.Max(a.HalfY / sa, b.HalfY / sb);
        // the turn: the field astern of the pivot bent onto the arc the ship has come along, y' = y + κ·x²/2 (κ the
        // path's curvature, + to starboard), held to a length's offset at the stamp's end
        double kappa0 = p.View.YawRate[p.Row] / u, turn = Math.Clamp(Math.Abs(kappa0) * h.LengthM / HardTurnKL, 0, 1);
        double fadeM = h.LengthM * (WakeFadeL + (WakeFadeTurnL - WakeFadeL) * turn);
        x0 = Math.Max(x0, pivot - 2.5 * fadeM);   // nothing shows past it
        double back = Math.Max(pivot - x0, 1), cap = 2 * h.LengthM / (back * back);
        double kappa = Math.Clamp(kappa0, -cap, cap), shear = 0.5 * Math.Abs(kappa) * back * back;
        double oy = -y1 - shear, w = x1 - x0, hgt = 2 * (y1 + shear);

        if (quadCount == quads.Length)
            Array.Resize(ref quads, quads.Length * 2);
        int first = quadCount;
        var o = p.Rel + p.F * x0 + p.S * oy;
        var ue = p.F * w;
        var ve = p.S * hgt;
        quads[quadCount++] = new ShipQuad
        {
            O = new Vector4((float)o.X, (float)o.Y, (float)o.Z, 0),
            U = new Vector4((float)ue.X, (float)ue.Y, (float)ue.Z, (float)(SmoothStep(WakeFromMs, WakeFullMs, u) * p.Fade)),
            V = new Vector4((float)ve.X, (float)ve.Y, (float)ve.Z, 0),
            Ship = new Vector4((float)x0, (float)oy, (float)w, 0),
            ShipV = new Vector4(0, (float)hgt, (float)fadeM, 0),
            Sun = new Vector4((float)stem, (float)kappa, (float)pivot, (float)wa),
            March = new Vector4((float)a.X0, (float)(1 / (a.X1 - a.X0)), (float)(1 / a.HalfY), (float)a.StemX),
            Hmap = new Vector4((float)b.X0, (float)(1 / (b.X1 - b.X0)), (float)(1 / b.HalfY), (float)b.StemX),
            Tint = new Vector4((float)sa, (float)ga, (float)sb, (float)gb),
            Sea = new Vector4((float)Vec3d.Dot(p.F, view.Right), (float)Vec3d.Dot(p.F, view.Up), (float)Vec3d.Dot(p.S, view.Right),
                (float)Vec3d.Dot(p.S, view.Up)),
        };
        if (wakeDrawCount > 0 && wakeDraws[wakeDrawCount - 1].Tex0 == a.Texture && wakeDraws[wakeDrawCount - 1].Tex1 == b.Texture
            && wakeDraws[wakeDrawCount - 1].First + wakeDraws[wakeDrawCount - 1].Count == first)
        {
            wakeDraws[wakeDrawCount - 1].Count++;
            return;
        }
        if (wakeDrawCount == wakeDraws.Length)
            Array.Resize(ref wakeDraws, wakeDraws.Length * 2);
        wakeDraws[wakeDrawCount++] = new ShipDraw { Kind = ShipDrawKind.Wake, Tex0 = a.Texture, Tex1 = b.Texture, First = first, Count = 1 };
    }

    void Hull(ref Placed p, ShipArt art)
    {
        var (x0, y0, w, h) = TiltedCanvas(p, art);
        Emit(ref p, art, x0, y0, w, 0, 0, h, mode: 0, opacity: p.SpriteAlpha, roof: 0, Vector4.Zero);
    }

    /// <summary>The canvas grown toward where a heeled and trimmed hull shows its tops (each point by its height times the
    /// sea's slopes), so they aren't cut off at its edge: corner and size, ship metres.</summary>
    static (double X0, double Y0, double W, double H) TiltedCanvas(in Placed p, ShipArt art)
    {
        double cw = art.CanvasX, ch = art.CanvasY, sx = p.Sea.W * p.Sea.Y, sy = p.Sea.W * p.Sea.Z;
        return (-cw / 2 + Math.Min(0, sx), -ch / 2 + Math.Min(0, sy), cw + Math.Abs(sx), ch + Math.Abs(sy));
    }

    /// <summary>A scorch over each wrecked part above the deck (superstructure, funnels, masts and the like: the
    /// turrets are tinted instead), its hitbox's plan.</summary>
    void Scorches(ref Placed p, ShipArt art)
    {
        if (p.Cls is not { } c)
            return;
        var v = p.View;
        for (int j = v.PartStart[p.Row], end = j + v.PartCount[p.Row]; j < end; j++)
        {
            var st = v.PartStates[j];
            if (st == PartState.Ok)
                continue;
            ref readonly var comp = ref c.Components[v.Parts[j]];
            if (comp.Bounds.Top <= 0 || comp.Kind is not (ComponentKind.Superstructure or ComponentKind.ConningTower or ComponentKind.Funnel
                    or ComponentKind.Uptake or ComponentKind.Mast or ComponentKind.Casing or ComponentKind.Sponson or ComponentKind.FlightDeck
                    or ComponentKind.HangarBay))
                continue;
            var b = comp.Bounds;
            double up = comp.Bounds.Top - c.Vertical.Waterline, sx = up * p.Sea.Y, sy = up * p.Sea.Z;   // tilted with the hull
            b = b with { X0 = b.X0 + sx, X1 = b.X1 + sx, Y0 = b.Y0 + sy, Y1 = b.Y1 + sy };
            var tint = st == PartState.Destroyed ? ScorchWrecked : st == PartState.Disabled ? ScorchOut : ScorchDamaged;
            Emit(ref p, art, b.X0, b.Y0, b.X1 - b.X0, 0, 0, b.Y1 - b.Y0, mode: 3, opacity: p.SpriteAlpha, roof: 0, tint);
        }
    }

    /// <summary>A mount's tint and soot by its state, or none.</summary>
    static (Vector4 Tint, float Soot) TurretTint(in Placed p, int classMount)
    {
        if (classMount < 0 || p.Cls is not { } c)
            return default;
        int comp = c.Mounts[classMount].Component;
        var v = p.View;
        for (int j = v.PartStart[p.Row], end = j + v.PartCount[p.Row]; j < end; j++)
            if (v.Parts[j] == comp)
                return v.PartStates[j] switch
                {
                    PartState.Destroyed => (TurretWrecked, SootWrecked),
                    PartState.Disabled => (TurretOut, SootOut),
                    PartState.Damaged => (TurretDamaged, SootDamaged),
                    _ => ((v.PartFlags[j] & PartFlags.Jammed) != 0 ? TurretJammed : Vector4.Zero, 0f),
                };
        return default;
    }

    void Turrets(ref Placed p, ShipArt art, int type, bool shadow)
    {
        var meta = art.Meta;
        var tm = meta.TurretTypes[art.TurretTypes[type]];
        double e = tm.SizePx[0] / art.Scale / 2;
        foreach (int i in art.DrawOrder)
        {
            if (art.MountType[i] != type)
                continue;
            var m = meta.Mounts[i];
            // on its bearing from the snapshot (fire control's), else at rest
            int cm = art.ClassMount[i];
            double deg = p.MountStart >= 0 && cm >= 0 ? p.Bearings[p.MountStart + cm] : m.RestDeg;
            double a = deg * Math.PI / 180, ca = Math.Cos(a), sa = Math.Sin(a);
            double axx = ca * 2 * e, axy = sa * 2 * e, ayx = -sa * 2 * e, ayy = ca * 2 * e;
            double ox = m.PosM[0] - axx / 2 - ayx / 2 + m.TopM * p.Sea.Y, oy = m.PosM[1] - axy / 2 - ayy / 2 + m.TopM * p.Sea.Z;   // tilted with the hull
            if (shadow)
            {
                // a box over the turret and as far down-light as its roof's shadow falls (on the sea); the shader projects
                // each point by the height there (Include/shipshadow.glsl). The tint carries the turret's frame.
                double r = e * Math.Sqrt(2), reach = (m.TopM + p.SeaSpan) / p.SunTan, cx = ox + axx / 2 + ayx / 2, cy = oy + axy / 2 + ayy / 2;
                double dx = -p.SunDir.X * reach, dy = -p.SunDir.Y * reach;
                double x0 = cx + Math.Min(0, dx) - r, y0 = cy + Math.Min(0, dy) - r;
                Emit(ref p, art, x0, y0, Math.Abs(dx) + 2 * r, 0, 0, Math.Abs(dy) + 2 * r, mode: 1, opacity: p.SpriteAlpha, roof: m.TopM,
                    new Vector4((float)ox, (float)oy, (float)axx, (float)axy), HullMarch(p, art, 0));
            }
            else
            {
                var (tint, soot) = TurretTint(p, cm);
                Emit(ref p, art, ox, oy, axx, axy, ayx, ayy, mode: 0, opacity: p.SpriteAlpha, roof: m.TopM, tint,
                    p.Shadows ? HullMarch(p, art, m.TopM) : default, CoverM(m), soot);
            }
        }
    }

    void SunShadow(ref Placed p, ShipArt art)
    {
        var (x0, y0, cw, ch) = TiltedCanvas(p, art);
        double reach = (art.Meta.Shadow.MaxHeightM + p.SeaSpan) / p.SunTan, dx = -p.SunDir.X * reach, dy = -p.SunDir.Y * reach;   // the canvas and down-light of it
        Emit(ref p, art, x0 + Math.Min(0, dx), y0 + Math.Min(0, dy), cw + Math.Abs(dx), 0, 0, ch + Math.Abs(dy), mode: 0,
            opacity: p.SpriteAlpha * p.Fade, roof: 0, Vector4.Zero, HullMarch(p, art, 0));
    }

    static Vector4 HullMarch(in Placed p, ShipArt art, double fromM) =>
        HullMarch(art.Meta.Shadow.MaxHeightM + p.SeaSpan, fromM, p.SunTan, Math.Clamp(Math.Log2(art.Scale / p.PxPerM), 0, art.MipLevels), art.Scale);

    /// <summary>The march for the hull's shadow (Include/shipshadow.glsl) on a point <paramref name="fromM"/> over the
    /// waterline: as far toward the light as the tallest column can still shade it (none from above them all), in steps
    /// of the height map's mip <paramref name="lod"/>, spread wider when that would take more than <see cref="MarchSteps"/>.
    /// The map and the viewer both march by it.</summary>
    public static Vector4 HullMarch(double maxHeightM, double fromM, double sunTan, double lod, double pxPerM)
    {
        double reach = Math.Max(0, maxHeightM - fromM) / sunTan;
        double step = Math.Max(Math.Max(0.1, Math.Pow(2, lod) / pxPerM), reach / MarchSteps);
        return new Vector4((float)step, (float)Math.Ceiling(reach / step), 0.3f, (float)lod);
    }

    /// <summary>The most steps a shadow's march takes.</summary>
    public const int MarchSteps = 256;

    /// <summary>The height over the waterline from which the hull covers a mount: a casemate is under the deck over it
    /// (or its housing's roof), so it shows only where the hull stands lower than half a deck under its roof, the shield
    /// out past the side and the barrels; 0 for the others, which nothing covers. The map and the viewer both use it.</summary>
    public static float CoverM(SpriteMount m) => m.Mount == "casemate" ? (float)(m.TopM - Fleetwright.Shipgen.Geometry.DeckPitch / 2) : 0f;

    void Icon(ref Placed p, bool selected)
    {
        // a fixed size on screen, along the heading: metres per pixel at the ship
        double l = IconLengthPx / p.PxPerM, w = IconWidthPx / p.PxPerM;
        Emit(ref p, null, -l / 2, -w / 2, l, 0, 0, w, mode: 2, opacity: p.IconAlpha, roof: 0,
            selected ? SelectedTint : SideTints[p.Side < SideTints.Length ? p.Side : 0], IconSea(p));
    }

    /// <summary>The sea over the icon's deck (docs/visuals/battle-marks.md M4): the sprite's sea plane (its settling,
    /// trim, list and going down, <see cref="SeaOf"/>) against the freeboard, over the icon's half length and half beam;
    /// w 1 when any of it is on. The freeboard is taken at <see cref="IconFreeboard"/> of its own, so a ship low in the
    /// water already shows it on the table, where the true deck going under would be the first sign.</summary>
    static Vector4 IconSea(in Placed p)
    {
        if (p.Cls is not { } c || p.Sea.W <= 0)
            return default;
        double fb = Math.Max(c.Vertical.Freeboard, 1) * IconFreeboard;
        return new Vector4((float)(p.Sea.X - fb), (float)(p.Sea.Y * c.Length / 2), (float)(p.Sea.Z * c.Beam / 2), 1);
    }

    /// <summary>The share of the freeboard the icon takes as its deck's height (realistic is 1; tuned down so the
    /// table shows a ship settling before it's awash).</summary>
    public const double IconFreeboard = 0.5;

    /// <summary>A quad from ship-local metres: corner (ox, oy), edges (ux, uy) and (vx, vy).</summary>
    void Emit(ref Placed p, ShipArt? art, double ox, double oy, double ux, double uy, double vx, double vy, int mode, double opacity,
        double roof, Vector4 tint, Vector4 march = default, float cover = 0, float soot = 0)
    {
        if (quadCount == quads.Length)
            Array.Resize(ref quads, quads.Length * 2);
        var o = p.Rel + p.F * ox + p.S * oy;
        var u = p.F * ux + p.S * uy;
        var v = p.F * vx + p.S * vy;
        var hmap = art != null
            ? new Vector4((float)(art.Scale / art.Meta.SizePx[0]), (float)(art.Scale / art.Meta.SizePx[1]), 0.5f, 0.5f)
            : Vector4.Zero;
        quads[quadCount++] = new ShipQuad
        {
            O = new Vector4((float)o.X, (float)o.Y, (float)o.Z, mode),
            U = new Vector4((float)u.X, (float)u.Y, (float)u.Z, (float)opacity),
            V = new Vector4((float)v.X, (float)v.Y, (float)v.Z, (float)roof),
            Ship = new Vector4((float)ox, (float)oy, (float)ux, (float)uy),
            ShipV = new Vector4((float)vx, (float)vy, cover, soot),
            Sun = new Vector4(p.SunDir, (float)p.SunTan, (float)Lights.ShadowOpacity),
            March = march,
            Hmap = hmap,
            Tint = tint,
            Sea = p.Sea,
        };
    }

    void AddDraw(ShipDrawKind kind, nint tex0, nint tex1, int first, nint tex2 = 0, nint tex3 = 0)
    {
        if (quadCount == first)
            return;
        if (drawCount == draws.Length)
            Array.Resize(ref draws, draws.Length * 2);
        draws[drawCount++] = new ShipDraw { Kind = kind, Tex0 = tex0, Tex1 = tex1, Tex2 = tex2, Tex3 = tex3, First = first, Count = quadCount - first };
    }

    static double SmoothStep(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }
}

/// <summary>Draws a <see cref="ShipDrawList"/> inside the globe's main pass (an <see cref="IMapLayer"/>): the quads go
/// up in one storage buffer, then one instanced draw per entry. Pipelines are built for the target's format, samples
/// and depth; with depth, ships are tested against the masking sphere as the coast is, so the far side hides them.
/// Knows nothing of input, ImGui or the world.</summary>
public sealed unsafe class ShipBatchRenderer : IMapLayer, IDisposable
{
    const uint QuadBufferBytes = 8 << 20;   // ~58,000 quads

    readonly SDL_GPUDevice* device;
    readonly SDL_GPUShader* vs, fsSprite, fsShadow, fsWake, vsRibbon, fsRibbon;
    readonly DynamicGpuBuffer foamBuf;
    int foamUploaded;
    const uint FoamBufferBytes = 4 << 20;   // ~131,000 corners
    readonly SDL_GPUSampler* linear, nearest;
    readonly DynamicGpuBuffer quadBuf;
    SDL_GPUGraphicsPipeline* spritePipe, shadowPipe, wakePipe, ribbonPipe;
    SDL_GPUTextureFormat wakeFormat;
    SDL_GPUTextureFormat pipeFormat;
    SDL_GPUSampleCount pipeSamples;
    bool pipeDepth;
    int uploaded;

    public ShipDrawList List { get; } = new();

    /// <summary>The wakes' bakes, on threads of their own (docs/visuals/wakes.md K2).</summary>
    public WakeLibrary Wakes { get; }

    /// <summary>The foam lanes this frame (docs/visuals/wakes.md K4), built by the map's trails.</summary>
    public FoamTrails Foam { get; } = new();

    /// <summary>The shell splashes in the world view (docs/visuals/splashes.md): their foam drawn with the wakes', their
    /// stains before the ships, their shadows and columns after.</summary>
    public SplashRenderer Splashes { get; }

    /// <summary>The muzzle flashes in the world view (docs/visuals/muzzle-flashes.md), drawn last.</summary>
    public FlashRenderer Flashes { get; }

    /// <summary>The muzzle blasts' marks on the water (docs/visuals/muzzle-flashes.md F5), drawn with the wakes'.</summary>
    public BlastRenderer Blasts { get; }

    /// <summary>The gun smoke (docs/visuals/muzzle-flashes.md F6), its shadows and puffs after the ships.</summary>
    public SmokeRenderer Smoke { get; }

    /// <summary>The sea's clock, s (SeaView.ClockS), set by the scene each frame: the foam churns on it.</summary>
    public double FoamClockS { get; set; }

    /// <summary>The stand-in icon: a hull's plan, white, mipmapped.</summary>
    public nint Icon { get; }

    struct ViewParams
    {
        public Vector4 Right, Up, Fwd, Depth, Sun, Moon;
    }

    public ShipBatchRenderer(SDL_GPUDevice* device)
    {
        this.device = device;
        vs = GpuShader.Load(device, "Content/Shaders/Compiled/shipmap.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 1, 0, 1);
        fsSprite = GpuShader.Load(device, "Content/Shaders/Compiled/shipmap.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 4, 0, 0, 1);
        fsShadow = GpuShader.Load(device, "Content/Shaders/Compiled/shipmap_shadow.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 1, 0, 0, 0);
        fsWake = GpuShader.Load(device, "Content/Shaders/Compiled/wakesplat.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 2, 0, 0, 0);
        vsRibbon = GpuShader.Load(device, "Content/Shaders/Compiled/wakeribbon.vert.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_VERTEX, 0, 0, 0, 1);
        fsRibbon = GpuShader.Load(device, "Content/Shaders/Compiled/wakeribbon.frag.spv", SDL_GPUShaderStage.SDL_GPU_SHADERSTAGE_FRAGMENT, 0, 0, 0, 0);
        if (vs == null || fsSprite == null || fsShadow == null || fsWake == null || vsRibbon == null || fsRibbon == null)
            throw new InvalidOperationException("ship map shaders failed to load");
        linear = Sampler(SDL_GPUFilter.SDL_GPU_FILTER_LINEAR, SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_LINEAR);
        nearest = Sampler(SDL_GPUFilter.SDL_GPU_FILTER_NEAREST, SDL_GPUSamplerMipmapMode.SDL_GPU_SAMPLERMIPMAPMODE_NEAREST);
        quadBuf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_GRAPHICS_STORAGE_READ, QuadBufferBytes);
        Icon = (nint)SpriteTextures.Upload(device, SpriteImages.Chain(IconImage(), false, 5));
        Wakes = new WakeLibrary(device);
        foamBuf = new DynamicGpuBuffer(device, SDL_GPUBufferUsageFlags.SDL_GPU_BUFFERUSAGE_VERTEX, FoamBufferBytes);
        Splashes = new SplashRenderer(device);
        Flashes = new FlashRenderer(device);
        Blasts = new BlastRenderer(device);
        Smoke = new SmokeRenderer(device);
    }

    /// <summary>The icon: a hull's plan 128 x 32, bow to the right (+x), a fine bow and a rounded stern.</summary>
    static Image8 IconImage()
    {
        const int W = 128, H = 32;
        var img = new Image8(W, H, 4);
        for (int py = 0; py < H; py++)
            for (int px = 0; px < W; px++)
            {
                // coverage by 4x4 samples of the half-breadth: full amidships, a pointed bow, a round stern
                int hits = 0;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                    {
                        double x = (px + (sx + 0.5) / 4) / W, y = Math.Abs((py + (sy + 0.5) / 4) / H - 0.5) * 2;
                        double half = x > 0.6 ? Math.Sqrt(Math.Max(0, 1 - Math.Pow((x - 0.6) / 0.4, 2))) * (1 - (x - 0.6) / 0.4 * 0.35)
                            : x < 0.12 ? Math.Sqrt(Math.Max(0, 1 - Math.Pow((0.12 - x) / 0.12, 2)))
                            : 1;
                        if (y <= half * 0.92)
                            hits++;
                    }
                int i = (py * W + px) * 4;
                byte a = (byte)(hits * 255 / 16);
                img.Data[i] = img.Data[i + 1] = img.Data[i + 2] = 255;
                img.Data[i + 3] = a;
            }
        return img;
    }

    SDL_GPUSampler* Sampler(SDL_GPUFilter f, SDL_GPUSamplerMipmapMode m)
    {
        var info = new SDL_GPUSamplerCreateInfo
        {
            min_filter = f, mag_filter = f, mipmap_mode = m,
            address_mode_u = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_v = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            address_mode_w = SDL_GPUSamplerAddressMode.SDL_GPU_SAMPLERADDRESSMODE_CLAMP_TO_EDGE,
            max_lod = 1000,
        };
        return SDL_CreateGPUSampler(device, &info);
    }

    void EnsurePipelines(RenderTarget target)
    {
        if (spritePipe != null && pipeFormat == target.ColorFormat && pipeSamples == target.SampleCount && pipeDepth == target.HasDepth)
            return;
        ReleasePipelines();
        (pipeFormat, pipeSamples, pipeDepth) = (target.ColorFormat, target.SampleCount, target.HasDepth);
        GpuPipelineBuilder B()
        {
            var b = new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
                .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingPremultiplied().ClearVertexLayout();
            return target.HasDepth
                ? b.SetDepthFormat(RenderTarget.DepthFormat).EnableDepthTest(false, SDL_GPUCompareOp.SDL_GPU_COMPAREOP_LESS_OR_EQUAL)
                : b.NoDepthTarget().DisableDepthTest();
        }
        spritePipe = B().SetShaders(vs, fsSprite).Build(device);
        shadowPipe = B().SetShaders(vs, fsShadow).Build(device);
        if (spritePipe == null || shadowPipe == null)
            throw new InvalidOperationException($"ship map pipelines: {SDL_GetError()}");
    }

    void ReleasePipelines()
    {
        if (spritePipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, spritePipe);
        if (shadowPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, shadowPipe);
        spritePipe = shadowPipe = null;
    }

    public void Copy(SDL_GPUCommandBuffer* cmd)
    {
        Wakes.Update(cmd);
        Splashes.Copy(cmd);
        Flashes.Copy(cmd);
        Blasts.Copy(cmd);
        Smoke.Copy(cmd);
        var fv = Foam.Vertices;
        foamUploaded = Math.Min(fv.Length, (int)(FoamBufferBytes / FoamVertex.SizeInBytes));
        foamUploaded -= foamUploaded % 6;
        foamBuf.Upload(cmd, fv[..foamUploaded]);
        var q = List.Quads;
        uploaded = Math.Min(q.Length, (int)(QuadBufferBytes / ShipQuad.SizeInBytes));
        quadBuf.Upload(cmd, q[..uploaded]);
    }

    public void Draw(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        Splashes.DrawStains(cmd, pass, target, view);
        DrawShips(cmd, pass, target, view);
        Smoke.Draw(cmd, pass, target, view);
        Splashes.DrawColumns(cmd, pass, target, view);
        Flashes.Draw(cmd, pass, target, view);
    }

    void DrawShips(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (uploaded == 0)
            return;
        EnsurePipelines(target);
        var (near, far) = GlobeRenderer.DepthRange(view);
        var sb = quadBuf.Buffer;
        var lights = List.Lights;
        var lu = lights.Uniforms;
        SDL_PushGPUFragmentUniformData(cmd, 0, (nint)(&lu), (uint)sizeof(LightUniforms));
        SDL_GPUGraphicsPipeline* bound = null;
        var b4 = stackalloc SDL_GPUTextureSamplerBinding[4];
        foreach (var d in List.Draws)
        {
            if (d.First + d.Count > uploaded)
                break;
            var pipe = d.Kind == ShipDrawKind.Shadow ? shadowPipe : spritePipe;
            if (pipe != bound)
            {
                SDL_BindGPUGraphicsPipeline(pass, pipe);
                SDL_BindGPUVertexStorageBuffers(pass, 0, &sb, 1);
                bound = pipe;
            }
            var vp = new ViewParams
            {
                Right = new Vector4(F(view.Right), (float)view.TanHalfX),
                Up = new Vector4(F(view.Up), (float)view.TanHalfY),
                Fwd = new Vector4(F(view.Forward), 0),
                Depth = new Vector4((float)near, (float)far, d.First, 0),
                Sun = new Vector4(F(lights.SunDir), 0),
                Moon = new Vector4(F(lights.MoonDir), 0),
            };
            SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&vp), (uint)sizeof(ViewParams));
            if (d.Kind == ShipDrawKind.Shadow)
            {
                var b = new SDL_GPUTextureSamplerBinding { texture = (SDL_GPUTexture*)d.Tex0, sampler = nearest };
                SDL_BindGPUFragmentSamplers(pass, 0, &b, 1);
            }
            else
            {
                b4[0] = new SDL_GPUTextureSamplerBinding { texture = (SDL_GPUTexture*)d.Tex0, sampler = linear };
                b4[1] = new SDL_GPUTextureSamplerBinding { texture = (SDL_GPUTexture*)d.Tex1, sampler = nearest };
                b4[2] = new SDL_GPUTextureSamplerBinding { texture = (SDL_GPUTexture*)d.Tex2, sampler = linear };
                b4[3] = new SDL_GPUTextureSamplerBinding { texture = (SDL_GPUTexture*)d.Tex3, sampler = linear };
                SDL_BindGPUFragmentSamplers(pass, 0, b4, 4);
            }
            SDL_DrawGPUPrimitives(pass, 6, (uint)d.Count, 0, 0);
        }
    }

    /// <summary>The wakes' stamps into the wake target (its own format, one sample, no depth): slopes and variance
    /// added, foam by the most (docs/visuals/wakes.md).</summary>
    public void DrawWake(SDL_GPUCommandBuffer* cmd, SDL_GPURenderPass* pass, RenderTarget target, in MapView view)
    {
        if (wakePipe == null || wakeFormat != target.ColorFormat)
        {
            if (wakePipe != null)
                SDL_ReleaseGPUGraphicsPipeline(device, wakePipe);
            if (ribbonPipe != null)
                SDL_ReleaseGPUGraphicsPipeline(device, ribbonPipe);
            wakeFormat = target.ColorFormat;
            GpuPipelineBuilder B() => new GpuPipelineBuilder().SetColorFormat(target.ColorFormat).SetMultisampling(target.SampleCount)
                .SetCullMode(SDL_GPUCullMode.SDL_GPU_CULLMODE_NONE).EnableBlendingAddMaxAlpha().NoDepthTarget().DisableDepthTest();
            wakePipe = B().ClearVertexLayout().SetShaders(vs, fsWake).Build(device);
            ribbonPipe = B().ClearVertexLayout().AddVertexBuffer(0, FoamVertex.SizeInBytes)
                .AddVertexAttribute(0, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT3, 0)
                .AddVertexAttribute(1, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT, 12)
                .AddVertexAttribute(2, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT2, 16)
                .AddVertexAttribute(3, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT, 24)
                .AddVertexAttribute(4, 0, SDL_GPUVertexElementFormat.SDL_GPU_VERTEXELEMENTFORMAT_FLOAT, 28)
                .SetShaders(vsRibbon, fsRibbon).Build(device);
            if (wakePipe == null || ribbonPipe == null)
                throw new InvalidOperationException($"wake pipelines: {SDL_GetError()}");
        }
        var (near, far) = GlobeRenderer.DepthRange(view);
        var lights = List.Lights;
        var vp0 = new ViewParams
        {
            Right = new Vector4(F(view.Right), (float)view.TanHalfX),
            Up = new Vector4(F(view.Up), (float)view.TanHalfY),
            Fwd = new Vector4(F(view.Forward), 0),
            Depth = new Vector4((float)near, (float)far, 0, 0),
            Sun = new Vector4(F(lights.SunDir), 0),
            Moon = new Vector4(F(lights.MoonDir), 0),
        };

        // the foam lanes, then the stamps (each to its own channels: the order doesn't matter)
        if (foamUploaded > 0)
        {
            SDL_BindGPUGraphicsPipeline(pass, ribbonPipe);
            var vb = new SDL_GPUBufferBinding { buffer = foamBuf.Buffer, offset = 0 };
            SDL_BindGPUVertexBuffers(pass, 0, &vb, 1);
            var vf = vp0;
            vf.Depth.W = (float)FoamClockS;
            SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&vf), (uint)sizeof(ViewParams));
            SDL_DrawGPUPrimitives(pass, (uint)foamUploaded, 1, 0, 0);
        }
        Splashes.DrawFoam(cmd, pass, target, view);
        Blasts.DrawWake(cmd, pass, target, view);
        if (List.WakeDraws.Length == 0 || uploaded == 0)
            return;
        var sb = quadBuf.Buffer;
        SDL_BindGPUGraphicsPipeline(pass, wakePipe);
        SDL_BindGPUVertexStorageBuffers(pass, 0, &sb, 1);
        var b2 = stackalloc SDL_GPUTextureSamplerBinding[2];
        foreach (var d in List.WakeDraws)
        {
            if (d.First + d.Count > uploaded)
                break;
            var vp = vp0;
            vp.Depth.Z = d.First;
            SDL_PushGPUVertexUniformData(cmd, 0, (nint)(&vp), (uint)sizeof(ViewParams));
            b2[0] = new SDL_GPUTextureSamplerBinding { texture = (SDL_GPUTexture*)d.Tex0, sampler = linear };
            b2[1] = new SDL_GPUTextureSamplerBinding { texture = (SDL_GPUTexture*)d.Tex1, sampler = linear };
            SDL_BindGPUFragmentSamplers(pass, 0, b2, 2);
            SDL_DrawGPUPrimitives(pass, 6, (uint)d.Count, 0, 0);
        }
    }

    static Vector3 F(Vec3d a) => new((float)a.X, (float)a.Y, (float)a.Z);

    public void Dispose()
    {
        Wakes.Dispose();
        Splashes.Dispose();
        Flashes.Dispose();
        Blasts.Dispose();
        Smoke.Dispose();
        SDL_WaitForGPUIdle(device);
        ReleasePipelines();
        if (wakePipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, wakePipe);
        if (ribbonPipe != null)
            SDL_ReleaseGPUGraphicsPipeline(device, ribbonPipe);
        foamBuf.Dispose();
        SDL_ReleaseGPUShader(device, fsWake);
        SDL_ReleaseGPUShader(device, vsRibbon);
        SDL_ReleaseGPUShader(device, fsRibbon);
        quadBuf.Dispose();
        SDL_ReleaseGPUTexture(device, (SDL_GPUTexture*)Icon);
        SDL_ReleaseGPUShader(device, vs);
        SDL_ReleaseGPUShader(device, fsSprite);
        SDL_ReleaseGPUShader(device, fsShadow);
        SDL_ReleaseGPUSampler(device, linear);
        SDL_ReleaseGPUSampler(device, nearest);
    }
}
