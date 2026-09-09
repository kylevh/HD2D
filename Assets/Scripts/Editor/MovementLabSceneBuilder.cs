using KVH.Game.Interaction;
using KVH.Game.Lighting;
using KVH.KylePixelator;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KVH.Game.Editor
{
    // Outdoor movement sandbox: shared playable systems + large checker floor + course.
    public static class MovementLabSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/MovementLab.unity";
        const string CheckerMatPath = "Assets/Art/Materials/Checker.mat";
        const string PlayerMatPath = "Assets/Art/Materials/Player.mat";

        // World size of the walkable checker (Unity Plane = 10 units at scale 1).
        const float FloorWorldSize = 80f;

        [MenuItem("KVH/Debug/Scenes/Build MovementLab")]
        public static void Build()
        {
            var day = AssetDatabase.LoadAssetAtPath<LightingProfile>(GameSceneBootstrap.DayProfilePath);
            var night = AssetDatabase.LoadAssetAtPath<LightingProfile>(GameSceneBootstrap.NightProfilePath);
            var checker = AssetDatabase.LoadAssetAtPath<Material>(CheckerMatPath);
            if (day == null || night == null || checker == null)
            {
                Debug.LogError("MovementLab: missing Day/Night profiles or Checker.mat.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameSceneBootstrap.EnsurePlayableSystems(
                GameSceneBootstrap.LightingPreset.OutdoorCycle,
                spawnPlayerIfMissing: true);

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.transform.position = new Vector3(0f, 0.1f, 0f);
                var visual = player.transform.Find("Visual");
                var playerMat = AssetDatabase.LoadAssetAtPath<Material>(PlayerMatPath);
                if (visual != null && playerMat != null)
                {
                    var mr = visual.GetComponent<MeshRenderer>();
                    if (mr != null)
                        mr.sharedMaterial = playerMat;
                    if (visual.GetComponent<VisualSnap>() == null)
                        visual.gameObject.AddComponent<VisualSnap>();
                }
            }

            // Large checker floor (no grass / lab props)
            var world = GameObject.Find("World") ?? new GameObject("World");
            world.name = "World";
            var floorScale = FloorWorldSize / 10f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "CheckerFloor";
            floor.transform.SetParent(world.transform, false);
            floor.transform.localPosition = Vector3.zero;
            floor.transform.localScale = new Vector3(floorScale, 1f, floorScale);
            var floorMr = floor.GetComponent<MeshRenderer>();

            var tiles = Mathf.Max(1f, FloorWorldSize / 2f);
            const string tiledMatPath = "Assets/Art/Materials/Checker_MovementLab.mat";
            var runtimeChecker = AssetDatabase.LoadAssetAtPath<Material>(tiledMatPath);
            if (runtimeChecker == null)
            {
                runtimeChecker = new Material(checker) { name = "Checker_MovementLab" };
                AssetDatabase.CreateAsset(runtimeChecker, tiledMatPath);
            }
            else
            {
                runtimeChecker.shader = checker.shader;
                runtimeChecker.CopyPropertiesFromMaterial(checker);
            }
            if (runtimeChecker.HasProperty("_BaseMap"))
                runtimeChecker.SetTextureScale("_BaseMap", new Vector2(tiles, tiles));
            runtimeChecker.mainTextureScale = new Vector2(tiles, tiles);
            EditorUtility.SetDirty(runtimeChecker);
            if (floorMr != null)
                floorMr.sharedMaterial = runtimeChecker;

            PopulateCourse(world.transform);
            GameSceneBootstrap.OrganizeMovementLabContent();
            GameSceneBootstrap.EnsureHierarchyScaffold();
            GameSceneBootstrap.WireCameraToPlayer();

            EditorSceneManager.SaveScene(scene, ScenePath);
            GameSceneBootstrap.EnsureInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"MovementLab: built {ScenePath} ({FloorWorldSize}u checker + course). PixelLab unchanged.");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        [MenuItem("KVH/Debug/Scenes/Populate MovementLab Course")]
        public static void PopulateCourseMenu()
        {
            var world = GameObject.Find("World");
            if (world == null)
            {
                Debug.LogError("MovementLab: open MovementLab and ensure a World root exists.");
                return;
            }

            PopulateCourse(world.transform);
            GameSceneBootstrap.OrganizeMovementLabContent();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("MovementLab: course props populated.");
        }

        // Movement playground: cover, ramps, pillars, crates — ToonLit mats for cel/shadow checks.
        public static void PopulateCourse(Transform world)
        {
            var stone = LoadMat("Assets/Art/Materials/Stone.mat");
            var wood = LoadMat("Assets/Art/Materials/Wood.mat");
            var clay = LoadMat("Assets/Art/Materials/Clay.mat");
            var teal = LoadMat("Assets/Art/Materials/Teal.mat");
            var sand = LoadMat("Assets/Art/Materials/Sand.mat");
            var ink = LoadMat("Assets/Art/Materials/Ink.mat");

            var existing = world.Find("Course");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var course = new GameObject("Course");
            course.transform.SetParent(world, false);

            var scale = new GameObject("ScaleRef");
            scale.transform.SetParent(course.transform, false);
            MarkInteractable(MakeBlock(scale.transform, "Unit_1", new Vector3(-2f, 0.5f, -2f), new Vector3(1f, 1f, 1f), teal));
            MakeBlock(scale.transform, "Unit_0_5", new Vector3(-3.2f, 0.25f, -2f), new Vector3(0.5f, 0.5f, 0.5f), sand);
            MakeBlock(scale.transform, "Unit_2", new Vector3(-0.5f, 1f, -2f), new Vector3(2f, 2f, 2f), clay);

            var cover = new GameObject("Cover");
            cover.transform.SetParent(course.transform, false);
            MakeBlock(cover.transform, "Wall_N", new Vector3(0f, 0.75f, 6f), new Vector3(10f, 1.5f, 0.4f), stone);
            MakeBlock(cover.transform, "Wall_E", new Vector3(6f, 0.75f, 0f), new Vector3(0.4f, 1.5f, 8f), stone);
            MakeBlock(cover.transform, "Wall_GapL", new Vector3(-5f, 0.6f, 3f), new Vector3(3f, 1.2f, 0.35f), wood);
            MakeBlock(cover.transform, "Wall_GapR", new Vector3(3.5f, 0.6f, -4f), new Vector3(4f, 1.2f, 0.35f), wood);
            MakeBlock(cover.transform, "ThinFence", new Vector3(-3f, 0.5f, 1.5f), new Vector3(0.2f, 1f, 4f), ink);

            var pillars = new GameObject("Pillars");
            pillars.transform.SetParent(course.transform, false);
            MakeBlock(pillars.transform, "Pillar_A", new Vector3(4f, 1.5f, 4f), new Vector3(1f, 3f, 1f), clay);
            MakeBlock(pillars.transform, "Pillar_B", new Vector3(-4f, 1.5f, 5f), new Vector3(1.2f, 3f, 1.2f), clay);
            MakeBlock(pillars.transform, "Pillar_C", new Vector3(8f, 1.25f, -2f), new Vector3(0.9f, 2.5f, 0.9f), stone);
            MakeCylinder(pillars.transform, "Column", new Vector3(-7f, 1.25f, -3f), new Vector3(1.2f, 2.5f, 1.2f), teal);

            var crates = new GameObject("Crates");
            crates.transform.SetParent(course.transform, false);
            MakeBlock(crates.transform, "Crate_1", new Vector3(2.5f, 0.35f, 2f), new Vector3(0.7f, 0.7f, 0.7f), sand);
            MakeBlock(crates.transform, "Crate_2", new Vector3(3.3f, 0.35f, 2.1f), new Vector3(0.7f, 0.7f, 0.7f), sand);
            MakeBlock(crates.transform, "Crate_3", new Vector3(2.9f, 1.05f, 2.05f), new Vector3(0.7f, 0.7f, 0.7f), wood);
            MarkInteractable(MakeBlock(crates.transform, "Crate_Wide", new Vector3(-2f, 0.4f, 4.5f), new Vector3(1.4f, 0.8f, 0.9f), wood));
            MarkInteractable(MakeBlock(crates.transform, "BarrelProxy", new Vector3(5.5f, 0.5f, 1f), new Vector3(0.8f, 1f, 0.8f), sand));

            var interactables = new GameObject("Interactables");
            interactables.transform.SetParent(course.transform, false);
            var chest = new GameObject("Chest");
            chest.transform.SetParent(interactables.transform, false);
            chest.transform.localPosition = new Vector3(1.6f, 0f, 1.4f);
            MakeBlock(chest.transform, "Body", new Vector3(0f, 0.28f, 0f), new Vector3(0.9f, 0.56f, 0.62f), wood);
            MakeBlock(chest.transform, "Lid", new Vector3(0f, 0.64f, 0f), new Vector3(0.96f, 0.16f, 0.68f), sand);
            MarkInteractable(chest);

            SpawnNpcProxy(course.transform, new Vector3(0.5f, 0f, 3.2f));

            var climb = new GameObject("Climb");
            climb.transform.SetParent(course.transform, false);
            MakeBlock(climb.transform, "Step_1", new Vector3(-8f, 0.2f, 2f), new Vector3(2f, 0.4f, 1.2f), stone);
            MakeBlock(climb.transform, "Step_2", new Vector3(-8f, 0.5f, 3f), new Vector3(2f, 0.4f, 1.2f), stone);
            MakeBlock(climb.transform, "Step_3", new Vector3(-8f, 0.8f, 4f), new Vector3(2f, 0.4f, 1.2f), stone);
            MakeBlock(climb.transform, "Landing", new Vector3(-8f, 1.1f, 5.5f), new Vector3(3f, 0.35f, 2.5f), stone);
            var ramp = MakeBlock(climb.transform, "Ramp", new Vector3(10f, 0.6f, 3f), new Vector3(4f, 0.25f, 2f), wood);
            ramp.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);

            var lookout = new GameObject("Lookout");
            lookout.transform.SetParent(course.transform, false);
            MakeBlock(lookout.transform, "Deck", new Vector3(0f, 1.6f, -10f), new Vector3(6f, 0.35f, 4f), wood);
            MakeBlock(lookout.transform, "Post_L", new Vector3(-2.5f, 0.8f, -10f), new Vector3(0.4f, 1.6f, 0.4f), stone);
            MakeBlock(lookout.transform, "Post_R", new Vector3(2.5f, 0.8f, -10f), new Vector3(0.4f, 1.6f, 0.4f), stone);
            MakeBlock(lookout.transform, "Rail", new Vector3(0f, 2.1f, -11.7f), new Vector3(6f, 0.25f, 0.25f), ink);
            MakeBlock(lookout.transform, "AccessStep_1", new Vector3(0f, 0.35f, -7.5f), new Vector3(2f, 0.7f, 1f), stone);
            MakeBlock(lookout.transform, "AccessStep_2", new Vector3(0f, 0.9f, -8.5f), new Vector3(2f, 0.7f, 1f), stone);

            var corridor = new GameObject("Corridor");
            corridor.transform.SetParent(course.transform, false);
            MakeBlock(corridor.transform, "Side_L", new Vector3(14f, 1f, 0f), new Vector3(0.4f, 2f, 8f), clay);
            MakeBlock(corridor.transform, "Side_R", new Vector3(17f, 1f, 0f), new Vector3(0.4f, 2f, 8f), clay);
            MakeBlock(corridor.transform, "EndCap", new Vector3(15.5f, 1f, 4.2f), new Vector3(3.4f, 2f, 0.4f), clay);

            var debris = new GameObject("Debris");
            debris.transform.SetParent(course.transform, false);
            MakeBlock(debris.transform, "Rock_1", new Vector3(1f, 0.2f, -5f), new Vector3(0.8f, 0.4f, 0.6f), stone);
            MakeBlock(debris.transform, "Rock_2", new Vector3(-6f, 0.15f, -6f), new Vector3(1.1f, 0.3f, 0.7f), stone);
            MakeBlock(debris.transform, "Slab", new Vector3(7f, 0.12f, 7f), new Vector3(2f, 0.25f, 1.2f), sand);
            MakeBlock(debris.transform, "TiltBlock", new Vector3(-10f, 0.4f, -1f), new Vector3(1.2f, 0.8f, 0.6f), wood)
                .transform.localRotation = Quaternion.Euler(0f, 35f, 0f);

            var rim = new GameObject("RimMarkers");
            rim.transform.SetParent(course.transform, false);
            MakeBlock(rim.transform, "N", new Vector3(0f, 0.15f, 28f), new Vector3(8f, 0.3f, 0.4f), ink);
            MakeBlock(rim.transform, "S", new Vector3(0f, 0.15f, -28f), new Vector3(8f, 0.3f, 0.4f), ink);
            MakeBlock(rim.transform, "E", new Vector3(28f, 0.15f, 0f), new Vector3(0.4f, 0.3f, 8f), ink);
            MakeBlock(rim.transform, "W", new Vector3(-28f, 0.15f, 0f), new Vector3(0.4f, 0.3f, 8f), ink);
        }

        static void MarkInteractable(GameObject go, float range = 1.7f)
        {
            var marker = go.GetComponent<Interactable>();
            if (marker == null)
                marker = go.AddComponent<Interactable>();
            var so = new SerializedObject(marker);
            so.FindProperty("kind").enumValueIndex = (int)InteractableKind.Prop;
            so.FindProperty("range").floatValue = range;
            so.FindProperty("useOutline").boolValue = true;
            so.FindProperty("promptLabel").stringValue = "F";
            so.ApplyModifiedPropertiesWithoutUndo();
            marker.CollectRenderers();
        }

        static GameObject SpawnNpcProxy(Transform parent, Vector3 localPos)
        {
            var npcMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Npc.mat");
            if (npcMat == null)
            {
                var shader = Shader.Find("KVH/KylePixelatorToonLit");
                npcMat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
                npcMat.name = "Npc";
                if (npcMat.HasProperty("_BaseColor"))
                    npcMat.SetColor("_BaseColor", new Color(0.78f, 0.62f, 0.86f, 1f));
                if (npcMat.HasProperty("_ShadowTint"))
                    npcMat.SetColor("_ShadowTint", new Color(0.48f, 0.36f, 0.58f, 1f));
                if (npcMat.HasProperty("_HighlightTint"))
                    npcMat.SetColor("_HighlightTint", new Color(0.95f, 0.88f, 1f, 1f));
                AssetDatabase.CreateAsset(npcMat, "Assets/Art/Materials/Npc.mat");
            }

            var root = new GameObject("Npc_Proxy");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Body";
            capsule.transform.SetParent(root.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 1f, 0f);
            capsule.transform.localRotation = Quaternion.identity;
            capsule.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
            Object.DestroyImmediate(capsule.GetComponent<Collider>());
            var mr = capsule.GetComponent<MeshRenderer>();
            mr.sharedMaterial = npcMat;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 0.4f;
            col.isTrigger = true;

            var marker = root.AddComponent<Interactable>();
            var so = new SerializedObject(marker);
            so.FindProperty("kind").enumValueIndex = (int)InteractableKind.Npc;
            so.FindProperty("range").floatValue = 2.5f;
            so.FindProperty("useOutline").boolValue = true;
            so.FindProperty("promptLabel").stringValue = "Talk";
            so.FindProperty("promptOffset").vector3Value = new Vector3(0f, 0.2f, 0f);
            so.FindProperty("outlineColor").colorValue = new Color(0.78f, 0.90f, 1f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
            marker.CollectRenderers();
            return root;
        }

        static Material LoadMat(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

        static GameObject MakeBlock(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && mat != null)
                renderer.sharedMaterial = mat;
            return go;
        }

        static GameObject MakeCylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && mat != null)
                renderer.sharedMaterial = mat;
            return go;
        }
    }
}
