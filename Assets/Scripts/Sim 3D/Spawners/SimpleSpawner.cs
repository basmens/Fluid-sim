using UnityEngine;


namespace Simulation3D
{
    public class SimpleSpawner : Spawner
    {
        [Header("Spawner")]
        public int particlesPerAxis;

        public override SpawnData Spawn()
        {
            int numParticles = particlesPerAxis * particlesPerAxis * particlesPerAxis;
            Vector3[] particlePositions = new Vector3[numParticles];
            Vector3[] particleVelocities = new Vector3[numParticles];

            int i = 0;
            for (int x = 0; x < particlesPerAxis; x++)
            {
                float px = (float)x / (particlesPerAxis - 1) - 0.5f;
                for (int y = 0; y < particlesPerAxis; y++)
                {
                    float py = (float)y / (particlesPerAxis - 1) - 0.5f;
                    for (int z = 0; z < particlesPerAxis; z++)
                    {
                        float pz = (float)z / (particlesPerAxis - 1) - 0.5f;
                        particlePositions[i] = new Vector3(px, py, pz);
                        particleVelocities[i] = Vector3.zero;
                        i++;
                    }
                }
            }

            transform.TransformPoints(particlePositions);
            return new SpawnData
            {
                position = particlePositions,
                velocity = particleVelocities
            };
        }

        void OnDrawGizmos()
        {
            // Draw Bounds
            var m = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, 0, 0.5f, 0.5f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = m;
        }
    }
}
