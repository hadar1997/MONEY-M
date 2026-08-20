using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tycoon
{
    /// <summary>
    /// Settings panel reachable from a small gear button in the HUD - right
    /// now just holds Reset Game. Reset wipes the save plus every persistent
    /// counter (daily streak, achievements) and reloads the scene, rather
    /// than trying to manually zero out every manager's in-memory state by
    /// hand - a fresh Awake() through the exact same startup path a real
    /// first launch takes is far less error-prone than hand-resetting a
    /// dozen fields across a dozen managers. Two-step confirm since this is
    /// destructive and unrecoverable.
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        GameManager game;

        GameObject panel;
        TextMeshProUGUI bodyText;
        Button primaryButton;
        TextMeshProUGUI primaryButtonLabel;
        Button secondaryButton;
        TextMeshProUGUI secondaryButtonLabel;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Init(GameManager owner)
        {
            game = owner;
        }

        public void Build()
        {
            var root = game.Hud.CanvasRoot;

            // Small gear button on the bottom row, clear of the speed/pause
            // buttons (which run from x=-220 to x=222) and well inside the
            // same safety margin the rest of the HUD uses.
            var gearBtn = UIFactory.CreateButton(root, "SettingsButton", new Color(0.2f, 0.2f, 0.24f, 0.88f),
                new Vector2(300, HudController.BottomRowY), new Vector2(70, 70), out var gearLabel);
            gearLabel.text = "⚙"; // gear glyph
            gearLabel.fontSize = 34;
            gearBtn.onClick.AddListener(Open);

            panel = UIFactory.CreateFullscreenImage(root, "SettingsPanel", new Color(0, 0, 0, 0.65f)).gameObject;
            var card = UIFactory.CreateBubblePanel(panel.transform, "SettingsCard", new Color(0.12f, 0.13f, 0.18f, 0.98f), Vector2.zero, new Vector2(680, 380));
            card.gameObject.AddComponent<FloatingBubble>();
            UIFactory.CreateText(card.transform, "Title", "Settings", 30, Color.white, new Vector2(0, 130), new Vector2(600, 60));
            bodyText = UIFactory.CreateText(card.transform, "Body", "", 18, new Color(0.85f, 0.85f, 0.88f), new Vector2(0, 45), new Vector2(600, 110));

            primaryButton = UIFactory.CreateButton(card.transform, "PrimaryButton", new Color(0.75f, 0.25f, 0.22f), new Vector2(0, -60), new Vector2(560, 76), out primaryButtonLabel);
            secondaryButton = UIFactory.CreateButton(card.transform, "SecondaryButton", new Color(0.3f, 0.32f, 0.38f), new Vector2(0, -160), new Vector2(560, 76), out secondaryButtonLabel);

            panel.SetActive(false);
        }

        void Open()
        {
            ShowMain();
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        void ShowMain()
        {
            bodyText.text = "Reset wipes your save and starts a brand new game. This cannot be undone.";
            primaryButtonLabel.text = "Reset Game";
            primaryButton.onClick.RemoveAllListeners();
            primaryButton.onClick.AddListener(ShowConfirm);

            secondaryButtonLabel.text = "Close";
            secondaryButton.onClick.RemoveAllListeners();
            secondaryButton.onClick.AddListener(Close);
        }

        void ShowConfirm()
        {
            bodyText.text = "Are you sure? Every property, all cash, and your whole streak will be gone for good.";
            primaryButtonLabel.text = "Yes, Reset Everything";
            primaryButton.onClick.RemoveAllListeners();
            primaryButton.onClick.AddListener(DoReset);

            secondaryButtonLabel.text = "Cancel";
            secondaryButton.onClick.RemoveAllListeners();
            secondaryButton.onClick.AddListener(ShowMain);
        }

        void DoReset()
        {
            // Speed/pause is a global engine setting, not scene state - without
            // this, resetting while paused or at 2x/3x would carry that into
            // the "fresh" game, with the new HUD's pause button showing the
            // wrong icon for it (its own paused flag starts false either way).
            Time.timeScale = 1f;

            SaveManager.DeleteSave();
            DailyRewardManager.DeleteProgress();
            AchievementManager.DeleteProgress();
            PlayerPrefs.Save();

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Close()
        {
            panel.SetActive(false);
        }
    }
}
