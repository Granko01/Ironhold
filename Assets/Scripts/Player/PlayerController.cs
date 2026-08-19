using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 720f;

    private void Awake()
    {
        GetComponent<Rigidbody>().isKinematic = true;
    }

    private void Update()
    {
        Vector3 moveInput = ReadMoveInput();
        transform.position += moveInput * moveSpeed * Time.deltaTime;

        if (moveInput.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveInput);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private Vector3 ReadMoveInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector3.zero;

        float x = 0f, z = 0f;
        if (kb.aKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed) x += 1f;
        if (kb.sKey.isPressed) z -= 1f;
        if (kb.wKey.isPressed) z += 1f;

        Vector3 moveInput = new Vector3(x, 0f, z);
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
        return moveInput;
    }
}
