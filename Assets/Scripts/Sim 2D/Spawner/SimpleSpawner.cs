using UnityEngine;


namespace Simulation2D
{
    public class SimpleSpawner : Spawner
    {
        [Header("Spawner")]
        public int particlesPerAxis;

        public override SpawnData Spawn()
        {
            int numParticles = particlesPerAxis * particlesPerAxis;
            Vector2[] particlePositions = new Vector2[numParticles];
            Vector2[] particleVelocities = new Vector2[numParticles];

            int i = 0;
            for (int x = 0; x < particlesPerAxis; x++)
            {
                float px = (float)x / (particlesPerAxis - 1) - 0.5f;
                for (int y = 0; y < particlesPerAxis; y++)
                {
                    float py = (float)y / (particlesPerAxis - 1) - 0.5f;
                    Vector3 pos3d = transform.TransformPoint(px, py, 0);
                    particlePositions[i] = new(pos3d.x, pos3d.y);
                    particleVelocities[i] = Vector2.zero;
                    i++;
                }
            }

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
            Gizmos.DrawWireCube(Vector2.zero, Vector2.one);
            Gizmos.matrix = m;
        }
    }
}
