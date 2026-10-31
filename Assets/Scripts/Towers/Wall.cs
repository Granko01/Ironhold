using System.Collections.Generic;
using UnityEngine;

// A solid block on the tower grid (wall, gate or watchtower). Enemies that walk into one stop and
// smash it before carrying on to the core, and the hero can't walk through it.
[RequireComponent(typeof(Health))]
public class Wall : MonoBehaviour
{
    // Half the footprint on X/Z, axis-aligned (walls are never rotated).
    [SerializeField] private Vector2 halfSize = new Vector2(1.45f, 1.45f);
    [SerializeField] private Color debrisColor = new Color(0.55f, 0.5f, 0.45f);
    [SerializeField] private PlacementGrid grid;

    private static readonly List<Wall> all = new List<Wall>();

    public Health Health { get; private set; }

    // An open gate: enemies and the hero go straight through.
    public bool Passable { get; set; }

    private HitFlash hitFlash;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    private void Awake()
    {
        Health = GetComponent<Health>();
        Health.Died += HandleDied;
        Health.Damaged += HandleDamaged;
        hitFlash = GetComponent<HitFlash>();
    }

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    public void SetFootprint(Vector2 halfExtents) => halfSize = halfExtents;
    public void SetGrid(PlacementGrid placementGrid) => grid = placementGrid;

    // Occupied cells aren't serialized, so every wall (pre-built or placed) claims its footprint at play start.
    private void Start()
    {
        if (grid != null) grid.OccupyWorldArea(transform.position, FootprintForGrid);
    }

    // Shrunk slightly so a footprint that ends exactly on a cell line doesn't spill into the next cell.
    private Vector3 FootprintForGrid => new Vector3(halfSize.x * 1.8f, 0f, halfSize.y * 1.8f);

    // The wall an enemy at `position` heading along `heading` would walk into within `reach`, if any.
    public static Wall FindBlocking(Vector3 position, Vector3 heading, float reach)
    {
        for (int i = 0; i < all.Count; i++)
        {
            Wall wall = all[i];
            if (wall.Passable) continue;

            Vector3 toEdge = wall.ClosestPoint(position) - position;
            toEdge.y = 0f;

            if (toEdge.sqrMagnitude > reach * reach) continue;
            // Already touching, or the wall lies ahead rather than beside/behind.
            if (toEdge.sqrMagnitude < 0.01f || Vector3.Dot(toEdge.normalized, heading) > 0.2f) return wall;
        }
        return null;
    }

    // Pushes a circle of `radius` at `position` out of every solid wall, so a character slides along them.
    public static Vector3 PushOut(Vector3 position, float radius)
    {
        // Two passes settle the corner where two blocks meet.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < all.Count; i++)
            {
                Wall wall = all[i];
                if (wall.Passable) continue;

                Vector3 closest = wall.ClosestPoint(position);
                Vector3 away = position - closest;
                away.y = 0f;
                float distSqr = away.sqrMagnitude;
                if (distSqr >= radius * radius) continue;

                if (distSqr > 0.000001f)
                {
                    position += away / Mathf.Sqrt(distSqr) * (radius - Mathf.Sqrt(distSqr));
                }
                else
                {
                    position = wall.EjectFromInside(position, radius);
                }
            }
        }
        return position;
    }

    // Centre ended up inside the box (e.g. spawned there): leave through the nearest face.
    private Vector3 EjectFromInside(Vector3 position, float radius)
    {
        Vector3 local = position - transform.position;
        float toX = halfSize.x - Mathf.Abs(local.x);
        float toZ = halfSize.y - Mathf.Abs(local.z);
        if (toX < toZ) position.x = transform.position.x + Mathf.Sign(local.x) * (halfSize.x + radius);
        else position.z = transform.position.z + Mathf.Sign(local.z) * (halfSize.y + radius);
        return position;
    }

    public Vector3 ClosestPoint(Vector3 position)
    {
        Vector3 center = transform.position;
        return new Vector3(
            Mathf.Clamp(position.x, center.x - halfSize.x, center.x + halfSize.x),
            position.y,
            Mathf.Clamp(position.z, center.z - halfSize.y, center.z + halfSize.y));
    }

    private void HandleDamaged(float amount)
    {
        if (hitFlash != null) hitFlash.Flash();
        GameFx.Debris(transform.position + Vector3.up * 1.2f, debrisColor, 3, 3f, 0.15f);
    }

    private void HandleDied()
    {
        all.Remove(this);
        if (grid != null) grid.FreeWorldArea(transform.position, FootprintForGrid);

        GameFx.Debris(transform.position + Vector3.up * 0.8f, debrisColor, 22, 6f, 0.3f);
        IsoCameraFollow.AddTrauma(0.2f);
        Sfx.Play(SfxId.Boom, 0.8f, 0.7f);
        Destroy(gameObject);
    }
}
