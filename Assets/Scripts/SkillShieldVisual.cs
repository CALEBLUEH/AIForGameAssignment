using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillShieldVisual : MonoBehaviour
{
    private float expiresAt;

    public static void Show(CombatUnit owner, float radius, float duration, Material material)
    {
        if (owner == null) return;
        SkillShieldVisual existing = owner.GetComponentInChildren<SkillShieldVisual>(true);
        GameObject sphere;
        if (existing == null)
        {
            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Skill Shield Visual";
            sphere.transform.SetParent(owner.transform, false);
            Collider collider = sphere.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            existing = sphere.AddComponent<SkillShieldVisual>();
        }
        else sphere = existing.gameObject;

        Collider body = owner.GetComponent<Collider>();
        sphere.transform.localPosition = body == null
            ? Vector3.zero
            : owner.transform.InverseTransformPoint(body.bounds.center);
        sphere.transform.localRotation = Quaternion.identity;
        sphere.transform.localScale = Vector3.one * radius * 2f;
        Renderer renderer = sphere.GetComponent<Renderer>();
        if (material != null) renderer.sharedMaterial = material;
        else
        {
            Shader shader = Shader.Find("AIFG/Skill Shield");
            if (shader != null) renderer.material = new Material(shader);
        }
        existing.expiresAt = Time.time + Mathf.Max(0.1f, duration);
        sphere.SetActive(true);
    }

    private void Update()
    {
        if (Time.time >= expiresAt) Destroy(gameObject);
    }
}
