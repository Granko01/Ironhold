using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// First-person mode for the hero. Look input turns the hero (yaw) and tilts the eye (pitch); the
// camera reads EyePosition/EyeRotation. While active, only the arms and weapon are drawn - they
// already sit at chest height in the aim pose, so they double as the on-screen viewmodel.
public class FirstPersonView : MonoBehaviour
{
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.72f, 0.1f);
    [SerializeField] private float lookSensitivity = 0.18f;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -55f;
    [SerializeField] private float maxPitch = 65f;
    [SerializeField] private Transform[] viewmodelRoots;

    public static FirstPersonView Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.active;
    public static event Action<bool> Changed;

    public Vector3 EyePosition => transform.TransformPoint(eyeOffset);
    // Recoil kick tilts the view up on top of the player's own pitch, then settles back.
    public Quaternion EyeRotation => transform.rotation * Quaternion.Euler(pitch - kick, 0f, 0f);

    private readonly List<Renderer> bodyRenderers = new List<Renderer>();
    private PlayerController controller;
    private bool active;
    private float pitch;
    private float kick;

    public void Configure(Transform[] armRoots, Vector3 eye)
    {
        viewmodelRoots = armRoots;
        eyeOffset = eye;
    }

    private void Awake()
    {
        Instance = this;
        controller = GetComponent<PlayerController>();

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (!IsViewmodel(r.transform)) bodyRenderers.Add(r);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private bool IsViewmodel(Transform t)
    {
        if (viewmodelRoots == null) return false;
        foreach (Transform root in viewmodelRoots)
        {
            if (root != null && t.IsChildOf(root)) return true;
        }
        return false;
    }

    public void Toggle() => SetActive(!active);

    public void SetActive(bool value)
    {
        if (active == value) return;
        active = value;
        pitch = 0f;

        // The body still casts its shadow so the hero doesn't vanish from the world.
        foreach (Renderer r in bodyRenderers)
        {
            if (r != null) r.shadowCastingMode = value ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }
        Changed?.Invoke(value);
    }

    // A little upward camera jolt per shot.
    public void Kick(float degrees) => kick += degrees;

    private void Update()
    {
        Vector2 look = TouchControls.ConsumeLookDelta() * lookSensitivity;

        // Desktop testing: hold the right mouse button and move the mouse to look.
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed) look += mouse.delta.ReadValue() * mouseSensitivity;

        if (!active || Time.timeScale <= 0f || (controller != null && controller.IsDead)) return;

        transform.Rotate(0f, look.x, 0f, Space.World);
        pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
        kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-14f * Time.deltaTime));
    }
}
