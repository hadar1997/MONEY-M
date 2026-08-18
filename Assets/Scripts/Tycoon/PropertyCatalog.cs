using UnityEngine;

namespace Tycoon
{
    /// <summary>
    /// Default seed data for the map's property tiers, cheapest (tent) to most
    /// expensive (skyscraper). Built at runtime so no .asset files are required.
    /// </summary>
    public static class PropertyCatalog
    {
        public static PropertyDefinition[] CreateDefault()
        {
            return new[]
            {
                // Three tent-tier entries so the cheapest plots on the map
                // already show visible variety (worn/patched vs. clean/kept),
                // not just a single repeated shape.
                Make("Ragged Tent", PropertyTier.Tent, 25, 9, 1),
                Make("Tent", PropertyTier.Tent, 40, 15, 2),
                Make("Canvas Tent", PropertyTier.Tent, 60, 22, 3),
                Make("Village Hut", PropertyTier.Hut, 90, 35, 4),
                Make("Small House", PropertyTier.SmallHouse, 200, 75, 9),
                Make("Family House", PropertyTier.House, 450, 170, 20),
                Make("Garden Apartment", PropertyTier.Apartment, 1000, 380, 45),
                Make("Apartment Tower", PropertyTier.Tower, 2200, 830, 100),
                Make("Luxury Tower", PropertyTier.Tower, 4800, 1800, 220),
                Make("Skyscraper", PropertyTier.Skyscraper, 10000, 3800, 480),
            };
        }

        static PropertyDefinition Make(string name, PropertyTier tier, int buyPrice, int rentPrice, int incomePerTick)
        {
            var def = ScriptableObject.CreateInstance<PropertyDefinition>();
            def.displayName = name;
            def.tier = tier;
            def.buyPrice = buyPrice;
            def.rentPrice = rentPrice;
            def.incomePerTick = incomePerTick;
            def.tickIntervalSeconds = 3f;
            def.leaseDurationMonths = 12;
            return def;
        }
    }
}
