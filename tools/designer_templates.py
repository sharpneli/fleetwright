"""Writes the designer's template catalogues, Content/Templates/*.json (game assets), from their sources:

- plants.json:  docs/shipgen/plant-templates.md, the `machinery.tech` blocks by year
- hulls.json:   docs/shipgen/hull-templates.md, the `hull.construction` blocks
- crew.json:    docs/shipgen/crew-templates.md, the `crew.standard` blocks (H0-H5)
- armour.json:  the `armour.materials` maps the shipped designs use (shipgen/designs/*.json), one per distinct scheme,
                weakest first (SIDE and DECK below)
- materials.json: material names for the pickers (structure: hull plating, superstructure; armour: per-part overrides)
- protection.json: docs/shipgen/protection-templates.md, the protection schemes (`scheme` blocks: the layout a family shared)

Each entry is {"name", "group", "note", "value"}: value is the block as the design JSON takes it.
Run from the repo root after editing a source: python -I tools/designer_templates.py
"""
import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Content", "Templates")


def blocks(md, key):
    """(section heading, bold label or None, the JSON object after `"key":`) for each ```json block in a doc."""
    text = open(os.path.join(ROOT, "docs", "shipgen", md), encoding="utf-8", newline="").read().replace("\r\n", "\n")
    section, label, out = None, None, []
    for m in re.finditer(r"^## ([^\n]+)$|^\*\*([^\n]+?)\*\*(?: \(`(\w+)`\))?|```json\n(.*?)\n```", text, re.M | re.S):
        if m.group(1):
            section, label = m.group(1).strip(), None
        elif m.group(2):
            label = (m.group(2).strip(), m.group(3))
        elif m.group(4):
            body = m.group(4).strip()
            if not body.startswith(f'"{key}"'):
                continue
            value = json.loads("{" + body + "}")[key]
            out.append((section, label, value))
    return out


def plants():
    return [{"name": v["name"], "group": sec, "note": (lab[1] or "") if lab else "", "value": v}
            for sec, lab, v in blocks("plant-templates.md", "tech")]


def hulls():
    return [{"name": v["name"], "group": "", "note": sec, "value": v} for sec, lab, v in blocks("hull-templates.md", "construction")]


def protection():
    return [{"name": lab[0] if lab else sec, "group": sec, "note": (lab[1] or "") if lab else "", "value": v}
            for sec, lab, v in blocks("protection-templates.md", "scheme")]


def crew():
    return [{"name": v["name"], "group": sec.split(":")[0], "note": "", "value": v} for sec, lab, v in blocks("crew-templates.md", "standard")]


# How well each material resists AP shell, for ordering the armour schemes in the picker only: rough ratings after
# Okun's comparisons (navweaps.com), Krupp cemented = 1.0. Face-hardened plate is rated as side armour; homogeneous
# plate as deck armour, where face hardening doesn't pay. The engine never sees these (DECISIONS.md: materials are
# names only); a new material needs a line here.
SIDE = {
    "wrought iron": 0.40, "mild steel": 0.40, "steel": 0.50, "compound": 0.50, "high-tensile steel": 0.55,
    "nickel steel": 0.65, "Harvey nickel steel": 0.80, "non-cemented armour": 0.80, "Krupp non-cemented": 0.80,
    "STS": 0.85, "special treatment steel": 0.85, "Class B": 0.85, "Vickers hardened": 0.90,
    "Witkowitz cemented": 0.95, "Terni cemented": 0.97, "cemented armour": 1.00, "Krupp cemented": 1.00,
    "Vickers cemented": 1.00, "Class A": 1.00, "Class A armour": 1.00, "Krupp cemented (KC n/A)": 1.05,
}
DECK = {
    "wrought iron": 0.60, "mild steel": 0.70, "steel": 0.70, "compound": 0.70, "high-tensile steel": 0.80,
    "nickel steel": 0.85, "Harvey nickel steel": 0.85, "cemented armour": 0.85, "Terni cemented": 0.85,
    "Witkowitz cemented": 0.85, "special steel": 0.95, "non-cemented armour": 0.95, "Krupp non-cemented": 0.95,
    "Molybdenum steel": 0.95, "Wotan": 1.00, "Wotan hart": 1.00, "STS": 1.00, "special treatment steel": 1.00,
    "Class B": 1.00,
}


def effectiveness(m):
    """A scheme's rating: mostly the belt, partly the decks."""
    return 0.7 * SIDE[m["belt"]] + 0.3 * DECK[m["decks"]]


def armour():
    """One scheme per distinct materials map, named by its side and deck armour, with the designs that use it."""
    schemes = {}
    folder = os.path.join(ROOT, "shipgen", "designs")
    for f in sorted(os.listdir(folder)):
        if not f.endswith(".json"):
            continue
        d = json.load(open(os.path.join(folder, f), encoding="utf-8"))
        m = (d.get("armour") or {}).get("materials")
        if not m:
            continue
        m = {k: v for k, v in m.items() if k != "flight_deck"}   # the designer adds it on carriers
        key = json.dumps(m, sort_keys=True)
        schemes.setdefault(key, (m, []))[1].append(f[:-5])
    out, names = [], {}
    for m, used in sorted(schemes.values(), key=lambda s: -len(s[1])):
        side, deck = m.get("belt", "?"), m.get("decks", "?")
        extra = sorted({v for k, v in m.items() if v not in (side, deck)})
        name = side if side == deck else f"{side}, {deck} decks"
        if extra:
            name += f" ({', '.join(extra)} elsewhere)"
        if name in names:   # a near twin (the conning tower or secondaries differ): the more used one stands
            continue
        names[name] = True
        out.append({"name": name, "group": "", "note": "as in " + ", ".join(used[:3]) + (" ..." if len(used) > 3 else ""),
                    "value": dict(sorted(m.items()))})
    out.sort(key=lambda e: (effectiveness(e["value"]), e["name"].lower()))   # weakest first, as the eras went
    return out


def materials():
    """Material names for the pickers: "structure" (hull plating, superstructure) and "armour" (a part's override),
    each the names the shipped designs use, most used first, plus a few of the period's own."""
    structure, armour_names = {}, {}
    folder = os.path.join(ROOT, "shipgen", "designs")
    for f in sorted(os.listdir(folder)):
        if not f.endswith(".json"):
            continue
        d = json.load(open(os.path.join(folder, f), encoding="utf-8"))
        for v in [((d.get("hull") or {}).get("plating") or {}).get("material"), (d.get("superstructure") or {}).get("material")]:
            if v:
                structure[v] = structure.get(v, 0) + 1
        for v in ((d.get("armour") or {}).get("materials") or {}).values():
            armour_names[v] = armour_names.get(v, 0) + 1
    for v in ["high-tensile steel", "aluminium alloy"]:
        structure.setdefault(v, 0)
    def ranked(c):
        return sorted(c, key=lambda k: (-c[k], k.lower()))
    return ([{"name": n, "group": "structure", "note": "", "value": n} for n in ranked(structure)]
            + [{"name": n, "group": "armour", "note": "", "value": n} for n in ranked(armour_names)])


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, entries in [("plants", plants()), ("hulls", hulls()), ("crew", crew()), ("armour", armour()), ("materials", materials()),
                          ("protection", protection())]:
        path = os.path.join(OUT, name + ".json")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(entries, f, indent=1, ensure_ascii=False)
            f.write("\n")
        print(f"{path}: {len(entries)}")


if __name__ == "__main__":
    main()
