using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "AIFG/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Basic Stats")]
    public float maxHealth = 100f;
    public float attackPower = 20f;
    public float defense = 5f;
    public float attackRange = 5f;
    public float attackSpeed = 1f;

    [Header("Other Stats")]
    public float resistance = 5f;
    public float skillPointRegeneration = 7f;
    public float skillPointCapacity = 100f;
    public float durationRate = 1f;
    public float manipulationRate = 1f;
}
