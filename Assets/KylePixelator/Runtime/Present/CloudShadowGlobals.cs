using UnityEngine;

namespace KVH.KylePixelator
{
    // Pushes shared cloud-shadow globals. Direction is fixed — never copy the sun.
    public static class CloudShadowGlobals
    {
        static readonly int ParamsId = Shader.PropertyToID("_KylePixelator_CloudShadowParams");
        static readonly int StrengthId = Shader.PropertyToID("_KylePixelator_CloudShadowStrength");
        static readonly int ActiveId = Shader.PropertyToID("_KylePixelator_CloudShadowActive");
        static readonly int QuantizeId = Shader.PropertyToID("_KylePixelator_CloudShadowQuantize");

        public static void Apply(bool active, Vector2 direction, float speed, float scale, float strength, float quantize)
        {
            if (!active)
            {
                Clear();
                return;
            }

            var dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.right;
            Shader.SetGlobalVector(ParamsId, new Vector4(dir.x, dir.y, Mathf.Max(0f, speed), Mathf.Max(0.01f, scale)));
            Shader.SetGlobalFloat(StrengthId, Mathf.Clamp01(strength));
            Shader.SetGlobalFloat(QuantizeId, Mathf.Max(0f, quantize));
            Shader.SetGlobalFloat(ActiveId, 1f);
        }

        public static void Clear()
        {
            Shader.SetGlobalFloat(ActiveId, 0f);
            Shader.SetGlobalFloat(StrengthId, 0f);
        }
    }
}
