using UnityEngine;

public enum TowerKind
{
    Hero,
    Gun,
    Cannon,
    Sniper,
    Tesla,
    Missile,
}

// Upgrade bonuses collected during the current run. Everything that fires reads its stats through
// here, so an upgrade takes effect immediately on every existing and future tower.
public static class RunModifiers
{
    private class KindBonus
    {
        public float damage;
        public float fireRate;
        public float range;
        public float splash;
        public int extraChains;
        public int extraPierce;
        public int extraProjectiles;
    }

    private static KindBonus[] bonuses;
    private static float allDamage;
    private static float allFireRate;
    private static float allRange;

    public static float ScrapMultiplier { get; private set; } = 1f;
    public static float CoreRegenPerSecond { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnDomainLoad() => Reset();

    public static void Reset()
    {
        int kindCount = System.Enum.GetValues(typeof(TowerKind)).Length;
        bonuses = new KindBonus[kindCount];
        for (int i = 0; i < kindCount; i++) bonuses[i] = new KindBonus();
        allDamage = 0f;
        allFireRate = 0f;
        allRange = 0f;
        ScrapMultiplier = 1f;
        CoreRegenPerSecond = 0f;
    }

    private static KindBonus For(TowerKind kind)
    {
        if (bonuses == null) Reset();
        return bonuses[(int)kind];
    }

    // Hero upgrades don't benefit from "all towers" bonuses, and vice versa.
    private static bool IsTower(TowerKind kind) => kind != TowerKind.Hero;

    public static float Damage(TowerKind kind) => 1f + For(kind).damage + (IsTower(kind) ? allDamage : 0f);
    public static float FireRate(TowerKind kind) => 1f + For(kind).fireRate + (IsTower(kind) ? allFireRate : 0f);
    public static float Range(TowerKind kind) => 1f + For(kind).range + (IsTower(kind) ? allRange : 0f);
    public static float Splash(TowerKind kind) => 1f + For(kind).splash;
    public static int ExtraChains(TowerKind kind) => For(kind).extraChains;
    public static int ExtraPierce(TowerKind kind) => For(kind).extraPierce;
    public static int ExtraProjectiles(TowerKind kind) => For(kind).extraProjectiles;

    public static void AddDamage(TowerKind kind, float amount) => For(kind).damage += amount;
    public static void AddFireRate(TowerKind kind, float amount) => For(kind).fireRate += amount;
    public static void AddRange(TowerKind kind, float amount) => For(kind).range += amount;
    public static void AddSplash(TowerKind kind, float amount) => For(kind).splash += amount;
    public static void AddChains(TowerKind kind, int amount) => For(kind).extraChains += amount;
    public static void AddPierce(TowerKind kind, int amount) => For(kind).extraPierce += amount;
    public static void AddProjectiles(TowerKind kind, int amount) => For(kind).extraProjectiles += amount;

    public static void AddAllDamage(float amount) => allDamage += amount;
    public static void AddAllFireRate(float amount) => allFireRate += amount;
    public static void AddAllRange(float amount) => allRange += amount;
    public static void AddScrapMultiplier(float amount) => ScrapMultiplier += amount;
    public static void AddCoreRegen(float perSecond) => CoreRegenPerSecond += perSecond;
}
