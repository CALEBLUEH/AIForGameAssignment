using System.Collections.Generic;
using UnityEngine;

public sealed class SkillTargetHighlighter
{
    private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
    private readonly Material outlineMaterial;

    public SkillTargetHighlighter(Material configuredMaterial)
    {
        outlineMaterial = configuredMaterial;
        if (outlineMaterial == null)
        {
            Shader shader = Shader.Find("AIFG/Skill Target Outline");
            if (shader != null) outlineMaterial = new Material(shader) { name = "Runtime Skill Target Outline" };
        }
    }

    public void Set(IEnumerable<CombatUnit> units)
    {
        Clear();
        if (outlineMaterial == null || units == null) return;
        var unique = new HashSet<CombatUnit>();
        foreach (CombatUnit unit in units)
        {
            if (unit == null || unit.IsDead || !unique.Add(unit)) continue;
            foreach (Renderer renderer in unit.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer.name == "Selected Attack Radius") continue;
                Material[] original = renderer.sharedMaterials;
                if (originals.ContainsKey(renderer)) continue;
                originals.Add(renderer, original);
                var highlighted = new Material[original.Length + 1];
                original.CopyTo(highlighted, 0);
                highlighted[highlighted.Length - 1] = outlineMaterial;
                renderer.sharedMaterials = highlighted;
            }
        }
    }

    public void Clear()
    {
        foreach (KeyValuePair<Renderer, Material[]> pair in originals)
            if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        originals.Clear();
    }
}
