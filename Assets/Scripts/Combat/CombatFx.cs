using System.Collections.Generic;
using UnityEngine;

// Minimal runtime-built visuals for area and chain attacks, so tower behaviors read on screen
// without needing authored particle assets.
public static class CombatFx
{
    private const float GlowBoost = 2.5f;

    private static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => materials.Clear();

    public static void Blast(Vector3 position, float radius, Color color, float duration = 0.18f)
    {
        GameObject blast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blast.name = "BlastFx";
        Object.Destroy(blast.GetComponent<Collider>());
        blast.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
        blast.transform.position = position;
        blast.transform.localScale = Vector3.one * radius * 2f;
        blast.AddComponent<ShrinkAndDestroy>().Begin(duration);
    }

    public static void Line(Vector3 from, Vector3 to, Color color, float width = 0.12f, float duration = 0.12f)
    {
        GameObject lineGo = new GameObject("LineFx");
        LineRenderer line = lineGo.AddComponent<LineRenderer>();
        line.sharedMaterial = GetMaterial(color);
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = width;
        line.endWidth = width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.Destroy(lineGo, duration);
    }

    // A split-second burst of light at a muzzle, stretched along the shot.
    public static void MuzzleFlash(Vector3 position, Vector3 forward, Color color)
    {
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "MuzzleFlashFx";
        Object.Destroy(flash.GetComponent<Collider>());
        Renderer renderer = flash.GetComponent<Renderer>();
        renderer.sharedMaterial = GetMaterial(color);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flash.transform.SetPositionAndRotation(position + forward * 0.12f, Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, Random.Range(0f, 90f)));
        flash.transform.localScale = new Vector3(0.24f, 0.24f, 0.5f);
        flash.AddComponent<ShrinkAndDestroy>().Begin(0.05f);
    }

    private static Material GetMaterial(Color color)
    {
        if (materials.TryGetValue(color, out Material material) && material != null) return material;

        // Pushed above 1.0 so bloom picks the effect up.
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetColor("_BaseColor", new Color(color.r * GlowBoost, color.g * GlowBoost, color.b * GlowBoost, 1f));
        materials[color] = material;
        return material;
    }

    private class ShrinkAndDestroy : MonoBehaviour
    {
        private float duration;
        private float elapsed;
        private Vector3 startScale;

        public void Begin(float lifetime)
        {
            duration = lifetime;
            startScale = transform.localScale;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = startScale * (1f - t * t);
        }
    }
}
