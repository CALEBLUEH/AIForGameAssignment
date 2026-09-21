using System.Collections.Generic;
using UnityEngine;

public class CombatUnit : MonoBehaviour
{
    public enum CombatTeam { Player, Enemy }
    public enum Stat { Attack, Defense, Range, AttackSpeed }
    private struct Modifier { public Stat stat; public float flat, multiplier, expires; }
    public CombatTeam team;
    public bool isBoss;
    [Header("Basic stats")]
    public float maxHealth = 100f, attackPower = 20f, defense = 5f, attackRange = 5f, attackSpeed = 1f;
    public float resistance = 5f, skillPointRegeneration = 7f, skillPointCapacity = 100f;
    public float durationRate = 1f, manipulationRate = 1f;
    [SerializeField] private float currentHealth, movementPoints;
    [SerializeField] private bool isDead;
    private int hitsTaken;
    private float coverProtection;
    private readonly List<Modifier> modifiers = new List<Modifier>();
    public float CurrentHealth => currentHealth;
    public float MovementPoints => movementPoints;
    public bool IsDead => isDead;
    public bool IsBoss => isBoss;
    public float AttackPower => Modified(Stat.Attack, attackPower);
    public float Defense => Modified(Stat.Defense, defense);
    public float AttackRange => Modified(Stat.Range, attackRange);
    public float AttackSpeed => Modified(Stat.AttackSpeed, attackSpeed);
    private void Awake() { currentHealth = maxHealth; movementPoints = skillPointCapacity; }
    private void Update()
    {
        if (isDead || (BattleDirector.Instance != null && !BattleDirector.Instance.IsPlaying)) return;
        movementPoints = Mathf.Min(skillPointCapacity, movementPoints + skillPointRegeneration * Time.deltaTime);
        modifiers.RemoveAll(m => m.expires <= Time.time);
    }
    private float Modified(Stat stat, float basis)
    {
        float flat = 0f, multiplier = 0f;
        foreach (var m in modifiers)
        {
            if (m.stat != stat || m.expires <= Time.time) continue;
            flat += m.flat * manipulationRate;
            multiplier += m.multiplier * manipulationRate;
        }
        return Mathf.Max(0f, (basis + flat) * Mathf.Max(0f, 1f + multiplier));
    }
    public void AddTimedModifier(Stat stat, float flat, float multiplier, float duration)
    {
        modifiers.Add(new Modifier { stat = stat, flat = flat, multiplier = multiplier,
            expires = Time.time + duration * Mathf.Max(0.1f, durationRate) });
    }
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
        float damage = Mathf.Max(1f, attack - Defense);
        currentHealth -= damage * (1f - Mathf.Clamp01(coverProtection));
        if (currentHealth <= 0f) { currentHealth = 0f; isDead = true; Destroy(gameObject, 0.5f); }
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
        if (!isDead) currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0f, amount));
    }

    public void SetCoverProtection(float protection)
    {
        coverProtection = Mathf.Clamp01(protection);
    }

    public void ConfigureSpawn(float health, float power, float armor, bool boss)
    {
        maxHealth = health;
        attackPower = power;
        defense = armor;
        isBoss = boss;
        currentHealth = maxHealth;
        movementPoints = skillPointCapacity;
        isDead = false;
        coverProtection = 0f;
        hitsTaken = 0;
    }
}
