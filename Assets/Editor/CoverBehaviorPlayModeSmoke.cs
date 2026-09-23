using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CoverBehaviorPlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string RunningKey = "AIForGame.CoverBehaviorSmoke.Running";
    private const string PhaseKey = "AIForGame.CoverBehaviorSmoke.Phase";

    static CoverBehaviorPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Cover Behavior Play Mode Smoke")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(PhaseKey, 0);
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (!EditorApplication.isPlaying)
        {
            if (SessionState.GetInt(PhaseKey, 0) != 99) return;
            SessionState.EraseBool(RunningKey);
            SessionState.EraseInt(PhaseKey);
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }
        try
        {
            BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
            if (director == null || !director.IsPlaying) return;
            if (!Validate()) return;
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static bool Validate()
    {
        AutoCombatAI[] all = Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None);
        AutoCombatAI player = all.FirstOrDefault(ai => ai.gameObject.activeInHierarchy && ai.Unit.team == CombatUnit.CombatTeam.Player &&
            ai.role != AutoCombatAI.CombatRole.YuukaTank);
        AutoCombatAI enemy = all.FirstOrDefault(ai => ai.gameObject.activeInHierarchy && ai.Unit.team == CombatUnit.CombatTeam.Enemy);
        if (player == null || enemy == null) return false;
        foreach (AutoCombatAI ai in all) ai.enabled = false;

        CoverPoint point = CoverPoint.All.FirstOrDefault(item => item != null && item.Obstacle != null);
        if (point == null) return false;
        TacticalCoverObstacle obstacle = point.Obstacle;
        GameObject secondPointObject = new GameObject("Cover Smoke Second Face");
        secondPointObject.transform.SetParent(point.transform.parent, false);
        secondPointObject.transform.position = point.transform.position + point.transform.forward * 0.1f;
        CoverPoint otherPoint = secondPointObject.AddComponent<CoverPoint>();
        otherPoint.Configure(point.coverCollider, obstacle, 1f, point.occupancyRadius);
        if (!point.TryReserve(player.Unit) || otherPoint.TryReserve(enemy.Unit) || obstacle.ReservedCount != 1)
            throw new InvalidOperationException("Physical obstacle capacity did not block a second approach reservation.");
        point.Release(player.Unit);

        DestructibleCover health = obstacle.GetComponent<DestructibleCover>();
        if (health == null || obstacle.GetComponentInChildren<CoverHealthHUD>(true) == null)
            throw new InvalidOperationException("Obstacle health or its world-space HUD is missing.");
        foreach (TacticalCoverObstacle sceneObstacle in Object.FindObjectsByType<TacticalCoverObstacle>(FindObjectsSortMode.None))
            if (sceneObstacle.gameObject.activeInHierarchy &&
                (sceneObstacle.GetComponent<DestructibleCover>() == null ||
                 sceneObstacle.GetComponentInChildren<CoverHealthHUD>(true) == null))
                throw new InvalidOperationException(sceneObstacle.name + " does not share universal cover health/HUD behaviour.");

        MethodInfo shouldUseCover = typeof(AutoCombatAI).GetMethod("ShouldUseCover", BindingFlags.Instance | BindingFlags.NonPublic);
        bool oldEnemyPermission = enemy.enemyCanUseCover;
        enemy.enemyCanUseCover = false;
        if ((bool)shouldUseCover.Invoke(enemy, null))
            throw new InvalidOperationException("A non-Sensei enemy was allowed to seek cover.");
        enemy.enemyCanUseCover = true;
        if (enemy.coverPreference != AutoCombatAI.CoverPreference.Never && !(bool)shouldUseCover.Invoke(enemy, null))
            throw new InvalidOperationException("Sensei-style enemy cover permission did not enable cover seeking.");
        enemy.enemyCanUseCover = oldEnemyPermission;

        float initialHealth = health.CurrentHealth;
        Vector3 coverCenter = point.coverCollider.bounds.center;
        DestructibleCover.DamageInRadius(coverCenter, 0.25f, 4f);
        if (health.CurrentHealth >= initialHealth)
            throw new InvalidOperationException("Airstrike-style radius damage did not hit the obstacle collider volume.");
        float afterRadius = health.CurrentHealth;
        Vector3 coneOrigin = coverCenter - Vector3.forward * 5f;
        DestructibleCover.DamageInCone(coneOrigin, Vector3.forward, 8f, 30f, 4f);
        if (health.CurrentHealth >= afterRadius)
            throw new InvalidOperationException("Momoi/Hina-style cone damage did not hit the obstacle.");
        float initialPlayerHealth = player.Unit.CurrentHealth;
        if (!point.TryReserve(player.Unit)) throw new InvalidOperationException("Player could not re-reserve cover.");
        point.Occupy(player.Unit);
        player.Unit.TakeDamage(25f, enemy.transform.position);
        if (health.CurrentHealth >= initialHealth)
            throw new InvalidOperationException("Enemy damage against a covered player did not reduce universal obstacle health.");
        if (player.Unit.CurrentHealth < initialPlayerHealth)
            throw new InvalidOperationException("Damage leaked through cover before its health was depleted.");
        point.Release(player.Unit);
        float afterEnemyDamage = health.CurrentHealth;
        if (!point.TryReserve(enemy.Unit)) throw new InvalidOperationException("Enemy could not reserve released cover.");
        point.Occupy(enemy.Unit);
        enemy.Unit.TakeDamage(25f, player.transform.position);
        if (health.CurrentHealth >= afterEnemyDamage)
            throw new InvalidOperationException("Player damage against a covered enemy did not reduce universal obstacle health.");
        point.Release(enemy.Unit);

        if (!point.TryGetSafeStandPosition(player.Unit, out Vector3 standPosition))
            throw new InvalidOperationException("Cover point did not produce a NavMesh-safe stand position.");
        Collider playerBody = player.GetComponent<Collider>() ?? player.GetComponentInChildren<Collider>(true);
        Vector3 closestToBody = point.coverCollider.ClosestPoint(standPosition);
        closestToBody.y = standPosition.y;
        if (playerBody != null && Vector3.Distance(standPosition, closestToBody) < 0.1f)
            throw new InvalidOperationException("Unit-sized safe stand position still intersects the obstacle.");
        player.SetNavMeshPosition(standPosition);
        if (!point.TryReserve(player.Unit)) throw new InvalidOperationException("Player could not reserve cover for stay-time validation.");
        SetPrivate(player, "coverPoint", point);
        SetPrivate(player, "currentTarget", enemy.Unit);
        SetPrivate(player, "coverReservedAt", Time.time);
        MethodInfo handleCover = typeof(AutoCombatAI).GetMethod("HandleCover", BindingFlags.Instance | BindingFlags.NonPublic);
        if (!(bool)handleCover.Invoke(player, null) || !player.IsOccupyingCover)
            throw new InvalidOperationException("AI did not enter the occupied-cover state at its safe point.");

        Vector3 outward = standPosition - point.coverCollider.bounds.center;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f) outward = point.transform.forward;
        float firingDistance = Mathf.Max(1f, player.Unit.AttackRange * 0.7f);
        enemy.transform.position = standPosition - outward.normalized * firingDistance;
        SetPrivate(player, "coverReachedAt", Time.time - 1.1f);
        SetPrivate(player, "nextAttack", 0f);
        float enemyHealthBeforeCoverAttack = enemy.Unit.CurrentHealth;
        Vector3 occupiedPosition = player.transform.position;
        if (!(bool)handleCover.Invoke(player, null) || player.ReservedCover == null ||
            enemy.Unit.CurrentHealth >= enemyHealthBeforeCoverAttack)
            throw new InvalidOperationException("Occupied unit did not stay and attack an in-range enemy from cover.");
        if (Vector3.Distance(occupiedPosition, player.transform.position) > 0.01f)
            throw new InvalidOperationException("Occupied unit moved while attacking from cover.");

        SetPrivate(player, "coverReachedAt", Time.time);
        enemy.transform.position = standPosition + Vector3.one * 500f;
        if (!(bool)handleCover.Invoke(player, null) || player.ReservedCover == null)
            throw new InvalidOperationException("AI left cover before the one-second minimum stay completed.");

        SetPrivate(player, "coverReachedAt", Time.time - 1.1f);
        handleCover.Invoke(player, null);
        if (player.ReservedCover != null || !obstacle.IsAbandonedFor(player.Unit.team))
            throw new InvalidOperationException("AI did not leave and team-mark cover used after the minimum stay with no useful target.");

        Debug.Log("COVER_BEHAVIOR_PLAYMODE_OK shared prefab/skill obstacle health and HUD, radius/cone skill damage, Sensei-only enemy permission, one reservation, unit-sized safe stand position, stationary cover attack, one-second minimum stay, team-used marking and full damage absorption passed.");
        return true;
    }

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (info == null) throw new MissingFieldException(target.GetType().Name, field);
        info.SetValue(target, value);
    }
}
