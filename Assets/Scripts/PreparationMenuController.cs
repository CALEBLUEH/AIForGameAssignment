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
    [Header("Sandbox character displays")]
    [SerializeField] private PreparationCharacterDisplay[] characterDisplays;
    [SerializeField] private Camera displayCamera;
    [SerializeField] private string battleSceneOverride;
    [SerializeField, Min(0.01f)] private float dragDegreesPerPixel = 0.35f;
    [SerializeField, Min(1f)] private float clickDragThreshold = 6f;
    private bool[] selected;
    private PreparationCharacterDisplay pointerDisplay;
    private Vector2 pointerDownPosition;
    private Vector2 previousPointerPosition;
    private bool pointerStartedOnPodium;
    private bool pointerDragged;

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioSettingsUI.ApplySavedVolume();
        selected = new bool[characterNames.Length];
        foreach (string saved in GameProgress.GetSelectedCharacters())
            for (int i = 0; i < characterNames.Length; i++) if (characterNames[i] == saved) selected[i] = true;
        bool usePodiumSelection = characterDisplays != null && characterDisplays.Length > 0;
        for (int i = 0; characterButtons != null && i < characterButtons.Length && i < selected.Length; i++)
        {
            if (characterButtons[i] == null) continue;
            characterButtons[i].interactable = !usePodiumSelection;
            if (!usePodiumSelection)
            {
                int index = i;
                characterButtons[i].onClick.AddListener(() => ToggleCharacter(index));
            }
        }
        if (battleButton != null) battleButton.onClick.AddListener(StartBattle);
        if (backButton != null) backButton.onClick.AddListener(() => SceneManager.LoadScene("LevelSelection"));
        if (settingsButton != null) settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(() => settingsPanel.SetActive(false));
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (titleText != null) titleText.text = "LEVEL " + GameProgress.SelectedLevel + "  •  SQUAD READY";
        RefreshSelection();
    }

    private void Update()
    {
        if (characterDisplays == null || characterDisplays.Length == 0) return;

        if (Input.GetMouseButtonDown(0)) BeginPointer(Input.mousePosition);
        if (Input.GetMouseButton(0)) MovePointer(Input.mousePosition);
        if (Input.GetMouseButtonUp(0)) EndPointer(Input.mousePosition);
    }

    private void BeginPointer(Vector2 screenPosition)
    {
        pointerDisplay = null;
        pointerDragged = false;
        pointerStartedOnPodium = false;
        Camera rayCamera = displayCamera != null ? displayCamera : Camera.main;
        if (rayCamera == null || !Physics.Raycast(rayCamera.ScreenPointToRay(screenPosition), out RaycastHit hit)) return;
        pointerDisplay = hit.collider.GetComponentInParent<PreparationCharacterDisplay>();
        if (pointerDisplay == null) return;

        pointerStartedOnPodium = pointerDisplay.OwnsPodiumCollider(hit.collider);
        pointerDownPosition = previousPointerPosition = screenPosition;
    }

    private void MovePointer(Vector2 screenPosition)
    {
        if (pointerDisplay == null) return;
        Vector2 delta = screenPosition - previousPointerPosition;
        if (!pointerDragged && (screenPosition - pointerDownPosition).sqrMagnitude >= clickDragThreshold * clickDragThreshold)
            pointerDragged = true;
        if (pointerDragged && Mathf.Abs(delta.x) > 0.01f)
            pointerDisplay.RotateModel(-delta.x * dragDegreesPerPixel);
        previousPointerPosition = screenPosition;
    }

    private void EndPointer(Vector2 screenPosition)
    {
        if (pointerDisplay != null && pointerStartedOnPodium && !pointerDragged)
            ToggleCharacterFromPodium(pointerDisplay.RosterIndex);
        pointerDisplay = null;
    }

    private void ToggleCharacter(int index)
    {
        if (selected == null || index < 0 || index >= selected.Length) return;
        int count = SelectedCount();
        if (selected[index] && count <= 1) return;
        if (!selected[index] && count >= 4) return;
        selected[index] = !selected[index];
        RefreshSelection();
    }

    public void ToggleCharacterFromPodium(int index) => ToggleCharacter(index);

    public bool IsCharacterSelected(int index) =>
        selected != null && index >= 0 && index < selected.Length && selected[index];

    public void ConfigureSandboxDisplays(PreparationCharacterDisplay[] displays, Camera camera, string battleScene)
    {
        characterDisplays = displays;
        displayCamera = camera;
        battleSceneOverride = battleScene;
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
        for (int i = 0; characterButtons != null && i < characterButtons.Length && i < characterNames.Length; i++)
        {
            if (characterButtons[i] == null) continue;
            Text label = characterButtons[i].GetComponentInChildren<Text>();
            if (label != null) label.text = (selected[i] ? "✓ " : "○ ") + characterNames[i].ToUpperInvariant();
            ColorBlock colors = characterButtons[i].colors;
            colors.normalColor = selected[i] ? new Color(0.12f, 0.72f, 0.92f, 1f) : new Color(0.35f, 0.42f, 0.50f, 1f);
            characterButtons[i].colors = colors;
        }
        for (int i = 0; characterDisplays != null && i < characterDisplays.Length; i++)
            if (characterDisplays[i] != null)
                characterDisplays[i].SetSelected(i < selected.Length && selected[i]);
    }

    private void StartBattle()
    {
        if (SelectedCount() != 4) return;
        var names = new List<string>();
        for (int i = 0; i < selected.Length; i++) if (selected[i]) names.Add(characterNames[i]);
        GameProgress.SetSelectedCharacters(names);
        SceneManager.LoadScene(string.IsNullOrWhiteSpace(battleSceneOverride)
            ? GameProgress.SelectedBattleScene
            : battleSceneOverride);
    }
}
