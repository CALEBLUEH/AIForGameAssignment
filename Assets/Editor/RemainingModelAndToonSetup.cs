using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RemainingModelAndToonSetup
{
    private const string AronaSource = "Assets/ThirdParty/OtherModels/MainMenuModel/AronaRoom";
    private const string AronaPrefab = "Assets/Prefabs/Other/MainMenu/AronaRoom.prefab";
    private const string ToonMaterialRoot = "Assets/Materials/ImportedToon";
    private static readonly string[] PrefabRoots =
    {
        "Assets/Prefabs/Environment",
        "Assets/Prefabs/Enemies/Models",
        "Assets/Prefabs/Skills/Models",
        "Assets/Prefabs/Other"
    };

    [MenuItem("Tools/AI For Game/Import Remaining Models And Apply Toon Materials")]
    public static void Build()
    {
        RequireAronaSource();
        EnsureFolder("Assets/Prefabs/Other/MainMenu");
        EnsureFolder(ToonMaterialRoot);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        GameObject arona = AssetDatabase.LoadAssetAtPath<GameObject>(AronaPrefab);
        if (arona == null)
            arona = EnvironmentModelImporter.ImportStandaloneModel(
                AronaSource, "AronaRoom", AronaPrefab, 18f, true);
        ConfigureAronaRoom();

        Shader toon = Shader.Find("AIFG/Character Preview Toon");
        if (toon == null) throw new InvalidOperationException("Missing AIFG character toon shader.");

        var converted = new Dictionary<Material, Material>();
        int prefabCount = 0;
        int rendererCount = 0;
        foreach (string root in PrefabRoots)
        {
            if (!AssetDatabase.IsValidFolder(root)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                try
                {
                    foreach (Renderer renderer in contents.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                            continue;
                        Material[] materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++)
                        {
                            Material source = materials[i];
                            if (source == null || source.shader == toon) continue;
                            if (!converted.TryGetValue(source, out Material replacement))
                            {
                                replacement = GetOrCreateToonMaterial(source, toon);
                                converted.Add(source, replacement);
                            }
                            materials[i] = replacement;
                            changed = true;
                        }
                        if (!changed) continue;
                        renderer.sharedMaterials = materials;
                        EditorUtility.SetDirty(renderer);
                        rendererCount++;
                    }
                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        prefabCount++;
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Validate();
        Debug.Log($"REMAINING_MODEL_TOON_SETUP_OK Arona room imported as visual-only main-menu environment; toonPrefabs={prefabCount}, toonRenderers={rendererCount}, toonMaterials={converted.Count}. Source ZIP remains untouched and CC-BY-4.0 licence is preserved.");
    }

    [MenuItem("Tools/AI For Game/Validate Remaining Models And Toon Materials")]
    public static void Validate()
    {
        GameObject arona = AssetDatabase.LoadAssetAtPath<GameObject>(AronaPrefab);
        if (arona == null || arona.GetComponentsInChildren<Renderer>(true).Length == 0)
            throw new InvalidOperationException("Arona room prefab is missing or has no renderers.");
        EnvironmentAsset descriptor = arona.GetComponent<EnvironmentAsset>();
        if (descriptor == null || descriptor.NavRole != EnvironmentAsset.NavigationRole.VisualOnly ||
            descriptor.Visibility != EnvironmentAsset.CombatVisibility.DoesNotBlock || descriptor.UsableAsCover)
            throw new InvalidOperationException("Arona room must remain a visual-only main-menu model.");
        if (!File.Exists(Path.GetFullPath(AronaSource + "/license.txt")))
            throw new InvalidOperationException("Arona room CC-BY licence was not preserved.");

        Shader toon = Shader.Find("AIFG/Character Preview Toon");
        foreach (string root in PrefabRoots)
        {
            if (!AssetDatabase.IsValidFolder(root)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                        continue;
                    if (renderer.sharedMaterials.Any(material => material != null && material.shader != toon))
                        throw new InvalidOperationException(prefab.name + " still contains a non-toon mesh material.");
                }
            }
        }
    }

    private static void ConfigureAronaRoom()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(AronaPrefab);
        try
        {
            EnvironmentAsset descriptor = contents.GetComponent<EnvironmentAsset>();
            if (descriptor == null) descriptor = contents.AddComponent<EnvironmentAsset>();
            descriptor.Configure(EnvironmentAsset.NavigationRole.VisualOnly,
                EnvironmentAsset.CombatVisibility.DoesNotBlock, false);
            PrefabUtility.SaveAsPrefabAsset(contents, AronaPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static Material GetOrCreateToonMaterial(Material source, Shader toon)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId);
        if (string.IsNullOrEmpty(guid)) guid = Math.Abs(source.GetInstanceID()).ToString();
        string path = ToonMaterialRoot + "/" + guid + "_" + localId + ".mat";
        Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result == null)
        {
            result = new Material(toon) { name = source.name + " Toon" };
            AssetDatabase.CreateAsset(result, path);
        }

        Texture texture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") :
            source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
        Color color = source.HasProperty("_Color") ? source.GetColor("_Color") :
            source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
        result.shader = toon;
        result.SetTexture("_MainTex", texture);
        result.SetColor("_Color", color);
        if (source.HasProperty("_MainTex"))
        {
            result.SetTextureScale("_MainTex", source.GetTextureScale("_MainTex"));
            result.SetTextureOffset("_MainTex", source.GetTextureOffset("_MainTex"));
        }
        bool alphaClip = source.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.AlphaTest ||
            (source.HasProperty("_Mode") && source.GetFloat("_Mode") > 0.5f);
        result.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
        result.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.08f);
        EditorUtility.SetDirty(result);
        return result;
    }

    private static void RequireAronaSource()
    {
        foreach (string file in new[] { "scene.gltf", "scene.bin", "license.txt" })
            if (!File.Exists(Path.GetFullPath(AronaSource + "/" + file)))
                throw new FileNotFoundException("Incomplete Arona room source", AronaSource + "/" + file);
    }

    private static void EnsureFolder(string assetPath)
    {
        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
