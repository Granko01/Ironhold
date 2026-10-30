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
        "Ground", "Core", "Player", "GameManager", "EnemySpawner", "TowerPlacer", "PlacementGrid", "EventSystem", "UICanvas", "EnvironmentDecor", "Arena", "PostFX", "Directional Light"
    };

    private const string PostFxProfilePath = "Assets/Settings/GameplayPostFX.asset";

    private static readonly Vector3[] SpawnPositions =
    {
        new Vector3(20f, 0f, 20f),
        new Vector3(-20f, 0f, 20f),
        new Vector3(20f, 0f, -20f),
        new Vector3(-20f, 0f, -20f),
    };

    // Shared by the arena fog and the camera clear colour so the world fades into the background.
    private static readonly Color HorizonColor = new Color(0.56f, 0.74f, 0.66f);
    private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.09f, 0.8f);
    private static readonly Color ScrapGold = new Color(1f, 0.82f, 0.3f);

    private static readonly Color EnemyRingColor = new Color(0.95f, 0.22f, 0.18f);
    private static readonly Color PlayerRingColor = new Color(0.25f, 0.75f, 1f);

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

        ConfigureProjectileBehavior(bulletProjectile, speed: 20f, lifetime: 3f, fxColor: new Color(1f, 0.85f, 0.2f), glow: 2.5f);
        ConfigureProjectileBehavior(cannonBallProjectile, speed: 14f, lifetime: 3f, fxColor: new Color(1f, 0.55f, 0.15f), glow: 2f, splashRadius: 2.5f);
        ConfigureProjectileBehavior(sniperProjectile, speed: 45f, lifetime: 1.5f, fxColor: new Color(0.85f, 1f, 0.5f), glow: 3f, pierceCount: 3);
        ConfigureProjectileBehavior(teslaBoltProjectile, speed: 30f, lifetime: 2f, fxColor: new Color(0.4f, 0.85f, 1f), glow: 3f, chainCount: 4, chainRange: 4.5f);
        ConfigureProjectileBehavior(missileProjectile, speed: 12f, lifetime: 5f, fxColor: new Color(1f, 0.45f, 0.1f), glow: 2.5f, homing: true, splashRadius: 2f);
        Enemy enemyPrefab = GetOrCreateEnemyPrefab();
        Enemy runnerPrefab = GetOrCreateRunnerPrefab();
        Enemy brutePrefab = GetOrCreateBrutePrefab();
        Enemy spitterPrefab = GetOrCreateSpitterPrefab();
        Enemy bomberPrefab = GetOrCreateBomberPrefab();
        Enemy engineerPrefab = GetOrCreateEngineerPrefab();
        GameObject wallPrefab = GetOrCreateWallPrefab();
        GameObject bossPrefab = GetOrCreateBossPrefab();

        Tower gunTower = GetOrCreateTowerPrefab("TowerGun", TowerStyle.Gun, new Color(0.56f, 0.56f, 0.6f), GunAccent, 1f, 8f, 3f, 8f, bulletProjectile);
        Tower cannonTower = GetOrCreateTowerPrefab("TowerCannon", TowerStyle.Cannon, new Color(0.56f, 0.54f, 0.52f), CannonAccent, 1.3f, 6f, 1f, 30f, cannonBallProjectile);
        Tower sniperTower = GetOrCreateTowerPrefab("TowerSniper", TowerStyle.Sniper, new Color(0.52f, 0.56f, 0.52f), SniperAccent, 0.85f, 14f, 0.6f, 45f, sniperProjectile);
        Tower teslaTower = GetOrCreateTowerPrefab("TowerTesla", TowerStyle.Tesla, new Color(0.52f, 0.55f, 0.6f), TeslaAccent, 1f, 6.5f, 2f, 8f, teslaBoltProjectile);
        Tower missileTower = GetOrCreateTowerPrefab("TowerMissile", TowerStyle.Missile, new Color(0.56f, 0.53f, 0.5f), MissileAccent, 1.15f, 11f, 0.5f, 55f, missileProjectile);

        ConfigureTowerSound(gunTower, SfxId.Shoot, 1f);
        ConfigureTowerSound(cannonTower, SfxId.Shoot, 0.5f);
        ConfigureTowerSound(sniperTower, SfxId.Shoot, 0.7f);
        ConfigureTowerSound(teslaTower, SfxId.Shoot, 1.8f);
        ConfigureTowerSound(missileTower, SfxId.Shoot, 0.4f);

        GameObject playerPrefabGo = GetOrCreatePlayerPrefab(bulletProjectile);
        GameObject corePrefabGo = GetOrCreateCorePrefab();

        BuildArena();
        ConfigureLightingAndAtmosphere();
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

        GameObject spawnerGo = GameObject.Find("EnemySpawner");
        if (spawnerGo == null)
        {
            spawnerGo = new GameObject("EnemySpawner");
            spawnerGo.AddComponent<EnemySpawner>();

            for (int i = 0; i < SpawnPositions.Length; i++)
            {
                GameObject sp = new GameObject($"SpawnPoint_{i}");
                sp.transform.SetParent(spawnerGo.transform);
                sp.transform.position = SpawnPositions[i];
            }
        }

        // Applied on every build (not only when the spawner is first created) so roster and
        // balance changes here reach scenes that were built before.
        var spawnPoints = new Transform[spawnerGo.transform.childCount];
        for (int i = 0; i < spawnPoints.Length; i++) spawnPoints[i] = spawnerGo.transform.GetChild(i);

        EnemySpawner enemySpawner = spawnerGo.GetComponent<EnemySpawner>();
        enemySpawner.Configure(new[]
        {
            new EnemySpawnEntry { prefab = enemyPrefab, threatCost = 1, firstWave = 1, weight = 4f },
            new EnemySpawnEntry { prefab = runnerPrefab, threatCost = 1, firstWave = 2, weight = 2f },
            new EnemySpawnEntry { prefab = spitterPrefab, threatCost = 2, firstWave = 3, weight = 1.5f },
            new EnemySpawnEntry { prefab = bomberPrefab, threatCost = 2, firstWave = 4, weight = 1.2f },
            new EnemySpawnEntry { prefab = brutePrefab, threatCost = 4, firstWave = 6, weight = 1f },
            new EnemySpawnEntry { prefab = engineerPrefab, threatCost = 2, firstWave = 7, weight = 1f },
        }, bossPrefab != null ? bossPrefab.GetComponent<Enemy>() : null, core.transform, spawnPoints);
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
        SetTowerDefinitions(towerPlacer, new (string name, int cost, Tower prefab, Color accentColor)[]
        {
            ("Gun", 50, gunTower, GunAccent),
            ("Cannon", 120, cannonTower, CannonAccent),
            ("Sniper", 150, sniperTower, SniperAccent),
            ("Tesla", 90, teslaTower, TeslaAccent),
            ("Missile", 180, missileTower, MissileAccent),
        });

        BuildGameplayUI(enemySpawner, coreHealth, towerPlacer);

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

    [MenuItem("Tools/Iron Hold/3) Build Main Menu Scene")]
    public static void BuildMainMenuScene()
    {
        EnsureFolder(SceneFolder);

        string previousScenePath = EditorSceneManager.GetActiveScene().path;

        Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        EnsureEventSystem();
        Transform canvasTransform = EnsureCanvas();

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasTransform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f);

        Text title = CreateText(canvasTransform, "TitleText", "IRON HOLD", 140, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(1040f, 180f));
        title.fontStyle = FontStyle.Bold;
        CreateText(canvasTransform, "SubtitleText", "Defend the Core", 48, new Color(0.75f, 0.78f, 0.85f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(1000f, 70f));

        Button playButton = CreateMenuButton(canvasTransform, "PlayButton", "PLAY", new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(600f, 150f), new Color(0.3f, 0.82f, 0.4f), 60);
        Button quitButton = CreateMenuButton(canvasTransform, "QuitButton", "QUIT", new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(600f, 120f));

        GameObject controllerGo = new GameObject("MainMenuController");
        MainMenuController controller = controllerGo.AddComponent<MainMenuController>();
        controller.Configure(playButton, quitButton);

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

        if (!string.IsNullOrEmpty(previousScenePath))
        {
            EditorSceneManager.OpenScene(previousScenePath);
        }

        Debug.Log($"Main menu scene built at {MainMenuScenePath} and registered in Build Settings.");
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
        collider.center = new Vector3(0f, 0.85f, 0f);
        collider.radius = 0.4f;
        collider.height = 1.7f;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(30f);
        Enemy enemy = LowPolyKit.GetOrAddComponent<Enemy>(go);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.58f, 0.22f, 0.2f),
            skin = new Color(0.55f, 0.45f, 0.4f),
            headwear = new Color(0.14f, 0.14f, 0.15f),
            accent = new Color(1f, 0.2f, 0.15f),
            weaponBody = new Color(0.2f, 0.2f, 0.22f),
            weaponDark = new Color(0.07f, 0.07f, 0.08f),
        };

        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, CharacterFactory.GruntProportions, colors, "Grunt");
        ConfigureEnemyFeedback(enemy, colors.accent, 14, 0f, 0f);
        AddTeamRing(go.transform, 0.6f, EnemyRingColor);
        CharacterFactory.BuildAnimations(go, rig, CharacterFactory.GruntProportions, "Grunt",
            CharacterFactory.WalkGait, null, "Attack",
            45f, 14f, false, out AnimatorController controller);

        Animator animator = LowPolyKit.GetOrAddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        GameObject prefab = LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
        return prefab.GetComponent<Enemy>();
    }

    private static Enemy GetOrCreateRunnerPrefab()
    {
        string path = $"{PrefabFolder}/EnemyRunner.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "EnemyRunner";

        CapsuleCollider collider = LowPolyKit.GetOrAddComponent<CapsuleCollider>(go);
        collider.center = new Vector3(0f, 0.68f, 0f);
        collider.radius = 0.28f;
        collider.height = 1.36f;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(16f);
        Enemy enemy = LowPolyKit.GetOrAddComponent<Enemy>(go);
        SetPrivateFloat(enemy, "moveSpeed", 6f);
        SetPrivateFloat(enemy, "attackRange", 1f);
        SetPrivateFloat(enemy, "attackDamage", 6f);
        SetPrivateInt(enemy, "scrapReward", 8);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.88f, 0.5f, 0.18f),
            skin = new Color(0.62f, 0.5f, 0.42f),
            headwear = new Color(0.12f, 0.12f, 0.13f),
            accent = new Color(0.9f, 0.15f, 0.1f),
            weaponBody = new Color(0.2f, 0.2f, 0.22f),
            weaponDark = new Color(0.07f, 0.07f, 0.08f),
        };

        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, CharacterFactory.RunnerProportions, colors, "Runner", hasWeapon: false);
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

        CharacterFactory.BuildAnimations(go, rig, CharacterFactory.RunnerProportions, "Runner",
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
        collider.center = new Vector3(0f, 1.1f, 0f);
        collider.radius = 0.55f;
        collider.height = 2.3f;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(120f);
        Enemy enemy = LowPolyKit.GetOrAddComponent<Enemy>(go);
        SetPrivateFloat(enemy, "moveSpeed", 1.6f);
        SetPrivateFloat(enemy, "attackRange", 1.6f);
        SetPrivateFloat(enemy, "attackDamage", 30f);
        SetPrivateInt(enemy, "scrapReward", 35);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.45f, 0.22f, 0.45f),
            skin = new Color(0.55f, 0.42f, 0.45f),
            headwear = new Color(0.1f, 0.1f, 0.1f),
            accent = new Color(0.9f, 0.4f, 0.1f),
            weaponBody = new Color(0.18f, 0.18f, 0.19f),
            weaponDark = new Color(0.08f, 0.08f, 0.08f),
        };

        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, CharacterFactory.BruteProportions, colors, "Brute", hasWeapon: false);
        ConfigureEnemyFeedback(enemy, colors.accent, 26, 0.15f, 0.2f);
        AddTeamRing(go.transform, 0.95f, EnemyRingColor);

        Material pauldronMat = GetOrCreateMaterial("BruteAccent", colors.accent, unlit: true);
        Vector3 pauldronSize = new Vector3(CharacterFactory.BruteProportions.armSize.x * 1.6f, 0.18f, CharacterFactory.BruteProportions.armSize.z * 1.6f);
        LowPolyKit.CreateBlock("Pauldron", rig.armLHinge, new Vector3(0f, 0.1f, 0f), Vector3.zero, pauldronSize, pauldronMat);
        LowPolyKit.CreateBlock("Pauldron", rig.armRHinge, new Vector3(0f, 0.1f, 0f), Vector3.zero, pauldronSize, pauldronMat);

        var bruteGait = new CharacterFactory.Gait
        {
            stateName = "Walk",
            period = 1.0f,
            legSwingDeg = 18f,
            armSwingDeg = 14f,
            hipsBob = 0.16f,
            torsoLeanExtra = 0f,
        };

        CharacterFactory.BuildAnimations(go, rig, CharacterFactory.BruteProportions, "Brute",
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
        collider.center = new Vector3(0f, 0.78f, 0f);
        collider.radius = 0.36f;
        collider.height = 1.56f;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(40f);
        Enemy enemy = LowPolyKit.GetOrAddComponent<Enemy>(go);
        SetPrivateFloat(enemy, "moveSpeed", 2.5f);
        SetPrivateFloat(enemy, "attackRange", 1.2f);
        SetPrivateFloat(enemy, "attackDamage", 14f);
        SetPrivateInt(enemy, "scrapReward", 14);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.65f, 0.78f, 0.2f),
            skin = new Color(0.55f, 0.65f, 0.35f),
            headwear = new Color(0.2f, 0.26f, 0.18f),
            accent = new Color(0.35f, 0.85f, 0.25f),
            weaponBody = new Color(0.2f, 0.24f, 0.18f),
            weaponDark = new Color(0.14f, 0.2f, 0.12f),
        };

        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, CharacterFactory.SpitterProportions, colors, "Spitter", hasWeapon: false);
        ConfigureEnemyFeedback(enemy, colors.accent, 14, 0f, 0f);
        AddTeamRing(go.transform, 0.6f, EnemyRingColor);

        Material spitterAccentMat = GetOrCreateMaterial("SpitterAccent", colors.accent, unlit: true);
        float torsoHalfDepth = CharacterFactory.SpitterProportions.torsoSize.z * 0.5f;
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

        CharacterFactory.BuildAnimations(go, rig, CharacterFactory.SpitterProportions, "Spitter",
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
        collider.center = new Vector3(0f, 0.8f, 0f);
        collider.radius = 0.38f;
        collider.height = 1.6f;

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
            uniform = new Color(0.4f, 0.38f, 0.44f),
            skin = new Color(0.4f, 0.34f, 0.3f),
            headwear = new Color(0.16f, 0.14f, 0.13f),
            accent = new Color(0.95f, 0.4f, 0.05f),
            weaponBody = new Color(0.2f, 0.2f, 0.21f),
            weaponDark = new Color(0.08f, 0.08f, 0.09f),
        };

        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, CharacterFactory.BomberProportions, colors, "Bomber", hasWeapon: false);
        ConfigureEnemyFeedback(enemy, colors.accent, 22, 0.35f, 0f);
        AddTeamRing(go.transform, 0.6f, EnemyRingColor);

        Material bomberPlateMat = GetOrCreateMaterial("BomberPlate", new Color(0.22f, 0.2f, 0.18f), unlit: false);
        Material bomberAccentMat = GetOrCreateMaterial("BomberAccent", colors.accent, unlit: true);
        float torsoHalfDepth = CharacterFactory.BomberProportions.torsoSize.z * 0.5f;
        LowPolyKit.CreateBlock("CanisterL", rig.torso, new Vector3(-0.14f, 0.1f, -torsoHalfDepth - 0.14f), Vector3.zero, new Vector3(0.16f, 0.5f, 0.16f), bomberPlateMat, PrimitiveType.Cylinder);
        LowPolyKit.CreateBlock("CanisterR", rig.torso, new Vector3(0.14f, 0.1f, -torsoHalfDepth - 0.14f), Vector3.zero, new Vector3(0.16f, 0.5f, 0.16f), bomberPlateMat, PrimitiveType.Cylinder);
        LowPolyKit.CreateBlock("WarningStripe", rig.torso, new Vector3(0f, 0.1f, -torsoHalfDepth - 0.14f), Vector3.zero, new Vector3(0.36f, 0.08f, 0.2f), bomberAccentMat);

        var bomberGait = new CharacterFactory.Gait
        {
            stateName = "Walk",
            period = 0.55f,
            legSwingDeg = 26f,
            armSwingDeg = 30f,
            hipsBob = 0.08f,
            torsoLeanExtra = 0f,
        };

        CharacterFactory.BuildAnimations(go, rig, CharacterFactory.BomberProportions, "Bomber",
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
        collider.center = new Vector3(0f, 0.82f, 0f);
        collider.radius = 0.34f;
        collider.height = 1.64f;

        Health health = LowPolyKit.GetOrAddComponent<Health>(go);
        health.Configure(35f);
        Enemy enemy = LowPolyKit.GetOrAddComponent<Enemy>(go);
        SetPrivateFloat(enemy, "moveSpeed", 2.2f);
        SetPrivateFloat(enemy, "attackRange", 1f);
        SetPrivateFloat(enemy, "attackDamage", 8f);
        SetPrivateInt(enemy, "scrapReward", 15);

        var colors = new CharacterFactory.BipedColors
        {
            uniform = new Color(0.9f, 0.74f, 0.2f),
            skin = new Color(0.72f, 0.55f, 0.44f),
            headwear = new Color(0.95f, 0.55f, 0.08f),
            accent = new Color(0.95f, 0.6f, 0.1f),
            weaponBody = new Color(0.2f, 0.2f, 0.22f),
            weaponDark = new Color(0.1f, 0.1f, 0.11f),
        };

        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, CharacterFactory.EngineerProportions, colors, "Engineer", hasWeapon: false);
        ConfigureEnemyFeedback(enemy, colors.accent, 14, 0f, 0f);
        AddTeamRing(go.transform, 0.6f, EnemyRingColor);

        // hasWeapon: false gives the right hand a generic "Fist" block - replace it with
        // the tool below instead of layering the tool on top of it.
        Transform autoFist = rig.weaponSocket.Find("Fist");
        if (autoFist != null) Object.DestroyImmediate(autoFist.gameObject);

        Material engineerTankMat = GetOrCreateMaterial("EngineerTank", new Color(0.3f, 0.3f, 0.32f), unlit: false);
        Material engineerAccentMat = GetOrCreateMaterial("EngineerAccent", colors.accent, unlit: true);
        float torsoHalfDepth = CharacterFactory.EngineerProportions.torsoSize.z * 0.5f;
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
        CharacterFactory.BuildAnimations(go, rig, CharacterFactory.EngineerProportions, "Engineer",
            engineerGait, null, "Attack", 20f, 6f, false, out AnimatorController controller);

        Animator animator = LowPolyKit.GetOrAddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        GameObject prefab = LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
        return prefab.GetComponent<Enemy>();
    }

    private static GameObject GetOrCreateWallPrefab()
    {
        // A static structural prop, like the environment decor - no placement-grid or
        // pathing integration, since walls blocking enemy movement/taking damage would be
        // a new gameplay system rather than a visual one.
        string path = $"{PrefabFolder}/Wall.prefab";
        GameObject go = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(go);
        LowPolyKit.RemoveOwnMesh(go);
        go.name = "Wall";

        Material coreMat = GetOrCreateMaterial("WallConcrete", new Color(0.32f, 0.32f, 0.34f), unlit: false);
        Material trimMat = GetOrCreateMaterial("WallTrim", new Color(0.18f, 0.18f, 0.2f), unlit: false);

        LowPolyKit.CreateBlock("Slab", go.transform, new Vector3(0f, 0.7f, 0f), Vector3.zero, new Vector3(2.6f, 1.4f, 0.4f), coreMat);
        LowPolyKit.CreateBlock("BaseTrim", go.transform, new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(2.7f, 0.16f, 0.5f), trimMat);
        LowPolyKit.CreateBlock("TopTrim", go.transform, new Vector3(0f, 1.42f, 0f), Vector3.zero, new Vector3(2.7f, 0.12f, 0.46f), trimMat);
        LowPolyKit.CreateBlock("PanelL", go.transform, new Vector3(-0.85f, 0.7f, 0.21f), Vector3.zero, new Vector3(0.08f, 1.2f, 0.04f), trimMat);
        LowPolyKit.CreateBlock("PanelR", go.transform, new Vector3(0.85f, 0.7f, 0.21f), Vector3.zero, new Vector3(0.08f, 1.2f, 0.04f), trimMat);

        BoxCollider collider = LowPolyKit.GetOrAddComponent<BoxCollider>(go);
        collider.center = new Vector3(0f, 0.75f, 0f);
        collider.size = new Vector3(2.7f, 1.5f, 0.5f);

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
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
        collider.center = new Vector3(0f, 2.7f, 0f);
        collider.radius = 1.1f;
        collider.height = 5.5f;

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
            uniform = new Color(0.32f, 0.1f, 0.14f),
            skin = new Color(0.22f, 0.12f, 0.15f),
            headwear = new Color(0.1f, 0.1f, 0.11f),
            accent = new Color(0.95f, 0.3f, 0.08f),
            weaponBody = new Color(0.14f, 0.14f, 0.15f),
            weaponDark = new Color(0.08f, 0.08f, 0.08f),
        };

        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, CharacterFactory.BossProportions, colors, "Boss", hasWeapon: false);
        ConfigureEnemyFeedback(enemy, colors.accent, 70, 0.5f, 1f);
        AddTeamRing(go.transform, 1.9f, EnemyRingColor);

        Material bossAccentMat = GetOrCreateMaterial("BossAccent", colors.accent, unlit: true);
        CharacterFactory.BipedProportions bp = CharacterFactory.BossProportions;

        Vector3 pauldronSize = new Vector3(bp.armSize.x * 1.8f, 0.32f, bp.armSize.z * 1.8f);
        LowPolyKit.CreateBlock("Pauldron", rig.armLHinge, new Vector3(0f, 0.16f, 0f), Vector3.zero, pauldronSize, bossAccentMat);
        LowPolyKit.CreateBlock("Pauldron", rig.armRHinge, new Vector3(0f, 0.16f, 0f), Vector3.zero, pauldronSize, bossAccentMat);

        LowPolyKit.CreateBlock("CoreLight", rig.torso, new Vector3(0f, 0.3f, bp.torsoSize.z * 0.5f + 0.02f), Vector3.zero, new Vector3(0.4f, 0.4f, 0.08f), bossAccentMat);
        LowPolyKit.CreateBlock("Visor", rig.torso, new Vector3(0f, bp.torsoSize.y * 0.5f + bp.headSize.y * 0.5f - 0.02f, bp.headSize.z * 0.5f - 0.02f), Vector3.zero, new Vector3(bp.headSize.x * 0.7f, 0.08f, 0.05f), bossAccentMat);

        float fistSize = bp.armSize.x * 1.5f;
        LowPolyKit.CreateBlock("KnuckleGlow", rig.weaponSocket, new Vector3(0f, 0f, fistSize * 0.4f), Vector3.zero, new Vector3(fistSize * 0.5f, fistSize * 0.5f, 0.04f), bossAccentMat);
        Transform handSocketL = rig.armLHinge.Find("HandSocketL");
        if (handSocketL != null)
        {
            LowPolyKit.CreateBlock("KnuckleGlow", handSocketL, new Vector3(0f, 0f, fistSize * 0.4f), Vector3.zero, new Vector3(fistSize * 0.5f, fistSize * 0.5f, 0.04f), bossAccentMat);
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

    private static Tower GetOrCreateTowerPrefab(string prefabName, TowerStyle style, Color baseColor, Color accentColor, float scale, float range, float fireRate, float damage, Projectile projectilePrefab)
    {
        string path = $"{PrefabFolder}/{prefabName}.prefab";
        GameObject baseGo = LowPolyKit.BeginPrefabEdit(path, out bool wasLoaded);
        LowPolyKit.ClearChildren(baseGo);
        baseGo.name = prefabName;

        LowPolyKit.SetPrimitiveMesh(baseGo, PrimitiveType.Cube);
        baseGo.transform.localScale = new Vector3(scale, scale, scale);
        baseGo.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial($"{prefabName}Base", baseColor, unlit: false);

        GameObject turretPivot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        turretPivot.name = "TurretPivot";
        turretPivot.transform.SetParent(baseGo.transform, false);
        turretPivot.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        turretPivot.transform.localScale = new Vector3(0.5f / scale, 0.5f / scale, 0.5f / scale);
        turretPivot.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial($"{prefabName}Turret", accentColor, unlit: false);

        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(turretPivot.transform, false);
        firePoint.transform.localPosition = new Vector3(0f, 0f, 1f);

        AddTowerAccessories(turretPivot.transform, style, accentColor);

        Tower tower = LowPolyKit.GetOrAddComponent<Tower>(baseGo);
        tower.Configure(projectilePrefab, firePoint.transform, turretPivot.transform);
        tower.ConfigureStats(range, fireRate, damage);

        GameObject prefab = LowPolyKit.EndPrefabEdit(baseGo, path, wasLoaded);
        return prefab.GetComponent<Tower>();
    }

    private static void AddTowerAccessories(Transform turretPivot, TowerStyle style, Color accentColor)
    {
        Material barrelMat = GetOrCreateMaterial("BarrelDark", new Color(0.15f, 0.15f, 0.17f), unlit: false);

        switch (style)
        {
            case TowerStyle.Gun:
                Material housingMat = GetOrCreateMaterial("TowerGunHousing", new Color(0.22f, 0.22f, 0.25f), unlit: false);
                Material ammoMat = GetOrCreateMaterial("TowerGunAmmo", new Color(0.35f, 0.24f, 0.14f), unlit: false);

                LowPolyKit.CreateBlock("Housing", turretPivot, new Vector3(0f, 0.02f, -0.08f), Vector3.zero, new Vector3(0.62f, 0.3f, 0.6f), housingMat);
                LowPolyKit.CreateBlock("Sight", turretPivot, new Vector3(0f, 0.22f, 0.05f), Vector3.zero, new Vector3(0.08f, 0.1f, 0.25f), housingMat);
                LowPolyKit.CreateBlock("AmmoBoxL", turretPivot, new Vector3(-0.3f, -0.02f, -0.15f), new Vector3(0f, 15f, 0f), new Vector3(0.16f, 0.22f, 0.2f), ammoMat);
                LowPolyKit.CreateBlock("AmmoBoxR", turretPivot, new Vector3(0.3f, -0.02f, -0.15f), new Vector3(0f, -15f, 0f), new Vector3(0.16f, 0.22f, 0.2f), ammoMat);

                AddBarrel(turretPivot, new Vector3(0.16f, 0f, 0.55f), new Vector3(90f, 0f, 0f), 0.55f, 0.08f, barrelMat, null);
                AddBarrel(turretPivot, new Vector3(-0.16f, 0f, 0.55f), new Vector3(90f, 0f, 0f), 0.55f, 0.08f, barrelMat, null);
                break;

            case TowerStyle.Cannon:
                Material cannonPlateMat = GetOrCreateMaterial("TowerCannonPlate", new Color(0.28f, 0.24f, 0.2f), unlit: false);
                Material cannonTrimMat = GetOrCreateMaterial("TowerCannonTrim", new Color(0.15f, 0.13f, 0.1f), unlit: false);

                LowPolyKit.CreateBlock("Housing", turretPivot, new Vector3(0f, 0f, -0.15f), Vector3.zero, new Vector3(0.7f, 0.4f, 0.55f), cannonPlateMat);
                LowPolyKit.CreateBlock("PlateL", turretPivot, new Vector3(-0.38f, 0f, 0.1f), Vector3.zero, new Vector3(0.1f, 0.32f, 0.7f), cannonTrimMat);
                LowPolyKit.CreateBlock("PlateR", turretPivot, new Vector3(0.38f, 0f, 0.1f), Vector3.zero, new Vector3(0.1f, 0.32f, 0.7f), cannonTrimMat);
                LowPolyKit.CreateBlock("Hatch", turretPivot, new Vector3(0f, 0.24f, -0.2f), Vector3.zero, new Vector3(0.3f, 0.08f, 0.3f), cannonTrimMat);
                LowPolyKit.CreateBlock("RecoilSleeve", turretPivot, new Vector3(0f, 0f, 0.4f), new Vector3(90f, 0f, 0f), new Vector3(0.34f, 0.16f, 0.34f), cannonTrimMat, PrimitiveType.Cylinder);

                AddBarrel(turretPivot, new Vector3(0f, 0f, 0.85f), new Vector3(90f, 0f, 0f), 1.0f, 0.24f, barrelMat, null);
                break;

            case TowerStyle.Sniper:
                AddBarrel(turretPivot, new Vector3(0f, 0.05f, 1.3f), new Vector3(90f, 0f, 0f), 2.0f, 0.07f, barrelMat, null);
                break;

            case TowerStyle.Missile:
                AddBarrel(turretPivot, new Vector3(0.22f, 0.1f, 0.45f), new Vector3(75f, 0f, 0f), 0.7f, 0.1f, barrelMat, new Color(0.85f, 0.15f, 0.1f));
                AddBarrel(turretPivot, new Vector3(-0.22f, 0.1f, 0.45f), new Vector3(75f, 0f, 0f), 0.7f, 0.1f, barrelMat, new Color(0.85f, 0.15f, 0.1f));
                break;

            case TowerStyle.Tesla:
                Material accentMat = GetOrCreateMaterial("TeslaAccent", accentColor, unlit: true);
                Material capacitorMat = GetOrCreateMaterial("TeslaCapacitor", new Color(0.16f, 0.18f, 0.22f), unlit: false);

                LowPolyKit.CreateBlock("CapacitorL", turretPivot, new Vector3(-0.28f, -0.12f, 0f), Vector3.zero, new Vector3(0.16f, 0.3f, 0.16f), capacitorMat);
                LowPolyKit.CreateBlock("CapacitorR", turretPivot, new Vector3(0.28f, -0.12f, 0f), Vector3.zero, new Vector3(0.16f, 0.3f, 0.16f), capacitorMat);

                float[] prongAngles = { 0f, 90f, 180f, 270f };
                foreach (float prongAngle in prongAngles)
                {
                    Vector3 prongDir = Quaternion.Euler(0f, prongAngle, 0f) * Vector3.forward;
                    LowPolyKit.CreateBlock("Prong", turretPivot, prongDir * 0.2f + new Vector3(0f, 0.55f, 0f), new Vector3(0f, prongAngle, 25f), new Vector3(0.03f, 0.2f, 0.03f), accentMat);
                }

                GameObject coil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                coil.name = "Coil";
                Object.DestroyImmediate(coil.GetComponent<Collider>());
                coil.transform.SetParent(turretPivot, false);
                coil.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                coil.transform.localScale = new Vector3(0.18f, 0.35f, 0.18f);
                coil.GetComponent<Renderer>().sharedMaterial = accentMat;

                GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "Orb";
                Object.DestroyImmediate(orb.GetComponent<Collider>());
                orb.transform.SetParent(turretPivot, false);
                orb.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                orb.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                orb.GetComponent<Renderer>().sharedMaterial = accentMat;
                orb.AddComponent<SlowSpinner>().Configure(Vector3.up, 180f);
                break;
        }
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

    private static void SetTowerDefinitions(TowerPlacer placer, (string name, int cost, Tower prefab, Color accentColor)[] defs)
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

    private static GameObject GetOrCreatePlayerPrefab(Projectile projectilePrefab)
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
        LowPolyKit.GetOrAddComponent<Health>(go);
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

        CharacterFactory.BipedRig rig = CharacterFactory.BuildBiped(go, CharacterFactory.PlayerProportions, colors, "Player");
        AddTeamRing(go.transform, 0.65f, PlayerRingColor);
        CharacterFactory.BuildAnimations(go, rig, CharacterFactory.PlayerProportions, "Player",
            CharacterFactory.WalkGait, CharacterFactory.RunGait, "Shoot",
            35f, 8f, false, out AnimatorController controller);

        Animator animator = LowPolyKit.GetOrAddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        // Kept as a stable, non-animated child (rather than the visual rifle's muzzle) so
        // shot direction never drifts while the recoil/arm-swing animation is playing.
        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(go.transform, false);
        firePoint.transform.localPosition = new Vector3(0.22f, 1.35f, 0.75f);

        PlayerShooter shooter = LowPolyKit.GetOrAddComponent<PlayerShooter>(go);
        shooter.Configure(projectilePrefab, firePoint.transform);

        return LowPolyKit.EndPrefabEdit(go, path, wasLoaded);
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

        // The pivot carries the vertical stretch and the spin; the gem inside is a cube stood on its
        // corner, which reads as a faceted crystal once lit.
        GameObject crystal = new GameObject("Crystal");
        crystal.transform.SetParent(go.transform, false);
        crystal.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        crystal.transform.localScale = new Vector3(0.45f, 1.25f, 0.45f);
        crystal.AddComponent<SlowSpinner>().Configure(Vector3.up, 45f);
        Transform gem = LowPolyKit.CreateBlock("Gem", crystal.transform, Vector3.zero, Vector3.zero, Vector3.one, crystalMat);
        gem.localRotation = Quaternion.FromToRotation(Vector3.one, Vector3.up);

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
        Material portalRimMat = GetOrCreateMaterial("SpawnPortalRim", new Color(0.2f, 0.17f, 0.2f), unlit: false);
        Material portalGlowMat = GetOrCreateMaterial("SpawnPortalGlow", new Color(2.6f, 0.4f, 0.25f), unlit: true);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(arena, false);
        ground.transform.localScale = new Vector3(14f, 1f, 14f);
        ground.GetComponent<Renderer>().sharedMaterial = grassMat;

        // Floor layers are stacked a few cm apart (path < plaza < portal) to avoid z-fighting.
        CreateFloorPiece("Plaza", PrimitiveType.Cylinder, arena, new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(11f, 0.02f, 11f), plazaMat);

        foreach (Vector3 spawn in SpawnPositions)
        {
            Vector3 dir = spawn.normalized;
            const float pathStart = 4f;
            float length = spawn.magnitude - pathStart;
            Vector3 center = dir * (pathStart + length * 0.5f);
            CreateFloorPiece("Path", PrimitiveType.Cube, arena, center + Vector3.up * 0.01f, Quaternion.LookRotation(dir), new Vector3(2.8f, 0.02f, length + 1f), pathMat);
            CreateFloorPiece("PortalRim", PrimitiveType.Cylinder, arena, spawn + Vector3.up * 0.03f, Quaternion.identity, new Vector3(3.6f, 0.03f, 3.6f), portalRimMat);
            CreateFloorPiece("PortalGlow", PrimitiveType.Cylinder, arena, spawn + Vector3.up * 0.04f, Quaternion.identity, new Vector3(2.7f, 0.04f, 2.7f), portalGlowMat);
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

            // Keep the diagonal spawn lanes clear so portals and paths stay readable.
            float fromLane = Mathf.Abs(Mathf.DeltaAngle(angle, Mathf.Round((angle - 45f) / 90f) * 90f + 45f));
            if (fromLane < 9f && radius < 34f) radius += 8f;

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

    private static void BuildGameplayUI(EnemySpawner spawner, Health coreHealth, TowerPlacer placer)
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

    private static TowerShopButtonView CreateTowerCard(Transform parent, int index, TowerDefinition def)
    {
        RectTransform card = CreateRect($"TowerCard_{index}", parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(196f, 250f));
        Image background = AddImage(card, new Color(0.1f, 0.11f, 0.14f, 0.92f), RoundedSprite, raycast: true);
        Button button = card.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;

        Vector2 topCenter = new Vector2(0.5f, 1f);
        RectTransform icon = CreateRect("Icon", card, topCenter, topCenter, topCenter, new Vector2(0f, -16f), new Vector2(124f, 124f));
        Image iconImage = AddImage(icon, def.accentColor, RoundedSprite);
        string initial = string.IsNullOrEmpty(def.displayName) ? "?" : def.displayName.Substring(0, 1).ToUpperInvariant();
        Text initialText = CreateFillText(icon, "Initial", 72, new Color(0f, 0f, 0f, 0.55f), TextAnchor.MiddleCenter);
        initialText.text = initial;
        initialText.fontStyle = FontStyle.Bold;

        Vector2 bottomCenter = new Vector2(0.5f, 0f);
        Text label = CreateText(card, "Label", def.displayName, 28, Color.white, TextAnchor.MiddleCenter, bottomCenter, new Vector2(0f, 62f), new Vector2(190f, 40f));
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
        bool homing = false, float splashRadius = 0f, int pierceCount = 0, int chainCount = 0, float chainRange = 4f)
    {
        var so = new SerializedObject(projectile);
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

    private static void ConfigureEnemyFeedback(Enemy enemy, Color deathColor, int deathBurstCount, float attackShake, float deathShake)
    {
        var so = new SerializedObject(enemy);
        so.FindProperty("deathColor").colorValue = deathColor;
        so.FindProperty("deathBurstCount").intValue = deathBurstCount;
        so.FindProperty("attackShake").floatValue = attackShake;
        so.FindProperty("deathShake").floatValue = deathShake;
        so.ApplyModifiedProperties();
    }

    private static void ConfigureTowerSound(Tower tower, SfxId sound, float pitch)
    {
        var so = new SerializedObject(tower);
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
