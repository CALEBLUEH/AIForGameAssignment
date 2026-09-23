using System;
using UnityEngine;

[RequireComponent(typeof(AutoCombatAI), typeof(CombatUnit))]
public sealed class CombatAnimationDriver : MonoBehaviour
{
    [SerializeField] private AutoCombatAI combatAI;
    [SerializeField] private CombatUnit combatUnit;
    [SerializeField] private Animator characterAnimator;

    private int currentStateHash;

    private void Awake()
    {
        if (combatAI == null) combatAI = GetComponent<AutoCombatAI>();
        if (combatUnit == null) combatUnit = GetComponent<CombatUnit>();
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>(true);
        if (characterAnimator != null) characterAnimator.applyRootMotion = false;
        PlayState("Idle");
    }

    private void Update()
    {
        if (characterAnimator == null || combatAI == null || combatUnit == null) return;
        PlayState(ResolveState());
    }

    public void Configure(AutoCombatAI ai, CombatUnit unit, Animator animator)
    {
        combatAI = ai;
        combatUnit = unit;
        characterAnimator = animator;
        if (characterAnimator != null) characterAnimator.applyRootMotion = false;
    }

    private string ResolveState()
    {
        if (combatUnit.IsDead) return "Retreat";

        string state = combatAI.CurrentState ?? string.Empty;
        if (ContainsAny(state, "attack", "attacking", "skill", "finisher", "auto skill", "status ability", "fighting from cover"))
            return "Attack";
        if (ContainsAny(state, "pursuing", "advancing", "returning", "repositioning", "avoiding", "taking cover",
            "leaving", "reserved", "charge", "dash", "flashed", "fleeing", "knockback"))
            return "Run";
        return "Idle";
    }

    private void PlayState(string stateName)
    {
        if (characterAnimator == null) return;
        int hash = Animator.StringToHash(stateName);
        if (hash == currentStateHash) return;
        currentStateHash = hash;
        characterAnimator.CrossFadeInFixedTime(hash, 0.12f, 0);
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        foreach (string term in terms)
            if (value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
}
