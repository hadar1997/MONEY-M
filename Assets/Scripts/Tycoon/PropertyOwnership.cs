namespace Tycoon
{
    /// <summary>
    /// Current ownership state of a plot on the map.
    /// </summary>
    public enum PropertyOwnership
    {
        Unowned,
        Rented,
        Owned,
        /// <summary>Lease ended; player must renew, sell, or keep holding bare.</summary>
        NeedsDecision
    }
}
