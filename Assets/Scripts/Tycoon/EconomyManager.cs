using UnityEngine;

namespace Tycoon
{
    /// <summary>
    /// Owns cash on hand and session profit, and computes net worth. Doesn't
    /// know about the map or UI - GameManager wires it to those.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        public int startingBalance = 2000;
        public int balance;
        public int sessionProfit;

        GameManager game;

        public void Init(GameManager owner)
        {
            game = owner;
            balance = startingBalance;
        }

        /// <summary>Cash on hand plus the current market value of everything you
        /// own - whether you're holding it bare or it's currently leased out,
        /// buying it is what made it yours, so both count as an asset. This is
        /// what actually grows while you hold a property, even though the
        /// spendable balance itself doesn't move until you sell.</summary>
        public int ComputeNetWorth()
        {
            int total = balance;
            var plots = game.Plots.plots;
            if (plots != null)
            {
                foreach (var state in plots)
                    if (state.ownership != PropertyOwnership.Unowned)
                        total += Mathf.RoundToInt(state.marketValue);
            }
            return total;
        }

        public static string FormatMoney(int amount)
        {
            bool negative = amount < 0;
            int abs = Mathf.Abs(amount);
            string formatted = abs >= 1_000_000 ? $"{abs / 1_000_000f:0.##}M"
                : abs >= 1_000 ? $"{abs / 1_000f:0.##}K"
                : abs.ToString();
            return (negative ? "-$" : "$") + formatted;
        }

        public static string FormatSigned(int amount) => amount >= 0 ? "+" + FormatMoney(amount) : FormatMoney(amount);
    }
}
