using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Simulation2D
{
    public class DebugRenderer : RawImageRenderer
    {
        [Header("Rendering")]
        public TMP_Text debugText;

        [Header("Debug At Sample")]
        public bool useParticlePosition;
        public bool keepParticleOnClick;
        public bool displayParticleIndex;
        public bool displayPosition;
        public bool displayParticleVelocity;
        public bool displayHighestVelocity;
        public float velocityScale = 1;
        public bool displayDensity;
        public float densityMultiplier = 0.01f;
        public bool displayPressureForce;
        public float pressureForceScale = 0.01f;
        public bool displayVelocityLaplacian;
        public float velocityLaplacianScale = 0.01f;

        [Header("Debug Imaging")]
        public DebugView debugView;

        int selectedParticleIndex;

        void LateUpdate()
        {
            SetBackgroundColor(backgroundColor);
            switch (debugView)
            {
                case DebugView.InterpolateProperty:
                    DrawInterpolateProperty();
                    break;
                case DebugView.PropertyToInterpolate:
                    DrawPropertyToInterpolate();
                    break;
                case DebugView.Density:
                case DebugView.DensityAndGradients:
                    DrawDensities();
                    if (debugView == DebugView.DensityAndGradients)
                        goto case DebugView.Density;
                    break;
                case DebugView.DensityGradients:
                    DrawPressureGradients();
                    break;
                case DebugView.VelocityLaplacians:
                    DrawVelocityLaplacians();
                    break;
                case DebugView.SpatialGrid:
                    DrawSpatialGrid();
                    break;
                default:
                    break;
            }

            DoDebugAtSample();
            texture.Apply();
        }

        void DoDebugAtSample()
        {
            Vector2 screenPos = Input.mousePosition;
            Vector2 worldPos = MapScreenToWorldSpace(screenPos);
            if (!keepParticleOnClick || Input.GetMouseButtonDown(0))
                selectedParticleIndex = GetNearestParticleIndex(worldPos);

            if (useParticlePosition)
            {
                worldPos = simulation.Positions[selectedParticleIndex];
                screenPos = MapWorldToScreenSpace(worldPos);
                DrawCircle(screenPos, 5, 1, new(1, 0, 0, 0.4f));
            }

            StringBuilder sb = new StringBuilder();
            if (useParticlePosition && displayParticleIndex)
                sb.Append($"Particle Index: {selectedParticleIndex}\n");

            if (displayPosition)
                sb.Append($"Position: {worldPos}\n");

            if (useParticlePosition && displayParticleVelocity)
                DrawPath(Color.red, screenPos, screenPos + MapWorldToScreenSpaceDir(simulation.Velocities[selectedParticleIndex]) * velocityScale);

            if (displayHighestVelocity)
                sb.Append($"Highest Velocity: {simulation.Velocities.Max(v => v.magnitude)}\n");

            if (displayDensity && useParticlePosition)
                sb.Append($"Density: {simulation.ComputeDensity(selectedParticleIndex)}\n");

            // if (displayPressureForce)
            // {
            //     Vector2 pressureForce = useParticlePosition ? simulation.PressureForces[selectedParticleIndex]
            //         : CalculatePressureForceAt(ref worldPos);
            //     DrawPath(Color.magenta, screenPos, screenPos + MapWorldToScreenSpaceDir(pressureForce) * pressureForceScale);
            // }

            if (displayVelocityLaplacian)
            {
                Vector2 velocityLaplacian = useParticlePosition ? simulation.ComputeVelocityLaplacian(selectedParticleIndex)
                    : CalculateVelocityLaplacianAt(ref worldPos);
                DrawPath(Color.yellow, screenPos, screenPos + MapWorldToScreenSpaceDir(velocityLaplacian) * velocityLaplacianScale);
            }

            // {
            //     float density = 0;
            //     worldPos = simulation.Positions[selectedParticleIndex];
            //     foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
            //     {
            //         int wrappedHash = SpatialGridHelper.CalcWrappedHash(worldPos, neighbour, simulation.smoothingRadius, simulation.spatialLookupSize);
            //         for (int j = simulation.SpatialLookup[wrappedHash];
            //             j < simulation.NumParticles && simulation.SpatialHashes[j].x == wrappedHash; j++)
            //         {
            //             float dist = (simulation.Positions[j] - worldPos).magnitude;
            //             if (dist > simulation.smoothingRadius) continue;

            //             density += simulation.Masses[j] * Kernels.SpikyKernel(dist, simulation.smoothingRadius);
            //             screenPos = MapWorldToScreenSpace(simulation.Positions[j]);
            //             DrawCircle(screenPos, 5, 1, new(1, 0, 0, 0.4f));
            //         }
            //     }
            //     Debug.Log($"Density: {density}");
            // }

            debugText.text = sb.ToString();
        }

        int GetNearestParticleIndex(Vector2 worldPos)
        {
            int index = -1;
            float dist = float.MaxValue;
            for (int i = 0; i < simulation.NumParticles; i++)
            {
                float distance = (simulation.Positions[i] - worldPos).magnitude;
                if (distance < dist)
                {
                    dist = distance;
                    index = i;
                }
            }
            return index;
        }

        void DrawInterpolateProperty()
        {
            Color[] pixels = new Color[width * height];
            Parallel.For(0, width, x =>
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 pos = MapScreenToWorldSpace(new(x, y));

                    float total = 0;
                    for (int i = 0; i < simulation.NumParticles; i++)
                    {
                        float distance = (simulation.Positions[i] - pos).magnitude;
                        float weight = Kernels.SpikyKernel(distance, simulation.smoothingRadius);
                        float property = CalcPropertyAt(simulation.Positions[i]);
                        float density = simulation.Densities[i];
                        total += weight * property * simulation.Masses[i] / density;
                    }

                    pixels[x + width * y] = new(total, total, total);
                }
            });
            texture.SetPixels(pixels);
        }

        void DrawPropertyToInterpolate()
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 pos = MapScreenToWorldSpace(new(x, y));
                    float color = CalcPropertyAt(pos);
                    texture.SetPixel(x, y, new Color(color, color, color));
                }
            }
        }

        float CalcPropertyAt(Vector2 pos)
        {
            return Mathf.Sin(pos.y / 8 + Mathf.Cos(pos.x / 8)) * 0.5f + 0.5f;
        }

        void DrawDensities()
        {
            Color[] pixels = new Color[width * height];
            Parallel.For(0, width, x =>
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 pos = MapScreenToWorldSpace(new(x, y));
                    // float density = simulation.ComputeDensity(ref pos) * densityMultiplier;
                    float density = 0;
                    pixels[x + width * y] = new(density, density, density);
                }
            });
            texture.SetPixels(pixels);
        }

        void DrawPressureGradients()
        {
            // for (int i = 0; i < simulation.NumParticles; i++)
            // {
            //     Vector2 screenPos = MapWorldToScreenSpace(simulation.Positions[i]);
            //     Vector2 gradient = MapWorldToScreenSpace(simulation.PressureForces[i]) * pressureForceScale;
            //     DrawPath(Color.red, screenPos, screenPos + gradient);
            // }
        }

        void DrawVelocityLaplacians()
        {
            for (int i = 0; i < simulation.NumParticles; i++)
            {
                Vector2 screenPos = MapWorldToScreenSpace(simulation.Positions[i]);
                Vector2 laplacian = MapWorldToScreenSpace(simulation.ComputeVelocityLaplacian(i)) * velocityLaplacianScale;
                DrawPath(Color.red, screenPos, screenPos + laplacian);
            }
        }

        void DrawSpatialGrid()
        {
            Vector2 mouseWorld = MapScreenToWorldSpace(Input.mousePosition);
            int wrappedHashAtMouse = SpatialGridHelper.CalcWrappedHash(mouseWorld, simulation.smoothingRadius, simulation.spatialLookupSize);

            Color[] pixels = new Color[width * height];
            Parallel.For(0, width, x =>
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 pixelWorld = MapScreenToWorldSpace(new(x, y));
                    int wrappedHash = SpatialGridHelper.CalcWrappedHash(pixelWorld, simulation.smoothingRadius, simulation.spatialLookupSize);

                    pixels[x + width * y] = wrappedHash == wrappedHashAtMouse ? new(0, 1, 0, backgroundColor.a) : backgroundColor;
                }
            });
            texture.SetPixels(pixels);

            for (int i = simulation.SpatialLookup[wrappedHashAtMouse];
                i < simulation.NumParticles && simulation.SpatialHashes[i].x == wrappedHashAtMouse; i++)
            {
                Vector2 pos = simulation.Positions[i];
                Vector2 screenPos = MapWorldToScreenSpace(pos);
                DrawCircle(screenPos, 5, 1, new(1, 0, 0, backgroundColor.a));
            }
        }

        // Vector2 CalculatePressureForceAt(ref Vector2 worldPos)
        // {
        //     Vector2 force = Vector2.zero;
        //     float densityI = simulation.ComputeDensity(ref worldPos);
        //     float pressureI = simulation.DensityToPressure(densityI);

        //     foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
        //     {
        //         int wrappedHash = SpatialGridHelper.CalcWrappedHash(worldPos, neighbour, simulation.smoothingRadius, simulation.spatialLookupSize);
        //         for (int j = simulation.SpatialLookup[wrappedHash];
        //             j < simulation.NumParticles && simulation.SpatialHashes[j].x == wrappedHash; j++)
        //         {
        //             Vector2 dir = simulation.Positions[j] - worldPos;
        //             float distance = dir.magnitude;
        //             if (distance == 0) continue;

        //             float densityJ = simulation.Densities[j];
        //             float symmetricPressure = simulation.SymmetrizeQuantity(densityI, pressureI, densityJ, simulation.DensityToPressure(densityJ));
        //             float weight = Kernels.SpikyKernelSlope(distance, simulation.smoothingRadius);
        //             force += weight * symmetricPressure * dir * simulation.Masses[j] / densityJ;
        //         }
        //     }
        //     return force;
        // }

        Vector2 CalculateVelocityLaplacianAt(ref Vector2 worldPos)
        {
            Vector2 laplacian = Vector2.zero;

            foreach (Vector2 neighbour in SpatialGridHelper.Neighbors)
            {
                int wrappedHash = SpatialGridHelper.CalcWrappedHash(worldPos, neighbour, simulation.smoothingRadius, simulation.spatialLookupSize);
                for (int j = simulation.SpatialLookup[wrappedHash];
                    j < simulation.NumParticles && simulation.SpatialHashes[j].x == wrappedHash; j++)
                {
                    float distance = Mathf.Max((simulation.Positions[j] - worldPos).magnitude, 0.0001f);
                    float weightSlope = Kernels.SpikyKernelSlope(distance, simulation.smoothingRadius);
                    float weight = 2 * weightSlope / distance;
                    laplacian += simulation.Masses[j] / simulation.Densities[j] * weight * -simulation.Velocities[j];
                }
            }
            return -laplacian;
        }
    }

    public enum DebugView
    {
        None,
        InterpolateProperty,
        PropertyToInterpolate,
        Density,
        DensityAndGradients,
        DensityGradients,
        VelocityLaplacians,
        SpatialGrid
    }
}
