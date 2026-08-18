namespace Tycoon
{
    /// <summary>
    /// Runtime state of one plot on the map.
    /// </summary>
    public class PropertyState
    {
        public int index;
        public PropertyDefinition definition;
        public PropertyOwnership ownership = PropertyOwnership.Unowned;
        public PropertyTileView view;

        /// <summary>Game time (Time.time) at which an unowned plot re-rolls to a different tier.</summary>
        public float expiresAt;

        /// <summary>Current market value - fluctuates monthly for every plot, drives the
        /// displayed buy price, rent cost, and (once Owned) sale price.</summary>
        public float marketValue;

        /// <summary>Price actually paid when this plot was bought, snapshotted at purchase
        /// time so a later sale can show profit/loss against the live marketValue.</summary>
        public int purchasePrice;

        /// <summary>Monthly rent locked in when the current lease was signed. Stays fixed
        /// for the whole lease term even as marketValue keeps drifting in the background;
        /// only renewing (or signing a new lease after expiry) recomputes it from the
        /// then-current marketValue.</summary>
        public int lockedRent;

        /// <summary>Months left on the current lease, counted down by MarketManager's
        /// monthly settlement (not a separate real-time timer, so it can never drift
        /// from the rent actually being paid). Reaching 0 ends the lease.</summary>
        public int leaseMonthsRemaining;

        /// <summary>Direction of the most recent market move, drives the price tag's trend badge (▲/▼).</summary>
        public bool lastDeltaPositive = true;
    }
}
