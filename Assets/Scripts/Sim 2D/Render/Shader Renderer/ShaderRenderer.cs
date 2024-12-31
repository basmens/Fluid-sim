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
        public Color staticParticleColor = Color.blue;

        bool needsUpdate = true;
        Material material;
        ComputeBuffer positionsBuffer;


        void Start()
        {
            material = new Material(shader);
        }

        void LateUpdate()
        {
            if (shader == null) return;

            UpdateSettings();

            positionsBuffer.SetData(simulation.Positions);
            material.SetBuffer("_ParticlePositions", positionsBuffer);
        
            // material.SetMatrix("_BillboardMatrix", Matrix4x4.Rotate(worldCamera.transform.rotation).inverse);

            RenderParams param = new(material) { camera = worldCamera };
            Graphics.RenderMeshPrimitives(param, mesh, 0, simulation.NumParticles);
        }

        void UpdateSettings()
        {
            if (!needsUpdate) return;
            needsUpdate = false;

            material.SetColor("_ParticleColor", staticParticleColor);
            material.SetFloat("_Scale", scale);
            material.SetFloat("_EdgeSoftness", edgeSoftness);

            if (positionsBuffer != null) return;
            positionsBuffer = new(simulation.NumParticles, 16);
        }

        void OnValidate()
        {
            needsUpdate = true;
        }

        void OnDestroy()
        {
            positionsBuffer.Release();
        }
    }

    public enum ParticleColorSource
    {
        Static
    }
}
