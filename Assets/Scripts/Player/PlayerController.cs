using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float sprintMultiplier = 1.6f;
    [SerializeField] private float acceleration = 45f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float bodyRadius = 0.45f;
    [SerializeField] private Animator animator;

    [Header("Survival")]
    [SerializeField] private float respawnSeconds = 5f;
    [SerializeField] private Vector3 respawnPoint = new Vector3(0f, 0f, -5f);
    [SerializeField] private float regenDelay = 4f;
    [SerializeField] private float regenPerSecond = 8f;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int DeathParam = Animator.StringToHash("Death");

    // Enemies look the hero up here to decide whether to chase them.
    public static PlayerController Instance { get; private set; }

    public Health Health => health;
    public bool IsDead => isDead;
    public float RespawnRemaining => isDead ? respawnTimer : 0f;

    private Health health;
    private PlayerShooter shooter;
    private HitFlash hitFlash;
    private Vector3 velocity;
    private bool isDead;
    private float respawnTimer;
    private float sinceDamaged;

    private void Awake()
    {
        Instance = this;
        GetComponent<Rigidbody>().isKinematic = true;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        shooter = GetComponent<PlayerShooter>();
        hitFlash = GetComponent<HitFlash>();

        health = GetComponent<Health>();
        if (health != null)
        {
            health.Died += HandleDeath;
            health.Damaged += HandleDamaged;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (isDead)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f) Respawn();
            return;
        }

        sinceDamaged += Time.deltaTime;
        if (sinceDamaged >= regenDelay && health != null) health.Heal(regenPerSecond * Time.deltaTime);

        Vector3 moveInput = ReadMoveInput();
        // In first-person the stick is relative to where the hero looks, not to the screen.
        bool firstPerson = FirstPersonView.IsActive;
        if (firstPerson) moveInput = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * moveInput;
        bool sprinting = IsSprintHeld() && moveInput.sqrMagnitude > 0.0001f;
        float speedMultiplier = sprinting ? sprintMultiplier : 1f;

        // A short ramp up/down gives the hero some weight instead of starting and stopping on a dime.
        Vector3 targetVelocity = moveInput * moveSpeed * speedMultiplier;
        velocity = Vector3.MoveTowards(velocity, targetVelocity, acceleration * Time.deltaTime);
        Vector3 start = transform.position;
        transform.position = Wall.PushOut(start + velocity * Time.deltaTime, bodyRadius);
        // Keep only the motion the walls allowed (sliding along them), so the run animation
        // doesn't play while the hero is pressed into a wall.
        if (Time.deltaTime > 0f) velocity = (transform.position - start) / Time.deltaTime;

        // While shooting, PlayerShooter owns facing; turning here as well made the two fight each frame.
        bool aiming = shooter != null && shooter.HasTarget;
        if (!aiming && !firstPerson && moveInput.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveInput);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (animator != null) animator.SetFloat(SpeedParam, velocity.magnitude / moveSpeed);
    }

    private bool IsSprintHeld()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && kb.leftShiftKey.isPressed;
    }

    private void HandleDamaged(float amount)
    {
        sinceDamaged = 0f;
        if (hitFlash != null) hitFlash.Flash();
        IsoCameraFollow.AddTrauma(0.12f);
        Sfx.Play(SfxId.CoreHit, 1.5f, 0.6f);
    }

    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;
        respawnTimer = respawnSeconds;
        velocity = Vector3.zero;
        if (animator != null) animator.SetTrigger(DeathParam);
        IsoCameraFollow.AddTrauma(0.4f);
    }

    // Back at the base with full health; the run carries on (only the core falling ends it).
    private void Respawn()
    {
        isDead = false;
        sinceDamaged = 0f;
        transform.position = respawnPoint;
        transform.rotation = Quaternion.identity;
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }
        GameFx.Dust(respawnPoint, 16);
        Sfx.Play(SfxId.Upgrade, 0.9f);
        health.Revive();
    }

    // The camera never yaws, so screen-up maps straight to world +Z for both joystick and keys.
    private Vector3 ReadMoveInput()
    {
        Vector2 stick = TouchControls.MoveInput;
        float x = stick.x, z = stick.y;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed) x += 1f;
            if (kb.sKey.isPressed) z -= 1f;
            if (kb.wKey.isPressed) z += 1f;
        }

        Vector3 moveInput = new Vector3(x, 0f, z);
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
        return moveInput;
    }
}
