namespace Tycoon
{
    /// <summary>
    /// Price/size tier of a property, cheapest to most expensive.
    /// </summary>
    public enum PropertyTier
    {
        Tent,
        Hut,
        SmallHouse,
        House,
        Apartment,
        Tower,
        Skyscraper,
        // Commercial/business tiers beyond residential, so wealth always has
        // somewhere further to climb instead of capping out at Skyscraper.
        Commercial,
        Office,
        Corporate,
        MegaComplex
    }
}
