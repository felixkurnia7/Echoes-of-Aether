using UnityEngine;

public class CombatFormulaTest : MonoBehaviour
{
    [SerializeField] CharacterData heroData;
    [SerializeField] CharacterData slimeData;
    [SerializeField] SkillData slashSkill;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (heroData == null || slimeData == null || slashSkill == null)
        {
            Debug.LogWarning("[CombatTest] Assign all SO references in Inspector.");
            return;
        }
        var hero = new CharacterRuntime(heroData);
        var slime = new CharacterRuntime(slimeData);
        Debug.Log($"--- Combat Formula Test ---");
        Debug.Log($"Hero: {hero.Data.characterName} | HP {hero.CurrentHP} | ATK {hero.Data.attack}");
        Debug.Log($"Slime: {slime.Data.characterName} | HP {slime.CurrentHP} | DEF {slime.Data.defense}");
        int damage = hero.CalculateSkillDamage(slashSkill, slime);
        slime.TakeDamage(damage);
        Debug.Log($"{hero.Data.characterName} uses {slashSkill.skillName} → {damage} damage");
        Debug.Log($"Slime HP after: {slime.CurrentHP}/{slime.Data.maxHP}");
        Debug.Log($"Expected: (10 + 12) - 3 = 19 damage");
    }
}
