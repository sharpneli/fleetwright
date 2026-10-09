"""Jet-phase test clip: aft magazine group, seen from a ship ~600 m off the beam at night.
Openings fail in sequence (Y gun ports, hood, X ports, deck ventilators, side scuttles,
engine-room vents at the mainmast), Y gunhouse roof lifts at 0.45 s, fireball at 0.9 s.
    python3 jet_clip.py OUT [n_frames=100] [workers]   -> OUT/frame_XXXX.png + magx_jet_phase_night.mp4
    python3 jet_clip.py OUT test                       -> a few stills """
import os, sys, subprocess
from multiprocessing import Pool
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import magazine_explosion_ref as M
import magazine_explosion_side as S

ship = dict(L=230.0, B=31.0, x0=-150.0, y0=-90.0)
h = ship["L"] / 2
mag_x = ship["x0"] - 0.27 * ship["L"]
broken = dict(ship, gap=(mag_x - 25, mag_x + 25))
X_x, Y_x = ship["x0"] - 0.46 * h, ship["x0"] - 0.62 * h      # turret centres
y0 = ship["y0"]


def openings():
    o = []
    for dy in (-2.2, 0, 2.2):   # gun ports, guns trained aft
        o.append(dict(pos=(Y_x - 7, y0 + dy, 11.5), dir=(-1, 0, 0.06), d=1.0, t_fail=0.00 + 0.02 * abs(dy), Lmax=90))
        o.append(dict(pos=(X_x - 7, y0 + dy, 14.0), dir=(-1, 0, 0.08), d=1.0, t_fail=0.10 + 0.02 * abs(dy), Lmax=90))
    o.append(dict(pos=(Y_x + 2, y0 + 3, 13.5), dir=(0.1, 0.1, 1), d=0.8, t_fail=0.05, Lmax=60))     # sighting hood
    o.append(dict(pos=(X_x + 2, y0 - 3, 15.5), dir=(0.1, -0.1, 1), d=0.8, t_fail=0.15, Lmax=60))
    for dx in (-12, 12):        # deck hatches / ammunition ventilators over the magazine
        for side in (-1, 1):
            o.append(dict(pos=(mag_x + dx, y0 + side * 10, 9.0), dir=(0, 0.35 * side, 1), d=1.0,
                          t_fail=0.12 + 0.05 * (dx > 0), Lmax=70))
    for dx in (-15, 0, 15):     # side scuttles facing the camera
        o.append(dict(pos=(mag_x + dx, y0 - ship["B"] / 2, 4.0), dir=(0, -1, 0.15), d=0.5, t_fail=0.22, Lmax=35))
    o.append(dict(pos=(ship["x0"] - 25, y0, 17.0), dir=(0, 0, 1), d=2.0, t_fail=0.35, Lmax=120))   # engine-room vents (Hood)
    o.append(dict(pos=(Y_x, y0, 12.0), dir=(0, 0, 1), d=8.5, t_fail=0.45, Lmax=120))                # barbette after roof lift
    return o


def make_event():
    return M.MagazineEvent(mag_x, y0, "blast", 20000.0, seed=5, openings=openings(), t_main=0.9, P_peak=3.0,
                           roof=(Y_x, y0, 13.5, 0.45), vents=[(ship["x0"] - 20, y0, 0.0, 1.0)])


cam = (mag_x + 70, y0 - 600, 18.0)
tgt = (mag_x + 30, y0, 95.0)
W, H = 1280, 720


def frame(args):
    k, t, ev, out = args
    from PIL import Image, ImageDraw
    img = Image.fromarray(S.render_side([ev], t, broken, cam, tgt, W=W, H=H, hfov=75.0, night=True))
    ImageDraw.Draw(img).text((10, 8), f"t = {t:4.2f} s", fill=(200, 200, 205))
    img.save(f"{out}/frame_{k:04d}.png")


if __name__ == "__main__":
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    test = len(sys.argv) > 2 and sys.argv[2] == "test"
    n = 100 if test or len(sys.argv) < 3 else int(sys.argv[2])
    workers = int(sys.argv[3]) if len(sys.argv) > 3 else os.cpu_count()
    keep = {6, 10, 16, 22, 40, 80} if test else set(range(n))
    ev, jobs = make_event(), []
    import copy
    for k in range(n):
        if k:
            ev.step(0.05)
        if k in keep:
            jobs.append((k, k * 0.05, copy.deepcopy(ev), out))
        if test and k >= max(keep):
            break
    print("particles at end:", len(ev.p))
    with Pool(workers) as p:
        for i, _ in enumerate(p.imap_unordered(frame, jobs), 1):
            if i % 10 == 0: print(f"{i}/{len(jobs)}", flush=True)
    if not test:
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", "20", "-i", f"{out}/frame_%04d.png",
                        "-frames:v", str(n), "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "16",
                        "-preset", "slow", "-movflags", "+faststart", "magx_jet_phase_night.mp4"], check=True)
