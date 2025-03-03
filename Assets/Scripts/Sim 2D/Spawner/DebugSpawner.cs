using System;
using UnityEngine;


namespace Simulation2D
{
    public class DebugSpawner : Spawner
    {
        [Header("Editor")]
        public bool drawGizmos = true;

        [Header("Spawner")]
        public int particlesPerAxis;
        public float totalMass = 100f;
        public int numParticles = 2;
        public Vector2[] positions;
        public Vector2[] velocities;
        public float[] masses;

        public override SpawnData Spawn()
        {
            if (numParticles == 0) return new (new Vector2[0], new Vector2[0], new float[0]);

            Vector2[] posCopy = (Vector2[]) positions.Clone();
            Vector2[] velCopy = (Vector2[]) velocities.Clone();
            float[] massCopy = (float[]) masses.Clone();

            return new(posCopy, velCopy, massCopy);
        }

        void OnValidate() {
            if (numParticles == 0) return;
            if (positions.Length != numParticles) {
                Vector2[] posCopy = positions;
                positions = new Vector2[numParticles];
                Array.Copy(posCopy, positions, Mathf.Min(posCopy.Length, numParticles));
            }
            if (velocities.Length != numParticles) {
                Vector2[] velCopy = velocities;
                velocities = new Vector2[numParticles];
                Array.Copy(velCopy, velocities, Mathf.Min(velCopy.Length, numParticles));
            }
            if (masses.Length != numParticles) {
                float[] massCopy = masses;
                masses = new float[numParticles];
                Array.Copy(massCopy, masses, Mathf.Min(massCopy.Length, numParticles));
            }
        }

        void Start() {
            OnValidate();
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
