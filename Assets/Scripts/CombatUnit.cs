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
    [Header("Basic stats")]
    public float maxHealth = 100f, attackPower = 20f, defense = 5f, attackRange = 5f, attackSpeed = 1f;
    public float resistance = 5f, skillPointRegeneration = 7f, skillPointCapacity = 100f;
    public float durationRate = 1f, manipulationRate = 1f;
    [SerializeField] private float currentHealth, movementPoints;
    [SerializeField] private bool isDead;
    [SerializeField] private List<ActiveStatusEffect> activeStatuses = new List<ActiveStatusEffect>();
    private int hitsTaken, statusVersion;
    private float coverProtection;
    private WorldUnitHUD worldHUD;
    private readonly List<Modifier> modifiers = new List<Modifier>();
    public float CurrentHealth => currentHealth;
    public float HealthRatio => currentHealth / Mathf.Max(1f, maxHealth);
    public float MovementPoints => movementPoints;
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
        float appliedDamage = Mathf.Max(1f, attack - Defense) * (1f - Mathf.Clamp01(coverProtection));
        currentHealth -= appliedDamage;
        if (worldHUD != null) worldHUD.ShowDamage(appliedDamage);
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            isDead = true;
            UnitDied?.Invoke(this);
            Destroy(gameObject, 1.1f);
        }
        else if (++hitsTaken >= Mathf.Max(1, Mathf.RoundToInt(resistance)))
        {
            hitsTaken = 0;
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
    public void Heal(float amount)
    {
        if (isDead) return;
        float oldHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0f, amount));
        if (worldHUD != null && currentHealth > oldHealth) worldHUD.ShowHeal(currentHealth - oldHealth);
    }
    public void SetCoverProtection(float protection) => coverProtection = Mathf.Clamp01(protection);
    public void ConfigureSpawn(float health, float power, float armor, bool boss)
    {
        maxHealth = health; attackPower = power; defense = armor; isBoss = boss;
        currentHealth = maxHealth; movementPoints = skillPointCapacity; isDead = false;
        coverProtection = 0f; hitsTaken = 0; activeStatuses.Clear(); modifiers.Clear(); statusVersion++;
    }
}
