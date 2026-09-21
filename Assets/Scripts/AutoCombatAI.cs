using UnityEngine;
using UnityEngine.AI;

// Uses NavMesh.CalculatePath and Transform movement; never requires NavMeshAgent.
[RequireComponent(typeof(CombatUnit))]
public class AutoCombatAI : MonoBehaviour
{
    public enum MovementKind { Charge, Dash, Flash }
    public enum CharacterSkillKind { PowerUp, Burst, Heal, Defensive, LowCostAOE, HighCostAOE }
    public enum CombatRole { Enemy, YuukaTank, AyaneHealer, MikaSingleTarget, MomoiLowCostAOE, HinaHighCostAOE }

    [Header("Identity and movement")]
    public CombatRole role;
    public int rosterOrder;
    public MovementKind movementKind;
    public CharacterSkillKind characterSkillKind;
    public float rotationSpeed = 10f, movementSpeed = 3.5f;
    public float targetSearchInterval = 0.25f, pathUpdateInterval = 0.35f;
    public LayerMask sightBlockers;
    public float movementRange = 7f, movementCost = 35f, movementCooldown = 7f;
    [Header("Separation")]
    public float separationDistance = 1.35f;
    public float separationStrength = 0.7f;
    [Header("Cover")]
    public float coverDetectionRange = 12f;
    [Header("Character skill balance")]
    public float characterSkillCost = 40f, characterSkillCooldown = 10f, skillRange = 6f;
    public float skillPowerMultiplier = 2.5f, skillHealAmount = 35f, aoeRadius = 4f;
    public StatusEffectSpec primaryEffect;
    public StatusEffectSpec secondaryEffect;
    [Header("Auto decision thresholds")]
    [Range(0.05f, 1f)] public float defensiveHealthThreshold = 0.55f;
    [Range(0.05f, 1f)] public float healingThreshold = 0.72f;
    public int aoeMinimumEnemyCount = 2;
    public int expensiveAoeMinimumEnemyCount = 3;
    public float autoDecisionInterval = 0.4f;
    [Header("Enemy status ability")]
    public StatusEffectSpec[] selfAbilityEffects;
    public StatusEffectSpec[] targetAbilityEffects;
    public StatusEffectSpec[] allyAbilityEffects;
    public float enemyAbilityCooldown = 9f;
    public float enemyAbilityRange = 8f;
    public int allyBuffMaxTargets = 3;
    [Header("Automatic attack set")]
    public int basicAttacksBeforeFinisher = 2;
    public float finisherMultiplier = 1.5f;

    [SerializeField] private CombatUnit currentTarget;
    [SerializeField] private string currentState = "Waiting";
    private CombatUnit unit;
    private SquadMember member;
    private NavMeshPath path;
    private int corner, actionIndex, enemyAbilityCycle;
    private float nextAttack, nextSearch, nextPath, nextAutoDecision;
    private float movementReadyAt, characterSkillReadyAt, enemyAbilityReadyAt;
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
            Move(skillSpeed, false);
            if (Distance(transform.position, skillDestination) < 0.25f || !HasPath())
            { skillMoving = false; currentState = "Waiting"; }
            return;
        }
        bool playerAuto = unit.team != CombatUnit.CombatTeam.Player || BattleDirector.Instance == null ||
            BattleDirector.Instance.AutoEnabled;
        if (!playerAuto) { currentState = "Manual"; return; }
        if (member != null && member.IsTooFarFromLeader() && Time.time >= nextAttack) returning = true;
        if (returning && member != null && !member.HasReturnedToLeader())
        {
            currentState = "Returning";
            Navigate(member.leader.position, member.returnDistance);
            Move(movementSpeed, true);
            return;
        }
        returning = false;
        if (Time.time >= nextSearch || currentTarget == null || currentTarget.IsDead)
        { FindTarget(); nextSearch = Time.time + targetSearchInterval; }
        if (currentTarget == null)
        {
            ReleaseCover(); ClearPath(); ApplyIdleSeparation(); currentState = "Advancing"; return;
        }
        if (Time.time >= nextAutoDecision)
        {
            nextAutoDecision = Time.time + autoDecisionInterval;
            if (unit.team == CombatUnit.CombatTeam.Player && TryAutoTacticalAction()) return;
            if (unit.team == CombatUnit.CombatTeam.Enemy && TryEnemyAbility()) return;
        }
        if (unit.team == CombatUnit.CombatTeam.Player && HandleCover()) return;
        bool inRange = Distance(transform.position, currentTarget.transform.position) <= unit.AttackRange;
        bool hasSight = HasSight(currentTarget);
        if (inRange && hasSight)
        {
            ClearPath(); ApplyIdleSeparation(); Face(currentTarget.transform.position - transform.position);
            currentState = "Attacking";
            if (Time.time >= nextAttack)
            {
                bool finisher = actionIndex >= basicAttacksBeforeFinisher;
                currentTarget.TakeDamage(unit.AttackPower * (finisher ? finisherMultiplier : 1f), transform.position);
                actionIndex = finisher ? 0 : actionIndex + 1;
                currentState = finisher ? "Finisher" : "Basic attack";
                nextAttack = Time.time + 1f / Mathf.Max(0.1f, unit.AttackSpeed);
            }
        }
        else
        {
            currentState = inRange ? "Repositioning for line of sight" : "Pursuing";
            Navigate(currentTarget.transform.position, hasSight ? unit.AttackRange * 0.8f : 0.2f);
            Move(movementSpeed, true);
        }
    }

    private bool TryAutoTacticalAction()
    {
        if (CharacterCooldownRemaining <= 0f)
        {
            CombatUnit skillTarget = FindAutoSkillTarget();
            bool shouldUse = false;
            switch (role)
            {
                case CombatRole.YuukaTank:
                    shouldUse = unit.HealthRatio <= defensiveHealthThreshold || CountInjuredAllies(defensiveHealthThreshold) >= 2;
                    break;
                case CombatRole.AyaneHealer: shouldUse = skillTarget != null && skillTarget.HealthRatio < healingThreshold; break;
                case CombatRole.MikaSingleTarget: shouldUse = skillTarget != null; break;
                case CombatRole.MomoiLowCostAOE: shouldUse = skillTarget != null && CountEnemiesNear(skillTarget.transform.position, aoeRadius) >= aoeMinimumEnemyCount; break;
                case CombatRole.HinaHighCostAOE:
                    shouldUse = skillTarget != null && (CountEnemiesNear(skillTarget.transform.position, aoeRadius) >= expensiveAoeMinimumEnemyCount || skillTarget.IsBoss);
                    break;
            }
            if (shouldUse && TryCharacterSkill(skillTarget)) { currentState = "Auto skill"; return true; }
        }
        if (currentTarget != null && MovementCooldownRemaining <= 0f &&
            Distance(transform.position, currentTarget.transform.position) > Mathf.Max(9f, unit.AttackRange * 2.2f))
        {
            Vector3 toward = (currentTarget.transform.position - transform.position).normalized;
            if (TryMovementSkill(transform.position + toward * movementRange)) { currentState = "Auto movement skill"; return true; }
        }
        return false;
    }

    private CombatUnit FindAutoSkillTarget()
    {
        CombatUnit best = null;
        float bestScore = float.NegativeInfinity;
        foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (candidate.IsDead) continue;
            bool ally = candidate.team == unit.team;
            if (role == CombatRole.AyaneHealer)
            {
                if (!ally || Distance(transform.position, candidate.transform.position) > skillRange) continue;
                float score = 1f - candidate.HealthRatio;
                if (score > bestScore) { bestScore = score; best = candidate; }
                continue;
            }
            if (ally || Distance(transform.position, candidate.transform.position) > skillRange) continue;
            float scoreValue = candidate.IsBoss ? 10000f : candidate.isElite ? 5000f :
                candidate.maxHealth + candidate.CurrentHealth;
            if (role == CombatRole.MomoiLowCostAOE || role == CombatRole.HinaHighCostAOE)
                scoreValue = CountEnemiesNear(candidate.transform.position, aoeRadius) * 1000f + scoreValue;
            else if (candidate.HealthRatio < 0.15f && !candidate.IsBoss && !candidate.isElite) scoreValue -= 2000f;
            if (scoreValue > bestScore) { bestScore = scoreValue; best = candidate; }
        }
        return best;
    }

    private int CountInjuredAllies(float threshold)
    {
        int count = 0;
        foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            if (!candidate.IsDead && candidate.team == unit.team && candidate.HealthRatio <= threshold) count++;
        return count;
    }

    private int CountEnemiesNear(Vector3 center, float radius)
    {
        int count = 0;
        foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            if (!candidate.IsDead && candidate.team != unit.team && Distance(center, candidate.transform.position) <= radius) count++;
        return count;
    }

    private bool TryEnemyAbility()
    {
        if (Time.time < enemyAbilityReadyAt || currentTarget == null ||
            Distance(transform.position, currentTarget.transform.position) > enemyAbilityRange) return false;
        bool used = ApplyCycledEffect(unit, selfAbilityEffects);
        used |= ApplyCycledEffect(currentTarget, targetAbilityEffects);
        if (allyAbilityEffects != null && allyAbilityEffects.Length > 0)
        {
            int buffed = 0;
            foreach (CombatUnit ally in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            {
                if (ally.IsDead || ally.team != unit.team || Distance(transform.position, ally.transform.position) > enemyAbilityRange) continue;
                if (ApplyCycledEffect(ally, allyAbilityEffects) && ++buffed >= allyBuffMaxTargets) break;
            }
        }
        if (!used) return false;
        enemyAbilityCycle++;
        enemyAbilityReadyAt = Time.time + enemyAbilityCooldown;
        currentState = "Status ability";
        return true;
    }

    private bool ApplyCycledEffect(CombatUnit target, StatusEffectSpec[] effects)
    {
        if (target == null || effects == null || effects.Length == 0) return false;
        StatusEffectSpec effect = effects[enemyAbilityCycle % effects.Length];
        if (!effect.IsValid) return false;
        target.ApplyStatus(effect);
        return true;
    }

    private bool HandleCover()
    {
        if (coverPoint != null)
        {
            if (!coverPoint.TryReserve(unit)) { coverPoint = null; return false; }
            if (Distance(transform.position, coverPoint.transform.position) > 0.4f)
            {
                currentState = "Taking cover"; Navigate(coverPoint.transform.position, 0.15f); Move(movementSpeed, true); return true;
            }
            coverPoint.Occupy(unit);
            if (Distance(transform.position, currentTarget.transform.position) <= unit.AttackRange && HasSight(currentTarget)) return false;
            coverPoint.Abandon(unit); coverPoint = null;
        }
        float targetDistance = Distance(transform.position, currentTarget.transform.position);
        if (targetDistance <= unit.AttackRange) return false;
        Vector3 towardEnemy = (currentTarget.transform.position - transform.position).normalized;
        CoverPoint best = null;
        float bestDistance = coverDetectionRange;
        foreach (CoverPoint candidate in CoverPoint.All)
        {
            if (!candidate.IsAvailable) continue;
            Vector3 toCover = candidate.transform.position - transform.position; toCover.y = 0f;
            float distance = toCover.magnitude;
            if (distance >= bestDistance || Vector3.Dot(towardEnemy, toCover.normalized) < 0.2f) continue;
            if (Distance(candidate.transform.position, currentTarget.transform.position) >= targetDistance) continue;
            best = candidate; bestDistance = distance;
        }
        if (best == null || !best.TryReserve(unit)) return false;
        coverPoint = best; return true;
    }

    private void ReleaseCover()
    {
        if (coverPoint == null) return;
        coverPoint.Release(unit); coverPoint = null;
    }

    private void FindTarget()
    {
        currentTarget = null;
        float best = float.PositiveInfinity;
        foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (candidate == unit || candidate.IsDead || candidate.team == unit.team) continue;
            float distance = Distance(transform.position, candidate.transform.position);
            if (distance < best) { best = distance; currentTarget = candidate; }
        }
    }

    private bool HasSight(CombatUnit target) => !Physics.Linecast(transform.position + Vector3.up,
        target.transform.position + Vector3.up, sightBlockers, QueryTriggerInteraction.Ignore);

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

    private void Move(float speed, bool allowSeparation)
    {
        if (!HasPath()) return;
        Vector3 delta = path.corners[corner] - transform.position; delta.y = 0f;
        if (delta.sqrMagnitude < 0.04f) { corner++; return; }
        Vector3 direction = delta.normalized;
        if (allowSeparation && unit.team == CombatUnit.CombatTeam.Player)
            direction = Vector3.Slerp(direction, (direction + SeparationVector()).normalized, separationStrength);
        Vector3 candidate = transform.position + direction * Mathf.Min(speed * Time.deltaTime, delta.magnitude);
        if (NavMesh.SamplePosition(candidate, out var hit, 0.55f, NavMesh.AllAreas))
            transform.position = hit.position;
        Face(direction);
    }

    private Vector3 SeparationVector()
    {
        Vector3 separation = Vector3.zero;
        foreach (CombatUnit ally in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (ally == unit || ally.IsDead || ally.team != unit.team) continue;
            Vector3 away = transform.position - ally.transform.position; away.y = 0f;
            float distance = away.magnitude;
            if (distance < 0.01f) away = transform.right * (GetInstanceID() % 2 == 0 ? 0.1f : -0.1f);
            if (distance < separationDistance) separation += away.normalized * (1f - distance / separationDistance);
        }
        return Vector3.ClampMagnitude(separation, 1f);
    }

    private void ApplyIdleSeparation()
    {
        Vector3 separation = SeparationVector();
        if (separation.sqrMagnitude < 0.05f) return;
        Vector3 destination = transform.position + separation * separationStrength * Time.deltaTime;
        if (NavMesh.SamplePosition(destination, out var hit, 0.4f, NavMesh.AllAreas)) transform.position = hit.position;
    }

    private void Face(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * Time.deltaTime);
    }
    private void ClearPath() { if (path != null) path.ClearCorners(); corner = 0; }
    private bool HasPath() => path != null && path.corners != null && corner < path.corners.Length;

    public bool TryMovementSkill(Vector3 destination)
    {
        if (unit.IsDead || MovementCooldownRemaining > 0f || !unit.SpendMovementPoints(movementCost)) return false;
        ReleaseCover();
        Vector3 delta = destination - transform.position; delta.y = 0f;
        destination = transform.position + Vector3.ClampMagnitude(delta, movementRange);
        if (!NavMesh.SamplePosition(transform.position, out var start, 2f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(destination, out var hit, 3f, NavMesh.AllAreas))
        { unit.RefundMovementPoints(movementCost); return false; }
        if (movementKind == MovementKind.Flash)
        {
            transform.position = hit.position; ClearPath(); currentState = "Flashed";
            movementReadyAt = Time.time + movementCooldown; return true;
        }
        var movementPath = new NavMeshPath();
        if (!NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, movementPath) ||
            movementPath.status != NavMeshPathStatus.PathComplete || movementPath.corners.Length < 2)
        { unit.RefundMovementPoints(movementCost); return false; }
        path = movementPath; corner = 1; nextPath = Time.time + pathUpdateInterval;
        skillDestination = hit.position; skillSpeed = movementKind == MovementKind.Charge ? 12f : 18f;
        skillMoving = true; currentState = movementKind.ToString(); movementReadyAt = Time.time + movementCooldown;
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
            case CharacterSkillKind.Defensive:
                unit.ApplyStatus(primaryEffect); unit.ApplyStatus(secondaryEffect);
                foreach (CombatUnit ally in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
                    if (ally != unit && ally.team == unit.team && !ally.IsDead && Distance(transform.position, ally.transform.position) <= aoeRadius)
                        ally.ApplyStatus(primaryEffect);
                break;
            case CharacterSkillKind.Burst:
                used = ValidTarget(target, false);
                if (used) { target.TakeDamage(unit.AttackPower * skillPowerMultiplier, transform.position); target.ApplyStatus(primaryEffect); }
                break;
            case CharacterSkillKind.Heal:
                used = ValidTarget(target, true) && target.HealthRatio < 0.999f;
                if (used) { target.Heal(skillHealAmount); target.ApplyStatus(primaryEffect); }
                break;
            case CharacterSkillKind.LowCostAOE:
            case CharacterSkillKind.HighCostAOE:
                used = ValidTarget(target, false);
                if (used)
                    foreach (CombatUnit enemy in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
                        if (!enemy.IsDead && enemy.team != unit.team && Distance(target.transform.position, enemy.transform.position) <= aoeRadius)
                        { enemy.TakeDamage(unit.AttackPower * skillPowerMultiplier, transform.position); enemy.ApplyStatus(primaryEffect); }
                break;
        }
        if (!used) BattleDirector.Instance.RefundUniversal(characterSkillCost);
        else characterSkillReadyAt = Time.time + characterSkillCooldown;
        return used;
    }

    private bool ValidTarget(CombatUnit target, bool ally) => target != null && !target.IsDead &&
        (target.team == unit.team) == ally && Distance(transform.position, target.transform.position) <= skillRange;

    public void ConfigureEnemyAbilities(StatusEffectSpec[] selfEffects, StatusEffectSpec[] targetEffects,
        StatusEffectSpec[] allyEffects, float cooldown)
    {
        selfAbilityEffects = selfEffects; targetAbilityEffects = targetEffects; allyAbilityEffects = allyEffects;
        enemyAbilityCooldown = cooldown; enemyAbilityCycle = 0; enemyAbilityReadyAt = Time.time + cooldown * 0.5f;
    }

    private void OnDisable() { ReleaseCover(); }
    private static float Distance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
}
