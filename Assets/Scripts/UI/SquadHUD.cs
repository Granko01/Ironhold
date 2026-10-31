using UnityEngine;
using UnityEngine.UI;

// The SQUAD order button (shows how many soldiers are hired and what tapping it will do next), and
// the HIRE GUARD button that appears while the hero stands by a watchtower with a free post.
public class SquadHUD : MonoBehaviour
{
    [SerializeField] private SquadCommander squad;
    [SerializeField] private Button squadButton;
    [SerializeField] private Text squadLabel;
    [SerializeField] private Image squadImage;
    [SerializeField] private GameObject squadHint;
    [SerializeField] private Transform hero;
    [SerializeField] private Button hireButton;
    [SerializeField] private Text hireLabel;
    [SerializeField] private Image hireImage;
    [SerializeField] private float hireRadius = 3.5f;

    private static readonly Color FollowColor = new Color(0.35f, 0.6f, 0.95f);
    private static readonly Color ChoosingColor = new Color(1f, 0.85f, 0.3f);
    private static readonly Color DeployedColor = new Color(0.95f, 0.45f, 0.35f);
    private static readonly Color AffordableColor = new Color(0.3f, 0.82f, 0.4f);
    private static readonly Color LockedColor = new Color(0.4f, 0.4f, 0.43f);

    private Watchtower nearbyTower;

    public void Configure(SquadCommander commander, Button squadBtn, Text squadText, GameObject hint,
        Transform heroTransform, Button hireBtn, Text hireText)
    {
        hero = heroTransform;
        hireButton = hireBtn;
        hireLabel = hireText;
        hireImage = hireBtn.GetComponent<Image>();
        squad = commander;
        squadButton = squadBtn;
        squadLabel = squadText;
        squadImage = squadBtn.GetComponent<Image>();
        squadHint = hint;
    }

    private void Awake()
    {
        if (squadButton != null) squadButton.onClick.AddListener(() => squad?.OnSquadButton());
        if (hireButton != null) hireButton.onClick.AddListener(OnHireClicked);
    }

    private void OnHireClicked()
    {
        if (nearbyTower != null && !nearbyTower.TryHireGuard()) Sfx.Play(SfxId.CoreHit, 1.6f, 0.4f);
    }

    private void Update()
    {
        UpdateHireButton();

        bool hasSquad = squad != null && squad.Count > 0;
        squadButton.gameObject.SetActive(hasSquad);
        if (squadHint != null) squadHint.SetActive(SquadCommander.IsChoosing);
        if (!hasSquad) return;

        switch (squad.State)
        {
            case SquadCommander.SquadState.Choosing:
                squadLabel.text = "CANCEL";
                squadImage.color = ChoosingColor;
                break;
            case SquadCommander.SquadState.Deployed:
                squadLabel.text = "RECALL";
                squadImage.color = DeployedColor;
                break;
            default:
                squadLabel.text = $"SQUAD x{squad.Count}";
                squadImage.color = FollowColor;
                break;
        }
    }

    private void UpdateHireButton()
    {
        nearbyTower = hero != null ? Watchtower.FindHireableNear(hero.position, hireRadius) : null;
        hireButton.gameObject.SetActive(nearbyTower != null);
        if (nearbyTower == null) return;

        int scrap = GameManager.Instance != null ? GameManager.Instance.Scrap : 0;
        bool affordable = scrap >= nearbyTower.HireCost;
        hireLabel.text = $"HIRE GUARD  {nearbyTower.HireCost}";
        hireImage.color = affordable ? AffordableColor : LockedColor;
        hireButton.interactable = affordable;
    }
}
