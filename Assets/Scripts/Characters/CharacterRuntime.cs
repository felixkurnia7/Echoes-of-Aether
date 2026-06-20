using UnityEngine;

public class CharacterRuntime
{
    public CharacterData Data {get;}
    public int CurrentHP {get; private set;}
    public int CurrentMP {get; private set;}
    public bool IsAlive => CurrentHP > 0;

    public CharacterRuntime(CharacterData data)
    {
        Data = data;
        CurrentHP = data.maxHP;
        CurrentMP = data.maxMP;
    }

    public float CritChance => Data.luck * 0.5f;
    public float HPPercentage => Data.maxHP > 0 ? (float)CurrentHP / Data.maxHP : 0f;

    public int CalculateSkillDamage(SkillData skill, CharacterRuntime target)
    {
        return Mathf.Max(1, (skill.power + Data.attack) - target.Data.defense);
    }

    public int CalculateBasicAttackDamage(CharacterRuntime target)
    {
        return Mathf.Max(1, Data.attack - target.Data.defense);
    }

    public bool TryUseSkill(SkillData skill)
    {
        if (skill == null || CurrentMP < skill.manaCost)
            return false;

        CurrentMP -= skill.manaCost;
        return true;
    }

    public void TakeDamage(int amount)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
    }

    public void Heal(int amount)
    {
        CurrentHP = Mathf.Min(Data.maxHP, CurrentHP + amount);
    }
}
