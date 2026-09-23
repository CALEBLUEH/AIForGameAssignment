using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleRuntimeHudControls : MonoBehaviour
{
    private const string PauseControlsName = "Runtime Pause Controls";
    private const string CameraControlName = "Runtime Camera Height Control";
    private Text skillVideoLabel;
    private Text cameraValueLabel;
    private TacticalCameraFollow tacticalCamera;

    public void Initialize(GameObject battlePanel, GameObject pausePanel, Action restartBattle)
    {
        tacticalCamera = FindFirstObjectByType<TacticalCameraFollow>(FindObjectsInactive.Include);
        if (pausePanel != null && pausePanel.transform.Find(PauseControlsName) == null)
            CreatePauseControls(pausePanel.transform, restartBattle);
        if (battlePanel != null && battlePanel.transform.Find(CameraControlName) == null)
            CreateCameraHeightControl(battlePanel.transform);
    }

    private void CreatePauseControls(Transform parent, Action restartBattle)
    {
        RectTransform root = CreateRect(PauseControlsName, parent);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(560f, 70f);
        root.anchoredPosition = new Vector2(0f, -125f);

        Button restart = CreateButton(root, "Restart Current Level", "RESTART", new Vector2(-145f, 0f));
        restart.onClick.AddListener(() => restartBattle?.Invoke());

        Button skillVideo = CreateButton(root, "Toggle Skill Videos", string.Empty, new Vector2(145f, 0f));
        skillVideoLabel = skillVideo.GetComponentInChildren<Text>();
        RefreshSkillVideoLabel();
        skillVideo.onClick.AddListener(() =>
        {
            SkillCinematicPlayer.SkillVideosEnabled = !SkillCinematicPlayer.SkillVideosEnabled;
            RefreshSkillVideoLabel();
        });
    }

    private void CreateCameraHeightControl(Transform parent)
    {
        RectTransform root = CreateRect(CameraControlName, parent);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 0.5f);
        root.sizeDelta = new Vector2(96f, 330f);
        root.anchoredPosition = new Vector2(-18f, 0f);

        Image panel = root.gameObject.AddComponent<Image>();
        panel.color = new Color(0.04f, 0.08f, 0.14f, 0.78f);
        panel.raycastTarget = true;

        Text title = CreateText(root, "Title", "CAMERA\nHEIGHT", 16, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(88f, 52f), new Vector2(0f, 132f));

        RectTransform sliderRoot = CreateRect("Height Slider", root);
        SetRect(sliderRoot, new Vector2(34f, 224f), new Vector2(0f, -5f));
        Slider slider = sliderRoot.gameObject.AddComponent<Slider>();
        slider.minValue = 20f;
        slider.maxValue = 40f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.BottomToTop;

        Image background = CreateImage(sliderRoot, "Background", new Color(0.12f, 0.18f, 0.28f, 1f));
        Stretch(background.rectTransform, new Vector2(10f, 0f));

        RectTransform fillArea = CreateRect("Fill Area", sliderRoot);
        Stretch(fillArea, new Vector2(9f, 9f));
        Image fill = CreateImage(fillArea, "Fill", new Color(1f, 0.55f, 0.12f, 1f));
        Stretch(fill.rectTransform, Vector2.zero);
        slider.fillRect = fill.rectTransform;

        RectTransform handleArea = CreateRect("Handle Slide Area", sliderRoot);
        Stretch(handleArea, new Vector2(0f, 10f));
        Image handle = CreateImage(handleArea, "Handle", new Color(1f, 0.78f, 0.2f, 1f));
        handle.rectTransform.sizeDelta = new Vector2(30f, 16f);
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;

        cameraValueLabel = CreateText(root, "Value", string.Empty, 18, TextAnchor.MiddleCenter);
        SetRect(cameraValueLabel.rectTransform, new Vector2(88f, 32f), new Vector2(0f, -143f));
        float startingHeight = tacticalCamera == null ? 40f : Mathf.Clamp(tacticalCamera.VerticalOffset, 20f, 40f);
        slider.SetValueWithoutNotify(startingHeight);
        ApplyCameraHeight(startingHeight);
        slider.onValueChanged.AddListener(ApplyCameraHeight);
    }

    private void ApplyCameraHeight(float value)
    {
        if (tacticalCamera != null) tacticalCamera.SetVerticalOffset(value);
        if (cameraValueLabel != null) cameraValueLabel.text = Mathf.RoundToInt(value).ToString();
    }

    private void RefreshSkillVideoLabel()
    {
        if (skillVideoLabel != null)
            skillVideoLabel.text = "SKILL VIDEO: " + (SkillCinematicPlayer.SkillVideosEnabled ? "ON" : "OFF");
    }

    private static Button CreateButton(Transform parent, string name, string caption, Vector2 position)
    {
        RectTransform rect = CreateRect(name, parent);
        SetRect(rect, new Vector2(270f, 58f), position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.1f, 0.48f, 0.72f, 1f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.18f, 0.66f, 0.92f, 1f);
        colors.pressedColor = new Color(0.07f, 0.32f, 0.52f, 1f);
        button.colors = colors;
        Text label = CreateText(rect, "Label", caption, 23, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform, new Vector2(8f, 6f));
        return button;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
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
        text.resizeTextMinSize = 10;
        text.resizeTextMaxSize = size;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect, Vector2 inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = inset;
        rect.offsetMax = -inset;
        rect.localScale = Vector3.one;
    }
}
