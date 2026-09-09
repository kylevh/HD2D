using UnityEngine;

namespace KVH.KylePixelator
{
    // lab views of the outline pass (runs on the low-res RT)
    public enum OutlineDebugMode
    {
        Composite = 0,
        ColorOnly = 1,
        Silhouette = 2,
        Depth = 3,
    }

    // which colour to darken for silhouette ink. detection only inks the farther texel of a depth step.
    public enum OutlineSilhouetteSide
    {
        OwnColor = 0,     // often sky with farther-side detection, usually skip
        NearerColor = 1,  // ink with the closer neighbour (the occluder)
    }

    // how ToonLit treats URP additional lights (fill dir + rare punctuals)
    public enum AdditionalLightsMode
    {
        Banded = 0,    // same banding as the main light
        TintOnly = 1,  // N.L * atten into palette, no bands. softer for point falloff
        Off = 2,
    }

    // pixelator knobs (res, snap, outlines, cel). inspector tooltips have the rest.
    [CreateAssetMenu(fileName = "PixelSettings", menuName = "KVH/KylePixelator/Settings")]
    public sealed class Settings : ScriptableObject
    {
        [Header("Internal resolution (M1)")]
        [Min(16)] public int internalWidth = 640;
        [Min(16)] public int internalHeight = 360;
        public bool enableLowResOutput = true;

        [Header("Camera (M0)")]
        [Min(0.1f)] public float orthoSize = 9f;
        public Vector3 fixedEuler = new Vector3(35f, 45f, 0f);
        [Min(0.1f)] public float cameraDistance = 30f;
        [Tooltip("Default pixel-camera clear when no LightingDirector override is active (skyboxes shimmer under snap).")]
        public Color skyColor = new Color(0.192f, 0.302f, 0.475f, 1f);

        [Header("Snap (M2)")]
        public bool enablePixelSnap;

        [Header("Compensate (M3)")]
        public bool enableSnapCompensate;
        [Min(1)] public int rtMarginTexels = 2;

        [Header("Upscale (M4)")]
        [Tooltip("fwidth sharp upscale instead of nearest. Needs bilinear RT sampling.")]
        public bool enableSharpUpscale;
        [Tooltip("Bloom the 1080p present, not the low-res RT. Pixel grid stays hard.")]
        public bool enablePresentBloom;
        [Range(0f, 8f)] public float presentBloomIntensity = 0.7f;
        [Range(0f, 1.5f)] public float presentBloomThreshold = 0.9f;

        [Header("Outlines (M5)")]
        [Tooltip("1px depth+normal outlines on the low-res RT (before sharp present).")]
        public bool enableOutlines = true;
        [Tooltip("Composite = final look. Other modes replace the RT with stage views ([ / ] cycles).")]
        public OutlineDebugMode outlineDebugMode = OutlineDebugMode.Composite;

        [Space(4)]
        [Tooltip("Depth discontinuity for silhouettes, in texel-world units (scaled up on grazing faces).")]
        [Range(0.25f, 20f)] public float silhouetteStepTexels = 3f;
        [Tooltip("NearerColor = ink with the closer neighbour’s colour (recommended). OwnColor = this texel (often sky when using farther-side 1px detection).")]
        public OutlineSilhouetteSide silhouetteSide = OutlineSilhouetteSide.NearerColor;
        [Tooltip("Ink brightness for depth silhouettes: 0 = black, 1 = no darkening.")]
        [Range(0f, 1f)] public float outlineLineDarken = 0.25f;

        [Space(4)]
        [Tooltip("Normal angle (1 − N·N) where a crease starts to show.")]
        [Range(0f, 1f)] public float creaseLow = 0.08f;
        [Tooltip("Normal angle where a crease is fully visible.")]
        [Range(0f, 2f)] public float creaseHigh = 0.3f;
        [Tooltip("How much creases lift the pixel (Leng/KYRIOTA). 0 = invisible; silhouettes stay darkened.")]
        [Range(0f, 1.5f)] public float creaseBrighten = 0.35f;
        [Tooltip("When on, form edges (top↔front) brighten. Off = silhouette-only.")]
        public bool enableCreases = true;

        [Header("Lighting (M6)")]
        [Tooltip("Banded N·L on KVH/KylePixelatorToonLit materials. Off = smooth Lambert with the same tint model (A/B).")]
        public bool enableCelLighting = true;
        [Tooltip("Flat value bands per light (darkest band = material Shadow Tint).")]
        [Range(2, 6)] public int lightBands = 2;
        [Tooltip("Half-width of each band threshold ramp, in N·L units. 0 = hard bands.")]
        [Range(0f, 0.1f)] public float bandSoftness = 0.02f;
        [Tooltip("Shifts the terminator: positive lights more of the surface.")]
        [Range(-0.5f, 0.5f)] public float lightWrap;

        [Header("Additional lights")]
        [Tooltip("Banded = fill directional. TintOnly / Off if stock point falloff looks like onion rings.")]
        public AdditionalLightsMode additionalLightsMode = AdditionalLightsMode.Banded;
        [Tooltip("Quantize point/spot distance attenuation (dir fill stays ~1). Reduces smooth×band rings.")]
        public bool stepPunctualAttenuation;
        [Tooltip("Steps for punctual distance atten when stepping is on.")]
        [Range(2, 8)] public int punctualAttenSteps = 4;

        public Vector2Int DisplayResolution =>
            new Vector2Int(Mathf.Max(16, internalWidth), Mathf.Max(16, internalHeight));

        // old name for display res
        public Vector2Int InternalResolution => DisplayResolution;

        public int RtMargin => Mathf.Max(1, rtMarginTexels);

        public bool SnapCompensateActive =>
            enableLowResOutput && enablePixelSnap && enableSnapCompensate;

        public bool OutlinePassActive =>
            enableLowResOutput
            && (enableOutlines || outlineDebugMode != OutlineDebugMode.Composite);

        public Vector2Int RenderResolution
        {
            get
            {
                var display = DisplayResolution;
                if (!SnapCompensateActive)
                    return display;

                var pad = RtMargin * 2;
                return new Vector2Int(display.x + pad, display.y + pad);
            }
        }
    }
}
