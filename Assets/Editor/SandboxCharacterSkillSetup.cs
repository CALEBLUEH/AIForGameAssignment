using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SandboxCharacterSkillSetup
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string MaterialFolder = "Assets/Materials";

    [MenuItem("Tools/AI For Game/Build Sandbox Character Skill Targeting")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RectSnapshot[] existingRects = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
            .Select(rect => new RectSnapshot(rect)).ToArray();

        BattleDirector director = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<BattleDirector>(true)).Single();
        Material outline = EnsureMaterial(MaterialFolder + "/SkillTargetOutline.mat", "AIFG/Skill Target Outline");
        Material shield = EnsureMaterial(MaterialFolder + "/SkillShield.mat", "AIFG/Skill Shield");
        Material indicator = EnsureMaterial(MaterialFolder + "/SkillGroundIndicator.mat", "AIFG/Skill Ground Indicator");

        GameObject overlayObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Skill Targeting Overlay");
        if (overlayObject == null)
        {
            overlayObject = new GameObject("Skill Targeting Overlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SkillTargetingOverlayUI));
        }
        Canvas canvas = overlayObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        CanvasScaler scaler = overlayObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        overlayObject.GetComponent<GraphicRaycaster>().enabled = false;

        Image dimmer = GetOrCreateImage(overlayObject.transform, "Targeting Dimmer");
        Stretch(dimmer.rectTransform);
        dimmer.color = new Color(0f, 0f, 0f, 0.55f);
        dimmer.raycastTarget = false;

        Image promptPanel = GetOrCreateImage(overlayObject.transform, "Skill Prompt Panel");
        RectTransform panelRect = promptPanel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(24f, -24f);
        panelRect.sizeDelta = new Vector2(690f, 126f);
        promptPanel.color = new Color(0.025f, 0.09f, 0.15f, 0.96f);
        promptPanel.raycastTarget = false;

        Text prompt = promptPanel.GetComponentInChildren<Text>(true);
        if (prompt == null)
        {
            GameObject textObject = new GameObject("Skill Prompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(promptPanel.transform, false);
            prompt = textObject.GetComponent<Text>();
        }
        Stretch(prompt.rectTransform, new Vector2(18f, 10f));
        prompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        prompt.fontSize = 22;
        prompt.fontStyle = FontStyle.Bold;
        prompt.alignment = TextAnchor.UpperLeft;
        prompt.color = Color.white;
        prompt.raycastTarget = false;

        SkillTargetingOverlayUI overlay = overlayObject.GetComponent<SkillTargetingOverlayUI>();
        overlay.Configure(dimmer, prompt);
        overlayObject.SetActive(false);

        var directorSO = new SerializedObject(director);
        directorSO.FindProperty("targetingOverlay").objectReferenceValue = overlay;
        directorSO.FindProperty("targetOutlineMaterial").objectReferenceValue = outline;
        directorSO.FindProperty("skillTargetingTimeScale").floatValue = 0.2f;
        directorSO.ApplyModifiedPropertiesWithoutUndo();
        if (director.targetingFeedback != null && director.targetingFeedback.rangeRenderer != null)
        {
            director.targetingFeedback.rangeRenderer.sharedMaterial = indicator;
            EditorUtility.SetDirty(director.targetingFeedback.rangeRenderer);
        }

        foreach (AutoCombatAI ai in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AutoCombatAI>(true)))
        {
            CombatUnit combatUnit = ai.GetComponent<CombatUnit>();
            if (combatUnit == null || combatUnit.team != CombatUnit.CombatTeam.Player) continue;
            ai.shieldMaterial = shield;
            switch (ai.role)
            {
                case AutoCombatAI.CombatRole.MikaSingleTarget:
                    ai.skillRange = 12f;
                    ai.targetSnapRadius = 2.4f;
                    ai.skillPowerMultiplier = 5f;
                    break;
                case AutoCombatAI.CombatRole.MomoiLowCostAOE:
                    ai.skillRange = 10f;
                    ai.coneAngle = 82f;
                    ai.damageTickCount = 6;
                    ai.damageTickDuration = 2f;
                    break;
                case AutoCombatAI.CombatRole.HinaHighCostAOE:
                    ai.skillRange = 15f;
                    ai.coneAngle = 42f;
                    ai.damageTickCount = 6;
                    ai.damageTickDuration = 2f;
                    break;
                case AutoCombatAI.CombatRole.AyaneHealer:
                    ai.skillRange = 11f;
                    ai.aoeRadius = 4f;
                    break;
                case AutoCombatAI.CombatRole.YuukaTank:
                    ai.shieldAmount = 90f;
                    ai.shieldDuration = 8f;
                    ai.shieldVisualRadius = 1.45f;
                    break;
            }
            EditorUtility.SetDirty(ai);
        }

        foreach (RectSnapshot snapshot in existingRects) snapshot.VerifyUnchanged();
        EditorUtility.SetDirty(overlay);
        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Sandbox_Gameplay.");
        AssetDatabase.SaveAssets();
        Debug.Log("SANDBOX_CHARACTER_SKILL_SETUP_OK targeting overlay/materials wired without modifying existing UI RectTransforms.");
    }

    private static Material EnsureMaterial(string path, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Missing shader " + shaderName);
        if (material == null)
        {
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
        }
        else material.shader = shader;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Image GetOrCreateImage(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null) return found.GetComponent<Image>();
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        return child.GetComponent<Image>();
    }

    private static void Stretch(RectTransform rect, Vector2 inset = default)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = -inset * 2f;
    }

    private readonly struct RectSnapshot
    {
        private readonly RectTransform rect;
        private readonly Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta;
        private readonly Vector3 localPosition, localScale;
        private readonly Quaternion localRotation;

        public RectSnapshot(RectTransform value)
        {
            rect = value;
            anchorMin = value.anchorMin;
            anchorMax = value.anchorMax;
            pivot = value.pivot;
            anchoredPosition = value.anchoredPosition;
            sizeDelta = value.sizeDelta;
            localPosition = value.localPosition;
            localRotation = value.localRotation;
            localScale = value.localScale;
        }

        public void VerifyUnchanged()
        {
            if (rect == null || rect.anchorMin != anchorMin || rect.anchorMax != anchorMax || rect.pivot != pivot ||
                rect.anchoredPosition != anchoredPosition || rect.sizeDelta != sizeDelta || rect.localPosition != localPosition ||
                rect.localRotation != localRotation || rect.localScale != localScale)
                throw new InvalidOperationException("Existing UI transform changed unexpectedly: " + (rect == null ? "missing" : rect.name));
        }
    }
}
