using System;
using System.Collections.Generic;
using UnityEngine;

public enum ObjectiveStatus
{
    Hidden,
    Active,
    Completed
}

public class SubObjective
{
    public string Id;
    public string Text;
    public bool Completed;
}

/// <summary>
/// Holds the current on-screen objective and notifies the HUD.
/// Auto-creates itself (and the HUD) before the first scene loads, so no
/// manual scene wiring is required. Persists across scene loads.
/// </summary>
public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    public string CurrentObjective { get; private set; }
    public ObjectiveStatus Status { get; private set; } = ObjectiveStatus.Hidden;

    readonly List<SubObjective> subObjectives = new();
    readonly HashSet<string> completedSubIds = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<SubObjective> SubObjectives => subObjectives;

    // Tracks which objective assets have been completed (kept across scene loads so
    // conditional commands can branch on past progress). The id of the objective set
    // from an asset, used to record completion.
    readonly HashSet<string> completedObjectiveIds = new(StringComparer.OrdinalIgnoreCase);
    string activeObjectiveId;

    public event Action<string> OnObjectiveSet;
    public event Action<string> OnObjectiveCompleted;
    public event Action OnObjectiveHidden;
    public event Action OnSubObjectivesChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject("[ObjectiveManager]");
        go.AddComponent<ObjectiveManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (GetComponent<ObjectiveHUD>() == null)
            gameObject.AddComponent<ObjectiveHUD>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetObjective(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        CurrentObjective = text;
        activeObjectiveId = null; // plain-text objective has no asset id to track
        Status = ObjectiveStatus.Active;
        OnObjectiveSet?.Invoke(text);
    }

    /// <summary>
    /// Sets the objective from a ScriptableObject asset. When
    /// <paramref name="includeSubObjectives"/> is true, replaces the sub-objective
    /// list with the ones listed on the asset.
    /// </summary>
    public void SetObjective(ObjectiveData data, bool includeSubObjectives = true)
    {
        if (data == null)
            return;

        SetObjective(data.Title);
        activeObjectiveId = Normalize(data.Id);

        if (!includeSubObjectives)
            return;

        ClearSubObjectives();
        foreach (SubObjectiveData sub in data.SubObjectives)
            AddSubObjective(sub);
    }

    public void CompleteObjective()
    {
        if (Status != ObjectiveStatus.Active)
            return;

        Status = ObjectiveStatus.Completed;
        if (!string.IsNullOrEmpty(activeObjectiveId))
            completedObjectiveIds.Add(activeObjectiveId);
        OnObjectiveCompleted?.Invoke(CurrentObjective);
    }

    public void HideObjective()
    {
        Status = ObjectiveStatus.Hidden;
        CurrentObjective = null;
        activeObjectiveId = null;
        subObjectives.Clear();
        completedSubIds.Clear();
        OnObjectiveHidden?.Invoke();
    }

    public void AddSubObjective(string id, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        string key = Normalize(string.IsNullOrEmpty(id) ? text : id);
        SubObjective existing = Find(key);
        if (existing != null)
        {
            existing.Text = text;
        }
        else
        {
            subObjectives.Add(new SubObjective
            {
                Id = key,
                Text = text,
                Completed = completedSubIds.Contains(key)
            });
        }

        OnSubObjectivesChanged?.Invoke();
    }

    /// <summary>Adds (or updates) a sub-objective from a ScriptableObject asset.</summary>
    public void AddSubObjective(SubObjectiveData sub)
    {
        if (sub != null)
            AddSubObjective(sub.Id, sub.Text);
    }

    public void CompleteSubObjective(string id)
    {
        string key = Normalize(id);
        if (string.IsNullOrEmpty(key))
            return;

        completedSubIds.Add(key);

        SubObjective existing = Find(key);
        if (existing != null)
        {
            if (!existing.Completed)
            {
                existing.Completed = true;
                OnSubObjectivesChanged?.Invoke();
            }
        }
        else
        {
            Debug.LogWarning(
                $"[ObjectiveManager] CompleteSubObjective('{id}') found no sub-objective on the HUD. " +
                "Did you AddSubObjective with the same id first? The completion flag is still set, " +
                "so an NPCWalker gate waiting on this id will continue.");
        }
    }

    /// <summary>Marks a sub-objective complete from a ScriptableObject asset.</summary>
    public void CompleteSubObjective(SubObjectiveData sub)
    {
        if (sub != null)
            CompleteSubObjective(sub.Id);
    }

    public bool IsSubObjectiveCompleted(string id)
    {
        return completedSubIds.Contains(Normalize(id));
    }

    public bool IsSubObjectiveCompleted(SubObjectiveData sub)
    {
        return sub != null && IsSubObjectiveCompleted(sub.Id);
    }

    /// <summary>True if the given objective asset has been completed (recorded when
    /// <see cref="CompleteObjective"/> runs while that asset is the active objective).</summary>
    public bool IsObjectiveCompleted(ObjectiveData data)
    {
        return data != null && completedObjectiveIds.Contains(Normalize(data.Id));
    }

    /// <summary>True if the given objective asset is the one currently shown and active.</summary>
    public bool IsObjectiveActive(ObjectiveData data)
    {
        return data != null
            && Status == ObjectiveStatus.Active
            && string.Equals(activeObjectiveId, Normalize(data.Id), StringComparison.OrdinalIgnoreCase);
    }

    public void ClearSubObjectives()
    {
        bool hadItems = subObjectives.Count > 0;

        subObjectives.Clear();
        completedSubIds.Clear();

        if (hadItems)
            OnSubObjectivesChanged?.Invoke();
    }

    SubObjective Find(string normalizedKey)
    {
        return subObjectives.Find(s => string.Equals(s.Id, normalizedKey, StringComparison.OrdinalIgnoreCase));
    }

    static string Normalize(string value)
    {
        return (value ?? string.Empty).Trim();
    }
}
