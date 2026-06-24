using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.CombatSystem.RenderRange
{
    public static class RenderRangeMeshBuilder
    {
        public static Mesh Build(RenderRangeShape shape, float radius, float innerRadius, float angle, float length,
            float width, float fill, RenderRangePivot pivot, int segments)
        {
            fill = Mathf.Clamp01(fill);
            radius = Mathf.Max(0.01f, radius);
            innerRadius = Mathf.Clamp(innerRadius, 0f, radius - 0.01f);
            angle = Mathf.Clamp(angle, 1f, 360f);
            length = Mathf.Max(0.01f, length);
            width = Mathf.Max(0.01f, width);
            segments = Mathf.Clamp(segments, 8, 256);

            return shape switch
            {
                RenderRangeShape.Circle => BuildDisc(radius * fill, 0f, 360f, segments, "RenderRange Circle"),
                RenderRangeShape.Donut => BuildDisc(radius, innerRadius + (radius - innerRadius) * (1f - fill),
                    360f, segments, "RenderRange Donut"),
                RenderRangeShape.Cone => BuildDisc(radius * fill, 0f, angle, segments, "RenderRange Cone"),
                RenderRangeShape.Line => BuildRectangle(length * fill, width, pivot, "RenderRange Line"),
                RenderRangeShape.Rectangle => BuildRectangle(length * fill, width, pivot, "RenderRange Rectangle"),
                _ => BuildDisc(radius * fill, 0f, 360f, segments, "RenderRange")
            };
        }

        private static Mesh BuildDisc(float radius, float innerRadius, float angle, int segments, string meshName)
        {
            Mesh mesh = new Mesh
            {
                name = meshName
            };

            if (radius <= 0.001f)
            {
                return mesh;
            }

            int segmentCount = Mathf.Max(3, Mathf.CeilToInt(segments * (angle / 360f)));
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            if (innerRadius <= 0.001f)
            {
                vertices.Add(Vector3.zero);
                uvs.Add(new Vector2(0.5f, 0.5f));

                for (int i = 0; i <= segmentCount; i++)
                {
                    float t = (float)i / segmentCount;
                    float degrees = angle >= 359.99f ? t * 360f : -angle * 0.5f + t * angle;
                    Vector3 point = GetDiscPoint(degrees, radius);
                    vertices.Add(point);
                    uvs.Add(GetDiscUv(point, radius));
                }

                for (int i = 1; i <= segmentCount; i++)
                {
                    triangles.Add(0);
                    triangles.Add(i);
                    triangles.Add(i + 1);
                }
            }
            else
            {
                for (int i = 0; i <= segmentCount; i++)
                {
                    float t = (float)i / segmentCount;
                    float degrees = angle >= 359.99f ? t * 360f : -angle * 0.5f + t * angle;
                    Vector3 outer = GetDiscPoint(degrees, radius);
                    Vector3 inner = GetDiscPoint(degrees, innerRadius);

                    vertices.Add(outer);
                    uvs.Add(GetDiscUv(outer, radius));
                    vertices.Add(inner);
                    uvs.Add(GetDiscUv(inner, radius));
                }

                for (int i = 0; i < segmentCount; i++)
                {
                    int outerA = i * 2;
                    int innerA = outerA + 1;
                    int outerB = outerA + 2;
                    int innerB = outerA + 3;

                    triangles.Add(outerA);
                    triangles.Add(outerB);
                    triangles.Add(innerA);
                    triangles.Add(innerA);
                    triangles.Add(outerB);
                    triangles.Add(innerB);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildRectangle(float length, float width, RenderRangePivot pivot, string meshName)
        {
            Mesh mesh = new Mesh
            {
                name = meshName
            };

            if (length <= 0.001f || width <= 0.001f)
            {
                return mesh;
            }

            float halfWidth = width * 0.5f;
            float minZ = pivot == RenderRangePivot.Center ? -length * 0.5f : 0f;
            float maxZ = pivot == RenderRangePivot.Center ? length * 0.5f : length;

            Vector3[] vertices =
            {
                new Vector3(-halfWidth, 0f, minZ),
                new Vector3(halfWidth, 0f, minZ),
                new Vector3(-halfWidth, 0f, maxZ),
                new Vector3(halfWidth, 0f, maxZ)
            };

            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };

            int[] triangles =
            {
                0, 2, 1,
                1, 2, 3
            };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Vector3 GetDiscPoint(float degrees, float radius)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians) * radius, 0f, Mathf.Cos(radians) * radius);
        }

        private static Vector2 GetDiscUv(Vector3 point, float radius)
        {
            return new Vector2(point.x / (radius * 2f) + 0.5f, point.z / (radius * 2f) + 0.5f);
        }
    }
}
