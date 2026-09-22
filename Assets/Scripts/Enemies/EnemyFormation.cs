using System.Collections.Generic;
using UnityEngine;

public enum EnemyFormationShape
{
    Row,
    Triangle,
    Box
}

public sealed class EnemyFormationCoordinator : MonoBehaviour
{
    private readonly List<EnemyFormationMember> members = new List<EnemyFormationMember>();
    private EnemyFormationShape shape;
    private float horizontalSpacing;
    private float rowSpacing;
    private float positionTolerance;

    public void Configure(EnemyFormationShape configuredShape, float configuredHorizontalSpacing,
        float configuredRowSpacing, float configuredTolerance)
    {
        shape = configuredShape;
        horizontalSpacing = Mathf.Max(0.5f, configuredHorizontalSpacing);
        rowSpacing = Mathf.Max(0.5f, configuredRowSpacing);
        positionTolerance = Mathf.Max(0.1f, configuredTolerance);
    }

    public EnemyFormationMember AddMember(GameObject enemy)
    {
        EnemyFormationMember member = enemy.GetComponent<EnemyFormationMember>();
        if (member == null) member = enemy.AddComponent<EnemyFormationMember>();
        members.Add(member);
        member.Configure(this, positionTolerance);
        return member;
    }

    public bool TryGetDestination(EnemyFormationMember member, Vector3 targetPosition, out Vector3 destination)
    {
        members.RemoveAll(candidate => candidate == null || !candidate.gameObject.activeInHierarchy);
        int rank = members.IndexOf(member);
        if (rank <= 0)
        {
            destination = default;
            return false;
        }

        Transform leader = members[0].transform;
        Vector3 forward = targetPosition - leader.position;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = leader.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector2 offset = FormationOffset(shape, rank, horizontalSpacing, rowSpacing);
        destination = leader.position + right * offset.x + forward * offset.y;
        return true;
    }

    public static Vector2 FormationOffset(EnemyFormationShape formation, int index, float horizontal, float depth)
    {
        if (index <= 0) return Vector2.zero;
        switch (formation)
        {
            case EnemyFormationShape.Row:
                int side = index % 2 == 1 ? -1 : 1;
                int step = (index + 1) / 2;
                return new Vector2(side * step * horizontal, 0f);
            case EnemyFormationShape.Triangle:
                int remaining = index - 1;
                int triangleRow = 1;
                while (remaining >= triangleRow + 1)
                {
                    remaining -= triangleRow + 1;
                    triangleRow++;
                }
                int count = triangleRow + 1;
                return new Vector2((remaining - (count - 1) * 0.5f) * horizontal, -triangleRow * depth);
            default:
                int boxIndex = index - 1;
                int column = boxIndex % 3;
                int row = boxIndex / 3 + 1;
                return new Vector2((column - 1) * horizontal, -row * depth);
        }
    }
}

[DisallowMultipleComponent]
public sealed class EnemyFormationMember : MonoBehaviour
{
    private EnemyFormationCoordinator coordinator;
    public float PositionTolerance { get; private set; } = 0.55f;

    public void Configure(EnemyFormationCoordinator configuredCoordinator, float tolerance)
    {
        coordinator = configuredCoordinator;
        PositionTolerance = Mathf.Max(0.1f, tolerance);
    }

    public bool TryGetDestination(Vector3 targetPosition, out Vector3 destination)
    {
        if (coordinator != null) return coordinator.TryGetDestination(this, targetPosition, out destination);
        destination = default;
        return false;
    }
}
