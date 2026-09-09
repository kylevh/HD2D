using UnityEngine;
using UnityEngine.Rendering;

namespace KVH.KylePixelator
{
    // snap-for-render on a child mesh. physics root stays free.
    // ViewGlue: live Round of camera-relative origin. don't put this on the CharacterController root.
    [DisallowMultipleComponent]
    public sealed class VisualSnap : MonoBehaviour
    {
        [Tooltip("Snap world yaw for the pixel-camera draw (0 = off). 45 = 8 directions.")]
        [SerializeField] float yawSnapDegrees = 45f;

        [SerializeField] CameraRig rig;

        CameraRig _rig;
        bool _snapped;
        Vector3 _restoreLocalPosition;
        Quaternion _restoreLocalRotation;

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RestoreIfNeeded();
            _rig = null;
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (_snapped)
                return;

            if (!ResolveRig() || camera != _rig.PixelCamera)
                return;

            if (!_rig.PixelSnapActive || !_rig.Settings.enableLowResOutput)
                return;

            var root = transform.parent != null ? transform.parent : transform;
            var doYaw = yawSnapDegrees > 0.01f;
            var upp = _rig.UnitsPerPixel;
            if (upp < 1e-6f)
                return;

            _restoreLocalPosition = transform.localPosition;
            _restoreLocalRotation = transform.localRotation;

            var pos = root.position;
            var camT = camera.transform;
            var right = camT.right;
            var up = camT.up;
            var fwd = camT.forward;
            var camPos = camT.position;
            var rel = pos - camPos;
            var sx = Mathf.Round(Vector3.Dot(rel, right) / upp) * upp;
            var sy = Mathf.Round(Vector3.Dot(rel, up) / upp) * upp;
            transform.position = camPos + right * sx + up * sy + fwd * Vector3.Dot(rel, fwd);

            if (doYaw)
            {
                var euler = root.rotation.eulerAngles;
                euler.y = Mathf.Round(euler.y / yawSnapDegrees) * yawSnapDegrees;
                transform.rotation = Quaternion.Euler(euler);
            }

            _snapped = true;
        }

        void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!_snapped || _rig == null || camera != _rig.PixelCamera)
                return;

            RestoreIfNeeded();
        }

        void RestoreIfNeeded()
        {
            if (!_snapped)
                return;

            transform.localPosition = _restoreLocalPosition;
            transform.localRotation = _restoreLocalRotation;
            _snapped = false;
        }

        void Reset() => AutoWireRig();

        void OnValidate()
        {
            if (rig == null)
                AutoWireRig();
        }

        void AutoWireRig()
        {
#if UNITY_EDITOR
            if (rig == null)
                rig = UnityEngine.Object.FindAnyObjectByType<CameraRig>();
#endif
        }

        bool ResolveRig()
        {
            _rig = rig;
            return _rig != null && _rig.PixelCamera != null && _rig.Settings != null;
        }
    }
}
