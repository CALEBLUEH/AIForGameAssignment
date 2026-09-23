using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class KnockbackRetargetPlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string RunningKey = "AIForGame.KnockbackRetargetSmoke.Running";
    private const string PhaseKey = "AIForGame.KnockbackRetargetSmoke.Phase";
    private const string SelectionKey = "AIForGame.KnockbackRetargetSmoke.Selection";
    private static double checkAt;
    private static AutoCombatAI subject;
    private static CombatUnit firstTarget, secondTarget;
    private static Vector3 knockbackStart, knockbackDirection;

    static KnockbackRetargetPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Knockback and Retarget Play Mode Smoke")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before running the knockback/retarget smoke.");
        SessionState.SetString(SelectionKey, string.Join(",", GameProgress.GetSelectedCharacters()));
        GameProgress.SetSelectedCharacters(new[] { "Yuuka", "Ayane", "Mika", "Momoi" });
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(PhaseKey, 0);
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
            if (phase == 0) SetupRetargetScenario();
            else if (phase == 1 && EditorApplication.timeSinceStartup >= checkAt) SwapTargetDistances();
            else if (phase == 2 && EditorApplication.timeSinceStartup >= checkAt) CheckTargetStayedLocked();
            else if (phase == 3 && EditorApplication.timeSinceStartup >= checkAt) CheckRetargetAndBeginKnockback();
            else if (phase == 4 && EditorApplication.timeSinceStartup >= checkAt) CheckKnockbackMovement();
            else if (phase == 5 && EditorApplication.timeSinceStartup >= checkAt) Finish();
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

    private static void SetupRetargetScenario()
    {
        AutoCombatAI[] all = Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None);
        subject = all.FirstOrDefault(ai => ai.Unit.team == CombatUnit.CombatTeam.Player && ai.gameObject.activeInHierarchy);
        if (subject == null) return;
        foreach (AutoCombatAI ai in all)
        {
            if (ai == subject) continue;
            if (ai.Unit.team == CombatUnit.CombatTeam.Player) ai.enabled = false;
            else ai.gameObject.SetActive(false);
        }

        Vector3 origin = subject.NavMeshWorldPosition;
        Vector3 near = FindReachablePoint(origin, 2.5f, -1);
        Vector3 far = FindReachablePoint(origin, 6f, 1);
        firstTarget = CreateTarget("Retarget Smoke A", near);
        secondTarget = CreateTarget("Retarget Smoke B", far);
        subject.Unit.attackRange = 50f;
        subject.Unit.attackPower = 1f;
        subject.ActivateAtSpawn(origin);
        checkAt = EditorApplication.timeSinceStartup + 0.5d;
        SessionState.SetInt(PhaseKey, 1);
    }

    private static void SwapTargetDistances()
    {
        if (subject.CurrentTarget != firstTarget)
            throw new InvalidOperationException("The test unit did not initially acquire the closest target.");
        Vector3 firstPosition = firstTarget.transform.position;
        firstTarget.transform.position = secondTarget.transform.position;
        secondTarget.transform.position = firstPosition;
        checkAt = EditorApplication.timeSinceStartup + 4d;
        SessionState.SetInt(PhaseKey, 2);
    }

    private static void CheckTargetStayedLocked()
    {
        if (subject.CurrentTarget != firstTarget)
            throw new InvalidOperationException("The target changed before the five-second closest-target check.");
        checkAt = EditorApplication.timeSinceStartup + 1.4d;
        SessionState.SetInt(PhaseKey, 3);
    }

    private static void CheckRetargetAndBeginKnockback()
    {
        if (subject.CurrentTarget != secondTarget)
            throw new InvalidOperationException("The unit did not switch to the new closest target after five seconds.");

        knockbackStart = subject.NavMeshWorldPosition;
        Vector3 retreatPoint = FindReachablePoint(knockbackStart, 2.25f, 0);
        knockbackDirection = retreatPoint - knockbackStart;
        knockbackDirection.y = 0f;
        knockbackDirection.Normalize();
        subject.Unit.resistance = 1f;
        subject.Unit.TakeDamage(1f, knockbackStart - knockbackDirection * 2f);
        if (!subject.IsKnockedBack)
            throw new InvalidOperationException("A resistance-triggering hit did not start knockback retreat.");
        checkAt = EditorApplication.timeSinceStartup + 0.45d;
        SessionState.SetInt(PhaseKey, 4);
    }

    private static void CheckKnockbackMovement()
    {
        Vector3 moved = subject.NavMeshWorldPosition - knockbackStart;
        moved.y = 0f;
        if (Vector3.Dot(moved, knockbackDirection) < 0.15f || !subject.IsKnockedBack ||
            subject.CurrentState.IndexOf("Fleeing", StringComparison.OrdinalIgnoreCase) < 0)
            throw new InvalidOperationException("Knockback did not visibly flee away from the damage source over time.");
        checkAt = EditorApplication.timeSinceStartup + 0.55d;
        SessionState.SetInt(PhaseKey, 5);
    }

    private static void Finish()
    {
        if (subject.IsKnockedBack)
            throw new InvalidOperationException("Knockback did not release the unit back to normal combat.");
        Debug.Log("KNOCKBACK_RETARGET_PLAYMODE_OK five-second closest-target refresh and timed NavMesh flee retreat passed.");
        RestoreSelection();
        SessionState.SetInt(PhaseKey, 99);
        EditorApplication.isPlaying = false;
    }

    private static CombatUnit CreateTarget(string name, Vector3 position)
    {
        GameObject targetObject = new GameObject(name);
        targetObject.transform.position = position;
        CombatUnit target = targetObject.AddComponent<CombatUnit>();
        target.team = CombatUnit.CombatTeam.Enemy;
        target.ConfigureSpawn(10000f, 0f, 0f, false);
        return target;
    }

    private static Vector3 FindReachablePoint(Vector3 origin, float radius, int preferredQuarterTurn)
    {
        int start = preferredQuarterTurn < 0 ? 0 : preferredQuarterTurn * 4;
        for (int offset = 0; offset < 16; offset++)
        {
            int i = (start + offset) % 16;
            float angle = i * Mathf.PI * 2f / 16f;
            Vector3 requested = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, 1.2f, NavMesh.AllAreas)) continue;
            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete && Vector3.Distance(origin, hit.position) >= radius * 0.55f)
                return hit.position;
        }
        throw new InvalidOperationException("Could not find a reachable NavMesh point for the combat smoke.");
    }

    private static void RestoreSelection()
    {
        string saved = SessionState.GetString(SelectionKey, string.Empty);
        if (!string.IsNullOrEmpty(saved)) GameProgress.SetSelectedCharacters(saved.Split(','));
    }
}
