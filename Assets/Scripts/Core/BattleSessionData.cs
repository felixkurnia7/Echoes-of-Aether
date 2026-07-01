public static class BattleSessionData
{
    public static CharacterData PlayerCharacter {get; private set;}
    public static EnemyData[] Enemies {get; private set;}
    public static string ReturnSceneName {get; private set;}
    public static string VictoryFlag {get; private set;}
    public static SubObjectiveData VictorySubObjective {get; private set;}
    public static bool BattleTutorial {get; private set;}

    public static bool HasSession => PlayerCharacter != null && Enemies != null && Enemies.Length > 0;

    public static void SetSession(
        CharacterData player,
        EnemyData[] enemies,
        string returnScene,
        string victoryFlag = null,
        SubObjectiveData victorySubObjective = null,
        bool battleTutorial = false)
    {
        PlayerCharacter = player;
        Enemies = enemies;
        ReturnSceneName = returnScene;
        VictoryFlag = victoryFlag;
        VictorySubObjective = victorySubObjective;
        BattleTutorial = battleTutorial;
    }

    public static void Clear()
    {
        PlayerCharacter = null;
        Enemies = null;
        ReturnSceneName = null;
        VictoryFlag = null;
        VictorySubObjective = null;
        BattleTutorial = false;
    }
}
