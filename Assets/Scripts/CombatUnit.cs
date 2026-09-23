using System;
using System.Collections.Generic;
using UnityEngine;

public class CombatUnit : MonoBehaviour
{
    public enum CombatTeam { Player, Enemy }
    public enum Stat { Attack, Defense, Range, AttackSpeed }
    [Serializable] public class ActiveStatusEffect { public StatusEffectType type; public float amount, expiresAt; }
    private struct Modifier { public Stat stat; public float flat, multiplier, expires; }
    public static event Action<CombatUnit> UnitDied;
    public CombatTeam team;
    public bool isBoss, isElite;

    [Header("Shared Character Stats")]
    [Tooltip("Assign YuukaData, MikaData, AyaneData, etc. All scenes using the same asset share these base stats.")]
    public CharacterData characterData;
    [Tooltip("When enabled, Character Data overwrites the Basic Stats below during Awake. Disable this on a scene character when you want its CombatUnit Inspector values to be authoritative.")]
    [SerializeField] private bool applyCharacterDataOnAwake = true;

    [Header("Basic stats")]
    public float maxHealth = 100f, attackPower = 20f, defense = 5f, attackRange = 5f, attackSpeed = 1f;
    public float resistance = 5f, skillPointRegeneration = 7f, skillPointCapacity = 100f;
    public float durationRate = 1f, manipulationRate = 1f;
    [SerializeField] private float currentHealth, movementPoints;
    [SerializeField] private float shieldPoints, shieldExpiresAt;
    [SerializeField] private bool isDead;
    [SerializeField] private List<ActiveStatusEffect> activeStatuses = new List<ActiveStatusEffect>();
    private int hitsTaken, statusVersion;
    private float coverProtection;
    private CoverPoint activeCoverPoint;
    public CoverPoint ActiveCoverPoint => activeCoverPoint;
    private WorldUnitHUD worldHUD;
    private readonly List<Modifier> modifiers = new List<Modifier>();
    public float CurrentHealth => currentHealth;
    public float HealthRatio => currentHealth / Mathf.Max(1f, maxHealth);
    public float MovementPoints => movementPoints;
    public float MovementPointCapacity => Mathf.Max(1f, skillPointCapacity);
    public float MovementPointRatio => Mathf.Clamp01(movementPoints / MovementPointCapacity);
    public bool IsMovementGaugeFull => movementPoints >= MovementPointCapacity - 0.01f;
    public float ShieldPoints => shieldExpiresAt > Time.time ? shieldPoints : 0f;
    public bool IsDead => isDead;
    public bool IsBoss => isBoss;
    public float AttackPower => CalculateStat(Stat.Attack, attackPower);
    public float Defense => CalculateStat(Stat.Defense, defense);
    public float AttackRange => CalculateStat(Stat.Range, attackRange);
    public float AttackSpeed => CalculateStat(Stat.AttackSpeed, attackSpeed);
    public IReadOnlyList<ActiveStatusEffect> ActiveStatuses => activeStatuses;
    public int StatusVersion => statusVersion;

    private void Awake()
    {
        if (applyCharacterDataOnAwake) ApplyCharacterData();
        currentHealth = maxHealth;
        movementPoints = skillPointCapacity;
        worldHUD = GetComponentInChildren<WorldUnitHUD>(true);
        if (worldHUD != null) worldHUD.Bind(this);
    }

    private void Update()
    {
        if (isDead || (BattleDirector.Instance != null && !BattleDirector.Instance.IsPlaying)) return;
        movementPoints = Mathf.Min(skillPointCapacity, movementPoints + skillPointRegeneration * Time.deltaTime);
        modifiers.RemoveAll(m => m.expires <= Time.time);
        if (activeStatuses.RemoveAll(effect => effect.expiresAt <= Time.time) > 0) statusVersion++;
        if (shieldPoints > 0f && shieldExpiresAt <= Time.time) shieldPoints = 0f;
    }

    private float CalculateStat(Stat stat, float basis)
    {
        float flat = 0f, percentage = 0f;
        foreach (Modifier modifier in modifiers)
        {
            if (modifier.stat != stat || modifier.expires <= Time.time) continue;
            flat += modifier.flat * manipulationRate;
            percentage += modifier.multiplier * manipulationRate;
        }
        if (stat == Stat.Attack)
        {
            flat += StatusAmount(StatusEffectType.Strength) - StatusAmount(StatusEffectType.Weak);
            percentage += StatusAmount(StatusEffectType.Rage) - StatusAmount(StatusEffectType.Dull);
        }
        else if (stat == Stat.Defense)
        {
            flat += StatusAmount(StatusEffectType.Hardening) - StatusAmount(StatusEffectType.Penetration);
            percentage += StatusAmount(StatusEffectType.Fortified) - StatusAmount(StatusEffectType.Piercing);
        }
        return Mathf.Max(0f, (basis + flat) * Mathf.Max(0f, 1f + percentage));
    }

    private float StatusAmount(StatusEffectType type)
    {
        float total = 0f;
        foreach (ActiveStatusEffect effect in activeStatuses)
            if (effect.type == type && effect.expiresAt > Time.time) total += effect.amount * manipulationRate;
        return total;
    }

    public void ApplyStatus(StatusEffectSpec spec)
    {
        if (!spec.IsValid || isDead) return;
        float expiry = Time.time + spec.duration * Mathf.Max(0.1f, durationRate);
        ActiveStatusEffect existing = activeStatuses.Find(effect => effect.type == spec.type);
        if (existing == null)
            activeStatuses.Add(new ActiveStatusEffect { type = spec.type, amount = Mathf.Abs(spec.amount), expiresAt = expiry });
        else
        {
            existing.amount = Mathf.Max(existing.amount, Mathf.Abs(spec.amount));
            existing.expiresAt = Mathf.Max(existing.expiresAt, expiry);
        }
        statusVersion++;
    }

    public void AddTimedModifier(Stat stat, float flat, float multiplier, float duration) =>
        modifiers.Add(new Modifier { stat = stat, flat = flat, multiplier = multiplier,
            expires = Time.time + duration * Mathf.Max(0.1f, durationRate) });
    public bool SpendMovementPoints(float amount)
    {
        if (movementPoints < amount) return false;
        movementPoints -= amount;
        return true;
    }
    public void RefundMovementPoints(float amount) => movementPoints = Mathf.Min(skillPointCapacity, movementPoints + amount);
    public void TakeDamage(float attack) => TakeDamage(attack, transform.position);
    public void TakeDamage(float attack, Vector3 source)
    {
        if (isDead) return;
        float rawDamage = Mathf.Max(1f, attack - Defense);
        float protection = Mathf.Clamp01(coverProtection);
        float appliedDamage = rawDamage;

        // A valid occupied cover object takes the hit first. Damage only reaches the
        // character when that hit breaks the remaining cover HP.
        if (activeCoverPoint != null && protection > 0f)
        {
            DestructibleCover destructible = activeCoverPoint.Destructible;
            if (destructible != null && !destructible.IsDestroyed)
                appliedDamage = destructible.AbsorbDamage(rawDamage);
            else
                appliedDamage = rawDamage * (1f - protection);
        }
        if (ShieldPoints > 0f)
        {
            float absorbed = Mathf.Min(shieldPoints, appliedDamage);
            shieldPoints -= absorbed;
            appliedDamage -= absorbed;
        }
        currentHealth -= appliedDamage;
        if (worldHUD != null) worldHUD.ShowDamage(appliedDamage);
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            isDead = true;
            UnitDied?.Invoke(this);
            Destroy(gameObject, 1.1f);
        }
        else if (appliedDamage > 0f && ++hitsTaken >= Mathf.Max(1, Mathf.RoundToInt(resistance)))
        {
            hitsTaken = 0;
            AutoCombatAI navigation = GetComponent<AutoCombatAI>();
            if (navigation != null)
                navigation.TriggerKnockback(source);
            else
            {
                Vector3 retreat = transform.position - source;
                retreat.y = 0f;
                if (retreat.sqrMagnitude > 0.01f)
                {
                    Vector3 destination = transform.position + retreat.normalized * 0.8f;
                    if (UnityEngine.AI.NavMesh.SamplePosition(destination, out var hit, 1f, UnityEngine.AI.NavMesh.AllAreas))
                        transform.position = hit.position;
                }
            }
        }
    }
    public void Heal(float amount)
    {
        if (isDead) return;
        float oldHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0f, amount));
        if (worldHUD != null && currentHealth > oldHealth) worldHUD.ShowHeal(currentHealth - oldHealth);
    }
    public void SetCoverProtection(float protection) => SetCoverProtection(protection, null);

    public void SetCoverProtection(float protection, CoverPoint sourceCover)
    {
        coverProtection = Mathf.Clamp01(protection);
        activeCoverPoint = coverProtection > 0f ? sourceCover : null;
    }
    public void ApplyCharacterData()
    {
        if (characterData == null) return;

        maxHealth = characterData.maxHealth;
        attackPower = characterData.attackPower;
        defense = characterData.defense;
        attackRange = characterData.attackRange;
        attackSpeed = characterData.attackSpeed;
        resistance = characterData.resistance;
        skillPointRegeneration = characterData.skillPointRegeneration;
        skillPointCapacity = characterData.skillPointCapacity;
        durationRate = characterData.durationRate;
        manipulationRate = characterData.manipulationRate;
    }
    public void ApplyShield(float amount, float duration)
    {
        if (isDead || amount <= 0f || duration <= 0f) return;
        shieldPoints = Mathf.Max(shieldPoints, amount);
        shieldExpiresAt = Mathf.Max(shieldExpiresAt, Time.time + duration);
    }

    public bool AppliesCharacterDataOnAwake => applyCharacterDataOnAwake;
    public void SetApplyCharacterDataOnAwake(bool value) => applyCharacterDataOnAwake = value;

    public void ConfigureFromCharacterData(CharacterData data, bool boss)
    {
        if (data != null)
            characterData = data;

        ApplyCharacterData();
        isBoss = boss;
        currentHealth = maxHealth;
        movementPoints = skillPointCapacity;
        isDead = false;
        shieldPoints = shieldExpiresAt = 0f;
        coverProtection = 0f;
        activeCoverPoint = null;
        hitsTaken = 0;
        activeStatuses.Clear();
        modifiers.Clear();
        statusVersion++;
    }

    public void ConfigureSpawn(float health, float power, float armor, bool boss)
    {
        maxHealth = health; attackPower = power; defense = armor; isBoss = boss;
        currentHealth = maxHealth; movementPoints = skillPointCapacity; isDead = false;
        shieldPoints = shieldExpiresAt = 0f;
        coverProtection = 0f; hitsTaken = 0; activeStatuses.Clear(); modifiers.Clear(); statusVersion++;
    }
}
