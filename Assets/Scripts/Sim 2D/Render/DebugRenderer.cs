using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Simulation2D
{
    public class DebugRenderer : RawImageRenderer
    {
        [Header("Debug")]
        public DebugView debugView;

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
                        ref Particle p = ref simulation.Particles[i];
                        float distance = (p.position - pos).magnitude;
                        float weight = Kernels.SquareKernel(distance, simulation.smoothingRadius);
                        float property = CalcPropertyAt(p.position);
                        float density = simulation.Densities[i];
                        total += weight * property * p.mass / density;
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
                    float density = simulation.CalculateDensity(ref pos);
                    pixels[x + width * y] = new(density, density, density);
                }
            });
            texture.SetPixels(pixels);
        }
    }

    public enum DebugView
    {
        None,
        InterpolateProperty,
        PropertyToInterpolate,
        Density
    }
}
