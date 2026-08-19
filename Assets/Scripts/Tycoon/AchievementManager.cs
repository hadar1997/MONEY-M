using UnityEngine;

namespace Tycoon
{
    /// <summary>
    /// One-time celebration + small cash bonus the first time the player
    /// crosses a net-worth milestone - gives the mid/long session concrete
    /// goals to chase beyond "number go up", and turns a few specific moments
    /// into something worth remembering. Checked on every Hud.Refresh() (net
    /// worth changes constantly), same cadence as tier unlocks / manager
    /// unlock - see PlotManager.CheckTierUnlocks / MarketManager.CheckManagerUnlock.
    /// </summary>
    public class AchievementManager : MonoBehaviour
    {
        struct Milestone
        {
            public readonly int netWorthThreshold;
            public readonly string title;
            public readonly float bonusPercent;
            public Milestone(int threshold, string title, float bonusPercent)
            {
                netWorthThreshold = threshold;
                this.title = title;
                this.bonusPercent = bonusPercent;
            }
        }

        // Strictly increasing thresholds - Check() only ever looks at the next
        // unclaimed one, so order here is load-bearing.
        static readonly Milestone[] Milestones =
        {
            new Milestone(5_000, "First Steps", 0.05f),
            new Milestone(25_000, "Rising Investor", 0.05f),
            new Milestone(100_000, "Six Figures!", 0.05f),
            new Milestone(500_000, "Half Millionaire", 0.05f),
            new Milestone(1_000_000, "Millionaire!", 0.08f),
            new Milestone(10_000_000, "Real Estate Mogul", 0.08f),
            new Milestone(100_000_000, "Tycoon Legend", 0.1f),
        };

        const string UnlockedCountKey = "TycoonMilestonesUnlocked_v1";

        GameManager game;
        int unlockedCount;

        public void Init(GameManager owner)
        {
            game = owner;
            unlockedCount = PlayerPrefs.GetInt(UnlockedCountKey, 0);
        }

        /// <summary>Unlocks (at most) one milestone per call, so a single big
        /// windfall that jumps several thresholds at once celebrates each one
        /// in turn on the next few Refresh() calls instead of skipping
        /// straight to the highest - Hud.Refresh() runs often enough (after
        /// every buy/sell/lease/monthly tick) that these still land in quick
        /// succession. Deliberately doesn't call Hud.Refresh() itself - this
        /// is invoked FROM Refresh(), and the rest of that same call already
        /// re-reads game.Economy.balance afterward.</summary>
        public void Check()
        {
            if (unlockedCount >= Milestones.Length) return;
            var next = Milestones[unlockedCount];
            int netWorth = game.Economy.ComputeNetWorth();
            if (netWorth < next.netWorthThreshold) return;

            unlockedCount++;
            PlayerPrefs.SetInt(UnlockedCountKey, unlockedCount);
            PlayerPrefs.Save();

            int bonus = Mathf.RoundToInt(netWorth * next.bonusPercent);
            game.Economy.balance += bonus;
            game.Economy.sessionProfit += bonus;

            game.Hud.QueueEventConfirmation(
                $"Achievement: {next.title}",
                $"Net worth passed {EconomyManager.FormatMoney(next.netWorthThreshold)}! Bonus: {EconomyManager.FormatSigned(bonus)}",
                new Color(0.95f, 0.8f, 0.25f), alarm: false, buttonLabel: "Nice!", onConfirm: null);
            game.World.SpawnCashEventEffect(EconomyManager.FormatSigned(bonus), new Color(1f, 0.85f, 0.3f));
        }
    }
}
