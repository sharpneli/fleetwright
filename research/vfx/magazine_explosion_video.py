"""Render the tier 3 magazine blast (day) as a real-time video.

Same setup as magx_strip_blast_day.png / magx_blast_day_realtime.mp4.
Needs magazine_explosion_ref.py in the same folder, plus numpy, scipy, pillow, and ffmpeg on PATH.

    python3 render_video.py                 # everything: sim -> frames (all cores) -> magx_blast_day.mp4
    python3 render_video.py --t-end 10      # shorter run
    python3 render_video.py --workers 8 --out vid

Frames are cached in --out: an interrupted run resumes where it stopped.
Rendering is the slow part (1-20 s per frame on CPU, later frames are heaviest).
"""
import argparse, os, pickle, subprocess, sys
from multiprocessing import Pool

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import magazine_explosion_ref as M

DT = 0.05                                   # sim step = frame step -> 20 fps real time
ship = dict(L=230.0, B=31.0, x0=-150.0, y0=-90.0)
mag_x = ship["x0"] - 0.27 * ship["L"]       # aft magazine group at ~0.77 L from the bow
broken = dict(ship, gap=(mag_x - 25, mag_x + 25))


def simulate(out, t_end):
    ev = M.MagazineEvent(mag_x, ship["y0"], "blast", 20000.0, seed=5,
                         vents=[(ship["x0"] - 20, ship["y0"], 0.4, 1.0)])   # Hood-like mainmast vent
    n = int(round(t_end / DT))
    for k in range(n + 1):
        if k:
            ev.step(DT)
        with open(f"{out}/state_{k:04d}.pkl", "wb") as f:
            pickle.dump((k * DT, ev), f)
    return n + 1


def render_one(path_png):
    from PIL import Image, ImageDraw
    state, png = path_png
    if os.path.exists(png):
        return
    with open(state, "rb") as f:
        t, ev = pickle.load(f)
    img = Image.fromarray(M.render([ev], t, ship=broken))
    ImageDraw.Draw(img).text((8, 6), f"t = {t:4.1f} s", fill=(235, 235, 235))
    img.save(png)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="vid")
    ap.add_argument("--t-end", type=float, default=30.0)
    ap.add_argument("--workers", type=int, default=os.cpu_count())
    ap.add_argument("--mp4", default="magx_blast_day.mp4")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    n = simulate(a.out, a.t_end)
    print(f"simulated {n} frames, rendering on {a.workers} workers...")
    jobs = [(f"{a.out}/state_{k:04d}.pkl", f"{a.out}/frame_{k:04d}.png") for k in range(n)]
    with Pool(a.workers) as p:
        for i, _ in enumerate(p.imap(render_one, jobs), 1):
            if i % 20 == 0 or i == n:
                print(f"  {i}/{n}", flush=True)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", str(round(1 / DT)),
                    "-i", f"{a.out}/frame_%04d.png", "-frames:v", str(n), "-c:v", "libx264",
                    "-pix_fmt", "yuv420p", "-crf", "16", "-preset", "slow", "-movflags", "+faststart", a.mp4],
                   check=True)
    print("wrote", a.mp4)
