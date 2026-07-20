using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GroundRadiusVisual : MonoBehaviour
{
    [Header("Radius Source")]
    public SquadMember squadMember;

    [Header("Fallback Radius")]
    public float radius = 18f;

    [Header("Ring Shape")]
    public float ringWidth = 0.3f;

    [Range(16, 256)]
    public int segments = 100;

    [Header("Ground Offset")]
    public float heightOffset = 0.1f;

    private Mesh generatedMesh;
    private float lastRadius = -1f;

    private void Awake()
    {
        GenerateRing();
    }

    private void Update()
    {
        float currentRadius = GetCurrentRadius();

        if (!Mathf.Approximately(currentRadius, lastRadius))
        {
            GenerateRing();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            GenerateRing();
        }
    }

    private float GetCurrentRadius()
    {
        if (squadMember != null)
        {
            return squadMember.leaderCombatRadius;
        }

        return radius;
    }

    public void GenerateRing()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null)
        {
            return;
        }

        float currentRadius = Mathf.Max(0.1f, GetCurrentRadius());

        if (generatedMesh == null)
        {
            generatedMesh = new Mesh();
            generatedMesh.name = "Ground Radius Ring";
        }
        else
        {
            generatedMesh.Clear();
        }

        segments = Mathf.Max(3, segments);
        ringWidth = Mathf.Clamp(ringWidth, 0.01f, currentRadius);

        float outerRadius = currentRadius;
        float innerRadius = currentRadius - ringWidth;

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float percentage = (float)i / segments;
            float angle = percentage * Mathf.PI * 2f;

            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            int vertexIndex = i * 2;

            vertices[vertexIndex] = new Vector3(
                cos * outerRadius,
                heightOffset,
                sin * outerRadius
            );

            vertices[vertexIndex + 1] = new Vector3(
                cos * innerRadius,
                heightOffset,
                sin * innerRadius
            );

            uvs[vertexIndex] = new Vector2(percentage, 1f);
            uvs[vertexIndex + 1] = new Vector2(percentage, 0f);
        }

        for (int i = 0; i < segments; i++)
        {
            int triangleIndex = i * 6;
            int vertexIndex = i * 2;

            // Facing upward
            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 1;
            triangles[triangleIndex + 2] = vertexIndex + 2;

            triangles[triangleIndex + 3] = vertexIndex + 1;
            triangles[triangleIndex + 4] = vertexIndex + 3;
            triangles[triangleIndex + 5] = vertexIndex + 2;
        }

        generatedMesh.vertices = vertices;
        generatedMesh.uv = uvs;
        generatedMesh.triangles = triangles;

        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();

        meshFilter.sharedMesh = generatedMesh;

        lastRadius = currentRadius;
    }
}