using System.Collections;
using Fungus;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Knight-guided battle tutorial using the Fungus bubble <see cref="SayDialog"/>.
/// Dialogue is shown at the start of each player turn (turns 1–3) and after victory.
/// Turns 1–3 also restrict the player to Attack, an offensive skill, or Heal.
/// </summary>
public class BattleTutorialGuide
{
    public enum RequiredAction
    {
        None,
        Attack,
        Skill,
        Heal
    }

    const string KnightObjectName = "NPC_Knight";
    const int BubbleCanvasSortOrder = 120;

    readonly Character knightCharacter;
    readonly SayDialog bubbleDialog;
    readonly DialogInput dialogInput;

    int playerTurnCount;

    public bool IsRestrictingActions => playerTurnCount >= 1 && playerTurnCount <= 3;

    public RequiredAction CurrentRequiredAction
    {
        get
        {
            return playerTurnCount switch
            {
                1 => RequiredAction.Attack,
                2 => RequiredAction.Skill,
                3 => RequiredAction.Heal,
                _ => RequiredAction.None
            };
        }
    }

    BattleTutorialGuide(Character knight, SayDialog dialog, DialogInput input)
    {
        knightCharacter = knight;
        bubbleDialog = dialog;
        dialogInput = input;
    }

    public static BattleTutorialGuide TryCreate()
    {
        GameObject knightObject = GameObject.Find(KnightObjectName);

        if (!BattleSessionData.BattleTutorial)
        {
            if (knightObject != null)
                knightObject.SetActive(false);
            return null;
        }

        if (knightObject == null)
        {
            Debug.LogWarning("[BattleTutorial] NPC_Knight not found in the Battle scene.");
            return null;
        }

        knightObject.SetActive(true);

        if (knightObject.TryGetComponent(out NPCWalker walker))
            walker.enabled = false;

        Character knight = knightObject.GetComponent<Character>();
        if (knight == null)
        {
            Debug.LogWarning("[BattleTutorial] NPC_Knight has no Fungus Character component.");
            return null;
        }

        SpeechBubbleAnchor anchor = knightObject.GetComponent<SpeechBubbleAnchor>();
        if (anchor == null)
            anchor = knightObject.AddComponent<SpeechBubbleAnchor>();
        anchor.Configure(knight);

        SayDialog dialog = BubbleDialogs.Resolve(SayBubbleStyle.Bubble);
        if (dialog == null)
        {
            Debug.LogWarning("[BattleTutorial] Bubble SayDialog is unavailable.");
            return null;
        }

        Canvas canvas = dialog.GetComponentInParent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = BubbleCanvasSortOrder;

        SayDialog.ActiveSayDialog = dialog;
        DialogInput input = dialog.GetComponent<DialogInput>();

        return new BattleTutorialGuide(knight, dialog, input);
    }

    public IEnumerator OnPlayerTurnStart()
    {
        playerTurnCount++;

        switch (playerTurnCount)
        {
            case 1:
                yield return Speak("Your Speed determines when you can act.");
                yield return Speak("Start with a basic attack.");
                break;
            case 2:
                yield return Speak("Well done.");
                yield return Speak("Skills deal more damage, but consume MP.");
                break;
            case 3:
                yield return Speak("Don't ignore your HP.");
                yield return Speak("Use Heal when necessary.");
                break;
        }
    }

    public IEnumerator OnVictory()
    {
        yield return Speak("Excellent.");
        yield return Speak("Remember, every battle is different.");
        yield return Speak("Choose your actions carefully.");
        HideBubble();
    }

    public void HideBubble()
    {
        if (bubbleDialog != null)
            bubbleDialog.SetActive(false);
    }

    IEnumerator Speak(string line)
    {
        if (bubbleDialog == null || knightCharacter == null)
            yield break;

        bool finished = false;
        bubbleDialog.SetActive(true);
        SayDialog.ActiveSayDialog = bubbleDialog;
        bubbleDialog.SetCharacter(knightCharacter);
        bubbleDialog.SetCharacterImage(null);

        bubbleDialog.Say(
            line,
            clearPrevious: true,
            waitForInput: true,
            fadeWhenDone: false,
            stopVoiceover: true,
            waitForVO: false,
            voiceOverClip: null,
            onComplete: () => finished = true);

        while (!finished)
        {
            PollAdvanceInput();
            yield return null;
        }
    }

    void PollAdvanceInput()
    {
        if (dialogInput == null)
            return;

        bool advance = false;

        if (Keyboard.current != null)
        {
            advance |= Keyboard.current.spaceKey.wasPressedThisFrame;
            advance |= Keyboard.current.enterKey.wasPressedThisFrame;
        }

        if (UnityEngine.InputSystem.Mouse.current != null)
            advance |= UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;

        if (advance)
            dialogInput.SetNextLineFlag();
    }
}
