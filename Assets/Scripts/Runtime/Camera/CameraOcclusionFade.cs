using System.Collections.Generic;
using UnityEngine;

namespace KVH.Game.Camera
{
    // Traditional third-person occlusion: fade props between the pixel camera and the follow
    // target so the player stays fully opaque. Drives ToonLit `_OcclusionFade` via MPB.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class CameraOcclusionFade : MonoBehaviour
    {
        static readonly int OcclusionFadeId = Shader.PropertyToID("_OcclusionFade");

        [SerializeField] Transform target;
        [SerializeField] UnityEngine.Camera pixelCamera;
        [SerializeField] LayerMask occluderMask = ~0;
        [SerializeField] float castRadius = 0.4f;
        [Tooltip("Aim a bit above the feet so casts hit cover, not the floor.")]
        [SerializeField] float targetHeight = 1.0f;
        [SerializeField, Range(0.2f, 0.95f)] float fadeAmount = 0.7f;
        [SerializeField] float fadeSpeed = 10f;

        readonly Dictionary<Renderer, float> _fade = new();
        readonly HashSet<Renderer> _hit = new();
        readonly List<Renderer> _scratch = new();
        RaycastHit[] _hits = new RaycastHit[24];
        MaterialPropertyBlock _block;

        public void SetTarget(Transform followTarget) => target = followTarget;

        void Awake()
        {
            if (pixelCamera == null)
            {
                var rig = GetComponent<KylePixelator.CameraRig>();
                if (rig != null)
                    pixelCamera = rig.PixelCamera;
                if (pixelCamera == null)
                    pixelCamera = GetComponentInChildren<UnityEngine.Camera>(true);
            }

            // Never fade the player silhouette — only world geo between cam and hero.
            var playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                occluderMask &= ~(1 << playerLayer);
        }

        void OnDisable() => RestoreAll();

        void LateUpdate()
        {
            if (target == null)
            {
                var hero = GameObject.FindGameObjectWithTag("Player");
                if (hero != null)
                    target = hero.transform;
            }

            if (pixelCamera == null || target == null)
            {
                RestoreAll();
                return;
            }

            _hit.Clear();
            var origin = pixelCamera.transform.position;
            var aim = target.position + Vector3.up * targetHeight;
            var to = aim - origin;
            var dist = to.magnitude;
            if (dist < 0.05f)
            {
                TickFades(Time.deltaTime);
                return;
            }

            var dir = to / dist;
            var count = Physics.SphereCastNonAlloc(
                origin, castRadius, dir, _hits, dist, occluderMask, QueryTriggerInteraction.Ignore);

            for (var i = 0; i < count; i++)
            {
                var col = _hits[i].collider;
                if (col == null)
                    continue;
                if (col.transform == target || col.transform.IsChildOf(target))
                    continue;

                var renderers = col.GetComponentsInChildren<Renderer>();
                for (var r = 0; r < renderers.Length; r++)
                {
                    var rend = renderers[r];
                    if (rend == null || !rend.enabled)
                        continue;
                    _hit.Add(rend);
                    if (!_fade.ContainsKey(rend))
                        _fade[rend] = 0f;
                }
            }

            TickFades(Time.deltaTime);
        }

        void TickFades(float dt)
        {
            _block ??= new MaterialPropertyBlock();
            var step = fadeSpeed * dt;
            // Don't foreach+_fade[key]= — mutating values invalidates the enumerator.
            _scratch.Clear();
            foreach (var rend in _fade.Keys)
                _scratch.Add(rend);

            for (var i = 0; i < _scratch.Count; i++)
            {
                var rend = _scratch[i];
                if (rend == null)
                {
                    _fade.Remove(rend);
                    continue;
                }

                if (!_fade.TryGetValue(rend, out var current))
                    continue;

                var want = _hit.Contains(rend) ? fadeAmount : 0f;
                var next = Mathf.MoveTowards(current, want, step);
                if (next <= 0.001f && want <= 0f)
                {
                    ClearFade(rend);
                    _fade.Remove(rend);
                }
                else
                {
                    _fade[rend] = next;
                    ApplyFade(rend, next);
                }
            }
        }

        void ApplyFade(Renderer rend, float amount)
        {
            rend.GetPropertyBlock(_block);
            _block.SetFloat(OcclusionFadeId, amount);
            rend.SetPropertyBlock(_block);
        }

        void ClearFade(Renderer rend)
        {
            if (rend == null)
                return;
            rend.GetPropertyBlock(_block ??= new MaterialPropertyBlock());
            _block.SetFloat(OcclusionFadeId, 0f);
            rend.SetPropertyBlock(_block);
        }

        void RestoreAll()
        {
            foreach (var kv in _fade)
                ClearFade(kv.Key);
            _fade.Clear();
            _hit.Clear();
        }
    }
}
