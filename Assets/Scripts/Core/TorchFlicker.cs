using UnityEngine;

// Makes a torch flame block waver: a noisy stretch and squash around its built size. Purely visual.
public class TorchFlicker : MonoBehaviour
{
    [SerializeField] private float speed = 9f;
    [SerializeField] private float amount = 0.25f;

    private Vector3 baseScale;
    private float seed;

    private void Awake()
    {
        baseScale = transform.localScale;
        seed = Random.value * 100f;
    }

    private void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
        transform.localScale = new Vector3(baseScale.x * (1f - n * amount), baseScale.y * (1f + n * amount * 2f), baseScale.z * (1f - n * amount));
    }
}
