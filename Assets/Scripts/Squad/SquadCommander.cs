using System.Collections.Generic;
using UnityEngine;

// The hero's hired squad. Soldiers follow the hero until ordered out: tap the SQUAD button, then
// tap the map, and they run there and hold it. Tapping the button again recalls them.
public class SquadCommander : MonoBehaviour
{
    public enum SquadState
    {
        Following,
        Choosing,
        Deployed,
    }

    [SerializeField] private Soldier soldierPrefab;
    [SerializeField] private Transform hero;
    [SerializeField] private GameObject rallyMarker;

    public static SquadCommander Instance { get; private set; }

    // TouchControls routes the next ground tap here while the player is picking a destination.
    public static bool IsChoosing => Instance != null && Instance.State == SquadState.Choosing;

    public SquadState State { get; private set; } = SquadState.Following;
    public int Count => soldiers.Count;

    private readonly List<Soldier> soldiers = new List<Soldier>();
    private Vector3 rallyPoint;

    // Cancelling a choice goes back to whatever the squad was doing; the marker shows whether it's out.
    private SquadState StateBeforeChoosing => rallyMarker != null && rallyMarker.activeSelf ? SquadState.Deployed : SquadState.Following;

    public void Configure(Soldier prefab, Transform heroTransform, GameObject marker)
    {
        soldierPrefab = prefab;
        hero = heroTransform;
        rallyMarker = marker;
    }

    private void Awake()
    {
        Instance = this;
        if (rallyMarker != null) rallyMarker.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Picking a tower to build takes over the next tap, so drop out of choosing.
        if (State == SquadState.Choosing && TowerPlacer.IsPlacing) State = StateBeforeChoosing;
    }

    public void Hire()
    {
        if (soldierPrefab == null || hero == null) return;

        Vector3 spawn = hero.position - hero.forward * 1.5f;
        Soldier soldier = Instantiate(soldierPrefab, spawn, hero.rotation);
        soldiers.Add(soldier);
        GameFx.Dust(spawn);
        ApplyOrders();
    }

    // The single SQUAD button cycles: follow -> choose a spot -> (deployed) -> recall.
    public void OnSquadButton()
    {
        if (soldiers.Count == 0) return;

        switch (State)
        {
            case SquadState.Following:
                State = SquadState.Choosing;
                break;
            case SquadState.Choosing:
                State = StateBeforeChoosing;
                break;
            case SquadState.Deployed:
                Recall();
                break;
        }
    }

    public bool SendToScreenPoint(Vector2 screenPoint)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPoint);
        var ground = new Plane(Vector3.up, Vector3.zero);
        if (!ground.Raycast(ray, out float enter)) return false;

        rallyPoint = ray.GetPoint(enter);
        rallyPoint.y = 0f;
        State = SquadState.Deployed;
        if (rallyMarker != null)
        {
            rallyMarker.transform.position = rallyPoint;
            rallyMarker.SetActive(true);
        }
        Sfx.Play(SfxId.Build, 1.4f, 0.6f);
        ApplyOrders();
        return true;
    }

    private void Recall()
    {
        State = SquadState.Following;
        if (rallyMarker != null) rallyMarker.SetActive(false);
        Sfx.Play(SfxId.Build, 1.1f, 0.5f);
        ApplyOrders();
    }

    private void ApplyOrders()
    {
        for (int i = 0; i < soldiers.Count; i++)
        {
            // Holding a spot, every other soldier takes a knee - a staggered firing line.
            if (State == SquadState.Deployed) soldiers[i].HoldPosition(rallyPoint + FormationOffset(i, 1.1f));
            else soldiers[i].Follow(hero, FormationOffset(i, 2f));
            soldiers[i].SetKneeling(State == SquadState.Deployed && i % 2 == 0);
        }
    }

    // Fixed world-space slots in rings, so the squad doesn't swing around as the hero turns to aim.
    private static Vector3 FormationOffset(int index, float spacing)
    {
        int ring = index / 6;
        float angle = (index % 6) * 60f + ring * 30f + 180f;
        return Quaternion.Euler(0f, angle, 0f) * Vector3.forward * spacing * (1f + ring * 0.7f);
    }
}
