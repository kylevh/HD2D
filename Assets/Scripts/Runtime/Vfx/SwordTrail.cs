using System.Collections.Generic;
using UnityEngine;

namespace KVH.Game.Vfx
{
    // swept-blade ribbon: catmull-rom along the arc, pinch toward the tip as it ages (crescent, not a pizza of quads).
    [ExecuteAlways]
    [DefaultExecutionOrder(120)]
    [DisallowMultipleComponent]
    [AddComponentMenu("KVH/Vfx/Sword Trail")]
    public sealed class SwordTrail : MonoBehaviour
    {
        [SerializeField] Transform edgeBase;
        [SerializeField] Transform edgeTip;
        [SerializeField] Material material;
        [SerializeField] float lifetime = 0.55f;
        [SerializeField] [Range(0f, 1f)] float pinch = 0.18f;
        [SerializeField] [Range(0.4f, 3f)] float pinchPower = 1.35f;
        [SerializeField] [Range(2, 12)] int subdivs = 8;
        [SerializeField] [Range(2, 8)] int across = 6;
        [SerializeField] int maxSamples = 40;
        [SerializeField] Color vertexTint = Color.white;

        struct Sample
        {
            public Vector3 a;
            public Vector3 b;
            public float time;
        }

        readonly List<Sample> _samples = new List<Sample>(48);
        readonly List<Vector3> _verts = new List<Vector3>(1024);
        readonly List<Vector2> _uvs = new List<Vector2>(1024);
        readonly List<Color> _cols = new List<Color>(1024);
        readonly List<int> _tris = new List<int>(2048);

        Mesh _mesh;
        GameObject _meshGo;
        MeshFilter _filter;
        MeshRenderer _renderer;
        Vector3 _lastA;
        Vector3 _lastB;
        bool _hasLast;

        void OnEnable()
        {
            EnsureMesh();
            _hasLast = false;
            _samples.Clear();
        }

        void OnDisable()
        {
            _samples.Clear();
            if (_mesh != null)
                _mesh.Clear();
        }

        void OnDestroy() => TearDownMesh();

        void LateUpdate()
        {
            if (edgeBase == null || edgeTip == null)
                return;

            EnsureMesh();
            PinMeshTransform();

            var now = Time.unscaledTime;
            var a = edgeBase.position;
            var b = edgeTip.position;

            if (_hasLast)
            {
                // only wipe on a true teleport. tip of a long blade can jump meters in one hitch.
                var jump = Vector3.Distance(a, _lastA);
                if (jump > 8f)
                    _samples.Clear();
            }

            Push(a, b, now);
            _hasLast = true;
            _lastA = a;
            _lastB = b;

            var cutoff = now - lifetime;
            var drop = 0;
            while (drop < _samples.Count && _samples[drop].time < cutoff)
                drop++;
            if (drop > 0)
                _samples.RemoveRange(0, drop);

            Rebuild();
        }

        void Push(Vector3 a, Vector3 b, float now)
        {
            if (_samples.Count >= maxSamples && _samples.Count > 0)
                _samples.RemoveAt(0);
            _samples.Add(new Sample { a = a, b = b, time = now });
        }

        Sample GetClamped(int i)
        {
            if (i < 0) i = 0;
            if (i >= _samples.Count) i = _samples.Count - 1;
            return _samples[i];
        }

        static Vector3 Catmull(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return 0.5f * (
                2f * p1
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        void Rebuild()
        {
            _verts.Clear();
            _uvs.Clear();
            _cols.Clear();
            _tris.Clear();

            var n = _samples.Count;
            if (n < 2 || _mesh == null)
                return;
            var cols = Mathf.Max(2, across);
            var sub = Mathf.Max(2, subdivs);
            var rows = 0;

            for (var i = 0; i < n - 1; i++)
            {
                var s0 = GetClamped(i - 1);
                var s1 = GetClamped(i);
                var s2 = GetClamped(i + 1);
                var s3 = GetClamped(i + 2);
                var steps = i == n - 2 ? sub + 1 : sub;

                for (var s = 0; s < steps; s++)
                {
                    var t = s / (float)sub;
                    var baseP = Catmull(s0.a, s1.a, s2.a, s3.a, t);
                    var tipP = Catmull(s0.b, s1.b, s2.b, s3.b, t);
                    var along = Mathf.Clamp01((i + t) / Mathf.Max(1f, n - 1));
                    var age = 1f - along;
                    // pinch by how far down the ribbon we are, not the clock (clocks hitch)
                    var pinchT = Mathf.Pow(age, pinchPower) * pinch;
                    var inner = Vector3.Lerp(baseP, tipP, pinchT);

                    for (var c = 0; c < cols; c++)
                    {
                        var u = c / (float)(cols - 1);
                        _verts.Add(Vector3.Lerp(inner, tipP, u));
                        _uvs.Add(new Vector2(u, along));
                        _cols.Add(vertexTint);
                    }

                    rows++;
                }
            }

            for (var r = 0; r < rows - 1; r++)
            {
                var r0 = r * cols;
                var r1 = (r + 1) * cols;
                for (var c = 0; c < cols - 1; c++)
                {
                    var i0 = r0 + c;
                    var i1 = i0 + 1;
                    var i2 = r1 + c;
                    var i3 = i2 + 1;
                    _tris.Add(i0);
                    _tris.Add(i2);
                    _tris.Add(i1);
                    _tris.Add(i1);
                    _tris.Add(i2);
                    _tris.Add(i3);
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_cols);
            _mesh.SetTriangles(_tris, 0, true);
            _mesh.RecalculateBounds();
        }

        void EnsureMesh()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "SwordTrail" };
                _mesh.MarkDynamic();
            }

            if (_meshGo == null)
            {
                var world = transform.root;
                var trailName = transform.name + "Trail";
                var existing = world.Find(trailName);
                _meshGo = existing != null ? existing.gameObject : new GameObject(trailName);
                _meshGo.transform.SetParent(world, true);
                PinMeshTransform();
                _filter = _meshGo.GetComponent<MeshFilter>();
                if (_filter == null)
                    _filter = _meshGo.AddComponent<MeshFilter>();
                _renderer = _meshGo.GetComponent<MeshRenderer>();
                if (_renderer == null)
                    _renderer = _meshGo.AddComponent<MeshRenderer>();
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
                _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }

            if (_filter == null)
                _filter = _meshGo.GetComponent<MeshFilter>();
            if (_renderer == null)
                _renderer = _meshGo.GetComponent<MeshRenderer>();

            _filter.sharedMesh = _mesh;
            if (material != null)
                _renderer.sharedMaterial = material;
        }

        void PinMeshTransform()
        {
            if (_meshGo == null)
                return;
            var t = _meshGo.transform;
            t.position = Vector3.zero;
            t.rotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        void TearDownMesh()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_mesh);
                else
                    DestroyImmediate(_mesh);
                _mesh = null;
            }

            if (_meshGo != null)
            {
                if (Application.isPlaying)
                    Destroy(_meshGo);
                else
                    DestroyImmediate(_meshGo);
                _meshGo = null;
            }
        }
    }
}
