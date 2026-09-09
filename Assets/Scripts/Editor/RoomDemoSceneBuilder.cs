using KVH.Game.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KVH.Game.Editor
{
    // Interior template: shared playable systems + iso cutaway room.
    public static class RoomDemoSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/RoomDemo.unity";
        const string VolumeLightPrefab = VolumeLightMenu.PrefabPath;
        const string SettingsPath = "Assets/KylePixelator/Settings/DefaultPixelSettings.asset";

        [MenuItem("KVH/Debug/Scenes/Build RoomDemo")]
        public static void Build()
        {
            var settings = AssetDatabase.LoadAssetAtPath<Object>(SettingsPath);
            var day = AssetDatabase.LoadAssetAtPath<LightingProfile>(GameSceneBootstrap.DayProfilePath);
            var night = AssetDatabase.LoadAssetAtPath<LightingProfile>(GameSceneBootstrap.NightProfilePath);
            var stone = LoadMat("Assets/Art/Materials/Stone.mat");
            var wood = LoadMat("Assets/Art/Materials/Wood.mat");
            var clay = LoadMat("Assets/Art/Materials/Clay.mat");
            var teal = LoadMat("Assets/Art/Materials/Teal.mat");
            var sand = LoadMat("Assets/Art/Materials/Sand.mat");

            if (settings == null || day == null || night == null || stone == null || wood == null || clay == null)
            {
                Debug.LogError("RoomDemo: missing settings, lighting profiles, or pastel mats.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameSceneBootstrap.EnsurePlayableSystems(
                GameSceneBootstrap.LightingPreset.IndoorNight,
                spawnPlayerIfMissing: true);

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                player.transform.position = Vector3.zero;

            // floor + N/E walls. S/W omitted for the iso cutaway
            GameSceneBootstrap.EnsureHierarchyScaffold();
            var world = GameObject.Find("World");
            var geometry = world != null ? world.transform.Find("Geometry") : null;
            if (geometry == null)
                geometry = world != null ? world.transform : null;
            if (geometry == null)
            {
                Debug.LogError("RoomDemo: missing World/Geometry.");
                return;
            }

            var existingRoom = geometry.Find("Room");
            if (existingRoom != null)
                Object.DestroyImmediate(existingRoom.gameObject);

            var room = new GameObject("Room");
            room.transform.SetParent(geometry, false);
            const float roomSize = 8f;
            const float wallH = 3f;
            const float wallT = 0.25f;

            MakeBlock(room.transform, "Floor",
                new Vector3(0f, -0.05f, 0f),
                new Vector3(roomSize, 0.1f, roomSize),
                stone);

            MakeBlock(room.transform, "Wall_N",
                new Vector3(0f, wallH * 0.5f, roomSize * 0.5f - wallT * 0.5f),
                new Vector3(roomSize, wallH, wallT),
                wood);

            MakeBlock(room.transform, "Wall_E",
                new Vector3(roomSize * 0.5f - wallT * 0.5f, wallH * 0.5f, 0f),
                new Vector3(wallT, wallH, roomSize),
                clay);

            var props = new GameObject("Props");
            props.transform.SetParent(room.transform, false);
            MakeBlock(props.transform, "Table",
                new Vector3(-1.5f, 0.4f, 1.5f),
                new Vector3(1.2f, 0.8f, 0.8f),
                teal);
            MakeBlock(props.transform, "Crate",
                new Vector3(2f, 0.35f, -1.2f),
                new Vector3(0.7f, 0.7f, 0.7f),
                sand);

            var interactables = world != null ? world.transform.Find("Interactables") : null;
            var volPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VolumeLightPrefab);
            if (volPrefab != null)
            {
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(volPrefab);
                lamp.name = "VolumeLight";
                lamp.transform.position = new Vector3(-2.5f, 1.2f, 2.5f);
                if (interactables != null)
                    lamp.transform.SetParent(interactables, true);
            }

            GameSceneBootstrap.WireCameraToPlayer();
            GameSceneBootstrap.EnsureHierarchyScaffold();

            EditorSceneManager.SaveScene(scene, ScenePath);
            GameSceneBootstrap.EnsureInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"RoomDemo: built {ScenePath} (PixelLab remains build index 0).");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        static Material LoadMat(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

        static void MakeBlock(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && mat != null)
                renderer.sharedMaterial = mat;
        }
    }
}
