using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SandboxSkillEffectsSetup
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string ModelPrefabFolder = "Assets/Prefabs/Skills/Models";

    [MenuItem("Tools/AI For Game/Build Sandbox Skill Effects")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        GameObject missile = EnvironmentModelImporter.ImportStandaloneModel(
            "Assets/ThirdParty/SkillModels/AirStrikeSkillModel", "Airstrike Missile",
            ModelPrefabFolder + "/AirstrikeMissile.prefab", 1.5f);
        GameObject medKit = EnvironmentModelImporter.ImportStandaloneModel(
            "Assets/ThirdParty/SkillModels/AyaneHealSkillModel", "Ayane Med Kit",
            ModelPrefabFolder + "/AyaneMedKit.prefab", 1.15f);
        GameObject barrier = EnvironmentModelImporter.ImportStandaloneModel(
            "Assets/ThirdParty/SkillModels/BlockSkillModel", "Skill Road Barrier",
            ModelPrefabFolder + "/SkillRoadBarrier.prefab", 3.2f);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RectSnapshot[] rects = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
            .Select(rect => new RectSnapshot(rect)).ToArray();
        BattleDirector director = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<BattleDirector>(true)).Single();
        SerializedObject directorSO = new SerializedObject(director);
        Assign(directorSO, "airstrikeMissilePrefab", missile);
        Assign(directorSO, "coverObstaclePrefab", barrier);
        Assign(directorSO, "buffSkillIcon", LoadSprite("BuffSkillIcon.png"));
        Assign(directorSO, "airstrikeSkillIcon", LoadSprite("AirStrikeSkillIcon.png"));
        directorSO.ApplyModifiedPropertiesWithoutUndo();

        foreach (AutoCombatAI ai in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AutoCombatAI>(true)))
        {
            CombatUnit unit = ai.GetComponent<CombatUnit>();
            if (unit == null || unit.team != CombatUnit.CombatTeam.Player) continue;
            ai.skillWindup = 0.45f;
            ai.skillRecovery = 0.35f;
            ai.skillProjectileSpeed = 28f;
            if (ai.role == AutoCombatAI.CombatRole.AyaneHealer) ai.medKitPrefab = medKit;
            EditorUtility.SetDirty(ai);
        }

        Material outline = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SkillTargetOutline.mat");
        if (outline != null)
        {
            outline.color = Color.white;
            if (outline.HasProperty("_Width")) outline.SetFloat("_Width", 0.004f);
            EditorUtility.SetDirty(outline);
        }

        foreach (RectSnapshot rect in rects) rect.VerifyUnchanged();
        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Sandbox_Gameplay.");
        AssetDatabase.SaveAssets();
        Debug.Log("SANDBOX_SKILL_EFFECTS_SETUP_OK models, skill references, effects and icons wired without changing UI transforms.");
    }

    private static Sprite LoadSprite(string filename)
    {
        string path = "Assets/UI/SkillIcons/" + filename;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Missing skill icon sprite: " + path);
        return sprite;
    }

    private static void Assign(SerializedObject owner, string field, UnityEngine.Object value)
    {
        SerializedProperty property = owner.FindProperty(field);
        if (property == null) throw new MissingFieldException(owner.targetObject.GetType().Name, field);
        property.objectReferenceValue = value;
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
