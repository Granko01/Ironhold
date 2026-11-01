using UnityEngine;
using UnityEngine.UI;

// Pooled floating damage numbers on their own overlay canvas, drawn beneath the HUD.
public class DamageNumbers : MonoBehaviour
{
    private const int PoolSize = 60;
    private const float Lifetime = 0.7f;
    private const float RiseDistance = 1.4f;
    private const float BigHitThreshold = 30f;

    private static readonly Color NormalColor = Color.white;
    private static readonly Color BigHitColor = new Color(1f, 0.85f, 0.25f);

    private static DamageNumbers instance;

    private Text[] texts;
    private Vector3[] origins;
    private float[] ages;
    private float[] baseScales;
    private int next;
    private Camera cam;

    public static void Show(Vector3 worldPosition, float amount)
    {
        if (amount < 0.5f) return;
        if (instance == null) instance = new GameObject("DamageNumbers").AddComponent<DamageNumbers>();
        instance.Spawn(worldPosition, amount);
    }

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texts = new Text[PoolSize];
        origins = new Vector3[PoolSize];
        ages = new float[PoolSize];
        baseScales = new float[PoolSize];

        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("DamageNumber", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(300f, 100f);

            Text text = go.AddComponent<Text>();
            text.font = font;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(2f, -2f);

            go.SetActive(false);
            texts[i] = text;
            ages[i] = Lifetime;
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Spawn(Vector3 worldPosition, float amount)
    {
        int i = next;
        next = (next + 1) % PoolSize;

        bool bigHit = amount >= BigHitThreshold;
        Text text = texts[i];
        text.text = Mathf.RoundToInt(amount).ToString();
        text.color = bigHit ? BigHitColor : NormalColor;
        text.fontSize = Mathf.RoundToInt(Screen.height * (bigHit ? 0.03f : 0.022f));
        text.gameObject.SetActive(true);

        origins[i] = worldPosition + new Vector3(Random.Range(-0.35f, 0.35f), 0f, Random.Range(-0.2f, 0.2f));
        ages[i] = 0f;
        baseScales[i] = bigHit ? 1.15f : 1f;
        Place(i);
    }

    private void LateUpdate()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            if (ages[i] >= Lifetime) continue;

            ages[i] += Time.deltaTime;
            if (ages[i] >= Lifetime)
            {
                texts[i].gameObject.SetActive(false);
                continue;
            }
            Place(i);
        }
    }

    private void Place(int i)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        float t = ages[i] / Lifetime;
        float rise = 1f - (1f - t) * (1f - t);
        Vector3 screen = cam.WorldToScreenPoint(origins[i] + Vector3.up * rise * RiseDistance);

        Text text = texts[i];
        if (screen.z < 0f)
        {
            text.enabled = false;
            return;
        }
        text.enabled = true;
        text.rectTransform.position = screen;

        // Quick pop on spawn, fade out over the last 40%.
        float pop = t < 0.15f ? Mathf.Lerp(1.6f, 1f, t / 0.15f) : 1f;
        text.rectTransform.localScale = Vector3.one * pop * baseScales[i];
        Color color = text.color;
        color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
        text.color = color;
    }
}
