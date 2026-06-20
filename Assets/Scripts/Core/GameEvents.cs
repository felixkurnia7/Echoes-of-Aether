using System;

public static class GameEvents
{
    public static event Action OnBattleStarted;
    public static event Action OnBattleEnded;
    public static event Action OnDialogueStarted;
    public static event Action OnDialogueFinished;
    public static event Action OnQuestAccepted;
    public static event Action OnQuestCompleted;

    public static void RaiseBattleStart() => OnBattleStarted?.Invoke();
    public static void RaiseBattleEnd() => OnBattleEnded?.Invoke();
    public static void RaiseDialogueStarted() => OnDialogueStarted?.Invoke();
    public static void RaiseDialogueFinished() => OnDialogueFinished?.Invoke();
    public static void RaiseQuestAccepted() => OnQuestAccepted?.Invoke();
    public static void RaiseQuestCompleted() => OnQuestCompleted?.Invoke();
}