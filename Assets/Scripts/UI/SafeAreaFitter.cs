using UnityEngine;

// Keeps this RectTransform inside Screen.safeArea so HUD elements clear the notch and home bar.
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private Rect appliedArea;
    private Vector2Int appliedScreenSize;

    private void OnEnable() => Apply();

    private void Update()
    {
        if (Screen.safeArea != appliedArea || appliedScreenSize.x != Screen.width || appliedScreenSize.y != Screen.height)
        {
            Apply();
        }
    }

    private void Apply()
    {
        appliedArea = Screen.safeArea;
        appliedScreenSize = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        var rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(appliedArea.xMin / Screen.width, appliedArea.yMin / Screen.height);
        rect.anchorMax = new Vector2(appliedArea.xMax / Screen.width, appliedArea.yMax / Screen.height);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
