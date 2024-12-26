using System;
using UnityEngine;

namespace Simulation2D
{
    public class Simulation : MonoBehaviour
    {
        [Header("Init")]
        public Spawner spawner;
        public GameObject particlePrefab;

        [Header("Time step")]
        public float maxTimeStepFPS = 60;
        public float iterationsPerFrame = 3;
        public bool paused = false;
        public bool pauseNextFrame = false;

        [Header("Simulation")]
        public Vector2 gravity = new(0, -9.81f);

        int numParticles;
        Vector2[] particlePositions;
        Vector2[] particleVelocities;
        GameObject[] particles;
        Vector2 Size => transform.localScale;

        void Start()
        {
            Initialize();
        }

        void Initialize()
        {
            Spawner.SpawnData spawnData = spawner.Spawn();
            particlePositions = spawnData.position;
            particleVelocities = spawnData.velocity;
            numParticles = particlePositions.Length;

            particles = new GameObject[numParticles];

            for (int i = 0; i < numParticles; i++)
            {
                GameObject particle = Instantiate(particlePrefab);
                particle.transform.localPosition = particlePositions[i];
                particles[i] = particle;
            }
        }

        void Update()
        {
            if (!paused)
            {
                float maxDt = maxTimeStepFPS > 0 ? 1f / maxTimeStepFPS : float.MaxValue;
                float dt = Mathf.Min(Time.deltaTime, maxDt) / iterationsPerFrame;
                for (int i = 0; i < iterationsPerFrame; i++)
                {
                    IterateSimulation(dt);
                }
            }
            if (pauseNextFrame) paused = true;

            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].transform.localPosition = particlePositions[i];
            }
        }

        void IterateSimulation(float dt)
        {
            for (int i = 0; i < particlePositions.Length; i++)
            {
                ref Vector2 pos = ref particlePositions[i];
                ref Vector2 vel = ref particleVelocities[i];

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
