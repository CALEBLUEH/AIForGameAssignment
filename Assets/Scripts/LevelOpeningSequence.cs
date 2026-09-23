using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public sealed class LevelOpeningSequence : MonoBehaviour
{
    [Header("Authored under Squad Coordinate")]
    [SerializeField] private Camera openingCamera;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Transform[] squadSpawnPoints = Array.Empty<Transform>();
    [Header("Timing")]
    [Min(0f)] [SerializeField] private float duration = 2f;
    [SerializeField] private float cameraPitchDecrease = 10f;
    [Min(0.1f)] [SerializeField] private float navMeshSearchRadius = 4f;
    [SerializeField] private AnimationCurve cameraMotion = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Header("Battle transition presentation")]
    [SerializeField] private Image initialBlackout;
    [SerializeField] private Image battleDim;
    [SerializeField] private Text battleText;
    [Min(0f)] [SerializeField] private float initialRevealDuration = 0.45f;
    [Range(0f, 1f)] [SerializeField] private float battleDimAlpha = 0.55f;
    [Min(0.05f)] [SerializeField] private float battleEnterDuration = 0.45f;
    [Min(0f)] [SerializeField] private float battleHoldDuration = 0.55f;
    [Min(0.05f)] [SerializeField] private float battleExitDuration = 0.3f;
    [Min(0f)] [SerializeField] private float battleDimRestoreDuration = 0.2f;

    private AudioListener openingListener;
    private AudioListener gameplayListener;
    private TacticalCameraFollow gameplayFollow;
    private Quaternion openingStartRotation;
    private Coroutine runningSequence;

    public Camera OpeningCamera => openingCamera;
    public Camera GameplayCamera => gameplayCamera;
    public IReadOnlyList<Transform> SquadSpawnPoints => squadSpawnPoints;
    public float Duration => duration;
    public bool IsPlaying { get; private set; }
    public bool BattleAnnouncementPlaying { get; private set; }
    public float AppliedPitchDecrease { get; private set; }

    private void Awake()
    {
        CacheCameraComponents();
        if (openingCamera != null) openingStartRotation = openingCamera.transform.localRotation;
        SetOpeningCameraActive(false);
        SetGameplayCameraActive(true);
        SetImageAlpha(initialBlackout, 0f, false);
        SetImageAlpha(battleDim, 0f, false);
        if (battleText != null) battleText.gameObject.SetActive(false);
    }

    public void Play(IReadOnlyList<AutoCombatAI> selectedSquad, Action completed)
    {
        if (runningSequence != null) StopCoroutine(runningSequence);
        runningSequence = StartCoroutine(PlayRoutine(selectedSquad, completed));
    }

    public void Configure(Camera authoredOpeningCamera, Camera authoredGameplayCamera,
        Transform[] authoredSpawnPoints, float authoredDuration, float authoredPitchDecrease)
    {
        openingCamera = authoredOpeningCamera;
        gameplayCamera = authoredGameplayCamera;
        squadSpawnPoints = authoredSpawnPoints ?? Array.Empty<Transform>();
        duration = Mathf.Max(0f, authoredDuration);
        cameraPitchDecrease = authoredPitchDecrease;
        CacheCameraComponents();
    }

    public void ConfigurePresentation(Image authoredInitialBlackout, Image authoredBattleDim,
        Text authoredBattleText, float revealDuration, float dimAlpha,
        float enterDuration, float holdDuration, float exitDuration, float restoreDuration)
    {
        initialBlackout = authoredInitialBlackout;
        battleDim = authoredBattleDim;
        battleText = authoredBattleText;
        initialRevealDuration = Mathf.Max(0f, revealDuration);
        battleDimAlpha = Mathf.Clamp01(dimAlpha);
        battleEnterDuration = Mathf.Max(0.05f, enterDuration);
        battleHoldDuration = Mathf.Max(0f, holdDuration);
        battleExitDuration = Mathf.Max(0.05f, exitDuration);
        battleDimRestoreDuration = Mathf.Max(0f, restoreDuration);
    }

    private IEnumerator PlayRoutine(IReadOnlyList<AutoCombatAI> selectedSquad, Action completed)
    {
        IsPlaying = true;
        PlaceSquad(selectedSquad);
        CacheCameraComponents();
        openingStartRotation = openingCamera != null ? openingCamera.transform.localRotation : Quaternion.identity;
        SetGameplayCameraActive(false);
        SetOpeningCameraActive(true);

        SetImageAlpha(initialBlackout, 1f, true);
        if (initialRevealDuration > 0f)
        {
            for (float reveal = 0f; reveal < initialRevealDuration; reveal += Time.unscaledDeltaTime)
            {
                SetImageAlpha(initialBlackout, 1f - Mathf.Clamp01(reveal / initialRevealDuration), true);
                yield return null;
            }
        }
        SetImageAlpha(initialBlackout, 0f, false);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            AnimateOpeningCamera(duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        AnimateOpeningCamera(1f);
        SetOpeningCameraActive(false);
        SetGameplayCameraActive(true);
        IsPlaying = false;
        completed?.Invoke();
        yield return PlayBattleAnnouncement();
        runningSequence = null;
    }

    private IEnumerator PlayBattleAnnouncement()
    {
        if (battleDim == null || battleText == null) yield break;
        BattleAnnouncementPlaying = true;
        SetImageAlpha(battleDim, battleDimAlpha, true);
        battleText.gameObject.SetActive(true);
        battleText.text = "BATTLE";
        battleText.color = new Color(1f, 0.78f, 0.08f, 1f);
        RectTransform textRect = battleText.rectTransform;
        float travel = Mathf.Max(900f, Screen.width * 0.75f);

        for (float elapsed = 0f; elapsed < battleEnterDuration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / battleEnterDuration);
            float easeOut = 1f - Mathf.Pow(1f - t, 5f);
            textRect.anchoredPosition = new Vector2(Mathf.Lerp(-travel, 0f, easeOut), 0f);
            yield return null;
        }
        textRect.anchoredPosition = Vector2.zero;
        if (battleHoldDuration > 0f) yield return new WaitForSecondsRealtime(battleHoldDuration);

        for (float elapsed = 0f; elapsed < battleExitDuration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed / battleExitDuration);
            float easeIn = Mathf.Pow(t, 5f);
            textRect.anchoredPosition = new Vector2(Mathf.Lerp(0f, travel, easeIn), 0f);
            yield return null;
        }
        textRect.anchoredPosition = new Vector2(travel, 0f);
        battleText.gameObject.SetActive(false);

        if (battleDimRestoreDuration > 0f)
        {
            for (float elapsed = 0f; elapsed < battleDimRestoreDuration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / battleDimRestoreDuration);
                SetImageAlpha(battleDim, Mathf.Lerp(battleDimAlpha, 0f, t), true);
                yield return null;
            }
        }
        SetImageAlpha(battleDim, 0f, false);
        BattleAnnouncementPlaying = false;
    }

    private static void SetImageAlpha(Image image, float alpha, bool active)
    {
        if (image == null) return;
        image.gameObject.SetActive(active);
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private void PlaceSquad(IReadOnlyList<AutoCombatAI> selectedSquad)
    {
        if (selectedSquad == null) return;
        int count = Mathf.Min(selectedSquad.Count, squadSpawnPoints?.Length ?? 0);
        for (int i = 0; i < count; i++)
        {
            AutoCombatAI member = selectedSquad[i];
            Transform marker = squadSpawnPoints[i];
            if (member == null || marker == null) continue;
            if (NavMesh.SamplePosition(marker.position, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
                member.SetNavMeshPosition(hit.position);
            else
                member.transform.position = marker.position;
            member.transform.rotation = marker.rotation;
        }
    }

    private void AnimateOpeningCamera(float normalizedTime)
    {
        if (openingCamera == null) return;
        float eased = cameraMotion == null ? normalizedTime : cameraMotion.Evaluate(normalizedTime);
        AppliedPitchDecrease = Mathf.Abs(cameraPitchDecrease) * eased;
        openingCamera.transform.localRotation = openingStartRotation *
            Quaternion.Euler(-AppliedPitchDecrease, 0f, 0f);
    }

    private void CacheCameraComponents()
    {
        openingListener = openingCamera != null ? openingCamera.GetComponent<AudioListener>() : null;
        gameplayListener = gameplayCamera != null ? gameplayCamera.GetComponent<AudioListener>() : null;
        gameplayFollow = gameplayCamera != null ? gameplayCamera.GetComponent<TacticalCameraFollow>() : null;
    }

    private void SetOpeningCameraActive(bool active)
    {
        if (openingCamera != null) openingCamera.enabled = active;
        if (openingListener != null) openingListener.enabled = active;
    }

    private void SetGameplayCameraActive(bool active)
    {
        if (gameplayCamera != null) gameplayCamera.enabled = active;
        if (gameplayListener != null) gameplayListener.enabled = active;
        if (gameplayFollow != null) gameplayFollow.enabled = active;
    }

    private void OnDrawGizmosSelected()
    {
        if (squadSpawnPoints == null) return;
        Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.9f);
        foreach (Transform marker in squadSpawnPoints)
        {
            if (marker == null) continue;
            Gizmos.DrawWireSphere(marker.position, 0.35f);
            Gizmos.DrawRay(marker.position, marker.forward * 1.2f);
        }
    }
}
