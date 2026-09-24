using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SceneMusicDirector : MonoBehaviour
{
    private enum MusicCue { None, MainMenu, Preparation, CharacterStates, Credits, Level1, Level2, Level3, Victory }

    private static SceneMusicDirector instance;
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioSource skillVoiceSource;
    private Coroutine delayedVictory;
    private MusicCue currentCue;
    private AutoCombatAI.CombatRole currentSkillVoiceRole = AutoCombatAI.CombatRole.Enemy;
    private readonly Dictionary<AutoCombatAI.CombatRole, AudioClip[]> skillVoiceClips =
        new Dictionary<AutoCombatAI.CombatRole, AudioClip[]>();

    public static string CurrentCueName => instance == null ? MusicCue.None.ToString() : instance.currentCue.ToString();
    public static string CurrentClipName => instance == null || instance.musicSource == null || instance.musicSource.clip == null
        ? string.Empty
        : instance.musicSource.clip.name;
    public static bool IsMusicPlaying => instance != null && instance.musicSource != null && instance.musicSource.isPlaying;
    public static string CurrentSkillVoiceRoleName => instance == null
        ? AutoCombatAI.CombatRole.Enemy.ToString()
        : instance.currentSkillVoiceRole.ToString();
    public static string CurrentSkillVoiceClipName => instance == null || instance.skillVoiceSource == null ||
        instance.skillVoiceSource.clip == null ? string.Empty : instance.skillVoiceSource.clip.name;
    public static float CurrentSkillVoicePitch => instance == null || instance.skillVoiceSource == null
        ? 1f
        : instance.skillVoiceSource.pitch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject service = new GameObject("Scene Music Director");
        instance = service.AddComponent<SceneMusicDirector>();
        DontDestroyOnLoad(service);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        musicSource = CreateSource("BGM Source", true);
        sfxSource = CreateSource("SFX Source", false);
        skillVoiceSource = CreateSource("Skill Voice Source", false);
        skillVoiceSource.pitch = 1f;
        AudioSettingsUI.ApplySavedVolume();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void Start() => PlayForScene(SceneManager.GetActiveScene().name);

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PlayForScene(scene.name);

    public static void PlayVictoryMusic(float silenceDuration = 0.65f)
    {
        Bootstrap();
        if (instance.delayedVictory != null) instance.StopCoroutine(instance.delayedVictory);
        instance.musicSource.Stop();
        instance.currentCue = MusicCue.None;
        instance.delayedVictory = instance.StartCoroutine(instance.PlayVictoryAfterSilence(silenceDuration));
    }

    public static void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        Bootstrap();
        instance.sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public static bool PlaySkillVoice(AutoCombatAI.CombatRole role)
    {
        Bootstrap();
        AudioClip[] clips = instance.GetSkillVoiceClips(role);
        if (clips.Length == 0) return false;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        // Voices intentionally share one channel: the newest skill replaces any
        // unfinished line instead of overlapping it.
        instance.skillVoiceSource.Stop();
        instance.skillVoiceSource.clip = clip;
        instance.skillVoiceSource.pitch = 1f;
        instance.currentSkillVoiceRole = role;
        instance.skillVoiceSource.Play();
        return true;
    }

    private IEnumerator PlayVictoryAfterSilence(float duration)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
        PlayCue(MusicCue.Victory);
        delayedVictory = null;
    }

    private void PlayForScene(string sceneName)
    {
        if (delayedVictory != null)
        {
            StopCoroutine(delayedVictory);
            delayedVictory = null;
        }
        PlayCue(CueForScene(sceneName));
    }

    private void PlayCue(MusicCue cue)
    {
        if (cue == currentCue && musicSource.isPlaying) return;
        currentCue = cue;
        musicSource.Stop();
        musicSource.clip = null;
        if (cue == MusicCue.None) return;

        AudioClip clip = Resources.Load<AudioClip>(ResourcePath(cue));
        if (clip == null)
        {
            Debug.LogWarning("Missing BGM resource for " + cue + ".");
            return;
        }
        musicSource.clip = clip;
        musicSource.loop = cue != MusicCue.Victory;
        musicSource.Play();
    }

    private AudioSource CreateSource(string sourceName, bool loop)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    private AudioClip[] GetSkillVoiceClips(AutoCombatAI.CombatRole role)
    {
        if (skillVoiceClips.TryGetValue(role, out AudioClip[] clips)) return clips;
        string folder = SkillVoiceResourceFolder(role);
        clips = string.IsNullOrEmpty(folder) ? new AudioClip[0] : Resources.LoadAll<AudioClip>(folder);
        skillVoiceClips[role] = clips;
        if (clips.Length == 0 && role != AutoCombatAI.CombatRole.Enemy)
            Debug.LogWarning("Missing skill voice resources for " + role + ".");
        return clips;
    }

    private static string SkillVoiceResourceFolder(AutoCombatAI.CombatRole role)
    {
        switch (role)
        {
            case AutoCombatAI.CombatRole.YuukaTank: return "Audio/SkillVoices/Yuuka";
            case AutoCombatAI.CombatRole.AyaneHealer: return "Audio/SkillVoices/Ayane";
            case AutoCombatAI.CombatRole.MikaSingleTarget: return "Audio/SkillVoices/Mika";
            case AutoCombatAI.CombatRole.MomoiLowCostAOE: return "Audio/SkillVoices/Momoi";
            case AutoCombatAI.CombatRole.HinaHighCostAOE: return "Audio/SkillVoices/Hina";
            default: return string.Empty;
        }
    }

    private static MusicCue CueForScene(string sceneName)
    {
        switch (sceneName)
        {
            case "Lobby":
            case "LevelSelection": return MusicCue.MainMenu;
            case "Preparation":
            case "SandBox_Preparation": return MusicCue.Preparation;
            case "Sandbox_CharacterStates": return MusicCue.CharacterStates;
            case "Credits": return MusicCue.Credits;
            case "Sandbox_Gameplay":
            case "Gameplay_Level1": return MusicCue.Level1;
            case "Gameplay_Level2": return MusicCue.Level2;
            case "Gameplay_Level3": return MusicCue.Level3;
            default: return MusicCue.None;
        }
    }

    private static string ResourcePath(MusicCue cue)
    {
        switch (cue)
        {
            case MusicCue.MainMenu: return "Audio/BGM/MainMenu_LevelSelection";
            case MusicCue.Preparation: return "Audio/BGM/Preparation";
            case MusicCue.CharacterStates: return "Audio/BGM/CharacterStates";
            case MusicCue.Credits: return "Audio/BGM/Credits";
            case MusicCue.Level1: return "Audio/BGM/Level1";
            case MusicCue.Level2: return "Audio/BGM/Level2";
            case MusicCue.Level3: return "Audio/BGM/Level3";
            case MusicCue.Victory: return "Audio/BGM/Victory";
            default: return string.Empty;
        }
    }
}
