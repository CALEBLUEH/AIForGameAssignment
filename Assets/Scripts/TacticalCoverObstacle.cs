using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticalCoverObstacle : MonoBehaviour
{
    private readonly HashSet<CombatUnit.CombatTeam> abandonedTeams = new HashSet<CombatUnit.CombatTeam>();

    public bool HasAnyAbandonedTeam => abandonedTeams.Count > 0;

    private void OnEnable()
    {
        abandonedTeams.Clear();
    }

    public bool IsAbandonedFor(CombatUnit.CombatTeam team) => abandonedTeams.Contains(team);

    public void AbandonFor(CombatUnit.CombatTeam team)
    {
        if (!abandonedTeams.Add(team)) return;
        foreach (CoverPoint point in GetComponentsInChildren<CoverPoint>(true))
        {
            CombatUnit occupant = point.Occupant;
            if (occupant != null && occupant.team == team) point.Release(occupant);
        }
    }
}
