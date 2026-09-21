using System.Collections.Generic;
using UnityEngine;

public class CoverPoint : MonoBehaviour
{
    public static readonly List<CoverPoint> All = new List<CoverPoint>();
    public float protection = 0.35f;
    public bool IsAvailable => occupant == null && !abandoned;
    public bool IsAbandoned => abandoned;
    private CombatUnit occupant;
    private bool abandoned;

    private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    private void OnDisable() { All.Remove(this); Release(occupant); }

    public bool TryReserve(CombatUnit unit)
    {
        if (unit == null || abandoned || (occupant != null && occupant != unit)) return false;
        occupant = unit;
        return true;
    }

    public void Occupy(CombatUnit unit)
    {
        if (occupant != unit) return;
        unit.SetCoverProtection(protection);
    }

    public void Release(CombatUnit unit)
    {
        if (unit == null || occupant != unit) return;
        unit.SetCoverProtection(0f);
        occupant = null;
    }

    public void Abandon(CombatUnit unit)
    {
        Release(unit);
        abandoned = true;
    }
}
