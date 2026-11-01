using UnityEngine;
using UnityEngine.UI;

// The FPS / TOP toggle. Swaps which HUD pieces are shown: building and squad orders need taps on
// the map, so they're hidden in first-person, where the FIRE button and crosshair appear instead.
public class ViewModeHUD : MonoBehaviour
{
    [SerializeField] private Button toggleButton;
    [SerializeField] private Text toggleLabel;
    [SerializeField] private TowerPlacer placer;
    [SerializeField] private GameObject[] topViewOnly;
    [SerializeField] private GameObject[] firstPersonOnly;

    public void Configure(Button button, Text label, TowerPlacer towerPlacer, GameObject[] topOnly, GameObject[] fpsOnly)
    {
        toggleButton = button;
        toggleLabel = label;
        placer = towerPlacer;
        topViewOnly = topOnly;
        firstPersonOnly = fpsOnly;
    }

    private void Awake()
    {
        if (toggleButton != null) toggleButton.onClick.AddListener(OnToggle);
        Apply(false);
    }

    private void OnToggle()
    {
        if (FirstPersonView.Instance == null || Time.timeScale <= 0f) return;

        // Leave any tap-on-the-map mode first so the next tap isn't swallowed.
        if (placer != null) placer.SelectTower(-1);
        if (SquadCommander.IsChoosing) SquadCommander.Instance.OnSquadButton();

        FirstPersonView.Instance.Toggle();
        Apply(FirstPersonView.IsActive);
        Sfx.Play(SfxId.Build, 1.5f, 0.5f);
    }

    private void Apply(bool firstPerson)
    {
        if (toggleLabel != null) toggleLabel.text = firstPerson ? "TOP" : "FPS";
        foreach (GameObject go in topViewOnly) if (go != null) go.SetActive(!firstPerson);
        foreach (GameObject go in firstPersonOnly) if (go != null) go.SetActive(firstPerson);
    }
}
