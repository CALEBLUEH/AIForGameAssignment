using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SkillTargetingFeedback : MonoBehaviour
{
    public MeshRenderer rangeRenderer;
    public LineRenderer arrowRenderer;
    public ParticleSystem feedbackParticles;
    [Range(8, 64)] public int shapeSegments = 32;
    [Min(0.01f)] public float groundOffset = 0.08f;

    private enum Shape { None, CircleAtSource, CircleAtPointer, Cone, Arrow }
    private Transform source;
    private AutoCombatAI skillOwner;
    private float radius;
    private float castRange;
    private float coneAngle;
    private Shape shape;
    private Vector3 pointer;
    private Mesh indicatorMesh;
    private bool useWorldSource;
    private bool clampPointerToRange = true;
    private Vector3 worldSource;

    public Vector3 Pointer => pointer;

    private void Awake()
    {
        EnsureMesh();
        Hide();
    }

    private void LateUpdate()
    {
        if (source == null && !useWorldSource) return;
        if (shape == Shape.Arrow) UpdateArrow();
        else if (shape != Shape.None) UpdateShape();
    }

    public void ShowCharacterSkill(AutoCombatAI owner)
    {
        useWorldSource = false;
        clampPointerToRange = true;
        skillOwner = owner;
        source = owner == null ? null : owner.transform;
        if (owner == null) { Hide(); return; }
        castRange = owner.skillRange;
        coneAngle = owner.coneAngle;
        radius = owner.aoeRadius;
        pointer = owner.NavMeshWorldPosition + owner.transform.forward * Mathf.Min(2f, castRange);
        switch (owner.role)
        {
            case AutoCombatAI.CombatRole.MomoiLowCostAOE:
            case AutoCombatAI.CombatRole.HinaHighCostAOE:
                shape = Shape.Cone;
                break;
            case AutoCombatAI.CombatRole.AyaneHealer:
                shape = Shape.CircleAtPointer;
                break;
            case AutoCombatAI.CombatRole.YuukaTank:
                radius = Mathf.Max(0.5f, owner.shieldVisualRadius);
                shape = Shape.CircleAtSource;
                break;
            default:
                radius = castRange;
                shape = Shape.CircleAtSource;
                break;
        }
        SetVisibility(true, false);
    }

    public void ShowRange(Transform origin, float skillRadius, bool followPointer = false)
    {
        useWorldSource = false;
        clampPointerToRange = true;
        skillOwner = origin == null ? null : origin.GetComponent<AutoCombatAI>();
        source = origin;
        radius = skillRadius;
        castRange = skillRadius;
        pointer = origin == null ? Vector3.zero : origin.position;
        shape = followPointer ? Shape.CircleAtPointer : Shape.CircleAtSource;
        SetVisibility(true, false);
    }

    public void ShowArrow(Transform origin, float movementRange)
    {
        useWorldSource = false;
        clampPointerToRange = true;
        skillOwner = origin == null ? null : origin.GetComponent<AutoCombatAI>();
        source = origin;
        radius = movementRange;
        castRange = movementRange;
        pointer = origin == null ? Vector3.zero : origin.position;
        shape = Shape.Arrow;
        SetVisibility(false, true);
    }

    public void ShowWorldCircle(Vector3 initialPointer, float skillRadius)
    {
        skillOwner = null;
        source = null;
        useWorldSource = true;
        clampPointerToRange = false;
        worldSource = initialPointer;
        pointer = initialPointer;
        radius = Mathf.Max(0.1f, skillRadius);
        castRange = float.PositiveInfinity;
        shape = Shape.CircleAtPointer;
        SetVisibility(true, false);
    }

    public void SetPointer(Vector3 worldPosition)
    {
        if (source == null && !useWorldSource) return;
        Vector3 origin = SourceGroundPosition();
        Vector3 delta = worldPosition - origin;
        delta.y = 0f;
        pointer = clampPointerToRange
            ? origin + Vector3.ClampMagnitude(delta, Mathf.Max(0.1f, castRange))
            : worldPosition;
        if (NavMesh.SamplePosition(pointer, out NavMeshHit hit, 4f, NavMesh.AllAreas)) pointer = hit.position;
    }

    public void Hide()
    {
        source = null;
        skillOwner = null;
        useWorldSource = false;
        clampPointerToRange = true;
        shape = Shape.None;
        SetVisibility(false, false);
        if (indicatorMesh != null) indicatorMesh.Clear();
    }

    public void PlayFeedback(Vector3 position, Color color)
    {
        if (feedbackParticles == null) return;
        feedbackParticles.transform.position = position + Vector3.up * 0.2f;
        var main = feedbackParticles.main;
        main.startColor = color;
        feedbackParticles.Play(true);
    }

    private void UpdateShape()
    {
        EnsureMesh();
        if (indicatorMesh == null) return;
        Vector3 origin = SourceGroundPosition();
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        if (shape == Shape.Cone)
        {
            Vector3 direction = pointer - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = source.forward;
            direction.Normalize();
            vertices.Add(Project(origin));
            for (int i = 0; i <= shapeSegments; i++)
            {
                float angle = -coneAngle * 0.5f + coneAngle * i / shapeSegments;
                Vector3 edge = Quaternion.Euler(0f, angle, 0f) * direction;
                vertices.Add(Project(origin + edge * castRange));
                if (i > 0)
                {
                    triangles.Add(0);
                    triangles.Add(i);
                    triangles.Add(i + 1);
                }
            }
        }
        else
        {
            Vector3 center = shape == Shape.CircleAtPointer ? pointer : origin;
            vertices.Add(Project(center));
            for (int i = 0; i <= shapeSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / shapeSegments;
                Vector3 edge = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(Project(center + edge * radius));
                if (i > 0)
                {
                    triangles.Add(0);
                    triangles.Add(i);
                    triangles.Add(i + 1);
                }
            }
        }

        Transform meshTransform = rangeRenderer.transform;
        for (int i = 0; i < vertices.Count; i++) vertices[i] = meshTransform.InverseTransformPoint(vertices[i]);
        indicatorMesh.Clear();
        indicatorMesh.SetVertices(vertices);
        indicatorMesh.SetTriangles(triangles, 0);
        indicatorMesh.RecalculateNormals();
        indicatorMesh.RecalculateBounds();
    }

    private void UpdateArrow()
    {
        if (arrowRenderer == null) return;
        Vector3 origin = SourceGroundPosition();
        Vector3 delta = pointer - origin;
        delta.y = 0f;
        Vector3 end = origin + Vector3.ClampMagnitude(delta, radius);
        Vector3 start = Project(origin) + Vector3.up * 0.17f;
        end = Project(end) + Vector3.up * 0.17f;
        arrowRenderer.positionCount = 3;
        arrowRenderer.SetPosition(0, start);
        arrowRenderer.SetPosition(1, end);
        Vector3 travel = end - start;
        Vector3 side = travel.sqrMagnitude < 0.01f ? Vector3.right : Vector3.Cross(Vector3.up, travel.normalized) * 0.45f;
        arrowRenderer.SetPosition(2, end - travel.normalized * 0.85f + side);
    }

    private Vector3 SourceGroundPosition() => useWorldSource
        ? worldSource
        : skillOwner == null ? source.position : skillOwner.NavMeshWorldPosition;

    private Vector3 Project(Vector3 world)
    {
        if (NavMesh.SamplePosition(world, out NavMeshHit hit, 5f, NavMesh.AllAreas)) world.y = hit.position.y;
        return world + Vector3.up * groundOffset;
    }

    private void EnsureMesh()
    {
        if (rangeRenderer == null) return;
        MeshFilter filter = rangeRenderer.GetComponent<MeshFilter>();
        if (filter == null) filter = rangeRenderer.gameObject.AddComponent<MeshFilter>();
        if (indicatorMesh == null)
        {
            indicatorMesh = new Mesh { name = "Runtime Ground Projected Skill Indicator" };
            indicatorMesh.MarkDynamic();
            filter.sharedMesh = indicatorMesh;
        }
    }

    private void SetVisibility(bool showRange, bool showArrow)
    {
        if (rangeRenderer != null) rangeRenderer.gameObject.SetActive(showRange);
        if (arrowRenderer != null) arrowRenderer.gameObject.SetActive(showArrow);
    }
}
