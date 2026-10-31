using System;
using UnityEngine;

public enum HeroWeaponKind
{
    Rifle,
    Bow,
}

// One weapon the hero can hold. Stats are base values; hero upgrades from RunModifiers stack on top.
[Serializable]
public class HeroWeapon
{
    public HeroWeaponKind kind;
    public GameObject model;
    public Projectile projectilePrefab;
    public float fireRate = 4f;
    public float damage = 10f;
    public float range = 10f;
    public SfxId fireSound = SfxId.Shoot;
    public float firePitch = 1.25f;
    public float fireVolume = 0.8f;
}
