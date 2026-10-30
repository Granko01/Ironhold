using UnityEditor.Animations;
using UnityEngine;

// Builds the low-poly "primitive puppet" biped rigs (Player Commander, Grunt, Runner,
// Brute, ...) and their animation clips/controller. A biped is a hierarchy of chunky
// cube/sphere blocks with empty "hinge" pivots at the shoulders/hips so a handful of
// keyframed rotations can produce a readable walk/run/attack/death cycle without any
// real skeletal skinning.
public static class CharacterFactory
{
    public struct BipedColors
    {
        public Color uniform;
        public Color skin;
        public Color headwear;
        public Color accent;
        public Color weaponBody;
        public Color weaponDark;
    }

    public struct BipedProportions
    {
        public float hipsY;
        public Vector3 pelvisSize;
        public Vector3 torsoSize;
        public Vector3 headSize;
        public Vector3 headwearSize;
        public bool headwearIsDome;
        public Vector3 legSize;
        public float legSpacingX;
        public Vector3 footSize;
        public Vector3 armSize;
        public float armSpacingX;
        public float armY;

        // Baked-in forward torso lean (degrees) for a permanently hunched stance (e.g. the
        // Runner). Animation curves add on top of this rather than overriding it.
        public float torsoRestLeanX;
    }

    public static readonly BipedProportions PlayerProportions = new BipedProportions
    {
        hipsY = 0.95f,
        pelvisSize = new Vector3(0.5f, 0.22f, 0.32f),
        torsoSize = new Vector3(0.6f, 0.72f, 0.34f),
        headSize = new Vector3(0.38f, 0.36f, 0.38f),
        headwearSize = new Vector3(0.44f, 0.26f, 0.44f),
        headwearIsDome = true,
        legSize = new Vector3(0.22f, 0.85f, 0.22f),
        legSpacingX = 0.18f,
        footSize = new Vector3(0.26f, 0.14f, 0.42f),
        armSize = new Vector3(0.2f, 0.5f, 0.2f),
        armSpacingX = 0.4f,
        armY = 0.26f,
        torsoRestLeanX = 0f,
    };

    public static readonly BipedProportions GruntProportions = new BipedProportions
    {
        hipsY = 0.85f,
        pelvisSize = new Vector3(0.54f, 0.22f, 0.36f),
        torsoSize = new Vector3(0.66f, 0.68f, 0.38f),
        headSize = new Vector3(0.38f, 0.34f, 0.38f),
        headwearSize = new Vector3(0.46f, 0.14f, 0.46f),
        headwearIsDome = false,
        legSize = new Vector3(0.24f, 0.7f, 0.24f),
        legSpacingX = 0.2f,
        footSize = new Vector3(0.28f, 0.14f, 0.44f),
        armSize = new Vector3(0.22f, 0.46f, 0.22f),
        armSpacingX = 0.44f,
        armY = 0.24f,
        torsoRestLeanX = 0f,
    };

    public static readonly BipedProportions RunnerProportions = new BipedProportions
    {
        hipsY = 0.72f,
        pelvisSize = new Vector3(0.34f, 0.16f, 0.24f),
        torsoSize = new Vector3(0.42f, 0.54f, 0.24f),
        headSize = new Vector3(0.3f, 0.28f, 0.3f),
        headwearSize = new Vector3(0.32f, 0.1f, 0.18f),
        headwearIsDome = false,
        legSize = new Vector3(0.17f, 0.65f, 0.17f),
        legSpacingX = 0.15f,
        footSize = new Vector3(0.2f, 0.12f, 0.34f),
        armSize = new Vector3(0.15f, 0.4f, 0.15f),
        armSpacingX = 0.3f,
        armY = 0.2f,
        torsoRestLeanX = 22f,
    };

    public static readonly BipedProportions BruteProportions = new BipedProportions
    {
        hipsY = 1.15f,
        pelvisSize = new Vector3(0.78f, 0.3f, 0.5f),
        torsoSize = new Vector3(1.0f, 1.0f, 0.56f),
        headSize = new Vector3(0.4f, 0.36f, 0.4f),
        headwearSize = new Vector3(0.42f, 0.16f, 0.42f),
        headwearIsDome = false,
        legSize = new Vector3(0.34f, 1.02f, 0.34f),
        legSpacingX = 0.26f,
        footSize = new Vector3(0.36f, 0.16f, 0.5f),
        armSize = new Vector3(0.34f, 0.72f, 0.34f),
        armSpacingX = 0.58f,
        armY = 0.34f,
        torsoRestLeanX = 0f,
    };

    public static readonly BipedProportions SpitterProportions = new BipedProportions
    {
        hipsY = 0.82f,
        pelvisSize = new Vector3(0.44f, 0.2f, 0.32f),
        torsoSize = new Vector3(0.62f, 0.6f, 0.42f),
        headSize = new Vector3(0.3f, 0.28f, 0.3f),
        headwearSize = new Vector3(0.32f, 0.08f, 0.28f),
        headwearIsDome = false,
        legSize = new Vector3(0.17f, 0.74f, 0.17f),
        legSpacingX = 0.16f,
        footSize = new Vector3(0.2f, 0.12f, 0.34f),
        armSize = new Vector3(0.16f, 0.42f, 0.16f),
        armSpacingX = 0.32f,
        armY = 0.2f,
        torsoRestLeanX = 10f,
    };

    public static readonly BipedProportions BomberProportions = new BipedProportions
    {
        hipsY = 0.84f,
        pelvisSize = new Vector3(0.5f, 0.2f, 0.34f),
        torsoSize = new Vector3(0.6f, 0.64f, 0.36f),
        headSize = new Vector3(0.34f, 0.32f, 0.34f),
        headwearSize = new Vector3(0.4f, 0.12f, 0.4f),
        headwearIsDome = false,
        legSize = new Vector3(0.22f, 0.76f, 0.22f),
        legSpacingX = 0.18f,
        footSize = new Vector3(0.26f, 0.14f, 0.4f),
        armSize = new Vector3(0.2f, 0.44f, 0.2f),
        armSpacingX = 0.4f,
        armY = 0.24f,
        torsoRestLeanX = 0f,
    };

    public static readonly BipedProportions EngineerProportions = new BipedProportions
    {
        hipsY = 0.85f,
        pelvisSize = new Vector3(0.5f, 0.2f, 0.32f),
        torsoSize = new Vector3(0.58f, 0.64f, 0.34f),
        headSize = new Vector3(0.34f, 0.32f, 0.34f),
        headwearSize = new Vector3(0.4f, 0.2f, 0.4f),
        headwearIsDome = true,
        legSize = new Vector3(0.2f, 0.77f, 0.2f),
        legSpacingX = 0.18f,
        footSize = new Vector3(0.24f, 0.14f, 0.38f),
        armSize = new Vector3(0.18f, 0.44f, 0.18f),
        armSpacingX = 0.36f,
        armY = 0.22f,
        torsoRestLeanX = 0f,
    };

    // ~2.6x PlayerProportions' height, within the brief's 2-3x range.
    public static readonly BipedProportions BossProportions = new BipedProportions
    {
        hipsY = 2.6f,
        pelvisSize = new Vector3(1.6f, 0.6f, 1.0f),
        torsoSize = new Vector3(2.0f, 2.0f, 1.1f),
        headSize = new Vector3(0.55f, 0.5f, 0.55f),
        headwearSize = new Vector3(0.62f, 0.22f, 0.62f),
        headwearIsDome = false,
        legSize = new Vector3(0.6f, 2.45f, 0.6f),
        legSpacingX = 0.55f,
        footSize = new Vector3(0.75f, 0.32f, 1.05f),
        armSize = new Vector3(0.65f, 1.5f, 0.65f),
        armSpacingX = 1.2f,
        armY = 0.7f,
        torsoRestLeanX = 0f,
    };

    public struct BipedRig
    {
        public Transform hips;
        public Transform torso;
        public Transform armLHinge;
        public Transform armRHinge;
        public Transform legLHinge;
        public Transform legRHinge;
        public Transform weaponSocket;
        public Transform muzzle;
    }

    public static BipedRig BuildBiped(GameObject root, BipedProportions p, BipedColors c, string namePrefix, bool hasWeapon = true)
    {
        Material uniformMat = LowPolyKit.GetOrCreateMaterial($"{namePrefix}Uniform", c.uniform);
        Material skinMat = LowPolyKit.GetOrCreateMaterial($"{namePrefix}Skin", c.skin);
        Material headwearMat = LowPolyKit.GetOrCreateMaterial($"{namePrefix}Headwear", c.headwear);
        Material accentMat = LowPolyKit.GetOrCreateMaterial($"{namePrefix}Accent", c.accent, unlit: true);
        Material weaponBodyMat = LowPolyKit.GetOrCreateMaterial($"{namePrefix}WeaponBody", c.weaponBody);
        Material weaponDarkMat = LowPolyKit.GetOrCreateMaterial($"{namePrefix}WeaponDark", c.weaponDark);

        BipedRig rig = default;

        GameObject hipsGo = new GameObject("Hips");
        hipsGo.transform.SetParent(root.transform, false);
        hipsGo.transform.localPosition = new Vector3(0f, p.hipsY, 0f);
        rig.hips = hipsGo.transform;

        LowPolyKit.CreateBlock("Pelvis", rig.hips, new Vector3(0f, p.pelvisSize.y * 0.5f, 0f), Vector3.zero, p.pelvisSize, uniformMat);

        Transform torso = LowPolyKit.CreateBlock("Torso", rig.hips, new Vector3(0f, p.pelvisSize.y + p.torsoSize.y * 0.5f, 0f), Vector3.zero, p.torsoSize, uniformMat);
        rig.torso = torso;

        LowPolyKit.CreateBlock("Head", torso, new Vector3(0f, p.torsoSize.y * 0.5f + p.headSize.y * 0.5f - 0.02f, 0f), Vector3.zero, p.headSize, skinMat);

        Vector3 headwearLocalPos = new Vector3(0f, p.torsoSize.y * 0.5f + p.headSize.y + p.headwearSize.y * 0.5f - 0.08f, 0f);
        LowPolyKit.CreateBlock("Headwear", torso, headwearLocalPos, Vector3.zero, p.headwearSize, headwearMat, p.headwearIsDome ? PrimitiveType.Sphere : PrimitiveType.Cube);

        LowPolyKit.CreateBlock("ChestAccent", torso, new Vector3(0f, 0.1f, p.torsoSize.z * 0.5f - 0.01f), Vector3.zero, new Vector3(0.14f, 0.14f, 0.04f), accentMat);

        rig.armLHinge = LowPolyKit.CreateHingeLimb("ArmL", torso, new Vector3(-p.armSpacingX, p.armY, 0f), p.armSize, uniformMat, out _);
        rig.armRHinge = LowPolyKit.CreateHingeLimb("ArmR", torso, new Vector3(p.armSpacingX, p.armY, 0f), p.armSize, uniformMat, out _);

        GameObject socketGo = new GameObject("WeaponSocket");
        socketGo.transform.SetParent(rig.armRHinge, false);
        socketGo.transform.localPosition = new Vector3(0f, -p.armSize.y, 0.12f);
        rig.weaponSocket = socketGo.transform;

        if (hasWeapon)
        {
            rig.muzzle = BuildRifle(rig.weaponSocket, weaponBodyMat, weaponDarkMat);
        }
        else
        {
            float fistSize = p.armSize.x * 1.5f;
            LowPolyKit.CreateBlock("Fist", rig.weaponSocket, Vector3.zero, Vector3.zero, new Vector3(fistSize, fistSize, fistSize), weaponDarkMat);

            GameObject handSocketL = new GameObject("HandSocketL");
            handSocketL.transform.SetParent(rig.armLHinge, false);
            handSocketL.transform.localPosition = new Vector3(0f, -p.armSize.y, 0.12f);
            LowPolyKit.CreateBlock("Fist", handSocketL.transform, Vector3.zero, Vector3.zero, new Vector3(fistSize, fistSize, fistSize), weaponDarkMat);

            rig.muzzle = null;
        }

        rig.legLHinge = LowPolyKit.CreateHingeLimb("LegL", rig.hips, new Vector3(-p.legSpacingX, 0f, 0f), p.legSize, uniformMat, out _);
        rig.legRHinge = LowPolyKit.CreateHingeLimb("LegR", rig.hips, new Vector3(p.legSpacingX, 0f, 0f), p.legSize, uniformMat, out _);
        LowPolyKit.CreateBlock("FootL", rig.legLHinge, new Vector3(0f, -p.legSize.y + p.footSize.y * 0.5f, p.footSize.z * 0.15f), Vector3.zero, p.footSize, weaponDarkMat);
        LowPolyKit.CreateBlock("FootR", rig.legRHinge, new Vector3(0f, -p.legSize.y + p.footSize.y * 0.5f, p.footSize.z * 0.15f), Vector3.zero, p.footSize, weaponDarkMat);

        return rig;
    }

    private static Transform BuildRifle(Transform socket, Material bodyMat, Material darkMat)
    {
        LowPolyKit.CreateBlock("Stock", socket, new Vector3(0f, -0.01f, -0.16f), Vector3.zero, new Vector3(0.055f, 0.09f, 0.2f), darkMat);
        LowPolyKit.CreateBlock("Receiver", socket, new Vector3(0f, 0.02f, 0.06f), Vector3.zero, new Vector3(0.07f, 0.08f, 0.28f), bodyMat);
        LowPolyKit.CreateBlock("Grip", socket, new Vector3(0f, -0.09f, -0.02f), new Vector3(20f, 0f, 0f), new Vector3(0.045f, 0.12f, 0.05f), darkMat);
        LowPolyKit.CreateBlock("Barrel", socket, new Vector3(0f, 0.02f, 0.32f), new Vector3(90f, 0f, 0f), new Vector3(0.03f, 0.14f, 0.03f), darkMat, PrimitiveType.Cylinder);

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(socket, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.02f, 0.46f);
        return muzzle.transform;
    }

    // --- Animation -----------------------------------------------------

    // One locomotion speed (Walk, Run, or a Runner's only gait labelled "Run"). stateName
    // becomes both the AnimatorState's name and the generated clip's filename suffix.
    public struct Gait
    {
        public string stateName;
        public float period;
        public float legSwingDeg;
        public float armSwingDeg;
        public float hipsBob;
        public float torsoLeanExtra;
    }

    public static readonly Gait WalkGait = new Gait { stateName = "Walk", period = 0.6f, legSwingDeg = 28f, armSwingDeg = 20f, hipsBob = 0.06f, torsoLeanExtra = 0f };
    public static readonly Gait RunGait = new Gait { stateName = "Run", period = 0.36f, legSwingDeg = 42f, armSwingDeg = 32f, hipsBob = 0.1f, torsoLeanExtra = 12f };

    public static void BuildAnimations(
        GameObject root,
        BipedRig rig,
        BipedProportions p,
        string namePrefix,
        Gait primaryGait,
        Gait? secondaryGait,
        string actionParamName,
        float actionArmSwingDeg,
        float actionTorsoLeanDeg,
        bool actionBothArms,
        out AnimatorController controllerWrapper)
    {
        string hips = LowPolyKit.GetRelativePath(root.transform, rig.hips);
        string torso = LowPolyKit.GetRelativePath(root.transform, rig.torso);
        string armL = LowPolyKit.GetRelativePath(root.transform, rig.armLHinge);
        string armR = LowPolyKit.GetRelativePath(root.transform, rig.armRHinge);
        string legL = LowPolyKit.GetRelativePath(root.transform, rig.legLHinge);
        string legR = LowPolyKit.GetRelativePath(root.transform, rig.legRHinge);

        float hipsY = p.hipsY;

        // Idle explicitly resets every property the locomotion/action clips touch - without
        // this, Mecanim leaves those transforms at whatever a previous state last wrote
        // (e.g. legs stuck mid-stride) since untouched curves aren't reset between states.
        AnimationClip idle = LowPolyAnimKit.GetOrCreateClip($"{namePrefix}_Idle", loop: true);
        Times5(2.2f, out float[] idleTimes);
        LowPolyAnimKit.SetPositionYCurve(idle, hips, idleTimes, new[] { hipsY, hipsY + 0.03f, hipsY, hipsY + 0.03f, hipsY });
        LowPolyAnimKit.SetEulerCurve(idle, armL, idleTimes, SwingX(6f, false));
        LowPolyAnimKit.SetEulerCurve(idle, armR, idleTimes, SwingX(6f, true));
        LowPolyAnimKit.SetEulerCurve(idle, torso, idleTimes, Constant5(new Vector3(p.torsoRestLeanX, 0f, 0f)));
        LowPolyAnimKit.SetEulerCurve(idle, legL, idleTimes, Constant5(Vector3.zero));
        LowPolyAnimKit.SetEulerCurve(idle, legR, idleTimes, Constant5(Vector3.zero));

        AnimationClip primary = BuildLocomotionClip(namePrefix, primaryGait, p, hips, torso, armL, armR, legL, legR);

        AnimationClip secondary = null;
        if (secondaryGait.HasValue)
        {
            secondary = BuildLocomotionClip(namePrefix, secondaryGait.Value, p, hips, torso, armL, armR, legL, legR);
        }

        AnimationClip action = LowPolyAnimKit.GetOrCreateClip($"{namePrefix}_{actionParamName}", loop: false);
        float[] actionTimes = { 0f, 0.08f, 0.22f, 0.4f };
        LowPolyAnimKit.SetEulerCurve(action, armR, actionTimes, ActionSwing(actionArmSwingDeg, 0f));
        if (actionBothArms)
        {
            LowPolyAnimKit.SetEulerCurve(action, armL, actionTimes, ActionSwing(actionArmSwingDeg, 0f));
        }
        LowPolyAnimKit.SetEulerCurve(action, torso, actionTimes, ActionSwing(actionTorsoLeanDeg, p.torsoRestLeanX));

        AnimationClip death = LowPolyAnimKit.GetOrCreateClip($"{namePrefix}_Death", loop: false);
        float[] deathTimes = { 0f, 0.3f, 0.6f };
        LowPolyAnimKit.SetEulerCurve(death, torso, deathTimes, new[] { new Vector3(p.torsoRestLeanX, 0f, 0f), new Vector3(70f, 0f, 12f), new Vector3(92f, 0f, 12f) });
        LowPolyAnimKit.SetPositionYCurve(death, hips, deathTimes, new[] { hipsY, hipsY * 0.5f, hipsY * 0.35f });
        LowPolyAnimKit.SetEulerCurve(death, armL, deathTimes, new[] { Vector3.zero, new Vector3(-30f, 0f, 0f), new Vector3(-40f, 0f, 0f) });
        LowPolyAnimKit.SetEulerCurve(death, armR, deathTimes, new[] { Vector3.zero, new Vector3(-20f, 0f, 0f), new Vector3(-30f, 0f, 0f) });

        controllerWrapper = LowPolyAnimKit.BuildBipedController(
            $"{namePrefix}Animator", idle, primaryGait.stateName, primary, secondary, actionParamName, action, death, runThreshold: 1.2f);
    }

    private static AnimationClip BuildLocomotionClip(string namePrefix, Gait gait, BipedProportions p, string hips, string torso, string armL, string armR, string legL, string legR)
    {
        AnimationClip clip = LowPolyAnimKit.GetOrCreateClip($"{namePrefix}_{gait.stateName}", loop: true);
        Times5(gait.period, out float[] times);
        float hipsY = p.hipsY;

        LowPolyAnimKit.SetPositionYCurve(clip, hips, times, new[] { hipsY, hipsY + gait.hipsBob, hipsY, hipsY + gait.hipsBob, hipsY });
        LowPolyAnimKit.SetEulerCurve(clip, legL, times, SwingX(gait.legSwingDeg, false));
        LowPolyAnimKit.SetEulerCurve(clip, legR, times, SwingX(gait.legSwingDeg, true));
        LowPolyAnimKit.SetEulerCurve(clip, armL, times, SwingX(gait.armSwingDeg, true));
        LowPolyAnimKit.SetEulerCurve(clip, armR, times, SwingX(gait.armSwingDeg, false));
        LowPolyAnimKit.SetEulerCurve(clip, torso, times, Constant5(new Vector3(p.torsoRestLeanX + gait.torsoLeanExtra, 0f, 0f)));

        return clip;
    }

    // The Boss has 4 distinct action states (Attack, GroundSlam, Charge, Roar) instead of
    // the single action every other biped gets, so it gets its own animation builder rather
    // than stretching BuildAnimations' one-action shape to fit.
    public static void BuildBossAnimations(GameObject root, BipedRig rig, BipedProportions p, out AnimatorController controllerWrapper)
    {
        string hips = LowPolyKit.GetRelativePath(root.transform, rig.hips);
        string torso = LowPolyKit.GetRelativePath(root.transform, rig.torso);
        string armL = LowPolyKit.GetRelativePath(root.transform, rig.armLHinge);
        string armR = LowPolyKit.GetRelativePath(root.transform, rig.armRHinge);
        string legL = LowPolyKit.GetRelativePath(root.transform, rig.legLHinge);
        string legR = LowPolyKit.GetRelativePath(root.transform, rig.legRHinge);

        float hipsY = p.hipsY;

        AnimationClip idle = LowPolyAnimKit.GetOrCreateClip("Boss_Idle", loop: true);
        Times5(2.6f, out float[] idleTimes);
        LowPolyAnimKit.SetPositionYCurve(idle, hips, idleTimes, new[] { hipsY, hipsY + 0.05f, hipsY, hipsY + 0.05f, hipsY });
        LowPolyAnimKit.SetEulerCurve(idle, armL, idleTimes, SwingX(4f, false));
        LowPolyAnimKit.SetEulerCurve(idle, armR, idleTimes, SwingX(4f, true));
        LowPolyAnimKit.SetEulerCurve(idle, torso, idleTimes, Constant5(Vector3.zero));
        LowPolyAnimKit.SetEulerCurve(idle, legL, idleTimes, Constant5(Vector3.zero));
        LowPolyAnimKit.SetEulerCurve(idle, legR, idleTimes, Constant5(Vector3.zero));

        var walkGait = new Gait { stateName = "Walk", period = 1.1f, legSwingDeg = 16f, armSwingDeg = 10f, hipsBob = 0.25f, torsoLeanExtra = 0f };
        AnimationClip walk = BuildLocomotionClip("Boss", walkGait, p, hips, torso, armL, armR, legL, legR);

        AnimationClip attack = LowPolyAnimKit.GetOrCreateClip("Boss_Attack", loop: false);
        float[] attackTimes = { 0f, 0.1f, 0.3f, 0.55f };
        LowPolyAnimKit.SetEulerCurve(attack, armR, attackTimes, new[] { Vector3.zero, new Vector3(-55f, 0f, 0f), new Vector3(90f, 0f, 0f), Vector3.zero });
        LowPolyAnimKit.SetEulerCurve(attack, torso, attackTimes, new[] { Vector3.zero, new Vector3(-10f, 0f, 0f), new Vector3(25f, 0f, 0f), Vector3.zero });

        AnimationClip groundSlam = LowPolyAnimKit.GetOrCreateClip("Boss_GroundSlam", loop: false);
        float[] slamTimes = { 0f, 0.18f, 0.4f, 0.7f };
        LowPolyAnimKit.SetEulerCurve(groundSlam, armL, slamTimes, new[] { Vector3.zero, new Vector3(-60f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(20f, 0f, 0f) });
        LowPolyAnimKit.SetEulerCurve(groundSlam, armR, slamTimes, new[] { Vector3.zero, new Vector3(-60f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(20f, 0f, 0f) });
        LowPolyAnimKit.SetEulerCurve(groundSlam, torso, slamTimes, new[] { Vector3.zero, new Vector3(-12f, 0f, 0f), new Vector3(35f, 0f, 0f), new Vector3(15f, 0f, 0f) });
        LowPolyAnimKit.SetPositionYCurve(groundSlam, hips, slamTimes, new[] { hipsY, hipsY - 0.2f, hipsY - 0.65f, hipsY - 0.2f });

        AnimationClip charge = LowPolyAnimKit.GetOrCreateClip("Boss_Charge", loop: false);
        float[] chargeTimes = { 0f, 0.12f, 0.28f, 0.45f };
        LowPolyAnimKit.SetEulerCurve(charge, torso, chargeTimes, new[] { Vector3.zero, new Vector3(30f, 0f, 0f), new Vector3(32f, 0f, 0f), new Vector3(10f, 0f, 0f) });
        LowPolyAnimKit.SetEulerCurve(charge, legL, chargeTimes, new[] { Vector3.zero, new Vector3(-38f, 0f, 0f), new Vector3(38f, 0f, 0f), new Vector3(-38f, 0f, 0f) });
        LowPolyAnimKit.SetEulerCurve(charge, legR, chargeTimes, new[] { Vector3.zero, new Vector3(38f, 0f, 0f), new Vector3(-38f, 0f, 0f), new Vector3(38f, 0f, 0f) });
        LowPolyAnimKit.SetEulerCurve(charge, armL, chargeTimes, new[] { Vector3.zero, new Vector3(-18f, 0f, 10f), new Vector3(-22f, 0f, 10f), new Vector3(-18f, 0f, 10f) });
        LowPolyAnimKit.SetEulerCurve(charge, armR, chargeTimes, new[] { Vector3.zero, new Vector3(-18f, 0f, -10f), new Vector3(-22f, 0f, -10f), new Vector3(-18f, 0f, -10f) });
        LowPolyAnimKit.SetPositionYCurve(charge, hips, chargeTimes, new[] { hipsY, hipsY - 0.1f, hipsY + 0.05f, hipsY - 0.1f });

        AnimationClip roar = LowPolyAnimKit.GetOrCreateClip("Boss_Roar", loop: false);
        float[] roarTimes = { 0f, 0.2f, 0.55f, 0.8f };
        LowPolyAnimKit.SetEulerCurve(roar, torso, roarTimes, new[] { Vector3.zero, new Vector3(-22f, 0f, 0f), new Vector3(-25f, 0f, 0f), new Vector3(-5f, 0f, 0f) });
        LowPolyAnimKit.SetEulerCurve(roar, armL, roarTimes, new[] { Vector3.zero, new Vector3(-35f, 0f, 45f), new Vector3(-35f, 0f, 45f), Vector3.zero });
        LowPolyAnimKit.SetEulerCurve(roar, armR, roarTimes, new[] { Vector3.zero, new Vector3(-35f, 0f, -45f), new Vector3(-35f, 0f, -45f), Vector3.zero });

        AnimationClip death = LowPolyAnimKit.GetOrCreateClip("Boss_Death", loop: false);
        float[] deathTimes = { 0f, 0.35f, 0.75f };
        LowPolyAnimKit.SetEulerCurve(death, torso, deathTimes, new[] { Vector3.zero, new Vector3(65f, 0f, 10f), new Vector3(88f, 0f, 10f) });
        LowPolyAnimKit.SetPositionYCurve(death, hips, deathTimes, new[] { hipsY, hipsY * 0.55f, hipsY * 0.4f });
        LowPolyAnimKit.SetEulerCurve(death, armL, deathTimes, new[] { Vector3.zero, new Vector3(-25f, 0f, 0f), new Vector3(-35f, 0f, 0f) });
        LowPolyAnimKit.SetEulerCurve(death, armR, deathTimes, new[] { Vector3.zero, new Vector3(-15f, 0f, 0f), new Vector3(-25f, 0f, 0f) });

        controllerWrapper = LowPolyAnimKit.BuildBossController(
            "BossAnimator", idle, walk,
            new[]
            {
                ("Attack", attack),
                ("GroundSlam", groundSlam),
                ("Charge", charge),
                ("Roar", roar),
            },
            death);
    }

    private static void Times5(float period, out float[] times)
    {
        times = new[] { 0f, period * 0.25f, period * 0.5f, period * 0.75f, period };
    }

    private static Vector3[] SwingX(float amplitudeDeg, bool inverted)
    {
        float sign = inverted ? -1f : 1f;
        return new[]
        {
            new Vector3(sign * -amplitudeDeg, 0f, 0f),
            Vector3.zero,
            new Vector3(sign * amplitudeDeg, 0f, 0f),
            Vector3.zero,
            new Vector3(sign * -amplitudeDeg, 0f, 0f),
        };
    }

    private static Vector3[] Constant5(Vector3 value)
    {
        return new[] { value, value, value, value, value };
    }

    private static Vector3[] ActionSwing(float amplitudeDeg, float baseDeg)
    {
        return new[]
        {
            new Vector3(baseDeg, 0f, 0f),
            new Vector3(baseDeg - amplitudeDeg, 0f, 0f),
            new Vector3(baseDeg + amplitudeDeg * 0.3f, 0f, 0f),
            new Vector3(baseDeg, 0f, 0f),
        };
    }
}
