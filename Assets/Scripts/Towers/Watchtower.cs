using System.Collections.Generic;
using UnityEngine;

// A corner tower on the base wall. The hero can hire a guard who stands on the platform and
// shoots further than a soldier on the ground. If the tower falls, the guard goes with it.
[RequireComponent(typeof(Wall))]
public class Watchtower : MonoBehaviour
{
    [SerializeField] private Soldier guardPrefab;
    [SerializeField] private Transform guardPost;
    [SerializeField] private int hireCost = 100;
    [SerializeField] private float guardRange = 13f;

    private static readonly List<Watchtower> all = new List<Watchtower>();

    public int HireCost => hireCost;
    public bool HasGuard => guard != null;

    private Soldier guard;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    public void Configure(Soldier prefab, Transform post)
    {
        guardPrefab = prefab;
        guardPost = post;
    }

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    public bool TryHireGuard()
    {
        if (HasGuard || guardPrefab == null || guardPost == null) return false;
        if (GameManager.Instance == null || !GameManager.Instance.SpendScrap(hireCost)) return false;

        StationGuard();
        Sfx.Play(SfxId.Upgrade, 1.2f);
        return true;
    }

    // Puts a guard on the platform without charging for it (paid hiring goes through TryHireGuard;
    // scripted scenes like the trailer call this directly).
    public void StationGuard()
    {
        if (HasGuard || guardPrefab == null || guardPost == null) return;

        // Parented to the tower so the guard falls with it.
        guard = Instantiate(guardPrefab, guardPost.position, guardPost.rotation, transform);
        guard.SetRange(guardRange);
        guard.HoldPosition(guardPost.position);
        GameFx.Dust(guardPost.position, 10);
    }

    // The nearest tower within `radius` that still has a free guard post.
    public static Watchtower FindHireableNear(Vector3 position, float radius)
    {
        Watchtower closest = null;
        float closestSqr = radius * radius;
        foreach (Watchtower tower in all)
        {
            if (tower.HasGuard) continue;
            Vector3 delta = tower.transform.position - position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= closestSqr)
            {
                closestSqr = delta.sqrMagnitude;
                closest = tower;
            }
        }
        return closest;
    }
}
