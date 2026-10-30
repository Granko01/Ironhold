using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float sprintMultiplier = 1.6f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private Animator animator;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int DeathParam = Animator.StringToHash("Death");

    private Health health;
    private bool isDead;

    private void Awake()
    {
        GetComponent<Rigidbody>().isKinematic = true;
        if (animator == null) animator = GetComponentInChildren<Animator>();

        health = GetComponent<Health>();
        if (health != null) health.Died += HandleDeath;
    }

    private void Update()
    {
        if (isDead) return;

        Vector3 moveInput = ReadMoveInput();
        bool sprinting = IsSprintHeld() && moveInput.sqrMagnitude > 0.0001f;
        float speedMultiplier = sprinting ? sprintMultiplier : 1f;

        transform.position += moveInput * moveSpeed * speedMultiplier * Time.deltaTime;

        if (moveInput.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveInput);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        if (animator != null) animator.SetFloat(SpeedParam, moveInput.magnitude * speedMultiplier);
    }

    private bool IsSprintHeld()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && kb.leftShiftKey.isPressed;
    }

    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;
        if (animator != null) animator.SetTrigger(DeathParam);
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
