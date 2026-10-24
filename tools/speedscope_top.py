"""Summarize a dotnet-trace speedscope file (docs/profiling.md).

    python -I tools/speedscope_top.py trace.speedscope.json [-n 25] [--focus Substring [--callers]] [--thread busiest|Substring]

Prints the busiest threads, the methods with the most self CPU time, the Fleetwright methods with the most
inclusive time, and with --focus the callees under every frame whose name contains Substring (with --callers: the nearest
Fleetwright frame above it instead, e.g. who triggers PollGCWorker). --thread keeps one thread: the busiest (a sim
thread) or those whose name contains Substring.
Times are summed over threads, so a Parallel.For body shows its CPU time, not its wall time.
"""
import argparse, json
from collections import defaultdict

ap = argparse.ArgumentParser()
ap.add_argument("path")
ap.add_argument("-n", type=int, default=25)
ap.add_argument("--focus")
ap.add_argument("--callers", action="store_true")
ap.add_argument("--thread")
args = ap.parse_args()

d = json.load(open(args.path, encoding="utf-8"))
frames = [f["name"] for f in d["shared"]["frames"]]
IDLE, CPU = "UNMANAGED_CODE_TIME", "CPU_TIME"

self_t = defaultdict(float)
incl_t = defaultdict(float)
callees = defaultdict(float)  # with --focus: time of each direct callee of a focused frame
threads = []
profiles = [p for p in d["profiles"] if p["type"] == "evented"]
if args.thread == "busiest":
    profiles = [max(profiles, key=lambda p: sum(1 for e in p["events"] if e["type"] == "O"))]
elif args.thread:
    profiles = [p for p in profiles if args.thread in p["name"]]
for p in profiles:
    stack, opened = [], []
    busy, last = 0.0, None
    for e in p["events"]:
        t = e["at"]
        if stack and last is not None:
            dt = t - last
            # dotnet-trace's leaves are pseudo-frames: charge CPU to the real method under it, drop idle time
            if frames[stack[-1]] == CPU and len(stack) > 1:
                self_t[stack[-2]] += dt
                busy += dt
            elif frames[stack[-1]] not in (IDLE, CPU):
                self_t[stack[-1]] += dt
                busy += dt
            if args.focus and frames[stack[-1]] != IDLE:
                for i in range(len(stack) - 1, -1, -1):
                    if args.focus in frames[stack[i]]:
                        if args.callers:
                            caller = next((stack[j] for j in range(i - 1, -1, -1) if "Fleetwright" in frames[stack[j]]), None)
                            callees["(none)" if caller is None else caller] += dt
                            break
                        callee = stack[i + 1] if i + 1 < len(stack) else stack[i]
                        callees["(self) " if callee == stack[i] or frames[callee] == CPU else callee] += dt
                        break
        last = t
        if e["type"] == "O":
            stack.append(e["frame"])
            opened.append(t)
        else:
            f, t0 = stack.pop(), opened.pop()
            if f not in stack:  # count recursion once
                incl_t[f] += t - t0
    threads.append((busy, p["name"]))


def name(f):
    return f if isinstance(f, str) else frames[f][:160]


def show(title, table):
    print(f"\n== {title} ==")
    for f, v in sorted(table.items(), key=lambda kv: -kv[1])[: args.n]:
        print(f"{v:10.1f}  {name(f)}")


print("unit:", d["profiles"][0].get("unit"))
print("\n== threads by CPU time ==")
for busy, tn in sorted(threads, reverse=True)[:8]:
    print(f"{busy:10.1f}  {tn}")
show("top self CPU time", self_t)
show("top inclusive time (Fleetwright frames)", {f: v for f, v in incl_t.items() if "Fleetwright" in frames[f]})
if args.focus:
    show(f"{'callers' if args.callers else 'callees'} of *{args.focus}* (CPU)", callees)
