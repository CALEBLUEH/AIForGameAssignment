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
    [Range(0f, 1f)] public float protection = 1f;
    public float occupancyRadius = 0.45f;
    public float coverCheckHeight = 1.0f;
    [SerializeField] private TacticalCoverObstacle obstacle;

    public bool IsAvailable => occupant == null;
    public bool IsAbandoned => obstacle != null && obstacle.HasAnyAbandonedTeam;
    public TacticalCoverObstacle Obstacle => obstacle;
    public DestructibleCover Destructible => obstacle != null
        ? obstacle.GetComponent<DestructibleCover>() ?? obstacle.GetComponentInParent<DestructibleCover>()
        : GetComponentInParent<DestructibleCover>();
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
        if (obstacle != null && !obstacle.TryReserve(unit)) return false;
        occupant = unit;
        return true;
    }

    public bool CanBeUsedBy(CombatUnit unit) => unit != null &&
        (occupant == null || occupant == unit) &&
        (obstacle == null || (!obstacle.IsAbandonedFor(unit.team) &&
            (obstacle.IsReservedBy(unit) || obstacle.ReservedCount < obstacle.Capacity)));

    public void Occupy(CombatUnit unit)
    {
        if (occupant != unit) return;
        // Tactical cover owns the incoming hit until its health is depleted.
        // Keep this gameplay contract independent from older serialized protection values.
        unit.SetCoverProtection(1f, this);
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
        if (obstacle != null) obstacle.Release(unit);
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
        protection = Mathf.Clamp01(configuredProtection);
        occupancyRadius = Mathf.Max(0.1f, configuredOccupancyRadius);
    }

    public bool TryGetSafeStandPosition(out Vector3 position)
        => TryGetSafeStandPosition(null, out position);

    public bool TryGetSafeStandPosition(CombatUnit unit, out Vector3 position)
    {
        position = transform.position;
        if (!UnityEngine.AI.NavMesh.SamplePosition(position, out UnityEngine.AI.NavMeshHit hit,
                Mathf.Max(0.75f, occupancyRadius * 2f), UnityEngine.AI.NavMesh.AllAreas))
            return false;
        position = hit.position;
        if (coverCollider == null) return true;

        Vector3 closest = coverCollider.ClosestPoint(position);
        Vector3 flatDelta = position - closest;
        flatDelta.y = 0f;
        float unitRadius = 0f;
        if (unit != null)
        {
            Collider body = unit.GetComponent<Collider>() ?? unit.GetComponentInChildren<Collider>(true);
            if (body != null) unitRadius = Mathf.Max(body.bounds.extents.x, body.bounds.extents.z);
        }
        float requiredClearance = Mathf.Max(0.15f, occupancyRadius * 0.5f, unitRadius + 0.12f);
        if (flatDelta.sqrMagnitude >= requiredClearance * requiredClearance) return true;

        Vector3 away = position - coverCollider.bounds.center;
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f) away = transform.forward;
        Vector3 outside = closest + away.normalized * requiredClearance;
        if (!UnityEngine.AI.NavMesh.SamplePosition(outside, out hit,
                Mathf.Max(1f, occupancyRadius * 2f), UnityEngine.AI.NavMesh.AllAreas)) return false;
        position = hit.position;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, occupancyRadius);
        if (coverCollider != null) Gizmos.DrawLine(transform.position, coverCollider.bounds.center);
    }
}
