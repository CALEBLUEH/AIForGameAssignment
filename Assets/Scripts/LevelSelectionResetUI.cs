using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LevelSelectionResetUI : MonoBehaviour
{
    private const string RootName = "Runtime Star Reset Controls";
    private GameObject confirmationPanel;

    public void Initialize(Transform canvas, Action resetStars)
    {
        if (canvas == null || canvas.Find(RootName) != null) return;

        RectTransform root = CreateRect(RootName, canvas);
        Stretch(root, Vector2.zero);
        root.SetAsLastSibling();

        Button reset = CreateButton(root, "Reset Stars", "RESET STARS", new Color(0.72f, 0.18f, 0.14f, 1f));
        RectTransform resetRect = reset.GetComponent<RectTransform>();
        resetRect.anchorMin = resetRect.anchorMax = resetRect.pivot = new Vector2(1f, 0f);
        resetRect.sizeDelta = new Vector2(220f, 58f);
        resetRect.anchoredPosition = new Vector2(-28f, 28f);

        confirmationPanel = CreateConfirmation(root, resetStars);
        confirmationPanel.SetActive(false);
        reset.onClick.AddListener(() => confirmationPanel.SetActive(true));
    }

    private GameObject CreateConfirmation(Transform parent, Action resetStars)
    {
        RectTransform panel = CreateRect("Reset Confirmation", parent);
        Stretch(panel, Vector2.zero);
        Image dim = panel.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);

        RectTransform dialog = CreateRect("Dialog", panel);
        dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(0.5f, 0.5f);
        dialog.sizeDelta = new Vector2(620f, 260f);
        dialog.anchoredPosition = Vector2.zero;
        Image dialogImage = dialog.gameObject.AddComponent<Image>();
        dialogImage.color = new Color(0.055f, 0.10f, 0.17f, 0.98f);

        Text prompt = CreateText(dialog, "Prompt", "Reset all earned level stars?\nThis cannot be undone.", 28, TextAnchor.MiddleCenter);
        prompt.rectTransform.anchorMin = new Vector2(0.08f, 0.42f);
        prompt.rectTransform.anchorMax = new Vector2(0.92f, 0.92f);
        prompt.rectTransform.offsetMin = prompt.rectTransform.offsetMax = Vector2.zero;

        Button yes = CreateButton(dialog, "Yes", "YES", new Color(0.76f, 0.22f, 0.16f, 1f));
        SetRect(yes.GetComponent<RectTransform>(), new Vector2(220f, 62f), new Vector2(-125f, -78f));
        Button no = CreateButton(dialog, "No", "NO", new Color(0.12f, 0.48f, 0.72f, 1f));
        SetRect(no.GetComponent<RectTransform>(), new Vector2(220f, 62f), new Vector2(125f, -78f));

        yes.onClick.AddListener(() =>
        {
            resetStars?.Invoke();
            panel.gameObject.SetActive(false);
        });
        no.onClick.AddListener(() => panel.gameObject.SetActive(false));
        return panel.gameObject;
    }

    private static Button CreateButton(Transform parent, string name, string caption, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.22f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.22f);
        button.colors = colors;
        Text label = CreateText(rect, "Label", caption, 23, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform, new Vector2(8f, 6f));
        return button;
    }

    private static Text CreateText(Transform parent, string name, string value, int size, TextAnchor alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = size;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect, Vector2 inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = inset;
        rect.offsetMax = -inset;
    }
}
