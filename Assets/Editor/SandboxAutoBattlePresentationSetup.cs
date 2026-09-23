using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SandboxAutoBattlePresentationSetup
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string OverlayName = "Battle Flow Presentation";

    [MenuItem("Tools/AI For Game/Configure Auto Battle And Battle Flow UI")]
    public static void Configure()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BattleDirector director = Find<BattleDirector>(scene);
        LevelOpeningSequence opening = Find<LevelOpeningSequence>(scene);
        if (director == null || opening == null || director.resultPanel == null || director.resultText == null ||
            director.restartButton == null)
            throw new InvalidOperationException("Sandbox gameplay is missing BattleDirector, opening sequence, or result UI references.");

        Transform canvas = director.resultPanel.transform.parent;
        if (canvas == null || canvas.GetComponentInParent<Canvas>() == null)
            throw new InvalidOperationException("Result Panel is not under the gameplay Canvas.");

        GameObject overlay = FindOrCreate(canvas, OverlayName, typeof(RectTransform));
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.transform.SetAsLastSibling();

        Image blackout = EnsureImage(overlay.transform, "Opening Blackout", Color.black);
        Stretch(blackout.rectTransform);
        blackout.raycastTarget = false;
        blackout.gameObject.SetActive(false);

        Image battleDim = EnsureImage(overlay.transform, "Battle Announcement Dim", new Color(0f, 0f, 0f, 0f));
        Stretch(battleDim.rectTransform);
        battleDim.raycastTarget = false;
        battleDim.gameObject.SetActive(false);

        Text battleText = EnsureText(overlay.transform, "Battle Announcement Text", "BATTLE", 120,
            new Color(1f, 0.78f, 0.08f, 1f), FontStyle.Bold);
        SetCentered(battleText.rectTransform, new Vector2(1400f, 220f), Vector2.zero);
        battleText.raycastTarget = false;
        battleText.gameObject.SetActive(false);
        opening.ConfigurePresentation(blackout, battleDim, battleText,
            0.45f, 0.55f, 0.45f, 0.55f, 0.3f, 0.2f);

        RectTransform resultRect = director.resultPanel.GetComponent<RectTransform>();
        Stretch(resultRect);
        director.resultPanel.transform.SetAsLastSibling();
        Image resultBackground = director.resultPanel.GetComponent<Image>();
        if (resultBackground == null) resultBackground = director.resultPanel.AddComponent<Image>();
        resultBackground.color = new Color(0f, 0f, 0f, 0.72f);
        resultBackground.raycastTarget = true;
        CanvasGroup resultGroup = director.resultPanel.GetComponent<CanvasGroup>();
        if (resultGroup == null) resultGroup = director.resultPanel.AddComponent<CanvasGroup>();
        resultGroup.alpha = 0f;
        resultGroup.interactable = true;
        resultGroup.blocksRaycasts = true;

        Text title = director.resultText;
        title.fontSize = 100;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.raycastTarget = false;
        SetCentered(title.rectTransform, new Vector2(1400f, 180f), new Vector2(0f, 95f));

        Text details = EnsureText(director.resultPanel.transform, "Result Details", string.Empty, 30,
            Color.white, FontStyle.Normal);
        SetCentered(details.rectTransform, new Vector2(1100f, 120f), new Vector2(0f, -20f));
        details.raycastTarget = false;

        Button restart = director.restartButton;
        ConfigureButton(restart, "RESTART", new Vector2(175f, -155f));
        Transform existingConfirm = director.resultPanel.transform.Find("Confirm");
        GameObject confirmObject = existingConfirm == null
            ? UnityEngine.Object.Instantiate(restart.gameObject, director.resultPanel.transform)
            : existingConfirm.gameObject;
        confirmObject.name = "Confirm";
        Button confirm = confirmObject.GetComponent<Button>();
        confirm.onClick.RemoveAllListeners();
        ConfigureButton(confirm, "CONFIRM", new Vector2(-175f, -155f));

        SerializedObject serialized = new SerializedObject(director);
        Assign(serialized, "confirmButton", confirm);
        Assign(serialized, "resultCanvasGroup", resultGroup);
        Assign(serialized, "resultDetailsText", details);
        serialized.FindProperty("resultDelay").floatValue = 2f;
        serialized.FindProperty("resultFadeDuration").floatValue = 0.35f;
        serialized.FindProperty("levelSelectionSceneName").stringValue = "LevelSelection";
        serialized.FindProperty("autoBasicSkillDecisionInterval").floatValue = 0.35f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        director.resultPanel.SetActive(false);
        EditorUtility.SetDirty(opening);
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(resultGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save " + ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("SANDBOX_AUTO_BATTLE_PRESENTATION_CONFIGURED auto basic-skill timing, black opening reveal, BATTLE fly-through, fullscreen dim result, Confirm and Restart wired.");
    }

    private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();

    private static GameObject FindOrCreate(Transform parent, string name, params Type[] components)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        GameObject created = new GameObject(name, components);
        created.transform.SetParent(parent, false);
        created.layer = parent.gameObject.layer;
        return created;
    }

    private static Image EnsureImage(Transform parent, string name, Color color)
    {
        GameObject gameObject = FindOrCreate(parent, name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text EnsureText(Transform parent, string name, string value, int size, Color color, FontStyle style)
    {
        GameObject gameObject = FindOrCreate(parent, name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        Text text = gameObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void ConfigureButton(Button button, string caption, Vector2 position)
    {
        RectTransform rect = button.GetComponent<RectTransform>();
        SetCentered(rect, new Vector2(300f, 72f), position);
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = caption;
            label.fontSize = 30;
            label.fontStyle = FontStyle.Bold;
        }
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = new Color(1f, 0.68f, 0.08f, 1f);
    }

    private static void Assign(SerializedObject target, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property == null) throw new MissingFieldException(target.targetObject.GetType().Name, propertyName);
        property.objectReferenceValue = value;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetCentered(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}
