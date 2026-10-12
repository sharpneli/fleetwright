# The land

`land-110m.bin`, `land-50m.bin` and `land-10m.bin` are the world's land at 1:110M, 1:50M and 1:10M, read by
`Fleetwright.Sim.Core`'s `Land`. `tools/land_bake.py` bakes them from world-atlas 2.0.2's `land-*.json`, and its
docstring has the format.

- **Data:** [Natural Earth](https://www.naturalearthdata.com/), public domain.
- **Packaging:** [world-atlas](https://github.com/topojson/world-atlas) by Mike Bostock, ISC licence:

  > Copyright 2013-2019 Michael Bostock
  >
  > Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby
  > granted, provided that the above copyright notice and this permission notice appear in all copies.

This is a rough first Earth: modern coastlines and no ports. The user's Earth model replaces it later
(`docs/battle/plan.md`, "A rough Earth").
