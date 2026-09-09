using KVH.Game.World;
using UnityEditor;
using UnityEngine;

namespace KVH.Game.Editor
{
    // builds Assets/Prefabs/World/Pond.prefab (circular basin + water, no foam crate).
    public static class PondPrefabBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/World/Pond.prefab";
        public const string WaveCellsPath = "Assets/Art/Textures/Water/WaveCells.png";
        public const string DiscMeshPath = "Assets/Art/Meshes/PondDisc.asset";
        const string StoneMatPath = "Assets/Art/Materials/Stone.mat";
        const string WaterMatPath = "Assets/Art/Materials/Water.mat";

        public const float DefaultHoleHalf = 3.5f;
        public const float ShoreOverlap = 0.5f;
        public const float WaterY = 0.06f;

        [MenuItem("KVH/Debug/Prefabs/Build Pond Prefab")]
        public static void BuildMenu()
        {
            var path = EnsurePrefab();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Debug.Log($"Pond prefab: {path}");
        }

        public static string EnsurePrefab()
        {
            PondLabSceneBuilder.EnsureWaterMaterial();
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/World");

            var stone = AssetDatabase.LoadAssetAtPath<Material>(StoneMatPath);
            var water = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            if (stone == null || water == null)
            {
                Debug.LogError("Pond prefab: missing Stone / Water materials.");
                return PrefabPath;
            }

            var root = new GameObject("Pond");
            var surface = root.AddComponent<PondSurface>();
            surface.HalfExtent = DefaultHoleHalf + ShoreOverlap;

            BuildBasin(root.transform, stone, DefaultHoleHalf);
            var waterGo = BuildWaterDisc(root.transform, water, DefaultHoleHalf);
            surface.SetWaterRenderer(waterGo.GetComponent<MeshRenderer>());
            surface.Push();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return PrefabPath;
        }

        public static GameObject InstantiatePond(Transform parent, Vector3 worldPos, float holeHalf = DefaultHoleHalf)
        {
            EnsurePrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Pond";
            if (parent != null)
                instance.transform.SetParent(parent, false);
            instance.transform.position = worldPos;

            var surface = instance.GetComponent<PondSurface>();
            if (surface != null)
            {
                surface.HalfExtent = holeHalf + ShoreOverlap;
                surface.Push();
            }

            if (!Mathf.Approximately(holeHalf, DefaultHoleHalf))
                RescalePondContents(instance.transform, holeHalf);

            return instance;
        }

        public static Mesh EnsureDiscMesh()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder("Assets/Art/Meshes");
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(DiscMeshPath);
            if (existing != null && existing.vertexCount > 0)
                return existing;

            var mesh = PondGeometry.BuildDisc(32);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, DiscMeshPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<Mesh>(DiscMeshPath);
        }

        public static Texture2D EnsureWaveCellsTexture()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder("Assets/Art/Textures");
            EnsureFolder("Assets/Art/Textures/Water");

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(WaveCellsPath);
            if (existing != null)
                return existing;

            const int res = 64;
            const int cells = 8;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                name = "WaveCells",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };

            var pixels = new Color[res * res];
            for (var y = 0; y < res; y++)
            {
                for (var x = 0; x < res; x++)
                {
                    var cellX = x * cells / res;
                    var cellY = y * cells / res;
                    var localX = (x % (res / cells)) / (float)(res / cells) - 0.5f;
                    var localY = (y % (res / cells)) / (float)(res / cells) - 0.5f;

                    var h = Hash(cellX, cellY);
                    var ang = h * Mathf.PI * 2f;
                    // bias toward a few axis-ish dirs so hop lines read
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    var dist = 1f - Mathf.Clamp01(new Vector2(localX, localY).magnitude * 2f);
                    pixels[y * res + x] = new Color(
                        dir.x * 0.5f + 0.5f,
                        dir.y * 0.5f + 0.5f,
                        Hash(cellX + 3, cellY + 7),
                        dist);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(Application.dataPath, "Art/Textures/Water/WaveCells.png"),
                bytes);
            AssetDatabase.ImportAsset(WaveCellsPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(WaveCellsPath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(WaveCellsPath);
        }

        static float Hash(int x, int y)
        {
            var n = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return n - Mathf.Floor(n);
        }

        static void RescalePondContents(Transform root, float holeHalf)
        {
            var waterR = holeHalf + ShoreOverlap;
            var water = root.Find("Water");
            if (water != null)
            {
                water.localPosition = new Vector3(0f, WaterY, 0f);
                water.localScale = new Vector3(waterR, 1f, waterR);
            }

            var basin = root.Find("Basin");
            if (basin == null)
                return;

            SetBasinStep(basin, "Step_1", holeHalf * 0.95f, -0.35f, 0.12f);
            SetBasinStep(basin, "Step_2", holeHalf * 0.7f, -0.75f, 0.12f);
            SetBasinStep(basin, "Step_3", holeHalf * 0.42f, -1.2f, 0.12f);
            SetBasinStep(basin, "Bottom", holeHalf * 0.22f, -1.55f, 0.12f);
        }

        static void SetBasinStep(Transform basin, string name, float radius, float y, float height)
        {
            var t = basin.Find(name);
            if (t == null)
                return;
            t.localPosition = new Vector3(0f, y, 0f);
            t.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        }

        static void BuildBasin(Transform root, Material stone, float holeHalf)
        {
            var basin = new GameObject("Basin");
            basin.transform.SetParent(root, false);
            PondGeometry.MakeBasinStep("Step_1", basin.transform, holeHalf * 0.95f, -0.35f, 0.12f, stone);
            PondGeometry.MakeBasinStep("Step_2", basin.transform, holeHalf * 0.7f, -0.75f, 0.12f, stone);
            PondGeometry.MakeBasinStep("Step_3", basin.transform, holeHalf * 0.42f, -1.2f, 0.12f, stone);
            PondGeometry.MakeBasinStep("Bottom", basin.transform, holeHalf * 0.22f, -1.55f, 0.12f, stone);
        }

        static GameObject BuildWaterDisc(Transform root, Material water, float holeHalf)
        {
            var radius = holeHalf + ShoreOverlap;
            var disc = EnsureDiscMesh();
            var go = PondGeometry.MakeFlatDisc("Water", root, radius, WaterY, water, shadows: false, disc);
            return go;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
