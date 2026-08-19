using UnityEngine;

public class SlowSpinner : MonoBehaviour
{
    [SerializeField] private Vector3 axis = Vector3.up;
    [SerializeField] private float degreesPerSecond = 60f;

    public void Configure(Vector3 rotationAxis, float speed)
    {
        axis = rotationAxis;
        degreesPerSecond = speed;
    }

    private void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
