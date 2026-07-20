using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class WokVisualMesh : MonoBehaviour
{
    [SerializeField, Range(16, 96)] private int radialSegments = 48;
    [SerializeField, Range(3, 20)] private int rings = 10;
    [SerializeField, Min(0.1f)] private float radius = 0.80f;
    [SerializeField, Min(0.01f)] private float bowlDepth = 0.27f;
    [SerializeField] private float centerHeight = 0.015f;

    private Mesh generatedMesh;

    private void OnEnable()
    {
        BuildMesh();
    }

    private void OnValidate()
    {
        BuildMesh();
    }

    private void OnDestroy()
    {
        if (generatedMesh == null)
            return;

        if (Application.isPlaying)
            Destroy(generatedMesh);
        else
            DestroyImmediate(generatedMesh);
    }

    private void BuildMesh()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null)
            return;

        if (generatedMesh != null)
        {
            if (Application.isPlaying)
                Destroy(generatedMesh);
            else
                DestroyImmediate(generatedMesh);
        }

        generatedMesh = new Mesh { name = "Generated Wok Visual" };
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        vertices.Add(new Vector3(0f, centerHeight, 0f));
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int ring = 1; ring <= rings; ring++)
        {
            float normalizedRadius = ring / (float)rings;
            float ringRadius = radius * normalizedRadius;
            float height = centerHeight + bowlDepth * normalizedRadius * normalizedRadius;

            for (int segment = 0; segment < radialSegments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / radialSegments;
                float x = Mathf.Sin(angle) * ringRadius;
                float z = Mathf.Cos(angle) * ringRadius;
                vertices.Add(new Vector3(x, height, z));
                uvs.Add(new Vector2(x / (radius * 2f) + 0.5f, z / (radius * 2f) + 0.5f));
            }
        }

        for (int segment = 0; segment < radialSegments; segment++)
        {
            int next = (segment + 1) % radialSegments;
            triangles.Add(0);
            triangles.Add(1 + segment);
            triangles.Add(1 + next);
        }

        for (int ring = 1; ring < rings; ring++)
        {
            int innerStart = 1 + (ring - 1) * radialSegments;
            int outerStart = 1 + ring * radialSegments;

            for (int segment = 0; segment < radialSegments; segment++)
            {
                int next = (segment + 1) % radialSegments;
                int inner = innerStart + segment;
                int innerNext = innerStart + next;
                int outer = outerStart + segment;
                int outerNext = outerStart + next;

                triangles.Add(inner);
                triangles.Add(outer);
                triangles.Add(outerNext);
                triangles.Add(inner);
                triangles.Add(outerNext);
                triangles.Add(innerNext);
            }
        }

        generatedMesh.SetVertices(vertices);
        generatedMesh.SetUVs(0, uvs);
        generatedMesh.SetTriangles(triangles, 0);
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateTangents();
        generatedMesh.RecalculateBounds();
        filter.sharedMesh = generatedMesh;
    }
}
