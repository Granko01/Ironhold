using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen: punch-in logo, pulsing PLAY, best wave, and a settings sheet (music, sound,
// vibration). Starts the menu music, which carries on into the run.
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private RectTransform titleBlock;
    [SerializeField] private Button playButton;
    [SerializeField] private Text bestWaveText;
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider soundSlider;
    [SerializeField] private Button vibrationButton;
    [SerializeField] private Text vibrationLabel;
    [SerializeField] private Button nightButton;
    [SerializeField] private Text nightLabel;
    [SerializeField] private Button settingsDoneButton;
    [SerializeField] private Image fade;

    private bool starting;
    private float shownAt;

    public void Configure(RectTransform title, Button play, Text bestWave, Button settings, GameObject panel,
        Slider music, Slider sound, Button vibration, Text vibrationText, Button night, Text nightText, Button done, Image fadeImage)
    {
        titleBlock = title;
        playButton = play;
        bestWaveText = bestWave;
        settingsButton = settings;
        settingsPanel = panel;
        musicSlider = music;
        soundSlider = sound;
        vibrationButton = vibration;
        vibrationLabel = vibrationText;
        nightButton = night;
        nightLabel = nightText;
        settingsDoneButton = done;
        fade = fadeImage;
    }

    private void Awake()
    {
        Time.timeScale = 1f;
        MusicDirector.Ensure();

        playButton.onClick.AddListener(PlayGame);
        settingsButton.onClick.AddListener(() => ShowSettings(true));
        settingsDoneButton.onClick.AddListener(() => ShowSettings(false));
        vibrationButton.onClick.AddListener(ToggleVibration);
        nightButton.onClick.AddListener(ToggleNight);

        musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        soundSlider.SetValueWithoutNotify(GameSettings.SoundVolume);
        musicSlider.onValueChanged.AddListener(value =>
        {
            GameSettings.MusicVolume = value;
            MusicDirector.ApplySettingsNow();
        });
        soundSlider.onValueChanged.AddListener(value =>
        {
            GameSettings.SoundVolume = value;
            MusicDirector.ApplySettingsNow();
            Sfx.Play(SfxId.Coin, 1f, 0.8f); // preview at the new level
        });
    }

    private void Start()
    {
        settingsPanel.SetActive(false);
        RefreshLabels();

        int best = GameSettings.BestWave;
        bestWaveText.text = best > 0 ? $"BEST  ·  WAVE {best}" : "";

        shownAt = Time.unscaledTime;
        StartCoroutine(FadeTo(0f, 0.8f));
    }

    private void Update()
    {
        float t = Time.unscaledTime - shownAt;

        // Logo slams in from large after a beat, with a little overshoot.
        if (titleBlock != null)
        {
            float k = Mathf.Clamp01((t - 0.3f) / 0.45f);
            float scale = k <= 0f ? 1.6f : Mathf.LerpUnclamped(1.6f, 1f, EaseOutBack(k));
            titleBlock.localScale = Vector3.one * scale;
        }

        if (!starting) playButton.transform.localScale = Vector3.one * (1f + 0.045f * Mathf.Sin(t * 4f));
    }

    private void ShowSettings(bool show)
    {
        settingsPanel.SetActive(show);
        Sfx.Play(SfxId.Build, show ? 1.5f : 1.3f, 0.5f);
        if (!show) GameSettings.Save();
    }

    private void ToggleVibration()
    {
        GameSettings.Vibration = !GameSettings.Vibration;
        RefreshLabels();
        Sfx.Play(SfxId.Build, 1.5f, 0.5f);
        Haptics.Pulse();
    }

    private void ToggleNight()
    {
        GameSettings.NightMode = !GameSettings.NightMode;
        RefreshLabels();
        Sfx.Play(SfxId.Build, 1.5f, 0.5f);
    }

    private void RefreshLabels()
    {
        vibrationLabel.text = GameSettings.Vibration ? "VIBRATION: ON" : "VIBRATION: OFF";
        nightLabel.text = GameSettings.NightMode ? "NIGHT MODE: ON" : "NIGHT MODE: OFF";
    }

    private void PlayGame()
    {
        if (starting) return;
        starting = true;
        GameSettings.Save();
        Sfx.Play(SfxId.WaveStart);
        StartCoroutine(StartRun());
    }

    private IEnumerator StartRun()
    {
        playButton.transform.localScale = Vector3.one * 0.92f;
        yield return FadeTo(1f, 0.5f);
        SceneManager.LoadScene(SceneNames.Gameplay);
    }

    private IEnumerator FadeTo(float target, float seconds)
    {
        if (fade == null) yield break;
        fade.raycastTarget = true;
        float start = fade.color.a;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetFade(Mathf.Lerp(start, target, t / seconds));
            yield return null;
        }
        SetFade(target);
        fade.raycastTarget = target > 0.01f;
    }

    private void SetFade(float alpha)
    {
        Color c = fade.color;
        c.a = alpha;
        fade.color = c;
    }

    private static float EaseOutBack(float k)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
