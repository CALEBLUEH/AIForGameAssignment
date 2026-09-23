using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FinalGameplayIntegrationPlayModeSmoke
{
    private const string Level2Path = "Assets/Scenes/Gameplay_Level2.unity";
    private const string RunningKey = "AIForGame.FinalGameplayIntegration.Running";
    private const string PhaseKey = "AIForGame.FinalGameplayIntegration.Phase";
    private const string OriginalLevelKey = "AIForGame.FinalGameplayIntegration.OriginalLevel";
    private static double deadline;
    private static double earliestCheck;
    private static GameObject testBoss;

    static FinalGameplayIntegrationPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Final Gameplay Integration Smoke")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before running the final gameplay integration smoke.");
        EditorSceneManager.OpenScene(Level2Path, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.SetInt(OriginalLevelKey, GameProgress.SelectedLevel);
        deadline = earliestCheck = 0d;
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        int phase = SessionState.GetInt(PhaseKey, 0);
        if (!EditorApplication.isPlaying)
        {
            if (phase != 99) return;
            SessionState.EraseBool(RunningKey);
            SessionState.EraseInt(PhaseKey);
            SessionState.EraseInt(OriginalLevelKey);
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }

        try
        {
            if (deadline <= 0d) deadline = EditorApplication.timeSinceStartup + 45d;
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Final gameplay integration smoke timed out in phase " + phase + ".");
            if (EditorApplication.timeSinceStartup < earliestCheck) return;

            BattleDirector director = Object.FindFirstObjectByType<BattleDirector>();
            if (phase == 0)
            {
                if (director == null || !director.IsPlaying) return;
                ValidateLevel(director, 2);
                ValidateProgressRules();
                testBoss = new GameObject("Boss HUD Runtime Smoke");
                CombatUnit boss = testBoss.AddComponent<CombatUnit>();
                boss.team = CombatUnit.CombatTeam.Enemy;
                boss.ConfigureSpawn(321f, 1f, 0f, true);
                Advance(1, 0.25d);
            }
            else if (phase == 1)
            {
                if (director == null || director.bossHealthPanel == null || !director.bossHealthPanel.activeInHierarchy)
                    throw new InvalidOperationException("Wave-spawned boss identity did not activate the boss HUD.");
                if (director.bossHealthSlider == null || Mathf.Abs(director.bossHealthSlider.maxValue - 321f) > 0.1f)
                    throw new InvalidOperationException("Boss HUD did not bind the runtime boss health.");
                Object.Destroy(testBoss);

                director.pauseButton.onClick.Invoke();
                Transform controls = director.pausePanel.transform.Find("Runtime Pause Controls");
                Button videoToggle = controls == null ? null : controls.Find("Toggle Skill Videos")?.GetComponent<Button>();
                Button restart = controls == null ? null : controls.Find("Restart Current Level")?.GetComponent<Button>();
                if (videoToggle == null || restart == null)
                    throw new InvalidOperationException("Pause Restart or Skill Video toggle was not created.");
                bool originalVideoState = SkillCinematicPlayer.SkillVideosEnabled;
                videoToggle.onClick.Invoke();
                if (SkillCinematicPlayer.SkillVideosEnabled == originalVideoState)
                    throw new InvalidOperationException("Skill Video toggle did not change the setting.");
                videoToggle.onClick.Invoke();
                if (SkillCinematicPlayer.SkillVideosEnabled != originalVideoState)
                    throw new InvalidOperationException("Skill Video setting did not restore after a second toggle.");

                restart.onClick.Invoke();
                Advance(2, 0.2d);
            }
            else if (phase == 2)
            {
                if (SceneManager.GetActiveScene().name != "Gameplay_Level2") return;
                LevelOpeningSequence opening = Object.FindFirstObjectByType<LevelOpeningSequence>();
                if (opening == null || !opening.IsPlaying) return;
                SceneManager.LoadScene("Gameplay_Level3");
                Advance(3, 0.2d);
            }
            else if (phase == 3)
            {
                director = Object.FindFirstObjectByType<BattleDirector>();
                if (director == null || !director.IsPlaying) return;
                ValidateLevel(director, 3);
                SceneManager.LoadScene("LevelSelection");
                Advance(4, 0.2d);
            }
            else if (phase == 4)
            {
                if (SceneManager.GetActiveScene().name != "LevelSelection") return;
                LevelSelectionController selection = Object.FindFirstObjectByType<LevelSelectionController>();
                if (selection == null || selection.levelCards == null || selection.levelCards.Length < 1)
                    throw new InvalidOperationException("Level Selection controller or Level 1 card is missing.");
                selection.levelCards[0].selectButton.onClick.Invoke();
                Advance(5, 0.2d);
            }
            else if (phase == 5)
            {
                if (SceneManager.GetActiveScene().name != GameProgress.PreparationSceneName) return;
                PreparationMenuController preparation = Object.FindFirstObjectByType<PreparationMenuController>();
                if (preparation == null || preparation.battleButton == null || !preparation.battleButton.interactable)
                    throw new InvalidOperationException("Sandbox Preparation did not load with four deployable characters.");
                preparation.battleButton.onClick.Invoke();
                Advance(6, 0.2d);
            }
            else if (phase == 6)
            {
                if (SceneManager.GetActiveScene().name != "Gameplay_Level1") return;
                director = Object.FindFirstObjectByType<BattleDirector>();
                if (director == null || director.levelNumber != 1) return;
                director.backButton.onClick.Invoke();
                Advance(7, 0.2d);
            }
            else if (phase == 7)
            {
                if (SceneManager.GetActiveScene().name != GameProgress.PreparationSceneName) return;
                PreparationMenuController preparation = Object.FindFirstObjectByType<PreparationMenuController>();
                if (preparation == null || !preparation.battleButton.interactable)
                    throw new InvalidOperationException("Returning to preparation did not preserve a valid squad.");
                preparation.battleButton.onClick.Invoke();
                Advance(8, 0.2d);
            }
            else if (phase == 8)
            {
                if (SceneManager.GetActiveScene().name != "Gameplay_Level1") return;
                RestoreOriginalLevel();
                Debug.Log("FINAL_GAMEPLAY_INTEGRATION_OK runtime cover points in Levels 2/3, boss HUD, actual Level Selection/Preparation/Gameplay and return-to-preparation routing, star rules, pause Restart, Skill Video toggle, and 20-40 camera-height slider passed.");
                SessionState.SetInt(PhaseKey, 99);
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (testBoss != null) Object.DestroyImmediate(testBoss);
            RestoreOriginalLevel();
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void ValidateLevel(BattleDirector director, int expectedLevel)
    {
        if (director.levelNumber != expectedLevel)
            throw new InvalidOperationException("Runtime level number did not match Gameplay_Level" + expectedLevel + ".");

        TacticalCoverObstacle[] obstacles = Object.FindObjectsByType<TacticalCoverObstacle>(FindObjectsSortMode.None);
        int usable = obstacles.Count(obstacle => obstacle.GetComponent<DestructibleCover>() != null &&
            obstacle.GetComponentsInChildren<CoverPoint>(true).Any(point => point.coverCollider != null));
        if (obstacles.Length == 0 || usable == 0)
            throw new InvalidOperationException("Level " + expectedLevel + " did not create any usable destructible cover points.");

        Transform cameraControl = director.battlePanel.transform.Find("Runtime Camera Height Control");
        Slider slider = cameraControl == null ? null : cameraControl.GetComponentInChildren<Slider>(true);
        TacticalCameraFollow camera = Object.FindFirstObjectByType<TacticalCameraFollow>();
        if (slider == null || camera == null || slider.minValue != 20f || slider.maxValue != 40f)
            throw new InvalidOperationException("The tactical camera height slider is missing or has the wrong range.");
        slider.value = 20f;
        if (Mathf.Abs(camera.VerticalOffset - 20f) > 0.01f)
            throw new InvalidOperationException("Camera slider minimum did not update the Y offset.");
        slider.value = 40f;
        if (Mathf.Abs(camera.VerticalOffset - 40f) > 0.01f)
            throw new InvalidOperationException("Camera slider maximum did not update the Y offset.");
    }

    private static void ValidateProgressRules()
    {
        if (GameProgress.BattleSceneForLevel(1) != "Gameplay_Level1" ||
            GameProgress.BattleSceneForLevel(2) != "Gameplay_Level2" ||
            GameProgress.BattleSceneForLevel(3) != "Gameplay_Level3")
            throw new InvalidOperationException("Level routing still points at an old non-underscored gameplay scene.");
        if (GameProgress.StarsForVictory(0, 100f) !=
                (GameProgress.LevelStar.Completed | GameProgress.LevelStar.NoCharacterDefeated | GameProgress.LevelStar.UnderTwoMinutes) ||
            GameProgress.StarsForVictory(1, 100f) !=
                (GameProgress.LevelStar.Completed | GameProgress.LevelStar.UnderTwoMinutes) ||
            GameProgress.StarsForVictory(0, 121f) !=
                (GameProgress.LevelStar.Completed | GameProgress.LevelStar.NoCharacterDefeated) ||
            GameProgress.StarsForVictory(1, 121f) != GameProgress.LevelStar.Completed)
            throw new InvalidOperationException("Victory star rules are incorrect.");

        int originalLevel = GameProgress.SelectedLevel;
        try
        {
            GameProgress.PrepareLevelSelection(3);
            if (GameProgress.ConsumeBattleScene("Sandbox_Gameplay") != "Gameplay_Level3")
                throw new InvalidOperationException("Preparation did not consume the selected level route.");
        }
        finally
        {
            GameProgress.SelectedLevel = originalLevel;
            GameProgress.ClearPendingBattleSelection();
        }
    }

    private static void Advance(int phase, double delay)
    {
        SessionState.SetInt(PhaseKey, phase);
        earliestCheck = EditorApplication.timeSinceStartup + delay;
    }

    private static void RestoreOriginalLevel()
    {
        GameProgress.SelectedLevel = SessionState.GetInt(OriginalLevelKey, 1);
        GameProgress.ClearPendingBattleSelection();
    }
}
