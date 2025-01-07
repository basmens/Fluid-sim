using System.Threading.Tasks;
using UnityEngine;

namespace Simulation2D
{
    public class DebugRenderer : RawImageRenderer
    {
        [Header("Debug")]
        public DebugView debugView;
        public float densityMultiplier = 0.01f;
        public bool drawPressureGradients;
        public float pressureGradientScale = 100;

        void LateUpdate()
        {
            switch (debugView)
            {
                case DebugView.InterpolateProperty:
                    DrawInterpolateProperty();
                    break;
                case DebugView.PropertyToInterpolate:
                    DrawPropertyToInterpolate();
                    break;
                case DebugView.Density:
                    DrawDensities();
                    if (drawPressureGradients) DrawPressureGradients();
                    break;
                case DebugView.SpatialGrid:
                    DrawSpatialGrid();
                    break;
                default:
                    SetBackgroundColor(backgroundColor);
                    break;
            }

            texture.Apply();
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
                        float weight = Kernels.DensityKernel(distance, simulation.smoothingRadius);
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
            simulation.IterateSimulation(0);
            Color[] pixels = new Color[width * height];
            Parallel.For(0, width, x =>
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 pos = MapScreenToWorldSpace(new(x, y));
                    float density = simulation.CalculateDensity(ref pos) * densityMultiplier;
                    pixels[x + width * y] = new(density, density, density);
                }
            });
            texture.SetPixels(pixels);
        }

        void DrawPressureGradients()
        {
            for (int i = 0; i < simulation.NumParticles; i++)
            {
                Vector2 screenPos = MapWorldToScreenSpace(simulation.Positions[i]);
                Vector2 gradient = simulation.CalculatePressureForce(i) * pressureGradientScale;
                DrawPath(Color.red, screenPos, screenPos + gradient);
            }
        }

        void DrawSpatialGrid()
        {
            simulation.IterateSimulation(0);

            Vector2 mouseWorld = MapScreenToWorldSpace(Input.mousePosition);
            int wrappedHashAtMouse = SpatialGridHelper.CalcWrappedHash(mouseWorld, simulation.smoothingRadius, simulation.NumParticles);

            Color[] pixels = new Color[width * height];
            Parallel.For(0, width, x =>
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 pixelWorld = MapScreenToWorldSpace(new(x, y));
                    int wrappedHash = SpatialGridHelper.CalcWrappedHash(pixelWorld, simulation.smoothingRadius, simulation.NumParticles);

                    pixels[x + width * y] = wrappedHash == wrappedHashAtMouse ? new(0, 1, 0, backgroundColor.a) : backgroundColor;
                }
            });
            texture.SetPixels(pixels);

            for (int i = simulation.SpatialLookup[wrappedHashAtMouse];
                i < simulation.NumParticles && simulation.SpatialHashes[i].x == wrappedHashAtMouse; i++)
            {
                Vector2 pos = simulation.Positions[simulation.SpatialHashes[i].y];
                Vector2 screenPos = MapWorldToScreenSpace(pos);
                DrawCircle(screenPos, 5, 1, new(1, 0, 0, backgroundColor.a));
            }
        }
    }

    public enum DebugView
    {
        None,
        InterpolateProperty,
        PropertyToInterpolate,
        Density,
        SpatialGrid
    }
}
