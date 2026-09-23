using System;
using System.Linq;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class LevelOpeningPlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string RunningKey = "AIForGame.LevelOpeningSmoke.Running";
    private const string PhaseKey = "AIForGame.LevelOpeningSmoke.Phase";
    private const string OpeningObservedAtKey = "AIForGame.LevelOpeningSmoke.OpeningObservedAt";

    static LevelOpeningPlayModeSmoke() => EditorApplication.update += Update;

    [MenuItem("Tools/AI For Game/Run Level Opening Play Mode Smoke")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.EraseString(OpeningObservedAtKey);
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
            SessionState.EraseString(OpeningObservedAtKey);
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }
        try
        {
            int phase = SessionState.GetInt(PhaseKey, 0);
            string observedText = SessionState.GetString(OpeningObservedAtKey, string.Empty);
            if (string.IsNullOrEmpty(observedText))
            {
                LevelOpeningSequence sequence = UnityEngine.Object.FindFirstObjectByType<LevelOpeningSequence>();
                if (sequence == null || !sequence.IsPlaying) return;
                observedText = EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture);
                SessionState.SetString(OpeningObservedAtKey, observedText);
                return;
            }
            double openingElapsed = EditorApplication.timeSinceStartup -
                double.Parse(observedText, CultureInfo.InvariantCulture);
            if (openingElapsed > 12d)
                throw new TimeoutException("Opening transition did not complete within twelve seconds.");
            if (phase == 0 && openingElapsed > 0.35d)
            {
                ValidateOpening();
                SessionState.SetInt(PhaseKey, 1);
            }
            if (phase == 1 && openingElapsed > 2d)
            {
                BattleDirector director = UnityEngine.Object.FindFirstObjectByType<BattleDirector>();
                LevelOpeningSequence sequence = UnityEngine.Object.FindFirstObjectByType<LevelOpeningSequence>();
                if (director == null || sequence == null || !director.IsPlaying ||
                    !sequence.BattleAnnouncementPlaying) return;
                ValidateGameplay();
                SessionState.SetInt(PhaseKey, 2);
            }
            if (phase == 2)
            {
                LevelOpeningSequence sequence = UnityEngine.Object.FindFirstObjectByType<LevelOpeningSequence>();
                if (sequence == null || sequence.BattleAnnouncementPlaying) return;
                ValidateAnnouncementFinished();
                Debug.Log("LEVEL_OPENING_PLAYMODE_OK black reveal, four-character idle opening, camera handoff, concurrent BATTLE announcement, and dim restoration passed.");
                Finish();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void ValidateOpening()
    {
        BattleDirector director = UnityEngine.Object.FindFirstObjectByType<BattleDirector>();
        LevelOpeningSequence sequence = UnityEngine.Object.FindFirstObjectByType<LevelOpeningSequence>();
        if (director == null || sequence == null || !sequence.IsPlaying || director.IsPlaying)
            throw new InvalidOperationException("Gameplay was not held during the opening sequence.");
        if (!sequence.OpeningCamera.enabled || sequence.GameplayCamera.enabled)
            throw new InvalidOperationException("Opening/main camera handoff began in the wrong state.");
        if (sequence.AppliedPitchDecrease <= 0.1f)
            throw new InvalidOperationException("Opening camera did not begin its authored negative X pitch motion.");
        AutoCombatAI[] activePlayers = UnityEngine.Object.FindObjectsByType<AutoCombatAI>(FindObjectsSortMode.None)
            .Where(ai => ai.GetComponent<CombatUnit>().team == CombatUnit.CombatTeam.Player).OrderBy(ai => ai.rosterOrder).ToArray();
        if (activePlayers.Length != 4) throw new InvalidOperationException("Opening did not deploy exactly four chosen characters.");
        if (activePlayers.Any(ai => ai.CurrentState != "Waiting"))
            throw new InvalidOperationException("A chosen character left Idle/Waiting before gameplay started.");
        for (int i = 0; i < activePlayers.Length; i++)
            if (Vector3.Distance(activePlayers[i].transform.position, sequence.SquadSpawnPoints[i].position) > 4.5f)
                throw new InvalidOperationException("A chosen character did not spawn near its authored squad marker.");
    }

    private static void ValidateGameplay()
    {
        BattleDirector director = UnityEngine.Object.FindFirstObjectByType<BattleDirector>();
        LevelOpeningSequence sequence = UnityEngine.Object.FindFirstObjectByType<LevelOpeningSequence>();
        if (director == null || sequence == null || !director.IsPlaying || sequence.IsPlaying)
            throw new InvalidOperationException("Gameplay did not begin after the opening sequence.");
        if (sequence.OpeningCamera.enabled || !sequence.GameplayCamera.enabled)
            throw new InvalidOperationException("Camera did not return to the main gameplay camera.");
        if (!sequence.BattleAnnouncementPlaying)
            throw new InvalidOperationException("BATTLE announcement was not playing over the active battle.");
        GameObject text = GameObject.Find("Battle Announcement Text");
        GameObject dim = GameObject.Find("Battle Announcement Dim");
        if (text == null || dim == null || !text.activeInHierarchy || !dim.activeInHierarchy)
            throw new InvalidOperationException("BATTLE text or dim overlay was not visible during the gameplay handoff.");
    }

    private static void ValidateAnnouncementFinished()
    {
        BattleDirector director = UnityEngine.Object.FindFirstObjectByType<BattleDirector>();
        LevelOpeningSequence sequence = UnityEngine.Object.FindFirstObjectByType<LevelOpeningSequence>();
        if (director == null || sequence == null || !director.IsPlaying || sequence.BattleAnnouncementPlaying)
            throw new InvalidOperationException("Gameplay or announcement completion state is incorrect.");
        if (GameObject.Find("Battle Announcement Text") != null || GameObject.Find("Battle Announcement Dim") != null)
            throw new InvalidOperationException("BATTLE presentation remained active after its exit animation.");
    }

    private static void Finish()
    {
        SessionState.SetInt(PhaseKey, 99);
        EditorApplication.isPlaying = false;
    }
}
