using UnityEngine;

// Phone vibration for big hits (explosions, the core taking damage, game over). Rate-limited,
// because the stock vibration is a long buzz and a busy wave would otherwise never stop.
public static class Haptics
{
    private const float MinInterval = 0.6f;
    private static float lastPulse = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => lastPulse = -10f;

    public static void Pulse()
    {
        if (!GameSettings.Vibration) return;
        float now = Time.unscaledTime;
        if (now - lastPulse < MinInterval) return;
        lastPulse = now;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}
