using UnityEngine;
using UnityEngine.SceneManagement;
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

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            LevelSelectionResetUI resetUI = GetComponent<LevelSelectionResetUI>();
            if (resetUI == null) resetUI = gameObject.AddComponent<LevelSelectionResetUI>();
            resetUI.Initialize(canvas.transform, ResetStars);
        }
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
        SceneManager.LoadScene(GameProgress.PreparationSceneName);
    }
}
