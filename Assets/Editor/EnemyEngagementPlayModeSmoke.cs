using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyEngagementPlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string RunningKey = "AIForGame.EnemyEngagementSmoke.Running";
    private const string PhaseKey = "AIForGame.EnemyEngagementSmoke.Phase";
    private const string SelectionKey = "AIForGame.EnemyEngagementSmoke.Selection";
    private static readonly Dictionary<int, float> initialDistances = new Dictionary<int, float>();
    private static double checkAt;
    private static CombatUnit player;
    private static float playerHealthBefore;

    static EnemyEngagementPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Enemy Engagement Play Mode Smoke")]
    public static void Run()
    {
        SessionState.SetString(SelectionKey, string.Join(",", GameProgress.GetSelectedCharacters()));
        GameProgress.SetSelectedCharacters(new[] { "Yuuka", "Ayane", "Mika", "Momoi" });
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(PhaseKey, 0);
        initialDistances.Clear();
        checkAt = 0d;
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (!EditorApplication.isPlaying)
        {
            if (SessionState.GetInt(PhaseKey, 0) != 99) return;
            RestoreSelection();
            SessionState.EraseBool(RunningKey);
            SessionState.EraseInt(PhaseKey);
            SessionState.EraseString(SelectionKey);
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }

        try
        {
            BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
            if (director == null || !director.IsPlaying) return;
            int phase = SessionState.GetInt(PhaseKey, 0);
            if (phase == 0) BeginPursuitCheck();
            else if (phase == 1 && EditorApplication.timeSinceStartup >= checkAt) BeginAttackCheck();
            else if (phase == 2 && EditorApplication.timeSinceStartup >= checkAt) FinishAttackCheck();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreSelection();
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void BeginPursuitCheck()
    {
        AutoCombatAI[] all = Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None);
        AutoCombatAI[] players = all.Where(ai => ai.Unit.team == CombatUnit.CombatTeam.Player && ai.gameObject.activeInHierarchy).ToArray();
        AutoCombatAI[] enemies = all.Where(ai => ai.Unit.team == CombatUnit.CombatTeam.Enemy && ai.gameObject.activeInHierarchy).ToArray();
        if (players.Length == 0 || enemies.Length == 0) return;

        player = players[0].Unit;
        foreach (AutoCombatAI playerAI in players) playerAI.enabled = false;
        initialDistances.Clear();
        foreach (AutoCombatAI enemy in enemies)
            initialDistances[enemy.GetInstanceID()] = FlatDistance(enemy.transform.position, player.transform.position);
        checkAt = EditorApplication.timeSinceStartup + 4d;
        SessionState.SetInt(PhaseKey, 1);
    }

    private static void BeginAttackCheck()
    {
        AutoCombatAI[] enemies = Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None)
            .Where(ai => ai.Unit.team == CombatUnit.CombatTeam.Enemy && !ai.Unit.IsDead).ToArray();
        if (enemies.Length == 0 || player == null) throw new InvalidOperationException("Enemy/player disappeared during pursuit check.");

        string report = string.Join("; ", enemies.Select(enemy =>
        {
            float before = initialDistances.TryGetValue(enemy.GetInstanceID(), out float value) ? value : -1f;
            float after = FlatDistance(enemy.transform.position, player.transform.position);
            bool startFound = NavMesh.SamplePosition(enemy.NavMeshWorldPosition, out NavMeshHit start, 3f, NavMesh.AllAreas);
            CombatUnit actualTarget = enemy.CurrentTarget;
            AutoCombatAI playerNavigation = actualTarget == null ? null : actualTarget.GetComponent<AutoCombatAI>();
            Vector3 playerGround = playerNavigation == null ?
                (actualTarget == null ? Vector3.positiveInfinity : actualTarget.transform.position) : playerNavigation.NavMeshWorldPosition;
            NavMeshHit end = default;
            bool endFound = actualTarget != null && NavMesh.SamplePosition(playerGround, out end, 3f, NavMesh.AllAreas);
            var path = new NavMeshPath();
            bool pathFound = startFound && endFound && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path);
            return $"{enemy.name}: {before:F2}->{after:F2} ({enemy.CurrentState}), " +
                $"enemy={enemy.NavMeshWorldPosition}, target={actualTarget?.name}@{playerGround}, samples={startFound}/{endFound}, path={pathFound}/{path.status}";
        }));
        if (!enemies.Any(enemy => initialDistances.TryGetValue(enemy.GetInstanceID(), out float before) &&
            before - FlatDistance(enemy.transform.position, player.transform.position) > 1f))
            throw new InvalidOperationException("No enemy closed distance toward the stationary player. " + report);

        AutoCombatAI attacker = enemies[0];
        Vector3 forward = player.transform.position - attacker.transform.position;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Vector3 desired = attacker.transform.position + forward.normalized * Mathf.Max(1f, attacker.Unit.AttackRange * 0.55f);
        if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            throw new InvalidOperationException("Could not place the player near an enemy for the attack check. " + report);
        AutoCombatAI playerAI = player.GetComponent<AutoCombatAI>();
        playerAI.SetNavMeshPosition(hit.position);
        playerHealthBefore = player.CurrentHealth;
        checkAt = EditorApplication.timeSinceStartup + 3d;
        SessionState.SetInt(PhaseKey, 2);
        Debug.Log("ENEMY_ENGAGEMENT_PURSUIT " + report);
    }

    private static void FinishAttackCheck()
    {
        AutoCombatAI[] enemies = Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None)
            .Where(ai => ai.Unit.team == CombatUnit.CombatTeam.Enemy && !ai.Unit.IsDead).ToArray();
        string states = string.Join(", ", enemies.Select(enemy => enemy.name + "=" + enemy.CurrentState));
        if (player == null || player.CurrentHealth >= playerHealthBefore)
        {
            string healthAfter = player == null ? "missing" : player.CurrentHealth.ToString("F1");
            throw new InvalidOperationException($"Nearby enemies did not damage the player: HP {playerHealthBefore:F1}->{healthAfter}; {states}");
        }
        Debug.Log($"ENEMY_ENGAGEMENT_PLAYMODE_OK pursuit and attack passed; HP {playerHealthBefore:F1}->{player.CurrentHealth:F1}; {states}");
        RestoreSelection();
        SessionState.SetInt(PhaseKey, 99);
        EditorApplication.isPlaying = false;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static void RestoreSelection()
    {
        string selection = SessionState.GetString(SelectionKey, string.Empty);
        if (!string.IsNullOrEmpty(selection)) GameProgress.SetSelectedCharacters(selection.Split(','));
    }
}
