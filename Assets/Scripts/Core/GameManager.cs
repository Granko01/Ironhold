using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private int startingScrap = 300;

    public int Scrap { get; private set; }
    public bool IsGameOver { get; private set; }

    private void Awake()
    {
        Instance = this;
        Scrap = startingScrap;
        IsGameOver = false;
        Time.timeScale = 1f;
    }

    public void RegisterCore(Health coreHealth)
    {
        coreHealth.Died += HandleCoreDied;
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

    private void HandleCoreDied()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        Time.timeScale = 0f;
        Debug.Log("GAME OVER - Core destroyed");
    }
}
