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

        // 5% chance to place a POI on this chunk
        if (Random.value < 0.05f) SpawnPOI(p + new Vector3(15f, 15f, 0f));
    }

    void SpawnPOI(Vector3 pos) {
        // Only the three POI types that have implemented logic get spawned
        POIType[] implemented = { POIType.Graveyard, POIType.HealingSpring, POIType.ManaWell };
        POIType chosen = implemented[Random.Range(0, implemented.Length)];

        GameObject poi = new GameObject($"POI_{chosen}");
        poi.transform.position = pos;
        poi.transform.SetParent(transform, true);

        // Visual indicator: small colored circle using SpriteRenderer
        SpriteRenderer sr = poi.AddComponent<SpriteRenderer>();
        sr.sprite       = MakeCircleSprite(32);
        sr.sortingOrder = 1; // below floor objects; gameplay (enemies/gems/player) render above
        sr.color = chosen == POIType.Graveyard     ? new Color(0.5f, 0.2f, 0.7f, 0.9f)
                 : chosen == POIType.HealingSpring ? new Color(0.2f, 0.9f, 0.4f, 0.9f)
                 : /* ManaWell */                    new Color(0.2f, 0.5f, 1.0f, 0.9f);
        poi.transform.localScale = Vector3.one * 24f; // doubled radius

        // Trigger collider so the player activates it on contact
        CircleCollider2D col = poi.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 0.5f;

        POIInstance inst = poi.AddComponent<POIInstance>();
        inst.type = chosen;
    }

    // Procedurally build a white filled-circle sprite of the given diameter in pixels.
    static Sprite MakeCircleSprite(int diameter) {
        Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = diameter * 0.5f;
        for (int y = 0; y < diameter; y++) {
            for (int x = 0; x < diameter; x++) {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float a  = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy)); // anti-aliased edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), diameter);
    }
}

public class POIInstance : MonoBehaviour {
    public POIType type;
    void OnTriggerEnter2D(Collider2D other) {
        if (!other.CompareTag("Player")) return;
        switch (type) {
            case POIType.Graveyard: SurvivorMasterScript.Instance.isInsideGraveyard = true; break;
            case POIType.HealingSpring: SurvivorMasterScript.Instance.playerHP = Mathf.Min(100, SurvivorMasterScript.Instance.playerHP + 25); break;
            case POIType.ManaWell: SurvivorMasterScript.Instance.FillUltimate(); break;
        }
    }
    void OnTriggerExit2D(Collider2D other) { if (other.CompareTag("Player") && type == POIType.Graveyard) SurvivorMasterScript.Instance.isInsideGraveyard = false; }
}