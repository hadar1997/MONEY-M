using UnityEngine;

namespace Tycoon
{
    /// <summary>Keeps a world-space UI element (price tags, floating +$ text,
    /// coin pops) facing the camera every frame. Needed now that the camera can
    /// orbit (WorldBuilder.RotateCamera) - without this, anything that only set
    /// its rotation once at spawn time would visibly drift out of alignment the
    /// moment the player rotates the view.</summary>
    public class Billboard : MonoBehaviour
    {
        Transform cam;

        public void Init(Transform cameraTransform) => cam = cameraTransform;

        void LateUpdate()
        {
            if (cam != null) transform.rotation = cam.rotation;
        }
    }
}
