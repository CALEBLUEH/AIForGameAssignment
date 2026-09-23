using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class EnemyModelPrefabBuilder
{
    private const string SourceRoot = "Assets/ThirdParty/EnemyModels";
    private const string VisualRoot = "Assets/Prefabs/Enemies/Models";
    private const string CombatRoot = "Assets/Prefabs/Enemies";
    private const string BaseEnemyPath = CombatRoot + "/Enemy_Normal.prefab";
    private const string SandboxScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";

    private sealed class Spec
    {
        public string sourceFolder;
        public string visualName;
        public string prefabName;
        public float size;
        public float health;
        public float attack;
        public float defense;
        public float moveSpeed;
        public bool boss;
        public bool elite;
        public bool flipTextureV = true;
    }

    private static readonly Spec[] Specs =
    {
        Enemy("EnemyRobot", "EnemyRobot", "Enemy_Robot", 2.2f, 180f, 7.5f, 6f, 9f),
        Enemy("ToramaruTank", "ToramaruTank", "Enemy_ToramaruTank", 3.4f, 300f, 13f, 12f, 6.5f, true),
        Enemy("HelmetGangVehicle", "HelmetGangVehicle", "Enemy_HelmetGangVehicle", 3.2f, 250f, 11f, 10f, 7f, true),
        Enemy("Sensei", "Sensei", "Enemy_Sensei", 2.0f, 160f, 7f, 5f, 9f),
        Boss("SaibaMomoi", "SaibaMomoi", "Boss_SaibaMomoi", 2.4f, 700f, 28f, 12f, 8f, false),
        Boss("KisakiBall", "KisakiBall", "Boss_KisakiBall", 3.0f, 900f, 34f, 16f, 7.5f),
        Boss("KoyukiPrism", "KoyukiPrism", "Boss_KoyukiPrism", 3.2f, 1200f, 42f, 20f, 7f)
    };

    [MenuItem("Tools/AI For Game/Build Enemy Model Prefabs")]
    public static void Build()
    {
        EnsureFolder(VisualRoot);
        GameObject baseEnemy = AssetDatabase.LoadAssetAtPath<GameObject>(BaseEnemyPath);
        if (baseEnemy == null) throw new FileNotFoundException("Missing base enemy prefab", BaseEnemyPath);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (Spec spec in Specs)
        {
            string source = SourceRoot + "/" + spec.sourceFolder;
            RequireSource(source);
            string visualPath = VisualRoot + "/" + spec.visualName + ".prefab";
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
            if (visualPrefab == null)
                visualPrefab = EnvironmentModelImporter.ImportStandaloneModel(
                    source, spec.visualName, visualPath, spec.size, spec.flipTextureV);
            BuildCombatPrefab(baseEnemy, visualPrefab, spec);
        }

        ConfigureSandboxConeProjectiles();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Validate();
        Debug.Log("ENEMY_MODEL_PREFABS_OK seven enemy/boss prefabs created with combat AI, grounded colliders, HUDs, preserved licences and procedural attack recoil. Momoi/Hina use five randomized cone projectiles per tick.");
    }

    [MenuItem("Tools/AI For Game/Validate Enemy Model Prefabs")]
    public static void Validate()
    {
        foreach (Spec spec in Specs)
        {
            string path = CombatRoot + "/" + spec.prefabName + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing generated prefab: " + path);
            CombatUnit unit = prefab.GetComponent<CombatUnit>();
            AutoCombatAI ai = prefab.GetComponent<AutoCombatAI>();
            CapsuleCollider collider = prefab.GetComponent<CapsuleCollider>();
            EnemyAttackRecoil recoil = prefab.GetComponent<EnemyAttackRecoil>();
            if (unit == null || ai == null || collider == null || recoil == null || recoil.VisualRoot == null)
                throw new InvalidOperationException(spec.prefabName + " is missing combat, collider, or recoil wiring.");
            if (unit.team != CombatUnit.CombatTeam.Enemy || unit.isBoss != spec.boss)
                throw new InvalidOperationException(spec.prefabName + " has incorrect enemy/boss identity.");
            if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                throw new InvalidOperationException(spec.prefabName + " contains no visible model.");
            if (collider.center.y - collider.height * 0.5f < -0.01f)
                throw new InvalidOperationException(spec.prefabName + " collider extends below the NavMesh root.");
            if (prefab.GetComponentInChildren<WorldUnitHUD>(true) == null)
                throw new InvalidOperationException(spec.prefabName + " is missing its combat HUD.");
            string license = SourceRoot + "/" + spec.sourceFolder + "/license.txt";
            if (!File.Exists(Path.GetFullPath(license)))
                throw new InvalidOperationException(spec.prefabName + " is missing its source licence.");
        }
    }

    [MenuItem("Tools/AI For Game/Rebuild Sensei And Boss Materials")]
    public static void RebuildSenseiAndBossMaterials()
    {
        EnsureFolder(VisualRoot);
        GameObject baseEnemy = AssetDatabase.LoadAssetAtPath<GameObject>(BaseEnemyPath);
        if (baseEnemy == null) throw new FileNotFoundException("Missing base enemy prefab", BaseEnemyPath);
        string[] affected = { "Sensei", "SaibaMomoi", "KisakiBall", "KoyukiPrism" };
        foreach (Spec spec in Specs.Where(item => affected.Contains(item.sourceFolder)))
        {
            string source = SourceRoot + "/" + spec.sourceFolder;
            RequireSource(source);
            string visualPath = VisualRoot + "/" + spec.visualName + ".prefab";
            AssetDatabase.DeleteAsset(visualPath);
            AssetDatabase.DeleteAsset(source + "/Generated");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            GameObject visualPrefab = EnvironmentModelImporter.ImportStandaloneModel(
                source, spec.visualName, visualPath, spec.size, spec.flipTextureV);
            BuildCombatPrefab(baseEnemy, visualPrefab, spec);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Validate();
        Debug.Log("ENEMY_CHARACTER_MATERIALS_REBUILT Sensei and all three bosses now use source-correct UV orientation, glTF unlit flags and double-sided settings.");
    }

    private static void BuildCombatPrefab(GameObject baseEnemy, GameObject visualPrefab, Spec spec)
    {
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(baseEnemy);
        root.name = spec.prefabName;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        Transform oldVisual = root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == "Capsule Visual (Replace Model Here)");
        if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, root.transform);
        model.name = "Model Visual (Adjust Here)";
        model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        model.transform.localScale = Vector3.one;

        Bounds bounds = LocalRendererBounds(root.transform, model);
        float height = Mathf.Max(0.5f, bounds.size.y);
        float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
        float radius = Mathf.Clamp(horizontal * 0.28f, 0.35f, height * 0.48f);
        CapsuleCollider collider = root.GetComponent<CapsuleCollider>();
        collider.center = new Vector3(bounds.center.x, Mathf.Max(radius, bounds.center.y), bounds.center.z);
        collider.radius = radius;
        collider.height = Mathf.Max(radius * 2f, height);

        CombatUnit unit = root.GetComponent<CombatUnit>();
        unit.team = CombatUnit.CombatTeam.Enemy;
        unit.characterData = null;
        unit.SetApplyCharacterDataOnAwake(false);
        unit.isBoss = spec.boss;
        unit.isElite = spec.elite || spec.boss;
        unit.maxHealth = spec.health;
        unit.attackPower = spec.attack;
        unit.defense = spec.defense;
        unit.attackRange = 30f;
        unit.attackSpeed = spec.boss ? 0.85f : 1f;

        AutoCombatAI ai = root.GetComponent<AutoCombatAI>();
        ai.role = AutoCombatAI.CombatRole.Enemy;
        ai.movementSpeed = spec.moveSpeed;
        ai.pathUpdateInterval = 0.2f;
        ai.separationDistance = Mathf.Max(2f, radius * 2.2f);
        ai.separationStrength = 1f;
        ai.enemyDetectionRange = spec.boss ? 24f : 18f;
        ai.enemyCanUseCover = spec.sourceFolder == "Sensei";

        EnemyAttackRecoil recoil = root.GetComponent<EnemyAttackRecoil>();
        if (recoil == null) recoil = root.AddComponent<EnemyAttackRecoil>();
        recoil.Configure(ai, model.transform);

        WorldUnitHUD hud = root.GetComponentInChildren<WorldUnitHUD>(true);
        if (hud != null)
        {
            Vector3 hudPosition = hud.transform.localPosition;
            hudPosition.y = bounds.max.y + 0.55f;
            hud.transform.localPosition = hudPosition;
        }

        string path = CombatRoot + "/" + spec.prefabName + ".prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        if (saved == null) throw new InvalidOperationException("Could not save " + path);
    }

    private static void ConfigureSandboxConeProjectiles()
    {
        Scene scene = EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Single);
        foreach (AutoCombatAI ai in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<AutoCombatAI>(true)))
        {
            if (ai.role != AutoCombatAI.CombatRole.MomoiLowCostAOE &&
                ai.role != AutoCombatAI.CombatRole.HinaHighCostAOE) continue;
            ai.coneProjectilesPerTick = 5;
            EditorUtility.SetDirty(ai);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save " + SandboxScenePath);
    }

    private static Bounds LocalRendererBounds(Transform root, GameObject visual)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException(visual.name + " has no renderers.");
        Bounds result = TransformBoundsToLocal(root, renderers[0].bounds);
        for (int i = 1; i < renderers.Length; i++)
            result.Encapsulate(TransformBoundsToLocal(root, renderers[i].bounds));
        return result;
    }

    private static Bounds TransformBoundsToLocal(Transform root, Bounds world)
    {
        Vector3 min = world.min;
        Vector3 max = world.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z)
        };
        Bounds local = new Bounds(root.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++) local.Encapsulate(root.InverseTransformPoint(corners[i]));
        return local;
    }

    private static void RequireSource(string source)
    {
        string absolute = Path.GetFullPath(source);
        if (!File.Exists(Path.Combine(absolute, "scene.gltf")) ||
            !File.Exists(Path.Combine(absolute, "scene.bin")) ||
            !File.Exists(Path.Combine(absolute, "license.txt")))
            throw new InvalidOperationException("Incomplete model source: " + source);
    }

    private static void EnsureFolder(string assetPath)
    {
        string[] parts = assetPath.Replace('\\', '/').Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static Spec Enemy(string source, string visual, string prefab, float size,
        float health, float attack, float defense, float speed, bool elite = false) => new Spec
    {
        sourceFolder = source, visualName = visual, prefabName = prefab, size = size,
        health = health, attack = attack, defense = defense, moveSpeed = speed, elite = elite
    };

    private static Spec Boss(string source, string visual, string prefab, float size,
        float health, float attack, float defense, float speed, bool flipTextureV = true) => new Spec
    {
        sourceFolder = source, visualName = visual, prefabName = prefab, size = size,
        health = health, attack = attack, defense = defense, moveSpeed = speed, boss = true, elite = true,
        flipTextureV = flipTextureV
    };
}
