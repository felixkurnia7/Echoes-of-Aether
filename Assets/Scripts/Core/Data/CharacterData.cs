using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Echoes of Aether/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterName;

    [Header("Base Stats")]
    public int maxHP;
    public int maxMP;
    public int attack;
    public int defense;
    public int speed;
    public int luck;

    [Header("Presentation")]
    public Sprite portrait;

    [Header("Skills")]
    public SkillData[] skillList;
}
