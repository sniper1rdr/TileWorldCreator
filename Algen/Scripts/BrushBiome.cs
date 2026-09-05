namespace AglenRealms.WorldCore
{
    public enum BrushBiome
    {
        Grasslands = 0,
        FrozenTundra = 1,
        GoldenDesert = 2,
        VolcanicAshlands = 3,
        RedForest = 4,
        MistySwamp = 5,
        Liquid = 6 // Legacy serialized value; migrated to Grasslands + Liquid layer on load.
    }
}
