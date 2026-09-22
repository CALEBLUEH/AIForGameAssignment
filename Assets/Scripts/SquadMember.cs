using UnityEngine;

[RequireComponent(typeof(AutoCombatAI))]
public class SquadMember : MonoBehaviour
{
    [Header("Squad Leader")]
    public Transform leader;

    [Header("Squad Range")]
    public float maximumDistanceFromLeader = 12f;
    public float returnDistance = 5f;

    [Header("Combat Area")]
    public float leaderCombatRadius = 18f;

    public bool IsTooFarFromLeader()
    {
        if (leader == null)
        {
            return false;
        }

        Vector3 difference =
            leader.position - transform.position;

        difference.y = 0f;

        return difference.sqrMagnitude >
               maximumDistanceFromLeader *
               maximumDistanceFromLeader;
    }

    public bool HasReturnedToLeader()
    {
        if (leader == null)
        {
            return true;
        }

        Vector3 difference =
            leader.position - transform.position;

        difference.y = 0f;

        return difference.sqrMagnitude <=
               returnDistance * returnDistance;
    }

    public bool IsEnemyInsideLeaderCombatArea(
        CombatUnit enemy
    )
    {
        if (leader == null || enemy == null)
        {
            return false;
        }

        Vector3 difference =
            enemy.transform.position -
            leader.position;

        difference.y = 0f;

        return difference.sqrMagnitude <=
               leaderCombatRadius *
               leaderCombatRadius;
    }

    private void OnDrawGizmosSelected()
    {
        if (leader == null)
        {
            return;
        }

        Gizmos.DrawWireSphere(
            leader.position,
            maximumDistanceFromLeader
        );

        Gizmos.DrawWireSphere(
            leader.position,
            leaderCombatRadius
        );
    }
}