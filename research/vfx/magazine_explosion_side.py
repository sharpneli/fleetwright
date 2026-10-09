"""Side view (sailor's eye) of the magazine explosion — same simulation as magazine_explosion_ref.py.

A perspective camera at bridge height on a ship nearby, looking at the exploding ship.
Reuses MagazineEvent unchanged; only the camera and the scene around it are new:
sky + haze, perspective sea with fresnel and ripples, a side-profile ship silhouette,
particles splatted in camera space (far first), and their mirror image in the water.

    python3 magazine_explosion_side.py OUT
"""
import numpy as np
from scipy.ndimage import gaussian_filter, map_coordinates, shift as nd_shift
import magazine_explosion_ref as M


def look_at(cam, target):
    f = np.asarray(target, float) - cam; f /= np.linalg.norm(f)
    r = np.cross(f, [0, 0, 1.0]); r /= np.linalg.norm(r)
    u = np.cross(r, f)
    return r, u, f


def sun_tau_grid(events, sun, bounds, vx=16.0):
    (x0, x1, y0, y1) = bounds
    gx = np.arange(x0, x1, vx); gy = np.arange(y0, y1, vx); gz = np.arange(0, 1800, vx)
    rho = np.zeros((len(gz), len(gy), len(gx)))
    for ev in events:
        d = ev.p.d
        if not len(ev.p):
            continue
        gas = d["kind"] < 2
        ix = np.clip(((d["pos"][:, 0] - gx[0]) / vx).astype(int), 0, len(gx) - 1)
        iy = np.clip(((d["pos"][:, 1] - gy[0]) / vx).astype(int), 0, len(gy) - 1)
        iz = np.clip((d["pos"][:, 2] / vx).astype(int), 0, len(gz) - 1)
        np.add.at(rho, (iz[gas], iy[gas], ix[gas]), (d["m"] * M.KAPPA / vx ** 2 * 0.06)[gas] * (1 - 0.7 * d["temp"][gas]))
    rho = gaussian_filter(rho, 1.2)
    tau = np.zeros_like(rho)
    acc = np.zeros(rho.shape[1:])
    for k in range(len(gz) - 1, -1, -1):
        tau[k] = acc
        acc = nd_shift(acc + rho[k] * vx / sun[2], (-sun[1] / sun[2], -sun[0] / sun[2]), order=1, mode="constant")

    def sample(X, Y, Z):
        return map_coordinates(tau, [np.clip(Z, 0, None) / vx, (Y - gy[0]) / vx, (X - gx[0]) / vx], order=1, mode="nearest")
    return sample


def ship_profile(ship):
    """Side silhouette boxes (x0, x1, z0, z1) in ship-local x, plus colour factor."""
    L = ship["L"]; h = L / 2
    boxes = [(-h, h * 0.92, -1.0, 9.0, 1.0),                          # hull
             (-0.30 * h, 0.28 * h, 9.0, 17.0, 1.05),                  # deckhouse
             (0.12 * h, 0.26 * h, 17.0, 30.0, 1.1),                    # bridge
             (0.18 * h, 0.22 * h, 30.0, 42.0, 0.9),                    # foremast
             (-0.12 * h, -0.02 * h, 17.0, 27.0, 0.8),                  # funnel
             (-0.24 * h, -0.20 * h, 17.0, 34.0, 0.9),                  # mainmast
             (0.50 * h, 0.62 * h, 9.0, 13.5, 0.85), (0.34 * h, 0.46 * h, 11.0, 15.5, 0.85),   # A, B turrets
             (-0.52 * h, -0.40 * h, 11.0, 15.5, 0.85), (-0.68 * h, -0.56 * h, 9.0, 13.5, 0.85),  # X, Y
             (0.62 * h, 0.62 * h + 18, 11.0, 12.0, 0.7), (0.46 * h, 0.46 * h + 18, 13.5, 14.5, 0.7),   # A, B guns
             (-0.52 * h - 18, -0.52 * h, 13.5, 14.5, 0.7), (-0.68 * h - 18, -0.68 * h, 11.0, 12.0, 0.7)]  # X, Y guns (trained aft)
    return boxes


def render_side(events, t, ship, cam, target, W=960, H=540, hfov=64.0, sun_el=35, sun_az=220,
                night=False, seed=0):
    rng = np.random.default_rng(seed)
    cam = np.asarray(cam, float)
    r, u, f = look_at(cam, target)
    fpx = (W / 2) / np.tan(np.radians(hfov / 2))
    sa, se = np.radians(sun_az), np.radians(sun_el)
    sun = np.array([np.cos(se) * np.cos(sa), np.cos(se) * np.sin(sa), np.sin(se)])
    sky_amb, sun_I = (np.array([0.015, 0.02, 0.04]), 0.0) if night else (np.array([0.35, 0.40, 0.48]), 1.0)

    jj, ii = np.meshgrid(np.arange(W), np.arange(H))
    dirs = (f[None, None] + ((jj - W / 2) / fpx)[..., None] * r[None, None] - ((ii - H / 2) / fpx)[..., None] * u[None, None])
    dirs /= np.linalg.norm(dirs, axis=-1, keepdims=True)
    dz = dirs[..., 2]

    # --- sky
    if night:
        sky = np.array([0.006, 0.009, 0.02]) + np.array([0.01, 0.012, 0.02]) * np.exp(-np.clip(dz, 0, 1) * 8)[..., None]
    else:
        e = np.clip(dz, 0, 1)[..., None]
        sky = np.array([0.55, 0.62, 0.70]) * np.exp(-e * 6) + np.array([0.12, 0.25, 0.55]) * (1 - np.exp(-e * 6))
        sky = sky + np.array([1.0, 0.9, 0.7]) * 0.25 * np.clip(dirs @ sun, 0, 1)[..., None] ** 30
    col = sky.copy()

    # --- sea: ray-plane hit, fresnel sky reflection, ripples, haze
    sea = dz < -1e-4
    tt = np.where(sea, cam[2] / np.maximum(-dz, 1e-4), 0.0)
    Px, Py = cam[0] + dirs[..., 0] * tt, cam[1] + dirs[..., 1] * tt
    if M.NOISE is None:
        M.NOISE = M._fbm()
    Nz = M.NOISE
    # world-anchored ripples: foreshortened with distance automatically
    rip = (0.5 * Nz[(Py / 1.5).astype(int) % 256, (Px / 4.5).astype(int) % 256] +
           Nz[(Py / 0.4 + 77).astype(int) % 256, (Px / 1.2 + 31).astype(int) % 256])
    rip = (rip - rip[sea].mean()) / (rip[sea].std() + 1e-6)
    rip = np.where(sea, rip * np.exp(-tt / 9000.0), 0.0)
    cosv = np.clip(-dz, 0, 1)
    fres = 0.02 + 0.98 * (1 - cosv) ** 5
    fres = np.clip(fres * (1 + 0.5 * rip), 0, 1)
    horizon_sky = np.array([0.55, 0.62, 0.70]) if not night else np.array([0.016, 0.02, 0.035])
    deep = np.array([0.02, 0.06, 0.13]) if not night else np.array([0.003, 0.006, 0.014])
    seacol = deep * (1 + 0.2 * rip[..., None]) * (1 - fres[..., None]) + horizon_sky * 0.75 * fres[..., None]
    col = np.where(sea[..., None], seacol, col)

    # fire point lights on the water (diffuse pool) + collect lights
    lights = [(c, I) for ev in events for (c, I) in ev.lights]
    for (c, I) in lights:
        In = I / M.I_REF
        Lz = max(c[2], 8.0)
        d2 = (Px - c[0]) ** 2 + (Py - c[1]) ** 2 + Lz ** 2
        irr = In * (Lz ** 2 / d2) ** 1.5 * (Lz / 60.0) ** -0.5
        col += (sea * irr)[..., None] * M.blackbody(np.array(0.55)) * (0.15 if not night else 0.25)

    # --- particle list, camera space
    tau_s = sun_tau_grid(events, sun, (-900, 900, -900, 900))
    parts = [ev for ev in events if len(ev.p)]
    P = np.concatenate([np.c_[ev.p.d["pos"], ev.p.d["r"], ev.p.d["m"], ev.p.d["temp"], ev.p.d["kind"], ev.p.d["seed"]]
                        for ev in parts]) if parts else np.zeros((0, 8))
    Ps = P[:, :3] + sun[None] * P[:, 3:4] * 0.8
    Tsun = np.exp(-0.3 * tau_s(Ps[:, 0], Ps[:, 1], Ps[:, 2])) if len(P) else np.zeros(0)
    firelit = np.zeros(len(P))
    for (c, I) in lights:
        firelit += min(I / M.I_REF, 3.0) * 60.0 ** 2 / (((P[:, :3] - c) ** 2).sum(1) + 30.0 ** 2)
    firelit = 1 - np.exp(-0.35 * firelit)
    NOISE = M.NOISE

    def splat_all(target_col, mirror=False, horizon_clip=None):
        rel = P[:, :3] - cam
        if mirror:
            rel = rel.copy(); rel[:, 2] = -P[:, 2] - cam[2]
        depth = rel @ f
        order = np.argsort(-depth)                               # far first
        for n_ in order:
            x, y, z, rr, m, T, kind, sd = P[n_]
            dpt = depth[n_]
            if dpt < 20 or z < -1:
                continue
            sx = W / 2 + fpx * (rel[n_] @ r) / dpt
            sy = H / 2 - fpx * (rel[n_] @ u) / dpt
            rp = max(fpx * rr / dpt, 0.6)
            R = int(2.0 * rp) + 1
            ja, jb = int(max(sx - R, 0)), int(min(sx + R + 1, W))
            ia, ib = int(max(sy - R, 0)), int(min(sy + R + 1, H))
            if ja >= jb or ia >= ib:
                continue
            j2, i2 = np.meshgrid(np.arange(ja, jb), np.arange(ia, ib))
            ux, uy = (j2 - sx) / rp, -(i2 - sy) / rp
            q2 = ux ** 2 + uy ** 2
            ang = np.arctan2(uy, ux)
            edge = 1 + 0.16 * np.sin(5 * ang + sd * 20) + 0.09 * np.sin(11 * ang + sd * 50)
            nzt = NOISE[((i2 - sy) * 52 / max(rp, 1) + sd * 997).astype(int) % 256, ((j2 - sx) * 52 / max(rp, 1) + sd * 613).astype(int) % 256]
            nzt = np.clip((nzt - 0.25) / 0.5, 0, 1); nzt = nzt * nzt * (3 - 2 * nzt)
            wn = NOISE[((i2 * 0.9 + t * 3) % 256).astype(int), ((j2 * 0.9) % 256).astype(int)]
            wn = np.clip((wn - 0.3) / 0.4, 0, 1)
            prof = np.exp(-(q2 / edge ** 2) ** 1.5 * 1.8) * (0.25 + 0.6 * nzt + 0.6 * wn)
            idx = (slice(ia, ib), slice(ja, jb))
            if mirror:
                prof = prof * 0.55 * (horizon_clip[idx])
                prof = prof * np.clip(0.6 + 0.6 * rip[idx], 0, 1.5)
            if kind == 2:
                a = np.clip(prof * 1.6, 0, 0.95)
                c_ = np.array([0.04, 0.035, 0.03]) + T * M.blackbody(np.array(0.8)) * 2
                target_col[idx] = target_col[idx] * (1 - a[..., None]) + a[..., None] * c_
                continue
            if kind == 3:
                target_col[idx] += prof[..., None] * M.blackbody(np.array(T * 0.8 + 0.2)) * 1.5
                continue
            dens = m * M.KAPPA / max(rr, 1.0) ** 2
            a = 1 - np.exp(-prof * dens * (1 - 0.45 * T))
            alb = np.array([0.80, 0.80, 0.82]) if kind == 1 else np.array([0.075, 0.064, 0.056])
            # billboard sphere normal in world space, lit by the sun
            nzs = np.sqrt(np.clip(1 - q2 * 0.5, 0, 1))
            nw = ux[..., None] * r + uy[..., None] * u - nzs[..., None] * f
            lam_ = np.clip(0.2 + 0.95 * (nw @ sun), 0, 1)
            lit = alb * (sky_amb * 0.5 * (0.5 + wn[..., None]) + sun_I * 2.2 * Tsun[n_] * (lam_ * (0.45 + 0.8 * wn))[..., None])
            lit = lit + firelit[n_] * M.blackbody(np.array(0.42)) * (0.55 if not night else 0.9) * np.clip(0.6 - uy, 0, 1.2)[..., None]
            Tp = np.clip(T * (0.35 + 0.45 * nzt + 0.9 * (1 - wn) ** 2), 0, 1)
            emis = M.blackbody(Tp) * (Tp ** 2.0)[..., None] * 3.2
            c_ = lit + emis
            # aerial perspective
            hz = 1 - np.exp(-dpt / 25000.0)
            c_ = c_ * (1 - hz) + horizon_sky * hz
            target_col[idx] = target_col[idx] * (1 - a[..., None]) + a[..., None] * c_

    # --- reflection of the explosion in the sea (mirror z), smeared vertically by ripples
    if len(P):
        refl = col.copy()
        splat_all(refl, mirror=True, horizon_clip=sea.astype(float))
        refl = gaussian_filter(refl, (2.5, 0.6, 0))
        col = np.where(sea[..., None], refl, col)

    # --- ship silhouette (boxes in the ship's vertical plane, broken at the gap)
    from PIL import Image, ImageDraw
    hull_c = np.array([0.10, 0.105, 0.115]) if not night else np.array([0.004, 0.0045, 0.005])
    lit_side = 0.5 + 0.5 * max(0.0, -sun[1])   # south side faces the camera
    for (bx0, bx1, z0, z1, k) in ship_profile(ship):
        segs = [(bx0, bx1)]
        if ship.get("gap"):
            g0, g1 = ship["gap"][0] - ship["x0"], ship["gap"][1] - ship["x0"]
            segs = [(a_, min(b_, g0)) for a_, b_ in segs if a_ < g0] + [(max(a_, g1), b_) for a_, b_ in segs if b_ > g1]
        for (a_, b_) in segs:
            if b_ <= a_:
                continue
            corners = [(ship["x0"] + a_, z0), (ship["x0"] + b_, z0), (ship["x0"] + b_, z1), (ship["x0"] + a_, z1)]
            pts = []
            for (cx_, cz_) in corners:
                rel = np.array([cx_, ship["y0"] - ship["B"] / 2, cz_]) - cam
                dpt = rel @ f
                pts.append((W / 2 + fpx * (rel @ r) / dpt, H / 2 - fpx * (rel @ u) / dpt))
            mimg = Image.new("L", (W, H), 0)
            ImageDraw.Draw(mimg).polygon(pts, fill=255)
            mk = np.asarray(mimg, float)[..., None] / 255
            c_ = hull_c * k * (0.6 + 0.6 * lit_side * sun_I + (0.3 if night else 0))
            fl = sum(min(I / M.I_REF, 3.0) * 3e3 / (((np.array([ship["x0"] + (a_ + b_) / 2, ship["y0"], (z0 + z1) / 2]) - c) ** 2).sum() + 900)
                     for (c, I) in lights)
            c_ = c_ + M.blackbody(np.array(0.5)) * min(fl, 1.0) * (0.12 if night else 0.04)
            col = col * (1 - mk) + mk * c_

    # --- explosion particles
    if len(P):
        splat_all(col)

    # --- bloom + tonemap
    bright = np.clip(col - 1.0, 0, None)
    col = col + gaussian_filter(bright, (6, 6, 0)) * 0.6 + gaussian_filter(bright, (24, 24, 0)) * 0.35
    col = col * 1.15 / (1 + col * 0.65)
    return (np.clip(col, 0, 1) ** (1 / 2.2) * 255).astype(np.uint8)


if __name__ == "__main__":
    import sys
    from PIL import Image, ImageDraw
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    ship = dict(L=230.0, B=31.0, x0=-150.0, y0=-90.0)
    mag_x = ship["x0"] - 0.27 * ship["L"]
    broken = dict(ship, gap=(mag_x - 25, mag_x + 25))
    # observer: a ship ~1.6 km off the beam, bridge height 20 m (Tiger was close astern of Queen Mary)
    cam = (mag_x + 250, ship["y0"] - 2200, 20.0)
    tgt = (mag_x, ship["y0"], 360.0)

    def run(times, night, label):
        ev = M.MagazineEvent(mag_x, ship["y0"], "blast", 20000.0, seed=5,
                             vents=[(ship["x0"] - 20, ship["y0"], 0.4, 1.0)])
        t, frames = 0.0, []
        for T in times:
            while t < T - 1e-9:
                ev.step(0.05); t += 0.05
            img = Image.fromarray(render_side([ev], t, broken, cam, tgt, night=night))
            ImageDraw.Draw(img).text((8, 6), f"{label}  t = {T:g} s", fill=(235, 235, 235))
            img.save(f"{out}/magx_side_{'night' if night else 'day'}_t{T:g}.png")
            frames.append(img)
        return frames

    day = run([0.3, 1.5, 4.0, 8.0, 15.0, 30.0], False, "2.2 km off the beam, day")
    night = run([0.3, 1.5, 4.0, 8.0], True, "2.2 km off the beam, night")
    for name, fr, cols in [("magx_side_day_sheet.png", day, 3), ("magx_side_night_sheet.png", night, 2)]:
        w, h = fr[0].size
        rows = (len(fr) + cols - 1) // cols
        sheet = Image.new("RGB", (w * cols, h * rows))
        for k, im in enumerate(fr):
            sheet.paste(im, ((k % cols) * w, (k // cols) * h))
        sheet.save(f"{out}/{name}")
