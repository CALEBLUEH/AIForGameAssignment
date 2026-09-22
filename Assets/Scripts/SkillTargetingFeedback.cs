using UnityEngine;

public class SkillTargetingFeedback : MonoBehaviour
{
    public MeshRenderer rangeRenderer;
    public LineRenderer arrowRenderer;
    public ParticleSystem feedbackParticles;
    private Transform source;
    private float radius;
    private bool showRange;
    private bool showArrow;
    private bool rangeFollowsPointer;
    private Vector3 pointer;

    private void Awake()
    {
        Hide();
    }

    private void LateUpdate()
    {
        if (source == null) return;
        if (showRange && rangeRenderer != null)
        {
            Vector3 center = rangeFollowsPointer ? pointer : source.position;
            rangeRenderer.transform.position = center + Vector3.up * 0.08f;
            rangeRenderer.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        }
        if (showArrow && arrowRenderer != null)
        {
            Vector3 start = source.position + Vector3.up * 0.25f;
            Vector3 delta = pointer - source.position;
            delta.y = 0f;
            Vector3 end = source.position + Vector3.ClampMagnitude(delta, radius) + Vector3.up * 0.25f;
            arrowRenderer.positionCount = 3;
            arrowRenderer.SetPosition(0, start);
            arrowRenderer.SetPosition(1, end);
            Vector3 side = Vector3.Cross(Vector3.up, (end - start).normalized) * 0.45f;
            arrowRenderer.SetPosition(2, end - (end - start).normalized * 0.85f + side);
        }
    }

    public void ShowRange(Transform origin, float skillRadius, bool followPointer = false)
    {
        source = origin;
        radius = skillRadius;
        pointer = origin.position;
        rangeFollowsPointer = followPointer;
        showRange = true;
        showArrow = false;
        if (rangeRenderer != null) rangeRenderer.gameObject.SetActive(true);
        if (arrowRenderer != null) arrowRenderer.gameObject.SetActive(false);
    }

    public void ShowArrow(Transform origin, float movementRange)
    {
        source = origin;
        radius = movementRange;
        pointer = origin.position;
        rangeFollowsPointer = false;
        showRange = false;
        showArrow = true;
        if (rangeRenderer != null) rangeRenderer.gameObject.SetActive(false);
        if (arrowRenderer != null) arrowRenderer.gameObject.SetActive(true);
    }

    public void SetPointer(Vector3 worldPosition) { pointer = worldPosition; }

    public void Hide()
    {
        source = null;
        rangeFollowsPointer = false;
        showRange = showArrow = false;
        if (rangeRenderer != null) rangeRenderer.gameObject.SetActive(false);
        if (arrowRenderer != null) arrowRenderer.gameObject.SetActive(false);
    }

    public void PlayFeedback(Vector3 position, Color color)
    {
        if (feedbackParticles == null) return;
        feedbackParticles.transform.position = position + Vector3.up * 0.2f;
        var main = feedbackParticles.main;
        main.startColor = color;
        feedbackParticles.Play(true);
    }
}
