using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using KVH.KylePixelator;

namespace KVH.KylePixelator.Editor
{
    // kylepixelator project setup (settings, cam prefab, outline feature). not a package installer.
    public static class SetupMenu
    {
        const string DefaultHostRendererPath = "Assets/Settings/PC_Renderer.asset";

        [MenuItem("KVH/KylePixelator/Create Settings Asset")]
        public static void CreateSettings()
        {
            var settingsPath = RootSettingsPath();
            EnsureFolder(ParentFolder(settingsPath));
            var existing = AssetDatabase.LoadAssetAtPath<Settings>(settingsPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var asset = ScriptableObject.CreateInstance<Settings>();
            asset.enableLowResOutput = true;
            asset.enablePixelSnap = true;
            asset.enableSnapCompensate = false;
            asset.enableSharpUpscale = true;
            asset.enableOutlines = true;
            asset.enableCelLighting = true;
            AssetDatabase.CreateAsset(asset, settingsPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }

        [MenuItem("KVH/KylePixelator/Create Camera Prefab")]
        public static void CreateCameraPrefab()
        {
            var prefabPath = RootPrefabPath();
            EnsureFolder(ParentFolder(prefabPath));
            var settingsPath = RootSettingsPath();
            var settings = AssetDatabase.LoadAssetAtPath<Settings>(settingsPath);
            if (settings == null)
            {
                CreateSettings();
                settings = AssetDatabase.LoadAssetAtPath<Settings>(settingsPath);
            }

            var root = new GameObject("PixelCamera");
            try
            {
                // aim/follow is game-owned (GameCamera), don't ship lab follow here
                var rig = root.AddComponent<CameraRig>();

                var camGo = new GameObject("Main Camera");
                camGo.transform.SetParent(root.transform, false);
                camGo.transform.localPosition = new Vector3(0f, 0f, -30f);
                var cam = camGo.AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.orthographic = true;
                var lowRes = camGo.AddComponent<LowResOutput>();

                var soRig = new SerializedObject(rig);
                soRig.FindProperty("settings").objectReferenceValue = settings;
                soRig.FindProperty("pixelCamera").objectReferenceValue = cam;
                soRig.ApplyModifiedPropertiesWithoutUndo();

                var soLow = new SerializedObject(lowRes);
                soLow.FindProperty("settings").objectReferenceValue = settings;
                soLow.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"KylePixelator: wrote prefab {prefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        [MenuItem("KVH/KylePixelator/Create Camera In Scene")]
        public static void CreateCameraInScene()
        {
            var prefabPath = RootPrefabPath();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                CreateCameraPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "PixelCamera";
            Undo.RegisterCreatedObjectUndo(instance, "Create KylePixelator Camera");
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
        }

        [MenuItem("KVH/KylePixelator/Add Outline Feature To Selected Renderer")]
        public static void AddOutlineFeatureToSelectedRenderer()
        {
            var renderer = Selection.activeObject as ScriptableRendererData;
            if (renderer == null)
                renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(DefaultHostRendererPath);

            if (renderer == null)
            {
                Debug.LogError(
                    "KylePixelator: select a Universal Renderer asset (or keep Assets/Settings/PC_Renderer.asset).");
                return;
            }

            AddOutlineFeature(renderer);
        }

        // old menu name, same thing
        [MenuItem("KVH/KylePixelator/Add Outline Feature To PC_Renderer")]
        public static void AddOutlineFeatureToPcRenderer()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(DefaultHostRendererPath);
            if (renderer == null)
            {
                Debug.LogError($"KylePixelator: missing renderer at {DefaultHostRendererPath}");
                return;
            }

            AddOutlineFeature(renderer);
            AddHighlightOutlineFeature(renderer);
        }

        [MenuItem("KVH/KylePixelator/Add Highlight Outline Feature To PC_Renderer")]
        public static void AddHighlightOutlineFeatureToPcRenderer()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(DefaultHostRendererPath);
            if (renderer == null)
            {
                Debug.LogError($"KylePixelator: missing renderer at {DefaultHostRendererPath}");
                return;
            }

            AddHighlightOutlineFeature(renderer);
        }

        static void AddOutlineFeature(ScriptableRendererData renderer)
        {
            var so = new SerializedObject(renderer);
            var features = so.FindProperty("m_RendererFeatures");
            for (var i = 0; i < features.arraySize; i++)
            {
                var feat = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (feat is OutlineFeature)
                {
                    Debug.Log($"KylePixelator: outline feature already on {renderer.name}.");
                    Selection.activeObject = renderer;
                    return;
                }
            }

            var feature = ScriptableObject.CreateInstance<OutlineFeature>();
            feature.name = "OutlineFeature";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            features.InsertArrayElementAtIndex(features.arraySize);
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            Debug.Log($"KylePixelator: added OutlineFeature to {AssetDatabase.GetAssetPath(renderer)}.");
            Selection.activeObject = renderer;
        }

        static void AddHighlightOutlineFeature(ScriptableRendererData renderer)
        {
            var so = new SerializedObject(renderer);
            var features = so.FindProperty("m_RendererFeatures");
            for (var i = 0; i < features.arraySize; i++)
            {
                var feat = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (feat is HighlightOutlineFeature)
                {
                    Debug.Log($"KylePixelator: highlight outline feature already on {renderer.name}.");
                    Selection.activeObject = renderer;
                    return;
                }
            }

            var feature = ScriptableObject.CreateInstance<HighlightOutlineFeature>();
            feature.name = "HighlightOutlineFeature";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            features.InsertArrayElementAtIndex(features.arraySize);
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            EnsureAlwaysIncludedShader("Hidden/KVH/KylePixelatorHighlightOutline");
            Debug.Log($"KylePixelator: added HighlightOutlineFeature to {AssetDatabase.GetAssetPath(renderer)}.");
            Selection.activeObject = renderer;
        }

        static void EnsureAlwaysIncludedShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"KylePixelator: Shader.Find('{shaderName}') failed; skip Always Included.");
                return;
            }

            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (objs == null || objs.Length == 0)
                return;

            var gs = new SerializedObject(objs[0]);
            var included = gs.FindProperty("m_AlwaysIncludedShaders");
            if (included == null)
                return;

            for (var i = 0; i < included.arraySize; i++)
            {
                if (included.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return;
            }

            included.InsertArrayElementAtIndex(included.arraySize);
            included.GetArrayElementAtIndex(included.arraySize - 1).objectReferenceValue = shader;
            gs.ApplyModifiedPropertiesWithoutUndo();
        }

        // Assets/KylePixelator, derived from this editor script's path
        public static string RootFolder()
        {
            var guids = AssetDatabase.FindAssets("SetupMenu t:MonoScript");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("SetupMenu.cs"))
                    continue;
                var editorIdx = path.Replace('\\', '/').LastIndexOf("/Editor/");
                if (editorIdx >= 0)
                    return path.Substring(0, editorIdx);
            }

            return "Assets/KylePixelator";
        }

        static string RootSettingsPath() => RootFolder() + "/Settings/DefaultPixelSettings.asset";
        static string RootPrefabPath() => RootFolder() + "/Prefabs/PixelCamera.prefab";

        static string ParentFolder(string assetPath)
        {
            var i = assetPath.Replace('\\', '/').LastIndexOf('/');
            return i > 0 ? assetPath.Substring(0, i) : assetPath;
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
