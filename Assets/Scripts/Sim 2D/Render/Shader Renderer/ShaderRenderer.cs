using System;
using System.Runtime.InteropServices;
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

        [Header("Border")]
        public Color borderColor = Color.white;

        [Header("Particle")]
        public ParticleColorSource particleColorSource = ParticleColorSource.Static;
        public float scale = 1;
        [Range(0, 1f)] public float edgeSoftness = 0.5f;
        public int coloringTextureResolution = 100;
        public Color staticParticleColor = Color.blue;
        public Gradient velocityColorGradient;
        public float maxVelocity = 100;
        public Gradient densityColorGradient;
        public float maxDensityFactor = 10;

        bool needsUpdate = true;
        Material material;
        Texture2D coloringTexture;
        RenderParams renderParams;
        ComputeBuffer positionsBuffer;
        ComputeBuffer velocitiesBuffer;

        ComputeBuffer densitiesBuffer;


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

            // Update buffers
            positionsBuffer.SetData(simulation.Positions);
            velocitiesBuffer.SetData(simulation.Velocities);

            if (particleColorSource == ParticleColorSource.Density)
            {
                densitiesBuffer.SetData(simulation.Densities);
                material.SetFloat("_PropertyTarget", simulation.targetDensity);
            }

            // Render particles
            // material.SetMatrix("_BillboardMatrix", Matrix4x4.Rotate(worldCamera.transform.rotation).inverse);
            Graphics.RenderMeshPrimitives(renderParams, mesh, 0, simulation.NumParticles);
        }

        void UpdateSettings()
        {
            if (!needsUpdate) return;
            needsUpdate = false;

            worldCamera.backgroundColor = backgroundColor;
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            renderParams = new(material) { camera = worldCamera };

            material.SetFloat("_Scale", scale);
            material.SetFloat("_EdgeSoftness", edgeSoftness);
            DiscernColoringProperty();

            if (positionsBuffer != null) return;
            int stride = Marshal.SizeOf(typeof(Vector2));
            positionsBuffer = new(simulation.NumParticles, stride);
            velocitiesBuffer = new(simulation.NumParticles, stride);
            densitiesBuffer = new(simulation.NumParticles, Marshal.SizeOf(typeof(float)));
            
            material.SetBuffer("_Positions", positionsBuffer);
            material.SetBuffer("_Velocities", velocitiesBuffer);
            material.SetBuffer("_Densities", densitiesBuffer);
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
                    material.SetFloat("_PropertyMax", maxVelocity);
                    break;
                case ParticleColorSource.Density:
                    GenerateColoringTexture(coloringTextureResolution, v => densityColorGradient.Evaluate(v));
                    material.SetTexture("_ColoringTexture", coloringTexture);
                    material.SetInteger("_ColoringProperty", 2);
                    material.SetFloat("_PropertyMax", maxDensityFactor);
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
        Velocity,
        Density
    }
}
