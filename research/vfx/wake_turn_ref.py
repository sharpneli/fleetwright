"""Turning-ship wake prototype.

A) Ground truth / runtime candidate: world-frame LINEAR SPECTRAL wave sim (exact deep-water
   dispersion per Fourier mode) forced by the moving hull's volume change, plus world-frame foam.
     eta_t = |k| phi + S,   phi_t = -g eta        (per mode, integrated exactly as a rotation)
     S = d/dt[ T * m(x,y,t) ] * (1-e^{-kT})/(kT)   (hull occupancy rate, depth-attenuated)
   Any path works (turns, speed changes, reversing) because nothing assumes steady motion.
B) Cheap path: warp the STRAIGHT steady bake (wakebake2.bake2) along the path history
   (pixel -> arc length s behind the ship + signed lateral offset d), rigid ship frame near the hull.
"""
import time
import numpy as np
from scipy import fft as sfft
from scipy.ndimage import gaussian_filter, distance_transform_edt
from scipy.spatial import cKDTree

G = 9.81
KN = 0.5144


def half_breadth(xs, L, B, n_fore, n_aft):
    xi = 2 * xs / L
    n = np.where(xi > 0, n_fore, n_aft)
    h = 0.5 * B * np.clip(1 - np.abs(xi) ** n, 0, None)
    return np.where(np.abs(xi) <= 1, h, 0.0)


def hull_cov(PX, PY, cx, cy, psi, L, B, nf, na, dx):
    """Analytic anti-aliased waterplane coverage of a hull at (cx,cy), heading psi."""
    c, s = np.cos(psi), np.sin(psi)
    xs = c * (PX - cx) + s * (PY - cy)
    ys = -s * (PX - cx) + c * (PY - cy)
    h = half_breadth(xs, L, B, nf, na)
    return np.clip((h - np.abs(ys)) / dx + 0.5, 0, 1) * (h > 0), xs, ys


def kinematics(L, U0, *, straight=3.0, R_over_L=2.5, turn_deg=220, beta_max_deg=None, speed_loss=0.25,
               dt=0.1, ramp_s=8.0):
    """CG track for: straight run, then a steady left turn (rudder ramp, drift angle, speed loss).
    Drift angle at the CG ~ atan(x_pivot/R), pivot point ~0.33 L forward of the CG (approx)."""
    R = R_over_L * L
    beta_max = np.radians(beta_max_deg) if beta_max_deg is not None else np.arctan(0.33 / R_over_L)
    x, y, chi, t = 0.0, 0.0, 0.0, 0.0
    U = U0
    t_turn = straight * L / U0
    out = []
    turned = 0.0
    while turned < np.radians(turn_deg):
        a = np.clip((t - t_turn) / ramp_s, 0, 1)
        a = a * a * (3 - 2 * a)
        U = U0 * (1 - speed_loss * a) if t > t_turn else U0
        r = a * U / R
        beta = a * beta_max
        chi += r * dt
        if t > t_turn:
            turned += r * dt
        x += U * np.cos(chi) * dt
        y += U * np.sin(chi) * dt
        out.append((t, x, y, chi, chi + beta, U, r, beta))
        t += dt
    return np.array(out)  # t, x, y, course, heading, U, r, beta


def spectral_sim(L, B, T, U0, path, *, nf, na, entrance_deg, wash=1.0, dx=2.0, gamma=0.03,
                 tau_fresh=3.0, tau_resid=15.0, tau_wash=45.0, resid_share=0.5, margin=1.2):
    t0 = time.perf_counter()
    xs_, ys_ = path[:, 1], path[:, 2]
    x0, x1 = xs_.min() - margin * L, xs_.max() + margin * L
    y0, y1 = ys_.min() - margin * L, ys_.max() + margin * L
    nx, ny = sfft.next_fast_len(int((x1 - x0) / dx)), sfft.next_fast_len(int((y1 - y0) / dx))
    x = x0 + dx * np.arange(nx); y = y0 + dx * np.arange(ny)
    kx = 2 * np.pi * sfft.rfftfreq(nx, dx); ky = 2 * np.pi * sfft.fftfreq(ny, dx)
    KX, KY = np.meshgrid(kx, ky)
    K = np.hypot(KX, KY); K[0, 0] = 1e-6
    W = np.sqrt(G * K)
    att = (1 - np.exp(-K * T)) / (K * T)
    eta_h = np.zeros(K.shape, np.complex128); phi_h = np.zeros_like(eta_h)
    dt = path[1, 0] - path[0, 0]
    cw, sw = np.cos(W * dt), np.sin(W * dt)
    damp = np.exp(-gamma * dt)

    beta_e = np.radians(entrance_deg)
    FrD = U0 / np.sqrt(G * T)
    Zb = 2.2 * U0 ** 2 / G * np.tan(beta_e) / (np.cos(beta_e) * (1 + FrD))
    FrL = U0 / np.sqrt(G * L)
    bow_white = np.clip((Zb - 0.5) / 2.5, 0, 1)
    band = B * (0.5 + 2.0 * max(FrL - 0.5, 0.0))

    fresh = np.zeros((ny, nx), np.float32); resid = np.zeros_like(fresh); washf = np.zeros_like(fresh)
    win = int(0.75 * L / dx) + 4  # local window half-size in cells
    m_prev = np.zeros((ny, nx), np.float32)
    scale = None
    t_cal = path[0, 0] + 2.5 * L / U0
    sm = lambda a, lo, hi: (lambda u: u * u * (3 - 2 * u))(np.clip((a - lo) / (hi - lo), 0, 1))
    stats = dict(fft=0.0, foam=0.0)
    for k_step, (t, cx, cy, chi, psi, U, r, beta) in enumerate(path):
        i0 = int((cx - x0) / dx); j0 = int((cy - y0) / dx)
        sl = (slice(max(j0 - win, 0), j0 + win), slice(max(i0 - win, 0), i0 + win))
        PX, PY = np.meshgrid(x[sl[1]], y[sl[0]])
        m_loc, xs, ys = hull_cov(PX, PY, cx, cy, psi, L, B, nf, na, dx)
        m = np.zeros_like(m_prev); m[sl] = m_loc
        S = T * (m - m_prev) / dt
        m_prev = m
        ta = time.perf_counter()
        Sh = sfft.rfft2(S, workers=-1) * att
        # exact free evolution (rotation in (eta, phi*k/w) space) + forcing (splitting) + damping
        e = eta_h * cw + (K / W) * phi_h * sw
        p = phi_h * cw - (G / W) * eta_h * sw
        eta_h = (e + Sh * dt) * damp
        phi_h = p * damp
        eta = sfft.irfft2(eta_h, s=(ny, nx), workers=-1)
        stats["fft"] += time.perf_counter() - ta

        tb = time.perf_counter()
        if scale is None and t >= t_cal:
            stem = (np.abs(xs - 0.5 * L) < 0.06 * L) & (np.abs(ys) < B) & (m_loc < 0.5)
            loc = eta[sl]
            s_ = 1.0 if loc[stem].max() >= -loc[stem].min() else -1.0
            scale = s_ * Zb / max(abs(loc[stem]).max(), 1e-9)
        # foam (world frame, local window only for sources)
        df = np.exp(-dt / tau_fresh); dr = np.exp(-dt / tau_resid); dw = np.exp(-dt / tau_wash)
        resid *= dr; resid += resid_share * fresh * (1 - df); fresh *= df; washf *= dw
        if scale is not None:
            le = eta[sl] * scale
            gy, gx = np.gradient(le, dx)
            slope = np.hypot(gx, gy)
            d_out = distance_transform_edt(m_loc < 0.5) * dx
            along = sm(xs / L + 0.5, 0.3, 1.0)
            src_hull = np.exp(-(d_out / (0.04 * B + 1.5 * dx)) ** 2) * (m_loc < 0.5) * along * bow_white * (np.abs(xs) < 0.55 * L)
            aft_w = float(np.clip(0.3 + 2.0 * (FrL - 0.3), 0.3, 1.0))
            w_break = aft_w + (1 - aft_w) * sm(xs / L, -0.45, -0.15)
            src_b = sm(le / Zb, 0.5, 0.9) * sm(slope, 0.2, 0.4) * w_break * np.exp(-(d_out / band) ** 2) * bow_white * 1.2
            # turning: the outer bow shoulder sees a larger relative inflow -> boost outer side, cut inner
            side = np.sign(ys) * np.sign(r) if r != 0 else 0 * ys  # +1 = inner side (left in a left turn)
            turn_k = np.clip(abs(r) * L / max(U, 0.1), 0, 1)       # ~L/R
            src = np.clip(np.maximum(src_b, src_hull) * (1 - 0.6 * turn_k * side), 0, 1)
            fresh[sl] = np.maximum(fresh[sl], src)
            # wash at the transom (moves with heading; the stern swings OUT of the CG track in a turn)
            sx = cx - 0.5 * L * np.cos(psi); sy = cy - 0.5 * L * np.sin(psi)
            ww = 0.3 * B * np.sqrt(wash)
            dist2 = (PX - sx) ** 2 + (PY - sy) ** 2
            washf[sl] = np.maximum(washf[sl], np.exp(-dist2 / ww ** 2) * min(U / 10, 1) * 0.8 * wash)
        if k_step % 20 == 19:  # slow lateral spreading of old foam (world frame diffusion)
            D = 0.005 * B * U0
            sig = np.sqrt(2 * D * 20 * dt) / dx
            pk = washf.max()
            washf = gaussian_filter(washf, sig)
            resid = gaussian_filter(resid, sig)
            if pk > 0:
                washf *= (pk / max(washf.max(), 1e-6)) ** 0.75
        stats["foam"] += time.perf_counter() - tb
    eta = eta * scale
    info = dict(nx=nx, ny=ny, dx=dx, steps=len(path), t_total=time.perf_counter() - t0, Zb=Zb, **stats)
    return x, y, eta.astype(np.float32), dict(fresh=fresh, resid=resid, turb=washf), m, info


def warp_bake(L, B, T, path, bake_out, x, y, *, near=0.6, far=1.2):
    """Approach B: sample the straight steady bake along the path history."""
    bx, by, beta_eta, F, bm, _, binfo = bake_out
    t, cx, cy, chi, psi = path[-1, :5]
    # dense CG track with arc length measured back from the current CG
    P = path[:, 1:3]
    seg = np.r_[0, np.cumsum(np.hypot(*np.diff(P, axis=0).T))]
    s_back = seg[-1] - seg
    tree = cKDTree(P)
    X, Y = np.meshgrid(x, y)
    d, idx = tree.query(np.c_[X.ravel(), Y.ravel()])
    idx = idx.reshape(X.shape); d = d.reshape(X.shape)
    tx, ty = np.cos(path[idx, 3]), np.sin(path[idx, 3])  # track tangent (course) at that point
    lat = -(X - P[idx, 0]) * ty + (Y - P[idx, 1]) * tx       # signed lateral (left +)
    s = s_back[idx]
    # far: path space; near: rigid ship frame
    c, s_ = np.cos(psi), np.sin(psi)
    xr = c * (X - cx) + s_ * (Y - cy); yr = -s_ * (X - cx) + c * (Y - cy)
    w = np.clip((s - near * L) / ((far - near) * L), 0, 1)
    w = w * w * (3 - 2 * w)
    w = np.where(xr > -0.5 * L, 0.0, w)  # alongside / ahead of the hull is always rigid
    qx = (1 - w) * xr + w * (-s)
    qy = (1 - w) * yr + w * lat
    from scipy.ndimage import map_coordinates
    bdx = bx[1] - bx[0]
    ci = (qx - bx[0]) / bdx; cj = (qy - by[0]) / bdx
    samp = lambda f: map_coordinates(f, [cj, ci], order=1, mode="constant", cval=0.0)
    eta = samp(beta_eta)
    out = {k: samp(v) for k, v in F.items()}
    return eta, out


def ribbon_foam(L, B, U0, path, x, y, *, wash=1.0, tau_wash=45.0, k_spread=0.10, keep=0.75):
    """Approach C foam: wash as a function of (age, lateral offset from the STERN track).
    This is what a trail-ribbon shader evaluates: per-vertex age, width w(age), gaussian across.
    Foam is material left in the world, so path space is exact for it (unlike waves)."""
    t, cx, cy, chi, psi, U = (path[:, i] for i in range(6))
    sx = cx - 0.5 * L * np.cos(psi); sy = cy - 0.5 * L * np.sin(psi)   # transom track (swings out in turns)
    P = np.c_[sx, sy]
    tree = cKDTree(P)
    X, Y = np.meshgrid(x, y)
    d, idx = tree.query(np.c_[X.ravel(), Y.ravel()])
    d = d.reshape(X.shape); idx = idx.reshape(X.shape)
    age = t[-1] - t[idx]
    dist_back = np.cumsum(np.r_[0, np.hypot(np.diff(sx), np.diff(sy))]); dist_back = dist_back[-1] - dist_back
    w0 = 0.3 * B * np.sqrt(wash)
    w = np.sqrt(w0 ** 2 + (k_spread ** 2) * B * dist_back[idx] * 2.0)
    inten = 1.6 * wash * np.minimum(U[idx] / 10, 1) * np.exp(-age / tau_wash) * (w0 / w) ** (1 - keep)
    ramp = np.clip(dist_back[idx] / (0.6 * B), 0, 1)
    # only the part of the track behind the transom (exclude the end cap ahead of the newest point)
    tx, ty = np.cos(psi[idx]), np.sin(psi[idx])
    along = (X - sx[idx]) * tx + (Y - sy[idx]) * ty
    cap = np.where((idx == len(t) - 1) & (along > 0), 0.0, 1.0)
    return np.clip(inten * np.exp(-(d / w) ** 2) * ramp * cap, 0, 1.5).astype(np.float32)


def spectral_local(L, B, T, U0, path, *, nf, na, entrance_deg, n=384, cells_per_L=80, gamma=0.03,
                   sponge=0.14, sponge_every=1):
    """Runtime-shaped variant: a ship-FOLLOWING, world-ALIGNED grid (n x n). When the ship moves a
    whole cell the grid scrolls by an integer shift (exact: a phase ramp in Fourier space).
    Waves leaving the box are absorbed by a sponge band so the periodic wrap never shows.
    Returns the final local field + its world origin; per-step timing is the runtime cost."""
    dx = L / cells_per_L
    kx = 2 * np.pi * sfft.rfftfreq(n, dx); ky = 2 * np.pi * sfft.fftfreq(n, dx)
    KX, KY = np.meshgrid(kx, ky)
    K = np.hypot(KX, KY); K[0, 0] = 1e-6
    Wm = np.sqrt(G * K); att = (1 - np.exp(-K * T)) / (K * T)
    dt = path[1, 0] - path[0, 0]
    cw, sw = np.cos(Wm * dt), np.sin(Wm * dt)
    damp = np.exp(-gamma * dt)
    # sponge: 1 in the interior, smoothly -> strong damping at the edges
    e = np.minimum(np.arange(n), np.arange(n)[::-1]) / (sponge * n)
    s1 = np.clip(e, 0, 1); s1 = s1 * s1 * (3 - 2 * s1)
    spg = (0.85 + 0.15 * np.minimum.outer(s1, s1)).astype(np.float32) ** sponge_every
    eta_h = np.zeros(K.shape, np.complex64); phi_h = np.zeros_like(eta_h)
    # grid origin follows the ship, kept on whole cells; ship sits a bit ahead of centre (wake is behind)
    def origin_for(cx, cy, psi):
        ox = cx - 0.5 * n * dx - 0.25 * n * dx * np.cos(psi)  # box centre trails the ship
        oy = cy - 0.5 * n * dx - 0.25 * n * dx * np.sin(psi)
        return np.floor(ox / dx) * dx, np.floor(oy / dx) * dx
    ox, oy = origin_for(*path[0, 1:3], path[0, 4])
    m_prev = np.zeros((n, n), np.float32)
    xs_l = np.arange(n) * dx; PX0, PY0 = np.meshgrid(xs_l, xs_l)
    tt = 0.0
    for k_step, (t, cx, cy, chi, psi, U, r, beta) in enumerate(path):
        t0 = time.perf_counter()
        nox, noy = origin_for(cx, cy, psi)
        sx_, sy_ = int(round((nox - ox) / dx)), int(round((noy - oy) / dx))
        if sx_ or sy_:  # scroll: content moves by -shift in the new frame
            ph = np.exp(1j * (KX * sx_ * dx + KY * sy_ * dx)).astype(np.complex64)
            eta_h *= ph; phi_h *= ph
            m_prev = np.roll(m_prev, (-sy_, -sx_), axis=(0, 1))
            ox, oy = nox, noy
        m, _, _ = hull_cov(PX0 + ox, PY0 + oy, cx, cy, psi, L, B, nf, na, dx)
        S = (T * (m - m_prev) / dt).astype(np.float32); m_prev = m
        Sh = sfft.rfft2(S, workers=-1) * att
        e_ = eta_h * cw + (K / Wm) * phi_h * sw
        p_ = phi_h * cw - (G / Wm) * eta_h * sw
        eta_h = ((e_ + Sh * dt) * damp).astype(np.complex64); phi_h = (p_ * damp).astype(np.complex64)
        if k_step % sponge_every == 0:
            eta_r = sfft.irfft2(eta_h, s=(n, n), workers=-1) * spg
            phi_r = sfft.irfft2(phi_h, s=(n, n), workers=-1) * spg
            eta_h = sfft.rfft2(eta_r, workers=-1).astype(np.complex64)
            phi_h = sfft.rfft2(phi_r, workers=-1).astype(np.complex64)
        tt += time.perf_counter() - t0
    eta = sfft.irfft2(eta_h, s=(n, n), workers=-1)
    return ox + xs_l, oy + xs_l, eta.astype(np.float32), m, dict(ms_per_step=tt / len(path) * 1e3, dx=dx, n=n)


def kinematics_schedule(L, U0, rudder, *, t_end, R_over_L=2.5, speed_loss=0.25, dt=0.1, tau_r=6.0, tau_u=20.0):
    """General path: rudder(t) in [-1, 1] (+ = left). Turn rate and drift follow with first-order lag,
    speed sags toward U0*(1 - speed_loss*|rudder|)."""
    x = y = chi = 0.0; U = U0; r = 0.0; out = []
    for t in np.arange(0, t_end, dt):
        a = rudder(t)
        U += ((U0 * (1 - speed_loss * abs(a))) - U) * dt / tau_u
        r += (a * U / (R_over_L * L) - r) * dt / tau_r
        beta = np.arctan(0.33 * r * L / max(U, 0.1))      # drift ~ atan(x_pivot / R_inst)
        chi += r * dt
        x += U * np.cos(chi) * dt; y += U * np.sin(chi) * dt
        out.append((t, x, y, chi, chi + beta, U, r, beta))
    return np.array(out)
