using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
public sealed class SkillCinematicPlayer : MonoBehaviour
{
    private const string EnabledPreferenceKey = "AIFG.SkillCinematicsEnabled";
    public static SkillCinematicPlayer Instance { get; private set; }
    public static bool SkillVideosEnabled
    {
        get => PlayerPrefs.GetInt(EnabledPreferenceKey, 1) != 0;
        set
        {
            PlayerPrefs.SetInt(EnabledPreferenceKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    [SerializeField] private RawImage videoImage;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private Material wipeMaterial;
    [Min(0.05f)] [SerializeField] private float wipeInDuration = 0.28f;
    [Min(0.05f)] [SerializeField] private float wipeOutDuration = 0.28f;
    [Min(0.5f)] [SerializeField] private float prepareTimeout = 5f;
    [SerializeField] private VideoClip yuukaClip;
    [SerializeField] private VideoClip ayaneClip;
    [SerializeField] private VideoClip mikaClip;
    [SerializeField] private VideoClip momoiClip;
    [SerializeField] private VideoClip hinaClip;

    private Material runtimeWipeMaterial;
    private bool playbackFinished, playbackFailed, sequenceRunning;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        Instance = this;
        if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
        if (videoImage == null) videoImage = GetComponentInChildren<RawImage>(true);
        if (wipeMaterial != null)
        {
            runtimeWipeMaterial = new Material(wipeMaterial)
            {
                name = "Runtime Skill Video Wipe",
                hideFlags = HideFlags.DontSave
            };
            if (videoImage != null) videoImage.material = runtimeWipeMaterial;
        }
        gameObject.SetActive(false);
    }

    public IEnumerator Play(AutoCombatAI.CombatRole role)
    {
        if (!SkillVideosEnabled) yield break;
        VideoClip clip = ClipFor(role);
        if (clip == null || videoPlayer == null || videoImage == null || Application.isBatchMode) yield break;
        while (sequenceRunning) yield return null;

        // Manual casts begin while the targeting slow-motion is still active. Wait until
        // BattleDirector has exited targeting, then use its actual 1x/2x battle speed.
        yield return null;
        sequenceRunning = true;
        previousTimeScale = ResolvePlaybackSpeed(BattleDirector.Instance, Time.timeScale);
        float cinematicSpeed = Mathf.Max(0.1f, previousTimeScale);
        Time.timeScale = 0f;
        gameObject.SetActive(true);
        playbackFinished = playbackFailed = false;
        SetWipe(0f, false);

        videoPlayer.Stop();
        videoPlayer.clip = clip;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.APIOnly;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.playbackSpeed = cinematicSpeed;
        videoPlayer.loopPointReached += OnPlaybackFinished;
        videoPlayer.errorReceived += OnPlaybackError;
        videoPlayer.Prepare();

        float prepareStarted = Time.realtimeSinceStartup;
        while (!videoPlayer.isPrepared && !playbackFailed &&
            Time.realtimeSinceStartup - prepareStarted < prepareTimeout)
            yield return null;

        if (!videoPlayer.isPrepared || playbackFailed)
        {
            FinishSequence();
            yield break;
        }

        videoImage.texture = videoPlayer.texture;
        videoPlayer.Play();
        float effectiveWipeIn = wipeInDuration / cinematicSpeed;
        float effectiveWipeOut = wipeOutDuration / cinematicSpeed;
        float elapsed = 0f;
        while (elapsed < effectiveWipeIn)
        {
            videoImage.texture = videoPlayer.texture;
            elapsed += Time.unscaledDeltaTime;
            SetWipe(Mathf.Clamp01(elapsed / effectiveWipeIn), false);
            yield return null;
        }
        SetWipe(1f, false);

        while (!playbackFinished && !playbackFailed)
        {
            videoImage.texture = videoPlayer.texture;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < effectiveWipeOut)
        {
            elapsed += Time.unscaledDeltaTime;
            SetWipe(Mathf.Clamp01(elapsed / effectiveWipeOut), true);
            yield return null;
        }
        FinishSequence();
    }

    public static float ResolvePlaybackSpeed(BattleDirector director, float fallbackTimeScale) =>
        director != null ? director.BattleSpeed : Mathf.Max(0.1f, fallbackTimeScale);

    private VideoClip ClipFor(AutoCombatAI.CombatRole role)
    {
        switch (role)
        {
            case AutoCombatAI.CombatRole.YuukaTank: return yuukaClip;
            case AutoCombatAI.CombatRole.AyaneHealer: return ayaneClip;
            case AutoCombatAI.CombatRole.MikaSingleTarget: return mikaClip;
            case AutoCombatAI.CombatRole.MomoiLowCostAOE: return momoiClip;
            case AutoCombatAI.CombatRole.HinaHighCostAOE: return hinaClip;
            default: return null;
        }
    }

    private void SetWipe(float progress, bool exiting)
    {
        if (runtimeWipeMaterial == null) return;
        runtimeWipeMaterial.SetFloat("_Progress", progress);
        runtimeWipeMaterial.SetFloat("_Exiting", exiting ? 1f : 0f);
    }

    private void OnPlaybackFinished(VideoPlayer _) => playbackFinished = true;

    private void OnPlaybackError(VideoPlayer _, string message)
    {
        playbackFailed = true;
        Debug.LogWarning("Skill cinematic could not play: " + message, this);
    }

    private void FinishSequence()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnPlaybackFinished;
            videoPlayer.errorReceived -= OnPlaybackError;
            videoPlayer.Stop();
            videoPlayer.clip = null;
        }
        if (videoImage != null) videoImage.texture = null;
        gameObject.SetActive(false);
        Time.timeScale = previousTimeScale;
        sequenceRunning = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (runtimeWipeMaterial != null) Destroy(runtimeWipeMaterial);
        if (sequenceRunning) Time.timeScale = previousTimeScale;
    }
}
