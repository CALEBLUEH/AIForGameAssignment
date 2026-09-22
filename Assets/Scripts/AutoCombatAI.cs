using UnityEngine;
using UnityEngine.AI;

// Uses NavMesh.CalculatePath and Transform movement; never requires NavMeshAgent.
[RequireComponent(typeof(CombatUnit))]
public class AutoCombatAI : MonoBehaviour
{
    public enum MovementKind { Charge, Dash, Flash }
    public enum CharacterSkillKind { PowerUp, Burst, Heal, Defensive, LowCostAOE, HighCostAOE }
    public enum CombatRole { Enemy, YuukaTank, AyaneHealer, MikaSingleTarget, MomoiLowCostAOE, HinaHighCostAOE }
    public enum CoverPreference { Never, Low, Normal, High }

    [Header("Identity and movement")]
    public CombatRole role;
    public int rosterOrder;
    public MovementKind movementKind;
    public CharacterSkillKind characterSkillKind;
    public float rotationSpeed = 10f, movementSpeed = 3.5f;
    public float targetSearchInterval = 0.25f, pathUpdateInterval = 0.35f;
    public LayerMask sightBlockers;
    public float movementRange = 7f, movementCost = 35f, movementCooldown = 7f;
    [Header("NavMesh grounding")]
    [Tooltip("Keeps a capsule/model above the NavMesh instead of placing its body centre on the road.")]
    [SerializeField] private bool deriveGroundOffsetFromCollider = true;
    [Min(0f)] [SerializeField] private float groundOffset;
    [Header("Separation")]
    public float separationDistance = 1.35f;
    public float separationStrength = 0.7f;
    [Header("Cover")]
    [Tooltip("How strongly this character prefers usable cover. Yuuka/Tank should normally use Low; backline units can use High.")]
    public CoverPreference coverPreference = CoverPreference.Normal;
    [Tooltip("If enabled, this unit behaves as a frontline unit and will not take ordinary cover unless its health is at or below the threshold below.")]
    public bool frontlineUnit = false;
    [Range(0.05f, 1f)] public float frontlineCoverHealthThreshold = 0.40f;
    public float coverDetectionRange = 12f;
    public float coverRetryDelay = 2f;
    public float coverFiringRangeMultiplier = 1.15f;
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
    [Header("Enemy engagement")]
    [Tooltip("Enemies stay at their spawn/guard position until a player enters this range.")]
    public float enemyDetectionRange = 14f;
    [Tooltip("Maximum distance an enemy may chase away from its spawn position before returning.")]
    public float enemyLeashRange = 22f;
    [Tooltip("How close the enemy must get to its spawn point before it finishes returning.")]
    public float enemyReturnRadius = 0.75f;
    [Tooltip("After detecting a player, keep aggro for this many seconds after they leave detection range, unless the leash is exceeded.")]
    public float enemyAggroMemory = 2f;
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
    private CoverPoint rejectedCoverPoint;
    private float rejectedCoverUntil;
    private Vector3 losRepositionPoint;
    private float losRepositionValidUntil;
    private Vector3 enemySpawnPosition;
    private bool enemyAggro;
    private float enemyLastSeenTime;
    private float runtimeGroundOffset;
    public string CurrentState => currentState;
    public CombatUnit Unit => unit;
    public float MovementCooldownRemaining => Mathf.Max(0f, movementReadyAt - Time.time);
    public float CharacterCooldownRemaining => Mathf.Max(0f, characterSkillReadyAt - Time.time);

    private void Awake()
    {
        unit = GetComponent<CombatUnit>();
        member = GetComponent<SquadMember>();
        path = new NavMeshPath();
        runtimeGroundOffset = Mathf.Max(0f, groundOffset);
        if (deriveGroundOffsetFromCollider && TryGetComponent(out Collider bodyCollider))
            runtimeGroundOffset = Mathf.Max(0f, transform.position.y - bodyCollider.bounds.min.y);

        // Role is authoritative: Yuuka cannot accidentally hide because of Inspector settings.
        if (role == CombatRole.YuukaTank)
        {
            frontlineUnit = true;
            coverPreference = CoverPreference.Never;
        }
    }

    private void Start()
    {
        // Keep the real Transform on the same NavMesh position used by CalculatePath.
        if (NavMesh.SamplePosition(NavMeshProbe(transform.position), out var hit, 2f, NavMesh.AllAreas))
            SetNavMeshPosition(hit.position);
        enemySpawnPosition = transform.position;
    }

    public void ActivateAtSpawn(Vector3 navMeshPosition)
    {
        SetNavMeshPosition(navMeshPosition);
        enemySpawnPosition = transform.position;
        enemyAggro = true;
        currentTarget = null;
        nextSearch = 0f;
        nextPath = 0f;
        ClearPath();
    }

    public void SetNavMeshPosition(Vector3 navMeshPosition)
    {
        navMeshPosition.y += runtimeGroundOffset;
        transform.position = navMeshPosition;
    }

    private Vector3 NavMeshProbe(Vector3 worldPosition)
    {
        worldPosition.y -= runtimeGroundOffset;
        return worldPosition;
    }

    private void Update()
    {
        if (unit.IsDead) { currentState = "Down"; return; }
        if (BattleDirector.Instance != null && !BattleDirector.Instance.IsPlaying) return;
        if (unit.team == CombatUnit.CombatTeam.Enemy && !UpdateEnemyEngagement()) return;
        if (skillMoving)
        {
            Move(skillSpeed, false);
            if (Distance(transform.position, skillDestination) < 0.25f || !HasPath())
            { skillMoving = false; currentState = "Waiting"; }
            return;
        }
        // Auto toggle controls tactical decisions only. Basic combat AI must always run:
        // target acquisition, pursuit/repositioning, separation and basic attacks.
        bool tacticalAuto = unit.team != CombatUnit.CombatTeam.Player || BattleDirector.Instance == null ||
            BattleDirector.Instance.AutoEnabled;
        // Valid cover has higher priority than formation return. The leader radius is a leash,
        // not a command for everyone to stack on the leader's position.
        bool hasReservedCover = coverPoint != null && coverPoint.IsReservedBy(unit);
        if (!hasReservedCover && member != null && member.IsTooFarFromLeader() && Time.time >= nextAttack)
            returning = true;
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
            // Player tactical actions (skills, auto movement, healing/defense decisions)
            // only run while Auto is enabled. Enemy abilities remain automatic.
            if (unit.team == CombatUnit.CombatTeam.Player && tacticalAuto && TryAutoTacticalAction()) return;
            if (unit.team == CombatUnit.CombatTeam.Enemy && TryEnemyAbility()) return;
        }

        // Cover is normal combat positioning, so it works with Auto ON or OFF.
        // Frontline/low-cover units are allowed to keep advancing instead of hiding with the backline.
        if (unit.team == CombatUnit.CombatTeam.Player && ShouldUseCover() && HandleCover()) return;
        if (unit.team == CombatUnit.CombatTeam.Player && !ShouldUseCover() && coverPoint != null) ReleaseCover();
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
            if (!hasSight && IsSightBlockedByBlockingObstacle(transform.position, currentTarget))
            {
                currentState = "Avoiding visual obstacle";
                if (Time.time >= losRepositionValidUntil || !IsGoodFiringPosition(losRepositionPoint, currentTarget))
                {
                    if (TryFindLineOfSightPosition(currentTarget, out var firingPoint))
                    {
                        losRepositionPoint = firingPoint;
                        losRepositionValidUntil = Time.time + 1.25f;
                        ClearPath();
                        nextPath = 0f;
                    }
                    else
                    {
                        // Fall back to normal NavMesh pursuit if no firing point can be found yet.
                        losRepositionPoint = currentTarget.transform.position;
                        losRepositionValidUntil = Time.time + 0.35f;
                    }
                }
                Navigate(losRepositionPoint, 0.2f);
            }
            else
            {
                currentState = inRange ? "Repositioning for line of sight" : "Pursuing";
                Navigate(currentTarget.transform.position, hasSight ? unit.AttackRange * 0.8f : 0.2f);
            }
            Move(movementSpeed, true);
        }
    }

    private bool UpdateEnemyEngagement()
    {
        CombatUnit nearestPlayer = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (CombatUnit candidate in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
        {
            if (candidate == null || candidate.IsDead || candidate.team == unit.team) continue;
            float d = Distance(transform.position, candidate.transform.position);
            if (d < nearestDistance)
            {
                nearestDistance = d;
                nearestPlayer = candidate;
            }
        }

        // Before activation, stay in this wave position.
        if (!enemyAggro)
        {
            if (nearestPlayer == null || nearestDistance > enemyDetectionRange)
            {
                ClearPath();
                currentState = "Guarding";
                return false;
            }

            enemyAggro = true;
        }

        // Once activated, never leash/return to the original area.
        if (nearestPlayer == null)
        {
            currentTarget = null;
            ClearPath();
            currentState = "No target";
            return false;
        }

        currentTarget = nearestPlayer;
        enemyLastSeenTime = Time.time;
        return true;
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
            Distance(transform.position, currentTarget.transform.position) > enemyAbilityRange ||
            !HasSight(currentTarget)) return false;
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


    private bool ShouldUseCover()
    {
        // Yuuka/tank NEVER uses cover. This does not depend on an Inspector checkbox.
        if (role == CombatRole.YuukaTank || frontlineUnit) return false;
        if (coverPreference == CoverPreference.Never) return false;

        switch (coverPreference)
        {
            case CoverPreference.Low:
                return unit.HealthRatio <= Mathf.Max(frontlineCoverHealthThreshold, 0.55f);
            case CoverPreference.High:
            case CoverPreference.Normal:
                return true;
            default:
                return false;
        }
    }

    private bool HandleCover()
    {
        if (currentTarget == null) return false;

        float coverAttackRange = unit.AttackRange * Mathf.Max(1f, coverFiringRangeMultiplier);

        // Already reserved/using cover.
        if (coverPoint != null)
        {
            if (!coverPoint.TryReserve(unit))
            {
                coverPoint = null;
                return false;
            }

            // Re-check the EXACT cover position every frame. If the enemy moved too far away,
            // abandon/reject this point BEFORE normal pursuit is allowed to run.
            float enemyDistanceFromCover = Distance(coverPoint.transform.position, currentTarget.transform.position);
            if (enemyDistanceFromCover > coverAttackRange ||
                !coverPoint.ProtectsFrom(currentTarget.transform.position) ||
                !HasSightFromCover(coverPoint.transform.position, currentTarget, coverPoint.coverCollider))
            {
                RejectCurrentCover();
                currentState = "Leaving invalid cover";
                return false;
            }

            if (Distance(transform.position, coverPoint.transform.position) > coverPoint.occupancyRadius)
            {
                currentState = "Taking cover";
                Navigate(coverPoint.transform.position, Mathf.Max(0.1f, coverPoint.occupancyRadius * 0.45f));
                Move(movementSpeed, true);
                return true;
            }

            // COMMIT to this fighting position. Do not fall through into normal Pursuing logic.
            coverPoint.Occupy(unit);
            ClearPath();
            ApplyIdleSeparation();
            Face(currentTarget.transform.position - transform.position);
            currentState = "Fighting from cover";

            if (Time.time >= nextAttack)
            {
                bool finisher = actionIndex >= basicAttacksBeforeFinisher;
                currentTarget.TakeDamage(unit.AttackPower * (finisher ? finisherMultiplier : 1f), transform.position);
                actionIndex = finisher ? 0 : actionIndex + 1;
                currentState = finisher ? "Cover finisher" : "Cover attack";
                nextAttack = Time.time + 1f / Mathf.Max(0.1f, unit.AttackSpeed);
            }

            return true;
        }

        CoverPoint best = null;
        float bestScore = float.PositiveInfinity;
        float currentEnemyDistance = Distance(transform.position, currentTarget.transform.position);

        foreach (CoverPoint candidate in CoverPoint.All)
        {
            if (candidate == null) continue;
            if (!candidate.IsAvailable && !candidate.IsReservedBy(unit)) continue;
            if (candidate == rejectedCoverPoint && Time.time < rejectedCoverUntil) continue;

            Vector3 candidatePos = candidate.transform.position;
            float travelDistance = Distance(transform.position, candidatePos);
            if (travelDistance > coverDetectionRange) continue;

            // Collider must be assigned. It proves this point is actually behind the intended cover.
            if (candidate.coverCollider == null) continue;
            if (!candidate.ProtectsFrom(currentTarget.transform.position)) continue;

            // IMPORTANT: ignore ONLY this candidate's own cover collider for outgoing fire.
            if (!HasSightFromCover(candidatePos, currentTarget, candidate.coverCollider)) continue;

            // The SAME range is used when selecting and when fighting from cover.
            float enemyDistance = Distance(candidatePos, currentTarget.transform.position);
            if (enemyDistance > coverAttackRange) continue;

            float firingPenalty = Mathf.Abs(enemyDistance - unit.AttackRange * 0.80f) * 0.35f;
            float tieBreaker = Mathf.Abs((candidate.GetInstanceID() * 0.001f + rosterOrder * 0.173f) % 0.17f);
            float preferenceMultiplier = coverPreference == CoverPreference.High ? 0.65f :
                                         coverPreference == CoverPreference.Low ? 1.5f : 1f;

            // Prefer nearby usable cover. No "retreat bonus" that encourages unnecessarily
            // far-away cover behind the squad.
            float score = travelDistance * preferenceMultiplier + firingPenalty + tieBreaker;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best == null || !best.TryReserve(unit)) return false;

        coverPoint = best;
        returning = false;
        currentState = "Cover reserved";
        return true;
    }

    private void ReleaseCover()
    {
        if (coverPoint == null) return;
        coverPoint.Release(unit); coverPoint = null;
    }

    private void RejectCurrentCover()
    {
        if (coverPoint == null) return;
        rejectedCoverPoint = coverPoint;
        rejectedCoverUntil = Time.time + Mathf.Max(0.1f, coverRetryDelay);
        coverPoint.Abandon(unit);
        coverPoint = null;
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

    private bool HasSight(CombatUnit target) => HasSightFrom(transform.position, target);

    private bool HasSightFrom(Vector3 origin, CombatUnit target)
    {
        if (target == null) return false;

        const float sightHeight = 1.5f;
        Vector3 from = origin + Vector3.up * sightHeight;
        Vector3 to = target.transform.position + Vector3.up * sightHeight;
        Vector3 dir = to - from;
        float distance = dir.magnitude;
        if (distance <= 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(
            from, dir.normalized, distance, ~0, QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        CoverPoint targetCover = target.ActiveCoverPoint;

        // IMPORTANT: occupied tactical cover is authoritative.
        // If this CoverPoint says its barrier is between this attacker and the target,
        // the attacker has NO line of sight. This avoids large bosses effectively
        // seeing/shooting over the cover because of model size or ray height.
        if (targetCover != null &&
            targetCover.coverCollider != null &&
            targetCover.ProtectsFrom(origin))
        {
            return false;
        }

        foreach (var hit in hits)
        {
            // Ignore the attacker's own colliders.
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            // If the target is occupying cover, its assigned wall must block incoming LOS.
            // This is checked BEFORE accepting the target collider.
            if (targetCover != null && targetCover.coverCollider != null)
            {
                Collider wall = targetCover.coverCollider;
                if (hit.collider == wall || hit.collider.transform.IsChildOf(wall.transform))
                    return false;
            }

            // We reached the target without a blocking wall.
            if (hit.transform == target.transform || hit.transform.IsChildOf(target.transform))
                return true;

            BlockingObstacle blocker = hit.collider.GetComponentInParent<BlockingObstacle>();
            if (blocker != null && blocker.blocksLineOfSight)
                return false;

            if (((1 << hit.collider.gameObject.layer) & sightBlockers.value) != 0)
                return false;
        }

        return true;
    }

    private bool HasSightFromCover(Vector3 origin, CombatUnit target, Collider ownCoverCollider)
    {
        if (target == null) return false;

        const float sightHeight = 1.5f;
        Vector3 from = origin + Vector3.up * sightHeight;
        Vector3 to = target.transform.position + Vector3.up * sightHeight;
        Vector3 dir = to - from;
        float distance = dir.magnitude;
        if (distance <= 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(from, dir.normalized, distance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (hit.transform == target.transform || hit.transform.IsChildOf(target.transform)) return true;

            // The wall protecting THIS CoverPoint must not block the user's outgoing attack.
            if (ownCoverCollider != null &&
                (hit.collider == ownCoverCollider || hit.collider.transform.IsChildOf(ownCoverCollider.transform)))
                continue;

            BlockingObstacle blocker = hit.collider.GetComponentInParent<BlockingObstacle>();
            if (blocker != null && blocker.blocksLineOfSight) return false;

            if (((1 << hit.collider.gameObject.layer) & sightBlockers.value) != 0) return false;
        }

        return true;
    }

    private bool IsSightBlockedByBlockingObstacle(Vector3 origin, CombatUnit target)
    {
        if (target == null) return false;
        const float sightHeight = 1.5f;
        Vector3 from = origin + Vector3.up * sightHeight;
        Vector3 to = target.transform.position + Vector3.up * sightHeight;
        Vector3 dir = to - from;
        foreach (var hit in Physics.RaycastAll(from, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<CoverPoint>() != null) continue;
            BlockingObstacle blocker = hit.collider.GetComponentInParent<BlockingObstacle>();
            if (blocker != null && blocker.blocksLineOfSight) return true;
        }
        return false;
    }

    private bool IsGoodFiringPosition(Vector3 point, CombatUnit target)
    {
        if (target == null) return false;
        if (Distance(point, target.transform.position) > unit.AttackRange * 0.95f) return false;
        if (!NavMesh.SamplePosition(point, out var navHit, 0.5f, NavMesh.AllAreas)) return false;
        return HasSightFrom(navHit.position, target);
    }

    private bool TryFindLineOfSightPosition(CombatUnit target, out Vector3 bestPoint)
    {
        bestPoint = transform.position;
        if (target == null) return false;
        float bestScore = float.PositiveInfinity;
        float radius = Mathf.Max(1.25f, unit.AttackRange * 0.82f);
        NavMeshPath testPath = new NavMeshPath();
        if (!NavMesh.SamplePosition(NavMeshProbe(transform.position), out var start, 1.5f, NavMesh.AllAreas)) return false;

        // Search around the enemy for a reachable firing position with clear LOS.
        const int samples = 16;
        for (int i = 0; i < samples; i++)
        {
            float angle = i * (360f / samples) * Mathf.Deg2Rad;
            Vector3 raw = target.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (!NavMesh.SamplePosition(raw, out var candidate, 1.2f, NavMesh.AllAreas)) continue;
            if (!HasSightFrom(candidate.position, target)) continue;
            if (!NavMesh.CalculatePath(start.position, candidate.position, NavMesh.AllAreas, testPath) ||
                testPath.status != NavMeshPathStatus.PathComplete) continue;

            float score = Distance(transform.position, candidate.position);
            if (score < bestScore)
            {
                bestScore = score;
                bestPoint = candidate.position;
            }
        }
        return bestScore < float.PositiveInfinity;
    }

    private void Navigate(Vector3 destination, float stopDistance)
    {
        if (Distance(transform.position, destination) <= stopDistance) { ClearPath(); return; }
        if (Time.time < nextPath && HasPath()) return;
        nextPath = Time.time + pathUpdateInterval;
        if (!NavMesh.SamplePosition(NavMeshProbe(transform.position), out var start, 2f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(destination, out var end, 2f, NavMesh.AllAreas) ||
            !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete)
        { ClearPath(); currentState = "No path"; return; }
        corner = path.corners.Length > 1 ? 1 : 0;
    }

    private void Move(float speed, bool allowSeparation)
    {
        if (!HasPath()) return;
        while (HasPath())
        {
            Vector3 check = path.corners[corner] - transform.position; check.y = 0f;
            if (check.sqrMagnitude >= 0.04f) break;
            corner++;
        }
        if (!HasPath()) return;

        Vector3 delta = path.corners[corner] - transform.position; delta.y = 0f;
        Vector3 forward = delta.normalized;
        Vector3 direction = forward;

        if (allowSeparation)
        {
            Vector3 separation = SeparationVector();
            // Only use the sideways part. Separation may create spacing but may not
            // push a unit backwards against its NavMesh path.
            Vector3 sideways = separation - Vector3.Project(separation, forward);
            direction = (forward + sideways * Mathf.Clamp01(separationStrength)).normalized;
        }

        float step = Mathf.Min(speed * Time.deltaTime, delta.magnitude);
        Vector3 candidate = transform.position + direction * step;
        if (NavMesh.SamplePosition(NavMeshProbe(candidate), out var hit, 0.45f, NavMesh.AllAreas))
            SetNavMeshPosition(hit.position);
        else
        {
            // Narrow passage fallback: ignore separation and preserve forward progress.
            candidate = transform.position + forward * step;
            if (NavMesh.SamplePosition(NavMeshProbe(candidate), out hit, 0.65f, NavMesh.AllAreas))
                SetNavMeshPosition(hit.position);
            else
                nextPath = 0f;
        }
        Face(forward);
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
        if (NavMesh.SamplePosition(NavMeshProbe(destination), out var hit, 0.4f, NavMesh.AllAreas))
            SetNavMeshPosition(hit.position);
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
        if (!NavMesh.SamplePosition(NavMeshProbe(transform.position), out var start, 2f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(destination, out var hit, 3f, NavMesh.AllAreas))
        { unit.RefundMovementPoints(movementCost); return false; }
        if (movementKind == MovementKind.Flash)
        {
            SetNavMeshPosition(hit.position); ClearPath(); currentState = "Flashed";
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
