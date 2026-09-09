using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KVH.KylePixelator
{
    // pixel cam -> RT (+ outlines) -> PresentFeature blit on PixelPresentCamera -> optional 1080p bloom -> Overlay HUD.
    // Present runs inside the present camera's URP pass so Screen Space Overlay is not wiped.
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(110)]
    public sealed class LowResOutput : MonoBehaviour
    {
        [SerializeField] Settings settings;
        [Tooltip("Cycle outline debug with [ / ] in Play Mode (Inspector still owns the mode).")]
        [SerializeField] bool enableOutlineDebugHotkeys = true;
        [Tooltip("Log each [ / ] outline debug cycle to the Console.")]
        [SerializeField] bool logOutlineDebugCycles;
        [Tooltip("Whole-number scale with letterboxing. Off = stretch full Game view.")]
        [SerializeField] bool integerScale;

        Camera _camera;
        Camera _presentCamera;
        CameraRig _rig;
        RenderTexture _rt;
        Vector2Int _rtSize;
        bool _editorTickSubscribed;
        Volume _presentVolume;
        VolumeProfile _presentProfile;
        Bloom _presentBloom;
        // play mode [ / ] only, never write Settings.outlineDebugMode (shared asset)
        OutlineDebugMode? _sessionOutlineDebug;

        public RenderTexture TargetTexture => _rt;
        public Settings Settings => settings;
        public bool IntegerScale => integerScale;
        public Camera PresentCamera => _presentCamera;

        public bool IsPresentCamera(Camera cam) =>
            cam != null && _presentCamera != null && cam == _presentCamera;

        public readonly struct PresentBlit
        {
            public readonly RenderTexture source;
            public readonly Rect viewportYUp;
            public readonly bool useSharp;

            public PresentBlit(RenderTexture source, Rect viewportYUp, bool useSharp)
            {
                this.source = source;
                this.viewportYUp = viewportYUp;
                this.useSharp = useSharp;
            }
        }

        // PresentFeature reads this each frame for the present camera only.
        public bool TryGetPresentBlit(out PresentBlit blit)
        {
            blit = default;
            if (settings == null || !settings.enableLowResOutput)
                return false;
            if (_rt == null || !_rt.IsCreated())
                return false;

            var screenW = Screen.width;
            var screenH = Screen.height;
            if (screenW < 1 || screenH < 1)
                return false;

            CacheRig();
            var compensate = _rig != null ? _rig.SnapCompensateActive : settings.SnapCompensateActive;
            var useSharp = settings.enableSharpUpscale;
            _rt.filterMode = useSharp ? FilterMode.Bilinear : FilterMode.Point;

            var display = settings.DisplayResolution;
            var margin = compensate ? settings.RtMargin : 0;
            var error = Vector2.zero;
            if (compensate)
            {
                CacheRig();
                if (_rig != null)
                    error = _rig.SnapErrorPixels;
            }

            float viewX, viewYFromTop, viewW, viewH;
            if (integerScale)
            {
                var scale = Mathf.Max(1, Mathf.Min(screenW / display.x, screenH / display.y));
                viewW = display.x * scale;
                viewH = display.y * scale;
                viewX = (screenW - viewW) * 0.5f;
                viewYFromTop = (screenH - viewH) * 0.5f;
            }
            else
            {
                viewX = 0f;
                viewYFromTop = 0f;
                viewW = screenW;
                viewH = screenH;
            }

            ComputePresentRect(
                _rt, viewX, viewYFromTop, viewW, viewH, display, margin, error, compensate, false,
                out var drawX, out var drawYFromTop, out var drawW, out var drawH);

            // CommandBuffer / URP viewport is bottom-left, Y-up (old GL present was Y-down).
            var viewportYUp = new Rect(drawX, screenH - drawYFromTop - drawH, drawW, drawH);
            blit = new PresentBlit(_rt, viewportYUp, useSharp);
            return true;
        }

        // Map a world point through the pixel cam + letterboxed present rect → overlay screen pixels
        // (origin bottom-left, same as RectTransformUtility / Screen Space Overlay).
        public bool TryWorldToOverlayScreen(Vector3 world, out Vector2 screenPixels)
        {
            screenPixels = default;
            if (_camera == null || settings == null)
                return false;

            var vp = _camera.WorldToViewportPoint(world);
            if (vp.z < 0f)
                return false;

            var screenW = Screen.width;
            var screenH = Screen.height;
            if (screenW < 1 || screenH < 1)
                return false;

            var display = settings.DisplayResolution;
            float viewX, viewYFromTop, viewW, viewH;
            if (integerScale)
            {
                var scale = Mathf.Max(1, Mathf.Min(screenW / display.x, screenH / display.y));
                viewW = display.x * scale;
                viewH = display.y * scale;
                viewX = (screenW - viewW) * 0.5f;
                viewYFromTop = (screenH - viewH) * 0.5f;
            }
            else
            {
                viewX = 0f;
                viewYFromTop = 0f;
                viewW = screenW;
                viewH = screenH;
            }

            // viewport Y up → UI screen Y up (bottom origin)
            var viewYFromBottom = screenH - viewYFromTop - viewH;
            screenPixels = new Vector2(
                viewX + Mathf.Clamp01(vp.x) * viewW,
                viewYFromBottom + Mathf.Clamp01(vp.y) * viewH);
            return vp.x >= -0.05f && vp.x <= 1.05f && vp.y >= -0.05f && vp.y <= 1.05f;
        }

        public OutlineDebugMode EffectiveOutlineDebugMode =>
            _sessionOutlineDebug ?? (settings != null ? settings.outlineDebugMode : OutlineDebugMode.Composite);

        public bool OutlinePassShouldRun
        {
            get
            {
                if (settings == null || !settings.enableLowResOutput)
                    return false;
                CacheRig();
                var outlines = settings.enableOutlines;
                return outlines || EffectiveOutlineDebugMode != OutlineDebugMode.Composite;
            }
        }

        #region Lifecycle

        void Awake()
        {
            _camera = GetComponent<Camera>();
            CacheRig();
        }

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
            CacheRig();
            SubscribeEditorTick(true);
            ApplyOutputState();
        }

        void OnDisable()
        {
            SubscribeEditorTick(false);
            TeardownLowResPath();
        }

        void OnDestroy()
        {
            SubscribeEditorTick(false);
            TeardownLowResPath();
        }

        void OnValidate()
        {
            if (!isActiveAndEnabled || settings == null)
                return;

            CelLightingGlobals.Apply(settings);
        }

        void LateUpdate() => ApplyOutputState();

        void Update()
        {
            if (settings == null || !enableOutlineDebugHotkeys || !Application.isPlaying)
                return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.rightBracketKey.wasPressedThisFrame)
                CycleOutlineDebug(1);
            else if (keyboard.leftBracketKey.wasPressedThisFrame)
                CycleOutlineDebug(-1);
        }

        #endregion

        #region Outline debug hotkeys

        void CycleOutlineDebug(int delta)
        {
            var values = (OutlineDebugMode[])System.Enum.GetValues(typeof(OutlineDebugMode));
            var current = EffectiveOutlineDebugMode;
            var i = System.Array.IndexOf(values, current);
            if (i < 0)
                i = 0;
            i = (i + delta + values.Length) % values.Length;
            _sessionOutlineDebug = values[i];
            if (logOutlineDebugCycles)
                Debug.Log($"KylePixelator outline debug → {_sessionOutlineDebug} (session)");
        }

        #endregion

        #region Editor tick

        void CacheRig()
        {
            if (_rig == null)
                _rig = GetComponentInParent<CameraRig>();
        }

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
            if (!this)
            {
                EditorApplication.update -= EditorTick;
                _editorTickSubscribed = false;
                return;
            }

            if (Application.isPlaying || !isActiveAndEnabled)
                return;

            ApplyOutputState();
        }
#endif

        #endregion

        #region Present math

        static void ComputePresentRect(
            RenderTexture source,
            float viewX, float viewYFromTop, float viewW, float viewH,
            Vector2Int display, int margin, Vector2 error, bool compensate, bool snapDestToPixels,
            out float drawX, out float drawYFromTop, out float drawW, out float drawH)
        {
            var px = viewW / display.x;
            var py = viewH / display.y;

            if (compensate)
            {
                drawW = source.width * px;
                drawH = source.height * py;
                drawX = viewX - margin * px - error.x * px;
                // error is view-space (Y up); drawYFromTop matches the old Y-down present matrix
                drawYFromTop = viewYFromTop - margin * py + error.y * py;
                if (snapDestToPixels)
                {
                    drawX = Mathf.Round(drawX);
                    drawYFromTop = Mathf.Round(drawYFromTop);
                    drawW = Mathf.Round(drawW);
                    drawH = Mathf.Round(drawH);
                }
            }
            else
            {
                drawX = viewX;
                drawYFromTop = viewYFromTop;
                drawW = viewW;
                drawH = viewH;
            }
        }

        #endregion

        #region RT / present camera / cel globals

        public void ApplyOutputState()
        {
            CacheRig();

            if (settings != null)
                CelLightingGlobals.Apply(settings);

            if (settings == null || !settings.enableLowResOutput)
            {
                TeardownLowResPath();
                return;
            }

            EnsureRt(_rig != null ? _rig.EffectiveRenderResolution : settings.RenderResolution);

            if (_camera == null)
                _camera = GetComponent<Camera>();

            if (_camera != null)
            {
                EnsureUrpCameraData(_camera, requireDepth: true);
                _camera.targetTexture = _rt;
                _camera.enabled = true;
            }

            EnsurePresentCamera();
            if (_presentCamera != null)
            {
                _presentCamera.enabled = true;
                _presentCamera.targetTexture = null;
                ApplyPresentBloom();
            }

            // after CameraRig, which forces HDR off. we only need headroom in the RT for present bloom.
            if (_camera != null && settings != null && settings.enablePresentBloom)
                _camera.allowHDR = true;
        }

        static void EnsureUrpCameraData(Camera cam, bool requireDepth)
        {
            if (cam == null)
                return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null)
                return;

            if (data.renderType != CameraRenderType.Base)
                data.renderType = CameraRenderType.Base;

            if (requireDepth && data.requiresDepthOption != CameraOverrideOption.On)
                data.requiresDepthOption = CameraOverrideOption.On;

            // pixel cam never posts. bloom lives on PixelPresentCamera after the upscale blit.
            if (requireDepth)
                data.renderPostProcessing = false;
        }

        void EnsurePresentCamera()
        {
            if (_presentCamera != null)
            {
                if (_presentCamera.gameObject.hideFlags == HideFlags.HideAndDontSave)
                    _presentCamera.gameObject.hideFlags = HideFlags.DontSave;
                _presentCamera.targetDisplay = 0;
                EnsureUrpCameraData(_presentCamera, requireDepth: false);
                return;
            }

            var go = new GameObject("PixelPresentCamera");
            go.transform.SetParent(transform, false);
            // DontSave only — HideAndDontSave makes the Game view show "No cameras rendering"
            go.hideFlags = HideFlags.DontSave;

            _presentCamera = go.AddComponent<Camera>();
            _presentCamera.clearFlags = CameraClearFlags.SolidColor;
            _presentCamera.backgroundColor = Color.black;
            _presentCamera.cullingMask = 0;
            _presentCamera.orthographic = true;
            _presentCamera.orthographicSize = 1f;
            _presentCamera.nearClipPlane = 0.1f;
            _presentCamera.farClipPlane = 10f;
            _presentCamera.depth = (_camera != null ? _camera.depth : 0f) + 10f;
            _presentCamera.allowHDR = false; // ApplyPresentBloom flips this when bloom is on
            _presentCamera.allowMSAA = false;
            _presentCamera.cameraType = CameraType.Game;
            _presentCamera.targetDisplay = 0;
            EnsureUrpCameraData(_presentCamera, requireDepth: false);
        }

        void ApplyPresentBloom()
        {
            var on = settings != null && settings.enablePresentBloom;
            var data = _presentCamera.GetUniversalAdditionalCameraData();
            _presentCamera.allowHDR = on;
            if (data != null)
                data.renderPostProcessing = on;

            if (!on)
            {
                if (_presentVolume != null)
                    _presentVolume.enabled = false;
                return;
            }

            EnsurePresentBloomVolume();
            _presentVolume.enabled = true;
            _presentBloom.active = true;
            _presentBloom.intensity.Override(Mathf.Max(0f, settings.presentBloomIntensity));
            _presentBloom.threshold.Override(Mathf.Max(0f, settings.presentBloomThreshold));
            _presentBloom.scatter.Override(0.55f);
            _presentBloom.maxIterations.Override(6);
            _presentBloom.clamp.Override(65472f);
            _presentBloom.tint.Override(Color.white);
        }

        void EnsurePresentBloomVolume()
        {
            if (_presentVolume == null)
                _presentVolume = _presentCamera.GetComponent<Volume>();
            if (_presentVolume == null)
                _presentVolume = _presentCamera.gameObject.AddComponent<Volume>();

            _presentVolume.isGlobal = true;
            _presentVolume.priority = 100f;
            _presentVolume.weight = 1f;

            var col = _presentCamera.GetComponent<SphereCollider>();
            if (col != null)
            {
                if (Application.isPlaying)
                    Destroy(col);
                else
                    DestroyImmediate(col);
            }

            if (_presentProfile == null)
            {
                _presentProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                _presentProfile.hideFlags = HideFlags.HideAndDontSave;
                _presentProfile.name = "PresentBloom";
            }

            if (!_presentProfile.TryGet(out _presentBloom))
                _presentBloom = _presentProfile.Add<Bloom>(true);

            _presentVolume.sharedProfile = _presentProfile;
        }

        void ReleasePresentBloom()
        {
            _presentBloom = null;
            _presentVolume = null;
            if (_presentProfile != null)
            {
                if (Application.isPlaying)
                    Destroy(_presentProfile);
                else
                    DestroyImmediate(_presentProfile);
                _presentProfile = null;
            }
        }

        void TeardownLowResPath()
        {
            ClearTargetTexture();

            ReleasePresentBloom();

            if (_presentCamera != null)
            {
                var presentGo = _presentCamera.gameObject;
                _presentCamera = null;
                if (Application.isPlaying)
                    Destroy(presentGo);
                else
                    DestroyImmediate(presentGo);
            }

            ReleaseRt();
        }

        void EnsureRt(Vector2Int size)
        {
            var compensate = _rig != null
                ? _rig.SnapCompensateActive
                : settings != null && settings.SnapCompensateActive;
            var wantFilter = settings != null && settings.enableSharpUpscale
                ? FilterMode.Bilinear
                : FilterMode.Point;
            var wantFormat = settings != null && settings.enablePresentBloom
                ? RenderTextureFormat.ARGBHalf
                : RenderTextureFormat.ARGB32;

            if (_rt != null && _rtSize == size && _rt.IsCreated() && _rt.format == wantFormat)
            {
                _rt.filterMode = wantFilter;
                return;
            }

            ReleaseRt();

            _rt = new RenderTexture(size.x, size.y, 24, wantFormat)
            {
                name = $"KylePixelatorRT_{size.x}x{size.y}",
                filterMode = wantFilter,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1,
                hideFlags = HideFlags.HideAndDontSave
            };
            _rt.Create();
            _rtSize = size;
        }

        void ClearTargetTexture()
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();

            if (_camera != null)
                _camera.targetTexture = null;
        }

        void ReleaseRt()
        {
            if (_rt == null)
                return;

            if (_camera != null && _camera.targetTexture == _rt)
                _camera.targetTexture = null;

            _rt.Release();
            if (Application.isPlaying)
                Destroy(_rt);
            else
                DestroyImmediate(_rt);

            _rt = null;
            _rtSize = default;
        }

        #endregion
    }
}
