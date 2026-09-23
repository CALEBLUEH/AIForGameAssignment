using System;
using System.Collections.Generic;
using UnityEngine;

public class DestructibleCover : MonoBehaviour
{
    private static readonly HashSet<DestructibleCover> All = new HashSet<DestructibleCover>();
    public event Action<DestructibleCover> HealthChanged;
    [Header("Cover Health")]
    [SerializeField] private float maxHealth = 150f;
    [SerializeField] private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDestroyed { get; private set; }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    public void Configure(float configuredMaxHealth)
    {
        maxHealth = Mathf.Max(1f, configuredMaxHealth);
        currentHealth = maxHealth;
        HealthChanged?.Invoke(this);
    }

    private void Awake()
    {
        currentHealth = Mathf.Max(1f, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        AbsorbDamage(amount);
    }

    public float AbsorbDamage(float amount)
    {
        if (amount <= 0f) return 0f;
        if (IsDestroyed) return amount;

        float absorbed = Mathf.Min(currentHealth, amount);
        currentHealth -= absorbed;
        HealthChanged?.Invoke(this);
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            HealthChanged?.Invoke(this);
            BreakCover();
        }
        return Mathf.Max(0f, amount - absorbed);
    }

    public bool IntersectsRadius(Vector3 center, float radius)
    {
        Collider physical = GetComponent<Collider>() ?? GetComponentInChildren<Collider>(true);
        Vector3 closest = physical == null ? transform.position : physical.ClosestPoint(center);
        closest.y = center.y;
        Vector3 flat = closest - center;
        flat.y = 0f;
        return flat.sqrMagnitude <= radius * radius;
    }

    public bool IntersectsCone(Vector3 origin, Vector3 direction, float range, float angle)
    {
        Collider physical = GetComponent<Collider>() ?? GetComponentInChildren<Collider>(true);
        Vector3 point = physical == null ? transform.position : physical.bounds.center;
        Vector3 delta = point - origin;
        delta.y = 0f;
        direction.y = 0f;
        if (delta.sqrMagnitude < 0.001f) return true;
        float padding = physical == null ? 0f : Mathf.Max(physical.bounds.extents.x, physical.bounds.extents.z);
        return delta.magnitude <= range + padding && Vector3.Angle(direction, delta) <= angle * 0.5f;
    }

    public static HashSet<DestructibleCover> DamageInRadius(Vector3 center, float radius, float amount)
    {
        var hit = new HashSet<DestructibleCover>();
        foreach (DestructibleCover cover in new List<DestructibleCover>(All))
        {
            if (cover == null || cover.IsDestroyed || !cover.IntersectsRadius(center, radius)) continue;
            cover.TakeDamage(amount);
            hit.Add(cover);
        }
        return hit;
    }

    public static HashSet<DestructibleCover> DamageInCone(
        Vector3 origin, Vector3 direction, float range, float angle, float amount)
    {
        var hit = new HashSet<DestructibleCover>();
        foreach (DestructibleCover cover in new List<DestructibleCover>(All))
        {
            if (cover == null || cover.IsDestroyed || !cover.IntersectsCone(origin, direction, range, angle)) continue;
            cover.TakeDamage(amount);
            hit.Add(cover);
        }
        return hit;
    }

    private void BreakCover()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;

        // Release every character using a CoverPoint belonging to this wall.
        TacticalCoverObstacle owner = GetComponent<TacticalCoverObstacle>();
        foreach (CoverPoint point in CoverPoint.All.ToArray())
        {
            if (point != null && (point.Obstacle == owner || point.GetComponentInParent<DestructibleCover>() == this) &&
                point.Occupant != null)
                point.Release(point.Occupant);
        }

        Destroy(gameObject);
    }
}
