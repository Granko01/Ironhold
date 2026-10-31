using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Plays the ~32 second vertical (9:16) trailer for TikTok/Reels/Shorts. Everything on screen is the
// real game running - towers, walls, guards and zombies use their normal scripts. The director only
// spawns the horde, moves the camera and hero through a fixed shot list, and handles the film
// language: handheld sway, focus pulls, slow-motion beats, flashes, dips to black and captions.
// Captions sit in the upper-middle of the frame, clear of the app UI at the bottom and right edge.
public class TrailerDirector : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform core;
    [SerializeField] private Transform hero;
    [SerializeField] private Wall wallToBreak;
    [SerializeField] private Volume postFx;
    [SerializeField] private Vector3 towerShotCenter = new Vector3(-8f, 0f, -12.5f);

    [Header("Prefabs")]
    [SerializeField] private Enemy walkerPrefab;
    [SerializeField] private Enemy runnerPrefab;
    [SerializeField] private Enemy brutePrefab;
    [SerializeField] private Enemy spitterPrefab;
    [SerializeField] private Enemy bomberPrefab;
    [SerializeField] private Enemy bossPrefab;
    [SerializeField] private Soldier soldierPrefab;
    [SerializeField] private DogCompanion dogPrefab;

    [Header("UI")]
    [SerializeField] private Image fade;
    [SerializeField] private Image flash;
    [SerializeField] private Text caption;
    [SerializeField] private Text title;
    [SerializeField] private Text subtitle;
    [SerializeField] private Text callToAction;
    [SerializeField] private Text comingSoon;

    [Header("Horde")]
    [SerializeField] private float hordeInterval = 0.32f;
    [SerializeField] private int maxEnemies = 40;

    private const float DefaultFov = 50f;

    private Health heroHealth;
    private Animator heroAnimator;
    private DepthOfField depthOfField;
    private Enemy boss;
    private bool hordeActive;
    private float shake;
    private float aperture = 5.6f;

    // Staging for the bigger base: the south gate line, the wall block that breaks (the ambush
    // happens just inside it) and the soldiers holding the line east of it.
    private const float GateZ = -16.5f;
    private static readonly Vector3 BreachPoint = new Vector3(4.5f, 0f, -16.5f);
    private static readonly Vector3[] DefenderLine =
    {
        new Vector3(8f, 0f, -12f), new Vector3(10.5f, 0f, -12.5f), new Vector3(13f, 0f, -12f),
    };

    private PlayerShooter heroShooter;
    private Soldier victim;
    private Enemy ambusher;
    private Vector3 bodyMiddle;
    private DogCompanion ambushDog;
    private Coroutine captionRoutine;
    private Vector2 captionBasePosition;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly Color BloodColor = new Color(0.55f, 0.06f, 0.05f);
    private static readonly int RoarParam = Animator.StringToHash("Roar");

    public void Configure(Camera camera, Transform coreTransform, Transform heroTransform, Wall breakableWall, Volume volume,
        Enemy walker, Enemy runner, Enemy brute, Enemy spitter, Enemy bomber, Enemy bossEnemy,
        Soldier soldier, DogCompanion dog,
        Image fadeImage, Image flashImage, Text captionText, Text titleText, Text subtitleText, Text ctaText, Text comingSoonText)
    {
        cam = camera;
        core = coreTransform;
        hero = heroTransform;
        wallToBreak = breakableWall;
        postFx = volume;
        walkerPrefab = walker;
        runnerPrefab = runner;
        brutePrefab = brute;
        spitterPrefab = spitter;
        bomberPrefab = bomber;
        bossPrefab = bossEnemy;
        soldierPrefab = soldier;
        dogPrefab = dog;
        fade = fadeImage;
        flash = flashImage;
        caption = captionText;
        title = titleText;
        subtitle = subtitleText;
        callToAction = ctaText;
        comingSoon = comingSoonText;
    }

    private void Awake()
    {
        heroHealth = hero.GetComponent<Health>();
        heroAnimator = hero.GetComponentInChildren<Animator>();
        heroShooter = hero.GetComponent<PlayerShooter>();
        cam.fieldOfView = DefaultFov;

        // .profile (not sharedProfile) works on a runtime copy, so focus pulls never edit the asset.
        if (postFx != null) postFx.profile.TryGet(out depthOfField);

        captionBasePosition = caption.rectTransform.anchoredPosition;
        SetAlpha(fade, 1f);
        SetAlpha(flash, 0f);
        foreach (Text text in new[] { caption, title, subtitle, callToAction, comingSoon }) SetAlpha(text, 0f);
    }

    private void Update()
    {
        // The hero is the star - they never go down mid-trailer. The ambushing zombie is kept alive
        // too, until the dog gets it, so stray tower fire can't spoil the beat.
        if (heroHealth != null) heroHealth.Heal(1000f);
        if (ambusher != null) ambusher.Health.Heal(1000f);
        shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.5f);
    }

    private void OnDisable() => Time.timeScale = 1f;

    private IEnumerator Start()
    {
        // Guards are already on the corner towers, and soldiers already hold the line by the
        // south wall, when the base is revealed.
        foreach (Watchtower tower in FindObjectsByType<Watchtower>()) tower.StationGuard();
        SpawnDefenders();
        // The team circles under characters are gameplay UI - they'd look out of place in a film.
        foreach (Transform t in FindObjectsByType<Transform>()) if (t.name == "TeamRing") t.gameObject.SetActive(false);

        // 1. Hook (no black open - viewers decide in the first second): straight in, low and close,
        // as the first zombie bursts out of the dirt right in front of the lens.
        StartCoroutine(FadeTo(fade, 0f, 0.35f));
        Sfx.Play(SfxId.Boom, 0.35f, 0.6f);
        Vector3 hookSpot = new Vector3(-3f, 0f, -28f);
        StartCoroutine(SpawnHookZombies(hookSpot));
        ShowWhisper("WHEN NIGHT FALLS...", 0.15f, 1.1f);
        StartCoroutine(After(1.9f, () => ShowWhisper("THE DEAD RISE", 0f, 1.3f)));
        aperture = 1.8f;
        yield return Shot(3.6f, k => Place(Vector3.Lerp(new Vector3(-4.7f, 0.7f, -30.7f), new Vector3(-4.3f, 0.95f, -30.1f), Ease(k)), hookSpot + Vector3.up * 0.8f));

        // 2. Reveal: drone push over the trees onto the walled village. The horde starts now.
        hordeActive = true;
        StartCoroutine(Horde());
        Spawn(brutePrefab, BreachPoint + new Vector3(0f, 0f, -4.5f), emerge: true);
        Punch("DEFEND\nYOUR BASE");
        aperture = 9f;
        yield return Shot(3.5f, k => Place(Vector3.Lerp(new Vector3(0f, 24f, -46f), new Vector3(0f, 15f, -29f), Ease(k)), Vector3.Lerp(core.position + new Vector3(0f, 0f, -8f), core.position, Ease(k))));

        // 3. Wall: low by the south wall; one block bursts in slow motion with a flash.
        Vector3 wallFocus = wallToBreak != null ? wallToBreak.transform.position : BreachPoint;
        Punch("HOLD\nTHE WALL");
        aperture = 3.5f;
        StartCoroutine(BreakWallAfter(2.2f));
        yield return Shot(3.5f, k => Place(Vector3.Lerp(new Vector3(11.5f, 2.2f, -22f), new Vector3(9.5f, 2.6f, -21f), Ease(k)), wallFocus + new Vector3(0f, 1f, -1.5f), roll: Mathf.Lerp(-4f, -1f, k)));

        // 4. Towers: slow orbit across the tower line as everything opens fire.
        Punch("UNLEASH\nYOUR ARSENAL");
        aperture = 5.6f;
        yield return Shot(4f, k =>
        {
            // Swings from straight south of the towers toward (but well clear of) the corner watchtower.
            float angle = Mathf.Lerp(175f, 220f, Ease(k)) * Mathf.Deg2Rad;
            Vector3 orbit = new Vector3(Mathf.Sin(angle) * 9f, 4.2f, Mathf.Cos(angle) * 9f);
            Place(towerShotCenter + orbit, towerShotCenter + new Vector3(0f, 1.4f, -2.5f));
        });

        // 4b. Cole: he turns from the breach to throw the hero a thumbs-up - and a zombie that came
        // through the gap leaps onto his back. He goes down; it starts to feed.
        yield return ColeIsJumped();

        // 5. Ambush: chase camera behind the hero, sprinting in from the edge of the base with
        // their dog. As they pull up, the view settles on a zombie hunched over a fallen soldier.
        heroShooter.enabled = false;
        Vector3 runFrom = new Vector3(-8.5f, 0f, -2.5f);
        Vector3 runTo = bodyMiddle + new Vector3(-1.2f, 0f, 3.4f);
        hero.SetPositionAndRotation(runFrom, Quaternion.LookRotation(runTo - runFrom));
        SpawnHeroDog(runFrom);
        ShowWhisper("NO ONE IS SAFE", 1.6f, 1.4f);
        aperture = 6f;
        // The chase camera and the dog's heel spot follow the run path, not the hero's facing, so
        // neither swings around when the hero turns at the end.
        Quaternion runHeading = Quaternion.LookRotation(Flat(runTo - runFrom));
        heroRunSpeed = 0f;
        Vector3 chaseEye = runFrom + runHeading * ChaseOffset;
        yield return Shot(3.8f, k =>
        {
            float remaining = RunHero(runTo, 5.5f);
            // Face the run, then - only once nearly stopped - turn to what they've found.
            FaceHeroToward(remaining > 0.8f ? runTo : bodyMiddle);
            DogAtHeel(runHeading);

            Vector3 desiredEye = hero.position + runHeading * ChaseOffset;
            chaseEye = Vector3.Lerp(chaseEye, desiredEye, 1f - Mathf.Exp(-6f * Time.deltaTime));
            Vector3 ahead = hero.position + runHeading * Vector3.forward * 5f + Vector3.up;
            Vector3 scene = bodyMiddle + Vector3.up * 0.5f;
            Place(chaseEye, Vector3.Lerp(ahead, scene, Ease(Mathf.InverseLerp(0.35f, 0.8f, k))));
        });
        if (heroAnimator != null) heroAnimator.SetFloat(SpeedParam, 0f);

        // 6. Dog: low side angle as the hero's dog launches itself onto the zombie (slow motion).
        Vector3 struggle = bodyMiddle;
        aperture = 4.5f;
        StartCoroutine(DogPounce(0.3f));
        yield return Shot(2.6f, k => Place(Vector3.Lerp(struggle + new Vector3(5.5f, 1.5f, 3.4f), struggle + new Vector3(4.8f, 1.4f, 2.8f), Ease(k)), struggle + new Vector3(-0.8f, 0.6f, 1.2f), roll: -2f));
        heroShooter.enabled = true;

        // 7. Defenders: low at the east end of the line, looking down it - the soldiers stack up
        // one behind another, firing toward the wall. A slow push in, well clear of the stonework.
        Punch("STAND\nTOGETHER");
        aperture = 4f;
        Vector3 lineEnd = DefenderLine[DefenderLine.Length - 1];
        Vector3 lineStart = DefenderLine[0];
        yield return Shot(3f, k =>
        {
            Vector3 eye = lineEnd + Vector3.Lerp(new Vector3(1.3f, 1.05f, -1.4f), new Vector3(0.8f, 1.15f, -1.1f), Ease(k));
            Place(eye, lineStart + new Vector3(-2.5f, 1.3f, 0.2f));
        });

        // 8. Boss: dutch-angle dolly zoom while the giant roars in slow motion.
        Punch("FACE\nTHE HORDE");
        // The giant claws its way out of the ground in front of the camera.
        boss = Spawn(bossPrefab, new Vector3(2f, 0f, GateZ - 10.5f), emerge: true);
        Vector3 bossPos = boss != null ? boss.transform.position : new Vector3(2f, 0f, GateZ - 10.5f);
        if (boss != null)
        {
            Animator bossAnimator = boss.GetComponentInChildren<Animator>();
            if (bossAnimator != null) bossAnimator.SetTrigger(RoarParam);
        }
        shake = 0.8f;
        Sfx.Play(SfxId.Boom, 0.4f);
        StartCoroutine(SlowMotion(0.35f, 0.8f));
        aperture = 2.2f;
        yield return Shot(3f, k =>
        {
            if (boss != null) bossPos = boss.transform.position;
            // Dolly zoom: the camera closes in while the lens widens, so the boss holds its size
            // while the world behind it stretches away.
            Vector3 eye = bossPos + Vector3.Lerp(new Vector3(6f, 0.6f, 7f), new Vector3(3.6f, 0.9f, 5f), Ease(k));
            cam.fieldOfView = Mathf.Lerp(38f, 58f, Ease(k));
            Place(eye, bossPos + Vector3.up * 4f, roll: Mathf.Lerp(9f, 5f, k));
        });
        cam.fieldOfView = DefaultFov;

        // 9. Face-off: the hero plants themselves in front of the giant, firing; it rears back
        // to smash them - and we cut to black just before the blow lands.
        yield return FaceOff();

        // 10. Title on black: logo, then the question that gets people commenting.
        HideCaption();
        hordeActive = false;
        StartCoroutine(PunchIn(title, 0.5f));
        StartCoroutine(PunchIn(subtitle, 1f));
        StartCoroutine(PulseCallToAction(1.7f));
        StartCoroutine(FadeTo(comingSoon, 1f, 0.5f, delay: 2.4f));
        yield return new WaitForSecondsRealtime(4.6f);

#if UNITY_EDITOR
        // Stops play mode, which also ends a Unity Recorder capture cleanly.
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator After(float seconds, Action action)
    {
        yield return new WaitForSeconds(seconds);
        action();
    }

    // One continuous beat so there's no doubt who it is: Cole turns to camera with a thumbs-up, a
    // zombie through the breach leaps onto his back, and he's driven face-down toward the camera.
    // The zombie ends crouched at his side, feeding - the pose the run-in shot later reveals.
    private IEnumerator ColeIsJumped()
    {
        if (victim == null) yield break;

        victim.enabled = false;
        Animator victimAnimator = victim.GetComponentInChildren<Animator>();
        if (victimAnimator != null) victimAnimator.enabled = false;
        Transform shoulder = FindChild(victim.transform, "ArmR");
        Transform elbow = FindChild(victim.transform, "ArmR_Elbow");
        Quaternion shoulderRest = shoulder != null ? shoulder.localRotation : Quaternion.identity;
        Quaternion elbowRest = elbow != null ? elbow.localRotation : Quaternion.identity;

        Vector3 feet = victim.transform.position;
        Vector3 camFrom = feet + new Vector3(-1.6f, 1.55f, 2.5f);
        Vector3 camTo = feet + new Vector3(-1.35f, 1.5f, 2.15f);
        Vector3 toCamera = Flat(camFrom - feet).normalized;
        Quaternion patrolling = Quaternion.Euler(0f, 90f, 0f);
        Quaternion turned = Quaternion.LookRotation(toCamera);

        // Where everything ends up: face-down, head toward the camera, zombie crouched at his side.
        bodyMiddle = feet + toCamera * 1f;
        Vector3 side = Vector3.Cross(Vector3.up, toCamera) * 0.75f;
        Vector3 crouchPos = bodyMiddle + side + Vector3.down * 0.5f;
        Quaternion crouchRot = Quaternion.LookRotation(-side) * Quaternion.Euler(38f, 0f, 0f);
        Quaternion lyingRot = Quaternion.LookRotation(Vector3.down, toCamera);

        ShowWhisper("SERGEANT LEO", 0.4f, 1.4f);
        aperture = 2.6f;
        Vector3 head = feet + Vector3.up * 1.75f;
        bool jumped = false;
        bool feeding = false;
        float fallStart = -1f;

        // Beat timings (seconds into the shot).
        const float turnAt = 1f, raiseAt = 1.45f, appearAt = 1.8f, leapAt = 2.05f, leapSeconds = 0.35f, shotSeconds = 4f;

        // The zombie appears in the gap behind him, then lunges.
        Vector3 leapFrom = BreachPoint + new Vector3(0f, 0f, 1.7f);
        StartCoroutine(After(appearAt, () =>
        {
            ambusher = Spawn(walkerPrefab, leapFrom, emerge: false);
            if (ambusher == null) return;
            ambusher.enabled = false;
            ambusher.transform.rotation = Quaternion.LookRotation(Flat(feet - leapFrom));
            Animator za = ambusher.GetComponentInChildren<Animator>();
            if (za != null) za.SetTrigger("Attack");
        }));

        yield return Shot(shotSeconds, k =>
        {
            float time = k * shotSeconds;
            if (!jumped)
            {
                // Looking along the wall, he turns to camera, then raises a thumb.
                victim.transform.rotation = Quaternion.Slerp(patrolling, turned, Ease(Mathf.Clamp01((time - turnAt) / 0.5f)));
                float raise = Ease(Mathf.Clamp01((time - raiseAt) / 0.3f));
                if (shoulder != null) shoulder.localRotation = Quaternion.Slerp(shoulderRest, Quaternion.Euler(-70f, 10f, -12f), raise);
                if (elbow != null) elbow.localRotation = Quaternion.Slerp(elbowRest, Quaternion.Euler(-105f, 0f, 0f), raise);
            }

            // The leap: a low arc onto his back, slowed right down as it starts.
            if (ambusher != null && time >= leapAt && fallStart < 0f)
            {
                if (time - Time.deltaTime < leapAt) StartCoroutine(SlowMotion(0.35f, 0.6f));
                float lk = Mathf.Clamp01((time - leapAt) / leapSeconds);
                // Feet leave the ground at the gap and come down right behind him, arms on his back.
                Vector3 landing = feet - toCamera * 0.45f;
                Vector3 p = Vector3.Lerp(leapFrom, landing, lk) + Vector3.up * (0.7f * 4f * lk * (1f - lk));
                ambusher.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Flat(feet - leapFrom)) * Quaternion.Euler(Mathf.Lerp(10f, 30f, lk), 0f, 0f));
                if (lk >= 1f)
                {
                    jumped = true;
                    fallStart = time;
                    GameFx.Debris(head, BloodColor, 16, 4f, 0.18f);
                    Sfx.Play(SfxId.CoreHit, 0.7f, 0.9f);
                    shake = 0.5f;
                    PoseLimb(victim.transform, "ArmL", new Vector3(-150f, 0f, -20f));
                    PoseLimb(victim.transform, "ArmR", new Vector3(-150f, 0f, 20f));
                    if (elbow != null) elbow.localRotation = elbowRest;
                }
            }

            // He's driven face-down; the zombie rides him down and settles into a crouch.
            if (fallStart >= 0f)
            {
                float fk = Ease(Mathf.Clamp01((time - fallStart) / 0.4f));
                victim.transform.SetPositionAndRotation(Vector3.Lerp(feet, feet + Vector3.up * 0.18f, fk), Quaternion.Slerp(turned, lyingRot, fk));
                if (ambusher != null)
                {
                    Transform z = ambusher.transform;
                    z.SetPositionAndRotation(Vector3.Lerp(z.position, crouchPos, fk), Quaternion.Slerp(z.rotation, crouchRot, fk));
                }
                // Linger on it: the feeding starts while we're still on the shot.
                if (!feeding && time - fallStart > 0.45f)
                {
                    feeding = true;
                    StartCoroutine(Feeding());
                }
            }

            Place(Vector3.Lerp(camFrom, camTo, Ease(k)), fallStart >= 0f ? Vector3.Lerp(head, bodyMiddle + Vector3.up * 0.4f, Ease(Mathf.Clamp01((time - fallStart) / 0.6f))) : head);
        });

        // Lock in the final pose for later, rifle gone, and start the feeding.
        victim.transform.SetPositionAndRotation(feet + Vector3.up * 0.18f, lyingRot);
        PoseLimb(victim.transform, "LegL", new Vector3(0f, 0f, -6f));
        PoseLimb(victim.transform, "LegR", new Vector3(0f, 0f, 9f));
        Transform rifle = FindChild(victim.transform, "Rifle");
        if (rifle != null) rifle.gameObject.SetActive(false);
        if (ambusher != null) ambusher.transform.SetPositionAndRotation(crouchPos, crouchRot);
        if (!feeding) StartCoroutine(Feeding());
    }

    // Hero steps up to the boss and opens fire; it winds up a haymaker in slow motion and the
    // frame cuts to black with the fist at the top of the swing.
    private IEnumerator FaceOff()
    {
        heroShooter.enabled = true;
        hero.SetPositionAndRotation(new Vector3(1.5f, 0f, GateZ - 4.5f), Quaternion.Euler(0f, 180f, 0f));
        if (heroAnimator != null) heroAnimator.SetFloat(SpeedParam, 0f);

        Animator bossAnimator = null;
        // The boss stays a live enemy (the hero's auto-aim fires at it); at slow-motion speed it
        // barely moves in the second this lasts.
        if (boss != null)
        {
            Vector3 bossPos = hero.position + Vector3.back * 5.2f;
            boss.transform.SetPositionAndRotation(bossPos, Quaternion.LookRotation(Flat(hero.position - bossPos)));
            bossAnimator = boss.GetComponentInChildren<Animator>();
        }
        Vector3 bossHead = (boss != null ? boss.transform.position : hero.position + Vector3.back * 5.2f) + Vector3.up * 3.8f;

        Time.timeScale = 0.25f;
        aperture = 2.8f;
        StartCoroutine(After(0.2f, () =>
        {
            if (bossAnimator != null) bossAnimator.SetTrigger("Attack");
            Sfx.Play(SfxId.Boom, 0.35f, 0.7f);
            shake = 0.4f;
        }));
        // Low over the hero's right shoulder, looking up at the giant.
        yield return Shot(0.33f, k => Place(hero.position + hero.rotation * Vector3.Lerp(new Vector3(0.75f, 1.15f, -2.3f), new Vector3(0.7f, 1.1f, -2f), k), bossHead, roll: 4f));

        // Hard cut on the swing.
        Time.timeScale = 1f;
        SetAlpha(fade, 1f);
        Sfx.Play(SfxId.Boom, 0.3f, 1f);
        yield return new WaitForSecondsRealtime(0.5f);
    }

    private static void HideTeamRings(GameObject character)
    {
        Transform ring = FindChild(character.transform, "TeamRing");
        if (ring != null) ring.gameObject.SetActive(false);
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
        {
            if (t.name == name) return t;
        }
        return null;
    }

    // --- Camera -------------------------------------------------------------

    private IEnumerator Shot(float duration, Action<float> frame)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            frame(t / duration);
            yield return null;
        }
        frame(1f);
    }

    // Every placement gets a slow handheld drift on top, plus shake and an optional dutch roll,
    // and pulls focus to whatever the camera is looking at.
    private void Place(Vector3 position, Vector3 lookAt, float roll = 0f)
    {
        float time = Time.unscaledTime;
        Vector3 sway = new Vector3(Mathf.PerlinNoise(time * 0.35f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.7f, time * 0.35f) - 0.5f, 0f) * 1.2f;
        Vector3 jitter = new Vector3(Mathf.PerlinNoise(time * 25f, 0f) - 0.5f, Mathf.PerlinNoise(0f, time * 25f) - 0.5f, 0f) * shake * 0.8f;

        Quaternion rotation = Quaternion.LookRotation(lookAt - position) * Quaternion.Euler(sway.y, sway.x, roll);
        cam.transform.SetPositionAndRotation(position + rotation * jitter, rotation);
        Focus(Vector3.Distance(position, lookAt));
    }

    private void Focus(float distance)
    {
        if (depthOfField == null) return;
        depthOfField.focusDistance.Override(distance);
        depthOfField.aperture.Override(aperture);
    }

    private void FaceHeroToward(Vector3 point)
    {
        Vector3 to = Flat(point - hero.position);
        // Too close to tell a direction - turning toward it would make the hero spin on the spot.
        if (to.sqrMagnitude < 0.04f) return;
        hero.rotation = Quaternion.RotateTowards(hero.rotation, Quaternion.LookRotation(to), 260f * Time.deltaTime);
    }

    // Moves the hero at a sprint (Speed above the animator's run threshold, so the Run clip plays).
    // Pulled well back and up behind the runner, a little to their right, so the sprint reads.
    private static readonly Vector3 ChaseOffset = new Vector3(1.2f, 3f, -5.5f);
    private float heroRunSpeed;

    // Sprints toward `destination`, speeding up from a standstill and easing to a stop at it, with
    // the animator's Speed damped so run blends down to idle instead of cutting. Returns the
    // distance still to go.
    private float RunHero(Vector3 destination, float topSpeed)
    {
        Vector3 to = Flat(destination - hero.position);
        float remaining = to.magnitude;
        float targetSpeed = Mathf.Min(topSpeed, remaining * 2.5f);
        heroRunSpeed = Mathf.MoveTowards(heroRunSpeed, targetSpeed, 14f * Time.deltaTime);
        if (remaining > 0.01f) hero.position += to / remaining * Mathf.Min(heroRunSpeed * Time.deltaTime, remaining);
        if (heroAnimator != null) heroAnimator.SetFloat(SpeedParam, heroRunSpeed / 3.4f, 0.12f, Time.deltaTime);
        return remaining;
    }

    private static float Ease(float k) => k * k * (3f - 2f * k);

    // Drops to `scale` speed, holds for `holdSeconds` of real time, then eases back to full speed.
    private IEnumerator SlowMotion(float scale, float holdSeconds)
    {
        Time.timeScale = scale;
        yield return new WaitForSecondsRealtime(holdSeconds);
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            Time.timeScale = Mathf.Lerp(scale, 1f, Ease(t / 0.35f));
            yield return null;
        }
        Time.timeScale = 1f;
    }

    private IEnumerator Flash(float peak, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetAlpha(flash, peak * (1f - t / seconds));
            yield return null;
        }
        SetAlpha(flash, 0f);
    }

    // --- Action -------------------------------------------------------------

    private Enemy Spawn(Enemy prefab, Vector3 position, bool emerge)
    {
        if (prefab == null) return null;
        Enemy enemy = Instantiate(prefab, position, Quaternion.identity);
        enemy.Initialize(core, emerge);
        HideTeamRings(enemy.gameObject);
        return enemy;
    }

    private IEnumerator SpawnHookZombies(Vector3 spot)
    {
        Vector3[] offsets = { new Vector3(0f, 0f, 0f), new Vector3(-1.6f, 0f, 0.8f), new Vector3(1.4f, 0f, 1.2f) };
        // The first one bursts out immediately - the opening frame is the hook.
        float[] waits = { 0.05f, 0.6f, 0.6f };
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 offset = offsets[i];
            yield return new WaitForSeconds(waits[i]);
            Spawn(walkerPrefab, spot + offset, emerge: true);
            Sfx.Play(SfxId.Pop, 0.6f, 0.7f);
        }
    }

    // A steady stream out of the southern treeline, so every shot has something to fight.
    private IEnumerator Horde()
    {
        while (hordeActive)
        {
            if (Enemy.Active.Count < maxEnemies)
            {
                float angle = 180f + UnityEngine.Random.Range(-28f, 28f);
                float radius = UnityEngine.Random.Range(27f, 31f);
                Vector3 position = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                Spawn(PickHordeType(), position, emerge: true);
            }
            yield return new WaitForSeconds(hordeInterval);
        }
    }

    private Enemy PickHordeType()
    {
        float roll = UnityEngine.Random.value;
        if (roll < 0.5f) return walkerPrefab;
        if (roll < 0.7f) return runnerPrefab;
        if (roll < 0.82f) return spitterPrefab;
        if (roll < 0.92f) return bomberPrefab;
        return brutePrefab;
    }

    private IEnumerator BreakWallAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (wallToBreak == null) yield break;
        wallToBreak.Health.TakeDamage(wallToBreak.Health.CurrentHealth);
        shake = 0.7f;
        StartCoroutine(Flash(0.3f, 0.3f));
        StartCoroutine(SlowMotion(0.2f, 0.8f));
    }

    private void SpawnDefenders()
    {
        if (soldierPrefab == null) return;
        for (int i = 0; i < DefenderLine.Length; i++)
        {
            Soldier soldier = Instantiate(soldierPrefab, DefenderLine[i], Quaternion.Euler(0f, 180f, 0f));
            soldier.HoldPosition(DefenderLine[i]);
            // Alternating kneel/stand, like a real firing line.
            soldier.SetKneeling(i % 2 == 0);
        }
        // The soldier who gets ambushed stands guard just inside the breach.
        victim = Instantiate(soldierPrefab, BreachPoint + new Vector3(0f, 0f, 4f), Quaternion.Euler(0f, 180f, 0f));
        victim.HoldPosition(victim.transform.position);
    }

    private static void PoseLimb(Transform root, string name, Vector3 euler)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
        {
            if (t.name == name)
            {
                t.localRotation = Quaternion.Euler(euler);
                return;
            }
        }
    }

    // The hero's dog, galloping at their heel on the run in. Scripted (its own AI off) so it
    // can't wander off to fight other zombies before its big moment.
    private void SpawnHeroDog(Vector3 near)
    {
        if (dogPrefab == null) return;
        ambushDog = Instantiate(dogPrefab, near + hero.rotation * DogHeelOffset, hero.rotation);
        ambushDog.enabled = false;
        HideTeamRings(ambushDog.gameObject);
    }

    private static readonly Vector3 DogHeelOffset = new Vector3(-1f, 0f, -0.8f);

    private void DogAtHeel(Quaternion heading)
    {
        // Once the pounce hands the dog back to its own AI (enabled), stop steering it.
        if (ambushDog == null || ambushDog.enabled) return;
        Transform dog = ambushDog.transform;
        Vector3 spot = hero.position + heading * DogHeelOffset;
        Vector3 to = spot - dog.position;
        to.y = 0f;
        float speed = Mathf.Min(to.magnitude / Mathf.Max(Time.deltaTime, 0.0001f), 7.5f);
        dog.position = Vector3.MoveTowards(dog.position, spot, speed * Time.deltaTime);
        if (to.sqrMagnitude > 0.01f) dog.rotation = Quaternion.RotateTowards(dog.rotation, Quaternion.LookRotation(to), 540f * Time.deltaTime);
        ambushDog.AnimateGait(speed);
    }

    // The zombie keeps biting at the body: an attack lunge and a small spray every half second.
    private IEnumerator Feeding()
    {
        Animator zombieAnimator = ambusher != null ? ambusher.GetComponentInChildren<Animator>() : null;
        while (ambusher != null)
        {
            if (zombieAnimator != null) zombieAnimator.SetTrigger("Attack");
            GameFx.Debris(bodyMiddle + Vector3.up * 0.35f, BloodColor, 5, 2.2f, 0.13f);
            Sfx.Play(SfxId.Hit, 0.5f, 0.6f);
            yield return new WaitForSeconds(0.5f);
        }
    }

    // A proper pounce: gallop in, a split-second crouch to load up, a low fast leap with the front
    // legs reaching (slow motion), then it slams into the zombie, knocks it over backwards, lands on
    // top and shakes its head until the zombie stops moving. Then it hops off back to the hero.
    private IEnumerator DogPounce(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ambushDog == null || ambusher == null) yield break;

        ambushDog.enabled = false;
        Transform dog = ambushDog.transform;
        Vector3 chest = bodyMiddle + Vector3.up * 0.35f;
        Vector3 heading = chest - dog.position;
        heading.y = 0f;
        heading.Normalize();
        Quaternion facing = Quaternion.LookRotation(heading);

        // Gallop to the take-off point a couple of metres short.
        Vector3 takeoff = new Vector3(chest.x, 0f, chest.z) - heading * 2.2f;
        while (Vector3.Distance(Flat(dog.position), Flat(takeoff)) > 0.1f)
        {
            dog.SetPositionAndRotation(Vector3.MoveTowards(dog.position, takeoff, 8.5f * Time.deltaTime), Quaternion.RotateTowards(dog.rotation, facing, 720f * Time.deltaTime));
            ambushDog.AnimateGait(8.5f);
            yield return null;
        }

        // Crouch: haunches drop and the nose dips as it loads up.
        for (float t = 0f; t < 0.12f; t += Time.deltaTime)
        {
            float k = t / 0.12f;
            dog.SetPositionAndRotation(takeoff + Vector3.down * 0.12f * k, facing * Quaternion.Euler(8f * k, 0f, 0f));
            ambushDog.PoseLeap(-0.25f * k);
            yield return null;
        }

        // Leap: low and fast, slowed down for the beat.
        Sfx.Play(SfxId.Pop, 0.5f, 0.8f);
        StartCoroutine(SlowMotion(0.35f, 0.55f));
        Vector3 landOn = chest + heading * 0.2f;
        const float leapSeconds = 0.36f;
        for (float t = 0f; t < leapSeconds; t += Time.deltaTime)
        {
            float k = t / leapSeconds;
            Vector3 position = Vector3.Lerp(takeoff, landOn, k) + Vector3.up * (0.55f * 4f * k * (1f - k));
            // Nose up off the ground, level at the top, tipping down into the hit.
            dog.SetPositionAndRotation(position, facing * Quaternion.Euler(Mathf.Lerp(-18f, 14f, k), 0f, 0f));
            ambushDog.PoseLeap(Mathf.Sin(Mathf.Min(1f, k * 1.4f) * Mathf.PI * 0.5f));
            yield return null;
        }

        // Impact: the zombie goes over backwards, the dog lands on top of it.
        Vector3 impact = ambusher.transform.position + Vector3.up * 0.9f;
        GameFx.Debris(impact, BloodColor, 14, 4f, 0.18f);
        Sfx.Play(SfxId.Boom, 1.2f, 0.6f);
        shake = 0.4f;
        Punch("LOYAL\nTILL THE END");
        StartCoroutine(KnockOver(ambusher.transform, heading));

        Vector3 pinned = ambusher.transform.position + heading * 0.6f + Vector3.up * 0.45f;
        for (float t = 0f; t < 0.2f; t += Time.deltaTime)
        {
            dog.SetPositionAndRotation(Vector3.Lerp(landOn, pinned, t / 0.2f), facing * Quaternion.Euler(Mathf.Lerp(14f, 22f, t / 0.2f), 0f, 0f));
            ambushDog.PoseLeap(Mathf.Lerp(1f, 0.3f, t / 0.2f));
            yield return null;
        }

        // Pinned down, it tears at the zombie - head shaking side to side.
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            ambushDog.ShakeHead(Mathf.Sin(t * 28f) * 28f);
            if (Mathf.Repeat(t, 0.3f) < Time.deltaTime) GameFx.Debris(pinned + heading * 0.3f, BloodColor, 4, 2f, 0.12f);
            yield return null;
        }
        ambushDog.ShakeHead(0f);
        ambusher.Health.TakeDamage(ambusher.Health.CurrentHealth);
        ambusher = null;

        // Hop off and trot back to the hero.
        ambushDog.PoseLeap(0f);
        dog.SetPositionAndRotation(new Vector3(pinned.x, 0f, pinned.z) - heading * 0.4f, facing);
        ambushDog.enabled = true;
        ambushDog.Initialize(hero, 0);
    }

    // The zombie tips over backwards from its crouch onto the ground.
    private IEnumerator KnockOver(Transform zombie, Vector3 pushDirection)
    {
        Vector3 fromPos = zombie.position;
        Quaternion fromRot = zombie.rotation;
        Vector3 toPos = new Vector3(fromPos.x, 0.1f, fromPos.z) + pushDirection * 0.7f;
        Quaternion toRot = Quaternion.LookRotation(-pushDirection) * Quaternion.Euler(-80f, 0f, 0f);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = Ease(t / 0.3f);
            zombie.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
            yield return null;
        }
        zombie.SetPositionAndRotation(toPos, toRot);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // --- Text ---------------------------------------------------------------

    // Action lines slam in from oversized - the standard short-form video caption move.
    private void Punch(string text)
    {
        ResetCaption();
        caption.text = text;
        captionRoutine = StartCoroutine(PunchIn(caption, 0f));
    }

    // Quiet lines fade in and drift up slowly, then fade out.
    private void ShowWhisper(string text, float delay, float hold)
    {
        ResetCaption();
        captionRoutine = StartCoroutine(Whisper(text, delay, hold));
    }

    // Stops whatever the caption was doing (a whisper may be mid-drift) and puts it back in place.
    private void ResetCaption()
    {
        if (captionRoutine != null) StopCoroutine(captionRoutine);
        caption.rectTransform.anchoredPosition = captionBasePosition;
        caption.rectTransform.localScale = Vector3.one;
        SetAlpha(caption, 0f);
    }

    private IEnumerator Whisper(string text, float delay, float hold)
    {
        yield return new WaitForSeconds(delay);
        caption.text = text;
        RectTransform rect = caption.rectTransform;
        Vector2 basePos = captionBasePosition;
        rect.localScale = Vector3.one * 0.8f;

        float total = hold + 0.8f;
        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            float alpha = Mathf.Min(1f, t / 0.5f) * Mathf.Min(1f, (total - t) / 0.4f);
            SetAlpha(caption, alpha);
            rect.anchoredPosition = basePos + Vector2.up * (t * 12f);
            yield return null;
        }
        SetAlpha(caption, 0f);
        rect.anchoredPosition = basePos;
        rect.localScale = Vector3.one;
    }

    private void HideCaption()
    {
        if (captionRoutine != null) StopCoroutine(captionRoutine);
        captionRoutine = StartCoroutine(FadeTo(caption, 0f, 0.3f));
    }

    private IEnumerator PunchIn(Text text, float delay)
    {
        SetAlpha(text, 0f);
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        const float duration = 0.22f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            text.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.45f, 1f, Ease(k));
            SetAlpha(text, k);
            yield return null;
        }
        text.rectTransform.localScale = Vector3.one;
        SetAlpha(text, 1f);
        Sfx.Play(SfxId.Boom, 1.6f, 0.35f);
    }

    private IEnumerator PulseCallToAction(float delay)
    {
        yield return PunchIn(callToAction, delay);
        while (true)
        {
            callToAction.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.04f);
            yield return null;
        }
    }

    private IEnumerator FadeTo(Graphic graphic, float target, float duration, float delay = 0f)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        float start = graphic.color.a;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetAlpha(graphic, Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        SetAlpha(graphic, target);
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }
}
