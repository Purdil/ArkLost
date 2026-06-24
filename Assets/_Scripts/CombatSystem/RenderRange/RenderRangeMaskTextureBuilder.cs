using UnityEngine;

namespace _Scripts.CombatSystem.RenderRange
{
    public static class RenderRangeMaskTextureBuilder
    {
        public static Texture2D Build(RenderRangeShape shape, RenderRangePivot pivot, float radius, float innerRadius,
            float angle, float length, float width, float fill, int resolution, bool edgeOnly, float edgeWorldWidth,
            Color tint, string textureName)
        {
            resolution = Mathf.Clamp(resolution, 32, 1024);
            radius = Mathf.Max(0.01f, radius);
            innerRadius = Mathf.Clamp(innerRadius, 0f, radius - 0.01f);
            angle = Mathf.Clamp(angle, 1f, 360f);
            length = Mathf.Max(0.01f, length);
            width = Mathf.Max(0.01f, width);
            fill = Mathf.Clamp01(fill);
            edgeWorldWidth = Mathf.Max(0.01f, edgeWorldWidth);

            Vector2 projectionSize = GetProjectionSize(shape, radius, angle, length, width);
            Vector3 centerOffset = GetProjectionCenterOffset(shape, pivot, radius, angle, length);
            float texelWorldSize = Mathf.Max(projectionSize.x, projectionSize.y) / resolution;
            float feather = Mathf.Max(texelWorldSize * 2f, 0.015f);

            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            Color32[] pixels = new Color32[resolution * resolution];
            Color clampedTint = new Color(Mathf.Clamp01(tint.r), Mathf.Clamp01(tint.g), Mathf.Clamp01(tint.b),
                Mathf.Clamp01(tint.a));
            byte red = (byte)Mathf.RoundToInt(clampedTint.r * 255f);
            byte green = (byte)Mathf.RoundToInt(clampedTint.g * 255f);
            byte blue = (byte)Mathf.RoundToInt(clampedTint.b * 255f);

            for (int y = 0; y < resolution; y++)
            {
                float v = (y + 0.5f) / resolution;
                float localZ = centerOffset.z + (v - 0.5f) * projectionSize.y;

                for (int x = 0; x < resolution; x++)
                {
                    float u = (x + 0.5f) / resolution;
                    float localX = (u - 0.5f) * projectionSize.x;
                    float alpha = edgeOnly
                        ? EvaluateEdgeAlpha(shape, pivot, localX, localZ, radius, innerRadius, angle, length, width,
                            edgeWorldWidth, feather)
                        : EvaluateFillAlpha(shape, pivot, localX, localZ, radius, innerRadius, angle, length, width,
                            fill, feather);

                    byte alphaByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha * clampedTint.a) * 255f);
                    pixels[y * resolution + x] = new Color32(red, green, blue, alphaByte);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        public static Vector2 GetProjectionSize(RenderRangeShape shape, float radius, float angle, float length,
            float width)
        {
            radius = Mathf.Max(0.01f, radius);
            angle = Mathf.Clamp(angle, 1f, 360f);
            length = Mathf.Max(0.01f, length);
            width = Mathf.Max(0.01f, width);

            return shape switch
            {
                RenderRangeShape.Cone when angle < 179.99f => new Vector2(
                    Mathf.Max(0.01f, 2f * radius * Mathf.Sin(angle * 0.5f * Mathf.Deg2Rad)), radius),
                RenderRangeShape.Line => new Vector2(width, length),
                RenderRangeShape.Rectangle => new Vector2(width, length),
                _ => Vector2.one * (radius * 2f)
            };
        }

        public static Vector3 GetProjectionCenterOffset(RenderRangeShape shape, RenderRangePivot pivot, float radius,
            float angle, float length)
        {
            if (shape == RenderRangeShape.Line || shape == RenderRangeShape.Rectangle)
            {
                return pivot == RenderRangePivot.Center ? Vector3.zero : Vector3.forward * (length * 0.5f);
            }

            if (shape == RenderRangeShape.Cone && angle < 179.99f)
            {
                return Vector3.forward * (radius * 0.5f);
            }

            return Vector3.zero;
        }

        private static float EvaluateFillAlpha(RenderRangeShape shape, RenderRangePivot pivot, float localX,
            float localZ, float radius, float innerRadius, float angle, float length, float width, float fill,
            float feather)
        {
            if (fill <= 0.001f)
            {
                return 0f;
            }

            float signedDistance = EvaluateSignedDistance(shape, pivot, localX, localZ, radius, innerRadius, angle,
                length, width, fill);
            return SmoothStep(-feather, feather, signedDistance);
        }

        private static float EvaluateEdgeAlpha(RenderRangeShape shape, RenderRangePivot pivot, float localX,
            float localZ, float radius, float innerRadius, float angle, float length, float width, float edgeWorldWidth,
            float feather)
        {
            float signedDistance = EvaluateSignedDistance(shape, pivot, localX, localZ, radius, innerRadius, angle,
                length, width, 1f);
            float inside = SmoothStep(-feather, feather, signedDistance);
            float band = 1f - SmoothStep(edgeWorldWidth, edgeWorldWidth + feather, signedDistance);
            return inside * band;
        }

        private static float EvaluateSignedDistance(RenderRangeShape shape, RenderRangePivot pivot, float localX,
            float localZ, float radius, float innerRadius, float angle, float length, float width, float fill)
        {
            float distanceFromOrigin = Mathf.Sqrt(localX * localX + localZ * localZ);

            switch (shape)
            {
                case RenderRangeShape.Donut:
                {
                    float currentRadius = innerRadius + (radius - innerRadius) * fill;
                    return Mathf.Min(currentRadius - distanceFromOrigin, distanceFromOrigin - innerRadius);
                }
                case RenderRangeShape.Cone:
                {
                    float currentRadius = radius * fill;
                    if (angle >= 359.99f)
                    {
                        return currentRadius - distanceFromOrigin;
                    }

                    float halfAngle = angle * 0.5f * Mathf.Deg2Rad;
                    float sideDistance = Mathf.Sin(halfAngle) * localZ - Mathf.Cos(halfAngle) * Mathf.Abs(localX);
                    float radiusDistance = currentRadius - distanceFromOrigin;
                    return Mathf.Min(radiusDistance, sideDistance);
                }
                case RenderRangeShape.Line:
                case RenderRangeShape.Rectangle:
                {
                    float currentLength = length * fill;
                    float halfWidth = width * 0.5f;
                    float minZ = pivot == RenderRangePivot.Center ? -currentLength * 0.5f : 0f;
                    float maxZ = pivot == RenderRangePivot.Center ? currentLength * 0.5f : currentLength;
                    float xDistance = halfWidth - Mathf.Abs(localX);
                    float frontDistance = maxZ - localZ;
                    float backDistance = localZ - minZ;
                    return Mathf.Min(Mathf.Min(xDistance, frontDistance), backDistance);
                }
                default:
                    return radius * fill - distanceFromOrigin;
            }
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
