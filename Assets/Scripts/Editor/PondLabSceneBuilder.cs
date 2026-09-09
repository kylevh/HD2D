using KVH.KylePixelator;
using KVH.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KVH.Game.Editor
{
    // Water playground: outdoor systems + circular pond (radial depth, foam, hop waves).
    public static class PondLabSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/PondLab.unity";
        const string GrassMatPath = "Assets/Art/Materials/Grass.mat";
        const string StoneMatPath = "Assets/Art/Materials/Stone.mat";
        const string WaterMatPath = "Assets/Art/Materials/Water.mat";
        const string WaterShaderName = "KVH/KylePixelatorWater";

        const float FieldHalf = 20f;
        const float HoleHalf = 4f;
        const float FloorY = 0f;
        const float WaterY = 0.06f;
        const float ShoreOverlap = 0.5f;

        [MenuItem("KVH/Debug/Scenes/Build PondLab")]
        public static void Build()
        {
            EnsureWaterMaterial();

            var grass = AssetDatabase.LoadAssetAtPath<Material>(GrassMatPath);
            var stone = AssetDatabase.LoadAssetAtPath<Material>(StoneMatPath);
            var water = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            if (grass == null || stone == null || water == null)
            {
                Debug.LogError("PondLab: missing Grass / Stone / Water materials.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameSceneBootstrap.EnsurePlayableSystems(
                GameSceneBootstrap.LightingPreset.OutdoorCycle,
                spawnPlayerIfMissing: true);

            var director = Object.FindAnyObjectByType<KVH.Game.Lighting.LightingDirector>();
            if (director != null)
            {
                var so = new SerializedObject(director);
                so.FindProperty("mode").enumValueIndex = (int)KVH.Game.Lighting.LightingDirector.Mode.Cycle;
                so.FindProperty("useNight").boolValue = false;
                so.FindProperty("timeOfDayHours").floatValue = 12f;
                so.FindProperty("autoAdvance").boolValue = false;
                so.FindProperty("rotateKeyWithTime").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                director.Apply();
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                player.transform.position = new Vector3(0f, 0.1f, HoleHalf + 3f);

            GameSceneBootstrap.EnsureHierarchyScaffold();
            var world = GameObject.Find("World");
            var geometry = world != null ? world.transform.Find("Geometry") : null;
            if (geometry == null)
            {
                Debug.LogError("PondLab: missing World/Geometry.");
                return;
            }

            BuildFieldRing(geometry, grass);
            BuildBasin(geometry, stone);
            BuildPond(geometry, water);
            BuildFoamTestProp(geometry, stone);

            PondPrefabBuilder.EnsurePrefab();

            GameSceneBootstrap.WireCameraToPlayer();
            GameSceneBootstrap.EnsureHierarchyScaffold();

            EditorSceneManager.SaveScene(scene, ScenePath);
            GameSceneBootstrap.EnsureInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"PondLab: built {ScenePath} (circular + waves).");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        public static void EnsureWaterMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            var shader = Shader.Find(WaterShaderName);
            if (shader == null)
            {
                Debug.LogError($"PondLab: shader {WaterShaderName} not found (recompile?).");
                return;
            }

            if (existing == null)
            {
                EnsureFolder("Assets/Art");
                EnsureFolder("Assets/Art/Materials");
                existing = new Material(shader) { name = "Water" };
                AssetDatabase.CreateAsset(existing, WaterMatPath);
            }
            else if (existing.shader != shader)
                existing.shader = shader;

            var waveCells = PondPrefabBuilder.EnsureWaveCellsTexture();

            existing.SetColor("_ShallowColor", new Color(0.45f, 0.68f, 0.72f, 0.92f));
            existing.SetColor("_DeepColor", new Color(0.10f, 0.14f, 0.32f, 0.96f));
            existing.SetColor("_NightDeepColor", new Color(0.04f, 0.06f, 0.14f, 0.98f));
            existing.SetColor("_RimColor", new Color(0.70f, 0.78f, 0.74f, 1f));
            existing.SetColor("_FoamColor", new Color(0.92f, 0.94f, 0.90f, 1f));
            existing.SetColor("_WaveColor", new Color(0.82f, 0.90f, 0.94f, 1f));
            existing.SetColor("_SkimColor", new Color(0.82f, 0.76f, 0.55f, 1f));
            existing.SetVector("_PondCenter", new Vector4(0f, 0f, 0f, 0f));
            existing.SetFloat("_PondRadius", HoleHalf + ShoreOverlap);
            existing.SetFloat("_DepthBands", 4f);
            existing.SetFloat("_CenterDepthBoost", 1f);
            existing.SetFloat("_RimWidth", 0.05f);
            existing.SetFloat("_FoamDistance", 0.4f);
            existing.SetFloat("_FoamBands", 2f);
            existing.SetFloat("_ShoreFoamWidth", 0.11f);
            existing.SetFloat("_DebugFoam", 0f);
            existing.SetFloat("_WaveScale", 1.5f);
            existing.SetFloat("_WaveSpeed", 0.7f);
            existing.SetFloat("_WaveWidth", 0.14f);
            existing.SetFloat("_WaveStrength", 0.45f);
            existing.SetFloat("_FloatFoamScale", 2.2f);
            existing.SetFloat("_FloatFoamStrength", 0.35f);
            existing.SetFloat("_SkimStrength", 0.38f);
            if (waveCells != null)
                existing.SetTexture("_WaveCells", waveCells);
            EditorUtility.SetDirty(existing);
        }

        static void BuildFieldRing(Transform geometry, Material grass)
        {
            var ring = new GameObject("FieldFloor");
            ring.transform.SetParent(geometry, false);
            ring.transform.localPosition = new Vector3(0f, FloorY, 0f);

            var mf = ring.AddComponent<MeshFilter>();
            mf.sharedMesh = PondGeometry.BuildBoxWithCircleHole(FieldHalf, HoleHalf, 24);
            var mr = ring.AddComponent<MeshRenderer>();
            mr.sharedMaterial = grass;
            var col = ring.AddComponent<MeshCollider>();
            col.sharedMesh = mf.sharedMesh;
        }

        static void BuildBasin(Transform geometry, Material stone)
        {
            var basin = new GameObject("PondBasin");
            basin.transform.SetParent(geometry, false);
            PondGeometry.MakeBasinStep("Step_1", basin.transform, HoleHalf * 0.95f, -0.35f, 0.12f, stone);
            PondGeometry.MakeBasinStep("Step_2", basin.transform, HoleHalf * 0.7f, -0.75f, 0.12f, stone);
            PondGeometry.MakeBasinStep("Step_3", basin.transform, HoleHalf * 0.42f, -1.2f, 0.12f, stone);
            PondGeometry.MakeBasinStep("Bottom", basin.transform, HoleHalf * 0.22f, -1.55f, 0.12f, stone);
        }

        static void BuildPond(Transform geometry, Material water)
        {
            var root = new GameObject("Pond");
            root.transform.SetParent(geometry, false);
            root.transform.localPosition = Vector3.zero;

            var surface = root.AddComponent<PondSurface>();
            surface.HalfExtent = HoleHalf + ShoreOverlap;

            var disc = PondGeometry.MakeFlatDisc(
                "Water",
                root.transform,
                HoleHalf + ShoreOverlap,
                WaterY,
                water,
                shadows: false,
                PondPrefabBuilder.EnsureDiscMesh());

            surface.SetWaterRenderer(disc.GetComponent<MeshRenderer>());
            surface.Push();
        }

        static void BuildFoamTestProp(Transform geometry, Material stone)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "FoamTestCrate";
            go.transform.SetParent(geometry, false);
            // on the circular shore
            go.transform.localPosition = new Vector3(HoleHalf - 0.7f, 0.35f, 0.4f);
            go.transform.localScale = new Vector3(1.1f, 0.9f, 1.1f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && stone != null)
                mr.sharedMaterial = stone;
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
