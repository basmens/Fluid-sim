using UnityEngine;

public class Simulation : MonoBehaviour
{
    [Header("Init")]
    public Spawner spawner;
    public GameObject particlePrefab;
    public Vector3 size;

    [Header("Time step")]
    public float maxTimeStepFPS = 60;
    public float iterationsPerFrame = 3;

    [Header("Simulation")]
    public Vector3 gravity = new Vector3(0, -9.81f, 0);

    Vector3[] particlePositions;
    Vector3[] particleVelocities;
    GameObject[] particles;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        Spawner.SpawnData spawnData = spawner.Spawn();
        particlePositions = spawnData.position;
        particleVelocities = spawnData.velocity;
        int numParticles = particlePositions.Length;

        particles = new GameObject[numParticles];

        for (int i = 0; i < numParticles; i++)
        {
            GameObject particle = Instantiate(particlePrefab, transform);
            particle.transform.localPosition = particlePositions[i];
            particles[i] = particle;
        }
    }

    void Update()
    {
        float maxDt = maxTimeStepFPS > 0 ?  1f / maxTimeStepFPS : float.MaxValue;
        float dt = Mathf.Min(Time.deltaTime, maxDt) / iterationsPerFrame;
        for (int i = 0; i < iterationsPerFrame; i++) {
            IterateSimulation(dt);
        }

        for (int i = 0; i < particles.Length; i++) {
            particles[i].transform.localPosition = particlePositions[i];
        }
    }

    void IterateSimulation(float dt) {
        for (int i = 0; i < particlePositions.Length; i++) {
            ref Vector3 pos = ref particlePositions[i];
            ref Vector3 vel = ref particleVelocities[i];

            vel += gravity * dt;
            pos += vel * dt;

            HandleBoundaryCollision(ref pos, ref vel);
        }
    }

    void HandleBoundaryCollision(ref Vector3 pos, ref Vector3 vel) {
        if (pos.x < -size.x / 2) {
            pos.x = -size.x / 2;
            vel.x = 0;
        }
        if (pos.x > size.x / 2) {
            pos.x = size.x / 2;
            vel.x = 0;
        }
        if (pos.y < -size.y / 2) {
            pos.y = -size.y / 2;
            vel.y = 0;
        }
        if (pos.y > size.y / 2) {
            pos.y = size.y / 2;
            vel.y = 0;
        }
        if (pos.z < -size.z / 2) {
            pos.z = -size.z / 2;
            vel.z = 0;
        }
        if (pos.z > size.z / 2) {
            pos.z = size.z / 2;
            vel.z = 0;
        }
    }

    void OnDrawGizmos()
    {
        // Draw Bounds
        var m = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = m;
    }
}
