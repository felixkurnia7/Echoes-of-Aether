using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Minimal turn-based battle controller. Reads the encounter from
/// <see cref="BattleSessionData"/>, builds a simple runtime UI, runs a
/// round-based loop (player acts, then each enemy acts) and returns to the
/// previous scene via <see cref="GameManager.EndBattle"/>.
///
/// Auto-spawns itself whenever the Battle scene loads, so no scene wiring is
/// needed.
/// </summary>
public class BattleManager : MonoBehaviour
{
    enum PlayerAction { Attack, Skill, Defend }

    class Combatant
    {
        public CharacterRuntime runtime;
        public EnemyData enemyData;
        public TMP_Text statLabel;
        public string Name => runtime.Data != null ? runtime.Data.characterName : "???";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneNames.Battle)
            return;

        if (FindFirstObjectByType<BattleManager>() != null)
            return;

        var go = new GameObject("[BattleManager]");
        go.AddComponent<BattleManager>();
    }

    Combatant player;
    readonly List<Combatant> enemies = new();

    TMP_Text messageLabel;
    TMP_Text playerStatLabel;
    Transform actionRow;
    readonly List<Button> actionButtons = new();
    readonly List<(Button button, SkillData skill)> skillButtons = new();

    PlayerAction chosenAction;
    SkillData chosenSkill;
    bool actionChosen;
    bool playerDefending;

    void Start()
    {
        Time.timeScale = 1f;

        if (!BattleSessionData.HasSession)
        {
            Debug.LogError("[BattleManager] No battle session found. Returning to previous scene.");
            if (GameManager.Instance != null)
                GameManager.Instance.EndBattle(false);
            return;
        }

        SetupCombatants();
        BuildUI();
        StartCoroutine(BattleLoop());
    }

    void SetupCombatants()
    {
        player = new Combatant { runtime = new CharacterRuntime(BattleSessionData.PlayerCharacter) };

        foreach (EnemyData data in BattleSessionData.Enemies)
        {
            if (data == null || data.characterData == null)
                continue;

            enemies.Add(new Combatant
            {
                runtime = new CharacterRuntime(data.characterData),
                enemyData = data
            });
        }
    }

    IEnumerator BattleLoop()
    {
        string opening = enemies.Count > 0 ? $"A wild {enemies[0].Name} appears!" : "Battle start!";
        SetMessage(opening);
        yield return new WaitForSeconds(1.2f);

        while (true)
        {
            playerDefending = false;
            yield return PlayerTurn();

            if (AllEnemiesDefeated())
            {
                yield return EndSequence(true);
                yield break;
            }

            foreach (Combatant enemy in enemies)
            {
                if (!enemy.runtime.IsAlive)
                    continue;

                yield return EnemyTurn(enemy);

                if (!player.runtime.IsAlive)
                {
                    yield return EndSequence(false);
                    yield break;
                }
            }
        }
    }

    IEnumerator PlayerTurn()
    {
        SetMessage("Your turn - choose an action.");
        SetButtonsInteractable(true);

        actionChosen = false;
        while (!actionChosen)
            yield return null;

        SetButtonsInteractable(false);

        Combatant target = FirstAliveEnemy();

        switch (chosenAction)
        {
            case PlayerAction.Attack:
                if (target != null)
                {
                    int dmg = player.runtime.CalculateBasicAttackDamage(target.runtime);
                    target.runtime.TakeDamage(dmg);
                    SetMessage($"You strike {target.Name} for {dmg} damage!");
                }
                break;

            case PlayerAction.Skill:
                yield return ResolvePlayerSkill(target);
                break;

            case PlayerAction.Defend:
                playerDefending = true;
                SetMessage("You brace for the next attack.");
                break;
        }

        RefreshStats();
        yield return new WaitForSeconds(1f);
    }

    IEnumerator ResolvePlayerSkill(Combatant target)
    {
        if (chosenSkill == null || !player.runtime.TryUseSkill(chosenSkill))
        {
            SetMessage("Not enough MP!");
            yield break;
        }

        if (chosenSkill.category == SkillCategory.Support || chosenSkill.targetType == SkillTargetType.Self)
        {
            int heal = Mathf.Max(1, chosenSkill.power);
            player.runtime.Heal(heal);
            SetMessage($"You cast {chosenSkill.skillName} and recover {heal} HP.");
        }
        else if (target != null)
        {
            int dmg = player.runtime.CalculateSkillDamage(chosenSkill, target.runtime);
            target.runtime.TakeDamage(dmg);
            SetMessage($"You cast {chosenSkill.skillName} on {target.Name} for {dmg} damage!");
        }
    }

    IEnumerator EnemyTurn(Combatant enemy)
    {
        EnemyData data = enemy.enemyData;

        bool wantsHeal = data != null
            && data.healSkill != null
            && enemy.runtime.HPPercentage <= data.healThreshold
            && enemy.runtime.TryUseSkill(data.healSkill);

        if (wantsHeal)
        {
            int heal = Mathf.Max(1, data.healSkill.power);
            enemy.runtime.Heal(heal);
            SetMessage($"{enemy.Name} heals for {heal} HP.");
        }
        else
        {
            int dmg = enemy.runtime.CalculateBasicAttackDamage(player.runtime);
            if (playerDefending)
                dmg = Mathf.Max(1, dmg / 2);

            player.runtime.TakeDamage(dmg);
            SetMessage($"{enemy.Name} hits you for {dmg} damage!");
        }

        RefreshStats();
        yield return new WaitForSeconds(1f);
    }

    IEnumerator EndSequence(bool victory)
    {
        SetButtonsInteractable(false);
        SetMessage(victory ? "Victory!" : "You were defeated...");
        yield return new WaitForSeconds(1.8f);

        if (GameManager.Instance != null)
            GameManager.Instance.EndBattle(victory);
        else
            Debug.LogError("[BattleManager] GameManager missing; cannot leave battle.");
    }

    bool AllEnemiesDefeated()
    {
        foreach (Combatant enemy in enemies)
            if (enemy.runtime.IsAlive)
                return false;
        return true;
    }

    Combatant FirstAliveEnemy()
    {
        foreach (Combatant enemy in enemies)
            if (enemy.runtime.IsAlive)
                return enemy;
        return null;
    }

    void OnAttack()
    {
        chosenAction = PlayerAction.Attack;
        actionChosen = true;
    }

    void OnDefend()
    {
        chosenAction = PlayerAction.Defend;
        actionChosen = true;
    }

    void OnSkill(SkillData skill)
    {
        chosenAction = PlayerAction.Skill;
        chosenSkill = skill;
        actionChosen = true;
    }

    void SetButtonsInteractable(bool value)
    {
        foreach (Button button in actionButtons)
            button.interactable = value;

        foreach ((Button button, SkillData skill) in skillButtons)
            button.interactable = value && player.runtime.CurrentMP >= skill.manaCost;
    }

    void SetMessage(string text)
    {
        if (messageLabel != null)
            messageLabel.text = text;
    }

    void RefreshStats()
    {
        if (playerStatLabel != null)
            playerStatLabel.text = $"{player.Name}\nHP {player.runtime.CurrentHP}/{player.runtime.Data.maxHP}    MP {player.runtime.CurrentMP}/{player.runtime.Data.maxMP}";

        foreach (Combatant enemy in enemies)
        {
            if (enemy.statLabel == null)
                continue;

            string hp = enemy.runtime.IsAlive
                ? $"HP {enemy.runtime.CurrentHP}/{enemy.runtime.Data.maxHP}"
                : "Defeated";
            enemy.statLabel.text = $"{enemy.Name}\n{hp}";
        }
    }

    // ---------- UI construction ----------

    void BuildUI()
    {
        var canvasGo = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        EnsureEventSystem();

        // Enemy stats (top)
        var enemyColumn = CreatePanel(canvas.transform, "EnemyStats",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-40f, -40f), new Vector2(520f, 0f));
        var enemyLayout = enemyColumn.gameObject.AddComponent<VerticalLayoutGroup>();
        enemyLayout.spacing = 6f;
        enemyLayout.childControlWidth = true;
        enemyLayout.childControlHeight = true;
        enemyLayout.childForceExpandWidth = true;
        AddFitter(enemyColumn.gameObject);

        foreach (Combatant enemy in enemies)
            enemy.statLabel = CreateText(enemyColumn, "Enemy", 30f, TextAlignmentOptions.Right);

        // Message line (center)
        var messagePanel = CreatePanel(canvas.transform, "Message",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 120f), new Vector2(1100f, 80f));
        messagePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        messageLabel = CreateText(messagePanel, "MessageText", 34f, TextAlignmentOptions.Center);
        StretchToParent(messageLabel.rectTransform);

        // Player stats (bottom-left)
        var playerPanel = CreatePanel(canvas.transform, "PlayerStats",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(40f, 40f), new Vector2(520f, 120f));
        playerStatLabel = CreateText(playerPanel, "PlayerText", 30f, TextAlignmentOptions.Left);
        StretchToParent(playerStatLabel.rectTransform);

        // Action buttons (bottom-right)
        var actionPanel = CreatePanel(canvas.transform, "Actions",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-40f, 40f), new Vector2(360f, 0f));
        var actionLayout = actionPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        actionLayout.spacing = 8f;
        actionLayout.childControlWidth = true;
        actionLayout.childControlHeight = true;
        actionLayout.childForceExpandWidth = true;
        AddFitter(actionPanel.gameObject);
        actionRow = actionPanel;

        Button attack = CreateButton(actionRow, "Attack", OnAttack);
        actionButtons.Add(attack);

        if (player.runtime.Data.skillList != null)
        {
            foreach (SkillData skill in player.runtime.Data.skillList)
            {
                if (skill == null)
                    continue;

                SkillData captured = skill;
                Button skillBtn = CreateButton(actionRow, $"{skill.skillName} ({skill.manaCost} MP)", () => OnSkill(captured));
                skillButtons.Add((skillBtn, captured));
            }
        }

        Button defend = CreateButton(actionRow, "Defend", OnDefend);
        actionButtons.Add(defend);

        SetButtonsInteractable(false);
        RefreshStats();
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            return;

        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
    }

    static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.35f);
        image.raycastTarget = false;

        return rect;
    }

    static TMP_Text CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var label = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = size + 18f;

        return label;
    }

    Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.2f, 0.28f, 0.95f);

        var button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 56f;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        text.text = label;
        text.fontSize = 26f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        StretchToParent(text.rectTransform);

        return button;
    }

    static void AddFitter(GameObject go)
    {
        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 6f);
        rect.offsetMax = new Vector2(-12f, -6f);
    }
}
