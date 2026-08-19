using UnityEngine;

namespace Tycoon
{
    /// <summary>
    /// Static configuration for one property type: price, rent, and income.
    /// Create custom tuned variants via Assets/Create/Tycoon/Property Definition.
    /// </summary>
    [CreateAssetMenu(menuName = "Tycoon/Property Definition")]
    public class PropertyDefinition : ScriptableObject
    {
        public string displayName;
        public PropertyTier tier;
        public int buyPrice;
        public int leaseDurationMonths = 12;
    }
}
