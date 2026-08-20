using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tycoon
{
    /// <summary>
    /// The persistent Screen Space Overlay HUD, laid out for a portrait phone
    /// screen: a slim top bar (Balance, date) that never covers the map, a
    /// compact secondary-stats cluster (next unlock, market trend, profit, net
    /// worth, income) near the bottom, speed/pause controls, and the world-event
    /// confirmation modal. Endless game, so there's no mission/time-limit
    /// display and no win/lose banner. Reads state from
    /// EconomyManager/PlotManager/CalendarManager - owns no game logic itself.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        GameManager game;

        TextMeshProUGUI balanceText;
        TextMeshProUGUI nextUnlockText;
        TextMeshProUGUI profitText;
        TextMeshProUGUI netWorthText;
        public TextMeshProUGUI calendarText;
        TextMeshProUGUI monthlyIncomeText;
        TextMeshProUGUI marketTrendText;
        Image marketTrendPill;

        GameObject eventBannerPanel;
        Image eventBannerBg;
        TextMeshProUGUI eventBannerTitle;
        TextMeshProUGUI eventBannerSubtitle;
        Button eventConfirmButton;
        TextMeshProUGUI eventConfirmButtonLabel;
        CanvasGroup eventBannerGroup;
        Coroutine eventBannerRoutine;

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
            // Snaps UI elements to whole physical pixels instead of leaving
            // them at whatever fractional position the ScaleWithScreenSize
            // scale factor produces - without this, text/edges can look
            // faintly soft/pixelated purely from sub-pixel positioning, even
            // though TMP's own glyph rendering is already crisp.
            canvas.pixelPerfect = true;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Portrait phone reference, not desktop landscape - every pill below
            // is laid out to fit within this width with margin, matching the
            // target platform (Screen.orientation is locked to Portrait in
            // WorldBuilder.SetupCamera). matchWidthOrHeight = 1 (match HEIGHT):
            // this layout's most extreme coordinates are vertical (rows stacked
            // up to y=+-860), so height is the dimension that must never shrink -
            // matching width instead (as this used to) collapses the effective
            // canvas height on any wider-than-portrait viewport (e.g. the Unity
            // Editor's Game view in its usual landscape-ish aspect), pushing
            // every pill off-screen top and bottom at once. Matching height
            // guarantees the full vertical layout always fits; on a genuine
            // portrait phone it also keeps width very close to correct, since
            // real phone aspects are close to this 1080x1920 reference anyway.
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 1f;
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

            // Top bar is deliberately just ONE slim row - Balance and the date,
            // the two things worth a permanent glance. Everything else used to
            // stack four rows deep at the top, burying most of the map behind
            // UI; it now lives in a compact cluster near the bottom instead (see
            // BuildBottomStats), leaving the whole middle of the screen clear
            // for the thing the player actually looks at.
            const float topRowY = 900f;
            const float sideCol = 210f, sideW = 380f;

            var balancePill = UIFactory.CreateBubblePanel(root, "BalancePill", new Color(0.1f, 0.11f, 0.15f, 0.95f), new Vector2(-sideCol, topRowY), new Vector2(sideW, 84));
            UIFactory.CreateIconBadge(root, new Color(0.95f, 0.7f, 0.15f), new Vector2(-sideCol - 148, topRowY), 58);
            balanceText = UIFactory.CreateText(balancePill.transform, "Balance", "", 28, new Color(1f, 0.92f, 0.45f), new Vector2(30, 0), new Vector2(300, 66));
            balanceText.fontStyle = FontStyles.Bold;

            var calendarPill = UIFactory.CreateBubblePanel(root, "CalendarPill", new Color(0.22f, 0.28f, 0.4f, 0.95f), new Vector2(sideCol, topRowY), new Vector2(sideW, 84));
            calendarText = UIFactory.CreateText(calendarPill.transform, "CalendarText", "", 20, new Color(0.85f, 0.9f, 1f), Vector2.zero, new Vector2(340, 66));

            BuildBottomStats(root);
            BuildSpeedControls(root);
            BuildPauseButton(root);
            BuildEventBanner(root);
        }

        /// <summary>Secondary stats - the ones worth checking occasionally, not
        /// glancing at constantly - sit as a compact cluster just above the
        /// speed/pause row, out of the way of the map for the rest of the
        /// screen. Same +-400 safety margin as the top bar.</summary>
        void BuildBottomStats(Transform root)
        {
            var nextUnlockPill = UIFactory.CreateBubblePanel(root, "NextUnlockPill", new Color(0.1f, 0.11f, 0.15f, 0.94f), new Vector2(0, -560f), new Vector2(760, 72));
            UIFactory.CreateIconBadge(root, new Color(0.55f, 0.85f, 0.4f), new Vector2(-330, -560f), 50);
            nextUnlockText = UIFactory.CreateText(nextUnlockPill.transform, "NextUnlock", "", 19, new Color(0.78f, 0.96f, 0.68f), new Vector2(28, 0), new Vector2(660, 60));

            marketTrendPill = UIFactory.CreateBubblePanel(root, "MarketTrendPill", new Color(0.18f, 0.18f, 0.22f, 0.94f), new Vector2(0, -640f), new Vector2(760, 58));
            marketTrendText = UIFactory.CreateText(marketTrendPill.transform, "MarketTrendText", "", 18, Color.white, Vector2.zero, new Vector2(700, 52));

            const float chipY = -716f, chipW = 250f;
            var profitPill = UIFactory.CreateBubblePanel(root, "ProfitPill", new Color(0.2f, 0.7f, 0.3f), new Vector2(-260, chipY), new Vector2(chipW, 58));
            profitText = UIFactory.CreateText(profitPill.transform, "ProfitText", "", 16, Color.white, Vector2.zero, new Vector2(220, 52));

            var netWorthPill = UIFactory.CreateBubblePanel(root, "NetWorthPill", new Color(0.55f, 0.42f, 0.12f), new Vector2(0, chipY), new Vector2(chipW, 58));
            netWorthText = UIFactory.CreateText(netWorthPill.transform, "NetWorthText", "", 16, Color.white, Vector2.zero, new Vector2(220, 52));

            var monthlyIncomePill = UIFactory.CreateBubblePanel(root, "MonthlyIncomePill", new Color(0.16f, 0.4f, 0.38f), new Vector2(260, chipY), new Vector2(chipW, 58));
            monthlyIncomeText = UIFactory.CreateText(monthlyIncomePill.transform, "MonthlyIncomeText", "", 16, Color.white, Vector2.zero, new Vector2(220, 52));
        }

        // Bottom control row sits near the actual bottom of the (now much
        // taller, 1920-reference) portrait canvas, within thumb reach on a
        // phone - the old y=-480 only made sense on the old 1080-tall
        // reference, where it was already near the bottom edge.
        public const float BottomRowY = -840f;

        void BuildSpeedControls(Transform root)
        {
            // Plain "1x/2x/6x" labels, not repeated ▶ glyphs - the default TMP
            // font asset's atlas is static and only has ASCII/Latin-1/a little
            // punctuation baked in (see LiberationSans SDF.asset's
            // characterSequence), so ▶ silently rendered as nothing at all.
            // Also just more accurate: the fastest button is 6x, not 3x.
            speedButtonImages = new Image[3];
            speedButtonImages[0] = CreateSpeedButton(root, "Speed1x", "1x", new Vector2(-220, BottomRowY), 1f, 0);
            speedButtonImages[1] = CreateSpeedButton(root, "Speed2x", "2x", new Vector2(-120, BottomRowY), 2f, 1);
            // Fastest button jumps to 6x rather than 3x - with secondsPerMonth
            // = 30, that's a 5s/month pace (30/6) instead of 10s (30/3).
            speedButtonImages[2] = CreateSpeedButton(root, "Speed3x", "6x", new Vector2(-20, BottomRowY), 6f, 2);
            HighlightSpeed(0);
        }

        float lastSpeed = 1f;
        int lastSpeedSlot = 0;

        Image CreateSpeedButton(Transform root, string name, string label, Vector2 pos, float speed, int slot)
        {
            var btn = UIFactory.CreateButton(root, name, new Color(0.15f, 0.16f, 0.2f, 0.88f), pos, new Vector2(90, 64), out var lbl);
            lbl.text = label;
            btn.onClick.AddListener(() =>
            {
                Time.timeScale = speed;
                lastSpeed = speed;
                lastSpeedSlot = slot;
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
            var btn = UIFactory.CreateButton(root, "PauseButton", new Color(0.95f, 0.6f, 0.15f), new Vector2(180, BottomRowY), new Vector2(84, 84), out var lbl);
            lbl.fontSize = 30;
            lbl.text = "II";
            btn.onClick.AddListener(() =>
            {
                paused = !paused;
                lbl.text = paused ? ">" : "II"; // ">" not "▶" - see BuildSpeedControls' comment on TMP glyph coverage
                // Resume at whatever speed was active before pausing, not a hard
                // reset to 1x - pausing shouldn't cost the player their 2x/3x pick.
                Time.timeScale = paused ? 0f : lastSpeed;
                if (!paused) HighlightSpeed(lastSpeedSlot);
            });
        }

        /// <summary>Full-screen modal for WorldEventManager's announcements -
        /// raycastTarget is ON, so it genuinely blocks input everywhere except its
        /// own button: the event's actual effect (market shift or cash grant)
        /// only happens once the player taps through, not the moment it's
        /// rolled. The dim/strobe tint lives on this fullscreen backdrop; the
        /// actual message sits on a separate floating card on top of it, so
        /// the banner reads as "a card appeared over a dimmed scene" rather
        /// than text pasted directly onto a flat color wash.</summary>
        void BuildEventBanner(Transform root)
        {
            eventBannerBg = UIFactory.CreateFullscreenImage(root, "EventBanner", new Color(0f, 0f, 0f, 0f));
            eventBannerPanel = eventBannerBg.gameObject;
            eventBannerGroup = eventBannerPanel.AddComponent<CanvasGroup>();

            // Widths capped with the same extra margin as the HUD pills above
            // (not the full 1080 reference width) - safe on real phone aspects
            // narrower than this reference, not just the reference itself.
            var card = UIFactory.CreateBubblePanel(eventBannerPanel.transform, "EventCard", new Color(0.12f, 0.13f, 0.18f, 0.98f), Vector2.zero, new Vector2(880, 520));
            card.gameObject.AddComponent<FloatingBubble>();

            eventBannerTitle = UIFactory.CreateText(card.transform, "EventTitle", "", 44, Color.white, new Vector2(0, 130), new Vector2(800, 100));
            eventBannerTitle.fontStyle = FontStyles.Bold;
            eventBannerSubtitle = UIFactory.CreateText(card.transform, "EventSubtitle", "", 20, Color.white, new Vector2(0, 0), new Vector2(800, 140));

            eventConfirmButton = UIFactory.CreateButton(card.transform, "EventConfirmButton", new Color(0.95f, 0.95f, 0.95f, 0.95f), new Vector2(0, -180), new Vector2(280, 76), out eventConfirmButtonLabel);
            eventConfirmButtonLabel.color = new Color(0.15f, 0.14f, 0.13f);
            eventConfirmButtonLabel.fontSize = 26;

            eventBannerPanel.SetActive(false);
        }

        public bool EventConfirmationOpen => eventBannerPanel != null && eventBannerPanel.activeSelf;

        readonly Queue<Action> announcementQueue = new Queue<Action>();

        /// <summary>Sole public entry point for the full-screen confirmation
        /// banner - WorldEventManager, AchievementManager, and the daily-reward/
        /// offline-earnings launch popups all funnel through here instead of
        /// calling ShowEventConfirmation directly. Without this, two systems
        /// wanting a banner in the same tick (e.g. a world event firing the
        /// same month a net-worth milestone is crossed) would have the second
        /// call silently overwrite the first's text/listener before the player
        /// ever saw it - this queues it to show right after instead.</summary>
        public void QueueEventConfirmation(string title, string subtitle, Color accentColor, bool alarm, string buttonLabel, Action onConfirm)
        {
            announcementQueue.Enqueue(() => ShowEventConfirmation(title, subtitle, accentColor, alarm, buttonLabel, onConfirm));
            TryShowNextQueued();
        }

        void TryShowNextQueued()
        {
            if (EventConfirmationOpen || announcementQueue.Count == 0) return;
            announcementQueue.Dequeue().Invoke();
        }

        /// <summary>Pops in, optionally strobes red a few times to grab attention
        /// (alarm - only for a bad event, never the calmer "gift" ones), then sits
        /// and waits: the event doesn't actually take effect until the player taps
        /// the button, at which point onConfirm runs and the modal fades out.
        /// accentColor tints the background so Storm/War/Plague/Inflation/gifts
        /// each read as visually distinct, not just by their text. Private - go
        /// through QueueEventConfirmation instead, so nothing can bypass the queue.</summary>
        void ShowEventConfirmation(string title, string subtitle, Color accentColor, bool alarm, string buttonLabel, Action onConfirm)
        {
            eventBannerTitle.text = title;
            eventBannerSubtitle.text = subtitle;
            eventConfirmButtonLabel.text = buttonLabel;
            eventBannerPanel.transform.SetAsLastSibling(); // always render above any other open popup
            eventBannerPanel.SetActive(true);

            eventConfirmButton.onClick.RemoveAllListeners();
            eventConfirmButton.onClick.AddListener(() =>
            {
                // try/finally: if onConfirm throws (e.g. a bug in whatever it
                // grants), the banner must still close - an exception here used
                // to abort the rest of this listener, leaving the modal stuck
                // on screen forever with no way to dismiss it.
                try { onConfirm?.Invoke(); }
                finally { HideEventConfirmation(); }
            });

            if (eventBannerRoutine != null) StopCoroutine(eventBannerRoutine);
            eventBannerRoutine = StartCoroutine(EventBannerIntroRoutine(accentColor, alarm));
        }

        void HideEventConfirmation()
        {
            if (eventBannerRoutine != null)
            {
                StopCoroutine(eventBannerRoutine);
                eventBannerRoutine = null;
            }
            StartCoroutine(EventBannerFadeOutRoutine());
        }

        IEnumerator EventBannerIntroRoutine(Color accentColor, bool alarm)
        {
            const float popDuration = 0.15f;
            var tint = Color.Lerp(accentColor, Color.black, 0.35f);
            var calm = new Color(tint.r, tint.g, tint.b, 0.6f); // opaque enough to read as blocking, now that it is

            float t = 0f;
            while (t < popDuration)
            {
                t += Time.unscaledDeltaTime;
                eventBannerGroup.alpha = Mathf.Clamp01(t / popDuration);
                yield return null;
            }
            eventBannerGroup.alpha = 1f;
            eventBannerBg.color = calm;

            if (alarm)
            {
                // A few quick strobes toward a hot, saturated red - the screen's
                // own version of the sirens about to flash outside on the map.
                var hot = new Color(0.85f, 0.08f, 0.05f, 0.75f);
                const int pulses = 3;
                const float pulseDuration = 0.22f;
                for (int i = 0; i < pulses; i++)
                {
                    t = 0f;
                    while (t < pulseDuration)
                    {
                        t += Time.unscaledDeltaTime;
                        eventBannerBg.color = Color.Lerp(calm, hot, Mathf.Sin(Mathf.Clamp01(t / pulseDuration) * Mathf.PI));
                        yield return null;
                    }
                }
                eventBannerBg.color = calm;
            }

            eventBannerRoutine = null; // intro finished - now just idles, waiting for the button
        }

        IEnumerator EventBannerFadeOutRoutine()
        {
            const float fadeDuration = 0.3f;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                eventBannerGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            eventBannerPanel.SetActive(false);
            // Only now is the panel genuinely free - advance the queue here
            // rather than the moment the button was tapped, since the panel is
            // still (visually) open for the rest of this fade.
            TryShowNextQueued();
        }

        public void Refresh()
        {
            game.Plots.CheckTierUnlocks();
            game.Market.CheckManagerUnlock();
            game.Achievements.Check();
            // No "Balance:" label - the gold coin badge and bold styling already
            // say what this pill is, and a bare number reads faster at a glance.
            balanceText.text = EconomyManager.FormatMoney(game.Economy.balance);

            var (nextName, remaining) = game.Plots.NextUnlockInfo();
            nextUnlockText.text = nextName != null
                ? $"Next: {nextName} · {EconomyManager.FormatMoney(remaining)} to go"
                : "Every tier unlocked!";

            profitText.text = $"Profit {EconomyManager.FormatSigned(game.Economy.sessionProfit)}";
            netWorthText.text = $"Worth {EconomyManager.FormatMoney(game.Economy.ComputeNetWorth())}";
            monthlyIncomeText.text = $"{EconomyManager.FormatSigned(game.Market.ComputeMonthlyPassiveIncome())}/mo";

            // An active world event is the dominant story of what's happening to
            // the market right now, so it takes over this pill from the ambient
            // trend label for as long as it runs.
            bool eventActive = game.WorldEvents.HasActiveEvent;
            string trendLine = eventActive ? game.WorldEvents.ActiveStatusText() : game.Market.TrendLabel();
            marketTrendText.text = game.Market.ManagerUnlocked ? $"{trendLine} · Manager Active" : trendLine;
            var trendPillColor = eventActive ? game.WorldEvents.ActiveEventColor() : game.Market.TrendColor();
            marketTrendPill.color = Color.Lerp(trendPillColor, new Color(0.18f, 0.18f, 0.22f), 0.75f);
        }
    }
}
