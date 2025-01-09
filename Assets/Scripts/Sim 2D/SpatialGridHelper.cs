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
            return cellIndex.x * 15823 + cellIndex.y * 9737333;
        }

        public static int WrapCellHash(int hash, int spatialLookupSize) {
            return (hash % spatialLookupSize + spatialLookupSize) % spatialLookupSize;
        }
        
        public static int CalcWrappedHash(Vector2 pos, float cellSize, int spatialLookupSize) {
            Vector2Int cellIndex = PositionToCellIndex(pos, cellSize);
            int hash = HashCellIndex(cellIndex);
            return WrapCellHash(hash, spatialLookupSize);
        }

        public static int CalcWrappedHash(Vector2 pos, Vector2 neighbour, float cellSize, int spatialLookupSize) {
            return CalcWrappedHash(pos + neighbour * cellSize, cellSize, spatialLookupSize);
        }
    }
}
