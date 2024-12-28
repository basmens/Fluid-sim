using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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
            foreach (Particle p in simulation.Particles)
                RenderParticle(p);

            texture.Apply();
        }

        void RenderParticle(Particle p)
        {
            Vector2 screenSpacePos = MapWorldToScreenSpace(p.position);
            DrawCircle(screenSpacePos, particleRadius, smoothWidth, particleColor);
        }
    }
}
