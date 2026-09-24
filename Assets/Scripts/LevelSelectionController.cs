using UnityEngine;
using UnityEngine.UI;

public class LevelSelectionController : MonoBehaviour
{
    public string preparationSceneName = "Preparation";
    public LevelCardUI[] levelCards;

    private void Awake()
    {
        Time.timeScale = 1f;
        GameProgress.ClearPendingBattleSelection();
        RefreshCards();
        for (int i = 0; i < levelCards.Length; i++)
        {
            int level = i + 1;
            levelCards[i].selectButton.onClick.AddListener(() => SelectLevel(level));
        }

        Canvas canvas = FindSceneCanvas();
        if (canvas != null)
        {
            LevelSelectionResetUI resetUI = GetComponent<LevelSelectionResetUI>();
            if (resetUI == null) resetUI = gameObject.AddComponent<LevelSelectionResetUI>();
            resetUI.Initialize(canvas.transform, ResetStars);
        }
    }

    private Canvas FindSceneCanvas()
    {
        foreach (Canvas candidate in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene != gameObject.scene) continue;
            if (candidate.renderMode == RenderMode.WorldSpace) continue;
            return candidate;
        }
        return null;
    }

    public void RefreshCards()
    {
        for (int i = 0; i < levelCards.Length; i++)
        {
            int level = i + 1;
            levelCards[i].RefreshAchievements(level, GameProgress.IsLevelUnlocked(level), GameProgress.GetStarFlags(level));
        }
    }

    private void ResetStars()
    {
        GameProgress.ResetAllStars();
        RefreshCards();
    }

    private void SelectLevel(int level)
    {
        if (!GameProgress.IsLevelUnlocked(level)) return;
        GameProgress.PrepareLevelSelection(level);
        SceneTransitionService.LoadScene(GameProgress.PreparationSceneName);
    }
}
