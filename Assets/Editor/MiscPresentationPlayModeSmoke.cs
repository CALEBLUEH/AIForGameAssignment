using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MiscPresentationPlayModeSmoke
{
    private const string InitialScene = "Assets/Scenes/Sandbox_CharacterStates.unity";
    private const string RunningKey = "AIForGame.MiscPresentationSmoke.Running";
    private const string PhaseKey = "AIForGame.MiscPresentationSmoke.Phase";
    private const string DeadlineKey = "AIForGame.MiscPresentationSmoke.Deadline";

    private static readonly (string path, string clip)[] AudioResources =
    {
        ("Audio/BGM/MainMenu_LevelSelection", "MainMenu_LevelSelection"),
        ("Audio/BGM/Preparation", "Preparation"),
        ("Audio/BGM/CharacterStates", "CharacterStates"),
        ("Audio/BGM/Credits", "Credits"),
        ("Audio/BGM/Level1", "Level1"),
        ("Audio/BGM/Level2", "Level2"),
        ("Audio/BGM/Level3", "Level3"),
        ("Audio/BGM/Victory", "Victory")
    };

    static MiscPresentationPlayModeSmoke()
    {
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AI For Game/Run Misc Presentation Play Mode Smoke")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before running the misc presentation smoke.");
        EditorSceneManager.OpenScene(InitialScene, OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.SetString(DeadlineKey, (EditorApplication.timeSinceStartup + 120d).ToString("R"));
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (!EditorApplication.isPlaying)
        {
            if (SessionState.GetInt(PhaseKey, 0) != 99) return;
            Cleanup();
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }

        try
        {
            if (EditorApplication.timeSinceStartup > double.Parse(SessionState.GetString(DeadlineKey, "0")))
                throw new TimeoutException("Misc presentation smoke exceeded 120 seconds.");
            if (SceneTransitionService.IsTransitioning) return;

            switch (SessionState.GetInt(PhaseKey, 0))
            {
                case 0:
                    ValidateCharacterPreview();
                    ValidateCue("CharacterStates", "CharacterStates");
                    LoadNext("Lobby", 1);
                    break;
                case 1:
                    WaitForScene("Lobby");
                    ValidateCue("MainMenu", "MainMenu_LevelSelection");
                    LoadNext("LevelSelection", 2);
                    break;
                case 2:
                    WaitForScene("LevelSelection");
                    ValidateCue("MainMenu", "MainMenu_LevelSelection");
                    // Deliberately use the legacy serialized name; production routing must normalize it.
                    LoadNext("Preparation", 3);
                    break;
                case 3:
                    WaitForScene("SandBox_Preparation");
                    ValidateCue("Preparation", "Preparation");
                    LoadNext("Gameplay_Level1", 4);
                    break;
                case 4:
                    WaitForScene("Gameplay_Level1");
                    ValidateCue("Level1", "Level1");
                    LoadNext("Gameplay_Level2", 5);
                    break;
                case 5:
                    WaitForScene("Gameplay_Level2");
                    ValidateCue("Level2", "Level2");
                    LoadNext("Gameplay_Level3", 6);
                    break;
                case 6:
                    WaitForScene("Gameplay_Level3");
                    ValidateCue("Level3", "Level3");
                    SceneMusicDirector.PlayVictoryMusic(0.1f);
                    SessionState.SetInt(PhaseKey, 7);
                    SessionState.SetString(DeadlineKey, (EditorApplication.timeSinceStartup + 120d).ToString("R"));
                    break;
                case 7:
                    if (SceneMusicDirector.CurrentCueName != "Victory") return;
                    ValidateCue("Victory", "Victory");
                    LoadNext("Credits", 8);
                    break;
                case 8:
                    WaitForScene("Credits");
                    ValidateCue("Credits", "Credits");
                    LoadNext("LevelSelection", 9);
                    break;
                case 9:
                    WaitForScene("LevelSelection");
                    ValidateCue("MainMenu", "MainMenu_LevelSelection");
                    if (SceneTransitionService.FadeAlpha > 0.01f)
                        throw new InvalidOperationException("Transition overlay remained visible after the fade completed.");
                    Debug.Log("MISC_PRESENTATION_PLAYMODE_OK character rotation, cover-only preview, fades, scene normalization, all BGM routes, and delayed victory music passed.");
                    SessionState.SetInt(PhaseKey, 99);
                    EditorApplication.isPlaying = false;
                    break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetInt(PhaseKey, 99);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void ValidateCharacterPreview()
    {
        CharacterStatePreviewController controller = Object.FindFirstObjectByType<CharacterStatePreviewController>();
        if (controller == null) throw new InvalidOperationException("CharacterStatePreviewController was not found.");

        GameObject cover = null;
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate.gameObject.scene == SceneManager.GetActiveScene() && candidate.name == "Cover State Preview")
            {
                cover = candidate.gameObject;
                break;
            }
        }
        if (cover == null) throw new InvalidOperationException("Cover State Preview was not found, including inactive objects.");

        controller.SelectState((int)CharacterStatePreviewController.PreviewState.Idle);
        if (cover.activeSelf) throw new InvalidOperationException("Cover preview was visible during Idle.");
        controller.SelectState((int)CharacterStatePreviewController.PreviewState.Cover);
        if (!cover.activeSelf) throw new InvalidOperationException("Cover preview was not visible during Cover.");
        controller.SelectState((int)CharacterStatePreviewController.PreviewState.Attack);
        if (cover.activeSelf) throw new InvalidOperationException("Cover preview remained visible after leaving Cover.");

        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty characters = serializedController.FindProperty("characters");
        GameObject selected = characters.GetArrayElementAtIndex(0).FindPropertyRelative("root").objectReferenceValue as GameObject;
        if (selected == null) throw new InvalidOperationException("The first preview character root was not assigned.");
        Transform root = selected.transform;
        Vector3 position = root.position;
        Vector3 scale = root.localScale;
        Quaternion rotation = root.rotation;
        Quaternion coverRotation = cover.transform.rotation;
        controller.RotateSelected(20f);
        if (Quaternion.Angle(rotation, root.rotation) < 10f)
            throw new InvalidOperationException("Preview rotation did not rotate the selected character.");
        if (Vector3.Distance(position, root.position) > 0.0001f || Vector3.Distance(scale, root.localScale) > 0.0001f)
            throw new InvalidOperationException("Preview rotation changed the character position or scale.");
        if (Quaternion.Angle(coverRotation, cover.transform.rotation) < 10f)
            throw new InvalidOperationException("Preview rotation did not rotate the cover obstacle with the character.");

        foreach ((string path, string clip) in AudioResources)
        {
            AudioClip loaded = Resources.Load<AudioClip>(path);
            if (loaded == null || loaded.name != clip)
                throw new InvalidOperationException("BGM resource did not load correctly: " + path + ".");
        }

        ValidateSkillVoiceResources("Yuuka", "YuukaSound");
        ValidateSkillVoiceResources("Ayane", "AyaneSound");
        ValidateSkillVoiceResources("Mika", "MikaSound");
        ValidateSkillVoiceResources("Momoi", "MomoiSound");
        ValidateSkillVoiceResources("Hina", "HinaSound");

        Time.timeScale = 2f;
        if (!SceneMusicDirector.PlaySkillVoice(AutoCombatAI.CombatRole.YuukaTank) ||
            SceneMusicDirector.CurrentSkillVoiceRoleName != "YuukaTank")
            throw new InvalidOperationException("Yuuka skill voice did not start.");
        if (!SceneMusicDirector.PlaySkillVoice(AutoCombatAI.CombatRole.HinaHighCostAOE) ||
            SceneMusicDirector.CurrentSkillVoiceRoleName != "HinaHighCostAOE" ||
            !SceneMusicDirector.CurrentSkillVoiceClipName.StartsWith("HinaSound", StringComparison.Ordinal) ||
            Mathf.Abs(SceneMusicDirector.CurrentSkillVoicePitch - 1f) > 0.001f)
            throw new InvalidOperationException("Newest-voice replacement or time-scale-independent pitch failed.");
        Time.timeScale = 1f;
    }

    private static void ValidateSkillVoiceResources(string folder, string prefix)
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/SkillVoices/" + folder);
        if (clips.Length != 3)
            throw new InvalidOperationException(folder + " expected three skill voices but loaded " + clips.Length + ".");
        foreach (AudioClip clip in clips)
            if (!clip.name.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException(folder + " loaded an unexpected skill voice: " + clip.name + ".");
    }

    private static void ValidateCue(string cue, string clip)
    {
        if (SceneMusicDirector.CurrentCueName != cue || SceneMusicDirector.CurrentClipName != clip)
            throw new InvalidOperationException("Expected BGM " + cue + "/" + clip + " but got " +
                SceneMusicDirector.CurrentCueName + "/" + SceneMusicDirector.CurrentClipName + ".");
    }

    private static void LoadNext(string sceneName, int nextPhase)
    {
        SessionState.SetInt(PhaseKey, nextPhase);
        SceneTransitionService.LoadScene(sceneName);
        if (!SceneTransitionService.IsTransitioning)
            throw new InvalidOperationException("Scene transition did not begin for " + sceneName + ".");
    }

    private static void WaitForScene(string expected)
    {
        if (SceneManager.GetActiveScene().name != expected)
            throw new InvalidOperationException("Expected scene " + expected + " but loaded " + SceneManager.GetActiveScene().name + ".");
    }

    private static void Cleanup()
    {
        SessionState.EraseBool(RunningKey);
        SessionState.EraseInt(PhaseKey);
        SessionState.EraseString(DeadlineKey);
    }
}
