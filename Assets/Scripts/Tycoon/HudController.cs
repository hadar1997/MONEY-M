using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tycoon
{
    /// <summary>
    /// The persistent Screen Space Overlay HUD: Balance/Mission/Time/Profit/
    /// NetWorth/Calendar pills, speed and pause controls, and the win/lose
    /// banner. Reads state from EconomyManager/PlotManager/CalendarManager -
    /// owns no game logic itself.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        GameManager game;

        Text balanceText;
        Text missionText;
        public Text timeText;
        Text profitText;
        Text netWorthText;
        public Text calendarText;
        Text monthlyIncomeText;
        Text marketTrendText;
        Image marketTrendPill;

        public GameObject bannerPanel;
        public Text bannerText;

        Image[] speedButtonImages;

        /// <summary>The HUD's Screen Space Overlay canvas root - PropertyPopupController
        /// attaches its popup to this same canvas rather than creating a second one.</summary>
        public Transform CanvasRoot { get; private set; }

        public void Init(GameManager owner)
        {
            game = owner;
        }

        public void Build()
        {
            var canvasGO = new GameObject("Canvas", typeof(RectTransform));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                // Project's Player Settings use the Input System package exclusively,
                // so StandaloneInputModule (legacy UnityEngine.Input) throws at runtime.
                esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            var root = canvasGO.transform;
            CanvasRoot = root;
            var font = game.Font;

            // Three separate icon-badge pills (coin/star/clock), matching the
            // reference game's HUD instead of one flat bar with plain text.
            var balancePill = UIFactory.CreateImage(root, "BalancePill", new Color(0.1f, 0.11f, 0.15f, 0.94f), new Vector2(-700, 480), new Vector2(360, 78));
            UIFactory.CreateIconBadge(root, new Color(0.95f, 0.7f, 0.15f), new Vector2(-700 - 155, 480), 66);
            balanceText = UIFactory.CreateText(balancePill.transform, "Balance", "", 26, new Color(1f, 0.9f, 0.4f), new Vector2(22, 0), new Vector2(300, 62), font);

            var missionPill = UIFactory.CreateImage(root, "MissionPill", new Color(0.1f, 0.11f, 0.15f, 0.94f), new Vector2(0, 480), new Vector2(460, 78));
            UIFactory.CreateIconBadge(root, new Color(0.95f, 0.78f, 0.2f), new Vector2(-185, 480), 66);
            missionText = UIFactory.CreateText(missionPill.transform, "Mission", "", 24, Color.white, new Vector2(25, 0), new Vector2(400, 62), font);

            var timePill = UIFactory.CreateImage(root, "TimePill", new Color(0.1f, 0.11f, 0.15f, 0.94f), new Vector2(680, 480), new Vector2(340, 78));
            UIFactory.CreateIconBadge(root, new Color(0.4f, 0.65f, 0.95f), new Vector2(680 - 145, 480), 66);
            timeText = UIFactory.CreateText(timePill.transform, "Time", "", 26, new Color(0.75f, 0.9f, 1f), new Vector2(20, 0), new Vector2(280, 62), font);

            var profitPill = UIFactory.CreateImage(root, "ProfitPill", new Color(0.2f, 0.7f, 0.3f), new Vector2(300, 390), new Vector2(380, 64));
            profitText = UIFactory.CreateText(profitPill.transform, "ProfitText", "", 26, Color.white, Vector2.zero, new Vector2(360, 60), font);

            var calendarPill = UIFactory.CreateImage(root, "CalendarPill", new Color(0.25f, 0.32f, 0.45f), new Vector2(-320, 390), new Vector2(500, 64));
            calendarText = UIFactory.CreateText(calendarPill.transform, "CalendarText", "", 24, Color.white, Vector2.zero, new Vector2(480, 60), font);

            var netWorthPill = UIFactory.CreateImage(root, "NetWorthPill", new Color(0.55f, 0.42f, 0.12f), new Vector2(-230, 318), new Vector2(380, 56));
            netWorthText = UIFactory.CreateText(netWorthPill.transform, "NetWorthText", "", 22, Color.white, Vector2.zero, new Vector2(360, 52), font);

            var monthlyIncomePill = UIFactory.CreateImage(root, "MonthlyIncomePill", new Color(0.16f, 0.4f, 0.38f), new Vector2(230, 318), new Vector2(380, 56));
            monthlyIncomeText = UIFactory.CreateText(monthlyIncomePill.transform, "MonthlyIncomeText", "", 22, Color.white, Vector2.zero, new Vector2(360, 52), font);

            marketTrendPill = UIFactory.CreateImage(root, "MarketTrendPill", new Color(0.18f, 0.18f, 0.22f, 0.94f), new Vector2(0, 256), new Vector2(460, 52));
            marketTrendText = UIFactory.CreateText(marketTrendPill.transform, "MarketTrendText", "", 22, Color.white, Vector2.zero, new Vector2(440, 48), font);

            BuildSpeedControls(root);
            BuildPauseButton(root);
            BuildBanner(root);
        }

        void BuildSpeedControls(Transform root)
        {
            var font = game.Font;
            speedButtonImages = new Image[3];
            speedButtonImages[0] = CreateSpeedButton(root, "Speed1x", "▶", new Vector2(-100, -480), 1f, 0, font);
            speedButtonImages[1] = CreateSpeedButton(root, "Speed2x", "▶▶", new Vector2(0, -480), 2f, 1, font);
            speedButtonImages[2] = CreateSpeedButton(root, "Speed3x", "▶▶▶", new Vector2(100, -480), 3f, 2, font);
            HighlightSpeed(0);
        }

        Image CreateSpeedButton(Transform root, string name, string label, Vector2 pos, float speed, int slot, Font font)
        {
            var btn = UIFactory.CreateButton(root, name, new Color(0.15f, 0.16f, 0.2f, 0.88f), pos, new Vector2(90, 64), font, out var lbl);
            lbl.text = label;
            btn.onClick.AddListener(() =>
            {
                Time.timeScale = speed;
                HighlightSpeed(slot);
            });
            return btn.GetComponent<Image>();
        }

        void HighlightSpeed(int slot)
        {
            for (int i = 0; i < speedButtonImages.Length; i++)
                speedButtonImages[i].color = i == slot
                    ? new Color(0.25f, 0.55f, 0.3f, 0.95f)
                    : new Color(0.15f, 0.16f, 0.2f, 0.88f);
        }

        void BuildPauseButton(Transform root)
        {
            bool paused = false;
            var btn = UIFactory.CreateButton(root, "PauseButton", new Color(0.95f, 0.6f, 0.15f), new Vector2(880, -480), new Vector2(84, 84), game.Font, out var lbl);
            lbl.fontSize = 30;
            lbl.text = "II";
            btn.onClick.AddListener(() =>
            {
                paused = !paused;
                lbl.text = paused ? "▶" : "II";
                Time.timeScale = paused ? 0f : 1f;
                if (!paused) HighlightSpeed(0);
            });
        }

        void BuildBanner(Transform root)
        {
            bannerPanel = UIFactory.CreateFullscreenImage(root, "BannerPanel", new Color(0, 0, 0, 0.78f)).gameObject;
            bannerText = UIFactory.CreateText(bannerPanel.transform, "BannerText", "", 44, Color.white, Vector2.zero, new Vector2(1200, 200), game.Font);
            bannerPanel.SetActive(false);
        }

        public void Refresh()
        {
            game.Plots.CheckTierUnlocks();
            game.Market.CheckManagerUnlock();
            balanceText.text = $"Balance: {EconomyManager.FormatMoney(game.Economy.balance)}";
            missionText.text = game.Economy.balance >= game.missionTarget
                ? $"Mission complete! ({EconomyManager.FormatMoney(game.missionTarget)})"
                : $"Mission: {EconomyManager.FormatMoney(game.Economy.balance)} / {EconomyManager.FormatMoney(game.missionTarget)}";
            profitText.text = $"Profit: {EconomyManager.FormatMoney(game.Economy.sessionProfit)}";
            netWorthText.text = $"Net Worth: {EconomyManager.FormatMoney(game.Economy.ComputeNetWorth())}";
            monthlyIncomeText.text = $"Income: {EconomyManager.FormatSigned(game.Market.ComputeMonthlyPassiveIncome())}/mo";
            marketTrendText.text = game.Market.ManagerUnlocked ? $"{game.Market.TrendLabel()} · Manager Active" : game.Market.TrendLabel();
            marketTrendPill.color = Color.Lerp(game.Market.TrendColor(), new Color(0.18f, 0.18f, 0.22f), 0.75f);
        }

        public void RefreshTimeDisplay(float timeRemaining)
        {
            int totalSeconds = Mathf.CeilToInt(timeRemaining);
            int m = totalSeconds / 60;
            int s = totalSeconds % 60;
            timeText.text = $"Time: {m:00}:{s:00}";
        }

        public void ShowBanner(string text)
        {
            bannerPanel.SetActive(true);
            bannerText.text = text;
        }
    }
}
