using UnityEngine;

namespace Simulation2D
{
    public abstract class Spawner : MonoBehaviour
    {
        public abstract SpawnData Spawn();

        public struct SpawnData
        {
            public Vector2[] positions;
            public Vector2[] velocities;
            public float[] masses;

            public SpawnData(Vector2[] positions, Vector2[] velocities, float[] masses) {
                this.positions = positions;
                this.velocities = velocities;
                this.masses = masses;
            }
        }
    }
}