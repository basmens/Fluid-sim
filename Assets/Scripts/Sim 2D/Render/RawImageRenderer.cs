using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Simulation2D
{
    public class RawImageRenderer : MonoBehaviour
    {
        [Header("Rendering")]
        public Simulation simulation;
        public RawImage rawImageComponent;
        public Camera worldCamera;

        [Header("Background")]
        public Color backgroundColor = Color.black;

        protected Texture2D texture;
        protected readonly int width = Screen.width;
        protected readonly int height = Screen.height;

        Matrix4x4 worldToScreenMatrix;
        Matrix4x4 screenToWorldMatrix;

        void Start()
        {
            texture = new Texture2D(width, height);
            rawImageComponent.texture = texture;
        }

        void Update()
        {
            float cameraH = worldCamera.orthographicSize;
            float cameraW = cameraH * worldCamera.aspect;

            float halfW = (width - 1) / 2f;
            float halfH = (height - 1) / 2f;

            worldToScreenMatrix = Matrix4x4.Translate(new(halfW, halfH))
                * Matrix4x4.Scale(new(halfW / cameraW, halfH / cameraH))
                * worldCamera.transform.worldToLocalMatrix;

            screenToWorldMatrix = worldCamera.transform.localToWorldMatrix
                * Matrix4x4.Scale(new(cameraW / halfW, cameraH / halfH))
                * Matrix4x4.Translate(new(-halfW, -halfH));
        }


        protected void SetBackgroundColor(Color color)
        {
            Color[] background = new Color[width * height];
            for (int i = 0; i < background.Length; i++)
                background[i] = backgroundColor;
            texture.SetPixels(background);
        }

        protected void DrawPath(Color color, params Vector2[] points)
        {
            Vector2 prevPoint = points.Last();
            foreach (Vector2 point in points)
            {
                // Times 1.05 to fill some gaps that might just sneak through
                Vector2 dir = (point - prevPoint).normalized;
                Vector2 p1 = ClampWithinBounds(point, dir);
                Vector2 p2 = ClampWithinBounds(prevPoint, dir);
                float dist = (p1 - p2).magnitude * 1.05f;
                for (int t = 0; t < dist; t++)
                {
                    Vector2 p = Vector2.Lerp(p2, p1, t / dist);
                    if (p.x >= 0 && p.x < width && p.y >= 0 && p.y < height)
                        texture.SetPixel((int)p.x, (int)p.y, color);
                }
                prevPoint = point;
            }
        }

        protected void DrawCircle(Vector2 center, float radius, float smoothWidth, Color color)
        {
            int minX = Math.Max((int)Math.Floor(center.x - radius - smoothWidth), 0);
            int maxX = Math.Min((int)Math.Ceiling(center.x + radius + smoothWidth), width - 1);
            int minY = Math.Max((int)Math.Floor(center.y - radius - smoothWidth), 0);
            int maxY = Math.Min((int)Math.Ceiling(center.y + radius + smoothWidth), height - 1);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    float dist = (new Vector2(x, y) - center).magnitude;
                    if (dist <= radius)
                    {
                        texture.SetPixel(x, y, color);
                        continue;
                    }

                    if (dist > radius + smoothWidth) continue;

                    float t = (dist - radius) / smoothWidth;
                    Color fadedColor = ColorSmoothStep(color, texture.GetPixel(x, y), t);
                    texture.SetPixel(x, y, fadedColor);
                }
            }
        }

        protected Vector2 ClampWithinBounds(Vector2 position, Vector2 dir)
        {
            if (position.x < 0) {
                Vector2 newPos = LineLineIntersect(Vector2.zero, Vector2.up, position, dir);
                if (newPos != Vector2.negativeInfinity) position = newPos;
            }
            if (position.x >= width) {
                Vector2 newPos = LineLineIntersect(Vector2.right * width, Vector2.up, position, dir);
                if (newPos != Vector2.negativeInfinity) position = newPos;
            }
            if (position.y < 0) {
                Vector2 newPos = LineLineIntersect(Vector2.zero, Vector2.right, position, dir);
                if (newPos != Vector2.negativeInfinity) position = newPos;
            }
            if (position.y >= height) {
                Vector2 newPos = LineLineIntersect(Vector2.up * height, Vector2.right, position, dir);
                if (newPos != Vector2.negativeInfinity) position = newPos;
            }
            return position;
        }

        protected Vector2 LineLineIntersect(Vector2 anchor1, Vector2 dir1, Vector2 anchor2, Vector2 dir2)
        {
            // Solve the formula for t1:
            // [dx1, -dx2] * [t1] = [x2 - x1]
            // [dy1, -dy2]   [t2] = [y2 - y1]
            // So
            // [t1] = 1/D * [-dy2, dx2] * [x2 - x1]
            // [t2]         [-dy1, dx1]   [y2 - y1]

            float determinant = dir1.x * -dir2.y - dir1.y * -dir2.x;
            if (determinant == 0) return Vector2.negativeInfinity;
            float t1 = (-dir2.y * (anchor2.x - anchor1.x) + dir2.x * (anchor2.y - anchor1.y)) / determinant;
            return anchor1 + t1 * dir1;
        }

        protected Color ColorSmoothStep(Color from, Color to, float t)
        {
            return new Color(
                Mathf.SmoothStep(from.r, to.r, t),
                Mathf.SmoothStep(from.g, to.g, t),
                Mathf.SmoothStep(from.b, to.b, t),
                Mathf.SmoothStep(from.a, to.a, t)
            );
        }

        protected Vector2 MapWorldToScreenSpace(Vector2 worldPos)
        {
            return worldToScreenMatrix.MultiplyPoint(worldPos);
        }

        protected Vector2 MapScreenToWorldSpace(Vector2 screenPos)
        {
            return screenToWorldMatrix.MultiplyPoint(screenPos);
        }

        protected Vector2 MapWorldToScreenSpaceDir(Vector2 worldDir)
        {
            return worldToScreenMatrix.MultiplyVector(worldDir);
        }

        protected Vector2 MapScreenToWorldSpaceDir(Vector2 screenDir)
        {
            return screenToWorldMatrix.MultiplyVector(screenDir);
        }
    }
}
