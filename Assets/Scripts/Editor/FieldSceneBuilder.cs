using KVH.Game.Foliage;
using KVH.Game.Interaction;
using KVH.Game.Inventory;
using KVH.Game.Lighting;
using KVH.KylePixelator;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KVH.Game.Editor
{
    // Outdoor grass field: PixelLab content in the standard playable hierarchy.
    public static class FieldSceneBuilder
    {
        const string SourceScenePath = "Assets/Scenes/PixelLab.unity";
        const string ScenePath = "Assets/Scenes/Field.unity";
        const string PlayerMatPath = "Assets/Art/Materials/Player.mat";

        [MenuItem("KVH/Debug/Scenes/Build Field")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
            {
                Debug.LogError("Field: missing PixelLab source scene.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                AssetDatabase.DeleteAsset(ScenePath);

            if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
            {
                Debug.LogError("Field: failed to copy PixelLab → Field.");
                return;
            }

            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            OrganizeFromPixelLab();
            EditorSceneManager.SaveScene(scene);
            GameSceneBootstrap.EnsureInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Field: built {ScenePath} from PixelLab (standardized systems + grass).");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        [MenuItem("KVH/Debug/Scenes/Organize Field Hierarchy")]
        public static void OrganizeMenu()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorUtility.DisplayDialog(
                        "Organize Field",
                        "Active scene is not Field.unity. Organize this scene as a Field layout anyway?",
                        "Organize",
                        "Cancel"))
                    return;
            }

            OrganizeFromPixelLab();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        public static void OrganizeFromPixelLab()
        {
            RenameRoot("CameraPivot", "PixelCamera");
            RenameRoot("Input", "GameInput");

            // PixelLab kept key/fill as loose roots — fold under DayNightLighting.
            var lighting = GameObject.Find("DayNightLighting")
                ?? GameObject.Find("Lighting")
                ?? GameObject.Find("RoomLighting");
            if (lighting == null)
                lighting = new GameObject("DayNightLighting");
            else
                lighting.name = "DayNightLighting";

            ReparentRootIfPresent("Directional Light", lighting.transform, "KeyLight");
            ReparentRootIfPresent("Fill Light", lighting.transform, "FillLight");

            // Shared playable stack (Flow / UI / Input / camera follow / scaffold).
            GameSceneBootstrap.EnsurePlayableSystems(
                GameSceneBootstrap.LightingPreset.OutdoorCycle,
                spawnPlayerIfMissing: false);

            // PixelLab player is a bare capsule — swap to Hero when possible.
            UpgradePlayer();

            OrganizeWorldContent();
            EnsureFieldPond();
            // drop leftover modular TerrainGrid if an old Field still has one
            StripTerrainGrid();
            GameSceneBootstrap.WireCameraToPlayer();
            GameSceneBootstrap.EnsureHierarchyScaffold();
            GameSceneBootstrap.HideUiFromSceneView();

            // Match PixelLab daytime knobs (cycle paused at noon).
            var director = Object.FindAnyObjectByType<KVH.Game.Lighting.LightingDirector>();
            if (director != null)
            {
                var so = new SerializedObject(director);
                so.FindProperty("mode").enumValueIndex = (int)KVH.Game.Lighting.LightingDirector.Mode.Cycle;
                so.FindProperty("useNight").boolValue = false;
                so.FindProperty("timeOfDayHours").floatValue = 12f;
                so.FindProperty("dayLengthSeconds").floatValue = 180f;
                so.FindProperty("autoAdvance").boolValue = false;
                so.FindProperty("rotateKeyWithTime").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                director.Apply();
            }
        }

        static void OrganizeWorldContent()
        {
            GameSceneBootstrap.EnsureHierarchyScaffold();
            var world = GameObject.Find("World");
            if (world == null)
                return;

            var geometry = world.transform.Find("Geometry");
            if (geometry == null)
            {
                var g = new GameObject("Geometry");
                g.transform.SetParent(world.transform, false);
                geometry = g.transform;
            }

            var lab = world.transform.Find("_Lab");
            if (lab == null)
            {
                var l = new GameObject("_Lab");
                l.transform.SetParent(world.transform, false);
                lab = l.transform;
            }

            var interactables = world.transform.Find("Interactables");
            if (interactables == null)
            {
                var i = new GameObject("Interactables");
                i.transform.SetParent(world.transform, false);
                interactables = i.transform;
            }

            // Grass plane + tuft draw stay under World (tuft host must stay identity-scale).
            // ProtoLab platform clutter → _Lab.
            TrySetParent(GameObject.Find("ProtoLab"), lab, worldPositionStays: true);

            // Cloud shadows are world presentation — keep beside grass under World.
            var cloud = GameObject.Find("CloudShadowDriver");
            if (cloud != null && cloud.transform.parent != world.transform)
                cloud.transform.SetParent(world.transform, true);

            // Prefer inactive-inclusive lookup (PixelLab VolumeLight starts disabled).
            GameObject vol = null;
            var vols = Object.FindObjectsByType<VolumeLight>(FindObjectsInactive.Include);
            if (vols != null && vols.Length > 0)
                vol = vols[0].gameObject;
            if (vol == null)
            {
                foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (root.name == "VolumeLight")
                    {
                        vol = root;
                        break;
                    }
                }
            }
            if (vol != null)
                vol.transform.SetParent(interactables, true);

            // Orphan proto mesh used as authoring ref
            Transform protoTuft = world.transform.Find("GrassTuft_Proto");
            if (protoTuft == null)
            {
                var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
                for (var i = 0; i < all.Length; i++)
                {
                    if (all[i].name == "GrassTuft_Proto")
                    {
                        protoTuft = all[i];
                        break;
                    }
                }
            }
            if (protoTuft != null)
                protoTuft.SetParent(lab, true);

            // Ensure GrassTuftField still finds World for its draw host.
            var grass = GameObject.Find("GrassField");
            if (grass != null)
            {
                if (grass.transform.parent != world.transform)
                    grass.transform.SetParent(world.transform, true);
                var field = grass.GetComponent<GrassTuftField>();
                if (field != null)
                    EditorUtility.SetDirty(field);
            }

            // Drop empty Unity default camera/light leftovers if any slipped in
            DestroyIfNamedRoot("Main Camera");
        }

        // punch a grass hole + drop Pond prefab (away from tuft patch at +X/-Z)
        static void EnsureFieldPond()
        {
            const float holeHalf = PondPrefabBuilder.DefaultHoleHalf;
            var pondPos = new Vector3(-12f, 0f, 12f);

            GameSceneBootstrap.EnsureHierarchyScaffold();
            var world = GameObject.Find("World");
            if (world == null)
                return;

            var geometry = world.transform.Find("Geometry");
            if (geometry == null)
            {
                var g = new GameObject("Geometry");
                g.transform.SetParent(world.transform, false);
                geometry = g.transform;
            }

            // remove prior pond / clearing so rebuild is idempotent
            DestroyChildNamed(geometry, "Pond");
            DestroyChildNamed(world.transform, "Pond");
            var oldClearing = world.transform.Find("PondClearing");
            if (oldClearing != null)
                Object.DestroyImmediate(oldClearing.gameObject);

            var grass = GameObject.Find("GrassField");
            if (grass != null)
                PunchGrassHole(grass, pondPos, holeHalf);

            PondPrefabBuilder.InstantiatePond(geometry, pondPos, holeHalf);

            var grassField = grass != null ? grass.GetComponent<GrassTuftField>() : null;
            if (grassField != null)
            {
                grassField.SetExcludeCircle(
                    new Vector2(pondPos.x, pondPos.z),
                    holeHalf + 0.85f);
                grassField.Rebuild();
                EditorUtility.SetDirty(grassField);
            }
        }

        // replace grass plane with a square field mesh that has a circular hole at pondPos
        static void PunchGrassHole(GameObject grassField, Vector3 pondPos, float holeHalf)
        {
            var mr = grassField.GetComponent<MeshRenderer>();
            var mat = mr != null ? mr.sharedMaterial : AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Grass.mat");

            Bounds bounds;
            if (mr != null)
                bounds = mr.bounds;
            else
            {
                var ringProbe = grassField.transform.Find("FloorRing");
                if (ringProbe != null)
                {
                    var cmr = ringProbe.GetComponent<MeshRenderer>();
                    bounds = cmr != null ? cmr.bounds : new Bounds(Vector3.zero, new Vector3(80f, 0f, 80f));
                }
                else
                    bounds = new Bounds(Vector3.zero, new Vector3(80f, 0f, 80f));
            }

            float y = grassField.transform.position.y;
            float outerHalf = Mathf.Max(
                Mathf.Max(Mathf.Abs(bounds.min.x - pondPos.x), Mathf.Abs(bounds.max.x - pondPos.x)),
                Mathf.Max(Mathf.Abs(bounds.min.z - pondPos.z), Mathf.Abs(bounds.max.z - pondPos.z)));
            outerHalf = Mathf.Max(outerHalf, holeHalf + 2f);

            DestroyImmediateSafe(grassField.GetComponent<MeshCollider>());
            DestroyImmediateSafe(grassField.GetComponent<MeshFilter>());
            DestroyImmediateSafe(grassField.GetComponent<MeshRenderer>());
            grassField.transform.localScale = Vector3.one;

            var oldRing = grassField.transform.Find("FloorRing");
            if (oldRing != null)
                Object.DestroyImmediate(oldRing.gameObject);

            var ring = new GameObject("FloorRing");
            ring.transform.SetParent(grassField.transform, false);
            ring.transform.position = new Vector3(pondPos.x, y, pondPos.z);

            var mf = ring.AddComponent<MeshFilter>();
            mf.sharedMesh = PondGeometry.BuildBoxWithCircleHole(outerHalf, holeHalf, 28);
            var rmr = ring.AddComponent<MeshRenderer>();
            if (mat != null)
                rmr.sharedMaterial = mat;
            var col = ring.AddComponent<MeshCollider>();
            col.sharedMesh = mf.sharedMesh;
        }

        static void StripTerrainGrid()
        {
            GameSceneBootstrap.EnsureHierarchyScaffold();
            var world = GameObject.Find("World");
            if (world == null)
                return;
            var geometry = world.transform.Find("Geometry");
            DestroyChildNamed(geometry, "TerrainGrid");
            // orphan roots from older builds
            var loose = GameObject.Find("TerrainGrid");
            if (loose != null)
                Object.DestroyImmediate(loose);
        }

        static void DestroyChildNamed(Transform parent, string name)
        {
            if (parent == null)
                return;
            var t = parent.Find(name);
            if (t != null)
                Object.DestroyImmediate(t.gameObject);
        }

        static void DestroyImmediateSafe(Object obj)
        {
            if (obj != null)
                Object.DestroyImmediate(obj);
        }

        static void UpgradePlayer()
        {
            var existing = GameObject.FindGameObjectWithTag("Player");
            var heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameSceneBootstrap.PlayerPrefabPath);

            // Already a prefab instance of Hero — just ensure gameplay bits.
            if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing) == heroPrefab)
            {
                existing.name = "Player";
                if (existing.GetComponent<InteractionScanner>() == null)
                    existing.AddComponent<InteractionScanner>();
                if (existing.GetComponent<PlayerInventory>() == null)
                    existing.AddComponent<PlayerInventory>();
                return;
            }

            Vector3 pos = existing != null ? existing.transform.position : new Vector3(0f, 0.1f, 0f);
            Quaternion rot = existing != null ? existing.transform.rotation : Quaternion.identity;
            if (existing != null)
                Object.DestroyImmediate(existing);

            if (heroPrefab == null)
            {
                Debug.LogWarning("Field: Hero prefab missing; left without Player.");
                return;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(heroPrefab);
            player.name = "Player";
            player.transform.SetPositionAndRotation(pos, rot);

            if (player.GetComponent<InteractionScanner>() == null)
                player.AddComponent<InteractionScanner>();
            if (player.GetComponent<PlayerInventory>() == null)
                player.AddComponent<PlayerInventory>();

            var visual = player.transform.Find("Visual");
            var playerMat = AssetDatabase.LoadAssetAtPath<Material>(PlayerMatPath);
            if (visual != null)
            {
                if (playerMat != null)
                {
                    var mr = visual.GetComponent<MeshRenderer>();
                    if (mr != null)
                        mr.sharedMaterial = playerMat;
                }
                if (visual.GetComponent<VisualSnap>() == null)
                    visual.gameObject.AddComponent<VisualSnap>();
            }

            var playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                foreach (var t in player.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = playerLayer;
            }
        }

        static void RenameRoot(string from, string to)
        {
            var go = GameObject.Find(from);
            if (go != null)
                go.name = to;
        }

        static void ReparentRootIfPresent(string name, Transform parent, string renameTo = null)
        {
            var go = GameObject.Find(name);
            if (go == null)
                return;
            if (renameTo != null)
                go.name = renameTo;
            if (go.transform.parent != parent)
                go.transform.SetParent(parent, true);
        }

        static void TrySetParent(GameObject go, Transform parent, bool worldPositionStays)
        {
            if (go == null || parent == null)
                return;
            if (go.transform.parent != parent)
                go.transform.SetParent(parent, worldPositionStays);
        }

        static void DestroyIfNamedRoot(string name)
        {
            var go = GameObject.Find(name);
            if (go != null && go.transform.parent == null
                && go.GetComponent<CameraRig>() == null
                && go.GetComponentInChildren<CameraRig>() == null)
            {
                // Don't destroy PixelCamera's Main Camera child — only stray roots.
                if (go.name == "Main Camera" && go.GetComponentInChildren<LowResOutput>() != null)
                    return;
                // Stray default cameras without LowResOutput
                if (go.GetComponent<UnityEngine.Camera>() != null && go.GetComponentInChildren<LowResOutput>() == null)
                    Object.DestroyImmediate(go);
            }
        }
    }
}
