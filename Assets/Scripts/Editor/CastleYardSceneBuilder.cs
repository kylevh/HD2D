using KVH.Game.Dialogue;
using KVH.Game.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KVH.Game.Editor
{
    // CastleYard: brick Terrain + JRPG-style inner bailey (walls, keep, wings, props).
    public static class CastleYardSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/CastleYard.unity";
        const string TerrainDataPath = "Assets/Art/Terrain/CastleYard_TerrainData.asset";
        const string TexFolder = "Assets/Art/Textures/Castle";
        const string LayerFolder = "Assets/Art/Terrain";
        const string YardRootName = "Yard";

        const float TerrainSize = 200f;
        const float TerrainHeight = 20f;
        const int HeightmapRes = 257;
        const int AlphamapRes = 512;

        // courtyard half-extent (full ~40u). gate half-width.
        const float Court = 20f;
        const float Gate = 3.5f;
        const float WallH = 3.2f;
        const float WallT = 1.2f;

        struct LayerSpec
        {
            public string TexPath;
            public string LayerPath;
            public string Name;
            public float TileSize;
        }

        // TileSize = world meters per texture repeat. tweak on the .terrainlayer assets.
        // floor language: brick base, flag path corridors, worn aprons (shared grey mist palette)
        static readonly LayerSpec[] Layers =
        {
            new LayerSpec
            {
                TexPath = TexFolder + "/StoneBrick_Albedo.png",
                LayerPath = LayerFolder + "/Castle_StoneBrick.terrainlayer",
                Name = "Castle_StoneBrick",
                TileSize = 6f,
            },
            new LayerSpec
            {
                TexPath = TexFolder + "/PathFlag_Albedo.png",
                LayerPath = LayerFolder + "/Castle_PathFlag.terrainlayer",
                Name = "Castle_PathFlag",
                TileSize = 8f,
            },
            new LayerSpec
            {
                TexPath = TexFolder + "/WornApron_Albedo.png",
                LayerPath = LayerFolder + "/Castle_WornApron.terrainlayer",
                Name = "Castle_WornApron",
                TileSize = 6f,
            },
        };

        [MenuItem("KVH/Debug/Scenes/Build CastleYard")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh();
            ConfigureCastleTextureImports();
            EnsureTerrainLayers();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameSceneBootstrap.EnsurePlayableSystems(
                GameSceneBootstrap.LightingPreset.OutdoorCycle,
                spawnPlayerIfMissing: true);

            ConfigureOutdoorNoon();

            GameSceneBootstrap.EnsureHierarchyScaffold();
            var geometry = FindGeometry();
            if (geometry == null)
                return;

            var proto = geometry.Find("PrototypeFloor");
            if (proto != null)
                Object.DestroyImmediate(proto.gameObject);

            BuildTerrain(geometry);
            PopulateYard();

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                player.transform.position = new Vector3(0f, 0.15f, -6f);

            GameSceneBootstrap.WireCameraToPlayer();
            GameSceneBootstrap.EnsureHierarchyScaffold();
            GameSceneBootstrap.HideUiFromSceneView();

            EditorSceneManager.SaveScene(scene, ScenePath);
            GameSceneBootstrap.EnsureInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"CastleYard: built {ScenePath} (terrain + yard content).");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }

        [MenuItem("KVH/Debug/Scenes/Populate CastleYard")]
        public static void MenuPopulateYard()
        {
            PopulateYard();
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                player.transform.position = new Vector3(0f, 0.15f, -6f);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("CastleYard: yard content populated.");
        }

        [MenuItem("KVH/Debug/Scenes/CastleYard Floor Language")]
        public static void MenuApplyFloorLanguage() => ApplyFloorLanguageToOpenScene();

        [MenuItem("KVH/Debug/Scenes/CastleYard Brick Only")]
        public static void MenuApplyBrickOnly() => ApplyFloorLanguageToOpenScene();

        public static void PopulateYard()
        {
            GameSceneBootstrap.EnsureHierarchyScaffold();
            var geometry = FindGeometry();
            if (geometry == null)
                return;

            var world = GameObject.Find("World");
            var interactables = world != null ? world.transform.Find("Interactables") : null;
            if (interactables == null)
            {
                Debug.LogError("CastleYard: missing World/Interactables.");
                return;
            }

            var gameplay = GameObject.Find("Gameplay");
            var npcs = gameplay != null ? gameplay.transform.Find("Npcs") : null;
            if (npcs == null)
            {
                Debug.LogError("CastleYard: missing Gameplay/Npcs.");
                return;
            }

            // wipe previous authored yard (keep CastleTerrain)
            var oldYard = geometry.Find(YardRootName);
            if (oldYard != null)
                Object.DestroyImmediate(oldYard.gameObject);

            for (var i = interactables.childCount - 1; i >= 0; i--)
            {
                var c = interactables.GetChild(i);
                if (c.name.StartsWith("Yard_"))
                    Object.DestroyImmediate(c.gameObject);
            }

            for (var i = npcs.childCount - 1; i >= 0; i--)
            {
                var c = npcs.GetChild(i);
                if (c.name.StartsWith("Yard_"))
                    Object.DestroyImmediate(c.gameObject);
            }

            var stone = LoadMat("Assets/Art/Materials/Stone.mat");
            var wood = LoadMat("Assets/Art/Materials/Wood.mat");
            var clay = LoadMat("Assets/Art/Materials/Clay.mat");
            var sand = LoadMat("Assets/Art/Materials/Sand.mat");
            var ink = LoadMat("Assets/Art/Materials/Ink.mat");
            var teal = LoadMat("Assets/Art/Materials/Teal.mat");

            var yard = new GameObject(YardRootName).transform;
            yard.SetParent(geometry, false);

            BuildCourtyardWalls(yard, stone, wood);
            BuildCourtyardCurbs(yard, stone);
            BuildCornerTowers(yard, stone, clay);
            BuildKeep(yard, stone, wood, clay);
            BuildEastWing(yard, stone, wood, clay);
            BuildWestGarden(yard, stone, wood, sand);
            BuildSouthApproach(yard, stone, clay);
            BuildOuterBailey(yard, stone, wood, clay);
            BuildFountain(yard, stone, teal, ink);
            BuildProps(yard, interactables, wood, sand, stone);
            SpawnYardPeople(npcs, interactables);
            PlaceVolumeLight(interactables);

            ConfigureOutdoorNoon();
        }

        static void BuildCourtyardWalls(Transform yard, Material stone, Material wood)
        {
            var walls = Folder(yard, "CourtyardWalls");
            var y = WallH * 0.5f;
            var span = Court * 2f;
            var side = (span - Gate * 2f) * 0.5f;

            // north / south: two segments with center gate
            MakeBlock(walls, "Wall_N_L", new Vector3(-(Gate + side * 0.5f), y, Court), new Vector3(side, WallH, WallT), stone);
            MakeBlock(walls, "Wall_N_R", new Vector3(Gate + side * 0.5f, y, Court), new Vector3(side, WallH, WallT), stone);
            MakeBlock(walls, "Wall_S_L", new Vector3(-(Gate + side * 0.5f), y, -Court), new Vector3(side, WallH, WallT), stone);
            MakeBlock(walls, "Wall_S_R", new Vector3(Gate + side * 0.5f, y, -Court), new Vector3(side, WallH, WallT), stone);

            // east / west
            MakeBlock(walls, "Wall_E_N", new Vector3(Court, y, Gate + side * 0.5f), new Vector3(WallT, WallH, side), stone);
            MakeBlock(walls, "Wall_E_S", new Vector3(Court, y, -(Gate + side * 0.5f)), new Vector3(WallT, WallH, side), stone);
            MakeBlock(walls, "Wall_W_N", new Vector3(-Court, y, Gate + side * 0.5f), new Vector3(WallT, WallH, side), stone);
            MakeBlock(walls, "Wall_W_S", new Vector3(-Court, y, -(Gate + side * 0.5f)), new Vector3(WallT, WallH, side), stone);

            // low gate arches (visual only)
            MakeBlock(walls, "Gate_N", new Vector3(0f, 2.6f, Court), new Vector3(Gate * 2f + 0.4f, 0.5f, WallT * 0.8f), wood);
            MakeBlock(walls, "Gate_S", new Vector3(0f, 2.6f, -Court), new Vector3(Gate * 2f + 0.4f, 0.5f, WallT * 0.8f), wood);
            MakeBlock(walls, "Gate_E", new Vector3(Court, 2.6f, 0f), new Vector3(WallT * 0.8f, 0.5f, Gate * 2f + 0.4f), wood);
            MakeBlock(walls, "Gate_W", new Vector3(-Court, 2.6f, 0f), new Vector3(WallT * 0.8f, 0.5f, Gate * 2f + 0.4f), wood);
        }

        // low stone skirt inside courtyard walls — grounds walls on the brick floor
        static void BuildCourtyardCurbs(Transform yard, Material stone)
        {
            var curbs = Folder(yard, "CourtyardCurbs");
            var y = 0.18f;
            var h = 0.36f;
            var t = 0.55f;
            var inset = Court - WallT * 0.5f - 0.35f;
            var span = Court * 2f;
            var side = (span - Gate * 2f) * 0.5f;

            MakeBlock(curbs, "Curb_N_L", new Vector3(-(Gate + side * 0.5f), y, inset), new Vector3(side, h, t), stone);
            MakeBlock(curbs, "Curb_N_R", new Vector3(Gate + side * 0.5f, y, inset), new Vector3(side, h, t), stone);
            MakeBlock(curbs, "Curb_S_L", new Vector3(-(Gate + side * 0.5f), y, -inset), new Vector3(side, h, t), stone);
            MakeBlock(curbs, "Curb_S_R", new Vector3(Gate + side * 0.5f, y, -inset), new Vector3(side, h, t), stone);
            MakeBlock(curbs, "Curb_E_N", new Vector3(inset, y, Gate + side * 0.5f), new Vector3(t, h, side), stone);
            MakeBlock(curbs, "Curb_E_S", new Vector3(inset, y, -(Gate + side * 0.5f)), new Vector3(t, h, side), stone);
            MakeBlock(curbs, "Curb_W_N", new Vector3(-inset, y, Gate + side * 0.5f), new Vector3(t, h, side), stone);
            MakeBlock(curbs, "Curb_W_S", new Vector3(-inset, y, -(Gate + side * 0.5f)), new Vector3(t, h, side), stone);
        }

        static void BuildCornerTowers(Transform yard, Material stone, Material clay)
        {
            var towers = Folder(yard, "Towers");
            var h = 8f;
            var s = 4f;
            var inset = Court - 0.2f;
            MakeBlock(towers, "Tower_NE", new Vector3(inset, h * 0.5f, inset), new Vector3(s, h, s), stone);
            MakeBlock(towers, "Tower_NW", new Vector3(-inset, h * 0.5f, inset), new Vector3(s, h, s), stone);
            MakeBlock(towers, "Tower_SE", new Vector3(inset, h * 0.5f, -inset), new Vector3(s, h, s), clay);
            MakeBlock(towers, "Tower_SW", new Vector3(-inset, h * 0.5f, -inset), new Vector3(s, h, s), clay);
            // caps
            MakeBlock(towers, "Cap_NE", new Vector3(inset, h + 0.4f, inset), new Vector3(s + 0.6f, 0.8f, s + 0.6f), clay);
            MakeBlock(towers, "Cap_NW", new Vector3(-inset, h + 0.4f, inset), new Vector3(s + 0.6f, 0.8f, s + 0.6f), clay);
            MakeBlock(towers, "Cap_SE", new Vector3(inset, h + 0.4f, -inset), new Vector3(s + 0.6f, 0.8f, s + 0.6f), stone);
            MakeBlock(towers, "Cap_SW", new Vector3(-inset, h + 0.4f, -inset), new Vector3(s + 0.6f, 0.8f, s + 0.6f), stone);
        }

        static void BuildKeep(Transform yard, Material stone, Material wood, Material clay)
        {
            // main keep north of courtyard (FF-style: keep behind the bailey)
            var keep = Folder(yard, "Keep");
            var z = Court + 12f;
            MakeBlock(keep, "KeepBody", new Vector3(0f, 5f, z), new Vector3(28f, 10f, 14f), stone);
            MakeBlock(keep, "KeepRoof", new Vector3(0f, 10.6f, z), new Vector3(30f, 1.2f, 16f), clay);
            MakeBlock(keep, "KeepDoorframe", new Vector3(0f, 2.2f, z - 7.2f), new Vector3(5f, 4.4f, 1.2f), wood);
            // open doorway gap: thin walls beside door
            MakeBlock(keep, "KeepDoorL", new Vector3(-3.2f, 2f, z - 7.4f), new Vector3(1.2f, 4f, 1.4f), stone);
            MakeBlock(keep, "KeepDoorR", new Vector3(3.2f, 2f, z - 7.4f), new Vector3(1.2f, 4f, 1.4f), stone);
            MakeBlock(keep, "KeepSteps", new Vector3(0f, 0.35f, Court + 3.5f), new Vector3(8f, 0.7f, 4f), stone);
            MakeBlock(keep, "KeepBannerL", new Vector3(-8f, 6f, z - 7.1f), new Vector3(1.2f, 3f, 0.2f), wood);
            MakeBlock(keep, "KeepBannerR", new Vector3(8f, 6f, z - 7.1f), new Vector3(1.2f, 3f, 0.2f), wood);
        }

        static void BuildEastWing(Transform yard, Material stone, Material wood, Material clay)
        {
            var wing = Folder(yard, "EastWing");
            var x = Court + 14f;
            MakeBlock(wing, "Barracks", new Vector3(x, 2.5f, 4f), new Vector3(16f, 5f, 18f), clay);
            MakeBlock(wing, "BarracksRoof", new Vector3(x, 5.4f, 4f), new Vector3(17f, 0.8f, 19f), wood);
            MakeBlock(wing, "BarracksDoor", new Vector3(Court + 5.5f, 1.6f, 0f), new Vector3(1.2f, 3.2f, 3.5f), wood);
            // covered walk from east gate toward barracks
            MakeBlock(wing, "WalkRoof", new Vector3(Court + 5f, 3.2f, 0f), new Vector3(8f, 0.35f, 5f), wood);
            MakeBlock(wing, "WalkPost_N", new Vector3(Court + 3f, 1.5f, 2f), new Vector3(0.4f, 3f, 0.4f), stone);
            MakeBlock(wing, "WalkPost_S", new Vector3(Court + 3f, 1.5f, -2f), new Vector3(0.4f, 3f, 0.4f), stone);
            // storage annex south-east
            MakeBlock(wing, "Storehouse", new Vector3(x + 2f, 2f, -16f), new Vector3(12f, 4f, 10f), stone);
        }

        static void BuildWestGarden(Transform yard, Material stone, Material wood, Material sand)
        {
            var west = Folder(yard, "WestGarden");
            var x = -(Court + 12f);
            // low hedge proxies (cylinders as bushes)
            for (var i = 0; i < 5; i++)
            {
                var z = -10f + i * 5f;
                MakeCylinder(west, $"Hedge_{i}", new Vector3(x + 2f, 0.7f, z), new Vector3(2.2f, 0.7f, 2.2f), sand);
            }

            MakeBlock(west, "GardenWall_N", new Vector3(x, 1.2f, 14f), new Vector3(18f, 2.4f, 0.6f), stone);
            MakeBlock(west, "GardenWall_W", new Vector3(x - 8f, 1.2f, 0f), new Vector3(0.6f, 2.4f, 28f), stone);
            MakeBlock(west, "Bench", new Vector3(x + 4f, 0.4f, 6f), new Vector3(2.4f, 0.35f, 0.7f), wood);
            MakeBlock(west, "Planter_A", new Vector3(x + 6f, 0.35f, -4f), new Vector3(1.4f, 0.7f, 1.4f), stone);
            MakeBlock(west, "Planter_B", new Vector3(x + 6f, 0.35f, -8f), new Vector3(1.4f, 0.7f, 1.4f), stone);
            // stairs to west lookout deck
            MakeBlock(west, "LookoutStep1", new Vector3(-(Court + 2f), 0.35f, 10f), new Vector3(2.5f, 0.7f, 1.4f), stone);
            MakeBlock(west, "LookoutStep2", new Vector3(-(Court + 4f), 0.9f, 11.5f), new Vector3(2.5f, 0.7f, 1.4f), stone);
            MakeBlock(west, "LookoutDeck", new Vector3(-(Court + 7f), 1.6f, 13f), new Vector3(6f, 0.35f, 5f), wood);
            MakeBlock(west, "LookoutRail", new Vector3(-(Court + 7f), 2.2f, 15.2f), new Vector3(6f, 0.3f, 0.25f), wood);
        }

        static void BuildSouthApproach(Transform yard, Material stone, Material clay)
        {
            var south = Folder(yard, "SouthApproach");
            MakeBlock(south, "Pillar_L", new Vector3(-5f, 2.5f, -(Court + 6f)), new Vector3(1.4f, 5f, 1.4f), clay);
            MakeBlock(south, "Pillar_R", new Vector3(5f, 2.5f, -(Court + 6f)), new Vector3(1.4f, 5f, 1.4f), clay);
            MakeBlock(south, "ApproachWall_L", new Vector3(-10f, 1.5f, -(Court + 14f)), new Vector3(0.8f, 3f, 16f), stone);
            MakeBlock(south, "ApproachWall_R", new Vector3(10f, 1.5f, -(Court + 14f)), new Vector3(0.8f, 3f, 16f), stone);
            MakeBlock(south, "OuterGateArch", new Vector3(0f, 3.5f, -(Court + 22f)), new Vector3(10f, 1f, 1.2f), stone);
            MakeBlock(south, "OuterGateL", new Vector3(-6f, 2f, -(Court + 22f)), new Vector3(2f, 4f, 1.5f), stone);
            MakeBlock(south, "OuterGateR", new Vector3(6f, 2f, -(Court + 22f)), new Vector3(2f, 4f, 1.5f), stone);
        }

        static void BuildOuterBailey(Transform yard, Material stone, Material wood, Material clay)
        {
            // outer ring so you can walk beyond the courtyard into alleys / ruins
            var outer = Folder(yard, "OuterBailey");
            var r = 48f;
            MakeBlock(outer, "Outer_N_L", new Vector3(-18f, 2f, r), new Vector3(28f, 4f, 1.4f), stone);
            MakeBlock(outer, "Outer_N_R", new Vector3(22f, 2f, r), new Vector3(20f, 4f, 1.4f), stone);
            MakeBlock(outer, "Outer_E", new Vector3(r, 2f, 0f), new Vector3(1.4f, 4f, 60f), stone);
            MakeBlock(outer, "Outer_W", new Vector3(-r, 2f, 8f), new Vector3(1.4f, 4f, 50f), stone);
            MakeBlock(outer, "Outer_S_L", new Vector3(-16f, 2f, -r), new Vector3(24f, 4f, 1.4f), stone);
            MakeBlock(outer, "Outer_S_R", new Vector3(18f, 2f, -r), new Vector3(20f, 4f, 1.4f), wood);

            // ruin stubs + alley props
            MakeBlock(outer, "Ruin_A", new Vector3(32f, 1.5f, -28f), new Vector3(6f, 3f, 4f), clay);
            MakeBlock(outer, "Ruin_B", new Vector3(-34f, 1.2f, -20f), new Vector3(5f, 2.4f, 8f), stone);
            MakeBlock(outer, "RuinSlab", new Vector3(28f, 0.2f, 28f), new Vector3(5f, 0.4f, 3f), sandMat());
            MakeBlock(outer, "AlleyWall", new Vector3(30f, 2f, 16f), new Vector3(0.8f, 4f, 14f), stone);
        }

        static Material sandMat() => LoadMat("Assets/Art/Materials/Sand.mat");

        static void BuildFountain(Transform yard, Material stone, Material teal, Material ink)
        {
            var f = Folder(yard, "Fountain");
            MakeCylinder(f, "Basin", new Vector3(0f, 0.35f, 4f), new Vector3(5f, 0.35f, 5f), stone);
            MakeCylinder(f, "WaterProxy", new Vector3(0f, 0.55f, 4f), new Vector3(4f, 0.12f, 4f), teal);
            MakeCylinder(f, "Plinth", new Vector3(0f, 1.1f, 4f), new Vector3(1.2f, 0.9f, 1.2f), stone);
            MakeBlock(f, "Statue", new Vector3(0f, 2.4f, 4f), new Vector3(0.9f, 1.8f, 0.9f), ink);
        }

        static void BuildProps(Transform yard, Transform interactables, Material wood, Material sand, Material stone)
        {
            var props = Folder(yard, "Props");
            MakeBlock(props, "Crate_1", new Vector3(8f, 0.4f, -8f), new Vector3(0.8f, 0.8f, 0.8f), sand);
            MakeBlock(props, "Crate_2", new Vector3(8.9f, 0.4f, -7.8f), new Vector3(0.8f, 0.8f, 0.8f), sand);
            MakeBlock(props, "Crate_3", new Vector3(8.4f, 1.15f, -8f), new Vector3(0.8f, 0.8f, 0.8f), wood);
            MakeBlock(props, "Barrel", new Vector3(11f, 0.55f, -6f), new Vector3(0.9f, 1.1f, 0.9f), wood);
            MakeBlock(props, "CrateStack_E", new Vector3(Court - 3f, 0.5f, 8f), new Vector3(1.2f, 1f, 1f), wood);

            // interactables under World/Interactables
            var sign = MakeBlock(interactables, "Yard_NoticeBoard", new Vector3(-6f, 1.2f, Court - 2f), new Vector3(1.6f, 2.2f, 0.25f), wood);
            MarkProp(sign, 2f);
            AddInspect(sign, "Notice board",
                "Castle Yard — visitors welcome.",
                "Keep to the marked paths. Watch the west garden stairs.");

            var chest = new GameObject("Yard_SupplyChest");
            chest.transform.SetParent(interactables, false);
            chest.transform.localPosition = new Vector3(Court - 4f, 0f, -10f);
            MakeBlock(chest.transform, "Body", new Vector3(0f, 0.28f, 0f), new Vector3(0.9f, 0.56f, 0.62f), wood);
            MakeBlock(chest.transform, "Lid", new Vector3(0f, 0.64f, 0f), new Vector3(0.96f, 0.16f, 0.68f), sand);
            MarkProp(chest, 1.8f);
            AddInspect(chest, "Supply chest", "Padlocked. Someone in the barracks might have the key.");

            var well = MakeCylinder(interactables, "Yard_OldWell", new Vector3(-(Court - 5f), 0.5f, -6f), new Vector3(2f, 0.5f, 2f), stone);
            MarkProp(well, 2f);
            AddInspect(well, "Old well", "Cool air rises from the shaft. Best not lean too far.");
        }

        static void SpawnYardPeople(Transform npcs, Transform interactables)
        {
            var guard = SpawnNpc(npcs, "Yard_Guard", new Vector3(4f, 0f, -Court + 2f),
                "Yard Guard",
                "Halt—ah, a traveler. The courtyard is open today.",
                "The keep's sealed for renovations. Try the east wing if you need supplies.");

            var gardener = SpawnNpc(npcs, "Yard_Gardener", new Vector3(-(Court + 4f), 0f, 4f),
                "Gardener",
                "Mind the hedges. They're more stubborn than they look.",
                "The lookout deck's got the best view of the outer walls.");

            // plaque near fountain
            var plaque = MakeBlock(interactables, "Yard_FountainPlaque", new Vector3(2.5f, 0.6f, 6.5f), new Vector3(0.8f, 0.9f, 0.2f),
                LoadMat("Assets/Art/Materials/Stone.mat"));
            MarkProp(plaque, 1.6f);
            AddInspect(plaque, "Fountain plaque", "\"For those who kept the yard.\" — year worn smooth.");

            _ = guard;
            _ = gardener;
        }

        static void PlaceVolumeLight(Transform interactables)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Lighting/VolumeLight.prefab");
            if (prefab == null)
                return;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "Yard_VolumeLight";
            inst.transform.SetParent(interactables, false);
            inst.transform.position = new Vector3(-8f, 0f, 8f);
        }

        static GameObject SpawnNpc(Transform parent, string name, Vector3 pos, string speaker, params string[] lines)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/NpcProxy.prefab");
            GameObject root;
            if (prefab != null)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                root.name = name;
                root.transform.SetParent(parent, false);
                root.transform.position = pos;
            }
            else
            {
                root = new GameObject(name);
                root.transform.SetParent(parent, false);
                root.transform.position = pos;
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, 1f, 0f);
                body.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
                Object.DestroyImmediate(body.GetComponent<Collider>());
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
                so.ApplyModifiedPropertiesWithoutUndo();
                marker.CollectRenderers();
            }

            var speakerComp = root.GetComponent<DialogueSpeaker>();
            if (speakerComp == null)
                speakerComp = root.AddComponent<DialogueSpeaker>();
            var ds = new SerializedObject(speakerComp);
            ds.FindProperty("speakerName").stringValue = speaker;
            var linesProp = ds.FindProperty("lines");
            linesProp.arraySize = lines.Length;
            for (var i = 0; i < lines.Length; i++)
                linesProp.GetArrayElementAtIndex(i).stringValue = lines[i];
            ds.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static void MarkProp(GameObject go, float range)
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

        static void AddInspect(GameObject go, string title, params string[] lines)
        {
            var inspect = go.GetComponent<InspectText>();
            if (inspect == null)
                inspect = go.AddComponent<InspectText>();
            var so = new SerializedObject(inspect);
            so.FindProperty("title").stringValue = title;
            var linesProp = so.FindProperty("lines");
            linesProp.arraySize = lines.Length;
            for (var i = 0; i < lines.Length; i++)
                linesProp.GetArrayElementAtIndex(i).stringValue = lines[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Folder(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

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

        static Material LoadMat(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

        static Transform FindGeometry()
        {
            var world = GameObject.Find("World");
            var geometry = world != null ? world.transform.Find("Geometry") : null;
            if (geometry == null)
                Debug.LogError("CastleYard: missing World/Geometry.");
            return geometry;
        }

        static void ConfigureOutdoorNoon()
        {
            var director = Object.FindAnyObjectByType<KVH.Game.Lighting.LightingDirector>();
            if (director == null)
                return;
            var so = new SerializedObject(director);
            so.FindProperty("mode").enumValueIndex = (int)KVH.Game.Lighting.LightingDirector.Mode.Cycle;
            so.FindProperty("useNight").boolValue = false;
            so.FindProperty("timeOfDayHours").floatValue = 12f;
            so.FindProperty("autoAdvance").boolValue = false;
            so.FindProperty("rotateKeyWithTime").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            director.Apply();
        }

        public static void ConfigureCastleTextureImports()
        {
            foreach (var spec in Layers)
            {
                var importer = AssetImporter.GetAtPath(spec.TexPath) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"CastleYard: missing texture {spec.TexPath}");
                    continue;
                }

                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.anisoLevel = 1;

                var platform = importer.GetDefaultPlatformTextureSettings();
                platform.format = TextureImporterFormat.RGBA32;
                platform.textureCompression = TextureImporterCompression.Uncompressed;
                platform.maxTextureSize = 1024;
                importer.SetPlatformTextureSettings(platform);
                importer.SaveAndReimport();
            }
        }

        public static void EnsureTerrainLayers()
        {
            EnsureFolders();
            foreach (var spec in Layers)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.TexPath);
                if (tex == null)
                {
                    Debug.LogError($"CastleYard: texture not found {spec.TexPath}");
                    continue;
                }

                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(spec.LayerPath);
                if (layer == null)
                {
                    layer = new TerrainLayer();
                    AssetDatabase.CreateAsset(layer, spec.LayerPath);
                }

                layer.diffuseTexture = tex;
                // seed defaults / fix tiny auto sizes; keep intentional manual tweaks (>= 3)
                if (layer.tileSize.x < 3f || layer.tileSize.y < 3f)
                    layer.tileSize = new Vector2(spec.TileSize, spec.TileSize);
                layer.tileOffset = Vector2.zero;
                layer.name = spec.Name;
                EditorUtility.SetDirty(layer);
            }

            AssetDatabase.SaveAssets();
        }

        public static void ApplyFloorLanguageToOpenScene()
        {
            EnsureFolders();
            AssetDatabase.Refresh();
            ConfigureCastleTextureImports();
            EnsureTerrainLayers();

            var terrain = Object.FindAnyObjectByType<UnityEngine.Terrain>();
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("CastleYard: no Terrain in open scene.");
                return;
            }

            var terrainLayers = new TerrainLayer[Layers.Length];
            for (var i = 0; i < Layers.Length; i++)
            {
                terrainLayers[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Layers[i].LayerPath);
                if (terrainLayers[i] == null)
                {
                    Debug.LogError($"CastleYard: missing layer {Layers[i].LayerPath}");
                    return;
                }
            }

            var data = terrain.terrainData;
            data.terrainLayers = terrainLayers;
            StampFloorLanguage(data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("CastleYard: floor language applied (brick / path / worn).");
        }

        // hard paint: brick base, flagstone corridors through gates, worn aprons at fountain + keep
        static void StampFloorLanguage(TerrainData data)
        {
            if (data.terrainLayers == null || data.terrainLayers.Length < 3)
                return;

            var aW = data.alphamapWidth;
            var aH = data.alphamapHeight;
            var maps = new float[aH, aW, data.terrainLayers.Length];

            const float pathHalf = Gate + 0.8f;
            const float southExtent = Court + 26f;
            const float eastExtent = Court + 18f;
            var fountain = new Vector2(0f, 4f);
            const float fountainR = 5.5f;

            for (var y = 0; y < aH; y++)
            {
                for (var x = 0; x < aW; x++)
                {
                    var u = x / (float)(aW - 1);
                    var v = y / (float)(aH - 1);
                    var wx = (u - 0.5f) * TerrainSize;
                    var wz = (v - 0.5f) * TerrainSize;

                    var layer = 0; // brick

                    var nsPath = Mathf.Abs(wx) <= pathHalf && wz >= -southExtent && wz <= Court + 10f;
                    var ewPath = Mathf.Abs(wz) <= pathHalf && Mathf.Abs(wx) <= eastExtent;
                    if (nsPath || ewPath)
                        layer = 1; // path

                    var keepApron = wz >= Court - 1.5f && wz <= Court + 9f && Mathf.Abs(wx) <= 11f;
                    var fountainRing = Vector2.Distance(new Vector2(wx, wz), fountain) <= fountainR;
                    var eastThreshold = wx >= Court - 1f && wx <= Court + 8f && Mathf.Abs(wz) <= 5f;
                    if (keepApron || fountainRing || eastThreshold)
                        layer = 2; // worn

                    maps[y, x, 0] = layer == 0 ? 1f : 0f;
                    maps[y, x, 1] = layer == 1 ? 1f : 0f;
                    maps[y, x, 2] = layer == 2 ? 1f : 0f;
                }
            }

            data.SetAlphamaps(0, 0, maps);
            EditorUtility.SetDirty(data);
        }

        public static void ApplyBrickOnlyToOpenScene() => ApplyFloorLanguageToOpenScene();

        static void BuildTerrain(Transform geometry)
        {
            EnsureTerrainLayers();

            var terrainLayers = new TerrainLayer[Layers.Length];
            for (var i = 0; i < Layers.Length; i++)
            {
                terrainLayers[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Layers[i].LayerPath);
                if (terrainLayers[i] == null)
                {
                    Debug.LogError($"CastleYard: missing layer {Layers[i].LayerPath}");
                    return;
                }
            }

            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            data.heightmapResolution = HeightmapRes;
            data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
            data.alphamapResolution = AlphamapRes;
            data.terrainLayers = terrainLayers;

            var hRes = data.heightmapResolution;
            var heights = new float[hRes, hRes];
            data.SetHeights(0, 0, heights);

            var aW = data.alphamapWidth;
            var aH = data.alphamapHeight;
            var maps = new float[aH, aW, terrainLayers.Length];
            for (var y = 0; y < aH; y++)
            for (var x = 0; x < aW; x++)
                maps[y, x, 0] = 1f;
            data.SetAlphamaps(0, 0, maps);
            StampFloorLanguage(data);
            EditorUtility.SetDirty(data);

            var old = geometry.Find("CastleTerrain");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            var go = UnityEngine.Terrain.CreateTerrainGameObject(data);
            go.name = "CastleTerrain";
            go.transform.SetParent(geometry, false);
            go.transform.position = new Vector3(-TerrainSize * 0.5f, 0f, -TerrainSize * 0.5f);

            var terrain = go.GetComponent<UnityEngine.Terrain>();
            if (terrain != null)
            {
                terrain.groupingID = 0;
                terrain.allowAutoConnect = true;
                terrain.drawInstanced = true;
                terrain.basemapDistance = 250f;
                terrain.heightmapPixelError = 5f;
            }

            EditorUtility.SetDirty(go);
        }

        static void EnsureFolders()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder("Assets/Art/Textures");
            EnsureFolder("Assets/Art/Textures/Castle");
            EnsureFolder("Assets/Art/Terrain");
            EnsureFolder("Assets/Scenes");
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
