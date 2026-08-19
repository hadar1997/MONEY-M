using UnityEngine;

namespace Tycoon
{
    [System.Serializable]
    public class PlotSaveData
    {
        public int ownership;
        public int catalogIndex;
        public float marketValue;
        public int purchasePrice;
        public int lockedRent;
        public int leaseMonthsRemaining;
        public bool lastDeltaPositive;
        public float expirySecondsRemaining;
    }

    [System.Serializable]
    public class GameSaveData
    {
        public int balance;
        public int sessionProfit;

        public int monthIndex;
        public int yearNumber;
        public float monthTimer;

        public int marketTrend;
        public int trendMonthsRemaining;
        public bool managerUnlocked;

        public bool hasActiveWorldEvent;
        public int activeWorldEventType;
        public int worldEventMonthsRemaining;
        public int worldEventCooldownMonths;

        public int unlockedTierIndex;

        public PlotSaveData[] plots;
    }

    /// <summary>
    /// Persists and restores the entire game via PlayerPrefs (JsonUtility-
    /// serialized) - without this, every session started completely fresh,
    /// which is the single biggest thing working against a player ever coming
    /// back to an "endless" game. Autosaves periodically and whenever the app is
    /// paused or quit; mobile backgrounding fires OnApplicationPause, not
    /// OnApplicationQuit, so both are handled.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        const string SaveKey = "TycoonSave_v1";
        const float AutosaveIntervalSeconds = 20f;

        GameManager game;
        float autosaveTimer;

        public void Init(GameManager owner)
        {
            game = owner;
        }

        public bool HasSave() => PlayerPrefs.HasKey(SaveKey);

        /// <summary>Called once a frame from GameManager.Update().</summary>
        public void Tick()
        {
            autosaveTimer += Time.unscaledDeltaTime; // keeps saving even while paused (Time.timeScale = 0)
            if (autosaveTimer < AutosaveIntervalSeconds) return;
            autosaveTimer = 0f;
            SaveGame();
        }

        public void SaveGame()
        {
            var plots = game.Plots.plots;
            var plotData = new PlotSaveData[plots.Length];
            for (int i = 0; i < plots.Length; i++)
            {
                var state = plots[i];
                plotData[i] = new PlotSaveData
                {
                    ownership = (int)state.ownership,
                    catalogIndex = game.Plots.IndexOfDefinition(state.definition),
                    marketValue = state.marketValue,
                    purchasePrice = state.purchasePrice,
                    lockedRent = state.lockedRent,
                    leaseMonthsRemaining = state.leaseMonthsRemaining,
                    lastDeltaPositive = state.lastDeltaPositive,
                    expirySecondsRemaining = Mathf.Max(0f, state.expiresAt - Time.time),
                };
            }

            var (hasEvent, eventType, eventMonthsRemaining, eventCooldown) = game.WorldEvents.GetSaveState();

            var data = new GameSaveData
            {
                balance = game.Economy.balance,
                sessionProfit = game.Economy.sessionProfit,

                monthIndex = game.Calendar.MonthIndex,
                yearNumber = game.Calendar.YearNumber,
                monthTimer = game.Calendar.MonthTimer,

                marketTrend = (int)game.Market.CurrentTrend,
                trendMonthsRemaining = game.Market.TrendMonthsRemaining,
                managerUnlocked = game.Market.ManagerUnlocked,

                hasActiveWorldEvent = hasEvent,
                activeWorldEventType = (int)eventType,
                worldEventMonthsRemaining = eventMonthsRemaining,
                worldEventCooldownMonths = eventCooldown,

                unlockedTierIndex = game.Plots.UnlockedTierIndex,
                plots = plotData,
            };

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        /// <summary>Only call after Plots.InitWorld() has already built every
        /// plot's view/mesh - this overwrites their state, it doesn't create
        /// them.</summary>
        public void LoadGame()
        {
            var data = JsonUtility.FromJson<GameSaveData>(PlayerPrefs.GetString(SaveKey));
            if (data == null) return;

            game.Economy.balance = data.balance;
            game.Economy.sessionProfit = data.sessionProfit;

            game.Calendar.RestoreState(data.monthIndex, data.yearNumber, data.monthTimer);
            game.Market.RestoreState((MarketManager.MarketTrend)data.marketTrend, data.trendMonthsRemaining, data.managerUnlocked);
            game.Plots.SetUnlockedTierIndex(data.unlockedTierIndex);
            game.WorldEvents.RestoreState(data.hasActiveWorldEvent, (WorldEventManager.WorldEventType)data.activeWorldEventType,
                data.worldEventMonthsRemaining, data.worldEventCooldownMonths);

            var plots = game.Plots.plots;
            for (int i = 0; i < data.plots.Length && i < plots.Length; i++)
            {
                var p = data.plots[i];
                var def = game.Plots.DefinitionAt(p.catalogIndex);
                game.Plots.RestorePlot(i, def, (PropertyOwnership)p.ownership, p.marketValue,
                    p.purchasePrice, p.lockedRent, p.leaseMonthsRemaining, p.lastDeltaPositive, p.expirySecondsRemaining);
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            // The only reliable "the player is leaving" signal on phones -
            // backgrounding an app fires Pause, often without ever firing Quit.
            if (pauseStatus) SaveGame();
        }

        void OnApplicationQuit()
        {
            SaveGame();
        }
    }
}
