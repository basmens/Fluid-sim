using System.Threading.Tasks;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Simulation2D
{
    public class Simulation : MonoBehaviour
    {
        [Header("Init")]
        public Spawner spawner;

        [Header("Time step")]
        public float maxTimeStepPerIteration = 0.005f;
        public float iterationsPerFrame = 3;
        [Range(0.01f, 10f)] public float timeMultiplier = 1;
        public bool paused = false;
        public bool pauseNextFrame = false;

        [Header("Simulation")]
        public Vector2 gravity = new(0, -9.81f);
        [Range(0, 1)] public float collisionDampening = 0.95f;
        public float smoothingRadius = 0.02f;
        public float pressureConstant;
        public float targetDensity;
        public float viscosity;

        [Header("Optimization")]
        [Range(0, 13)] public int spatialLookupSize = 10;

        public int NumParticles => Positions.Length;
        public Vector2[] Positions { get; private set; }
        public Vector2[] Velocities { get; private set; }
        public float[] Masses { get; private set; }

        public Vector2[] PosDependentAccelerations { get; private set; }
        public float[] Densities { get; private set; }
        public Vector2Int[] SpatialHashes { get; private set; }
        public int[] SpatialLookup { get; private set; }

        Vector2[] sortingPositionsTemp;
        Vector2[] sortingVelocitiesTemp;
        float[] sortingMassesTemp;
        Vector2[] sortingPosDependentAccelerationsTemp;

        bool needsUpdate;

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

            PosDependentAccelerations = new Vector2[NumParticles];
            Densities = new float[NumParticles];
            SpatialHashes = new Vector2Int[NumParticles];

            sortingPositionsTemp = new Vector2[NumParticles];
            sortingVelocitiesTemp = new Vector2[NumParticles];
            sortingMassesTemp = new float[NumParticles];
            sortingPosDependentAccelerationsTemp = new Vector2[NumParticles];

            UpdateSettings();
            CalculateDensities();
        }

        void UpdateSettings()
        {
            needsUpdate = false;

            SpatialLookup = new int[1 << spatialLookupSize];
            DoSpatialHashing();
        }

        void Update()
        {
            if (needsUpdate) UpdateSettings();
            if (paused || smoothingRadius < 0.01) return;

            float dt = Mathf.Min(Time.deltaTime * timeMultiplier / iterationsPerFrame, maxTimeStepPerIteration);
            for (int i = 0; i < iterationsPerFrame; i++)
            {
                IterateSimulation(dt);
            }
            if (pauseNextFrame) paused = true;
        }

        private static readonly ProfilerMarker iterateSimulationProfilerMarker = new("IterateSimulation");
        public void IterateSimulation(float dt)
        {
            iterateSimulationProfilerMarker.Begin();
            // Step simulation using leapfrog integration
            // https://en.wikipedia.org/wiki/Leapfrog_integration
            UpdateVelocities(dt);
            UpdatePositions(dt);

            DoSpatialHashing();

            CalculateDensities();
            CalculateAccelerations();

            UpdateVelocities(dt);

            iterateSimulationProfilerMarker.End();
        }

        void CalculateAccelerations()
        {
            Profiler.BeginSample("Calculate position dependent accelerations");
            Parallel.For(0, NumParticles, i =>
            {
                ref Vector2 a = ref PosDependentAccelerations[i];
                a = CalculatePressureForce(i);
                a /= Densities[i];
                a += gravity;
            });
            Profiler.EndSample();
        }

        void UpdateVelocities(float dt)
        {
            Profiler.BeginSample("Update velocities");
            Parallel.For(0, NumParticles, i =>
            {
                Vector2 velDependentAccelerations = CalculateViscosityForce(i);
                velDependentAccelerations /= Densities[i];
                Velocities[i] += (PosDependentAccelerations[i] + velDependentAccelerations) * dt / 2;
            });
            Profiler.EndSample();
        }

        void UpdatePositions(float dt)
        {
            Profiler.BeginSample("Update positions");

            // Pre compute world to and from local space matrices
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            Matrix4x4 LocalToWorld = transform.localToWorldMatrix;

            Parallel.For(0, NumParticles, i =>
            {
                ref Vector2 p = ref Positions[i];
                ref Vector2 v = ref Velocities[i];
                p += v * dt;
                HandleBoundaryCollision(ref p, ref v, ref worldToLocal, ref LocalToWorld);
            });
            Profiler.EndSample();
        }

        void CalculateDensities()
        {
            Profiler.BeginSample("Calculate densities");
            Parallel.For(0, NumParticles, i =>
            {
                Densities[i] = CalculateDensity(ref Positions[i]);
            });
            Profiler.EndSample();
        }

        void DoSpatialHashing()
        {
            // Generate spatial grid
            Profiler.BeginSample("Generate spatial hashes");
            Parallel.For(0, NumParticles, i =>
            {
                SpatialHashes[i].x = SpatialGridHelper.CalcWrappedHash(Positions[i], smoothingRadius, spatialLookupSize);
                SpatialHashes[i].y = i;
            });
            Profiler.EndSample();

            Profiler.BeginSample("Sort spatial hashes");
            System.Array.Sort(SpatialHashes, (a, b) => a.x - b.x);
            Profiler.EndSample();

            Profiler.BeginSample("Generate spatial lookup");
            Parallel.For(0, NumParticles, i =>
            {
                if (i == 0 || SpatialHashes[i].x != SpatialHashes[i - 1].x)
                {
                    SpatialLookup[SpatialHashes[i].x] = i;
                }
            });
            Profiler.EndSample();

            // Sort the positions, velocities and masses arrays according to 
            // the spatial hash array in order to improve memory locality
            Profiler.BeginSample("Sort positions, velocities and masses");
            Parallel.For(0, NumParticles, i =>
            {
                sortingPositionsTemp[i] = Positions[SpatialHashes[i].y];
                sortingVelocitiesTemp[i] = Velocities[SpatialHashes[i].y];
                sortingMassesTemp[i] = Masses[SpatialHashes[i].y];
                sortingPosDependentAccelerationsTemp[i] = PosDependentAccelerations[SpatialHashes[i].y];
            });
            (Positions, sortingPositionsTemp) = (sortingPositionsTemp, Positions);
            (Velocities, sortingVelocitiesTemp) = (sortingVelocitiesTemp, Velocities);
            (Masses, sortingMassesTemp) = (sortingMassesTemp, Masses);
            (PosDependentAccelerations, sortingPosDependentAccelerationsTemp) = (sortingPosDependentAccelerationsTemp, PosDependentAccelerations);
            Profiler.EndSample();
        }

        void HandleBoundaryCollision(ref Vector2 pos, ref Vector2 vel, ref Matrix4x4 worldToLocal, ref Matrix4x4 LocalToWorld)
        {
            pos = worldToLocal.MultiplyPoint3x4(pos);
            vel = worldToLocal.MultiplyVector(vel);
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
            pos = LocalToWorld.MultiplyPoint3x4(pos);
            vel = LocalToWorld.MultiplyVector(vel);
        }

        public float DensityToPressure(float density)
        {
            return pressureConstant * (density - targetDensity);
        }

        public float CalculateDensity(ref Vector2 pos)
        {
            float density = 0;
            foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
            {
                int wrappedHash = SpatialGridHelper.CalcWrappedHash(pos, neighbour, smoothingRadius, spatialLookupSize);
                for (int i = SpatialLookup[wrappedHash]; i < NumParticles && SpatialHashes[i].x == wrappedHash; i++)
                {
                    float distance = (Positions[i] - pos).magnitude;
                    float weight = Kernels.DensityKernel(distance, smoothingRadius);
                    density += weight * Masses[i];
                }
            }
            return density;
        }

        public Vector2 CalculatePressureForce(int i)
        {
            Vector2 force = Vector2.zero;
            float pressureI = DensityToPressure(Densities[i]);

            foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
            {
                int wrappedHash = SpatialGridHelper.CalcWrappedHash(Positions[i], neighbour, smoothingRadius, spatialLookupSize);
                for (int j = SpatialLookup[wrappedHash]; j < NumParticles && SpatialHashes[j].x == wrappedHash; j++)
                {
                    if (i == j) continue;

                    Vector2 dir = Positions[j] - Positions[i];
                    float distance = dir.magnitude;
                    dir = (distance == 0) ? new(Mathf.Cos(i + j), Mathf.Sin(i + j)) : dir / distance;

                    float pressure = (pressureI + DensityToPressure(Densities[j])) / 2;
                    float weight = Kernels.DensityKernelSlope(distance, smoothingRadius);
                    force += weight * pressure * dir * Masses[j] / Densities[j];
                }
            }
            return force;
        }

        public Vector2 CalculateViscosityForce(int i)
        {
            Vector2 force = Vector2.zero;

            foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
            {
                int wrappedHash = SpatialGridHelper.CalcWrappedHash(Positions[i], neighbour, smoothingRadius, spatialLookupSize);
                for (int j = SpatialLookup[wrappedHash]; j < NumParticles && SpatialHashes[j].x == wrappedHash; j++)
                {
                    if (i == j) continue;

                    float distance = (Positions[j] - Positions[i]).magnitude;
                    Vector2 diffVel = Velocities[j] - Velocities[i];
                    float weight = Kernels.ViscosityKernel(distance, smoothingRadius);
                    force += weight * diffVel * Masses[j] / Densities[j];
                }
            }
            return force * viscosity;
        }

        void OnValidate()
        {
            needsUpdate = true;
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
