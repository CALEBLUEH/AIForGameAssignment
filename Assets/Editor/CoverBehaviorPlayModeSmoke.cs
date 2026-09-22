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
            Validate();
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

    private static void Validate()
    {
        AutoCombatAI[] all = Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None);
        AutoCombatAI player = all.First(ai => ai.gameObject.activeInHierarchy && ai.Unit.team == CombatUnit.CombatTeam.Player &&
            ai.role != AutoCombatAI.CombatRole.YuukaTank);
        AutoCombatAI enemy = all.First(ai => ai.gameObject.activeInHierarchy && ai.Unit.team == CombatUnit.CombatTeam.Enemy);
        foreach (AutoCombatAI ai in all) ai.enabled = false;

        TacticalCoverObstacle obstacle = Object.FindObjectsByType<TacticalCoverObstacle>(FindObjectsSortMode.None)
            .First(item => item.GetComponentsInChildren<CoverPoint>(true).Length >= 2);
        CoverPoint[] points = obstacle.GetComponentsInChildren<CoverPoint>(true);
        CoverPoint point = points[0];
        CoverPoint otherPoint = points[1];
        if (!point.TryReserve(player.Unit) || otherPoint.TryReserve(enemy.Unit) || obstacle.ReservedCount != 1)
            throw new InvalidOperationException("Physical obstacle capacity did not block a second approach reservation.");
        point.Release(player.Unit);

        DestructibleCover health = obstacle.GetComponent<DestructibleCover>();
        if (health == null || obstacle.GetComponentInChildren<CoverHealthHUD>(true) == null)
            throw new InvalidOperationException("Obstacle health or its world-space HUD is missing.");
        float initialHealth = health.CurrentHealth;
        if (!point.TryReserve(player.Unit)) throw new InvalidOperationException("Player could not re-reserve cover.");
        point.Occupy(player.Unit);
        player.Unit.TakeDamage(25f, enemy.transform.position);
        if (health.CurrentHealth >= initialHealth)
            throw new InvalidOperationException("Enemy damage against a covered player did not reduce universal obstacle health.");
        point.Release(player.Unit);
        float afterEnemyDamage = health.CurrentHealth;
        if (!point.TryReserve(enemy.Unit)) throw new InvalidOperationException("Enemy could not reserve released cover.");
        point.Occupy(enemy.Unit);
        enemy.Unit.TakeDamage(25f, player.transform.position);
        if (health.CurrentHealth >= afterEnemyDamage)
            throw new InvalidOperationException("Player damage against a covered enemy did not reduce universal obstacle health.");
        point.Release(enemy.Unit);

        if (!point.TryGetSafeStandPosition(out Vector3 standPosition))
            throw new InvalidOperationException("Cover point did not produce a NavMesh-safe stand position.");
        player.SetNavMeshPosition(standPosition);
        if (!point.TryReserve(player.Unit)) throw new InvalidOperationException("Player could not reserve cover for stay-time validation.");
        SetPrivate(player, "coverPoint", point);
        SetPrivate(player, "currentTarget", enemy.Unit);
        SetPrivate(player, "coverReservedAt", Time.time);
        MethodInfo handleCover = typeof(AutoCombatAI).GetMethod("HandleCover", BindingFlags.Instance | BindingFlags.NonPublic);
        if (!(bool)handleCover.Invoke(player, null) || !player.IsOccupyingCover)
            throw new InvalidOperationException("AI did not enter the occupied-cover state at its safe point.");
        enemy.transform.position = standPosition + Vector3.one * 500f;
        if (!(bool)handleCover.Invoke(player, null) || player.ReservedCover == null)
            throw new InvalidOperationException("AI left cover before the one-second minimum stay completed.");

        SetPrivate(player, "coverReachedAt", Time.time - 1.1f);
        handleCover.Invoke(player, null);
        if (player.ReservedCover != null || !obstacle.IsAbandonedFor(player.Unit.team))
            throw new InvalidOperationException("AI did not leave and team-mark cover used after the minimum stay with no useful target.");

        Debug.Log("COVER_BEHAVIOR_PLAYMODE_OK one reservation per physical obstacle, approach marking, safe stand position, one-second minimum stay, team-used marking, bidirectional damage and obstacle HUD passed.");
    }

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (info == null) throw new MissingFieldException(target.GetType().Name, field);
        info.SetValue(target, value);
    }
}
