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

    // All sizes are in metres, as they appear in the world (body parts are never parented under a
    // scaled block, so nothing gets squashed or sheared).
    public struct BipedProportions
    {
        public float hipsY;
        public Vector3 pelvisSize;
        public Vector3 torsoSize;
        public Vector3 headSize;
        public Vector3 headwearSize;
        public bool headwearIsDome;
        // Whole leg, hip to sole (thigh + shin + foot).
        public Vector3 legSize;
        public float legSpacingX;
        public Vector3 footSize;
        // Whole arm, shoulder to fingertips (upper arm + forearm + hand).
        public Vector3 armSize;
        public float armSpacingX;
        public float armY;

        // Baked-in forward torso lean (degrees) for a permanently hunched stance (e.g. the
        // Runner). Animation curves add on top of this rather than overriding it.
        public float torsoRestLeanX;

        // Baked-in shoulder rotations (e.g. the Commander holding a weapon up at chest height).
        // Like torsoRestLeanX, the arm swing curves oscillate around these instead of hanging straight.
        public Vector3 armRestEulerL;
        public Vector3 armRestEulerR;

        // Fixed elbow bend (degrees about X, negative brings the forearm forward/up).
        public float elbowRestL;
        public float elbowRestR;

        // Multiplier on hand size; 0 means the default.
        public float handScale;
    }

    private const float NeckHeight = 0.05f;

    // Head centre / top, relative to the torso's centre - shared with the gear and face builders.
    public static float HeadCenterY(BipedProportions p) => p.torsoSize.y * 0.5f + NeckHeight + p.headSize.y * 0.5f;
    public static float HeadTopY(BipedProportions p) => p.torsoSize.y * 0.5f + NeckHeight + p.headSize.y;

    // About 1.95m. Heads run a little large (a stylised touch that keeps faces readable on a phone).
    public static readonly BipedProportions PlayerProportions = new BipedProportions
    {
        hipsY = 0.88f,
        pelvisSize = new Vector3(0.38f, 0.18f, 0.24f),
        torsoSize = new Vector3(0.48f, 0.58f, 0.27f),
        headSize = new Vector3(0.32f, 0.34f, 0.33f),
        headwearSize = new Vector3(0.38f, 0.22f, 0.4f),
        headwearIsDome = true,
        legSize = new Vector3(0.16f, 0.88f, 0.17f),
        legSpacingX = 0.11f,
        footSize = new Vector3(0.15f, 0.1f, 0.28f),
        armSize = new Vector3(0.12f, 0.66f, 0.12f),
        armSpacingX = 0.3f,
        armY = 0.22f,
        torsoRestLeanX = 0f,
        // Rifle held at the shoulder: right hand on the grip with the elbow tucked, left arm
        // reaching across to steady the barrel.
        armRestEulerL = new Vector3(-48f, 34f, 0f),
        armRestEulerR = new Vector3(-28f, -18f, 0f),
        elbowRestL = -42f,
        elbowRestR = -68f,
        handScale = 1.1f,
    };

    public static readonly BipedProportions GruntProportions = new BipedProportions
    {
        hipsY = 0.84f,
        pelvisSize = new Vector3(0.38f, 0.18f, 0.25f),
        torsoSize = new Vector3(0.48f, 0.56f, 0.28f),
        headSize = new Vector3(0.3f, 0.33f, 0.32f),
        headwearSize = new Vector3(0.32f, 0.08f, 0.33f),
        headwearIsDome = false,
        legSize = new Vector3(0.16f, 0.84f, 0.17f),
        legSpacingX = 0.11f,
        footSize = new Vector3(0.14f, 0.09f, 0.26f),
        armSize = new Vector3(0.11f, 0.68f, 0.11f),
        armSpacingX = 0.29f,
        armY = 0.21f,
        torsoRestLeanX = 0f,
        elbowRestL = -12f,
        elbowRestR = -12f,
    };

    public static readonly BipedProportions RunnerProportions = new BipedProportions
    {
        hipsY = 0.82f,
        pelvisSize = new Vector3(0.32f, 0.16f, 0.21f),
        torsoSize = new Vector3(0.38f, 0.5f, 0.22f),
        headSize = new Vector3(0.27f, 0.3f, 0.29f),
        headwearSize = new Vector3(0.29f, 0.07f, 0.3f),
        headwearIsDome = false,
        legSize = new Vector3(0.13f, 0.82f, 0.14f),
        legSpacingX = 0.09f,
        footSize = new Vector3(0.12f, 0.08f, 0.24f),
        armSize = new Vector3(0.09f, 0.64f, 0.09f),
        armSpacingX = 0.24f,
        armY = 0.19f,
        torsoRestLeanX = 22f,
        elbowRestL = -20f,
        elbowRestR = -20f,
    };

    public static readonly BipedProportions BruteProportions = new BipedProportions
    {
        hipsY = 1.05f,
        pelvisSize = new Vector3(0.62f, 0.26f, 0.38f),
        torsoSize = new Vector3(0.86f, 0.82f, 0.46f),
        headSize = new Vector3(0.36f, 0.36f, 0.38f),
        headwearSize = new Vector3(0.38f, 0.1f, 0.4f),
        headwearIsDome = false,
        legSize = new Vector3(0.26f, 1.05f, 0.27f),
        legSpacingX = 0.17f,
        footSize = new Vector3(0.22f, 0.12f, 0.38f),
        armSize = new Vector3(0.22f, 0.95f, 0.22f),
        armSpacingX = 0.55f,
        armY = 0.3f,
        torsoRestLeanX = 0f,
        elbowRestL = -15f,
        elbowRestR = -15f,
    };

    public static readonly BipedProportions SpitterProportions = new BipedProportions
    {
        hipsY = 0.82f,
        pelvisSize = new Vector3(0.36f, 0.18f, 0.26f),
        torsoSize = new Vector3(0.5f, 0.52f, 0.34f),
        headSize = new Vector3(0.29f, 0.31f, 0.31f),
        headwearSize = new Vector3(0.3f, 0.06f, 0.3f),
        headwearIsDome = false,
        legSize = new Vector3(0.14f, 0.82f, 0.15f),
        legSpacingX = 0.1f,
        footSize = new Vector3(0.13f, 0.08f, 0.25f),
        armSize = new Vector3(0.1f, 0.62f, 0.1f),
        armSpacingX = 0.28f,
        armY = 0.2f,
        torsoRestLeanX = 10f,
        elbowRestL = -15f,
        elbowRestR = -15f,
    };

    public static readonly BipedProportions BomberProportions = new BipedProportions
    {
        hipsY = 0.8f,
        pelvisSize = new Vector3(0.44f, 0.2f, 0.32f),
        torsoSize = new Vector3(0.58f, 0.58f, 0.4f),
        headSize = new Vector3(0.31f, 0.32f, 0.32f),
        headwearSize = new Vector3(0.33f, 0.07f, 0.33f),
        headwearIsDome = false,
        legSize = new Vector3(0.17f, 0.8f, 0.18f),
        legSpacingX = 0.13f,
        footSize = new Vector3(0.15f, 0.09f, 0.27f),
        armSize = new Vector3(0.12f, 0.62f, 0.12f),
        armSpacingX = 0.33f,
        armY = 0.21f,
        torsoRestLeanX = 0f,
        elbowRestL = -12f,
        elbowRestR = -12f,
    };

    public static readonly BipedProportions EngineerProportions = new BipedProportions
    {
        hipsY = 0.84f,
        pelvisSize = new Vector3(0.38f, 0.18f, 0.25f),
        torsoSize = new Vector3(0.48f, 0.56f, 0.28f),
        headSize = new Vector3(0.3f, 0.33f, 0.32f),
        headwearSize = new Vector3(0.36f, 0.17f, 0.38f),
        headwearIsDome = true,
        legSize = new Vector3(0.16f, 0.84f, 0.17f),
        legSpacingX = 0.11f,
        footSize = new Vector3(0.15f, 0.1f, 0.28f),
        armSize = new Vector3(0.11f, 0.66f, 0.11f),
        armSpacingX = 0.29f,
        armY = 0.21f,
        torsoRestLeanX = 0f,
        elbowRestL = -12f,
        elbowRestR = -12f,
    };

    // About 5.2m, roughly 2.7x the hero.
    public static readonly BipedProportions BossProportions = new BipedProportions
    {
        hipsY = 2.3f,
        pelvisSize = new Vector3(1.2f, 0.5f, 0.75f),
        torsoSize = new Vector3(1.7f, 1.6f, 0.95f),
        headSize = new Vector3(0.7f, 0.7f, 0.72f),
        headwearSize = new Vector3(0.74f, 0.18f, 0.76f),
        headwearIsDome = false,
        legSize = new Vector3(0.5f, 2.3f, 0.52f),
        legSpacingX = 0.38f,
        footSize = new Vector3(0.45f, 0.25f, 0.8f),
        armSize = new Vector3(0.45f, 2f, 0.45f),
        armSpacingX = 1.05f,
        armY = 0.62f,
        torsoRestLeanX = 0f,
        elbowRestL = -15f,
        elbowRestR = -15f,
    };

    // A copy of `p` posed like the undead: arms reaching forward (each its own angle, for an
    // uneven, lurching look) and the torso hunched further over.
    public static BipedProportions Zombified(BipedProportions p, Vector3 armL, Vector3 armR, float extraLean)
    {
        p.armRestEulerL = armL;
        p.armRestEulerR = armR;
        p.torsoRestLeanX += extraLean;
        return p;
    }

    public struct BipedRig
    {
        public Transform hips;
        public Transform pelvis;
        public Transform torso;
        public Transform armLHinge;
        public Transform armRHinge;
        public Transform elbowL;
        public Transform elbowR;
        public Transform forearmL;
        public Transform forearmR;
        public Transform handL;
        public Transform handR;
        public Transform legLHinge;
        public Transform legRHinge;
        public Transform kneeL;
        public Transform kneeR;
        public Transform thighL;
        public Transform thighR;
        public Transform shinL;
        public Transform shinR;
        public Transform weaponSocket;
        public Transform muzzle;
        public Transform rifle;
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

        rig.hips = Pivot("Hips", root.transform, new Vector3(0f, p.hipsY, 0f));
        rig.pelvis = LowPolyKit.CreateBlock("Pelvis", rig.hips, new Vector3(0f, p.pelvisSize.y * 0.5f, 0f), Vector3.zero, p.pelvisSize, uniformMat);

        // The torso is an unscaled pivot (animated for lean/twist); its look is a broad chest over a
        // narrower waist, so the silhouette tapers like a real body.
        Vector3 t = p.torsoSize;
        rig.torso = Pivot("Torso", rig.hips, new Vector3(0f, p.pelvisSize.y + t.y * 0.5f, 0f));
        LowPolyKit.CreateBlock("Chest", rig.torso, new Vector3(0f, t.y * 0.21f, 0f), Vector3.zero, new Vector3(t.x, t.y * 0.58f, t.z), uniformMat);
        LowPolyKit.CreateBlock("Waist", rig.torso, new Vector3(0f, -t.y * 0.26f, 0f), Vector3.zero, new Vector3(t.x * 0.82f, t.y * 0.48f, t.z * 0.92f), uniformMat);

        Vector3 h = p.headSize;
        LowPolyKit.CreateBlock("Neck", rig.torso, new Vector3(0f, t.y * 0.5f + NeckHeight * 0.5f, 0f), Vector3.zero, new Vector3(h.x * 0.42f, NeckHeight + 0.04f, h.z * 0.42f), skinMat);
        float headY = HeadCenterY(p);
        LowPolyKit.CreateBlock("Head", rig.torso, new Vector3(0f, headY, 0f), Vector3.zero, h, skinMat);
        LowPolyKit.CreateBlock("Nose", rig.torso, new Vector3(0f, headY - h.y * 0.08f, h.z * 0.5f + 0.015f), Vector3.zero, new Vector3(h.x * 0.14f, h.y * 0.18f, 0.05f), skinMat);
        LowPolyKit.CreateBlock("EarL", rig.torso, new Vector3(-h.x * 0.5f - 0.01f, headY, 0f), Vector3.zero, new Vector3(0.04f, h.y * 0.26f, h.z * 0.2f), skinMat);
        LowPolyKit.CreateBlock("EarR", rig.torso, new Vector3(h.x * 0.5f + 0.01f, headY, 0f), Vector3.zero, new Vector3(0.04f, h.y * 0.26f, h.z * 0.2f), skinMat);
        LowPolyKit.CreateBlock("Brow", rig.torso, new Vector3(0f, headY + h.y * 0.17f, h.z * 0.5f + 0.01f), Vector3.zero, new Vector3(h.x * 0.72f, h.y * 0.06f, 0.03f), headwearMat);

        float headTop = HeadTopY(p);
        Vector3 headwearPos = p.headwearIsDome
            ? new Vector3(0f, headTop - p.headwearSize.y * 0.2f, 0f)
            : new Vector3(0f, headTop + p.headwearSize.y * 0.5f - 0.02f, -h.z * 0.04f);
        LowPolyKit.CreateBlock("Headwear", rig.torso, headwearPos, Vector3.zero, p.headwearSize, headwearMat, p.headwearIsDome ? PrimitiveType.Sphere : PrimitiveType.Cube);

        LowPolyKit.CreateBlock("ChestAccent", rig.torso, new Vector3(0f, t.y * 0.22f, t.z * 0.5f + 0.01f), Vector3.zero, new Vector3(0.1f, 0.1f, 0.03f), accentMat);

        float handScale = p.handScale > 0f ? p.handScale : 1f;
        rig.armLHinge = BuildArm("ArmL", rig.torso, -1f, p, p.armRestEulerL, p.elbowRestL, handScale, uniformMat, skinMat, out rig.elbowL, out rig.forearmL, out rig.handL);
        rig.armRHinge = BuildArm("ArmR", rig.torso, 1f, p, p.armRestEulerR, p.elbowRestR, handScale, uniformMat, skinMat, out rig.elbowR, out rig.forearmR, out rig.handR);

        // The weapon rides in the right hand. Its rotation cancels the shoulder and elbow rest
        // pose, so a held gun points straight ahead in the rest pose.
        float foreLen = p.armSize.y * ForearmShare;
        float handLen = p.armSize.y * HandShare * handScale;
        rig.weaponSocket = Pivot("WeaponSocket", rig.elbowR, new Vector3(0f, -foreLen - handLen * 0.5f, 0.03f));
        rig.weaponSocket.localRotation = Quaternion.Inverse(Quaternion.Euler(p.armRestEulerR) * Quaternion.Euler(p.elbowRestR, 0f, 0f));

        if (hasWeapon)
        {
            GameObject rifle = new GameObject("Rifle");
            rifle.transform.SetParent(rig.weaponSocket, false);
            rig.rifle = rifle.transform;
            rig.muzzle = BuildRifle(rig.rifle, weaponBodyMat, weaponDarkMat);
        }

        rig.legLHinge = BuildLeg("LegL", rig.hips, -1f, p, uniformMat, weaponDarkMat, out rig.kneeL, out rig.thighL, out rig.shinL);
        rig.legRHinge = BuildLeg("LegR", rig.hips, 1f, p, uniformMat, weaponDarkMat, out rig.kneeR, out rig.thighR, out rig.shinR);

        return rig;
    }

    private const float UpperArmShare = 0.47f;
    private const float ForearmShare = 0.4f;
    private const float HandShare = 0.13f;
    private const float ThighShare = 0.48f;

    private static Transform Pivot(string name, Transform parent, Vector3 localPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    // Shoulder hinge (animated) -> rounded shoulder + upper arm -> elbow pivot (fixed bend) ->
    // forearm -> hand with a thumb on the inner side.
    private static Transform BuildArm(string name, Transform torso, float side, BipedProportions p, Vector3 restEuler, float elbowBend, float handScale,
        Material sleeveMat, Material skinMat, out Transform elbow, out Transform forearm, out Transform hand)
    {
        Vector3 a = p.armSize;
        float upperLen = a.y * UpperArmShare;
        float foreLen = a.y * ForearmShare;
        float handLen = a.y * HandShare * handScale;

        Transform hinge = Pivot(name, torso, new Vector3(side * p.armSpacingX, p.armY, 0f));
        hinge.localRotation = Quaternion.Euler(restEuler);
        LowPolyKit.CreateBlock(name + "_Shoulder", hinge, new Vector3(0f, -a.x * 0.15f, 0f), Vector3.zero, Vector3.one * a.x * 1.45f, sleeveMat, PrimitiveType.Sphere);
        LowPolyKit.CreateBlock(name + "_Upper", hinge, new Vector3(0f, -upperLen * 0.5f, 0f), Vector3.zero, new Vector3(a.x, upperLen, a.z), sleeveMat);

        elbow = Pivot(name + "_Elbow", hinge, new Vector3(0f, -upperLen, 0f));
        elbow.localRotation = Quaternion.Euler(elbowBend, 0f, 0f);
        LowPolyKit.CreateBlock(name + "_ElbowJoint", elbow, Vector3.zero, Vector3.zero, Vector3.one * a.x * 0.98f, sleeveMat, PrimitiveType.Sphere);
        forearm = LowPolyKit.CreateBlock(name + "_Forearm", elbow, new Vector3(0f, -foreLen * 0.5f, 0f), Vector3.zero, new Vector3(a.x * 0.88f, foreLen, a.z * 0.88f), sleeveMat);

        Vector3 palm = new Vector3(a.x * handScale, handLen, a.z * 1.25f * handScale);
        hand = LowPolyKit.CreateBlock(name + "_Hand", elbow, new Vector3(0f, -foreLen - handLen * 0.5f, 0.01f), Vector3.zero, palm, skinMat);
        LowPolyKit.CreateBlock(name + "_Thumb", elbow, new Vector3(-side * palm.x * 0.55f, -foreLen - handLen * 0.25f, palm.z * 0.3f), new Vector3(0f, 0f, side * 25f), new Vector3(palm.x * 0.38f, handLen * 0.6f, palm.z * 0.38f), skinMat);
        return hinge;
    }

    // Hip hinge (animated) -> thigh -> knee pivot (animated) -> shin -> boot with a toe cap.
    private static Transform BuildLeg(string name, Transform hips, float side, BipedProportions p, Material trouserMat, Material bootMat,
        out Transform knee, out Transform thigh, out Transform shin)
    {
        Vector3 l = p.legSize;
        Vector3 f = p.footSize;
        float thighLen = l.y * ThighShare;
        float shinLen = l.y - thighLen - f.y;

        Transform hinge = Pivot(name, hips, new Vector3(side * p.legSpacingX, 0f, 0f));
        thigh = LowPolyKit.CreateBlock(name + "_Thigh", hinge, new Vector3(0f, -thighLen * 0.5f, 0f), Vector3.zero, new Vector3(l.x, thighLen, l.z), trouserMat);

        knee = Pivot(name + "_Knee", hinge, new Vector3(0f, -thighLen, 0f));
        LowPolyKit.CreateBlock(name + "_KneeJoint", knee, Vector3.zero, Vector3.zero, Vector3.one * l.x * 0.95f, trouserMat, PrimitiveType.Sphere);
        shin = LowPolyKit.CreateBlock(name + "_Shin", knee, new Vector3(0f, -shinLen * 0.5f, 0f), Vector3.zero, new Vector3(l.x * 0.86f, shinLen, l.z * 0.86f), trouserMat);
        LowPolyKit.CreateBlock(name + "_Boot", knee, new Vector3(0f, -shinLen - f.y * 0.5f, f.z * 0.18f), Vector3.zero, f, bootMat);
        LowPolyKit.CreateBlock(name + "_Toe", knee, new Vector3(0f, -shinLen - f.y * 0.62f, f.z * 0.58f), Vector3.zero, new Vector3(f.x * 0.94f, f.y * 0.72f, f.z * 0.22f), bootMat);
        return hinge;
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

    // A recurve bow held upright with an arrow nocked and pointing forward. Returns the arrow tip.
    public static Transform BuildBow(Transform socket, Material woodMat, Material stringMat, Material arrowMat)
    {
        LowPolyKit.CreateBlock("Grip", socket, new Vector3(0f, 0.02f, 0.14f), Vector3.zero, new Vector3(0.06f, 0.16f, 0.06f), woodMat);
        LowPolyKit.CreateBlock("LimbUpper", socket, new Vector3(0f, 0.27f, 0.1f), new Vector3(-16f, 0f, 0f), new Vector3(0.05f, 0.36f, 0.05f), woodMat);
        LowPolyKit.CreateBlock("LimbLower", socket, new Vector3(0f, -0.23f, 0.1f), new Vector3(16f, 0f, 0f), new Vector3(0.05f, 0.36f, 0.05f), woodMat);
        LowPolyKit.CreateBlock("TipUpper", socket, new Vector3(0f, 0.46f, 0.1f), new Vector3(20f, 0f, 0f), new Vector3(0.045f, 0.08f, 0.045f), woodMat);
        LowPolyKit.CreateBlock("TipLower", socket, new Vector3(0f, -0.42f, 0.1f), new Vector3(-20f, 0f, 0f), new Vector3(0.045f, 0.08f, 0.045f), woodMat);
        LowPolyKit.CreateBlock("String", socket, new Vector3(0f, 0.02f, 0.02f), Vector3.zero, new Vector3(0.012f, 0.86f, 0.012f), stringMat);

        LowPolyKit.CreateBlock("ArrowShaft", socket, new Vector3(0f, 0.02f, 0.22f), new Vector3(90f, 0f, 0f), new Vector3(0.025f, 0.24f, 0.025f), woodMat, PrimitiveType.Cylinder);
        LowPolyKit.CreateBlock("ArrowHead", socket, new Vector3(0f, 0.02f, 0.47f), new Vector3(0f, 45f, 0f), new Vector3(0.055f, 0.03f, 0.055f), arrowMat);
        LowPolyKit.CreateBlock("Fletching", socket, new Vector3(0f, 0.02f, 0.02f), Vector3.zero, new Vector3(0.012f, 0.07f, 0.09f), arrowMat);

        GameObject tip = new GameObject("ArrowTip");
        tip.transform.SetParent(socket, false);
        tip.transform.localPosition = new Vector3(0f, 0.02f, 0.5f);
        return tip.transform;
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
        string kneeL = LowPolyKit.GetRelativePath(root.transform, rig.kneeL);
        string kneeR = LowPolyKit.GetRelativePath(root.transform, rig.kneeR);

        float hipsY = p.hipsY;

        // Idle explicitly resets every property the locomotion/action clips touch - without
        // this, Mecanim leaves those transforms at whatever a previous state last wrote
        // (e.g. legs stuck mid-stride) since untouched curves aren't reset between states.
        AnimationClip idle = LowPolyAnimKit.GetOrCreateClip($"{namePrefix}_Idle", loop: true);
        Times5(2.2f, out float[] idleTimes);
        LowPolyAnimKit.SetPositionYCurve(idle, hips, idleTimes, new[] { hipsY, hipsY + 0.03f, hipsY, hipsY + 0.03f, hipsY });
        LowPolyAnimKit.SetEulerCurve(idle, armL, idleTimes, Around(SwingX(6f, false), p.armRestEulerL));
        LowPolyAnimKit.SetEulerCurve(idle, armR, idleTimes, Around(SwingX(6f, true), p.armRestEulerR));
        LowPolyAnimKit.SetEulerCurve(idle, torso, idleTimes, Constant5(new Vector3(p.torsoRestLeanX, 0f, 0f)));
        LowPolyAnimKit.SetEulerCurve(idle, legL, idleTimes, Constant5(Vector3.zero));
        LowPolyAnimKit.SetEulerCurve(idle, legR, idleTimes, Constant5(Vector3.zero));
        LowPolyAnimKit.SetEulerCurve(idle, kneeL, idleTimes, Constant5(new Vector3(IdleKneeBend, 0f, 0f)));
        LowPolyAnimKit.SetEulerCurve(idle, kneeR, idleTimes, Constant5(new Vector3(IdleKneeBend, 0f, 0f)));

        AnimationClip primary = BuildLocomotionClip(namePrefix, primaryGait, p, hips, torso, armL, armR, legL, legR, kneeL, kneeR);

        AnimationClip secondary = null;
        if (secondaryGait.HasValue)
        {
            secondary = BuildLocomotionClip(namePrefix, secondaryGait.Value, p, hips, torso, armL, armR, legL, legR, kneeL, kneeR);
        }

        AnimationClip action = LowPolyAnimKit.GetOrCreateClip($"{namePrefix}_{actionParamName}", loop: false);
        float[] actionTimes = { 0f, 0.08f, 0.22f, 0.4f };
        LowPolyAnimKit.SetEulerCurve(action, armR, actionTimes, Around(ActionSwing(actionArmSwingDeg, 0f), p.armRestEulerR));
        if (actionBothArms)
        {
            LowPolyAnimKit.SetEulerCurve(action, armL, actionTimes, Around(ActionSwing(actionArmSwingDeg, 0f), p.armRestEulerL));
        }
        LowPolyAnimKit.SetEulerCurve(action, torso, actionTimes, ActionSwing(actionTorsoLeanDeg, p.torsoRestLeanX));

        AnimationClip death = LowPolyAnimKit.GetOrCreateClip($"{namePrefix}_Death", loop: false);
        float[] deathTimes = { 0f, 0.3f, 0.6f };
        LowPolyAnimKit.SetEulerCurve(death, torso, deathTimes, new[] { new Vector3(p.torsoRestLeanX, 0f, 0f), new Vector3(70f, 0f, 12f), new Vector3(92f, 0f, 12f) });
        LowPolyAnimKit.SetPositionYCurve(death, hips, deathTimes, new[] { hipsY, hipsY * 0.5f, hipsY * 0.35f });
        LowPolyAnimKit.SetEulerCurve(death, armL, deathTimes, new[] { p.armRestEulerL, new Vector3(-30f, 0f, 0f), new Vector3(-40f, 0f, 0f) });
        LowPolyAnimKit.SetEulerCurve(death, armR, deathTimes, new[] { p.armRestEulerR, new Vector3(-20f, 0f, 0f), new Vector3(-30f, 0f, 0f) });
        // Legs fold as the body drops.
        LowPolyAnimKit.SetEulerCurve(death, kneeL, deathTimes, new[] { new Vector3(IdleKneeBend, 0f, 0f), new Vector3(55f, 0f, 0f), new Vector3(75f, 0f, 0f) });
        LowPolyAnimKit.SetEulerCurve(death, kneeR, deathTimes, new[] { new Vector3(IdleKneeBend, 0f, 0f), new Vector3(45f, 0f, 0f), new Vector3(70f, 0f, 0f) });

        controllerWrapper = LowPolyAnimKit.BuildBipedController(
            $"{namePrefix}Animator", idle, primaryGait.stateName, primary, secondary, actionParamName, action, death, runThreshold: 1.2f);
    }

    private const float IdleKneeBend = 4f;

    // Each knee folds as its leg swings forward through the stride (peaking mid-swing) and stays
    // nearly straight while that foot is planted; the torso counter-twists against the arms.
    private static AnimationClip BuildLocomotionClip(string namePrefix, Gait gait, BipedProportions p, string hips, string torso, string armL, string armR, string legL, string legR, string kneeL, string kneeR)
    {
        AnimationClip clip = LowPolyAnimKit.GetOrCreateClip($"{namePrefix}_{gait.stateName}", loop: true);
        Times5(gait.period, out float[] times);
        float hipsY = p.hipsY;

        LowPolyAnimKit.SetPositionYCurve(clip, hips, times, new[] { hipsY, hipsY + gait.hipsBob, hipsY, hipsY + gait.hipsBob, hipsY });
        LowPolyAnimKit.SetEulerCurve(clip, legL, times, SwingX(gait.legSwingDeg, false));
        LowPolyAnimKit.SetEulerCurve(clip, legR, times, SwingX(gait.legSwingDeg, true));
        // Arms holding a weapon only bob a little instead of swinging the gun around.
        float armSwingL = p.armRestEulerL == Vector3.zero ? gait.armSwingDeg : gait.armSwingDeg * 0.2f;
        float armSwingR = p.armRestEulerR == Vector3.zero ? gait.armSwingDeg : gait.armSwingDeg * 0.2f;
        LowPolyAnimKit.SetEulerCurve(clip, armL, times, Around(SwingX(armSwingL, true), p.armRestEulerL));
        LowPolyAnimKit.SetEulerCurve(clip, armR, times, Around(SwingX(armSwingR, false), p.armRestEulerR));
        float lean = p.torsoRestLeanX + gait.torsoLeanExtra;
        float twist = gait.armSwingDeg * 0.2f;
        LowPolyAnimKit.SetEulerCurve(clip, torso, times, new[]
        {
            new Vector3(lean, -twist, 0f), new Vector3(lean, 0f, 0f), new Vector3(lean, twist, 0f), new Vector3(lean, 0f, 0f), new Vector3(lean, -twist, 0f),
        });

        // Left leg is back at the half-period key, so it swings forward (knee folding) in the last
        // quarter; the right leg does the same half a stride earlier.
        float fold = gait.legSwingDeg * 1.3f;
        float planted = IdleKneeBend;
        LowPolyAnimKit.SetEulerCurve(clip, kneeL, times, new[]
        {
            new Vector3(planted, 0f, 0f), new Vector3(planted, 0f, 0f), new Vector3(fold * 0.3f, 0f, 0f), new Vector3(fold, 0f, 0f), new Vector3(planted, 0f, 0f),
        });
        LowPolyAnimKit.SetEulerCurve(clip, kneeR, times, new[]
        {
            new Vector3(fold * 0.3f, 0f, 0f), new Vector3(fold, 0f, 0f), new Vector3(planted, 0f, 0f), new Vector3(planted, 0f, 0f), new Vector3(fold * 0.3f, 0f, 0f),
        });

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
        string kneeL = LowPolyKit.GetRelativePath(root.transform, rig.kneeL);
        string kneeR = LowPolyKit.GetRelativePath(root.transform, rig.kneeR);
        AnimationClip walk = BuildLocomotionClip("Boss", walkGait, p, hips, torso, armL, armR, legL, legR, kneeL, kneeR);

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

    private static Vector3[] Around(Vector3[] curve, Vector3 rest)
    {
        var shifted = new Vector3[curve.Length];
        for (int i = 0; i < curve.Length; i++) shifted[i] = curve[i] + rest;
        return shifted;
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
