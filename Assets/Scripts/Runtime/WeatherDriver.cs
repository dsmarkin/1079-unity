using UnityEngine;
using Height1079.Night;

namespace Height1079.Runtime
{
    /// <summary>Tells the shared <see cref="Weather"/> what the game wants of it: the night of 1959 has its own
    /// blizzard cycle (<c>NightSession.Storm</c>), the mountain breaks its weather after noon by itself
    /// (<c>MountainStorm</c>), the demo reel has seconds rather than minutes to show the weather turn, and on the
    /// southern slope the strength of the wind is a property of the day. Weather itself knows none of this — it
    /// blows the same snow for the sandbox, which drives it off its own run clock.</summary>
    public sealed class WeatherDriver : MonoBehaviour
    {
        Weather weather;

        void Awake() => weather = GetComponent<Weather>();

        void Update()
        {
            if (weather == null) return;
            var s = NightSession.Instance;
            var me = Bootstrap.LocalHiker;
            bool storming = s != null && (s.Storm.Value || s.MapStorm);
            weather.Want = storming ? 1f : 0f;
            weather.RiseSeconds = 25f; weather.FallSeconds = 40f;
            if (DemoReel.StormWanted >= 0f) { weather.Want = DemoReel.StormWanted; weather.RiseSeconds = weather.FallSeconds = 1.3f; }
            weather.Live = s != null || DemoReel.StormWanted >= 0f;
            weather.DayWind = LocationViews.DayWind;
            weather.Inside = me != null && me.Crawling;
            weather.GroundY = me != null ? me.transform.position.y : float.NaN;
        }
    }
}
