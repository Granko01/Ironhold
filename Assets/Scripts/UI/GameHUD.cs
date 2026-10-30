using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    [SerializeField] private EnemySpawner spawner;
    [SerializeField] private Health coreHealth;
    [SerializeField] private Text scrapText;
    [SerializeField] private Text waveText;
    [SerializeField] private Text enemiesText;
    [SerializeField] private Text coreHpText;
    [SerializeField] private RectTransform coreHpFill;
    [SerializeField] private Image coreHpFillImage;
    [SerializeField] private RectTransform scrapPill;
    [SerializeField] private Image coinIcon;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text gameOverStatsText;

    private static readonly Color HealthyColor = new Color(0.3f, 0.9f, 1f);
    private static readonly Color CriticalColor = new Color(1f, 0.25f, 0.2f);
    private static readonly Color BossColor = new Color(1f, 0.35f, 0.2f);

    private const float CoinFlightTime = 0.5f;
    private const int MaxCoinsInFlight = 24;

    private class FlyingCoin
    {
        public RectTransform rect;
        public Vector2 start;
        public float age;
    }

    private readonly List<FlyingCoin> coins = new List<FlyingCoin>();
    private readonly Stack<RectTransform> coinPool = new Stack<RectTransform>();
    private Transform coinLayer;
    private Camera cam;
    private int shownWave;
    private float wavePunch;
    private float scrapPunch;
    private float hpPunch;

    public void Configure(EnemySpawner spawnerRef, Health core, Text scrap, Text wave, Text enemies, Text coreHp,
        RectTransform hpFill, RectTransform scrapPillRect, Image coin, GameObject gameOver, Text gameOverStats)
    {
        spawner = spawnerRef;
        coreHealth = core;
        scrapText = scrap;
        waveText = wave;
        enemiesText = enemies;
        coreHpText = coreHp;
        coreHpFill = hpFill;
        coreHpFillImage = hpFill != null ? hpFill.GetComponent<Image>() : null;
        scrapPill = scrapPillRect;
        coinIcon = coin;
        gameOverPanel = gameOver;
        gameOverStatsText = gameOverStats;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    private void Start()
    {
        cam = Camera.main;
        Canvas canvas = GetComponentInParent<Canvas>();
        coinLayer = canvas != null ? canvas.transform : transform;

        if (GameManager.Instance != null) GameManager.Instance.ScrapCollected += HandleScrapCollected;
        if (coreHealth != null) coreHealth.Damaged += HandleCoreDamaged;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.ScrapCollected -= HandleScrapCollected;
        if (coreHealth != null) coreHealth.Damaged -= HandleCoreDamaged;
    }

    private void HandleScrapCollected(int amount, Vector3 worldPosition)
    {
        if (cam == null || coins.Count >= MaxCoinsInFlight)
        {
            scrapPunch = 1f;
            return;
        }

        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        if (screen.z < 0f) return;

        RectTransform rect = coinPool.Count > 0 ? coinPool.Pop() : CreateCoin();
        rect.gameObject.SetActive(true);
        rect.position = screen;
        coins.Add(new FlyingCoin { rect = rect, start = screen, age = 0f });
    }

    private void HandleCoreDamaged(float amount)
    {
        hpPunch = 1f;
    }

    private RectTransform CreateCoin()
    {
        var go = new GameObject("FlyingCoin", typeof(RectTransform));
        go.transform.SetParent(coinLayer, false);
        Image image = go.AddComponent<Image>();
        image.sprite = coinIcon != null ? coinIcon.sprite : null;
        image.color = coinIcon != null ? coinIcon.color : new Color(1f, 0.82f, 0.3f);
        image.raycastTarget = false;
        RectTransform rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(44f, 44f);
        return rect;
    }

    private void Update()
    {
        UpdateCoins();
        UpdateLabels();
        UpdatePunches();
    }

    private void UpdateCoins()
    {
        Vector2 target = coinIcon != null ? (Vector2)coinIcon.rectTransform.position : Vector2.zero;

        for (int i = coins.Count - 1; i >= 0; i--)
        {
            FlyingCoin coin = coins[i];
            coin.age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(coin.age / CoinFlightTime);

            if (t >= 1f)
            {
                coin.rect.gameObject.SetActive(false);
                coinPool.Push(coin.rect);
                coins.RemoveAt(i);
                scrapPunch = 1f;
                Sfx.Play(SfxId.Coin);
                continue;
            }

            // Ease-in toward the counter with a sideways arc so coins don't all travel in a line.
            float eased = t * t;
            Vector2 straight = Vector2.LerpUnclamped(coin.start, target, eased);
            Vector2 arc = Vector2.Perpendicular(target - coin.start).normalized * Mathf.Sin(t * Mathf.PI) * 80f;
            coin.rect.position = straight + arc;
            coin.rect.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.7f, t);
        }
    }

    private void UpdateLabels()
    {
        int scrap = GameManager.Instance != null ? GameManager.Instance.Scrap : 0;
        if (scrapText != null) scrapText.text = scrap.ToString();

        int wave = spawner != null ? spawner.CurrentWave : 0;
        bool bossWave = spawner != null && spawner.IsBossWave;
        if (wave != shownWave)
        {
            shownWave = wave;
            wavePunch = 1f;
            if (wave > 0) Sfx.Play(SfxId.WaveStart, bossWave ? 0.7f : 1f);
        }

        if (waveText != null)
        {
            waveText.text = $"WAVE {wave}";
            waveText.color = bossWave ? BossColor : Color.white;
        }

        if (enemiesText != null)
        {
            int enemiesLeft = spawner != null ? spawner.EnemiesRemaining : 0;
            enemiesText.text = bossWave ? $"BOSS INCOMING  ·  {enemiesLeft} left" : $"{enemiesLeft} enemies left";
        }

        if (coreHealth != null)
        {
            float fraction = coreHealth.MaxHealth > 0f ? coreHealth.CurrentHealth / coreHealth.MaxHealth : 0f;
            if (coreHpFill != null) coreHpFill.anchorMax = new Vector2(fraction, 1f);
            if (coreHpFillImage != null)
            {
                Color barColor = Color.Lerp(CriticalColor, HealthyColor, Mathf.InverseLerp(0.2f, 0.7f, fraction));
                coreHpFillImage.color = Color.Lerp(barColor, Color.white, hpPunch * 0.7f);
            }
            if (coreHpText != null) coreHpText.text = $"CORE  {coreHealth.CurrentHealth:0} / {coreHealth.MaxHealth:0}";
        }

        if (gameOverPanel != null)
        {
            // Wait for the slow-motion explosion to finish (time fully frozen) before covering it.
            bool showGameOver = GameManager.Instance != null && GameManager.Instance.IsGameOver && Time.timeScale == 0f;
            if (gameOverPanel.activeSelf != showGameOver) gameOverPanel.SetActive(showGameOver);
            if (showGameOver && gameOverStatsText != null) gameOverStatsText.text = $"You held out until wave {wave}";
        }
    }

    private void UpdatePunches()
    {
        float decay = Time.unscaledDeltaTime * 5f;
        wavePunch = Mathf.Max(0f, wavePunch - decay * 0.6f);
        scrapPunch = Mathf.Max(0f, scrapPunch - decay);
        hpPunch = Mathf.Max(0f, hpPunch - decay);

        if (waveText != null) waveText.rectTransform.localScale = Vector3.one * (1f + 0.5f * wavePunch * wavePunch);
        if (scrapPill != null) scrapPill.localScale = Vector3.one * (1f + 0.15f * scrapPunch);
        if (coreHpFill != null && coreHpFill.parent != null)
        {
            float jitter = hpPunch > 0f ? Mathf.Sin(Time.unscaledTime * 80f) * 8f * hpPunch : 0f;
            ((RectTransform)coreHpFill.parent).anchoredPosition = new Vector2(jitter, ((RectTransform)coreHpFill.parent).anchoredPosition.y);
        }
    }
}
