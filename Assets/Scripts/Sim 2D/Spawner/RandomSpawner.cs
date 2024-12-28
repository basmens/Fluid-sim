using UnityEngine;


namespace Simulation2D
{
    public class RandomSpawner : Spawner
    {
        [Header("Editor")]
        public bool drawGizmos = true;

        [Header("Spawner")]
        public int numParticles;
        public float mass = 1f;

        public override SpawnData Spawn()
        {
            Particle[] particles = new Particle[numParticles];

            for (int i = 0; i < numParticles; i++)
            {
                float x = Random.Range(-0.5f, 0.5f);
                float y = Random.Range(-0.5f, 0.5f);
                Vector2 pos = transform.TransformPoint(new(x, y));
                particles[i] = new Particle(pos, Vector2.zero, mass);
            }

            return new SpawnData
            {
                particles = particles
            };
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
