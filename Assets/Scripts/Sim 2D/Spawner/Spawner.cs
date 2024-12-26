using UnityEngine;

namespace Simulation2D
{
    public abstract class Spawner : MonoBehaviour
    {
        public abstract SpawnData Spawn();

        public struct SpawnData
        {
            public Vector2[] position;
            public Vector2[] velocity;
        }
    }
}