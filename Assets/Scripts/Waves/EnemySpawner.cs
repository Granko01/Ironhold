using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemySpawnEntry[] enemyTypes;
    [SerializeField] private Enemy enemyPrefab; // Fallback when no enemyTypes are configured.
    [SerializeField] private Enemy bossPrefab;
    [SerializeField] private Transform coreTarget;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private UpgradeDraft upgradeDraft;

    [Header("Wave Scaling")]
    [SerializeField] private int baseThreatBudget = 8;
    [SerializeField] private int threatBudgetPerWave = 4;
    [SerializeField] private float healthGrowthPerWave = 0.12f;
    [SerializeField] private int bossEveryNWaves = 5;
    [SerializeField] private float bossWaveBudgetMultiplier = 0.5f;

    [Header("Pacing")]
    [SerializeField] private float spawnInterval = 0.6f;
    [SerializeField] private float minSpawnInterval = 0.15f;
    [SerializeField] private float spawnIntervalDecayPerWave = 0.03f;
    [SerializeField] private float delayBetweenWaves = 1.5f;

    public int CurrentWave { get; private set; }
    public int EnemiesRemaining { get; private set; }
    public bool IsBossWave { get; private set; }

    public void SetUpgradeDraft(UpgradeDraft draft)
    {
        upgradeDraft = draft;
    }

    public void Configure(EnemySpawnEntry[] types, Enemy boss, Transform core, Transform[] points)
    {
        enemyTypes = types;
        bossPrefab = boss;
        coreTarget = core;
        spawnPoints = points;
    }

    private void Start()
    {
        StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        while (true)
        {
            CurrentWave++;
            IsBossWave = bossPrefab != null && bossEveryNWaves > 0 && CurrentWave % bossEveryNWaves == 0;

            List<Enemy> wave = BuildWave(CurrentWave);
            if (IsBossWave) wave.Insert(wave.Count / 2, bossPrefab);
            EnemiesRemaining = wave.Count;

            float healthMultiplier = 1f + (CurrentWave - 1) * healthGrowthPerWave;
            float interval = Mathf.Max(minSpawnInterval, spawnInterval - (CurrentWave - 1) * spawnIntervalDecayPerWave);

            foreach (Enemy prefab in wave)
            {
                SpawnEnemy(prefab, healthMultiplier);
                yield return new WaitForSeconds(interval);
            }

            while (EnemiesRemaining > 0)
            {
                yield return null;
            }

            Debug.Log($"Wave {CurrentWave} clear!");
            if (upgradeDraft != null) yield return upgradeDraft.Run(CurrentWave);
            yield return new WaitForSeconds(delayBetweenWaves);
        }
    }

    // Spends a per-wave threat budget on a weighted-random mix of the enemy types unlocked by this wave.
    private List<Enemy> BuildWave(int waveNumber)
    {
        var wave = new List<Enemy>();
        int budget = baseThreatBudget + (waveNumber - 1) * threatBudgetPerWave;
        if (IsBossWave) budget = Mathf.RoundToInt(budget * bossWaveBudgetMultiplier);

        var unlocked = new List<EnemySpawnEntry>();
        if (enemyTypes != null)
        {
            foreach (EnemySpawnEntry entry in enemyTypes)
            {
                if (entry.prefab != null && entry.firstWave <= waveNumber && entry.threatCost > 0) unlocked.Add(entry);
            }
        }

        if (unlocked.Count == 0)
        {
            if (enemyPrefab != null) unlocked.Add(new EnemySpawnEntry { prefab = enemyPrefab });
            else return wave;
        }

        var affordable = new List<EnemySpawnEntry>();
        while (true)
        {
            affordable.Clear();
            float totalWeight = 0f;
            foreach (EnemySpawnEntry entry in unlocked)
            {
                if (entry.threatCost > budget) continue;
                affordable.Add(entry);
                totalWeight += entry.weight;
            }
            if (affordable.Count == 0 || totalWeight <= 0f) break;

            float roll = Random.value * totalWeight;
            EnemySpawnEntry pick = affordable[affordable.Count - 1];
            foreach (EnemySpawnEntry entry in affordable)
            {
                roll -= entry.weight;
                if (roll <= 0f)
                {
                    pick = entry;
                    break;
                }
            }

            wave.Add(pick.prefab);
            budget -= pick.threatCost;
        }

        return wave;
    }

    private void SpawnEnemy(Enemy prefab, float healthMultiplier)
    {
        if (prefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            EnemiesRemaining--;
            return;
        }

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Enemy enemy = Instantiate(prefab, spawnPoint.position, Quaternion.identity);
        enemy.Health.Configure(enemy.Health.MaxHealth * healthMultiplier);
        enemy.Initialize(coreTarget);
        enemy.Despawned += () => EnemiesRemaining--;
    }
}
