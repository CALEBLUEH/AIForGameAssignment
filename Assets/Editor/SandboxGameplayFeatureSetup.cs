using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class SandboxGameplayFeatureSetup
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string GeneratedCoverRootName = "Tactical Cover Points (Generated)";
    private static readonly string[] EnemyPrefabPaths =
    {
        "Assets/Prefabs/Enemies/Enemy_Normal.prefab",
        "Assets/Prefabs/Enemies/Enemy_Heavy.prefab"
    };

    [MenuItem("Tools/AI For Game/Build Sandbox Cover and Formations")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        TransformSnapshot[] snapshots = CaptureExistingTransforms(scene);

        AutoCombatAI momoi = FindAll<AutoCombatAI>(scene).FirstOrDefault(ai => ai.name == "Momoi");
        if (momoi == null) throw new InvalidOperationException("Sandbox_Gameplay is missing Momoi.");
        Vector3 momoiScale = momoi.transform.localScale;
        Quaternion momoiRotation = momoi.transform.localRotation;
        Vector3 oldMomoiPosition = momoi.transform.position;
        SnapMomoiToSquadRoad(scene, momoi);

        foreach (AutoCombatAI player in FindAll<AutoCombatAI>(scene))
        {
            CombatUnit unit = player.GetComponent<CombatUnit>();
            if (unit == null || unit.team != CombatUnit.CombatTeam.Player) continue;
            player.separationDistance = 4.2f;
            player.separationStrength = 1f;
            EditorUtility.SetDirty(player);
        }

        GameObject obstacleRoot = FindObject(scene, "Obstacle");
        if (obstacleRoot == null) throw new InvalidOperationException("Sandbox_Gameplay is missing its Obstacle root.");
        Collider[] obstacleColliders = obstacleRoot.GetComponentsInChildren<Collider>(true);
        if (obstacleColliders.Length == 0) throw new InvalidOperationException("The Obstacle root contains no colliders.");
        foreach (Collider collider in obstacleColliders) ConfigureObstacle(collider);

        EnemySpawnPoint[] spawnPoints = FindAll<EnemySpawnPoint>(scene);
        foreach (EnemySpawnPoint point in spawnPoints)
        {
            EnemyFormationShape shape = point.WaveNumber == 1
                ? EnemyFormationShape.Triangle
                : EnemyFormationShape.Box;
            point.ConfigureFormation(shape, 2.1f, 1.8f, 0.65f);
            EditorUtility.SetDirty(point);
        }

        BattleDirector director = FindAll<BattleDirector>(scene).FirstOrDefault();
        if (director == null || director.battlePanel == null)
            throw new InvalidOperationException("Sandbox_Gameplay is missing its BattleDirector or Battle HUD.");
        Transform battleCanvas = director.battlePanel.transform.parent;
        if (battleCanvas == null)
            throw new InvalidOperationException("The Battle HUD has no canvas parent.");
        battleCanvas.gameObject.SetActive(true);
        EditorUtility.SetDirty(battleCanvas.gameObject);

        if (momoi.transform.localScale != momoiScale || momoi.transform.localRotation != momoiRotation)
            throw new InvalidOperationException("Momoi's scale or rotation changed unexpectedly.");
        RestoreExistingTransforms(snapshots, momoi.transform);
        VerifyExistingTransforms(snapshots, momoi.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Unity could not save Sandbox_Gameplay.");

        ConfigureEnemyPrefabs();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"SANDBOX_GAMEPLAY_FEATURE_SETUP_OK Momoi moved from {oldMomoiPosition} to {momoi.transform.position}; scale preserved at {momoiScale}. coverPoints={FindAll<CoverPoint>(scene).Length} formations={spawnPoints.Length}.");
    }

    private static void SnapMomoiToSquadRoad(Scene scene, AutoCombatAI momoi)
    {
        Collider body = momoi.GetComponent<Collider>();
        float groundOffset = body == null ? 0f : Mathf.Max(0f, momoi.transform.position.y - body.bounds.min.y);

        var teammatePoints = new List<Vector3>();
        foreach (AutoCombatAI candidate in FindAll<AutoCombatAI>(scene))
        {
            if (candidate == momoi) continue;
            CombatUnit unit = candidate.GetComponent<CombatUnit>();
            if (unit == null || unit.team != CombatUnit.CombatTeam.Player) continue;
            Collider teammateBody = candidate.GetComponent<Collider>();
            float teammateOffset = teammateBody == null ? 0f :
                Mathf.Max(0f, candidate.transform.position.y - teammateBody.bounds.min.y);
            Vector3 teammateProbe = candidate.transform.position - Vector3.up * teammateOffset;
            if (NavMesh.SamplePosition(teammateProbe, out NavMeshHit teammateHit, 10f, NavMesh.AllAreas))
                teammatePoints.Add(teammateHit.position);
        }
        if (teammatePoints.Count == 0)
            throw new InvalidOperationException("No teammate NavMesh positions were available for Momoi.");

        Vector3 center = Vector3.zero;
        foreach (Vector3 point in teammatePoints) center += point;
        center /= teammatePoints.Count;

        var candidates = new List<Vector3>();
        Vector3[] directions =
        {
            Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            new Vector3(1f, 0f, 1f).normalized, new Vector3(-1f, 0f, 1f).normalized,
            new Vector3(1f, 0f, -1f).normalized, new Vector3(-1f, 0f, -1f).normalized
        };
        foreach (float radius in new[] { 0f, 2.5f, 5f })
            foreach (Vector3 direction in directions)
                if (NavMesh.SamplePosition(center + direction * radius, out NavMeshHit candidateHit, 8f, NavMesh.AllAreas))
                    if (!candidates.Any(value => FlatDistance(value, candidateHit.position) < 0.25f))
                        candidates.Add(candidateHit.position);

        Vector3 best = default;
        int bestReachable = -1;
        float bestScore = float.PositiveInfinity;
        foreach (Vector3 candidate in candidates)
        {
            int reachable = 0;
            foreach (Vector3 teammate in teammatePoints)
            {
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(candidate, teammate, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete) reachable++;
            }
            float nearestTeammate = teammatePoints.Min(point => FlatDistance(candidate, point));
            float spacingPenalty = Mathf.Abs(nearestTeammate - 3.5f);
            float score = spacingPenalty + FlatDistance(candidate, center) * 0.1f;
            if (reachable > bestReachable || reachable == bestReachable && score < bestScore)
            {
                bestReachable = reachable;
                bestScore = score;
                best = candidate;
            }
        }
        if (bestReachable <= 0)
            throw new InvalidOperationException("Could not place Momoi on a NavMesh region shared with the squad.");

        momoi.transform.position = best + Vector3.up * groundOffset;
        EditorUtility.SetDirty(momoi.transform);
    }

    private static void ConfigureObstacle(Collider collider)
    {
        GameObject obstacleObject = collider.gameObject;
        TacticalCoverObstacle cover = GetOrAdd<TacticalCoverObstacle>(obstacleObject);
        DestructibleCover destructible = GetOrAdd<DestructibleCover>(obstacleObject);
        destructible.Configure(180f);
        BlockingObstacle blocker = GetOrAdd<BlockingObstacle>(obstacleObject);
        blocker.blocksLineOfSight = true;

        Transform existing = FindDirectChild(obstacleObject.transform, GeneratedCoverRootName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        GameObject root = new GameObject(GeneratedCoverRootName);
        root.transform.SetParent(obstacleObject.transform, false);

        var positions = new List<Vector3>();
        Vector3[] directions =
        {
            obstacleObject.transform.right, -obstacleObject.transform.right,
            obstacleObject.transform.forward, -obstacleObject.transform.forward
        };
        foreach (Vector3 rawDirection in directions)
        {
            Vector3 direction = rawDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) continue;
            direction.Normalize();
            Ray ray = new Ray(collider.bounds.center + direction * 100f, -direction);
            if (!collider.Raycast(ray, out RaycastHit obstacleHit, 200f)) continue;
            Vector3 intended = obstacleHit.point + direction * 0.9f;
            if (!NavMesh.SamplePosition(intended, out NavMeshHit navHit, 3f, NavMesh.AllAreas)) continue;
            if (positions.Any(position => FlatDistance(position, navHit.position) < 0.8f)) continue;
            positions.Add(navHit.position);
        }

        if (positions.Count == 0)
            throw new InvalidOperationException(obstacleObject.name + " has no reachable tactical cover positions on the baked NavMesh.");

        for (int i = 0; i < positions.Count; i++)
        {
            GameObject pointObject = new GameObject("Cover Point " + (i + 1));
            pointObject.transform.SetParent(root.transform, true);
            pointObject.transform.position = positions[i];
            CoverPoint point = pointObject.AddComponent<CoverPoint>();
            point.Configure(collider, cover, 0.4f, 0.65f);
            EditorUtility.SetDirty(point);
        }

        EditorUtility.SetDirty(cover);
        EditorUtility.SetDirty(destructible);
        EditorUtility.SetDirty(blocker);
    }

    private static void ConfigureEnemyPrefabs()
    {
        foreach (string path in EnemyPrefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                AutoCombatAI ai = root.GetComponent<AutoCombatAI>();
                if (ai == null) throw new InvalidOperationException(path + " has no AutoCombatAI.");
                ai.separationDistance = 2f;
                ai.separationStrength = 1f;
                EditorUtility.SetDirty(ai);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private readonly struct TransformSnapshot
    {
        public readonly Transform transform;
        public readonly Vector3 position;
        public readonly Quaternion rotation;
        public readonly Vector3 scale;

        public TransformSnapshot(Transform value)
        {
            transform = value;
            position = value.localPosition;
            rotation = value.localRotation;
            scale = value.localScale;
        }
    }

    private static TransformSnapshot[] CaptureExistingTransforms(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
        .Where(item => item.name != GeneratedCoverRootName && item.GetComponentInParent<CoverPoint>() == null)
        .Select(item => new TransformSnapshot(item)).ToArray();

    private static void VerifyExistingTransforms(IEnumerable<TransformSnapshot> snapshots, Transform allowedPositionChange)
    {
        foreach (TransformSnapshot snapshot in snapshots)
        {
            if (snapshot.transform == null) continue;
            if (snapshot.transform == allowedPositionChange)
            {
                if (snapshot.transform.localRotation != snapshot.rotation || snapshot.transform.localScale != snapshot.scale)
                    throw new InvalidOperationException("Momoi's rotation or scale changed.");
                continue;
            }
            if (snapshot.transform.localPosition != snapshot.position ||
                snapshot.transform.localRotation != snapshot.rotation || snapshot.transform.localScale != snapshot.scale)
                throw new InvalidOperationException(snapshot.transform.name + " transform changed unexpectedly.");
        }
    }

    private static void RestoreExistingTransforms(IEnumerable<TransformSnapshot> snapshots, Transform allowedPositionChange)
    {
        foreach (TransformSnapshot snapshot in snapshots)
        {
            if (snapshot.transform == null || snapshot.transform == allowedPositionChange) continue;
            snapshot.transform.localPosition = snapshot.position;
            snapshot.transform.localRotation = snapshot.rotation;
            snapshot.transform.localScale = snapshot.scale;
        }
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);
        return null;
    }

    private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    private static GameObject FindObject(Scene scene, string objectName) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
        .FirstOrDefault(item => item.name == objectName)?.gameObject;
}
