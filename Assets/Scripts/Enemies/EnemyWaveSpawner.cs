using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyWaveSpawner : MonoBehaviour
{
    [SerializeField] private EnemySpawnPoint[] spawnPoints;
    [Min(0f)] [SerializeField] private float delayBetweenWaves = 2f;
    [SerializeField] private bool finishAfterLastWave = true;

    private readonly HashSet<CombatUnit> aliveWaveEnemies = new HashSet<CombatUnit>();
    private readonly List<int> configuredWaves = new List<int>();
    private bool running;
    private bool transitionPending;
    private float nextWaveAt;
    private int waveListIndex = -1;

    public int CurrentWave => waveListIndex >= 0 && waveListIndex < configuredWaves.Count
        ? configuredWaves[waveListIndex]
        : 0;
    public int AliveEnemyCount => aliveWaveEnemies.Count;
    public bool IsTransitioning => transitionPending;
    public bool IsComplete { get; private set; }
    public bool FinishAfterLastWave => finishAfterLastWave;

    public void Configure(EnemySpawnPoint[] configuredSpawnPoints, float configuredDelay)
    {
        spawnPoints = configuredSpawnPoints;
        delayBetweenWaves = Mathf.Max(0f, configuredDelay);
    }

    private void OnEnable()
    {
        CombatUnit.UnitDied += OnUnitDied;
    }

    private void OnDisable()
    {
        CombatUnit.UnitDied -= OnUnitDied;
    }

    private void Update()
    {
        if (!running || IsComplete || !transitionPending || Time.time < nextWaveAt) return;
        transitionPending = false;
        SpawnNextWave();
    }

    public void BeginBattle()
    {
        aliveWaveEnemies.Clear();
        configuredWaves.Clear();
        waveListIndex = -1;
        IsComplete = false;
        transitionPending = false;

        if (spawnPoints != null)
        {
            foreach (EnemySpawnPoint point in spawnPoints)
            {
                if (point == null || point.TotalEnemyCount == 0 || configuredWaves.Contains(point.WaveNumber)) continue;
                configuredWaves.Add(point.WaveNumber);
            }
        }

        configuredWaves.Sort();
        running = true;
        if (configuredWaves.Count == 0)
        {
            Debug.LogError("EnemyWaveSpawner has no valid enemy entries assigned to its spawn points.", this);
            running = false;
            return;
        }

        SpawnNextWave();
    }

    private void OnUnitDied(CombatUnit deadUnit)
    {
        if (!running || deadUnit == null || !aliveWaveEnemies.Remove(deadUnit) || aliveWaveEnemies.Count > 0) return;
        QueueNextWaveOrComplete();
    }

    private void SpawnNextWave()
    {
        waveListIndex++;
        if (waveListIndex >= configuredWaves.Count)
        {
            Complete();
            return;
        }

        int waveNumber = configuredWaves[waveListIndex];
        foreach (EnemySpawnPoint point in spawnPoints)
        {
            if (point == null || point.WaveNumber != waveNumber) continue;
            int unitIndex = 0;
            foreach (EnemySpawnPoint.SpawnEntry entry in point.Enemies)
            {
                if (entry == null || entry.enemyPrefab == null) continue;
                for (int i = 0; i < entry.count; i++, unitIndex++)
                    SpawnEnemy(point, entry.enemyPrefab, unitIndex);
            }
        }

        if (aliveWaveEnemies.Count == 0)
        {
            Debug.LogError($"Enemy wave {waveNumber} could not spawn any enemies on the baked NavMesh.", this);
            running = false;
        }
    }

    private void SpawnEnemy(EnemySpawnPoint point, GameObject prefab, int unitIndex)
    {
        if (!point.TryGetSpawnPosition(unitIndex, out Vector3 navMeshPosition))
        {
            Debug.LogWarning($"Skipped {prefab.name}: {point.name} could not find a baked NavMesh position.", point);
            return;
        }

        GameObject instance = Instantiate(prefab, navMeshPosition, point.transform.rotation);
        instance.name = $"Wave {CurrentWave} {prefab.name}";
        CombatUnit unit = instance.GetComponent<CombatUnit>();
        AutoCombatAI ai = instance.GetComponent<AutoCombatAI>();
        if (unit == null || ai == null || unit.team != CombatUnit.CombatTeam.Enemy)
        {
            Debug.LogError($"{prefab.name} must have enemy CombatUnit and AutoCombatAI components.", prefab);
            Destroy(instance);
            return;
        }

        ai.ActivateAtSpawn(navMeshPosition);
        aliveWaveEnemies.Add(unit);
    }

    private void QueueNextWaveOrComplete()
    {
        if (waveListIndex + 1 >= configuredWaves.Count)
        {
            Complete();
            return;
        }

        transitionPending = true;
        nextWaveAt = Time.time + delayBetweenWaves;
    }

    private void Complete()
    {
        running = false;
        transitionPending = false;
        IsComplete = true;
    }
}
