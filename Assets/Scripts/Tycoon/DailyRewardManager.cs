using System;
using System.Globalization;
using UnityEngine;

namespace Tycoon
{
    /// <summary>
    /// Escalating cash reward for returning on consecutive real-world days -
    /// the classic mobile retention hook. Streak resets if a day is missed;
    /// caps and repeats at a 7-day cycle so day 7+ stays a fixed high tier
    /// rather than growing forever. Reward scales with net worth (same idea
    /// as WorldEventManager's grants) so it stays meaningful at any stage of
    /// the game instead of a flat dollar amount that's trivial late-game.
    /// Independent of GameSaveData on purpose - the streak should survive
    /// even across a "New Game" in some future version, and checking it
    /// doesn't require a save to already exist.
    /// </summary>
    public class DailyRewardManager : MonoBehaviour
    {
        const string StreakKey = "TycoonDailyStreak_v1";
        const string LastClaimDateKey = "TycoonLastClaimDate_v1"; // yyyy-MM-dd, UTC, invariant culture

        static readonly float[] StreakPercent = { 0.03f, 0.04f, 0.05f, 0.06f, 0.08f, 0.1f, 0.15f };

        GameManager game;

        public int PendingStreakDay { get; private set; }
        public int PendingAmount { get; private set; }

        public void Init(GameManager owner)
        {
            game = owner;
        }

        /// <summary>Call once at startup, after any save has already been
        /// loaded (the reward scales off net worth, which needs the restored
        /// plot state to be meaningful). Returns true if today's reward hasn't
        /// been claimed yet - GameManager queues a banner and calls Claim()
        /// once the player taps through.</summary>
        public bool CheckPending()
        {
            string today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string lastClaim = PlayerPrefs.GetString(LastClaimDateKey, "");
            if (lastClaim == today) return false; // already claimed today

            bool consecutive = lastClaim != "" && DateTime.TryParseExact(lastClaim, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var lastDate) && lastDate == DateTime.UtcNow.Date.AddDays(-1);
            int streak = consecutive ? PlayerPrefs.GetInt(StreakKey, 0) + 1 : 1;

            int slot = (streak - 1) % StreakPercent.Length;
            int netWorth = game.Economy.ComputeNetWorth();
            int amount = Mathf.Max(20, Mathf.RoundToInt(netWorth * StreakPercent[slot]));

            PendingStreakDay = streak;
            PendingAmount = amount;
            return true;
        }

        public void Claim()
        {
            game.Economy.balance += PendingAmount;
            game.Economy.sessionProfit += PendingAmount;
            PlayerPrefs.SetInt(StreakKey, PendingStreakDay);
            PlayerPrefs.SetString(LastClaimDateKey, DateTime.UtcNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            PlayerPrefs.Save();

            game.World.SpawnCashEventEffect(EconomyManager.FormatSigned(PendingAmount), new Color(1f, 0.85f, 0.3f));
            game.Hud.Refresh();
        }
    }
}
