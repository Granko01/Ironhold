using System.Collections.Generic;
using UnityEngine;

// A solid block on the tower grid (wall, gate or watchtower). Enemies that walk into one stop and
// smash it before carrying on to the core, and the hero can't walk through it.
[RequireComponent(typeof(Health))]
public class Wall : MonoBehaviour
{
    // Half the footprint on X/Z, axis-aligned (walls are never rotated).
    [SerializeField] private Vector2 halfSize = new Vector2(1.45f, 1.45f);
    // Height of the solid part above the wall's base, for shots flying over it.
    [SerializeField] private float height = 1.95f;
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
    public void SetHeight(float solidHeight) => height = solidHeight;

    private float Top => transform.position.y + height;

    private bool ContainsXZ(Vector3 point) =>
        Mathf.Abs(point.x - transform.position.x) <= halfSize.x && Mathf.Abs(point.z - transform.position.z) <= halfSize.y;

    // The wall the point stands on (e.g. a guard on their watchtower), so their own shots ignore it.
    public static Wall FindUnder(Vector3 point)
    {
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].ContainsXZ(point) && point.y >= all[i].Top - 0.5f) return all[i];
        }
        return null;
    }

    // Inside solid stone - shots that end up here stop.
    public static bool IsInsideSolid(Vector3 point, Wall ignore)
    {
        for (int i = 0; i < all.Count; i++)
        {
            Wall wall = all[i];
            if (wall == ignore || wall.Passable) continue;
            if (point.y < wall.Top && point.y > wall.transform.position.y && wall.ContainsXZ(point)) return true;
        }
        return false;
    }

    // How much higher than the straight line from `from` to `to` a shot must pass to clear every
    // wall in between (0 if the line is already clear), and where along the line (0-1) that is.
    public static float ClearanceNeeded(Vector3 from, Vector3 to, Wall ignore, out float atFraction)
    {
        const float margin = 0.35f;
        float worst = 0f;
        atFraction = 0.5f;

        for (int i = 0; i < all.Count; i++)
        {
            Wall wall = all[i];
            if (wall == ignore || wall.Passable) continue;
            if (!wall.SegmentOverlapXZ(from, to, out float enter, out float exit)) continue;

            // Check where the line enters and leaves the block, plus its middle.
            foreach (float t in new[] { enter, (enter + exit) * 0.5f, exit })
            {
                float lineY = Mathf.Lerp(from.y, to.y, t);
                float shortfall = wall.Top + margin - lineY;
                if (shortfall > worst)
                {
                    worst = shortfall;
                    atFraction = t;
                }
            }
        }
        return worst;
    }

    // Peak height of a parabolic lob (lift = h*4t(1-t) along the line) that clears every wall
    // between `from` and `to` - checked at each block's near face, middle and far face. 0 if the
    // straight line is already clear.
    public static float RequiredArc(Vector3 from, Vector3 to, Wall ignore)
    {
        const float margin = 0.35f;
        float arc = 0f;

        for (int i = 0; i < all.Count; i++)
        {
            Wall wall = all[i];
            if (wall == ignore || wall.Passable) continue;
            if (!wall.SegmentOverlapXZ(from, to, out float enter, out float exit)) continue;

            for (int k = 0; k <= 2; k++)
            {
                float t = Mathf.Lerp(enter, exit, k * 0.5f);
                float shortfall = wall.Top + margin - Mathf.Lerp(from.y, to.y, t);
                if (shortfall <= 0f) continue;
                float tc = Mathf.Clamp(t, 0.04f, 0.96f);
                arc = Mathf.Max(arc, shortfall / (4f * tc * (1f - tc)));
            }
        }
        return arc;
    }

    // Slab test of the segment against this wall's footprint on the ground plane.
    private bool SegmentOverlapXZ(Vector3 from, Vector3 to, out float enter, out float exit)
    {
        enter = 0f;
        exit = 1f;
        Vector3 center = transform.position;
        float[] origin = { from.x - center.x, from.z - center.z };
        float[] delta = { to.x - from.x, to.z - from.z };
        float[] half = { halfSize.x, halfSize.y };

        for (int axis = 0; axis < 2; axis++)
        {
            if (Mathf.Abs(delta[axis]) < 0.0001f)
            {
                if (Mathf.Abs(origin[axis]) > half[axis]) return false;
                continue;
            }
            float t1 = (-half[axis] - origin[axis]) / delta[axis];
            float t2 = (half[axis] - origin[axis]) / delta[axis];
            if (t1 > t2) (t1, t2) = (t2, t1);
            enter = Mathf.Max(enter, t1);
            exit = Mathf.Min(exit, t2);
            if (enter > exit) return false;
        }
        return true;
    }
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
