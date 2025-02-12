using System;
using System.Threading.Tasks;
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
        [Range(3, 13)] public int spatialLookupSize = 10;

        public int NumParticles => Positions.Length;
        public Vector2[] Positions { get; private set; }
        public Vector2[] Velocities { get; private set; }
        public float[] Masses { get; private set; }

        public Vector2Int[] SpatialHashes { get; private set; }
        public int[] SpatialLookup { get; private set; }

        public float[] Densities { get; private set; }
        public Vector2[] PressureForces { get; private set; }

        Vector2[] sortingPositionsTemp;
        Vector2[] sortingVelocitiesTemp;
        float[] sortingMassesTemp;

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

            SpatialHashes = new Vector2Int[NumParticles];
            SpatialLookup = new int[1 << spatialLookupSize];

            Densities = new float[NumParticles];
            PressureForces = new Vector2[NumParticles];

            sortingPositionsTemp = new Vector2[NumParticles];
            sortingVelocitiesTemp = new Vector2[NumParticles];
            sortingMassesTemp = new float[NumParticles];

            UpdateSettings();
            IterateSimulation(0);
        }

        void UpdateSettings()
        {
            needsUpdate = false;

            if (SpatialLookup.Length != 1 << spatialLookupSize)
                SpatialLookup = new int[1 << spatialLookupSize];
        }

        void Update()
        {
            HandleInputs();
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

            // Updated algorithm of the SPH Tutorial paper from 2019, algorithm 1
            // Apply non-pressure forces on velocities
            DoSpatialHashing();
            ComputeDensities();
            ApplyNonPressureForces(dt);

            // Apply pressure forces on velocities
            PredictDensities(0.03f);
            ComputePressureForces();
            ApplyPressureForces(dt);
            UpdatePositions(dt);

            iterateSimulationProfilerMarker.End();
        }

        void ComputeDensities()
        {
            Profiler.BeginSample("Compute densities");
            Parallel.For(0, NumParticles, i =>
            {
                Densities[i] = ComputeDensity(ref Positions[i]);
            });
            Profiler.EndSample();
        }

        void PredictDensities(float dt)
        {
            Profiler.BeginSample("Compute densities");
            Parallel.For(0, NumParticles, i =>
            {
                Densities[i] *= 1 - dt * ComputeVelocityDivergence(i);
            });
            Profiler.EndSample();
        }

        void ApplyNonPressureForces(float dt)
        {
            Profiler.BeginSample("Apply external forces");
            Parallel.For(0, NumParticles, i =>
            {
                Vector2 nonPressureForceDivMass = ComputeExternalForceDivMass(i);
                nonPressureForceDivMass += viscosity * ComputeVelocityLaplacian(i); ;
                Velocities[i] += nonPressureForceDivMass * dt;
            });
            Profiler.EndSample();
        }

        void ComputePressureForces()
        {
            Profiler.BeginSample("Compute pressure forces");
            Parallel.For(0, NumParticles, i =>
            {
                PressureForces[i] = -ComputePressureGradient(i) / Densities[i];
            });
            Profiler.EndSample();
        }

        void ApplyPressureForces(float dt)
        {
            Profiler.BeginSample("Apply pressure forces");
            Parallel.For(0, NumParticles, i =>
            {
                Velocities[i] += PressureForces[i] / Masses[i] * dt;
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

        void DoSpatialHashing()
        {
            // Spatial hashing follows the algorithm from Sebastian Lague's video
            // Generate spatial grid
            Profiler.BeginSample("Generate spatial hashes");
            Parallel.For(0, NumParticles, i =>
            {
                SpatialHashes[i].x = SpatialGridHelper.CalcWrappedHash(Positions[i], smoothingRadius, spatialLookupSize);
                SpatialHashes[i].y = i;
            });
            Profiler.EndSample();

            Profiler.BeginSample("Sort spatial hashes");
            // Array.Sort(SpatialHashes, (a, b) => a.x - b.x);
            Array.Sort(SpatialHashes, (a, b) => a.x == b.x ? a.y - b.y : a.x - b.x); // Easier debugging
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
                int originIndex = SpatialHashes[i].y;
                sortingPositionsTemp[i] = Positions[originIndex];
                sortingVelocitiesTemp[i] = Velocities[originIndex];
                sortingMassesTemp[i] = Masses[originIndex];
            });
            (Positions, sortingPositionsTemp) = (sortingPositionsTemp, Positions);
            (Velocities, sortingVelocitiesTemp) = (sortingVelocitiesTemp, Velocities);
            (Masses, sortingMassesTemp) = (sortingMassesTemp, Masses);
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

        public Vector2 ComputeExternalForceDivMass(int i)
        {
            return gravity;
        }

        public float DensityToPressure(float density)
        {
            return pressureConstant * Mathf.Max(density - targetDensity, 0);
        }

        public float SymmetrizeQuantity(float densityI, float quantityI, float densityJ, float quantityJ)
        {
            return quantityI / densityI * densityJ + quantityJ / densityJ * densityI;
        }

        public float ComputeDensity(ref Vector2 pos)
        {
            float density = 0;
            foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
            {
                int wrappedHash = SpatialGridHelper.CalcWrappedHash(pos, neighbour, smoothingRadius, spatialLookupSize);
                for (int i = SpatialLookup[wrappedHash]; i < NumParticles && SpatialHashes[i].x == wrappedHash; i++)
                {
                    float distance = (Positions[i] - pos).magnitude;
                    float weight = Kernels.SpikyKernel(distance, smoothingRadius);
                    density += Masses[i] * weight;
                }
            }
            return density;
        }

        public Vector2 ComputePressureGradient(int i)
        {
            Vector2 gradient = Vector2.zero;
            float densityI = Densities[i];
            float pressureI = DensityToPressure(densityI);

            foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
            {
                int wrappedHash = SpatialGridHelper.CalcWrappedHash(Positions[i], neighbour, smoothingRadius, spatialLookupSize);
                for (int j = SpatialLookup[wrappedHash]; j < NumParticles && SpatialHashes[j].x == wrappedHash; j++)
                {
                    if (i == j) continue;

                    Vector2 dir = Positions[j] - Positions[i];
                    float distance = dir.magnitude;
                    dir = (distance == 0) ? new(Mathf.Cos(i + j), Mathf.Sin(i + j)) : dir / distance;

                    float symmetricPressure = SymmetrizeQuantity(densityI, pressureI, Densities[j], DensityToPressure(Densities[j]));
                    float weightSlope = Kernels.SpikyKernelSlope(distance, smoothingRadius);
                    gradient += Masses[j] / Densities[j] * symmetricPressure * weightSlope * dir;
                }
            }
            return gradient;
        }

        public float ComputeVelocityDivergence(int i)
        {
            float divergence = 0;

            foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
            {
                int wrappedHash = SpatialGridHelper.CalcWrappedHash(Positions[i], neighbour, smoothingRadius, spatialLookupSize);
                for (int j = SpatialLookup[wrappedHash]; j < NumParticles && SpatialHashes[j].x == wrappedHash; j++)
                {
                    if (i == j) continue;

                    Vector2 dir = Positions[j] - Positions[i];
                    float distance = dir.magnitude;
                    dir = (distance == 0) ? new(Mathf.Cos(i + j), Mathf.Sin(i + j)) : dir / distance;

                    float weightSlope = Kernels.SpikyKernelSlope(distance, smoothingRadius);
                    divergence += Masses[j] / Densities[j] * weightSlope * Vector2.Dot(Velocities[j], dir);
                }
            }

            return divergence;
        }

        public Vector2 ComputeVelocityLaplacian(int i)
        {
            Vector2 laplacian = Vector2.zero;

            foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
            {
                int wrappedHash = SpatialGridHelper.CalcWrappedHash(Positions[i], neighbour, smoothingRadius, spatialLookupSize);
                for (int j = SpatialLookup[wrappedHash]; j < NumParticles && SpatialHashes[j].x == wrappedHash; j++)
                {
                    if (i == j) continue;

                    Vector2 velDiff = Velocities[i] - Velocities[j];
                    float distance = Mathf.Max((Positions[j] - Positions[i]).magnitude, 0.0001f);
                    float weightSlope = Kernels.RoundedKernelSlope(distance, smoothingRadius);
                    float weight = 2 * weightSlope / distance;
                    laplacian += Masses[j] / Densities[j] * weight * velDiff;
                }
            }
            return -laplacian;
        }

        // bool prevDown;
        void HandleInputs()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                pauseNextFrame = !paused;
                paused = !paused;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                paused = false;
                pauseNextFrame = true;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                timeMultiplier *= Mathf.Sqrt(10);
                timeMultiplier = Mathf.Clamp(timeMultiplier, 0.01f, 10);
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                timeMultiplier /= Mathf.Sqrt(10);
                timeMultiplier = Mathf.Clamp(timeMultiplier, 0.01f, 10);
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                Initialize();
            }


            // if (Input.GetMouseButton(0) && !prevDown)
            // {
            //     Vector2 mousePos = Input.mousePosition;
            //     Vector2 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

            //     float angle = Random.Range(-0.6f, 0.6f) + Mathf.Atan2(gravity.y, gravity.x);
            //     Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 20;

            //     int index = Random.Range(0, NumParticles);
            //     Positions[index] = worldPos;
            //     Velocities[index] = vel;
            //     Masses[index] = 100;
            // }
            // prevDown = Input.GetMouseButton(0);
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
