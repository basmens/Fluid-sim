using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Simulation2D
{
    public class DebugRenderer : RawImageRenderer
    {
        [Header("Debug")]
        public DebugView debugView;

        void LateUpdate()
        {
            // Clear texture
            SetBackgroundColor(backgroundColor);

            // Render particles
            switch (debugView)
            {
                case DebugView.InterpolateProperty:
                    DrawInterpolateProperty();
                    break;
                case DebugView.Density:
                    DrawDensities();
                    break;
                default:
                    break;
            }

            texture.Apply();
        }

        void DrawInterpolateProperty()
        {
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    Vector2 pos = MapScreenToWorldSpace(new(x, y));
                    texture.SetPixel(x, y, new Color(pos.x, pos.y, 0));
                }
            }
        }

        void DrawDensities()
        {
            SetBackgroundColor(new Color(0.5f, 0, 0.8f));
        }
    }

    public enum DebugView
    {
        None,
        InterpolateProperty,
        Density
    }
}
