using UnityEngine;

public class TowerShopUI : MonoBehaviour
{
    [SerializeField] private TowerPlacer placer;
    [SerializeField] private TowerShopButtonView[] buttons;
    [SerializeField] private GameObject hintLabel;

    private static readonly Color SelectedColor = new Color(0.3f, 0.9f, 0.4f);
    private static readonly Color UnaffordableColor = new Color(0.55f, 0.55f, 0.55f);

    public void Configure(TowerPlacer towerPlacer, TowerShopButtonView[] buttonViews, GameObject hint)
    {
        placer = towerPlacer;
        buttons = buttonViews;
        hintLabel = hint;

        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            buttons[i].Button.onClick.AddListener(() => OnButtonClicked(index));
        }

        if (hintLabel != null) hintLabel.SetActive(false);
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
            bool selected = placer.SelectedIndex == i;
            bool affordable = scrap >= def.cost;

            buttons[i].Label.text = $"[{i + 1}] {def.displayName}\n{def.cost} scrap";
            buttons[i].Button.interactable = affordable || selected;
            buttons[i].Background.color = selected ? SelectedColor : (affordable ? Color.white : UnaffordableColor);
        }

        if (hintLabel != null) hintLabel.SetActive(placer.SelectedIndex >= 0);
    }
}
