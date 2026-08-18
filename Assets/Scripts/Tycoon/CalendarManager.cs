using UnityEngine;

namespace Tycoon
{
    /// <summary>
    /// Drives the in-game month/year clock and the 3 weather types tied to
    /// season, including the rain particle effect. Triggers MarketManager's
    /// monthly settlement on every month advance.
    /// </summary>
    public class CalendarManager : MonoBehaviour
    {
        public float secondsPerMonth = 5f;

        static readonly string[] MonthNames =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        public enum WeatherType { Sunny, Cloudy, Rainy }

        int monthIndex;
        int yearNumber = 1;
        float monthTimer;
        WeatherType currentWeather = WeatherType.Sunny;
        ParticleSystem rainParticles;

        GameManager game;

        public void Init(GameManager owner)
        {
            game = owner;
        }

        public void Tick()
        {
            monthTimer += Time.deltaTime;
            if (monthTimer < secondsPerMonth) return;
            monthTimer -= secondsPerMonth;

            monthIndex++;
            if (monthIndex >= 12)
            {
                monthIndex = 0;
                yearNumber++;
            }
            UpdateCalendarDisplay();
            ApplyWeather(WeatherForMonth(monthIndex));
            game.Market.PayMonthlyRent();
            game.Market.FluctuateMarket();
            game.Hud.Refresh(); // after both, so the dashboard shows this month's trend, not last month's
        }

        public void UpdateCalendarDisplay()
        {
            game.Hud.calendarText.text = $"{MonthNames[monthIndex]}, Year {yearNumber} · {WeatherForMonth(monthIndex)}";
        }

        public static WeatherType WeatherForMonth(int month)
        {
            // Dec/Jan/Feb: cloudy, Mar-Aug: sunny, Sep-Nov: rainy - 3 weather
            // types cycling with the seasons across the 12-month year.
            if (month == 11 || month <= 1) return WeatherType.Cloudy;
            if (month <= 7) return WeatherType.Sunny;
            return WeatherType.Rainy;
        }

        public void ApplyWeather(WeatherType weather)
        {
            currentWeather = weather;
            var cam = game.World.MapCamera;
            switch (weather)
            {
                case WeatherType.Sunny:
                    cam.backgroundColor = new Color(0.85f, 0.9f, 0.94f);
                    RenderSettings.ambientLight = new Color(0.96f, 0.93f, 0.85f);
                    rainParticles.Stop();
                    break;
                case WeatherType.Cloudy:
                    cam.backgroundColor = new Color(0.76f, 0.78f, 0.8f);
                    RenderSettings.ambientLight = new Color(0.82f, 0.82f, 0.82f);
                    rainParticles.Stop();
                    break;
                case WeatherType.Rainy:
                    cam.backgroundColor = new Color(0.54f, 0.6f, 0.68f);
                    RenderSettings.ambientLight = new Color(0.68f, 0.7f, 0.75f);
                    rainParticles.Play();
                    break;
            }
        }

        public void CreateRainParticles()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(game.transform, false);
            go.transform.position = new Vector3(0, 4f, 0);
            rainParticles = go.AddComponent<ParticleSystem>();

            var main = rainParticles.main;
            main.loop = true;
            main.startLifetime = 1.2f;
            main.startSpeed = 6f;
            main.startSize = 0.03f;
            main.startColor = new Color(0.7f, 0.8f, 0.95f, 0.6f);
            main.maxParticles = 400;

            var emission = rainParticles.emission;
            emission.rateOverTime = 180f;

            var shape = rainParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(9f, 0.1f, 7f);
            shape.rotation = new Vector3(90f, 0f, 0f); // box's local forward (+Z, the default emit direction) now points straight down

            var renderer = rainParticles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3f;

            rainParticles.Stop();
        }

        public WeatherType CurrentWeather => currentWeather;
        public int MonthIndex => monthIndex;
    }
}
