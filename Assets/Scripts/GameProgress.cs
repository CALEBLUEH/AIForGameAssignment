using System.Collections.Generic;
using UnityEngine;

public static class GameProgress
{
    [System.Flags]
    public enum LevelStar
    {
        None = 0,
        Completed = 1 << 0,
        NoCharacterDefeated = 1 << 1,
        UnderTwoMinutes = 1 << 2
    }

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

    public static bool IsLevelUnlocked(int level) => level <= UnlockedLevel;
    public static int GetStars(int level) => CountStars(GetStarFlags(level));

    public static LevelStar GetStarFlags(int level)
    {
        level = Mathf.Clamp(level, 1, 3);
        string flagsKey = StarFlagsKey(level);
        if (PlayerPrefs.HasKey(flagsKey))
            return (LevelStar)(PlayerPrefs.GetInt(flagsKey, 0) & (int)AllStars);

        // Previous builds saved only a one-to-three count based on casualties. That
        // value cannot prove the new optional conditions, but it does prove a victory.
        return PlayerPrefs.GetInt(LegacyStarsKey(level), 0) > 0 ? LevelStar.Completed : LevelStar.None;
    }

    public static bool HasStar(int level, LevelStar star) => (GetStarFlags(level) & star) == star;

    public static LevelStar StarsForVictory(int playerDeaths, float completionSeconds)
    {
        LevelStar earned = LevelStar.Completed;
        if (playerDeaths <= 0) earned |= LevelStar.NoCharacterDefeated;
        if (completionSeconds <= 120f) earned |= LevelStar.UnderTwoMinutes;
        return earned;
    }

    public static int CountStars(LevelStar stars)
    {
        int value = (int)stars & (int)AllStars;
        int count = 0;
        while (value != 0) { count += value & 1; value >>= 1; }
        return count;
    }

    public static LevelStar CompleteLevel(int level, int playerDeaths, float completionSeconds)
    {
        level = Mathf.Clamp(level, 1, 3);
        LevelStar earned = StarsForVictory(playerDeaths, completionSeconds);
        LevelStar combined = GetStarFlags(level) | earned;
        PlayerPrefs.SetInt(StarFlagsKey(level), (int)combined);
        PlayerPrefs.DeleteKey(LegacyStarsKey(level));
        if (level < 3 && UnlockedLevel < level + 1) PlayerPrefs.SetInt(UnlockedLevelKey, level + 1);
        PlayerPrefs.Save();
        return earned;
    }

    public static void ResetAllStars()
    {
        for (int level = 1; level <= 3; level++)
        {
            PlayerPrefs.DeleteKey(StarFlagsKey(level));
            PlayerPrefs.DeleteKey(LegacyStarsKey(level));
        }
        PlayerPrefs.Save();
    }

    private const LevelStar AllStars = LevelStar.Completed | LevelStar.NoCharacterDefeated | LevelStar.UnderTwoMinutes;
    private static string StarFlagsKey(int level) => "AIFG.LevelStarFlags." + level;
    private static string LegacyStarsKey(int level) => "AIFG.LevelStars." + level;

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
