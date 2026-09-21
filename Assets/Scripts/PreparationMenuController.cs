using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PreparationMenuController : MonoBehaviour
{
    public string gameplaySceneName = "GameplayAI";
    public Button battleButton;
    public Button settingsButton;
    public Button closeSettingsButton;
    public GameObject settingsPanel;

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioSettingsUI.ApplySavedVolume();
        if (battleButton != null) battleButton.onClick.AddListener(StartBattle);
        if (settingsButton != null) settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(() => settingsPanel.SetActive(false));
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void StartBattle()
    {
        SceneManager.LoadScene(gameplaySceneName);
    }
}
