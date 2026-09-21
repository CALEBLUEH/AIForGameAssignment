using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterModelAuditor
{
    private static readonly string[] ModelPaths =
    {
        "Assets/ThirdParty/BlueArchiveModels/Halano/Hina/Hina_Original_Mesh.fbx",
        "Assets/ThirdParty/BlueArchiveModels/Halano/Momoi/Momoi_Original_Mesh.fbx",
        "Assets/ThirdParty/BlueArchiveModels/Halano/Mika/CH0069_Mesh.fbx",
        "Assets/ThirdParty/BlueArchiveModels/Halano/Ayane/Ayane_Original_Mesh.fbx",
        "Assets/ThirdParty/BlueArchiveModels/Halano/Yuuka/Yuuka_Original_Mesh.fbx"
    };

    [MenuItem("AIFG/Character Preview/Audit Imported Models")]
    public static void AuditImportedModels()
    {
        foreach (string path in ModelPaths)
        {
            ConfigureHumanoidImporter(path);
            ReportModel(path);
        }
    }

    public static void AuditFromCommandLine()
    {
        AuditImportedModels();
        AssetDatabase.SaveAssets();
        Debug.Log("CHARACTER_MODEL_AUDIT_COMPLETE");
        EditorApplication.Exit(0);
    }

    private static void ConfigureHumanoidImporter(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("No ModelImporter found for " + path);
        }

        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }

        if (importer.importAnimation)
        {
            importer.importAnimation = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static void ReportModel(string path)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        if (model == null)
        {
            Debug.LogError("CHARACTER_MODEL_AUDIT missing model: " + path);
            return;
        }

        SkinnedMeshRenderer[] skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        MeshRenderer[] staticMeshes = model.GetComponentsInChildren<MeshRenderer>(true);
        int bones = skinned.Sum(renderer => renderer.bones == null ? 0 : renderer.bones.Length);
        Bounds bounds = CalculateBounds(model);

        Debug.LogFormat(
            "CHARACTER_MODEL_AUDIT path={0} avatar={1} valid={2} humanoid={3} skinned={4} static={5} bones={6} bounds={7}",
            path,
            avatar != null,
            avatar != null && avatar.isValid,
            avatar != null && avatar.isHuman,
            skinned.Length,
            staticMeshes.Length,
            bones,
            string.Format("({0:F5}, {1:F5}, {2:F5})", bounds.size.x, bounds.size.y, bounds.size.z));

        foreach (SkinnedMeshRenderer renderer in skinned)
        {
            string blendShapes = renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount == 0
                ? "<none>"
                : string.Join(",", Enumerable.Range(0, renderer.sharedMesh.blendShapeCount)
                    .Select(renderer.sharedMesh.GetBlendShapeName));
            Debug.LogFormat(
                "CHARACTER_MODEL_RENDERER path={0} renderer={1} blendShapes={2} materials={3}",
                path,
                renderer.name,
                blendShapes,
                string.Join(",", renderer.sharedMaterials.Select(material => material == null ? "<missing>" : material.name)));

            foreach (Material material in renderer.sharedMaterials.Where(material => material != null))
            {
                Debug.LogFormat(
                    "CHARACTER_MODEL_MATERIAL path={0} renderer={1} material={2} shader={3} queue={4} mode={5} texture={6} color={7}",
                    path,
                    renderer.name,
                    material.name,
                    material.shader == null ? "<missing>" : material.shader.name,
                    material.renderQueue,
                    material.HasProperty("_Mode") ? material.GetFloat("_Mode").ToString("F0") : "<none>",
                    material.mainTexture == null ? "<missing>" : material.mainTexture.name,
                    material.HasProperty("_Color") ? material.color.ToString() : "<none>");
            }

            if (renderer.name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 && renderer.sharedMesh != null)
            {
                Mesh mesh = renderer.sharedMesh;
                BoneWeight[] weights = mesh.boneWeights;
                string dominantBones = weights.Length == 0
                    ? "<none>"
                    : string.Join(",", weights
                        .GroupBy(weight => DominantBoneName(renderer, weight))
                        .OrderByDescending(group => group.Count())
                        .Select(group => group.Key + ":" + group.Count()));
                Debug.LogFormat(
                    "CHARACTER_MODEL_WEAPON_TOPOLOGY path={0} renderer={1} vertices={2} subMeshes={3} bones={4} dominantBones={5}",
                    path, renderer.name, mesh.vertexCount, mesh.subMeshCount, renderer.bones.Length, dominantBones);
            }
        }

        string[] eyeControls = model.GetComponentsInChildren<Transform>(true)
            .Select(transform => transform.name)
            .Where(name => name.IndexOf("lid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("blink", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           name.IndexOf("eye_d", StringComparison.OrdinalIgnoreCase) >= 0)
            .Distinct()
            .ToArray();
        Debug.LogFormat("CHARACTER_MODEL_EYE_CONTROLS path={0} controls={1}", path,
            eyeControls.Length == 0 ? "<none>" : string.Join(",", eyeControls));
    }

    private static string DominantBoneName(SkinnedMeshRenderer renderer, BoneWeight weight)
    {
        int index = weight.boneIndex0;
        float value = weight.weight0;
        if (weight.weight1 > value) { index = weight.boneIndex1; value = weight.weight1; }
        if (weight.weight2 > value) { index = weight.boneIndex2; value = weight.weight2; }
        if (weight.weight3 > value) index = weight.boneIndex3;
        return index >= 0 && index < renderer.bones.Length && renderer.bones[index] != null
            ? renderer.bones[index].name
            : "<invalid>";
    }

    private static Bounds CalculateBounds(GameObject model)
    {
        GameObject instance = UnityEngine.Object.Instantiate(model);
        try
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
