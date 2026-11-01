using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays the synthesized ambience and adaptive music. All music layers start on the same DSP
// tick and loop together; only their volumes move:
//   calm   - always on (pad + bells)
//   combat - fades in while a wave has enemies left
//   boss   - joins on top during boss waves
// In gameplay the layers follow the wave state: pauses (shop, upgrade draft) duck the music and
// game over fades it out, leaving the forest. Scenes without a wave spawner (the trailer) drive
// the layers by hand with Cue(), and the music slows with slow motion there.
// It outlives scene loads, so the menu's music carries straight on into the run.
public class MusicDirector : MonoBehaviour
{
    private const float AmbienceVolume = 0.32f;
    private const float CalmVolume = 0.22f;
    private const float CombatVolume = 0.3f;
    private const float BossVolume = 0.34f;
    private const float FadeInPerSecond = 0.35f;
    private const float FadeOutPerSecond = 0.12f;

    private static MusicDirector instance;

    private EnemySpawner spawner;
    private AudioSource ambience, calm, combat, boss;
    private bool started;
    private bool ready;
    private bool snapToTargets;

    // Scripted mode (set by Cue): layer levels 0-1 and how long to reach them.
    private bool scripted;
    private float cueCalm, cueCombat, cueBoss, cueAmbience = 1f;
    private float cueFadeSeconds;

    // renderImmediately blocks for a moment instead of rendering on a worker thread, so a
    // scene that needs music from its very first frame (the trailer) gets it.
    public static void Ensure(bool renderImmediately = false)
    {
        if (instance == null) new GameObject("MusicDirector").AddComponent<MusicDirector>();
        if (renderImmediately && !instance.started)
        {
            instance.started = true;
            instance.Begin(MusicSynth.RenderAll());
        }
    }

    // Takes manual control of the layers. Levels are 0-1 of each layer's normal volume;
    // fadeSeconds 0 snaps straight there.
    public static void Cue(float calmLevel, float combatLevel, float bossLevel, float fadeSeconds, float ambienceLevel = 1f)
    {
        Ensure();
        instance.scripted = true;
        instance.cueCalm = calmLevel;
        instance.cueCombat = combatLevel;
        instance.cueBoss = bossLevel;
        instance.cueAmbience = ambienceLevel;
        instance.cueFadeSeconds = fadeSeconds;
    }

    // Volumes jump straight to the new settings (used while dragging the settings sliders).
    public static void ApplySettingsNow()
    {
        if (instance != null) instance.snapToTargets = true;
    }

    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (instance == this) instance = null;
    }

    // Each scene starts back in automatic mode; a scripted scene (the trailer) cues again in its Start.
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        spawner = FindFirstObjectByType<EnemySpawner>();
        scripted = false;
        if (!ready) return;
        calm.pitch = combat.pitch = boss.pitch = 1f;
    }

    private IEnumerator Start()
    {
        spawner = FindFirstObjectByType<EnemySpawner>();
        if (started) yield break;
        started = true;

        Task<MusicSynth.Tracks> render = Task.Run(MusicSynth.RenderAll);
        while (!render.IsCompleted) yield return null;
        if (render.IsFaulted)
        {
            Debug.LogException(render.Exception);
            yield break;
        }
        Begin(render.Result);
    }

    private void Begin(MusicSynth.Tracks tracks)
    {
        ambience = CreateLayer("Ambience", tracks.ambience);
        calm = CreateLayer("MusicCalm", tracks.calm);
        combat = CreateLayer("MusicCombat", tracks.combat);
        boss = CreateLayer("MusicBoss", tracks.boss);

        ambience.Play();
        double startAt = AudioSettings.dspTime + 0.1;
        calm.PlayScheduled(startAt);
        combat.PlayScheduled(startAt);
        boss.PlayScheduled(startAt);
        ready = true;
    }

    private AudioSource CreateLayer(string clipName, float[] data)
    {
        AudioClip clip = AudioClip.Create(clipName, data.Length, 1, MusicSynth.Rate, false);
        clip.SetData(data, 0);

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.priority = 0;
        source.volume = 0f;
        return source;
    }

    private void Update()
    {
        if (!ready) return;

        // Player settings scale everything; ambience counts as sound, not music.
        float musicSetting = GameSettings.MusicVolume;
        float soundSetting = GameSettings.SoundVolume;

        if (scripted)
        {
            FadeOver(ambience, AmbienceVolume * cueAmbience * soundSetting, AmbienceVolume);
            FadeOver(calm, CalmVolume * cueCalm * musicSetting, CalmVolume);
            FadeOver(combat, CombatVolume * cueCombat * musicSetting, CombatVolume);
            FadeOver(boss, BossVolume * cueBoss * musicSetting, BossVolume);

            // Slow motion drags the score down with it; all layers share one pitch so they stay in sync.
            float pitch = Mathf.Lerp(0.84f, 1f, Mathf.Clamp01(Time.timeScale));
            calm.pitch = combat.pitch = boss.pitch = pitch;
            snapToTargets = false;
            return;
        }

        bool gameOver = GameManager.Instance != null && GameManager.Instance.IsGameOver;
        bool fighting = spawner != null && spawner.EnemiesRemaining > 0;
        bool bossFight = fighting && spawner.IsBossWave;
        float music = (gameOver ? 0f : (Time.timeScale <= 0f ? 0.6f : 1f)) * musicSetting;

        Fade(ambience, AmbienceVolume * soundSetting);
        Fade(calm, CalmVolume * music);
        Fade(combat, fighting ? CombatVolume * music : 0f);
        Fade(boss, bossFight ? BossVolume * music : 0f);
        snapToTargets = false;
    }

    // Unscaled time: the shop and drafts pause with timeScale 0 but audio should keep moving.
    private void Fade(AudioSource source, float target)
    {
        if (snapToTargets && source != combat && source != boss)
        {
            source.volume = target;
            return;
        }
        float speed = target > source.volume ? FadeInPerSecond : FadeOutPerSecond;
        source.volume = Mathf.MoveTowards(source.volume, target, speed * Time.unscaledDeltaTime);
    }

    private void FadeOver(AudioSource source, float target, float fullVolume)
    {
        if (cueFadeSeconds <= 0f || snapToTargets)
        {
            source.volume = target;
            return;
        }
        source.volume = Mathf.MoveTowards(source.volume, target, fullVolume / cueFadeSeconds * Time.unscaledDeltaTime);
    }
}
