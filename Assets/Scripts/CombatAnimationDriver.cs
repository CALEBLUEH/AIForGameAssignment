using System;
using UnityEngine;

[RequireComponent(typeof(AutoCombatAI), typeof(CombatUnit))]
public sealed class CombatAnimationDriver : MonoBehaviour
{
    [SerializeField] private AutoCombatAI combatAI;
    [SerializeField] private CombatUnit combatUnit;
    [SerializeField] private Animator characterAnimator;

    private int currentStateHash;
    private Transform visualRoot;
    private Collider bodyCollider;
    private SkinnedMeshRenderer[] visualRenderers = Array.Empty<SkinnedMeshRenderer>();
    private float appliedVisualLift;
    private const float GroundTolerance = 0.015f;
    private const float MaximumVisualLift = 0.8f;

    private void Awake()
    {
        if (combatAI == null) combatAI = GetComponent<AutoCombatAI>();
        if (combatUnit == null) combatUnit = GetComponent<CombatUnit>();
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>(true);
        InitializeAnimatorPresentation();
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
        InitializeAnimatorPresentation();
    }

    private void LateUpdate()
    {
        if (characterAnimator == null || visualRoot == null || visualRoot == transform || bodyCollider == null) return;
        if (!TryGetVisualSoleY(out float soleY)) return;

        float error = bodyCollider.bounds.min.y - soleY;
        float adjustment = 0f;
        if (error > GroundTolerance)
            adjustment = Mathf.Min(error, MaximumVisualLift - appliedVisualLift);
        else if (error < -GroundTolerance && appliedVisualLift > 0f)
            adjustment = -Mathf.Min(-error, appliedVisualLift);
        if (Mathf.Abs(adjustment) <= 0.0001f) return;

        visualRoot.position += Vector3.up * adjustment;
        appliedVisualLift = Mathf.Clamp(appliedVisualLift + adjustment, 0f, MaximumVisualLift);
    }

    private void InitializeAnimatorPresentation()
    {
        if (characterAnimator == null) return;
        characterAnimator.applyRootMotion = false;
        visualRoot = characterAnimator.transform;
        while (visualRoot.parent != null && visualRoot.parent != transform)
            visualRoot = visualRoot.parent;
        if (visualRoot == transform)
        {
            SkinnedMeshRenderer fallback = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (fallback != null)
            {
                visualRoot = fallback.transform;
                while (visualRoot.parent != null && visualRoot.parent != transform)
                    visualRoot = visualRoot.parent;
            }
        }
        bodyCollider = GetComponent<Collider>();
        visualRenderers = visualRoot == null
            ? Array.Empty<SkinnedMeshRenderer>()
            : visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        appliedVisualLift = 0f;
    }

    private bool TryGetVisualSoleY(out float soleY)
    {
        soleY = float.PositiveInfinity;
        foreach (SkinnedMeshRenderer visual in visualRenderers)
            if (visual != null && visual.enabled && visual.gameObject.activeInHierarchy)
                soleY = Mathf.Min(soleY, visual.bounds.min.y);
        return soleY < float.PositiveInfinity;
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
