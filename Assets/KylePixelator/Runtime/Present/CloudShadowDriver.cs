using UnityEngine;

namespace KVH.KylePixelator
{
    // Scene knobs for shared cloud shadows (ToonLit opt-in + grass blades).
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CloudShadowDriver : MonoBehaviour
    {
        [SerializeField] bool active = true;
        [SerializeField] Vector2 direction = new Vector2(1f, 0.25f);
        [Range(0f, 0.5f)] [SerializeField] float speed = 0.04f;
        [Range(0.01f, 0.5f)] [SerializeField] float scale = 0.065f;
        [Range(0f, 1f)] [SerializeField] float strength = 0.3f;
        [Min(0f)] [SerializeField] float quantize = 0.3f;

        void OnEnable() => Push();
        void OnDisable() => CloudShadowGlobals.Clear();
        void OnValidate() => Push();
        void LateUpdate() => Push();

        void Push()
        {
            CloudShadowGlobals.Apply(active && isActiveAndEnabled, direction, speed, scale, strength, quantize);
        }
    }
}
