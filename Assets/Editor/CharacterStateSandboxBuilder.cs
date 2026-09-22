using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CharacterStateSandboxBuilder
{
    private const string ScenePath = "Assets/Scenes/Sandbox_CharacterStates.unity";
    private const string GeneratedRootName = "Character Preview Sandbox";
    private const string AnimationFolder = "Assets/Animations/CharacterPreview";
    private const string PrefabFolder = "Assets/Prefabs/CharacterPreview";
    private const string MaterialFolder = "Assets/Materials/CharacterPreview";
    private const string MeshFolder = "Assets/Meshes/CharacterPreview";
    private const string ControllerPath = AnimationFolder + "/CharacterPreview.controller";
    private static readonly string[] StateNames = { "Idle", "Run", "Attack", "Cover", "Retreat" };
    private static readonly CharacterDefinition[] CharacterDefinitions =
    {
        new CharacterDefinition("Hina", "Assets/ThirdParty/BlueArchiveModels/Halano/Hina/Hina_Original_Mesh.fbx"),
        new CharacterDefinition("Momoi", "Assets/ThirdParty/BlueArchiveModels/Halano/Momoi/Momoi_Original_Mesh.fbx"),
        new CharacterDefinition("Mika", "Assets/ThirdParty/BlueArchiveModels/Halano/Mika/CH0069_Mesh.fbx",
            "Assets/ThirdParty/BlueArchiveModels/Halano/Mika/CH0069_Halo.fbx"),
        new CharacterDefinition("Ayane", "Assets/ThirdParty/BlueArchiveModels/Halano/Ayane/Ayane_Original_Mesh.fbx"),
        new CharacterDefinition("Yuuka", "Assets/ThirdParty/BlueArchiveModels/Halano/Yuuka/Yuuka_Original_Mesh.fbx")
    };

    [MenuItem("AIFG/Character Preview/Rebuild Sandbox Scene")]
    public static void BuildScene()
    {
        EnsureFolder("Assets", "Animations");
        EnsureFolder("Assets/Animations", "CharacterPreview");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "CharacterPreview");
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "CharacterPreview");
        EnsureFolder("Assets", "Meshes");
        EnsureFolder("Assets/Meshes", "CharacterPreview");

        CharacterModelAuditor.AuditImportedModels();
        RefreshGeneratedMaterials();
        AnimatorController controller = BuildAnimatorController();
        GameObject[] characterPrefabs = CharacterDefinitions
            .Select(definition => GetOrCreateCharacterPrefab(definition, controller))
            .ToArray();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        CharacterStatePreviewController existing = UnityEngine.Object.FindFirstObjectByType<CharacterStatePreviewController>();
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

        ConfigureCamera();
        ConfigureLight();

        GameObject root = new GameObject(GeneratedRootName);
        CharacterStatePreviewController preview = root.AddComponent<CharacterStatePreviewController>();
        GameObject coverPreview = CreateStage(root.transform);

        GameObject modelStage = new GameObject("Model Stage");
        modelStage.transform.SetParent(root.transform, false);
        modelStage.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        GameObject[] characters = new GameObject[characterPrefabs.Length];
        for (int i = 0; i < characterPrefabs.Length; i++)
        {
            GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefabs[i], scene);
            character.transform.SetParent(modelStage.transform, false);
            character.name = CharacterDefinitions[i].displayName + " Preview";
            character.SetActive(i == 0);
            characters[i] = character;
        }

        UIReferences ui = CreateUI(root.transform, CharacterDefinitions.Select(definition => definition.displayName).ToArray());
        AssignPreviewReferences(preview, characters, coverPreview, ui);
        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("CHARACTER_SANDBOX_BUILD_COMPLETE scene=" + ScenePath);
    }

    public static void BuildFromCommandLine()
    {
        try
        {
            BuildScene();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static AnimatorController BuildAnimatorController()
    {
        var clips = new Dictionary<string, AnimationClip>
        {
            { "Idle", CreateIdleClip() },
            { "Run", CreateRunClip() },
            { "Attack", CreateAttackClip() },
            { "Cover", CreateCoverClip() },
            { "Retreat", CreateRetreatClip() }
        };

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);
        foreach (string stateName in StateNames)
        {
            AnimatorState state = machine.AddState(stateName);
            state.motion = clips[stateName];
            if (stateName == "Idle") machine.defaultState = state;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimationClip CreateIdleClip()
    {
        AnimationClip clip = PrepareClip("Idle", 2f);
        SetMuscle(clip, "Left Upper Leg Front-Back", Constant(0f, 2f));
        SetMuscle(clip, "Right Upper Leg Front-Back", Constant(0f, 2f));
        SetMuscle(clip, "Left Lower Leg Stretch", Constant(0.55f, 2f));
        SetMuscle(clip, "Right Lower Leg Stretch", Constant(0.55f, 2f));
        SetMuscle(clip, "Spine Front-Back", Wave(2f, -0.05f, -0.03f, -0.05f));
        SetMuscle(clip, "Chest Front-Back", Wave(2f, -0.03f, -0.05f, -0.03f));
        SetMuscle(clip, "Head Nod Down-Up", Wave(2f, 0.01f, -0.015f, 0.01f));
        SetMuscle(clip, "Left Arm Down-Up", Constant(-0.78f, 2f));
        SetMuscle(clip, "Right Arm Down-Up", Constant(-0.78f, 2f));
        SetMuscle(clip, "Left Arm Front-Back", Constant(-0.03f, 2f));
        SetMuscle(clip, "Right Arm Front-Back", Constant(0.03f, 2f));
        SetMuscle(clip, "Left Forearm Stretch", Constant(0.62f, 2f));
        SetMuscle(clip, "Right Forearm Stretch", Constant(0.62f, 2f));
        SetMuscle(clip, "Left Hand In-Out", Constant(-0.04f, 2f));
        SetMuscle(clip, "Right Hand In-Out", Constant(0.04f, 2f));
        SetMuscle(clip, "Left Hand Down-Up", Constant(-0.05f, 2f));
        SetMuscle(clip, "Right Hand Down-Up", Constant(0.05f, 2f));
        FinishClip(clip);
        return clip;
    }

    private static AnimationClip CreateRunClip()
    {
        AnimationClip clip = PrepareClip("Run", 0.8f);
        SetMuscle(clip, "Left Upper Leg Front-Back", Alternating(0.8f, 0.30f));
        SetMuscle(clip, "Right Upper Leg Front-Back", Alternating(0.8f, -0.30f));
        SetMuscle(clip, "Left Arm Front-Back", Alternating(0.8f, -0.25f));
        SetMuscle(clip, "Right Arm Front-Back", Alternating(0.8f, 0.20f));
        SetMuscle(clip, "Left Arm Down-Up", Constant(-0.62f, 0.8f));
        SetMuscle(clip, "Right Arm Down-Up", Constant(-0.62f, 0.8f));
        SetMuscle(clip, "Left Forearm Stretch", Constant(0.55f, 0.8f));
        SetMuscle(clip, "Right Forearm Stretch", Constant(0.55f, 0.8f));
        SetMuscle(clip, "Left Lower Leg Stretch", BentStride(0.8f, false));
        SetMuscle(clip, "Right Lower Leg Stretch", BentStride(0.8f, true));
        SetMuscle(clip, "Spine Front-Back", Constant(-0.08f, 0.8f));
        SetMuscle(clip, "Chest Front-Back", Constant(-0.04f, 0.8f));
        FinishClip(clip);
        return clip;
    }

    private static AnimationClip CreateAttackClip()
    {
        const float length = 1.6f;
        AnimationClip clip = PrepareClip("Attack", length);
        SetMuscle(clip, "Left Upper Leg Front-Back", Constant(0f, length));
        SetMuscle(clip, "Right Upper Leg Front-Back", Constant(0f, length));
        SetMuscle(clip, "Left Lower Leg Stretch", Constant(0.55f, length));
        SetMuscle(clip, "Right Lower Leg Stretch", Constant(0.55f, length));
        // Keep each upper arm on its own side of the torso. Both shoulders move
        // forward by the same amount, while the elbows supply the small inward
        // angle that forms the two sides of the aiming triangle.
        SetMuscle(clip, "Left Arm Down-Up", Constant(-0.08f, length));
        SetMuscle(clip, "Right Arm Down-Up", Constant(-0.03f, length));
        SetMuscle(clip, "Left Arm Front-Back", Constant(-0.48f, length));
        SetMuscle(clip, "Right Arm Front-Back", Constant(-0.45f, length));
        SetMuscle(clip, "Left Arm Twist In-Out", Constant(-0.04f, length));
        SetMuscle(clip, "Right Arm Twist In-Out", Constant(0f, length));
        SetMuscle(clip, "Left Forearm Stretch", RecoilLoop(length, 0.80f, 0.62f));
        SetMuscle(clip, "Right Forearm Stretch", RecoilLoop(length, 0.85f, 0.67f));
        SetMuscle(clip, "Left Hand Down-Up", Constant(-0.06f, length));
        SetMuscle(clip, "Right Hand Down-Up", Constant(0.02f, length));
        SetMuscle(clip, "Left Hand In-Out", Constant(-0.04f, length));
        SetMuscle(clip, "Right Hand In-Out", Constant(0f, length));
        SetMuscle(clip, "Spine Front-Back", RecoilLoop(length, -0.05f, 0.02f));
        SetMuscle(clip, "Chest Front-Back", RecoilLoop(length, -0.03f, 0.015f));
        FinishClip(clip);
        return clip;
    }

    private static AnimationClip CreateCoverClip()
    {
        AnimationClip clip = PrepareClip("Cover", 1.6f);
        SetMuscle(clip, "Left Upper Leg Front-Back", Constant(0.20f, 1.6f));
        SetMuscle(clip, "Right Upper Leg Front-Back", Constant(0.20f, 1.6f));
        SetMuscle(clip, "Left Lower Leg Stretch", Constant(-0.62f, 1.6f));
        SetMuscle(clip, "Right Lower Leg Stretch", Constant(-0.62f, 1.6f));
        SetMuscle(clip, "Spine Front-Back", Wave(1.6f, -0.24f, -0.28f, -0.24f));
        SetMuscle(clip, "Chest Front-Back", Constant(-0.10f, 1.6f));
        SetMuscle(clip, "Head Nod Down-Up", Constant(0.24f, 1.6f));
        SetMuscle(clip, "Left Arm Front-Back", Constant(0.28f, 1.6f));
        SetMuscle(clip, "Right Arm Front-Back", Constant(0.28f, 1.6f));
        FinishClip(clip);
        return clip;
    }

    private static AnimationClip CreateRetreatClip()
    {
        const float length = 2.4f;
        AnimationClip clip = PrepareClip("Retreat", length);
        SetMuscle(clip, "Left Upper Leg Front-Back", Constant(0.18f, length));
        SetMuscle(clip, "Right Upper Leg Front-Back", Constant(0.18f, length));
        SetMuscle(clip, "Left Lower Leg Stretch", Constant(-0.60f, length));
        SetMuscle(clip, "Right Lower Leg Stretch", Constant(-0.60f, length));
        SetMuscle(clip, "Left Upper Leg In-Out", Constant(-0.10f, length));
        SetMuscle(clip, "Right Upper Leg In-Out", Constant(0.10f, length));
        SetMuscle(clip, "Spine Front-Back", Wave(length, -0.18f, -0.22f, -0.18f));
        SetMuscle(clip, "Chest Front-Back", Wave(length, -0.10f, -0.13f, -0.10f));
        SetMuscle(clip, "Head Nod Down-Up", Wave(length, -0.34f, -0.39f, -0.34f));
        SetMuscle(clip, "Left Arm Down-Up", Constant(-0.70f, length));
        SetMuscle(clip, "Right Arm Down-Up", Constant(-0.70f, length));
        SetMuscle(clip, "Left Arm Front-Back", Constant(0.06f, length));
        SetMuscle(clip, "Right Arm Front-Back", Constant(0.12f, length));
        SetMuscle(clip, "Left Forearm Stretch", Constant(-0.06f, length));
        SetMuscle(clip, "Right Forearm Stretch", Constant(-0.12f, length));
        FinishClip(clip);
        return clip;
    }

    private static AnimationClip PrepareClip(string name, float length)
    {
        string path = AnimationFolder + "/" + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(clip, path);
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            AnimationUtility.SetEditorCurve(clip, binding, null);
        clip.frameRate = 30f;
        return clip;
    }

    private static void FinishClip(AnimationClip clip)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.loopBlend = true;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionXZ = true;
        settings.keepOriginalPositionY = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void SetMuscle(AnimationClip clip, string muscleName, AnimationCurve curve)
    {
        if (!HumanTrait.MuscleName.Contains(muscleName))
        {
            Debug.LogWarning("Character preview muscle was not found: " + muscleName);
            return;
        }

        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscleName), curve);
    }

    private static AnimationCurve Wave(float length, float start, float middle, float end) =>
        new AnimationCurve(new Keyframe(0f, start), new Keyframe(length * 0.5f, middle), new Keyframe(length, end));
    private static AnimationCurve Constant(float value, float length) =>
        new AnimationCurve(new Keyframe(0f, value), new Keyframe(length, value));
    private static AnimationCurve Alternating(float length, float amplitude) =>
        new AnimationCurve(new Keyframe(0f, amplitude), new Keyframe(length * 0.5f, -amplitude), new Keyframe(length, amplitude));
    private static AnimationCurve BentStride(float length, bool offset) => offset
        ? new AnimationCurve(new Keyframe(0f, -0.05f), new Keyframe(length * 0.5f, -0.30f), new Keyframe(length, -0.05f))
        : new AnimationCurve(new Keyframe(0f, -0.30f), new Keyframe(length * 0.5f, -0.05f), new Keyframe(length, -0.30f));
    private static AnimationCurve RecoilLoop(float length, float aim, float recoil) =>
        new AnimationCurve(
            new Keyframe(0f, aim),
            new Keyframe(length * 0.20f, aim),
            new Keyframe(length * 0.23f, recoil),
            new Keyframe(length * 0.31f, aim),
            new Keyframe(length * 0.53f, aim),
            new Keyframe(length * 0.56f, recoil),
            new Keyframe(length * 0.64f, aim),
            new Keyframe(length * 0.86f, aim),
            new Keyframe(length * 0.89f, recoil),
            new Keyframe(length * 0.96f, aim),
            new Keyframe(length, aim));

    private static GameObject GetOrCreateCharacterPrefab(CharacterDefinition definition, RuntimeAnimatorController controller)
    {
        string prefabPath = PrefabFolder + "/" + definition.displayName + "Preview.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        bool hasRejectedYuukaHalo = existing != null && definition.displayName == "Yuuka" &&
            existing.GetComponentsInChildren<Transform>(true).Any(transform => transform.name == "Yuuka Halo");
        bool hasVisibleYuukaCalculator = existing != null && definition.displayName == "Yuuka" &&
            existing.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer =>
                renderer.enabled && renderer.name.IndexOf("Calculator", StringComparison.OrdinalIgnoreCase) >= 0);
        bool hasCombinedYuukaWeapon = existing != null && definition.displayName == "Yuuka" &&
            existing.GetComponentsInChildren<MeshRenderer>(true).Any(renderer => renderer.name == "Yuuka Weapon");
        bool hasUnbalancedYuukaSplit = existing != null && definition.displayName == "Yuuka" &&
            HasUnbalancedWeaponSplit(existing);
        bool hasLegacyYuukaSplit = existing != null && definition.displayName == "Yuuka" &&
            existing.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.name.StartsWith("Yuuka Weapon ", StringComparison.Ordinal))
                .Any(filter => filter.sharedMesh == null ||
                               filter.sharedMesh.name.IndexOf("Separated", StringComparison.Ordinal) < 0);
        if (existing != null && !hasRejectedYuukaHalo && !hasVisibleYuukaCalculator &&
            !hasCombinedYuukaWeapon && !hasUnbalancedYuukaSplit && !hasLegacyYuukaSplit) return existing;
        return BuildCharacterPrefab(definition.displayName, definition.modelPath, definition.accessoryPath, controller);
    }

    private static bool HasUnbalancedWeaponSplit(GameObject prefab)
    {
        int[] triangleCounts = prefab.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.name.StartsWith("Yuuka Weapon ", StringComparison.Ordinal) && filter.sharedMesh != null)
            .Select(filter => Enumerable.Range(0, filter.sharedMesh.subMeshCount)
                .Sum(subMesh => (int)filter.sharedMesh.GetIndexCount(subMesh) / 3))
            .ToArray();
        if (triangleCounts.Length != 2) return true;
        int total = triangleCounts.Sum();
        return total == 0 || triangleCounts.Min() < total * 0.30f;
    }

    private static GameObject BuildCharacterPrefab(string displayName, string modelPath, string accessoryPath,
        RuntimeAnimatorController controller)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (modelAsset == null) throw new InvalidOperationException("Missing character model: " + modelPath);
        string path = PrefabFolder + "/" + displayName + "Preview.prefab";
        Dictionary<string, WeaponTransform> weaponTransforms = ReadExistingWeaponTransforms(path, displayName);

        GameObject wrapper = new GameObject(displayName + " Preview Character");
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        model.transform.SetParent(wrapper.transform, false);
        model.name = displayName + " Model";
        GameObject accessory = InstantiateAccessory(displayName, accessoryPath, model.transform);
        ApplyMaterials(displayName, model);
        NormalizeModel(model, 1.75f);
        wrapper.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        AttachAccessoryToHead(accessory, animator, displayName);
        GameObject[] weapons = AttachRigidWeapons(displayName, model, animator);
        WeaponTransform fallbackWeaponTransform = weaponTransforms.TryGetValue(displayName + " Weapon", out WeaponTransform combined)
            ? combined
            : WeaponTransform.Identity;
        foreach (GameObject weapon in weapons)
        {
            WeaponTransform transform = weaponTransforms.TryGetValue(weapon.name, out WeaponTransform saved)
                ? saved
                : fallbackWeaponTransform;
            transform.ApplyTo(weapon.transform);
        }
        HideEmbeddedProps(model);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        AddGrounder(wrapper, model, animator);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, path);
        UnityEngine.Object.DestroyImmediate(wrapper);
        return prefab;
    }

    private static GameObject InstantiateAccessory(string displayName, string accessoryPath, Transform model)
    {
        if (string.IsNullOrEmpty(accessoryPath)) return null;
        GameObject accessoryAsset = AssetDatabase.LoadAssetAtPath<GameObject>(accessoryPath);
        if (accessoryAsset == null)
            throw new InvalidOperationException("Missing " + displayName + " accessory model: " + accessoryPath);
        GameObject accessory = (GameObject)PrefabUtility.InstantiatePrefab(accessoryAsset);
        accessory.transform.SetParent(model, false);
        accessory.name = displayName + " Halo";
        return accessory;
    }

    private static void AttachAccessoryToHead(GameObject accessory, Animator animator, string displayName)
    {
        if (accessory == null) return;
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) throw new InvalidOperationException(displayName + " is missing a head bone for its halo.");
        accessory.transform.SetParent(head, true);
    }

    private static void ApplyMaterials(string character, GameObject model)
    {
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material sourceMaterial = materials[i];
                string sourceName = sourceMaterial == null ? renderer.name : sourceMaterial.name;
                materials[i] = GetOrCreateMaterial(character, sourceName, sourceMaterial);
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static void RefreshGeneratedMaterials()
    {
        foreach (CharacterDefinition definition in CharacterDefinitions)
        {
            foreach (string assetPath in new[] { definition.modelPath, definition.accessoryPath })
            {
                if (string.IsNullOrEmpty(assetPath)) continue;
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset == null) continue;
                foreach (Renderer renderer in asset.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material sourceMaterial in renderer.sharedMaterials.Where(material => material != null))
                        GetOrCreateMaterial(definition.displayName, sourceMaterial.name, sourceMaterial);
                }
            }
        }
    }

    private static Material GetOrCreateMaterial(string character, string sourceName, Material sourceMaterial = null)
    {
        string safeName = sourceName.Replace(" (Instance)", string.Empty);
        string path = MaterialFolder + "/" + character + "_" + safeName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader toonShader = Shader.Find("AIFG/Character Preview Toon");
        if (toonShader == null) throw new InvalidOperationException("Character preview toon shader was not imported.");
        bool applyToonDefaults = material == null || material.shader != toonShader;
        if (material == null)
        {
            material = new Material(toonShader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != toonShader)
        {
            material.shader = toonShader;
        }

        bool isEyebrow = sourceName.IndexOf("Eyebrow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         sourceName.IndexOf("EyeBrow", StringComparison.OrdinalIgnoreCase) >= 0;
        Texture2D sourceTexture = sourceMaterial == null ? null : sourceMaterial.mainTexture as Texture2D;
        Texture2D texture = sourceTexture != null || (isEyebrow && sourceMaterial != null)
            ? sourceTexture
            : FindTexture(character, sourceName);
        material.mainTexture = texture;
        if (applyToonDefaults)
        {
            material.color = Color.white;
            material.SetColor("_ShadowColor", new Color(0.52f, 0.58f, 0.76f, 1f));
            material.SetFloat("_ShadowThreshold", 0.52f);
            material.SetFloat("_ShadowSoftness", 0.035f);
            material.SetFloat("_AmbientStrength", 0.22f);
            material.SetColor("_RimColor", new Color(0.55f, 0.75f, 1f, 1f));
            material.SetFloat("_RimPower", 3f);
            material.SetFloat("_RimStrength", 0.12f);
        }
        if (isEyebrow && sourceMaterial != null && sourceTexture == null)
            material.color = sourceMaterial.color;
        ConfigureFaceTransparency(material, character, sourceName, texture);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureFaceTransparency(Material material, string character, string sourceName, Texture2D texture)
    {
        bool isEyeMouth = sourceName.IndexOf("EyeMouth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          sourceName.IndexOf("Mouth", StringComparison.OrdinalIgnoreCase) >= 0;
        TextureImporter importer = texture == null ? null : AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
        bool useAlphaClip = isEyeMouth && importer != null && importer.DoesSourceTextureHaveAlpha();
        bool useColorKey = isEyeMouth && !useAlphaClip &&
                           (character == "Momoi" || character == "Mika" || character == "Yuuka");
        bool useMouthAtlas = isEyeMouth && character != "Hina";
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", useAlphaClip ? 1f : 0f);
        if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.08f);
        if (material.HasProperty("_ColorKeyEnabled")) material.SetFloat("_ColorKeyEnabled", useColorKey ? 1f : 0f);
        if (material.HasProperty("_UseMouthAtlas")) material.SetFloat("_UseMouthAtlas", useMouthAtlas ? 1f : 0f);
        if (material.HasProperty("_MouthTex"))
        {
            Texture2D mouthAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ThirdParty/BlueArchiveModels/Halano/Hina/Hina_Mouth.png");
            material.SetTexture("_MouthTex", mouthAtlas);
            material.SetVector("_MouthUvOffset", new Vector4(0f, 0.75f, 0f, 0f));
            material.SetColor("_MouthKeyColor", Color.white);
            material.SetFloat("_MouthKeyTolerance", 0.035f);
        }
        if (material.HasProperty("_ColorKey1") && material.HasProperty("_ColorKey2"))
        {
            Color firstKey = Color.white;
            Color secondKey = Color.white;
            float tolerance = 0.03f;
            if (character == "Momoi")
            {
                firstKey = Color.black;
                secondKey = Color.white;
                tolerance = 0.025f;
            }
            else if (character == "Mika")
            {
                firstKey = new Color(117f / 255f, 103f / 255f, 128f / 255f, 1f);
                secondKey = new Color(118f / 255f, 105f / 255f, 130f / 255f, 1f);
                tolerance = 0.045f;
            }
            else if (character == "Yuuka")
            {
                firstKey = Color.white;
                secondKey = Color.black;
                tolerance = 0.04f;
            }
            material.SetColor("_ColorKey1", firstKey);
            material.SetColor("_ColorKey2", secondKey);
            material.SetFloat("_ColorKeyTolerance", tolerance);
        }
        material.renderQueue = useAlphaClip || useColorKey
            ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest
            : -1;
        material.SetShaderPassEnabled("SHADOWCASTER", !isEyeMouth);
    }

    private static Texture2D FindTexture(string character, string materialName)
    {
        string folder = "Assets/ThirdParty/BlueArchiveModels/Halano/" + character;
        string semantic;
        if (materialName.IndexOf("Eyebrow", StringComparison.OrdinalIgnoreCase) >= 0 ||
            materialName.IndexOf("EyeBrow", StringComparison.OrdinalIgnoreCase) >= 0) semantic = "Face";
        else if (materialName.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0) semantic = "Hair";
        else if (materialName.IndexOf("Face", StringComparison.OrdinalIgnoreCase) >= 0) semantic = "Face";
        else if (materialName.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0) semantic = "EyeMouth";
        else if (materialName.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0) semantic = "Weapon";
        else if (materialName.IndexOf("GamePad", StringComparison.OrdinalIgnoreCase) >= 0) semantic = "GamePad";
        else if (materialName.IndexOf("Calculator", StringComparison.OrdinalIgnoreCase) >= 0) semantic = "Calculator";
        else if (materialName.IndexOf("Halo", StringComparison.OrdinalIgnoreCase) >= 0) semantic = "Halo";
        else semantic = "Body";

        string candidate = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => Path.GetFileNameWithoutExtension(path).IndexOf(semantic, StringComparison.OrdinalIgnoreCase) >= 0)
            .Where(path => path.IndexOf("Mask", StringComparison.OrdinalIgnoreCase) < 0 &&
                           path.IndexOf("Spec", StringComparison.OrdinalIgnoreCase) < 0)
            .OrderBy(path => path.Length)
            .FirstOrDefault();
        return string.IsNullOrEmpty(candidate) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(candidate);
    }

    private static void NormalizeModel(GameObject model, float targetHeight)
    {
        Bounds bounds = CalculateBounds(model);
        float scale = targetHeight / Mathf.Max(0.001f, bounds.size.y);
        model.transform.localScale = Vector3.one * scale;
        bounds = CalculateBounds(model);
        model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
    }

    private static GameObject[] AttachRigidWeapons(string displayName, GameObject model, Animator animator)
    {
        SkinnedMeshRenderer source = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(renderer => renderer.name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (source == null || hand == null)
            throw new InvalidOperationException(displayName + " is missing an embedded weapon renderer or right hand bone.");

        Mesh baked = new Mesh { name = displayName + " Preview Weapon" };
        source.BakeMesh(baked);
        // BakeMesh has already applied the FBX hierarchy scale. Preserve the
        // renderer's position and orientation, but do not apply that scale a
        // second time when converting the frozen mesh into hand-local space.
        Matrix4x4 bakedToWorld = Matrix4x4.TRS(source.transform.position, source.transform.rotation, Vector3.one);
        Matrix4x4 rendererToHand = hand.worldToLocalMatrix * bakedToWorld;
        Vector3[] vertices = baked.vertices;
        for (int i = 0; i < vertices.Length; i++) vertices[i] = rendererToHand.MultiplyPoint3x4(vertices[i]);
        baked.vertices = vertices;

        Vector3[] normals = baked.normals;
        Matrix4x4 normalMatrix = rendererToHand.inverse.transpose;
        for (int i = 0; i < normals.Length; i++) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
        baked.normals = normals;

        Vector4[] tangents = baked.tangents;
        for (int i = 0; i < tangents.Length; i++)
        {
            Vector3 direction = rendererToHand.MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
            tangents[i] = new Vector4(direction.x, direction.y, direction.z, tangents[i].w);
        }
        baked.tangents = tangents;
        baked.RecalculateBounds();

        if (displayName == "Yuuka")
        {
            Mesh[] splitMeshes = SplitMeshIntoTwoSpatialGroups(baked, displayName);
            UnityEngine.Object.DestroyImmediate(baked);
            var weapons = new GameObject[splitMeshes.Length];
            for (int i = 0; i < splitMeshes.Length; i++)
            {
                string meshPath = MeshFolder + "/" + displayName + "Weapon" + (i + 1) + ".asset";
                Mesh savedMesh = SaveOrUpdateMesh(splitMeshes[i], meshPath);
                weapons[i] = CreateRigidWeaponObject(displayName + " Weapon " + (i + 1), hand, savedMesh,
                    source.sharedMaterials);
            }
            AssetDatabase.DeleteAsset(MeshFolder + "/YuukaWeapon.asset");
            return weapons;
        }

        string singleMeshPath = MeshFolder + "/" + displayName + "Weapon.asset";
        Mesh singleSavedMesh = SaveOrUpdateMesh(baked, singleMeshPath);
        return new[] { CreateRigidWeaponObject(displayName + " Weapon", hand, singleSavedMesh, source.sharedMaterials) };
    }

    private static Mesh SaveOrUpdateMesh(Mesh generated, string meshPath)
    {
        Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (savedMesh == null)
        {
            AssetDatabase.CreateAsset(generated, meshPath);
            savedMesh = generated;
        }
        else
        {
            EditorUtility.CopySerialized(generated, savedMesh);
            UnityEngine.Object.DestroyImmediate(generated);
        }
        return savedMesh;
    }

    private static GameObject CreateRigidWeaponObject(string name, Transform hand, Mesh mesh, Material[] materials)
    {
        GameObject weapon = new GameObject(name);
        weapon.transform.SetParent(hand, false);
        weapon.AddComponent<MeshFilter>().sharedMesh = mesh;
        weapon.AddComponent<MeshRenderer>().sharedMaterials = materials;
        return weapon;
    }

    private static Mesh[] SplitMeshIntoTwoSpatialGroups(Mesh source, string displayName)
    {
        Vector3[] vertices = source.vertices;
        if (vertices.Length < 6) throw new InvalidOperationException(displayName + " weapon mesh is too small to split.");

        var triangles = new List<WeaponTriangle>();
        var sets = new DisjointSets(vertices.Length);
        for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
        {
            int[] indices = source.GetTriangles(subMesh);
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i];
                int b = indices[i + 1];
                int c = indices[i + 2];
                sets.Union(a, b);
                sets.Union(a, c);
                triangles.Add(new WeaponTriangle(subMesh, a, b, c,
                    (vertices[a] + vertices[b] + vertices[c]) / 3f));
            }
        }

        List<WeaponComponent> components = triangles.GroupBy(triangle => sets.Find(triangle.a))
            .Select(group => new WeaponComponent(group.ToList()))
            .ToList();
        if (components.Count < 2)
            throw new InvalidOperationException(displayName + " weapon mesh is one connected surface and cannot be split safely.");

        int[] assignments = FindBestTwoClusterSplit(components);

        Mesh first = CopyMeshChannels(source, displayName + " Preview Weapon 1 Separated");
        Mesh second = CopyMeshChannels(source, displayName + " Preview Weapon 2 Separated");
        first.subMeshCount = source.subMeshCount;
        second.subMeshCount = source.subMeshCount;
        int firstTriangleCount = 0;
        int secondTriangleCount = 0;
        for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
        {
            var firstTriangles = new List<int>();
            var secondTriangles = new List<int>();
            for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
            {
                List<int> target = assignments[componentIndex] == 0 ? firstTriangles : secondTriangles;
                foreach (WeaponTriangle triangle in components[componentIndex].triangles)
                {
                    if (triangle.subMesh != subMesh) continue;
                    target.Add(triangle.a);
                    target.Add(triangle.b);
                    target.Add(triangle.c);
                }
            }
            first.SetTriangles(firstTriangles, subMesh, false);
            second.SetTriangles(secondTriangles, subMesh, false);
            firstTriangleCount += firstTriangles.Count / 3;
            secondTriangleCount += secondTriangles.Count / 3;
        }

        int totalTriangleCount = firstTriangleCount + secondTriangleCount;
        if (firstTriangleCount < totalTriangleCount * 0.30f || secondTriangleCount < totalTriangleCount * 0.30f)
            throw new InvalidOperationException(displayName + " weapon mesh did not contain two separable spatial groups.");
        first.RecalculateBounds();
        second.RecalculateBounds();
        Debug.LogFormat("CHARACTER_WEAPON_SPLIT character={0} components={1} triangles={2}/{3}",
            displayName, components.Count, firstTriangleCount, secondTriangleCount);
        return new[] { first, second };
    }

    private static int[] FindBestTwoClusterSplit(IReadOnlyList<WeaponComponent> components)
    {
        int[] bestAssignments = null;
        float bestScore = float.PositiveInfinity;
        int totalWeight = components.Sum(component => component.triangleCount);
        for (int axis = 0; axis < 3; axis++)
        {
            WeaponComponent minimum = components.OrderBy(component => GetAxisValue(component.centroid, axis)).First();
            WeaponComponent maximum = components.OrderByDescending(component => GetAxisValue(component.centroid, axis)).First();
            Vector3 firstCenter = minimum.centroid;
            Vector3 secondCenter = maximum.centroid;
            var assignments = new int[components.Count];
            for (int iteration = 0; iteration < 16; iteration++)
            {
                Vector3 firstSum = Vector3.zero;
                Vector3 secondSum = Vector3.zero;
                int firstWeight = 0;
                int secondWeight = 0;
                for (int i = 0; i < components.Count; i++)
                {
                    WeaponComponent component = components[i];
                    assignments[i] = (component.centroid - firstCenter).sqrMagnitude <=
                                     (component.centroid - secondCenter).sqrMagnitude ? 0 : 1;
                    if (assignments[i] == 0)
                    {
                        firstSum += component.centroid * component.triangleCount;
                        firstWeight += component.triangleCount;
                    }
                    else
                    {
                        secondSum += component.centroid * component.triangleCount;
                        secondWeight += component.triangleCount;
                    }
                }
                if (firstWeight == 0 || secondWeight == 0) break;
                firstCenter = firstSum / firstWeight;
                secondCenter = secondSum / secondWeight;
            }

            int clusterWeight = components.Where((component, index) => assignments[index] == 0)
                .Sum(component => component.triangleCount);
            int smallerWeight = Mathf.Min(clusterWeight, totalWeight - clusterWeight);
            if (smallerWeight < totalWeight * 0.30f) continue;
            float score = 0f;
            for (int i = 0; i < components.Count; i++)
            {
                Vector3 center = assignments[i] == 0 ? firstCenter : secondCenter;
                score += (components[i].centroid - center).sqrMagnitude * components[i].triangleCount;
            }
            if (score >= bestScore) continue;
            bestScore = score;
            bestAssignments = (int[])assignments.Clone();
        }
        if (bestAssignments == null)
            throw new InvalidOperationException("Weapon components could not be divided into two balanced groups.");
        return bestAssignments;
    }

    private readonly struct WeaponTriangle
    {
        public readonly int subMesh;
        public readonly int a;
        public readonly int b;
        public readonly int c;
        public readonly Vector3 centroid;

        public WeaponTriangle(int subMesh, int a, int b, int c, Vector3 centroid)
        {
            this.subMesh = subMesh;
            this.a = a;
            this.b = b;
            this.c = c;
            this.centroid = centroid;
        }
    }

    private sealed class WeaponComponent
    {
        public readonly List<WeaponTriangle> triangles;
        public readonly Vector3 centroid;
        public readonly int triangleCount;

        public WeaponComponent(List<WeaponTriangle> triangles)
        {
            this.triangles = triangles;
            triangleCount = triangles.Count;
            centroid = triangles.Aggregate(Vector3.zero,
                (sum, triangle) => sum + triangle.centroid) / Mathf.Max(1, triangleCount);
        }
    }

    private sealed class DisjointSets
    {
        private readonly int[] parent;
        private readonly byte[] rank;

        public DisjointSets(int count)
        {
            parent = Enumerable.Range(0, count).ToArray();
            rank = new byte[count];
        }

        public int Find(int value)
        {
            while (parent[value] != value)
            {
                parent[value] = parent[parent[value]];
                value = parent[value];
            }
            return value;
        }

        public void Union(int first, int second)
        {
            int firstRoot = Find(first);
            int secondRoot = Find(second);
            if (firstRoot == secondRoot) return;
            if (rank[firstRoot] < rank[secondRoot]) parent[firstRoot] = secondRoot;
            else if (rank[firstRoot] > rank[secondRoot]) parent[secondRoot] = firstRoot;
            else
            {
                parent[secondRoot] = firstRoot;
                rank[firstRoot]++;
            }
        }
    }

    private static Mesh CopyMeshChannels(Mesh source, string name)
    {
        var copy = new Mesh { name = name, indexFormat = source.indexFormat };
        copy.vertices = source.vertices;
        if (source.normals.Length == source.vertexCount) copy.normals = source.normals;
        if (source.tangents.Length == source.vertexCount) copy.tangents = source.tangents;
        if (source.colors.Length == source.vertexCount) copy.colors = source.colors;
        if (source.uv.Length == source.vertexCount) copy.uv = source.uv;
        if (source.uv2.Length == source.vertexCount) copy.uv2 = source.uv2;
        return copy;
    }

    private static float GetAxisValue(Vector3 value, int axis)
    {
        if (axis == 0) return value.x;
        return axis == 1 ? value.y : value.z;
    }

    private static Dictionary<string, WeaponTransform> ReadExistingWeaponTransforms(string prefabPath, string displayName)
    {
        var transforms = new Dictionary<string, WeaponTransform>(StringComparer.Ordinal);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return transforms;
        foreach (Transform weapon in prefab.GetComponentsInChildren<Transform>(true).Where(transform =>
                     transform.name.StartsWith(displayName + " Weapon", StringComparison.Ordinal)))
            transforms[weapon.name] = new WeaponTransform(weapon);
        return transforms;
    }

    private readonly struct WeaponTransform
    {
        public static WeaponTransform Identity => new WeaponTransform(Vector3.zero, Quaternion.identity, Vector3.one);

        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly Vector3 scale;

        public WeaponTransform(Transform transform) : this(transform.localPosition, transform.localRotation, transform.localScale) { }

        private WeaponTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

        public void ApplyTo(Transform transform)
        {
            transform.localPosition = position;
            transform.localRotation = rotation;
            transform.localScale = scale;
        }
    }

    private static void HideEmbeddedProps(GameObject model)
    {
        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                renderer.name.IndexOf("GamePad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                renderer.name.IndexOf("Calculator", StringComparison.OrdinalIgnoreCase) >= 0)
                renderer.enabled = false;
        }
    }

    private static void AddGrounder(GameObject wrapper, GameObject model, Animator animator)
    {
        Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        SkinnedMeshRenderer body = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .First(renderer => renderer.name.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0);
        float lowerFootY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
        float footToSoleDistance = Mathf.Max(0f, lowerFootY - body.bounds.min.y);

        CharacterPreviewGrounder grounder = wrapper.AddComponent<CharacterPreviewGrounder>();
        SerializedObject serialized = new SerializedObject(grounder);
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("visualRoot").objectReferenceValue = model.transform;
        serialized.FindProperty("footToSoleDistance").floatValue = footToSoleDistance;
        serialized.FindProperty("groundOffset").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Bounds CalculateBounds(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(model.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static GameObject CreateStage(Transform parent)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Preview Platform";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, -0.08f, 0f);
        floor.transform.localScale = new Vector3(1.8f, 0.08f, 1.8f);
        Material material = GetOrCreateStageMaterial();
        floor.GetComponent<Renderer>().sharedMaterial = material;

        GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backdrop.name = "Backdrop";
        backdrop.transform.SetParent(parent, false);
        backdrop.transform.localPosition = new Vector3(0f, 1.6f, 1.8f);
        backdrop.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        backdrop.transform.localScale = new Vector3(6.5f, 3.6f, 1f);
        backdrop.GetComponent<Renderer>().sharedMaterial = material;

        GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cover.name = "Cover State Preview";
        cover.transform.SetParent(parent, false);
        cover.transform.localPosition = new Vector3(0f, 0.32f, -0.55f);
        cover.transform.localScale = new Vector3(1.55f, 0.64f, 0.28f);
        cover.GetComponent<Renderer>().sharedMaterial = material;
        cover.SetActive(false);
        return cover;
    }

    private static Material GetOrCreateStageMaterial()
    {
        string path = MaterialFolder + "/PreviewStage.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = new Color(0.08f, 0.14f, 0.24f, 1f);
        material.SetFloat("_Glossiness", 0.18f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            camera = cameraObject.GetComponent<Camera>();
        }
        camera.transform.position = new Vector3(0f, 2.15f, -5.2f);
        camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.78f, 0f) - camera.transform.position);
        camera.fieldOfView = 36f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.055f, 0.11f, 1f);
    }

    private static void ConfigureLight()
    {
        Light light = UnityEngine.Object.FindFirstObjectByType<Light>();
        if (light == null) light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.86f, 0.93f, 1f, 1f);
        light.intensity = 1.35f;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
    }

    private static UIReferences CreateUI(Transform parent, string[] characterNames)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject canvasObject = new GameObject("Character Preview Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Text title = CreateText(canvasObject.transform, "Title", font, "CHARACTER STATE LAB", 34, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(760f, 70f), new Vector2(0f, -52f));
        title.color = new Color(0.86f, 0.95f, 1f, 1f);

        GameObject modelRow = CreatePanel(canvasObject.transform, "Character Model Row", new Color(0.04f, 0.09f, 0.17f, 0.92f));
        SetRect(modelRow.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
            new Vector2(Mathf.Max(620f, characterNames.Length * 170f), 82f), new Vector2(0f, -126f));
        HorizontalLayoutGroup horizontal = modelRow.AddComponent<HorizontalLayoutGroup>();
        horizontal.padding = new RectOffset(16, 16, 13, 13);
        horizontal.spacing = 14f;
        horizontal.childForceExpandWidth = true;
        horizontal.childForceExpandHeight = true;
        Button[] modelButtons = characterNames.Select(name => CreateButton(modelRow.transform, name, font)).ToArray();

        GameObject stateColumn = CreatePanel(canvasObject.transform, "Character State Column", new Color(0.04f, 0.09f, 0.17f, 0.92f));
        SetRect(stateColumn.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(260f, 480f), new Vector2(160f, -20f));
        VerticalLayoutGroup vertical = stateColumn.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(16, 16, 16, 16);
        vertical.spacing = 12f;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = true;
        Button[] stateButtons = StateNames.Select(state => CreateButton(stateColumn.transform, state, font)).ToArray();

        Text selection = CreateText(canvasObject.transform, "Current Selection", font, string.Empty, 24, TextAnchor.MiddleCenter);
        SetRect(selection.rectTransform, new Vector2(0.5f, 0f), new Vector2(900f, 80f), new Vector2(0f, 84f));
        selection.color = new Color(0.83f, 0.91f, 0.98f, 1f);

        return new UIReferences { modelButtons = modelButtons, stateButtons = stateButtons, selection = selection };
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static Button CreateButton(Transform parent, string caption, Font font)
    {
        GameObject buttonObject = new GameObject(caption + " Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = new Color(0.10f, 0.18f, 0.30f, 0.96f);
        buttonObject.GetComponent<LayoutElement>().minHeight = 54f;
        Text label = CreateText(buttonObject.transform, "Label", font, caption.ToUpperInvariant(), 22, TextAnchor.MiddleCenter);
        label.color = Color.white;
        label.raycastTarget = false;
        Stretch(label.rectTransform);
        return buttonObject.GetComponent<Button>();
    }

    private static Text CreateText(Transform parent, string name, Font font, string value, int size, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void AssignPreviewReferences(CharacterStatePreviewController preview, GameObject[] characterRoots,
        GameObject coverPreview, UIReferences ui)
    {
        SerializedObject serialized = new SerializedObject(preview);
        SerializedProperty characters = serialized.FindProperty("characters");
        characters.arraySize = CharacterDefinitions.Length;
        for (int i = 0; i < CharacterDefinitions.Length; i++)
            SetCharacter(characters.GetArrayElementAtIndex(i), CharacterDefinitions[i].displayName, characterRoots[i]);

        SetObjectArray(serialized.FindProperty("characterButtons"), ui.modelButtons.Cast<UnityEngine.Object>().ToArray());
        SetObjectArray(serialized.FindProperty("stateButtons"), ui.stateButtons.Cast<UnityEngine.Object>().ToArray());
        serialized.FindProperty("selectionText").objectReferenceValue = ui.selection;
        serialized.FindProperty("coverPreview").objectReferenceValue = coverPreview;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetCharacter(SerializedProperty property, string displayName, GameObject root)
    {
        property.FindPropertyRelative("displayName").stringValue = displayName;
        property.FindPropertyRelative("root").objectReferenceValue = root;
        property.FindPropertyRelative("animator").objectReferenceValue = root.GetComponentInChildren<Animator>(true);
    }

    private static void SetObjectArray(SerializedProperty property, UnityEngine.Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
    }

    private readonly struct CharacterDefinition
    {
        public readonly string displayName;
        public readonly string modelPath;
        public readonly string accessoryPath;

        public CharacterDefinition(string displayName, string modelPath, string accessoryPath = null)
        {
            this.displayName = displayName;
            this.modelPath = modelPath;
            this.accessoryPath = accessoryPath;
        }
    }

    private struct UIReferences
    {
        public Button[] modelButtons;
        public Button[] stateButtons;
        public Text selection;
    }
}
