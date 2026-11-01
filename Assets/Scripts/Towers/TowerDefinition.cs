using System;
using UnityEngine;

[Serializable]
public class TowerDefinition
{
    public string displayName = "Tower";
    public int cost = 50;
    // A Tower, or a Wall - anything placed on the grid from the build cards.
    public GameObject prefab;
    public Color accentColor = Color.white;
}
