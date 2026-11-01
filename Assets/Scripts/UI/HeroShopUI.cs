using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class ShopItemView
{
    public RectTransform Root;
    public Button Button;
    public Image Background;
    public Image Icon;
    public Text IconLetter;
    public Text Title;
    public Text Description;
    public Text ButtonLabel;
    public Image ButtonImage;
}

// Pause-menu shop for hero gear, opened from the SHOP button. Items cost scrap, the same
// currency as towers, so buying a dog is a real trade-off against building.
public class HeroShopUI : MonoBehaviour
{
    private enum ItemId
    {
        Soldier,
        Dog,
        Bow,
    }

    [SerializeField] private Button openButton;
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text scrapText;
    [SerializeField] private ShopItemView[] views;
    [SerializeField] private PlayerShooter shooter;
    [SerializeField] private DogCompanion dogPrefab;
    [SerializeField] private TowerPlacer placer;
    [SerializeField] private SquadCommander squad;

    private static readonly int[] DogCosts = { 250, 400, 600 };
    private const int SoldierBaseCost = 120;
    private const int SoldierCostStep = 40;
    private const int MaxSoldiers = 8;
    private const int BowCost = 200;

    private static readonly Color AffordableButton = new Color(0.3f, 0.82f, 0.4f);
    private static readonly Color OwnedButton = new Color(0.35f, 0.6f, 0.95f);
    private static readonly Color LockedButton = new Color(0.3f, 0.3f, 0.33f);
    private static readonly Color SoldierColor = new Color(0.4f, 0.6f, 1f);
    private static readonly Color DogColor = new Color(0.85f, 0.6f, 0.35f);
    private static readonly Color BowColor = new Color(0.55f, 0.85f, 0.4f);
    private static readonly Color ScrapColor = new Color(1f, 0.82f, 0.3f);

    private readonly List<DogCompanion> dogs = new List<DogCompanion>();
    private bool bowOwned;
    private bool isOpen;
    private float openedAt;

    private const float TapGuardSeconds = 0.3f;

    public void Configure(Button open, GameObject panelRoot, Button close, Text scrap, ShopItemView[] itemViews,
        PlayerShooter heroShooter, DogCompanion dog, TowerPlacer towerPlacer, SquadCommander squadCommander)
    {
        openButton = open;
        panel = panelRoot;
        closeButton = close;
        scrapText = scrap;
        views = itemViews;
        shooter = heroShooter;
        dogPrefab = dog;
        placer = towerPlacer;
        squad = squadCommander;
        if (panel != null) panel.SetActive(false);
    }

    private void Awake()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (views == null) return;
        for (int i = 0; i < views.Length; i++)
        {
            ItemId id = (ItemId)i;
            views[i].Button.onClick.AddListener(() => OnItemClicked(id));
        }
    }

    private void Open()
    {
        if (isOpen || !CanOpen()) return;

        if (placer != null) placer.SelectTower(-1);
        isOpen = true;
        openedAt = Time.unscaledTime;
        panel.SetActive(true);
        Time.timeScale = 0f;
        Sfx.Play(SfxId.Coin, 0.9f, 0.6f);
        Refresh();
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    // Upgrade drafts and game over also pause with timeScale 0, so the shop stays shut during them.
    private bool CanOpen()
    {
        bool gameOver = GameManager.Instance != null && GameManager.Instance.IsGameOver;
        return !gameOver && Time.timeScale > 0f;
    }

    private void OnItemClicked(ItemId id)
    {
        if (!isOpen || Time.unscaledTime - openedAt < TapGuardSeconds) return;

        switch (id)
        {
            case ItemId.Soldier:
                if (squad == null || squad.Count >= MaxSoldiers || !TrySpend(SoldierCost)) return;
                squad.Hire();
                break;

            case ItemId.Dog:
                if (dogs.Count >= DogCosts.Length || !TrySpend(DogCosts[dogs.Count])) return;
                SpawnDog();
                break;

            case ItemId.Bow:
                if (!bowOwned)
                {
                    if (!TrySpend(BowCost)) return;
                    bowOwned = true;
                    if (shooter != null) shooter.Equip(HeroWeaponKind.Bow);
                }
                else if (shooter != null)
                {
                    // Once bought, swapping between the bow and rifle is free.
                    shooter.Equip(shooter.EquippedKind == HeroWeaponKind.Bow ? HeroWeaponKind.Rifle : HeroWeaponKind.Bow);
                    Sfx.Play(SfxId.Build, 1.3f, 0.6f);
                }
                break;
        }

        Refresh();
    }

    private bool TrySpend(int cost)
    {
        if (GameManager.Instance == null || !GameManager.Instance.SpendScrap(cost))
        {
            Sfx.Play(SfxId.CoreHit, 1.6f, 0.4f);
            return false;
        }

        Sfx.Play(SfxId.Upgrade);
        return true;
    }

    private int SoldierCost => SoldierBaseCost + SoldierCostStep * (squad != null ? squad.Count : 0);

    private void SpawnDog()
    {
        if (dogPrefab == null || shooter == null) return;

        Transform hero = shooter.transform;
        Vector3 position = hero.position - hero.forward * 1.5f;
        DogCompanion dog = Instantiate(dogPrefab, position, hero.rotation);
        dog.Initialize(hero, dogs.Count);
        dogs.Add(dog);
        GameFx.Dust(position);
    }

    private void Update()
    {
        if (openButton != null) openButton.interactable = CanOpen();
        if (isOpen) Refresh();
    }

    private void Refresh()
    {
        if (views == null || views.Length < 3) return;

        int scrap = GameManager.Instance != null ? GameManager.Instance.Scrap : 0;
        if (scrapText != null) scrapText.text = scrap.ToString();

        ShopItemView soldierView = views[(int)ItemId.Soldier];
        int soldierCount = squad != null ? squad.Count : 0;
        Show(soldierView, "S", SoldierColor, $"Hire Soldier  ({soldierCount}/{MaxSoldiers})",
            "A rifleman for your squad. Use the SQUAD button to send them anywhere on the map.");
        if (soldierCount >= MaxSoldiers) SetButton(soldierView, "MAXED", LockedButton, false);
        else SetCostButton(soldierView, SoldierCost, scrap);

        ShopItemView dogView = views[(int)ItemId.Dog];
        Show(dogView, "D", DogColor, $"Guard Dog  ({dogs.Count}/{DogCosts.Length})",
            "A loyal pup that follows you and bites enemies that get close.");
        if (dogs.Count >= DogCosts.Length) SetButton(dogView, "MAXED", LockedButton, false);
        else SetCostButton(dogView, DogCosts[dogs.Count], scrap);

        ShopItemView bowView = views[(int)ItemId.Bow];
        Show(bowView, "B", BowColor, "Longbow",
            "Slow, heavy arrows that pierce through 2 enemies. Swap back to the rifle any time.");
        if (!bowOwned) SetCostButton(bowView, BowCost, scrap);
        else if (shooter != null && shooter.EquippedKind == HeroWeaponKind.Bow) SetButton(bowView, "USE RIFLE", OwnedButton, true);
        else SetButton(bowView, "USE BOW", OwnedButton, true);
    }

    private static void Show(ShopItemView view, string letter, Color iconColor, string title, string description)
    {
        view.IconLetter.text = letter;
        view.Icon.color = iconColor;
        view.Title.text = title;
        view.Description.text = description;
    }

    private static void SetCostButton(ShopItemView view, int cost, int scrap)
    {
        bool affordable = scrap >= cost;
        SetButton(view, cost.ToString(), affordable ? AffordableButton : LockedButton, affordable);
        view.ButtonLabel.color = affordable ? Color.white : ScrapColor;
    }

    private static void SetButton(ShopItemView view, string label, Color color, bool interactable)
    {
        view.ButtonLabel.text = label;
        view.ButtonLabel.color = Color.white;
        view.ButtonImage.color = color;
        view.Button.interactable = interactable;
    }
}
