using UnityEngine;

namespace Simulation2D
{
    public abstract class Spawner : MonoBehaviour
    {
        public abstract SpawnData Spawn();

        public struct SpawnData
        {
            public Particle[] particles;
        }
    }
}