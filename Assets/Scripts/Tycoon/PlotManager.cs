using UnityEngine;

namespace Tycoon
{
    /// <summary>
    /// Owns the grid layout and every plot's runtime state: which tier is
    /// currently offered, wealth-gated tier unlocking, and the persistent price
    /// tag's text/colors. Delegates actual mesh construction to WorldBuilder.
    /// </summary>
    public class PlotManager : MonoBehaviour
    {
        public float plotExpirySeconds = 20f;

        // Square grid on purpose: the ground plane is padded to a square around
        // whichever of Columns/Rows is larger, so a non-square grid (e.g. the
        // old 4x3) sits off-center within that square - hugging one pair of
        // edges more than the other - which reads as visibly crooked once the
        // camera can orbit around it. Equal Columns/Rows keeps the board itself
        // centered from every angle.
        public const int Columns = 4;
        public const int Rows = 4;
        public const float CellSpacing = 1.7f;

        /// <summary>
        /// Balance required before unowned plots offer each catalog tier, so the
        /// map starts all-tents and only shows richer tiers once you can afford
        /// the climb. Index-aligned with PropertyCatalog.CreateDefault() - the
        /// first three (the tent variants) unlock almost immediately so early
        /// game already shows visual variety; Village Hut onward keeps the
        /// original pacing, then continues past Skyscraper into the commercial
        /// tiers at roughly the same ~2x-per-tier growth so there's always a
        /// next unlock to work toward.
        /// </summary>
        static readonly int[] TierUnlockBalance =
            { 0, 50, 150, 1000, 3000, 7000, 15000, 30000, 60000, 120000, 250000, 520000, 1080000, 2250000 };
        int unlockedTierIndex;

        PropertyDefinition[] catalog;
        public PropertyState[] plots;

        GameManager game;

        static readonly Color UnownedPillColor = new Color(0.2f, 0.55f, 0.9f);   // blue - click to buy
        static readonly Color OwnedPillColor = new Color(0.25f, 0.75f, 0.35f);   // green - click to sell
        static readonly Color RentedPillColor = new Color(0.25f, 0.65f, 0.65f);
        static readonly Color ExpiredPillColor = new Color(0.95f, 0.55f, 0.15f);
        static readonly Color TrendUpColor = new Color(0.4f, 0.75f, 0.95f);
        static readonly Color TrendDownColor = new Color(0.95f, 0.5f, 0.2f);

        public void Init(GameManager owner)
        {
            game = owner;
            catalog = PropertyCatalog.CreateDefault();
        }

        public void InitWorld()
        {
            var mapRoot = new GameObject("Map").transform;
            mapRoot.SetParent(game.transform, false);

            game.World.BuildGroundAndRoads(mapRoot);

            int plotCount = Columns * Rows;
            plots = new PropertyState[plotCount];
            for (int i = 0; i < plotCount; i++)
            {
                var state = new PropertyState { index = i };
                plots[i] = state;
                var view = game.World.CreateBuildingShell(mapRoot, SlotPosition(i));
                view.index = i;
                view.Bind(state);
                RerollPlot(state);
            }
        }

        public static Vector3 SlotPosition(int index)
        {
            int col = index % Columns;
            int row = index / Columns;
            float x = (col - (Columns - 1) / 2f) * CellSpacing;
            float z = (row - (Rows - 1) / 2f) * CellSpacing;
            return new Vector3(x, 0f, z);
        }

        /// <summary>Picks a random tier from the currently-unlocked range, (re)builds
        /// its mesh, and resets the expiry timer. Used for first construction and
        /// whenever an unowned plot's timer runs out.</summary>
        public void RerollPlot(PropertyState state)
        {
            var newDef = catalog[Random.Range(0, unlockedTierIndex + 1)];
            state.definition = newDef;
            state.marketValue = newDef.buyPrice;
            // A brand-new listing has no market history yet - without this it could
            // show a stale ▲/▼ left over from whatever property previously occupied
            // this slot, until the next monthly tick happened to correct it.
            state.lastDeltaPositive = true;
            game.World.RebuildBuildingMesh(state.view, newDef);
            state.view.Refresh();
            RefreshPriceLabel(state);
            state.expiresAt = Time.time + plotExpirySeconds;
        }

        public void UpdatePlotExpiry()
        {
            if (plots == null) return;
            // Whichever plot the Buy/Cancel popup is currently open on is
            // frozen, same principle as MarketManager/WorldEventManager
            // already freeze a NeedsDecision plot mid post-lease-decision -
            // without this, a plot the player is actively deciding whether to
            // buy could silently reroll to a different (often much cheaper)
            // listing while the popup just sits there, and clicking "Buy"
            // afterward buys whatever it rerolled into, not what they saw.
            int openIndex = game.Popup.ActiveIndex;
            foreach (var state in plots)
            {
                if (state.ownership != PropertyOwnership.Unowned) continue;
                if (state.index == openIndex) continue;
                if (Time.time >= state.expiresAt) RerollPlot(state);
            }
        }

        /// <summary>Expands the pool of tiers RerollPlot can pick from; plots
        /// pick up richer tiers gradually as they individually expire, rather
        /// than the whole map upgrading at once.</summary>
        public void CheckTierUnlocks()
        {
            while (unlockedTierIndex < catalog.Length - 1 && game.Economy.balance >= TierUnlockBalance[unlockedTierIndex + 1])
                unlockedTierIndex++;
        }

        /// <summary>Name and $-remaining for the next tier the player hasn't
        /// unlocked yet - the "goal gradient effect" (motivation rises as a
        /// visible goal gets closer) only works if the goal is visible at all;
        /// unlocking silently in the background wastes the strongest lever this
        /// progression system has. Null once every tier is unlocked.</summary>
        public (string name, int remaining) NextUnlockInfo()
        {
            if (unlockedTierIndex >= catalog.Length - 1) return (null, 0);
            int threshold = TierUnlockBalance[unlockedTierIndex + 1];
            int remaining = Mathf.Max(0, threshold - game.Economy.balance);
            return (catalog[unlockedTierIndex + 1].displayName, remaining);
        }

        public int UnlockedTierIndex => unlockedTierIndex;

        /// <summary>For SaveManager only.</summary>
        public void SetUnlockedTierIndex(int value) => unlockedTierIndex = value;

        /// <summary>For SaveManager only - PropertyDefinition is a ScriptableObject,
        /// not directly JSON-serializable, so saves store a plot's catalog index
        /// instead and resolve it back through these two on load. Safe because
        /// PropertyCatalog.CreateDefault() always builds the same fixed list in the
        /// same order.</summary>
        public int IndexOfDefinition(PropertyDefinition def) => System.Array.IndexOf(catalog, def);
        public PropertyDefinition DefinitionAt(int catalogIndex) => catalog[catalogIndex];

        /// <summary>For SaveManager only - rebuilds one plot's mesh and state from
        /// saved data instead of RerollPlot's random pick. expirySecondsRemaining
        /// is used instead of a saved expiresAt because that field is measured
        /// against Time.time, which resets to 0 every session.</summary>
        public void RestorePlot(int index, PropertyDefinition def, PropertyOwnership ownership, float marketValue,
            int purchasePrice, int lockedRent, int leaseMonthsRemaining, bool lastDeltaPositive, float expirySecondsRemaining)
        {
            var state = plots[index];
            state.definition = def;
            state.ownership = ownership;
            state.marketValue = marketValue;
            state.purchasePrice = purchasePrice;
            state.lockedRent = lockedRent;
            state.leaseMonthsRemaining = leaseMonthsRemaining;
            state.lastDeltaPositive = lastDeltaPositive;
            state.expiresAt = Time.time + expirySecondsRemaining;

            game.World.RebuildBuildingMesh(state.view, def);
            state.view.Refresh();
            RefreshPriceLabel(state);
        }

        public void OnRentExpired(PropertyState state)
        {
            state.ownership = PropertyOwnership.NeedsDecision;
            state.view.Refresh();
            RefreshPriceLabel(state);
        }

        /// <summary>Text + pill color for a plot's persistent price tag - always
        /// reflects the current (fluctuating) market value, never a stale catalog
        /// price. Blue/green matches the reference game's buy/sell color language;
        /// the badge shows the direction of the most recent market move.</summary>
        public void RefreshPriceLabel(PropertyState state)
        {
            int marketPrice = Mathf.RoundToInt(state.marketValue);
            Color pillColor;
            string text;
            switch (state.ownership)
            {
                case PropertyOwnership.Unowned:
                    text = EconomyManager.FormatMoney(marketPrice);
                    pillColor = UnownedPillColor;
                    break;
                case PropertyOwnership.Rented:
                    // Locked-in rent for the lease term, not the live (fluctuating)
                    // market value - see PropertyState.lockedRent.
                    text = $"{EconomyManager.FormatMoney(state.lockedRent)}/mo";
                    pillColor = RentedPillColor;
                    break;
                case PropertyOwnership.Owned:
                    text = EconomyManager.FormatMoney(marketPrice);
                    pillColor = OwnedPillColor;
                    break;
                default: // NeedsDecision
                    text = "Expired!";
                    pillColor = ExpiredPillColor;
                    break;
            }
            bool changed = state.view.priceTagText.text != text;
            state.view.priceTagText.text = text;
            state.view.priceTagPill.color = pillColor;
            state.view.priceTagBadge.color = state.lastDeltaPositive ? TrendUpColor : TrendDownColor;
            // "^"/"v" not "▲"/"▼" - the default TMP font asset's atlas is
            // static and doesn't have the Geometric Shapes block baked in, so
            // those silently rendered as nothing (see HudController's speed
            // buttons for the same issue and full explanation).
            state.view.priceTagArrow.text = state.lastDeltaPositive ? "^" : "v";
            if (changed) state.view.PulsePriceTag();
        }
    }
}
