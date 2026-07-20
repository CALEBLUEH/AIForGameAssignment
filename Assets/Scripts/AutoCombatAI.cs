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

    private float nextAttackTime;
    private float nextTargetSearchTime;
    private float nextPathUpdateTime;

    private void Awake()
    {
        unit = GetComponent<CombatUnit>();
        agent = GetComponent<NavMeshAgent>();

        // We rotate the character ourselves.
        agent.updateRotation = false;
    }

    private void Update()
    {
        if (unit.IsDead)
        {
            StopMoving();
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
        Vector3 movementDirection = agent.desiredVelocity;
        movementDirection.y = 0f;

        if (movementDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        RotateTowards(movementDirection.normalized);
    }

    private void FaceTarget()
    {
        Vector3 targetDirection =
            currentTarget.transform.position -
            transform.position;

        targetDirection.y = 0f;

        if (targetDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        RotateTowards(targetDirection.normalized);
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