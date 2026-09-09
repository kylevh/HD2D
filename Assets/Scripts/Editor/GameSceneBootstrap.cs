using KVH.Game.Camera;
using KVH.Game.Dialogue;
using KVH.Game.Flow;
using KVH.Game.Interaction;
using KVH.Game.Inventory;
using KVH.Game.Lighting;
using KVH.Game.UI;
using KVH.KylePixelator;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace KVH.Game.Editor
{
    // Scene roots for flow + overlay UI + hierarchy contract.
    // Present blit stays on PixelPresentCamera (cullingMask 0).
    public static class GameSceneBootstrap
    {
        public const string FlowPrefabPath = "Assets/Prefabs/Systems/GameFlow.prefab";
        public const string UiPrefabPath = "Assets/Prefabs/UI/GameUI.prefab";
        public const string InputPrefabPath = "Assets/Prefabs/Systems/Input.prefab";
        public const string PixelCameraPrefabPath = "Assets/Prefabs/Systems/PixelCamera.prefab";
        // fallback if Systems prefab is missing. do not instantiate this in playable scenes.
        public const string KylePixelCameraPrefabPath = "Assets/KylePixelator/Prefabs/PixelCamera.prefab";
        public const string DayNightLightingPrefabPath = "Assets/Prefabs/Systems/DayNightLighting.prefab";
        public const string PlayerPrefabPath = "Assets/Prefabs/Characters/Hero.prefab";
        public const string DayProfilePath = "Assets/Settings/Lighting/Day.asset";
        public const string NightProfilePath = "Assets/Settings/Lighting/Night.asset";
        public const string CheckerMatPath = "Assets/Art/Materials/Checker.mat";

        public const string SystemsSeparator = "── Systems ──";
        public const string SceneSeparator = "── Scene ──";

        public enum LightingPreset
        {
            OutdoorCycle = 0,
            IndoorNight = 1,
        }

        [MenuItem("KVH/Scenes/Ensure Scene Systems")]
        public static void EnsureMenu()
        {
            var preset = GameObject.Find("RoomLighting") != null || GameObject.Find("Lighting") != null
                ? LightingPreset.IndoorNight
                : LightingPreset.OutdoorCycle;
            EnsurePlayableSystems(preset, spawnPlayerIfMissing: true);
            WarnMissingRequired();
            HideUiFromSceneView();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("KVH/Debug/Scenes/Ensure GameFlow And GameUI")]
        public static void EnsureMenuLegacy() => EnsureMenu();

        [MenuItem("KVH/Scenes/Create Empty Playable Scene")]
        public static void CreateEmptyPlayableSceneMenu()
        {
            const string defaultPath = "Assets/Scenes/SceneSandbox.unity";
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Empty Playable Scene",
                "SceneSandbox",
                "unity",
                "Bare playable scene: systems + player + small floor.",
                "Assets/Scenes");
            if (string.IsNullOrEmpty(path))
                path = defaultPath;

            CreateEmptyPlayableScene(path, LightingPreset.OutdoorCycle);
        }

        [MenuItem("KVH/Debug/Scenes/Organize MovementLab Hierarchy")]
        public static void OrganizeMovementLabMenu()
        {
            EnsurePlayableSystems(LightingPreset.OutdoorCycle, spawnPlayerIfMissing: true);
            OrganizeMovementLabContent();
            WarnMissingRequired();
            HideUiFromSceneView();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // Overlay canvas is a giant screen in Scene view; Game view still draws it.
        public static void HideUiFromSceneView()
        {
            var vis = SceneVisibilityManager.instance;
            var roots = Object.FindObjectsByType<UiRoot>(FindObjectsInactive.Include);
            for (var i = 0; i < roots.Length; i++)
            {
                var go = roots[i].gameObject;
                if (!vis.IsHidden(go, false))
                    vis.Hide(go, true);
            }
        }

        // Full playable stack. Call from scene builders or Ensure menu.
        public static void EnsurePlayableSystems(LightingPreset lighting, bool spawnPlayerIfMissing)
        {
            EnsureSystemPrefabsExist();
            EnsurePixelCamera();
            EnsureLighting(lighting);
            EnsureInScene();
            EnsureHierarchyScaffold();
            if (spawnPlayerIfMissing)
                EnsurePlayer();
            WireCameraToPlayer();
            ReorderRoots();
            HideUiFromSceneView();
        }

        public static void CreateEmptyPlayableScene(string scenePath, LightingPreset lighting)
        {
            EnsureSystemPrefabsExist();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsurePlayableSystems(lighting, spawnPlayerIfMissing: true);
            EnsurePrototypeFloor();
            ReorderRoots();

            EditorSceneManager.SaveScene(scene, scenePath);
            EnsureInBuildSettings(scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Empty playable scene: {scenePath}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        }

        // Make sure Systems prefabs exist so new scenes only instantiate.
        public static void EnsureSystemPrefabsExist()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Systems");
            EnsurePixelCameraPrefabAsset();
            EnsureDayNightLightingPrefabAsset();
        }

        public static void EnsureInScene()
        {
            EnsureNamedPrefab("GameInput", InputPrefabPath, null);
            EnsureNamedPrefab("GameFlow", FlowPrefabPath, () => new GameObject("GameFlow").AddComponent<GameFlow>());

            var flow = Object.FindAnyObjectByType<GameFlow>();
            if (flow != null && flow.GetComponent<DialogueRunner>() == null)
                flow.gameObject.AddComponent<DialogueRunner>();

            if (Object.FindAnyObjectByType<UiRoot>() == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabPath);
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.name = "GameUI";
                }
                else
                    CreateUiRootInScene();
            }

            var ui = Object.FindAnyObjectByType<UiRoot>();
            if (ui != null)
            {
                if (ui.GetComponent<DialogueBox>() == null)
                    ui.gameObject.AddComponent<DialogueBox>();
                if (ui.GetComponent<ItemToast>() == null)
                    ui.gameObject.AddComponent<ItemToast>();
                if (ui.GetComponent<InteractPrompt>() == null)
                    ui.gameObject.AddComponent<InteractPrompt>();
                if (ui.GetComponent<ExplorationHud>() == null)
                    ui.gameObject.AddComponent<ExplorationHud>();
                if (ui.GetComponent<PauseMenu>() == null)
                    ui.gameObject.AddComponent<PauseMenu>();
            }

            HideUiFromSceneView();
        }

        public static GameObject EnsurePixelCamera()
        {
            var existing = GameObject.Find("PixelCamera");
            if (existing == null)
            {
                // RoomDemo used CameraPivot historically
                var pivot = GameObject.Find("CameraPivot");
                if (pivot != null)
                {
                    pivot.name = "PixelCamera";
                    existing = pivot;
                }
            }

            if (existing != null)
            {
                EnsureGameCameraComponents(existing);
                return existing;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PixelCameraPrefabPath);
            if (prefab == null)
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KylePixelCameraPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("Scene systems: missing PixelCamera prefab.");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "PixelCamera";
            go.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.Euler(35f, 45f, 0f));
            EnsureGameCameraComponents(go);
            return go;
        }

        public static GameObject EnsureLighting(LightingPreset preset)
        {
            var outdoor = GameObject.Find("DayNightLighting");
            var indoor = GameObject.Find("RoomLighting");
            // legacy RoomDemo name
            var legacy = GameObject.Find("Lighting");
            if (legacy != null && outdoor == null && indoor == null)
            {
                legacy.name = preset == LightingPreset.IndoorNight ? "RoomLighting" : "DayNightLighting";
                indoor = preset == LightingPreset.IndoorNight ? legacy : null;
                outdoor = preset == LightingPreset.OutdoorCycle ? legacy : null;
            }

            // one lighting root per scene — rename instead of stacking both
            if (preset == LightingPreset.IndoorNight && outdoor != null && indoor == null)
            {
                outdoor.name = "RoomLighting";
                indoor = outdoor;
                outdoor = null;
            }
            if (preset == LightingPreset.OutdoorCycle && indoor != null && outdoor == null)
            {
                indoor.name = "DayNightLighting";
                outdoor = indoor;
                indoor = null;
            }

            GameObject root = outdoor != null ? outdoor : indoor;
            if (root == null)
            {
                if (preset == LightingPreset.IndoorNight)
                    root = CreateLightingRoot("RoomLighting", LightingPreset.IndoorNight);
                else
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DayNightLightingPrefabPath);
                    if (prefab != null)
                    {
                        root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        root.name = "DayNightLighting";
                    }
                    else
                        root = CreateLightingRoot("DayNightLighting", LightingPreset.OutdoorCycle);
                }
            }

            ConfigureLighting(root, preset);
            return root;
        }

        public static GameObject EnsurePlayer()
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
            {
                if (tagged.name != "Player")
                    tagged.name = "Player";
                EnsurePlayerGameplayBits(tagged);
                return tagged;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("Scene systems: missing Hero prefab.");
                return null;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 0.1f, 0f);
            EnsurePlayerGameplayBits(player);
            return player;
        }

        public static void WireCameraToPlayer()
        {
            var cam = Object.FindAnyObjectByType<GameCamera>();
            var player = GameObject.FindGameObjectWithTag("Player");
            if (cam == null || player == null)
                return;

            var so = new SerializedObject(cam);
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.FindProperty("height").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var fade = cam.GetComponent<CameraOcclusionFade>();
            if (fade != null)
                fade.SetTarget(player.transform);

            var director = Object.FindAnyObjectByType<LightingDirector>();
            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (director != null && rig != null)
            {
                var soDir = new SerializedObject(director);
                soDir.FindProperty("cameraRig").objectReferenceValue = rig;
                soDir.ApplyModifiedPropertiesWithoutUndo();
                director.Apply();
            }
        }

        public static void EnsurePrototypeFloor(float worldSize = 20f)
        {
            EnsureIdentityFolder("World/Geometry");
            var world = GameObject.Find("World");
            var geometry = world != null ? world.transform.Find("Geometry") : null;
            if (geometry == null)
                geometry = world != null ? world.transform : null;
            if (geometry == null)
                return;

            if (geometry.Find("PrototypeFloor") != null)
                return;

            var checker = AssetDatabase.LoadAssetAtPath<Material>(CheckerMatPath);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "PrototypeFloor";
            floor.transform.SetParent(geometry, false);
            floor.transform.localPosition = Vector3.zero;
            floor.transform.localScale = new Vector3(worldSize / 10f, 1f, worldSize / 10f);
            var mr = floor.GetComponent<MeshRenderer>();
            if (mr != null && checker != null)
                mr.sharedMaterial = checker;
        }

        static void EnsurePlayerGameplayBits(GameObject player)
        {
            if (player.GetComponent<InteractionScanner>() == null)
                player.AddComponent<InteractionScanner>();
            if (player.GetComponent<PlayerInventory>() == null)
                player.AddComponent<PlayerInventory>();

            var playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                foreach (var t in player.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = playerLayer;
            }
        }

        static void EnsureGameCameraComponents(GameObject pivot)
        {
            if (pivot.GetComponent<GameCamera>() == null)
                pivot.AddComponent<GameCamera>();
            if (pivot.GetComponent<CameraOcclusionFade>() == null)
                pivot.AddComponent<CameraOcclusionFade>();
        }

        static GameObject CreateLightingRoot(string name, LightingPreset preset)
        {
            var root = new GameObject(name);

            var keyGo = new GameObject("KeyLight");
            keyGo.transform.SetParent(root.transform, false);
            keyGo.transform.rotation = Quaternion.Euler(42f, 325f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.shadows = LightShadows.Hard;

            var fillGo = new GameObject("FillLight");
            fillGo.transform.SetParent(root.transform, false);
            fillGo.transform.rotation = Quaternion.Euler(50f, 145f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.shadows = LightShadows.None;

            root.AddComponent<LightingDirector>();
            ConfigureLighting(root, preset);
            return root;
        }

        static void ConfigureLighting(GameObject root, LightingPreset preset)
        {
            var director = root.GetComponent<LightingDirector>();
            if (director == null)
                director = root.AddComponent<LightingDirector>();

            var key = root.GetComponentInChildren<Light>();
            Light fill = null;
            var lights = root.GetComponentsInChildren<Light>();
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i].type != LightType.Directional)
                    continue;
                if (key == null || lights[i].shadows != LightShadows.None)
                    key = lights[i];
                else if (lights[i] != key)
                    fill = lights[i];
            }

            // Prefer named children when present
            var keyT = root.transform.Find("KeyLight") ?? root.transform.Find("Directional Light");
            var fillT = root.transform.Find("FillLight") ?? root.transform.Find("Fill Light");
            if (keyT != null)
                key = keyT.GetComponent<Light>();
            if (fillT != null)
                fill = fillT.GetComponent<Light>();

            var day = AssetDatabase.LoadAssetAtPath<LightingProfile>(DayProfilePath);
            var night = AssetDatabase.LoadAssetAtPath<LightingProfile>(NightProfilePath);
            var rig = Object.FindAnyObjectByType<CameraRig>();

            var so = new SerializedObject(director);
            so.FindProperty("dayProfile").objectReferenceValue = day;
            so.FindProperty("nightProfile").objectReferenceValue = night;
            so.FindProperty("keyLight").objectReferenceValue = key;
            so.FindProperty("fillLight").objectReferenceValue = fill;
            so.FindProperty("cameraRig").objectReferenceValue = rig;

            if (preset == LightingPreset.IndoorNight)
            {
                so.FindProperty("mode").enumValueIndex = (int)LightingDirector.Mode.Manual;
                so.FindProperty("useNight").boolValue = true;
                so.FindProperty("autoAdvance").boolValue = false;
                so.FindProperty("rotateKeyWithTime").boolValue = false;
            }
            else
            {
                so.FindProperty("mode").enumValueIndex = (int)LightingDirector.Mode.Cycle;
                so.FindProperty("useNight").boolValue = false;
                so.FindProperty("timeOfDayHours").floatValue = 12f;
                so.FindProperty("dayLengthSeconds").floatValue = 180f;
                so.FindProperty("autoAdvance").boolValue = true;
                so.FindProperty("rotateKeyWithTime").boolValue = true;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            director.Apply();
        }

        static void EnsurePixelCameraPrefabAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PixelCameraPrefabPath) != null)
                return;

            var kyle = AssetDatabase.LoadAssetAtPath<GameObject>(KylePixelCameraPrefabPath);
            if (kyle == null)
            {
                Debug.LogWarning("Cannot create Systems/PixelCamera prefab: KylePixelator PixelCamera missing.");
                return;
            }

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(kyle);
            temp.name = "PixelCamera";
            temp.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.Euler(35f, 45f, 0f));
            EnsureGameCameraComponents(temp);
            PrefabUtility.SaveAsPrefabAsset(temp, PixelCameraPrefabPath);
            Object.DestroyImmediate(temp);
        }

        static void EnsureDayNightLightingPrefabAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DayNightLightingPrefabPath) != null)
                return;

            var root = CreateLightingRoot("DayNightLighting", LightingPreset.OutdoorCycle);
            PrefabUtility.SaveAsPrefabAsset(root, DayNightLightingPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void EnsureNamedPrefab(string objectName, string prefabPath, System.Action createFallback)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
                return;

            if (objectName == "GameFlow" && Object.FindAnyObjectByType<GameFlow>() != null)
                return;
            if (objectName == "GameInput" && Object.FindAnyObjectByType<KVH.Game.Input.InputReader>() != null)
            {
                var reader = Object.FindAnyObjectByType<KVH.Game.Input.InputReader>();
                if (reader != null)
                    reader.gameObject.name = "GameInput";
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = objectName;
            }
            else
                createFallback?.Invoke();
        }

        public static void EnsureHierarchyScaffold()
        {
            EnsureSeparator(SystemsSeparator);
            EnsureSeparator(SceneSeparator);
            EnsureIdentityFolder("World");
            EnsureIdentityFolder("World/Geometry");
            EnsureIdentityFolder("World/Interactables");
            EnsureIdentityFolder("Gameplay");
            EnsureIdentityFolder("Gameplay/Npcs");
            EnsureIdentityFolder("Gameplay/Spawns");
            EnsureIdentityFolder("_Dynamic");

            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null && tagged.name != "Player")
                tagged.name = "Player";

            ReorderRoots();
        }

        public static void OrganizeMovementLabContent()
        {
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

            var interactables = world.transform.Find("Interactables");
            if (interactables == null)
            {
                var i = new GameObject("Interactables");
                i.transform.SetParent(world.transform, false);
                interactables = i.transform;
            }

            var lab = world.transform.Find("_Lab");
            if (lab == null)
            {
                var l = new GameObject("_Lab");
                l.transform.SetParent(world.transform, false);
                lab = l.transform;
            }

            TryReparent(world.transform, "CheckerFloor", geometry);
            TryReparent(world.transform, "GrassTuftDraw", geometry);
            TryReparent(world.transform, "Course", geometry);

            var course = geometry.Find("Course") ?? world.transform.Find("Course");
            if (course != null)
            {
                var courseInteract = course.Find("Interactables");
                if (courseInteract != null)
                {
                    while (courseInteract.childCount > 0)
                    {
                        var child = courseInteract.GetChild(0);
                        child.SetParent(interactables, true);
                    }
                    Object.DestroyImmediate(courseInteract.gameObject);
                }

                TryReparent(course, "ScaleRef", lab);
                TryReparent(course, "RimMarkers", lab);
            }

            var gameplay = GameObject.Find("Gameplay");
            Transform npcs = null;
            if (gameplay != null)
            {
                npcs = gameplay.transform.Find("Npcs");
                if (npcs == null)
                {
                    var n = new GameObject("Npcs");
                    n.transform.SetParent(gameplay.transform, false);
                    npcs = n.transform;
                }
            }

            Transform npcProxy = null;
            var found = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i].name == "Npc_Proxy")
                {
                    npcProxy = found[i];
                    break;
                }
            }

            if (npcProxy != null && npcs != null && npcProxy.parent != npcs)
                npcProxy.SetParent(npcs, true);

            ReorderRoots();
        }

        static void TryReparent(Transform fromParent, string childName, Transform newParent)
        {
            if (fromParent == null || newParent == null)
                return;
            var child = fromParent.Find(childName);
            if (child == null || child.parent == newParent)
                return;
            child.SetParent(newParent, true);
        }

        static void EnsureSeparator(string name)
        {
            if (GameObject.Find(name) != null)
                return;
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
        }

        static void EnsureIdentityFolder(string path)
        {
            var parts = path.Split('/');
            Transform parent = null;
            for (var i = 0; i < parts.Length; i++)
            {
                Transform found = null;
                if (parent == null)
                {
                    var root = GameObject.Find(parts[i]);
                    if (root != null)
                        found = root.transform;
                }
                else
                    found = parent.Find(parts[i]);

                if (found == null)
                {
                    var go = new GameObject(parts[i]);
                    if (parent != null)
                        go.transform.SetParent(parent, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    found = go.transform;
                }

                parent = found;
            }
        }

        static void ReorderRoots()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
                return;

            var order = new[]
            {
                SystemsSeparator,
                "PixelCamera",
                "GameInput",
                "GameFlow",
                "GameUI",
                "DayNightLighting",
                "RoomLighting",
                SceneSeparator,
                "Player",
                "World",
                "Gameplay",
                "_Dynamic",
            };

            var sibling = 0;
            for (var i = 0; i < order.Length; i++)
            {
                var go = GameObject.Find(order[i]);
                if (go == null || go.transform.parent != null)
                    continue;
                go.transform.SetSiblingIndex(sibling++);
            }
        }

        static void WarnMissingRequired()
        {
            if (GameObject.Find("PixelCamera") == null
                && Object.FindAnyObjectByType<CameraRig>() == null)
                Debug.LogWarning("Scene systems: missing PixelCamera.");
            if (GameObject.FindGameObjectWithTag("Player") == null)
                Debug.LogWarning("Scene systems: missing Player (tag).");
            if (GameObject.Find("World") == null)
                Debug.LogWarning("Scene systems: missing World folder.");
        }

        public static void EnsureInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                    return;
            }

            var list = new EditorBuildSettingsScene[scenes.Length + 1];
            for (var i = 0; i < scenes.Length; i++)
                list[i] = scenes[i];
            list[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = list;
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

        public static GameObject CreateUiRootInScene()
        {
            var go = new GameObject("GameUI");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiRoot.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<UiRoot>();
            go.AddComponent<InteractPrompt>();
            go.AddComponent<ExplorationHud>();
            go.AddComponent<PauseMenu>();
            go.AddComponent<DialogueBox>();
            go.AddComponent<ItemToast>();

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(go.transform, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            var hud = new GameObject("Hud", typeof(RectTransform));
            hud.transform.SetParent(go.transform, false);
            UiRoot.StretchFill(hud.GetComponent<RectTransform>());

            var menus = new GameObject("Menus", typeof(RectTransform));
            menus.transform.SetParent(go.transform, false);
            UiRoot.StretchFill(menus.GetComponent<RectTransform>());

            go.GetComponent<UiRoot>().ApplyBestPracticeLayout();
            return go;
        }
    }

    [InitializeOnLoad]
    static class HideUiRootInSceneViewOnLoad
    {
        static HideUiRootInSceneViewOnLoad()
        {
            EditorSceneManager.sceneOpened += (_, _) => GameSceneBootstrap.HideUiFromSceneView();
            EditorApplication.delayCall += GameSceneBootstrap.HideUiFromSceneView;
        }
    }
}
