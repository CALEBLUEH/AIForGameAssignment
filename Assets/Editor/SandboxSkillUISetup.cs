using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class SandboxSkillUISetup
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string IconFolder = "Assets/UI/SkillIcons/";

    [MenuItem("Tools/AI For Game/Build Sandbox Three-Card Skill UI")]
    public static void Build()
    {
        ImportIcons();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        TransformSnapshot[] worldSnapshots = CaptureWorldTransforms(scene);
        BattleDirector director = FindAll<BattleDirector>(scene).Single();
        if (director.battlePanel == null) throw new InvalidOperationException("BattleDirector has no Battle HUD reference.");

        CanvasScaler scaler = director.battlePanel.GetComponentInParent<CanvasScaler>(true);
        if (scaler == null) throw new InvalidOperationException("Battle HUD has no CanvasScaler parent.");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        EditorUtility.SetDirty(scaler);

        RectTransform hud = director.battlePanel.GetComponent<RectTransform>();
        Stretch(hud);

        Anchor(FindRect(hud, "Level Badge"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -24f), new Vector2(230f, 62f));
        Anchor(FindRect(hud, "Stage Status"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -96f), new Vector2(720f, 40f));
        Anchor(FindRect(hud, "Enemy Counter"), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-270f, -24f), new Vector2(150f, 58f));
        Anchor(FindRect(hud, "Timer Panel"), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-98f, -24f), new Vector2(165f, 58f));
        Anchor(director.pauseButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-24f, -92f), new Vector2(72f, 72f));
        Anchor(director.bossHealthPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -24f), new Vector2(560f, 82f));

        Anchor(FindRect(hud, "Selected Unit Panel"), Vector2.zero, Vector2.zero,
            new Vector2(24f, 24f), new Vector2(650f, 84f));
        for (int i = 0; i < director.squadButtons.Length; i++)
            Anchor(director.squadButtons[i].GetComponent<RectTransform>(), Vector2.zero, Vector2.zero,
                new Vector2(24f + i * 152f, 122f), new Vector2(145f, 90f));
        Anchor(director.moveButton.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero,
            new Vector2(24f, 226f), new Vector2(145f, 62f));

        Button[] cardButtons = { director.skillButton, director.healButton, director.coverButton };
        var cards = new SkillCardUI[3];
        for (int i = 0; i < cardButtons.Length; i++)
        {
            Button button = cardButtons[i];
            Anchor(button.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-404f + i * 190f, 116f), new Vector2(180f, 112f));
            cards[i] = ConfigureCard(button);
        }

        if (director.skillCooldownOverlay != null) director.skillCooldownOverlay.gameObject.SetActive(false);
        Anchor(director.speedButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-24f, 250f), new Vector2(150f, 76f));
        Anchor(director.autoButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-24f, 340f), new Vector2(150f, 76f));
        Anchor(director.manaSlider.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-24f, 78f), new Vector2(555f, 18f));

        RectTransform costBadge = FindRect(hud, "Cost Badge");
        Anchor(costBadge, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-594f, 18f), new Vector2(82f, 82f));
        for (int i = 0; i < director.energySegments.Length; i++)
            Anchor(director.energySegments[i].rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-502f + i * 52f, 28f), new Vector2(48f, 22f));

        SerializedObject serializedDirector = new SerializedObject(director);
        AssignObjectArray(serializedDirector.FindProperty("skillCards"), cards.Cast<Object>().ToArray());
        Assign(serializedDirector, "yuukaSkillIcon", Sprite("YuukaSkillIcon.png"));
        Assign(serializedDirector, "ayaneSkillIcon", Sprite("AyaneSkillIcon.png"));
        Assign(serializedDirector, "mikaSkillIcon", Sprite("MikaSkillIcon.png"));
        Assign(serializedDirector, "momoiSkillIcon", Sprite("MomoiSkillIcon.png"));
        Assign(serializedDirector, "hinaSkillIcon", Sprite("HinaSkillIcon.png"));
        Assign(serializedDirector, "healSkillIcon", Sprite("HealSkillIcon.png"));
        Assign(serializedDirector, "coverSkillIcon", Sprite("BlockSkillIcon.png"));
        serializedDirector.ApplyModifiedPropertiesWithoutUndo();

        foreach (CombatUnit unit in FindAll<CombatUnit>(scene))
        {
            if (unit.team != CombatUnit.CombatTeam.Player) continue;
            unit.SetApplyCharacterDataOnAwake(false);
            EditorUtility.SetDirty(unit);
        }

        VerifyWorldTransforms(worldSnapshots);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Unity could not save Sandbox_Gameplay.");
        AssetDatabase.SaveAssets();
        Debug.Log("SANDBOX_SKILL_UI_SETUP_OK Three FIFO cards configured; four-corner HUD anchored; player CombatUnit Inspector stats are authoritative.");
    }

    private static SkillCardUI ConfigureCard(Button button)
    {
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        SkillCardUI card = button.GetComponent<SkillCardUI>();
        if (card == null) card = button.gameObject.AddComponent<SkillCardUI>();

        Image icon = button.GetComponent<Image>();
        icon.color = Color.white;
        icon.type = Image.Type.Simple;
        icon.preserveAspect = true;

        Text nameText = button.GetComponentsInChildren<Text>(true)
            .FirstOrDefault(text => text.transform.parent == button.transform && text.name != "Skill Cost Text");
        if (nameText == null)
        {
            GameObject nameObject = new GameObject("Skill Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            nameObject.transform.SetParent(button.transform, false);
            nameText = nameObject.GetComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        nameRect.anchoredPosition = new Vector2(0f, 5f);
        nameRect.sizeDelta = new Vector2(-12f, 28f);
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.fontSize = 16;
        nameText.fontStyle = FontStyle.Bold;
        nameText.color = Color.white;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;

        Transform oldBadge = button.transform.Find("Skill Cost Badge");
        GameObject badge = oldBadge == null
            ? new GameObject("Skill Cost Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            : oldBadge.gameObject;
        badge.transform.SetParent(button.transform, false);
        RectTransform badgeRect = badge.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(0f, 1f);
        badgeRect.pivot = new Vector2(0f, 1f);
        badgeRect.anchoredPosition = new Vector2(6f, -6f);
        badgeRect.sizeDelta = new Vector2(42f, 36f);
        badge.GetComponent<Image>().color = new Color(0.02f, 0.08f, 0.14f, 0.9f);

        Text costText = badge.GetComponentInChildren<Text>(true);
        if (costText == null)
        {
            GameObject textObject = new GameObject("Skill Cost Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(badge.transform, false);
            costText = textObject.GetComponent<Text>();
        }
        RectTransform costRect = costText.rectTransform;
        Stretch(costRect);
        costText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        costText.alignment = TextAnchor.MiddleCenter;
        costText.fontSize = 23;
        costText.fontStyle = FontStyle.Bold;
        costText.color = Color.white;
        costText.raycastTarget = false;

        card.Configure(icon, costText, nameText);
        EditorUtility.SetDirty(group);
        EditorUtility.SetDirty(card);
        EditorUtility.SetDirty(icon);
        EditorUtility.SetDirty(nameText);
        EditorUtility.SetDirty(costText);
        return card;
    }

    private static void ImportIcons()
    {
        foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder })
                     .Select(AssetDatabase.GUIDToAssetPath))
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }

    private static Sprite Sprite(string fileName)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + fileName);
        if (sprite == null) throw new InvalidOperationException("Missing skill icon: " + fileName);
        return sprite;
    }

    private static void Assign(SerializedObject owner, string field, Object value)
    {
        SerializedProperty property = owner.FindProperty(field);
        if (property == null) throw new InvalidOperationException("BattleDirector is missing serialized field " + field + ".");
        property.objectReferenceValue = value;
    }

    private static void AssignObjectArray(SerializedProperty property, Object[] values)
    {
        if (property == null) throw new InvalidOperationException("BattleDirector is missing its skillCards field.");
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static RectTransform FindRect(Transform root, string name)
    {
        RectTransform result = root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(item => item.name == name);
        if (result == null) throw new InvalidOperationException("Battle HUD is missing " + name + ".");
        return result;
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        if (rect == null) throw new ArgumentNullException(nameof(rect));
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        EditorUtility.SetDirty(rect);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        EditorUtility.SetDirty(rect);
    }

    private readonly struct TransformSnapshot
    {
        public readonly Transform transform;
        public readonly Vector3 position;
        public readonly Quaternion rotation;
        public readonly Vector3 scale;

        public TransformSnapshot(Transform value)
        {
            transform = value;
            position = value.localPosition;
            rotation = value.localRotation;
            scale = value.localScale;
        }
    }

    private static TransformSnapshot[] CaptureWorldTransforms(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
        .Where(transform => !(transform is RectTransform))
        .Select(transform => new TransformSnapshot(transform)).ToArray();

    private static void VerifyWorldTransforms(IEnumerable<TransformSnapshot> snapshots)
    {
        foreach (TransformSnapshot snapshot in snapshots)
        {
            if (snapshot.transform == null) continue;
            if (snapshot.transform.localPosition != snapshot.position ||
                snapshot.transform.localRotation != snapshot.rotation ||
                snapshot.transform.localScale != snapshot.scale)
                throw new InvalidOperationException(snapshot.transform.name + " world transform changed during UI setup.");
        }
    }

    private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
}
