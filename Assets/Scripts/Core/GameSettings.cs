using UnityEngine;

// Player settings and records, saved between sessions with PlayerPrefs.
public static class GameSettings
{
    private const string MusicKey = "settings.music";
    private const string SoundKey = "settings.sound";
    private const string VibrationKey = "settings.vibration";
    private const string NightModeKey = "settings.nightMode";
    private const string BestWaveKey = "records.bestWave";

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicKey, 0.8f);
        set => PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
    }

    // Sound effects and the forest ambience.
    public static float SoundVolume
    {
        get => PlayerPrefs.GetFloat(SoundKey, 1f);
        set => PlayerPrefs.SetFloat(SoundKey, Mathf.Clamp01(value));
    }

    public static bool Vibration
    {
        get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
        set => PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
    }

    // Gameplay at night (moonlight, torches, hero lantern) instead of daylight.
    public static bool NightMode
    {
        get => PlayerPrefs.GetInt(NightModeKey, 1) == 1;
        set => PlayerPrefs.SetInt(NightModeKey, value ? 1 : 0);
    }

    public static int BestWave => PlayerPrefs.GetInt(BestWaveKey, 0);

    // Called as each wave starts, so quitting mid-run still keeps the record.
    public static void RecordWave(int wave)
    {
        if (wave <= BestWave) return;
        PlayerPrefs.SetInt(BestWaveKey, wave);
        PlayerPrefs.Save();
    }

    public static void Save() => PlayerPrefs.Save();
}
