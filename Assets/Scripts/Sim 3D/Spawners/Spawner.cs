using UnityEngine;

namespace Simulation3D
{
    public abstract class Spawner : MonoBehaviour
    {
        public abstract SpawnData Spawn();

        public struct SpawnData
        {
            public Vector3[] position;
            public Vector3[] velocity;
        }
    }
}
