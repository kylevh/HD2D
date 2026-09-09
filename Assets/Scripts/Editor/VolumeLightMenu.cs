using KVH.Game.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KVH.Game.Editor
{
    public static class VolumeLightMenu
    {
        public const string PrefabPath = "Assets/Prefabs/Lighting/VolumeLight.prefab";

        [MenuItem("KVH/Lighting/Create Volume Light", false, 10)]
        [MenuItem("GameObject/Light/Volume Light", false, 40)]
        public static void Create()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject go;
            if (prefab != null)
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            else
            {
                go = new GameObject("VolumeLight", typeof(VolumeLight));
                Debug.LogWarning($"VolumeLight prefab missing at {PrefabPath}; created a bare component.");
            }

            go.name = "VolumeLight";
            var view = SceneView.lastActiveSceneView;
            go.transform.position = view != null ? view.pivot : Vector3.zero;

            var sel = Selection.activeGameObject;
            if (sel != null && sel.GetComponent<LightingDirector>() != null)
                go.transform.SetParent(sel.transform, true);

            Undo.RegisterCreatedObjectUndo(go, "Create Volume Light");
            Selection.activeGameObject = go;
            HideCoversFromSceneView();
        }

        // Cover sphere is huge and eats Scene view; Game view still renders it.
        public static void HideCoversFromSceneView()
        {
            var vis = SceneVisibilityManager.instance;
            var lights = Object.FindObjectsByType<VolumeLight>(FindObjectsInactive.Include);
            for (var i = 0; i < lights.Length; i++)
            {
                var volume = lights[i].transform.Find("Volume");
                if (volume != null && !vis.IsHidden(volume.gameObject, false))
                    vis.Hide(volume.gameObject, false);
            }
        }
    }

    [InitializeOnLoad]
    static class HideVolumeLightCoverInSceneViewOnLoad
    {
        static HideVolumeLightCoverInSceneViewOnLoad()
        {
            EditorSceneManager.sceneOpened += (_, _) => VolumeLightMenu.HideCoversFromSceneView();
            EditorApplication.delayCall += VolumeLightMenu.HideCoversFromSceneView;
        }
    }
}
