using UnityEngine;

namespace Simulation2D
{
    public class ParticleImageRenderer : RawImageRenderer
    {
        [Header("Border")]
        public Color borderColor = Color.white;

        [Header("Particle")]
        public float particleRadius = 2;
        public float smoothWidth = Mathf.Sqrt(2);
        public Color particleColor = Color.blue;

        void LateUpdate()
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
            foreach (Vector2 pos in simulation.Positions)
                RenderParticle(pos);

            texture.Apply();
        }

        void RenderParticle(Vector2 pos)
        {
            Vector2 screenSpacePos = MapWorldToScreenSpace(pos);
            DrawCircle(screenSpacePos, particleRadius, smoothWidth, particleColor);
        }
    }
}
