using UnityEngine;

public class TowerShopUI : MonoBehaviour
{
    [SerializeField] private TowerPlacer placer;
    [SerializeField] private TowerShopButtonView[] buttons;
    [SerializeField] private GameObject hintLabel;

    private static readonly Color SelectedColor = new Color(0.22f, 0.5f, 0.28f, 0.97f);
    private static readonly Color AffordableColor = new Color(0.1f, 0.11f, 0.14f, 0.92f);
    private static readonly Color UnaffordableColor = new Color(0.08f, 0.08f, 0.09f, 0.7f);
    private static readonly Color DimTextColor = new Color(0.55f, 0.55f, 0.58f);
    private static readonly Color ScrapColor = new Color(1f, 0.82f, 0.3f);
    private static readonly Color CantAffordColor = new Color(0.9f, 0.35f, 0.3f);

    public void Configure(TowerPlacer towerPlacer, TowerShopButtonView[] buttonViews, GameObject hint)
    {
        placer = towerPlacer;
        buttons = buttonViews;
        hintLabel = hint;

        if (hintLabel != null) hintLabel.SetActive(false);
    }

    private void Awake()
    {
        if (buttons == null) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].Button.onClick.AddListener(() => OnButtonClicked(index));
        }
    }

    private void OnButtonClicked(int index)
    {
        if (placer == null) return;
        placer.SelectTower(placer.SelectedIndex == index ? -1 : index);
    }

    private void Update()
    {
        if (placer == null || placer.Definitions == null || buttons == null) return;

        int scrap = GameManager.Instance != null ? GameManager.Instance.Scrap : 0;

        for (int i = 0; i < buttons.Length && i < placer.Definitions.Length; i++)
        {
            TowerDefinition def = placer.Definitions[i];
            TowerShopButtonView view = buttons[i];
            bool selected = placer.SelectedIndex == i;
            bool affordable = scrap >= def.cost;
            bool usable = affordable || selected;

            view.Label.text = def.displayName.ToUpperInvariant();
            view.CostLabel.text = def.cost.ToString();
            view.Button.interactable = usable;
            view.Background.color = selected ? SelectedColor : (affordable ? AffordableColor : UnaffordableColor);
            view.Label.color = usable ? Color.white : DimTextColor;
            view.CostLabel.color = affordable ? ScrapColor : CantAffordColor;

            if (view.Icon != null)
            {
                Color accent = def.accentColor;
                view.Icon.color = usable ? accent : new Color(accent.r, accent.g, accent.b, 0.35f);
            }

            view.Button.transform.localScale = Vector3.one * (selected ? 1.08f : 1f);
        }

        if (hintLabel != null) hintLabel.SetActive(placer.SelectedIndex >= 0);
    }
}
