using UnityEngine;

/// <summary>
/// Describes how an authored environment prefab participates in navigation and combat queries.
/// It does not generate or move anything at runtime; the values remain editable in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnvironmentAsset : MonoBehaviour
{
    public enum NavigationRole
    {
        WalkableRoad,
        MovementBlocker,
        VisualOnly
    }

    public enum CombatVisibility
    {
        DoesNotBlock,
        BlocksSightAndProjectiles
    }

    [Header("Authored Semantics")]
    [SerializeField] private NavigationRole navigationRole = NavigationRole.VisualOnly;
    [SerializeField] private CombatVisibility combatVisibility = CombatVisibility.DoesNotBlock;
    [SerializeField] private bool usableAsCover;

    public NavigationRole NavRole => navigationRole;
    public CombatVisibility Visibility => combatVisibility;
    public bool UsableAsCover => usableAsCover;

#if UNITY_EDITOR
    public void Configure(
        NavigationRole newNavigationRole,
        CombatVisibility newCombatVisibility,
        bool newUsableAsCover)
    {
        navigationRole = newNavigationRole;
        combatVisibility = newCombatVisibility;
        usableAsCover = newUsableAsCover;
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = navigationRole switch
        {
            NavigationRole.WalkableRoad => new Color(0.15f, 0.8f, 0.3f, 0.7f),
            NavigationRole.MovementBlocker => new Color(0.95f, 0.3f, 0.15f, 0.7f),
            _ => new Color(0.3f, 0.65f, 1f, 0.45f)
        };

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
