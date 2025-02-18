using System.Linq;
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
        public float iterationsPerFrame = 3;
        public float scalarCFLCondition = 0.4f;
        [Range(0.01f, 10f)] public float timeMultiplier = 1;
        public bool paused = false;
        public bool pauseNextFrame = false;

        [Header("Simulation")]
        public Vector2 gravity = new(0, -9.81f);
        [Range(0, 1)] public float collisionDampening = 0.95f;
        public float smoothingRadius = 0.02f;
        public float maxDensityDivergence = 0.1f;
        public float maxVelocityDivergence = 0.1f;
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
        public Vector2[] AccelerationsNonP { get; private set; }
        public float[] DfsphFactors { get; private set; }
        public float[] Pressures { get; private set; }
        public float[] DensityPredictions { get; private set; }
        public float[] TimeDerivativeDensities { get; private set; }

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
            AccelerationsNonP = new Vector2[NumParticles];
            DfsphFactors = new float[NumParticles];
            Pressures = new float[NumParticles];
            DensityPredictions = new float[NumParticles];
            TimeDerivativeDensities = new float[NumParticles];

            sortingPositionsTemp = new Vector2[NumParticles];
            sortingVelocitiesTemp = new Vector2[NumParticles];
            sortingMassesTemp = new float[NumParticles];

            UpdateSettings();
            UpdateSpatialHashing();
            UpdateDensities();
            UpdateDfsphFactors();
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

            float dt = Time.deltaTime * timeMultiplier / iterationsPerFrame;
            for (int i = 0; i < iterationsPerFrame; i++)
            {
                IterateSimulation(dt);
            }
            if (pauseNextFrame) paused = true;
        }

        private static readonly ProfilerMarker iterateSimulationProfilerMarker = new("IterateSimulation");
        public void IterateSimulation(float maxDt)
        {
            iterateSimulationProfilerMarker.Begin();

            // Algorithm of the SPH Tutorial paper from 2019, algorithm 6
            // Do non pressure bits and dt
            ComputeAccelerationsNonP();
            float cflDt = ComputeCFLConditionDt();
            float dt = Mathf.Max(Mathf.Min(cflDt, maxDt), 0.001f);
            dt = 0.001f;
            ApplyAccelerationsNonP(dt);

            // Apply constant density solver (fix density divergence)
            ApplyConstantDensitySolver(dt);
            UpdatePositions(dt);
            UpdateSpatialHashing();
            UpdateDensities();
            UpdateDfsphFactors();

            // Apply divergence free solver (fix velocity divergence)
            ApplyDivergenceFreeSolver(dt);

            iterateSimulationProfilerMarker.End();
        }

        void UpdateDensities()
        {
            Profiler.BeginSample("Compute densities");
            Parallel.For(0, NumParticles, i =>
            {
                Densities[i] = ComputeDensity(ref Positions[i]);
            });
            Debug.Log($"Average density: {Densities.Average()}");
            Profiler.EndSample();
        }

        float ComputeCFLConditionDt()
        {
            Profiler.BeginSample("Compute Courant-Friedrichs-Lewy condition max dt");
            float cflDt = float.MaxValue;
            for (int i = 0; i < NumParticles; i++)
            {
                // dt = scaler * radius / maxVel
                // dt = scaler * radius / (v + a * dt)
                // a * dt^2 + v * dt - scaler * radius = 0
                // dt = (-v + sqrt(v^2 - 4 * a * (scaler * radius))) / 2a
                float v = Velocities[i].magnitude;
                float a = Mathf.Max(AccelerationsNonP[i].magnitude, 0.0001f);
                float determinant = Mathf.Max(v * v - 4 * a * (scalarCFLCondition * smoothingRadius), 0);
                float dt = (-Velocities[i].magnitude + Mathf.Sqrt(determinant)) / 2 / a;
                if (dt < cflDt) cflDt = dt;
            }
            Profiler.EndSample();
            return cflDt;
        }

        void ComputeAccelerationsNonP()
        {
            Profiler.BeginSample("Compute accelerations non pressure");
            Parallel.For(0, NumParticles, i =>
            {
                ref Vector2 a = ref AccelerationsNonP[i];
                a = ComputeAccelerationsExternal(i);
                a += viscosity * ComputeVelocityLaplacian(i);
            });
            Profiler.EndSample();
        }

        void ApplyAccelerationsNonP(float dt)
        {
            Profiler.BeginSample("Apply accelerations non pressure");
            Parallel.For(0, NumParticles, i =>
            {
                Velocities[i] += AccelerationsNonP[i] * dt;
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

        void UpdateDfsphFactors()
        {
            Profiler.BeginSample("Update dfsph factors");
            Parallel.For(0, NumParticles, i =>
            {
                DfsphFactors[i] = ComputeDfsphFactor(i) * 0.65f; // This 0.65f shouldn't be there, but otherwise the solvers diverge
            });
            Profiler.EndSample();
        }

        public void ApplyConstantDensitySolver(float dt)
        {
            Profiler.BeginSample("Apply constant density solver");

            int tmp = 0;
            for (int iter = 0; iter < 1000 && (iter < 2 || Mathf.Abs(DensityPredictions.Average() / targetDensity - 1) > maxDensityDivergence); iter++)
            {
                Parallel.For(0, NumParticles, i =>
                {
                    DensityPredictions[i] = Densities[i] + dt * ComputeTimeDerivativeDensity(i);
                    Pressures[i] = (DensityPredictions[i] - targetDensity) / (dt * dt) * DfsphFactors[i];
                });
                Parallel.For(0, NumParticles, i =>
                {
                    Velocities[i] -= dt * ComputePressureGradient(i);
                });
                tmp = iter;
            }
            Debug.Log($"Density divergence: {Mathf.Abs(DensityPredictions.Average() / targetDensity - 1)}   -   {tmp}");

            Profiler.EndSample();
        }

        public void ApplyDivergenceFreeSolver(float dt)
        {
            Profiler.BeginSample("Apply divergence free solver");

            int tmp = 0;
            for (int iter = 0; iter < 1000 && (iter < 1 || Mathf.Abs(TimeDerivativeDensities.Average()) > maxDensityDivergence); iter++)
            {
                Parallel.For(0, NumParticles, i =>
                {
                    TimeDerivativeDensities[i] = ComputeTimeDerivativeDensity(i);
                    Pressures[i] = TimeDerivativeDensities[i] / dt * DfsphFactors[i];
                });
                Parallel.For(0, NumParticles, i =>
                {
                    Velocities[i] -= dt * ComputePressureGradient(i);
                });
            }
            Debug.Log($"Velocity divergence: {Mathf.Abs(TimeDerivativeDensities.Average())}   -   {tmp}");

            Profiler.EndSample();
        }

        void UpdateSpatialHashing()
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
            System.Array.Sort(SpatialHashes, (a, b) => a.x == b.x ? a.y - b.y : a.x - b.x); // Easier debugging
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

            Parallel.For(0, NumParticles, i =>
            {
                int originIndex = SpatialHashes[i].y;
                sortingMassesTemp[i] = DensityPredictions[originIndex];
            });
            (DensityPredictions, sortingMassesTemp) = (sortingMassesTemp, DensityPredictions);
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

        public Vector2 ComputeAccelerationsExternal(int i)
        {
            return gravity;
        }

        public float SymmetrizeQuantity(float densityI, float quantityI, float densityJ, float quantityJ)
        {
            return quantityI / (densityI * densityI) + quantityJ / (densityJ * densityJ);
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

            foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
            {
                int wrappedHash = SpatialGridHelper.CalcWrappedHash(Positions[i], neighbour, smoothingRadius, spatialLookupSize);
                for (int j = SpatialLookup[wrappedHash]; j < NumParticles && SpatialHashes[j].x == wrappedHash; j++)
                {
                    if (i == j) continue;

                    (float distance, Vector2 dir) = ComputeDistAndDir(i, j);
                    float symmetricPressure = SymmetrizeQuantity(Densities[i], Pressures[i], Densities[j], Pressures[j]);
                    float weightSlope = Kernels.SpikyKernelSlope(distance, smoothingRadius);
                    gradient += Masses[j] * symmetricPressure * weightSlope * dir;
                }
            }
            return gradient;
        }

        public float ComputeTimeDerivativeDensity(int i)
        {
            float derivative = 0;

            foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
            {
                int wrappedHash = SpatialGridHelper.CalcWrappedHash(Positions[i], neighbour, smoothingRadius, spatialLookupSize);
                for (int j = SpatialLookup[wrappedHash]; j < NumParticles && SpatialHashes[j].x == wrappedHash; j++)
                {
                    if (i == j) continue;

                    (float distance, Vector2 dir) = ComputeDistAndDir(i, j);
                    float weightSlope = Kernels.SpikyKernelSlope(distance, smoothingRadius);
                    derivative += Masses[j] * weightSlope * Vector2.Dot(Velocities[i] - Velocities[j], dir);
                }
            }

            return derivative;
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

                    (float distance, Vector2 dir) = ComputeDistAndDir(i, j);
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

        public float ComputeDfsphFactor(int i)
        {
            Vector2 vectorAccumulator = Vector2.zero;
            float floatAccumulator = 0;

            foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
            {
                int wrappedHash = SpatialGridHelper.CalcWrappedHash(Positions[i], neighbour, smoothingRadius, spatialLookupSize);
                for (int j = SpatialLookup[wrappedHash]; j < NumParticles && SpatialHashes[j].x == wrappedHash; j++)
                {
                    if (i == j) continue;

                    (float distance, Vector2 dir) = ComputeDistAndDir(i, j);
                    float weightSlope = Kernels.SpikyKernelSlope(distance, smoothingRadius);
                    vectorAccumulator += Masses[j] * weightSlope * dir;
                    floatAccumulator += Masses[j] * Masses[j] * weightSlope * weightSlope;
                }
            }

            return Densities[i] * Densities[i] / (floatAccumulator + vectorAccumulator.sqrMagnitude);
        }

        public (float, Vector2) ComputeDistAndDir(int i, int j)
        {
            Vector2 dir = Positions[j] - Positions[i];
            float distance = dir.magnitude;
            dir = (distance == 0) ? new(Mathf.Cos(i + j), Mathf.Sin(i + j)) : dir / distance;
            return (distance, dir);
        }

        bool prevDownLeft;
        bool prevDownRight;
        public float pushRadius = 3f;
        public float pushForce = 1f;
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

            // if (Input.GetMouseButton(0) && !prevDownLeft)
            // {
            //     Vector2 mousePos = Input.mousePosition;
            //     Vector2 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

            //     for (int i = 0; i < NumParticles; i++) {
            //         Vector2 dir = Positions[i] - worldPos;
            //         float distance = dir.magnitude;
            //         if (distance > pushRadius) continue;
            //         dir = (distance == 0) ? new(Mathf.Cos(i), Mathf.Sin(i)) : dir / distance;

            //         Velocities[i] = Kernels.SpikyKernel(distance, pushRadius) * dir * pushForce;
            //     }
            // }
            // prevDownLeft = Input.GetMouseButton(0);

            // if (Input.GetMouseButton(1) && !prevDownRight)
            // {
            //     Vector2 mousePos = Input.mousePosition;
            //     Vector2 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

            //     for (int i = 0; i < NumParticles; i++) {
            //         Vector2 dir = Positions[i] - worldPos;
            //         float distance = dir.magnitude;
            //         if (distance > pushRadius) continue;
            //         dir = (distance == 0) ? new(Mathf.Cos(i), Mathf.Sin(i)) : dir / distance;

            //         Velocities[i] = -Kernels.SpikyKernel(distance, pushRadius) * dir * pushForce;
            //     }
            // }
            // prevDownRight = Input.GetMouseButton(1);
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
