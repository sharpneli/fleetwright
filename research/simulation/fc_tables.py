"""One of fire_control_ref.py's tables, for the sim's fire-control benches to print beside their own
(docs/battle/fire-control.md F1: `shipgen fc-converge --ref`, `fc-levels --ref`, `fc-history --ref`).

    python fc_tables.py converge|circle|levels|turning|history [N]

converge = table 5, circle = table 12, levels = table 6 (the reference calls it its ladder), turning = table 11, history = table 8; N runs a cell
(the reference's own defaults when left out).
"""
import sys

import fire_control_ref as f

TABLES = {
    "converge": lambda n: f.convergence_table(),
    "circle": lambda n: f.circle_convergence_table(),
    "levels": lambda n: f.ladder_table(n or 40),
    "turning": lambda n: f.turning_table(n or 40),
    "history": lambda n: f.history_table(n or 40),
}

if __name__ == "__main__":
    if len(sys.argv) < 2 or sys.argv[1] not in TABLES:
        sys.exit(__doc__)
    TABLES[sys.argv[1]](int(sys.argv[2]) if len(sys.argv) > 2 else 0)
