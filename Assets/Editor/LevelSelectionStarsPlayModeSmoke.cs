using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class LevelSelectionStarsPlayModeSmoke
{
    private const string ScenePath = "Assets/Scenes/LevelSelection.unity";
    private const string RunningKey = "AIForGame.LevelSelectionStarsSmoke.Running";
    private const string PhaseKey = "AIForGame.LevelSelectionStarsSmoke.Phase";
    private const string BackupPrefix = "AIForGame.LevelSelectionStarsSmoke.Backup.";
    private static readonly string[] ProgressKeys =
    {
        "AIFG.UnlockedLevel",
        "AIFG.LevelStars.1", "AIFG.LevelStars.2", "AIFG.LevelStars.3",
        "AIFG.LevelStarFlags.1", "AIFG.LevelStarFlags.2", "AIFG.LevelStarFlags.3"
    };

    static LevelSelectionStarsPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Level Selection Stars Play Mode Smoke")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before running the level star smoke.");
        BackupProgress();
        GameProgress.ResetAllStars();
        GameProgress.CompleteLevel(1, 2, 180f);
        GameProgress.CompleteLevel(2, 0, 180f);
        GameProgress.CompleteLevel(3, 1, 90f);
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
            RestoreProgress();
            SessionState.EraseBool(RunningKey);
            SessionState.EraseInt(PhaseKey);
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }

        try
        {
            LevelSelectionController controller = Object.FindFirstObjectByType<LevelSelectionController>();
            if (controller == null || controller.levelCards == null || controller.levelCards.Length != 3) return;
            ValidateCard(controller.levelCards[0], true, false, false);
            ValidateCard(controller.levelCards[1], true, true, false);
            ValidateCard(controller.levelCards[2], true, false, true);

            Canvas sceneCanvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.scene == controller.gameObject.scene &&
                    candidate.renderMode != RenderMode.WorldSpace);
            Transform root = sceneCanvas?.transform.Find("Runtime Star Reset Controls");
            Button reset = root?.Find("Reset Stars")?.GetComponent<Button>();
            Transform confirmation = root?.Find("Reset Confirmation");
            if (reset == null || confirmation == null)
                throw new InvalidOperationException("Reset Stars button or confirmation panel was not created.");
            if (root.gameObject.scene != controller.gameObject.scene)
                throw new InvalidOperationException("Reset Stars was parented to a persistent transition canvas.");

            reset.onClick.Invoke();
            if (!confirmation.gameObject.activeSelf)
                throw new InvalidOperationException("Reset Stars did not open the confirmation panel.");
            confirmation.Find("Dialog/No").GetComponent<Button>().onClick.Invoke();
            if (confirmation.gameObject.activeSelf || GameProgress.GetStars(1) == 0)
                throw new InvalidOperationException("No did not cancel the star reset.");

            reset.onClick.Invoke();
            confirmation.Find("Dialog/Yes").GetComponent<Button>().onClick.Invoke();
            if (GameProgress.GetStars(1) != 0 || GameProgress.GetStars(2) != 0 || GameProgress.GetStars(3) != 0)
                throw new InvalidOperationException("Yes did not clear every earned star.");
            foreach (LevelCardUI card in controller.levelCards) ValidateCard(card, false, false, false);

            Debug.Log("LEVEL_SELECTION_STARS_PLAYMODE_OK independent completion/no-defeat/under-two-minute sprites and scene-local bottom-right Reset Stars UI passed.");
            RestoreProgress();
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            RestoreProgress();
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void ValidateCard(LevelCardUI card, bool first, bool second, bool third)
    {
        Image[] stars = card.GetComponentsInChildren<Image>(true)
            .Where(image => image.gameObject.name.StartsWith("StarUi", StringComparison.Ordinal))
            .OrderBy(image => image.rectTransform.anchoredPosition.x).ToArray();
        if (stars.Length != 3) throw new InvalidOperationException(card.name + " does not contain exactly three star Images.");
        bool[] expected = { first, second, third };
        for (int i = 0; i < stars.Length; i++)
        {
            string expectedName = expected[i] ? "Star" : "StarHidden";
            if (stars[i].sprite == null || stars[i].sprite.name != expectedName)
                throw new InvalidOperationException(card.name + " star " + (i + 1) + " expected " + expectedName +
                    " but displayed " + (stars[i].sprite == null ? "null" : stars[i].sprite.name) + ".");
        }
    }

    private static void BackupProgress()
    {
        for (int i = 0; i < ProgressKeys.Length; i++)
        {
            bool exists = PlayerPrefs.HasKey(ProgressKeys[i]);
            SessionState.SetBool(BackupPrefix + i + ".Exists", exists);
            if (exists) SessionState.SetInt(BackupPrefix + i + ".Value", PlayerPrefs.GetInt(ProgressKeys[i]));
        }
    }

    private static void RestoreProgress()
    {
        for (int i = 0; i < ProgressKeys.Length; i++)
        {
            if (SessionState.GetBool(BackupPrefix + i + ".Exists", false))
                PlayerPrefs.SetInt(ProgressKeys[i], SessionState.GetInt(BackupPrefix + i + ".Value", 0));
            else
                PlayerPrefs.DeleteKey(ProgressKeys[i]);
            SessionState.EraseBool(BackupPrefix + i + ".Exists");
            SessionState.EraseInt(BackupPrefix + i + ".Value");
        }
        PlayerPrefs.Save();
    }
}
