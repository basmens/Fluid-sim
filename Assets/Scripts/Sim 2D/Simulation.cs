using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

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
        [Range(0, 1)] public float collisionDampening = 0.95f;
        public float smoothingRadius = 0.02f;
        public float pressureConstant;
        public float targetDensity;
        public float viscosity;

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
            if (paused || smoothingRadius < 0.01) return;

            float maxDt = maxTimeStepFPS > 0 ? 1f / maxTimeStepFPS : float.MaxValue;
            float dt = Mathf.Min(Time.deltaTime, maxDt) / iterationsPerFrame;
            for (int i = 0; i < iterationsPerFrame; i++)
            {
                IterateSimulation(dt);
            }
            if (pauseNextFrame) paused = true;
        }

        public void IterateSimulation(float dt)
        {
            Parallel.For(0, NumParticles, i =>
            {
                Densities[i] = CalculateDensity(ref Positions[i]);
            });

            Parallel.For(0, NumParticles, i =>
            {
                Vector2 force = CalculatePressureForce(i);
                force += CalculateViscosityForce(i);
                force /= Densities[i];

                force += gravity;
                Velocities[i] += force * dt;
            });

            for (int i = 0; i < NumParticles; i++)
            {
                Positions[i] += Velocities[i] * dt;
                HandleBoundaryCollision(ref Positions[i], ref Velocities[i]);
            }
        }

        void HandleBoundaryCollision(ref Vector2 pos, ref Vector2 vel)
        {
            pos = transform.InverseTransformPoint(pos);
            vel = transform.InverseTransformDirection(vel);
            if (Mathf.Abs(pos.x) > 0.5f)
            {
                pos.x = 0.5f * Mathf.Sign(pos.x);
                vel.x *= -collisionDampening;
            }
            if (Mathf.Abs(pos.y) > 0.5f)
            {
                pos.y = 0.5f * Mathf.Sign(pos.y);
                vel.y *= -collisionDampening;
            }
            pos = transform.TransformPoint(pos);
            vel = transform.TransformDirection(vel);
        }

        public float DensityToPressure(float density)
        {
            return pressureConstant * (density - targetDensity);
        }

        public float CalculateDensity(ref Vector2 pos)
        {
            float density = 0;
            for (int i = 0; i < NumParticles; i++)
            {
                float distance = (Positions[i] - pos).magnitude;
                float weight = Kernels.DensityKernel(distance, smoothingRadius);
                density += weight * Masses[i];
            }
            return density;
        }

        public Vector2  CalculatePressureForce(int i)
        {
            Vector2 force = Vector2.zero;
            float pressureI = DensityToPressure(Densities[i]);
            for (int j = 0; j < NumParticles; j++)
            {
                if (i == j) continue;

                Vector2 dir = Positions[j] - Positions[i];
                float distance = dir.magnitude;
                dir = (distance == 0) ? new(Mathf.Cos(i + j), Mathf.Sin(i + j)) : dir / distance;

                float pressure = (pressureI + DensityToPressure(Densities[j])) / 2;
                float weight = Kernels.DensityKernelSlope(distance, smoothingRadius);
                force += weight * pressure * dir * Masses[j] / Densities[j];
            }
            return force;
        }
        
        public Vector2  CalculateViscosityForce(int i)
        {
            Vector2 force = Vector2.zero;
            for (int j = 0; j < NumParticles; j++)
            {
                if (i == j) continue;

                float distance = (Positions[j] - Positions[i]).magnitude;
                Vector2 diffVel = Velocities[j] - Velocities[i];
                float weight = Kernels.ViscosityKernel(distance, smoothingRadius);
                force += weight * diffVel * Masses[j] / Densities[j];
            }
            return force * viscosity;
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
