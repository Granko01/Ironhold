using UnityEngine;
using UnityEngine.UI;

// The hero's HP bar under the scrap counter, plus the "RESPAWNING" countdown while they're down.
public class HeroHealthBar : MonoBehaviour
{
    [SerializeField] private RectTransform fill;
    [SerializeField] private Text label;
    [SerializeField] private Text respawnText;

    private static readonly Color HealthyColor = new Color(0.35f, 0.9f, 0.4f);
    private static readonly Color LowColor = new Color(1f, 0.3f, 0.25f);

    private Image fillImage;

    public void Configure(RectTransform fillRect, Text hpLabel, Text respawnLabel)
    {
        fill = fillRect;
        label = hpLabel;
        respawnText = respawnLabel;
    }

    private void Awake()
    {
        if (fill != null) fillImage = fill.GetComponent<Image>();
    }

    private void Update()
    {
        PlayerController hero = PlayerController.Instance;
        if (hero == null || hero.Health == null) return;

        float t = Mathf.Clamp01(hero.Health.CurrentHealth / hero.Health.MaxHealth);
        fill.anchorMax = new Vector2(t, 1f);
        if (fillImage != null) fillImage.color = Color.Lerp(LowColor, HealthyColor, Mathf.InverseLerp(0.2f, 0.6f, t));
        label.text = Mathf.CeilToInt(hero.Health.CurrentHealth).ToString();

        bool down = hero.IsDead;
        respawnText.gameObject.SetActive(down);
        if (down) respawnText.text = $"YOU FELL\nback in {Mathf.CeilToInt(hero.RespawnRemaining)}";
    }
}
