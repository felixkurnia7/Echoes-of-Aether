using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Echoes of Aether/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public CharacterData characterData;

    [Header("AI")]
    [Range(0f, 1f)] public float healThreshold = 0.3f;
    public SkillData healSkill;
}
