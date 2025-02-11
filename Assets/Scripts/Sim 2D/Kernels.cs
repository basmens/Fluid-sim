using System;
using UnityEngine;

namespace Simulation2D
{
    public class Kernels
    {
        public static float SpikyKernel(float x, float h)
        {
            if (x > h) return 0;
            double value = (double)h - x;
            value *= value * value;
            double volume = Math.PI / 10 * Math.Pow(h, 5);
            return (float)(value / volume);
        }

        public static float SpikyKernelSlope(float x, float h)
        {
            if (x > h) return 0;
            double value = (double)h - x;
            value *= 3 * value;
            double volume = Math.PI / 10 * Math.Pow(h, 5);
            return (float)(value / volume);
        }

        // public static float ViscosityKernel(float x, float h)
        // {
        //     // Double integral of (h ^ 2 - x ^ 2) ^ 3
        //     if (x > h) return 0;
        //     double value = -6 * Math.Pow(h, 4) + 36 * Math.Pow(h, 2) * Math.Pow(x, 2) - 30 * Math.Pow(x, 4);
        //     double volume = Math.PI / 4 * Math.Pow(h, 8);
        //     return (float)(value / volume);
        // }

        public static float RoundedKernel(float x, float h) {
            if (x > h) return 0;
            double value = (double)h * h - (double)x * x;
            value *= value * value;
            double volume = Math.PI / 4 * Math.Pow(h, 8);
            return (float)(value / volume);
        }

        public static float RoundedKernelSlope(float x, float h) {
            if (x > h) return 0;
            double value = (double)h * h - (double)x * x;
            value *= 6 * value * x;
            double volume = Math.PI / 4 * Math.Pow(h, 8);
            return (float)(value / volume);
        }
    }
}
