using System;
using System.Collections.Generic;
using UnityEngine;

public enum UpgradeRarity
{
    Common,
    Rare,
    Epic,
}

public class UpgradeDefinition
{
    public string Id;
    public string Title;
    public string Description;
    public string IconLetter;
    public Color IconColor;
    public UpgradeRarity Rarity;
    public int MaxStacks = 5;
    public TowerKind? RequiresTower; // Only offered once at least one of these towers is built.
    public Action Apply;
}

public static class UpgradeCatalog
{
    // Mirrors the tower accent colours used for the shop cards and turrets.
    private static readonly Color GunColor = new Color(1f, 0.76f, 0.22f);
    private static readonly Color CannonColor = new Color(1f, 0.48f, 0.18f);
    private static readonly Color SniperColor = new Color(0.45f, 0.88f, 0.35f);
    private static readonly Color TeslaColor = new Color(0.3f, 0.8f, 1f);
    private static readonly Color MissileColor = new Color(0.95f, 0.26f, 0.32f);
    private static readonly Color HeroColor = new Color(0.35f, 0.55f, 1f);
    private static readonly Color AllTowersColor = new Color(0.85f, 0.85f, 0.9f);
    private static readonly Color EconomyColor = new Color(1f, 0.82f, 0.3f);
    private static readonly Color CoreColor = new Color(0.4f, 0.95f, 1f);

    private static readonly Dictionary<string, int> taken = new Dictionary<string, int>();
    private static List<UpgradeDefinition> all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnDomainLoad() => ResetRun();

    public static void ResetRun() => taken.Clear();

    public static int TimesTaken(UpgradeDefinition upgrade) => taken.TryGetValue(upgrade.Id, out int n) ? n : 0;

    public static void Take(UpgradeDefinition upgrade)
    {
        taken[upgrade.Id] = TimesTaken(upgrade) + 1;
        upgrade.Apply?.Invoke();
    }

    public static List<UpgradeDefinition> Draw(int count)
    {
        var pool = new List<UpgradeDefinition>();
        foreach (UpgradeDefinition upgrade in All)
        {
            if (TimesTaken(upgrade) >= upgrade.MaxStacks) continue;
            if (upgrade.RequiresTower.HasValue && Tower.CountOf(upgrade.RequiresTower.Value) == 0) continue;
            pool.Add(upgrade);
        }

        var hand = new List<UpgradeDefinition>();
        while (hand.Count < count && pool.Count > 0)
        {
            float total = 0f;
            foreach (UpgradeDefinition upgrade in pool) total += Weight(upgrade.Rarity);

            float roll = UnityEngine.Random.value * total;
            int pick = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= Weight(pool[i].Rarity);
                if (roll <= 0f)
                {
                    pick = i;
                    break;
                }
            }

            hand.Add(pool[pick]);
            pool.RemoveAt(pick);
        }
        return hand;
    }

    private static float Weight(UpgradeRarity rarity)
    {
        switch (rarity)
        {
            case UpgradeRarity.Rare: return 4f;
            case UpgradeRarity.Epic: return 1.5f;
            default: return 10f;
        }
    }

    private static List<UpgradeDefinition> All => all ??= Build();

    private static List<UpgradeDefinition> Build()
    {
        return new List<UpgradeDefinition>
        {
            // All towers
            new UpgradeDefinition { Id = "all_damage", Title = "Hollow Points", Description = "All towers deal +20% damage", IconLetter = "+", IconColor = AllTowersColor, Rarity = UpgradeRarity.Common, Apply = () => RunModifiers.AddAllDamage(0.2f) },
            new UpgradeDefinition { Id = "all_firerate", Title = "Overclock", Description = "All towers fire 15% faster", IconLetter = "»", IconColor = AllTowersColor, Rarity = UpgradeRarity.Common, Apply = () => RunModifiers.AddAllFireRate(0.15f) },
            new UpgradeDefinition { Id = "all_range", Title = "Long Scopes", Description = "All towers gain +15% range", IconLetter = "R", IconColor = AllTowersColor, Rarity = UpgradeRarity.Common, MaxStacks = 3, Apply = () => RunModifiers.AddAllRange(0.15f) },

            // Hero
            new UpgradeDefinition { Id = "hero_multishot", Title = "Twin Barrels", Description = "Your hero fires +1 extra bullet", IconLetter = "H", IconColor = HeroColor, Rarity = UpgradeRarity.Rare, MaxStacks = 4, Apply = () => RunModifiers.AddProjectiles(TowerKind.Hero, 1) },
            new UpgradeDefinition { Id = "hero_firerate", Title = "Adrenaline", Description = "Your hero fires 40% faster", IconLetter = "H", IconColor = HeroColor, Rarity = UpgradeRarity.Common, Apply = () => RunModifiers.AddFireRate(TowerKind.Hero, 0.4f) },
            new UpgradeDefinition { Id = "hero_damage", Title = "Heavy Rounds", Description = "Your hero deals +50% damage", IconLetter = "H", IconColor = HeroColor, Rarity = UpgradeRarity.Common, Apply = () => RunModifiers.AddDamage(TowerKind.Hero, 0.5f) },

            // Tower specific
            new UpgradeDefinition { Id = "gun_gatling", Title = "Gatling Mod", Description = "Gun towers fire 50% faster", IconLetter = "G", IconColor = GunColor, Rarity = UpgradeRarity.Common, RequiresTower = TowerKind.Gun, Apply = () => RunModifiers.AddFireRate(TowerKind.Gun, 0.5f) },
            new UpgradeDefinition { Id = "gun_double", Title = "Double Tap", Description = "Gun towers fire +1 extra bullet", IconLetter = "G", IconColor = GunColor, Rarity = UpgradeRarity.Rare, MaxStacks = 3, RequiresTower = TowerKind.Gun, Apply = () => RunModifiers.AddProjectiles(TowerKind.Gun, 1) },
            new UpgradeDefinition { Id = "cannon_bertha", Title = "Big Bertha", Description = "Cannon blasts +40% bigger, +25% damage", IconLetter = "C", IconColor = CannonColor, Rarity = UpgradeRarity.Rare, RequiresTower = TowerKind.Cannon, Apply = () => { RunModifiers.AddSplash(TowerKind.Cannon, 0.4f); RunModifiers.AddDamage(TowerKind.Cannon, 0.25f); } },
            new UpgradeDefinition { Id = "sniper_rail", Title = "Railgun", Description = "Sniper shots pierce +3 enemies, +30% damage", IconLetter = "S", IconColor = SniperColor, Rarity = UpgradeRarity.Rare, RequiresTower = TowerKind.Sniper, Apply = () => { RunModifiers.AddPierce(TowerKind.Sniper, 3); RunModifiers.AddDamage(TowerKind.Sniper, 0.3f); } },
            new UpgradeDefinition { Id = "tesla_storm", Title = "Storm Coil", Description = "Tesla lightning chains to +2 more enemies", IconLetter = "T", IconColor = TeslaColor, Rarity = UpgradeRarity.Rare, RequiresTower = TowerKind.Tesla, Apply = () => RunModifiers.AddChains(TowerKind.Tesla, 2) },
            new UpgradeDefinition { Id = "tesla_amp", Title = "Amplifier", Description = "Tesla towers deal +40% damage", IconLetter = "T", IconColor = TeslaColor, Rarity = UpgradeRarity.Common, RequiresTower = TowerKind.Tesla, Apply = () => RunModifiers.AddDamage(TowerKind.Tesla, 0.4f) },
            new UpgradeDefinition { Id = "missile_swarm", Title = "Swarm Pods", Description = "Missile towers launch +1 extra missile", IconLetter = "M", IconColor = MissileColor, Rarity = UpgradeRarity.Epic, MaxStacks = 3, RequiresTower = TowerKind.Missile, Apply = () => RunModifiers.AddProjectiles(TowerKind.Missile, 1) },
            new UpgradeDefinition { Id = "missile_cluster", Title = "Cluster Payload", Description = "Missile blasts +50% bigger", IconLetter = "M", IconColor = MissileColor, Rarity = UpgradeRarity.Common, RequiresTower = TowerKind.Missile, Apply = () => RunModifiers.AddSplash(TowerKind.Missile, 0.5f) },

            // Economy and core
            new UpgradeDefinition { Id = "salvage", Title = "Salvage Crew", Description = "+25% scrap from kills", IconLetter = "$", IconColor = EconomyColor, Rarity = UpgradeRarity.Common, MaxStacks = 4, Apply = () => RunModifiers.AddScrapMultiplier(0.25f) },
            new UpgradeDefinition { Id = "war_chest", Title = "War Chest", Description = "Gain 150 scrap right now", IconLetter = "$", IconColor = EconomyColor, Rarity = UpgradeRarity.Common, MaxStacks = 99, Apply = () => GameManager.Instance?.AddScrap(150) },
            new UpgradeDefinition { Id = "core_plating", Title = "Reinforced Core", Description = "Core +150 max HP and heals 150", IconLetter = "HP", IconColor = CoreColor, Rarity = UpgradeRarity.Common, Apply = () => GameManager.Instance?.CoreHealth?.IncreaseMax(150f) },
            new UpgradeDefinition { Id = "core_regen", Title = "Nanobots", Description = "Core regenerates 3 HP per second", IconLetter = "HP", IconColor = CoreColor, Rarity = UpgradeRarity.Rare, MaxStacks = 3, Apply = () => RunModifiers.AddCoreRegen(3f) },
        };
    }
}
