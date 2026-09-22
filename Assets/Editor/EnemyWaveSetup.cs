using System;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class EnemyWaveSetup
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string PrefabFolder = "Assets/Prefabs/Enemies";
    private const string MaterialFolder = "Assets/Materials/Enemies";
    private const string NormalPrefabPath = PrefabFolder + "/Enemy_Normal.prefab";
    private const string HeavyPrefabPath = PrefabFolder + "/Enemy_Heavy.prefab";
    private const string NavMeshAssetPath = "Assets/Scenes/Sandbox_Gameplay/NavMesh-Envirnoment.asset";

    [MenuItem("Tools/AI For Game/Build Enemy Waves")]
    public static void Build()
    {
        EnsureFolder("Assets/Prefabs", "Enemies");
        EnsureFolder("Assets/Materials", "Enemies");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject legacyEnemies = FindSceneObject(scene, "Enemies");
        WorldUnitHUD sourceHud = legacyEnemies == null ? null : legacyEnemies.GetComponentInChildren<WorldUnitHUD>(true);

        Material normalMaterial = CreateOrLoadMaterial(MaterialFolder + "/Enemy_Normal.mat", new Color(0.78f, 0.18f, 0.16f));
        Material heavyMaterial = CreateOrLoadMaterial(MaterialFolder + "/Enemy_Heavy.mat", new Color(0.42f, 0.10f, 0.12f));
        GameObject normalPrefab = CreateEnemyPrefab(NormalPrefabPath, normalMaterial, sourceHud, false);
        GameObject heavyPrefab = CreateEnemyPrefab(HeavyPrefabPath, heavyMaterial, sourceHud, true);

        GameObject markerRoot = FindSceneObject(scene, "EnemySpawnPoint");
        if (markerRoot == null || markerRoot.transform.childCount < 2)
            throw new InvalidOperationException("Sandbox_Gameplay needs the EnemySpawnPoint root with two child markers.");

        EnemySpawnPoint waveOne = GetOrAdd<EnemySpawnPoint>(markerRoot.transform.GetChild(0).gameObject);
        EnemySpawnPoint waveTwo = GetOrAdd<EnemySpawnPoint>(markerRoot.transform.GetChild(1).gameObject);
        waveOne.Configure(1, new[]
        {
            new EnemySpawnPoint.SpawnEntry { enemyPrefab = normalPrefab, count = 3 }
        });
        waveTwo.Configure(2, new[]
        {
            new EnemySpawnPoint.SpawnEntry { enemyPrefab = normalPrefab, count = 2 },
            new EnemySpawnPoint.SpawnEntry { enemyPrefab = heavyPrefab, count = 1 }
        });

        GameObject spawnerObject = FindSceneObject(scene, "Enemy Wave Spawner");
        if (spawnerObject == null)
        {
            spawnerObject = new GameObject("Enemy Wave Spawner");
            SceneManager.MoveGameObjectToScene(spawnerObject, scene);
        }

        EnemyWaveSpawner spawner = GetOrAdd<EnemyWaveSpawner>(spawnerObject);
        spawner.Configure(new[] { waveOne, waveTwo }, 2f);

        BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
        if (director == null) throw new InvalidOperationException("Sandbox_Gameplay is missing BattleDirector.");
        director.SetEnemyWaveSpawner(spawner);

        if (legacyEnemies != null) legacyEnemies.SetActive(false);

        NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
        if (surface == null) throw new InvalidOperationException("Sandbox_Gameplay is missing NavMeshSurface.");
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        ExternalizeNavMesh(surface);

        EditorUtility.SetDirty(waveOne);
        EditorUtility.SetDirty(waveTwo);
        EditorUtility.SetDirty(spawner);
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ValidateScene(scene);
        Debug.Log("ENEMY_WAVE_SETUP_OK: two marker waves, two capsule prefabs, static child-only NavMesh bake, and legacy Raiders disabled.");
    }

    [MenuItem("Tools/AI For Game/Validate Enemy Waves")]
    public static void Validate()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ValidateScene(scene);
        Debug.Log("ENEMY_WAVE_VALIDATION_OK");
    }

    public static void ReserializeSceneAsText()
    {
        EditorSettings.serializationMode = SerializationMode.ForceText;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
        if (surface == null || surface.navMeshData == null)
            throw new InvalidOperationException("Sandbox_Gameplay has no NavMesh data to externalize.");
        ExternalizeNavMesh(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Unity could not resave Sandbox_Gameplay.");
        AssetDatabase.SaveAssets();
        Debug.Log("ENEMY_WAVE_SCENE_TEXT_OK");
    }

    private static void ExternalizeNavMesh(NavMeshSurface surface)
    {
        NavMeshData freshlyBaked = surface.navMeshData;
        NavMeshData storedData = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath);
        if (storedData == null)
        {
            AssetDatabase.CreateAsset(freshlyBaked, NavMeshAssetPath);
            EditorUtility.SetDirty(freshlyBaked);
            return;
        }

        if (freshlyBaked == storedData) return;
        EditorUtility.CopySerialized(freshlyBaked, storedData);
        storedData.name = freshlyBaked.name;
        surface.navMeshData = storedData;
        EditorUtility.SetDirty(storedData);
        EditorUtility.SetDirty(surface);
        if (!AssetDatabase.Contains(freshlyBaked)) Object.DestroyImmediate(freshlyBaked);
    }

    private static void ValidateScene(Scene scene)
    {
        EnemyWaveSpawner spawner = Object.FindFirstObjectByType<EnemyWaveSpawner>();
        EnemySpawnPoint[] points = Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
        BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
        NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
        GameObject normal = AssetDatabase.LoadAssetAtPath<GameObject>(NormalPrefabPath);
        GameObject heavy = AssetDatabase.LoadAssetAtPath<GameObject>(HeavyPrefabPath);

        if (!scene.IsValid() || spawner == null || director == null) throw new InvalidOperationException("Wave manager wiring is incomplete.");
        if (points.Length != 2 || points.Any(point => point.TotalEnemyCount == 0))
            throw new InvalidOperationException("Exactly two configured enemy spawn points are required.");
        if (points.Select(point => point.WaveNumber).Distinct().OrderBy(value => value).SequenceEqual(new[] { 1, 2 }) == false)
            throw new InvalidOperationException("Spawn points must configure waves 1 and 2.");
        if (normal == null || heavy == null) throw new InvalidOperationException("Both enemy prefabs are required.");
        ValidatePrefab(normal);
        ValidatePrefab(heavy);
        if (surface == null || surface.collectObjects != CollectObjects.Children || surface.navMeshData == null)
            throw new InvalidOperationException("NavMeshSurface must use a saved child-only static bake.");

        AutoCombatAI[] playerUnits = Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None)
            .Where(ai => ai.GetComponent<CombatUnit>() != null &&
                ai.GetComponent<CombatUnit>().team == CombatUnit.CombatTeam.Player)
            .ToArray();
        foreach (EnemySpawnPoint point in points)
        {
            for (int index = 0; index < point.TotalEnemyCount; index++)
                if (!point.TryGetSpawnPosition(index, out _))
                    throw new InvalidOperationException(point.name + " cannot place its formation on the baked NavMesh.");

            if (!point.TryGetSpawnPosition(0, out Vector3 destination)) continue;
            foreach (AutoCombatAI player in playerUnits)
            {
                if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit start, 10f, NavMesh.AllAreas))
                    throw new InvalidOperationException(player.name + " is not close enough to the baked NavMesh.");
                NavMeshPath path = new NavMeshPath();
                if (!NavMesh.CalculatePath(start.position, destination, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException(player.name + " has no complete NavMesh path to " + point.name + ".");
            }
        }
        GameObject legacyEnemies = FindSceneObject(scene, "Enemies");
        if (legacyEnemies != null && legacyEnemies.activeSelf)
            throw new InvalidOperationException("Legacy scene Raiders must remain disabled while marker waves are assigned.");
    }

    private static void ValidatePrefab(GameObject prefab)
    {
        CombatUnit unit = prefab.GetComponent<CombatUnit>();
        AutoCombatAI ai = prefab.GetComponent<AutoCombatAI>();
        CapsuleCollider collider = prefab.GetComponent<CapsuleCollider>();
        if (unit == null || ai == null || collider == null || unit.team != CombatUnit.CombatTeam.Enemy)
            throw new InvalidOperationException(prefab.name + " is not a valid enemy prefab.");
        if (collider.center.y - collider.height * 0.5f < -0.01f)
            throw new InvalidOperationException(prefab.name + " collider extends below its NavMesh root.");
    }

    private static GameObject CreateEnemyPrefab(string path, Material material, WorldUnitHUD sourceHud, bool heavy)
    {
        GameObject root = new GameObject(heavy ? "Enemy_Heavy" : "Enemy_Normal");
        float scale = heavy ? 1.3f : 1f;
        CapsuleCollider body = root.AddComponent<CapsuleCollider>();
        body.center = new Vector3(0f, scale, 0f);
        body.height = 2f * scale;
        body.radius = 0.5f * scale;

        CombatUnit unit = root.AddComponent<CombatUnit>();
        unit.team = CombatUnit.CombatTeam.Enemy;
        unit.isElite = heavy;
        unit.maxHealth = heavy ? 220f : 140f;
        unit.attackPower = heavy ? 8.5f : 5.4f;
        unit.defense = heavy ? 8f : 5f;
        unit.attackRange = 5f;
        unit.attackSpeed = heavy ? 0.8f : 1f;

        AutoCombatAI ai = root.AddComponent<AutoCombatAI>();
        ai.role = AutoCombatAI.CombatRole.Enemy;
        ai.movementSpeed = heavy ? 7.5f : 10f;
        ai.pathUpdateInterval = 0.2f;
        ai.separationDistance = heavy ? 1.7f : 1.35f;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Capsule Visual (Replace Model Here)";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, scale, 0f);
        visual.transform.localScale = Vector3.one * scale;
        visual.GetComponent<Renderer>().sharedMaterial = material;

        if (sourceHud != null)
        {
            GameObject hud = Object.Instantiate(sourceHud.gameObject, root.transform);
            hud.name = sourceHud.gameObject.name;
            hud.transform.localPosition = sourceHud.transform.localPosition;
            hud.transform.localRotation = sourceHud.transform.localRotation;
            hud.transform.localScale = sourceHud.transform.localScale;
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        if (saved == null) throw new InvalidOperationException("Could not create " + path);
        return saved;
    }

    private static Material CreateOrLoadMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Standard shader is unavailable.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
                if (candidate.name == objectName) return candidate.gameObject;
        }
        return null;
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }
}
