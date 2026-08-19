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
                Make("Ragged Tent", PropertyTier.Tent, 25),
                Make("Tent", PropertyTier.Tent, 40),
                Make("Canvas Tent", PropertyTier.Tent, 60),
                Make("Village Hut", PropertyTier.Hut, 90),
                Make("Small House", PropertyTier.SmallHouse, 200),
                Make("Family House", PropertyTier.House, 450),
                Make("Garden Apartment", PropertyTier.Apartment, 1000),
                Make("Apartment Tower", PropertyTier.Tower, 2200),
                Make("Luxury Tower", PropertyTier.Tower, 4800),
                Make("Skyscraper", PropertyTier.Skyscraper, 10000),
                // Commercial/business tiers past Skyscraper - keeps the same
                // ~2.2x-per-tier curve as the residential ladder above, so
                // wealth always has somewhere further to climb.
                Make("Retail Plaza", PropertyTier.Commercial, 22000),
                Make("Office Tower", PropertyTier.Office, 48000),
                Make("Corporate HQ", PropertyTier.Corporate, 105000),
                Make("Mega Complex", PropertyTier.MegaComplex, 230000),
            };
        }

        static PropertyDefinition Make(string name, PropertyTier tier, int buyPrice)
        {
            var def = ScriptableObject.CreateInstance<PropertyDefinition>();
            def.displayName = name;
            def.tier = tier;
            def.buyPrice = buyPrice;
            def.leaseDurationMonths = 12;
            return def;
        }
    }
}
