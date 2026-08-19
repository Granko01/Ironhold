using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class ProjectSetup
{
    private const string PipelineAssetPath = "Assets/Settings/UniversalRP.asset";
    private const string RendererAssetPath = "Assets/Settings/UniversalRenderer3D.asset";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string MaterialFolder = "Assets/Materials";

    private static readonly string[] GeneratedSceneObjectNames =
    {
        "Ground", "Core", "Player", "GameManager", "EnemySpawner", "TowerPlacer", "PlacementGrid", "EventSystem", "UICanvas", "EnvironmentDecor", "Directional Light"
    };

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

        Projectile bulletProjectile = GetOrCreateProjectilePrefab("ProjectileBullet", 0.35f, new Color(1f, 0.85f, 0.2f));
        Projectile cannonBallProjectile = GetOrCreateProjectilePrefab("ProjectileCannonBall", 0.55f, new Color(0.15f, 0.1f, 0.08f));
        Projectile teslaBoltProjectile = GetOrCreateProjectilePrefab("ProjectileTeslaBolt", 0.25f, new Color(0.4f, 0.85f, 1f));
        Projectile missileProjectile = GetOrCreateProjectilePrefab("ProjectileMissile", 0.4f, new Color(0.95f, 0.95f, 0.95f));
        Enemy enemyPrefab = GetOrCreateEnemyPrefab();

        Tower gunTower = GetOrCreateTowerPrefab("TowerGun", TowerStyle.Gun, new Color(0.3f, 0.3f, 0.35f), new Color(0.4f, 0.4f, 0.42f), 1f, 8f, 3f, 8f, bulletProjectile);
        Tower cannonTower = GetOrCreateTowerPrefab("TowerCannon", TowerStyle.Cannon, new Color(0.25f, 0.22f, 0.2f), new Color(0.3f, 0.27f, 0.24f), 1.3f, 6f, 1f, 30f, cannonBallProjectile);
        Tower sniperTower = GetOrCreateTowerPrefab("TowerSniper", TowerStyle.Sniper, new Color(0.2f, 0.28f, 0.22f), new Color(0.5f, 0.55f, 0.32f), 0.85f, 14f, 0.6f, 45f, bulletProjectile);
        Tower teslaTower = GetOrCreateTowerPrefab("TowerTesla", TowerStyle.Tesla, new Color(0.22f, 0.25f, 0.3f), new Color(0.3f, 0.75f, 1f), 1f, 6.5f, 5f, 4f, teslaBoltProjectile);
        Tower missileTower = GetOrCreateTowerPrefab("TowerMissile", TowerStyle.Missile, new Color(0.28f, 0.24f, 0.18f), new Color(0.45f, 0.4f, 0.32f), 1.15f, 11f, 0.5f, 55f, missileProjectile);

        GameObject playerPrefabGo = GetOrCreatePlayerPrefab(bulletProjectile);
        GameObject corePrefabGo = GetOrCreateCorePrefab();

        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
        }

        if (Object.FindFirstObjectByType<Light>() == null)
        {
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        BuildEnvironmentDecor();

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
            player.transform.position = new Vector3(0f, 1f, -6f);
        }

        GameObject gameManagerGo = GameObject.Find("GameManager");
        if (gameManagerGo == null)
        {
            gameManagerGo = new GameObject("GameManager");
            GameManager gm = gameManagerGo.AddComponent<GameManager>();
            gm.RegisterCore(coreHealth);
        }

        GameObject spawnerGo = GameObject.Find("EnemySpawner");
        if (spawnerGo == null)
        {
            spawnerGo = new GameObject("EnemySpawner");
            EnemySpawner spawner = spawnerGo.AddComponent<EnemySpawner>();

            Vector3[] spawnPositions =
            {
                new Vector3(20f, 0.5f, 20f),
                new Vector3(-20f, 0.5f, 20f),
                new Vector3(20f, 0.5f, -20f),
                new Vector3(-20f, 0.5f, -20f),
            };

            var spawnPoints = new Transform[spawnPositions.Length];
            for (int i = 0; i < spawnPositions.Length; i++)
            {
                GameObject sp = new GameObject($"SpawnPoint_{i}");
                sp.transform.SetParent(spawnerGo.transform);
                sp.transform.position = spawnPositions[i];
                spawnPoints[i] = sp.transform;
            }

            spawner.Configure(enemyPrefab, core.transform, spawnPoints);
        }

        GameObject towerPlacerGo = GameObject.Find("TowerPlacer");
        if (towerPlacerGo == null)
        {
            towerPlacerGo = new GameObject("TowerPlacer");
            towerPlacerGo.AddComponent<TowerPlacer>();
        }
        TowerPlacer towerPlacer = towerPlacerGo.GetComponent<TowerPlacer>();
        SetPrivateField(towerPlacer, "grid", grid);
        SetPrivateField(towerPlacer, "player", player.transform);
        SetTowerDefinitions(towerPlacer, new (string name, int cost, Tower prefab)[]
        {
            ("Gun Tower", 50, gunTower),
            ("Cannon Tower", 120, cannonTower),
            ("Sniper Tower", 150, sniperTower),
            ("Tesla Tower", 90, teslaTower),
            ("Missile Tower", 180, missileTower),
        });

        EnsureEventSystem();
        Transform canvasTransform = EnsureCanvas();
        BuildDebugHud(canvasTransform, spawnerGo.GetComponent<EnemySpawner>(), coreHealth);
        BuildTowerShop(canvasTransform, towerPlacer);

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
        string path = $"{PrefabFolder}/Enemy.prefab";
        Enemy existing = AssetDatabase.LoadAssetAtPath<Enemy>(path);
        if (existing != null) return existing;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Enemy";
        go.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Enemy", new Color(0.85f, 0.15f, 0.15f), unlit: false);

        Health health = go.AddComponent<Health>();
        health.Configure(30f);
        go.AddComponent<Enemy>();

        GameObject prefab = SaveAndDestroy(go, path);
        return prefab.GetComponent<Enemy>();
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
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing.GetComponent<Tower>();

        GameObject baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseGo.name = prefabName;
        baseGo.transform.localScale = new Vector3(scale, scale, scale);
        baseGo.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial($"{prefabName}Base", baseColor, unlit: false);

        GameObject turretPivot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        turretPivot.name = "TurretPivot";
        turretPivot.transform.SetParent(baseGo.transform);
        turretPivot.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        turretPivot.transform.localScale = new Vector3(0.5f / scale, 0.5f / scale, 0.5f / scale);
        turretPivot.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial($"{prefabName}Turret", accentColor, unlit: false);

        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(turretPivot.transform);
        firePoint.transform.localPosition = new Vector3(0f, 0f, 1f);

        AddTowerAccessories(turretPivot.transform, style, accentColor);

        Tower tower = baseGo.AddComponent<Tower>();
        tower.Configure(projectilePrefab, firePoint.transform, turretPivot.transform);
        tower.ConfigureStats(range, fireRate, damage);

        GameObject prefab = SaveAndDestroy(baseGo, path);
        return prefab.GetComponent<Tower>();
    }

    private static void AddTowerAccessories(Transform turretPivot, TowerStyle style, Color accentColor)
    {
        Material barrelMat = GetOrCreateMaterial("BarrelDark", new Color(0.15f, 0.15f, 0.17f), unlit: false);

        switch (style)
        {
            case TowerStyle.Gun:
                AddBarrel(turretPivot, new Vector3(0.16f, 0f, 0.55f), new Vector3(90f, 0f, 0f), 0.55f, 0.08f, barrelMat, null);
                AddBarrel(turretPivot, new Vector3(-0.16f, 0f, 0.55f), new Vector3(90f, 0f, 0f), 0.55f, 0.08f, barrelMat, null);
                break;

            case TowerStyle.Cannon:
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

    private static void SetTowerDefinitions(TowerPlacer placer, (string name, int cost, Tower prefab)[] defs)
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
        }

        so.ApplyModifiedProperties();
    }

    private static GameObject GetOrCreatePlayerPrefab(Projectile projectilePrefab)
    {
        string path = $"{PrefabFolder}/Player.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Player";
        go.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Player", new Color(0.22f, 0.4f, 0.6f), unlit: false);

        GameObject helmet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        helmet.name = "Helmet";
        Object.DestroyImmediate(helmet.GetComponent<Collider>());
        helmet.transform.SetParent(go.transform, false);
        helmet.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        helmet.transform.localScale = new Vector3(0.55f, 0.4f, 0.55f);
        helmet.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Helmet", new Color(0.18f, 0.22f, 0.16f), unlit: false);

        GameObject rifle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rifle.name = "Rifle";
        Object.DestroyImmediate(rifle.GetComponent<Collider>());
        rifle.transform.SetParent(go.transform, false);
        rifle.transform.localPosition = new Vector3(0.25f, 0.1f, 0.55f);
        rifle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        rifle.transform.localScale = new Vector3(0.05f, 0.4f, 0.05f);
        rifle.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Rifle", new Color(0.12f, 0.12f, 0.13f), unlit: false);

        go.AddComponent<Rigidbody>();
        go.AddComponent<Health>();
        go.AddComponent<PlayerController>();

        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(go.transform);
        firePoint.transform.localPosition = new Vector3(0f, 0f, 0.7f);

        PlayerShooter shooter = go.AddComponent<PlayerShooter>();
        shooter.Configure(projectilePrefab, firePoint.transform);

        return SaveAndDestroy(go, path);
    }

    private static GameObject GetOrCreateCorePrefab()
    {
        string path = $"{PrefabFolder}/Core.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Core";
        go.transform.localScale = new Vector3(2.4f, 1f, 2.4f);
        go.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("CoreBase", new Color(0.25f, 0.25f, 0.28f), unlit: false);

        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crystal.name = "Crystal";
        Object.DestroyImmediate(crystal.GetComponent<Collider>());
        crystal.transform.SetParent(go.transform, false);
        crystal.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        crystal.transform.localRotation = Quaternion.Euler(0f, 45f, 45f);
        crystal.transform.localScale = new Vector3(0.7f, 1.6f, 0.7f);
        crystal.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("CoreCrystal", new Color(0.25f, 0.95f, 1f), unlit: true);
        crystal.AddComponent<SlowSpinner>().Configure(Vector3.up, 45f);

        Health health = go.AddComponent<Health>();
        health.Configure(500f);

        return SaveAndDestroy(go, path);
    }

    private static void BuildEnvironmentDecor()
    {
        if (GameObject.Find("EnvironmentDecor") != null) return;

        GameObject parent = new GameObject("EnvironmentDecor");

        Material trunkMat = GetOrCreateMaterial("TreeTrunk", new Color(0.35f, 0.24f, 0.14f), unlit: false);
        Material canopyMat = GetOrCreateMaterial("TreeCanopy", new Color(0.2f, 0.45f, 0.18f), unlit: false);
        Material rockMat = GetOrCreateMaterial("Rock", new Color(0.45f, 0.45f, 0.47f), unlit: false);
        Material crateMat = GetOrCreateMaterial("Crate", new Color(0.45f, 0.32f, 0.16f), unlit: false);

        Random.InitState(1234);

        const int propCount = 14;
        for (int i = 0; i < propCount; i++)
        {
            float angle = i * (360f / propCount) + Random.Range(-8f, 8f);
            float radius = Random.Range(23f, 29f);
            Vector3 localPos = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;

            int propType = i % 3;
            if (propType == 0) CreateTree(parent.transform, localPos, trunkMat, canopyMat);
            else if (propType == 1) CreateRock(parent.transform, localPos, rockMat);
            else CreateCrate(parent.transform, localPos, crateMat);
        }
    }

    private static void CreateTree(Transform parent, Vector3 localPos, Material trunkMat, Material canopyMat)
    {
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "TreeTrunk";
        trunk.transform.SetParent(parent, false);
        trunk.transform.localPosition = localPos + Vector3.up * 0.75f;
        trunk.transform.localScale = new Vector3(0.25f, 0.75f, 0.25f);
        trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;

        GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        canopy.name = "TreeCanopy";
        Object.DestroyImmediate(canopy.GetComponent<Collider>());
        canopy.transform.SetParent(parent, false);
        canopy.transform.localPosition = localPos + Vector3.up * 2.4f;
        canopy.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
        canopy.GetComponent<Renderer>().sharedMaterial = canopyMat;
    }

    private static void CreateRock(Transform parent, Vector3 localPos, Material rockMat)
    {
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "Rock";
        float size = Random.Range(0.8f, 1.6f);
        rock.transform.SetParent(parent, false);
        rock.transform.localPosition = localPos + Vector3.up * size * 0.3f;
        rock.transform.localScale = new Vector3(size, size * 0.7f, size);
        rock.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        rock.GetComponent<Renderer>().sharedMaterial = rockMat;
    }

    private static void CreateCrate(Transform parent, Vector3 localPos, Material crateMat)
    {
        GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crate.name = "Crate";
        crate.transform.SetParent(parent, false);
        crate.transform.localPosition = localPos + Vector3.up * 0.5f;
        crate.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        crate.GetComponent<Renderer>().sharedMaterial = crateMat;
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
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return go.transform;
    }

    private static void BuildDebugHud(Transform canvasTransform, EnemySpawner spawner, Health coreHealth)
    {
        GameObject panel = GameObject.Find("HUDPanel");
        if (panel == null) panel = new GameObject("HUDPanel", typeof(RectTransform));
        panel.transform.SetParent(canvasTransform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = Vector2.zero;

        Vector2 anchor = new Vector2(0f, 1f);
        const float lineHeight = 40f;
        const float startY = -28f;
        const float x = 28f;

        Text scrapText = CreateText(panel.transform, "ScrapText", "Scrap: 0", 28, Color.white, TextAnchor.UpperLeft, anchor, new Vector2(x, startY), new Vector2(500f, lineHeight));
        Text coreHpText = CreateText(panel.transform, "CoreHpText", "Core HP: -", 28, Color.white, TextAnchor.UpperLeft, anchor, new Vector2(x, startY - lineHeight), new Vector2(500f, lineHeight));
        Text waveText = CreateText(panel.transform, "WaveText", "Wave: 0", 28, Color.white, TextAnchor.UpperLeft, anchor, new Vector2(x, startY - lineHeight * 2f), new Vector2(500f, lineHeight));
        CreateText(panel.transform, "InstructionsText", "WASD move | Auto-fires at nearest enemy in range | Pick a tower below or press 1-9 to place", 20, new Color(0.85f, 0.85f, 0.85f), TextAnchor.UpperLeft, anchor, new Vector2(x, startY - lineHeight * 3f), new Vector2(1200f, lineHeight));
        Text placementText = CreateText(panel.transform, "PlacementText", "Placement mode: OFF", 28, Color.white, TextAnchor.UpperLeft, anchor, new Vector2(x, startY - lineHeight * 4f), new Vector2(800f, lineHeight));

        Text gameOverText = CreateText(canvasTransform, "GameOverText", "GAME OVER - Core destroyed", 56, Color.red, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 100f));
        gameOverText.fontStyle = FontStyle.Bold;
        gameOverText.gameObject.SetActive(false);

        DebugHUD hud = panel.GetComponent<DebugHUD>();
        if (hud == null) hud = panel.AddComponent<DebugHUD>();
        hud.Configure(spawner, coreHealth, scrapText, coreHpText, waveText, placementText, gameOverText.gameObject);
    }

    private static void BuildTowerShop(Transform canvasTransform, TowerPlacer placer)
    {
        GameObject panel = GameObject.Find("TowerShopPanel");
        if (panel == null)
        {
            panel = new GameObject("TowerShopPanel", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        }
        panel.transform.SetParent(canvasTransform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 36f);

        HorizontalLayoutGroup layout = panel.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (Transform child in panel.transform)
        {
            Object.DestroyImmediate(child.gameObject);
        }

        TowerDefinition[] defs = placer.Definitions ?? new TowerDefinition[0];
        var buttonViews = new TowerShopButtonView[defs.Length];
        for (int i = 0; i < defs.Length; i++)
        {
            buttonViews[i] = CreateTowerButton(panel.transform, i);
        }

        Text hintText = CreateText(canvasTransform, "PlacementHint", "Face where you want it, then press B (or click) - right click / Esc to cancel", 24, Color.yellow, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(900f, 40f));

        TowerShopUI shopUi = panel.GetComponent<TowerShopUI>();
        if (shopUi == null) shopUi = panel.AddComponent<TowerShopUI>();
        shopUi.Configure(placer, buttonViews, hintText.gameObject);
    }

    private static TowerShopButtonView CreateTowerButton(Transform parent, int index)
    {
        GameObject go = new GameObject($"TowerButton_{index}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280f, 96f);

        LayoutElement layoutElement = go.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 280f;
        layoutElement.preferredHeight = 96f;

        Image background = go.GetComponent<Image>();
        background.color = Color.white;

        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        Text label = CreateFillText(go.transform, "Label", 24, Color.black, TextAnchor.MiddleCenter);

        return new TowerShopButtonView { Button = button, Background = background, Label = label };
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
        string path = $"{MaterialFolder}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        string shaderName = unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit";
        Shader shader = Shader.Find(shaderName);
        Material mat = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
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

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        string folderName = System.IO.Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
