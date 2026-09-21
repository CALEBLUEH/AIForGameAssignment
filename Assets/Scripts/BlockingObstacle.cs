using UnityEngine;

// TYPE 2: visual/movement blocker only. This is NOT cover and has no CoverPoint.
// When this blocks sight to an enemy, AutoCombatAI searches for a reachable firing
// position around it and resumes normal attacks after line of sight is restored.
public class BlockingObstacle : MonoBehaviour
{
    [Tooltip("If enabled, this object prevents attacks through it and forces AI repositioning.")]
    public bool blocksLineOfSight = true;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.65f);
        Collider obstacle = GetComponent<Collider>();
        if (obstacle != null) Gizmos.DrawWireCube(obstacle.bounds.center, obstacle.bounds.size);
    }
}
