using UnityEngine;

public class SimpleSpawner : Spawner
{
    [Header("Spawner")]
    public int particlesPerAxis;
    public Vector3 center;
    public Vector3 size;

    public override SpawnData Spawn()
    {
        int numParticles = particlesPerAxis * particlesPerAxis * particlesPerAxis;
        Vector3[] particlePositions = new Vector3[numParticles];
        Vector3[] particleVelocities = new Vector3[numParticles];

        int i = 0;
        for (int x = 0; x < particlesPerAxis; x++)
        {
            float tx = (float)x / (particlesPerAxis - 1);
            float px = (tx - 0.5f) * size.x + center.x;
            for (int y = 0; y < particlesPerAxis; y++)
            {
                float ty = (float)y / (particlesPerAxis - 1);
                float py = (ty - 0.5f) * size.y + center.y;
                for (int z = 0; z < particlesPerAxis; z++)
                {
                    float tz = (float)z / (particlesPerAxis - 1);
                    float pz = (tz - 0.5f) * size.z + center.z;
                    particlePositions[i] = new Vector3(px, py, pz);
                    particleVelocities[i] = Vector3.zero;
                    i++;
                }
            }
        }

        return new SpawnData {
            position = particlePositions,
            velocity = particleVelocities
        };
    }

    void OnDrawGizmos()
    {
        // Draw Bounds
        var m = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1, 0, 0.5f, 0.5f);
        Gizmos.DrawWireCube(center, size);
        Gizmos.matrix = m;
    }
}
