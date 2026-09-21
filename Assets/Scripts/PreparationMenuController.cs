using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PreparationMenuController : MonoBehaviour
{
    public Button battleButton, backButton, settingsButton, closeSettingsButton;
    public GameObject settingsPanel;
    public Text titleText;
    [Header("Five-character roster; exactly four deploy")]
    public string[] characterNames = { "Yuuka", "Ayane", "Mika", "Momoi", "Hina" };
    public Button[] characterButtons;
    public Text selectionText;
    private bool[] selected;

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioSettingsUI.ApplySavedVolume();
        selected = new bool[characterNames.Length];
        foreach (string saved in GameProgress.GetSelectedCharacters())
            for (int i = 0; i < characterNames.Length; i++) if (characterNames[i] == saved) selected[i] = true;
        for (int i = 0; i < characterButtons.Length && i < selected.Length; i++)
        {
            int index = i;
            characterButtons[i].onClick.AddListener(() => ToggleCharacter(index));
        }
        if (battleButton != null) battleButton.onClick.AddListener(StartBattle);
        if (backButton != null) backButton.onClick.AddListener(() => SceneManager.LoadScene("LevelSelection"));
        if (settingsButton != null) settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(() => settingsPanel.SetActive(false));
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (titleText != null) titleText.text = "LEVEL " + GameProgress.SelectedLevel + "  •  SQUAD READY";
        RefreshSelection();
    }

    private void ToggleCharacter(int index)
    {
        int count = SelectedCount();
        if (selected[index] && count <= 1) return;
        if (!selected[index] && count >= 4) return;
        selected[index] = !selected[index];
        RefreshSelection();
    }

    private int SelectedCount()
    {
        int count = 0;
        foreach (bool value in selected) if (value) count++;
        return count;
    }

    private void RefreshSelection()
    {
        int count = SelectedCount();
        if (selectionText != null) selectionText.text = "DEPLOY " + count + "/4  •  SELECT EXACTLY FOUR";
        if (battleButton != null) battleButton.interactable = count == 4;
        for (int i = 0; i < characterButtons.Length && i < characterNames.Length; i++)
        {
            Text label = characterButtons[i].GetComponentInChildren<Text>();
            if (label != null) label.text = (selected[i] ? "✓ " : "○ ") + characterNames[i].ToUpperInvariant();
            ColorBlock colors = characterButtons[i].colors;
            colors.normalColor = selected[i] ? new Color(0.12f, 0.72f, 0.92f, 1f) : new Color(0.35f, 0.42f, 0.50f, 1f);
            characterButtons[i].colors = colors;
        }
    }

    private void StartBattle()
    {
        if (SelectedCount() != 4) return;
        var names = new List<string>();
        for (int i = 0; i < selected.Length; i++) if (selected[i]) names.Add(characterNames[i]);
        GameProgress.SetSelectedCharacters(names);
        SceneManager.LoadScene(GameProgress.SelectedBattleScene);
    }
}
