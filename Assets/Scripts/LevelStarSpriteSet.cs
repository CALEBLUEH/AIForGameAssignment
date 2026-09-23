using UnityEngine;

public sealed class LevelStarSpriteSet : ScriptableObject
{
    public Sprite earnedStar;
    public Sprite hiddenStar;

    private static LevelStarSpriteSet cached;

    public static LevelStarSpriteSet Load()
    {
        if (cached == null) cached = Resources.Load<LevelStarSpriteSet>("LevelStarSpriteSet");
        return cached;
    }
}
