using UnityEngine;

/// <summary>
/// A single sub-objective defined as a drag-and-drop asset. Referenced by Fungus
/// commands (Set/Complete) and by mover gates instead of typing string ids.
/// </summary>
[CreateAssetMenu(fileName = "NewSubObjective", menuName = "Echoes of Aether/Sub-Objective")]
public class SubObjectiveData : ScriptableObject
{
    [Tooltip("Unique id used internally for completion tracking. Leave empty to use the asset name.")]
    [SerializeField] private string id = "";

    [Tooltip("Text shown on the objective HUD.")]
    [TextArea(1, 3)]
    [SerializeField] private string text = "";

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string Text => text;
}
