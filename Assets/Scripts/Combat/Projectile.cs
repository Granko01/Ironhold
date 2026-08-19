using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 3f;

    private float damage;
    private Vector3 direction;

    public void Launch(Vector3 launchDirection, float launchDamage)
    {
        direction = launchDirection.normalized;
        damage = launchDamage;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null) return;

        Health enemyHealth = enemy.GetComponent<Health>();
        enemyHealth?.TakeDamage(damage);
        Destroy(gameObject);
    }
}
