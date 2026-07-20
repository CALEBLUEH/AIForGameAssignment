using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CombatUnit))]
[RequireComponent(typeof(NavMeshAgent))]
public class AutoCombatAI : MonoBehaviour
{
    [Header("Movement")]
    public float rotationSpeed = 10f;

    [Header("AI Timing")]
    public float targetSearchInterval = 0.25f;
    public float pathUpdateInterval = 0.2f;

    [Header("Runtime")]
    [SerializeField] private CombatUnit currentTarget;
    [SerializeField] private string currentState = "Waiting";

    private CombatUnit unit;
    private NavMeshAgent agent;
    private SquadMember squadMember;

    private float nextAttackTime;
    private float nextTargetSearchTime;
    private float nextPathUpdateTime;

    private void Awake()
    {
        unit = GetComponent<CombatUnit>();
        agent = GetComponent<NavMeshAgent>();

        // Only squad members have this component.
        squadMember = GetComponent<SquadMember>();

        // Rotation is handled by this script.
        agent.updateRotation = false;
    }

    private void Update()
    {
        if (unit.IsDead)
        {
            StopMoving();
            return;
        }

        // Returning to the leader has higher priority than combat.
        if (ShouldReturnToLeader())
        {
            ReturnToLeader();
            currentState = "Returning to Leader";
            return;
        }

        SearchForTargetWhenNeeded();

        if (!IsTargetValid())
        {
            StopMoving();
            currentState = "Waiting";
            return;
        }

        float distanceToTarget = GetDistanceToTarget();

        if (distanceToTarget > unit.attackRange)
        {
            MoveTowardsTarget();
            currentState = "Moving";
        }
        else
        {
            StopMoving();
            FaceTarget();
            AttackTarget();
            currentState = "Attacking";
        }
    }

    private bool ShouldReturnToLeader()
    {
        if (squadMember == null)
        {
            return false;
        }

        return squadMember.IsTooFarFromLeader();
    }

    private void ReturnToLeader()
    {
        if (squadMember == null || squadMember.leader == null)
        {
            return;
        }

        // Ignore the enemy while returning.
        currentTarget = null;

        if (!agent.isOnNavMesh)
        {
            return;
        }

        float distanceToLeader = Vector3.Distance(
            transform.position,
            squadMember.leader.position
        );

        if (distanceToLeader <= squadMember.returnDistance)
        {
            StopMoving();
            return;
        }

        if (Time.time >= nextPathUpdateTime)
        {
            agent.isStopped = false;
            agent.stoppingDistance = squadMember.returnDistance;

            agent.SetDestination(
                squadMember.leader.position
            );

            nextPathUpdateTime =
                Time.time + pathUpdateInterval;
        }

        FaceMovementDirection();
    }

    private void SearchForTargetWhenNeeded()
    {
        bool targetIsMissing =
            currentTarget == null ||
            currentTarget.IsDead;

        if (!targetIsMissing &&
            Time.time < nextTargetSearchTime)
        {
            return;
        }

        FindNearestEnemy();

        nextTargetSearchTime =
            Time.time + targetSearchInterval;
    }

    private void FindNearestEnemy()
    {
        CombatUnit[] allUnits =
            FindObjectsByType<CombatUnit>(
                FindObjectsSortMode.None
            );

        CombatUnit nearestEnemy = null;
        float nearestDistanceSquared = Mathf.Infinity;

        foreach (CombatUnit possibleTarget in allUnits)
        {
            if (possibleTarget == unit)
            {
                continue;
            }
            

            if (possibleTarget.IsDead)
            {
                continue;
            }

            if (possibleTarget.team == unit.team)
            {
                continue;
            }
            if (squadMember != null)
{
    bool enemyIsInsideCombatArea =
        squadMember.IsEnemyInsideLeaderCombatArea(
            possibleTarget
        );

    if (!enemyIsInsideCombatArea)
    {
        continue;
    }
}

            Vector3 difference =
                possibleTarget.transform.position -
                transform.position;

            difference.y = 0f;

            float distanceSquared =
                difference.sqrMagnitude;

            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestEnemy = possibleTarget;
            }
        }

        currentTarget = nearestEnemy;
    }

    private bool IsTargetValid()
    {
        return currentTarget != null &&
               !currentTarget.IsDead &&
               currentTarget.team != unit.team;
    }

    private float GetDistanceToTarget()
    {
        Vector3 difference =
            currentTarget.transform.position -
            transform.position;

        difference.y = 0f;

        return difference.magnitude;
    }

    private void MoveTowardsTarget()
    {
        if (!agent.isOnNavMesh)
        {
            return;
        }

        if (Time.time >= nextPathUpdateTime)
        {
            agent.isStopped = false;
            agent.stoppingDistance = unit.attackRange * 0.9f;

            agent.SetDestination(
                currentTarget.transform.position
            );

            nextPathUpdateTime =
                Time.time + pathUpdateInterval;
        }

        FaceMovementDirection();
    }

    private void StopMoving()
    {
        if (!agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;

        if (agent.hasPath)
        {
            agent.ResetPath();
        }
    }

    private void FaceMovementDirection()
    {
        Vector3 direction = agent.desiredVelocity;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        RotateTowards(direction.normalized);
    }

    private void FaceTarget()
    {
        Vector3 direction =
            currentTarget.transform.position -
            transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        RotateTowards(direction.normalized);
    }

    private void RotateTowards(Vector3 direction)
    {
        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void AttackTarget()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        if (!IsTargetValid())
        {
            return;
        }

        currentTarget.TakeDamage(unit.attackPower);

        float attackInterval =
            1f / Mathf.Max(0.01f, unit.attackSpeed);

        nextAttackTime =
            Time.time + attackInterval;

        Debug.DrawLine(
            transform.position + Vector3.up,
            currentTarget.transform.position + Vector3.up,
            Color.red,
            0.3f
        );
    }
}