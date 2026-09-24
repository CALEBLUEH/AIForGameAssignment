using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Uses NavMesh.CalculatePath and Transform movement; never requires NavMeshAgent.
[RequireComponent(typeof(CombatUnit))]
public class AutoCombatAI : MonoBehaviour
{
    private const float DefaultClosestTargetRefreshInterval = 5f;
    private const float DefaultKnockbackDuration = 0.75f;
    private const float DefaultKnockbackDistance = 2.25f;
    private const float DefaultKnockbackSpeedMultiplier = 1.45f;
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
    [Header("Dash")]
    [Min(0.05f)] public float dashProbeRadius = 0.3f;
    [Min(0.05f)] public float dashStopPadding = 0.45f;
    [Min(0.1f)] public float dashGroundSampleRadius = 0.8f;
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
    [Tooltip("Enemy-only permission. Keep disabled on every enemy except Sensei. Player characters ignore this field.")]
    public bool enemyCanUseCover;
    [Tooltip("If enabled, this unit behaves as a frontline unit and will not take ordinary cover unless its health is at or below the threshold below.")]
    public bool frontlineUnit = false;
    [Range(0.05f, 1f)] public float frontlineCoverHealthThreshold = 0.40f;
    public float coverDetectionRange = 12f;
    public float coverRetryDelay = 2f;
    public float coverFiringRangeMultiplier = 1.15f;
    [Min(1f)] public float coverMinimumStayDuration = 1f;
    [Min(1f)] public float coverApproachTimeout = 8f;
    [Header("Character skill balance")]
    public float characterSkillCost = 40f, characterSkillCooldown = 10f, skillRange = 6f;
    public float skillPowerMultiplier = 2.5f, skillHealAmount = 35f, aoeRadius = 4f;
    [TextArea(2, 4)] public string skillDescription;
    [Tooltip("Maximum pointer distance from Mika's enemy target.")]
    [Min(0.1f)] public float targetSnapRadius = 2.2f;
    [Range(5f, 170f)] public float coneAngle = 55f;
    [Min(1)] public int damageTickCount = 6;
    [Min(0f)] public float damageTickDuration = 2f;
    [Min(0f)] public float shieldAmount = 80f;
    [Min(0.1f)] public float shieldDuration = 8f;
    [Min(0.1f)] public float shieldVisualRadius = 1.35f;
    public Material shieldMaterial;
    [Header("Character skill timing and effects")]
    [Min(0f)] public float skillWindup = 0.45f;
    [Min(0f)] public float skillRecovery = 0.35f;
    [Min(1f)] public float skillProjectileSpeed = 28f;
    [Min(0.05f)] public float skillProjectileSize = 0.32f;
    [Min(1)] public int coneProjectilesPerTick = 5;
    [Min(0.1f)] public float skillDropHeight = 9f;
    [Min(0.05f)] public float skillDropDuration = 0.7f;
    public GameObject medKitPrefab;
    public StatusEffectSpec primaryEffect;
    public StatusEffectSpec secondaryEffect;
    [Header("Auto decision thresholds")]
    [Range(0.05f, 1f)] public float defensiveHealthThreshold = 0.55f;
    [Range(0.05f, 1f)] public float healingThreshold = 0.72f;
    public int aoeMinimumEnemyCount = 2;
    public int expensiveAoeMinimumEnemyCount = 3;
    public float autoDecisionInterval = 0.4f;
    [Range(0.5f, 1f)] public float autoMovementAttackRangeFraction = 0.92f;
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
    [Header("Target refresh and knockback")]
    [Min(0.1f)] [SerializeField] private float closestTargetRefreshInterval = 5f;
    [Min(0.05f)] [SerializeField] private float knockbackDuration = 0.75f;
    [Min(0.1f)] [SerializeField] private float knockbackDistance = 2.25f;
    [Min(0.1f)] [SerializeField] private float knockbackSpeedMultiplier = 1.45f;

    [SerializeField] private CombatUnit currentTarget;
    [SerializeField] private string currentState = "Waiting";
    private CombatUnit unit;
    private SquadMember member;
    private NavMeshPath path;
    private int corner, actionIndex, enemyAbilityCycle;
    private float nextAttack, nextSearch, nextPath, nextAutoDecision, nextClosestTargetRefresh;
    private float knockbackUntil;
    private Vector3 knockbackDestination;
    private float movementReadyAt, characterSkillReadyAt, enemyAbilityReadyAt;
    private bool returning, skillMoving, skillCasting;
    private Vector3 skillDestination;
    private float skillSpeed;
    private CoverPoint coverPoint;
    private EnemyFormationMember formationMember;
    private CoverPoint rejectedCoverPoint;
    private float rejectedCoverUntil;
    private float coverReservedAt, coverReachedAt;
    private bool coverOccupied;
    private Vector3 losRepositionPoint;
    private float losRepositionValidUntil;
    private Vector3 enemySpawnPosition;
    private bool enemyAggro;
    private float enemyLastSeenTime;
    private float runtimeGroundOffset;
    public string CurrentState => currentState;
    public CombatUnit Unit => unit;
    public CombatUnit CurrentTarget => currentTarget;
    public Vector3 NavMeshWorldPosition => NavMeshProbe(transform.position);
    public float MovementCooldownRemaining => Mathf.Max(0f, movementReadyAt - Time.time);
    public float CharacterCooldownRemaining => Mathf.Max(0f, characterSkillReadyAt - Time.time);
    public bool IsCastingSkill => skillCasting;
    public bool IsDashing => skillMoving;
    public bool IsKnockedBack => Time.time < knockbackUntil;
    public CoverPoint ReservedCover => coverPoint;
    public bool IsOccupyingCover => coverPoint != null && coverOccupied;
    public float CoverOccupiedDuration => IsOccupyingCover ? Mathf.Max(0f, Time.time - coverReachedAt) : 0f;
    public string SkillDescription => string.IsNullOrWhiteSpace(skillDescription) ? DefaultSkillDescription() : skillDescription;
    public event System.Action AttackPerformed;

    private void Awake()
    {
        unit = GetComponent<CombatUnit>();
        member = GetComponent<SquadMember>();
        path = new NavMeshPath();
        // Older prefabs/scenes may deserialize newly introduced fields as zero.
        // Upgrade only invalid values and preserve every authored positive value.
        if (closestTargetRefreshInterval <= 0f) closestTargetRefreshInterval = DefaultClosestTargetRefreshInterval;
        if (knockbackDuration <= 0f) knockbackDuration = DefaultKnockbackDuration;
        if (knockbackDistance <= 0f) knockbackDistance = DefaultKnockbackDistance;
        if (knockbackSpeedMultiplier <= 0f) knockbackSpeedMultiplier = DefaultKnockbackSpeedMultiplier;
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
        nextClosestTargetRefresh = 0f;
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
        if (UpdateKnockback()) return;
        if (skillCasting)
        {
            ClearPath();
            return;
        }
        if (unit.team == CombatUnit.CombatTeam.Enemy && !UpdateEnemyEngagement()) return;
        if (skillMoving)
        {
            UpdateDash();
            return;
        }
        // Auto toggle controls tactical decisions only. Basic combat AI must always run:
        // target acquisition, pursuit/repositioning, separation and basic attacks.
        bool tacticalAuto = unit.team != CombatUnit.CombatTeam.Player || BattleDirector.Instance == null ||
            BattleDirector.Instance.AutoEnabled;
        // Proposal hierarchy: finish the current attack section, then return to the
        // squad before considering or keeping a cover reservation.
        if (member != null && member.IsTooFarFromLeader() && Time.time >= nextAttack)
            returning = true;
        if (returning && member != null && !member.HasReturnedToLeader())
        {
            ReleaseCover();
            currentState = "Returning";
            Navigate(member.leader.position, member.returnDistance);
            Move(movementSpeed, true);
            return;
        }
        returning = false;
        bool targetMissing = currentTarget == null || currentTarget.IsDead;
        if ((targetMissing && Time.time >= nextSearch) || Time.time >= nextClosestTargetRefresh)
        {
            FindTarget();
            nextSearch = Time.time + targetSearchInterval;
            nextClosestTargetRefresh = Time.time + closestTargetRefreshInterval;
        }
        if (currentTarget == null)
        {
            if (coverPoint != null && HandleCover()) return;
            ReleaseCover(); ClearPath(); ApplyIdleSeparation(); currentState = "Waiting"; return;
        }
        if (Time.time >= nextAutoDecision)
        {
            nextAutoDecision = Time.time + autoDecisionInterval;
            // Player tactical actions (skills, auto movement, healing/defense decisions)
            // only run while Auto is enabled. Enemy abilities remain automatic.
            if (unit.team == CombatUnit.CombatTeam.Player && tacticalAuto && TryAutoTacticalAction()) return;
            if (unit.team == CombatUnit.CombatTeam.Enemy && TryEnemyAbility()) return;
        }

        // Cover is normal combat positioning for either team and has higher priority
        // than enemy formation movement. Frontline/low-cover units may still advance.
        if (ShouldUseCover() && HandleCover()) return;
        if (!ShouldUseCover() && coverPoint != null) ReleaseCover();
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
                AttackPerformed?.Invoke();
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
                Vector3 destination = GetNavigationPosition(currentTarget);
                float stopDistance = hasSight ? unit.AttackRange * 0.8f : 0.2f;
                if (unit.team == CombatUnit.CombatTeam.Enemy && TryGetFormationDestination(out Vector3 formationDestination))
                {
                    destination = formationDestination;
                    stopDistance = formationMember.PositionTolerance;
                    currentState = "Holding formation";
                }
                else currentState = inRange ? "Repositioning for line of sight" : "Pursuing";
                Navigate(destination, stopDistance);
            }
            Move(movementSpeed, true);
        }
    }

    private bool UpdateEnemyEngagement()
    {
        // Before activation, stay in this wave position.
        if (!enemyAggro)
        {
            if (Time.time >= nextSearch || currentTarget == null || currentTarget.IsDead)
            {
                FindTarget();
                nextSearch = Time.time + targetSearchInterval;
            }
            if (currentTarget == null || Distance(transform.position, currentTarget.transform.position) > enemyDetectionRange)
            {
                currentTarget = null;
                ClearPath();
                currentState = "Guarding";
                return false;
            }

            enemyAggro = true;
            nextClosestTargetRefresh = Time.time + closestTargetRefreshInterval;
        }

        // Once activated, never leash/return to the original area.
        if (currentTarget == null || currentTarget.IsDead)
        {
            FindTarget();
            nextClosestTargetRefresh = Time.time + closestTargetRefreshInterval;
        }

        if (currentTarget == null)
        {
            ClearPath();
            currentState = "No target";
            return false;
        }
        enemyLastSeenTime = Time.time;
        return true;
    }

    public void TriggerKnockback(Vector3 source)
    {
        if (unit == null || unit.IsDead) return;
        Vector3 away = NavMeshWorldPosition - source;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;

        Vector3 requested = NavMeshWorldPosition + away.normalized * knockbackDistance;
        if (!TryResolveReachablePoint(requested, out knockbackDestination))
        {
            // A shorter retreat is preferable to teleporting or leaving the NavMesh.
            requested = NavMeshWorldPosition + away.normalized * (knockbackDistance * 0.5f);
            if (!TryResolveReachablePoint(requested, out knockbackDestination)) return;
        }

        ReleaseCover();
        skillMoving = false;
        ClearPath();
        nextPath = 0f;
        knockbackUntil = Time.time + knockbackDuration;
        nextAttack = Mathf.Max(nextAttack, knockbackUntil);
        currentState = "Fleeing from knockback";
    }

    private bool UpdateKnockback()
    {
        if (!IsKnockedBack) return false;
        currentState = "Fleeing from knockback";
        Navigate(knockbackDestination, 0.08f);
        Move(movementSpeed * knockbackSpeedMultiplier, false);
        return true;
    }

    private bool TryResolveReachablePoint(Vector3 requested, out Vector3 destination)
    {
        destination = NavMeshWorldPosition;
        if (!NavMesh.SamplePosition(NavMeshProbe(transform.position), out NavMeshHit start, 2f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(requested, out NavMeshHit end, 2f, NavMesh.AllAreas)) return false;
        var retreatPath = new NavMeshPath();
        if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, retreatPath) ||
            retreatPath.status != NavMeshPathStatus.PathComplete) return false;
        destination = end.position;
        return true;
    }

    private bool TryAutoTacticalAction()
    {
        if (CharacterCooldownRemaining <= 0f)
        {
            bool queueAllowsSkill = BattleDirector.Instance == null ||
                BattleDirector.Instance.CanUseQueuedCharacterSkill(this);
            if (queueAllowsSkill)
            {
                CombatUnit skillTarget = FindAutoSkillTarget();
                bool shouldUse = false;
                switch (role)
                {
                    case CombatRole.YuukaTank: shouldUse = skillTarget != null; break;
                    case CombatRole.AyaneHealer: shouldUse = skillTarget != null && skillTarget.HealthRatio < 0.999f; break;
                    case CombatRole.MikaSingleTarget: shouldUse = skillTarget != null; break;
                    case CombatRole.MomoiLowCostAOE: shouldUse = skillTarget != null; break;
                    case CombatRole.HinaHighCostAOE: shouldUse = skillTarget != null; break;
                }
                if (shouldUse && TryCharacterSkill(skillTarget)) { currentState = "Auto skill"; return true; }
            }
        }
        if (TryGetAutoMovementDestination(currentTarget, out Vector3 destination))
        {
            if (TryMovementSkill(destination)) { currentState = "Auto movement to attack range"; return true; }
        }
        return false;
    }

    private bool TryGetAutoMovementDestination(CombatUnit target, out Vector3 destination)
    {
        destination = NavMeshWorldPosition;
        if (target == null || target.IsDead || MovementCooldownRemaining > 0f ||
            !unit.IsMovementGaugeFull) return false;

        Vector3 toward = target.transform.position - destination;
        toward.y = 0f;
        float distance = toward.magnitude;
        float preferredRange = Mathf.Max(0.5f, unit.AttackRange * autoMovementAttackRangeFraction);
        if (distance <= preferredRange + 0.2f || toward.sqrMagnitude <= 0.001f) return false;
        float travel = Mathf.Min(movementRange, distance - preferredRange);
        destination += toward.normalized * travel;
        return true;
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
                if (candidate.HealthRatio >= 0.999f) continue;
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
        if (unit.team == CombatUnit.CombatTeam.Enemy && !enemyCanUseCover) return false;
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
        float coverAttackRange = unit.AttackRange * Mathf.Max(1f, coverFiringRangeMultiplier);

        // Already reserved/using cover.
        if (coverPoint != null)
        {
            if (!coverPoint.CanBeUsedBy(unit) || !coverPoint.TryReserve(unit))
            {
                coverPoint = null;
                return false;
            }

            if (!coverPoint.TryGetSafeStandPosition(unit, out Vector3 standPosition))
            {
                RejectCurrentCover();
                currentState = "Leaving unreachable cover";
                return false;
            }

            if (Distance(transform.position, standPosition) > coverPoint.occupancyRadius)
            {
                if (Time.time - coverReservedAt >= Mathf.Max(1f, coverApproachTimeout))
                {
                    RejectCurrentCover();
                    currentState = "Cover approach timed out";
                    return false;
                }
                currentState = "Taking cover";
                Navigate(standPosition, Mathf.Max(0.1f, coverPoint.occupancyRadius * 0.45f));
                Move(movementSpeed, true);
                return true;
            }

            if (!coverOccupied)
            {
                coverOccupied = true;
                coverReachedAt = Time.time;
                coverPoint.Occupy(unit);
                ClearPath();
            }

            bool minimumStayComplete = Time.time - coverReachedAt >= Mathf.Max(1f, coverMinimumStayDuration);
            if (!minimumStayComplete)
            {
                ClearPath();
                if (currentTarget != null) Face(currentTarget.transform.position - transform.position);
                currentState = "Occupying cover";
                return true;
            }

            // The proposal treats a reached obstacle with no valid target in range as used.
            // Abandoning the obstacle is shared by this team, preventing repeated useless visits.
            float enemyDistanceFromCover = currentTarget == null ? float.PositiveInfinity :
                Distance(standPosition, currentTarget.transform.position);
            if (currentTarget == null || enemyDistanceFromCover > coverAttackRange ||
                !HasSightFromCover(standPosition, currentTarget, coverPoint.coverCollider))
            {
                CoverPoint abandoned = coverPoint;
                coverPoint = null;
                abandoned.Abandon(unit);
                currentState = "Leaving abandoned cover";
                return false;
            }

            // COMMIT to this fighting position. Do not fall through into normal Pursuing logic.
            ClearPath();
            Face(currentTarget.transform.position - transform.position);
            currentState = "Fighting from cover";

            if (Time.time >= nextAttack)
            {
                bool finisher = actionIndex >= basicAttacksBeforeFinisher;
                currentTarget.TakeDamage(unit.AttackPower * (finisher ? finisherMultiplier : 1f), transform.position);
                AttackPerformed?.Invoke();
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
            if (!candidate.CanBeUsedBy(unit)) continue;
            if (candidate == rejectedCoverPoint && Time.time < rejectedCoverUntil) continue;

            Vector3 candidatePos = candidate.transform.position;
            float travelDistance = Distance(transform.position, candidatePos);
            if (travelDistance > coverDetectionRange) continue;

            // Collider must be assigned. It proves this point is actually behind the intended cover.
            if (candidate.coverCollider == null) continue;
            if (!candidate.ProtectsFrom(currentTarget.transform.position)) continue;

            // IMPORTANT: ignore ONLY this candidate's own cover collider for outgoing fire.
            if (!HasSightFromCover(candidatePos, currentTarget, candidate.coverCollider)) continue;

            float enemyDistance = Distance(candidatePos, currentTarget.transform.position);
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
        coverReservedAt = Time.time;
        coverReachedAt = 0f;
        coverOccupied = false;
        returning = false;
        currentState = "Cover reserved";
        return true;
    }

    private void ReleaseCover()
    {
        if (coverPoint == null) return;
        coverPoint.Release(unit); coverPoint = null;
        coverReservedAt = coverReachedAt = 0f;
        coverOccupied = false;
    }

    private void RejectCurrentCover()
    {
        if (coverPoint == null) return;
        rejectedCoverPoint = coverPoint;
        rejectedCoverUntil = Time.time + Mathf.Max(0.1f, coverRetryDelay);
        coverPoint.Abandon(unit);
        coverPoint = null;
        coverReservedAt = coverReachedAt = 0f;
        coverOccupied = false;
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

    private bool TryGetFormationDestination(out Vector3 destination)
    {
        if (formationMember == null) formationMember = GetComponent<EnemyFormationMember>();
        if (formationMember != null && currentTarget != null)
        {
            Vector3 targetGround = GetNavigationPosition(currentTarget);
            if (formationMember.TryGetDestination(targetGround, out destination) &&
                NavMesh.SamplePosition(NavMeshProbe(transform.position), out NavMeshHit start, 2f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(destination, out NavMeshHit end, 3.5f, NavMesh.AllAreas))
            {
                var formationPath = new NavMeshPath();
                if (NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, formationPath) &&
                    formationPath.status == NavMeshPathStatus.PathComplete)
                {
                    destination = end.position;
                    return true;
                }
            }
        }
        destination = default;
        return false;
    }

    private static Vector3 GetNavigationPosition(CombatUnit target)
    {
        if (target == null) return default;
        AutoCombatAI navigation = target.GetComponent<AutoCombatAI>();
        return navigation == null ? target.transform.position : navigation.NavMeshWorldPosition;
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

        // An occupied tactical cover point remains attackable: the shot is aimed at
        // the protected unit, then CombatUnit routes that damage into the cover HP.
        if (targetCover != null &&
            targetCover.coverCollider != null &&
            targetCover.ProtectsFrom(origin))
        {
            return true;
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
        if (!NavMesh.SamplePosition(NavMeshProbe(transform.position), out var start, 2f, NavMesh.AllAreas))
        { ClearPath(); currentState = "No path (start)"; return; }
        if (!NavMesh.SamplePosition(destination, out var end, 3.5f, NavMesh.AllAreas))
        { ClearPath(); currentState = "No path (destination)"; return; }
        if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path))
        { ClearPath(); currentState = "No path (calculation)"; return; }
        if (path.status != NavMeshPathStatus.PathComplete)
        { ClearPath(); currentState = "No path (incomplete)"; return; }
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
        if (unit.IsDead || skillMoving || MovementCooldownRemaining > 0f || !unit.IsMovementGaugeFull) return false;
        float staminaCost = unit.MovementPointCapacity;
        if (!unit.SpendMovementPoints(staminaCost)) return false;
        ReleaseCover();
        Vector3 resolved = ResolveDashDestination(destination);
        if (Distance(NavMeshWorldPosition, resolved) < 0.2f)
        { unit.RefundMovementPoints(staminaCost); return false; }
        ClearPath();
        skillDestination = resolved;
        skillSpeed = 18f;
        skillMoving = true;
        currentState = "Dash";
        movementReadyAt = Time.time + movementCooldown;
        return true;
    }

    public Vector3 ResolveDashDestination(Vector3 requestedDestination)
    {
        Vector3 origin = NavMeshWorldPosition;
        Vector3 delta = requestedDestination - origin;
        delta.y = 0f;
        float distance = Mathf.Min(delta.magnitude, Mathf.Max(0.1f, movementRange));
        if (distance < 0.01f) return origin;
        Vector3 direction = delta / delta.magnitude;

        float allowedDistance = distance;
        Vector3 castOrigin = origin + Vector3.up * Mathf.Max(0.35f, dashProbeRadius);
        RaycastHit[] hits = Physics.SphereCastAll(castOrigin, dashProbeRadius, direction,
            distance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            bool isBlocker = hit.collider.GetComponentInParent<BlockingObstacle>() != null ||
                hit.collider.GetComponentInParent<TacticalCoverObstacle>() != null ||
                hit.collider.GetComponentInParent<DestructibleCover>() != null;
            if (!isBlocker) continue;
            allowedDistance = Mathf.Max(0f, hit.distance - dashStopPadding);
            break;
        }

        Vector3 desired = origin + direction * allowedDistance;
        if (NavMesh.SamplePosition(desired, out NavMeshHit hitPoint,
                Mathf.Max(0.2f, dashGroundSampleRadius), NavMesh.AllAreas))
            return hitPoint.position;
        return origin;
    }

    private void UpdateDash()
    {
        Vector3 groundPosition = NavMeshWorldPosition;
        Vector3 delta = skillDestination - groundPosition;
        delta.y = 0f;
        float remaining = delta.magnitude;
        if (remaining <= 0.12f)
        {
            FinishDash();
            return;
        }

        Vector3 direction = delta / remaining;
        float step = Mathf.Min(remaining, skillSpeed * Time.deltaTime);
        Vector3 candidate = groundPosition + direction * step;
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                Mathf.Max(0.2f, dashGroundSampleRadius), NavMesh.AllAreas))
        {
            FinishDash();
            return;
        }
        Vector3 lateral = hit.position - candidate;
        lateral.y = 0f;
        if (lateral.magnitude > dashGroundSampleRadius)
        {
            FinishDash();
            return;
        }
        SetNavMeshPosition(hit.position);
        Face(direction);
        if (step >= remaining - 0.01f) FinishDash();
    }

    private void FinishDash()
    {
        skillMoving = false;
        currentState = "Waiting";
        ClearPath();
    }

    public bool TryCharacterSkill(CombatUnit target)
    {
        Vector3 point = target == null ? transform.position : target.transform.position;
        return TryCharacterSkillAt(point, target);
    }

    public bool TryCharacterSkillAt(Vector3 point, CombatUnit target = null)
    {
        if (unit.IsDead || CharacterCooldownRemaining > 0f || BattleDirector.Instance == null) return false;
        Vector3 origin = NavMeshWorldPosition;
        Vector3 direction = point - origin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
        direction.Normalize();

        bool valid;
        switch (role)
        {
            case CombatRole.MikaSingleTarget:
                valid = ValidTarget(target, false);
                break;
            case CombatRole.MomoiLowCostAOE:
            case CombatRole.HinaHighCostAOE:
                valid = Distance(origin, point) <= skillRange + 0.25f;
                break;
            case CombatRole.AyaneHealer:
                valid = Distance(origin, point) <= skillRange + 0.25f;
                break;
            case CombatRole.YuukaTank:
                valid = true;
                break;
            default:
                valid = characterSkillKind == CharacterSkillKind.PowerUp ||
                    characterSkillKind == CharacterSkillKind.Defensive || ValidTarget(target, characterSkillKind == CharacterSkillKind.Heal);
                break;
        }
        if (!valid || !BattleDirector.Instance.TrySpendUniversal(characterSkillCost)) return false;

        skillCasting = true;
        ClearPath();
        SceneMusicDirector.PlaySkillVoice(role);
        StartCoroutine(PerformCharacterSkill(origin, direction, point, target));
        characterSkillReadyAt = Time.time + characterSkillCooldown;
        BattleDirector.Instance.NotifyCharacterSkillUsed(this);
        return true;
    }

    private IEnumerator PerformCharacterSkill(Vector3 origin, Vector3 direction, Vector3 point, CombatUnit target)
    {
        if (SkillCinematicPlayer.Instance != null)
            yield return SkillCinematicPlayer.Instance.Play(role);
        Face(direction);
        currentState = "Skill windup";
        if (skillWindup > 0f) yield return new WaitForSeconds(skillWindup);
        if (unit.IsDead) { skillCasting = false; yield break; }

        switch (role)
        {
            case CombatRole.MikaSingleTarget:
                currentState = "Skill shot";
                if (target != null && !target.IsDead)
                {
                    Vector3 impactPoint = TargetCenter(target);
                    float travel = SkillVfx.LaunchProjectile(MuzzlePosition(), target.transform, impactPoint,
                        skillProjectileSpeed, skillRange, new Color(1f, 0.2f, 0.72f, 1f), false, () =>
                        {
                            if (target == null || target.IsDead) return;
                            DestructibleCover targetCover = target.ActiveCoverPoint == null
                                ? null : target.ActiveCoverPoint.Destructible;
                            if (targetCover != null && !targetCover.IsDestroyed)
                                targetCover.TakeDamage(unit.AttackPower * skillPowerMultiplier);
                            else
                                target.TakeDamage(unit.AttackPower * skillPowerMultiplier, transform.position);
                            target.ApplyStatus(primaryEffect);
                            SkillVfx.SpawnBurst(TargetCenter(target), new Color(1f, 0.22f, 0.72f, 1f), 0.55f, 38, 0.9f);
                        }, skillProjectileSize);
                    yield return new WaitForSeconds(travel);
                }
                break;
            case CombatRole.MomoiLowCostAOE:
            case CombatRole.HinaHighCostAOE:
                currentState = "Skill barrage";
                yield return ApplyConeDamage(origin, direction);
                break;
            case CombatRole.AyaneHealer:
                currentState = "Skill med kit drop";
                SkillVfx.DropModel(medKitPrefab, point, Vector3.up * skillDropHeight,
                    skillDropDuration, 1f, true, dropped =>
                    {
                        foreach (CombatUnit ally in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
                            if (!ally.IsDead && ally.team == unit.team && Distance(point, ally.transform.position) <= aoeRadius)
                            {
                                ally.Heal(skillHealAmount);
                                ally.ApplyStatus(primaryEffect);
                            }
                        SkillVfx.SpawnBurst(point + Vector3.up * 0.15f, new Color(0.2f, 1f, 0.42f, 1f), 0.5f, 46, 1f);
                    });
                yield return new WaitForSeconds(skillDropDuration);
                break;
            case CombatRole.YuukaTank:
                currentState = "Skill shield";
                unit.ApplyShield(shieldAmount, shieldDuration);
                unit.ApplyStatus(primaryEffect);
                unit.ApplyStatus(secondaryEffect);
                SkillShieldVisual.Show(unit, shieldVisualRadius, shieldDuration, shieldMaterial);
                break;
            default:
                ApplyLegacySkill(target);
                break;
        }

        currentState = "Skill recovery";
        if (skillRecovery > 0f) yield return new WaitForSeconds(skillRecovery);
        skillCasting = false;
        currentState = "Waiting";
    }

    private IEnumerator ApplyConeDamage(Vector3 origin, Vector3 direction)
    {
        int ticks = Mathf.Max(1, damageTickCount);
        float interval = ticks <= 1 ? 0f : Mathf.Max(0f, damageTickDuration) / (ticks - 1);
        float damagePerTick = unit.AttackPower * skillPowerMultiplier / ticks;
        for (int tick = 0; tick < ticks; tick++)
        {
            Color projectileColor = role == CombatRole.HinaHighCostAOE
                ? new Color(0.58f, 0.08f, 0.96f, 1f)
                : new Color(1f, 0.48f, 0.34f, 1f);
            bool whiteCore = role == CombatRole.HinaHighCostAOE;
            System.Collections.Generic.HashSet<DestructibleCover> hitCovers =
                DestructibleCover.DamageInCone(origin, direction, skillRange, coneAngle, damagePerTick);
            foreach (CombatUnit enemy in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
            {
                if (enemy.IsDead || enemy.team == unit.team || !IsInsideCone(origin, direction, enemy.transform.position)) continue;
                DestructibleCover enemyCover = enemy.ActiveCoverPoint == null ? null : enemy.ActiveCoverPoint.Destructible;
                if (enemyCover != null && hitCovers.Contains(enemyCover)) continue;
                enemy.TakeDamage(damagePerTick, transform.position);
                if (tick == 0) enemy.ApplyStatus(primaryEffect);
            }

            int projectileCount = Mathf.Max(1, coneProjectilesPerTick);
            for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
            {
                // Pick a fresh direction for every visual bullet and every damage tick.
                // Damage still uses the full cone test above, so this changes presentation only.
                float spread = Random.Range(-coneAngle * 0.48f, coneAngle * 0.48f);
                Vector3 visualDirection = Quaternion.Euler(0f, spread, 0f) * direction;
                SkillVfx.LaunchProjectile(MuzzlePosition(), null, origin + visualDirection * skillRange,
                    skillProjectileSpeed, skillRange, projectileColor, whiteCore, null, skillProjectileSize);
            }
            if (tick + 1 < ticks && interval > 0f) yield return new WaitForSeconds(interval);
            else yield return null;
        }
    }

    private Vector3 MuzzlePosition()
    {
        Collider body = GetComponent<Collider>();
        if (body != null) return body.bounds.center + transform.forward * 0.35f;
        return transform.position + Vector3.up * 1.1f + transform.forward * 0.35f;
    }

    private static Vector3 TargetCenter(CombatUnit target)
    {
        if (target == null) return Vector3.zero;
        Collider body = target.GetComponent<Collider>();
        return body == null ? target.transform.position + Vector3.up : body.bounds.center;
    }

    public bool IsInsideCone(Vector3 origin, Vector3 direction, Vector3 candidate)
    {
        Vector3 delta = candidate - origin;
        delta.y = 0f;
        direction.y = 0f;
        return delta.magnitude <= skillRange && delta.sqrMagnitude > 0.001f &&
            Vector3.Angle(direction, delta) <= coneAngle * 0.5f;
    }

    private void ApplyLegacySkill(CombatUnit target)
    {
        switch (characterSkillKind)
        {
            case CharacterSkillKind.PowerUp:
                unit.AddTimedModifier(CombatUnit.Stat.Attack, 12f, 0.2f, 8f);
                break;
            case CharacterSkillKind.Defensive:
                unit.ApplyStatus(primaryEffect);
                unit.ApplyStatus(secondaryEffect);
                break;
            case CharacterSkillKind.Burst:
                if (target != null) target.TakeDamage(unit.AttackPower * skillPowerMultiplier, transform.position);
                break;
            case CharacterSkillKind.Heal:
                if (target != null) target.Heal(skillHealAmount);
                break;
            case CharacterSkillKind.LowCostAOE:
            case CharacterSkillKind.HighCostAOE:
                if (target != null)
                    foreach (CombatUnit enemy in FindObjectsByType<CombatUnit>(FindObjectsSortMode.None))
                        if (!enemy.IsDead && enemy.team != unit.team && Distance(target.transform.position, enemy.transform.position) <= aoeRadius)
                            enemy.TakeDamage(unit.AttackPower * skillPowerMultiplier, transform.position);
                break;
        }
    }

    private string DefaultSkillDescription()
    {
        switch (role)
        {
            case CombatRole.MikaSingleTarget: return "Drag onto one enemy. Release to deal guaranteed high damage.";
            case CombatRole.MomoiLowCostAOE: return "Aim the wide fan. Enemies inside take repeated damage.";
            case CombatRole.HinaHighCostAOE: return "Aim the long fan. Enemies inside take repeated damage.";
            case CombatRole.AyaneHealer: return "Place the circle. Allies inside recover HP.";
            case CombatRole.YuukaTank: return "Release to give Yuuka a temporary protective shield.";
            default: return "Drag to aim, then release to use this skill.";
        }
    }

    private bool ValidTarget(CombatUnit target, bool ally) => target != null && !target.IsDead &&
        (target.team == unit.team) == ally && Distance(transform.position, target.transform.position) <= skillRange;

    public void ConfigureEnemyAbilities(StatusEffectSpec[] selfEffects, StatusEffectSpec[] targetEffects,
        StatusEffectSpec[] allyEffects, float cooldown)
    {
        selfAbilityEffects = selfEffects; targetAbilityEffects = targetEffects; allyAbilityEffects = allyEffects;
        enemyAbilityCooldown = cooldown; enemyAbilityCycle = 0; enemyAbilityReadyAt = Time.time + cooldown * 0.5f;
    }

    private void OnDisable() { skillCasting = false; ReleaseCover(); }
    private static float Distance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
}
