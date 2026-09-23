using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SandboxDashCoverSetup
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private static readonly Color HealthGreen = new Color(0.12f, 0.85f, 0.25f, 1f);
    private static readonly Color StaminaOrange = new Color(1f, 0.48f, 0.05f, 1f);

    [MenuItem("Tools/AI For Game/Build Dash and Cover Final Pass")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int staminaBars = 0;
        foreach (WorldUnitHUD hud in FindAll<WorldUnitHUD>(scene))
        {
            CombatUnit unit = hud.unit != null ? hud.unit : hud.GetComponentInParent<CombatUnit>();
            if (unit == null || unit.team != CombatUnit.CombatTeam.Player) continue;
            hud.unit = unit;
            hud.playerColor = HealthGreen;
            if (hud.healthFill != null) hud.healthFill.color = HealthGreen;
            EnsureStaminaBar(hud);
            staminaBars++;
            EditorUtility.SetDirty(hud);
        }

        foreach (AutoCombatAI ai in FindAll<AutoCombatAI>(scene))
        {
            CombatUnit unit = ai.GetComponent<CombatUnit>();
            if (unit == null || unit.team != CombatUnit.CombatTeam.Player) continue;
            ai.movementKind = AutoCombatAI.MovementKind.Dash;
            EditorUtility.SetDirty(ai);
        }

        foreach (CoverPoint point in FindAll<CoverPoint>(scene))
        {
            point.protection = 1f;
            EditorUtility.SetDirty(point);
        }
        foreach (TacticalCoverObstacle obstacle in FindAll<TacticalCoverObstacle>(scene))
        {
            obstacle.ConfigureCapacity(1);
            EditorUtility.SetDirty(obstacle);
        }

        SkillTargetingFeedback feedback = FindAll<SkillTargetingFeedback>(scene).FirstOrDefault();
        if (feedback != null && feedback.arrowRenderer != null)
        {
            feedback.arrowRenderer.widthMultiplier = 0.32f;
            feedback.arrowRenderer.numCornerVertices = 3;
            feedback.arrowRenderer.numCapVertices = 3;
            EditorUtility.SetDirty(feedback.arrowRenderer);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save Sandbox_Gameplay.");
        AssetDatabase.SaveAssets();
        Debug.Log($"SANDBOX_DASH_COVER_SETUP_OK staminaBars={staminaBars}, green health, orange stamina, Dash movement and capacity-one cover configured without moving existing HUD roots.");
    }

    private static void EnsureStaminaBar(WorldUnitHUD hud)
    {
        if (hud.healthSlider == null) return;
        Transform parent = hud.healthSlider.transform.parent;
        Transform existing = parent.Find("Movement Stamina");
        GameObject staminaObject = existing == null
            ? UnityEngine.Object.Instantiate(hud.healthSlider.gameObject, parent, false)
            : existing.gameObject;
        staminaObject.name = "Movement Stamina";

        Slider slider = staminaObject.GetComponent<Slider>();
        if (slider == null) slider = staminaObject.AddComponent<Slider>();
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = Mathf.Max(1f, hud.unit == null ? 100f : hud.unit.MovementPointCapacity);
        slider.value = slider.maxValue;

        RectTransform healthRect = hud.healthSlider.transform as RectTransform;
        RectTransform staminaRect = staminaObject.transform as RectTransform;
        if (healthRect != null && staminaRect != null && existing == null)
        {
            staminaRect.anchorMin = healthRect.anchorMin;
            staminaRect.anchorMax = healthRect.anchorMax;
            staminaRect.pivot = healthRect.pivot;
            staminaRect.anchoredPosition = healthRect.anchoredPosition + Vector2.down *
                (Mathf.Max(8f, healthRect.rect.height) + 4f);
            staminaRect.sizeDelta = new Vector2(healthRect.sizeDelta.x,
                Mathf.Max(6f, healthRect.sizeDelta.y * 0.7f));
        }

        Image fill = slider.fillRect == null ? null : slider.fillRect.GetComponent<Image>();
        if (fill != null) fill.color = StaminaOrange;
        hud.staminaSlider = slider;
        hud.staminaFill = fill;
        hud.staminaColor = StaminaOrange;
    }

    private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
}
