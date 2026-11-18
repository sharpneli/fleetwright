"""whose_salvo_ref.py - telling your own salvo from another ship's on the same target (1905-1925 first).

Companion to fire-control-research/whose-salvo.md. Pure Python, no imports beyond the standard library; runs in
well under a second:  python3 whose_salvo_ref.py

The model it tabulates (the note's proposal, every constant tagged there):
  - A spotter watches for his own salvo in a window of +/-W seconds about when he expects it. W is how well he knows
    the moment: about 15 s with no time-of-flight clock (Bernotti 1914 wanted 15-20 s between ships' salvos; a heavy
    column stands 10-15 s), about 5 s with a clock or buzzer that is used (USN 1930s: no ship fires within 5 s of
    another's salvo).
  - Each other ship on the target, of his calibre band, fires a salvo every T seconds at a phase he doesn't control.
    The number k of other salvos that land inside his window is floor(2W/T) or one more (by the fraction).
  - With k others in the window he tells his own with 1/(1 + c k): c = 1 if a salvo is no more his than any other's
    (a pure guess among k+1 groups), c = 0.6 if pattern, line and count give him something (the proposal: the same
    constant as the lone shell's today).
  - Sequence firing (the RN's GIC): the ships fire in turns in slots of s seconds each, so no window ever holds
    another salvo (with a clock), but a ship can fire only once every n*s seconds: the cost is rate, not confusion.

Tables:
  S1  P(told) by others on the target, timing, and c, for heavy guns (T 40 s) and medium (T 15 s)
  S2  Sequence firing's rate cost against the RN's 1922 war-game factors (2 ships x2, 3 x2.5, 4 x3)
  S3  The told share under a clock as a fire factor, against the same 1922 factors
"""

from itertools import product


def told_given_k(k, c):
    return 1.0 / (1.0 + c * k)


def p_told(n_others, w_s, t_s, c):
    """Expected chance of telling his own salvo with n_others same-band ships, each firing every t_s seconds at a
    random phase, inside his window of +/- w_s seconds."""
    span = 2.0 * w_s / t_s
    base = int(span)
    frac = span - base
    # each other ship puts base or base+1 salvos in the window; enumerate the 2^n cases
    total = 0.0
    for extra in product((0, 1), repeat=n_others):
        p = 1.0
        for e in extra:
            p *= frac if e else (1.0 - frac)
        k = n_others * base + sum(extra)
        total += p * told_given_k(k, c)
    return total


def table_s1():
    print("S1  P(told) for a salvo, by other same-band ships on the target")
    print("    W: +/-15 s with no clock, +/-5 s with a clock in use; c 0.6 (proposed) and 1.0 (pure guess)")
    for label, t_s in (("heavy, salvo every 40 s", 40.0), ("medium, salvo every 15 s", 15.0)):
        print(f"\n    {label}")
        print("    others   no clock c0.6   no clock c1.0   clock c0.6   clock c1.0")
        for n in range(0, 6):
            row = [p_told(n, w, t_s, c) for w in (15.0, 5.0) for c in (0.6, 1.0)]
            print(f"    {n:>6}   {row[0]:>13.2f}   {row[1]:>13.2f}   {row[2]:>10.2f}   {row[3]:>10.2f}")


def table_s2():
    print("\nS2  Sequence firing (GIC): each ship fires once every max(T, n*s); rate share = T / max(T, n*s)")
    print("    T 40 s (deliberate heavy fire); slots s of 13, 15, 20 and 30 s; the 1922 rules' per-ship share beside")
    rules = {1: 1.0, 2: 1.0, 3: 2.5 / 3, 4: 3.0 / 4}
    print("    ships   s=13   s=15   s=20   s=30   1922 rules")
    for n in range(1, 5):
        cells = [40.0 / max(40.0, n * s) for s in (13.0, 15.0, 20.0, 30.0)]
        print(f"    {n:>5}   " + "   ".join(f"{x:.2f}" for x in cells) + f"   {rules[n]:.2f}")


def table_s3():
    print("\nS3  Unsynchronised fire with a clock (W 5 s, T 40 s, c 0.6): told share as the fire factor")
    print("    ships on target   told share   x ships   1922 rules")
    rules = {1: 1.0, 2: 2.0, 3: 2.5, 4: 3.0, 5: None}
    for n in range(1, 6):
        p = p_told(n - 1, 5.0, 40.0, 0.6)
        r = rules[n]
        print(f"    {n:>15}   {p:>10.2f}   {n * p:>7.2f}   {('x%.1f' % r) if r else '-':>10}")
    print("    (no clock, W 15 s, same T and c:)")
    for n in range(1, 5):
        p = p_told(n - 1, 15.0, 40.0, 0.6)
        print(f"    {n:>15}   {p:>10.2f}   {n * p:>7.2f}")


if __name__ == "__main__":
    table_s1()
    table_s2()
    table_s3()
