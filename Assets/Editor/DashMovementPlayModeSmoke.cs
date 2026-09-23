using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DashMovementPlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string RunningKey = "AIForGame.DashSmoke.Running";
    private const string PhaseKey = "AIForGame.DashSmoke.Phase";

    static DashMovementPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Dash Movement Play Mode Smoke")]
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
        AutoCombatAI ai = Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None)
            .First(candidate => candidate.gameObject.activeInHierarchy &&
                candidate.Unit.team == CombatUnit.CombatTeam.Player && !candidate.Unit.IsDead);
        ai.enabled = false;
        typeof(AutoCombatAI).GetField("movementReadyAt", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(ai, 0f);
        typeof(AutoCombatAI).GetField("skillMoving", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(ai, false);
        WorldUnitHUD hud = ai.GetComponentInChildren<WorldUnitHUD>(true);
        if (hud == null || hud.staminaSlider == null || hud.staminaFill == null)
            throw new InvalidOperationException("Player world HUD has no serialized stamina bar.");
        if (hud.healthFill == null || hud.healthFill.color.g <= hud.healthFill.color.r)
            throw new InvalidOperationException("Player health bar is not green.");
        if (hud.staminaFill.color.r <= hud.staminaFill.color.g)
            throw new InvalidOperationException("Movement stamina bar is not orange.");
        if (!ai.Unit.IsMovementGaugeFull)
            throw new InvalidOperationException("Movement stamina did not begin full.");

        Collider selectableBody = ai.GetComponent<Collider>() ?? ai.GetComponentInChildren<Collider>(true);
        Vector3 selectableCenter = selectableBody == null ? ai.transform.position + Vector3.up : selectableBody.bounds.center;
        Vector3 selectableScreen = Camera.main.WorldToScreenPoint(selectableCenter);
        MethodInfo fallbackPicker = typeof(BattleDirector).GetMethod("FindMovementDragCandidateFromScreen",
            BindingFlags.Instance | BindingFlags.NonPublic);
        AutoCombatAI fallbackResult = (AutoCombatAI)fallbackPicker.Invoke(BattleDirector.Instance,
            new object[] { new Vector2(selectableScreen.x, selectableScreen.y) });
        if (fallbackResult == null || fallbackResult.Unit.team != CombatUnit.CombatTeam.Player)
            throw new InvalidOperationException("Screen-space movement selection could not recover a character hidden by cover.");

        Vector3 origin = ai.NavMeshWorldPosition;
        Vector3 direction = Vector3.zero;
        Vector3 requested = origin;
        for (int i = 0; i < 8; i++)
        {
            Vector3 candidateDirection = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
            Vector3 candidate = origin + candidateDirection * Mathf.Max(3f, ai.movementRange * 0.8f);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 1f, NavMesh.AllAreas)) continue;
            if (FlatDistance(origin, ai.ResolveDashDestination(navHit.position)) < 3.5f) continue;
            direction = candidateDirection;
            requested = navHit.position;
            break;
        }
        if (direction == Vector3.zero)
            throw new InvalidOperationException("Could not find a straight NavMesh direction for dash validation.");

        GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "Dash Smoke Blocker";
        blocker.transform.position = origin + direction * 3.2f + Vector3.up * 0.6f;
        blocker.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        blocker.AddComponent<BlockingObstacle>();
        Physics.SyncTransforms();

        Vector3 resolved = ai.ResolveDashDestination(requested);
        float resolvedDistance = FlatDistance(origin, resolved);
        if (resolvedDistance < 0.2f || resolvedDistance >= 3f)
            throw new InvalidOperationException("Dash preview did not stop before a blockade.");
        if (!ai.TryMovementSkill(requested))
            throw new InvalidOperationException($"A full movement gauge could not start Dash. resolvedDistance={resolvedDistance:0.###}, gauge={ai.Unit.MovementPoints:0.###}, cooldown={ai.MovementCooldownRemaining:0.###}, dashing={ai.IsDashing}.");
        if (ai.Unit.MovementPoints > 0.01f || !ai.IsDashing)
            throw new InvalidOperationException("Dash did not spend the full movement gauge and enter its movement state.");

        Debug.Log("DASH_MOVEMENT_PLAYMODE_OK green health, orange refill gauge, cover-occlusion selection fallback, full-gauge Dash, straight endpoint and blockade stop passed.");
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
