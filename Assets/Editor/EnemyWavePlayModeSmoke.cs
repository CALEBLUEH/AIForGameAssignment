using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyWavePlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string RunningKey = "AIForGame.EnemyWaveSmoke.Running";
    private const string PhaseKey = "AIForGame.EnemyWaveSmoke.Phase";
    private const string SuccessKey = "AIForGame.EnemyWaveSmoke.Success";
    private static double deadline;
    private static double earliestCheck;

    static EnemyWavePlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Enemy Wave Play Mode Smoke")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before starting the enemy-wave smoke test.");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.SetBool(SuccessKey, false);
        deadline = 0d;
        earliestCheck = 0d;
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;

        int phase = SessionState.GetInt(PhaseKey, 0);
        if (!EditorApplication.isPlaying)
        {
            if (phase != 99) return;
            bool success = SessionState.GetBool(SuccessKey, false);
            SessionState.EraseBool(RunningKey);
            SessionState.EraseInt(PhaseKey);
            SessionState.EraseBool(SuccessKey);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            return;
        }

        try
        {
            if (deadline <= 0d)
            {
                deadline = EditorApplication.timeSinceStartup + 15d;
                earliestCheck = EditorApplication.timeSinceStartup + 1d;
            }
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Enemy-wave Play Mode smoke test timed out in phase " + phase + ".");
            if (EditorApplication.timeSinceStartup < earliestCheck) return;

            BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
            EnemyWaveSpawner spawner = Object.FindFirstObjectByType<EnemyWaveSpawner>();
            if (director == null || spawner == null || !director.IsPlaying) return;

            CombatUnit[] enemies = ActiveEnemies();
            if (phase == 0)
            {
                if (spawner.CurrentWave != 1 || enemies.Length != 3) return;
                ValidateUnitsAndPaths(enemies, 1);
                foreach (CombatUnit enemy in enemies) enemy.TakeDamage(999999f);
                SessionState.SetInt(PhaseKey, 1);
                deadline = EditorApplication.timeSinceStartup + 8d;
                earliestCheck = EditorApplication.timeSinceStartup + 2.25d;
            }
            else if (phase == 1)
            {
                if (spawner.CurrentWave != 2 || enemies.Length != 3) return;
                ValidateUnitsAndPaths(enemies, 2);
                Debug.Log("ENEMY_WAVE_PLAYMODE_OK: wave 1 and wave 2 spawned 3 grounded, pursuing, reachable enemies each.");
                SessionState.SetBool(SuccessKey, true);
                SessionState.SetInt(PhaseKey, 99);
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(SuccessKey, false);
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
        }
    }

    private static CombatUnit[] ActiveEnemies()
    {
        return Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None)
            .Where(unit => unit.team == CombatUnit.CombatTeam.Enemy && !unit.IsDead)
            .ToArray();
    }

    private static void ValidateUnitsAndPaths(CombatUnit[] enemies, int wave)
    {
        CombatUnit player = Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.None)
            .FirstOrDefault(unit => unit.team == CombatUnit.CombatTeam.Player && !unit.IsDead);
        if (player == null) throw new InvalidOperationException("No active player unit was available for path validation.");
        if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit playerHit, 3f, NavMesh.AllAreas))
            throw new InvalidOperationException("The player squad is not on the baked NavMesh.");

        foreach (CombatUnit enemy in enemies)
        {
            AutoCombatAI ai = enemy.GetComponent<AutoCombatAI>();
            CapsuleCollider body = enemy.GetComponent<CapsuleCollider>();
            if (ai == null || body == null) throw new InvalidOperationException(enemy.name + " is missing its AI or capsule body.");
            if (ai.CurrentState == "Guarding" || ai.CurrentState == "No path" || ai.CurrentState == "Waiting")
                throw new InvalidOperationException(enemy.name + " did not begin pursuing the player; state=" + ai.CurrentState);
            if (!NavMesh.SamplePosition(enemy.transform.position, out NavMeshHit enemyHit, 3f, NavMesh.AllAreas))
                throw new InvalidOperationException(enemy.name + " is not on the baked NavMesh.");
            if (Mathf.Abs(body.bounds.min.y - enemyHit.position.y) > 0.08f)
                throw new InvalidOperationException(enemy.name + " is sunk into or floating above the road.");

            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(playerHit.position, enemyHit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException($"Wave {wave} enemy {enemy.name} is unreachable from the player squad.");
        }
    }
}
