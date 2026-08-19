using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private Transform coreTarget;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int baseWaveSize = 8;
    [SerializeField] private int waveSizeIncrement = 3;
    [SerializeField] private float spawnInterval = 0.6f;
    [SerializeField] private float delayBetweenWaves = 3f;

    public int CurrentWave { get; private set; }
    public int EnemiesRemaining { get; private set; }

    public void Configure(Enemy prefab, Transform core, Transform[] points)
    {
        enemyPrefab = prefab;
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
            int waveSize = baseWaveSize + (CurrentWave - 1) * waveSizeIncrement;
            EnemiesRemaining = waveSize;

            for (int i = 0; i < waveSize; i++)
            {
                SpawnEnemy();
                yield return new WaitForSeconds(spawnInterval);
            }

            while (EnemiesRemaining > 0)
            {
                yield return null;
            }

            Debug.Log($"Wave {CurrentWave} clear!");
            yield return new WaitForSeconds(delayBetweenWaves);
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Enemy enemy = Instantiate(enemyPrefab, spawnPoint.position, Quaternion.identity);
        enemy.Initialize(coreTarget);
        enemy.Despawned += () => EnemiesRemaining--;
    }
}
