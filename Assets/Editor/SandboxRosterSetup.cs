using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class SandboxRosterSetup
{
    private const string PreparationScenePath = "Assets/Scenes/SandBox_Preparation.unity";
    private const string GameplayScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string GameplaySceneName = "Sandbox_Gameplay";
    private const string PreparationSceneName = "SandBox_Preparation";
    private const string PreviewFolder = "Assets/Prefabs/CharacterPreview/";
    private const string PreparationPivotName = "Character Model Pivot";
    private static readonly string[] CharacterNames = { "Yuuka", "Ayane", "Mika", "Momoi", "Hina" };

    [MenuItem("Tools/AI For Game/Build Sandbox Preparation Roster")]
    public static void Build()
    {
        if (!System.IO.File.Exists(PreparationScenePath) || !System.IO.File.Exists(GameplayScenePath))
            throw new InvalidOperationException("Both SandBox_Preparation and Sandbox_Gameplay scenes are required.");

        BuildPreparationScene();
        BuildGameplayScene();
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SANDBOX_ROSTER_SETUP_OK: podium selection, drag rotation, battle transition, and five animated gameplay models are wired.");
    }

    [MenuItem("Tools/AI For Game/Validate Sandbox Preparation Roster")]
    public static void Validate()
    {
        ValidatePreparationScene(EditorSceneManager.OpenScene(PreparationScenePath, OpenSceneMode.Single));
        ValidateGameplayScene(EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single));
        Debug.Log("SANDBOX_ROSTER_VALIDATION_OK");
    }

    private static void BuildPreparationScene()
    {
        Scene scene = EditorSceneManager.OpenScene(PreparationScenePath, OpenSceneMode.Single);
        PreparationMenuController controller = FindSceneComponent<PreparationMenuController>(scene);
        Camera camera = FindSceneComponent<Camera>(scene);
        if (controller == null || camera == null)
            throw new InvalidOperationException("SandBox_Preparation needs its PreparationMenuController and camera.");

        var displays = new List<PreparationCharacterDisplay>();
        for (int i = 0; i < CharacterNames.Length; i++)
        {
            string characterName = CharacterNames[i];
            GameObject displayRoot = FindSceneObject(scene, characterName + " Ready Display");
            if (displayRoot == null) throw new InvalidOperationException("Missing preparation display for " + characterName + ".");

            Transform podium = FindDirectChild(displayRoot.transform, "Podium");
            if (podium == null || podium.GetComponent<Renderer>() == null || podium.GetComponent<Collider>() == null)
                throw new InvalidOperationException(characterName + " display needs a rendered podium with a collider.");

            SetLegacyVisualActive(displayRoot.transform, "Character Body", false);
            SetLegacyVisualActive(displayRoot.transform, "Tactical Visor", false);
            DestroyDirectChild(displayRoot.transform, PreparationPivotName);

            GameObject pivotObject = new GameObject(PreparationPivotName);
            pivotObject.transform.SetParent(displayRoot.transform, false);
            pivotObject.transform.localPosition = new Vector3(0f, 0.36f, 0f);

            GameObject model = InstantiatePreview(characterName, pivotObject.transform);
            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null) throw new InvalidOperationException(characterName + " preview prefab has no Animator.");

            PreparationCharacterDisplay display = displayRoot.GetComponent<PreparationCharacterDisplay>();
            if (display == null) display = displayRoot.AddComponent<PreparationCharacterDisplay>();
            display.Configure(i, pivotObject.transform, podium.GetComponent<Renderer>(), podium.GetComponent<Collider>(), animator);
            EditorUtility.SetDirty(display);
            displays.Add(display);
        }

        controller.ConfigureSandboxDisplays(displays.ToArray(), camera, GameplaySceneName);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Unity could not save SandBox_Preparation.");
        ValidatePreparationScene(scene);
    }

    private static void BuildGameplayScene()
    {
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        BattleDirector director = FindSceneComponent<BattleDirector>(scene);
        if (director == null) throw new InvalidOperationException("Sandbox_Gameplay needs a BattleDirector.");
        director.preparationSceneName = PreparationSceneName;
        EditorUtility.SetDirty(director);

        foreach (string characterName in CharacterNames)
        {
            GameObject unitRoot = FindPlayerUnit(scene, characterName);
            if (unitRoot == null) throw new InvalidOperationException("Missing player unit " + characterName + " in Sandbox_Gameplay.");

            MeshRenderer capsuleRenderer = unitRoot.GetComponent<MeshRenderer>();
            if (capsuleRenderer != null) capsuleRenderer.enabled = false;

            string modelName = "Character Model (" + characterName + ")";
            DestroyDirectChild(unitRoot.transform, modelName);
            GameObject model = InstantiatePreview(characterName, unitRoot.transform);
            model.name = modelName;
            model.transform.localPosition = new Vector3(0f, -1f, 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            Animator animator = model.GetComponentInChildren<Animator>(true);
            AutoCombatAI ai = unitRoot.GetComponent<AutoCombatAI>();
            CombatUnit unit = unitRoot.GetComponent<CombatUnit>();
            if (animator == null || ai == null || unit == null)
                throw new InvalidOperationException(characterName + " is missing its model Animator or combat components.");

            CombatAnimationDriver driver = unitRoot.GetComponent<CombatAnimationDriver>();
            if (driver == null) driver = unitRoot.AddComponent<CombatAnimationDriver>();
            driver.Configure(ai, unit, animator);
            EditorUtility.SetDirty(driver);
            if (capsuleRenderer != null) EditorUtility.SetDirty(capsuleRenderer);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Unity could not save Sandbox_Gameplay.");
        ValidateGameplayScene(scene);
    }

    private static GameObject InstantiatePreview(string characterName, Transform parent)
    {
        string path = PreviewFolder + characterName + "Preview.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing preview prefab: " + path);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null) throw new InvalidOperationException("Could not instantiate " + path + ".");
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static void EnsureBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        AddBuildScene(scenes, PreparationScenePath);
        AddBuildScene(scenes, GameplayScenePath);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void AddBuildScene(List<EditorBuildSettingsScene> scenes, string path)
    {
        if (scenes.Any(scene => scene.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
    }

    private static void ValidatePreparationScene(Scene scene)
    {
        PreparationMenuController controller = FindSceneComponent<PreparationMenuController>(scene);
        PreparationCharacterDisplay[] displays = FindSceneComponents<PreparationCharacterDisplay>(scene);
        if (controller == null || displays.Length != CharacterNames.Length)
            throw new InvalidOperationException("Preparation roster needs one controller and exactly five displays.");
        foreach (PreparationCharacterDisplay display in displays)
            if (display.ModelPivot == null || display.CharacterAnimator == null)
                throw new InvalidOperationException(display.name + " has incomplete model/animation wiring.");
    }

    private static void ValidateGameplayScene(Scene scene)
    {
        foreach (string characterName in CharacterNames)
        {
            GameObject root = FindPlayerUnit(scene, characterName);
            if (root == null || root.GetComponent<CombatAnimationDriver>() == null)
                throw new InvalidOperationException(characterName + " has no combat animation driver.");
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.enabled)
                throw new InvalidOperationException(characterName + " capsule renderer is still visible.");
            Transform model = FindDirectChild(root.transform, "Character Model (" + characterName + ")");
            if (model == null || model.GetComponentInChildren<Animator>(true) == null)
                throw new InvalidOperationException(characterName + " gameplay model is missing.");
        }
    }

    private static GameObject FindPlayerUnit(Scene scene, string characterName)
    {
        foreach (CombatUnit unit in FindSceneComponents<CombatUnit>(scene))
            if (unit.team == CombatUnit.CombatTeam.Player && unit.name == characterName) return unit.gameObject;
        return null;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component =>
        FindSceneComponents<T>(scene).FirstOrDefault();

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (Transform candidate in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
            if (candidate.name == objectName) return candidate.gameObject;
        return null;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == childName) return parent.GetChild(i);
        return null;
    }

    private static void DestroyDirectChild(Transform parent, string childName)
    {
        Transform child = FindDirectChild(parent, childName);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    private static void SetLegacyVisualActive(Transform parent, string childName, bool active)
    {
        Transform child = FindDirectChild(parent, childName);
        if (child != null) child.gameObject.SetActive(active);
    }
}
