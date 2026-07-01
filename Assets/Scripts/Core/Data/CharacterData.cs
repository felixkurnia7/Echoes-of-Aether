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

    [Header("Battle")]
    [Tooltip("Prefab (with a BattleActor) spawned to represent this character in the Battle scene. Leave empty to fall back to a pre-placed actor in the scene.")]
    public GameObject battlePrefab;

    [Header("Skills")]
    public SkillData[] skillList;
}
