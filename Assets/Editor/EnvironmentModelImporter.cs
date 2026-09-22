using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Imports the static Sketchfab glTF environment downloads without adding a runtime package.
/// Generated meshes/materials live beside copied source files and remain normal Unity assets.
/// </summary>
public static class EnvironmentModelImporter
{
    private const string DefaultSourceRoot =
        @"C:\Users\user\Desktop\Study\Y2S3\AIForGame\AIForGameModel\EnvironmentAndBackgroundModel";
    private const string ThirdPartyRoot = "Assets/ThirdParty/EnvironmentModels";
    private const string PrefabRoot = "Assets/Prefabs/Environment";

    private sealed class ImportSpec
    {
        public string relativeSource;
        public string outputGroup;
        public string prefabName;
        public EnvironmentAsset.NavigationRole navigationRole;
        public EnvironmentAsset.CombatVisibility visibility;
        public bool usableAsCover;
        public float sourceScale = 1f;
        public string[] splitNodes;
        public bool sourceOnly;
    }

    private static readonly ImportSpec[] Specs =
    {
        Road("Road/Road1", "AmericanRoad", 0.001f),
        Road("Road/Road2", "StreetRoad"),
        Road("Road/Road3", "RoadFree"),

        Blocker("Blockade/NormalBlockade/Blockade1", "Normal", "OldRustyCar", true, 0.001f),
        Blocker("Blockade/NormalBlockade/Blockade2", "Normal", "MobileDiner", true),
        Blocker("Blockade/NormalBlockade/Blockade3", "Normal", "TimberCarrier", true),
        Blocker("Blockade/SeenThroughBlockade/Blockade4", "SeeThrough", "TruckTire", false),
        Blocker("Blockade/SeenThroughBlockade/Blockade5", "SeeThrough", "WoodFence", false),
        Blocker("Blockade/SeenThroughBlockade/Blockade6", "SeeThrough", "FireHydrant", false),

        new ImportSpec
        {
            relativeSource = "Obstacle/Obstacle1", outputGroup = "Obstacle", prefabName = "ConcreteFence",
            navigationRole = EnvironmentAsset.NavigationRole.MovementBlocker,
            visibility = EnvironmentAsset.CombatVisibility.BlocksSightAndProjectiles,
            usableAsCover = true,
            sourceScale = 0.01f
        },

        Decoration("Decoration/Building1", "LowPolyBuilding"),
        new ImportSpec
        {
            relativeSource = "Decoration/Building2", outputGroup = "Decoration",
            prefabName = "AleppoDestroyedBuildingFront",
            navigationRole = EnvironmentAsset.NavigationRole.VisualOnly,
            visibility = EnvironmentAsset.CombatVisibility.DoesNotBlock,
            sourceOnly = true
        },
        Decoration("Decoration/Building3", "BuildingsSet",
            "Building_03", "Building_01", "Building", "Building_02", "Building_04",
            "Building_09", "Building_05", "Building_07", "Building_08", "Building_06"),
        Decoration("Decoration/Building4", "ChicagoBuildingsSet", "Building 2_2", "Building 1_3"),
        Decoration("Decoration/Building5", "Building"),
        Decoration("Decoration/Building6", "BuildingsCombined"),
    };

    [MenuItem("AIFG/Environment/Import Downloaded Environment Models")]
    public static void ImportAllFromDefault()
    {
        ImportAll(DefaultSourceRoot);
    }

    public static void ImportAll(string sourceRoot)
    {
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException("Environment model folder not found: " + sourceRoot);

        EnsureFolder(ThirdPartyRoot);
        EnsureFolder(PrefabRoot);
        int createdPrefabs = 0;
        List<string> notes = new List<string>();

        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (ImportSpec spec in Specs)
                CopySource(spec, sourceRoot);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        foreach (ImportSpec spec in Specs)
        {
            string assetFolder = ThirdPartyRoot + "/" + spec.relativeSource;
            if (spec.sourceOnly)
            {
                notes.Add(spec.prefabName + ": retained as source only (the download is a POINTS glTF, not triangle geometry). ");
                continue;
            }

            string gltfPath = assetFolder + "/scene.gltf";
            GltfRoot gltf = JsonUtility.FromJson<GltfRoot>(File.ReadAllText(ToAbsolutePath(gltfPath)));
            if (gltf == null) throw new InvalidDataException("Could not parse " + gltfPath);

            ImportContext context = new ImportContext(gltf, gltfPath, assetFolder);
            GameObject fullModel = context.BuildScene(spec.prefabName);
            fullModel.transform.localScale = Vector3.one * spec.sourceScale;
            ConfigureEnvironment(fullModel, spec);
            SavePrefab(fullModel, PrefabRoot + "/" + spec.outputGroup + "/" + spec.prefabName + ".prefab");
            createdPrefabs++;

            if (spec.splitNodes != null)
            {
                foreach (string nodeName in spec.splitNodes)
                {
                    Transform sourceNode = FindDescendant(fullModel.transform, nodeName);
                    if (sourceNode == null)
                    {
                        notes.Add(spec.prefabName + ": split node not found: " + nodeName);
                        continue;
                    }

                    string splitName = SanitizeName(nodeName);
                    GameObject wrapper = new GameObject(splitName);
                    GameObject clone = Object.Instantiate(sourceNode.gameObject);
                    clone.name = sourceNode.name;
                    clone.transform.SetParent(wrapper.transform, false);
                    clone.transform.SetPositionAndRotation(sourceNode.position, sourceNode.rotation);
                    clone.transform.localScale = sourceNode.lossyScale;
                    ConfigureEnvironment(wrapper, spec);
                    SavePrefab(wrapper, PrefabRoot + "/" + spec.outputGroup + "/Separated/" + splitName + ".prefab");
                    Object.DestroyImmediate(wrapper);
                    createdPrefabs++;
                }
            }

            notes.AddRange(context.Notes);
            Object.DestroyImmediate(fullModel);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string noteText = notes.Count == 0 ? "none" : string.Join("\n- ", notes);
        Debug.Log($"Environment import complete. Created {createdPrefabs} prefabs.\nNotes:\n- {noteText}");
    }

    public static GameObject ImportStandaloneModel(string assetFolder, string prefabName,
        string prefabPath, float targetLargestDimension)
    {
        string gltfPath = assetFolder.TrimEnd('/') + "/scene.gltf";
        if (!File.Exists(ToAbsolutePath(gltfPath)))
            throw new FileNotFoundException("Missing glTF source", gltfPath);

        GltfRoot gltf = JsonUtility.FromJson<GltfRoot>(File.ReadAllText(ToAbsolutePath(gltfPath)));
        if (gltf == null) throw new InvalidDataException("Could not parse " + gltfPath);
        var context = new ImportContext(gltf, gltfPath, assetFolder.TrimEnd('/'));
        GameObject model = context.BuildScene(prefabName + " Model");
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Object.DestroyImmediate(model);
            throw new InvalidDataException(prefabName + " contains no renderable triangle meshes.");
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float scale = targetLargestDimension / Mathf.Max(0.0001f, largest);
        model.transform.localScale = Vector3.one * scale;

        bounds = model.GetComponentsInChildren<Renderer>(true)[0].bounds;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(renderer.bounds);
        GameObject wrapper = new GameObject(prefabName);
        model.transform.SetParent(wrapper.transform, true);
        model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        SavePrefab(wrapper, prefabPath);
        Object.DestroyImmediate(wrapper);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private static ImportSpec Road(string source, string name, float sourceScale = 1f) => new ImportSpec
    {
        relativeSource = source, outputGroup = "Road", prefabName = name,
        navigationRole = EnvironmentAsset.NavigationRole.WalkableRoad,
        visibility = EnvironmentAsset.CombatVisibility.DoesNotBlock,
        sourceScale = sourceScale
    };

    private static ImportSpec Blocker(string source, string group, string name, bool blocksSight, float sourceScale = 1f) => new ImportSpec
    {
        relativeSource = source, outputGroup = "Blockade/" + group, prefabName = name,
        navigationRole = EnvironmentAsset.NavigationRole.MovementBlocker,
        visibility = blocksSight
            ? EnvironmentAsset.CombatVisibility.BlocksSightAndProjectiles
            : EnvironmentAsset.CombatVisibility.DoesNotBlock,
        sourceScale = sourceScale
    };

    private static ImportSpec Decoration(string source, string name, params string[] splitNodes) => new ImportSpec
    {
        relativeSource = source, outputGroup = "Decoration", prefabName = name,
        navigationRole = EnvironmentAsset.NavigationRole.VisualOnly,
        visibility = EnvironmentAsset.CombatVisibility.DoesNotBlock,
        splitNodes = splitNodes.Length == 0 ? null : splitNodes
    };

    private static void CopySource(ImportSpec spec, string sourceRoot)
    {
        string source = Path.Combine(sourceRoot, spec.relativeSource.Replace('/', Path.DirectorySeparatorChar));
        string destinationAssetPath = ThirdPartyRoot + "/" + spec.relativeSource;
        string destination = ToAbsolutePath(destinationAssetPath);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);

        if (AssetDatabase.IsValidFolder(destinationAssetPath)) AssetDatabase.DeleteAsset(destinationAssetPath);
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(file);
            if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            string relative = Path.GetRelativePath(source, file);
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, true);
        }
    }

    private static void ConfigureEnvironment(GameObject root, ImportSpec spec)
    {
        EnvironmentAsset descriptor = root.GetComponent<EnvironmentAsset>();
        if (descriptor == null) descriptor = root.AddComponent<EnvironmentAsset>();
        descriptor.Configure(spec.navigationRole, spec.visibility, spec.usableAsCover);

        if (spec.navigationRole == EnvironmentAsset.NavigationRole.WalkableRoad)
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                GameObjectUtility.SetStaticEditorFlags(filter.gameObject,
                    GameObjectUtility.GetStaticEditorFlags(filter.gameObject) |
                    StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);
            }
        }
        else if (spec.navigationRole == EnvironmentAsset.NavigationRole.MovementBlocker)
        {
            AddBoundsCollider(root);
            NavMeshModifier modifier = root.GetComponent<NavMeshModifier>();
            if (modifier == null) modifier = root.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NavMesh.GetAreaFromName("Not Walkable");
            BlockingObstacle blocker = root.GetComponent<BlockingObstacle>();
            if (blocker == null) blocker = root.AddComponent<BlockingObstacle>();
            blocker.blocksLineOfSight = spec.visibility == EnvironmentAsset.CombatVisibility.BlocksSightAndProjectiles;
            GameObjectUtility.SetStaticEditorFlags(root,
                GameObjectUtility.GetStaticEditorFlags(root) | StaticEditorFlags.NavigationStatic);
        }
    }

    private static void AddBoundsCollider(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds localBounds = TransformBoundsToLocal(root.transform, renderers[0].bounds);
        for (int i = 1; i < renderers.Length; i++)
            localBounds.Encapsulate(TransformBoundsToLocal(root.transform, renderers[i].bounds));
        BoxCollider collider = root.GetComponent<BoxCollider>();
        if (collider == null) collider = root.AddComponent<BoxCollider>();
        collider.center = localBounds.center;
        collider.size = localBounds.size;
    }

    private static Bounds TransformBoundsToLocal(Transform root, Bounds worldBounds)
    {
        Vector3 min = worldBounds.min, max = worldBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x,min.y,min.z), new Vector3(max.x,min.y,min.z),
            new Vector3(min.x,max.y,min.z), new Vector3(max.x,max.y,min.z),
            new Vector3(min.x,min.y,max.z), new Vector3(max.x,min.y,max.z),
            new Vector3(min.x,max.y,max.z), new Vector3(max.x,max.y,max.z)
        };
        Bounds result = new Bounds(root.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int i = 1; i < corners.Length; i++) result.Encapsulate(root.InverseTransformPoint(corners[i]));
        return result;
    }

    private static void SavePrefab(GameObject root, string prefabPath)
    {
        EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
    }

    private static Transform FindDescendant(Transform root, string exactName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == exactName) return child;
        return null;
    }

    private static string SanitizeName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return value.Replace(' ', '_');
    }

    private static void EnsureFolder(string assetPath)
    {
        string[] parts = assetPath.Replace('\\', '/').Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string ToAbsolutePath(string assetPath) =>
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));

    private sealed class ImportContext
    {
        private readonly GltfRoot gltf;
        private readonly string gltfAssetPath;
        private readonly string assetFolder;
        private readonly byte[][] buffers;
        private readonly Material[] materials;
        private readonly Dictionary<long, Mesh> meshes = new Dictionary<long, Mesh>();
        public readonly List<string> Notes = new List<string>();

        public ImportContext(GltfRoot root, string sourcePath, string folder)
        {
            gltf = root;
            gltfAssetPath = sourcePath;
            assetFolder = folder;
            buffers = LoadBuffers();
            materials = BuildMaterials();
        }

        public GameObject BuildScene(string rootName)
        {
            GameObject root = new GameObject(rootName);
            int sceneIndex = Mathf.Clamp(gltf.scene, 0, Math.Max(0, (gltf.scenes?.Length ?? 1) - 1));
            int[] sceneNodes = gltf.scenes != null && gltf.scenes.Length > 0 ? gltf.scenes[sceneIndex].nodes : null;
            if (sceneNodes == null) sceneNodes = Enumerable.Range(0, gltf.nodes?.Length ?? 0).ToArray();
            foreach (int nodeIndex in sceneNodes) BuildNode(nodeIndex, root.transform);
            return root;
        }

        private void BuildNode(int nodeIndex, Transform parent)
        {
            GltfNode node = gltf.nodes[nodeIndex];
            GameObject go = new GameObject(string.IsNullOrWhiteSpace(node.name) ? "Node_" + nodeIndex : node.name);
            go.transform.SetParent(parent, false);
            ApplyTransform(go.transform, node);

            if (node.mesh >= 0 && node.mesh < (gltf.meshes?.Length ?? 0))
            {
                GltfMesh sourceMesh = gltf.meshes[node.mesh];
                for (int primitiveIndex = 0; primitiveIndex < sourceMesh.primitives.Length; primitiveIndex++)
                {
                    GltfPrimitive primitive = sourceMesh.primitives[primitiveIndex];
                    if (primitive.mode != 0 && primitive.mode != 4)
                    {
                        Notes.Add($"{gltfAssetPath}: skipped unsupported primitive mode {primitive.mode}.");
                        continue;
                    }
                    if (primitive.mode == 0)
                    {
                        Notes.Add($"{gltfAssetPath}: skipped point-cloud primitive; it has no triangle surface.");
                        continue;
                    }

                    GameObject target = sourceMesh.primitives.Length == 1 ? go : new GameObject("Primitive_" + primitiveIndex);
                    if (target != go) target.transform.SetParent(go.transform, false);
                    MeshFilter filter = target.AddComponent<MeshFilter>();
                    MeshRenderer renderer = target.AddComponent<MeshRenderer>();
                    filter.sharedMesh = GetOrCreateMesh(node.mesh, primitiveIndex, primitive);
                    renderer.sharedMaterial = primitive.material >= 0 && primitive.material < materials.Length
                        ? materials[primitive.material]
                        : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
                }
            }

            if (node.children != null)
                foreach (int child in node.children) BuildNode(child, go.transform);
        }

        private Mesh GetOrCreateMesh(int meshIndex, int primitiveIndex, GltfPrimitive primitive)
        {
            long key = ((long)meshIndex << 32) | (uint)primitiveIndex;
            if (meshes.TryGetValue(key, out Mesh existing)) return existing;

            Vector3[] vertices = ReadVector3(primitive.attributes.POSITION, true);
            Vector3[] normals = primitive.attributes.NORMAL >= 0 ? ReadVector3(primitive.attributes.NORMAL, true) : null;
            Vector2[] uv = primitive.attributes.TEXCOORD_0 >= 0 ? ReadVector2(primitive.attributes.TEXCOORD_0) : null;
            Vector4[] tangents = primitive.attributes.TANGENT >= 0 ? ReadVector4(primitive.attributes.TANGENT, true) : null;
            int[] indices = primitive.indices >= 0
                ? ReadIndices(primitive.indices)
                : Enumerable.Range(0, vertices.Length).ToArray();
            for (int i = 0; i + 2 < indices.Length; i += 3)
                (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);

            Mesh mesh = new Mesh { name = $"Mesh_{meshIndex}_{primitiveIndex}" };
            if (vertices.Length > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            if (normals != null && normals.Length == vertices.Length) mesh.normals = normals;
            if (uv != null && uv.Length == vertices.Length) mesh.uv = uv;
            if (tangents != null && tangents.Length == vertices.Length) mesh.tangents = tangents;
            mesh.triangles = indices;
            if (normals == null) mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string meshFolder = assetFolder + "/Generated/Meshes";
            EnsureFolder(meshFolder);
            string meshPath = meshFolder + "/" + mesh.name + ".asset";
            AssetDatabase.CreateAsset(mesh, meshPath);
            meshes[key] = mesh;
            return mesh;
        }

        private byte[][] LoadBuffers()
        {
            if (gltf.buffers == null) return Array.Empty<byte[]>();
            byte[][] result = new byte[gltf.buffers.Length][];
            for (int i = 0; i < result.Length; i++)
            {
                string uri = Uri.UnescapeDataString(gltf.buffers[i].uri ?? string.Empty);
                if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    result[i] = Convert.FromBase64String(uri.Substring(uri.IndexOf(',') + 1));
                else
                    result[i] = File.ReadAllBytes(ToAbsolutePath(assetFolder + "/" + uri.Replace('\\', '/')));
            }
            return result;
        }

        private Material[] BuildMaterials()
        {
            if (gltf.materials == null) return Array.Empty<Material>();
            Material[] result = new Material[gltf.materials.Length];
            string materialFolder = assetFolder + "/Generated/Materials";
            EnsureFolder(materialFolder);
            for (int i = 0; i < result.Length; i++)
            {
                GltfMaterial source = gltf.materials[i];
                GltfSpecGloss specGloss = source.extensions?.KHR_materials_pbrSpecularGlossiness;
                Material material = new Material(Shader.Find(specGloss == null ? "Standard" : "Standard (Specular setup)"))
                {
                    name = string.IsNullOrWhiteSpace(source.name) ? "Material_" + i : SanitizeName(source.name)
                };
                GltfPbr pbr = source.pbrMetallicRoughness;
                if (pbr != null)
                {
                    if (pbr.baseColorFactor != null && pbr.baseColorFactor.Length >= 4)
                        material.color = new Color(pbr.baseColorFactor[0], pbr.baseColorFactor[1], pbr.baseColorFactor[2], pbr.baseColorFactor[3]);
                    AssignTexture(material, "_MainTex", pbr.baseColorTexture);
                    material.SetFloat("_Metallic", pbr.metallicFactor);
                    material.SetFloat("_Glossiness", 1f - pbr.roughnessFactor);
                }
                else if (specGloss != null)
                {
                    if (specGloss.diffuseFactor != null && specGloss.diffuseFactor.Length >= 4)
                        material.color = new Color(specGloss.diffuseFactor[0], specGloss.diffuseFactor[1], specGloss.diffuseFactor[2], specGloss.diffuseFactor[3]);
                    AssignTexture(material, "_MainTex", specGloss.diffuseTexture);
                    if (specGloss.specularFactor != null && specGloss.specularFactor.Length >= 3)
                        material.SetColor("_SpecColor", new Color(specGloss.specularFactor[0], specGloss.specularFactor[1], specGloss.specularFactor[2]));
                    material.SetFloat("_Glossiness", specGloss.glossinessFactor);
                }
                AssignTexture(material, "_BumpMap", source.normalTexture);
                if (material.GetTexture("_BumpMap") != null) material.EnableKeyword("_NORMALMAP");
                if (source.emissiveFactor != null && source.emissiveFactor.Length >= 3)
                {
                    material.SetColor("_EmissionColor", new Color(source.emissiveFactor[0], source.emissiveFactor[1], source.emissiveFactor[2]));
                    material.EnableKeyword("_EMISSION");
                }
                AssignTexture(material, "_EmissionMap", source.emissiveTexture);
                ConfigureAlpha(material, source.alphaMode, source.alphaCutoff);
                if (source.doubleSided && material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);

                string path = materialFolder + "/" + i.ToString("D3") + "_" + material.name + ".mat";
                AssetDatabase.CreateAsset(material, path);
                result[i] = material;
            }
            return result;
        }

        private void AssignTexture(Material material, string property, GltfTextureInfo textureInfo)
        {
            if (textureInfo == null || textureInfo.index < 0 || textureInfo.index >= (gltf.textures?.Length ?? 0)) return;
            int imageIndex = gltf.textures[textureInfo.index].source;
            if (imageIndex < 0 || imageIndex >= (gltf.images?.Length ?? 0)) return;
            string uri = Uri.UnescapeDataString(gltf.images[imageIndex].uri ?? string.Empty).Replace('\\', '/');
            if (string.IsNullOrEmpty(uri) || uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;
            string texturePath = assetFolder + "/" + uri;
            if (property == "_BumpMap" && AssetImporter.GetAtPath(texturePath) is TextureImporter importer &&
                importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null) return;
            material.SetTexture(property, texture);
            GltfTextureTransform transform = textureInfo.extensions?.KHR_texture_transform;
            if (transform?.scale != null && transform.scale.Length >= 2)
                material.SetTextureScale(property, new Vector2(transform.scale[0], transform.scale[1]));
            if (transform?.offset != null && transform.offset.Length >= 2)
                material.SetTextureOffset(property, new Vector2(transform.offset[0], transform.offset[1]));
        }

        private static void ConfigureAlpha(Material material, string alphaMode, float cutoff)
        {
            if (alphaMode == "MASK")
            {
                material.SetFloat("_Mode", 1f);
                material.SetFloat("_Cutoff", cutoff <= 0f ? 0.5f : cutoff);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (alphaMode == "BLEND")
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
        }

        private Vector3[] ReadVector3(int accessorIndex, bool flipX)
        {
            float[][] values = ReadFloatAccessor(accessorIndex, 3);
            Vector3[] result = new Vector3[values.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = new Vector3(flipX ? -values[i][0] : values[i][0], values[i][1], values[i][2]);
            return result;
        }

        private Vector2[] ReadVector2(int accessorIndex)
        {
            float[][] values = ReadFloatAccessor(accessorIndex, 2);
            Vector2[] result = new Vector2[values.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector2(values[i][0], values[i][1]);
            return result;
        }

        private Vector4[] ReadVector4(int accessorIndex, bool convertTangent)
        {
            float[][] values = ReadFloatAccessor(accessorIndex, 4);
            Vector4[] result = new Vector4[values.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = new Vector4(convertTangent ? -values[i][0] : values[i][0], values[i][1], values[i][2], convertTangent ? -values[i][3] : values[i][3]);
            return result;
        }

        private float[][] ReadFloatAccessor(int accessorIndex, int expectedComponents)
        {
            GltfAccessor accessor = gltf.accessors[accessorIndex];
            int components = ComponentCount(accessor.type);
            if (components < expectedComponents) throw new InvalidDataException("Accessor has too few components: " + accessorIndex);
            GltfBufferView view = gltf.bufferViews[accessor.bufferView];
            byte[] data = buffers[view.buffer];
            int componentSize = ComponentSize(accessor.componentType);
            int stride = view.byteStride > 0 ? view.byteStride : components * componentSize;
            int start = view.byteOffset + accessor.byteOffset;
            float[][] result = new float[accessor.count][];
            for (int i = 0; i < accessor.count; i++)
            {
                result[i] = new float[expectedComponents];
                for (int c = 0; c < expectedComponents; c++)
                    result[i][c] = ReadComponentAsFloat(data, start + i * stride + c * componentSize, accessor.componentType, accessor.normalized);
            }
            return result;
        }

        private int[] ReadIndices(int accessorIndex)
        {
            GltfAccessor accessor = gltf.accessors[accessorIndex];
            GltfBufferView view = gltf.bufferViews[accessor.bufferView];
            byte[] data = buffers[view.buffer];
            int componentSize = ComponentSize(accessor.componentType);
            int stride = view.byteStride > 0 ? view.byteStride : componentSize;
            int start = view.byteOffset + accessor.byteOffset;
            int[] result = new int[accessor.count];
            for (int i = 0; i < result.Length; i++)
            {
                int offset = start + i * stride;
                result[i] = accessor.componentType switch
                {
                    5121 => data[offset],
                    5123 => BitConverter.ToUInt16(data, offset),
                    5125 => checked((int)BitConverter.ToUInt32(data, offset)),
                    _ => throw new InvalidDataException("Unsupported index component type " + accessor.componentType)
                };
            }
            return result;
        }

        private static float ReadComponentAsFloat(byte[] data, int offset, int componentType, bool normalized)
        {
            return componentType switch
            {
                5120 => normalized ? Math.Max((sbyte)data[offset] / 127f, -1f) : (sbyte)data[offset],
                5121 => normalized ? data[offset] / 255f : data[offset],
                5122 => normalized ? Math.Max(BitConverter.ToInt16(data, offset) / 32767f, -1f) : BitConverter.ToInt16(data, offset),
                5123 => normalized ? BitConverter.ToUInt16(data, offset) / 65535f : BitConverter.ToUInt16(data, offset),
                5125 => BitConverter.ToUInt32(data, offset),
                5126 => BitConverter.ToSingle(data, offset),
                _ => throw new InvalidDataException("Unsupported accessor component type " + componentType)
            };
        }

        private static int ComponentSize(int componentType) => componentType switch
        {
            5120 or 5121 => 1,
            5122 or 5123 => 2,
            5125 or 5126 => 4,
            _ => throw new InvalidDataException("Unsupported component type " + componentType)
        };

        private static int ComponentCount(string type) => type switch
        {
            "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4,
            "MAT2" => 4, "MAT3" => 9, "MAT4" => 16,
            _ => throw new InvalidDataException("Unsupported accessor type " + type)
        };

        private static void ApplyTransform(Transform target, GltfNode node)
        {
            if (node.matrix != null && node.matrix.Length == 16)
            {
                Matrix4x4 matrix = new Matrix4x4();
                for (int column = 0; column < 4; column++)
                    for (int row = 0; row < 4; row++)
                        matrix[row, column] = node.matrix[column * 4 + row];
                Matrix4x4 mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
                Decompose(mirror * matrix * mirror, target);
                return;
            }

            if (node.translation != null && node.translation.Length >= 3)
                target.localPosition = new Vector3(-node.translation[0], node.translation[1], node.translation[2]);
            if (node.rotation != null && node.rotation.Length >= 4)
                target.localRotation = new Quaternion(node.rotation[0], -node.rotation[1], -node.rotation[2], node.rotation[3]);
            if (node.scale != null && node.scale.Length >= 3)
                target.localScale = new Vector3(node.scale[0], node.scale[1], node.scale[2]);
        }

        private static void Decompose(Matrix4x4 matrix, Transform target)
        {
            Vector3 right = matrix.GetColumn(0), up = matrix.GetColumn(1), forward = matrix.GetColumn(2);
            Vector3 scale = new Vector3(right.magnitude, up.magnitude, forward.magnitude);
            if (Vector3.Dot(Vector3.Cross(right, up), forward) < 0f)
            {
                scale.x = -scale.x;
                right = -right;
            }
            target.localPosition = matrix.GetColumn(3);
            target.localScale = scale;
            if (scale.y > 0.000001f && scale.z > 0.000001f)
                target.localRotation = Quaternion.LookRotation(forward / scale.z, up / scale.y);
        }
    }

    [Serializable] private sealed class GltfRoot
    {
        public int scene;
        public GltfScene[] scenes;
        public GltfNode[] nodes;
        public GltfMesh[] meshes;
        public GltfAccessor[] accessors;
        public GltfBufferView[] bufferViews;
        public GltfBuffer[] buffers;
        public GltfMaterial[] materials;
        public GltfTexture[] textures;
        public GltfImage[] images;
    }
    [Serializable] private sealed class GltfScene { public int[] nodes; }
    [Serializable] private sealed class GltfNode
    {
        public string name;
        public int mesh = -1;
        public int[] children;
        public float[] matrix, translation, rotation, scale;
    }
    [Serializable] private sealed class GltfMesh { public string name; public GltfPrimitive[] primitives; }
    [Serializable] private sealed class GltfPrimitive
    {
        public GltfAttributes attributes;
        public int indices = -1;
        public int material = -1;
        public int mode = 4;
    }
    [Serializable] private sealed class GltfAttributes
    {
        public int POSITION = -1, NORMAL = -1, TANGENT = -1, TEXCOORD_0 = -1;
    }
    [Serializable] private sealed class GltfAccessor
    {
        public int bufferView = -1, byteOffset, componentType, count;
        public string type;
        public bool normalized;
    }
    [Serializable] private sealed class GltfBufferView
    {
        public int buffer, byteOffset, byteLength, byteStride;
    }
    [Serializable] private sealed class GltfBuffer { public string uri; public int byteLength; }
    [Serializable] private sealed class GltfMaterial
    {
        public string name, alphaMode = "OPAQUE";
        public float alphaCutoff = 0.5f;
        public bool doubleSided;
        public float[] emissiveFactor;
        public GltfPbr pbrMetallicRoughness;
        public GltfTextureInfo normalTexture, emissiveTexture, occlusionTexture;
        public GltfMaterialExtensions extensions;
    }
    [Serializable] private sealed class GltfPbr
    {
        public float[] baseColorFactor;
        public float metallicFactor = 1f, roughnessFactor = 1f;
        public GltfTextureInfo baseColorTexture, metallicRoughnessTexture;
    }
    [Serializable] private sealed class GltfTextureInfo
    {
        public int index = -1;
        public int texCoord;
        public GltfTextureInfoExtensions extensions;
    }
    [Serializable] private sealed class GltfMaterialExtensions
    {
        public GltfSpecGloss KHR_materials_pbrSpecularGlossiness;
    }
    [Serializable] private sealed class GltfSpecGloss
    {
        public float[] diffuseFactor, specularFactor;
        public float glossinessFactor = 1f;
        public GltfTextureInfo diffuseTexture, specularGlossinessTexture;
    }
    [Serializable] private sealed class GltfTextureInfoExtensions
    {
        public GltfTextureTransform KHR_texture_transform;
    }
    [Serializable] private sealed class GltfTextureTransform
    {
        public float[] offset, scale;
        public float rotation;
        public int texCoord;
    }
    [Serializable] private sealed class GltfTexture { public int source = -1, sampler = -1; }
    [Serializable] private sealed class GltfImage { public string uri, mimeType; public int bufferView = -1; }
}
