using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class UpgradeCardView
{
    public RectTransform Root;
    public Button Button;
    public Image Background;
    public Image Icon;
    public Text IconLetter;
    public Text Title;
    public Text Description;
    public Text Rarity;
}

// Pauses between waves and offers a pick of upgrade cards.
public class UpgradeDraft : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Text headerText;
    [SerializeField] private UpgradeCardView[] cards;

    private const float TapGuardSeconds = 0.45f;
    private const float CardIntroSeconds = 0.25f;
    private const float CardStagger = 0.08f;

    private static readonly Color CommonBackground = new Color(0.12f, 0.13f, 0.17f, 0.98f);
    private static readonly Color RareBackground = new Color(0.1f, 0.17f, 0.32f, 0.98f);
    private static readonly Color EpicBackground = new Color(0.24f, 0.11f, 0.32f, 0.98f);
    private static readonly Color CommonLabel = new Color(0.7f, 0.72f, 0.78f);
    private static readonly Color RareLabel = new Color(0.45f, 0.75f, 1f);
    private static readonly Color EpicLabel = new Color(0.9f, 0.55f, 1f);

    private List<UpgradeDefinition> hand = new List<UpgradeDefinition>();
    private bool picking;
    private int chosenIndex = -1;
    private float openedAt;

    public void Configure(GameObject panelRoot, Text header, UpgradeCardView[] cardViews)
    {
        panel = panelRoot;
        headerText = header;
        cards = cardViews;
        if (panel != null) panel.SetActive(false);
    }

    private void Awake()
    {
        if (cards == null) return;
        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;
            cards[i].Button.onClick.AddListener(() => OnCardClicked(index));
        }
    }

    public IEnumerator Run(int clearedWave)
    {
        hand = UpgradeCatalog.Draw(cards.Length);
        if (hand.Count == 0) yield break;

        if (headerText != null) headerText.text = $"WAVE {clearedWave} CLEARED";
        for (int i = 0; i < cards.Length; i++)
        {
            bool hasCard = i < hand.Count;
            cards[i].Root.gameObject.SetActive(hasCard);
            if (hasCard) Populate(cards[i], hand[i]);
        }

        chosenIndex = -1;
        picking = true;
        openedAt = Time.unscaledTime;
        panel.SetActive(true);
        Time.timeScale = 0f;
        Sfx.Play(SfxId.Coin, 0.8f);

        while (picking) yield return null;

        // Let the chosen card's pop play before resuming.
        yield return new WaitForSecondsRealtime(0.4f);
        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    private void Populate(UpgradeCardView view, UpgradeDefinition upgrade)
    {
        int stacks = UpgradeCatalog.TimesTaken(upgrade);
        view.Title.text = stacks > 0 ? $"{upgrade.Title}  {ToRoman(stacks + 1)}" : upgrade.Title;
        view.Description.text = upgrade.Description;
        view.Icon.color = upgrade.IconColor;
        view.IconLetter.text = upgrade.IconLetter;
        view.Rarity.text = upgrade.Rarity.ToString().ToUpperInvariant();

        switch (upgrade.Rarity)
        {
            case UpgradeRarity.Epic:
                view.Background.color = EpicBackground;
                view.Rarity.color = EpicLabel;
                break;
            case UpgradeRarity.Rare:
                view.Background.color = RareBackground;
                view.Rarity.color = RareLabel;
                break;
            default:
                view.Background.color = CommonBackground;
                view.Rarity.color = CommonLabel;
                break;
        }
    }

    private void OnCardClicked(int index)
    {
        // The guard stops a finger still on the joystick from instantly picking a card.
        if (!picking || index >= hand.Count || Time.unscaledTime - openedAt < TapGuardSeconds) return;

        UpgradeCatalog.Take(hand[index]);
        chosenIndex = index;
        picking = false;
        Sfx.Play(SfxId.Upgrade);
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf || cards == null) return;

        float elapsed = Time.unscaledTime - openedAt;
        for (int i = 0; i < cards.Length; i++)
        {
            RectTransform root = cards[i].Root;
            if (!root.gameObject.activeSelf) continue;

            float scale;
            if (chosenIndex < 0)
            {
                float t = Mathf.Clamp01((elapsed - i * CardStagger) / CardIntroSeconds);
                scale = EaseOutBack(t);
            }
            else
            {
                scale = i == chosenIndex ? 1.08f : Mathf.MoveTowards(root.localScale.x, 0f, Time.unscaledDeltaTime * 4f);
            }
            root.localScale = Vector3.one * scale;
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private static string ToRoman(int n)
    {
        string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        return n >= 1 && n <= numerals.Length ? numerals[n - 1] : n.ToString();
    }
}
