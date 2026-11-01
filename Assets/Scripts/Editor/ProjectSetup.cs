using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ProjectSetup
{
    private const string PipelineAssetPath = "Assets/Settings/UniversalRP.asset";
    private const string RendererAssetPath = "Assets/Settings/UniversalRenderer3D.asset";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string MaterialFolder = "Assets/Materials";
    private const string SceneFolder = "Assets/Scenes";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

    private static readonly string[] GeneratedSceneObjectNames =
    {
        "Ground", "Core", "Player", "GameManager", "EnemySpawner", "TowerPlacer", "PlacementGrid", "EventSystem", "UICanvas", "EnvironmentDecor", "Arena", "PostFX", "Directional Light", "Squad", "LootSpawner", "BaseWalls", "Village", "PostFXNight", "NightLook"
    };

    private const string PostFxProfilePath = "Assets/Settings/GameplayPostFX.asset";

    // The starting fort: a square ring of wall blocks this many grid cells out from the core,
    // leaving room inside for towers.
    private const int BaseWallRingCells = 6;

    // Shared by the arena fog and the camera clear colour so the world fades into the background.
    private static readonly Color HorizonColor = new Color(0.56f, 0.74f, 0.66f);
    private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.09f, 0.8f);
    private static readonly Color ScrapGold = new Color(1f, 0.82f, 0.3f);

    private static readonly Color ZombieBlood = new Color(0.5f, 0.07f, 0.06f);
    private static readonly Color ZombieBone = new Color(0.86f, 0.82f, 0.7f);

    // Slow, heavy-footed shuffle shared by the plain walkers.
    private static readonly CharacterFactory.Gait ShambleGait = new CharacterFactory.Gait
    {
        stateName = "Walk",
        period = 0.9f,
        legSwingDeg = 20f,
        armSwingDeg = 20f,
        hipsBob = 0.09f,
        torsoLeanExtra = 4f,
    };

    private static readonly Color EnemyRingColor = new Color(0.95f, 0.22f, 0.18f);
    private static readonly Color PlayerRingColor = new Color(0.25f, 0.75f, 1f);

    private static readonly Color WallAccent = new Color(0.72f, 0.68f, 0.62f);
    private static readonly Color GunAccent = new Color(1f, 0.76f, 0.22f);
    private static readonly Color CannonAccent = new Color(1f, 0.48f, 0.18f);
    private static readonly Color SniperAccent = new Color(0.45f, 0.88f, 0.35f);
    private static readonly Color TeslaAccent = new Color(0.3f, 0.8f, 1f);
    private static readonly Color MissileAccent = new Color(0.95f, 0.26f, 0.32f);

    [MenuItem("Tools/Iron Hold/0) Reset Prototype Scene && Prefabs")]
    public static void ResetPrototype()
    {
        foreach (string name in GeneratedSceneObjectNames)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            IsoCameraFollow follow = mainCamera.GetComponent<IsoCameraFollow>();
            if (follow != null) Object.DestroyImmediate(follow);
        }

        if (AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.DeleteAsset(PrefabFolder);
        if (AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.DeleteAsset(MaterialFolder);
        if (AssetDatabase.IsValidFolder(LowPolyAnimKit.AnimationFolder)) AssetDatabase.DeleteAsset(LowPolyAnimKit.AnimationFolder);

        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Prototype reset. Run '2) Build Prototype Scene' again to rebuild with the latest settings.");
    }

    [MenuItem("Tools/Iron Hold/1) Setup 3D Render Pipeline")]
    public static void SetupRenderPipeline()
    {
        var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
        if (pipelineAsset == null)
        {
            Debug.LogError($"Could not find pipeline asset at {PipelineAssetPath}");
            return;
        }

        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
        }

        var so = new SerializedObject(pipelineAsset);
        SerializedProperty listProp = so.FindProperty("m_RendererDataList");
        SerializedProperty defaultIndexProp = so.FindProperty("m_DefaultRendererIndex");

        if (listProp == null || defaultIndexProp == null)
        {
            Debug.LogError("Could not find renderer list properties on the pipeline asset. URP internals may have changed.");
            return;
        }

        // Rebuild the list from scratch (rather than appending) so a previously
        // corrupted/duplicated list can't leave a stale or out-of-range entry behind.
        listProp.ClearArray();
        listProp.InsertArrayElementAtIndex(0);
        listProp.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
        defaultIndexProp.intValue = 0;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(pipelineAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Render pipeline reset to a single, clean 3D Universal Renderer.");
    }

    [MenuItem("Tools/Iron Hold/2) Build Prototype Scene")]
    public static void BuildPrototypeScene()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(LowPolyAnimKit.AnimationFolder);

        Projectile bulletProjectile = GetOrCreateProjectilePrefab("ProjectileBullet", 0.35f, new Color(1f, 0.85f, 0.2f));
        Projectile cannonBallProjectile = GetOrCreateProjectilePrefab("ProjectileCannonBall", 0.55f, new Color(0.15f, 0.1f, 0.08f));
        Projectile teslaBoltProjectile = GetOrCreateProjectilePrefab("ProjectileTeslaBolt", 0.25f, new Color(0.4f, 0.85f, 1f));
        Projectile missileProjectile = GetOrCreateProjectilePrefab("ProjectileMissile", 0.4f, new Color(0.95f, 0.95f, 0.95f));
        Projectile sniperProjectile = GetOrCreateProjectilePrefab("ProjectileSniperRound", 0.22f, new Color(0.85f, 1f, 0.5f));

        // Bullets are fast, thin tracers (hits are swept along their path, so speed is safe).
        ConfigureProjectileBehavior(bulletProjectile, speed: 75f, lifetime: 0.5f, fxColor: new Color(1f, 0.8f, 0.35f), glow: 4f, bullet: true);
        StyleAsTracer(bulletProjectile, length: 0.75f, width: 0.05f);
        ConfigureProjectileBehavior(cannonBallProjectile, speed: 22f, lifetime: 2f, fxColor: new Color(1f, 0.55f, 0.15f), glow: 2f, splashRadius: 2.5f);
        ConfigureProjectileBehavior(sniperProjectile, speed: 120f, lifetime: 0.35f, fxColor: new Color(0.85f, 1f, 0.6f), glow: 5f, pierceCount: 3, bullet: true);
        StyleAsTracer(sniperProjectile, length: 1.3f, width: 0.06f);
        ConfigureProjectileBehavior(teslaBoltProjectile, speed: 45f, lifetime: 1.2f, fxColor: new Color(0.4f, 0.85f, 1f), glow: 3f, chainCount: 4, chainRange: 4.5f);
        ConfigureProjectileBehavior(missileProjectile, speed: 12f, lifetime: 5f, fxColor: new Color(1f, 0.45f, 0.1f), glow: 2.5f, homing: true, splashRadius: 2f);
        Projectile arrowProjectile = GetOrCreateArrowPrefab();
        ConfigureProjectileBehavior(arrowProjectile, speed: 38f, lifetime: 1.2f, fxColor: new Color(1f, 0.95f, 0.75f), glow: 1f, pierceCount: 2);
        Enemy enemyPrefab = GetOrCreateEnemyPrefab();
        Enemy runnerPrefab = GetOrCreateRunnerPrefab();
        Enemy brutePrefab = GetOrCreateBrutePrefab();
        Enemy spitterPrefab = GetOrCreateSpitterPrefab();
        Enemy bomberPrefab = GetOrCreateBomberPrefab();
        Enemy engineerPrefab = GetOrCreateEngineerPrefab();
        Wall wallPrefab = GetOrCreateWallPrefab();
        GameObject bossPrefab = GetOrCreateBossPrefab();

        Tower gunTower = GetOrCreateTowerPrefab("TowerGun", TowerStyle.Gun, new Color(0.56f, 0.56f, 0.6f), GunAccent, 1f, 8f, 3f, 8f, bulletProjectile);
        Tower cannonTower = GetOrCreateTowerPrefab("TowerCannon", TowerStyle.Cannon, new Color(0.56f, 0.54f, 0.52f), CannonAccent, 1.3f, 6f, 1f, 30f, cannonBallProjectile);
        Tower sniperTower = GetOrCreateTowerPrefab("TowerSniper", TowerStyle.Sniper, new Color(0.52f, 0.56f, 0.52f), SniperAccent, 0.85f, 14f, 0.6f, 45f, sniperProjectile);
        Tower teslaTower = GetOrCreateTowerPrefab("TowerTesla", TowerStyle.Tesla, new Color(0.52f, 0.55f, 0.6f), TeslaAccent, 1f, 6.5f, 2f, 8f, teslaBoltProjectile);
        Tower missileTower = GetOrCreateTowerPrefab("TowerMissile", TowerStyle.Missile, new Color(0.56f, 0.53f, 0.5f), MissileAccent, 1.15f, 11f, 0.5f, 55f, missileProjectile);

        ConfigureTower(gunTower, TowerKind.Gun, SfxId.LightShot, 1f);
        ConfigureTower(cannonTower, TowerKind.Cannon, SfxId.CannonShot, 1f);
        ConfigureTower(sniperTower, TowerKind.Sniper, SfxId.SniperShot, 1f);
        ConfigureTower(teslaTower, TowerKind.Tesla, SfxId.Zap, 0.6f);
        ConfigureTower(missileTower, TowerKind.Missile, SfxId.MissileLaunch, 1f);

        GameObject playerPrefabGo = GetOrCreatePlayerPrefab(bulletProjectile, arrowProjectile);
        DogCompanion dogPrefab = GetOrCreateDogPrefab();
        Soldier soldierPrefab = GetOrCreateSoldierPrefab(bulletProjectile);
        Gate gatePrefab = GetOrCreateGatePrefab();
        Watchtower watchtowerPrefab = GetOrCreateWatchtowerPrefab(soldierPrefab);
        LootCache lootCachePrefab = GetOrCreateLootCachePrefab();
        GameObject corePrefabGo = GetOrCreateCorePrefab();

        BuildArena();
        ConfigureLightingAndAtmosphere();
        BuildNightLook();
        // Leftover visual test of the wall prefab; walls aren't a gameplay feature yet.
        DestroyIfExists("WallDemo");
        // The static enemy gallery / boss demo predate real wave spawning. Every type now
        // spawns in waves, and stationary Enemy instances would just be free scrap.
        DestroyIfExists("EnemyGallery");
        DestroyIfExists("BossDemo");

        GameObject core = GameObject.Find("Core");
        if (core == null)
        {
            core = (GameObject)PrefabUtility.InstantiatePrefab(corePrefabGo);
            core.name = "Core";
            core.transform.position = new Vector3(0f, 1f, 0f);
        }
        Health coreHealth = core.GetComponent<Health>();

        GameObject gridGo = GameObject.Find("PlacementGrid");
        if (gridGo == null)
        {
            gridGo = new GameObject("PlacementGrid");
            gridGo.AddComponent<PlacementGrid>();
        }
        PlacementGrid grid = gridGo.GetComponent<PlacementGrid>();
        grid.OccupyWorldArea(core.transform.position, new Vector3(2.5f, 0f, 2.5f));

        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefabGo);
            player.name = "Player";
            // The rig's local origin already sits at ground level (unlike the old capsule,
            // which needed +1 to sit its center above the ground).
            player.transform.position = new Vector3(0f, 0f, -6f);
        }

        GameObject gameManagerGo = GameObject.Find("GameManager");
        if (gameManagerGo == null) gameManagerGo = new GameObject("GameManager");
        GameManager gameManager = LowPolyKit.GetOrAddComponent<GameManager>(gameManagerGo);
        SetPrivateField(gameManager, "coreHealth", coreHealth);
        GameFx gameFx = LowPolyKit.GetOrAddComponent<GameFx>(gameManagerGo);
        SetPrivateField(gameFx, "particleMaterial", GetOrCreateParticleMaterial());

        GameObject spawnerGo = FindOrCreateEmpty("EnemySpawner");
        // Enemies now dig up out of the treeline; the old fixed portal spawn points are gone.
        LowPolyKit.ClearChildren(spawnerGo);

        // Applied on every build (not only when the spawner is first created) so roster and
        // balance changes here reach scenes that were built before.
        EnemySpawner enemySpawner = LowPolyKit.GetOrAddComponent<EnemySpawner>(spawnerGo);
        enemySpawner.Configure(new[]
        {
            new EnemySpawnEntry { prefab = enemyPrefab, threatCost = 1, firstWave = 1, weight = 4f },
            new EnemySpawnEntry { prefab = runnerPrefab, threatCost = 1, firstWave = 2, weight = 2f },
            new EnemySpawnEntry { prefab = spitterPrefab, threatCost = 2, firstWave = 3, weight = 1.5f },
            new EnemySpawnEntry { prefab = bomberPrefab, threatCost = 2, firstWave = 4, weight = 1.2f },
            new EnemySpawnEntry { prefab = brutePrefab, threatCost = 4, firstWave = 6, weight = 1f },
            new EnemySpawnEntry { prefab = engineerPrefab, threatCost = 2, firstWave = 7, weight = 1f },
        }, bossPrefab != null ? bossPrefab.GetComponent<Enemy>() : null, core.transform);
        EditorUtility.SetDirty(enemySpawner);

        GameObject towerPlacerGo = GameObject.Find("TowerPlacer");
        if (towerPlacerGo == null)
        {
            towerPlacerGo = new GameObject("TowerPlacer");
            towerPlacerGo.AddComponent<TowerPlacer>();
        }
        TowerPlacer towerPlacer = towerPlacerGo.GetComponent<TowerPlacer>();
        SetPrivateField(towerPlacer, "grid", grid);
        SetPrivateField(towerPlacer, "player", player.transform);
        SetTowerDefinitions(towerPlacer, new (string name, int cost, GameObject prefab, Color accentColor)[]
        {
            ("Gun", 50, gunTower.gameObject, GunAccent),
            ("Cannon", 120, cannonTower.gameObject, CannonAccent),
            ("Sniper", 150, sniperTower.gameObject, SniperAccent),
            ("Tesla", 90, teslaTower.gameObject, TeslaAccent),
            ("Missile", 180, missileTower.gameObject, MissileAccent),
            ("Wall", 25, wallPrefab.gameObject, WallAccent),
        });

        SquadCommander squad = BuildSquad(soldierPrefab, player.transform);
        // Outposts were tried and dropped; clear any left in scenes built with them.
        DestroyIfExists("Outposts");
        BuildBaseWalls(wallPrefab, gatePrefab, watchtowerPrefab, grid, player.transform);
        BuildVillage(GetOrCreateHousePrefabs(), grid);

        GameObject lootGo = FindOrCreateEmpty("LootSpawner");
        LowPolyKit.GetOrAddComponent<LootCacheSpawner>(lootGo).Configure(lootCachePrefab, player.transform, enemySpawner);

        BuildGameplayUI(enemySpawner, coreHealth, towerPlacer, player.GetComponent<PlayerShooter>(), dogPrefab, squad);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            IsoCameraFollow follow = mainCamera.GetComponent<IsoCameraFollow>();
            if (follow == null) follow = mainCamera.gameObject.AddComponent<IsoCameraFollow>();
            follow.SetTarget(player.transform);
        }
        else
        {
            Debug.LogWarning("No Main Camera found in the scene - camera follow was not set up.");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Prototype scene built. Press Play to test. Remember to save the scene (Ctrl+S).");
    }

    private const string TrailerScenePath = "Assets/Scenes/Trailer.unity";

    // Where the wall block breaks in the trailer (south wall, just east of the gate); the soldier
    // ambush happens just inside it. Must match TrailerDirector's staging.
    private static readonly Vector3 TrailerBreachPoint = new Vector3(4.5f, 0f, -16.5f);

    // A self-playing ~30s vertical trailer for TikTok/Reels/Shorts, staged with the real game
    // prefabs. Press Play in this scene (Game view at 1080x1920) and record it with Unity Recorder.
    [MenuItem("Tools/Iron Hold/4) Build Trailer Scene")]
    public static void BuildTrailerScene()
    {
        T Load<T>(string name) where T : Object => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab")?.GetComponent<T>();

        string[] required = { "Core", "Player", "Wall", "Gate", "Watchtower", "Enemy", "Boss", "House", "HouseAlt" };
        foreach (string name in required)
        {
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID($"{PrefabFolder}/{name}.prefab")))
            {
                EditorUtility.DisplayDialog("Iron Hold", "Prefabs are missing. Run 'Tools > Iron Hold > 2) Build Prototype Scene' first, then build the trailer.", "OK");
                return;
            }
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder(SceneFolder);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Loaded only after the new scene exists: opening a scene unloads unused assets, which
        // would leave references fetched earlier pointing at destroyed prefabs.
        Transform corePrefab = Load<Transform>("Core");
        Transform playerPrefab = Load<Transform>("Player");
        Wall wallPrefab = Load<Wall>("Wall");
        Gate gatePrefab = Load<Gate>("Gate");
        Watchtower watchtowerPrefab = Load<Watchtower>("Watchtower");
        Enemy walker = Load<Enemy>("Enemy");
        Enemy boss = Load<Enemy>("Boss");

        GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        Camera cam = cameraGo.GetComponent<Camera>();
        cam.fieldOfView = 50f;

        BuildArena();
        ConfigureLightingAndAtmosphere();
        UnityEngine.Rendering.Volume postFx = ApplyTrailerLook(cam);

        GameObject fxGo = new GameObject("GameFx");
        SetPrivateField(fxGo.AddComponent<GameFx>(), "particleMaterial", GetOrCreateParticleMaterial());

        PlacementGrid grid = new GameObject("PlacementGrid").AddComponent<PlacementGrid>();

        var core = (GameObject)PrefabUtility.InstantiatePrefab(corePrefab.gameObject);
        core.name = "Core";
        core.transform.position = new Vector3(0f, 1f, 0f);

        // The director drives the hero; player input stays off.
        var hero = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab.gameObject);
        hero.name = "Player";
        hero.transform.SetPositionAndRotation(new Vector3(0f, 0f, -2f), Quaternion.Euler(0f, 180f, 0f));
        hero.GetComponent<PlayerController>().enabled = false;

        BuildBaseWalls(wallPrefab, gatePrefab, watchtowerPrefab, grid, hero.transform);
        BuildVillage(new[] { Load<Wall>("House"), Load<Wall>("HouseAlt") }, grid);

        // The block that breaks on cue in the wall shot: south wall, just east of the gate.
        Wall breakable = null;
        float bestDistance = float.MaxValue;
        foreach (Wall wall in Object.FindObjectsByType<Wall>())
        {
            if (wall.GetComponent<Gate>() != null || wall.GetComponent<Watchtower>() != null) continue;
            float distance = Vector3.Distance(wall.transform.position, TrailerBreachPoint);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                breakable = wall;
            }
        }

        // A tower line behind the south wall, where the horde attacks.
        (string prefab, Vector3 position)[] towers =
        {
            ("TowerCannon", new Vector3(-10.5f, 0f, -13.5f)),
            ("TowerTesla", new Vector3(-7.5f, 0f, -13.5f)),
            ("TowerGun", new Vector3(-4.5f, 0f, -13.5f)),
            ("TowerSniper", new Vector3(-7.5f, 0f, -10.5f)),
            ("TowerMissile", new Vector3(-13.5f, 0f, -10.5f)),
        };
        Transform towerParent = new GameObject("Towers").transform;
        foreach ((string prefab, Vector3 position) in towers)
        {
            Tower tower = Load<Tower>(prefab);
            if (tower == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(tower.gameObject);
            go.transform.SetParent(towerParent, false);
            go.transform.position = position;
        }

        TrailerUi ui = BuildTrailerUi();

        TrailerDirector director = new GameObject("TrailerDirector").AddComponent<TrailerDirector>();
        director.Configure(cam, core.transform, hero.transform, breakable, postFx,
            walker, Load<Enemy>("EnemyRunner"), Load<Enemy>("EnemyBrute"), Load<Enemy>("EnemySpitter"), Load<Enemy>("EnemyBomber"), boss,
            Load<Soldier>("Soldier"), Load<DogCompanion>("Dog"),
            ui.fade, ui.flash, ui.caption, ui.title, ui.subtitle, ui.callToAction, ui.comingSoon);

        // Opening frame of the first shot, so the Scene/Game view preview matches the start.
        cam.transform.SetPositionAndRotation(new Vector3(-6.5f, 1.1f, -32.5f), Quaternion.LookRotation(new Vector3(-3f, 0.9f, -28f) - new Vector3(-6.5f, 1.1f, -32.5f)));

        EditorSceneManager.SaveScene(scene, TrailerScenePath);
        Debug.Log($"Trailer scene saved to {TrailerScenePath}. Set the Game view to 1080x1920, then press Play (or record with Window > General > Recorder).");
    }

    private const string TrailerPostFxProfilePath = "Assets/Settings/TrailerPostFX.asset";

    // Dusk and a film grade for the trailer only: low orange back-light with long shadows toward
    // the camera, blue-violet ambient and fog (so torches, eyes and muzzle flashes pop), plus
    // teal/orange split toning, grain, slight fringing and depth of field the director pulls per shot.
    // No motion blur: the trailer hard-cuts between shots, and camera blur smears the frame on a cut.
    private static UnityEngine.Rendering.Volume ApplyTrailerLook(Camera cam)
    {
        Color dusk = new Color(0.3f, 0.27f, 0.36f);

        Light sun = Object.FindAnyObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            sun.transform.rotation = Quaternion.Euler(16f, 160f, 0f);
            sun.color = new Color(1f, 0.58f, 0.32f);
            sun.intensity = 1.25f;
            sun.shadowStrength = 0.75f;
        }

        RenderSettings.ambientSkyColor = new Color(0.32f, 0.36f, 0.55f);
        RenderSettings.ambientEquatorColor = new Color(0.3f, 0.27f, 0.32f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.1f, 0.09f);
        RenderSettings.fogColor = dusk;
        RenderSettings.fogStartDistance = 22f;
        RenderSettings.fogEndDistance = 70f;
        cam.backgroundColor = dusk;

        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(TrailerPostFxProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(profile, TrailerPostFxProfilePath);
        }

        Bloom bloom = GetOrAddVolumeOverride<Bloom>(profile);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(1.4f);
        bloom.scatter.Override(0.7f);

        GetOrAddVolumeOverride<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);

        ColorAdjustments color = GetOrAddVolumeOverride<ColorAdjustments>(profile);
        color.postExposure.Override(0.25f);
        color.contrast.Override(26f);
        color.saturation.Override(8f);

        SplitToning toning = GetOrAddVolumeOverride<SplitToning>(profile);
        toning.shadows.Override(new Color(0.2f, 0.45f, 0.55f));
        toning.highlights.Override(new Color(1f, 0.7f, 0.42f));
        toning.balance.Override(-15f);

        // Kept light: on a tall 9:16 frame a strong vignette closes in like a dark ring.
        Vignette vignette = GetOrAddVolumeOverride<Vignette>(profile);
        vignette.intensity.Override(0.18f);
        vignette.smoothness.Override(0.8f);

        FilmGrain grain = GetOrAddVolumeOverride<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium2);
        grain.intensity.Override(0.3f);
        grain.response.Override(0.8f);

        GetOrAddVolumeOverride<ChromaticAberration>(profile).intensity.Override(0.15f);

        DepthOfField dof = GetOrAddVolumeOverride<DepthOfField>(profile);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(10f);
        dof.focalLength.Override(50f);
        dof.aperture.Override(5.6f);

        foreach (UnityEngine.Rendering.VolumeComponent component in profile.components) EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        GameObject postFxGo = GameObject.Find("PostFX");
        var volume = postFxGo != null ? postFxGo.GetComponent<UnityEngine.Rendering.Volume>() : new GameObject("PostFX").AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        return volume;
    }

    private struct TrailerUi
    {
        public Image fade;
        public Image flash;
        public Text caption;
        public Text title;
        public Text subtitle;
        public Text callToAction;
        public Text comingSoon;
    }

    private static TrailerUi BuildTrailerUi()
    {
        Transform canvas = EnsureCanvas();
        Vector2 center = new Vector2(0.5f, 0.5f);
        var ui = new TrailerUi();

        RectTransform flash = CreateRect("Flash", canvas, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
        ui.flash = AddImage(flash, new Color(1f, 0.95f, 0.85f, 0f));

        // Widescreen bars: a film frame inside the 9:16 video.
        const float barHeight = 150f;
        AddImage(CreateRect("LetterboxTop", canvas, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, barHeight)), Color.black);
        AddImage(CreateRect("LetterboxBottom", canvas, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, barHeight)), Color.black);

        RectTransform fade = CreateRect("Fade", canvas, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
        ui.fade = AddImage(fade, Color.black);

        // Upper-middle of the frame: TikTok/Reels cover the bottom fifth and the right edge with UI.
        ui.caption = CreateTrailerText(canvas, "Caption", 112, Color.white, new Vector2(0f, 420f));
        ui.title = CreateTrailerText(canvas, "Title", 190, ScrapGold, new Vector2(0f, 260f));
        ui.title.text = "IRON HOLD";
        ui.subtitle = CreateTrailerText(canvas, "Subtitle", 88, Color.white, new Vector2(0f, 110f));
        ui.subtitle.text = "LAST STAND";
        // Ends on a question - it invites comments, which short-form apps reward.
        ui.callToAction = CreateTrailerText(canvas, "CallToAction", 70, new Color(1f, 0.92f, 0.5f), new Vector2(0f, -130f));
        ui.callToAction.text = "HOW LONG CAN\nYOU HOLD?";
        ui.comingSoon = CreateTrailerText(canvas, "ComingSoon", 40, new Color(0.85f, 0.85f, 0.88f), new Vector2(0f, -330f));
        ui.comingSoon.text = "COMING SOON TO MOBILE";
        return ui;
    }

    private static Text CreateTrailerText(Transform canvas, string name, int size, Color color, Vector2 position)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        Text text = CreateText(canvas, name, "", size, color, TextAnchor.MiddleCenter, center, position, new Vector2(1000f, size * 2.6f));
        text.fontStyle = FontStyle.Bold;
        text.lineSpacing = 0.9f;
        // Heavy outline + drop shadow so captions read over any frame.
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(5f, -5f);
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -10f);
        return text;
    }

    [MenuItem("Tools/Iron Hold/3) Build Main Menu Scene")]
    public static void BuildMainMenuScene()
    {
        T Load<T>(string name) where T : Object => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab")?.GetComponent<T>();

        // The menu backdrop is the real village, so it needs the game's prefabs.
        string[] required = { "Core", "Player", "Wall", "Gate", "Watchtower", "Enemy", "House", "HouseAlt" };
        foreach (string name in required)
        {
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID($"{PrefabFolder}/{name}.prefab")))
            {
                EditorUtility.DisplayDialog("Iron Hold", "Prefabs are missing. Run 'Tools > Iron Hold > 2) Build Prototype Scene' first, then build the main menu.", "OK");
                return;
            }
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder(SceneFolder);
        EnsureFolder(MaterialFolder);
        string previousScenePath = EditorSceneManager.GetActiveScene().path;
        Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---- 3D backdrop: the walled village at dusk, in the trailer's grade ----
        GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        Camera cam = cameraGo.GetComponent<Camera>();
        cam.fieldOfView = 50f;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 15f, -31f), Quaternion.LookRotation(new Vector3(0f, -1f, 0f) - new Vector3(0f, 15f, -31f)));

        BuildArena();
        ConfigureLightingAndAtmosphere();
        UnityEngine.Rendering.Volume postFx = ApplyTrailerLook(cam);

        GameObject fxGo = new GameObject("GameFx");
        SetPrivateField(fxGo.AddComponent<GameFx>(), "particleMaterial", GetOrCreateParticleMaterial());

        PlacementGrid grid = new GameObject("PlacementGrid").AddComponent<PlacementGrid>();

        var core = (GameObject)PrefabUtility.InstantiatePrefab(Load<Transform>("Core").gameObject);
        core.name = "Core";
        core.transform.position = new Vector3(0f, 1f, 0f);

        // The hero stands watch inside the south gate (far enough back that it stays shut).
        var hero = (GameObject)PrefabUtility.InstantiatePrefab(Load<Transform>("Player").gameObject);
        hero.name = "Player";
        hero.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.Euler(0f, 180f, 0f));
        hero.GetComponent<PlayerController>().enabled = false;
        hero.GetComponent<PlayerShooter>().enabled = false;

        BuildBaseWalls(Load<Wall>("Wall"), Load<Gate>("Gate"), Load<Watchtower>("Watchtower"), grid, hero.transform);
        BuildVillage(new[] { Load<Wall>("House"), Load<Wall>("HouseAlt") }, grid);

        (string prefab, Vector3 position)[] towers =
        {
            ("TowerCannon", new Vector3(-10.5f, 0f, -13.5f)),
            ("TowerTesla", new Vector3(-7.5f, 0f, -13.5f)),
            ("TowerGun", new Vector3(-4.5f, 0f, -13.5f)),
            ("TowerSniper", new Vector3(7.5f, 0f, 10.5f)),
            ("TowerMissile", new Vector3(-13.5f, 0f, 10.5f)),
            ("TowerGun", new Vector3(13.5f, 0f, -4.5f)),
        };
        Transform towerParent = new GameObject("Towers").transform;
        foreach ((string prefab, Vector3 position) in towers)
        {
            Tower tower = Load<Tower>(prefab);
            if (tower == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(tower.gameObject);
            go.transform.SetParent(towerParent, false);
            go.transform.position = position;
        }

        var zombies = new System.Collections.Generic.List<Enemy>();
        foreach (string name in new[] { "Enemy", "Enemy", "EnemyRunner", "EnemyBrute", "EnemySpitter" })
        {
            Enemy prefab = Load<Enemy>(name);
            if (prefab != null) zombies.Add(prefab);
        }
        new GameObject("MenuBackdrop").AddComponent<MenuBackdrop>().Configure(cam, postFx, zombies.ToArray());

        // ---- UI ----
        EnsureEventSystem();
        Transform canvas = EnsureCanvas();
        Vector2 center = new Vector2(0.5f, 0.5f);
        Sprite shade = GetOrCreateShadeSprite();

        // Dark gradients top and bottom so the logo and buttons read over the moving scene.
        RectTransform topShade = CreateRect("TopShade", canvas, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 900f));
        AddImage(topShade, new Color(0.03f, 0.03f, 0.06f, 0.8f), shade);
        // Flipped about its own centre so the gradient is darkest at the bottom edge.
        RectTransform bottomShade = CreateRect("BottomShade", canvas, Vector2.zero, new Vector2(1f, 0f), center, new Vector2(0f, 450f), new Vector2(0f, 900f));
        AddImage(bottomShade, new Color(0.03f, 0.03f, 0.06f, 0.85f), shade);
        bottomShade.localScale = new Vector3(1f, -1f, 1f);

        RectTransform safeArea = CreateRect("SafeArea", canvas, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
        safeArea.gameObject.AddComponent<SafeAreaFitter>();

        // Logo block, styled like the trailer's end card.
        RectTransform titleBlock = CreateRect("TitleBlock", safeArea, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), center, new Vector2(0f, -360f), new Vector2(1000f, 520f));
        CreateTrailerText(titleBlock, "Title", 170, ScrapGold, new Vector2(0f, 120f)).text = "IRON HOLD";
        CreateTrailerText(titleBlock, "Subtitle", 76, Color.white, new Vector2(0f, -25f)).text = "LAST STAND";
        CreateTrailerText(titleBlock, "Tagline", 44, new Color(1f, 0.92f, 0.5f), new Vector2(0f, -130f)).text = "HOW LONG CAN YOU HOLD?";

        Text bestWave = CreateText(safeArea, "BestWave", "", 46, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 560f), new Vector2(900f, 70f));
        bestWave.fontStyle = FontStyle.Bold;
        AddTextShadow(bestWave);

        Button play = CreateMenuButton(safeArea, "PlayButton", "PLAY", new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(640f, 190f), ScrapGold, 84);
        // Pivot in the middle so the idle pulse grows from the centre.
        RectTransform playRect = (RectTransform)play.transform;
        playRect.pivot = center;
        playRect.anchoredPosition = new Vector2(0f, 365f);

        Button settings = CreateMenuButton(safeArea, "SettingsButton", "SETTINGS", new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(380f, 100f), new Color(1f, 1f, 1f, 0.16f), 38);
        settings.GetComponentInChildren<Text>().color = Color.white;

        // Settings sheet: dim overlay blocks the menu behind it.
        RectTransform panel = CreateRect("SettingsPanel", canvas, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
        AddImage(panel, new Color(0f, 0f, 0f, 0.6f), raycast: true);
        RectTransform card = CreateRect("Card", panel, center, center, center, Vector2.zero, new Vector2(900f, 1160f));
        AddImage(card, new Color(0.08f, 0.09f, 0.12f, 0.96f), RoundedSprite, raycast: true);

        Text header = CreateText(card, "Header", "SETTINGS", 72, ScrapGold, TextAnchor.MiddleCenter, center, new Vector2(0f, 470f), new Vector2(800f, 100f));
        header.fontStyle = FontStyle.Bold;

        CreateText(card, "MusicLabel", "MUSIC", 44, Color.white, TextAnchor.MiddleLeft, center, new Vector2(0f, 340f), new Vector2(720f, 60f)).fontStyle = FontStyle.Bold;
        Slider music = CreateMenuSlider(card, "MusicSlider", new Vector2(0f, 260f), 720f);
        CreateText(card, "SoundLabel", "SOUND", 44, Color.white, TextAnchor.MiddleLeft, center, new Vector2(0f, 140f), new Vector2(720f, 60f)).fontStyle = FontStyle.Bold;
        Slider sound = CreateMenuSlider(card, "SoundSlider", new Vector2(0f, 60f), 720f);

        Button vibration = CreateMenuButton(card, "VibrationButton", "VIBRATION: ON", center, new Vector2(0f, -80f), new Vector2(720f, 120f), new Color(1f, 1f, 1f, 0.14f), 44);
        Text vibrationLabel = vibration.GetComponentInChildren<Text>();
        vibrationLabel.color = Color.white;

        Button night = CreateMenuButton(card, "NightButton", "NIGHT MODE: ON", center, new Vector2(0f, -220f), new Vector2(720f, 120f), new Color(1f, 1f, 1f, 0.14f), 44);
        Text nightLabel = night.GetComponentInChildren<Text>();
        nightLabel.color = Color.white;

        Button done = CreateMenuButton(card, "DoneButton", "DONE", center, new Vector2(0f, -440f), new Vector2(480f, 140f), ScrapGold, 56);

        // Full-screen fade for entering and leaving the menu (starts black, fades up).
        RectTransform fadeRect = CreateRect("Fade", canvas, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
        Image fade = AddImage(fadeRect, Color.black, raycast: true);

        MainMenuController controller = new GameObject("MainMenuController").AddComponent<MainMenuController>();
        controller.Configure(titleBlock, play, bestWave, settings, panel.gameObject, music, sound, vibration, vibrationLabel, night, nightLabel, done, fade);

        EditorSceneManager.SaveScene(menuScene, MainMenuScenePath);

        var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MainMenuScenePath, true),
        };
        if (System.IO.File.Exists(GameplayScenePath))
        {
            buildScenes.Add(new EditorBuildSettingsScene(GameplayScenePath, true));
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();

        if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != MainMenuScenePath)
        {
            EditorSceneManager.OpenScene(previousScenePath);
        }

        Debug.Log($"Main menu scene built at {MainMenuScenePath} and registered in Build Settings.");
    }

    private static Slider CreateMenuSlider(Transform parent, string name, Vector2 position, float width)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        RectTransform root = CreateRect(name, parent, center, center, center, position, new Vector2(width, 90f));
        Slider slider = root.gameObject.AddComponent<Slider>();

        RectTransform track = CreateRect("Track", root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), center, Vector2.zero, new Vector2(0f, 24f));
        AddImage(track, new Color(1f, 1f, 1f, 0.18f), RoundedSprite, raycast: true);

        RectTransform fillArea = CreateRect("FillArea", root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), center, Vector2.zero, new Vector2(0f, 24f));
        RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero, new Vector2(0f, 1f), center, Vector2.zero, Vector2.zero);
        AddImage(fill, ScrapGold, RoundedSprite);

        // Inset by half the knob so it stops flush with the track ends.
        RectTransform handleArea = CreateRect("HandleArea", root, Vector2.zero, Vector2.one, center, Vector2.zero, new Vector2(-80f, 0f));
        RectTransform handle = CreateRect("Handle", handleArea, Vector2.zero, new Vector2(0f, 1f), center, Vector2.zero, new Vector2(80f, 0f));
        Image knob = AddImage(handle, Color.white, CircleSprite, raycast: true);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = knob;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.transition = Selectable.Transition.None;
        return slider;
    }

    private const string ShadeSpritePath = "Assets/Materials/MenuShade.png";

    // Vertical gradient (opaque at the top, clear at the bottom) for the menu's legibility shades.
    private static Sprite GetOrCreateShadeSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(ShadeSpritePath);
        if (existing != null) return existing;

        var texture = new Texture2D(4, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < texture.height; y++)
        {
            float fromTop = 1f - y / (float)(texture.height - 1);
            float alpha = Mathf.Pow(1f - fromTop, 1.6f);
            for (int x = 0; x < texture.width; x++) texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        System.IO.File.WriteAllBytes(ShadeSpritePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(ShadeSpritePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(ShadeSpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(ShadeSpritePath);
    }

    private static Button CreateMenuButton(Transform parent, string name, string label, Vector2 anchor, Vector2 anchoredPosition, Vector2 sizeDelta, Color? background = null, int fontSize = 44)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = go.GetComponent<Image>();
        image.color = background ?? Color.white;
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 0.35f;

        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        Text labelText = CreateFillText(go.transform, "Label", fontSize, new Color(0.08f, 0.09f, 0.11f), TextAnchor.MiddleCenter);
        labelText.text = label;
        labelText.fontStyle = FontStyle.Bold;

        return button;
    }

    private static Projectile GetOrCreateProjectilePrefab(string prefabName, float scale, Color color)
    {
        string path = $"{PrefabFolder}/{prefabName}.prefab";
        Projectile existing = AssetDatabase.LoadAssetAtPath<Projectile>(path);
        if (existing != null) return existing;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = prefabName;
        go.transform.localScale = Vector3.one * scale;
        go.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(prefabName, color, unlit: true);

        SphereCollider collider = go.GetComponent<SphereCollider>();
        collider.isTrigger = true;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        go.AddComponent<Projectile>();

        GameObject prefab = SaveAndDestroy(go, path);
        return prefab.GetComponent<Projectile>();
    }

    private static Enemy GetOrCreateEnemyPrefab()
    {
        // "Enemy" is the Grunt - the basic enemy type. The prefab/asset path and the
        // Enemy.cs component name are kept as-is so EnemySpawner/TowerPlacer references
        // don't need to change; only the visuals/rig are new.
        string path = $"{PrefabFolder}/Enemy.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "Enemy";

        CapsuleCollider collider = LowPolyKit.GetOrAddComponent<CapsuleCollider>(go);
        collider.center = new Vector3(0f, 0.98f, 0f);
        collider.radius = 0.35f;
        collider.height = 1.96f;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(30f);
        Enemy enemy = LowPolyKit.GetOrAddComponent<Enemy>(go);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.46f, 0.4f, 0.32f),
            skin = new Color(0.5f, 0.6f, 0.42f),
            headwear = new Color(0.14f, 0.12f, 0.1f),
            accent = ZombieBlood,
            weaponBody = new Color(0.46f, 0.4f, 0.32f),
            weaponDark = new Color(0.4f, 0.48f, 0.34f),
        };

        CharacterFactory.BipedProportions p = CharacterFactory.Zombified(CharacterFactory.GruntProportions, new Vector3(-76f, 4f, 0f), new Vector3(-88f, -4f, 0f), 14f);
        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, p, colors, "Grunt", hasWeapon: false);
        AddZombieDetails(rig, p, "Grunt", colors.skin, new Color(0.24f, 0.26f, 0.34f), new Color(2.4f, 2f, 0.3f));
        ConfigureEnemyFeedback(enemy, colors.accent, 14, 0f, 0f);
        AddTeamRing(go.transform, 0.6f, EnemyRingColor);
        CharacterFactory.BuildAnimations(go, rig, p, "Grunt",
            ShambleGait, null, "Attack",
            45f, 14f, false, out AnimatorController controller);

        Animator animator = LowPolyKit.GetOrAddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        GameObject prefab = LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
        return prefab.GetComponent<Enemy>();
    }

    // Turns a biped into one of the undead: glowing eyes and a dark mouth, bare rotting forearms,
    // torn clothes showing skin and ribs, bloodstains and jeans. Removes the chest badge that
    // living characters wear.
    private static void AddZombieDetails(CharacterFactory.BipedRig rig, CharacterFactory.BipedProportions p, string prefix, Color skin, Color pants, Color eyeGlow)
    {
        Transform badge = rig.torso.Find("ChestAccent");
        if (badge != null) Object.DestroyImmediate(badge.gameObject);

        Material skinMat = GetOrCreateMaterial($"{prefix}Skin", skin, unlit: false);
        Material pantsMat = GetOrCreateMaterial($"{prefix}Pants", pants, unlit: false);
        Material eyeMat = GetOrCreateMaterial($"{prefix}EyeGlow", eyeGlow, unlit: true);
        Material mouthMat = GetOrCreateMaterial("ZombieMouth", new Color(0.1f, 0.02f, 0.02f), unlit: true);
        Material bloodMat = GetOrCreateMaterial("ZombieBloodStain", ZombieBlood, unlit: false);
        Material boneMat = GetOrCreateMaterial("ZombieBone", ZombieBone, unlit: false);

        Transform torso = rig.torso;
        Vector3 t = p.torsoSize;
        Vector3 h = p.headSize;
        float headCenterY = CharacterFactory.HeadCenterY(p);
        float headFront = h.z * 0.5f + 0.006f;
        float front = t.z * 0.5f + 0.006f;

        LowPolyKit.CreateBlock("EyeL", torso, new Vector3(-h.x * 0.22f, headCenterY + h.y * 0.08f, headFront), Vector3.zero, new Vector3(h.x * 0.17f, h.y * 0.13f, 0.02f), eyeMat);
        LowPolyKit.CreateBlock("EyeR", torso, new Vector3(h.x * 0.24f, headCenterY + h.y * 0.05f, headFront), Vector3.zero, new Vector3(h.x * 0.15f, h.y * 0.12f, 0.02f), eyeMat);
        LowPolyKit.CreateBlock("Mouth", torso, new Vector3(0.02f, headCenterY - h.y * 0.26f, headFront), new Vector3(0f, 0f, -6f), new Vector3(h.x * 0.48f, h.y * 0.12f, 0.02f), mouthMat);

        // Shirt ripped open across the belly, ribs showing through.
        LowPolyKit.CreateBlock("Tear", torso, new Vector3(t.x * 0.12f, -t.y * 0.12f, front), new Vector3(0f, 0f, 8f), new Vector3(t.x * 0.42f, t.y * 0.34f, 0.02f), skinMat);
        for (int i = 0; i < 3; i++)
        {
            LowPolyKit.CreateBlock("Rib", torso, new Vector3(t.x * 0.12f, -t.y * 0.02f - i * t.y * 0.09f, front + 0.008f), new Vector3(0f, 0f, 8f), new Vector3(t.x * 0.3f, 0.025f, 0.015f), boneMat);
        }
        LowPolyKit.CreateBlock("Blood", torso, new Vector3(-t.x * 0.22f, t.y * 0.18f, front), Vector3.zero, new Vector3(t.x * 0.26f, t.y * 0.2f, 0.02f), bloodMat);
        LowPolyKit.CreateBlock("BloodDrip", torso, new Vector3(-t.x * 0.26f, t.y * 0.02f, front), Vector3.zero, new Vector3(t.x * 0.07f, t.y * 0.2f, 0.02f), bloodMat);
        LowPolyKit.CreateBlock("BloodBack", torso, new Vector3(t.x * 0.15f, 0f, -front), Vector3.zero, new Vector3(t.x * 0.3f, t.y * 0.25f, 0.02f), bloodMat);

        // Sleeves torn off at the elbow (bare forearms) and jeans, with one leg ripped off at the knee.
        Recolor(skinMat, rig.forearmL, rig.forearmR, rig.shinR);
        Recolor(pantsMat, rig.pelvis, rig.thighL, rig.thighR, rig.shinL);
    }

    private static void Recolor(Material material, params Transform[] parts)
    {
        foreach (Transform part in parts)
        {
            if (part != null && part.TryGetComponent(out Renderer renderer)) renderer.sharedMaterial = material;
        }
    }

    private static Enemy GetOrCreateRunnerPrefab()
    {
        string path = $"{PrefabFolder}/EnemyRunner.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "EnemyRunner";

        CapsuleCollider collider = LowPolyKit.GetOrAddComponent<CapsuleCollider>(go);
        collider.center = new Vector3(0f, 0.85f, 0f);
        collider.radius = 0.28f;
        collider.height = 1.7f;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(16f);
        Enemy enemy = LowPolyKit.GetOrAddComponent<Enemy>(go);
        SetPrivateFloat(enemy, "moveSpeed", 6f);
        SetPrivateFloat(enemy, "attackRange", 1f);
        SetPrivateFloat(enemy, "attackDamage", 6f);
        SetPrivateInt(enemy, "scrapReward", 8);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.55f, 0.2f, 0.18f),
            skin = new Color(0.64f, 0.68f, 0.62f),
            headwear = new Color(0.1f, 0.1f, 0.1f),
            accent = ZombieBlood,
            weaponBody = new Color(0.55f, 0.2f, 0.18f),
            weaponDark = new Color(0.5f, 0.54f, 0.5f),
        };

        CharacterFactory.BipedProportions p = CharacterFactory.Zombified(CharacterFactory.RunnerProportions, new Vector3(-58f, 6f, 0f), new Vector3(-70f, -6f, 0f), 6f);
        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, p, colors, "Runner", hasWeapon: false);
        AddZombieDetails(rig, p, "Runner", colors.skin, new Color(0.2f, 0.2f, 0.22f), new Color(2.6f, 0.3f, 0.2f));
        ConfigureEnemyFeedback(enemy, colors.accent, 10, 0f, 0f);
        AddTeamRing(go.transform, 0.5f, EnemyRingColor);

        var runnerGait = new CharacterFactory.Gait
        {
            stateName = "Run",
            period = 0.3f,
            legSwingDeg = 46f,
            armSwingDeg = 34f,
            hipsBob = 0.12f,
            torsoLeanExtra = 6f,
        };

        CharacterFactory.BuildAnimations(go, rig, p, "Runner",
            runnerGait, null, "Attack", 50f, 20f, false, out AnimatorController controller);

        Animator animator = LowPolyKit.GetOrAddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        GameObject prefab = LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
        return prefab.GetComponent<Enemy>();
    }

    private static Enemy GetOrCreateBrutePrefab()
    {
        string path = $"{PrefabFolder}/EnemyBrute.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "EnemyBrute";

        CapsuleCollider collider = LowPolyKit.GetOrAddComponent<CapsuleCollider>(go);
        collider.center = new Vector3(0f, 1.18f, 0f);
        collider.radius = 0.55f;
        collider.height = 2.45f;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(120f);
        Enemy enemy = LowPolyKit.GetOrAddComponent<Enemy>(go);
        SetPrivateFloat(enemy, "moveSpeed", 1.6f);
        SetPrivateFloat(enemy, "attackRange", 1.6f);
        SetPrivateFloat(enemy, "attackDamage", 30f);
        SetPrivateInt(enemy, "scrapReward", 35);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.3f, 0.25f, 0.22f),
            skin = new Color(0.42f, 0.5f, 0.4f),
            headwear = new Color(0.1f, 0.1f, 0.1f),
            accent = ZombieBlood,
            weaponBody = new Color(0.3f, 0.25f, 0.22f),
            weaponDark = new Color(0.34f, 0.4f, 0.32f),
        };

        CharacterFactory.BipedProportions p = CharacterFactory.Zombified(CharacterFactory.BruteProportions, new Vector3(-48f, 0f, 0f), new Vector3(-38f, 0f, 0f), 10f);
        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, p, colors, "Brute", hasWeapon: false);
        AddZombieDetails(rig, p, "Brute", colors.skin, new Color(0.22f, 0.2f, 0.18f), new Color(2.4f, 1f, 0.2f));
        ConfigureEnemyFeedback(enemy, colors.accent, 26, 0.15f, 0.2f);
        AddTeamRing(go.transform, 0.95f, EnemyRingColor);

        // Bone spurs bursting out of the shoulders and back.
        Material boneMat = GetOrCreateMaterial("ZombieBone", ZombieBone, unlit: false);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 shoulder = new Vector3(side * p.armSpacingX, p.armY + 0.12f, 0f);
            LowPolyKit.CreateBlock("BoneSpur", rig.torso, shoulder, new Vector3(0f, 45f, side * -25f), new Vector3(0.12f, 0.42f, 0.12f), boneMat);
            LowPolyKit.CreateBlock("BoneSpur", rig.torso, shoulder + new Vector3(-side * 0.16f, -0.05f, -0.12f), new Vector3(-20f, 45f, side * -15f), new Vector3(0.09f, 0.3f, 0.09f), boneMat);
        }
        LowPolyKit.CreateBlock("BackSpur", rig.torso, new Vector3(0f, 0.2f, -p.torsoSize.z * 0.5f - 0.05f), new Vector3(-50f, 45f, 0f), new Vector3(0.1f, 0.34f, 0.1f), boneMat);

        var bruteGait = new CharacterFactory.Gait
        {
            stateName = "Walk",
            period = 1.0f,
            legSwingDeg = 18f,
            armSwingDeg = 14f,
            hipsBob = 0.16f,
            torsoLeanExtra = 0f,
        };

        CharacterFactory.BuildAnimations(go, rig, p, "Brute",
            bruteGait, null, "Attack", 55f, 18f, true, out AnimatorController controller);

        Animator animator = LowPolyKit.GetOrAddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        GameObject prefab = LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
        return prefab.GetComponent<Enemy>();
    }

    private static Enemy GetOrCreateSpitterPrefab()
    {
        string path = $"{PrefabFolder}/EnemySpitter.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "EnemySpitter";

        CapsuleCollider collider = LowPolyKit.GetOrAddComponent<CapsuleCollider>(go);
        collider.center = new Vector3(0f, 0.92f, 0f);
        collider.radius = 0.36f;
        collider.height = 1.84f;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(40f);
        Enemy enemy = LowPolyKit.GetOrAddComponent<Enemy>(go);
        SetPrivateFloat(enemy, "moveSpeed", 2.5f);
        SetPrivateFloat(enemy, "attackRange", 1.2f);
        SetPrivateFloat(enemy, "attackDamage", 14f);
        SetPrivateInt(enemy, "scrapReward", 14);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.36f, 0.42f, 0.26f),
            skin = new Color(0.62f, 0.7f, 0.36f),
            headwear = new Color(0.16f, 0.18f, 0.12f),
            accent = ZombieBlood,
            weaponBody = new Color(0.36f, 0.42f, 0.26f),
            weaponDark = new Color(0.5f, 0.58f, 0.3f),
        };

        CharacterFactory.BipedProportions p = CharacterFactory.Zombified(CharacterFactory.SpitterProportions, new Vector3(-66f, 0f, 0f), new Vector3(-54f, 0f, 0f), 8f);
        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, p, colors, "Spitter", hasWeapon: false);
        AddZombieDetails(rig, p, "Spitter", colors.skin, new Color(0.26f, 0.24f, 0.2f), new Color(0.6f, 2.4f, 0.4f));
        ConfigureEnemyFeedback(enemy, colors.accent, 14, 0f, 0f);
        AddTeamRing(go.transform, 0.6f, EnemyRingColor);

        // Glowing acid: the sac on its back and a drip hanging from the mouth.
        Material spitterAccentMat = GetOrCreateMaterial("SpitterAccent", new Color(0.5f, 1.8f, 0.3f), unlit: true);
        float torsoHalfDepth = p.torsoSize.z * 0.5f;
        float headCenterY = CharacterFactory.HeadCenterY(p);
        LowPolyKit.CreateBlock("Drool", rig.torso, new Vector3(0.02f, headCenterY - p.headSize.y * 0.5f - 0.05f, p.headSize.z * 0.5f), Vector3.zero, new Vector3(0.05f, 0.14f, 0.05f), spitterAccentMat);
        LowPolyKit.CreateBlock("Sac", rig.torso, new Vector3(0f, 0.06f, -torsoHalfDepth - 0.1f), Vector3.zero, new Vector3(0.34f, 0.32f, 0.28f), spitterAccentMat, PrimitiveType.Sphere);
        LowPolyKit.CreateBlock("SacRidge", rig.torso, new Vector3(0f, 0.3f, -torsoHalfDepth - 0.02f), Vector3.zero, new Vector3(0.1f, 0.14f, 0.12f), spitterAccentMat);

        var spitterGait = new CharacterFactory.Gait
        {
            stateName = "Walk",
            period = 0.7f,
            legSwingDeg = 22f,
            armSwingDeg = 14f,
            hipsBob = 0.05f,
            torsoLeanExtra = 4f,
        };

        CharacterFactory.BuildAnimations(go, rig, p, "Spitter",
            spitterGait, null, "Attack", 25f, 25f, false, out AnimatorController controller);

        Animator animator = LowPolyKit.GetOrAddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        GameObject prefab = LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
        return prefab.GetComponent<Enemy>();
    }

    private static Enemy GetOrCreateBomberPrefab()
    {
        string path = $"{PrefabFolder}/EnemyBomber.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "EnemyBomber";

        CapsuleCollider collider = LowPolyKit.GetOrAddComponent<CapsuleCollider>(go);
        collider.center = new Vector3(0f, 0.95f, 0f);
        collider.radius = 0.38f;
        collider.height = 1.9f;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(25f);
        Enemy enemy = LowPolyKit.GetOrAddComponent<Enemy>(go);
        SetPrivateFloat(enemy, "moveSpeed", 3.2f);
        SetPrivateFloat(enemy, "attackRange", 1.2f);
        SetPrivateFloat(enemy, "attackDamage", 25f);
        SetPrivateInt(enemy, "scrapReward", 12);
        SetPrivateBool(enemy, "dieOnAttack", true);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.4f, 0.36f, 0.42f),
            skin = new Color(0.6f, 0.56f, 0.42f),
            headwear = new Color(0.14f, 0.12f, 0.1f),
            accent = ZombieBlood,
            weaponBody = new Color(0.4f, 0.36f, 0.42f),
            weaponDark = new Color(0.48f, 0.44f, 0.34f),
        };

        CharacterFactory.BipedProportions p = CharacterFactory.Zombified(CharacterFactory.BomberProportions, new Vector3(-70f, 6f, 0f), new Vector3(-80f, -6f, 0f), 6f);
        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, p, colors, "Bomber", hasWeapon: false);
        AddZombieDetails(rig, p, "Bomber", colors.skin, new Color(0.3f, 0.28f, 0.3f), new Color(2.6f, 1.2f, 0.2f));
        ConfigureEnemyFeedback(enemy, colors.accent, 22, 0.35f, 0f);
        AddTeamRing(go.transform, 0.6f, EnemyRingColor);

        // Swollen, glowing boils all over - the tell that this one bursts.
        Material bomberAccentMat = GetOrCreateMaterial("BomberAccent", new Color(2.6f, 1.1f, 0.15f), unlit: true);
        float torsoHalfDepth = p.torsoSize.z * 0.5f;
        float torsoHalfWidth = p.torsoSize.x * 0.5f;
        Vector3[] boils =
        {
            new Vector3(0.12f, 0.12f, torsoHalfDepth), new Vector3(-0.16f, -0.12f, torsoHalfDepth),
            new Vector3(0.05f, -0.2f, torsoHalfDepth), new Vector3(-0.1f, 0.18f, -torsoHalfDepth),
            new Vector3(0.14f, -0.05f, -torsoHalfDepth), new Vector3(torsoHalfWidth, 0.05f, 0.04f),
            new Vector3(-torsoHalfWidth, -0.1f, -0.05f),
        };
        for (int i = 0; i < boils.Length; i++)
        {
            float size = 0.12f + (i % 3) * 0.04f;
            LowPolyKit.CreateBlock("Boil", rig.torso, boils[i], Vector3.zero, Vector3.one * size, bomberAccentMat, PrimitiveType.Sphere);
        }

        var bomberGait = new CharacterFactory.Gait
        {
            stateName = "Walk",
            period = 0.55f,
            legSwingDeg = 26f,
            armSwingDeg = 30f,
            hipsBob = 0.08f,
            torsoLeanExtra = 0f,
        };

        CharacterFactory.BuildAnimations(go, rig, p, "Bomber",
            bomberGait, null, "Attack", 60f, 15f, true, out AnimatorController controller);

        Animator animator = LowPolyKit.GetOrAddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        GameObject prefab = LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
        return prefab.GetComponent<Enemy>();
    }

    private static Enemy GetOrCreateEngineerPrefab()
    {
        string path = $"{PrefabFolder}/EnemyEngineer.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "EnemyEngineer";

        CapsuleCollider collider = LowPolyKit.GetOrAddComponent<CapsuleCollider>(go);
        collider.center = new Vector3(0f, 0.98f, 0f);
        collider.radius = 0.34f;
        collider.height = 1.96f;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(35f);
        Enemy enemy = LowPolyKit.GetOrAddComponent<Enemy>(go);
        SetPrivateFloat(enemy, "moveSpeed", 2.2f);
        SetPrivateFloat(enemy, "attackRange", 1f);
        SetPrivateFloat(enemy, "attackDamage", 8f);
        SetPrivateInt(enemy, "scrapReward", 15);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.85f, 0.7f, 0.2f),
            skin = new Color(0.52f, 0.62f, 0.44f),
            headwear = new Color(0.95f, 0.55f, 0.08f),
            accent = ZombieBlood,
            weaponBody = new Color(0.85f, 0.7f, 0.2f),
            weaponDark = new Color(0.42f, 0.5f, 0.36f),
        };

        CharacterFactory.BipedProportions p = CharacterFactory.Zombified(CharacterFactory.EngineerProportions, new Vector3(-80f, 4f, 0f), new Vector3(-55f, -4f, 0f), 10f);
        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, p, colors, "Engineer", hasWeapon: false);
        AddZombieDetails(rig, p, "Engineer", colors.skin, new Color(0.22f, 0.26f, 0.36f), new Color(2.4f, 2f, 0.3f));
        ConfigureEnemyFeedback(enemy, colors.accent, 14, 0f, 0f);
        AddTeamRing(go.transform, 0.6f, EnemyRingColor);

        Material engineerTankMat = GetOrCreateMaterial("EngineerTank", new Color(0.3f, 0.3f, 0.32f), unlit: false);
        Material engineerAccentMat = GetOrCreateMaterial("EngineerAccent", new Color(0.95f, 0.6f, 0.1f), unlit: true);
        float torsoHalfDepth = p.torsoSize.z * 0.5f;
        LowPolyKit.CreateBlock("Tank", rig.torso, new Vector3(0f, 0.1f, -torsoHalfDepth - 0.1f), Vector3.zero, new Vector3(0.24f, 0.4f, 0.2f), engineerTankMat);
        LowPolyKit.CreateBlock("TankStripe", rig.torso, new Vector3(0f, 0.1f, -torsoHalfDepth), Vector3.zero, new Vector3(0.2f, 0.08f, 0.05f), engineerAccentMat);
        LowPolyKit.CreateBlock("ToolHandle", rig.weaponSocket, new Vector3(0f, -0.05f, 0.1f), new Vector3(20f, 0f, 0f), new Vector3(0.04f, 0.24f, 0.04f), engineerTankMat);
        LowPolyKit.CreateBlock("ToolHead", rig.weaponSocket, new Vector3(0f, 0.1f, 0.2f), Vector3.zero, new Vector3(0.1f, 0.06f, 0.03f), engineerAccentMat);

        var engineerGait = new CharacterFactory.Gait
        {
            stateName = "Walk",
            period = 0.65f,
            legSwingDeg = 20f,
            armSwingDeg = 16f,
            hipsBob = 0.05f,
            torsoLeanExtra = 0f,
        };

        // Enemy.cs only fires a single "Attack" trigger for all enemy types, so the
        // Engineer's Repair-tap motion lives in the same Attack state/clip slot rather than
        // a separate parameter - adding a per-type trigger name would be a gameplay change,
        // not a visual one.
        CharacterFactory.BuildAnimations(go, rig, p, "Engineer",
            engineerGait, null, "Attack", 20f, 6f, false, out AnimatorController controller);

        Animator animator = LowPolyKit.GetOrAddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        GameObject prefab = LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
        return prefab.GetComponent<Enemy>();
    }

    // A square stone block that fills one grid cell, so a row of them reads as one continuous wall.
    private static Wall GetOrCreateWallPrefab()
    {
        string path = $"{PrefabFolder}/Wall.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "Wall";

        Material stoneMat = GetOrCreateMaterial("WallStone", new Color(0.62f, 0.58f, 0.52f), unlit: false);
        Material darkStoneMat = GetOrCreateMaterial("WallStoneDark", new Color(0.42f, 0.39f, 0.36f), unlit: false);
        Material woodMat = GetOrCreateMaterial("WallWood", new Color(0.5f, 0.34f, 0.18f), unlit: false);

        LowPolyKit.CreateBlock("Footing", go.transform, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(2.95f, 0.24f, 2.95f), darkStoneMat);
        LowPolyKit.CreateBlock("Body", go.transform, new Vector3(0f, 0.8f, 0f), Vector3.zero, new Vector3(2.8f, 1.2f, 2.8f), stoneMat);
        LowPolyKit.CreateBlock("Cap", go.transform, new Vector3(0f, 1.44f, 0f), Vector3.zero, new Vector3(2.9f, 0.1f, 2.9f), darkStoneMat);

        // Merlons around the rim, plus a wooden plank band so the block reads as a fortification.
        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                LowPolyKit.CreateBlock("Merlon", go.transform, new Vector3(x * 1.1f, 1.7f, z * 1.1f), Vector3.zero, new Vector3(0.55f, 0.42f, 0.55f), stoneMat);
            }
        }
        LowPolyKit.CreateBlock("PlankBand", go.transform, new Vector3(0f, 0.7f, 0f), Vector3.zero, new Vector3(2.84f, 0.16f, 2.84f), woodMat);

        // Mortar lines, pale corner stones and moss: enough texture to read as masonry without a
        // per-brick block count that would hurt phones (the ring alone is 28 of these).
        Material mortarMat = GetOrCreateMaterial("WallMortar", new Color(0.3f, 0.28f, 0.26f), unlit: false);
        Material quoinMat = GetOrCreateMaterial("WallQuoin", new Color(0.74f, 0.7f, 0.64f), unlit: false);
        Material mossMat = GetOrCreateMaterial("WallMoss", new Color(0.3f, 0.45f, 0.2f), unlit: false);
        LowPolyKit.CreateBlock("MortarLow", go.transform, new Vector3(0f, 0.42f, 0f), Vector3.zero, new Vector3(2.82f, 0.04f, 2.82f), mortarMat);
        LowPolyKit.CreateBlock("MortarHigh", go.transform, new Vector3(0f, 1.12f, 0f), Vector3.zero, new Vector3(2.82f, 0.04f, 2.82f), mortarMat);
        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                LowPolyKit.CreateBlock("Quoin", go.transform, new Vector3(x * 1.32f, 0.8f, z * 1.32f), Vector3.zero, new Vector3(0.2f, 1.18f, 0.2f), quoinMat);
            }
        }
        LowPolyKit.CreateBlock("Moss", go.transform, new Vector3(0.4f, 1.5f, -0.3f), Vector3.zero, new Vector3(1.1f, 0.03f, 0.8f), mossMat);
        LowPolyKit.CreateBlock("MossDrip", go.transform, new Vector3(-0.7f, 1.2f, 1.41f), Vector3.zero, new Vector3(0.5f, 0.45f, 0.03f), mossMat);

        BoxCollider collider = LowPolyKit.GetOrAddComponent<BoxCollider>(go);
        collider.center = new Vector3(0f, 0.95f, 0f);
        collider.size = new Vector3(2.9f, 1.9f, 2.9f);

        LowPolyKit.GetOrAddComponent<Health>(go).Configure(300f);
        LowPolyKit.GetOrAddComponent<HitFlash>(go);
        LowPolyKit.GetOrAddComponent<Wall>(go);

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded).GetComponent<Wall>();
    }

    private static GameObject GetOrCreateBossPrefab()
    {
        // Uses the shared Enemy behavior (walk to core, attack on an interval) tuned slow and
        // heavy. Spawned by EnemySpawner every few waves.
        string path = $"{PrefabFolder}/Boss.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "Boss";

        CapsuleCollider collider = LowPolyKit.GetOrAddComponent<CapsuleCollider>(go);
        collider.center = new Vector3(0f, 2.82f, 0f);
        collider.radius = 1.1f;
        collider.height = 5.75f;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(1200f);
        Enemy enemy = LowPolyKit.GetOrAddComponent<Enemy>(go);
        SetPrivateFloat(enemy, "moveSpeed", 1.2f);
        SetPrivateFloat(enemy, "attackRange", 2.8f);
        SetPrivateFloat(enemy, "attackDamage", 60f);
        SetPrivateFloat(enemy, "attackInterval", 2f);
        SetPrivateInt(enemy, "scrapReward", 250);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.24f, 0.12f, 0.12f),
            skin = new Color(0.32f, 0.38f, 0.28f),
            headwear = new Color(0.08f, 0.08f, 0.08f),
            accent = ZombieBlood,
            weaponBody = new Color(0.24f, 0.12f, 0.12f),
            weaponDark = new Color(0.26f, 0.3f, 0.22f),
        };

        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, CharacterFactory.BossProportions, colors, "Boss", hasWeapon: false);
        ConfigureEnemyFeedback(enemy, colors.accent, 70, 0.5f, 1f);
        AddZombieDetails(rig, CharacterFactory.BossProportions, "Boss", colors.skin, new Color(0.18f, 0.14f, 0.12f), new Color(3f, 0.25f, 0.15f));
        AddTeamRing(go.transform, 1.9f, EnemyRingColor);

        Material bossAccentMat = GetOrCreateMaterial("BossAccent", new Color(2.4f, 0.3f, 0.15f), unlit: true);
        Material boneMat = GetOrCreateMaterial("ZombieBone", ZombieBone, unlit: false);
        CharacterFactory.BipedProportions bp = CharacterFactory.BossProportions;

        Vector3 pauldronSize = new Vector3(bp.armSize.x * 1.8f, 0.32f, bp.armSize.z * 1.8f);
        LowPolyKit.CreateBlock("Pauldron", rig.armLHinge, new Vector3(0f, 0.16f, 0f), Vector3.zero, pauldronSize, boneMat);
        LowPolyKit.CreateBlock("Pauldron", rig.armRHinge, new Vector3(0f, 0.16f, 0f), Vector3.zero, pauldronSize, boneMat);
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 0.45f;
            LowPolyKit.CreateBlock("BackSpur", rig.torso, new Vector3(x, 0.5f - Mathf.Abs(x) * 0.4f, -bp.torsoSize.z * 0.5f - 0.1f), new Vector3(-45f, 45f, 0f), new Vector3(0.22f, 0.7f, 0.22f), boneMat);
        }

        // A glowing wound where the old chest light was.
        LowPolyKit.CreateBlock("CoreLight", rig.torso, new Vector3(0f, 0.3f, bp.torsoSize.z * 0.5f + 0.02f), Vector3.zero, new Vector3(0.4f, 0.4f, 0.08f), bossAccentMat);

        // Glowing knuckles on the front of each fist.
        foreach (Transform hand in new[] { rig.handL, rig.handR })
        {
            Vector3 knuckle = hand.localPosition + new Vector3(0f, -hand.localScale.y * 0.2f, hand.localScale.z * 0.5f + 0.02f);
            LowPolyKit.CreateBlock("KnuckleGlow", hand.parent, knuckle, Vector3.zero, new Vector3(hand.localScale.x * 0.8f, hand.localScale.y * 0.35f, 0.05f), bossAccentMat);
        }

        LowPolyKit.CreateBlock("LegStripeL", rig.legLHinge, new Vector3(0f, -bp.legSize.y * 0.5f, bp.legSize.z * 0.5f + 0.01f), Vector3.zero, new Vector3(bp.legSize.x * 0.3f, bp.legSize.y * 0.6f, 0.04f), bossAccentMat);
        LowPolyKit.CreateBlock("LegStripeR", rig.legRHinge, new Vector3(0f, -bp.legSize.y * 0.5f, bp.legSize.z * 0.5f + 0.01f), Vector3.zero, new Vector3(bp.legSize.x * 0.3f, bp.legSize.y * 0.6f, 0.04f), bossAccentMat);

        CharacterFactory.BuildBossAnimations(go, rig, bp, out AnimatorController controller);

        Animator animator = LowPolyKit.GetOrAddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
    }

    private enum TowerStyle
    {
        Gun,
        Cannon,
        Sniper,
        Tesla,
        Missile,
    }

    // Every tower: a stone plinth and body with a band in the tower's card colour (so it matches the
    // build button), a turret ring, and a style-specific turret on a pivot the Tower script yaws.
    private static Tower GetOrCreateTowerPrefab(string prefabName, TowerStyle style, Color baseColor, Color accentColor, float scale, float range, float fireRate, float damage, Projectile projectilePrefab)
    {
        string path = $"{PrefabFolder}/{prefabName}.prefab";
        GameObject baseGo = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(baseGo);
        LowPolyKit.RemoveOwnMesh(baseGo);
        baseGo.name = prefabName;
        baseGo.transform.localScale = Vector3.one * scale;

        Material plinthMat = GetOrCreateMaterial("TowerPlinth", new Color(0.4f, 0.38f, 0.36f), unlit: false);
        Material bodyMat = GetOrCreateMaterial($"{prefabName}Base", baseColor, unlit: false);
        Material trimMat = GetOrCreateMaterial("TowerTrim", new Color(0.2f, 0.2f, 0.22f), unlit: false);
        Material bandMat = GetOrCreateMaterial($"{prefabName}Band", accentColor, unlit: true);

        LowPolyKit.CreateBlock("Plinth", baseGo.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0f, 22.5f, 0f), new Vector3(2.1f, 0.12f, 2.1f), plinthMat, PrimitiveType.Cylinder);
        LowPolyKit.CreateBlock("Body", baseGo.transform, new Vector3(0f, 0.65f, 0f), Vector3.zero, new Vector3(1.4f, 0.85f, 1.4f), bodyMat);
        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                LowPolyKit.CreateBlock("Buttress", baseGo.transform, new Vector3(x * 0.68f, 0.5f, z * 0.68f), Vector3.zero, new Vector3(0.24f, 0.8f, 0.24f), trimMat);
            }
        }
        LowPolyKit.CreateBlock("Band", baseGo.transform, new Vector3(0f, 0.92f, 0f), Vector3.zero, new Vector3(1.44f, 0.1f, 1.44f), bandMat);
        LowPolyKit.CreateBlock("Ring", baseGo.transform, new Vector3(0f, 1.12f, 0f), Vector3.zero, new Vector3(1.25f, 0.06f, 1.25f), trimMat, PrimitiveType.Cylinder);

        BoxCollider collider = LowPolyKit.GetOrAddComponent<BoxCollider>(baseGo);
        collider.center = new Vector3(0f, 0.8f, 0f);
        collider.size = new Vector3(1.6f, 1.6f, 1.6f);

        GameObject turretPivot = new GameObject("TurretPivot");
        turretPivot.transform.SetParent(baseGo.transform, false);
        turretPivot.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(turretPivot.transform, false);
        firePoint.transform.localPosition = BuildTurret(turretPivot.transform, style, accentColor);

        Tower tower = LowPolyKit.GetOrAddComponent<Tower>(baseGo);
        tower.Configure(projectilePrefab, firePoint.transform, turretPivot.transform);
        tower.ConfigureStats(range, fireRate, damage);

        GameObject prefab = LowPolyKit.EndPrefabEdit(baseGo, path, wasLoaded);
        return prefab.GetComponent<Tower>();
    }

    // Builds the turret for `style` on the pivot (forward = +Z) and returns where shots leave it.
    private static Vector3 BuildTurret(Transform pivot, TowerStyle style, Color accentColor)
    {
        Material gunMetal = GetOrCreateMaterial("BarrelDark", new Color(0.15f, 0.15f, 0.17f), unlit: false);
        Material steel = GetOrCreateMaterial("TowerSteel", new Color(0.34f, 0.36f, 0.4f), unlit: false);
        Material accentGlow = GetOrCreateMaterial($"TowerGlow{style}", accentColor * 1.8f, unlit: true);

        switch (style)
        {
            case TowerStyle.Gun:
            {
                Material sandbagMat = GetOrCreateMaterial("Sandbag", new Color(0.62f, 0.55f, 0.38f), unlit: false);
                Material brassMat = GetOrCreateMaterial("AmmoBrass", new Color(0.85f, 0.65f, 0.25f), unlit: false);

                // Sandbags sit on the base ring and don't turn with the gun.
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * 60f + 30f;
                    Vector3 pos = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 0.58f;
                    LowPolyKit.CreateBlock("Sandbag", pivot.parent, pos + new Vector3(0f, 1.24f, 0f), new Vector3(0f, angle, 0f), new Vector3(0.5f, 0.18f, 0.26f), sandbagMat);
                }

                LowPolyKit.CreateBlock("Housing", pivot, new Vector3(0f, 0.22f, -0.05f), Vector3.zero, new Vector3(0.62f, 0.36f, 0.66f), steel);
                LowPolyKit.CreateBlock("Shield", pivot, new Vector3(0f, 0.32f, 0.36f), new Vector3(-12f, 0f, 0f), new Vector3(0.9f, 0.55f, 0.06f), gunMetal);
                LowPolyKit.CreateBlock("AmmoBox", pivot, new Vector3(0.42f, 0.16f, -0.1f), Vector3.zero, new Vector3(0.2f, 0.26f, 0.32f), gunMetal);
                LowPolyKit.CreateBlock("AmmoBelt", pivot, new Vector3(0.3f, 0.26f, 0.08f), new Vector3(0f, 0f, 30f), new Vector3(0.22f, 0.05f, 0.08f), brassMat);
                for (int side = -1; side <= 1; side += 2)
                {
                    AddBarrel(pivot, new Vector3(side * 0.13f, 0.24f, 0.78f), new Vector3(90f, 0f, 0f), 0.95f, 0.09f, gunMetal, null);
                    LowPolyKit.CreateBlock("MuzzleBrake", pivot, new Vector3(side * 0.13f, 0.24f, 1.24f), new Vector3(90f, 0f, 0f), new Vector3(0.14f, 0.06f, 0.14f), steel, PrimitiveType.Cylinder);
                }
                return new Vector3(0f, 0.24f, 1.3f);
            }

            case TowerStyle.Cannon:
            {
                Material bronzeMat = GetOrCreateMaterial("CannonBronze", new Color(0.62f, 0.42f, 0.2f), unlit: false);
                LowPolyKit.CreateBlock("Dome", pivot, new Vector3(0f, 0.18f, -0.1f), Vector3.zero, new Vector3(1.05f, 0.7f, 1.05f), steel, PrimitiveType.Sphere);
                LowPolyKit.CreateBlock("Mantlet", pivot, new Vector3(0f, 0.3f, 0.38f), Vector3.zero, new Vector3(0.6f, 0.42f, 0.24f), gunMetal);
                AddBarrel(pivot, new Vector3(0f, 0.32f, 1.0f), new Vector3(90f, 0f, 0f), 1.3f, 0.3f, bronzeMat, null);
                LowPolyKit.CreateBlock("BandRear", pivot, new Vector3(0f, 0.32f, 0.62f), new Vector3(90f, 0f, 0f), new Vector3(0.36f, 0.05f, 0.36f), gunMetal, PrimitiveType.Cylinder);
                LowPolyKit.CreateBlock("BandMid", pivot, new Vector3(0f, 0.32f, 1.05f), new Vector3(90f, 0f, 0f), new Vector3(0.34f, 0.04f, 0.34f), gunMetal, PrimitiveType.Cylinder);
                LowPolyKit.CreateBlock("MuzzleRing", pivot, new Vector3(0f, 0.32f, 1.62f), new Vector3(90f, 0f, 0f), new Vector3(0.4f, 0.07f, 0.4f), bronzeMat, PrimitiveType.Cylinder);
                LowPolyKit.CreateBlock("Hatch", pivot, new Vector3(0f, 0.55f, -0.25f), Vector3.zero, new Vector3(0.3f, 0.06f, 0.3f), gunMetal, PrimitiveType.Cylinder);
                return new Vector3(0f, 0.32f, 1.7f);
            }

            case TowerStyle.Sniper:
            {
                Material woodMat = GetOrCreateMaterial("SniperStock", new Color(0.42f, 0.28f, 0.16f), unlit: false);
                LowPolyKit.CreateBlock("Mount", pivot, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(0.3f, 0.24f, 0.3f), steel);
                LowPolyKit.CreateBlock("Receiver", pivot, new Vector3(0f, 0.3f, 0.1f), Vector3.zero, new Vector3(0.16f, 0.16f, 0.6f), gunMetal);
                LowPolyKit.CreateBlock("Stock", pivot, new Vector3(0f, 0.26f, -0.42f), new Vector3(8f, 0f, 0f), new Vector3(0.12f, 0.2f, 0.5f), woodMat);
                AddBarrel(pivot, new Vector3(0f, 0.3f, 1.25f), new Vector3(90f, 0f, 0f), 1.7f, 0.07f, gunMetal, null);
                LowPolyKit.CreateBlock("Scope", pivot, new Vector3(0f, 0.46f, 0.14f), new Vector3(90f, 0f, 0f), new Vector3(0.1f, 0.24f, 0.1f), gunMetal, PrimitiveType.Cylinder);
                LowPolyKit.CreateBlock("Lens", pivot, new Vector3(0f, 0.46f, 0.39f), new Vector3(90f, 0f, 0f), new Vector3(0.09f, 0.01f, 0.09f), accentGlow, PrimitiveType.Cylinder);
                LowPolyKit.CreateBlock("BipodL", pivot, new Vector3(-0.1f, 0.14f, 0.85f), new Vector3(0f, 0f, 20f), new Vector3(0.03f, 0.3f, 0.03f), gunMetal);
                LowPolyKit.CreateBlock("BipodR", pivot, new Vector3(0.1f, 0.14f, 0.85f), new Vector3(0f, 0f, -20f), new Vector3(0.03f, 0.3f, 0.03f), gunMetal);
                return new Vector3(0f, 0.3f, 2.1f);
            }

            case TowerStyle.Missile:
            {
                Material podMat = GetOrCreateMaterial("MissilePod", new Color(0.36f, 0.4f, 0.3f), unlit: false);
                Material warheadMat = GetOrCreateMaterial("MissileTip", new Color(0.85f, 0.15f, 0.1f), unlit: true);
                LowPolyKit.CreateBlock("Yoke", pivot, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(0.9f, 0.3f, 0.5f), steel);

                // Launcher pod tilted up, 2x2 tubes each with a red warhead peeking out.
                GameObject pod = new GameObject("Pod");
                pod.transform.SetParent(pivot, false);
                pod.transform.localPosition = new Vector3(0f, 0.55f, 0.05f);
                pod.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
                LowPolyKit.CreateBlock("Box", pod.transform, Vector3.zero, Vector3.zero, new Vector3(0.9f, 0.62f, 0.95f), podMat);
                LowPolyKit.CreateBlock("Stripe", pod.transform, new Vector3(0f, 0f, -0.2f), Vector3.zero, new Vector3(0.92f, 0.1f, 0.2f), accentGlow);
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        Vector3 tube = new Vector3(x * 0.2f, y * 0.14f, 0.48f);
                        LowPolyKit.CreateBlock("Tube", pod.transform, tube, new Vector3(90f, 0f, 0f), new Vector3(0.2f, 0.03f, 0.2f), gunMetal, PrimitiveType.Cylinder);
                        LowPolyKit.CreateBlock("Warhead", pod.transform, tube + new Vector3(0f, 0f, 0.03f), Vector3.zero, new Vector3(0.14f, 0.14f, 0.16f), warheadMat, PrimitiveType.Sphere);
                    }
                }
                return new Vector3(0f, 0.75f, 0.65f);
            }

            case TowerStyle.Tesla:
            {
                Material copperMat = GetOrCreateMaterial("TeslaCopper", new Color(0.75f, 0.42f, 0.22f), unlit: false);
                Material insulatorMat = GetOrCreateMaterial("TeslaInsulator", new Color(0.85f, 0.85f, 0.8f), unlit: false);

                LowPolyKit.CreateBlock("Column", pivot, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.22f, 0.55f, 0.22f), gunMetal, PrimitiveType.Cylinder);
                for (int i = 0; i < 4; i++)
                {
                    float y = 0.25f + i * 0.2f;
                    float width = 0.62f - i * 0.08f;
                    LowPolyKit.CreateBlock("Coil", pivot, new Vector3(0f, y, 0f), Vector3.zero, new Vector3(width, 0.05f, width), i % 2 == 0 ? copperMat : accentGlow, PrimitiveType.Cylinder);
                }
                for (int i = 0; i < 4; i++)
                {
                    Vector3 dir = Quaternion.Euler(0f, 45f + i * 90f, 0f) * Vector3.forward * 0.5f;
                    LowPolyKit.CreateBlock("Insulator", pivot, dir + new Vector3(0f, 0.18f, 0f), Vector3.zero, new Vector3(0.12f, 0.18f, 0.12f), insulatorMat, PrimitiveType.Cylinder);
                    LowPolyKit.CreateBlock("Spark", pivot, dir + new Vector3(0f, 0.4f, 0f), Vector3.zero, new Vector3(0.08f, 0.08f, 0.08f), accentGlow, PrimitiveType.Sphere);
                }

                GameObject orb = new GameObject("OrbPivot");
                orb.transform.SetParent(pivot, false);
                orb.transform.localPosition = new Vector3(0f, 1.25f, 0f);
                orb.AddComponent<SlowSpinner>().Configure(Vector3.up, 180f);
                LowPolyKit.CreateBlock("Orb", orb.transform, Vector3.zero, Vector3.zero, Vector3.one * 0.42f, accentGlow, PrimitiveType.Sphere);
                LowPolyKit.CreateBlock("OrbRing", orb.transform, Vector3.zero, new Vector3(70f, 0f, 0f), new Vector3(0.62f, 0.02f, 0.62f), copperMat, PrimitiveType.Cylinder);
                return new Vector3(0f, 1.25f, 0.25f);
            }
        }

        return new Vector3(0f, 0.3f, 0.8f);
    }

    private static void AddBarrel(Transform parent, Vector3 localPos, Vector3 localEuler, float length, float radius, Material material, Color? tipColor)
    {
        Quaternion rotation = Quaternion.Euler(localEuler);

        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "Barrel";
        Object.DestroyImmediate(barrel.GetComponent<Collider>());
        barrel.transform.SetParent(parent, false);
        barrel.transform.localPosition = localPos;
        barrel.transform.localRotation = rotation;
        barrel.transform.localScale = new Vector3(radius, length * 0.5f, radius);
        barrel.GetComponent<Renderer>().sharedMaterial = material;

        if (!tipColor.HasValue) return;

        Vector3 tipOffset = rotation * Vector3.up * (length * 0.5f);
        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Tip";
        Object.DestroyImmediate(tip.GetComponent<Collider>());
        tip.transform.SetParent(parent, false);
        tip.transform.localPosition = localPos + tipOffset;
        tip.transform.localScale = new Vector3(radius * 2.2f, radius * 2.2f, radius * 2.2f);
        tip.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("MissileTip", tipColor.Value, unlit: true);
    }

    private static void SetTowerDefinitions(TowerPlacer placer, (string name, int cost, GameObject prefab, Color accentColor)[] defs)
    {
        var so = new SerializedObject(placer);
        SerializedProperty arrayProp = so.FindProperty("towerDefinitions");
        arrayProp.arraySize = defs.Length;

        for (int i = 0; i < defs.Length; i++)
        {
            SerializedProperty element = arrayProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("displayName").stringValue = defs[i].name;
            element.FindPropertyRelative("cost").intValue = defs[i].cost;
            element.FindPropertyRelative("prefab").objectReferenceValue = defs[i].prefab;
            element.FindPropertyRelative("accentColor").colorValue = defs[i].accentColor;
        }

        so.ApplyModifiedProperties();
    }

    private static GameObject GetOrCreatePlayerPrefab(Projectile bulletPrefab, Projectile arrowPrefab)
    {
        string path = $"{PrefabFolder}/Player.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "Player";

        CapsuleCollider collider = LowPolyKit.GetOrAddComponent<CapsuleCollider>(go);
        collider.center = new Vector3(0f, 1f, 0f);
        collider.radius = 0.38f;
        collider.height = 2f;

        LowPolyKit.GetOrAddComponent<Rigidbody>(go);
        LowPolyKit.GetOrAddComponent<Health>(go).Configure(200f);
        LowPolyKit.GetOrAddComponent<HitFlash>(go);
        LowPolyKit.GetOrAddComponent<PlayerController>(go);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.2f, 0.42f, 0.88f),
            skin = new Color(0.78f, 0.6f, 0.48f),
            headwear = new Color(0.16f, 0.19f, 0.15f),
            accent = new Color(0.3f, 0.85f, 1f),
            weaponBody = new Color(0.22f, 0.22f, 0.24f),
            weaponDark = new Color(0.08f, 0.08f, 0.09f),
        };

        CharacterFactory.BipedProportions proportions = CharacterFactory.PlayerProportions;
        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, proportions, colors, "Player");
        AddHeroGear(rig, proportions, colors);

        // Chunkier than an enemy grunt's rifle so the hero's weapon reads at phone size.
        rig.rifle.localScale = Vector3.one * 1.4f;

        GameObject bow = new GameObject("Bow");
        bow.transform.SetParent(rig.weaponSocket, false);
        bow.transform.localScale = Vector3.one * 1.1f;
        CharacterFactory.BuildBow(bow.transform,
            GetOrCreateMaterial("PlayerBowWood", new Color(0.5f, 0.32f, 0.16f), unlit: false),
            GetOrCreateMaterial("PlayerBowString", new Color(0.92f, 0.9f, 0.82f), unlit: true),
            GetOrCreateMaterial("PlayerBowArrow", new Color(0.85f, 0.88f, 0.92f), unlit: false));
        bow.SetActive(false);

        AddTeamRing(go.transform, 0.65f, PlayerRingColor);
        CharacterFactory.BuildAnimations(go, rig, proportions, "Player",
            CharacterFactory.WalkGait, CharacterFactory.RunGait, "Shoot",
            12f, 4f, true, out AnimatorController controller);

        Animator animator = LowPolyKit.GetOrAddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        // Kept as a stable, non-animated child (rather than the visual rifle's muzzle) so
        // shot direction never drifts while the recoil animation is playing. It starts where
        // the muzzle sits in the rest pose so bullets visibly leave the gun.
        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(go.transform, false);
        firePoint.transform.localPosition = go.transform.InverseTransformPoint(rig.muzzle.position);

        // Eye just inside the front of the head, a touch below its centre.
        Vector3 eye = rig.torso.localPosition + rig.hips.localPosition + new Vector3(0f, CharacterFactory.HeadCenterY(proportions) - 0.01f, proportions.headSize.z * 0.3f);
        LowPolyKit.GetOrAddComponent<FirstPersonView>(go).Configure(new[] { rig.armLHinge, rig.armRHinge }, eye);

        PlayerShooter shooter = LowPolyKit.GetOrAddComponent<PlayerShooter>(go);
        shooter.Configure(new[]
        {
            new HeroWeapon
            {
                kind = HeroWeaponKind.Rifle, model = rig.rifle.gameObject, projectilePrefab = bulletPrefab,
                fireRate = 4f, damage = 10f, range = 10f, fireSound = SfxId.RifleShot, firePitch = 1f, fireVolume = 1f,
            },
            new HeroWeapon
            {
                kind = HeroWeaponKind.Bow, model = bow, projectilePrefab = arrowPrefab,
                fireRate = 1.5f, damage = 28f, range = 12f, fireSound = SfxId.BowShot, firePitch = 1f, fireVolume = 1f,
            },
        }, firePoint.transform);

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
    }

    // Helmet visor, face, armour vest, backpack, belt, shoulder and knee pads that turn the plain block
    // biped into a fighter's silhouette. Shared by the hero and their hired soldiers.
    private static void AddHeroGear(CharacterFactory.BipedRig rig, CharacterFactory.BipedProportions p, CharacterFactory.BipedColors c)
    {
        Material gearMat = GetOrCreateMaterial("PlayerGear", new Color(0.26f, 0.3f, 0.2f), unlit: false);
        Material strapMat = GetOrCreateMaterial("PlayerStrap", new Color(0.32f, 0.22f, 0.14f), unlit: false);
        Material eyeMat = GetOrCreateMaterial("PlayerEyes", new Color(0.06f, 0.06f, 0.08f), unlit: true);
        Material visorMat = GetOrCreateMaterial("PlayerVisor", new Color(0.5f, 1.4f, 2f), unlit: true);
        Material padMat = GetOrCreateMaterial("PlayerHeadwear", c.headwear, unlit: false);

        Transform torso = rig.torso;
        float halfTorsoY = p.torsoSize.y * 0.5f;
        float halfTorsoZ = p.torsoSize.z * 0.5f;
        float headCenterY = CharacterFactory.HeadCenterY(p);
        float headFrontZ = p.headSize.z * 0.5f;

        Vector3 h = p.headSize;
        Vector3 eyeSize = new Vector3(h.x * 0.16f, h.y * 0.19f, 0.02f);
        LowPolyKit.CreateBlock("EyeL", torso, new Vector3(-h.x * 0.21f, headCenterY + h.y * 0.03f, headFrontZ + 0.005f), Vector3.zero, eyeSize, eyeMat);
        LowPolyKit.CreateBlock("EyeR", torso, new Vector3(h.x * 0.21f, headCenterY + h.y * 0.03f, headFrontZ + 0.005f), Vector3.zero, eyeSize, eyeMat);
        LowPolyKit.CreateBlock("Visor", torso, new Vector3(0f, headCenterY + h.y * 0.42f, headFrontZ + 0.03f), new Vector3(-12f, 0f, 0f), new Vector3(h.x * 1.05f, 0.06f, 0.1f), visorMat);

        Vector3 t = p.torsoSize;
        LowPolyKit.CreateBlock("Backpack", torso, new Vector3(0f, t.y * 0.05f, -halfTorsoZ - 0.09f), Vector3.zero, new Vector3(t.x * 0.76f, t.y * 0.7f, 0.18f), gearMat);
        LowPolyKit.CreateBlock("Bedroll", torso, new Vector3(0f, t.y * 0.45f, -halfTorsoZ - 0.11f), new Vector3(0f, 0f, 90f), new Vector3(0.12f, t.x * 0.42f, 0.12f), strapMat, PrimitiveType.Cylinder);
        LowPolyKit.CreateBlock("Strap", torso, new Vector3(0f, t.y * 0.05f, halfTorsoZ + 0.055f), new Vector3(0f, 0f, 40f), new Vector3(0.07f, t.y * 1.25f, 0.02f), strapMat);

        Vector3 pad = new Vector3(p.armSize.x * 2.1f, 0.09f, p.armSize.z * 2.3f);
        LowPolyKit.CreateBlock("PadL", torso, new Vector3(-p.armSpacingX, p.armY + 0.08f, 0f), new Vector3(0f, 0f, 14f), pad, padMat);
        LowPolyKit.CreateBlock("PadR", torso, new Vector3(p.armSpacingX, p.armY + 0.08f, 0f), new Vector3(0f, 0f, -14f), pad, padMat);

        LowPolyKit.CreateBlock("Belt", rig.hips, new Vector3(0f, p.pelvisSize.y + 0.02f, 0f), Vector3.zero, new Vector3(p.pelvisSize.x + 0.06f, 0.09f, p.pelvisSize.z + 0.06f), strapMat);
        LowPolyKit.CreateBlock("Pouch", rig.hips, new Vector3(0.2f, p.pelvisSize.y - 0.04f, p.pelvisSize.z * 0.5f + 0.05f), Vector3.zero, new Vector3(0.12f, 0.12f, 0.08f), gearMat);

        // Armour vest over the chest (with a few mag pouches) and a collar where the helmet meets the body.
        Material vestMat = GetOrCreateMaterial("PlayerVest", new Color(0.2f, 0.24f, 0.18f), unlit: false);
        LowPolyKit.CreateBlock("Vest", torso, new Vector3(0f, 0.02f, halfTorsoZ + 0.02f), Vector3.zero, new Vector3(p.torsoSize.x * 0.82f, p.torsoSize.y * 0.62f, 0.06f), vestMat);
        for (int i = -1; i <= 1; i++)
        {
            LowPolyKit.CreateBlock("MagPouch", torso, new Vector3(i * 0.13f, -0.16f, halfTorsoZ + 0.07f), Vector3.zero, new Vector3(0.1f, 0.14f, 0.06f), gearMat);
        }
        LowPolyKit.CreateBlock("Collar", torso, new Vector3(0f, halfTorsoY - 0.02f, 0f), Vector3.zero, new Vector3(p.torsoSize.x * 0.62f, 0.08f, p.torsoSize.z * 1.08f), vestMat);

        // On the knee pivots, so they stay on the knee as it bends.
        Vector3 kneePad = new Vector3(p.legSize.x * 1.15f, 0.12f, 0.06f);
        Vector3 kneePadPos = new Vector3(0f, -0.02f, p.legSize.z * 0.5f + 0.02f);
        LowPolyKit.CreateBlock("KneePadL", rig.kneeL, kneePadPos, Vector3.zero, kneePad, padMat);
        LowPolyKit.CreateBlock("KneePadR", rig.kneeR, kneePadPos, Vector3.zero, kneePad, padMat);
    }

    // Built in place every run (unlike the sphere projectiles) so tweaks to its shape reach the existing prefab.
    private static Projectile GetOrCreateArrowPrefab()
    {
        string path = $"{PrefabFolder}/ProjectileArrow.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        go.name = "ProjectileArrow";

        SphereCollider collider = LowPolyKit.GetOrAddComponent<SphereCollider>(go);
        collider.isTrigger = true;
        collider.radius = 0.25f;

        Rigidbody rb = LowPolyKit.GetOrAddComponent<Rigidbody>(go);
        rb.isKinematic = true;
        rb.useGravity = false;

        LowPolyKit.GetOrAddComponent<Projectile>(go);

        Material shaftMat = GetOrCreateMaterial("ArrowShaft", new Color(0.62f, 0.42f, 0.22f), unlit: false);
        // HDR so the head and fletching catch bloom like the other projectiles.
        Material glowMat = GetOrCreateMaterial("ArrowGlow", new Color(2.4f, 2.2f, 1.5f), unlit: true);

        LowPolyKit.CreateBlock("Shaft", go.transform, new Vector3(0f, 0f, -0.15f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.3f, 0.05f), shaftMat, PrimitiveType.Cylinder);
        LowPolyKit.CreateBlock("Head", go.transform, new Vector3(0f, 0f, 0.18f), new Vector3(0f, 45f, 0f), new Vector3(0.12f, 0.06f, 0.12f), glowMat);
        LowPolyKit.CreateBlock("Fletching", go.transform, new Vector3(0f, 0f, -0.42f), Vector3.zero, new Vector3(0.02f, 0.14f, 0.16f), glowMat);

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded).GetComponent<Projectile>();
    }

    private static DogCompanion GetOrCreateDogPrefab()
    {
        string path = $"{PrefabFolder}/Dog.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "Dog";

        Material furMat = GetOrCreateMaterial("DogFur", new Color(0.78f, 0.5f, 0.26f), unlit: false);
        Material darkFurMat = GetOrCreateMaterial("DogFurDark", new Color(0.36f, 0.22f, 0.12f), unlit: false);
        Material lightFurMat = GetOrCreateMaterial("DogFurLight", new Color(0.96f, 0.88f, 0.74f), unlit: false);
        Material noseMat = GetOrCreateMaterial("DogNose", new Color(0.05f, 0.05f, 0.06f), unlit: true);
        Material collarMat = GetOrCreateMaterial("DogCollar", new Color(0.3f, 0.85f, 1.4f), unlit: true);

        GameObject bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(go.transform, false);
        bodyGo.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        Transform body = bodyGo.transform;

        LowPolyKit.CreateBlock("Torso", body, Vector3.zero, Vector3.zero, new Vector3(0.42f, 0.36f, 0.8f), furMat);
        LowPolyKit.CreateBlock("Belly", body, new Vector3(0f, -0.15f, 0.04f), Vector3.zero, new Vector3(0.36f, 0.08f, 0.6f), lightFurMat);
        LowPolyKit.CreateBlock("Collar", body, new Vector3(0f, 0.15f, 0.36f), Vector3.zero, new Vector3(0.36f, 0.1f, 0.14f), collarMat);

        GameObject headGo = new GameObject("Head");
        headGo.transform.SetParent(body, false);
        headGo.transform.localPosition = new Vector3(0f, 0.28f, 0.46f);
        Transform head = headGo.transform;

        LowPolyKit.CreateBlock("Skull", head, Vector3.zero, Vector3.zero, new Vector3(0.36f, 0.34f, 0.36f), furMat);
        LowPolyKit.CreateBlock("Snout", head, new Vector3(0f, -0.06f, 0.24f), Vector3.zero, new Vector3(0.2f, 0.16f, 0.2f), lightFurMat);
        LowPolyKit.CreateBlock("Nose", head, new Vector3(0f, -0.01f, 0.35f), Vector3.zero, new Vector3(0.09f, 0.07f, 0.04f), noseMat);
        LowPolyKit.CreateBlock("EyeL", head, new Vector3(-0.09f, 0.06f, 0.181f), Vector3.zero, new Vector3(0.05f, 0.06f, 0.02f), noseMat);
        LowPolyKit.CreateBlock("EyeR", head, new Vector3(0.09f, 0.06f, 0.181f), Vector3.zero, new Vector3(0.05f, 0.06f, 0.02f), noseMat);
        LowPolyKit.CreateBlock("EarL", head, new Vector3(-0.15f, 0.12f, -0.04f), new Vector3(0f, 0f, 20f), new Vector3(0.08f, 0.2f, 0.14f), darkFurMat);
        LowPolyKit.CreateBlock("EarR", head, new Vector3(0.15f, 0.12f, -0.04f), new Vector3(0f, 0f, -20f), new Vector3(0.08f, 0.2f, 0.14f), darkFurMat);
        // Happy panting tongue and a darker brow so the face reads at phone size.
        Material tongueMat = GetOrCreateMaterial("DogTongue", new Color(0.95f, 0.45f, 0.5f), unlit: false);
        LowPolyKit.CreateBlock("Tongue", head, new Vector3(0.03f, -0.15f, 0.27f), new Vector3(20f, 0f, 0f), new Vector3(0.08f, 0.1f, 0.03f), tongueMat);
        LowPolyKit.CreateBlock("Brow", head, new Vector3(0f, 0.11f, 0.17f), Vector3.zero, new Vector3(0.3f, 0.05f, 0.04f), darkFurMat);

        // Saddle patch along the back and a pale chest, like a shepherd mix.
        LowPolyKit.CreateBlock("Saddle", body, new Vector3(0f, 0.17f, -0.06f), Vector3.zero, new Vector3(0.44f, 0.06f, 0.56f), darkFurMat);
        LowPolyKit.CreateBlock("Chest", body, new Vector3(0f, -0.02f, 0.4f), Vector3.zero, new Vector3(0.34f, 0.28f, 0.04f), lightFurMat);

        GameObject tailGo = new GameObject("Tail");
        tailGo.transform.SetParent(body, false);
        tailGo.transform.localPosition = new Vector3(0f, 0.12f, -0.38f);
        tailGo.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        LowPolyKit.CreateBlock("TailMesh", tailGo.transform, new Vector3(0f, 0.17f, 0f), Vector3.zero, new Vector3(0.08f, 0.34f, 0.08f), darkFurMat);
        LowPolyKit.CreateBlock("TailTip", tailGo.transform, new Vector3(0f, 0.36f, 0f), Vector3.zero, new Vector3(0.09f, 0.08f, 0.09f), lightFurMat);

        Vector3 legSize = new Vector3(0.12f, 0.42f, 0.12f);
        string[] legNames = { "LegFL", "LegFR", "LegBL", "LegBR" };
        Vector3[] legPositions =
        {
            new Vector3(-0.13f, -0.12f, 0.28f),
            new Vector3(0.13f, -0.12f, 0.28f),
            new Vector3(-0.13f, -0.12f, -0.28f),
            new Vector3(0.13f, -0.12f, -0.28f),
        };
        var legs = new Transform[legNames.Length];
        for (int i = 0; i < legs.Length; i++)
        {
            legs[i] = LowPolyKit.CreateHingeLimb(legNames[i], body, legPositions[i], legSize, furMat, out _);
            LowPolyKit.CreateBlock("Paw", legs[i], new Vector3(0f, -legSize.y + 0.03f, 0.03f), Vector3.zero, new Vector3(0.14f, 0.06f, 0.18f), lightFurMat);
        }

        AddTeamRing(go.transform, 0.45f, PlayerRingColor);

        DogCompanion dog = LowPolyKit.GetOrAddComponent<DogCompanion>(go);
        dog.Configure(body, head, tailGo.transform, legs);

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded).GetComponent<DogCompanion>();
    }

    private static Soldier GetOrCreateSoldierPrefab(Projectile bulletPrefab)
    {
        string path = $"{PrefabFolder}/Soldier.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "Soldier";
        // A bit smaller than the hero so the leader stands out in the crowd.
        go.transform.localScale = Vector3.one * 0.85f;

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.36f, 0.55f, 0.82f),
            skin = new Color(0.72f, 0.55f, 0.43f),
            headwear = new Color(0.28f, 0.36f, 0.24f),
            accent = new Color(0.3f, 0.85f, 1f),
            weaponBody = new Color(0.25f, 0.24f, 0.22f),
            weaponDark = new Color(0.1f, 0.1f, 0.1f),
        };

        CharacterFactory.BipedProportions proportions = CharacterFactory.PlayerProportions;
        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, proportions, colors, "Soldier");
        AddHeroGear(rig, proportions, colors);
        rig.rifle.localScale = Vector3.one * 1.25f;
        AddTeamRing(go.transform, 0.55f, PlayerRingColor);
        CharacterFactory.BuildAnimations(go, rig, proportions, "Soldier",
            CharacterFactory.WalkGait, CharacterFactory.RunGait, "Shoot",
            10f, 3f, true, out AnimatorController controller);

        Animator animator = LowPolyKit.GetOrAddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(go.transform, false);
        firePoint.transform.localPosition = go.transform.InverseTransformPoint(rig.muzzle.position);

        Soldier soldier = LowPolyKit.GetOrAddComponent<Soldier>(go);
        soldier.Configure(bulletPrefab, firePoint.transform);

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded).GetComponent<Soldier>();
    }

    private static LootCache GetOrCreateLootCachePrefab()
    {
        string path = $"{PrefabFolder}/LootCache.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "LootCache";

        Material woodMat = GetOrCreateMaterial("LootCrateWood", new Color(0.6f, 0.4f, 0.2f), unlit: false);
        Material bandMat = GetOrCreateMaterial("LootCrateBand", new Color(0.25f, 0.22f, 0.2f), unlit: false);
        Material goldMat = GetOrCreateMaterial("LootGlow", new Color(2.2f, 1.7f, 0.5f), unlit: true);

        LowPolyKit.CreateBlock("Crate", go.transform, new Vector3(0f, 0.35f, 0f), Vector3.zero, new Vector3(0.9f, 0.7f, 0.65f), woodMat);
        LowPolyKit.CreateBlock("Lid", go.transform, new Vector3(0f, 0.74f, 0f), Vector3.zero, new Vector3(0.96f, 0.1f, 0.7f), bandMat);
        LowPolyKit.CreateBlock("BandL", go.transform, new Vector3(-0.3f, 0.35f, 0f), Vector3.zero, new Vector3(0.08f, 0.72f, 0.67f), bandMat);
        LowPolyKit.CreateBlock("BandR", go.transform, new Vector3(0.3f, 0.35f, 0f), Vector3.zero, new Vector3(0.08f, 0.72f, 0.67f), bandMat);
        LowPolyKit.CreateBlock("Gleam", go.transform, new Vector3(0f, 0.82f, 0f), new Vector3(0f, 45f, 0f), new Vector3(0.22f, 0.08f, 0.22f), goldMat);

        // A tall thin light pillar so crates can be spotted from across the arena on a phone.
        Transform beam = LowPolyKit.CreateBlock("Beam", go.transform, new Vector3(0f, 3f, 0f), Vector3.zero, new Vector3(0.16f, 3f, 0.16f), goldMat, PrimitiveType.Cylinder);
        beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameObject ringPivot = new GameObject("ProgressRing");
        ringPivot.transform.SetParent(go.transform, false);
        ringPivot.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        Transform disc = LowPolyKit.CreateBlock("Disc", ringPivot.transform, Vector3.zero, Vector3.zero, new Vector3(1f, 0.005f, 1f), goldMat, PrimitiveType.Cylinder);
        disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        LootCache cache = LowPolyKit.GetOrAddComponent<LootCache>(go);
        cache.Configure(ringPivot.transform, beam);

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded).GetComponent<LootCache>();
    }

    private static SquadCommander BuildSquad(Soldier soldierPrefab, Transform hero)
    {
        GameObject squadGo = FindOrCreateEmpty("Squad");
        // The marker is saved hidden, which GameObject.Find can't see - so it lives under Squad and is cleared with it.
        LowPolyKit.ClearChildren(squadGo);
        GameObject rally = new GameObject("SquadRally");
        rally.transform.SetParent(squadGo.transform, false);
        Material poleMat = GetOrCreateMaterial("RallyPole", new Color(0.3f, 0.25f, 0.2f), unlit: false);
        Material flagMat = GetOrCreateMaterial("RallyFlag", new Color(1f, 0.85f, 0.3f), unlit: true);
        LowPolyKit.CreateBlock("Pole", rally.transform, new Vector3(0f, 0.9f, 0f), Vector3.zero, new Vector3(0.07f, 0.9f, 0.07f), poleMat, PrimitiveType.Cylinder);
        LowPolyKit.CreateBlock("Flag", rally.transform, new Vector3(0.25f, 1.55f, 0f), Vector3.zero, new Vector3(0.45f, 0.3f, 0.03f), flagMat);
        Transform ring = LowPolyKit.CreateBlock("Ring", rally.transform, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(2.6f, 0.005f, 2.6f), flagMat, PrimitiveType.Cylinder);
        ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        SquadCommander squad = LowPolyKit.GetOrAddComponent<SquadCommander>(squadGo);
        squad.Configure(soldierPrefab, hero, rally);
        rally.SetActive(false);
        EditorUtility.SetDirty(squad);
        return squad;
    }

    // Square ring of wall blocks around the core, with a watchtower on each corner and a double
    // gate in the middle of the south side (the side the hero starts on).
    private static void BuildBaseWalls(Wall wallPrefab, Gate gatePrefab, Watchtower towerPrefab, PlacementGrid grid, Transform hero)
    {
        DestroyIfExists("BaseWalls");
        Transform parent = new GameObject("BaseWalls").transform;

        // The core sits on a grid line between four cells, so the ring is centred on that corner.
        Vector2Int coreCell = grid.WorldToCell(Vector3.zero);
        int min = coreCell.x - BaseWallRingCells;
        int max = coreCell.x + BaseWallRingCells - 1;

        GameObject Place(GameObject prefab, Vector3 position)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            SetPrivateField(go.GetComponent<Wall>(), "grid", grid);
            return go;
        }

        for (int x = min; x <= max; x++)
        {
            for (int z = min; z <= max; z++)
            {
                bool onRing = x == min || x == max || z == min || z == max;
                if (!onRing) continue;

                bool corner = (x == min || x == max) && (z == min || z == max);
                // The gate is two cells wide, straddling the core's column on the south side.
                bool gateCell = z == min && (x == coreCell.x - 1 || x == coreCell.x);
                if (gateCell) continue;

                Place(corner ? towerPrefab.gameObject : wallPrefab.gameObject, grid.CellToWorldCenter(new Vector2Int(x, z)));
            }
        }

        Vector3 gatePosition = (grid.CellToWorldCenter(new Vector2Int(coreCell.x - 1, min)) + grid.CellToWorldCenter(new Vector2Int(coreCell.x, min))) * 0.5f;
        GameObject gate = Place(gatePrefab.gameObject, gatePosition);
        SetPrivateField(gate.GetComponent<Gate>(), "hero", hero);
    }

    // Wooden torch with a glowing, flickering flame. No real light - bloom sells the glow and keeps
    // per-pixel lights off the phone's budget.
    private static void AddTorch(Transform parent, Vector3 localPos)
    {
        Material stickMat = GetOrCreateMaterial("TorchStick", new Color(0.35f, 0.22f, 0.12f), unlit: false);
        Material flameMat = GetOrCreateMaterial("TorchFlame", new Color(3f, 1.4f, 0.3f), unlit: true);
        Material emberMat = GetOrCreateMaterial("TorchEmber", new Color(3f, 0.6f, 0.1f), unlit: true);

        LowPolyKit.CreateBlock("TorchStick", parent, localPos, Vector3.zero, new Vector3(0.08f, 0.4f, 0.08f), stickMat);
        LowPolyKit.CreateBlock("TorchEmber", parent, localPos + new Vector3(0f, 0.24f, 0f), new Vector3(0f, 45f, 0f), new Vector3(0.14f, 0.1f, 0.14f), emberMat);
        Transform flame = LowPolyKit.CreateBlock("TorchFlame", parent, localPos + new Vector3(0f, 0.38f, 0f), new Vector3(0f, 45f, 0f), new Vector3(0.12f, 0.24f, 0.12f), flameMat);
        flame.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flame.gameObject.AddComponent<TorchFlicker>();
    }

    // Cottage spots on the north side of the base, away from the south gate. Each fills one grid
    // cell; the rotation picks which way the front door faces.
    private static readonly (Vector3 position, float yaw)[] VillageLayout =
    {
        (new Vector3(-10.5f, 0f, 10.5f), 180f),
        (new Vector3(-4.5f, 0f, 10.5f), 180f),
        (new Vector3(4.5f, 0f, 10.5f), 180f),
        (new Vector3(10.5f, 0f, 10.5f), 180f),
        (new Vector3(-10.5f, 0f, 4.5f), 90f),
        (new Vector3(10.5f, 0f, 4.5f), -90f),
    };

    private static void BuildVillage(Wall[] housePrefabs, PlacementGrid grid)
    {
        DestroyIfExists("Village");
        Transform parent = new GameObject("Village").transform;
        for (int i = 0; i < VillageLayout.Length; i++)
        {
            Wall prefab = housePrefabs[i % housePrefabs.Length];
            if (prefab == null) continue;
            var house = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
            house.transform.SetParent(parent, false);
            house.transform.SetPositionAndRotation(VillageLayout[i].position, Quaternion.Euler(0f, VillageLayout[i].yaw, 0f));
            SetPrivateField(house.GetComponent<Wall>(), "grid", grid);
        }
    }

    private static Wall[] GetOrCreateHousePrefabs()
    {
        return new[]
        {
            GetOrCreateHousePrefab("House", new Color(0.88f, 0.82f, 0.7f), new Color(0.55f, 0.2f, 0.16f)),
            GetOrCreateHousePrefab("HouseAlt", new Color(0.78f, 0.74f, 0.66f), new Color(0.26f, 0.3f, 0.36f)),
        };
    }

    // A timber-framed cottage that fills one grid cell: plaster walls, dark beams, a pitched roof
    // with filled gables, chimney, door and warm glowing windows (they read at night in the trailer).
    // It's a Wall, so the hero can't walk through, shots lob over it and zombies smash it if it's
    // in their way.
    private static Wall GetOrCreateHousePrefab(string name, Color plaster, Color roof)
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = name;

        Material plasterMat = GetOrCreateMaterial($"{name}Plaster", plaster, unlit: false);
        Material roofMat = GetOrCreateMaterial($"{name}Roof", roof, unlit: false);
        Material beamMat = GetOrCreateMaterial("HouseBeam", new Color(0.3f, 0.2f, 0.12f), unlit: false);
        Material stoneMat = GetOrCreateMaterial("WallStoneDark", new Color(0.42f, 0.39f, 0.36f), unlit: false);
        Material windowMat = GetOrCreateMaterial("HouseWindowGlow", new Color(2.4f, 1.5f, 0.6f), unlit: true);

        const float wallTop = 1.85f;
        LowPolyKit.CreateBlock("Foundation", go.transform, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(2.8f, 0.24f, 2.6f), stoneMat);
        LowPolyKit.CreateBlock("Walls", go.transform, new Vector3(0f, 1.04f, 0f), Vector3.zero, new Vector3(2.5f, 1.62f, 2.2f), plasterMat);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                LowPolyKit.CreateBlock("Post", go.transform, new Vector3(x * 1.24f, 1.04f, z * 1.09f), Vector3.zero, new Vector3(0.14f, 1.62f, 0.14f), beamMat);
            }
        }
        LowPolyKit.CreateBlock("BeamBand", go.transform, new Vector3(0f, 1.2f, 0f), Vector3.zero, new Vector3(2.54f, 0.1f, 2.24f), beamMat);

        // Roof: two pitched slabs along X over a rotated block that fills the gable triangles.
        LowPolyKit.CreateBlock("Gable", go.transform, new Vector3(0f, wallTop, 0f), new Vector3(45f, 0f, 0f), new Vector3(2.46f, 1.5f, 1.5f), plasterMat);
        LowPolyKit.CreateBlock("RoofFront", go.transform, new Vector3(0f, wallTop + 0.5f, 0.6f), new Vector3(42f, 0f, 0f), new Vector3(2.9f, 0.12f, 1.75f), roofMat);
        LowPolyKit.CreateBlock("RoofBack", go.transform, new Vector3(0f, wallTop + 0.5f, -0.6f), new Vector3(-42f, 0f, 0f), new Vector3(2.9f, 0.12f, 1.75f), roofMat);
        LowPolyKit.CreateBlock("Ridge", go.transform, new Vector3(0f, wallTop + 1.08f, 0f), Vector3.zero, new Vector3(2.95f, 0.1f, 0.16f), beamMat);
        LowPolyKit.CreateBlock("Chimney", go.transform, new Vector3(0.75f, wallTop + 0.9f, -0.45f), Vector3.zero, new Vector3(0.34f, 1.1f, 0.34f), stoneMat);

        // Front (+Z) door and windows; one more window on each side.
        float front = 1.1f + 0.02f;
        LowPolyKit.CreateBlock("Door", go.transform, new Vector3(-0.45f, 0.75f, front), Vector3.zero, new Vector3(0.6f, 1.05f, 0.05f), beamMat);
        LowPolyKit.CreateBlock("WindowFront", go.transform, new Vector3(0.6f, 1.15f, front), Vector3.zero, new Vector3(0.5f, 0.45f, 0.04f), windowMat);
        LowPolyKit.CreateBlock("WindowFrameFront", go.transform, new Vector3(0.6f, 1.15f, front + 0.01f), Vector3.zero, new Vector3(0.06f, 0.45f, 0.04f), beamMat);
        LowPolyKit.CreateBlock("WindowLeft", go.transform, new Vector3(-1.27f, 1.15f, 0f), Vector3.zero, new Vector3(0.04f, 0.45f, 0.5f), windowMat);
        LowPolyKit.CreateBlock("WindowRight", go.transform, new Vector3(1.27f, 1.15f, 0f), Vector3.zero, new Vector3(0.04f, 0.45f, 0.5f), windowMat);

        LowPolyKit.GetOrAddComponent<Health>(go).Configure(600f);
        LowPolyKit.GetOrAddComponent<HitFlash>(go);
        Wall wall = LowPolyKit.GetOrAddComponent<Wall>(go);
        wall.SetHeight(2.9f);

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded).GetComponent<Wall>();
    }

    private static Gate GetOrCreateGatePrefab()
    {
        string path = $"{PrefabFolder}/Gate.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "Gate";

        Material stoneMat = GetOrCreateMaterial("WallStone", new Color(0.62f, 0.58f, 0.52f), unlit: false);
        Material darkStoneMat = GetOrCreateMaterial("WallStoneDark", new Color(0.42f, 0.39f, 0.36f), unlit: false);
        Material woodMat = GetOrCreateMaterial("GateWood", new Color(0.5f, 0.33f, 0.17f), unlit: false);
        Material ironMat = GetOrCreateMaterial("GateIron", new Color(0.2f, 0.2f, 0.22f), unlit: false);

        // Pillars flush with the neighbouring wall blocks (which end 3m either side of centre).
        LowPolyKit.CreateBlock("PillarL", go.transform, new Vector3(-2.75f, 1.3f, 0f), Vector3.zero, new Vector3(0.6f, 2.6f, 1.6f), stoneMat);
        LowPolyKit.CreateBlock("PillarR", go.transform, new Vector3(2.75f, 1.3f, 0f), Vector3.zero, new Vector3(0.6f, 2.6f, 1.6f), stoneMat);
        LowPolyKit.CreateBlock("CapL", go.transform, new Vector3(-2.75f, 2.7f, 0f), Vector3.zero, new Vector3(0.8f, 0.3f, 1.8f), darkStoneMat);
        LowPolyKit.CreateBlock("CapR", go.transform, new Vector3(2.75f, 2.7f, 0f), Vector3.zero, new Vector3(0.8f, 0.3f, 1.8f), darkStoneMat);
        LowPolyKit.CreateBlock("Lintel", go.transform, new Vector3(0f, 2.45f, 0f), Vector3.zero, new Vector3(5.2f, 0.3f, 0.5f), woodMat);

        Transform BuildDoor(string name, float hingeX, float side)
        {
            GameObject hinge = new GameObject(name);
            hinge.transform.SetParent(go.transform, false);
            hinge.transform.localPosition = new Vector3(hingeX, 0f, 0f);
            LowPolyKit.CreateBlock("Leaf", hinge.transform, new Vector3(side * 1.22f, 1.05f, 0f), Vector3.zero, new Vector3(2.45f, 1.9f, 0.16f), woodMat);
            LowPolyKit.CreateBlock("BandTop", hinge.transform, new Vector3(side * 1.22f, 1.6f, -0.1f), Vector3.zero, new Vector3(2.45f, 0.14f, 0.05f), ironMat);
            LowPolyKit.CreateBlock("BandLow", hinge.transform, new Vector3(side * 1.22f, 0.5f, -0.1f), Vector3.zero, new Vector3(2.45f, 0.14f, 0.05f), ironMat);
            return hinge.transform;
        }

        Transform leftDoor = BuildDoor("DoorL", -2.45f, 1f);
        Transform rightDoor = BuildDoor("DoorR", 2.45f, -1f);

        // A torch on the outer face of each pillar marks the way in.
        AddTorch(go.transform, new Vector3(-2.75f, 1.9f, -0.85f));
        AddTorch(go.transform, new Vector3(2.75f, 1.9f, -0.85f));

        LowPolyKit.GetOrAddComponent<Health>(go).Configure(500f);
        LowPolyKit.GetOrAddComponent<HitFlash>(go);
        Wall wall = LowPolyKit.GetOrAddComponent<Wall>(go);
        wall.SetFootprint(new Vector2(3f, 0.8f));
        wall.SetHeight(2.85f);
        LowPolyKit.GetOrAddComponent<Gate>(go).Configure(leftDoor, rightDoor);

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded).GetComponent<Gate>();
    }

    private static Watchtower GetOrCreateWatchtowerPrefab(Soldier guardPrefab)
    {
        string path = $"{PrefabFolder}/Watchtower.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "Watchtower";

        Material stoneMat = GetOrCreateMaterial("WallStone", new Color(0.62f, 0.58f, 0.52f), unlit: false);
        Material darkStoneMat = GetOrCreateMaterial("WallStoneDark", new Color(0.42f, 0.39f, 0.36f), unlit: false);
        Material woodMat = GetOrCreateMaterial("WallWood", new Color(0.5f, 0.34f, 0.18f), unlit: false);
        Material bannerMat = GetOrCreateMaterial("WatchtowerBanner", new Color(0.3f, 0.75f, 1.4f), unlit: true);

        const float platformY = 3.5f;
        LowPolyKit.CreateBlock("Footing", go.transform, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(2.95f, 0.3f, 2.95f), darkStoneMat);
        LowPolyKit.CreateBlock("Shaft", go.transform, new Vector3(0f, 1.8f, 0f), Vector3.zero, new Vector3(2.3f, 3.1f, 2.3f), stoneMat);
        LowPolyKit.CreateBlock("Platform", go.transform, new Vector3(0f, platformY, 0f), Vector3.zero, new Vector3(3.1f, 0.25f, 3.1f), woodMat);

        // Timber braces under the platform overhang and a dark arrow slit on each face.
        Material slitMat = GetOrCreateMaterial("WatchtowerSlit", new Color(0.06f, 0.05f, 0.05f), unlit: true);
        for (int i = 0; i < 4; i++)
        {
            Quaternion face = Quaternion.Euler(0f, i * 90f, 0f);
            LowPolyKit.CreateBlock("Brace", go.transform, face * new Vector3(0.7f, platformY - 0.35f, 1.25f), new Vector3(0f, i * 90f, 45f), new Vector3(0.12f, 0.6f, 0.12f), woodMat);
            LowPolyKit.CreateBlock("Brace", go.transform, face * new Vector3(-0.7f, platformY - 0.35f, 1.25f), new Vector3(0f, i * 90f, -45f), new Vector3(0.12f, 0.6f, 0.12f), woodMat);
            LowPolyKit.CreateBlock("Slit", go.transform, face * new Vector3(0f, 2.1f, 1.16f), new Vector3(0f, i * 90f, 0f), new Vector3(0.14f, 0.55f, 0.02f), slitMat);
        }
        LowPolyKit.CreateBlock("MortarBand", go.transform, new Vector3(0f, 1.2f, 0f), Vector3.zero, new Vector3(2.32f, 0.05f, 2.32f), GetOrCreateMaterial("WallMortar", new Color(0.3f, 0.28f, 0.26f), unlit: false));
        AddTorch(go.transform, new Vector3(-1.35f, platformY + 0.85f, 1.35f));

        // Low rails and corner merlons keep the guard visible from the overhead camera.
        for (int i = 0; i < 4; i++)
        {
            Quaternion rot = Quaternion.Euler(0f, i * 90f, 0f);
            LowPolyKit.CreateBlock("Rail", go.transform, rot * new Vector3(0f, platformY + 0.3f, 1.45f) , new Vector3(0f, i * 90f, 0f), new Vector3(2.9f, 0.35f, 0.14f), darkStoneMat);
            LowPolyKit.CreateBlock("Merlon", go.transform, rot * new Vector3(1.35f, platformY + 0.45f, 1.35f), Vector3.zero, new Vector3(0.45f, 0.65f, 0.45f), stoneMat);
        }
        LowPolyKit.CreateBlock("Pole", go.transform, new Vector3(1.3f, platformY + 1.2f, -1.3f), Vector3.zero, new Vector3(0.07f, 1.1f, 0.07f), woodMat, PrimitiveType.Cylinder);
        LowPolyKit.CreateBlock("Banner", go.transform, new Vector3(1.6f, platformY + 1.95f, -1.3f), Vector3.zero, new Vector3(0.55f, 0.35f, 0.04f), bannerMat);

        GameObject post = new GameObject("GuardPost");
        post.transform.SetParent(go.transform, false);
        post.transform.localPosition = new Vector3(0f, platformY + 0.13f, 0f);

        LowPolyKit.GetOrAddComponent<Health>(go).Configure(700f);
        LowPolyKit.GetOrAddComponent<HitFlash>(go);
        // Solid up to the platform; the rails above are thin enough for shots to pass.
        LowPolyKit.GetOrAddComponent<Wall>(go).SetHeight(platformY + 0.13f);
        LowPolyKit.GetOrAddComponent<Watchtower>(go).Configure(guardPrefab, post.transform);

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded).GetComponent<Watchtower>();
    }

    private static GameObject GetOrCreateCorePrefab()
    {
        string path = $"{PrefabFolder}/Core.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        go.name = "Core";

        LowPolyKit.SetPrimitiveMesh(go, PrimitiveType.Cube);
        go.transform.localScale = new Vector3(2.4f, 1f, 2.4f);
        Material baseMat = GetOrCreateMaterial("CoreBase", new Color(0.25f, 0.25f, 0.28f), unlit: false);
        go.GetComponent<Renderer>().sharedMaterial = baseMat;

        Material collarMat = GetOrCreateMaterial("CoreCollar", new Color(0.18f, 0.18f, 0.2f), unlit: false);
        // HDR (>1) colours so bloom makes the core read as the glowing thing to protect.
        Material accentMat = GetOrCreateMaterial("CoreAccentPanel", new Color(0.45f, 0.85f, 1.6f), unlit: true);
        Material crystalMat = GetOrCreateEmissiveMaterial("CoreCrystal", new Color(0.45f, 0.9f, 1f), new Color(0.3f, 1.5f, 2f));

        LowPolyKit.CreateBlock("Collar", go.transform, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.62f, 0.32f, 0.62f), collarMat);

        float[] pillarAngles = { 45f, 135f, 225f, 315f };
        foreach (float angle in pillarAngles)
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            LowPolyKit.CreateBlock("Pillar", go.transform, dir * 0.36f + new Vector3(0f, 0.28f, 0f), new Vector3(0f, angle, 0f), new Vector3(0.14f, 0.9f, 0.14f), collarMat);
            LowPolyKit.CreateBlock("PillarLight", go.transform, dir * 0.36f + new Vector3(0f, 0.05f, 0f), new Vector3(0f, angle, 0f), new Vector3(0.16f, 0.06f, 0.16f), accentMat);
        }

        // The pivot carries the spin and size (compensating the base's wide X/Z scale); the gem is a
        // flat-shaded hexagonal bipyramid so each facet catches the light differently.
        GameObject crystal = new GameObject("Crystal");
        crystal.transform.SetParent(go.transform, false);
        crystal.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        crystal.transform.localScale = new Vector3(0.5f, 1.15f, 0.5f);
        crystal.AddComponent<SlowSpinner>().Configure(Vector3.up, 45f);

        GameObject gem = new GameObject("Gem", typeof(MeshFilter), typeof(MeshRenderer));
        gem.transform.SetParent(crystal.transform, false);
        gem.GetComponent<MeshFilter>().sharedMesh = GetOrCreateGemMesh();
        gem.GetComponent<MeshRenderer>().sharedMaterial = crystalMat;

        GameObject glow = new GameObject("CoreGlow");
        glow.transform.SetParent(go.transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        Light glowLight = glow.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = new Color(0.35f, 0.9f, 1f);
        glowLight.range = 9f;
        glowLight.intensity = 4f;
        glowLight.shadows = LightShadows.None;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(500f);
        LowPolyKit.GetOrAddComponent<CoreFeedback>(go).Configure(crystal.transform);

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
    }

    private static void BuildArena()
    {
        DestroyIfExists("Ground");
        DestroyIfExists("EnvironmentDecor");
        DestroyIfExists("Arena");
        Transform arena = new GameObject("Arena").transform;

        Material grassMat = GetOrCreateMaterial("ArenaGrass", new Color(0.44f, 0.66f, 0.3f), unlit: false);
        Material plazaMat = GetOrCreateMaterial("ArenaPlaza", new Color(0.88f, 0.78f, 0.56f), unlit: false);
        Material pathMat = GetOrCreateMaterial("ArenaPath", new Color(0.68f, 0.53f, 0.34f), unlit: false);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(arena, false);
        ground.transform.localScale = new Vector3(14f, 1f, 14f);
        ground.GetComponent<Renderer>().sharedMaterial = grassMat;

        // Floor layers are stacked a few cm apart (path < plaza < portal) to avoid z-fighting.
        CreateFloorPiece("Plaza", PrimitiveType.Cylinder, arena, new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(11f, 0.02f, 11f), plazaMat);

        // Churned-up dirt patches scattered through the treeline hint at where the dead crawl out.
        Random.InitState(4321);
        for (int i = 0; i < 18; i++)
        {
            float angle = i * 20f + Random.Range(-8f, 8f);
            float radius = Random.Range(27f, 32f);
            Vector3 pos = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            float size = Random.Range(1.6f, 2.8f);
            CreateFloorPiece("DirtPatch", PrimitiveType.Cylinder, arena, pos + Vector3.up * 0.015f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), new Vector3(size, 0.02f, size * 0.7f), pathMat);
        }

        BuildDecorRing(arena);
    }

    private static void BuildDecorRing(Transform parent)
    {
        Material trunkMat = GetOrCreateMaterial("TreeTrunk", new Color(0.42f, 0.28f, 0.16f), unlit: false);
        Material canopyMat = GetOrCreateMaterial("TreeCanopy", new Color(0.24f, 0.52f, 0.2f), unlit: false);
        Material canopyAltMat = GetOrCreateMaterial("TreeCanopyAlt", new Color(0.36f, 0.6f, 0.22f), unlit: false);
        Material rockMat = GetOrCreateMaterial("Rock", new Color(0.6f, 0.6f, 0.62f), unlit: false);
        Material crateMat = GetOrCreateMaterial("Crate", new Color(0.6f, 0.42f, 0.2f), unlit: false);

        Random.InitState(1234);

        const int propCount = 44;
        for (int i = 0; i < propCount; i++)
        {
            float angle = i * (360f / propCount) + Random.Range(-3f, 3f);
            float radius = Random.Range(26f, 40f);

            Vector3 localPos = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
            float scale = Random.Range(0.85f, 1.5f);

            int propType = i % 4;
            if (propType <= 1) CreateTree(parent, localPos, scale, trunkMat, propType == 0 ? canopyMat : canopyAltMat);
            else if (propType == 2) CreateRock(parent, localPos, scale, rockMat);
            else CreateCrate(parent, localPos, crateMat);
        }
    }

    private static void CreateFloorPiece(string name, PrimitiveType shape, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(shape);
        piece.name = name;
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localRotation = rotation;
        piece.transform.localScale = scale;
        Renderer renderer = piece.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void CreateTree(Transform parent, Vector3 localPos, float scale, Material trunkMat, Material canopyMat)
    {
        CreateDecorPiece("TreeTrunk", PrimitiveType.Cylinder, parent, localPos + Vector3.up * 0.75f * scale, Quaternion.identity, new Vector3(0.3f, 0.75f, 0.3f) * scale, trunkMat);
        CreateDecorPiece("TreeCanopy", PrimitiveType.Cube, parent, localPos + Vector3.up * 2.3f * scale, Quaternion.Euler(0f, Random.Range(0f, 90f), 45f), Vector3.one * 1.7f * scale, canopyMat);
        CreateDecorPiece("TreeCanopyTop", PrimitiveType.Cube, parent, localPos + Vector3.up * 3.3f * scale, Quaternion.Euler(45f, Random.Range(0f, 90f), 0f), Vector3.one * 1.1f * scale, canopyMat);
    }

    private static void CreateRock(Transform parent, Vector3 localPos, float scale, Material rockMat)
    {
        float size = Random.Range(0.9f, 1.6f) * scale;
        Quaternion rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
        CreateDecorPiece("Rock", PrimitiveType.Cube, parent, localPos + Vector3.up * size * 0.3f, rotation, new Vector3(size, size * 0.7f, size * 0.9f), rockMat);
    }

    private static void CreateCrate(Transform parent, Vector3 localPos, Material crateMat)
    {
        CreateDecorPiece("Crate", PrimitiveType.Cube, parent, localPos + Vector3.up * 0.5f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one, crateMat);
    }

    private static void CreateDecorPiece(string name, PrimitiveType shape, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(shape);
        piece.name = name;
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localRotation = rotation;
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void ConfigureLightingAndAtmosphere()
    {
        Light sun = null;
        foreach (Light light in Object.FindObjectsByType<Light>())
        {
            if (light.type == LightType.Directional)
            {
                sun = light;
                break;
            }
        }
        if (sun == null)
        {
            sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        sun.color = new Color(1f, 0.93f, 0.8f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.55f;
        EditorUtility.SetDirty(sun);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
        RenderSettings.ambientEquatorColor = new Color(0.56f, 0.62f, 0.56f);
        RenderSettings.ambientGroundColor = new Color(0.36f, 0.33f, 0.28f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = HorizonColor;
        RenderSettings.fogStartDistance = 38f;
        RenderSettings.fogEndDistance = 80f;

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = HorizonColor;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 150f;
            UniversalAdditionalCameraData cameraData = cam.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            EditorUtility.SetDirty(cam);
            EditorUtility.SetDirty(cameraData);
        }

        BuildPostProcessing();

        // Mobile portrait is the target platform; lock orientation so builds never rotate to landscape.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
    }

    private static void BuildPostProcessing()
    {
        // Without PostProcessData on the renderer, URP silently skips every volume effect.
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);
        var postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
        if (rendererData != null && postProcessData != null)
        {
            var rendererSo = new SerializedObject(rendererData);
            SerializedProperty postProcessProp = rendererSo.FindProperty("postProcessData");
            if (postProcessProp != null && postProcessProp.objectReferenceValue == null)
            {
                postProcessProp.objectReferenceValue = postProcessData;
                rendererSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(rendererData);
            }
        }
        else
        {
            Debug.LogWarning("Could not assign URP PostProcessData - bloom and color grading will not render.");
        }

        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(PostFxProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostFxProfilePath);
        }

        Bloom bloom = GetOrAddVolumeOverride<Bloom>(profile);
        bloom.threshold.Override(1f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.65f);

        Tonemapping tonemapping = GetOrAddVolumeOverride<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.Neutral);

        ColorAdjustments colorAdjustments = GetOrAddVolumeOverride<ColorAdjustments>(profile);
        colorAdjustments.postExposure.Override(0.15f);
        colorAdjustments.contrast.Override(14f);
        colorAdjustments.saturation.Override(20f);

        Vignette vignette = GetOrAddVolumeOverride<Vignette>(profile);
        vignette.intensity.Override(0.28f);
        vignette.smoothness.Override(0.45f);

        foreach (UnityEngine.Rendering.VolumeComponent component in profile.components) EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        DestroyIfExists("PostFX");
        var volume = new GameObject("PostFX").AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;
    }

    private const string NightPostFxProfilePath = "Assets/Settings/NightPostFX.asset";

    // Night grade for gameplay, layered over the day grade and switched on at runtime by
    // NightLook when the player has Night Mode on. Kept lighter than the trailer's (no grain or
    // depth of field) for phone performance and readability.
    private static void BuildNightLook()
    {
        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(NightPostFxProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            AssetDatabase.CreateAsset(profile, NightPostFxProfilePath);
        }

        Bloom bloom = GetOrAddVolumeOverride<Bloom>(profile);
        bloom.threshold.Override(0.8f);
        bloom.intensity.Override(1.6f);
        bloom.scatter.Override(0.7f);

        GetOrAddVolumeOverride<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);

        ColorAdjustments color = GetOrAddVolumeOverride<ColorAdjustments>(profile);
        color.postExposure.Override(0.55f);
        color.contrast.Override(18f);
        color.saturation.Override(0f);

        GetOrAddVolumeOverride<WhiteBalance>(profile).temperature.Override(-15f);

        SplitToning toning = GetOrAddVolumeOverride<SplitToning>(profile);
        toning.shadows.Override(new Color(0.2f, 0.35f, 0.6f));
        toning.highlights.Override(new Color(1f, 0.72f, 0.45f));
        toning.balance.Override(-10f);

        Vignette vignette = GetOrAddVolumeOverride<Vignette>(profile);
        vignette.intensity.Override(0.34f);
        vignette.smoothness.Override(0.5f);

        foreach (UnityEngine.Rendering.VolumeComponent component in profile.components) EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        DestroyIfExists("PostFXNight");
        var volume = new GameObject("PostFXNight").AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.priority = 2f;
        volume.weight = 0f;
        volume.sharedProfile = profile;

        Light sun = null;
        foreach (Light light in Object.FindObjectsByType<Light>())
        {
            if (light.type == LightType.Directional) sun = light;
        }

        DestroyIfExists("NightLook");
        new GameObject("NightLook").AddComponent<NightLook>().Configure(sun, Camera.main, volume);
    }

    private static T GetOrAddVolumeOverride<T>(UnityEngine.Rendering.VolumeProfile profile) where T : UnityEngine.Rendering.VolumeComponent
    {
        if (profile.TryGet(out T component)) return component;

        component = profile.Add<T>(true);
        component.name = typeof(T).Name;
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    private static void EnsureEventSystem()
    {
        GameObject go = GameObject.Find("EventSystem");
        if (go == null)
        {
            go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
        else if (go.GetComponent<InputSystemUIInputModule>() == null)
        {
            go.AddComponent<InputSystemUIInputModule>();
        }
    }

    private static Transform EnsureCanvas()
    {
        GameObject go = GameObject.Find("UICanvas");
        if (go == null)
        {
            go = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        }

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // Portrait phone layout: lock to width so cards always span the screen edge to edge.
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0f;

        return go.transform;
    }

    private static void BuildGameplayUI(EnemySpawner spawner, Health coreHealth, TowerPlacer placer, PlayerShooter shooter, DogCompanion dogPrefab, SquadCommander squad)
    {
        // Rebuilt from scratch so stale elements from older layouts never linger.
        DestroyIfExists("UICanvas");
        EnsureEventSystem();
        Transform canvas = EnsureCanvas();

        BuildTouchControls(canvas, placer);

        RectTransform safeArea = CreateRect("SafeArea", canvas, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        safeArea.gameObject.AddComponent<SafeAreaFitter>();

        BuildHud(canvas, safeArea, spawner, coreHealth);
        BuildTowerShop(safeArea, placer);
        spawner.SetUpgradeDraft(BuildUpgradeDraft(canvas));
        BuildSquadHud(safeArea, squad, shooter != null ? shooter.transform : null);
        BuildHeroShop(canvas, safeArea, shooter, dogPrefab, placer, squad);
        BuildViewModeHud(safeArea, placer);
        EditorUtility.SetDirty(spawner);
    }

    private static void BuildTouchControls(Transform canvas, TowerPlacer placer)
    {
        RectTransform zone = CreateRect("TouchZone", canvas, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AddImage(zone, Color.clear, raycast: true);

        Vector2 center = new Vector2(0.5f, 0.5f);
        RectTransform joystickBase = CreateRect("JoystickBase", zone, center, center, center, Vector2.zero, new Vector2(260f, 260f));
        AddImage(joystickBase, new Color(1f, 1f, 1f, 0.18f), CircleSprite);
        RectTransform joystickKnob = CreateRect("JoystickKnob", joystickBase, center, center, center, Vector2.zero, new Vector2(120f, 120f));
        AddImage(joystickKnob, new Color(1f, 1f, 1f, 0.65f), CircleSprite);

        zone.gameObject.AddComponent<TouchControls>().Configure(placer, joystickBase, joystickKnob);
    }

    private static void BuildHud(Transform canvas, RectTransform safeArea, EnemySpawner spawner, Health coreHealth)
    {
        Vector2 topCenter = new Vector2(0.5f, 1f);
        Vector2 topLeft = new Vector2(0f, 1f);
        Vector2 midLeft = new Vector2(0f, 0.5f);

        Text waveText = CreateText(safeArea, "WaveText", "WAVE 1", 72, Color.white, TextAnchor.MiddleCenter, topCenter, new Vector2(0f, -16f), new Vector2(520f, 90f));
        waveText.fontStyle = FontStyle.Bold;
        AddTextShadow(waveText);

        Text enemiesText = CreateText(safeArea, "EnemiesText", "", 32, new Color(0.92f, 0.94f, 0.96f), TextAnchor.MiddleCenter, topCenter, new Vector2(0f, -104f), new Vector2(700f, 44f));
        AddTextShadow(enemiesText);

        RectTransform hpBar = CreateRect("CoreHpBar", safeArea, topCenter, topCenter, topCenter, new Vector2(0f, -160f), new Vector2(640f, 48f));
        AddImage(hpBar, PanelColor, RoundedSprite);
        RectTransform hpFill = CreateRect("Fill", hpBar, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        hpFill.offsetMin = new Vector2(6f, 6f);
        hpFill.offsetMax = new Vector2(-6f, -6f);
        AddImage(hpFill, new Color(0.3f, 0.9f, 1f), RoundedSprite);
        Text coreHpText = CreateFillText(hpBar, "CoreHpText", 26, Color.white, TextAnchor.MiddleCenter);
        coreHpText.fontStyle = FontStyle.Bold;
        AddTextShadow(coreHpText);

        RectTransform scrapPill = CreateRect("ScrapPill", safeArea, topLeft, topLeft, topLeft, new Vector2(24f, -24f), new Vector2(230f, 80f));
        AddImage(scrapPill, PanelColor, RoundedSprite);
        RectTransform coin = CreateRect("CoinIcon", scrapPill, midLeft, midLeft, midLeft, new Vector2(16f, 0f), new Vector2(48f, 48f));
        Image coinImage = AddImage(coin, ScrapGold, CircleSprite);
        Text scrapText = CreateText(scrapPill, "ScrapText", "0", 44, ScrapGold, TextAnchor.MiddleLeft, midLeft, new Vector2(76f, 0f), new Vector2(150f, 64f));
        scrapText.fontStyle = FontStyle.Bold;

        // Hero HP sits right under the scrap counter; the respawn countdown is centred on screen.
        RectTransform heroBar = CreateRect("HeroHpBar", safeArea, topLeft, topLeft, topLeft, new Vector2(24f, -116f), new Vector2(230f, 40f));
        AddImage(heroBar, PanelColor, RoundedSprite);
        RectTransform heroFill = CreateRect("Fill", heroBar, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        heroFill.offsetMin = new Vector2(5f, 5f);
        heroFill.offsetMax = new Vector2(-5f, -5f);
        AddImage(heroFill, new Color(0.35f, 0.9f, 0.4f), RoundedSprite);
        Text heroHpText = CreateFillText(heroBar, "HeroHpText", 24, Color.white, TextAnchor.MiddleCenter);
        heroHpText.fontStyle = FontStyle.Bold;
        AddTextShadow(heroHpText);

        Text respawnText = CreateText(safeArea, "RespawnText", "", 64, new Color(1f, 0.4f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1000f, 200f));
        respawnText.fontStyle = FontStyle.Bold;
        AddTextShadow(respawnText);
        respawnText.gameObject.SetActive(false);
        heroBar.gameObject.AddComponent<HeroHealthBar>().Configure(heroFill, heroHpText, respawnText);

        // Game over sits on the canvas (not the safe area) so its dimmer covers the whole screen.
        RectTransform gameOverPanel = CreateRect("GameOverPanel", canvas, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AddImage(gameOverPanel, new Color(0f, 0f, 0f, 0.75f), raycast: true);

        Vector2 center = new Vector2(0.5f, 0.5f);
        RectTransform card = CreateRect("GameOverCard", gameOverPanel, center, center, center, Vector2.zero, new Vector2(900f, 640f));
        AddImage(card, new Color(0.08f, 0.09f, 0.11f, 0.97f), RoundedSprite);

        Text titleText = CreateText(card, "GameOverText", "CORE DESTROYED", 72, new Color(1f, 0.3f, 0.25f), TextAnchor.MiddleCenter, topCenter, new Vector2(0f, -60f), new Vector2(860f, 100f));
        titleText.fontStyle = FontStyle.Bold;
        Text statsText = CreateText(card, "GameOverStatsText", "", 40, Color.white, TextAnchor.MiddleCenter, topCenter, new Vector2(0f, -180f), new Vector2(860f, 60f));

        Vector2 bottomCenter = new Vector2(0.5f, 0f);
        Button restartButton = CreateMenuButton(card, "RestartButton", "PLAY AGAIN", bottomCenter, new Vector2(0f, 200f), new Vector2(560f, 130f), new Color(0.3f, 0.82f, 0.4f));
        restartButton.gameObject.AddComponent<RestartButton>();
        Button menuButton = CreateMenuButton(card, "ReturnToMenuButton", "MENU", bottomCenter, new Vector2(0f, 50f), new Vector2(560f, 110f), new Color(0.85f, 0.87f, 0.9f));
        menuButton.gameObject.AddComponent<ReturnToMenuButton>();

        GameHUD hud = safeArea.gameObject.AddComponent<GameHUD>();
        hud.Configure(spawner, coreHealth, scrapText, waveText, enemiesText, coreHpText, hpFill, scrapPill, coinImage, gameOverPanel.gameObject, statsText);
    }

    private static void BuildTowerShop(RectTransform safeArea, TowerPlacer placer)
    {
        Vector2 bottomCenter = new Vector2(0.5f, 0f);
        RectTransform row = CreateRect("TowerShopPanel", safeArea, bottomCenter, bottomCenter, bottomCenter, new Vector2(0f, 24f), new Vector2(1040f, 260f));

        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TowerDefinition[] defs = placer.Definitions ?? new TowerDefinition[0];
        var buttonViews = new TowerShopButtonView[defs.Length];
        for (int i = 0; i < defs.Length; i++)
        {
            buttonViews[i] = CreateTowerCard(row, i, defs[i]);
        }

        Text hintText = CreateText(safeArea, "PlacementHint", "Tap the ground to build  ·  tap the card again to cancel", 32, new Color(1f, 0.92f, 0.4f), TextAnchor.MiddleCenter, bottomCenter, new Vector2(0f, 300f), new Vector2(1040f, 50f));
        AddTextShadow(hintText);

        TowerShopUI shopUi = row.gameObject.AddComponent<TowerShopUI>();
        shopUi.Configure(placer, buttonViews, hintText.gameObject);
    }

    private static void BuildHeroShop(Transform canvas, RectTransform safeArea, PlayerShooter shooter, DogCompanion dogPrefab, TowerPlacer placer, SquadCommander squad)
    {
        Vector2 topRight = new Vector2(1f, 1f);
        Button openButton = CreateMenuButton(safeArea, "ShopButton", "SHOP", topRight, new Vector2(-24f, -24f), new Vector2(200f, 80f), new Color(1f, 0.62f, 0.2f), 40);

        Vector2 center = new Vector2(0.5f, 0.5f);
        RectTransform overlay = CreateRect("HeroShopPanel", canvas, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
        AddImage(overlay, new Color(0.02f, 0.03f, 0.05f, 0.85f), raycast: true);

        Text header = CreateText(overlay, "HeaderText", "SHOP", 76, ScrapGold, TextAnchor.MiddleCenter, center, new Vector2(0f, 640f), new Vector2(1040f, 100f));
        header.fontStyle = FontStyle.Bold;
        AddTextShadow(header);

        RectTransform scrapPill = CreateRect("ShopScrapPill", overlay, center, center, center, new Vector2(0f, 535f), new Vector2(260f, 76f));
        AddImage(scrapPill, PanelColor, RoundedSprite);
        Vector2 midLeft = new Vector2(0f, 0.5f);
        RectTransform coin = CreateRect("CoinIcon", scrapPill, midLeft, midLeft, midLeft, new Vector2(18f, 0f), new Vector2(46f, 46f));
        AddImage(coin, ScrapGold, CircleSprite);
        Text scrapText = CreateText(scrapPill, "ScrapText", "0", 42, ScrapGold, TextAnchor.MiddleLeft, midLeft, new Vector2(78f, 0f), new Vector2(170f, 64f));
        scrapText.fontStyle = FontStyle.Bold;

        var views = new ShopItemView[3];
        for (int i = 0; i < views.Length; i++)
        {
            views[i] = CreateShopItemCard(overlay, i, new Vector2(0f, 300f - i * 280f));
        }

        Button closeButton = CreateMenuButton(overlay, "CloseButton", "BACK", center, new Vector2(0f, -540f), new Vector2(520f, 120f), new Color(0.85f, 0.87f, 0.9f));

        // Lives on the always-visible button: a script on the hidden panel would never wake up to wire the button.
        HeroShopUI shop = openButton.gameObject.AddComponent<HeroShopUI>();
        shop.Configure(openButton, overlay.gameObject, closeButton, scrapText, views, shooter, dogPrefab, placer, squad);
    }

    private static void BuildViewModeHud(RectTransform safeArea, TowerPlacer placer)
    {
        Vector2 topRight = new Vector2(1f, 1f);
        Vector2 bottomRight = new Vector2(1f, 0f);
        Vector2 center = new Vector2(0.5f, 0.5f);

        Button toggle = CreateMenuButton(safeArea, "ViewToggleButton", "FPS", topRight, new Vector2(-24f, -120f), new Vector2(200f, 80f), new Color(0.7f, 0.5f, 1f), 40);

        RectTransform fire = CreateRect("FireButton", safeArea, bottomRight, bottomRight, new Vector2(0.5f, 0.5f), new Vector2(-170f, 230f), new Vector2(250f, 250f));
        AddImage(fire, new Color(0.95f, 0.3f, 0.25f, 0.85f), CircleSprite, raycast: true);
        Text fireLabel = CreateFillText(fire, "Label", 52, Color.white, TextAnchor.MiddleCenter);
        fireLabel.text = "FIRE";
        fireLabel.fontStyle = FontStyle.Bold;
        AddTextShadow(fireLabel);
        fire.gameObject.AddComponent<FireButton>();

        // Four ticks around a dot, centred on screen where the camera looks.
        RectTransform crosshair = CreateRect("Crosshair", safeArea.parent, center, center, center, Vector2.zero, new Vector2(80f, 80f));
        // Just above the touch surface, so shop/upgrade overlays draw over it.
        crosshair.SetSiblingIndex(safeArea.GetSiblingIndex());
        Color tickColor = new Color(1f, 1f, 1f, 0.9f);
        AddImage(CreateRect("Dot", crosshair, center, center, center, Vector2.zero, new Vector2(8f, 8f)), tickColor);
        AddImage(CreateRect("Top", crosshair, center, center, center, new Vector2(0f, 24f), new Vector2(5f, 18f)), tickColor);
        AddImage(CreateRect("Bottom", crosshair, center, center, center, new Vector2(0f, -24f), new Vector2(5f, 18f)), tickColor);
        AddImage(CreateRect("Left", crosshair, center, center, center, new Vector2(-24f, 0f), new Vector2(18f, 5f)), tickColor);
        AddImage(CreateRect("Right", crosshair, center, center, center, new Vector2(24f, 0f), new Vector2(18f, 5f)), tickColor);

        Transform towerShop = safeArea.Find("TowerShopPanel");
        ViewModeHUD hud = toggle.gameObject.AddComponent<ViewModeHUD>();
        hud.Configure(toggle, toggle.GetComponentInChildren<Text>(), placer,
            new[] { towerShop != null ? towerShop.gameObject : null },
            new[] { fire.gameObject, crosshair.gameObject });
        fire.gameObject.SetActive(false);
        crosshair.gameObject.SetActive(false);
    }

    private static void BuildSquadHud(RectTransform safeArea, SquadCommander squad, Transform hero)
    {
        Vector2 bottomRight = new Vector2(1f, 0f);
        Vector2 bottomCenter = new Vector2(0.5f, 0f);

        Button squadButton = CreateMenuButton(safeArea, "SquadButton", "SQUAD", bottomRight, new Vector2(-24f, 330f), new Vector2(290f, 100f), new Color(0.35f, 0.6f, 0.95f), 38);

        Text hint = CreateText(safeArea, "SquadHint", "Tap the map to send your squad", 38, new Color(1f, 0.92f, 0.4f), TextAnchor.MiddleCenter, bottomCenter, new Vector2(0f, 600f), new Vector2(1040f, 56f));
        AddTextShadow(hint);

        Button hireButton = CreateMenuButton(safeArea, "HireGuardButton", "HIRE GUARD", bottomCenter, new Vector2(0f, 460f), new Vector2(560f, 110f), new Color(0.3f, 0.82f, 0.4f), 40);

        SquadHUD hud = safeArea.gameObject.AddComponent<SquadHUD>();
        hud.Configure(squad, squadButton, squadButton.GetComponentInChildren<Text>(), hint.gameObject,
            hero, hireButton, hireButton.GetComponentInChildren<Text>());
        squadButton.gameObject.SetActive(false);
        hireButton.gameObject.SetActive(false);
        hint.gameObject.SetActive(false);
    }

    private static ShopItemView CreateShopItemCard(Transform parent, int index, Vector2 position)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        RectTransform card = CreateRect($"ShopItem_{index}", parent, center, center, center, position, new Vector2(960f, 250f));
        Image background = AddImage(card, new Color(0.12f, 0.13f, 0.17f, 0.98f), RoundedSprite, raycast: true);

        Vector2 midLeft = new Vector2(0f, 0.5f);
        RectTransform icon = CreateRect("Icon", card, midLeft, midLeft, midLeft, new Vector2(30f, 0f), new Vector2(170f, 170f));
        Image iconImage = AddImage(icon, Color.white, RoundedSprite);
        Text iconLetter = CreateFillText(icon, "IconLetter", 80, new Color(0f, 0f, 0f, 0.6f), TextAnchor.MiddleCenter);
        iconLetter.fontStyle = FontStyle.Bold;

        Vector2 topLeft = new Vector2(0f, 1f);
        Text title = CreateText(card, "Title", "", 46, Color.white, TextAnchor.UpperLeft, topLeft, new Vector2(225f, -30f), new Vector2(480f, 60f));
        title.fontStyle = FontStyle.Bold;

        Text description = CreateText(card, "Description", "", 30, new Color(0.82f, 0.85f, 0.9f), TextAnchor.UpperLeft, topLeft, new Vector2(225f, -100f), new Vector2(460f, 130f));
        description.horizontalOverflow = HorizontalWrapMode.Wrap;

        Vector2 midRight = new Vector2(1f, 0.5f);
        Button buyButton = CreateMenuButton(card, "BuyButton", "", midRight, new Vector2(-30f, 0f), new Vector2(230f, 120f), new Color(0.3f, 0.82f, 0.4f), 40);

        return new ShopItemView
        {
            Root = card,
            Button = buyButton,
            Background = background,
            Icon = iconImage,
            IconLetter = iconLetter,
            Title = title,
            Description = description,
            ButtonLabel = buyButton.GetComponentInChildren<Text>(),
            ButtonImage = buyButton.GetComponent<Image>(),
        };
    }

    private static UpgradeDraft BuildUpgradeDraft(Transform canvas)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        RectTransform overlay = CreateRect("UpgradeDraftPanel", canvas, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
        AddImage(overlay, new Color(0.02f, 0.03f, 0.05f, 0.82f), raycast: true);

        Text header = CreateText(overlay, "HeaderText", "WAVE 1 CLEARED", 68, ScrapGold, TextAnchor.MiddleCenter, center, new Vector2(0f, 520f), new Vector2(1040f, 90f));
        header.fontStyle = FontStyle.Bold;
        AddTextShadow(header);
        Text subheader = CreateText(overlay, "SubheaderText", "Choose an upgrade", 38, new Color(0.85f, 0.88f, 0.92f), TextAnchor.MiddleCenter, center, new Vector2(0f, 445f), new Vector2(1040f, 56f));
        AddTextShadow(subheader);

        var views = new UpgradeCardView[3];
        for (int i = 0; i < views.Length; i++)
        {
            views[i] = CreateUpgradeCard(overlay, i, new Vector2(0f, 220f - i * 280f));
        }

        UpgradeDraft draft = overlay.gameObject.AddComponent<UpgradeDraft>();
        draft.Configure(overlay.gameObject, header, views);
        return draft;
    }

    private static UpgradeCardView CreateUpgradeCard(Transform parent, int index, Vector2 position)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        RectTransform card = CreateRect($"UpgradeCard_{index}", parent, center, center, center, position, new Vector2(960f, 250f));
        Image background = AddImage(card, new Color(0.12f, 0.13f, 0.17f, 0.98f), RoundedSprite, raycast: true);
        Button button = card.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;

        Vector2 midLeft = new Vector2(0f, 0.5f);
        RectTransform icon = CreateRect("Icon", card, midLeft, midLeft, midLeft, new Vector2(30f, 0f), new Vector2(180f, 180f));
        Image iconImage = AddImage(icon, Color.white, RoundedSprite);
        Text iconLetter = CreateFillText(icon, "IconLetter", 80, new Color(0f, 0f, 0f, 0.6f), TextAnchor.MiddleCenter);
        iconLetter.fontStyle = FontStyle.Bold;

        Vector2 topLeft = new Vector2(0f, 1f);
        Text title = CreateText(card, "Title", "", 50, Color.white, TextAnchor.UpperLeft, topLeft, new Vector2(240f, -34f), new Vector2(560f, 64f));
        title.fontStyle = FontStyle.Bold;

        Text description = CreateText(card, "Description", "", 34, new Color(0.82f, 0.85f, 0.9f), TextAnchor.UpperLeft, topLeft, new Vector2(240f, -110f), new Vector2(690f, 110f));
        description.horizontalOverflow = HorizontalWrapMode.Wrap;

        Vector2 topRight = new Vector2(1f, 1f);
        Text rarity = CreateText(card, "Rarity", "", 26, Color.white, TextAnchor.UpperRight, topRight, new Vector2(-30f, -30f), new Vector2(200f, 40f));
        rarity.fontStyle = FontStyle.Bold;

        return new UpgradeCardView
        {
            Root = card,
            Button = button,
            Background = background,
            Icon = iconImage,
            IconLetter = iconLetter,
            Title = title,
            Description = description,
            Rarity = rarity,
        };
    }

    private static TowerShopButtonView CreateTowerCard(Transform parent, int index, TowerDefinition def)
    {
        RectTransform card = CreateRect($"TowerCard_{index}", parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(162f, 240f));
        Image background = AddImage(card, new Color(0.1f, 0.11f, 0.14f, 0.92f), RoundedSprite, raycast: true);
        Button button = card.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;

        Vector2 topCenter = new Vector2(0.5f, 1f);
        RectTransform icon = CreateRect("Icon", card, topCenter, topCenter, topCenter, new Vector2(0f, -14f), new Vector2(112f, 112f));
        Image iconImage = AddImage(icon, def.accentColor, RoundedSprite);
        string initial = string.IsNullOrEmpty(def.displayName) ? "?" : def.displayName.Substring(0, 1).ToUpperInvariant();
        Text initialText = CreateFillText(icon, "Initial", 72, new Color(0f, 0f, 0f, 0.55f), TextAnchor.MiddleCenter);
        initialText.text = initial;
        initialText.fontStyle = FontStyle.Bold;

        Vector2 bottomCenter = new Vector2(0.5f, 0f);
        Text label = CreateText(card, "Label", def.displayName, 28, Color.white, TextAnchor.MiddleCenter, bottomCenter, new Vector2(0f, 62f), new Vector2(158f, 40f));
        label.fontStyle = FontStyle.Bold;

        RectTransform coin = CreateRect("CoinIcon", card, bottomCenter, bottomCenter, bottomCenter, new Vector2(-34f, 20f), new Vector2(30f, 30f));
        AddImage(coin, ScrapGold, CircleSprite);
        Text costLabel = CreateText(card, "CostLabel", def.cost.ToString(), 32, ScrapGold, TextAnchor.MiddleLeft, bottomCenter, new Vector2(30f, 14f), new Vector2(90f, 42f));
        costLabel.fontStyle = FontStyle.Bold;

        return new TowerShopButtonView { Button = button, Background = background, Icon = iconImage, Label = label, CostLabel = costLabel };
    }

    private static Sprite RoundedSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    private static Sprite CircleSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return rect;
    }

    private static Image AddImage(RectTransform rect, Color color, Sprite sprite = null, bool raycast = false)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        if (sprite != null)
        {
            image.sprite = sprite;
            if (sprite.border != Vector4.zero)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 0.35f; // Larger rounded corners at the 1080-wide reference size.
            }
        }
        return image;
    }

    private static void AddTextShadow(Text text)
    {
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -3f);
    }

    private static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, TextAnchor alignment, Vector2 anchor, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Text text = go.AddComponent<Text>();
        text.text = content;
        text.font = GetDefaultFont();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Text CreateFillText(Transform parent, string name, int fontSize, Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 8f);
        rect.offsetMax = new Vector2(-8f, -8f);

        Text text = go.AddComponent<Text>();
        text.font = GetDefaultFont();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    private static Material GetOrCreateMaterial(string name, Color color, bool unlit)
    {
        return LowPolyKit.GetOrCreateMaterial(name, color, unlit);
    }

    private static GameObject SaveAndDestroy(GameObject go, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static void SetPrivateField(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"Could not find field '{fieldName}' on {target.GetType().Name}");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetPrivateFloat(Object target, string fieldName, float value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"Could not find field '{fieldName}' on {target.GetType().Name}");
            return;
        }
        prop.floatValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetPrivateInt(Object target, string fieldName, int value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"Could not find field '{fieldName}' on {target.GetType().Name}");
            return;
        }
        prop.intValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetPrivateBool(Object target, string fieldName, bool value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"Could not find field '{fieldName}' on {target.GetType().Name}");
            return;
        }
        prop.boolValue = value;
        so.ApplyModifiedProperties();
    }

    private static void ConfigureProjectileBehavior(Projectile projectile, float speed, float lifetime, Color fxColor, float glow,
        bool homing = false, float splashRadius = 0f, int pierceCount = 0, int chainCount = 0, float chainRange = 4f, bool bullet = false)
    {
        var so = new SerializedObject(projectile);
        so.FindProperty("isBullet").boolValue = bullet;
        so.FindProperty("speed").floatValue = speed;
        so.FindProperty("lifetime").floatValue = lifetime;
        so.FindProperty("homing").boolValue = homing;
        so.FindProperty("splashRadius").floatValue = splashRadius;
        so.FindProperty("pierceCount").intValue = pierceCount;
        so.FindProperty("chainCount").intValue = chainCount;
        so.FindProperty("chainRange").floatValue = chainRange;
        so.FindProperty("fxColor").colorValue = fxColor;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(projectile);

        // HDR body colour so bloom makes shots glow.
        Renderer renderer = projectile.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            renderer.sharedMaterial.color = new Color(fxColor.r * glow, fxColor.g * glow, fxColor.b * glow, 1f);
            EditorUtility.SetDirty(renderer.sharedMaterial);
        }
    }

    // Stretches a round projectile into a thin streak along its flight direction (+Z).
    private static void StyleAsTracer(Projectile projectile, float length, float width)
    {
        projectile.transform.localScale = new Vector3(width, width, length);
        if (projectile.TryGetComponent(out SphereCollider collider)) collider.radius = 0.5f;
        if (projectile.TryGetComponent(out Renderer renderer)) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        PrefabUtility.SavePrefabAsset(projectile.gameObject);
    }

    private static void ConfigureEnemyFeedback(Enemy enemy, Color deathColor, int deathBurstCount, float attackShake, float deathShake)
    {
        var so = new SerializedObject(enemy);
        so.FindProperty("deathColor").colorValue = deathColor;
        so.FindProperty("deathBurstCount").intValue = deathBurstCount;
        so.FindProperty("attackShake").floatValue = attackShake;
        so.FindProperty("deathShake").floatValue = deathShake;
        so.ApplyModifiedProperties();
    }

    private static void ConfigureTower(Tower tower, TowerKind kind, SfxId sound, float pitch)
    {
        var so = new SerializedObject(tower);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        so.FindProperty("fireSound").enumValueIndex = (int)sound;
        so.FindProperty("firePitch").floatValue = pitch;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(tower);
    }

    // Flat team-coloured disc under a character so friend/foe reads instantly at phone size.
    private static void AddTeamRing(Transform character, float radius, Color color)
    {
        Material ringMat = GetOrCreateMaterial(color == PlayerRingColor ? "TeamRingPlayer" : "TeamRingEnemy", color, unlit: true);
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "TeamRing";
        Object.DestroyImmediate(ring.GetComponent<Collider>());
        ring.transform.SetParent(character, false);
        ring.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        ring.transform.localScale = new Vector3(radius * 2f, 0.005f, radius * 2f);
        Renderer renderer = ring.GetComponent<Renderer>();
        renderer.sharedMaterial = ringMat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Mesh GetOrCreateGemMesh()
    {
        const string path = "Assets/Materials/CoreGem.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;

        const int sides = 6;
        const float radius = 0.6f;
        const float girdleHeight = 0.25f;
        Vector3 top = new Vector3(0f, 1f, 0f);
        Vector3 bottom = new Vector3(0f, -0.8f, 0f);

        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();

        // Separate vertices per triangle give hard, flat-shaded facets.
        void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides;
            float a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, girdleHeight, Mathf.Sin(a0) * radius);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, girdleHeight, Mathf.Sin(a1) * radius);
            Vector3 q0 = new Vector3(p0.x, -girdleHeight * 0.4f, p0.z);
            Vector3 q1 = new Vector3(p1.x, -girdleHeight * 0.4f, p1.z);

            AddTriangle(top, p1, p0);
            AddTriangle(p0, p1, q1);
            AddTriangle(p0, q1, q0);
            AddTriangle(bottom, q0, q1);
        }

        mesh = new Mesh { name = "CoreGem" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Material GetOrCreateEmissiveMaterial(string name, Color baseColor, Color emission)
    {
        Material mat = GetOrCreateMaterial(name, baseColor, unlit: false);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emission);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Saved as an asset and referenced from the scene so the particle shader is included in builds.
    private static Material GetOrCreateParticleMaterial()
    {
        string path = $"{MaterialFolder}/FxParticles.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    private static void DestroyIfExists(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) Object.DestroyImmediate(go);
    }

    private static GameObject FindOrCreateEmpty(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) go = new GameObject(name);
        return go;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        string folderName = System.IO.Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
