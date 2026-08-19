using UnityEngine;

namespace Tycoon
{
    /// <summary>
    /// Drives the two recurring economic events: monthly rent settlement for
    /// leased plots, and the monthly market value drift that underlies every
    /// buy/rent/sell price in the game.
    /// </summary>
    public class MarketManager : MonoBehaviour
    {
        /// <summary>Monthly rent cost as a fraction of current market value (e.g. 0.05 = 5%/month).</summary>
        public float rentRateFraction = 0.05f;

        /// <summary>Rounded rent, floored at $1 - a depressed-enough property
        /// (marketValue can crash toward MarketManager's own $1 floor during a
        /// Plague/Crash) would otherwise round down to a nonsensical $0/mo lease.
        /// Shared by both PropertyPopupController call sites (the popup's shown
        /// price and SignLease's actual charge) so they can never disagree.</summary>
        public int ComputeMonthlyRent(float marketValue) => Mathf.Max(1, Mathf.RoundToInt(marketValue * rentRateFraction));

        /// <summary>Net worth at which the Property Manager upgrade unlocks and starts
        /// auto-renewing expired leases at the current market rate instead of leaving
        /// them parked in NeedsDecision until the player taps them.</summary>
        public int managerUnlockNetWorth = 5000;
        public bool ManagerUnlocked { get; private set; }

        /// <summary>Named market regime that biases every property's monthly move in the
        /// same direction at once, on top of each property's own independent noise -
        /// gives the market a "feel" (a real bull run, a real crash) instead of every
        /// plot just wandering independently.</summary>
        public enum MarketTrend { Stable, Bull, Bear, Boom, Crash }

        public MarketTrend CurrentTrend { get; private set; } = MarketTrend.Stable;
        int trendMonthsRemaining = 1;

        static readonly Color GainColor = new Color(0.3f, 0.9f, 0.4f);
        static readonly Color LossColor = new Color(1f, 0.35f, 0.3f);

        GameManager game;

        public void Init(GameManager owner)
        {
            game = owner;
        }

        /// <summary>Leased properties collect rent from their tenant once per
        /// in-game month instead of on a fast tick schedule. The rent is the
        /// amount locked in when the lease was signed (PropertyState.lockedRent),
        /// not the live market value - a signed lease shouldn't get more expensive
        /// or cheaper mid-term just because the market moved. Also counts down
        /// each lease's remaining months and ends it (or auto-renews it, once the
        /// Property Manager upgrade is unlocked) when it reaches zero - driven off
        /// this same monthly settlement rather than a second, separately-clocked
        /// timer, so the "months remaining" shown in the portfolio can never drift
        /// from the rent actually being collected.</summary>
        public void PayMonthlyRent()
        {
            var plots = game.Plots.plots;
            if (plots == null) return;
            foreach (var state in plots)
            {
                if (state.ownership != PropertyOwnership.Rented) continue;

                int net = state.lockedRent;
                game.Economy.balance += net;
                game.Economy.sessionProfit += net;

                game.World.SpawnFloatingIndicator(state, EconomyManager.FormatSigned(net), GainColor, coinPop: true);

                state.leaseMonthsRemaining--;
                if (state.leaseMonthsRemaining <= 0)
                {
                    game.Plots.OnRentExpired(state);
                    if (ManagerUnlocked) game.Popup.SignLease(state.index);
                }
            }
        }

        /// <summary>Every plot's market value drifts up or down each month - this
        /// drives the displayed buy price, rent cost, and (once Owned) sale
        /// price, so all of them stay dynamic rather than fixed catalog numbers.
        /// Decisions in progress (NeedsDecision) are frozen so the options don't
        /// shift under the player mid-choice. A shared MarketTrend biases every
        /// plot's move the same way that month (bull/bear/boom/crash), on top of
        /// per-plot independent noise, then rolls to a new trend on its own timer.</summary>
        public void FluctuateMarket()
        {
            if (--trendMonthsRemaining <= 0) RollTrend();

            var plots = game.Plots.plots;
            if (plots == null) return;
            foreach (var state in plots)
            {
                if (state.ownership == PropertyOwnership.NeedsDecision) continue;

                float changePercent = Random.Range(-0.12f, 0.12f) + TrendBiasPercent(CurrentTrend);
                float delta = state.marketValue * changePercent;
                state.marketValue = Mathf.Max(1f, state.marketValue + delta);
                state.lastDeltaPositive = delta >= 0;
                game.Plots.RefreshPriceLabel(state);

                if (state.ownership == PropertyOwnership.Owned)
                {
                    string arrow = delta >= 0 ? "▲" : "▼";
                    var color = delta >= 0 ? GainColor : LossColor;
                    game.World.SpawnFloatingIndicator(state, $"{arrow}{EconomyManager.FormatMoney(Mathf.RoundToInt(Mathf.Abs(delta)))}", color);
                }
            }
        }

        /// <summary>Weighted pick for the next market regime. Boom/Crash are sharp,
        /// rare, one-month spikes (always settling back into a calmer regime right
        /// after); Stable/Bull/Bear are the common multi-month backdrop.</summary>
        void RollTrend()
        {
            float roll = Random.value;
            if (roll < 0.06f) { CurrentTrend = MarketTrend.Crash; trendMonthsRemaining = 1; }
            else if (roll < 0.14f) { CurrentTrend = MarketTrend.Boom; trendMonthsRemaining = 1; }
            else if (roll < 0.44f) { CurrentTrend = MarketTrend.Bull; trendMonthsRemaining = Random.Range(2, 5); }
            else if (roll < 0.74f) { CurrentTrend = MarketTrend.Bear; trendMonthsRemaining = Random.Range(2, 5); }
            else { CurrentTrend = MarketTrend.Stable; trendMonthsRemaining = Random.Range(2, 4); }
        }

        static float TrendBiasPercent(MarketTrend trend) => trend switch
        {
            MarketTrend.Bull => 0.08f,
            MarketTrend.Bear => -0.08f,
            MarketTrend.Boom => 0.23f,
            MarketTrend.Crash => -0.27f,
            _ => 0f,
        };

        public string TrendLabel() => CurrentTrend switch
        {
            MarketTrend.Bull => "Bull Market",
            MarketTrend.Bear => "Bear Market",
            MarketTrend.Boom => "Boom!",
            MarketTrend.Crash => "Crash!",
            _ => "Stable Market",
        };

        public Color TrendColor() => CurrentTrend switch
        {
            MarketTrend.Bull => GainColor,
            MarketTrend.Boom => new Color(0.5f, 0.85f, 1f),
            MarketTrend.Bear => LossColor,
            MarketTrend.Crash => new Color(1f, 0.2f, 0.2f),
            _ => Color.white,
        };

        /// <summary>Sum of locked-in rent across every currently-leased plot -
        /// the dashboard's "Monthly Passive Income" figure.</summary>
        public int ComputeMonthlyPassiveIncome()
        {
            var plots = game.Plots.plots;
            if (plots == null) return 0;
            int total = 0;
            foreach (var state in plots)
                if (state.ownership == PropertyOwnership.Rented)
                    total += state.lockedRent;
            return total;
        }

        /// <summary>Checked on every HUD refresh, same cadence as PlotManager's tier
        /// unlocks, so the upgrade unlocks the moment net worth crosses the threshold
        /// rather than waiting for the next month to roll over.</summary>
        public void CheckManagerUnlock()
        {
            if (!ManagerUnlocked && game.Economy.ComputeNetWorth() >= managerUnlockNetWorth)
                ManagerUnlocked = true;
        }

        public int TrendMonthsRemaining => trendMonthsRemaining;

        /// <summary>For SaveManager only.</summary>
        public void RestoreState(MarketTrend savedTrend, int savedTrendMonthsRemaining, bool savedManagerUnlocked)
        {
            CurrentTrend = savedTrend;
            trendMonthsRemaining = savedTrendMonthsRemaining;
            ManagerUnlocked = savedManagerUnlocked;
        }
    }
}
