using System.Collections;
using UnityEngine;

namespace Tycoon
{
    /// <summary>The lamp head of a map-perimeter siren - dark red and idle by
    /// default, strobes bright red when WorldEventManager triggers a new world
    /// event. Uses scaled time (unlike the UI announcement) since this is a
    /// world object and should freeze/speed up with the rest of the game.</summary>
    public class SirenLight : MonoBehaviour
    {
        static readonly Color IdleColor = new Color(0.3f, 0.08f, 0.06f);
        static readonly Color FlashColor = new Color(1f, 0.12f, 0.08f);

        Renderer rend;
        Coroutine flashRoutine;

        void Awake() => rend = GetComponent<Renderer>();

        public void Flash(float duration)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(duration));
        }

        IEnumerator FlashRoutine(float duration)
        {
            const float pulseInterval = 0.25f; // urgent strobe, not a gentle fade
            float elapsed = 0f;
            bool on = false;
            while (elapsed < duration)
            {
                on = !on;
                rend.material.color = on ? FlashColor : IdleColor;
                yield return new WaitForSeconds(pulseInterval);
                elapsed += pulseInterval;
            }
            rend.material.color = IdleColor;
            flashRoutine = null;
        }
    }
}
