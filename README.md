# TileWorldCreator

## Auto Tiling (Level brush)

`TileBiomeData` now supports a simple rotate-only auto tile system, driven by which
orthogonal sides of a cell already have a same-type tile placed:

- Enable **Use Auto Tiling** on the biome asset.
- Under **Auto Tiling**, fill the `Ground/Liquid/Decorative Auto Tiles` pools with one
  (or a few, for variety) prefab per shape:
  - **Isolated** – no connected neighbours.
  - **End Cap** – 1 connected neighbour, authored connecting North.
  - **Straight** – 2 opposite connected neighbours, authored connecting North+South.
  - **Corner** – 2 adjacent connected neighbours, authored connecting North+East.
  - **T Junction** – 3 connected neighbours, authored connecting North+East+South.
  - **Cross** – all 4 neighbours connected.
- The brush computes the neighbour mask, picks the matching shape, and rotates the
  prefab in 90° clockwise steps (never mirrors it) so it lines up with the actual
  neighbours - painting a tile also refreshes its already-placed neighbours so their
  shape/rotation stays correct.
- Leaving a pool empty falls back to a plain random tile from `Ground/Liquid/Decorative
  Tiles` with no rotation, so existing biomes keep working unchanged.

See `Script/Core/AutoTileMask.cs` for the exact bitmask/rotation convention.