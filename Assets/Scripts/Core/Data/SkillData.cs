using UnityEngine;

[CreateAssetMenu(fileName = "NewSkill", menuName = "Echoes of Aether/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Identity")]
    public string skillName;
    [TextArea(2, 4)] public string description;

    [Header("Combat")]
    public SkillCategory category;
    public int power;
    public int manaCost;
    public SkillTargetType targetType;

    [Header("Presentation")]
    public Sprite icon;
    public string animationTrigger;
}
