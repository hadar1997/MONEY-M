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

        public const int Columns = 5;
        public const int Rows = 4;
        public const float CellSpacing = 1.7f;

        /// <summary>
        /// Balance required before unowned plots offer each catalog tier, so the
        /// map starts all-tents and only shows richer tiers once you can afford
        /// the climb. Index-aligned with PropertyCatalog.CreateDefault() - the
        /// first three (the tent variants) unlock almost immediately so early
        /// game already shows visual variety; Village Hut onward keeps the
        /// original pacing.
        /// </summary>
        static readonly int[] TierUnlockBalance = { 0, 50, 150, 1000, 3000, 7000, 15000, 30000, 60000, 120000 };
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
            game.World.RebuildBuildingMesh(state.view, newDef);
            state.view.Refresh();
            RefreshPriceLabel(state);
            state.expiresAt = Time.time + plotExpirySeconds;
        }

        public void UpdatePlotExpiry()
        {
            if (plots == null) return;
            foreach (var state in plots)
            {
                if (state.ownership != PropertyOwnership.Unowned) continue;
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
            state.view.priceTagText.text = text;
            state.view.priceTagPill.color = pillColor;
            state.view.priceTagBadge.color = state.lastDeltaPositive ? TrendUpColor : TrendDownColor;
            state.view.priceTagArrow.text = state.lastDeltaPositive ? "▲" : "▼";
        }
    }
}
