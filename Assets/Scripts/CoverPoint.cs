using System.Collections.Generic;
using UnityEngine;

// TYPE 1: usable "hide and fight" cover.
// Put this on/near a cover position and assign the collider of the physical cover object.
public class CoverPoint : MonoBehaviour
{
    public static readonly List<CoverPoint> All = new List<CoverPoint>();

    [Header("Usable Cover")]
    [Tooltip("The physical collider that must be between the enemy and this cover point.")]
    public Collider coverCollider;
    [Range(0f, 0.95f)] public float protection = 0.35f;
    public float occupancyRadius = 0.45f;
    public float coverCheckHeight = 1.0f;
    [SerializeField] private TacticalCoverObstacle obstacle;

    public bool IsAvailable => occupant == null;
    public bool IsAbandoned => obstacle != null && obstacle.HasAnyAbandonedTeam;
    public CombatUnit Occupant => occupant;
    public bool IsReservedBy(CombatUnit unit) => occupant == unit;
    private CombatUnit occupant;

    private void Awake()
    {
        if (obstacle == null) obstacle = GetComponentInParent<TacticalCoverObstacle>();
    }

    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() { All.Remove(this); Release(occupant); }

    public bool TryReserve(CombatUnit unit)
    {
        if (!CanBeUsedBy(unit)) return false;
        occupant = unit;
        return true;
    }

    public bool CanBeUsedBy(CombatUnit unit) => unit != null &&
        (occupant == null || occupant == unit) &&
        (obstacle == null || !obstacle.IsAbandonedFor(unit.team));

    public void Occupy(CombatUnit unit)
    {
        if (occupant != unit) return;
        unit.SetCoverProtection(protection, this);
    }

    public bool ProtectsFrom(Vector3 enemyPosition)
    {
        if (coverCollider == null) return false;
        Vector3 from = enemyPosition + Vector3.up * coverCheckHeight;
        Vector3 to = transform.position + Vector3.up * coverCheckHeight;
        Vector3 dir = to - from;
        if (dir.sqrMagnitude < 0.001f) return false;

        RaycastHit[] hits = Physics.RaycastAll(from, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
            if (hit.collider == coverCollider || hit.collider.transform.IsChildOf(coverCollider.transform))
                return true;
        return false;
    }

    public void Release(CombatUnit unit)
    {
        if (unit == null || occupant != unit) return;
        unit.SetCoverProtection(0f, null);
        occupant = null;
    }

    public void Abandon(CombatUnit unit)
    {
        if (unit == null) return;
        if (obstacle != null) obstacle.AbandonFor(unit.team);
        else Release(unit);
    }

    public void Configure(Collider physicalCover, TacticalCoverObstacle owner, float configuredProtection, float configuredOccupancyRadius)
    {
        coverCollider = physicalCover;
        obstacle = owner;
        protection = Mathf.Clamp(configuredProtection, 0f, 0.95f);
        occupancyRadius = Mathf.Max(0.1f, configuredOccupancyRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, occupancyRadius);
        if (coverCollider != null) Gizmos.DrawLine(transform.position, coverCollider.bounds.center);
    }
}
