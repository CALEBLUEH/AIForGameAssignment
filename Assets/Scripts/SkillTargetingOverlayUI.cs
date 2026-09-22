using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SkillTargetingOverlayUI : MonoBehaviour
{
    [SerializeField] private Image dimmer;
    [SerializeField] private Text prompt;
    private Material runtimeDimmerMaterial;
    private static readonly int[] HoleIds =
    {
        Shader.PropertyToID("_Hole0"), Shader.PropertyToID("_Hole1"),
        Shader.PropertyToID("_Hole2"), Shader.PropertyToID("_Hole3"),
        Shader.PropertyToID("_Hole4"), Shader.PropertyToID("_Hole5"),
        Shader.PropertyToID("_Hole6"), Shader.PropertyToID("_Hole7")
    };

    public void Configure(Image configuredDimmer, Text configuredPrompt)
    {
        dimmer = configuredDimmer;
        prompt = configuredPrompt;
    }

    public void Show(string title, string description)
    {
        EnsureDimmerMaterial();
        ClearHighlightHoles();
        gameObject.SetActive(true);
        if (dimmer != null) dimmer.gameObject.SetActive(true);
        if (prompt != null)
        {
            prompt.gameObject.SetActive(true);
            prompt.text = title + "\n" + description + "\nDrag to aim • Release to cast • Tap the same card or press Esc to cancel";
        }
    }

    public void Hide()
    {
        ClearHighlightHoles();
        if (dimmer != null) dimmer.gameObject.SetActive(false);
        if (prompt != null) prompt.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    public void SetHighlightHoles(IEnumerable<CombatUnit> units, Camera worldCamera)
    {
        EnsureDimmerMaterial();
        if (runtimeDimmerMaterial == null || worldCamera == null) return;
        ClearHighlightHoles();
        if (units == null) return;

        int index = 0;
        foreach (CombatUnit unit in units)
        {
            if (unit == null || unit.IsDead || index >= HoleIds.Length) continue;
            if (!TryGetViewportBounds(unit, worldCamera, out Vector4 hole)) continue;
            runtimeDimmerMaterial.SetVector(HoleIds[index++], hole);
        }
    }

    private void EnsureDimmerMaterial()
    {
        if (dimmer == null || runtimeDimmerMaterial != null) return;
        Shader shader = Shader.Find("AIFG/Targeting Dimmer Cutout");
        if (shader == null) return;
        runtimeDimmerMaterial = new Material(shader)
        {
            name = "Runtime Targeting Dimmer Cutout",
            hideFlags = HideFlags.DontSave
        };
        runtimeDimmerMaterial.color = dimmer.color;
        dimmer.color = Color.white;
        dimmer.material = runtimeDimmerMaterial;
    }

    private void ClearHighlightHoles()
    {
        if (runtimeDimmerMaterial == null) return;
        foreach (int id in HoleIds) runtimeDimmerMaterial.SetVector(id, Vector4.zero);
    }

    private static bool TryGetViewportBounds(CombatUnit unit, Camera camera, out Vector4 hole)
    {
        Renderer[] renderers = unit.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer.name == "Selected Attack Radius") continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!found) { hole = default; return false; }

        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Vector2 minimum = Vector2.one * float.PositiveInfinity;
        Vector2 maximum = Vector2.one * float.NegativeInfinity;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 viewport = camera.WorldToViewportPoint(center + Vector3.Scale(extents, new Vector3(x, y, z)));
            if (viewport.z <= 0f) continue;
            minimum = Vector2.Min(minimum, viewport);
            maximum = Vector2.Max(maximum, viewport);
        }
        if (float.IsInfinity(minimum.x)) { hole = default; return false; }
        Vector2 halfSize = (maximum - minimum) * 0.58f + new Vector2(0.018f, 0.028f);
        hole = new Vector4((minimum.x + maximum.x) * 0.5f, (minimum.y + maximum.y) * 0.5f,
            Mathf.Max(0.025f, halfSize.x), Mathf.Max(0.04f, halfSize.y));
        return true;
    }

    private void OnDestroy()
    {
        if (runtimeDimmerMaterial != null) Destroy(runtimeDimmerMaterial);
    }
}
