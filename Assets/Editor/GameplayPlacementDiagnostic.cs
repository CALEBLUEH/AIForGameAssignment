using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class GameplayPlacementDiagnostic
{
    private const string ScenePath = "Assets/Scenes/Gameplay_Level1.unity";
    private const string RunningKey = "AIForGame.GameplayPlacementDiagnostic.Running";
    private static double sampleUntil;
    private static float largestColliderGroundError;
    private static float lowestVisualGroundError = float.PositiveInfinity;
    private static int sampledUnits;
    private static int missingRootColliders;
    private static int coverPenetrations;

    static GameplayPlacementDiagnostic()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Diagnose Gameplay Grounding And Cover Overlap")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before running the placement diagnostic.");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        sampleUntil = 0d;
        largestColliderGroundError = 0f;
        lowestVisualGroundError = float.PositiveInfinity;
        sampledUnits = missingRootColliders = coverPenetrations = 0;
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (!EditorApplication.isPlaying)
        {
            SessionState.EraseBool(RunningKey);
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }

        BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
        if (director == null || !director.IsPlaying) return;
        if (sampleUntil <= 0d) sampleUntil = EditorApplication.timeSinceStartup + 10d;

        SamplePlayers();
        if (EditorApplication.timeSinceStartup < sampleUntil) return;

        Debug.Log("GAMEPLAY_PLACEMENT_DIAGNOSTIC samples=" + sampledUnits +
            " missingRootColliders=" + missingRootColliders +
            " largestColliderGroundError=" + largestColliderGroundError.ToString("F4") +
            " lowestVisualGroundError=" + lowestVisualGroundError.ToString("F4") +
            " coverPenetrations=" + coverPenetrations + ".");
        SessionState.SetBool(RunningKey, false);
        EditorApplication.isPlaying = false;
    }

    private static void SamplePlayers()
    {
        TacticalCoverObstacle[] covers = Object.FindObjectsByType<TacticalCoverObstacle>(FindObjectsSortMode.None);
        foreach (AutoCombatAI ai in Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None))
        {
            if (ai.Unit == null || ai.Unit.IsDead || ai.Unit.team != CombatUnit.CombatTeam.Player) continue;
            sampledUnits++;
            float groundY = ai.NavMeshWorldPosition.y;
            Collider body = ai.GetComponent<Collider>();
            if (body == null)
            {
                missingRootColliders++;
                continue;
            }

            largestColliderGroundError = Mathf.Max(largestColliderGroundError, Mathf.Abs(body.bounds.min.y - groundY));
            foreach (SkinnedMeshRenderer model in ai.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                lowestVisualGroundError = Mathf.Min(lowestVisualGroundError, model.bounds.min.y - groundY);

            foreach (TacticalCoverObstacle cover in covers)
            foreach (Collider obstacle in cover.GetComponentsInChildren<Collider>(true))
            {
                if (!obstacle.enabled || obstacle.isTrigger) continue;
                if (Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out float distance) &&
                    distance > 0.01f)
                    coverPenetrations++;
            }
        }
    }
}
