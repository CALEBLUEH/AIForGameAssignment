using System;
using UnityEngine;

public enum StatusEffectType
{
    Strength, Rage, Weak, Dull, Hardening, Fortified, Penetration, Piercing
}

[Serializable]
public struct StatusEffectSpec
{
    public StatusEffectType type;
    public float amount;
    public float duration;
    public bool IsValid => Mathf.Abs(amount) > 0.001f && duration > 0f;
}
