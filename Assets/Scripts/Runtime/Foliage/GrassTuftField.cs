using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KVH.Game.Foliage
{
    // Bakes tuft quads into one MeshRenderer (instancing dropped _BaseMap → white).
    // UV1 = (scale, atlasIndex, shade 0/1); UV2 = world root. Billboard in shader.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GrassTuftField : MonoBehaviour
    {
        static readonly int GrassWindParamsId = Shader.PropertyToID("_KylePixelator_GrassWindParams");
        static readonly int GrassWindScaleId = Shader.PropertyToID("_KylePixelator_GrassWindScale");
        static readonly int GrassWindActiveId = Shader.PropertyToID("_KylePixelator_GrassWindActive");
        static readonly int GrassWindQuantizeId = Shader.PropertyToID("_KylePixelator_GrassWindQuantize");
        static readonly int GrassWindGroundId = Shader.PropertyToID("_KylePixelator_GrassWindGround");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ShadowTintId = Shader.PropertyToID("_ShadowTint");
        static readonly int HighlightTintId = Shader.PropertyToID("_HighlightTint");
        static readonly int DarkMulId = Shader.PropertyToID("_DarkMul");

        [Header("Draw")]
        [SerializeField] Mesh tuftMesh;
        [SerializeField] Material tuftMaterial;
        [SerializeField] MeshRenderer boundsSource;

        [Header("Scatter (ground plane)")]
        [Min(0.5f)] [SerializeField] float patchSizeX = 8f;
        [Min(0.5f)] [SerializeField] float patchSizeZ = 8f;
        [SerializeField] float offsetX = 12f;
        [SerializeField] float offsetZ = -10f;
        [Min(0.05f)] [SerializeField] float spacing = 0.28f;
        [Range(0f, 1f)] [SerializeField] float jitter = 0.45f;
        [SerializeField] int seed = 42;
        [SerializeField] Vector2 scaleRange = new Vector2(0.85f, 1.25f);
        [SerializeField] float yOffset = 0.04f;
        [SerializeField] int maxInstances = 1500;

        [Header("Wind (F3)")]
        [SerializeField] bool pushWindGlobals = true;
        [SerializeField] Vector2 windDirection = new Vector2(1f, 0.35f);
        [Range(0f, 0.4f)] [SerializeField] float windStrength = 0.1f;
        [Range(0f, 4f)] [SerializeField] float windSpeed = 1.0f;
        [Range(0.05f, 2f)] [SerializeField] float windScale = 0.18f;
        [Min(0f)] [SerializeField] float windQuantize = 0.04f;
        [Range(0f, 1f)] [SerializeField] float windGroundShade = 0f;

        [Header("Ground bind (F4)")]
        [SerializeField] bool syncGroundTint = true;

        [Header("Atlas accents (F7)")]
        [SerializeField] int atlasColumns = 4;
        [Range(0f, 1f)] [SerializeField] float accentChance = 0.08f;
        [Tooltip("Weights for cells 1..N-1 when an accent rolls (cell 0 = base).")]
        [SerializeField] float[] accentWeights = { 1f, 1f, 0.6f };

        [Header("Darker tones (F8)")]
        [Range(0f, 1f)] [SerializeField] float darkChance = 0.04f;
        [Range(0.5f, 0.95f)] [SerializeField] float darkMul = 0.88f;

        [Header("Exclude (pond holes)")]
        [SerializeField] bool excludeRect;
        [SerializeField] bool excludeCircle;
        [SerializeField] Vector2 excludeCenterXZ;
        [SerializeField] Vector2 excludeHalfExtents;
        [SerializeField] float excludeRadius;
        [Tooltip("Min grid cells between dark tufts (Chebyshev). Stops clumps.")]
        [Min(1)] [SerializeField] int darkMinCellGap = 4;

        MeshFilter _filter;
        MeshRenderer _renderer;
        Mesh _baked;
        Material _runtimeMat;
        int _count;
        bool _dirty = true;

        public int InstanceCount => _count;

        public void SetExcludeRect(Vector2 centerXZ, Vector2 halfExtents)
        {
            excludeRect = true;
            excludeCircle = false;
            excludeCenterXZ = centerXZ;
            excludeHalfExtents = halfExtents;
            _dirty = true;
        }

        public void SetExcludeCircle(Vector2 centerXZ, float radius)
        {
            excludeCircle = true;
            excludeRect = false;
            excludeCenterXZ = centerXZ;
            excludeRadius = Mathf.Max(0.05f, radius);
            _dirty = true;
        }

        void OnEnable()
        {
            EnsureDrawHost();
            _dirty = true;
            PushWindGlobals();
            ApplyGroundBind();
        }

        void OnDisable()
        {
            Shader.SetGlobalFloat(GrassWindActiveId, 0f);
            Shader.SetGlobalFloat(GrassWindGroundId, 0f);
        }

        void OnDestroy()
        {
            if (_runtimeMat != null)
            {
                if (Application.isPlaying) Destroy(_runtimeMat);
                else DestroyImmediate(_runtimeMat);
                _runtimeMat = null;
            }

            if (_baked == null)
                return;
            if (Application.isPlaying) Destroy(_baked);
            else DestroyImmediate(_baked);
            _baked = null;
        }

        void OnValidate()
        {
            spacing = Mathf.Max(0.05f, spacing);
            maxInstances = Mathf.Clamp(maxInstances, 1, 10000);
            patchSizeX = Mathf.Max(0.5f, patchSizeX);
            patchSizeZ = Mathf.Max(0.5f, patchSizeZ);
            scaleRange.x = Mathf.Max(0.05f, scaleRange.x);
            scaleRange.y = Mathf.Max(scaleRange.x, scaleRange.y);
            windStrength = Mathf.Clamp(windStrength, 0f, 0.4f);
            windSpeed = Mathf.Max(0f, windSpeed);
            windScale = Mathf.Max(0.05f, windScale);
            windQuantize = Mathf.Max(0f, windQuantize);
            windGroundShade = Mathf.Clamp01(windGroundShade);
            atlasColumns = Mathf.Clamp(atlasColumns, 1, 8);
            accentChance = Mathf.Clamp01(accentChance);
            darkChance = Mathf.Clamp01(darkChance);
            darkMul = Mathf.Clamp(darkMul, 0.5f, 0.95f);
            darkMinCellGap = Mathf.Max(1, darkMinCellGap);
            _dirty = true;
            PushWindGlobals();
            ApplyGroundBind();
        }

        void LateUpdate()
        {
            if (_dirty)
                Rebuild();
            PushWindGlobals();
            ApplyGroundBind();
        }

        void EnsureDrawHost()
        {
            // Identity under World — never parent to scaled GrassField (scale 8×).
            var world = GameObject.Find("World");
            var parent = world != null ? world.transform : transform.root;
            const string hostName = "GrassTuftDraw";
            Transform host = parent.Find(hostName);
            if (host == null)
            {
                var go = new GameObject(hostName);
                go.transform.SetParent(parent, false);
                host = go.transform;
            }

            host.localPosition = Vector3.zero;
            host.localRotation = Quaternion.identity;
            host.localScale = Vector3.one;

            _filter = host.GetComponent<MeshFilter>();
            if (_filter == null)
                _filter = host.gameObject.AddComponent<MeshFilter>();

            _renderer = host.GetComponent<MeshRenderer>();
            if (_renderer == null)
                _renderer = host.gameObject.AddComponent<MeshRenderer>();

            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            // Receive at blade root in shader (even lighting) — not per-pixel crush.
            _renderer.receiveShadows = true;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void ApplyMaterial()
        {
            if (_renderer == null || tuftMaterial == null)
                return;

            if (_runtimeMat == null || _runtimeMat.shader != tuftMaterial.shader)
            {
                if (_runtimeMat != null)
                {
                    if (Application.isPlaying) Destroy(_runtimeMat);
                    else DestroyImmediate(_runtimeMat);
                }
                _runtimeMat = new Material(tuftMaterial) { name = tuftMaterial.name + " (Field)" };
            }

            if (_renderer.sharedMaterial != _runtimeMat)
                _renderer.sharedMaterial = _runtimeMat;
            if (_runtimeMat.renderQueue < 3000)
                _runtimeMat.renderQueue = 3000;
            _runtimeMat.enableInstancing = false;
            // Keep field instance in sync with the authored asset (atlas swap, etc.).
            if (tuftMaterial.HasProperty("_BaseMap"))
                _runtimeMat.SetTexture("_BaseMap", tuftMaterial.GetTexture("_BaseMap"));
            if (_runtimeMat.HasProperty("_AtlasColumns"))
                _runtimeMat.SetFloat("_AtlasColumns", Mathf.Max(1, atlasColumns));
            if (_runtimeMat.HasProperty(DarkMulId))
                _runtimeMat.SetFloat(DarkMulId, darkMul);
        }

        void PushWindGlobals()
        {
            if (pushWindGlobals)
            {
                var dir = windDirection.sqrMagnitude > 1e-6f ? windDirection.normalized : Vector2.right;
                Shader.SetGlobalVector(GrassWindParamsId, new Vector4(dir.x, dir.y, windStrength, windSpeed));
                Shader.SetGlobalFloat(GrassWindScaleId, windScale);
                Shader.SetGlobalFloat(GrassWindQuantizeId, windQuantize);
                Shader.SetGlobalFloat(GrassWindGroundId, windGroundShade);
                Shader.SetGlobalFloat(GrassWindActiveId, 1f);
            }
            else
            {
                Shader.SetGlobalFloat(GrassWindActiveId, 0f);
                Shader.SetGlobalFloat(GrassWindGroundId, 0f);
            }
        }

        // Shape from alpha; Base/Shadow/Highlight copied from GrassField (real mat props — MPB breaks SRP batcher).
        void ApplyGroundBind()
        {
            if (_renderer == null || tuftMaterial == null)
                return;

            ApplyMaterial();
            _renderer.SetPropertyBlock(null);

            if (!syncGroundTint || _runtimeMat == null)
                return;

            Color baseCol = _runtimeMat.GetColor(BaseColorId);
            Color shadow = _runtimeMat.GetColor(ShadowTintId);
            Color hi = _runtimeMat.HasProperty(HighlightTintId)
                ? _runtimeMat.GetColor(HighlightTintId)
                : Color.white;

            if (boundsSource != null && boundsSource.sharedMaterial != null)
            {
                var g = boundsSource.sharedMaterial;
                if (g.HasProperty(BaseColorId))
                    baseCol = g.GetColor(BaseColorId);
                if (g.HasProperty(ShadowTintId))
                    shadow = g.GetColor(ShadowTintId);
                if (g.HasProperty(HighlightTintId))
                    hi = g.GetColor(HighlightTintId);
            }

            _runtimeMat.SetColor(BaseColorId, baseCol);
            _runtimeMat.SetColor(ShadowTintId, shadow);
            _runtimeMat.SetColor(HighlightTintId, hi);
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            _dirty = false;
            EnsureDrawHost();

            if (tuftMesh == null || spacing <= 0f)
            {
                _count = 0;
                if (_filter != null)
                    _filter.sharedMesh = null;
                return;
            }

            var center = ResolveCenter();
            center.x += offsetX;
            center.z += offsetZ;
            var y = ResolveSurfaceY() + yOffset;
            var halfX = patchSizeX * 0.5f;
            var halfZ = patchSizeZ * 0.5f;

            var cols = Mathf.Max(1, Mathf.FloorToInt(patchSizeX / spacing) + 1);
            var rows = Mathf.Max(1, Mathf.FloorToInt(patchSizeZ / spacing) + 1);
            FitGridToMax(ref cols, ref rows, maxInstances);

            var srcVerts = tuftMesh.vertices;
            var srcUv = tuftMesh.uv;
            var srcTris = tuftMesh.triangles;
            if (srcVerts == null || srcVerts.Length == 0 || srcTris == null || srcTris.Length == 0)
            {
                _count = 0;
                return;
            }

            var capacity = cols * rows;
            var verts = new List<Vector3>(capacity * srcVerts.Length);
            var uvs = new List<Vector2>(capacity * srcVerts.Length);
            var scales = new List<Vector3>(capacity * srcVerts.Length); // x=scale, y=atlas, z=shade
            var roots = new List<Vector3>(capacity * srcVerts.Length);
            var tris = new List<int>(capacity * srcTris.Length);

            var jitterReach = spacing * jitter;
            var count = 0;
            var darkCells = new List<Vector2Int>(Mathf.Max(8, capacity / 20));

            for (var iz = 0; iz < rows; iz++)
            {
                for (var ix = 0; ix < cols; ix++)
                {
                    var u = cols == 1 ? 0.5f : ix / (float)(cols - 1);
                    var v = rows == 1 ? 0.5f : iz / (float)(rows - 1);
                    var x = Mathf.Lerp(-halfX, halfX, u);
                    var z = Mathf.Lerp(-halfZ, halfZ, v);

                    if (jitterReach > 0f)
                    {
                        var h = Hash(seed, ix, iz);
                        x += (Frac(h) * 2f - 1f) * jitterReach;
                        z += (Frac(h * 1.6180339887f) * 2f - 1f) * jitterReach;
                        x = Mathf.Clamp(x, -halfX, halfX);
                        z = Mathf.Clamp(z, -halfZ, halfZ);
                    }

                    var h2 = Hash(seed ^ unchecked((int)0x9E3779B9), ix, iz);
                    var scale = Mathf.Lerp(scaleRange.x, scaleRange.y, Frac(h2));
                    var atlasIndex = PickAtlasIndex(Hash(seed ^ unchecked((int)0x85EBCA6B), ix, iz));
                    // Rare darker tone + min cell gap so they don't clump.
                    var shade = 0f;
                    if (darkChance > 0f &&
                        Frac(Hash(seed ^ unchecked((int)0xC2B2AE35), ix, iz)) < darkChance &&
                        DarkGapOk(darkCells, ix, iz, darkMinCellGap))
                    {
                        shade = 1f;
                        darkCells.Add(new Vector2Int(ix, iz));
                    }
                    var root = new Vector3(center.x + x, y, center.z + z);
                    if (excludeCircle)
                    {
                        var dx = root.x - excludeCenterXZ.x;
                        var dz = root.z - excludeCenterXZ.y;
                        if (dx * dx + dz * dz <= excludeRadius * excludeRadius)
                            continue;
                    }
                    else if (excludeRect &&
                        Mathf.Abs(root.x - excludeCenterXZ.x) <= excludeHalfExtents.x &&
                        Mathf.Abs(root.z - excludeCenterXZ.y) <= excludeHalfExtents.y)
                        continue;

                    var vertBase = verts.Count;
                    for (var vi = 0; vi < srcVerts.Length; vi++)
                    {
                        verts.Add(srcVerts[vi]); // unit quad card in OS
                        uvs.Add(srcUv != null && vi < srcUv.Length ? srcUv[vi] : Vector2.zero);
                        scales.Add(new Vector3(scale, atlasIndex, shade));
                        roots.Add(root);
                    }
                    for (var ti = 0; ti < srcTris.Length; ti++)
                        tris.Add(vertBase + srcTris[ti]);

                    count++;
                }
            }

            _count = count;
            if (_baked == null)
            {
                _baked = new Mesh { name = "GrassTuftFieldBaked" };
                _baked.MarkDynamic();
            }
            else
            {
                _baked.Clear();
            }

            _baked.indexFormat = verts.Count > 65000
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;
            _baked.SetVertices(verts);
            _baked.SetUVs(0, uvs);
            _baked.SetUVs(1, scales);
            _baked.SetUVs(2, roots);
            _baked.SetTriangles(tris, 0);

            // Bounds cover the whole patch in world (host is at origin).
            var min = new Vector3(center.x - halfX, y, center.z - halfZ);
            var max = new Vector3(center.x + halfX, y + 2f, center.z + halfZ);
            _baked.bounds = new Bounds((min + max) * 0.5f, (max - min) + new Vector3(2f, 2f, 2f));

            _filter.sharedMesh = _baked;
            ApplyGroundBind();
            PushWindGlobals();
        }

        static bool DarkGapOk(List<Vector2Int> darkCells, int ix, int iz, int minGap)
        {
            for (var i = 0; i < darkCells.Count; i++)
            {
                var d = darkCells[i];
                if (Mathf.Max(Mathf.Abs(d.x - ix), Mathf.Abs(d.y - iz)) < minGap)
                    return false;
            }
            return true;
        }

        static void FitGridToMax(ref int cols, ref int rows, int maxCount)
        {
            if (cols * rows <= maxCount)
                return;
            var t = Mathf.Sqrt(maxCount / (float)(cols * rows));
            cols = Mathf.Max(1, Mathf.FloorToInt(cols * t));
            rows = Mathf.Max(1, Mathf.FloorToInt(rows * t));
            while (cols * rows > maxCount)
            {
                if (cols >= rows && cols > 1) cols--;
                else if (rows > 1) rows--;
                else break;
            }
        }

        static uint Hash(int seed, int x, int z)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)x * 374761393u;
                h ^= (uint)z * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static float Frac(uint h) => (h & 0x00FFFFFFu) / 16777216f;
        static float Frac(float v) => v - Mathf.Floor(v);

        int PickAtlasIndex(uint h)
        {
            var cols = Mathf.Max(1, atlasColumns);
            if (cols <= 1 || accentChance <= 0f)
                return 0;
            if (Frac(h) >= accentChance)
                return 0;

            // Weighted pick among accent cells 1..cols-1
            var accentCount = cols - 1;
            var total = 0f;
            for (var i = 0; i < accentCount; i++)
            {
                var w = accentWeights != null && i < accentWeights.Length ? accentWeights[i] : 1f;
                total += Mathf.Max(0f, w);
            }
            if (total <= 1e-6f)
                return 1;

            var r = Frac(h * 1.6180339887f) * total;
            var acc = 0f;
            for (var i = 0; i < accentCount; i++)
            {
                var w = accentWeights != null && i < accentWeights.Length ? accentWeights[i] : 1f;
                acc += Mathf.Max(0f, w);
                if (r <= acc)
                    return i + 1;
            }
            return accentCount;
        }

        Vector3 ResolveCenter()
        {
            if (boundsSource != null)
                return boundsSource.bounds.center;
            return transform.position;
        }

        float ResolveSurfaceY()
        {
            if (boundsSource != null)
                return boundsSource.bounds.max.y;
            return transform.position.y;
        }
    }
}
