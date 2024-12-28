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

        void Start()
        {
            texture = new Texture2D(width, height);
            rawImageComponent.texture = texture;
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
                float dist = (point - prevPoint).magnitude * 1.05f;
                for (int t = 0; t < dist; t++)
                {
                    Vector2 p = Vector2.Lerp(prevPoint, point, t / dist);
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
            Vector2 cameraPos = worldCamera.transform.InverseTransformPoint(worldPos);
            float cameraSize = worldCamera.orthographicSize;
            float aspectRatio = worldCamera.aspect;
            return new(
                (cameraPos.x / cameraSize / aspectRatio + 1) / 2 * (width - 1),
                (cameraPos.y / cameraSize + 1) / 2 * (height - 1)
            );
        }

        protected Vector2 MapScreenToWorldSpace(Vector2 screenPos)
        {
            float cameraSize = worldCamera.orthographicSize;
            float aspectRatio = worldCamera.aspect;
            Vector2 cameraPos = new(
                (screenPos.x / (width - 1) * 2 - 1) * cameraSize * aspectRatio,
                (screenPos.y / (height - 1) * 2 - 1) * cameraSize
            );
            return worldCamera.transform.InverseTransformPoint(cameraPos);
        }
    }
}
