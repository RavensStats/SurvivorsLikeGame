using System.Collections.Generic;
using UnityEngine;

public class SpatialGrid {
    private float cellSize;
    // Maps a 2D Grid Coordinate to a list of enemies currently in that square
    private Dictionary<Vector2Int, List<EnemyEntity>> cells = new Dictionary<Vector2Int, List<EnemyEntity>>();

    public SpatialGrid(float size) {
        cellSize = size;
    }

    // Called by EnemyEntity.Update() to keep the grid accurate as they move
    public void UpdateEntity(EnemyEntity entity, Vector3 position) {
        Vector2Int newCell = new Vector2Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.y / cellSize)
        );

        if (entity.currentCell != newCell) {
            // Remove from old cell
            if (cells.ContainsKey(entity.currentCell)) {
                cells[entity.currentCell].Remove(entity);
            }

            // Add to new cell
            if (!cells.ContainsKey(newCell)) {
                cells[newCell] = new List<EnemyEntity>();
            }

            cells[newCell].Add(entity);
            entity.currentCell = newCell;
        }
    }

    // Called when an enemy dies to stop tracking them
    public void Remove(EnemyEntity entity) {
        if (cells.ContainsKey(entity.currentCell)) {
            cells[entity.currentCell].Remove(entity);
        }
    }

    // Used by Ultimates and Weapons to find targets efficiently
    public List<EnemyEntity> GetNearby(Vector3 position) {
        List<EnemyEntity> nearbyEntities = new List<EnemyEntity>();
        GetNearby(position, nearbyEntities);
        return nearbyEntities;
    }

    // Allocation-free overload: clears and fills the caller-supplied list so no
    // heap allocation is needed on every call. Use this in hot paths.
    public void GetNearby(Vector3 position, List<EnemyEntity> result) {
        result.Clear();
        Vector2Int centerCell = new Vector2Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.y / cellSize)
        );

        // Checks the center cell and all 8 surrounding cells
        for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
                Vector2Int targetCell = centerCell + new Vector2Int(x, y);
                if (cells.ContainsKey(targetCell)) {
                    result.AddRange(cells[targetCell]);
                }
            }
        }
    }
}