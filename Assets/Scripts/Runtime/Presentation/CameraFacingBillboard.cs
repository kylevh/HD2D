using UnityEngine;

namespace KVH.Game.Presentation
{
    // faces Camera.main. lockYawOnly keeps it upright.
    public sealed class CameraFacingBillboard : MonoBehaviour
    {
        [SerializeField] bool lockYawOnly = true;

        void LateUpdate()
        {
            var camera = UnityEngine.Camera.main;
            if (camera == null)
                return;

            if (lockYawOnly)
            {
                var toCamera = camera.transform.position - transform.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude < 0.0001f)
                    return;

                transform.rotation = Quaternion.LookRotation(-toCamera);
                return;
            }

            transform.rotation = camera.transform.rotation;
        }
    }
}
