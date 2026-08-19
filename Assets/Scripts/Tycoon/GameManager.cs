using Platformer.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Tycoon
{
    /// <summary>
    /// Top-level orchestrator: creates and wires every manager (World/Economy/
    /// Market/Plots/Calendar/Hud/Popup), drives the per-frame update dispatch,
    /// owns the mission win/lose state, and handles map click routing.
    /// Attach to a single empty GameObject in an otherwise empty scene - every
    /// manager component gets added to that same object at runtime.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public int missionTarget = 2000;
        public float missionTimeSeconds = 300f;

        float timeRemaining;
        bool missionResolved;

        public Font Font { get; private set; }

        public WorldBuilder World { get; private set; }
        public EconomyManager Economy { get; private set; }
        public MarketManager Market { get; private set; }
        public PlotManager Plots { get; private set; }
        public CalendarManager Calendar { get; private set; }
        public HudController Hud { get; private set; }
        public PropertyPopupController Popup { get; private set; }

        void Awake()
        {
            QualitySettings.antiAliasing = 4;
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timeRemaining = missionTimeSeconds;

            World = gameObject.AddComponent<WorldBuilder>();
            Economy = gameObject.AddComponent<EconomyManager>();
            Market = gameObject.AddComponent<MarketManager>();
            Plots = gameObject.AddComponent<PlotManager>();
            Calendar = gameObject.AddComponent<CalendarManager>();
            Hud = gameObject.AddComponent<HudController>();
            Popup = gameObject.AddComponent<PropertyPopupController>();

            World.Init(this);
            Economy.Init(this);
            Market.Init(this);
            Plots.Init(this);
            Calendar.Init(this);
            Hud.Init(this);
            Popup.Init(this);

            // Same build order as the original single-controller Awake().
            World.SetupCamera();
            World.SetupLighting();
            World.SetupPostProcessing();
            Calendar.CreateRainParticles();
            Hud.Build();
            Popup.Build();
            Plots.InitWorld();
            Hud.Refresh();
            Calendar.UpdateCalendarDisplay();
            Calendar.ApplyWeather(CalendarManager.WeatherForMonth(Calendar.MonthIndex));
        }

        void Update()
        {
            Simulation.Tick();

            if (!missionResolved)
            {
                timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
                if (Economy.balance >= missionTarget)
                    ResolveMission(true);
                else if (timeRemaining <= 0f)
                    ResolveMission(false);
            }
            Hud.RefreshTimeDisplay(timeRemaining);
            HandleWorldClick();
            Calendar.Tick();
            Plots.UpdatePlotExpiry();
            Popup.RefreshIfOpen();
            World.TickAmbient();
        }

        void HandleWorldClick()
        {
            if (missionResolved) return;
            if (Popup.IsOpen || Hud.bannerPanel.activeSelf) return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var ray = World.MapCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                var view = hit.collider.GetComponentInParent<PropertyTileView>();
                if (view != null) OnTileClicked(view.index);
            }
        }

        void OnTileClicked(int index)
        {
            if (missionResolved) return;
            Popup.Open(index);
        }

        void ResolveMission(bool success)
        {
            missionResolved = true;
            Popup.Close();
            Hud.ShowBanner(success
                ? $"You win! Reached {EconomyManager.FormatMoney(missionTarget)}"
                : "Time's up...");
        }
    }
}
