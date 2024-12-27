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

        [Header("Border")]
        public Color borderColor = Color.white;

        [Header("Particle")]
        public float particleRadius = 5f;
        public float smoothWidth = Mathf.Sqrt(2);
        public Color particleColor = Color.blue;

        Texture2D texture;
        readonly int width = Screen.width;
        readonly int height = Screen.height;

        void Start()
        {
            texture = new Texture2D(width, height);
            rawImageComponent.texture = texture;
        }

        void LateUpdate()
        {
            RenderParticles();
        }

        void RenderParticles()
        {
            // Clear texture
            SetBackgroundColor(backgroundColor);

            // Draw simulation bounds
            Vector2 leftTop = MapWorldToScreenSpace(simulation.transform.TransformPoint(new(-0.5f, 0.5f)));
            Vector2 rightTop = MapWorldToScreenSpace(simulation.transform.TransformPoint(new(0.5f, 0.5f)));
            Vector2 rightBottom = MapWorldToScreenSpace(simulation.transform.TransformPoint(new(0.5f, -0.5f)));
            Vector2 leftBottom = MapWorldToScreenSpace(simulation.transform.TransformPoint(new(-0.5f, -0.5f)));
            DrawPath(borderColor, leftTop, rightTop, rightBottom, leftBottom);

            // Render particles
            foreach (Particle p in simulation.Particles)
                RenderParticle(p);

            texture.Apply();
        }

        void SetBackgroundColor(Color color)
        {
            Color[] background = new Color[width * height];
            for (int i = 0; i < background.Length; i++)
                background[i] = backgroundColor;
            texture.SetPixels(background);
        }

        void DrawPath(Color color, params Vector2[] points)
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

        void RenderParticle(Particle p)
        {
            Vector2 screenSpacePos = MapWorldToScreenSpace(p.position);
            int minX = Math.Max((int)Math.Floor(-particleRadius - smoothWidth + screenSpacePos.x), 0);
            int maxX = Math.Min((int)Math.Ceiling(particleRadius + smoothWidth + screenSpacePos.x), width - 1);
            int minY = Math.Max((int)Math.Floor(-particleRadius - smoothWidth + screenSpacePos.y), 0);
            int maxY = Math.Min((int)Math.Ceiling(particleRadius + smoothWidth + screenSpacePos.y), height - 1);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    float dist = (new Vector2(x, y) - screenSpacePos).magnitude;
                    if (dist <= particleRadius)
                    {
                        texture.SetPixel(x, y, particleColor);
                        continue;
                    }

                    if (dist > particleRadius + smoothWidth) continue;

                    float t = (dist - particleRadius) / smoothWidth;
                    Color color = ColorSmoothStep(particleColor, texture.GetPixel(x, y), t);
                    texture.SetPixel(x, y, color);
                }
            }
        }

        Color ColorSmoothStep(Color from, Color to, float t)
        {
            return new Color(
                Mathf.SmoothStep(from.r, to.r, t),
                Mathf.SmoothStep(from.g, to.g, t),
                Mathf.SmoothStep(from.b, to.b, t),
                Mathf.SmoothStep(from.a, to.a, t)
            );
        }

        Vector2 MapWorldToScreenSpace(Vector2 worldPos)
        {
            worldPos = worldCamera.transform.InverseTransformPoint(worldPos);
            float cameraSize = worldCamera.orthographicSize;
            float aspectRatio = worldCamera.aspect;
            return new Vector2(
                (worldPos.x / cameraSize / aspectRatio + 1) / 2 * (width - 1),
                (worldPos.y / cameraSize + 1) / 2 * (height - 1)
            );
        }
    }
}
