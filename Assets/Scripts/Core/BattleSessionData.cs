public static class BattleSessionData
{
    public static CharacterData PlayerCharacter {get; private set;}
    public static EnemyData[] Enemies {get; private set;}
    public static string ReturnSceneName {get; private set;}

    public static bool HasSession => PlayerCharacter != null && Enemies != null && Enemies.Length > 0;

    public static void SetSession(CharacterData player, EnemyData[] enemies, string returnScene)
    {
        PlayerCharacter = player;
        Enemies = enemies;
        ReturnSceneName = returnScene;
    }

    public static void Clear()
    {
        PlayerCharacter = null;
        Enemies = null;
        ReturnSceneName = null;
    }
}