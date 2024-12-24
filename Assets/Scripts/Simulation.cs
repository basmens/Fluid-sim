using UnityEngine;

public class Simulation : MonoBehaviour
{
    [Header("Init")]
    public Spawner spawner;
    public GameObject particlePrefab;
    public Vector3 size;

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
        // Debug.Log(particlePositions.Length);
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
