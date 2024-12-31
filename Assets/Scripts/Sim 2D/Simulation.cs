using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Simulation2D
{
    public class Simulation : MonoBehaviour
    {
        [Header("Init")]
        public Spawner spawner;

        [Header("Time step")]
        public float maxTimeStepFPS = 60;
        public float iterationsPerFrame = 3;
        public bool paused = false;
        public bool pauseNextFrame = false;

        [Header("Simulation")]
        public Vector2 gravity = new(0, -9.81f);
        public float smoothingRadius = 0.02f;

        public int NumParticles => Positions.Length;
        public Vector2[] Positions { get; private set; }
        public Vector2[] Velocities { get; private set; }
        public float[] Masses { get; private set; }

        public float[] Densities { get; private set; }

        void Start()
        {
            Initialize();
        }

        void Initialize()
        {
            Spawner.SpawnData spawnData = spawner.Spawn();
            Positions = spawnData.positions;
            Velocities = spawnData.velocities;
            Masses = spawnData.masses;

            Densities = new float[NumParticles];
        }

        void Update()
        {
            if (paused)
            {
                IterateSimulation(0);
            }
            else
            {
                float maxDt = maxTimeStepFPS > 0 ? 1f / maxTimeStepFPS : float.MaxValue;
                float dt = Mathf.Min(Time.deltaTime, maxDt) / iterationsPerFrame;
                for (int i = 0; i < iterationsPerFrame; i++)
                {
                    IterateSimulation(dt);
                }
            }
            if (pauseNextFrame) paused = true;
        }

        void IterateSimulation(float dt)
        {
            Parallel.For(0, NumParticles, i => {
                Densities[i] = CalculateDensity(ref Positions[i]);
            });

            for (int i = 0; i < NumParticles; i++)
            {
                ref Vector2 pos = ref Positions[i];
                ref Vector2 vel = ref Velocities[i];

                vel += gravity * dt;
                pos += vel * dt;

                HandleBoundaryCollision(ref pos, ref vel);
            }
        }

        void HandleBoundaryCollision(ref Vector2 pos, ref Vector2 vel)
        {
            pos = transform.InverseTransformPoint(pos);
            vel = transform.InverseTransformDirection(vel);
            if (Math.Abs(pos.x) > 0.5f)
            {
                pos.x = 0.5f * Math.Sign(pos.x);
                vel.x = 0;
            }
            if (Math.Abs(pos.y) > 0.5f)
            {
                pos.y = 0.5f * Math.Sign(pos.y);
                vel.y = 0;
            }
            pos = transform.TransformPoint(pos);
            vel = transform.TransformDirection(vel);
        }

        public float CalculateDensity(ref Vector2 pos) {
            float density = 0;
            for (int i = 0; i < NumParticles; i++) {
                float distance = (Positions[i] - pos).magnitude;
                float weight = Kernels.SquareKernel(distance, smoothingRadius);
                density += weight * Masses[i];
            }
            return density;
        }

        void OnDrawGizmos()
        {
            // Draw Bounds
            var m = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawWireCube(Vector2.zero, Vector2.one);
            Gizmos.matrix = m;
        }
    }
}
