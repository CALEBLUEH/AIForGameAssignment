using UnityEngine;

[CreateAssetMenu(menuName = "AIFG/Status Icon Catalog", fileName = "StatusIconCatalog")]
public class StatusIconCatalog : ScriptableObject
{
    [Header("Buff icons")]
    public Sprite strengthIcon;
    public Sprite rageIcon;
    public Sprite hardeningIcon;
    public Sprite fortifiedIcon;
    [Header("Debuff icons")]
    public Sprite weakIcon;
    public Sprite dullIcon;
    public Sprite penetrationIcon;
    public Sprite piercingIcon;
    [Header("Layout")]
    public Vector2 iconSize = new Vector2(22f, 22f);
    public float iconSpacing = 4f;

    public Sprite GetIcon(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Strength: return strengthIcon;
            case StatusEffectType.Rage: return rageIcon;
            case StatusEffectType.Weak: return weakIcon;
            case StatusEffectType.Dull: return dullIcon;
            case StatusEffectType.Hardening: return hardeningIcon;
            case StatusEffectType.Fortified: return fortifiedIcon;
            case StatusEffectType.Penetration: return penetrationIcon;
            case StatusEffectType.Piercing: return piercingIcon;
            default: return null;
        }
    }
}
