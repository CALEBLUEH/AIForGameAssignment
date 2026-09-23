using System.Collections.Generic;
using UnityEngine;

public static class GameProgress
{
    private const string UnlockedLevelKey = "AIFG.UnlockedLevel";
    private const string SelectedLevelKey = "AIFG.SelectedLevel";
    private const string SelectedCharactersKey = "AIFG.SelectedCharacters";
    private const string PendingBattleKey = "AIFG.PendingBattleSelection";
    private static readonly string[] DefaultSquad = { "Yuuka", "Ayane", "Mika", "Momoi" };
    public const string PreparationSceneName = "SandBox_Preparation";

    public static int UnlockedLevel => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedLevelKey, 1), 1, 3);
    public static int SelectedLevel
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(SelectedLevelKey, 1), 1, 3);
        set => PlayerPrefs.SetInt(SelectedLevelKey, Mathf.Clamp(value, 1, 3));
    }

    public static string SelectedBattleScene => BattleSceneForLevel(SelectedLevel);
    public static string BattleSceneForLevel(int level) => level == 2 ? "Gameplay_Level2" :
        level == 3 ? "Gameplay_Level3" : "Gameplay_Level1";
    public static int LevelForBattleScene(string sceneName, int fallback = 1)
    {
        if (sceneName == "Gameplay_Level1") return 1;
        if (sceneName == "Gameplay_Level2") return 2;
        if (sceneName == "Gameplay_Level3") return 3;
        return Mathf.Clamp(fallback, 1, 3);
    }

    public static void PrepareLevelSelection(int level)
    {
        SelectedLevel = level;
        PlayerPrefs.SetInt(PendingBattleKey, 1);
        PlayerPrefs.Save();
    }

    public static void ClearPendingBattleSelection() => PlayerPrefs.DeleteKey(PendingBattleKey);

    public static string ConsumeBattleScene(string fallbackScene)
    {
        if (PlayerPrefs.GetInt(PendingBattleKey, 0) == 0 && !string.IsNullOrWhiteSpace(fallbackScene))
            return fallbackScene;
        PlayerPrefs.DeleteKey(PendingBattleKey);
        PlayerPrefs.Save();
        return SelectedBattleScene;
    }

    public static int StarsForPlayerDeaths(int playerDeaths) => playerDeaths <= 0 ? 3 : playerDeaths == 1 ? 2 : 1;
    public static bool IsLevelUnlocked(int level) => level <= UnlockedLevel;
    public static int GetStars(int level) => Mathf.Clamp(PlayerPrefs.GetInt("AIFG.LevelStars." + level, 0), 0, 3);

    public static void CompleteLevel(int level, int stars)
    {
        level = Mathf.Clamp(level, 1, 3);
        stars = Mathf.Clamp(stars, 1, 3);
        string key = "AIFG.LevelStars." + level;
        if (stars > PlayerPrefs.GetInt(key, 0)) PlayerPrefs.SetInt(key, stars);
        if (level < 3 && UnlockedLevel < level + 1) PlayerPrefs.SetInt(UnlockedLevelKey, level + 1);
        PlayerPrefs.Save();
    }

    public static string[] GetSelectedCharacters()
    {
        string saved = PlayerPrefs.GetString(SelectedCharactersKey, string.Join(",", DefaultSquad));
        string[] values = saved.Split(',');
        return values.Length == 0 ? DefaultSquad : values;
    }

    public static bool IsCharacterSelected(string characterName)
    {
        foreach (string value in GetSelectedCharacters())
            if (value == characterName) return true;
        return false;
    }

    public static void SetSelectedCharacters(IList<string> names)
    {
        var unique = new List<string>();
        foreach (string name in names)
            if (!string.IsNullOrWhiteSpace(name) && !unique.Contains(name) && unique.Count < 4) unique.Add(name);
        if (unique.Count == 0) unique.AddRange(DefaultSquad);
        PlayerPrefs.SetString(SelectedCharactersKey, string.Join(",", unique));
        PlayerPrefs.Save();
    }
}
