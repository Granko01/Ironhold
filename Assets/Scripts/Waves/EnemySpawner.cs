using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemySpawnEntry[] enemyTypes;
    [SerializeField] private Enemy enemyPrefab; // Fallback when no enemyTypes are configured.
    [SerializeField] private Enemy bossPrefab;
    [SerializeField] private Transform coreTarget;
    [SerializeField] private UpgradeDraft upgradeDraft;

    // Enemies dig up out of the treeline around the arena. Each wave comes from a couple of
    // random directions, so the player has to read where the horde is and shore up that side.
    [Header("Spawning")]
    [SerializeField] private float spawnRadiusMin = 27f;
    [SerializeField] private float spawnRadiusMax = 32f;
    [SerializeField] private int approachesPerWave = 2;
    [SerializeField] private float approachSpreadDegrees = 22f;

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

    private readonly List<float> approachAngles = new List<float>();

    public void Configure(EnemySpawnEntry[] types, Enemy boss, Transform core)
    {
        enemyTypes = types;
        bossPrefab = boss;
        coreTarget = core;
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
            GameSettings.RecordWave(CurrentWave);
            IsBossWave = bossPrefab != null && bossEveryNWaves > 0 && CurrentWave % bossEveryNWaves == 0;

            PickApproaches();
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

    // Approaches are kept at least 60 degrees apart so two of them never merge into one.
    private void PickApproaches()
    {
        approachAngles.Clear();
        int count = Mathf.Max(1, approachesPerWave + (CurrentWave >= 8 ? 1 : 0));
        for (int attempt = 0; approachAngles.Count < count && attempt < 50; attempt++)
        {
            float angle = Random.Range(0f, 360f);
            bool tooClose = false;
            foreach (float other in approachAngles)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(angle, other)) < 60f) tooClose = true;
            }
            if (!tooClose) approachAngles.Add(angle);
        }
    }

    private Vector3 PickSpawnPosition()
    {
        float angle = approachAngles.Count > 0 ? approachAngles[Random.Range(0, approachAngles.Count)] : Random.Range(0f, 360f);
        angle += Random.Range(-approachSpreadDegrees, approachSpreadDegrees);
        float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);
        Vector3 center = coreTarget != null ? new Vector3(coreTarget.position.x, 0f, coreTarget.position.z) : Vector3.zero;
        return center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
    }

    private void SpawnEnemy(Enemy prefab, float healthMultiplier)
    {
        if (prefab == null)
        {
            EnemiesRemaining--;
            return;
        }

        Enemy enemy = Instantiate(prefab, PickSpawnPosition(), Quaternion.identity);
        enemy.Health.Configure(enemy.Health.MaxHealth * healthMultiplier);
        enemy.Initialize(coreTarget, emergeFromGround: true);
        enemy.Despawned += () => EnemiesRemaining--;
    }
}
