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
    [SerializeField] private EnemyFormationShape formationShape = EnemyFormationShape.Row;
    [Min(0.25f)] [SerializeField] private float horizontalSpacing = 1.6f;
    [Min(0.25f)] [SerializeField] private float rowSpacing = 1.5f;
    [Min(0.1f)] [SerializeField] private float formationPositionTolerance = 0.55f;
    [Header("NavMesh placement")]
    [Tooltip("Vertical marker mistakes are tolerated as long as the intended baked road is inside this radius.")]
    [Min(0.5f)] [SerializeField] private float navMeshSearchRadius = 25f;
    [SerializeField] private int navMeshAreaMask = NavMesh.AllAreas;

    public int WaveNumber => waveNumber;
    public IReadOnlyList<SpawnEntry> Enemies => enemies;
    public EnemyFormationShape FormationShape => formationShape;
    public float HorizontalSpacing => horizontalSpacing;
    public float RowSpacing => rowSpacing;
    public float FormationPositionTolerance => formationPositionTolerance;

    public bool TryGetSpawnPosition(int unitIndex, out Vector3 position)
    {
        Vector2 offset = EnemyFormationCoordinator.FormationOffset(
            formationShape, unitIndex, horizontalSpacing, rowSpacing);
        Vector3 intended = transform.position + transform.right * offset.x + transform.forward * offset.y;

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

    public void ConfigureFormation(EnemyFormationShape shape, float spacing, float depth, float tolerance)
    {
        formationShape = shape;
        horizontalSpacing = Mathf.Max(0.25f, spacing);
        rowSpacing = Mathf.Max(0.25f, depth);
        formationPositionTolerance = Mathf.Max(0.1f, tolerance);
    }

    private void OnValidate()
    {
        waveNumber = Mathf.Max(1, waveNumber);
        horizontalSpacing = Mathf.Max(0.25f, horizontalSpacing);
        rowSpacing = Mathf.Max(0.25f, rowSpacing);
        formationPositionTolerance = Mathf.Max(0.1f, formationPositionTolerance);
        navMeshSearchRadius = Mathf.Max(0.5f, navMeshSearchRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.55f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
    }
}
