# Fittings templates

Written by `shipgen templates` from `src/Fleetwright.Shipgen/Fittings.cs`; don't edit by hand. The designer's
templates are `Content/Templates/fittings.json`, from the same table.

A design's `fittings` block says how its gear is mounted. Its one number so far is `shock_grade`, G0 to G3
(`docs/battle/damage/17_service_network_faults.md` §2.2; decision 10 of docs/battle/shells-armour-damage.md):
how hard a shock trips its breakers, lamps and generators, from the hull's shock factor at the device. The
armour shock of a hit that didn't get through (E4), the near misses (E7) and later the service networks read it.
A device the ship doesn't carry can't fail, so an early ship with few breakers trips little whatever its grade.
A design that gives none is G0. The notes say who fitted to each grade; the design carries the grade, not the
year (old designs got theirs from their navy and their plant's year when the format moved to 4).

```json
"fittings": {"shock_grade": "G1"}
```

| Template | Grade |
|---|---|
| Unsecured (G0) | G0 |
| Secured (G1) | G1 |
| Shock-tested (G2) | G2 |
| Grade-A (G3) | G3 |

- **Unsecured (G0):** Gear as fitted, nothing secured against shock: breakers trip at a hull shock factor of 0.07 (half the time). Everyone before 1940, the RN to late 1940, the USN to early 1942; merchant-built and converted hulls throughout; the IJN and Kriegsmarine throughout [UNCERTAIN: no record of their securing work]. Before about 1910 a ship has few breakers to trip (fuses, knife switches, dynamos on their own governors).
- **Secured (G1):** Fuse and switch securing alterations: breakers at 0.22. The RN from late 1940 (after Southampton), the USN from 1942. Cheap: fixes and procedures, no weight.
- **Shock-tested (G2):** High-impact shock-tested gear on resilient mounts: breakers at 0.55 [EST]. USN new construction and refits from 1943; RN new construction from 1943 [UNCERTAIN]. +1-2% of electrical weight [EST], not built.
- **Grade-A (G3):** Post-war grade-A shock gear: breakers at 1.1 [EST].
