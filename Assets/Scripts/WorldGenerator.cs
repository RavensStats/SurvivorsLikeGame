using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour {
    public static WorldGenerator Instance { get; private set; }

    public GameObject floorPrefab;
    public List<BiomeData> biomes;
    [Tooltip("Whether distant chunks are destroyed to save memory")]
    public bool enableChunkUnloading = false;
    [Tooltip("Chunks beyond this Manhattan distance from the player are unloaded")]
    public int unloadRadius = 5;
    [Tooltip("How many chunks out to spawn in each direction (2 = 5x5 grid, 3 = 7x7 grid)")]
    public int spawnRadius = 3;
    [Tooltip("Scale of the Perlin noise used for biome blending. Higher = smaller biomes.")]
    public float biomeNoiseScale = 0.025f;

    // Per-run noise offset – randomised in StartGame() so every run starts in a different biome.
    private float _biomeOffsetX;
    private float _biomeOffsetY;

    private Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
    private Vector2Int lastPlayerCell  = new Vector2Int(-999, -999);
    // Visual cell is offset by half a chunk so it flips at the sprite edge, not the logical origin.
    private Vector2Int _lastVisualCell = new Vector2Int(-999, -999);
    private string _lastBiomeName = null;
    private bool _gameActive = false;
    private bool _poiActive  = false; // true while player is inside a POI

    // Expose chunk data for enemy spawner
    public IEnumerable<Vector2Int> ActiveChunkKeys => chunks.Keys;
    public Vector3 GetChunkWorldCenter(Vector2Int key) => new Vector3(key.x * 30f + 15f, key.y * 30f + 15f, 0f);
    public const float ChunkSize = 30f;

    void Awake() {
        Instance = this;
    }

    /// <summary>
    /// Resets and starts the world. Call this from OnCharacterSelected and OnPlayAgain.
    /// Clears all existing chunks, resets biome state, teleports the player to origin,
    /// and pre-warms the banner canvas so the first show has no layout delay.
    /// </summary>
    public void StartGame() {
        // Destroy all chunks from any previous run
        foreach (var kvp in chunks)
            if (kvp.Value != null) Destroy(kvp.Value);
        chunks.Clear();

        lastPlayerCell = new Vector2Int(-999, -999);
        _lastVisualCell = new Vector2Int(-999, -999);
        _lastBiomeName = null;
        _poiActive = false;

        // Randomise biome noise offset so each run starts in a different biome area.
        _biomeOffsetX = Random.Range(0f, 9000f);
        _biomeOffsetY = Random.Range(0f, 9000f);

        // Teleport player back to origin for a clean start
        if (SurvivorMasterScript.Instance != null && SurvivorMasterScript.Instance.player != null)
            SurvivorMasterScript.Instance.player.position = Vector3.zero;

        // Pre-warm the banner canvas so the first biome show has no setup delay
        BiomePOIBanner.EnsureInstance();

        _gameActive = true;
    }

    // Called by POIInstance to suppress biome banner while a POI name is being displayed.
    public void SetPoiActive(bool active) => _poiActive = active;

    // Re-shows the current biome banner; called when the player exits a POI.
    public void ShowCurrentBiome() {
        if (!string.IsNullOrEmpty(_lastBiomeName))
            BiomePOIBanner.Show(_lastBiomeName);
    }

    void Update() {
        if (!_gameActive) return;
        var playerPos = SurvivorMasterScript.Instance.player.position;

        // ── Chunk management (logical cell, exact boundary) ──────────────────
        Vector2Int currentCell = new Vector2Int(
            Mathf.FloorToInt(playerPos.x / 30f),
            Mathf.FloorToInt(playerPos.y / 30f)
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

        // ── Biome banner (visual cell, +half-chunk offset for centre-anchored sprites) ──
        if (!_poiActive) {
            Vector2Int visualCell = new Vector2Int(
                Mathf.FloorToInt((playerPos.x + 15f) / 30f),
                Mathf.FloorToInt((playerPos.y + 15f) / 30f)
            );
            if (visualCell != _lastVisualCell) {
                _lastVisualCell = visualCell;
                string newBiome = GetBiomeNameForCell(visualCell);
                if (newBiome != null && newBiome != _lastBiomeName) {
                    _lastBiomeName = newBiome;
                    BiomePOIBanner.Show(newBiome);
                }
            }
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

        // Perlin noise biome selection — offset by random run seed + 1000 to avoid the 0,0 flat spot
        float noise = Mathf.PerlinNoise(
            (c.x + _biomeOffsetX + 1000) * biomeNoiseScale,
            (c.y + _biomeOffsetY + 1000) * biomeNoiseScale);
        int biomeIndex = Mathf.Clamp(Mathf.FloorToInt(noise * biomes.Count), 0, biomes.Count - 1);
        sr.color = biomes[biomeIndex].groundColor;
        string biomeName = biomes[biomeIndex].biomeName;

        // 5% chance to place a POI on this chunk
        if (Random.value < 0.05f) SpawnPOI(p + new Vector3(15f, 15f, 0f));
    }

    // Returns the biome index at the given world position using the current run's noise offset.
    // EnemySpawner uses this so biome multipliers stay consistent with rendered biome colours.
    public int GetBiomeIndex(Vector3 worldPos) {
        if (biomes == null || biomes.Count == 0) return 0;
        float noise = Mathf.PerlinNoise(
            (worldPos.x / ChunkSize + _biomeOffsetX + 1000f) * biomeNoiseScale,
            (worldPos.y / ChunkSize + _biomeOffsetY + 1000f) * biomeNoiseScale);
        return Mathf.Clamp(Mathf.FloorToInt(noise * biomes.Count), 0, biomes.Count - 1);
    }

    // Returns the biome name for a chunk grid cell using the exact same Perlin formula as Spawn().
    string GetBiomeNameForCell(Vector2Int cell) {
        if (biomes == null || biomes.Count == 0) return null;
        float noise = Mathf.PerlinNoise(
            (cell.x + _biomeOffsetX + 1000f) * biomeNoiseScale,
            (cell.y + _biomeOffsetY + 1000f) * biomeNoiseScale);
        int idx = Mathf.Clamp(Mathf.FloorToInt(noise * biomes.Count), 0, biomes.Count - 1);
        return biomes[idx].biomeName;
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

