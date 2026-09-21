using UnityEngine;
using UnityEngine.AI;

// Uses NavMesh.CalculatePath and Transform movement; never requires NavMeshAgent.
[RequireComponent(typeof(CombatUnit))]
public class AutoCombatAI : MonoBehaviour
{
    public enum MovementKind { Charge, Dash, Flash }
    public enum CharacterSkillKind { PowerUp, Burst, Heal }
    public float rotationSpeed = 10f, movementSpeed = 3.5f;
    public float targetSearchInterval = 0.25f, pathUpdateInterval = 0.35f;
    public LayerMask sightBlockers;
    public MovementKind movementKind;
    public CharacterSkillKind characterSkillKind;
    public float movementRange = 7f, movementCost = 35f, characterSkillCost = 40f, skillRange = 6f;
    public float movementCooldown = 7f, characterSkillCooldown = 10f;
    [Header("Automatic action set")]
    public int basicAttacksBeforeFinisher = 2;
    public float finisherMultiplier = 1.5f;
    [SerializeField] private CombatUnit currentTarget;
    [SerializeField] private string currentState = "Waiting";
    private CombatUnit unit;
    private SquadMember member;
    private NavMeshPath path;
    private int corner;
    private float nextAttack, nextSearch, nextPath;
    private float movementReadyAt, characterSkillReadyAt;
    private int actionIndex;
    private bool returning, skillMoving;
    private Vector3 skillDestination;
    private float skillSpeed;
    private CoverPoint coverPoint;
    public string CurrentState => currentState;
    public CombatUnit Unit => unit;
    public float MovementCooldownRemaining => Mathf.Max(0f, movementReadyAt - Time.time);
    public float CharacterCooldownRemaining => Mathf.Max(0f, characterSkillReadyAt - Time.time);
    private void Awake()
    {
        unit = GetComponent<CombatUnit>();
        member = GetComponent<SquadMember>();
        path = new NavMeshPath();
    }
    private void Update()
    {
        if (unit.IsDead) { currentState = "Down"; return; }
        if (BattleDirector.Instance != null && !BattleDirector.Instance.IsPlaying) return;
        if (skillMoving)
        {
            Move(skillSpeed);
            if (Distance(transform.position, skillDestination) < 0.25f || !HasPath())
            { skillMoving = false; currentState = "Waiting"; }
            return;
        }
        if (unit.team == CombatUnit.CombatTeam.Player && BattleDirector.Instance != null &&
            !BattleDirector.Instance.AutoEnabled)
        {
            currentState = "Manual";
            return;
        }
        if (member != null && member.IsTooFarFromLeader() && Time.time >= nextAttack) returning = true;
        if (returning && member != null && !member.HasReturnedToLeader())
        {
            currentState = "Returning";
            Navigate(member.leader.position, member.returnDistance);
            Move(movementSpeed);
            return;
        }
        returning = false;
        if (Time.time >= nextSearch || currentTarget == null || currentTarget.IsDead)
        { FindTarget(); nextSearch = Time.time + targetSearchInterval; }
        if (currentTarget == null)
        {
            ReleaseCover();
            ClearPath();
            currentState = "Advancing";
            return;
        }
        if (unit.team == CombatUnit.CombatTeam.Player && HandleCover()) return;
        if (Distance(transform.position, currentTarget.transform.position) <= unit.AttackRange && HasSight(currentTarget))
        {
            ClearPath(); Face(currentTarget.transform.position - transform.position);
            currentState = "Attacking";
            if (Time.time >= nextAttack)
            {
                bool finisher = actionIndex >= basicAttacksBeforeFinisher;
                currentTarget.TakeDamage(unit.AttackPower * (finisher ? finisherMultiplier : 1f),
                    transform.position);
                actionIndex = finisher ? 0 : actionIndex + 1;
                currentState = finisher ? "Finisher" : "Basic attack";
                nextAttack = Time.time + 1f / Mathf.Max(0.1f, unit.AttackSpeed);
            }
        }
        else
        {
            currentState = "Pursuing";
            Navigate(currentTarget.transform.position, unit.AttackRange * 0.8f);
            Move(movementSpeed);
        }
    }

    private bool HandleCover()
    {
        if (coverPoint != null)
        {
            if (!coverPoint.TryReserve(unit)) { coverPoint = null; return false; }
            if (Distance(transform.position, coverPoint.transform.position) > 0.4f)
            {
                currentState = "Taking cover";
                Navigate(coverPoint.transform.position, 0.15f);
                Move(movementSpeed);
                return true;
            }
            coverPoint.Occupy(unit);
            if (Distance(transform.position, currentTarget.transform.position) <= unit.AttackRange &&
                HasSight(currentTarget)) return false;
            coverPoint.Abandon(unit);
            coverPoint = null;
        }
        float targetDistance = Distance(transform.position, currentTarget.transform.position);
        if (targetDistance <= unit.AttackRange) return false;
        Vector3 towardEnemy = (currentTarget.transform.position - transform.position).normalized;
        CoverPoint best = null;
        float bestDistance = 12f;
        foreach (CoverPoint candidate in CoverPoint.All)
        {
            if (!candidate.IsAvailable) continue;
            Vector3 toCover = candidate.transform.position - transform.position;
            toCover.y = 0f;
            float distance = toCover.magnitude;
            if (distance >= bestDistance || Vector3.Dot(towardEnemy, toCover.normalized) < 0.35f) continue;
            if (Distance(candidate.transform.position, currentTarget.transform.position) >= targetDistance) continue;
            best = candidate;
            bestDistance = distance;
        }
        if (best == null || !best.TryReserve(unit)) return false;
        coverPoint = best;
        return true;
    }

    private void ReleaseCover()
    {
        if (coverPoint == null) return;
        coverPoint.Release(unit);
        coverPoint = null;
    }
    private void FindTarget()
    {
        currentTarget = null;
        float best = float.PositiveInfinity;
        foreach (var candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (candidate == unit || candidate.IsDead || candidate.team == unit.team) continue;
            float distance = Distance(transform.position, candidate.transform.position);
            if (distance < best) { best = distance; currentTarget = candidate; }
        }
    }
    private bool HasSight(CombatUnit target) =>
        !Physics.Linecast(transform.position + Vector3.up, target.transform.position + Vector3.up,
            sightBlockers, QueryTriggerInteraction.Ignore);
    private void Navigate(Vector3 destination, float stopDistance)
    {
        if (Distance(transform.position, destination) <= stopDistance) { ClearPath(); return; }
        if (Time.time < nextPath && HasPath()) return;
        nextPath = Time.time + pathUpdateInterval;
        if (!NavMesh.SamplePosition(transform.position, out var start, 2f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(destination, out var end, 2f, NavMesh.AllAreas) ||
            !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete)
        { ClearPath(); currentState = "No path"; return; }
        corner = path.corners.Length > 1 ? 1 : 0;
    }
    private bool HasPath() => path != null && path.corners != null && corner < path.corners.Length;
    private void Move(float speed)
    {
        if (!HasPath()) return;
        Vector3 delta = path.corners[corner] - transform.position; delta.y = 0f;
        if (delta.sqrMagnitude < 0.04f) { corner++; return; }
        Vector3 step = delta.normalized * Mathf.Min(speed * Time.deltaTime, delta.magnitude);
        transform.position += step; Face(step);
    }
    private void Face(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction),
                rotationSpeed * Time.deltaTime);
    }
    private void ClearPath() { if (path != null) path.ClearCorners(); corner = 0; }
    public bool TryMovementSkill(Vector3 destination)
    {
        if (unit.IsDead || MovementCooldownRemaining > 0f || !unit.SpendMovementPoints(movementCost)) return false;
        ReleaseCover();
        Vector3 delta = destination - transform.position; delta.y = 0f;
        destination = transform.position + Vector3.ClampMagnitude(delta, movementRange);
        if (!NavMesh.SamplePosition(destination, out var hit, 2f, NavMesh.AllAreas))
        { unit.RefundMovementPoints(movementCost); return false; }
        if (movementKind == MovementKind.Flash)
        {
            transform.position = hit.position;
            ClearPath();
            currentState = "Flashed";
            movementReadyAt = Time.time + movementCooldown;
            return true;
        }
        if (movementKind == MovementKind.Dash &&
            Physics.Linecast(transform.position + Vector3.up, hit.position + Vector3.up, sightBlockers))
        { unit.RefundMovementPoints(movementCost); return false; }
        nextPath = 0f; Navigate(hit.position, 0f);
        if (!HasPath()) { unit.RefundMovementPoints(movementCost); return false; }
        skillDestination = hit.position;
        skillSpeed = movementKind == MovementKind.Charge ? 12f : 18f;
        skillMoving = true; currentState = movementKind.ToString();
        movementReadyAt = Time.time + movementCooldown;
        return true;
    }
    public bool TryCharacterSkill(CombatUnit target)
    {
        if (unit.IsDead || CharacterCooldownRemaining > 0f || BattleDirector.Instance == null ||
            !BattleDirector.Instance.TrySpendUniversal(characterSkillCost)) return false;
        bool used = true;
        switch (characterSkillKind)
        {
            case CharacterSkillKind.PowerUp:
                unit.AddTimedModifier(CombatUnit.Stat.Attack, 12f, 0.2f, 8f); break;
            case CharacterSkillKind.Burst:
                used = target != null && !target.IsDead && target.team != unit.team &&
                    Distance(transform.position, target.transform.position) <= skillRange;
                if (used) target.TakeDamage(unit.AttackPower * 2.5f, transform.position);
                break;
            case CharacterSkillKind.Heal:
                used = target != null && !target.IsDead && target.team == unit.team &&
                    Distance(transform.position, target.transform.position) <= skillRange;
                if (used) target.Heal(35f);
                break;
        }
        if (!used) BattleDirector.Instance.RefundUniversal(characterSkillCost);
        else characterSkillReadyAt = Time.time + characterSkillCooldown;
        return used;
    }
    private void OnDisable() { ReleaseCover(); }
    private static float Distance(Vector3 a, Vector3 b)
    { a.y = b.y = 0f; return Vector3.Distance(a, b); }
}
