using System;

[Serializable]
public class EnemySpawnEntry
{
    public Enemy prefab;
    public int threatCost = 1;
    public int firstWave = 1;
    public float weight = 1f;
}
