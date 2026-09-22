using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class EnvironmentPrefabValidator
{
    private const string PrefabRoot = "Assets/Prefabs/Environment";
    private const string SourceRoot = "Assets/ThirdParty/EnvironmentModels";

    [MenuItem("AIFG/Environment/Validate Generated Prefabs")]
    public static void Validate()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot });
        string[] prefabPaths = prefabGuids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
        List<string> errors = new List<string>();
        List<string> report = new List<string>();

        if (prefabPaths.Length != 27) errors.Add($"Expected 27 environment prefabs, found {prefabPaths.Length}.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/Decoration/AleppoDestroyedBuildingFront.prefab") != null)
            errors.Add("Aleppo point-cloud download must not produce a solid gameplay prefab.");

        foreach (string path in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { errors.Add("Could not load " + path); continue; }

            EnvironmentAsset descriptor = prefab.GetComponent<EnvironmentAsset>();
            if (descriptor == null) errors.Add(path + " has no EnvironmentAsset descriptor.");
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (filters.Length == 0) errors.Add(path + " has no imported meshes.");
            foreach (MeshFilter filter in filters)
                if (filter.sharedMesh == null) errors.Add(path + " contains a MeshFilter with no mesh: " + filter.name);
            foreach (Renderer renderer in renderers)
                if (renderer.sharedMaterials.Any(material => material == null))
                    errors.Add(path + " contains a missing material: " + renderer.name);

            Bounds bounds = CalculateBounds(renderers);
            if (!IsFinite(bounds.size) || bounds.size.sqrMagnitude < 0.000001f)
                errors.Add(path + " has invalid or empty render bounds.");
            if (Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) > 1000f)
                errors.Add(path + " still appears to use millimetres rather than Unity metres: " + bounds.size);

            bool isRoad = path.Contains("/Road/");
            bool isMovementBlocker = path.Contains("/Blockade/") || path.Contains("/Obstacle/");
            if (isRoad)
            {
                if (descriptor != null && descriptor.NavRole != EnvironmentAsset.NavigationRole.WalkableRoad)
                    errors.Add(path + " is not marked WalkableRoad.");
                foreach (MeshFilter filter in filters)
                    if (filter.sharedMesh != null && filter.GetComponent<MeshCollider>() == null)
                        errors.Add(path + " road mesh has no MeshCollider: " + filter.name);
            }
            else if (isMovementBlocker)
            {
                if (prefab.GetComponent<BoxCollider>() == null) errors.Add(path + " has no movement collider.");
                NavMeshModifier modifier = prefab.GetComponent<NavMeshModifier>();
                if (modifier == null || !modifier.overrideArea || modifier.area != NavMesh.GetAreaFromName("Not Walkable"))
                    errors.Add(path + " is not configured as a Not Walkable NavMesh modifier.");
                BlockingObstacle blocker = prefab.GetComponent<BlockingObstacle>();
                if (blocker == null) errors.Add(path + " has no BlockingObstacle combat filter.");
                bool expectedSightBlock = path.Contains("/Normal/") || path.Contains("/Obstacle/");
                if (blocker != null && blocker.blocksLineOfSight != expectedSightBlock)
                    errors.Add(path + " has the wrong line-of-sight behavior.");
            }
            else if (descriptor != null && descriptor.NavRole != EnvironmentAsset.NavigationRole.VisualOnly)
            {
                errors.Add(path + " decoration is not marked VisualOnly.");
            }

            report.Add($"{path}: meshes={filters.Length}, materials={renderers.Sum(r => r.sharedMaterials.Length)}, bounds={bounds.size}");
        }

        string[] licences = Directory.GetFiles(SourceRoot, "license.txt", SearchOption.AllDirectories);
        if (licences.Length != 16) errors.Add($"Expected 16 copied licence files, found {licences.Length}.");

        Debug.Log("Environment prefab validation inventory:\n" + string.Join("\n", report));
        if (errors.Count > 0) throw new InvalidOperationException("Environment validation failed:\n- " + string.Join("\n- ", errors));
        Debug.Log($"ENVIRONMENT_PREFAB_VALIDATION_OK prefabs={prefabPaths.Length} licences={licences.Length}");
    }

    private static Bounds CalculateBounds(Renderer[] renderers)
    {
        if (renderers.Length == 0) return new Bounds();
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static bool IsFinite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);
}
