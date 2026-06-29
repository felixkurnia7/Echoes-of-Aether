using System.Collections.Generic;
using Fungus;
using UnityEngine;

/// <summary>
/// Fungus conditional: runs the following indented block when an Objective or
/// Sub-Objective asset matches the chosen completion state. Works like the built-in
/// "If" command, so pair it with "Else" / "Else If" / "End" from the Flow category.
/// </summary>
[CommandInfo("Narrative",
             "If Objective",
             "Conditional based on objective progress. Runs the block if an Objective or Sub-Objective asset is (or isn't) completed. Pair with Else / End from the Flow category.")]
[AddComponentMenu("")]
public class IfObjectiveCommand : Condition
{
    public enum Target
    {
        SubObjective,
        Objective
    }

    public enum Check
    {
        Completed,
        Active
    }

    public enum Match
    {
        All,
        Any
    }

    [Tooltip("Check sub-objective(s) or the main objective.")]
    [SerializeField] private Target target = Target.SubObjective;

    [Tooltip("Sub-objective asset(s) to check (when Target = Sub Objective). With more than one, use Match to require All or Any.")]
    [SerializeField] private List<SubObjectiveData> subObjectives = new();

    [Tooltip("With multiple sub-objectives: All = every one must match, Any = at least one matches.")]
    [SerializeField] private Match match = Match.All;

    [Tooltip("Objective asset to check (when Target = Objective).")]
    [SerializeField] private ObjectiveData objective;

    [Tooltip("Completed = has been finished. Active = currently shown but not yet finished (Objective only).")]
    [SerializeField] private Check check = Check.Completed;

    [Tooltip("Run the block when the state matches (true) or does NOT match (false), enabling 'if not yet done' branches.")]
    [SerializeField] private bool expected = true;

    protected override bool EvaluateCondition()
    {
        ObjectiveManager manager = ObjectiveManager.Instance;
        bool state = manager != null && CurrentState(manager);
        return state == expected;
    }

    bool CurrentState(ObjectiveManager manager)
    {
        if (target == Target.SubObjective)
            return SubObjectivesState(manager);

        return check == Check.Active
            ? manager.IsObjectiveActive(objective)
            : manager.IsObjectiveCompleted(objective);
    }

    bool SubObjectivesState(ObjectiveManager manager)
    {
        bool anyChecked = false;
        bool anyCompleted = false;
        bool allCompleted = true;

        foreach (SubObjectiveData sub in subObjectives)
        {
            if (sub == null)
                continue;

            anyChecked = true;
            if (manager.IsSubObjectiveCompleted(sub))
                anyCompleted = true;
            else
                allCompleted = false;
        }

        if (!anyChecked)
            return false;

        return match == Match.Any ? anyCompleted : allCompleted;
    }

    protected override bool HasNeededProperties()
    {
        if (target == Target.Objective)
            return objective != null;

        foreach (SubObjectiveData sub in subObjectives)
            if (sub != null)
                return true;

        return false;
    }

    public override string GetSummary()
    {
        string subject;
        string stateWord;

        if (target == Target.SubObjective)
        {
            subject = SubObjectivesSummary();
            if (subject == null)
                return "Error: no sub-objective";
            stateWord = "completed";
        }
        else
        {
            if (objective == null)
                return "Error: no objective";
            subject = objective.name;
            stateWord = check == Check.Active ? "active" : "completed";
        }

        return expected
            ? $"if {subject} {stateWord}"
            : $"if {subject} NOT {stateWord}";
    }

    string SubObjectivesSummary()
    {
        int count = 0;
        SubObjectiveData first = null;
        foreach (SubObjectiveData sub in subObjectives)
        {
            if (sub == null)
                continue;
            if (first == null)
                first = sub;
            count++;
        }

        if (count == 0)
            return null;
        if (count == 1)
            return first.name;

        return $"{match} of {count} subs";
    }
}
