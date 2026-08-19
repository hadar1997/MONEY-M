using UnityEngine;

namespace Tycoon
{
    /// <summary>Small self-contained yaw sway for decorative parts (currently the
    /// Canvas Tent's flag) - reads as wind. Self-contained on purpose: the object
    /// it's attached to gets destroyed and recreated by WorldBuilder.RebuildBuildingMesh
    /// on every reroll, so tracking it from a central list would mean cleaning up
    /// dangling references on every rebuild for no benefit over just letting the
    /// component live and die with its own GameObject.</summary>
    public class IdleSway : MonoBehaviour
    {
        public float amplitudeDegrees = 6f;
        public float speed = 1.6f;

        float phase;
        Quaternion baseRotation;

        void Start()
        {
            phase = Random.value * Mathf.PI * 2f; // per-instance offset so flags don't all sway in lockstep
            baseRotation = transform.localRotation;
        }

        void Update()
        {
            float angle = Mathf.Sin(Time.time * speed + phase) * amplitudeDegrees;
            transform.localRotation = baseRotation * Quaternion.Euler(0f, angle, 0f);
        }
    }
}
