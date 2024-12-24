using UnityEngine;

public class Simulation : MonoBehaviour
{
    [Header("Init")]
    public int particlesPerAxis;
    public GameObject particlePrefab;

    Vector3[] particlePositions;
    Vector3[] particleVelocities;
    GameObject[] particles;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        int numParticles = particlesPerAxis * particlesPerAxis * particlesPerAxis;
        particlePositions = new Vector3[numParticles];
        particleVelocities = new Vector3[numParticles];
        particles = new GameObject[numParticles];

        int i = 0;
        for (int x = 0; x < particlesPerAxis; x++)
        {
            float px = (float) x / (particlesPerAxis - 1);
            for (int y = 0; y < particlesPerAxis; y++)
            {
                float py = (float) y / (particlesPerAxis - 1);
                for (int z = 0; z < particlesPerAxis; z++)
                {
                    float pz = (float) z / (particlesPerAxis - 1);
                    particlePositions[i] = new Vector3(px, py, pz);
                    particleVelocities[i] = Vector3.zero;

                    GameObject particle = Instantiate(particlePrefab, gameObject.transform);
                    particle.transform.localPosition = particlePositions[i];
                    particles[i] = particle;

                    i++;
                }
            }
        }
    }

    void Update()
    {
        Debug.Log(particlePositions.Length);
    }
}
