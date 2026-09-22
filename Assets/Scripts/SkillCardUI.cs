using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public sealed class SkillCardUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Text costText;
    [SerializeField] private Text nameText;
    [SerializeField] private float usedAnimationDuration = 0.2f;
    [SerializeField] private float usedAnimationRise = 75f;

    private Button button;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 restingPosition;
    private Coroutine animationRoutine;

    public Button Button => button != null ? button : button = GetComponent<Button>();
    public Sprite DisplayedIcon => icon == null ? null : icon.sprite;
    public string DisplayName => nameText == null ? string.Empty : nameText.text;
    public string DisplayedCost => costText == null ? string.Empty : costText.text;

    private void Awake()
    {
        button = GetComponent<Button>();
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = (RectTransform)transform;
        restingPosition = rectTransform.anchoredPosition;
    }

    public void Configure(Image configuredIcon, Text configuredCost, Text configuredName)
    {
        icon = configuredIcon;
        costText = configuredCost;
        nameText = configuredName;
    }

    public void Bind(Sprite sprite, string displayName, float cost, bool interactable)
    {
        if (button == null) button = GetComponent<Button>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (rectTransform == null) rectTransform = (RectTransform)transform;
        if (animationRoutine == null)
        {
            restingPosition = rectTransform.anchoredPosition;
            canvasGroup.alpha = 1f;
        }

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.color = sprite == null ? new Color(0.15f, 0.25f, 0.38f, 1f) : Color.white;
        }
        if (costText != null) costText.text = Mathf.CeilToInt(cost).ToString();
        if (nameText != null) nameText.text = displayName;
        button.interactable = interactable && animationRoutine == null;
    }

    public void PlayUsed(Action completed)
    {
        if (!isActiveAndEnabled)
        {
            completed?.Invoke();
            return;
        }
        if (animationRoutine != null) StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(AnimateUsed(completed));
    }

    private IEnumerator AnimateUsed(Action completed)
    {
        if (button == null) button = GetComponent<Button>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (rectTransform == null) rectTransform = (RectTransform)transform;
        restingPosition = rectTransform.anchoredPosition;
        button.interactable = false;

        float duration = Mathf.Max(0.05f, usedAnimationDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);
            rectTransform.anchoredPosition = restingPosition + Vector2.up * (usedAnimationRise * eased);
            canvasGroup.alpha = 1f - eased;
            yield return null;
        }

        rectTransform.anchoredPosition = restingPosition;
        canvasGroup.alpha = 1f;
        animationRoutine = null;
        completed?.Invoke();
    }

    private void OnDisable()
    {
        if (animationRoutine != null) StopCoroutine(animationRoutine);
        animationRoutine = null;
        if (rectTransform != null) rectTransform.anchoredPosition = restingPosition;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }
}
