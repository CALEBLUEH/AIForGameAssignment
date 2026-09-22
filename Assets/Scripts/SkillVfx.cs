using System;
using UnityEngine;

public static class SkillVfx
{
    public static float LaunchProjectile(Vector3 start, Transform homingTarget, Vector3 targetPoint,
        float speed, float maxRange, Color color, bool whiteCore, Action onImpact = null,
        float projectileSize = 0.32f)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "Skill Projectile";
        UnityEngine.Object.Destroy(projectile.GetComponent<Collider>());
        projectile.transform.position = start;
        projectile.transform.localScale = Vector3.one * Mathf.Max(0.05f, projectileSize);
        Material material = CreateGlowMaterial(color);
        projectile.GetComponent<Renderer>().sharedMaterial = material;

        if (whiteCore)
        {
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "White Core";
            UnityEngine.Object.Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(projectile.transform, false);
            core.transform.localScale = Vector3.one * 0.45f;
            core.GetComponent<Renderer>().sharedMaterial = CreateGlowMaterial(Color.white);
        }

        TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
        trail.time = 0.28f;
        trail.startWidth = 0.16f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.04f;
        trail.startColor = new Color(color.r, color.g, color.b, 0.95f);
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.sharedMaterial = material;

        SkillProjectileMotion motion = projectile.AddComponent<SkillProjectileMotion>();
        motion.Configure(homingTarget, targetPoint, speed, maxRange, color, onImpact);
        float distance = Vector3.Distance(start, homingTarget == null ? targetPoint : homingTarget.position);
        return Mathf.Min(maxRange, distance) / Mathf.Max(0.1f, speed);
    }

    public static float DropModel(GameObject prefab, Vector3 target, Vector3 startOffset,
        float duration, float scale, bool destroyOnLand, Action<GameObject> onLanded)
    {
        GameObject instance = prefab == null
            ? GameObject.CreatePrimitive(PrimitiveType.Cube)
            : UnityEngine.Object.Instantiate(prefab);
        instance.name = prefab == null ? "Skill Drop Placeholder" : prefab.name + " Drop";
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        instance.transform.position = target + startOffset;
        instance.transform.localScale *= Mathf.Max(0.01f, scale);
        SkillDropMotion motion = instance.AddComponent<SkillDropMotion>();
        motion.Configure(target, duration, destroyOnLand, onLanded);
        return Mathf.Max(0.05f, duration);
    }

    public static void SpawnBurst(Vector3 position, Color color, float size = 0.45f,
        int count = 28, float duration = 0.8f, float effectRadius = 0.2f)
    {
        GameObject effect = new GameObject("Skill Impact Particles");
        effect.transform.position = position;
        ParticleSystem particles = effect.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(duration * 0.45f, duration);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.35f, size);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, short.MaxValue)) });
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.01f, effectRadius);
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = CreateParticleMaterial(color);
        particles.Play();
    }

    public static void SpawnAura(Transform target, Color color, float duration, float radius = 0.75f)
    {
        if (target == null) return;
        GameObject effect = new GameObject("Timed Skill Aura");
        effect.transform.SetParent(target, false);
        Collider body = target.GetComponent<Collider>();
        effect.transform.localPosition = body == null
            ? Vector3.zero
            : target.InverseTransformPoint(body.bounds.center);
        ParticleSystem particles = effect.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.duration = duration;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = color;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var emission = particles.emission;
        emission.rateOverTime = 18f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = CreateParticleMaterial(color);
        particles.Play();
        SkillTimedDestroy destroy = effect.AddComponent<SkillTimedDestroy>();
        destroy.Configure(duration);
    }

    private static Material CreateGlowMaterial(Color color)
    {
        Shader shader = Shader.Find("AIFG/Skill Glow");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader) { name = "Runtime Skill Glow", hideFlags = HideFlags.DontSave };
        material.color = color;
        return material;
    }

    private static Material CreateParticleMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader) { name = "Runtime Skill Particle", hideFlags = HideFlags.DontSave };
        material.color = color;
        return material;
    }
}

public sealed class SkillProjectileMotion : MonoBehaviour
{
    private Transform target;
    private Vector3 targetPoint;
    private float speed, maxDistance, travelled;
    private Color impactColor;
    private Action onImpact;
    private bool completed;

    public void Configure(Transform homingTarget, Vector3 point, float configuredSpeed,
        float configuredMaxDistance, Color color, Action impact)
    {
        target = homingTarget;
        targetPoint = point;
        speed = Mathf.Max(0.1f, configuredSpeed);
        maxDistance = Mathf.Max(0.1f, configuredMaxDistance);
        impactColor = color;
        onImpact = impact;
    }

    private void Update()
    {
        Vector3 destination = target == null ? targetPoint : target.position;
        Vector3 delta = destination - transform.position;
        float step = speed * Time.deltaTime;
        if (delta.magnitude <= step || travelled + step >= maxDistance)
        {
            transform.position = delta.magnitude <= step ? destination : transform.position + delta.normalized * step;
            Complete();
            return;
        }
        transform.position += delta.normalized * step;
        transform.rotation = Quaternion.LookRotation(delta.normalized);
        travelled += step;
    }

    private void Complete()
    {
        if (completed) return;
        completed = true;
        SkillVfx.SpawnBurst(transform.position, impactColor, 0.35f, 22, 0.65f);
        onImpact?.Invoke();
        Destroy(gameObject);
    }
}

public sealed class SkillDropMotion : MonoBehaviour
{
    private Vector3 start, target;
    private float duration, elapsed;
    private bool destroyOnLand;
    private Action<GameObject> onLanded;

    public void Configure(Vector3 targetPosition, float configuredDuration, bool destroyAtLanding,
        Action<GameObject> landed)
    {
        start = transform.position;
        target = targetPosition;
        duration = Mathf.Max(0.05f, configuredDuration);
        destroyOnLand = destroyAtLanding;
        onLanded = landed;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = 1f - (1f - t) * (1f - t);
        transform.position = Vector3.Lerp(start, target, eased);
        transform.Rotate(Vector3.up, 180f * Time.deltaTime, Space.World);
        if (t < 1f) return;
        onLanded?.Invoke(gameObject);
        onLanded = null;
        if (destroyOnLand) Destroy(gameObject);
        else Destroy(this);
    }
}

public sealed class SkillTimedDestroy : MonoBehaviour
{
    private float destroyAt;
    public void Configure(float duration) => destroyAt = Time.time + Mathf.Max(0.05f, duration);
    private void Update() { if (Time.time >= destroyAt) Destroy(gameObject); }
}
