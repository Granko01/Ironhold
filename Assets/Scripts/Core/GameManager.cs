using System;
using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private int startingScrap = 300;
    [SerializeField] private Health coreHealth;

    public int Scrap { get; private set; }
    public Health CoreHealth => coreHealth;
    public bool IsGameOver { get; private set; }

    // Amount and world position, so the HUD can fly a coin from where the scrap dropped.
    public event Action<int, Vector3> ScrapCollected;

    private void Awake()
    {
        Instance = this;
        Scrap = startingScrap;
        IsGameOver = false;
        Time.timeScale = 1f;
        RunModifiers.Reset();
        UpgradeCatalog.ResetRun();
    }

    private void Update()
    {
        if (!IsGameOver && coreHealth != null && RunModifiers.CoreRegenPerSecond > 0f)
        {
            coreHealth.Heal(RunModifiers.CoreRegenPerSecond * Time.deltaTime);
        }
    }

    // Subscribed at runtime: event subscriptions made by editor setup code are not serialized.
    private void OnEnable()
    {
        if (coreHealth != null) coreHealth.Died += HandleCoreDied;
    }

    private void OnDisable()
    {
        if (coreHealth != null) coreHealth.Died -= HandleCoreDied;
    }

    public bool SpendScrap(int amount)
    {
        if (amount > Scrap) return false;
        Scrap -= amount;
        return true;
    }

    public void AddScrap(int amount)
    {
        Scrap += amount;
    }

    public void AddScrap(int amount, Vector3 worldPosition)
    {
        AddScrap(amount);
        ScrapCollected?.Invoke(amount, worldPosition);
    }

    private void HandleCoreDied()
    {
        if (IsGameOver) return;
        IsGameOver = true;

        if (coreHealth != null) GameFx.Debris(coreHealth.transform.position + Vector3.up, new Color(0.4f, 0.9f, 1f), 60, 9f, 0.4f);
        IsoCameraFollow.AddTrauma(1f);
        Sfx.Play(SfxId.GameOver);

        Debug.Log("GAME OVER - Core destroyed");
        StartCoroutine(GameOverSlowMotion());
    }

    // Brief slow-motion so the core explosion plays out before everything freezes.
    private IEnumerator GameOverSlowMotion()
    {
        Time.timeScale = 0.2f;
        yield return new WaitForSecondsRealtime(1.2f);
        Time.timeScale = 0f;
    }
}
