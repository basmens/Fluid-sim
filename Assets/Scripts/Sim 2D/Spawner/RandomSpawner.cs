using UnityEngine;


namespace Simulation2D
{
    public class RandomSpawner : Spawner
    {
        [Header("Editor")]
        public bool drawGizmos = true;

        [Header("Spawner")]
        public int numParticles;
        public float totalMass = 1f;

        public override SpawnData Spawn()
        {
            Vector2[] positions = new Vector2[numParticles];
            Vector2[] velocities = new Vector2[numParticles];
            float[] masses = new float[numParticles];

            float mass = totalMass / numParticles;
            for (int i = 0; i < numParticles; i++)
            {
                float x = Random.Range(-0.5f, 0.5f);
                float y = Random.Range(-0.5f, 0.5f);
                positions[i] = transform.TransformPoint(new(x, y));
                velocities[i] = Vector2.zero;
                masses[i] = mass;
            }

            return new(positions, velocities, masses);
        }

        void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Draw Bounds
            var m = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1, 0, 0.5f, 0.5f);
            Gizmos.DrawWireCube(Vector2.zero, Vector2.one);
            Gizmos.matrix = m;
        }
    }
}
