using UnityEngine;

namespace Roguelike
{
    /// Keeps a quad (glow halo) facing the camera.
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam)
                transform.rotation = cam.transform.rotation;
        }
    }
}
