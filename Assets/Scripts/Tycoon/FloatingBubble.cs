using UnityEngine;

namespace Tycoon
{
    /// <summary>
    /// Gentle idle up/down bob for a "floating bubble" UI card, plus a quick
    /// scale-in punch the moment it becomes active - the UI equivalent of
    /// WorldBuilder.PlayBuildingPopAnimation. Pure Mathf.Sin/Update, no
    /// external tweening package, matching every other animation already in
    /// this codebase (IdleSway, PropertyTileView's price-pulse coroutine).
    /// Drives anchoredPosition and localScale directly, so don't combine this
    /// with another system that also drives those on the same RectTransform.
    /// </summary>
    public class FloatingBubble : MonoBehaviour
    {
        public float bobAmplitude = 7f;
        public float bobSpeed = 1.1f;
        public float popDuration = 0.22f;

        RectTransform rt;
        Vector2 basePos;
        float phase;
        float popElapsed;

        void Awake()
        {
            rt = (RectTransform)transform;
        }

        void OnEnable()
        {
            basePos = rt.anchoredPosition;
            phase = Random.value * Mathf.PI * 2f; // per-instance offset so bubbles don't all bob in lockstep
            popElapsed = 0f;
            rt.localScale = Vector3.one * 0.85f;
        }

        void Update()
        {
            float t = Time.unscaledTime * bobSpeed + phase; // unscaled - stays alive through pause, like the popup punch-scale
            rt.anchoredPosition = basePos + Vector2.up * (Mathf.Sin(t) * bobAmplitude);

            if (popElapsed < popDuration)
            {
                popElapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(popElapsed / popDuration);
                float eased = 1f - (1f - p) * (1f - p) * (1f - p); // ease-out cubic
                rt.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, eased);
            }
        }
    }
}
