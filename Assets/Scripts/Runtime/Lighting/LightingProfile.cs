using UnityEngine;

namespace KVH.Game.Lighting
{
    // key / fill / sky colours for a time-of-day look.
    [CreateAssetMenu(fileName = "LightingProfile", menuName = "KVH/Game/Lighting Profile")]
    public sealed class LightingProfile : ScriptableObject
    {
        [Header("Key (main directional)")]
        public Color keyColor = new Color(1f, 0.96f, 0.88f, 1f);
        [Min(0f)] public float keyIntensity = 1.25f;
        [Range(0f, 1f)] public float keyShadowStrength = 1f;

        [Header("Fill (second directional, usually no shadows)")]
        public Color fillColor = new Color(0.72f, 0.78f, 1f, 1f);
        [Min(0f)] public float fillIntensity = 0.45f;

        [Header("Pixel camera clear")]
        [Tooltip("Pixel-camera clear color. LightingDirector applies this via CameraRig.SetClearColor (does not mutate Settings).")]
        public Color skyColor = new Color(0.78f, 0.9f, 0.98f, 1f);
    }
}
