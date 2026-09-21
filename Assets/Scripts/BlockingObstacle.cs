using UnityEngine;

// Marker for solid sight/movement blockers. It intentionally exposes no cover position.
public class BlockingObstacle : MonoBehaviour
{
    public bool blocksLineOfSight = true;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.65f);
        Collider obstacle = GetComponent<Collider>();
        if (obstacle != null) Gizmos.DrawWireCube(obstacle.bounds.center, obstacle.bounds.size);
    }
}
