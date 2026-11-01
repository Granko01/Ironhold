using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The living scene behind the main menu: a slow camera orbit around the walled village at dusk
// while zombies claw out of the treeline, shamble up to the walls, beat on them, and sink away
// again. Zombies are puppets here (their Enemy brain is off), so towers and guards stay quiet
// and nothing gets hurt.
public class MenuBackdrop : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private Volume postFx;
    [SerializeField] private Enemy[] zombiePrefabs;
    [SerializeField] private int zombieCount = 9;

    [Header("Camera orbit")]
    [SerializeField] private float orbitRadius = 31f;
    [SerializeField] private float orbitHeight = 15f;
    [SerializeField] private float degreesPerSecond = 3.5f;
    [SerializeField] private Vector3 lookAt = new Vector3(0f, -1f, 0f);

    private const float WallRadius = 21.5f;
    private const float RiseSeconds = 1.6f;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int AttackParam = Animator.StringToHash("Attack");

    private enum Phase { Rising, Walking, Pounding, Sinking }

    private class Shambler
    {
        public Transform body;
        public Animator animator;
        public float height;
        public Vector3 direction; // from the village outwards
        public float speed;
        public Phase phase;
        public float timer;
        public float nextBlow;
    }

    private readonly List<Shambler> shamblers = new List<Shambler>();
    private float angle;

    public void Configure(Camera camera, Volume volume, Enemy[] zombies)
    {
        cam = camera;
        postFx = volume;
        zombiePrefabs = zombies;
    }

    private void Start()
    {
        angle = Random.Range(0f, 360f);

        // Far shots need everything sharp: switch off the trailer grade's depth of field
        // (on an instanced copy, so the shared profile asset is untouched).
        if (postFx != null && postFx.profile.TryGet(out DepthOfField dof)) dof.active = false;

        foreach (Watchtower tower in FindObjectsByType<Watchtower>()) tower.StationGuard();
        foreach (Transform t in FindObjectsByType<Transform>())
        {
            if (t.name == "TeamRing") t.gameObject.SetActive(false);
        }

        if (zombiePrefabs == null || zombiePrefabs.Length == 0) return;
        for (int i = 0; i < zombieCount; i++)
        {
            Shambler s = SpawnShambler();
            // Stagger the opening so they aren't all climbing out at once.
            if (s != null) s.timer = -i * 0.9f;
        }
    }

    private Shambler SpawnShambler()
    {
        Enemy prefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Length)];
        if (prefab == null) return null;

        Enemy enemy = Instantiate(prefab, transform);
        enemy.enabled = false; // puppet: no targeting, no attacks, not a tower target

        var s = new Shambler
        {
            body = enemy.transform,
            animator = enemy.GetComponentInChildren<Animator>(),
        };
        Collider collider = enemy.GetComponent<Collider>();
        s.height = collider != null ? collider.bounds.size.y : 2f;
        foreach (Transform t in enemy.GetComponentsInChildren<Transform>())
        {
            if (t.name == "TeamRing") t.gameObject.SetActive(false);
        }

        shamblers.Add(s);
        Respawn(s);
        return s;
    }

    private void Respawn(Shambler s)
    {
        float a = Random.Range(0f, 360f);
        s.direction = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
        Vector3 ground = s.direction * Random.Range(27f, 31f);
        s.body.SetPositionAndRotation(ground + Vector3.down * s.height, Quaternion.LookRotation(-s.direction));
        s.speed = Random.Range(0.55f, 0.95f);
        s.phase = Phase.Rising;
        s.timer = 0f;
        s.body.gameObject.SetActive(true);
    }

    private void Update()
    {
        OrbitCamera();
        foreach (Shambler s in shamblers) Animate(s, Time.deltaTime);
    }

    private void OrbitCamera()
    {
        if (cam == null) return;
        angle += degreesPerSecond * Time.deltaTime;
        float rad = angle * Mathf.Deg2Rad;
        float bob = Mathf.Sin(Time.time * 0.4f) * 0.6f;
        Vector3 eye = new Vector3(Mathf.Sin(rad) * orbitRadius, orbitHeight + bob, Mathf.Cos(rad) * orbitRadius);
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(lookAt - eye));
    }

    private void Animate(Shambler s, float dt)
    {
        s.timer += dt;
        if (s.timer < 0f) return; // still waiting for its staggered start
        Vector3 p = s.body.position;

        switch (s.phase)
        {
            case Phase.Rising:
            {
                if (s.timer <= dt) GameFx.Dust(new Vector3(p.x, 0f, p.z), 12);
                float k = Mathf.Clamp01(s.timer / RiseSeconds);
                s.body.position = new Vector3(p.x, Mathf.Lerp(-s.height, 0f, 1f - (1f - k) * (1f - k)), p.z);
                SetSpeed(s, 0f);
                if (k >= 1f) Next(s, Phase.Walking);
                break;
            }
            case Phase.Walking:
            {
                Vector3 flat = new Vector3(p.x, 0f, p.z);
                if (flat.magnitude <= WallRadius)
                {
                    Next(s, Phase.Pounding);
                    s.nextBlow = Random.Range(0.2f, 0.8f);
                    break;
                }
                s.body.position = Vector3.MoveTowards(flat, Vector3.zero, s.speed * dt);
                SetSpeed(s, 1f);
                break;
            }
            case Phase.Pounding:
            {
                SetSpeed(s, 0f);
                if (s.timer >= s.nextBlow)
                {
                    s.nextBlow = s.timer + Random.Range(1.1f, 1.8f);
                    if (s.animator != null) s.animator.SetTrigger(AttackParam);
                }
                if (s.timer > 7f + s.speed * 4f) Next(s, Phase.Sinking);
                break;
            }
            case Phase.Sinking:
            {
                float k = Mathf.Clamp01(s.timer / RiseSeconds);
                s.body.position = new Vector3(p.x, -s.height * k * k, p.z);
                if (k >= 1f) Respawn(s);
                break;
            }
        }
    }

    private static void Next(Shambler s, Phase phase)
    {
        s.phase = phase;
        s.timer = 0f;
    }

    private static void SetSpeed(Shambler s, float speed)
    {
        if (s.animator != null) s.animator.SetFloat(SpeedParam, speed);
    }
}
