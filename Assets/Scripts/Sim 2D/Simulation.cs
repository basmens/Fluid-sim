using System;
using UnityEngine;
using UnityEngine.UI;

namespace Simulation2D
{
    public class Simulation : MonoBehaviour
    {
        [Header("Init")]
        public Spawner spawner;
        public SimulationRenderer simRenderer;

        [Header("Time step")]
        public float maxTimeStepFPS = 60;
        public float iterationsPerFrame = 3;
        public bool paused = false;
        public bool pauseNextFrame = false;

        [Header("Simulation")]
        public Vector2 gravity = new(0, -9.81f);

        public int NumParticles => Particles.Length;
        public Particle[] Particles { get; private set; }

        void Start()
        {
            Initialize();
        }

        void Initialize()
        {
            Spawner.SpawnData spawnData = spawner.Spawn();
            Particles = spawnData.particles;
            simRenderer.Init(this);
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
        }

        void IterateSimulation(float dt)
        {
            for (int i = 0; i < NumParticles; i++)
            {
                ref Particle p = ref Particles[i];
                ref Vector2 pos = ref p.position;
                ref Vector2 vel = ref p.velocity;

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

    public struct Particle
    {
        public Vector2 position;
        public Vector2 velocity;
        public float mass;

        public Particle(Vector2 position, Vector2 velocity, float mass)
        {
            this.position = position;
            this.velocity = velocity;
            this.mass = mass;
        }
    }
}
