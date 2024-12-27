using UnityEngine;

namespace Simulation2D
{
    public class Kernels
    {

        public static double SquareKernel(float x, float h)
        {
            float y = Mathf.Pow((h - x), 2);
            
            return Mathf.Max(y, 0);
        }
    }
}
