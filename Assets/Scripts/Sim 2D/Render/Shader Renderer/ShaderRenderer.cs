using System;
using UnityEngine;

namespace Simulation2D
{
    public class ShaderRenderer : MonoBehaviour
    {
        [Header("Rendering")]
        public Simulation simulation;
        public Camera worldCamera;
        public Shader shader;
        public Mesh mesh;

        [Header("Background")]
        public Color backgroundColor = Color.black;

        [Header("Particle")]
        public ParticleColorSource particleColorSource = ParticleColorSource.Static;
        public float scale = 1;
        [Range(0, 1f)] public float edgeSoftness = 0.5f;
        public int coloringTextureResolution = 100;
        public Color staticParticleColor = Color.blue;
        public Gradient velocityColorGradient;
        public float maxVelocity = 100;

        bool needsUpdate = true;
        Material material;
        Texture2D coloringTexture;
        ComputeBuffer positionsBuffer;
        ComputeBuffer velocitiesBuffer;


        void Start()
        {
            material = new(shader);
            coloringTexture = new(1, 1)
            {
                filterMode = FilterMode.Bilinear
            };
        }

        void LateUpdate()
        {
            if (shader == null) return;

            UpdateSettings();

            positionsBuffer.SetData(simulation.Positions);
            velocitiesBuffer.SetData(simulation.Velocities);
            material.SetBuffer("_Positions", positionsBuffer);
            material.SetBuffer("_Velocities", velocitiesBuffer);

            // material.SetMatrix("_BillboardMatrix", Matrix4x4.Rotate(worldCamera.transform.rotation).inverse);

            RenderParams param = new(material) { camera = worldCamera };
            Graphics.RenderMeshPrimitives(param, mesh, 0, simulation.NumParticles);
        }

        void UpdateSettings()
        {
            if (!needsUpdate) return;
            needsUpdate = false;

            material.SetFloat("_Scale", scale);
            material.SetFloat("_EdgeSoftness", edgeSoftness);
            DiscernColoringProperty();

            if (positionsBuffer != null) return;
            int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector2));
            positionsBuffer = new(simulation.NumParticles, 8);
            velocitiesBuffer = new(simulation.NumParticles, 8);
        }

        void DiscernColoringProperty()
        {
            switch (particleColorSource)
            {
                case ParticleColorSource.Static:
                    GenerateColoringTexture(1, _ => staticParticleColor);
                    material.SetTexture("_ColoringTexture", coloringTexture);
                    material.SetInteger("_ColoringProperty", 0);
                    break;
                case ParticleColorSource.Velocity:
                    GenerateColoringTexture(coloringTextureResolution, v => velocityColorGradient.Evaluate(v));
                    material.SetTexture("_ColoringTexture", coloringTexture);
                    material.SetInteger("_ColoringProperty", 1);
                    material.SetFloat("_PropertyMin", 0);
                    material.SetFloat("_PropertyMax", maxVelocity);
                    break;
            }
        }

        void GenerateColoringTexture(int width, Func<float, Color> posToColor)
        {
            if (coloringTexture.width != width) coloringTexture.Reinitialize(width, 1);

            Color[] colors = new Color[width];
            for (int i = 0; i < width; i++)
            {
                colors[i] = posToColor((float)i / (width - 1));
            }
            coloringTexture.SetPixels(colors);
            coloringTexture.Apply();
        }

        void OnValidate()
        {
            needsUpdate = true;
        }

        void OnDestroy()
        {
            positionsBuffer.Release();
            velocitiesBuffer.Release();
        }
    }

    public enum ParticleColorSource
    {
        Static,
        Velocity
    }
}
