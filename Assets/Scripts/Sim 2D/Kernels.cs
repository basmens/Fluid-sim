using UnityEngine;

namespace Simulation2D
{
    public class Kernels
    {
        public static float SquareKernel(float x, float h)
        {
            if (x > h) return 0;
            float value = h - x;
            value *= value;
            float volume = Mathf.PI / 6 * h * h * h * h;
            return value / volume;
        }

        public static float SquareKernelSlope(float x, float h)
        {
            if (x > h) return 0;
            float value = -2 * (h - x);
            float volume = Mathf.PI / 6 * h * h * h * h;
            return value / volume;
        }
    }
}
