using Fungus;
using UnityEngine;

[CommandInfo("Narrative", "Show Tutorial Popup", "Displays a modal tutorial panel with text and an OK button to close it.")]
[AddComponentMenu("")]
public class ShowTutorialPopupCommand : Command
{
    [Tooltip("Heading shown at the top of the popup. Leave empty to hide.")]
    [SerializeField] private string title = "TUTORIAL";

    [Tooltip("Main tutorial text.")]
    [TextArea(2, 5)]
    [SerializeField] private string message = "";

    [Tooltip("Label on the close button.")]
    [SerializeField] private string okButtonLabel = "OK";

    [Tooltip("If enabled, the flowchart waits until the player clicks OK before continuing.")]
    [SerializeField] private bool waitUntilClosed = true;

    public override void OnEnter()
    {
        TutorialPopup popup = TutorialPopup.Instance;
        if (popup == null)
        {
            Debug.LogError("[ShowTutorialPopup] TutorialPopup instance not found.");
            Continue();
            return;
        }

        if (waitUntilClosed)
        {
            popup.Show(title, message, okButtonLabel, Continue);
        }
        else
        {
            popup.Show(title, message, okButtonLabel, null);
            Continue();
        }
    }

    public override string GetSummary()
    {
        if (string.IsNullOrEmpty(message))
            return "<empty>";

        return message;
    }

    public override Color GetButtonColor()
    {
        return new Color32(184, 210, 235, 255);
    }
}
