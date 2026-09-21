using UnityEngine;

public class DestructibleCover : MonoBehaviour
{
    [Header("Cover Health")]
    [SerializeField] private float maxHealth = 150f;
    [SerializeField] private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDestroyed { get; private set; }

    private void Awake()
    {
        currentHealth = Mathf.Max(1f, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0f) return;

        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            BreakCover();
        }
    }

    private void BreakCover()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;

        // Release every character using a CoverPoint belonging to this wall.
        CoverPoint[] points = GetComponentsInChildren<CoverPoint>(true);
        foreach (CoverPoint point in points)
        {
            if (point != null && point.Occupant != null)
                point.Release(point.Occupant);
        }

        Destroy(gameObject);
    }
}
