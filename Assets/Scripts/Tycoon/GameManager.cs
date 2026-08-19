using Platformer.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Tycoon
{
    /// <summary>
    /// Top-level orchestrator: creates and wires every manager (World/Economy/
    /// Market/Plots/Calendar/Hud/Popup), drives the per-frame update dispatch,
    /// and handles map click routing. Endless - no win/lose condition; the
    /// player just keeps building for as long as they want. Attach to a single
    /// empty GameObject in an otherwise empty scene - every manager component
    /// gets added to that same object at runtime.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public Font Font { get; private set; }

        public WorldBuilder World { get; private set; }
        public EconomyManager Economy { get; private set; }
        public MarketManager Market { get; private set; }
        public PlotManager Plots { get; private set; }
        public CalendarManager Calendar { get; private set; }
        public HudController Hud { get; private set; }
        public PropertyPopupController Popup { get; private set; }
        public WorldEventManager WorldEvents { get; private set; }
        public SaveManager Save { get; private set; }
        public AchievementManager Achievements { get; private set; }
        public DailyRewardManager DailyReward { get; private set; }
        public SettingsController Settings { get; private set; }

        // Drag-to-rotate vs. tap-to-select: a press only opens a plot's popup if
        // the pointer never moved past DragThresholdPixels before release: past
        // that, it's treated as a camera-orbit drag instead and no click fires.
        const float DragThresholdPixels = 8f;
        const float DragYawPerPixel = 0.2f;
        bool pointerDown;
        Vector2 pointerDownPos;
        float dragDistanceAccum;

        void Awake()
        {
            QualitySettings.antiAliasing = 4;
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            World = gameObject.AddComponent<WorldBuilder>();
            Economy = gameObject.AddComponent<EconomyManager>();
            Market = gameObject.AddComponent<MarketManager>();
            Plots = gameObject.AddComponent<PlotManager>();
            Calendar = gameObject.AddComponent<CalendarManager>();
            Hud = gameObject.AddComponent<HudController>();
            Popup = gameObject.AddComponent<PropertyPopupController>();
            WorldEvents = gameObject.AddComponent<WorldEventManager>();
            Save = gameObject.AddComponent<SaveManager>();
            Achievements = gameObject.AddComponent<AchievementManager>();
            DailyReward = gameObject.AddComponent<DailyRewardManager>();
            Settings = gameObject.AddComponent<SettingsController>();

            World.Init(this);
            Economy.Init(this);
            Market.Init(this);
            Plots.Init(this);
            Calendar.Init(this);
            Hud.Init(this);
            Popup.Init(this);
            WorldEvents.Init(this);
            Save.Init(this);
            Achievements.Init(this);
            DailyReward.Init(this);
            Settings.Init(this);

            // Same build order as the original single-controller Awake().
            World.SetupCamera();
            World.SetupLighting();
            World.SetupPostProcessing();
            Calendar.CreateRainParticles();
            Hud.Build();
            Popup.Build();
            Settings.Build();
            Plots.InitWorld(); // always builds every plot's mesh/view fresh first...
            if (Save.HasSave()) Save.LoadGame(); // ...then a save, if one exists, overwrites the state on top
            Hud.Refresh();
            Calendar.UpdateCalendarDisplay();
            Calendar.ApplyWeather(CalendarManager.WeatherForMonth(Calendar.MonthIndex));
            ShowLaunchPopups();
        }

        /// <summary>Retention hooks that only make sense once per app launch:
        /// "while you were away" earnings (only set if SaveManager just paid
        /// out, i.e. real time actually passed since the last save) and the
        /// daily login streak reward. Both funnel through Hud's announcement
        /// queue, so if both apply they show one after another instead of one
        /// clobbering the other.</summary>
        void ShowLaunchPopups()
        {
            if (Save.PendingOfflineEarnings > 0)
            {
                string duration = SaveManager.FormatDuration(Save.PendingOfflineSeconds);
                Hud.QueueEventConfirmation("Welcome Back!",
                    $"While you were away for {duration}, your properties earned {EconomyManager.FormatSigned(Save.PendingOfflineEarnings)}!",
                    new Color(0.3f, 0.75f, 0.45f), alarm: false, buttonLabel: "Awesome!", onConfirm: null);
            }

            if (DailyReward.CheckPending())
            {
                Hud.QueueEventConfirmation($"Day {DailyReward.PendingStreakDay} Streak!",
                    "Come back every day for a bigger reward.",
                    new Color(0.95f, 0.75f, 0.2f), alarm: false,
                    buttonLabel: $"Claim {EconomyManager.FormatMoney(DailyReward.PendingAmount)}",
                    onConfirm: () => DailyReward.Claim());
            }
        }

        void Update()
        {
            Simulation.Tick();

            HandleWorldInput();
            Calendar.Tick();
            Plots.UpdatePlotExpiry();
            Popup.RefreshIfOpen();
            World.TickAmbient();
            Save.Tick();
        }

        /// <summary>One handler for both interactions the left mouse button now
        /// drives on the map: a short press-and-release opens the plot under the
        /// cursor (unchanged); a press-and-drag instead orbits the camera around
        /// the map so every building can be viewed from any angle. Which one it
        /// is can't be known until release, so both are tracked together here.</summary>
        void HandleWorldInput()
        {
            if (Popup.IsOpen || Hud.EventConfirmationOpen) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    pointerDown = false; // press started on UI - not ours to track
                    return;
                }
                pointerDown = true;
                pointerDownPos = mouse.position.ReadValue();
                dragDistanceAccum = 0f;
                return;
            }

            if (!pointerDown) return;

            if (mouse.leftButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                dragDistanceAccum += delta.magnitude;
                if (dragDistanceAccum > DragThresholdPixels)
                    World.RotateCamera(-delta.x * DragYawPerPixel);
                return;
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                pointerDown = false;
                if (dragDistanceAccum > DragThresholdPixels) return; // was a drag, not a tap

                var ray = World.MapCamera.ScreenPointToRay(pointerDownPos);
                if (Physics.Raycast(ray, out var hit, 100f))
                {
                    var view = hit.collider.GetComponentInParent<PropertyTileView>();
                    if (view != null) OnTileClicked(view.index);
                }
            }
        }

        void OnTileClicked(int index)
        {
            Popup.Open(index);
        }
    }
}
