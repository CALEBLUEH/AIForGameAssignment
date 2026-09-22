using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

public static class SandboxCoverCinematicSetup
{
    private const string ScenePath = "Assets/Scenes/Sandbox_Gameplay.unity";
    private const string ConcreteFencePrefab = "Assets/Prefabs/Environment/Obstacle/ConcreteFence.prefab";
    private const string SkillBarrierPrefab = "Assets/Prefabs/Skills/Models/SkillRoadBarrier.prefab";
    private const string WipeMaterialPath = "Assets/Materials/SkillVideoWipe.mat";
    private const string HealthHudName = "Obstacle Health HUD";

    [MenuItem("Tools/AI For Game/Build Cover Health and Skill Cinematics")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        Material wipeMaterial = EnsureMaterial(WipeMaterialPath, "AIFG/Skill Video Wipe");
        ConfigureObstaclePrefab(ConcreteFencePrefab, true);
        ConfigureObstaclePrefab(SkillBarrierPrefab, false);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RectSnapshot[] existingRects = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
            .Where(rect => rect.name != HealthHudName && rect.GetComponentInParent<CoverHealthHUD>() == null &&
                rect.GetComponentInParent<SkillCinematicPlayer>() == null)
            .Select(rect => new RectSnapshot(rect)).ToArray();

        var configuredObjects = new HashSet<GameObject>();
        foreach (BlockingObstacle blocker in FindAll<BlockingObstacle>(scene))
        {
            Collider collider = blocker.GetComponent<Collider>() ?? blocker.GetComponentInChildren<Collider>(true) ??
                blocker.GetComponentInParent<Collider>();
            if (collider == null || !configuredObjects.Add(collider.gameObject)) continue;
            ConfigureSceneObstacle(collider.gameObject, collider);
        }
        foreach (DestructibleCover cover in FindAll<DestructibleCover>(scene))
        {
            Collider collider = cover.GetComponent<Collider>() ?? cover.GetComponentInChildren<Collider>(true);
            if (collider == null || !configuredObjects.Add(cover.gameObject)) continue;
            ConfigureSceneObstacle(cover.gameObject, collider);
        }

        ConfigureCinematicOverlay(scene, wipeMaterial);
        foreach (RectSnapshot snapshot in existingRects) snapshot.VerifyUnchanged();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Sandbox_Gameplay.");
        AssetDatabase.SaveAssets();
        Debug.Log($"SANDBOX_COVER_CINEMATIC_SETUP_OK obstacles={configuredObjects.Count}, capacity=1, health HUDs and five skill videos wired without moving existing UI.");
    }

    private static void ConfigureSceneObstacle(GameObject obstacle, Collider collider)
    {
        TacticalCoverObstacle tactical = GetOrAdd<TacticalCoverObstacle>(obstacle);
        tactical.ConfigureCapacity(1);
        DestructibleCover health = GetOrAdd<DestructibleCover>(obstacle);
        if (health.MaxHealth <= 1f) health.Configure(180f);
        BlockingObstacle blocker = GetOrAdd<BlockingObstacle>(obstacle);
        blocker.blocksLineOfSight = true;

        CoverPoint[] points = obstacle.GetComponentsInChildren<CoverPoint>(true);
        if (points.Length == 0) points = CreateCoverPoints(obstacle, collider, tactical);
        foreach (CoverPoint point in points) point.Configure(collider, tactical, 0.4f, 0.65f);
        EnsureHealthHud(obstacle, health, collider.bounds);
        EditorUtility.SetDirty(tactical);
        EditorUtility.SetDirty(health);
        EditorUtility.SetDirty(blocker);
    }

    private static CoverPoint[] CreateCoverPoints(GameObject obstacle, Collider collider, TacticalCoverObstacle tactical)
    {
        GameObject root = new GameObject("Tactical Cover Points (Generated)");
        root.transform.SetParent(obstacle.transform, false);
        var points = new List<CoverPoint>();
        Vector3[] directions =
        {
            obstacle.transform.right, -obstacle.transform.right,
            obstacle.transform.forward, -obstacle.transform.forward
        };
        foreach (Vector3 rawDirection in directions)
        {
            Vector3 direction = Vector3.ProjectOnPlane(rawDirection, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.01f) continue;
            Ray ray = new Ray(collider.bounds.center + direction * 100f, -direction);
            if (!collider.Raycast(ray, out RaycastHit hit, 200f)) continue;
            Vector3 intended = hit.point + direction * 1.1f;
            if (!NavMesh.SamplePosition(intended, out NavMeshHit navHit, 3f, NavMesh.AllAreas)) continue;
            if (points.Any(point => FlatDistance(point.transform.position, navHit.position) < 0.8f)) continue;
            GameObject pointObject = new GameObject("Cover Point " + (points.Count + 1));
            pointObject.transform.SetParent(root.transform, true);
            pointObject.transform.position = navHit.position;
            CoverPoint point = pointObject.AddComponent<CoverPoint>();
            point.Configure(collider, tactical, 0.4f, 0.65f);
            points.Add(point);
        }
        if (points.Count == 0) throw new InvalidOperationException(obstacle.name + " has no reachable NavMesh cover position.");
        return points.ToArray();
    }

    private static void ConfigureObstaclePrefab(string path, bool ensureCollider)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Collider collider = root.GetComponent<Collider>() ?? root.GetComponentInChildren<Collider>(true);
            if (collider == null && ensureCollider)
            {
                Bounds bounds = RendererBounds(root);
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.center = root.transform.InverseTransformPoint(bounds.center);
                box.size = bounds.size;
                collider = box;
            }
            TacticalCoverObstacle tactical = GetOrAdd<TacticalCoverObstacle>(root);
            tactical.ConfigureCapacity(1);
            DestructibleCover health = GetOrAdd<DestructibleCover>(root);
            health.Configure(180f);
            GetOrAdd<BlockingObstacle>(root).blocksLineOfSight = true;
            EnsureHealthHud(root, health, collider == null ? RendererBounds(root) : collider.bounds);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void EnsureHealthHud(GameObject obstacle, DestructibleCover cover, Bounds bounds)
    {
        Transform existing = obstacle.transform.Find(HealthHudName);
        GameObject hudObject = existing == null
            ? new GameObject(HealthHudName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CoverHealthHUD))
            : existing.gameObject;
        hudObject.transform.SetParent(obstacle.transform, true);
        hudObject.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.65f, bounds.center.z);
        hudObject.transform.rotation = Quaternion.identity;
        hudObject.transform.localScale = Vector3.one * 0.01f;
        RectTransform hudRect = hudObject.GetComponent<RectTransform>();
        hudRect.sizeDelta = new Vector2(170f, 22f);
        Canvas canvas = hudObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 25;
        CanvasScaler scaler = hudObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        Slider slider = hudObject.GetComponentInChildren<Slider>(true);
        if (slider == null)
        {
            GameObject sliderObject = new GameObject("Health", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(hudObject.transform, false);
            Stretch(sliderObject.GetComponent<RectTransform>());
            slider = sliderObject.GetComponent<Slider>();
        }
        Image background = EnsureImage(slider.transform, "Background", new Color(0.03f, 0.05f, 0.08f, 0.9f));
        Stretch(background.rectTransform);
        Image fill = EnsureImage(slider.transform, "Fill", new Color(0.25f, 0.78f, 1f, 1f));
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        slider.fillRect = fillRect;
        slider.targetGraphic = fill;
        slider.direction = Slider.Direction.LeftToRight;
        hudObject.GetComponent<CoverHealthHUD>().Configure(cover, slider, fill);
    }

    private static void ConfigureCinematicOverlay(Scene scene, Material wipeMaterial)
    {
        GameObject overlay = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Skill Cinematic Overlay");
        if (overlay == null)
            overlay = new GameObject("Skill Cinematic Overlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(SkillCinematicPlayer), typeof(VideoPlayer));
        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Transform imageTransform = overlay.transform.Find("Skill Video");
        GameObject imageObject = imageTransform == null
            ? new GameObject("Skill Video", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage))
            : imageTransform.gameObject;
        imageObject.transform.SetParent(overlay.transform, false);
        RawImage image = imageObject.GetComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;
        Stretch(image.rectTransform);

        VideoPlayer player = overlay.GetComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = false;
        player.renderMode = VideoRenderMode.APIOnly;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        SerializedObject component = new SerializedObject(overlay.GetComponent<SkillCinematicPlayer>());
        Assign(component, "videoImage", image);
        Assign(component, "videoPlayer", player);
        Assign(component, "wipeMaterial", wipeMaterial);
        Assign(component, "yuukaClip", Clip("Yuuka"));
        Assign(component, "ayaneClip", Clip("Ayane"));
        Assign(component, "mikaClip", Clip("Mika"));
        Assign(component, "momoiClip", Clip("Momoi"));
        Assign(component, "hinaClip", Clip("Hina"));
        component.ApplyModifiedPropertiesWithoutUndo();
        overlay.SetActive(true);
    }

    private static VideoClip Clip(string character)
    {
        string path = "Assets/Video/SkillAnimations/" + character + "SkillAnimation.mp4";
        VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
        if (clip == null) throw new InvalidOperationException("Missing imported skill video: " + path);
        return clip;
    }

    private static Material EnsureMaterial(string path, string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Missing shader " + shaderName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = "SkillVideoWipe" };
            AssetDatabase.CreateAsset(material, path);
        }
        else material.shader = shader;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Image EnsureImage(Transform parent, string name, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject item = existing == null
            ? new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
            : existing.gameObject;
        item.transform.SetParent(parent, false);
        Image image = item.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static T GetOrAdd<T>(GameObject owner) where T : Component =>
        owner.GetComponent<T>() ?? owner.AddComponent<T>();

    private static void Assign(SerializedObject owner, string field, Object value)
    {
        SerializedProperty property = owner.FindProperty(field);
        if (property == null) throw new MissingFieldException(owner.targetObject.GetType().Name, field);
        property.objectReferenceValue = value;
    }

    private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static Bounds RendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
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
            localScale = value.localScale;
            localRotation = value.localRotation;
        }

        public void VerifyUnchanged()
        {
            if (rect == null || rect.anchorMin != anchorMin || rect.anchorMax != anchorMax || rect.pivot != pivot ||
                rect.anchoredPosition != anchoredPosition || rect.sizeDelta != sizeDelta ||
                rect.localPosition != localPosition || rect.localScale != localScale || rect.localRotation != localRotation)
                throw new InvalidOperationException("Existing UI transform changed: " + (rect == null ? "missing" : rect.name));
        }
    }
}
