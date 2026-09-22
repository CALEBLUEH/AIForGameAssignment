using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CharacterStatePreviewController : MonoBehaviour
{
    public enum PreviewState
    {
        Idle,
        Run,
        Attack,
        Cover,
        Retreat
    }

    [Serializable]
    private class CharacterOption
    {
        public string displayName;
        public GameObject root;
        public Animator animator;
    }

    [Header("Character Models")]
    [SerializeField] private CharacterOption[] characters = Array.Empty<CharacterOption>();

    [Header("Authored UI References")]
    [SerializeField] private Button[] characterButtons = Array.Empty<Button>();
    [SerializeField] private Button[] stateButtons = Array.Empty<Button>();
    [SerializeField] private Text selectionText;
    [SerializeField] private GameObject coverPreview;
    [SerializeField] private Color selectedColor = new Color(0.18f, 0.76f, 0.96f, 1f);
    [SerializeField] private Color normalColor = new Color(0.10f, 0.18f, 0.30f, 0.96f);

    private UnityAction[] characterActions = Array.Empty<UnityAction>();
    private UnityAction[] stateActions = Array.Empty<UnityAction>();
    private int selectedCharacter;
    private PreviewState selectedState = PreviewState.Idle;

    private void Awake()
    {
        BindButtons();
        SelectCharacter(0);
        SelectState((int)PreviewState.Idle);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectCharacter(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectCharacter(1);
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) CycleState(-1);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) CycleState(1);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < characterActions.Length && i < characterButtons.Length; i++)
        {
            if (characterButtons[i] != null) characterButtons[i].onClick.RemoveListener(characterActions[i]);
        }

        for (int i = 0; i < stateActions.Length && i < stateButtons.Length; i++)
        {
            if (stateButtons[i] != null) stateButtons[i].onClick.RemoveListener(stateActions[i]);
        }
    }

    public void SelectCharacter(int index)
    {
        if (characters.Length == 0) return;
        selectedCharacter = Mathf.Clamp(index, 0, characters.Length - 1);

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i].root != null) characters[i].root.SetActive(i == selectedCharacter);
        }

        PlaySelectedState();
        RefreshUI();
    }

    public void SelectState(int stateIndex)
    {
        selectedState = (PreviewState)Mathf.Clamp(stateIndex, 0, Enum.GetValues(typeof(PreviewState)).Length - 1);
        if (coverPreview != null) coverPreview.SetActive(selectedState == PreviewState.Cover);
        PlaySelectedState();
        RefreshUI();
    }

    private void BindButtons()
    {
        characterActions = new UnityAction[characterButtons.Length];
        for (int i = 0; i < characterButtons.Length; i++)
        {
            int index = i;
            characterActions[i] = () => SelectCharacter(index);
            if (characterButtons[i] != null) characterButtons[i].onClick.AddListener(characterActions[i]);
        }

        stateActions = new UnityAction[stateButtons.Length];
        for (int i = 0; i < stateButtons.Length; i++)
        {
            int index = i;
            stateActions[i] = () => SelectState(index);
            if (stateButtons[i] != null) stateButtons[i].onClick.AddListener(stateActions[i]);
        }
    }

    private void CycleState(int direction)
    {
        int count = Enum.GetValues(typeof(PreviewState)).Length;
        SelectState(((int)selectedState + direction + count) % count);
    }

    private void PlaySelectedState()
    {
        if (selectedCharacter < 0 || selectedCharacter >= characters.Length) return;
        Animator animator = characters[selectedCharacter].animator;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.Play(selectedState.ToString(), 0, 0f);
    }

    private void RefreshUI()
    {
        for (int i = 0; i < characterButtons.Length; i++) SetButtonColor(characterButtons[i], i == selectedCharacter);
        for (int i = 0; i < stateButtons.Length; i++) SetButtonColor(stateButtons[i], i == (int)selectedState);

        if (selectionText != null && characters.Length > 0)
        {
            selectionText.text = characters[selectedCharacter].displayName + "  •  " + selectedState +
                "\n1 / 2 selects a model     W / S or ↑ / ↓ selects a state";
        }
    }

    private void SetButtonColor(Button button, bool selected)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = selected ? selectedColor : normalColor;
    }
}
