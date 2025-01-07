using UnityEngine;

namespace Simulation2D
{
    public class SpatialGridHelper
    {
        public static Vector2[] Neighbors = {
            new(0, 0),
            new(0, 1),
            new(0, -1),
            new(1, 0),
            new(1, 1),
            new(1, -1),
            new(-1, 0),
            new(-1, 1),
            new(-1, -1),
        };

        public static Vector2Int PositionToCellIndex(Vector2 position, float cellSize) {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.y / cellSize)
            );
        }

        public static int HashCellIndex(Vector2Int cellIndex) {
            return cellIndex.x * 467 + cellIndex.y * 7919;
        }

        public static int WrapCellHash(int hash, int numParticles) {
            return (hash % numParticles + numParticles) % numParticles;
        }
        
        public static int CalcWrappedHash(Vector2 pos, float cellSize, int numParticles) {
            Vector2Int cellIndex = PositionToCellIndex(pos, cellSize);
            int hash = HashCellIndex(cellIndex);
            return WrapCellHash(hash, numParticles);
        }

        public static int CalcWrappedHash(Vector2 pos, Vector2 neighbour, float cellSize, int numParticles) {
            return CalcWrappedHash(pos + neighbour * cellSize, cellSize, numParticles);
        }
    }
}
