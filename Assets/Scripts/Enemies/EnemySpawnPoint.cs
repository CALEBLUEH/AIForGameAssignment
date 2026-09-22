using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemySpawnPoint : MonoBehaviour
{
    [Serializable]
    public class SpawnEntry
    {
        [Tooltip("Enemy prefab spawned by this entry.")]
        public GameObject enemyPrefab;
        [Min(1)] public int count = 1;
    }

    [Min(1)] [SerializeField] private int waveNumber = 1;
    [SerializeField] private List<SpawnEntry> enemies = new List<SpawnEntry>();
    [Header("Formation")]
    [Min(0.25f)] [SerializeField] private float horizontalSpacing = 1.6f;
    [Min(1)] [SerializeField] private int unitsPerRow = 3;
    [Min(0.25f)] [SerializeField] private float rowSpacing = 1.5f;
    [Header("NavMesh placement")]
    [Tooltip("Vertical marker mistakes are tolerated as long as the intended baked road is inside this radius.")]
    [Min(0.5f)] [SerializeField] private float navMeshSearchRadius = 25f;
    [SerializeField] private int navMeshAreaMask = NavMesh.AllAreas;

    public int WaveNumber => waveNumber;
    public IReadOnlyList<SpawnEntry> Enemies => enemies;

    public bool TryGetSpawnPosition(int unitIndex, out Vector3 position)
    {
        int row = unitIndex / Mathf.Max(1, unitsPerRow);
        int column = unitIndex % Mathf.Max(1, unitsPerRow);
        int countInRow = Mathf.Min(unitsPerRow, Mathf.Max(1, TotalEnemyCount - row * unitsPerRow));
        float centeredColumn = column - (countInRow - 1) * 0.5f;
        Vector3 intended = transform.position + transform.right * (centeredColumn * horizontalSpacing) -
            transform.forward * (row * rowSpacing);

        if (NavMesh.SamplePosition(intended, out NavMeshHit hit, navMeshSearchRadius, navMeshAreaMask))
        {
            position = hit.position;
            return true;
        }

        position = intended;
        return false;
    }

    public int TotalEnemyCount
    {
        get
        {
            int total = 0;
            foreach (SpawnEntry entry in enemies)
                if (entry != null && entry.enemyPrefab != null) total += Mathf.Max(0, entry.count);
            return total;
        }
    }

    public void Configure(int configuredWave, IEnumerable<SpawnEntry> configuredEnemies)
    {
        waveNumber = Mathf.Max(1, configuredWave);
        enemies = configuredEnemies == null
            ? new List<SpawnEntry>()
            : new List<SpawnEntry>(configuredEnemies);
    }

    private void OnValidate()
    {
        waveNumber = Mathf.Max(1, waveNumber);
        unitsPerRow = Mathf.Max(1, unitsPerRow);
        horizontalSpacing = Mathf.Max(0.25f, horizontalSpacing);
        rowSpacing = Mathf.Max(0.25f, rowSpacing);
        navMeshSearchRadius = Mathf.Max(0.5f, navMeshSearchRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.55f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
    }
}
