using UnityEngine;
using UnityEngine.UI;

public class DebugHUD : MonoBehaviour
{
    [SerializeField] private EnemySpawner spawner;
    [SerializeField] private Health coreHealth;
    [SerializeField] private Text scrapText;
    [SerializeField] private Text coreHpText;
    [SerializeField] private Text waveText;
    [SerializeField] private Text placementText;
    [SerializeField] private GameObject gameOverLabel;

    public void Configure(EnemySpawner spawnerRef, Health core, Text scrap, Text coreHp, Text wave, Text placement, GameObject gameOver)
    {
        spawner = spawnerRef;
        coreHealth = core;
        scrapText = scrap;
        coreHpText = coreHp;
        waveText = wave;
        placementText = placement;
        gameOverLabel = gameOver;
        if (gameOverLabel != null) gameOverLabel.SetActive(false);
    }

    private void Update()
    {
        int scrap = GameManager.Instance != null ? GameManager.Instance.Scrap : 0;
        if (scrapText != null) scrapText.text = $"Scrap: {scrap}";

        string hp = coreHealth != null ? $"{coreHealth.CurrentHealth:0}/{coreHealth.MaxHealth:0}" : "-";
        if (coreHpText != null) coreHpText.text = $"Core HP: {hp}";

        int wave = spawner != null ? spawner.CurrentWave : 0;
        int enemiesLeft = spawner != null ? spawner.EnemiesRemaining : 0;
        if (waveText != null) waveText.text = $"Wave: {wave}    Enemies left: {enemiesLeft}";

        if (placementText != null)
        {
            bool placing = TowerPlacer.IsPlacing;
            placementText.text = placing
                ? "Placement mode: ON (face a spot, press B to place - right click / Esc to cancel)"
                : "Placement mode: OFF - pick a tower below or press 1-9";
            placementText.color = placing ? Color.yellow : Color.white;
        }

        if (gameOverLabel != null)
        {
            bool isOver = GameManager.Instance != null && GameManager.Instance.IsGameOver;
            if (gameOverLabel.activeSelf != isOver) gameOverLabel.SetActive(isOver);
        }
    }
}
