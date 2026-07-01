using Fungus;
using UnityEngine;
using System;

[CommandInfo("Narrative", "Show Ending Screen", "Shows the full-screen ending credits after a fade or cutscene.")]
[AddComponentMenu("")]
public class ShowEndingScreenCommand : Command
{
    [Tooltip("Use the default ending text for Echoes of Aether.")]
    [SerializeField] private bool useDefaultText = true;

    [Tooltip("Shown when Use Default Text is off.")]
    [SerializeField] private string titleLine = "Echoes of Aether";

    [Tooltip("Shown when Use Default Text is off.")]
    [SerializeField] private string subtitleLine = "The Lost Crystal";

    [Tooltip("Shown when Use Default Text is off.")]
    [TextArea(2, 5)]
    [SerializeField] private string epilogueLine =
        "The Aether Crystal has been restored.\nPeace returns to the outpost...\n\n...for now.";

    [Tooltip("Shown when Use Default Text is off.")]
    [SerializeField] private string footerLine = "Thank You For Playing";

    [Tooltip("Wait for any key press before ending (quits the game by default).")]
    [SerializeField] private bool waitUntilDismissed = true;

    [Tooltip("Quit the application when the player presses a key.")]
    [SerializeField] private bool quitOnDismiss = true;

    public override void OnEnter()
    {
        EndingScreen screen = EndingScreen.Instance;
        if (screen == null)
        {
            Debug.LogError("[ShowEndingScreen] EndingScreen instance not found.");
            Continue();
            return;
        }

        Action onComplete = waitUntilDismissed && !quitOnDismiss ? Continue : null;

        if (useDefaultText)
            screen.ShowDefault(waitUntilDismissed, quitOnDismiss, onComplete);
        else
            screen.Show(titleLine, subtitleLine, epilogueLine, footerLine, waitUntilDismissed, quitOnDismiss, onComplete);

        if (!waitUntilDismissed)
            Continue();
    }

    public override string GetSummary()
    {
        if (useDefaultText)
            return "Echoes of Aether / The Lost Crystal";

        return $"{titleLine} / {subtitleLine}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(210, 180, 120, 255);
    }
}
