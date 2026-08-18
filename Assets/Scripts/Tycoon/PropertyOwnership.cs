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
        /// <summary>Rental period ended; player must buy, renew, or abandon.</summary>
        NeedsDecision
    }
}
