using UnityEngine;

// A pet bought from the shop. Trots after the hero and runs down enemies that come near them,
// biting until they die. Legs, tail and head are animated procedurally (no Animator) since the
// rig is just four hinge pivots.
public class DogCompanion : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private float aggroRange = 7f;
    [SerializeField] private float leashRange = 10f;
    [SerializeField] private float biteRange = 1.2f;
    [SerializeField] private float biteDamage = 14f;
    [SerializeField] private float biteInterval = 0.6f;
    [SerializeField] private Color biteColor = new Color(1f, 0.85f, 0.55f);

    [Header("Rig")]
    [SerializeField] private Transform body;
    [SerializeField] private Transform head;
    [SerializeField] private Transform tail;
    [SerializeField] private Transform[] legs;

    private Transform owner;
    private Vector3 followOffset;
    private Enemy target;
    private float biteTimer;
    private float biteAnim;
    private float gaitPhase;
    private float bodyBaseY;
    private Vector3 headBasePos;

    public void Configure(Transform bodyPivot, Transform headPivot, Transform tailPivot, Transform[] legHinges)
    {
        body = bodyPivot;
        head = headPivot;
        tail = tailPivot;
        legs = legHinges;
    }

    // Each dog gets its own slot around the hero so a pack doesn't stack on one spot.
    public void Initialize(Transform hero, int slot)
    {
        owner = hero;
        float angle = 140f + slot * 40f;
        followOffset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 1.6f;
    }

    private void Awake()
    {
        if (body != null) bodyBaseY = body.localPosition.y;
        if (head != null) headBasePos = head.localPosition;
    }

    private void Update()
    {
        if (owner == null) return;

        biteTimer -= Time.deltaTime;
        UpdateTarget();

        Vector3 destination;
        float stopDistance;
        if (target != null)
        {
            destination = target.transform.position;
            stopDistance = biteRange * 0.8f;
        }
        else
        {
            destination = owner.position + followOffset;
            stopDistance = 0.3f;
        }

        Vector3 toDestination = destination - transform.position;
        toDestination.y = 0f;
        float distance = toDestination.magnitude;

        float speed = 0f;
        if (distance > stopDistance)
        {
            // Sprint to catch up when left far behind.
            float catchUp = target == null && distance > 4f ? 1.5f : 1f;
            speed = moveSpeed * catchUp;
            transform.position += toDestination / distance * Mathf.Min(speed * Time.deltaTime, distance - stopDistance);
        }

        Vector3 lookDirection = target != null ? toDestination : (speed > 0f ? toDestination : owner.forward);
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(lookDirection), turnSpeed * Time.deltaTime);
        }

        if (target != null && distance <= biteRange && biteTimer <= 0f) Bite();

        Animate(speed);
    }

    private void UpdateTarget()
    {
        // Give up the chase once it would drag the dog too far from the hero.
        if (target != null && (!target.IsAlive || !target.IsWithin(owner.position, leashRange))) target = null;
        if (target != null) return;

        Enemy closest = Enemy.FindClosest(transform.position, aggroRange);
        if (closest != null && closest.IsWithin(owner.position, leashRange)) target = closest;
    }

    private void Bite()
    {
        biteTimer = biteInterval;
        biteAnim = 1f;
        target.Health.TakeDamage(biteDamage * RunModifiers.Damage(TowerKind.Hero));
        GameFx.Sparks(target.transform.position + Vector3.up * 0.6f, biteColor, 5);
        Sfx.Play(SfxId.Hit, 0.75f, 0.9f);
    }

    // Scripted scenes (the trailer) move the dog themselves with this component disabled; these
    // drive the same procedural rig so it still looks alive.
    public void AnimateGait(float speed) => Animate(speed);

    // Mid-leap stretch: front legs reach forward, hind legs kick back. 0 = neutral, 1 = full stretch.
    public void PoseLeap(float stretch)
    {
        if (legs == null) return;
        for (int i = 0; i < legs.Length; i++)
        {
            if (legs[i] == null) continue;
            bool front = i < 2;
            legs[i].localRotation = Quaternion.Euler((front ? -70f : 55f) * stretch, 0f, 0f);
        }
    }

    // Side-to-side tug of the head while it has hold of something.
    public void ShakeHead(float yawDegrees)
    {
        if (head != null) head.localRotation = Quaternion.Euler(12f, yawDegrees, 0f);
    }

    private void Animate(float speed)
    {
        float moving = Mathf.Clamp01(speed / moveSpeed);
        gaitPhase += Time.deltaTime * Mathf.Lerp(0f, 14f, moving);
        float swing = Mathf.Sin(gaitPhase) * 38f * moving;

        if (legs != null)
        {
            // Diagonal pairs move together (front-left with back-right), like a real trot.
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float sign = i == 0 || i == 3 ? 1f : -1f;
                legs[i].localRotation = Quaternion.Euler(sign * swing, 0f, 0f);
            }
        }

        if (body != null)
        {
            Vector3 p = body.localPosition;
            p.y = bodyBaseY + Mathf.Abs(Mathf.Sin(gaitPhase)) * 0.05f * moving;
            body.localPosition = p;
        }

        if (tail != null)
        {
            float wagSpeed = target != null ? 10f : 18f;
            tail.localRotation = Quaternion.Euler(-35f, Mathf.Sin(Time.time * wagSpeed) * 30f, 0f);
        }

        biteAnim = Mathf.MoveTowards(biteAnim, 0f, Time.deltaTime * 5f);
        if (head != null) head.localPosition = headBasePos + Vector3.forward * Mathf.Sin(biteAnim * Mathf.PI) * 0.18f;
    }
}
