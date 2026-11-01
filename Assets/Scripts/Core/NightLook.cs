using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Turns the gameplay scene into night when Night Mode is on (Settings, saved per player). The
// scene is built in daylight; this swaps in cold moonlight, dark blue fog and a night grade, then
// lights the fort the way it would be lit: flickering light on every torch and a warm lantern
// glow around the hero so the area you're fighting in always reads clearly. Zombie eyes, muzzle
// flashes, tracers and the core crystal all glow through the bloom.
public class NightLook : MonoBehaviour
{
    [SerializeField] private Light sun;
    [SerializeField] private Camera cam;
    [SerializeField] private Volume nightVolume;

    private static readonly Color NightFog = new Color(0.07f, 0.09f, 0.15f);
    private static readonly Color TorchColor = new Color(1f, 0.6f, 0.25f);
    private static readonly Color LanternColor = new Color(1f, 0.82f, 0.6f);

    private class Torch
    {
        public Light light;
        public float seed;
    }

    private readonly List<Torch> torches = new List<Torch>();
    private Light lantern;
    private Transform hero;

    public void Configure(Light sunLight, Camera camera, Volume volume)
    {
        sun = sunLight;
        cam = camera;
        nightVolume = volume;
    }

    private void Awake()
    {
        bool night = GameSettings.NightMode;
        if (nightVolume != null) nightVolume.weight = night ? 1f : 0f;
        if (!night)
        {
            enabled = false;
            return;
        }

        if (sun != null)
        {
            // Moon high behind the fort, so shadows fall toward the camera like the trailer's.
            sun.transform.rotation = Quaternion.Euler(52f, 150f, 0f);
            sun.color = new Color(0.6f, 0.7f, 1f);
            sun.intensity = 0.55f;
            sun.shadowStrength = 0.7f;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.24f, 0.29f, 0.48f);
        RenderSettings.ambientEquatorColor = new Color(0.17f, 0.18f, 0.27f);
        RenderSettings.ambientGroundColor = new Color(0.07f, 0.07f, 0.09f);
        RenderSettings.fogColor = NightFog;
        RenderSettings.fogStartDistance = 26f;
        RenderSettings.fogEndDistance = 64f;
        if (cam != null) cam.backgroundColor = NightFog;
    }

    private void Start()
    {
        foreach (TorchFlicker flame in FindObjectsByType<TorchFlicker>())
        {
            Light light = CreatePointLight("TorchLight", flame.transform.parent != null ? flame.transform.parent : flame.transform, TorchColor, 6.5f, 2.2f);
            light.transform.position = flame.transform.position + Vector3.up * 0.3f;
            torches.Add(new Torch { light = light, seed = Random.value * 100f });
        }

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            hero = player.transform;
            lantern = CreatePointLight("HeroLantern", null, LanternColor, 11f, 2.4f);
        }
    }

    private void LateUpdate()
    {
        foreach (Torch torch in torches)
        {
            if (torch.light == null) continue;
            float n = Mathf.PerlinNoise(torch.seed, Time.time * 7f);
            torch.light.intensity = 2.2f * (0.75f + 0.5f * n);
        }

        if (lantern != null && hero != null) lantern.transform.position = hero.position + Vector3.up * 3.2f;
    }

    private static Light CreatePointLight(string name, Transform parent, Color color, float range, float intensity)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        return light;
    }
}
