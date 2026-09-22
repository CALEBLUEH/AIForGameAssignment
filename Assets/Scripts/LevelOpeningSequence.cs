using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
    public float AppliedPitchDecrease { get; private set; }

    private void Awake()
    {
        CacheCameraComponents();
        if (openingCamera != null) openingStartRotation = openingCamera.transform.localRotation;
        SetOpeningCameraActive(false);
        SetGameplayCameraActive(true);
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

    private IEnumerator PlayRoutine(IReadOnlyList<AutoCombatAI> selectedSquad, Action completed)
    {
        IsPlaying = true;
        PlaceSquad(selectedSquad);
        CacheCameraComponents();
        openingStartRotation = openingCamera != null ? openingCamera.transform.localRotation : Quaternion.identity;
        SetGameplayCameraActive(false);
        SetOpeningCameraActive(true);

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
        runningSequence = null;
        completed?.Invoke();
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
