using System;
using UnityEngine;

[Serializable]
public class TowerDefinition
{
    public string displayName = "Tower";
    public int cost = 50;
    public Tower prefab;
    public Color accentColor = Color.white;
}
