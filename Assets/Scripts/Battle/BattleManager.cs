using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Minimal turn-based battle controller. Reads the encounter from
/// <see cref="BattleSessionData"/>, builds a simple runtime UI, runs a
/// speed-ordered turn loop and returns to the previous scene via
/// <see cref="GameManager.EndBattle"/>.
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
        public Button targetButton;
        public Image targetBackground;
        public BattleActor actor;
        public bool isPlayer;
        public string Name => runtime.Data != null ? runtime.Data.characterName : "???";
        public int Speed => runtime.Data != null ? runtime.Data.speed : 0;
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
    CanvasGroup messageGroup;
    Coroutine messageFadeRoutine;
    TMP_Text playerStatLabel;
    Transform actionRow;
    readonly List<Button> actionButtons = new();
    readonly List<(Button button, SkillData skill)> skillButtons = new();

    PlayerAction chosenAction;
    SkillData chosenSkill;
    Combatant chosenTarget;
    bool actionChosen;
    bool targetChosen;
    bool requiresTarget;
    bool playerDefending;

    BattleTutorialGuide battleTutorial;

    static readonly Color EnemyRowColor = new Color(0.06f, 0.07f, 0.1f, 0.55f);
    static readonly Color EnemyTargetColor = new Color(0.22f, 0.14f, 0.1f, 0.92f);
    static readonly Color EnemySelectedColor = new Color(0.35f, 0.22f, 0.08f, 0.95f);

    const float MessageHoldDuration = 2f;
    const float MessageFadeDuration = 0.65f;
    const float MessageEndHoldDuration = 2.5f;

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
        SetupActors();
        battleTutorial = BattleTutorialGuide.TryCreate();
        BuildUI();
        StartCoroutine(BattleLoop());
    }

    void SetupCombatants()
    {
        player = new Combatant { runtime = new CharacterRuntime(BattleSessionData.PlayerCharacter), isPlayer = true };

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

    /// <summary>
    /// Hooks up the 3D actors that are pre-placed in the Battle scene. Actors are
    /// matched to combatants by <see cref="BattleActor.Side"/>, moved onto their
    /// spawn markers ("Spawn Player" and up to six "Spawn Enemy" empties) and
    /// turned to face each other. Everything stays optional, so the battle still
    /// runs if no actors or markers exist.
    /// </summary>
    const string PlayerSpawnName = "Spawn Player";
    const string EnemySpawnName = "Spawn Enemy";

    void SetupActors()
    {
        // Snapshot of actors already in the scene (used as a migration fallback
        // when a character has no battlePrefab assigned yet).
        var preplaced = new List<BattleActor>(FindObjectsByType<BattleActor>(FindObjectsSortMode.None));

        Transform playerSpawn = FindMarker(PlayerSpawnName);
        List<Transform> enemySpawns = FindEnemySpawnMarkers();

        Vector3 playerPos = playerSpawn != null ? playerSpawn.position : new Vector3(-3f, 0f, 0f);
        Quaternion playerRot = playerSpawn != null ? playerSpawn.rotation : Quaternion.identity;
        player.actor = ResolveActor(player.runtime.Data, BattleSide.Player, preplaced, playerPos, playerRot);

        for (int i = 0; i < enemies.Count; i++)
        {
            Transform spawn = GetEnemySpawnMarker(enemySpawns, i);
            Vector3 pos = spawn != null ? spawn.position : new Vector3(2f, 0f, 0f);
            Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;
            enemies[i].actor = ResolveActor(enemies[i].runtime.Data, BattleSide.Enemy, preplaced, pos, rot);
        }

        if (enemySpawns.Count > 0 && enemies.Count > enemySpawns.Count)
        {
            Debug.LogWarning(
                $"[BattleManager] {enemies.Count} enemies but only {enemySpawns.Count} '{EnemySpawnName}' markers; extras share the last slot.");
        }

        // Hide any pre-placed actors that data-driven spawns replaced.
        foreach (BattleActor leftover in preplaced)
            if (leftover != null)
                leftover.gameObject.SetActive(false);

        // Turn every living actor toward the player for a proper face-off.
        if (player.actor != null)
        {
            foreach (Combatant enemy in enemies)
            {
                if (enemy.actor != null)
                    enemy.actor.FaceInstant(player.actor.transform.position);
            }

            Combatant firstEnemy = FirstAliveEnemy();
            if (firstEnemy?.actor != null)
                player.actor.FaceInstant(firstEnemy.actor.transform.position);
        }
    }

    /// <summary>
    /// Spawns the character's <see cref="CharacterData.battlePrefab"/> at the
    /// given pose. If no prefab is assigned, falls back to reusing a pre-placed
    /// actor of the matching side (removing it from <paramref name="preplaced"/>
    /// so it isn't disabled afterwards).
    /// </summary>
    BattleActor ResolveActor(CharacterData data, BattleSide side, List<BattleActor> preplaced, Vector3 pos, Quaternion rot)
    {
        if (data != null && data.battlePrefab != null)
        {
            GameObject go = Instantiate(data.battlePrefab, pos, rot);
            BattleActor spawned = go.GetComponent<BattleActor>() ?? go.GetComponentInChildren<BattleActor>();
            if (spawned != null)
                spawned.PlaceAt(pos, rot);
            else
                Debug.LogWarning($"[BattleManager] battlePrefab for '{data.characterName}' has no BattleActor component.");
            return spawned;
        }

        for (int i = 0; i < preplaced.Count; i++)
        {
            if (preplaced[i] == null || preplaced[i].Side != side)
                continue;

            BattleActor actor = preplaced[i];
            preplaced.RemoveAt(i);
            actor.PlaceAt(pos, rot);
            return actor;
        }

        return null;
    }

    /// <summary>
    /// Collects every <see cref="EnemySpawnName"/> in the active scene and sorts
    /// them column-first (lower X, then higher Z = top row) so lineup index 0
    /// maps to the nearest column's top slot.
    /// </summary>
    static List<Transform> FindEnemySpawnMarkers()
    {
        var markers = new List<Transform>();
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
            CollectSpawnMarkers(root.transform, markers);

        markers.Sort(CompareEnemySpawnMarkers);
        return markers;
    }

    static void CollectSpawnMarkers(Transform node, List<Transform> results)
    {
        if (node.name == EnemySpawnName)
            results.Add(node);

        for (int i = 0; i < node.childCount; i++)
            CollectSpawnMarkers(node.GetChild(i), results);
    }

    static int CompareEnemySpawnMarkers(Transform a, Transform b)
    {
        Vector3 pa = a.position;
        Vector3 pb = b.position;

        int xCompare = pa.x.CompareTo(pb.x);
        if (xCompare != 0)
            return xCompare;

        return pb.z.CompareTo(pa.z);
    }

    static Transform GetEnemySpawnMarker(List<Transform> markers, int enemyIndex)
    {
        if (markers == null || markers.Count == 0)
            return null;

        int index = Mathf.Min(enemyIndex, markers.Count - 1);
        return markers[index];
    }

    static Transform FindMarker(string markerName)
    {
        GameObject go = GameObject.Find(markerName);
        return go != null ? go.transform : null;
    }

    /// <summary>
    /// Runs the attacker's lunge animation (if it has an actor) and fires
    /// <paramref name="onImpact"/> at the moment damage should land. Falls back to
    /// applying the impact immediately when no actors are present.
    /// </summary>
    IEnumerator PerformAttack(Combatant attacker, Combatant target, System.Action onImpact)
    {
        if (attacker?.actor != null && target?.actor != null)
            yield return attacker.actor.AttackRoutine(target.actor, onImpact);
        else
            onImpact?.Invoke();
    }

    /// <summary>Plays the hit or death reaction on a combatant's actor and refreshes the HUD.</summary>
    void ReactToDamage(Combatant victim)
    {
        if (victim?.actor != null)
        {
            if (victim.runtime.IsAlive)
                victim.actor.PlayHit();
            else
                victim.actor.PlayDeath();
        }

        AudioManager.Instance?.PlayHitSound();
        RefreshStats();
    }

    IEnumerator BattleLoop()
    {
        string opening = enemies.Count switch
        {
            0 => "Battle start!",
            1 => $"A wild {enemies[0].Name} appears!",
            _ => $"{enemies.Count} enemies appear!"
        };
        SetMessage(opening);
        yield return new WaitForSeconds(1.2f);

        while (true)
        {
            playerDefending = false;
            List<Combatant> turnOrder = BuildTurnOrder();

            foreach (Combatant combatant in turnOrder)
            {
                if (combatant.isPlayer)
                {
                    if (!player.runtime.IsAlive)
                        continue;

                    yield return PlayerTurn();
                }
                else
                {
                    if (!combatant.runtime.IsAlive)
                        continue;

                    yield return EnemyTurn(combatant);
                }

                if (AllEnemiesDefeated())
                {
                    yield return EndSequence(true);
                    yield break;
                }

                if (!player.runtime.IsAlive)
                {
                    yield return EndSequence(false);
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// Living combatants sorted by <see cref="CharacterData.speed"/> (highest first).
    /// Ties favour the player.
    /// </summary>
    List<Combatant> BuildTurnOrder()
    {
        var order = new List<Combatant> { player };
        foreach (Combatant enemy in enemies)
        {
            if (enemy.runtime.IsAlive)
                order.Add(enemy);
        }

        order.Sort(CompareTurnPriority);
        return order;
    }

    static int CompareTurnPriority(Combatant a, Combatant b)
    {
        int speedCompare = b.Speed.CompareTo(a.Speed);
        if (speedCompare != 0)
            return speedCompare;

        if (a.isPlayer != b.isPlayer)
            return a.isPlayer ? -1 : 1;

        return 0;
    }

    IEnumerator PlayerTurn()
    {
        if (battleTutorial != null)
            yield return battleTutorial.OnPlayerTurnStart();

        chosenTarget = null;
        SetMessage(GetTurnPrompt());
        SetActionButtonsForTurn(true);
        SetTargetButtonsInteractable(false);

        actionChosen = false;
        requiresTarget = false;
        while (!actionChosen)
            yield return null;

        SetButtonsInteractable(false);

        if (chosenAction == PlayerAction.Skill && IsSelfHealSkill(chosenSkill))
            requiresTarget = false;

        if (RequiresEnemyTargetSelection())
        {
            SetMessage("Select an enemy to target.");
            SetTargetButtonsInteractable(true);

            targetChosen = false;
            while (!targetChosen)
                yield return null;

            SetTargetButtonsInteractable(false);
            ResetEnemyRowColors();
        }
        else if (requiresTarget)
        {
            chosenTarget = FirstAliveEnemy();
        }

        switch (chosenAction)
        {
            case PlayerAction.Attack:
                if (chosenTarget != null)
                {
                    Combatant attackTarget = chosenTarget;
                    yield return PerformAttack(player, attackTarget, () =>
                    {
                        int dmg = player.runtime.CalculateBasicAttackDamage(attackTarget.runtime);
                        attackTarget.runtime.TakeDamage(dmg);
                        SetMessage($"You strike {attackTarget.Name} for {dmg} damage!");
                        ReactToDamage(attackTarget);
                    });

                }
                break;

            case PlayerAction.Skill:
                yield return ResolvePlayerSkill(chosenTarget);
                break;

            case PlayerAction.Defend:
                playerDefending = true;
                SetMessage("You brace for the next attack.");
                break;
        }

        RefreshStats();
        yield return new WaitForSeconds(1f);
    }

    bool RequiresEnemyTargetSelection()
    {
        if (!requiresTarget)
            return false;

        if (chosenAction == PlayerAction.Skill && IsSelfHealSkill(chosenSkill))
            return false;

        return CountAliveEnemies() > 1;
    }

    static bool NeedsEnemyTarget(SkillData skill)
    {
        if (skill == null)
            return false;

        if (IsSelfHealSkill(skill))
            return false;

        return skill.targetType == SkillTargetType.Enemy;
    }

    int CountAliveEnemies()
    {
        int count = 0;
        foreach (Combatant enemy in enemies)
            if (enemy.runtime.IsAlive)
                count++;
        return count;
    }

    IEnumerator ResolvePlayerSkill(Combatant target)
    {
        if (chosenSkill == null || !player.runtime.TryUseSkill(chosenSkill))
        {
            SetMessage("Not enough MP!");
            yield break;
        }

        if (IsSelfHealSkill(chosenSkill))
        {
            int heal = Mathf.Max(1, chosenSkill.power);
            player.runtime.Heal(heal);
            SetMessage($"You cast {chosenSkill.skillName} and recover {heal} HP.");
        }
        else if (target != null)
        {
            SkillData skill = chosenSkill;
            yield return PerformAttack(player, target, () =>
            {
                int dmg = player.runtime.CalculateSkillDamage(skill, target.runtime);
                target.runtime.TakeDamage(dmg);
                SetMessage($"You cast {skill.skillName} on {target.Name} for {dmg} damage!");
                ReactToDamage(target);
            });
        }
        else
        {
            SetMessage("No valid target for that skill.");
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
            yield return PerformAttack(enemy, player, () =>
            {
                int dmg = enemy.runtime.CalculateBasicAttackDamage(player.runtime);
                if (playerDefending)
                    dmg = Mathf.Max(1, dmg / 2);

                player.runtime.TakeDamage(dmg);
                SetMessage($"{enemy.Name} hits you for {dmg} damage!");
                ReactToDamage(player);
            });
        }

        RefreshStats();
        yield return new WaitForSeconds(1f);
    }

    IEnumerator EndSequence(bool victory)
    {
        SetButtonsInteractable(false);
        SetTargetButtonsInteractable(false);
        SetMessage(victory ? "Victory!" : "You were defeated...", holdDuration: MessageEndHoldDuration);

        if (victory && battleTutorial != null)
            yield return battleTutorial.OnVictory();
        else
            yield return new WaitForSeconds(1.8f);

        battleTutorial?.HideBubble();

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
        if (!IsTutorialActionAllowed(PlayerAction.Attack))
            return;

        chosenAction = PlayerAction.Attack;
        chosenSkill = null;
        requiresTarget = CountAliveEnemies() > 0;
        actionChosen = true;
    }

    void OnDefend()
    {
        if (!IsTutorialActionAllowed(PlayerAction.Defend))
            return;

        chosenAction = PlayerAction.Defend;
        chosenSkill = null;
        requiresTarget = false;
        actionChosen = true;
    }

    void OnSkill(SkillData skill)
    {
        if (!IsTutorialActionAllowed(PlayerAction.Skill, skill))
            return;

        chosenAction = PlayerAction.Skill;
        chosenSkill = skill;
        requiresTarget = !IsSelfHealSkill(skill) && NeedsEnemyTarget(skill) && CountAliveEnemies() > 0;
        actionChosen = true;
    }

    void OnEnemyTargetSelected(Combatant enemy)
    {
        if (enemy == null || !enemy.runtime.IsAlive)
            return;

        chosenTarget = enemy;
        targetChosen = true;
        HighlightSelectedEnemy(enemy);
    }

    void HighlightSelectedEnemy(Combatant selected)
    {
        foreach (Combatant enemy in enemies)
        {
            if (enemy.targetBackground == null)
                continue;

            enemy.targetBackground.color = enemy == selected
                ? EnemySelectedColor
                : EnemyTargetColor;
        }
    }

    void ResetEnemyRowColors()
    {
        foreach (Combatant enemy in enemies)
        {
            if (enemy.targetBackground != null)
                enemy.targetBackground.color = EnemyRowColor;
        }
    }

    void SetTargetButtonsInteractable(bool value)
    {
        foreach (Combatant enemy in enemies)
        {
            if (enemy.targetButton == null)
                continue;

            bool alive = enemy.runtime.IsAlive;
            enemy.targetButton.interactable = value && alive;

            if (enemy.targetBackground != null)
            {
                enemy.targetBackground.color = value && alive ? EnemyTargetColor : EnemyRowColor;
                enemy.targetBackground.raycastTarget = value && alive;
            }
        }
    }

    void SetButtonsInteractable(bool value)
    {
        if (!value)
        {
            foreach (Button button in actionButtons)
                button.interactable = false;

            foreach ((Button button, SkillData _) in skillButtons)
                button.interactable = false;
            return;
        }

        SetActionButtonsForTurn(true);
    }

    void SetActionButtonsForTurn(bool enabled)
    {
        if (!enabled)
        {
            SetButtonsInteractable(false);
            return;
        }

        bool restrict = battleTutorial != null && battleTutorial.IsRestrictingActions;
        BattleTutorialGuide.RequiredAction required = restrict
            ? battleTutorial.CurrentRequiredAction
            : BattleTutorialGuide.RequiredAction.None;

        bool freeChoice = required == BattleTutorialGuide.RequiredAction.None;

        if (actionButtons.Count > 0)
            actionButtons[0].interactable = freeChoice || required == BattleTutorialGuide.RequiredAction.Attack;

        if (actionButtons.Count > 1)
            actionButtons[actionButtons.Count - 1].interactable = freeChoice;

        foreach ((Button button, SkillData skill) in skillButtons)
        {
            bool skillAllowed = freeChoice;
            if (!freeChoice)
            {
                skillAllowed = required switch
                {
                    BattleTutorialGuide.RequiredAction.Skill => IsOffensiveSkill(skill),
                    BattleTutorialGuide.RequiredAction.Heal => IsHealSkill(skill),
                    _ => false
                };
            }

            button.interactable = skillAllowed && player.runtime.CurrentMP >= skill.manaCost;
        }
    }

    string GetTurnPrompt()
    {
        if (battleTutorial == null || !battleTutorial.IsRestrictingActions)
            return "Your turn - choose an action.";

        return battleTutorial.CurrentRequiredAction switch
        {
            BattleTutorialGuide.RequiredAction.Attack => "Your turn - choose Attack.",
            BattleTutorialGuide.RequiredAction.Skill => "Your turn - choose a skill.",
            BattleTutorialGuide.RequiredAction.Heal => "Your turn - use Heal.",
            _ => "Your turn - choose an action."
        };
    }

    bool IsTutorialActionAllowed(PlayerAction action, SkillData skill = null)
    {
        if (battleTutorial == null || !battleTutorial.IsRestrictingActions)
            return true;

        return battleTutorial.CurrentRequiredAction switch
        {
            BattleTutorialGuide.RequiredAction.Attack => action == PlayerAction.Attack,
            BattleTutorialGuide.RequiredAction.Skill => action == PlayerAction.Skill && IsOffensiveSkill(skill),
            BattleTutorialGuide.RequiredAction.Heal => action == PlayerAction.Skill && IsHealSkill(skill),
            _ => true
        };
    }

    static bool IsSelfHealSkill(SkillData skill)
    {
        if (skill == null)
            return false;

        if (skill.category == SkillCategory.Support)
            return true;

        return skill.targetType == SkillTargetType.Self || skill.targetType == SkillTargetType.Ally;
    }

    static bool IsHealSkill(SkillData skill) => IsSelfHealSkill(skill);

    static bool IsOffensiveSkill(SkillData skill)
    {
        return skill != null && !IsHealSkill(skill);
    }

    void SetMessage(string text, float holdDuration = MessageHoldDuration, bool autoFade = true)
    {
        if (messageFadeRoutine != null)
        {
            StopCoroutine(messageFadeRoutine);
            messageFadeRoutine = null;
        }

        if (messageLabel == null)
            return;

        if (string.IsNullOrEmpty(text))
        {
            messageLabel.text = string.Empty;
            if (messageGroup != null)
                messageGroup.alpha = 0f;
            return;
        }

        messageLabel.text = text;

        if (messageGroup != null)
            messageGroup.alpha = 1f;

        if (autoFade)
            messageFadeRoutine = StartCoroutine(FadeOutMessage(holdDuration));
    }

    IEnumerator FadeOutMessage(float holdDuration)
    {
        yield return new WaitForSeconds(holdDuration);

        if (messageGroup == null)
        {
            if (messageLabel != null)
                messageLabel.text = string.Empty;
            messageFadeRoutine = null;
            yield break;
        }

        float startAlpha = messageGroup.alpha;
        float elapsed = 0f;
        while (elapsed < MessageFadeDuration)
        {
            elapsed += Time.deltaTime;
            messageGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / MessageFadeDuration);
            yield return null;
        }

        messageGroup.alpha = 0f;
        messageLabel.text = string.Empty;
        messageFadeRoutine = null;
    }

    void RefreshStats()
    {
        if (playerStatLabel != null)
            playerStatLabel.text = $"{player.Name}\nHP {player.runtime.CurrentHP}/{player.runtime.Data.maxHP}    MP {player.runtime.CurrentMP}/{player.runtime.Data.maxMP}    SPD {player.Speed}";

        foreach (Combatant enemy in enemies)
        {
            if (enemy.statLabel == null)
                continue;

            string hp = enemy.runtime.IsAlive
                ? $"HP {enemy.runtime.CurrentHP}/{enemy.runtime.Data.maxHP}    SPD {enemy.Speed}"
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
        enemyLayout.spacing = 8f;
        enemyLayout.padding = new RectOffset(10, 10, 10, 10);
        enemyLayout.childAlignment = TextAnchor.UpperRight;
        enemyLayout.childControlWidth = true;
        enemyLayout.childControlHeight = true;
        enemyLayout.childForceExpandWidth = true;
        AddFitter(enemyColumn.gameObject);

        float enemyRowHeight = enemies.Count > 3 ? 60f : 78f;
        foreach (Combatant enemy in enemies)
            CreateEnemyTargetRow(enemyColumn, enemy, enemyRowHeight);

        // Message line (center)
        var messagePanel = CreatePanel(canvas.transform, "Message",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 120f), new Vector2(1100f, 80f));
        messagePanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        messageGroup = messagePanel.gameObject.AddComponent<CanvasGroup>();
        messageGroup.alpha = 0f;
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

    void CreateEnemyTargetRow(Transform parent, Combatant enemy, float rowHeight)
    {
        var go = new GameObject(enemy.Name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.color = EnemyRowColor;
        image.raycastTarget = false;

        var button = go.GetComponent<Button>();
        button.interactable = false;
        Combatant captured = enemy;
        button.onClick.AddListener(() => OnEnemyTargetSelected(captured));

        var colors = button.colors;
        colors.highlightedColor = EnemySelectedColor;
        colors.pressedColor = EnemySelectedColor;
        colors.selectedColor = EnemySelectedColor;
        button.colors = colors;

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = rowHeight;

        enemy.targetButton = button;
        enemy.targetBackground = image;
        enemy.statLabel = CreateEnemyRowText(go.transform);
    }

    TMP_Text CreateEnemyRowText(Transform parent)
    {
        var textGo = new GameObject("Stats", typeof(RectTransform));
        textGo.transform.SetParent(parent, false);

        var rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 8f);
        rect.offsetMax = new Vector2(-12f, -8f);

        var label = textGo.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 28f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.TopRight;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.richText = true;
        label.raycastTarget = false;

        return label;
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
