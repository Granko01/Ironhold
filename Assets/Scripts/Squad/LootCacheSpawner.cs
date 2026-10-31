using System.Collections.Generic;
using UnityEngine;

// Keeps a couple of supply crates out on the map, away from the core so fetching one means
// leaving the safety of the base (or sending the squad).
public class LootCacheSpawner : MonoBehaviour
{
    [SerializeField] private LootCache cachePrefab;
    [SerializeField] private Transform hero;
    [SerializeField] private EnemySpawner waves;
    [SerializeField] private float interval = 20f;
    [SerializeField] private int maxActive = 2;
    // Outside the starting walls, so grabbing a crate means a trip beyond the base.
    [SerializeField] private float minRadius = 21f;
    [SerializeField] private float maxRadius = 26f;
    [SerializeField] private int baseReward = 60;
    [SerializeField] private int rewardPerWave = 10;

    private readonly List<LootCache> active = new List<LootCache>();
    private float timer;

    public void Configure(LootCache prefab, Transform heroTransform, EnemySpawner spawner)
    {
        cachePrefab = prefab;
        hero = heroTransform;
        waves = spawner;
    }

    private void Start()
    {
        // First crate shows up early so players learn the mechanic in wave 1.
        timer = interval * 0.4f;
    }

    private void Update()
    {
        active.RemoveAll(cache => cache == null);
        if (cachePrefab == null || active.Count >= maxActive) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = interval;

        Vector2 dir = Random.insideUnitCircle.normalized;
        float radius = Random.Range(minRadius, maxRadius);
        Vector3 position = new Vector3(dir.x, 0f, dir.y) * radius;

        LootCache cache = Instantiate(cachePrefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        int wave = waves != null ? Mathf.Max(1, waves.CurrentWave) : 1;
        cache.Initialize(baseReward + rewardPerWave * wave, hero);
        active.Add(cache);
        GameFx.Dust(position, 10);
    }
}
