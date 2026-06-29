using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An objective defined as a drag-and-drop asset: its on-screen text plus an
/// ordered list of sub-objectives. Referenced by the Fungus "Set Objective"
/// command so objectives no longer need to be typed inside Fungus.
/// </summary>
[CreateAssetMenu(fileName = "NewObjective", menuName = "Echoes of Aether/Objective")]
public class ObjectiveData : ScriptableObject
{
    [Tooltip("Unique id (optional). Leave empty to use the asset name.")]
    [SerializeField] private string id = "";

    [Tooltip("Main objective text shown on the HUD.")]
    [TextArea(1, 3)]
    [SerializeField] private string title = "";

    [Tooltip("Sub-objectives shown beneath the main objective.")]
    [SerializeField] private List<SubObjectiveData> subObjectives = new();

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string Title => title;
    public IReadOnlyList<SubObjectiveData> SubObjectives => subObjectives;
}
