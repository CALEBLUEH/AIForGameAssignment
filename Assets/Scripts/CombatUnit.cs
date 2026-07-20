using UnityEngine;

public class CombatUnit : MonoBehaviour
{
    public enum CombatTeam
    {
        Player,
        Enemy
    }

    [Header("Identity")]
    public CombatTeam team;

    [Header("Basic Stats")]
    public float maxHealth = 100f;
    public float attackPower = 20f;
    public float defense = 5f;
    public float attackRange = 5f;
    public float attackSpeed = 1f;

    [Header("Runtime Information")]
    [SerializeField] private float currentHealth;
    [SerializeField] private bool isDead;

    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float incomingAttackPower)
    {
        if (isDead)
        {
            return;
        }

        float finalDamage = incomingAttackPower - defense;

        // Every attack deals at least 1 damage.
        finalDamage = Mathf.Max(1f, finalDamage);

        currentHealth -= finalDamage;

        Debug.Log(
            gameObject.name +
            " received " +
            finalDamage +
            " damage. Remaining HP: " +
            currentHealth
        );

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        currentHealth = 0f;

        Debug.Log(gameObject.name + " has been defeated.");

        Destroy(gameObject, 0.5f);
    }
}