using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KVH.KylePixelator
{
    // pixel cam child on CameraPivot: ortho + pitch/roll from settings, snaps local XY to the texel grid.
    // game writes pivot pos + SetYaw. never this camera's local XY.
    [ExecuteAlways]
    [DefaultExecutionOrder(100)]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] Settings settings;
        [SerializeField] Camera pixelCamera;

        Vector2 _snapErrorPixels;
        bool _editorTickSubscribed;
        Color? _clearColorOverride;
        float? _yawOverride; // game 8-way orbit. pitch/roll stay on settings.fixedEuler

        public Settings Settings => settings;
        public Camera PixelCamera => pixelCamera;

        public Vector2 SnapErrorPixels => _snapErrorPixels; // desired minus snapped, in texels. usually < 0.5

        public bool PixelSnapActive => settings != null && settings.enablePixelSnap;

        public bool SnapCompensateActive =>
            settings != null
            && settings.enableLowResOutput
            && PixelSnapActive
            && settings.enableSnapCompensate;

        public Vector2Int EffectiveRenderResolution
        {
            get
            {
                if (settings == null)
                    return new Vector2Int(16, 16);
                var display = settings.DisplayResolution;
                if (!SnapCompensateActive)
                    return display;
                var pad = settings.RtMargin * 2;
                return new Vector2Int(display.x + pad, display.y + pad);
            }
        }

        // runtime clear (day/night). does not write the shared Settings asset.
        public void SetClearColor(Color color) => _clearColorOverride = color;

        public void ClearClearColorOverride() => _clearColorOverride = null;

        // runtime yaw (8-way orbit). pitch/roll stay on settings.fixedEuler. editor uses the asset.
        public void SetYaw(float yawDegrees) => _yawOverride = yawDegrees;

        public void ClearYawOverride() => _yawOverride = null;

        void OnEnable()
        {
            SubscribeEditorTick(true);
            Apply();
            ApplySnap();
        }

        void OnDisable()
        {
            _yawOverride = null;
            SubscribeEditorTick(false);
        }

        void OnDestroy() => SubscribeEditorTick(false);

        void OnValidate()
        {
            Apply();
            if (isActiveAndEnabled)
                ApplySnap();
        }

        void LateUpdate() => ApplySnap();

        void SubscribeEditorTick(bool on)
        {
#if UNITY_EDITOR
            if (on == _editorTickSubscribed)
                return;

            if (on)
                EditorApplication.update += EditorTick;
            else
                EditorApplication.update -= EditorTick;

            _editorTickSubscribed = on;
#else
            _editorTickSubscribed = false;
#endif
        }

#if UNITY_EDITOR
        void EditorTick()
        {
            // destroyed Unity objects still get update callbacks until we unsubscribe
            if (!this)
            {
                EditorApplication.update -= EditorTick;
                _editorTickSubscribed = false;
                return;
            }

            if (Application.isPlaying || !isActiveAndEnabled)
                return;

            ApplySnap();
        }
#endif

        public void Apply()
        {
            if (settings == null)
                return;

            if (pixelCamera == null)
                pixelCamera = GetComponentInChildren<Camera>(true);

            if (pixelCamera == null)
                return;

            var euler = settings.fixedEuler;
            if (_yawOverride.HasValue)
                euler.y = _yawOverride.Value;
            transform.rotation = Quaternion.Euler(euler);

            pixelCamera.orthographic = true;
            // when compensate enlarges the RT, scale ortho so each texel keeps the same world size as snap PPU
            var displayH = Mathf.Max(1, settings.internalHeight);
            if (SnapCompensateActive)
            {
                var renderH = displayH + settings.RtMargin * 2;
                pixelCamera.orthographicSize = settings.orthoSize * (renderH / (float)displayH);
            }
            else
            {
                pixelCamera.orthographicSize = settings.orthoSize;
            }

            pixelCamera.nearClipPlane = 0.1f;
            pixelCamera.farClipPlane = 100f;
            // procedural skyboxes re-filter every snap frame at low res, use a flat clear instead
            pixelCamera.clearFlags = CameraClearFlags.SolidColor;
            pixelCamera.backgroundColor = _clearColorOverride ?? settings.skyColor;
            pixelCamera.allowHDR = false;
            pixelCamera.allowMSAA = false;
            pixelCamera.transform.localRotation = Quaternion.identity;

            var local = pixelCamera.transform.localPosition;
            local.z = -settings.cameraDistance;

            if (PixelSnapActive)
            {
                // local XY owned by ApplySnap
                pixelCamera.transform.localPosition = local;
            }
            else
            {
                local.x = 0f;
                local.y = 0f;
                pixelCamera.transform.localPosition = local;
                _snapErrorPixels = Vector2.zero;
            }
        }

        public void ApplySnap()
        {
            if (settings == null)
                return;

            if (pixelCamera == null)
                pixelCamera = GetComponentInChildren<Camera>(true);

            if (pixelCamera == null)
                return;

            // keep ortho (incl oversized scale), rotation, and local Z in sync
            Apply();

            if (!PixelSnapActive)
            {
                _snapErrorPixels = Vector2.zero;
                return;
            }

            var distance = settings.cameraDistance;
            var height = Mathf.Max(1, settings.internalHeight);
            // PPU always from display ortho/height, never from oversized RT height
            var unitsPerPixel = (2f * settings.orthoSize) / height;

            var desiredWorld = transform.TransformPoint(new Vector3(0f, 0f, -distance));
            var right = transform.right;
            var up = transform.up;

            // nearest texel on each axis. leftover goes to dest-slide (compensate).
            // do not king-step / hysteresis this: a lagged axis desyncs when you
            // change direction, then every later hop compounds.
            var x = Vector3.Dot(desiredWorld, right);
            var y = Vector3.Dot(desiredWorld, up);
            var snappedX = Mathf.Round(x / unitsPerPixel) * unitsPerPixel;
            var snappedY = Mathf.Round(y / unitsPerPixel) * unitsPerPixel;

            var snappedWorld = desiredWorld + right * (snappedX - x) + up * (snappedY - y);
            var local = transform.InverseTransformPoint(snappedWorld);
            local.z = -distance;
            pixelCamera.transform.localPosition = local;

            _snapErrorPixels = new Vector2(
                (x - snappedX) / unitsPerPixel,
                (y - snappedY) / unitsPerPixel);
        }

        public float UnitsPerPixel
        {
            get
            {
                if (settings == null)
                    return 0f;
                return (2f * settings.orthoSize) / Mathf.Max(1, settings.internalHeight);
            }
        }
    }
}
