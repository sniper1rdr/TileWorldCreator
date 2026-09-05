# TileWorldCreator

## Auto Tiling (Level brush)

`TileBiomeData` now uses a rotate-only auto tile system for Ground/Liquid/Decorative
tiles: instead of a flat list of random prefabs, each type has 4 shape pools that the
brush picks between based on same-type neighbours, then rotates in 90° clockwise
steps (never mirrors) to line up with the actual neighbours:

- **Flat (ровная земля)** – tile is surrounded on all 4 sides + all 4 diagonals by the
  same tile type. Author with no particular connection features.
- **Straight Edge (прямой край)** – connected on 3 orthogonal sides, border on the 4th.
  Author connecting **North+East+South** (border facing West) at 0°.
- **Outer Corner (внешний угол)** – connected on 2 adjacent orthogonal sides, border on
  the other 2. Author connecting **North+East** (border facing South+West) at 0°.
- **Inner Corner (внутренний угол)** – all 4 orthogonal sides connect, but one diagonal
  neighbour is missing (a notch cut into an otherwise flat area). Author with the notch
  on the **NE** diagonal at 0°.

Painting a tile also refreshes all 8 same-type neighbours (orthogonal + diagonal) so
their shape/rotation stays correct as you keep painting. A single floating tile, a tile
with only 1 connection, or a 1-tile-wide strip (2 opposite sides connected) can't be
represented by a single one of these 4 whole-tile shapes - the brush layers 2 pieces on
top of each other in the same cell instead (e.g. 2 Outer Corner pieces rotated 180°
apart close off all 4 sides of a lone tile), so a placed tile never shows an open,
un-bordered side. See `AutoTileMask.ClassifyComposite` for the exact composition rules.

See `Script/Core/AutoTileMask.cs` for the exact bitmask/rotation convention.
