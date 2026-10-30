using UnityEngine;

// One shared particle system that every effect emits into, so hundreds of bursts cost a single
// draw setup instead of a GameObject each.
public class GameFx : MonoBehaviour
{
    [SerializeField] private Material particleMaterial;

    private static GameFx instance;
    private ParticleSystem particles;

    public static void Debris(Vector3 position, Color color, int count, float speed = 5f, float size = 0.25f)
    {
        GameFx fx = GetInstance();
        if (fx != null) fx.Emit(position, color, count, speed, size, 0.75f);
    }

    public static void Sparks(Vector3 position, Color color, int count = 4)
    {
        GameFx fx = GetInstance();
        if (fx != null) fx.Emit(position, color, count, 4f, 0.1f, 0.25f);
    }

    public static void Dust(Vector3 position, int count = 14)
    {
        GameFx fx = GetInstance();
        if (fx != null) fx.Emit(position + Vector3.up * 0.2f, new Color(0.9f, 0.82f, 0.64f), count, 3.5f, 0.35f, 0.5f);
    }

    private static GameFx GetInstance()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<GameFx>();
        if (instance == null) instance = new GameObject("GameFx").AddComponent<GameFx>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
        particles = CreateParticleSystem();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Emit(Vector3 position, Color color, int count, float speed, float size, float lifetime)
    {
        if (particles == null) return;

        var emitParams = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < count; i++)
        {
            Vector3 direction = Random.onUnitSphere;
            direction.y = Mathf.Abs(direction.y) * 1.5f + 0.3f;

            emitParams.position = position;
            emitParams.velocity = direction.normalized * speed * Random.Range(0.5f, 1.2f);
            emitParams.startSize = size * Random.Range(0.6f, 1.3f);
            emitParams.startLifetime = lifetime * Random.Range(0.7f, 1.2f);
            emitParams.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            emitParams.startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.25f));
            emitParams.angularVelocity3D = Random.insideUnitSphere * 540f;
            particles.Emit(emitParams, 1);
        }
    }

    private ParticleSystem CreateParticleSystem()
    {
        var go = new GameObject("FxParticles");
        go.transform.SetParent(transform, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = system.main;
        main.playOnAwake = false;
        main.loop = true; // Emission is off; looping just keeps the system simulating forever.
        main.maxParticles = 2000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize3D = false;
        main.startRotation3D = true;
        main.gravityModifier = 1.4f;

        var emission = system.emission;
        emission.enabled = false;

        var shape = system.shape;
        shape.enabled = false;

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.sharedMaterial = particleMaterial != null
            ? particleMaterial
            : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

        system.Play();
        return system;
    }
}
