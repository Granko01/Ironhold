using UnityEngine;

public class IsoCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 20f, -16f);
    [SerializeField] private float orthographicSize = 16f;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Awake()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null) return;

        cam.orthographic = true;
        cam.orthographicSize = orthographicSize;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }
}
