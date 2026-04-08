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
        // All 20 implemented POI types
        POIType[] all = (POIType[])System.Enum.GetValues(typeof(POIType));
        POIType chosen = all[Random.Range(0, all.Length)];

        GameObject poi = new GameObject($"POI_{chosen}");
        poi.transform.position = pos;
        poi.transform.SetParent(transform, true);

        // Visual indicator: colored circle
        SpriteRenderer sr = poi.AddComponent<SpriteRenderer>();
        sr.sprite       = MakeCircleSprite(32);
        sr.sortingOrder = 1;
        sr.color = POIColor(chosen);
        poi.transform.localScale = Vector3.one * 24f;

        // Trigger collider — player activates on contact
        CircleCollider2D col = poi.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius    = 0.5f;

        POIInstance inst = poi.AddComponent<POIInstance>();
        inst.type = chosen;
    }

    static Color POIColor(POIType t) {
        switch (t) {
            case POIType.Graveyard:      return new Color(0.5f, 0.2f, 0.7f, 0.9f);
            case POIType.Forge:          return new Color(1.0f, 0.4f, 0.1f, 0.9f);
            case POIType.HolyShrine:     return new Color(1.0f, 1.0f, 0.6f, 0.9f);
            case POIType.CursedAltar:    return new Color(0.3f, 0.0f, 0.3f, 0.9f);
            case POIType.ManaWell:       return new Color(0.2f, 0.5f, 1.0f, 0.9f);
            case POIType.MerchantCart:   return new Color(1.0f, 0.85f, 0.0f, 0.9f);
            case POIType.AncientLibrary: return new Color(0.6f, 0.4f, 0.1f, 0.9f);
            case POIType.HealingSpring:  return new Color(0.2f, 0.9f, 0.4f, 0.9f);
            case POIType.ScrapHeap:      return new Color(0.5f, 0.5f, 0.5f, 0.9f);
            case POIType.VolcanicVent:   return new Color(1.0f, 0.2f, 0.0f, 0.9f);
            case POIType.FrozenObelisk:  return new Color(0.5f, 0.8f, 1.0f, 0.9f);
            case POIType.ThievesDen:     return new Color(0.7f, 0.6f, 0.1f, 0.9f);
            case POIType.Monolith:       return new Color(0.3f, 0.3f, 0.3f, 0.9f);
            case POIType.RadarStation:   return new Color(0.1f, 0.7f, 0.7f, 0.9f);
            case POIType.ToxicPit:       return new Color(0.3f, 0.7f, 0.1f, 0.9f);
            case POIType.Beehive:        return new Color(1.0f, 0.75f, 0.0f, 0.9f);
            case POIType.GoldenStatue:   return new Color(1.0f, 0.9f, 0.2f, 0.9f);
            case POIType.TimeRift:       return new Color(0.7f, 0.1f, 1.0f, 0.9f);
            case POIType.Overgrowth:     return new Color(0.0f, 0.5f, 0.1f, 0.9f);
            case POIType.Meteorite:      return new Color(0.6f, 0.3f, 0.1f, 0.9f);
            default:                     return new Color(1f, 1f, 1f, 0.9f);
        }
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