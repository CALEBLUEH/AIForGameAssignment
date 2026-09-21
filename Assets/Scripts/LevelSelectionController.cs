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
        for (int i = 0; i < levelCards.Length; i++)
        {
            int level = i + 1;
            levelCards[i].Refresh(level, GameProgress.IsLevelUnlocked(level), GameProgress.GetStars(level));
            levelCards[i].selectButton.onClick.AddListener(() => SelectLevel(level));
        }
    }

    private void SelectLevel(int level)
    {
        if (!GameProgress.IsLevelUnlocked(level)) return;
        GameProgress.SelectedLevel = level;
        SceneManager.LoadScene(preparationSceneName);
    }
}
