using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour {
    public GameObject floorPrefab;
    public List<BiomeData> biomes;
    [Tooltip("Whether distant chunks are destroyed to save memory")]
    public bool enableChunkUnloading = false;
    [Tooltip("Chunks beyond this Manhattan distance from the player are unloaded")]
    public int unloadRadius = 5;
    [Tooltip("How many chunks out to spawn in each direction (2 = 5x5 grid, 3 = 7x7 grid)")]
    public int spawnRadius = 3;
    [Tooltip("Scale of the Perlin noise used for biome blending")]
    public float biomeNoiseScale = 0.015f;

    private Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
    private Vector2Int lastPlayerCell = new Vector2Int(-999, -999);

    // Expose chunk data for enemy spawner
    public IEnumerable<Vector2Int> ActiveChunkKeys => chunks.Keys;
    public Vector3 GetChunkWorldCenter(Vector2Int key) => new Vector3(key.x * 30f + 15f, key.y * 30f + 15f, 0f);
    public const float ChunkSize = 30f;

    void Update() {
        Vector2Int currentCell = new Vector2Int(
            Mathf.FloorToInt(SurvivorMasterScript.Instance.player.position.x / 30f),
            Mathf.FloorToInt(SurvivorMasterScript.Instance.player.position.y / 30f)
        );

        if (currentCell != lastPlayerCell) {
            // Spawn chunks around the player
            for (int x = -spawnRadius; x <= spawnRadius; x++)
                for (int y = -spawnRadius; y <= spawnRadius; y++)
                    Spawn(currentCell + new Vector2Int(x, y));

            // Unload chunks that are too far away
            if (enableChunkUnloading) {
                var toRemove = new List<Vector2Int>();
                foreach (var kvp in chunks) {
                    Vector2Int delta = kvp.Key - currentCell;
                    if (Mathf.Abs(delta.x) > unloadRadius || Mathf.Abs(delta.y) > unloadRadius)
                        toRemove.Add(kvp.Key);
                }
                foreach (var key in toRemove) {
                    Destroy(chunks[key]);
                    chunks.Remove(key);
                }
            }

            lastPlayerCell = currentCell;
        }
    }

    void Spawn(Vector2Int c) {
        if (chunks.ContainsKey(c)) return;
        Vector3 p = new Vector3(c.x * 30, c.y * 30, 0);
        GameObject chunk = Instantiate(floorPrefab, p, Quaternion.identity, transform);
        chunks.Add(c, chunk);

        SpriteRenderer sr = chunk.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) {
            Debug.LogWarning($"[WorldGenerator] FloorChunk at {c} has no SpriteRenderer in children.", chunk);
            return;
        }
        if (sr.sprite == null) {
            Debug.LogWarning($"[WorldGenerator] SpriteRenderer on FloorChunk at {c} has no Sprite assigned.", chunk);
        }

        // Perlin noise biome selection — offset by 1000 to avoid the 0,0 flat spot
        float noise = Mathf.PerlinNoise((c.x + 1000) * biomeNoiseScale, (c.y + 1000) * biomeNoiseScale);
        int biomeIndex = Mathf.Clamp(Mathf.FloorToInt(noise * biomes.Count), 0, biomes.Count - 1);
        sr.color = biomes[biomeIndex].groundColor;
    }
}

public class POIInstance : MonoBehaviour {
    public POIType type;
    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Player")) return;
        switch (type) {
            case POIType.Graveyard: SurvivorMasterScript.Instance.isInsideGraveyard = true; break;
            case POIType.HealingSpring: SurvivorMasterScript.Instance.playerHP = Mathf.Min(100, SurvivorMasterScript.Instance.playerHP + 25); break;
            case POIType.ManaWell: SurvivorMasterScript.Instance.ResetUltimate(); break;
        }
    }
    void OnTriggerExit2D(Collider2D other) { if (other.CompareTag("Player") && type == POIType.Graveyard) SurvivorMasterScript.Instance.isInsideGraveyard = false; }
}