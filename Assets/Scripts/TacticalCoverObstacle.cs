using System.Collections.Generic;
using UnityEngine;

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

    private void RemoveInvalidReservations()
    {
        reservations.RemoveWhere(unit => unit == null || unit.IsDead || !unit.gameObject.activeInHierarchy);
    }
}
