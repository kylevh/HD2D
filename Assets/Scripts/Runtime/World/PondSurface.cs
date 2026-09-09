using UnityEngine;

namespace KVH.Game.World
{
    // pushes pond center / half-extent onto the water renderer so the mat can move with the prefab.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PondSurface : MonoBehaviour
    {
        static readonly int CenterId = Shader.PropertyToID("_PondCenter");
        static readonly int RadiusId = Shader.PropertyToID("_PondRadius");

        [SerializeField] Renderer waterRenderer;
        [SerializeField] float halfExtent = 4.5f;

        MaterialPropertyBlock _mpb;

        public float HalfExtent
        {
            get => halfExtent;
            set => halfExtent = Mathf.Max(0.05f, value);
        }

        public Renderer WaterRenderer => waterRenderer;

        public void SetWaterRenderer(Renderer r) => waterRenderer = r;

        void OnEnable() => Push();

        void LateUpdate() => Push();

        void OnValidate()
        {
            halfExtent = Mathf.Max(0.05f, halfExtent);
            Push();
        }

        public void Push()
        {
            if (waterRenderer == null)
                return;

            _mpb ??= new MaterialPropertyBlock();
            waterRenderer.GetPropertyBlock(_mpb);
            var p = transform.position;
            _mpb.SetVector(CenterId, new Vector4(p.x, p.y, p.z, 0f));
            _mpb.SetFloat(RadiusId, halfExtent);
            waterRenderer.SetPropertyBlock(_mpb);
        }
    }
}
