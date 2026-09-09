using UnityEngine;
using KVH.KylePixelator;

namespace KVH.Game.Lighting
{
    // surface bands in world; halo is a screen-space disc (pixels), not a bigger 3D radius
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    [AddComponentMenu("KVH/Lighting/Volume Light")]
    public sealed class VolumeLight : MonoBehaviour
    {
        const string ShaderName = "KVH/KylePixelatorVolumeLight";
        const string SourceShaderName = "KVH/KylePixelatorVolumeLightSource";
        const float MeshPad = 1.08f;

        [Header("Surface")]
        [SerializeField] Color color = new Color(1f, 0.55f, 0.22f, 1f);
        [Min(0f)] [SerializeField] float intensity = 1.5f;
        [Tooltip("World-space reach of light on surfaces.")]
        [Min(0.1f)] [SerializeField] float radius = 5f;
        [Range(0f, 2f)] [SerializeField] float blend = 1f;
        [Tooltip("Hard falloff steps on geometry (pixel-art bands).")]
        [Range(2, 16)] [SerializeField] int quantization = 6;

        [Header("Halo")]
        [SerializeField] Color haloColor = new Color(1f, 0.7f, 0.35f, 1f);
        [Tooltip("Screen-space disc radius in low-res pixels (perfect circle around the lamp on screen).")]
        [Min(1f)] [SerializeField] float haloSize = 64f;
        [Range(0f, 2f)] [SerializeField] float haloBlend = 0.45f;
        [Range(2, 16)] [SerializeField] int haloQuantization = 4;

        [Header("Shading")]
        [Tooltip("0 = flat; 1 = full N·L darkening in facing-away corners (surface only).")]
        [Range(0f, 1f)] [SerializeField] float cornerBrightness = 0.35f;

        [Header("Shadows")]
        [Tooltip("Experimental SS occlusion. Applies to surface only, never the screen halo.")]
        [SerializeField] bool screenSpaceShadows = false;
        [Range(0f, 1f)] [SerializeField] float shadowStrength = 1f;
        [Range(8, 64)] [SerializeField] int shadowSteps = 32;
        [Min(0.05f)] [SerializeField] float shadowThickness = 1f;

        [Header("Night")]
        [Min(1f)] [SerializeField] float nightBoost = 1.6f;
        [Tooltip("Leave empty; finds LightingDirector in the scene.")]
        [SerializeField] LightingDirector lightingDirector;

        [Header("Refs")]
        [SerializeField] Transform volumeSphere;
        [SerializeField] MeshRenderer volumeRenderer;
        [SerializeField] Transform sourceMarker;
        [Tooltip("Optional. Preferred over Camera.main for halo world size.")]
        [SerializeField] CameraRig cameraRig;

        Material _mat;
        Material _sourceMat;
        MaterialPropertyBlock _mpb;
        MaterialPropertyBlock _sourceMpb;
        LightingDirector _director;
        CameraRig _rig;
        float _lastDayAmount = float.NaN;
        bool _dirty = true;

        static readonly int LightColorId = Shader.PropertyToID("_LightColor");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int LightBlendId = Shader.PropertyToID("_LightBlend");
        static readonly int QuantizationId = Shader.PropertyToID("_Quantization");
        static readonly int CornerBrightnessId = Shader.PropertyToID("_CornerBrightness");
        static readonly int HaloColorId = Shader.PropertyToID("_HaloColor");
        static readonly int HaloSizeId = Shader.PropertyToID("_HaloSize");
        static readonly int HaloBlendId = Shader.PropertyToID("_HaloBlend");
        static readonly int HaloQuantizationId = Shader.PropertyToID("_HaloQuantization");
        static readonly int ScreenSpaceShadowsId = Shader.PropertyToID("_ScreenSpaceShadows");
        static readonly int ShadowStepsId = Shader.PropertyToID("_ShadowSteps");
        static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
        static readonly int ShadowThicknessId = Shader.PropertyToID("_ShadowThickness");
        static readonly int SourceColorId = Shader.PropertyToID("_Color");

        float EffectiveIntensity(float dayAmount)
        {
            var nightFactor = 1f - Mathf.Clamp01(dayAmount);
            return intensity * Mathf.Lerp(1f, nightBoost, nightFactor);
        }

        void OnEnable()
        {
            EnsureHierarchy();
            _dirty = true;
            Apply();
        }

        void OnDestroy()
        {
            if (_mat != null)
            {
                if (Application.isPlaying)
                    Destroy(_mat);
                else
                    DestroyImmediate(_mat);
                _mat = null;
            }
            if (_sourceMat != null)
            {
                if (Application.isPlaying)
                    Destroy(_sourceMat);
                else
                    DestroyImmediate(_sourceMat);
                _sourceMat = null;
            }
        }

        void LateUpdate()
        {
            var director = ResolvedDirector();
            var day = director != null ? director.DayAmount : 1f;
            if (_dirty || !Mathf.Approximately(day, _lastDayAmount))
                Apply();
        }

        void OnValidate()
        {
            radius = Mathf.Max(0.1f, radius);
            intensity = Mathf.Max(0f, intensity);
            nightBoost = Mathf.Max(1f, nightBoost);
            haloSize = Mathf.Max(1f, haloSize);
            quantization = Mathf.Clamp(quantization, 2, 16);
            haloQuantization = Mathf.Clamp(haloQuantization, 2, 16);
            shadowSteps = Mathf.Clamp(shadowSteps, 8, 64);
            shadowThickness = Mathf.Max(0.05f, shadowThickness);
            _dirty = true;
            if (isActiveAndEnabled)
            {
                EnsureHierarchy();
                Apply();
            }
        }

        LightingDirector ResolvedDirector()
        {
            if (lightingDirector != null)
                return lightingDirector;
            if (_director == null)
                _director = FindAnyObjectByType<LightingDirector>();
            return _director;
        }

        CameraRig ResolvedRig()
        {
            if (cameraRig != null)
                return cameraRig;
            if (_rig == null)
                _rig = FindAnyObjectByType<CameraRig>();
            return _rig;
        }

        void EnsureHierarchy()
        {
            var legacy = transform.Find("ShadowCaster");
            if (legacy != null)
            {
                if (Application.isPlaying)
                    Destroy(legacy.gameObject);
                else
                    DestroyImmediate(legacy.gameObject);
            }

            if (volumeSphere == null)
            {
                var existing = transform.Find("Volume");
                if (existing != null)
                    volumeSphere = existing;
                else
                {
                    volumeSphere = CreateChildSphere("Volume", 1f);
                    volumeRenderer = volumeSphere.GetComponent<MeshRenderer>();
                }
            }

            if (volumeRenderer == null && volumeSphere != null)
                volumeRenderer = volumeSphere.GetComponent<MeshRenderer>();

            if (sourceMarker == null)
            {
                var existing = transform.Find("Source");
                if (existing != null)
                    sourceMarker = existing;
                else
                    sourceMarker = CreateChildSphere("Source", 0.4f);
            }

            EnsureSourceMarker();
            EnsureMaterial();
        }

        Transform CreateChildSphere(string childName, float diameter)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = childName;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * diameter;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying)
                    Destroy(col);
                else
                    DestroyImmediate(col);
            }
            return go.transform;
        }

        static bool HasShader(Material mat, string shaderName)
        {
            return mat != null && mat.shader != null && mat.shader.name == shaderName;
        }

        void EnsureSourceMarker()
        {
            if (sourceMarker == null)
                return;

            if (!sourceMarker.TryGetComponent<MeshRenderer>(out var mr))
                return;

            if (!HasShader(mr.sharedMaterial, SourceShaderName))
            {
                if (_sourceMat == null)
                {
                    var shader = Shader.Find(SourceShaderName);
                    if (shader == null)
                    {
                        Debug.LogWarning($"VolumeLight: missing shader '{SourceShaderName}'.");
                        return;
                    }
                    _sourceMat = new Material(shader)
                    {
                        name = "VolumeLight Source (Instance)",
                        hideFlags = HideFlags.DontSave,
                    };
                }
                mr.sharedMaterial = _sourceMat;
            }

            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void EnsureMaterial()
        {
            if (volumeRenderer == null)
                return;

            if (!HasShader(volumeRenderer.sharedMaterial, ShaderName))
            {
                if (_mat == null)
                {
                    var shader = Shader.Find(ShaderName);
                    if (shader == null)
                    {
                        Debug.LogWarning($"VolumeLight: missing shader '{ShaderName}'.");
                        return;
                    }
                    _mat = new Material(shader) { name = "VolumeLight (Instance)", hideFlags = HideFlags.DontSave };
                }
                volumeRenderer.sharedMaterial = _mat;
            }

            volumeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            volumeRenderer.receiveShadows = false;
        }

        // prefer CameraRig display PPU. Camera.main can be the present cam or wrong ortho.
        float WorldPerPixel()
        {
            var rig = ResolvedRig();
            if (rig != null)
            {
                var upp = rig.UnitsPerPixel;
                if (upp > 1e-6f)
                    return upp;
            }

            return (2f * 9f) / 360f;
        }

        void Apply()
        {
            EnsureHierarchy();
            if (volumeSphere == null || volumeRenderer == null)
                return;

            var director = ResolvedDirector();
            var day = director != null ? director.DayAmount : 1f;
            _lastDayAmount = day;
            _dirty = false;
            var i = EffectiveIntensity(day);

            // cover both the surface ball and the screen-halo disc projected to world
            var haloWorldR = haloSize * WorldPerPixel();
            var coverR = Mathf.Max(radius, haloWorldR) * MeshPad;
            volumeSphere.localScale = Vector3.one * (coverR * 2f);

            _mpb ??= new MaterialPropertyBlock();
            volumeRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(LightColorId, color);
            _mpb.SetFloat(IntensityId, i);
            _mpb.SetFloat(RadiusId, radius);
            _mpb.SetFloat(LightBlendId, blend);
            _mpb.SetFloat(QuantizationId, quantization);
            _mpb.SetFloat(CornerBrightnessId, cornerBrightness);
            _mpb.SetColor(HaloColorId, haloColor);
            _mpb.SetFloat(HaloSizeId, haloSize);
            _mpb.SetFloat(HaloBlendId, haloBlend);
            _mpb.SetFloat(HaloQuantizationId, haloQuantization);
            _mpb.SetFloat(ScreenSpaceShadowsId, screenSpaceShadows ? 1f : 0f);
            _mpb.SetFloat(ShadowStepsId, shadowSteps);
            _mpb.SetFloat(ShadowStrengthId, shadowStrength);
            _mpb.SetFloat(ShadowThicknessId, shadowThickness);
            volumeRenderer.SetPropertyBlock(_mpb);
            ApplySourceColor();
        }

        void ApplySourceColor()
        {
            if (sourceMarker == null || !sourceMarker.TryGetComponent<MeshRenderer>(out var mr))
                return;

            _sourceMpb ??= new MaterialPropertyBlock();
            mr.GetPropertyBlock(_sourceMpb);
            _sourceMpb.SetColor(SourceColorId, color);
            mr.SetPropertyBlock(_sourceMpb);
        }

        void OnDrawGizmos()
        {
            var c = color;
            c.a = 0.9f;
            Gizmos.color = c;
            Gizmos.DrawWireSphere(transform.position, 0.15f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
