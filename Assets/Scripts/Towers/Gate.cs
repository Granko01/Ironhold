using UnityEngine;

// Double doors in the base wall. They swing open while the hero is close and shut behind them.
// Open doors let anything through - including enemies that happen to be heading that way.
[RequireComponent(typeof(Wall))]
public class Gate : MonoBehaviour
{
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;
    [SerializeField] private Transform hero;
    [SerializeField] private float openDistance = 4.5f;
    [SerializeField] private float openAngle = 100f;
    [SerializeField] private float swingSpeed = 3.5f;

    private Wall wall;
    private float openAmount;
    private bool wasOpen;

    public void Configure(Transform left, Transform right)
    {
        leftDoor = left;
        rightDoor = right;
    }

    public void SetHero(Transform heroTransform) => hero = heroTransform;

    private void Awake()
    {
        wall = GetComponent<Wall>();
    }

    private void Update()
    {
        // A fallen hero lying by the gate shouldn't hold it open for the horde.
        bool heroDown = PlayerController.Instance != null && PlayerController.Instance.IsDead;
        bool wantOpen = hero != null && !heroDown && Flat(hero.position - transform.position).sqrMagnitude <= openDistance * openDistance;
        openAmount = Mathf.MoveTowards(openAmount, wantOpen ? 1f : 0f, swingSpeed * Time.deltaTime);

        // Solid again only once the doors are nearly shut, so the hero is never trapped mid-swing.
        bool open = openAmount > 0.15f;
        wall.Passable = open;
        if (open != wasOpen)
        {
            Sfx.Play(SfxId.Build, open ? 0.7f : 0.55f, 0.5f);
            wasOpen = open;
        }

        float angle = Mathf.SmoothStep(0f, openAngle, openAmount);
        if (leftDoor != null) leftDoor.localRotation = Quaternion.Euler(0f, -angle, 0f);
        if (rightDoor != null) rightDoor.localRotation = Quaternion.Euler(0f, angle, 0f);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
