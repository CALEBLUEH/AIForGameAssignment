using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class TacticalCoverObstacle : MonoBehaviour
{
    [Header("Shared Occupancy")]
    [Min(1)] [SerializeField] private int capacity = 1;
    private readonly HashSet<CombatUnit.CombatTeam> abandonedTeams = new HashSet<CombatUnit.CombatTeam>();
    private readonly HashSet<CombatUnit> reservations = new HashSet<CombatUnit>();

    public bool HasAnyAbandonedTeam => abandonedTeams.Count > 0;
    public int Capacity => Mathf.Max(1, capacity);
    public int ReservedCount
    {
        get
        {
            RemoveInvalidReservations();
            return reservations.Count;
        }
    }

    private void OnEnable()
    {
        abandonedTeams.Clear();
        reservations.Clear();
    }

    private void Start() => EnsureRuntimeCoverPoints();

    private void OnDisable()
    {
        foreach (CoverPoint point in CoverPoint.All.ToArray())
            if (point != null && point.Obstacle == this && point.Occupant != null)
                point.Release(point.Occupant);
        reservations.Clear();
    }

    public bool IsAbandonedFor(CombatUnit.CombatTeam team) => abandonedTeams.Contains(team);

    public bool TryReserve(CombatUnit unit)
    {
        if (unit == null || unit.IsDead || IsAbandonedFor(unit.team)) return false;
        RemoveInvalidReservations();
        if (reservations.Contains(unit)) return true;
        if (reservations.Count >= Capacity) return false;
        reservations.Add(unit);
        return true;
    }

    public bool IsReservedBy(CombatUnit unit) => unit != null && reservations.Contains(unit);

    public void Release(CombatUnit unit)
    {
        if (unit != null) reservations.Remove(unit);
    }

    public void AbandonFor(CombatUnit.CombatTeam team)
    {
        if (!abandonedTeams.Add(team)) return;
        foreach (CoverPoint point in CoverPoint.All.ToArray())
        {
            if (point == null || point.Obstacle != this) continue;
            CombatUnit occupant = point.Occupant;
            if (occupant != null && occupant.team == team) point.Release(occupant);
        }
    }

    public void ConfigureCapacity(int configuredCapacity) => capacity = Mathf.Max(1, configuredCapacity);

    /// <summary>
    /// Level scenes may contain cover prefabs without scene-authored standing points.
    /// Generate reachable points only for those instances; authored Sandbox points are
    /// left untouched and nothing is written back to the scene or prefab.
    /// </summary>
    public int EnsureRuntimeCoverPoints()
    {
        if (GetComponentsInChildren<CoverPoint>(true).Length > 0) return 0;

        Collider physical = null;
        float largestBounds = 0f;
        foreach (Collider candidate in GetComponentsInChildren<Collider>(true))
        {
            if (candidate == null || candidate.isTrigger) continue;
            float size = candidate.bounds.size.sqrMagnitude;
            if (size <= largestBounds) continue;
            largestBounds = size;
            physical = candidate;
        }
        if (physical == null) return 0;

        GameObject root = new GameObject("Runtime Cover Points");
        root.transform.SetParent(transform, false);

        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float rightThickness = ProjectedExtent(physical.bounds.extents, right);
        float forwardThickness = ProjectedExtent(physical.bounds.extents, forward);
        Vector3 primary = rightThickness <= forwardThickness ? right : forward;
        Vector3 secondary = primary == right ? forward : right;

        int created = CreateOpposingPoints(root.transform, physical, primary);
        if (created == 0) created = CreateOpposingPoints(root.transform, physical, secondary);
        if (created == 0) Destroy(root);
        return created;
    }

    private int CreateOpposingPoints(Transform root, Collider physical, Vector3 axis)
    {
        int created = 0;
        foreach (Vector3 direction in new[] { axis, -axis })
        {
            Ray ray = new Ray(physical.bounds.center + direction * 100f, -direction);
            if (!physical.Raycast(ray, out RaycastHit surface, 200f)) continue;
            Vector3 intended = surface.point + direction * 1.1f;
            if (!NavMesh.SamplePosition(intended, out NavMeshHit navHit, 3f, NavMesh.AllAreas)) continue;

            bool duplicate = false;
            foreach (CoverPoint existing in root.GetComponentsInChildren<CoverPoint>())
            {
                Vector3 delta = existing.transform.position - navHit.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.64f) { duplicate = true; break; }
            }
            if (duplicate) continue;

            GameObject pointObject = new GameObject("Runtime Cover Point " + (created + 1));
            pointObject.transform.SetParent(root, true);
            pointObject.transform.position = navHit.position;
            pointObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            CoverPoint point = pointObject.AddComponent<CoverPoint>();
            point.Configure(physical, this, 1f, 0.65f);
            created++;
        }
        return created;
    }

    private static float ProjectedExtent(Vector3 extents, Vector3 axis) =>
        Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;

    private void RemoveInvalidReservations()
    {
        reservations.RemoveWhere(unit => unit == null || unit.IsDead || !unit.gameObject.activeInHierarchy);
    }
}
